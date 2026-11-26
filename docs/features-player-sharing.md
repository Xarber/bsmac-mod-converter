# Share Beat Saber data with another app

Player Backup writes four private, user-owned `latest` records after signed-in startup, sign-in, and successful cloud backup. The source is the last save written to disk. The plugin uses the existing App data consent. X-Center ID controls all destination permissions independently.

| Collection | Contents |
| --- | --- |
| `scores` | Up to 200 highest valid local scores with level ID, difficulty, characteristic, score, combo, and play count. These are local saved bests. |
| `game_stats` | Saved counters by solo, campaign, party, and online mode, including Beat Saber's `timePlayed` counter. |
| `installed_mods` | Installed plugin DLL filenames. |
| `game_version` | Exact instance version. |

The current save does not supply a reliable timestamped recently played history. Player identity, config files, save contents, and backup ZIPs are excluded from these records.

## Publish and request the permissions

The owner of the verified Beat Saber registration opens **Developer Apps → Beat Saber → Manage app storage** in X-Center ID. Add the four collection IDs above to **Shared collections**, preserving other entries, and confirm the change with a passkey. In an app configuration export, these are string entries in `storage.shared`. App verification is required. Do not change Files visibility to publish these collections.

The destination app adds one request per needed collection with the Beat Saber registration's public client ID as `source`, the collection ID as `collection`, and the user-facing purpose as `label`. The user can grant or revoke each one in X-Center ID. The destination reads the collection via `GET /v1/apps/SOURCE_CLIENT_ID/data/COLLECTION` using its user's authorized token. See the [X-Center ID contract](https://github.com/Xarber/xcenter-id/blob/testing/docs/HOSTED_DATA.md#app-to-app-permissions).

## Install Player Backup on Apple Silicon

Install native Beat Saber 1.40.8 in MoltenVR and use the [fresh setup](REPLICATE_INSTALLATION.md) to install BSIPA and BeatSaberMarkupLanguage. Install Python 3.12 and the .NET SDK. Close Beat Saber, then run:

```sh
git clone --branch testing https://github.com/Xarber/bsmac-mod-converter.git
cd bsmac-mod-converter
bash scripts/install-player-backup.sh
```

For an existing Player Backup installation, pass `--replace-existing`. The installer verifies the staged DLL, keeps a rollback receipt, and signs the native app. Start through MoltenVR and open **Mod Settings → Player Backup**. Sign in through X-Center ID and allow App data. Local backups work without sign-in.

## Install Player Backup on Windows

Use PC Beat Saber 1.40.8 with BSIPA and BeatSaberMarkupLanguage already installed. Install Python 3.12 with the Windows Python launcher and the .NET SDK. Close Beat Saber, clone this repository at `testing`, then run in PowerShell from the checkout:

```powershell
.\scripts\install-player-backup.ps1 -GameDirectory 'C:\Program Files (x86)\Steam\steamapps\common\Beat Saber'
```

Pass `-ReplaceExisting` to upgrade. The installer builds against your installed game assemblies and saves the previous DLL when replacing it. The worker lives under `%LOCALAPPDATA%\Bsmac`. The plugin uses Unity's actual persistent data directory and protects browser-approved persistent sessions for the Windows user with DPAPI. Restore waits for normal shutdown and launches the exact restored installation's executable. Keep the owning store client running. Windows runtime, DPAPI and relaunch still require a real PC test.


On macOS, Player Backup waits for the Keychain lookup off the game thread before offering a new sign-in. The account section prompts you to allow access if macOS asks, then resumes the saved session in the same game launch. A rebuilt and re-signed game can still trigger the macOS Keychain prompt. Windows uses its existing protected credential storage.
