"""Procedural trees (usable by every workstream for lots, parks and streets).

    s = ik.Scene((1, 1)); ik.add_tree(s, 0.5, 0.5, 'oak', season='summer', stage=2, seed=4)
    spr = ik.render(ik.tree('spruce', season='winter'), season='winter')
Species: linden (game tree-1), oak (tree-2), spruce (tree-3), birch (tree-4), maple, pine, fir, poplar,
willow, apple, cherry. Stages 0 sapling, 1 young, 2 mature. Seasons spring/summer/autumn/winter.
State: 'alive' | 'dead' | 'burnt' | 'stump'.
"""
import math

import numpy as np

from .materials import mat
from .surfaces import pat_foliage
from .noise import Rng
from .scene import Scene

SPECIES = {
    # kind, trunk height px, crown radius (cells), crown half-height px, trunk radius, foliage per season
    "linden": dict(kind="round", trunk=12, r=0.27, rz=16, tr=0.035,
                   fol={"spring": ("grass", 0.5), "summer": ("grass", -1.0), "autumn": ("yellow", -1.5)}),
    "oak": dict(kind="round", trunk=12, r=0.34, rz=17, tr=0.05, n=14,
                fol={"spring": ("grass", 0.0), "summer": ("leaf", 0.3), "autumn": ("terra", -1.5)}),
    "maple": dict(kind="round", trunk=11, r=0.28, rz=17, tr=0.035,
                  fol={"spring": ("grass", 0.3), "summer": ("leaf", 0.8), "autumn": ("red", -0.5)}),
    "birch": dict(kind="airy", trunk=16, r=0.2, rz=15, tr=0.025, bark="bark_birch",
                  fol={"spring": ("grass", 1.0), "summer": ("grass", 0.0), "autumn": ("yellow", 0.0)}),
    "poplar": dict(kind="column", trunk=6, r=0.12, rz=30, tr=0.03,
                   fol={"spring": ("grass", 0.3), "summer": ("leaf", 0.6), "autumn": ("yellow", -0.5)}),
    "willow": dict(kind="weeping", trunk=10, r=0.3, rz=14, tr=0.045,
                   fol={"spring": ("grass", 1.2), "summer": ("grass", 0.4), "autumn": ("olive", 0.5)}),
    "apple": dict(kind="round", trunk=7, r=0.22, rz=11, tr=0.03, fruit="red",
                  fol={"spring": ("grass", 0.5), "summer": ("leaf", 0.8), "autumn": ("olive", 0.0)}),
    "cherry": dict(kind="round", trunk=8, r=0.23, rz=12, tr=0.03, blossom="rose",
                   fol={"spring": ("rose", 2.5), "summer": ("leaf", 0.8), "autumn": ("terra", -0.5)}),
    "spruce": dict(kind="cone", trunk=4, r=0.2, h=52, tr=0.025, ever=True,
                   fol={s: ("teal", -2.1) for s in ("spring", "summer", "autumn", "winter")}),
    "fir": dict(kind="cone", trunk=5, r=0.16, h=46, tr=0.02, ever=True, tiers=5,
                fol={s: ("leaf", -1.2) for s in ("spring", "summer", "autumn", "winter")}),
    "pine": dict(kind="pine", trunk=30, r=0.26, rz=6, tr=0.035, ever=True, bark_ramp="terra",
                 fol={s: ("leaf", -0.8) for s in ("spring", "summer", "autumn", "winter")}),
}
GAME_TREES = {"tree-1": "linden", "tree-2": "oak", "tree-3": "spruce", "tree-4": "birch"}
STAGE_SCALE = (0.38, 0.68, 1.0)


def _foliage(sp, season, extra=None):
    ramp, shade = sp["fol"].get(season, sp["fol"]["summer"])
    m = mat("conifer" if sp["kind"] in ("cone",) else "foliage", ramp=ramp, shade=shade)
    if sp.get("ever"):
        m = m.with_(snow_at=0.5)
    if extra:
        m = m.with_(**extra)
    return m


def _branches(s, rng, base, up, length, r, depth, bark, spread=0.75, kids=(2, 3)):
    """Recursive bare branches from base along direction up (world, z in px units of length)."""
    tip = base + up * length
    s.limb(base, tip, r, r * 0.65, bark)
    if depth <= 0:
        return [tip]
    tips = []
    for _ in range(rng.randint(*kids)):
        az = rng.uniform(0, 2 * math.pi)
        tilt = rng.uniform(0.35, spread)
        d = np.array([math.cos(az) * math.sin(tilt), math.sin(az) * math.sin(tilt), math.cos(tilt)])
        d = d / np.array([39.2, 39.2, 1.0])          # metric unit direction -> world per metric px
        tips += _branches(s, rng, tip, d, length * rng.uniform(0.55, 0.75), r * 0.62, depth - 1, bark,
                          spread, kids)
    return tips


def pat_conifer(c, m):
    """Conifer tier: needle clumps + dark underside / light top of each tier."""
    pat_foliage(c, m)
    t = (c.z - m.p("z0", 0.0)) / max(m.p("h", 1.0), 1.0)
    c.tone += (np.clip(t, 0, 1) - 0.35) * 2.4


def bare_crown(s, rng, cx, cy, z0, ztop, r, bark, tr, n=8, rise=0.45, twigs=2):
    """Leafless crown: leader trunk up to ztop with n branches reaching ~r cells, each with twigs."""
    s.limb((cx, cy, z0 - 1), (cx, cy, ztop), tr, tr * 0.35, bark)
    for i in range(n):
        t = (i + rng.uniform(0.2, 0.8)) / n
        zb = z0 + (ztop - z0) * t * 0.85
        a = rng.uniform(0, 2 * math.pi) if i == 0 else a + 2.4 + rng.uniform(-0.4, 0.4)
        reach = r * (0.7 + 0.45 * math.sin(math.pi * min(1.0, 0.2 + t * 0.9))) * rng.uniform(0.85, 1.1)
        ex, ey = cx + math.cos(a) * reach, cy + math.sin(a) * reach
        ez = zb + reach * 39.2 * rise * rng.uniform(0.7, 1.2)
        s.limb((cx, cy, zb), (ex, ey, ez), tr * 0.55, tr * 0.25, bark)
        for j in range(twigs):
            f = rng.uniform(0.45, 0.8)
            px, py, pz = cx + (ex - cx) * f, cy + (ey - cy) * f, zb + (ez - zb) * f
            b = a + rng.uniform(-0.9, 0.9)
            L = reach * rng.uniform(0.35, 0.55)
            s.limb((px, py, pz), (px + math.cos(b) * L, py + math.sin(b) * L, pz + L * 39.2 * 0.8),
                   tr * 0.3, tr * 0.2, bark)


def add_tree(s, cx, cy, species="oak", season="summer", stage=2, seed=0, state="alive", z=0.0, scale=1.0):
    """Add a tree standing at (cx, cy, z) to scene s. scale multiplies every size (e.g. 0.6 for
    planter trees, 1.3 for park giants)."""
    sp = SPECIES[species]
    rng = Rng(seed * 131 + sum(map(ord, species)))
    k = STAGE_SCALE[stage] * scale
    bark = mat(sp.get("bark", "bark"))
    if "bark_ramp" in sp:
        bark = bark.with_(ramp=sp["bark_ramp"], shade=0.0)
    if state == "stump":
        s.cylinder(cx, cy, z, sp["tr"] * 1.3 * scale, 4 * scale, bark, top=mat("wood", shade=1.5), segs=10)
        return s
    if state in ("dead", "burnt"):
        bark = mat("bark", ramp="grey", shade=-0.5) if state == "dead" else mat("plain", ramp="grey", shade=-6.5)
    ever = sp.get("ever", False)
    bare = state != "alive" or (season == "winter" and not ever)
    base = np.array([cx, cy, z])
    kind = sp["kind"]
    if kind == "cone":
        h = sp["h"] * k
        tr = max(sp["tr"] * k, 0.02)
        z0 = z + sp["trunk"] * k
        if state != "alive":
            bare_crown(s, rng, cx, cy, z, z0 + h * 0.85, sp["r"] * k * 0.8, bark, tr, n=9, rise=0.15, twigs=1)
            return s
        s.cylinder(cx, cy, z, tr, sp["trunk"] * k + 2, bark, segs=8)
        tiers = sp.get("tiers", 6) if stage else 3
        fol = _foliage(sp, season)
        for i in range(tiers):
            t = i / tiers
            r = sp["r"] * k * (1.0 - t * 0.8)
            zb = z0 + h * t * 0.84
            hh = h * (1.0 - t * 0.84) * 0.5 + 5 * k
            tm = fol.with_(pattern=pat_conifer, z0=zb, h=hh)
            s.cone(cx, cy, zb, r, hh, tm, segs=16)
            m = max(5, int(r * 60))
            for j in range(m):
                a = 2 * math.pi * (j + rng.uniform(-0.3, 0.3)) / m
                rr = r * rng.uniform(0.85, 1.0)
                s.ellipsoid(cx + math.cos(a) * rr * 0.85, cy + math.sin(a) * rr * 0.85, zb + 1.5,
                            r * 0.22, r * 0.22, 2.2 * k + 1, tm, rough=0.3)
        return s
    th = sp["trunk"] * k
    tr = max(sp["tr"] * k, 0.018)
    r, rz = sp["r"] * k, sp["rz"] * k
    if kind == "column":
        cz = z + th + rz
    elif kind == "pine":
        cz = z + th * (0.6 + 0.4 * k)
    else:
        cz = z + th + rz * 0.8
    s.limb(base, base + [0, 0, cz - z - rz * 0.3], tr, tr * 0.75, bark)
    if bare:
        bare_crown(s, rng, cx, cy, cz - rz * 0.6, cz + rz * 0.9, r * (1.3 if kind == "column" else 0.95),
                   bark, tr * 0.8, n=(11 if stage == 2 else 7) if stage else 4,
                   rise=0.9 if kind == "column" else 0.35, twigs=3 if stage == 2 else 2)
        if state == "burnt":
            s.ellipsoid(cx, cy, z + 1, r * 0.5, r * 0.5, 2, mat("plain", ramp="grey", shade=-7), rough=0.4)
        return s
    extra = {}
    if sp.get("fruit") and season in ("summer", "autumn"):
        extra = dict(accent=sp["fruit"], accent_amt=0.08 if season == "summer" else 0.16, accent_tone=2.0)
    if sp.get("blossom") and season == "spring":
        extra = dict(accent="snow", accent_amt=0.2, accent_tone=1.0)
    fol = _foliage(sp, season, extra)
    if kind == "pine":
        clumps = [(0.0, 0.0, 6, 1.0), (-0.45, 0.25, -2, 0.75), (0.5, -0.2, -4, 0.7), (0.15, 0.55, -8, 0.6),
                  (-0.3, -0.5, 1, 0.65)][:5 if stage else 2]
        for dx, dy, dz, sc in clumps:
            px, py, pz = cx + dx * r, cy + dy * r, cz + dz * k
            s.limb((cx, cy, pz - 6 * k), (px, py, pz), tr * 0.45, tr * 0.3, bark)
            s.ellipsoid(px, py, pz, r * 0.75 * sc, r * 0.75 * sc, rz * k * sc + 1.5, fol, rough=0.45, rough_scale=1.3)
        return s
    n = sp.get("n", 11) if stage else 5
    parts = [(cx, cy, cz, r * 0.72, rz * 0.72)]
    for i in range(n):
        u = rng.uniform(-1, 1)
        a = rng.uniform(0, 2 * math.pi)
        if kind == "column":
            u = rng.uniform(-0.9, 0.95)
        ring = math.sqrt(max(0.0, 1 - u * u))
        f = 0.55 if kind != "airy" else 0.65
        px = cx + math.cos(a) * ring * r * f
        py = cy + math.sin(a) * ring * r * f
        pz = cz + u * rz * f
        cr = r * rng.uniform(0.38, 0.5) * (1.6 if kind == "column" else 1.0)
        parts.append((px, py, pz, cr, cr * 39.2 * (rz / (r * 39.2)) ** 0.5 * 0.95))
    rough = 0.42 if kind == "airy" else 0.28
    if kind == "column":
        parts = [(p[0], p[1], p[2], min(p[3], r * 0.75), p[4]) for p in parts]
    s.blob(parts, fol, rough=rough, rough_scale=1.4 if kind == "airy" else 1.7)
    if kind == "weeping":
        for i in range(14 if stage else 6):
            a = 2 * math.pi * i / (14 if stage else 6) + rng.uniform(-0.2, 0.2)
            px, py = cx + math.cos(a) * r * 0.85, cy + math.sin(a) * r * 0.85
            s.limb([px, py, cz + rz * 0.1], [px * 1.0 + math.cos(a) * 0.03, py + math.sin(a) * 0.03,
                                             z + th * rng.uniform(0.25, 0.55)], 0.03, 0.02, fol)
    return s


def tree(species="oak", season="summer", stage=2, seed=0, state="alive", scale=1.0):
    """A 1x1 scene holding one tree at the cell centre."""
    s = Scene((1, 1), seed)
    return add_tree(s, 0.5, 0.5, species, season, stage, seed, state, scale=scale)
