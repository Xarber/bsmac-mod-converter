// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AssetBundleLoadingTools.Utilities;
using UnityEngine;

namespace Bsmac {
    public static class EquipmentPreparation {
        static readonly string Cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "bsmac", "mod-converter", "cache");
        static string Index => Environment.GetEnvironmentVariable("BSMAC_EQUIPMENT_INDEX") ?? Path.Combine(Cache, "EquipmentIndex.tsv");
        static readonly SemaphoreSlim Parallelism = new(1);
        static readonly Dictionary<string, Task<string>> Pending = new();
        static readonly Dictionary<string, (long Length, long Modified)> Checked = new();
        static FileSystemWatcher notesWatcher;

        public static void WatchNotes(string root) {
            if (!AutomaticPreparation.Enabled || notesWatcher != null) return;
            try {
                Directory.CreateDirectory(root);
                notesWatcher = new FileSystemWatcher(root) { IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite };
                notesWatcher.Created += (_, args) => QueueNote(args.FullPath);
                notesWatcher.Changed += (_, args) => QueueNote(args.FullPath);
                notesWatcher.Renamed += (_, args) => QueueNote(args.FullPath);
                notesWatcher.Error += (_, args) => Console.WriteLine("[BsmacEquipment] Note watcher failed: " + args.GetException().Message);
                notesWatcher.EnableRaisingEvents = true;
                _ = Task.Run(() => {
                    try {
                        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                            if (Path.GetExtension(path).Equals(".cyoob", StringComparison.OrdinalIgnoreCase))
                                QueueNote(path);
                    } catch (IOException error) {
                        Console.WriteLine("[BsmacEquipment] Package discovery failed: " + error.Message);
                    }
                });
            } catch (Exception error) {
                notesWatcher?.Dispose();
                notesWatcher = null;
                Debug.LogWarning("[BsmacEquipment] Note watcher unavailable: " + error.Message);
            }
        }

        public static AssetBundle LoadNote(string original) {
            if (Path.GetFileName(Path.GetDirectoryName(original)) == "Qosmetics"
                && Path.GetFileNameWithoutExtension(original).Length == 64)
                return LoadPreparedNote(original);
            if (AutomaticPreparation.Enabled) {
                try {
                    var target = FindVerified(original);
                    if (target.Path != null) {
                        var bundle = AssetBundle.LoadFromFile(target.Path, target.Crc);
                        if (bundle != null) return bundle;
                    }
                    QueueNote(original);
                } catch (Exception error) {
                    Debug.LogWarning("[BsmacEquipment] Note cache ignored: " + error.Message);
                }
            }
            return AssetBundle.LoadFromFile(original);
        }

        static AssetBundle LoadPreparedNote(string original) {
            string path = Path.ChangeExtension(original, ".qos.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 8192)
                throw new InvalidDataException("Qosmetics note receipt missing");
            var receipt = JsonUtility.FromJson<PreparedNote>(File.ReadAllText(path));
            if (receipt == null || receipt.schema != 1 || receipt.crc <= 0 || receipt.crc > uint.MaxValue
                || receipt.sourceSha256 != Path.GetFileNameWithoutExtension(original) || receipt.sha256 != Digest(original))
                throw new InvalidDataException("Qosmetics note receipt failed validation");
            return AssetBundle.LoadFromFile(original, (uint)receipt.crc);
        }

        [Serializable]
        sealed class PreparedNote {
            public int schema = 0;
            public long crc = 0;
            public string sha256 = null;
            public string sourceSha256 = null;
        }

        public static async Task<AssetBundle> LoadAsync(string original) {
            if (!AutomaticPreparation.Enabled || !SupportedAsync(original) || SynchronizationContext.Current == null)
                return await AssetBundleExtensions.LoadFromFileAsync(original);
            try {
                string error = await Request(original);
                if (error != null) Debug.LogWarning("[BsmacEquipment] " + error);
                var target = await Task.Run(() => FindVerified(original));
                if (target.Path != null) {
                    var request = AssetBundle.LoadFromFileAsync(target.Path, target.Crc);
                    AssetBundle bundle;
                    if (request.isDone) {
                        bundle = request.assetBundle;
                    } else {
                        var result = new TaskCompletionSource<AssetBundle>();
                        request.completed += _ => result.TrySetResult(request.assetBundle);
                        bundle = await result.Task;
                    }
                    if (bundle != null) return bundle;
                }
            } catch (Exception error) {
                Debug.LogWarning("[BsmacEquipment] Derivative unavailable: " + error.Message);
            }
            return await AssetBundleExtensions.LoadFromFileAsync(original);
        }

        static bool SupportedAsync(string path) {
            switch (Path.GetExtension(path).ToLowerInvariant()) {
                case ".pixie": case ".wall": case ".saber": return true;
                default: return false;
            }
        }

        static void QueueNote(string path) {
            string ext = Path.GetExtension(path);
            if (!ext.Equals(".bloq", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".note", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".cyoob", StringComparison.OrdinalIgnoreCase)) return;
            if (Path.GetFileName(Path.GetDirectoryName(path)) == "Qosmetics" && ext.Equals(".bloq", StringComparison.OrdinalIgnoreCase)) return;
            try {
                _ = Request(path);
            } catch (IOException error) {
                Console.WriteLine("[BsmacEquipment] Note changed before it could be queued: " + error.Message);
            }
        }

        static Task<string> Request(string path) {
            lock (Pending) {
                if (Pending.TryGetValue(path, out var active)) return active;
                if (Pending.Count >= 32) return Task.FromResult("Equipment queue is full. Reopen this asset later.");
                if (Checked.TryGetValue(path, out var cached) && ReadStamp(path) == cached)
                    return Task.FromResult<string>(null);
                var task = Task.Run(async () => {
                    if (!await Parallelism.WaitAsync(30000))
                        return "Another equipment bundle is still preparing.";
                    try {
                        if (!AutomaticPreparation.WorkerAvailable || !File.Exists(path)) return null;
                        var before = ReadStamp(path);
                        var result = AutomaticPreparation.RunLocalWorker("prepare-equipment", path);
                        if (result.Error != null) {
                            Console.WriteLine("[BsmacEquipment] " + Path.GetFileName(path) + ": " + result.Error);
                            return result.Error;
                        }
                        if (before == ReadStamp(path)) lock (Pending) Checked[path] = before;
                        if (!string.IsNullOrEmpty(result.Output)) Console.WriteLine("[BsmacEquipment] Prepared " + Path.GetFileName(path));
                        return null;
                    } finally {
                        Parallelism.Release();
                    }
                });
                Pending.Add(path, task);
                _ = task.ContinueWith(_ => { lock (Pending) Pending.Remove(path); });
                return task;
            }
        }

        static (long Length, long Modified) ReadStamp(string path) {
            var file = new FileInfo(path);
            return file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : (-1, 0);
        }

        static string Digest(string path) {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        static (string Path, uint Crc) FindVerified(string original) {
            if (!File.Exists(Index) || !File.Exists(original)) return (null, 0);
            string digest = null;
            foreach (string line in File.ReadLines(Index)) {
                var row = line.Split('\t');
                if (row.Length != 5 || !Path.IsPathRooted(row[4])) continue;
                digest ??= Digest(original);
                if (row[0] == digest && uint.TryParse(row[2], out uint crc) && crc != 0 && File.Exists(row[4])
                        && row[3] == Digest(row[4])) return (row[4], crc);
            }
            return (null, 0);
        }
    }
}
