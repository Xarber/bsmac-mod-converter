# Avatar and Xbox One Kinect findings — 2026-11-24

## Existing Camera2 configuration

The configured positionable third-person camera already has Avatar visibility set to Visible. No avatar renderer plugin was initially installed. Camera2 controls visibility; it does not itself instantiate the user's multiplayer avatar in solo play.

The current official NalulunaModAssistant catalog for 1.40.8 was inspected through its public author-hosted metadata. It lists a supporter-only NalulunaAvatars package and no AvatarsLite entry. Older community instructions referring to free Lite do not establish that a compatible package remains available today. The user has neither a package nor a subscription and explicitly declines payment.

An original free NativeAvatarRenderer is prepared under implementation/tools. It uses the installed game's AvatarSystemCollection and BeatAvatarSystem to instantiate the saved built-in avatar, supplies measured HMD/controller poses, and uses the third-person layer already recognized by Camera2. It does not download a model, randomize the avatar, modify saved avatar data, add an account, or claim full-body tracking. It safely skips scenes without the avatar services or an existing saved avatar. Compilation succeeded against the installed game assemblies; installation, service availability and visual behavior remain pending.

## Kinect alternatives

- [Amethyst](https://github.com/KinectToVR/Amethyst) is a Windows/WinUI project; its Kinect v2 plugin depends on Microsoft's Kinect SDK. It is not a native macOS application.
- [libfreenect2-metal](https://github.com/hbmartin/libfreenect2-metal) is the strongest identified native Apple Silicon foundation for the Xbox One/Kinect v2 sensor. Its README documents a Metal depth pipeline and tested Apple Silicon support, but explicitly excludes native Kinect SDK skeleton tracking. Its supported pose-estimation route combines MediaPipe and registered depth, producing estimated joints.
- [kinect4metal](https://github.com/RESMP-DEV/kinect4metal) is another macOS-native driver foundation. Driver availability alone is not a complete calibrated VR tracker solution.
- [SlimeVR](https://docs.slimevr.dev/) is a possible non-Kinect route, with separate hardware requirements. Do not present it as a Kinect driver or assume its SteamVR path works with the native MoltenVR setup.
- [VRChat OSC trackers](https://docs.vrchat.com/docs/osc-trackers) are an application-specific receiving interface. Their existence does not prove that Beat Saber or MoltenVR receives those trackers.

No ready-to-install, verified Xbox One Kinect → skeleton tracking → calibrated MoltenVR/Beat Saber tracker pipeline was established. The gap is both pose estimation and a supported receiver/protocol, not just USB access. No tracking software, SDK, driver or new paid hardware was installed. A prototype would be a separate development task requiring real Kinect/USB testing and validation of MoltenVR's tracker ingestion interface. Camera2's VMC camera-output settings are not evidence of body-tracker input.
