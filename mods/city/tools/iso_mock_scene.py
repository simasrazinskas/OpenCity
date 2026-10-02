"""Compose the mockup City into a screen image with every workstream's real art (isokit.World)."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_mock_assets as A  # noqa: E402
import iso_mock_city as MC  # noqa: E402
from isokit.tiles import SIDE_OFF, CORNER_OFF, CORNER_SIDES  # noqa: E402

SKY = {False: (0, 0, 0, 255), True: (10, 14, 30, 255)}


class Mode:
    def __init__(self, night=False, season="summer", tf=None, info=None, frame=0):
        self.night, self.season, self.tf, self.info, self.frame = night, season, tf, info, frame


def _blob(city, x, y, same):
    def at(c):
        return city.terr.get(c, "grass") == "water"
    me = at((x, y))
    mask = sum(b for b, (dx, dy) in SIDE_OFF.items() if at((x + dx, y + dy)) != me)
    corners = 0
    for b, (dx, dy) in CORNER_OFF.items():
        a, c = CORNER_SIDES[b]
        if not (mask & a) and not (mask & c) and at((x + dx, y + dy)) != me:
            corners |= b
    return mask, corners


def ground_img(city, c, m):
    x, y = c
    n, s = m.night, m.season
    if c in city.roads:
        r = city.roads[c]
        if r.get("bridge"):
            return None
        if r.get("crossing"):
            return A.rail(0, n, s, crossing="ew")
        return A.road(r["cls"], city.mask(c), n, s, r["ctrl"], (x * 7 + y) % 13)
    if c in city.rail:
        return A.rail(city.rail_mask(c), n, s)
    t = city.terr.get(c, "grass")
    var = (x * 3 + y * 5) % 4
    if t == "water" or t == "grass":
        mask, corners = _blob(city, x, y, None)
        if mask or corners:
            style = "bank" if y <= 38 and x >= 12 else "beach"
            if t == "water":
                style = "bank" if y == MC.RIVER[0] else "beach"
            return A.coast("water" if t == "water" else "land", mask, corners, style, n, s, m.frame)
        if t == "water":
            return A.coast("water", 0, 0, "beach", n, s, m.frame)
        return A.ground("grass", var, n, s)
    if t.startswith("field:"):
        _, crop, stage, axis = t.split(":")
        return A.field(crop, stage, axis, n, s)
    return A.ground(t, var, n, s)


def compose(city, m, W=1920, H=1080, centre=(32, 32), cull=True):
    ox = W / 2 - (centre[0] - centre[1]) * 32
    oy = H / 2 - (centre[0] + centre[1]) * 16
    w = ik.World(W, H, int(ox), int(oy), bg=SKY[m.night])
    tf = m.tf or (lambda im: im)

    def vis(x, y, mg=160):
        return w.visible(x, y, mg)
    for x in range(MC.N):
        for y in range(MC.N):
            if not vis(x + 0.5, y + 0.5, 80):
                continue
            g = ground_img(city, (x, y), m)
            if g is None:
                g = A.coast("water", 0, 0, "beach", m.night, m.season, m.frame)
            elif (x, y) in city.roads or (x, y) in city.rail:
                base = "dirt" if city.district(x, y) == "farm" else "grass"
                w.ground(tf(A.ground(base, (x + y) % 4, m.night, m.season)), 32, 16, x + 0.5, y + 0.5, layer=-1)
            w.ground(tf(g), 32, 16, x + 0.5, y + 0.5)
            if m.info is not None and (x, y) in m.info:
                w.ground(m.info[(x, y)], 32, 16, x + 0.5, y + 0.5, layer=4)
    for (x, y), r in city.roads.items():
        if r.get("bridge") and vis(x + 0.5, y + 0.5):
            axis = "ns" if city.mask((x, y)) & 5 else "ew"
            img, ax, ay = A.bridge(r["cls"], axis, r["bridge"], m.night, m.season)
            w.obj(tf(img), ax, ay, x + 0.5, y + 0.5, bias=-0.6)
    for b in city.buildings:
        if not vis(b["cx"] + b["fx"] / 2, b["cy"] + b["fy"] / 2, 400):
            continue
        img, ax, ay = building_img(b, m)
        b["_img"] = (img, ax, ay)
        w.building(tf(img), ax, ay, b["cx"], b["cy"], b["fx"], b["fy"])
    for (x, y, sp, st, sd) in city.trees:
        if vis(x, y):
            img, ax, ay = A.tree(sp, m.season, st, sd % 6, m.night)
            w.obj(tf(img), ax, ay, x, y)
    for (kind, x, y, sd) in city.small:
        if vis(x, y):
            img, ax, ay = A.nature(kind, m.season, sd % 4, m.night)
            w.obj(tf(img), ax, ay, x, y)
    for (name, cx, cy, f, arg) in city.props:
        if vis(cx + 0.5, cy + 0.5):
            img, ax, ay = A.prop(name, f, m.night, m.season, arg)
            w.obj(tf(img), ax, ay, cx + 0.5, cy + 0.5, bias=0.3)
    for (cx, cy, f) in city.lamps:
        if vis(cx + 0.5, cy + 0.5):
            img, ax, ay = A.prop("lamp", f, m.night, m.season)
            w.obj(tf(img), ax, ay, cx + 0.5, cy + 0.5, bias=0.3)
            if m.night:
                w.ground(A.light_pool(A.SIDE[f]), 32, 16, cx + 0.5, cy + 0.5, layer=2)
    for (model, x, y, d, col, z) in city.vehicles:
        if vis(x, y):
            img, ax, ay, beam = A.vehicle(model, d, m.night, col)
            w.obj(tf(img), ax, ay, x, y, z=z)
            if beam is not None:
                w.ground(beam, ax, ay - int(z), x, y, layer=3)
    for (k, x, y, d) in city.people:
        if vis(x, y):
            img, ax, ay = A.person(k, m.night, d)
            w.obj(tf(img), ax, ay, x, y)
    for extra in getattr(city, "extras", []):
        extra(w, m, tf)
    return w


def building_img(b, m):
    a = b["args"]
    if b["kind"] == "civic":
        return A.civic(a["name"], m.night, m.season)
    return A.zoned(a["prefix"], a["fp"], a["level"], a["var"], a["facing"], m.night, m.season,
                   a.get("state", "ok"), a.get("stage", 0))


def top_point(b, w):
    """Screen position of a building sprite's highest opaque pixel (chimney tips, roofs)."""
    img, ax, ay = b["_img"]
    a = img[..., 3] > 0
    rows = np.where(a.any(1))[0]
    r = rows[0]
    cols = np.where(a[r])[0]
    sx, sy = w.screen(b["cx"] + b["fx"] / 2, b["cy"] + b["fy"] / 2)
    return int(sx - ax + cols[len(cols) // 2]), int(sy - ay + r)
