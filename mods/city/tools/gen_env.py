#!/usr/bin/env python3
"""
gen_env.py - OpenCity environment art (work package ENV).

Writes bits/world/env/*.png:
  problem-tiles.png    8 severity tiles (20x20): minimal, info, problem, warning, major, error, fatal, good
  problem-glyphs.png   24 problem glyphs (16x16), frame = CityProblem - 1
  lights.png           additive night lights: window glows (frames 0..3), street lamp pool (4), headlight (5)
  snow.png             4 translucent snow cell variants (32x32)

    python3 mods/city/tools/gen_env.py [--sheet /tmp/env-contact.png]
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, shade, mix, hexcolor  # noqa: E402

MOD = os.path.dirname(HERE)
OUT = os.path.join(MOD, "bits", "world", "env")

INK = (28, 22, 34, 255)
WHITE = (255, 255, 255, 255)
CREAM = (255, 244, 214, 255)


# ----------------------------------------------------------------------------- severity tiles
TIERS = [
    ("minimal", (156, 160, 172)),
    ("info", (88, 168, 232)),
    ("problem", (242, 202, 64)),
    ("warning", (242, 142, 42)),
    ("major", (222, 62, 52)),
    ("error", (150, 30, 56)),
    ("fatal", (62, 30, 74)),
    ("good", (84, 190, 96)),
]


def tile(color):
    c = Canvas(20, 20)
    body = (1, 2, 18, 16)
    # drop shadow
    c.rect(2, 3, 18, 16, (0, 0, 0, 60))
    # rounded body
    for y in range(body[1], body[1] + body[3]):
        for x in range(body[0], body[0] + body[2]):
            cx = min(x - body[0], body[0] + body[2] - 1 - x)
            cy = min(y - body[1], body[1] + body[3] - 1 - y)
            if cx + cy < 1 and cx == 0 and cy == 0:
                continue
            t = (y - body[1]) / (body[3] - 1)
            c.set(x, y, mix(shade(color, 1.22), shade(color, 0.82), t))
    # pointer
    for i, w in enumerate((5, 3, 1)):
        for x in range(10 - w // 2, 10 + w // 2 + 1):
            c.set(x, 18 + i - 0, shade(color, 0.8))
    c.set(9, 18, shade(color, 0.8))
    # outline and bevel
    c.outline(INK)
    for x in range(3, 17):
        c.blend(x, 3, (255, 255, 255, 70))
    return c


# ----------------------------------------------------------------------------- glyphs (16x16, centre 8,8)
def disc(c, cx, cy, r, col):
    c.circle(cx, cy, r, col, aa=False)


def glyph_nopower(c):
    c.polygon([(9, 1), (3, 9), (7, 9), (6, 15), (13, 6), (9, 6), (11, 1)], (255, 230, 90, 255))


def drop(c, col, cy=10):
    c.polygon([(8, 1), (4, cy), (12, cy)], col)
    disc(c, 8, cy, 4, col)


def glyph_nowater(c):
    drop(c, (225, 245, 255, 255))
    c.line(3, 14, 13, 3, (222, 62, 52, 255), 2)


def glyph_nosewage(c):
    drop(c, (176, 128, 74, 255))
    c.line(6, 9, 10, 13, CREAM, 1)
    c.line(10, 9, 6, 13, CREAM, 1)


def glyph_dirtywater(c):
    drop(c, (150, 170, 80, 255))
    for x, y in ((6, 10), (9, 12), (10, 9)):
        c.set(x, y, (60, 50, 20, 255))


def glyph_noroad(c):
    c.polygon([(6, 3), (10, 3), (14, 14), (2, 14)], (230, 230, 236, 255))
    for y in (5, 8, 11):
        c.rect(7.5, y, 1, 2, (70, 70, 80, 255))
    c.line(2, 2, 14, 14, (222, 62, 52, 255), 2)


def glyph_abandoned(c):
    c.rect(2, 2, 12, 12, (150, 112, 70, 255))
    c.rect(4, 4, 8, 8, (50, 40, 44, 255))
    c.line(2, 4, 14, 12, (196, 150, 96, 255), 2)
    c.line(2, 12, 14, 4, (196, 150, 96, 255), 2)


def glyph_unhappy(c):
    disc(c, 8, 8, 7, (255, 220, 100, 255))
    c.rect(5, 5, 2, 3, INK)
    c.rect(10, 5, 2, 3, INK)
    c.line(5, 12, 7, 10, INK, 1)
    c.line(7, 10, 9, 10, INK, 1)
    c.line(9, 10, 11, 12, INK, 1)


def person(c, cx, col, s=1):
    disc(c, cx, 5, 2, col)
    c.polygon([(cx - 4, 14), (cx - 3, 8), (cx + 3, 8), (cx + 4, 14)], col)


def glyph_noworkers(c):
    person(c, 8, (232, 232, 240, 255))
    c.line(2, 14, 14, 2, (222, 62, 52, 255), 2)


def glyph_nocustomers(c):
    c.polygon([(2, 6), (14, 6), (12, 13), (4, 13)], (232, 232, 240, 255))
    c.line(5, 6, 7, 2, (232, 232, 240, 255), 1)
    c.line(11, 6, 9, 2, (232, 232, 240, 255), 1)
    c.line(2, 14, 14, 2, (222, 62, 52, 255), 2)


def glyph_nogoods(c):
    c.rect(2, 4, 12, 10, (190, 140, 84, 255))
    c.rect_outline(2, 4, 12, 10, (110, 76, 42, 255))
    c.rect(7, 4, 2, 10, (232, 206, 150, 255))
    c.rect(2, 8, 12, 1, (110, 76, 42, 255))


def glyph_garbage(c):
    c.polygon([(5, 4), (11, 4), (13, 9), (12, 14), (4, 14), (3, 9)], (96, 190, 110, 255))
    c.polygon([(6, 1), (10, 1), (9, 4), (7, 4)], (96, 190, 110, 255))
    c.line(6, 7, 7, 11, (40, 100, 56, 255), 1)
    c.line(10, 7, 9, 11, (40, 100, 56, 255), 1)


def glyph_fire(c):
    c.polygon([(8, 1), (11, 6), (13, 9), (12, 13), (8, 15), (4, 13), (3, 9), (5, 6), (6, 8), (8, 5)], (255, 150, 40, 255))
    c.polygon([(8, 7), (10, 10), (10, 13), (8, 14), (6, 13), (6, 10)], (255, 226, 110, 255))


def glyph_crime(c):
    c.ellipse(8, 8, 7, 4, (36, 36, 48, 255))
    c.rect(4, 6, 3, 2, WHITE)
    c.rect(9, 6, 3, 2, WHITE)
    c.rect(2, 2, 12, 2, (36, 36, 48, 255))


def glyph_sick(c):
    c.rect(6, 2, 4, 12, WHITE)
    c.rect(2, 6, 12, 4, WHITE)
    c.rect(7, 3, 2, 10, (222, 62, 52, 255))
    c.rect(3, 7, 10, 2, (222, 62, 52, 255))


def glyph_ambulance(c):
    c.rect(1, 5, 14, 7, WHITE)
    c.rect(1, 5, 5, 3, (170, 210, 240, 255))
    c.rect(8, 6, 2, 5, (222, 62, 52, 255))
    c.rect(7, 7, 4, 2, (222, 62, 52, 255))
    disc(c, 4, 13, 2, INK)
    disc(c, 12, 13, 2, INK)
    c.rect(6, 2, 4, 2, (255, 120, 100, 255))


def glyph_highrent(c):
    # dollar sign
    c.arc = None
    col = (255, 236, 150, 255)
    c.rect(7, 1, 2, 14, col)
    c.rect(5, 3, 6, 2, col)
    c.rect(4, 4, 2, 3, col)
    c.rect(5, 7, 6, 2, col)
    c.rect(10, 9, 2, 3, col)
    c.rect(5, 11, 6, 2, col)


def glyph_traffic(c):
    c.rect(2, 7, 12, 5, (232, 80, 70, 255))
    c.polygon([(4, 7), (6, 3), (10, 3), (12, 7)], (232, 80, 70, 255))
    c.rect(6, 4, 4, 3, (170, 214, 240, 255))
    disc(c, 4, 12, 2, INK)
    disc(c, 12, 12, 2, INK)
    c.set(2, 9, (255, 230, 120, 255))
    c.set(13, 9, (255, 230, 120, 255))


def glyph_air(c):
    col = (214, 218, 228, 255)
    disc(c, 5, 9, 3, col)
    disc(c, 9, 7, 4, col)
    disc(c, 12, 10, 3, col)
    c.rect(4, 10, 9, 3, col)
    c.rect(3, 14, 3, 1, (130, 134, 150, 255))
    c.rect(8, 14, 5, 1, (130, 134, 150, 255))


def glyph_ground(c):
    c.rect(3, 4, 10, 10, (130, 150, 70, 255))
    c.rect(3, 4, 10, 2, (176, 196, 96, 255))
    c.rect(3, 8, 10, 1, (70, 86, 36, 255))
    c.rect(3, 11, 10, 1, (70, 86, 36, 255))
    disc(c, 8, 1, 1, (176, 196, 96, 255))
    c.line(5, 1, 5, 2, (130, 150, 70, 255), 1)


def glyph_noise(c):
    c.polygon([(2, 6), (5, 6), (9, 3), (9, 13), (5, 10), (2, 10)], (232, 232, 240, 255))
    for r, a in ((3, 0), (5, 0), (7, 0)):
        for i in range(-r, r + 1):
            x = 9 + r + 1 - abs(i) // 3
            c.blend(x, 8 + i, (255, 255, 255, 220))


def glyph_levelup(c):
    c.polygon([(8, 1), (14, 8), (10, 8), (10, 14), (6, 14), (6, 8), (2, 8)], (232, 255, 232, 255))


def glyph_collapsed(c):
    c.rect(2, 6, 12, 8, (170, 150, 140, 255))
    c.polygon([(2, 6), (5, 3), (8, 6), (6, 9), (8, 14), (2, 14)], (120, 100, 96, 255))
    c.line(8, 3, 6, 8, INK, 1)
    c.line(6, 8, 9, 11, INK, 1)
    c.line(9, 11, 8, 14, INK, 1)


def glyph_noservice(c):
    c.rect(5, 2, 6, 2, WHITE)
    c.rect(9, 3, 2, 4, WHITE)
    c.rect(7, 6, 3, 2, WHITE)
    c.rect(7, 8, 2, 3, WHITE)
    c.rect(7, 13, 2, 2, WHITE)
    c.rect(5, 3, 2, 2, WHITE)


def glyph_flooded(c):
    col = (150, 210, 255, 255)
    for y in (4, 8, 12):
        for x in range(1, 15):
            yy = y + (1 if (x // 2) % 2 else 0)
            c.set(x, yy, col)
            c.set(x, yy + 1, shade(col, 0.75))
    c.rect(6, 1, 4, 3, (232, 232, 240, 255))


GLYPHS = [
    glyph_nopower, glyph_nowater, glyph_nosewage, glyph_dirtywater, glyph_noroad, glyph_abandoned, glyph_unhappy,
    glyph_noworkers, glyph_nocustomers, glyph_nogoods, glyph_garbage, glyph_fire, glyph_crime, glyph_sick,
    glyph_ambulance, glyph_highrent, glyph_traffic, glyph_air, glyph_ground, glyph_noise, glyph_levelup,
    glyph_collapsed, glyph_noservice, glyph_flooded,
]


def glyph_frame(fn):
    c = Canvas(16, 16)
    fn(c)
    c.outline((28, 22, 34, 235))
    return c


# ----------------------------------------------------------------------------- night lights
def glow(w, h, color, radius_x, radius_y, strength=1.0, cx=None, cy=None):
    """Soft radial glow with a hot core, RGBA with alpha used for additive blending."""
    c = Canvas(w, h)
    cx = w / 2.0 - 0.5 if cx is None else cx
    cy = h / 2.0 - 0.5 if cy is None else cy
    for y in range(h):
        for x in range(w):
            d = math.hypot((x - cx) / radius_x, (y - cy) / radius_y)
            if d >= 1:
                continue
            f = (1 - d) ** 1.5 * strength
            r, g, b, _ = color
            core = max(0.0, 1 - d * 2.2)
            r = min(255, r + int(core * (255 - r) * 0.8))
            g = min(255, g + int(core * (255 - g) * 0.8))
            b = min(255, b + int(core * (255 - b) * 0.8))
            c.set(x, y, (r, g, b, int(255 * min(1.0, f))))
    return c


def lights_frames():
    frames = []
    warm = (255, 196, 96, 255)
    # 0..3: lit windows glow of increasing size (tiny, small, medium, large buildings), 32x32 frames
    for rx, ry in ((6, 5), (8, 6), (11, 8), (14, 10)):
        frames.append(glow(32, 32, warm, rx, ry, 1.0))
    # 4: street-lamp light pool on the ground (isometric ellipse)
    frames.append(glow(32, 32, (255, 214, 130, 255), 15, 9, 0.85))
    # 5: headlight / generic small dot
    frames.append(glow(32, 32, (255, 240, 200, 255), 5, 4, 1.0))
    # 6: cool white blob (office / commercial)
    frames.append(glow(32, 32, (200, 226, 255, 255), 9, 7, 1.0))
    # 7: red-orange (industrial furnace)
    frames.append(glow(32, 32, (255, 120, 60, 255), 9, 7, 0.9))
    return frames


# ----------------------------------------------------------------------------- snow cell overlays
def snow_variants():
    out = []
    for v in range(4):
        r = random.Random("opencity-env-snow:%d" % v)
        c = Canvas(32, 32)
        # soft white-blue cover with slightly brighter drifts
        for y in range(32):
            for x in range(32):
                n = (math.sin((x + v * 7) * 0.45) + math.cos((y - v * 5) * 0.38) + math.sin((x + y) * 0.21 + v)) / 3.0
                a = 200 + int(n * 30) + r.randint(-6, 6)
                base = (232 + int(n * 10), 240 + int(n * 8), 250, max(120, min(255, a)))
                c.set(x, y, base)
        for _ in range(26):
            c.set(r.randrange(32), r.randrange(32), (255, 255, 255, 255))
        out.append(c)
    return out


def flood_frames():
    out = []
    for v in range(2):
        c = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                wave = math.sin((x + v * 8) * 0.5 + y * 0.35) * 0.5 + 0.5
                a = 150 + int(wave * 45)
                c.set(x, y, (70 + int(wave * 40), 140 + int(wave * 40), 210 + int(wave * 25), a))
        out.append(c)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    tiles = [tile(col) for _, col in TIERS]
    glyphs = [glyph_frame(fn) for fn in GLYPHS]
    lights = lights_frames()
    snow = snow_variants()
    save_sheet(os.path.join(OUT, "problem-tiles.png"), tiles)
    save_sheet(os.path.join(OUT, "problem-glyphs.png"), glyphs)
    save_sheet(os.path.join(OUT, "lights.png"), lights)
    save_sheet(os.path.join(OUT, "snow.png"), snow)
    save_sheet(os.path.join(OUT, "flood.png"), flood_frames())
    if "--sheet" in sys.argv:
        path = sys.argv[sys.argv.index("--sheet") + 1]
        scale = 4
        sheet = Canvas(24 * scale * 8 + 8, 24 * scale * 4 + 8, (70, 74, 84, 255))
        for i, t in enumerate(tiles):
            sheet.blit(t.scale(scale), 4 + i * 24 * scale, 4, blend=True)
        for i, g in enumerate(glyphs):
            tl = tiles[(i * 5) % 8].scale(scale)
            x = 4 + (i % 8) * 24 * scale
            y = 4 + (1 + i // 8) * 24 * scale
            sheet.blit(tl, x, y, blend=True)
            sheet.blit(g.scale(scale), x + 2 * scale, y + 1 * scale, blend=True)
        sheet.save(path)


if __name__ == "__main__":
    main()
