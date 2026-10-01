"""gen_zon_res2.py - dense residential lots: res-med, res-high, res-lowrent, res-mixed."""
import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (layers, finish, lawn, paving, hedge, trees, street_trees, draw_parts,
                          roof_gear, roof_garden, antenna, helipad, fine_windows, balconies,
                          laundry, awnings, shopfront, parking_rows, asphalt, parapet,
                          pk, tiled, dormers, mansard, cobbles, cafe, light_up, planters, boost,
                          EU_SLATE, EU_ZINC, EU_BRICK, SHUTTER, FRAME)
import random

# entries 0-3 North American, 4-7 European
MED_WALL = [H("D9A37E"), H("E8DCC4"), H("C7D3CF"), H("F0D8C0"),
            H("F3E3C3"), H("EBC6A4"), H("F2D7CF"), H("DCE5D0")]
MED_ROOF = [H("8B4A3C"), H("5F6670"), H("6E5A4A"), H("4E5A6E"),
            H("B4553A"), H("9C4A36"), H("C46A44"), H("8A4B3B")]
HI_WALL = [H("E6DCCB"), H("C98F6F"), H("D5DCE2"), H("E9C9A4"),
           H("F1E4CC"), H("D9B48F"), H("F2D7CF"), H("E6E2D0")]
HI_ROOF = [H("9AA0A8"), H("8E857A"), H("7F8E9A"), H("A39A8C"),
           H("8E96A0"), H("A4ABB2"), H("7C8A94"), H("9AA0A6")]
HI_GLASS = [H("6FA3CC"), H("5C8DB8"), H("78A9C4"), H("6A98C2"),
            H("7FB0B5"), H("6FA5A0"), H("86B5AE"), H("78A8B8")]
LR_WALL = [H("B8B4A8"), H("D2C6A5"), H("A9B8A6"), H("AFC0CC"),
           H("CFCBC0"), H("D9D2BE"), H("C3CCC4"), H("C6CDD4")]
LR_ROOF = [H("8A8A84"), H("948C78"), H("7E8A7C"), H("86919A"),
           H("6E6E6A"), H("78746A"), H("666F68"), H("6C757E")]
MX_WALL = [H("E8C9A0"), H("D7E0E8"), H("E9B8A8"), H("DCD6C4"),
           H("F6EFDF"), H("F3E3C3"), H("F2E8D2"), H("EFE2C8")]
MX_ROOF = [H("A08C78"), H("8D99A6"), H("9C8478"), H("8F8A7C"),
           EU_SLATE[0], EU_SLATE[1], EU_SLATE[2], EU_SLATE[3]]
EU_AWN = [H("8A3A30"), H("2F5E4A"), H("2F5E8A"), H("6B2F4A"), H("A0522D"), H("3E4A6B")]
PASTEL = [H("E8B7A8"), H("A9CDB8"), H("F0DDA0"), H("A9C3E0")]
SHOP = [H("2C6FD1"), H("E67E22"), H("1FA37A"), H("E8467C"), H("C0392B"), H("8E44AD")]


def P(x, yb, w, d, h, roof, wall, style="res", seed=0, lit=0.35, after=None, accent=None):
    return dict(x=x, yb=yb, w=w, d=d, h=h, roof=roof, wall=wall, style=style, seed=seed, lit=lit,
                after=after, accent=accent)


def plaza(g, L, col=H("CFCBC2"), green=True):
    paving(g, 0, L.y0, L.W, L.fd, col)
    g.rect(0, L.y0, L.W, 1, shade(col, 0.8))
    if green:
        lawn(g, 2, L.yB - 6, L.W - 4, 4, seed=L.seed("strip"))


def roof_dress(b, L, rr, kind):
    x, y, w, d = rr
    parapet(b, rr, b.get(x + w // 2, y + d // 2))
    if kind == "garden":
        roof_garden(b, rr, L.seed("rg", x))
    elif kind == "solar":
        roof_gear(b, rr, L, 0.6, solar=True)
    else:
        roof_gear(b, rr, L, 1.0, tank=(kind == "tank"))


def eu_roof(b, rr, p, T, lit):
    """European roof: tiled gable with dormers + chimney (T1), slate/zinc mansard with dormers (T2+)."""
    x, y, w, d = rr
    if T == 1:
        vert = p["w"] < p["d"]
        tiled(b, x, y, w, d, p["roof"], vert)
        if not vert and d >= 10:
            dormers(b, x, y + d - 5, w, p["roof"], FRAME, lit, p["seed"])
        if w >= 12 and d >= 8:
            b.rect(x + w // 3, y + 2, 2, 3, EU_BRICK[0])
            b.rect(x + w // 3, y + 2, 2, 1, shade(EU_BRICK[0], 1.3))
    else:
        mansard(b, rr, p["roof"], EU_ZINC, FRAME, lit, p["seed"])


def uplights(b, out, L, frac=0.9):
    """Boost: also light the dormer windows of pitched/mansard roofs."""
    if L.boost:
        for rr, p in out:
            light_up(b, rr[0], rr[1], rr[2], rr[3], frac, L.seed("lr", p["x"], p["yb"]))


# =========================================================================== res-med
def res_med(L):
    g, b = layers(L.W, L.H)
    v, T, fw, y0, yB = L.lv, L.tier, L.fw, L.y0, L.yB
    roof, wall = pk(MED_ROOF, L), pk(MED_WALL, L, v + T - 1)
    if L.eu and T > 1:
        roof = EU_SLATE[v]
    lit = 0.35 + 0.2 * L.up
    plaza(g, L, H("D6D0C4"), green=False)
    if L.eu:
        cobbles(g, 0, y0, L.W, L.fd, H("C4BBAB"), seed=L.seed("cob"))
    yk = y0 + 4
    yF = yB - 7
    hh = {1: 15, 2: 26, 3: 36}[T]
    dB = 14 + 2 * T
    ww = 14 + 2 * T
    s = L.seed("med")
    lawn(g, 2, yk + dB, fw - 4, yF - yk - dB, seed=s)
    g.rect(fw // 2 - 2, yk + dB, 4, yB - yk - dB, H("D6D0C4"))  # courtyard path
    for k in range(2):
        G.bench(g, fw // 2 + 4, yk + dB + 6 + 8 * k) if yk + dB + 14 < yF else None
    parts = []
    if v == 0:      # U open to the south, courtyard garden
        if L.eu:    # closed perimeter block: back wing, two side wings, low gate wing in front
            fh_ = 10
            parts.append(P(3, yk + dB, fw - 6, dB - 2, hh, roof, wall, seed=s, lit=lit))
            parts.append(P(3, yF, ww, yF - yk, L.hfit(yF, yF - yk, hh - 2), roof, shade(wall, 0.97), seed=s + 1, lit=lit))
            parts.append(P(fw - 3 - ww, yF, ww, yF - yk, L.hfit(yF, yF - yk, hh - 2), roof, shade(wall, 0.97), seed=s + 2, lit=lit))
            parts.append(P(3, yF, fw - 6, 7, fh_, roof, shade(wall, 1.03), seed=s + 3, lit=lit))
            parts[-1]["gate"] = True
            cy_ = (yk + dB + yF - 7 - fh_) // 2
            if yF - 7 - fh_ - yk - dB >= 9:     # courtyard garden: gravel cross, fountain, trees
                g.rect(3 + ww, cy_ - 1, fw - 6 - 2 * ww, 2, H("D9D0BC"))
                g.circle(fw // 2, cy_, 4.5, H("EDEBE4"))
                g.circle(fw // 2, cy_, 3.4, H("3FA7D6"))
                g.set(fw // 2, cy_, H("DFF3FF"))
                trees(g, [(3 + ww + 5, cy_ - 2, 3), (fw - 3 - ww - 6, cy_ - 2, 3)], L)
        else:
            parts.append(P(3, yk + dB, fw - 6, dB, hh, roof, wall, seed=s, lit=lit))
            parts.append(P(3, yF, ww, yF - yk, L.hfit(yF, yF - yk, hh - 2), roof, shade(wall, 0.97), seed=s + 1, lit=lit))
            parts.append(P(fw - 3 - ww, yF, ww, yF - yk, L.hfit(yF, yF - yk, hh - 2), roof, shade(wall, 0.97), seed=s + 2, lit=lit))
    elif v == 1:    # L-shape + playground
        parts.append(P(3, yk + dB, fw - 6, dB, hh, roof, wall, seed=s, lit=lit))
        parts.append(P(fw - 3 - ww, yF, ww, yF - yk, L.hfit(yF, yF - yk, hh), roof, wall, seed=s + 1, lit=lit))
        px, py = 6, yF - 14
        g.rect(px, py, 14, 10, H("E8D8A0"))
        g.rect_outline(px, py, 14, 10, H("B08A5A"))
        g.rect(px + 2, py + 2, 4, 1, H("D94F4F"))
        g.rect(px + 8, py + 4, 1, 4, H("2C6FD1"))
    elif v == 2:    # twin gable-fronted blocks with a garden between
        bw = (fw - 14) // 2
        for i in range(2):
            yy = yF - 3 * i
            parts.append(P(4 + i * (bw + 6), yy, bw, yy - yk - 4, L.hfit(yy, yy - yk - 4, hh + 2 * i),
                           roof, shade(wall, 1 - 0.05 * i), seed=s + i, lit=lit))
    else:           # long slab set back, front garden with parking bays
        parts.append(P(3, yk + dB + 8, fw - 6, dB + 8, hh + 2, roof, wall, seed=s, lit=lit))
        if L.eu:    # cobbled forecourt with clipped hedges
            cobbles(g, 4, yF - 10, fw - 8, 9, H("B9AFA0"), seed=s)
            for hx in range(6, fw - 12, 16):
                hedge(g, hx, yF - 8, 10, 2)
        else:
            parking_rows(g, 4, yF - 10, fw - 8, 9, s)
    if L.eu:
        for p in parts:
            p["style"] = "eu"
            p["accent"] = SHUTTER[(v + T + p["seed"]) % 4]

        def after(rr, p):
            eu_roof(b, rr, p, T, lit)
    elif T == 1:
        def after(rr, p):
            G.pitched(b, rr[0], rr[1], rr[2], rr[3], p["roof"], ridge_vertical=p["w"] < p["d"])
    else:
        def after(rr, p):
            roof_dress(b, L, rr, "garden" if T == 3 and p["w"] >= 20 else ("solar" if L.up else "gear"))
    for p in parts:
        p["after"] = after
    def deco(rr, p):
        if T >= 2 and not L.eu:
            balconies(b, p["x"], rr[1] + rr[3], p["w"], p["h"], 5, seed=p["seed"])
        if p.get("gate"):   # archway through the gate wing
            ah = min(7, p["h"] - 2)
            ax = p["x"] + p["w"] // 2 - 3
            b.rect(ax, p["yb"] - ah, 6, ah, (34, 30, 36, 255))
            b.rect(ax + 2, p["yb"] - ah + 1, 2, 2, H("6FA05A"))     # garden glimpse
            b.rect(ax - 1, p["yb"] - ah - 1, 8, 1, FRAME)
            b.rect(ax - 1, p["yb"] - ah, 1, ah, FRAME)
            b.rect(ax + 6, p["yb"] - ah, 1, ah, FRAME)
        elif L.eu:
            G.door(b, p["x"] + p["w"] // 2 - 1, p["yb"] - 4, 3, 4, H("2F5E4A"))
            b.rect(p["x"] + p["w"] // 2 - 2, p["yb"] - 5, 5, 1, FRAME)
        else:
            G.door(b, p["x"] + p["w"] // 2 - 1, p["yb"] - 4, 3, 4, H("5E4632"))
        if L.up:
            for xx in range(p["x"] + 2, p["x"] + p["w"] - 3, 5):
                G.flowerbed(b, xx, p["yb"] - 6, 2, seed=xx)
    for p in parts:
        p["deco"] = deco
    out = draw_parts(b, g, L, parts)
    boost(b, g, L, out, lit=0.75, terrace=T >= 2 and not L.eu)
    if L.boost:
        if L.eu:
            uplights(b, out, L)
        planters(g, 3, yF + 1, fw - 6, seed=L.seed("pl"))
    street_trees(g, L, yB - 4, 14 if not L.up else 10, 3)
    if (L.up or T == 3) and not (L.eu and v == 0):
        trees(g, [(fw // 2, yF - 6, 4)], L)
    if L.boost:
        trees(g, [(fw // 2 - 9, yF - 10, 3), (fw // 2 + 9, yF - 10, 3)], L)
    return finish(b, g)


# =========================================================================== res-high
def tower_after(b, L, T, top_extra=True):
    def after(rr, p):
        roof_dress(b, L, rr, "garden" if ((T == 3 and p["v"] % 2) or (L.eu and (T >= 2 or p["v"] % 2))) else ("solar" if L.up else "tank"))
        if p.get("tallest"):
            x, y, w, d = rr
            if T == 3:
                b.rect(x + 2, y - 3, w - 4, 3, shade(p["roof"], 0.9))  # crown
                b.rect(x + 2, y - 3, w - 4, 1, shade(p["roof"], 1.25))
                antenna(b, x + w // 2, y - 3, 5)
            elif T == 2 and p["v"] % 2 == 0 and w >= 14 and d >= 12:
                helipad(b, x + w // 2, y + d // 2, 9)
    return after


def loggias(b, x, y, w, h, seed):
    """European recessed loggias with a white rail and a planted pot."""
    r = random.Random(seed)
    for k, fy in enumerate(range(y + 3, y + h - 6, 6)):
        for xx in range(x + 3 + (k % 2) * 4, x + w - 6, 10):
            b.rect(xx, fy, 5, 3, (70, 56, 48, 150))
            b.rect(xx, fy + 3, 5, 1, FRAME)
            b.set(xx + r.randrange(5), fy + 2, r.choice([H("4FA55B"), H("F26B6B"), H("F7D154")]))


def forest(b, x, y, w, h, seed):
    """Vertical forest: staggered balcony slabs on a glass tower, planted with shrubs and trees."""
    r = random.Random(seed)
    greens = [H("3F9B4A"), H("5DB062"), H("2F7D3E"), H("77C06A")]
    for k, fy in enumerate(range(y + 4, y + h - 5, 5)):
        for xx in range(x + 2 + (k % 2) * 3, x + w - 5, 6):
            if r.random() < 0.9:
                b.rect(xx, fy, 5, 1, (235, 240, 242, 190))
                for i in range(5):
                    if r.random() < 0.8:
                        b.set(xx + i, fy - 1, r.choice(greens))
                        if r.random() < 0.4:
                            b.set(xx + i, fy - 2, r.choice(greens))
                if r.random() < 0.3:
                    b.set(xx + r.randrange(5), fy + 1, greens[0])


def res_high(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    roof, wall = pk(HI_ROOF, L), pk(HI_WALL, L, v + T - 1)
    lit = 0.35 + 0.2 * L.up
    plaza(g, L, H("C9C6BE"))
    if L.eu:
        cobbles(g, 0, y0, L.W, L.fd, H("BDB6AA"), seed=L.seed("cob"))
        lawn(g, 2, L.yB - 6, L.W - 4, 4, seed=L.seed("strip"))
    L.top = 11
    s = L.seed("hi")
    hh = {1: 34, 2: 56, 3: 200}[T]
    yF = yB - 8 - (fd - 64) // 4
    d = min(16 + 4 * L.cy, fd - 16)
    rs = "eu" if L.eu else "res"
    style = rs if T < 3 or v % 2 else "glass"
    wl = wall if style == rs else pk(HI_GLASS, L)
    parts = []
    if v == 0:      # one big slab
        parts.append(P(4, yF, fw - 8, d, L.hfit(yF, d, hh), roof, wl, style, s, lit))
    elif v == 1:    # tower on the left + podium wing
        tw = min(26, fw // 2)
        parts.append(P(fw - 4 - (fw - tw - 10), yF, fw - tw - 10, d - 6, 10, shade(roof, 1.05), wall, rs, s + 1, lit))
        parts.append(P(4, yF - 2, tw, d, L.hfit(yF - 2, d, hh), roof, wl, style, s, lit))
    elif v == 2:    # two (three) towers of different height
        n = 3 if L.cx >= 3 else 2
        tw = (fw - 6 - 4 * (n - 1)) // n
        for i in range(n):
            f = [1.0, 0.72, 0.86][i]
            yy = yF - (i % 2) * 6
            parts.append(P(3 + i * (tw + 4), yy, tw, d - 2, L.hfit(yy, d - 2, int(hh * f)), roof, wl, style, s + i, lit))
    else:           # slender back tower + front mid slab
        tw = min(22, fw // 2 - 2)
        ty = yF - min(16, fd // 4)
        parts.append(P(fw - 4 - tw, ty, tw, d - 4, L.hfit(ty, d - 4, hh), roof, wl, style, s, lit))
        parts.append(P(4, yF, fw - tw - 12, d - 6, L.hfit(yF, d - 6, int(hh * 0.5)), shade(roof, 1.05), wall, rs, s + 1, lit))
    tall = max(parts, key=lambda p: p["h"])
    for p in parts:
        p["v"] = v
        p["tallest"] = p is tall
        p["after"] = tower_after(b, L, T)
        if L.eu:
            p["accent"] = SHUTTER[(v + p["seed"]) % 4]
    def deco(rr, p):
        if p["style"] == "res" and p["h"] > 12:
            balconies(b, p["x"], rr[1] + rr[3], p["w"], p["h"] - 4, 5, (255, 255, 255, 70) if T < 3 else (190, 230, 255, 120), seed=p["seed"])
        elif p["style"] == "eu" and p["h"] > 12:
            loggias(b, p["x"], rr[1] + rr[3], p["w"], p["h"] - 4, p["seed"])
        elif L.eu and p["style"] == "glass" and p["h"] > 12:
            forest(b, p["x"] + 1, rr[1] + rr[3], p["w"] - 2, p["h"] - 4, p["seed"])
        b.rect(p["x"], p["yb"] - 4, p["w"], 4, (40, 50, 70, 120))  # lobby band
        b.rect(p["x"] + p["w"] // 2 - 2, p["yb"] - 4, 4, 4, (255, 232, 170, 220))
    for p in parts:
        p["deco"] = deco
    out = draw_parts(b, g, L, parts)
    boost(b, g, L, out, lit=0.75)
    if L.boost:
        planters(g, 4, yF + 1, fw - 8, seed=L.seed("pl"))
    street_trees(g, L, yB - 4, 12 if not L.up else 8, 3)
    if fd >= 96:
        trees(g, [(6, y0 + 8, 4), (fw - 7, y0 + 10, 4)], L)
    return finish(b, g)


# =========================================================================== res-lowrent
def panels(b, x, y, w, h):
    """Plattenbau prefab panel joints: vertical every 8px, horizontal every 5px."""
    for xx in range(x + 7, x + w - 2, 8):
        b.vline(xx, y, y + h - 1, (0, 0, 0, 42))
        b.vline(xx + 1, y, y + h - 1, (255, 255, 255, 36))
    for yy in range(y + 4, y + h - 1, 5):
        b.hline(x, x + w - 1, yy, (0, 0, 0, 32))


def renovate(b, x, y, w, h, col, seed, strong=True):
    """Pastel renovation: coloured stair-core stripes and a plinth band."""
    a = 190 if strong else 130
    for xx in range(x + 4, x + w - 5, 24):
        b.rect(xx, y + 1, 4, h - 6, (col[0], col[1], col[2], a))
    b.rect(x + 1, y + h - 8, w - 2, 3, (col[0], col[1], col[2], a))


def res_lowrent(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    roof, wall = pk(LR_ROOF, L), pk(LR_WALL, L, v + T - 1)
    if L.up:
        wall = shade(wall, 1.06)
    s = L.seed("lr")
    r = L.rng("lr")
    lawn(g, 0, y0, fw, fd, H("8FAE5E"), seed=s)
    for _ in range(fw * fd // 300):  # dirt patches
        g.ellipse(r.randrange(fw), y0 + 3 + r.randrange(fd - 6), 3 + r.randrange(4), 2, H("A89A72"))
    asphalt(g, 0, yB - 9, fw, 9, s)
    for x in range(2, fw - 4, 7):
        g.rect(x, yB - 8, 5, 3, (255, 255, 255, 30))
    G.fence(g, 0, y0, fw, fd, H("9AA0A8"), "lr")
    hh = {1: 28, 2: 46, 3: 70}[T]
    yF = yB - 11
    d = min(14 + 3 * L.cy, fd - 18)
    parts = []
    if v in (0, 2):
        parts.append(P(3, yF - (6 if v == 2 else 0), fw - 6, d, L.hfit(yF, d, hh), roof, wall, None, s))
    if v == 1:
        bw = fw // 2 - 2
        parts.append(P(2, yF - 8, bw, d, L.hfit(yF - 8, d, hh), roof, wall, None, s))
        parts.append(P(fw - 2 - bw, yF, bw, d - 2, L.hfit(yF, d - 2, hh - 8), roof, shade(wall, 0.95), None, s + 1))
    if v == 3:
        tw = 22
        parts.append(P(fw - 3 - tw, yF - 4, tw, d, L.hfit(yF - 4, d, hh + 8), roof, wall, None, s))
        parts.append(P(3, yF, fw - tw - 9, d - 4, L.hfit(yF, d - 4, hh // 2), roof, shade(wall, 0.94), None, s + 1))
    if v == 2:  # L wing
        parts.append(P(3, yF, 14, d + 4, L.hfit(yF, d + 4, hh - 10), roof, shade(wall, 0.92), None, s + 2))

    def after(rr, p):
        x, y, w, dd = rr
        b.rect(x, y, w, 1, shade(p["roof"], 1.2))
        rr2 = r.random()
        for k in range(max(1, w // 12)):  # satellite dishes / vents
            px = x + 3 + k * 10 + r.randrange(3)
            b.circle(px, y + 3 + r.randrange(max(1, dd - 6)), 1.5, H("E8E8E8"))
        G.ac_unit(b, x + w - 6, y + 2) if rr2 < 0.7 else None
        if L.up and w >= 16:
            G.solar_panel(b, x + 2, y + dd - 6, min(12, w - 4), 4)
    for p in parts:
        p["after"] = after
    def deco(rr, p):
        fx, fy, fwid, fh = p["x"], rr[1] + rr[3], p["w"], p["h"]
        for xx in range(fx + 3, fx + fwid - 2, 9):  # rain stains
            b.rect(xx, fy + 2, 1, fh - 4, (60, 60, 50, 22))
        if L.eu:    # prefab panel grid + renovated pastel stripes
            panels(b, fx, fy, fwid, fh)
            renovate(b, fx, fy, fwid, fh, PASTEL[(p["seed"] + v) % 4], p["seed"])
            b.rect(fx + 1, fy + 1, fwid - 2, 2, (255, 255, 255, 90))
        fine_windows(b, fx, fy, fwid, fh - 3, p["seed"], lit=0.3 + 0.2 * L.up, step_x=3, step_y=3)
        for fy2 in range(fy + 6, fy + fh - 6, 9):
            if r.random() < 0.5:
                laundry(b, fx + 2 + r.randrange(max(1, fwid - 12)), fy2, 8, r.random() * 1000)
            if r.random() < 0.6:
                ax = fx + 2 + r.randrange(max(1, fwid - 5))
                b.rect(ax, fy2 + 2, 3, 2, H("C9CED6"))
        G.door(b, fx + fwid // 2 - 2, p["yb"] - 4, 4, 4, H("4E555E"))
    for p in parts:
        p["deco"] = deco
    out = draw_parts(b, g, L, parts)
    if L.boost:     # renovated block: fresh pastel stripes, lit windows, flags, balcony planters
        boost(b, g, L, out, lit=0.7, terrace=False)
        for rr, p in out:
            renovate(b, p["x"], rr[1] + rr[3], p["w"], p["h"], PASTEL[(p["seed"] + 2 * v + 1) % 4], p["seed"])
            planters(b, p["x"] + 3, p["yb"] - 9, p["w"] - 6, 12, seed=p["seed"])
    for x in (4, fw - 10):  # dumpsters
        b.rect(x, yB - 13, 6, 3, H("2E7D4F"))
        b.rect(x, yB - 13, 6, 1, H("4FA06E"))
    if T >= 2 or L.up:
        trees(g, [(5, yF - 2, 3), (fw - 5, y0 + 6, 3)], L)
    return finish(b, g)


# =========================================================================== res-mixed
def wrought(b, x, y, w, h):
    """Haussmann wrought-iron balcony rails on every other floor."""
    for fy in range(y + 10, y + h - 8, 12):
        b.hline(x + 1, x + w - 2, fy, (45, 45, 52, 220))
        for xx in range(x + 2, x + w - 2, 2):
            b.set(xx, fy + 1, (45, 45, 52, 130))


def flower_boxes(b, x, y, w, h, seed):
    """Window flower boxes on every floor (upgrade dressing)."""
    r = random.Random(seed)
    for fy in range(y + 4, y + h - 8, 6):
        for xx in range(x + 2, x + w - 2, 3):
            b.set(xx, fy, r.choice([H("E8467C"), H("F26B6B"), H("FFFFFF"), H("F7D154")]))
            b.set(xx + 1, fy, H("3F9B4A"))


def res_mixed(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    roof, wall = pk(MX_ROOF, L), pk(MX_WALL, L, v + T - 1)
    s = L.seed("mx")
    r = L.rng("mx")
    lit = 0.4 + 0.2 * L.up
    SH = EU_AWN if L.eu else SHOP
    rs = "eushop" if L.eu else "retail"
    paving(g, 0, y0, fw, fd, H("D9D2C4"), 5)
    if L.eu:
        cobbles(g, 0, y0, fw, fd, H("C4BBAB"), seed=L.seed("cob"))
    lawn(g, 3, y0 + 3, fw - 6, fd // 3, seed=s)  # back courtyard
    hh = {1: 20, 2: 36, 3: 60}[T]
    yF = yB - 8
    d = min(16 + 3 * L.cy, fd - 14)
    parts = []
    if v == 0:
        parts.append(P(2, yF, fw - 4, d, L.hfit(yF, d, hh), roof, wall, rs, s, lit, accent=SH[0]))
    elif v == 1:
        bw = fw // 2
        parts.append(P(2, yF, bw - 2, d, L.hfit(yF, d, hh), roof, wall, rs, s, lit, accent=SH[1]))
        parts.append(P(bw, yF, fw - bw - 2, d - 2, L.hfit(yF, d - 2, int(hh * 0.75)), shade(roof, 1.08), pk(MX_WALL, L, v + 2), rs, s + 1, lit, accent=SH[4]))
    elif v == 2:
        cw = 18
        parts.append(P(2, yF, fw - cw - 3, d - 2, L.hfit(yF, d - 2, int(hh * 0.7)), roof, wall, rs, s, lit, accent=SH[2]))
        parts.append(P(fw - cw - 1, yF, cw - 1, d, L.hfit(yF, d, hh + 8), shade(roof, 0.95), shade(wall, 0.92), rs, s + 1, lit, accent=SH[5]))
    else:
        parts.append(P(2, yF, fw - 4, d, 10, shade(roof, 1.08), wall, rs, s, lit, accent=SH[3]))
        tw = fw - 16
        parts.append(P(8, yF - d + 6, tw, d - 8, L.hfit(yF - d + 6, d - 8, hh), roof, pk(MX_WALL, L, v + 1), "eu" if L.eu else "res", s + 1, lit,
                       accent=SHUTTER[1] if L.eu else None))

    def after(rr, p):
        if L.eu:    # Haussmann mansard with dormers
            mansard(b, rr, p["roof"], EU_ZINC, FRAME, lit, p["seed"])
        else:
            roof_dress(b, L, rr, "garden" if T == 3 else ("solar" if L.up else "gear"))
    for p in parts:
        p["after"] = after
    def deco(rr, p):
        if p["style"] in ("retail", "eushop"):
            if L.eu:
                wrought(b, p["x"], rr[1] + rr[3], p["w"], p["h"])
            else:
                awnings(b, p["x"] + 1, p["yb"] - 7, p["w"] - 2, SHOP[(v * 2 + p["seed"]) % 6:] + SHOP[:2], 7)
            if T >= 2 and not L.eu:
                balconies(b, p["x"], rr[1] + rr[3], p["w"], p["h"] - 8, 5, seed=p["seed"])
            if L.up:
                for xx in range(p["x"] + 3, p["x"] + p["w"] - 3, 6):
                    b.set(xx, rr[1] + rr[3] + 4, H("5DB062"))
                    b.set(xx + 1, rr[1] + rr[3] + 4, H("F26B6B"))
    for p in parts:
        p["deco"] = deco
    out = draw_parts(b, g, L, parts)
    boost(b, g, L, out, lit=0.75, terrace=not L.eu)
    if L.boost:
        if L.eu:
            uplights(b, out, L)
            for rr, p in out:
                flower_boxes(b, p["x"], rr[1] + rr[3], p["w"], p["h"], L.seed("fb", p["x"]))
        planters(g, 3, yF + 1, fw - 6, seed=L.seed("pl"))
    if L.eu:        # cafe tables with parasols on the cobbled front
        cafe(g, 2, yB - 4, fw - 4, L.seed("cafe"), step=6 if L.up else 8)
    else:
        for x in range(3, fw - 4, 9):  # cafe tables / planters
            if r.random() < 0.5 + 0.3 * L.up:
                g.circle(x + 2, yB - 4, 1.4, H("F4F4F4"))
                g.set(x, yB - 4, H("C0392B"))
                g.set(x + 4, yB - 4, H("C0392B"))
            else:
                G.bush(g, x + 2, yB - 4)
    return finish(b, g)


ZONES = {
    "res-med": (1, "2x2 3x2 2x3 3x3", res_med),
    "res-high": (2, "2x2 3x2 2x3 3x3", res_high),
    "res-lowrent": (2, "2x2 3x2 2x3", res_lowrent),
    "res-mixed": (2, "2x2 3x2 2x3", res_mixed),
}
