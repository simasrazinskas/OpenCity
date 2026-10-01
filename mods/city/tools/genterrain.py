#!/usr/bin/env python3
"""
genterrain.py - procedural TEMPERATE tileset for OpenCity (python3 stdlib + pngkit only).

Writes  bits/terrain/terrain.png  (one sheet of 32x32 frames) and  tilesets/temperate.yaml.
Also exposes the template-id scheme used by genmap.py (see ids below).

Template ids
  1..6     grass variants (1 = plain)            Clear
  10,11,18,19  meadow (flowers)                  Clear
  12..14,20..22 rough / stony grass, shrubs      Rough
  15,16    dirt                                  Rough
  17       sand                                  Clear
  100..103 shallow water, 104,105 mid, 106,107 deep           Water
  200 + SHORE_INDEX[(mask, corners)]  land "beach" tiles (grass that meets water)   Clear
  400 + SHORE_INDEX[(mask, corners)]  water "shore" tiles (sand beach in the water) Water
mask bits: 1=N 2=E 4=S 8=W  (neighbour of the OTHER kind, i.e. water for beach tiles, land for water tiles)
corners bits: 1=NE 2=SE 4=SW 8=NW (diagonal-only neighbours of the other kind)
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngkit import Canvas, mix, save_sheet, hexcolor  # noqa: E402

MOD = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
T = 32
TAU = 2 * math.pi

GRASS = hexcolor("729f50")
GRASS_DARK = hexcolor("66924a")
GRASS_LIGHT = hexcolor("7dab58")
MEADOW = hexcolor("82b650")
ROUGH = hexcolor("72a24d")
DIRT = hexcolor("a08460")
SAND = hexcolor("ecd9a0")
SAND_DRY = hexcolor("f3e4b4")
SAND_WET = hexcolor("cdb77d")
W_SHALLOW = hexcolor("5fb6d4")
W_MID = hexcolor("54a8cf")
W_DEEP = hexcolor("4392c1")
FOAM = hexcolor("f4fbff")


def hash01(x, y, seed):
    n = (x * 374761393 + y * 668265263 + seed * 2147483647) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    n ^= n >> 16
    return (n & 0xffff) / 65535.0


class PeriodicNoise:
    """Tileable value noise, period T pixels. Smooth bicubic-ish interpolation of a coarse lattice."""

    def __init__(self, seed, cells=4):
        r = random.Random(seed)
        self.n = cells
        self.g = [[r.random() for _ in range(cells)] for _ in range(cells)]

    def at(self, x, y):
        n = self.n
        fx, fy = (x % T) / T * n, (y % T) / T * n
        x0, y0 = int(fx), int(fy)
        tx, ty = fx - x0, fy - y0
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        g = self.g
        a = g[y0 % n][x0 % n] * (1 - tx) + g[y0 % n][(x0 + 1) % n] * tx
        b = g[(y0 + 1) % n][x0 % n] * (1 - tx) + g[(y0 + 1) % n][(x0 + 1) % n] * tx
        return a * (1 - ty) + b * ty


def fbm(seed):
    n1, n2 = PeriodicNoise(seed, 2), PeriodicNoise(seed + 7, 4)
    return lambda x, y: 0.65 * n1.at(x, y) + 0.35 * n2.at(x, y)


def tone(c, f):
    return (max(0, min(255, int(c[0] * f))), max(0, min(255, int(c[1] * f))),
            max(0, min(255, int(c[2] * f))), 255)


def interior(x, y, m=3):
    return m <= x < T - m and m <= y < T - m


def base_tile(color, seed, amp=0.07, grain=0.025, dark=None, light=None):
    """Soft tone + fine texture. Low-frequency variation fades out towards the tile edges so
    neighbouring tiles of any variant meet on (nearly) the same colour: no visible grid."""
    c = Canvas(T, T)
    f = fbm(seed)
    fine = PeriodicNoise(seed + 99, 8)
    for y in range(T):
        for x in range(T):
            win = math.sin(math.pi * (x + 0.5) / T) * math.sin(math.pi * (y + 0.5) / T)
            win = 0.25 + 0.75 * win
            v = 0.5 + (f(x, y) - 0.5) * win
            col = color
            if dark is not None and v < 0.47:
                col = mix(color, dark, (0.47 - v) * 1.6)
            elif light is not None and v > 0.53:
                col = mix(color, light, (v - 0.53) * 1.6)
            k = 1 + (v - 0.5) * amp + (fine.at(x, y) - 0.5) * 0.045 + (hash01(x, y, seed) - 0.5) * grain * 2
            c.set(x, y, tone(col, k))
    return c


def stamp_tufts(c, rnd, n, col_dark, col_light):
    for _ in range(n):
        x, y = rnd.randint(3, T - 5), rnd.randint(4, T - 4)
        c.blend(x, y, (*col_dark[:3], 150))
        c.blend(x, y - 1, (*col_light[:3], 170))
        c.blend(x + 1, y - 2, (*col_light[:3], 140))
        c.blend(x - 1, y - 2, (*col_light[:3], 140))


def stamp_flower(c, x, y, col, centre=(255, 226, 120)):
    c.blend(x, y, col)
    c.blend(x - 1, y, (*col[:3], 190))
    c.blend(x + 1, y, (*col[:3], 190))
    c.blend(x, y - 1, (*col[:3], 190))
    c.blend(x, y + 1, (*col[:3], 190))
    c.set(x, y, centre)


def stamp_clover(c, x, y):
    cl = (74, 128, 62, 255)
    for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        c.blend(x + dx - 1, y + dy - 1, cl)
    c.blend(x, y - 2, (96, 150, 76, 200))


FLOWERS = [(255, 255, 255), (255, 214, 235), (255, 236, 130), (200, 180, 255)]


def grass_tile(i):
    rnd = random.Random(1000 + i)
    c = base_tile(GRASS, 40 + i, amp=0.05, dark=GRASS_DARK, light=GRASS_LIGHT)
    if i > 0:
        stamp_tufts(c, rnd, 3 + i, GRASS_DARK, GRASS_LIGHT)
    if i in (2, 4):
        for _ in range(2):
            x, y = rnd.randint(4, T - 5), rnd.randint(4, T - 5)
            stamp_clover(c, x, y)
    if i in (3, 5):
        for _ in range(2):
            stamp_flower(c, rnd.randint(4, T - 5), rnd.randint(4, T - 5), rnd.choice(FLOWERS))
    return c


def meadow_tile(i):
    """Grass-coloured base (no visible tile boundary) sprinkled with flowers."""
    rnd = random.Random(2000 + i)
    c = base_tile(GRASS, 40 + i, amp=0.05, dark=GRASS_DARK, light=GRASS_LIGHT)
    stamp_tufts(c, rnd, 7, GRASS_DARK, hexcolor("8fbb62"))
    for _ in range(6 + 3 * i):
        x, y = rnd.randint(3, T - 4), rnd.randint(3, T - 4)
        stamp_flower(c, x, y, rnd.choice(FLOWERS + [(255, 170, 120)]))
    return c


def rough_tile(i):
    """Grass base with pebbles, dark tufts and small shrubs: reads as rough, no hard edge."""
    rnd = random.Random(3000 + i)
    c = base_tile(GRASS, 120 + i, amp=0.05, dark=GRASS_DARK, light=GRASS_LIGHT)
    stamp_tufts(c, rnd, 7, hexcolor("4f7a3c"), hexcolor("8ab35e"))
    for _ in range(2 + i % 3):
        x, y = rnd.randint(5, T - 7), rnd.randint(5, T - 7)
        c.ellipse(x + 1, y + 1.5, 3.2, 1.8, (30, 60, 30, 60))
        c.ellipse(x, y, 3, 2.3, hexcolor("5b8c40"))
        c.ellipse(x - 0.7, y - 0.7, 1.8, 1.2, hexcolor("79a653"))
    for _ in range(3 + i):
        x, y = rnd.randint(3, T - 6), rnd.randint(3, T - 6)
        r = rnd.choice((1, 1, 2))
        c.ellipse(x + 1, y + 2, r + 1, r * 0.6 + 0.8, (40, 60, 30, 70))
        c.ellipse(x, y, r + 0.5, r * 0.8 + 0.5, hexcolor("a3a69a"))
        c.blend(x - 1, y - 1, (230, 232, 220, 190))
    return c


def dirt_tile(i):
    """Grass base with soft bare-earth patches inside the tile (edges stay grass)."""
    rnd = random.Random(4000 + i)
    c = base_tile(GRASS, 150 + i, amp=0.05, dark=GRASS_DARK, light=GRASS_LIGHT)
    for _ in range(3):
        x, y = rnd.randint(9, T - 10), rnd.randint(9, T - 10)
        rx, ry = rnd.uniform(3, 6), rnd.uniform(2.5, 4.5)
        for k in range(4):
            s_ = 1 - k * 0.2
            c.ellipse(x, y, rx * s_, ry * s_, (*mix(hexcolor("8e8a55"), DIRT, k / 3.0)[:3], 110 if k < 3 else 200))
    for _ in range(4):
        x, y = rnd.randint(6, T - 7), rnd.randint(6, T - 7)
        c.ellipse(x, y, 1.4, 1.0, hexcolor("c3b79f"))
    return c


def sand_tile():
    c = base_tile(SAND, 170, amp=0.04, grain=0.03, dark=SAND_WET, light=SAND_DRY)
    return c


def water_base(color, seed, ripples, phase_seed):
    c = base_tile(color, seed, amp=0.03, grain=0.012)
    return c


def water_tile(color, seed, variant, depth):
    """Shared smooth base per depth + interior ripples/sparkles per variant."""
    c = water_base(color, 200 + depth, 0, 0)
    rnd = random.Random(seed * 31 + variant)
    n = 5 if depth == 0 else (4 if depth == 1 else 3)
    for _ in range(n + variant % 3):
        x, y = rnd.randint(5, T - 11), rnd.randint(5, T - 6)
        w = rnd.randint(4, 8)
        a = 90 if depth == 0 else 65
        for k in range(w):
            yy = y + int(round(math.sin(k / w * math.pi * 1.5) * 1.2))
            fade = math.sin(k / max(1, w - 1) * math.pi)
            c.blend(x + k, yy, (255, 255, 255, int(a * fade)))
            c.blend(x + k, yy + 1, (20, 70, 120, int(a * 0.45 * fade)))
    if depth == 0:
        for _ in range(2):
            x, y = rnd.randint(4, T - 5), rnd.randint(4, T - 5)
            c.blend(x, y, (255, 255, 255, 140))
    return c


# ---- shoreline tiles -----------------------------------------------------------------------
SIDE_BITS = (1, 2, 4, 8)  # N E S W
CORNER_SIDES = {1: (1, 2), 2: (2, 4), 4: (4, 8), 8: (8, 1)}  # NE SE SW NW


def shore_combos():
    out = []
    for mask in range(16):
        free = [c for c, (a, b) in CORNER_SIDES.items() if not (mask & a) and not (mask & b)]
        for k in range(1 << len(free)):
            corners = 0
            for j, c in enumerate(free):
                if k & (1 << j):
                    corners |= c
            if mask or corners:
                out.append((mask, corners))
    return out


SHORE_COMBOS = shore_combos()
SHORE_INDEX = {k: i for i, k in enumerate(SHORE_COMBOS)}
LAND_BASE, WATER_BASE = 200, 400


def edge_depth(t, d0, a):
    """Wobbly band depth along a side; exactly d0 at t=0 and t=T so neighbouring tiles join up."""
    return d0 + a * math.sin(TAU * t / T) + a * 0.6 * math.sin(2 * TAU * t / T) + a * 0.4 * math.sin(3 * TAU * t / T)


def feature_e(x, y, mask, corners, d0, a):
    """> 0 inside the feature (sand), distance to its boundary in px."""
    px, py = x + 0.5, y + 0.5
    best = -99.0
    if mask & 1:
        best = max(best, edge_depth(px, d0, a) - py)
    if mask & 2:
        best = max(best, edge_depth(py, d0, a) - (T - px))
    if mask & 4:
        best = max(best, edge_depth(px, d0, a) - (T - py))
    if mask & 8:
        best = max(best, edge_depth(py, d0, a) - px)
    cx = {1: T, 2: T, 4: 0, 8: 0}
    cy = {1: 0, 2: T, 4: T, 8: 0}
    for c in (1, 2, 4, 8):
        if corners & c:
            best = max(best, d0 - math.hypot(px - cx[c], py - cy[c]))
    return best


LAND_D0, LAND_A = 9.0, 1.6
WATER_D0, WATER_A = 13.5, 2.2


def land_shore_tile(mask, corners, seed):
    c = grass_tile(0)
    wob = PeriodicNoise(seed, 8)
    for y in range(T):
        for x in range(T):
            e = feature_e(x, y, mask, corners, LAND_D0, LAND_A) + (wob.at(x, y) - 0.5) * 1.6
            if e <= -2:
                continue
            g = (hash01(x, y, seed) - 0.5) * 0.05
            sand = tone(mix(SAND, SAND_DRY, min(1.0, max(0.0, e / 5.0))), 1 + g)
            if e > 0.6:
                c.set(x, y, sand)
            else:  # ragged grass/sand transition
                t = (e + 2) / 2.6
                if hash01(x, y, seed + 5) < t:
                    c.set(x, y, tone(mix(SAND, GRASS_DARK, 0.25), 1 + g))
    return c


def water_shore_tile(mask, corners, seed):
    c = water_base(W_SHALLOW, 200, 0, 0)
    wob = PeriodicNoise(seed, 8)
    for y in range(T):
        for x in range(T):
            e = feature_e(x, y, mask, corners, WATER_D0, WATER_A) + (wob.at(x, y) - 0.5) * 1.4
            g = (hash01(x, y, seed) - 0.5) * 0.05
            if e > 0:
                if e > 5.5:
                    col = mix(SAND, SAND_DRY, min(1.0, (e - 5.5) / 6))
                elif e > 1.8:
                    col = mix(SAND_WET, SAND, (e - 1.8) / 3.7)
                else:
                    col = mix(hexcolor("b49e66"), SAND_WET, e / 1.8)
                c.set(x, y, tone(col, 1 + g))
            elif e > -2.6:  # foam line
                a = (e + 2.6) / 2.6
                p = c.get(x, y)
                col = mix(p, FOAM, 0.15 + 0.7 * a * a)
                c.set(x, y, tone(col, 1 + g))
            elif e > -8:  # light turquoise shallows
                a = (e + 8) / 5.4
                p = c.get(x, y)
                c.set(x, y, mix(p, hexcolor("9ad9e2"), 0.38 * a))
    return c


def build_frames():
    frames, ids, kinds = [], [], []

    def add(tid, canvas, ttype):
        ids.append(tid)
        frames.append(canvas)
        kinds.append(ttype)

    for i in range(6):
        add(1 + i, grass_tile(i), "Clear")
    for i in range(2):
        add(10 + i, meadow_tile(i), "Clear")
    for i in range(2):
        add(18 + i, meadow_tile(2 + i), "Clear")
    for i in range(3):
        add(12 + i, rough_tile(i), "Rough")
    for i in range(3):
        add(20 + i, rough_tile(3 + i), "Rough")
    for i in range(2):
        add(15 + i, dirt_tile(i), "Rough")
    add(17, sand_tile(), "Clear")
    for i in range(4):
        add(100 + i, water_tile(W_SHALLOW, 1, i, 0), "Water")
    for i in range(2):
        add(104 + i, water_tile(W_MID, 2, i, 1), "Water")
    for i in range(2):
        add(106 + i, water_tile(W_DEEP, 3, i, 2), "Water")
    for (mask, corners), idx in SHORE_INDEX.items():
        add(LAND_BASE + idx, land_shore_tile(mask, corners, 500 + idx), "Clear")
    for (mask, corners), idx in SHORE_INDEX.items():
        add(WATER_BASE + idx, water_shore_tile(mask, corners, 900 + idx), "Water")
    return frames, ids, kinds


def write_tileset(ids, kinds):
    cats = {"Clear": "Terrain", "Rough": "Terrain", "Water": "Water"}
    out = ["General:", "\tName: tileset-temperate", "\tId: TEMPERATE", "\tTileSize: 32, 32", "\tSheetSize: 2048",
           "\tEditorTemplateOrder: Terrain, Water, Shore", "", "Terrain:"]
    for n, col, tt in (("Clear", "78B052", "Ground"), ("Rough", "7F9A52", "Ground"),
                       ("Water", "4A9FC9", "Water"), ("Road", "505050", "Ground")):
        out += [f"\tTerrainType@{n}:", f"\t\tType: {n}", f"\t\tTargetTypes: {tt}", f"\t\tColor: {col}"]
    out += ["", "Templates:"]
    for i, (tid, tt) in enumerate(zip(ids, kinds)):
        cat = "Shore" if tid >= LAND_BASE else cats[tt]
        out += [f"\tTemplate@{tid}:", f"\t\tId: {tid}", "\t\tImages: terrain.png", f"\t\tFrames: {i}",
                "\t\tSize: 1,1", f"\t\tCategories: {cat}", "\t\tTiles:", f"\t\t\t0: {tt}"]
    with open(os.path.join(MOD, "tilesets", "temperate.yaml"), "w") as f:
        f.write("\n".join(out) + "\n")


def contact_sheet(frames, path, cols=16):
    rows = (len(frames) + cols - 1) // cols
    c = Canvas(cols * T, rows * T)
    for i, f in enumerate(frames):
        c.blit(f, (i % cols) * T, (i // cols) * T, blend=False)
    c.scale(3).save(path)


# ---- trees ---------------------------------------------------------------------------------
LIGHT = (-0.55, -0.7, 0.45)  # light from the top-left


def blob(c, cx, cy, r, base, hi=1.28, lo=0.62):
    """Shaded sphere-ish leaf mass, lit from the top-left."""
    ln = math.sqrt(sum(v * v for v in LIGHT))
    lx, ly, lz = (v / ln for v in LIGHT)
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            dx, dy = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
            d2 = dx * dx + dy * dy
            if d2 > 1.0:
                edge = (math.sqrt(d2) - 1.0) * r
                if edge > 0.6:
                    continue
                a = int(255 * (0.6 - edge) / 0.6)
                nz = 0.0
            else:
                a = 255
                nz = math.sqrt(1 - d2)
            lam = max(0.0, dx * lx + dy * ly + nz * lz)
            f = lo + (hi - lo) * min(1.0, lam / 0.9)
            col = tone(base, f)
            c.blend(x, y, (col[0], col[1], col[2], a))


def leaf_dabs(c, rnd, cx, cy, r, base, n):
    for _ in range(n):
        a = rnd.uniform(0, TAU)
        d = r * math.sqrt(rnd.random()) * 0.85
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
        lit = (-(x - cx) - (y - cy)) / max(1.0, r)  # top-left = positive
        f = 1.0 + 0.22 * lit + rnd.uniform(-0.05, 0.05)
        c.circle(x, y, rnd.choice((1.0, 1.4, 1.8)), (*tone(base, f)[:3], 200))


def soft_shadow(c, cx, cy, rx, ry, alpha=62):
    for k in range(5):
        s = 1 - k * 0.12
        for y in range(int(cy - ry - 2), int(cy + ry + 3)):
            for x in range(int(cx - rx - 2), int(cx + rx + 3)):
                if y < 16:
                    continue
                if ((x + 0.5 - cx) / (rx * s)) ** 2 + ((y + 0.5 - cy) / (ry * s)) ** 2 <= 1:
                    c.blend(x, y, (20, 40, 30, alpha // 4))


def trunk(c, x, y0, y1, w, col, hi=None):
    for y in range(y0, y1):
        for dx in range(w):
            f = 1.15 if dx == 0 else (0.7 if dx == w - 1 else 1.0)
            c.blend(x + dx, y, tone(col, f))
    c.ellipse(x + w / 2.0, y1 - 0.5, w / 2.0 + 1.2, 1.3, tone(col, 0.75))


def make_tree(kind):
    c = Canvas(32, 48)
    rnd = random.Random(7000 + kind)
    if kind == 1:  # round, fresh green
        soft_shadow(c, 20, 41, 11, 4.2)
        trunk(c, 14, 31, 42, 4, hexcolor("8a5f3a"))
        base = hexcolor("4fa84a")
        for cx, cy, r in ((10.5, 26, 7.5), (21.5, 26.5, 7.5), (16, 25, 9), (13, 16.5, 8), (20, 17, 7.5), (16.5, 12, 7)):
            blob(c, cx, cy, r, base)
            leaf_dabs(c, rnd, cx, cy, r, base, 6)
    elif kind == 2:  # large, deep emerald with warm highlights
        soft_shadow(c, 20, 41, 12, 4.4)
        trunk(c, 14, 32, 42, 4, hexcolor("7a5233"))
        base = hexcolor("3e8f4c")
        for cx, cy, r in ((9.5, 27, 7), (22.5, 27, 7), (16, 27, 8), (11, 19, 8.5), (21, 19.5, 8), (16, 12.5, 8.5), (16, 20, 8)):
            blob(c, cx, cy, r, base, hi=1.35)
            leaf_dabs(c, rnd, cx, cy, r, hexcolor("5aa84c"), 5)
    elif kind == 3:  # conifer
        soft_shadow(c, 20, 42, 9, 3.6, 66)
        trunk(c, 15, 36, 43, 3, hexcolor("6b4a30"))
        base = hexcolor("2f7a52")
        for cy, w in ((33, 13), (26, 11), (19, 9.5), (12, 7.5), (6.5, 5)):
            tier = Canvas(32, 48)
            tier.polygon([(16, cy - w * 0.95), (16 + w, cy + 3.6), (16 - w, cy + 3.6)], base)
            # shading: left half lit, right half dark, bottom darker
            for y in range(48):
                for x in range(32):
                    p = tier.get(x, y)
                    if p[3] == 0:
                        continue
                    t = (x - 16) / w
                    f = 1.22 - 0.5 * (t + 1) / 2 + (rnd.random() - 0.5) * 0.08
                    f *= 1.0 - 0.18 * max(0.0, (y - (cy - w * 0.4)) / (w * 1.0))
                    col = tone(base, f)
                    tier.set(x, y, (col[0], col[1], col[2], p[3]))
            c.blit(tier, 0, 0)
            for i in range(5):  # ragged lower skirt
                x = int(16 - w + 2 + i * (2 * w - 4) / 4)
                c.circle(x, cy + 3.4, 1.6, tone(base, 0.78))
    else:  # birch / bushy
        soft_shadow(c, 20, 41, 9.5, 3.8)
        trunk(c, 15, 28, 42, 3, hexcolor("e9e6dc"))
        for y in (31, 34, 37, 40):
            c.blend(15 + (y % 2), y, (50, 45, 40, 220))
            c.blend(16, y + 1, (50, 45, 40, 150))
        c.line(16, 31, 11, 24, (225, 222, 210, 255))
        c.line(17, 30, 22, 23, (225, 222, 210, 255))
        base = hexcolor("8cc152")
        for cx, cy, r in ((11, 22, 5.8), (22, 22, 5.8), (16, 17, 7), (12, 12.5, 5.5), (20.5, 13, 5.5), (16, 8.5, 5.2), (16, 23, 5.8)):
            blob(c, cx, cy, r, base, hi=1.25, lo=0.68)
            leaf_dabs(c, rnd, cx, cy, r, hexcolor("a7d468"), 5)
    return c


def write_trees():
    for k in range(1, 5):
        save_sheet(os.path.join(MOD, "bits", "world", f"tree-{k}.png"), [make_tree(k)])


def tree_contact(path):
    c = Canvas(4 * 64, 96, (95, 156, 70))
    for k in range(1, 5):
        c.blit(make_tree(k), (k - 1) * 64 + 16, 24)
    c.scale(3).save(path)


def main():
    frames, ids, kinds = build_frames()
    save_sheet(os.path.join(MOD, "bits", "terrain", "terrain.png"), frames, cols=16)
    write_tileset(ids, kinds)
    write_trees()
    if "--sheet" in sys.argv:
        tree_contact("/tmp/trees.png")
    if "--sheet" in sys.argv:
        contact_sheet(frames, "/tmp/terrain-sheet.png")
    print(f"{len(frames)} tiles")


if __name__ == "__main__":
    main()
