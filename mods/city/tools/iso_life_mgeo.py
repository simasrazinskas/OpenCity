"""iso_life_mgeo - iso projection helpers, footprint masks, tiny box-house renderer (numpy only)."""
import numpy as np

from iso_life_common import canvas
from iso_life_mdraw import col, shade, mix, outline_mask

M = 3   # margin px around footprint sprites


def proj(x, y, z, ox, oy):
    return ox + (x - y) * 32.0, oy + (x + y) * 16.0 - z


def poly_mask(w, h, pts):
    """Even-odd fill of polygon pts [(sx, sy)...] on pixel centres."""
    ys, xs = np.mgrid[0:h, 0:w]
    px_, py_ = xs + 0.5, ys + 0.5
    inside = np.zeros((h, w), bool)
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        if y0 == y1:
            continue
        c = ((y0 <= py_) & (py_ < y1)) | ((y1 <= py_) & (py_ < y0))
        xi = x0 + (py_ - y0) * (x1 - x0) / (y1 - y0)
        inside ^= c & (px_ < xi)
    return inside


def fp_size(nx, ny):
    return (nx + ny) * 32 + 2 * M, (nx + ny) * 16 + 2 * M


def fp_origin(nx, ny):
    """Screen position (in the footprint sprite) of the footprint's top corner (cell 0,0 corner)."""
    return ny * 32 + M, M


def fp_coords(nx, ny):
    """Per-pixel cell coords (x, y) of pixel centres of the footprint sprite. Returns X, Y, w, h."""
    w, h = fp_size(nx, ny)
    ox, oy = fp_origin(nx, ny)
    ys, xs = np.mgrid[0:h, 0:w]
    sx, sy = xs + 0.5 - ox, ys + 0.5 - oy
    X = (sy / 16.0 + sx / 32.0) / 2.0
    Y = (sy / 16.0 - sx / 32.0) / 2.0
    return X, Y, w, h


def fp_inside(nx, ny, inset=0.0):
    X, Y, w, h = fp_coords(nx, ny)
    return (X >= inset) & (X <= nx - inset) & (Y >= inset) & (Y <= ny - inset)


def fp_edge_band(nx, ny, px_thick=1, arm=None):
    """Mask of the footprint's border band (px_thick screen px vertically).
    arm: None for the full border, or arm length in px (horizontal) for corner brackets."""
    X, Y, w, h = fp_coords(nx, ny)
    t = px_thick / 32.0
    inside = (X >= 0) & (X <= nx) & (Y >= 0) & (Y <= ny)
    band = np.zeros((h, w), bool)
    a = None if arm is None else arm / 32.0
    # edge x=0 (along y), x=nx, y=0 (along x), y=ny
    def part(dist, along, length):
        b = inside & (dist < t)
        if a is not None:
            b &= (along < a) | (length - along < a)
        return b
    band |= part(X, Y, ny)
    band |= part(nx - X, Y, ny)
    band |= part(Y, X, nx)
    band |= part(ny - Y, X, nx)
    return band


def fp_cell_lines(nx, ny):
    """Mask of interior cell boundaries (1 px)."""
    X, Y, w, h = fp_coords(nx, ny)
    t = 1 / 32.0
    m = np.zeros((h, w), bool)
    inside = (X > 0) & (X < nx) & (Y > 0) & (Y < ny)
    for i in range(1, nx):
        m |= inside & (np.abs(X - i) < t / 2 + 1e-9)
    for j in range(1, ny):
        m |= inside & (np.abs(Y - j) < t / 2 + 1e-9)
    return m


# ----------------------------------------------------------------------------- simple box house
HOUSE_W, HOUSE_H = 64, 64


def house(wall="#d8c8a0", roof="#b84a30", trim="#6a4a2a", door=True, height=14, ridge=10):
    """One-cell box house with gable roof, ridge along X. 64x64 sprite, cell top corner at (32, 32)."""
    w, h = HOUSE_W, HOUSE_H
    ox, oy = 32.0, 32.0
    img = canvas(w, h)
    a, b = 0.12, 0.88
    hz = height
    rz = height + ridge
    m = 0.5

    def P(x, y, z):
        return proj(x, y, z, ox, oy)

    def face(pts3, colr):
        pts = [P(*p) for p in pts3]
        mk = poly_mask(w, h, pts)
        img[mk] = col(colr)
        return mk

    lit, dark = col(wall), shade(wall, 0.72)
    # back roof slope (-Y) first
    face([(a, a, hz), (b, a, hz), (b, m, rz), (a, m, rz)], shade(roof, 0.70))
    # +X wall (shaded), pentagon with gable
    face([(b, a, 0), (b, b, 0), (b, b, hz), (b, m, rz), (b, a, hz)], dark)
    # +Y wall (lit)
    wm = face([(a, b, 0), (b, b, 0), (b, b, hz), (a, b, hz)], lit)
    # front roof slope (+Y), overhangs slightly
    face([(a - .04, b + .04, hz - 1), (b + .04, b + .04, hz - 1), (b + .04, m, rz), (a - .04, m, rz)], roof)
    # roof tile rows (dark lines)
    for k in range(1, 3):
        t = k / 3.0
        yy = b + .04 - (b + .04 - m) * t
        zz = hz - 1 + (rz - hz + 1) * t
        p0, p1 = P(a - .04, yy, zz), P(b + .04, yy, zz)
        _line(img, p0, p1, shade(roof, 0.82))
    # gable ridge cap
    _line(img, P(a - .04, m, rz), P(b + .04, m, rz), shade(roof, 1.25))
    # eave shadow line under roof on lit wall
    _line(img, P(a, b, hz - 2), P(b, b, hz - 2), shade(wall, 0.85))
    # door and window on +Y wall
    if door:
        _rect3(img, P, (0.30, b, 0), (0.46, b, 9), "#6a4a2a")
        _rect3(img, P, (0.58, b, 5), (0.76, b, 10), "#6fa0c8")
    # window on +X wall
    _rect3y(img, P, (b, 0.30, 5), (b, 0.52, 10), "#547898")
    # base outline for readability
    mk = img[..., 3] > 0
    o = outline_mask(mk, diag=False)
    img[o] = col("#3a2c24")
    return img


def _line(img, p0, p1, c):
    from iso_life_mdraw import line
    line(img, int(round(p0[0])), int(round(p0[1])), int(round(p1[0])), int(round(p1[1])), c)


def _rect3(img, P, a, b, c):
    """Axis-aligned rect on a y=const wall from (x0,y,z0) to (x1,y,z1)."""
    h, w = img.shape[:2]
    pts = [P(a[0], a[1], a[2]), P(b[0], a[1], a[2]), P(b[0], a[1], b[2]), P(a[0], a[1], b[2])]
    mk = poly_mask(w, h, pts)
    mk &= img[..., 3] > 0
    img[mk] = col(c)


def _rect3y(img, P, a, b, c):
    """Rect on an x=const wall from (x, y0, z0) to (x, y1, z1)."""
    h, w = img.shape[:2]
    pts = [P(a[0], a[1], a[2]), P(a[0], b[1], a[2]), P(a[0], b[1], b[2]), P(a[0], a[1], b[2])]
    mk = poly_mask(w, h, pts)
    mk &= img[..., 3] > 0
    img[mk] = col(c)
