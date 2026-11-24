// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Bsmac {
    // Scoped replacements for three observed failed transpilers. Original game
    // methods and mod behavior remain intact; unknown shapes fail explicitly.
    public static class NativeCompatibility {
        static bool Call(CodeInstruction i, string type, string name) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) &&
            i.operand is MethodBase m && m.Name == name && m.DeclaringType.Name == type;

        static CodeInstruction LoadForStore(CodeInstruction store) {
            if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (store.opcode == OpCodes.Stloc || store.opcode == OpCodes.Stloc_S)
                return new CodeInstruction(OpCodes.Ldloc, store.operand);
            throw new InvalidOperationException("Expected obstacle local store");
        }

        public static IEnumerable<CodeInstruction> TimeRow(IEnumerable<CodeInstruction> input, MethodInfo clamp) {
            var code = input.ToList();
            int[] getters = Enumerable.Range(0, code.Count).Where(i => Call(code[i], "NoteData", "get_lineIndex")).ToArray();
            if (getters.Length != 1) throw new InvalidOperationException("Unknown time-row line-index shape");
            int index = getters[0] + 1;
            if (code[index].opcode == OpCodes.Ldelem_Ref) {
                code.InsertRange(index, new[] { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ldc_I4_3), new CodeInstruction(OpCodes.Call, clamp) });
            } else if (!(code[index].opcode == OpCodes.Ldc_I4_0 && code[index+1].opcode == OpCodes.Ldc_I4_3 &&
                code[index+2].operand is MethodInfo existing && existing.Name == "Clamp" &&
                existing.ReturnType == typeof(int) && existing.GetParameters().All(p => p.ParameterType == typeof(int)) &&
                code[index+3].opcode == OpCodes.Ldelem_Ref)) {
                throw new InvalidOperationException("Unknown preexisting time-row clamp");
            }
            int slider = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "GetEnumerator" &&
                m.DeclaringType.IsGenericType && m.DeclaringType.GetGenericArguments().Any(t => t.Name == "SliderData"));
            if (slider < 1 || code[slider-1].opcode != OpCodes.Ldloc_1 || code[slider-1].blocks.Count != 0)
                throw new InvalidOperationException("Unknown time-row slider boundary");
            // Noodle's postfix performs the custom slider pass. Keep upstream's
            // early return, including labels from the completed note foreach.
            var ret = new CodeInstruction(OpCodes.Ret);
            ret.labels.AddRange(code[slider-1].labels);
            code[slider-1].labels.Clear();
            code.Insert(slider-1, ret);
            return code;
        }

        public static IEnumerable<CodeInstruction> Bounds(IEnumerable<CodeInstruction> input, CodeInstruction fakeCheck) {
            var code = input.ToList();
            var matches = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo m &&
                m.Name == "get_Current" && m.ReturnType.Name == "ObstacleController").ToArray();
            if (matches.Length != 1 || matches[0] + 4 >= code.Count)
                throw new InvalidOperationException("Unknown obstacle enumeration shape");
            int store = matches[0] + 1;
            var load = LoadForStore(code[store]);
            int branch = code.FindIndex(store+1, i => i.opcode == OpCodes.Brfalse || i.opcode == OpCodes.Brfalse_S);
            if (branch < 0 || branch > store+4 || !(code[branch].operand is Label))
                throw new InvalidOperationException("Unknown obstacle continue branch");
            code.InsertRange(store+1, new[] { load, new CodeInstruction(fakeCheck), new CodeInstruction(OpCodes.Brtrue, code[branch].operand) });
            return code;
        }

        public static IEnumerable<CodeInstruction> Debris(IEnumerable<CodeInstruction> input, MethodInfo expected, CodeInstruction replacement) {
            var code = input.ToList();
            var matches = Enumerable.Range(0, code.Count).Where(i =>
                (code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt) &&
                code[i].operand is MethodInfo m && m.Name == expected.Name &&
                m.DeclaringType.FullName == expected.DeclaringType.FullName &&
                m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(expected.GetParameters().Select(p => p.ParameterType.FullName))).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Unknown debris call shape: " + expected);
            int index = matches[0];
            var note = new CodeInstruction(OpCodes.Ldarg_2);
            note.labels.AddRange(code[index].labels);
            note.blocks.AddRange(code[index].blocks);
            code[index] = note;
            code.Insert(index+1, new CodeInstruction(replacement));
            return code;
        }
    }
}
