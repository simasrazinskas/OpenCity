"""iso_life_mdraw - small pixel drawing helpers for the LIFE markers generator (numpy only)."""
import numpy as np

from iso_life_common import canvas, blit, hexrgb, rgba

KEYS = {}   # single-char colour keys (set by glyph modules)
BAYER4 =np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]])


def col(c):
    """hex string / tuple / rgba array -> uint8 RGBA."""
    if isinstance(c, str):
        if len(c) == 1 and c in KEYS:
            c = KEYS[c]
        return rgba(hexrgb(c))
    c = np.asarray(c)
    if len(c) == 4:
        return c.astype(np.uint8)
    return rgba(c)


def px(img, x, y, c):
    h, w = img.shape[:2]
    x, y = int(x), int(y)
    if 0 <= x < w and 0 <= y < h:
        img[y, x] = col(c)


def rect(img, x0, y0, x1, y1, c):
    """Inclusive rectangle fill."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(img, x, y, c)


def line(img, x0, y0, x1, y1, c):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx + dy
    while True:
        px(img, x0, y0, c)
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def ellipse_mask(w, h, cx, cy, rx, ry):
    ys, xs = np.mgrid[0:h, 0:w]
    return ((xs - cx) / rx) ** 2 + ((ys - cy) / ry) ** 2 <= 1.0


def fill_mask(img, mask, c):
    img[mask] = col(c)


def outline_mask(mask, diag=False):
    """Pixels 4-adjacent (or 8) to mask but not in it."""
    m = mask
    n = np.zeros_like(m)
    n[1:] |= m[:-1]
    n[:-1] |= m[1:]
    n[:, 1:] |= m[:, :-1]
    n[:, :-1] |= m[:, 1:]
    if diag:
        n[1:, 1:] |= m[:-1, :-1]
        n[:-1, :-1] |= m[1:, 1:]
        n[1:, :-1] |= m[:-1, 1:]
        n[:-1, 1:] |= m[1:, :-1]
    return n & ~m


def add_outline(img, c, diag=False, pad=1):
    """Pad image and add 1 px outline around opaque pixels."""
    h, w = img.shape[:2]
    out = canvas(w + 2 * pad, h + 2 * pad)
    out[pad:pad + h, pad:pad + w] = img
    m = out[..., 3] > 0
    o = outline_mask(m, diag)
    out[o] = col(c)
    return out


def art(rows, pal):
    """ASCII art -> RGBA. '.' or ' ' transparent; other chars looked up in pal (hex or rgba)."""
    h = len(rows)
    w = max(len(r) for r in rows)
    img = canvas(w, h)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in ". ":
                continue
            img[y, x] = col(pal[ch])
    return img


def diamond_mask(w, h, cx, cy, hw, hh):
    """Pixel mask of a 2:1 diamond centred at (cx, cy) with half-extents hw, hh (pixel centres)."""
    ys, xs = np.mgrid[0:h, 0:w]
    return (np.abs(xs + 0.5 - cx) / hw + np.abs(ys + 0.5 - cy) / hh) <= 1.0


def shade(c, f):
    c = col(c).astype(float)
    c[:3] = np.clip(c[:3] * f, 0, 255)
    return c.astype(np.uint8)


def mix(a, b, t):
    a, b = col(a).astype(float), col(b).astype(float)
    r = a * (1 - t) + b * t
    r[3] = 255
    return r.astype(np.uint8)


def flip_h(img):
    return img[:, ::-1].copy()


def tile_blit(dst, src, x, y):
    blit(dst, src, x, y)
