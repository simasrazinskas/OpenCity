"""Services art, part A: health, deathcare and education (3x3 frames are 96x128, 2x2 are 64x96, 1x1 are 32x64)."""
import random

from genworld import (H, new_layers, finish, pad, lot, shadow, box3d, pitched, windows, door, ac_unit, skylight, tree, bush,
                      cross, lawn, fence, flowerbed, bench, lamp, vehicle_top, shade, mix, chimney)


def hospital():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4BCC2"))
    lawn(g, 0, 114, 96, 14, base=H("78BE55"), seed=21)
    lot(g, 60, 98, 34, 14, H("5B6068"))
    # main block + tall ward wing
    shadow(g, 6, 106, 60, 38, 26)
    rx, ry, rw, rd = box3d(b, 6, 106, 60, 38, 26, H("F2F4F6"), H("F7F8FA"))
    b.rect(6, ry + rd, 60, 3, H("3FB8A0"))
    windows(b, 9, ry + rd + 6, 54, 10, 12, 2, lit=0.4, seed=31, dark_col=H("5E86AE"))
    b.rect(28, 94, 16, 12, H("D7DCE2"))
    b.rect(30, 96, 12, 10, (150, 200, 235, 230))
    b.rect(25, 90, 22, 3, H("3FB8A0"))           # entrance canopy
    b.rect(27, 93, 1, 3, H("9AA2AE"))
    b.rect(45, 93, 1, 3, H("9AA2AE"))
    shadow(g, 64, 90, 26, 28, 44)
    tx, ty, tw, td = box3d(b, 64, 90, 26, 28, 44, H("E4E8EC"), H("F7F8FA"))
    windows(b, 67, ty + td + 4, 20, 34, 5, 6, lit=0.45, seed=33, dark_col=H("5E86AE"))
    # helipad on the tall roof
    b.circle(tx + 13, ty + 14, 9, H("3C78C8"))
    b.circle(tx + 13, ty + 14, 7, H("2D5FA8"))
    b.rect(tx + 10, ty + 10, 1, 8, H("F4F6F8"))
    b.rect(tx + 15, ty + 10, 1, 8, H("F4F6F8"))
    b.rect(tx + 10, ty + 13, 6, 2, H("F4F6F8"))
    # red cross roof pad + gear
    b.rect(rx + 18, ry + 5, 24, 24, H("E9ECEF"))
    cross(b, rx + 30, ry + 17, 8, H("D93636"))
    ac_unit(b, rx + 3, ry + 4)
    ac_unit(b, rx + 47, ry + 6)
    skylight(b, rx + 4, ry + 24, 8, 5)
    # ambulances at the bay
    for i in range(2):
        g.rect(64 + i * 14, 102, 10, 6, H("F4F6F8"))
        g.rect(64 + i * 14, 102, 10, 1, H("FFFFFF"))
        g.rect(71 + i * 14, 103, 3, 4, H("5A7E9E"))
        g.rect(66 + i * 14, 104, 4, 1, H("D93636"))
        g.rect(67 + i * 14, 103, 2, 3, H("D93636"))
    tree(g, 6, 120, 4, H("3F9B4A"))
    tree(g, 84, 120, 4, H("3F9B4A"))
    tree(b, 3, 52, 3, H("3F9B4A"))
    return [finish(b, g)]


def cemetery():
    g, b = new_layers(96, 128)
    lawn(g, 0, 32, 96, 96, base=H("6EB251"), seed=41)
    fence(g, 0, 32, 96, 96, col=H("2B2F36"), sides="tblr")
    # gravel paths
    g.rect(44, 32, 8, 96, H("D8CDB0"))
    g.rect(0, 80, 96, 6, H("D8CDB0"))
    g.noise(0.05, seed=3, x=44, y=32, w=8, h=96)
    r = random.Random(7)
    for gy in range(42, 124, 9):
        for gx in range(6, 90, 7):
            if 42 <= gx <= 52 or 78 <= gy <= 88:
                continue
            col = r.choice([H("B9BEC6"), H("A5ABB4"), H("C9CDD3")])
            g.rect(gx + 1, gy + 3, 4, 4, (10, 18, 30, 60))
            b.rect(gx, gy, 3, 5, col)
            b.rect(gx, gy, 3, 1, shade(col, 1.25))
            if r.random() < 0.18:
                b.set(gx + 1, gy + 5, H("F26B6B"))
    # chapel
    shadow(g, 62, 74, 26, 20, 14)
    rx, ry, rw, rd = box3d(b, 62, 74, 26, 20, 14, H("8A5A4A"), H("EDE6D8"))
    pitched(b, rx, ry, rw, rd, H("7C4D3E"), ridge_vertical=True)
    b.rect(72, 66, 6, 8, H("5B3E2E"))
    b.rect(73, 67, 4, 7, (150, 200, 235, 220))
    b.rect(74, ry - 8, 2, 8, H("EDE6D8"))          # cross on top
    b.rect(72, ry - 6, 6, 2, H("EDE6D8"))
    # cypress trees and gate
    for (cx, cy) in ((6, 42), (88, 42), (6, 100), (88, 108), (30, 120)):
        tree(b, cx, cy, 4, H("2E7D45"))
    b.rect(40, 118, 3, 10, H("2B2F36"))
    b.rect(53, 118, 3, 10, H("2B2F36"))
    return [finish(b, g)]


def crematorium():
    out = []
    for t in range(4):
        g, b = new_layers(64, 96)
        pad(g, 0, 32, 64, 64, H("A9ADB2"))
        lawn(g, 0, 84, 64, 12, base=H("78BE55"), seed=43)
        shadow(g, 6, 82, 40, 28, 16)
        rx, ry, rw, rd = box3d(b, 6, 82, 40, 28, 16, H("8E8780"), H("E2DCD0"))
        pitched(b, rx, ry, rw, rd, H("7A746D"))
        b.rect(6, ry + rd, 40, 2, H("6B655F"))
        windows(b, 9, ry + rd + 3, 34, 5, 6, 1, lit=0.2, seed=44)
        door(b, 22, 76, 8, 6, H("5B3E2E"))
        # tall chimney with smoke
        shadow(g, 46, 62, 8, 8, 34, alpha=60)
        b.rect(46, 30, 8, 34, H("8A5A4A"))
        b.rect(46, 30, 8, 2, H("2A2A2A"))
        b.rect(52, 32, 2, 32, H("6B4437"))
        for i in range(3):
            b.circle(50 + (t + i) % 3 - 1 + i, 24 - i * 6, 2.5 + i * 0.7, (200, 205, 212, 200 - i * 55))
        tree(g, 56, 88, 3, H("3F9B4A"))
        out.append(finish(b, g))
    return out


def _school_yard(g, seed):
    lawn(g, 0, 32, 96, 96, base=H("7CC25A"), seed=seed)
    pad(g, 0, 96, 96, 32, H("C99F72"), edge=H("A67C52"))
    g.noise(0.05, seed=seed, x=0, y=96, w=96, h=32)


def highschool():
    g, b = new_layers(96, 128)
    _school_yard(g, 51)
    # running track + pitch
    g.ellipse(70, 112, 22, 12, H("C0583F"))
    g.ellipse(70, 112, 16, 7, H("5DAE45"))
    g.rect_outline(58, 107, 24, 11, H("F4F4F4"))
    fence(g, 0, 96, 96, 32)
    shadow(g, 6, 92, 80, 30, 22)
    rx, ry, rw, rd = box3d(b, 6, 92, 80, 30, 22, H("9B3F32"), H("EBD7B8"))
    pitched(b, rx, ry, rw, rd, H("9B3F32"))
    b.rect(6, ry + rd, 80, 2, H("7A3026"))
    windows(b, 9, ry + rd + 4, 74, 9, 15, 1, lit=0.45, seed=52)
    b.rect(40, 84, 12, 8, H("7A3026"))
    b.rect(42, 85, 8, 7, (150, 200, 235, 230))
    # gym dome on the right + side wing
    shadow(g, 62, 66, 26, 20, 20, alpha=60)
    box3d(b, 62, 66, 26, 20, 16, H("C8CDD3"), H("E6EAEE"))
    b.ellipse(75, 52, 12, 6, H("B8BEC6"))
    # flag + bell tower
    shadow(g, 14, 62, 10, 10, 30, alpha=60)
    box3d(b, 14, 62, 10, 10, 34, H("7A3026"), H("EBD7B8"))
    b.circle(19, 40, 3, H("FFFFFF"))
    b.set(19, 39, H("333333"))
    b.polygon([(13, 33), (19, 24), (25, 33)], H("7A3026"))
    tree(g, 4, 50, 4, H("3F9B4A"))
    tree(b, 90, 70, 4, H("3F9B4A"))
    for (x, y, col) in ((26, 104, "F26B6B"), (32, 108, "4C78C8"), (46, 106, "F2C94C")):
        g.rect(x, y, 2, 2, H(col))
    return [finish(b, g)]


def college():
    g, b = new_layers(96, 128)
    lawn(g, 0, 32, 96, 96, base=H("7CC25A"), seed=61)
    g.rect(40, 100, 16, 28, H("D8CDB0"))
    g.noise(0.05, seed=61, x=40, y=100, w=16, h=28)
    shadow(g, 8, 100, 80, 28, 26)
    rx, ry, rw, rd = box3d(b, 8, 100, 80, 28, 22, H("D9D2C3"), H("F0E6D2"))
    b.rect(8, ry + rd, 80, 2, H("B8AD96"))
    windows(b, 11, ry + rd + 4, 74, 9, 16, 1, lit=0.4, seed=62)
    # portico with columns and pediment
    b.rect(34, 84, 28, 3, H("F7F2E6"))
    for i in range(6):
        b.rect(35 + i * 5, 87, 3, 13, H("F7F2E6"))
        b.rect(35 + i * 5 + 2, 87, 1, 13, H("C9C0AC"))
    b.polygon([(32, 84), (48, 74), (64, 84)], H("E6DCC6"))
    b.polygon([(36, 83), (48, 77), (60, 83)], H("C9C0AC"))
    # central cupola
    shadow(g, 38, 66, 20, 14, 26, alpha=60)
    box3d(b, 38, 66, 20, 14, 14, H("C9503A"), H("F0E6D2"))
    b.ellipse(48, 46, 10, 6, H("C9503A"))
    b.ellipse(48, 44, 7, 4, H("E26B52"))
    b.rect(47, 32, 2, 10, H("C9CED6"))
    b.rect(48, 32, 5, 3, H("3C78C8"))
    # wings
    box3d(b, 8, 62, 22, 16, 18, H("C9503A"), H("EBD7B8"))
    box3d(b, 66, 62, 22, 16, 18, H("C9503A"), H("EBD7B8"))
    windows(b, 10, 49, 18, 6, 4, 1, lit=0.4, seed=63)
    windows(b, 68, 49, 18, 6, 4, 1, lit=0.4, seed=64)
    for (cx, cy) in ((6, 44), (90, 46), (28, 118), (68, 118)):
        tree(b, cx, cy, 4, H("3F9B4A"))
    flowerbed(g, 38, 98, 6, 3)
    return [finish(b, g)]


def university():
    g, b = new_layers(96, 128)
    lawn(g, 0, 32, 96, 96, base=H("72B84F"), seed=71)
    g.rect(14, 96, 68, 6, H("D8CDB0"))
    g.rect(44, 70, 8, 58, H("D8CDB0"))
    g.noise(0.05, seed=71, x=14, y=96, w=68, h=6)
    # library dome (left), main hall with bell tower (centre), lab wing (right)
    shadow(g, 4, 92, 30, 24, 22)
    lx, ly, lw, ld = box3d(b, 4, 92, 30, 24, 18, H("E6DCC6"), H("EFE7D6"))
    b.ellipse(19, ly + 6, 12, 8, H("4A78B8"))
    b.ellipse(18, ly + 4, 8, 5, H("6B97D6"))
    windows(b, 7, ly + ld + 3, 24, 6, 6, 1, lit=0.45, seed=72)
    shadow(g, 32, 100, 40, 30, 28)
    mx, my, mw, md = box3d(b, 32, 100, 40, 30, 26, H("C9D2DD"), H("F2EEE2"))
    b.rect(32, my + md, 40, 2, H("9FAEBF"))
    windows(b, 35, my + md + 4, 34, 10, 8, 2, lit=0.4, seed=73)
    b.rect(46, 90, 12, 10, H("5B6F8A"))
    b.rect(48, 92, 8, 8, (150, 200, 235, 230))
    shadow(g, 44, 62, 14, 12, 40, alpha=60)
    box3d(b, 44, 62, 14, 12, 40, H("4A78B8"), H("EFE7D6"))
    b.circle(51, 30, 3.4, H("FFFFFF"))
    b.set(51, 29, H("333333"))
    b.polygon([(43, 22), (51, 8), (59, 22)], H("4A78B8"))
    shadow(g, 70, 92, 24, 24, 20)
    rx, ry, rw, rd = box3d(b, 70, 92, 24, 24, 20, H("D8DDE3"), H("EDEFF2"))
    windows(b, 73, ry + rd + 3, 18, 8, 5, 2, lit=0.5, seed=74, glassy=True)
    skylight(b, rx + 4, ry + 4, 8, 5)
    ac_unit(b, rx + 14, ry + 5)
    for (cx, cy) in ((4, 62), (88, 66), (34, 124), (66, 124), (92, 120)):
        tree(b, cx, cy, 4, H("3F9B4A"))
    bench(g, 20, 108)
    bench(g, 74, 120)
    lamp(b, 12, 112)
    return [finish(b, g)]


def build():
    return {
        "hospital": (hospital(), None, 3),
        "cemetery": (cemetery(), None, 3),
        "crematorium": (crematorium(), 160, 2),
        "highschool": (highschool(), None, 3),
        "college": (college(), None, 3),
        "university": (university(), None, 3),
    }
