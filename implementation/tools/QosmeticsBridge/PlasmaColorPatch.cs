// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Bsmac.Qosmetics
{
    public sealed class PlasmaColorTarget : MonoBehaviour
    {
    }

    public static class PlasmaColorPatch
    {
        private static bool installed;
        private static int logged;

        private static MaterialPropertyBlock block;

        public static void Install()
        {
            if (installed)
                return;

            try
            {
                Type utils = typeof(CustomNotes.NoteDescriptor)
                    .Assembly.GetType(
                        "CustomNotes.Utilities.Utils",
                        true
                    );

                MethodInfo target = utils.GetMethod(
                    "ColorizeCustomNote",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new Type[] {
                        typeof(Color),
                        typeof(float),
                        typeof(GameObject)
                    },
                    null
                );

                if (target == null)
                    throw new MissingMethodException(
                        "ColorizeCustomNote"
                    );

                MethodInfo prefix = typeof(PlasmaColorPatch)
                    .GetMethod(
                        nameof(Prefix),
                        BindingFlags.Static |
                        BindingFlags.Public
                    );

                new Harmony("bsmac.qosmetics.plasma.colors")
                    .Patch(
                        target,
                        prefix: new HarmonyMethod(prefix)
                    );

                installed = true;

                Debug.Log(
                    "[PlasmaColorFix] Color adapter installed"
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PlasmaColorFix] Installation failed: " + ex
                );
            }
        }

        public static bool Prefix(
            Color __0,
            float __1,
            GameObject __2
        )
        {
            GameObject note = __2;

            if (note == null)
                return true;

            // Preserve the normal CustomNotes implementation
            // for every model not created by our adapter.
            if (note.GetComponent<PlasmaColorTarget>() == null)
                return true;

            try
            {
                if (block == null)
                    block = new MaterialPropertyBlock();

                Color color = __0 * __1;

                int colorId = Shader.PropertyToID("_Color");
                int customId = Shader.PropertyToID("_CustomColors");

                int updated = 0;
                int preserved = 0;

                foreach (
                    Renderer renderer in
                    note.GetComponentsInChildren<Renderer>(true)
                )
                {
                    Material[] materials =
                        renderer.sharedMaterials;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];

                        if (material == null)
                            continue;

                        if (
                            !material.HasProperty(customId) ||
                            material.GetFloat(customId) < 0.5f ||
                            !material.HasProperty(colorId)
                        )
                        {
                            preserved++;
                            continue;
                        }

                        Color tint = color;

                        // Preserve authored transparency.
                        tint.a = material.GetColor(colorId).a;

                        block.Clear();

                        // Preserve any existing per-slot properties.
                        renderer.GetPropertyBlock(block, i);

                        // If no per-slot block exists, inherit the
                        // renderer-wide properties first.
                        if (block.isEmpty)
                            renderer.GetPropertyBlock(block);

                        block.SetColor(colorId, tint);

                        renderer.SetPropertyBlock(block, i);

                        updated++;

                        if (logged < 12)
                        {
                            Debug.Log(
                                "[PlasmaColorFix] " +
                                renderer.name +
                                " slot=" + i +
                                " material=" + material.name +
                                " shader=" + material.shader.name +
                                " color=" + tint
                            );
                        }
                    }
                }

                if (logged++ < 12)
                {
                    Debug.Log(
                        "[PlasmaColorFix] " +
                        note.name +
                        " incoming=" + __0 +
                        " strength=" + __1 +
                        " updated=" + updated +
                        " preserved=" + preserved
                    );
                }

                // Skip the original first-material-only handler
                // for adapted Plasma notes.
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PlasmaColorFix] Color update failed: " + ex
                );

                return true;
            }
        }
    }
}
