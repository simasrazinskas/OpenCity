"""Pollution (smog haze, ground stains, toxic sheen, noise rings, dead grass) and flood water."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

SMOG = ramp("#6a5a22", "#8e7a30", "#b09a42", "#cdb85a")
STAIN = ramp("#120e0c", "#241a14", "#3a2a1c", "#52402a", "#6e5a3a")
SHEEN = ramp("#2a5a3a", "#58a050", "#a8d048", "#d8f070")
SHEEN2 = ramp("#5a2a6a", "#8a4aa0", "#c070c8")
NOISE = ramp("#8a8a96", "#b4b4be", "#dcdce4", "#ffffff")
DEAD = ramp("#4a3a22", "#6a5430", "#8e7440", "#b09a58", "#cdb878")
FLOOD = ramp("#3a4a4c", "#4c5e5c", "#607268", "#7e8a74", "#a4aa90")
FLOODHI = ramp("#8a9a94", "#b4c0b8", "#e0e8e0")


def smog_tile(dens, seed):
    img = canvas(64, 32)
    for y in range(32):
        for x in range(64):
            if not DIAMOND[y, x]:
                continue
            u, v = tile_uv(x, y)
            n = pnoise(u, v, seed)
            cover = pn_clip(dens * (0.6 + 0.8 * n))
            if cover > BAYER4[y & 3, x & 3]:
                px(img, x, y, tone(SMOG, 0.25 + 0.6 * n, x, y))
    return img


def smog(root, items):
    for k, (name, d) in enumerate((("1", 0.28), ("2", 0.5), ("3", 0.78))):
        save_still(root, "fx/pollution/smog_%s.png" % name, smog_tile(d, 11), items, "smog " + name)


def stain_tile(level, seed):
    img = canvas(64, 32)
    thr = (0.74, 0.62, 0.50)[level]
    for y in range(32):
        for x in range(64):
            if not DIAMOND[y, x]:
                continue
            u, v = tile_uv(x, y)
            n = 0.65 * pnoise(u, v, seed) + 0.35 * pnoise(u * 2 % 1.0, v * 2 % 1.0, seed + 5)
            if n < thr:
                continue
            e = (n - thr) / (1 - thr)
            # blotch with a dark core and brown rim, dithered at the very edge
            if e < 0.12 and BAYER4[y & 3, x & 3] > e * 8:
                continue
            c = tone(STAIN, 0.15 + 0.55 * (1 - e) + (0.1 if (x - y) % 7 == 0 else 0), x, y, False)
            px(img, x, y, c)
            # oily sheen: a few blue-purple highlight pixels on the dark core
            if e > 0.5 and (x * 3 + y * 5) % 11 == 0:
                px(img, x, y, SHEEN2[1])
    return img


def stains(root, items):
    for lv in range(3):
        save_still(root, "fx/pollution/stain_%d.png" % (lv + 1), stain_tile(lv, 21), items, "stain %d" % (lv + 1))


def toxic(root, items):
    frames = []
    for f in range(4):
        img = canvas(64, 32)
        for y in range(32):
            for x in range(64):
                if not DIAMOND[y, x]:
                    continue
                u, v = tile_uv(x, y)
                n = pnoise(u, v, 31, phase=f / 4.0)
                # swirl bands: thin iridescent contour lines of the noise field
                # oil-film stripes: wavy diagonal bands (solid), wobbled by the looping noise
                band = (u * 2 - v * 1 + (n - 0.5) * 0.35) % 1.0
                if band < 0.14:
                    px(img, x, y, SHEEN[1])
                elif band < 0.20:
                    px(img, x, y, SHEEN[3] if (x + y) % 2 == 0 else SHEEN[2])
                elif band < 0.30:
                    px(img, x, y, SHEEN2[1] if BAYER4[y & 3, x & 3] < 0.7 else SHEEN2[0])
                elif band < 0.36 and BAYER4[y & 3, x & 3] < 0.4:
                    px(img, x, y, SHEEN[0])
        frames.append(img)
    save_anim(root, "fx/pollution/toxic_sheen", frames, items, "toxic sheen")


def noise_rings(root, items):
    W, H, n = 70, 36, 4
    frames = []
    for f in range(n):
        img = canvas(W, H)
        cx, cy = W / 2.0, H / 2.0
        for k in range(4):
            r = ((k + f / float(n)) * 8.5) % 34 + 3
            fade = r / 37.0                                   # farther = fainter
            for a in range(0, 360, 4):
                # dash pattern: break the ring into arcs; arcs thin out with radius
                seg = (a // 20 + k) % 3
                if seg == 2 or (fade > 0.6 and seg == 1):
                    continue
                x = cx + math.cos(math.radians(a)) * r
                y = cy + math.sin(math.radians(a)) * r * 0.5
                lit = 0.85 - fade * 0.7 + (0.1 if a > 180 else -0.05)
                px(img, x, y, tone(NOISE, lit, 0, 0, False))
        frames.append(img)
    save_anim(root, "fx/pollution/noise_rings", frames, items, "noise rings")


def dead_grass(variant):
    W, H = 52, 28
    img = canvas(W, H)
    rs = np.random.RandomState(50 + variant)
    cx, cy = W / 2.0, H / 2.0
    for y in range(H):
        for x in range(W):
            d = math.hypot((x + 0.5 - cx) / 24.0, (y + 0.5 - cy) / 12.0)
            ang = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
            d /= 1 + 0.15 * math.sin(ang * 3 + variant * 2)
            if d > 1:
                continue
            if d > 0.78 and BAYER4[y & 3, x & 3] < (d - 0.78) * 4.2:
                continue
            t = 0.28 + 0.22 * pnoise(x / 52.0, y / 28.0, 41 + variant) - 0.1 * d
            px(img, x, y, tone(DEAD, t, x, y))
    # withered tufts: little 2-3 px blades leaning, with pale tips
    for _ in range(14):
        x, y = int(rs.randint(8, W - 8)), int(rs.randint(8, H - 4))
        if img[y, x, 3] == 0:
            continue
        lean = int(rs.randint(-1, 2))
        for k in range(3):
            px(img, x + (lean if k == 2 else 0), y - k, DEAD[2 + (k > 1)])
        px(img, x + 1, y, DEAD[1])
    # cracked-earth specks
    for _ in range(10):
        x, y = int(rs.randint(6, W - 6)), int(rs.randint(6, H - 3))
        if img[y, x, 3]:
            px(img, x, y, DEAD[0])
            px(img, x + 1, y + (_ % 2), DEAD[0])
    return img


def dead(root, items):
    for v in (0, 1):
        save_still(root, "fx/pollution/dead_grass_%d.png" % (v + 1), dead_grass(v), items, "dead grass %d" % (v + 1))


def flood_frame(f, mask=None, seed=61):
    img = canvas(64, 32)
    for y in range(32):
        for x in range(64):
            if not DIAMOND[y, x] or (mask is not None and not mask[y, x]):
                continue
            u, v = tile_uv(x, y)
            n = pnoise(u, v, seed, phase=f / 4.0)
            base = 0.30 + 0.28 * n + (0.06 if (x + y * 2) % 8 < 2 else 0)
            # lit from the upper left: faintly lighter toward u+v small
            base += 0.10 * (1 - (u + v) / 2.0) - 0.04
            # ripple crests: thin lighter lines that travel (loop in 4 frames)
            rip = (u * 3 + v * 2 + f / 4.0 + 0.25 * math.sin(2 * math.pi * (u - v))) % 1.0
            if rip < 0.12 and BAYER4[y & 3, x & 3] < 0.85:
                px(img, x, y, FLOODHI[0 if rip > 0.05 else 1])
                continue
            px(img, x, y, tone(FLOOD, base, x, y))
    return img


def flood(root, items):
    frames = [flood_frame(f) for f in range(4)]
    save_anim(root, "fx/flood/water", frames, items, "flood water")


def flood_edges(root, items):
    # water covers half of the tile: it lies on the side named; ragged dithered shoreline with a muddy rim
    for name, (ax, ay) in (("n", (1, -1)), ("e", (1, 1)), ("s", (-1, 1)), ("w", (-1, -1))):
        mask = np.zeros((32, 64), bool)
        rim = np.zeros((32, 64), bool)
        for y in range(32):
            for x in range(64):
                sx, sy = x + 0.5 - 32, y + 0.5 - 16
                # signed distance along the direction's screen-axis (water side positive)
                dist = (sx * ax / 32.0 + sy * ay / 16.0) / 2.0
                u, v = tile_uv(x, y)
                dist += 0.10 * (pnoise(u, v, 77) - 0.5)
                if dist > 0.0:
                    mask[y, x] = True
                elif dist > -0.07:
                    rim[y, x] = True
        img = flood_frame(0, mask)
        for y in range(32):
            for x in range(64):
                if rim[y, x] and DIAMOND[y, x] and BAYER4[y & 3, x & 3] < 0.65:
                    px(img, x, y, FLOOD[1] if (x + y) % 2 else FLOOD[0])
        # foam line along the water side of the shore
        for y in range(1, 31):
            for x in range(1, 63):
                if mask[y, x] and DIAMOND[y, x] and not (mask[y - 1, x] and mask[y, x - 1] and mask[y + 1, x] and mask[y, x + 1]):
                    px(img, x, y, FLOODHI[1] if (x + y) % 3 else FLOODHI[2])
        save_still(root, "fx/flood/edge_%s.png" % name, img, items, "edge " + name.upper())


def build(root):
    pol, fl = [], []
    smog(root, pol)
    stains(root, pol)
    toxic(root, pol)
    noise_rings(root, pol)
    dead(root, pol)
    flood(root, fl)
    flood_edges(root, fl)
    return pol, fl


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    a, b = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in a + b:
        prev(sc, "p_" + os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 4, (96, 130, 80))
    print(len(a), len(b))
