"""Water FX (hose, splash, heli drop) and construction/demolition dust + sparks."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

WATER = ramp("#2c64ac", "#4f93d8", "#8ec8f2", "#d4f0ff", "#ffffff")
OCHRE = ramp("#6e5a3a", "#8e7648", "#b09658", "#d0b878", "#ece0b0")
GREYDUST = ramp("#5e5a56", "#7c7872", "#9c9890", "#bcb8ae", "#dcd8cc")
SPARK = ramp("#b8381a", "#f07420", "#ffb830", "#ffe888", "#ffffff")
ELEC = ramp("#2a4ab8", "#4a8af0", "#90d0ff", "#d8f4ff", "#ffffff")


def hose(root, items):
    n = 4
    # world compass: N = -Y = screen up-right, E = +X = screen down-right, S = screen down-left, W = screen up-left
    dirs = {"n": (1, -1), "e": (1, 1), "s": (-1, 1), "w": (-1, -1)}
    for name, (sx, sy) in dirs.items():
        W, H = 36, 34
        # nozzle position so the whole arc stays inside the canvas
        nx = 2 if sx > 0 else W - 3
        ny = 12 if sy > 0 else H - 9
        ex, ey = nx + sx * 30, ny + sy * 15
        frames = []
        for f in range(n):
            img = canvas(W, H)
            N = 28
            for k in range(N):
                t = k / N
                # dashes travel along the arc: seamless over 4 frames
                u = (t * 5.0 - f / n) % 1.0
                if u > 0.7:
                    continue
                x = nx + (ex - nx) * t
                y = ny + (ey - ny) * t - 9 * 4 * t * (1 - t)
                # 2 px thick jet: lit top-left, shaded lower-right
                px(img, x, y, WATER[3] if u < 0.4 else WATER[2])
                px(img, x + 1, y, WATER[2])
                px(img, x, y + 1, WATER[1])
                px(img, x + 1, y + 1, WATER[0])
                if u < 0.15:
                    px(img, x, y - 1, WATER[4])
            # impact mist
            for k, (dx, dy) in enumerate([(0, 0), (-1, -1), (2, -1), (1, -2), (-2, 0), (3, 0)]):
                if (k + f) % 2 == 0:
                    px(img, ex + dx, ey + dy, WATER[3 + (k % 2)])
            # nozzle glint
            px(img, nx, ny, WATER[4])
            frames.append(img)
        save_anim(root, "fx/water/hose_" + name, frames, items, "hose " + name.upper())


def splash(root, items):
    W, H, n = 28, 24, 6
    frames = []
    cx, by = W // 2, H - 4
    angs = [-150, -125, -105, -90, -75, -55, -30, -165, -15]
    for f in range(n):
        img = canvas(W, H)
        # ground ring
        r = 3 + f * 2.2
        if f < n - 1:
            for a in range(0, 360, 10):
                x = cx + math.cos(math.radians(a)) * r
                y = by + math.sin(math.radians(a)) * r * 0.5
                if f < 4 or a % 20 == 0:
                    px(img, x, y, WATER[2] if a < 180 else WATER[1])
        # droplets
        for k, a in enumerate(angs):
            v = 3.0 + (k % 3) * 1.1
            vx, vy = math.cos(math.radians(a)) * v, math.sin(math.radians(a)) * v * 1.5
            t = f * 0.9
            x = cx + vx * t
            y = by + vy * t + 0.9 * t * t
            if y < by + 2 and f < n:
                px(img, x, y, WATER[3] if f < 3 else WATER[2])
                if f < 2:
                    px(img, x, y + 1, WATER[1])
        # crown (first frames)
        if f < 3:
            h = 3 + f * 2
            for dx in range(-3 + f, 4 - f):
                px(img, cx + dx, by - h + abs(dx) // 2, WATER[3])
                px(img, cx + dx, by - h + 1 + abs(dx) // 2, WATER[2])
            px(img, cx, by - h - 1, WATER[4])
        frames.append(img)
    save_anim(root, "fx/water/splash", frames, items, "splash")


def heli_drop(root, items):
    W, H, n = 36, 56, 4
    frames = []
    cx = W // 2
    for f in range(n):
        img = canvas(W, H)
        for y in range(2, H):
            t = y / (H - 1.0)                       # 0 top .. 1 bottom
            half = 6 + 4 * t                          # sheet widens a little as it falls
            for x in range(int(cx - half - 1), int(cx + half + 2)):
                u = (x + 0.5 - cx) / half
                if abs(u) > 1.0:
                    continue
                # vertical streak bands (chunky), falling; ragged dithered edges and top
                col = int(x - cx + 20)
                seg = (y * 0.25 - f / n + (col % 3) * 0.33) % 1.0
                if abs(u) > 0.75 and BAYER4[y & 3, x & 3] < (abs(u) - 0.75) * 3.0:
                    continue
                if t < 0.15 and BAYER4[y & 3, x & 3] > t * 6:
                    continue
                if t > 0.9 and BAYER4[(y + 1) & 3, x & 3] < (t - 0.9) * 8:
                    continue
                lit = 0.35 + 0.30 * (1 - (u + 1) / 2) + (0.25 if seg < 0.4 else 0) + (0.1 if col % 3 == 0 else 0)
                px(img, x, y, tone(WATER, lit, x, y, False))
        # loose drops falling beside
        for k in range(8):
            yy = (k * 7 + f * 14) % (H - 4)
            xx = cx + (-1) ** k * (6 + (k * 5) % 9)
            px(img, xx, yy, WATER[3])
            px(img, xx, yy + 1, WATER[2])
        frames.append(img)
    save_anim(root, "fx/water/heli_drop", frames, items, "heli water drop")


def dust(root, items):
    # construction dust: low billowing ochre/grey cloud near the ground, 6 frames
    W, H, n = 40, 28, 6
    blobs = [(-9, 0, 4.5, 0), (-3, -2, 5.0, 1), (4, -1, 5.0, 0), (10, 1, 4.0, 1), (0, 2, 4.5, 0), (-6, 3, 3.5, 1)]
    frames = []
    for f in range(n):
        img = canvas(W, H)
        t = f / (n - 1.0)
        for k, (bx, by, r, tg) in enumerate(blobs):
            rp = OCHRE if tg == 0 else GREYDUST
            rr = r * (0.6 + 0.8 * t) if t < 1 else r
            x = W // 2 + bx * (0.4 + 0.6 * t)
            y = H - 4 - rr * 0.5 + by - 11 * t * (0.7 + 0.12 * k)
            cut = max(0.0, (t - 0.45) * 1.6)
            disc(img, x, y, rr, rp, squash=0.8, cut=min(cut, 0.95))
        frames.append(img)
    save_anim(root, "fx/construction/dust", frames, items, "construction dust")


def demolition(root, items):
    W, H, n = 52, 40, 6
    frames = []
    chunks = [(-14, -3), (-8, -9), (-2, -12), (5, -10), (11, -5), (16, 0), (-17, 2)]
    for f in range(n):
        img = canvas(W, H)
        t = f / (n - 1.0)
        cx = W // 2
        for k in (0, 4, 1, 3, 2, 5, 6, 7, 8):
            if k < 5:   # ground ring spreading
                x = cx + (k - 2) * (2.5 + 4.5 * t) * 1.25
                y = H - 9 + abs(k - 2) * 0.8 - 4 * t
            else:       # upper plume billowing from the middle
                ux, uy = ((-4, -4), (4, -5), (0, -9), (-5, -10))[k - 5]
                x = cx + ux * (0.6 + 0.9 * t)
                y = H - 9 + uy * (0.5 + 0.9 * t) - 3 * t
            r = (3.0 + 4.5 * min(t * 1.5, 1.0)) * (0.8 + 0.1 * (k % 3))
            rp = OCHRE if k % 2 else GREYDUST
            disc(img, x, y, r, rp, squash=0.85, cut=min(0.95, max(0.0, (t - 0.4) * 1.5)))
        # flying debris chips
        for k, (dx, dy) in enumerate(chunks):
            if f == 0:
                continue
            tt = f * 0.9
            x = cx + dx * (0.4 + tt * 0.4)
            y = H - 10 + dy * tt * 1.2 + 0.7 * tt * tt * 1.5
            if y < H - 1:
                px(img, x, y, GREYDUST[1] if k % 2 else OCHRE[1])
                px(img, x + 1, y, GREYDUST[0] if k % 2 else OCHRE[0])
        frames.append(img)
    save_anim(root, "fx/construction/demolition", frames, items, "demolition burst")


def sparks(root, items):
    n = 4
    for name, rp, lab in (("weld", SPARK, "weld sparks"), ("short", ELEC, "power short")):
        frames = []
        W, H = 18, 18
        for f in range(n):
            img = canvas(W, H)
            cx, cy = 9, 12
            # bright core
            for dy in range(-2, 3):
                for dx in range(-2, 3):
                    d = abs(dx) + abs(dy)
                    if d <= 1 or (d == 2 and (dx + dy + f) % 2 == 0):
                        px(img, cx + dx, cy + dy, rp[4] if d == 0 else (rp[3] if d == 1 else rp[2]))
            # sparks arc out and fall (2 px each: bright head, darker tail)
            for k in range(11):
                a = math.radians(-175 + k * 17 + 9 * f)
                v = 1.8 + (k % 3) * 0.9
                t = (f + k % 2 * 0.5) + 0.8
                x = cx + math.cos(a) * v * t
                y = cy + math.sin(a) * v * t * 0.9 + 0.5 * t * t
                if 0 <= y < H:
                    px(img, x, y, rp[4 if t < 1.6 else 3])
                    px(img, x - math.cos(a) * 1.5, y - math.sin(a) * 1.3, rp[2])
                    px(img, x - math.cos(a) * 3, y - math.sin(a) * 2.2, rp[1])
            if name == "short":
                # a jagged blue arc above the core
                x, y = cx, cy
                for s in range(4):
                    x += (1, -1, 1, 1)[(s + f) % 4]
                    y -= 1
                    px(img, x, y, rp[3])
                    px(img, x + 1, y, rp[1])
            frames.append(img)
        save_anim(root, "fx/construction/sparks_" + name, frames, items, lab)


def build(root):
    water_items, cons_items = [], []
    hose(root, water_items)
    splash(root, water_items)
    heli_drop(root, water_items)
    dust(root, cons_items)
    demolition(root, cons_items)
    sparks(root, cons_items)
    return water_items, cons_items


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    a, b = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in a + b:
        prev(sc, os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 4, (100, 140, 90))
    print(len(a), len(b))
