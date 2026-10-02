"""iso_zoned_off.py - offices low and high (ZONED).

off (low) families: v0 NA suburban office (brick/stone, ribbon windows, car park), v1 NA glass box,
v2 EU classic stone/plaster office (string courses, hip roof), v3 EU modern white with ribbon windows
and green roof. Storeys 2/2/3/4/5 (fewer on 1-cell lots).
off-high families: v0 NA classic (stone base, setback shaft, spire), v1 NA modern glass slab, v2 EU
stepped glass tower with sky gardens, v3 EU slender tower with vertical fins. Storeys 12-26.
"""
import isokit as ik

import iso_zoned_props as P
from iso_zoned_apt import apartment, parking, roof_gear
from iso_zoned_reshigh import helipad, fountain

ST = ik.STOREY
RIBBON = dict(win_w=7, win_h=5, period=8, margin=2, sill=3)


def glass_box(lot, x, y, z0, dx, dy, floors, mat="glass", lit=0.55, ground=None):
    """Curtain-wall volume; follows the lot state (abandoned glass gets grimy windows instead)."""
    h = floors * ST + 1
    if lot.wstate() == "ok":
        g = ik.mat(mat, lit=lit)
    else:
        g = lot.fac(ik.mat("concrete", shade=0.5), win_w=6, period=7, margin=1)
    lot.block(x, y, z0, dx, dy, h, g, g, g)
    return z0 + h


def off_low(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    small = W * D == 1
    floors = {1: 2, 2: 2, 3: 3, 4: 4, 5: 5}[L] - (1 if small and L >= 3 else 0)
    sign = lot.pick(["glass", "teal", "red", "slate", "leaf"])
    lot.lawn("grass")
    bw = min(W - 0.24, 0.62 + 0.55 * (W - 1) + 0.05 * tier)
    bd = min(D - 0.4, 0.5 + 0.45 * (D - 1))
    bx = (W - bw) / 2
    by = D - 0.25 - bd
    lot.pave(0, D - 0.25, W, 0.25, "paving", layer=1)
    if fam == 0:
        wall = [ik.mat("brick_yellow"), ik.mat("brick", shade=-0.3), ik.mat("stone", shade=1.0)][tier - 1]
        top = apartment(lot, bx, by, bw, bd, floors, wall, "flat", "roof_flat", doors=(0.5,), win=RIBBON,
                        band=1.0, parapet=2, rim=ik.mat("stone", shade=1.5), gear=dict(core=False))
        lot.solid(bx + bw / 2 - 0.12, by + bd, 0, 0.24, 0.1, 12, lot.m("glass", lit=0.8), top=lot.m("concrete"))
    elif fam == 1:
        top = glass_box(lot, bx, by, 0, bw, bd, floors, "glass_dark" if tier < 3 else "glass")
        lot.flat(bx, by, top, bw, bd, "roof_flat", parapet=2, rim="metal_light")
        roof_gear(lot, bx, by, top, bw, bd, core=False)
    elif fam == 2:
        wall = [ik.mat("plaster", ramp="sand", shade=3.0), ik.mat("stone", shade=1.0), ik.mat("stone", shade=2.0)][tier - 1]
        top = apartment(lot, bx, by, bw, bd, floors, wall, "hip", "slate" if tier >= 2 else "roof_tiles",
                        doors=(0.5,), band=1.2, win=dict(win_w=4, win_h=6, period=7, margin=3, sill=2))
        lot.solid(bx + bw / 2 - 0.08, by + bd, 0, 0.16, 0.06, 13, lot.m(wall))
    else:
        wall = ik.mat("plaster_white") if tier >= 2 else ik.mat("concrete", shade=1.5)
        top = apartment(lot, bx, by, bw, bd, floors, wall, "flat", "roof_green" if L >= 4 else "roof_flat",
                        doors=(0.3,), win=RIBBON, frame="slate", gear=dict(core=False, panels=L == 5))
        if tier >= 2:
            lot.solid(bx - 0.0, by + bd, floors * ST - 9, bw, 0.05, 1, lot.m("wood", shade=1.0))
    P.sign(lot, bx + 0.05, by + bd + 0.005, 3, min(0.25, bw * 0.3), 4, sign)
    # grounds
    if by > 0.4 and fam in (0, 1):
        parking(lot, 0.1, 0.06, W - 0.2, min(0.4, by - 0.1), cars=0.4 + 0.1 * tier)
    elif by > 0.3:
        P.scatter_trees(lot, 1 + L // 2, [(bx, by, bw, bd + 0.3)], 0.9, ["round", "tall"], y1=by)
    elif not small or True:
        P.car(lot, W - 0.12, D - 0.12, "y")
    if L >= 2:
        for i in range(max(2, int(bw / 0.15))):
            P.bush(lot, bx + 0.04 + i * 0.15, D - 0.18, 0.03)
    if L >= 4:
        P.tree(lot, 0.1, D - 0.1, 0.8, "round")
        if W > 1:
            P.tree(lot, W - 0.1, D - 0.1, 0.8, "round")
        P.flagpole(lot, bx + bw + 0.06 if bx + bw + 0.1 < W else bx - 0.06, D - 0.15, 16, sign)
    return lot


def off_high(lot):
    W, D, L = lot.W, lot.D, lot.level
    fam = lot.var % 4
    tier = (L + 1) // 2
    floors = {1: 12, 2: 14, 3: 17, 4: 20, 5: 24}[L] + (2 if W * D >= 9 else 0)
    lot.lawn("paving")
    tw = min(W - 0.5, 1.0 + 0.3 * (W - 2))
    td = min(D - 0.6, 0.9 + 0.3 * (D - 2))
    tx = (W - tw) / 2
    ty = D - 0.35 - td
    # plaza + base
    lot.pave(0, ty + td, W, D - ty - td, "paving", layer=1)
    if fam == 0:
        base = ik.mat("stone", shade=1.0)
        lot.block(tx - 0.08, ty - 0.08, 0, tw + 0.16, td + 0.16, 3 * ST + 2,
                  lot.fac(base, doors=(0.5,), door_w=7, win_w=4, win_h=7, period=7), lot.fac(base, win_w=4, win_h=7, period=7))
        lot.flat(tx - 0.08, ty - 0.08, 3 * ST + 2, tw + 0.16, td + 0.16, "roof_flat", parapet=2, rim=base)
        shaft = ik.mat("stone", shade=1.5) if tier < 3 else ik.mat("stone", ramp="sand", shade=2.0)
        f = lot.fac(shaft, win_w=3, win_h=6, period=5, margin=2, sill=2, frame="stone")
        z = 3 * ST + 2
        h1 = int(floors * 0.65) * ST
        lot.block(tx, ty, z, tw, td, h1, f, f, f)
        z += h1
        lot.block(tx + 0.12, ty + 0.12, z, tw - 0.24, td - 0.24, (floors - int(floors * 0.65) - 3) * ST, f, f, f)
        z += (floors - int(floors * 0.65) - 3) * ST
        lot.flat(tx, ty, 3 * ST + 2 + h1, tw, td, "roof_flat", parapet=2, rim=shaft)
        lot.flat(tx + 0.12, ty + 0.12, z, tw - 0.24, td - 0.24, "roof_flat", parapet=2, rim=shaft)
        if lot.roofed() and lot.state != "burnt" and L >= 3:
            c = (tx + tw / 2, ty + td / 2)
            lot.s.pyramid(c[0] - 0.15, c[1] - 0.15, z, 0.3, 0.3, 18, lot.m("roof_metal"))
            lot.s.box(c[0] - 0.01, c[1] - 0.01, z + 18, 0.02, 0.02, 14 + 4 * L, lot.m("metal_light"))
    elif fam == 1:
        z = glass_box(lot, tx, ty, 0, tw, td, floors, "glass" if tier >= 2 else "glass_dark")
        lot.flat(tx, ty, z, tw, td, "roof_flat", parapet=3, rim="metal_light")
        roof_gear(lot, tx, ty, z, tw, td, core=True, antenna=L >= 4)
        lot.solid(tx + tw / 2 - 0.2, ty + td, 0, 0.4, 0.15, 12, lot.m("concrete", shade=1.5))
    elif fam == 2:
        z = 0
        n1 = int(floors * 0.45)
        z = glass_box(lot, tx, ty, 0, tw, td, n1, "glass")
        lot.flat(tx, ty, z, tw, td, "roof_green", parapet=2, rim="metal_light")
        if lot.live:
            lot.s.blob([(tx + 0.12, ty + td - 0.12, z + 4, 0.06, 6)], "foliage", rough=0.35)
        z2 = glass_box(lot, tx + 0.15, ty, z, tw - 0.15, td - 0.15, floors - n1, "glass_dark" if tier < 3 else "glass")
        lot.flat(tx + 0.15, ty, z2, tw - 0.15, td - 0.15, "roof_flat", parapet=3, rim="metal_light")
        helipad(lot, tx + 0.25, ty + 0.1, z2, min(tw, td) * 0.5) if L == 5 else roof_gear(lot, tx + 0.15, ty, z2, tw - 0.15, td - 0.15, core=False)
    else:
        z = glass_box(lot, tx, ty, 0, tw, td, floors, "glass")
        lot.flat(tx, ty, z, tw, td, "roof_flat", parapet=4, rim="metal_light")
        if lot.walls_visible and lot.state != "collapsed":
            fin = lot.m("plaster_white")
            n = max(3, int(tw / 0.12))
            for i in range(n + 1):
                lot.s.box(tx + i * (tw - 0.02) / n, ty + td, 0, 0.02, 0.04, z + 4, fin)
            n = max(3, int(td / 0.12))
            for i in range(n + 1):
                lot.s.box(tx + tw, ty + i * (td - 0.02) / n, 0, 0.04, 0.02, z + 4, fin)
        roof_gear(lot, tx, ty, z, tw, td, core=True, panels=L >= 4)
    # grounds
    if L >= 2:
        for i in range(W):
            P.tree(lot, i + 0.5, D - 0.08, 0.7, "round")
    if L >= 4:
        fountain(lot, 0.3, D - 0.3, 0.1)
    if ty > 0.4:
        parking(lot, 0.1, 0.05, W - 0.2, min(0.4, ty - 0.1), cars=0.6)
    return lot
