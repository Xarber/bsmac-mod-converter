# SPDX-License-Identifier: AGPL-3.0-only
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
files = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
for name in filter(None, files):
    path = root/name
    if path.suffix.lower() in {'.dll', '.dylib', '.pyc', '.bundle', '.vivify', '.shaderbundle', '.bloq', '.log', '.zip'}:
        raise ValueError('Non-source/private artifact tracked: ' + name)
    text = path.read_text(errors='replace')
    for marker in ['-----BEGIN OPENSSH PRIVATE KEY-----', '-----BEGIN RSA PRIVATE KEY-----']:
        if marker in text and path.name != 'check-source-boundary.py':
            raise ValueError('Private key detected: ' + name)
print('Tracked-source asset and private-key boundary passed; not a complete secret/security audit.')
