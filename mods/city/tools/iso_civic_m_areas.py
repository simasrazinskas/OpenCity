"""CIVIC models: extractor area tiles (grain, vegetables, cotton, orchard, livestock, forestry plantation,
quarry pit, mine spoil, oil pad, fish water). Category "areas".

Each is a 1x1 ground tile that must join seamlessly to its neighbours:
- all textures use PERIODIC world-space noise (period = one tile) and rows run along x with a period that
  divides the tile, so tile A's right edge continues into tile B's left edge;
- 3D plants/props stay strictly inside the tile (nothing crosses x or y = 0 / 1), on a grid whose pitch
  divides 1 so the pattern is also continuous across the seam;
- `_ground` materials cancel the 'edge' outline (-2 shades on the silhouette rim) on the diamond edge so
  the tile border does not show up as a dark grid line.
Growth stage / arrangement comes from `st.variant` (0 = the default "day" render).
"""
import math

import numpy as np

from iso_civic_kit import ik, M, P, rect, lot, hcyl, cone
from iso_civic_props import tree, WATER_Z
from iso_civic_states import model
from isokit.noise import hash2

# ---------------------------------------------------------------- seamless ground
TWO = 2.0


def pvalue(x, y, n, seed=0):
    """Smooth value noise in [0,1) that is periodic with period 1 (n lattice cells per tile)."""
    x = np.asarray(x, np.float64) * n
    y = np.asarray(y, np.float64) * n
    x0, y0 = np.floor(x), np.floor(y)
    fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    xi, yi = x0.astype(np.int64) % n, y0.astype(np.int64) % n
    xj, yj = (xi + 1) % n, (yi + 1) % n
    a, b = hash2(xi, yi, seed), hash2(xj, yi, seed)
    c, d = hash2(xi, yj, seed), hash2(xj, yj, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def _rim(c):
    """Pixels that the 'edge' outline would darken because they border the transparent area outside the
    1x1 diamond (ground at z=0)."""
    r = np.zeros(len(c.sx), bool)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        sx = c.sx + 0.5 + dx
        sy = c.sy + 0.5 + dy
        X = (sy / 16.0 + sx / 32.0) / 2.0
        Y = (sy / 16.0 - sx / 32.0) / 2.0
        r |= (X < 0) | (X > 1) | (Y < 0) | (Y > 1)
    return r


def _pat(c, m):
    """Periodic soft patches + pixel speckle (+ optional furrow rows along x, ridges along y)."""
    seed = m.p("seed", 5)
    n = m.p("n", 4)
    p = pvalue(c.x, c.y, n, seed) * 0.6 + pvalue(c.x, c.y, n * 2, seed + 1) * 0.4
    c.tone += (p - 0.5) * m.p("patch", 1.8)
    h = hash2(c.sx, c.sy, seed + 2)
    sp = m.p("speck", 0.06)
    c.tone[h < sp] += m.p("lite", 1.3)
    c.tone[h > 1 - sp] -= m.p("dark", 1.2)
    rows = m.p("rows", 0)
    if rows:
        ph = (c.y * rows) % 1.0
        fw = m.p("furrow", 0.34)
        fur = np.abs(ph - 0.5) < fw / 2
        c.tone[fur] -= m.p("furrow_tone", 1.5)
        c.tone[~fur & (ph < 0.5)] += m.p("ridge_tone", 0.5)
    fn = m.p("extra", None)
    if fn is not None:
        fn(c, m)
    c.tone[_rim(c)] += 2.0


def G(ramp, shade=0.0, snow=True, **kw):
    """Seamless ground material: ramp + shade + params (patch, n, speck, lite, dark, rows, furrow, seed,
    furrow_tone, ridge_tone, extra)."""
    kw.setdefault("snow_at", 1.16)      # patchy snow: furrows / gravel keep showing through
    return ik.Material(ramp, shade, _pat, dither=kw.pop("dither", 0.35), snow=snow, **kw)


def _rim_only(c, m):
    c.tone[_rim(c)] += 2.0


def flat(ramp, shade=0.0, snow=True, dither=0.0):
    """Flat colour decal that also cancels the rim darkening."""
    return ik.Material(ramp, shade, _rim_only, dither=dither, snow=snow)


# standard grounds
GRASS = G("grass", -1.0, seed=31, speck=0.07, lite=1.3, dark=1.0)
DIRT = G("wood", 0.5, seed=41, speck=0.05)
MUD = G("wood", -1.2, seed=42, speck=0.03, patch=1.2)
PLOUGH = G("wood", -0.3, seed=43, rows=5, furrow=0.36, furrow_tone=1.6, ridge_tone=0.6, speck=0.05)
GRAVEL = G("stone", -0.5, seed=44, speck=0.16, patch=1.0, n=6)
ROCK = G("stone", -1.0, seed=45, speck=0.05, patch=2.4)


def scene(seed):
    s = ik.Scene((1, 1), seed)
    return s


def full(s, mat, layer=0):
    rect(s, 0, 0, 1, 1, mat, layer)


def clumps(s, rows, per, build, jitter=0.0, seed=0):
    """Plant grid: `rows` rows along x (row j at y=(j+.5)/rows), `per` plants per row (x=(i+.5)/per).
    build(s, x, y, j, i, rng) places one plant. Positions repeat across tiles (period 1)."""
    rng = np.random.default_rng(seed)
    for j in range(rows):
        for i in range(per):
            x = (i + 0.5) / per
            y = (j + 0.5) / rows
            if jitter:
                x += rng.uniform(-jitter, jitter)
                y += rng.uniform(-jitter, jitter)
            build(s, x, y, j, i, rng)


def blobp(ramp, shade, dither=0.0):
    return ik.Material(ramp, shade, None, dither=dither)


# ---------------------------------------------------------------- crop rows
def _crop_pat(c, m):
    """Speckled crop surface: ears / leaves as bright and dark pixels, soft periodic banding along the row."""
    seed = m.p("seed", 9)
    h = hash2(c.sx, c.sy, seed)
    c.tone += (pvalue(c.x, c.y, 6, seed) - 0.5) * m.p("band", 0.8)
    c.tone[h < m.p("lite_p", 0.16)] += m.p("lite", 1.2)
    c.tone[h > 1 - m.p("dark_p", 0.12)] -= m.p("dark", 1.4)
    acc = m.p("accent", None)
    if acc is not None:
        sel = hash2(c.sx // 2, c.sy // 2, seed + 4) < m.p("accent_amt", 0.1)
        c.ramp[sel] = ik.palette.RAMP[acc]
        c.tone[sel] += m.p("accent_tone", 1.0)


def crop(ramp, shade=0.0, **kw):
    return ik.Material(ramp, shade, _crop_pat, dither=kw.pop("dither", 0.3), **kw)


def ridge(s, y, w, h, mat, x0=0.0, x1=1.0, top=0.35):
    """A crop row along x: trapezoid ridge centred on y (width w at the ground, `top`*w on top, h px high)
    spanning almost the full tile so the rows of neighbouring tiles read as one continuous row."""
    a = w / 2
    b = a * top
    # +y slope (lit, visible), top, +x end cap (seen from the right), -y slope for other facings
    s.poly([(x0, y + a, 0), (x1, y + a, 0), (x1, y + b, h), (x0, y + b, h)], mat, outward=(0, 1, 0.5))
    s.poly([(x0, y - b, h), (x1, y - b, h), (x1, y + b, h), (x0, y + b, h)], mat, outward=(0, 0, 1))
    s.poly([(x0, y - a, 0), (x1, y - a, 0), (x1, y - b, h), (x0, y - b, h)], mat, outward=(0, -1, 0.5))


def ridges(s, nrows, w, h, mats):
    for j in range(nrows):
        ridge(s, (j + 0.5) / nrows, w, h, mats[j % len(mats)] if isinstance(mats, (list, tuple)) else mats)


GR_STAGES = ("ripe", "ploughed", "sprouting", "growing", "stubble")
GRAIN_V = tuple((lab, {"variant": k}) for k, lab in enumerate(GR_STAGES) if k)
STUBBLE = G("sand", -0.6, seed=47, rows=5, furrow=0.3, furrow_tone=0.9, ridge_tone=0.3, speck=0.08)


@model("area-grain", (1, 1), "areas", "Grain field", power=False, build=False, variants=GRAIN_V,
       note="Variant 0 / day = ripe wheat; stages: ploughed, sprouting, growing, stubble. Winter = fallow, snowy.")
def area_grain(st):
    stage = st.variant if st.season != "winter" else 1
    s = scene(70)
    full(s, STUBBLE if stage == 4 else PLOUGH)
    if stage == 0:
        ridges(s, 5, 0.14, 5, [crop("yellow", 0.2, seed=11, lite_p=0.1, dark_p=0.1),
                               crop("yellow", -0.4, seed=12, lite_p=0.1, dark_p=0.12)])
    elif stage == 3:
        ridges(s, 5, 0.14, 3.5, [crop("leaf", 0.8, seed=13, lite_p=0.1, dark_p=0.1)])
    elif stage == 2:
        def sprout(s, x, y, j, i, r):
            s.ellipsoid(x, y, 0.8, 0.026, 0.02, 1.8, blobp("grass", 1.5), zmin=0)
        clumps(s, 5, 8, sprout)
    elif stage == 4:
        def stub(s, x, y, j, i, r):
            s.box(x - 0.04, y - 0.01, 0, 0.08, 0.02, 1.0, blobp("sand", 0.5))
        clumps(s, 5, 6, stub)
        for (x, y) in ((0.3, 0.45), (0.75, 0.2)):          # round bale
            hcyl(s, x - 0.07, y, 3.2, 0.07, 0.14, P("yellow", 0.2, snow=False), "x", 10)
    return s


def bush(s, x, y, h, rx, ry, mat, rough=0.0):
    s.ellipsoid(x, y, h * 0.45, rx, ry, h * 0.55 + 0.4, mat, zmin=0, rough=rough)


# ---------------------------------------------------------------- vegetables
VEG_V = (("ploughed", {"variant": 1}), ("sprouting", {"variant": 2}), ("growing", {"variant": 3}))
BED = G("wood", -0.9, seed=48, rows=5, furrow=0.4, furrow_tone=1.8, ridge_tone=0.5, speck=0.04)


@model("area-vegetables", (1, 1), "areas", "Vegetable field", power=False, build=False, variants=VEG_V,
       note="Variant 0 / day = ready to harvest (cabbage, lettuce, red cabbage rows); ploughed, sprouting, growing. "
            "Winter = bare beds.")
def area_vegetables(st):
    stage = st.variant if st.season != "winter" else 1
    s = scene(71)
    full(s, BED)
    if stage == 0 or stage == 3:
        big = stage == 0
        for j in range(5):
            kind = j % 3                                   # cabbage, lettuce, red cabbage
            ramp, sh = (("leaf", 1.5), ("grass", 2.0), ("purple", 0.5))[kind]
            for i in range(5):
                x, y = (i + 0.5) / 5, (j + 0.5) / 5
                k = 1.0 if big else 0.62
                outer = ik.Material(ramp, sh, _crop_pat, dither=0.2, seed=20 + kind, lite_p=0.12, dark_p=0.1)
                s.ellipsoid(x, y, 1.6 * k, 0.075 * k, 0.062 * k, 3.4 * k, outer, zmin=0, rough=0.1)
                if big:
                    core = blobp(("grass", "yellow", "rose")[kind], (3.0, 3.5, 1.5)[kind])
                    s.ellipsoid(x, y + 0.004, 3.2, 0.04, 0.034, 2.4, core, zmin=1.5)
    elif stage == 2:
        def sprout(s, x, y, j, i, r):
            s.ellipsoid(x, y, 0.8, 0.032, 0.026, 1.8, blobp("grass", 2.0), zmin=0)
        clumps(s, 5, 5, sprout)
    return s


# ---------------------------------------------------------------- cotton
COT_V = (("ploughed", {"variant": 1}), ("sprouting", {"variant": 2}), ("growing", {"variant": 3}))
CBED = G("terra", -3.4, seed=49, rows=5, furrow=0.36, furrow_tone=1.4, ridge_tone=0.5, speck=0.05, patch=1.2)


@model("area-cotton", (1, 1), "areas", "Cotton field", power=False, build=False, variants=COT_V,
       note="Variant 0 / day = open white bolls; ploughed, sprouting, growing (pink-white flowers). "
            "Winter = bare red soil.")
def area_cotton(st):
    stage = st.variant if st.season != "winter" else 1
    s = scene(72)
    full(s, CBED)
    if stage == 2:
        def sprout(s, x, y, j, i, r):
            s.ellipsoid(x, y, 0.8, 0.03, 0.024, 1.8, blobp("leaf", 2.0), zmin=0)
        clumps(s, 5, 8, sprout)
    elif stage in (0, 3):
        def plant(s, x, y, j, i, r):
            m = ik.Material("leaf", 0.6, _crop_pat, dither=0.2, seed=30 + (i + j) % 3, lite_p=0.1, dark_p=0.12,
                            accent=("rose" if stage == 3 else None), accent_amt=0.12, accent_tone=1.5)
            s.ellipsoid(x, y, 2.6, 0.085, 0.065, 3.4, m, zmin=0, rough=0.2)
            if stage == 0:
                for dx, dy, dz in ((-0.04, 0.02, 4.6), (0.01, -0.02, 5.2), (0.045, 0.025, 3.8), (-0.005, 0.035, 3.2)):
                    s.ellipsoid(x + dx, y + dy, dz, 0.022, 0.02, 1.2, blobp("stone", 5.5, dither=0.0))
        clumps(s, 5, 6, plant)
    return s


# ---------------------------------------------------------------- orchard
ORC_V = (("saplings", {"variant": 1}), ("young", {"variant": 2}), ("mature", {"variant": 3}))
ORCH_GROUND = G("grass", -1.2, seed=33, speck=0.06, rows=2, furrow=0.5, furrow_tone=0.6, ridge_tone=0.0)
TREE_XY = ((0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75))


def _fruit_tree(s, x, y, h, r, leaf, trunk=None):
    th = h * 0.38
    s.cylinder(x, y, 0, 0.022 + r * 0.04, th + 2, trunk or "bark", segs=6)
    rz = (h - th) * 0.55
    s.blob([(x, y, th + rz, r, rz), (x + r * 0.3, y - r * 0.2, th + rz * 0.7, r * 0.72, rz * 0.8)], leaf, rough=0.28)


@model("area-orchard", (1, 1), "areas", "Orchard", power=False, build=False, variants=ORC_V,
       note="Variant 0 / day = fruiting trees (red apples); saplings, young, mature (no fruit). Winter = bare trees.")
def area_orchard(st):
    stage = st.variant
    s = scene(73)
    full(s, ORCH_GROUND)
    for (x, y) in TREE_XY:
        a = [2 * math.pi * k / 16 for k in range(16)]
        s.ground([(x + 0.1 * math.cos(t), y + 0.1 * math.sin(t)) for t in a], DIRT, layer=1)
    if st.season == "winter":
        leaf = ik.mat("forest_floor", ramp="wood", shade=-2.0, pebbles=0.2, patch=1.0, dither=0.8)
    elif stage == 0:
        leaf = ik.mat("foliage", accent="red", accent_amt=0.16, accent_tone=1.0)
    else:
        leaf = ik.mat("foliage")
    h, r = {0: (22, 0.21), 1: (8, 0.07), 2: (15, 0.14), 3: (22, 0.21)}[stage]
    for (x, y) in TREE_XY:
        if stage == 1 and st.season != "winter":
            s.box(x - 0.07, y - 0.007, 0, 0.014, 0.014, 6, P("wood", 1, snow=False))     # stake
        _fruit_tree(s, x, y, h, r, leaf)
    return s


# ---------------------------------------------------------------- livestock pasture
LIV_V = (("sheep, left fence", {"variant": 1}), ("calves, corner fence", {"variant": 2}),
         ("sheep, open pasture", {"variant": 3}))
PASTURE = G("grass", -0.8, seed=34, speck=0.08, patch=2.2)


def _hide_pat(c, m):
    """Cow hide: white with big dark (or brown) patches; wool for sheep."""
    from isokit.noise import value2
    n = value2(c.sx / 2.2, c.sy / 1.6, 1.0, m.p("seed", 3))
    sel = n > m.p("cut", 0.6)
    c.ramp[sel] = ik.palette.RAMP[m.p("patch_ramp", "grey")]
    c.tone[sel] += m.p("patch_tone", -5.0) - m.shade


def cow(s, x, y, kind="holstein", axis="x"):
    """Cow with its rear-left corner (x, y); body ~0.16 x 0.06."""
    brown = kind == "brown"
    hide = ik.Material("snow" if not brown else "wood", 1.0 if not brown else 0.2, _hide_pat, dither=0.0,
                       patch_ramp="grey" if not brown else "snow", patch_tone=-5.0 if not brown else 3.0,
                       seed=int(x * 50 + y * 30), cut=0.6 if not brown else 0.72, snow=False)
    dark = P("grey", -5, snow=False)
    L, W = 0.16, 0.065
    if axis == "x":
        for lx in (0.015, 0.12):
            for ly in (0.0, W - 0.02):
                s.box(x + lx, y + ly, 0, 0.022, 0.02, 2.5, dark)
        s.box(x, y, 2.4, L, W, 3.6, hide)
        s.box(x + L, y + 0.012, 3.6, 0.05, 0.04, 3.0, hide)          # head
        s.box(x + L + 0.04, y + 0.016, 3.8, 0.012, 0.032, 1.2, P("rose", 2, snow=False))   # muzzle
        s.box(x - 0.012, y + 0.028, 4.5, 0.012, 0.008, 2.4, dark)   # tail
    else:
        for ly in (0.015, 0.12):
            for lx in (0.0, W - 0.02):
                s.box(x + lx, y + ly, 0, 0.02, 0.022, 2.5, dark)
        s.box(x, y, 2.4, W, L, 3.6, hide)
        s.box(x + 0.012, y + L, 3.6, 0.04, 0.05, 3.0, hide)
        s.box(x + 0.016, y + L + 0.04, 3.8, 0.032, 0.012, 1.2, P("rose", 2, snow=False))
        s.box(x + 0.028, y - 0.012, 4.5, 0.008, 0.012, 2.4, dark)


def sheep(s, x, y, axis="x"):
    wool = ik.Material("snow", 1.4, None, dither=0.0, snow=False)
    dark = P("grey", -4, snow=False)
    if axis == "x":
        s.ellipsoid(x, y, 3.2, 0.055, 0.04, 2.6, wool, zmin=1.2, rough=0.25)
        s.box(x + 0.05, y - 0.014, 2.4, 0.03, 0.028, 2.6, dark)
        for lx in (-0.03, 0.03):
            s.box(x + lx, y - 0.01, 0, 0.012, 0.012, 1.5, dark)
    else:
        s.ellipsoid(x, y, 3.2, 0.04, 0.055, 2.6, wool, zmin=1.2, rough=0.25)
        s.box(x - 0.014, y + 0.05, 2.4, 0.028, 0.03, 2.6, dark)
        for ly in (-0.03, 0.03):
            s.box(x - 0.01, y + ly, 0, 0.012, 0.012, 1.5, dark)


def trough(s, x, y, L=0.2, axis="x"):
    w = P("wood", 0.5)
    if axis == "x":
        s.box(x, y, 0, L, 0.07, 3.2, w)
        rect(s, x + 0.012, y + 0.012, x + L - 0.012, y + 0.058, "water", layer=3, z=3.2)
    else:
        s.box(x, y, 0, 0.07, L, 3.2, w)
        rect(s, x + 0.012, y + 0.012, x + 0.058, y + L - 0.012, "water", layer=3, z=3.2)


def rail_fence(s, axis, at, n=4, h=6.5):
    """Post-and-rail fence along an edge of the tile: axis 'x' = along x at y=at, 'y' = along y at x=at."""
    wood = P("wood", 1.5, snow=True)
    for k in range(n):
        p = (k + 0.5) / n
        if axis == "x":
            s.box(p - 0.012, at - 0.012, 0, 0.024, 0.024, h, wood)
        else:
            s.box(at - 0.012, p - 0.012, 0, 0.024, 0.024, h, wood)
    for z in (h - 1.4, h * 0.45):
        if axis == "x":
            s.box(0.0, at - 0.007, z, 1.0, 0.014, 0.9, wood)
        else:
            s.box(at - 0.007, 0.0, z, 0.014, 1.0, 0.9, wood)


@model("area-livestock", (1, 1), "areas", "Livestock pasture", power=False, build=False, variants=LIV_V,
       note="Variant 0 / day = cows with back fence and a trough; sheep, calves in a corner, open pasture. "
            "Fences only on the tile edges that border another pasture.")
def area_livestock(st):
    v = st.variant
    s = scene(74)
    full(s, PASTURE)
    if v in (0, 2):
        rail_fence(s, "x", 0.04)
    if v in (1, 2):
        rail_fence(s, "y", 0.04)
    if v == 0:
        cow(s, 0.2, 0.28)
        cow(s, 0.55, 0.62, "brown")
        trough(s, 0.62, 0.2, 0.22)
        tuft = P("grass", 3, snow=False)
    elif v == 1:
        for (x, y, ax) in ((0.3, 0.3, "x"), (0.62, 0.45, "x"), (0.42, 0.7, "y"), (0.78, 0.76, "x")):
            sheep(s, x, y, ax)
    elif v == 2:
        cow(s, 0.3, 0.3, "brown")
        cow(s, 0.58, 0.5, "brown", axis="y")
        s.box(0.12, 0.62, 0, 0.2, 0.1, 4, P("sand", 1.5, snow=True), top=P("yellow", 2.5))   # hay rack
        s.box(0.12, 0.62, 4, 0.2, 0.1, 1, P("wood", 0))
    else:
        sheep(s, 0.25, 0.25)
        sheep(s, 0.55, 0.3, "y")
        sheep(s, 0.4, 0.62)
        trough(s, 0.62, 0.7, 0.2)
    return s


# ---------------------------------------------------------------- forestry plantation
FOR_V = (("saplings", {"variant": 1}), ("young", {"variant": 2}), ("felled, log stack", {"variant": 3}))
FLOOR = G("leaf", -1.8, seed=35, speck=0.1, patch=2.2, lite=1.0, dark=1.2)
NEEDLES = G("wood", -2.2, seed=36, speck=0.1, patch=2.0)


def conifer(s, x, y, h, r, tiers=3, mat="conifer"):
    s.cylinder(x, y, 0, 0.02, h * 0.3, "bark", segs=6)
    for i in range(tiers):
        z0 = h * (0.16 + 0.26 * i)
        rr = r * (1.15 - 0.3 * i)
        cone(s, x, y, z0, rr, h * 0.44, mat, segs=10)


@model("area-forestry", (1, 1), "areas", "Forestry plantation", power=False, build=False, variants=FOR_V,
       note="Variant 0 / day = mature conifers; saplings (planted rows), young, felled (stumps + log stack). "
            "Trees are never cut by the tile edge.")
def area_forestry(st):
    v = st.variant
    s = scene(75)
    full(s, NEEDLES if v == 3 else FLOOR)
    pts = [((i + 0.5) / 3, (j + 0.5) / 3) for j in range(3) for i in range(3)]
    if v == 0:
        for k, (x, y) in enumerate(pts):
            conifer(s, x, y, 24 + (k * 5 % 5), 0.13)
    elif v == 2:
        pts = [((i + 0.5) / 4, (j + 0.5) / 4) for j in range(4) for i in range(4)]
        for (x, y) in pts:
            conifer(s, x, y, 15, 0.085, tiers=3)
    elif v == 1:
        pts = [((i + 0.5) / 4, (j + 0.5) / 4) for j in range(4) for i in range(4)]
        for (x, y) in pts:
            cone(s, x, y, 1, 0.05, 8, ik.mat("conifer", shade=0.5), segs=8)
            s.box(x - 0.007, y - 0.007, 0, 0.014, 0.014, 2, P("wood", -1, snow=False))
    else:
        stump = P("wood", 1.0)
        for k, (x, y) in enumerate(pts):
            if (k % 3, k // 3) in ((0, 2), (1, 2)):
                continue
            s.cylinder(x, y, 0, 0.045, 2.2, stump, top=P("sand", 0.5, snow=True), segs=8)
        logm = ik.Material("wood", 0.4, None, dither=0.0)
        for (y, z) in ((0.77, 1.5), (0.85, 1.5), (0.81, 4.1)):
            hcyl(s, 0.17, y, z, 0.036, 0.62, logm, "x", 8)
    return s


# ---------------------------------------------------------------- quarry pit
QUARRY_V = (("pit depth 2", {"variant": 1}), ("pit depth 3", {"variant": 2}), ("pit depth 4", {"variant": 3}))
STRATA = ik.Material("stone", -1.0, None, dither=0.3)


def _strata_pat(c, m):
    """Rock face: horizontal strata lines and chips (u along x)."""
    v = np.floor(c.v).astype(np.int64)
    c.tone[(v % 3) == 0] -= 0.9
    h = hash2(c.sx, c.sy, m.p("seed", 7))
    c.tone[h < 0.1] += 1.0
    c.tone[h > 0.92] -= 1.0


def _wave(x, amp, k, ph):
    return amp * math.sin(2 * math.pi * (k * x + ph)) + amp * 0.5 * math.sin(2 * math.pi * (2 * k + 1) * x + ph * 3)


def bench(s, y0, y1, h, top, face, amp=0.025, k=2, ph=0.0, n=16):
    """A terrace along x with a wavy front lip (period divides the tile, so it joins the next tile): flat top
    at height h + a +y riser (no end caps: it continues into the next tile)."""
    xs = [i / n for i in range(n + 1)]
    ys = [y1 + _wave(x, amp, k, ph) for x in xs]
    s.poly([(0, y0, h)] + [(x, y, h) for x, y in zip(xs, ys)] + [(1, y0, h)], top, outward=(0, 0, 1))
    for i in range(n):
        s.poly([(xs[i], ys[i], 0), (xs[i + 1], ys[i + 1], 0), (xs[i + 1], ys[i + 1], h), (xs[i], ys[i], h)], face,
               outward=(0, 1, 0))
    return ys


def boulder(s, x, y, r, h, mat, rough=0.4, z=0.0):
    s.ellipsoid(x, y, z + h * 0.4, r, r * 0.85, h * 0.6, mat, zmin=z, rough=rough)


@model("area-quarry", (1, 1), "areas", "Quarry pit", power=False, build=False, variants=QUARRY_V,
       note="Variant 0 / day = shallow bench; deeper pit tiles get higher terraces, darker rock, rubble and a puddle. "
            "Terraces run along x so neighbours join.")
def area_quarry(st):
    d = st.variant
    s = scene(76)
    sh = -0.9 - 0.45 * d
    floor = G("stone", -2.2 - 0.5 * d, seed=45 + d, speck=0.12, patch=2.2)
    top = G("stone", sh + 0.8, seed=52, speck=0.07, patch=1.8, n=4)
    face = ik.Material("stone", sh - 1.6, _strata_pat, dither=0.3, seed=7 + d)
    shadow = flat("grey", -6.5 + 0.3 * d, dither=0.0)
    full(s, floor)
    h = 3.0 + 1.5 * d
    for (y0, y1, hh, ph) in ((0.04, 0.4, h, 0.0), (0.54, 0.9, h * 0.75, 0.3)):
        rect(s, 0, y1 + 0.02, 1, min(y1 + 0.1, 1.0), floor.with_(shade=floor.shade - 1.4), layer=2)   # foot shadow
        bench(s, y0, y1, hh, top, face, ph=ph)
    rock = ik.Material("stone", sh - 0.2, _strata_pat, dither=0.2, seed=19, snow=True)
    boulder(s, 0.22, 0.2, 0.065, 5, rock, z=h)
    boulder(s, 0.72, 0.68, 0.055, 4, rock, z=h * 0.75)
    if d >= 2:
        boulder(s, 0.5, 0.28, 0.035, 2.4, rock, z=h)
    if d == 3:
        rect(s, 0.12, 0.925, 0.88, 0.985, G("water", -1.0, snow=False, speck=0.0, patch=0.8), layer=3)
    return s


# ---------------------------------------------------------------- mine spoil + rails
MINE_V = (("rails + ore tub", {"variant": 1}), ("spoil heaps", {"variant": 2}), ("ore pile + 2 tubs", {"variant": 3}))
SPOIL = G("stone", -2.0, seed=53, speck=0.2, patch=1.6, n=6, dark=1.6)
DARKROCK = ik.Material("grey", -2.8, None, dither=0.5)
ORE = ik.Material("grey", -2.2, None, dither=0.6)


def rails(s, y=0.5, gauge=0.2):
    """Narrow-gauge track along x: two rails and sleepers (period 0.125, so tiles join)."""
    rail = P("grey", 1.5, snow=False)
    tie = P("wood", -1.5, snow=True)
    for k in range(8):
        x = (k + 0.5) / 8
        s.box(x - 0.017, y - gauge / 2 - 0.03, 0, 0.034, gauge + 0.06, 1.0, tie)
    for dy in (-gauge / 2, gauge / 2):
        s.box(0.0, y + dy - 0.007, 1.0, 1.0, 0.014, 1.0, rail)


def tub(s, x, y, ore=True, ramp="wood"):
    """Mine tub (ore wagon) on the track, length along x."""
    body = M("metal", ramp=ramp, shade=-0.5, snow=False)
    s.box(x, y - 0.1, 2, 0.26, 0.2, 5, body, top=P("slate", -3, snow=False))
    for lx in (0.04, 0.17):
        for ly in (-0.1, 0.07):
            s.box(x + lx, y + ly, 0.5, 0.05, 0.03, 2, P("grey", -4, snow=False))
    if ore:
        s.ellipsoid(x + 0.13, y, 6.5, 0.1, 0.08, 3.0, ORE, zmin=6.5, rough=0.4)


@model("area-mine", (1, 1), "areas", "Mine spoil and track", power=False, build=False, variants=MINE_V,
       note="Variant 0 / day = gravel with empty track; track + loaded tub, spoil heaps, ore pile with two tubs.")
def area_mine(st):
    v = st.variant
    s = scene(77)
    full(s, SPOIL)
    if v == 0:
        rails(s)
        for (x, y) in ((0.15, 0.15), (0.8, 0.82), (0.55, 0.2)):
            boulder(s, x, y, 0.04, 3, ORE, 0.3)
    elif v == 1:
        rails(s)
        tub(s, 0.35, 0.5)
        boulder(s, 0.8, 0.15, 0.05, 3, ORE, 0.3)
    elif v == 2:
        for (x, y, r, h) in ((0.3, 0.3, 0.25, 9), (0.68, 0.62, 0.28, 11), (0.28, 0.75, 0.17, 6)):
            s.ellipsoid(x, y, 0, r, r * 0.9, h, DARKROCK, zmin=0, rough=0.35)
    else:
        rails(s, 0.7)
        tub(s, 0.1, 0.7)
        tub(s, 0.62, 0.7)
        s.ellipsoid(0.5, 0.25, 0, 0.3, 0.2, 9, ORE, zmin=0, rough=0.4)
    return s


# ---------------------------------------------------------------- oil pad
OIL_V = (("pipes + valves", {"variant": 1}), ("barrels + drums", {"variant": 2}), ("pump skid", {"variant": 3}))


def _oil_pat(c, m):
    from isokit.noise import value2
    n = value2(c.sx / 5.0, c.sy / 3.0, 1.0, 61)
    c.tone[n > 0.74] -= 2.4          # oil stains


def oil_ground(seed):
    return G("stone", -0.2, seed=seed, speck=0.14, patch=1.2, n=6, extra=_oil_pat)


def pipe_x(s, y, z, r, mat):
    hcyl(s, 0.0, y, z, r, 1.0, mat, "x", 10, caps=False)


def valve(s, x, y, z):
    s.box(x - 0.02, y - 0.02, z, 0.04, 0.04, 2.4, P("grey", 0, snow=False))
    s.box(x - 0.045, y - 0.006, z + 2.4, 0.09, 0.012, 0.8, P("red", 1, snow=False))


@model("area-oil", (1, 1), "areas", "Oil pad", power=False, build=False, variants=OIL_V,
       note="Variant 0 / day = gravel pad with two pipes; pipes and valve manifold, barrels, pump skid. "
            "Pipes run along x, supports repeat every 0.25 so tiles join.")
def area_oil(st):
    v = st.variant
    s = scene(78)
    full(s, oil_ground(54))
    pm = ik.Material("olive", 0.5, None, dither=0.0, snow=False)
    ym = ik.Material("yellow", 0.5, None, dither=0.0, snow=False)
    if v in (0, 1):
        for y, mm in ((0.38, pm), (0.52, ym)):
            for k in range(4):
                s.box((k + 0.5) / 4 - 0.015, y - 0.015, 0, 0.03, 0.03, 2.2, P("grey", -1, snow=False))
            pipe_x(s, y, 4.0, 0.035, mm)
        if v == 1:
            pole_x = 0.3
            valve(s, 0.3, 0.45, 5.4)
            hcyl(s, 0.3, 0.2, 4.0, 0.025, 0.2, pm, "y", 8, caps=True)
            valve(s, 0.72, 0.45, 5.4)
        else:
            s.box(0.12, 0.7, 0, 0.1, 0.1, 3, P("grey", -2, snow=False))
            valve(s, 0.7, 0.38, 5.4)
    elif v == 2:
        for (x, y) in ((0.2, 0.3), (0.32, 0.34), (0.26, 0.45), (0.7, 0.62), (0.8, 0.7)):
            s.cylinder(x, y, 0, 0.05, 6, M("metal", ramp="red" if (x * 10) % 2 < 1 else "olive", shade=-0.3),
                       top=P("grey", 1), segs=10)
        s.box(0.55, 0.12, 0, 0.3, 0.16, 3, P("wood", 1))
    else:
        s.ground([(0.08, 0.1), (0.92, 0.1), (0.92, 0.9), (0.08, 0.9)], flat("grey", 1.0), layer=1)
        s.box(0.2, 0.3, 0, 0.35, 0.3, 9, M("metal", ramp="olive", shade=0.0), top=P("grey", 2))
        s.cylinder(0.72, 0.4, 0, 0.1, 9, M("metal_light", shade=0.5), top=P("grey", 2), segs=12)
        pipe_x(s, 0.78, 3.0, 0.03, pm)
        valve(s, 0.4, 0.78, 3.0)
    return s


# ---------------------------------------------------------------- fish water
FISH_V = (("floating net cage", {"variant": 1}), ("net curtain", {"variant": 2}), ("marker buoy", {"variant": 3}))


def _water_pat(c, m):
    from isokit.materials import REGISTRY
    REGISTRY["water"].pattern(c, REGISTRY["water"])
    c.tone[_rim(c)] += 2.0


SEA = ik.Material("water", -0.5, _water_pat, dither=0.0, snow=False)


def _net_pat(c, m):
    """Net mesh: dark water-ish tone with a light diamond lattice."""
    a = (c.sx + 2 * c.sy) % 6 == 0
    b = (c.sx - 2 * c.sy) % 6 == 0
    c.tone[a | b] += 2.4
    c.ramp[a | b] = ik.palette.RAMP["sand"]
    c.tone[_rim(c)] += 2.0


NET = ik.Material("water", -3.2, _net_pat, dither=0.0, snow=False)


def buoy(s, x, y, ramp="terra", r=0.028):
    s.ellipsoid(x, y, 1.0, r, r, 2.2, ik.Material(ramp, 2.0, None, dither=0.0, snow=False), zmin=0.0)


@model("area-fish", (1, 1), "areas", "Fishing water", power=False, build=False, variants=FISH_V,
       note="Variant 0 / day = buoy line; floating net cage, net curtain, marker buoy. The sea animates "
            "(8-frame water shader). No boats here (LIFE).")
def area_fish(st):
    """Fishing gear on the water: built at z=0, then lowered to the natural water level."""
    out = scene(79)
    out.merge(_area_fish(st), dz=WATER_Z)
    return out


def _area_fish(st):
    v = st.variant
    s = scene(79)
    full(s, SEA)
    if v == 0:
        for k in range(5):
            buoy(s, (k + 0.5) / 5, 0.5, "terra" if k % 2 == 0 else "snow")
        s.box(0.0, 0.497, 0.9, 1.0, 0.006, 0.5, P("sand", 0, snow=False))
    elif v == 1:
        x0, y0, w = 0.2, 0.2, 0.6
        rect(s, x0, y0, x0 + w, y0 + w, NET, layer=3)
        fl = P("yellow", 1.5, snow=False)
        s.box(x0, y0 + w - 0.045, 0, w, 0.045, 2.2, fl)
        s.box(x0 + w - 0.045, y0, 0, 0.045, w, 2.2, fl)
        s.box(x0, y0, 0, 0.045, w, 2.2, fl)
        s.box(x0, y0, 0, w, 0.045, 2.2, fl)
        for (px, py) in ((x0, y0), (x0 + w - 0.03, y0), (x0, y0 + w - 0.03), (x0 + w - 0.03, y0 + w - 0.03)):
            s.box(px, py, 0, 0.03, 0.03, 7, P("grey", 1, snow=False))
        s.box(x0 + w - 0.03, y0 + w - 0.03, 7, 0.03, 0.03, 1, M("lamp", ramp="terra", eramp="terra", shade=6))
        for k in range(1, 4):                              # walkway rail between posts
            pass
    elif v == 2:
        rect(s, 0.0, 0.3, 1.0, 0.7, NET, layer=3)
        for y in (0.3, 0.7):
            for k in range(10):
                x = (k + 0.5) / 10
                buoy(s, x, y - 0.02 if y > 0.5 else y + 0.02, "snow" if k % 2 else "terra", 0.022)
    else:
        s.cylinder(0.5, 0.5, 0, 0.07, 6, M("awning", ramp="red", width=3), top=P("red", 1, snow=False), segs=12)
        cone(s, 0.5, 0.5, 6, 0.07, 6, P("red", 1, snow=False), segs=12)
        s.box(0.49, 0.49, 12, 0.02, 0.02, 7, P("grey", 1, snow=False))
        s.box(0.47, 0.47, 17.5, 0.06, 0.06, 1.5, M("lamp", ramp="yellow", eramp="yellow", shade=6))
        for (x, y) in ((0.15, 0.2), (0.82, 0.82), (0.8, 0.18)):
            buoy(s, x, y, "terra")
    return s
