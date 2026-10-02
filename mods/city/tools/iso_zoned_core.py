"""iso_zoned_core.py - registry, seeding, rendering and footprint clipping for the ZONED generators."""
import zlib

import numpy as np

import isokit as ik
from iso_zoned_lot import Lot
import iso_zoned_build as B

VARIANTS = 4          # v0-v1 North American theme, v2-v3 European theme
LEVELS = 5

# zone prefix -> (title, footprints, model module attribute)
ZONES = {}


def register(prefix, title, footprints, fn):
    ZONES[prefix] = (title, footprints.split(), fn)


def seed_of(*parts):
    return zlib.crc32("/".join(str(p) for p in parts).encode()) & 0x7FFFFFFF


def dims(fp):
    a, b = fp.split("x")
    return int(a), int(b)


def build(prefix, fp, level=1, var=0, state="ok", stage=0, season="summer"):
    """Model scene of zone `prefix`, footprint 'WxD' (W along X = frontage), front facing +Y."""
    W, D = dims(fp)
    # the same variant keeps its seed across levels and states so a building visibly upgrades
    lot = Lot(W, D, level, var, state, stage, seed_of(prefix, fp, var), season)
    ZONES[prefix][2](lot)
    return B.finish(lot)


def clip_footprint(spr):
    """Engine constraint: nothing may poke past the footprint diamond's left/right screen corners.
    Returns the number of clipped pixels (should be 0; reported by the driver)."""
    W, D = spr.footprint
    half = (W + D) * 16
    x0, x1 = spr.ax - half, spr.ax + half
    a = spr.img[..., 3] > 0
    cols = np.arange(spr.img.shape[1])
    bad = a & ((cols < x0) | (cols >= x1))[None, :]
    n = int(bad.sum())
    if n:
        spr.img[bad] = 0
        spr.emit[bad] = False
    return n


CLIPPED = [0]


def render(scene, facing=0, night=False, season="summer"):
    spr = ik.render(scene, facing=facing, night=night, season=season)
    clipped = clip_footprint(spr)
    CLIPPED[0] += clipped
    return spr, clipped


def lit_frame(night_spr):
    """Separate emissive ('-lit') frame: only the lit pixels of the night render, same size/anchor."""
    img = np.zeros_like(night_spr.img)
    img[night_spr.emit] = night_spr.img[night_spr.emit]
    return ik.Sprite(img, night_spr.ax, night_spr.ay, None, night_spr.footprint)


def on_bg(img, bg=(96, 104, 96)):
    out = np.zeros(img.shape[:2] + (3,), np.uint8)
    out[:] = bg
    a = img[..., 3] > 0
    out[a] = img[..., :3][a]
    return out


def sheet(sprites, cols, gap=4, bg=(96, 104, 96)):
    """Anchor-aligned preview grid (bottom aligned by anchor) as RGB."""
    if not sprites:
        return np.zeros((8, 8, 3), np.uint8)
    L = max(s.ax for s in sprites)
    T = max(s.ay for s in sprites)
    R = max(s.w - s.ax for s in sprites)
    Bm = max(s.h - s.ay for s in sprites)
    cw, ch = L + R, T + Bm
    rows = (len(sprites) + cols - 1) // cols
    out = np.zeros((rows * (ch + gap) + gap, cols * (cw + gap) + gap, 3), np.uint8)
    out[:] = bg
    for i, s in enumerate(sprites):
        r, c = divmod(i, cols)
        y0 = gap + r * (ch + gap) + T - s.ay
        x0 = gap + c * (cw + gap) + L - s.ax
        a = s.img[..., 3] > 0
        reg = out[y0:y0 + s.h, x0:x0 + s.w]
        reg[a] = s.img[..., :3][a]
    return out
