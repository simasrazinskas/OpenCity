"""CIVIC models: garbage (landfill, incinerator, recycling). Accent: wood/brown + green."""
import math

import numpy as np

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, wall_mat, stack, tank, pole, beam, \
    pipe, fence, hcyl, cone, disc, lattice
from iso_civic_props import vehicle, tree, smoke, heap, lamp, person, crate_stack, bench, flowers
from iso_civic_states import model

S = ik.S
GREEN = P("leaf", 1.0)
GREEN_ROOF = M("roof_metal", ramp="leaf", shade=0.5)
BROWN = P("wood", 0.5)
SIDING = M("metal_light", shade=0.5)
HALL = wall_mat("brick", win_w=4, win_h=7, period=8, sill=4, storey=14, glass="slate", lit=0.6)
CONC_W = wall_mat("concrete", win_w=4, win_h=6, period=9, storey=14, glass="slate", lit=0.6)
GARB_RAMPS = ("red", "water", "yellow", "snow", "leaf", "terra", "purple", "teal", "rose")


def _pat_garbage(c, m):
    """Mixed rubbish: dark brown-grey bulk with speckles of coloured bags and packaging."""
    hs = ik.hash2
    big = ik.value2(c.sx, c.sy, 3.0, c.seed + 4)
    c.tone += (big - 0.5) * 2.4 + (hs(c.sx, c.sy, c.seed) - 0.5) * 1.0
    cell = hs(c.sx // 2, c.sy // 2, c.seed + 3)
    sp = (cell > 0.82) | ((cell > 0.66) & (big > 0.55))
    ids = np.array([ik.RAMP[r] for r in GARB_RAMPS])
    pick = (hs(c.sx // 2, c.sy // 2, c.seed + 7) * len(ids)).astype(int) % len(ids)
    c.ramp = np.where(sp, ids[pick], c.ramp)
    c.tone = np.where(sp, c.tone + 1.4, c.tone)
    # dark bin-bag blacks
    blk = (cell < 0.2)
    c.tone = np.where(blk, c.tone - 1.8, c.tone)


GARBAGE = ik.Material("stone", -1.4, _pat_garbage, dither=0.0, snow=False)
LINER = P("slate", -2.8, snow=False)
LINER_L = P("slate", -1.6, snow=False)


def _pit_floor(s, x0, y0, x1, y1):
    rect(s, x0, y0, x1, y1, LINER, layer=2)
    n = 7
    for i in range(1, n):
        x = x0 + (x1 - x0) * i / n
        rect(s, x - 0.008, y0, x + 0.008, y1, LINER_L, layer=3)
    # sump / leachate channel along the front
    rect(s, x0 + 0.1, y1 - 0.12, x1 - 0.1, y1 - 0.04, P("olive", -3, snow=False), layer=3)


def dozer(s, x, y, axis="x", paint="yellow", z=0.0):
    """Bulldozer with blade, rear-left corner at (x, y), standing at height z."""
    trk = P("grey", -6, snow=False)
    body = P(paint, 0.5, snow=False)
    glass = M("glass", shade=-1, snow=False)
    blade = P("grey", 1, snow=False)
    if axis == "x":
        for yy in (y, y + 0.1):
            s.box(x, yy, z, 0.22, 0.03, 3, trk)
        s.box(x + 0.02, y + 0.01, z + 3, 0.18, 0.1, 3, body)
        s.box(x + 0.04, y + 0.02, z + 6, 0.08, 0.08, 4, glass, top=body)
        s.box(x + 0.22, y - 0.01, z + 1, 0.025, 0.14, 6, blade)
    else:
        for xx in (x, x + 0.1):
            s.box(xx, y, z, 0.03, 0.22, 3, trk)
        s.box(x + 0.01, y + 0.02, z + 3, 0.1, 0.18, 3, body)
        s.box(x + 0.02, y + 0.04, z + 6, 0.08, 0.08, 4, glass, top=body)
        s.box(x - 0.01, y + 0.22, z + 1, 0.14, 0.025, 6, blade)


def bale(s, x, y, dx, dy, ramp, z=0.0, h=5):
    """Strapped bale: coloured block with a lighter strap band."""
    s.box(x, y, z, dx, dy, h, M("metal", ramp=ramp, shade=0.8, snow=False), top=P(ramp, 2, snow=False))
    s.box(x - 0.004, y - 0.004, z + h * 0.45, dx + 0.008, dy + 0.008, 0.9, P("grey", 4, snow=False))


def bale_stack(s, x, y, nx, ny, nz, ramps, dx=0.1, dy=0.08):
    k = 0
    for iz in range(nz):
        for ix in range(nx):
            for iy in range(ny):
                if iz > 0 and (ix + iy + iz) % 3 == 2:
                    continue
                bale(s, x + ix * dx, y + iy * dy, dx - 0.006, dy - 0.006, ramps[(ix + iy * 2 + iz) % len(ramps)],
                     z=iz * 5)
                k += 1


def container(s, x, y, ramp, axis="x", L=0.2, W=0.08, h=7, z=0.0):
    mm = M("metal", ramp=ramp, shade=0.5, snow=False)
    if axis == "x":
        s.box(x, y, z, L, W, h, mm, top=P(ramp, 1.5, snow=False))
        for k in range(1, 5):
            s.box(x + k * L / 5, y + W, z + 0.5, 0.008, 0.004, h - 1, P(ramp, -1, snow=False))
    else:
        s.box(x, y, z, W, L, h, mm, top=P(ramp, 1.5, snow=False))
        for k in range(1, 5):
            s.box(x + W, y + k * L / 5, z + 0.5, 0.004, 0.008, h - 1, P(ramp, -1, snow=False))


def bottle_bank(s, x, y, ramp):
    s.cylinder(x, y, 0, 0.035, 6, M("metal", ramp=ramp, shade=0.5, snow=False), top=P(ramp, 2, snow=False), segs=10)
    s.dome(x, y, 6, 0.035, 2.5, P(ramp, 2, snow=False))


def conveyor(s, a, b, w=0.05, hpx=2.5, legs=True):
    """Inclined belt from point a to b with trestle legs and a dark belt."""
    beam(s, a, b, P("yellow", 0.5, snow=False), w=w, hpx=hpx)
    beam(s, (a[0], a[1], a[2] + 1.5), (b[0], b[1], b[2] + 1.5), P("grey", -4, snow=False), w=w * 0.6, hpx=0.8)
    if legs:
        n = 3
        for k in range(1, n):
            t = k / n
            x, y, z = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t
            pole(s, x, y, z, P("grey", 1, snow=False), w=0.014)


def gull(s, x, y, z):
    s.box(x, y, z, 0.03, 0.012, 1.0, P("snow", 3, snow=False))
    s.box(x - 0.01, y - 0.012, z + 0.2, 0.012, 0.036, 0.5, P("snow", 2, snow=False))


def road(s, x0, y0, x1, y1, layer=2):
    rect(s, x0, y0, x1, y1, M("asphalt", shade=0.2), layer=layer)


def _fp_fence(s, h=4, m=0.04, gap=None):
    fx, fy = s.footprint
    fence(s, [(m, m), (fx - m, m), (fx - m, fy - m), (m, fy - m)], h, closed=True)


def garbage_truck(s, x, y, axis="y", L=None):
    """Green refuse truck: cab + body with a rear hopper stripe."""
    L = L or 0.4
    if axis == "y":
        for t in (0.12, 0.8):
            s.box(x, y + L * t - 0.02, 0, 0.14, 0.04, 2, P("grey", -6, snow=False))
        s.box(x, y, 2, 0.14, L * 0.62, 9, P("leaf", 1.2, snow=False), top=P("leaf", 2, snow=False))
        s.box(x, y + L * 0.62, 2, 0.14, L * 0.38, 7, P("snow", 1.0, snow=False), top=P("snow", 2, snow=False))
        s.box(x + 0.01, y + L - 0.006, 5, 0.12, 0.008, 3.5, M("glass", shade=-1, snow=False))
        s.box(x + 0.02, y + 0.01, 11, 0.1, L * 0.55, 1.2, P("grey", 3, snow=False))
    else:
        for t in (0.12, 0.8):
            s.box(x + L * t - 0.02, y, 0, 0.04, 0.14, 2, P("grey", -6, snow=False))
        s.box(x, y, 2, L * 0.62, 0.14, 9, P("leaf", 1.2, snow=False), top=P("leaf", 2, snow=False))
        s.box(x + L * 0.62, y, 2, L * 0.38, 0.14, 7, P("snow", 1.0, snow=False), top=P("snow", 2, snow=False))
        s.box(x + L - 0.006, y + 0.01, 5, 0.008, 0.12, 3.5, M("glass", shade=-1, snow=False))
        s.box(x + 0.01, y + 0.02, 11, L * 0.55, 0.1, 1.2, P("grey", 3, snow=False))


# ---------------------------------------------------------------- landfill
@model("landfill", (3, 3), "garbage", "Landfill", fill=True,
       ups=(("depot", "truck depot"), ("recycling", "recycling unit")),
       note="Lined pit; the heap grows with the fill level 0..100%.")
def landfill(st):
    s = ik.Scene((3, 3), 71)
    f = 0.5 if st.fill is None else float(st.fill)
    lot(s, "grass_dry")
    # haul road: along the front + ramp up to the pit
    road(s, 0.05, 2.5, 2.95, 2.95)
    road(s, 1.15, 1.45, 1.45, 2.5)
    rect(s, 1.28, 1.45, 1.32, 2.5, "marking_yellow", layer=3)
    # lined pit
    px0, py0, px1, py1 = 0.25, 0.25, 1.92, 1.65
    _pit_floor(s, px0, py0, px1, py1)
    # earth berm round the pit (front berm has the ramp gap)
    dirt = M("dirt", shade=0.2)
    bt = 0.12
    s.box(px0 - bt, py0 - bt, 0, px1 - px0 + 2 * bt, bt, 4, dirt, top="grass")
    s.box(px0 - bt, py0 - bt, 0, bt, py1 - py0 + 2 * bt, 4, dirt, top="grass")
    s.box(px1, py0 - bt, 0, bt, py1 - py0 + 2 * bt, 4, dirt, top="grass")
    s.box(px0 - bt, py1, 0, 1.15 - (px0 - bt), bt, 4, dirt, top="grass")
    s.box(1.45, py1, 0, px1 + bt - 1.45, bt, 4, dirt, top="grass")
    # garbage heaps: a hill that grows with f
    rng = np.random.default_rng(7)
    cx, cy = (px0 + px1) / 2 - 0.05, (py0 + py1) / 2
    if f > 0.02:
        for i in range(6):
            for j in range(5):
                x = px0 + 0.14 + i * (px1 - px0 - 0.28) / 5 + rng.uniform(-0.05, 0.05)
                y = py0 + 0.14 + j * (py1 - py0 - 0.28) / 4 + rng.uniform(-0.05, 0.05)
                d = math.hypot((x - cx) / 0.85, (y - cy) / 0.7)
                prof = max(0.15, 1.15 - 0.6 * d) * (0.75 + 0.25 * rng.random())
                # a heap only appears once the fill level passes its own threshold, so it spreads out first
                lvl = min(1.0, max(0.0, (f * 1.15 - d * 0.7) / 0.45))
                if lvl <= 0:
                    continue
                r = (0.22 + 0.1 * rng.random()) * (0.5 + 0.5 * lvl)
                h = 3 + 19 * prof * (f ** 0.8) * lvl
                s.ellipsoid(x, y, 0, r, r * 0.9, h, GARBAGE, zmin=0, rough=0.4)
        # gas well pipes poking out of a mature heap + a few gulls
        if f > 0.45:
            for (x, y) in ((0.7, 0.8), (1.3, 0.95), (1.05, 1.3)):
                hh = 3 + 19 * max(0.15, 1.15 - 0.6 * math.hypot((x - cx) / 0.85, (y - cy) / 0.7)) * f ** 0.8
                pole(s, x, y, hh + 5, P("yellow", 0.5, snow=False), w=0.016)
        for k, (x, y) in enumerate(((0.55, 0.6), (1.5, 1.2), (0.9, 1.45), (1.7, 0.5))):
            if f > 0.25 + 0.12 * k:
                hh = 3 + 19 * max(0.15, 1.15 - 0.6 * math.hypot((x - cx) / 0.85, (y - cy) / 0.7)) * f ** 0.8
                gull(s, x, y, hh * 0.9)
    # dozer working the heap (rides up on the mound as it grows)
    dz = (1.5, 1.38)
    hd = 3 + 19 * max(0.15, 1.15 - 0.6 * math.hypot((dz[0] - cx) / 0.85, (dz[1] - cy) / 0.7)) * f ** 0.8
    dozer(s, dz[0], dz[1], "x", z=0.0 if f < 0.15 else hd * 0.55)
    # weighbridge + booth on the haul road
    s.box(1.12, 2.0, 0, 0.36, 0.38, 1.2, M("concrete", shade=0.8), top=M("asphalt", shade=1.0))
    block(s, 1.55, 2.08, 0.26, 0.24, 11, wall_mat("plaster", ramp="sand", shade=0.4, win_w=4, period=8, storey=11),
          roof="hip", roof_mat=M("roof_tiles_brown"), roof_h=4)
    door(s, 1.62, 2.32, 0.1, 7, "+y", "door")
    garbage_truck(s, 1.18, 1.62, "y", 0.34)
    # left-front: recycling unit (upgrade) or bottle banks
    if "recycling" in st.ups:
        block(s, 0.12, 1.88, 0.85, 0.5, 14, wall_mat("plaster", ramp="leaf", shade=1.5, win_w=5, period=9, storey=14,
                                                      glass="glass", lit=0.6),
              roof="gable", roof_mat=GREEN_ROOF, roof_h=6, axis="y", gable=M("plaster", ramp="leaf", shade=1.5))
        door(s, 0.2, 2.38, 0.22, 10, "+y", "garage")
        door(s, 0.52, 2.38, 0.22, 10, "+y", "garage")
        container(s, 0.76, 1.93, "terra", "y", 0.2, 0.08, 7)
    else:
        for k, r in enumerate(("leaf", "terra", "water", "yellow")):
            bottle_bank(s, 0.22 + k * 0.14, 2.15, r)
        container(s, 0.25, 1.9, "slate", "x", 0.22, 0.08, 7)
        tree(s, 0.8, 2.2, 14, 0.1, "bush", season=st.season)
    # right-front: truck depot (upgrade) or a couple of parked trucks
    if "depot" in st.ups:
        block(s, 2.0, 1.88, 0.88, 0.5, 14, wall_mat("plaster", ramp="wood", shade=1.5, win_w=5, period=9, storey=14,
                                                     glass="slate", lit=0.6),
              roof="flat", roof_mat="roof_flat", rim=M("plaster", ramp="wood", shade=1.5))
        s.box(1.99, 1.87, 11, 0.9, 0.52, 2, P("leaf", 1))
        for i in range(3):
            door(s, 2.06 + i * 0.27, 2.38, 0.2, 10, "+y", "garage")
        garbage_truck(s, 1.62, 2.52, "x", 0.34)
        garbage_truck(s, 2.15, 2.57, "x", 0.34)
    else:
        garbage_truck(s, 2.1, 2.1, "x", 0.34)
        garbage_truck(s, 2.1, 1.88, "x", 0.34)
        tree(s, 2.8, 2.15, 18, 0.12, season=st.season, seed=3)
    # back-right corner: leachate tank + gas flare + trees
    tank(s, 2.45, 0.55, 0.2, 14, "metal_light", roof="dome")
    pole(s, 2.78, 0.3, 28, P("grey", 2, snow=False), w=0.03)
    s.ellipsoid(2.78, 0.3, 30, 0.025, 0.025, 3, ik.mat("lamp", ramp="terra", eramp="terra"))
    pipe(s, (2.45, 0.35, 5), (2.78, 0.35, 5), 0.02, P("yellow", 0.5))
    for (x, y, h) in ((2.2, 1.2, 20), (2.55, 1.45, 17), (2.8, 1.0, 22)):
        tree(s, x, y, h, 0.13, season=st.season, seed=int(x * 10))
    lamp(s, 1.05, 2.45, 14)
    person(s, 1.5, 2.45, "red")
    _fp_fence(s, 4)
    return s


# ---------------------------------------------------------------- incinerator
@model("incinerator", (3, 3), "garbage", "Incinerator", anim=4,
       ups=(("furnace", "extra furnace"), ("filter", "exhaust filter")),
       note="Stack smoke 4 frames: dark and heavy, whiter and thinner with the exhaust filter.")
def incinerator(st):
    s = ik.Scene((3, 3), 72)
    kerb_lot(s, "concrete_ground", outer="grass_dry")
    road(s, 0.1, 2.5, 2.9, 2.9)
    road(s, 1.9, 1.35, 2.9, 2.5)
    # boiler / furnace hall: tall brick block
    top = block(s, 0.2, 0.25, 1.0, 0.95, 52, HALL, roof="flat", roof_mat="roof_gravel")
    s.box(0.19, 0.24, 36, 1.02, 0.97, 3, GREEN)
    # steam drum + pipes on the boiler roof
    hcyl(s, 0.35, 0.5, 55, 0.07, 0.6, M("metal_light", shade=1), "x", 10)
    pole(s, 0.9, 0.35, 8, P("grey", 2), z=52)
    # grate hall (lower, brick, gable) beside it
    if "furnace" in st.ups:
        block(s, 1.25, 0.25, 0.75, 0.95, 46, HALL, roof="flat", roof_mat="roof_gravel")
        s.box(1.24, 0.24, 33, 0.77, 0.97, 3, GREEN)
        hcyl(s, 1.35, 0.6, 49, 0.06, 0.5, M("metal_light", shade=1), "x", 10)
        s.box(1.5, 0.3, 46, 0.2, 0.2, 10, M("metal", ramp="grey", shade=1.5), top=P("grey", 2))
        s.box(0.95, 0.4, 46, 0.15, 0.05, 8, M("metal", ramp="grey", shade=0))
    else:
        rect(s, 1.25, 0.25, 2.0, 1.2, "concrete_ground", layer=2)
        for k in range(3):
            tank(s, 1.45 + k * 0.22, 0.5, 0.08, 10, "metal_light", roof="dome")
        crate_stack(s, 1.35, 0.9, 2, 1, 1, 0.08)
    # bunker (waste pit hall) + tipping hall in front
    block(s, 0.2, 1.2, 1.0, 0.65, 34, CONC_W, roof="gable", roof_mat="roof_metal", roof_h=10, axis="y",
          gable=M("concrete"))
    block(s, 0.2, 1.85, 1.5, 0.6, 22, wall_mat("concrete", win_w=5, win_h=5, period=10, storey=11, glass="slate"),
          roof="flat", roof_mat="roof_metal", rim=SIDING)
    s.box(0.19, 1.84, 17, 1.52, 0.62, 2.5, GREEN)
    for i in range(3):
        door(s, 0.3 + i * 0.45, 2.45, 0.3, 14, "+y", "garage")
    # crane gantry over the bunker (visible above the gable)
    beam(s, (0.3, 1.45, 46), (1.1, 1.45, 46), P("yellow", 0.5, snow=False), w=0.05, hpx=3)
    s.box(0.56, 1.4, 41, 0.1, 0.1, 5, P("grey", -1, snow=False))
    # stack
    sx, sy = 2.55, 0.55
    top = stack(s, sx, sy, 0.12, 100, M("chimney_bands", ramp="leaf", height=8), cap=P("grey", -1))
    filt = "filter" in st.ups
    if filt:
        # baghouse filter block + scrubber tank beside the stack
        s.box(2.1, 0.85, 0, 0.42, 0.4, 20, M("metal", ramp="snow", shade=0.5), top=P("grey", 2))
        for k in range(4):
            s.box(2.13 + k * 0.1, 0.84, 17, 0.07, 0.012, 5, P("leaf", 1))
        pipe(s, (2.2, 0.7, 18), (2.4, 0.7, 18), 0.03, P("grey", 2))
        s.cylinder(2.8, 0.95, 0, 0.1, 15, M("metal_light", shade=1.5), top=P("grey", 3), segs=12)
    else:
        s.box(2.2, 0.9, 0, 0.3, 0.3, 8, M("metal", ramp="grey", shade=0.5), top=P("grey", 1))
    fx0 = 2.0 if "furnace" in st.ups else 1.2
    beam(s, (fx0, 0.55, 40), (sx - 0.1, 0.55, 40), P("grey", 0, snow=False), w=0.07, hpx=4)
    glow = ik.mat("lamp", ramp="terra", eramp="terra", eshade=9.0)
    for k in range(3):                       # glowing furnace inspection slits (lit at night)
        s.box(fx0 + 0.001, 0.4 + k * 0.22, 22, 0.012, 0.1, 4, glow)
    if not st.inactive:
        if filt:
            smoke(s, sx, sy, top, st.frame, n=3, ramp="snow", shade=1.0, rise=9, r0=0.06, grow=0.018,
                  drift=(0.03, -0.02), rough=0.35)
        else:
            smoke(s, sx, sy, top, st.frame, n=4, ramp="grey", shade=-1.5, rise=8, r0=0.1, grow=0.04,
                  drift=(0.03, -0.03))
    # trucks queueing + trucks tipping
    garbage_truck(s, 2.15, 1.55, "y", 0.38)
    garbage_truck(s, 2.5, 1.7, "y", 0.38)
    garbage_truck(s, 0.35, 2.55, "x", 0.34)
    garbage_truck(s, 1.0, 2.62, "x", 0.34)
    # ash heaps & containers
    heap(s, 2.75, 2.0, 0.14, 7, M("rock", ramp="slate", shade=-3), rough=0.45)
    tree(s, 2.8, 2.65, 16, 0.12, "conifer", season=st.season)
    tree(s, 0.12, 2.7, 14, 0.1, "bush", season=st.season)
    lamp(s, 1.75, 2.38, 13)
    person(s, 0.95, 2.35, "yellow")
    fence(s, [(0.04, 0.04), (2.96, 0.04), (2.96, 2.96), (0.04, 2.96)], 4, closed=True)
    return s


# ---------------------------------------------------------------- recycling
@model("recycling", (3, 3), "garbage", "Recycling centre",
       ups=(("depot", "truck depot"),),
       note="Sorting hall with conveyors, colourful bales and containers.")
def recycling(st):
    s = ik.Scene((3, 3), 73)
    kerb_lot(s, "concrete_ground", outer="grass_dry")
    road(s, 0.1, 2.5, 2.9, 2.9)
    road(s, 0.15, 1.35, 1.9, 1.7)
    rect(s, 0.15, 1.52, 1.9, 1.535, "marking_yellow", layer=3)
    # sorting hall: long gable-roofed shed, green siding + band, skylights
    SORT = wall_mat("siding", ramp="leaf", shade=2.0, win_w=6, win_h=4, period=11, storey=14, sill=7,
                    glass="glass", lit=0.6)
    block(s, 0.2, 0.2, 1.9, 1.05, 18, SORT, roof="gable", roof_mat=GREEN_ROOF, roof_h=9, axis="x",
          gable=M("siding", ramp="leaf", shade=2.0), rim=SORT)
    s.box(0.19, 0.19, 13, 1.92, 1.07, 2, P("snow", 1.5))
    for i in range(4):
        door(s, 0.32 + i * 0.45, 1.25, 0.3, 12, "+y", "garage")
    for k in range(4):                                    # roof skylights
        s.box(0.4 + k * 0.45, 0.55, 26, 0.22, 0.2, 1.2, M("glass", shade=1.5))
    # sorting machine tower + conveyors
    s.box(2.2, 0.25, 0, 0.32, 0.45, 28, M("metal", ramp="grey", shade=1.5), top=P("grey", 2))
    for z in (8, 15, 22):
        s.box(2.19, 0.25, z, 0.34, 0.46, 1.5, P("yellow", 0.5, snow=False))
    conveyor(s, (2.36, 0.72, 24), (2.36, 1.45, 6), 0.06)
    conveyor(s, (2.52, 0.45, 16), (2.92, 0.45, 16), 0.05)
    # bale stacks in the yard (colourful)
    ramps = ("red", "water", "yellow", "leaf", "snow", "teal")
    bale_stack(s, 0.2, 1.8, 4, 2, 3, ramps, 0.12, 0.09)
    bale_stack(s, 0.2, 2.15, 3, 2, 2, ("terra", "grey", "water", "yellow"), 0.12, 0.09)
    bale_stack(s, 2.3, 1.5, 2, 3, 2, ("snow", "red", "leaf"), 0.1, 0.08)
    # skip containers
    for k, r in enumerate(("terra", "water", "leaf", "yellow")):
        container(s, 0.9 + (k % 2) * 0.28, 1.78 + (k // 2) * 0.14, r, "x", 0.24, 0.1, 7 + (k % 2))
    # bottle banks
    for k, r in enumerate(("leaf", "terra", "snow", "water")):
        bottle_bank(s, 0.95 + k * 0.1, 2.3, r)
    garbage_truck(s, 1.3, 1.4, "x", 0.34)
    # depot upgrade or parked trucks
    if "depot" in st.ups:
        block(s, 1.85, 1.9, 1.0, 0.5, 14, wall_mat("plaster", ramp="wood", shade=1.5, win_w=5, period=9, storey=14,
                                                   glass="slate", lit=0.6), roof="flat", roof_mat="roof_flat",
              rim=M("plaster", ramp="wood", shade=1.5))
        s.box(1.84, 1.89, 11, 1.02, 0.52, 2, GREEN)
        for i in range(3):
            door(s, 1.93 + i * 0.3, 2.4, 0.22, 10, "+y", "garage")
        garbage_truck(s, 1.45, 2.55, "x", 0.34)
        garbage_truck(s, 2.15, 2.57, "x", 0.34)
    else:
        garbage_truck(s, 1.9, 2.0, "x", 0.34)
        garbage_truck(s, 1.9, 2.22, "x", 0.34)
        tree(s, 2.75, 2.1, 16, 0.11, season=st.season, seed=2)
        tree(s, 2.55, 1.95, 12, 0.09, "bush", season=st.season)
    # office kiosk
    block(s, 1.4, 1.95, 0.36, 0.26, 11, wall_mat("plaster", ramp="sand", shade=0.4, win_w=4, period=8, storey=11),
          roof="hip", roof_mat=M("roof_tiles_brown"), roof_h=4)
    door(s, 1.5, 2.21, 0.1, 7, "+y", "door")
    _recycle_sign(s, 2.7, 1.3)
    lamp(s, 1.0, 2.45, 13)
    person(s, 0.8, 2.4, "leaf")
    person(s, 1.2, 1.9, "yellow")
    fence(s, [(0.04, 0.04), (2.96, 0.04), (2.96, 2.96), (0.04, 2.96)], 4, closed=True)
    return s


def _recycle_sign(s, x, y):
    """Free-standing green board with a white triangle (reads as the recycling logo at 1x)."""
    for dx in (-0.17, 0.15):
        pole(s, x + dx, y, 9, P("grey", 1, snow=False), w=0.02)
    s.box(x - 0.2, y - 0.006, 6, 0.4, 0.012, 9, P("leaf", 2.0, snow=False))
    w = P("snow", 3, snow=False)
    s.box(x - 0.07, y + 0.007, 8, 0.14, 0.006, 1.2, w)
    s.box(x - 0.07, y + 0.007, 8, 0.02, 0.006, 5, w)
    s.box(x + 0.05, y + 0.007, 8, 0.02, 0.006, 5, w)
    s.box(x - 0.07, y + 0.007, 12, 0.14, 0.006, 1.2, w)


# ================================================================ deathcare (grey + dark green, dignified)
STONE_W = wall_mat("plaster", ramp="stone", shade=2.5, win_w=3, win_h=8, period=9, storey=18, sill=5, glass="slate",
                   lit=0.5)
STONE_P = M("plaster", ramp="stone", shade=2.5)
SLATE_ROOF = M("slate", shade=-0.5)
DARK_GRASS = M("grass", ramp="leaf", shade=0.6)
HEDGE_C = M("foliage", ramp="leaf", shade=-1.5)
GRAVEL = M("gravel", ramp="sand", shade=0.6)


def cross(s, x, y, z, h=7):
    m = P("grey", 4, snow=False)
    s.box(x - 0.007, y - 0.007, z, 0.014, 0.014, h, m)
    s.box(x - 0.028, y - 0.007, z + h * 0.62, 0.056, 0.014, 1.3, m)


def hedge_line(s, pts, h=5, t=0.05, season="summer"):
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        x0, x1 = min(ax, bx), max(ax, bx)
        y0, y1 = min(ay, by), max(ay, by)
        s.box(x0 - t / 2, y0 - t / 2, 0, x1 - x0 + t, y1 - y0 + t, h,
              HEDGE_C if season != "winter" else P("leaf", -3))


def cypress(s, x, y, h=26, season="summer"):
    """Tall slim dark cypress."""
    s.cylinder(x, y, 0, 0.012, 4, "bark", segs=6)
    s.ellipsoid(x, y, h * 0.5 + 2, 0.045, 0.045, h * 0.5, M("conifer", shade=-1.0), rough=0.2)


def gate(s, x, y, w=0.4):
    """Entrance gate on the +y edge: two stone piers + a cross bar."""
    for xx in (x, x + w - 0.07):
        s.box(xx, y, 0, 0.07, 0.07, 14, M("stone", shade=1.5), top=M("stone", shade=2.5))
    s.box(x, y + 0.02, 12, w, 0.03, 2, P("grey", -2, snow=False))


def grave_cells(x0, y0, cols, rows, px, py):
    return [(x0 + c * px, y0 + r * py) for r in range(rows) for c in range(cols)]


def _grave(s, x, y, i):
    """One grave: low mound + headstone (slab, cross, tall stele or rounded slab) facing the +y side."""
    st_ = M("stone", ramp="grey", shade=0.2 + (i % 3) * 0.7)
    k = i % 4
    s.box(x, y + 0.045, 0, 0.075, 0.12, 1.0, M("grass", ramp="leaf", shade=0.0))          # grave mound
    if k == 0:
        s.box(x + 0.006, y + 0.035, 0, 0.063, 0.025, 5, st_, top=st_)
    elif k == 1:
        s.box(x + 0.028, y + 0.035, 0, 0.02, 0.02, 8, st_)
        s.box(x + 0.006, y + 0.035, 4.5, 0.064, 0.02, 1.8, st_)
    elif k == 2:
        s.box(x + 0.006, y + 0.035, 0, 0.063, 0.04, 3, st_, top=st_)
    else:
        s.box(x + 0.012, y + 0.035, 0, 0.05, 0.022, 7, st_, top=P("grey", 4, snow=False))


@model("cemetery", (3, 3), "deathcare", "Cemetery", fill=True,
       ups=(("columbarium", "columbarium"),),
       note="Gravestone rows fill with the fill level 0..100%.")
def cemetery(st):
    s = ik.Scene((3, 3), 81)
    f = 0.5 if st.fill is None else float(st.fill)
    lot(s, DARK_GRASS)
    # main axis gravel path from the front gate to the chapel; cross path
    rect(s, 1.35, 0.8, 1.65, 3.0, GRAVEL, layer=1)
    rect(s, 0.15, 2.05, 2.85, 2.2, GRAVEL, layer=1)
    rect(s, 1.0, 0.8, 2.0, 1.0, GRAVEL, layer=1)
    # perimeter hedge with a gate gap at the front
    hedge_line(s, [(0.05, 0.05), (2.95, 0.05), (2.95, 2.95), (1.85, 2.95)], 5, 0.05, st.season)
    hedge_line(s, [(1.15, 2.95), (0.05, 2.95), (0.05, 0.05)], 5, 0.05, st.season)
    gate(s, 1.2, 2.9, 0.6)
    # chapel at the back: stone nave, slate gable roof, small bell tower with spire
    block(s, 1.05, 0.2, 0.9, 0.6, 18, STONE_W, roof="gable", roof_mat=SLATE_ROOF, roof_h=11, axis="y", gable=STONE_P)
    s.box(1.4, 0.8, 0, 0.2, 0.05, 3, M("stone", shade=1.5), top=M("stone", shade=2))     # porch step
    door(s, 1.42, 0.8, 0.16, 10, "+y", "door")
    block(s, 1.38, 0.28, 0.24, 0.24, 36, STONE_W, roof="hip", roof_mat=SLATE_ROOF, roof_h=14)
    cross(s, 1.5, 0.4, 50, 6)
    # grave fields (left, right) filled by deterministic order
    cells = (grave_cells(0.25, 0.95, 5, 6, 0.19, 0.17) + grave_cells(1.85, 0.95, 5, 5, 0.19, 0.17)
             + grave_cells(0.25, 2.3, 5, 3, 0.19, 0.17))
    rng = np.random.default_rng(5)
    order = rng.permutation(len(cells))
    on = set(order[:int(round(f * len(cells)))].tolist())
    for i, (x, y) in enumerate(cells):
        if i in on:
            _grave(s, x, y, i)
        else:
            rect(s, x - 0.01, y + 0.04, x + 0.07, y + 0.13, M("grass", shade=0.2), layer=2)
    # right-front: columbarium wall (upgrade) or rose garden
    if "columbarium" in st.ups:
        s.box(2.2, 2.35, 0, 0.6, 0.3, 14, M("concrete", ramp="stone", shade=2.0), top=M("stone", shade=2.5))
        niche = M("windows_concrete", ramp="stone", shade=1.5, win_w=3, win_h=3, period=5, storey=4, sill=1,
                  margin=2, glass="slate", lit=0.0, base=M("concrete", ramp="stone", shade=2.0))
        s.box(2.2, 2.65, 0, 0.6, 0.015, 13, niche)
        s.box(2.15, 2.32, 14, 0.7, 0.36, 2, P("grey", 1))
        for x in (2.25, 2.55):
            s.box(x, 2.7, 0, 0.1, 0.05, 2, M("stone", shade=2))
    else:
        rect(s, 2.2, 2.3, 2.85, 2.85, M("meadow", ramp="snow", shade=-0.8, flowers=0.3), layer=2)
    # cypress avenue
    for (x, y) in ((1.22, 1.15), (1.78, 1.15), (1.22, 1.7), (1.78, 1.7), (1.22, 2.5), (1.78, 2.5), (0.15, 0.2),
                   (2.85, 0.2), (2.85, 2.8)):
        cypress(s, x, y, 26 if y < 1.5 else 22, st.season)
    tree(s, 0.2, 2.8, 20, 0.12, "conifer", season=st.season)
    tree(s, 2.7, 1.3, 22, 0.13, "round", season=st.season, seed=3)
    bench(s, 1.0, 2.25, "x", 0.16)
    bench(s, 1.85, 2.25, "x", 0.16)
    lamp(s, 1.25, 2.85, 12)
    person(s, 1.5, 2.55, "slate", "slate")
    person(s, 0.95, 1.9, "slate", "slate")
    return s


@model("crematorium", (2, 2), "deathcare", "Crematorium", front=True, anim=4,
       ups=(("garage", "hearse garage"),),
       note="Slim chimney wisp (4 frames), hearse at the door.")
def crematorium(st):
    s = ik.Scene((2, 2), 82)
    lot(s, DARK_GRASS)
    rect(s, 0.7, 1.15, 1.3, 2.0, GRAVEL, layer=1)
    rect(s, 0.7, 1.55, 1.9, 1.95, GRAVEL, layer=1)
    # chapel hall: stone walls, tall windows, slate gable roof (ridge along y: gable front)
    block(s, 0.3, 0.25, 0.9, 0.9, 20, STONE_W, roof="gable", roof_mat=SLATE_ROOF, roof_h=12, axis="y", gable=STONE_P)
    s.box(0.29, 0.24, 18, 0.92, 0.92, 2, P("grey", 0.5))
    # porch: columns + small gable
    for x in (0.55, 0.9):
        s.box(x, 1.2, 0, 0.04, 0.04, 12, P("snow", 0.5))
    s.box(0.5, 1.14, 12, 0.5, 0.14, 2, P("grey", 1.5))
    door(s, 0.65, 1.15, 0.2, 10, "+y", "door")
    cross(s, 0.75, 0.3, 32, 7)
    # crematory annex at the back with the slim brick chimney
    block(s, 1.2, 0.3, 0.55, 0.5, 14, M("windows_brick", ramp="stone", shade=1.0, base=M("brick", ramp="stone", shade=1.0),
                                       win_w=3, win_h=4, period=9, storey=14, glass="slate", lit=0.4),
          roof="flat", roof_mat="roof_gravel")
    top = stack(s, 1.5, 0.45, 0.06, 56, M("brick", shade=0.0))
    if not st.inactive:
        smoke(s, 1.5, 0.45, top, st.frame, n=3, ramp="snow", shade=0.0, rise=7, r0=0.04, grow=0.016,
              drift=(0.02, -0.015), rough=0.3)
    # hearse garage (upgrade) or garden bed
    if "garage" in st.ups:
        block(s, 1.3, 0.95, 0.55, 0.55, 12, M("plaster", ramp="stone", shade=2.0), roof="flat", roof_mat="roof_flat",
              rim=STONE_P)
        door(s, 1.4, 1.5, 0.35, 9, "+y", "garage")
        vehicle(s, 1.4, 1.62, "x", "hearse", "slate")
    else:
        rect(s, 1.3, 0.95, 1.85, 1.45, M("meadow", ramp="snow", shade=-0.8, flowers=0.25), layer=2)
        cypress(s, 1.6, 1.1, 20, st.season)
    vehicle(s, 0.82, 1.45, "y", "hearse", "slate")
    # garden: hedges, cypress, benches
    hedge_line(s, [(0.15, 1.35), (0.15, 1.9), (0.55, 1.9)], 4, 0.05, st.season)
    hedge_line(s, [(1.85, 0.2), (1.85, 0.8)], 4, 0.05, st.season)
    cypress(s, 0.15, 0.2, 24, st.season)
    cypress(s, 1.9, 1.05 if "garage" not in st.ups else 0.9, 24, st.season)
    cypress(s, 0.12, 1.2, 20, st.season)
    tree(s, 1.85, 1.85, 14, 0.1, "bush", season=st.season)
    bench(s, 0.25, 1.6, "y", 0.14)
    lamp(s, 0.62, 1.8, 12)
    person(s, 0.6, 1.4, "slate", "slate")
    person(s, 0.68, 1.42, "slate", "slate")
    return s
