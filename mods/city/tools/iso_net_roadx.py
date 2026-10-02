"""
iso_net_roadx.py - road extras on the ground plane: paired-carriageway medians, one-way arrows, class transitions,
junction-control paint (stop, yield, mini roundabout, highway ramp gore), roundabout islands, map-edge highway,
bus/bike lanes, tram track in the road.
"""
import numpy as np

import iso_net_pal as P
from iso_net_core import hash2, lerp, render_ground, rotate_scene, scale
from iso_net_roads import (CLASSES, E, N, S, W, arm_coords, road_scene)

MEDIAN_W = {"avenue": 10, "boulevard": 8, "highway": 6}


# --------------------------------------------------------------------------- medians (sideBlock overlay)
def median_scene(cls, sb):
    m = MEDIAN_W[cls]

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        n = U.shape[0]
        dist = np.full(n, 99.0)
        for bit, dd in ((N, V), (E, 64 - U), (S, 64 - V), (W, U)):
            if sb & bit:
                dist = np.minimum(dist, dd)
        band = dist < m
        h = hash2(u, v, 21)
        col = np.zeros((n, 3), np.float32)
        raised = band.copy()
        if cls == "avenue":
            kerb = dist >= m - 2
            shrub = (h < 0.45) & (dist < m - 3)
            col[:] = np.where(kerb[:, None], P.KERB, np.where(shrub[:, None], np.where((h < 0.12)[:, None], P.GRASS_L, P.GRASS_D), P.GRASS))
            flower = (h > 0.93) & (dist < m - 3)
            col = np.where(flower[:, None], np.array([214, 96, 120], np.float32), col)
        elif cls == "boulevard":
            kerb = dist >= m - 2
            col[:] = np.where(kerb[:, None], P.KERB, np.where((h < 0.25)[:, None], P.GRASS_D, P.GRASS))
        else:
            wall = dist < 2.5
            col[:] = np.where(wall[:, None], np.where((dist < 1)[:, None], P.KERB, scale(P.KERB, 0.9)),
                              np.where((h < 0.2)[:, None], scale(P.SHOULDER, 0.9), P.SHOULDER))
            raised = band & wall
        return col, band, raised, None, None

    return scene


def median_tile(cls, sb):
    return render_ground(median_scene(cls, sb), raise_px=2 if cls == "highway" else 1)


# --------------------------------------------------------------------------- one-way arrows (overlay)
def arrow_paint(U, V, direction, lanes=(-11, 11)):
    """direction 0..3 = N, E, S, W (travel toward that edge)."""
    arm = (N, E, S, W)[direction]
    Sx, Tx = arm_coords(U, V, arm)
    hit = np.zeros(U.shape, bool)
    for c in lanes:
        t = Tx - c
        shaft = (np.abs(t) < 1.0) & (Sx > -9) & (Sx < 2)
        head = (Sx >= 2) & (Sx < 9) & (np.abs(t) < (9 - Sx) * 0.7)
        hit |= shaft | head
    return hit


def arrow_scene(direction):
    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        hit = arrow_paint(U, V, direction)
        col = np.zeros((U.shape[0], 3), np.float32) + P.WHITE
        return col, hit, np.zeros(U.shape, bool), None, None
    return scene


def with_arrows(direction):
    def extra(c, col, alpha, raised):
        hit = arrow_paint(c["U"], c["V"], direction) & c["road"]
        col = np.where(hit[:, None], P.WHITE, col)
        return col, alpha, raised
    return extra


# --------------------------------------------------------------------------- transitions (class A at N, B at S)
def transition_scene(a_cls, b_cls, a_ow=False, b_ow=False):
    sa = road_scene(a_cls, N | S, a_ow)
    sb = road_scene(b_cls, N | S, b_ow)
    aa = CLASSES[a_cls]["ow" if a_ow else "tw"]["a"]
    ab = CLASSES[b_cls]["ow" if b_ow else "tw"]["a"]

    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        T = np.abs(U - 32)
        lo = min(aa, ab)
        slope = 1.0 if ab > aa else -1.0
        vb = 32 + np.maximum(0, T - lo) * slope
        ca, al_a, ra, _, _ = sa(u, v)
        cb, al_b, rb, _, _ = sb(u, v)
        pick_a = V < vb
        col = np.where(pick_a[:, None], ca, cb)
        return col, np.where(pick_a, al_a, al_b), np.where(pick_a, ra, rb), None, None

    return scene


# --------------------------------------------------------------------------- junction control paint
def control_extra(kind, arms=None):
    """kind: stop | yield | mini | ramp | signal. Paints on the inbound half of every arm (or `arms`)."""
    def extra(c, col, alpha, raised):
        U, V, a, road = c["U"], c["V"], c["a"], c["road"]
        mask = c["mask"]
        paint = np.zeros(U.shape, bool)
        dark = np.zeros(U.shape, bool)
        for arm in (N, E, S, W):
            if not mask & arm or (arms is not None and not arms & arm):
                continue
            Sx, Tx = arm_coords(U, V, arm)
            inbound = (Tx < -1) & (Tx > -a + 1)
            if kind in ("stop", "signal"):
                paint |= inbound & (Sx >= a + 1) & (Sx < a + 4)
            elif kind == "yield":
                k = (Tx + 64) % 6
                tri = (Sx >= a + 1) & (Sx < a + 7 - np.abs(k - 3) * 2)
                paint |= inbound & tri
        if kind == "mini":
            r = np.hypot(U - 32, V - 32)
            ring = (np.abs(r - 9) < 1.2)
            dome = r < 5.5
            paint |= ring & road
            raised = raised | (dome & road)
            col = np.where((dome & road)[:, None], np.where((r < 3)[:, None], P.WHITE, scale(P.WHITE, 0.82)), col)
            # three curved arrows (heads) around the ring, counter-clockwise
            ang = np.arctan2(V - 32, U - 32)
            for k in range(3):
                a0 = -np.pi + k * 2 * np.pi / 3 + 0.4
                da = np.angle(np.exp(1j * (ang - a0)))
                head = (da > 0) & (da < 0.45) & (np.abs(r - 9) < 3.6 - da * 7)
                paint |= head & road
        if kind == "ramp":
            Sx, Tx = arm_coords(U, V, N)
            chev = (np.abs(((Sx - np.abs(Tx) * 0.8 + 64) % 12) - 3) < 1.5) & (np.abs(Tx) < 16)
            paint |= chev & road & (np.abs(Tx) < 16)
            col = np.where((chev & road)[:, None], P.YELLOW, col)
            paint &= False
        col = np.where((paint & road)[:, None], P.WHITE, col)
        col = np.where((dark & road)[:, None], P.IRON, col)
        return col, alpha, raised
    return extra


# --------------------------------------------------------------------------- roundabout islands
def island_scene(gx=0, gy=0, n=1, edge=True):
    """Island cell (gx, gy) of an n x n island (n = 1 or 3). Raised grass with a kerb, a flower ring and gravel path."""
    def scene(u, v):
        U = (u + gx) * 64.0
        V = (v + gy) * 64.0
        c = n * 32.0
        r = np.hypot(U - c, V - c)
        h = hash2(u + gx, v + gy, 31)
        col = np.zeros((U.shape[0], 3), np.float32) + np.where((h < 0.2)[:, None], P.GRASS_D, P.GRASS)
        dx = np.minimum(U, n * 64 - U)
        dy = np.minimum(V, n * 64 - V)
        kerb = np.minimum(dx, dy) < 3
        flowers = np.abs(r - c * 0.62) < 3.5
        fl = np.where((h < 0.33)[:, None], np.array([222, 84, 92], np.float32),
                      np.where((h < 0.66)[:, None], np.array([238, 206, 80], np.float32), np.array([240, 240, 236], np.float32)))
        col = np.where(flowers[:, None], fl, col)
        path = np.abs(r - c * 0.3) < 1.6
        col = np.where(path[:, None], P.GRAVEL_L, col)
        col = np.where(kerb[:, None], P.KERB, col)
        ones = np.ones(U.shape, bool)
        return col, ones, ones, None, None
    return scene


# --------------------------------------------------------------------------- map-edge highway (fades to the map edge)
def map_edge_extra(direction):
    """direction 0..3: the map edge lies beyond the N, E, S, W edge of the cell."""
    arm = (N, E, S, W)[direction]

    def extra(c, col, alpha, raised):
        Sx, Tx = arm_coords(c["U"], c["V"], arm)
        t = np.clip((Sx - 8) / 24.0, 0, 1)
        dither = hash2(c["U"] / 64, c["V"] / 64, 41) < t * 0.9
        col = np.where(dither[:, None], scale(col, 0.35), col)
        # in/out arrows: inbound lane points toward the centre, outbound to the edge
        for sgn, toward in ((-1, -1), (1, 1)):
            tt = Tx - sgn * 15
            ss = (Sx - 2) * toward
            hit = ((np.abs(tt) < 1.2) & (ss > -10) & (ss < 0)) | ((ss >= 0) & (ss < 7) & (np.abs(tt) < (7 - ss) * 0.8))
            col = np.where((hit & c["road"])[:, None], P.WHITE, col)
        return col, alpha, raised
    return extra


# --------------------------------------------------------------------------- tram track in the road
def tram_extra(c, col, alpha, raised):
    U, V, T, road, box, mask = c["U"], c["V"], c["T"], c["road"], c["box"], c["mask"]
    rails = np.zeros(U.shape, bool)
    groove = np.zeros(U.shape, bool)
    sec = c["sec"]
    centres = (-10, 10) if sec["oneway"] is False else (0,)
    if bin(mask).count("1") >= 3:
        segs = []
        if mask & N and mask & S:
            segs.append(U - 32)
        if mask & E and mask & W:
            segs.append(V - 32)
        for arm in (N, E, S, W):
            if mask & arm:
                Sx, Tx = arm_coords(U, V, arm)
                segs.append(np.where(Sx > -2, Tx, 999))
        tlist = segs
    else:
        tlist = [T]
    for t in tlist:
        for cc in centres:
            for g in (-4, 4):
                rails |= np.abs(t - cc - g) < 1.0
                groove |= np.abs(t - cc - g - np.sign(g)) < 0.5
    inlay = np.zeros(U.shape, bool)
    for t in tlist:
        for cc in centres:
            inlay |= np.abs(t - cc) < 6
    col = np.where((inlay & road)[:, None], lerp(col, P.CONC, 0.35), col)
    col = np.where((groove & road)[:, None], P.IRON, col)
    col = np.where((rails & road)[:, None], np.array([176, 182, 188], np.float32), col)
    return col, alpha, raised


def combine(*extras):
    def extra(c, col, alpha, raised):
        for e in extras:
            if e is not None:
                col, alpha, raised = e(c, col, alpha, raised)
        return col, alpha, raised
    return extra


def tile(scene, raise_px=1):
    return render_ground(scene, raise_px=raise_px)


def rot(scene, k):
    return rotate_scene(scene, k)
