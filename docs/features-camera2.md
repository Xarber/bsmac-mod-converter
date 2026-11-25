# Camera2 and a full-screen magenta mirror

If the desktop mirror goes magenta in the menu or across multiple songs on a fresh native 1.40.8 installation, check Camera2 0.6.118 first. That pattern is different from individual magenta materials in a Vivify song. A normal headset view with a magenta desktop mirror makes Camera2's desktop compositing path a stronger suspect.

Camera2 loads its embedded `Camera2.Shaders.camera2utils` Unity bundle at startup. It contains luminance key, outline, and volumetric blit shaders. Camera2 can run an image effect across the whole camera texture. The upstream bundle lacks Metal programs. We verified that the reference Mac's working Camera2 already contains a converted bundle and found that the previous fresh installer omitted this conversion. This is a confirmed setup gap. The new report needs a relaunch to establish whether that gap caused the observed screen.

## Repair an existing installation

Close Beat Saber. Use the `testing` branch, with the Python 3.12, .NET and Metal worker prerequisites from the [installation guide](REPLICATE_INSTALLATION.md). From the repository, run:

```sh
resources="$HOME/Library/Application Support/bsmac/instances/1.40.8/Beat Saber.app/Contents/Resources"
stage="$(mktemp -d)/camera2"
python3.12 scripts/stage-fresh-compatibility.py --camera2-only "$resources" "$stage"
# Use the bsmac-mod-installer from the repository's setup virtual environment.
bsmac-mod-installer apply-local "$stage/plan.json"
```

Staging checks the exact original resource SHA-256 and Unity CRC (1881945326), compiles Metal shaders, and inserts the derivative into a staged DLL. Camera2 uses the no-CRC stream-loading overload, so this fix requires no managed CRC rewrite. The installer verifies the original DLL and staged hash, backs up Camera2 and signs the app. There is no need to reinstall the other mods. If staging reports an unexpected shader hash, inspect the installed resource for Metal before attempting another patch.

Launch once through MoltenVR and compare the desktop mirror and headset in the menu and a vanilla song. If the whole screen is still magenta, close the game and temporarily move only `Plugins/Camera2.dll` outside the app while preserving its settings, then re-sign the app and compare again. If removing Camera2 makes no difference, restore it and use the new Player.log and which view was affected to investigate the next rendering path.

New installations via `scripts/install-fresh.sh` include the Camera2 resource fix automatically.
