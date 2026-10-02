"""iso_life_mcursor - RCT2-style 32x32 cursors: chunky black outline, white/colour fill. Returns (img, hotspot)."""
import math

import numpy as np

from iso_life_common import canvas, blit
from iso_life_mdraw import art, col, line, outline_mask, px, rect, shade
import iso_life_mglyphs_a  # noqa: F401  (registers single-char colour keys)
import iso_life_mgeo as G

DARK = "#16161a"
P = {"k": "#16161a", "W": "#ffffff", "G": "#c8cad0", "g": "#8a8c94", "d": "#4a4d54", "y": "#f0c020",
     "Y": "#fff070", "o": "#e07418", "r": "#d83024", "R": "#8a1a1a", "b": "#2a62c8", "B": "#9ac4f8",
     "n": "#a06a38", "N": "#6a4222", "l": "#2f9a3c", "L": "#80d868"}


def finish(img):
    """Add the 1 px black outline (8-neighbour, chunky) around every opaque pixel, on a 32x32 canvas."""
    m = img[..., 3] > 0
    img[outline_mask(m, diag=True)] = col(DARK)
    return img


def put(base, art_img, x, y):
    blit(base, art_img, x, y)


def new():
    return canvas(32, 32)


def thick_line(img, p0, p1, c, w=2):
    for dx in range(w):
        for dy in range(w):
            line(img, p0[0] + dx, p0[1] + dy, p1[0] + dx, p1[1] + dy, c)


# ----------------------------------------------------------------------------- basic cursors
def pointer():
    im = new()
    a = art([
        "W",
        "WW",
        "WWW",
        "WWWW",
        "WWWWW",
        "WWWWWW",
        "WWWWWWW",
        "WWWWWWWW",
        "WWWWWWWWW",
        "WWWWWWWWWW",
        "WWWWWWWWWW",
        "WWWWWWW",
        "WWWGWWW",
        "WWG.WWW",
        "WG..GWWW",
        "G....WWW",
        ".....GWW",
        "......GG",
    ], P)
    put(im, a, 2, 2)
    return finish(im), (2, 2)


def hand_open():
    im = new()
    a = art([
        ".........WW.......",
        "......WW.WWW......",
        "......WW.WWW.WW...",
        "..WW..WW.WWW.WWW..",
        "..WW..WW.WWW.WWW..",
        "..WW..WWGWWWGWWWW.",
        "..WW..WWGWWWGWWWW.",
        "..WWW.WWWWWWWWWWW.",
        "..WWWWWWWWWWWWWWW.",
        "...WWWWWWWWWWWWWWG",
        ".WW.WWWWWWWWWWWWWG",
        ".WWWWWWWWWWWWWWWWG",
        "..WWWWWWWWWWWWWWG.",
        "...WWWWWWWWWWWWGG.",
        "....WWWWWWWWWWGG..",
        ".....WWWWWWWWGG...",
    ], P)
    put(im, a, 5, 8)
    return finish(im), (16, 16)


def hand_grab():
    im = new()
    a = art([
        "...WWWWWWWWW....",
        "..WWWWWWWWWWW...",
        ".WWkWWkWWkWWWW..",
        ".WWkWWkWWkWWWWW.",
        ".WWkWWkWWkWWWWW.",
        ".WWWWWWWWWWWWWW.",
        ".WWWWWWWWWWWWWG.",
        "..WWWWWWWWWWWWG.",
        "..WWWWWWWWWWWGG.",
        "...WWWWWWWWWGG..",
        "....WWWWWWWGG...",
    ], P)
    put(im, a, 8, 12)
    return finish(im), (16, 18)


def blocked_big():
    im = new()
    ys, xs = np.mgrid[0:32, 0:32]
    d = np.hypot(xs + 0.5 - 16, ys + 0.5 - 16)
    ring = (d <= 11.0) & (d >= 7.2)
    sl = (np.abs((xs + 0.5 - 16) + (ys + 0.5 - 16)) <= 1.9) & (d <= 11.0)
    im[ring | sl] = col("#e02a22")
    # highlight on the upper-left of the ring
    hi = ring & ((xs + ys) < 28) & (d >= 9.4)
    im[hi] = col("#ff7a6a")
    return finish(im), (16, 16)


def badge():
    """Small red circle-slash badge (13x13) used by blocked tool variants."""
    b = canvas(13, 13)
    ys, xs = np.mgrid[0:13, 0:13]
    d = np.hypot(xs + 0.5 - 6.5, ys + 0.5 - 6.5)
    ring = (d <= 6.2) & (d >= 3.8)
    sl = (np.abs((xs - 6.0) + (ys - 6.0)) <= 1.2) & (d <= 6.2)
    inner = (d < 3.8)
    b[inner] = col("#ffffff")
    b[ring | sl] = col("#e02a22")
    return b


def with_badge(img):
    out = img.copy()
    b = badge()
    bm = np.zeros((32, 32), bool)
    # clear a halo so the badge reads over the tool, then paint badge with black outline
    bb = canvas(15, 15)
    bb[1:-1, 1:-1] = b
    bb[outline_mask(bb[..., 3] > 0, diag=True)] = col(DARK)
    blit(out, bb, 17, 17)
    return out


# ----------------------------------------------------------------------------- tools (art in the top-left 22x22)
def hammer():
    im = new()
    thick_line(im, (6, 22), (16, 10), "n", 3)
    thick_line(im, (8, 23), (18, 11), "N", 1)
    head = art([
        "..GGGGGGGGG",
        ".GWWWWWWWGg",
        "GWWWWWWWWgg",
        "GWWWWWWWWgg",
        ".GGGGGGGGgg",
    ], P)
    # head tilted look: stack on top of handle end
    put(im, head, 10, 3)
    return finish(im), (15, 5)


def road_strip(img, x0, y0, x1, y1, half=3, base="d", centre="y", dash=True, rim="g"):
    n = x1 - x0
    for i in range(n + 1):
        x = x0 + i
        yc = y0 + (y1 - y0) * i / max(1, n)
        for dy in range(-half, half + 1):
            c = base
            if dy == -half:
                c = rim
            if dy == half:
                c = "k" if False else base
            px(img, x, int(round(yc)) + dy, c)
        if dash and (i // 3) % 2 == 0:
            px(img, x, int(round(yc)), centre)


def road():
    im = new()
    road_strip(im, 1, 19, 25, 7, half=3)
    return finish(im), (2, 19)


def zone_brush():
    im = new()
    roller = art([
        "GGGGGGGGGGGGg",
        "lllllyyyyybbb",
        "LLLLLYYYYYBBB",
        "lllllyyyyybbb",
        "GGGGGGGGGGGGg",
    ], P)
    put(im, roller, 3, 4)
    rect(im, 14, 9, 15, 11, "d")
    rect(im, 8, 11, 15, 12, "d")
    rect(im, 8, 11, 9, 14, "d")
    rect(im, 7, 15, 10, 23, "n")
    rect(im, 10, 15, 10, 23, "N")
    return finish(im), (9, 6)


def bulldozer():
    im = new()
    a = art([
        "........yyyyy.......",
        "........yBBByy......",
        "........yBBByy......",
        "GG......yyyyyyyyyy..",
        "GGG...yyyyyyyyyyyyy.",
        "GGG...yyyyyyyyyyyyy.",
        "GGG..dddddddddddddd.",
        "GGGG.dGdGdGdGdGdGdd.",
        "GGGGdddddddddddddddd",
        "GGG..dddddddddddddd.",
    ], P)
    put(im, a, 1, 7)
    return finish(im), (3, 15)


def inspect():
    im = new()
    thick_line(im, (13, 14), (21, 22), "n", 3)
    thick_line(im, (14, 15), (22, 23), "N", 1)
    ys, xs = np.mgrid[0:32, 0:32]
    d = np.hypot(xs + 0.5 - 10, ys + 0.5 - 10)
    lens = d <= 7.2
    ring = lens & (d >= 5.0)
    im[lens] = col("#cfe8ff")
    im[ring] = col("#c8cad0")
    hi = lens & ~ring & (xs < 9) & (ys < 9) & ((xs + ys) < 14)
    im[hi] = col("#ffffff")
    return finish(im), (10, 10)


def move():
    im = new()
    rect(im, 15, 8, 16, 23, "W")
    rect(im, 8, 15, 23, 16, "W")
    for tri in [[(16, 2), (20, 8), (12, 8)], [(16, 30), (20, 24), (12, 24)],
                [(2, 16), (8, 12), (8, 20)], [(30, 16), (24, 12), (24, 20)]]:
        mk = G.poly_mask(32, 32, [(x, y) for x, y in tri])
        im[mk] = col("#ffffff")
    return finish(im), (16, 16)


def place():
    im = new()
    pts = [(16, 5), (30, 12), (16, 19), (2, 12)]
    mk = G.poly_mask(32, 32, pts)
    inner = G.poly_mask(32, 32, [(16, 8), (25, 12), (16, 16), (7, 12)])
    im[mk] = col("#f0c020")
    im[inner] = col("#fff6b0")
    for k in range(-3, 4):
        px(im, 16 + k, 12, "d"); px(im, 16, 12 + k // 2 if abs(k) < 3 else 12, "d")
    px(im, 16, 10, "d"); px(im, 16, 14, "d"); px(im, 15, 12, "d"); px(im, 17, 12, "d")
    return finish(im), (16, 12)


def pipe():
    im = new()
    road_strip(im, 3, 20, 23, 10, half=2, base="b", centre="B", dash=False, rim="B")
    for (x, y) in [(3, 20), (23, 10)]:
        rect(im, x - 1, y - 4, x + 1, y + 4, "G")
    drop = art([
        "..bb..",
        ".bBbb.",
        "bBbbbb",
        "bbbbbb",
        ".bbbb.",
    ], P)
    put(im, drop, 12, 1)
    return finish(im), (4, 20)


def power():
    im = new()
    thick_line(im, (3, 22), (24, 11), "d", 2)
    bolt = art([
        "...yyy",
        "..yYy.",
        ".yYYy.",
        "yyYYYy",
        ".yyYy.",
        "..yYy.",
        "..yy..",
    ], P)
    put(im, bolt, 9, 1)
    for (x, y) in [(3, 20), (24, 9)]:
        rect(im, x, y, x + 1, y + 4, "G")
    return finish(im), (4, 22)


def transit():
    im = new()
    thick_line(im, (4, 20), (22, 10), "o", 3)
    thick_line(im, (4, 20), (22, 10), "y", 1)
    for (x, y) in [(5, 21), (23, 11)]:
        ys, xs = np.mgrid[0:32, 0:32]
        d = np.hypot(xs + 0.5 - x, ys + 0.5 - y)
        im[d <= 4.2] = col("#e07418")
        im[d <= 2.4] = col("#ffffff")
    return finish(im), (5, 21)


# ----------------------------------------------------------------------------- scroll arrows
DIRS = ["n", "ne", "e", "se", "s", "sw", "w", "nw"]


def scroll_arrow(d, blocked=False):
    ang = math.radians(DIRS.index(d) * 45)
    base = [(16, 3), (26, 15), (19.5, 15), (19.5, 28), (12.5, 28), (12.5, 15), (6, 15)]
    # centre the arrow body on (16, 16)
    pts = []
    for (x, y) in base:
        dx, dy = x - 16, y - 16
        rx = dx * math.cos(ang) - dy * math.sin(ang)
        ry = dx * math.sin(ang) + dy * math.cos(ang)
        pts.append((16 + rx, 16 + ry))
    im = new()
    mk = G.poly_mask(32, 32, pts)
    fillc = "#f04030" if blocked else "#ffffff"
    im[mk] = col(fillc)
    # shade: darker lower-right half
    ys, xs = np.mgrid[0:32, 0:32]
    sh = mk & ((xs + ys) > 33)
    im[sh] = col("#b82a20" if blocked else "#c8cad0")
    return finish(im), (16, 16)
