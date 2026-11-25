# SPDX-License-Identifier: AGPL-3.0-only
import lzma
import struct
import zlib

from pathlib import Path
from UnityPy.helpers import CompressionHelper


def decompress(data, size, flags):
    mode = flags & 63

    if mode == 0:
        result = data
    elif mode == 1:
        if len(data) < 5 or not 0 <= size <= 512 * 1024**2:
            raise ValueError("Invalid UnityFS LZMA block size")
        props = data[0]
        dictionary = struct.unpack_from("<I", data, 1)[0]
        if props >= 225 or dictionary > 256 * 1024**2:
            raise ValueError("Invalid or oversized UnityFS LZMA properties")
        decoder = lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[{
            "id": lzma.FILTER_LZMA1,
            "dict_size": dictionary,
            "lc": props % 9,
            "lp": (props // 9) % 5,
            "pb": props // 45,
        }])
        # UnityFS records the uncompressed size. Its raw stream can omit an
        # end marker, so EOF is not required when the declared bytes are read.
        result = decoder.decompress(data[5:], max_length=size) if size else b''
    elif mode in (2, 3):
        result = CompressionHelper.decompress_lz4(data, size)
    else:
        raise ValueError(f"Unknown compression: {mode}")

    if len(result) != size:
        raise ValueError("UnityFS block length does not match its declared size")
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


