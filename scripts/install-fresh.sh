#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
if [[ $# != 2 || $1 != --install ]]; then
    printf 'Usage: %s --install NEW_WORK_DIRECTORY\n' "$0" >&2
    exit 2
fi
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
root="$HOME/Library/Application Support/bsmac"
backend="$root/mod-converter"
resources="$root/instances/1.40.8/Beat Saber.app/Contents/Resources"
for command in python3.12 dotnet swiftc curl codesign; do command -v "$command" >/dev/null; done
dotnet --list-sdks | awk '$1 ~ /^11\./ { found = 1 } END { exit !found }'
if [[ ! -f "$resources/Beat Saber_Data/Managed/BGLib.UnityExtension.dll" ]]; then
    printf 'Create a native Beat Saber 1.40.8 instance in MoltenVR first.\n' >&2
    exit 1
fi
if [[ -e "$backend/automatic/enabled" ]]; then
    printf 'An automatic converter is already enabled. Use the replication guide for an existing setup.\n' >&2
    exit 1
fi
mkdir -- "$2"
work=$(cd -- "$2" && pwd)
python3.12 -m venv "$work/venv"
"$work/venv/bin/python" -m pip install "$repo"
installer="$work/venv/bin/bsmac-mod-installer"
fetch_verified() {
    curl --fail --location --retry 2 --proto '=https' "$2" -o "$work/$1"
    printf '%s  %s\n' "$3" "$work/$1" | shasum -a 256 -c
}
counters_sha=923fa2d03ec45d2ea1201a9e4a98b14b05557a2ca8976cae65311fc98e091e7b
jd_sha=a83ae3f68921a9698616ecd89d08b7397a550c2464a7871b6c65506ce0c7d360
audio_sha=50e4bbd0858b35c3f52ca8babc388b75f3e547a65a729809d3ee1386c071e252
score_sha=0f486a81df1b2dd5e78ad1b41cefa6142fb19031fd96b96681419b5849f18b2c
plus_sha=fa5cd2d510868662e912a625e4cc67d193168fd49808ac698d1d9137c50dca18
leader_sha=2375bade2b8b51a0451f4d75ad1dc11ff428869d6b84361493ff80736c6b08f3
fetch_verified AudioLink.zip 'https://github.com/Aeroluna/BSAudioLink/releases/download/v1.1.1/AudioLink-1.1.1%2B1.40.8-bs1.40.8-0b127ea.zip' "$audio_sha"
fetch_verified BeatLeader.zip 'https://github.com/BeatLeader/beatleader-mod/releases/download/v0.9.33/BeatLeader-0.9.33-bs1.40.2-1.40.4.zip' "$leader_sha"

fetch_verified ScoreSaber.zip 'https://github.com/ScoreSaber/pc-mod/releases/download/v3.4.2/ScoreSaber-v3.4.2-bs1.40.0-to-1.40.8-9b90ec3.zip' "$score_sha"
fetch_verified BeatSaberPlus.zip 'https://github.com/hardcpp/BeatSaberPlus/releases/download/V6.4.5/BeatSaberPlus_v6.4.5-bs-1.40.0-to-1.40.8.zip' "$plus_sha"

fetch_verified NotCounters.zip 'https://github.com/NotNic182/NotCounters/releases/download/BS-1.40.8-v1.2.0/NotCounters-1.2.0-bs1.40.8.zip' "$counters_sha"
fetch_verified JDFixer.dll 'https://github.com/zeph-yr/JDFixer/releases/download/v.7.4.0/JDFixer.dll' "$jd_sha"

"$installer" seed-catalog "$repo/config/molten-catalog-1.40.8.json"
"$installer" beatmods --profile "$repo/config/beatmods-1.40.8.json"
bash "$repo/scripts/install-automatic-worker.sh"
"$installer" beatmods --profile "$repo/config/beatmods-1.40.8.json" --install
"$installer" install-zip "$work/AudioLink.zip" "$audio_sha" AudioLink.dll
"$installer" install-zip "$work/BeatLeader.zip" "$leader_sha" BeatLeader.dll
"$installer" install-zip "$work/ScoreSaber.zip" "$score_sha" ScoreSaber.dll
"$installer" install-zip "$work/BeatSaberPlus.zip" "$plus_sha" \
    BeatSaberPlus_Multiplayer.dll BeatSaberPlus_SongChartVisualizer.dll ChatPlexSDK_BS.dll
"$installer" install-zip "$work/NotCounters.zip" "$counters_sha" NotCounters.dll
python3.12 "$repo/scripts/stage-fresh-compatibility.py" "$resources" "$work/compatibility"
python3.12 - "$work" "$jd_sha" <<'PYTHON'
import json
from pathlib import Path
import shutil
import sys

work = Path(sys.argv[1])
stage = work / "compatibility"
plan = stage / "plan.json"
target = "Plugins/JDFixer.dll"
shutil.copyfile(work / "JDFixer.dll", stage / target)
rows = json.loads(plan.read_text())
rows.append(dict(source=target, target=target, sha256=sys.argv[2], previousSha256=None))
plan.write_text(json.dumps(rows, indent=2) + "\n")
PYTHON
"$installer" apply-local "$work/compatibility/plan.json"
"$backend/automatic/venv/bin/python" -I -m bsmac_converter.cli automatic enable
