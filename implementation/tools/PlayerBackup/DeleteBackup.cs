// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
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
        [UIValue("confirm-delete")] public bool ShowConfirmDelete => deleteTarget != null && job == null;
        bool CanDelete(int row) => ShowBackup && At(row) != null;
        void DrawDelete() {
            Changed(nameof(CanDeleteA));
            Changed(nameof(CanDeleteB));
            Changed(nameof(CanDeleteC));
            Changed(nameof(ShowConfirmDelete));
        }

        [UIAction("delete-row-a")] public void ChooseDeleteA() => ChooseDelete(0);
        [UIAction("delete-row-b")] public void ChooseDeleteB() => ChooseDelete(1);
        [UIAction("delete-row-c")] public void ChooseDeleteC() => ChooseDelete(2);

        void ChooseDelete(int row) {
            if (!CanDelete(row)) return;
            deleteTarget = At(row);
            RestoreStatus = $"Delete {Row(row)}? This removes the local file and any copy on your signed-in account.";
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
                RestoreStatus = "Deletion interrupted. Check the backup list before retrying.";
            } finally {
                deleteTarget = null;
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }
    }
}
