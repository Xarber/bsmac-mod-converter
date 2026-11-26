#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
root="$HOME/Library/Application Support/bsmac"
resources="$root/instances/1.40.8/Beat Saber.app/Contents/Resources"
runtime="$root/player-sync"
if [[ -e "$resources/Plugins/PlayerBackup.dll" ]]; then
    printf 'Player Backup is already installed. Preserve the existing install before upgrading.\n' >&2
    exit 1
fi
mkdir -p "$runtime"
python3.12 -m venv "$runtime/venv"
"$runtime/venv/bin/python" -m pip install "$repo"
stage=$(mktemp -d "$runtime/stage.XXXXXXXX")
dotnet build "$repo/implementation/tools/PlayerBackup" -c Release --disable-build-servers \
    -p:GameResources="$resources" -o "$stage"
"$runtime/venv/bin/python" - "$stage" <<'PY'
import json
import sys
from pathlib import Path
from bsmac_converter.index import sha256
stage = Path(sys.argv[1])
(stage / "plan.json").write_text(json.dumps([{
    "source": "PlayerBackup.dll", "target": "Plugins/PlayerBackup.dll",
    "sha256": sha256(stage / "PlayerBackup.dll"), "previousSha256": None,
}]))
PY
"$runtime/venv/bin/bsmac-mod-installer" apply-local "$stage/plan.json"
