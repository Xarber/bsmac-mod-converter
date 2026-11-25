# SPDX-License-Identifier: AGPL-3.0-only
"""Build a checked allowlist without changing map metadata or original files."""
import hashlib
import fcntl
import json
import os
import re
import tempfile
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

def read_index(output):
    output = Path(output)
    if not output.exists():
        return {}
    rows = {}
    for line in output.read_text().splitlines():
        digest, crc, metal_crc, metal_digest, path = line.split('\t')
        key = digest, int(crc)
        if (not re.fullmatch('[0-9a-f]{64}', digest)
            or not re.fullmatch('[0-9a-f]{64}', metal_digest)
            or not 0 < key[1] <= 0xffffffff
            or not 0 < int(metal_crc) <= 0xffffffff
            or not Path(path).is_absolute()
            or key in rows):
            raise ValueError('Malformed or duplicate existing Metal index row')
        rows[key] = (digest, key[1], int(metal_crc), metal_digest, path)
    return rows


def validate_original(original, expected_crc=None):
    original = Path(original)
    if expected_crc is None:
        infos = [p for p in original.parent.iterdir() if p.name.lower() == 'info.dat']
        if len(infos) != 1:
            raise ValueError('Expected one Info.dat')
        expected = set(expected_crcs(json.loads(infos[0].read_text(encoding='utf-8-sig'))))
    else:
        expected = {expected_crc}
    actual = bundle_crc(original)
    if actual == 0 or expected != {actual}:
        raise ValueError('Original bundle CRC does not match map metadata')
    return sha256(original), actual


def derivative_row(source_key, derivative):
    derivative = Path(derivative)
    digest, actual = source_key
    new_crc = bundle_crc(derivative)
    if new_crc == 0 or new_crc == actual:
        raise ValueError('Invalid derivative CRC')
    path = str(derivative.resolve())
    if any(c in path for c in '\t\r\n'):
        raise ValueError('Index paths cannot contain line delimiters')
    return digest, actual, new_crc, sha256(derivative), path


def publish_rows(output, updates, *, replace=False):
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.with_suffix('.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        previous = read_index(output)
        rows = previous.copy()
        for row in updates:
            if replace:
                rows[row[:2]] = row
            else:
                rows.setdefault(row[:2], row)
        if not rows:
            raise ValueError('No validated derivatives found; existing index retained')
        if rows == previous:
            return list(rows.values())
        with tempfile.NamedTemporaryFile(mode='w', dir=output.parent, delete=False) as temp:
            try:
                temp.writelines('\t'.join(map(str, row)) + '\n' for row in rows.values())
                temp.flush()
                os.fsync(temp.fileno())
                os.replace(temp.name, output)
                directory = os.open(output.parent, os.O_RDONLY)
                try:
                    os.fsync(directory)
                finally:
                    os.close(directory)
            finally:
                Path(temp.name).unlink(missing_ok=True)
        return list(rows.values())


def build_index(maps, converted, output):
    updates = []
    for original in sorted(Path(maps).rglob('bundleWindows2021.vivify')):
        digest = sha256(original)
        derivative = Path(converted) / (digest[:16] + '.vivify')
        if not derivative.is_file():
            continue
        updates.append(derivative_row(validate_original(original), derivative))
    return publish_rows(output, updates, replace=True)
