# Replicate the native Beat Saber 1.40.8 setup

The reference system is Apple Silicon with MoltenVR and a legally installed Steam game. The complete observed plugin filename inventory is [INSTALLED_PLUGINS.txt](INSTALLED_PLUGINS.txt). The version-pinned [BeatMods profile](../config/beatmods-1.40.8.json) covers the corresponding catalog mods. The profile is a baseline, since some files in the reference installation have local patches or come directly from upstream.

## Install the baseline

1. Create the native 1.40.8 instance in MoltenVR. Seed its BeatMods cache from this repository before installing mods. Use a clean instance for replication and close Beat Saber before installation. Keep backups of any existing modded instance.
2. From the repository, run the following with Python 3.12. The first installer run verifies and caches the archives. The second invokes the installed MoltenVR engine with the same selections and lets it install BSIPA, the mods, and their dependencies.

```sh
python3.12 -m venv .venv
.venv/bin/python -m pip install -e .
.venv/bin/bsmac-mod-installer seed-catalog config/molten-catalog-1.40.8.json
.venv/bin/bsmac-mod-installer beatmods --profile config/beatmods-1.40.8.json
.venv/bin/bsmac-mod-installer beatmods --profile config/beatmods-1.40.8.json --install
```

The installer reconciles the MoltenVR cache catalog with BeatMods, retains the known archive key, encodes download filenames, and checks the published ZIP member hashes even on cache hits. If an old version is missing or differs from a profile pin, stop and obtain the intended upstream version. Do not substitute an unreviewed version. The upstream install command is intended for a clean instance. Reinstalling into a modified instance can overwrite fixes and requires explicitly passing `--replace-existing`.

## Add the upstream packages outside the baseline

| Component | Reference installation |
| --- | --- |
| BeatLeader | PC 0.9.33 with the local replay-path correction. Use the working installed artifact as a reference before replacing it. |
| ScoreSaber | PC 3.4.2 compatible build. Verify its current package with [upstream](https://github.com/ScoreSaber/pc-mod/releases). |
| AudioLink | [Aeroluna 1.1.1 for 1.40.8](https://github.com/Aeroluna/BSAudioLink/releases/tag/v1.1.1), with the bundle and CRC patch below. |
| BeatSaberPlus | [6.4.5 for 1.40.0–1.40.8](https://github.com/hardcpp/BeatSaberPlus/releases/tag/V6.4.5). The reference uses Multiplayer+, ChatPlexSDK and SongChartVisualizer from that same archive. Lobby verification remains open. |
| ReeSabers | Use your authorized upstream package. The reference 0.4.0 build has a local Metal bundle and blur layout fix. |
| NativeAvatarRenderer | Build the original plugin from `implementation/tools/NativeAvatarRenderer` against the local game resources. |

The inventory also includes the local Qosmetics, Vivify, and native diagnostics libraries under Resources/Libs rather than Plugins. Their sources are in `implementation/tools`. The BeatLeader standalone downloader is documented in [the replay guide](features-beatleader-replays.md).

## Apply the compatibility layer

Build `AssemblyTool` with the installed .NET SDK. For managed bridges, pass the local Resources path as `GameResources`, or its `Beat Saber_Data/Managed` child as `UnityManaged`, as declared by the project. Build and stage into separate files before installation. Patch operations reject unexpected assembly or method shapes.

- **Custom songs and gameplay:** use `file-uri` with LocalMediaBridge on BGLib.UnityExtension and `compat` with NativeDiagnostics on NoodleExtensions and Chroma.
- **Vivify:** use `crc` for the independently validated DepthBlit CRC alongside its converted resource, `bridge` to add the checked redirect, then `vivify-auto` against the redirected output. See [automatic preparation](features-automatic-vivify.md).
- **Equipment:** use `equipment-auto` on the three supported mod assemblies and install VivifyMetalBridge. See [equipment](features-automatic-equipment.md) and [Qosmetics](features-qosmetics.md). For CustomNotes, also run `qosmetics-notes INPUT QOSMETICS_BRIDGE OUTPUT` and install QosmeticsBridge in Libs.
- **Replay:** run `beatleader-replay-paths` on BeatLeader 0.9.33 and enable its existing replay-caching setting.
- **Embedded Unity bundles:** use `AssemblyTool resource INPUT RESOURCE OUTPUT` and `bsmac-mod-converter convert INPUT`. Run `bsmac-mod-converter crc` and hash both files. Use `AssemblyTool replace-resource INPUT RESOURCE ORIGINAL_SHA256 DERIVATIVE DERIVATIVE_SHA256 STAGED_DLL`, then update the specific loader CRC where required. The historical AudioLink, DepthBlit and CustomWalls evidence is in [the verification report](reports/VERIFICATION.md).
- **ReeSabers:** with the local MoltenVR backend on your Python module path, run `buffer-convert.py ORIGINAL STAGE --build`, then `blur-layout-fix.py ORIGINAL STAGE DERIVATIVE` in `implementation/tools/reesabers`. The blur build checks the three compute kernels with Metal. Reinsert the checked derivative with the same embedded-resource flow. These are format-specific tools.
- **Maps and shader libraries:** use `prepare-map`, `rescan-maps`, or `convert` as appropriate. Use the installed automatic worker for on-demand operation. Original and derivative checksums must pass. Geometry-dependent and experimental effects may still be incomplete.

To install staged files, create a local JSON array with entries containing `source` (relative to the plan), `target` (relative to the app's Resources), `sha256`, and `previousSha256` (null for a new file). Run `bsmac-mod-installer apply-local PLAN.json`. It checks installed and staged hashes, creates a rollback receipt, signs the app, and verifies the signature. Then install the worker via `scripts/install-automatic-worker.sh`, enable it per the Vivify guide, and launch yourself through MoltenVR for verification.

This guide reproduces the baseline and describes the local patch build steps. A full clean-room replay of all historical patches, including native FPFC and the separately sourced compatibility libraries, has not been verified. Game binaries, upstream mod ZIPs, maps, private configs, paid assets, and the proprietary MoltenVR translator are supplied locally rather than published with the repo.

For a separately downloaded upstream ZIP, `bsmac-mod-installer install-zip ARCHIVE SHA256 MODULE.dll [MODULE.dll ...]` installs only the explicitly named new `Plugins/` entries with a rollback receipt. Verify the archive hash against a trusted source. Stage patched DLLs and use `apply-local` when the archive includes a mod that needs conversion or when replacing an existing file.

For a prepared baseline with upstream plugin DLLs and converted embedded bundles already in place, `scripts/stage-managed-compatibility.sh ASSEMBLY_TOOL_DLL RESOURCES BRIDGES NEW_STAGE` stages the managed hooks in the correct order and produces the verified-input installation plan. Put the built bridge DLLs together in `BRIDGES`. Review `NEW_STAGE/plan.json` before passing it to `apply-local`. The staging script requires clean hook sites. It deliberately does not patch the original game or install into it.

If MoltenVR's BeatMods game-version lookup returns 404, run `bsmac-mod-installer seed-versions` before opening the Mods UI in step 1. It creates or refreshes `cache/beatmods/versions.json` and ensures 1.40.8 is listed while retaining other cached versions. The installer repeats this immediately before calling the engine. The game-version list and the separate 1.40.8 mod catalog are different files. Seeding the version list alone does not build the mod catalog.

## Bootstrap the converter on a clean Mac

Run `scripts/install-automatic-worker.sh` after installing MoltenVR. If the separately installed converter package is absent, this script uses pinned PyInstaller tooling to extract only the required Python 3.12 shader modules from your installed MoltenVR engine. The native translator stays in MoltenVR.app. The script imports the staged Python modules before publishing the backend and never ships extracted modules in Git. Engines with a different packaging layout or Python version need a compatible extraction tool. The installer does not require the Desktop archive.

`seed-catalog` provides both cache files required to get past the old version lookup. The checked-in catalog is a minimal snapshot of 132 public release records, without binaries or user data. Existing catalog files are retained. The download step still fetches approved releases and checks their member hashes.

## Keep installed assets in the game

Use `python scripts/store-registered-bundles.py APP INDEX` with the game closed. It copies the currently registered derivatives to `Contents/Resources/MetalAssets`, checks their CRC and SHA-256, signs and verifies the app, backs up the index, then redirects its entries to the copies. Supply the live `MetalMapIndex.tsv` from the converter cache. You may run it again after more conversions are registered. The worker, converter backend, working cache, and index live in Application Support. Those tools are distinct from the installed game assets, and MoltenVR itself is still required.

| Material formerly kept on Desktop | Reproduction or storage |
| --- | --- |
| Extracted stock and converted bundles, patched DLLs | Use your upstream packages and AssemblyTool's resource extraction, conversion, resource replacement and loader patches above. |
| ReeSabers scripts and blur reflection | The reproducible scripts and Metal reflection tool are under `implementation/tools/reesabers`. Diagnostic-only variants are archived locally. |
| Notes, sabers, walls, Plasma and shader libraries | Use `convert`, `prepare-equipment`, or `rescan-equipment` and the checked lookup. Embedded wall replacement uses the resource patch workflow. |
| Vivify maps and experimental derivatives | Use `prepare-map`, `convert --experimental` and `audit-shaders`. Historical experimental outputs can differ from current builds, and some special-case geometry edits lack an exact recipe. Keep those derivatives in your local archive. |
| Old shader source dumps, decompiled listings, patch checkpoints and diagnostic logs | The verified private archive at `~/Library/Application Support/bsmac/mod-converter/archive/ConvertedMods` retains them. They are historical evidence and are excluded from Git. |

An inventory with hashes named `ConvertedMods.inventory.json` is alongside the local archive. The desktop directory is not an installation or runtime input.
