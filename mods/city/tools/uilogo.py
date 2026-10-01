"""OpenCity logo (own stroke font, no font files needed), loadscreen atlas and mod icons."""

import math
import os
import random
from uidraw import G, pad_pot
from pngkit import Canvas, mix
from uitheme import *  # noqa: F401,F403
from uistyle import lerpc, metal_tint, stylize, metal_frame, fbm


# ---- tiny stroke font ------------------------------------------------------------
# glyphs are described in a box: x 0..w, baseline at y=0 going up is negative; x-height 10, cap 14.

def glyph(g, ch, x, base, k, sw, c):
    """Draw one letter with its left edge at x; returns the advance width."""
    def P(px, py):
        return (x + px * k, base + py * k)

    def ln(a, b):
        (x0, y0), (x1, y1) = P(*a), P(*b)
        g.line(x0, y0, x1, y1, sw, c)

    def ar(cx, cy, r, a0, a1):
        px, py = P(cx, cy)
        g.arc(px, py, r * k, a0, a1, sw, c)

    if ch == 'O':
        ar(7, -7, 7, 0, 360)
        return 15
    if ch == 'C':
        ar(7, -7, 7, 38, 322)
        return 14
    if ch == 'p':
        ln((1, -10), (1, 4))
        ar(6, -5, 5, 0, 360)
        return 12
    if ch == 'e':
        ln((1.2, -5), (10.8, -5))
        ar(6, -5, 5.2, 0, -320)
        return 12
    if ch == 'n':
        ln((1, -10), (1, 0))
        ar(6, -5, 5, 180, 360)
        ln((11, -5), (11, 0))
        return 12
    if ch == 'i':
        ln((1, -10), (1, 0))
        p = P(1, -14.6)
        g.circle(p[0], p[1], sw * 0.62, c)
        return 3
    if ch == 't':
        ln((3, -14), (3, -2.5))
        ar(6.2, -2.7, 3.2, 90, 180)
        ln((0, -10), (7.5, -10))
        return 8
    if ch == 'y':
        ln((0.5, -10), (6, 0))
        ln((11.5, -10), (4, 4))
        return 12
    return 8


def word(g, text, x, base, k, sw, colors, gap=3.2):
    """colors: per-letter list or single colour."""
    for i, ch in enumerate(text):
        c = colors[i] if isinstance(colors, list) else colors
        x += glyph(g, ch, x, base, k, sw, c) * k + gap * k
    return x


def word_width(text, k, gap=3.2):
    g = G(1, 1, 1)
    w = 0
    for ch in text:
        w += glyph(g, ch, 0, 0, 1, 0.1, (0, 0, 0, 0)) + gap
    return (w - gap) * k




# ---- skyline emblem (Dune 2000 palette: dusk desert sky, dark silhouettes, bronze rim) ----------
BUILDINGS = [  # x, w, h, kind (0 back / 1 mid / 2 front)
    (-48, 14, 36, 0), (-34, 12, 52, 0), (22, 14, 46, 0), (35, 12, 34, 0),
    (-38, 18, 26, 1), (-14, 16, 64, 1), (6, 18, 78, 1), (26, 18, 42, 1),
    (-22, 18, 34, 2), (-2, 10, 50, 2), (12, 20, 30, 2), (-34, 12, 20, 2), (30, 14, 24, 2),
]
SKY = [(0.0, (22, 8, 10)), (0.45, (96, 30, 18)), (0.75, (214, 104, 40)), (1.0, (255, 190, 90))]


def sky(t):
    t = max(0.0, min(1.0, t))
    for (t0, c0), (t1, c1) in zip(SKY, SKY[1:]):
        if t <= t1:
            return lerpc(c0, c1, (t - t0) / (t1 - t0)) + (255,)
    return SKY[-1][1] + (255,)


def emblem(g, cx, cy, r):
    ground = cy + 38
    g.circle(cx, cy, r + 8, fill=(0, 0, 0, 90))
    g.circle(cx, cy, r + 7, fill=OUTLINE)
    rim = metal_frame(2 * r + 14, 2 * r + 14)
    g.circle(cx, cy, r + 6, fill=lambda x, y: rim(x - cx + r + 7, y - cy + r + 7))
    g.ring(cx, cy, r + 1.5, 1.2, (60, 26, 8, 255))
    g.circle(cx, cy, r, fill=lambda x, y: sky((y - (cy - r)) / (ground - (cy - r) + 6.0)))
    # sun and moon
    g.circle(cx + 30, ground - 26, 20, fill=(255, 200, 100, 40))
    g.circle(cx + 30, ground - 26, 12, fill=(255, 226, 150, 255))
    g.circle(cx - 40, cy - 40, 5, fill=(238, 222, 196, 255))
    g.circle(cx - 38, cy - 41, 4.4, fill=(26, 10, 10, 120))
    cols = {0: (104, 44, 26, 255), 1: (60, 26, 16, 255), 2: (30, 12, 8, 255)}
    for bx, bw, bh, kind in BUILDINGS:
        x0 = cx + bx
        g.rect(x0, ground - bh, bw, bh, cols[kind])
        g.rect(x0, ground - bh, bw, 1.2, (150, 84, 44, 255) if kind < 2 else (92, 44, 24, 255))
        if kind == 1 and bh > 60:
            g.rect(x0 + bw / 2 - 1, ground - bh - 9, 2, 9, cols[kind])
        ny = int(bh // 9)
        for iy in range(ny):
            for ix in range(max(1, int(bw // 6))):
                if (ix * 3 + iy * 5 + bx) % 4 != 0:
                    lit = (ix + iy * 2 + bx) % 3 != 0
                    g.rect(x0 + 2 + ix * 5.2, ground - bh + 4 + iy * 8.2, 2.6, 3.6,
                           (255, 196, 86, 255) if lit and kind > 0 else (30, 14, 8, 255) if kind == 0 else (74, 36, 18, 255))
    a0 = math.degrees(math.asin((ground - cy) / r))
    seg = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a)))
           for a in [a0 + (180 - 2 * a0) * i / 24.0 for i in range(25)]]
    g.poly(seg, (18, 8, 5, 255))
    for i in range(-2, 3):
        g.rect(cx + i * 17 - 5, ground + 9, 10, 2.4, (232, 156, 56, 255))
    g.ring(cx, cy, r - 0.6, 1.2, (255, 220, 150, 70))


def draw_logo(g):
    emblem(g, 128, 82, 66)
    # metallic gold wordmark: drawn white, tinted with a vertical gold gradient, then bevelled + outlined
    wsz = word_width('OpenCity', 2.08)
    wg = G(256, 80, g.s)
    word(wg, 'OpenCity', 128 - wsz / 2, 60, 2.08, 4.6, WHITE)
    metal_tint(wg, (255, 236, 176), (206, 120, 40))
    stylize(wg, outline=1.5, bevel=1.8, strength=1.1, vgrad=(1.0, 1.0))
    g.blit(wg, 0, 160)
    g.rrect(60, 241, 136, 2, 1, fill=(206, 120, 40, 200))


# ---- stripe: dark steel band with orange edges (like the D2k loadscreen) ---------------------------
def draw_stripe(g):
    s = g.s
    y0, y1 = 100, 156
    for y in range(int(y0 * s), int(y1 * s)):
        t = (y - y0 * s) / ((y1 - y0) * s)
        base = lerpc((50, 60, 58), (28, 34, 34), t)
        for x in range(g.cv.w):
            n = (fbm(x / s * 0.5, y / s * 0.5, 4, 2) - 0.5) * 14
            g.cv.set(x, y, (int(base[0] + n), int(base[1] + n), int(base[2] + n), 255))
    for yy, c in ((y0, (190, 108, 44, 255)), (y0 + 1, (122, 66, 26, 255)), (y1 - 2, (122, 66, 26, 255)), (y1 - 1, (190, 108, 44, 255))):
        g.rect(0, yy, 253, 1, c)


def build(scales, mod):
    """Window icons only: the logo and load screen are drawn at runtime (OpenRA.Mods.City/UIArt, a port of draw_logo)."""
    for s in scales:
        suf = '' if s == 1 else '-%dx' % s
        ic = G(32, 32, s)
        draw_icon(ic)
        ic.cv.save(os.path.join(mod, 'icon%s.png' % suf))


def draw_icon(g):
    fr = metal_frame(32, 32)
    g.rrect(0, 0, 32, 32, 6, fill=OUTLINE)
    g.rrect(0.6, 0.6, 30.8, 30.8, 5.4, fill=lambda x, y: sky((y - 3) / 24.0), stroke=fr, sw=2.2)
    g.circle(23, 20, 4.6, fill=(255, 226, 150, 255))
    for x0, w, h, c in ((5, 5, 11, (104, 44, 26, 255)), (21, 6, 9, (104, 44, 26, 255)),
                        (9.5, 6.5, 18, (46, 20, 12, 255)), (16, 6.5, 13, (66, 28, 16, 255))):
        g.rect(x0, 26 - h, w, h, c)
    for wx, wy in ((11, 10), (13.4, 10), (11, 14), (13.4, 14), (11, 18), (13.4, 18), (18, 16), (20, 16), (18, 20), (20, 20)):
        g.rect(wx, wy, 1.4, 1.8, (255, 196, 86, 255))
    g.rect(3, 26, 26, 3, (18, 8, 5, 255))
    for x in (6, 14, 22):
        g.rect(x, 27.1, 4, 0.9, (232, 156, 56, 255))
