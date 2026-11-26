// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using BeatSaberMarkupLanguage.Attributes;

namespace PlayerBackup {
    public sealed partial class Plugin {
        string deleteTarget;
        [UIValue("delete-a")] public bool CanDeleteA => CanDelete(0);
        [UIValue("delete-b")] public bool CanDeleteB => CanDelete(1);
        [UIValue("delete-c")] public bool CanDeleteC => CanDelete(2);
        [UIValue("confirm-a")] public bool ConfirmA => CanConfirm(0);
        [UIValue("confirm-b")] public bool ConfirmB => CanConfirm(1);
        [UIValue("confirm-c")] public bool ConfirmC => CanConfirm(2);
        bool ShowConfirmDelete => deleteTarget != null && job == null;
        bool CanConfirm(int row) => ShowConfirmDelete && At(row) == deleteTarget;
        bool confirmClearLocal;
        [UIValue("show-clear-local")] public bool ShowClearLocal => ShowBackup &&
            versions.Any(id => File.Exists(LocalBackup(id)));
        [UIValue("confirm-clear-local")] public bool ShowConfirmClearLocal => confirmClearLocal;
        [UIAction("clear-local")]
        public void ClearLocal() {
            if (!ShowClearLocal) return;
            confirmClearLocal = true;
            RestoreStatus = "Clear local copies only?";
            Draw();
        }

        [UIAction("confirm-clear-local-action")]
        public void ConfirmClearLocal() {
            if (!confirmClearLocal) return;
            try {
                var folder = Path.Combine(Root, "player-sync", "cleared-backups", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                foreach (var id in versions) {
                    string source = LocalBackup(id);
                    if (File.Exists(source)) File.Move(source, Path.Combine(folder, Path.GetFileName(source)));
                }
                RestoreStatus = "Local copies cleared";
            } catch (Exception error) {
                log.Warn("Could not clear all local backups: " + error.GetType().Name);
                RestoreStatus = "Could not clear all local copies";
            } finally {
                confirmClearLocal = false;
                RefreshHistory();
            }
        }

        bool CanDelete(int row) => ShowBackup && At(row) != null;
        void DrawDelete() {
            Changed(nameof(ShowClearLocal));
            Changed(nameof(ShowConfirmClearLocal));
            Changed(nameof(CanDeleteA));
            Changed(nameof(CanDeleteB));
            Changed(nameof(CanDeleteC));
            Changed(nameof(ConfirmA));
            Changed(nameof(ConfirmB));
            Changed(nameof(ConfirmC));
        }

        [UIAction("delete-row-a")] public void ChooseDeleteA() => ChooseDelete(0);
        [UIAction("delete-row-b")] public void ChooseDeleteB() => ChooseDelete(1);
        [UIAction("delete-row-c")] public void ChooseDeleteC() => ChooseDelete(2);

        void ChooseDelete(int row) {
            if (!CanDelete(row)) return;
            deleteTarget = At(row);
            RestoreStatus = "";
            Draw();
        }

        [UIAction("confirm-delete-action")]
        public async void ConfirmDelete() {
            if (!ShowConfirmDelete) return;
            string id = deleteTarget;
            string owner = HasSession ? session : null;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            job = operation;
            RestoreStatus = "Deleting backup…";
            Draw();
            try {
                if (owner != null) {
                    var path = "/api/backups/" + id;
                    using (var head = await SendBackup(HttpMethod.Head, path, owner, operation.Token)) {
                        if (head.StatusCode != HttpStatusCode.NotFound) {
                            head.EnsureSuccessStatusCode();
                            string revision = head.Headers.ETag?.Tag ?? throw new InvalidDataException();
                            if (session != owner) throw new OperationCanceledException();
                            using (var request = new HttpRequestMessage(HttpMethod.Delete, path)) {
                                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
                                request.Headers.TryAddWithoutValidation("If-Match", revision);
                                using (var reply = await AccountClient.SendAsync(request, operation.Token))
                                    reply.EnsureSuccessStatusCode();
                            }
                        }
                    }
                    cloudVersions = Array.FindAll(cloudVersions, value => value != id);
                }
                File.Delete(LocalBackup(id));
                RestoreStatus = "Backup deleted";
            } catch (Exception error) {
                log.Warn("Backup deletion interrupted: " + error.GetType().Name);
                RestoreStatus = "Delete failed. Check the list.";
            } finally {
                deleteTarget = null;
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }
    }
}
