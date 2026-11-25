# SPDX-License-Identifier: AGPL-3.0-only
import json
import lzma
import struct
import zlib
import zipfile
from pathlib import Path
import pytest
from bsmac_converter.crc import bundle_crc, decompress
from bsmac_converter.index import build_index, expected_crcs, sha256
from bsmac_converter.transaction import install, rollback
from bsmac_converter.pipeline import cache_key, repack_shader_library
from bsmac_converter.cli import discover, parser
from bsmac_converter.msl import repair_unsigned_offsets

def bundle(path, payload, *, stored=None, mode=0):
    stored = payload if stored is None else stored
    info = bytes(16) + struct.pack('>I', 1) + struct.pack('>IIH', len(payload), len(stored), mode) + struct.pack('>I', 0)
    prefix = b'UnityFS\0' + struct.pack('>I', 6) + b'5.x.x\0' + b'2021.3\0'
    total = len(prefix) + 20 + len(info) + len(stored)
    path.write_bytes(prefix + struct.pack('>QIII', total, len(info), len(info), 0x40) + info + stored)
    return zlib.crc32(payload)

def test_crc_lzma_uses_uncompressed_size_from_unityfs(tmp_path):
    payload = b'example UnityFS serialized data' * 128
    dictionary = 2**20
    filters = [{'id': lzma.FILTER_LZMA1, 'dict_size': dictionary, 'lc': 3, 'lp': 0, 'pb': 2}]
    encoded = lzma.compress(payload, format=lzma.FORMAT_RAW, filters=filters)
    props = bytes([93]) + struct.pack('<I', dictionary)
    source = tmp_path / 'lzma.bundle'
    expected = bundle(source, payload, stored=props + encoded[:-5], mode=1)
    assert bundle_crc(source) == expected
    with pytest.raises(ValueError, match='declared size'):
        decompress(props + encoded, len(payload) + 1, 1)

@pytest.mark.parametrize('mode', [2, 3])
def test_crc_lz4_formats(tmp_path, mode):
    from UnityPy.helpers.CompressionHelper import compress_lz4
    payload = b'serialized data' * 100
    path = tmp_path / 'lz4.bundle'
    expected = bundle(path, payload, stored=compress_lz4(payload), mode=mode)
    assert bundle_crc(path) == expected

def test_repair_only_known_unsigned_offsets():
    shader = 'read(u_xlatu13.wy + ivec2(1, 0)); read(signed.xy + ivec2(1, 0)); u_xlatu13.wy + ivec2(-1, 0);'
    assert repair_unsigned_offsets(shader) == shader.replace('ivec2(1, 0)', 'uint2(1, 0)', 1)

def test_crc_validates_payload_and_total(tmp_path):
    p = tmp_path/'a'
    expected = bundle(p, b'original')
    assert bundle_crc(p) == expected
    p.write_bytes(p.read_bytes()+b'corrupt')
    with pytest.raises(AssertionError): bundle_crc(p)

def test_crc_rejects_bad_format(tmp_path):
    p=tmp_path/'bad'; p.write_bytes(b'invalid')
    with pytest.raises(AssertionError): bundle_crc(p)

def test_index_preserves_maps_and_old_index_on_mismatch(tmp_path):
    maps=tmp_path/'maps'; maps.mkdir()
    converted=tmp_path/'converted'; converted.mkdir()
    source=maps/'bundleWindows2021.vivify'
    crc=bundle(source,b'original')
    info=maps/'Info.dat'; info.write_text(json.dumps({'_customData': {'_assetBundle': {'_windows2021':crc}}}))
    info_before=info.read_bytes(); source_before=source.read_bytes()
    derivative=converted/(sha256(source)[:16]+'.vivify'); new=bundle(derivative,b'metal')
    out=tmp_path/'index.tsv'
    rows=build_index(maps,converted,out)
    assert len(rows)==1 and rows[0][1:3]==(crc,new) and rows[0][3]==sha256(derivative)
    assert info.read_bytes()==info_before and source.read_bytes()==source_before
    previous=out.read_bytes()
    info.write_text('{"_assetBundle":{"_windows2021":1}}')
    with pytest.raises(ValueError,match='Original bundle CRC'): build_index(maps,converted,out)
    assert out.read_bytes()==previous

@pytest.mark.parametrize('value',[0,-1,2**32,True,'123'])
def test_rejects_invalid_metadata_crc(value):
    with pytest.raises(ValueError): list(expected_crcs({'_assetBundle':{'_windows2021':value}}))

def test_recursive_metadata():
    assert list(expected_crcs([{'_assetBundle':{'_windows2021':42}}]))==[42]

def test_no_index_without_derivatives(tmp_path):
    with pytest.raises(ValueError,match='No validated'): build_index(tmp_path,tmp_path,tmp_path/'out')

def test_index_rebuild_preserves_other_derivative_directories(tmp_path):
    maps = tmp_path / 'maps'
    output = tmp_path / 'index.tsv'
    for idx in range(2):
        folder = maps / str(idx)
        folder.mkdir(parents=True)
        original = folder / 'bundleWindows2021.vivify'
        crc = bundle(original, bytes([idx]))
        (folder / 'Info.dat').write_text(json.dumps({'_assetBundle': {'_windows2021': crc}}))
        converted = tmp_path / f'converted-{idx}'
        converted.mkdir()
        derivative = converted / (sha256(original)[:16] + '.vivify')
        bundle(derivative, bytes([idx, 255]))
        assert len(build_index(maps, converted, output)) == idx + 1
    assert 'converted-0' in output.read_text()
    assert 'converted-1' in output.read_text()

def test_install_and_verified_rollback(tmp_path):
    source=tmp_path/'staged'; source.write_bytes(b'new')
    target=tmp_path/'target'; target.write_bytes(b'old')
    new=tmp_path/'new'
    receipt=install([(source,target),(source,new)],tmp_path/'backups')
    assert target.read_bytes()==b'new' and new.exists()
    rollback(receipt)
    assert target.read_bytes()==b'old' and not new.exists()
    with pytest.raises(ValueError): rollback(receipt)

def test_rollback_refuses_user_changes(tmp_path):
    source=tmp_path/'s'; source.write_bytes(b'new')
    target=tmp_path/'t'; target.write_bytes(b'old')
    receipt=install([(source,target)],tmp_path/'b')
    target.write_bytes(b'user edit')
    with pytest.raises(ValueError,match='target changed'): rollback(receipt)
    assert target.read_bytes()==b'user edit'

def test_rejects_duplicate_targets_before_backup(tmp_path):
    source=tmp_path/'s'; source.write_bytes(b'new')
    with pytest.raises(ValueError,match='Duplicate'): install([(source,tmp_path/'t')]*2,tmp_path/'b')
    assert not (tmp_path/'b').exists()

def test_shader_manifest_is_preserved(tmp_path):
    source=tmp_path/'s.shaderbundle'; metal=tmp_path/'metal'; metal.write_bytes(b'new')
    with zipfile.ZipFile(source,'w') as z:
        z.writestr('manifest.json',b'{"Platforms":[0,1,2]}'); z.writestr('shaders.asset',b'old'); z.writestr('other',b'preserved')
    output=repack_shader_library(source,metal,tmp_path/'out.shaderbundle')
    with zipfile.ZipFile(source) as a, zipfile.ZipFile(output) as b:
        assert a.read('manifest.json')==b.read('manifest.json') and b.read('other')==b'preserved' and b.read('shaders.asset')==b'new'
    with pytest.raises(ValueError): repack_shader_library(source,metal,source)

def test_invalid_library_rejected(tmp_path):
    p=tmp_path/'bad'
    with zipfile.ZipFile(p,'w') as z: z.writestr('manifest.json','{}')
    with pytest.raises(ValueError): repack_shader_library(p,p,tmp_path/'out')

def test_cache_key_tracks_source_backend_and_extensions(tmp_path):
    source=tmp_path/'s'; source.write_bytes(b'a')
    backend=tmp_path/'backend'; (backend/'bsmac/shaders').mkdir(parents=True)
    code=backend/'bsmac/shaders/convert.pyc'; code.write_bytes(b'local backend')
    a=cache_key(source,backend)
    source.write_bytes(b'b'); assert cache_key(source,backend)!=a
    b=cache_key(source,backend); code.write_bytes(b'update'); assert cache_key(source,backend)!=b
    with pytest.raises(ValueError): cache_key(source,tmp_path/'missing')

def test_discovery_excludes_dlls_and_parser(tmp_path):
    for name in ['a.vivify','b.dll','c.shaderbundle']: (tmp_path/name).write_bytes(b'')
    assert {p.name for p in discover([tmp_path])}=={'a.vivify','c.shaderbundle'}
    assert parser().parse_args(['scan',str(tmp_path)]).command=='scan'


def engine_map(levels, folder, data):
    original = levels / folder / 'bundleWindows2021.vivify'
    original.parent.mkdir(parents=True)
    crc = bundle(original, data)
    (original.parent / 'Info.dat').write_text(json.dumps({'_assetBundle': {'_windows2021': crc}}))
    return original, crc


def test_prepare_map_preserves_existing_registration(tmp_path, monkeypatch):
    from bsmac_converter import engine
    from bsmac_converter.index import derivative_row, publish_rows
    levels = tmp_path / 'CustomLevels'
    original, crc = engine_map(levels, 'Song + title', b'original')
    derivative = tmp_path / 'maps' / 'existing'
    derivative.parent.mkdir()
    bundle(derivative, b'metal output')
    index = tmp_path / 'MetalMapIndex.tsv'
    publish_rows(index, [derivative_row((sha256(original), crc), derivative)])
    previous = index.read_bytes()
    monkeypatch.setattr(engine, 'convert', lambda *args, **kwargs: pytest.fail('attempted to replace existing derivative'))
    result = engine.prepare_map(original, tmp_path, tmp_path / 'pipeline', index, levels, crc=crc)
    assert result == {'status': 'registered', 'derivative': str(derivative.resolve())}
    assert index.read_bytes() == previous
    with pytest.raises(ValueError, match='download checksum'):
        engine.prepare_map(original, tmp_path, tmp_path / 'pipeline', index, levels, crc=crc ^ 1)
    derivative.write_bytes(b'changed')
    with pytest.raises(AssertionError):
        engine.prepare_map(original, tmp_path, tmp_path / 'pipeline', index, levels, crc=crc)
    assert index.read_bytes() == previous


def test_prepare_map_selectively_publishes_new_valid_output(tmp_path, monkeypatch):
    from bsmac_converter import engine
    from bsmac_converter.index import read_index
    levels = tmp_path / 'CustomLevels'
    original, crc = engine_map(levels, 'checked', b'source bytes')
    derivative = tmp_path / 'metal'
    new_crc = bundle(derivative, b'compiled derivative')
    index = tmp_path / 'MetalMapIndex.tsv'
    monkeypatch.setattr(engine, 'needs_conversion', lambda path: True)
    monkeypatch.setattr(engine, 'cache_key', lambda *args: 'a' * 64)
    monkeypatch.setattr(engine, 'convert', lambda *args, **kwargs: derivative)
    result = engine.prepare_map(original, tmp_path, tmp_path / 'pipeline', index, levels)
    row = read_index(index)[sha256(original), crc]
    assert row[2] == new_crc and row[3] == sha256(derivative)
    assert result == {'status': 'converted', 'derivative': str(derivative.resolve())}
    outside, _ = engine_map(tmp_path, 'outside', b'foreign bytes')
    with pytest.raises(ValueError, match='CustomLevels'):
        engine.prepare_map(outside, tmp_path, tmp_path / 'pipeline', index, levels)


def test_prepare_map_does_not_publish_when_source_changes(tmp_path, monkeypatch):
    from bsmac_converter import engine
    levels = tmp_path / 'CustomLevels'
    original, _ = engine_map(levels, 'changed', b'before')
    derivative = tmp_path / 'metal'
    bundle(derivative, b'compiled')
    monkeypatch.setattr(engine, 'needs_conversion', lambda path: True)
    monkeypatch.setattr(engine, 'cache_key', lambda *args: 'b' * 64)

    def change_source(*args, **kwargs):
        bundle(original, b'after')
        return derivative

    monkeypatch.setattr(engine, 'convert', change_source)
    index = tmp_path / 'MetalMapIndex.tsv'
    with pytest.raises(ValueError, match='changed while preparing'):
        engine.prepare_map(original, tmp_path, tmp_path / 'pipeline', index, levels)
    assert not index.exists()
