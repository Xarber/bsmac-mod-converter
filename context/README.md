# Restart here

This repository tracks the native Beat Saber 1.40.8 / MoltenVR compatibility assignment begun 2026-11-24. Read this page, [the queue](../docs/ACTIVE_TASKS.md), and [project documentation](../docs/INDEX.md) before continuing. Inspect actual state before relying on any recorded result. Do not edit AGENTS.md: the user explicitly excluded it.

## Local environment

- Beat Saber instance: `~/Library/Application Support/bsmac/instances/1.40.8`.
- App resources: `Beat Saber.app/Contents/Resources` inside that instance.
- Managed assemblies: `Resources/Beat Saber_Data/Managed`; `Resources/Data` is a symlink to it.
- Custom levels: `Beat Saber.app/Contents/CustomLevels`.
- Existing converter and separately supplied bytecode backend: `~/Library/Application Support/bsmac/mod-converter`.
- Existing user assets and conversion diagnostics: `~/Desktop/ConvertedMods`.
- Python environment: `/tmp/bsmac-tools/bin/python` (ephemeral; recreate a small virtualenv if missing).
- Installed MoltenVR native translator: `/Applications/MoltenVR.app/Contents/Resources/payload/bsmac/prebuilt`.
- .NET SDK 11.0.100; UnityPy 1.25.4; lz4 4.4.5; native game Unity 2022.3.33f1.

## Safety and scope

The user explicitly controls launching: **do not launch Beat Saber**. Their earlier close/relaunch authorization was superseded. When the user tests, they launch only through MoltenVR; direct-launch commands fail in this setup. Preserve original maps, Info.dat, CRC checks, and working mods. Never globally bypass integrity or AssetBundleLoadingTools safety checks. Its manifest Platforms values are VR modes, not graphics API IDs. Sign the app only after staged verification. Keep reversible backups with hashes. The user explicitly authorized public repository visibility on 2026-11-24; binary releases and external distribution remain unauthorized. Do not redistribute extracted assets, proprietary translator bytecode, game DLLs, private logs or user identity data.

User reports already working: native game, Quest 3 streaming, Steam authentication, BeatLeader, ScoreSaber, ReeSabers v3 and motion blur, CustomNotes all models, CustomSabers, Camera2, PlaylistManager. These are historical user reports, not newly executed verification.

## Confirmed starting findings

Latest user steering: **Do not launch Beat Saber. The user will run one single session themselves.** Do not click MoltenVR Launch or execute a game launch command. Inspect logs and prepare code independently. The prior close/relaunch authorization is superseded for launching.

- Installed Vivify DepthBlit resource has Unity CRC 2484130083; its loading IL contains the matching constant. Original checksum 1746663828.
- Both map originals match Info.dat CRC. Their Metal derivatives were previously converted (61 shaders, 1642 variants); current index generation revalidated hashes and CRCs.
- The latest initial gameplay attempt fails in NoodleExtensions ProcessColorNotesInTimeRowTranspiler. Additional BoundsNullCheckTranspiler and Chroma DebrisSpawnerSkip failures appear. Duplicate bundle errors occur on later retries. The loader bridge alone is insufficient proof of an abort fix.
- Last CustomWalls bundle kind-2 structured buffer uses t0, stride 56, flags 0. Newly staged conversion compiled 80 Metal variants successfully and round-tripped both platforms.
- Exact upstream PC AudioLink asset: Aeroluna/BSAudioLink v1.1.1, `AudioLink-1.1.1+1.40.8-bs1.40.8-0b127ea.zip`. Manifest confirms game 1.40.8; dependencies BSIPA ^4.2.2 and SiraUtil ^3.0.5 are satisfied by locally recorded 4.3.6 and 3.2.1. Its Bundle2021 CRC 2240245297 becomes 3530250200 after conversion; 28 Metal variants compiled successfully. Runtime verification remains pending.
- No CustomAvatars plugin was present in initial plugin inventory; inspect Camera2 configuration before drawing conclusions about avatar rendering.

## Where code lives

`implementation/python/bsmac_converter` contains original Python extensions and CLI. `implementation/tools` contains original C#/Swift compatibility and validation tools. `docs` holds durable architecture and evidence; `context` holds restart context. Local generated work is outside tracked source. Follow the queue and verification report for current installation state, which supersedes these starting findings.

## Latest checkpoint

Read the active queue for current state. Requirement-free Color also returns, while vanilla $100 Bills works. MediaLoader uses FileHelpers in BGLib.UnityExtension, which form-encodes local POSIX paths with plus signs. A narrow LocalMediaBridge correction is installed with saved built-in NativeAvatarRenderer under rollback transaction 20261124T201937894623Z. Synthetic URI roundtrip and compatibility tests passed in installed Mono; game was not launched. Both Vivify derivatives now live in Application Support cache/maps rather than Desktop, with validated hashes. Snow wall crash remains unresolved. Disk below 2 GB blocks further shader conversion; do not delete unrelated caches or user assets. Public source repository is authorized; binary distribution is not.

## November 25 continuation

The user reports that custom maps, the saved Camera2 avatar and original Plasma Notes all work after their separate session. Custom wall compatibility is reported in general. Do not reinstall or replace those components without inspecting the working local state. The 11 live original/derivative pairs were independently checked against the current map metadata and index. Existing experimental rows remain registered as they were. Reported magenta effects remain open.

Resume from T13–T17 in the active queue. Current source integrates local conversion improvements and atomic merge-based index publishing. Modern conversion runs in an importable, separate worker process with bounded serial translation. Explicit experimental conversion produces a receipt with failed/skipped/unsupported counts and does not register automatically. A new 42-flux candidate is private and unregistered. The live index and game were untouched. The user later overrode the weekly checkpoint for this chat. The Vivify asynchronous preparation gate and persistent worker are installed and enabled. First-play UI and cancellation still need user-owned testing. See [automatic preparation](../docs/features-automatic-vivify.md) and T14.

Loose automatic notes, walls and sabers have been installed as well. Verify first use in the headset before calling either automatic path complete. See the equipment feature doc and T14. T18 records the separate SiraUtil FPFC request, which the user prioritized after automatic conversion.

BeatLeader 0.9.33 already includes the PC replay player and in-game downloader. See [replay setup](../docs/features-beatleader-replays.md) and T19 for the standalone downloader and installed local path fix. The user still owns Beat Saber launches.

Second handoff: the working Plasma patch is consolidated and generic checked `.cyoob` preparation is installed. Verify True Acc Bloq V2 and existing Plasma in the user-started game. Multiplayer+ 6.4.5 is installed to address the observed 6.4.2 menu failure. BeatTogether still needs the exact native TLS-enabled ENet transport. The 42-flux experimental candidate resolves all scheduled jobs, with geometry still open. See T20–T23.
