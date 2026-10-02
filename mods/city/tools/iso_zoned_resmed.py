"""iso_zoned_resmed.py - residential medium density (3-5 storey apartment blocks) and mixed use
(shops on the ground floor, flats above) for ZONED.

res-med families: v0 NA brick walk-up + car park, v1 NA modern condo (cladding, balconies),
v2 EU plaster block with hip roof + green courtyard, v3 EU Scandi brick/white with glass balconies.
res-mixed families: v0 NA main-street brick, v1 NA modern podium, v2 EU Parisian (mansard, shutters),
v3 EU Nordic gabled townhouses. Levels add storeys, balconies, better materials, roof gardens.
"""
import isokit as ik

import iso_zoned_props as P
from iso_zoned_apt import apartment, parking

ST = ik.STOREY
SIGNS = ["red", "teal", "glass", "leaf", "purple", "yellow", "terra"]
AWN = ["red", "teal", "leaf", "glass", "purple", "terra", "yellow"]


def _wall_med(lot, fam, tier):
    if fam == 0:
        return ik.mat("brick", shade=[-0.8, 0.0, 0.6][tier - 1] + lot.r(-0.2, 0.2))
    if fam == 1:
        return [ik.mat("siding", ramp=lot.pick(["slate", "sand", "glass"]), shade=2.0),
                ik.mat("plaster", ramp="stone", shade=3.0),
                ik.mat("plaster", ramp="snow", shade=0.0)][tier - 1]
    if fam == 2:
        r = lot.pick([("sand", 3.0), ("yellow", 2.5), ("rose", 3.5), ("stone", 3.0), ("terra", 3.5)])
        return ik.mat("plaster", ramp=r[0], shade=r[1] - (0.6 if tier == 1 else 0.0))
    return [ik.mat("brick_yellow"), ik.mat("brick", shade=-0.4), ik.mat("plaster_white")][tier - 1]


def res_med(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    floors = {1: 3, 2: 3, 3: 4, 4: 4, 5: 5}[L]
    walls = [_wall_med(lot, fam, t) for t in (1, 2, 3)]
    wall = walls[tier - 1]
    lot.lawn("grass")
    bd = 0.62
    fy1 = D - 0.28
    fy0 = fy1 - bd
    bx0, bx1 = 0.16, W - 0.16
    balcony = None
    if L >= 2:
        balcony = {0: "metal", 1: "wood" if tier < 3 else "glass", 2: "metal" if tier < 3 else "white", 3: "glass"}[fam]
    roof, rmat = "flat", "roof_flat"
    if fam == 2:
        roof, rmat = "hip", lot.pick(["roof_tiles", "slate", "roof_tiles_brown"])
    if fam == 0 and tier == 1:
        roof, rmat = "gable", "shingle"
    gear = dict(tank=fam == 0 and tier >= 2, panels=L == 5 and fam in (1, 3), garden=L >= 4 and fam in (1, 3))
    # front block
    lot.pave(0, fy1, W, 0.28, "grass", layer=1)
    lot.pave(W / 2 - 0.05, fy1, 0.1, 0.28, "paving", layer=2)
    apartment(lot, bx0, fy0, bx1 - bx0, bd, floors, wall, roof, rmat, balcony=balcony, gear=gear,
              shutter="slate" if fam == 2 and tier >= 2 else None, frame="snow" if fam != 3 else "slate",
              band=0.8 if fam == 2 else 0)
    if fam == 3 and tier == 3:
        lot.solid(bx0 + 0.1, fy1, 1, 0.3, 0.04, floors * ST - 2, ik.mat("wood", shade=1.0))
    back_y1 = fy0 - 0.06
    # rear: second block (EU courtyard) or car park (NA)
    if fam >= 2:
        if D >= 3 or (W >= 3 and tier >= 2):
            wing_w = 0.62
            if D >= 3:
                apartment(lot, bx0, 0.14, bx1 - bx0, 0.6, floors - (1 if tier < 3 else 0), wall, roof, rmat,
                          doors=(0.3,), gear=gear, band=0.8 if fam == 2 else 0)
                court = (bx0, 0.8, bx1 - bx0, back_y1 - 0.8)
            else:
                apartment(lot, bx1 - wing_w, 0.14, wing_w, back_y1 - 0.14, floors, wall, roof, rmat,
                          doors=(), gear=gear)
                court = (bx0, 0.14, bx1 - wing_w - bx0 - 0.06, back_y1 - 0.14)
        else:
            court = (bx0, 0.12, bx1 - bx0, back_y1 - 0.12)
        cx, cy, cw, cd = court
        if cd > 0.15:
            lot.pave(cx + cw * 0.35, cy + 0.04, 0.3, cd - 0.08, "paving", layer=2)
            if L >= 3 and cw > 0.6:
                lot.pave(cx + 0.1, cy + 0.06, 0.22, 0.18, "sand", layer=2, hard=False)
                P.bench(lot, cx + 0.12, cy + 0.27)
            P.scatter_trees(lot, 1 + L // 2 + (W * D) // 4, [(cx + cw * 0.33, cy, 0.34, cd)], 0.9,
                            ["round", "tall"], x0=cx, y0=cy, x1=cx + cw, y1=cy + cd)
    else:
        if back_y1 > 0.5:
            parking(lot, 0.12, 0.08, W - 0.24, min(0.42, back_y1 - 0.1), cars=0.4 + 0.1 * tier)
        if fam == 1 and L >= 4 and D >= 3:
            apartment(lot, bx0, 0.14, 0.7, 0.55, floors - 1, wall, "flat", gear=gear, doors=(0.5,))
        P.scatter_trees(lot, L, [(bx0, fy0, bx1 - bx0, bd + 0.05), (0.1, 0.05, W - 0.2, 0.5)], 0.9)
    # front dressing
    if L >= 2:
        for i in range(int(W * 2)):
            P.bush(lot, 0.2 + i * (W - 0.4) / max(1, int(W * 2) - 1), fy1 + 0.05, 0.03)
    if L >= 3:
        P.tree(lot, 0.1, D - 0.1, 0.8, "round")
        P.tree(lot, W - 0.1, D - 0.1, 0.8, "round")
    if L >= 4:
        P.lamp(lot, W / 2 + 0.1, D - 0.05, 10)
    return lot


def res_mixed(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    lot.lawn("paving")
    n = W if fam != 3 else 2 * W
    uw = W / n
    fy1 = D - 0.06
    bd = 0.75 if D == 2 else 0.95
    fy0 = fy1 - bd
    base_f = {1: 3, 2: 3, 3: 4, 4: 4, 5: 5}[L]
    lot.pave(0, 0, W, fy0, "asphalt" if fam < 2 else "paving", layer=1)
    for i in range(n):
        x = i * uw
        fl = base_f + (1 if (i + lot.var) % 3 == 1 else 0) - (1 if fam == 3 else 0)
        signc = lot.pick(SIGNS)
        awn = lot.pick(AWN)
        if fam == 0:
            wall = ik.mat("brick", shade=lot.r(-0.8, 0.8), ramp=lot.pick(["brick", "brick", "wood", "terra"]))
            top = apartment(lot, x, fy0, uw, bd, fl, wall, "flat", ik.mat("roof_flat", shade=-1.0), shop=13,
                            sign=signc, doors=(0.8,), parapet=3, rim=ik.mat("stone", shade=1.5),
                            gear=dict(tank=tier >= 2 and i == 0, core=False))
        elif fam == 1:
            wall = [ik.mat("concrete"), ik.mat("plaster", ramp="glass", shade=3), ik.mat("plaster_white")][tier - 1]
            top = apartment(lot, x, fy0, uw, bd, fl, wall, "flat", shop=14, sign=signc, doors=(0.85,),
                            balcony="glass" if L >= 2 else None, gear=dict(panels=L == 5, garden=L >= 4))
        elif fam == 2:
            r = lot.pick([("stone", 3.0), ("sand", 3.0), ("snow", 0.0), ("yellow", 3.0)])
            wall = ik.mat("plaster" if tier < 3 else "stone", ramp=r[0], shade=r[1] if tier < 3 else 1.5)
            top = apartment(lot, x, fy0, uw, bd, fl, wall, "mansard" if tier >= 2 else "hip",
                            "slate" if tier >= 2 else "roof_tiles", shop=13, sign=signc, doors=(0.85,),
                            shutter="slate" if tier == 1 else None, band=1.0, balcony="metal" if L >= 4 else None)
        else:
            r = lot.pick([("rose", 3.5), ("yellow", 2.5), ("glass", 3.0), ("teal", 3.5), ("terra", 3.5), ("snow", 0)])
            wall = ik.mat("plaster", ramp=r[0], shade=r[1])
            top = apartment(lot, x, fy0, uw, bd, fl, wall, "gable", lot.pick(["roof_tiles", "slate"]), shop=13,
                            sign=signc, doors=(0.8,), roof_h=12)
        if fam in (0, 2, 3) or L >= 3:
            from iso_zoned_props import awning
            awning(lot, x + 0.04, fy1, 13, uw * 0.6, awn, depth=0.07)
    if L >= 3:
        P.cafe_tables(lot, 0.15, D - 0.03, min(0.6, W * 0.3), 2, lot.pick(AWN))
    if L >= 4:
        for i in range(W):
            P.tree(lot, i + 0.5, D - 0.02, 0.6, "small")
    # rear yard: bins, parking or garden
    if fy0 > 0.3:
        if fam < 2:
            for i in range(int(W * 2)):
                P.car(lot, 0.2 + i * 0.45, 0.25, "y")
        else:
            P.scatter_trees(lot, L // 2 + 1, [(0, fy0, W, bd)], 0.8, ["round"], y1=fy0)
        P.dumpster(lot, W - 0.25, fy0 - 0.15, lot.pick(["teal", "leaf", "slate"]))
    return lot
