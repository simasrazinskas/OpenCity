"""CIVIC models: comms (post office, sorting centre, telecom mast + tower; accent purple) and admin
(city hall, welfare office; accent sand/stone + gold). Shares a few small helpers with iso_civic_m_edu."""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, canopy, wall_mat, disc, ring, pole, \
    beam, fence, lathe, cone, hcyl
from iso_civic_props import vehicle, bay_row, tree, person, lamp, bench, hedge, flowers, flag, fountain, crate_stack
from iso_civic_states import model
from iso_civic_m_edu import sign, columns, clock_face, band, path, kids, bike_rack, ellipse

PURPLE = P("purple", 1.0)
PURPLE_D = P("purple", -0.5)
GOLD = P("yellow", 2.5, snow=False)
TRIM_W = P("snow", 0.5)
RED = P("red", 1.0, snow=False)
YELLOW = P("yellow", 1.5, snow=False)
BEAM_RED = ik.mat("lamp", ramp="red", eramp="red")

PO_WALL = wall_mat("plaster", ramp="sand", shade=2.5, win_w=5, win_h=6, period=9, sill=4, storey=ST,
                   glass="glass", lit=0.7)
PO_PLAIN = M("plaster", ramp="sand", shade=2.5)
SHED = wall_mat("siding", ramp="grey", shade=3.2, win_w=6, win_h=3, period=12, storey=14, sill=8, margin=3,
                glass="glass", lit=0.6)
SHED_PLAIN = M("metal_light", shade=0.8)
STONE_W = wall_mat("plaster", ramp="stone", shade=3.0, win_w=4, win_h=7, period=9, sill=3, storey=13,
                   glass="glass", lit=0.7)
STONE_PLAIN = M("plaster", ramp="stone", shade=3.0)
SAND_W = wall_mat("plaster", ramp="sand", shade=2.0, win_w=4, win_h=7, period=9, sill=3, storey=ST + 2,
                  glass="glass", lit=0.7)
SAND_PLAIN = M("plaster", ramp="sand", shade=2.0)
GOLD_DOME = M("roof_flat", ramp="yellow", shade=0.0)


def postbox(s, x, y):
    s.cylinder(x, y, 0, 0.032, 6, P("red", 0.5, snow=False), segs=10)
    s.dome(x, y, 6, 0.032, 2.5, P("red", 1.0, snow=False))
    s.box(x - 0.018, y + 0.028, 3.5, 0.036, 0.006, 1, P("grey", -6, snow=False))


# ---------------------------------------------------------------- post office
@model("postoffice", (2, 2), "comms", "Post office", front=True,
       ups=(("fleet", "van depot"),),
       note="Yellow/red post vans; the van depot upgrade adds a garage with a second row of vans.")
def postoffice(st):
    s = ik.Scene((2, 2), 41)
    lot(s, "grass")
    rect(s, 0.05, 0.95, 1.95, 1.95, "paving", layer=1)
    rect(s, 1.3, 1.0, 1.95, 1.95, "asphalt", layer=2)
    h = 2 * ST + 2
    block(s, 0.12, 0.2, 1.1, 0.75, h, PO_WALL, roof="hip", roof_mat=M("roof_tiles", ramp="terra", shade=-0.5),
          roof_h=9)
    band(s, 0.12, 0.2, 1.1, 0.75, ST, 1.2, TRIM_W)
    band(s, 0.12, 0.2, 1.1, 0.75, h - 2, 2.0, RED)
    # counter hall with glass doors, purple fascia and a big yellow/red signboard
    block(s, 0.22, 0.95, 0.9, 0.14, ST - 1, PO_WALL, roof="none", roof_mat="roof_flat")
    s.box(0.18, 0.92, ST - 1, 0.98, 0.22, 1.8, PURPLE)
    door(s, 0.3, 1.09, 0.28, 8, "+y", "door_glass")
    door(s, 0.7, 1.09, 0.34, 8, "+y", "shopfront")
    sign(s, 0.67, 0.95, h - 9, 0.7, 6, "red", "+y", shade=1.0)
    s.box(0.3, 0.949, h - 7, 0.14, 0.012, 3, P("yellow", 3, snow=False))
    s.box(0.95, 0.52, h + 3, 0.07, 0.07, 8, M("brick", shade=-1))
    # post boxes, bench, queue, bikes
    postbox(s, 1.2, 1.2)
    postbox(s, 1.27, 1.2)
    bench(s, 0.15, 1.2, "x", 0.14)
    person(s, 0.5, 1.28, "red")
    person(s, 0.58, 1.3, "yellow", "leaf")
    person(s, 0.9, 1.35, "teal")
    bike_rack(s, 0.15, 1.4, 3)
    lamp(s, 1.0, 1.55, 12)
    if "fleet" in st.ups:
        block(s, 1.38, 0.2, 0.55, 0.75, ST + 5, wall_mat("siding", ramp="yellow", shade=2.5, win_w=4, period=9,
                                                           storey=ST, glass="glass"), roof_mat="roof_metal",
              rim=M("plaster", ramp="yellow", shade=2.5))
        band(s, 1.38, 0.2, 0.55, 0.75, ST + 2, 1.8, RED)
        door(s, 1.43, 0.95, 0.2, 8, "+y", "garage")
        door(s, 1.69, 0.95, 0.2, 8, "+y", "garage")
        vehicle(s, 1.43, 1.05, "y", "van", "yellow")
        vehicle(s, 1.69, 1.05, "y", "van", "red")
        vehicle(s, 1.43, 1.5, "y", "van", "yellow")
        vehicle(s, 1.69, 1.5, "y", "van", "yellow")
    else:
        tree(s, 1.55, 0.45, 22, 0.15, season=st.season, seed=1)
        tree(s, 1.8, 0.7, 17, 0.12, season=st.season, seed=2)
        bench(s, 1.4, 0.85, "x")
        vehicle(s, 1.43, 1.1, "y", "van", "yellow")
        vehicle(s, 1.69, 1.5, "y", "van", "red")
    vehicle(s, 0.55, 1.65, "x", "car", "slate")
    tree(s, 0.2, 1.8, 18, 0.12, season=st.season, seed=3)
    return s


# ---------------------------------------------------------------- sorting centre
@model("sortingcenter", (3, 3), "comms", "Sorting centre",
       note="Big shed with loading docks and parcel trucks; purple signage.")
def sortingcenter(st):
    s = ik.Scene((3, 3), 42)
    kerb_lot(s, "concrete_ground", outer="grass")
    rect(s, 0.1, 1.45, 2.9, 2.9, "asphalt", layer=2)
    rect(s, 0.1, 2.15, 2.9, 2.17, "marking_yellow", layer=3)
    h = 30
    block(s, 0.2, 0.2, 2.6, 1.25, h, SHED, roof="gable", roof_mat=M("roof_metal", ramp="purple", shade=-0.5),
          roof_h=8, axis="x", gable=SHED_PLAIN)
    s.box(0.19, 0.19, 17, 2.62, 1.27, 3, PURPLE)
    s.box(0.19, 0.19, 24, 2.62, 1.27, 1, TRIM_W)
    # loading docks on the +Y face, each with a dock door, canopy and bumper
    for i in range(5):
        x = 0.4 + i * 0.46
        door(s, x, 1.45, 0.3, 14, "+y", "garage")
        s.box(x - 0.03, 1.45, 0, 0.36, 0.06, 2, P("grey", 1))
        s.box(x - 0.02, 1.445, 15, 0.34, 0.012, 1.5, P("purple", 2.5))
    s.box(0.25, 1.45, 15.5, 2.5, 0.08, 1.2, P("grey", 0))
    sign(s, 1.5, 1.45, 20.5, 1.1, 5, "purple", "+y", shade=2.0)
    s.box(0.5, 1.444, 22, 0.14, 0.012, 3, P("yellow", 3, snow=False))
    for i in range(4):
        s.box(0.6 + i * 0.6, 0.45, h + 3, 0.28, 0.2, 1.5, M("glass"))
    for i in range(3):
        s.cylinder(0.8 + i * 0.7, 0.3, h + 4, 0.05, 4, "metal_light", segs=8)
    # office annex (front right)
    block(s, 2.3, 1.55, 0.55, 0.5, 2 * ST, wall_mat("concrete", win_w=4, win_h=6, period=8, storey=ST,
                                                     glass="glass"),
          roof_mat=M("roof_flat", ramp="purple", shade=0), rim=PURPLE)
    s.box(2.29, 1.54, ST, 0.57, 0.52, 1.2, P("purple", 2.0))
    door(s, 2.45, 2.05, 0.16, 7, "+y", "door_glass")
    # trucks backed into three docks
    for i, (x, pt) in enumerate(((0.43, "snow"), (1.35, "yellow"), (1.81, "snow"))):
        vehicle(s, x, 1.5, "y", "truck", pt)
    # outbound row
    for i, (x, pt, k) in enumerate(((0.25, "terra", "truck"), (0.9, "snow", "truck"))):
        vehicle(s, x, 2.3, "x", k, pt)
    vehicle(s, 0.3, 2.6, "x", "van", "yellow")
    vehicle(s, 1.0, 2.6, "x", "van", "red")
    vehicle(s, 1.8, 2.6, "x", "van", "yellow")
    crate_stack(s, 2.2, 2.35, 1, 1, 2, 0.08, ramps=("wood", "terra"))
    person(s, 1.2, 2.1, "red")
    person(s, 2.1, 2.1, "yellow")
    for (x, y) in ((0.15, 2.85), (2.85, 2.15), (1.2, 2.2)):
        lamp(s, x, y, 16)
    tree(s, 2.7, 2.7, 16, 0.12, season=st.season, seed=7)
    return s


# ---------------------------------------------------------------- telecom mast
def _mast(s, cx, cy, h, w0, w1, section=24, mats=None):
    """Tapering 4-leg lattice mast centred at (cx, cy); sections coloured alternately."""
    mats = mats or (P("red", 1.0, snow=False), P("snow", 1.5, snow=False))
    n = max(2, int(h / section))
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        a0, a1 = w0 + (w1 - w0) * t0, w0 + (w1 - w0) * t1
        z0, z1 = h * t0, h * t1
        m = mats[i % len(mats)]
        pts0 = [(cx - a0, cy - a0), (cx + a0, cy - a0), (cx + a0, cy + a0), (cx - a0, cy + a0)]
        pts1 = [(cx - a1, cy - a1), (cx + a1, cy - a1), (cx + a1, cy + a1), (cx - a1, cy + a1)]
        for p0, p1 in zip(pts0, pts1):
            beam(s, (p0[0], p0[1], z0), (p1[0], p1[1], z1), m, w=0.03, hpx=2.0)
        for (p0, p1) in zip(pts1, pts1[1:] + pts1[:1]):
            beam(s, (p0[0], p0[1], z1), (p1[0], p1[1], z1), m, w=0.016, hpx=1.0)
        # diagonals on the two visible faces (+y and +x)
        beam(s, (cx - a0, cy + a0, z0), (cx + a1, cy + a1, z1), m, w=0.014, hpx=1.0)
        beam(s, (cx + a0, cy - a0, z0), (cx + a1, cy + a1, z1), m, w=0.014, hpx=1.0)
        beam(s, (cx + a0, cy + a0, z0), (cx - a1, cy + a1, z1), m, w=0.014, hpx=1.0)


def dish(s, x, y, z, r=0.07, axis="x", ramp="snow"):
    """Parabolic dish, drawn as a shallow disc on a short stub, facing +y or +x."""
    d = P(ramp, 1.5, snow=False)
    if axis == "y":
        hcyl(s, x, y, z, r, 0.03, d, "y", 12)
        s.box(x - 0.008, y - 0.04, z - 1, 0.016, 0.04, 2, P("grey", 1, snow=False))
    else:
        hcyl(s, x, y, z, r, 0.03, d, "x", 12)
        s.box(x - 0.04, y - 0.008, z - 1, 0.04, 0.016, 2, P("grey", 1, snow=False))


@model("telecom-mast", (1, 1), "comms", "Telecom mast",
       note="Red/white lattice mast with dishes and antenna panels; red beacon lit at night.")
def telecom_mast(st):
    s = ik.Scene((1, 1), 43)
    lot(s, "grass")
    rect(s, 0.05, 0.05, 0.95, 0.95, "gravel", layer=1)
    H = 120
    s.box(0.34, 0.34, 0, 0.32, 0.32, 2, M("concrete_ground"))
    _mast(s, 0.5, 0.5, H, 0.17, 0.035, section=20)
    # platforms with antennas / dishes
    s.box(0.4, 0.4, H - 38, 0.2, 0.2, 1.2, P("grey", 1, snow=False))
    s.box(0.4, 0.4, H - 70, 0.2, 0.2, 1.2, P("grey", 1, snow=False))
    for (x, y) in ((0.42, 0.62), (0.52, 0.62), (0.58, 0.54)):
        face = "+y" if y > 0.6 else "+x"
        if face == "+y":
            s.box(x, y, H - 38, 0.05, 0.015, 10, P("snow", 2, snow=False))
        else:
            s.box(x, y - 0.02, H - 38, 0.015, 0.05, 10, P("snow", 2, snow=False))
    dish(s, 0.46, 0.63, H - 62, 0.08, "y")
    dish(s, 0.64, 0.5, H - 52, 0.065, "x")
    dish(s, 0.62, 0.42, H - 80, 0.06, "x")
    s.box(0.49, 0.49, H, 0.02, 0.02, 12, P("grey", 2, snow=False))
    s.box(0.475, 0.475, H + 12, 0.05, 0.05, 2.5, BEAM_RED)
    # equipment hut + fence + cable
    block(s, 0.12, 0.7, 0.2, 0.18, 8, M("metal_light", shade=0.5), roof="none", roof_mat="roof_metal")
    door(s, 0.17, 0.88, 0.08, 6, "+y", "garage")
    fence(s, [(0.06, 0.06), (0.94, 0.06), (0.94, 0.94), (0.06, 0.94)], 4, closed=True)
    beam(s, (0.5, 0.5, 2), (0.22, 0.75, 2), P("grey", -2, snow=False), w=0.02, hpx=1)
    tree(s, 0.82, 0.8, 16, 0.1, season=st.season, seed=2)
    return s


# ---------------------------------------------------------------- telecom tower
@model("telecom-tower", (2, 2), "comms", "Telecom tower",
       note="Concrete broadcast tower with an observation pod and a lit spire.")
def telecom_tower(st):
    s = ik.Scene((2, 2), 44)
    lot(s, "grass")
    cx, cy = 1.0, 0.95
    ellipse(s, cx, cy + 0.2, 0.9, 0.78, "paving", layer=1)
    ellipse(s, cx, cy + 0.2, 0.86, 0.74, "grass", layer=2)
    conc = M("concrete", ramp="stone", shade=2.2, panel_u=60, panel_v=30)
    # base building (round) + shaft
    s.cylinder(cx, cy, 0, 0.5, 14, M("plaster", ramp="stone", shade=1.5), top=M("roof_flat", ramp="stone"), segs=24)
    s.cylinder(cx, cy, 8, 0.52, 2, PURPLE, segs=24)
    lathe(s, cx, cy, [(12, 0.2), (40, 0.14), (95, 0.095), (112, 0.085)], conc, segs=18)
    # pod: two discs, glazed band, roof cone
    lathe(s, cx, cy, [(104, 0.1), (110, 0.28), (112, 0.3), (120, 0.3), (122, 0.28), (128, 0.1)],
          M("concrete", ramp="stone", shade=2.8, panel_u=60, panel_v=30), segs=20)
    s.cylinder(cx, cy, 112, 0.305, 8, M("glass_dark"), segs=20)
    s.cylinder(cx, cy, 120, 0.31, 1.5, PURPLE, segs=20)
    s.cylinder(cx, cy, 128, 0.1, 6, conc, segs=14)
    # spire with antenna and beacon
    s.cylinder(cx, cy, 134, 0.03, 36, P("snow", 1.5, snow=False), segs=8)
    s.cylinder(cx, cy, 160, 0.035, 5, RED, segs=8)
    s.box(cx - 0.02, cy - 0.02, 170, 0.04, 0.04, 3, BEAM_RED)
    # entrance + canopy
    door(s, 0.9, 1.45, 0.2, 8, "+y", "door_glass")
    canopy(s, 0.82, 1.43, 0.36, 0.16, 10, PURPLE, posts=True)
    # dishes on the base roof, cars, fence, trees
    for (x, y) in ((1.45, 0.5), (1.55, 0.85)):
        dish(s, x, y, 13, 0.07, "x")
    vehicle(s, 0.2, 1.6, "x", "van", "snow")
    vehicle(s, 1.45, 1.6, "x", "car", "slate")
    bench(s, 1.4, 1.35, "x")
    person(s, 0.85, 1.65, "yellow")
    for (x, y, hh) in ((0.25, 0.3, 22), (0.5, 0.2, 18), (1.75, 1.25, 20)):
        tree(s, x, y, hh, 0.13, season=st.season, seed=int(x * 10))
    lamp(s, 1.2, 1.7, 13)
    lamp(s, 0.7, 1.7, 13)
    fence(s, [(0.05, 1.82), (1.95, 1.82)], 4)
    return s


# ---------------------------------------------------------------- city hall
@model("cityhall", (3, 3), "admin", "City hall", front=True, anim=4,
       note="Classical hall: portico, pediment, gold dome, flag (4 frames), fountain forecourt.")
def cityhall(st):
    s = ik.Scene((3, 3), 51)
    lot(s, "grass")
    # forecourt: paved plaza with axis path and fountain
    rect(s, 0.45, 1.55, 2.55, 2.95, "paving", layer=1)
    rect(s, 1.35, 2.95 - 0.0, 1.65, 3.0, "paving", layer=1)
    disc(s, 1.5, 2.2, 0.36, M("paving", shade=-0.5), layer=2)
    h = 3 * ST + 4
    # side wings
    for x in (0.2, 2.3):
        block(s, x, 0.3, 0.5, 1.1, 2 * ST + 6, SAND_W, roof_mat="roof_flat", rim=SAND_PLAIN)
        band(s, x, 0.3, 0.5, 1.1, ST + 2, 1.2, TRIM_W)
        band(s, x, 0.3, 0.5, 1.1, 2 * ST + 4, 1.8, GOLD)
    # main hall
    block(s, 0.7, 0.3, 1.6, 0.95, h, SAND_W, roof_mat="roof_flat", rim=SAND_PLAIN)
    band(s, 0.7, 0.3, 1.6, 0.95, ST + 2, 1.2, TRIM_W)
    band(s, 0.7, 0.3, 1.6, 0.95, h - 2, 2.2, GOLD)
    # steps, colonnade, entablature, pediment
    s.box(0.85, 1.25, 0, 1.3, 0.3, 2, STONE_PLAIN)
    s.box(0.9, 1.25, 2, 1.2, 0.26, 1.5, STONE_PLAIN)
    columns(s, 0.95, 2.05, 1.3, 6, 3 * ST + 2, z=3.5, w=0.055)
    s.box(0.88, 1.24, 3 * ST + 5.5, 1.24, 0.3, 2.2, STONE_PLAIN)
    s.roof_gable(0.86, 1.22, 3 * ST + 7.5, 1.28, 0.34, 10, STONE_PLAIN, axis="x", overhang=0.03, gable=STONE_PLAIN)
    s.box(1.35, 1.52, 3 * ST + 9, 0.3, 0.012, 4, GOLD)
    door(s, 1.38, 1.25, 0.24, 14, "+y", "door_glass", z=3.5)
    # dome on a drum with a gold lantern + flag
    cz = h
    s.cylinder(1.5, 0.7, cz, 0.3, 9, SAND_PLAIN, segs=20)
    for k in range(8):
        a = 2 * math.pi * k / 8
        s.box(1.5 + 0.3 * math.cos(a) - 0.015, 0.7 + 0.3 * math.sin(a) - 0.015, cz + 2, 0.03, 0.03, 5,
              M("glass_dark"))
    s.cylinder(1.5, 0.7, cz + 9, 0.32, 1.5, TRIM_W, segs=20)
    s.dome(1.5, 0.7, cz + 10.5, 0.3, 22, GOLD_DOME)
    s.cylinder(1.5, 0.7, cz + 32, 0.05, 6, GOLD, segs=8)
    flag(s, 1.5, 0.7, cz + 52, "red", st.frame, w=0.16, fh=7, ramp2="yellow")
    # fountain, lawns, hedges, flagpoles, lamps, people
    fountain(s, 1.5, 2.2, 0.2, st.frame)
    for (x, y, w_, d) in ((0.2, 1.55, 0.25, 1.3), (2.55, 1.55, 0.25, 1.3)):
        rect(s, x, y, x + w_, y + d, M("meadow", shade=0.5), layer=1)
    hedge(s, 0.2, 1.5, 0.6, 0.06, 4, st.season)
    hedge(s, 2.2, 1.5, 0.6, 0.06, 4, st.season)
    for (x, y) in ((0.55, 1.75), (0.55, 2.35), (2.45, 1.75), (2.45, 2.35)):
        tree(s, x, y, 18, 0.1, "poplar", season=st.season, seed=int(y * 10))
    for x in (1.2, 1.8):
        pole(s, x, 1.8, 30, P("grey", 3, snow=False), w=0.016)
        s.box(x - 0.01, 1.79, 24, 0.14, 0.01, 5, P("red" if x < 1.5 else "yellow", 1.0, snow=False))
    for (x, y) in ((0.85, 2.0), (2.15, 2.0), (0.85, 2.7), (2.15, 2.7)):
        lamp(s, x, y, 14)
    bench(s, 0.7, 2.2, "y")
    bench(s, 2.25, 2.2, "y")
    kids(s, [(1.2, 2.55), (1.8, 2.5), (1.45, 1.85), (1.65, 2.8), (1.25, 1.65)])
    vehicle(s, 0.3, 2.75, "x", "car", "slate")
    vehicle(s, 2.4, 2.75, "x", "car", "snow")
    return s


# ---------------------------------------------------------------- welfare office
@model("welfare", (2, 2), "admin", "Welfare office", front=True,
       note="Civic office with a deep canopy, benches and a queue.")
def welfare(st):
    s = ik.Scene((2, 2), 52)
    lot(s, "grass")
    rect(s, 0.05, 0.9, 1.95, 1.95, "paving", layer=1)
    rect(s, 1.3, 1.5, 1.95, 1.95, "asphalt", layer=2)
    h = 2 * ST + 3
    block(s, 0.2, 0.2, 1.4, 0.7, h, SAND_W, roof_mat="roof_flat", rim=SAND_PLAIN)
    band(s, 0.2, 0.2, 1.4, 0.7, ST + 2, 1.2, TRIM_W)
    band(s, 0.2, 0.2, 1.4, 0.7, h - 2, 2.0, GOLD)
    # glazed entrance hall under a deep canopy on columns
    s.box(0.35, 0.88, 0, 1.1, 0.04, ST - 1, M("glass"))
    door(s, 0.8, 0.9, 0.3, 9, "+y", "door_glass")
    canopy(s, 0.25, 0.9, 1.3, 0.38, 11.5, GOLD, posts=False, t=1.8)
    for x in (0.28, 0.75, 1.17, 1.5):
        s.box(x, 1.24, 0, 0.035, 0.035, 11.5, STONE_PLAIN)
    s.box(0.55, 0.9, h - 12, 0.7, 0.012, 5, P("yellow", 2.0, snow=False))
    s.box(0.58, 0.9 + 0.012, h - 10.5, 0.64, 0.006, 2, P("snow", 1.5, snow=False))
    # roof plant
    s.box(1.1, 0.35, h, 0.25, 0.2, 5, "metal_light")
    # benches + queue under the canopy and in the forecourt
    bench(s, 0.38, 1.1, "x", 0.2)
    bench(s, 1.1, 1.1, "x", 0.2)
    kids(s, [(0.5, 1.0), (0.58, 1.02), (0.66, 1.0), (1.22, 1.05)])
    person(s, 0.75, 1.5, "slate", "slate")
    # notice board, planter, bikes
    s.box(0.12, 1.3, 0, 0.02, 0.2, 7, P("grey", 1, snow=False))
    s.box(0.1, 1.3, 7, 0.06, 0.2, 5, P("snow", 1.2, snow=False))
    flowers(s, 0.2, 1.55, 0.6, 1.75, "rose")
    lamp(s, 1.0, 1.6, 12)
    # lawn, trees, parked cars
    tree(s, 1.75, 0.45, 22, 0.15, season=st.season, seed=1)
    tree(s, 1.8, 0.9, 17, 0.12, season=st.season, seed=2)
    bench(s, 1.7, 1.15, "y", 0.12)
    vehicle(s, 1.4, 1.6, "x", "car", "terra")
    tree(s, 0.2, 1.85, 18, 0.12, season=st.season, seed=3)
    return s
