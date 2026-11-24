# SPDX-License-Identifier: AGPL-3.0-only
"""Original legacy format extensions. Requires the separately installed MoltenVR backend."""
import re, copy, struct, subprocess, shutil
from pathlib import Path
import UnityPy
from UnityPy.helpers import CompressionHelper
from bsmac.shaders import ushader, pblob, convert, translate as T, metalgen
ROOT = Path("cache")
DEST = ROOT / "Converted"


def entries_2018(raw):
    count = struct.unpack_from("<I", raw)[0]
    result = []

    for i in range(count):
        off, size = struct.unpack_from(
            "<II", raw, 4 + i * 8
        )

        assert off >= 4 + count * 8
        assert off + size <= len(raw)

        result.append(raw[off:off+size])

    return result

def parse_legacy(sp):
    assert sp.ver in (201708220, 201802150)

    source_map, channels, tail = ushader.bind_channels(
        sp.tail
    )

    # The legacy parameter block lacks its own version.
    # Add a temporary version header for the modern parser.
    raw = struct.pack("<i", sp.ver) + tail

    params = pblob.parse(raw)

    decoded = []

    for name, slot, packed, flags in params["textures"]:
        dimension = packed & 0xff
        sampler = (packed >> 8) & 0xff

        if sampler == 255:
            sampler = -1

        decoded.append(
            (name, slot, sampler, dimension)
        )

    params["textures"] = decoded

    return params

def translate_legacy(sp, params):
    dxbc = sp.dxbc()

    assert dxbc[:4] == b"DXBC"

    msl, reflection = T.translate(
        sp, params, {}, []
    )

    vs = sp.type in ushader.D3D_VS

    # Try the existing layout validator first.
    try:
        convert.check_layout(msl, params, vs)

    except ValueError as error:
        print("Initial layout mismatch:", error)

        # Apply the packed-vector correction we discovered
        # while converting ReeSabers.
        for name, size, members, structs in params["cbs"]:
            if name == "$Globals":
                name = "VGlobals" if vs else "FGlobals"

            pattern = (
                r"(\bstruct\s+" + re.escape(name)
                + r"_Type\s*\{)(.*?)(\}\s*;)"
            )

            def pack_vectors(match):
                body = re.sub(
                    r"\b(float|int|uint)3(?=\s+\w+\s*;)",
                    r"packed_\g<1>3",
                    match.group(2)
                )

                return (
                    match.group(1)
                    + body
                    + match.group(3)
                )

            msl = re.sub(
                pattern, pack_vectors, msl, flags=re.S
            )

        convert.check_layout(msl, params, vs)

    return msl

def build_table(entries):
    header = bytearray(
        struct.pack("<I", len(entries))
    )

    offset = 4 + len(entries) * 8

    for entry in entries:
        header.extend(
            struct.pack("<II", offset, len(entry))
        )
        offset += len(entry)

    result = bytes(header) + b"".join(entries)

    assert entries_2018(result) == entries

    return result

def encoded_string(name):
    raw = name.encode()

    return (
        struct.pack("<I", len(raw))
        + raw
        + bytes((-len(raw)) % 4)
    )

def replace_record(data, name, old, new):
    prefix = encoded_string(name)

    before = prefix + struct.pack(
        "<" + "i" * len(old), *old
    )

    after = prefix + struct.pack(
        "<" + "i" * len(new), *new
    )

    if data.count(before) != 1:
        raise RuntimeError(
            f"Ambiguous resource record: {name}"
        )

    return data.replace(before, after, 1)

def resource_slots(msl, kind):
    pattern = (
        r"(\w+)\s*\[\[\s*" + kind
        + r"\s*\(\s*(\d+)\s*\)\s*\]\]"
    )

    return {
        name: int(slot)
        for name, slot in re.findall(pattern, msl)
    }

def convert_entry(entry, msl):
    sp = ushader.SubProgram(entry)

    assert sp.ver in (201708220, 201802150)

    vs = sp.type in ushader.D3D_VS

    assert vs or sp.type in ushader.D3D_PS

    _, _, old_tail = ushader.bind_channels(sp.tail)

    raw = struct.pack("<i", sp.ver) + old_tail
    params = pblob.parse(raw)

    buffers = resource_slots(msl, "buffer")
    textures = resource_slots(msl, "texture")
    samplers = resource_slots(msl, "sampler")

    print("  Metal buffers:", buffers)
    print("  Metal textures:", textures)
    print("  Metal samplers:", samplers)

    assert len(buffers) == len(set(buffers.values()))

    cb_mapping = {}
    tex_mapping = {}
    sampler_mapping = {}

    for name, kind, old_slot, flags in params["binds"]:
        mname = name

        if name == "$Globals":
            mname = "VGlobals" if vs else "FGlobals"

        if mname not in buffers:
            raise RuntimeError(
                f"Missing constant buffer: {mname}"
            )

        new_slot = buffers[mname]

        raw = replace_record(
            raw, name,
            (kind, old_slot, flags),
            (kind, new_slot, flags)
        )

        cb_mapping[old_slot] = new_slot

    for name, old_slot, packed, flags in params["textures"]:
        dimension = packed & 255
        old_sampler = (packed >> 8) & 255

        if name not in textures:
            raise RuntimeError(
                f"Missing texture: {name}"
            )

        new_slot = textures[name]
        new_sampler = old_sampler

        if old_sampler != 255:
            candidates = [
                slot for sampler_name, slot in samplers.items()
                if sampler_name in (
                    "sampler" + name,
                    name + "_sampler",
                    "sampler_" + name.lstrip("_")
                )
            ]

            if len(set(candidates)) == 1:
                new_sampler = candidates[0]

            elif old_sampler in samplers.values():
                new_sampler = old_sampler

            else:
                raise RuntimeError(
                    f"Cannot resolve sampler for {name}"
                )

            sampler_mapping[old_slot] = new_sampler

        new_packed = dimension | (new_sampler << 8)

        raw = replace_record(
            raw, name,
            (0, old_slot, packed, flags),
            (0, new_slot, new_packed, flags)
        )

        tex_mapping[old_slot] = new_slot

    # Verify the modified embedded parameter block.
    pblob.parse(raw)

    msp = metalgen.metal_subprogram(sp, msl)

    source_map, channels, _ = ushader.bind_channels(
        msp.tail
    )

    channel_length = 8 + len(channels) * 8

    # Keep the original legacy format: parameters follow
    # the channel table without a separate version header.
    msp.tail = msp.tail[:channel_length] + raw[4:]

    built = msp.build()

    restored = ushader.SubProgram(built)

    assert restored.ver == sp.ver
    assert restored.keywords == sp.keywords
    assert restored.type == (
        ushader.METAL_VS if vs else ushader.METAL_FS
    )

    _, _, restored_tail = ushader.bind_channels(
        restored.tail
    )

    assert restored_tail == raw[4:]

    return built, {
        "type": restored.type,
        "source_map": source_map,
        "channels": channels,
        "cbs": cb_mapping,
        "textures": tex_mapping,
        "samplers": sampler_mapping
    }

def keyword_end(data, pos):
    count = struct.unpack_from("<I", data, pos)[0]
    pos += 4

    assert count < 4096

    for _ in range(count):
        size = struct.unpack_from("<I", data, pos)[0]
        pos += 4
        assert pos + size <= len(data)
        pos += (size + 3) & ~3

    return pos

def parse_program(entry):
    version = struct.unpack_from("<I", entry)[0]

    if version in (201708220, 201802150):
        return ushader.SubProgram(entry), b""

    if version == 201806140:
        # Global keywords are followed by local keywords.
        start = keyword_end(entry, 24)
        end = keyword_end(entry, start)

        local_keywords = entry[start:end]

        normalized = entry[:start] + entry[end:]
        sp = ushader.SubProgram(normalized)

        assert sp.dxbc()[:4] == b"DXBC" or (
            sp.type in (ushader.METAL_VS, ushader.METAL_FS)
        )

        return sp, local_keywords

    raise ValueError(f"Unsupported version: {version}")

def read_platform(d, platform):
    i = d["platforms"].index(platform)

    off = d["offsets"][i]
    clen = d["compressedLengths"][i]
    dlen = d["decompressedLengths"][i]

    blob = bytes(d["compressedBlob"])

    if isinstance(off, int):
        raw = CompressionHelper.decompress_lz4(
            blob[off:off+clen], dlen
        )

        return entries_2018(raw), [raw], False

    segments = [
        CompressionHelper.decompress_lz4(
            blob[o:o+c], n
        )
        for o, c, n in zip(off, clen, dlen)
    ]

    return ushader.entries(segments), segments, True

def convert_2019(sp, local_keywords, msl, raw):
    params = pblob.parse(raw)

    vs = sp.type in ushader.D3D_VS

    # Reuse MoltenVR's existing sampler remapping.
    msl, updated = convert.samplers_follow_textures(
        msl, params, raw
    )

    buffers = resource_slots(msl, "buffer")
    textures = resource_slots(msl, "texture")

    cb_mapping = {}
    tex_mapping = {}
    sampler_mapping = {}

    for name, kind, old, flags in params["binds"]:
        mname = name

        if name == "$Globals":
            mname = "VGlobals" if vs else "FGlobals"

        new = buffers[mname]

        updated = replace_record(
            updated, name,
            (kind, old, flags),
            (kind, new, flags)
        )

        cb_mapping[old] = new

    for name, old, sampler, dim in (
        pblob.parse(updated)["textures"]
    ):
        new = textures[name]

        updated = replace_record(
            updated, name,
            (0, old, sampler, dim),
            (0, new, sampler, dim)
        )

        tex_mapping[old] = new
        sampler_mapping[old] = sampler

    pblob.parse(updated)

    msp = metalgen.metal_subprogram(sp, msl)

    source_map, channels, _ = ushader.bind_channels(
        msp.tail
    )

    msp.tail = (
        msp.tail[:8 + len(channels)*8]
        + updated[4:]
    )

    built = msp.build()

    # Restore the Unity 2019 local-keyword block.
    pos = keyword_end(built, 24)
    built = built[:pos] + local_keywords + built[pos:]

    restored, local_check = parse_program(built)

    assert local_check == local_keywords
    assert restored.keywords == sp.keywords
    assert restored.type == msp.type

    return built, {
        "type": msp.type,
        "source_map": source_map,
        "channels": channels,
        "cbs": cb_mapping,
        "textures": tex_mapping,
        "samplers": sampler_mapping
    }, msl

def translate_entry(entry):
    sp, local_keywords = parse_program(entry)

    _, _, tail = ushader.bind_channels(sp.tail)

    raw = struct.pack("<i", sp.ver) + tail

    if sp.ver in (201708220, 201802150):
        params = parse_legacy(sp)

        msl = translate_legacy(sp, params)
        built, mapping = convert_entry(entry, msl)

        return built, mapping, msl

    params = pblob.parse(raw)

    msl = translate_legacy(sp, params)

    return convert_2019(
        sp, local_keywords, msl, raw
    )

def add_programs(d, mappings):
    linked = set()

    for ss in d["m_ParsedForm"]["m_SubShaders"]:
        for shader_pass in ss["m_Passes"]:
            for stage in ("progVertex", "progFragment"):
                programs = shader_pass[stage]["m_SubPrograms"]
                additions = []

                for entry in programs:
                    if entry["m_GpuProgramType"] not in (
                        ushader.D3D_VS + ushader.D3D_PS
                    ):
                        continue

                    index = entry["m_BlobIndex"]
                    mapping = mappings[index]

                    new = copy.deepcopy(entry)
                    new["m_GpuProgramType"] = mapping["type"]

                    for cb in new["m_ConstantBufferBindings"]:
                        cb["m_Index"] = mapping["cbs"][
                            cb["m_Index"]
                        ]

                    for tex in new["m_TextureParams"]:
                        old = tex["m_Index"]

                        tex["m_Index"] = mapping["textures"][old]

                        if old in mapping["samplers"]:
                            tex["m_SamplerIndex"] = (
                                mapping["samplers"][old]
                            )

                    new["m_Channels"]["m_SourceMap"] = (
                        mapping["source_map"]
                    )

                    new["m_Channels"]["m_Channels"] = [
                        {"source": s, "target": t}
                        for s, t in mapping["channels"]
                    ]

                    additions.append(new)
                    linked.add(index)

                programs.extend(additions)

    assert linked == set(mappings)

def append_platform(d, segments, nested):
    blob = bytearray(d["compressedBlob"])
    offsets, clens, dlens = [], [], []

    for raw in segments:
        packed = CompressionHelper.compress_lz4(raw)

        offsets.append(len(blob))
        clens.append(len(packed))
        dlens.append(len(raw))

        blob.extend(packed)

    for key, values in (
        ("offsets", offsets),
        ("compressedLengths", clens),
        ("decompressedLengths", dlens)
    ):
        if not nested:
            assert len(values) == 1

        d[key].append(values if nested else values[0])

    d["platforms"].append(14)

    d["compressedBlob"] = (
        list(blob)
        if isinstance(d["compressedBlob"], list)
        else bytes(blob)
    )

def process(source):
    output = DEST / source.name

    if output.exists():
        print("SKIP: Output already exists")
        return

    env = UnityPy.load(str(source))

    shader_dir = ROOT / "MSL" / source.stem
    shader_dir.mkdir(parents=True, exist_ok=True)

    records = {}
    metal_files = []

    for obj in env.objects:
        if shutil.disk_usage(shader_dir).free < 2200 * 1024**2:
            raise OSError("Conversion paused to preserve 2200 MiB free storage")
        if obj.type.name != "Shader":
            continue

        d = obj.read_typetree()

        if 4 not in d["platforms"] or 14 in d["platforms"]:
            continue

        entries, original, nested = read_platform(d, 4)

        versions = {
            struct.unpack_from("<I", b)[0]
            for b in entries
        }

        print(
            d["m_ParsedForm"]["m_Name"],
            f"({len(entries)} variants)"
        )

        metal_entries = []
        mappings = {}

        for index, entry in enumerate(entries):
            built, mapping, msl = translate_entry(entry)

            metal_entries.append(built)
            mappings[index] = mapping

            path = shader_dir / (
                f"{obj.path_id}-{index}.metal"
            )

            path.write_text(msl)
            metal_files.append(path)

        add_programs(d, mappings)

        segments = (
            ushader.build_segments(metal_entries)
            if nested
            else [build_table(metal_entries)]
        )

        append_platform(d, segments, nested)

        records[obj.path_id] = (
            original, segments
        )

        obj.save_typetree(d)

    if not records:
        print("No shaders converted")
        return

    # Actual Metal compilation on the M4.
    manifest = shader_dir / "metal-files.txt"
    manifest.write_text("\n".join(map(str, metal_files)))
    result = subprocess.run([
        "swift",
        str(Path(__file__).resolve().parents[2] / "tools/compile-metal.swift"),
        "--manifest", str(manifest)
    ])

    if result.returncode:
        raise RuntimeError("Native Metal compilation failed")

    temp = output.with_suffix(".tmp")
    temp.write_bytes(env.file.save(packer="lz4"))

    check = UnityPy.load(str(temp))
    verified = set()

    for obj in check.objects:
        if obj.path_id not in records:
            continue

        d = obj.read_typetree()

        _, original, _ = read_platform(d, 4)
        _, metal, _ = read_platform(d, 14)

        assert (original, metal) == records[obj.path_id]

        verified.add(obj.path_id)

    assert verified == set(records)

    temp.rename(output)

    print("SUCCESS:", output)
    print("Verified shaders:", len(verified))
    print("Compiled variants:", len(metal_files))
