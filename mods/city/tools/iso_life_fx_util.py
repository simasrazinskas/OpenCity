"""
iso_life_fx_util - drawing helpers for the LIFE FX generators (smoke, fire, water, night, weather ...).

Everything is pixel exact: alpha is 0/255, tones come from short ramps, soft edges are ordered-dithered.
Light comes from the upper left (puffs are brightest top-left, darkest bottom-right).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_common import (save_png, canvas, blit, crop, strip, unify, upscale, preview,  # noqa: E402,F401
                             make_ramp, rgba, hexrgb)

BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float64) / 16.0


def ramp(*hexes):
    """Ramp (dark -> light) from explicit hex colours -> list of (r,g,b) tuples."""
    return [tuple(int(v) for v in hexrgb(h)) for h in hexes]


def px(img, x, y, c):
    h, w = img.shape[:2]
    x, y = int(x), int(y)
    if 0 <= x < w and 0 <= y < h:
        img[y, x, :3] = c
        img[y, x, 3] = 255


def hline(img, x0, x1, y, c):
    for x in range(int(x0), int(x1) + 1):
        px(img, x, y, c)


def line(img, x0, y0, x1, y1, c):
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
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


def tone(rp, t, x=0, y=0, dither=True):
    """Pick ramp colour for t in [0,1] (0 dark, 1 light), ordered-dithered between neighbouring steps."""
    n = len(rp)
    v = min(max(t, 0.0), 1.0) * (n - 1)
    i = int(v)
    f = v - i
    if i >= n - 1:
        return rp[-1]
    if dither and f > BAYER4[y & 3, x & 3]:
        return rp[i + 1]
    return rp[i]


def disc(img, cx, cy, r, rp, squash=1.0, shade=1.0, lo=0.0, hi=1.0, cut=0.0):
    """Shaded pixel sphere/puff. Light from the upper left. squash<1 flattens vertically.
    lo/hi limit the ramp range used. cut: ordered-dither erosion of the rim (0..1) for dissolving puffs."""
    r = float(r)
    ry = r * squash
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            dx = (x + 0.5 - cx) / r
            dy = (y + 0.5 - cy) / max(ry, 0.5)
            d2 = dx * dx + dy * dy
            if d2 > 1.0:
                continue
            if cut > 0 and d2 > 1.0 - cut and BAYER4[y & 3, x & 3] < (d2 - (1.0 - cut)) / cut:
                continue
            # light: from upper-left, tone falls off to lower-right
            t = 0.62 - (dx * 0.55 + dy * 0.75) * 0.5 * shade
            t = lo + (hi - lo) * min(max(t, 0.0), 1.0)
            px(img, x, y, tone(rp, t, x, y))


def ellipse_iso(img, cx, cy, rx, c, filled=True, ring=None):
    """Iso (2:1) ellipse: rx wide, rx/2 tall. filled or ring (list of (inner, outer) fractions)."""
    ry = rx / 2.0
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
            if d <= 1.0:
                px(img, x, y, c)


def diamond_mask(w=64, h=32):
    """Boolean mask of the 64x32 iso diamond."""
    m = np.zeros((h, w), bool)
    for y in range(h):
        for x in range(w):
            if abs(x + 0.5 - w / 2) / (w / 2) + abs(y + 0.5 - h / 2) / (h / 2) <= 1.0:
                m[y, x] = True
    return m


DIAMOND = diamond_mask()


def iso_dist(x, y, cx, cy):
    """Normalised distance in an iso 2:1 plane (1.0 at distance cx-units horizontally)."""
    return np.hypot(x - cx, (y - cy) * 2.0)


def pad_even(img):
    """Pad 1 px at right/bottom (transparent) if width or height is odd."""
    h, w = img.shape[:2]
    if w % 2 or h % 2:
        img = np.pad(img, ((0, h % 2), (0, w % 2), (0, 0)))
    return img


def save_anim(root, rel, frames, out_items, label, name=None, gap=0):
    """Write frame PNGs rel_0.png.. and strip rel_strip.png; append strip item to out_items.
    rel e.g. 'fx/smoke/house' -> fx/smoke/house_0.png, fx/smoke/house_strip.png."""
    frames = [f for f in frames]
    h = max(f.shape[0] for f in frames)
    w = max(f.shape[1] for f in frames)
    fixed = []
    for f in frames:
        c = canvas(w, h)
        if f.shape[:2] == (h, w):
            c = f.copy()
        else:
            blit(c, f, (w - f.shape[1]) // 2, h - f.shape[0])
        fixed.append(pad_even(c))
    for i, f in enumerate(fixed):
        save_png(os.path.join(root, "%s_%d.png" % (rel, i)), f)
    s = strip(fixed, gap=gap)
    save_png(os.path.join(root, rel + "_strip.png"), s)
    out_items.append({"file": rel + "_strip.png", "label": "%s, %d frames" % (label, len(fixed))})
    return fixed


def save_still(root, rel, img, out_items, label):
    save_png(os.path.join(root, rel), pad_even(img))
    out_items.append({"file": rel, "label": label})


def prev(scratch, name, img, k=6, bg=(120, 150, 110)):
    os.makedirs(scratch, exist_ok=True)
    preview(os.path.join(scratch, name), img, k, bg)


def composite_frames(frames, bg=(120, 150, 110), k=4, gap=2):
    """Strip of frames on a flat bg, upscaled (for inspection)."""
    s = strip(frames, gap=gap)
    return s


# ----------------------------------------------------------------------------- tile-periodic noise
def tile_uv(x, y, w=64, h=32):
    """Pixel -> world cell coordinates (u, v) in [0,1] across a diamond tile (u along +X, v along +Y)."""
    sx, sy = x + 0.5 - w / 2.0, y + 0.5 - h / 2.0
    u = (sx / 32.0 + sy / 16.0) / 2.0 + 0.5
    v = (sy / 16.0 - sx / 32.0) / 2.0 + 0.5
    return u, v


_WAVES = {}


def _waves(seed):
    if seed not in _WAVES:
        rs = np.random.RandomState(seed)
        ws = []
        for _ in range(7):
            km = (rs.randint(-3, 4), rs.randint(-3, 4))
            if km == (0, 0):
                km = (1, 2)
            ws.append((km[0], km[1], rs.rand(), 0.5 + rs.rand()))
        _WAVES[seed] = ws
    return _WAVES[seed]


def pnoise(u, v, seed=1, phase=0.0):
    """Smooth noise 0..1, periodic with period 1 in u and v (integer wavenumbers) -> tiles seamlessly.
    `phase` in cycles; integer phase loops (animation friendly)."""
    s, tot = 0.0, 0.0
    for a, b, p, amp in _waves(seed):
        s += amp * np.cos(2 * np.pi * (a * u + b * v + p + phase))
        tot += amp
    return 0.5 + 0.5 * s / tot * 1.9


def pn_clip(x):
    return min(max(x, 0.0), 1.0)
