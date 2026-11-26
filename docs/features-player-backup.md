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

For model downloads, put a local `model-sources.json` alongside the backup ZIP. Its keys are SHA-256 values from the backup inventory and its values are user-reviewed HTTPS download URLs for the exact loose model bytes. Selecting Models restores missing files with a matching source and lists unresolved models. The converter can prepare supported restored models when loaded. Existing model files are preserved. Automatic map and mod downloads remain pending.

## Select and restore

Use repeatable `--include player`, `--include configs`, `--include mods`, `--include maps`, and `--include models` to choose backup categories. Use `--exclude CATEGORY` to remove a category from the defaults. Creating a partial backup replaces the single local ZIP for that version.

Pass the ZIP path as the third argument with `--restore` to preview destinations without changing files. Add `--include player`, `--include configs`, or `--include models` to select what to restore. Add `--apply` after reviewing the preview. Close the game before applying. Replaced files are preserved under the instance's `PlayerBackup-Rollback` folder. The mod and map inventories require provider-specific resolvers.

## X-Center ID and restoration design

An ad-hoc signed game plugin has no owned HTTPS app callback. Use a separate confidential website registration with an exact owned HTTPS callback and the documented browser handoff. Release activation requires live portal and domain verification. Use a one-time pickup proof, PKCE, state, nonce, validated issuer and audience, and revocable product sessions. Only the backend holds the client secret. No registration or callback for this plugin has been verified.

Use one private record per game version in the signed-in user's namespace, with revision checks and explicit replacement on conflict. X-Center ID `app_data` records are bounded JSON, so use those records for small manifests or pointers. The bulk backup needs an approved private storage path. The issuer advertises separate file scopes, whose permissions and quotas must be evaluated before using them. Keep account consent separate from the controls for player data, reviewed configs, and inventory. Keep unpublished models and credentials out of shared assets.

Restore only after the user reviews a preview and the game has closed. Validate version, paths and hashes and preserve the current files for rollback. Resolve confirmed map IDs through BeatSaver and pinned mod versions through verified installers. Automatically fetch models only from verified origins with permission to download and verify their hashes. Present missing provenance explicitly and use automatic Metal preparation for compatible restored assets.

The registration export lists `https://beatsaber.xcenter.it/api/account/callback`. On November 26 the endpoint returned HTTP 404. Portal activation and callback service deployment remain unverified. The backup plugin is local-only pending backend and sign-in integration.
