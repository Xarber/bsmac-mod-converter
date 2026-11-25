# SPDX-License-Identifier: AGPL-3.0-only
"""Copy registered bundles into a closed Beat Saber application and update the index."""
import argparse
from datetime import datetime, timezone
from pathlib import Path
import shutil
import tempfile

from bsmac_converter.index import derivative_row, publish_rows, read_index, sha256
from bsmac_converter.mod_installer import require_closed, sign

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('app', type=Path)
parser.add_argument('index', type=Path)
args = parser.parse_args()
require_closed()
app, index = args.app.resolve(strict=True), args.index.resolve(strict=True)
resources = (app / 'Contents/Resources').resolve(strict=True)
destination = resources / 'MetalAssets' / index.stem
if not destination.resolve().is_relative_to(resources):
    parser.error('Bundle destination escapes Resources')
rows = read_index(index)
if not rows:
    parser.error('Empty bundle index')
destination.mkdir(parents=True, exist_ok=True)
updates = []
for key, row in rows.items():
    source = Path(row[4])
    if derivative_row(key, source) != row:
        raise ValueError('Existing derivative does not match index: ' + source.name)
    target = destination / (row[3] + '.bundle')
    if target.is_symlink():
        raise ValueError('Unexpected symlink at bundle destination')
    if not target.exists():
        with tempfile.NamedTemporaryFile(dir=destination, delete=False) as temp:
            try:
                with source.open('rb') as stream:
                    shutil.copyfileobj(stream, temp)
                temp.flush()
                if sha256(temp.name) != row[3]:
                    raise ValueError('Copied bundle failed verification')
                Path(temp.name).replace(target)
            finally:
                Path(temp.name).unlink(missing_ok=True)
    checked = derivative_row(key, target)
    if checked[:4] != row[:4]:
        raise ValueError('Stored bundle failed CRC or hash verification')
    updates.append(checked)
require_closed()
sign(app)
if rows != read_index(index):
    raise RuntimeError('Bundle index changed while copying. Retry with the game closed.')
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
backup = index.with_name(index.name + '.before-store-' + stamp)
shutil.copy2(index, backup)
publish_rows(index, updates, replace=True)
print(f'Stored {len(updates)} registrations in {destination}; previous index: {backup}')
