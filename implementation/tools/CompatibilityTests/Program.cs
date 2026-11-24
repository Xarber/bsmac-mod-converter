// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Bsmac;
using HarmonyLib;

static class Check {
    static MethodInfo Method(Type type, string name) => type.GetMethod(name)!;
    static void Require(bool condition) { if (!condition) throw new Exception("Regression assertion failed"); }
    static IEnumerable<CodeInstruction> TimeRow(bool existing) {
        var code = new List<CodeInstruction> { new(OpCodes.Ldloc_S, 6), new(OpCodes.Callvirt, Method(typeof(NoteData), "get_lineIndex")) };
        if (existing) code.AddRange(new[] {new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ldc_I4_3), new CodeInstruction(OpCodes.Call, typeof(Math).GetMethod("Clamp", new[]{typeof(int),typeof(int),typeof(int)})!)});
        code.AddRange(new[] {new CodeInstruction(OpCodes.Ldelem_Ref), new CodeInstruction(OpCodes.Ldloc_1), new CodeInstruction(OpCodes.Callvirt, typeof(IEnumerable<SliderData>).GetMethod("GetEnumerator")!), new CodeInstruction(OpCodes.Stloc_S,10), new CodeInstruction(OpCodes.Ret)});
        return code;
    }
    public static void Main() {
        string sample="/Users/example/Library/Application Support/Beat Saber/Color + #50%/song.egg";
        string url=LocalMedia.FileUrl(sample);
        Require(url=="file:///Users/example/Library/Application%20Support/Beat%20Saber/Color%20%2B%20%2350%25/song.egg");
        Require(new Uri(url).LocalPath==sample);
        Require(LocalMedia.FileUrl("https://example.invalid/a+b?q=one+two")=="https://example.invalid/a+b?q=one+two");
        Require(new Uri(LocalMedia.FileUrl("/tmp/音楽 +.egg")).LocalPath=="/tmp/音楽 +.egg");
        var clamp=typeof(Math).GetMethod("Clamp", new[]{typeof(int),typeof(int),typeof(int)})!;
        foreach (bool existing in new[]{false,true}) {
            var result=NativeCompatibility.TimeRow(TimeRow(existing),clamp).ToList();
            Require(result.Count(i=>i.operand is MethodInfo m && m.Name=="Clamp")==1);
            Require(result[result.FindIndex(i=>i.opcode==OpCodes.Ldloc_1)-1].opcode==OpCodes.Ret);
        }
        var generator=new DynamicMethod("fixture",typeof(void),Type.EmptyTypes).GetILGenerator();
        Label label=generator.DefineLabel();
        var fake=new CodeInstruction(OpCodes.Call,Method(typeof(Check),nameof(Fake)));
        var bounds=new[]{new CodeInstruction(OpCodes.Call,Method(typeof(Cursor),"get_Current")),new CodeInstruction(OpCodes.Stloc_2),new CodeInstruction(OpCodes.Ldloc_2),new CodeInstruction(OpCodes.Call,Method(typeof(Check),nameof(Fake))),new CodeInstruction(OpCodes.Brfalse,label),new CodeInstruction(OpCodes.Ret)};
        var guarded=NativeCompatibility.Bounds(bounds,fake).ToList();
        Require(guarded[2].opcode==OpCodes.Ldloc_2 && guarded[4].opcode==OpCodes.Brtrue && guarded[4].operand.Equals(label));
        var debris=Method(typeof(Check),nameof(SpawnDebris));
        var replaced=NativeCompatibility.Debris(new[]{new CodeInstruction(OpCodes.Callvirt,debris)},debris,fake).ToList();
        Require(replaced.Count==2 && replaced[0].opcode==OpCodes.Ldarg_2 && replaced[1].operand==fake.operand);
        bool refused=false;
        try { NativeCompatibility.TimeRow(Array.Empty<CodeInstruction>(),clamp).ToList(); } catch(InvalidOperationException) {refused=true;}
        Require(refused);
        Console.WriteLine("PASS: local-file URI encoding/roundtrip/remote passthrough/unicode, unmodified/already-clamped rows, actual obstacle local, debris delegate, unsupported-shape refusal");
    }
    public static bool Fake() => false;
    public static void SpawnDebris() { }
}
public class NoteData { public int lineIndex => 0; }
public class SliderData { }
public class ObstacleController { }
public class Cursor { public ObstacleController Current => null!; }
