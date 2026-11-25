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
| T07 | Public original-source repository, context and implementation folders | Implemented | Xarber/bsmac-mod-converter public, testing default, beta/main seed branches. Original code AGPL-3.0-only; no extracted/proprietary assets published. Commit 61df0ab pushed to testing; remote branch SHA verified. |
| T08 | CLI, safe automatic conversion, rollback and tests | Implemented | CLI scan/watch stages derivatives with content cache and validation; no persistent watcher running. 17 Python tests passed. New local-file URL and compatibility tests passed in installed Mono. Broader-library integration blocked by disk. |
| T09 | Backed-up installation, signing and runtime evidence | Implemented | Latest transaction 20261124T201937894623Z installs local URI correction and renderer. Ad-hoc signature and deep strict verification passed after installation. No headset verification claimed. |
| T10 | Actual in-game avatar in Camera2, free renderer | Implemented | Original NativeAvatarRenderer compiled and installed. Uses saved built-in avatar and actual HMD/controller poses, no replacement model. Runtime service availability and Camera2 appearance pending. |
| T11 | macOS Kinect v2 alternative to Amethyst | Verified | Research report documents Apple Silicon driver foundations, no verified turnkey skeleton-to-MoltenVR tracker pipeline. No installation or full-body support claim. |
| T12 | MysticalSnowWalls selection crash | Blocked | User reproduced crash. Its named shader is Spawn Holo (Optimized), distinct from final particle-buffer bundle. No matching macOS crash report found; no safe reproducible trace yet. Do not select Snow again until investigated. |

## Next runtime checks

User launches exactly one session through MoltenVR. First test Color (cover/preview/play), then Mapping-only The Wall, Noodle-only Drowned Rotten and Alone, Chroma-only Ignite, finally you. Check Desktop permission prompt and saved avatar in Camera2. Preserve logs after each return/crash. Avoid MysticalSnowWalls. Runtime-dependent items remain unverified until actual evidence arrives.

## History

Initial source commit d50e0f8; compatibility/context commit 2ff7f6b; local-media fix commit 61df0ab. Initial GitHub quality run 45472973930 passed after public visibility; later revisions require their own checks. No binary release or branch promotion authorized/performed. Unsigned commits used because configured hardware signing key was unavailable.

## Resumed assignment — 2026-11-25

The user's separate session supersedes the earlier avatar and custom-note open status. The user reports working built-in avatar in Camera2, original Plasma Notes, custom maps including Noodle/Mapping Extensions and Chroma, custom walls, and Vivify redirection. Individual wall and AudioLink visual results still need specific evidence. The verified live index contains 11 map pairs. Some effects remain magenta per user report.

| ID | Request | State | Evidence / next action |
| --- | --- | --- | --- |
| T13 | Consolidate CRC, legacy/modern shader conversion and index changes | In Progress | Bounded UnityFS LZMA, targeted unsigned constructor repair, 2019 and 2021 kind-2 handling, separate experimental receipts and merge-safe publication are checkpointed. Real bundles checked; 42-flux particle programs succeed, but four variant jobs share incompatible Metal code and require per-job blob index allocation. Add durable opt-in real-bundle regressions and retest more legacy assets. |
| T14 | Integrate automatic asset preparation and cache management | In Progress | Vivify preparation installed and enabled under rollback receipt 20261125T060312425682Z. Tests verified isolated conversion, index preservation and installed Mono worker execution. User-started Final Slaughter verified uncached conversion, registration, and transition into gameplay. The missing-file download path, Back action and visual parity still require verification. Loose notes, walls and sabers also installed under receipt 20261125T062031452982Z. Host tests passed and a user-started session loaded all nine existing notes with the updated bridge. First-use conversion, note file events and reload still need game verification. External avatars need a loose-asset loader; the working built-in renderer already uses game assets. |
| T15 | Identify and repair remaining magenta materials | In Progress | Offline experimental shader-stage audit completed. Bounded `[BsmacRender]` observer installed and enabled locally under receipt 20261125T151850307320Z. User-owned gameplay needed to associate actual material slots and effects with known candidates. Active map derivatives retained. |
| T16 | Geometry and tessellation parity or documented fallbacks | Queued | Based on 42-flux audit, two families still require geometry. Also inspect Through The Screen and the two grass shaders from the user handoff. Separate translated-program compilation from draw pipeline execution. |
| T17 | Reconcile prior compatibility, avatar/notes, Kinect and release work | Queued | Verify provenance of locally working avatar and Qosmetics implementations before consolidating original source. Continue prior open compatibility checks after T13–T16. No release promotion. |

### Usage checkpoint

At 06:49 Rome on November 25, the five-hour window had 84% remaining and resets November 25 at 11:42. The weekly window had 6% remaining and resets November 30 at 11:09. No reset credit was available. The reset was more than five days away, so the 35% minimum under UADS §0 applies. New work is paused. Resume by checking usage and running the opt-in integration checks for current T13 sources. Then split modern blob indices by job where code differs and use the inspected Vivify lifecycle to implement T14. The user continues to own game launches.

The user overrode the weekly usage checkpoint for this chat. Continue observing the five-hour limit. The automatic Vivify scope advanced as described under T14.

T18 (queued after automatic conversion): enable SiraUtil FPFC with G on the native MoltenVR path. Existing SiraUtil 3.2.1 already loads and its JSON already maps G. Inspect the launch-argument gate, XR lifecycle and Camera2 compatibility before changing binaries. No changes applied to SiraUtil yet.

T19 (2026-11-25): GitHub and BeatLeader replays. Publish project source and docs to `testing`, keeping game assets, configurations and replays local. The installed version includes in-game replay playback/download. The standalone downloader is locally installed and passed a real public download. The local macOS replay path patch and cache setting were installed with backup receipt 20261125T063018371000Z. Deep strict signature verification passed. Later inspection found 11 BSOR files in each of Replays and ReplayerCache, establishing persistence in both corrected directories. Visual replay playback still requires user confirmation.

## Second handoff — November 25

| ID | Request | State | Evidence / next action |
| --- | --- | --- | --- |
| T20 | Preserve working Plasma colors and generalize Qosmetics | Implemented | Snapshot hashes match the previously working bridge. Original patch source consolidated in `implementation/tools/QosmeticsBridge`. `.cyoob` discovery, checked PC extraction and runtime note adaptation installed, receipt 20261125T151418478483Z. Plasma and True Acc Bloq V2 passed conversion rehearsals. True Acc is present for user-owned runtime verification. Android-only, debris, packaged walls/sabers and external avatars remain open. |
| T21 | Restore Multiplayer+ | Implemented | Local 6.4.2 throws in `Present`. Upstream 6.4.5 removes the early multiplexer call and ships an explicit 1.40.8-compatible archive. Installed the three modules already present as a matched set, receipt 20261125T151452347099Z. Verify menu, lobby, and SongChartVisualizer in game. |
| T22 | Restore BeatTogether | Blocked | Installed 2.2.1 is the supported PC release. The logs show auth success followed by an Ignorance native load failure. The Windows ENet library exports TLS functions absent from the inspected public SoftwareGuy fork. Need a verified native arm64 implementation of the exact enet TLS ABI before OpenSSL mapping can help. Do not substitute stock ENet or weaken transport security. |
| T23 | Complete modern program index handling | Implemented | The experimental 42-flux rerun translated 624 jobs and compiled all emitted sources. Two shader families retain 28 unsupported geometry programs. Candidate stays unregistered pending focused runtime diagnostics and a visual test. |

T18 follow-up: later user-started logs show the SiraUtil FPFC controller invoking XR enable and disable repeatedly. The current installation already has an active toggle path. User confirmation of both directions and Camera2 behavior is pending. No SiraUtil replacement was applied here.

## Installer and replication
The original BeatMods cache workaround is ported to the repo as `bsmac-mod-installer`. See the setup guide and installed inventory. Local patch installation is hash-checked with rollback. Continue toward complete clean-instance replication by checking historical patch sites and authorizations against their actual upstream inputs, then testing the complete setup on a clean instance.
