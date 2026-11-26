# Versioned Beat Saber backup

The local backup tool writes one ZIP per exact game version. A new successful backup replaces that version's local ZIP atomically. With Beat Saber closed, run this from an installed copy of the repository's Python package:

```sh
python -m bsmac_converter.player_backup \
  "$HOME/Library/Application Support/bsmac/instances/1.40.8" \
  "$HOME/Library/Application Support/dev.moltenvr.bsmac.beatsaber" \
  "$HOME/Library/Application Support/bsmac/player-backups"
```

It saves player data, avatar data, controller profiles and settings. It includes recognized config files under UserData by default, including nested folders. Pass `--exclude configs` to turn that category off, or add `--config MOD.json` for each config you want. Review configs before sharing a ZIP. The inventory records local mod DLLs, map Info.dat files, and loose equipment by path and SHA-256. Inventory entries do not contain downloadable assets or download URLs. The archive contains private data in plaintext and stays local.

## In-game controls and installation

On native 1.40.8 with BSIPA and BSML, run `bash scripts/install-player-backup.sh` from the repo while the game is closed. In Mod Settings, open **Player Backup**, select categories, and queue backup or restore. Selections default to on. Use **Cancel queued operation** before exiting to discard it. Actual file work runs after a normal quit. CLI preview remains available before queuing a restore. No game launch is performed by the installer.

For model downloads, put a local `model-sources.json` alongside the backup ZIP. Its keys are SHA-256 values from the backup inventory and its values are user-reviewed HTTPS download URLs for the exact loose model bytes. Selecting Models restores missing files with a matching source and lists unresolved models. The converter can prepare supported restored models when loaded. Existing model files are preserved. For maps and mods, create `package-sources.json` beside the backup. Key each entry by the inventory SHA-256 and give it a `url` and `sha256` for the reviewed upstream ZIP. Maps require a flat Beat Saber ZIP with matching Info.dat. Mods require the original archive with the exact backed-up plugin bytes at Plugins/NAME.dll. A patched mod whose only upstream archive contains different bytes must be rebuilt with the compatibility installer. Sources are machine-local and never imported from cloud backups. The resolver reports files lacking a source or conflicting with existing files.

## Select and restore

Use repeatable `--include player`, `--include configs`, `--include mods`, `--include maps`, and `--include models` to choose backup categories. Use `--exclude CATEGORY` to remove a category from the defaults. Creating a partial backup replaces the single local ZIP for that version.

Pass the ZIP path as the third argument with `--restore` to preview destinations without changing files. Add `--include player`, `--include configs`, or `--include models` to select what to restore. Add `--apply` after reviewing the preview. Close the game before applying. Replaced files are preserved under the instance's `PlayerBackup-Rollback` folder. Select `--include mods` or `--include maps` to restore packages with approved sources.

## X-Center ID and restoration design

An ad-hoc signed game plugin has no owned HTTPS app callback. Use a separate confidential website registration with an exact owned HTTPS callback and the documented browser handoff. Release activation requires live portal and domain verification. Use a one-time pickup proof, PKCE, state, nonce, validated issuer and audience, and revocable product sessions. Only the backend holds the client secret. No registration or callback for this plugin has been verified.

Use one private record per game version in the signed-in user's namespace, with revision checks and explicit replacement on conflict. X-Center ID `app_data` records are bounded JSON, so use those records for small manifests or pointers. The bulk backup needs an approved private storage path. The issuer advertises separate file scopes, whose permissions and quotas must be evaluated before using them. Keep account consent separate from the controls for player data, reviewed configs, and inventory. Keep unpublished models and credentials out of shared assets.

Restore only after the user reviews a preview and the game has closed. Validate version, paths and hashes and preserve the current files for rollback. Resolve confirmed map IDs through BeatSaver and pinned mod versions through verified installers. Automatically fetch models only from verified origins with permission to download and verify their hashes. Present missing provenance explicitly and use automatic Metal preparation for compatible restored assets.

The registration export lists `https://beatsaber.xcenter.it/api/account/callback`. On November 26 the endpoint returned HTTP 404. The server and browser handoff client are implemented. Portal activation and deployment remain unverified. See [deployment and verification](features-player-sync-service.md).

## Live backups and restore

Select categories and choose **Back up now** from the main menu. The worker reads the last saves written to disk and aborts if a file changes during its read. When signed in, a successful local backup uploads automatically with a revision check.

Local and cloud backups appear together by version. Select **Restore** beside the current version's backup. A cloud-only archive downloads and validates immediately. The selected categories apply after quitting. Choose **Restore and restart** to confirm. Beat Saber quits, the worker applies the selected backup, and the MoltenVR launcher opens the restored version on success. After launching, the worker asks macOS to bring the running Beat Saber window forward. If restoration fails, the game stays closed. Worker details are written to `player-sync/restore-result.log` under the bsmac Application Support folder. Package restoration still requires configured sources.

Choose **Sign in to X-Center ID** to open the issuer in the browser. Cancel ends local polling. The account session stays in memory. Issuer credential rejection is shown in the browser and in the game.

The compact menu keeps account status and sign-in on one row. Backup progress and errors appear beside the backup action, and restore feedback stays with backup history.

The **Automatic backup** dropdown counts main-menu launches. Manual only is the default. A due backup runs after account sign-in and resets the counter when its cloud upload succeeds. Choose **Stay signed in** on the X-Center ID consent page. If the issuer grants renewal, the plugin stores only the product session in macOS Keychain for up to 14 days. Declining keeps the session in memory. Server restarts currently require sign-in again. When a backup is due and sign-in is needed, the menu shows a prompt. No provider tokens or client secret are stored in the plugin.

Cloud-only entries offer **Download**. Downloading saves a local ZIP without modifying the game. The current version then offers **Restore**. Older versions can be listed and downloaded without being applied to the running version.

## Multiple backups per version

New in-game backups have separate dated snapshot IDs. Existing version-only backups remain available. Manual backups have no count limit. The automatic backup limit is per game version and defaults to five. Scheduled backups pause at the limit until you delete an automatic snapshot or raise the limit. Select **Delete** beside an item and confirm to remove its local file and, when signed in, the matching cloud copy. The legacy CLI continues to use a single version-only ZIP.

Delete confirmation stays in the selected backup row. Backup feedback stays beside the backup action and list feedback stays below the list.

**Clear local backups** moves local ZIPs into a recoverable folder under `player-sync/cleared-backups` after confirmation. It leaves the user's cloud Files alone. After clearing, entries in personal Files show **Download**.
