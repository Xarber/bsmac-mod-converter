// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using IPA;
using UnityEngine;

namespace PlayerBackup {
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed partial class Plugin : INotifyPropertyChanged {
        readonly IPA.Logging.Logger log;
        string[] pending;
        bool restoring;
        int menuGeneration;
        BSMLSettings settings;
        static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                           "Library", "Application Support", "bsmac");
        static string Backups => Path.Combine(Root, "player-backups");
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        [UIValue("queue-status")]
        public string QueueStatus { get; private set; } = "No operation queued";

        [UIValue("history")]
        public string History { get; private set; } = "No local backups yet";

        [UIAction("refresh-history")]
        public void RefreshHistory() {
            try {
                var rows = Directory.Exists(Backups)
                    ? new DirectoryInfo(Backups).EnumerateFiles("*.zip")
                        .Where(file => Regex.IsMatch(Path.GetFileNameWithoutExtension(file.Name), @"^\d+\.\d+\.\d+$"))
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .Select(file => Path.GetFileNameWithoutExtension(file.Name) + " · " + file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"))
                        .ToArray()
                    : Array.Empty<string>();
                History = rows.Length == 0 ? "No local backups yet" : string.Join("\n", rows);
            } catch (IOException) {
                History = "Could not read local backups";
            } catch (UnauthorizedAccessException) {
                History = "Could not read local backups";
            }
            Changed(nameof(History));
        }

        void SetQueueStatus(string status) {
            QueueStatus = status;
            Changed(nameof(QueueStatus));
        }

        [Init]
        public Plugin(IPA.Logging.Logger logger) { log = logger; }

        [OnEnable]
        public async void Enable() {
            int generation = ++menuGeneration;
            try {
                await MainMenuAwaiter.WaitForMainMenuAsync();
                if (generation != menuGeneration) return;
                RefreshHistory();
                var menu = BSMLSettings.Instance;
                menu.AddSettingsMenu("Player Backup", "PlayerBackup.Settings.bsml", this);
                settings = menu;
                log.Info("Player Backup settings registered.");
            } catch (Exception error) {
                log.Error("Could not register Player Backup settings: " + error.Message);
            }
        }

        [OnDisable]
        public void Disable() {
            ++menuGeneration;
            ClearAccount();
            pending = null;
            settings?.RemoveSettingsMenu(this);
            settings = null;
        }

        [UIValue("player")] public bool Player { get; set; } = true;
        [UIValue("configs")] public bool Configs { get; set; } = true;
        [UIValue("mods")] public bool Mods { get; set; } = true;
        [UIValue("maps")] public bool Maps { get; set; } = true;
        [UIValue("models")] public bool Models { get; set; } = true;

        [UIAction("backup")] public void Backup() => Queue(false);
        [UIAction("restore")] public void Restore() => Queue(true);
        [UIAction("cancel")] public void Cancel() {
            pending = null;
            SetQueueStatus("No operation queued");
        }

        void Queue(bool restore) {
            var choices = new[] {
                (Player, "player"), (Configs, "configs"), (Mods, "mods"),
                (Maps, "maps"), (Models, "models")
            };
            pending = choices.Where(choice => choice.Item1).Select(choice => choice.Item2).ToArray();
            restoring = restore;
            SetQueueStatus(pending.Length == 0 ? "Select at least one category"
                : (restoring ? "Restore" : "Backup") + " queued for exit · " + string.Join(", ", pending));
        }

        [OnExit]
        public void Exit() {
            if (pending == null || pending.Length == 0) return;
            try {

                var contents = Directory.GetParent(Application.dataPath)?.Parent;
                if (contents == null || contents.Name != "Contents")
                    throw new InvalidOperationException("Expected a MoltenVR app bundle");
                var instance = contents.Parent?.Parent?.FullName;
                var python = Path.Combine(Root, "player-sync", "venv", "bin", "python");
                var start = new ProcessStartInfo(python) { UseShellExecute = false };
                var backups = Backups;
                var output = restoring ? Path.Combine(backups, File.ReadAllText(Path.Combine(instance, "BeatSaberVersion.txt")).Trim() + ".zip") : backups;
                start.Arguments = string.Join(" ", new[] {
                    "-I", "-m", "bsmac_converter.player_backup", Quote(instance),
                    Quote(Application.persistentDataPath), Quote(output),
                    "--after-pid", Process.GetCurrentProcess().Id.ToString(),
                }.Concat(pending.SelectMany(category => new[] { "--include", category })));
                if (restoring) start.Arguments += " --restore --apply";
                using (Process.Start(start)) { }
            } catch (Exception error) {
                log.Error("Could not start the backup worker: " + error.GetType().Name);
            }
        }

        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
