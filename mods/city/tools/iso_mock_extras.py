"""Scene extras for the mockups: smoke, status bubbles, a train, boats, a helicopter, construction and
zoning tools. Each extra is a callable(world, mode, tf) appended to city.extras."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_mock_assets as A  # noqa: E402
import iso_mock_scene as MS  # noqa: E402


def smoke(city):
    def ex(w, m, tf):
        k = 0
        for b in city.buildings:
            if "_img" not in b:
                continue
            a = b["args"]
            name = a.get("prefix") or a.get("name")
            heavy = name in ("ind", "powerplant-coal")
            house = m.season == "winter" and name in ("res-low", "res-row") and (b["cx"] + b["cy"]) % 3 == 0
            if not (heavy and (b["cx"] * 7 + b["cy"]) % 3 != 0) and not house:
                continue
            sx, sy = MS.top_point(b, w)
            rel, n = ("life/fx/smoke/stack_plume_strip.png", 8) if name == "powerplant-coal" else \
                (("life/fx/smoke/house_strip.png", 8) if house else ("life/fx/smoke/industry_strip.png", 8))
            img, ax, ay = A.fx(rel, n, (k * 3 + 2) % n)
            if m.night:
                img = ik.nightify(img)
            w.overlay_screen(tf(img), sx - ax, sy - ay + 2)
            k += 1
    city.extras.append(ex)


def bubbles(city, picks):
    """picks: [(building index predicate, icon)] -> icons hover above the matching buildings."""
    def ex(w, m, tf):
        for pred, icon in picks:
            for b in city.buildings:
                if "_img" in b and pred(b):
                    sx, sy = MS.top_point(b, w)
                    img, ax, ay = A.marker(icon)
                    w.overlay_screen(img, sx - ax, sy - ay - 6)
                    break
    city.extras.append(ex)


def train(city, x, y0, d="N", cars=("train-loco", "train-coach", "train-coach", "train-coach"), gap=0.62):
    def ex(w, m, tf):
        for i, model in enumerate(cars):
            y = y0 + i * gap if d == "N" else y0 - i * gap
            img, ax, ay, _ = A.vehicle(model, d, m.night)
            w.obj(tf(img), ax, ay, x, y)
    city.extras.append(ex)


def boats(city, items):
    def ex(w, m, tf):
        for model, x, y, d in items:
            img, ax, ay, _ = A.vehicle(model, d, m.night)
            w.obj(tf(img), ax, ay, x, y, z=-5)
    city.extras.append(ex)


def heli(city, x, y, z=26, d="SW"):
    def ex(w, m, tf):
        img, ax, ay, _ = A.vehicle("heli-medevac", d, m.night)
        sh = A.png("life/vehicles/air/heli-medevac/heli-medevac-shadow-8dirs.png")
        fw = sh.shape[1] // 8
        k = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"].index(d)
        w.ground(sh[:, k * fw:(k + 1) * fw], ax, ay, x, y, layer=5)
        w.overlay(tf(img), ax, ay, x, y, z)
    city.extras.append(ex)


def ghost(img, colour=(70, 210, 110)):
    """Placement ghost (LIFE ghost look): green-tinted checker of strong/soft tint, bright rim."""
    out = img.copy()
    yy, xx = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    a = out[..., 3] > 0
    k = np.where((xx + yy) % 2 == 0, 0.7, 0.4)[..., None]
    out[..., :3] = np.where(a[..., None], out[..., :3] * (1 - k) + np.array(colour) * k, out[..., :3]).astype(np.uint8)
    rim = a & ~(np.roll(a, 1, 0) & np.roll(a, -1, 0) & np.roll(a, 1, 1) & np.roll(a, -1, 1))
    out[rim, :3] = (170, 255, 170)
    return ik.snap(out)


def tools(city, zone_cells, ghost_at, path_cells, cursor_at):
    """Zoning brush tiles, a placement ghost (CIVIC school), a road drag path and the cursor."""
    def ex(w, m, tf):
        zt = A.png("net/zones/res-low.png")
        for (x, y) in zone_cells:
            w.ground(zt, 32, 16, x + 0.5, y + 0.5, layer=6)
        gx, gy, name, fx, fy = ghost_at
        fp = A.png("life/markers/ghost/fp-%dx%d-valid.png" % (fx, fy))
        w.ground(fp, fp.shape[1] // 2, fp.shape[0] // 2, gx + fx / 2, gy + fy / 2, layer=6)
        img, ax, ay = A.civic(name, False, "summer")
        w.building(ghost(img), ax, ay, gx, gy, fx, fy)
        for (x, y, dname) in path_cells:
            p = A.png("net/markers/%s.png" % dname)
            w.ground(p, p.shape[1] // 2, p.shape[0] - 16, x + 0.5, y + 0.5, layer=6)
        cur = A.png("life/cursors/road.png")
        sx, sy = w.screen(*cursor_at)
        w.overlay_screen(cur, sx, sy)
    city.extras.append(ex)
