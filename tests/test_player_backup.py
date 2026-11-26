# SPDX-License-Identifier: AGPL-3.0-only
import json
from pathlib import Path
import zipfile

import pytest

from bsmac_converter.player_backup import create, restore


def test_versioned_snapshot(tmp_path: Path):
    (tmp_path / "BeatSaberVersion.txt").write_text("1.40.8")
    configs = tmp_path / "Beat Saber.app/Contents/Resources/UserData"
    configs.mkdir(parents=True)
    (configs / "mod.json").write_text("{}")
    (configs / "private.json").write_text("excluded")
    player = tmp_path / "player"
    player.mkdir()
    save = player / "PlayerData.dat"
    save.write_text("first")
    output = tmp_path / "out"
    destination = create(tmp_path, player, output, ["mod.json"])
    save.write_text("second")
    assert create(tmp_path, player, output, ["mod.json"]) == destination
    assert sorted(output.iterdir()) == [destination]
    with zipfile.ZipFile(destination) as archive:
        assert archive.read("player/PlayerData.dat") == b"second"
        assert "configs/private.json" not in archive.namelist()
        assert json.loads(archive.read("manifest.json"))["gameVersion"] == "1.40.8"
    saved = destination.read_bytes()
    with pytest.raises(ValueError, match="Invalid selected config"):
        create(tmp_path, player, output, ["../mod.json"])
    assert destination.read_bytes() == saved


def test_config_only_backup_and_restore(tmp_path, monkeypatch):
    monkeypatch.setattr("bsmac_converter.player_backup.require_closed", lambda: None)
    (tmp_path / "BeatSaberVersion.txt").write_text("1.40.8")
    configs = tmp_path / "Beat Saber.app/Contents/Resources/UserData/Mod"
    configs.mkdir(parents=True)
    path = configs / "layout.json"
    path.write_text("saved")
    backup = create(tmp_path, tmp_path / "player", tmp_path / "out",
                    categories=("configs",))
    path.write_text("current")
    assert restore(tmp_path, tmp_path / "player", backup, ("configs",)) == [str(path)]
    assert path.read_text() == "current"
    receipt = restore(tmp_path, tmp_path / "player", backup, ("configs",), apply=True)
    assert path.read_text() == "saved"
    assert json.loads(receipt.read_text())["entries"][0]["oldSha256"]


def test_missing_model_download(tmp_path, monkeypatch):
    from hashlib import sha256
    from bsmac_converter.backup_downloads import restore_models

    resources = tmp_path / "Beat Saber.app/Contents/Resources"
    resources.mkdir(parents=True)
    payload = b"example model"
    digest = sha256(payload).hexdigest()
    sources = tmp_path / "model-sources.json"
    sources.write_text(json.dumps({digest: "https://example.com/example.bloq"}))
    monkeypatch.setattr("bsmac_converter.backup_downloads.require_closed", lambda: None)
    monkeypatch.setattr("bsmac_converter.backup_downloads.curl",
                        lambda url, flag, target: Path(target).write_bytes(payload))
    inventory = {"CustomNotes": [{"path": "example.bloq", "sha256": digest}]}
    assert restore_models(tmp_path, inventory, sources)["restored"] == 1
    assert (resources / "CustomNotes/example.bloq").read_bytes() == payload
    assert restore_models(tmp_path, inventory, sources)["restored"] == 0


def test_verified_map_package(tmp_path, monkeypatch):
    from hashlib import sha256
    from bsmac_converter.backup_downloads import restore_packages

    (tmp_path / "Beat Saber.app/Contents").mkdir(parents=True)
    info = b'{"version": "2.1.0"}'
    map_zip = tmp_path / "source.zip"
    with zipfile.ZipFile(map_zip, "w") as archive:
        archive.writestr("Info.dat", info)
        archive.writestr("Expert.dat", b"test chart")
    digest = sha256(info).hexdigest()
    registry = tmp_path / "package-sources.json"
    registry.write_text(json.dumps({digest: {
        "url": "https://example.com/example.zip", "sha256": sha256(map_zip.read_bytes()).hexdigest(),
    }}))
    inventory = {"maps": [{"path": "Example/Info.dat", "sha256": digest}]}
    monkeypatch.setattr("bsmac_converter.backup_downloads.require_closed", lambda: None)
    monkeypatch.setattr("bsmac_converter.backup_downloads.curl",
                        lambda url, flag, target: Path(target).write_bytes(map_zip.read_bytes()))
    result = restore_packages(tmp_path, inventory, registry, ("maps",))
    assert result["restored"] == ["Example/Info.dat"]
    assert (tmp_path / "Beat Saber.app/Contents/CustomLevels/Example/Expert.dat").read_bytes() == b"test chart"
    assert restore_packages(tmp_path, inventory, registry, ("maps",))["restored"] == []


def test_separate_snapshots_preserve_legacy_and_previous_manual_backup(tmp_path):
    (tmp_path / "BeatSaberVersion.txt").write_text("1.40.8")
    (tmp_path / "Beat Saber.app/Contents/Resources/UserData").mkdir(parents=True)
    player = tmp_path / "player"
    player.mkdir()
    save = player / "PlayerData.dat"
    save.write_text("first")
    out = tmp_path / "out"
    legacy = create(tmp_path, player, out)
    first = create(tmp_path, player, out, snapshot_id="1.40.8-manual-20261126123456789-aaaaaaaa")
    save.write_text("second")
    create(tmp_path, player, out, snapshot_id="1.40.8-manual-20261126123456790-bbbbbbbb")
    with pytest.raises(FileExistsError):
        create(tmp_path, player, out, snapshot_id=first.stem)
    for path in (legacy, first):
        with zipfile.ZipFile(path) as archive:
            assert archive.read("player/PlayerData.dat") == b"first"
