# SPDX-License-Identifier: AGPL-3.0-only
"""Targeted repairs for known HLSLcc Metal output."""
import re


_UNSIGNED_LITERAL_OFFSET = re.compile(
    r"(\bu_xlatu\d+\.[xyzw]{2}\s*\+\s*)ivec2"
    r"(\(\s*\d+\s*,\s*\d+\s*\))"
)


def repair_unsigned_offsets(source):
    """Repair unsigned swizzle offsets while preserving signed expressions."""
    return _UNSIGNED_LITERAL_OFFSET.sub(r"\g<1>uint2\g<2>", source)
