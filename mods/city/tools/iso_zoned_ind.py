"""iso_zoned_ind.py - industrial and warehouse lots (ZONED).

ind families: v0 NA metal works (corrugated sheds, tanks), v1 NA brick factory (sawtooth roof,
smokestack), v2 EU modern clad plant (white/blue panels, solar), v3 EU food/agri processing (silos,
tanks). L1 small and grimy (gravel yard, rust), L3 bigger with office annex, L5 modern and clean
(landscaping, solar, fresh cladding). warehouse: big clad sheds with loading docks, trucks and
trailers; L5 = modern logistics centre.
"""
import isokit as ik

import iso_zoned_mats as M
import iso_zoned_props as P
from iso_zoned_apt import apartment, parking

ST = ik.STOREY


def sawtooth(lot, x, y, z, dx, dy, n, h, mat):
    """Sawtooth (north-light) roof: n sheds along y, glazing on the high sides."""
    if not lot.roofed():
        return
    if lot.state == "burnt":
        lot._burnt_roof(x, y, z, dx, dy, h, "y")
        return
    seg = dy / n
    glass = lot.m(ik.mat("glass_dark", lit=0.4))
    for i in range(n):
        lot.s.roof_shed(x, y + i * seg, z, dx, seg, h, lot.m(mat), high="-y", wall=glass, overhang=0.0)


def shed(lot, x, y, dx, dy, h, wall, roof="gable", roof_mat="roof_metal", doors=1, door_ramp="stone",
         win=True, rh=None):
    """Industrial shed with roller doors on the front (+Y)."""
    front = lot.fac(wall, doors=[], garage=(0.5, min(14, dx * 32 * 0.5)) if doors else None,
                    garage_ramp=door_ramp, win_w=5, win_h=3, period=10, sill=int(h) - 6, margin=4,
                    floors=1, frame="grey") if win else lot.m(wall)
    side = lot.fac(wall, win_w=5, win_h=3, period=10, sill=int(h) - 6, margin=4, floors=1, frame="grey") \
        if win else lot.m(wall)
    lot.block(x, y, 0, dx, dy, h, front, side, lot.m(wall))
    if roof == "gable":
        lot.gable(x, y, h, dx, dy, rh or (5 + 6 * min(dx, dy)), roof_mat, axis="x" if dx >= dy else "y",
                  overhang=0.03, gable=lot.m(wall))
    elif roof == "saw":
        sawtooth(lot, x, y, h, dx, dy, max(2, int(dy / 0.25)), 7, roof_mat)
    else:
        lot.flat(x, y, h, dx, dy, roof_mat, parapet=2, rim=wall)
    return h


def office_annex(lot, x, y, dx, dy, floors, wall):
    return apartment(lot, x, y, dx, dy, floors, wall, "flat", "roof_flat", doors=(0.5,),
                     win=dict(win_w=6, win_h=4, period=8, margin=2), gear=dict(core=False))


def yard(lot, x, y, dx, dy, L, tier):
    """Storage yard filled to its size: container stacks, pallet rows, crates, trucks, a skip."""
    if dx < 0.3 or dy < 0.2:
        return
    P.crates(lot, x + 0.05, y + 0.05, 3 + tier)
    cx = x + 0.3
    row = 0
    while cx + 0.4 < x + dx and row < (6 if tier < 3 else 3):
        yy = y + 0.05
        k = 0
        while yy + 0.15 < y + dy - (0.25 if L >= 2 else 0.0) and k < (3 if tier < 3 else 2):
            P.container(lot, cx, yy, "x")
            if (row + k + lot.var) % 3 == 0:
                P.container(lot, cx, yy, "x", z=7)
            yy += 0.17
            k += 1
        cx += 0.45
        row += 1
    if dy > 0.45 and dx > 0.5:
        for i in range(int(dx / 0.25)):
            if (i + lot.var) % 2 == 0:
                lot.solid(x + 0.05 + i * 0.25, y + dy - 0.12, 0, 0.12, 0.08, 1, "wood")
                lot.solid(x + 0.06 + i * 0.25, y + dy - 0.11, 1, 0.1, 0.06, 3,
                          lot.m("plain", ramp=lot.pick(["sand", "glass", "olive"]), shade=1.5))
    if L >= 2 and dx > 0.8 and dy > 0.3:
        P.truck(lot, x + dx - 0.7, y + dy - 0.2, "x")
    if dx > 1.6 and dy > 0.5:
        P.truck(lot, x + 0.1, y + dy - 0.2, "x")
    P.dumpster(lot, x + dx - 0.15, y + 0.05, "slate")


def ind(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    lot.lawn("gravel" if tier == 1 else ("concrete_ground" if tier == 2 else "asphalt"))
    rusty = tier == 1
    if fam == 0:
        clad = ik.mat("metal", ramp="terra" if rusty else "slate", shade=0.0 if rusty else 1.0)
    elif fam == 1:
        clad = ik.mat("brick", shade=-0.6 if rusty else 0.0)
    elif fam == 2:
        clad = [ik.mat("metal_light"), ik.mat("metal", ramp="glass", shade=2.0), ik.mat("metal_light", ramp="snow", shade=0.0)][tier - 1]
    else:
        clad = ik.mat("concrete", shade=1.0) if tier < 3 else ik.mat("plaster", ramp="sand", shade=3.0)
    if rusty:
        clad = M.st(clad, "worn", amt=1.0)
    small = W * D == 1
    # main hall
    hw = W - (0.25 if small else 0.4)
    hd = min(D - 0.45, 0.5 if small else 0.9 + 0.35 * (D - 2))
    hx = 0.12
    hy = 0.15 if not small else D - 0.3 - hd
    hh = (12 if small else 16) + 4 * tier
    roof_kind = "saw" if fam == 1 and not small else ("flat" if fam == 2 and tier >= 2 else "gable")
    if small:
        hw = 0.62
    shed(lot, hx, hy, hw, hd, hh, clad, roof_kind,
         "roof_metal" if fam != 1 else "slate", doors=1, door_ramp="yellow" if fam == 2 else "stone")
    if fam == 2 and tier >= 2 and not small:
        P.panels(lot, hx + 0.1, hy + 0.1, hh + 1, hw - 0.3, hd - 0.2)
    # second shed on big lots
    if W * D >= 9 and tier >= 2:
        sw = min(0.9, W - hw - 0.2) if W > hw + 1.0 else 0.0
        if sw <= 0.3:
            sw = 0.0
        if sw:
            shed(lot, W - sw - 0.1, 0.15, sw, min(0.7, hd), hh - 4, clad, "gable", "roof_metal", doors=1)
    # office annex at the front (L3+)
    if tier >= 2 and not small:
        aw = min(0.7, hw * 0.4)
        wall = ik.mat("brick") if fam == 1 else (ik.mat("glass") if tier == 3 and fam == 2 else ik.mat("concrete", shade=1.5))
        if wall.name == "glass":
            from iso_zoned_off import glass_box
            z = glass_box(lot, hx, hy + hd, 0, aw, 0.3, 2)
            lot.flat(hx, hy + hd, z, aw, 0.3, "roof_flat", parapet=2, rim="metal_light")
        else:
            office_annex(lot, hx, hy + hd, aw, 0.3, 2, wall)
    # chimneys / tanks / silos
    if not small or L >= 3:
        ax = hx + hw - 0.2
        if fam == 1:
            P.smokestack(lot, ax, hy + 0.15, 0.07, 40 + 8 * tier, "brick", "red" if tier < 3 else None)
        elif fam == 0:
            P.smokestack(lot, ax, hy + 0.12, 0.05, 32 + 6 * tier, "metal_light", "red")
    if not small:
        tx = W - 0.25
        if fam == 0:
            for i in range(min(3, tier + 1)):
                P.tank(lot, tx, hy + hd + 0.2 + i * 0.3, 0.12, 14 + 2 * tier, "metal_light")
        elif fam == 3:
            for i in range(1 + tier):
                if hy + hd + 0.15 + i * 0.28 < D - 0.15:
                    lot.s.cylinder(tx, hy + hd + 0.15 + i * 0.28, 0, 0.11, 36 + 4 * tier,
                                   lot.m("metal_light", shade=0.5), segs=16) if lot.stage not in (1, 2) else None
                    lot.s.cone(tx, hy + hd + 0.15 + i * 0.28, 36 + 4 * tier, 0.11, 6, lot.m("roof_metal"),
                               segs=16) if lot.roofed() else None
        elif fam == 2 and tier >= 2:
            P.tank(lot, tx, hy + hd + 0.25, 0.1, 12, "metal_light")
    # yard, fence, trucks
    yx0 = 0.12 + (0.75 if tier >= 2 and not small else 0.0)
    yard(lot, yx0, hy + hd + 0.08, W - yx0 - 0.45, D - hy - hd - 0.2, L, tier)
    if small:
        P.crates(lot, 0.12, 0.08, 2)
        P.car(lot, W - 0.15, D - 0.2, "y")
    if tier == 1:
        P.fence(lot, 0.02, 0.02, W - 0.04, 0.02, "chain")
        P.fence(lot, 0.02, 0.04, 0.02, D - 0.1, "chain")
    if tier == 3:
        P.scatter_trees(lot, W, [(0.1, 0.1, W - 0.2, D - 0.3)], 0.8, ["round", "tall"], y0=D - 0.25)
        for i in range(W * 2):
            P.bush(lot, 0.15 + i * 0.5, D - 0.08, 0.035)
    return lot


def warehouse(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    lot.lawn("asphalt" if tier >= 2 else "concrete_ground")
    if fam in (0, 2):
        clad = [ik.mat("metal_light"), ik.mat("metal", ramp="glass", shade=2.0), ik.mat("metal_light", ramp="snow", shade=0.0)][tier - 1]
    else:
        clad = [ik.mat("metal", ramp="terra", shade=0.5), ik.mat("metal", ramp="slate", shade=1.5), ik.mat("metal", ramp="teal", shade=2.0)][tier - 1]
    if tier == 1:
        clad = M.st(clad, "worn", amt=0.8)
    bw = W - 0.3
    bd = min(D - 0.75, 1.2 + 0.3 * (D - 2))
    bx, by = 0.15, 0.12
    h = 18 + 4 * tier
    ndocks = max(2, int(bw / 0.28))
    band = lot.fac(clad, win_w=8, win_h=3, period=12, sill=h - 6, margin=6, floors=1, frame="grey")
    lot.block(bx, by, 0, bw, bd, h, lot.m(clad), band, lot.m(clad))
    if fam in (1, 3) and tier < 3:
        lot.gable(bx, by, h, bw, bd, 8, "roof_metal", axis="x", overhang=0.02, gable=lot.m(clad))
    else:
        lot.flat(bx, by, h, bw, bd, "roof_flat", parapet=2, rim=clad)
        if L == 5:
            P.panels(lot, bx + 0.1, by + 0.1, h + 1, bw - 0.3, bd - 0.3)
        elif lot.roofed() and lot.stage != 3:
            for i in range(int(bw / 0.4)):
                lot.s.box(bx + 0.15 + i * 0.4, by + bd * 0.3, h, 0.15, bd * 0.4, 2, lot.m("glass_dark"))
    # loading docks on the front: roller doors + dock levellers + trucks/trailers
    if lot.walls_visible and lot.state != "collapsed":
        for i in range(ndocks):
            dx = bx + (i + 0.5) * bw / ndocks - 0.08
            door = ik.mat("metal_light", ramp="yellow" if tier == 3 else "grey", shade=1.0)
            lot.s.box(dx, by + bd, 0, 0.16, 0.012, 11, lot.m(door))
            lot.s.box(dx - 0.01, by + bd, 11, 0.18, 0.05, 2, lot.m("metal"))
            lot.s.box(dx, by + bd, 0, 0.16, 0.08, 3, lot.m("concrete", shade=0.8))
    for i in range(ndocks):
        if (i + lot.var) % 3 != 2 and i < 2 + tier:
            dx = bx + (i + 0.5) * bw / ndocks
            P.truck(lot, dx - 0.07, by + bd + 0.1, "y", lot.pick(["snow", "red", "glass", "yellow"]))
    # office corner + car park
    oy = by + bd + 0.12
    if tier >= 2 and D - oy > 0.6:
        wall = ik.mat("glass") if tier == 3 else ik.mat("concrete", shade=1.5)
        if tier == 3:
            from iso_zoned_off import glass_box
            z = glass_box(lot, W - 0.65, D - 0.5, 0, 0.5, 0.35, 2)
            lot.flat(W - 0.65, D - 0.5, z, 0.5, 0.35, "roof_flat", parapet=2, rim="metal_light")
        else:
            office_annex(lot, W - 0.65, D - 0.5, 0.5, 0.35, 2, wall)
        parking(lot, 0.15, D - 0.45, W - 0.9, 0.38, cars=0.5, along="y")
    # trailers parked in the yard
    if D - oy > 0.5 and W >= 3:
        P.container(lot, 0.2, D - 0.2, "x", "grey")
    if tier == 1:
        P.fence(lot, 0.02, D - 0.04, W - 0.04, 0.02, "chain")
    if tier == 3:
        for i in range(W):
            P.tree(lot, i + 0.3, D - 0.06, 0.7, "round")
    return lot
