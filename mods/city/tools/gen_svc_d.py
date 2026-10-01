"""Services art, part D: generic stand-in icons for placeables of other work packages, and service effects (fire, garbage piles).

The stand-ins are only used for build-menu icons (they are drawn like small buildings, 64x96); the real actors are
drawn by their own work package.
"""
import math
import random

from genworld import (H, new_layers, finish, pad, lot, shadow, box3d, pitched, windows, door, ac_unit, tree, bush, lawn, fence,
                      shade, mix, tank, Canvas)


def farm_hub():
    g, b = new_layers(64, 96)
    lawn(g, 0, 32, 64, 64, base=H("C9B45A"), seed=201)
    for i in range(0, 64, 6):
        g.rect(i, 62, 3, 34, H("8FB44A"))
    shadow(g, 6, 70, 30, 24, 14)
    rx, ry, rw, rd = box3d(b, 6, 70, 30, 22, 14, H("B23A2E"), H("D9C7A0"))
    pitched(b, rx, ry, rw, rd, H("A82E24"))
    b.rect(14, 62, 12, 8, H("7A3026"))
    # silo
    tank(b, 46, 70, 7, H("C9CED6"))
    b.rect(39, 48, 14, 22, H("C9CED6"))
    b.ellipse(46, 48, 7, 3, H("8D95A0"))
    tree(b, 58, 56, 4, H("3F9B4A"))
    return finish(b, g)


def forestry_hub():
    g, b = new_layers(64, 96)
    lawn(g, 0, 32, 64, 64, base=H("6E9A4A"), seed=211)
    for (x, y) in ((6, 44), (18, 38), (50, 46), (58, 60)):
        tree(b, x, y, 5, H("2E7D45"))
    shadow(g, 8, 82, 30, 18, 12)
    rx, ry, rw, rd = box3d(b, 8, 82, 30, 18, 12, H("8A5A3C"), H("C9A57A"))
    pitched(b, rx, ry, rw, rd, H("6B4430"))
    for i in range(5):
        b.rect(42, 70 + i * 3, 16, 3, H("A8794A") if i % 2 else H("8A5A3C"))
        b.circle(58, 71 + i * 3, 1.6, H("D8B88A"))
    b.rect(14, 62, 2, 12, H("5B6068"))
    return finish(b, g)


def quarry_hub():
    g, b = new_layers(64, 96)
    g.rect(0, 32, 64, 64, H("A59D8E"))
    g.noise(0.1, seed=22, x=0, y=32, w=64, h=64)
    g.ellipse(32, 66, 26, 16, H("857E70"))
    g.ellipse(32, 68, 18, 10, H("6B655A"))
    g.ellipse(32, 70, 10, 5, H("4F4A42"))
    r = random.Random(23)
    for _ in range(18):
        x, y = r.randrange(4, 58), r.randrange(40, 90)
        b.rect(x, y, 3, 2, r.choice([H("C9C3B4"), H("B8B1A2"), H("D8D2C4")]))
    # crane
    b.rect(50, 40, 3, 40, H("E8C547"))
    b.rect(36, 40, 18, 2, H("E8C547"))
    b.rect(38, 42, 1, 10, H("2A2A2A"))
    b.rect(36, 52, 5, 4, H("5B6068"))
    shadow(g, 6, 92, 16, 10, 8, alpha=50)
    box3d(b, 6, 92, 16, 10, 8, H("7A8794"), H("C5CAD0"))
    return finish(b, g)


def mine_hub():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("8E8A80"))
    g.noise(0.1, seed=24, x=0, y=32, w=64, h=64)
    # headframe
    b.polygon([(14, 80), (26, 36), (38, 80)], H("5B6068"))
    b.polygon([(18, 80), (26, 44), (34, 80)], H("A9ADB2"))
    b.rect(24, 30, 4, 8, H("2A2A2A"))
    b.circle(26, 34, 4, H("C9CED6"))
    shadow(g, 40, 84, 20, 16, 12)
    rx, ry, rw, rd = box3d(b, 40, 84, 20, 16, 12, H("7A8794"), H("C5CAD0"))
    windows(b, 42, ry + rd + 2, 16, 4, 3, 1, lit=0.5, seed=25)
    # ore pile + cart
    g.ellipse(14, 86, 10, 5, H("4F4A42"))
    b.ellipse(14, 84, 8, 4, H("3A3631"))
    b.rect(30, 86, 8, 4, H("C9503A"))
    return finish(b, g)


def oil_hub():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A59D8E"))
    g.noise(0.1, seed=26, x=0, y=32, w=64, h=64)
    g.ellipse(14, 84, 12, 5, H("1F1F24"))
    # derrick
    b.polygon([(10, 80), (22, 30), (34, 80)], H("D9601E"))
    b.polygon([(14, 80), (22, 40), (30, 80)], H("A59D8E"))
    for y in range(44, 80, 8):
        half = (y - 30) // 4
        b.hline(22 - half, 22 + half, y, H("D9601E"))
    b.rect(21, 24, 2, 8, H("2A2A2A"))
    # tank
    tank(b, 48, 72, 8, H("D8DCE0"))
    b.rect(34, 70, 6, 2, H("5B6068"))
    # pump jack
    b.rect(42, 84, 14, 2, H("3B5FB8"))
    b.polygon([(44, 84), (48, 78), (52, 84)], H("3B5FB8"))
    return finish(b, g)


def busdepot():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A9ADB2"))
    lot(g, 0, 78, 64, 18, H("5B6068"))
    shadow(g, 4, 76, 52, 28, 14)
    rx, ry, rw, rd = box3d(b, 4, 76, 52, 28, 14, H("2E9E6A"), H("E6F0EA"))
    b.rect(4, ry + rd, 52, 2, H("1F7A50"))
    for i in range(3):
        gx = 8 + i * 16
        b.rect(gx, 64, 12, 12, H("D8DCE2"))
        for k in range(64, 76, 3):
            b.hline(gx, gx + 11, k, H("9AA2AE"))
        b.rect_outline(gx - 1, 63, 14, 13, H("1F7A50"))
    b.rect(8, ry + 4, 20, 8, H("1F7A50"))
    b.rect_outline(8, ry + 4, 20, 8, H("F4F6F8"))
    # buses
    for i in range(2):
        g.rect(8 + i * 22, 84, 18, 6, H("2E9E6A"))
        g.rect(8 + i * 22, 84, 18, 1, H("7ED9A8"))
        for k in range(3):
            g.rect(10 + i * 22 + k * 5, 85, 3, 2, H("5A7E9E"))
    return finish(b, g)


def busstop():
    g, b = new_layers(32, 64)
    pad(g, 0, 32, 32, 32, H("A9ADB2"))
    shadow(g, 6, 54, 20, 8, 12, alpha=60)
    b.rect(6, 42, 20, 2, H("2E9E6A"))
    b.rect(6, 44, 1, 10, H("8D95A0"))
    b.rect(25, 44, 1, 10, H("8D95A0"))
    b.rect(7, 44, 18, 8, (150, 200, 235, 130))
    b.rect(12, 50, 8, 1, H("8B5E3C"))
    b.rect(28, 34, 1, 20, H("C9CED6"))
    b.rect(26, 34, 5, 5, H("2E9E6A"))
    return finish(b, g)


def transformer():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A9ADB2"))
    fence(g, 4, 50, 56, 42, col=H("5B6068"), sides="tblr")
    for i in range(2):
        x = 10 + i * 24
        shadow(g, x, 78, 16, 12, 14, alpha=60)
        box3d(b, x, 78, 16, 12, 14, H("7A8794"), H("C5CAD0"))
        for k in range(3):
            b.rect(x + 3 + k * 4, 56, 2, 8, H("F2F4F6"))
            b.rect(x + 3 + k * 4, 55, 2, 1, H("E8C547"))
    # pylon
    b.polygon([(52, 90), (56, 36), (60, 90)], H("8D95A0"))
    b.rect(46, 44, 20, 1, H("8D95A0"))
    b.rect(48, 54, 16, 1, H("8D95A0"))
    b.rect(10, 84, 6, 4, H("E8C547"))
    return finish(b, g)


def sewage_outlet():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 40, H("A9ADB2"))
    g.rect(0, 70, 64, 26, H("4A7AA0"))
    g.noise(0.07, seed=27, x=0, y=70, w=64, h=26)
    shadow(g, 6, 70, 24, 14, 10, alpha=50)
    rx, ry, rw, rd = box3d(b, 6, 70, 24, 14, 10, H("7A8794"), H("C5CAD0"))
    # outfall pipe into the water
    b.rect(30, 62, 26, 8, H("9AA2AE"))
    b.rect(30, 62, 26, 2, H("C9CED6"))
    b.ellipse(56, 66, 3, 4, H("5B6068"))
    for i in range(4):
        b.circle(58 + i, 74 + i * 3, 2, (110, 100, 60, 150 - i * 25))
    b.rect(10, ry + 4, 6, 4, H("2A2A2A"))
    return finish(b, g)


def treatment_plant():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B4B6B8"))
    for (cx, cy, r) in ((18, 74, 12), (44, 74, 12), (30, 52, 10)):
        g.ellipse(cx + 2, cy + 2, r, r * 0.6, (10, 18, 30, 60))
        b.ellipse(cx, cy, r, r * 0.6, H("C9CED6"))
        b.ellipse(cx, cy - 1, r - 2, r * 0.6 - 2, H("4A88C0"))
        b.hline(cx - r + 2, cx + r - 3, cy - 1, (255, 255, 255, 90))
        b.line(cx, cy - 1, cx + r - 3, cy - 1, H("5B6068"), 1)
    shadow(g, 4, 94, 28, 14, 10, alpha=60)
    rx, ry, rw, rd = box3d(b, 4, 94, 28, 14, 10, H("3FA05E"), H("DCE8DF"))
    b.rect(36, 88, 24, 2, H("8D95A0"))
    return finish(b, g)


STANDINS = {
    "farm-hub": farm_hub, "forestry-hub": forestry_hub, "quarry-hub": quarry_hub, "mine-hub": mine_hub, "oil-hub": oil_hub,
    "busdepot": busdepot, "busstop": busstop, "transformer": transformer, "sewage-outlet": sewage_outlet,
    "treatment-plant": treatment_plant,
}


def fire_frames():
    """Four 32x48 flame frames (anchored at the bottom centre)."""
    out = []
    for t in range(4):
        c = Canvas(32, 48)
        r = random.Random(300 + t)
        # smoke column
        for i in range(4):
            c.circle(16 + (t + i) % 3 - 1 + i * 0.5, 14 - i * 3, 3 + i * 0.8, (60, 60, 64, 170 - i * 40))
        for k in range(3):
            cx = 9 + k * 7
            h = 14 + r.randrange(0, 8)
            for y in range(h):
                tt = y / h
                w = max(1, int((1 - tt) * 4.5))
                col = mix(H("F2C94C"), H("E8472E"), tt)
                c.rect(cx - w + (t + k) % 2, 46 - y, w * 2, 1, col)
            c.circle(cx + 0.5, 44, 2.5, H("FFF2A8"))
        out.append(c)
    return out


def pile_frames():
    """Three 32x32 garbage heaps (small, medium, large) anchored at the bottom centre."""
    out = []
    for n in range(3):
        c = Canvas(32, 32)
        r = random.Random(400 + n)
        for _ in range(10 + n * 12):
            x = 16 + r.randrange(-6 - n * 3, 7 + n * 3)
            y = 26 - r.randrange(0, 5 + n * 4)
            w, h = r.randrange(2, 5), r.randrange(2, 4)
            col = r.choice([H("3A3631"), H("5E7A4A"), H("8A8F96"), H("B8A58A"), H("2E4A78"), H("D8D2C4")])
            c.rect(x, y, w, h, col)
        c.outline(H("1B2230"))
        out.append(c)
    return out
