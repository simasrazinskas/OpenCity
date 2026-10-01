"""gen_zon_ind.py - industrial lots: ind (factories) and warehouse (logistics sheds)."""
import random

import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (layers, finish, shadow, lawn, trees, draw_parts, roof_gear, truck,
                          parapet, asphalt)

YEL = G.YELLOW
IND_ROOF = [H("8C949E"), H("A5ADB6"), H("7E8791"), H("9AA3A0")]
IND_WALL = [H("C5CAD0"), H("D1C7B4"), H("B9C0C8"), H("CBCFC8")]
T3_WALL = [H("E8ECF0"), H("F0F2F4"), H("E4EAE6"), H("ECEFF4")]
T3_BAND = [H("2C6FD1"), H("1FA37A"), H("E67E22"), H("5B6B8C")]
CONT = [H("B0503C"), H("3D6A9E"), H("C9A23A"), H("4F8A5B"), H("8C949E"), H("C27A44")]


def pad(g, L, T, seed):
    col = {1: H("A39E92"), 2: H("A5A9AD"), 3: H("B9BDC1")}[T]
    g.rect(0, L.y0, L.W, L.fd, col)
    g.noise(0.05 if T < 3 else 0.03, seed=seed, x=0, y=L.y0, w=L.W, h=L.fd)
    g.rect(0, L.y0, L.W, 1, shade(col, 0.8))
    r = random.Random(seed)
    if T == 1:  # oil stains, gravel
        for _ in range(L.W * L.fd // 260):
            g.ellipse(r.randrange(L.W), L.y0 + 3 + r.randrange(L.fd - 6), 2 + r.randrange(3), 1.5, (40, 35, 30, 60))
    for x in range(0, L.W, 6):
        g.rect(x, L.yB - 2, 3, 1, YEL)
    if T == 3:
        lawn(g, 0, L.yB - 6, L.W, 3, base=H("72B552"), seed=seed)


def sawtooth(b, rr, roof, step=5):
    x, y, w, d = rr
    for i in range(0, w - 3, step):
        b.rect(x + i, y + 1, 3, d - 2, shade(roof, 1.18))
        b.rect(x + i + 3, y + 1, step - 3, d - 2, mix(shade(roof, 0.8), H("8FC3E6"), 0.35))


def silo(b, g, L, cx, yb, r, h, col):
    shadow(g, L, cx - r, yb, 2 * r, r, h, alpha=55)
    for xx in range(-r, r):
        t = (xx + r) / (2 * r)
        c = shade(col, 1.18 - 0.45 * t)
        b.rect(cx + xx, yb - h - r // 2, 1, h + r // 2, c)
    for yy in range(yb - h, yb, 5):
        b.hline(cx - r, cx + r - 1, yy, (0, 0, 0, 30))
    b.ellipse(cx, yb - h - r // 2, r, r * 0.45 + 0.5, shade(col, 1.15))
    b.ellipse(cx - 1, yb - h - r // 2 - 1, r * 0.4, r * 0.15 + 0.3, (255, 255, 255, 90))


def containers(b, x, yb, n, seed, stack=1):
    """n shipping containers side by side (9x3 tops + front face), stacked `stack` high."""
    r = random.Random(seed)
    for k in range(stack):
        for i in range(n):
            if k and r.random() < 0.35:
                continue
            col = r.choice(CONT)
            cx, cy = x + i * 10, yb - 5 - k * 2
            b.rect(cx, cy, 9, 3, col)
            b.rect(cx, cy, 9, 1, shade(col, 1.25))
            b.rect(cx, cy + 3, 9, 2, shade(col, 0.72))
            for xx in range(cx + 1, cx + 9, 2):
                b.set(xx, cy + 3, shade(col, 0.6))


def pipes(b, x0, x1, y, col=H("D9A441")):
    b.hline(x0, x1, y, col)
    b.hline(x0, x1, y + 1, shade(col, 0.7))
    for x in range(x0, x1, 8):
        b.rect(x, y + 2, 1, 4, H("7B828E"))


# =========================================================================== ind
def ind(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    s = L.seed("ind")
    r = L.rng("ind")
    pad(g, L, T, s)
    roof = IND_ROOF[v] if T < 3 else H("DCE0E4")
    wall = IND_WALL[v] if T < 3 else T3_WALL[v]
    if L.up:
        wall = shade(wall, 1.05)
    band = YEL if T < 3 else T3_BAND[v]
    yF = yB - 7
    hw = min(fw - 10, int(fw * (0.62 if fw > 32 else 0.7)))
    if v == 2 and fw > 32:
        hw = fw - 8
    hd = min(fd - 20, 13 + 14 * (L.cy - 1))
    hh = {1: 8, 2: 11, 3: 14}[T] + 2 * (L.cx >= 3) + 2 * (L.cy - 1)
    left = v % 2 == 0
    hx = 3 if left else fw - 3 - hw
    ox = hx + hw + 3 if left else 3  # open yard x range
    ow = fw - hw - 9
    parts = [dict(x=hx, yb=yF, w=hw, d=hd, h=hh, roof=roof, wall=wall, seed=s)]

    def after(rr, p):
        rx, ry, rw, rd = rr
        if T < 3:
            sawtooth(b, rr, roof, 5 if v != 2 else 7)
        else:
            parapet(b, rr, roof)
            roof_gear(b, rr, L, 0.6, solar=True)
        b.rect(p["x"], ry + rd, p["w"], 2, band)
        nd = max(1, p["w"] // 14)
        for i in range(nd):  # roller doors
            dx = p["x"] + 3 + i * (p["w"] - 6) // nd
            b.rect(dx, p["yb"] - 6, 7, 6, shade(band, 0.8) if T < 3 else H("C9CED6"))
            for k in range(0, 6, 2):
                b.hline(dx, dx + 6, p["yb"] - 6 + k, (0, 0, 0, 45))
        if p["h"] >= 10:
            G.windows(b, p["x"] + 2, ry + rd + 3, p["w"] - 4, 3, max(2, p["w"] // 5), 1, lit=0.5, seed=p["seed"])
        if T == 1 and L.up:
            roof_gear(b, rr, L, 0.6)
    parts[0]["after"] = after
    if L.cy >= 2:  # back yard: tanks / containers / trucks
        by = yF - hd - hh - 1
        if by - y0 > 14:
            nc = max(1, (fw - 8) // 20)
            if v % 2:
                containers(b, 4, by, nc, s + 1, 1 + (T >= 2))
                for i in range(max(1, (fw - 8 - nc * 10) // 14)):
                    G.tank(b, 10 + nc * 10 + i * 14, by - 9, 5)
            else:
                for i in range(max(1, (fw - 8) // 16)):
                    G.tank(b, 9 + i * 16, by - 9, 5, [H("B9C6D0"), H("D8D0C0")][i % 2])
            if by - y0 > 26:
                for i in range(max(1, fw // 30)):
                    truck(g, 4 + i * 26, y0 + 4, CONT[(i + v) % 6], horizontal=True)
    draw_parts(b, g, L, parts)
    # yard kit by tier / variant
    if ow >= 8:
        cx = ox + ow // 2
        if T == 1:
            containers(b, ox + 1, yF - 2, max(1, (ow - 6) // 10), s, 2)
            for i in range(2):
                b.rect(ox + 1 + i * 5, yF - 18, 4, 4, H("D2873A"))
                b.rect(ox + 1 + i * 5, yF - 18, 4, 1, H("EBA860"))
            G.stack(b, g, min(ox + ow - 5, fw - 10), yF - 14, 14, 3, seed=s, puff=True)
        elif T == 2:
            G.tank(b, cx - 3, yF - 8, min(5, ow // 2 - 1))
            if ow >= 16:
                G.tank(b, cx + 4, yF - 18, 4)
            G.stack(b, g, min(ox + ow - 5, fw - 10), yF - 12, 22 + 4 * L.up, 4, seed=s, puff=True)
            pipes(b, hx + 2, ox + ow - 2, yF - hd - hh + 4)
        else:
            n = 2 if ow < 20 else 3
            for i in range(n):
                silo(b, g, L, ox + 4 + i * (ow - 8) // max(1, n - 1), yF - 2 - (i % 2) * 6, 4, 18 + 4 * (i % 2), [H("E4E8EC"), H("D9DEE4")][i % 2])
            pipes(b, hx + 2, ox + ow - 2, yF - hd - hh + 4, H("B9C6D0"))
    else:  # narrow 1x1 lots: roof stack
        G.stack(b, g, fw - 10, yF - 3, 14 + 4 * T, 3, seed=s, puff=T < 3)
    if T >= 2:
        truck(b, fw - 18 if left else 4, yB - 7, CONT[v], horizontal=True)
    if T == 3 or L.up:
        trees(g, [(3, yB - 4, 2), (fw - 4, yB - 4, 2)], L)
    return finish(b, g)


# =========================================================================== warehouse
WH_ROOF = [H("A8AEB6"), H("B5A28C"), H("8F9A8E"), H("9AA6B8")]
WH_WALL = [H("D6D9DD"), H("D9CDB8"), H("C8D0C4"), H("CCD6E2")]


def warehouse(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    s = L.seed("wh")
    r = L.rng("wh")
    pad(g, L, T, s)
    asphalt(g, 0, yB - 18, fw, 15, s + 1)  # truck apron
    roof = WH_ROOF[v] if T < 3 else H("ECEEF0")
    wall = WH_WALL[v] if T < 3 else T3_WALL[v]
    if L.up:
        wall = shade(wall, 1.05)
    band = T3_BAND[v] if T == 3 else shade(wall, 0.7)
    yF = yB - 18
    D = min(fd - 26, 26 + 18 * (L.cy - 2))
    h = {1: 9, 2: 12, 3: 15}[T]
    parts = []
    if T == 1 and fw >= 96:  # two sheds
        sw = (fw - 10) // 2
        parts.append(dict(x=3, yb=yF, w=sw, d=D, h=h, roof=roof, wall=wall, seed=s))
        parts.append(dict(x=fw - 3 - sw, yb=yF - 4, w=sw, d=D - 4, h=h, roof=shade(roof, 1.05), wall=wall, seed=s + 1))
    else:
        parts.append(dict(x=3, yb=yF, w=fw - 6 - (14 if T == 3 else 0), d=D, h=h, roof=roof, wall=wall, seed=s))

    def after(rr, p):
        rx, ry, rw, rd = rr
        if T == 1:  # corrugated metal
            for xx in range(rx + 1, rx + rw - 1, 2):
                b.vline(xx, ry + 1, ry + rd - 2, (0, 0, 0, 22))
            G.pitched(b, rx, ry, rw, rd, p["roof"]) if v % 2 else None
        elif T == 2:
            for yy in range(ry + 3, ry + rd - 3, 6):
                for xx in range(rx + 4, rx + rw - 6, 10):
                    G.skylight(b, xx, yy, 5, 2)
        else:
            parapet(b, rr, p["roof"])
            rows = max(1, (rd - 8) // 7)
            for k in range(rows):
                for xx in range(rx + 4, rx + rw - 14, 14):
                    G.solar_panel(b, xx, ry + 4 + k * 7, 12, 4)
        if L.up:
            roof_gear(b, (rx, ry, rw, 6), L, 0.8)
        b.rect(p["x"], ry + rd, p["w"], 2, band)
        nd = max(2, (p["w"] - 6) // (9 if T == 1 else 7))
        for i in range(nd):  # dock doors
            dx = p["x"] + 3 + i * (p["w"] - 6) // nd
            b.rect(dx, p["yb"] - 6, 5, 5, H("5E646E") if T < 3 else H("C9CED6"))
            b.rect(dx, p["yb"] - 6, 5, 1, (255, 255, 255, 60))
            b.rect(dx - 1, p["yb"] - 1, 7, 1, H("2E2E2E"))  # dock leveller
        b.rect(p["x"] + 2, ry + rd + 3, p["w"] - 4, 1, (255, 255, 255, 40))
    for p in parts:
        p["after"] = after
    draw_parts(b, g, L, parts)
    if T == 3:  # glass office annex at the end of the shed
        ax = fw - 17
        rr = G.box3d(b, ax, yF + 2, 14, 12, 12, H("C7D0DA"), H("6FA3CC"))
        G.facade(b, ax, rr[1] + rr[3], 14, 12, "glass", s, lit=0.5)
    # trucks at the docks (more per level) + trailers
    p0 = parts[0]
    nd = max(2, (p0["w"] - 6) // 7)
    nt = min(nd, {1: 1, 2: 2, 3: 3}[T] + L.up + (fw >= 96))
    for i in range(nt):
        dx = p0["x"] + 3 + (i * 2 + v % 2) % nd * (p0["w"] - 6) // nd
        truck(b, dx, yF, CONT[(v + i) % 6], horizontal=False)
    for i in range(1 + T):
        tx = 4 + i * 16 + r.randrange(4)
        if tx + 14 < fw:
            truck(g, tx, yB - 8, CONT[(i + 2 * v) % 6], horizontal=True)
    if yF - D - h - y0 > 12:  # trailer park behind the shed
        for i in range(max(1, (fw - 6) // 16)):
            if r.random() < 0.7:
                truck(g, 4 + i * 16, y0 + 3, CONT[(i + v) % 6], horizontal=True)
    if T >= 2:
        g.rect(fw - 4, y0 + 2, 2, fd - 22, H("72B552"))
    return finish(b, g)


ZONES = {
    "ind": (1, "1x1 2x2 3x2 2x3 3x3 4x3 3x4", ind),
    "warehouse": (1, "3x2 2x3 3x3 4x3 3x4", warehouse),
}
