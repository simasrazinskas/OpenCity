"""CIVIC models: water (water tower, pump house, large pumping station). Accent: water blue + white."""
from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, wall_mat, beam, pole, pipe, \
    fence, hcyl, cone
from iso_civic_props import vehicle, tree, lamp, person, water_rect
from iso_civic_states import model

BLUE = P("water", 1.5, snow=False)
STEEL = P("grey", 2, snow=False)
WHITE = P("snow", 0.8)
PUMPHALL = wall_mat("brick", win_w=4, win_h=8, period=8, sill=4, storey=14, glass="water", lit=0.7)
PUMPSMALL = wall_mat("plaster", ramp="sand", shade=1.0, win_w=3, win_h=5, period=8, sill=5, storey=14,
                     glass="water", lit=0.7)


def _band(s, x, y, dx, dy, z, h=2, mat=BLUE):
    s.box(x - 0.01, y - 0.01, z, dx + 0.02, dy + 0.02, h, mat)


@model("watertower", (1, 1), "water", "Water tower",
       note="Tank on legs; red aircraft-warning beacon lit at night.")
def watertower(st):
    s = ik.Scene((1, 1), 31)
    lot(s, "grass")
    rect(s, 0.1, 0.1, 0.9, 0.9, "concrete_ground", layer=1)
    # legs: four chunky steel legs, X bracing in two bays, footings
    legs = ((0.27, 0.27), (0.71, 0.27), (0.27, 0.71), (0.71, 0.71))
    for (x, y) in legs:
        s.box(x - 0.025, y - 0.025, 0, 0.08, 0.08, 2, M("concrete"))
        s.box(x, y, 2, 0.03, 0.03, 31, STEEL)
    for z0, z1 in ((4, 16), (18, 30)):
        for (a, b) in (((0.285, 0.285), (0.725, 0.285)), ((0.725, 0.285), (0.725, 0.725)),
                       ((0.285, 0.725), (0.725, 0.725)), ((0.285, 0.285), (0.285, 0.725))):
            beam(s, (a[0], a[1], z0), (b[0], b[1], z1), P("grey", 3, snow=False), w=0.02, hpx=1.6)
            beam(s, (a[0], a[1], z1), (b[0], b[1], z0), P("grey", 3, snow=False), w=0.02, hpx=1.6)
    for z in (17, 31):
        s.box(0.27, 0.27, z, 0.47, 0.47, 1.5, STEEL)
    # tank: water-blue drum with white stripe, steel cone roof
    s.cylinder(0.5, 0.5, 33, 0.33, 2, STEEL, top=STEEL)
    s.cylinder(0.5, 0.5, 35, 0.32, 17, M("metal", ramp="water", shade=1.8), top=STEEL)
    s.cylinder(0.5, 0.5, 42, 0.325, 4, WHITE)
    cone(s, 0.5, 0.5, 52, 0.34, 11, M("roof_metal", ramp="grey", shade=2.5), segs=20)
    s.cylinder(0.5, 0.5, 63, 0.03, 4, STEEL)
    # ladder up the +Y side
    for k in range(2):
        pole(s, 0.45 + k * 0.08, 0.76, 34, STEEL, w=0.012)
    for z in range(3, 34, 5):
        s.box(0.45, 0.76, z, 0.09, 0.012, 0.7, STEEL)
    # valve house + inlet
    s.box(0.3, 0.08, 0, 0.18, 0.14, 7, M("metal_light", ramp="water", shade=1.0), top=STEEL)
    door(s, 0.35, 0.22, 0.07, 5, "+y", "door")
    beacon = M("lamp", ramp="red", eramp="red", eshade=9)
    s.box(0.485, 0.485, 66.5, 0.03, 0.03, 2.5, beacon)
    if st.night:
        s.box(0.47, 0.47, 69, 0.06, 0.06, 1, beacon)
    tree(s, 0.88, 0.2, 18, 0.1, season=st.season, seed=1)
    tree(s, 0.14, 0.86, 14, 0.09, "conifer", season=st.season)
    lamp(s, 0.2, 0.7, 12)
    return s


@model("waterpump", (1, 1), "water", "Water pump",
       note="Pump house with an intake pipe running into a small intake pond.")
def waterpump(st):
    s = ik.Scene((1, 1), 32)
    lot(s, "grass")
    rect(s, 0.04, 0.5, 0.96, 0.96, "concrete_ground", layer=1)
    water_rect(s, 0.58, 0.08, 0.95, 0.52, layer=2, wall=M("stone", shade=1), depth=2)
    block(s, 0.1, 0.12, 0.42, 0.42, 20, PUMPSMALL, roof="gable", roof_mat="roof_tiles", roof_h=9, axis="x",
          gable=M("plaster", ramp="sand", shade=1.0))
    _band(s, 0.1, 0.12, 0.42, 0.42, 17, 2)
    door(s, 0.2, 0.54, 0.14, 9, "+y", "door")
    s.box(0.18, 0.54, 10, 0.18, 0.012, 3, BLUE)          # name plate over the door
    hcyl(s, 0.52, 0.32, 5, 0.035, 0.34, P("grey", 1.5), "x", 8)
    for x in (0.64, 0.8):
        pole(s, x, 0.32, 5, STEEL, w=0.03)
    s.cylinder(0.64, 0.32, 5, 0.05, 4, BLUE)             # valve
    s.cylinder(0.64, 0.32, 9, 0.025, 1.5, P("red", 0.5, snow=False))
    s.cylinder(0.84, 0.74, 0, 0.05, 11, M("metal", ramp="water", shade=1.0), top=STEEL)   # standpipe
    s.cylinder(0.84, 0.74, 11, 0.06, 2, STEEL)
    s.box(0.5, 0.64, 0, 0.18, 0.14, 4, M("wood", shade=0.5))      # pallet of pipe sections
    pipe(s, (0.52, 0.68, 6), (0.68, 0.68, 6), 0.03, P("grey", 1.5))
    s.box(0.12, 0.8, 0, 0.14, 0.12, 5, M("metal_light", ramp="water"), top=STEEL)
    person(s, 0.42, 0.7, "water")
    tree(s, 0.9, 0.62, 15, 0.1, season=st.season, seed=3)
    tree(s, 0.06, 0.2, 14, 0.09, season=st.season, seed=4)
    lamp(s, 0.38, 0.9, 11)
    return s


@model("waterpump-large", (2, 2), "water", "Pumping station",
       note="Pump hall, two open settling basins, intake pipes and a chemical store.")
def waterpump_large(st):
    s = ik.Scene((2, 2), 33)
    kerb_lot(s, "concrete_ground", outer="grass")
    # settling basins: two walled tanks of water in front of the hall
    for (x0, x1) in ((0.18, 0.9), (1.08, 1.8)):
        s.box(x0 - 0.03, 1.1, 0, x1 - x0 + 0.06, 0.8, 4, M("concrete", shade=1.5), top="concrete")
        rect(s, x0, 1.14, x1, 1.86, "water", layer=3, z=4)
        for k in range(1, 3):                                   # baffle walls
            s.box(x0 + (x1 - x0) * k / 3 - 0.01, 1.16, 4, 0.02, 0.5, 1.5, P("snow", -1))
    # pump hall: long brick shed, blue band
    block(s, 0.15, 0.15, 1.35, 0.7, 28, PUMPHALL, roof="gable", roof_mat=M("roof_metal", ramp="water", shade=1.0), roof_h=11, axis="x",
          gable=M("brick"))
    _band(s, 0.15, 0.15, 1.35, 0.7, 24, 2)
    for x in (0.28, 0.58):
        door(s, x, 0.85, 0.2, 14, "+y", "garage")
    for x in (0.4, 0.75, 1.1):
        s.box(x, 0.4, 39, 0.1, 0.1, 3, STEEL)
    # chemical store
    block(s, 1.55, 0.15, 0.3, 0.7, 20, wall_mat("concrete", win_w=3, win_h=5, period=8, storey=12, glass="water"),
          rim="concrete")
    # intake pipes from the hall to the basins
    for x in (0.4, 0.7):
        hcyl(s, x, 0.86, 3, 0.03, 0.24, P("grey", 1), "y", 8)
    vehicle(s, 1.1, 0.92, "y", "van", "water")
    person(s, 1.35, 0.95, "water")
    for (x, y, h) in ((0.08, 1.9, 16), (1.92, 1.9, 18), (1.92, 1.0, 16)):
        tree(s, x, y, h, 0.1, season=st.season, seed=int(x * 7))
    lamp(s, 0.08, 1.0, 13)
    fence(s, [(0.03, 0.03), (1.97, 0.03), (1.97, 1.97), (0.03, 1.97)], 4, closed=True)
    return s
