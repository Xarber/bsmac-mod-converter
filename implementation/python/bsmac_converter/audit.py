# SPDX-License-Identifier: AGPL-3.0-only
"""Report serialized shader coverage without mutating or loading a map in Unity."""
from collections import Counter

import UnityPy

STAGES = ('progVertex', 'progFragment', 'progGeometry', 'progHull', 'progDomain', 'progRayTracing')


def audit_shaders(original, derivative):
    from bsmac.shaders import ushader
    from .legacy import read_platform

    def shaders(path):
        env = UnityPy.load(str(path))
        return {obj.path_id: obj.read_typetree() for obj in env.objects if obj.type.name == 'Shader'}

    before, after = shaders(original), shaders(derivative)
    if before.keys() != after.keys():
        raise ValueError('The original and derivative shader identities do not match')
    for identity, source in before.items():
        if 4 not in source['platforms']:
            continue
        output = after[identity]
        stages = Counter()
        output_stages = Counter()
        for shader, counts in ((source, stages), (output, output_stages)):
            for sub in shader['m_ParsedForm']['m_SubShaders']:
                for shader_pass in sub['m_Passes']:
                    for stage in STAGES:
                        slot = shader_pass.get(stage, {})
                        programs = list(slot.get('m_SubPrograms', []))
                        for group in slot.get('m_PlayerSubPrograms', []):
                            programs.extend(group)
                        for program in programs:
                            counts[(stage, program['m_GpuProgramType'])] += 1
        metal_platform = 14 in output['platforms']
        metal_entries = len(read_platform(output, 14)[0]) if metal_platform else 0
        yield {'shader': source['m_ParsedForm']['m_Name'], 'pathId': identity,
               'metalPlatform': metal_platform, 'metalTableEntries': metal_entries,
               'sourcePrograms': {stage: {kind: count for (s, kind), count in stages.items() if s == stage}
                                  for stage in STAGES if any(s == stage for s, _ in stages)},
               'metalProgramReferences': {stage: sum(output_stages[stage, kind] for kind in (ushader.METAL_VS, ushader.METAL_FS))
                                          for stage in STAGES}}
