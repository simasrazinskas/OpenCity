"""iso_life_people_bench - tiny iso boxes (bench) and the sitting peeps. Bench canvas 16x14, seat top at SEAT_Y."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, person_cols, canvas, blit, rgba, tri, item, pv, strip

UX = {"x": ((2, 1), (-2, 1)), "y": ((-2, 1), (2, 1))}   # (along, across) screen steps per unit
WOOD = "b98a52"
WOOD_DARK = "6b4a2a"
IRON = "3a3d44"


def box(img, o, u, v, L, W, z0, z1, top, lit, dark, t0=0.0, s0=0.0):
    """Axis-aligned box in a 2:1 iso frame: along u (L units), across v (W units); z in px."""
    ox, oy = o
    cx = ox + u[0] * (t0 + L) + v[0] * (s0 + W)
    H, Wd = img.shape[:2]
    for z in range(z0, z1):
        top_level = z == z1 - 1
        for py in range(H):
            for px in range(Wd):
                dx, dy = px + 0.5 - ox, py + 0.5 - oy + z
                # solve dx = ux*t + vx*s, dy = uy*t + vy*s
                det = u[0] * v[1] - u[1] * v[0]
                t = (dx * v[1] - dy * v[0]) / det - t0
                s = (u[0] * dy - u[1] * dx) / det - s0
                if 0 <= t < L and 0 <= s < W:
                    img[py, px] = rgba(top if top_level else (lit if px + 0.5 < cx else dark))


def bench(axis):
    """Crisp wooden park bench, 4x1.8 units (about 12 px along the axis): 2 seat slats, 2 back slats, 4 dark legs."""
    img = canvas(16, 16)
    u, v = UX[axis]
    ox = 8 - (u[0] * 4 + v[0] * 1.8) // 2
    o = (ox, 8)
    L, W, zs = 4.0, 1.8, 3
    for t0 in (0.2, L - 0.8):                            # dark end frames (legs)
        box(img, o, u, v, 0.6, W - 0.3, 0, zs, "4a4e58", "4a4e58", "23262d", t0, 0.15)
    box(img, o, u, v, L, 0.6, zs, zs + 4, "e0b472", "b88848", "7a5328", 0, 0)        # backrest block
    box(img, o, u, v, L, 0.6, zs + 1, zs + 2, "8a6030", "8a6030", "5a3a1c", 0, 0)    # seam between 2 back slats
    box(img, o, u, v, L, W, zs, zs + 1, "ecc080", "c9975a", "8a5f33", 0, 0)           # seat
    box(img, o, u, v, L, 0.18, zs, zs + 1, "9a6a34", "9a6a34", "9a6a34", 0, 0.9)      # seam between 2 seat slats
    return img


def sit_grid(face, f):
    """Seated peep facing screen-right: head, shirt, knees forward, shins/feet down. f1 = reads a newspaper. 8x8."""
    g = blank(8, 8)
    stamp(g, 0, 0 + (1 if f == 1 else 0), ["...HHH..", "...HSS.." if face == "F" else "...HHH.."])
    if f == 1:
        stamp(g, 0, 2, ["...TTT..", "...TTT..", "...TTT.."])
        stamp(g, 5, 2, ["NN", "nN", "NN"])
    else:
        stamp(g, 0, 2, ["...TTT..", "...TTTS.", "...TTT.."])
    stamp(g, 0, 5, ["...PPPP.", "......P.", "......KK" if f == 0 else ".....KK."])
    return g


def sitter(axis, f, cols):
    face = "F" if axis in ("x", "y") else "B"
    flip = axis == "x"    # axis x bench: sitter faces +Y (down-left) -> mirrored
    c = dict(cols)
    c.update({"N": "f0f0ea", "n": "8a8a92"})
    return render(sit_grid(face, f), c, flip=flip)


def bench_with(axis, f, cols, cols2=None):
    """Bench with one sitter (cols) or two (cols2 added; second one sits on the other half)."""
    b = bench(axis)
    bx, by = (5 if axis == "x" else 4), 2
    if cols2 is None:
        blit(b, sitter(axis, f, cols), bx, by)
    else:
        blit(b, sitter(axis, f, cols), bx - 2, by - 1)
        blit(b, sitter(axis, 1 - f, cols2), bx + 2, by + 1)
    return b


if __name__ == "__main__":
    c = person_cols(1, 1, 1, 0)
    fr = [bench("x"), bench("y")] + [bench_with(a, f, c) for a in "xy" for f in (0, 1)] + [bench_with(a, 0, c, person_cols(0, 5, 1, 2)) for a in "xy"]
    pv("bench.png", strip(fr, 2), 10)
