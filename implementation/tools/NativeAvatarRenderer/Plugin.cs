// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Threading.Tasks;
using BeatSaber.AvatarCore;
using IPA;
using SiraUtil.Zenject;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace Bsmac.AvatarRendering {
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin {
        internal static IPA.Logging.Logger Log;
        [Init]
        public Plugin(IPA.Logging.Logger logger, Zenjector zenjector) {
            Log = logger;
            zenjector.Install<RendererInstaller>(Location.Menu);
            zenjector.Install<RendererInstaller>(Location.Player);
        }
        [OnEnable] public void Enable() { }
    }

    public sealed class RendererInstaller : Installer {
        public override void InstallBindings() => Container.BindInterfacesTo<RendererService>().AsSingle();
    }

    public sealed class RendererService : IInitializable, IDisposable {
        readonly DiContainer container;
        GameObject host;
        bool disposed;
        public RendererService(DiContainer container) { this.container = container; }
        public void Initialize() { _ = Load(); }
        async Task Load() {
            try {
                var collection = container.TryResolve<AvatarSystemCollection>();
                if (collection == null) { Plugin.Log.Warn("Built-in avatar services are unavailable in this scene; renderer skipped safely."); return; }
                var system = collection.GetAvatarSystem("BeatAvatarSystem");
                if (system == null || system.typeIdentifier.value != "BeatAvatarSystem") {
                    Plugin.Log.Warn("Built-in BeatAvatarSystem not found; no substitute model selected."); return;
                }
                if (!await system.avatarCreated) {
                    Plugin.Log.Warn("No saved built-in avatar is available; choose your avatar in the game's avatar editor first."); return;
                }
                var visual = await collection.GetMultiplayerAvatarsData("BeatAvatarSystem");
                if (disposed) return;
                var avatar = await system.InstantiateAvatar(AvatarDisplayContext.MultiplayerGameplay, 0, container);
                if (avatar == null) { Plugin.Log.Warn("Built-in avatar prefab did not load."); return; }
                if (disposed) { UnityEngine.Object.Destroy(avatar.gameObject); return; }
                host = new GameObject("Bsmac Native Avatar Renderer");
                avatar.transform.SetParent(host.transform, false);
                avatar.transform.localPosition = Vector3.zero;
                avatar.transform.localRotation = Quaternion.identity;
                foreach (var child in host.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 3;
                var tracking = host.AddComponent<LiveAvatarTracking>();
                tracking.Visual = visual;
                avatar.SetVisualDataProvider(tracking);
                avatar.SetPoseDataProvider(tracking);
                avatar.SetLightColor(Color.white);
                tracking.Model = avatar.gameObject;
                tracking.Model.SetActive(false);
                Plugin.Log.Info("Loaded the saved built-in avatar on the third-person layer. Awaiting real HMD/controller poses.");
            } catch (Exception error) {
                Plugin.Log.Error("Built-in avatar renderer could not initialize: " + error);
                Dispose();
            }
        }
        public void Dispose() { disposed = true; if (host != null) UnityEngine.Object.Destroy(host); }
    }

    public sealed class LiveAvatarTracking : MonoBehaviour, IAvatarPoseDataProvider, IAvatarVisualDataProvider {
        internal MultiplayerAvatarsData Visual;
        internal GameObject Model;
        Camera head;
        VRController left, right;
        float nextSearch;
        public AvatarPoseData currentPose { get; private set; }
        public MultiplayerAvatarsData avatarsData => Visual;
        public event Action<AvatarPoseData> poseDidChangeEvent;
        void LateUpdate() {
            if (Time.unscaledTime >= nextSearch && (head == null || left == null || right == null)) {
                nextSearch = Time.unscaledTime + 1f;
                head = Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(c => c.isActiveAndEnabled && c.stereoEnabled &&
                    (c.name == "MainCamera" || c.name == "MenuMainCamera"));
                var controllers = Resources.FindObjectsOfTypeAll<VRController>().Where(c => c.isActiveAndEnabled).ToArray();
                left = controllers.FirstOrDefault(c => c.node == XRNode.LeftHand);
                right = controllers.FirstOrDefault(c => c.node == XRNode.RightHand);
            }
            bool tracked = head != null && head.isActiveAndEnabled && left != null && left.isActiveAndEnabled && right != null && right.isActiveAndEnabled;
            if (Model != null && Model.activeSelf != tracked) Model.SetActive(tracked);
            if (!tracked) return;
            currentPose = new AvatarPoseData(new Pose(head.transform.position, head.transform.rotation),
                new Pose(left.transform.position, left.transform.rotation), new Pose(right.transform.position, right.transform.rotation));
            poseDidChangeEvent?.Invoke(currentPose);
        }
    }
}
