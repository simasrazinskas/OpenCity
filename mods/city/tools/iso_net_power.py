"""
iso_net_power.py - power lines: HV lattice pylons and wooden distribution poles for all 16 masks, wires drawn
as sagging 1-px lines that meet the neighbour cell's wires at the shared edge midpoint.
"""
import numpy as np

import isokit as ik
from iso_net_core import over
from iso_net_kit import flat, render_fixed, screen

N, E, S, W = 1, 2, 4, 8
LATTICE = flat("slate", -1.0)
LATTICE_L = flat("grey", 0.0)
INSUL = flat("glass", 1.0, snow=False)
WOOD = ik.mat("wood")
WIRE = np.array(ik.color("grey", 1), np.float32)
CW, CH = 64, 112
AX, AY = 32, CH - 16

HV = dict(arms=[(0.21, 34.0), (-0.21, 34.0), (0.0, 44.0)], sag=5.0)
LV = dict(arms=[(0.12, 22.0), (-0.12, 22.0), (0.0, 25.0)], sag=3.0)


def _leg_quads(s, x, y, base, top, z1, mat):
    """Tapered square lattice tower as 4 slanted faces with cross bracing bands."""
    b, t = base, top
    corners0 = [(x - b, y - b), (x + b, y - b), (x + b, y + b), (x - b, y + b)]
    corners1 = [(x - t, y - t), (x + t, y - t), (x + t, y + t), (x - t, y + t)]
    outs = [(0, -1, 0), (1, 0, 0), (0, 1, 0), (-1, 0, 0)]
    for i in range(4):
        j = (i + 1) % 4
        s.quad(corners0[i] + (0,), corners0[j] + (0,), corners1[j] + (z1,), corners1[i] + (z1,), mat, outward=outs[i])


def pylon(orient="x", cross=False):
    """HV pylon. orient: crossarm along x (line runs N-S) or y (line runs E-W); cross = arms both ways."""
    s = ik.Scene(footprint=(1, 1), seed=31)
    x = y = 0.5
    # four legs + bracing as thin boxes (reads as lattice at 1x)
    for k in range(8):
        z0 = k * 5.0
        f = 1 - k / 9.0
        b = 0.09 * f
        for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            s.box(x + sx * b - 0.012, y + sy * b - 0.012, z0, 0.024, 0.024, 5.0, LATTICE)
        if k % 2 == 1:
            s.box(x - b, y + b - 0.008, z0 + 2, 2 * b, 0.016, 1.0, LATTICE_L)
            s.box(x + b - 0.008, y - b, z0 + 2, 0.016, 2 * b, 1.0, LATTICE_L)
    s.box(x - 0.025, y - 0.025, 40, 0.05, 0.05, 5, LATTICE)
    dirs = ["x", "y"] if cross else [orient]
    for d in dirs:
        if d == "x":
            s.box(x - 0.23, y - 0.015, 34, 0.46, 0.03, 2, LATTICE)
            s.box(x - 0.08, y - 0.015, 44, 0.16, 0.03, 1.5, LATTICE)
            for o in (-0.21, 0.21):
                s.box(x + o - 0.01, y - 0.01, 31, 0.02, 0.02, 3, INSUL)
        else:
            s.box(x - 0.015, y - 0.23, 34, 0.03, 0.46, 2, LATTICE)
            s.box(x - 0.015, y - 0.08, 44, 0.03, 0.16, 1.5, LATTICE)
            for o in (-0.21, 0.21):
                s.box(x - 0.01, y + o - 0.01, 31, 0.02, 0.02, 3, INSUL)
    return s


def pole(orient="x", cross=False):
    s = ik.Scene(footprint=(1, 1), seed=32)
    x = y = 0.5
    s.cylinder(x, y, 0, 0.025, 26, WOOD, segs=8)
    for d in (["x", "y"] if cross else [orient]):
        if d == "x":
            s.box(x - 0.15, y - 0.012, 22, 0.30, 0.024, 1.5, WOOD)
            for o in (-0.12, 0.12):
                s.box(x + o - 0.008, y - 0.008, 23.5, 0.016, 0.016, 1.5, INSUL)
        else:
            s.box(x - 0.012, y - 0.15, 22, 0.024, 0.30, 1.5, WOOD)
            for o in (-0.12, 0.12):
                s.box(x - 0.008, y + o - 0.008, 23.5, 0.016, 0.016, 1.5, INSUL)
    s.box(x - 0.02, y + 0.03, 14, 0.05, 0.05, 5, flat("grey", 0.5))  # pole transformer can
    return s


def _polyline(img, pts):
    """One pixel per screen column (wires run along 2:1 diagonals), so lines stay 1 px thin."""
    h, w = img.shape[:2]
    xs = np.array([p[0] for p in pts])
    ys = np.array([p[1] for p in pts])
    order = np.argsort(xs)
    xs, ys = xs[order], ys[order]
    for col in range(int(np.floor(xs.min())), int(np.floor(xs.max())) + 1):
        y = np.interp(col + 0.5, xs, ys)
        py = int(np.floor(y))
        if 0 <= col < w and 0 <= py < h:
            img[py, col, :3] = WIRE
            img[py, col, 3] = 255


def _wires(img, mask, spec, arms_dir, near):
    """Draw the wires of every arm; near=True draws only the arms toward the viewer (E, S)."""
    for arm in (N, E, S, W):
        if not mask & arm:
            continue
        is_near = arm in (E, S)
        if is_near != near:
            continue
        ns = arm in (N, S)
        for off, z in spec["arms"]:
            if ns:
                p0 = (0.5 + off, 0.5, z - 2)
                p1 = (0.5 + off, 0.0 if arm == N else 1.0, z - 2 - spec["sag"])
            else:
                p0 = (0.5, 0.5 + off, z - 2)
                p1 = (1.0 if arm == E else 0.0, 0.5 + off, z - 2 - spec["sag"])
            # half catenary: lowest point at the edge midpoint
            x0, y0, z0 = p0
            x1, y1, z1 = p1
            pts = []
            for i in range(41):
                t = i / 40
                pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 - (z0 - z1) * (1 - (1 - t) ** 2)))
            _polyline(img, [screen(*p, AX, AY) for p in pts])


def power_tile(mask, kind="hv"):
    spec = HV if kind == "hv" else LV
    ns = bool(mask & (N | S))
    ew = bool(mask & (E | W))
    cross = ns and ew
    orient = "y" if (ew and not ns) else "x"
    model = pylon(orient, cross) if kind == "hv" else pole(orient, cross)
    img = np.zeros((CH, CW, 4), np.float32)
    _wires(img, mask, spec, orient, near=False)
    over(img, render_fixed(model, CW, CH, AX, AY), 0, 0)
    _wires(img, mask, spec, orient, near=True)
    return img
