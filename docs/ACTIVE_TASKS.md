# Active assignment

Updated 2026-11-24. Preserve originals and working mods; do not edit AGENTS.md. User controls launching: never launch Beat Saber. Usage-window information is unavailable.

| ID | Original request | State | Evidence / next action / blocker |
| --- | --- | --- | --- |
| T01 | Recover previous handoff and diagnostics | Verified | Prior chat and local diagnostics recovered; previous suggested steps were not assumed executed. |
| T02 | Diagnose Vivify and other maps returning to picker | Implemented | Narrow Noodle/Chroma transpiler repairs installed; synthetic tests passed in installed game Mono. All custom maps including requirement-free Color still returned while vanilla $100 Bills worked. Confirmed malformed local song URLs; corrected FileHelpers with LocalMediaBridge, runtime retest pending. |
| T03 | Preserve originals, validate CRC/SHA and narrow Vivify loader | Implemented | Original and derivative CRC/SHA validated. Bridge logged successful validated Metal bundle loads. Two derivative paths moved from Desktop to Application Support cache/maps; original maps and Info.dat unchanged. Retest permission prompt and gameplay. |
| T04 | Final CustomWalls structured buffer conversion | Implemented | 80 Metal variants compiled, fourth bundle installed and signed. PixelWalls works per user; other materials unverified. Snow crash remains T12. |
| T05 | raz0r and broader shader libraries | Blocked | raz0r installed preserving manifest VR-mode Platforms semantics; runtime loaded 6 manifests/644 shaders, visual replacement unverified. Further conversion refused by 2300 MiB disk gate; roughly 533 MiB free at checkpoint. Older 201802150 format handling prepared but not integration verified. |
| T06 | Install compatible PC AudioLink | Implemented | Official 1.1.1+1.40.8 asset and dependencies verified; 28 Metal variants compiled, bundle and CRC installed. Runtime installers observed; reactive appearance pending working custom-song playback. |
| T07 | Public original-source repository, context and implementation folders | Implemented | Xarber/bsmac-mod-converter public, testing default, beta/main seed branches. Original code AGPL-3.0-only; no extracted/proprietary assets published. Latest push must be verified. |
| T08 | CLI, safe automatic conversion, rollback and tests | Implemented | CLI scan/watch stages derivatives with content cache and validation; no persistent watcher running. 17 Python tests passed. New local-file URL and compatibility tests passed in installed Mono. Broader-library integration blocked by disk. |
| T09 | Backed-up installation, signing and runtime evidence | Implemented | Latest transaction 20261124T201937894623Z installs local URI correction and renderer. Ad-hoc signature and deep strict verification passed after installation. No headset verification claimed. |
| T10 | Actual in-game avatar in Camera2, free renderer | Implemented | Original NativeAvatarRenderer compiled and installed. Uses saved built-in avatar and actual HMD/controller poses, no replacement model. Runtime service availability and Camera2 appearance pending. |
| T11 | macOS Kinect v2 alternative to Amethyst | Verified | Research report documents Apple Silicon driver foundations, no verified turnkey skeleton-to-MoltenVR tracker pipeline. No installation or full-body support claim. |
| T12 | MysticalSnowWalls selection crash | Blocked | User reproduced crash. Its named shader is Spawn Holo (Optimized), distinct from final particle-buffer bundle. No matching macOS crash report found; no safe reproducible trace yet. Do not select Snow again until investigated. |

## Next runtime checks

User launches exactly one session through MoltenVR. First test Color (cover/preview/play), then Mapping-only The Wall, Noodle-only Drowned Rotten and Alone, Chroma-only Ignite, finally you. Check Desktop permission prompt and saved avatar in Camera2. Preserve logs after each return/crash. Avoid MysticalSnowWalls. Runtime-dependent items remain unverified until actual evidence arrives.

## History

Initial source commit d50e0f8; compatibility/context commit 2ff7f6b. Initial GitHub quality run 45472973930 passed after public visibility; later revisions require their own checks. No binary release or branch promotion authorized/performed. Unsigned commits used because configured hardware signing key was unavailable.
