// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Bsmac {
    public static class VivifyMetalBridge {
        static string Digest(string path) {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // Called only by Vivify's checked two-argument map loader. Debug and all
        // other AssetBundle load calls retain their upstream implementation.
        public static AssetBundle Load(string original, uint crc) {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Metal || crc == 0)
                return AssetBundle.LoadFromFile(original, crc);
            try {
                string index = Environment.GetEnvironmentVariable("BSMAC_METAL_INDEX") ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "bsmac", "mod-converter", "cache", "MetalMapIndex.tsv");
                if (File.Exists(index)) {
                    string digest = null;
                    foreach (string line in File.ReadLines(index)) {
                        string[] row = line.Split('\t');
                        if (row.Length != 5 || !uint.TryParse(row[1], out uint expected) || expected != crc ||
                            !uint.TryParse(row[2], out uint convertedCrc) || convertedCrc == 0 ||
                            row[0].Length != 64 || row[3].Length != 64 || !Path.IsPathRooted(row[4])) continue;
                        if (digest == null) digest = Digest(original);
                        if (digest != row[0] || !File.Exists(row[4])) continue;
                        if (Digest(row[4]) != row[3]) {
                            Debug.LogError("[VivifyMetalBridge] Derivative SHA-256 mismatch; retaining checked original loader");
                            break;
                        }
                        Debug.Log("[VivifyMetalBridge] Validated original SHA/CRC and derivative SHA; loading Metal bundle");
                        var bundle = AssetBundle.LoadFromFile(row[4], convertedCrc);
                        Debug.Log(bundle == null ? "[VivifyMetalBridge] Metal bundle failed CRC/load validation" : "[VivifyMetalBridge] Metal bundle loaded");
                        return bundle;
                    }
                }
            } catch (Exception error) {
                Debug.LogWarning("[VivifyMetalBridge] Index unavailable: " + error.GetType().Name);
            }
            Debug.Log("[VivifyMetalBridge] No validated derivative; retaining original CRC");
            return AssetBundle.LoadFromFile(original, crc);
        }
    }
}
