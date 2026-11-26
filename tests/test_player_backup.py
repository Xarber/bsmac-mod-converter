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
