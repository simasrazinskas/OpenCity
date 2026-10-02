"""CIVIC models: police (policebox, station, HQ, prison) and fire (firehouse, station, helipad, firewatch).
Accents: police = slate / dark blue + white, fire = red + white."""
import math

from iso_civic_kit import ik, M, P, ST, rect, lot, kerb_lot, block, door, wall_mat, canopy, letter_h, \
    pole, beam, fence, solid_wall, hcyl, cone, disc, ring, panel
from iso_civic_props import vehicle, bay_row, tree, person, lamp, bench, hedge, flag, flowers
from iso_civic_states import model

# ---------------------------------------------------------------- shared look
NAVY = P("slate", -1.5, snow=False)
BLUE = P("water", -0.5, snow=False)
WHITE = P("snow", 1.0, snow=False)
WHITE_S = P("snow", 0.6)
BLUE_LAMP = M("lamp", ramp="water", shade=1.2, eramp="water", eshade=9)
RED_LAMP = M("lamp", ramp="red", eramp="red", eshade=9)
DARK = P("grey", -4.5, snow=False)

POLICE_WALL = wall_mat("plaster", ramp="snow", shade=-0.8, win_w=4, win_h=5, period=8, sill=3, glass="slate",
                       lit=0.8)
POLICE_PLAIN = M("plaster", ramp="snow", shade=-0.8)
POLICE_ROOF = M("roof_flat", ramp="slate", shade=-1.0)
CELL_WALL = wall_mat("concrete", win_w=2, win_h=4, period=5, sill=4, margin=2, glass="slate", lit=0.5)
HQ_WALL = wall_mat("office", ramp="slate", shade=2.2, win_w=5, win_h=6, period=8, sill=2, glass="glass", lit=0.75)

FONT = {
    "P": ("###", "#.#", "###", "#..", "#.."), "O": ("###", "#.#", "#.#", "#.#", "###"),
    "L": ("#..", "#..", "#..", "#..", "###"), "I": ("###", ".#.", ".#.", ".#.", "###"),
    "C": ("###", "#..", "#..", "#..", "###"), "E": ("###", "#..", "##.", "#..", "###"),
    "F": ("###", "#..", "##.", "#..", "#.."), "R": ("##.", "#.#", "##.", "#.#", "#.#"),
    "S": ("###", "#..", "###", "..#", "###"), "N": ("#.#", "###", "###", "#.#", "#.#"),
    "T": ("###", ".#.", ".#.", ".#.", ".#."), "A": (".#.", "#.#", "###", "#.#", "#.#"),
    "H": ("#.#", "#.#", "###", "#.#", "#.#"), " ": ("..", "..", "..", "..", ".."),
}


def text_width(txt):
    return sum(len(FONT[c][0]) + 1 for c in txt) - 1


def text_px(s, cx, y, z, txt, mat=WHITE, face="+y"):
    """3x5 pixel lettering on a facade (centre x = cx at facade line y). 1 letter px = 1 screen px."""
    u = 1 / 32.0
    w = text_width(txt)
    x0 = cx - w * u / 2
    off = 0
    for ch in txt:
        g = FONT[ch]
        for r in range(5):
            row = g[r]
            c = 0
            while c < len(row):
                if row[c] == "#":
                    e = c
                    while e < len(row) and row[e] == "#":
                        e += 1
                    xx = x0 + (off + c) * u
                    zz = z + (4 - r)
                    if face == "+y":
                        s.box(xx, y, zz, (e - c) * u, 0.01, 1, mat)
                    else:
                        s.box(y, xx, zz, 0.01, (e - c) * u, 1, mat)
                    c = e
                else:
                    c += 1
        off += len(g[0]) + 1


def roof_sign(s, cx, y, z, txt, bg=BLUE, mat=WHITE, pad=3, h=8):
    """Free-standing roof billboard (blue board, white pixel text) facing +y, standing on the roof at height z."""
    w = (text_width(txt) + 2 * pad) / 32.0
    s.box(cx - w / 2, y - 0.02, z, 0.025, 0.03, 3, P("grey", 2, snow=False))
    s.box(cx + w / 2 - 0.025, y - 0.02, z, 0.025, 0.03, 3, P("grey", 2, snow=False))
    s.box(cx - w / 2, y - 0.015, z + 3, w, 0.02, h, bg, top=bg)
    text_px(s, cx, y + 0.005, z + 3 + (h - 5) // 2 + 0, txt, mat)


def band(s, x, y, dx, dy, z, h=2, mat=WHITE_S):
    s.box(x - 0.01, y - 0.01, z, dx + 0.02, dy + 0.02, h, mat)


def blue_lamp(s, x, y, z, night=False):
    """Police blue lamp on a bracket / roof pole."""
    s.box(x - 0.03, y - 0.03, z, 0.06, 0.06, 4, BLUE_LAMP)
    s.box(x - 0.035, y - 0.035, z - 1, 0.07, 0.07, 1, WHITE)
    s.box(x - 0.035, y - 0.035, z + 4, 0.07, 0.07, 1, WHITE)


def lightbar(s, x, y, dx, dy, z):
    s.box(x, y, z, dx, dy, 2, BLUE_LAMP)


def _h_on(s, cx, cy, z, size):
    a, t = size / 2, size * 0.16
    s.box(cx - a, cy - a, z, t, size, 0.4, WHITE)
    s.box(cx + a - t, cy - a, z, t, size, 0.4, WHITE)
    s.box(cx - a, cy - t / 2, z, size, t, 0.4, WHITE)


def helipad_roof(s, cx, cy, r, z, label="H"):
    s.cylinder(cx, cy, z, r, 2, P("grey", 1), top=M("concrete_ground", shade=-0.8), segs=20)
    s.cylinder(cx, cy, z + 2, r * 0.88, 0.2, WHITE, top=WHITE, segs=20)
    s.cylinder(cx, cy, z + 2.2, r * 0.8, 0.2, M("asphalt", shade=1), top=M("asphalt", shade=1), segs=20)
    _h_on(s, cx, cy, z + 2.4, r * 0.8)


def bay_hall(s, x, y, dx, dy, h, bays, door_h, wall, upper, roof_mat, rim=None, back=0.1, interior=DARK, parapet=2):
    """Building front (+y) with real open bays you can see into. `bays` = [(x0, width), ...] sorted, absolute x.
    Piers (below door_h) use `wall`, a full-width upper floor uses `upper` (windowed wall). Dark back wall,
    side walls and floor inside each bay. Works in all 4 facings."""
    cur = x
    for (bx, bw) in bays:
        if bx > cur + 1e-6:
            s.box(cur, y, 0, bx - cur, dy, door_h, wall)
        cur = bx + bw
        s.box(bx, y, 0, bw, back, door_h, interior)
        rect(s, bx, y + back, bx + bw, y + dy, P("grey", -2.5, snow=False), layer=4)
        e = 0.004
        s.poly([(bx + e, y + back, 0), (bx + e, y + dy, 0), (bx + e, y + dy, door_h), (bx + e, y + back, door_h)],
               interior, outward=(1, 0, 0))
        s.poly([(bx + bw - e, y + back, 0), (bx + bw - e, y + dy, 0), (bx + bw - e, y + dy, door_h),
                (bx + bw - e, y + back, door_h)], interior, outward=(-1, 0, 0))
    if x + dx > cur + 1e-6:
        s.box(cur, y, 0, x + dx - cur, dy, door_h, wall)
    s.box(x, y, door_h, dx, dy, h - door_h, upper, top=roof_mat)
    s.roof_flat(x, y, h, dx, dy, roof_mat, parapet=parapet, rim=rim or wall)


# ---------------------------------------------------------------- police
@model("policebox", (1, 1), "police", "Police box", front=True,
       note="Small blue kiosk with a blue lamp, white POLICE band.")
def policebox(st):
    s = ik.Scene((1, 1), 41)
    lot(s, "grass")
    rect(s, 0.04, 0.4, 0.96, 0.96, "paving", layer=1)
    rect(s, 0.2, 0.05, 0.8, 0.45, "paving", layer=1)
    kx, ky, kd = 0.3, 0.28, 0.4
    s.box(kx - 0.02, ky - 0.02, 0, kd + 0.04, kd + 0.04, 2, M("concrete"))            # plinth
    s.box(kx, ky, 2, kd, kd, 20, M("windows_plaster", ramp="water", shade=-0.2, win_w=4, win_h=6, period=8,
                                  sill=6, storey=20, margin=3, glass="glass", lit=0.9),
          top=M("roof_flat", ramp="slate"))
    s.box(kx - 0.03, ky - 0.03, 20, kd + 0.06, kd + 0.06, 2, P("slate", -2.5))
    s.roof_hip(kx - 0.03, ky - 0.03, 22, kd + 0.06, kd + 0.06, 7, M("roof_tiles", ramp="slate", shade=-1.5))
    band(s, kx, ky, kd, kd, 15, 2, WHITE_S)
    band(s, kx, ky, kd, kd, 2, 1.5, WHITE_S)
    door(s, kx + 0.1, ky + kd, 0.14, 11, "+y", "door_glass", z=2)
    panel(s, kx + kd * 0.5, ky + kd, 15, 0.34, 6, WHITE, t=0.025)
    panel(s, kx + kd * 0.5, ky + kd + 0.02, 16, 0.3, 4, P("water", -1.5, snow=False), t=0.012)
    blue_lamp(s, kx + kd / 2, ky + kd / 2, 29)
    person(s, 0.75, 0.7, "water", "slate")
    bench(s, 0.62, 0.9, "x")
    tree(s, 0.85, 0.22, 20, 0.12, season=st.season, seed=5)
    tree(s, 0.12, 0.78, 14, 0.09, "conifer", season=st.season)
    lamp(s, 0.14, 0.45, 12)
    return s


@model("police", (2, 2), "police", "Police station", front=True, anim=4,
       ups=(("garage", "police garage"), ("cells", "cell block")),
       note="Flag waves (4 frames). Garage and cell block are drawn inside the same 2x2.")
def police(st):
    s = ik.Scene((2, 2), 42)
    lot(s, "grass")
    rect(s, 0.05, 1.22, 1.95, 1.95, "paving", layer=1)
    rect(s, 1.1, 1.3, 1.95, 1.95, "asphalt", layer=2)
    # main block, 2 storeys, white band, parapet roof
    h = 2 * ST + 2
    block(s, 0.15, 0.45, 1.05, 0.77, h, POLICE_WALL, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
    band(s, 0.15, 0.45, 1.05, 0.77, h - 3, 2, BLUE)
    band(s, 0.15, 0.45, 1.05, 0.77, 1, 1.5, WHITE_S)
    # entrance: glass door, blue canopy, blue lamp
    door(s, 0.48, 1.22, 0.24, 9, "+y", "door_glass")
    canopy(s, 0.4, 1.22, 0.4, 0.2, 10, BLUE)
    pole(s, 0.88, 1.27, 10, P("grey", 2, snow=False), w=0.018)
    blue_lamp(s, 0.88, 1.27, 10)
    # roof billboard: blue board with white POLICE
    roof_sign(s, 0.62, 1.12, h, "POLICE")
    s.box(0.25, 0.55, h, 0.18, 0.14, 4, "metal_light")
    # car park: patrol cars
    bay_row(s, 1.15, 1.58, 3, 0.26, "x", "police", ("slate", "snow", None))
    vehicle(s, 1.65, 1.35, "x", "police", "snow")
    vehicle(s, 0.2, 1.5, "x", "car", "terra")
    flag(s, 0.12, 1.3, 30, "water", st.frame, ramp2="snow")
    person(s, 0.95, 1.4, "water", "slate")
    lamp(s, 1.02, 1.9, 12)
    # upgrade: garage wing (back right) / lawn
    if "garage" in st.ups:
        block(s, 1.3, 0.3, 0.6, 0.62, ST + 4, POLICE_PLAIN, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
        band(s, 1.3, 0.3, 0.6, 0.62, ST + 1, 1.5, WHITE_S)
        for i in range(2):
            door(s, 1.38 + i * 0.26, 0.92, 0.22, 9, "+y", "garage")
        rect(s, 1.25, 0.92, 1.95, 1.3, "asphalt", layer=2)
        vehicle(s, 1.5, 1.0, "y", "police", "slate")
    else:
        for (x, y, hh) in ((1.5, 0.5, 22), (1.75, 0.85, 16)):
            tree(s, x, y, hh, 0.13, season=st.season, seed=int(x * 10))
        bench(s, 1.4, 1.02, "x")
    # upgrade: cell block (back)
    if "cells" in st.ups:
        block(s, 0.15, 0.06, 1.05, 0.38, 4 * ST + 2, CELL_WALL, rim="concrete", roof_mat=POLICE_ROOF)
        band(s, 0.15, 0.06, 1.05, 0.38, 4 * ST, 2, P("water", -1, snow=False))
    else:
        tree(s, 0.3, 0.25, 22, 0.12, "conifer", season=st.season)
        tree(s, 0.65, 0.22, 18, 0.12, season=st.season, seed=3)
        tree(s, 1.0, 0.25, 22, 0.12, "conifer", season=st.season)
    return s


@model("police-hq", (3, 3), "police", "Police headquarters", front=True,
       ups=(("garage", "police garage"), ("cells", "cell block")),
       note="Tall glass tower over a podium, roof helipad (no helicopter: LIFE), car park.")
def police_hq(st):
    s = ik.Scene((3, 3), 43)
    lot(s, "grass")
    rect(s, 0.05, 2.2, 3.0, 2.97, "paving", layer=1)
    rect(s, 1.95, 1.4, 2.97, 2.97, "asphalt", layer=2)
    # tower (6 storeys, glass) behind a 2-storey podium
    th = 6 * ST + 2
    block(s, 0.2, 0.3, 1.3, 1.0, th, HQ_WALL, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
    for k in (2, 4):
        band(s, 0.2, 0.3, 1.3, 1.0, k * ST + 1, 1.5, WHITE_S)
    band(s, 0.2, 0.3, 1.3, 1.0, th - 3, 3, BLUE)
    # antenna mast + blue beacon on the tower roof
    pole(s, 0.4, 0.5, 14, P("grey", 3, snow=False), w=0.03, z=th)
    s.box(0.37, 0.47, th + 14, 0.06, 0.06, 3, BLUE_LAMP)
    s.box(1.1, 0.5, th, 0.22, 0.2, 4, "metal_light")
    # podium
    ph = 2 * ST + 4
    block(s, 0.2, 1.3, 1.75, 0.9, ph, POLICE_WALL, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
    band(s, 0.2, 1.3, 1.75, 0.9, ph - 3, 2.5, BLUE)
    band(s, 0.2, 1.3, 1.75, 0.9, 1, 1.5, WHITE_S)
    # roof helipad on the podium
    helipad_roof(s, 1.55, 1.78, 0.28, ph)
    # roof sign over the entrance
    roof_sign(s, 0.78, 2.12, ph, "POLICE", h=9, pad=4)
    # entrance: glass doors, deep canopy, flags
    door(s, 0.62, 2.2, 0.36, 10, "+y", "door_glass")
    canopy(s, 0.5, 2.2, 0.6, 0.3, 12, BLUE)
    blue_lamp(s, 0.4, 2.4, 14)
    pole(s, 0.4, 2.4, 14, P("grey", 2, snow=False), w=0.02)
    flag(s, 0.14, 2.45, 38, "water", st.frame, ramp2="snow")
    flag(s, 0.28, 2.6, 34, "snow", st.frame + 2, ramp2="water")
    # forecourt + car park
    bay_row(s, 2.0, 2.45, 3, 0.3, "x", "police", ("snow", "slate", "snow"), depth=0.4)
    vehicle(s, 2.3, 2.0, "x", "police", "slate")
    vehicle(s, 2.3, 1.75, "x", "police", "snow")
    vehicle(s, 0.95, 2.62, "x", "car", "terra")
    person(s, 1.35, 2.5, "water", "slate")
    person(s, 1.5, 2.45, "snow", "slate")
    lamp(s, 1.85, 2.4, 14)
    lamp(s, 1.9, 2.9, 14)
    tree(s, 0.12, 1.9, 16, 0.1, season=st.season, seed=2)
    tree(s, 0.1, 2.85, 14, 0.1, "conifer", season=st.season)
    # upgrade: garage (back right)
    if "garage" in st.ups:
        block(s, 2.0, 0.4, 0.9, 0.8, ST + 6, POLICE_PLAIN, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
        band(s, 2.0, 0.4, 0.9, 0.8, ST + 3, 1.5, BLUE)
        for i in range(3):
            door(s, 2.06 + i * 0.28, 1.2, 0.24, 10, "+y", "garage")
        rect(s, 1.95, 1.2, 2.97, 1.42, "asphalt", layer=2)
        vehicle(s, 2.2, 1.24, "x", "police", "slate")
    else:
        for (x, y, hh) in ((2.25, 0.55, 22), (2.7, 0.8, 18), (2.4, 1.05, 16)):
            tree(s, x, y, hh, 0.14, season=st.season, seed=int(x * 10))
        bench(s, 2.7, 1.1, "x")
    # upgrade: cell block (between tower and car park)
    if "cells" in st.ups:
        block(s, 1.5, 0.35, 0.4, 0.95, 4 * ST + 2, CELL_WALL, rim="concrete", roof_mat=POLICE_ROOF)
        band(s, 1.5, 0.35, 0.4, 0.95, 4 * ST, 2, BLUE)
    else:
        tree(s, 1.7, 0.6, 20, 0.12, season=st.season, seed=6)
        hedge(s, 1.55, 1.1, 0.3, 0.06, 4, st.season)
    return s


def watchtower(s, x, y, night=False, h=24):
    """Prison guard tower: concrete shaft, glazed cab with hip roof and a searchlight."""
    s.box(x, y, 0, 0.14, 0.14, h, M("concrete", shade=0.8), top=P("grey", 2))
    s.box(x - 0.04, y - 0.04, h, 0.22, 0.22, 2, P("grey", 1))
    s.box(x - 0.03, y - 0.03, h + 2, 0.2, 0.2, 7, M("glass", shade=-0.5), top=P("grey", 1))
    s.roof_hip(x - 0.06, y - 0.06, h + 9, 0.26, 0.26, 6, M("roof_tiles", ramp="slate", shade=-1.0))
    s.box(x + 0.14, y + 0.14, h + 9, 0.04, 0.04, 2, M("lamp", ramp="snow", eramp="yellow", eshade=11))


@model("prison", (3, 3), "police", "Prison",
       ups=(("cells", "cell block"),),
       note="Perimeter wall, four watchtowers, gatehouse, two cell blocks, exercise yard; third cell block as upgrade.")
def prison(st):
    s = ik.Scene((3, 3), 44)
    lot(s, "grass")
    rect(s, 0.06, 0.06, 2.94, 2.94, "concrete_ground", layer=1)
    rect(s, 0.2, 1.3, 1.4, 2.75, M("asphalt", shade=0.5), layer=2)               # exercise yard
    rect(s, 1.6, 2.5, 2.85, 2.9, "paving", layer=2)                              # forecourt / road from the gate
    wall = M("concrete", shade=1.2)
    # perimeter: solid back, left and right walls, solid front-right; chain fence on the front-left (see in)
    solid_wall(s, [(0.1, 0.1), (2.9, 0.1)], 11, 0.07, wall)
    solid_wall(s, [(0.1, 0.1), (0.1, 2.9)], 11, 0.07, wall)
    solid_wall(s, [(2.9, 0.1), (2.9, 2.9)], 11, 0.07, wall)
    solid_wall(s, [(2.3, 2.9), (2.9, 2.9)], 11, 0.07, wall)
    fence(s, [(0.1, 2.9), (1.45, 2.9)], 11, P("grey", 3, snow=False))
    # gatehouse with a real sally port
    bay_hall(s, 1.45, 2.6, 0.85, 0.32, 20, [(1.7, 0.35)], 11, wall, M("windows_plaster", ramp="snow", shade=-0.8, win_w=3,
             win_h=4, period=7, sill=2, storey=9, glass="slate", lit=0.6), POLICE_ROOF, rim=POLICE_PLAIN, back=0.05)
    band(s, 1.45, 2.6, 0.85, 0.32, 17, 2, BLUE)
    roof_sign(s, 1.86, 2.84, 20, "PRISON", h=8, pad=3)
    fence(s, [(1.7, 2.8), (2.05, 2.8)], 11, P("grey", 3, snow=False))
    # cell blocks
    ch = 3 * ST + 2
    for (x0, x1) in ((0.3, 1.45), (1.6, 2.75)):
        block(s, x0, 0.25, x1 - x0, 0.7, ch, CELL_WALL, rim="concrete", roof_mat=POLICE_ROOF)
        band(s, x0, 0.25, x1 - x0, 0.7, ch - 2, 2, BLUE)
        band(s, x0, 0.25, x1 - x0, 0.7, 0.5, 1.5, WHITE_S)
        door(s, (x0 + x1) / 2 - 0.08, 0.95, 0.16, 8, "+y", "garage")
        s.box(x1 - 0.25, 0.4, ch, 0.12, 0.12, 4, "metal_light")
    # admin / visitor block
    block(s, 1.7, 1.55, 1.0, 0.6, 2 * ST + 2, POLICE_WALL, rim=POLICE_PLAIN, roof_mat=POLICE_ROOF)
    band(s, 1.7, 1.55, 1.0, 0.6, 2 * ST - 1, 2, BLUE)
    door(s, 2.1, 2.15, 0.2, 9, "+y", "door_glass")
    canopy(s, 2.02, 2.15, 0.36, 0.14, 10, BLUE)
    # cell block upgrade between the cell blocks and the admin building, else a garden
    if "cells" in st.ups:
        block(s, 1.7, 1.05, 1.0, 0.42, ch, CELL_WALL, rim="concrete", roof_mat=POLICE_ROOF)
        band(s, 1.7, 1.05, 1.0, 0.42, ch - 2, 2, BLUE)
    else:
        rect(s, 1.65, 1.05, 2.75, 1.5, M("meadow", ramp="leaf", shade=0.0, flowers=0.0), layer=3)
        rect(s, 1.8, 1.15, 2.6, 1.22, M("dirt"), layer=4)
        rect(s, 1.8, 1.32, 2.6, 1.39, M("dirt"), layer=4)
        tree(s, 2.65, 1.45, 14, 0.1, season=st.season, seed=3)
    # yard: inner fence, hoop, bench, inmates
    fence(s, [(0.2, 1.3), (1.4, 1.3), (1.4, 2.75), (0.2, 2.75), (0.2, 1.3)], 6, P("grey", 3, snow=False))
    pole(s, 0.45, 2.0, 12, P("grey", 2, snow=False), w=0.03)
    s.box(0.45, 1.93, 12, 0.02, 0.14, 4, WHITE)
    bench(s, 0.8, 2.6, "x")
    for (x, y, shirt) in ((0.6, 1.7, "terra"), (0.9, 1.95, "terra"), (1.1, 2.3, "yellow"), (0.7, 2.4, "terra")):
        person(s, x, y, shirt, "terra")
    # towers, vehicles, lamps
    watchtower(s, 0.14, 0.14, st.night)
    watchtower(s, 2.72, 0.14, st.night)
    watchtower(s, 2.72, 2.72, st.night)
    watchtower(s, 0.14, 2.72, st.night)
    vehicle(s, 2.4, 2.45, "x", "van", "slate")
    vehicle(s, 2.4, 2.68, "x", "police", "snow")
    lamp(s, 1.55, 2.4, 14)
    lamp(s, 2.82, 2.0, 14)
    return s


# ---------------------------------------------------------------- fire
RED_W = P("red", 1.2, snow=False)
FIRE_WALL = M("brick", ramp="red", shade=-0.6)
FIRE_UPPER = wall_mat("brick", ramp="red", shade=-0.6, win_w=4, win_h=5, period=8, sill=3, glass="water", lit=0.8,
                      storey=11)
FIRE_ROOF = M("roof_flat", ramp="grey", shade=-0.5)


def hydrant(s, x, y):
    s.cylinder(x, y, 0, 0.025, 4, P("red", 1.5, snow=False), top=P("red", 2, snow=False), segs=8)
    s.box(x - 0.04, y - 0.01, 2, 0.08, 0.02, 1.5, P("red", 1.5, snow=False))


def hose_tower(s, x, y, dx, dy, z0, h, night=False):
    """Brick hose-drying tower with louvred belfry and a hip roof."""
    s.box(x, y, z0, dx, dy, h, wall_mat("brick", ramp="red", shade=-0.6, win_w=2, win_h=6, period=5, sill=3,
                                          margin=2, storey=13, glass="slate", lit=0.7), top=P("grey", 1))
    band(s, x, y, dx, dy, z0 + h - 3, 3, WHITE_S)
    s.roof_hip(x - 0.02, y - 0.02, z0 + h, dx + 0.04, dy + 0.04, 9, M("roof_tiles", ramp="slate", shade=-1.5))
    s.box(x + dx / 2 - 0.012, y + dy / 2 - 0.012, z0 + h + 9, 0.024, 0.024, 4, P("grey", 3, snow=False))


@model("firehouse", (1, 1), "fire", "Firehouse", front=True,
       note="One open red bay with the engine inside, hose tower, FIRE sign on the roof.")
def firehouse(st):
    s = ik.Scene((1, 1), 51)
    lot(s, "grass")
    rect(s, 0.04, 0.8, 0.96, 0.97, "concrete_ground", layer=1)
    rect(s, 0.06, 0.72, 0.64, 0.97, "concrete_ground", layer=1)
    h = 2 * ST + 4
    bay_hall(s, 0.1, 0.12, 0.7, 0.7, h, [(0.16, 0.34)], 13, FIRE_WALL, FIRE_UPPER, FIRE_ROOF, back=0.1)
    band(s, 0.1, 0.12, 0.7, 0.7, 12, 1.5, WHITE_S)          # white lintel band over the opening
    s.box(0.16, 0.8, 11, 0.34, 0.03, 2, RED_W)                  # roll-up door coil, rolled up
    vehicle(s, 0.245, 0.47, "y", "fireengine", "red", L=0.44)
    hose_tower(s, 0.56, 0.6, 0.22, 0.25, 0, 52, st.night)
    roof_sign(s, 0.285, 0.78, h, "FIRE", bg=P("red", 0.2, snow=False), mat=WHITE, h=8, pad=1)
    hydrant(s, 0.7, 0.9)
    person(s, 0.52, 0.9, "yellow", "slate")
    lamp(s, 0.1, 0.9, 11)
    tree(s, 0.9, 0.3, 18, 0.1, season=st.season, seed=3)
    tree(s, 0.88, 0.55, 16, 0.09, "conifer", season=st.season)
    return s


@model("firestation", (2, 2), "fire", "Fire station", front=True,
       ups=(("bay", "engine bay"),),
       note="Three open red bays with engines, drill tower at the back; the engine bay upgrade adds a fourth.")
def firestation(st):
    s = ik.Scene((2, 2), 52)
    lot(s, "grass")
    rect(s, 0.05, 1.2, 1.95, 1.95, "concrete_ground", layer=1)
    rect(s, 0.1, 1.24, 1.5, 1.3, "marking_yellow", layer=2)
    h = 2 * ST + 4
    bays = [(0.2, 0.3), (0.58, 0.3), (0.96, 0.3)]
    bay_hall(s, 0.12, 0.4, 1.3, 0.8, h, bays, 13, FIRE_WALL, FIRE_UPPER, FIRE_ROOF, back=0.1)
    band(s, 0.12, 0.4, 1.3, 0.8, 12, 1.5, WHITE_S)
    for (bx, bw) in bays:
        s.box(bx, 1.17, 11, bw, 0.03, 2, RED_W)               # rolled-up door coils
    for k, (bx, bw) in enumerate(bays):
        vehicle(s, bx + (bw - 0.15) / 2, 0.8, "y", "fireengine" if k != 2 else "truck", "red" if k != 2 else "snow",
                L=0.46)
    roof_sign(s, 0.62, 1.12, h, "FIRE", bg=P("red", 0.2, snow=False), mat=WHITE, h=8, pad=2)
    # drill / hose tower at the back right
    hose_tower(s, 1.55, 0.18, 0.3, 0.3, 0, 62, st.night)
    pole(s, 1.7, 0.33, 16, P("grey", 3, snow=False), w=0.02, z=62 + 9)
    # forecourt: parked car, firefighters, hydrants, flag
    vehicle(s, 0.2, 1.55, "x", "car", "red")
    hydrant(s, 1.15, 1.38)
    person(s, 0.7, 1.45, "yellow", "slate")
    person(s, 0.85, 1.38, "yellow", "slate")
    flag(s, 0.1, 1.4, 30, "red", st.frame, ramp2="snow")
    lamp(s, 1.0, 1.9, 12)
    lamp(s, 1.95, 1.9, 12)
    tree(s, 0.1, 0.25, 22, 0.12, "conifer", season=st.season)
    tree(s, 0.4, 0.22, 20, 0.12, season=st.season, seed=2)
    if "bay" in st.ups:
        bay_hall(s, 1.5, 0.55, 0.42, 0.65, h, [(1.55, 0.3)], 13, FIRE_WALL, FIRE_UPPER, FIRE_ROOF, back=0.1)
        band(s, 1.5, 0.55, 0.42, 0.65, 12, 1.5, WHITE_S)
        s.box(1.55, 1.17, 11, 0.3, 0.03, 2, RED_W)
        vehicle(s, 1.625, 0.8, "y", "fireengine", "red", L=0.46)
    else:
        for (x, y, hh) in ((1.7, 0.95, 20), (1.8, 0.7, 16)):
            tree(s, x, y, hh, 0.12, season=st.season, seed=int(x * 9))
        hydrant(s, 1.55, 1.1)
    return s


@model("fire-helipad", (2, 2), "fire", "Fire helipad",
       note="Pad and hangar only: the helicopter is LIFE's.")
def fire_helipad(st):
    s = ik.Scene((2, 2), 53)
    lot(s, "grass")
    rect(s, 0.05, 0.05, 1.95, 1.95, "concrete_ground", layer=1)
    # pad: raised disc, white rim, red ring, red H
    cx, cy, r = 0.62, 1.32, 0.52
    s.cylinder(cx, cy, 0, r, 3, P("grey", 0.5), top=M("asphalt", shade=-1.0), segs=24)
    s.cylinder(cx, cy, 3, r * 0.92, 0.2, WHITE, top=WHITE, segs=24)
    s.cylinder(cx, cy, 3.2, r * 0.86, 0.2, M("asphalt", shade=-1.0), top=M("asphalt", shade=-1.0), segs=24)
    s.cylinder(cx, cy, 3.4, r * 0.62, 0.2, RED_W, top=RED_W, segs=24)
    s.cylinder(cx, cy, 3.6, r * 0.56, 0.2, M("asphalt", shade=-1.0), top=M("asphalt", shade=-1.0), segs=24)
    a, t = 0.18, 0.06
    for (x0, y0, x1, y1) in ((cx - a, cy - a, cx - a + t, cy + a), (cx + a - t, cy - a, cx + a, cy + a),
                             (cx - a, cy - t / 2, cx + a, cy + t / 2)):
        s.box(x0, y0, 3.8, x1 - x0, y1 - y0, 0.3, P("snow", 1.0, snow=False))
    for k in range(10):
        ang = k * math.pi / 5
        s.box(cx + (r + 0.04) * math.cos(ang) - 0.02, cy + (r + 0.04) * math.sin(ang) - 0.02, 0, 0.04, 0.04, 2,
              RED_LAMP)
    # hangar: gable shed with a big open bay
    bay_hall(s, 1.15, 0.15, 0.75, 0.7, 22, [(1.27, 0.5)], 15, M("siding", ramp="red", shade=-0.4),
             M("siding", ramp="red", shade=-0.4), M("roof_metal", ramp="grey", shade=1.0), rim=WHITE_S, back=0.1)
    band(s, 1.15, 0.15, 0.75, 0.7, 14, 1.5, WHITE_S)
    s.box(1.27, 0.82, 14, 0.5, 0.03, 2, RED_W)
    s.box(1.55, 0.25, 0, 0.18, 0.15, 6, M("metal_light", ramp="red", shade=0.5))        # tool cart in the hangar
    # fuel tank + hose, fire truck, crew, windsock
    s.cylinder(1.75, 1.05, 0, 0.11, 9, M("metal_light", shade=2.0), top=P("grey", 3), segs=14)
    s.cylinder(1.75, 1.05, 3, 0.115, 2, RED_W, segs=14)
    vehicle(s, 1.1, 1.5, "x", "fireengine", "red")
    vehicle(s, 1.1, 1.72, "x", "car", "snow")
    person(s, 1.65, 1.5, "yellow", "slate")
    pole(s, 1.8, 1.82, 16, P("grey", 2, snow=False), w=0.02)
    hcyl(s, 1.8, 1.82, 14, 0.03, 0.15, M("awning", ramp="terra", width=3), "x", 8, caps=False)
    hydrant(s, 1.45, 1.35)
    lamp(s, 0.15, 0.2, 13)
    tree(s, 0.2, 0.55, 18, 0.12, "conifer", season=st.season)
    tree(s, 0.62, 0.35, 20, 0.12, season=st.season, seed=4)
    return s


@model("firewatch", (1, 1), "fire", "Fire watch tower",
       note="Timber lookout tower with a glazed cab among pines.")
def firewatch(st):
    s = ik.Scene((1, 1), 54)
    lot(s, M("forest_floor"))
    wood = P("wood", 0.5)
    wd = P("wood", -0.8, snow=False)
    # four splayed legs, cross braces every 16 px
    zc = 52
    base = ((0.28, 0.28), (0.72, 0.28), (0.28, 0.72), (0.72, 0.72))
    top = ((0.36, 0.36), (0.64, 0.36), (0.36, 0.64), (0.64, 0.64))
    for (bx, by), (tx, ty) in zip(base, top):
        s.box(bx - 0.03, by - 0.03, 0, 0.08, 0.08, 2, M("concrete"))
        beam(s, (bx, by, 2), (tx, ty, zc), wood, w=0.07, hpx=4)
    for k, z in enumerate((14, 28, 42)):
        t_ = z / zc
        lo = [(b[0] + (tt[0] - b[0]) * t_, b[1] + (tt[1] - b[1]) * t_) for b, tt in zip(base, top)]
        z2 = z + 14 if z < 42 else zc
        t2 = z2 / zc
        hi = [(b[0] + (tt[0] - b[0]) * t2, b[1] + (tt[1] - b[1]) * t2) for b, tt in zip(base, top)]
        for i, j in ((0, 1), (1, 3), (2, 3), (0, 2)):
            beam(s, (lo[i][0], lo[i][1], z), (lo[j][0], lo[j][1], z), wd, w=0.03, hpx=2)
            beam(s, (lo[i][0], lo[i][1], z), (hi[j][0], hi[j][1], z2), wd, w=0.018, hpx=1.4)
    # cab: deck, glazed room, red roof
    s.box(0.3, 0.3, zc, 0.4, 0.4, 2, M("wood", shade=1), top=M("wood", shade=1))
    s.box(0.32, 0.32, zc + 2, 0.36, 0.36, 11, M("windows_siding", ramp="wood", shade=1.2, win_w=5, win_h=5, period=7,
                                                 sill=3, storey=11, margin=2, glass="water", lit=0.9),
          top=P("grey", 2))
    s.box(0.3, 0.3, zc + 8, 0.4, 0.4, 1.5, WHITE_S)
    s.roof_hip(0.27, 0.27, zc + 13, 0.46, 0.46, 8, M("roof_tiles", ramp="red", shade=-0.8))
    pole(s, 0.5, 0.5, 9, P("grey", 3, snow=False), w=0.016, z=zc + 21)
    s.box(0.485, 0.485, zc + 30, 0.03, 0.03, 2, RED_LAMP)
    # stairs up the +y face (zig-zag treads) and rail
    beam(s, (0.34, 0.76, 3), (0.64, 0.7, zc - 2), P("wood", 2, snow=False), w=0.04, hpx=2)
    # pines
    for (x, y, h_, c) in ((0.12, 0.2, 30, 0), (0.88, 0.18, 34, 1), (0.08, 0.9, 28, 2), (0.92, 0.88, 32, 3),
                          (0.5, 0.08, 26, 4), (0.2, 0.62, 22, 6), (0.88, 0.45, 26, 7)):
        tree(s, x, y, h_, 0.12, "conifer", season=st.season, seed=c)
    return s
