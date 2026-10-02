"""
iso_net_kit.py - NET glue around isokit: NET materials (signal lenses, lamp glass, paint), fixed-size canvases
with a known anchor, screen-space line drawing (wires), and night conversion for NET's own ground tiles.

Fixed canvas convention for NET props: a 1x1 prop sprite is 64 x (32 + H) with the cell's ground centre at
(32, H + 16); a w x h footprint sprite is (w + h) * 32 wide.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.palette import PALETTE, RAMP, SHADES  # noqa: E402
from isokit.render import night_color  # noqa: E402


# --------------------------------------------------------------------------- materials
def _pat_glow(c, m):
    """Self-lit surface: fixed shade by day (ignores light), emissive at night."""
    c.ramp[:] = RAMP[m.p("glow", "yellow")]
    c.flat[:] = m.p("day", 9.0)
    if m.p("lit", True):
        c.emit[:] = True
        c.eramp[:] = RAMP[m.p("glow", "yellow")]
        c.eshade[:] = m.p("night", 11.0)


def _pat_off(c, m):
    c.flat[:] = m.p("day", 2.0)


def glow(ramp, day=9.0, night=11.0, lit=True):
    return ik.Material(ramp, 0.0, _pat_glow, dither=0.0, snow=False, glow=ramp, day=day, night=night, lit=lit)


def dark_lens(ramp="grey", day=2.0):
    return ik.Material(ramp, 0.0, _pat_off, dither=0.0, snow=False, day=day)


def flat(ramp, shade=0.0, dither=0.0, snow=True):
    return ik.Material(ramp, shade, None, dither=dither, snow=snow)


STEEL = flat("grey", 0.5)
STEEL_DARK = flat("slate", -2.0)
POLE = flat("slate", -1.5)
GALV = flat("grey", 1.5)
SIGN_RED = flat("red", 1.0)
SIGN_WHITE = flat("grey", 3.0, snow=False)
SIGN_BLUE = flat("water", 0.5, snow=False)
SIGN_GREEN = flat("leaf", 0.5, snow=False)
SIGN_YELLOW = flat("yellow", 1.0, snow=False)
CONCRETE = ik.mat("concrete")
GLASS = ik.mat("glass")


# --------------------------------------------------------------------------- canvases
def fixed(spr, w=64, h=96, ax=32, ay=None):
    """Paste an isokit Sprite into a w x h RGBA float canvas with its anchor at (ax, ay)."""
    if ay is None:
        ay = h - 16
    out = np.zeros((h, w, 4), np.float32)
    img = spr.img.astype(np.float32)
    dx, dy = ax - spr.ax, ay - spr.ay
    sh, sw = img.shape[:2]
    x0, y0 = max(0, dx), max(0, dy)
    x1, y1 = min(w, dx + sw), min(h, dy + sh)
    if x0 < x1 and y0 < y1:
        s = img[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
        m = s[..., 3] >= 128
        d = out[y0:y1, x0:x1]
        d[m] = s[m]
    return out


def render_fixed(scene, w=64, h=96, ax=32, ay=None, lit_only=False, **kw):
    """Render into a fixed canvas. At night, emissive pixels keep their full colour (the 'edge' outline would
    darken tiny lenses); lit_only=True returns just the emissive pixels (the engine's '-lit' companion frame)."""
    spr = ik.render(scene, crop=False, **kw)
    if kw.get("night"):
        plain = ik.render(scene, crop=False, **dict(kw, outline=None))
        e = plain.emit & (plain.img[..., 3] > 0)
        spr.img[e] = plain.img[e]
        if lit_only:
            spr.img[~e] = 0
    return fixed(spr, w, h, ax, ay)


def screen(x, y, z, ax, ay):
    """Screen pixel of cell-local world point (x, y in [0, 1], z px); the ground centre (0.5, 0.5, 0) is (ax, ay)."""
    return ax + (x - y) * 32.0, ay + (x + y - 1.0) * 16.0 - z


def wire(img, p0, p1, sag, col, ax, ay, steps=None):
    """Draw a sagging wire between world points p0, p1 = (x, y, z) (cells, cells, px) onto img (in place)."""
    x0, y0, z0 = p0
    x1, y1, z1 = p1
    a = screen(x0, y0, z0, ax, ay)
    b = screen(x1, y1, z1, ax, ay)
    n = steps or int(max(abs(b[0] - a[0]), abs(b[1] - a[1])) * 2 + 2)
    h, w = img.shape[:2]
    for i in range(n + 1):
        t = i / n
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        z = z0 + (z1 - z0) * t - sag * 4 * t * (1 - t)
        sx, sy = screen(x, y, z, ax, ay)
        px, py = int(np.floor(sx)), int(np.floor(sy))
        if 0 <= px < w and 0 <= py < h:
            img[py, px, :3] = col
            img[py, px, 3] = 255


# --------------------------------------------------------------------------- night for NET ground tiles
_FLAT = PALETTE.reshape(-1, 3).astype(np.float64)


def night_tile(img, pools=None):
    """Night version of a palette-quantized RGBA tile (same rule as isokit: idx*0.68-1.3 + night tint).
    pools: optional HxW float 0..1 lamp light (brightens back toward day, warm)."""
    out = np.asarray(img, np.float32).copy()
    m = out[..., 3] >= 128
    if not m.any():
        return out
    rgb = out[m, :3].astype(np.float64)
    w = np.array([0.30, 0.59, 0.11])
    d = (((rgb[:, None, :] - _FLAT[None]) ** 2) * w).sum(-1)
    k = np.argmin(d, 1)
    r, s = k // SHADES, k % SHADES
    idx = np.clip(np.round(s * 0.68 - 1.3), 0, SHADES - 1).astype(int)
    col = night_color(PALETTE[r, idx], r)
    if pools is not None:
        p = pools[m]
        lit_idx = np.clip(np.round(idx + p * (s - idx + 1.2)), 0, SHADES - 1).astype(int)
        lit = PALETTE[r, lit_idx].astype(np.float64)
        warm = (p > 0.35)
        lit[warm] = ik.nearest(lit[warm] * np.array([1.08, 1.0, 0.82]))
        col = np.where((p > 0.08)[:, None], lit, col)
    out[m, :3] = col
    return out


def winter_tile(img, seed=0):
    """Snow version of a palette-quantized NET ground tile: paving, verges, grass and gravel turn to snow
    (keeping their light level), asphalt stays clear with a little slush speckle."""
    out = np.asarray(img, np.float32).copy()
    m = out[..., 3] >= 128
    if not m.any():
        return out
    rgb = out[m, :3].astype(np.float64)
    w = np.array([0.30, 0.59, 0.11])
    d = (((rgb[:, None, :] - _FLAT[None]) ** 2) * w).sum(-1)
    k = np.argmin(d, 1)
    r, s = k // SHADES, k % SHADES
    snowy = np.isin(r, [RAMP[n] for n in ("stone", "grass", "leaf", "sand", "olive")])
    col = PALETTE[r, s].astype(np.float64)
    sn = np.clip(s + 1, 7, 11)
    col[snowy] = PALETTE[RAMP["snow"], sn[snowy]]
    yy, xx = np.nonzero(m)
    hsh = ((xx * 73856093) ^ (yy * 19349663) ^ (seed * 83492791)) & 255
    slush = (~snowy) & (r == RAMP["grey"]) & (s <= 4) & (hsh < 20)
    col[slush] = PALETTE[RAMP["snow"], 7]
    out[m, :3] = col
    return out


def quantize(img):
    img = np.asarray(img, np.float32).copy()
    m = img[..., 3] >= 128
    if m.any():
        img[m, :3] = ik.nearest(img[m, :3])
    return img
