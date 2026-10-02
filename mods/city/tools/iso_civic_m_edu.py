"""CIVIC models: education (school, highschool, college, university). Accent: terra/orange + red brick.

Also holds a few helpers shared with iso_civic_m_comms (columns, signboards, clock face, play equipment).
"""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, block, door, canopy, wall_mat, disc, ring, pole, beam, \
    fence, lathe, cone
from iso_civic_props import vehicle, bay_row, tree, person, lamp, bench, hedge, flowers, flag, fountain
from iso_civic_states import model

BRICK = wall_mat("brick", win_w=4, win_h=6, period=8, sill=4, storey=ST, glass="glass", lit=0.7)
BRICK_PLAIN = M("brick")
CREAM = wall_mat("plaster", ramp="sand", shade=2.5, win_w=4, win_h=6, period=8, sill=4, storey=ST, glass="glass",
                 lit=0.7)
CREAM_PLAIN = M("plaster", ramp="sand", shade=2.5)
STONE_W = wall_mat("plaster", ramp="stone", shade=2.8, win_w=4, win_h=7, period=8, sill=3, storey=12,
                   glass="glass", lit=0.7)
STONE_PLAIN = M("plaster", ramp="stone", shade=2.8)
ORANGE = P("terra", 2.0)
TRIM_W = P("snow", 0.5)
ROOF = "roof_tiles"

WHITE_LINE = P("snow", 1.5, snow=False)
COURT = P("water", -0.5, snow=False)
TRACK = P("terra", 1.0, snow=False)


# ---------------------------------------------------------------- shared helpers
def sign(s, x, y, z, w, h, ramp="terra", face="+y", shade=1.5):
    """Coloured signboard with a lighter 'text' bar, centred at x/y on the facade."""
    panel_m = P(ramp, shade, snow=False)
    bar = P("snow", 1.5, snow=False)
    if face == "+y":
        s.box(x - w / 2, y, z, w, 0.014, h, panel_m)
        s.box(x - w / 2 + w * 0.12, y + 0.012, z + h * 0.35, w * 0.76, 0.006, max(1, h * 0.3), bar)
    else:
        s.box(x, y - w / 2, z, 0.014, w, h, panel_m)
        s.box(x + 0.012, y - w / 2 + w * 0.12, z + h * 0.35, 0.006, w * 0.76, max(1, h * 0.3), bar)


def columns(s, x0, x1, y, n, h, mat=None, w=0.045, z=0.0):
    """A row of n thin square columns along x at facade y (front colonnade)."""
    m = mat or P("snow", 0.0)
    for i in range(n):
        x = x0 + (x1 - x0 - w) * i / max(n - 1, 1)
        s.box(x, y, z, w, w, h, m)
        s.box(x - 0.008, y - 0.008, z + h - 1.5, w + 0.016, w + 0.016, 1.5, m)
        s.box(x - 0.008, y - 0.008, z, w + 0.016, w + 0.016, 1.5, m)


def clock_face(s, x, y, z, r=0.045, face="+y"):
    """Clock: white dial with two dark hands. r in cells (half width)."""
    d = r * 2
    dial = P("snow", 1.5, snow=False)
    hand = P("grey", -6, snow=False)
    if face == "+y":
        s.box(x - r, y, z, d, 0.012, d * ik.S, dial)
        s.box(x - 0.006, y + 0.012, z + r * ik.S, 0.012, 0.004, r * ik.S * 0.85, hand)
        s.box(x - 0.006, y + 0.012, z + r * ik.S - 0.5, r * 0.8, 0.004, 1, hand)
    else:
        s.box(x, y - r, z, 0.012, d, d * ik.S, dial)
        s.box(x + 0.012, y - 0.006, z + r * ik.S, 0.004, 0.012, r * ik.S * 0.85, hand)
        s.box(x + 0.012, y - 0.006, z + r * ik.S - 0.5, 0.004, r * 0.8, 1, hand)


def slide(s, x, y):
    """Playground slide: ladder tower + sloping chute along +x."""
    red = P("red", 1.0, snow=False)
    yel = P("yellow", 1.5, snow=False)
    s.box(x, y, 0, 0.04, 0.05, 8, red)
    s.box(x, y, 8, 0.05, 0.05, 1, yel)
    s.poly([(x + 0.04, y, 8), (x + 0.04, y + 0.045, 8), (x + 0.2, y + 0.045, 0.5), (x + 0.2, y, 0.5)], yel,
           cull=False, outward=(0, 0, 1))
    for k in range(3):
        s.box(x - 0.012, y + 0.005, 1.5 + k * 2.4, 0.012, 0.04, 0.6, P("grey", 3, snow=False))


def swings(s, x, y, w=0.3, h=9):
    """Swing set along x: two A-frames, top bar, two seats on chains."""
    fr = P("grey", 1, snow=False)
    for px in (x, x + w):
        pole(s, px, y - 0.03, h, fr, w=0.016)
        pole(s, px, y + 0.03, h, fr, w=0.016)
    s.box(x - 0.01, y - 0.008, h - 1, w + 0.02, 0.016, 1.2, fr)
    for sx in (x + w * 0.3, x + w * 0.7):
        pole(s, sx, y, h - 1, P("grey", 4, snow=False), w=0.006, z=2.5)
        s.box(sx - 0.025, y - 0.012, 2, 0.05, 0.025, 1, P("terra", 0.5, snow=False))


def hoop(s, x, y):
    """Basketball post with backboard + ring."""
    pole(s, x, y, 12, P("grey", 1, snow=False), w=0.016)
    s.box(x - 0.04, y - 0.005, 10, 0.08, 0.01, 4, P("snow", 1, snow=False))
    s.box(x - 0.015, y + 0.005, 10, 0.03, 0.03, 0.8, P("red", 1, snow=False))


def goal(s, x, y, axis="y", w=0.16):
    """Football goal posts (open frame)."""
    wm = P("snow", 1.5, snow=False)
    if axis == "y":
        pole(s, x, y, 8, wm, w=0.012)
        pole(s, x, y + w, 8, wm, w=0.012)
        s.box(x - 0.006, y, 7, 0.012, w, 1, wm)
    else:
        pole(s, x, y, 8, wm, w=0.012)
        pole(s, x + w, y, 8, wm, w=0.012)
        s.box(x, y - 0.006, 7, w, 0.012, 1, wm)


def bike_rack(s, x, y, n=4):
    for i in range(n):
        s.box(x + i * 0.04, y, 0, 0.012, 0.09, 3, P(("red", "teal", "yellow", "water")[i % 4], 1, snow=False))


def kids(s, pts):
    cols = (("red", "slate"), ("yellow", "water"), ("teal", "slate"), ("rose", "leaf"))
    for i, (x, y) in enumerate(pts):
        person(s, x, y, *cols[i % 4])


def pitch(s, x0, y0, x1, y1, lines=True):
    g = M("grass", shade=1.0)
    rect(s, x0, y0, x1, y1, g, layer=2)
    if lines:
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        t, m = 0.012, 0.03
        for (a, b, c, d) in ((x0 + m, y0 + m, x1 - m, y0 + m + t), (x0 + m, y1 - m - t, x1 - m, y1 - m),
                             (x0 + m, y0 + m, x0 + m + t, y1 - m), (x1 - m - t, y0 + m, x1 - m, y1 - m),
                             (cx - t / 2, y0 + m, cx + t / 2, y1 - m)):
            rect(s, a, b, c, d, WHITE_LINE, layer=3)
        r = min(x1 - x0, y1 - y0) * 0.16
        disc(s, cx, cy, r, WHITE_LINE, layer=3, segs=16)
        disc(s, cx, cy, r - 0.012, g, layer=4, segs=16)


def path(s, x0, y0, x1, y1, layer=2):
    rect(s, x0, y0, x1, y1, "paving", layer=layer)


# ---------------------------------------------------------------- school
@model("school", (2, 2), "education", "School", front=True,
       ups=(("wing", "seats wing"), ("library", "library")))
def school(st):
    s = ik.Scene((2, 2), 31)
    lot(s, "grass")
    path(s, 0.55, 0.85, 0.75, 2.0)
    # main block (2 storeys, brick, tiled gable roof)
    h = 2 * ST + 1
    block(s, 0.15, 0.2, 1.2, 0.6, h, BRICK, roof="gable", roof_mat=ROOF, roof_h=9, axis="x", gable=BRICK_PLAIN)
    s.box(0.14, 0.19, 9, 1.22, 0.62, 1.5, TRIM_W)
    s.box(0.14, 0.19, h - 1, 1.22, 0.62, 1.5, ORANGE)
    # entrance bay: taller front gable with door, sign, clock
    block(s, 0.45, 0.45, 0.4, 0.5, h + 1, BRICK, roof="gable", roof_mat=ROOF, roof_h=11, axis="y",
          gable=BRICK_PLAIN)
    s.box(0.44, 0.44, h, 0.42, 0.52, 1.5, ORANGE)
    door(s, 0.55, 0.95, 0.2, 9, "+y", "door_glass")
    canopy(s, 0.5, 0.95, 0.3, 0.14, 11, ORANGE, posts=False, t=1.2)
    for px in (0.52, 0.78):
        s.box(px, 1.05, 0, 0.025, 0.025, 11, TRIM_W)
    sign(s, 0.65, 0.95, 12.5, 0.26, 4, "terra", "+y")
    clock_face(s, 0.65, 0.95, h + 2.5, 0.04, "+y")
    # bell cupola on the main ridge
    s.box(0.97, 0.43, h + 8, 0.12, 0.12, 5, TRIM_W)
    s.roof_hip(0.96, 0.42, h + 13, 0.14, 0.14, 5, M("roof_tiles"))
    s.box(1.18, 0.3, h + 3, 0.06, 0.06, 9, M("brick", shade=-1))
    # upgrades / lawn
    if "wing" in st.ups:
        block(s, 1.4, 0.2, 0.52, 0.7, 2 * ST + 1, BRICK, roof="gable", roof_mat=ROOF, roof_h=8, axis="y",
              gable=BRICK_PLAIN)
        s.box(1.39, 0.19, 9, 0.54, 0.72, 1.5, TRIM_W)
        s.box(1.39, 0.19, 2 * ST, 0.54, 0.72, 1.5, ORANGE)
        door(s, 1.5, 0.9, 0.14, 8, "+y", "door_glass")
    else:
        tree(s, 1.55, 0.45, 22, 0.15, season=st.season, seed=1)
        tree(s, 1.8, 0.75, 17, 0.12, season=st.season, seed=2)
        bench(s, 1.4, 0.85, "x")
    if "library" in st.ups:
        block(s, 0.08, 1.2, 0.42, 0.55, ST + 3, CREAM, roof="gable", roof_mat=ROOF, roof_h=7, axis="y",
              gable=CREAM_PLAIN)
        s.box(0.07, 1.19, ST + 1, 0.44, 0.57, 1.5, ORANGE)
        door(s, 0.2, 1.75, 0.16, 7, "+y", "door_glass")
        sign(s, 0.28, 1.75, 8.5, 0.2, 3, "water", "+y")
    else:
        tree(s, 0.25, 1.4, 24, 0.15, season=st.season, seed=3)
        hedge(s, 0.08, 1.8, 0.4, 0.05, 3, st.season)
        flowers(s, 0.15, 1.12, 0.45, 1.25, "rose")
    # playground: slide + swings
    rect(s, 0.82, 1.15, 1.2, 1.95, "sand", layer=2)
    slide(s, 0.9, 1.25)
    swings(s, 0.9, 1.7, 0.26)
    # painted court + hoops
    rect(s, 1.25, 1.1, 1.92, 1.92, "asphalt", layer=2)
    rect(s, 1.3, 1.15, 1.87, 1.87, COURT, layer=3)
    rect(s, 1.3, 1.5, 1.87, 1.52, WHITE_LINE, layer=4)
    disc(s, 1.585, 1.51, 0.1, WHITE_LINE, layer=4, segs=14)
    disc(s, 1.585, 1.51, 0.085, COURT, layer=5, segs=14)
    hoop(s, 1.58, 1.14)
    hoop(s, 1.58, 1.88)
    kids(s, [(1.5, 1.4), (1.7, 1.6), (1.05, 1.5), (0.95, 1.9), (0.7, 1.3)])
    bike_rack(s, 0.2, 0.9, 4)
    lamp(s, 0.78, 1.0, 13)
    tree(s, 1.0, 1.05, 18, 0.1, season=st.season, seed=4)
    return s


def ellipse(s, cx, cy, rx, ry, mat, layer=2, segs=28, z=0.0):
    return s.ground([(cx + rx * math.cos(2 * math.pi * k / segs), cy + ry * math.sin(2 * math.pi * k / segs))
                     for k in range(segs)], mat, z=z, layer=layer)


def band(s, x, y, dx, dy, z, h=1.5, mat=ORANGE):
    s.box(x - 0.01, y - 0.01, z, dx + 0.02, dy + 0.02, h, mat)


# ---------------------------------------------------------------- high school
@model("highschool", (3, 3), "education", "High school", front=True,
       ups=(("wing", "seats wing"), ("library", "library")))
def highschool(st):
    s = ik.Scene((3, 3), 32)
    lot(s, "grass")
    path(s, 1.0, 1.15, 1.2, 3.0)
    h = 3 * ST + 2
    # main block: 3 storeys, flat roof, orange bands
    block(s, 0.2, 0.2, 1.85, 0.75, h, BRICK, roof_mat="roof_gravel", rim=BRICK_PLAIN)
    for z in (ST, 2 * ST):
        band(s, 0.2, 0.2, 1.85, 0.75, z, 1.2, TRIM_W)
    band(s, 0.2, 0.2, 1.85, 0.75, h - 1, 1.8)
    # entrance tower with clock
    th = 4 * ST + 4
    block(s, 0.8, 0.8, 0.5, 0.4, th, BRICK, roof="hip", roof_mat=ROOF, roof_h=9)
    band(s, 0.8, 0.8, 0.5, 0.4, th - 2, 1.8)
    band(s, 0.8, 0.8, 0.5, 0.4, 12, 1.5, TRIM_W)
    door(s, 0.92, 1.2, 0.26, 9, "+y", "door_glass")
    canopy(s, 0.88, 1.2, 0.34, 0.14, 11, ORANGE, posts=False, t=1.2)
    for px in (0.9, 1.2):
        s.box(px, 1.3, 0, 0.025, 0.025, 11, TRIM_W)
    sign(s, 1.05, 1.2, 13.5, 0.34, 4, "terra")
    clock_face(s, 1.05, 1.2, th - 12, 0.06, "+y")
    clock_face(s, 1.3, 1.0, th - 12, 0.06, "+x")
    s.box(1.55, 0.35, h, 0.2, 0.18, 5, "metal_light")
    # wing / lawn
    if "wing" in st.ups:
        wz = 3 * ST + 2
        block(s, 2.2, 0.2, 0.7, 1.1, wz, BRICK, roof_mat="roof_gravel", rim=BRICK_PLAIN)
        for z in (ST, 2 * ST):
            band(s, 2.2, 0.2, 0.7, 1.1, z, 1.2, TRIM_W)
        band(s, 2.2, 0.2, 0.7, 1.1, wz - 1, 1.8)
        door(s, 2.4, 1.3, 0.2, 8, "+y", "door_glass")
    else:
        for (x, y, hh) in ((2.35, 0.45, 24), (2.7, 0.7, 20), (2.4, 1.0, 22)):
            tree(s, x, y, hh, 0.15, season=st.season, seed=int(x * 10))
        bench(s, 2.2, 1.2, "x")
    if "library" in st.ups:
        block(s, 0.12, 1.2, 0.7, 0.55, 2 * ST + 2, CREAM, roof="gable", roof_mat=ROOF, roof_h=8, axis="y",
              gable=CREAM_PLAIN)
        band(s, 0.12, 1.2, 0.7, 0.55, 2 * ST, 1.5)
        door(s, 0.35, 1.75, 0.2, 8, "+y", "door_glass")
        sign(s, 0.45, 1.75, 10.5, 0.3, 3, "water")
    else:
        tree(s, 0.4, 1.35, 24, 0.16, season=st.season, seed=3)
        flowers(s, 0.15, 1.5, 0.8, 1.7, "rose")
    # running track + football pitch (front right)
    ellipse(s, 2.15, 2.2, 0.85, 0.7, "gravel", layer=1)
    ellipse(s, 2.15, 2.2, 0.82, 0.67, TRACK, layer=2)
    ellipse(s, 2.15, 2.2, 0.58, 0.45, M("grass", shade=1.0), layer=3)
    rect(s, 2.14, 1.8, 2.16, 2.6, WHITE_LINE, layer=4)
    goal(s, 1.62, 2.12, "y", 0.16)
    goal(s, 2.64, 2.12, "y", 0.16)
    kids(s, [(1.8, 1.6), (2.3, 2.35), (2.55, 1.65), (1.5, 2.0)])
    # car park + school bus + bike racks
    rect(s, 0.1, 1.9, 1.35, 2.95, "asphalt", layer=1)
    bay_row(s, 0.15, 2.05, 4, 0.2, "x", "car", ("terra", "slate", "snow", "water"), depth=0.3)
    bay_row(s, 0.15, 2.65, 4, 0.2, "x", "car", ("snow", None, "red", "slate"), depth=0.3)
    vehicle(s, 0.3, 2.35, "x", "bus", "yellow")
    bike_rack(s, 1.15, 1.35, 5)
    for (x, y) in ((1.4, 1.5), (0.95, 2.95)):
        lamp(s, x, y, 14)
    tree(s, 1.5, 2.9, 18, 0.12, season=st.season, seed=6)
    kids(s, [(1.1, 1.6), (0.9, 1.55)])
    return s


# ---------------------------------------------------------------- college
@model("college", (3, 3), "education", "College",
       ups=(("wing", "seats wing"), ("library", "library")),
       note="Campus blocks around a quad; the gate faces the viewer.")
def college(st):
    s = ik.Scene((3, 3), 33)
    lot(s, "grass")
    # quad: lawn with crossing paths
    rect(s, 0.85, 0.85, 2.15, 2.15, M("grass", shade=1.0), layer=1)
    path(s, 1.4, 0.8, 1.6, 3.0)
    path(s, 0.8, 1.4, 2.2, 1.6, layer=2)
    disc(s, 1.5, 1.5, 0.2, "paving", layer=3)
    fountain(s, 1.5, 1.5, 0.12, st.frame)
    h = 3 * ST + 2
    # back block with columned centre
    block(s, 0.2, 0.2, 2.6, 0.6, h, CREAM, roof_mat="roof_flat", rim=CREAM_PLAIN)
    for z in (ST + 1, 2 * ST + 1):
        band(s, 0.2, 0.2, 2.6, 0.6, z, 1.2, TRIM_W)
    band(s, 0.2, 0.2, 2.6, 0.6, h - 1, 1.8)
    block(s, 1.1, 0.7, 0.8, 0.2, h + 4, CREAM, roof="gable", roof_mat=ROOF, roof_h=8, axis="x",
          gable=CREAM_PLAIN)
    columns(s, 1.15, 1.85, 0.92, 4, h + 2)
    door(s, 1.38, 0.9, 0.24, 9, "+y", "door_glass")
    # cupola
    s.box(1.35, 0.4, h, 0.3, 0.3, 8, CREAM_PLAIN)
    s.roof_hip(1.32, 0.37, h + 8, 0.36, 0.36, 8, M("roof_flat", ramp="leaf", shade=0))
    # side blocks (brick)
    for (x, y, dx, dy) in ((0.2, 0.8, 0.6, 1.4), (2.2, 0.8, 0.6, 1.4)):
        block(s, x, y, dx, dy, 2 * ST + 2, BRICK, roof="gable", roof_mat=ROOF, roof_h=7, axis="y",
              gable=BRICK_PLAIN)
        band(s, x, y, dx, dy, ST, 1.2, TRIM_W)
        band(s, x, y, dx, dy, 2 * ST, 1.5)
    door(s, 0.4, 2.2, 0.16, 8, "+y", "door_glass")
    door(s, 2.45, 2.2, 0.16, 8, "+y", "door_glass")
    # front: wing / library upgrades or lawn
    if "wing" in st.ups:
        block(s, 0.2, 2.3, 0.6, 0.65, 2 * ST + 2, BRICK, roof="gable", roof_mat=ROOF, roof_h=7, axis="y",
              gable=BRICK_PLAIN)
        band(s, 0.2, 2.3, 0.6, 0.65, ST, 1.2, TRIM_W)
        band(s, 0.2, 2.3, 0.6, 0.65, 2 * ST, 1.5)
        door(s, 0.4, 2.95, 0.16, 8, "+y", "door_glass")
    else:
        tree(s, 0.4, 2.5, 22, 0.16, season=st.season, seed=1)
        tree(s, 0.6, 2.8, 18, 0.12, season=st.season, seed=2)
        bench(s, 0.25, 2.3, "y")
    if "library" in st.ups:
        s.cylinder(2.5, 2.62, 0, 0.3, ST * 2, M("plaster", ramp="sand", shade=2.8), segs=20)
        s.cylinder(2.5, 2.62, ST * 2, 0.32, 1.5, ORANGE, segs=20)
        s.dome(2.5, 2.62, ST * 2 + 1.5, 0.3, 11, M("roof_flat", ramp="leaf", shade=0))
        door(s, 2.4, 2.9, 0.2, 8, "+y", "door_glass")
    else:
        tree(s, 2.5, 2.55, 22, 0.16, season=st.season, seed=3)
        tree(s, 2.7, 2.85, 17, 0.12, season=st.season, seed=4)
        bench(s, 2.2, 2.4, "x")
    for x in (1.05, 1.9):
        s.box(x, 2.8, 0, 0.07, 0.07, 14, CREAM_PLAIN)
        s.box(x - 0.01, 2.79, 14, 0.09, 0.09, 2, ORANGE)
    hedge(s, 0.85, 2.85, 0.15, 0.06, 4, st.season)
    hedge(s, 2.0, 2.85, 0.15, 0.06, 4, st.season)
    for (x, y) in ((1.0, 1.0), (2.0, 1.0), (1.0, 2.0), (2.0, 2.0)):
        tree(s, x, y, 20, 0.12, season=st.season, seed=int(x * y * 3))
    bench(s, 1.7, 1.05, "x")
    bench(s, 1.05, 1.7, "y")
    kids(s, [(1.45, 2.4), (1.6, 1.9), (1.3, 1.25), (1.7, 1.7), (0.95, 1.5)])
    bike_rack(s, 0.9, 0.95, 3)
    lamp(s, 1.35, 2.7, 14)
    lamp(s, 1.65, 2.7, 14)
    return s


# ---------------------------------------------------------------- university
@model("university", (3, 3), "education", "University", front=True,
       ups=(("wing", "seats wing"), ("library", "library")),
       note="Domed great hall with portico, clock tower, lawns; the landmark of the education set.")
def university(st):
    s = ik.Scene((3, 3), 34)
    lot(s, "grass")
    path(s, 1.4, 1.35, 1.6, 3.0)
    rect(s, 1.0, 1.35, 2.0, 1.55, "paving", layer=2)
    h = 3 * ST + 2
    # long main block (stone) with brick end pavilions
    block(s, 0.25, 0.25, 2.5, 0.7, h, STONE_W, roof_mat="roof_flat", rim=STONE_PLAIN)
    for z in (ST + 1, 2 * ST + 1):
        band(s, 0.25, 0.25, 2.5, 0.7, z, 1.2, TRIM_W)
    band(s, 0.25, 0.25, 2.5, 0.7, h - 1, 2.0, ORANGE)
    for x in (0.25, 2.45):
        block(s, x, 0.25, 0.3, 0.7, h + 6, BRICK, roof="hip", roof_mat=ROOF, roof_h=7)
    # central great hall: taller block + portico + pediment
    hh = 4 * ST
    block(s, 0.95, 0.35, 1.1, 0.75, hh, STONE_W, roof_mat="roof_flat", rim=STONE_PLAIN)
    band(s, 0.95, 0.35, 1.1, 0.75, hh - 1, 2.0, ORANGE)
    s.box(0.97, 1.1, 0, 1.06, 0.2, 2, STONE_PLAIN)
    s.box(0.99, 1.1, 2, 1.02, 0.2, 1, STONE_PLAIN)
    columns(s, 1.0, 2.0, 1.15, 6, 3 * ST + 2, z=3, w=0.05)
    s.box(0.96, 1.08, 3 * ST + 5, 1.08, 0.24, 2, STONE_PLAIN)
    s.roof_gable(0.94, 1.06, 3 * ST + 7, 1.12, 0.26, 9, STONE_PLAIN, axis="x", overhang=0.03, gable=STONE_PLAIN)
    door(s, 1.4, 1.1, 0.2, 14, "+y", "door_glass", z=3)
    # dome on a drum
    s.cylinder(1.5, 0.72, hh, 0.3, 8, STONE_PLAIN, segs=20)
    for k in range(8):
        a = 2 * math.pi * k / 8
        s.box(1.5 + 0.3 * math.cos(a) - 0.015, 0.72 + 0.3 * math.sin(a) - 0.015, hh + 2, 0.03, 0.03, 5,
              M("glass_dark"))
    s.cylinder(1.5, 0.72, hh + 8, 0.32, 1.5, TRIM_W, segs=20)
    s.dome(1.5, 0.72, hh + 9.5, 0.3, 20, M("roof_flat", ramp="teal", shade=-0.5))
    s.cylinder(1.5, 0.72, hh + 29, 0.05, 5, TRIM_W, segs=8)
    cone(s, 1.5, 0.72, hh + 34, 0.05, 7, P("yellow", 3, snow=False))
    # clock tower (west, in front of the end pavilion)
    tz = 5 * ST + 8
    s.box(0.3, 0.95, 0, 0.36, 0.36, tz, STONE_W)
    s.box(0.28, 0.93, tz - 12, 0.4, 0.4, 12, STONE_PLAIN)
    clock_face(s, 0.48, 1.33, tz - 10, 0.07, "+y")
    clock_face(s, 0.68, 1.13, tz - 10, 0.07, "+x")
    s.box(0.26, 0.91, tz, 0.44, 0.44, 2, ORANGE)
    s.roof_hip(0.28, 0.93, tz + 2, 0.4, 0.4, 14, M("roof_flat", ramp="teal", shade=-0.5))
    door(s, 0.42, 1.31, 0.12, 9, "+y", "door_glass")
    # upgrades
    if "wing" in st.ups:
        block(s, 2.2, 1.1, 0.6, 1.0, 2 * ST + 2, STONE_W, roof_mat="roof_flat", rim=STONE_PLAIN)
        band(s, 2.2, 1.1, 0.6, 1.0, ST + 1, 1.2, TRIM_W)
        band(s, 2.2, 1.1, 0.6, 1.0, 2 * ST, 1.8)
        door(s, 2.4, 2.1, 0.2, 8, "+y", "door_glass")
    else:
        for (x, y, hh_) in ((2.4, 1.4, 24), (2.65, 1.85, 20), (2.35, 2.0, 22)):
            tree(s, x, y, hh_, 0.15, season=st.season, seed=int(y * 7))
        bench(s, 2.2, 1.3, "y")
    if "library" in st.ups:
        s.cylinder(0.7, 2.0, 0, 0.32, 2 * ST, STONE_PLAIN, segs=20)
        s.cylinder(0.7, 2.0, 2 * ST, 0.34, 1.5, ORANGE, segs=20)
        s.dome(0.7, 2.0, 2 * ST + 1.5, 0.32, 10, M("roof_flat", ramp="teal", shade=-0.5))
        door(s, 0.6, 2.3, 0.2, 8, "+y", "door_glass")
    else:
        tree(s, 0.55, 1.9, 24, 0.17, season=st.season, seed=3)
        tree(s, 0.85, 2.25, 18, 0.12, season=st.season, seed=4)
    # lawns, fountain, paths, students
    rect(s, 0.25, 2.55, 1.35, 2.95, "meadow", layer=1)
    rect(s, 1.65, 2.55, 2.75, 2.95, "meadow", layer=1)
    fountain(s, 1.5, 2.1, 0.17, st.frame)
    disc(s, 1.5, 2.1, 0.3, "paving", layer=2)
    for x in (1.2, 1.8):
        tree(s, x, 1.75, 14, 0.1, "poplar", season=st.season, seed=int(x * 5))
        tree(s, x, 2.7, 14, 0.1, "poplar", season=st.season, seed=int(x * 7))
    bench(s, 1.1, 2.1, "y")
    bench(s, 1.9, 2.1, "y")
    for (x, y) in ((1.35, 1.65), (1.65, 1.65), (1.35, 2.9), (1.65, 2.9)):
        lamp(s, x, y, 14)
    kids(s, [(1.45, 1.5), (1.58, 1.9), (1.4, 2.5), (1.2, 2.3), (1.8, 2.45), (1.0, 1.45)])
    return s
