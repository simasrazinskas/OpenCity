#!/usr/bin/env python3
"""
iso_net_overlays.py - NET art: zone tiles, tool/placement markers, footprint previews, info-view heat tiles.

Outputs 1x PNGs (alpha strictly 0/255) under design/iso/net/{zones,markers,infoviews}/ and the manifest
fragment design/iso/net/_frag/40-overlays.json. Deterministic; numpy + stdlib only.

Translucency is simulated with screen-space 4x4 Bayer dithering (all neighbouring cells sit at offsets that are
multiples of (32, 16) px, so the dither tiles seamlessly). Patterns are defined in cell space quantised to 1/64
cell ("q"): a line of width 2q running along a cell axis is exactly 2 px per row (a crisp 2:1 stair).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_net_core import (BAYER4, OUT, TH, TW, blank, hx, lerp, save_png, scale,  # noqa: E402
                          tile_grid, write_manifest_fragment)

WHITE = np.array([255, 255, 255], np.float32)
GROUPS = []


# --------------------------------------------------------------------------- small helpers
def qpos(u, v):
    return np.floor(u * 64).astype(np.int64), np.floor(v * 64).astype(np.int64)


def stripe(Q, period, width, centre):
    """True where integer q coordinate Q lies in a band of `width` q centred on `centre` (mod period)."""
    return ((Q + width // 2 - centre) % period) < width


def thr(px, py, ox=0, oy=0):
    return BAYER4[(py + oy) % 4, (px + ox) % 4]


def render_n(fn, nx=1, ny=1):
    """Render an nx x ny cell footprint sprite ((nx+ny)*32 x (nx+ny)*16). fn(u, v, px, py) gets footprint cell
    coords u in [0,nx), v in [0,ny) and sprite pixel coords; returns (rgb[N,3], alpha[N])."""
    w, h = (nx + ny) * 32, (nx + ny) * 16
    u, v, _ = tile_grid(0, w, h, ny * 32, 0)
    m = (u >= 0) & (u < nx) & (v >= 0) & (v < ny)
    py, px = np.mgrid[0:h, 0:w]
    rgb, a = fn(u[m], v[m], px[m], py[m])
    img = blank(w, h)
    out = np.zeros((m.sum(), 4), np.float32)
    out[:, :3] = rgb
    out[:, 3] = np.where(a, 255, 0)
    img[m] = out
    return img


def edge_dist(U, V, nx=1, ny=1):
    """Integer distance (q) from the footprint's outer edge."""
    return np.minimum(np.minimum(U, nx * 64 - 1 - U), np.minimum(V, ny * 64 - 1 - V))


def perimeter_s(U, V, nx=1, ny=1):
    """Position (q) along the outline of the footprint, clockwise from the top corner (N edge first)."""
    W, H = nx * 64, ny * 64
    d = np.stack([V, W - 1 - U, H - 1 - V, U])  # distance to N, E, S, W edges
    k = np.argmin(d, axis=0)
    s = np.where(k == 0, U, np.where(k == 1, W + V, np.where(k == 2, W + H + (W - 1 - U), 2 * W + H + (H - 1 - V))))
    return s


def save(rel, img):
    save_png(os.path.join(OUT, rel), img)
    return rel


def hstrip(frames):
    return np.concatenate(frames, axis=1)


# --------------------------------------------------------------------------- 1. zone tiles
ZONES = [  # key, label, colour
    ("dezone", "Dezone", "FFFFFF"),
    ("res-low", "Res low", "7ED957"),
    ("res-medium", "Res med", "4FBF4F"),
    ("res-high", "Res high", "2E9E3E"),
    ("res-row", "Res row", "A8E063"),
    ("res-mixed", "Res mixed", "8FD3C8"),
    ("res-lowrent", "Low rent", "2F6F3F"),
    ("com-low", "Com low", "5AB4F0"),
    ("com-high", "Com high", "2C6FD1"),
    ("industrial", "Industrial", "F2C94C"),
    ("warehouse", "Warehouse", "C9A227"),
    ("office", "Office", "B07CE8"),
    ("office-high", "Office high", "7A4FC4"),
    ("invalid", "Invalid", "E03030"),
]


def _dots(U, V, p, s):
    return stripe(U, p, s, p // 2) & stripe(V, p, s, p // 2)


def _diamonds(U, V, p, r):
    du = (U % p) + 0.5 - p / 2
    dv = (V % p) + 0.5 - p / 2
    return (np.abs(du) + np.abs(dv)) <= r


def _xmark(U, V, p, arm):
    du = (U % p) - p // 2
    dv = (V % p) - p // 2
    return ((du >= -1) & (du <= 0) & (np.abs(dv + 0.5) <= arm)) | ((dv >= -1) & (dv <= 0) & (np.abs(du + 0.5) <= arm))


PATTERNS = {
    "dezone": lambda U, V: _xmark(U, V, 16, 3.6),
    "res-low": lambda U, V: _dots(U, V, 16, 6),
    "res-medium": lambda U, V: _dots(U, V, 8, 4),
    "res-high": lambda U, V: _diamonds(U, V, 8, 3.5),
    "res-row": lambda U, V: stripe(V, 16, 2, 8) & ((U % 16) < 12),
    "res-mixed": lambda U, V: _dots(U, V, 16, 4) | stripe(V, 32, 2, 16),
    "res-lowrent": lambda U, V: (((U // 8) + (V // 8)) % 2) == 0,
    "com-low": lambda U, V: stripe(U, 16, 2, 8),
    "com-high": lambda U, V: stripe(U, 16, 2, 6) | stripe(U, 16, 2, 10),
    "industrial": lambda U, V: stripe(U, 16, 2, 8) | stripe(V, 16, 2, 8),
    "warehouse": lambda U, V: stripe(V, 32, 6, 16),
    "office": lambda U, V: _dots(U, V, 16, 8) & ~_dots(U, V, 16, 4),
    "office-high": lambda U, V: _dots(U, V, 8, 6) & ~_dots(U, V, 8, 2),
    "invalid": lambda U, V: _xmark(U, V, 64, 14),
}


def zone_tile(key, colour, active=True):
    c = hx(colour)
    light = lerp(c, WHITE, 0.4)
    dark = scale(c, 0.42)
    bcol = scale(c, 0.72)
    pat_fn = PATTERNS[key]

    def fn(u, v, px, py):
        U, V = qpos(u, v)
        n = len(u)
        rgb = np.zeros((n, 3), np.float32)
        rgb[:] = light
        a = thr(px, py) < (0.5 if active else 0.25)
        pat = pat_fn(U, V)
        if not active:
            pat = pat & (thr(px, py, 1, 1) < 0.5)
        rgb[pat] = dark
        a = a | pat
        e = edge_dist(U, V)
        border = (e >= 2) & (e < 4)
        rgb[border] = bcol
        a = a | border
        return rgb, a

    from iso_net_core import render_flat
    return render_flat(fn)


def make_zones():
    act, idl = [], []
    for key, label, colour in ZONES:
        act.append({"file": save("zones/%s.png" % key, zone_tile(key, colour, True)), "label": label})
        idl.append({"file": save("zones/%s-idle.png" % key, zone_tile(key, colour, False)), "label": label})
    GROUPS.append({"title": "Zone tiles: active", "note": "50% dither tint + cell-space pattern + 2q inset cell border; one pattern per zone so they read without colour",
                   "columns": 7, "scale": 2, "items": act})
    GROUPS.append({"title": "Zone tiles: idle", "note": "Border + sparse pattern, 25% dither (zone brush not active)",
                   "columns": 7, "scale": 2, "items": idl})


# --------------------------------------------------------------------------- 2. markers
MARKERS = [  # key, label, colour
    ("valid", "Valid", "46DC5A"),
    ("invalid", "Invalid", "E63C32"),
    ("neutral", "Existing", "FFFFFF"),
    ("replace", "Replace", "4696FF"),
    ("remove", "Remove", "FF961E"),
]
INK = np.array([24, 24, 32], np.float32)


def _flat(fn):
    from iso_net_core import render_flat
    return render_flat(fn)


def marker_tile(colour, density=0.35, arrow=None, ghost=False):
    """Dither fill + solid 2q outline + bright 4q corner brackets (+ optional direction arrow)."""
    c = hx(colour)
    fill = lerp(c, WHITE, 0.15)
    edge = scale(c, 0.8)
    bright = lerp(c, WHITE, 0.55)

    def fn(u, v, px, py):
        U, V = qpos(u, v)
        n = len(u)
        rgb = np.zeros((n, 3), np.float32)
        rgb[:] = fill
        a = thr(px, py) < density
        e = edge_dist(U, V)
        a = a | (e < 2)
        rgb[e < 2] = edge
        near_u = np.minimum(U, 63 - U) < 14
        near_v = np.minimum(V, 63 - V) < 14
        br = (e < 4) & near_u & near_v
        rgb[br] = bright
        a = a | br
        if arrow is not None:
            m_out, m_in = arrow_masks(U, V, arrow)
            rgb[m_out] = INK
            rgb[m_in] = WHITE
            a = a | m_out
        return rgb, a
    return _flat(fn)


def arrow_masks(U, V, d):
    """Arrow painted in cell space pointing toward edge d (N: v=0, E: u=1, S: v=1, W: u=0)."""
    uc, vc = U + 0.5 - 32, V + 0.5 - 32
    t, l = {"N": (-vc, uc), "E": (uc, vc), "S": (vc, -uc), "W": (-uc, -vc)}[d]

    def shape(g):
        shaft = (t >= -13 - g) & (t <= 3) & (np.abs(l) <= 2.5 + g)
        head = (t > 3 - g) & (t <= 19 + g) & (np.abs(l) <= (19 - t) * 0.72 + 0.5 + g * 1.2)
        return shaft | head
    outer = shape(1.3)
    return outer, shape(0.0)


def x_mask(U, V, w):
    """X as it appears on screen: two cell-axis lines through the centre (cross in cell space)."""
    du, dv = U + 0.5 - 32, V + 0.5 - 32
    return ((np.abs(du) <= w / 2) & (np.abs(dv) <= 20)) | ((np.abs(dv) <= w / 2) & (np.abs(du) <= 20))


def bulldoze_tile():
    orange = hx("FF961E")
    dred = hx("8A1414")

    def fn(u, v, px, py):
        U, V = qpos(u, v)
        n = len(u)
        rgb = np.zeros((n, 3), np.float32)
        hatch = stripe(U + V, 16, 6, 8)  # horizontal screen hatch bands in cell space
        rgb[:] = lerp(orange, WHITE, 0.2)
        a = (thr(px, py) < 0.3) | hatch
        rgb[hatch] = orange
        e = edge_dist(U, V)
        rgb[e < 2] = scale(orange, 0.75)
        a = a | (e < 2)
        outer = x_mask(U, V, 8)
        inner = x_mask(U, V, 4)
        rgb[outer] = INK
        rgb[inner] = dred
        a = a | outer
        return rgb, a
    return _flat(fn)


def ants_tile(frame, colour="FFFFFF"):
    def fn(u, v, px, py):
        U, V = qpos(u, v)
        s = perimeter_s(U, V)
        e = edge_dist(U, V)
        band = (e >= 1) & (e < 3)
        dash = ((s + frame * 8) % 16) < 8
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = np.where(dash[:, None], hx(colour), INK)
        return rgb, band
    return _flat(fn)


def gridtile():
    def fn(u, v, px, py):
        U, V = qpos(u, v)
        s = perimeter_s(U, V)
        e = edge_dist(U, V)
        band = (e >= 1) & (e < 3) & ((s % 8) < 4)
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = [235, 235, 235]
        return rgb, band
    return _flat(fn)


def footprint(n, valid=True):
    c = hx("46DC5A" if valid else "E63C32")
    fill = lerp(c, WHITE, 0.15)
    edge = lerp(c, WHITE, 0.45)
    cellgrid = lerp(c, WHITE, 0.6)

    def fn(u, v, px, py):
        U, V = np.floor(u * 64).astype(np.int64), np.floor(v * 64).astype(np.int64)
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = fill
        a = thr(px, py) < 0.4
        # faint per-cell grid: dotted lines on interior cell boundaries
        ib_u = stripe(U, 64, 2, 0) & (U > 4) & (U < n * 64 - 4)
        ib_v = stripe(V, 64, 2, 0) & (V > 4) & (V < n * 64 - 4)
        grid = (ib_u | ib_v) & (((px + py) % 2) == 0)
        rgb[grid] = cellgrid
        a = a | grid
        e = edge_dist(U, V, n, n)
        s = perimeter_s(U, V, n, n)
        dash = (e < 4) & ((s % 16) < 11)
        rgb[dash] = edge
        a = a | dash
        return rgb, a
    return render_n(fn, n, n)


def make_markers():
    items = []
    for key, label, colour in MARKERS:
        items.append({"file": save("markers/%s.png" % key, marker_tile(colour)), "label": label})
    items.append({"file": save("markers/bulldoze.png", bulldoze_tile()), "label": "Bulldoze"})
    f0, f1 = ants_tile(0), ants_tile(1)
    items.append({"file": save("markers/select-0.png", f0), "label": "Select f1"})
    items.append({"file": save("markers/select-1.png", f1), "label": "Select f2"})
    items.append({"file": save("markers/select-strip.png", hstrip([f0, f1])), "label": "Select 2 frames"})
    items.append({"file": save("markers/grid.png", gridtile()), "label": "Grid tile"})
    for d in "NESW":
        items.append({"file": save("markers/path-%s.png" % d.lower(), marker_tile("46DC5A", 0.25, arrow=d)),
                      "label": "Path " + d})
    GROUPS.append({"title": "Tool markers and drag path", "note": "Dither fill, solid outline, bright corner brackets; select = marching ants; path = ghost + direction arrow",
                   "columns": 8, "scale": 2, "items": items})
    fp = []
    for n in (1, 2, 3):
        for valid in (True, False):
            tag = "%dx%d-%s" % (n, n, "ok" if valid else "bad")
            fp.append({"file": save("markers/footprint-%s.png" % tag, footprint(n, valid)),
                       "label": "%dx%d %s" % (n, n, "valid" if valid else "invalid")})
    GROUPS.append({"title": "Building footprint previews", "note": "Multi-cell diamonds ((w+h)*32 x (w+h)*16), dashed outline, faint cell grid",
                   "columns": 2, "scale": 2, "items": fp})


# --------------------------------------------------------------------------- 3. info-view heat tiles
def _stops(*hexes_or_rgb):
    out = []
    n = len(hexes_or_rgb)
    for i, c in enumerate(hexes_or_rgb):
        rgb = hx(c) if isinstance(c, str) else np.array(c, np.float32)
        out.append((i / (n - 1), rgb))
    return out


RAMPS = {  # key: (label, stops, density(t))
    "good": ("Good", _stops("E54B3C", "F2D04C", "3FC95A"), lambda t: 0.62 + 0 * t),
    "pollution": ("Pollution", _stops((150, 220, 130), (240, 170, 40), (105, 45, 20)), lambda t: 0.06 + 0.66 * t),
    "blue": ("Blue", _stops((205, 230, 255), (40, 85, 215)), lambda t: 0.40 + 0.30 * t),
    "green": ("Green", _stops((25, 35, 25), (70, 215, 95)), lambda t: 0.30 + 0.40 * t),
}
CATEGORY = "E54B3C 3FA9F5 7ED957 F2C94C B07CE8 FF8C3A 4FD6C6 E86BB4 9AC93C 5A6FE0 D69E6B 6BC99B C95A7B 8FA8B8 E0E05A 5AA08A".split()
RESOURCES = [("Yellow", "EBCD3C"), ("Green", "3CB94B"), ("Blue", "4682EB"), ("Coal", "46465A"),
             ("Stone", "AAA096"), ("Cyan", "3CC8D2")]
LEVELS = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100]
STEPS = 10  # palette steps used by the gradient dither (matches the 11 level tiles)


def ramp_rgb(name, t):
    t = np.asarray(t, np.float32)
    stops = RAMPS[name][1]
    xs = [s[0] for s in stops]
    return np.stack([np.interp(t, xs, [s[1][k] for s in stops]) for k in range(3)], axis=-1).astype(np.float32)


def level_tile(name, lv):
    t = lv / 100.0
    col = ramp_rgb(name, np.array([t]))[0]
    dens = float(RAMPS[name][2](t))

    def fn(u, v, px, py):
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = col
        return rgb, thr(px, py) < dens
    return _flat(fn)


def flat_colour_tile(colour, dens):
    col = hx(colour)

    def fn(u, v, px, py):
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = col
        return rgb, thr(px, py) < dens
    return _flat(fn)


def heat_pixels(name, t, px, py):
    """Smooth heat colour: bayer-dither between neighbouring ramp steps, dither density from the ramp."""
    t = np.clip(t, 0, 1)
    x = t * STEPS
    i = np.floor(x).astype(np.int64)
    f = x - i
    idx = np.where(f > thr(px, py), np.minimum(i + 1, STEPS), i)
    rgb = ramp_rgb(name, idx / STEPS)
    a = thr(px, py, 2, 1) < RAMPS[name][2](t)
    return rgb, a


def gradient_tile(name, corners):
    """corners = values (0..1) at the diamond's top (u0,v0), right (u1,v0), bottom (u1,v1), left (u0,v1)."""
    c00, c10, c11, c01 = corners

    def fn(u, v, px, py):
        t = c00 * (1 - u) * (1 - v) + c10 * u * (1 - v) + c11 * u * v + c01 * (1 - u) * v
        return heat_pixels(name, t, px, py)
    return _flat(fn)


def hill(x, y, n):
    cx, cy = n * 0.58, n * 0.45
    h = np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (2 * (n * 0.26) ** 2))
    h2 = 0.55 * np.exp(-((x - n * 0.2) ** 2 + (y - n * 0.78) ** 2) / (2 * (n * 0.18) ** 2))
    return np.clip(h + h2, 0, 1)


def demo_patch(name, n=6, inverse=False):
    L = hill(*np.meshgrid(np.arange(n + 1, dtype=np.float64), np.arange(n + 1, dtype=np.float64), indexing="ij"), n)
    if inverse:
        L = 1 - L

    def fn(u, v, px, py):
        i = np.clip(np.floor(u).astype(int), 0, n - 1)
        j = np.clip(np.floor(v).astype(int), 0, n - 1)
        fu, fv = u - i, v - j
        t = L[i, j] * (1 - fu) * (1 - fv) + L[i + 1, j] * fu * (1 - fv) + L[i + 1, j + 1] * fu * fv + L[i, j + 1] * (1 - fu) * fv
        return heat_pixels(name, t, px, py)
    return render_n(fn, n, n)


def legend_bar(name=None, colours=None, w=128, h=10):
    img = np.zeros((h, w, 4), np.float32)
    img[..., 3] = 255
    img[..., :3] = [32, 32, 36]
    x = np.arange(1, w - 1)
    if colours is not None:
        seg = ((x - 1) * len(colours)) // (w - 2)
        row = np.stack([colours[k] for k in seg])
    else:
        row = ramp_rgb(name, (x - 1) / (w - 3))
    img[1:h - 1, 1:w - 1, :3] = row[None]
    return img


def make_infoviews():
    for key, (label, _, _) in RAMPS.items():
        items = [{"file": save("infoviews/%s-%03d.png" % (key, lv), level_tile(key, lv)), "label": "%s %d" % (label, lv)}
                 for lv in LEVELS]
        GROUPS.append({"title": "Info view: %s ramp (0..100)" % label,
                       "note": "11 level tiles, no outline (neighbours merge); dither density follows the ramp",
                       "columns": 6, "scale": 2, "items": items})
    items = [{"file": save("infoviews/category-%02d.png" % i, flat_colour_tile(c, 0.6)), "label": "Cat %d" % (i + 1)}
             for i, c in enumerate(CATEGORY)]
    GROUPS.append({"title": "Info view: category colours", "note": "16 categories, 60% dither", "columns": 8, "scale": 2, "items": items})
    items = []
    for name, c in RESOURCES:
        for lvl, d in enumerate((0.30, 0.55, 0.80), 1):
            items.append({"file": save("infoviews/resource-%s-%d.png" % (name.lower(), lvl), flat_colour_tile(c, d)),
                          "label": "%s %d" % (name, lvl)})
    GROUPS.append({"title": "Info view: resource richness", "note": "6 hues x 3 richness levels (dither density 30/55/80%)",
                   "columns": 6, "scale": 2, "items": items})

    demos, tiles = [], []
    corner_sets = [("N", (1, 0, 0, 0)), ("E", (0, 1, 0, 0)), ("S", (0, 0, 1, 0)), ("W", (0, 0, 0, 1))]
    for key in ("good", "pollution"):
        label = RAMPS[key][0]
        demos.append({"file": save("infoviews/gradient-%s-6x6.png" % key, demo_patch(key)), "label": "%s 6x6 field" % label})
        for tag, cs in corner_sets:
            tiles.append({"file": save("infoviews/gradient-%s-%s.png" % (key, tag.lower()), gradient_tile(key, cs)),
                          "label": "%s %s high" % (label, tag)})
    GROUPS.append({"title": "Smooth gradient demo: 6x6 cells", "note": "Corner values shared between cells, bilinear + Bayer dither between ramp steps: no cell seams",
                   "columns": 2, "scale": 1, "items": demos})
    GROUPS.append({"title": "Smooth gradient tiles (one corner high)", "note": "Bilinear from 4 diamond corners; adjacent tiles with matching corners join seamlessly",
                   "columns": 4, "scale": 2, "items": tiles})

    legends = []
    for key, (label, _, _) in RAMPS.items():
        legends.append({"file": save("infoviews/legend-%s.png" % key, legend_bar(key)), "label": label})
    legends.append({"file": save("infoviews/legend-category.png", legend_bar(colours=[hx(c) for c in CATEGORY])), "label": "Category"})
    legends.append({"file": save("infoviews/legend-resource.png", legend_bar(colours=[hx(c) for _, c in RESOURCES])), "label": "Resource"})
    GROUPS.append({"title": "Legend bars", "note": "128x10, 1px dark border", "columns": 2, "scale": 2, "items": legends})


def main():
    make_zones()
    make_markers()
    make_infoviews()
    write_manifest_fragment(os.path.join(OUT, "_frag", "40-overlays.json"), GROUPS)
    n_png = sum(len(g["items"]) for g in GROUPS)
    print("wrote %d groups, %d manifest items" % (len(GROUPS), n_png))
    for g in GROUPS:
        print("  %-48s %d" % (g["title"], len(g["items"])))


if __name__ == "__main__":
    main()
