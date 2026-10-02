"""Night lights for additive blending in game (black = no light). All pixels are opaque colour on transparency,
dark (faint) -> bright (strong), dithered falloff, alpha 0/255.

Anchors: ground glows (pools, cones) are anchored at the canvas centre = the light source position on the ground.
Window / sign glows are anchored at their centre.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa

WARM = ramp("#2a1a0c", "#6a4218", "#b8782a", "#ffc060", "#fff0b8")
COOL = ramp("#10202c", "#285068", "#4a90b0", "#a0e0f8", "#f0ffff")
HEADL = ramp("#30280e", "#7a6a24", "#c8b44a", "#fff0a0", "#ffffe8")
TAIL = ramp("#2a0808", "#6a1010", "#b81c14", "#ff4a30", "#ffc8a8")
NEON = {
    "red": ramp("#3a0c18", "#8a1a2c", "#e03050", "#ff7890", "#ffe0e8"),
    "blue": ramp("#0c1840", "#1a3a9a", "#3a78e8", "#80b8ff", "#e0f0ff"),
    "amber": ramp("#2a1a08", "#7a4a10", "#e09020", "#ffcc50", "#fff4c0"),
    "green": ramp("#0c2a14", "#1a6a30", "#30c060", "#80f0a0", "#e0ffe8"),
}


def glow_t(t, x, y):
    """Drop the faintest fringe with ordered dither so edges are stipple, not a hard ring."""
    return t > 0.06 + 0.12 * BAYER4[y & 3, x & 3]


def pool(rx, rp=WARM, bulb=False):
    """Iso 2:1 ellipse light pool on the ground, centre = lamp foot. Canvas 2*rx+3 by rx+3."""
    w, h = int(2 * rx + 3), int(rx + 3)
    img = canvas(w, h)
    cx, cy = w / 2.0, h / 2.0
    for y in range(h):
        for x in range(w):
            d = math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / (rx / 2.0))
            if d >= 1.0:
                continue
            t = (1 - d) ** 1.25
            if glow_t(t, x, y):
                px(img, x, y, tone(rp, t * 0.95, x, y))
    return img


def halo(w, h, rp, core, ring=1):
    """Window-ish glow piece: core mask (list of (x,y) pixels, relative) drawn bright, halo of `ring` px dithered.
    Returns canvas (w+2*ring+? ...)."""
    pad = ring + 1
    img = canvas(w + 2 * pad, h + 2 * pad)
    pts = set((x + pad, y + pad) for x, y in core)
    for y in range(img.shape[0]):
        for x in range(img.shape[1]):
            if (x, y) in pts:
                continue
            dmin = min(max(abs(x - px_), abs(y - py_)) if False else math.hypot((x - px_), (y - py_) * 1.0)
                       for px_, py_ in pts)
            if dmin <= ring + 0.5:
                t = 0.62 - 0.24 * (dmin - 1)
                if dmin <= 1.01 or BAYER4[y & 3, x & 3] < 0.7 - 0.25 * (dmin - 1):
                    px(img, x, y, tone(rp, t, x, y, False))
    for (x, y) in pts:
        px(img, x, y, rp[4] if (x + y) % 3 else rp[3])
    return img


def window(ww, wh, face, rp):
    """Lit window on an iso wall: ww x wh px, sheared 1 px per 2 columns. face 'L' slopes down-right, 'R' down-left."""
    core = []
    for cx_ in range(ww):
        off = cx_ // 2 if face == "L" else (ww - 1 - cx_) // 2
        for ry in range(wh):
            core.append((cx_, ry + off))
    return halo(ww, wh + (ww - 1) // 2, rp, core)


def sign(wd, ht, rp):
    """Shop sign: bar of wd x ht px on a left face with a wider halo."""
    core = []
    for cx_ in range(wd):
        for ry in range(ht):
            core.append((cx_, ry + cx_ // 2))
    img = halo(wd, ht + wd // 2, rp, core, ring=2)
    # inner pixels: a sparkle of lettering (darker dashes) so it reads as a sign
    for k in range(1, wd - 1, 2):
        px(img, 3 + k, 3 + (3 + k) // 2 - 1 + 0, rp[3])
    return img


def cone(dirn, L=0.55, rp=HEADL, w0=0.06, w1=0.27, W=44, H=28, back=0.0):
    """Ground light cone in iso. dirn in NE/SE/SW/NW; origin = canvas centre. Light spreads along the direction."""
    d = {"NE": (0, -1), "SE": (1, 0), "SW": (0, 1), "NW": (-1, 0)}[dirn]
    img = canvas(W, H)
    ox, oy = W / 2.0, H / 2.0
    for y in range(H):
        for x in range(W):
            sx, sy = x + 0.5 - ox, y + 0.5 - oy
            wx = (sx / 32.0 + sy / 16.0) / 2.0
            wy = (sy / 16.0 - sx / 32.0) / 2.0
            a = wx * d[0] + wy * d[1]
            l = -wx * d[1] + wy * d[0]
            if a < -back or a > L:
                continue
            hw = w0 + (w1 - w0) * max(a, 0) / L
            if abs(l) > hw:
                continue
            fall = (1 - max(a, 0) / L) ** 1.1
            side = 1 - (abs(l) / hw) ** 2
            t = fall * (0.35 + 0.65 * side)
            if glow_t(t, x, y):
                px(img, x, y, tone(rp, t * 1.05, x, y))
    return img


def tail(dirn):
    """Rear light glow: the vehicle faces dirn; glow is behind it. Canvas 20x14, origin = centre."""
    opp = {"NE": "SW", "SE": "NW", "SW": "NE", "NW": "SE"}[dirn]
    img = cone(opp, L=0.16, rp=TAIL, w0=0.08, w1=0.12, W=20, H=14)
    # two bright lamps (left / right of the axis, in world +- 0.07)
    d = {"NE": (0, -1), "SE": (1, 0), "SW": (0, 1), "NW": (-1, 0)}[dirn]
    for s in (-1, 1):
        wx, wy = -d[1] * s * 0.07, d[0] * s * 0.07
        sx, sy = (wx - wy) * 32, (wx + wy) * 16
        px(img, 10 + sx, 7 + sy, TAIL[4])
        px(img, 10 + sx + 1, 7 + sy, TAIL[3])
    return img


def bulb():
    """Lamp-head halo (the bulb seen from the side), 9x9, anchor centre."""
    img = canvas(9, 9)
    for y in range(9):
        for x in range(9):
            d = math.hypot(x - 4, y - 4) / 4.5
            if d < 1:
                t = (1 - d) ** 1.1
                if glow_t(t, x, y):
                    px(img, x, y, tone(WARM, t, x, y))
    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
        px(img, 4 + dx, 4 + dy, WARM[4])
    return img


def build(root):
    pools, windows, signs, cones = [], [], [], []
    save_still(root, "fx/night/pool_s.png", pool(14), pools, "pool S")
    save_still(root, "fx/night/pool_m.png", pool(22), pools, "pool M")
    save_still(root, "fx/night/pool_l.png", pool(30), pools, "pool L")
    save_still(root, "fx/night/pool_cool_m.png", pool(22, COOL), pools, "cool pool M")
    save_still(root, "fx/night/bulb.png", bulb(), pools, "lamp bulb")
    for tone_name, rp in (("warm", WARM), ("cool", COOL)):
        for ww, wh in ((2, 3), (3, 4)):
            for face in ("L", "R"):
                save_still(root, "fx/night/window_%s_%dx%d_%s.png" % (tone_name, ww, wh, face),
                           window(ww, wh, face, rp), windows, "%s %dx%d %s" % (tone_name, ww, wh, face))
    for name, rp in NEON.items():
        save_still(root, "fx/night/sign_%s.png" % name, sign(9, 3, rp), signs, "sign " + name)
    # world compass names (N = screen up-right, E = down-right, S = down-left, W = up-left); cone()/tail() take screen names
    world = (("N", "NE"), ("E", "SE"), ("S", "SW"), ("W", "NW"))
    for wn, dn in world:
        save_still(root, "fx/night/headlight_%s.png" % wn.lower(), cone(dn), cones, "head " + wn)
    for wn, dn in world:
        save_still(root, "fx/night/taillight_%s.png" % wn.lower(), tail(dn), cones, "tail " + wn)
    return pools, windows, signs, cones


if __name__ == "__main__":
    from iso_life_common import load_png
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    res = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    sheet = canvas(1, 1)
    for grp in res:
        row = [load_png(os.path.join(root, i["file"])) for i in grp]
        s = strip(row, gap=3)
        prev(sc, "night_%d.png" % res.index(grp), s, 5, (0, 0, 0))
    print([len(g) for g in res])
