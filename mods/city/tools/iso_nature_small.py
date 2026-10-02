"""Small nature props: bushes, hedges, flower beds, rocks, tall grass, reeds, logs, groves."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402

M = ik.mat
LEAF = {"spring": ("grass", -0.3), "summer": ("leaf", 0.2), "autumn": ("terra", -1.2), "winter": ("wood", -1.5)}


def _fol(season, ramp=None, shade=None, **kw):
    r, s = LEAF[season]
    return M("foliage", ramp=ramp or r, shade=s if shade is None else shade, **kw)


def bush(size=1.0, season="summer", seed=0, flowers=None):
    s = ik.Scene((1, 1), seed)
    rng = ik.Rng(seed + 50)
    r = 0.14 * size
    extra = dict(accent=flowers, accent_amt=0.22, accent_tone=1.6) if flowers and season in ("spring", "summer") else {}
    if season == "winter" and not flowers == "ever":
        for i in range(7):
            a = 2 * math.pi * i / 7
            s.limb((0.5, 0.5, 0), (0.5 + math.cos(a) * r, 0.5 + math.sin(a) * r, 9 * size), 0.012, 0.008, "bark")
        return s
    parts = [(0.5, 0.5, 3.5 * size, r, 4.5 * size)]
    for i in range(6):
        a = rng.uniform(0, 2 * math.pi)
        parts.append((0.5 + math.cos(a) * r * 0.55, 0.5 + math.sin(a) * r * 0.55, rng.uniform(3, 6) * size,
                      r * rng.uniform(0.5, 0.65), 3.8 * size))
    s.blob(parts, _fol(season, **extra), rough=0.3, rough_scale=1.3)
    return s


def hedge(kind="x", season="summer", seed=0):
    """Hedge piece: 'x' (along X), 'y', 'corner', 'end'. Fills its cell edge-to-edge for joining."""
    s = ik.Scene((1, 1), seed)
    fol = _fol(season if season != "winter" else "summer", ramp="leaf", shade=-0.5)
    rng = ik.Rng(seed + 9)
    segs = []
    if kind in ("x", "corner"):
        segs.append(((0.0, 0.5), (1.0, 0.5)) if kind == "x" else ((0.5, 0.5), (1.0, 0.5)))
    if kind in ("y", "corner"):
        segs.append(((0.5, 0.0), (0.5, 1.0)) if kind == "y" else ((0.5, 0.5), (0.5, 1.0)))
    if kind == "end":
        segs.append(((0.5, 0.5), (1.0, 0.5)))
    for (x0, y0), (x1, y1) in segs:
        bx0, bx1 = min(x0, x1), max(x0, x1)
        by0, by1 = min(y0, y1), max(y0, y1)
        s.box(bx0 - 0.08 if bx0 > 0 else 0, by0 - 0.08 if by0 > 0 else 0,
              0, max(bx1 - bx0, 0) + (0.16 if bx1 - bx0 < 0.9 and bx1 - bx0 > 0 else (0.16 if bx1 == bx0 else 0)),
              max(by1 - by0, 0) + (0.16 if by1 - by0 < 0.9 and by1 - by0 > 0 else (0.16 if by1 == by0 else 0)), 8, fol)
        n = 9
        for i in range(n + 1):
            t = i / n
            s.ellipsoid(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 8 + rng.uniform(-0.5, 0.5), 0.085, 0.085, 3.5, fol,
                        rough=0.3, rough_scale=1.2)
    return s


def flower_bed(colors=("rose", "yellow"), shape="rect", seed=0, season="summer"):
    s = ik.Scene((1, 1), seed)
    soil = M("dirt", shade=-1.2)
    flw = M("meadow", ramp="leaf", shade=-0.5, flowers=0.0)
    if shape == "rect":
        s.box(0.2, 0.25, 0, 0.6, 0.5, 2, M("stone", shade=1.0), top=soil)
        s.box(0.24, 0.29, 2, 0.52, 0.42, 1, soil, top=soil)
        pts = [(0.28 + 0.44 * (i % 5) / 4, 0.33 + 0.34 * (i // 5) / 3) for i in range(20)]
    else:
        s.cylinder(0.5, 0.5, 0, 0.3, 2, M("stone", shade=1.0), top=soil, segs=24)
        pts = [(0.5 + 0.22 * math.cos(a) * rr, 0.5 + 0.22 * math.sin(a) * rr)
               for rr in (0.35, 1.0) for a in [2 * math.pi * k / (6 if rr < 1 else 12) for k in range(6 if rr < 1 else 12)]]
    if season == "winter":
        return s
    for i, (x, y) in enumerate(pts):
        col = colors[i % len(colors)]
        s.ellipsoid(x, y, 4, 0.035, 0.035, 2.2, flw, rough=0.25)
        if season in ("spring", "summer"):
            s.ellipsoid(x, y, 5.5, 0.022, 0.022, 1.5, M("plain", ramp=col, shade=2.0, snow=False))
    return s


def boulder(size=1.0, seed=0, n=1):
    s = ik.Scene((1, 1), seed)
    rng = ik.Rng(seed + 3)
    for i in range(n):
        dx, dy = (0, 0) if i == 0 else (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))
        sc = size * (1.0 if i == 0 else rng.uniform(0.35, 0.6))
        s.rock(0.5 + dx, 0.5 + dy, 0, 0.16 * sc, 11 * sc, M("rock", shade=-0.6), seed=seed * 7 + i)
    return s


def tall_grass(season="summer", seed=0, reeds=False):
    s = ik.Scene((1, 1), seed)
    rng = ik.Rng(seed + 21)
    ramp, sh = {"spring": ("grass", -0.5), "summer": ("grass", -1.0), "autumn": ("olive", 0.0),
                "winter": ("sand", -1.0)}[season]
    blade = M("plain", ramp=ramp, shade=sh)
    for i in range(16 if not reeds else 12):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0, 0.12)
        bx, by = 0.5 + math.cos(a) * r, 0.5 + math.sin(a) * r
        h = rng.uniform(7, 12) * (1.5 if reeds else 1.0)
        lean = rng.uniform(0.02, 0.06)
        tx, ty = bx + math.cos(a) * lean, by + math.sin(a) * lean
        s.limb((bx, by, 0), (tx, ty, h), 0.012, 0.006, blade)
        if reeds and i % 3 == 0:
            s.ellipsoid(tx, ty, h - 1.5, 0.018, 0.018, 2.5, M("plain", ramp="wood", shade=-1.0))
    return s


def log(seed=0):
    s = ik.Scene((1, 1), seed)
    s.cylinder(0.2, 0.55, 3, 0.07, 0.6, "bark", top=M("wood", shade=2.0), axis="x", segs=10)
    return s


def grove(kind="deciduous", season="summer", seed=0):
    """A 1x1 cell of 3-4 trees (forests are painted with these)."""
    s = ik.Scene((1, 1), seed)
    rng = ik.Rng(seed + 77)
    spots = [(0.3, 0.3), (0.72, 0.36), (0.38, 0.72), (0.74, 0.76)]
    pools = {"deciduous": ["oak", "linden", "maple", "birch"], "conifer": ["spruce", "fir", "pine", "spruce"],
             "mixed": ["spruce", "oak", "birch", "fir"]}[kind]
    for i, (x, y) in enumerate(spots):
        if i == 3 and rng.chance(0.4):
            continue
        sp = pools[(i + seed) % len(pools)]
        ik.add_tree(s, x + rng.uniform(-0.05, 0.05), y + rng.uniform(-0.05, 0.05), sp, season,
                    stage=2 if rng.chance(0.7) else 1, seed=seed * 5 + i)
    return s
