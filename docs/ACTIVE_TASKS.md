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
| T22 | Restore BeatTogether | Implemented | Installed the arm64 ENet adapter for the official plain ENet server. A live BeatTogether lobby is pending. See [installation and rollback](features-beattogether.md). |
| T23 | Complete modern program index handling | Implemented | The experimental 42-flux rerun translated 624 jobs and compiled all emitted sources. Two shader families retain 28 unsupported geometry programs. Candidate stays unregistered pending focused runtime diagnostics and a visual test. |

T18 follow-up: later user-started logs show the SiraUtil FPFC controller invoking XR enable and disable repeatedly. The current installation already has an active toggle path. User confirmation of both directions and Camera2 behavior is pending. No SiraUtil replacement was applied here.

## Installer and replication
The original BeatMods cache workaround is ported to the repo as `bsmac-mod-installer`. See the setup guide and installed inventory. Local patch installation is hash-checked with rollback. Continue toward complete clean-instance replication by checking historical patch sites and authorizations against their actual upstream inputs, then testing the complete setup on a clean instance.

Desktop independence checkpoint: verified 475 archive entries outside Desktop and all 12 live map registrations inside the app. Clean catalog and backend bootstrap tested. Historical exact experimental geometry build recipes and end-to-end clean install remain open.

### Fresh installation follow-up

| ID | Request | State | Evidence / next action |
| --- | --- | --- | --- |
| T24 | Fresh installed MoltenVR instance to patched 1.40.8 with automatic conversion | In Progress | Added pinned AudioLink and BeatLeader acquisition and a single installer for the core patch set. An isolated Apple Silicon rehearsal built the managed bridges and produced the complete core staging plan from upstream DLLs and original managed files. Full clean-instance install and gameplay, the separately authorized ReeSabers package, and unsupported shader effects remain open. |
| T25 | Preserve useful Desktop conversion work before deletion | Verified | The private copy and SHA-256 inventory match all conversion files; five Desktop Finder metadata files changed. The repository archive guide catalogs the material, local evidence, and maintained reproduction tools. |
| T26 | Replace Counters+ with NotCounters after repository work | Implemented | The fresh profile pins NotCounters 1.2.0 for 1.40.8. Verify the local replacement independently, preserving Counters+ and its settings for rollback. |
| T27 | Document and fix Camera2 omission after fresh install showed a magenta screen | Implemented | Camera2-only staging produced a hash-checked plan and a shader resource byte-identical to the working reference. New installs include the same patch. Confirm the affected system's desktop mirror and headset after a user-owned launch. |

T28 — Player Backup plugin installed on the reference Mac after a warning-free build and signed transaction. Local versioned backup, selective restore, and hash-pinned model download tests passed. In-game interaction, map and mod download resolvers, and X-Center ID backend deployment remain pending. See [player backups](features-player-backup.md).

T29 — Fixed the BSIPA manifest namespace rejection from the reference Player.log. Selectable restore now covers model files and reviewed map/mod packages. Added OIDC handoff service and cloud CLI. Pending live menu retest, automatic discovery of source URLs, and production deployment and consent verification.

T30 — Deployed Player Sync on X-Center and verified public health, issuer sign-in entry, and pending pickup. The second game log showed BSMLSettings was accessed before initialization. The installed plugin now waits for the main menu. Runtime menu and authenticated transfer remain pending.

T31 — Added visible queue status, local archive history, X-Center browser login, and current-version upload/download to Player Backup. Build and installation can be verified without launching. Live in-game UI and authenticated transfer remain runtime checks.

T32 — Reactive backup controls, live snapshots, automatic cloud transfers and per-row restore staged. The issuer returned invalid_client for the deployed registration credentials. Existing keys were preserved. Live in-game layout and signed-in backup remain pending.

T33 — Compact the backup layout and report live snapshot progress and operation-specific errors. Verify the native layout on the next user-owned launch.

T34 — Use the native BSML settings container for vertical layout and full-width toggle rows. Native visual verification pending.

T35 — Add opt-in Keychain session reuse, launch-based backup frequency, compact history and cloud-only download rows. Native layout and signed-in runtime verification pending.

T36 — Add multiple dated snapshots per game version, automatic backup cap, and confirmation-based deletion. Browser-hosted renewal consent documented in the SDK. Native layout and signed-in round trip pending.

T37 — Keep delete confirmation in the selected row and constrain feedback to single-line layout. Native visual check pending.

T38 — Hide the empty history footer. Both reference snapshots contained distinct avatar bytes, but no restore receipt was present. The confirmation action now starts the worker before shutdown. The worker waits for the previous process, applies the restore and requests a MoltenVR relaunch only on success. Avatar byte restore and rollback are tested offline. User-owned in-game confirmation remains pending.

T39 — Restore receipt and logs confirmed MoltenVR launched and OpenXR reached a focused session. Added targeted macOS activation of only the already-running instance, and a fixed-height empty-history row. Cloud-only download and foreground restart await native verification.

T40 — The live black-screen process inherited DOORSTOP_INITIALIZED and reported a BSIPA Harmony load failure. Relaunch now clears old injector and launcher state. Empty history uses the existing backup row. Native relaunch verification pending.

T41 — Cloud archives move to delegated personal Files. Legacy archives migrate only for the matching owner after consent and byte verification. Added reversible local-only clearing. Live user consent and migration checks remain pending.

T42 — Use browseable X-Center ID app files scoped to the signed-in user. Import matching legacy personal Files without deleting originals, and fix the plugin cloud-history decoder and cloud labels. Live account sign-in, history and quota visibility remain native checks.

T43 — Export four private user-owned Beat Saber collections. Portal publishing requires the verified registration owner and a passkey. Recent play history is absent from the inspected save. Native export and destination consent remain pending.

T44 — Add Windows paths, installer, DPAPI session storage, and non-destructive process exit checks. Test the layout with synthetic files and compile the managed plugin. Windows runtime verification is pending.

T45 — Re-register Player Backup settings when returning to a main-menu scene, preserving single registration and removing the event on disable. In-game return from a song remains a verification step.

T46 — Stop requesting Files read. The older recovery code remains for explicitly granted tokens. BeatTogether transport remediation is tracked under T22. Multiplayer+ avatar tracking delay remains user-reported and lacks a confirmed cause.
