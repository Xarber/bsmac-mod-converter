# SPDX-License-Identifier: AGPL-3.0-only
"""Restore missing loose models from locally reviewed, hash-pinned HTTPS sources."""
import json
from pathlib import Path
import tempfile
from urllib.parse import urlsplit

from .index import sha256
from .mod_installer import curl, require_closed
from .transaction import install

MODEL_EXTENSIONS = {
    "CustomNotes": {".bloq", ".cyoob"},
    "CustomSabers": {".saber"},
    "CustomWalls": {".pixie"},
    "CustomAvatars": {".avatar"},
}


def restore_models(instance, inventory, sources_file):
    require_closed()
    sources = json.loads(sources_file.read_text()) if sources_file.is_file() else {}
    resources = (instance / "Beat Saber.app/Contents/Resources").resolve(strict=True)
    missing = []
    with tempfile.TemporaryDirectory() as directory:
        stage = Path(directory)
        plan = []
        for category, extensions in MODEL_EXTENSIONS.items():
            for row in inventory.get(category, []):
                name = row["path"]
                if (Path(name).name != name or chr(92) in name
                        or Path(name).suffix.lower() not in extensions):
                    continue
                target = resources / category / name
                if target.resolve() != target:
                    raise ValueError("Model destination must not be a symlink")
                if target.is_file() and sha256(target) == row["sha256"]:
                    continue
                url = sources.get(row["sha256"])
                if url is None or target.exists():
                    missing.append(f"{category}/{name}")
                    continue
                parsed = urlsplit(url)
                if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
                    raise ValueError("Expected an HTTPS model URL without credentials")
                payload = stage / str(len(plan))
                curl(url, "-o", str(payload))
                if sha256(payload) != row["sha256"]:
                    raise ValueError(f"Model download checksum mismatch: {name}")
                plan.append((payload, target))
        receipt = install(plan, instance / "PlayerBackup-Rollback") if plan else None
    return {"restored": len(plan), "unresolved": missing, "receipt": str(receipt) if receipt else None}
