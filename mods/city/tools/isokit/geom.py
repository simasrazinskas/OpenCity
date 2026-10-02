"""Projection, metric and face records.

World coords: x, y in cells (+X screen down-right, +Y screen down-left), z in screen px (up).
screen_x = (x - y) * 32, screen_y = (x + y) * 16 - z.
Metric space (for normals/lighting/UV lengths): (x * S, y * S, z), S = px-equivalent of one
cell length, so a 1-cell cube looks cubic (true 2:1 dimetric, camera elevation 30 deg).
"""
import numpy as np

TILE_W, TILE_H = 64, 32
S = 39.2                       # metric px per cell (1 cell cube is ~39 px tall)
STOREY = 10                    # px per storey (TTD-style exaggeration; fixed for the project)
VIEW = np.array([1.0, 1.0, 32.0])        # world-space direction toward the camera (screen-invariant)
VIEW_M = np.array([S, S, 32.0]) / np.linalg.norm([S, S, 32.0])   # metric, unit
LIGHT = np.array([-0.22, 0.46, 0.86])
LIGHT = LIGHT / np.linalg.norm(LIGHT)    # metric unit vector toward the light (upper left)


def project(x, y, z=0.0):
    """World -> screen (float, origin = world origin)."""
    return (np.asarray(x) - np.asarray(y)) * 32.0, (np.asarray(x) + np.asarray(y)) * 16.0 - np.asarray(z)


def to_metric(p):
    p = np.asarray(p, np.float64)
    return p * np.array([S, S, 1.0])


def unit(v):
    v = np.asarray(v, np.float64)
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


def newell(vm):
    """Polygon normal (unnormalized) via Newell's method, vm in metric coords."""
    n = np.zeros(3)
    for i in range(len(vm)):
        a, b = vm[i], vm[(i + 1) % len(vm)]
        n += np.array([(a[1] - b[1]) * (a[2] + b[2]), (a[2] - b[2]) * (a[0] + b[0]),
                       (a[0] - b[0]) * (a[1] + b[1])])
    return n


class Face:
    """A planar polygon with a material and a face-local UV frame.

    kind: 'wall' (vertical), 'top' (horizontal, facing up), 'roof' (sloped), 'ground' (decal), 'under'.
    nmode: None (flat) or a tuple for smooth normals: ('cyl', p_m, axis_m) | ('cone', apex_m, axis_m, k).
    u runs along the face horizontally (32 units per cell), v runs up (px). For tops u=x*32, v=y*32.
    """
    __slots__ = ("verts", "mat", "n", "kind", "o", "ua", "va", "nmode", "bias", "seed", "cull", "info")

    def __init__(self, verts, mat, outward=None, kind=None, nmode=None, bias=0.0, seed=0,
                 cull=True, info=None):
        self.verts = np.asarray(verts, np.float64)
        self.mat = mat
        nm = newell(to_metric(self.verts))
        if outward is not None and np.dot(nm, to_metric(outward)) < 0:
            nm = -nm
            self.verts = self.verts[::-1].copy()
        self.n = unit(nm)
        self.nmode, self.bias, self.seed, self.cull = nmode, bias, seed, cull
        self.info = dict(info or {})
        self.kind = kind or self._guess_kind()
        self._frame()

    def _guess_kind(self):
        nz = self.n[2]
        if nz > 0.999:
            return "top"
        if abs(nz) < 1e-3:
            return "wall"
        if nz < -0.999:
            return "under"
        return "roof"

    def _frame(self):
        v0 = self.verts[0]
        n = self.n
        if self.kind in ("top", "ground", "under"):
            self.o = np.array([0.0, 0.0, v0[2]])
            self.ua = np.array([32.0, 0.0, 0.0])
            self.va = np.array([0.0, 32.0, 0.0])
            return
        h = np.array([n[0], n[1], 0.0])
        if np.linalg.norm(h) < 1e-9:
            h = np.array([0.0, 1.0, 0.0])
        h = unit(h)
        t = np.array([h[1], -h[0], 0.0])          # horizontal, left-to-right seen from outside
        self.ua = t * 32.0
        up = unit(np.cross(n, t))                  # metric, up the face
        if up[2] < 0:
            up = -up
        self.va = up * np.array([S, S, 1.0])
        self.o = self.verts[np.argmin(self.verts[:, 2])].copy()
        uu = (self.verts - self.o) @ self.ua
        self.o = self.o + 0.0
        self.info.setdefault("u0", float(uu.min()))

    def uv_extent(self):
        d = self.verts - self.o
        u, v = d @ self.ua, d @ self.va
        return float(u.min()), float(u.max()), float(v.min()), float(v.max())

    def transformed(self, fn_p, fn_v):
        """Copy with points mapped by fn_p and directions by fn_v (used for facings)."""
        f = object.__new__(Face)
        f.verts = np.array([fn_p(p) for p in self.verts])
        f.mat, f.kind, f.bias, f.seed, f.cull = self.mat, self.kind, self.bias, self.seed, self.cull
        f.info = dict(self.info)
        f.n = unit(fn_v(self.n))
        f.o = fn_p(self.o)
        f.ua = fn_v(self.ua)
        f.va = fn_v(self.va)
        f.nmode = None
        if self.nmode is not None:
            m = list(self.nmode)
            m[1] = fn_p(np.asarray(m[1]) / np.array([S, S, 1.0])) * np.array([S, S, 1.0])
            m[2] = fn_v(np.asarray(m[2]))
            f.nmode = tuple(m)
        return f


class Ellipsoid:
    """Analytic ellipsoid (trees, bushes, domes). c in world coords, radii rx, ry in cells, rz in px."""
    __slots__ = ("c", "r", "mat", "seed", "kind", "bias", "info", "zmin")

    def __init__(self, c, r, mat, seed=0, kind="curved", bias=0.0, zmin=None, info=None):
        self.c = np.asarray(c, np.float64)
        self.r = np.asarray(r, np.float64)
        self.mat, self.seed, self.kind, self.bias = mat, seed, kind, bias
        self.zmin = zmin             # clip below this z (domes)
        self.info = dict(info or {})

    def transformed(self, fn_p, fn_v):
        e = Ellipsoid(fn_p(self.c), self.r, self.mat, self.seed, self.kind, self.bias, self.zmin, self.info)
        d = np.abs(fn_v(np.array([1.0, 0.0, 0.0])))
        if d[1] > 0.5:
            e.r = self.r[[1, 0, 2]].copy()
        return e


def rotation(k, footprint):
    """Point/vector maps rotating a model by k*90 deg around its footprint (front +Y -> +X -> -Y -> -X).

    Returns (fn_p, fn_v, new_footprint).
    """
    k %= 4
    fx, fy = footprint
    c = np.array([fx / 2.0, fy / 2.0, 0.0])
    nf = (fy, fx) if k % 2 else (fx, fy)
    c2 = np.array([nf[0] / 2.0, nf[1] / 2.0, 0.0])
    # rotate (x, y) clockwise in xy so +Y -> +X : (x, y) -> (y, -x)
    mats = [np.eye(3),
            np.array([[0, 1, 0], [-1, 0, 0], [0, 0, 1.0]]),
            np.array([[-1, 0, 0], [0, -1, 0], [0, 0, 1.0]]),
            np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1.0]])]
    R = mats[k]

    def fn_p(p):
        return R @ (np.asarray(p, np.float64) - c) + c2

    def fn_v(v):
        return R @ np.asarray(v, np.float64)
    return fn_p, fn_v, nf


def yaw_rotation(yaw_deg, footprint):
    """Point/vector maps rotating by yaw degrees about the footprint centre, in the same sense as
    facings (yaw = 90 * facing: front +Y -> +X -> -Y -> -X). Returns (fn_p, fn_v)."""
    t = -np.radians(yaw_deg)
    c, s = np.cos(t), np.sin(t)
    R = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1.0]])
    ctr = np.array([footprint[0] / 2.0, footprint[1] / 2.0, 0.0])

    def fn_p(p):
        return R @ (np.asarray(p, np.float64) - ctr) + ctr

    def fn_v(v):
        return R @ np.asarray(v, np.float64)
    return fn_p, fn_v
