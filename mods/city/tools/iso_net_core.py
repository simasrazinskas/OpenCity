"""
iso_net_core.py - shared helpers for the NET iso art (design phase): projection, PNG io, ground-tile raycaster.

Projection (BRIEF.md): screen_x = (x - y) * 32, screen_y = (x + y) * 16 - z. A cell is a 64x32 diamond.
A ground tile sprite is 64x32 with the cell's top corner at (32, 0); its anchor (ground centre) is (32, 16).

Ground tiles are rendered per pixel: the pixel centre is projected back to cell coordinates (u = x, v = y in
[0, 1)) and a scene function returns colours. Raised surfaces (kerbs, medians, platforms) use a small
height-field raycast with the sample clamped to the cell, so neighbouring tiles agree at shared edges.
"""
import os
import struct
import zlib

import numpy as np

TW, TH = 64, 32
HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.dirname(HERE)
OUT = os.path.join(MOD, "design", "iso", "net")


# --------------------------------------------------------------------------- colours
def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def scale(c, f):
    return np.clip(np.asarray(c, dtype=np.float32) * f, 0, 255)


def lerp(a, b, t):
    a = np.asarray(a, dtype=np.float32)
    b = np.asarray(b, dtype=np.float32)
    return a + (b - a) * t


# --------------------------------------------------------------------------- PNG io (numpy, no PIL)
def save_png(path, img):
    """img: HxWx4 uint8 (alpha forced to 0/255)."""
    img = np.asarray(img)
    if img.dtype != np.uint8:
        img = np.clip(np.rint(img), 0, 255).astype(np.uint8)
    img = img.copy()
    a = img[..., 3]
    img[..., 3] = np.where(a >= 128, 255, 0)
    img[img[..., 3] == 0] = 0
    h, w = img.shape[:2]
    raw = b"".join(b"\x00" + img[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def load_png(path):
    """Minimal reader for 8-bit RGBA/RGB non-interlaced PNGs (what we and isokit write)."""
    with open(path, "rb") as f:
        data = f.read()
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        n = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + n]
        if tag == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif tag == b"IDAT":
            idat += body
        pos += 12 + n
    ch = 4 if ct == 6 else 3
    raw = zlib.decompress(idat)
    out = np.zeros((h, w * ch), dtype=np.uint8)
    stride = w * ch
    prev = np.zeros(stride, dtype=np.int32)
    i = 0
    for y in range(h):
        ft = raw[i]
        line = np.frombuffer(raw[i + 1:i + 1 + stride], dtype=np.uint8).astype(np.int32)
        i += 1 + stride
        cur = np.zeros(stride, dtype=np.int32)
        for x in range(stride):
            a = cur[x - ch] if x >= ch else 0
            b = prev[x]
            c = prev[x - ch] if x >= ch else 0
            if ft == 0:
                p = 0
            elif ft == 1:
                p = a
            elif ft == 2:
                p = b
            elif ft == 3:
                p = (a + b) // 2
            else:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
            cur[x] = (line[x] + p) & 255
        out[y] = cur
        prev = cur
    img = out.reshape(h, w, ch)
    if ch == 3:
        img = np.concatenate([img, np.full((h, w, 1), 255, np.uint8)], axis=2)
    return img


# --------------------------------------------------------------------------- canvases
def blank(w, h):
    return np.zeros((h, w, 4), dtype=np.float32)


def over(dst, src, dx, dy):
    """Paste src (alpha 0/255) onto dst at (dx, dy), clipped."""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    x0, y0 = max(0, dx), max(0, dy)
    x1, y1 = min(W, dx + w), min(H, dy + h)
    if x0 >= x1 or y0 >= y1:
        return dst
    s = src[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
    m = s[..., 3] >= 128
    d = dst[y0:y1, x0:x1]
    d[m] = s[m]
    return dst


def upscale(img, k):
    return np.repeat(np.repeat(img, k, axis=0), k, axis=1)


# --------------------------------------------------------------------------- projection
def tile_grid(z=0.0, w=TW, h=TH, ox=32, oy=0):
    """Cell coordinates (u, v) of every pixel centre of a w x h sprite whose cell top corner is at (ox, oy),
    looking at the plane at height z (px). Returns u, v, inside-mask (pixel centre within the diamond)."""
    py, px = np.mgrid[0:h, 0:w].astype(np.float64)
    sx = px + 0.5 - ox
    sy = py + 0.5 - oy + z
    a = sx / 32.0
    b = sy / 16.0
    u = (a + b) / 2.0
    v = (b - a) / 2.0
    inside = (u >= 0) & (u < 1) & (v >= 0) & (v < 1)
    return u, v, inside


def hash2(u, v, seed=0):
    """Deterministic per-position hash in [0, 1) from cell coordinates (quantised to 1/256)."""
    iu = np.floor(np.asarray(u) * 256).astype(np.int64)
    iv = np.floor(np.asarray(v) * 256).astype(np.int64)
    h = (iu * 73856093) ^ (iv * 19349663) ^ (seed * 83492791)
    h = (h ^ (h >> 13)) * 1274126177
    h = h ^ (h >> 16)
    return (h & 0xFFFF) / 65536.0


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) / 16.0


def bayer(h, w, ox=0, oy=0):
    py, px = np.mgrid[0:h, 0:w]
    return BAYER4[(py + oy) % 4, (px + ox) % 4]


def render_ground(scene, raise_px=1, w=TW, h=TH, ox=32, oy=0):
    """Render one ground tile. scene(u, v) -> (rgb[N,3], alpha[N] bool, raised[N] bool, normal_x[N], normal_y[N])
    evaluated on flat arrays. Raised cells are drawn raise_px higher with a kerb face (lit for +Y facing,
    shaded for +X facing). Returns an HxWx4 float image."""
    img = blank(w, h)
    u0, v0, in0 = tile_grid(0, w, h, ox, oy)
    u1, v1, _ = tile_grid(raise_px, w, h, ox, oy)
    eps = 1e-6
    u1c = np.clip(u1, 0, 1 - eps)
    v1c = np.clip(v1, 0, 1 - eps)
    m = in0
    U0, V0 = u0[m], v0[m]
    c0, a0, r0, _, _ = scene(U0, V0)
    c1, a1, r1, _, _ = scene(u1c[m], v1c[m])
    out = c0.copy()
    alpha = a0.copy()
    top = r1 & a1
    out[top] = c1[top]
    alpha[top] = True
    face = (~top) & r0
    if face.any():
        # kerb face: estimate the outward normal of the raised region at p0 from a probe pattern
        d = 0.02
        rx = scene(U0[face] + d, V0[face])[2]
        ry = scene(U0[face], V0[face] + d)[2]
        base = c0[face]
        lit = scale(base, 0.86)
        dark = scale(base, 0.62)
        # region ends toward +X (not raised at +d in u) -> +X facing face: shaded; else +Y facing: lit
        f = np.where((~rx)[:, None], dark, np.where((~ry)[:, None], lit, scale(base, 0.75)))
        out[face] = f
    rgba = np.zeros((m.sum(), 4), dtype=np.float32)
    rgba[:, :3] = out
    rgba[:, 3] = np.where(alpha, 255, 0)
    img[m] = rgba
    return img


def diamond_mask(w=TW, h=TH, ox=32, oy=0):
    return tile_grid(0, w, h, ox, oy)[2]


def _grid_z(z0, dz, axis, w, h, ox, oy):
    """Cell coords of the pixel centres of a plane z = z0 + dz * (v or u) (px). Returns u, v, inside."""
    py, px = np.mgrid[0:h, 0:w].astype(np.float64)
    a = (px + 0.5 - ox) / 32.0
    b = (py + 0.5 - oy) / 16.0
    k = 2.0 - dz / 16.0
    if axis == "v":
        v = (b - a + z0 / 16.0) / k
        u = a + v
    else:
        u = (b + a + z0 / 16.0) / k
        v = u - a
    inside = (u >= 0) & (u < 1) & (v >= 0) & (v < 1)
    return u, v, inside


def render_ground_z(scene, z0=0.0, z1=None, axis="v", H=16, raise_px=1):
    """Ground tile lifted to height z0 (px), or sloped from z0 at the u/v = 0 edge to z1 at the 1 edge.
    Canvas 64 x (32 + H); the z = 0 diamond's top corner is at (32, H), so the anchor is (32, H + 16)."""
    w, h, ox, oy = TW, TH + H, 32, H
    dz = 0.0 if z1 is None else (z1 - z0)
    img = blank(w, h)
    u0, v0, in0 = _grid_z(z0, dz, axis, w, h, ox, oy)
    u1, v1, _ = _grid_z(z0 + raise_px, dz, axis, w, h, ox, oy)
    eps = 1e-6
    m = in0
    c0, a0, r0, _, _ = scene(u0[m], v0[m])
    c1, a1, r1, _, _ = scene(np.clip(u1, 0, 1 - eps)[m], np.clip(v1, 0, 1 - eps)[m])
    out = c0.copy()
    alpha = a0.copy()
    top = r1 & a1
    out[top] = c1[top]
    alpha[top] = True
    face = (~top) & r0
    out[face] = scale(c0[face], 0.7)
    rgba = np.zeros((m.sum(), 4), np.float32)
    rgba[:, :3] = out
    rgba[:, 3] = np.where(alpha, 255, 0)
    img[m] = rgba
    return img


def render_flat(fn, w=TW, h=TH, ox=32, oy=0):
    """Flat overlay tile with pixel coordinates available (for screen-space dithering).
    fn(u, v, px, py) -> (rgb[N,3], alpha[N] bool) on the pixels inside the diamond."""
    img = blank(w, h)
    u, v, m = tile_grid(0, w, h, ox, oy)
    py, px = np.mgrid[0:h, 0:w]
    rgb, a = fn(u[m], v[m], px[m], py[m])
    out = np.zeros((m.sum(), 4), np.float32)
    out[:, :3] = rgb
    out[:, 3] = np.where(a, 255, 0)
    img[m] = out
    return img


def rotate_scene(scene, k):
    """Rotate a cell-space scene by k quarter turns clockwise (N->E->S->W)."""
    for _ in range(k % 4):
        scene = (lambda s: (lambda u, v, *rest: s(v, 1 - u, *rest)))(scene)
    return scene


def write_manifest_fragment(path, groups):
    """Each NET module writes design/iso/net/_frag/<module>.json (a list of manifest groups);
    iso_net.py merges them in order into manifest.json."""
    import json
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(groups, f, indent=1)
