#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
if [[ $# != 4 && $# != 5 ]]; then
    printf 'Usage: %s ASSEMBLY_TOOL_DLL GAME_RESOURCES BUILT_BRIDGES NEW_STAGE_DIRECTORY [PREPARED_PLUGINS]\n' "$0" >&2
    exit 2
fi
tool=$1
resources=$2
bridges=$3
stage=$4
prepared=${5:-"$resources/Plugins"}
mkdir -- "$stage"
mkdir "$stage/Plugins" "$stage/Libs" "$stage/Managed"
patch() { dotnet "$tool" "$@"; }

# Run on an upstream baseline. Vivify's supplied input must already have its
# converted DepthBlit resource and matching CRC applied.
patch file-uri "$resources/Beat Saber_Data/Managed/BGLib.UnityExtension.dll" "$bridges/LocalMediaBridge.dll" "$stage/Managed/BGLib.UnityExtension.dll"
for mod in NoodleExtensions Chroma; do
    patch compat "$resources/Plugins/$mod.dll" "$bridges/NativeDiagnostics.dll" "$stage/Plugins/$mod.dll"
done
patch bridge "$prepared/Vivify.dll" "$bridges/VivifyMetalBridge.dll" "$stage/Vivify-redirected.dll"
patch vivify-auto "$stage/Vivify-redirected.dll" "$bridges/VivifyMetalBridge.dll" "$stage/Plugins/Vivify.dll"
patch equipment-auto "$resources/Plugins/CustomNotes.dll" "$bridges/VivifyMetalBridge.dll" "$stage/CustomNotes-loose.dll"
patch qosmetics-notes "$stage/CustomNotes-loose.dll" "$bridges/QosmeticsBridge.dll" "$stage/Plugins/CustomNotes.dll"
for mod in CustomWalls CustomSabersLite; do
    input="$resources/Plugins/$mod.dll"
    if [[ -f "$prepared/$mod.dll" ]]; then input="$prepared/$mod.dll"; fi
    patch equipment-auto "$input" "$bridges/VivifyMetalBridge.dll" "$stage/Plugins/$mod.dll"
done
patch beatleader-replay-paths "$prepared/BeatLeader.dll" "$stage/Plugins/BeatLeader.dll"
for library in VivifyMetalBridge QosmeticsBridge NativeDiagnostics; do
    cp "$bridges/$library.dll" "$stage/Libs/"
done
cp "$bridges/LocalMediaBridge.dll" "$stage/Managed/"
cp "$bridges/NativeAvatarRenderer.dll" "$stage/Plugins/"
if [[ $# == 5 ]]; then cp "$prepared/AudioLink.dll" "$stage/Plugins/"; fi

python3.12 - "$stage" "$resources" <<'PY'
import hashlib
import json
from pathlib import Path
import sys

stage, resources = map(Path, sys.argv[1:])
def sha(path):
    if path.is_file():
        with path.open('rb') as stream:
            return hashlib.file_digest(stream, 'sha256').hexdigest()
rows = []
for folder, destination in (
    ('Plugins', 'Plugins'), ('Libs', 'Libs'), ('Managed', 'Beat Saber_Data/Managed')
):
    for source in sorted((stage / folder).glob('*.dll')):
        target = Path(destination) / source.name
        rows.append(dict(source=str(source.relative_to(stage)), target=str(target),
                         sha256=sha(source), previousSha256=sha(resources / target)))
(stage / 'plan.json').write_text(json.dumps(rows, indent=2) + '\n')
print(stage / 'plan.json')
PY
