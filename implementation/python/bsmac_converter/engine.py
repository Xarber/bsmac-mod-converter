# SPDX-License-Identifier: AGPL-3.0-only
"""Prepare checked Vivify bundles from a game-owned request or a manual rescan."""
import fcntl
import json
import time
from contextlib import contextmanager
from pathlib import Path

from .crc import bundle_crc
from .index import derivative_row, publish_rows, read_index, sha256, validate_original
from .pipeline import atomic_json, cache_key, convert

MAP_BUNDLE = 'bundleWindows2021.vivify'
RETRY_SECONDS = 300
EQUIPMENT_EXTENSIONS = {
    'CustomNotes': {'.bloq', '.note'},
    'CustomWalls': {'.pixie', '.wall'},
    'CustomSabers': {'.saber'},
}


@contextmanager
def engine_lock(cache, timeout=30):
    cache.mkdir(parents=True, exist_ok=True)
    deadline = time.monotonic() + timeout
    with (cache / 'engine.lock').open('a') as lock:
        while True:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if time.monotonic() >= deadline:
                    raise TimeoutError('Another asset is still preparing. Try again shortly.') from None
                time.sleep(0.25)
        yield


def file_state(path):
    stat = path.stat()
    return stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns


def settled(path):
    previous = file_state(path)
    time.sleep(0.25)
    if not previous[2] or file_state(path) != previous:
        raise ValueError('The bundle is still being written. Try again after downloading finishes.')
    return previous


def needs_conversion(source):
    import UnityPy
    from bsmac.shaders import convert as backend

    # A bundle with no Shader objects can rely on Unity/game-provided assets.
    # UnityPy must still be able to reopen the source before it is accepted.
    if UnityPy.load(str(source)).file is None:
        raise ValueError('Could not reopen the original UnityFS bundle')
    return backend.has_d3d_only_shaders(str(source))


def prepare_map(source, backend, cache, index, maps, *, crc=None):
    source, backend, cache, index, maps = (Path(p).resolve() for p in (source, backend, cache, index, maps))
    if source.name != MAP_BUNDLE or not source.is_relative_to(maps):
        raise ValueError('Auto preparation only accepts Vivify bundles inside the configured CustomLevels directory')
    if crc is not None and not 0 < crc <= 0xffffffff:
        raise ValueError('Vivify did not supply a valid checksum')
    with engine_lock(cache):
        stamp = settled(source)
        original = validate_original(source)
        if crc is not None and original[1] != crc:
            raise ValueError('Vivify download checksum does not match the original map metadata')
        current = read_index(index).get(original)
        if current is not None:
            if derivative_row(original, current[4]) != current:
                raise ValueError('The existing Metal derivative failed validation. Inspect its index entry.')
            if file_state(source) != stamp:
                raise ValueError('The original bundle changed during validation')
            return {'status': 'registered', 'derivative': current[4]}
        if not needs_conversion(source):
            if file_state(source) != stamp:
                raise ValueError('The original bundle changed during validation')
            return {'status': 'original'}

        derivative = convert_with_cooldown(source, backend, cache)
        if file_state(source) != stamp or validate_original(source) != original:
            raise ValueError('The original bundle or its metadata changed while preparing the derivative')
        row = derivative_row(original, derivative)
        registered = {r[:2]: r for r in publish_rows(index, [row])}[original]
        if derivative_row(original, registered[4]) != registered:
            raise ValueError('The selected derivative changed during registration')
        return {'status': 'converted', 'derivative': registered[4]}


def convert_with_cooldown(source, backend, cache):
    retry = cache / cache_key(source, backend) / 'retry.json'
    if retry.exists():
        try:
            failure = json.loads(retry.read_text())
            if time.time() < failure['retryAfter']:
                raise RuntimeError(failure['message'])
        except (ValueError, KeyError, TypeError):
            pass
    try:
        converted = convert(source, backend, cache, timeout=540)
    except RuntimeError as error:
        # Scope expensive translation failures to current bytes and tools.
        retry.parent.mkdir(parents=True, exist_ok=True)
        atomic_json(retry, {'retryAfter': time.time() + RETRY_SECONDS, 'message': str(error)})
        raise
    retry.unlink(missing_ok=True)
    return converted


def prune_diagnostics(cache, older_than_days):
    """Remove only generated Metal source; keep receipts, bundles and reports."""
    import shutil

    if older_than_days < 1:
        raise ValueError('Keep diagnostic sources for at least one day')
    cache = Path(cache).resolve()
    cutoff = time.time() - older_than_days * 86400
    removed = 0
    with engine_lock(cache):
        for work in cache.iterdir():
            if work.is_symlink() or not work.is_dir():
                continue
            name = work.name.removesuffix('-experimental')
            if len(name) != 64 or any(c not in '0123456789abcdef' for c in name):
                continue
            source = work / 'MSL'
            if not source.is_dir() or source.is_symlink() or source.stat().st_mtime >= cutoff:
                continue
            with (work / 'conversion.lock').open('a') as lock:
                try:
                    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                except BlockingIOError:
                    continue
                shutil.rmtree(source)
                removed += 1
    return removed


def equipment_source(source, resources):
    source, resources = Path(source).resolve(), Path(resources).resolve()
    for folder, extensions in EQUIPMENT_EXTENSIONS.items():
        root = resources / folder
        if source.is_relative_to(root) and source.suffix.lower() in extensions:
            return source
    raise ValueError('Unsupported loose equipment path')


def prepare_equipment(source, backend, cache, index, resources):
    source = equipment_source(source, resources)
    backend, cache, index = (Path(p).resolve() for p in (backend, cache, index))
    with engine_lock(cache):
        stamp = settled(source)
        with source.open('rb') as stream:
            if stream.read(8) != b'UnityFS\0':
                raise ValueError('Loose equipment must be a UnityFS bundle')
        if not needs_conversion(source):
            return None
        original = sha256(source), bundle_crc(source)
        if not original[1]:
            raise ValueError('Invalid equipment bundle CRC')
        derivative = convert_with_cooldown(source, backend, cache)
        if file_state(source) != stamp or (sha256(source), bundle_crc(source)) != original:
            raise ValueError('The equipment file changed while preparing it')
        row = derivative_row(original, derivative)
        publish_rows(index, [row], replace=True)
        return row
