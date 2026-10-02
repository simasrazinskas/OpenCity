"""Weather FX: rain, snow, puddles, fog, cloud shadow, lightning."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

RAIN = ramp("#6a88a8", "#9ab8d4", "#cde2f4", "#f2f8ff")
SNOW = ramp("#c4d2e4", "#e6eef8", "#ffffff")
PUDDLE = ramp("#3c4c62", "#52657e", "#6c829c", "#8ea6bf", "#c4d8ea")
FOG = ramp("#c8d0da", "#dde3ea", "#f0f3f7", "#ffffff")
SHADOW = ramp("#0e1626", "#1c2a42", "#2c3c58")
BOLT = ramp("#4a60c0", "#90a8ff", "#dce6ff", "#ffffff")


def rain_streaks(root, items):
    for name, ln in (("s", 6), ("m", 9), ("l", 13)):
        img = canvas(ln // 3 + 3, ln + 1)
        for k in range(ln):
            x = img.shape[1] - 2 - k // 3
            t = k / (ln - 1.0)                       # 0 head (bottom) ... but head is at k=0 (lowest, brightest)
            px(img, x, ln - k, RAIN[3] if k == 0 else RAIN[2] if k < ln * 0.4 else RAIN[1] if k < ln * 0.75 else RAIN[0])
        save_still(root, "fx/weather/rain_%s.png" % name, img, items, "rain " + name.upper())


def rain_splash(root, items):
    n = 4
    frames = []
    for f in range(n):
        img = canvas(13, 8)
        cx, cy = 6, 5
        r = 1.5 + f * 1.7
        for a in range(0, 360, 15):
            x = cx + math.cos(math.radians(a)) * r
            y = cy + math.sin(math.radians(a)) * r * 0.5
            if f == 3 and (a // 15) % 2:
                continue
            px(img, x, y, RAIN[3 - (f // 2)] if a > 180 else RAIN[2 - (f // 3)])
        if f < 2:
            px(img, cx, cy - 1 - f, RAIN[3])
            px(img, cx - 1 - f, cy - 2 - f, RAIN[2])
            px(img, cx + 1 + f, cy - 2 - f, RAIN[2])
        frames.append(img)
    save_anim(root, "fx/weather/rain_splash", frames, items, "rain splash")


def snow(root, items):
    # 1 px speck, 2x2 flake, 3x3 plus/star flake
    a = canvas(1, 1)
    px(a, 0, 0, SNOW[2])
    save_still(root, "fx/weather/snow_s.png", a, items, "flake S")
    b = canvas(2, 2)
    for x, y in ((0, 0), (1, 0), (0, 1), (1, 1)):
        px(b, x, y, SNOW[2] if (x, y) == (0, 0) else SNOW[1] if (x, y) != (1, 1) else SNOW[0])
    save_still(root, "fx/weather/snow_m.png", b, items, "flake M")
    c = canvas(3, 3)
    for x, y in ((1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
        px(c, x, y, SNOW[2] if (x, y) in ((1, 0), (0, 1), (1, 1)) else SNOW[1])
    px(c, 0, 0, SNOW[0])
    px(c, 2, 2, SNOW[0])
    save_still(root, "fx/weather/snow_l.png", c, items, "flake L")


def puddle(rx, seed):
    w, h = int(2 * rx + 4), int(rx + 4)
    img = canvas(w, h)
    cx, cy = w / 2.0, h / 2.0
    for y in range(h):
        for x in range(w):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / (rx / 2.0)
            ang = math.atan2(dy, dx)
            wob = 1 + 0.12 * math.sin(ang * 3 + seed) + 0.07 * math.sin(ang * 5 + seed * 2)
            d = math.hypot(dx, dy) / wob
            if d > 1.0:
                continue
            # lit rim at the upper-left inner edge, darker towards the lower right; sky reflection brightest centre
            t = 0.32 + 0.28 * (1 - d) - 0.12 * (dx * 0.6 + dy * 0.8)
            if d > 0.82:
                t = 0.18 + (0.55 if (dx + dy) < 0 else 0.0)       # wet rim: bright on the lit side
            px(img, x, y, tone(PUDDLE, t, x, y))
    # reflection glints
    gx, gy = int(cx - rx * 0.3), int(cy - rx * 0.12)
    for k in range(max(2, int(rx * 0.3))):
        px(img, gx + k, gy, PUDDLE[4])
    px(img, gx + 2, gy + 2, PUDDLE[3])
    px(img, gx + 3, gy + 2, PUDDLE[4])
    return img


def puddles(root, items):
    for name, rx, s in (("s", 7, 1.0), ("m", 12, 2.0), ("l", 18, 3.0)):
        save_still(root, "fx/weather/puddle_%s.png" % name, puddle(rx, s), items, "puddle " + name.upper())


def fog_tile(dens, seed=3, ramp_=FOG, w=64, h=32):
    img = canvas(w, h)
    for y in range(h):
        for x in range(w):
            u, v = tile_uv(x, y)
            if not DIAMOND[y, x]:
                continue
            n = pnoise(u, v, seed)
            cover = pn_clip(dens * (0.75 + 0.5 * n))
            if cover > BAYER4[y & 3, x & 3]:
                px(img, x, y, tone(ramp_, 0.35 + 0.7 * n, x, y))
    return img


def fog(root, items):
    save_still(root, "fx/weather/fog_light.png", fog_tile(0.42), items, "fog light")
    save_still(root, "fx/weather/fog_dense.png", fog_tile(0.82), items, "fog dense")


def cloud_shadow(shape):
    W, H = 204, 104
    img = canvas(W, H)
    rs = np.random.RandomState(40 + shape)
    blobs = []
    if shape == 0:
        blobs = [(0.0, 0.0, 70, 0.8), (-45, 8, 45, 0.7), (48, -6, 48, 0.75), (10, -14, 40, 0.6), (-15, 14, 38, 0.6)]
    else:
        blobs = [(-55, -6, 40, 0.75), (-18, 4, 52, 0.85), (22, -8, 46, 0.8), (58, 6, 34, 0.7), (20, 18, 36, 0.5),
                 (-30, -18, 28, 0.5)]
    for y in range(H):
        for x in range(W):
            sx, sy = x + 0.5 - W / 2.0, (y + 0.5 - H / 2.0) * 2.0      # iso: vertical stretch inverse
            f = 0.0
            for bx, by, r, k in blobs:
                d = math.hypot(sx - bx, sy - by * 2) / r
                if d < 1.0:
                    f = max(f, k * (1 - d * d) * 2.6)
            # soft edge: ordered dither only where the field is below 1 (solid, uniform core)
            if f > BAYER4[y & 3, x & 3] + 0.02:
                px(img, x, y, SHADOW[1] if f > 1.25 else SHADOW[0])
    return img


def cloud_shadows(root, items):
    for i in (0, 1):
        save_still(root, "fx/weather/cloud_shadow_%d.png" % i, cloud_shadow(i), items, "cloud shadow %d" % (i + 1))


def lightning(root, items):
    W, H = 36, 72
    frames = []
    # jagged path from top (x=20,y=0) to the ground (x=17, y=H-1)
    pts = [(20, 0), (18, 9), (23, 18), (15, 30), (21, 42), (14, 54), (17, H - 2)]
    branch = [(15, 30), (9, 38), (11, 47)]
    for f in range(3):
        img = canvas(W, H)
        if f == 0:
            # leader: thin, partial
            for a, b in zip(pts[:5], pts[1:5]):
                line(img, a[0], a[1], b[0], b[1], BOLT[1])
            px(img, pts[4][0], pts[4][1], BOLT[2])
        elif f == 1:
            # full strike with glow
            for a, b in zip(pts, pts[1:]):
                line(img, a[0] - 1, a[1], b[0] - 1, b[1], BOLT[1])
                line(img, a[0] + 1, a[1], b[0] + 1, b[1], BOLT[0])
            for a, b in zip(pts, pts[1:]):
                line(img, a[0], a[1], b[0], b[1], BOLT[3])
            for a, b in zip(branch, branch[1:]):
                line(img, a[0], a[1], b[0], b[1], BOLT[2])
            # ground flash: dithered iso ellipse
            for y in range(H - 8, H):
                for x in range(4, W - 4):
                    d = math.hypot((x - 17) / 12.0, (y - (H - 3)) / 5.0)
                    if d < 1 and (1 - d) > BAYER4[y & 3, x & 3] * 1.1:
                        px(img, x, y, BOLT[2] if d < 0.55 else BOLT[1])
        else:
            for a, b in zip(pts, pts[1:]):
                if (a[1] // 12) % 2 == 0:
                    line(img, a[0], a[1], b[0], b[1], BOLT[1])
            for a, b in zip(pts[3:], pts[4:]):
                line(img, a[0], a[1], b[0], b[1], BOLT[0])
        frames.append(img)
    save_anim(root, "fx/weather/lightning", frames, items, "lightning")


def build(root):
    items = []
    rain_streaks(root, items)
    rain_splash(root, items)
    snow(root, items)
    puddles(root, items)
    fog(root, items)
    cloud_shadows(root, items)
    lightning(root, items)
    return items


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    it = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in it:
        prev(sc, "w_" + os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 4, (90, 120, 80))
    print(len(it))
