// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;

namespace PlayerBackup {
    public sealed partial class Plugin {
        [UIValue("show-sync-cloud")]
        public bool ShowSyncCloud => ShowBackup && HasSession;

        [UIAction("sync-cloud")]
        public async void SyncCloudBackups() {
            if (!ShowSyncCloud) return;
            var owner = session;
            using var operation = new CancellationTokenSource();
            job = operation;
            BackupStatus = "Checking cloud backups…";
            Draw();
            int uploaded = 0, skipped = 0, failed = 0;
            try {
                await RefreshCloud(operation.Token);
                var hashes = new HashSet<string>(cloudFiles.Select(item => item.sha256).Where(value => value != null));
                var local = Directory.Exists(Backups) ? Directory.GetFiles(Backups, "*.zip") : Array.Empty<string>();
                foreach (var path in local) {
                    operation.Token.ThrowIfCancellationRequested();
                    if (session != owner) throw new OperationCanceledException();
                    var version = Path.GetFileNameWithoutExtension(path);
                    if (!IsSnapshot(version)) continue;
                    try {
                        var digest = await Task.Run(() => {
                            using var input = File.OpenRead(path);
                            if (input.Length > MaxBackup) throw new InvalidDataException();
                            using var hash = SHA256.Create();
                            return HexDigest(hash.ComputeHash(input));
                        }, operation.Token);
                        if (hashes.Contains(digest)) { skipped++; continue; }
                        if (cloudVersions.Contains(version)) { failed++; continue; }
                        operation.Token.ThrowIfCancellationRequested();
                        if (session != owner) throw new OperationCanceledException();
                        BackupStatus = "Uploading local backups · " + (uploaded + 1);
                        Draw();
                        await UploadBackup(version, operation.Token);
                        hashes.Add(digest);
                        uploaded++;
                    } catch (OperationCanceledException) { throw; }
                    catch (Exception error) {
                        failed++;
                        log.Warn("Cloud sync skipped a local snapshot: " + error.GetType().Name);
                    }
                }
                await RefreshCloud(operation.Token);
                BackupStatus = $"Uploaded {uploaded} · Already synced {skipped}" +
                    (failed > 0 ? $" · Could not sync {failed}" : "");
            } catch (OperationCanceledException) {
                BackupStatus = $"Sync canceled · Uploaded {uploaded}";
            } catch (Exception error) {
                log.Warn("Cloud sync could not finish: " + error.GetType().Name);
                BackupStatus = $"Could not finish sync · Uploaded {uploaded}";
            } finally {
                job = null;
                RefreshHistory();
            }
        }
    }
}
