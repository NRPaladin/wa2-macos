# SPDX-License-Identifier: MIT
"""Read user-owned Leaf KCAP/LAC archives. No game data is included here."""
from pathlib import Path
import struct


def index(path):
    path = Path(path)
    result = {}
    with path.open('rb') as f:
        magic = f.read(4)
        if magic == b'KCAP':
            f.seek(12)
            count, = struct.unpack('<I', f.read(4))
            for _ in range(count):
                crypt, name, offset, size = struct.unpack('<I24s8xII', f.read(44))
                name = name.rstrip(b'\0')
                result[name] = (path, offset, size, crypt)
        elif magic == b'LAC\0':
            count, = struct.unpack('<I', f.read(4))
            for _ in range(count):
                name, size, offset = struct.unpack('<32sII', f.read(40))
                name = bytes((~b & 255) if b else 0 for b in name).rstrip(b'\0')
                result[name] = (path, offset, size, 0)
        else:
            raise ValueError(f'Unsupported archive: {path}: {magic!r}')
    for name, (_, offset, size, _) in result.items():
        if size and offset + size > path.stat().st_size:
            raise ValueError(f'Entry outside archive: {path}: {name!r}')
    return result


def read(entry):
    path, offset, size, crypt = entry
    if not size:
        return b''
    with path.open('rb') as f:
        f.seek(offset)
        data = f.read(size)
    if not crypt:
        return data
    in_size, out_size = struct.unpack_from('<II', data)
    # Original archives count the header; some translation patches count only payload.
    if in_size == len(data):
        in_size -= 8
    if in_size > len(data) - 8 or out_size > 512 * 1024 * 1024:
        raise ValueError(f'Invalid compressed entry in {path}')
    source = memoryview(data)[8:8 + in_size]
    ring = bytearray(b' ' * 0xFEE + b'\0' * 18)
    output = bytearray(out_size)
    pos, written, head = 0, 0, 0xFEE
    while pos < in_size and written < out_size:
        flag = source[pos]
        pos += 1
        for _ in range(8):
            if pos >= in_size or written >= out_size:
                break
            a = source[pos]
            pos += 1
            if flag & 1:
                ring[head] = output[written] = a
                head = (head + 1) & 4095
                written += 1
            else:
                b = source[pos]
                pos += 1
                start = a | ((b & 0xF0) << 4)
                for k in range((b & 15) + 3):
                    if written >= out_size:
                        raise ValueError('LZSS output exceeds declared length')
                    value = ring[(start + k) & 4095]
                    ring[head] = output[written] = value
                    head = (head + 1) & 4095
                    written += 1
            flag >>= 1
    if written != out_size:
        raise ValueError(f'Truncated LZSS: {written}/{out_size}: {path}')
    return bytes(output)
