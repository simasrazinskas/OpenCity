"""
iso_net_rail.py - rail track ground pieces (16 masks, switches, diamond crossing), level crossings with roads,
and underground pipes (water, sewage) in an x-ray trench style.
"""
import numpy as np

import iso_net_pal as P
from iso_net_core import hash2, hx, lerp, render_ground, scale
from iso_net_paths import nearest, paths
from iso_net_roads import road_scene

BALLAST = hx("9A9387")
BALLAST_D = hx("7E776C")
BALLAST_L = hx("B2AB9F")
SLEEPER = hx("6E5038")
SLEEPER_D = hx("553C29")
RAIL = hx("C3C8CE")
GAUGE = 5


def track_paint(U, V, plist, h, col, base_mask=None, ballast=True, rail_col=RAIL):
    """Paint ballast, sleepers and rails for every path. Returns col, alpha, raised."""
    n = U.shape[0]
    alpha = np.zeros(n, bool)
    raised = np.zeros(n, bool)
    sl_any = np.zeros(n, bool)
    rails = np.zeros(n, bool)
    bal = np.zeros(n, bool)
    for f in plist:
        T, Ph, ok = f(U, V)
        bal |= ok & (np.abs(T) < 13)
        sl_any |= ok & (np.abs(T) < 9) & (((Ph * 2.0) % 1.0) < 0.38)
        rails |= ok & (np.abs(np.abs(T) - GAUGE) < 1.0)
    if ballast:
        edge = bal & ~np.zeros(n, bool)
        bcol = np.where((h < 0.22)[:, None], BALLAST_D, np.where((h > 0.85)[:, None], BALLAST_L, BALLAST))
        col = np.where(bal[:, None], bcol, col)
        alpha |= bal
    col = np.where(sl_any[:, None], np.where((h < 0.3)[:, None], SLEEPER_D, SLEEPER), col)
    col = np.where(rails[:, None], rail_col, col)
    raised |= rails
    alpha |= sl_any | rails
    return col, alpha, raised


def rail_scene(mask, junction="switch"):
    plist = paths(mask, junction)

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        h = hash2(u, v, 51)
        col = np.zeros((U.shape[0], 3), np.float32)
        col, alpha, raised = track_paint(U, V, plist, h, col)
        return col, alpha, raised, None, None
    return scene


def crossing_scene(rail_ns, road_cls="street"):
    """Level crossing: road across the cell, rail through the other axis set in concrete panels."""
    road_mask = 10 if rail_ns else 5
    rs = road_scene(road_cls, road_mask, wear=0)
    plist = paths(5 if rail_ns else 10)

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        col, alpha, raised, _, _ = rs(u, v)
        T, Ph, ok = plist[0](U, V)
        a = 20
        on_road = (np.abs((V if rail_ns else U) - 32) < a)
        panel = on_road & (np.abs(T) < 10)
        col = np.where(panel[:, None], np.where((np.abs(((Ph * 4) % 1.0) - 0.5) < 0.06)[:, None], P.CONC_J, P.CONC), col)
        h = hash2(u, v, 52)
        tc, ta, tr = track_paint(U, V, plist, h, col.copy(), ballast=True)
        off_road = ~on_road
        col = np.where((off_road & ta)[:, None], tc, col)
        rails = np.abs(np.abs(T) - GAUGE) < 1.0
        col = np.where(rails[:, None], RAIL, col)
        raised = np.where(off_road, raised | (ta & ~on_road & rails), raised)
        # yellow box warning lines either side of the track on the road
        band = on_road & (np.abs(np.abs(T) - 14) < 1.0)
        col = np.where(band[:, None], P.YELLOW, col)
        alpha = alpha | ta
        return col, alpha, raised, None, None
    return scene


# --------------------------------------------------------------------------- pipes (underground x-ray style)
PIPE = {"water": (hx("4FA3D9"), hx("2F6E9E"), hx("A8DBF5")), "sewage": (hx("8C7A4E"), hx("5E4F30"), hx("C2AE78"))}
EARTH = hx("6A5238")
EARTH_D = hx("54402B")


def pipe_scene(mask, kind):
    plist = paths(mask, junction="cross")
    base, dark, light = PIPE[kind]
    r = 5.0 if kind == "water" else 6.0
    off = 0.0 if kind == "water" else 0.0

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        h = hash2(u, v, 61)
        T, Ph = nearest(U, V, plist)
        T = T - off
        n = U.shape[0]
        trench = np.abs(T) < r + 2
        # cut-away earth with a dither so the ground stays readable underneath (alpha 0/255: checker via hash)
        earth_on = trench & (np.abs(T) >= r)
        pipe = np.abs(T) < r
        t = (T + r) / (2 * r)
        shade = np.where(t < 0.3, 2, np.where(t > 0.75, 1, 0))
        pc = np.where((shade == 2)[:, None], light, np.where((shade == 1)[:, None], dark, base))
        joint = pipe & ((Ph * 1.0) % 1.0 < 0.08)
        pc = np.where(joint[:, None], dark, pc)
        col = np.where(pipe[:, None], pc, np.where((h < 0.3)[:, None], EARTH_D, EARTH))
        alpha = pipe | earth_on
        # junction chamber (manhole box) for 3+ arms
        if bin(mask).count("1") >= 3:
            box = (np.abs(U - 32) < 9) & (np.abs(V - 32) < 9)
            rim = box & ((np.abs(U - 32) >= 7) | (np.abs(V - 32) >= 7))
            col = np.where(box[:, None], np.where(rim[:, None], scale(P.CONC, 0.9), P.CONC), col)
            alpha |= box
            return col, alpha, box, None, None
        return col, alpha, np.zeros(n, bool), None, None
    return scene


def rail_tile(mask, junction="switch"):
    return render_ground(rail_scene(mask, junction))


def crossing_tile(rail_ns):
    return render_ground(crossing_scene(rail_ns))


def pipe_tile(mask, kind):
    return render_ground(pipe_scene(mask, kind), raise_px=2)
