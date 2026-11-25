// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bsmac {
    public sealed class RenderDiagnostics : MonoBehaviour {
        static readonly string Marker = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "bsmac", "mod-converter", "automatic", "render-diagnostics");
        const int Limit = 48;
        string label;
        AssetBundle bundle;
        string filter;
        readonly HashSet<string> reported = new();

        internal static void Observe(string original, string derivative, AssetBundle bundle) {
            try {
                if (bundle == null || !File.Exists(Marker)) return;
                var observer = new GameObject("Bsmac Vivify shader diagnostics").AddComponent<RenderDiagnostics>();
                DontDestroyOnLoad(observer.gameObject);
                observer.bundle = bundle;
                observer.label = Path.GetFileName(Path.GetDirectoryName(original)) + " / " + Path.GetFileName(derivative);
                observer.filter = new FileInfo(Marker).Length <= 1024 ? File.ReadAllText(Marker).Trim() : "";
                observer.StartCoroutine(observer.Sample());
            } catch (Exception error) {
                Debug.LogWarning("[BsmacRender] Unable to start shader diagnostics: " + error.Message);
            }
        }

        IEnumerator Sample() {
            foreach (float delay in new[] {3f, 8f, 15f}) {
                yield return new WaitForSecondsRealtime(delay);
                if (bundle == null || reported.Count >= Limit) break;
                try {
                    foreach (var renderer in FindObjectsOfType<Renderer>()) {
                        if (reported.Count >= Limit) break;
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        Report(renderer);
                    }
                } catch (Exception error) {
                    Debug.LogWarning("[BsmacRender] Material inspection stopped: " + error.Message);
                    break;
                }
            }
            Debug.Log("[BsmacRender] " + label + " sampled renderer diagnostics=" + reported.Count);
            Destroy(gameObject);
        }

        void Report(Renderer renderer) {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length && reported.Count < Limit; ++slot) {
                var material = materials[slot];
                if (material == null) continue;
                var shader = material.shader;
                string shaderName = shader == null ? "<missing>" : shader.name;
                bool supported = shader != null && shader.isSupported;
                bool errorShader = shaderName == "Hidden/InternalErrorShader";
                bool matchesFilter = filter.Length > 0 && shaderName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                if (supported && !errorShader && !matchesFilter) continue;
                string hierarchy = renderer.name;
                for (var node = renderer.transform.parent; node != null && hierarchy.Length < 256; node = node.parent)
                    hierarchy = node.name + "/" + hierarchy;
                var keywords = material.shaderKeywords ?? Array.Empty<string>();
                string line = $"{hierarchy} slot={slot} material={material.name} shader={shaderName} " +
                    $"supported={supported} passes={material.passCount} keywords={string.Join(",", keywords)}";
                if (reported.Add(line)) Debug.LogWarning("[BsmacRender] " + label + " | " + line);
            }
        }
    }
}
