"""CIVIC models: health (clinic, hospital, medevac helipad). White walls, teal trim, red crosses."""
from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, red_cross, canopy, wall_mat, letter_h, \
    disc, ring, pole, hcyl
from iso_civic_props import vehicle, bay_row, tree, person, lamp, bench, hedge, flowers
from iso_civic_states import model

WHITE = wall_mat("plaster", ramp="snow", shade=0.2, win_w=4, period=7, glass="glass", lit=0.75)
WHITE_PLAIN = M("plaster", ramp="snow", shade=0.2)
TEAL = P("teal", 2.5)
TEAL_ROOF = M("roof_flat", ramp="teal", shade=0.5)


def band(s, x, y, dx, dy, z, h=2, mat=TEAL):
    """Coloured trim band around a block (slightly proud)."""
    s.box(x - 0.01, y - 0.01, z, dx + 0.02, dy + 0.02, h, mat)


@model("clinic", (2, 2), "health", "Clinic", front=True,
       ups=(("depot", "ambulance depot"), ("wing", "beds wing")))
def clinic(st):
    s = ik.Scene((2, 2), 11)
    lot(s, "grass")
    rect(s, 0.05, 1.25, 1.95, 1.95, "paving", layer=1)
    rect(s, 1.1, 1.3, 1.9, 1.95, "asphalt", layer=2)
    # main block, 2 storeys
    block(s, 0.25, 0.3, 1.1, 0.95, 2 * ST + 2, WHITE, rim=WHITE_PLAIN)
    band(s, 0.25, 0.3, 1.1, 0.95, 2 * ST + 1, 2)
    door(s, 0.6, 1.25, 0.24, 8, "+y", "door_glass")
    canopy(s, 0.5, 1.25, 0.45, 0.22, 9, TEAL)
    red_cross(s, 1.12, 1.25, 11, 8, "+y")
    red_cross(s, 0.8, 0.75, 2 * ST + 2.2, 10, "top")
    s.box(0.4, 0.4, 2 * ST + 2, 0.2, 0.15, 4, "metal_light")
    bay_row(s, 1.15, 1.6, 3, 0.24, "x", "car", ("water", "snow", None))
    if "depot" in st.ups:
        block(s, 1.45, 0.25, 0.48, 0.65, 11, M("plaster", ramp="snow", shade=0.0), rim=WHITE_PLAIN)
        door(s, 1.5, 0.9, 0.17, 8, "+y", "garage")
        door(s, 1.72, 0.9, 0.17, 8, "+y", "garage")
        vehicle(s, 1.53, 0.93, "y", "ambulance", "snow", L=0.3)
    else:
        tree(s, 1.65, 0.55, 20, 0.14, season=st.season, seed=1)
        tree(s, 1.7, 1.0, 16, 0.11, season=st.season, seed=2)
    if "wing" in st.ups:
        block(s, 0.05, 0.3, 0.2, 0.95, ST + 2, WHITE, rim=WHITE_PLAIN)
        block(s, 0.25, 0.05, 1.1, 0.25, 2 * ST + 2, WHITE, rim=WHITE_PLAIN)
    vehicle(s, 0.12, 1.55, "x", "ambulance", "snow")
    lamp(s, 1.0, 1.9, 12)
    person(s, 0.95, 1.45, "teal")
    return s


@model("hospital", (3, 3), "health", "Hospital", front=True,
       ups=(("trauma", "trauma centre"), ("depot", "ambulance depot")))
def hospital(st):
    s = ik.Scene((3, 3), 12)
    lot(s, "grass")
    rect(s, 0.05, 2.15, 2.0, 2.95, "paving", layer=1)
    rect(s, 0.08, 2.3, 0.8, 2.95, "asphalt", layer=2)
    rect(s, 0.85, 2.15, 2.0, 2.95, "asphalt", layer=2)
    rect(s, 0.8, 2.15, 0.85, 2.95, "marking_yellow", layer=3)
    # tower block (6 storeys) behind a 2-storey front wing
    th = 6 * ST + 2
    block(s, 0.25, 0.25, 1.4, 1.1, th, WHITE, rim=WHITE_PLAIN, roof_mat=TEAL_ROOF)
    for k in (2, 4):
        band(s, 0.25, 0.25, 1.4, 1.1, k * ST + 1, 1.5)
    red_cross(s, 0.95, 1.35, th - 13, 11, "+y")
    # roof helipad
    s.cylinder(0.95, 0.8, th, 0.42, 2, P("grey", 1), top=M("concrete_ground", shade=-0.8), segs=20)
    _h_on(s, 0.95, 0.8, th + 2, 0.34)
    pole(s, 0.3, 0.3, 6, P("red", 0), z=th)
    # front wing
    wh = 2 * ST + 3
    block(s, 0.25, 1.35, 1.7, 0.8, wh, WHITE, rim=WHITE_PLAIN, roof_mat=TEAL_ROOF)
    band(s, 0.25, 1.35, 1.7, 0.8, wh - 2, 2)
    door(s, 1.0, 2.15, 0.35, 9, "+y", "door_glass")
    canopy(s, 0.85, 2.15, 0.7, 0.4, 11, TEAL)
    s.box(0.95, 2.53, 13, 0.5, 0.012, 4, P("snow", 1))           # sign board
    red_cross(s, 1.55, 2.15, 10, 8, "+y")
    vehicle(s, 0.95, 2.28, "x", "ambulance", "snow")
    vehicle(s, 1.55, 2.6, "x", "ambulance", "snow")
    # car park
    bay_row(s, 0.1, 2.62, 3, 0.22, "x", "car", ("terra", "slate", "snow"))
    lamp(s, 0.78, 2.4, 14)
    # right column: garden / upgrades
    if "depot" in st.ups:
        block(s, 2.1, 0.15, 0.8, 0.75, 12, M("plaster", ramp="snow"), rim=WHITE_PLAIN, roof_mat=TEAL_ROOF)
        for i in range(3):
            door(s, 2.15 + i * 0.25, 0.9, 0.2, 8, "+y", "garage")
        rect(s, 2.05, 0.9, 2.95, 1.35, "asphalt", layer=2)
        vehicle(s, 2.17, 0.95, "y", "ambulance", "snow", L=0.3)
        vehicle(s, 2.67, 0.95, "y", "ambulance", "snow", L=0.3)
    else:
        for (x, y, h) in ((2.3, 0.4, 22), (2.7, 0.75, 18), (2.35, 1.0, 20)):
            tree(s, x, y, h, 0.15, season=st.season, seed=int(x * 10))
    if "trauma" in st.ups:
        tz = 3 * ST + 2
        block(s, 2.1, 1.45, 0.8, 0.85, tz, WHITE, rim=WHITE_PLAIN, roof_mat=TEAL_ROOF)
        band(s, 2.1, 1.45, 0.8, 0.85, tz - 3, 2, P("red", 1))
        red_cross(s, 2.5, 2.3, tz - 12, 8, "+y")
        s.box(1.95, 1.65, ST + 2, 0.15, 0.3, 7, M("glass"))     # link bridge
    else:
        rect(s, 2.1, 1.5, 2.9, 2.3, "meadow", layer=1)
        tree(s, 2.55, 1.8, 18, 0.13, season=st.season, seed=4)
        bench(s, 2.2, 2.15, "x")
    for (x, y) in ((2.15, 2.6), (2.6, 2.75)):
        tree(s, x, y, 16, 0.12, season=st.season, seed=int(y * 10))
    person(s, 1.2, 2.45, "teal")
    person(s, 1.75, 2.35, "rose")
    return s


def _h_on(s, cx, cy, z, size):
    """H marking raised on a roof / pad surface at height z (as a thin top slab)."""
    a, t = size / 2, size * 0.16
    w = P("snow", 1, snow=False)
    s.box(cx - a, cy - a, z, t, size, 0.4, w)
    s.box(cx + a - t, cy - a, z, t, size, 0.4, w)
    s.box(cx - a, cy - t / 2, z, size, t, 0.4, w)


@model("medevac-helipad", (2, 2), "health", "Medevac helipad",
       note="Pad only: the helicopter is LIFE's.")
def medevac(st):
    s = ik.Scene((2, 2), 13)
    lot(s, "grass")
    rect(s, 0.05, 0.05, 1.95, 1.95, "concrete_ground", layer=1)
    s.cylinder(0.85, 0.85, 0, 0.72, 3, P("grey", 0.5), top=M("asphalt", shade=1), segs=24)
    s.cylinder(0.85, 0.85, 3, 0.62, 0.2, P("snow", 1, snow=False), top=P("snow", 1, snow=False), segs=24)
    s.cylinder(0.85, 0.85, 3.2, 0.56, 0.2, M("asphalt", shade=1), top=M("asphalt", shade=1), segs=24)
    red_cross(s, 0.85, 0.85, 3.6, 22, "top", bg=False)
    for k in range(8):
        import math
        a = k * math.pi / 4
        s.box(0.85 + 0.66 * math.cos(a) - 0.02, 0.85 + 0.66 * math.sin(a) - 0.02, 3, 0.04, 0.04, 1.2,
              M("lamp", ramp="leaf", eramp="leaf"))
    block(s, 1.55, 0.25, 0.38, 0.8, ST + 2, WHITE, rim=WHITE_PLAIN, roof_mat=TEAL_ROOF)
    red_cross(s, 1.93, 0.6, 3, 6, "+x")
    door(s, 1.6, 1.05, 0.15, 7, "+y", "door_glass")
    # windsock
    pole(s, 1.8, 1.75, 14, P("grey", 2))
    hcyl(s, 1.8, 1.75, 12, 0.03, 0.14, M("awning", ramp="terra", width=3), "x", 8, caps=False)
    vehicle(s, 1.45, 1.4, "y", "ambulance", "snow")
    return s
