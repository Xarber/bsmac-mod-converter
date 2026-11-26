# SPDX-License-Identifier: AGPL-3.0-only
"""Create a private local Beat Saber backup for one exact game version."""

import argparse
import hashlib
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import tempfile
import time
import zipfile

from .index import sha256
from .mod_installer import require_closed
from .transaction import install

PLAYER_FILES = ("AvatarData.dat", "ControllerProfiles.dat", "PlayerData.dat", "settings.ini")
MODEL_DIRS = ("CustomNotes", "CustomSabers", "CustomWalls", "CustomAvatars")


CATEGORIES = ("player", "configs", "mods", "maps", "models")
CONFIG_TYPES = {".json", ".ini", ".cfg", ".toml", ".xml", ".yaml", ".yml"}


def game_version(instance):
    version = (instance / "BeatSaberVersion.txt").read_text().strip()
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("Expected an exact game version")
    return version


def create(instance, player, output, configs=(), *, categories=CATEGORIES):
    version = game_version(instance)
    contents = instance / "Beat Saber.app/Contents"
    resources = contents / "Resources"
    selected = {"player/" + name: player / name for name in PLAYER_FILES} if "player" in categories else {}
    config_root = (resources / "UserData").resolve()
    config_names = configs or (
        path.relative_to(config_root).as_posix() for path in sorted(config_root.rglob("*"))
        if path.suffix.lower() in CONFIG_TYPES and path.is_file() and not path.is_symlink()
        and path.resolve().is_relative_to(config_root)
    )
    for name in config_names if "configs" in categories else ():
        relative = Path(name)
        path = config_root / relative
        if (relative.is_absolute() or ".." in relative.parts or path.is_symlink()
                or not path.resolve().is_relative_to(config_root)
                or not path.is_file() or path.suffix.lower() not in CONFIG_TYPES):
            raise ValueError(f"Invalid selected config: {name}")
        selected["configs/" + relative.as_posix()] = path

    manifest = {
        "schema": 1, "gameVersion": version, "createdAt": datetime.now(timezone.utc).isoformat(),
        "files": {}, "inventory": {},
    }
    for category, root, pattern in (
        ("mods", resources / "Plugins", "*.dll"),
        ("maps", contents / "CustomLevels", "*/*"),
        *((name, resources / name, "*") for name in MODEL_DIRS),
    ):
        if (category if category in ("mods", "maps") else "models") not in categories:
            continue
        manifest["inventory"][category] = [
            {"path": path.relative_to(root).as_posix(), "sha256": sha256(path)}
            for path in sorted(root.glob(pattern))
            if path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(root.resolve())
            and (category != "maps" or path.name.lower() == "info.dat")
            and not path.name.startswith(".")
        ]

    output.mkdir(mode=0o700, parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output) as scratch:
        staged = Path(scratch) / "backup.zip"
        with zipfile.ZipFile(staged, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, path in sorted(selected.items()):
                if path.is_file() and not path.is_symlink():
                    if path.stat().st_size > 64 * 1024**2:
                        raise ValueError(f"Selected file is too large: {name}")
                    before = path.stat()
                    payload = path.read_bytes()
                    after = path.stat()
                    if (before.st_ino, before.st_size, before.st_mtime_ns) != (after.st_ino, after.st_size, after.st_mtime_ns):
                        raise ValueError(f"Save changed during backup. Retry from the main menu: {name}")
                    manifest["files"][name] = hashlib.sha256(payload).hexdigest()
                    archive.writestr(name, payload)
            if "player" in categories and "player/PlayerData.dat" not in manifest["files"]:
                raise ValueError("PlayerData.dat is missing")
            archive.writestr("manifest.json", json.dumps(manifest, indent=2))
        destination = output / f"{version}.zip"
        os.chmod(staged, 0o600)
        os.replace(staged, destination)
    return destination


def restore(instance, player, backup, categories, *, apply=False):
    """Validate the selected files before writing a rollback-backed transaction."""
    resources = instance / "Beat Saber.app/Contents/Resources"
    roots = {"player": player.resolve(), "configs": (resources / "UserData").resolve()}
    with zipfile.ZipFile(backup) as archive, tempfile.TemporaryDirectory() as directory:
        entries = {entry.filename: entry for entry in archive.infolist()}
        if len(entries) != len(archive.infolist()) or entries["manifest.json"].file_size > 16 * 1024**2:
            raise ValueError("Invalid backup manifest or duplicate members")
        manifest = json.loads(archive.read("manifest.json"))
        if manifest["schema"] != 1 or manifest["gameVersion"] != game_version(instance):
            raise ValueError("Backup does not match this Beat Saber version")
        plan = []
        total = 0
        for name, expected in manifest["files"].items():
            category, _, relative = name.partition("/")
            if category not in categories:
                continue
            root = roots[category]
            path = Path(relative)
            target = root / path
            if (not relative or path.is_absolute() or ".." in path.parts or chr(92) in relative
                    or not target.resolve().is_relative_to(root) or target.is_symlink()
                    or (category == "player" and relative not in PLAYER_FILES)
                    or (category == "configs" and path.suffix.lower() not in CONFIG_TYPES)):
                raise ValueError("Unsafe backup member path")
            size = entries[name].file_size
            total += size
            if size > 64 * 1024**2 or total > 256 * 1024**2:
                raise ValueError("Selected backup files exceed the restore limit")
            data = archive.read(name)
            if hashlib.sha256(data).hexdigest() != expected:
                raise ValueError(f"Backup checksum mismatch: {name}")
            staged = Path(directory) / str(len(plan))
            staged.write_bytes(data)
            plan.append((staged, target))
        if apply:
            require_closed()
            if not plan:
                raise ValueError("The backup contains no selected files")
            return install(plan, instance / "PlayerBackup-Rollback")
        return [str(target) for _, target in plan]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("instance", type=Path)
    parser.add_argument("player_data", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--config", action="append", default=[], help="reviewed path relative to UserData")
    parser.add_argument("--include", action="append", choices=CATEGORIES)
    parser.add_argument("--exclude", action="append", choices=CATEGORIES, default=[])
    parser.add_argument("--restore", action="store_true", help="preview restore from the ZIP at OUTPUT")
    parser.add_argument("--apply", action="store_true", help="apply the selected restore")
    parser.add_argument("--expected-sha256", help=argparse.SUPPRESS)
    parser.add_argument("--live-snapshot", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--after-pid", type=int, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    if args.after_pid is not None:
        if args.after_pid <= 1:
            parser.error("invalid game PID")
        for _ in range(120):
            try:
                os.kill(args.after_pid, 0)
            except ProcessLookupError:
                break
            time.sleep(1)
        else:
            parser.error("timed out waiting for Beat Saber to quit")
    if args.live_snapshot and args.restore:
        parser.error("live snapshots are backup-only")
    if args.apply and not args.restore:
        parser.error("--apply requires --restore")
    default = ("player", "configs") if args.restore else CATEGORIES
    selected = tuple(name for name in (args.include or default) if name not in args.exclude)
    if not selected:
        parser.error("select at least one category")
    if args.restore:
        if args.expected_sha256 and sha256(args.output) != args.expected_sha256:
            raise ValueError("Backup changed since restore was prepared")
        inventory_categories = set(selected).intersection({"models", "mods", "maps"})
        if inventory_categories:
            from .backup_downloads import restore_models, restore_packages
            with zipfile.ZipFile(args.output) as archive:
                if archive.getinfo("manifest.json").file_size > 16 * 1024**2:
                    raise ValueError("Backup manifest is too large")
                manifest = json.loads(archive.read("manifest.json"))
            if manifest["schema"] != 1 or manifest["gameVersion"] != game_version(args.instance):
                raise ValueError("Backup does not match this Beat Saber version")
        fields = tuple(name for name in selected if name in ("player", "configs"))
        if fields:
            result = restore(args.instance, args.player_data, args.output, fields, apply=args.apply)
            print(result if args.apply else json.dumps(result, indent=2))
        if "models" in selected:
            result = restore_models(args.instance, manifest["inventory"],
                                    args.output.parent / "model-sources.json") if args.apply else {
                name: entries for name, entries in manifest["inventory"].items() if name in MODEL_DIRS
            }
            print(json.dumps(result, indent=2))
        packages = tuple(name for name in ("maps", "mods") if name in selected)
        if packages:
            result = restore_packages(args.instance, manifest["inventory"],
                                       args.output.parent / "package-sources.json", packages) if args.apply else {
                name: manifest["inventory"].get(name, []) for name in packages
            }
            print(json.dumps(result, indent=2))
    else:
        if not args.live_snapshot:
            require_closed()
        print(create(args.instance, args.player_data, args.output, args.config, categories=selected))


if __name__ == "__main__":
    main()
