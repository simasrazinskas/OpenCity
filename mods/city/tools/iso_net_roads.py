"""
iso_net_roads.py - NET iso road surfaces: every class, 16 connection masks, two-way and one-way, wear states.

Geometry is in q units (1 q = 1/64 cell = 0.25 m; markings 2 q wide land exactly on 2-pixel 2:1 stairs).
A road cell's cross-section from the centreline: asphalt half-width `a`, then `side` q of sidewalk/apron/verge.
Masks: bit0 N (v=0 edge, screen upper-right), bit1 E (u=1, lower-right), bit2 S (v=1, lower-left), bit3 W (u=0).
"""
import math

import numpy as np

import iso_net_pal as P
from iso_net_core import hash2, lerp, render_ground, scale

N, E, S, W = 1, 2, 4, 8
CURVES = {N | E: (64, 0), N | W: (0, 0), S | E: (64, 64), S | W: (0, 64)}

# two-way (tw) and one-way (ow) cross-sections. lines: list of (T centre, kind) for |T| (two-way) or T (one-way).
CLASSES = {
    "street": dict(tw=dict(a=20, lines=[(0, "ydash")]), ow=dict(a=20, lines=[(0, "wdash")]),
                   side=12, edge="walk", cross=True, manholes=True),
    "alley": dict(tw=dict(a=12, lines=[]), ow=dict(a=12, lines=[]), side=20, edge="apron", cross=False, manholes=True),
    "gravel": dict(tw=dict(a=14, lines=[]), ow=dict(a=14, lines=[]), side=4, edge="fringe", cross=False, manholes=False),
    "avenue": dict(tw=dict(a=26, lines=[(2, "ysolid"), (14, "wdash")]), ow=dict(a=22, lines=[(0, "wdash")]),
                   side=None, edge="walk", cross=True, manholes=True),
    "boulevard": dict(tw=dict(a=27, lines=[(16, "wdash")], median=5), ow=dict(a=24, lines=[(-8, "wdash"), (8, "wdash")]),
                      side=None, edge="walk", cross=True, manholes=True),
    "highway": dict(tw=dict(a=28, lines=[(4, "ysolid"), (15, "wdash"), (26, "wsolid")], barrier=2),
                    ow=dict(a=26, lines=[(-22, "ysolid"), (-7, "wdash"), (7, "wdash"), (22, "wsolid")]),
                    side=None, edge="verge", cross=False, manholes=False, asph="hwy"),
}
# lane centres for LIFE (q from the centreline, + = right of travel); see report
ORDER = ["alley", "gravel", "street", "avenue", "boulevard", "highway"]


def section(cls, oneway):
    c = CLASSES[cls]
    s = dict(c["ow" if oneway else "tw"])
    s["side"] = 32 - s["a"]
    s["edge"] = c["edge"]
    if c["edge"] == "fringe":
        s["side"] = 4
    elif c["edge"] == "verge":
        s["side"] = 32 - s["a"]
    s["cross"] = c["cross"]
    s["manholes"] = c["manholes"]
    s["asph"] = c.get("asph", "std")
    s["oneway"] = oneway
    return s


def round_union(d1, d2, r):
    m = np.maximum(r, np.minimum(d1, d2))
    return m - np.hypot(np.maximum(r - d1, 0), np.maximum(r - d2, 0))


def arm_capsule(U, V, arm):
    dx, dy = U - 32, V - 32
    if arm == N:
        return np.where(dy <= 0, np.abs(dx), np.hypot(dx, dy))
    if arm == S:
        return np.where(dy >= 0, np.abs(dx), np.hypot(dx, dy))
    if arm == E:
        return np.where(dx >= 0, np.abs(dy), np.hypot(dx, dy))
    return np.where(dx <= 0, np.abs(dy), np.hypot(dx, dy))


def centre_dist(U, V, mask):
    """Distance (q) to the road centreline network of the cell, plus path coords: T (signed offset), P (phase 0..1
    along the path for dashes, global), region (0 box/plain, 1..8 arm bit, 16 curve)."""
    if mask in CURVES:
        cx, cy = CURVES[mask]
        R = np.hypot(U - cx, V - cy)
        ang = np.arctan2(np.abs(V - cy), np.abs(U - cx))  # 0 at the E/W edge side, pi/2 at N/S edge side
        T = R - 32
        P = (1 - ang / (math.pi / 2)) * 3.0  # three dash periods along the quarter arc
        return np.abs(T), T, P, np.full(U.shape, 16)
    if mask == 0:
        d = np.hypot(U - 32, V - 32)
        return d, U - 32, V / 16.0, np.zeros(U.shape, int)
    ns = [arm_capsule(U, V, a) for a in (N, S) if mask & a]
    ew = [arm_capsule(U, V, a) for a in (E, W) if mask & a]
    dns = np.minimum.reduce(ns) if ns else None
    dew = np.minimum.reduce(ew) if ew else None
    if dns is None:
        d = dew
    elif dew is None:
        d = dns
    else:
        d = np.minimum(dns, dew)
    # path coords per arm region
    T = np.zeros(U.shape)
    P = np.zeros(U.shape)
    reg = np.zeros(U.shape, int)
    ax, ay = U - 32, V - 32
    vert = np.abs(ax) <= np.abs(ay)
    for arm, sel, t, p in ((N, vert & (ay < 0), ax, V), (S, vert & (ay >= 0), -ax, V),
                           (E, ~vert & (ax >= 0), ay, U), (W, ~vert & (ax < 0), -ay, U)):
        T = np.where(sel, t, T)
        P = np.where(sel, p / 16.0, P)
        reg = np.where(sel, arm, reg)
    if mask in (N | S, E | W) or bin(mask).count("1") == 1:
        if mask & (N | S):
            T, P = U - 32, V / 16.0
        else:
            T, P = V - 32, U / 16.0
        reg = np.full(U.shape, 32)
    return d, T, P, reg


def asphalt_sdf(U, V, mask, a, fillet):
    if mask in CURVES:
        cx, cy = CURVES[mask]
        return np.abs(np.hypot(U - cx, V - cy) - 32) - a
    if mask == 0:
        return np.hypot(U - 32, V - 32) - a
    ns = [arm_capsule(U, V, x) - a for x in (N, S) if mask & x]
    ew = [arm_capsule(U, V, x) - a for x in (E, W) if mask & x]
    dns = np.minimum.reduce(ns) if ns else None
    dew = np.minimum.reduce(ew) if ew else None
    if dns is None:
        return dew
    if dew is None:
        return dns
    return round_union(dns, dew, fillet)


def arm_coords(U, V, arm):
    """S = distance from the cell centre along the arm, T = offset to the right of outbound travel."""
    if arm == N:
        return 32 - V, U - 32
    if arm == S:
        return V - 32, 32 - U
    if arm == E:
        return U - 32, V - 32
    return 32 - U, 32 - V


def line_on(T, P, centre, kind, faded):
    hit = np.abs(T - centre) < 1.0
    if kind.endswith("dash"):
        hit &= (P % 1.0) < 0.5
    return hit


def road_scene(cls, mask, oneway=False, wear=1, bus=False, bike=False, extra=None, stop_line=True):
    s = section(cls, oneway)
    a, side = s["a"], s["side"]
    asph_base = P.HWY if s["asph"] == "hwy" else P.ASPH
    asph_l = P.HWY_L if s["asph"] == "hwy" else P.ASPH_L
    junction = bin(mask).count("1") >= 3
    fillet = min(8, side)

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        n = U.shape[0]
        d = asphalt_sdf(U, V, mask, a, fillet)
        _, T, Ph, reg = centre_dist(U, V, mask)
        h = hash2(u, v, 7)
        col = np.zeros((n, 3), np.float32)
        alpha = np.ones(n, bool)
        raised = np.zeros(n, bool)
        road = d < 0
        # ---- asphalt with grain
        grain = np.where(h < 0.06, 1, np.where(h > 0.97, 2, 0))
        base = np.where(grain[:, None] == 1, asph_l, np.where(grain[:, None] == 2, P.ASPH_D, asph_base))
        if cls == "gravel":
            g = np.where(h < 0.07, 1, np.where(h > 0.94, 2, 0))
            base = np.where(g[:, None] == 1, P.GRAVEL_L, np.where(g[:, None] == 2, P.GRAVEL_D, P.GRAVEL))
            rut = (np.abs(np.abs(T) - 7) < 2.0) & (h > 0.12)
            base = np.where(rut[:, None], P.RUT, base)
        col[:] = base
        # ---- wear: patches and cracks (deterministic, in cell space)
        if wear >= 1 and cls not in ("gravel",):
            pt = ((np.abs(U - 41) < 5) & (np.abs(V - 12) < 3)) | ((np.abs(U - 22) < 3) & (np.abs(V - 47) < 5))
            col = np.where((pt & road)[:, None], P.ASPH_PATCH if wear == 1 else P.ASPH_PATCH_OLD, col)
        if wear >= 2 and cls not in ("gravel",):
            ck = hash2(np.floor(U / 3), np.floor(V / 3), 11)
            crack = (ck < 0.06) & (np.abs(((U + 2 * V) % 7) - 3.5) < 0.6)
            crack |= (ck > 0.97)
            col = np.where((crack & road)[:, None], P.CRACK, col)
        # ---- lane paint
        lanes_paint = np.zeros(n, bool)
        yellow = np.zeros(n, bool)
        box = junction & (np.abs(U - 32) < a) & (np.abs(V - 32) < a)
        if bin(mask).count("1") == 1:
            only = mask
            Sx, _ = arm_coords(U, V, only)
            capzone = Sx < 6
        else:
            capzone = np.zeros(n, bool)
        allow = road & ~box & ~capzone & (reg != 0 if mask != 0 else np.zeros(n, bool))
        TT = T if s["oneway"] else np.abs(T)
        for centre, kind in s["lines"]:
            hit = line_on(TT, Ph, centre, kind, wear >= 2) & allow
            if kind[0] == "y":
                yellow |= hit
            else:
                lanes_paint |= hit
        if "median" in s:
            med = (np.abs(T) < s["median"]) & road & ~box
            raised |= med
            mg = np.where(np.abs(T) >= s["median"] - 1.5, 1, 0)
            col = np.where(med[:, None], np.where(mg[:, None] == 1, P.KERB, np.where(h[:, None] < 0.3, P.GRASS_D, P.GRASS)), col)
        if "barrier" in s:
            bar = (np.abs(T) < s["barrier"]) & road & ~box
            raised |= bar
            col = np.where(bar[:, None], np.where((T < 0)[:, None], scale(P.KERB, 0.95), P.KERB), col)
        # ---- special lanes (addon paint on the outermost lane)
        lane_w = 14
        if bus:
            outer = road & ~box & (np.abs(T) > a - lane_w) & (np.abs(T) < a - 1)
            if s["oneway"]:
                outer = road & ~box & (T > a - lane_w) & (T < a - 1)
            col = np.where(outer[:, None], lerp(P.BUS_RED, col, 0.25), col)
        if bike:
            bl = road & ~box & (np.abs(T) > a - 6) & (np.abs(T) < a - 1)
            col = np.where(bl[:, None], P.BIKE_GREEN, col)
        # ---- junction crosswalks and stop lines
        if junction and s["cross"]:
            for arm in (N, E, S, W):
                if not mask & arm:
                    continue
                Sx, Tx = arm_coords(U, V, arm)
                zebra = crosswalk(Sx, Tx, a) & road
                lanes_paint |= zebra
                if stop_line:
                    stop = road & (Tx < -1) & (Tx > -a + 3) & (Sx >= a + 1) & (Sx < a + 3)
                    lanes_paint |= stop
        paint_w = P.PAINT_OLD if wear >= 2 else P.WHITE
        fade = (hash2(u, v, 3) < (0.35 if wear >= 2 else 0.0))
        col = np.where((lanes_paint & ~fade)[:, None], paint_w, col)
        col = np.where((yellow & ~fade)[:, None], scale(P.YELLOW, 0.85) if wear >= 2 else P.YELLOW, col)
        # ---- manholes and drains
        if s["manholes"] and mask != 0:
            for (mu, mv) in manhole_spots(mask, a):
                r = np.hypot((U - mu) * 1.0, (V - mv) * 1.0)
                ring = road & (r < 3.2)
                col = np.where(ring[:, None], np.where((r < 1.8)[:, None], P.IRON_L, P.IRON), col)
        # ---- sidewalk / apron / verge
        off = ~road
        walk = off & (d < side)
        if s["edge"] == "walk":
            kerb = walk & (d < 2)
            joint = walk & ((np.abs(((U + 4) % 8) - 4) < 0.5) | (np.abs(((V + 4) % 8) - 4) < 0.5))
            wc = np.where(h[:, None] < 0.03, P.WALK_D, P.WALK)
            col = np.where(walk[:, None], np.where(kerb[:, None], P.KERB, np.where(joint[:, None], P.WALK_J, wc)), col)
            raised |= walk
            alpha = road | walk
        elif s["edge"] == "apron":
            joint = walk & ((np.abs((U % 16) - 8) < 0.5) | (np.abs((V % 16) - 8) < 0.5))
            gutter = walk & (d < 1.5)
            cc = np.where(h[:, None] < 0.1, scale(P.CONC, 0.94), P.CONC)
            col = np.where(walk[:, None], np.where(gutter[:, None], P.CONC_J, np.where(joint[:, None], P.CONC_J, cc)), col)
            alpha = road | walk
        elif s["edge"] == "fringe":
            keep = walk & (hash2(u, v, 5) > d / side)
            col = np.where(keep[:, None], P.GRAVEL_D, col)
            alpha = road | keep
        else:  # highway verge: gravel shoulder strip then transparent
            sh = off & (d < 4)
            col = np.where(sh[:, None], np.where(h[:, None] < 0.2, scale(P.SHOULDER, 0.9), P.SHOULDER), col)
            alpha = road | sh
        if extra is not None:
            ctx = dict(U=U, V=V, T=T, P=Ph, d=d, road=road, box=box, mask=mask, a=a, side=side, h=h, sec=s)
            col, alpha, raised = extra(ctx, col, alpha, raised)
        return col, alpha, raised, None, None

    return scene


XWALK = "ladder"


def crosswalk(Sx, Tx, a):
    """Pedestrian crossing paint across an arm at the junction box edge (Sx from the centre, Tx across)."""
    span = np.abs(Tx) < a - 5
    if XWALK == "ladder":
        return span & (((Sx >= a - 9) & (Sx < a - 7)) | ((Sx >= a - 3) & (Sx < a - 1)))
    if XWALK == "zebra2":
        return span & (Sx >= a - 9) & (Sx < a - 1) & ((np.floor((Tx + 64) / 2) % 2) == 0)
    if XWALK == "zebra4":
        return span & (Sx >= a - 9) & (Sx < a - 1) & ((np.floor((Tx + 66) / 4) % 2) == 0)
    return span & (Sx >= a - 9) & (Sx < a - 1) & ((np.floor((Tx + 64) / 3) % 2) == 0)


def manhole_spots(mask, a):
    spots = []
    if mask == N | S:
        spots.append((32 + a / 2, 18))
    elif mask == E | W:
        spots.append((46, 32 - a / 2))
    elif bin(mask).count("1") >= 3:
        spots.append((32 - a / 2, 32 + a / 2))
    return spots


def road_tile(cls, mask, oneway=False, wear=1, **kw):  # kw: bus, bike, extra, stop_line
    return render_ground(road_scene(cls, mask, oneway, wear, **kw))


def mask_name(mask):
    return "".join("1" if mask & b else "0" for b in (N, E, S, W))


def mask_label(mask):
    names = [nm for b, nm in ((N, "N"), (E, "E"), (S, "S"), (W, "W")) if mask & b]
    return "+".join(names) if names else "isolated"
