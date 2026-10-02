"""iso_zoned_reshigh.py - residential high density towers and low-rent blocks (ZONED).

res-high: podium + tower. Storeys 8/10/13/16/20 (L1-L5), +2 on 3x3. Families: v0 NA brick/concrete
tower with water tank, v1 NA glass condo tower with balconies, v2 EU white tower with coloured balcony
panels, v3 EU terracotta brick stepped tower. L4+ plaza with trees/fountain, L5 crown + helipad.
res-lowrent: worn slab blocks (5-8 storeys) with laundry and dishes; L5 = renovated, colourful.
"""
import isokit as ik

import iso_zoned_mats as M
import iso_zoned_props as P
from iso_zoned_apt import apartment, balconies, parking

ST = ik.STOREY


def fountain(lot, x, y, r=0.12):
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    lot.s.cylinder(x, y, 0, r, 2, lot.m("stone", shade=1.0), top=lot.m("water") if lot.live else lot.m("mud"),
                   segs=16)
    if lot.live:
        lot.s.cylinder(x, y, 2, r * 0.2, 4, lot.m("stone"), segs=8)


def helipad(lot, x, y, z, s):
    if not lot.roofed() or lot.stage == 3 or lot.state == "burnt":
        return
    lot.s.ground([(x, y), (x + s, y), (x + s, y + s), (x, y + s)], ik.mat("plain", ramp="slate", shade=0.0),
                 z=z + 0.6, layer=3)
    w = ik.mat("plain", ramp="snow", shade=1.0)
    c, a = s / 2, s * 0.22
    for (px, py, dx, dy) in ((x + c - a, y + c - a, 0.03, 2 * a), (x + c + a - 0.03, y + c - a, 0.03, 2 * a),
                             (x + c - a, y + c - 0.015, 2 * a, 0.03)):
        lot.s.ground([(px, py), (px + dx, py), (px + dx, py + dy), (px, py + dy)], w, z=z + 0.7, layer=4)


def tower(lot, x, y, dx, dy, z0, floors, fam, tier, L, front_balc=True):
    """Tower shaft with family facade + crown; returns its top."""
    if fam == 0:
        wall = [ik.mat("brick", shade=-0.5), ik.mat("brick_yellow"), ik.mat("stone", shade=1.0)][tier - 1]
        win = dict(win_w=3, win_h=5, period=5, margin=2)
    elif fam == 1:
        wall = [ik.mat("concrete", shade=0.5), ik.mat("glass"), ik.mat("glass")][tier - 1]
        win = dict(win_w=4, win_h=6, period=6, margin=2, sill=2)
    elif fam == 2:
        wall = ik.mat("plaster_white") if tier >= 2 else ik.mat("concrete", shade=1.0)
        win = dict(win_w=4, win_h=5, period=6, margin=3)
    else:
        wall = ik.mat("brick", ramp="terra", shade=-0.5) if tier >= 2 else ik.mat("brick")
        win = dict(win_w=3, win_h=6, period=5, margin=2, sill=2)
    glassy = fam == 1 and tier >= 2
    h = floors * ST + 1
    if glassy:
        g = lot.m(ik.mat("glass", lit=0.5)) if lot.wstate() != "ok" else ik.mat("glass", lit=0.5)
        gm = lot.fac(ik.mat("concrete"), **win) if lot.wstate() != "ok" else g
        lot.block(x, y, z0, dx, dy, h, gm, gm, gm)
    else:
        f = lot.fac(wall, **win)
        lot.block(x, y, z0, dx, dy, h, f, f, f)
    top = z0 + h
    if front_balc and fam in (1, 2) and lot.walls_visible:
        rail = "glass" if fam == 1 else "white"
        balconies(lot, x, y + dy, z0, dx, floors, rail, every=0.24, w=0.14)
        if fam == 2 and tier >= 2 and lot.state == "ok" and lot.stage == 0:
            cols = ["yellow", "teal", "terra", "glass"]
            for f in range(1, floors, 2):
                lot.s.box(x + 0.05, y + dy + 0.05, z0 + f * ST + 1, 0.12, 0.013, 3,
                          ik.mat("plain", ramp=cols[(f // 2) % 4], shade=2.5))
    # crown / roof
    if L == 5 and min(dx, dy) > 0.5:
        cx, cy, cdx, cdy = x + 0.1, y + 0.1, dx - 0.2, dy - 0.2
        f = lot.fac(wall, **win) if not glassy else ik.mat("glass_dark")
        lot.block(cx, cy, top, cdx, cdy, 2 * ST, f, f, f, mass=False)
        top2 = top + 2 * ST
        lot.flat(cx, cy, top2, cdx, cdy, "roof_flat", parapet=3, rim="concrete")
        if fam in (1, 2):
            helipad(lot, cx + cdx * 0.2, cy + cdy * 0.15, top2, min(cdx, cdy) * 0.6)
        else:
            lot.s.box(cx + cdx / 2, cy + cdy / 2, top2, 0.015, 0.015, 26, lot.m("plain", ramp="slate")) \
                if lot.roofed() else None
        lot.flat(x, y, top, dx, dy, "roof_gravel", parapet=2, rim="concrete")
        return top2
    lot.flat(x, y, top, dx, dy, "roof_flat", parapet=3, rim="concrete")
    from iso_zoned_apt import roof_gear
    roof_gear(lot, x, y, top, dx, dy, tank=fam == 0, antenna=fam == 3 and L >= 3, garden=fam == 2 and L >= 4)
    return top


def res_high(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    floors = {1: 8, 2: 10, 3: 13, 4: 16, 5: 20}[L] + (2 if W * D >= 9 else 0)
    lot.lawn("paving" if fam in (0, 1) else "grass")
    # podium (lobby / shops)
    px0, px1 = 0.12, W - 0.12
    py1 = D - 0.2
    py0 = py1 - min(1.1, D - 0.5)
    pod = ik.mat("stone", shade=1.0) if fam != 1 else ik.mat("concrete", shade=1.5)
    podf = lot.fac(pod, shop=12, sign="glass" if fam != 0 else "red", doors=(0.5,), win_w=4, period=7)
    lot.block(px0, py0, 0, px1 - px0, py1 - py0, 14, podf, lot.fac(pod, shop=12, doors=()), lot.fac(pod))
    lot.flat(px0, py0, 14, px1 - px0, py1 - py0, "roof_green" if L >= 4 else "roof_flat", parapet=2,
             rim="concrete")
    # towers
    if W == 3 and D == 2 and fam in (1, 3):
        t1 = tower(lot, 0.25, py0 + 0.12, 0.75, 0.75, 14, floors, fam, tier, L)
        tower(lot, 1.95, py0 + 0.12, 0.75, 0.75, 14, floors - 3, fam, tier, L)
    elif W == 3 and D == 2:
        tower(lot, 0.35, py0 + 0.15, 2.0, 0.7, 14, floors - 2, fam, tier, L)
    else:
        tw = 0.9 if W == 2 else 1.1
        td = 0.85 if D == 2 else 1.05
        tx = (W - tw) / 2
        ty = py0 + 0.12 if D == 2 else 0.25
        tower(lot, tx, ty, tw, td, 14 if D == 2 else 0, floors, fam, tier, L)
        if W * D >= 6 and L >= 3 and D == 3:
            pass
    # plaza / grounds in front of the podium
    lot.pave(0, py1, W, 0.2, "paving", layer=1)
    if L >= 4:
        for i in range(W):
            P.tree(lot, i + 0.5, D - 0.08, 0.7, "round")
    if L >= 2 and py0 > 0.35:
        if fam in (0, 1):
            parking(lot, 0.12, 0.05, W - 0.24, min(0.42, py0 - 0.08), cars=0.6)
        else:
            P.scatter_trees(lot, 2 + L // 2, [(0, py0, W, D)], 0.9, ["round", "tall"], y1=py0)
            if L >= 4:
                fountain(lot, W / 2, py0 / 2, 0.1)
    return lot


def res_lowrent(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    renovated = L == 5
    floors = {1: 5, 2: 5, 3: 7, 4: 7, 5: 8}[L]
    lot.lawn("grass_dry" if L <= 2 else "grass")
    if fam == 0:
        wall = ik.mat("brick", shade=-0.8)
    elif fam == 1:
        wall = ik.mat("concrete", shade=0.0)
    elif fam == 2:
        wall = ik.mat("concrete", shade=0.8, panel_u=12)
    else:
        wall = ik.mat("plaster", ramp="sand", shade=1.5)
    if renovated:
        wall = ik.mat("plaster", ramp=lot.pick(["yellow", "teal", "terra", "glass"]), shade=3.0)
    elif L <= 4:
        wall = M.st(wall, "worn", amt=1.2 if L <= 2 else 0.6)
    bd = 0.6
    by1 = D - 0.35
    by0 = by1 - bd
    bx0, bx1 = 0.15, W - 0.15
    win = dict(win_w=4, win_h=5, period=7, margin=3)
    top = apartment(lot, bx0, by0, bx1 - bx0, bd, floors, wall, "flat", "roof_flat", win=win,
                    doors=(0.3, 0.7), frame="stone", parapet=1, rim="concrete",
                    gear=dict(core=True, tank=fam == 0))
    if fam == 1 and lot.walls_visible and lot.state != "collapsed":
        # exterior access decks (projects style)
        for f in range(1, floors):
            lot.s.box(bx0, by1, f * ST + 1, bx1 - bx0, 0.07, 1, lot.m("concrete", shade=1.0))
            lot.s.box(bx0, by1 + 0.06, f * ST + 2, bx1 - bx0, 0.01, 3, lot.m("plain", ramp="slate", shade=0.5))
    else:
        balconies(lot, bx0, by1, 0, bx1 - bx0, floors, "metal" if not renovated else "glass", every=0.28)
    if lot.live:
        # laundry lines and satellite dishes on random balconies
        n = int((bx1 - bx0) / 0.28)
        for f in range(1, floors):
            for i in range(n):
                k = (f * 7 + i * 3 + lot.var) % 5
                bx = bx0 + i * (bx1 - bx0) / n + 0.06
                if k == 0 and not renovated:
                    lot.s.box(bx, by1 + 0.06, f * ST + 4, 0.1, 0.008, 2,
                              ik.mat("plain", ramp=lot.pick(["snow", "rose", "glass", "yellow"]), shade=2))
                elif k == 2:
                    lot.s.ellipsoid(bx + 0.03, by1 + 0.07, f * ST + 6, 0.025, 0.01, 2, ik.mat("plaster_white"))
    # yard
    yard_y1 = by0 - 0.05
    if yard_y1 > 0.3:
        parking(lot, 0.12, 0.06, W - 0.24, min(0.4, yard_y1 - 0.08), cars=0.35 + 0.1 * tier, along="y")
    lot.pave(0, by1, W, D - by1, "asphalt" if L <= 2 else "paving", layer=1)
    if L >= 3:
        P.scatter_trees(lot, L - 1, [(bx0, by0, bx1 - bx0, bd + 0.2), (0.1, 0.05, W - 0.2, 0.45)], 0.85)
    if L >= 4 and lot.live:
        cx = W - 0.45
        lot.pave(cx, D - 0.3, 0.32, 0.26, "sand", layer=2, hard=False)
        lot.s.box(cx + 0.05, D - 0.2, 0, 0.012, 0.012, 7, ik.mat("plain", ramp="red", shade=1))
        lot.s.box(cx + 0.2, D - 0.2, 0, 0.012, 0.012, 7, ik.mat("plain", ramp="red", shade=1))
        lot.s.box(cx + 0.05, D - 0.2, 7, 0.16, 0.012, 1, ik.mat("plain", ramp="red", shade=1))
    for i in range(2 if L <= 3 else 1):
        P.dumpster(lot, 0.2 + i * 0.15, by1 + 0.12, "leaf" if i else "slate")
    if L <= 2:
        P.fence(lot, 0.02, 0.02, W - 0.04, 0.02, "chain")
    return lot
