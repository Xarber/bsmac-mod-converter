// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace MultiplayerCosmetics {
    public sealed class RemoteVisual : MonoBehaviour {
        GameObject visual;
        GameObject source;
        MeshRenderer[] hidden = Array.Empty<MeshRenderer>();
        internal Func<Component> Source;
        internal Color? Tint;
        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        int remainingFrames = 450;

        void LateUpdate() {
            if (Source == null) return;
            if (!Plugin.Options.Enabled) { Source = null; return; }
            try {
                var model = Source();
                var instance = Plugin.Field(model, "liteSaberInstance");
                var custom = instance?.GetType().FullName == "CustomSabersLite.Models.CustomLiteSaber";
                var materials = custom ? Plugin.Field(instance, "colorableMaterials") as Material[] : null;
                if ((custom && Apply(model.gameObject, model.transform, materials)) || --remainingFrames == 0)
                    Source = null;
            } catch (Exception e) {
                Source = null;
                Plugin.ReportOnce("saber visual", e);
            }
        }

        internal bool Apply(GameObject asset, Transform origin, Material[] colorable) {
            if (asset == source && visual != null) return true;
            var meshes = asset.GetComponentsInChildren<MeshRenderer>(true).Where(renderer =>
                renderer.enabled && renderer.GetComponent<MeshFilter>()?.sharedMesh != null &&
                renderer.sharedMaterials.Length > 0 &&
                renderer.sharedMaterials.All(material => material != null && material.shader.isSupported)).ToArray();
            if (meshes.Length == 0) return false;

            Clear();
            var tinted = new HashSet<Material>(colorable ?? Array.Empty<Material>());
            var target = GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.enabled).ToArray();
            var root = new GameObject("Remote cosmetic meshes");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            try {
                foreach (var mesh in meshes) {
                    var leaf = new GameObject(mesh.name);
                    leaf.layer = gameObject.layer;
                    leaf.transform.SetParent(root.transform, false);
                    leaf.transform.localPosition = origin.InverseTransformPoint(mesh.transform.position);
                    leaf.transform.localRotation = Quaternion.Inverse(origin.rotation) * mesh.transform.rotation;
                    var originScale = origin.lossyScale;
                    var scale = mesh.transform.lossyScale;
                    leaf.transform.localScale = new Vector3(scale.x / originScale.x, scale.y / originScale.y, scale.z / originScale.z);
                    leaf.AddComponent<MeshFilter>().sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
                    var destination = leaf.AddComponent<MeshRenderer>();
                    destination.sharedMaterials = mesh.sharedMaterials;
                    var properties = new MaterialPropertyBlock();
                    mesh.GetPropertyBlock(properties);
                    destination.SetPropertyBlock(properties);
                    var materials = destination.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++) {
                        if (!Tint.HasValue || !tinted.Contains(materials[slot])) continue;
                        mesh.GetPropertyBlock(properties, slot);
                        if (properties.isEmpty) mesh.GetPropertyBlock(properties);
                        properties.SetColor(ColorProperty, Tint.Value);
                        destination.SetPropertyBlock(properties, slot);
                    }
                }
                hidden = target;
                foreach (var renderer in hidden) renderer.enabled = false;
                visual = root;
                source = asset;
                root.SetActive(true);
                return true;
            } catch {
                Destroy(root);
                Clear();
                throw;
            }
        }

        internal void Clear() {
            if (visual != null) {
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
                visual.transform.SetParent(null, false);
                visual.SetActive(false);
                Destroy(visual);
            }
            visual = null;
            source = null;
            foreach (var renderer in hidden)
                if (renderer != null) renderer.enabled = true;
            hidden = Array.Empty<MeshRenderer>();
        }
    }
}
