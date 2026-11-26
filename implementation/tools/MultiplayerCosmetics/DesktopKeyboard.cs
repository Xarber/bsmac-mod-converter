// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Bsmac.MultiplayerCosmetics {
    public sealed class DesktopKeyboard : MonoBehaviour {
        static readonly List<DesktopKeyboard> Visible = new List<DesktopKeyboard>();
        internal Component Keyboard;
        void OnEnable() => Visible.Add(this);
        void OnDisable() => Visible.Remove(this);

        void Update() {
            if (Keyboard == null || !Application.isFocused || Visible.Count == 0 ||
                Visible[Visible.Count - 1] != this) return;
            var input = Input.inputString;
            if (string.IsNullOrEmpty(input)) return;
            try {
                var model = Plugin.Property(Keyboard, "Keyboard");
                if (model != null) {
                    AppendToModal(model, input);
                    return;
                }
                var type = Keyboard.GetType();
                var typing = (Action<char>)(AccessTools.Field(type, "textKeyWasPressedEvent") ??
                    AccessTools.Field(type, "keyWasPressedEvent"))?.GetValue(Keyboard);
                var delete = AccessTools.Field(type, "deleteButtonWasPressedEvent")?.GetValue(Keyboard) as Action;
                foreach (char character in input) {
                    if (character == '\b') delete?.Invoke();
                    else if (!char.IsControl(character)) typing?.Invoke(character);
                }
            } catch (Exception e) {
                Plugin.ReportOnce("desktop keyboard", e);
                enabled = false;
            }
        }
        static void AppendToModal(object keyboard, string input) {
            if (!(Plugin.Property(keyboard, "KeyboardText") is Behaviour label) || !label.isActiveAndEnabled) return;
            var property = AccessTools.Property(label.GetType(), "text");
            var value = (string)property.GetValue(label) ?? "";
            foreach (char character in input) {
                if (character == '\b') {
                    if (value.Length != 0) value = value.Substring(0, value.Length -
                        (value.Length > 1 && char.IsSurrogatePair(value, value.Length - 2) ? 2 : 1));
                } else if (!char.IsControl(character)) value += character;
            }
            property.SetValue(label, value);
        }
    }
}
