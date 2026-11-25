# SPDX-License-Identifier: AGPL-3.0-only
"""Adapter for the separately installed Unity 2021 converter."""
from collections import Counter
from hashlib import sha256
from itertools import chain

from bsmac.shaders import convert, metalgen, translate, ushader
from .msl import repair_unsigned_offsets
from .structured import parse_buffers, remap_records, translate_buffers, verify_records


class TranslationPool:
    """Run backend jobs in the isolated worker and capture compilation inputs."""

    def __init__(self, keys, shader_dir):
        self.keys = list(keys)
        self.shader_dir = shader_dir
        self.sources = []
        self.failures = {}
        self.programs = {}
        self.parameters = {}

    def map(self, function, jobs, chunksize=1):
        for key, job in zip(self.keys, jobs, strict=True):
            try:
                program, params, msl = translate_program(job)
                previous = self.programs.setdefault(key[0], program)
                if previous != program:
                    raise ValueError('One blob needs multiple incompatible Metal programs')
                path = self.shader_dir / f'{sha256(msl.encode()).hexdigest()}.metal'
                if not path.exists():
                    path.write_text(msl)
                self.sources.append(path)
                self.parameters[key] = params
                yield program, params, None
            except (ValueError, KeyError, AssertionError, RuntimeError, StopIteration) as error:
                self.failures[key] = str(error)
                yield None, None, str(error)


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
    pool = TranslationPool(jobs, shader_dir)
    ok, failed = convert.add_metal(shader, pool, print)
    if ok + failed != len(jobs) or failed != len(pool.failures):
        raise ValueError('Modern backend job accounting changed')
    if ok:
        verify_bindings(shader, jobs, pool)
    return pool.sources, {'translated': ok, 'failed': failed, 'skipped': 0, 'unsupported': 0,
                          'errors': {str(k): v for k, v in pool.failures.items()}}


def verify_bindings(shader, jobs, pool):
    metal = ushader.entries(ushader.platform_segments(shader, 14))
    seen = set()
    for shader_pass in passes(shader):
        for stage in ('progVertex', 'progFragment'):
            prog = shader_pass[stage]
            for entries, parameter_indices in zip(prog['m_PlayerSubPrograms'], prog['m_ParameterBlobIndices'], strict=True):
                old_indices = (index for entry, index in zip(entries, parameter_indices, strict=True)
                               if entry['m_GpuProgramType'] in ushader.D3D_VS + ushader.D3D_PS
                               and (entry['m_BlobIndex'], index) in jobs
                               and (entry['m_BlobIndex'], index) not in pool.failures)
                for entry, new_index in zip(entries, parameter_indices, strict=True):
                    if entry['m_GpuProgramType'] not in (ushader.METAL_VS, ushader.METAL_FS):
                        continue
                    blob_index = entry['m_BlobIndex']
                    key = blob_index, next(old_indices)
                    if metal[blob_index] != pool.programs[blob_index]:
                        raise ValueError('Metal program index does not match translated bytes')
                    if metal[new_index] != pool.parameters[key]:
                        raise ValueError('Modern parameter index does not match validated bytes')
                    seen.add(key)
    if seen != jobs.keys() - pool.failures.keys():
        raise ValueError('Modern Metal references do not match translated jobs')
