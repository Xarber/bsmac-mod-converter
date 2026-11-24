# SPDX-License-Identifier: AGPL-3.0-only
import json
from datetime import date
from pathlib import Path
from jsonschema import Draft202012Validator, FormatChecker

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root/'uads.json').read_text())
schema = json.loads((root/'config/uads-schema.json').read_text())
Draft202012Validator.check_schema(schema)
Draft202012Validator(schema, format_checker=FormatChecker()).validate(manifest)
registry = json.loads((root/'config/uads-requirements.json').read_text())['requirements']
ids = [item['id'] for item in registry]
declared = [item['id'] for item in manifest['requirements']]
if len(ids) != len(set(ids)) or len(declared) != len(set(declared)) or set(ids) != set(declared):
    raise ValueError('Missing, unknown or duplicate requirement IDs')
for item in manifest['requirements']:
    if item['state'] == 'Not Applicable' and item['applicability'] != 'Not Applicable':
        raise ValueError('Inconsistent applicability/state')
    for name in item['evidence']:
        path = (root/name).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError('Missing or external evidence: ' + name)
for exception in manifest['exceptions']:
    if exception['requirement'] not in ids:
        raise ValueError('Unknown exception requirement')
    if 'expiresAt' in exception and date.fromisoformat(exception['expiresAt']) < date.today():
        raise ValueError('Expired exception')
print('Manifest/schema/registry/evidence structure passed. Functional verification is separate.')
