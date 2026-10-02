"""
iso_life_common - shared helpers of the LIFE workstream (vehicles, people, FX, markers, cursors).

PNG IO, colour ramps, sprite sheets/strips, 2D pixel drawing on numpy RGBA arrays, previews and the
Figma manifest builder. Projection constants follow mods/city/design/iso/BRIEF.md.
When isokit is available its palette ramps are used (see `ramp`); otherwise the local fallback ramps.
"""
import json
import os
import struct
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "design", "iso", "life"))

TILE_W, TILE_H = 64, 32


# ----------------------------------------------------------------------------- PNG IO
def save_png(path, img):
    """img: HxWx4 uint8. Alpha is forced to 0/255."""
    img = np.asarray(img, dtype=np.uint8).copy()
    img[..., 3] = np.where(img[..., 3] >= 128, 255, 0)
    img[img[..., 3] == 0] = 0
    h, w = img.shape[:2]
    raw = b"".join(b"\x00" + img[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def load_png(path):
    """Minimal reader for 8-bit RGBA/RGB non-interlaced PNGs (enough for our own files)."""
    data = open(path, "rb").read()
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        n = struct.unpack(">I", data[pos:pos + 4])[0]
        t = data[pos + 4:pos + 8]
        d = data[pos + 8:pos + 8 + n]
        if t == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", d[:10])
        elif t == b"IDAT":
            idat += d
        pos += 12 + n
    ch = 4 if ct == 6 else 3
    raw = zlib.decompress(idat)
    out = np.zeros((h, w * ch), np.int32)
    stride = w * ch
    prev = np.zeros(stride, np.int32)
    i = 0
    for y in range(h):
        f = raw[i]
        row = np.frombuffer(raw[i + 1:i + 1 + stride], np.uint8).astype(np.int32)
        i += 1 + stride
        cur = np.zeros(stride, np.int32)
        for x in range(stride):
            a = cur[x - ch] if x >= ch else 0
            b = prev[x]
            c = prev[x - ch] if x >= ch else 0
            if f == 0:
                p = 0
            elif f == 1:
                p = a
            elif f == 2:
                p = b
            elif f == 3:
                p = (a + b) // 2
            else:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
            cur[x] = (row[x] + p) & 255
        out[y] = cur
        prev = cur
    img = out.reshape(h, w, ch).astype(np.uint8)
    if ch == 3:
        img = np.concatenate([img, np.full((h, w, 1), 255, np.uint8)], 2)
    return img


# ----------------------------------------------------------------------------- colour
def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float64)


def make_ramp(base, n=8, warm=0.06, cool=0.10):
    """A ramp dark->light around `base` (hex). Shadows shift cool (blue), highlights warm (yellow)."""
    b = hexrgb(base) / 255.0
    out = []
    for i in range(n):
        t = i / (n - 1)                 # 0 dark .. 1 light
        k = 0.30 + 1.05 * t             # brightness multiplier
        c = b * k
        if t < 0.5:
            c = c + (0.5 - t) * cool * np.array([-0.4, -0.1, 0.6])
        else:
            c = c + (t - 0.5) * warm * np.array([0.6, 0.4, -0.5])
        if k > 1.0:                     # highlights lift toward white
            c = c + (k - 1.0) * 0.6 * (1 - c)
        out.append(np.clip(np.round(c * 255), 0, 255).astype(np.uint8))
    return np.array(out)


def rgba(c, a=255):
    if isinstance(c, str):
        c = hexrgb(c)
    c = np.asarray(c)
    return np.array([int(c[0]), int(c[1]), int(c[2]), a], np.uint8)


# ----------------------------------------------------------------------------- canvases
def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def put(img, x, y, c):
    h, w = img.shape[:2]
    if 0 <= x < w and 0 <= y < h:
        img[y, x] = rgba(c) if not isinstance(c, np.ndarray) or len(c) != 4 else c


def blit(dst, src, x, y):
    """Alpha-blit src (binary alpha) onto dst at x, y (may clip)."""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    m = s[..., 3] > 0
    dst[y0:y1, x0:x1][m] = s[m]


def crop(img, pad=0):
    """Crop to the opaque bbox. Returns (img, dx, dy) with the offset of the crop origin."""
    ys, xs = np.nonzero(img[..., 3])
    if len(xs) == 0:
        return img[:1, :1], 0, 0
    x0, x1, y0, y1 = xs.min() - pad, xs.max() + 1 + pad, ys.min() - pad, ys.max() + 1 + pad
    x0, y0 = max(0, x0), max(0, y0)
    return img[y0:y1, x0:x1], x0, y0


def strip(frames, gap=0):
    """Horizontal strip of equally sized frames."""
    h = max(f.shape[0] for f in frames)
    w = max(f.shape[1] for f in frames)
    out = canvas(len(frames) * (w + gap) - gap, h)
    for i, f in enumerate(frames):
        blit(out, f, i * (w + gap), 0)
    return out


def unify(frames):
    """Pad frames to a common size (bottom-centre aligned)."""
    h = max(f.shape[0] for f in frames)
    w = max(f.shape[1] for f in frames)
    res = []
    for f in frames:
        c = canvas(w, h)
        blit(c, f, (w - f.shape[1]) // 2, h - f.shape[0])
        res.append(c)
    return res


def upscale(img, k):
    return np.repeat(np.repeat(img, k, 0), k, 1)


def preview(path, img, k=6, bg=(120, 150, 110)):
    """Upscaled preview on a flat background (for inspecting renders, not shipped)."""
    big = upscale(img, k)
    out = np.zeros_like(big)
    out[..., :3] = bg
    out[..., 3] = 255
    m = big[..., 3] > 0
    out[m] = big[m]
    save_png(path, out)


# ----------------------------------------------------------------------------- manifest
class Manifest:
    def __init__(self, page, section, root):
        self.page, self.section, self.root, self.groups = page, section, root, []

    def group(self, title, items, columns=8, scale=2, note=None):
        g = {"title": title, "columns": columns, "scale": scale, "items": items}
        if note:
            g["note"] = note
        self.groups.append(g)

    def item(self, img, rel, label):
        save_png(os.path.join(self.root, rel), img)
        return {"file": rel, "label": label}

    def data(self):
        return {"page": self.page, "section": self.section, "groups": self.groups}
