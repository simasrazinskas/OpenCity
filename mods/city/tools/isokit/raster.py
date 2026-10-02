"""Z-buffer rasterizer: planar faces and analytic ellipsoids into depth/id/point/normal buffers.

Pixel (i, j) samples the screen point (X0 + i + 0.5, Y0 + j + 0.5). Inside test is even-odd with
half-open edge crossings, so shared edges are never double-filled or missed and 2:1 edges come out
as clean 2-px stairs. Depth = x + y + z/32 (larger is closer to the camera).
"""
import numpy as np

from .geom import VIEW, S, newell, unit
from .noise import value2, hash2


class Buffers:
    def __init__(self, x0, y0, w, h, scale=1.0):
        """x0, y0, w, h are in output pixels; scale = output px per 1x screen px (tile width / 64)."""
        self.x0, self.y0, self.w, self.h = x0, y0, w, h
        self.scale = scale
        self.depth = np.full((h, w), -np.inf)
        self.fid = np.full((h, w), -1, np.int32)
        self.P = np.zeros((h, w, 3))
        self.N = np.zeros((h, w, 3))

    def rays(self, i0, i1, j0, j1):
        """World points at z=0 under pixel centres of the box [i0,i1) x [j0,j1).
        Returns sx, sy in output pixels; x, y via the 1x projection of (sx, sy) / scale."""
        jj, ii = np.mgrid[j0:j1, i0:i1]
        sx = ii + self.x0 + 0.5
        sy = jj + self.y0 + 0.5
        a, b = sx / (32.0 * self.scale), sy / (16.0 * self.scale)
        return ii, jj, sx, sy, (a + b) / 2.0, (b - a) / 2.0


def _inside(sx, sy, px, py):
    inside = np.zeros(sx.shape, bool)
    n = len(px)
    for k in range(n):
        xa, ya, xb, yb = px[k], py[k], px[(k + 1) % n], py[(k + 1) % n]
        if ya == yb:
            continue
        cross = (ya > sy) != (yb > sy)
        xi = xa + (sy - ya) * (xb - xa) / (yb - ya)
        inside ^= cross & (sx < xi)
    return inside


def raster_face(buf, f, fid):
    v = f.verts
    px = (v[:, 0] - v[:, 1]) * 32.0 * buf.scale
    py = ((v[:, 0] + v[:, 1]) * 16.0 - v[:, 2]) * buf.scale
    i0 = max(int(np.floor(px.min())) - buf.x0, 0)
    i1 = min(int(np.ceil(px.max())) - buf.x0 + 1, buf.w)
    j0 = max(int(np.floor(py.min())) - buf.y0, 0)
    j1 = min(int(np.ceil(py.max())) - buf.y0 + 1, buf.h)
    if i0 >= i1 or j0 >= j1:
        return
    nw = newell(v)
    den = nw @ VIEW
    if abs(den) < 1e-9 * max(1.0, np.linalg.norm(nw)):
        return
    ii, jj, sx, sy, x, y = buf.rays(i0, i1, j0, j1)
    ins = _inside(sx, sy, px, py)
    if not ins.any():
        return
    ii, jj, x, y = ii[ins], jj[ins], x[ins], y[ins]
    t = (nw @ v[0] - (nw[0] * x + nw[1] * y)) / den
    P = np.stack([x + t, y + t, 32.0 * t], -1)
    d = P[:, 0] + P[:, 1] + P[:, 2] / 32.0 + f.bias
    win = d > buf.depth[jj, ii]
    if not win.any():
        return
    jj, ii, P, d = jj[win], ii[win], P[win], d[win]
    buf.depth[jj, ii] = d
    buf.fid[jj, ii] = fid
    buf.P[jj, ii] = P
    buf.N[jj, ii] = _normals(f, P)


def _normals(f, P):
    if f.nmode is None:
        return np.broadcast_to(f.n, P.shape)
    Pm = P * np.array([S, S, 1.0])
    kind = f.nmode[0]
    if kind == "cyl":
        a0, ax = np.asarray(f.nmode[1]), unit(f.nmode[2])
        r = Pm - a0
        r = r - np.outer(r @ ax, ax)
    elif kind == "cone":
        apex, ax, k = np.asarray(f.nmode[1]), unit(f.nmode[2]), f.nmode[3]
        r = Pm - apex
        r = r - np.outer(r @ ax, ax)
        r = r / np.maximum(np.linalg.norm(r, axis=1, keepdims=True), 1e-9)
        r = r + k * ax[None, :]          # k = radius/height tilts the normal toward the apex side
    else:
        return np.broadcast_to(f.n, P.shape)
    return r / np.maximum(np.linalg.norm(r, axis=1, keepdims=True), 1e-9)


def raster_ellipsoid(buf, e, fid):
    c, r = e.c, e.r
    corners = np.array([[c[0] + sx * r[0], c[1] + sy * r[1], c[2] + sz * r[2]]
                        for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
    px = (corners[:, 0] - corners[:, 1]) * 32.0 * buf.scale
    py = ((corners[:, 0] + corners[:, 1]) * 16.0 - corners[:, 2]) * buf.scale
    i0 = max(int(np.floor(px.min())) - buf.x0, 0)
    i1 = min(int(np.ceil(px.max())) - buf.x0 + 1, buf.w)
    j0 = max(int(np.floor(py.min())) - buf.y0, 0)
    j1 = min(int(np.ceil(py.max())) - buf.y0 + 1, buf.h)
    if i0 >= i1 or j0 >= j1:
        return
    ii, jj, sx, sy, x, y = buf.rays(i0, i1, j0, j1)
    q0 = np.stack([(x - c[0]) / r[0], (y - c[1]) / r[1], np.full_like(x, (0.0 - c[2]) / r[2])], -1)
    dq = VIEW / r
    a = dq @ dq
    b = 2.0 * (q0 @ dq)
    k = 1.0
    rough = e.info.get("rough", 0.0)
    if rough:
        sc = e.info.get("rough_scale", 1.6)
        sx, sy = sx / buf.scale, sy / buf.scale
        nz = value2(sx, sy, sc, e.seed + 77) * 0.7 + hash2(np.floor(sx), np.floor(sy), e.seed + 78) * 0.3
        k = 1.0 - rough * nz
    cc = (q0 * q0).sum(-1) - k
    disc = b * b - 4 * a * cc
    hit = disc >= 0
    if not hit.any():
        return
    t = (-b[hit] + np.sqrt(disc[hit])) / (2 * a)
    ii, jj, x, y = ii[hit], jj[hit], x[hit], y[hit]
    P = np.stack([x + t, y + t, 32.0 * t], -1)
    if e.zmin is not None:
        keep = P[:, 2] >= e.zmin
        ii, jj, P = ii[keep], jj[keep], P[keep]
    d = P[:, 0] + P[:, 1] + P[:, 2] / 32.0 + e.bias
    win = d > buf.depth[jj, ii]
    jj, ii, P, d = jj[win], ii[win], P[win], d[win]
    if len(d) == 0:
        return
    q = (P - c) / r
    # gradient of |q|^2 w.r.t. metric coords: q_i / (r_i * scale_i)
    g = q / (r * np.array([S, S, 1.0]))
    n = g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-12)
    buf.depth[jj, ii] = d
    buf.fid[jj, ii] = fid
    buf.P[jj, ii] = P
    buf.N[jj, ii] = n
