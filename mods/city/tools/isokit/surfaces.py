"""Roof, ground, water and foliage patterns."""
import numpy as np

from .noise import hash2, value2, fbm
from .palette import RAMP
from .materials import _fl, _speckle


# ---------------------------------------------------------------- roofs
def pat_tiles(c, m):
    """Clay roof tiles: courses up the slope with a shadow line, staggered joints, per-tile tone."""
    cr = m.p("course", 3)
    tw = m.p("tile", 4)
    v = _fl(c.v)
    row = v // cr
    off = (row % 2) * (tw // 2)
    col = _fl(c.u + off) // tw
    c.tone += (hash2(col, row, c.seed + 11) - 0.5) * 0.9
    c.tone[(v % cr) == 0] -= 1.1
    c.tone[((_fl(c.u + off) % tw) == 0) & ((v % cr) != 0)] -= 0.45
    c.tone[(v % cr) == cr - 1] += 0.35


def pat_slate(c, m):
    cr = m.p("course", 2)
    v = _fl(c.v)
    row = v // cr
    off = (row % 2) * 2
    col = _fl(c.u + off) // 3
    c.tone += (hash2(col, row, c.seed + 13) - 0.5) * 1.0
    c.tone[(v % cr) == 0] -= 0.8


def pat_shingle(c, m):
    pat_slate(c, m)
    _speckle(c, 0.3, 14)


# ---------------------------------------------------------------- ground (top faces: u = x*32, v = y*32)
def pat_asphalt(c, m):
    h = hash2(c.sx, c.sy, c.seed * 0 + 21)
    c.tone += (value2(c.sx, c.sy, 7.0, 22) - 0.5) * 0.6
    c.tone[h < 0.08] += 0.8
    c.tone[h > 0.95] -= 0.7


def pat_paving(c, m):
    s = m.p("slab", 4)
    u, v = _fl(c.u), _fl(c.v)
    c.tone += (hash2(u // s, v // s, 23) - 0.5) * 0.6
    c.tone[((u % s) == 0) | ((v % s) == 0)] -= 0.9


def pat_grass(c, m):
    """Grass: soft patches + tuft speckles. Params: patch (amp), tuft (density)."""
    p = fbm(c.sx, c.sy * 2.0, m.p("scale", 14.0), 3, c.info.get("gseed", 31))
    c.tone += (p - 0.5) * m.p("patch", 2.2)
    h = hash2(c.sx, c.sy, c.info.get("gseed", 31) + 1)
    td = m.p("tuft", 0.07)
    c.tone[h < td] += 1.3
    c.tone[(h > 1 - td) & (h < 1 - td / 3)] -= 1.0
    # tiny vertical blade pairs: pixel above a dark one gets light
    hb = hash2(c.sx, c.sy + 1, c.info.get("gseed", 31) + 1)
    c.tone[(hb > 1 - td) & (hb < 1 - td / 3)] += 0.7
    flw = m.p("flowers", 0.0)
    if flw:
        hf = hash2(c.sx, c.sy, c.info.get("gseed", 31) + 5)
        fsel = hf < flw
        cols = np.array([RAMP["yellow"], RAMP["rose"], RAMP["snow"], RAMP["purple"]])
        c.ramp[fsel] = cols[(hash2(c.sx, c.sy, 6)[fsel] * len(cols)).astype(int)]
        c.tone[fsel] = 1.6 - m.shade


def pat_dirt(c, m):
    p = fbm(c.sx, c.sy * 2.0, m.p("scale", 10.0), 3, c.info.get("gseed", 41))
    c.tone += (p - 0.5) * m.p("patch", 1.8)
    h = hash2(c.sx, c.sy, c.info.get("gseed", 41) + 1)
    pd = m.p("pebbles", 0.05)
    c.tone[h < pd] += 1.4
    c.tone[h > 1 - pd] -= 1.2


def pat_sand(c, m):
    rip = np.sin((c.sx * 0.5 + c.sy * 1.0 + value2(c.sx, c.sy, 9.0, 51) * 8.0) * 0.9)
    c.tone += rip * m.p("ripple", 0.45) + (value2(c.sx, c.sy, 12.0, 52) - 0.5) * 1.0
    h = hash2(c.sx, c.sy, 53)
    c.tone[h < 0.04] += 1.0
    c.tone[h > 0.97] -= 0.8


def pat_rock(c, m):
    """Rock: plates (cellular-ish value steps) with dark cracks and lit upper rims."""
    p = fbm(c.sx, c.sy * 2.0, 6.0, 3, c.info.get("gseed", 61))
    q = value2(c.sx, c.sy * 2.0, 7.0, c.info.get("gseed", 62))
    plate = np.floor(q * 5)
    c.tone += (p - 0.5) * 1.6 + (hash2(plate, 0, 63) - 0.5) * 1.4
    edge = np.floor(value2(c.sx, c.sy * 2.0 - 2, 7.0, c.info.get("gseed", 62)) * 5) != plate
    c.tone[edge] -= 1.8
    up = np.floor(value2(c.sx, c.sy * 2.0 + 2, 7.0, c.info.get("gseed", 62)) * 5) != plate
    c.tone[up & ~edge] += 1.0


def pat_forest_floor(c, m):
    """Leaf litter and needles: brown base, green moss patches, dark twigs, light leaves."""
    p = fbm(c.sx, c.sy * 2.0, 9.0, 3, 65)
    c.tone += (p - 0.5) * 1.6
    moss = fbm(c.sx, c.sy * 2.0, 12.0, 2, 66) > 0.56
    c.ramp[moss] = RAMP["olive"]
    c.tone[moss] -= 0.6
    h = hash2(c.sx, c.sy, 67)
    c.tone[h < 0.08] += 1.5
    c.tone[h > 0.92] -= 1.5
    lv = hash2(c.sx // 2, c.sy, 68) < 0.04
    c.ramp[lv] = RAMP["terra"]


def pat_water(c, m):
    """Water: slow colour bands + drifting horizontal glints. c.frame animates (8-frame loop)."""
    f = c.frame % m.p("frames", 8)
    ph = f / m.p("frames", 8) * 2 * np.pi
    band = fbm(c.sx * 0.5, c.sy, 16.0, 2, 71)
    c.tone += (band - 0.5) * 1.2
    # ripple lines running along the screen diagonal, drifting with the frame (loops in `frames`)
    warp = value2(c.sx, c.sy * 2.0, 11.0, 73) * 6.0
    wv = np.sin((c.sx * 0.5 - c.sy) * 0.55 + warp - ph)
    c.tone[wv > 0.82] += 0.9
    c.tone[wv < -0.85] -= 0.6
    # sparkles: jittered short streaks in 12x6 px cells, each lives a few frames
    gx, gy = _fl(c.sx / 12.0), _fl(c.sy / 6.0)
    on = hash2(gx, gy, 72)
    jx = (hash2(gx, gy, 74) * 9).astype(np.int64)
    jy = (hash2(gx, gy, 75) * 5).astype(np.int64)
    life = (_fl(on * 8) + f) % 8
    ln = np.array([1, 3, 4, 2, 0, 0, 0, 0])[life]
    lx = _fl(c.sx) - gx * 12 - jx
    gl = (on < m.p("sparkle", 0.45)) & (_fl(c.sy) - gy * 6 == jy) & (lx >= 0) & (lx < ln)
    c.tone[gl] += 2.0
    c.flat[gl & (life == 2)] = 10.0


# ---------------------------------------------------------------- vegetation
def pat_foliage(c, m):
    """Leaf clumps on curved crowns: clump noise in world space + bright leaf flecks on the lit side."""
    k = m.p("clump", 2.2)
    X = c.sx + c.seed * 0
    Y = c.sy
    cl = value2(X, Y, k, c.seed + 81) * 0.65 + value2(X, Y, k * 2.2, c.seed + 82) * 0.35
    c.tone += (cl - 0.5) * m.p("amp", 2.2)
    h = hash2(c.sx, c.sy, c.seed + 83)
    lit = c.n[:, 2] * 0.6 + c.n[:, 1] * 0.4 - c.n[:, 0] * 0.2
    c.tone[(h < 0.10) & (lit > 0.3)] += 1.4
    c.tone[h > 0.93] -= 1.4
    acc = m.p("accent", None)
    if acc is not None:
        ha = hash2(_fl(c.sx / 2.0), _fl(c.sy / 2.0), c.seed + 84)
        sel = ha < m.p("accent_amt", 0.12)
        c.ramp[sel] = RAMP[acc]
        c.tone[sel] += m.p("accent_tone", 0.5)


def pat_bark(c, m):
    c.tone[(_fl(c.u) % 3) == 0] -= 0.7
    _speckle(c, 0.3, 91)
    if m.p("marks", False):
        mk = hash2(_fl(c.z / 2.0), _fl(c.sx / 2.0), c.seed + 92) < m.p("marks", 0.3)
        c.ramp[mk] = RAMP["grey"]
        c.tone[mk] = -6.0 - m.shade
