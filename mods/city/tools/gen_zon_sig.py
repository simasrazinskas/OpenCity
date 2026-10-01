"""gen_zon_sig.py - hand-composed 3x3 signature buildings (one frame each)."""
import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (Lot, layers, finish, shadow, lawn, paving, hedge, pool, draw_parts,
                          roof_gear, roof_garden, antenna, helipad, awnings, shopfront,
                          parking_rows, parapet, street_trees)
from gen_zon_res import hipped
from gen_zon_com import glassf, big_sign, led, ribbon
from gen_zon_ind import silo, pipes


def lot(name, hr):
    return Lot("sig-" + name, 3, 3, hr, 5, 0)


def villa():
    L = lot("villa", 1)
    g, b = layers(L.W, L.H)
    y0, yB = L.y0, L.yB
    lawn(g, 0, y0, 96, 96, H("6FB850"), seed=1)
    hedge(g, 1, y0 + 1, 94, 3)
    g.rect(1, y0 + 1, 3, 94, H("3E8E45"))
    g.rect(92, y0 + 1, 3, 94, H("2F7A3A"))
    # driveway loop + fountain
    g.circle(48, yB - 16, 13, H("DCD3C0"))
    g.circle(48, yB - 16, 7, H("6FB850"))
    g.circle(48, yB - 16, 4, H("EDEBE4"))
    g.circle(48, yB - 16, 3, H("4FB0DD"))
    g.rect(44, yB - 6, 8, 6, H("DCD3C0"))
    for x in (10, 22, 74, 86):  # formal parterres
        g.rect(x - 5, yB - 24, 10, 14, H("3E8E45"))
        g.rect(x - 4, yB - 23, 8, 12, H("7CC25A"))
        G.flowerbed(g, x - 3, yB - 17, 6, seed=x)
    pool(g, 8, y0 + 8, 22, 8)
    for x in range(36, 92, 7):
        G.tree(g, x, y0 + 6, 3, H("2F7A3A"))
    yb = yB - 32
    wall, roof = H("F7F1E4"), H("9A6B52")
    shadow(g, L, 14, yb, 68, 22, 16)
    for (x, w, d, h, yy) in ((14, 16, 18, 10, yb + 2), (66, 16, 18, 10, yb + 2)):  # wings
        rr = G.box3d(b, x, yy, w, d, h, roof, wall)
        hipped(b, rr[0], rr[1] - 2, rr[2], rr[3] + 2, shade(roof, 1.05))
        G.windows(b, x + 2, yy - 7, w - 4, 3, 3, 1, lit=0.6, seed=x)
    rr = G.box3d(b, 28, yb, 40, 24, 16, roof, wall)
    hipped(b, rr[0], rr[1] - 3, rr[2], rr[3] + 3, roof)
    for f in range(2):
        G.windows(b, 30, yb - 14 + f * 7, 36, 3, 8, 1, lit=0.7, seed=f + 4)
    for x in range(31, 66, 6):  # portico columns
        b.rect(x, yb - 7, 1, 7, H("FFFFFF"))
    b.rect(40, yb - 9, 16, 2, H("FFFFFF"))
    G.door(b, 46, yb - 5, 4, 5, H("6B4A32"))
    for x in (36, 60):
        G.chimney(b, x, rr[1] + 2, 5, 3, H("C9B8A6"))
    G.car_top(g, 36, yB - 12, H("2E2E2E"))
    G.car_top(g, 56, yB - 22, H("C0392B"))
    return finish(b, g)


def garden_row():
    L = lot("garden-row", 1)
    g, b = layers(L.W, L.H)
    y0, yB = L.y0, L.yB
    lawn(g, 0, y0, 96, 96, H("74B954"), seed=2)
    g.rect(0, yB - 4, 96, 4, H("CFCBC2"))
    # communal garden with fountain and lime trees
    g.rect(46, yB - 30, 4, 26, H("DCD3C0"))
    g.circle(48, yB - 18, 5, H("EDEBE4"))
    g.circle(48, yB - 18, 4, H("4FB0DD"))
    for i, x in enumerate(range(8, 96, 16)):
        G.tree(g, x, yB - 9, 4, H("3F9B4A") if i % 2 else H("52A857"))
    for x in range(4, 92, 19):
        hedge(g, x, yB - 30, 15, 2)
    yb = yB - 34
    n = 5
    shadow(g, L, 0, yb, 96, 22, 24)
    cols = [H("F7F1E4"), H("F2E6CF"), H("EFE8DE"), H("F5EAD8"), H("F7F1E4")]
    for i in range(n):
        x, w = i * 96 // n, 96 // n + (1 if i < n - 1 else 0)
        rr = G.box3d(b, x, yb, w, 20, 22, H("4E5A6E"), cols[i], light=False)
        b.rect(rr[0], rr[1] - 2, rr[2], rr[3] + 2, H("4E5A6E"))
        b.rect(rr[0], rr[1] + rr[3] - 3, rr[2], 3, H("3A4456"))  # mansard slope
        b.rect(x + w // 2 - 2, rr[1] + rr[3] - 3, 4, 2, H("E8E4DA"))
        b.rect(x, rr[1] + rr[3], 1, 22, shade(cols[i], 0.75))
        for f in range(3):
            G.windows(b, x + 2, yb - 20 + f * 6, w - 4, 3, 3, 1, lit=0.6, seed=i * 5 + f, ww=2, wh=3)
        b.rect(x + 1, yb - 15, w - 2, 1, H("2C3440"))  # wrought-iron balcony
        G.door(b, x + w // 2 - 1, yb - 5, 3, 5, [H("2E4C7A"), H("7A2E2E"), H("2E6B4C")][i % 3])
        b.rect(x + w // 2 - 2, yb, 5, 2, H("D9D4C8"))
        G.flowerbed(b, x + 3, yb - 9, w - 6, seed=i)
    for x in range(0, 96, 6):
        G.chimney(b, x + 1, yb - 22 - 20 + 4, 3, 2, H("C9B8A6")) if x % 18 == 0 else None
    return finish(b, g)


def sky_tower():
    L = lot("sky-tower", 3)
    L.top = 24
    g, b = layers(L.W, L.H)
    paving(g, 0, L.y0, 96, 96, H("D6D2CA"), 8)
    lawn(g, 4, L.yB - 22, 30, 16, seed=3)
    lawn(g, 62, L.yB - 22, 30, 16, seed=4)
    for (x, y) in ((10, L.yB - 16), (24, L.yB - 12), (72, L.yB - 15), (86, L.yB - 11)):
        G.tree(g, x, y, 4, H("3F9B4A"))
    pod = dict(x=12, yb=L.yB - 26, w=72, d=40, h=12, roof=H("B8BEC6"), wall=H("E6E1D6"), style="retail", seed=7, lit=0.6, accent=H("1FA37A"))
    yb = L.yB - 46
    tw = dict(x=32, yb=yb, w=32, d=20, h=yb - 20 - 22, roof=H("C9CED6"), wall=H("A9C8F0"), seed=8, z=1)

    def after(rr, p):
        x, y, w, d = rr
        if p is tw:
            glassf(b, p["x"], y + d, p["w"], p["h"], H("CFE4F7"), H("3F5F94"), 8, 0.55, 4, 5)
            for k in range(0, p["h"], 15):  # sky gardens
                b.rect(p["x"] + 1, y + d + k + 10, p["w"] - 2, 2, H("5DB062"))
            b.rect(x + 4, y - 4, w - 8, 4, H("DDE3EA"))
            b.rect(x + 8, y - 8, w - 16, 4, H("C9CED6"))
            antenna(b, x + w // 2, y - 8, 12)
        else:
            roof_garden(b, rr, 9)
    pod["after"] = after
    tw["after"] = after
    draw_parts(b, g, L, [pod, tw])
    return finish(b, g)


def bazaar():
    L = lot("bazaar", 1)
    g, b = layers(L.W, L.H)
    paving(g, 0, L.y0, 96, 96, H("E2D2B0"), 6)
    cols = [H("E8467C"), H("F2C94C"), H("1FA37A"), H("2C6FD1"), H("E67E22"), H("8E44AD")]
    yb = L.yB - 30
    shadow(g, L, 8, yb, 80, 40, 14)
    rr = G.box3d(b, 8, yb, 80, 40, 14, H("D98C5F"), H("F2E3C6"))
    for i in range(5):  # barrel-vaulted roof bays in alternating colours
        x = rr[0] + i * 16
        for k in range(16):
            f = 1.25 - abs(k - 7.5) / 7.5 * 0.5
            b.rect(x + k, rr[1], 1, rr[3], shade(mix(cols[i % 6], H("D98C5F"), 0.4), f))
        b.rect(x, rr[1], 1, rr[3], H("7A5A40"))
    b.rect(rr[0], rr[1] + rr[3] // 2 - 1, rr[2], 2, (255, 255, 255, 120))
    for x in range(12, 86, 10):  # arches
        b.rect(x, yb - 9, 6, 9, H("6B4A32"))
        b.rect(x + 1, yb - 8, 4, 2, H("FFD98A"))
    awnings(b, 9, yb - 12, 78, cols, 8)
    for x in (10, 86):  # flag poles
        b.rect(x, rr[1] - 10, 1, 10, H("7B828E"))
        b.rect(x + 1, rr[1] - 10, 5, 3, cols[x % 6])
    for i, x in enumerate(range(6, 90, 14)):  # market stalls in front
        c = cols[i % 6]
        G.box3d(b, x, L.yB - 8, 10, 6, 3, c, H("F4F4F4"))
        for k in range(0, 10, 2):
            b.rect(x + k, L.yB - 17, 1, 6, (255, 255, 255, 120))
        b.rect(x + 2, L.yB - 7, 2, 1, H("F2C94C"))
        b.rect(x + 5, L.yB - 7, 2, 1, H("E84545"))
    import random
    r = random.Random(5)
    for _ in range(40):  # crowd
        g.set(r.randrange(2, 94), L.yB - 26 + r.randrange(14), r.choice(cols + [H("2E2E2E"), H("FFFFFF")]))
    return finish(b, g)


def mall():
    L = lot("mall", 2)
    g, b = layers(L.W, L.H)
    parking_rows(g, 0, L.y0, 96, 96, 11, fill=0.75)
    yb = L.yB - 30
    p = dict(x=4, yb=yb, w=88, d=50, h=22, roof=H("D2D8E0"), wall=H("E9EEF4"), seed=12, accent=H("E8467C"))

    def after(rr, q):
        x, y, w, d = rr
        glassf(b, q["x"] + 26, y + d, 36, q["h"], H("CFE9F7"), H("5A90B8"), 3, 0.6, 4, 5)
        b.ellipse(x + w // 2, y + d // 2, 18, 13, H("8AB4CC"))   # glass dome atrium
        b.ellipse(x + w // 2, y + d // 2, 16, 11, H("BFE4F5"))
        for k in range(-14, 15, 4):
            b.vline(x + w // 2 + k, y + d // 2 - 9, y + d // 2 + 9, (90, 140, 170, 140))
        b.ellipse(x + w // 2 - 6, y + d // 2 - 4, 6, 3, (255, 255, 255, 140))
        roof_gear(b, (x, y, 22, d), L, 1.0)
        roof_gear(b, (x + w - 22, y, 22, d), L, 1.0, solar=True)
        big_sign(b, x + 28, y - 7, 32, H("E8467C"), 4, True)
        led(b, q["x"] + 4, y + d + 4, 16, 9, 2)
        led(b, q["x"] + q["w"] - 20, y + d + 4, 16, 9, 3)
        awnings(b, q["x"] + 2, q["yb"] - 7, 22, [H("E8467C")], 22)
        awnings(b, q["x"] + q["w"] - 24, q["yb"] - 7, 22, [H("2C6FD1")], 22)
    p["after"] = after
    draw_parts(b, g, L, [p])
    g.rect(0, L.yB - 4, 96, 4, H("C9CCD1"))
    return finish(b, g)


def glass_hub():
    L = lot("glass-hub", 3)
    L.top = 24
    g, b = layers(L.W, L.H)
    paving(g, 0, L.y0, 96, 96, H("D8DADC"), 8)
    lawn(g, 2, L.yB - 8, 92, 5, base=H("72B552"), seed=6)
    g.circle(20, L.yB - 24, 7, H("4FB0DD"))  # reflecting pool
    street_trees(g, L, L.yB - 5, 12, 3)
    yb = L.yB - 30
    tiers = [(14, yb, 68, 30, 40), (20, yb - 3, 56, 24, 80), (28, yb - 6, 40, 18, 120)]
    parts = []
    for i, (x, y, w, d, h) in enumerate(tiers):
        parts.append(dict(x=x, yb=y, w=w, d=d, h=h, roof=H("BFC9D6"), wall=H("8FC3E6"), seed=20 + i, z=i))

    def after(rr, p):
        x, y, w, d = rr
        glassf(b, p["x"], y + d, p["w"], p["h"] - (0 if p["z"] == 2 else 0), H("D4ECFA"), H("2E5C8C"), p["seed"], 0.6, 3, 4)
        parapet(b, rr, p["roof"])
        if p["z"] < 2:
            roof_garden(b, rr, p["seed"])
        else:
            helipad(b, x + w // 2, y + d // 2, 11)
            b.polygon([(x + w // 2 - 3, y), (x + w // 2 + 3, y), (x + w // 2, y - 14)], H("DDE3EA"))
            antenna(b, x + w // 2, y - 14, 6)
    for p in parts:
        p["after"] = after
    draw_parts(b, g, L, parts)
    return finish(b, g)


def fuel_plant():
    L = lot("fuel-plant", 2)
    g, b = layers(L.W, L.H)
    g.rect(0, L.y0, 96, 96, H("BEC2C6"))
    g.noise(0.03, seed=9, x=0, y=L.y0, w=96, h=96)
    lawn(g, 0, L.yB - 6, 96, 4, base=H("72B552"), seed=9)
    for x in range(0, 96, 6):
        g.rect(x, L.yB - 2, 3, 1, G.YELLOW)
    for i, x in enumerate((14, 34)):  # spherical tanks
        cy = L.y0 + 22
        g.ellipse(x + 3, cy + 10, 9, 4, (10, 18, 30, 60))
        b.circle(x, cy, 9, H("C9D1D8"))
        b.circle(x - 2, cy - 2, 6, H("EEF2F5"))
        b.circle(x - 4, cy - 4, 2, H("FFFFFF"))
        b.rect(x - 7, cy + 7, 1, 5, H("7B828E"))
        b.rect(x + 6, cy + 7, 1, 5, H("7B828E"))
    for i in range(3):  # distillation columns
        silo(b, g, L, 58 + i * 11, L.yB - 40 + (i % 2) * 4, 3, 46 + (i % 2) * 10, H("E4E8EC"))
    hall = dict(x=6, yb=L.yB - 10, w=44, d=22, h=12, roof=H("DCE0E4"), wall=H("F0F2F4"), seed=3)

    def after(rr, p):
        parapet(b, rr, p["roof"])
        roof_gear(b, rr, L, 0.6, solar=True)
        b.rect(p["x"], rr[1] + rr[3], p["w"], 2, H("1FA37A"))
        ribbon(b, p["x"], rr[1] + rr[3] + 2, p["w"], p["h"] - 2, 3, 0.6, 5)
    hall["after"] = after
    draw_parts(b, g, L, [hall])
    for x in (56, 78):
        G.tank(b, x, L.yB - 16, 6, H("E4E8EC"))
    pipes(b, 20, 90, L.yB - 30, H("1FA37A"))
    b.rect(90, L.y0 - 30, 2, 52, H("B9C0C8"))  # flare stack
    b.circle(91, L.y0 - 33, 2, H("F2A93B"))
    b.set(91, L.y0 - 35, H("FFE08A"))
    return finish(b, g)


SIGS = {
    "villa": (1, villa),
    "garden-row": (1, garden_row),
    "sky-tower": (3, sky_tower),
    "bazaar": (1, bazaar),
    "mall": (2, mall),
    "glass-hub": (3, glass_hub),
    "fuel-plant": (2, fuel_plant),
}
