// SPDX-License-Identifier: AGPL-3.0-only
using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using IPA.Config;
using IPA.Config.Stores;

namespace MultiplayerCosmetics {
    public class Preferences {
        public virtual bool Enabled { get; set; } = true;
        public virtual bool Blocks { get; set; } = true;
        public virtual bool Bombs { get; set; } = true;
        public virtual bool Sabers { get; set; } = true;
        public virtual bool Walls { get; set; } = true;
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

        [UIValue("blocks")]
        public bool Blocks {
            get => Options.Blocks;
            set => Options.Blocks = value;
        }

        [UIValue("bombs")]
        public bool Bombs {
            get => Options.Bombs;
            set => Options.Bombs = value;
        }

        [UIValue("sabers")]
        public bool Sabers {
            get => Options.Sabers;
            set => Options.Sabers = value;
        }

        [UIValue("walls")]
        public bool Walls {
            get => Options.Walls;
            set => Options.Walls = value;
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
