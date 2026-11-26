// SPDX-License-Identifier: AGPL-3.0-only
using HarmonyLib;
using UnityEngine;

namespace Bsmac.MultiplayerCosmetics {
    public sealed class RemoteWall : MonoBehaviour {
        Renderer core;
        MeshFilter mesh;
        Material[] originalMaterials;
        Mesh originalMesh;
        GameObject overlay;

        internal void Apply(Renderer wall, Renderer selected, MeshFilter model, object descriptor) {
            core = wall;
            mesh = wall.GetComponent<MeshFilter>();
            originalMaterials = core.sharedMaterials;
            originalMesh = mesh == null ? null : mesh.sharedMesh;
            bool isOverlay = Plugin.Field(descriptor, "Overlay") is true;
            bool replaceMesh = Plugin.Field(descriptor, "ReplaceMesh") is true && model != null && model.sharedMesh != null;
            if (isOverlay) {
                var helper = AccessTools.Method("CustomWalls.Utilities.MeshUtils:CreateOverlay");
                float offset = Plugin.Field(descriptor, "OverlayOffset") is float value ? value : 0f;
                overlay = helper?.Invoke(null, new object[] { core, selected, offset }) as GameObject;
                if (overlay == null) return;
                if (replaceMesh && overlay.TryGetComponent<MeshFilter>(out var overlayMesh))
                    overlayMesh.sharedMesh = model.sharedMesh;
            } else {
                if (selected.sharedMaterials.Length != originalMaterials.Length) return;
                core.sharedMaterials = selected.sharedMaterials;
            }

            if (replaceMesh && mesh != null && (!isOverlay || !(Plugin.Field(descriptor, "ReplaceOnlyOverlayMesh") is true)))
                mesh.sharedMesh = model.sharedMesh;
        }

        internal void Restore() {
            if (core != null) core.sharedMaterials = originalMaterials;
            if (mesh != null) mesh.sharedMesh = originalMesh;
            if (overlay != null) {
                overlay.SetActive(false);
                Destroy(overlay);
            }
            core = null;
            mesh = null;
            overlay = null;
        }

        void OnDisable() => Restore();
    }
}
