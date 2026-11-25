# SPDX-License-Identifier: AGPL-3.0-only
"""Prepare a bounded Qosmetics package for the CustomNotes runtime adapter."""
import json
import os
import shutil
import stat
import tempfile
import zipfile
from pathlib import Path, PurePosixPath

from .crc import bundle_crc
from .engine import convert_with_cooldown, engine_lock, file_state, needs_conversion, settled
from .index import sha256
from .pipeline import atomic_json, cache_key

MAX_BUNDLE = 256 * 1024**2
PREFAB = 'assets/qosmetics/_cyoob.prefab'


def extract_pc_bundle(package, target):
    with zipfile.ZipFile(package) as archive:
        entries = archive.infolist()
        names = {entry.filename for entry in entries}
        if len(entries) > 64 or len(names) != len(entries):
            raise ValueError('Qosmetics package has too many or duplicate members')
        for entry in entries:
            path = PurePosixPath(entry.filename)
            if (path.is_absolute() or '..' in path.parts or '\\' in entry.filename
                    or stat.S_ISLNK(entry.external_attr >> 16) or entry.flag_bits & 1):
                raise ValueError('Unsafe Qosmetics archive member')
        config = archive.getinfo('package.json')
        if config.file_size > 64 * 1024:
            raise ValueError('Qosmetics package metadata is too large')
        meta = json.loads(archive.read(config))
        name = meta.get('pcFileName')
        if not isinstance(name, str) or not name or name == meta.get('androidFileName') or name not in names:
            raise ValueError('No supported PC bundle is declared. Android bundles need a separate converter.')
        entry = archive.getinfo(name)
        if entry.is_dir() or not 8 <= entry.file_size <= MAX_BUNDLE:
            raise ValueError('Invalid or oversized Qosmetics PC bundle')
        with archive.open(entry) as stream, target.open('wb') as output:
            shutil.copyfileobj(stream, output, 1024**2)
        descriptor = meta.get('descriptor', {})
        options = meta.get('config', {})
        def label(key, default):
            text = descriptor.get(key, default)
            return text[:512] if isinstance(text, str) else default
        return {'name': label('objectName', package.stem), 'author': label('author', ''),
                'description': label('description', ''), 'showArrows': options.get('showArrows') is True}


def validate_prefab(path):
    import UnityPy
    env = UnityPy.load(str(path))
    pointer = env.container[PREFAB] if PREFAB in env.container else None
    if pointer is None or pointer.type.name != 'GameObject':
        raise ValueError('Expected a supported Qosmetics note prefab')
    root = pointer.read()
    def children(obj):
        transform = next(pair.component.read() for pair in obj.m_Component if pair.component.type.name == 'Transform')
        return {child.read().m_GameObject.read().m_Name: child.read().m_GameObject.read()
                for child in transform.m_Children}
    notes = children(root).get('Notes')
    if notes is None or not {'LeftArrow', 'RightArrow'} <= children(notes).keys():
        raise ValueError('Qosmetics note prefab is missing its required arrow objects')


def prepare_cyoob(source, backend, cache, resources):
    source, backend, cache, resources = (Path(p).resolve() for p in (source, backend, cache, resources))
    root = resources / 'CustomNotes'
    if source.suffix.lower() != '.cyoob' or not source.is_relative_to(root):
        raise ValueError('Put .cyoob packages in CustomNotes to prepare them')
    dest = root / 'Qosmetics'
    if dest.is_symlink():
        raise ValueError('Qosmetics output directory must not be a symlink')
    with engine_lock(cache):
        stamp = settled(source)
        digest = sha256(source)
        key = cache_key(source, backend)
        dest.mkdir(exist_ok=True)
        output = dest / (digest + '.bloq')
        manifest = output.with_suffix('.qos.json')
        if manifest.is_file() and output.is_file():
            info = json.loads(manifest.read_text())
            if (info.get('converterKey') == key and info.get('sha256') == sha256(output)
                    and info.get('sourceSha256') == digest):
                return output
        if shutil.disk_usage(cache).free < 2300 * 1024**2:
            raise OSError('Qosmetics preparation requires at least 2300 MiB available')
        with tempfile.TemporaryDirectory(dir=cache) as workspace:
            bundle = Path(workspace) / 'model.bundle'
            meta = extract_pc_bundle(source, bundle)
            validate_prefab(bundle)
            bundle_crc(bundle)
            derivative = convert_with_cooldown(bundle, backend, cache) if needs_conversion(bundle) else bundle
            validate_prefab(derivative)
            crc = bundle_crc(derivative)
            if not crc or file_state(source) != stamp or sha256(source) != digest:
                raise ValueError('Qosmetics source changed or derivative CRC is invalid')
            meta.update(schema=1, sourceSha256=digest, converterKey=key, sha256=sha256(derivative), crc=crc)
            with tempfile.NamedTemporaryFile(dir=dest, delete=False) as temporary:
                try:
                    with derivative.open('rb') as stream:
                        shutil.copyfileobj(stream, temporary, 1024**2)
                    temporary.flush()
                    os.fsync(temporary.fileno())
                    # Write metadata first. Runtime accepts it only with the matching hash.
                    atomic_json(manifest, meta)
                    os.replace(temporary.name, output)
                finally:
                    Path(temporary.name).unlink(missing_ok=True)
    return output
