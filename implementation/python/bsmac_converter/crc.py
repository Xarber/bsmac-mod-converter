# SPDX-License-Identifier: AGPL-3.0-only
import sys
import struct
import zlib

from pathlib import Path
from UnityPy.helpers import CompressionHelper


def decompress(data, size, flags):
    mode = flags & 63

    if mode == 0:
        result = data
    elif mode == 1:
        result = CompressionHelper.decompress_lzma(data)
    elif mode in (2, 3):
        result = CompressionHelper.decompress_lz4(data, size)
    else:
        raise ValueError(f"Unknown compression: {mode}")

    assert len(result) == size
    return result


def bundle_crc(path):
    data = Path(path).read_bytes()

    assert data.startswith(b"UnityFS\0")

    version = struct.unpack_from(">I", data, 8)[0]

    pos = data.index(b"\0", 12) + 1
    pos = data.index(b"\0", pos) + 1

    total, compressed, uncompressed, flags = (
        struct.unpack_from(">QIII", data, pos)
    )

    assert total == len(data)

    pos += 20

    if version >= 7:
        pos = (pos + 15) & ~15

    if flags & 0x80:
        info_start = len(data) - compressed
        block_end = info_start
    else:
        info_start = pos
        pos += compressed
        block_end = len(data)

    info = decompress(
        data[info_start:info_start + compressed],
        uncompressed,
        flags
    )

    if flags & 0x200:
        pos = (pos + 15) & ~15

    count = struct.unpack_from(">I", info, 16)[0]
    assert 20 + count * 10 <= len(info)

    crc = 0

    for i in range(count):
        usize, csize, block_flags = struct.unpack_from(
            ">IIH", info, 20 + i * 10
        )

        assert pos + csize <= block_end

        block = decompress(
            data[pos:pos + csize],
            usize,
            block_flags
        )

        crc = zlib.crc32(block, crc)
        pos += csize

    return crc & 0xffffffff


