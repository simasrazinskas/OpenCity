"""
gen_zon_base.py - shared helpers for gen_zon.py (WP ZON growable lot art).

A frame is (32*CX) x (32*(CY+HR)); the lot footprint is the bottom 32CX x 32CY
rectangle. Buildings are composed per lot (not tiled 1x1 copies), entrance facing
south (bottom). Light from the top-left, 3/4 top-down like genworld.py.
"""
import random

from pngkit import Canvas, rgba, shade, mix, with_alpha  # noqa: F401
import genworld as G
from genworld import H, OUT, WARM, DARKWIN, GLASS  # noqa: F401


# --------------------------------------------------------------------------- fast canvas
class FC(Canvas):
    """Canvas with fast-path rect/blit (same results as pngkit.Canvas)."""

    def rect(self, x, y, w, h, c, blend=True):
        c = rgba(c)
        x0, x1 = max(0, int(x)), min(self.w, int(x + w))
        y0, y1 = max(0, int(y)), min(self.h, int(y + h))
        if x0 >= x1 or y0 >= y1 or (blend and c[3] == 0):
            return
        px, W = self.px, self.w
        if c[3] == 255 or not blend:
            cl = list(c)
            for yy in range(y0, y1):
                b = yy * W
                px[b + x0:b + x1] = [cl[:] for _ in range(x1 - x0)]
            return
        sa = c[3] / 255.0
        for yy in range(y0, y1):
            b = yy * W
            for xx in range(x0, x1):
                d = px[b + xx]
                da = d[3] / 255.0
                oa = sa + da * (1 - sa)
                for i in range(3):
                    d[i] = int(round((c[i] * sa + d[i] * da * (1 - sa)) / oa))
                d[3] = int(round(oa * 255))

    def blit(self, src, dx, dy, blend=True):
        sw = src.w
        for y in range(src.h):
            ty = dy + y
            if not 0 <= ty < self.h:
                continue
            row = y * sw
            for x in range(sw):
                c = src.px[row + x]
                a = c[3]
                tx = dx + x
                if not 0 <= tx < self.w:
                    continue
                if a == 255 or not blend:
                    self.px[ty * self.w + tx] = c[:]
                elif a:
                    self.blend(tx, ty, c)


def layers(w, h):
    return FC(w, h), FC(w, h)


def finish(b, g):
    b.outline(OUT)
    g.blit(b, 0, 0)
    return g


# --------------------------------------------------------------------------- lot geometry
class Lot:
    """Geometry + identity of one frame."""

    def __init__(self, zone, cx, cy, hr, level, v):
        self.zone, self.cx, self.cy, self.hr = zone, cx, cy, hr
        self.level, self.v = level, v
        self.tier = {1: 1, 2: 1, 3: 2, 4: 2, 5: 3}[level]
        self.up = level in (2, 4)
        self.fw, self.fd = 32 * cx, 32 * cy
        self.W, self.H = self.fw, 32 * (cy + hr)
        self.y0 = 32 * hr          # top of the footprint
        self.yB = self.H           # bottom of the footprint (exclusive)

    def rng(self, *k):
        return random.Random(f"opencity-zon:{self.zone}:{self.cx}x{self.cy}:t{self.tier}:{self.v}:" + ":".join(map(str, k)))

    def seed(self, *k):
        return self.rng(*k).randrange(1 << 30)

    top = 4

    def hfit(self, yb, d, want, top=None):
        """Clamp a wall height so the roof (plus self.top px of crown) stays inside the frame."""
        return max(3, min(int(want), yb - d - (self.top if top is None else top)))


# --------------------------------------------------------------------------- shadows
def shadow(g, L, x, yb, w, d, h, alpha=70, sx=None, sy=None):
    """Swept-rect cast shadow toward the bottom-right, clipped to the footprint."""
    sx = 2 + int(h * 0.30) if sx is None else sx
    sy = 1 + int(h * 0.18) if sy is None else sy
    top, bot = yb - d, yb + sy
    col = (10, 18, 30, alpha)
    for y in range(max(top, L.y0), min(bot, L.yB)):
        # t range where the swept rect covers row y
        t0 = 0.0 if y < yb else (y - yb + 1) / max(1, sy)
        t1 = 1.0 if y >= top + sy else (y - top) / max(1, sy)
        xa = int(x + sx * t0)
        xb = int(x + w + sx * t1)
        g.rect(max(0, xa), y, min(L.W, xb) - max(0, xa), 1, col)


# --------------------------------------------------------------------------- grounds
def ground(g, L, col, noise=0.04, seed=0, edge=True):
    g.rect(0, L.y0, L.W, L.fd, col)
    if noise:
        g.noise(noise, seed=seed, x=0, y=L.y0, w=L.W, h=L.fd)
    if edge:
        g.rect(0, L.y0, L.W, 1, with_alpha(shade(col, 0.82), 255))
        g.rect(0, L.y0, 1, L.fd, with_alpha(shade(col, 0.9), 255))


def lawn(g, x, y, w, h, base=H("78BE55"), seed=0, edge=False):
    g.rect(x, y, w, h, base)
    r = random.Random(seed)
    for _ in range(max(1, w * h // 9)):
        g.set(x + r.randrange(max(1, w)), y + r.randrange(max(1, h)), shade(base, r.choice([0.9, 1.08])))
    if edge:
        g.rect_outline(x, y, w, h, shade(base, 0.8))


def paving(g, x, y, w, h, col=H("CFCBC2"), step=6):
    g.rect(x, y, w, h, col)
    lc = with_alpha(shade(col, 0.86), 150)
    for yy in range(y + step - 1, y + h, step):
        g.rect(x, yy, w, 1, lc)
    for xx in range(x + step - 1, x + w, step):
        g.rect(xx, y, 1, h, with_alpha(shade(col, 0.9), 110))


def asphalt(g, x, y, w, h, seed=0):
    g.rect(x, y, w, h, H("5B6068"))
    g.noise(0.05, seed=seed, x=x, y=y, w=w, h=h)


def parking_rows(g, x, y, w, h, seed, horizontal_bays=False, fill=0.6):
    """Asphalt lot with bays (5px stalls, 8px deep) and parked cars."""
    asphalt(g, x, y, w, h, seed)
    r = random.Random(seed)
    yy = y + 1
    while yy + 7 <= y + h:
        for xx in range(x + 1, x + w - 4, 5):
            g.rect(xx, yy, 1, 6, (235, 235, 235, 150))
            if r.random() < fill and xx + 4 < x + w:
                G.car_top(g, xx + 1, yy + 1, r.choice(G.CARCOLS), horizontal=False)
        yy += 9
        if yy + 7 <= y + h:
            g.rect(x + 1, yy - 2, w - 2, 1, (235, 235, 235, 60))


def hedge(g, x, y, w, h=2, col=H("3E8E45")):
    g.rect(x, y, w, h, col)
    g.rect(x, y, w, 1, shade(col, 1.25))
    g.rect(x, y + h, w, 1, (10, 18, 30, 60))


def pool(g, x, y, w, h):
    g.rect(x - 1, y - 1, w + 2, h + 2, H("EDEBE4"))
    g.rect(x, y, w, h, H("3FA7D6"))
    g.rect(x, y, w, 1, H("2C7FA8"))
    for i in range(1, w - 2, 4):
        g.blend(x + i, y + 1 + (i // 4) % max(1, h - 2), (255, 255, 255, 120))


def trees(g, pts, L, cols=(H("3F9B4A"), H("52A857"), H("2F8A44"))):
    r = L.rng("trees", len(pts))
    for (x, y, rad) in pts:
        G.tree(g, x, y, rad, r.choice(cols))


def street_trees(g, L, y, step=12, rad=3, x0=4):
    for x in range(x0, L.W - 2, step):
        G.tree(g, x, y, rad, H("3F9B4A") if (x // step) % 2 else H("52A857"))


def car(g, x, y, col, horizontal=True):
    G.car_top(g, x, y, col, horizontal)


def truck(b, x, y, col, horizontal=True):
    """Small lorry (top view): cab + box."""
    if horizontal:
        b.rect(x, y, 10, 5, H("E6E6E6"))
        b.rect(x, y, 10, 1, H("FFFFFF"))
        b.rect(x + 10, y + 1, 3, 4, col)
        b.rect(x + 10, y + 1, 3, 1, shade(col, 1.3))
    else:
        b.rect(x, y + 3, 5, 10, H("E6E6E6"))
        b.rect(x, y + 3, 1, 10, H("FFFFFF"))
        b.rect(x, y, 5, 3, col)
        b.rect(x, y, 5, 1, shade(col, 1.3))


# --------------------------------------------------------------------------- building parts
def part(b, g, L, x, yb, w, d, h, roof, wall, style=None, seed=0, lit=0.35, accent=None, shad=True):
    """Shadowed box with an optional facade style. Returns the roof rect."""
    if shad:
        shadow(g, L, x, yb, w, d, h)
    rr = G.box3d(b, x, yb, w, d, h, roof, wall)
    if style:
        G.facade(b, x, rr[1] + rr[3], w, h, style, seed, accent=accent, lit=lit)
    return rr


def draw_parts(b, g, L, parts):
    """parts: dicts with x, yb, w, d, h, roof, wall, style, seed, lit, accent, then
    optional callback 'after(rr)'. Shadows first, then back-to-front."""
    for p in parts:
        p["h"] = L.hfit(p["yb"], p["d"], p["h"])
        shadow(g, L, p["x"], p["yb"], p["w"], p["d"], p["h"])
    out = []
    for p in sorted(parts, key=lambda p: (p.get("z", 0), p["yb"], p["h"])):
        rr = part(b, g, L, p["x"], p["yb"], p["w"], p["d"], p["h"], p["roof"], p["wall"], p.get("style"),
                  p.get("seed", 0), p.get("lit", 0.35), p.get("accent"), shad=False)
        if p.get("after"):
            p["after"](rr, p)
        if p.get("deco"):
            p["deco"](rr, p)
        out.append((rr, p))
    return out


def fine_windows(b, x, y, w, h, seed, lit=0.3, step_x=3, step_y=3, lit_col=WARM, dark=DARKWIN):
    """Dense 1px windows (cheap housing)."""
    r = random.Random(seed)
    for yy in range(y + 1, y + h - 1, step_y):
        for xx in range(x + 1, x + w - 1, step_x):
            b.set(xx, yy, lit_col if r.random() < lit else dark)


def balconies(b, x, y, w, h, floor_h=5, col=(255, 255, 255, 90), step=6, seed=0):
    r = random.Random(seed)
    for fy in range(y + floor_h - 1, y + h - 2, floor_h):
        for xx in range(x + 1, x + w - 4, step):
            if r.random() < 0.8:
                b.rect(xx, fy, 4, 1, col)


def laundry(b, x, y, w, seed):
    r = random.Random(seed)
    b.hline(x, x + w - 1, y, (220, 220, 220, 160))
    for xx in range(x + 1, x + w - 1, 2):
        if r.random() < 0.6:
            b.set(xx, y + 1, r.choice([H("F26B6B"), H("FFFFFF"), H("6FA8DC"), H("F7D154")]))


def parapet(b, rr, col):
    x, y, w, d = rr
    b.rect(x, y, w, 1, shade(col, 1.25))
    b.rect(x, y, 1, d, shade(col, 1.15))
    b.rect(x + 1, y + 1, w - 2, 1, shade(col, 0.85))


def roof_gear(b, rr, L, density=1.0, solar=False, ac=True, sky=False, tank=False):
    """Tidy roof equipment inside roof rect rr (x, y, w, d): AC row at the back,
    optional solar array in the middle, skylights, water tank."""
    x, y, w, d = rr
    r = L.rng("roofgear", x, y)
    if w < 8 or d < 6:
        return
    xs = list(range(x + 3, x + w - 6, 7))
    if ac:
        for px in xs:
            if r.random() < 0.55 * density:
                G.ac_unit(b, px, y + 2)
    if solar and d >= 10:
        rows = max(1, min(3, (d - 8) // 6))
        sw = max(6, min(w - 6, 10 * max(1, (w - 6) // 10)))
        for k in range(rows):
            G.solar_panel(b, x + 3, y + 6 + k * 6, sw, 4)
    elif sky and d >= 10:
        for px in xs[1::2]:
            G.skylight(b, px, y + d // 2, 4, 3)
    if tank and w >= 12 and d >= 9:
        wx, wy = x + w - 8, y + d - 8
        b.rect(wx + 1, wy + 5, 1, 2, H("6B4A32"))
        b.rect(wx + 4, wy + 5, 1, 2, H("6B4A32"))
        b.rect(wx, wy, 6, 5, H("8C6A4F"))
        b.rect(wx, wy, 6, 1, H("B08A6A"))
    if d >= 14 and w >= 14:  # stair/lift housing
        hx = x + 3 + (r.randrange(max(1, w - 12)))
        hy = y + d - 7
        b.rect(hx, hy, 6, 4, shade(b.get(x + 1, y + 2), 0.92))
        b.rect(hx, hy + 4, 6, 1, (10, 18, 30, 90))
        b.rect(hx, hy, 6, 1, (255, 255, 255, 70))


def antenna(b, x, y, h=5, light=True):
    b.rect(x, y - h, 1, h, H("7B828E"))
    if light:
        b.set(x, y - h - 1, H("E84545"))


def helipad(b, cx, cy, s=7):
    b.rect(cx - s // 2, cy - s // 2, s, s, H("4B5260"))
    b.rect_outline(cx - s // 2, cy - s // 2, s, s, H("F4F4F4"))
    b.rect(cx - 1, cy - 2, 1, 5, H("F4F4F4"))
    b.rect(cx + 1, cy - 2, 1, 5, H("F4F4F4"))
    b.rect(cx, cy, 1, 1, H("F4F4F4"))


def roof_garden(b, rr, seed):
    x, y, w, d = rr
    if w < 10 or d < 8:
        return
    lawn(b, x + 2, y + 2, w - 4, d - 4, base=H("6DB04E"), seed=seed)
    r = random.Random(seed)
    for _ in range(max(1, w * d // 120)):
        G.bush(b, x + 3 + r.randrange(max(1, w - 6)), y + 3 + r.randrange(max(1, d - 6)))


def awnings(b, x, y, w, cols, seg=8):
    """Row of striped shop awnings along y (cols = list of colours)."""
    i = 0
    for xx in range(x, x + w - 3, seg):
        col = cols[i % len(cols)]
        sw = min(seg - 1, x + w - xx)
        for k in range(sw):
            b.set(xx + k, y, col if (k // 2) % 2 == 0 else H("F4F4F4"))
            b.set(xx + k, y + 1, shade(col if (k // 2) % 2 == 0 else H("F4F4F4"), 0.8))
        b.rect(xx, y + 2, sw, 1, (10, 18, 30, 70))
        i += 1


def shopfront(b, x, y, w, h=4, seed=0):
    b.rect(x, y, w, h, mix(H("A8D4EE"), H("5A90B8"), 0.4))
    for xx in range(x, x + w, 4):
        b.vline(xx, y, y + h - 1, (255, 255, 255, 90))
    r = random.Random(seed)
    for xx in range(x + 1, x + w - 1, 3):
        if r.random() < 0.4:
            b.set(xx, y + h - 2, (255, 226, 150, 220))
