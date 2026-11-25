# Automatic Vivify preparation

On native Metal, Vivify 1.0.5 asks the local conversion worker to prepare a required bundle before releasing its Play screen. The hook runs for an existing map bundle and after Vivify finishes downloading a missing one. A regular song without the Vivify requirement does not enter this path. The view shows “Preparing map assets” and retains Vivify's consent, network errors and Back action.

The game's main thread continues while the worker runs. The worker waits for a stable file, checks the original against `Info.dat` and any download CRC sent by Vivify, then verifies and reuses an existing registration. It can also pass through a bundle the local backend identifies as already compatible. New converted derivatives must pass native Metal compilation, bundle verification and a final source validation before being published. The managed loader still independently checks the source hash and uses the original or derivative CRC, as appropriate.

The converter serializes map requests with a local lock. A request can wait up to 30 seconds for the worker and new conversion has a separate nine-minute limit. Requests for the same file and CRC share an active task. Back cancels Play; a task already working can still finish and cache valid bytes for next time. Incomplete conversions show Vivify's error view and keep their reports in the local pipeline cache. An existing experimental registration is retained, without upgrading its compatibility classification. New experimental derivatives require deliberate registration. For visual errors, preserve the affected map, renderer and shader log lines for investigation.

## Local setup and controls

The patch is specific to the inspected Vivify 1.0.5 assembly. Stage it with `AssemblyTool vivify-auto` and the matching bridge, then back up and verify the two install targets before signing. The patch expects the version's existing downloader and checked loader. Keep unrelated Vivify resources intact. Run the persistent-worker installer with Python 3.12, existing Swift and the separately installed MoltenVR backend. The installer stages the Python distribution outside the checkout and leaves the worker disabled until explicitly enabled.

```sh
scripts/install-automatic-worker.sh
runtime="$HOME/Library/Application Support/bsmac/mod-converter/automatic"
"$runtime/venv/bin/python" -I -m bsmac_converter.cli automatic status
"$runtime/venv/bin/python" -I -m bsmac_converter.cli automatic enable
"$runtime/venv/bin/python" -I -m bsmac_converter.cli automatic disable
"$runtime/venv/bin/python" -I -m bsmac_converter.cli rescan-maps
"$runtime/venv/bin/python" -I -m bsmac_converter.cli prune-diagnostics --older-than-days 14
```

Disabling preparation keeps the existing checked loader and registrations. Rollback restores the two backed-up assemblies via the installation receipt and requires app signature verification afterwards. The diagnostic prune removes only generated MSL under old content-addressed pipeline folders. It preserves converted assets, logs, receipts and index rows. The worker uses Application Support and does not require a Desktop folder, checkout, temporary Python environment or system daemon.

Automatic redirection covers Vivify. CustomNotes and CustomWalls require separate loading integrations. CustomNotes loads loose assets synchronously and has a separate memory path for notes; CustomWalls uses an asynchronous file path for loose walls and a separate memory path for embedded walls. The existing CLI `scan` can stage supported loose assets. No automatic replacement of those mods is installed. Physical VR testing must confirm successful Play, fresh download completion, Back during preparation, and normal behavior on non-Vivify songs.
