// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Bsmac {
    public static class NativeDiagnostics {
        static readonly HashSet<string> Seen = new HashSet<string>();
        public static IEnumerable<CodeInstruction> Trace(IEnumerable<CodeInstruction> instructions, string name) {
            var code = instructions.ToList();
            lock (Seen) {
                if (Seen.Add(name)) {
                    try {
                        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Library", "Application Support", "bsmac", "mod-converter", "cache", "NativePatchDiagnostics.txt");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.AppendAllText(path, "\n=== " + DateTime.UtcNow.ToString("O") + " " + name + " ===\n" +
                            string.Join("\n", code.Select((i, n) => n + ": " + i)) + "\n");
                        UnityEngine.Debug.Log("[BsmacNativeDiagnostics] Recorded " + name + ": " + code.Count + " instructions");
                    } catch (Exception error) { UnityEngine.Debug.LogWarning("[BsmacNativeDiagnostics] " + error.GetType().Name); }
                }
            }
            return code;
        }
    }
}
