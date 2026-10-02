"""iso_zoned_reslow.py - residential low density: detached family houses (ZONED).

L1 starter bungalow, L2 + porch/shrubs/fence, L3 two storeys + garage (NA) / carport (EU) + patio,
L4 + side wing, trees, play area, L5 large home with hip roof, pool, two cars, lamps.
NA: siding, shingles, open lawns, asphalt drive, mailbox. EU: plaster/brick, clay tiles, hedges, walls.
"""
import isokit as ik

import iso_zoned_props as P
from iso_zoned_res import NA_SIDING, EU_PLASTER, NA_ROOF, EU_ROOF, DOORS, SHUTTERS, house

ST = ik.STOREY


def identity(lot):
    """Choices that stay the same for a variant across its levels (fixed RNG order)."""
    r = lot.pick(EU_PLASTER if lot.eu else NA_SIDING)
    base = ik.mat("plaster" if lot.eu else "siding", ramp=r[0], shade=r[1])
    brick = ik.mat("brick", shade=lot.r(-0.4, 0.5))
    use_brick = lot.chance(0.4)
    return dict(base=base, brick=brick, use_brick=use_brick,
                roof=lot.pick(EU_ROOF if lot.eu else NA_ROOF), door=lot.pick(DOORS),
                shutter=lot.pick(SHUTTERS) if lot.chance(0.6) else None,
                side=lot.chance(0.5), trees=lot.pick(["round", "tall", "conifer", "round"]))


def dormer(lot, x, y1, z, w, wall, roof):
    """Small gabled dormer on the front (+Y) roof slope; y1 = front eave line."""
    if not lot.roofed() or lot.state == "burnt":
        return
    front = lot.fac(wall, win_w=3, win_h=4, sill=2, period=6, margin=0, frame="snow")
    lot.box6(x, y1 - 0.13, z + 1, w, 0.1, 7, front, lot.m(wall), lot.m(wall), lot.m(wall))
    lot.s.roof_gable(x, y1 - 0.13, z + 8, w, 0.1, 4, lot.m(roof), axis="y", overhang=0.02, gable=lot.m(wall))


def garden(lot, x0, y0, x1, y1, L, idt):
    """Fill the back/side garden rect with level-appropriate dressing."""
    w, d = x1 - x0, y1 - y0
    if w < 0.15 or d < 0.12:
        return
    items = []
    if L >= 2:
        items.append("bed")
    if L >= 3:
        items += ["patio", "shed"]
    if L >= 4:
        items += ["play"]
    if L >= 5 and w >= 0.45 and d >= 0.28:
        items = ["pool"] + items
    x = x0
    for it in items:
        if x > x1 - 0.12:
            break
        if it == "pool":
            pw = min(0.45, w * 0.6)
            P.pool(lot, x + 0.04, y0 + 0.04, pw, min(0.22, d - 0.1))
            x += pw + 0.12
        elif it == "patio":
            lot.pave(x, y1 - 0.14, min(0.3, x1 - x), 0.12, "paving", layer=2)
            P.bench(lot, x + 0.05, y1 - 0.1)
            x += 0.32
        elif it == "shed" and d >= 0.18:
            lot.solid(x + 0.02, y0 + 0.03, 0, 0.12, 0.1, 6, ik.mat("wood", shade=0.5))
            lot.gable(x + 0.02, y0 + 0.03, 6, 0.12, 0.1, 3, "roof_metal", axis="x")
            x += 0.16
        elif it == "bed":
            lot.pave(x + 0.02, y0 + 0.02, min(0.18, x1 - x - 0.02), 0.07, "dirt", layer=2, hard=False)
            for i in range(3):
                P.bush(lot, x + 0.05 + i * 0.05, y0 + 0.055, 0.025, "foliage_light")
            x += 0.22
        elif it == "play" and lot.live:
            lot.s.box(x + 0.02, y0 + 0.05, 0, 0.012, 0.012, 7, ik.mat("plain", ramp="red", shade=1))
            lot.s.box(x + 0.14, y0 + 0.05, 0, 0.012, 0.012, 7, ik.mat("plain", ramp="red", shade=1))
            lot.s.box(x + 0.02, y0 + 0.05, 7, 0.132, 0.012, 1, ik.mat("plain", ramp="red", shade=1))
            lot.pave(x + 0.0, y0 + 0.1, 0.18, 0.08, "sand", layer=2, hard=False)
            x += 0.2


def res_low(lot):
    W, D, L = lot.W, lot.D, lot.level
    eu = lot.eu
    idt = identity(lot)
    lot.lawn("grass")
    wall = idt["brick"] if (idt["use_brick"] and L >= 3) else idt["base"]
    roof = idt["roof"]
    floors = 1 if L <= 2 else 2
    tier = (L + 1) // 2                       # 1, 2, 3 = distinct house models
    # house size by tier and lot
    hw = (0.44 + 0.07 * tier) if W == 1 else (0.8 + 0.14 * tier)
    hd = (0.36 + 0.03 * tier) if D == 1 else (0.52 + 0.08 * tier)
    setback = 0.26 if D == 1 else 0.42
    has_garage = (not eu) and tier >= 2
    gw = 0.2 if has_garage else 0.0
    wing = tier == 3 or (L == 4 and W * D >= 2)
    ww = 0.2 if (wing and W >= 2) else (0.16 if wing else 0.0)
    if W == 1:
        hw = min(hw, 0.92 - gw - ww)
    hy = D - setback - hd
    hx = (W - hw - gw - ww) / 2.0 + ww
    hx += (-0.03 if W == 1 else 0.0)
    axis = "x" if (hw >= hd or not eu) else "y"
    roof_kind = "hip" if tier == 3 and idt["side"] else "gable"
    door_at = 0.3 if has_garage else 0.5
    h = house(lot, hx, hy, hw, hd, floors, wall, roof, roof_kind, axis=axis, door_at=door_at,
              shutter=idt["shutter"] if (eu or tier >= 2) else None, door=idt["door"])
    rh = 8 + 4 * min(hw, hd) / 0.4
    if eu and tier >= 2 and axis == "x" and roof_kind == "gable":
        n = 1 if hw < 0.6 else 2
        for i in range(n):
            dormer(lot, hx + hw * (i + 1) / (n + 1) - 0.1, hy + hd, h, 0.2, wall, roof)
    # side wing (one storey) on the -X side
    if ww:
        wy = hy + 0.06
        wd = hd - 0.06
        wf = lot.fac(wall, doors=[], frame="snow", shutter=idt["shutter"] if eu else None)
        lot.block(hx - ww, wy, 0, ww, wd, ST + 1, wf, lot.fac(wall))
        lot.gable(hx - ww, wy, ST + 1, ww, wd, 6, roof, axis="y", gable=lot.m(wall))
    # garage on the +X side
    drive_x = min(W - 0.22, hx + hw + 0.02)
    if has_garage:
        gf = lot.fac(wall, doors=[], garage=(0.5, 11), floors=0)
        lot.block(hx + hw, hy + 0.08, 0, gw, hd - 0.08, ST + 2, gf, lot.fac(wall, floors=1))
        lot.gable(hx + hw, hy + 0.08, ST + 2, gw, hd - 0.08, 6, roof, axis="y", gable=lot.m(wall))
        drive_x = hx + hw
    dw = 0.2
    d_y0 = hy + hd if has_garage else hy + hd * 0.4
    dmat = "asphalt" if not eu else ("gravel" if tier < 3 else "paving")
    lot.pave(drive_x, d_y0, dw, D - d_y0, dmat, layer=1)
    if eu and tier >= 2 and not has_garage:
        lot.solid(drive_x + 0.01, hy + hd * 0.4, 0, 0.012, 0.012, ST, "wood")
        lot.solid(drive_x + dw - 0.02, hy + hd * 0.4, 0, 0.012, 0.012, ST, "wood")
        lot.solid(drive_x, hy + hd * 0.4, ST, dw, hd * 0.6, 1.5, "wood", top="roof_metal")
    door_x = hx + hw * door_at
    lot.pave(door_x - 0.035, hy + hd, 0.07, D - hy - hd, "paving", layer=2)
    # porch
    if L >= 2:
        pw = min(hw * 0.45, 0.28)
        px = door_x - pw / 2
        lot.solid(px, hy + hd, 0, pw, 0.08, 1, "paving")
        if not eu or tier == 3:
            for cx in (px, px + pw - 0.015):
                lot.solid(cx, hy + hd + 0.065, 1, 0.015, 0.015, ST - 1, "plaster_white")
            lot.solid(px - 0.01, hy + hd - 0.005, ST, pw + 0.02, 0.09, 1.5, lot.m(roof))
    if eu or tier >= 2:
        P.chimney(lot, hx + hw * 0.78, hy + hd * 0.42, h, int(rh) + 3, "brick")
    # cars
    P.car(lot, drive_x + dw / 2, D - 0.2, "y")
    if L == 5 and D >= 2:
        P.car(lot, drive_x + dw / 2, D - 0.55, "y")
    if not eu:
        P.mailbox(lot, drive_x - 0.04, D - 0.05)
    # front boundary
    if L >= 2:
        if eu:
            P.hedge(lot, 0.03, D - 0.07, max(0.06, drive_x - 0.06), 0.05, 4 + (L >= 4))
        elif L >= 3:
            P.fence(lot, 0.03, D - 0.05, max(0.06, door_x - 0.08), 0.025, "picket")
    if L >= 3:
        P.fence(lot, 0.02, 0.02, W - 0.04, 0.025, "wall" if (eu and L >= 4) else "rail")
        P.fence(lot, 0.02, 0.045, 0.025, max(0.1, hy + hd * 0.5), "rail")
    # garden + trees
    garden(lot, 0.08, 0.08, W - 0.06, max(0.1, hy - 0.04), L, idt)
    nt = {1: 1, 2: 2, 3: 2, 4: 3, 5: 4}[L] + 2 * (W * D - 1)
    avoid = [(hx - ww, hy, hw + gw + ww, hd + 0.12), (drive_x, hy, dw, D - hy), (0.05, 0.05, W - 0.1, 0.22 if L >= 4 else 0.0)]
    P.scatter_trees(lot, nt, avoid, 0.75 + 0.1 * tier, [idt["trees"], "round"])
    for i in range(min(4, L - 1)):
        P.bush(lot, hx + 0.04 + i * (hw - 0.08) / 3, hy + hd + 0.05, 0.03)
    if L == 5:
        P.lamp(lot, door_x + 0.06, D - 0.05, 8)
    return lot
