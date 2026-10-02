"""Milestone fireworks: 8 frames each (rocket trail, burst, falling sparkles). Canvas 32x40, anchor = launch point
(bottom centre). Burst centre at (16, 13)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

COLOURS = {
    "red": ramp("#6a1018", "#c02034", "#ff4c58", "#ff9a98", "#fff0e8"),
    "gold": ramp("#7a4a10", "#d88a18", "#ffc828", "#ffee80", "#fffbe0"),
    "green": ramp("#10502a", "#209a48", "#50e070", "#a8ffb0", "#f0fff0"),
    "blue": ramp("#142a78", "#2a58d8", "#58a0ff", "#a8d8ff", "#f0f8ff"),
}


def firework(rp, seed, nsp=20):
    W, H, n = 32, 40, 8
    cx, cy = 16, 13
    frames = []
    for f in range(n):
        img = canvas(W, H)
        if f <= 2:
            # rocket rising: head + fading tail
            y = (31, 23, 16)[f]
            for k in range(9):
                c = rp[4] if k == 0 else (rp[3] if k < 2 else rp[2] if k < 4 else rp[1] if k < 6 else rp[0])
                if k > 3 and (k + f) % 2:
                    continue
                px(img, 16, y + k, c)
                if k < 3:
                    px(img, 17, y + k, rp[2])
            px(img, 15, y, rp[3])
        else:
            t = f - 2                                  # 1..5
            for k in range(nsp):
                a = 2 * math.pi * k / nsp + (0.2 if k % 2 else 0)
                spd = 3.6 + (k % 3) * 0.5
                r = spd * min(t, 3.0) + 0.6 * max(0, t - 3)
                x = cx + math.cos(a) * r * 1.0
                y = cy + math.sin(a) * r * 0.85 + 0.9 * max(0, t - 2) ** 2
                head = rp[4] if t < 3 else (rp[3] if t < 5 else rp[2])
                px(img, x, y, head)
                if t < 4:
                    px(img, x + 1, y, rp[3])
                    px(img, x, y + 1, rp[2])
                    px(img, x + 1, y + 1, rp[1])
                # trail behind the head (toward the centre)
                px(img, x - math.cos(a) * 1.8, y - math.sin(a) * 1.5, rp[2 if t < 4 else 1])
                if t < 4:
                    px(img, x - math.cos(a) * 3.4, y - math.sin(a) * 2.8, rp[1])
                if t >= 3 and k % 2 == 0:
                    px(img, x, y - 1, rp[1 if t < 5 else 0])      # falling sparkle tail
            if t <= 2:
                for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                    px(img, cx + dx, cy + dy, rp[4] if t == 1 else rp[3])
        frames.append(img)
    return frames


def build(root):
    items = []
    for i, (name, rp) in enumerate(COLOURS.items()):
        save_anim(root, "fx/fireworks/" + name, firework(rp, i), items, name + " burst")
    return items


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    it = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in it:
        prev(sc, os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 4, (14, 16, 40))
    print(len(it))
