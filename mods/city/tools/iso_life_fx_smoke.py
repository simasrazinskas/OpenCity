"""Smoke and steam animations (8-frame seamless loops). Emitter = bottom centre of the canvas."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

SMOKE_LIGHT = ramp("#7e838c", "#a0a5ae", "#c4c8cf", "#e4e6ea", "#fbfbfb")
SMOKE_DARK = ramp("#16161a", "#26262c", "#3a3a42", "#54545e", "#72727e")
STEAM = ramp("#bccbdc", "#dce7f3", "#f1f7fd", "#ffffff")
STEAM_CLOUD = ramp("#aebfd4", "#ccdaea", "#e6eef8", "#f8fbff", "#ffffff")


def column(W, H, n, puffs, r0, r1, rise, drift, rp, cut0=0.35, seed=1, wob=1.2, squash=0.9, lo=0.0, hi=1.0,
           ex=None):
    """A rising column of puffs. Puff i has age a = (f/n + i/puffs) % 1 -> loops seamlessly in n frames."""
    frames = []
    cx0 = W // 2 if ex is None else ex
    rnd = [((seed * 37 + i * 91) % 17) / 17.0 for i in range(puffs)]
    for f in range(n):
        img = canvas(W, H)
        order = []
        for i in range(puffs):
            a = (f / n + i / puffs) % 1.0
            order.append((a, i))
        for a, i in sorted(order, reverse=True):
            r = (r0 + (r1 - r0) * (a ** 0.7)) * (0.82 + 0.36 * rnd[i])
            x = cx0 + drift * (a ** 1.6) + math.sin((a * 2 + rnd[i]) * math.pi * 2) * wob * a + (rnd[i] - 0.5) * 2.4 * a
            y = H - 1 - r * 0.6 - rise * a
            cut = 0.0 if a < 0.45 else min(0.95, cut0 + (a - 0.45) * 1.4)
            sh = 0.18 * a
            disc(img, x, y, r, rp, squash=squash, lo=min(lo + sh, 0.5), hi=min(hi, 0.85 + sh * 0.6 + 0.15), cut=cut)
        frames.append(img)
    return frames


def house(root, items):
    # white/light grey chimney smoke, ~14 wide x ~36 tall
    fr = column(26, 40, 8, 5, 2.2, 5.0, 31, 9, SMOKE_LIGHT, seed=2, wob=1.4, ex=7)
    save_anim(root, "fx/smoke/house", fr, items, "house smoke")


def house_dark(root, items):
    fr = column(26, 40, 8, 5, 2.4, 5.4, 31, 9, SMOKE_DARK, seed=3, wob=1.4, ex=7)
    save_anim(root, "fx/smoke/industry", fr, items, "industry smoke")


def stack(root, items):
    fr = column(44, 60, 8, 6, 4.0, 9.0, 48, 18, SMOKE_DARK, cut0=0.3, seed=5, wob=2.2, squash=0.85, ex=12)
    save_anim(root, "fx/smoke/stack_plume", fr, items, "stack plume")
    fr = column(44, 60, 8, 6, 4.0, 9.0, 48, 18, SMOKE_LIGHT, cut0=0.3, seed=6, wob=2.2, squash=0.85, ex=12)
    save_anim(root, "fx/smoke/stack_plume_light", fr, items, "stack plume, light")


def steam(root, items):
    fr = column(22, 32, 8, 4, 2.0, 4.6, 26, 5, STEAM, cut0=0.5, seed=7, wob=1.2, squash=0.9, ex=7)
    save_anim(root, "fx/smoke/steam", fr, items, "steam")


def cooling(root, items):
    # big billowing cloud, rises from a cooling tower mouth
    frames = []
    W, H, n = 56, 56, 8
    cx0 = 22
    blobs = [(-9, 0), (-3, -3), (4, -1), (10, 2), (0, 3), (-6, 4), (7, 5)]
    for f in range(n):
        img = canvas(W, H)
        order = []
        for i in range(7):
            a = (f / n + i / 7.0) % 1.0
            order.append((a, i))
        for a, i in sorted(order, reverse=True):
            bx, by = blobs[i]
            r = 5.0 + 7.0 * (a ** 0.6)
            x = cx0 + bx * (0.5 + a) + 14 * (a ** 1.5)
            y = H - 1 - r * 0.6 - 38 * a + by * 0.3
            cut = 0.0 if a < 0.4 else min(0.95, 0.3 + (a - 0.4) * 1.3)
            disc(img, x, y, r, STEAM_CLOUD, squash=0.85, cut=cut)
        frames.append(img)
    save_anim(root, "fx/smoke/cooling_cloud", frames, items, "cooling tower")


def build(root, items_out=None):
    items = []
    house(root, items)
    house_dark(root, items)
    stack(root, items)
    steam(root, items)
    cooling(root, items)
    return items


if __name__ == "__main__":
    import shutil
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    it = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in it:
        im = None
        from iso_life_common import load_png
        im = load_png(os.path.join(root, i["file"]))
        prev(sc, os.path.basename(i["file"]), im, 4, (110, 150, 200))
    print(len(it))
