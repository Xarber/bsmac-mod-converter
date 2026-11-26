#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
root="$HOME/Library/Application Support/bsmac"
resources="$root/instances/1.40.8/Beat Saber.app/Contents/Resources"
python="${BSMAC_PYTHON:-$root/player-sync/venv/bin/python}"
export PYTHONPATH="$repo/implementation/python${PYTHONPATH:+:$PYTHONPATH}"
stage=$(mktemp -d "${TMPDIR:-/tmp}/bsmac-scoresaber-stage.XXXXXXXX")
target="$resources/Plugins/ScoreSaber.dll"
original=$(shasum -a 256 "$target" | cut -d ' ' -f 1)
dotnet build "$repo/implementation/tools/AssemblyTool" -c Release -o "$stage/tool"
dotnet "$stage/tool/AssemblyTool.dll" scoresaber-multiplayer-pause "$target" \
  "$resources/Beat Saber_Data/Managed/Zenject-usage.dll" "$stage/ScoreSaber.dll"
"$python" - "$stage" "$original" <<'PY'
import json
import sys
from pathlib import Path
from bsmac_converter.index import sha256

stage = Path(sys.argv[1])
(stage / "plan.json").write_text(json.dumps([dict(
    source="ScoreSaber.dll", target="Plugins/ScoreSaber.dll",
    sha256=sha256(stage / "ScoreSaber.dll"), previousSha256=sys.argv[2],
)]))
print(stage / "plan.json")
PY
"$python" -m bsmac_converter.mod_installer apply-local "$stage/plan.json"
