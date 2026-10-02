"""PNG read/write for RGBA uint8 numpy arrays (stdlib zlib only) + inspection helpers."""
import struct
import zlib

import numpy as np


def _chunk(tag, data):
    c = struct.pack(">I", len(data)) + tag + data
    return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def write_png(path, img, meta=None):
    """Write an (H, W, 4) uint8 RGBA array. `meta` dict -> tEXt chunks (before IDAT)."""
    img = np.ascontiguousarray(img, dtype=np.uint8)
    if img.shape[2] == 3:
        img = np.concatenate([img, np.full(img.shape[:2] + (1,), 255, np.uint8)], 2)
    h, w = img.shape[:2]
    raw = np.zeros((h, w * 4 + 1), np.uint8)
    raw[:, 1:] = img.reshape(h, w * 4)
    out = [b"\x89PNG\r\n\x1a\n",
           _chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))]
    for k, v in (meta or {}).items():
        out.append(_chunk(b"tEXt", k.encode("latin-1") + b"\0" + str(v).encode("latin-1")))
    out.append(_chunk(b"IDAT", zlib.compress(raw.tobytes(), 9)))
    out.append(_chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(b"".join(out))


def read_png(path):
    """Read an 8-bit RGBA/RGB non-interlaced PNG into (H, W, 4) uint8 (slow filters in pure python)."""
    with open(path, "rb") as f:
        data = f.read()
    pos, idat, w, h, ct = 8, b"", 0, 0, 6
    while pos < len(data):
        ln, tag = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, _bd, ct = struct.unpack(">IIBB", body[:10])
        elif tag == b"IDAT":
            idat += body
        pos += 12 + ln
    bpp = {6: 4, 2: 3}[ct]
    raw = np.frombuffer(zlib.decompress(idat), np.uint8).reshape(h, w * bpp + 1)
    out = np.zeros((h, w * bpp), np.int32)
    prev = np.zeros(w * bpp, np.int32)
    for y in range(h):
        ft, line = raw[y, 0], raw[y, 1:].astype(np.int32)
        if ft == 0:
            cur = line
        elif ft == 2:
            cur = (line + prev) & 255
        else:
            cur = np.zeros_like(line)
            for x in range(w * bpp):
                a = cur[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                if ft == 1:
                    p = a
                elif ft == 3:
                    p = (a + b) // 2
                else:
                    pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                    p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                cur[x] = (line[x] + p) & 255
        out[y] = cur
        prev = cur
    img = out.reshape(h, w, bpp).astype(np.uint8)
    if bpp == 3:
        img = np.concatenate([img, np.full((h, w, 1), 255, np.uint8)], 2)
    return img


def upscale(img, k):
    """Nearest-neighbour integer upscale (for inspecting crops)."""
    return np.repeat(np.repeat(img, k, 0), k, 1)


def on_bg(img, bg=(96, 112, 96)):
    """Flatten RGBA over a solid colour (for previews)."""
    out = np.empty(img.shape[:2] + (4,), np.uint8)
    a = img[..., 3:4] > 0
    out[..., :3] = np.where(a, img[..., :3], np.array(bg, np.uint8))
    out[..., 3] = 255
    return out


def inspect(path_out, img, k=4, bg=(96, 112, 96), crop=None):
    """Write an upscaled, flattened preview PNG of `img` (optionally crop=(x, y, w, h))."""
    if crop is not None:
        x, y, w, h = crop
        img = img[y:y + h, x:x + w]
    write_png(path_out, upscale(on_bg(img, bg), k))
