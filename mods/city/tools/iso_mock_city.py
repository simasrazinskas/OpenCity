"""The mockup city: a 64x64-cell CS2-style town laid out procedurally (deterministic).

Screen layout (view centred on cell (32, 32)): a river runs along X (y 37-39) from the upper left to the
lower right; north of it (upper right) the downtown core, a mid-density ring and the industrial zone with
rail; south of it (lower left) suburbs with parks and a school; farms in the top-left corner.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402

N = 64
RIVER = (37, 38, 39)
N_ROWS = {8: "street", 13: "street", 18: "street", 23: "street", 28: "boulevard", 35: "street"}
N_COLS = [7, 12, 17, 22, 27, 37, 42, 47, 52, 57]
S_ROWS = {41: "street", 46: "street", 51: "street", 56: "street", 61: "street"}
S_COLS = [10, 16, 22, 28, 38, 44, 50, 56]
AVENUE_X = 32
RAIL_X = 45
BRIDGES = (22, 32)
NB = [(0, -1), (1, 0), (0, 1), (-1, 0)]
COLOURS = ["red", "blue", "white", "black", "silver", "green", "yellow", "beige"]


class City:
    def __init__(self, seed=7):
        self.rng = ik.Rng(seed)
        self.terr = {}
        self.roads = {}
        self.rail = set()
        self.occ = set()
        self.buildings = []     # dict(kind, args, cx, cy, fx, fy)
        self.trees = []         # (x, y, species, stage, seed)
        self.small = []         # (kind, x, y, seed)
        self.props = []         # (name, cx, cy, facing, arg)
        self.vehicles = []      # (model, x, y, dir, colour)
        self.people = []        # (k, x, y)
        self.lamps = []         # (cx, cy, facing)
        self.blocks = []
        self.extras = []
        self._terrain()
        self._roads()
        self._blocks()

    # ------------------------------------------------------------ base
    def water(self, x, y):
        return y in RIVER

    def district(self, x, y):
        if y > 39:
            return "suburb"
        if x < 12:
            return "farm"
        if x > RAIL_X:
            return "industry"
        if 22 <= x <= 42 and 13 < y < 35:
            return "downtown"
        return "mid"

    def _terrain(self):
        for x in range(N):
            for y in range(N):
                self.terr[(x, y)] = "water" if self.water(x, y) else "grass"

    def road(self, x, y, cls, bridge=None):
        if (x, y) in self.roads and self.roads[(x, y)]["cls"] in ("avenue", "boulevard") and cls == "street":
            return
        self.roads[(x, y)] = dict(cls=cls, bridge=bridge, ctrl=None)

    def _roads(self):
        for y, cls in N_ROWS.items():
            for x in range(1, N - 1):
                self.road(x, y, "gravel" if x < 12 and y != 35 else cls)
        for x in N_COLS:
            for y in range(4 if x < 12 else 2, 36):
                self.road(x, y, "gravel" if x < 12 else "street")
        for y, cls in S_ROWS.items():
            for x in range(4, N - 1):
                self.road(x, y, cls)
        for x in S_COLS:
            for y in range(41, N - 1):
                self.road(x, y, "street")
        for y in range(1, N - 1):
            if y in RIVER:
                self.road(AVENUE_X, y, "avenue", bridge=["end0", "pier", "end1"][y - RIVER[0]])
            else:
                self.road(AVENUE_X, y, "avenue")
        for y in RIVER:
            self.road(22, y, "street", bridge=["end0", "pier", "end1"][y - RIVER[0]])
        for y in range(36, 41):
            if y not in RIVER:
                self.road(22, y, "street")
                self.road(AVENUE_X, y, "avenue")
        for y in range(2, 34):
            if (RAIL_X, y) in self.roads:
                self.roads[(RAIL_X, y)]["crossing"] = True
            else:
                self.rail.add((RAIL_X, y))
        for c, r in self.roads.items():
            m = self.mask(c)
            n = bin(m).count("1")
            if n >= 3 and not r.get("crossing"):
                big = r["cls"] in ("avenue", "boulevard") or any(
                    self.roads.get((c[0] + dx, c[1] + dy), {}).get("cls") in ("avenue", "boulevard") for dx, dy in NB)
                r["ctrl"] = "signal" if big else ("stop" if r["cls"] == "street" else None)

    def mask(self, c):
        m = 0
        for i, (dx, dy) in enumerate(NB):
            n = (c[0] + dx, c[1] + dy)
            if n in self.roads or (n in self.rail and False):
                m |= 1 << i
        r = self.roads[c]
        if r.get("crossing"):
            m |= 0
        return m

    def rail_mask(self, c):
        m = 0
        for i, (dx, dy) in enumerate(NB):
            n = (c[0] + dx, c[1] + dy)
            if n in self.rail or (n in self.roads and self.roads[n].get("crossing")):
                m |= 1 << i
        return m

    def free(self, cells):
        return all(c not in self.roads and c not in self.rail and c not in self.occ and
                   self.terr.get(c) != "water" and 0 <= c[0] < N and 0 <= c[1] < N for c in cells)

    def take(self, cx, cy, fx, fy):
        for x in range(cx, cx + fx):
            for y in range(cy, cy + fy):
                self.occ.add((x, y))

    def place(self, kind, args, cx, cy, fx, fy):
        cells = [(x, y) for x in range(cx, cx + fx) for y in range(cy, cy + fy)]
        if not self.free(cells):
            return False
        self.take(cx, cy, fx, fy)
        self.buildings.append(dict(kind=kind, args=args, cx=cx, cy=cy, fx=fx, fy=fy))
        return True

    # ------------------------------------------------------------ blocks
    def _blocks(self):
        rows_n = sorted(set(N_ROWS) | {1})
        cols_n = sorted(set(N_COLS) | {AVENUE_X, RAIL_X, 0, N - 1})
        for a, b in zip(rows_n, rows_n[1:]):
            for c, d in zip(cols_n, cols_n[1:]):
                if b - a > 2 and d - c > 2:
                    self.blocks.append((c + 1, a + 1, d - c - 1, b - a - 1))
        rows_s = sorted(set(S_ROWS) | {N - 1})
        cols_s = sorted(set(S_COLS) | {AVENUE_X, 3, N - 1})
        for a, b in zip(rows_s, rows_s[1:]):
            for c, d in zip(cols_s, cols_s[1:]):
                if b - a > 2 and d - c > 2:
                    self.blocks.append((c + 1, a + 1, d - c - 1, b - a - 1))
