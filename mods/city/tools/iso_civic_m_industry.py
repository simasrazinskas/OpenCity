"""CIVIC models: industry hubs (farm, forestry, quarry, mine, oil, fish) and the oil derrick. Accent: olive.

Hubs are the buildings the player places; the matching extractor area tiles live in iso_civic_m_areas.py.
"""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, panel, wall_mat, stack, lathe, tank, pole, \
    lattice, beam, pipe, fence, hcyl, cone, solid_wall
from iso_civic_props import vehicle, tree, smoke, heap, lamp, crate_stack, person, bench, sea, WATER_Z
from iso_civic_states import model

S = ik.S
BARN_RED = M("siding", ramp="red", shade=-1.2)
BARN_ROOF = M("roof_metal", ramp="slate", shade=0.0)
WHITE = P("snow", 1.5)
OLIVE = P("olive", 0.5)
HOUSE_WALL = wall_mat("plaster", ramp="snow", shade=0.4, win_w=4, win_h=5, period=8, storey=10, sill=3,
                      glass="glass", lit=0.8)
HOUSE_ROOF = M("roof_tiles_brown", shade=-0.2)
SILO = M("metal_light", shade=1.0)
YARD = M("dirt", shade=0.3)
WOOD_FENCE = P("wood", 1.5)


def gambrel(s, x, y, dx, dy, z, h1, h2, roof, gable, inset=0.2, axis_y=True):
    """Gambrel barn roof, ridge along y (gable end toward +y). h1 = steep lower part, h2 = shallow upper part,
    inset = fraction of dx the lower slope runs in."""
    a = dx * inset
    xm = x + dx / 2
    prof = [(x, z), (x + a, z + h1), (xm, z + h1 + h2), (x + dx - a, z + h1), (x + dx, z)]
    for yy, o in ((y + dy, (0, 1, 0)), (y, (0, -1, 0))):
        s.poly([(px, yy, pz) for px, pz in prof], gable, outward=o)
    ov = 0.03
    for (xa, za), (xb, zb) in zip(prof, prof[1:]):
        sgn = 1 if (xa + xb) / 2 > xm else -1
        o = (sgn * abs(zb - za) / S, 0, abs(xb - xa))
        s.poly([(xa, y - ov, za), (xb, y - ov, zb), (xb, y + dy + ov, zb), (xa, y + dy + ov, za)], roof, outward=o)


def x_door(s, x, y, w, h, z=0.0, panel_m=None, frame=WHITE, face="+y"):
    """Barn door with a white frame and an X brace."""
    pm = panel_m or P("red", -0.5)
    t = 0.014
    s.box(x - 0.02, y, z, w + 0.04, t, h + 1.5, frame)
    s.box(x, y + t, z, w, 0.006, h, pm)
    d = 0.4
    beam(s, (x, y + t + 0.008, z), (x + w, y + t + 0.008, z + h), frame, w=0.012, hpx=1)
    beam(s, (x, y + t + 0.008, z + h), (x + w, y + t + 0.008, z), frame, w=0.012, hpx=1)


def round_bale(s, x, y, z=0.0, axis="y", mat=None):
    m = mat or ik.Material("yellow", 0.0, None, dither=0.0, snow=True)
    hcyl(s, x, y, z + 2.2, 0.056, 0.1, m, axis, 10)


def bale_stack(s, x, y, nx=2, ny=1, nz=2):
    m = ik.Material("yellow", 0.4, None, dither=0.3)
    for k in range(nz):
        for i in range(nx - k if nz > 1 else nx):
            for j in range(ny):
                s.box(x + i * 0.12 + k * 0.06, y + j * 0.07, k * 3.6, 0.11, 0.065, 3.5, m)


def silo(s, cx, cy, r, h, mat=None, cap="dome"):
    mat = mat or SILO
    s.cylinder(cx, cy, 0, r, h, mat, top=P("grey", 2), segs=18)
    for z in range(8, int(h), 9):
        s.cylinder(cx, cy, z, r + 0.004, 1.2, P("grey", -0.5, snow=False), segs=18)
    s.dome(cx, cy, h, r, r * 18, P("grey", 3))
    s.box(cx - 0.012, cy - 0.012, h + r * 18, 0.024, 0.024, 3, P("grey", 1))


@model("farm-hub", (3, 3), "industry", "Farm hub", front=True,
       note="Barn, silos, farmhouse, tractor. Extractor field tiles are the areas/ set.")
def farm(st):
    s = ik.Scene((3, 3), 51)
    lot(s, "grass")
    rect(s, 0.15, 1.45, 2.85, 2.95, YARD, layer=1)
    rect(s, 0.58, 1.3, 0.92, 3.0, "gravel", layer=2)          # farm track from the barn door to the front
    # barn (gambrel, red) with big door facing +y
    bx, by, bw, bd = 0.25, 0.25, 1.0, 1.0
    s.box(bx, by, 0, bw, bd, 17, BARN_RED)
    s.box(bx - 0.01, by - 0.01, 0, bw + 0.02, bd + 0.02, 2.2, WHITE)       # white sill
    gambrel(s, bx, by, bw, bd, 17, 11, 7, BARN_ROOF, BARN_RED)
    x_door(s, bx + 0.33, by + bd, 0.34, 13)
    s.box(bx + 0.2, by + bd + 0.0, 22, 0.1, 0.012, 4, WHITE)                  # hayloft hatch
    s.box(bx + 0.72, by + bd + 0.0, 22, 0.1, 0.012, 4, WHITE)
    s.box(bx + 0.4, by + bd, 26, 0.2, 0.012, 3, WHITE)
    # annex shed on the barn's right side
    block(s, bx + bw, by + 0.35, 0.28, 0.6, 9, M("siding", ramp="red", shade=-1.5), roof="shed", roof_h=5,
          roof_mat=BARN_ROOF, axis="-y")
    # silos
    for k, (cx, cy) in enumerate(((1.75, 0.38), (2.1, 0.38), (2.45, 0.38))):
        silo(s, cx, cy, 0.13, 40 + (k % 2) * 6, M("metal", ramp="water" if k == 1 else "grey", shade=1.5))
    s.box(1.6, 0.6, 20, 0.9, 0.05, 1.5, P("grey", -1))                         # feed belt between silos
    block(s, 1.65, 0.62, 0.8, 0.28, 9, M("siding", ramp="grey", shade=1.5), roof="shed", roof_h=3,
          roof_mat="roof_metal", axis="-y")
    # farmhouse
    hx, hy = 2.0, 1.85
    block(s, hx, hy, 0.75, 0.6, 17, HOUSE_WALL, roof="gable", roof_mat=HOUSE_ROOF, roof_h=12, axis="y",
          gable=HOUSE_WALL, rim="plaster_white")
    door(s, hx + 0.28, hy + 0.6, 0.12, 8, "+y", "door")
    s.box(hx + 0.2, hy + 0.6, 9, 0.28, 0.14, 1.2, P("wood", 1))              # porch roof
    for dx in (0.2, 0.46):
        s.box(hx + dx, hy + 0.7, 0, 0.02, 0.02, 9, WHITE)
    s.box(hx + 0.55, hy + 0.1, 17, 0.07, 0.07, 9, M("brick"), top=P("grey", -2))   # chimney
    # tractor with hay trailer + round bales
    vehicle(s, 1.0, 2.2, "x", "tractor", "leaf")
    s.box(1.22, 2.22, 2, 0.26, 0.1, 2, P("wood", 0.5))
    round_bale(s, 1.27, 2.22, 2.0, "y")
    for (x, y) in ((0.3, 1.5), (0.3, 1.65), (0.3, 1.8)):
        round_bale(s, x, y)
    round_bale(s, 0.3, 1.575, 4.3, "y")
    # corral fence and a few chickens
    fence(s, [(0.12, 2.1), (0.12, 2.9), (1.0, 2.9)], 4, WOOD_FENCE)
    heap(s, 0.5, 2.55, 0.18, 4, M("meadow", ramp="yellow", shade=0.5))
    # trees + windpump
    for (x, y, h) in ((2.8, 1.45, 22), (2.8, 2.7, 18), (1.7, 2.75, 16)):
        tree(s, x, y, h, 0.14, season=st.season, seed=int(x * 7))
    lattice(s, 1.55, 1.5, 0.1, 0.1, 30, P("grey", 2, snow=False), taper=0.02, levels=4)
    s.ellipsoid(1.6, 1.55, 30, 0.07, 0.07, 3, P("grey", 3, snow=False))
    return s


# ---------------------------------------------------------------- forestry
LOG = ik.Material("wood", -0.3, None, dither=0.25)
LOG_L = ik.Material("wood", 0.8, None, dither=0.25)
PLANK = ik.Material("sand", -0.2, None, dither=0.3)
TIMBER = M("wood", shade=-0.3)


def logpile(s, x, y, n, L, axis="x", r=0.034, mats=(LOG, LOG_L)):
    """Pyramid of horizontal logs: bottom row of n logs (axis along `axis`), x/y = rear-left corner."""
    k = 0
    for row in range(n):
        for i in range(n - row):
            off = (row * r)
            c = i * 2 * r + r + off
            z = r * S + row * r * S * 1.72
            m = mats[(i + row) % 2]
            if axis == "x":
                hcyl(s, x, y + c, z, r, L, m, "x", 8)
            else:
                hcyl(s, x + c, y, z, r, L, m, "y", 8)
            k += 1


def logs_on(s, x, y, z, n, L, axis="x", r=0.028):
    for i in range(n):
        if axis == "x":
            hcyl(s, x, y + i * 2 * r * 0.9 + r, z + r * S, r, L, LOG if i % 2 else LOG_L, "x", 8)
        else:
            hcyl(s, x + i * 2 * r * 0.9 + r, y, z + r * S, r, L, LOG if i % 2 else LOG_L, "y", 8)


@model("forestry-hub", (2, 2), "industry", "Forestry hub", front=True,
       note="Sawmill shed, log piles, log truck and loader, conifers.")
def forestry(st):
    s = ik.Scene((2, 2), 52)
    lot(s, "forest_floor")
    rect(s, 0.08, 0.85, 1.92, 1.92, M("dirt", shade=-0.2), layer=1)
    rect(s, 0.08, 1.6, 1.92, 1.92, "mud", layer=2)
    # sawmill: long wooden shed, open bay toward the yard, tall sawdust silo and stack
    wall = M("wood", ramp="wood", shade=0.6)
    block(s, 0.12, 0.12, 1.2, 0.62, 13, wall, roof="gable", roof_h=9, roof_mat=M("roof_metal", ramp="olive", shade=-0.5),
          axis="x", gable=wall)
    door(s, 0.2, 0.74, 0.5, 10, "+y", "garage")
    door(s, 0.8, 0.74, 0.34, 8, "+y", M("glass_dark"))
    s.box(0.12, 0.7, 11, 1.2, 0.05, 1.4, P("olive", 0.5))                       # olive band
    # inclined feed conveyor from the log deck into the mill
    beam(s, (0.2, 0.95, 2), (0.55, 0.77, 9), P("yellow", 0.5, snow=False), w=0.07, hpx=3)
    # sawdust silo + burner stack
    s.cylinder(1.5, 0.28, 0, 0.14, 30, M("metal", ramp="olive", shade=1.0), top=P("grey", 2), segs=16)
    cone(s, 1.5, 0.28, 30, 0.14, 8, P("grey", 1.5), segs=16)
    stack(s, 1.28, 0.62, 0.05, 30, M("metal_light", shade=0.0), cap=P("grey", -1))
    # log decks
    logpile(s, 0.12, 1.0, 4, 0.5)
    logpile(s, 0.12, 1.5, 3, 0.5)
    logpile(s, 1.2, 0.95, 3, 0.38, "y")
    # planks + sawdust
    for k in range(3):
        s.box(1.5 + k * 0.14, 0.88, 0, 0.12, 0.34, 6 - k, PLANK, top=P("sand", 2.5))
    heap(s, 1.7, 0.6, 0.13, 6, M("sand", shade=1.5), rough=0.4)
    # log truck with a load, loader forklift carrying a log
    vehicle(s, 0.8, 1.7, "x", "truck", "olive")
    logs_on(s, 0.85, 1.72, 11, 3, 0.36, "x", r=0.022)
    vehicle(s, 1.3, 1.5, "x", "forklift", "yellow")
    hcyl(s, 1.42, 1.5, 7, 0.02, 0.24, LOG, "y", 8)
    # conifers around the edge (back and right)
    for (x, y, h) in ((1.86, 0.22, 30), (1.88, 0.8, 26), (1.72, 0.5, 22), (0.07, 1.35, 22)):
        tree(s, x, y, h, 0.12, "conifer", season=st.season)
    return s


# ---------------------------------------------------------------- quarry
GRAVEL_HEAP = ik.Material("stone", -0.5, None, dither=0.6)
SAND_HEAP = ik.Material("sand", -0.5, None, dither=0.6)
ROCKH = ik.Material("stone", -1.6, None, dither=0.6)


def gravel_cone(s, cx, cy, r, h, mat=GRAVEL_HEAP):
    cone(s, cx, cy, 0, r, h, mat, segs=16)


def dump_truck(s, x, y, paint="yellow", loaded=True):
    """Big quarry haul truck (axis x): chassis, big wheels, tipping body with a rock load, cab with canopy."""
    body = P(paint, 0.3, snow=False)
    dark = P("grey", -4, snow=False)
    tyre = P("grey", -6, snow=False)
    s.box(x, y + 0.02, 5, 0.52, 0.16, 2.5, dark)                                  # chassis
    for t in (0.1, 0.4):
        hcyl(s, x + t - 0.0, y - 0.02, 4.0, 0.06, 0.24, tyre, "y", 10)
    s.box(x, y, 7.5, 0.38, 0.2, 5, body, top=body)                                # tipping body
    s.box(x - 0.01, y - 0.005, 12, 0.4, 0.21, 1.2, P(paint, 1.5, snow=False))     # body rim
    if loaded:
        s.ellipsoid(x + 0.19, y + 0.1, 13, 0.16, 0.08, 3.2, ROCKH, zmin=12.5, rough=0.4)
    s.box(x + 0.38, y + 0.02, 7.5, 0.12, 0.16, 6, body)                            # cab
    s.box(x + 0.36, y, 14, 0.18, 0.2, 1.2, body)                                  # canopy over body and cab
    s.box(x + 0.495, y + 0.03, 10, 0.01, 0.14, 3, P("glass", -1, snow=False))


@model("quarry-hub", (3, 3), "industry", "Quarry hub", front=True,
       note="Crusher tower with inclined conveyor, gravel heaps, dump truck, stepped rock face at the back.")
def quarry(st):
    s = ik.Scene((3, 3), 53)
    lot(s, M("gravel", shade=-0.2))
    rect(s, 0.1, 1.5, 2.9, 2.9, M("dirt", ramp="stone", shade=-1.2), layer=1)
    # stepped rock face along the back-left edges
    face = ik.Material("stone", -2.0, None, dither=0.4)
    topr = ik.Material("stone", -0.4, None, dither=0.5)
    for (x0, y0, dx, dy, h) in ((0.0, 0.0, 1.0, 0.45, 8), (0.0, 0.0, 0.55, 1.1, 8), (0.0, 0.0, 0.85, 0.25, 16),
                                 (0.0, 0.0, 0.3, 0.8, 16)):
        s.box(x0, y0, 0, dx, dy, h, M("rock", shade=-1.5), top=M("rock", shade=0.5))
    s.box(0.0, 0.0, 0, 0.45, 0.4, 24, M("rock", shade=-1.5), top=M("rock", shade=0.8))
    heap(s, 0.7, 0.9, 0.18, 6, ROCKH, rough=0.5)
    # crusher: tall steel tower with hopper, jaw crusher box, discharge chute
    cx, cy = 1.35, 0.55
    s.box(cx, cy, 0, 0.55, 0.45, 20, M("metal", ramp="olive", shade=0.8), top=P("grey", 1))
    s.box(cx + 0.08, cy + 0.06, 20, 0.4, 0.33, 14, M("metal", ramp="grey", shade=1.0), top=P("grey", 2))
    lattice(s, cx + 0.02, cy + 0.02, 0.1, 0.1, 46, P("grey", 2, snow=False), taper=0.0, levels=5)
    lattice(s, cx + 0.43, cy + 0.33, 0.1, 0.1, 46, P("grey", 2, snow=False), taper=0.0, levels=5)
    s.box(cx - 0.02, cy - 0.02, 34, 0.6, 0.5, 4, P("yellow", 0.5, snow=False))
    s.box(cx + 0.1, cy + 0.05, 38, 0.36, 0.34, 6, M("metal", ramp="olive", shade=1.5), top=P("grey", 2))
    door(s, cx + 0.12, cy + 0.45, 0.3, 9, "+y", "garage")
    # inclined conveyor from the heap-side up into the hopper, and a discharge conveyor to the heap
    beam(s, (0.95, 1.35, 2), (cx + 0.2, cy + 0.5, 30), P("grey", 3, snow=False), w=0.08, hpx=3)
    for k in range(4):
        t = (k + 0.5) / 4
        pole(s, 0.95 + (cx + 0.2 - 0.95) * t, 1.35 + (cy + 0.5 - 1.35) * t, 2 + 28 * t, P("grey", 0, snow=False), z=0)
    beam(s, (cx + 0.55, cy + 0.2, 20), (2.1, 0.95, 4), P("yellow", 0.5, snow=False), w=0.08, hpx=3)
    # gravel stockpiles by size
    gravel_cone(s, 2.15, 1.0, 0.3, 19)
    gravel_cone(s, 2.6, 0.7, 0.22, 14, SAND_HEAP)
    gravel_cone(s, 2.6, 1.3, 0.2, 12, ROCKH)
    # front: weigh bridge office + dump truck + excavator-ish loader
    block(s, 0.2, 2.1, 0.4, 0.3, 11, M("siding", ramp="yellow", shade=0.5), roof_mat="roof_flat")
    door(s, 0.35, 2.4, 0.1, 7, "+y", "door")
    dump_truck(s, 1.05, 2.0)
    dump_truck(s, 1.75, 2.45, "olive", loaded=False)
    # barrels, rocks
    for (x, y, r, h) in ((2.75, 2.3, 0.1, 4), (2.55, 2.7, 0.07, 3), (0.2, 2.8, 0.09, 4)):
        heap(s, x, y, r, h, ROCKH, rough=0.5)
    fence(s, [(0.05, 2.95), (2.95, 2.95), (2.95, 1.7)], 4, closed=False)
    return s


# ---------------------------------------------------------------- mine
ORE_DARK = ik.Material("grey", -2.6, None, dither=0.55)
ORE_MID = ik.Material("grey", -1.2, None, dither=0.55)
STEEL = P("grey", 1.0, snow=False)
STEEL_Y = P("yellow", 0.5, snow=False)


def plane_pt(hub, u, v):
    """Point in the camera-facing vertical plane through `hub` (normal (1,1,0)); u along the screen's
    horizontal, v up, both in metric px."""
    k = u / (S * math.sqrt(2))
    return (hub[0] + k, hub[1] - k, hub[2] + v)


def winding_wheel(s, hub, r, ang, mat=STEEL, spokes=4, segs=16):
    """Sheave wheel in the camera-facing plane: rim polygon + spokes (rotated by ang)."""
    pts = [plane_pt(hub, r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs))
           for k in range(segs)]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        beam(s, a, b, mat, w=0.035, hpx=2.4)
    for k in range(spokes):
        a = ang + k * 2 * math.pi / spokes
        beam(s, hub, plane_pt(hub, r * math.cos(a), r * math.sin(a)), mat, w=0.025, hpx=2.0)
    s.box(hub[0] - 0.02, hub[1] - 0.02, hub[2] - 2, 0.04, 0.04, 4, STEEL_Y)


def headframe(s, cx, cy, h, base=0.4, top=0.14, mat=None, levels=4):
    """Heavy steel A-frame headgear: 4 thick legs tapering to the top, ring braces and X bracing."""
    m = mat or P("yellow", -0.5, snow=False)
    dark = P("grey", -1, snow=False)
    b, tp = base / 2, top / 2
    corners = ((-1, -1), (1, -1), (-1, 1), (1, 1))
    for sx, sy in corners:
        beam(s, (cx + sx * b, cy + sy * b, 3), (cx + sx * tp, cy + sy * tp, h), m, w=0.04, hpx=2.6)
    for i in range(1, levels + 1):
        t = i / (levels + 1)
        r = b + (tp - b) * t
        z = 3 + (h - 3) * t
        for (ax, ay), (bx, by) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((-1, 1), (1, 1)), ((-1, -1), (-1, 1))):
            beam(s, (cx + ax * r, cy + ay * r, z), (cx + bx * r, cy + by * r, z), m, w=0.03, hpx=2)
        if i < levels + 1:
            t2 = (i + 1) / (levels + 1)
            r2 = b + (tp - b) * t2
            z2 = 3 + (h - 3) * t2
            beam(s, (cx - r, cy + r, z), (cx + r2, cy + r2, z2), dark, w=0.02, hpx=1.6)
            beam(s, (cx + r, cy - r, z), (cx + r2, cy + r2, z2), dark, w=0.02, hpx=1.6)
    s.box(cx - tp - 0.02, cy - tp - 0.02, h - 1.5, top + 0.04, top + 0.04, 3, STEEL)


def ore_tub(s, x, y, ore=True, ramp="terra"):
    s.box(x, y - 0.075, 2, 0.2, 0.15, 5, M("metal", ramp=ramp, shade=-0.3, snow=False), top=P("slate", -3, snow=False))
    for lx in (0.03, 0.13):
        for ly in (-0.075, 0.05):
            s.box(x + lx, y + ly, 0.5, 0.045, 0.028, 2, P("grey", -4, snow=False))
    if ore:
        s.ellipsoid(x + 0.1, y, 7, 0.085, 0.065, 2.8, ORE_MID, zmin=7, rough=0.4)


@model("mine-hub", (3, 3), "industry", "Mine hub", front=True, anim=4,
       note="Headframe with turning winding wheel (4 frames), winding house, spoil heap, ore wagons on a track.")
def mine(st):
    s = ik.Scene((3, 3), 54)
    lot(s, M("gravel", shade=-1.3))
    rect(s, 0.1, 1.2, 2.9, 2.9, M("dirt", ramp="stone", shade=-1.6), layer=1)
    # spoil heap (back-left corner) with a tipping conveyor
    s.ellipsoid(0.42, 0.42, 0, 0.38, 0.36, 22, ORE_DARK, zmin=0, rough=0.35)
    s.ellipsoid(0.75, 0.3, 0, 0.22, 0.2, 13, ORE_DARK, zmin=0, rough=0.4)
    beam(s, (0.9, 0.95, 3), (0.5, 0.5, 21), STEEL_Y, w=0.07, hpx=3)
    # headframe with a sheave wheel at the top
    cx, cy = 1.2, 1.45
    s.box(cx - 0.2, cy - 0.2, 0, 0.4, 0.4, 3, M("concrete", shade=0.5))
    headframe(s, cx, cy, 52)
    hub = (cx, cy, 60)
    ang = 0.0 if st.inactive else st.frame * (math.pi / 2) / 4
    winding_wheel(s, hub, 11, ang, P("grey", 0.5, snow=False))
    # winding house (right of the headframe in screen space) + cable
    block(s, 1.9, 0.55, 0.65, 0.55, 15, wall_mat("brick", win_w=3, win_h=6, period=8, storey=14, glass="slate",
                                                  lit=0.6),
          roof="gable", roof_mat=M("roof_metal", ramp="slate", shade=0.5), roof_h=9, axis="y", gable=M("brick"))
    door(s, 2.05, 1.1, 0.22, 8, "+y", "garage")
    beam(s, plane_pt(hub, 11, 0), (2.1, 0.9, 20), P("grey", -1, snow=False), w=0.016, hpx=1)
    beam(s, plane_pt(hub, 0, -11), (cx, cy, 6), P("grey", -1, snow=False), w=0.016, hpx=1)
    # track with ore tubs along the front
    rail = P("grey", 1.5, snow=False)
    for k in range(14):
        s.box(0.2 + k * 0.19, 2.32, 0, 0.05, 0.3, 1.0, P("wood", -1.5))
    for yy in (2.38, 2.54):
        s.box(0.15, yy, 1.0, 2.7, 0.025, 1.0, rail)
    for x in (0.4, 0.75, 1.1):
        ore_tub(s, x, 2.46)
    ore_tub(s, 1.6, 2.46, ore=False, ramp="olive")
    # office hut, ore bin, lamp, lorry
    block(s, 2.2, 1.65, 0.5, 0.35, 11, M("siding", ramp="olive", shade=0.8), roof="shed", roof_h=4,
          roof_mat="roof_metal", axis="-y")
    door(s, 2.35, 2.0, 0.1, 7, "+y", "door")
    s.box(1.65, 1.55, 0, 0.3, 0.3, 8, M("metal", ramp="yellow", shade=-0.5), top=P("grey", -3))
    heap(s, 1.8, 1.7, 0.13, 3, ORE_MID)
    vehicle(s, 2.0, 2.65, "y", "truck", "olive")
    lamp(s, 2.75, 2.2, 16)
    return s


# ---------------------------------------------------------------- oil
TANK_W = M("metal_light", shade=2.5)
RED_BAND = P("red", 0.5, snow=False)


def flare(s, x, y, h, frame, on=True):
    pole(s, x, y, h, P("grey", 1, snow=False), w=0.045)
    s.box(x - 0.04, y - 0.04, h - 2, 0.08, 0.08, 2.5, STEEL)
    s.box(x - 0.055, y - 0.055, 0, 0.11, 0.11, 3, P("grey", -1, snow=False))
    if not on:
        return
    sway = (0.012, 0.0, -0.012, 0.004)[frame % 4]
    hts = (11, 14, 10, 13)[frame % 4]
    outer = ik.Material("terra", 5.0, None, dither=0.0, snow=False)
    flame_o = M("lamp", ramp="terra", eramp="terra", eshade=9, shade=5)
    flame_i = M("lamp", ramp="yellow", eramp="yellow", eshade=11, shade=7)
    s.ellipsoid(x + sway, y, h + hts * 0.5, 0.045, 0.045, hts * 0.65, flame_o, zmin=h)
    s.ellipsoid(x + sway * 1.6, y, h + hts * 0.45, 0.028, 0.028, hts * 0.45, flame_i, zmin=h)
    s.ellipsoid(x - sway, y + 0.01, h + hts * 0.95, 0.012, 0.012, 1.6, flame_o)


@model("oil-hub", (2, 2), "industry", "Oil hub", front=True, anim=4,
       note="Storage tanks, small refinery column, gas flare flame (4 frames).")
def oil(st):
    s = ik.Scene((2, 2), 55)
    kerb_lot(s, M("concrete_ground", shade=-0.5), outer="gravel")
    rect(s, 0.9, 1.35, 1.9, 1.9, "asphalt", layer=2)
    # two large floating-roof tanks with a red band, one smaller
    for (cx, cy, r, h) in ((0.42, 0.45, 0.3, 24), (1.15, 0.38, 0.24, 20)):
        s.cylinder(cx, cy, 0, r, h, TANK_W, top=P("grey", 1.5), segs=22)
        s.cylinder(cx, cy, h - 5, r + 0.005, 1.4, RED_BAND, segs=22)
        s.cylinder(cx, cy, 0, r + 0.01, 2, P("grey", -1), segs=22)
        s.cylinder(cx, cy, h, r * 0.92, 1.0, P("grey", 3, snow=True), segs=22, top=P("grey", 3))
        s.box(cx + r - 0.02, cy - 0.02, 0, 0.012, 0.04, h, P("grey", -1))
    s.cylinder(0.3, 1.1, 0, 0.16, 14, TANK_W, top=P("grey", 2), segs=16)
    s.dome(0.3, 1.1, 14, 0.16, 5, P("grey", 3))
    # refinery column with platforms + a pipe rack
    s.cylinder(1.6, 1.05, 0, 0.09, 52, M("metal", ramp="olive", shade=1.5), top=P("grey", 2), segs=14)
    s.dome(1.6, 1.05, 52, 0.09, 6, P("grey", 3))
    for z in (14, 28, 41):
        s.cylinder(1.6, 1.05, z, 0.125, 1.5, STEEL, segs=14)
    s.cylinder(1.82, 1.4, 0, 0.055, 36, M("metal", ramp="grey", shade=1.0), top=P("grey", 2), segs=12)
    beam(s, (1.62, 1.1, 36), (1.8, 1.38, 34), STEEL_Y, w=0.03, hpx=2)
    for k in range(4):
        pole(s, 0.75 + k * 0.17, 0.97, 7, P("grey", 0, snow=False))
    pipe(s, (0.7, 0.97, 7), (1.5, 0.97, 7), 0.03, STEEL_Y)
    pipe(s, (0.7, 1.07, 7), (1.5, 1.07, 7), 0.03, P("olive", 1))
    # control hut, pump, flare stack
    block(s, 0.12, 1.55, 0.4, 0.3, 9, M("siding", ramp="snow", shade=0.2), roof_mat="roof_flat")
    door(s, 0.25, 1.85, 0.1, 6, "+y", "door")
    s.box(0.6, 1.62, 0, 0.16, 0.12, 4, M("metal", ramp="olive"), top=P("grey", 2))
    flare(s, 1.86, 0.3, 44, st.frame, on=not st.inactive)
    vehicle(s, 1.0, 1.55, "x", "truck", "snow")
    fence(s, [(0.03, 0.03), (1.97, 0.03), (1.97, 1.97), (0.03, 1.97)], 4, closed=True)
    return s


# ---------------------------------------------------------------- fish
QUAY = M("stone", shade=0.8)
DECK = M("wood", shade=0.4)


def net_rack(s, x, y, L, h=9, axis="x"):
    """Drying rack: two posts with a hanging net (lattice of thin beams)."""
    post = P("wood", 0.2)
    net = P("sand", -0.5, snow=False)
    if axis == "x":
        pole(s, x, y, h + 1, post, w=0.025)
        pole(s, x + L, y, h + 1, post, w=0.025)
        beam(s, (x, y, h), (x + L, y, h), post, w=0.02, hpx=1.2)
        for k in range(1, 7):
            xx = x + L * k / 7
            beam(s, (xx, y, h), (xx + 0.01, y, h * 0.3), net, w=0.01, hpx=0.8)
        for z in (h * 0.75, h * 0.5):
            beam(s, (x, y, z), (x + L, y, z), net, w=0.01, hpx=0.8)
    else:
        pole(s, x, y, h + 1, post, w=0.025)
        pole(s, x, y + L, h + 1, post, w=0.025)
        beam(s, (x, y, h), (x, y + L, h), post, w=0.02, hpx=1.2)
        for k in range(1, 7):
            yy = y + L * k / 7
            beam(s, (x, yy, h), (x, yy + 0.01, h * 0.3), net, w=0.01, hpx=0.8)
        for z in (h * 0.75, h * 0.5):
            beam(s, (x, y, z), (x, y + L, z), net, w=0.01, hpx=0.8)


@model("fish-hub", (2, 2), "industry", "Fish hub", front=True, anim=4,
       note="Half the footprint is water (right side): quay, pier with crates, fish hall with ice sign, "
            "nets drying. No boats here (LIFE owns them). Water ripples over 4 frames.")
def fish(st):
    s = ik.Scene((2, 2), 56)
    rect(s, 0, 0, 1.05, 2, "sand", layer=0)
    rect(s, 0, 0, 1.05, 2, M("paving", shade=-0.5), layer=1)
    sea(s, 1.05, 0, 2, 2)
    # quay wall along x=1.05, down to the natural water level
    s.box(1.0, 0.0, WATER_Z, 0.06, 2.0, 4 - WATER_Z, QUAY, top=M("concrete_ground", shade=0.5))
    # pier out into the water with piles, bollards, lamp
    s.box(1.06, 0.88, 3, 0.8, 0.22, 1.2, DECK, top=M("wood", shade=1.2))
    for k in range(4):
        for yy in (0.9, 1.05):
            s.box(1.12 + k * 0.22, yy, WATER_Z, 0.03, 0.03, 3 - WATER_Z, P("wood", -2.5))
    for yy in (0.9, 1.07):
        s.box(1.84, yy, 4.2, 0.04, 0.03, 2.2, P("grey", -2))
    lamp(s, 1.12, 0.9, 14)
    crate_stack(s, 1.4, 0.92, 1, 2, 2, 0.05, ramps=("snow", "teal"))
    # fish hall
    hall = wall_mat("siding", ramp="water", shade=2.5, win_w=4, win_h=5, period=9, storey=11, sill=3,
                    glass="glass", lit=0.7)
    block(s, 0.1, 0.12, 0.8, 0.7, 12, hall, roof="gable", roof_h=7, roof_mat=M("roof_metal", ramp="slate", shade=0.5),
          axis="x", gable=hall)
    door(s, 0.2, 0.82, 0.3, 8, "+y", "garage")
    panel(s, 0.65, 0.82, 8, 0.22, 4, P("snow", 1.5, snow=False))
    s.box(0.58, 0.834, 8.8, 0.14, 0.006, 2.2, P("water", 3, snow=False))              # ICE sign stripe
    s.box(0.1, 0.78, 10.5, 0.8, 0.05, 1.2, P("teal", 1.5))
    # crates + ice chest + nets drying + barrels
    crate_stack(s, 0.15, 1.2, 2, 2, 2, 0.07, ramps=("snow", "teal", "water"))
    s.box(0.7, 1.15, 0, 0.18, 0.12, 5, P("snow", 1.0), top=P("snow", 2.5))
    net_rack(s, 0.15, 1.65, 0.55, 10, "x")
    net_rack(s, 0.82, 1.4, 0.45, 9, "y")
    for (x, y) in ((0.45, 1.3), (0.55, 1.35)):
        s.cylinder(x, y, 0, 0.04, 5, M("metal", ramp="water", shade=-0.3), top=P("grey", 2), segs=8)
    return s


# ---------------------------------------------------------------- oil derrick
@model("oil-derrick", (1, 1), "industry", "Oil derrick (pumpjack)", anim=4, power=False, build=False,
       note="Placed by the oil hub inside its area. Walking beam rocks over 4 frames.")
def derrick(st):
    s = ik.Scene((1, 1), 57)
    rect(s, 0, 0, 1, 1, M("gravel", shade=-0.5), layer=0)
    rect(s, 0.12, 0.12, 0.88, 0.88, M("concrete_ground", shade=-0.3), layer=1)
    # base skid + samson post (A-frame) + walking beam rocking about the pivot, horsehead at +x
    s.box(0.2, 0.4, 0, 0.6, 0.2, 2, P("grey", -1))
    px, py, pz = 0.5, 0.5, 20
    for (dy, x0, x1) in ((-0.07, 0.34, 0.5), (0.07, 0.34, 0.5), (-0.07, 0.66, 0.5), (0.07, 0.66, 0.5)):
        beam(s, (x0, py + dy, 2), (x1, py + dy * 0.2, pz), P("yellow", 0.2, snow=False), w=0.025, hpx=2)
    ang = (0.0, 0.2, 0.0, -0.2)[st.frame % 4]
    L1, L2 = 0.3, 0.26                       # horsehead side (+x) and tail side
    def pt(t):
        return (px + t * math.cos(ang), py, pz + t * math.sin(ang) * S)
    beam(s, pt(L1), pt(-L2), P("olive", 0.5, snow=False), w=0.05, hpx=3.2)
    hx, _, hz = pt(L1)
    s.box(hx - 0.03, py - 0.035, hz - 6, 0.06, 0.07, 9, M("metal", ramp="olive", shade=0.5))      # horsehead
    beam(s, (hx, py, hz - 6), (hx, py, 4), P("grey", 0, snow=False), w=0.012, hpx=1)            # bridle / polished rod
    tx, _, tz = pt(-L2)
    s.box(tx - 0.05, py - 0.05, tz - 6, 0.1, 0.1, 6, M("metal", ramp="red", shade=0.2))             # counterweight
    s.box(px - 0.03, py - 0.04, pz - 1, 0.06, 0.08, 3, STEEL)
    beam(s, (tx, py, tz - 6), (0.3, py, 4), P("grey", 1, snow=False), w=0.015, hpx=1)           # pitman arm
    s.box(0.25, 0.4, 2, 0.1, 0.2, 5, M("metal", ramp="yellow", shade=-0.5))                      # gearbox
    # wellhead, small tank
    s.box(0.78 - 0.015, 0.5 - 0.015, 0, 0.03, 0.03, 5, P("red", 0.5, snow=False))
    s.cylinder(0.78, 0.2, 0, 0.1, 8, M("metal_light", shade=1.5), top=P("grey", 2), segs=12)
    pipe(s, (0.78, 0.5, 2), (0.78, 0.22, 2), 0.015, P("olive", 1))
    return s
