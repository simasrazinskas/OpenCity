"""iso_zoned_lot.py - the Lot builder every ZONED generator draws through.

A Lot wraps an isokit Scene for one footprint (W along X = frontage, D along Y = depth, front edge at
y = D, i.e. the front faces +Y = the default facing). Builders call lot.block / roof / prop helpers; the
Lot applies the building state so one model function yields every state:
  state: ok | abandoned | burnt | collapsed
  stage: 0 finished | 1 foundation | 2 frame + scaffolding + crane | 3 nearly done
"""
import isokit as ik
from isokit.noise import Rng

import iso_zoned_mats as M

ST = ik.STOREY

# NET sidewalk contract: sidewalk = stone shade 8 (184,173,154); forecourts that touch it use the same tone.
# Top faces light at 0.75 * 11 = 8.25, so a -0.25 offset lands paving on shade 8.
FORECOURT = ik.mat("paving", shade=-0.25)


class Lot:
    def __init__(self, W, D, level=1, var=0, state="ok", stage=0, seed=0, season="summer"):
        self.W, self.D, self.level, self.var = W, D, level, var
        self.state, self.stage = state, stage
        self.season = season
        self.theme = "na" if var % 4 < 2 else "eu"
        self.seed = seed
        self.s = ik.Scene((W, D), seed=seed)
        self.rng = Rng(seed)
        self.masses = []
        self.top = 0.0

    # ------------------------------------------------------------- state helpers
    @property
    def eu(self):
        return self.theme == "eu"

    @property
    def live(self):
        """Finished, inhabited building (props, cars, lights)."""
        return self.stage == 0 and self.state == "ok"

    @property
    def walls_visible(self):
        return self.stage in (0, 3)

    def wstate(self):
        if self.stage:
            return "ok"
        return "abandoned" if self.state == "collapsed" else self.state

    def m(self, mat, **kw):
        """Material following the lot state."""
        mm = ik.mat(mat, **kw) if isinstance(mat, str) or kw else ik.mat(mat)
        return M.st(mm, self.wstate())

    def fac(self, base, **kw):
        """Facade material (windows, doors...) following the lot state."""
        if self.stage == 3:
            kw = dict(kw, lit=0.0)
        return M.facade(base, state=self.wstate(), **kw)

    def r(self, a=0.0, b=1.0):
        return self.rng.uniform(a, b)

    def pick(self, seq):
        return self.rng.choice(seq)

    def chance(self, p):
        return self.rng.chance(p)

    # ------------------------------------------------------------- ground
    def lawn(self, mat="grass"):
        """Whole-lot base ground."""
        if self.stage:
            mat = "mud" if self.stage == 1 else "dirt"
        elif self.state == "abandoned" or self.state == "collapsed":
            mat = "grass_dry" if mat in ("grass", "meadow") else mat
        elif self.state == "burnt":
            mat = "dirt" if mat in ("grass", "meadow") else mat
        if mat == "paving":
            mat = FORECOURT
        self.s.ground([(0, 0), (self.W, 0), (self.W, self.D), (0, self.D)], self.m(mat) if mat not in ("mud", "dirt", "grass_dry") else mat)
        if self.state in ("abandoned", "collapsed") and not self.stage:
            for _ in range(int(3 * self.W * self.D)):
                x, y = self.r(0.05, self.W - 0.15), self.r(0.05, self.D - 0.15)
                self.s.ground([(x, y), (x + 0.12, y), (x + 0.12, y + 0.08), (x, y + 0.08)], "meadow", layer=3)

    def pave(self, x, y, dx, dy, mat="paving", layer=1, hard=True):
        """Ground rect. During construction only hard surfaces near the front stay (as gravel)."""
        if self.stage in (1, 2):
            return
        if self.stage == 3:
            mat = "gravel" if hard else "dirt"
        if isinstance(mat, str) and mat == "paving":
            mat = FORECOURT
        self.s.ground([(x, y), (x + dx, y), (x + dx, y + dy), (x, y + dy)], self.m(mat), layer=layer)

    def poly_ground(self, pts, mat, layer=1):
        if self.stage in (1, 2):
            return
        self.s.ground(pts, self.m(mat if not self.stage else "dirt"), layer=layer)

    # ------------------------------------------------------------- solids
    def box6(self, x, y, z, dx, dy, dz, front, side=None, back=None, top=None, left=None):
        """Axis-aligned box with per-face materials: front = +Y, side = +X, left = -X (default side),
        back = -Y (default side), top."""
        s = self.s
        side = side or front
        left = left or side
        back = back or side
        top = top or side
        x1, y1, z1 = x + dx, y + dy, z + dz
        s.poly([(x, y, z1), (x1, y, z1), (x1, y1, z1), (x, y1, z1)], top, outward=(0, 0, 1))
        s.poly([(x, y1, z), (x1, y1, z), (x1, y1, z1), (x, y1, z1)], front, outward=(0, 1, 0))
        s.poly([(x1, y, z), (x1, y1, z), (x1, y1, z1), (x1, y, z1)], side, outward=(1, 0, 0))
        s.poly([(x, y, z), (x1, y, z), (x1, y, z1), (x, y, z1)], back, outward=(0, -1, 0))
        s.poly([(x, y, z), (x, y1, z), (x, y1, z1), (x, y, z1)], left, outward=(-1, 0, 0))

    def block(self, x, y, z, dx, dy, dz, front, side=None, back=None, top=None, left=None, mass=True):
        """A building volume. Recorded as a mass (for construction / collapse); drawn per state."""
        if mass:
            self.masses.append((x, y, z, dx, dy, dz))
        self.top = max(self.top, z + dz)
        if self.stage in (1, 2):
            return False
        if self.state == "collapsed":
            if z > 0 or not mass:
                return False
            self._broken_shell(x, y, dx, dy, dz, front, side)
            return True
        self.box6(x, y, z, dx, dy, dz, front, side, back, top or self.m("roof_flat"), left)
        return True

    def _broken_shell(self, x, y, dx, dy, dz, front, side):
        """Collapsed building: jagged stubs of the outer walls (heights vary per segment)."""
        t = 0.045
        top = M.st(ik.mat("concrete", shade=0.5), "abandoned")
        hmax = min(dz, ST * 3.2)
        rng = Rng(self.seed + int(x * 131 + y * 71))

        def run(x0, y0, length, along_x, mat):
            n = max(1, int(length / 0.12))
            seg = length / n
            for i in range(n):
                hh = max(2.0, hmax * rng.uniform(0.15, 1.0) * (0.4 if rng.chance(0.25) else 1.0))
                if along_x:
                    self.box6(x0 + i * seg, y0, 0, seg, t, hh, mat, mat, mat, top)
                else:
                    self.box6(x0, y0 + i * seg, 0, t, seg, hh, mat, mat, mat, top)
        run(x, y + dy - t, dx, True, front)
        run(x + dx - t, y, dy - t, False, side)
        run(x, y, dx, True, side)
        run(x, y + t, dy - 2 * t, False, side)

    def solid(self, x, y, z, dx, dy, dz, mat, top=None):
        """Small non-mass volume (porch, chimney, sign...). Hidden during stages 1-2 and when collapsed."""
        if self.stage in (1, 2) or self.state == "collapsed":
            return
        m = self.m(mat) if isinstance(mat, str) else mat
        t = (self.m(top) if isinstance(top, str) else top) if top is not None else m
        self.box6(x, y, z, dx, dy, dz, m, m, m, t)

    def roofed(self):
        return self.stage in (0, 3) and self.state not in ("collapsed",)

    def gable(self, x, y, z, dx, dy, h, mat, axis="x", overhang=0.04, gable=None):
        if not self.roofed():
            return
        if self.state == "burnt":
            self._burnt_roof(x, y, z, dx, dy, h, axis)
            return
        self.s.roof_gable(x, y, z, dx, dy, h, self.m(mat), axis=axis, overhang=overhang,
                          gable=self.m(gable) if isinstance(gable, str) else gable)

    def hip(self, x, y, z, dx, dy, h, mat, overhang=0.04):
        if not self.roofed():
            return
        if self.state == "burnt":
            self._burnt_roof(x, y, z, dx, dy, h, "x" if dx >= dy else "y")
            return
        self.s.roof_hip(x, y, z, dx, dy, h, self.m(mat), overhang=overhang)

    def flat(self, x, y, z, dx, dy, mat="roof_flat", parapet=2, rim=None):
        if not self.roofed():
            return
        rim = rim if rim is not None else "concrete"
        self.s.roof_flat(x, y, z, dx, dy, self.m(mat), parapet=parapet,
                         rim=self.m(rim) if isinstance(rim, str) else rim)

    def mansard(self, x, y, z, dx, dy, h, inset, mat, top="roof_flat"):
        if not self.roofed():
            return
        if self.state == "burnt":
            return
        self.s.roof_mansard(x, y, z, dx, dy, h, inset, self.m(mat), top=self.m(top))

    def shed(self, x, y, z, dx, dy, h, mat, high="-y", wall=None):
        if not self.roofed():
            return
        self.s.roof_shed(x, y, z, dx, dy, h, self.m(mat), high=high,
                         wall=self.m(wall) if isinstance(wall, str) else wall)

    def _burnt_roof(self, x, y, z, dx, dy, h, axis):
        """Charred rafters over an open shell."""
        char = M.st(ik.mat("wood"), "burnt")
        n = 4
        for i in range(n):
            if axis == "x":
                xx = x + dx * (i + 0.5) / n
                self.s.roof_gable(xx - 0.02, y, z, 0.04, dy, h * 0.8, char, axis="x", overhang=0.0)
            else:
                yy = y + dy * (i + 0.5) / n
                self.s.roof_gable(x, yy - 0.02, z, dx, 0.04, h * 0.8, char, axis="y", overhang=0.0)
