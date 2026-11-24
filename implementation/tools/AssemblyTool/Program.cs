// SPDX-License-Identifier: AGPL-3.0-only
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;

if (args.Length < 2) throw new ArgumentException("inspect|resource|crc|bridge input [arguments]");
using var resolver = new DefaultAssemblyResolver();
string inputDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
resolver.AddSearchDirectory(inputDirectory);
string resources = Directory.GetParent(inputDirectory)!.FullName;
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
if (args[0] == "crc") {
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
