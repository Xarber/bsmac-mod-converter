# SPDX-License-Identifier: AGPL-3.0-only
import argparse
import json
import sys
import time
import zipfile
from pathlib import Path

def parser():
    p = argparse.ArgumentParser(description='Validated local Metal asset conversion. Originals remain untouched.')
    p.add_argument('--backend', type=Path, default=Path.home()/'Library/Application Support/bsmac/mod-converter')
    p.add_argument('--cache', type=Path, default=Path.home()/'Library/Application Support/bsmac/mod-converter/cache/pipeline')
    sub = p.add_subparsers(dest='command', required=True)
    c = sub.add_parser('convert'); c.add_argument('assets', nargs='+', type=Path)
    c = sub.add_parser('scan'); c.add_argument('roots', nargs='+', type=Path)
    c = sub.add_parser('watch'); c.add_argument('roots', nargs='+', type=Path); c.add_argument('--interval', type=float, default=30)
    c = sub.add_parser('index'); c.add_argument('maps', type=Path); c.add_argument('converted', type=Path); c.add_argument('output', type=Path)
    c = sub.add_parser('rollback'); c.add_argument('receipt', type=Path)
    c = sub.add_parser('crc'); c.add_argument('asset', type=Path)
    return p

def discover(roots):
    supported = {'.vivify', '.bloq', '.saber', '.pixie', '.shaderbundle'}
    for root in roots:
        for path in sorted(root.rglob('*')):
            if path.is_file() and path.suffix.lower() in supported:
                yield path

def convert_asset(source, args):
    from .pipeline import convert, repack_shader_library
    from .index import sha256
    if source.suffix.lower() == '.shaderbundle':
        key = sha256(source)
        extracted = args.cache/'libraries'/key/'shaders.asset'
        extracted.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(source) as archive:
            if archive.namelist().count('shaders.asset') != 1 or archive.testzip():
                raise ValueError('Invalid shader library')
            extracted.write_bytes(archive.read('shaders.asset'))
        result = convert(extracted, args.backend, args.cache)
        return repack_shader_library(source, result, extracted.parent/source.name)
    result = convert(source, args.backend, args.cache)
    if source.name == 'bundleWindows2021.vivify':
        import shutil
        from .index import build_index
        destination = args.cache/'map-derivatives'/(sha256(source)[:16]+'.vivify')
        destination.parent.mkdir(parents=True, exist_ok=True)
        temp = destination.with_suffix('.tmp')
        shutil.copyfile(result, temp)
        temp.replace(destination)
        # Revalidate metadata and both CRCs before publishing an allowlist entry.
        # Rebuild from the map root, preserving all currently available records.
        build_index(source.parent.parent, destination.parent, args.cache.parent/'MetalMapIndex.tsv')
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

if __name__ == '__main__':
    raise SystemExit(main())
