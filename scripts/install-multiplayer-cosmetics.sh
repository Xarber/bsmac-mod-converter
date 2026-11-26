#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
root="$HOME/Library/Application Support/bsmac"
resources="$root/instances/1.40.8/Beat Saber.app/Contents/Resources"
python="${BSMAC_PYTHON:-$root/player-sync/venv/bin/python}"
export PYTHONPATH="$repo/implementation/python${PYTHONPATH:+:$PYTHONPATH}"
stage=$(mktemp -d "${TMPDIR:-/tmp}/bsmac-cosmetics.XXXXXXXX")
dotnet build "$repo/implementation/tools/MultiplayerCosmetics" -c Release -p:GameResources="$resources" -o "$stage"
dotnet build "$repo/implementation/tools/AssemblyTool" -c Release -o "$stage/check"
dotnet "$stage/check/AssemblyTool.dll" verify-ipa-plugin "$stage/MultiplayerCosmetics.dll"
"$python" - "$stage" "$resources" <<'PY'
import json
import sys
from pathlib import Path
from bsmac_converter.index import sha256

stage, resources = map(Path, sys.argv[1:])
source = "MultiplayerCosmetics.dll"
target = "Plugins/" + source
installed = resources / target
(stage / "plan.json").write_text(json.dumps([dict(
    source=source, target=target, sha256=sha256(stage / source),
    previousSha256=sha256(installed) if installed.is_file() else None,
)]))
PY
if [[ ${1:-} == --stage ]]; then
  echo "$stage/plan.json"
else
  "$python" -m bsmac_converter.mod_installer apply-local "$stage/plan.json"
fi
