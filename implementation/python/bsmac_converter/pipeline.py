# SPDX-License-Identifier: AGPL-3.0-only
"""Content-addressed conversion and conservative discovery of new assets."""
import hashlib
import importlib.metadata
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from .index import sha256

def cache_key(source, backend):
    files = sorted(Path(backend).glob('bsmac/shaders/*.pyc'))
    if not files:
        raise ValueError('Separately installed backend not found')
    backend_hash = hashlib.sha256(''.join(sha256(p) for p in files).encode()).hexdigest()
    extensions = hashlib.sha256(''.join(sha256(p) for p in sorted(Path(__file__).parent.glob('*.py'))).encode()).hexdigest()
    return hashlib.sha256(json.dumps([sha256(source), backend_hash, extensions, importlib.metadata.version('UnityPy')]).encode()).hexdigest()

def verify_bundle(original, converted):
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
        if 4 not in a['platforms']:
            if obj.get_raw_data() != target.get_raw_data():
                raise ValueError('Unsupported shader changed')
            continue
        if 14 not in b['platforms']:
            raise ValueError('Converted shader lacks Metal')
        if legacy.read_platform(a, 4)[1] != legacy.read_platform(b, 4)[1]:
            raise ValueError('Original Direct3D bytecode changed')
        shaders += 1
    if shaders == 0:
        raise ValueError('No supported shader evidence')
    return shaders

def convert(source, backend, cache):
    source, backend, cache = Path(source).resolve(), Path(backend).resolve(), Path(cache).resolve()
    key = cache_key(source, backend)
    folder = cache / key
    receipt = folder/'receipt.json'
    result = folder/'Converted'/source.name
    if receipt.is_file() and result.is_file():
        info = json.loads(receipt.read_text())
        if info.get('outputSha256') == sha256(result):
            return result
        raise ValueError('Cached output hash mismatch; inspect cache')
    if shutil.disk_usage(cache.parent if not cache.exists() else cache).free < 2300 * 1024**2:
        raise OSError('Conversion requires at least 2300 MiB available')
    folder.mkdir(parents=True, exist_ok=True)
    log = folder/'conversion.log'
    command = [sys.executable, '-m', 'bsmac_converter.worker', str(source), str(backend), str(folder)]
    with log.open('w') as output:
        process = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT)
    if process.returncode or not result.is_file():
        raise RuntimeError('Conversion failed; inspect ' + str(log))
    # Independent reopen after the worker's own validation and native compilation.
    sys.path.insert(0, str(backend))
    count = verify_bundle(source, result)
    receipt.write_text(json.dumps({'schemaVersion': 1, 'sourceSha256': sha256(source),
        'outputSha256': sha256(result), 'cacheKey': key, 'verifiedShaders': count,
        'nativeMetalCompilation': 'passed', 'gameplay': 'pending'}, indent=2))
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
