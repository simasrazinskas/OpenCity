"""Services art, part C: power plants, water pumping, garbage handling and rubble."""
import random

from genworld import (H, new_layers, finish, pad, lot, shadow, box3d, pitched, windows, door, ac_unit, skylight, tree, bush,
                      lawn, fence, vehicle_top, shade, mix, chimney, smoke, tank, stack, Canvas)


def powerplant_gas():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("A9ADB2"))
    # fuel tanks
    for (cx, cy) in ((20, 112), (40, 114)):
        g.ellipse(cx + 2, cy + 3, 11, 4, (10, 18, 30, 60))
    tank(b, 20, 108, 10, H("C8D0D8"))
    tank(b, 40, 110, 10, H("C8D0D8"))
    # turbine hall
    shadow(g, 6, 100, 78, 30, 20)
    rx, ry, rw, rd = box3d(b, 6, 100, 78, 30, 20, H("8E9AA6"), H("D4DBE2"))
    b.rect(6, ry + rd, 78, 2, H("6E7882"))
    windows(b, 9, ry + rd + 4, 72, 8, 14, 1, lit=0.5, seed=121)
    b.rect(0, ry + 12, 6, 6, H("5B6068"))
    for i in range(4):
        b.circle(rx + 12 + i * 18, ry + 14, 5, H("B8C0C8"))
        b.circle(rx + 12 + i * 18, ry + 14, 3, H("8E96A0"))
    # two stacks
    for sx in (66, 78):
        shadow(g, sx, 78, 6, 6, 40, alpha=60)
        b.rect(sx, 38, 6, 42, H("C9CED6"))
        b.rect(sx, 38, 6, 3, H("D93636"))
        b.rect(sx, 48, 6, 3, H("D93636"))
        b.rect(sx + 4, 41, 2, 39, H("8D95A0"))
    # pipes
    b.rect(14, 92, 52, 2, H("8D95A0"))
    b.rect(14, 91, 52, 1, H("C9CED6"))
    ac_unit(b, rx + 4, ry + 4)
    ac_unit(b, rx + 12, ry + 4)
    return [finish(b, g)]


def hydro_dam():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4B6B8"))
    # reservoir (upstream, top) and river (downstream, bottom)
    g.rect(0, 32, 96, 30, H("4A88C0"))
    g.noise(0.06, seed=9, x=0, y=32, w=96, h=30)
    g.rect(0, 100, 96, 28, H("5A9AD0"))
    g.noise(0.06, seed=10, x=0, y=100, w=96, h=28)
    for i in range(0, 96, 6):
        g.hline(i, i + 2, 108 + (i // 6) % 3 * 4, (255, 255, 255, 110))
    # dam wall
    shadow(g, 4, 100, 88, 22, 14)
    rx, ry, rw, rd = box3d(b, 4, 100, 88, 20, 12, H("C9CDD2"), H("A9AEB5"))
    b.rect(4, ry + rd, 88, 2, H("6E7782"))
    for i in range(6):
        b.rect(10 + i * 14, ry + rd + 3, 8, 7, H("6E7782"))
        b.rect(10 + i * 14, ry + rd + 3, 8, 1, H("8D95A0"))
    # spillway water
    for i in range(3):
        b.rect(20 + i * 14, ry + rd + 9, 6, 6, (230, 240, 250, 200))
    # power house
    shadow(g, 56, 94, 32, 22, 22)
    px, py, pw, pd = box3d(b, 56, 94, 32, 22, 22, H("5B7A9E"), H("D8DEE6"))
    windows(b, 59, py + pd + 4, 26, 8, 5, 1, lit=0.5, seed=131)
    b.rect(px + 4, py + 4, 10, 6, H("E8C547"))
    # pylon
    b.rect(14, 60, 1, 28, H("8D95A0"))
    b.rect(8, 62, 14, 1, H("8D95A0"))
    b.rect(10, 68, 10, 1, H("8D95A0"))
    b.line(8, 62, 14, 74, H("8D95A0"), 1)
    b.line(22, 62, 14, 74, H("8D95A0"), 1)
    return [finish(b, g)]


def _cooling_tower(b, g, cx, yb, h, steam_t):
    g.ellipse(cx + 5, yb + 1, 14, 5, (10, 18, 30, 60))
    for i in range(h):
        t = i / (h - 1)
        w = int(9 + 6 * abs(t - 0.45) * 2 - 0 + 2 * (1 - t) * 0)
        col = mix(H("E9ECEF"), H("B8BEC6"), t * 0.7)
        b.rect(cx - w, yb - h + i, w * 2, 1, col)
        b.set(cx + w - 1, yb - h + i, H("8D95A0"))
    b.ellipse(cx, yb - h, 9, 3, H("6E7782"))
    for i in range(3):
        b.circle(cx + (steam_t + i) % 3 - 1, yb - h - 4 - i * 6, 4 + i * 0.8, (236, 240, 244, 210 - i * 50))


def powerplant_nuclear(steam_t=0):
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4B6B8"))
    lawn(g, 0, 118, 96, 10, base=H("78BE55"), seed=141)
    _cooling_tower(b, g, 22, 80, 40, steam_t)
    _cooling_tower(b, g, 66, 70, 36, steam_t + 1)
    # turbine hall
    shadow(g, 6, 116, 60, 26, 16)
    rx, ry, rw, rd = box3d(b, 6, 116, 60, 26, 16, H("A9B3BC"), H("DDE2E8"))
    windows(b, 9, ry + rd + 3, 54, 6, 11, 1, lit=0.5, seed=142)
    # reactor dome
    shadow(g, 66, 112, 24, 22, 22, alpha=60)
    box3d(b, 66, 112, 24, 18, 14, H("C9CED6"), H("E4E8EC"))
    b.ellipse(78, 94, 11, 7, H("E4E8EC"))
    b.ellipse(77, 92, 8, 4, H("F4F6F8"))
    # warning stripe + switchyard
    b.rect(66, 112, 24, 2, H("F2C94C"))
    for x in range(66, 90, 4):
        b.rect(x, 112, 2, 2, H("2A2A2A"))
    b.rect(8, 100, 1, 8, H("8D95A0"))
    b.rect(5, 101, 7, 1, H("8D95A0"))
    return [finish(b, g)]


def waterpump_large():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B4B6B8"))
    g.rect(0, 80, 64, 16, H("5A9AD0"))
    g.noise(0.06, seed=12, x=0, y=80, w=64, h=16)
    shadow(g, 4, 80, 34, 24, 14)
    rx, ry, rw, rd = box3d(b, 4, 80, 34, 24, 14, H("4A78B8"), H("D8E4F0"))
    windows(b, 7, ry + rd + 3, 28, 5, 6, 1, lit=0.4, seed=151, dark_col=H("5E86AE"))
    b.rect(14, 74, 10, 6, H("2D4A94"))
    # tanks and pipes
    tank(b, 50, 74, 8, H("B9D6EA"))
    tank(b, 50, 54, 7, H("B9D6EA"))
    b.rect(36, 66, 14, 3, H("8D95A0"))
    b.rect(36, 65, 14, 1, H("C9CED6"))
    b.rect(52, 84, 3, 10, H("8D95A0"))
    for (cx, cy) in ((8, 54), (24, 50)):
        ac_unit(b, cx, cy)
    return [finish(b, g)]


def landfill():
    g, b = new_layers(96, 128)
    g.rect(0, 32, 96, 96, H("8B7355"))
    g.noise(0.12, seed=15, x=0, y=32, w=96, h=96)
    # terraced mound
    r = random.Random(16)
    for (cx, cy, rx_, ry_, col) in ((40, 78, 36, 24, "6E5A42"), (44, 74, 28, 18, "5E4D38"), (46, 70, 18, 11, "4F412F")):
        g.ellipse(cx, cy, rx_, ry_, H(col))
    for _ in range(90):
        x, y = r.randrange(8, 80), r.randrange(52, 100)
        g.set(x, y, r.choice([H("D8D2C4"), H("C0583F"), H("3C78C8"), H("E8C547"), H("2E8B57")]))
    fence(g, 0, 32, 96, 96, col=H("2B2F36"), sides="tblr")
    # site office + weighbridge
    shadow(g, 66, 112, 24, 14, 12, alpha=60)
    rx, ry, rw, rd = box3d(b, 66, 112, 24, 14, 12, H("7A8794"), H("D8DEE6"))
    windows(b, 69, ry + rd + 2, 18, 4, 4, 1, lit=0.5, seed=161)
    # garbage truck and bulldozer
    g.rect(14, 112, 16, 8, H("2E8B57"))
    g.rect(14, 112, 16, 1, H("5DBB63"))
    g.rect(30, 113, 5, 6, H("E8E8E8"))
    g.rect(40, 108, 10, 8, H("E8C547"))
    g.rect(38, 108, 2, 8, H("2A2A2A"))
    # gulls
    for (x, y) in ((20, 60), (60, 56), (74, 74)):
        b.set(x, y, H("F4F6F8"))
        b.set(x + 1, y - 1, H("F4F6F8"))
    return [finish(b, g)]


def incinerator():
    out = []
    for t in range(4):
        g, b = new_layers(96, 128)
        pad(g, 0, 32, 96, 96, H("A9ADB2"))
        lot(g, 0, 112, 96, 16, H("5B6068"))
        shadow(g, 6, 108, 70, 34, 22)
        rx, ry, rw, rd = box3d(b, 6, 108, 70, 34, 22, H("7A8794"), H("C5CAD0"))
        b.rect(6, ry + rd, 70, 2, H("5B6068"))
        windows(b, 9, ry + rd + 4, 64, 8, 12, 1, lit=0.45, seed=171)
        # furnace glow vents
        for i in range(3):
            b.rect(12 + i * 14, ry + rd + 12, 8, 4, H("F28A2E") if (t + i) % 2 == 0 else H("D9601E"))
        # bunker and crane
        shadow(g, 62, 96, 28, 22, 16, alpha=60)
        bx, by, bw, bd = box3d(b, 62, 96, 28, 22, 16, H("5B6068"), H("8E96A0"))
        b.rect(bx + 4, by + 5, 20, 12, H("2E2E2E"))
        # stacks with smoke
        for sx in (14, 26):
            shadow(g, sx, 76, 7, 7, 44, alpha=60)
            b.rect(sx, 24, 7, 54, H("B9A89A"))
            b.rect(sx + 5, 24, 2, 54, H("8A7A6D"))
            b.rect(sx, 24, 7, 2, H("2A2A2A"))
            b.rect(sx, 36, 7, 3, H("D93636"))
            for i in range(3):
                b.circle(sx + 3 + (t + i) % 3 - 1 + i, 20 - i * 6, 3 + i * 0.8, (205, 210, 216, 215 - i * 50))
        # trucks
        for i in range(2):
            g.rect(10 + i * 18, 118, 12, 6, H("2E8B57"))
            g.rect(22 + i * 18, 119, 4, 4, H("E8E8E8"))
        out.append(finish(b, g))
    return out


def recycling():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4B6B8"))
    lot(g, 0, 110, 96, 18, H("5B6068"))
    shadow(g, 6, 106, 62, 38, 20)
    rx, ry, rw, rd = box3d(b, 6, 106, 62, 38, 20, H("3FA05E"), H("DCE8DF"))
    b.rect(6, ry + rd, 62, 2, H("2A7A44"))
    windows(b, 9, ry + rd + 4, 56, 8, 11, 1, lit=0.45, seed=181)
    # recycling emblem on the roof
    b.circle(rx + 30, ry + 19, 12, H("E9F5EC"))
    for k in range(3):
        import math
        a = math.radians(k * 120 + 20)
        px, py = rx + 30 + math.cos(a) * 7, ry + 19 + math.sin(a) * 7
        b.circle(px, py, 2.4, H("2A7A44"))
    skylight(b, rx + 4, ry + 4, 8, 5)
    ac_unit(b, rx + 48, ry + 6)
    # sorted material bays
    for i, col in enumerate(("3C78C8", "E8C547", "2E8B57", "C9503A")):
        bx = 72 + (i % 2) * 11
        by = 70 + (i // 2) * 16
        shadow(g, bx, by + 12, 10, 8, 8, alpha=50)
        box3d(b, bx, by + 12, 10, 8, 8, H(col), shade(H(col), 0.8))
    # bales stack
    for i in range(3):
        b.rect(74 + i * 6, 104 - (i % 2) * 5, 6, 5, H("8E96A0"))
        b.rect(74 + i * 6, 104 - (i % 2) * 5, 6, 1, H("C9CED6"))
    # trucks
    for i in range(2):
        g.rect(10 + i * 18, 116, 12, 6, H("3FA05E"))
        g.rect(22 + i * 18, 117, 4, 4, H("E8E8E8"))
    return [finish(b, g)]


def rubble():
    out = []
    for t in range(2):
        g, b = new_layers(32, 64)
        r = random.Random(191)
        g.rect(2, 36, 28, 26, H("5E574E"))
        g.noise(0.15, seed=19, x=2, y=36, w=28, h=26)
        for _ in range(26):
            x, y = 3 + r.randrange(24), 38 + r.randrange(22)
            w, h = r.randrange(2, 6), r.randrange(2, 4)
            col = r.choice([H("9A9288"), H("6E6A64"), H("3A3631"), H("B8A58A"), H("7A3B2C")])
            b.rect(x, y, w, h, col)
            b.rect(x, y, w, 1, shade(col, 1.25))
        for k in range(3):
            b.rect(6 + k * 8, 44 + k * 3, 1, 8, H("2A2A2A"))
        for i in range(3):
            b.circle(14 + (t + i) % 2, 36 - i * 6, 2.5 + i * 0.6, (70, 70, 74, 150 - i * 40))
        out.append(finish(b, g))
    return out


def build():
    return {
        "powerplant-gas": ([powerplant_gas()[0]], None, 3),
        "hydro-dam": (hydro_dam(), None, 3),
        "powerplant-nuclear": ([powerplant_nuclear(t)[0] for t in range(3)], 200, 3),
        "waterpump-large": (waterpump_large(), None, 2),
        "landfill": (landfill(), None, 3),
        "incinerator": (incinerator(), 160, 3),
        "recycling": (recycling(), None, 3),
        "rubble": (rubble(), 200, 1),
    }
