"""
iso_ui_icon_dsl - tiny vector-to-pixel DSL for the RCT2-style OpenCity icon set.

Icons are authored once on a 16-unit grid and rasterized at any size (16, 24, 32 px...) by sampling
pixel centres, then get a 1 px dark outline at native resolution (RCT2 look: saturated colours, dark
outline, light from the upper left). Register icons with @icon("name", "group", "Label").

    @icon("power", "services", "Electricity")
    def _(I):
        I.isobox(8, 14, 5, 5, 6, "grey")
        I.poly([(7, 2), (10, 2), (8, 6)], "yellow", 6)

Shapes (all coordinates in 16-grid units, floats fine; colour = ramp name + shade 0..7, or "#hex"):
    rect(x, y, w, h, c, s)        ellipse(cx, cy, rx, ry, c, s)    poly(pts, c, s)
    line(x0, y0, x1, y1, c, s, t=1)   ring(cx, cy, r, t, c, s)     erase(...) variants via c=None
    isobox(fx, fy, lx, ly, h, ramp, top=6, left=5, right=3)  # fx, fy = front ground corner on screen
    isotile(fx, fy, lx, ly, ramp, shade=5, edge=1, edge_ramp="brown")  # flat tile with soil edge
    gable(fx, fy, lx, ly, z, rh, ramp, along="x")     text(x, y, s, c, sh)   pixel(x, y, c, s)
Outline: I.outline_color can be changed; I.no_outline = True disables it (for tiny badges).
"""

import numpy as np

from iso_ui_kit import Canvas, R, RAMPS, hexc, col
import iso_ui_font as F

OUTLINE = hexc("#120c06")
REG = {}       # name -> (group, label, fn)
ORDER = []


def icon(name, group, label):
    def deco(fn):
        if name not in REG:
            ORDER.append(name)
        REG[name] = (group, label, fn)
        return fn
    return deco


def C(c, s=5):
    if c is None:
        return None
    if isinstance(c, tuple):
        return col(c)
    if c.startswith("#"):
        return hexc(c)
    return R(c, s)


class Ico:
    def __init__(self, n):
        self.n = n
        self.k = n / 16.0
        self.rgba = np.zeros((n, n, 4), np.uint8)
        ys, xs = np.mgrid[0:n, 0:n]
        self.X = (xs + 0.5) / self.k
        self.Y = (ys + 0.5) / self.k
        self.outline_color = OUTLINE
        self.no_outline = False
        self.extra = []  # (mask, colour) drawn after the outline pass

    def _paint(self, m, c, s, after=False):
        cc = C(c, s)
        if after:
            self.extra.append((m, cc))
            return
        if cc is None:
            self.rgba[m] = 0
        else:
            self.rgba[m] = cc

    def rect(self, x, y, w, h, c, s=5, after=False):
        m = (self.X >= x) & (self.X < x + w) & (self.Y >= y) & (self.Y < y + h)
        self._paint(m, c, s, after)

    def ellipse(self, cx, cy, rx, ry, c, s=5, after=False):
        m = ((self.X - cx) / rx) ** 2 + ((self.Y - cy) / ry) ** 2 <= 1.0
        self._paint(m, c, s, after)

    def ring(self, cx, cy, r, t, c, s=5, after=False):
        d = np.sqrt((self.X - cx) ** 2 + (self.Y - cy) ** 2)
        self._paint((d <= r) & (d >= r - t), c, s, after)

    def poly(self, pts, c, s=5, after=False):
        X, Y = self.X, self.Y
        inside = np.zeros(X.shape, bool)
        n = len(pts)
        for i in range(n):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % n]
            if y0 == y1:
                continue
            cond = (Y >= min(y0, y1)) & (Y < max(y0, y1))
            xi = x0 + (Y - y0) * (x1 - x0) / (y1 - y0)
            inside ^= cond & (X < xi)
        self._paint(inside, c, s, after)

    def line(self, x0, y0, x1, y1, c, s=5, t=1.0, after=False):
        X, Y = self.X, self.Y
        dx, dy = x1 - x0, y1 - y0
        L2 = dx * dx + dy * dy or 1e-9
        u = np.clip(((X - x0) * dx + (Y - y0) * dy) / L2, 0, 1)
        d = np.sqrt((X - x0 - u * dx) ** 2 + (Y - y0 - u * dy) ** 2)
        self._paint(d <= t / 2.0 + 0.02, c, s, after)

    def pixel(self, x, y, c, s=5, after=False):
        self.rect(x, y, 1, 1, c, s, after)

    # ---- isometric helpers (2:1, light from upper left: +Y faces lit, +X faces shaded) ----------------
    @staticmethod
    def P(fx, fy, x, y, z):
        """World offset (x, y <= 0 behind the front corner, z up) to 16-grid screen point."""
        return (fx + (x - y), fy + (x + y) * 0.5 - z)

    def isobox(self, fx, fy, lx, ly, h, ramp, top=6, left=5, right=3, after=False):
        P = lambda x, y, z: self.P(fx, fy, x, y, z)
        # left face (+Y side, lit): y = 0, x from -lx..0
        self.poly([P(-lx, 0, 0), P(0, 0, 0), P(0, 0, h), P(-lx, 0, h)], ramp, left, after)
        # right face (+X side, shaded): x = 0, y from -ly..0
        self.poly([P(0, 0, 0), P(0, -ly, 0), P(0, -ly, h), P(0, 0, h)], ramp, right, after)
        self.poly([P(0, 0, h), P(-lx, 0, h), P(-lx, -ly, h), P(0, -ly, h)], ramp, top, after)

    def isotile(self, fx, fy, lx, ly, ramp, shade=5, edge=1.0, edge_ramp="brown"):
        self.isobox(fx, fy, lx, ly, edge, edge_ramp, top=4, left=4, right=2)
        P = lambda x, y, z: self.P(fx, fy, x, y, z)
        self.poly([P(0, 0, edge), P(-lx, 0, edge), P(-lx, -ly, edge), P(0, -ly, edge)], ramp, shade)

    def isoquad(self, fx, fy, pts3, c, s=5, after=False):
        self.poly([self.P(fx, fy, *p) for p in pts3], c, s, after)

    def gable(self, fx, fy, lx, ly, z, rh, ramp, along="x", lit=6, dark=3, gable_ramp=None, gs=5):
        """Gable roof on a box top at height z; ridge runs along world x (or y)."""
        q = lambda pts, c, s: self.isoquad(fx, fy, pts, c, s)
        g = gable_ramp or ramp
        if along == "x":
            m = -ly / 2.0
            q([(0, 0, z), (-lx, 0, z), (-lx, m, z + rh), (0, m, z + rh)], ramp, lit)        # front slope (+Y)
            q([(0, 0, z), (0, m, z + rh), (0, -ly, z)], g, gs)                              # gable end (+X)
        else:
            m = -lx / 2.0
            q([(0, 0, z), (0, -ly, z), (m, -ly, z + rh), (m, 0, z + rh)], ramp, dark)       # right slope (+X)
            q([(0, 0, z), (m, 0, z + rh), (-lx, 0, z)], g, gs + 1)                          # gable end (+Y)

    def text(self, x, y, s, c, sh=7, face="8"):
        """Pixel-font text placed at 16-grid (x, y) = cap top-left; drawn after the outline."""
        m, asc = F.render_mask(s, face)
        top = asc - F.cap_height(face)
        m = m[top:top + F.cap_height(face) + 2]
        px, py = int(round(x * self.k)), int(round(y * self.k))
        h, w = m.shape
        full = np.zeros((self.n, self.n), bool)
        ys0, xs0 = max(0, py), max(0, px)
        ys1, xs1 = min(self.n, py + h), min(self.n, px + w)
        if ys1 > ys0 and xs1 > xs0:
            full[ys0:ys1, xs0:xs1] = m[ys0 - py:ys1 - py, xs0 - px:xs1 - px]
        self._paint(full, c, sh)

    # ---- finish ----------------------------------------------------------------------------------------
    def canvas(self):
        a = self.rgba
        if not self.no_outline:
            m = a[:, :, 3] > 0
            nb = np.zeros_like(m)
            nb[1:, :] |= m[:-1, :]
            nb[:-1, :] |= m[1:, :]
            nb[:, 1:] |= m[:, :-1]
            nb[:, :-1] |= m[:, 1:]
            a[nb & ~m] = self.outline_color
        for m, cc in self.extra:
            a[m] = cc if cc is not None else 0
        cv = Canvas(self.n, self.n)
        cv.a = a
        return cv


_cache = {}


def get(name, n=16):
    key = (name, n)
    if key not in _cache:
        I = Ico(n)
        REG[name][2](I)
        _cache[key] = I.canvas()
    return _cache[key]


def sheet(names, n=16, cols=8, pad=4, bg=None, labels=False):
    rows = (len(names) + cols - 1) // cols
    cw = n + pad
    c = Canvas(cols * cw + pad, rows * cw + pad, bg or (0, 0, 0, 0))
    for i, nm in enumerate(names):
        c.blit(get(nm, n), pad + (i % cols) * cw, pad + (i // cols) * cw)
    return c
