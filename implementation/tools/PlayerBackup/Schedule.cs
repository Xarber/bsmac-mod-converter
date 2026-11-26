// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;

namespace PlayerBackup {
    public sealed partial class Plugin {
        static readonly string[] Frequencies = { "Manual only", "Every launch", "Every 3 launches", "Every 5 launches", "Every 10 launches" };
        static readonly int[] Intervals = { 0, 1, 3, 5, 10 };
        static string SchedulePath => Path.Combine(Root, "player-sync", "schedule.txt");
        int frequency;
        int launches;
        bool scheduled;
        static readonly string[] Limits = { "3", "5", "10", "20" };
        int limit = 1;
        [UIValue("automatic-limits")] public List<object> AutomaticLimits => new List<object>(Limits);
        [UIValue("automatic-limit")] public string AutomaticLimit {
            get => Limits[limit];
            set {
                int selected = Array.IndexOf(Limits, value);
                if (selected < 0) return;
                limit = selected;
                SaveSchedule();
            }
        }
        [UIValue("frequencies")] public List<object> FrequencyOptions => new List<object>(Frequencies);
        [UIValue("frequency")] public string Frequency {
            get => Frequencies[frequency];
            set {
                int selected = Array.IndexOf(Frequencies, value);
                if (selected < 0 || selected == frequency) return;
                frequency = selected;
                launches = 0;
                SaveSchedule();
            }
        }
        void SaveSchedule() {
            try { File.WriteAllText(SchedulePath, frequency + "\n" + launches + "\n" + limit); }
            catch (IOException) { log.Warn("Could not save automatic backup settings."); }
            catch (UnauthorizedAccessException) { log.Warn("Could not save automatic backup settings."); }
        }
        void ResumeSchedule() {
            if (scheduled) return;
            scheduled = true;
            try {
                if (File.Exists(SchedulePath)) {
                    var lines = File.ReadAllLines(SchedulePath);
                    if (lines.Length >= 2 && int.TryParse(lines[0], out int saved) &&
                        saved >= 0 && saved < Frequencies.Length && int.TryParse(lines[1], out int count)) {
                        frequency = saved;
                        launches = Math.Max(0, Math.Min(count, 10));
                        if (lines.Length == 3 && int.TryParse(lines[2], out int savedLimit) &&
                            savedLimit >= 0 && savedLimit < Limits.Length) limit = savedLimit;
                    }
                }
            } catch (IOException) { log.Warn("Could not read automatic backup settings."); }
            catch (UnauthorizedAccessException) { log.Warn("Could not read automatic backup settings."); }
            if (frequency == 0) return;
            launches++;
            SaveSchedule();
            RunScheduledBackup();
        }

        void RunScheduledBackup() {
            if (frequency == 0 || launches < Intervals[frequency] || !ShowBackup) return;
            if (!HasSession) {
                SetAccountStatus("Automatic backup due. Sign in to continue.");
                return;
            }
            string prefix = CurrentVersion() + "-auto-";
            if (versions.Count(id => id.StartsWith(prefix, StringComparison.Ordinal)) >= int.Parse(AutomaticLimit)) {
                BackupStatus = "Automatic limit reached. Delete an automatic backup or raise the limit.";
                Draw();
                return;
            }
            SaveBackup(true);
        }

        void CompleteSchedule() {
            if (frequency == 0) return;
            launches = 0;
            SaveSchedule();
        }
    }
}
