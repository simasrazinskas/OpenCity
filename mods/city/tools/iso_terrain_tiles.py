"""iso_terrain_tiles.py - game terrain tiles rendered with KIT's iso terrain code (isokit + iso_terrain_water).

The design set (iso_terrain.py) samples its ground noise in global screen coordinates, which is right for a
tile-by-tile map composition but leaves visible seams when a tile is instanced anywhere in the game. For the
game tileset every low-frequency pattern is therefore made periodic in CELL space (one period = one cell), which
makes any two tiles join seamlessly whatever their neighbours are; the variants add a deviation that is windowed
to zero on the diamond edges. Pixel-level speckles stay hash based (they never create seams).

Importing this module swaps the registered ground patterns (grass, dirt, sand, water, ...) for the periodic ones
(only used by the exporter process).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit import surfaces as S  # noqa: E402
from isokit.materials import REGISTRY  # noqa: E402
from isokit.noise import hash2  # noqa: E402
from isokit.palette import RAMP  # noqa: E402
from isokit.details import paint  # noqa: E402
import iso_terrain_water as W  # noqa: E402

FL = np.floor
VORIG = [(0, 0), (7, 3), (13, 11), (5, 19), (3, 29), (17, 7), (23, 13), (11, 31)]
FRAMES = W.FRAMES


# ---------------------------------------------------------------- periodic noise (period = 1 cell)
def pvalue(u, v, k, seed):
    """Value noise on a k x k lattice that wraps every cell: pvalue(u + 1, v) == pvalue(u, v)."""
    x, y = np.asarray(u, np.float64) * k, np.asarray(v, np.float64) * k
    x0, y0 = FL(x), FL(y)
    fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    i0, j0 = x0.astype(np.int64), y0.astype(np.int64)
    a = hash2(i0 % k, j0 % k, seed)
    b = hash2((i0 + 1) % k, j0 % k, seed)
    c = hash2(i0 % k, (j0 + 1) % k, seed)
    d = hash2((i0 + 1) % k, (j0 + 1) % k, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def pfbm(u, v, ks, seed):
    tot = norm = 0.0
    amp = 1.0
    for o, k in enumerate(ks):
        tot = tot + pvalue(u, v, k, seed + o * 101) * amp
        norm += amp
        amp *= 0.5
    return tot / norm


def window(u, v):
    """1 in the middle of the cell, 0 on the diamond edges (u, v in [0, 1])."""
    uu, vv = np.clip(u, 0, 1), np.clip(v, 0, 1)
    return (4 * uu * (1 - uu)) ** 0.8 * (4 * vv * (1 - vv)) ** 0.8


def field(c, m, ks, seed, var_amp=1.0):
    """Low-frequency field in [0, 1]: a shared periodic part (identical in every tile) plus a variant part
    (param `pseed`) that fades to zero at the tile edges. pseed 0 = the shared field only."""
    base = pfbm(c.x, c.y, ks, seed)
    ps = m.p("pseed", 0)
    if not ps:
        return base
    var = pfbm(c.x, c.y, ks, seed + 1000 + ps * 17)
    w = window(c.x, c.y)
    return np.clip(base + (var - 0.5) * 0.9 * var_amp * w, 0, 1)


# ---------------------------------------------------------------- periodic patterns
def pat_grass_p(c, m):
    p = field(c, m, (4, 8, 16), c.info.get("gseed", 31))
    c.tone += (p - 0.5) * m.p("patch", 2.2)
    h = hash2(c.sx, c.sy, c.info.get("gseed", 31) + 1)
    td = m.p("tuft", 0.07)
    c.tone[h < td] += 1.3
    c.tone[(h > 1 - td) & (h < 1 - td / 3)] -= 1.0
    hb = hash2(c.sx, c.sy + 1, c.info.get("gseed", 31) + 1)
    c.tone[(hb > 1 - td) & (hb < 1 - td / 3)] += 0.7
    flw = m.p("flowers", 0.0)
    if flw:
        hf = hash2(c.sx, c.sy, c.info.get("gseed", 31) + 5)
        fsel = hf < flw
        cols = np.array([RAMP["yellow"], RAMP["rose"], RAMP["snow"], RAMP["purple"]])
        c.ramp[fsel] = cols[(hash2(c.sx, c.sy, 6)[fsel] * len(cols)).astype(int)]
        c.tone[fsel] = 1.6 - m.shade


def pat_dirt_p(c, m):
    p = field(c, m, (6, 12, 24), c.info.get("gseed", 41))
    c.tone += (p - 0.5) * m.p("patch", 1.8)
    h = hash2(c.sx, c.sy, c.info.get("gseed", 41) + 1)
    pd = m.p("pebbles", 0.05)
    c.tone[h < pd] += 1.4
    c.tone[h > 1 - pd] -= 1.2


def pat_sand_p(c, m):
    # ripple phase advances by whole turns along both cell axes (2*pi*5/32 per px) so it tiles
    warp = pvalue(c.x, c.y, 4, 51) * 8.0
    rip = np.sin((c.sx * 0.5 + c.sy * 1.0) * 0.98175 + warp)
    c.tone += rip * m.p("ripple", 0.45) + (field(c, m, (4, 8), 52) - 0.5) * 1.0
    h = hash2(c.sx, c.sy, 53)
    c.tone[h < 0.04] += 1.0
    c.tone[h > 0.97] -= 0.8


def pat_water_p(c, m):
    """pat_water with cell-periodic bands/ripples (same look, same 8-frame loop)."""
    f = c.frame % m.p("frames", 8)
    ph = f / m.p("frames", 8) * 2 * np.pi
    band = field(c, m, (4, 8), 71, 0.6)
    c.tone += (band - 0.5) * 1.2
    warp = pvalue(c.x, c.y, 6, 73) * 6.0
    wv = np.sin((c.sx * 0.5 - c.sy) * 0.58905 + warp - ph)   # 3 turns per 32 px: tiles along both cell axes
    c.tone[wv > 0.82] += 0.9
    c.tone[wv < -0.85] -= 0.6
    gx, gy = FL(c.sx / 12.0), FL(c.sy / 6.0)
    on = hash2(gx, gy, 72)
    jx = (hash2(gx, gy, 74) * 9).astype(np.int64)
    jy = (hash2(gx, gy, 75) * 5).astype(np.int64)
    life = (FL(on * 8) + f).astype(np.int64) % 8
    ln = np.array([1, 3, 4, 2, 0, 0, 0, 0])[life]
    lx = FL(c.sx) - gx * 12 - jx
    gl = (on < m.p("sparkle", 0.45)) & (FL(c.sy) - gy * 6 == jy) & (lx >= 0) & (lx < ln)
    c.tone[gl] += 2.0
    c.flat[gl & (life == 2)] = 10.0


def _swap():
    orig = {S.pat_grass: pat_grass_p, S.pat_dirt: pat_dirt_p, S.pat_sand: pat_sand_p, S.pat_water: pat_water_p}
    for m in REGISTRY.values():
        if m.pattern in orig:
            m.pattern = orig[m.pattern]
    S.pat_grass, S.pat_dirt, S.pat_sand, S.pat_water = pat_grass_p, pat_dirt_p, pat_sand_p, pat_water_p
    W.pat_water = pat_water_p


_swap()


# ---------------------------------------------------------------- ground tiles
def grass(i, flowers=0.0):
    """Grass variant i (0 = plain)."""
    return ik.mat("grass", pseed=i, flowers=flowers)


def tile(material, i, **kw):
    return ik.flat_tile(material, VORIG[i % len(VORIG)], **kw)


def pat_rough(c, m):
    """Rough ground: grass with stones and shrubs (stamps stay inside the tile, edges are plain grass)."""
    pat_grass_p(c, m)
    c.tone -= 0.4
    ps = m.p("pseed", 1)
    nst, nsh = m.p("stones", 3), m.p("shrubs", 2)
    for k in range(nsh):
        cx, cy = 0.2 + 0.6 * hash2(ps, k, 301), 0.2 + 0.6 * hash2(ps, k, 302)
        r = 0.09 + 0.05 * hash2(ps, k, 303)
        d = np.hypot(c.x - cx, c.y - cy) / r
        sel = d < 1.0
        if not sel.any():
            continue
        c.ramp[sel] = RAMP["leaf"]
        lit = -((c.x - cx) + (c.y - cy)) / r             # up-left lit
        c.tone[sel] = -1.2 + lit[sel] * 1.3 - d[sel] * 0.8
        dab = sel & (hash2(c.sx, c.sy, 310 + k) < 0.16)
        c.tone[dab] += 1.4
        sh = (np.hypot(c.x - cx - r * 0.55, c.y - cy - r * 0.55) / (r * 1.05) < 1.0) & ~sel
        c.tone[sh] -= 1.4
    for k in range(nst):
        cx, cy = 0.14 + 0.72 * hash2(ps, k, 321), 0.14 + 0.72 * hash2(ps, k, 322)
        r = 0.04 + 0.03 * hash2(ps, k, 323)
        d = np.hypot(c.x - cx, c.y - cy) / r
        sel = d < 1.0
        if not sel.any():
            continue
        c.ramp[sel] = RAMP["stone"]
        lit = -((c.x - cx) + (c.y - cy)) / r
        c.tone[sel] = 0.2 + lit[sel] * 1.2 - d[sel] * 0.6
        sh = (np.hypot(c.x - cx - r * 0.6, c.y - cy - r * 0.6) / (r * 1.0) < 1.0) & ~sel
        c.tone[sh] -= 1.5


def pat_dirt_patch(c, m):
    """Grass with a ragged bare-earth patch in the middle of the tile."""
    pat_grass_p(c, m)
    ps = m.p("pseed", 1)
    cx, cy = 0.5 + (hash2(ps, 1, 331) - 0.5) * 0.18, 0.5 + (hash2(ps, 2, 332) - 0.5) * 0.18
    rag = pvalue(c.x, c.y, 5, 333 + ps) - 0.5
    d = np.hypot((c.x - cx) * (1.0 + 0.25 * (hash2(ps, 3, 334) - 0.5)), c.y - cy) + rag * 0.16
    r0 = 0.30 + 0.06 * hash2(ps, 4, 335)
    fringe = d < r0 + 0.045
    c.tone[fringe] -= 0.9
    paint(c, ik.mat("dirt", pseed=0), d < r0, m.shade)
    pb = (d < r0) & (hash2(c.sx, c.sy, 336) < 0.05)
    c.tone[pb] += 1.2


def rough_tile(i, **kw):
    m = ik.mat("grass", pattern=pat_rough, pseed=1 + i, stones=2 + i % 3, shrubs=1 + i % 3)
    return tile(m, 1 + i, **kw)


def dirt_tile(i, **kw):
    return tile(ik.mat("grass", pattern=pat_dirt_patch, pseed=1 + i), 2 + i, **kw)


def meadow_tile(i, **kw):
    return tile(grass(1 + i, flowers=0.018 + 0.012 * i), 1 + i, **kw)


def grass_tile(i, **kw):
    return tile(grass(i), i, **kw)


def sand_tile(**kw):
    return tile(ik.mat("sand"), 0, **kw)


def water_depth_tile(depth, variant, frame):
    """Open water tile of a depth (shallow/open/deep), variant 0..3 (own noise field + origin), frame 0..7."""
    s = ik.Scene((1, 1))
    s.ground([(-0.5, -0.5), (1.5, -0.5), (1.5, 1.5), (-0.5, 1.5)],
             W.water_mat(depth, mask=0, corners=0, style="beach", pseed=variant), z=W.WL, layer=5)
    return ik.render_tile(s, VORIG[variant % len(VORIG)], frame=frame)


def shore_water_tile(mask, corners, frame):
    return W.water_tile(mask, corners, "beach", frame, "shallow", (0, 0))


def shore_land_tile(mask, corners, **kw):
    if not kw:
        return W.land_tile(mask, corners, "beach", (0, 0))
    s = ik.Scene((1, 1))
    s.tile(0, 0, ik.mat("grass", pattern=W.pat_shore_land, mask=mask, corners=corners, style="beach"))
    return ik.render_tile(s, (0, 0), **kw)


def snow_tile(i):
    return ik.flat_tile("grass", VORIG[i % len(VORIG)], season="winter")


def flood_tile(frame):
    return ik.flat_tile(ik.mat("water", shade=0.2), VORIG[0], frame=frame)


# ---------------------------------------------------------------- winter versions of every land template
def _cell_uv(h=32, w=64):
    ys, xs = np.mgrid[0:h, 0:w] + 0.5
    sx, sy = (xs - w / 2) / (w / 2), (ys - h / 2) / (h / 2)
    return (sx + sy) / 2 + 0.5, (sy - sx) / 2 + 0.5, xs.astype(np.int64), ys.astype(np.int64)


def light_snow(summer, full, amount, variant):
    """Patchy snow: the summer tile with snow patches taken from the full-snow tile. The patch field repeats every
    cell (shared part) and only varies inside the tile (variant part fades out at the edges), so neighbouring
    tiles of any template join seamlessly."""
    u, v, px, py = _cell_uv(*summer.img.shape[:2])
    base = pfbm(u, v, (2, 4, 8), 7101)
    var = pfbm(u, v, (2, 4, 8), 7301 + variant * 13)
    wv = window(u, v) ** 0.5            # edges stay snowy: joins full-snow and light-snow neighbours alike
    n = np.clip(0.25 * (1 - wv) + wv * (0.62 + (var - 0.5) * 1.4) + (base - 0.5) * 0.15, 0, 1)
    n = n + (hash2(px, py, 7401) - 0.5) * 0.10           # ragged patch edges
    sel = (n < amount) & (summer.img[..., 3] > 0) & (full.img[..., 3] > 0)
    img = summer.img.copy()
    img[sel] = full.img[sel]
    return ik.Sprite(img, summer.ax, summer.ay, None, (1, 1))
