#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-only
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
root="$HOME/Library/Application Support/bsmac"
resources="$root/instances/1.40.8/Beat Saber.app/Contents/Resources"
python="${BSMAC_PYTHON:-$root/player-sync/venv/bin/python}"
export PYTHONPATH="$repo/implementation/python${PYTHONPATH:+:$PYTHONPATH}"
openssl_prefix="${BSMAC_OPENSSL_PREFIX:-/opt/homebrew/opt/openssl@3}"
rev=dd97d94787994a67263ac8ecadd19119e1da8c8c
managed='Beat Saber_Data/Managed'
target="$managed/Ignorance.dll"
case "${1:-}" in ""|--install) ;; *) echo "Usage: $0 [--install]" >&2; exit 2 ;; esac
test "$#" -le 1
test "$(uname -m)" = arm64
test -f "$resources/$managed/Ignorance.dll"
test -f "$resources/Plugins/BeatTogether.dll"
if [[ $(shasum -a 256 "$resources/$managed/Ignorance.dll" | cut -d " " -f 1) != 48dedfbe5e7ae3fd6abe0aee40070fb515f14ff6cc4c9dea4fb36036e1ec86c8 ]]; then
  echo "This Ignorance build is outside the validated Beat Saber 1.40.8 ABI." >&2
  exit 1
fi
stage=$(mktemp -d "${TMPDIR:-/tmp}/bsmac-enet-stage.XXXXXXXX")
for library in libcrypto.3.dylib libssl.3.dylib; do
  lipo -verify_arch arm64 "$openssl_prefix/lib/$library"
done
git -c advice.detachedHead=false clone -q --depth 1 --branch v2.4.7 https://github.com/SoftwareGuy/ENet-CSharp.git "$stage/enet"
test "$(git -C "$stage/enet" rev-parse HEAD)" = "$rev"
src="$stage/enet/Source/Native"
"$python" "$repo/implementation/tools/beattogether/normalize-darwin-address.py" "$src/enet.h"
xcrun clang -arch arm64 -O2 -dynamiclib -DENET_DLL -I "$src" \
  "$src/enet.c" "$repo/implementation/tools/beattogether/plain-enet.c" \
  -o "$stage/libbsmac-beattogether-enet.dylib"
dotnet build "$repo/implementation/tools/AssemblyTool" -c Release -o "$stage/tool"
dotnet "$stage/tool/AssemblyTool.dll" ignorance-native-imports "$resources/$target" \
  "$resources/Libs/libbsmac-beattogether-enet.dylib" "$openssl_prefix/lib/libcrypto.3.dylib" \
  "$openssl_prefix/lib/libssl.3.dylib" "$stage/Ignorance.dll"
"$python" - "$stage" "$resources" "$target" <<'PY'
import ctypes
import json
import sys
from pathlib import Path
from bsmac_converter.index import sha256

stage, resources, target = map(Path, sys.argv[1:])
library = "libbsmac-beattogether-enet.dylib"
native = ctypes.CDLL(str(stage / library))
assert native.enet_linked_version() == 132103
assert native.enet_initialize() == 0
native.enet_host_create_ssl.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_size_t,
                                      ctypes.c_uint, ctypes.c_uint, ctypes.c_int, ctypes.c_void_p]
native.enet_host_create_ssl.restype = ctypes.c_void_p
native.enet_host_destroy.argtypes = [ctypes.c_void_p]
for tls in (1, 2):
    assert not native.enet_host_create_ssl(None, 1, 1, 0, 0, 0, ctypes.byref(ctypes.c_int32(tls)))
host = native.enet_host_create_ssl(None, 1, 1, 0, 0, 0, ctypes.byref(ctypes.c_int32(0)))
assert host
native.enet_host_destroy(host)
native.enet_deinitialize()

plan = []
for source, relative in [(stage / target.name, target), (stage / library, Path("Libs") / library)]:
    old = resources / relative
    plan.append(dict(source=source.name, target=str(relative), sha256=sha256(source),
                     previousSha256=sha256(old) if old.is_file() else None))
(stage / "plan.json").write_text(json.dumps(plan, indent=2))
print(stage / "plan.json")
PY
if [[ ${1:-} == --install ]]; then
  "$python" -m bsmac_converter.mod_installer apply-local "$stage/plan.json"
fi
