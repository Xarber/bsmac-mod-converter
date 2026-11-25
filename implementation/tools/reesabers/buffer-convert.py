# SPDX-License-Identifier: AGPL-3.0-only
import re
import sys
import struct
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor

import UnityPy
from bsmac.shaders import (
    convert, pblob, ushader,
    translate as T, rdef, metalgen
)

import argparse

parser = argparse.ArgumentParser(description="Convert the verified ReeSabers structured-buffer bundle")
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
parser.add_argument("--build", action="store_true")
args = parser.parse_args()
SOURCE, DEST = args.source.resolve(), args.destination.resolve()
ROOT = DEST.parent
if SOURCE == DEST:
    parser.error("Use a separate output path")

BUFFER = re.compile(
    r"\[\[\s*buffer\s*\(\s*(\d+)\s*\)\s*\]\]"
)

def parse_extended(data):
    temp = bytearray(data)
    buffers = {}

    while True:
        try:
            params = pblob.parse(bytes(temp))
            break
        except ValueError as e:
            m = re.fullmatch(
                r"unknown resource kind 2 \((.*?)\) at (\d+)",
                str(e)
            )

            if not m:
                raise

            name = m.group(1)
            pos = int(m.group(2))
            slot, count = struct.unpack_from("<ii", temp, pos)

            if count != 1 or name in buffers:
                raise ValueError("Unsupported buffer record")

            if struct.unpack_from("<i", temp, pos - 4)[0] != 2:
                raise ValueError("Unexpected buffer format")

            buffers[name] = (slot, count, pos)
            struct.pack_into("<i", temp, pos - 4, 1)

    params["binds"] = [
        b for b in params["binds"]
        if b[0] not in buffers
    ]

    return params, buffers


def recover(args):
    blob, raw, common, names = args
    params, buffers = parse_extended(raw)

    if not buffers:
        raise ValueError("No structured buffers")

    sp = ushader.SubProgram(blob)
    dxbc = sp.dxbc()
    declarations = rdef.shdr_buffers(dxbc)

    resources = []

    for name, (slot, count, pos) in buffers.items():
        rtype, stride, dim = declarations[("t", slot)]
        resources.append((name, rtype, slot, stride, dim))

    cbs, binds = T.merged_cbuffers(params, common, names)

    tex, samp = T.resource_names(
        dxbc, common, names, params
    )

    rebuilt = rdef.with_rdef(
        dxbc, cbs, binds, tex, samp, resources
    )

    msl, reflection = T.run_hlslcc(rebuilt)

    msl = T.array_tail_members(
        T.metal_array_strides(
            T.unity_style(msl), cbs
        ), cbs
    )

    locations = {}

    for name in buffers:
        pattern = re.compile(
            r"\b" + re.escape(name) +
            r"\s*\[\[\s*buffer\s*\(\s*(\d+)\s*\)\s*\]\]"
        )

        matches = list(pattern.finditer(msl))

        if len(matches) != 1:
            raise ValueError(
                f"Unexpected Metal declaration: {name}"
            )

        locations[name] = matches[0].span(1)

    structured_spans = set(locations.values())

    used = {
        int(m.group(1))
        for m in BUFFER.finditer(msl)
        if m.span(1) not in structured_spans
    }

    # Preserve existing constant-buffer indices.
    # Avoid slots reserved by their original metadata.
    used.update(
        slot for slot in binds.values()
        if isinstance(slot, int) and 0 <= slot < 31
    )

    metadata = bytearray(raw)
    edits = []
    mapping = {}

    for name, (old_slot, count, pos) in buffers.items():
        new_slot = next(
            (i for i in range(31) if i not in used),
            None
        )

        if new_slot is None:
            raise ValueError("No free Metal buffer slots")

        used.add(new_slot)

        start, end = locations[name]
        edits.append((start, end, str(new_slot)))

        # Preserve resource kind 2 and binding count.
        struct.pack_into("<i", metadata, pos, new_slot)

        mapping[name] = new_slot

    for start, end, value in sorted(edits, reverse=True):
        msl = msl[:start] + value + msl[end:]

    slots = [int(x) for x in BUFFER.findall(msl)]

    if len(slots) != len(set(slots)):
        raise ValueError("Metal binding collision remains")

    convert.check_layout(
        msl, params, sp.type in ushader.D3D_VS
    )

    msl, metadata = convert.samplers_follow_textures(
        msl, params, bytes(metadata)
    )

    program = metalgen.metal_subprogram(sp, msl)

    return program.build(), metadata, msl, mapping


def translate_extended(args):
    original = convert.translate_one(args)

    if not original[2]:
        return original

    if "unknown resource kind 2" not in original[2]:
        return original

    try:
        program, metadata, _, _ = recover(args)
        return program, metadata, None

    except Exception as e:
        return None, None, str(e)[:300]


class ExtendedPool:
    def __init__(self, executor):
        self.executor = executor

    def map(self, fn, jobs, chunksize=8):
        jobs = list(jobs)

        results = list(self.executor.map(
            translate_extended,
            jobs,
            chunksize=chunksize
        ))

        # A program shared by different parameter blobs
        # must not receive conflicting implementations.
        programs = {}

        for args, result in zip(jobs, results):
            if result[2]:
                continue

            original_blob = args[0]

            if original_blob in programs:
                if programs[original_blob] != result[0]:
                    raise RuntimeError(
                        "Conflicting shared shader program"
                    )

            programs[original_blob] = result[0]

        return iter(results)


def test():
    out = ROOT / "BufferTests"
    out.mkdir(exist_ok=True)

    env = UnityPy.load(str(SOURCE))

    for obj in env.objects:
        if obj.type.name != "Shader":
            continue

        d = obj.read_typetree()

        if d["m_ParsedForm"]["m_Name"] != "Ree/BlurSaber":
            continue

        _, jobs = convert.jobs_for(d)

        for key in ((12, 0), (13, 1), (14, 0)):
            program, metadata, msl, mapping = recover(jobs[key])

            dest = out / f"BlurSaber-{key[0]}-{key[1]}-fixed.metal"
            dest.write_text(msl)

            print("\nVariant:", key)
            print("New buffer bindings:", mapping)
            print("All slots:", BUFFER.findall(msl))
            print("Saved:", dest)

    print("\nBuffer remapping tests passed.")


def build():
    if DEST.exists():
        raise RuntimeError(
            f"Output already exists: {DEST}"
        )

    with ProcessPoolExecutor(max_workers=4) as executor:
        result = convert.convert_file(
            str(SOURCE),
            str(DEST),
            pool=ExtendedPool(executor),
            log=print
        )

    print("\n=== FINAL RESULTS ===")
    print(dict(result))



def add_metal_split(d, pool, log, progress=None):
    from UnityPy.helpers import CompressionHelper

    segments, jobs = convert.jobs_for(d)
    keys = list(jobs)

    executor = (
        pool.executor
        if isinstance(pool, ExtendedPool)
        else pool
    )

    results = list(executor.map(
        translate_extended,
        [jobs[k] for k in keys],
        chunksize=8
    ))

    assert len(results) == len(keys)

    mblobs = list(segments)
    implementations = {}
    parameters = {}
    mapping = {}

    ok = bad = splits = 0

    for i, (key, result) in enumerate(zip(keys, results)):
        bi, pi = key
        program, metadata, error = result

        if progress and i % 500 == 0:
            progress(i / len(keys))

        if error:
            bad += 1
            if bad <= 3:
                log(f"  variant {key} failed: {error}")
            continue

        assert program and metadata

        versions = implementations.setdefault(bi, {})

        if program in versions:
            new_bi = versions[program]

        elif not versions:
            new_bi = bi
            mblobs[new_bi] = program
            versions[program] = new_bi

        else:
            # Same original blob, different Metal implementation.
            new_bi = len(mblobs)
            mblobs.append(program)
            versions[program] = new_bi
            splits += 1

        if metadata not in parameters:
            parameters[metadata] = len(mblobs)
            mblobs.append(metadata)

        new_pi = parameters[metadata]

        mapping[key] = (new_bi, new_pi)
        ok += 1

    if bad:
        raise RuntimeError(
            f"{bad} variants failed; refusing partial output"
        )

    linked = set()

    for ss in d["m_ParsedForm"]["m_SubShaders"]:
        for shader_pass in ss["m_Passes"]:
            for stage in ("progVertex", "progFragment"):
                prog = shader_pass[stage]

                for li, entries in enumerate(
                    prog["m_PlayerSubPrograms"]
                ):
                    indices = prog["m_ParameterBlobIndices"][li]

                    assert len(entries) == len(indices)

                    extra_entries = []
                    extra_indices = []

                    for entry, pi in zip(entries, indices):
                        gpu_type = entry["m_GpuProgramType"]

                        if gpu_type not in (
                            ushader.D3D_VS + ushader.D3D_PS
                        ):
                            continue

                        key = (entry["m_BlobIndex"], pi)

                        if key not in mapping:
                            continue

                        new_bi, new_pi = mapping[key]

                        new_entry = dict(entry)
                        new_entry["m_BlobIndex"] = new_bi

                        new_entry["m_GpuProgramType"] = (
                            ushader.METAL_VS
                            if gpu_type in ushader.D3D_VS
                            else ushader.METAL_FS
                        )

                        extra_entries.append(new_entry)
                        extra_indices.append(new_pi)
                        linked.add(key)

                    entries.extend(extra_entries)
                    indices.extend(extra_indices)

    if linked != set(mapping):
        raise RuntimeError("Unlinked Metal shader variants")

    compressed = bytearray(d["compressedBlob"])
    offsets = []
    clens = []
    dlens = []

    for segment in ushader.build_segments(mblobs):
        packed = CompressionHelper.compress_lz4(segment)

        offsets.append(len(compressed))
        clens.append(len(packed))
        dlens.append(len(segment))

        compressed.extend(packed)

    d["offsets"].append(offsets)
    d["compressedLengths"].append(clens)
    d["decompressedLengths"].append(dlens)

    d["compressedBlob"] = (
        list(compressed)
        if isinstance(d["compressedBlob"], list)
        else bytes(compressed)
    )

    d["stageCounts"].append(
        d["stageCounts"][d["platforms"].index(4)]
    )

    d["platforms"].append(14)

    # Verify that serialization preserved every blob.
    restored = ushader.entries(
        ushader.platform_segments(d, 14)
    )

    if list(restored) != mblobs:
        raise RuntimeError("Metal blob round-trip mismatch")

    original = ushader.entries(
        ushader.platform_segments(d, 4)
    )

    if list(original) != list(segments):
        raise RuntimeError("Original D3D blobs were modified")

    if splits:
        log(f"  Separated {splits} shared shader implementations")

    return ok, bad


convert.add_metal = add_metal_split


if __name__ == "__main__":
    if "--build" in sys.argv:
        build()
    else:
        test()
