"""
iso_ui_world - placeholder 2:1 isometric city (64x32 tiles) for the UI mockups and build-menu thumbnails.
Not final world art: the lead swaps in real isokit scenes later. Deterministic (fixed seed).
Projection per BRIEF: sx = (x - y) * 32 * t, sy = (x + y) * 16 * t - z * t (t = tile scale, 1 = 64x32).
"""

import numpy as np

from iso_ui_kit import Canvas, hexc

G = {
    "grass": [hexc("#4f8a2c"), hexc("#5a9632"), hexc("#477e28")],
    "road": hexc("#5a5a60"), "road_edge": hexc("#8a8a8e"), "mark": hexc("#e8d860"), "walk": hexc("#a8a49a"),
    "water": [hexc("#2a5cb0"), hexc("#3a70c4")], "sand": hexc("#d8c890"),
    "zone_r": hexc("#5aa83a"), "zone_c": hexc("#3a78c8"), "zone_i": hexc("#c8a83a"),
}


def ground(w, h, cells, ox, oy, t=1.0):
    """Vectorized ground: cells(x, y) -> kind array lookup. Returns Canvas."""
    c = Canvas(w, h, "#000000")
    ys, xs = np.mgrid[0:h, 0:w]
    sx = (xs - ox + 0.5) / (32 * t)
    sy = (ys - oy + 0.5) / (16 * t)
    X = (sx + sy) / 2
    Y = (sy - sx) / 2
    cx, cy = np.floor(X).astype(int), np.floor(Y).astype(int)
    fx, fy = X - cx, Y - cy
    kind = cells(cx, cy)
    a = c.a
    rng = np.random.RandomState(7)
    noise = rng.randint(0, 3, size=(h, w))
    chk = ((xs // 2 + ys) % 3 == 0)
    gr = np.array(G["grass"])
    a[:] = gr[np.where(chk, noise, 0)]
    # roads: kind 1 = road along x, 2 = road along y, 3 = crossing
    road = kind >= 1
    road &= kind <= 3
    a[road] = G["road"]
    edge_y = road & ((kind == 1) | (kind == 3)) & ((fy < 0.12) | (fy > 0.88))
    edge_x = road & ((kind == 2) | (kind == 3)) & ((fx < 0.12) | (fx > 0.88))
    a[(edge_y & (kind == 1)) | (edge_x & (kind == 2))] = G["walk"]
    dash_x = (kind == 1) & (np.abs(fy - 0.5) < 0.035) & ((X * 4).astype(int) % 2 == 0)
    dash_y = (kind == 2) & (np.abs(fx - 0.5) < 0.035) & ((Y * 4).astype(int) % 2 == 0)
    a[dash_x | dash_y] = G["mark"]
    water = kind == 5
    wv = (((xs // 3) + (ys // 2) * 3) % 11 == 0)
    a[water] = G["water"][0]
    a[water & wv] = G["water"][1]
    a[kind == 6] = G["sand"]
    for k, nm in ((7, "zone_r"), (8, "zone_c"), (9, "zone_i")):
        m = (kind == k) & ((fx < 0.06) | (fx > 0.94) | (fy < 0.06) | (fy > 0.94))
        a[m] = G[nm]
    a[:, :, 3] = 255
    return c


def _poly_mask(c, pts):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1 = max(0, int(min(xs))), min(c.w, int(max(xs)) + 1)
    y0, y1 = max(0, int(min(ys))), min(c.h, int(max(ys)) + 1)
    if x1 <= x0 or y1 <= y0:
        return None
    Y, X = np.mgrid[y0:y1, x0:x1] + 0.5
    inside = np.zeros(X.shape, bool)
    for i in range(len(pts)):
        ax, ay = pts[i]; bx, by = pts[(i + 1) % len(pts)]
        if ay == by:
            continue
        cond = (Y >= min(ay, by)) & (Y < max(ay, by))
        xi = ax + (Y - ay) * (bx - ax) / (by - ay)
        inside ^= cond & (X < xi)
    return (y0, y1, x0, x1, inside, X, Y)


def building(c, ox, oy, x0, y0, x1, y1, h, wall, roof, t=1.0, windows=True, roof_kind="flat", lit=False):
    """Box building over world rect [x0,x1]x[y0,y1] (cells), height h px at 1x. wall/roof: (light, mid, dark)."""
    S = lambda x, y, z: (ox + (x - y) * 32 * t, oy + (x + y) * 16 * t - z * t)
    win_c = hexc("#ffe890") if lit else hexc("#2c3c58")
    win_l = hexc("#8ab0d8")
    # +Y face (lit, lower-left)
    for face in ("y", "x"):
        if face == "y":
            pts = [S(x0, y1, 0), S(x1, y1, 0), S(x1, y1, h), S(x0, y1, h)]
            base = wall[0]
        else:
            pts = [S(x1, y1, 0), S(x1, y0, 0), S(x1, y0, h), S(x1, y1, h)]
            base = wall[2]
        r = _poly_mask(c, pts)
        if r is None:
            continue
        ya, yb, xa, xb, m, X, Y = r
        sub = c.a[ya:yb, xa:xb]
        sub[m] = base
        if windows and h >= 9:
            sx = (X - ox) / (32 * t)
            if face == "y":
                u = sx + y1
                z = ((u + y1) * 16 * t - (Y - oy)) / t
            else:
                u = x1 - sx
                z = ((x1 + u) * 16 * t - (Y - oy)) / t
            fu = (u * 4) % 1.0
            fz = (z % 10)
            wm = m & (fu > 0.3) & (fu < 0.75) & (fz > 3) & (fz < 8) & (z > 4) & (z < h - 2)
            sub[wm] = win_c if (face == "y" or lit) else win_c
            sub[wm & (fz > 6.6) & (face == "y")] = win_l if not lit else win_c
    top = [S(x1, y1, h), S(x0, y1, h), S(x0, y0, h), S(x1, y0, h)]
    if roof_kind == "gable":
        zr = h + (x1 - x0) * 10
        ym = (y0 + y1) / 2
        for pts, colr in (([S(x0, y1, h), S(x1, y1, h), S(x1, ym, zr), S(x0, ym, zr)], roof[0]),
                          ([S(x1, y1, h), S(x1, y0, h), S(x1, ym, zr)], wall[1]),
                          ([S(x0, y0, h), S(x1, y0, h), S(x1, ym, zr), S(x0, ym, zr)], roof[2])):
            r = _poly_mask(c, pts)
            if r:
                ya, yb, xa, xb, m, _, _ = r
                c.a[ya:yb, xa:xb][m] = colr
    else:
        r = _poly_mask(c, top)
        if r:
            ya, yb, xa, xb, m, X, Y = r
            c.a[ya:yb, xa:xb][m] = roof[1]
        # parapet lines (front edges)
        for (a, b) in ((S(x0, y1, h), S(x1, y1, h)), (S(x1, y1, h), S(x1, y0, h))):
            n = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
            for i in range(n):
                px = int(a[0] + (b[0] - a[0]) * i / max(1, n - 1)); py = int(a[1] + (b[1] - a[1]) * i / max(1, n - 1))
                c.fill(px, py, 1, 1, roof[0])


def tree(c, ox, oy, x, y, t=1.0, s=1.0, dark=False):
    sx, sy = ox + (x - y) * 32 * t, oy + (x + y) * 16 * t
    tr = max(1, int(2 * t))
    c.fill(sx - tr // 2, sy - 8 * t * s, tr, 8 * t * s, hexc("#5a3a1c"))
    cols = [hexc("#2a5a1c"), hexc("#3c7a24"), hexc("#58a032")]
    for i, (dx, dy, r) in enumerate(((0, -14, 7), (-3, -12, 5), (3, -16, 4.5))):
        cx, cy, rr = sx + dx * t * s, sy + dy * t * s, r * t * s
        x0, x1 = int(cx - rr - 1), int(cx + rr + 2); y0, y1 = int(cy - rr - 1), int(cy + rr + 2)
        x0, y0 = max(0, x0), max(0, y0); x1, y1 = min(c.w, x1), min(c.h, y1)
        if x1 <= x0 or y1 <= y0:
            continue
        YY, XX = np.mgrid[y0:y1, x0:x1] + 0.5
        d = ((XX - cx) ** 2 + (YY - cy) ** 2) / (rr * rr)
        sub = c.a[y0:y1, x0:x1]
        sub[d <= 1] = cols[0]
        sub[(d <= 0.75) & ((XX - cx) + (YY - cy) < rr * 0.3)] = cols[1]
        sub[(d <= 0.35) & ((XX - cx) + (YY - cy) < -rr * 0.2)] = cols[2]


WALLS = {
    "house": ([hexc("#ecdcb4"), hexc("#d0c098"), hexc("#a89874")], [hexc("#b8402c"), hexc("#c84a32"), hexc("#7e2a1c")]),
    "house2": ([hexc("#c8d8e8"), hexc("#a8b8c8"), hexc("#8090a4")], [hexc("#4a5a8a"), hexc("#5a6a9a"), hexc("#323e64")]),
    "flats": ([hexc("#d8ccb8"), hexc("#bcb09c"), hexc("#948a78")], [hexc("#6a6460"), hexc("#86807a"), hexc("#504a46")]),
    "brick": ([hexc("#c0684a"), hexc("#a4583e"), hexc("#80442e")], [hexc("#5a5a60"), hexc("#7a7a80"), hexc("#4a4a50")]),
    "shop": ([hexc("#a8d0f0"), hexc("#88b0d8"), hexc("#6488b0")], [hexc("#3a6ab0"), hexc("#5a8ad0"), hexc("#2a4a80")]),
    "office": ([hexc("#9cc4dc"), hexc("#80a8c4"), hexc("#5a7c98")], [hexc("#6a7a8a"), hexc("#8a9aa8"), hexc("#4a5a68")]),
    "factory": ([hexc("#d8c070"), hexc("#bca458"), hexc("#94803e")], [hexc("#8a8a80"), hexc("#a4a49a"), hexc("#6a6a62")]),
}


def scene(w, h, seed=3, t=1.0, ox=None, oy=None, lit=False):
    """Placeholder city: road grid, mixed zones, parks and a river. Returns Canvas."""
    rng = np.random.RandomState(seed)
    ox = w // 2 if ox is None else ox
    oy = -h // 2 if oy is None else oy

    def kinds(cx, cy):
        k = np.zeros(cx.shape, int)
        k[cy % 6 == 0] = 1
        k[cx % 7 == 0] = 2
        k[(cy % 6 == 0) & (cx % 7 == 0)] = 3
        d = np.abs((cx - cy) - 3 - ((cx + cy) // 9) % 2)
        k[(d <= 1) & (cx + cy > 28)] = 5
        k[(d == 2) & (cx + cy > 28)] = 6
        return k
    c = ground(w, h, kinds, ox, oy, t)
    span = int((w + h * 2) / (32 * t)) + 4
    items = []
    for cx in range(-span, span):
        for cy in range(-span, span):
            sx, sy = ox + (cx - cy) * 32 * t, oy + (cx + cy) * 16 * t
            if not (-80 * t < sx < w + 80 * t and -10 * t < sy < h + 120 * t):
                continue
            if kinds(np.array([cx]), np.array([cy]))[0] != 0:
                continue
            if abs((cx - cy) - 3) <= 3 and cx + cy > 26:
                continue
            items.append((cx + cy, cx, cy))
    items.sort()
    for _, cx, cy in items:
        bx, by = cx // 7, cy // 6
        zone = (bx * 5 + by * 3) % 7
        r = rng.rand()
        if zone == 0:  # park
            if r < 0.7:
                tree(c, ox, oy, cx + 0.5, cy + 0.5, t)
            continue
        if zone in (1, 2):
            kind = "house" if r < 0.6 else "house2"
            building(c, ox, oy, cx + 0.15, cy + 0.15, cx + 0.85, cy + 0.85, 12, *WALLS[kind], t, True, "gable", lit)
            if r > 0.8:
                tree(c, ox, oy, cx + 0.9, cy + 0.95, t, 0.7)
        elif zone in (3, 4):
            kind = "flats" if r < 0.5 else "brick"
            building(c, ox, oy, cx + 0.08, cy + 0.08, cx + 0.92, cy + 0.92, int(18 + r * 30), *WALLS[kind], t, True, "flat", lit)
        elif zone == 5:
            kind = "office" if r < 0.6 else "shop"
            building(c, ox, oy, cx + 0.06, cy + 0.06, cx + 0.94, cy + 0.94, int(24 + r * 70), *WALLS[kind], t, True, "flat", lit)
        else:
            building(c, ox, oy, cx + 0.05, cy + 0.05, cx + 0.95, cy + 0.95, int(14 + r * 10), *WALLS["factory"], t, False, "flat", lit)
    return c
