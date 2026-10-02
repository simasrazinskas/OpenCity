"""iso_zoned_sig.py - signature buildings (3x3, level 5 landmarks, NA + EU variant each).

sig-villa: grand mansion with portico, wings, forecourt fountain, formal garden, pool.
sig-garden-row: U of townhouses around a communal garden.
sig-sky-tower: 30+ storey glass tower with sky gardens and a spire.
sig-bazaar: market hall with clock tower and striped stalls on a plaza.
sig-mall: mall with glass dome and car park. sig-glass-hub: glass office campus with a round tower.
sig-fuel-plant: refinery with distillation columns, sphere tanks, pipe racks and a flare stack.
"""
import isokit as ik

import iso_zoned_mats as M
import iso_zoned_props as P
from iso_zoned_apt import apartment, parking
from iso_zoned_reshigh import fountain, helipad
from iso_zoned_off import glass_box
from iso_zoned_com import roof_sign

ST = ik.STOREY


def _columns(lot, x, y, w, h, n=4, mat="plaster_white"):
    for i in range(n):
        lot.solid(x + i * (w - 0.03) / (n - 1), y, 0, 0.03, 0.03, h, mat)


def sig_villa(lot):
    eu = lot.eu
    lot.lawn("grass")
    wall = ik.mat("stone", shade=1.5) if eu else ik.mat("siding_white")
    roof = "slate"
    # forecourt with gravel loop and fountain
    lot.pave(0.9, 1.75, 1.2, 1.25, "gravel", layer=1)
    lot.pave(1.35, 1.6, 0.3, 1.4, "gravel", layer=1)
    fountain(lot, 1.5, 2.35, 0.16)
    # main house + wings
    top = apartment(lot, 0.85, 0.9, 1.3, 0.7, 2, wall, "mansard" if eu else "hip", roof, doors=(0.5,),
                    shutter=None if eu else "slate", win=dict(win_w=3, win_h=6, period=7, sill=2), roof_h=12)
    for x in (0.3, 2.15):
        apartment(lot, x, 1.0, 0.55, 0.55, 2, wall, "hip", roof, doors=(), shutter=None if eu else "slate",
                  win=dict(win_w=3, win_h=6, period=7, sill=2), roof_h=9)
    # portico with pediment
    _columns(lot, 1.3, 1.6, 0.4, 2 * ST, 4)
    lot.solid(1.28, 1.58, 2 * ST, 0.44, 0.1, 2, "plaster_white")
    lot.gable(1.28, 1.58, 2 * ST + 2, 0.44, 0.1, 7, "slate", axis="y", overhang=0.01,
              gable=lot.m("plaster_white"))
    P.chimney(lot, 1.0, 1.1, top, 18, "brick" if not eu else "stone", 0.07)
    P.chimney(lot, 1.9, 1.1, top, 18, "brick" if not eu else "stone", 0.07)
    # formal garden: hedges + topiary along the front, pool behind
    for x0 in (0.15, 2.15):
        P.hedge(lot, x0, 1.9, 0.7, 0.06, 4)
        P.hedge(lot, x0, 2.6, 0.7, 0.06, 4)
        for i in range(3):
            P.bush(lot, x0 + 0.12 + i * 0.23, 2.25, 0.05)
    P.pool(lot, 0.6, 0.2, 0.9, 0.35)
    lot.pave(1.6, 0.2, 0.6, 0.35, "paving", layer=1)
    P.scatter_trees(lot, 7, [(0.25, 0.15, 2.5, 1.5), (0.85, 1.6, 1.3, 1.4)], 1.1, ["round", "tall", "conifer"])
    P.fence(lot, 0.03, 2.95, 1.25, 0.03, "wall")
    P.fence(lot, 1.75, 2.95, 1.22, 0.03, "wall")
    for x in (1.2, 1.8):
        P.lamp(lot, x, 2.92, 10)
    for i in range(2):
        P.car(lot, 1.42 + i * 0.16, 1.85, "y", ["slate", "snow"][i])
    return lot


def sig_garden_row(lot):
    eu = lot.eu
    lot.lawn("grass")
    wall = ik.mat("brick", shade=-0.3) if eu else ik.mat("brick", ramp="wood", shade=-0.8)
    roof = "slate"
    depth = 0.45
    # back row and two side rows (units 0.5 wide), all facing the garden
    for i in range(5):
        x = 0.25 + i * 0.5
        apartment(lot, x, 0.15, 0.5, depth, 3, wall, "gable" if eu else "flat", roof if eu else "roof_flat",
                  doors=(0.3,), door=["red", "teal", "glass", "yellow", "leaf"][i], win=dict(win_w=3, period=6),
                  roof_h=12, gear=dict(core=False))
    for side, x in ((0, 0.15), (1, 2.85 - depth)):
        for j in range(4):
            y = 0.7 + j * 0.5
            apartment(lot, x, y, depth, 0.5, 3, wall, "gable" if eu else "flat", roof if eu else "roof_flat",
                      doors=(), win=dict(win_w=3, period=6), roof_h=12, gear=dict(core=False))
    # communal garden: paths, lawn, pergola, trees, benches
    lot.pave(1.4, 0.6, 0.2, 2.4, "paving", layer=1)
    lot.pave(0.6, 1.6, 1.8, 0.2, "paving", layer=1)
    lot.pave(1.2, 1.4, 0.6, 0.6, "paving", layer=2)
    fountain(lot, 1.5, 1.7, 0.12)
    for (x, y) in ((0.85, 1.0), (2.15, 1.0), (0.85, 2.4), (2.15, 2.4)):
        P.tree(lot, x, y, 1.1, "round")
    for i in range(4):
        P.bench(lot, 1.2 + i * 0.18, 1.32)
    for x in (0.7, 2.0):
        lot.solid(x, 2.0, 0, 0.3, 0.02, 9, "wood")
        lot.solid(x, 2.25, 0, 0.3, 0.02, 9, "wood")
        lot.solid(x, 2.0, 9, 0.3, 0.27, 1, "wood")
    P.fence(lot, 0.15, 2.95, 1.2, 0.03, "rail")
    P.fence(lot, 1.65, 2.95, 1.2, 0.03, "rail")
    for i in range(6):
        P.bush(lot, 0.7 + i * 0.3, 1.25, 0.04)
    return lot


def sig_sky_tower(lot):
    lot.lawn("paving")
    tx, ty, tw = 0.85, 0.75, 1.3
    z = 0
    seg = [(12, 0.0), (10, 0.12), (8, 0.24)]
    if lot.eu:
        # EU: slimmer white-framed tower with a tilted crown and sky-garden notches
        seg = [(9, 0.0), (1, 0.18), (9, 0.04), (1, 0.18), (9, 0.08)]
    for k, (fl, inset) in enumerate(seg):
        x, y, w = tx + inset, ty + inset, tw - 2 * inset
        if lot.eu and fl == 1:
            z2 = glass_box(lot, x, y, z, w, w, 1, "glass_dark", lit=0.9)
            if lot.live:
                lot.s.blob([(x + 0.1, y + w - 0.1, z + 3, 0.08, 5), (x + w - 0.1, y + w - 0.1, z + 3, 0.08, 5)],
                           "foliage", rough=0.35)
            z = z2
            continue
        z2 = glass_box(lot, x, y, z, w, w, fl, ("glass" if k != 1 else "glass_dark") if not lot.eu else "glass", lit=0.6)
        if lot.eu and lot.walls_visible and lot.state != "collapsed":
            fin = lot.m("plaster_white")
            for i in range(5):
                lot.s.box(x + i * (w - 0.025) / 4, y + w, z, 0.025, 0.03, z2 - z, fin)
                lot.s.box(x + w, y + i * (w - 0.025) / 4, z, 0.03, 0.025, z2 - z, fin)
        lot.flat(x, y, z2, w, w, "roof_green", parapet=2, rim="metal_light")
        if lot.live and k < 2:
            lot.s.blob([(x + 0.12, y + w - 0.12, z2 + 4, 0.07, 6), (x + w - 0.12, y + w - 0.12, z2 + 4, 0.07, 6)],
                       "foliage", rough=0.35)
        z = z2
    if lot.roofed() and lot.state != "burnt" and lot.eu:
        lot.shed(tx + 0.08, ty + 0.08, z, tw - 0.16, tw - 0.16, 24, ik.mat("glass", lit=0.7), high="-y",
                 wall=ik.mat("glass", lit=0.7))
    elif lot.roofed() and lot.state != "burnt":
        c = tx + tw / 2
        lot.s.cylinder(c, ty + tw / 2, z, 0.12, 8, lot.m("metal_light"), segs=12)
        lot.s.cone(c, ty + tw / 2, z + 8, 0.08, 60, lot.m("metal_light"), segs=10)
        if lot.live:
            lot.s.sphere(c, ty + tw / 2, z + 70, 0.015, ik.mat("plain", ramp="red", shade=4))
    # plaza
    fountain(lot, 0.55, 2.5, 0.2)
    for i in range(5):
        P.tree(lot, 0.25 + i * 0.6, 2.85, 0.8, "round")
    for i in range(3):
        P.tree(lot, 2.7, 0.4 + i * 0.6, 0.9, "tall")
    lot.pave(0.3, 2.1, 2.4, 0.2, "paving", layer=2)
    return lot


def sig_bazaar(lot):
    eu = lot.eu
    lot.lawn("paving")
    wall = ik.mat("brick", ramp="terra" if not eu else "brick", shade=0.3)
    # market hall with a glazed roof
    hx, hy, hw, hd = 0.4, 0.3, 1.9, 1.0
    lot.block(hx, hy, 0, hw, hd, 16, lot.fac(wall, doors=(0.3, 0.5, 0.7), door_w=6, win_w=5, win_h=8, period=9, sill=3),
              lot.fac(wall, win_w=5, win_h=8, period=9, sill=3), lot.m(wall))
    lot.gable(hx, hy, 16, hw, hd, 18, ik.mat("glass_dark", lit=0.6), axis="x", overhang=0.04, gable=lot.m(wall))
    # clock tower
    cx, cy = 2.35, 1.05
    lot.block(cx, cy, 0, 0.3, 0.3, 52, lot.fac(wall, win_w=3, win_h=6, period=8, floors=4), lot.fac(wall, win_w=3, win_h=6, period=8))
    if lot.walls_visible and lot.state != "collapsed":
        for (px, py, nx) in ((cx + 0.15, cy + 0.31, (0, 1, 0)), (cx + 0.31, cy + 0.15, (1, 0, 0))):
            lot.s.cylinder(px, py, 44, 0.08, 0.01, lot.m("plaster_white"), axis="y" if nx[1] else "x", segs=12)
    lot.hip(cx, cy, 52, 0.3, 0.3, 16, "roof_metal" if not eu else "slate")
    # stalls with striped awnings around the plaza
    cols = ["red", "teal", "yellow", "leaf", "purple", "glass", "terra"]
    i = 0
    for row_y in (1.75, 2.35):
        for k in range(5):
            x = 0.3 + k * 0.48
            c = cols[i % len(cols)]
            i += 1
            lot.solid(x, row_y, 0, 0.32, 0.2, 5, ik.mat("wood", shade=0.5), top="wood")
            if lot.live:
                P.crates(lot, x + 0.04, row_y + 0.21, 2)
            if lot.state != "burnt" and lot.stage == 0:
                lot.s.roof_gable(x - 0.02, row_y - 0.02, 9, 0.36, 0.24, 4, M.stripes(c), axis="x", overhang=0.0)
                for (px, py) in ((x, row_y), (x + 0.3, row_y), (x, row_y + 0.18), (x + 0.3, row_y + 0.18)):
                    lot.solid(px, py, 5, 0.012, 0.012, 4, "wood")
    for x in (0.2, 2.8):
        P.tree(lot, x, 2.85, 0.9, "round")
    P.lamp(lot, 1.5, 2.9, 12)
    return lot


def sig_mall(lot):
    lot.lawn("asphalt")
    wall = ik.mat("plaster", ramp="sand", shade=3.0) if not lot.eu else ik.mat("stone", shade=1.5)
    top = apartment(lot, 0.2, 0.2, 2.6, 1.5, 3, wall, "flat", "roof_flat", shop=14, sign="purple", doors=(0.3, 0.7),
                    win=dict(win_w=6, period=12, margin=6), parapet=3, rim=wall, gear=dict(core=False))
    if lot.roofed() and lot.state != "burnt":
        lot.s.cylinder(1.5, 0.95, top, 0.5, 6, lot.m("concrete", shade=1.0), segs=24)
        lot.s.dome(1.5, 0.95, top + 6, 0.48, 24, ik.mat("glass", lit=0.8))
    lot.solid(1.2, 1.7, 0, 0.6, 0.1, top + 6, lot.m("glass", lit=0.9), top=lot.m("concrete"))
    roof_sign(lot, 0.4, 1.62, top, 0.7, "purple", 7)
    roof_sign(lot, 1.9, 1.62, top, 0.7, "red", 7)
    parking(lot, 0.15, 1.95, 2.7, 0.8, cars=0.7)
    for i in range(6):
        P.tree(lot, 0.2 + i * 0.52, 2.92, 0.8, "round")
    return lot


def sig_glass_hub(lot):
    lot.lawn("grass")
    # low glass ring (L-shape) + round tower
    z = glass_box(lot, 0.3, 0.3, 0, 2.2, 0.6, 4, "glass")
    lot.flat(0.3, 0.3, z, 2.2, 0.6, "roof_green", parapet=2, rim="metal_light")
    z = glass_box(lot, 0.3, 0.9, 0, 0.6, 1.3, 4, "glass")
    lot.flat(0.3, 0.9, z, 0.6, 1.3, "roof_green", parapet=2, rim="metal_light")
    if lot.walls_visible and lot.state != "collapsed":
        g = ik.mat("glass", lit=0.6) if lot.wstate() == "ok" else lot.m("glass_dark")
        lot.masses.append((1.45, 1.25, 0, 0.9, 0.9, 150))
        lot.s.cylinder(1.9, 1.7, 0, 0.42, 150, g, segs=28, caps=False)
        lot.s.cylinder(1.9, 1.7, 150, 0.44, 4, lot.m("metal_light"), segs=28)
        lot.s.cylinder(1.9, 1.7, 154, 0.3, 10, lot.m("glass_dark"), segs=24)
    lot.pave(1.2, 2.2, 0.25, 0.8, "paving", layer=1)
    lot.pave(0.2, 2.5, 2.6, 0.2, "paving", layer=1)
    P.scatter_trees(lot, 8, [(0.3, 0.3, 2.2, 0.6), (0.3, 0.9, 0.6, 1.3), (1.4, 1.2, 1.0, 1.0), (1.2, 2.2, 0.25, 0.8)],
                    1.0, ["round", "tall"])
    fountain(lot, 0.6, 2.7, 0.12)
    return lot


def sig_fuel_plant(lot):
    lot.lawn("gravel")
    s = lot.s
    # tank farm
    for i, (x, y) in enumerate(((0.45, 0.45), (1.05, 0.45), (0.45, 1.05), (1.05, 1.05))):
        P.tank(lot, x, y, 0.24, 18, "metal_light")
    # sphere tanks on legs
    if lot.stage not in (1, 2):
        for x in (2.05, 2.6):
            for (ox, oy) in ((-0.1, 0), (0.1, 0), (0, -0.1), (0, 0.1)):
                lot.solid(x + ox, 0.55 + oy, 0, 0.02, 0.02, 10, "metal")
            if lot.state != "collapsed":
                s.sphere(x, 0.55, 18, 0.2, lot.m("plaster_white"))
    # distillation columns
    for i, (x, h) in enumerate(((1.75, 70), (2.05, 90), (2.35, 60))):
        if lot.stage not in (1, 2):
            s.cylinder(x, 1.55, 0, 0.07, h if lot.state != "collapsed" else 15, lot.m("metal_light", shade=0.5), segs=12)
            if lot.state == "ok":
                for k in range(10, h - 5, 14):
                    s.cylinder(x, 1.55, k, 0.085, 2, ik.mat("plain", ramp="slate"), segs=12)
    lot.masses.append((1.6, 1.4, 0, 0.9, 0.3, 90))
    # pipe racks
    if lot.stage not in (1, 2) and lot.state != "collapsed":
        for y in (1.85, 1.95):
            s.cylinder(0.3, y, 9, 0.025, 2.4, lot.m("metal", ramp="yellow", shade=0.5), axis="x", segs=8)
        for x in (0.4, 1.0, 1.6, 2.2):
            lot.solid(x, 1.82, 0, 0.02, 0.18, 8, "metal")
    # flare stack
    if lot.stage not in (1, 2):
        s.cylinder(0.4, 2.55, 0, 0.04, 80, lot.m("metal_light", shade=-0.5), segs=8)
        if lot.live:
            s.blob([(0.4, 2.55, 84, 0.035, 6)], M.lamp_mat(), rough=0.4)
    # control building + fence
    apartment(lot, 1.2, 2.3, 0.8, 0.4, 2, ik.mat("concrete", shade=1.5), "flat", doors=(0.5,),
              win=dict(win_w=6, period=8), gear=dict(core=False))
    P.truck(lot, 2.2, 2.55, "x", "red")
    P.fence(lot, 0.03, 0.03, 2.94, 0.02, "chain")
    P.fence(lot, 0.03, 0.05, 0.02, 2.9, "chain")
    return lot


SIGS = {
    "sig-villa": ("Villa", sig_villa), "sig-garden-row": ("Garden Row", sig_garden_row),
    "sig-sky-tower": ("Sky Tower", sig_sky_tower), "sig-bazaar": ("Bazaar", sig_bazaar),
    "sig-mall": ("Mall", sig_mall), "sig-glass-hub": ("Glass Hub", sig_glass_hub),
    "sig-fuel-plant": ("Fuel Plant", sig_fuel_plant),
}
