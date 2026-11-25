# SPDX-License-Identifier: AGPL-3.0-only
import json
import zipfile

import pytest

from bsmac_converter.qosmetics import extract_pc_bundle


def write_package(path, **fields):
    manifest = {'pcFileName': 'windows', 'androidFileName': 'android',
                'descriptor': {'objectName': 'Example', 'author': 'Mapper'},
                'config': {'showArrows': True}, **fields}
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr('package.json', json.dumps(manifest))
        archive.writestr('windows', b'Windows bytes')
        archive.writestr('android', b'Android bytes')


def test_extract_declared_pc_payload(tmp_path):
    source = tmp_path / 'example.cyoob'
    output = tmp_path / 'staged'
    write_package(source)
    assert extract_pc_bundle(source, output) == {'name': 'Example', 'author': 'Mapper',
        'description': '', 'showArrows': True}
    assert output.read_bytes() == b'Windows bytes'


def test_refuse_android_as_pc(tmp_path):
    source = tmp_path / 'example.cyoob'
    output = tmp_path / 'staged'
    write_package(source, pcFileName='android')
    with pytest.raises(ValueError, match='No supported PC bundle'):
        extract_pc_bundle(source, output)
    assert not output.exists()


def test_refuse_archive_traversal(tmp_path):
    source = tmp_path / 'example.cyoob'
    output = tmp_path / 'staged'
    write_package(source)
    with zipfile.ZipFile(source, 'a') as archive:
        archive.writestr('../escape', 'data')
    with pytest.raises(ValueError, match='Unsafe Qosmetics archive member'):
        extract_pc_bundle(source, output)
    assert not output.exists()
