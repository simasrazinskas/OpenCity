"""iso_life_people_scene - 'In context' patches: grey asphalt, light pavement and grass (own quick tiles) with peeps."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import person_cols, item, canvas, blit, rgba, pv, SKIN
from iso_life_people_var import walker
from iso_life_people_act import cycling, waiting
from iso_life_people_act2 import jog, umbrella, UMB, worker_walk, worker_cols
from iso_life_people_act3 import parent_child, dog_walker
from iso_life_people_animals import dog, pigeon
from iso_life_people_bench import bench_with
from iso_life_people_walk import frames

GRASS = ["5f9a45", "69a84d", "558c3e"]
PAVE = ["c9c4b6", "d4cfc2", "bdb8aa"]
ASPH = ["5d606a", "666973", "54575f"]
ROAD_LINE = "d8d4c0"


def hsh(x, y):
    return (x * 73856093 ^ y * 19349663) & 0xffff


def patch(nx, ny, kind_of_cell):
    """Diamond patch of nx x ny cells; kind_of_cell(cx, cy) -> 'g' grass / 'p' pavement / 'a' asphalt. Returns img, origin."""
    w, h = (nx + ny) * 32, (nx + ny) * 16
    img = canvas(w, h)
    ox, oy = ny * 32, 0
    for py in range(h):
        for px in range(w):
            sx, sy = px + 0.5 - ox, py + 0.5 - oy
            wx, wy = (sx / 32 + sy / 16) / 2, (sy / 16 - sx / 32) / 2
            cx, cy = int(np.floor(wx)), int(np.floor(wy))
            if not (0 <= cx < nx and 0 <= cy < ny):
                continue
            k = kind_of_cell(cx, cy)
            r = hsh(px, py) % 7
            if k == "g":
                c = GRASS[0 if r < 4 else (1 if r < 6 else 2)]
            elif k == "p":
                c = PAVE[0 if r < 5 else (1 if r < 6 else 2)]
                fx, fy = wx - cx, wy - cy
                if abs(fx - 0.5) < 0.03 or abs(fy - 0.5) < 0.06:
                    c = PAVE[2]
                if fy > 0.94 and kind_of_cell(cx, min(cy + 1, ny - 1)) == "a":
                    c = "8f8b80"
            else:
                c = ASPH[0 if r < 4 else (1 if r < 5 else 2)]
                fy = wy - cy
                if kind_of_cell(cx, cy - 1 if cy else 0) == "a" and cy and abs(fy) < 0.04 and int(wx * 4) % 2 == 0:
                    c = ROAD_LINE
            img[py, px] = rgba(c)
    return img, (ox, oy)


def place(img, org, spr, wx, wy, ax=None):
    """Put sprite so that its anchor (default bottom centre) sits at world (wx, wy)."""
    sx, sy = org[0] + (wx - wy) * 32, org[1] + (wx + wy) * 16
    h, w = spr.shape[:2]
    ax = w // 2 if ax is None else ax
    blit(img, spr, int(round(sx)) - ax, int(round(sy)) - h)


def street():
    kinds = lambda cx, cy: "g" if cy == 0 else ("p" if cy == 1 else "a")
    img, o = patch(6, 5, kinds)
    items = [(2.0, 1.5, walker(3, "E", 0)), (3.5, 1.5, worker_walk("E", 1, worker_cols())), (5.2, 1.6, walker(10, "W", 2)),
             (0.9, 1.3, frames("child", "E", 1, person_cols(0, 2, 0, 0))), (4.6, 1.35, walker(5, "E", 1)),
             (1.3, 2.5, cycling("E", 1, person_cols(2, 1, 4, 1), 0)), (4.2, 3.6, cycling("W", 2, person_cols(0, 2, 7, 3), 1)),
             (2.5, 1.52, parent_child("E", 1, person_cols(1, 3, 6, 0), person_cols(1, 3, 5, 2)))]
    items.append((3.0, 1.05, bench_with("x", 0, person_cols(1, 0, 1, 1), person_cols(4, 3, 0, 2)), 8))
    items.append((3.5, 1.28, pigeon(0), None))
    for it in sorted(items, key=lambda t: t[0] + t[1]):
        place(img, o, it[2], it[0], it[1], it[3] if len(it) > 3 else None)
    return img


def park():
    kinds = lambda cx, cy: "p" if (cy == 2 or cx == 0) else "g"
    img, o = patch(5, 5, kinds)
    items = [(2.5, 1.0, bench_with("x", 1, person_cols(1, 1, 1, 0), person_cols(3, 2, 5, 2)), 8), (3.6, 2.5, dog_walker("W", 1, person_cols(0, 4, 7, 1))),
             (1.5, 3.4, dog("E", 2, "golden")), (3.0, 4.0, jog("E", 2, person_cols(2, 1, 7, 0))),
             (0.5, 3.0, walker(8, "S", 1)), (4.0, 1.8, pigeon(1)), (4.3, 1.7, pigeon(0, "peck", True)),
             (1.0, 1.9, waiting("S", 1, person_cols(4, 3, 1, 2)))]
    for it in sorted(items, key=lambda t: t[0] + t[1]):
        place(img, o, it[2], it[0], it[1], it[3] if len(it) > 3 else None)
    return img


def scene_items(root):
    return [item(root, "people/context/street.png", street(), "street: walkers, bench, cyclists"),
            item(root, "people/context/park.png", park(), "park: dog, jogger, pigeons")]


if __name__ == "__main__":
    pv("street.png", street(), 3)
    pv("park.png", park(), 3)
