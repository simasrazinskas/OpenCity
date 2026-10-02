"""Terrain tile helpers: exact 64x32 diamond tiles and the blob transition mask scheme.

Mask scheme (same as tools/genterrain.py): mask bits 1=N 2=E 4=S 8=W are edge neighbours of the
OTHER kind; corners bits 1=NE 2=SE 4=SW 8=NW are diagonal-only neighbours of the other kind.
Iso directions: N = -Y (up-right edge), E = +X (down-right), S = +Y (down-left), W = -X (up-left).
"""
import numpy as np

from .render import render, Sprite
from .scene import Scene

SIDE_BITS = (1, 2, 4, 8)
CORNER_SIDES = {1: (1, 2), 2: (2, 4), 4: (4, 8), 8: (8, 1)}
SIDE_NAMES = {1: "N", 2: "E", 4: "S", 8: "W"}
CORNER_NAMES = {1: "NE", 2: "SE", 4: "SW", 8: "NW"}
# neighbour offsets in cells (dx, dy) for sides and corners
SIDE_OFF = {1: (0, -1), 2: (1, 0), 4: (0, 1), 8: (-1, 0)}
CORNER_OFF = {1: (1, -1), 2: (1, 1), 4: (-1, 1), 8: (-1, -1)}


def shore_combos():
    """All (mask, corners) pairs except (0, 0), in genterrain order (index = template offset)."""
    out = []
    for mask in range(16):
        free = [c for c, (a, b) in CORNER_SIDES.items() if not (mask & a) and not (mask & b)]
        for k in range(1 << len(free)):
            corners = 0
            for j, c in enumerate(free):
                if k & (1 << j):
                    corners |= c
            if mask or corners:
                out.append((mask, corners))
    return out


def combo_label(mask, corners):
    s = "".join(SIDE_NAMES[b] for b in SIDE_BITS if mask & b)
    c = ",".join(CORNER_NAMES[b] for b in SIDE_BITS if corners & b)
    return (s or "-") + ("/" + c if c else "")


def edge_distance(u, v, mask, corners, wobble=0.0, seed=0):
    """Distance (cells) from local point (u, v) in [0,1]^2 to the nearest 'other' edge/corner.
    wobble > 0 bends the iso-lines organically but stays 0 at cell corners, so any two tiles that
    share an edge still match there (tiles are instanced anywhere in the game)."""
    from .noise import value2
    u = np.asarray(u, np.float64)
    v = np.asarray(v, np.float64)
    d = np.full(np.shape(u), 9.0)

    def w(t, k):
        if not wobble:
            return 0.0
        return wobble * np.sin(np.pi * np.clip(t, 0, 1)) * (value2(t * 5.0, k * 7.0, 1.0, seed) - 0.5) * 2

    if mask & 1:
        d = np.minimum(d, v + w(u, 1))
    if mask & 2:
        d = np.minimum(d, 1 - u + w(v, 2))
    if mask & 4:
        d = np.minimum(d, 1 - v + w(u, 3))
    if mask & 8:
        d = np.minimum(d, u + w(v, 4))
    for bit, (cu, cv) in ((1, (1, 0)), (2, (1, 1)), (4, (0, 1)), (8, (0, 0))):
        if corners & bit:
            ang = np.arctan2(np.abs(v - cv), np.abs(u - cu)) / (np.pi / 2)
            d = np.minimum(d, np.hypot(u - cu, v - cv) + w(ang, 5 + bit))
    return d


def diamond_mask(h=32, w=64):
    jj, ii = np.mgrid[0:h, 0:w]
    X, Y = ii + 0.5 - w / 2, jj + 0.5
    return np.abs(X) / (w / 2) + np.abs(Y - h / 2) / (h / 2) <= 1


def render_tile(scene, origin=(0, 0), extra_top=0, **kw):
    """Render a scene built around cell (0, 0) and return the exact 64x(32+extra_top) tile Sprite
    clipped to the cell diamond (anchor 32, 16+extra_top). `origin` shifts global pattern coordinates
    (use the cell's map position for seamless neighbours, or different origins for variants)."""
    kw.setdefault("outline", None)
    kw.setdefault("creases", 0)
    spr = render(scene, crop=False, origin=origin, **kw)
    # screen (0, 0) of cell (0, 0) is at pixel (ax, ay - 16) because the anchor is the cell centre
    x0, y0 = spr.ax - 32, spr.ay - 16 - extra_top
    out = np.zeros((32 + extra_top, 64, 4), np.uint8)
    src = spr.img[max(y0, 0):y0 + 32 + extra_top, x0:x0 + 64]
    out[(32 + extra_top) - src.shape[0]:, :src.shape[1]] = src if y0 >= 0 else src
    m = np.zeros((32 + extra_top, 64), bool)
    m[extra_top:] = diamond_mask()
    out[~m] = 0
    emit = np.zeros(m.shape, bool)
    return Sprite(out, 32, 16 + extra_top, emit, (1, 1))


def flat_tile(material, origin=(0, 0), **kw):
    """A plain ground tile of one material."""
    s = Scene((1, 1))
    s.tile(0, 0, material)
    return render_tile(s, origin, **kw)
