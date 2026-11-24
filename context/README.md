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

The user authorized closing and relaunching Beat Saber when staged fixes are ready. **Launch only through MoltenVR; the user confirmed direct-launch commands fail in this setup.** Preserve original maps, Info.dat, CRC checks, and working mods. Never globally bypass integrity or AssetBundleLoadingTools safety checks. Its manifest Platforms values are VR modes, not graphics API IDs. Sign the app only after staged verification. Keep reversible backups with hashes. No public repository/release authorization has been granted. Do not redistribute extracted assets, proprietary translator bytecode, game DLLs, private logs or user identity data.

User reports already working: native game, Quest 3 streaming, Steam authentication, BeatLeader, ScoreSaber, ReeSabers v3 and motion blur, CustomNotes all models, CustomSabers, Camera2, PlaylistManager. These are historical user reports, not newly executed verification.

## Confirmed starting findings

- Installed Vivify DepthBlit resource has Unity CRC 2484130083; its loading IL contains the matching constant. Original checksum 1746663828.
- Both map originals match Info.dat CRC. Their Metal derivatives were previously converted (61 shaders, 1642 variants); current index generation revalidated hashes and CRCs.
- The latest initial gameplay attempt fails in NoodleExtensions ProcessColorNotesInTimeRowTranspiler. Additional BoundsNullCheckTranspiler and Chroma DebrisSpawnerSkip failures appear. Duplicate bundle errors occur on later retries. The loader bridge alone is insufficient proof of an abort fix.
- Last CustomWalls bundle kind-2 structured buffer uses t0, stride 56, flags 0. Newly staged conversion compiled 80 Metal variants successfully and round-tripped both platforms.
- Exact upstream PC AudioLink asset: Aeroluna/BSAudioLink v1.1.1, `AudioLink-1.1.1+1.40.8-bs1.40.8-0b127ea.zip`. Manifest confirms game 1.40.8; dependencies BSIPA ^4.2.2 and SiraUtil ^3.0.5 are satisfied by locally recorded 4.3.6 and 3.2.1. Its Bundle2021 CRC 2240245297 becomes 3530250200 after conversion; 28 Metal variants compiled successfully. Runtime verification remains pending.
- No CustomAvatars plugin was present in initial plugin inventory; inspect Camera2 configuration before drawing conclusions about avatar rendering.

## Where code lives

`implementation/python/bsmac_converter` contains original Python extensions and CLI. `implementation/tools` contains original C#/Swift compatibility and validation tools. `docs` holds durable architecture and evidence; `context` holds restart context. Local generated work is outside tracked source. Follow the queue and verification report for current installation state, which supersedes these starting findings.
