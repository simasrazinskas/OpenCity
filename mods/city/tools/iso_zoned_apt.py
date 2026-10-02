"""iso_zoned_apt.py - shared apartment/office block builder for ZONED generators.

apartment() draws one rectangular block: facade with windows (optional shop ground floor), balconies
on the front, a roof (flat with parapet + gear, gable, hip or mansard) and returns the roof height.
"""
import isokit as ik

import iso_zoned_props as P

ST = ik.STOREY

WIN_STD = dict(win_w=4, win_h=5, period=7, sill=3, margin=3)


def balconies(lot, x, y, z0, dx, floors, rail="metal", face="+y", every=0.3, w=0.17, start=1):
    """Front balconies: slab + railing per floor in evenly spaced bays."""
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    n = max(1, int(dx / every))
    sp = dx / n
    slab = lot.m("concrete", shade=1.0)
    rm = {"metal": lot.m("plain", ramp="slate", shade=0.5), "glass": lot.m("plain", ramp="glass", shade=2.5),
          "wood": lot.m("wood", shade=1.0), "white": lot.m("plaster_white")}[rail]
    for f in range(start, floors):
        zz = z0 + f * ST
        for i in range(n):
            bx = x + i * sp + (sp - w) / 2
            if face == "+y":
                lot.s.box(bx, y, zz, w, 0.06, 1, slab)
                lot.s.box(bx, y + 0.05, zz + 1, w, 0.012, 3, rm)
            else:
                lot.s.box(y, bx, zz, 0.06, w, 1, slab)
                lot.s.box(y + 0.05, bx, zz + 1, 0.012, w, 3, rm)


def roof_gear(lot, x, y, z, dx, dy, tank=False, core=True, antenna=False, panels=False, garden=False):
    if not lot.roofed() or lot.stage == 3:
        return
    s = lot.s
    if core:
        lot.box6(x + dx * 0.55, y + dy * 0.25, z, min(0.25, dx * 0.3), min(0.2, dy * 0.4), 7,
                 lot.m("concrete", shade=0.5), top=lot.m("roof_flat"))
    P.ac_units(lot, x + 0.08, y + 0.08, z, 2 if dx > 0.5 else 1)
    if tank and lot.state != "burnt":
        tx, ty = x + dx * 0.25, y + dy * 0.6
        for ox in (-0.04, 0.04):
            for oy in (-0.04, 0.04):
                s.box(tx + ox - 0.006, ty + oy - 0.006, z, 0.012, 0.012, 6, lot.m("plain", ramp="slate"))
        s.cylinder(tx, ty, z + 6, 0.07, 10, lot.m("wood", shade=0.5), segs=12)
        s.cone(tx, ty, z + 16, 0.075, 4, lot.m("roof_metal"), segs=12)
    if antenna:
        s.box(x + dx * 0.3, y + dy * 0.3, z, 0.012, 0.012, 22, lot.m("plain", ramp="slate"))
        if lot.live:
            s.box(x + dx * 0.3 - 0.01, y + dy * 0.3 - 0.01, z + 22, 0.03, 0.03, 2, ik.mat("plain", ramp="red", shade=3))
    if panels:
        P.panels(lot, x + 0.08, y + 0.1, z + 1, dx * 0.4, dy * 0.6)
    if garden and lot.state == "ok":
        lot.s.ground([(x + 0.06, y + dy * 0.55), (x + dx * 0.5, y + dy * 0.55), (x + dx * 0.5, y + dy - 0.06),
                      (x + 0.06, y + dy - 0.06)], "roof_green", z=z + 0.5, layer=2)
        lot.s.blob([(x + 0.14, y + dy - 0.14, z + 4, 0.05, 5)], "foliage", rough=0.4)


def apartment(lot, x, y, dx, dy, floors, wall, roof="flat", roof_mat="roof_flat", z0=0, shop=0, sign="red",
              doors=(0.5,), balcony=None, win=None, side_win=None, parapet=2, rim=None, gear=None,
              shutter=None, frame="snow", band=0, roof_h=None, door="glass"):
    """One block. Returns the roof (eave) height."""
    win = dict(WIN_STD, **(win or {}))
    side_win = dict(win, **(side_win or {}))
    h = floors * ST + (shop and 3) + 1
    front = lot.fac(wall, doors=list(doors), door=door, door_w=5, shop=shop, sign=sign, shutter=shutter,
                    frame=frame, band=band, **win)
    side = lot.fac(wall, shutter=shutter, frame=frame, band=band, **side_win)
    back = lot.fac(wall, doors=[0.5], door="slate", shutter=shutter, frame=frame, band=band, **win)
    lot.block(x, y, z0, dx, dy, h, front, side, back)
    top = z0 + h
    if balcony:
        balconies(lot, x, y + dy, z0 + (shop and 3), dx, floors, balcony)
    if roof == "flat":
        lot.flat(x, y, top, dx, dy, roof_mat, parapet=parapet, rim=rim or wall)
        roof_gear(lot, x, y, top, dx, dy, **(gear or {}))
    elif roof == "gable":
        lot.gable(x, y, top, dx, dy, roof_h or (8 + 10 * min(dx, dy)), roof_mat,
                  axis="x" if dx >= dy else "y", gable=lot.m(wall))
    elif roof == "hip":
        lot.hip(x, y, top, dx, dy, roof_h or (6 + 10 * min(dx, dy)), roof_mat)
    elif roof == "mansard":
        mh = roof_h or 9
        lot.mansard(x, y, top, dx, dy, mh, 0.07, roof_mat, top="roof_flat")
        if lot.roofed() and lot.state != "burnt":
            n = max(1, int(dx / 0.22))
            for i in range(n):
                bx = x + (i + 0.5) * dx / n - 0.04
                lot.box6(bx, y + dy - 0.06, top + 1, 0.08, 0.04, 6,
                         lot.fac(wall, win_w=2, win_h=4, sill=1, margin=0, period=4), lot.m(wall))
    return top


def parking(lot, x, y, dx, dy, cars=0.6, along="y"):
    """Asphalt car park with bay lines and parked cars."""
    lot.pave(x, y, dx, dy, "asphalt", layer=1)
    if lot.stage:
        return
    stripe = lot.m("plain", ramp="snow", shade=0.0)
    if along == "y":
        n = int(dx / 0.2)
        for i in range(n + 1):
            lot.s.ground([(x + i * 0.2, y + 0.04), (x + i * 0.2 + 0.015, y + 0.04),
                          (x + i * 0.2 + 0.015, y + 0.34), (x + i * 0.2, y + 0.34)], stripe, layer=2)
        for i in range(n):
            if lot.rng.chance(cars):
                P.car(lot, x + i * 0.2 + 0.1, y + 0.2, "y")
    else:
        n = int(dy / 0.2)
        for i in range(n + 1):
            lot.s.ground([(x + 0.04, y + i * 0.2), (x + 0.34, y + i * 0.2), (x + 0.34, y + i * 0.2 + 0.015),
                          (x + 0.04, y + i * 0.2 + 0.015)], stripe, layer=2)
        for i in range(n):
            if lot.rng.chance(cars):
                P.car(lot, x + 0.2, y + i * 0.2 + 0.1, "x")
