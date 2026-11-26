# A small PNG reader and writer in plain Python (no PIL on this PC): 8-bit RGB or RGBA, not interlaced.
import struct
import zlib


def read(path):
    """Returns (width, height, rows): rows[y] is a bytearray of RGBA bytes."""
    data = open(path, 'rb').read()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('not a PNG: ' + path)
    pos, idat, w = 8, [], 0
    while pos < len(data):
        n, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if kind == b'IHDR':
            w, h, depth, ctype, _, _, lace = struct.unpack('>IIBBBBB', body)
            if depth != 8 or ctype not in (2, 6) or lace:
                raise ValueError('only 8-bit RGB/RGBA, not interlaced: ' + path)
        elif kind == b'IDAT':
            idat.append(body)
        elif kind == b'IEND':
            break
    raw = zlib.decompress(b''.join(idat))
    bpp = 4 if ctype == 6 else 3
    stride = w * bpp
    rows, prev, at = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[at]
        line = bytearray(raw[at + 1:at + 1 + stride])
        at += 1 + stride
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                left = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        if bpp == 3:
            rgba = bytearray(w * 4)
            rgba[0::4], rgba[1::4], rgba[2::4] = line[0::3], line[1::3], line[2::3]
            rgba[3::4] = b'\xff' * w
            line = rgba
        rows.append(line)
    return w, h, rows


def write(path, w, h, rows):
    """rows[y]: RGBA bytes. Each line is filtered with Sub or Up, whichever packs smaller (cheap and good enough)."""
    stride = w * 4
    out, prev = bytearray(), bytearray(stride)
    for line in rows:
        sub = bytearray(stride)
        sub[:4] = line[:4]
        for i in range(4, stride):
            sub[i] = (line[i] - line[i - 4]) & 255
        up = bytearray((line[i] - prev[i]) & 255 for i in range(stride))
        if sum(min(v, 256 - v) for v in sub[::7]) <= sum(min(v, 256 - v) for v in up[::7]):
            out += b'\x01' + sub
        else:
            out += b'\x02' + up
        prev = line

    def chunk(kind, body):
        return struct.pack('>I', len(body)) + kind + body + struct.pack('>I', zlib.crc32(kind + body) & 0xffffffff)

    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(out), 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)
