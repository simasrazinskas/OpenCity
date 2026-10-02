#!/usr/bin/env python3
"""
genmap.py - deterministic OpenCity map generator (python3 stdlib + pngkit + iso_terrain_ids only).

usage: genmap.py [--previews-only] [map ...]      (default: all maps)   maps: green-valley lakeside riverbend shellmap

--previews-only regenerates the map.png lobby previews (isometric diamonds) and leaves map.yaml / map.bin alone.
NOTE: map.png is part of the map UID (map format 12 hashes it), and the UID seeds simulation hashes (e.g. fish
stocks) and identifies replays. Writing new previews therefore changes the simulation: do it only in a deliberate
commit that also re-records mods/city/tests/golden (tools/golden.sh record). The committed previews are still the
top-down ones for that reason.

Writes mods/city/maps/<name>/{map.yaml,map.bin,map.png}. Template ids come from iso_terrain_ids.py.
Cell coordinates are absolute (the 1-cell map border included): playable Bounds = 1,1,W-2,H-2.
"""
import math
import os
import random
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngkit import Canvas, hexcolor, mix  # noqa: E402
import iso_terrain_ids as gt  # noqa: E402

MAPS = os.path.join(gt.MOD, "maps")
PREVIEWS_ONLY = False  # --previews-only: write map.png only (never map.yaml / map.bin)
N4 = ((0, -1), (1, 0), (0, 1), (-1, 0))  # N E S W, bit order 1,2,4,8
DIAG = ((1, -1, 1), (1, 1, 2), (-1, 1, 4), (-1, -1, 8))  # NE SE SW NW


class Noise:
    def __init__(self, seed):
        self.seed = seed

    def lat(self, ix, iy):
        return gt.hash01(ix, iy, self.seed)

    def at(self, x, y):
        ix, iy = math.floor(x), math.floor(y)
        tx, ty = x - ix, y - iy
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = self.lat(ix, iy) * (1 - tx) + self.lat(ix + 1, iy) * tx
        b = self.lat(ix, iy + 1) * (1 - tx) + self.lat(ix + 1, iy + 1) * tx
        return a * (1 - ty) + b * ty

    def fbm(self, x, y, octaves=4):
        v, amp, tot, f = 0.0, 0.5, 0.0, 1.0
        for _ in range(octaves):
            v += amp * self.at(x * f, y * f)
            tot += amp
            amp *= 0.5
            f *= 2
        return v / tot


class Map:
    def __init__(self, name, title, w, h, seed, visibility="Lobby"):
        self.name, self.title, self.w, self.h, self.seed, self.visibility = name, title, w, h, seed, visibility
        self.rnd = random.Random(seed)
        self.water = [[False] * w for _ in range(h)]
        self.guard = set()  # cells that must stay clear land
        self.actors = []  # (type, x, y)
        self.occupied = set()
        self.tmpl = None
        self.kind = None  # per-cell kind string for the preview
        self.rules = None
        self.n_trees = Noise(seed + 1)
        self.n_meadow = Noise(seed + 2)
        self.n_rough = Noise(seed + 3)
        self.n_var = Noise(seed + 4)
        self.anchor = None  # (x, y) end of the outside connection road
        self.res = {}  # (x, y) -> (type, density); type 1 fertile, 2 ore, 3 oil (map.bin resource bytes)

    # ---- geometry helpers ------------------------------------------------------------------
    def inb(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def add_guard(self, x0, y0, x1, y1):
        for y in range(max(0, y0), min(self.h, y1 + 1)):
            for x in range(max(0, x0), min(self.w, x1 + 1)):
                self.guard.add((x, y))

    def guard_margin(self, m):
        out = set()
        for (x, y) in self.guard:
            for dy in range(-m, m + 1):
                for dx in range(-m, m + 1):
                    out.add((x + dx, y + dy))
        return out

    def disc(self, cx, cy, r, val=True):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if self.inb(x, y) and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    self.water[y][x] = val

    def lake(self, cx, cy, rx, ry, seed, wobble=0.35):
        n = Noise(seed)
        for y in range(int(cy - ry * 1.6), int(cy + ry * 1.6)):
            for x in range(int(cx - rx * 1.6), int(cx + rx * 1.6)):
                if not self.inb(x, y):
                    continue
                d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
                if d < 1 + (n.fbm(x / 7.0, y / 7.0, 3) - 0.5) * 2 * wobble:
                    self.water[y][x] = True

    def river(self, pts, width, seed, amp=5.0, wl=18.0):
        """Meandering river through control points [(x, y), ...] (piecewise linear + sine/noise offset)."""
        n = Noise(seed)
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            length = math.hypot(x1 - x0, y1 - y0)
            nx, ny = -(y1 - y0) / length, (x1 - x0) / length
            steps = int(length * 2)
            for i in range(steps + 1):
                t = i / steps
                s = t * length
                off = amp * math.sin(s / wl * math.tau + seed) * math.sin(math.pi * t) + (n.fbm(s / 9.0, seed, 2) - 0.5) * amp
                w = width * (0.85 + 0.5 * n.at(s / 6.0, seed + 3))
                self.disc(x0 + (x1 - x0) * t + nx * off, y0 + (y1 - y0) * t + ny * off, w / 2.0)

    def finalize_water(self):
        keep = self.guard_margin(2)
        for (x, y) in keep:
            if self.inb(x, y):
                self.water[y][x] = False
        for _ in range(3):  # remove 1-cell specks / necks that make ugly shores
            new = [row[:] for row in self.water]
            for y in range(self.h):
                for x in range(self.w):
                    c = sum(1 for dx, dy in N4 if self.is_water(x + dx, y + dy, self.water[y][x]))
                    if self.water[y][x] and c <= 1 and (x, y) not in keep:
                        new[y][x] = False
                    elif not self.water[y][x] and c >= 3:
                        new[y][x] = True if (x, y) not in keep else False
            self.water = new

    def is_water(self, x, y, default=False):
        return self.water[y][x] if self.inb(x, y) else default

    # ---- terrain templates -----------------------------------------------------------------
    def shore(self, x, y, other):
        """(mask, corners) of neighbouring cells whose water-ness == other."""
        mask = 0
        for i, (dx, dy) in enumerate(N4):
            if self.is_water(x + dx, y + dy, self.water[y][x]) == other:
                mask |= 1 << i
        corners = 0
        for dx, dy, bit in DIAG:
            sa, sb = gt.CORNER_SIDES[bit]
            if mask & sa or mask & sb:
                continue
            if self.is_water(x + dx, y + dy, self.water[y][x]) == other:
                corners |= bit
        return mask, corners

    def water_distance(self):
        INF = 999
        d = [[INF] * self.w for _ in range(self.h)]
        q = []
        for y in range(self.h):
            for x in range(self.w):
                if not self.water[y][x]:
                    d[y][x] = 0
                    q.append((x, y))
        i = 0
        while i < len(q):
            x, y = q[i]
            i += 1
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if self.inb(nx, ny) and d[ny][nx] > d[y][x] + 1:
                        d[ny][nx] = d[y][x] + 1
                        q.append((nx, ny))
        return d

    def build_terrain(self, rough_ok=True):
        dist = self.water_distance()
        self.tmpl = [[1] * self.w for _ in range(self.h)]
        self.kind = [[""] * self.w for _ in range(self.h)]
        for y in range(self.h):
            for x in range(self.w):
                if self.water[y][x]:
                    m, c = self.shore(x, y, False)
                    if m or c:
                        self.tmpl[y][x] = gt.WATER_BASE + gt.SHORE_INDEX[(m, c)]
                        self.kind[y][x] = "wshore"
                    else:
                        d = dist[y][x]
                        v = int(gt.hash01(x, y, self.seed + 11) * 4)
                        if d <= 3:
                            self.tmpl[y][x], self.kind[y][x] = 100 + v, "shallow"
                        elif d <= 6:
                            self.tmpl[y][x], self.kind[y][x] = 104 + (v & 1), "mid"
                        else:
                            self.tmpl[y][x], self.kind[y][x] = 106 + (v & 1), "deep"
                    continue
                m, c = self.shore(x, y, True)
                if m or c:
                    self.tmpl[y][x] = gt.LAND_BASE + gt.SHORE_INDEX[(m, c)]
                    self.kind[y][x] = "beach"
                    continue
                r = gt.hash01(x, y, self.seed + 21)
                mead = self.n_meadow.fbm(x / 14.0, y / 14.0, 3)
                rough = self.n_rough.fbm(x / 11.0, y / 11.0, 3)
                if (x, y) in self.guard:
                    self.tmpl[y][x] = 1 if r < 0.55 else 2 + int(r * 40) % 5
                    self.kind[y][x] = "grass"
                elif mead > 0.62:
                    self.tmpl[y][x], self.kind[y][x] = (10, 11, 18, 19)[int(r * 40) % 4], "meadow"
                elif rough_ok and rough > 0.66:
                    dirt = rough > 0.84 and r > 0.7
                    self.tmpl[y][x] = 15 + (r > 0.8) if dirt else (12, 13, 14, 20, 21, 22)[int(r * 60) % 6]
                    self.kind[y][x] = "dirt" if dirt else "rough"
                else:
                    self.tmpl[y][x] = 1 if r < 0.5 else 2 + int(r * 50) % 5
                    self.kind[y][x] = "grass"

    # ---- nature ----------------------------------------------------------------------------
    def can_tree(self, x, y):
        return (self.inb(x, y) and (x, y) not in self.occupied and (x, y) not in self.guard
                and self.kind[y][x] in ("grass", "meadow", "rough") and 1 <= x < self.w - 1 and 1 <= y < self.h - 1)

    def put(self, typ, x, y, size=1):
        self.actors.append((typ, x, y))
        for dy in range(size):
            for dx in range(size):
                self.occupied.add((x + dx, y + dy))

    def forest(self, cx, cy, r, density, mix_=(1, 1, 1, 1), squash=1.0):
        rnd = random.Random(self.seed * 1000 + int(cx) * 7 + int(cy))
        for y in range(int(cy - r * 1.4), int(cy + r * 1.4) + 1):
            for x in range(int(cx - r * 1.4), int(cx + r * 1.4) + 1):
                if not self.can_tree(x, y):
                    continue
                d = math.hypot((x - cx), (y - cy) * squash) / r
                d += (self.n_trees.fbm(x / 4.0, y / 4.0, 2) - 0.5) * 0.8
                if d >= 1:
                    continue
                if rnd.random() < density * (1 - d) ** 0.6:
                    self.put("tree-%d" % rnd.choices((1, 2, 3, 4), mix_)[0], x, y)

    def scatter_trees(self, p=0.012, mix_=(1, 1, 1, 1)):
        rnd = random.Random(self.seed * 77)
        for y in range(self.h):
            for x in range(self.w):
                if self.can_tree(x, y) and rnd.random() < p:
                    self.put("tree-%d" % rnd.choices((1, 2, 3, 4), mix_)[0], x, y)

    # ---- natural resources (WP IND: map.bin resource bytes) -----------------------------------
    RES_FERTILE, RES_ORE, RES_OIL = 1, 2, 3

    def _dist_to_water(self):
        INF = 9999
        d = [[INF] * self.w for _ in range(self.h)]
        q = []
        for y in range(self.h):
            for x in range(self.w):
                if self.water[y][x]:
                    d[y][x] = 0
                    q.append((x, y))
        i = 0
        while i < len(q):
            x, y = q[i]
            i += 1
            for dx, dy in N4:
                nx, ny = x + dx, y + dy
                if self.inb(nx, ny) and d[ny][nx] > d[y][x] + 1:
                    d[ny][nx] = d[y][x] + 1
                    q.append((nx, ny))
        return d

    def _res_free(self, x, y, kinds):
        return (self.inb(x, y) and 1 <= x < self.w - 1 and 1 <= y < self.h - 1 and (x, y) not in self.guard
                and (x, y) not in self.res and self.kind[y][x] in kinds)

    def _anchor_dist(self, x, y):
        return math.hypot(x - self.anchor[0], y - self.anchor[1])

    def _blob(self, cx, cy, r, typ, kinds, noise, dmin, dmax, bonus_kinds=()):
        """Noise-shaped blob; density fades from dmax in the core to dmin at the rim. Returns cells placed."""
        n = 0
        for y in range(int(cy - r * 1.5), int(cy + r * 1.5) + 1):
            for x in range(int(cx - r * 1.5), int(cx + r * 1.5) + 1):
                if not self._res_free(x, y, kinds):
                    continue
                d = math.hypot(x - cx, y - cy) / r
                d += (noise.fbm(x / 5.0, y / 5.0, 3) - 0.5) * 0.7
                if d >= 1:
                    continue
                core = max(0.0, 1 - d)
                den = dmin + (dmax - dmin) * (core ** 0.8) * (0.6 + 0.4 * noise.at(x / 3.0, y / 3.0))
                if self.kind[y][x] in bonus_kinds:
                    den *= 1.15
                self.res[(x, y)] = (typ, max(1, min(255, int(den))))
                n += 1
        return n

    def _pick(self, rnd, kinds, r, score_kinds, dmin, dmax, avoid=(), spacing=0, tries=400):
        """Centre (x, y) at anchor distance dmin..dmax whose radius-r disc holds the most cells of score_kinds."""
        w2 = self._dist_to_water()
        cands = []
        for _ in range(tries):
            x, y = rnd.randrange(3, self.w - 3), rnd.randrange(3, self.h - 3)
            if not (dmin <= self._anchor_dist(x, y) <= dmax) or self.kind[y][x] not in kinds:
                continue
            if any(math.hypot(x - ax, y - ay) < spacing for ax, ay in avoid):
                continue
            cnt = 0
            for yy in range(y - r, y + r + 1, 2):
                for xx in range(x - r, x + r + 1, 2):
                    if self.inb(xx, yy) and self.kind[yy][xx] in score_kinds and (xx, yy) not in self.guard:
                        cnt += 1
            cands.append((cnt, -rnd.random(), x, y))
        if not cands:
            return None
        cands.sort(reverse=True)
        return cands[0][2], cands[0][3]

    def resources(self, ore_far, oil_zone, fertile_count=5):
        """Fertile patches near water, two ore blobs (one near, one far), one far oil field. Own RNG: trees are unaffected."""
        rnd = random.Random(self.seed * 131 + 7)
        n_f = Noise(self.seed + 31)
        n_o = Noise(self.seed + 32)
        n_oil = Noise(self.seed + 33)
        w2 = self._dist_to_water()
        centres = []
        # Ore first (hills), so fertile soil does not take the rough cells.
        hills = ("rough", "dirt")
        ore_kinds = ("rough", "dirt", "grass", "meadow")
        near = self._pick(rnd, ore_kinds, 6, hills, 25, 45, spacing=0)
        if near:
            self._blob(near[0], near[1], rnd.uniform(5.5, 8), self.RES_ORE, ore_kinds, n_o, 100, 255, hills)
            centres.append(near)
        far = self._pick(rnd, ore_kinds, 6, hills, ore_far[0], ore_far[1], avoid=centres, spacing=30)
        if far:
            self._blob(far[0], far[1], rnd.uniform(5.5, 8), self.RES_ORE, ore_kinds, n_o, 100, 255, hills)
            centres.append(far)
        oil = self._pick(rnd, ore_kinds, 5, ore_kinds, oil_zone[0], oil_zone[1], avoid=centres, spacing=22)
        if oil:
            self._blob(oil[0], oil[1], rnd.uniform(4.2, 6), self.RES_OIL, ore_kinds, n_oil, 90, 220)
            centres.append(oil)

        fertile = []
        for _ in range(fertile_count):
            c = None
            for attempt in range(300):
                # Prefer soil near rivers/lakes (within 14 cells) and a walkable distance from the start area.
                x, y = rnd.randrange(8, self.w - 8), rnd.randrange(8, self.h - 8)
                if self.kind[y][x] not in ("grass", "meadow") or w2[y][x] > 14 or w2[y][x] < 2:
                    continue
                if not (14 <= self._anchor_dist(x, y) <= 75):
                    continue
                if any(math.hypot(x - ax, y - ay) < 18 for ax, ay in centres + fertile):
                    continue
                c = (x, y)
                break
            if c:
                fertile.append(c)
                self._blob(c[0], c[1], rnd.uniform(8, 14), self.RES_FERTILE, ("grass", "meadow"), n_f, 40, 255, ("meadow",))
        counts = {}
        for t, _d in self.res.values():
            counts[t] = counts.get(t, 0) + 1
        print(f"  resources: fertile={counts.get(1, 0)} ore={counts.get(2, 0)} oil={counts.get(3, 0)}")

    # ---- output ----------------------------------------------------------------------------
    def write(self):
        d = os.path.join(MAPS, self.name)
        os.makedirs(d, exist_ok=True)
        w, h = self.w, self.h
        if PREVIEWS_ONLY:
            self.preview().save(os.path.join(d, "map.png"))
            print(f"{self.name}: map.png preview only")
            return
        with open(os.path.join(d, "map.bin"), "wb") as f:
            f.write(struct.pack("<BHHIII", 2, w, h, 17, 0, 17 + 3 * w * h))
            for x in range(w):
                for y in range(h):
                    f.write(struct.pack("<HB", self.tmpl[y][x], 0))
            for x in range(w):
                for y in range(h):
                    f.write(bytes(self.res.get((x, y), (0, 0))))
        lines = ["MapFormat: 12", "", "RequiresMod: city", "", f"Title: {self.title}", "", "Author: OpenCity", "",
                 "Tileset: TEMPERATE", "", f"MapSize: {w},{h}", "", f"Bounds: 1,1,{w - 2},{h - 2}", "",
                 f"Visibility: {self.visibility}", "", "Categories: City", "", "LockPreview: True", "",
                 "Players:", "\tPlayerReference@Neutral:", "\t\tName: Neutral", "\t\tOwnsWorld: True",
                 "\t\tNonCombatant: True", "\t\tFaction: city", "\tPlayerReference@Multi0:", "\t\tName: Multi0",
                 "\t\tPlayable: True", "\t\tRequired: True", "\t\tAllowBots: False", "\t\tLockFaction: True",
                 "\t\tFaction: city", "", "Actors:"]
        for i, (t, x, y) in enumerate(self.actors):
            lines += [f"\tActor{i}: {t}", f"\t\tLocation: {x},{y}", "\t\tOwner: Neutral"]
        if self.rules:
            lines += ["", "Rules:"] + self.rules
        with open(os.path.join(d, "map.yaml"), "w") as f:
            f.write("\n".join(lines) + "\n")
        self.preview().save(os.path.join(d, "map.png"))
        trees = sum(1 for a in self.actors if a[0].startswith("tree-"))
        print(f"{self.name}: {w}x{h}, {len(self.actors)} actors ({trees} trees)")

    PCOL = {"grass": "729f50", "meadow": "78a856", "rough": "6a9a4a", "dirt": "86895a", "beach": "bcc17a",
            "wshore": "7cc0cf", "shallow": "5fb6d4", "mid": "4a9fc9", "deep": "3a85b8"}
    ACOL = {"res": "e8845a", "com": "4aa3e8", "off": "a77be0", "ind": "e6c040", "road": "6b6f78", "park": "3f9d4a",
            "svc": "e04a4a", "hw": "3a3d44"}

    RCOL = {1: (150, 160, 40), 2: (96, 112, 150), 3: (24, 24, 28)}  # fertile olive, ore blue-grey, oil black

    def tint_resource(self, x, y, col):
        r = self.res.get((x, y))
        if not r:
            return col
        t, den = r
        a = 0.35 + 0.45 * den / 255.0
        rc = self.RCOL[t]
        return tuple(int(col[i] * (1 - a) + rc[i] * a) for i in range(3))

    VOID = (14, 16, 22)      # RCT2-style dark backdrop
    VOID_DOT = (20, 23, 31)  # subtle backdrop pattern

    def preview(self, scale=None):
        """Isometric lobby preview: the playable map as a 2:1 diamond on a dark backdrop. The image keeps
        the old square size (cells * scale), so the lobby preview slot is unchanged."""
        scale = scale or (4 if self.w < 80 else 2)
        nx, ny = self.w - 2, self.h - 2
        W, H = nx * scale, ny * scale
        c = Canvas(W, H)
        a = min(W / float(nx + ny), 2.0 * H / (nx + ny))  # px per cell along the screen x diagonal (half a cell width)
        ox = (W - (nx + ny) * a) / 2.0 + ny * a           # screen x of map corner (0, 0)
        oy = (H - (nx + ny) * a / 2.0) / 2.0              # screen y of map corner (0, 0)
        ss = 3  # supersampling per axis (smooth diamond edges, 2-px wide cells stay readable)
        cols = {k: hexcolor(v)[:3] for k, v in self.PCOL.items()}

        def cell_at(px, py):
            """Image pixel (float) -> (x, y) cell index inside the playable area or None."""
            dx, dy = (px - ox) / a, (py - oy) / (a / 2.0)  # dx = X - Y, dy = X + Y
            X, Y = (dy + dx) * 0.5, (dy - dx) * 0.5
            if 0 <= X < nx and 0 <= Y < ny:
                return int(X), int(Y)
            return None

        for py in range(H):
            for px in range(W):
                r = g = b = 0
                hit = 0
                for j in range(ss):
                    for i in range(ss):
                        k = cell_at(px + (i + 0.5) / ss, py + (j + 0.5) / ss)
                        if k is None:
                            continue
                        x, y = k
                        col = cols[self.kind[y + 1][x + 1]]
                        col = self.tint_resource(x + 1, y + 1, col)
                        nz = (gt.hash01(x, y, 5) - 0.5) * 0.06
                        # soft light from the upper left: darken the lower-right part of the map slightly
                        f = 1.04 - 0.10 * ((x + y) / float(nx + ny)) + nz
                        r += col[0] * f
                        g += col[1] * f
                        b += col[2] * f
                        hit += 1
                bd = self.VOID_DOT if (px + py) % 4 == 0 and px % 2 == 0 else self.VOID
                if hit == 0:
                    c.set(px, py, (*bd, 255))
                else:
                    t = hit / float(ss * ss)
                    rr = (r / hit) * t + bd[0] * (1 - t)
                    gg = (g / hit) * t + bd[1] * (1 - t)
                    bb = (b / hit) * t + bd[2] * (1 - t)
                    c.set(px, py, (max(0, min(255, int(rr))), max(0, min(255, int(gg))), max(0, min(255, int(bb))), 255))

        def pos(ax, ay, w=1, h=1):
            """Playable cell (ax, ay) with a w x h footprint -> image px of its centre."""
            X, Y = (ax - 1) + w / 2.0, (ay - 1) + h / 2.0
            return ox + (X - Y) * a, oy + (X + Y) * a / 2.0

        for t, ax, ay in self.actors:
            if t.startswith("tree-"):
                col, s_ = ((52, 120, 62) if t != "tree-3" else (36, 98, 66)), 1
            elif t == "roadseed" or t.startswith("highway"):
                col, s_ = hexcolor(self.ACOL["road"]), 1
            else:
                key = t.split("-")[0]
                col = hexcolor(self.ACOL.get(key, self.ACOL["svc"]))
                if t in ("park-small", "plaza", "park-large"):
                    col = hexcolor(self.ACOL["park"])
                s_ = 2 if t in ("police", "firestation", "clinic", "school", "park-large", "powerplant-coal", "solarplant") else 1
            cx, cy = pos(ax, ay, s_, s_)
            # a small diamond (2:1) per actor cell footprint
            rw = max(1.0, s_ * a)
            for yy in range(int(cy - rw / 2) - 1, int(cy + rw / 2) + 2):
                for xx in range(int(cx - rw) - 1, int(cx + rw) + 2):
                    if abs(xx + 0.5 - cx) / rw + abs(yy + 0.5 - cy) / (rw / 2.0) <= 1.0 and 0 <= xx < W and 0 <= yy < H:
                        c.set(xx, yy, (*col[:3], 255))
        return c


# ---- highways ------------------------------------------------------------------------------
HWDIR = {"highway-w": (1, 0), "highway-e": (-1, 0), "highway-n": (0, 1), "highway-s": (0, -1)}


def add_highway(m, typ, x, y, length=11):
    """Place the highway actor (first actor), mark its road and the clear build area; returns the anchor."""
    dx, dy = HWDIR[typ]
    ax, ay = x + dx * length, y + dy * length
    m.actors.insert(0, (typ, x, y))

    def cell(a, b):  # a = along the road, b = lateral
        return (x + dx * a - dy * b, y + dy * a + dx * b)

    for a in range(0, length + 1):
        for b in range(-3, 4):
            m.guard.add(cell(a, b))
    for a in range(length - 1, length + 38):
        for b in range(-10, 11):
            m.guard.add(cell(a, b))
    m.guard = {c for c in m.guard if m.inb(*c)}
    m.occupied.update(cell(a, 0) for a in range(0, length + 1))
    m.anchor = (ax, ay)
    return ax, ay


def forests(m, specs):
    mixes = {"d": (4, 4, 1, 1), "c": (1, 1, 5, 1), "b": (2, 1, 1, 4), "m": (3, 3, 2, 2)}
    for cx, cy, r, dens, mx in specs:
        m.forest(cx, cy, r, dens, mixes[mx])


def finish(m):
    m.finalize_water()
    m.build_terrain()


def green_valley():
    m = Map("green-valley", "Green Valley", 130, 130, 11)
    add_highway(m, "highway-w", 1, 64)
    m.river([(78, 1), (70, 25), (86, 48), (76, 75), (84, 100), (68, 128)], 5.5, 3)
    m.river([(128, 86), (105, 80), (80, 76)], 3.2, 5, amp=4)
    m.lake(98, 34, 14, 10, 21)
    m.lake(26, 106, 10, 6, 22)
    m.lake(32, 22, 8, 5.5, 23)
    finish(m)
    forests(m, [(18, 34, 13, .8, "d"), (42, 100, 14, .75, "m"), (112, 18, 14, .8, "c"), (108, 108, 16, .8, "m"),
                (60, 118, 10, .7, "b"), (56, 34, 9, .7, "d"), (8, 94, 9, .8, "c"), (118, 62, 9, .8, "b"),
                (64, 88, 8, .6, "m"), (46, 8, 10, .75, "b"), (100, 76, 6, .6, "d")])
    m.scatter_trees(0.012)
    m.resources(ore_far=(60, 120), oil_zone=(35, 70))
    m.write()


def lakeside():
    m = Map("lakeside", "Lakeside", 112, 112, 12)
    add_highway(m, "highway-s", 30, 110)
    m.lake(78, 46, 30, 22, 31, 0.4)
    m.lake(18, 22, 9, 6, 32)
    m.river([(100, 50), (111, 62)], 3.6, 7, amp=2)
    finish(m)
    forests(m, [(14, 60, 12, .8, "c"), (50, 90, 12, .75, "m"), (96, 98, 14, .8, "d"), (50, 12, 11, .8, "b"),
                (104, 20, 10, .75, "c"), (70, 94, 7, .6, "b"), (8, 98, 8, .7, "d"), (36, 36, 7, .6, "m")])
    m.scatter_trees(0.012)
    m.resources(ore_far=(45, 95), oil_zone=(35, 70))
    m.write()


def riverbend():
    m = Map("riverbend", "River Bend", 120, 120, 13)
    add_highway(m, "highway-n", 30, 1)
    m.river([(118, 22), (96, 34), (72, 58), (92, 84), (58, 102), (30, 90), (4, 108)], 6.0, 9, amp=7, wl=26)
    m.lake(84, 46, 5.5, 9, 41)
    m.lake(14, 70, 8, 5.5, 42)
    finish(m)
    forests(m, [(10, 40, 11, .8, "d"), (60, 20, 12, .75, "m"), (108, 84, 13, .8, "c"), (96, 6, 9, .7, "b"),
                (40, 72, 8, .65, "m"), (14, 108, 9, .8, "c"), (76, 108, 10, .7, "d"), (112, 50, 8, .75, "b"),
                (50, 52, 6, .6, "d")])
    m.scatter_trees(0.012)
    m.resources(ore_far=(50, 100), oil_zone=(35, 70))
    m.write()



# ---- shellmap: a small pre-built town --------------------------------------------------------
def shellmap():
    m = Map("shellmap", "OpenCity Shellmap", 64, 48, 21, visibility="Shellmap")
    rnd = m.rnd
    m.lake(58, 27, 7, 14, 51, 0.3)
    m.lake(5, 41, 4, 3, 52)
    finish(m)
    xs, ys = (8, 20, 32, 44), (10, 22, 34)
    roads = set()
    for y in ys:
        for x in range(3 if y == 22 else 8, 45):
            roads.add((x, y))
    for x in xs:
        for y in range(10, 35):
            roads.add((x, y))
    for (x, y) in sorted(roads, key=lambda c: (c[1], c[0])):
        m.put("roadseed", x, y)

    def land_free(x, y):
        return (m.inb(x, y) and 1 <= x < m.w - 1 and 1 <= y < m.h - 1 and not m.water[y][x]
                and (x, y) not in m.occupied and m.kind[y][x] != "wshore")

    # services (2x2 top-left locations, all adjacent to a road)
    services = [("police", 21, 12), ("firestation", 21, 35), ("clinic", 9, 23), ("school", 45, 23),
                ("powerplant-coal", 9, 12), ("solarplant", 33, 35), ("park-large", 33, 12), ("park-large", 9, 35),
                ("clinic", 45, 12), ("police", 33, 23)]
    for t, x, y in services:
        if all(land_free(x + dx, y + dy) for dx in (0, 1) for dy in (0, 1)):
            m.put(t, x, y, 2)
    for x, y in ((14, 9), (26, 9), (38, 9), (14, 36), (26, 36), (38, 36)):
        if land_free(x, y):
            m.put("windturbine", x, y)
    for x, y in ((19, 11), (43, 21), (7, 33), (31, 21)):
        if land_free(x, y):
            m.put("watertower", x, y)

    # growables along the roads (two deep)
    rd = {}
    q = list(roads)
    for c in q:
        rd[c] = 0
    i = 0
    while i < len(q):
        x, y = q[i]
        i += 1
        if rd[(x, y)] >= 2:
            continue
        for dx, dy in N4:
            n = (x + dx, y + dy)
            if n not in rd:
                rd[n] = rd[(x, y)] + 1
                q.append(n)
    cx, cy = 26, 22
    for (x, y), d in sorted(rd.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        if d == 0 or not land_free(x, y) or rnd.random() < (0.06 if d == 1 else 0.22):
            continue
        dist = math.hypot((x - cx) * 0.8, (y - cy) * 1.2)
        if x < 22 and y < 22 and dist > 7:
            t, lvl = "ind", (2 if dist < 17 else 1)
        elif dist < 8:
            t, lvl = ("com-high" if d == 1 else "off"), (3 if dist < 5 else 2)
        elif dist < 15:
            t = rnd.choice(("com-low", "res-high", "res-high", "off")) if d == 1 else rnd.choice(("res-high", "res-low"))
            lvl = 2 if t != "res-low" else 3
        else:
            t, lvl = ("com-low" if (d == 1 and y == 22 and rnd.random() < 0.5) else "res-low"), rnd.choice((1, 1, 2))
        if t == "off" and lvl == 1:
            lvl = 2
        m.put(f"{t}-{lvl}", x, y)

    # block interiors: parks and trees; then forests around the town
    for y in range(m.h):
        for x in range(m.w):
            if land_free(x, y) and (x, y) in rd or not land_free(x, y):
                continue
            if 8 < x < 44 and 10 < y < 34:
                r = rnd.random()
                if r < 0.1:
                    m.put("park-small", x, y)
                elif r < 0.55:
                    m.put("tree-%d" % rnd.choices((1, 2, 3, 4), (4, 4, 1, 3))[0], x, y)
    forests(m, [(14, 3, 9, .85, "m"), (38, 3, 10, .8, "d"), (4, 20, 6, .8, "b"), (52, 6, 8, .85, "c"),
                (26, 43, 11, .85, "m"), (50, 42, 9, .8, "d"), (12, 44, 8, .8, "c"), (62, 44, 5, .7, "m")])
    m.scatter_trees(0.03)
    m.rules = ["\tWorld:", "\t\tCameraOvalMover:", "\t\t\tStartAngle: 20", "\t\t\tDegreesPerSecond: 2",
               "\t\t\tOvalRadius: 4096,2560,0"]
    m.write()


BUILDERS = {"green-valley": green_valley, "lakeside": lakeside, "riverbend": riverbend, "shellmap": shellmap}


def main():
    global PREVIEWS_ONLY
    PREVIEWS_ONLY = "--previews-only" in sys.argv[1:]
    names = [a for a in sys.argv[1:] if not a.startswith("-")] or list(BUILDERS)
    for n in names:
        BUILDERS[n]()


if __name__ == "__main__":
    main()
