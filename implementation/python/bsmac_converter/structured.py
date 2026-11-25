# SPDX-License-Identifier: AGPL-3.0-only
import re, struct
from . import legacy
from .legacy import *
from bsmac.shaders import rdef

original_parse = pblob.parse
original_translate = translate_entry
original_add_programs = add_programs

def parse_buffers(raw):
    temp = bytearray(raw)
    resources = {}

    while True:
        try:
            params = original_parse(bytes(temp))
            break

        except ValueError as exc:
            match = re.fullmatch(
                r"unknown resource kind 2 \((.*?)\) at (\d+)",
                str(exc)
            )

            if not match:
                raise

            name = match.group(1)
            pos = int(match.group(2))

            slot, flags = struct.unpack_from("<ii", temp, pos)

            assert struct.unpack_from("<i", temp, pos-4)[0] == 2
            assert flags in (0, 1)
            assert name not in resources

            resources[name] = (slot, flags)

            # Temporarily represent the record as kind 1
            # so the existing parser can read past it.
            struct.pack_into("<i", temp, pos-4, 1)

    # Structured buffers must not be treated as constant buffers.
    params["binds"] = [
        binding for binding in params["binds"]
        if binding[0] not in resources
    ]

    return params, resources

# Keep the original serialized resource records unchanged.
# Only the parser's temporary copy is normalized.
pblob.parse = lambda raw: parse_buffers(raw)[0]

def translate_entry(entry):
    sp, local = parse_program(entry)

    _, _, tail = ushader.bind_channels(sp.tail)

    raw = struct.pack("<i", sp.ver) + tail
    _, buffers = parse_buffers(raw)

    if not buffers:
        return original_translate(entry)

    if sp.ver == 201708220:
        params = parse_legacy(sp)
    elif sp.ver == 201806140:
        params = parse_buffers(raw)[0]
    else:
        raise ValueError(f"Unsupported structured-buffer format: {sp.ver}")

    msl, remapped = translate_buffers(sp, params, buffers)
    if sp.ver == 201806140:
        raw = remap_records(raw, buffers, remapped)
        built, mapping, msl = legacy.convert_2019(sp, local, msl, raw)
        check, checked_local = parse_program(built)
        _, _, check_tail = ushader.bind_channels(check.tail)
        verify_records(struct.pack("<i", check.ver) + check_tail, buffers, remapped)
        if checked_local != local:
            raise ValueError("Unity 2019 local keywords changed")
        mapping["buffers"] = remapped
        return built, mapping, msl

    built, mapping = convert_entry(entry, msl)
    restored = ushader.SubProgram(built)
    _, _, restored_tail = ushader.bind_channels(restored.tail)
    updated = remap_records(struct.pack("<i", restored.ver) + restored_tail, buffers, remapped)
    prefix_length = len(restored.tail) - len(restored_tail)
    restored.tail = restored.tail[:prefix_length] + updated[4:]
    built = restored.build()
    check = ushader.SubProgram(built)
    _, _, check_tail = ushader.bind_channels(check.tail)
    verify_records(struct.pack("<i", check.ver) + check_tail, buffers, remapped)
    mapping["buffers"] = remapped
    return built, mapping, msl


def remap_records(raw, buffers, remapped):
    for name, (old, flags) in buffers.items():
        raw = replace_record(raw, name, (2, old, flags), (2, remapped[old], flags))
    return raw


def verify_records(raw, buffers, remapped):
    _, verified = parse_buffers(raw)
    if verified != {name: (remapped[old], flags) for name, (old, flags) in buffers.items()}:
        raise ValueError("Structured-buffer resource records failed validation")


def translate_buffers(sp, params, buffers, common=None, names=()):
    dxbc = sp.dxbc()
    declarations = rdef.shdr_buffers(dxbc)

    resources = []

    for name, (slot, flags) in buffers.items():
        resource_type, stride, dimension = declarations[("t", slot)]

        resources.append(
            (name, resource_type, slot, stride, dimension)
        )

        print("Structured buffer:", name, "stride:", stride)

    common = {} if common is None else common
    cbs, binds = T.merged_cbuffers(params, common, names)

    textures, samplers = T.resource_names(
        dxbc, common, names, params
    )

    rebuilt = rdef.with_rdef(
        dxbc, cbs, binds, textures, samplers, resources
    )

    msl, reflection = T.run_hlslcc(rebuilt)

    msl = T.array_tail_members(
        T.metal_array_strides(
            T.unity_style(msl), cbs
        ),
        cbs
    )
    msl = legacy.repair_unsigned_offsets(msl)

    pattern = re.compile(
        r"\[\[\s*buffer\s*\(\s*(\d+)\s*\)\s*\]\]"
    )

    locations = {}

    for name in buffers:
        matches = list(re.finditer(
            r"\b" + re.escape(name)
            + r"\s*\[\[\s*buffer\s*\(\s*(\d+)\s*\)\s*\]\]",
            msl
        ))

        assert len(matches) == 1, name

        locations[name] = matches[0].span(1)

    structured_spans = set(locations.values())

    used = {
        int(m.group(1))
        for m in pattern.finditer(msl)
        if m.span(1) not in structured_spans
    }

    edits = []
    remapped = {}

    for name, (old, flags) in buffers.items():
        new = next(i for i in range(31) if i not in used)
        used.add(new)

        start, end = locations[name]

        edits.append((start, end, str(new)))
        remapped[old] = new

        print(f"  {name}: slot {old} -> {new}")

    for start, end, replacement in sorted(edits, reverse=True):
        msl = msl[:start] + replacement + msl[end:]

    slots = list(map(int, pattern.findall(msl)))

    assert len(slots) == len(set(slots)), (
        "Metal buffer binding collision"
    )

    convert.check_layout(
        msl, params, sp.type in ushader.D3D_VS
    )

    return msl, remapped

def add_programs(d, mappings):
    original_add_programs(d, mappings)

    for ss in d["m_ParsedForm"]["m_SubShaders"]:
        for shader_pass in ss["m_Passes"]:
            for stage in ("progVertex", "progFragment"):
                for entry in shader_pass[stage]["m_SubPrograms"]:
                    if entry["m_GpuProgramType"] not in (
                        ushader.METAL_VS, ushader.METAL_FS
                    ):
                        continue

                    mapping = mappings[entry["m_BlobIndex"]]
                    buffers = mapping.get("buffers", {})

                    if not buffers:
                        continue

                    checked = set()

                    for param in entry["m_BufferParams"]:
                        old = param["m_Index"]

                        param["m_Index"] = buffers[old]
                        checked.add(old)

                    assert checked == set(buffers)


legacy.translate_entry = translate_entry
legacy.add_programs = add_programs
