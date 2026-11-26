// SPDX-License-Identifier: AGPL-3.0-only
using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using IPA.Config;
using IPA.Config.Stores;

namespace MultiplayerCosmetics {
    public class Preferences {
        public virtual bool Enabled { get; set; } = true;
        public virtual bool DesktopTyping { get; set; } = true;
    }

    public sealed partial class Plugin {
        internal static Preferences Options { get; private set; } = new Preferences();

        static void LoadSettings(Config config) => Options = config.Generated<Preferences>();

        [UIValue("cosmetics")]
        public bool Cosmetics {
            get => Options.Enabled;
            set => Options.Enabled = value;
        }

        [UIValue("desktop-typing")]
        public bool DesktopTyping {
            get => Options.DesktopTyping;
            set => Options.DesktopTyping = value;
        }

        void RegisterSettings() {
            try {
                BSMLSettings.Instance.AddSettingsMenu("Multiplayer Cosmetics", "MultiplayerCosmetics.Settings.bsml", this);
            } catch (Exception error) {
                ReportOnce("settings", error);
            }
        }
    }
}
