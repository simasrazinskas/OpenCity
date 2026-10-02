"""iso_zoned_resrow.py - row houses / terraces (ZONED).

Two narrow houses per cell of frontage. The terrace runs the full lot width (x = 0..W) at a fixed
setback with flush party walls, so neighbouring row lots join into one continuous street front.
Families: v0 NA brownstones (flat roof, cornice, stoops), v1 NA painted townhouses (street gables,
bay windows), v2 EU brick terrace (continuous gable roof, party-wall chimneys), v3 EU canal houses
(pastel, individual stepped/neck gables). Levels: 2 storeys -> 3 storeys -> bay windows, dormers,
roof terraces, front gardens.
"""
import isokit as ik

import iso_zoned_props as P
from iso_zoned_res import DOORS

ST = ik.STOREY
SETBACK = 0.16
PASTEL = [("rose", 3.5), ("yellow", 2.5), ("glass", 3.0), ("teal", 3.5), ("sand", 3.0), ("snow", 0.0),
          ("terra", 3.5), ("olive", 3.5)]


def cornice(lot, x, y, z, dx, dy, mat):
    lot.solid(x - 0.01, y - 0.01, z, dx + 0.02, dy + 0.03, 2, mat)


def stoop(lot, x, y, w=0.08):
    for i in range(3):
        lot.solid(x, y + i * 0.025, 0, w, 0.025, 3 - i, "stone")


def bay(lot, x, y, z, w, h, wall):
    m = lot.fac(wall, win_w=3, win_h=5, period=5, margin=1, frame="snow")
    lot.solid(x, y, z, w, 0.06, h, m, top=lot.m("roof_metal"))


def res_row(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    floors = 2 if L <= 2 else 3
    if fam == 3 and L >= 4:
        floors = 4
    depth = 0.42 if D == 1 else (0.5 if D == 2 else 0.58)
    y1 = D - SETBACK
    y0 = y1 - depth
    n = 2 * W
    uw = W / n
    lot.lawn("grass")
    # front strip: paving for stoops/front gardens
    lot.pave(0, y1, W, SETBACK, "paving" if fam != 2 else "grass", layer=1)
    # shared look of this variant
    base_brick = ik.mat("brick", shade=lot.r(-0.6, 0.2) if fam != 0 else -1.0,
                        ramp="wood" if fam == 0 and lot.chance(0.5) else "brick")
    roof = lot.pick(["slate", "roof_tiles", "roof_tiles_brown"]) if fam != 0 else ik.mat("roof_flat", shade=-1.5)
    doors = [lot.pick(DOORS) for _ in range(n)]
    cols = [lot.pick(PASTEL) for _ in range(n)]
    h = floors * ST + 1
    for i in range(n):
        x = i * uw
        if fam == 0:
            wall = base_brick
        elif fam == 2:
            wall = base_brick if tier < 3 or i % 2 == 0 else ik.mat("brick", shade=0.6)
        else:
            r, sh = cols[i]
            wall = ik.mat("siding" if fam == 1 else "plaster", ramp=r, shade=sh)
        door_at = 0.25 if i % 2 == 0 else 0.75
        uh = h + (ST if (fam == 3 and i % 3 == 1 and L >= 3) else 0)
        front = lot.fac(wall, doors=[door_at], door=doors[i], door_w=4, period=6, margin=2,
                        frame="snow", shutter=None)
        side = lot.fac(wall, period=6)
        back = lot.fac(wall, doors=[0.5], door="wood", period=6, margin=2)
        lot.block(x, y0, 0, uw, depth, uh, front, side, back)
        dxp = x + uw * door_at
        if fam == 0:
            stoop(lot, dxp - 0.04, y1, 0.08)
            cornice(lot, x, y0, uh - 1, uw, depth, lot.m("stone", shade=1.5))
            lot.flat(x, y0, uh + 1, uw, depth, roof, parapet=0)
            if L >= 3:
                bay(lot, x + uw * (0.6 if i % 2 == 0 else 0.05), y1, ST + 1, uw * 0.35, (floors - 1) * ST - 1, wall)
        elif fam == 1:
            lot.gable(x + 0.01, y0, uh, uw - 0.02, depth, 12, lot.pick(["shingle", "slate"]), axis="y",
                      overhang=0.02, gable=lot.fac(wall, floors=1, win_w=3, period=6, ground=2, sill=1))
            lot.solid(dxp - 0.05, y1, 0, 0.1, 0.05, 2, "wood")
            if L >= 3:
                bay(lot, x + uw * (0.55 if i % 2 == 0 else 0.1), y1, 1, uw * 0.35, floors * ST - 2, wall)
        elif fam == 3:
            # canal house: neck gable facing the street
            lot.gable(x + 0.02, y0, uh, uw - 0.04, depth, 14, "slate" if i % 2 else "roof_tiles", axis="y",
                      overhang=0.0, gable=lot.fac(wall, floors=1, win_w=3, period=6, ground=3, sill=1))
            lot.solid(x + uw * 0.3, y1 - 0.03, uh + 9, uw * 0.4, 0.03, 4, lot.m(wall))
        # front garden / railing
        if fam in (2, 3) and L >= 2:
            P.fence(lot, x + 0.02, D - 0.04, uw - 0.04, 0.02, "rail" if fam == 3 else "brick")
            if L >= 3:
                P.bush(lot, x + uw * (0.75 if i % 2 == 0 else 0.25), y1 + 0.07, 0.03)
        # back garden fences between units
        if D >= 1 and i > 0 and L >= 2:
            P.fence(lot, x - 0.01, 0.03, 0.02, y0 - 0.05, "rail")
    if fam == 2:
        # continuous terrace roof, flush ends, chimney stacks on party walls
        lot.gable(0, y0, h, W, depth, 13, roof, axis="x", overhang=0.0, gable=lot.m(base_brick))
        for i in range(1, n, 2):
            P.chimney(lot, i * uw - 0.04, y0 + depth / 2 - 0.04, h + 6, 10, "brick", 0.08)
        if L >= 3:
            for i in range(n):
                if (i + L) % 2 == 0:
                    lot.box6(i * uw + uw * 0.3, y0 + depth - 0.16, h + 1, uw * 0.4, 0.1, 6,
                             lot.fac(base_brick, win_w=3, win_h=4, sill=1, margin=1, period=6),
                             lot.m(base_brick))
                    lot.s.roof_gable(i * uw + uw * 0.3, y0 + depth - 0.16, h + 7, uw * 0.4, 0.1, 3, lot.m("slate"),
                                     axis="y", overhang=0.01) if lot.roofed() and lot.state != "burnt" else None
    if fam == 0 and L >= 4:
        for i in range(n):
            P.ac_units(lot, i * uw + 0.1, y0 + 0.1, h + 1, 1)
        if L == 5:
            P.panels(lot, 0.05, y0 + 0.05, h + 1, W - 0.1, depth * 0.5)
    # back gardens: sheds, trees
    gy = y0 - 0.04
    if gy > 0.15:
        for i in range(n):
            x = i * uw
            if L >= 3 and (i + lot.var) % 2 == 0:
                lot.solid(x + 0.06, 0.06, 0, 0.12, 0.1, 6, ik.mat("wood", shade=0.6), top="roof_metal")
            elif L >= 2 and gy > 0.3:
                lot.pave(x + 0.05, gy - 0.12, uw - 0.1, 0.1, "paving", layer=2)
        for i in range(n):
            x = i * uw
            if D >= 2 and L >= 2:
                lot.pave(x + 0.06, y0 - 0.16, uw - 0.12, 0.12, "paving", layer=2)
            if D >= 2 and (i + L) % 2 == 1:
                lot.pave(x + 0.08, 0.35, uw - 0.16, 0.22, "dirt", layer=2, hard=False)
                for k in range(3):
                    P.bush(lot, x + 0.13 + k * (uw - 0.26) / 2, 0.46, 0.025, "foliage_light")
        P.scatter_trees(lot, min(2 * n, L + 2 * W * (D - 1)), [(0, y0 - 0.2, W, depth + SETBACK + 0.2)], 0.8,
                        ["round", "small", "tall"], y1=y0 - 0.02)
    if L >= 4 and fam in (0, 1):
        for i in range(0, n, 2):
            P.tree(lot, i * uw + uw, D - 0.05, 0.6, "small")
    return lot
