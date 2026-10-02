"""Organic primitives: limbs (thin tapered rods, never thinner than 1 px) and faceted rocks."""
import math

import numpy as np

from .geom import S, VIEW_M, unit
from .scene import Scene, _mat
from .noise import Rng


def limb(self, p0, p1, r0, r1=None, mat="bark", min_px=1.1):
    """Tapered rod from p0 to p1 (world coords), radii in cells. Drawn as a camera-facing band with
    cylindrical normals, so it shades like a round branch and stays at least `min_px` wide."""
    r1 = r0 if r1 is None else r1
    a = np.asarray(p0, np.float64)
    b = np.asarray(p1, np.float64)
    sc = np.array([S, S, 1.0])
    ax = unit((b - a) * sc)
    side = np.cross(ax, VIEW_M)
    if np.linalg.norm(side) < 1e-6:
        side = np.array([1.0, -1.0, 0.0])
    side = unit(side)
    # screen-space width of a metric unit along `side`: side_world -> screen
    sw = side / sc
    scr = np.array([(sw[0] - sw[1]) * 32.0, (sw[0] + sw[1]) * 16.0 - sw[2]])
    px_per_m = max(np.linalg.norm(scr), 1e-6)
    min_m = (min_px / 2.0) / px_per_m
    w0 = max(r0 * S, min_m)
    w1 = max(r1 * S, min_m)
    q = [a - side * w0 / sc, b - side * w1 / sc, b + side * w1 / sc, a + side * w0 / sc]
    return self.poly(q, mat, outward=VIEW_M / sc, kind="curved", cull=False,
                     nmode=("cyl", a * sc, ax))


def rock(self, cx, cy, z, r, h, mat="rock", seed=0, segs=7, rings=3, jitter=0.22):
    """Faceted boulder: a jittered half-ellipsoid mesh (r in cells, h in px), flat-shaded facets."""
    rng = Rng(seed)
    pts = []
    for i in range(rings):
        el = (i / rings) * (math.pi / 2)
        ring = []
        for k in range(segs):
            az = 2 * math.pi * (k + 0.5 * (i % 2)) / segs + rng.uniform(-0.2, 0.2)
            j = 1 + rng.uniform(-jitter, jitter)
            ring.append((cx + r * math.cos(el) * math.cos(az) * j, cy + r * math.cos(el) * math.sin(az) * j,
                         z + h * math.sin(el) * (1 + rng.uniform(-jitter, jitter) * 0.5)))
        pts.append(ring)
    top = (cx + rng.uniform(-0.2, 0.2) * r, cy + rng.uniform(-0.2, 0.2) * r, z + h)
    m = _mat(mat)
    c = np.array([cx, cy, z + h * 0.3])
    for i in range(rings):
        for k in range(segs):
            a, b = pts[i][k], pts[i][(k + 1) % segs]
            if i + 1 < rings:
                cc, d = pts[i + 1][(k + 1) % segs], pts[i + 1][k]
                for tri in ((a, b, cc), (a, cc, d)):
                    mid = np.mean(tri, 0)
                    self.poly(list(tri), m, outward=(mid - c) * np.array([S, S, 1.0]) / np.array([S, S, 1.0]))
            else:
                mid = np.mean([a, b, top], 0)
                self.poly([a, b, top], m, outward=mid - c)
    return self


def heightfield(self, x0, y0, dx, dy, zfn, mat, n=16, steep=None, steep_at=0.6, kind=None):
    """Triangulated terrain patch over [x0, x0+dx] x [y0, y0+dy]; zfn(x, y) -> z px (numpy arrays).
    Triangles whose normal z < steep_at use material `steep` (banks, cliffs) if given."""
    xs = np.linspace(x0, x0 + dx, n + 1)
    ys = np.linspace(y0, y0 + dy, n + 1)
    X, Y = np.meshgrid(xs, ys, indexing="ij")
    Z = np.asarray(zfn(X, Y), np.float64) * np.ones_like(X)
    m, st = _mat(mat), _mat(steep) if steep is not None else None
    sc = np.array([S, S, 1.0])
    for i in range(n):
        for j in range(n):
            p = [np.array([X[a, b], Y[a, b], Z[a, b]]) for a, b in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
            for tri in ((p[0], p[1], p[2]), (p[0], p[2], p[3])):
                nm = np.cross((tri[1] - tri[0]) * sc, (tri[2] - tri[0]) * sc)
                nz = abs(nm[2]) / max(np.linalg.norm(nm), 1e-9)
                use = st if (st is not None and nz < steep_at) else m
                flat = nz > 0.9999
                self.poly(list(tri), use, outward=(0, 0, 1), kind=("ground" if flat else kind))
    return self


Scene.limb = limb
Scene.rock = rock
Scene.heightfield = heightfield
