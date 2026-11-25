# SPDX-License-Identifier: AGPL-3.0-only
"""Content-addressed conversion and conservative discovery of new assets."""
import hashlib
import fcntl
import importlib.metadata
import json
import shutil
import subprocess
import sys
import zipfile
import os
import tempfile
from pathlib import Path
from .index import sha256

def cache_key(source, backend):
    files = sorted(Path(backend).glob('bsmac/shaders/*.pyc'))
    if not files:
        raise ValueError('Separately installed backend not found')
    backend_hash = hashlib.sha256(''.join(sha256(p) for p in files).encode()).hexdigest()
    extensions = hashlib.sha256(''.join(sha256(p) for p in sorted(Path(__file__).parent.glob('*.py'))).encode()).hexdigest()
    return hashlib.sha256(json.dumps([sha256(source), backend_hash, extensions, importlib.metadata.version('UnityPy')]).encode()).hexdigest()

def verify_bundle(original, converted, *, experimental=False):
    import UnityPy
    from . import legacy
    old = UnityPy.load(str(original))
    new = UnityPy.load(str(converted))
    before = {o.path_id: o for o in old.objects}
    after = {o.path_id: o for o in new.objects}
    if before.keys() != after.keys():
        raise ValueError('Bundle object identities changed')
    shaders = 0
    for ident, obj in before.items():
        target = after[ident]
        if obj.type.name != target.type.name:
            raise ValueError('Bundle object type changed')
        if obj.type.name != 'Shader':
            if obj.get_raw_data() != target.get_raw_data():
                raise ValueError('Non-shader asset changed')
            continue
        a, b = obj.read_typetree(), target.read_typetree()
        if 4 not in a['platforms'] or 14 in a['platforms']:
            if obj.get_raw_data() != target.get_raw_data():
                raise ValueError('Unsupported shader changed')
            continue
        if 14 not in b['platforms']:
            if experimental and obj.get_raw_data() == target.get_raw_data():
                continue
            raise ValueError('Converted shader lacks Metal')
        if legacy.read_platform(a, 4)[1] != legacy.read_platform(b, 4)[1]:
            raise ValueError('Original Direct3D bytecode changed')
        shaders += 1
    if shaders == 0:
        raise ValueError('No supported shader evidence')
    return shaders

def convert(source, backend, cache, *, experimental=False, timeout=900):
    source, backend, cache = Path(source).resolve(), Path(backend).resolve(), Path(cache).resolve()
    source_hash = sha256(source)
    key = cache_key(source, backend) + ('-experimental' if experimental else '')
    folder = cache / key
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / 'conversion.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        if sha256(source) != source_hash:
            raise ValueError('Source changed before conversion')
        return convert_locked(source, backend, folder, key, source_hash, experimental, timeout)


def atomic_json(path, value):
    with tempfile.NamedTemporaryFile(mode='w', dir=path.parent, delete=False) as temp:
        try:
            json.dump(value, temp, indent=2)
            temp.flush()
            os.fsync(temp.fileno())
            os.replace(temp.name, path)
        finally:
            Path(temp.name).unlink(missing_ok=True)


def convert_locked(source, backend, folder, key, source_hash, experimental, timeout):
    receipt = folder / 'receipt.json'
    # The cache is addressed by source bytes, so basename changes reuse it.
    result = folder / 'Converted' / 'asset.bundle'
    if receipt.is_file() and result.is_file():
        info = json.loads(receipt.read_text())
        if (info.get('sourceSha256') == source_hash and info.get('cacheKey') == key
                and info.get('outputSha256') == sha256(result)):
            return result
        raise ValueError('Cached output hash mismatch; inspect cache')
    if shutil.disk_usage(folder).free < 2300 * 1024**2:
        raise OSError('Conversion requires at least 2300 MiB available')
    # A prior interrupted run can leave a staged output without a receipt.
    result.unlink(missing_ok=True)
    log = folder/'conversion.log'
    command = [sys.executable, '-m', 'bsmac_converter.worker', str(source), str(backend), str(folder)]
    if experimental:
        command.append('--experimental')
    with log.open('w') as output:
        process = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    if process.returncode or not result.is_file():
        raise RuntimeError('Conversion failed; inspect ' + str(log))
    # Independent reopen after the worker's own validation and native compilation.
    sys.path.insert(0, str(backend))
    count = verify_bundle(source, result, experimental=experimental)
    if sha256(source) != source_hash:
        raise ValueError('Source changed during conversion')
    report = json.loads((folder / 'program-report.json').read_text())
    totals = {field: sum(shader.get(field, 0) for shader in report)
              for field in ('translated', 'failed', 'unsupported', 'skipped')}
    atomic_json(receipt, {'schemaVersion': 2, 'sourceSha256': source_hash,
        'outputSha256': sha256(result), 'cacheKey': key, 'verifiedShaders': count,
        'nativeMetalCompilation': 'passed', 'programs': totals,
        'compatibility': 'experimental' if experimental else 'translated',
        'gameplay': 'pending'})
    return result

def repack_shader_library(original, converted_bundle, output):
    original, output = Path(original), Path(output)
    if original.resolve() == output.resolve():
        raise ValueError('Repack into a separate path')
    with zipfile.ZipFile(original) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or names.count('shaders.asset') != 1 or names.count('manifest.json') != 1 or archive.testzip():
            raise ValueError('Invalid shader library')
        expected = {i.filename: archive.read(i.filename) for i in archive.infolist()}
        expected['shaders.asset'] = Path(converted_bundle).read_bytes()
        output.parent.mkdir(parents=True, exist_ok=True)
        temp = output.with_suffix('.tmp')
        with zipfile.ZipFile(temp, 'w') as target:
            for item in archive.infolist():
                target.writestr(item, expected[item.filename])
    with zipfile.ZipFile(temp) as check:
        if check.testzip() or any(check.read(name) != data for name, data in expected.items()):
            raise ValueError('Library repack verification failed')
    temp.replace(output)
    return output
