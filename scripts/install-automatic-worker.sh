#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail

repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
backend="${1:-$HOME/Library/Application Support/bsmac/mod-converter}"
runtime="$backend/automatic"
python="${BSMAC_INSTALL_PYTHON:-python3.12}"

if [[ -e "$runtime/enabled" ]]; then
    printf 'Disable automatic conversion before updating its worker.\n' >&2
    exit 1
fi
mkdir -p "$runtime"
stage=$(mktemp -d "$runtime/worker-install.XXXXXXXX")
trap 'rm -rf -- "$stage"' EXIT
mkdir -p "$stage/implementation/python"
cp "$repo/pyproject.toml" "$repo/LICENSE" "$stage/"
cp -R "$repo/implementation/python/bsmac_converter" "$stage/implementation/python/"
"$python" -m venv "$runtime/venv"
"$runtime/venv/bin/python" -m pip install "$stage"
if [[ ! -d "$backend/bsmac/shaders" ]]; then
    "$runtime/venv/bin/python" -m pip install 'pyinstaller==6.16.0'
    "$runtime/venv/bin/python" "$repo/scripts/bootstrap-molten-backend.py" --backend "$backend"
fi
swiftc -O "$repo/implementation/tools/compile-metal.swift" -o "$stage/compile-metal"
mv -f "$stage/compile-metal" "$runtime/compile-metal"
"$runtime/venv/bin/python" -I -m bsmac_converter.cli --backend "$backend" automatic status
