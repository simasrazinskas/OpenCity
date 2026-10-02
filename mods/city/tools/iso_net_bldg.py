"""
iso_net_bldg.py - NET network buildings (transformer 2x2, battery 1x1, sewage outlet 1x1, treatment plant 2x2),
rail props (buffer stop, level-crossing barriers, signal, catenary mast), highway gantry, sound barrier,
guard rail, parked cars and light pools.
"""
import numpy as np

import isokit as ik
from iso_net_kit import (CONCRETE, GALV, GLASS, POLE, SIGN_GREEN, SIGN_RED, SIGN_WHITE, SIGN_YELLOW, STEEL,
                         STEEL_DARK, flat, glow)
from iso_net_props import GREEN_ON, LENS_OFF, RED_ON

FENCE = flat("grey", 0.5)
TRAFO = flat("slate", 0.0)
TRAFO_FIN = ik.mat("metal")
HAZARD = ik.Material("yellow", 0.0, None, dither=0.0)


def fence(s, x0, y0, x1, y1, z=0, h=5, gate=None):
    """Chain-link fence on the rectangle border: posts + top rail + see-through mesh suggested by a low rail."""
    def run(ax, ay, bx, by):
        n = max(2, int(max(abs(bx - ax), abs(by - ay)) / 0.12))
        for i in range(n + 1):
            t = i / n
            px, py = ax + (bx - ax) * t, ay + (by - ay) * t
            s.box(px - 0.008, py - 0.008, z, 0.016, 0.016, h, FENCE)
        if ax == bx:
            s.box(ax - 0.006, min(ay, by), z + h - 1, 0.012, abs(by - ay), 1, FENCE)
            s.box(ax - 0.006, min(ay, by), z + 1, 0.012, abs(by - ay), 0.6, FENCE)
        else:
            s.box(min(ax, bx), ay - 0.006, z + h - 1, abs(bx - ax), 0.012, 1, FENCE)
            s.box(min(ax, bx), ay - 0.006, z + 1, abs(bx - ax), 0.012, 0.6, FENCE)
    run(x0, y0, x1, y0)
    run(x0, y0, x0, y1)
    run(x1, y0, x1, y1)
    if gate is None:
        run(x0, y1, x1, y1)
    else:
        run(x0, y1, gate[0], y1)
        run(gate[1], y1, x1, y1)


def transformer_unit(s, x, y, rot=0):
    w, d = (0.34, 0.22) if rot == 0 else (0.22, 0.34)
    s.box(x - w / 2 - 0.03, y - d / 2 - 0.03, 0, w + 0.06, d + 0.06, 2, CONCRETE)
    s.box(x - w / 2, y - d / 2, 2, w, d, 10, TRAFO_FIN)
    s.box(x - w / 2 + 0.02, y - d / 2 + 0.02, 12, w - 0.04, d - 0.04, 2, TRAFO)
    for i in range(3):
        ox = (i - 1) * w * 0.3 if rot == 0 else 0
        oy = 0 if rot == 0 else (i - 1) * d * 0.3
        s.cylinder(x + ox, y + oy, 14, 0.014, 6, ik.mat("plaster_white"), segs=6)


def transformer():
    s = ik.Scene(footprint=(2, 2), seed=41)
    s.ground([(0.05, 0.05), (1.95, 0.05), (1.95, 1.95), (0.05, 1.95)], "gravel")
    s.ground([(0.85, 1.4), (1.15, 1.4), (1.15, 1.98), (0.85, 1.98)], "concrete_ground", layer=1)
    transformer_unit(s, 0.55, 0.6)
    transformer_unit(s, 1.3, 0.6)
    # steel gantry with insulator strings
    for gx in (0.3, 1.0, 1.7):
        s.box(gx - 0.02, 1.1 - 0.02, 0, 0.04, 0.04, 26, GALV)
    s.box(0.28, 1.08, 26, 1.44, 0.04, 2, GALV)
    for gx in (0.5, 0.8, 1.2, 1.5):
        s.box(gx - 0.01, 1.09, 21, 0.02, 0.02, 5, ik.mat("plaster_white"))
    # control hut
    s.box(1.45, 1.35, 0, 0.4, 0.45, 11, ik.mat("windows_brick", storey=11))
    s.roof_flat(1.45, 1.35, 11, 0.4, 0.45, "roof_flat", parapet=1)
    s.box(1.5, 1.79, 0, 0.1, 0.01, 7, flat("slate", -1.0))
    fence(s, 0.06, 0.06, 1.94, 1.94, gate=(0.85, 1.15))
    s.box(0.7, 1.95, 3, 0.1, 0.006, 3, SIGN_YELLOW)
    return s


def battery():
    s = ik.Scene(footprint=(1, 1), seed=42)
    s.ground([(0.04, 0.04), (0.96, 0.04), (0.96, 0.96), (0.04, 0.96)], "concrete_ground")
    for i, y in enumerate((0.14, 0.52)):
        s.box(0.12, y, 0, 0.62, 0.3, 11, ik.mat("metal_light"))
        s.box(0.74, y + 0.06, 3, 0.004, 0.18, 7, flat("grey", -1.0))           # door seam
        for k in range(3):
            s.cylinder(0.22 + k * 0.2, y + 0.15, 11, 0.06, 2, flat("slate", -1.5), segs=10)
        s.box(0.745, y + 0.04, 8, 0.004, 0.04, 1.5, glow("teal", day=8, night=10))  # status light
    s.box(0.8, 0.3, 0, 0.12, 0.2, 7, TRAFO)
    s.box(0.81, 0.53, 0, 0.1, 0.1, 5, ik.mat("metal"))
    fence(s, 0.05, 0.05, 0.95, 0.95, h=4, gate=(0.4, 0.6))
    return s


def sewage_outlet():
    """Concrete headwall on the bank; the culvert mouth faces +Y (toward the water at the lower left)."""
    s = ik.Scene(footprint=(1, 1), seed=43)
    s.ground([(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)], "grass")
    s.ground([(0.3, 0.55), (0.7, 0.55), (0.75, 1.0), (0.25, 1.0)], "mud", layer=1)
    s.box(0.12, 0.40, 0, 0.76, 0.16, 12, CONCRETE)                       # headwall
    s.box(0.08, 0.36, 12, 0.84, 0.24, 2, CONCRETE)
    for wx in (0.12, 0.8):
        s.box(wx, 0.56, 0, 0.08, 0.3, 7, CONCRETE)                       # wing walls
    s.cylinder(0.5, 0.56, 6, 0.13, 0.04, flat("grey", 0.5), axis="y", segs=16)
    s.cylinder(0.5, 0.60, 6, 0.10, 0.01, flat("slate", -4.0), axis="y", segs=16)
    s.ground([(0.42, 0.6), (0.58, 0.6), (0.62, 1.0), (0.38, 1.0)], ik.mat("water", ramp="olive"), layer=2)
    s.box(0.2, 0.42, 14, 0.6, 0.02, 3, GALV)                              # railing
    s.box(0.7, 0.2, 0, 0.14, 0.14, 6, ik.mat("metal"))                    # valve kiosk
    return s


def treatment_plant():
    s = ik.Scene(footprint=(2, 2), seed=44)
    s.ground([(0.0, 0.0), (2.0, 0.0), (2.0, 2.0), (0.0, 2.0)], "concrete_ground")
    s.ground([(0.0, 0.0), (2.0, 0.0), (2.0, 0.12), (0.0, 0.12)], "grass", layer=1)
    for (cx, cy) in ((0.5, 0.55), (1.2, 0.5)):
        s.cylinder(cx, cy, 0, 0.36, 7, CONCRETE, segs=28)
        ring = [(cx + 0.31 * np.cos(t), cy + 0.31 * np.sin(t)) for t in np.linspace(0, 2 * np.pi, 28, endpoint=False)]
        s.ground(ring, ik.mat("water", ramp="olive"), z=7.05, layer=3)
        s.box(cx - 0.02, cy - 0.34, 7, 0.04, 0.68, 1.5, GALV)          # rotating bridge
        s.cylinder(cx, cy, 6, 0.05, 3, STEEL, segs=10)
    # aeration basin
    s.box(0.12, 1.05, 0, 0.9, 0.55, 5, CONCRETE)
    s.ground([(0.16, 1.09), (0.98, 1.09), (0.98, 1.56), (0.16, 1.56)], ik.mat("water", ramp="teal"), z=5.05, layer=3)
    for k in range(3):
        s.box(0.16, 1.18 + k * 0.15, 5.0, 0.82, 0.02, 0.8, GALV)
    # operations building
    s.box(1.2, 1.1, 0, 0.65, 0.6, 14, ik.mat("windows_concrete"))
    s.roof_flat(1.2, 1.1, 14, 0.65, 0.6, "roof_flat", parapet=1.5)
    s.box(1.6, 1.15, 15.5, 0.15, 0.15, 3, ik.mat("metal"))
    s.box(1.3, 1.69, 0, 0.12, 0.01, 8, ik.mat("glass_dark"))
    # pipe rack
    s.cylinder(1.0, 0.95, 4, 0.03, 0.9, flat("water", 0.0), axis="x", segs=8)
    s.cylinder(0.8, 0.95, 4, 0.03, 0.55, flat("olive", 0.0), axis="y", segs=8)
    return s


# --------------------------------------------------------------------------- rail props
def buffer_stop(facing_arm="N"):
    """Buffer at the end of a dead-end track whose arm leaves toward N (stop sits at v ~ 0.72)."""
    s = ik.Scene(footprint=(1, 1), seed=45)
    y = 0.72
    s.box(0.38, y, 0, 0.24, 0.08, 6, ik.mat("awning"))
    s.box(0.35, y - 0.02, 4, 0.3, 0.04, 3, flat("red", 0.5))
    s.box(0.4, y - 0.035, 5, 0.04, 0.02, 1.5, flat("yellow", 2.0))
    s.box(0.56, y - 0.035, 5, 0.04, 0.02, 1.5, flat("yellow", 2.0))
    s.box(0.42, y + 0.08, 0, 0.16, 0.08, 3, ik.mat("dirt"))
    return s


def crossing_gate(down=True, lamp=0):
    """Barrier + flashing lights for a level crossing (rail N-S, road E-W): two posts at the road edges."""
    s = ik.Scene(footprint=(1, 1), seed=46)
    for (px, py, sgn) in ((0.24, 0.2, 1), (0.76, 0.8, -1)):
        s.box(px - 0.02, py - 0.02, 1, 0.04, 0.04, 12, flat("grey", 2.0))
        s.box(px - 0.04, py - 0.015, 10, 0.08, 0.03, 3, STEEL_DARK)
        on_l = RED_ON if lamp == 0 else LENS_OFF
        on_r = RED_ON if lamp == 1 else LENS_OFF
        s.box(px - 0.04, py + 0.015, 10.5, 0.025, 0.006, 2, on_l)
        s.box(px + 0.015, py + 0.015, 10.5, 0.025, 0.006, 2, on_r)
        s.box(px - 0.025, py - 0.04, 13, 0.05, 0.08, 1, SIGN_WHITE)
        if down:
            for k in range(6):
                mat = SIGN_RED if k % 2 == 0 else SIGN_WHITE
                y0 = py + sgn * (0.03 + k * 0.05)
                s.box(px - 0.01, min(y0, y0 + sgn * 0.05), 5, 0.02, 0.05, 1.5, mat)
        else:
            for k in range(6):
                mat = SIGN_RED if k % 2 == 0 else SIGN_WHITE
                s.box(px - 0.01, py - 0.01, 6 + k * 4, 0.02, 0.02, 4, mat)
    return s


def rail_signal(state="green"):
    s = ik.Scene(footprint=(1, 1), seed=47)
    x, y = 0.72, 0.62
    s.cylinder(x, y, 0, 0.014, 16, GALV, segs=6)
    s.box(x - 0.025, y - 0.02, 14, 0.05, 0.04, 7, STEEL_DARK)
    s.box(x - 0.012, y + 0.02, 18.5, 0.024, 0.006, 1.6, RED_ON if state == "red" else LENS_OFF)
    s.box(x - 0.012, y + 0.02, 15.5, 0.024, 0.006, 1.6, GREEN_ON if state == "green" else LENS_OFF)
    return s


def catenary_mast(axis="ns"):
    """Overhead line mast beside a straight track with the contact wire along the track centre."""
    s = ik.Scene(footprint=(1, 1), seed=48)
    if axis == "ns":
        s.box(0.75, 0.48, 0, 0.04, 0.04, 26, GALV)
        s.box(0.48, 0.49, 24, 0.31, 0.02, 1.2, GALV)
        s.box(0.495, 0.0, 21, 0.01, 1.0, 0.8, flat("grey", -2.0))
    else:
        s.box(0.48, 0.75, 0, 0.04, 0.04, 26, GALV)
        s.box(0.49, 0.48, 24, 0.02, 0.31, 1.2, GALV)
        s.box(0.0, 0.495, 21, 1.0, 0.01, 0.8, flat("grey", -2.0))
    return s


# --------------------------------------------------------------------------- road structures
def gantry(axis="ns"):
    """Overhead sign gantry spanning a highway carriageway (green signs face +Y / +X toward the viewer)."""
    s = ik.Scene(footprint=(1, 1), seed=49)
    if axis == "ns":
        for x in (0.06, 0.94):
            s.box(x - 0.02, 0.48, 0, 0.04, 0.04, 30, GALV)
        s.box(0.04, 0.47, 28, 0.92, 0.06, 3, GALV)
        for x0 in (0.12, 0.55):
            s.box(x0, 0.53, 20, 0.33, 0.01, 8, SIGN_GREEN)
            s.box(x0 + 0.04, 0.542, 25, 0.22, 0.002, 1, SIGN_WHITE)
            s.box(x0 + 0.04, 0.542, 22, 0.14, 0.002, 1, SIGN_WHITE)
    else:
        for y in (0.06, 0.94):
            s.box(0.48, y - 0.02, 0, 0.04, 0.04, 30, GALV)
        s.box(0.47, 0.04, 28, 0.06, 0.92, 3, GALV)
        for y0 in (0.12, 0.55):
            s.box(0.53, y0, 20, 0.01, 0.33, 8, SIGN_GREEN)
            s.box(0.542, y0 + 0.04, 25, 0.002, 0.22, 1, SIGN_WHITE)
            s.box(0.542, y0 + 0.04, 22, 0.002, 0.14, 1, SIGN_WHITE)
    return s


def sound_barrier(side="E"):
    """Sound wall along the east cell edge (the barrier add-on); other sides via facings."""
    s = ik.Scene(footprint=(1, 1), seed=50)
    for i in range(4):
        y0 = i * 0.25
        s.box(0.93, y0 + 0.01, 0, 0.04, 0.23, 14, ik.mat("metal", ramp="leaf"))
        s.box(0.92, y0, 0, 0.06, 0.02, 15, CONCRETE)
    return s


def guard_rail(side="E"):
    s = ik.Scene(footprint=(1, 1), seed=51)
    for i in range(8):
        s.box(0.9, i / 8 + 0.05, 0, 0.015, 0.015, 3, STEEL)
    s.box(0.895, 0.0, 2, 0.02, 1.0, 1.5, GALV)
    return s


# --------------------------------------------------------------------------- parked cars
CAR_RAMPS = ["red", "water", "grey", "snow", "slate", "yellow", "leaf", "stone", "purple", "brick"]


def car(s, x, y, along="y", ramp="red", z=0.0):
    """Parked car ~0.42 x 0.2 cells (about 15 px long on screen), body + glass cabin."""
    L, Wd = 0.42, 0.2
    body = ik.Material(ramp, 0.5, None, dither=0.0)
    if along == "y":
        s.box(x - Wd / 2, y - L / 2, z + 1, Wd, L, 4, body)
        s.box(x - Wd / 2 + 0.02, y - L / 2 + 0.1, z + 5, Wd - 0.04, L - 0.2, 3, ik.mat("glass_dark"))
        s.box(x - Wd / 2 + 0.03, y - L / 2 + 0.12, z + 8, Wd - 0.06, L - 0.24, 0.8, body)
    else:
        s.box(x - L / 2, y - Wd / 2, z + 1, L, Wd, 4, body)
        s.box(x - L / 2 + 0.1, y - Wd / 2 + 0.02, z + 5, L - 0.2, Wd - 0.04, 3, ik.mat("glass_dark"))
        s.box(x - L / 2 + 0.12, y - Wd / 2 + 0.03, z + 8, L - 0.24, Wd - 0.06, 0.8, body)
    for (dx, dy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        if along == "y":
            s.box(x + dx * Wd / 2 - 0.01, y + dy * L * 0.3 - 0.03, z, 0.02, 0.06, 2, flat("grey", -3.0))
        else:
            s.box(x + dx * L * 0.3 - 0.03, y + dy * Wd / 2 - 0.01, z, 0.06, 0.02, 2, flat("grey", -3.0))


def lot_cars(side, occupancy, seed=0):
    """Cars in a stall-row cell (stall row on the N/E/S/W half; three stalls). occupancy 0..3."""
    s = ik.Scene(footprint=(1, 1), seed=52 + seed)
    rng = np.random.RandomState(100 + seed * 7 + side)
    stalls = [0.167, 0.5, 0.833]
    order = rng.permutation(3)[:occupancy]
    for k in order:
        a = stalls[k]
        ramp = CAR_RAMPS[rng.randint(len(CAR_RAMPS))]
        depth = 0.24
        if side == 0:
            car(s, a, depth, "y", ramp)
        elif side == 2:
            car(s, a, 1 - depth, "y", ramp)
        elif side == 1:
            car(s, 1 - depth, a, "x", ramp)
        else:
            car(s, depth, a, "x", ramp)
    return s


def kerb_cars(side="E", n=1, seed=0, a=20):
    """Parallel-parked car(s) in the outer lane of a N-S street (parking add-on), along the east kerb."""
    s = ik.Scene(footprint=(1, 1), seed=60 + seed)
    rng = np.random.RandomState(200 + seed)
    x = 0.5 + (a - 7) / 64.0
    for i in range(n):
        car(s, x, 0.5 if n == 1 else 0.25 + i * 0.5, "y", CAR_RAMPS[rng.randint(len(CAR_RAMPS))])
    return s
