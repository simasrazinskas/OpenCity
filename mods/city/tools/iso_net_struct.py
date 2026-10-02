"""
iso_net_struct.py - bridges (deck, piers, railings, abutment ramps) and the elevated highway concept.

A bridge piece = isokit structure (girder, piers, far railing) + NET road surface lifted to deck height
+ near railing on top. Canvas 64 x (32 + H), anchor (32, H + 16) like every NET prop.
"""
import numpy as np

import iso_net_pal as P
import isokit as ik
from iso_net_core import over, render_ground_z
from iso_net_kit import CONCRETE, GALV, STEEL, flat, render_fixed
from iso_net_roads import road_scene

DECK_Z = 8
ELEV_Z = 24
H = 48
GIRDER = flat("slate", 0.5)
GIRDER_RED = flat("brick", 0.0)
STONE = ik.mat("stone")


def _xy(axis, along, across):
    """Map (along, across) to (x, y): axis 'ns' runs along y, 'ew' along x."""
    return (across, along) if axis == "ns" else (along, across)


def _box(s, axis, a0, a1, c0, c1, z, dz, mat):
    if axis == "ns":
        s.box(c0, a0, z, c1 - c0, a1 - a0, dz, mat)
    else:
        s.box(a0, c0, z, a1 - a0, c1 - c0, dz, mat)


def railing(s, axis, across, z0, z1=None, kind="rail", n=8):
    """Posts + top rail along a deck edge (across = 0.03 far or 0.97 near). z1: deck height at the far end (ramps)."""
    z1 = z0 if z1 is None else z1
    if kind == "parapet":
        for i in range(n):
            a = i / n
            zz = z0 + (z1 - z0) * (a + 0.5 / n)
            _box(s, axis, a, a + 1.0 / n, across - 0.02, across + 0.02, zz - 0.5, 3.5, CONCRETE)
        return
    for i in range(n):
        a = (i + 0.5) / n
        zz = z0 + (z1 - z0) * a
        x, y = _xy(axis, a, across)
        s.box(x - 0.008, y - 0.008, zz, 0.016, 0.016, 4, STEEL)
    for i in range(n):
        a = i / n
        zz = z0 + (z1 - z0) * (a + 0.5 / n)
        _box(s, axis, a, a + 1.0 / n, across - 0.01, across + 0.01, zz + 3.5, 1.0, GALV)


def _deck_surface(cls, axis, oneway, z0, z1=None):
    mask = 5 if axis == "ns" else 10
    sc = road_scene(cls, mask, oneway, wear=0)
    return P.quantize(render_ground_z(sc, z0, z1, "v" if axis == "ns" else "u", H=H))


def bridge_piece(cls="street", axis="ns", piece="span", oneway=False, rail="rail", girder=GIRDER):
    """piece: span | pier | end0 (land at the 0 edge, rises toward 1) | end1 (land at the 1 edge)."""
    z = DECK_Z
    s_back = ik.Scene(footprint=(1, 1), seed=21)
    s_front = ik.Scene(footprint=(1, 1), seed=22)
    near = 0.97
    far = 0.03
    if piece in ("span", "pier"):
        _box(s_back, axis, 0, 1, 0, 1, z - 5, 5, girder)
        if piece == "pier":
            _box(s_back, axis, 0.42, 0.58, 0.14, 0.86, -8, z - 3, CONCRETE)
            x0, y0 = _xy(axis, 0.5, 0.14)
            x1, y1 = _xy(axis, 0.5, 0.86)
            s_back.cylinder(x0, y0, -8, 0.08, z - 3 + 8, CONCRETE, segs=12)
            s_back.cylinder(x1, y1, -8, 0.08, z - 3 + 8, CONCRETE, segs=12)
        railing(s_back, axis, far, z, kind=rail)
        railing(s_front, axis, near, z, kind=rail)
        surf = _deck_surface(cls, axis, oneway, z)
    else:
        lo_first = piece == "end0"
        za, zb = (0, z) if lo_first else (z, 0)
        # abutment wedge: two side triangles and the high end wall, stone
        for c in (0.0, 1.0):
            pa = _xy(axis, 0, c) + (za,)
            pb = _xy(axis, 1, c) + (zb,)
            qa = _xy(axis, 0, c) + (0,)
            qb = _xy(axis, 1, c) + (0,)
            out = (1, 0, 0) if axis == "ns" else (0, 1, 0)
            if c == 0.0:
                out = tuple(-o for o in out)
            s_back.quad(qa, qb, pb, pa, STONE, outward=out)
        hi = 1.0 if lo_first else 0.0
        e = [_xy(axis, hi, 0) + (0,), _xy(axis, hi, 1) + (0,), _xy(axis, hi, 1) + (z,), _xy(axis, hi, 0) + (z,)]
        outw = (0, 1 if lo_first else -1, 0) if axis == "ns" else (1 if lo_first else -1, 0, 0)
        s_back.quad(*e, STONE, outward=outw)
        railing(s_back, axis, far, za, zb, kind=rail)
        railing(s_front, axis, near, za, zb, kind=rail)
        surf = _deck_surface(cls, axis, oneway, za, zb)
    w, h = 64, 32 + H
    img = render_fixed(s_back, w, h, 32, H + 16)
    over(img, surf, 0, 0)
    over(img, render_fixed(s_front, w, h, 32, H + 16), 0, 0)
    return img


def overpass_piece(axis="ns", piece="span", oneway=True):
    """Elevated highway: deck at 24 px on T-piers; piece: span | pier | rampA0/rampB0 (rising toward 1) | rampA1/rampB1."""
    s_back = ik.Scene(footprint=(1, 1), seed=23)
    s_front = ik.Scene(footprint=(1, 1), seed=24)
    z = ELEV_Z
    if piece in ("span", "pier"):
        _box(s_back, axis, 0, 1, 0.02, 0.98, z - 4, 4, CONCRETE)
        if piece == "pier":
            x, y = _xy(axis, 0.5, 0.5)
            s_back.cylinder(x, y, 0, 0.09, z - 7, CONCRETE, segs=14)
            _box(s_back, axis, 0.42, 0.58, 0.08, 0.92, z - 7, 3, CONCRETE)
        z0 = z1 = z
    else:
        rising = piece.endswith("0")
        lo, hi = (0, z / 2) if piece.startswith("rampA") else (z / 2, z)
        z0, z1 = (lo, hi) if rising else (hi, lo)
        for c in (0.02, 0.98):
            pa = _xy(axis, 0, c) + (z0,)
            pb = _xy(axis, 1, c) + (z1,)
            qa = _xy(axis, 0, c) + (0,)
            qb = _xy(axis, 1, c) + (0,)
            out = (1, 0, 0) if axis == "ns" else (0, 1, 0)
            if c < 0.5:
                out = tuple(-o for o in out)
            s_back.quad(qa, qb, pb, pa, ik.mat("concrete"), outward=out)
    railing(s_back, axis, 0.04, z0, z1, kind="parapet")
    railing(s_front, axis, 0.96, z0, z1, kind="parapet")
    mask = 5 if axis == "ns" else 10
    sc = road_scene("highway", mask, oneway, wear=0)
    surf = P.quantize(render_ground_z(sc, z0, None if z0 == z1 else z1, "v" if axis == "ns" else "u", H=H))
    w, h = 64, 32 + H
    img = render_fixed(s_back, w, h, 32, H + 16)
    over(img, surf, 0, 0)
    over(img, render_fixed(s_front, w, h, 32, H + 16), 0, 0)
    return img
