# SPDX-License-Identifier: AGPL-3.0-only
"""Adapter for the separately installed Unity 2021 converter."""
from collections import Counter
from copy import deepcopy
from hashlib import sha256
from itertools import chain

from bsmac.shaders import convert, metalgen, translate, ushader
from .legacy import append_platform
from .msl import repair_unsigned_offsets
from .structured import parse_buffers, remap_records, translate_buffers, verify_records


def translate_program(job):
    blob, raw, common, names = job
    program = ushader.SubProgram(blob)
    if program.ver != 202012090:
        raise ValueError(f'Unsupported modern shader version {program.ver}')
    params, buffers = parse_buffers(raw)
    if buffers:
        msl, slots = translate_buffers(program, params, buffers, common, names)
    else:
        msl, _ = translate.translate(program, params, common, names)
        msl = repair_unsigned_offsets(msl)
    convert.check_layout(msl, params, program.type in ushader.D3D_VS)
    msl, updated = convert.samplers_follow_textures(msl, params, raw)
    if buffers:
        updated = remap_records(updated, buffers, slots)
        verify_records(updated, buffers, slots)
    built = metalgen.metal_subprogram(program, msl).build()
    if ushader.SubProgram(built).keywords != program.keywords:
        raise ValueError('Modern shader keywords changed')
    return built, updated, msl


def passes(shader):
    return chain.from_iterable(s['m_Passes'] for s in shader['m_ParsedForm']['m_SubShaders'])


def add_shader(shader, shader_dir):
    _, jobs = convert.jobs_for(shader)
    stages = ('progVertex', 'progFragment', 'progGeometry', 'progHull', 'progDomain', 'progRayTracing')
    unsupported = Counter()
    for shader_pass in passes(shader):
        for stage in stages[2:]:
            for program in chain.from_iterable(shader_pass.get(stage, {}).get('m_PlayerSubPrograms', [])):
                kind = program['m_GpuProgramType']
                unsupported[(stage, kind, program['m_BlobIndex'])] += 1
    # The backend only knows how to schedule vertex and fragment programs.
    # Decline the whole shader if those stages depend on a D3D-only stage.
    if unsupported:
        return [], {'translated': 0, 'failed': 0, 'skipped': len(jobs),
                    'unsupported': len(unsupported), 'stages': sorted({k[0] for k in unsupported})}
    if not jobs:
        return [], {'translated': 0, 'failed': 0, 'skipped': 0, 'unsupported': 1,
                    'detail': 'No supported jobs for Direct3D shader'}
    programs = {}
    failures = {}
    sources = set()
    for key, job in jobs.items():
        try:
            program, params, msl = translate_program(job)
        except (ValueError, KeyError, AssertionError, RuntimeError, StopIteration) as error:
            failures[key] = str(error)
            continue
        programs[key] = program, params
        path = shader_dir / f'{sha256(msl.encode()).hexdigest()}.metal'
        if not path.exists():
            path.write_text(msl)
        sources.add(path)
    if programs:
        add_metal_programs(shader, programs)
    return sorted(sources), {'translated': len(programs), 'failed': len(failures), 'skipped': 0,
                             'unsupported': 0, 'errors': {str(k): v for k, v in failures.items()}}


def add_metal_programs(shader, programs):
    blobs = []
    indices = {}

    def intern(data):
        if data not in indices:
            indices[data] = len(blobs)
            blobs.append(data)
        return indices[data]

    expected = []
    seen = set()
    for shader_pass in passes(shader):
        for stage in ('progVertex', 'progFragment'):
            slots = shader_pass[stage]
            for entries, parameter_indices in zip(slots['m_PlayerSubPrograms'], slots['m_ParameterBlobIndices'], strict=True):
                additions = []
                for entry, index in zip(entries, parameter_indices, strict=True):
                    kind = entry['m_GpuProgramType']
                    if kind not in ushader.D3D_VS + ushader.D3D_PS:
                        continue
                    key = entry['m_BlobIndex'], index
                    if key not in programs:
                        continue
                    program, params = programs[key]
                    new = deepcopy(entry)
                    new['m_GpuProgramType'] = ushader.METAL_VS if kind in ushader.D3D_VS else ushader.METAL_FS
                    new['m_BlobIndex'] = intern(program)
                    param_index = intern(params)
                    additions.append((new, param_index))
                    expected.append((new['m_BlobIndex'], param_index, key))
                    seen.add(key)
                entries.extend(entry for entry, _ in additions)
                parameter_indices.extend(index for _, index in additions)
    if seen != programs.keys():
        raise ValueError('Modern Metal references do not match translated jobs')
    shader['stageCounts'].append(deepcopy(shader['stageCounts'][shader['platforms'].index(4)]))
    append_platform(shader, ushader.build_segments(blobs), nested=True)
    reopened = ushader.entries(ushader.platform_segments(shader, 14))
    for program_index, param_index, key in expected:
        if (reopened[program_index], reopened[param_index]) != programs[key]:
            raise ValueError('Metal program or parameters reference unexpected bytes')
