"""
iso_life_render - ray-cast renderer for small rotatable models (vehicles, aircraft, boats).

Why its own renderer: vehicles need 8 rotations (incl. 45 degree diagonals) and sloped faces
(windscreens, bonnets, cab noses). Models are unions of convex polyhedra given as half-spaces in a
metric-isotropic local frame: u forward, v left, w up, 1 unit = 1/64 cell (0.707 metric px).
z in screen px = w * WZ.

Projection (BRIEF.md): screen_x = (x - y) * 32, screen_y = (x + y) * 16 - z_px, x, y in cells.
Light from the upper left: top brightest, +Y faces lit, +X faces shaded.
Each screen pixel is super-sampled SS x SS; a pixel is opaque when half its samples hit, and takes the
most frequent (material, shade) code, so colours stay on the ramps (no anti-aliasing).
"""
import math

import numpy as np

WZ = 0.7071 / 1.1547         # local w unit -> screen z px
SS = 4

# Light in metric world space (x, y, z): top > +Y (left face) > +X (right face).
LIGHT = np.array([0.25, 0.55, 0.80])
LIGHT = LIGHT / np.linalg.norm(LIGHT)

# Facings in WORLD compass, clockwise from world north (engine plan): N = -Y (screen up-right), NE = screen right,
# E = +X (screen down-right), SE = screen down, S = +Y (screen down-left), SW = screen left, W = -X (screen up-left),
# NW = screen up. Value = world yaw (atan2(y, x), degrees) of the vehicle's forward axis.
DIRS = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]
DIR_YAW = {d: (270 + 45 * i) % 360 for i, d in enumerate(DIRS)}


class Prim:
    """Convex polyhedron: rows (nu, nv, nw, d) meaning nu*u + nv*v + nw*w <= d."""

    def __init__(self, planes, mat, facemats=None):
        self.planes = np.array(planes, np.float64)
        self.mat = mat
        self.facemats = facemats or {}   # plane index -> material override

    def cut(self, n, p, mat=None):
        """Add the half-space n . (x - p) <= 0 (keeps the side opposite to n)."""
        n = np.array(n, np.float64)
        self.planes = np.vstack([self.planes, [n[0], n[1], n[2], float(np.dot(n, p))]])
        if mat:
            self.facemats[len(self.planes) - 1] = mat
        return self

    def face(self, idx, mat):
        self.facemats[idx] = mat
        return self


# Plane order of a box: 0 +u (front), 1 -u (back), 2 +v (left), 3 -v (right), 4 +w (top), 5 -w (bottom).
FRONT, BACK, LEFT, RIGHT, TOP, BOTTOM = range(6)


def box(u0, u1, v0, v1, w0, w1, mat, **faces):
    names = {"front": FRONT, "back": BACK, "left": LEFT, "right": RIGHT, "top": TOP, "bottom": BOTTOM}
    p = Prim([[1, 0, 0, u1], [-1, 0, 0, -u0], [0, 1, 0, v1], [0, -1, 0, -v0], [0, 0, 1, w1], [0, 0, -1, -w0]], mat)
    for k, m in faces.items():
        p.facemats[names[k]] = m
    return p


def cbox(cu, cv, cw, lu, lv, lw, mat, **faces):
    """Box by centre (u, v) and bottom w, with lengths."""
    return box(cu - lu / 2, cu + lu / 2, cv - lv / 2, cv + lv / 2, cw, cw + lw, mat, **faces)


def cyl(cu, cv, cw, r, h, mat, sides=8, axis="w", top=None):
    """Prism with a regular polygon cross-section. axis w: vertical; v: wheel axle; u: lengthwise tank."""
    planes = []
    for k in range(sides):
        a = 2 * math.pi * (k + 0.5) / sides
        c, s = math.cos(a), math.sin(a)
        if axis == "w":
            planes.append([c, s, 0, c * cu + s * cv + r])
        elif axis == "v":
            planes.append([c, 0, s, c * cu + s * (cw + r) + r])
        else:
            planes.append([0, c, s, c * cv + s * (cw + r) + r])
    if axis == "w":
        planes += [[0, 0, 1, cw + h], [0, 0, -1, -cw]]
    elif axis == "v":
        planes += [[0, 1, 0, cv + h / 2], [0, -1, 0, -(cv - h / 2)]]
    else:
        planes += [[1, 0, 0, cu + h / 2], [-1, 0, 0, -(cu - h / 2)]]
    p = Prim(planes, mat)
    if top and axis == "w":
        p.facemats[sides] = top
    return p


def wheels(u_list, half_track, r, width, mat="tyre"):
    out = []
    for u in u_list:
        for side in (-1, 1):
            out.append(cyl(u, side * half_track, 0, r, width, mat, sides=8, axis="v"))
    return out


def transform(prims, du=0.0, dv=0.0, dw=0.0, yaw_deg=0.0):
    """Rigidly move prims inside the local frame (for hinged segments, rotors, sub-assemblies)."""
    a = math.radians(yaw_deg)
    c, s = math.cos(a), math.sin(a)
    out = []
    for p in prims:
        q = Prim(p.planes.copy(), p.mat, dict(p.facemats))
        n = q.planes[:, :3].copy()
        nu = c * n[:, 0] - s * n[:, 1]
        nv = s * n[:, 0] + c * n[:, 1]
        q.planes[:, 0], q.planes[:, 1] = nu, nv
        q.planes[:, 3] = q.planes[:, 3] + nu * du + nv * dv + n[:, 2] * dw
        out.append(q)
    return out


_VCACHE = {}


def vertices(p):
    """Corner points of a convex prim (intersections of plane triples inside all half-spaces), cached."""
    key = p.planes.tobytes()
    if key in _VCACHE:
        return _VCACHE[key]
    P = p.planes
    n = len(P)
    pts = []
    for i in range(n):
        for j in range(i + 1, n):
            for k in range(j + 1, n):
                M = P[[i, j, k], :3]
                if abs(np.linalg.det(M)) < 1e-9:
                    continue
                x = np.linalg.solve(M, P[[i, j, k], 3])
                if np.all(P[:, :3] @ x <= P[:, 3] + 1e-6):
                    pts.append(x)
    V = np.array(pts) if pts else np.zeros((0, 3))
    _VCACHE[key] = V
    return V


def render(prims, yaw_deg, width, height, ax, ay, lift=0.0):
    """Ray-cast prims rotated by yaw (world degrees) into a width x height canvas whose ground anchor
    (local origin) is at pixel (ax, ay). Returns (mat_index_image, shade_image, mats) with mat -1 = empty.
    `lift` raises the model (screen px) without moving the anchor (aircraft)."""
    mats = sorted({p.mat for p in prims} | {m for p in prims for m in p.facemats.values()})
    mid = {m: i for i, m in enumerate(mats)}
    ys, xs = np.mgrid[0:height * SS, 0:width * SS]
    sx = (xs + 0.5) / SS - ax
    sy = (ys + 0.5) / SS - ay + lift
    # World point on the ray at height t (screen px): z = t, x + y = (sy + t) / 16, x - y = sx / 32.
    # In local units: X = 64 x, Y = 64 y, w = t / WZ; then rotate by -yaw.
    X0 = 64 * (sy / 32 + sx / 64)
    Y0 = 64 * (sy / 32 - sx / 64)
    dX = 64 / 32.0
    a = math.radians(yaw_deg)
    c, s = math.cos(a), math.sin(a)
    U0, V0 = c * X0 + s * Y0, -s * X0 + c * Y0
    dU, dV = c * dX + s * dX, -s * dX + c * dX
    dW = 1 / WZ
    best = np.full(sx.shape, -1e9)
    code = np.full(sx.shape, -1, np.int32)
    shade = np.zeros(sx.shape)
    H, W = sx.shape
    for p in prims:
        P = p.planes
        # screen-space bounding box of the prim (in sub-samples) to limit the work
        V = vertices(p)
        if len(V) == 0:
            continue
        wx = (c * V[:, 0] - s * V[:, 1]) / 64
        wy = (s * V[:, 0] + c * V[:, 1]) / 64
        px = (wx - wy) * 32 + ax
        py = (wx + wy) * 16 - V[:, 2] * WZ + ay - lift
        x0 = max(0, int(math.floor(px.min() * SS)) - 1)
        x1 = min(W, int(math.ceil(px.max() * SS)) + 2)
        y0 = max(0, int(math.floor(py.min() * SS)) - 1)
        y1 = min(H, int(math.ceil(py.max() * SS)) + 2)
        if x0 >= x1 or y0 >= y1:
            continue
        win = (slice(y0, y1), slice(x0, x1))
        u0, v0 = U0[win], V0[win]
        A = P[:, 0, None, None] * u0 + P[:, 1, None, None] * v0          # n . p0 (w0 = 0)
        B = P[:, 0] * dU + P[:, 1] * dV + P[:, 2] * dW                     # n . dir per t
        # constraint A + B t <= d
        lo = np.full(u0.shape, -1e9)
        hi = np.full(u0.shape, 1e9)
        hi_plane = np.full(u0.shape, -1, np.int32)
        ok = np.ones(u0.shape, bool)
        for i in range(len(P)):
            r = P[i, 3] - A[i]
            if abs(B[i]) < 1e-9:
                ok &= r >= 0
            elif B[i] > 0:
                t = r / B[i]
                m = t < hi
                hi = np.where(m, t, hi)
                hi_plane = np.where(m, i, hi_plane)
            else:
                lo = np.maximum(lo, r / B[i])
        bw = best[win]
        hit = ok & (lo <= hi) & (hi > bw)
        if not hit.any():
            continue
        best[win] = np.where(hit, hi, bw)
        cw, sw = code[win], shade[win]
        for i in range(len(P)):
            m = hit & (hi_plane == i)
            if not m.any():
                continue
            nu, nv, nw = P[i, :3]
            nx, ny = c * nu - s * nv, s * nu + c * nv
            n = np.array([nx, ny, nw])
            n = n / (np.linalg.norm(n) + 1e-12)
            cw[m] = mid[p.facemats.get(i, p.mat)]
            sw[m] = float(np.dot(n, LIGHT))
    # downsample: mode of (mat, shade level) codes
    lvl = np.clip(np.round((shade + 0.2) * 8), 0, 15).astype(np.int32)
    full = np.where(code >= 0, code * 16 + lvl, -1)
    blocks = full.reshape(height, SS, width, SS).transpose(0, 2, 1, 3).reshape(height, width, SS * SS)
    cov = (blocks >= 0).sum(-1)
    cnt = (blocks[..., :, None] == blocks[..., None, :]).sum(-1)
    cnt = np.where(blocks >= 0, cnt, -1)
    pick = np.take_along_axis(blocks, cnt.argmax(-1)[..., None], -1)[..., 0]
    out_code = np.where(cov * 2 >= SS * SS, pick, -1)
    matimg = np.where(out_code >= 0, out_code // 16, -1)
    shimg = np.where(out_code >= 0, (out_code % 16) / 8.0 - 0.2, 0.0)
    return matimg, shimg, mats


def colourise(matimg, shimg, mats, palette, emissive=(), outline=True):
    """Colour a render on the isokit palette.

    palette: material -> (isokit ramp, shade offset) for lit materials, or a fixed (ramp, index) via EMIT for
    emissive ones (`emissive` names, or any material missing from palette but present in EMIT).
    The lit level is the face's light dot (top ~0.75, +Y ~0.51, +X ~0.22, same as isokit); index = level * 11 + offset.
    outline: isokit's 'edge' house style, the silhouette rim is darkened by 2 shades (not on emissive pixels).
    """
    from iso_life_palette import PALETTE, RAMP, EMIT
    h, w = matimg.shape
    rid = np.full((h, w), -1, np.int64)
    idx = np.zeros((h, w), np.float64)
    fixed = np.zeros((h, w), bool)
    for i, m in enumerate(mats):
        sel = matimg == i
        if not sel.any():
            continue
        if m in emissive or (m not in palette and m in EMIT):
            r, k = EMIT[m] if m in EMIT else palette[m]
            rid[sel], idx[sel], fixed[sel] = RAMP[r], k, True
        else:
            r, off = palette[m]
            rid[sel] = RAMP[r]
            idx[sel] = np.clip(shimg[sel], 0, 1) * 0.94 * 11 + off
    opaque = rid >= 0
    if outline:
        pad = np.pad(opaque, 1)
        rim = opaque & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
        idx[rim & ~fixed] -= 2
    out = np.zeros((h, w, 4), np.uint8)
    k = np.clip(np.round(idx), 0, 11).astype(np.int64)
    out[opaque, :3] = PALETTE[rid[opaque], k[opaque]]
    out[opaque, 3] = 255
    return out
