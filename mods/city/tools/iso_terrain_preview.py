"""Small composed terrain maps that prove the tiles join (used by iso_terrain.py and mockups)."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.tiles import SIDE_OFF, CORNER_OFF, CORNER_SIDES  # noqa: E402
import iso_terrain_water as W  # noqa: E402
import iso_terrain_edges as E  # noqa: E402
import iso_terrain_fields as F  # noqa: E402

# 0 water, 1 grass, 2 dirt patch, 3 wheat field, 4 forest floor, 5 sand
MAP = """
000000000000
000111110000
001111111100
011133311110
011133311210
011111112210
001144111110
001144411100
000114111000
000011100000
000000000000
"""


def grid_from(text):
    rows = [r for r in text.strip().splitlines()]
    return np.array([[int(ch) for ch in r] for r in rows])


def blob(grid, x, y, same):
    """(mask, corners) of neighbours for which same(neighbour_value) is False."""
    h, w = grid.shape

    def at(xx, yy):
        return grid[min(max(yy, 0), h - 1), min(max(xx, 0), w - 1)]
    mask = sum(b for b, (dx, dy) in SIDE_OFF.items() if not same(at(x + dx, y + dy)))
    corners = 0
    for b, (dx, dy) in CORNER_OFF.items():
        a, c = CORNER_SIDES[b]
        if not (mask & a) and not (mask & c) and not same(at(x + dx, y + dy)):
            corners |= b
    return mask, corners


def island(style="beach", season="summer", text=MAP, edge=True):
    g = grid_from(text)
    h, w = g.shape
    cm = ik.Compositor(w, h, top_margin=8, bottom_margin=30)
    for y in range(h):
        for x in range(w):
            v = g[y, x]
            org = (x, y)
            if v == 0:
                m, c = blob(g, x, y, lambda n: n == 0)
                spr = W.water_tile(m, c, style, 0, "open", org)
            else:
                m, c = blob(g, x, y, lambda n: n != 0)
                if m or c:
                    spr = W.land_tile(m, c, style, org, season)
                elif v == 1:
                    spr = ik.flat_tile("grass", org, season=season)
                elif v == 3:
                    spr = ik.flat_tile(F.field_material("wheat", "ripe"), org, season=season)
                else:
                    b = {2: "dirt", 4: "forest_floor", 5: "sand"}[v]
                    m2, c2 = blob(g, x, y, lambda n, v=v: n == v)
                    if m2 or c2:
                        # this cell is type b with grass intruding where neighbours are not b
                        spr = E.transition_tile(b, "grass", m2, c2, org)
                    else:
                        spr = ik.flat_tile(b, org, season=season)
            cm.ground(spr, x, y)
            if edge and y == h - 1:
                cm.ground(E.edge_face("S", water=(v == 0)), x, y)
            if edge and x == w - 1:
                cm.ground(E.edge_face("E", water=(v == 0)), x, y)
    return cm.render()
