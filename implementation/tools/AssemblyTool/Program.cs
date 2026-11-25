// SPDX-License-Identifier: AGPL-3.0-only
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using System.Security.Cryptography;

if (args.Length < 2) throw new ArgumentException("inspect|resource|crc|bridge input [arguments]");
using var resolver = new DefaultAssemblyResolver();
string inputDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
resolver.AddSearchDirectory(inputDirectory);
string resources = Environment.GetEnvironmentVariable("BSMAC_GAME_RESOURCES")
    ?? Directory.GetParent(inputDirectory)!.FullName;
foreach (string directory in new[] { Path.Combine(resources, "Plugins"), Path.Combine(resources, "Libs"), Path.Combine(resources, "Beat Saber_Data", "Managed") })
    if (Directory.Exists(directory)) resolver.AddSearchDirectory(directory);
using var asm = AssemblyDefinition.ReadAssembly(args[1], new ReaderParameters { InMemory = true, AssemblyResolver = resolver });
IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(Flatten(t.NestedTypes)));
var types = Flatten(asm.MainModule.Types).ToArray();
if (args[0] == "inspect") {
    Console.WriteLine(asm.Name.FullName);
    foreach (var r in asm.MainModule.Resources) Console.WriteLine($"RESOURCE {r.Name}");
    foreach (var t in types.Where(t => args.Length > 2 && t.FullName.Contains(args[2])))
        foreach (var m in t.Methods.Where(m => m.HasBody && (args.Length < 4 || m.Name.Contains(args[3])))) {
            Console.WriteLine($"METHOD {m.FullName}");
            foreach (var i in m.Body.Instructions) Console.WriteLine(i);
        }
    return;
}
if (args[0] == "resource") {
    var r = asm.MainModule.Resources.OfType<EmbeddedResource>().Single(r => r.Name == args[2]);
    File.WriteAllBytes(args[3], r.GetResourceData());
    return;
}
if (asm.Name.HasPublicKey) throw new InvalidOperationException("Refusing strong-named assembly modification");
if (args[0] == "replace-resource") {
    if (args.Length != 7)
        throw new ArgumentException("replace-resource input name original-sha256 derivative derivative-sha256 output");
    var resource = asm.MainModule.Resources.OfType<EmbeddedResource>().Single(r => r.Name == args[2]);
    var replacement = File.ReadAllBytes(args[4]);
    string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    if (!Hash(resource.GetResourceData()).Equals(args[3], StringComparison.OrdinalIgnoreCase) ||
        !Hash(replacement).Equals(args[5], StringComparison.OrdinalIgnoreCase) ||
        !replacement.AsSpan().StartsWith("UnityFS\0"u8))
        throw new InvalidOperationException("Resource or UnityFS derivative hash verification failed");
    asm.MainModule.Resources[asm.MainModule.Resources.IndexOf(resource)] =
        new EmbeddedResource(resource.Name, resource.Attributes, replacement);
} else if (args[0] == "crc") {
    uint old = uint.Parse(args[2]), value = uint.Parse(args[3]);
    var matches = types.SelectMany(t => t.Methods).Where(m => m.HasBody)
        .SelectMany(m => m.Body.Instructions).Where(i => i.OpCode == OpCodes.Ldc_I4 && unchecked((uint)(int)i.Operand) == old).ToArray();
    if (matches.Length != 1) throw new InvalidOperationException($"Expected one CRC constant, found {matches.Length}");
    matches[0].Operand = unchecked((int)value);
} else if (args[0] == "bridge") {
    using var bridge = AssemblyDefinition.ReadAssembly(args[2]);
    var replacement = asm.MainModule.ImportReference(bridge.MainModule.Types.Single(t => t.FullName == "Bsmac.VivifyMetalBridge").Methods.Single(m => m.Name == "Load"));
    var matches = types.Single(t => t.Name == "AssetBundleManager").Methods.Where(m => m.IsConstructor && m.HasBody)
        .SelectMany(m => m.Body.Instructions).Where(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference r &&
        r.DeclaringType.FullName == "UnityEngine.AssetBundle" && r.Name == "LoadFromFile" && r.Parameters.Count == 2 &&
        r.Parameters[0].ParameterType.FullName == "System.String" && r.Parameters[1].ParameterType.FullName == "System.UInt32").ToArray();
    if (matches.Length != 1) throw new InvalidOperationException($"Expected one checked map loader call, found {matches.Length}");
    matches[0].Operand = replacement;
} else if (args[0] == "vivify-auto") {
    if (asm.Name.Name != "Vivify" || asm.Name.Version != new Version(1, 0, 5, 0))
        throw new InvalidOperationException("The automatic preparation patch requires the inspected Vivify 1.0.5 build");
    using var bridge = AssemblyDefinition.ReadAssembly(args[2]);
    var api = bridge.MainModule.Types.Single(t => t.FullName == "Bsmac.AutomaticPreparation");
    MethodReference Hook(string name) => asm.MainModule.ImportReference(api.Methods.Single(m => m.Name == name));
    var type = types.Single(t => t.FullName == "Vivify.Controllers.AssetBundleDownloadViewController");
    foreach (string fieldName in new[] {"_assetDownloader", "_downloadFinished", "_doAbort", "_downloadProgress", "_newView", "_lastError", "_downloadWaiter"})
        if (!type.Fields.Any(f => f.Name == fieldName))
            throw new InvalidOperationException("Vivify preparation field missing: " + fieldName);
    var init = type.Methods.Single(m => m.Name == "Init" && m.Parameters.Count == 1);
    var check = init.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m &&
        m.DeclaringType.FullName == "System.IO.File" && m.Name == "Exists");
    var found = check.Next.Next;
    if (check.Previous.OpCode != OpCodes.Ldloc_2 || init.Body.Variables[2].VariableType.FullName != "System.String" ||
        check.Next.OpCode != OpCodes.Brfalse_S || found.OpCode != OpCodes.Ldc_I4_0 || found.Next.OpCode != OpCodes.Ret)
        throw new InvalidOperationException("Unexpected Vivify existing-bundle branch");
    var initIl = init.Body.GetILProcessor();
    var foundReturn = found.Next;
    found.OpCode = OpCodes.Ldarg_0;
    initIl.InsertBefore(foundReturn, initIl.Create(OpCodes.Ldloc_2));
    initIl.InsertBefore(foundReturn, initIl.Create(OpCodes.Call, Hook("PrepareExisting")));
    var consent = init.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "get_AllowDownload");
    initIl.InsertAfter(consent, initIl.Create(OpCodes.Ldarg_0));
    initIl.InsertAfter(consent.Next, initIl.Create(OpCodes.Call, Hook("SetDownloadConsentView")));
    // Added IL can move existing short branch targets out of range.
    foreach (var op in init.Body.Instructions) {
        if (op.OpCode == OpCodes.Br_S) op.OpCode = OpCodes.Br;
        else if (op.OpCode == OpCodes.Brtrue_S) op.OpCode = OpCodes.Brtrue;
        else if (op.OpCode == OpCodes.Brfalse_S) op.OpCode = OpCodes.Brfalse;
    }
    var download = type.Methods.Single(m => m.Name == "DownloadAndSave" && m.Parameters.Count == 2 &&
        m.ReturnType.FullName == "System.Collections.IEnumerator");
    var downloadIl = download.Body.GetILProcessor();
    var end = download.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
    end.OpCode = OpCodes.Ldarg_0;
    downloadIl.Append(downloadIl.Create(OpCodes.Ldarg_1));
    downloadIl.Append(downloadIl.Create(OpCodes.Ldarg_2));
    downloadIl.Append(downloadIl.Create(OpCodes.Call, Hook("PrepareAfterDownload")));
    downloadIl.Append(downloadIl.Create(OpCodes.Ret));
    var show = type.Methods.Single(m => m.Name == "OnShow" && m.Parameters.Count == 0);
    var start = show.Body.Instructions.Single(i => i.Operand is MethodReference m &&
        m.DeclaringType.FullName == "UnityEngine.MonoBehaviour" && m.Name == "StartCoroutine");
    var pop = start.Next;
    if (!(start.Previous.Operand is MethodReference wait && wait.Name == "WaitForDownload") || pop.OpCode != OpCodes.Pop)
        throw new InvalidOperationException("Unexpected Vivify download waiter shape");
    pop.OpCode = OpCodes.Ldarg_0;
    var showIl = show.Body.GetILProcessor();
    showIl.InsertAfter(pop, showIl.Create(OpCodes.Call, Hook("RememberWaiter")));
    // This view covers both the original download and the local conversion.
    var view = asm.MainModule.Resources.OfType<EmbeddedResource>().Single(r => r.Name == "Vivify.Resources.AssetBundleDownloading.bsml");
    string markup = Encoding.UTF8.GetString(view.GetResourceData());
    const string oldLabel = "Downloading map assets...";
    if (markup.Split(oldLabel, StringSplitOptions.None).Length != 2)
        throw new InvalidOperationException("Unexpected Vivify preparation view label");
    int viewIndex = asm.MainModule.Resources.IndexOf(view);
    asm.MainModule.Resources[viewIndex] = new EmbeddedResource(view.Name, view.Attributes,
        Encoding.UTF8.GetBytes(markup.Replace(oldLabel, "Preparing map assets...", StringComparison.Ordinal)));
} else if (args[0] == "qosmetics-notes") {
    if (asm.Name.Name != "CustomNotes" || asm.Name.Version != new Version(2, 6, 9, 0))
        throw new InvalidOperationException("Expected CustomNotes 2.6.9");
    using var bridge = AssemblyDefinition.ReadAssembly(args[2]);
    var api = bridge.MainModule.Types.Single(t => t.FullName == "Bsmac.Qosmetics.Bridge");
    var load = types.Single(t => t.FullName == "CustomNotes.Managers.NoteAssetLoader")
        .Methods.Single(m => m.Name == "LoadNotePrefab");
    var loadCall = load.Body.Instructions.Single(i => i.Operand is GenericInstanceMethod m &&
        m.Name == "LoadAsset" && m.DeclaringType.FullName == "UnityEngine.AssetBundle" &&
        m.GenericArguments.Single().FullName == "UnityEngine.GameObject");
    if (load.Parameters.Count != 2 || load.Parameters[1].ParameterType.FullName != "System.String" ||
        loadCall.Previous.OpCode != OpCodes.Ldstr || (string)loadCall.Previous.Operand != "assets/_customnote.prefab")
        throw new InvalidOperationException("Unexpected CustomNotes prefab loader");
    load.Body.GetILProcessor().InsertBefore(loadCall, Instruction.Create(OpCodes.Ldarg, load.Parameters[1]));
    loadCall.OpCode = OpCodes.Call;
    loadCall.Operand = asm.MainModule.ImportReference(api.Methods.Single(m => m.Name == "Load"));
    var unload = types.Single(t => t.FullName == "CustomNotes.Models.CustomNote")
        .Methods.Single(m => m.Name == "Destroy").Body.Instructions.Single(i =>
            i.Operand is MethodReference m && m.Name == "Unload" && m.DeclaringType.FullName == "UnityEngine.AssetBundle" &&
            m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Boolean");
    unload.OpCode = OpCodes.Call;
    unload.Operand = asm.MainModule.ImportReference(api.Methods.Single(m => m.Name == "Unload"));
} else if (args[0] == "equipment-auto") {
    using var bridge = AssemblyDefinition.ReadAssembly(args[2]);
    var api = bridge.MainModule.Types.Single(t => t.FullName == "Bsmac.EquipmentPreparation");
    MethodReference Hook(string name) => asm.MainModule.ImportReference(api.Methods.Single(m => m.Name == name));
    string owner, name, targetType, targetName, helper;
    Version version;
    switch (asm.Name.Name) {
        case "CustomNotes":
            version = new Version(2, 6, 9, 0);
            owner = "CustomNotes.Models.CustomNote"; name = "Load";
            targetType = "UnityEngine.AssetBundle"; targetName = "LoadFromFile"; helper = "LoadNote";
            break;
        case "CustomWalls":
            version = new Version(1, 6, 4, 0);
            owner = "CustomWalls.Data.CustomMaterial/<CreateAsync>d__24"; name = "MoveNext";
            targetType = "AssetBundleLoadingTools.Utilities.AssetBundleExtensions";
            targetName = "LoadFromFileAsync"; helper = "LoadAsync";
            break;
        case "CustomSabersLite":
            version = new Version(1, 0, 3, 0);
            owner = "CustomSabersLite.Utilities.Common.BundleLoading/<LoadBundle>d__0"; name = "MoveNext";
            targetType = "AssetBundleLoadingTools.Utilities.AssetBundleExtensions";
            targetName = "LoadFromFileAsync"; helper = "LoadAsync";
            break;
        default: throw new InvalidOperationException("Unsupported equipment assembly");
    }
    if (asm.Name.Version != version)
        throw new InvalidOperationException("The equipment loader version differs from the inspected assembly");
    var method = types.Single(t => t.FullName == owner).Methods.Single(m => m.Name == name && m.HasBody);
    var site = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m &&
        m.DeclaringType.FullName == targetType && m.Name == targetName && m.Parameters.Count == 1 &&
        m.Parameters[0].ParameterType.FullName == "System.String");
    var replacement = Hook(helper);
    if (((MethodReference)site.Operand).ReturnType.FullName != replacement.ReturnType.FullName)
        throw new InvalidOperationException("The equipment loader return type differs");
    site.Operand = replacement;
    if (asm.Name.Name == "CustomNotes") {
        var loader = types.Single(t => t.FullName == "CustomNotes.Managers.NoteAssetLoader");
        var getDirectory = loader.Methods.Single(m => m.Name == "get_NotesDirectory" && m.IsStatic);
        var init = loader.Methods.Single(m => m.Name == "Initialize" && m.HasBody);
        var il = init.Body.GetILProcessor();
        var first = init.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Call, getDirectory));
        il.InsertBefore(first, il.Create(OpCodes.Call, Hook("WatchNotes")));
    }
} else if (args[0] == "beatleader-replay-paths") {
    if (asm.Name.Name != "BeatLeader" || asm.Name.Version != new Version(0, 9, 33, 0))
        throw new InvalidOperationException("Expected the inspected BeatLeader 0.9.33 assembly");
    var cache = types.Single(t => t.FullName == "BeatLeader.Replayer.ReplayerCache")
        .Methods.Single(m => m.Name == ".cctor");
    var cacheFolder = cache.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr &&
        (string)i.Operand == "ReplayerCache\\");
    var userData = cache.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().Single(m =>
        m.DeclaringType.FullName == "IPA.Utilities.UnityGame" && m.Name == "get_UserDataPath");
    var files = types.Single(t => t.FullName == "BeatLeader.Utils.FileManager")
        .Methods.Single(m => m.Name == ".cctor");
    var replayFolder = files.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr &&
        (string)i.Operand == "\\UserData\\BeatLeader\\Replays\\");
    if (replayFolder.Previous.Operand is not MethodReference cwd || cwd.Name != "get_CurrentDirectory" ||
        cwd.DeclaringType.FullName != "System.Environment" ||
        replayFolder.Next.Operand is not MethodReference concat || concat.Name != "Concat" ||
        concat.DeclaringType.FullName != "System.String")
        throw new InvalidOperationException("Unexpected replay storage initializer");
    // Replay recording appends its generated filename to the directory string.
    replayFolder.Previous.Operand = userData;
    replayFolder.Operand = "/BeatLeader/Replays/";
    cacheFolder.Operand = "ReplayerCache/";
} else if (args[0] == "trace") {
    using var bridge = AssemblyDefinition.ReadAssembly(args[2]);
    var trace = asm.MainModule.ImportReference(bridge.MainModule.Types.Single(t => t.FullName == "Bsmac.NativeDiagnostics").Methods.Single(m => m.Name == "Trace"));
    var names = args[3].Split(',');
    foreach (string name in names) {
        var method = types.SelectMany(t => t.Methods).Single(m => m.Name == name && m.HasBody);
        if (method.Body.Instructions.Any(i => i.Operand is MethodReference r && r.FullName == trace.FullName))
            throw new InvalidOperationException("Already instrumented");
        var first = method.Body.Instructions[0];
        var il = method.Body.GetILProcessor();
        var parameter = method.Parameters.Single(p => p.ParameterType.FullName.StartsWith("System.Collections.Generic.IEnumerable`1"));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, parameter));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, asm.Name.Name + "." + name));
        il.InsertBefore(first, il.Create(OpCodes.Call, trace));
        il.InsertBefore(first, il.Create(OpCodes.Starg, parameter));
    }
} else if (args[0] == "compat") {
    using var helper = AssemblyDefinition.ReadAssembly(args[2]);
    var api = helper.MainModule.Types.Single(t => t.FullName == "Bsmac.NativeCompatibility");
    void Replace(string typeName, string methodName, string replacement, params string[] fields) {
        var type = types.Single(t => t.FullName == typeName);
        var method = type.Methods.Single(m => m.Name == methodName);
        if (!method.HasBody || method.Parameters.Count != 1) throw new InvalidOperationException("Unexpected transpiler signature");
        var body = new Mono.Cecil.Cil.MethodBody(method);
        method.Body = body;
        var il = body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg, method.Parameters[0]));
        foreach (string fieldName in fields) {
            var field = type.Fields.Single(f => f.Name == fieldName);
            if (!field.IsStatic) il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field));
        }
        il.Append(il.Create(OpCodes.Call, asm.MainModule.ImportReference(api.Methods.Single(m => m.Name == replacement))));
        il.Append(il.Create(OpCodes.Ret));
    }
    if (asm.Name.Name == "NoodleExtensions") {
        Replace("NoodleExtensions.HarmonyPatches.ObjectProcessing.ProcessNotesNoodleDataInTimeRow", "ProcessColorNotesInTimeRowTranspiler", "TimeRow", "_clampMethod");
        Replace("NoodleExtensions.Managers.FakePatchesManager", "BoundsNullCheckTranspiler", "Bounds", "_obstacleFakeCheck");
    } else if (asm.Name.Name == "Chroma") {
        Replace("Chroma.HarmonyPatches.DebrisSpawnerSkip", "ReplaceNoteConditionTranspiler", "Debris", "_noteSpawnDebris", "_noteDebrisCheck");
        Replace("Chroma.HarmonyPatches.DebrisSpawnerSkip", "ReplaceBombConditionTranspiler", "Debris", "_bombSpawnDebris", "_bombDebrisCheck");
    } else throw new InvalidOperationException("Unsupported compatibility target");
} else if (args[0] == "file-uri") {
    using var helper = AssemblyDefinition.ReadAssembly(args[2]);
    var target = types.Single(t => t.FullName == "FileHelpers").Methods.Single(m => m.Name == "GetEscapedURLForFilePath");
    if (!target.IsStatic || target.Parameters.Count != 1 || target.Parameters[0].ParameterType.FullName != "System.String" || target.ReturnType.FullName != "System.String")
        throw new InvalidOperationException("Unexpected local file URL helper signature");
    if (!target.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "EscapeURL") ||
        !target.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "file:///"))
        throw new InvalidOperationException("Expected the observed form-encoded file URL implementation");
    target.Body = new Mono.Cecil.Cil.MethodBody(target);
    var il = target.Body.GetILProcessor();
    il.Append(il.Create(OpCodes.Ldarg_0));
    il.Append(il.Create(OpCodes.Call, asm.MainModule.ImportReference(helper.MainModule.Types.Single(t => t.FullName == "Bsmac.LocalMedia").Methods.Single(m => m.Name == "FileUrl"))));
    il.Append(il.Create(OpCodes.Ret));
} else throw new ArgumentException("Unknown command");
string output = args[^1];
if (Path.GetFullPath(output) == Path.GetFullPath(args[1])) throw new InvalidOperationException("Stage into a separate file");
asm.Write(output + ".tmp");
using (var check = AssemblyDefinition.ReadAssembly(output + ".tmp")) {
    if (check.Name.FullName != asm.Name.FullName || check.MainModule.Resources.Count != asm.MainModule.Resources.Count)
        throw new InvalidOperationException("Assembly identity or resource count changed");
}
File.Move(output + ".tmp", output, true);
Console.WriteLine("Written and reopened " + output);
