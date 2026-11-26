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


def restore_packages(instance, inventory, sources_file, selected):
    """Restore missing pinned packages while preserving existing installations."""
    import re
    import zipfile

    from .mod_installer import install_zip

    require_closed()
    sources = json.loads(sources_file.read_text()) if sources_file.is_file() else {}
    contents = (instance / "Beat Saber.app/Contents").resolve(strict=True)
    result = {"restored": [], "unresolved": []}
    with tempfile.TemporaryDirectory() as directory:
        stage = Path(directory)
        for category in selected:
            for row in inventory.get(category, []):
                name = row["path"]
                parts = name.split("/")
                digest = row["sha256"]
                if any(not part or part in (".", "..") or chr(92) in part for part in parts):
                    raise ValueError("Unsafe inventory path")
                if category == "mods":
                    if len(parts) != 1 or not re.fullmatch(r"[A-Za-z0-9_.+ -]+\.dll", name):
                        raise ValueError("Invalid mod file name")
                    root = contents / "Resources/Plugins"
                else:
                    if len(parts) != 2 or parts[1].lower() != "info.dat":
                        raise ValueError("Expected a map folder with Info.dat")
                    root = contents / "CustomLevels"
                target = root / name
                if target.resolve() != target:
                    raise ValueError("Package destination must not be a symlink")
                if target.is_file() and sha256(target) == digest:
                    continue
                source = sources.get(digest)
                if source is None or target.exists() or (category == "maps" and target.parent.exists()):
                    result["unresolved"].append(name)
                    continue
                parsed = urlsplit(source["url"])
                if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
                    raise ValueError("Expected an HTTPS package URL without credentials")
                archive_path = stage / "package.zip"
                curl(source["url"], "-o", str(archive_path))
                if sha256(archive_path) != source["sha256"]:
                    raise ValueError(f"Package checksum mismatch: {name}")
                with zipfile.ZipFile(archive_path) as archive:
                    records = archive.infolist()
                    if len(records) > 1000 or sum(item.file_size for item in records) > 256 * 1024**2:
                        raise ValueError("Package exceeds extraction limits")
                    if len({item.filename.casefold() for item in records}) != len(records):
                        raise ValueError("Duplicate package entries")
                    import hashlib
                    if category == "mods":
                        data = archive.read("Plugins/" + name)
                        if hashlib.sha256(data).hexdigest() != digest:
                            raise ValueError("Plugin does not match backed-up binary")
                    else:
                        data = archive.read(parts[1])
                        if hashlib.sha256(data).hexdigest() != digest:
                            raise ValueError("Map Info.dat does not match the backup")
                        plan = []
                        for index, item in enumerate(records):
                            member = item.filename
                            if (Path(member).name != member or chr(92) in member or member in (".", "..")
                                    or item.is_dir() or (item.external_attr >> 28) == 10):
                                raise ValueError("Expected flat map ZIP without links")
                            output = stage / f"map-{index}"
                            output.write_bytes(archive.read(item))
                            plan.append((output, target.parent / member))
                        install(plan, instance / "PlayerBackup-Rollback")
                if category == "mods":
                    if instance.name != "1.40.8" or instance.parent.name != "instances":
                        raise ValueError("Mod restore requires a MoltenVR 1.40.8 instance")
                    install_zip(instance.parent.parent, archive_path, source["sha256"], [name])
                result["restored"].append(name)
    return result
