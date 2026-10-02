"""iso_zoned_res.py - residential low density and row houses (ZONED).

Model functions take a Lot (W = frontage along X, D = depth along Y, front edge y = D) and draw
the level/variant/theme given on it. Levels: 1 starter bungalow -> 5 large family home with pool.
"""
import isokit as ik

import iso_zoned_props as P

ST = ik.STOREY

NA_SIDING = [("snow", 0.0), ("glass", 2.5), ("yellow", 2.5), ("olive", 3.0), ("sand", 2.5), ("slate", 3.0)]
EU_PLASTER = [("stone", 2.5), ("sand", 3.0), ("yellow", 2.0), ("terra", 3.0), ("snow", 0.0), ("rose", 3.5)]
NA_ROOF = ["shingle", "roof_tiles_brown", "slate", "shingle"]
EU_ROOF = ["roof_tiles", "roof_tiles", "slate", "roof_tiles_brown"]
DOORS = ["red", "teal", "glass", "wood", "yellow", "slate"]
SHUTTERS = ["teal", "leaf", "glass", "wood", "slate"]


def walls(lot, quality):
    """Wall base material for the lot's theme; quality 0..1 picks nicer materials."""
    if lot.eu:
        if quality > 0.6 and lot.chance(0.4):
            return ik.mat("brick", shade=lot.r(-0.5, 0.5))
        r, sh = lot.pick(EU_PLASTER)
        return ik.mat("plaster", ramp=r, shade=sh)
    if quality > 0.6 and lot.chance(0.35):
        return ik.mat("brick", shade=lot.r(-0.3, 0.6))
    r, sh = lot.pick(NA_SIDING)
    return ik.mat("siding", ramp=r, shade=sh)


def house(lot, x, y, dx, dy, floors, wall, roof, roof_kind="gable", axis="x", door_at=0.5,
          garage=None, roof_h=None, shutter=None, door="red", win=None):
    """A detached house volume with roof. garage: u fraction for a garage door on the front."""
    h = floors * ST + 1
    win = win or {}
    common = dict(frame="snow", shutter=shutter, **win)
    front = lot.fac(wall, doors=[door_at] if door_at is not None else [], door=door,
                    garage=(garage, 10) if garage is not None else None, **common)
    side = lot.fac(wall, **common)
    back = lot.fac(wall, doors=[0.3], door="wood", **common)
    lot.block(x, y, 0, dx, dy, h, front, side, back)
    rh = roof_h or (8 + 4 * min(dx, dy) / 0.4)
    gab = lot.m(wall)
    if roof_kind == "gable":
        lot.gable(x, y, h, dx, dy, rh, roof, axis=axis, gable=gab)
    elif roof_kind == "hip":
        lot.hip(x, y, h, dx, dy, rh * 0.85, roof)
    else:
        lot.flat(x, y, h, dx, dy, "roof_gravel", parapet=2, rim=wall)
    return h


