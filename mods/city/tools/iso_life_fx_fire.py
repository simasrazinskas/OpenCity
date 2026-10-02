"""Building fire stages (1-cell footprint overlays), garbage-heap fire. Anchor = bottom centre = footprint ground centre.

Assumed host building (what the fire sits on): footprint diamond 64x32 centred on the anchor; walls ~28 px tall.
Window flame point: left (lit, +Y) face at (cx-14, H-17). Roof fire centre: (cx, H-30).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa
from iso_life_fx_smoke import column, SMOKE_DARK, SMOKE_LIGHT

FIRE = ramp("#6e1410", "#b8241a", "#e8601c", "#ffa828", "#ffe06a", "#fffbc8")
EMBER = ramp("#2a1410", "#6a2014", "#c03a18", "#ff7a28", "#ffc050")
HEAP = ramp("#1e1612", "#33261c", "#4a3626", "#654a32", "#7e6040")
BLACK = ramp("#0c0c10", "#17171c", "#25252c", "#383842", "#50505c")


def flame(img, cx, by, w, h, f, n, seed, lean=0.0, heat=1.0):
    """One flame tongue: base centre (cx, by), width w, height h. Flickers with frame f of n (loops)."""
    ph = 2 * math.pi * f / n
    s = seed * 1.7
    hh = h * (1.0 + 0.16 * math.sin(ph + s) + 0.08 * math.sin(2 * ph + s * 2))
    sway = math.sin(ph + s * 1.3) * w * 0.28
    rows = int(hh) + 1
    for r in range(rows):
        t = r / max(hh, 1)
        half = (w / 2.0) * (1 - t) ** 0.75
        # jag: rows at the top alternate
        half += 0.7 * math.sin(r * 2.1 + ph * 2 + s) * (t > 0.35)
        if half < 0.4:
            half = 0.4 if r < hh - 1 else 0
        cxr = cx + sway * t * t + lean * t
        y = int(by - r)
        for x in range(int(cxr - half - 1), int(cxr + half + 2)):
            u = abs(x + 0.5 - cxr) / max(half, 0.5)
            if u > 1.0:
                continue
            e = (1 - t * 0.95) * (1 - u * 0.85) * heat
            e += 0.10 * math.sin(x * 1.3 + y * 0.9 + ph * 2)
            px(img, x, y, tone(FIRE, 0.12 + e * 0.92, x, y))


def cluster(img, cx, by, spec, f, n, seed=0):
    """spec: list of (dx, w, h, lean). Draw back to front (tall first)."""
    for k, s in sorted(enumerate(spec), key=lambda a: a[1][-1] if False else (a[1][4] if len(a[1]) > 4 else 0)):
        dx, w, h, lean = s[:4]
        dy = s[4] if len(s) > 4 else 0
        flame(img, cx + dx, by + dy, w, h, f, n, seed + k * 3.1, lean)


def smoke_into(img, ex, ey, W, H, n, f, rp, **kw):
    """Blit a smoke column frame so its emitter (bottom centre-ish) sits at (ex, ey)."""
    fr = column(kw.pop("w", 26), kw.pop("h", 40), n, kw.pop("puffs", 5), kw.pop("r0", 2.2), kw.pop("r1", 5.0),
                kw.pop("rise", 31), kw.pop("drift", 9), rp, seed=kw.pop("seed", 2), ex=kw.pop("ex", 7), **kw)[f]
    blit(img, fr, int(ex) - 7, int(ey) - fr.shape[0])


def stage(n, W, H, fn):
    out = []
    for f in range(n):
        img = canvas(W, H)
        fn(img, f, n, W, H)
        out.append(img)
    return out


def st1(img, f, n, W, H):
    cx = W // 2
    cluster(img, cx - 14, H - 17, [(0, 6, 10, 0), (-2, 4, 6, -1), (2, 4, 7, 1)], f, n, 1)


def st2(img, f, n, W, H):
    cx = W // 2
    smoke_into(img, cx - 6, H - 28, W, H, n, f, SMOKE_DARK, h=40, w=26, rise=26, drift=8, r1=5.0, seed=4)
    cluster(img, cx - 14, H - 15, [(0, 7, 13, 0), (-3, 4, 8, -1), (3, 4, 9, 1)], f, n, 2)
    cluster(img, cx + 4, H - 28, [(0, 5, 8, 0), (3, 3, 5, 1)], f, n, 7)


def st3(img, f, n, W, H):
    cx = W // 2
    smoke_into(img, cx - 2, H - 34, W, H, n, f, BLACK, h=50, w=40, rise=40, drift=12, r0=3.0, r1=7.0, puffs=6,
               seed=5, cut0=0.3)
    cluster(img, cx - 14, H - 14, [(0, 8, 15, 0), (-3, 4, 9, -1), (3, 5, 11, 1)], f, n, 3)
    # roof fire: tongues staggered over the diamond roof (back ones higher on screen, drawn first)
    cluster(img, cx, H - 30, [(-2, 6, 22, 0, -4), (5, 5, 19, 1, -3), (-9, 5, 14, -1, -1), (10, 5, 15, 1, 0),
                               (-5, 6, 20, -1, 1), (2, 7, 24, 0, 2), (-12, 4, 10, -1, 2), (8, 5, 16, 1, 3),
                               (-1, 5, 14, 0, 5)], f, n, 11)
    cluster(img, cx + 14, H - 20, [(0, 6, 10, 0), (2, 3, 7, 1)], f, n, 17)


def st4(img, f, n, W, H):
    cx = W // 2
    smoke_into(img, cx, H - 24, W, H, n, f, SMOKE_DARK, h=36, w=26, rise=24, drift=6, r0=2.0, r1=4.4, puffs=5,
               seed=8, cut0=0.5, lo=0.1, hi=0.8)
    # glowing embers on the roof / around the base; they pulse
    pts = [(-12, -17), (-6, -22), (0, -28), (5, -25), (9, -20), (13, -14), (-9, -9), (2, -12), (-2, -31), (12, -22)]
    for k, (dx, dy) in enumerate(pts):
        ph = (f + k * 2) % n
        t = 0.5 + 0.5 * math.sin(2 * math.pi * ph / n)
        c = tone(EMBER, 0.15 + 0.85 * t, 0, 0, False)
        px(img, cx + dx, H + dy, c)
        px(img, cx + dx + 1, H + dy, EMBER[1] if t < 0.5 else EMBER[2])
        if t > 0.7:
            px(img, cx + dx, H + dy - 1, EMBER[3])
    # a lick of low flame at one spot
    if f % 2 == 0:
        flame(img, cx - 3, H - 24, 3, 4 + (f % 3), f, n, 3.3, 0, 0.7)


def heap(root, items):
    n = 4

    def fn(img, f, n, W, H):
        cx = W // 2
        # trash heap: bumpy dark mound with a few bright specks (bags / cans)
        for y in range(H - 8, H):
            for x in range(cx - 10, cx + 11):
                dx = (x - cx) / 10.0
                top = H - 8 + 5 * dx * dx + 0.8 * math.sin(x * 1.7)
                if y >= top and abs(dx) <= 1 - (H - 1 - y) * 0.0:
                    t = 0.35 + 0.35 * ((H - y) / 8.0) - 0.25 * dx
                    px(img, x, y, tone(HEAP, t, x, y))
        for (dx, dy, c) in [(-5, -5, "#9aa0a6"), (3, -6, "#c8c0a0"), (-1, -4, "#5a7aa0"), (6, -3, "#a04a3a")]:
            px(img, cx + dx, H + dy, rgba(c)[:3])
        cluster(img, cx, H - 7, [(0, 6, 11, 0), (-4, 4, 7, -1), (4, 4, 8, 1)], f, n, 21)

    fr = stage(n, 28, 30, fn)
    save_anim(root, "fx/fire/garbage", fr, items, "garbage fire")


def build(root):
    items = []
    W, H = 56, 76
    specs = [("stage1", 6, st1, "stage 1: window"), ("stage2", 6, st2, "stage 2: medium"),
             ("stage3", 6, st3, "stage 3: roof"), ("stage4", 6, st4, "stage 4: burnt out")]
    for name, n, fn, lab in specs:
        save_anim(root, "fx/fire/" + name, stage(n, W, H, fn), items, lab)
    heap(root, items)
    return items


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    it = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in it:
        prev(sc, os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 4, (96, 120, 90))
    print(len(it))
