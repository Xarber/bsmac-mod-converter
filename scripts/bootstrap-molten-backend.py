#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
"""Provision the converter locally from the user's installed MoltenVR app."""
import argparse
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile

from PyInstaller.archive.readers import CArchiveReader

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--app', type=Path, default=Path('/Applications/MoltenVR.app'))
parser.add_argument('--backend', type=Path, default=Path.home() / 'Library/Application Support/bsmac/mod-converter')
args = parser.parse_args()
if sys.version_info[:2] != (3, 12):
    parser.error('The bundled converter requires Python 3.12')
payload = args.app.resolve() / 'Contents/Resources/payload/bsmac'
native = payload / 'prebuilt'
for name in ('dxbc2msl', 'libdxbc2msl.dylib'):
    if not (native / name).is_file():
        parser.error(f'MoltenVR translator missing: {name}')
archive = CArchiveReader(str(payload / 'engine/bsmac')).open_embedded_archive('PYZ.pyz')
modules = ('compute', 'convert', 'metalgen', 'pblob', 'rdef', 'translate', 'ushader')
args.backend.mkdir(parents=True, exist_ok=True)
target = args.backend / 'bsmac'
if target.exists():
    parser.error('Backend already exists. Use a separate --backend directory to verify a fresh extraction.')
with tempfile.TemporaryDirectory(dir=args.backend) as scratch:
    package = Path(scratch) / 'bsmac'
    (package / 'shaders').mkdir(parents=True)
    # PyInstaller stores marshalled code without a pyc header.
    header = importlib.util.MAGIC_NUMBER + bytes(12)
    for module in modules:
        data = archive.extract('bsmac.shaders.' + module, raw=True)
        (package / 'shaders' / (module + '.pyc')).write_bytes(header + data)
    for path in (package, package / 'shaders'):
        (path / '__init__.py').touch()
    (package / 'native.py').write_text(
        f'def library():\n    return {str(native / "libdxbc2msl.dylib")!r}\n\n'
        f'def tool():\n    return {str(native / "dxbc2msl")!r}\n')
    subprocess.run([sys.executable, '-I', '-c',
                    'import sys; sys.path.insert(0, sys.argv[1]); from bsmac.shaders import convert, ushader',
                    scratch], check=True)
    package.rename(target)
print(target)
