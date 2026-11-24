# SPDX-License-Identifier: AGPL-3.0-only
"""Build a checked allowlist without changing map metadata or original files."""
import hashlib
import json
import os
from pathlib import Path
from .crc import bundle_crc

def sha256(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def expected_crcs(value):
    if isinstance(value, dict):
        asset = value.get('_assetBundle')
        if isinstance(asset, dict) and '_windows2021' in asset:
            crc = asset['_windows2021']
            if type(crc) is not int or not 0 < crc <= 0xffffffff:
                raise ValueError('Invalid map CRC')
            yield crc
        for child in value.values():
            yield from expected_crcs(child)
    elif isinstance(value, list):
        for child in value:
            yield from expected_crcs(child)

def build_index(maps, converted, output):
    rows = []
    for original in sorted(Path(maps).rglob('bundleWindows2021.vivify')):
        digest = sha256(original)
        derivative = Path(converted) / (digest[:16] + '.vivify')
        if not derivative.is_file():
            continue
        infos = [p for p in original.parent.iterdir() if p.name.lower() == 'info.dat']
        if len(infos) != 1:
            raise ValueError('Expected one Info.dat')
        expected = set(expected_crcs(json.loads(infos[0].read_text(encoding='utf-8-sig'))))
        actual = bundle_crc(original)
        if expected != {actual}:
            raise ValueError('Original bundle CRC does not match map metadata')
        new_crc = bundle_crc(derivative)
        if new_crc == 0 or new_crc == actual:
            raise ValueError('Invalid derivative CRC')
        path = str(derivative.resolve())
        if any(c in path for c in '\t\r\n'):
            raise ValueError('Index paths cannot contain line delimiters')
        rows.append((digest, actual, new_crc, sha256(derivative), path))
    if not rows:
        raise ValueError('No validated derivatives found; existing index retained')
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    temp = output.with_suffix('.tmp')
    temp.write_text(''.join('\t'.join(map(str, row)) + '\n' for row in rows))
    os.replace(temp, output)
    return rows
