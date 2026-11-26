// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using IPA;
using UnityEngine;

namespace PlayerBackup {
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed partial class Plugin : INotifyPropertyChanged {
        readonly IPA.Logging.Logger log;
        string[] restoreCategories;
        string restoreVersion;
        string restoreDigest;
        int menuGeneration;
        int page;
        BSMLSettings settings;
        CancellationTokenSource job;
        string[] cloudVersions = Array.Empty<string>();
        string[] versions = Array.Empty<string>();
        static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                           "Library", "Application Support", "bsmac");
        static string Backups => Path.Combine(Root, "player-backups");
        static string LocalBackup(string version) => Path.Combine(Backups, version + ".zip");
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        string BackupStatus { get; set; } = "";
        [UIValue("restore-status")] public string RestoreStatus { get; private set; } = "";
        [UIValue("show-restore-footer")] public bool ShowRestoreFooter => !NoBackups &&
            (RestoreStatus.Length != 0 || ShowRestart);
        static string RestoreResult => Path.Combine(Root, "player-sync", "restore-result.txt");
        [UIValue("backup-feedback")] public string BackupFeedback =>
            job != null && restoreVersion == null && deleteTarget == null && BackupStatus == "Saving backup…"
                ? BackupProgress : NoBackups && BackupStatus.Length == 0 ? "No backups yet" : BackupStatus;
        string BackupProgress = "";
        void ShowProgress(int percent) {
            int blocks = Math.Max(0, Math.Min(10, percent / 10));
            BackupProgress = new string('■', blocks) + new string('□', 10 - blocks) + $"  {percent}%";
            Changed(nameof(BackupFeedback));
        }
        [UIValue("show-backup")] public bool ShowBackup => job == null && restoreVersion == null && deleteTarget == null;
        [UIValue("show-cancel")] public bool ShowCancel => !ShowBackup;
        [UIValue("show-restart")] public bool ShowRestart => restoreCategories != null && job == null;
        bool NoBackups => versions.Length == 0;
        [UIValue("history-page")] public string HistoryPage => versions.Length == 0 ? "No backups yet"
            : $"Backups · {page + 1} / {(versions.Length + 2) / 3}";
        [UIValue("show-prev")] public bool ShowPrevious => page > 0 && ShowBackup;
        [UIValue("show-next")] public bool ShowNext => (page + 1) * 3 < versions.Length && ShowBackup;
        [UIValue("row-a")] public string RowA => Row(0);
        [UIValue("row-b")] public string RowB => Row(1);
        [UIValue("row-c")] public string RowC => Row(2);
        [UIValue("has-a")] public bool HasA => At(0) != null;
        [UIValue("has-b")] public bool HasB => At(1) != null;
        [UIValue("has-c")] public bool HasC => At(2) != null;
        [UIValue("restore-a")] public bool CanRestoreA => CanRestore(0);
        [UIValue("restore-b")] public bool CanRestoreB => CanRestore(1);
        [UIValue("restore-c")] public bool CanRestoreC => CanRestore(2);
        [UIValue("cancel-a")] public bool CanCancelA => CanCancel(0);
        [UIValue("cancel-b")] public bool CanCancelB => CanCancel(1);
        [UIValue("cancel-c")] public bool CanCancelC => CanCancel(2);

        string At(int row) => versions.ElementAtOrDefault(page * 3 + row);
        bool CanCancel(int row) => (restoreVersion != null && At(row) == restoreVersion) || CanConfirm(row);
        bool CanRestore(int row) => ShowBackup && At(row) != null && SnapshotVersion(At(row)) == CurrentVersion() && File.Exists(LocalBackup(At(row)));
        bool CanDownload(int row) => ShowBackup && HasSession && At(row) != null && cloudVersions.Contains(At(row)) &&
            !File.Exists(LocalBackup(At(row)));
        [UIValue("download-a")] public bool CanDownloadA => CanDownload(0);
        [UIValue("download-b")] public bool CanDownloadB => CanDownload(1);
        [UIValue("download-c")] public bool CanDownloadC => CanDownload(2);
        [UIValue("show-pages")] public bool ShowPages => ShowPrevious || ShowNext;

        [UIAction("download-row-a")] public void DownloadA() => Download(0);
        [UIAction("download-row-b")] public void DownloadB() => Download(1);
        [UIAction("download-row-c")] public void DownloadC() => Download(2);

        async void Download(int row) {
            if (!CanDownload(row)) return;
            string version = At(row);
            restoreVersion = version;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            job = operation;
            RestoreStatus = "Downloading " + version + "…";
            Draw();
            try {
                await DownloadBackup(version, operation.Token);
                RestoreStatus = "Downloaded " + version;
            } catch (OperationCanceledException) {
                RestoreStatus = "Download canceled";
            } catch (Exception) {
                RestoreStatus = "Could not download " + version;
            } finally {
                restoreVersion = null;
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }
        string Row(int row) {
            var version = At(row);
            if (version == null) return "";
            if (version == deleteTarget)
                return job != null ? "Deleting…" : HasSession ? "Delete local & cloud?" : "Delete local backup?";
            var file = new FileInfo(LocalBackup(version));
            string where = file.Exists
                ? file.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + (cloudVersions.Contains(version) ? " · Cloud" : "")
                : "Cloud";
            var details = version.Split('-');
            if (details.Length == 4 && DateTime.TryParseExact(details[2], "yyyyMMddHHmmssfff",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var created))
                where = created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") +
                    (details[1] == "auto" ? " · Auto" : " · Manual") + (file.Exists ? "" : " · Cloud");
            return SnapshotVersion(version) + " · " + where;
        }

        void Draw() {
            DrawDelete();
            foreach (var property in new[] { nameof(BackupFeedback), nameof(RestoreStatus), nameof(ShowRestoreFooter), nameof(ShowBackup), nameof(ShowCancel), nameof(ShowRestart),
                nameof(HistoryPage), nameof(ShowPrevious), nameof(ShowNext), nameof(RowA), nameof(RowB), nameof(RowC),
                nameof(ShowPages), nameof(CanDownloadA), nameof(CanDownloadB), nameof(CanDownloadC), nameof(HasA), nameof(HasB), nameof(HasC), nameof(CanRestoreA), nameof(CanRestoreB), nameof(CanRestoreC),
                nameof(CanCancelA), nameof(CanCancelB), nameof(CanCancelC) }) Changed(property);
        }

        void RefreshHistory() {
            var local = Directory.Exists(Backups) ? Directory.EnumerateFiles(Backups, "*.zip")
                .Select(Path.GetFileNameWithoutExtension).Where(IsSnapshot) : Enumerable.Empty<string>();
            versions = local.Concat(cloudVersions).Distinct().OrderByDescending(value => Version.Parse(SnapshotVersion(value))).ThenByDescending(value => value.Split('-').ElementAtOrDefault(2)).ToArray();
            page = Math.Min(page, Math.Max(0, (versions.Length - 1) / 3));
            Draw();
        }

        [UIAction("previous")] public void Previous() { if (ShowPrevious) { page--; Draw(); } }
        [UIAction("next")] public void Next() { if (ShowNext) { page++; Draw(); } }

        [Init]
        public Plugin(IPA.Logging.Logger logger) { log = logger; }

        [OnEnable]
        public async void Enable() {
            int generation = ++menuGeneration;
            try {
                await MainMenuAwaiter.WaitForMainMenuAsync();
                if (generation != menuGeneration) return;
                if (File.Exists(RestoreResult)) {
                    RestoreStatus = File.ReadAllText(RestoreResult).Trim();
                    if (RestoreStatus != "Restore in progress") File.Delete(RestoreResult);
                }
                RefreshHistory();
                settings = BSMLSettings.Instance;
                settings.AddSettingsMenu("Player Backup", "PlayerBackup.Settings.bsml", this);
                log.Info("Player Backup settings registered.");
                ResumeSchedule();
                await ResumeAccount();
            } catch (Exception error) {
                log.Error("Could not register Player Backup settings: " + error.Message);
            }
        }

        [OnDisable]
        public void Disable() {
            ++menuGeneration;
            Cancel();
            EndAccount(!persistentSession);
            settings?.RemoveSettingsMenu(this);
            settings = null;
        }

        [UIValue("player")] public bool Player { get; set; } = true;
        [UIValue("configs")] public bool Configs { get; set; } = true;
        [UIValue("mods")] public bool Mods { get; set; } = true;
        [UIValue("maps")] public bool Maps { get; set; } = true;
        [UIValue("models")] public bool Models { get; set; } = true;

        string[] Categories() => new[] { (Player, "player"), (Configs, "configs"), (Mods, "mods"),
            (Maps, "maps"), (Models, "models") }.Where(choice => choice.Item1).Select(choice => choice.Item2).ToArray();

        static bool IsSnapshot(string id) => Regex.IsMatch(id ?? "",
            @"^[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}(?:-(?:manual|auto)-[0-9]{17}-[0-9a-f]{8})?$");
        static string SnapshotVersion(string id) => id.Split('-')[0];

        [UIAction("backup")] public void Backup() => SaveBackup(false);
        async void SaveBackup(bool automatic) {
            if (!ShowBackup) return;
            string[] categories = Categories();
            if (categories.Length == 0) { BackupStatus = "Select at least one category"; Draw(); return; }
            var operation = new CancellationTokenSource();
            job = operation;
            BackupStatus = "Saving backup…";
            ShowProgress(0);
            Draw();
            bool saved = false;
            try {
                var version = CurrentVersion() + (automatic ? "-auto-" : "-manual-") +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture) +
                    "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                using (var process = StartWorker(Backups, categories, "--live-snapshot --progress --snapshot-id " + version, true))
                using (operation.Token.Register(() => Stop(process))) {
                    var errors = process.StandardError.ReadToEndAsync();
                    string line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null) {
                        if (int.TryParse(line, out int progress) && progress >= 0 && progress <= 100)
                            ShowProgress(progress);
                    }
                    await Task.Run(() => process.WaitForExit());
                    operation.Token.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0) {
                        var detail = (await errors).Split('\n').LastOrDefault(row => !string.IsNullOrWhiteSpace(row));
                        log.Error("Backup worker failed: " + detail);
                        throw new IOException();
                    }
                }
                saved = true;
                if (HasSession) {
                    BackupStatus = "Uploading backup…";
                    Draw();
                    await UploadBackup(version, operation.Token);
                    if (automatic) CompleteSchedule();
                    await RefreshCloud(operation.Token);
                }
                BackupStatus = HasSession ? "Saved locally and in cloud" : "Local backup saved";
            } catch (OperationCanceledException) {
                BackupStatus = saved ? "Saved locally · upload canceled" : "Backup canceled";
            } catch (Exception error) {
                log.Error("Backup operation failed: " + error.GetType().Name);
                BackupStatus = saved ? "Saved locally · upload failed" : "Backup failed · retry from menu";
            } finally {
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }

        static void Stop(Process process) {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        [UIAction("restore-row-a")] public void RestoreA() => Prepare(0);
        [UIAction("restore-row-b")] public void RestoreB() => Prepare(1);
        [UIAction("restore-row-c")] public void RestoreC() => Prepare(2);

        async void Prepare(int index) {
            if (!CanRestore(index)) return;
            var categories = Categories();
            if (categories.Length == 0) { RestoreStatus = "Select at least one category"; Draw(); return; }
            var version = At(index);
            RestoreStatus = "";
            restoreVersion = version;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            job = operation;
            RestoreStatus = "Preparing restore…";
            Draw();
            try {
                if (!File.Exists(LocalBackup(version))) await DownloadBackup(version, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                using (var input = File.OpenRead(LocalBackup(version))) {
                    CheckBackup(input, SnapshotVersion(version));
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        restoreDigest = string.Concat(hash.ComputeHash(input).Select(value => value.ToString("x2")));
                }
                restoreCategories = categories;
                RestoreStatus = "Restore ready";
            } catch (OperationCanceledException) {
                restoreVersion = null;
                RestoreStatus = "Restore canceled";
            } catch (Exception) {
                restoreVersion = null;
                RestoreStatus = "Could not prepare this backup";
            } finally {
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }

        [UIAction("cancel")]
        public void Cancel() {
            job?.Cancel();
            deleteTarget = null;
            restoreCategories = null;
            restoreVersion = null;
            restoreDigest = null;
            if (job == null) RestoreStatus = "";
            Draw();
        }

        [UIAction("quit-restore")]
        public void QuitForRestore() {
            if (!ShowRestart) return;
            try {
                using (StartWorker(LocalBackup(restoreVersion), restoreCategories,
                    "--after-pid " + Process.GetCurrentProcess().Id + " --restore --apply --expected-sha256 " +
                    restoreDigest + " --relaunch --result-file " + Quote(RestoreResult))) { }
                Application.Quit();
            } catch (Exception error) {
                log.Error("Could not start restore: " + error.GetType().Name);
                RestoreStatus = "Could not start restore";
                Draw();
            }
        }

        static string InstancePath() {
            for (var parent = new DirectoryInfo(Application.dataPath); parent != null; parent = parent.Parent) {
                if (parent.Name == "Contents" && parent.Parent?.Name == "Beat Saber.app"
                    && parent.Parent.Parent != null)
                    return parent.Parent.Parent.FullName;
            }
            throw new InvalidOperationException("Expected a MoltenVR app bundle");
        }

        static Process StartWorker(string output, string[] categories, string extra, bool capture = false) {
            var python = Path.Combine(Root, "player-sync", "venv", "bin", "python");
            var start = new ProcessStartInfo(python) { UseShellExecute = false,
                RedirectStandardOutput = capture, RedirectStandardError = capture };
            start.Arguments = string.Join(" ", new[] {
                "-I", "-m", "bsmac_converter.player_backup", Quote(InstancePath()),
                Quote(Application.persistentDataPath), Quote(output), extra,
            }.Concat(categories.SelectMany(category => new[] { "--include", category })));
            return Process.Start(start);
        }

        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
