"""Terrain transitions (blob edge masks), map-edge border faces and cliff faces."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.palette import RAMP  # noqa: E402
from isokit.details import paint  # noqa: E402
from isokit.noise import hash2, fbm  # noqa: E402

TRANS_W = 0.36


def pat_transition(c, m):
    """Base material A with material B intruding from the masked sides/corners."""
    a, b = ik.mat(m.p("a", "grass")), ik.mat(m.p("b", "dirt"))
    paint(c, a, np.ones(len(c.x), bool), m.shade)
    d = ik.edge_distance(c.x, c.y, m.p("mask", 0), m.p("corners", 0), 0.08, 3)
    fringe = (fbm(c.sx, c.sy * 2.0, 5.0, 2, 8) - 0.5) * 0.12 + (hash2(c.sx, c.sy, 9) - 0.5) * 0.05
    edge = np.where(d < 9, d, 9.0)
    sel = edge + fringe < TRANS_W
    paint(c, b, sel, m.shade)


def transition_tile(a, b, mask, corners, origin=(0, 0)):
    s = ik.Scene((1, 1))
    s.tile(0, 0, ik.Material("grass", 0.0, pat_transition, a=a, b=b, mask=mask, corners=corners))
    return ik.render_tile(s, origin)


def mask_tile(mask, corners):
    """Binary edge mask (white = material B), usable to blend any terrain pair in the engine.
    Uses the same distance field and fringe as transition_tile (origin 0)."""
    jj, ii = np.mgrid[0:32, 0:64]
    sx, sy = ii + 0.5 - 32, jj + 0.5
    u, v = (sx / 32 + sy / 16) / 2, (sy / 16 - sx / 32) / 2
    d = ik.edge_distance(u, v, mask, corners, 0.08, 3)
    gx, gy = np.floor(sx).astype(int), np.floor(sy).astype(int)
    fringe = (fbm(gx, gy * 2.0, 5.0, 2, 8) - 0.5) * 0.12 + (hash2(gx, gy, 9) - 0.5) * 0.05
    on = (d + fringe < TRANS_W) & ik.diamond_mask()
    img = np.zeros((32, 64, 4), np.uint8)
    img[ik.diamond_mask()] = (0, 0, 0, 255)
    img[on] = (255, 255, 255, 255)
    return ik.Sprite(img, 32, 16, None, (1, 1))


def pat_strata(c, m):
    """Soil cross-section: grass lip, topsoil, subsoil bands, rock at the bottom (v = height px)."""
    top = c.v1 - c.v
    lip = top < 2
    c.ramp[lip] = RAMP["grass"]
    c.tone[lip] = -2.0 - m.shade + 8.0 * 0.0
    soil = (top >= 2) & (top < 8)
    c.tone[soil] -= 0.8
    rock = top > 14 + (fbm(c.sx, c.sy, 6.0, 2, 4) - 0.5) * 6
    c.ramp[rock] = RAMP["stone"]
    c.tone[rock] -= 0.5
    c.tone += (fbm(c.sx, c.sy * 2.0, 4.0, 2, 5) - 0.5) * 1.2
    h = hash2(c.sx, c.sy, 6)
    c.tone[h < 0.06] += 1.2
    c.tone[h > 0.95] -= 1.4


STRATA = ik.Material("wood", -0.5, pat_strata)


def edge_face(side, depth=24, water=False):
    """Map-edge cross-section under a border cell: side 'S' (+Y face) or 'E' (+X face)."""
    s = ik.Scene((1, 1))
    top = -5.0 if water else 0.0
    mat = STRATA
    if side == "S":
        s.poly([(0, 1, -depth), (1, 1, -depth), (1, 1, top), (0, 1, top)], mat, outward=(0, 1, 0))
        if water:
            s.poly([(0, 1, -5), (1, 1, -5), (1, 1, 0), (0, 1, 0)], ik.mat("plain", ramp="water", shade=1), outward=(0, 1, 0))
    else:
        s.poly([(1, 0, -depth), (1, 1, -depth), (1, 1, top), (1, 0, top)], mat, outward=(1, 0, 0))
        if water:
            s.poly([(1, 0, -5), (1, 1, -5), (1, 1, 0), (1, 0, 0)], ik.mat("plain", ramp="water", shade=1), outward=(1, 0, 0))
    return ik.render(s, outline=None, creases=0)


def cliff_face(side, h=16, mat="rock"):
    """Raised-terrain cliff (future height support): vertical rock face of height h under a cell edge."""
    s = ik.Scene((1, 1))
    m = ik.mat(mat)
    if side == "S":
        s.poly([(0, 1, 0), (1, 1, 0), (1, 1, h), (0, 1, h)], m, outward=(0, 1, 0))
    else:
        s.poly([(1, 0, 0), (1, 1, 0), (1, 1, h), (1, 0, h)], m, outward=(1, 0, 0))
    return ik.render(s, outline=None, creases=0)


def raised_block(h=8, top="grass"):
    """A cell raised h px with rock sides (preview of height support)."""
    s = ik.Scene((1, 1))
    s.box(0, 0, 0, 1, 1, h, ik.mat("rock", shade=-1.0), top=top)
    return ik.render(s, outline=None, creases=0)
