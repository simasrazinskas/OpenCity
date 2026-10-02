"""Roof primitives, attached to Scene as methods."""
from .scene import Scene, _mat


def roof_gable(self, x, y, z, dx, dy, h, mat, axis="x", overhang=0.06, gable=None, ends=True):
    """Gable roof over the rect (x, y, dx, dy) whose eaves sit at z, ridge h px higher.
    axis: ridge direction 'x' or 'y'. gable: material for the end triangles (default: roof mat)."""
    m, g = _mat(mat), _mat(gable) or _mat(mat)
    o = overhang
    x1, y1 = x + dx, y + dy
    if axis == "x":
        ym, half = y + dy / 2, dy / 2
        ze = z - h * o / half
        self.poly([(x - o, y - o, ze), (x1 + o, y - o, ze), (x1 + o, ym, z + h), (x - o, ym, z + h)], m,
                  outward=(0, -1, 1), kind="roof")
        self.poly([(x - o, y1 + o, ze), (x1 + o, y1 + o, ze), (x1 + o, ym, z + h), (x - o, ym, z + h)], m,
                  outward=(0, 1, 1), kind="roof")
        if ends:
            self.poly([(x1, y, z), (x1, y1, z), (x1, ym, z + h)], g, outward=(1, 0, 0))
            self.poly([(x, y, z), (x, y1, z), (x, ym, z + h)], g, outward=(-1, 0, 0))
    else:
        xm, half = x + dx / 2, dx / 2
        ze = z - h * o / half
        self.poly([(x - o, y - o, ze), (x - o, y1 + o, ze), (xm, y1 + o, z + h), (xm, y - o, z + h)], m,
                  outward=(-1, 0, 1), kind="roof")
        self.poly([(x1 + o, y - o, ze), (x1 + o, y1 + o, ze), (xm, y1 + o, z + h), (xm, y - o, z + h)], m,
                  outward=(1, 0, 1), kind="roof")
        if ends:
            self.poly([(x, y1, z), (x1, y1, z), (xm, y1, z + h)], g, outward=(0, 1, 0))
            self.poly([(x, y, z), (x1, y, z), (xm, y, z + h)], g, outward=(0, -1, 0))
    return self


def roof_hip(self, x, y, z, dx, dy, h, mat, overhang=0.06):
    """Hip roof: four slopes; ridge along the longer side (a pyramid if square)."""
    m, o = _mat(mat), overhang
    x0, y0, x1, y1 = x - o, y - o, x + dx + o, y + dy + o
    w, d = x1 - x0, y1 - y0
    ze = z - h * o / (min(dx, dy) / 2)
    zt = z + h
    if w >= d:
        ym, a = (y0 + y1) / 2, d / 2
        r0, r1 = (x0 + a, ym), (x1 - a, ym)
    else:
        xm, a = (x0 + x1) / 2, w / 2
        r0, r1 = (xm, y0 + a), (xm, y1 - a)
    A, B, C, D = (x0, y0, ze), (x1, y0, ze), (x1, y1, ze), (x0, y1, ze)
    R0, R1 = (r0[0], r0[1], zt), (r1[0], r1[1], zt)
    if w >= d:
        self.poly([A, B, R1, R0], m, outward=(0, -1, 1), kind="roof")
        self.poly([D, C, R1, R0], m, outward=(0, 1, 1), kind="roof")
        self.poly([B, C, R1], m, outward=(1, 0, 1), kind="roof")
        self.poly([A, D, R0], m, outward=(-1, 0, 1), kind="roof")
    else:
        self.poly([A, D, R1, R0], m, outward=(-1, 0, 1), kind="roof")
        self.poly([B, C, R1, R0], m, outward=(1, 0, 1), kind="roof")
        self.poly([D, C, R1], m, outward=(0, 1, 1), kind="roof")
        self.poly([A, B, R0], m, outward=(0, -1, 1), kind="roof")
    return self


def pyramid(self, x, y, z, dx, dy, h, mat):
    """Pyramid with apex over the rect centre."""
    m = _mat(mat)
    ap = (x + dx / 2, y + dy / 2, z + h)
    A, B, C, D = (x, y, z), (x + dx, y, z), (x + dx, y + dy, z), (x, y + dy, z)
    for p, q, o in ((A, B, (0, -1, 1)), (B, C, (1, 0, 1)), (C, D, (0, 1, 1)), (D, A, (-1, 0, 1))):
        self.poly([p, q, ap], m, outward=o, kind="roof")
    return self


def roof_shed(self, x, y, z, dx, dy, h, mat, high="-y", wall=None, overhang=0.04):
    """Single-slope roof rising h px toward side `high` ('-y', '+y', '-x', '+x')."""
    m, wm = _mat(mat), _mat(wall) or _mat(mat)
    x1, y1, o = x + dx, y + dy, overhang
    if high in ("-y", "+y"):
        lo_y, hi_y = (y1, y) if high == "-y" else (y, y1)
        sg = 1 if high == "-y" else -1
        self.poly([(x - o, lo_y + sg * o, z), (x1 + o, lo_y + sg * o, z), (x1 + o, hi_y, z + h),
                   (x - o, hi_y, z + h)], m, outward=(0, sg, 1), kind="roof")
        self.poly([(x1, y, z), (x1, y1, z), (x1, hi_y, z + h)], wm, outward=(1, 0, 0))
        self.poly([(x, y, z), (x, y1, z), (x, hi_y, z + h)], wm, outward=(-1, 0, 0))
        self.poly([(x, hi_y, z), (x1, hi_y, z), (x1, hi_y, z + h), (x, hi_y, z + h)], wm, outward=(0, -sg, 0))
    else:
        lo_x, hi_x = (x1, x) if high == "-x" else (x, x1)
        sg = 1 if high == "-x" else -1
        self.poly([(lo_x + sg * o, y - o, z), (lo_x + sg * o, y1 + o, z), (hi_x, y1 + o, z + h),
                   (hi_x, y - o, z + h)], m, outward=(sg, 0, 1), kind="roof")
        self.poly([(x, y1, z), (x1, y1, z), (hi_x, y1, z + h)], wm, outward=(0, 1, 0))
        self.poly([(x, y, z), (x1, y, z), (hi_x, y, z + h)], wm, outward=(0, -1, 0))
        self.poly([(hi_x, y, z), (hi_x, y1, z), (hi_x, y1, z + h), (hi_x, y, z + h)], wm, outward=(-sg, 0, 0))
    return self


def roof_flat(self, x, y, z, dx, dy, mat, parapet=2, rim=None, rim_w=0.05):
    """Flat roof surface at z with an optional parapet (height px) of material rim."""
    m, r = _mat(mat), _mat(rim) or _mat(mat)
    self.poly([(x, y, z), (x + dx, y, z), (x + dx, y + dy, z), (x, y + dy, z)], m, outward=(0, 0, 1),
              bias=1e-5)
    if parapet:
        w = rim_w
        self.box(x, y, z, dx, w, parapet, r)
        self.box(x, y + dy - w, z, dx, w, parapet, r)
        self.box(x, y + w, z, w, dy - 2 * w, parapet, r)
        self.box(x + dx - w, y + w, z, w, dy - 2 * w, parapet, r)
    return self


def roof_mansard(self, x, y, z, dx, dy, h, inset, mat, top=None):
    """Mansard / frustum: steep slopes from the rect at z up to a rect inset by `inset` cells at z+h,
    closed by a flat top (material top)."""
    m, t = _mat(mat), _mat(top) or _mat(mat)
    i = inset
    A, B, C, D = (x, y, z), (x + dx, y, z), (x + dx, y + dy, z), (x, y + dy, z)
    a, b, c, d = ((x + i, y + i, z + h), (x + dx - i, y + i, z + h), (x + dx - i, y + dy - i, z + h),
                  (x + i, y + dy - i, z + h))
    self.poly([A, B, b, a], m, outward=(0, -1, 1), kind="roof")
    self.poly([B, C, c, b], m, outward=(1, 0, 1), kind="roof")
    self.poly([C, D, d, c], m, outward=(0, 1, 1), kind="roof")
    self.poly([D, A, a, d], m, outward=(-1, 0, 1), kind="roof")
    self.poly([a, b, c, d], t, outward=(0, 0, 1))
    return self


for _f in (roof_gable, roof_hip, pyramid, roof_shed, roof_flat, roof_mansard):
    setattr(Scene, _f.__name__, _f)
