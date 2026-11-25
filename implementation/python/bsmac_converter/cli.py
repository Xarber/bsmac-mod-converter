# SPDX-License-Identifier: AGPL-3.0-only
import argparse
import json
import os
import sys
import time
import zipfile
from pathlib import Path

def parser():
    p = argparse.ArgumentParser(description='Validated local Metal asset conversion. Originals remain untouched.')
    p.add_argument('--backend', type=Path, default=Path.home()/'Library/Application Support/bsmac/mod-converter')
    p.add_argument('--cache', type=Path, default=Path.home()/'Library/Application Support/bsmac/mod-converter/cache/pipeline')
    sub = p.add_subparsers(dest='command', required=True)
    c = sub.add_parser('convert'); c.add_argument('assets', nargs='+', type=Path); c.add_argument('--experimental', action='store_true')
    c = sub.add_parser('scan'); c.add_argument('roots', nargs='+', type=Path)
    c = sub.add_parser('watch'); c.add_argument('roots', nargs='+', type=Path); c.add_argument('--interval', type=float, default=30)
    c = sub.add_parser('index'); c.add_argument('maps', type=Path); c.add_argument('converted', type=Path); c.add_argument('output', type=Path)
    c = sub.add_parser('rollback'); c.add_argument('receipt', type=Path)
    c = sub.add_parser('crc'); c.add_argument('asset', type=Path)
    c = sub.add_parser('audit-shaders'); c.add_argument('original', type=Path); c.add_argument('derivative', type=Path)
    c = sub.add_parser('prepare-map')
    c.add_argument('asset', nargs='?', type=Path)
    c.add_argument('--request-stdin', action='store_true')
    default_maps = Path.home() / 'Library/Application Support/bsmac/instances/1.40.8/Beat Saber.app/Contents/CustomLevels'
    for command in (c, sub.add_parser('rescan-maps')):
        command.add_argument('--maps', type=Path, default=default_maps)
        command.add_argument('--index-path', type=Path)
    c = sub.add_parser('prune-diagnostics'); c.add_argument('--older-than-days', type=int, default=14)
    c = sub.add_parser('automatic')
    c.add_argument('mode', choices=('status', 'enable', 'disable'))
    c = sub.add_parser('prepare-equipment')
    c.add_argument('asset', nargs='?', type=Path)
    c.add_argument('--request-stdin', action='store_true')
    for command in (c, sub.add_parser('rescan-equipment')):
        command.add_argument('--resources', type=Path, default=default_maps.parent / 'Resources')
    return p

def discover(roots):
    supported = {'.vivify', '.bloq', '.saber', '.pixie', '.shaderbundle'}
    for root in roots:
        for path in sorted(root.rglob('*')):
            if path.is_file() and path.suffix.lower() in supported:
                yield path

def convert_asset(source, args):
    from .pipeline import convert, repack_shader_library
    from .index import derivative_row, publish_rows, sha256, validate_original
    experimental = getattr(args, 'experimental', False)
    if source.suffix.lower() == '.shaderbundle':
        key = sha256(source)
        extracted = args.cache/'libraries'/key/'shaders.asset'
        extracted.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(source) as archive:
            if archive.namelist().count('shaders.asset') != 1 or archive.testzip():
                raise ValueError('Invalid shader library')
            extracted.write_bytes(archive.read('shaders.asset'))
        result = convert(extracted, args.backend, args.cache, experimental=experimental)
        return repack_shader_library(source, result, extracted.parent/source.name)
    is_map = source.name == 'bundleWindows2021.vivify'
    original = validate_original(source) if is_map else None
    result = convert(source, args.backend, args.cache, experimental=experimental)
    if is_map and not experimental:
        if validate_original(source) != original:
            raise ValueError('Map changed while preparing derivative')
        publish_rows(args.cache.parent/'MetalMapIndex.tsv', [derivative_row(original, result)])
    return result

def scan(args):
    failures = 0
    for asset in discover(args.roots):
        try:
            print(json.dumps({'asset': str(asset), 'converted': str(convert_asset(asset, args)), 'installed': False}), flush=True)
        except Exception as error:
            failures += 1
            print(json.dumps({'asset': str(asset), 'error': str(error)}), file=sys.stderr, flush=True)
    return int(failures > 0)

def main(argv=None):
    args = parser().parse_args(argv)
    sys.path.insert(0, str(args.backend.resolve()))
    installed_compiler = args.backend.resolve() / 'automatic/compile-metal'
    if installed_compiler.is_file():
        os.environ.setdefault('BSMAC_METAL_COMPILER', str(installed_compiler))
    if args.command in ('prepare-map', 'rescan-maps', 'prune-diagnostics'):
        return engine_command(args)
    if args.command in ('prepare-equipment', 'rescan-equipment'):
        return equipment_command(args)
    if args.command == 'automatic':
        return automatic_settings(args)
    if args.command == 'audit-shaders':
        from .audit import audit_shaders
        print(json.dumps(list(audit_shaders(args.original, args.derivative)), indent=2)); return 0
    if args.command == 'crc':
        from .crc import bundle_crc
        print(bundle_crc(args.asset)); return 0
    if args.command == 'index':
        from .index import build_index
        print(json.dumps(build_index(args.maps, args.converted, args.output))); return 0
    if args.command == 'rollback':
        from .transaction import rollback
        rollback(args.receipt); return 0
    if args.command == 'convert':
        for asset in args.assets:
            print(convert_asset(asset, args))
        return 0
    if args.command == 'scan':
        return scan(args)
    if args.interval < 5:
        raise ValueError('Watch interval must be at least five seconds')
    while True:
        scan(args)
        time.sleep(args.interval)


def engine_command(args):
    from .engine import MAP_BUNDLE, prepare_map, prune_diagnostics
    if args.command == 'prune-diagnostics':
        print(json.dumps({'removedDiagnosticDirectories': prune_diagnostics(args.cache, args.older_than_days)}))
        return 0
    index = args.index_path or Path(os.environ.get('BSMAC_METAL_INDEX') or args.cache.parent / 'MetalMapIndex.tsv')
    if args.command == 'prepare-map':
        try:
            crc = None
            path = args.asset
            if args.request_stdin:
                if path is not None:
                    raise ValueError('Choose a path or a request on stdin')
                request = sys.stdin.read(16385)
                if len(request) > 16384:
                    raise ValueError('Preparation request is too large')
                source, checksum = request.splitlines()
                path = Path(source)
                crc = int(checksum) or None
            if path is None:
                raise ValueError('Supply a map bundle to prepare')
            print(json.dumps(prepare_map(path, args.backend, args.cache, index, args.maps, crc=crc)))
            return 0
        except Exception as error:
            print(str(error).replace('\n', ' ')[:1000], file=sys.stderr, flush=True)
            return 1
    failures = 0
    for source in sorted(args.maps.rglob(MAP_BUNDLE)):
        try:
            result = prepare_map(source, args.backend, args.cache, index, args.maps)
            print(json.dumps({'asset': str(source), **result}), flush=True)
        except Exception as error:
            failures += 1
            print(json.dumps({'asset': str(source), 'error': str(error)}), file=sys.stderr, flush=True)
    return int(failures > 0)


def automatic_settings(args):
    runtime = args.backend / 'automatic'
    marker = runtime / 'enabled'
    if args.mode == 'enable':
        if not all(path.is_file() for path in (runtime / 'venv/bin/python', runtime / 'compile-metal')):
            raise ValueError('The persistent conversion worker is not installed')
        (runtime / '.enable-pending').touch()
        (runtime / '.enable-pending').replace(marker)
    elif args.mode == 'disable':
        marker.unlink(missing_ok=True)
    print(json.dumps({'automaticPreparation': 'enabled' if marker.is_file() else 'disabled'}))
    return 0


def equipment_command(args):
    from .engine import EQUIPMENT_EXTENSIONS, prepare_equipment
    from .qosmetics import prepare_cyoob
    index = Path(os.environ.get('BSMAC_EQUIPMENT_INDEX') or args.cache.parent / 'EquipmentIndex.tsv')
    def prepare(source):
        if source.suffix.lower() == '.cyoob':
            return str(prepare_cyoob(source, args.backend, args.cache, args.resources))
        row = prepare_equipment(source, args.backend, args.cache, index, args.resources)
        return row[4] if row else None
    if args.command == 'rescan-equipment':
        failed = False
        for name, extensions in EQUIPMENT_EXTENSIONS.items():
            if name == 'CustomNotes':
                extensions = extensions | {'.cyoob'}
            for source in sorted((args.resources / name).rglob('*')):
                if not source.is_file() or source.suffix.lower() not in extensions:
                    continue
                try:
                    print(json.dumps({'asset': str(source), 'derivative': prepare(source)}))
                except Exception as error:
                    failed = True
                    print(json.dumps({'asset': str(source), 'error': str(error)}), file=sys.stderr)
        return int(failed)
    try:
        source = args.asset
        if args.request_stdin:
            if source is not None:
                raise ValueError('Choose a path or a request on stdin')
            request = sys.stdin.read(16385)
            if len(request) > 16384 or len(request.splitlines()) != 1:
                raise ValueError('Invalid equipment preparation request')
            source = Path(request.rstrip('\n'))
        if source is None:
            raise ValueError('Supply a loose equipment file')
        derivative = prepare(source)
        if derivative is not None:
            print(derivative)
        return 0
    except Exception as error:
        print(str(error).replace('\n', ' ')[:1000], file=sys.stderr, flush=True)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
