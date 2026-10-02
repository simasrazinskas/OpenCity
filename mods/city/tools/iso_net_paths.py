"""
iso_net_paths.py - centreline paths through a cell for tracks and pipes (same geometry as the roads: straight
arms through the centre, quarter arcs of radius 32 q around a cell corner for 2-arm bends).

paths(mask, junction="switch") -> list of fn(U, V) -> (T, P, valid): T signed offset (q), P phase along the path
in 1/16-cell units (global, so dashes/sleepers line up across tiles), valid = where the path exists.
"""
import math

import numpy as np

N, E, S, W = 1, 2, 4, 8
CORNER = {N | E: (64, 0), N | W: (0, 0), S | E: (64, 64), S | W: (0, 64)}


def straight(axis, lo=-1e9, hi=1e9):
    """axis 'ns' (along v) or 'ew' (along u); lo/hi clip the path along its axis (q)."""
    def f(U, V):
        if axis == "ns":
            return U - 32, V / 16.0, (V >= lo) & (V < hi)
        return V - 32, U / 16.0, (U >= lo) & (U < hi)
    return f


def arc(m):
    cx, cy = CORNER[m]

    def f(U, V):
        R = np.hypot(U - cx, V - cy)
        ang = np.arctan2(np.abs(V - cy), np.abs(U - cx))
        P = (1 - ang / (math.pi / 2)) * 3.0
        inside = (np.abs(U - cx) <= 64) & (np.abs(V - cy) <= 64)
        return R - 32, P, inside
    return f


def arm_stub(arm, reach=0):
    """Straight from the arm's edge to `reach` q past the centre."""
    if arm == N:
        return straight("ns", -1, 32 + reach)
    if arm == S:
        return straight("ns", 32 - reach, 65)
    if arm == E:
        return straight("ew", 32 - reach, 65)
    return straight("ew", -1, 32 + reach)


def paths(mask, junction="switch"):
    n = bin(mask).count("1")
    if mask == 0:
        return [straight("ns", 16, 48)]
    if n == 1:
        return [arm_stub(mask, 14)]
    if n == 2:
        if mask in CORNER:
            return [arc(mask)]
        return [straight("ns") if mask & N else straight("ew")]
    out = []
    if mask & N and mask & S:
        out.append(straight("ns"))
    if mask & E and mask & W:
        out.append(straight("ew"))
    if n == 3 and junction == "switch":
        odd = [a for a in (N, E, S, W) if mask & a and not mask & {N: S, S: N, E: W, W: E}[a]][0]
        for other in (N, E, S, W):
            if mask & other and other != odd and (odd | other) in CORNER:
                out.append(arc(odd | other))
    return out


def nearest(U, V, plist):
    """Combine several paths: per pixel the one with the smallest |T|."""
    bestT = np.full(U.shape, 1e9)
    bestP = np.zeros(U.shape)
    for f in plist:
        T, P, ok = f(U, V)
        T = np.where(ok, T, 1e9)
        sel = np.abs(T) < np.abs(bestT)
        bestT = np.where(sel, T, bestT)
        bestP = np.where(sel, P, bestP)
    return bestT, bestP
