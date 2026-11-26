// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MultiplayerCosmetics {
    public sealed class RemoteNote : MonoBehaviour {
        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        GameObject container;
        GameObject source;
        Renderer[] stock = Array.Empty<Renderer>();
        Renderer[] recolorable = Array.Empty<Renderer>();
        Animation[] legacyAnimations = Array.Empty<Animation>();
        Animator[] animators = Array.Empty<Animator>();

        internal void Apply(GameObject asset, Transform anchor, float scale, Color? tint, float strength) {
            if (asset != source || container == null || container.transform.parent != anchor) {
                Clear();
                var original = anchor.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
                var holder = new GameObject("Remote custom note");
                holder.SetActive(false);
                holder.transform.SetParent(anchor, false);
                try {
                    var model = Instantiate(asset, holder.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.SetActive(true);
                    foreach (var child in model.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = anchor.gameObject.layer;

                    var rendererList = model.GetComponentsInChildren<Renderer>(true);
                    if (!rendererList.Any(r => r.enabled && r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.isSupported))) {
                        Destroy(holder);
                        return;
                    }
                    var skipTint = AccessTools.TypeByName("CustomNotes.DisableNoteColorOnGameobject");
                    recolorable = rendererList.Where(r => skipTint == null || r.GetComponent(skipTint) == null).ToArray();
                    foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
                        if (script != null) DestroyImmediate(script);
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                        DestroyImmediate(collider);
                    foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
                        DestroyImmediate(body);
                    foreach (var animation in model.GetComponentsInChildren<Animator>(true))
                        animation.applyRootMotion = false;
                    legacyAnimations = model.GetComponentsInChildren<Animation>(true);
                    animators = model.GetComponentsInChildren<Animator>(true);
                    stock = original;
                    container = holder;
                    source = asset;
                } catch {
                    Destroy(holder);
                    throw;
                }
            }

            container.transform.localScale = Vector3.one * scale;
            container.SetActive(true);
            foreach (var renderer in stock)
                if (renderer != null) renderer.enabled = false;
            foreach (var animator in animators)
                if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                    animator.Rebind();
            foreach (var animation in legacyAnimations)
                if (animation.isActiveAndEnabled && animation.playAutomatically && animation.clip != null) {
                    animation.Stop();
                    animation.Play();
                }
            if (tint.HasValue) {
                var properties = new MaterialPropertyBlock();
                foreach (var renderer in recolorable) {
                    renderer.GetPropertyBlock(properties);
                    properties.SetColor(ColorProperty, tint.Value * strength);
                    renderer.SetPropertyBlock(properties);
                }
            }
        }

        void OnDisable() {
            if (container != null) container.SetActive(false);
            foreach (var renderer in stock)
                if (renderer != null) renderer.enabled = true;
        }

        internal void Clear() {
            OnDisable();
            if (container != null) {
                container.transform.SetParent(null, false);
                Destroy(container);
            }
            container = null;
            source = null;
            stock = Array.Empty<Renderer>();
            recolorable = Array.Empty<Renderer>();
            animators = Array.Empty<Animator>();
            legacyAnimations = Array.Empty<Animation>();
        }
    }
}
