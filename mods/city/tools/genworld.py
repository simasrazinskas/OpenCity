#!/usr/bin/env python3
"""
genworld.py - OpenCity world art generator (work package A2).

Deterministic and seeded. Writes every world image except tree-* into bits/world/,
the build-menu icon atlas, chrome-buildings.yaml and the A2 sequence files.

    python3 mods/city/tools/genworld.py [--sheet /tmp/contact.png]

Light comes from the top-left. Buildings are drawn in a 3/4 top-down style: the roof
is seen from above, the south facade below it, and tall buildings rise upward.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, save_atlas, shade, mix, with_alpha, hexcolor  # noqa: E402

MOD = os.path.dirname(HERE)
WORLD = os.path.join(MOD, "bits", "world")
CHROME = os.path.join(MOD, "bits", "chrome")
OUT = hexcolor("1B2230", 190)  # subtle dark navy outline
WARM = hexcolor("FFD98A")      # lit window
DARKWIN = hexcolor("2C3A52")   # unlit window
GLASS = hexcolor("7FB6DE")


def H(s):
    return hexcolor(s)


def rng(*key):
    return random.Random("opencity-a2:" + ":".join(str(k) for k in key))


# --------------------------------------------------------------------------- shadows
def shadow(c, x, yb, w, d, h, dx=None, dy=None, alpha=70):
    """Soft cast shadow toward the bottom-right on the ground.

    The footprint is x..x+w, yb-d..yb (ground rect). The shadow is that rect swept
    toward the bottom-right by an amount that grows with height.
    """
    sx = 2 + int(h * 0.30) if dx is None else dx
    sy = 1 + int(h * 0.18) if dy is None else dy
    mask = set()
    steps = max(sx, sy, 1)
    for i in range(steps + 1):
        ox, oy = round(sx * i / steps), round(sy * i / steps)
        for yy in range(yb - d + oy, yb + oy):
            for xx in range(x + ox, x + w + ox):
                mask.add((xx, yy))
    for (xx, yy) in mask:
        c.blend(xx, yy, (10, 18, 30, alpha))
    return mask


# --------------------------------------------------------------------------- boxes
def box3d(c, x, yb, w, d, h, roof, wall, light=True, gradient=True):
    """Extruded box. Ground rect is x..x+w, yb-d..yb; roof is lifted by h.

    Returns the roof rectangle (x, y, w, d).
    """
    ry = yb - h - d
    if h > 0:
        for i in range(h):
            t = i / max(1, h - 1)
            col = shade(wall, 1.06 - 0.22 * t) if gradient else wall
            c.rect(x, ry + d + i, w, 1, col)
        c.rect(x + w - 1, ry + d, 1, h, shade(wall, 0.66))
        c.rect(x, ry + d, 1, h, shade(wall, 1.12))
        c.rect(x, yb - 1, w, 1, shade(wall, 0.55))
    c.rect(x, ry, w, d, roof)
    if light:
        c.rect(x, ry, w, 1, shade(roof, 1.2))
        c.rect(x, ry, 1, d, shade(roof, 1.15))
        c.rect(x, ry + d - 1, w, 1, shade(roof, 0.78))
        c.rect(x + w - 1, ry, 1, d, shade(roof, 0.82))
    return (x, ry, w, d)


def pitched(c, x, y, w, d, col, ridge_vertical=False):
    """Gabled roof seen from above: two shaded halves plus a ridge line."""
    if ridge_vertical:
        hw = w // 2
        c.rect(x, y, hw, d, shade(col, 1.14))
        c.rect(x + hw, y, w - hw, d, shade(col, 0.82))
        c.rect(x + hw - 1, y, 1, d, shade(col, 0.6))
    else:
        hd = d // 2
        c.rect(x, y, w, hd, shade(col, 1.14))
        c.rect(x, y + hd, w, d - hd, shade(col, 0.82))
        c.rect(x, y + hd - 1, w, 1, shade(col, 0.6))
    n = rng("tiles", x, y, w, d)
    for _ in range(max(2, w * d // 28)):
        c.blend(x + n.randrange(w), y + n.randrange(d), (0, 0, 0, 22))
    c.rect(x, y, w, 1, shade(col, 1.3))
    c.rect(x, y + d - 1, w, 1, shade(col, 0.55))


# --------------------------------------------------------------------------- windows
def windows(c, x, y, w, h, cols, rows, lit=0.35, seed=0, lit_col=WARM, dark_col=DARKWIN, ww=2, wh=2, glassy=False):
    """Evenly spaced window grid inside rect x,y,w,h."""
    r = random.Random(seed)
    if cols <= 0 or rows <= 0:
        return
    cx = w / cols
    cy = h / rows
    for j in range(rows):
        for i in range(cols):
            px = int(x + i * cx + (cx - ww) / 2)
            py = int(y + j * cy + (cy - wh) / 2)
            on = r.random() < lit
            col = lit_col if on else dark_col
            if glassy and not on:
                col = mix(dark_col, GLASS, 0.35 + 0.3 * (1 - j / max(1, rows)))
            c.rect(px, py, ww, wh, col)
            if not on:
                c.blend(px, py, (255, 255, 255, 40))


def door(c, x, y, w=3, h=4, col=H("6B4A32")):
    c.rect(x, y, w, h, col)
    c.rect(x, y, w, 1, shade(col, 1.3))


# --------------------------------------------------------------------------- roof gear
def ac_unit(c, x, y, w=4, d=3):
    c.rect(x, y, w, d, H("C9CED6"))
    c.rect(x, y, w, 1, H("EEF1F5"))
    c.rect(x, y + d, w, 1, H("7B828E"))
    c.set(x + w // 2, y + d // 2, H("4C525C"))


def solar_panel(c, x, y, w=8, d=5):
    c.rect(x, y, w, d, H("27427A"))
    for i in range(1, w, 2):
        c.vline(x + i, y, y + d - 1, H("3A5FA5"))
    c.hline(x, x + w - 1, y + d // 2, H("1A2C55"))
    c.rect(x, y, w, 1, H("6D8FD0"))
    c.rect(x, y + d, w, 1, (20, 25, 40, 120))


def skylight(c, x, y, w=5, d=3):
    c.rect(x, y, w, d, H("BFE4F5"))
    c.rect(x, y, w, 1, H("F2FBFF"))
    c.rect_outline(x, y, w, d, H("5E7C92"))


def chimney(c, x, y_base, h=6, w=3, col=H("8A5A4A")):
    """Roof chimney: y_base is the y of its base on the roof."""
    c.rect(x, y_base - h, w, h, col)
    c.rect(x, y_base - h, w, 1, shade(col, 1.3))
    c.rect(x + w - 1, y_base - h, 1, h, shade(col, 0.7))
    c.rect(x, y_base - h - 1, w, 1, H("2A2A2A"))


def smoke(c, x, y, n=3, seed=0, base=7):
    r = random.Random(seed)
    for i in range(n):
        rr = base * 0.35 + i * 0.7
        c.circle(x + r.uniform(-1, 1) + i * 1.2, y - i * 4.5, rr, (235, 238, 242, int(200 - i * 45)))


def tree(c, cx, cy, r=4, col=H("3F9B4A"), trunk=True):
    """Tiny round tree: shadow, trunk, lit canopy (cy is the canopy centre)."""
    c.ellipse(cx + 1.5, cy + r - 0.5, r, max(2, r * 0.55), (10, 18, 30, 60))
    if trunk:
        c.rect(cx, cy + r - 1, 1, 2, H("5C4128"))
    c.circle(cx, cy, r, shade(col, 0.82))
    c.circle(cx - 0.5, cy - 0.5, r - 1, col)
    c.circle(cx - 1, cy - 1.2, max(1, r - 2.5), shade(col, 1.25))


def bush(c, cx, cy, col=H("4FA55B")):
    c.circle(cx, cy, 2, shade(col, 0.8))
    c.circle(cx - 0.5, cy - 0.5, 1.4, shade(col, 1.2))


def flowerbed(c, x, y, w, seed=0):
    r = random.Random(seed)
    for i in range(w):
        c.set(x + i, y, r.choice([H("F26B6B"), H("F7D154"), H("FFFFFF"), H("C77DE8")]))


def car_top(c, x, y, col, horizontal=True):
    """Tiny parked car (top view)."""
    if horizontal:
        c.rect(x, y, 5, 3, col)
        c.rect(x + 1, y, 2, 3, shade(col, 1.2))
        c.rect(x + 3, y, 1, 3, H("4A5C70"))
    else:
        c.rect(x, y, 3, 5, col)
        c.rect(x, y + 1, 3, 2, shade(col, 1.2))
        c.rect(x, y + 3, 3, 1, H("4A5C70"))


def finish(bld, base):
    """Outline the building layer, then composite it onto the base canvas."""
    bld.outline(OUT)
    base.blit(bld, 0, 0)
    return base


def new_layers(w, h):
    """(ground/shadow canvas, building canvas)."""
    return Canvas(w, h), Canvas(w, h)


# =========================================================================== growables
FW, FH = 32, 64


def facade(c, x, y, w, h, style, seed, accent=None, lit=0.35):
    """Draw windows on a south facade rect (x, y, w, h)."""
    if h < 4 or w < 4:
        return
    if style == "res":
        floors = max(1, h // 5)
        cols = max(2, (w - 2) // 4)
        fh = h / floors
        for j in range(floors):
            fy = int(y + j * fh)
            windows(c, x + 1, fy + 1, w - 2, int(fh) - 1, cols, 1, lit=lit, seed=seed * 31 + j, ww=2, wh=2)
            c.hline(x + 1, x + w - 2, int(fy + fh) - 1, (255, 255, 255, 55))  # balcony slab
    elif style == "glass":
        for i in range(h):
            t = i / max(1, h - 1)
            c.rect(x + 1, y + i, w - 2, 1, mix(H("8FC3E6"), H("3A6C9A"), t))
        for xx in range(x + 1, x + w - 1, 4):
            c.vline(xx, y, y + h - 1, (230, 245, 255, 70))
        floors = max(2, h // 4)
        r = random.Random(seed)
        for j in range(floors):
            yy = y + int(j * h / floors)
            c.hline(x + 1, x + w - 2, yy, (20, 40, 70, 90))
            for xx in range(x + 1, x + w - 3, 4):
                if r.random() < lit * 0.6:
                    c.rect(xx + 1, yy + 1, 3, max(1, h // floors - 2), (255, 226, 150, 150))
        # diagonal reflection streaks
        for k in range(0, w + h, 9):
            for i in range(h):
                px = x + k - i
                if x < px < x + w - 1:
                    c.blend(px, y + i, (255, 255, 255, 32))
    elif style == "retail":
        gh = min(6, h // 3)
        up = h - gh
        floors = max(1, up // 5)
        cols = max(2, (w - 2) // 4)
        if up >= 4:
            fh = up / floors
            for j in range(floors):
                windows(c, x + 1, int(y + j * fh) + 1, w - 2, int(fh) - 1, cols, 1, lit=lit, seed=seed * 7 + j)
        gy = y + up
        c.rect(x + 1, gy, w - 2, gh - 1, mix(H("A8D4EE"), H("5A90B8"), 0.4))
        for xx in range(x + 1, x + w - 1, 5):
            c.vline(xx, gy, gy + gh - 2, (255, 255, 255, 80))
        aw = accent or H("2C6FD1")
        for xx in range(x + 1, x + w - 1):
            col = aw if ((xx - x) // 2) % 2 == 0 else H("F4F4F4")
            c.set(xx, gy, col)
            c.set(xx, gy + 1, shade(col, 0.8))


def lawn(g, x, y, w, h, base=H("78BE55"), seed=0):
    g.rect(x, y, w, h, base)
    r = random.Random(seed)
    for _ in range(w * h // 9):
        px, py = x + r.randrange(w), y + r.randrange(h)
        g.set(px, py, shade(base, r.choice([0.9, 1.08])))
    g.rect_outline(x, y, w, h, shade(base, 0.75))


def fence(g, x, y, w, h, col=H("F3F0E6"), sides="tblr"):
    if "t" in sides:
        for xx in range(x, x + w, 2):
            g.set(xx, y, col)
    if "b" in sides:
        for xx in range(x, x + w, 2):
            g.set(xx, y + h - 1, col)
    if "l" in sides:
        for yy in range(y, y + h, 2):
            g.set(x, yy, col)
    if "r" in sides:
        for yy in range(y, y + h, 2):
            g.set(x + w - 1, yy, col)


ROOFS_RES = [H("C8593A"), H("5F7391"), H("8B5E3C"), H("D4803B")]
WALLS_RES = [H("F2E8D2"), H("F2DDA0"), H("ECECEC"), H("EBC9A8")]


def res_low(level, v):
    g, b = new_layers(FW, FH)
    r = rng("res-low", level, v)
    roof, wall = ROOFS_RES[v], WALLS_RES[(v + level) % 4]
    lawn(g, 1, 34, 30, 29, seed=v * 10 + level)
    fence(g, 1, 34, 30, 29, sides="tblr")
    g.rect(14 + v, 55, 4, 8, H("C9C4B8"))  # path to street
    if level == 1:
        x = 6 + (v % 2) * 4
        yb, w, d, h = 52, 14, 9, 6
        shadow(g, x, yb, w, d, h)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        pitched(b, rx, ry - 2, rw, rd + 2, roof, ridge_vertical=(v % 2 == 1))
        door(b, x + 3 + v, yb - 4)
        windows(b, x + 8, yb - 5, 5, 3, 2, 1, lit=0.5, seed=v)
        chimney(b, x + 9, ry + 3, 4)
        tree(g, 24, 46 + v % 2, 4, H("3F9B4A"))
        bush(g, x - 2, 56)
        flowerbed(g, 5, 59, 6, seed=v)
        car_top(g, 22, 56, [H("C0392B"), H("3C78C8"), H("E8E8E8"), H("2E2E2E")][v], horizontal=False) if v % 2 == 0 else None
    elif level == 2:
        x = 4 + (v % 2) * 3
        yb, w, d, h = 50, 17, 11, 8
        shadow(g, x, yb, w + 7, d, h)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        pitched(b, rx, ry - 3, rw, rd + 3, roof, ridge_vertical=False)
        gx = x + w
        box3d(b, gx, yb, 7, 8, 5, shade(roof, 0.92), shade(wall, 0.92))
        pitched(b, gx, yb - 5 - 8 - 1, 7, 9, shade(roof, 0.95), ridge_vertical=True)
        b.rect(gx + 1, yb - 4, 5, 3, shade(wall, 0.6))  # garage door
        door(b, x + 3, yb - 4)
        windows(b, x + 7, yb - 6, 9, 4, 3, 1, lit=0.45, seed=v + 3)
        chimney(b, x + 12, ry + 3, 5)
        tree(g, 5 + 20 * (v % 2), 56, 4, H("3F9B4A"))
        tree(g, 26 - 20 * (v % 2), 42, 3, H("52A857"))
        flowerbed(g, 6, 56, 7, seed=v + 5)
        solar_panel(b, x + 3, ry + 2, 6, 3) if v == 2 else None
    else:
        x = 3 + (v % 2) * 2
        yb, w, d, h = 55, 24, 13, 15
        shadow(g, x, yb, w, d, h)
        g.rect(1, 34, 30, 29, H("7A8A6A"))  # townhouse plot: more paved
        lawn(g, 1, 56, 30, 7, seed=v)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, shade(roof, 0.9), wall)
        for i in range(3):
            pitched(b, rx + i * 8, ry, 8, rd, shade(roof, 0.9 + 0.05 * (i % 2)), ridge_vertical=True)
        facade(b, x, ry + rd, w, h, "res", v * 3 + 1, lit=0.4)
        for i in range(3):
            door(b, x + 3 + i * 8, yb - 4, 3, 4)
        chimney(b, x + 6, ry + 4, 4)
        chimney(b, x + 18, ry + 4, 4)
        tree(g, 5, 59, 3, H("3F9B4A"))
        tree(g, 27, 59, 3, H("52A857"))
    return finish(b, g)


ROOFS_HI = [H("8E99A8"), H("B5A89A"), H("9C8F86"), H("7A8D8A")]
WALLS_HI = [H("C9805F"), H("E0C9A6"), H("B8B2A6"), H("D8A88A")]


def res_high(level, v):
    g, b = new_layers(FW, FH)
    roof, wall = ROOFS_HI[v], WALLS_HI[(v * 3 + level) % 4]
    g.rect(0, 33, 32, 31, H("B9B7AD"))
    g.rect(0, 33, 32, 1, H("9A988E"))
    lawn(g, 1, 59, 30, 4, seed=v + level * 7)
    heights = {1: 20, 2: 30, 3: 40}
    hh = heights[level]
    yb = 58 if level == 3 else 55
    if level == 3:
        layouts = [[(7, 18, 12, hh)], [(5, 13, 12, hh), (18, 10, 11, hh - 14)], [(4, 11, 11, hh - 8), (17, 11, 11, hh)], [(6, 20, 13, hh)]]
    else:
        layouts = [[(4, 24, 13, hh)], [(3, 14, 13, hh), (17, 12, 12, int(hh * 0.65))], [(4, 18, 13, hh), (22, 7, 9, int(hh * 0.55))], [(3, 26, 12, hh)]]
    parts = layouts[v]
    for (x, w, d, h) in parts:
        shadow(g, x, yb, w, d, h)
    for i, (x, w, d, h) in enumerate(sorted(parts, key=lambda p: p[3])):
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        facade(b, x, ry + rd, w, h, "res", v * 5 + i + level)
        ac_unit(b, rx + 2, ry + 2)
        if w > 12:
            ac_unit(b, rx + w - 7, ry + 3)
        if level == 3 and i == len(parts) - 1:
            b.rect(rx + w // 2 - 1, ry - 4, 1, 4, H("7B828E"))  # antenna
            b.set(rx + w // 2 - 1, ry - 5, H("E84545"))
        if level == 1 and v % 2 == 0:
            solar_panel(b, rx + w - 11, ry + 3, 8, 4)
        b.rect(x + w // 2 - 1, yb - 3, 3, 3, H("5E4632"))  # entrance
    tree(g, 3, 59, 3, H("3F9B4A"))
    tree(g, 28, 58, 3, H("57B05C"))
    return finish(b, g)


def lot(g, x, y, w, h, col=H("5B6068")):
    g.rect(x, y, w, h, col)
    g.rect(x, y, w, 1, shade(col, 0.8))
    g.noise(0.04, seed=x * 7 + y, x=x, y=y, w=w, h=h)


def parking(g, x, y, w, seed, cols):
    """Row of parking bays (white ticks) with a few parked cars."""
    r = random.Random(seed)
    n = max(1, w // 6)
    for i in range(n + 1):
        g.vline(x + i * 6, y, y + 4, (235, 235, 235, 150))
    for i in range(n):
        if r.random() < 0.6:
            car_top(g, x + 1 + i * 6, y + 1, r.choice(cols), horizontal=False)


CARCOLS = [H("C0392B"), H("3C78C8"), H("E8E8E8"), H("F2C94C"), H("2E8B57"), H("2E2E2E")]
ROOFS_COM = [H("E8E2D4"), H("D6DBE2"), H("F0D9A8"), H("CFE3F2")]
SIGNS = [H("2C6FD1"), H("E67E22"), H("1FA37A"), H("E8467C")]


def sign(b, x, y, w, col, seed=0):
    b.rect(x, y, w, 4, col)
    b.rect(x, y, w, 1, shade(col, 1.35))
    b.rect(x, y + 3, w, 1, shade(col, 0.7))
    r = random.Random(seed)
    for i in range(1, w - 1, 2):
        if r.random() < 0.7:
            b.set(x + i, y + 1, (255, 255, 255, 230))
            b.set(x + i, y + 2, (255, 255, 255, 200))


def com_low(level, v):
    g, b = new_layers(FW, FH)
    r = rng("com-low", level, v)
    roof = ROOFS_COM[v]
    acc = SIGNS[v]
    wall = [H("F6F1E4"), H("E9EEF4"), H("F4E3C4"), H("E4F0F6")][v]
    lot(g, 0, 33, 32, 31)
    g.hline(0, 31, 33, H("C9CDD2"))
    g.rect(0, 33, 32, 1, H("D5D8DC"))
    if level == 1:
        w, d, h, yb = 20, 11, 7, 48
    elif level == 2:
        w, d, h, yb = 26, 13, 9, 49
    else:
        w, d, h, yb = 30, 15, 11, 50
    x = (32 - w) // 2
    shadow(g, x, yb, w, d, h)
    rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
    b.rect(x, ry + rd, w, 2, acc)  # coloured fascia
    # storefront glass + awning
    gx, gw = x + 2, w - 4
    b.rect(gx, yb - 5, gw, 4, mix(H("A8D4EE"), H("5A90B8"), 0.4))
    for xx in range(gx, gx + gw, 4):
        b.vline(xx, yb - 5, yb - 2, (255, 255, 255, 90))
    for xx in range(gx, gx + gw):
        col = acc if ((xx - gx) // 2) % 2 == 0 else H("F4F4F4")
        b.set(xx, yb - 6, col)
    door(b, x + w // 2 - 1, yb - 4, 3, 4, shade(acc, 0.7))
    # roof gear
    ac_unit(b, rx + 2, ry + 2)
    skylight(b, rx + w - 8, ry + 3, 5, 3)
    if level >= 2:
        ac_unit(b, rx + 8, ry + 3)
        skylight(b, rx + w - 15, ry + 3, 4, 3)
    if level == 2:
        # pylon sign
        b.rect(x + w + 0 - 4, ry - 7, 1, 8, H("7B828E"))
        sign(b, x + w - 7, ry - 11, 7, acc, v)
    if level == 3:
        sign(b, x + 4, ry - 7, w - 8, acc, v + 9)
        b.rect(x + 6, ry - 3, 1, 3, H("7B828E"))
        b.rect(x + w - 7, ry - 3, 1, 3, H("7B828E"))
        # entrance canopy
        b.rect(x + w // 2 - 4, yb - 1, 8, 2, shade(acc, 0.9))
    parking(g, 2, yb + 3, 28, v * 5 + level, CARCOLS)
    g.hline(1, 30, 62, (235, 235, 235, 110))
    if v % 2 == 0:
        tree(g, 3, 36, 3, H("4CA654"))
    return finish(b, g)


def com_high(level, v):
    g, b = new_layers(FW, FH)
    acc = SIGNS[v]
    roof = [H("C9D0DA"), H("B8C4D2"), H("D8D4CC"), H("AEBBC8")][v]
    wall = [H("E6EAF0"), H("DCE4EE"), H("F1E8D8"), H("D6E2EC")][v]
    g.rect(0, 33, 32, 31, H("B7BAC0"))
    g.rect(0, 33, 32, 1, H("D9DBDF"))
    for xx in range(0, 32, 4):
        g.set(xx, 62, H("9FA3AA"))
    hh = {1: 18, 2: 28, 3: 36}[level]
    yb = 58 if level == 3 else 56
    layouts = [[(4, 24, 14, hh)], [(3, 15, 14, hh), (18, 11, 12, int(hh * 0.6))],
               [(5, 22, 14, hh)], [(3, 12, 13, int(hh * 0.7)), (15, 14, 14, hh)]]
    parts = layouts[v]
    for (x, w, d, h) in parts:
        shadow(g, x, yb, w, d, h)
    for i, (x, w, d, h) in enumerate(sorted(parts, key=lambda p: p[3])):
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        facade(b, x, ry + rd, w, h, "retail", v * 13 + i + level, accent=acc, lit=0.4)
        b.rect(x, ry + rd - 1, w, 2, acc)  # coloured parapet band
        ac_unit(b, rx + 2, ry + 3)
        skylight(b, rx + w - 8, ry + 4, 4, 3)
    # rooftop billboard on the tallest part
    x, w, d, h = max(parts, key=lambda p: p[3])
    ry = yb - h - d
    if level >= 2:
        sign(b, x + 3, ry - 5, w - 6, acc, v + level)
        b.rect(x + 5, ry - 1, 1, 2, H("7B828E"))
        b.rect(x + w - 6, ry - 1, 1, 2, H("7B828E"))
    if level == 3:
        b.rect(x + w // 2, ry - 5, 1, 4, H("7B828E"))
        b.set(x + w // 2, ry - 6, H("E84545"))
    tree(g, 3, 60, 2, H("4CA654"))
    tree(g, 29, 60, 2, H("4CA654"))
    return finish(b, g)


ROOFS_IND = [H("8C949E"), H("A5ADB6"), H("7E8791"), H("9AA3A0")]
WALLS_IND = [H("C5CAD0"), H("D1D4D6"), H("B9C0C8"), H("CBCFC8")]
YELLOW = H("E3B735")


def tank(b, cx, cy, r, col=H("B9C6D0")):
    """Cylindrical storage tank seen from above/south."""
    b.rect(cx - r, cy, 2 * r, 5, shade(col, 0.82))
    b.ellipse(cx, cy + 5, r, r * 0.45 + 0.5, shade(col, 0.7))
    b.ellipse(cx, cy, r, r * 0.45 + 0.5, shade(col, 1.12))
    b.hline(cx - r, cx + r - 1, cy + 2, shade(col, 0.6))
    b.ellipse(cx - 1, cy - 0.5, r * 0.45, r * 0.18 + 0.3, (255, 255, 255, 90))


def stack(b, g, x, yb, h, w=4, seed=0, puff=True):
    """Industrial smokestack standing on the ground at (x, yb)."""
    shadow(g, x, yb, w, 3, h, dx=int(h * 0.35), dy=int(h * 0.12), alpha=55)
    for i in range(h):
        col = mix(H("C9805F"), H("8A5340"), i / h)
        b.rect(x, yb - i, w, 1, col)
    for i in range(0, h, 4):
        b.rect(x, yb - i, w, 1, (255, 255, 255, 40))
    b.rect(x, yb - h, w, 2, H("2E2E2E"))
    b.rect(x + w - 1, yb - h, 1, h, (0, 0, 0, 55))
    if puff:
        smoke(b, x + w // 2, yb - h - 3, 3, seed, 6)


def com_ind_pad(g, v):
    g.rect(0, 33, 32, 31, H("A5A9AD"))
    g.noise(0.05, seed=v, x=0, y=33, w=32, h=31)
    g.rect(0, 33, 32, 1, H("85898E"))
    for xx in range(0, 32, 6):
        g.rect(xx, 62, 3, 1, YELLOW)  # hazard dashes


def ind(level, v):
    g, b = new_layers(FW, FH)
    roof, wall = ROOFS_IND[v], WALLS_IND[v]
    com_ind_pad(g, v)
    if level == 1:
        x, yb, w, d, h = 5 + v % 2 * 2, 54, 20, 13, 8
        shadow(g, x, yb, w, d, h)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        for i in range(0, rw - 3, 5):  # sawtooth roof
            b.rect(rx + i, ry + 1, 3, rd - 2, shade(roof, 1.18))
            b.rect(rx + i + 3, ry + 1, 2, rd - 2, shade(roof, 0.82))
        b.rect(x, ry + rd, w, 2, YELLOW)
        for i in range(2):
            b.rect(x + 3 + i * 8, yb - 5, 6, 5, shade(YELLOW, 0.8))
            for k in range(0, 5, 2):
                b.hline(x + 3 + i * 8, x + 8 + i * 8, yb - 5 + k, (0, 0, 0, 50))
        ac_unit(b, rx + rw - 5, ry + 3)
        b.rect(26, 40 + v, 4, 4, H("D2873A"))  # crate/container
        b.rect(26, 40 + v, 4, 1, H("EBA860"))
        tank(b, 27, 46, 3) if v % 2 else None
    elif level == 2:
        x, yb, w, d, h = 3, 54, 22, 13, 10
        shadow(g, x, yb, w, d, h)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        for i in range(0, rw - 3, 6):
            b.rect(rx + i, ry + 1, 3, rd - 2, shade(roof, 1.15))
        b.rect(x, ry + rd, w, 2, YELLOW)
        windows(b, x + 2, ry + rd + 3, w - 4, 3, 5, 1, lit=0.5, seed=v)
        b.rect(x + 8, yb - 5, 6, 5, shade(YELLOW, 0.8))
        stack(b, g, 24 + v % 2 * 2, 46, 18, 4, seed=v)
        tank(b, 27, 54, 4)
    else:
        x, yb, w, d, h = 2, 56, 19, 14, 11
        shadow(g, x, yb, w, d, h)
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wall)
        for i in range(0, rw - 3, 6):
            b.rect(rx + i, ry + 1, 3, rd - 2, shade(roof, 1.15))
        b.rect(x, ry + rd, w, 2, YELLOW)
        windows(b, x + 2, ry + rd + 3, w - 4, 4, 4, 1, lit=0.5, seed=v)
        b.rect(x + 7, yb - 5, 6, 5, shade(YELLOW, 0.8))
        # pipes across the roof
        b.hline(rx + 1, rx + rw + 8, ry + 3, H("D9A441"))
        stack(b, g, 23, 50, 26, 4, seed=v)
        stack(b, g, 28, 54, 22, 3, seed=v + 5, puff=(v % 2 == 0))
        tank(b, 25, 59, 4)
        tank(b, 10 + v % 2, 38, 3) if False else None
    return finish(b, g)


def off(level, v):
    g, b = new_layers(FW, FH)
    roof = [H("D7DCE2"), H("C7D0DA"), H("CFCFD2"), H("BFC9D6")][v]
    g.rect(0, 33, 32, 31, H("D8DADC"))
    g.rect(0, 33, 32, 1, H("B4B8BC"))
    lawn(g, 1, 56, 30, 7, base=H("72B552"), seed=v)
    hh = {1: 18, 2: 30, 3: 40}[level]
    yb = 58 if level == 3 else 55
    layouts = [[(5, 22, 14, hh)], [(4, 14, 14, hh), (18, 10, 12, int(hh * 0.65))],
               [(6, 20, 14, hh)], [(4, 24, 14, hh)]]
    if level == 3:
        layouts = [[(6, 20, 13, hh)], [(5, 13, 13, hh), (18, 9, 11, hh - 14)], [(7, 18, 13, hh)], [(4, 11, 12, hh - 10), (16, 12, 13, hh)]]
    parts = layouts[v]
    for (x, w, d, h) in parts:
        shadow(g, x, yb, w, d, h)
    for i, (x, w, d, h) in enumerate(sorted(parts, key=lambda p: p[3])):
        wallc = [H("6FA3CC"), H("5C8DB8"), H("78A9C4"), H("6A98C2")][v]
        rx, ry, rw, rd = box3d(b, x, yb, w, d, h, roof, wallc)
        facade(b, x, ry + rd, w, h, "glass", v * 11 + i + level, lit=0.5)
        b.rect(x, ry + rd, w, 1, shade(roof, 0.7))
        b.rect(x, yb - 3, w, 3, (30, 50, 80, 150))  # lobby
        b.rect(x + w // 2 - 2, yb - 3, 4, 3, (255, 232, 170, 220))
        # roof: skylights, AC, helipad on the first tall block
        skylight(b, rx + 2, ry + 2, 4, 3)
        ac_unit(b, rx + w - 6, ry + 3)
        if level >= 2 and w >= 14 and i == len(parts) - 1 and v % 2 == 0:
            b.rect_outline(rx + w // 2 - 3, ry + 2, 7, 7, H("F4F4F4"))
            b.hline(rx + w // 2 - 1, rx + w // 2 + 1, ry + 5, H("F4F4F4"))
    x, w, d, h = max(parts, key=lambda p: p[3])
    ry = yb - h - d
    if level == 3:
        b.rect(x + w // 2, ry - 4, 1, 4, H("7B828E"))
        b.set(x + w // 2, ry - 5, H("E84545"))
        b.rect(x + 3, ry - 2, w - 6, 2, shade(roof, 0.9))  # crown
    tree(g, 2, 60, 3, H("3F9B4A"))
    tree(g, 29, 60, 3, H("52A857"))
    return finish(b, g)


# =========================================================================== services
def pad(g, x, y, w, h, col=H("B4B6B8"), edge=None):
    g.rect(x, y, w, h, col)
    g.noise(0.04, seed=x * 13 + y, x=x, y=y, w=w, h=h)
    g.rect_outline(x, y, w, h, edge or shade(col, 0.8))


def cross(b, cx, cy, s=3, col=H("D93636"), bg=None):
    if bg:
        b.rect(cx - s - 1, cy - s - 1, 2 * s + 3, 2 * s + 3, bg)
    b.rect(cx - 1, cy - s, 3, 2 * s + 1, col)
    b.rect(cx - s, cy - 1, 2 * s + 1, 3, col)


def tilepath(g, pts, col=H("D8CDB0"), width=3):
    for (x0, y0, x1, y1) in pts:
        g.line(x0, y0, x1, y1, shade(col, 0.82), width + 2)
    for (x0, y0, x1, y1) in pts:
        g.line(x0, y0, x1, y1, col, width)


def bench(c, x, y):
    c.rect(x, y, 5, 2, H("8B5E3C"))
    c.rect(x, y + 2, 5, 1, (0, 0, 0, 80))
    c.rect(x, y - 1, 5, 1, H("B0825A"))


def lamp(c, x, y):
    c.rect(x, y, 1, 4, H("4A4F58"))
    c.set(x, y - 1, H("FFE9A8"))


def windturbine(frames=8):
    out = []
    for f in range(frames):
        g, b = new_layers(32, 64)
        pad(g, 8, 50, 16, 12, H("B9BBBD"))
        # long tower shadow toward bottom-right
        g.line(17, 57, 29, 60, (10, 18, 30, 55), 2)
        hx, hy = 16, 22
        for i in range(hy, 57):
            t = (i - hy) / 35.0
            wd = 2 + int(t * 2)
            b.rect(16 - wd // 2 - 1, i, wd + 1, 1, mix(H("F4F6F8"), H("C7CDD4"), 0.2))
            b.set(16 + wd // 2, i, H("AAB2BC"))
        b.rect(13, 55, 7, 3, H("D8DCE0"))
        b.rect(13, 57, 7, 1, H("8D95A0"))
        # nacelle + blades (3 blades, f/frames * 120 degrees)
        base = f * 120.0 / frames
        for k in range(3):
            a = math.radians(base + k * 120.0)
            tx, ty = hx + math.sin(a) * 17, hy - math.cos(a) * 17
            nx, ny = math.cos(a), math.sin(a)
            poly = [(hx + nx * 1.6, hy + ny * 1.6), (tx + nx * 0.4, ty + ny * 0.4), (tx - nx * 0.4, ty - ny * 0.4),
                    (hx - nx * 1.6, hy - ny * 1.6)]
            b.polygon(poly, H("F6F8FA"))
            b.line(hx, hy, tx, ty, H("C2C9D1"), 1)
        b.circle(hx, hy, 2.6, H("DDE2E7"))
        b.circle(hx - 0.5, hy - 0.5, 1.5, H("FFFFFF"))
        out.append(finish(b, g))
    return out


def watertower():
    g, b = new_layers(32, 64)
    pad(g, 4, 42, 24, 20, H("B4B6B8"))
    shadow(g, 7, 58, 18, 6, 26, alpha=60)
    for x in (9, 21):
        b.rect(x, 40, 2, 18, H("707A86"))
        b.rect(x, 40, 1, 18, H("9AA4B0"))
    b.line(10, 58, 22, 42, H("707A86"))
    b.line(22, 58, 10, 42, H("707A86"))
    # tank
    b.rect(6, 20, 20, 22, H("7FB2DE"))
    for i in range(22):
        b.rect(6, 20 + i, 20, 1, mix(H("A9D0EE"), H("5C95C4"), i / 21.0))
    b.rect(6, 20, 3, 22, (255, 255, 255, 50))
    b.rect(23, 20, 3, 22, (0, 0, 40, 45))
    b.rect(6, 30, 20, 1, H("4C7FAB"))
    b.ellipse(16, 20, 10, 4, H("C9E3F5"))
    b.polygon([(6, 20), (16, 10), (26, 20)], H("4F82B0"))
    b.polygon([(6, 20), (16, 10), (16, 20)], H("6FA3D2"))
    b.rect(16, 6, 1, 5, H("606A76"))
    b.rect(10, 34, 4, 4, (255, 255, 255, 90))  # droplet badge
    return [finish(b, g)]


def waterpump():
    g, b = new_layers(32, 64)
    pad(g, 1, 33, 30, 30, H("A9B4A8"))
    g.ellipse(21, 52, 10, 6, H("2F72B8"))
    g.ellipse(21, 52, 8, 4.5, H("4B9AE0"))
    g.hline(16, 22, 51, (255, 255, 255, 120))
    g.hline(22, 26, 54, (255, 255, 255, 70))
    shadow(g, 5, 52, 13, 9, 9)
    rx, ry, rw, rd = box3d(b, 5, 52, 13, 9, 9, H("4B7FB2"), H("DCE4EA"))
    pitched(b, rx, ry - 1, rw, rd + 1, H("4B7FB2"))
    door(b, 9, 49, 3, 4, H("3C5A78"))
    b.rect(19, 38, 2, 14, H("7C8A98"))     # vertical pipe
    b.rect(19, 38, 7, 2, H("7C8A98"))
    b.rect(25, 38, 2, 8, H("9AAABB"))
    b.rect(19, 38, 1, 14, H("A8B6C4"))
    b.circle(26, 47, 1.6, H("4B9AE0"))
    return [finish(b, g)]


def park_small():
    g, b = new_layers(32, 64)
    lawn(g, 0, 33, 32, 31, base=H("72BF55"), seed=3)
    tilepath(g, [(0, 54, 32, 54), (16, 33, 16, 63)], width=3)
    g.circle(16, 54, 4, H("D8CDB0"))
    flowerbed(g, 4, 41, 5, seed=2)
    flowerbed(g, 23, 60, 5, seed=4)
    bench(g, 21, 47)
    lamp(b, 11, 50)
    tree(g, 8, 44, 6, H("3F9B4A"))
    tree(g, 24, 38, 5, H("57B05C"))
    tree(b, 6, 40, 6, H("3A9347"))
    tree(b, 25, 36, 5, H("4CA654"))
    bush(g, 28, 58)
    return [finish(b, g)]


def plaza():
    g, b = new_layers(32, 64)
    for y in range(33, 64):
        for x in range(0, 32):
            col = H("D9D3C3") if ((x // 4) + ((y - 33) // 4)) % 2 == 0 else H("CCC5B2")
            g.set(x, y, col)
    g.rect_outline(0, 33, 32, 31, H("A59F8E"))
    # fountain
    g.ellipse(16, 51, 10, 7, H("8F99A6"))
    g.ellipse(16, 50, 9, 6, H("5FA8E6"))
    g.ellipse(16, 50, 6, 4, H("8CCBF5"))
    g.hline(11, 15, 48, (255, 255, 255, 140))
    b.rect(15, 44, 3, 6, H("A8D8F5"))
    b.rect(16, 40, 1, 5, (255, 255, 255, 200))
    b.circle(16, 42, 2.4, (255, 255, 255, 150))
    for dx in (-4, 4):
        b.line(16, 44, 16 + dx, 48, (200, 235, 255, 180))
    bench(g, 3, 58)
    bench(g, 24, 58)
    tree(g, 4, 38, 4, H("3F9B4A"))
    tree(b, 4, 36, 4, H("4CA654"))
    tree(b, 27, 36, 4, H("3F9B4A"))
    lamp(b, 28, 58)
    lamp(b, 3, 48)
    return [finish(b, g)]


def park_large():
    g, b = new_layers(64, 96)
    lawn(g, 0, 32, 64, 64, base=H("6DBA52"), seed=9)
    for (x, y, rx, ry) in ((10, 66, 8, 5),):
        pass
    g.ellipse(46, 74, 13, 9, H("3C7FB8"))
    g.ellipse(46, 74, 11, 7, H("58A6E0"))
    g.hline(40, 48, 72, (255, 255, 255, 120))
    tilepath(g, [(0, 76, 20, 70), (20, 70, 32, 50), (32, 50, 32, 96), (32, 50, 64, 42), (8, 94, 32, 80)], width=4)
    g.circle(32, 50, 5, H("D8CDB0"))
    # playground
    g.rect(6, 40, 14, 10, H("E2C48D"))
    g.rect_outline(6, 40, 14, 10, H("B8975F"))
    g.rect(9, 43, 5, 2, H("D94F4F"))
    g.rect(10, 46, 6, 1, H("4C78C8"))
    bench(g, 22, 62)
    bench(g, 52, 88)
    flowerbed(g, 38, 90, 8, seed=1)
    flowerbed(g, 4, 90, 6, seed=2)
    spots = [(8, 50), (56, 40), (55, 56), (10, 66), (20, 86), (58, 90), (26, 40), (44, 90), (4, 80)]
    for i, (x, y) in enumerate(spots):
        tree(g, x, y - 2, 6 if i % 2 else 5, [H("3F9B4A"), H("52A857"), H("3A9347")][i % 3])
    for i, (x, y) in enumerate([(8, 44), (56, 38), (55, 52), (20, 80), (58, 84), (28, 36), (4, 70)]):
        tree(b, x, y - 6, 7 if i % 2 else 6, [H("3A9347"), H("4CA654")][i % 2])
    lamp(b, 34, 70)
    lamp(b, 30, 86)
    return [finish(b, g)]


def coal_frame(t):
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("9EA2A6"))
    for xx in range(0, 64, 6):
        g.rect(xx, 94, 3, 1, YELLOW)
    # coal heap + conveyor
    for i in range(10):
        g.ellipse(11, 84, 11 - i, 7 - i * 0.6, shade(H("2B2D31"), 1 + i * 0.05))
    shadow(g, 4, 70, 40, 22, 20)
    rx, ry, rw, rd = box3d(b, 4, 70, 40, 20, 18, H("7D8791"), H("B8BFC7"))
    for i in range(0, rw - 2, 6):
        b.rect(rx + i, ry + 1, 3, rd - 2, shade(H("7D8791"), 1.15))
    b.rect(4, ry + rd, 40, 2, YELLOW)
    windows(b, 7, ry + rd + 4, 34, 5, 7, 1, lit=0.5, seed=2)
    b.rect(10, 66, 8, 4, shade(YELLOW, 0.7))
    b.line(14, 60, 22, 50, H("5E6670"), 2)
    stack(b, g, 48, 62, 40, 6, seed=1, puff=False)
    stack(b, g, 56, 70, 34, 5, seed=2, puff=False)
    for (sx, sy) in ((51, 22), (58, 34)):
        # animated smoke: puffs drift upward and fade (4 frames)
        for i in range(4):
            k = (i + t * 1.0) % 4
            b.circle(sx + k * 1.5 + (2 if i % 2 else 0), sy - k * 5, 3.5 + k * 1.1, (232, 235, 240, int(215 - k * 52)))
    return finish(b, g)


def solarplant():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A9B0A4"))
    g.noise(0.05, seed=4)
    for row in range(3):
        for col in range(4):
            if row == 2 and col == 3:
                continue
            x, y = 4 + col * 14, 50 + row * 14
            shadow(g, x + 1, y + 8, 12, 4, 5, dx=2, dy=2, alpha=55)
    for row in range(3):
        for col in range(4):
            if row == 2 and col == 3:
                continue
            x, y = 4 + col * 14, 36 + row * 15 + 2
            b.rect(x, y, 12, 9, H("23417E"))
            for i in range(0, 12, 3):
                b.vline(x + i, y, y + 8, H("3E69B6"))
            b.hline(x, x + 11, y + 4, H("1B3267"))
            b.rect(x, y, 12, 1, H("8FB0E8"))
            b.rect(x, y + 9, 12, 2, H("3A4A66"))
            b.rect(x + 5, y + 11, 2, 2, H("5E6670"))
    shadow(g, 50, 92, 12, 8, 8)
    rx, ry, rw, rd = box3d(b, 50, 92, 12, 8, 8, H("C9D0D8"), H("E8ECF0"))
    b.rect(52, 88, 3, 4, H("5A6A80"))
    b.rect(rx + 3, ry + 2, 6, 3, H("FFD54A"))
    return finish(b, g)


def vehicle_top(c, x, y, w, h, col, light=None):
    """Small top-down service vehicle (horizontal if w > h)."""
    c.rect(x, y, w, h, col)
    c.rect(x, y, w, 1, shade(col, 1.25))
    if w > h:
        c.rect(x + 2, y + 1, 2, h - 2, H("5A7E9E"))
        c.rect(x + w - 3, y + 1, 1, h - 2, H("5A7E9E"))
    else:
        c.rect(x + 1, y + 2, w - 2, 2, H("5A7E9E"))
    if light:
        c.set(x + w // 2, y + h // 2, light)


def police():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A9ADB2"))
    lot(g, 0, 80, 64, 16, H("5B6068"))
    parking(g, 6, 82, 30, 3, [H("F4F6F8")])
    for i in range(3):
        vehicle_top(g, 8 + i * 6, 84, 3, 7, H("F2F4F6"), H("3C78F0") if i == 1 else None) if i != 2 else None
    shadow(g, 6, 78, 46, 24, 18)
    rx, ry, rw, rd = box3d(b, 6, 78, 46, 24, 18, H("3B5FB8"), H("E6EBF2"))
    b.rect(6, ry + rd, 46, 3, H("2D4A94"))       # blue fascia band
    windows(b, 9, ry + rd + 4, 40, 6, 8, 1, lit=0.45, seed=7)
    b.rect(26, 72, 8, 6, H("2D4A94"))             # entrance
    b.rect(27, 73, 6, 5, (180, 215, 245, 220))
    # shield badge + sign
    b.polygon([(28, ry + rd + 5 - 6), (34, ry + rd + 5 - 6), (34, ry + rd + 5), (31, ry + rd + 8), (28, ry + rd + 5)], H("F2C94C"))
    for i in range(2):
        b.rect(rx + 3, ry + 3 + i * 3, 12, 2, H("5C80D8"))
    skylight(b, rx + 22, ry + 4, 6, 4)
    b.rect(rx + 33, ry + 4, 8, 6, H("2D4A94"))
    b.rect_outline(rx + 33, ry + 4, 8, 6, H("F4F6F8"))
    b.rect(rx + 36, ry + 6, 2, 2, H("F4F6F8"))
    # light bar beacon (red + blue)
    b.rect(rx + 18, ry - 3, 3, 3, H("E84545"))
    b.rect(rx + 21, ry - 3, 3, 3, H("3C78F0"))
    # flagpole
    b.rect(rx + 44, ry - 10, 1, 11, H("C9CED6"))
    b.rect(rx + 40, ry - 10, 4, 3, H("3C78F0"))
    tree(g, 59, 44, 4, H("3F9B4A"))
    tree(b, 3, 38, 3, H("3F9B4A"))
    return finish(b, g)


def firestation():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("AAAEB2"))
    g.rect(0, 80, 64, 16, H("62666E"))
    for i in range(3):
        g.rect(8 + i * 17, 80, 12, 16, H("5A5E66"))
        g.hline(8 + i * 17, 19 + i * 17, 80, (255, 255, 255, 120))
    shadow(g, 5, 78, 46, 22, 18)
    rx, ry, rw, rd = box3d(b, 5, 78, 46, 22, 17, H("C23B3B"), H("EBC8C0"))
    pitched(b, rx + 0, ry, rw, rd, H("B83232"))
    b.rect(5, ry + rd, 46, 2, H("A82A2A"))
    for i in range(3):
        gx = 8 + i * 14
        b.rect(gx, 66, 11, 12, H("D8DCE2"))
        for k in range(66, 78, 3):
            b.hline(gx, gx + 10, k, H("9AA2AE"))
        b.rect(gx, 66, 11, 1, H("F0F2F5"))
        b.rect_outline(gx - 1, 65, 13, 13, H("A82A2A"))
    # hose tower
    shadow(g, 46, 66, 10, 10, 30, alpha=60)
    box3d(b, 46, 66, 10, 10, 30, H("A82A2A"), H("D9ADA4"))
    windows(b, 48, 40, 6, 20, 2, 4, lit=0.5, seed=3)
    b.rect(48, 36, 6, 2, H("F2C94C"))
    b.rect(51, 28, 1, 6, H("C9CED6"))
    b.rect(rx + 8, ry + 4, 18, 8, H("8E2A2A"))
    b.rect_outline(rx + 8, ry + 4, 18, 8, H("F2C94C"))
    cross(b, rx + 17, ry + 8, 2, H("F2C94C"))
    vehicle_top(g, 12, 86, 8, 4, H("E23A3A")) if False else None
    # fire truck parked in front
    g.rect(40, 82, 6, 12, H("CC2F2F"))
    g.rect(40, 82, 6, 2, H("F0E2A0"))
    g.rect(41, 85, 4, 2, H("5A7E9E"))
    g.hline(41, 44, 90, H("F4F6F8"))
    g.rect(40, 82, 1, 12, H("F26B6B"))
    tree(g, 59, 90, 3, H("3F9B4A"))
    return finish(b, g)


def clinic():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B6BCC0"))
    lawn(g, 0, 86, 64, 10, base=H("78BE55"), seed=5)
    shadow(g, 6, 78, 44, 24, 16)
    rx, ry, rw, rd = box3d(b, 6, 78, 44, 24, 16, H("F2F4F6"), H("F7F8FA"))
    b.rect(6, ry + rd, 44, 2, H("D7DCE2"))
    windows(b, 9, ry + rd + 3, 38, 6, 9, 1, lit=0.4, seed=11, dark_col=H("5E86AE"))
    b.rect(24, 72, 10, 6, H("D7DCE2"))
    b.rect(25, 73, 8, 5, (150, 200, 235, 230))
    b.rect(22, 70, 14, 2, H("3FB8A0"))             # canopy
    # roof: red cross pad + gear
    b.rect(rx + 14, ry + 3, 16, 16, H("E9ECEF"))
    cross(b, rx + 22, ry + 11, 5, H("D93636"))
    ac_unit(b, rx + 3, ry + 3)
    ac_unit(b, rx + 35, ry + 5)
    skylight(b, rx + 35, ry + 13, 6, 4)
    cross(b, 14, ry + rd + 8, 2, H("D93636"), bg=H("FFFFFF")) if False else None
    # ambulance
    g.rect(48, 84, 12, 6, H("F4F6F8"))
    g.rect(48, 84, 12, 1, H("FFFFFF"))
    g.rect(55, 85, 3, 4, H("5A7E9E"))
    g.rect(49, 86, 4, 2, H("D93636"))
    g.rect(50, 85, 2, 4, H("D93636"))
    tree(g, 4, 92, 3, H("3F9B4A"))
    tree(b, 3, 40, 3, H("3F9B4A"))
    return finish(b, g)


def school():
    g, b = new_layers(64, 96)
    lawn(g, 0, 32, 64, 64, base=H("7CC25A"), seed=2)
    pad(g, 0, 70, 64, 26, H("C99F72"), edge=H("A67C52"))     # yard
    g.noise(0.05, seed=2, x=0, y=70, w=64, h=26)
    # court + playground
    g.rect_outline(36, 76, 22, 14, H("F4F4F4"))
    g.vline(47, 76, 89, H("F4F4F4"))
    g.circle(47, 83, 3, (255, 255, 255, 0))
    g.rect(5, 76, 14, 10, H("E2C48D"))
    g.rect(8, 79, 6, 2, H("D94F4F"))
    g.rect(9, 83, 8, 1, H("4C78C8"))
    fence(g, 0, 70, 64, 26, sides="tblr")
    shadow(g, 5, 66, 54, 24, 17)
    rx, ry, rw, rd = box3d(b, 5, 66, 54, 22, 16, H("B24A3C"), H("F0DEC0"))
    pitched(b, rx, ry, rw, rd, H("B24A3C"))
    b.rect(5, ry + rd, 54, 2, H("8E3A2E"))
    windows(b, 8, ry + rd + 3, 48, 5, 10, 1, lit=0.45, seed=5)
    b.rect(28, 60, 8, 6, H("8E3A2E"))
    b.rect(29, 61, 6, 5, (150, 200, 235, 230))
    # clock tower
    shadow(g, 27, 50, 10, 9, 18, alpha=60)
    box3d(b, 27, 50, 10, 9, 22, H("8E3A2E"), H("F0DEC0"))
    b.circle(32, 36, 3.2, H("FFFFFF"))
    b.set(32, 35, H("333333"))
    b.vline(32, 34, 36, H("333333"))
    b.hline(32, 34, 36, H("333333"))
    b.polygon([(26, 28), (32, 20), (38, 28)], H("8E3A2E"))
    tree(g, 4, 42, 4, H("3F9B4A"))
    tree(b, 58, 44, 5, H("3F9B4A"))
    # kids dots
    for (x, y, col) in ((22, 80, "F26B6B"), (26, 84, "4C78C8"), (52, 80, "F2C94C")):
        g.rect(x, y, 2, 2, H(col))
    return finish(b, g)


# =========================================================================== construction
def scaffold(b, x, y, w, h, t=0):
    """Steel scaffolding frame with planks (x, y = top-left, w x h)."""
    pole = H("D8A53A")
    for px in range(x, x + w + 1, max(4, w // 3)):
        b.rect(px, y, 1, h, pole)
    for py in range(y, y + h + 1, 6):
        b.rect(x, py, w + 1, 1, shade(pole, 0.8))
        b.rect(x, py + 1, w + 1, 1, (0, 0, 0, 40))
    for i in range(0, h - 6, 12):
        b.line(x, y + i + 6, x + w // 2, y + i, shade(pole, 0.7))
        b.line(x + w // 2, y + i + 6, x + w, y + i, shade(pole, 0.7))


def crane(b, bx, by, h, jib, t, big=False):
    """Tower crane: mast from (bx, by) upward, jib swinging with frame t."""
    col = H("F2C230")
    sw = 1 if not big else 2
    b.rect(bx, by - h, sw + 1, h, col)
    for yy in range(by - h, by, 4):
        b.hline(bx, bx + sw, yy, shade(col, 0.65))
    top = by - h
    sway = (0, 1, 2, 1)[t % 4]
    b.rect(bx - 3, top - 2, jib + 3, 2, col)               # jib
    b.rect(bx - 3, top - 2, jib + 3, 1, shade(col, 1.25))
    b.rect(bx - 6, top - 2, 4, 3, H("707880"))             # counterweight
    b.line(bx + 1, top - 6, bx + jib, top - 2, shade(col, 0.7))
    b.rect(bx, top - 6, 2, 4, col)
    hx = bx + jib - 4 + sway
    cab_y = top + 6 + (0, 2, 4, 2)[t % 4]
    b.vline(hx, top, cab_y, H("3C3C3C"))
    b.rect(hx - 1, cab_y, 3, 2, H("D94F4F"))
    b.rect(hx - 3, cab_y + 2, 7, 2, H("A0A0A0"))        # load


def construction_small(t):
    g, b = new_layers(32, 64)
    pad(g, 0, 33, 32, 31, H("B79F76"), edge=H("8E7650"))
    g.noise(0.08, seed=t + 3, x=0, y=33, w=32, h=31)
    for i in range(4):
        g.rect(2 + i * 8, 60, 4, 2, H("E8E8E8") if i % 2 else H("E04A2B"))   # barrier
    shadow(g, 6, 54, 20, 14, 18)
    # growing concrete core
    hh = 9 + (t % 2)
    box3d(b, 7, 54, 18, 12, hh, H("A8ADB4"), H("C4C8CE"))
    scaffold(b, 5, 54 - 12 - 12 + 1, 22, 24)
    # planks of material + stacked bricks
    g.rect(2, 36, 6, 3, H("C27B54"))
    g.rect(2, 36, 6, 1, H("DDA27C"))
    g.rect(24, 58, 6, 2, H("7B5B3A"))
    crane(b, 26, 48, 38, 24, t)
    return finish(b, g)


def construction_large(t):
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B79F76"), edge=H("8E7650"))
    g.noise(0.08, seed=t + 9, x=0, y=32, w=64, h=64)
    for i in range(8):
        g.rect(2 + i * 8, 92, 4, 2, H("E8E8E8") if i % 2 else H("E04A2B"))
    shadow(g, 8, 82, 40, 28, 28)
    box3d(b, 10, 82, 36, 24, 14 + (t % 2) * 2, H("A8ADB4"), H("C4C8CE"))
    scaffold(b, 8, 82 - 24 - 18, 42, 34)
    g.rect(52, 40, 8, 5, H("C27B54"))
    g.rect(52, 40, 8, 1, H("DDA27C"))
    g.ellipse(14, 90, 6, 3, H("D8C8A0"))   # sand pile
    crane(b, 54, 86, 66, 40, t, big=True)
    return finish(b, g)


# =========================================================================== roads
ASPH = H("4D5159")
SIDEW = H("C9CCD1")
CURB = H("9AA0A8")
LINE = H("F2D16B")


def road_frame(mask):
    c = Canvas(32, 32)
    N, E, S, W = mask & 1, mask & 2, mask & 4, mask & 8
    R = 4  # sidewalk width / fillet radius

    def asphalt(x, y):
        if R <= x < 32 - R and R <= y < 32 - R:
            return True
        if N and R <= x < 32 - R and y < R:
            return True
        if S and R <= x < 32 - R and y >= 32 - R:
            return True
        if W and R <= y < 32 - R and x < R:
            return True
        if E and R <= y < 32 - R and x >= 32 - R:
            return True
        # fillets: concave corner blocks between two adjacent arms stay sidewalk only
        # inside a quarter disc around the cell corner, otherwise asphalt
        for (a, b2, cx, cy) in ((N, E, 32, 0), (E, S, 32, 32), (S, W, 0, 32), (W, N, 0, 0)):
            if a and b2:
                inx = (x >= 32 - R) if cx == 32 else (x < R)
                iny = (y >= 32 - R) if cy == 32 else (y < R)
                if inx and iny:
                    return math.hypot(x + 0.5 - cx, y + 0.5 - cy) >= R
        # rounded outer corners of the central block on bends
        return False

    for y in range(32):
        for x in range(32):
            a = asphalt(x, y)
            n = ((x * 73856093) ^ (y * 19349663)) & 15
            if a:
                col = shade(ASPH, 0.97 + n * 0.004)
            else:
                col = SIDEW
                if ((x % 8) == 0 or (y % 8) == 0) and n > 5:
                    col = shade(SIDEW, 0.93)
            c.set(x, y, col)
    # curb: sidewalk pixels adjacent to asphalt
    cur = c.copy()
    for y in range(32):
        for x in range(32):
            if asphalt(x, y):
                continue
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if asphalt(x + ox, y + oy):
                    cur.set(x, y, CURB)
                    break
    c = cur
    # round outer asphalt corners on bends / dead ends (nibble the corner pixel)
    for (cx, cy) in ((R, R), (31 - R, R), (R, 31 - R), (31 - R, 31 - R)):
        sx = 1 if cx < 16 else -1
        sy = 1 if cy < 16 else -1
        # corner pixel is exposed when both neighbouring arms toward it are absent
        arm_h = (W if cx < 16 else E)
        arm_v = (N if cy < 16 else S)
        if not arm_h and not arm_v:
            c.set(cx, cy, CURB)
    # lane markings: dashed centre line along arms of 1 or 2-arm roads
    arms = [bool(N), bool(E), bool(S), bool(W)]
    cnt = sum(arms)
    if cnt in (1, 2):
        def dash_h(x0, x1):
            for x in range(x0, x1):
                if (x % 8) < 4:
                    c.set(x, 15, LINE)
                    c.set(x, 16, LINE)

        def dash_v(y0, y1):
            for y in range(y0, y1):
                if (y % 8) < 4:
                    c.set(15, y, LINE)
                    c.set(16, y, LINE)
        if cnt == 2 and N and S:
            dash_v(0, 32)
        elif cnt == 2 and E and W:
            dash_h(0, 32)
        else:
            if N:
                dash_v(0, 16)
            if S:
                dash_v(16, 32)
            if W:
                dash_h(0, 16)
            if E:
                dash_h(16, 32)
    return c


def roads():
    save_sheet(os.path.join(WORLD, "roads.png"), [road_frame(m) for m in range(16)], cols=4)


# =========================================================================== overlays
ZONE_COLS = ["7ED957", "2E9E3E", "5AB4F0", "2C6FD1", "F2C94C", "B07CE8"]


def overlays():
    zone = [Canvas(32, 32)]
    for hx in ZONE_COLS:
        col = H(hx)
        c = Canvas(32, 32, with_alpha(col, 84))
        for y in range(32):
            for x in range(32):
                if (x + y) % 8 == 0:
                    c.blend(x, y, with_alpha(col, 40))
        c.rect_outline(0, 0, 32, 32, with_alpha(col, 215))
        c.rect_outline(1, 1, 30, 30, with_alpha(shade(col, 1.2), 70))
        zone.append(c)
    save_sheet(os.path.join(WORLD, "overlay-zone.png"), zone)
    heat = []
    stops = [(0.0, H("E5483A")), (0.5, H("F2D04C")), (1.0, H("3DBE5A"))]
    for i in range(11):
        t = i / 10.0
        col = mix(stops[0][1], stops[1][1], t * 2) if t <= 0.5 else mix(stops[1][1], stops[2][1], (t - 0.5) * 2)
        heat.append(Canvas(32, 32, with_alpha(col, 120)))
    save_sheet(os.path.join(WORLD, "overlay-heat.png"), heat)
    for name, col in (("valid", H("3DDC6A")), ("invalid", H("EA4B4B"))):
        c = Canvas(32, 32, with_alpha(col, 100))
        c.rect_outline(0, 0, 32, 32, with_alpha(col, 230))
        c.rect_outline(1, 1, 30, 30, (255, 255, 255, 60))
        save_sheet(os.path.join(WORLD, f"overlay-{name}.png"), [c])
    g = Canvas(32, 32)
    g.rect_outline(0, 0, 32, 32, (255, 255, 255, 46))
    save_sheet(os.path.join(WORLD, "overlay-grid.png"), [g])


# =========================================================================== status icons
def badge(c, col):
    c.circle(8, 8, 7.8, (20, 26, 40, 235))
    c.circle(8, 8, 6.6, col)
    c.circle(8, 8, 5.4, shade(col, 0.55))


def status_frames():
    out = []
    # 0 no power: yellow bolt
    c = Canvas(16, 16)
    badge(c, H("F2C230"))
    c.polygon([(9, 2.5), (4.5, 9), (7.5, 9), (6.5, 13.5), (11.5, 6.5), (8.5, 6.5)], H("FFE066"))
    out.append(c)
    # 1 no water: drop
    c = Canvas(16, 16)
    badge(c, H("3C8DDE"))
    c.polygon([(8, 2.5), (11.5, 8), (4.5, 8)], H("9ED4FF"))
    c.circle(8, 9.4, 3.4, H("9ED4FF"))
    c.circle(7, 8.6, 1.1, (255, 255, 255, 200))
    out.append(c)
    # 2 no road: sign with road and slash
    c = Canvas(16, 16)
    badge(c, H("9AA0A8"))
    c.polygon([(5, 12.5), (7, 3.5), (9, 3.5), (11, 12.5)], H("5A6068"))
    for y in (5, 8, 11):
        c.vline(8, y, y + 1, H("F2D16B"))
    c.line(3, 3, 13, 13, H("E84545"), 2)
    out.append(c)
    # 3 abandoned: boarded
    c = Canvas(16, 16)
    badge(c, H("8B6A45"))
    c.line(3, 4, 13, 12, H("D9B47C"), 2)
    c.line(3, 12, 13, 4, H("C49A5E"), 2)
    for (x, y) in ((4, 5), (12, 11), (4, 11), (12, 5)):
        c.set(x, y, H("3A2A18"))
    out.append(c)
    # 4 unhappy: frown face
    c = Canvas(16, 16)
    badge(c, H("F0B429"))
    c.circle(8, 8, 5.2, H("FFD84D"))
    c.rect(5, 6, 2, 2, H("3A2A18"))
    c.rect(9, 6, 2, 2, H("3A2A18"))
    c.hline(6, 9, 11, H("3A2A18"))
    c.set(5, 12, H("3A2A18"))
    c.set(10, 12, H("3A2A18"))
    out.append(c)
    # 5 level up: arrow
    c = Canvas(16, 16)
    badge(c, H("3DBE5A"))
    c.polygon([(8, 2.8), (12.6, 8), (9.5, 8), (9.5, 13), (6.5, 13), (6.5, 8), (3.4, 8)], H("B8F5C4"))
    out.append(c)
    return out


def statusicons():
    save_sheet(os.path.join(WORLD, "statusicons.png"), status_frames())


# =========================================================================== vehicles
SS = 3  # supersampling factor for vehicle rendering


def downsample(c, f):
    """Box-filter downsample of an RGBA canvas by integer factor (premultiplied)."""
    n = Canvas(c.w // f, c.h // f)
    for y in range(n.h):
        for x in range(n.w):
            r = g = bl = a = 0
            for oy in range(f):
                for ox in range(f):
                    p = c.px[(y * f + oy) * c.w + x * f + ox]
                    r += p[0] * p[3]
                    g += p[1] * p[3]
                    bl += p[2] * p[3]
                    a += p[3]
            if a:
                n.px[y * n.w + x] = [int(r / a), int(g / a), int(bl / a), int(a / (f * f))]
    return n


class SSCanvas:
    """Draw vehicle parts in 1x units on a supersampled canvas."""

    def __init__(self, size=32):
        self.c = Canvas(size * SS, size * SS)

    def rect(self, x, y, w, h, col):
        self.c.rect(round(x * SS), round(y * SS), round(w * SS), round(h * SS), col)

    def rrect(self, x, y, w, h, r, col):
        x0, y0, x1, y1 = x * SS, y * SS, (x + w) * SS, (y + h) * SS
        rr = r * SS
        for yy in range(int(y0), int(y1)):
            for xx in range(int(x0), int(x1)):
                dx = max(x0 + rr - xx - 0.5, 0, xx + 0.5 - (x1 - rr))
                dy = max(y0 + rr - yy - 0.5, 0, yy + 0.5 - (y1 - rr))
                if dx * dx + dy * dy <= rr * rr:
                    self.c.blend(xx, yy, col)


def vehicle_base(kind, col):
    """North-facing vehicle on a 32x32 (1x) design grid, centred at 16,16."""
    s = SSCanvas()
    glass = H("27384C")
    if kind == "car":
        L, W = 18, 10
        x, y = 16 - W / 2, 16 - L / 2
        for wy in (y + 3, y + L - 6):                       # wheels peeking out
            s.rect(x - 0.6, wy, 1.4, 3, H("1A1D22"))
            s.rect(x + W - 0.8, wy, 1.4, 3, H("1A1D22"))
        s.rrect(x, y, W, L, 2.2, shade(col, 0.72))
        s.rrect(x + 0.3, y + 0.2, W - 1.2, L - 0.8, 2.0, col)
        s.rrect(x + 1.2, y + 4.2, W - 2.4, 3.0, 0.9, glass)            # windshield
        s.rect(x + 1.5, y + 4.4, 3.0, 0.7, (255, 255, 255, 90))
        s.rrect(x + 1.2, y + L - 5.0, W - 2.4, 2.0, 0.8, glass)       # rear window
        s.rrect(x + 1.0, y + 7.4, W - 2.0, 4.8, 1.0, shade(col, 1.18))  # roof
        s.rect(x + 1.3, y + 7.6, 3.0, 0.6, (255, 255, 255, 80))
        s.rect(x + 1.2, y + 0.5, 1.8, 0.9, H("FFF1B8"))                # headlights
        s.rect(x + W - 3.0, y + 0.5, 1.8, 0.9, H("FFF1B8"))
        s.rect(x + 1.2, y + L - 1.4, 1.8, 0.9, H("D83A3A"))            # tail lights
        s.rect(x + W - 3.0, y + L - 1.4, 1.8, 0.9, H("D83A3A"))
    elif kind == "truck":
        W, L = 10, 25
        x, y = 16 - W / 2, 16 - L / 2
        for wy in (y + 3, y + 15, y + 20):
            s.rect(x - 0.6, wy, 1.4, 3, H("1A1D22"))
            s.rect(x + W - 0.8, wy, 1.4, 3, H("1A1D22"))
        s.rrect(x, y, W, 8, 1.8, shade(col, 0.72))                 # cab
        s.rrect(x + 0.3, y + 0.2, W - 1.2, 7.4, 1.6, col)
        s.rrect(x + 1.2, y + 2.6, W - 2.4, 2.4, 0.8, glass)
        s.rect(x + 1.2, y + 0.4, 1.8, 0.8, H("FFF1B8"))
        s.rect(x + W - 3.0, y + 0.4, 1.8, 0.8, H("FFF1B8"))
        s.rect(x + 1, y + 8, W - 2, 1, H("3A3F48"))
        s.rect(x - 0.2, y + 9, W + 0.4, L - 9, H("B9C0C8"))        # cargo box
        s.rect(x - 0.2, y + 9, W + 0.4, 0.8, H("E6EAEE"))
        s.rect(x + W - 1.0, y + 9, 1.0, L - 9, H("8E96A0"))
        for i in range(0, L - 9, 3):
            s.rect(x - 0.2, y + 10.5 + i, W + 0.4, 0.5, H("9AA2AC"))
        s.rect(x + 1.0, y + L - 1.0, 2, 0.8, H("D83A3A"))
        s.rect(x + W - 3.0, y + L - 1.0, 2, 0.8, H("D83A3A"))
    else:  # bus
        W, L = 10, 28
        x, y = 16 - W / 2, 16 - L / 2
        for wy in (y + 4, y + L - 8):
            s.rect(x - 0.6, wy, 1.4, 3.4, H("1A1D22"))
            s.rect(x + W - 0.8, wy, 1.4, 3.4, H("1A1D22"))
        s.rrect(x, y, W, L, 2.0, shade(col, 0.7))
        s.rrect(x + 0.3, y + 0.2, W - 1.2, L - 0.8, 1.9, col)
        s.rrect(x + 1.0, y + 1.8, W - 2.0, 2.6, 0.8, glass)        # windshield
        s.rect(x + 1.3, y + 1.9, 3.0, 0.6, (255, 255, 255, 90))
        s.rect(x + 1.0, y + 5.4, W - 2.0, L - 8.2, shade(col, 1.2))  # roof
        for i in range(3):                                          # roof AC pods
            s.rect(x + 2.5, y + 7 + i * 6.5, W - 5.0, 3, H("E8EEF2"))
            s.rect(x + 2.5, y + 7 + i * 6.5, W - 5.0, 0.6, H("FFFFFF"))
        s.rect(x + 1.2, y + 0.4, 1.8, 0.8, H("FFF1B8"))
        s.rect(x + W - 3.0, y + 0.4, 1.8, 0.8, H("FFF1B8"))
        s.rrect(x + 1.0, y + L - 2.6, W - 2.0, 1.6, 0.6, glass)
        s.rect(x + 1.2, y + L - 1.0, 1.8, 0.7, H("D83A3A"))
        s.rect(x + W - 3.0, y + L - 1.0, 1.8, 0.7, H("D83A3A"))
        s.rect(x, y + 3, W, 0.5, (255, 255, 255, 60))
    return s.c


def rotate_ss(c, angle):
    return c.rotate(angle)


def vehicle_sheet(kind, col):
    base = vehicle_base(kind, col)
    frames = []
    for i in range(32):
        r = base.rotate(i * 360.0 / 32)
        f = downsample(r, SS)
        # shadow computed after rotation so it always falls toward the bottom-right
        sh = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                a = f.px[y * 32 + x][3]
                if a:
                    sh.blend(x + 1, y + 1, (10, 18, 30, int(a * 0.28)))
        sh.blit(f, 0, 0)
        frames.append(sh)
    return frames


VEHICLES = {"car-a": ("car", H("D8473A")), "car-b": ("car", H("3B73D1")), "car-c": ("car", H("E9ECEF")),
            "car-d": ("car", H("EDB92E")), "truck": ("truck", H("2E7D6B")), "bus": ("bus", H("2FA0B8"))}


def vehicles():
    for name, (kind, col) in VEHICLES.items():
        save_sheet(os.path.join(WORLD, f"{name}.png"), vehicle_sheet(kind, col), cols=8)


# =========================================================================== services + icons
def build_services():
    """name -> (list of frames, tick or None, footprint size)."""
    s = {}
    s["powerplant-coal"] = ([coal_frame(t) for t in range(4)], 160, 2)
    s["windturbine"] = (windturbine(8), 90, 1)
    s["solarplant"] = ([solarplant()], None, 2)
    s["watertower"] = (watertower(), None, 1)
    s["waterpump"] = (waterpump(), None, 1)
    s["police"] = ([police()], None, 2)
    s["firestation"] = ([firestation()], None, 2)
    s["clinic"] = ([clinic()], None, 2)
    s["school"] = ([school()], None, 2)
    s["park-small"] = (park_small(), None, 1)
    s["plaza"] = (plaza(), None, 1)
    s["park-large"] = (park_large(), None, 2)
    return s


ICON_TINT = {"powerplant-coal": "F2B84B", "windturbine": "F2B84B", "solarplant": "F2B84B",
             "watertower": "4FA3E8", "waterpump": "4FA3E8", "police": "4C7BE0", "firestation": "E0534A",
             "clinic": "3FC1A5", "school": "F29A4B", "park-small": "5DBB63", "plaza": "5DBB63", "park-large": "5DBB63"}


def resize_area(c, nw, nh):
    """Area-average resize (premultiplied) used for the build icons."""
    n = Canvas(nw, nh)
    sx, sy = c.w / nw, c.h / nh
    for y in range(nh):
        for x in range(nw):
            x0, x1 = x * sx, (x + 1) * sx
            y0, y1 = y * sy, (y + 1) * sy
            r = g = b = a = wsum = 0.0
            for yy in range(int(y0), min(c.h, int(math.ceil(y1)))):
                wy = min(y1, yy + 1) - max(y0, yy)
                for xx in range(int(x0), min(c.w, int(math.ceil(x1)))):
                    wx = min(x1, xx + 1) - max(x0, xx)
                    wgt = wx * wy
                    p = c.px[yy * c.w + xx]
                    pa = p[3] * wgt
                    r += p[0] * pa
                    g += p[1] * pa
                    b += p[2] * pa
                    a += pa
                    wsum += wgt
            if a > 0:
                n.px[y * nw + x] = [int(r / a), int(g / a), int(b / a), int(a / wsum)]
    return n


def bbox(c):
    xs, ys = [], []
    for y in range(c.h):
        for x in range(c.w):
            if c.px[y * c.w + x][3] > 20:
                xs.append(x)
                ys.append(y)
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def build_icon(name, frame, size):
    tint = H(ICON_TINT[name])
    ic = Canvas(48, 48)
    for y in range(48):
        for x in range(48):
            d = math.hypot(x - 24, y - 30) / 30.0
            ic.set(x, y, mix(shade(tint, 0.92), shade(tint, 0.55), min(1, d) * 0.9))
    # soft round-rect mask + frame
    mask = Canvas(48, 48)
    mask.fill((0, 0, 0, 0))
    out = Canvas(48, 48)
    for y in range(48):
        for x in range(48):
            dx = max(5 - x - 0.5, 0, x + 0.5 - 43)
            dy = max(5 - y - 0.5, 0, y + 0.5 - 43)
            if dx * dx + dy * dy <= 25:
                out.px[y * 48 + x] = ic.px[y * 48 + x][:]
    # light cast (top-left glow)
    for y in range(48):
        for x in range(48):
            if out.px[y * 48 + x][3]:
                t = max(0, 1 - (x + y) / 40.0)
                out.blend(x, y, (255, 255, 255, int(46 * t)))
    # ground ellipse
    out.ellipse(24, 40, 17, 4, (0, 0, 0, 55))
    x0, y0, x1, y1 = bbox(frame)
    crop = Canvas(x1 - x0, y1 - y0)
    for y in range(crop.h):
        for x in range(crop.w):
            crop.px[y * crop.w + x] = frame.px[(y + y0) * frame.w + x + x0][:]
    k = min(1.0, 38.0 / crop.w, 36.0 / crop.h)
    if k < 1.0:
        crop = resize_area(crop, max(1, round(crop.w * k)), max(1, round(crop.h * k)))
    ox = (48 - crop.w) // 2
    oy = 41 - crop.h
    out.blit(crop, ox, max(3, oy))
    # border: dark outer, light inner top-left
    for y in range(48):
        for x in range(48):
            if out.px[y * 48 + x][3] == 0:
                continue
            edge = False
            for ox2, oy2 in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if out.get(x + ox2, y + oy2)[3] == 0:
                    edge = True
            if edge:
                out.px[y * 48 + x] = [18, 24, 36, 255]
    for i in range(6, 42):
        out.blend(i, 1, (255, 255, 255, 120))
        out.blend(1, i, (255, 255, 255, 90))
    return out


def buildicons(services):
    regions = {}
    for name, (frames, tick, size) in services.items():
        regions[name] = build_icon(name, frames[0], size)
    placed = save_atlas(os.path.join(CHROME, "buildicons.png"), regions, width=256)
    with open(os.path.join(MOD, "chrome-buildings.yaml"), "w") as f:
        f.write("# Build-menu icons (48x48), one region per CityPlaceable actor. Generated by tools/genworld.py.\n")
        f.write("city-buildicons:\n\tImage: buildicons.png\n\tRegions:\n")
        for name in sorted(placed):
            x, y, w, h = placed[name]
            f.write(f"\t\t{name}: {x}, {y}, {w}, {h}\n")
    return regions


# =========================================================================== driver
GROW_FUNCS = {"res-low": res_low, "res-high": res_high, "com-low": com_low, "com-high": com_high,
              "ind": ind, "off": off}


def write_sequences(services):
    def w(name, text):
        with open(os.path.join(MOD, "sequences", name), "w") as f:
            f.write(text.rstrip() + "\n")

    t = "# Growable buildings. Frames: 32x64 (4 variants), footprint = bottom 32x32. Generated by tools/genworld.py.\n"
    t += "^Growable:\n\tDefaults:\n\t\tOffset: 0,-16\n\n"
    for z in GROW_FUNCS:
        for lvl in (1, 2, 3):
            t += f"{z}-{lvl}:\n\tInherits: ^Growable\n\tidle:\n\t\tFilename: {z}-{lvl}.png\n\t\tLength: *\n\n"
    t += ("construction:\n\tsmall:\n\t\tFilename: construction.png\n\t\tLength: *\n\t\tTick: 200\n\t\tOffset: 0,-16\n"
          "\tlarge:\n\t\tFilename: construction-2x2.png\n\t\tLength: *\n\t\tTick: 200\n\t\tOffset: 0,-16\n")
    w("buildings.yaml", t)

    t = "# Player-placed services. 1x1 frames 32x64, 2x2 frames 64x96 (footprint = bottom square). Generated by tools/genworld.py.\n"
    for name, (frames, tick, size) in services.items():
        t += f"{name}:\n\tidle:\n\t\tFilename: {name}.png\n\t\tLength: *\n"
        if tick:
            t += f"\t\tTick: {tick}\n"
        t += "\t\tOffset: 0,-16\n\n"
    w("services.yaml", t)

    t = "# Vehicles: 32 facings, frames clockwise from north (Facings: -32 because OpenRA facings run counter-clockwise). Generated by tools/genworld.py.\n"
    for name in VEHICLES:
        t += f"{name}:\n\tidle:\n\t\tFilename: {name}.png\n\t\tFacings: -32\n\t\tLength: 1\n\n"
    w("vehicles.yaml", t)

    t = "# Roads, overlays and status icons. Generated by tools/genworld.py.\n"
    t += "roads:\n\troad:\n\t\tFilename: roads.png\n\t\tLength: 16\n\n"
    t += ("overlays:\n\tzone:\n\t\tFilename: overlay-zone.png\n\t\tLength: 7\n"
          "\theat:\n\t\tFilename: overlay-heat.png\n\t\tLength: 11\n"
          "\tvalid:\n\t\tFilename: overlay-valid.png\n"
          "\tinvalid:\n\t\tFilename: overlay-invalid.png\n"
          "\tgrid:\n\t\tFilename: overlay-grid.png\n\n")
    t += "statusicons:\n\ticons:\n\t\tFilename: statusicons.png\n\t\tLength: 6\n"
    w("misc.yaml", t)


def contact_sheet(path, services, icons):
    """Scratch overview of all generated art at 2x (never committed)."""
    Z = 2
    W = 1700
    sheet = Canvas(W, 2100, (104, 148, 78, 255))
    st = {"x": 4, "y": 4, "rowh": 0}

    def put(c, gap=6):
        f = c.scale(Z)
        if st["x"] + f.w > W:
            st["x"] = 4
            st["y"] += st["rowh"] + 6
            st["rowh"] = 0
        sheet.blit(f, st["x"], st["y"])
        st["x"] += f.w + gap
        st["rowh"] = max(st["rowh"], f.h)

    def newrow():
        st["x"] = 4
        st["y"] += st["rowh"] + 10
        st["rowh"] = 0

    for z, fn in GROW_FUNCS.items():
        for lvl in (1, 2, 3):
            for v in range(4):
                put(fn(lvl, v), 2)
            st["x"] += 10
        newrow()
    for name, (frames, tick, size) in services.items():
        put(frames[0])
    newrow()
    for t in range(4):
        put(construction_small(t))
    for t in range(4):
        put(construction_large(t))
    newrow()
    for m in range(16):
        put(road_frame(m), 2)
    newrow()
    for name in VEHICLES:
        fr = vehicle_sheet(*VEHICLES[name])
        for i in range(0, 32, 2):
            put(fr[i], 0)
        newrow()
    for name, ic in icons.items():
        put(ic)
    newrow()
    for c in status_frames():
        put(c.scale(2), 4)
    newrow()
    sheet.save(path)


def main():
    os.makedirs(WORLD, exist_ok=True)
    os.makedirs(CHROME, exist_ok=True)
    for z, fn in GROW_FUNCS.items():
        for lvl in (1, 2, 3):
            save_sheet(os.path.join(WORLD, f"{z}-{lvl}.png"), [fn(lvl, v) for v in range(4)])
    save_sheet(os.path.join(WORLD, "construction.png"), [construction_small(t) for t in range(4)])
    save_sheet(os.path.join(WORLD, "construction-2x2.png"), [construction_large(t) for t in range(4)])
    services = build_services()
    for name, (frames, tick, size) in services.items():
        save_sheet(os.path.join(WORLD, f"{name}.png"), frames)
    roads()
    overlays()
    statusicons()
    vehicles()
    icons = buildicons(services)
    write_sequences(services)
    if "--sheet" in sys.argv:
        contact_sheet(sys.argv[sys.argv.index("--sheet") + 1], services, icons)
    print("genworld: done")


if __name__ == "__main__":
    main()
