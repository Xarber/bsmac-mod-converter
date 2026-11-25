# SPDX-License-Identifier: AGPL-3.0-only
"""Prepare verified BeatMods archives for MoltenVR's native installer."""
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
import zipfile
from pathlib import Path, PurePosixPath
from urllib.parse import quote, urljoin, urlsplit

from .index import sha256
from .pipeline import atomic_json
from .transaction import install, rollback

VERSION = '1.40.8'
BASE = 'https://beatmods.com'
ROOT = Path.home() / 'Library/Application Support/bsmac'
ENGINE = Path('/Applications/MoltenVR.app/Contents/Resources/payload/bsmac/engine/bsmac')


def curl(url, *args):
    return subprocess.check_output(['/usr/bin/curl', '-fsSL', '--proto', '=https',
                                    '--proto-redir', '=https', '--max-time', '180',
                                    '--max-filesize', '268435456', '--retry', '2', url, *args])


def resolve(names, catalog, approved, pins=None):
    entries = {item['mod']['name'].casefold(): item for item in catalog}
    by_id = {item['latest']['id']: item for item in catalog}
    releases = {(item['name'].casefold(), item['version']): item for item in approved}
    result, visiting, done = [], set(), set()
    pins = pins or {}

    def visit(entry):
        latest = entry['latest']
        name, version = entry['mod']['name'], latest['modVersion']
        identifier = latest['id']
        if identifier in done:
            return
        if identifier in visiting:
            raise ValueError('Dependency cycle at ' + name)
        if name.casefold() in pins and version != pins[name.casefold()]:
            raise ValueError(f'{name}: MoltenVR catalog has {version}, profile requires {pins[name.casefold()]}')
        visiting.add(identifier)
        for dependency in latest['dependencies']:
            if dependency not in by_id:
                raise ValueError(f'{name}: missing catalog dependency {dependency}')
            visit(by_id[dependency])
        visiting.remove(identifier)
        match = releases.get((name.casefold(), version))
        if match is None:
            raise ValueError(f'{name} {version} is absent from the approved BeatMods list')
        download = next((d for platform in ('universal', 'steam')
                         for d in match['downloads'] if d['type'] == platform), None)
        if download is None or not re.fullmatch(r'[0-9a-f]{32,64}', latest['zipHash']):
            raise ValueError(f'{name}: no supported archive')
        result.append((name, version, latest['zipHash'], download))
        done.add(identifier)

    for name in ['BSIPA', *names]:
        if name.casefold() not in entries:
            raise ValueError(f'{name} missing from MoltenVR catalog')
        visit(entries[name.casefold()])
    return result


def verify_archive(path, download):
    expected = {entry['file']: entry['hash'].lower() for entry in download.get('hashMd5', [])}
    if not expected:
        raise ValueError('Archive has no published member hashes')
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        names = {entry.filename for entry in entries}
        if len(names) != len(entries) or sum(entry.file_size for entry in entries) > 512 * 1024**2:
            raise ValueError('Duplicate or oversized archive contents')
        for entry in entries:
            name = entry.filename
            if (PurePosixPath(name).is_absolute() or '..' in PurePosixPath(name).parts
                    or ':' in name or chr(92) in name or entry.external_attr >> 28 == 10):
                raise ValueError('Unsafe mod archive path')
        if expected.keys() != {entry.filename for entry in entries if not entry.is_dir()}:
            raise ValueError('Archive members differ from the approved hash manifest')
        for name, digest in expected.items():
            with archive.open(name) as stream:
                if hashlib.file_digest(stream, 'md5').hexdigest() != digest:
                    raise ValueError(f'BeatMods member hash mismatch: {name}')


def cache_archive(folder, archive_id, download):
    folder.mkdir(parents=True, exist_ok=True)
    destination = folder / (archive_id + '.zip')
    if destination.is_file():
        try:
            verify_archive(destination, download)
            return
        except (ValueError, zipfile.BadZipFile):
            pass
    url = urlsplit(urljoin(BASE, download['url']))
    if url.scheme != 'https' or url.hostname != 'beatmods.com' or url.username or url.password:
        raise ValueError('Unexpected BeatMods download origin')
    with tempfile.TemporaryDirectory(dir=folder) as stage:
        archive = Path(stage) / 'download.zip'
        curl(url._replace(path=quote(url.path, safe='/%')).geturl(), '-o', str(archive))
        verify_archive(archive, download)
        archive.replace(destination)


def require_closed():
    if 'Beat Saber.app/Contents/MacOS/Beat Saber' in subprocess.check_output(['ps', '-axo', 'comm'], text=True):
        raise RuntimeError('Close Beat Saber before installing mods')


def seed_versions(cache):
    """Keep the supported version discoverable when MoltenVR's lookup returns 404."""
    cache.mkdir(parents=True, exist_ok=True)
    path = cache / 'versions.json'
    versions = json.loads(path.read_text()) if path.exists() else []
    if not isinstance(versions, list) or any(not isinstance(v, str) for v in versions):
        raise ValueError('Unexpected MoltenVR version cache format')
    if VERSION not in versions:
        atomic_json(path, [VERSION, *versions])
    path.touch()
    return path


def sign(app):
    subprocess.run(['codesign', '--force', '--deep', '--sign', '-', str(app)], check=True)
    subprocess.run(['codesign', '--verify', '--deep', '--strict', str(app)], check=True)


def apply_local(root, plan_file):
    require_closed()
    instance = root / 'instances' / VERSION
    app = instance / 'Beat Saber.app'
    resources = (app / 'Contents/Resources').resolve(strict=True)
    plan = []
    for row in json.loads(plan_file.read_text()):
        source = (plan_file.parent / row['source']).resolve()
        target = resources / row['target']
        relative = Path(row['target'])
        if not relative.parts or relative.parts[0] not in ('Plugins', 'Libs', 'Beat Saber_Data', 'UserData'):
            raise ValueError('Target is outside supported mod folders')
        if not target.resolve().is_relative_to(resources) or sha256(source) != row['sha256']:
            raise ValueError('Target escaped Resources or source hash changed')
        old = sha256(target) if target.is_file() else None
        if old != row['previousSha256']:
            raise ValueError(f'Unexpected installed file: {relative}')
        plan.append((source, target))
    if not plan:
        raise ValueError('Empty installation plan')
    receipt = install(plan, instance / 'Compatibility-Rollback')
    try:
        sign(app)
    except subprocess.CalledProcessError:
        rollback(receipt)
        sign(app)
        raise
    print(receipt)


def install_zip(root, archive_path, expected_hash, modules):
    require_closed()
    if sha256(archive_path) != expected_hash.lower():
        raise ValueError('Upstream archive SHA-256 mismatch')
    resources = root / 'instances' / VERSION / 'Beat Saber.app/Contents/Resources'
    with zipfile.ZipFile(archive_path) as archive, tempfile.TemporaryDirectory() as folder:
        stage = Path(folder)
        rows = []
        for module in modules:
            if not re.fullmatch(r'[A-Za-z0-9_.+ -]+\.dll', module, re.ASCII):
                raise ValueError('Expected a DLL filename')
            target = 'Plugins/' + module
            entries = [entry for entry in archive.infolist() if entry.filename == target]
            if len(entries) != 1 or entries[0].file_size > 64 * 1024**2:
                raise ValueError('Missing, duplicate or oversized upstream module: ' + module)
            payload = stage / module
            payload.write_bytes(archive.read(entries[0]))
            if payload.open('rb').read(2) != b'MZ':
                raise ValueError('Expected a managed PE file: ' + module)
            existing = resources / target
            if existing.exists():
                raise ValueError('Use a reviewed apply-local plan to replace existing modules')
            rows.append({'source': module, 'target': target, 'sha256': sha256(payload), 'previousSha256': None})
        plan = stage / 'plan.json'
        plan.write_text(json.dumps(rows))
        apply_local(root, plan)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('seed-versions')
    commands.add_parser('seed-catalog').add_argument('snapshot', type=Path)
    mods = commands.add_parser('beatmods')
    mods.add_argument('names', nargs='*')
    mods.add_argument('--profile', type=Path)
    mods.add_argument('--install', action='store_true')
    mods.add_argument('--replace-existing', action='store_true',
                      help='Allow MoltenVR to reinstall upstream mods in a configured instance')
    commands.add_parser('apply-local').add_argument('plan', type=Path)
    upstream = commands.add_parser('install-zip')
    upstream.add_argument('archive', type=Path)
    upstream.add_argument('sha256')
    upstream.add_argument('modules', nargs='+')
    args = parser.parse_args(argv)
    if args.command in ('seed-versions', 'seed-catalog'):
        cache = args.root / 'cache/beatmods'
        print(seed_versions(cache))
        if args.command == 'seed-catalog':
            catalog = cache / f'{VERSION}.json'
            if not catalog.exists():
                rows = json.loads(args.snapshot.read_text())
                if not isinstance(rows, list) or not rows:
                    raise ValueError('Empty or invalid catalog snapshot')
                atomic_json(catalog, rows)
            catalog.touch()
            print(catalog)
        return
    if args.command == 'apply-local':
        apply_local(args.root, args.plan)
        return
    if args.command == 'install-zip':
        install_zip(args.root, args.archive, args.sha256, args.modules)
        return
    if args.install and args.root.resolve() != ROOT.resolve():
        raise ValueError('MoltenVR installs into its standard root only')
    if args.install:
        require_closed()
        resources = args.root / 'instances' / VERSION / 'Beat Saber.app/Contents/Resources'
        if not resources.is_dir():
            raise ValueError('Create the native 1.40.8 instance through MoltenVR first')
        if any((resources / 'Plugins').glob('*.dll')) and not args.replace_existing:
            raise ValueError('Use a fresh instance or explicitly allow reinstalling existing mods')
    pins = json.loads(args.profile.read_text()) if args.profile else {}
    names = [*pins, *args.names]
    if not names:
        parser.error('Supply a profile or mod names')
    cache = args.root / 'cache/beatmods'
    catalog = cache / f'{VERSION}.json'
    approved = json.loads(curl(f'{BASE}/api/v1/mod?gameVersion={VERSION}&status=approved'))
    for name, version, archive, download in resolve(names, json.loads(catalog.read_text()), approved,
                                                     {name.casefold(): ver for name, ver in pins.items()}):
        cache_archive(cache / 'zips', archive, download)
        print(f'Verified {name} {version}', flush=True)
    if args.install:
        require_closed()
        seed_versions(cache)
        catalog.touch()
        subprocess.run([str(ENGINE), '-v', 'mods', 'install', VERSION, *names], check=True)


if __name__ == '__main__':
    main()
