# SPDX-License-Identifier: AGPL-3.0-only
import hashlib
import zipfile
import pytest
from bsmac_converter.mod_installer import verify_archive


def test_verify_cached_mod_against_published_manifest(tmp_path):
    path = tmp_path / 'mod.zip'
    member, content = 'Plugins/example.dll', b'example'
    hashes = {'hashMd5': [{'file': member, 'hash': hashlib.md5(content).hexdigest()}]}
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr(member, content)
    verify_archive(path, hashes)
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr(member, b'changed content')
    with pytest.raises(ValueError, match='hash mismatch'):
        verify_archive(path, hashes)
    with zipfile.ZipFile(path, 'a') as archive:
        archive.writestr('../escape', b'x')
    with pytest.raises(ValueError, match='Unsafe mod archive path'):
        verify_archive(path, hashes)
