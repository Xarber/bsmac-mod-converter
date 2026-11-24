# SPDX-License-Identifier: AGPL-3.0-only
"""Backed-up, hash-checked replacement of explicitly staged local files."""
import json
import os
import shutil
from datetime import datetime, timezone
from pathlib import Path
from .index import sha256

def install(plan, backup_root):
    entries = []
    for source, target in plan:
        source, target = Path(source).resolve(), Path(target).resolve()
        if not source.is_file() or source == target:
            raise ValueError('Invalid staged source')
        entries.append({'source': str(source), 'target': str(target), 'newSha256': sha256(source),
                        'oldSha256': sha256(target) if target.exists() else None})
    targets = [e['target'] for e in entries]
    if len(targets) != len(set(targets)):
        raise ValueError('Duplicate install target')
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    backup = Path(backup_root) / stamp
    backup.mkdir(parents=True, exist_ok=False)
    for index, entry in enumerate(entries):
        if entry['oldSha256']:
            dest = backup / str(index)
            shutil.copy2(entry['target'], dest)
            if sha256(dest) != entry['oldSha256']:
                raise ValueError('Backup verification failed')
            entry['backup'] = str(dest)
    receipt = backup/'receipt.json'
    record = {'schemaVersion': 1, 'state': 'prepared', 'entries': entries}
    receipt.write_text(json.dumps(record, indent=2))
    applied = []
    try:
        for entry in entries:
            target = Path(entry['target'])
            if (sha256(target) if target.exists() else None) != entry['oldSha256']:
                raise ValueError('Target changed since preparation')
            target.parent.mkdir(parents=True, exist_ok=True)
            temp = target.with_name(target.name + '.bsmac-tmp')
            if temp.exists():
                raise ValueError('Stale install temporary file; inspect it before retrying')
            shutil.copy2(entry['source'], temp)
            if sha256(temp) != entry['newSha256']:
                raise ValueError('Staging hash mismatch')
            os.replace(temp, target)
            applied.append(entry)
        record['state'] = 'installed'
    except Exception:
        for entry in reversed(applied):
            if entry['oldSha256']:
                shutil.copy2(entry['backup'], entry['target'])
            else:
                Path(entry['target']).unlink()
        record['state'] = 'rolled-back'
        receipt.write_text(json.dumps(record, indent=2))
        raise
    receipt.write_text(json.dumps(record, indent=2))
    return receipt

def rollback(receipt):
    receipt = Path(receipt)
    record = json.loads(receipt.read_text())
    if record.get('schemaVersion') != 1 or record.get('state') != 'installed':
        raise ValueError('Receipt is not an installed transaction')
    for entry in record['entries']:
        if not Path(entry['target']).is_file() or sha256(entry['target']) != entry['newSha256']:
            raise ValueError('Installed target changed; refusing destructive rollback')
        if entry['oldSha256'] and sha256(entry['backup']) != entry['oldSha256']:
            raise ValueError('Backup hash mismatch')
    for entry in reversed(record['entries']):
        target = Path(entry['target'])
        if entry['oldSha256']:
            temp = target.with_name(target.name + '.bsmac-rollback')
            shutil.copy2(entry['backup'], temp)
            os.replace(temp, target)
        else:
            target.unlink()
    record['state'] = 'rolled-back'
    receipt.write_text(json.dumps(record, indent=2))
