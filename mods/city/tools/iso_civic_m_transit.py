"""CIVIC models: transit (bus depot, taxi depot, metro station, tram depot, train station, rail yard, cargo
harbor, cargo airport) and the highway entry. Accent: teal. Rails are ground decals (ballast + sleepers + rails)."""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, wall_mat, canopy, pole, lattice, beam, \
    fence, hcyl, stripes, disc, panel
from iso_civic_props import vehicle, bay_row, tree, person, lamp, bench, hedge, flag, crate_stack, sea, WATER_Z
from iso_civic_states import model

S = ik.S
TEAL = P("teal", 2.5)
TEAL_D = P("teal", 0.5)
WHITE = P("snow", 1.0)
HALL = wall_mat("siding", ramp="grey", shade=3, win_w=6, win_h=3, period=10, storey=12, glass="glass", lit=0.7)
SIDING = M("metal_light", shade=0.5)
BRICK = wall_mat("brick", win_w=4, win_h=6, period=8, sill=3, storey=ST, glass="glass", lit=0.7)
BRICK_P = M("brick")
BALLAST = M("gravel", shade=0.5)
SLEEPER = P("wood", -4.5, snow=False)
RAIL = P("grey", 4.5, snow=False)
RAIL_S = P("grey", 3, snow=False)


# ---------------------------------------------------------------- local helpers
def track_x(s, x0, x1, y, w=0.15, layer=2, rails=True, bed=True, z=0.0):
    """Track along x centred on y: gravel bed, sleepers, two rails (ground decals)."""
    if bed:
        rect(s, x0, y - w / 2, x1, y + w / 2, BALLAST, layer, z)
    n = max(2, int((x1 - x0) / 0.075))
    for i in range(n):
        xx = x0 + (i + 0.5) * (x1 - x0) / n
        rect(s, xx - 0.011, y - w / 2 + 0.01, xx + 0.011, y + w / 2 - 0.01, SLEEPER, layer + 1, z)
    if rails:
        for dy in (-0.036, 0.036):
            rect(s, x0, y + dy - 0.009, x1, y + dy + 0.009, RAIL, layer + 2, z)


def track_y(s, y0, y1, x, w=0.15, layer=2, rails=True, bed=True, z=0.0):
    """Track along y centred on x."""
    if bed:
        rect(s, x - w / 2, y0, x + w / 2, y1, BALLAST, layer, z)
    n = max(2, int((y1 - y0) / 0.075))
    for i in range(n):
        yy = y0 + (i + 0.5) * (y1 - y0) / n
        rect(s, x - w / 2 + 0.01, yy - 0.011, x + w / 2 - 0.01, yy + 0.011, SLEEPER, layer + 1, z)
    if rails:
        for dx in (-0.036, 0.036):
            rect(s, x + dx - 0.009, y0, x + dx + 0.009, y1, RAIL, layer + 2, z)


def buffer_stop_x(s, x, y, facing=1):
    """Buffer stop at the end of a track along x (facing = +1: the track lies to the -x side)."""
    s.box(x - (0.04 if facing > 0 else 0), y - 0.06, 0, 0.04, 0.12, 5, P("red", -1, snow=False))
    s.box(x - (0.07 if facing > 0 else -0.04), y - 0.05, 2, 0.03, 0.025, 2, P("grey", -2, snow=False))
    s.box(x - (0.07 if facing > 0 else -0.04), y + 0.025, 2, 0.03, 0.025, 2, P("grey", -2, snow=False))


def roundel(s, cx, y, z, r=0.17, face="+y", ramp="red"):
    """Transit roundel: red disc with a white 'M' standing in a vertical plane facing +y."""
    t = 0.025
    hcyl(s, cx, y, z, r, t, P(ramp, 0.5, snow=False), "y", 16)
    wh = P("snow", 1.5, snow=False)
    yy = y + t + 0.004
    hz = r * S * 0.5                 # half height of the M in px
    a = r * 0.5                      # half width in cells
    w = 1.6 / 32.0
    for xx in (cx - a, cx + a - w):
        s.box(xx, yy - 0.002, z - hz, w, 0.005, 2 * hz, wh)
    beam(s, (cx - a + w, yy, z + hz), (cx, yy, z - hz * 0.2), wh, w=0.01, hpx=2.2)
    beam(s, (cx + a - w, yy, z + hz), (cx, yy, z - hz * 0.2), wh, w=0.01, hpx=2.2)


def awning_strip(s, x, y, dx, z, h=3, ramp="teal"):
    s.box(x, y, z, dx, 0.07, h, M("awning", ramp=ramp, width=3), top=P(ramp, 1))


# ---------------------------------------------------------------- bus depot
DOOR_D = P("slate", -3.5, snow=False)


def bigdoors(s, x0, y, n, pitch, w, h, face="+y"):
    """Row of dark roller doors with a pale frame."""
    for i in range(n):
        x = x0 + i * pitch
        s.box(x - 0.025, y, 0, w + 0.05, 0.01, h + 2, P("snow", -0.5, snow=False))
        door(s, x, y, w, h, face, DOOR_D, t=0.02)


@model("busdepot", (3, 2), "transit", "Bus depot", front=True,
       note="Garage hall at the back, forecourt with buses, fuel pumps and wash bay on the right.")
def busdepot(st):
    s = ik.Scene((3, 2), 61)
    lot(s, "concrete_ground")
    rect(s, 0.05, 0.9, 2.95, 1.95, "asphalt", layer=1)
    # garage hall: low gable roof along x, teal fascia
    block(s, 0.15, 0.12, 2.0, 0.78, ST + 8, HALL, roof="gable", roof_mat="roof_metal", roof_h=7, axis="x",
          gable=SIDING)
    s.box(0.14, 0.11, ST + 5, 2.02, 0.8, 3, TEAL)
    bigdoors(s, 0.3, 0.9, 4, 0.47, 0.34, 15)
    # office
    block(s, 2.25, 0.12, 0.6, 0.45, ST + 4, wall_mat("plaster", ramp="sand", win_w=4, period=8, glass="glass"),
          roof_mat="roof_flat")
    door(s, 2.45, 0.57, 0.18, 8, "+y", "door_glass")
    s.box(2.24, 0.56, ST, 0.62, 0.07, 2, TEAL)
    # wash bay: drive-through shed
    s.box(2.2, 0.68, 0, 0.7, 0.42, 17, M("metal_light", shade=1.5), top=M("roof_flat", ramp="teal", shade=0.5))
    door(s, 2.3, 1.1, 0.32, 12, "+y", DOOR_D, t=0.02)
    s.box(2.28, 1.1, 12, 0.36, 0.02, 3, TEAL)
    for k in range(3):
        s.box(2.7, 1.1, 2 + k * 4, 0.12, 0.012, 3, P("water", 1, snow=False))
    # fuel canopy with pumps
    rect(s, 2.1, 1.3, 2.95, 1.9, M("concrete_ground", shade=-0.5), layer=2)
    canopy(s, 2.2, 1.4, 0.65, 0.4, 16, WHITE, post_mat=P("grey", 3))
    s.box(2.2, 1.4, 16, 0.65, 0.4, 1.5, TEAL)
    for x in (2.35, 2.6):
        s.box(x, 1.55, 0, 0.07, 0.1, 9, P("red", 0.5))
        s.box(x + 0.012, 1.645, 5, 0.046, 0.012, 3, P("glass", 0))
    # roundel sign on the corner
    pole(s, 0.12, 1.05, 34, P("grey", 1, snow=False), w=0.02)
    s.box(0.04, 1.03, 34, 0.16, 0.03, 11, TEAL, top=P("teal", 2))
    s.box(0.07, 1.06, 36, 0.1, 0.012, 7, WHITE)
    s.box(0.09, 1.07, 38, 0.06, 0.012, 3, TEAL_D)
    # parked buses: long sides toward the viewer
    for (x, y, p) in ((0.3, 1.15, "teal"), (1.1, 1.15, "yellow"), (0.55, 1.5, "teal"), (1.35, 1.5, "teal")):
        vehicle(s, x, y, "x", "bus", p, L=0.7)
    rect(s, 0.2, 1.4, 2.0, 1.412, "marking", layer=3)
    lamp(s, 0.1, 1.9, 14)
    lamp(s, 2.05, 1.9, 14)
    person(s, 2.02, 1.3, "teal")
    tree(s, 2.95, 0.75, 18, 0.12, season=st.season, seed=3)
    return s


# ---------------------------------------------------------------- taxi depot
@model("taxidepot", (2, 2), "transit", "Taxi depot", front=True,
       note="Dispatch office with a roof sign, workshop, yellow taxis in marked bays.")
def taxidepot(st):
    s = ik.Scene((2, 2), 62)
    lot(s, "concrete_ground")
    rect(s, 0.05, 0.75, 1.95, 1.95, "asphalt", layer=1)
    # office, 2 storeys, warm brick with yellow band
    ow = wall_mat("brick", ramp="sand", win_w=4, win_h=5, period=8, sill=3, storey=ST, glass="glass", lit=0.8)
    block(s, 0.15, 0.12, 0.95, 0.6, 2 * ST + 2, ow, roof_mat="roof_flat", rim=M("brick", ramp="sand"))
    s.box(0.14, 0.11, 2 * ST - 1, 0.97, 0.62, 3, P("yellow", 1.5))
    door(s, 0.4, 0.72, 0.22, 9, "+y", "door_glass")
    s.box(0.34, 0.715, 10, 0.34, 0.1, 2, P("yellow", 1.5))
    # roof sign (lit TAXI box)
    s.box(0.45, 0.3, 2 * ST + 2, 0.3, 0.1, 5, M("lamp", ramp="yellow", eramp="yellow", eshade=9))
    # checker band
    for k in range(9):
        s.box(0.16 + k * 0.1, 0.725, 2 * ST - 1, 0.05, 0.012, 3, P("grey", -5 if k % 2 else 5, snow=False))
    # workshop
    block(s, 1.2, 0.12, 0.7, 0.5, ST + 4, M("metal_light", shade=1), roof_mat="roof_metal")
    door(s, 1.3, 0.62, 0.22, 11, "+y", DOOR_D, t=0.02)
    door(s, 1.58, 0.62, 0.22, 11, "+y", DOOR_D, t=0.02)
    s.box(1.2, 0.61, ST + 1, 0.7, 0.03, 3, P("yellow", 1.5))
    # taxi bays (cabs nose-in)
    bay_row(s, 0.15, 1.25, 5, 0.34, "x", "taxi", ("yellow", "yellow", None, "yellow", "yellow"), depth=0.4)
    vehicle(s, 0.75, 0.86, "x", "taxi", "yellow")
    rect(s, 0.15, 1.8, 1.85, 1.812, "marking_yellow", layer=3)
    # taxi stand sign
    pole(s, 1.8, 1.0, 22, P("grey", 1, snow=False), w=0.02)
    s.box(1.7, 0.98, 22, 0.2, 0.03, 8, P("yellow", 1.5), top=P("yellow", 2))
    s.box(1.74, 1.01, 24, 0.12, 0.012, 4, P("grey", -6, snow=False))
    lamp(s, 0.08, 1.9, 14)
    lamp(s, 1.9, 1.6, 14)
    person(s, 1.55, 1.1, "yellow")
    tree(s, 1.95, 0.8, 18, 0.12, season=st.season, seed=5)
    return s


# ---------------------------------------------------------------- metro station
@model("metrostation", (1, 2), "transit", "Metro station", front=True,
       note="Entrance pavilion with a stairwell down to the platforms and the big M roundel on a pole.")
def metrostation(st):
    s = ik.Scene((1, 2), 63)
    lot(s, "paving")
    rect(s, 0.03, 0.03, 0.97, 1.97, M("paving", shade=0.8), layer=1)
    # stairwell: sunken stairs with concrete rim, steps as alternating shaded strips
    rect(s, 0.14, 0.22, 0.86, 1.02, P("slate", -4.5), layer=2)
    for k in range(10):
        y = 0.25 + k * 0.075
        rect(s, 0.2, y, 0.8, y + 0.04, P("grey", 1.5 - 0.5 * k, snow=False), layer=3)
    for (x, y, dx, dy) in ((0.12, 0.2, 0.74, 0.04), (0.12, 1.0, 0.74, 0.04), (0.12, 0.2, 0.04, 0.84),
                           (0.84, 0.2, 0.04, 0.84)):
        s.box(x, y, 0, dx, dy, 4, M("concrete", shade=1.5))
    s.box(0.49, 0.28, 3, 0.02, 0.7, 1.2, P("grey", 4, snow=False))
    # glass canopy on posts over the stairs
    cz = 16
    for (x, y) in ((0.15, 0.22), (0.83, 0.22), (0.15, 0.98), (0.83, 0.98)):
        pole(s, x, y, cz, P("teal", 0, snow=False), w=0.03)
    s.box(0.1, 0.17, cz, 0.8, 0.9, 1.5, P("teal", 1.5))
    s.box(0.14, 0.21, cz + 1.5, 0.72, 0.82, 1.5, M("glass", shade=1.5), top=M("glass", shade=1.5))
    # ticket kiosk
    block(s, 0.18, 1.2, 0.64, 0.4, ST + 4,
          wall_mat("plaster", ramp="snow", shade=0.3, win_w=5, period=8, glass="glass", lit=0.9),
          roof_mat="roof_flat", rim=M("plaster", ramp="snow"))
    s.box(0.17, 1.19, ST + 1, 0.66, 0.42, 3, TEAL)
    door(s, 0.4, 1.6, 0.2, 9, "+y", "door_glass")
    # roundel on a pole (front-right corner)
    pole(s, 0.74, 1.8, 34, P("grey", 1, snow=False), w=0.024)
    roundel(s, 0.74, 1.78, 40, 0.2, "+y", "red")
    s.box(0.7, 1.76, 0, 0.08, 0.08, 3, P("grey", 0))
    lamp(s, 0.1, 1.85, 14)
    person(s, 0.5, 1.8, "teal")
    return s


# ---------------------------------------------------------------- tram depot
TYRE_D = P("grey", -6, snow=False)


def tram(s, x, y, L=0.85, axis="y", body="red"):
    """A parked tram as a simple long box: coloured body with a window band, white roof and a pantograph."""
    W = 0.13
    bm = M("windows_plaster", ramp=body, base=P(body, 0.5), shade=0.5, storey=8, win_w=5, win_h=3, period=6, sill=4,
           margin=2, glass="glass", snow=False)
    if axis == "y":
        s.box(x, y, 2, W, L, 9, bm, top=P("snow", 0.5))
        for t in (0.12, 0.8):
            s.box(x, y + L * t - 0.04, 0, W, 0.08, 2, TYRE_D)
        s.box(x + W / 2 - 0.02, y + L * 0.5 - 0.04, 11, 0.04, 0.08, 1.5, P("grey", -2, snow=False))
    else:
        s.box(x, y, 2, L, W, 9, bm, top=P("snow", 0.5))
        for t in (0.12, 0.8):
            s.box(x + L * t - 0.04, y, 0, 0.08, W, 2, TYRE_D)
        s.box(x + L * 0.5 - 0.04, y + W / 2 - 0.02, 11, 0.08, 0.04, 1.5, P("grey", -2, snow=False))


@model("tramdepot", (3, 2), "transit", "Tram depot", front=True,
       note="Brick hall; three tracks run out of its doors across a gravel yard. Parked trams are simple boxes.")
def tramdepot(st):
    s = ik.Scene((3, 2), 64)
    lot(s, "concrete_ground")
    rect(s, 0.05, 0.85, 2.95, 1.95, "asphalt", layer=1)
    block(s, 0.12, 0.1, 2.76, 0.75, ST + 9, BRICK, roof="gable", roof_mat="roof_tiles_brown", roof_h=7, axis="x",
          gable=BRICK_P)
    s.box(0.11, 0.09, ST + 6, 2.78, 0.77, 2, P("sand", 2))
    xs = (0.55, 1.5, 2.45)
    for x in xs:
        s.box(x - 0.2, 0.85, 0, 0.4, 0.012, 15, P("sand", 2.5, snow=False))
        door(s, x - 0.17, 0.85, 0.34, 13, "+y", DOOR_D, t=0.02)
        track_y(s, 0.88, 1.95, x, 0.14)
        s.box(x - 0.06, 1.92, 0, 0.12, 0.04, 4, P("red", -1, snow=False))
    tram(s, 0.55 - 0.065, 1.05, 0.85, "y", "red")
    tram(s, 1.5 - 0.065, 1.2, 0.85, "y", "snow")
    for x in (1.02, 1.98):
        pole(s, x, 1.88, 18, P("slate", 0, snow=False), w=0.02)
        beam(s, (x, 1.88, 18), (x, 0.9, 18), P("grey", -3, snow=False), w=0.008, hpx=0.8)
    s.box(2.5, 1.15, 0, 0.3, 0.3, 7, M("metal_light", shade=0.5))
    lamp(s, 0.1, 1.9, 14)
    person(s, 1.05, 1.5, "teal")
    tree(s, 2.9, 1.7, 16, 0.1, season=st.season, seed=6)
    return s


# ---------------------------------------------------------------- train station
@model("trainstation", (2, 4), "transit", "Train station", front=True,
       note="Terminus: station hall with a clock tower at the front, island platform with a long canopy, "
            "a track each side. Trains are LIFE's.")
def trainstation(st):
    s = ik.Scene((2, 4), 65)
    lot(s, "concrete_ground")
    rect(s, 0.04, 3.55, 1.96, 3.96, "paving", layer=1)
    # tracks and island platform
    for x in (0.3, 1.7):
        track_y(s, 0.0, 2.78, x, 0.16)
        buffer_y(s, x, 2.78)
    PLAT = M("concrete_ground", shade=1.5)
    s.box(0.58, 0.05, 0, 0.84, 2.73, 3, PLAT, top=M("paving", shade=1))
    for x in (0.58, 1.38):
        rect(s, x + (0.0 if x < 1 else 0.03), 0.06, x + (0.03 if x < 1 else 0.06), 2.78, "marking_yellow", layer=4,
             z=3)
    # long canopy on posts, with a teal fascia
    cz = 24
    for k in range(4):
        y = 0.3 + k * 0.5
        for x in (0.64, 1.32):
            pole(s, x, y, cz, P("teal", -0.5, snow=False), w=0.035, z=3)
    s.box(0.5, 0.2, cz + 1.5, 1.0, 1.75, 1.5, P("teal", 1.5))
    s.box(0.5, 0.2, cz, 1.0, 1.75, 1.5, M("roof_flat", ramp="grey", shade=1.5), top=M("roof_metal", ramp="teal", shade=0))
    # platform furniture
    for y in (0.7, 1.5, 2.2):
        bench(s, 0.88, y, "y", 0.14)
    person(s, 0.75, 1.1, "red")
    person(s, 1.2, 1.95, "teal")
    person(s, 0.7, 2.4, "yellow")
    s.box(0.95, 1.05, 3, 0.1, 0.05, 6, P("grey", 1))                 # platform sign board
    s.box(0.97, 1.05, 9, 0.06, 0.05, 2, P("teal", 1.5))
    # station hall (2 storeys + central clock tower)
    SW = wall_mat("brick", ramp="sand", win_w=4, win_h=7, period=8, sill=3, storey=ST, glass="glass", lit=0.8)
    block(s, 0.08, 2.82, 1.84, 0.86, 2 * ST + 4, SW, roof="hip", roof_mat="roof_tiles_brown", roof_h=6,
          gable=BRICK_P)
    s.box(0.07, 2.81, ST + 6, 1.86, 0.88, 2, P("sand", 2.5))
    # tower
    block(s, 0.75, 3.05, 0.5, 0.45, 2 * ST + 24, wall_mat("brick", ramp="sand", win_w=3, win_h=5, period=8,
                                                           storey=ST, glass="glass"), roof="hip",
          roof_mat="roof_tiles_brown", roof_h=14, gable=BRICK_P)
    # entrance: three arched doors + clock
    for x in (0.35, 0.82, 1.3):
        s.box(x - 0.01, 3.68, 0, 0.26, 0.012, 13, P("snow", -0.5, snow=False))
        door(s, x, 3.68, 0.24, 11, "+y", "door_glass", t=0.02)
    s.box(0.3, 3.68, 13, 1.35, 0.03, 2, P("teal", 1.5))
    clock(s, 1.0, 3.5, 2 * ST + 14)
    # forecourt
    vehicle(s, 0.3, 3.8, "x", "taxi", "yellow")
    vehicle(s, 0.7, 3.8, "x", "taxi", "yellow")
    vehicle(s, 1.35, 3.8, "x", "car", "slate")
    lamp(s, 0.1, 3.9, 14)
    lamp(s, 1.9, 3.9, 14)
    tree(s, 1.85, 2.4, 20, 0.12, season=st.season, seed=2)
    tree(s, 0.15, 2.5, 18, 0.1, season=st.season, seed=7)
    return s


def buffer_y(s, x, y):
    """Buffer stop at the +y end of a track along y."""
    s.box(x - 0.06, y - 0.04, 0, 0.12, 0.04, 5, P("red", -1, snow=False))
    s.box(x - 0.05, y - 0.07, 2, 0.025, 0.03, 2, P("grey", -2, snow=False))
    s.box(x + 0.025, y - 0.07, 2, 0.025, 0.03, 2, P("grey", -2, snow=False))


def clock(s, cx, y, z, r=0.075):
    """Round clock face on a +y facade."""
    hcyl(s, cx, y, z, r, 0.02, P("snow", 1.5, snow=False), "y", 16)
    yy = y + 0.022
    dk = P("grey", -6, snow=False)
    s.box(cx - 0.006, yy, z, 0.012, 0.004, r * S * 0.8, dk)
    s.box(cx - 0.006, yy, z - 0.5, r * 0.8, 0.004, 1, dk)


# ---------------------------------------------------------------- rail yard
def wagon(s, x, y, kind="box", paint="terra", L=0.27):
    """Goods wagon on a track along x; (x, y) = rear-left corner of the body."""
    W = 0.11
    for t in (0.18, 0.76):
        s.box(x + L * t - 0.025, y + 0.005, 0, 0.05, W - 0.01, 2.5, P("grey", -5.5, snow=False))
    s.box(x, y + 0.01, 2, L, W - 0.02, 1, P("grey", -3, snow=False))
    if kind == "box":
        s.box(x + 0.01, y, 3, L - 0.02, W, 8, M("siding", ramp=paint, shade=0, snow=False),
              top=P(paint, 2))
    elif kind == "flat":
        s.box(x, y, 3, L, W, 1, P("wood", 1))
        s.box(x + 0.02, y + 0.01, 4, L * 0.45, W - 0.02, 5, M("metal", ramp=paint, shade=0.5))
        s.box(x + L * 0.55, y + 0.01, 4, L * 0.4, W - 0.02, 5, M("metal", ramp="teal", shade=0.5))
    elif kind == "tank":
        hcyl(s, x + 0.01, y + W / 2, 7.5, 0.052, L - 0.02, M("metal", ramp=paint, shade=0.5), "x", 10)
    elif kind == "open":
        s.box(x + 0.01, y, 3, L - 0.02, W, 5, M("metal", ramp=paint, shade=-0.5), top=P("slate", -4))
        s.box(x + 0.03, y + 0.015, 8, L - 0.06, W - 0.03, 0.4, P("grey", -6, snow=False))
        s.ellipsoid(x + L / 2, y + W / 2, 8, L * 0.4, W * 0.4, 3, M("rock", ramp="slate", shade=-3), zmin=7, rough=0.4)


def signal(s, x, y, h=16, green=True):
    pole(s, x, y, h, P("grey", -1, snow=False), w=0.016)
    s.box(x - 0.025, y - 0.02, h - 6, 0.05, 0.04, 6, P("grey", -5, snow=False))
    s.box(x - 0.012, y + 0.018, h - 2.5, 0.024, 0.006, 1.8, M("lamp", ramp="leaf" if green else "red",
                                                                eramp="leaf" if green else "red"))


@model("railyard", (3, 2), "transit", "Rail yard", front=True,
       note="Four sidings with parked wagons, buffer stops, signals and a signal box.")
def railyard(st):
    s = ik.Scene((3, 2), 66)
    lot(s, "gravel")
    rect(s, 0.04, 0.04, 2.96, 1.96, M("dirt", ramp="grey", shade=-2.5), layer=1)
    ys = (0.62, 0.92, 1.22, 1.52)
    # lead track along y with a ladder of sidings
    track_y(s, 0.45, 1.75, 0.22, 0.14, layer=2)
    for y in ys:
        track_x(s, 0.22, 2.95, y, 0.14, layer=2)
        buffer_stop_x(s, 2.96, y)
    kinds = [("box", "terra"), ("box", "brick"), ("tank", "slate"), ("open", "olive"), ("flat", "yellow"),
             ("box", "teal"), ("tank", "terra"), ("open", "wood"), ("box", "slate"), ("flat", "terra")]
    plan = {0: (0.5, 0.82, 1.14, 1.46), 1: (1.9, 2.22, 2.54), 2: (0.5, 0.82, 1.9, 2.22), 3: (1.14, 1.46, 1.78)}
    k = 0
    for r, xs in plan.items():
        for x in xs:
            kd, pt = kinds[k % len(kinds)]
            wagon(s, x, ys[r] - 0.055, kd, pt)
            k += 1
    # signal box: ground floor brick, upper glazed cab
    s.box(1.55, 0.08, 0, 0.36, 0.3, 11, M("brick", shade=0), top=P("slate", -1))
    s.box(1.53, 0.06, 11, 0.4, 0.34, 8, M("windows_plaster", ramp="snow", base=P("snow", 0.5), storey=8, win_w=7,
                                           win_h=5, period=9, sill=2, margin=1, glass="glass", lit=1.0),
          top=M("roof_flat", ramp="slate", shade=0))
    door(s, 1.66, 0.38, 0.1, 7, "+y", "door")
    s.box(1.52, 0.05, 19, 0.42, 0.36, 1.5, P("slate", -1))
    # signals
    signal(s, 0.34, 0.45, 18, True)
    signal(s, 1.05, 1.75, 16, False)
    signal(s, 2.0, 0.45, 16, True)
    # service building + water crane
    block(s, 0.5, 0.05, 0.6, 0.28, ST + 2, M("siding", ramp="wood", shade=0), roof="gable", roof_mat="roof_metal",
          roof_h=5, axis="x", gable=M("siding", ramp="wood"))
    pole(s, 2.6, 1.78, 20, P("grey", 0), w=0.03)
    beam(s, (2.6, 1.78, 20), (2.6, 1.6, 18), P("grey", 0), w=0.03, hpx=2)
    for (x, y) in ((2.8, 0.2), (2.65, 0.28)):
        tree(s, x, y, 18, 0.12, "conifer", season=st.season)
    lamp(s, 0.1, 1.9, 16)
    person(s, 1.4, 0.45, "yellow")
    return s


# ---------------------------------------------------------------- cargo harbor
CRANE = P("water", 1.5, snow=False)
CRANE_D = P("water", -0.5, snow=False)


def gantry_crane(s, cy, frame, x0=1.45, x1=2.92, legs_x=(1.86, 2.3), boom_z=50):
    """Ship-to-shore container crane (portal legs on the quay, boom over the water along +x).
    The trolley (with a hanging container) runs along the boom: `frame` 0..3."""
    ya, yb = cy - 0.2, cy + 0.2
    for lx in legs_x:
        for ly in (ya, yb):
            s.box(lx - 0.02, ly - 0.02, 0, 0.04, 0.04, boom_z - 2, CRANE)
    for lz in (24, boom_z - 6):
        for lx in legs_x:
            beam(s, (lx, ya, lz), (lx, yb, lz), CRANE_D, w=0.025, hpx=2)
        for ly in (ya, yb):
            beam(s, (legs_x[0], ly, lz), (legs_x[1], ly, lz), CRANE_D, w=0.025, hpx=2)
    for ly in (ya, yb):         # diagonal bracing
        beam(s, (legs_x[0], ly, 0), (legs_x[1], ly, 24), CRANE_D, w=0.014, hpx=1.2)
        beam(s, (legs_x[1], ly, 0), (legs_x[0], ly, 24), CRANE_D, w=0.014, hpx=1.2)
    # girders along the boom (two, front and back) with a top deck
    for ly in (ya + 0.02, yb - 0.02):
        s.box(x0, ly - 0.02, boom_z - 4, x1 - x0, 0.04, 4, CRANE)
    s.box(x0, ya, boom_z, x1 - x0, yb - ya, 1.2, CRANE_D)
    # machinery house and A-frame
    s.box(legs_x[0] - 0.05, cy - 0.12, boom_z + 1.2, 0.3, 0.24, 7, P("snow", 0.5, snow=False),
          top=P("grey", 2))
    beam(s, (legs_x[1] - 0.02, cy, boom_z + 8), (x1 - 0.08, cy, boom_z + 1), CRANE_D, w=0.02, hpx=1.5)
    beam(s, (legs_x[0] - 0.1, cy, boom_z + 1), (legs_x[0] - 0.05, cy, boom_z + 12), CRANE_D, w=0.02, hpx=1.5)
    # trolley with spreader and a container, sliding over 4 frames
    tx = 2.0 + 0.2 * frame
    s.box(tx - 0.05, cy - 0.08, boom_z - 6, 0.1, 0.16, 3, P("grey", -3, snow=False))
    hz = 22 if frame % 2 == 0 else 24
    for dy in (-0.05, 0.05):
        beam(s, (tx, cy + dy, boom_z - 6), (tx, cy + dy, hz + 6), P("grey", -4, snow=False), w=0.008, hpx=0.8)
    s.box(tx - 0.1, cy - 0.04, hz + 5, 0.2, 0.08, 1, P("grey", -2, snow=False))
    s.box(tx - 0.09, cy - 0.04, hz, 0.18, 0.08, 5, M("metal", ramp=("terra", "teal", "yellow", "slate")[frame],
                                                     shade=0.5))


def straddle(s, x, y, axis="y", paint="yellow", load=True):
    """Straddle carrier: box frame on four legs with a container held between."""
    L, W = 0.3, 0.17
    m = P(paint, 0.5, snow=False)
    if axis == "y":
        L, W = W, L
        dx, dy = L, W
    else:
        dx, dy = L, W
    for ix in (0, 1):
        for iy in (0, 1):
            s.box(x + ix * (dx - 0.025), y + iy * (dy - 0.025), 0, 0.025, 0.025, 12, P("grey", -4, snow=False))
    s.box(x, y, 12, dx, dy, 2.5, m, top=m)
    s.box(x + 0.01, y + 0.01, 14.5, dx - 0.02, dy - 0.02, 1.2, P("grey", -3))
    s.box(x + dx * 0.5 - 0.02, y + 0.02, 14.5, 0.05, 0.06, 4, P("snow", 0, snow=False))
    if load:
        if axis == "y":
            s.box(x + 0.025, y + 0.04, 3, dx - 0.05, dy - 0.08, 5, M("metal", ramp="teal", shade=0.5))
        else:
            s.box(x + 0.04, y + 0.03, 3, dx - 0.08, dy - 0.06, 5, M("metal", ramp="terra", shade=0.5))


@model("cargoharbor", (3, 3), "transit", "Cargo harbor", anim=4,
       note="Quay on the right with an empty ship berth (ship is LIFE's); two gantry cranes whose trolleys "
            "move over 4 frames, container stacks, straddle carrier.")
def cargoharbor(st):
    s = ik.Scene((3, 3), 67)
    rect(s, 0, 0, 2.2, 3.0, "concrete_ground", layer=0)
    rect(s, 0.04, 0.04, 2.08, 2.96, M("concrete_ground", shade=0.5), layer=1)
    # water berth (right part) at the natural water level
    sea(s, 2.2, 0.0, 3.0, 3.0)
    # quay edge: raised concrete lip down to the water, yellow safety strip, bollards, fenders
    s.box(2.08, 0.0, WATER_Z, 0.12, 3.0, 3 - WATER_Z, M("concrete", shade=1), top=M("concrete_ground", shade=1))
    rect(s, 1.98, 0.02, 2.04, 2.98, "marking_yellow", layer=3)
    for y in (0.3, 1.0, 1.7, 2.4):
        s.box(2.1, y, 3, 0.06, 0.06, 2.5, P("grey", -5, snow=False))
        s.box(2.2, y + 0.1, -3.5, 0.025, 0.14, 4, P("grey", -6, snow=False))
    # crane rails
    rect(s, 1.84, 0.0, 1.88, 3.0, RAIL_S, layer=3)
    rect(s, 2.28, 0.0, 2.32, 3.0, RAIL_S, layer=3)
    for cy in (0.75, 1.95):
        gantry_crane(s, cy, (st.frame + (0 if cy < 1 else 2)) % 4 if not st.inactive else 0)
    # container yard (left): stacks with lanes between
    ramps = ("terra", "teal", "slate", "yellow")
    crate_stack(s, 0.12, 0.12, 2, 2, 2, 0.09, ramps=ramps)
    crate_stack(s, 0.12, 0.58, 2, 3, 3, 0.09, ramps=("teal", "terra", "slate"))
    crate_stack(s, 0.62, 0.12, 2, 2, 1, 0.09, ramps=("yellow", "slate"))
    crate_stack(s, 0.62, 0.58, 2, 2, 2, 0.09, ramps=("slate", "terra"))
    crate_stack(s, 0.12, 1.35, 2, 3, 2, 0.09, ramps=("terra", "yellow", "teal"))
    crate_stack(s, 0.12, 2.0, 2, 2, 1, 0.09, ramps=("slate", "teal"))
    crate_stack(s, 0.62, 1.35, 2, 2, 3, 0.09, ramps=("teal", "terra"))
    # straddle carrier + truck lane
    straddle(s, 1.2, 1.3, "y", "yellow")
    straddle(s, 1.35, 2.2, "x", "yellow", load=False)
    vehicle(s, 1.5, 0.6, "y", "truck", "teal", L=0.5)
    vehicle(s, 1.52, 1.0, "y", "truck", "terra", L=0.5)
    # port office with a signal mast
    block(s, 0.12, 2.5, 0.8, 0.4, ST + 4,
          wall_mat("plaster", ramp="snow", shade=0.2, win_w=5, period=8, glass="glass", lit=0.9),
          roof_mat="roof_flat", rim=M("plaster", ramp="snow"))
    s.box(0.11, 2.49, ST, 0.82, 0.42, 2, TEAL)
    door(s, 0.2, 2.9, 0.18, 8, "+y", "door_glass")
    pole(s, 0.95, 2.6, 26, P("grey", 0, snow=False), w=0.02)
    s.box(0.9, 2.58, 26, 0.1, 0.04, 2, M("lamp", ramp="red", eramp="red"))
    floodlight_mast(s, 1.75, 0.2)
    floodlight_mast(s, 1.75, 2.8)
    person(s, 1.0, 2.1, "yellow")
    return s


def floodlight_mast(s, x, y, h=34):
    pole(s, x, y, h, P("grey", 1, snow=False), w=0.024)
    s.box(x - 0.07, y - 0.025, h, 0.14, 0.05, 3, M("lamp", ramp="snow", eramp="yellow", eshade=11))


# ---------------------------------------------------------------- cargo airport
def runway_light(s, x, y):
    s.box(x - 0.012, y - 0.012, 0, 0.024, 0.024, 2, M("lamp", ramp="snow", eramp="yellow", eshade=11))


@model("cargoairport", (3, 3), "transit", "Cargo airport", front=True,
       note="Runway segment with edge lights, arched hangar, control tower and cargo apron. Aircraft are LIFE's.")
def cargoairport(st):
    s = ik.Scene((3, 3), 68)
    lot(s, "grass_dry")
    # runway along x at the front, taxiway to the apron
    rect(s, 0.0, 2.05, 3.0, 2.85, M("asphalt", shade=-2.0), layer=1)
    rect(s, 0.0, 2.05, 3.0, 2.07, "marking", layer=3)
    rect(s, 0.0, 2.83, 3.0, 2.85, "marking", layer=3)
    for k in range(7):
        rect(s, 0.15 + k * 0.4, 2.44, 0.4 + k * 0.4 - 0.1, 2.46, "marking", layer=3)
    for k in range(6):      # threshold bars
        rect(s, 0.04, 2.12 + k * 0.12, 0.2, 2.16 + k * 0.12, "marking", layer=3)
    rect(s, 1.0, 1.3, 1.25, 2.05, M("asphalt", shade=0), layer=1)
    rect(s, 1.11, 1.3, 1.13, 2.05, "marking_yellow", layer=3)
    for k in range(10):
        x = 0.1 + k * 0.3
        runway_light(s, x, 2.0)
        runway_light(s, x, 2.9) if x < 2.95 else None
    # cargo apron
    rect(s, 1.3, 0.08, 2.95, 1.9, M("concrete_ground", shade=1), layer=1)
    rect(s, 1.3, 1.9, 2.95, 1.92, "marking_yellow", layer=3)
    # arched hangar (axis y, door facing +y)
    s.box(0.12, 0.12, 0, 0.8, 1.2, 12, M("metal_light", shade=1))
    hcyl(s, 0.52, 0.12, 12, 0.4, 1.2, M("roof_metal", ramp="grey", shade=1.5), "y", 16, caps=True)
    s.box(0.12, 1.32, 0, 0.8, 0.02, 12, M("metal_light", shade=1))
    door(s, 0.2, 1.32, 0.64, 13, "+y", DOOR_D, t=0.02)
    s.box(0.18, 1.33, 14, 0.68, 0.03, 2, TEAL)
    # control tower
    s.box(2.45, 0.2, 0, 0.2, 0.2, 40, M("concrete", shade=1.5, panel_u=20))
    s.box(2.37, 0.12, 40, 0.36, 0.36, 9, M("glass", shade=1.0), top=P("grey", 1))
    s.box(2.34, 0.09, 49, 0.42, 0.42, 2, P("grey", 3))
    s.box(2.41, 0.16, 51, 0.28, 0.28, 2, P("grey", -1))
    pole(s, 2.55, 0.3, 14, P("grey", 2, snow=False), w=0.015, z=51)
    s.box(2.52, 0.27, 65, 0.06, 0.06, 1.5, M("lamp", ramp="red", eramp="red"))
    s.box(2.45, 0.2, 18, 0.2, 0.012, 1.5, P("red", 1, snow=False))
    # cargo containers (ULD) and dollies on the apron
    crate_stack(s, 1.4, 0.2, 2, 2, 2, 0.07, ramps=("slate", "terra", "teal"))
    crate_stack(s, 1.4, 0.6, 2, 1, 1, 0.07, ramps=("yellow",))
    crate_stack(s, 1.9, 1.0, 2, 3, 1, 0.07, ramps=("teal", "slate"))
    # dolly train
    for i in range(3):
        s.box(1.45 + i * 0.2, 1.4, 0, 0.17, 0.08, 2, P("grey", -3, snow=False))
        s.box(1.47 + i * 0.2, 1.41, 2, 0.13, 0.06, 5, M("metal", ramp=("terra", "slate", "teal")[i], shade=0.5))
    vehicle(s, 1.4, 1.55, "x", "van", "yellow")
    vehicle(s, 2.1, 1.55, "x", "truck", "snow", L=0.5)
    # cargo shed + fuel
    block(s, 2.2, 0.75, 0.65, 0.3, ST, M("metal_light", shade=0.5), roof_mat="roof_metal")
    door(s, 2.3, 1.05, 0.45, 8, "+y", DOOR_D, t=0.02)
    s.cylinder(2.25, 1.4, 0, 0.09, 8, M("metal_light", shade=2), top=P("grey", 2), segs=14)
    s.cylinder(2.5, 1.4, 0, 0.09, 8, M("metal_light", shade=2), top=P("grey", 2), segs=14)
    # windsock
    pole(s, 0.3, 1.8, 16, P("grey", 2, snow=False), w=0.016)
    hcyl(s, 0.3, 1.8, 14, 0.025, 0.12, M("awning", ramp="terra", width=3), "x", 8, caps=False)
    fence(s, [(0.04, 1.95), (0.04, 0.04), (2.96, 0.04), (2.96, 1.95)], 4)
    return s


# ---------------------------------------------------------------- highway entry
@model("highway-entry", (1, 1), "roads", "Highway entry (highway-w / -e / -n / -s)", front=True, power=False,
       build=False,
       note="One model for the 4 actors highway-w/e/n/s: they use the 4 facings (the road runs along the "
            "facing's y axis, the sign faces the incoming traffic).")
def highway_entry(st):
    s = ik.Scene((1, 1), 69)
    lot(s, "grass")
    # carriageway along y: two lanes each way, concrete median
    rect(s, 0.08, 0.0, 0.92, 1.0, "asphalt", layer=1)
    rect(s, 0.08, 0.0, 0.1, 1.0, "marking", layer=3)
    rect(s, 0.9, 0.0, 0.92, 1.0, "marking", layer=3)
    for k in range(4):
        y = 0.05 + k * 0.25
        rect(s, 0.29, y, 0.305, y + 0.13, "marking", layer=3)
        rect(s, 0.695, y, 0.71, y + 0.13, "marking", layer=3)
    s.box(0.475, 0.0, 0, 0.05, 1.0, 3, M("concrete", shade=1.5), top=M("concrete", shade=2))
    rect(s, 0.0, 0.0, 0.08, 1.0, "gravel", layer=1)
    rect(s, 0.92, 0.0, 1.0, 1.0, "gravel", layer=1)
    # sign gantry: two posts, a truss beam, a green panel
    gz = 30
    gy = 0.45
    for x in (0.03, 0.95):
        s.box(x, gy, 0, 0.03, 0.03, gz + 2, P("grey", 1, snow=False))
        s.box(x - 0.015, gy - 0.015, 0, 0.06, 0.06, 2, M("concrete", shade=1))
    s.box(0.03, gy, gz, 0.95, 0.03, 2.5, P("grey", 2, snow=False))
    beam(s, (0.03, gy + 0.015, gz + 5), (0.98, gy + 0.015, gz + 5), P("grey", 0, snow=False), w=0.01, hpx=1.2)
    for k in range(10):
        x = 0.05 + k * 0.095
        beam(s, (x, gy + 0.015, gz + 2.5), (x + 0.047, gy + 0.015, gz + 5), P("grey", 0, snow=False), w=0.008,
             hpx=1)
        beam(s, (x + 0.047, gy + 0.015, gz + 5), (x + 0.095, gy + 0.015, gz + 2.5), P("grey", 0, snow=False),
             w=0.008, hpx=1)
    # green welcome panel hanging in front of the beam: white lines stand in for "OpenCity"
    s.box(0.14, gy + 0.03, gz - 14, 0.72, 0.02, 16, P("leaf", -1.5, snow=False), top=P("leaf", -2))
    s.box(0.13, gy + 0.03, gz - 15, 0.74, 0.015, 1, P("snow", 1, snow=False))
    s.box(0.13, gy + 0.03, gz + 1, 0.74, 0.015, 1, P("snow", 1, snow=False))
    for (w, z) in ((0.5, 26), (0.34, 22.5), (0.44, 19)):
        s.box(0.5 - w / 2, gy + 0.052, z, w, 0.005, 1.6, P("snow", 2, snow=False))
    s.box(0.14, gy + 0.052, gz - 14, 0.008, 0.005, 16, P("snow", 1, snow=False))
    s.box(0.852, gy + 0.052, gz - 14, 0.008, 0.005, 16, P("snow", 1, snow=False))
    # flag on the roadside
    flag(s, 0.04, 0.12, 26, "red", 0, 0.12, 5, "snow")
    lamp(s, 0.05, 0.85, 16)
    lamp(s, 0.95, 0.15, 16)
    return s
