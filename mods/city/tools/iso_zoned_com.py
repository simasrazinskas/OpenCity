"""iso_zoned_com.py - commercial low/high (ZONED).

com-low families: v0 NA strip store with front car park and roof sign, v1 NA diner/cafe with pole
sign, v2 EU shop-house (shop + flat above, awning, cafe tables), v3 EU modern bakery/boutique
(gabled, big glass). com-high families: v0 NA mall/big-box + car park, v1 NA hotel tower on a
podium, v2 EU stone department store with rooftop sign, v3 EU glass galleria with atrium.
Levels grow floors, signage, glazing, landscaping; L5 is premium (roof terrace, trees, lit signs).
"""
import isokit as ik

import iso_zoned_props as P
from iso_zoned_apt import apartment, parking, roof_gear

ST = ik.STOREY
SIGNS = ["red", "teal", "glass", "leaf", "purple", "yellow", "terra", "rose"]


def roof_sign(lot, x, y, z, w, ramp, h=6, face="+y"):
    """Big sign board standing on a roof (two legs)."""
    if lot.stage in (1, 2) or lot.state in ("collapsed", "burnt"):
        return
    lot.s.box(x + 0.02, y, z, 0.012, 0.012, 3, lot.m("plain", ramp="slate"))
    lot.s.box(x + w - 0.03, y, z, 0.012, 0.012, 3, lot.m("plain", ramp="slate"))
    P.sign(lot, x, y + 0.012, z + 3, w, h, ramp, face=face)


def pole_sign(lot, x, y, ramp, h=22):
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    lot.s.box(x, y, 0, 0.015, 0.015, h, lot.m("plain", ramp="slate", shade=0.5))
    P.sign(lot, x - 0.08, y + 0.015, h, 0.17, 7, ramp)


def com_low(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    sign = lot.pick(SIGNS)
    awn = lot.pick(SIGNS)
    lot.lawn("paving" if fam >= 2 else "asphalt")
    if fam == 0:
        # strip store: building at the back, car park in front
        bd = 0.42 if D == 1 else 0.7
        by0 = 0.12
        bw = W - 0.2
        wall = [ik.mat("concrete", shade=1.0), ik.mat("brick_yellow"), ik.mat("stone", shade=1.5)][tier - 1]
        units = max(1, int(W * (1 + (tier >= 2))))
        for i in range(units):
            ux = 0.1 + i * bw / units
            apartment(lot, ux, by0, bw / units, bd, 1, wall, "flat", "roof_flat", shop=13,
                      sign=lot.pick(SIGNS), doors=(0.5,), parapet=3, rim=wall, gear=dict(core=False))
            if L >= 2:
                P.awning(lot, ux + 0.03, by0 + bd, 13, bw / units - 0.06, lot.pick(SIGNS), depth=0.07)
        if L >= 3:
            roof_sign(lot, 0.1 + bw * 0.25, by0 + bd - 0.04, 18, bw * 0.5, sign, 6)
        parking(lot, 0.1, by0 + bd + 0.12, W - 0.2, D - by0 - bd - 0.14, cars=0.3 + 0.12 * L, along="y") \
            if D - by0 - bd > 0.3 else None
        if L >= 4:
            P.tree(lot, 0.08, D - 0.1, 0.7, "round")
            P.tree(lot, W - 0.08, D - 0.1, 0.7, "round")
    elif fam == 1:
        # diner / cafe: single storey glass box, pole sign, small car park on the side
        bw = min(W - 0.3, 0.55 + 0.1 * tier + 0.3 * (W - 1))
        bd = min(D - 0.3, 0.42 + 0.05 * tier)
        bx, by = 0.12, D - 0.18 - bd
        wall = [ik.mat("siding", ramp="snow", shade=0.0), ik.mat("metal_light"), ik.mat("plaster", ramp="glass", shade=3)][tier - 1]
        apartment(lot, bx, by, bw, bd, 1, wall, "flat", "roof_flat", shop=13, sign=sign, doors=(0.3,), parapet=2,
                  rim=ik.mat("plain", ramp=sign, shade=1.5), gear=dict(core=False))
        if L >= 2:
            P.awning(lot, bx + 0.03, by + bd, 13, bw - 0.06, awn, depth=0.08)
        pole_sign(lot, min(W - 0.1, bx + bw + 0.12), D - 0.12, sign, 18 + 3 * tier)
        if W >= 2 or D >= 2:
            parking(lot, bx + bw + 0.05 if W >= 2 else 0.1, 0.08, max(0.4, W - bx - bw - 0.15) if W >= 2 else W - 0.2,
                    0.36 if W >= 2 else min(0.36, by - 0.1), cars=0.5)
        else:
            P.car(lot, W - 0.15, 0.3, "y")
        if L >= 4:
            P.cafe_tables(lot, bx + 0.05, D - 0.08, bw * 0.6, 2, awn)
    else:
        # EU shop-house (v2) or modern boutique (v3) along the sidewalk
        n = W
        floors = {1: 1, 2: 2, 3: 2, 4: 2, 5: 3}[L] if fam == 2 else 1
        bd = min(D - 0.2, 0.55 if D == 1 else 0.9)
        by = D - 0.06 - bd
        for i in range(n):
            x = i + 0.06
            uw = 0.88
            if fam == 2:
                r = lot.pick([("stone", 3.0), ("sand", 3.0), ("yellow", 2.5), ("rose", 3.5), ("snow", 0.0)])
                wall = ik.mat("plaster", ramp=r[0], shade=r[1])
                top = apartment(lot, x, by, uw, bd, floors, wall, "gable" if tier < 3 else "mansard",
                                lot.pick(["roof_tiles", "slate"]), shop=13, sign=lot.pick(SIGNS), doors=(0.7,),
                                shutter="leaf" if tier == 2 else None, roof_h=11)
                P.awning(lot, x + 0.04, by + bd, 13, uw * 0.6, lot.pick(SIGNS), depth=0.08)
            else:
                wall = [ik.mat("brick"), ik.mat("wood", shade=1.0), ik.mat("plaster_white")][tier - 1]
                fl = 1 if L < 3 else 2
                top = apartment(lot, x, by, uw, bd, fl, wall, "gable", "slate" if tier < 3 else "roof_metal",
                                shop=13, sign=lot.pick(SIGNS), doors=(0.3,), roof_h=14)
                P.sign(lot, x + uw * 0.55, by + bd + 0.005, 6, 0.3, 5, lot.pick(SIGNS))
            if L >= 3:
                P.cafe_tables(lot, x + 0.1, D - 0.03, uw * 0.5, 2, awn)
        if by > 0.25:
            lot.pave(0.05, 0.05, W - 0.1, by - 0.1, "paving", layer=1)
            P.dumpster(lot, 0.15, 0.1, "leaf")
            P.crates(lot, 0.4, 0.08, 3)
            if L >= 4:
                P.scatter_trees(lot, W, [(0, by, W, D)], 0.7, ["round"], y1=by)
        if L >= 4:
            for i in range(W):
                P.bush(lot, i + 0.9, D - 0.05, 0.03)
    return lot


def com_high(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    sign = lot.pick(SIGNS)
    lot.lawn("paving")
    if fam == 0:
        # mall / big box: wide 1-3 storeys at the back, car park in front
        floors = {1: 1, 2: 1, 3: 2, 4: 2, 5: 3}[L]
        bd = min(D - 0.75, 1.3 + 0.1 * tier)
        by0 = 0.1
        wall = [ik.mat("concrete", shade=1.2), ik.mat("stone", shade=1.0), ik.mat("plaster", ramp="sand", shade=3)][tier - 1]
        top = apartment(lot, 0.1, by0, W - 0.2, bd, floors, wall, "flat", "roof_flat", shop=14, sign=sign,
                        doors=(0.5,), win=dict(win_w=6, period=12, margin=6), parapet=3, rim=wall,
                        gear=dict(core=True, panels=L == 5))
        # skylight ridges + rooftop plant
        if lot.roofed() and lot.stage == 0 and lot.state != "burnt":
            for i in range(max(1, int((W - 0.6) / 0.45))):
                lot.s.roof_gable(0.35 + i * 0.45, by0 + 0.2, top, 0.22, bd - 0.5, 4,
                                 lot.m(ik.mat("glass_dark", lit=0.5)), axis="y", overhang=0.0,
                                 gable=lot.m("metal_light"))
            P.ac_units(lot, W - 0.5, by0 + 0.1, top, 3)
        # entrance portal
        lot.solid(W / 2 - 0.25, by0 + bd, 0, 0.5, 0.08, top + 4, lot.m("glass", lit=0.8) if lot.live else lot.m("glass_dark"),
                  top=lot.m("concrete"))
        roof_sign(lot, W / 2 - 0.4, by0 + bd + 0.06, top + 4, 0.8, sign, 7)
        parking(lot, 0.1, by0 + bd + 0.14, W - 0.2, D - by0 - bd - 0.18, cars=0.35 + 0.1 * L)
        if L >= 3:
            for i in range(W):
                P.tree(lot, i + 0.5, D - 0.06, 0.7, "round")
    elif fam == 1:
        # hotel: podium + slab tower with name sign at the top
        floors = {1: 7, 2: 9, 3: 11, 4: 13, 5: 16}[L]
        py1 = D - 0.2
        pd = min(D - 0.4, 1.2)
        pod = ik.mat("stone", shade=1.5)
        lot.block(0.12, py1 - pd, 0, W - 0.24, pd, 14, lot.fac(pod, shop=12, sign="yellow", doors=(0.5,)),
                  lot.fac(pod, shop=12, doors=()), lot.fac(pod))
        lot.flat(0.12, py1 - pd, 14, W - 0.24, pd, "roof_flat", parapet=2, rim="concrete")
        tw = min(W - 0.6, 1.6)
        tx = (W - tw) / 2
        ty = py1 - pd + 0.1
        wall = [ik.mat("concrete", shade=1.0), ik.mat("plaster", ramp="sand", shade=3.0), ik.mat("stone", shade=1.5)][tier - 1]
        top = apartment(lot, tx, ty, tw, 0.6, floors, wall, "flat", z0=14, doors=(),
                        win=dict(win_w=4, period=6, margin=3), balcony="metal" if tier >= 2 else None,
                        gear=dict(core=True, antenna=False))
        P.sign(lot, tx + tw * 0.25, ty + 0.6, top - 8, tw * 0.5, 6, sign)
        # canopy at the entrance + flags
        lot.solid(W / 2 - 0.2, py1, 10, 0.4, 0.12, 2, lot.m("plain", ramp=sign, shade=1.0))
        if L >= 3:
            for i in range(3):
                P.flagpole(lot, W / 2 - 0.3 + i * 0.3, D - 0.06, 18, ["red", "glass", "yellow"][i])
        if py1 - pd > 0.3:
            parking(lot, 0.12, 0.05, W - 0.24, min(0.4, py1 - pd - 0.08), cars=0.6)
    elif fam == 2:
        # EU department store: stone block at the sidewalk, big windows, glass corner, rooftop sign
        floors = {1: 3, 2: 4, 3: 5, 4: 5, 5: 6}[L]
        bd = min(D - 0.15, 1.6)
        by = D - 0.06 - bd
        wall = [ik.mat("plaster", ramp="sand", shade=2.5), ik.mat("stone", shade=1.0), ik.mat("stone", shade=2.0)][tier - 1]
        top = apartment(lot, 0.06, by, W - 0.12, bd, floors, wall, "mansard" if tier == 3 else "flat",
                        "slate" if tier == 3 else "roof_flat", shop=14, sign=sign, doors=(0.3, 0.7),
                        win=dict(win_w=5, win_h=6, period=8, margin=4), band=1.2, gear=dict(core=True))
        lot.solid(W - 0.4, by + bd - 0.34, 0, 0.36, 0.36, top + 6, lot.m("glass", lit=0.8), top=lot.m("roof_metal"))
        roof_sign(lot, 0.3, by + bd - 0.1, top + (9 if tier == 3 else 0), min(1.2, W - 0.9), sign, 7)
        for i in range(W):
            P.awning(lot, 0.1 + i * (W - 0.2) / W, by + bd, 14, (W - 0.2) / W * 0.7, sign, depth=0.08)
        if by > 0.3:
            P.truck(lot, 0.2, 0.12, "x")
    else:
        # EU glass galleria: two blocks with a glazed atrium roof between
        floors = {1: 2, 2: 3, 3: 3, 4: 4, 5: 5}[L]
        bd = min(D - 0.2, 1.5)
        by = D - 0.1 - bd
        wing = (W - 0.2) * 0.38
        wall = ik.mat("glass") if tier >= 2 else ik.mat("windows_office")
        for x in (0.1, W - 0.1 - wing):
            f = lot.fac(ik.mat("concrete", shade=1.5), shop=14, sign=sign, doors=(0.5,), win_w=6, period=8) \
                if tier == 1 else (lot.m(wall) if lot.wstate() != "ok" else wall)
            lot.block(x, by, 0, wing, bd, floors * ST + 4, f, f, f)
            lot.flat(x, by, floors * ST + 4, wing, bd, "roof_green" if L >= 4 else "roof_flat", parapet=2,
                     rim="concrete")
        ax0, ax1 = 0.1 + wing, W - 0.1 - wing
        h = floors * ST + 4
        atrium = lot.m(ik.mat("glass", lit=0.9))
        lot.block(ax0, by, 0, ax1 - ax0, bd, h - 4, atrium, lot.m("concrete"), lot.m("concrete"), mass=False)
        if lot.roofed() and lot.state != "burnt":
            lot.s.roof_gable(ax0, by, h - 4, ax1 - ax0, bd, 10, lot.m(ik.mat("glass", lit=0.7)), axis="y",
                             overhang=0.0, gable=atrium)
        roof_sign(lot, ax0, by + bd - 0.05, h + 6, ax1 - ax0, sign, 5)
        if L >= 3:
            for i in range(W):
                P.tree(lot, i + 0.5, D - 0.05, 0.7, "tall")
        if by > 0.35:
            parking(lot, 0.1, 0.05, W - 0.2, min(0.4, by - 0.08), cars=0.5)
    return lot
