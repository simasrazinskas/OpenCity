"""CIVIC models: power (wind turbine, coal, solar, gas, hydro dam, nuclear). Accent: yellow/hazard."""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, wall_mat, stack, lathe, tank, pole, \
    lattice, beam, pipe, fence, hcyl, cone
from iso_civic_props import vehicle, tree, smoke, heap, lamp, sea, WATER_Z
from iso_civic_states import model

S = ik.S
HALL = wall_mat("brick", win_w=4, win_h=8, period=8, sill=4, storey=14, glass="slate", lit=0.6)
SIDING = M("metal_light", shade=0.5)
SIDING_W = wall_mat("siding", ramp="grey", shade=3, win_w=6, win_h=3, period=10, storey=12, glass="glass")
BLUE_BAND = P("water", 1.5)
WHITE_M = P("snow", 0.5)


def _plane_pt_x(hub, u, v):
    """Same for a model turned by an odd facing (normal (1,-1,0)), so the rotor still faces the camera."""
    return (hub[0] + u / (S * math.sqrt(2)), hub[1] + u / (S * math.sqrt(2)), hub[2] + v)


def _plane_pt(hub, u, v):
    """Point in the vertical plane facing the camera (normal (1,1,0)); u, v in metric px."""
    return (hub[0] + u / (S * math.sqrt(2)), hub[1] - u / (S * math.sqrt(2)), hub[2] + v)


@model("windturbine", (1, 1), "power", "Wind turbine", anim=4, note="Rotor turns (4 frames); stopped when inactive. st.variant 1 = built for odd facings "
       "(rotor plane turned so it faces the camera after the 90 degree rotation).")
def windturbine(st):
    s = ik.Scene((1, 1), 21)
    lot(s, "grass")
    s.cylinder(0.5, 0.5, 0, 0.14, 2, P("grey", 2), top=M("concrete_ground"))
    lathe(s, 0.5, 0.5, [(0, 0.05), (62, 0.025)], WHITE_M, segs=10)
    s.box(0.44, 0.44, 61, 0.12, 0.12, 6, WHITE_M)
    hub = (0.56, 0.56, 64)
    s.sphere(hub[0], hub[1], hub[2], 0.035, WHITE_M)
    blade = P("snow", 1.0, snow=False)
    a0 = (0 if st.inactive else st.frame) * (2 * math.pi / 3) / 4 + 0.3
    for k in range(3):
        a = a0 + k * 2 * math.pi / 3
        ca, sa = math.cos(a), math.sin(a)
        L, w0, w1 = 26, 2.2, 0.8
        pts = []
        for (r, w) in ((2, w0), (L, w1), (L, -w1 * 0.3), (2, -w0 * 0.6)):
            pts.append((_plane_pt_x if st.variant else _plane_pt)(hub, r * ca - w * sa, r * sa + w * ca))
        s.poly(pts, blade, cull=False, outward=(1, -1, 0) if st.variant else (1, 1, 0))
    s.box(0.48, 0.48, 67, 0.04, 0.04, 1.5, M("lamp", ramp="red", eramp="red"))
    return s


@model("powerplant-coal", (2, 2), "power", "Coal power plant", anim=4,
       note="Chimney smoke 4 frames; no smoke when inactive.")
def coal(st):
    s = ik.Scene((2, 2), 22)
    kerb_lot(s, "concrete_ground", outer="gravel")
    rect(s, 1.15, 1.0, 1.95, 1.95, M("dirt", ramp="grey", shade=-3), layer=2)
    # boiler house (tall) + turbine hall (gable)
    block(s, 0.15, 0.15, 0.8, 0.6, 46, HALL, roof="flat", roof_mat="roof_gravel")
    block(s, 0.15, 0.75, 0.9, 1.05, 26, HALL, roof="gable", roof_mat="roof_metal", roof_h=10, axis="y",
          gable=M("brick"))
    door(s, 0.3, 1.8, 0.25, 12, "+y", "garage")
    door(s, 1.05, 1.1, 0.2, 9, "+x", "garage")
    s.box(0.95, 0.25, 0, 0.3, 0.35, 20, M("metal", ramp="grey", shade=0))
    # chimney
    top = stack(s, 1.55, 0.45, 0.13, 88, "chimney_bands")
    if not st.inactive:
        smoke(s, 1.55, 0.45, top, st.frame, n=4, ramp="grey", shade=1.5, rise=7, r0=0.1, grow=0.035,
              drift=(0.04, -0.03))
    # coal heap + conveyor up to the boiler house
    heap(s, 1.55, 1.5, 0.36, 13, M("rock", ramp="slate", shade=-3.5), rough=0.45)
    heap(s, 1.3, 1.75, 0.2, 7, M("rock", ramp="slate", shade=-3.5), rough=0.45)
    beam(s, (1.4, 1.25, 4), (0.95, 0.6, 40), P("yellow", 0.5, snow=False), w=0.06, hpx=3)
    pole(s, 1.2, 0.95, 22, P("grey", 0))
    # transformer yard
    for k in range(2):
        s.box(1.25 + k * 0.25, 0.85, 0, 0.12, 0.1, 9, M("metal", ramp="slate"))
    vehicle(s, 1.2, 1.55, "y", "truck", "terra")
    return s


@model("solarplant", (2, 2), "power", "Solar power plant", note="Panel rows tilt toward the light.")
def solar(st):
    s = ik.Scene((2, 2), 23)
    lot(s, "grass_dry")
    rect(s, 0.05, 1.75, 1.95, 1.95, "gravel", layer=1)
    panel = ik.Material("water", -2.5, ik.mat("glass").pattern, dither=0.0, panel=3, frame="slate")
    frame_m = P("grey", 3, snow=False)
    for r in range(5):
        y = 0.2 + r * 0.3
        x0, x1 = 0.12, 1.88 if r > 0 else 1.45
        z0, z1 = 4, 7        # shallow tilt: the panel faces the camera from all four facings
        s.poly([(x0, y + 0.2, z0), (x1, y + 0.2, z0), (x1, y, z1), (x0, y, z1)], panel, outward=(0, 1, 2))
        s.box(x0, y + 0.2, 0, x1 - x0, 0.01, z0, frame_m)
        for x in (x0 + 0.05, (x0 + x1) / 2, x1 - 0.06):
            s.box(x, y + 0.02, 0, 0.02, 0.02, z1, frame_m)
    block(s, 1.55, 0.08, 0.35, 0.25, 9, M("metal_light"), roof_mat="roof_metal")
    fence(s, [(0.03, 0.03), (1.97, 0.03), (1.97, 1.97), (0.03, 1.97)], 4, closed=True)
    vehicle(s, 1.5, 1.77, "x", "van", "yellow")
    return s


@model("powerplant-gas", (3, 3), "power", "Gas power plant", anim=4, note="Hot exhaust 4 frames.")
def gas(st):
    s = ik.Scene((3, 3), 24)
    kerb_lot(s, "concrete_ground", outer="gravel")
    # turbine hall: long light metal shed with blue band
    block(s, 0.2, 1.3, 1.9, 1.0, 30, SIDING_W, roof="flat", roof_mat="roof_metal", rim=SIDING)
    s.box(0.19, 1.29, 22, 1.92, 1.02, 3, BLUE_BAND)
    door(s, 0.4, 2.3, 0.3, 16, "+y", "garage")
    door(s, 1.4, 2.3, 0.15, 8, "+y", "door_glass")
    # HRSG boxes + stacks
    for i, y in enumerate((0.3, 0.85)):
        s.box(0.3, y, 0, 1.0, 0.4, 36, M("metal", ramp="grey", shade=1.5), top=P("grey", 2))
        top = stack(s, 1.55, y + 0.2, 0.11, 64, M("metal_light", shade=1))
        s.cylinder(1.55, y + 0.2, 52, 0.12, 3, BLUE_BAND)
        beam(s, (1.3, y + 0.2, 30), (1.5, y + 0.2, 30), P("grey", 1), w=0.12, hpx=6)
        if not st.inactive:
            smoke(s, 1.55, y + 0.2, top, (st.frame + 2 * i) % 4, n=3, ramp="snow", shade=-0.5, rise=9, r0=0.06,
                  grow=0.035, drift=(0.03, -0.02))
    # gas tanks + pipe rack
    s.sphere(2.45, 0.55, 18, 0.32, M("metal_light", shade=2))
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        pole(s, 2.45 + 0.25 * math.cos(a), 0.55 + 0.25 * math.sin(a), 12, P("grey", 0))
    hcyl(s, 2.1, 1.2, 6, 0.1, 0.8, M("metal_light", shade=1.5), "x", 12)
    hcyl(s, 2.1, 1.45, 6, 0.1, 0.8, M("metal_light", shade=1.5), "x", 12)
    for y in (1.65, 1.95, 2.25):
        pipe(s, (2.15, y, 8), (2.85, y, 8), 0.035, P("yellow", 0.5))
    for x in (2.2, 2.5, 2.8):
        pole(s, x, 1.95, 8, P("grey", 0))
    # switchyard
    for x in (2.2, 2.55):
        lattice(s, x, 2.45, 0.2, 0.3, 16, P("grey", 2, snow=False), levels=3)
    fence(s, [(0.05, 0.05), (2.95, 0.05), (2.95, 2.95), (0.05, 2.95)], 4, closed=True)
    vehicle(s, 1.75, 2.6, "x", "car", "slate")
    return s


@model("hydro-dam", (3, 3), "power", "Hydroelectric dam", anim=4,
       note="Raised reservoir behind the wall, spillway foam animates. Implementation: needs a river; "
            "art includes its own water.")
def hydro(st):
    s = ik.Scene((3, 3), 25)
    rect(s, 1.2, 0, 3, 0.9, "grass", layer=0)
    rect(s, 1.2, 2.1, 3, 3, "grass", layer=0)
    wz = 30
    # reservoir plateau (x < 1.25), lake on top
    s.box(0, 0, 0, 1.2, 3, wz - 2, M("rock", shade=0), top="grass")
    rect(s, 0.0, 0.25, 1.2, 2.75, "water", layer=3, z=wz - 2)
    # dam wall: battered concrete face toward +x
    conc = M("concrete", shade=1.0, panel_u=12, panel_v=6)
    s.box(1.2, 0, 0, 0.16, 3, wz + 2, conc, top=M("asphalt", shade=1))
    s.poly([(1.36, 0, wz + 2), (1.36, 3, wz + 2), (1.6, 3, 0), (1.6, 0, 0)], conc, outward=(1, 0, 0.5))
    s.poly([(1.36, 3, wz + 2), (1.6, 3, 0), (1.36, 3, 0)], conc, outward=(0, 1, 0))
    for y in (0.02, 2.95):
        s.box(1.2, y, wz + 2, 0.16, 0.03, 2, P("grey", 3))
    for y in (0.5, 1.0, 2.0, 2.5):
        s.box(1.22, y, wz + 2, 0.12, 0.05, 6, P("grey", 1))
        s.box(1.21, y - 0.01, wz + 8, 0.14, 0.07, 1, M("lamp"))
    # spillway chute (middle) with animated foam
    foam = ik.Material("snow", 1.0, None, dither=0.0, snow=False)
    s.poly([(1.37, 1.25, wz + 2.1), (1.37, 1.75, wz + 2.1), (1.62, 1.75, WATER_Z), (1.62, 1.25, WATER_Z)],
           "water", outward=(1, 0, 0.5))
    # river downstream at the natural water level, rocky back bank
    sea(s, 1.6, 0.9, 3.0, 2.1, bank=M("rock", shade=0))
    rect(s, 1.6, 0.82, 3.0, 0.9, "sand", layer=2)
    rect(s, 1.6, 2.1, 3.0, 2.18, "sand", layer=2)
    if not st.inactive:
        for k in range(5):
            t = ((k + st.frame / 4.0) / 5.0)
            x = 1.62 + t * 0.9
            for yy in (1.3, 1.5, 1.7):
                s.ellipsoid(x, yy + 0.04 * ((k + st.frame) % 2), WATER_Z + 0.5, 0.05 + 0.03 * (1 - t), 0.06, 2.5 * (1 - t) + 1,
                            foam, rough=0.4)
    # powerhouse at the foot of the dam
    block(s, 1.6, 0.15, 0.7, 0.6, 18, wall_mat("concrete", win_w=3, win_h=8, period=7, storey=16), rim="concrete")
    block(s, 1.6, 2.25, 0.7, 0.6, 18, wall_mat("concrete", win_w=3, win_h=8, period=7, storey=16), rim="concrete")
    for y in (0.3, 0.55):
        s.box(2.3, y, 0, 0.12, 0.12, 4, P("grey", -1))
    # switchyard + pylon base
    lattice(s, 2.55, 0.2, 0.25, 0.25, 34, P("grey", 2, snow=False), taper=0.08, levels=5)
    for (x, y) in ((2.6, 2.5), (2.8, 2.75), (0.4, 0.12)):
        tree(s, x, y, 18, 0.12, "conifer", season=st.season)
    return s


@model("powerplant-nuclear", (3, 3), "power", "Nuclear power plant", anim=4,
       note="Two cooling towers with steam (4 frames); reactor dome.")
def nuclear(st):
    s = ik.Scene((3, 3), 26)
    kerb_lot(s, "concrete_ground", outer="grass")
    towm = M("concrete", shade=1.5, panel_u=40, panel_v=40)
    prof = [(0, 0.56), (22, 0.44), (46, 0.36), (74, 0.34)]
    for (cx, cy) in ((0.7, 0.7), (0.7, 1.95)):
        lathe(s, cx, cy, prof, towm, segs=28)
        s.ground([(cx + 0.36 * math.cos(a), cy + 0.36 * math.sin(a)) for a in
                  [2 * math.pi * k / 20 for k in range(20)]], P("grey", -3), z=73.5, layer=1)
        s.cylinder(cx, cy, 0, 0.56, 3, P("grey", 0), top=None, caps=False, segs=28)
        if not st.inactive:
            smoke(s, cx, cy, 72, st.frame, n=4, ramp="snow", shade=0.5, rise=11, r0=0.26, grow=0.05,
                  drift=(0.03, -0.03), rough=0.3)
    # reactor building: cylinder + dome
    s.cylinder(2.1, 0.75, 0, 0.42, 36, M("concrete", ramp="stone", shade=2.5, panel_u=60), segs=24)
    s.dome(2.1, 0.75, 36, 0.42, 16, M("concrete", ramp="stone", shade=3, panel_u=60))
    s.cylinder(2.1, 0.75, 30, 0.43, 3, P("red", 0.5))
    # turbine hall + offices
    block(s, 1.55, 1.45, 1.3, 0.8, 26, SIDING_W, roof_mat="roof_metal", rim=SIDING)
    s.box(1.54, 1.44, 20, 1.32, 0.82, 3, P("yellow", 1))
    block(s, 1.55, 2.3, 0.7, 0.55, 2 * ST + 2, wall_mat("office"), rim="concrete")
    door(s, 1.75, 2.85, 0.2, 7, "+y", "door_glass")
    # hazard-striped stack
    stack(s, 2.65, 0.25, 0.07, 64, M("chimney_bands", height=8))
    fence(s, [(0.04, 0.04), (2.96, 0.04), (2.96, 2.96), (0.04, 2.96)], 5, closed=True)
    vehicle(s, 2.4, 2.45, "y", "car", "snow")
    vehicle(s, 2.65, 2.45, "y", "car", "slate")
    return s
