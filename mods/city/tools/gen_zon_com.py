"""gen_zon_com.py - commercial and office lots: com-low, com-high, off, off-high."""
import random

import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (layers, finish, shadow, lawn, paving, trees, street_trees, draw_parts,
                          roof_gear, roof_garden, antenna, helipad, awnings, shopfront,
                          parking_rows, asphalt, parapet, truck)

ACC = [H("2C6FD1"), H("E67E22"), H("1FA37A"), H("E8467C")]
CL_WALL = [H("F6F1E4"), H("F2E2C8"), H("D49A7A"), H("E4F0F6")]
CL_ROOF = [H("D8D2C4"), H("B5483A"), H("8B6A55"), H("C4D3E0")]


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


# =========================================================================== com-low
def com_low(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    acc, wall, roof = ACC[v], CL_WALL[v], CL_ROOF[v]
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
    draw_parts(b, g, L, parts)
    parking_rows(g, 2, yb + 2, fw - 4, yB - 5 - yb, s, fill=0.4 + 0.3 * L.up)
    if L.up or T == 3:
        trees(g, [(3, y0 + 5, 3), (fw - 4, y0 + 5, 3)], L)
    if T >= 2:
        G.bush(g, 3, yB - 5)
        G.bush(g, fw - 4, yB - 5)
    return finish(b, g)


# =========================================================================== com-high
CH_WALL = [H("E6EAF0"), H("DCE4EE"), H("F1E8D8"), H("D6E2EC")]
CH_ROOF = [H("C9D0DA"), H("D4B08A"), H("B8C9B0"), H("AEBBC8")]


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
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    acc, wall, roof = ACC[v], CH_WALL[(v + T) % 4], CH_ROOF[v]
    s = L.seed("ch")
    lit = 0.45 + 0.2 * L.up
    L.top = 12
    paving(g, 0, y0, fw, fd, H("C4C6CA"), 8)
    g.rect(0, y0, fw, 1, H("9A9EA4"))
    yF = yB - 8 - (6 if fd >= 96 else 0)
    D = min(fd - 12, 26 + 14 * (L.cy - 2))
    hb = {1: 20, 2: 26, 3: 24}[T]
    parts = [P(3, yF, fw - 6, D, hb, roof, wall, "retail", s, lit, acc)]
    if fd >= 96:  # forecourt with fountain
        g.circle(fw // 2, yB - 6, 4, H("EDEBE4"))
        g.circle(fw // 2, yB - 6, 3, H("4FB0DD"))
        g.set(fw // 2, yB - 7, H("FFFFFF"))
    if T == 3 or (T == 2 and v in (1, 3)):  # tower(s) standing on the podium roof
        tw = min(fw - 16, 30 + 6 * (L.cx - 2))
        th = {2: 40, 3: 200}[T]
        tx = [fw - tw - 6, 6, (fw - tw) // 2, 8][v]
        td = 14
        ty = yF - hb - 3 - (D - td - 6) // 3
        glass = [H("9CC8E8"), H("A6D8C8"), H("C8B89A"), H("A8B8E0")][v]
        parts.append(dict(P(tx, ty, tw, td, L.hfit(ty, td, th), shade(roof, 0.95), glass, "glassT", s + 1, lit), z=1))
        if L.cx >= 3 and T == 3 and v % 2 == 0:
            ox = 6 if tx > fw // 2 else fw - 26
            parts.append(dict(P(ox, ty + 1, 20, 12, L.hfit(ty + 1, 12, th * 0.6), roof, glass, "glassT", s + 2, lit), z=1))

    def after(rr, p):
        rx, ry, rw, rd = rr
        if p["style"] == "glassT":
            glassf(b, p["x"], ry + rd, p["w"], p["h"], shade(p["wall"], 1.1), shade(p["wall"], 0.55), p["seed"], lit)
            roof_gear(b, rr, L, 0.6, sky=True)
            if T == 3:
                led(b, p["x"] + 3, ry + rd + 4, p["w"] - 6, 7, p["seed"])
                antenna(b, rx + rw // 2, ry, 6)
            return
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
    draw_parts(b, g, L, parts)
    for x in range(4, fw - 3, 8):  # bollards / planters
        g.rect(x, yB - 5, 2, 2, H("7B828E"))
    street_trees(g, L, yB - 4, 16 if not L.up else 10, 3, x0=8)
    return finish(b, g)


# =========================================================================== offices
OF_GLASS = [(H("8FC3E6"), H("3A6C9A")), (H("9FD6D0"), H("2F7A78")), (H("B8C8DA"), H("4A5D7A")), (H("A9C8F0"), H("4060A8"))]
OH_GLASS = [(H("5C7C9A"), H("1C2C44")), (H("7AA59E"), H("1E4844")), (H("B59A72"), H("4A3A28")), (H("6E6A8E"), H("24203E"))]
STONE = [H("E3DED2"), H("D2D6DC"), H("EADCC6"), H("CACFD4")]


def office(L, high=False):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    s = L.seed("of")
    lit = 0.45 + 0.25 * L.up
    L.top = 12
    paving(g, 0, y0, fw, fd, H("D8DADC"), 6)
    g.rect(0, y0, fw, 1, H("B4B8BC"))
    lawn(g, 1, yB - 7, fw - 2, 5, base=H("72B552"), seed=s)
    gl = (OH_GLASS if high else OF_GLASS)[v]
    roof = [H("D7DCE2"), H("C7D0DA"), H("CFCFD2"), H("BFC9D6")][v] if not high else [H("8E97A3"), H("7D8A88"), H("9A8E7E"), H("878299")][v]
    hh = ({1: 36, 2: 54, 3: 200} if high else {1: 18, 2: 36, 3: 200})[T]
    yF = yB - 8 - (fd - 32) // 6
    d = min(12 + 5 * L.cy, fd - 12)
    style = "ribbon" if (T == 1 and not high) else "glassO"
    wl = STONE[v] if style == "ribbon" else gl[0]
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
        parts.append(P(3, yF, fw - tw - 8, d - 6, 10, shade(roof, 1.05), STONE[(v + 1) % 4], "ribbon", s + 1, lit))
    else:  # stepped: wide base + narrower upper block behind
        parts.append(P(3, yF, fw - 6, d - 4, L.hfit(yF, d - 4, hh * 0.35), roof, wl, style, s, lit))
        uy = yF - d + 10
        parts.append(P(8, uy, fw - 16, d - 8, L.hfit(uy, d - 8, hh), roof, wl, style, s + 1, lit))
    tall = max(parts, key=lambda p: p["h"])

    def after(rr, p):
        rx, ry, rw, rd = rr
        if p["style"] == "glassO":
            glassf(b, p["x"], ry + rd, p["w"], p["h"], p["wall"], gl[1], p["seed"], lit, step=4 if not high else 3)
        else:
            ribbon(b, p["x"], ry + rd, p["w"], p["h"], p["seed"], lit)
        b.rect(p["x"], ry + rd, p["w"], 1, shade(roof, 0.7))
        b.rect(p["x"], p["yb"] - 3, p["w"], 3, (30, 50, 80, 150))  # lobby
        b.rect(p["x"] + p["w"] // 2 - 2, p["yb"] - 3, 4, 3, (255, 232, 170, 220))
        parapet(b, rr, roof)
        if p is tall and T >= 2 and rw >= 14 and rd >= 12 and (high or v % 2 == 0):
            helipad(b, rx + rw // 2, ry + rd // 2, 9)
            roof_gear(b, (rx, ry, rw, 5), L, 0.6)
        elif L.up and T == 1:
            roof_garden(b, rr, p["seed"])
        else:
            roof_gear(b, rr, L, 0.8, solar=L.up, sky=True)
        if p is tall and T == 3:
            b.rect(rx + 2, ry - 3, rw - 4, 3, shade(roof, 0.85))
            b.rect(rx + 2, ry - 3, rw - 4, 1, shade(roof, 1.2))
            antenna(b, rx + rw // 2, ry - 3, 6 if high else 4)
        if high and p is tall:  # vertical fins
            for xx in range(p["x"] + 2, p["x"] + p["w"] - 2, 6):
                b.vline(xx, ry + rd, p["yb"] - 4, (255, 255, 255, 50))
    for p in parts:
        p["after"] = after
    draw_parts(b, g, L, parts)
    trees(g, [(3, yB - 4, 3), (fw - 4, yB - 4, 3)] + ([(fw // 2, yB - 4, 3)] if L.up and fw > 32 else []), L)
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
