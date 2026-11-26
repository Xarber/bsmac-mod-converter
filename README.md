# bsmac-mod-converter

Original, reversible compatibility extensions for user-owned Beat Saber assets on Apple Silicon with MoltenVR. This repository contains Python/C#/Swift source, validation and restart context. It does **not** contain Beat Saber, mods, maps, extracted shaders, MoltenVR bytecode or the native translator.

Start with [context/README.md](context/README.md), [docs/INDEX.md](docs/INDEX.md), and [docs/ACTIVE_TASKS.md](docs/ACTIVE_TASKS.md). Implementation lives under `implementation/`.

Original source is **AGPL-3.0-only**. The separately installed translator is not relicensed or redistributed. Local conversion success is distinct from verified gameplay or headset testing.

## Use

Use Python 3.12 and a separately installed, compatible MoltenVR shader backend. Install this package in a virtual environment with `python -m pip install -e .`. Existing backend path defaults to `~/Library/Application Support/bsmac/mod-converter`.

```sh
bsmac-mod-converter convert /path/to/user-owned.bundle
bsmac-mod-converter scan /path/to/CustomLevels
bsmac-mod-converter watch /path/to/CustomLevels --interval 30
bsmac-mod-converter crc /path/to/bundle
bsmac-mod-converter rollback /path/to/receipt.json
```

Conversions are content-addressed, validated, staged separately and never silently substituted for successful installation. The watch command runs only while its process is running. No background service is implied by installing the CLI. Map allowlists require original SHA-256/CRC and derivative SHA-256/CRC; the narrow Vivify bridge preserves the original loader when no validated derivative exists.

Compatible Vivify bundles can be prepared automatically before Play when the local worker and narrow versioned patch are installed. See [setup, controls and verification limits](docs/features-automatic-vivify.md).

## Player Backup

[Install the Player Backup mod and enable X-Center ID sharing](docs/features-player-sharing.md#install-player-backup-on-apple-silicon).

## Verification

```sh
python -m pytest tests
python scripts/validate-uads.py
```

Native Metal compilation requires existing macOS Swift/Metal tools. Managed patching tools require an existing .NET SDK and local Unity references. No game assemblies are downloaded by the project.
