"""
uidraw - anti-aliased, resolution independent drawing helpers for the OpenCity UI art scripts.

All coordinates handed to `G` are in *design units* (1x pixels, floats allowed). The G object
multiplies them by its scale `s`, so the same drawing code renders crisp 1x/2x/3x images.
Pure python 3 stdlib on top of pngkit.Canvas.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngkit import Canvas, rgba  # noqa: E402


def _cov(d):
    """Pixel coverage from a signed distance (in pixels, negative = inside)."""
    v = 0.5 - d
    return 0.0 if v < 0 else (1.0 if v > 1 else v)


def col(c, a=None):
    c = rgba(c)
    return c if a is None else (c[0], c[1], c[2], a)


class G:
    """A canvas plus a design-unit scale."""

    def __init__(self, w, h, s=1, fill=(0, 0, 0, 0)):
        self.s = s
        self.dw, self.dh = w, h
        self.cv = Canvas(int(round(w * s)), int(round(h * s)), fill)

    # ---- internal -------------------------------------------------
    def _paint(self, x, y, c, a):
        if a <= 0.003:
            return
        c = rgba(c)
        al = int(round(c[3] * a))
        if al > 0:
            self.cv.blend(x, y, (c[0], c[1], c[2], al))

    def _bbox(self, x0, y0, x1, y1, pad=2):
        s = self.s
        return (max(0, int(math.floor(x0 * s)) - pad), max(0, int(math.floor(y0 * s)) - pad),
                min(self.cv.w, int(math.ceil(x1 * s)) + pad), min(self.cv.h, int(math.ceil(y1 * s)) + pad))

    def _shape(self, bbox, sdf, fill, stroke, sw, clip=None):
        """Render an sdf (pixel coords, negative inside) with optional fill / inner stroke."""
        s = self.s
        swp = sw * s
        bx0, by0, bx1, by1 = bbox
        fill_fn = fill if callable(fill) else None
        for py in range(by0, by1):
            for px in range(bx0, bx1):
                d = sdf(px + 0.5, py + 0.5)
                if d > 0.5:
                    continue
                ao = _cov(d)
                if clip is not None:
                    ao *= clip(px, py)
                    if ao <= 0:
                        continue
                if stroke is not None and swp > 0:
                    ai = _cov(d + swp)
                    ring = ao - min(ao, ai)
                    if fill is not None and ai > 0:
                        fc = fill_fn(px / s, py / s) if fill_fn else fill
                        self._paint(px, py, fc, min(ai, ao))
                    if ring > 0:
                        sc = stroke(px / s, py / s) if callable(stroke) else stroke
                        self._paint(px, py, sc, ring)
                elif fill is not None:
                    fc = fill_fn(px / s, py / s) if fill_fn else fill
                    self._paint(px, py, fc, ao)

    # ---- shapes ---------------------------------------------------
    def rrect(self, x, y, w, h, r, fill=None, stroke=None, sw=1, rr=None):
        """Rounded rect. rr = (tl, tr, br, bl) radii overrides r."""
        s = self.s
        cx, cy = (x + w / 2) * s, (y + h / 2) * s
        hw, hh = w * s / 2, h * s / 2
        radii = rr if rr else (r, r, r, r)

        def sdf(px, py):
            dx, dy = px - cx, py - cy
            if rr:
                rad = radii[0] if (dx < 0 and dy < 0) else radii[1] if (dx >= 0 and dy < 0) else \
                    radii[2] if (dx >= 0 and dy >= 0) else radii[3]
                rad = min(rad * s, hw, hh)
            else:
                rad = min(r * s, hw, hh)
            qx = abs(dx) - hw + rad
            qy = abs(dy) - hh + rad
            return math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - rad

        self._shape(self._bbox(x, y, x + w, y + h), sdf, fill, stroke, sw)

    def circle(self, cx, cy, r, fill=None, stroke=None, sw=1):
        s = self.s
        ccx, ccy, rr = cx * s, cy * s, r * s

        def sdf(px, py):
            return math.hypot(px - ccx, py - ccy) - rr

        self._shape(self._bbox(cx - r, cy - r, cx + r, cy + r), sdf, fill, stroke, sw)

    def ellipse(self, cx, cy, rx, ry, fill):
        s = self.s
        ccx, ccy, rrx, rry = cx * s, cy * s, rx * s, ry * s

        def sdf(px, py):
            k = math.hypot((px - ccx) / rrx, (py - ccy) / rry)
            return (k - 1) * min(rrx, rry)

        self._shape(self._bbox(cx - rx, cy - ry, cx + rx, cy + ry), sdf, fill, None, 0)

    def ring(self, cx, cy, r, w, c):
        """Stroke-only circle of mid-radius r and thickness w."""
        s = self.s
        ccx, ccy, rr, hw = cx * s, cy * s, r * s, w * s / 2

        def sdf(px, py):
            return abs(math.hypot(px - ccx, py - ccy) - rr) - hw

        self._shape(self._bbox(cx - r - w, cy - r - w, cx + r + w, cy + r + w), sdf, c, None, 0)

    def line(self, x0, y0, x1, y1, w, c, cap=True):
        s = self.s
        ax, ay, bx, by = x0 * s, y0 * s, x1 * s, y1 * s
        hw = w * s / 2
        dx, dy = bx - ax, by - ay
        ll = dx * dx + dy * dy

        def sdf(px, py):
            if ll == 0:
                return math.hypot(px - ax, py - ay) - hw
            t = ((px - ax) * dx + (py - ay) * dy) / ll
            if cap:
                t = 0 if t < 0 else 1 if t > 1 else t
            else:
                if t < 0 or t > 1:
                    # butt caps: distance to the cap line
                    t2 = 0 if t < 0 else 1
                    return math.hypot(px - (ax + dx * t2), py - (ay + dy * t2)) + 1
            return math.hypot(px - (ax + dx * t), py - (ay + dy * t)) - hw

        self._shape(self._bbox(min(x0, x1) - w, min(y0, y1) - w, max(x0, x1) + w, max(y0, y1) + w), sdf, c, None, 0)

    def polyline(self, pts, w, c, closed=False):
        n = len(pts)
        for i in range(n - 1 + (1 if closed else 0)):
            a, b = pts[i], pts[(i + 1) % n]
            self.line(a[0], a[1], b[0], b[1], w, c)

    def arc(self, cx, cy, r, a0, a1, w, c):
        """Arc from angle a0 to a1 (degrees, 0 = +x, clockwise on screen) as chained capsules."""
        steps = max(6, int(abs(a1 - a0) / 8))
        pts = []
        for i in range(steps + 1):
            a = math.radians(a0 + (a1 - a0) * i / steps)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        self.polyline(pts, w, c)

    def poly(self, pts, c):
        """Anti-aliased polygon fill (4 vertical subsamples, exact horizontal coverage)."""
        s = self.s
        P = [(x * s, y * s) for x, y in pts]
        n = len(P)
        ys = [p[1] for p in P]
        y_lo, y_hi = max(0, int(math.floor(min(ys)))), min(self.cv.h, int(math.ceil(max(ys))))
        SUB = 4
        for y in range(y_lo, y_hi):
            acc = {}
            for k in range(SUB):
                sy = y + (k + 0.5) / SUB
                xs = []
                for i in range(n):
                    (x0, y0), (x1, y1) = P[i], P[(i + 1) % n]
                    if (y0 <= sy < y1) or (y1 <= sy < y0):
                        xs.append(x0 + (sy - y0) * (x1 - x0) / (y1 - y0))
                xs.sort()
                for i in range(0, len(xs) - 1, 2):
                    a, b = xs[i], xs[i + 1]
                    xa, xb = int(math.floor(a)), int(math.floor(b))
                    if xa == xb:
                        acc[xa] = acc.get(xa, 0) + (b - a) / SUB
                    else:
                        acc[xa] = acc.get(xa, 0) + (xa + 1 - a) / SUB
                        for xx in range(xa + 1, xb):
                            acc[xx] = acc.get(xx, 0) + 1.0 / SUB
                        acc[xb] = acc.get(xb, 0) + (b - xb) / SUB
            for x, a in acc.items():
                if 0 <= x < self.cv.w:
                    self._paint(x, y, c, min(1.0, a))

    def rect(self, x, y, w, h, c):
        """Sharp-ish axis aligned rect (rounded to device pixels)."""
        s = self.s
        self.cv.rect(int(round(x * s)), int(round(y * s)), max(1, int(round(w * s))), max(1, int(round(h * s))), c)

    # ---- composition ----------------------------------------------
    def blit(self, other, x, y):
        self.cv.blit(other.cv, int(round(x * self.s)), int(round(y * self.s)))


def pot(n):
    p = 1
    while p < n:
        p *= 2
    return p


def pad_pot(cv):
    """OpenRA textures must be power-of-two sized: pad the canvas to the next PoT (transparent)."""
    w, h = pot(cv.w), pot(cv.h)
    if (w, h) == (cv.w, cv.h):
        return cv
    out = Canvas(w, h)
    out.blit(cv, 0, 0, blend=False)
    return out


# ---- atlas -------------------------------------------------------------

class Atlas:
    """Collects named design-unit drawings, packs them once, renders at any scale."""

    def __init__(self, width, pad=2):
        self.width = width
        self.pad = pad
        self.items = []   # (name, w, h, fn)

    def add(self, name, w, h, fn):
        self.items.append((name, w, h, fn))

    def layout(self):
        pad = self.pad
        x = y = pad
        shelf = 0
        placed = {}
        for name, w, h, fn in self.items:
            if x + w + pad > self.width:
                x = pad
                y += shelf + pad
                shelf = 0
            placed[name] = (x, y, w, h)
            x += w + pad
            shelf = max(shelf, h)
        self.height = y + shelf + pad
        return placed

    def render(self, s):
        placed = self.layout()
        atlas = Canvas(self.width * s, self.height * s)
        for name, w, h, fn in self.items:
            g = G(w, h, s)
            fn(g)
            x, y, _, _ = placed[name]
            atlas.blit(g.cv, x * s, y * s, blend=False)
        return pad_pot(atlas), placed
