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

        [UIValue("queue-status")] public string QueueStatus { get; private set; } = "Ready";
        [UIValue("show-backup")] public bool ShowBackup => job == null && restoreVersion == null;
        [UIValue("show-cancel")] public bool ShowCancel => !ShowBackup;
        [UIValue("show-restart")] public bool ShowRestart => restoreCategories != null && job == null;
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
        bool CanCancel(int row) => restoreVersion != null && At(row) == restoreVersion;
        bool CanRestore(int row) => ShowBackup && At(row) != null && At(row) == CurrentVersion();
        string Row(int row) {
            var version = At(row);
            if (version == null) return "";
            var file = new FileInfo(LocalBackup(version));
            string where = file.Exists
                ? file.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + (cloudVersions.Contains(version) ? " · Cloud" : "")
                : "Cloud";
            return version + " · " + where;
        }

        void Draw() {
            foreach (var property in new[] { nameof(QueueStatus), nameof(ShowBackup), nameof(ShowCancel), nameof(ShowRestart),
                nameof(HistoryPage), nameof(ShowPrevious), nameof(ShowNext), nameof(RowA), nameof(RowB), nameof(RowC),
                nameof(HasA), nameof(HasB), nameof(HasC), nameof(CanRestoreA), nameof(CanRestoreB), nameof(CanRestoreC),
                nameof(CanCancelA), nameof(CanCancelB), nameof(CanCancelC) }) Changed(property);
        }

        void RefreshHistory() {
            var local = Directory.Exists(Backups) ? Directory.EnumerateFiles(Backups, "*.zip")
                .Select(Path.GetFileNameWithoutExtension).Where(IsVersion) : Enumerable.Empty<string>();
            versions = local.Concat(cloudVersions).Distinct().OrderByDescending(value => Version.Parse(value)).ToArray();
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
                RefreshHistory();
                settings = BSMLSettings.Instance;
                settings.AddSettingsMenu("Player Backup", "PlayerBackup.Settings.bsml", this);
                log.Info("Player Backup settings registered.");
            } catch (Exception error) {
                log.Error("Could not register Player Backup settings: " + error.Message);
            }
        }

        [OnDisable]
        public void Disable() {
            ++menuGeneration;
            Cancel();
            ClearAccount();
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
        static bool IsVersion(string version) => Regex.IsMatch(version ?? "", @"^[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}$");

        [UIAction("backup")]
        public async void Backup() {
            if (!ShowBackup) return;
            string[] categories = Categories();
            if (categories.Length == 0) { QueueStatus = "Select at least one category"; Draw(); return; }
            var operation = new CancellationTokenSource();
            job = operation;
            QueueStatus = "Saving backup…";
            Draw();
            bool saved = false;
            try {
                var version = CurrentVersion();
                using (var process = StartWorker(Backups, categories, "--live-snapshot")) {
                    using (operation.Token.Register(() => Stop(process)))
                        await Task.Run(() => process.WaitForExit());
                    operation.Token.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0) throw new IOException();
                }
                saved = true;
                if (HasSession) {
                    QueueStatus = "Uploading backup…";
                    Draw();
                    await UploadBackup(version, operation.Token);
                    await RefreshCloud(operation.Token);
                }
                QueueStatus = HasSession ? "Backup saved locally and in the cloud" : "Local backup saved";
            } catch (OperationCanceledException) {
                QueueStatus = saved ? "Local backup saved. Upload canceled." : "Backup canceled";
            } catch (Exception) {
                QueueStatus = saved ? "Local backup saved. Cloud upload failed." : "Backup failed. Retry from the main menu.";
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
            if (categories.Length == 0) { QueueStatus = "Select at least one category"; Draw(); return; }
            var version = At(index);
            restoreVersion = version;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            job = operation;
            QueueStatus = "Preparing restore…";
            Draw();
            try {
                if (!File.Exists(LocalBackup(version))) await DownloadBackup(version, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                using (var input = File.OpenRead(LocalBackup(version))) {
                    CheckBackup(input, version);
                    using (var hash = System.Security.Cryptography.SHA256.Create())
                        restoreDigest = string.Concat(hash.ComputeHash(input).Select(value => value.ToString("x2")));
                }
                restoreCategories = categories;
                QueueStatus = "Restore ready. Quit and relaunch through MoltenVR.";
            } catch (OperationCanceledException) {
                restoreVersion = null;
                QueueStatus = "Restore canceled";
            } catch (Exception) {
                restoreVersion = null;
                QueueStatus = "Could not prepare this backup";
            } finally {
                job = null;
                operation.Dispose();
                RefreshHistory();
            }
        }

        [UIAction("cancel")]
        public void Cancel() {
            job?.Cancel();
            restoreCategories = null;
            restoreVersion = null;
            restoreDigest = null;
            QueueStatus = job == null ? "Ready" : "Canceling…";
            Draw();
        }

        [UIAction("quit-restore")]
        public void QuitForRestore() {
            if (ShowRestart) Application.Quit();
        }

        [OnExit]
        public void Exit() {
            if (restoreCategories == null || restoreVersion == null || restoreDigest == null) return;
            try {
                using (StartWorker(LocalBackup(restoreVersion), restoreCategories,
                    "--after-pid " + Process.GetCurrentProcess().Id + " --restore --apply --expected-sha256 " + restoreDigest)) { }
            } catch (Exception error) {
                log.Error("Could not start restore: " + error.GetType().Name);
            }
        }

        static string InstancePath() {
            var contents = Directory.GetParent(Application.dataPath)?.Parent;
            if (contents?.Name != "Contents") throw new InvalidOperationException("Expected a MoltenVR app bundle");
            return contents.Parent.Parent.FullName;
        }

        static Process StartWorker(string output, string[] categories, string extra) {
            var python = Path.Combine(Root, "player-sync", "venv", "bin", "python");
            var start = new ProcessStartInfo(python) { UseShellExecute = false };
            start.Arguments = string.Join(" ", new[] {
                "-I", "-m", "bsmac_converter.player_backup", Quote(InstancePath()),
                Quote(Application.persistentDataPath), Quote(output), extra,
            }.Concat(categories.SelectMany(category => new[] { "--include", category })));
            return Process.Start(start);
        }

        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
