// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using IPA;
using UnityEngine;

namespace Bsmac.MultiplayerCosmetics {
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin {
        static IPA.Logging.Logger log;
        static object noteLoader;
        static readonly Component[] localSabers = new Component[2];
        static readonly Harmony Harmony = new Harmony("it.xcenter.bsmac.multiplayer-cosmetics");
        static Type WallLoader => AccessTools.TypeByName("CustomWalls.Utilities.MaterialAssetLoader");

        [Init] public Plugin(IPA.Logging.Logger logger) { log = logger; }

        [OnEnable]
        public void Enable() {
            Patch("CustomNotes.Managers.NoteAssetLoader", "Initialize", nameof(NoteLoaderReady));
            Patch("MultiplayerConnectedPlayerGameNoteController", "Init", nameof(NoteReady), 4);
            Patch("MultiplayerConnectedPlayerObstacleController", "Init", nameof(WallReady), 2);
            Patch("SaberModelContainer", "Start", nameof(SaberReady));
            Patch("UIKeyboard", "Awake", nameof(KeyboardReady));
            Patch("HMUI.UIKeyboard", "Awake", nameof(KeyboardReady));
            Patch("BeatSaberMarkupLanguage.Components.ModalKeyboard", "OnEnable", nameof(KeyboardReady));
        }

        static void Patch(string typeName, string methodName, string hook, int count = 0) {
            var type = AccessTools.TypeByName(typeName);
            var method = type?.GetMethods(AccessTools.allDeclared)
                .SingleOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == count);
            if (method == null) {
                log.Warn("Optional integration unavailable: " + typeName + "." + methodName);
                return;
            }
            try { Harmony.Patch(method, postfix: new HarmonyMethod(typeof(Plugin), hook)); }
            catch (Exception e) { log.Warn("Could not attach " + hook + ": " + e.Message); }
        }

        internal static object Property(object value, string name) =>
            value == null ? null : AccessTools.Property(value.GetType(), name)?.GetValue(value);
        static object StaticProperty(Type type, string name) =>
            type == null ? null : AccessTools.Property(type, name)?.GetValue(null);
        internal static object Field(object value, string name) =>
            value == null ? null : AccessTools.Field(value.GetType(), name)?.GetValue(value);
        static object Selection(object list, object index) =>
            list is IList items && index is int selected && selected >= 0 && selected < items.Count ? items[selected] : null;
        static void NoteLoaderReady(object __instance) => noteLoader = __instance;

        static void NoteReady(Component __instance, object __0) {
            try {
                var existing = __instance.GetComponent<RemoteVisual>();
                var color = Property(__0, "colorType")?.ToString();
                var direction = Property(__0, "cutDirection")?.ToString();
                if (color != "ColorA" && color != "ColorB") { existing?.Clear(); return; }
                var selection = Property(noteLoader, "CustomNoteSelected") is true
                    ? Selection(Property(noteLoader, "CustomNoteObjects"), Property(noteLoader, "SelectedNoteIdx")) : null;
                var side = color == "ColorA" ? "Left" : "Right";
                var prefab = Property(selection, "Note" + (direction == "Any" ? "Dot" : "") + side) as GameObject;
                if (prefab == null) { existing?.Clear(); return; }
                (existing ?? __instance.gameObject.AddComponent<RemoteVisual>()).Apply(prefab, prefab.transform);
            } catch (Exception e) { ReportOnce("note", e); }
        }

        static void WallReady(Component __instance) {
            try {
                var old = __instance.GetComponent<RemoteWall>();
                old?.Restore();
                var loader = WallLoader;
                var selection = Selection(StaticProperty(loader, "CustomMaterialObjects"), StaticProperty(loader, "SelectedMaterial"));
                var selected = Property(selection, "MaterialRenderer") as Renderer;
                var setter = __instance.GetComponentInChildren(AccessTools.TypeByName("ObstacleMaterialSetter"), true);
                var core = Field(setter, "_obstacleCoreRenderer") as Renderer;
                if (selected == null || core == null || Equals(Property(selection, "FileName"),
                    Property(StaticProperty(AccessTools.TypeByName("CustomWalls.Data.CustomMaterial"), "DefaultMaterial"), "FileName"))) return;
                var materials = selected.sharedMaterials;
                if (materials.Length == 0 || materials.Any(m => m == null || m.shader == null || !m.shader.isSupported)) return;
                var descriptor = Property(selection, "Descriptor");
                var model = Property(selection, "MaterialMeshFilter") as MeshFilter;
                (old ?? __instance.gameObject.AddComponent<RemoteWall>()).Apply(core, selected, model, descriptor);
            } catch (Exception e) { ReportOnce("wall", e); }
        }

        static void SaberReady(Component __instance) {
            try {
                var saber = Field(__instance, "_saber");
                var side = Property(saber, "saberType")?.ToString();
                if (side != "SaberA" && side != "SaberB") return;
                int hand = side == "SaberA" ? 0 : 1;
                if (__instance.GetComponentInParent(AccessTools.TypeByName("MultiplayerConnectedPlayerFacade")) == null) {
                    var lite = AccessTools.TypeByName("CustomSabersLite.Components.LiteSaberModelController");
                    if (lite != null) localSabers[hand] = __instance.GetComponentInChildren(lite, true);
                    return;
                }
                var visual = __instance.gameObject.AddComponent<RemoteVisual>();
                visual.Source = () => localSabers[hand];
            } catch (Exception e) { ReportOnce("saber", e); }
        }

        static void KeyboardReady(Component __instance) {
            if (__instance.GetComponent<DesktopKeyboard>() == null)
                __instance.gameObject.AddComponent<DesktopKeyboard>().Keyboard = __instance;
        }

        static readonly System.Collections.Generic.HashSet<string> Reported = new System.Collections.Generic.HashSet<string>();
        internal static void ReportOnce(string feature, Exception e) {
            if (Reported.Add(feature)) log.Warn("Multiplayer Cosmetics " + feature + ": " + e.Message);
        }
    }
}
