// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using IPA;
using UnityEngine;

namespace PlayerBackup {
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin {
        readonly IPA.Logging.Logger log;
        string[] pending;
        bool restoring;
        int menuGeneration;
        BSMLSettings settings;

        [Init]
        public Plugin(IPA.Logging.Logger logger) { log = logger; }

        [OnEnable]
        public async void Enable() {
            int generation = ++menuGeneration;
            try {
                await MainMenuAwaiter.WaitForMainMenuAsync();
                if (generation != menuGeneration) return;
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
            log.Info("Queued backup operation canceled.");
        }

        void Queue(bool restore) {
            var choices = new[] {
                (Player, "player"), (Configs, "configs"), (Mods, "mods"),
                (Maps, "maps"), (Models, "models")
            };
            pending = choices.Where(choice => choice.Item1).Select(choice => choice.Item2).ToArray();
            restoring = restore;
            log.Info(pending.Length == 0 ? "Select a category." : "Operation queued until Beat Saber exits.");
        }

        [OnExit]
        public void Exit() {
            if (pending == null || pending.Length == 0) return;
            try {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                        "Library", "Application Support", "bsmac");
                var contents = Directory.GetParent(Application.dataPath)?.Parent;
                if (contents == null || contents.Name != "Contents")
                    throw new InvalidOperationException("Expected a MoltenVR app bundle");
                var instance = contents.Parent?.Parent?.FullName;
                var python = Path.Combine(root, "player-sync", "venv", "bin", "python");
                var start = new ProcessStartInfo(python) { UseShellExecute = false };
                var backups = Path.Combine(root, "player-backups");
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
