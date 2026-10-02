"""CIVIC toolkit, part 3: model registry, shared states (inactive, construction, burnt, rubble) and the
per-building render pipeline that writes PNGs + returns Figma manifest items.

A model is a function `fn(st) -> ik.Scene`. `st` carries: frame (0..3), season, inactive, fill (0..1 or
None), ups (set of upgrade ids), variant (int), night. Models skip smoke / stop machines when
st.inactive is set; the pipeline also dims inactive renders.
"""
import copy
import os
from dataclasses import dataclass, field, replace

import numpy as np

from iso_civic_kit import ik, M, P, rect, lot, beam, lattice, pole, fence
from iso_civic_props import crate_stack, heap

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "design", "iso", "civic")

REG = {}


@dataclass
class St:
    frame: int = 0
    season: str = "summer"
    inactive: bool = False
    fill: float = None
    ups: frozenset = field(default_factory=frozenset)
    variant: int = 0
    night: bool = False


@dataclass
class Spec:
    name: str
    fn: object
    fp: tuple
    cat: str
    title: str
    front: bool = False          # has a clear front: render 4 facings
    anim: int = 0                # animation frames (0 = static)
    ups: tuple = ()              # ((id, label), ...)
    fill: bool = False           # landfill / cemetery fill levels
    power: bool = True           # can go inactive (no power / water)
    build: bool = True           # construction + burnt states
    variants: tuple = ()         # ((label, St overrides dict), ...) extra named states (growth stages ...)
    note: str = None
    order: int = 0


def model(name, fp, cat, title, **kw):
    def deco(fn):
        REG[name] = Spec(name, fn, tuple(fp), cat, title, order=len(REG), **kw)
        return fn
    return deco


# ---------------------------------------------------------------- post-process states
def dim(img, amt=0.5, dark=0.85):
    """Inactive look: desaturate + darken, re-quantized to the palette (alpha kept)."""
    out = img.copy()
    a = img[..., 3] > 0
    rgb = img[..., :3].astype(np.float64)
    g = rgb @ np.array([0.30, 0.59, 0.11])
    rgb = (rgb * (1 - amt) + g[..., None] * amt) * dark
    q = ik.nearest(rgb[a])
    out[..., :3][a] = q
    return out


def char_img(img, seed=0):
    """Burnt look from a normal render: near-black soot keeping the light/shade structure, blotchy
    ash, a few glowing embers low on the walls."""
    from isokit.noise import hash2, value2
    out = img.copy()
    a = img[..., 3] > 0
    h, w = a.shape
    ys, xs = np.mgrid[0:h, 0:w]
    rgb = img[..., :3].astype(np.float64)
    lum = rgb @ np.array([0.30, 0.59, 0.11])
    n = value2(xs / 6.0, ys / 4.0, seed + 5)
    v = 10 + lum * 0.30 + (n - 0.5) * 26 + (hash2(xs, ys, seed) - 0.5) * 8
    col = np.stack([v, v, v * 1.02], -1)
    ember = a & (n > 0.78) & (hash2(xs, ys, seed + 9) > 0.9)
    out[..., :3][a] = ik.nearest(col[a], ramps=["grey", "slate"])
    out[..., :3][ember] = ik.color("terra", 8)
    return out


def _pat_char(c, m):
    from isokit.noise import hash2, value2
    c.tone += (hash2(c.sx, c.sy, c.seed) - 0.5) * 1.6
    n = value2(c.sx / 5.0, c.sy / 3.0, c.seed)
    c.tone += (n - 0.5) * 2.0
    hot = (n > 0.82) & (c.v < 6) if c.kind != "ground" else (n > 0.86)
    if c.night:
        c.emit |= hot
        c.eramp[hot] = ik.palette.RAMP["terra"]
        c.eshade[hot] = 9.0


CHAR = ik.Material("grey", -3.6, _pat_char, dither=0.7)
CHAR_ROOF = ik.Material("slate", -4.2, _pat_char, dither=0.7)
ASH = ik.Material("grey", -2.2, _pat_char, dither=0.8)


def burnt(scene, seed=0):
    """Charred copy of a scene: every surface swapped for soot/char, ground for ash."""
    out = ik.Scene(scene.footprint, seed)
    for it in scene._items:
        it2 = copy.copy(it)
        kind = getattr(it, "kind", "")
        if kind == "ground":
            it2.mat = ASH
        elif kind in ("top", "roof"):
            it2.mat = CHAR_ROOF
        else:
            it2.mat = CHAR
        out.add(it2)
    return out


def height_of(spr):
    fx, fy = spr.footprint
    top = np.where(spr.img[..., 3].any(1))[0][0]
    return max(10, spr.ay - (fx + fy) * 8 - top)


def construction(fp, hmax, stage, seed=0):
    """Generic site for a footprint: stage 1 foundations + rebar, stage 2 concrete frame in scaffolding with
    a tower crane. hmax = finished height in px."""
    s = ik.Scene(fp, seed)
    fx, fy = fp
    lot(s, "dirt")
    rect(s, 0.15, 0.15, fx - 0.15, fy - 0.15, "concrete_ground", layer=1)
    conc = M("concrete", shade=0.5)
    hoard = P("yellow", 0.5, snow=False)
    fence(s, [(0.04, 0.04), (fx - 0.04, 0.04), (fx - 0.04, fy - 0.04), (0.04, fy - 0.04)], 4, hoard, closed=True)
    xs = np.linspace(0.25, fx - 0.25, max(2, int(fx * 3)))
    ys = np.linspace(0.25, fy - 0.25, max(2, int(fy * 3)))
    if stage == 1:
        s.box(0.2, 0.2, 0, fx - 0.4, fy - 0.4, 2, conc)
        for x in xs:
            for y in ys:
                pole(s, x, y, 9, P("terra", -2, snow=False), w=0.02, z=2)
        crate_stack(s, fx - 0.42, 0.08, 1, 2, 1, 0.08, ramps=("wood",))
        heap(s, 0.3, fy - 0.3, 0.16, 7, "gravel")
        s.box(0.08, 0.1, 0, 0.3, 0.14, 7, M("metal", ramp="water", shade=1), top=P("grey", 2))
        return s
    h = max(ik.STOREY * 2, int(hmax * 0.65) // ik.STOREY * ik.STOREY)
    nfl = h // ik.STOREY
    s.box(0.2, 0.2, 0, fx - 0.4, fy - 0.4, 2, conc)
    for k in range(1, nfl + 1):
        z = 2 + k * ik.STOREY
        s.box(0.22, 0.22, z - 1.5, fx - 0.44, fy - 0.44, 1.5, conc)
    for x in xs:
        for y in ys:
            s.box(x - 0.02, y - 0.02, 2, 0.04, 0.04, nfl * ik.STOREY, conc)
    scaf = P("grey", 3, snow=False)
    plank = P("wood", 2, snow=False)
    # scaffolding along the two visible faces (+X, +Y)
    for k in range(nfl + 1):
        z = 2 + k * ik.STOREY
        beam(s, (0.18, fy - 0.16, z), (fx - 0.16, fy - 0.16, z), plank, w=0.03, hpx=1)
        beam(s, (fx - 0.16, 0.18, z), (fx - 0.16, fy - 0.16, z), plank, w=0.03, hpx=1)
    for x in np.linspace(0.18, fx - 0.16, max(3, int(fx * 4))):
        pole(s, x, fy - 0.15, nfl * ik.STOREY + 4, scaf, w=0.012)
    for y in np.linspace(0.18, fy - 0.16, max(3, int(fy * 4))):
        pole(s, fx - 0.15, y, nfl * ik.STOREY + 4, scaf, w=0.012)
    # tower crane at the back corner, jib along +x inside the footprint
    cy_ = 0.12
    ch = h + 26
    yel = P("yellow", 1.5, snow=False)
    lattice(s, 0.08, cy_ - 0.05, 0.08, 0.08, ch, yel, levels=max(4, int(ch / 10)), legs=True)
    s.box(0.04, cy_ - 0.06, ch, fx - 0.12, 0.1, 2, yel)
    s.box(0.04, cy_ - 0.06, ch + 2, 0.14, 0.1, 5, P("snow", 0, snow=False))
    s.box(0.18, cy_ - 0.05, ch - 4, 0.1, 0.08, 4, P("grey", -2, snow=False))
    hook_x = fx * 0.62
    pole(s, hook_x, cy_, 18, P("grey", -2, snow=False), w=0.006, z=ch - 18)
    s.box(hook_x - 0.03, cy_ - 0.03, ch - 22, 0.06, 0.06, 4, P("grey", 0, snow=False))
    return s


def rubble(variant=0):
    """1x1 debris tile of a burnt / demolished building."""
    s = ik.Scene((1, 1), 40 + variant)
    lot(s, ASH)
    rng = np.random.default_rng(100 + variant)
    mats = [M("brick", shade=-1.5), M("concrete", shade=-1.0), CHAR, M("stone", shade=-1)]
    for k in range(5 + variant):
        cx, cy = rng.uniform(0.25, 0.75, 2)
        heap(s, cx, cy, rng.uniform(0.12, 0.22), rng.uniform(4, 9), mats[(k + variant) % len(mats)], rough=0.5)
    for k in range(3):
        x, y = rng.uniform(0.2, 0.7, 2)
        L = rng.uniform(0.15, 0.3)
        beam(s, (x, y, rng.uniform(1, 6)), (min(x + L, 0.95), y + rng.uniform(-0.1, 0.1), rng.uniform(0, 3)),
             P("wood", -2), w=0.03, hpx=1.5)
    if variant % 2:
        s.box(0.15, 0.2, 0, 0.06, 0.25, 12, CHAR)
        s.box(0.15, 0.2, 0, 0.3, 0.05, 8, CHAR)
    return s
