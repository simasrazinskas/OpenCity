"""gen_zon_com.py - commercial and office lots: com-low, com-high, off, off-high."""
import random

import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (layers, finish, shadow, lawn, paving, trees, street_trees, draw_parts,
                          roof_gear, roof_garden, antenna, helipad, awnings, shopfront,
                          parking_rows, asphalt, parapet, truck, hedge,
                          pk, EU_TILE, EU_SLATE, EU_ZINC, EU_STUCCO, EU_BRICK, FRAME,
                          tiled, dormers, mansard, brick, cobbles, cafe, planters, flag, boost,
                          roof_terrace)

# palettes: entries 0-3 North American, 4-7 European
ACC = [H("2C6FD1"), H("E67E22"), H("1FA37A"), H("E8467C"), H("A3342B"), H("2E6B4C"), H("27456E"), H("C99A2E")]
CL_WALL = [H("F6F1E4"), H("F2E2C8"), H("D49A7A"), H("E4F0F6"), EU_STUCCO[0], EU_BRICK[1], EU_STUCCO[1], EU_STUCCO[3]]
CL_ROOF = [H("D8D2C4"), H("B5483A"), H("8B6A55"), H("C4D3E0"), EU_TILE[0], EU_SLATE[1], EU_TILE[2], EU_SLATE[3]]


def P(x, yb, w, d, h, roof, wall, style=None, seed=0, lit=0.4, accent=None):
    return dict(x=x, yb=yb, w=w, d=d, h=h, roof=roof, wall=wall, style=style, seed=seed, lit=lit, accent=accent)


def glassf(b, x, y, w, h, top, bot, seed, lit=0.4, step=4, floor=4):
    """Curtain wall with a vertical tint gradient, mullions, lit floors and reflections."""
    for i in range(h):
        b.rect(x + 1, y + i, w - 2, 1, mix(top, bot, i / max(1, h - 1)))
    for xx in range(x + 1, x + w - 1, step):
        b.vline(xx, y, y + h - 1, (230, 245, 255, 60))
    r = random.Random(seed)
    for yy in range(y + floor - 1, y + h - 1, floor):
        b.hline(x + 1, x + w - 2, yy, (15, 30, 55, 90))
        for xx in range(x + 2, x + w - 3, step):
            if r.random() < lit * 0.5:
                b.rect(xx, yy - floor + 2, step - 1, floor - 2, (255, 226, 150, 140))
    for k in range(0, w + h, 11):
        for i in range(h):
            px = x + k - i
            if x < px < x + w - 1:
                b.blend(px, y + i, (255, 255, 255, 30))


def ribbon(b, x, y, w, h, seed, lit=0.4, band=5):
    """Horizontal ribbon windows between stone spandrels."""
    r = random.Random(seed)
    for yy in range(y + 2, y + h - 3, band):
        b.rect(x + 1, yy, w - 2, 2, mix(H("2C3A52"), H("7FB6DE"), 0.45))
        for xx in range(x + 2, x + w - 3, 3):
            if r.random() < lit * 0.6:
                b.rect(xx, yy, 2, 2, (255, 226, 150, 200))
        b.hline(x + 1, x + w - 2, yy, (255, 255, 255, 60))


def big_sign(b, x, y, w, col, seed, up=False):
    G.sign(b, x, y, w, col, seed)
    b.rect(x + 2, y + 4, 1, 2, H("7B828E"))
    b.rect(x + w - 3, y + 4, 1, 2, H("7B828E"))
    if up:  # neon frame
        b.rect_outline(x - 1, y - 1, w + 2, 6, (255, 240, 160, 200))


def banner(b, x, y, h, col):
    """Hanging vertical banner."""
    b.rect(x, y, 3, h, col)
    b.rect(x, y, 3, 1, shade(col, 1.35))
    b.rect(x + 2, y, 1, h, shade(col, 0.75))
    b.set(x + 1, y + h, shade(col, 0.8))
    b.set(x + 1, y + 2, (255, 255, 255, 170))


def lettering(b, x, y, w, seed, base=H("23372E"), gold=H("E5C46A")):
    """Classic department-store lettering band: dark panel with gilded capitals."""
    b.rect(x, y, w, 5, base)
    b.rect(x, y, w, 1, shade(base, 1.6))
    b.rect(x, y + 4, w, 1, shade(base, 0.6))
    r = random.Random(seed)
    xx = x + 2
    while xx < x + w - 3:
        for k in range(r.randint(2, 4)):
            if xx + k < x + w - 2:
                b.rect(xx + k, y + 1 + (k % 2) * r.randint(0, 1), 1, 3 - (k % 2), gold)
        xx += 6


def dome(b, cx, cy, r):
    """Glass dome (atrium lantern) seen from above."""
    b.circle(cx, cy + 1, r + 1, H("8E9AA3"))
    b.circle(cx, cy, r, H("BFE4F5"))
    b.circle(cx - 1, cy - 1, max(1, r - 2.5), H("E6F6FC"))
    b.vline(cx, cy - r + 1, cy + r - 1, H("8AB4CC"))
    b.hline(cx - r + 1, cx + r - 1, cy, H("8AB4CC"))
    b.rect(cx - 1, cy - 1, 2, 2, FRAME)


def bikes(g, x, y, n):
    """Bicycle rack with n bikes (top view)."""
    g.rect(x - 1, y + 5, n * 3 + 1, 1, H("8D96A0"))
    for i in range(n):
        g.rect(x + i * 3, y + 1, 1, 4, H("39414B"))
        g.rect(x + i * 3 - 1, y + 1, 3, 1, H("39414B"))


def stalls(b, g, x, y, w, seed, cols):
    """Row of striped market stalls with counters and produce."""
    r = random.Random(seed)
    for n, sx in enumerate(range(x, x + w - 7, 9)):
        col = cols[n % len(cols)]
        g.rect(sx + 1, y + 5, 8, 5, (10, 18, 30, 50))
        for k in range(8):
            b.rect(sx + k, y, 1, 5, col if (k // 2) % 2 == 0 else H("F4F4F4"))
        b.rect(sx, y, 8, 1, shade(col, 1.25))
        b.rect(sx, y + 5, 8, 1, shade(col, 0.7))
        b.rect(sx + 1, y + 6, 6, 2, H("8C6A4F"))
        for k in range(3):
            b.set(sx + 1 + k * 2, y + 6, r.choice([H("E84545"), H("F2C94C"), H("5DB062"), H("F4F4F4")]))


# =========================================================================== com-low
def com_low(L):
    if L.eu:
        return com_low_eu(L)
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    acc, wall, roof = pk(ACC, L), pk(CL_WALL, L), pk(CL_ROOF, L)
    if T == 3:
        wall = shade(wall, 1.04)
    s = L.seed("cl")
    lit = 0.4 + 0.25 * L.up
    asphalt(g, 0, y0, fw, fd, s)
    g.rect(0, y0, fw, 1, H("D5D8DC"))
    g.rect(0, yB - 3, fw, 3, H("C9CCD1"))
    w = min(fw - 4, {1: 20, 2: 25, 3: 29}[T] + 22 * (L.cx - 1))
    d = {1: 11, 2: 12, 3: 14}[T] + 4 * (L.cy - 1)
    h = {1: 7, 2: 9, 3: 11}[T]
    park = 7 if L.cy == 1 else 7 + (fd - 32) // 2
    yb = yB - 3 - park
    x = (fw - w) // 2 + (0 if L.cx > 1 or T == 3 else (-2 if v % 2 else 2))
    if v == 3 and fw >= 64:  # fuel canopy beside the store
        x = 3
        w = min(w, fw - 30)
        cx0 = x + w + 4
        cw = fw - cx0 - 3
        shadow(g, L, cx0, yb + 4, cw, 12, 7, alpha=50)
        g.rect(cx0 + 1, yb - 7, cw - 2, 10, H("C9CCD1"))  # forecourt
        for k in range(2):
            px = cx0 + cw // 3 * (k + 1) - 1
            g.rect(px, yb - 3, 2, 3, H("D94F4F"))
            g.rect(px, yb - 3, 2, 1, H("F08080"))
            b.rect(px, yb - 15, 1, 12, H("9AA0A8"))  # canopy column
        b.rect(cx0, yb - 21, cw, 10, H("F4F4F4"))
        b.rect(cx0, yb - 21, cw, 1, H("FFFFFF"))
        b.rect(cx0, yb - 12, cw, 2, acc)
        b.rect(cx0, yb - 10, cw, 1, shade(acc, 0.7))
    parts = [P(x, yb, w, d, h, roof, wall, seed=s)]
    if T == 3 and L.cx == 1 and L.cy >= 2:
        parts[0]["d"] += 4

    def after(rr, p):
        rx, ry, rw, rd = rr
        b.rect(p["x"], ry + rd, p["w"], 2, acc)  # fascia
        roof_gear(b, rr, L, 0.6 + 0.6 * L.up, solar=(L.up and T == 2), sky=True)
        yy = p["yb"] - 5
        n = 1 if p["w"] < 26 else max(2, p["w"] // 14)
        if v == 2 or T == 1 and n > 1:  # several boutiques with different awnings
            seg = p["w"] // n
            for i in range(n):
                shopfront(b, p["x"] + i * seg + 2, yy, seg - 3, 4, s + i)
            awnings(b, p["x"] + 1, yy - 2, p["w"] - 2, [ACC[(v + i) % 4] for i in range(4)], seg)
        else:
            shopfront(b, p["x"] + 2, yy, p["w"] - 4, 4, s)
            awnings(b, p["x"] + 2, yy - 2, p["w"] - 4, [acc], p["w"])
        G.door(b, p["x"] + p["w"] // 2 - 1, p["yb"] - 4, 3, 4, shade(acc, 0.7))
        if T == 1 and v == 0:  # produce crates in front
            for i in range(3):
                b.rect(p["x"] + 2 + i * 4, p["yb"], 3, 2, [H("E84545"), H("F2C94C"), H("5DB062")][i])
        if T >= 2:  # pylon / roof sign
            if v == 1 or T == 2:
                px = min(p["x"] + p["w"] - 3, L.W - 7) if v % 2 else max(p["x"] + 2, 6)
                b.rect(px, ry - 8, 1, 9 + h, H("7B828E"))
                big_sign(b, px - 4, ry - 13, 9, acc, s, L.up)
            if T == 3:
                big_sign(b, p["x"] + 4, ry - 6, p["w"] - 8, acc, s + 1, L.up)
        if T == 3 and v != 2:  # entrance canopy
            b.rect(p["x"] + p["w"] // 2 - 5, p["yb"] - 1, 10, 2, shade(acc, 0.9))
    parts[0]["after"] = after
    out = draw_parts(b, g, L, parts)
    parking_rows(g, 2, yb + 2, fw - 4, yB - 5 - yb, s, fill=0.4 + 0.3 * L.up)
    if L.up or T == 3:
        trees(g, [(3, y0 + 5, 3), (fw - 4, y0 + 5, 3)], L)
    if T >= 2:
        G.bush(g, 3, yB - 5)
        G.bush(g, fw - 4, yB - 5)
    if L.boost:  # upgrade: terrace, flags, banners, planters, extra trees
        boost(b, g, L, out, lit=0.7, terrace=T >= 2, flags=True, col=acc)
        planters(g, x + 3, yb + 1, w - 6, 6, L.seed("pl"))
        banner(b, x + 5, yb - 10, 7, acc)
        banner(b, x + w - 8, yb - 10, 7, acc)
        trees(g, [(fw // 3, y0 + 5, 3), (2 * fw // 3, y0 + 5, 3)], L)
    return finish(b, g)


def com_low_eu(L):
    """Old-town shops: stucco/brick terrace with steep tiles or a mansard, shop awnings, cobbled square."""
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    acc, wall = pk(ACC, L), pk(CL_WALL, L)
    s = L.seed("cl")
    lit = 0.4 + 0.25 * L.up
    cobbles(g, 0, y0, fw, fd, H("B9AFA0"), s)
    g.rect(0, y0, fw, 1, H("8F887C"))
    g.rect(0, yB - 4, fw, 1, H("D8D3C8"))  # kerb
    g.rect(0, yB - 3, fw, 3, H("62666E"))  # street
    cr = random.Random(s)
    for cxp in range(3, fw - 8, 18):  # a few parked cars
        if cr.random() < 0.65:
            G.car_top(g, cxp + cr.randrange(8), yB - 3, cr.choice(G.CARCOLS), horizontal=True)
    w = min(fw - 4, {1: 20, 2: 25, 3: 29}[T] + 22 * (L.cx - 1))
    d = {1: 11, 2: 12, 3: 14}[T] + 4 * (L.cy - 1)
    h = {1: 18, 2: 21, 3: 24}[T]
    sq = 6 if L.cy == 1 else 6 + (fd - 32) // 2   # depth of the cobbled square
    yb = yB - 4 - sq
    x = (fw - w) // 2 + (0 if L.cx > 1 or T == 3 else (-2 if v % 2 else 2))
    if v == 3 and fw >= 64:  # covered market beside the shop block
        x = 3
        w = min(w, fw - 30)
        cx0 = x + w + 4
        cw = fw - cx0 - 3
        stalls(b, g, cx0 + 1, yb - 15, cw - 2, s, [ACC[4], ACC[5], ACC[6], ACC[7]])
        stalls(b, g, cx0 + 1, yb - 4, cw - 2, s + 1, [ACC[7], ACC[4], ACC[5], ACC[6]])
    part = P(x, yb, w, d, h, pk(CL_ROOF, L), wall, "eushop", s, lit, acc)
    rc = pk(CL_ROOF, L)

    def after(rr, p):
        rx, ry, rw, rd = rr
        h2 = p["h"]
        gh = min(6, h2 // 3)
        if v == 1:
            brick(b, p["x"], ry + rd, p["w"], h2 - gh)
        b.rect(p["x"], ry + rd, p["w"], 1, FRAME)  # cornice
        dw = FRAME if v == 1 else p["wall"]
        if T == 3:
            mansard(b, rr, EU_SLATE[v], EU_ZINC, dw, lit, s)
        else:
            tiled(b, rx, ry, rw, rd, rc)
            dormers(b, rx, ry + rd - 4, rw, rc, dw, lit, s)
            for kx in (rx + 3, rx + rw - 6):  # chimneys on the ridge
                b.rect(kx, ry + rd // 2 - 4, 3, 4, EU_BRICK[2])
                b.rect(kx, ry + rd // 2 - 4, 3, 1, shade(EU_BRICK[2], 1.3))
        n = max(1, p["w"] // 14)
        awnings(b, p["x"] + 2, p["yb"] - gh, p["w"] - 4, [pk(ACC, L, v + i) for i in range(4)], max(7, (p["w"] - 4) // n))
        G.door(b, p["x"] + p["w"] // 2 - 1, p["yb"] - gh + 2, 3, gh - 2, shade(acc, 0.7))
    part["after"] = after
    out = draw_parts(b, g, L, [part])
    cafe(g, 4, yb + 4 + L.boost, fw - 8, s)
    if sq >= 16:
        for xx in range(10, fw - 8, 22):
            G.tree(g, xx, yb + 14, 3, H("3F9B4A") if (xx // 22) % 2 else H("52A857"))
        bikes(g, 6, yb + sq - 7, 4)
        bikes(g, fw - 18, yb + sq - 7, 4)
    trees(g, [(3, yb + 5, 3), (fw - 4, yb + 5, 3)], L)
    if L.boost:
        boost(b, g, L, out, lit=0.7, terrace=False, flags=True, col=acc)
        planters(g, x + 3, yb + 1, w - 6, 6, L.seed("pl"))
        ry = out[0][0][1]
        if T < 3:  # skylights in the tile slope
            for kx in range(x + 8, x + w - 10, 11):
                G.skylight(b, kx, ry + 1, 4, 3)
        banner(b, x + 5, yb - 14, 8, acc)
        banner(b, x + w - 8, yb - 14, 8, acc)
    return finish(b, g)


# =========================================================================== com-high
CH_WALL = [H("E6EAF0"), H("DCE4EE"), H("F1E8D8"), H("D6E2EC"), H("EFE5D0"), H("E6D9BF"), H("F2E9D8"), H("E3DCCB")]
CH_ROOF = [H("C9D0DA"), H("D4B08A"), H("B8C9B0"), H("AEBBC8"), H("7A8190"), H("8A7B6A"), H("6F7F7A"), H("7B7F8C")]
CH_GLASS = [H("9CC8E8"), H("A6D8C8"), H("C8B89A"), H("A8B8E0"), H("A9C7B4"), H("C7AE8A"), H("9FB4B0"), H("B9C4A8")]


def led(b, x, y, w, h, seed):
    r = random.Random(seed)
    b.rect(x, y, w, h, H("1A1F2B"))
    cols = [H("E8467C"), H("2C9BF0"), H("F2C94C"), H("5BE07A")]
    c1, c2 = r.sample(cols, 2)
    for yy in range(y + 1, y + h - 1):
        b.rect(x + 1, yy, w - 2, 1, mix(c1, c2, (yy - y) / max(1, h)))
    b.rect(x + 2, y + h // 2, w - 4, 1, (255, 255, 255, 160))


def com_high(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    eu = L.eu
    acc, wall, roof = pk(ACC, L), pk(CH_WALL, L, v + T), pk(CH_ROOF, L)
    s = L.seed("ch")
    lit = 0.45 + 0.2 * L.up
    L.top = 12
    if eu:
        cobbles(g, 0, y0, fw, fd, H("C8BFB0"), s)
        g.rect(0, y0, fw, 1, H("8F887C"))
    else:
        paving(g, 0, y0, fw, fd, H("C4C6CA"), 8)
        g.rect(0, y0, fw, 1, H("9A9EA4"))
    yF = yB - 8 - (6 if fd >= 96 else 0)
    D = min(fd - 12, 26 + 14 * (L.cy - 2))
    hb = {1: 20, 2: 26, 3: 24}[T]
    parts = [P(3, yF, fw - 6, D, hb, roof, wall, "eushop" if eu else "retail", s, lit, acc)]
    if fd >= 96:  # forecourt with fountain
        g.circle(fw // 2, yB - 6, 4, H("EDEBE4"))
        g.circle(fw // 2, yB - 6, 3, H("4FB0DD"))
        g.set(fw // 2, yB - 7, H("FFFFFF"))
    tower = T == 3 or (T == 2 and v in (1, 3))
    tx = tw = 0
    if tower:  # tower(s) standing on the podium roof
        tw = min(fw - 16, 30 + 6 * (L.cx - 2))
        th = {2: 40, 3: 200}[T]
        tx = [fw - tw - 6, 6, (fw - tw) // 2, 8][v]
        td = 14
        ty = yF - hb - 3 - (D - td - 6) // 3
        glass = pk(CH_GLASS, L)
        parts.append(dict(P(tx, ty, tw, td, L.hfit(ty, td, th), shade(roof, 0.95), glass, "glassT", s + 1, lit), z=1))
        if L.cx >= 3 and T == 3 and v % 2 == 0:
            ox = 6 if tx > fw // 2 else fw - 26
            parts.append(dict(P(ox, ty + 1, 20, 12, L.hfit(ty + 1, 12, th * 0.6), roof, glass, "glassT", s + 2, lit), z=1))

    def podium_eu(rr, p):
        """Department store: mansard band with dormers, glass dome, gilded lettering, stone portal."""
        rx, ry, rw, rd = rr
        mansard(b, rr, EU_SLATE[v], EU_ZINC, p["wall"], lit, s)
        r = min(7, (rd - 8) // 2)
        if tower:  # dome beside the tower
            lw, rw2 = tx - rx, rx + rw - (tx + tw)
            fx0, fw2 = (rx, lw) if lw >= rw2 else (tx + tw, rw2)
        else:
            fx0, fw2 = rx, rw
        r = min(r, (fw2 - 6) // 2)
        if r >= 3:
            dcx, dcy = fx0 + fw2 // 2, ry + 2 + (rd - 8) // 2 + 1
            if L.boost:  # upgrade: planted roof garden ring with parasols around the dome
                b.circle(dcx, dcy, r + 3.5, H("4FA55B"))
                b.circle(dcx, dcy, r + 2.5, H("6DB04E"))
                for k, (ox, oy) in enumerate(((-r - 6, 0), (r + 6, 0))):
                    if fx0 + 2 < dcx + ox < fx0 + fw2 - 2:
                        b.circle(dcx + ox, dcy + oy, 1.8, [H("F4F4F4"), H("E86F4A")][k])
            dome(b, dcx, dcy, r)
        sw = min(p["w"] - 8, 40)
        lettering(b, p["x"] + (p["w"] - sw) // 2, ry - 6, sw, p["seed"])
        awnings(b, p["x"] + 2, p["yb"] - 6, p["w"] - 4, [acc, pk(ACC, L, v + 1)], 10)
        ex = p["x"] + p["w"] // 2 - 6
        b.rect(ex - 1, p["yb"] - 6, 14, 1, FRAME)
        b.rect(ex - 1, p["yb"] - 5, 1, 5, FRAME)
        b.rect(ex + 12, p["yb"] - 5, 1, 5, FRAME)
        b.rect(ex, p["yb"] - 5, 12, 5, mix(H("A8D4EE"), H("5A90B8"), 0.3))
        b.vline(ex + 6, p["yb"] - 5, p["yb"] - 1, FRAME)

    def after(rr, p):
        rx, ry, rw, rd = rr
        if p["style"] == "glassT":
            glassf(b, p["x"], ry + rd, p["w"], p["h"], shade(p["wall"], 1.1), shade(p["wall"], 0.55), p["seed"], lit)
            roof_gear(b, rr, L, 0.6, sky=True)
            if T == 3:
                if eu:
                    lettering(b, p["x"] + 3, ry + rd + 4, p["w"] - 6, p["seed"])
                else:
                    led(b, p["x"] + 3, ry + rd + 4, p["w"] - 6, 7, p["seed"])
                antenna(b, rx + rw // 2, ry, 6)
            return
        if eu:
            return podium_eu(rr, p)
        b.rect(p["x"], ry + rd - 1, p["w"], 2, acc)
        if v == 2 or T == 2:  # glass barrel-vault atrium along the roof
            ax = rx + rw // 2 - 5
            b.rect(ax, ry + 2, 10, rd - 4, H("BFE4F5"))
            for yy in range(ry + 3, ry + rd - 3, 3):
                b.hline(ax, ax + 9, yy, H("8AB4CC"))
            b.rect(ax, ry + 2, 3, rd - 4, (255, 255, 255, 90))
            roof_gear(b, (rx, ry, ax - rx, rd), L, 0.8)
        elif v in (0, 3) and T == 1:  # rooftop car park
            b.rect(rx + 2, ry + 2, rw - 4, rd - 4, H("6A6F78"))
            pr = random.Random(p["seed"])
            for yy in range(ry + 3, ry + rd - 8, 9):
                for xx in range(rx + 4, rx + rw - 6, 5):
                    b.rect(xx, yy, 1, 6, (235, 235, 235, 140))
                    if pr.random() < 0.45 + 0.3 * L.up:
                        G.car_top(b, xx + 1, yy + 1, pr.choice(G.CARCOLS), horizontal=False)
            b.rect(rx + rw - 9, ry + rd - 6, 6, 4, shade(roof, 0.85))  # ramp housing
        else:
            roof_gear(b, rr, L, 1.0, solar=L.up)
        sw = min(p["w"] - 8, 40)
        big_sign(b, p["x"] + (p["w"] - sw) // 2, ry - 6, sw, acc, p["seed"], L.up)
        if L.up or T >= 2:
            led(b, p["x"] + 3, ry + rd + 3, 10, 6, p["seed"] + 3)
        awnings(b, p["x"] + 2, p["yb"] - 8, p["w"] - 4, [acc, ACC[(v + 1) % 4]], 10)
        ex = p["x"] + p["w"] // 2 - 6
        b.rect(ex, p["yb"] - 5, 12, 5, mix(H("A8D4EE"), H("5A90B8"), 0.3))
        b.rect(ex - 1, p["yb"] - 6, 14, 1, shade(acc, 0.85))
    for p in parts:
        p["after"] = after
    out = draw_parts(b, g, L, parts)
    if eu:  # cafe terrace and trees at the front
        cy = yF + 4 + (1 if L.boost else 0)
        if fd >= 96:
            cafe(g, 8, cy, fw // 2 - 14, s)
            cafe(g, fw // 2 + 6, cy, fw // 2 - 14, s + 1)
        else:
            cafe(g, 14, cy, fw - 28, s)
        for tx2 in (6, fw - 7):
            G.tree(g, tx2, yB - 5, 3, H("3F9B4A") if tx2 < 10 else H("52A857"))
    else:
        for x in range(4, fw - 3, 8):  # bollards / planters
            g.rect(x, yB - 5, 2, 2, H("7B828E"))
        street_trees(g, L, yB - 4, 16 if not L.up else 10, 3, x0=8)
    if L.boost:  # upgrade: lit windows, flags, banners, planters (+ roof terrace on flat roofs)
        boost(b, g, L, out, lit=0.65, terrace=not eu, flags=True, col=acc)
        pp = parts[0]
        planters(g, 8, yF + 1, fw - 16, 6, L.seed("pl"))
        ex = pp["x"] + pp["w"] // 2 - 6
        for bx in (ex - 8, ex + 17):
            banner(b, bx, pp["yb"] - 17, 10, acc)
    return finish(b, g)


# =========================================================================== offices
OF_GLASS = [(H("8FC3E6"), H("3A6C9A")), (H("9FD6D0"), H("2F7A78")), (H("B8C8DA"), H("4A5D7A")), (H("A9C8F0"), H("4060A8")),
            (H("A6BFB2"), H("4C6B5E")), (H("C9AE86"), H("6B5436")), (H("A9B7C4"), H("4D6074")), (H("9CC2B8"), H("386B62"))]
OH_GLASS = [(H("5C7C9A"), H("1C2C44")), (H("7AA59E"), H("1E4844")), (H("B59A72"), H("4A3A28")), (H("6E6A8E"), H("24203E")),
            (H("C08A64"), H("4F2E22")), (H("A38A66"), H("3A2D1E")), (H("6F9A84"), H("1E3A30")), (H("7D8DA0"), H("26303E"))]
STONE = [H("E3DED2"), H("D2D6DC"), H("EADCC6"), H("CACFD4"), H("DCCBA4"), H("CDB88F"), H("D8C4B0"), H("C3C7B3")]
OF_ROOF = [H("D7DCE2"), H("C7D0DA"), H("CFCFD2"), H("BFC9D6"), H("7C8490"), H("8A8478"), H("727D7A"), H("858A96")]
OH_ROOF = [H("8E97A3"), H("7D8A88"), H("9A8E7E"), H("878299"), H("7F6A58"), H("6F6A5A"), H("5F7A70"), H("6E7684")]


def office(L, high=False):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.lv, L.tier, L.fw, L.fd, L.y0, L.yB
    eu = L.eu
    stone = eu and not high and T < 3   # Haussmann stone office with mansard
    s = L.seed("of")
    lit = 0.45 + 0.25 * L.up
    L.top = 12
    if eu:
        cobbles(g, 0, y0, fw, fd, H("CFC6B6"), s)
        g.rect(0, y0, fw, 1, H("A39B8C"))
        hedge(g, 2, yB - 6, fw // 2 - 8, 2)
        hedge(g, fw // 2 + 6, yB - 6, fw // 2 - 8, 2)
    else:
        paving(g, 0, y0, fw, fd, H("D8DADC"), 6)
        g.rect(0, y0, fw, 1, H("B4B8BC"))
        lawn(g, 1, yB - 7, fw - 2, 5, base=H("72B552"), seed=s)
    gl = pk(OH_GLASS if high else OF_GLASS, L)
    roof = pk(OH_ROOF if high else OF_ROOF, L)
    hh = ({1: 36, 2: 54, 3: 200} if high else {1: 18, 2: 36, 3: 200})[T]
    if stone:
        hh = {1: 24, 2: 33}[T]
    yF = yB - 8 - (fd - 32) // 6
    d = min(12 + 5 * L.cy, fd - 12)
    style = "eu" if stone else "ribbon" if (T == 1 and not high) else "glassO"
    wl = pk(STONE, L) if style in ("ribbon", "eu") else gl[0]
    parts = []
    if v == 0 or fw == 32:
        parts.append(P(3, yF, fw - 6, d, L.hfit(yF, d, hh), roof, wl, style, s, lit))
    elif v == 1:  # twin blocks
        bw = (fw - 10) // 2
        parts.append(P(3, yF - 4, bw, d, L.hfit(yF - 4, d, hh), roof, wl, style, s, lit))
        parts.append(P(fw - 3 - bw, yF, bw, d - 2, L.hfit(yF, d - 2, hh * 0.7), roof, wl, style, s + 1, lit))
    elif v == 2:  # tower + low atrium wing
        tw = max(18, fw // 2 - 2)
        parts.append(P(fw - 3 - tw, yF - 3, tw, d, L.hfit(yF - 3, d, hh), roof, wl, style, s, lit))
        parts.append(P(3, yF, fw - tw - 8, d - 6, 10, shade(roof, 1.05), pk(STONE, L, v + 1), "eu" if eu else "ribbon", s + 1, lit))
    else:  # stepped: wide base + narrower upper block behind
        parts.append(P(3, yF, fw - 6, d - 4, L.hfit(yF, d - 4, hh * 0.35), roof, wl, style, s, lit))
        uy = yF - d + 10
        parts.append(P(8, uy, fw - 16, d - 8, L.hfit(uy, d - 8, hh), roof, wl, style, s + 1, lit))
    tall = max(parts, key=lambda p: p["h"])
    done = []   # parts whose roof already carries a helipad / garden / crown (no terrace on top)

    def after(rr, p):
        rx, ry, rw, rd = rr
        if p["style"] == "glassO":
            glassf(b, p["x"], ry + rd, p["w"], p["h"], p["wall"], gl[1], p["seed"], lit, step=4 if not high else 3)
        elif p["style"] == "ribbon":
            ribbon(b, p["x"], ry + rd, p["w"], p["h"], p["seed"], lit)
        if p["style"] == "eu":  # stone office: cornice, mansard roof, portal
            b.rect(p["x"], ry + rd, p["w"], 1, FRAME)
            mansard(b, rr, EU_SLATE[v], EU_ZINC, FRAME, lit, p["seed"])
            done.append(p)
            px, yb = p["x"] + p["w"] // 2, p["yb"]
            b.rect(px - 3, yb - 5, 6, 5, FRAME)
            b.rect(px - 2, yb - 4, 4, 4, shade(H("6B4A32"), 0.8))
            b.rect(px - 1, yb - 3, 2, 3, (255, 232, 170, 220))
            return
        b.rect(p["x"], ry + rd, p["w"], 1, shade(roof, 0.7))
        b.rect(p["x"], p["yb"] - 3, p["w"], 3, (30, 50, 80, 150))  # lobby
        b.rect(p["x"] + p["w"] // 2 - 2, p["yb"] - 3, 4, 3, (255, 232, 170, 220))
        parapet(b, rr, roof)
        if p is tall and T >= 2 and rw >= 14 and rd >= 12 and (high or v % 2 == 0):
            done.append(p)
            if eu:  # green roof instead of a helipad
                roof_garden(b, rr, p["seed"])
            else:
                helipad(b, rx + rw // 2, ry + rd // 2, 9)
                roof_gear(b, (rx, ry, rw, 5), L, 0.6)
        elif (L.up and T == 1) or (eu and (T == 1 or high)):
            done.append(p)
            roof_garden(b, rr, p["seed"])
        else:
            roof_gear(b, rr, L, 0.8, solar=L.up, sky=True)
        if p is tall and T == 3 and eu and high and rw >= 12 and rd >= 8:  # copper crown
            done.append(p)
            tiled(b, rx + 2, ry - 4, rw - 4, rd - 2, [H("5E9C88"), H("6FA896"), H("4F8C7C"), H("7FAF9A")][v])
            b.rect(rx + 2, ry - 4, rw - 4, 1, H("B8D8CC"))
            antenna(b, rx + rw // 2, ry - 4, 6)
        elif p is tall and T == 3:
            b.rect(rx + 2, ry - 3, rw - 4, 3, shade(roof, 0.85))
            b.rect(rx + 2, ry - 3, rw - 4, 1, shade(roof, 1.2))
            antenna(b, rx + rw // 2, ry - 3, 6 if high else 4)
        if high and p is tall:  # vertical fins
            for xx in range(p["x"] + 2, p["x"] + p["w"] - 2, 6):
                b.vline(xx, ry + rd, p["yb"] - 4, (255, 255, 255, 50))
    for p in parts:
        p["after"] = after
    out = draw_parts(b, g, L, parts)
    trees(g, [(3, yB - 4, 3), (fw - 4, yB - 4, 3)] + ([(fw // 2, yB - 4, 3)] if L.up and fw > 32 else []), L)
    if L.boost:  # upgrade: lit windows, flags, banners, planters, roof terrace on a free flat roof
        fl = H("B0302C") if eu else H("2C6FD1")
        boost(b, g, L, out, lit=0.65, terrace=False, flags=True, col=fl)
        free = [o for o in out if o[1] not in done]
        if free:
            rr, p = max(free, key=lambda o: o[0][2] * o[0][3])
            roof_terrace(b, rr, L.seed("rt"))
        fp = max(out, key=lambda o: o[1]["yb"])[1]
        bh = max(4, min(12, fp["h"] - 6))
        for bx in (fp["x"] + 4, fp["x"] + fp["w"] - 7):
            banner(b, bx, fp["yb"] - 4 - bh, bh, fl)
        planters(g, 9, yB - 5, fw - 18, 7, L.seed("pl"))
    return finish(b, g)


def off(L):
    return office(L, False)


def off_high(L):
    return office(L, True)


ZONES = {
    "com-low": (1, "1x1 2x1 1x2 2x2", com_low),
    "com-high": (2, "2x2 3x2 2x3 3x3 4x3 3x4", com_high),
    "off": (2, "1x1 2x1 1x2 2x2 2x3 3x2 3x3", off),
    "off-high": (2, "2x2 3x2 2x3 3x3", off_high),
}
