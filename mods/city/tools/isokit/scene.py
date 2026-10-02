"""Scene: a list of 3D primitives in world units (x, y in cells, z in px)."""
import math

import numpy as np

from .geom import Face, Ellipsoid, S, rotation, yaw_rotation
from .materials import mat as _m


def _mat(m):
    return None if m is None else _m(m)


class Scene:
    def __init__(self, footprint=(1, 1), seed=0):
        self.footprint = tuple(footprint)
        self.seed = seed
        self._items = []
        self._n = 0

    # ------------------------------------------------------------- core
    def add(self, item):
        self._items.append(item)
        return item

    def _seed(self):
        self._n += 1
        return self.seed * 7919 + self._n

    def poly(self, verts, mat, outward=None, kind=None, cull=True, bias=0.0, nmode=None, info=None):
        """Arbitrary planar polygon (3+ world-space verts). `outward`: a vector the normal should face."""
        return self.add(Face(verts, _mat(mat), outward, kind, nmode, bias, self._seed(), cull, info))

    def quad(self, a, b, c, d, mat, outward=None, **kw):
        return self.poly([a, b, c, d], mat, outward, **kw)

    def tri(self, a, b, c, mat, outward=None, **kw):
        return self.poly([a, b, c], mat, outward, **kw)

    def items(self, facing=0, yaw=None):
        """Primitives transformed for a facing (0..3) and the resulting footprint. yaw (degrees,
        same sense as facings: yaw = 90 * facing) rotates by any angle about the footprint centre
        (8-direction vehicles: yaw = 45 * k; the footprint is kept)."""
        if yaw is not None:
            fp, fv = yaw_rotation(yaw, self.footprint)
            return [it.transformed(fp, fv) for it in self._items], self.footprint
        if facing % 4 == 0:
            return list(self._items), self.footprint
        fp, fv, nf = rotation(facing, self.footprint)
        return [it.transformed(fp, fv) for it in self._items], nf

    def merge(self, other, dx=0.0, dy=0.0, dz=0.0):
        """Append another scene's primitives, translated."""
        off = np.array([dx, dy, dz])
        for it in other._items:
            self.add(it.transformed(lambda p: np.asarray(p) + off, lambda v: np.asarray(v)))
        return self

    # ------------------------------------------------------------- solids
    def box(self, x, y, z, dx, dy, dz, mat, top=None, left=None, right=None, back=True, back_left=None,
            back_right=None):
        """Axis-aligned box. mat: walls; top: roof face (default mat). Overrides are per model face and
        rotate with the model in other facings: left = +Y face, right = +X face, back_left = -X face,
        back_right = -Y face (back faces default to mat). back=False skips the faces never visible in
        facing 0 (saves time, breaks facings)."""
        m = _mat(mat)
        t, l, r = _mat(top) or m, _mat(left) or m, _mat(right) or m
        bl, br = _mat(back_left) or m, _mat(back_right) or m
        x1, y1, z1 = x + dx, y + dy, z + dz
        c = np.array([x + dx / 2, y + dy / 2, z + dz / 2])
        P = lambda a, b, cc: (a, b, cc)
        faces = [
            ([P(x, y, z1), P(x1, y, z1), P(x1, y1, z1), P(x, y1, z1)], t, (0, 0, 1)),
            ([P(x, y1, z), P(x1, y1, z), P(x1, y1, z1), P(x, y1, z1)], l, (0, 1, 0)),
            ([P(x1, y, z), P(x1, y1, z), P(x1, y1, z1), P(x1, y, z1)], r, (1, 0, 0)),
        ]
        if back:
            faces += [([P(x, y, z), P(x1, y, z), P(x1, y, z1), P(x, y, z1)], br, (0, -1, 0)),
                      ([P(x, y, z), P(x, y1, z), P(x, y1, z1), P(x, y, z1)], bl, (-1, 0, 0))]
        for vs, mm, o in faces:
            self.poly(vs, mm, outward=o)
        return self

    def extrude(self, poly2d, z0, z1, mat, top=None):
        """Prism from a 2D polygon (list of (x, y) in cells, any winding, may be concave)."""
        pts = [tuple(p) for p in poly2d]
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
                   for i in range(len(pts)))
        ccw = area > 0
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            ex, ey = b[0] - a[0], b[1] - a[1]
            o = (ey, -ex, 0) if ccw else (-ey, ex, 0)
            self.poly([(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], mat, outward=o)
        self.poly([(p[0], p[1], z1) for p in pts], _mat(top) or _mat(mat), outward=(0, 0, 1))
        return self

    def ground(self, poly2d, mat, z=0.0, layer=0):
        """Flat decal polygon on the ground (roads, fields, paths). Higher layer draws over lower."""
        return self.poly([(p[0], p[1], z) for p in poly2d], mat, outward=(0, 0, 1), kind="ground",
                         bias=1e-4 * (layer + 1))

    def tile(self, cx, cy, mat, z=0.0, layer=0):
        """A full ground cell."""
        return self.ground([(cx, cy), (cx + 1, cy), (cx + 1, cy + 1), (cx, cy + 1)], mat, z, layer)

    # ------------------------------------------------------------- curved
    def cylinder(self, cx, cy, z, r, h, mat, top=None, axis="z", segs=20, caps=True):
        """Cylinder. axis 'z': base centre (cx, cy, z), radius r cells, height h px.
        axis 'x'/'y': (cx, cy, z) is the start of the axis (z = axis height in px), r is the radius in
        cells (converted to px vertically), h is the length in cells."""
        m, t = _mat(mat), _mat(top) or _mat(mat)
        ang = [2 * math.pi * k / segs for k in range(segs)]
        if axis == "z":
            ring = [(cx + r * math.cos(a), cy + r * math.sin(a)) for a in ang]
            a0 = np.array([cx * S, cy * S, z])
            for i in range(segs):
                p, q = ring[i], ring[(i + 1) % segs]
                mid = ((p[0] + q[0]) / 2 - cx, (p[1] + q[1]) / 2 - cy, 0)
                self.poly([(p[0], p[1], z), (q[0], q[1], z), (q[0], q[1], z + h), (p[0], p[1], z + h)], m,
                          outward=mid, kind="curved", nmode=("cyl", a0, np.array([0, 0, 1.0])))
            if caps:
                self.poly([(p[0], p[1], z + h) for p in ring], t, outward=(0, 0, 1))
            return self
        rz = r * S
        for i in range(segs):
            a, b = ang[i], ang[(i + 1) % segs]
            am = a + math.pi / segs          # true mid-angle (the wrap-around segment included)
            if axis == "x":
                pa = lambda L, aa: (cx + L, cy + r * math.cos(aa), z + rz * math.sin(aa))
                ax3 = np.array([1.0, 0, 0])
                mid = (0, math.cos(am), math.sin(am))
            else:
                pa = lambda L, aa: (cx + r * math.cos(aa), cy + L, z + rz * math.sin(aa))
                ax3 = np.array([0, 1.0, 0])
                mid = (math.cos(am), 0, math.sin(am))
            self.poly([pa(0, a), pa(0, b), pa(h, b), pa(h, a)], m, outward=mid, kind="curved",
                      nmode=("cyl", np.array([cx * S, cy * S, z]), ax3))
        if caps:
            for L, sgn in ((0, -1), (h, 1)):
                o = (sgn, 0, 0) if axis == "x" else (0, sgn, 0)
                self.poly([pa(L, aa) for aa in ang], t, outward=o)
        return self

    def cone(self, cx, cy, z, r, h, mat, segs=20, base=False):
        """Upright cone: base centre (cx, cy, z), radius r cells, height h px."""
        m = _mat(mat)
        ang = [2 * math.pi * k / segs for k in range(segs)]
        apex = (cx, cy, z + h)
        k = (r * S) / max(h, 1e-6)
        for i in range(segs):
            a, b = ang[i], ang[(i + 1) % segs]
            p = (cx + r * math.cos(a), cy + r * math.sin(a), z)
            q = (cx + r * math.cos(b), cy + r * math.sin(b), z)
            am = a + math.pi / segs
            mid = (math.cos(am), math.sin(am), k)
            self.poly([p, q, apex], m, outward=mid, kind="curved",
                      nmode=("cone", np.array([cx * S, cy * S, z]), np.array([0, 0, 1.0]), k))
        if base:
            self.poly([(cx + r * math.cos(a), cy + r * math.sin(a), z) for a in ang], m, outward=(0, 0, -1))
        return self

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, mat, zmin=None, rough=0.0, rough_scale=1.6):
        """Analytic ellipsoid: centre (cx, cy, cz), radii rx, ry in cells, rz in px. rough>0 gives a
        ragged, leafy silhouette (0.15-0.5)."""
        return self.add(Ellipsoid((cx, cy, cz), (rx, ry, rz), _mat(mat), self._seed(), zmin=zmin,
                                  info={"rough": rough, "rough_scale": rough_scale}))

    def sphere(self, cx, cy, cz, r, mat, **kw):
        """Sphere of radius r cells (r * 39.2 px tall half-height)."""
        return self.ellipsoid(cx, cy, cz, r, r, r * S, mat, **kw)

    def dome(self, cx, cy, z, r, h, mat):
        """Half-ellipsoid dome on z."""
        return self.ellipsoid(cx, cy, z, r, r, h, mat, zmin=z)

    def blob(self, parts, mat, rough=0.3, rough_scale=1.6):
        """Union of ellipsoids [(cx, cy, cz, r_cells[, rz_px])] - tree crowns, bushes."""
        for p in parts:
            rz = p[4] if len(p) > 4 else p[3] * S
            self.ellipsoid(p[0], p[1], p[2], p[3], p[3], rz, mat, rough=rough, rough_scale=rough_scale)
        return self
