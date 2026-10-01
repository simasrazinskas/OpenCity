"""Services art, part B: police, fire, parks/sport, post, telecom and administration."""
import random

from genworld import (H, new_layers, finish, pad, lot, shadow, box3d, pitched, windows, door, ac_unit, skylight, tree, bush,
                      cross, lawn, fence, flowerbed, bench, lamp, vehicle_top, shade, mix, parking, car_top, CARCOLS)


def policebox():
    g, b = new_layers(32, 64)
    pad(g, 0, 32, 32, 32, H("A9ADB2"))
    shadow(g, 6, 58, 20, 14, 14)
    rx, ry, rw, rd = box3d(b, 6, 58, 20, 14, 14, H("3B5FB8"), H("E6EBF2"))
    b.rect(6, ry + rd, 20, 2, H("2D4A94"))
    windows(b, 8, ry + rd + 3, 16, 4, 4, 1, lit=0.5, seed=7)
    b.rect(13, 52, 6, 6, H("2D4A94"))
    b.rect(14, 53, 4, 5, (180, 215, 245, 220))
    b.rect(14, ry - 3, 2, 3, H("E84545"))
    b.rect(16, ry - 3, 2, 3, H("3C78F0"))
    b.rect(28, ry - 6, 1, 12, H("C9CED6"))
    vehicle_top(g, 9, 59, 3, 5, H("F2F4F6"), H("3C78F0")) if False else None
    tree(g, 3, 60, 3, H("3F9B4A"))
    return [finish(b, g)]


def firehouse():
    g, b = new_layers(32, 64)
    pad(g, 0, 32, 32, 32, H("AAAEB2"))
    shadow(g, 3, 58, 24, 16, 14)
    rx, ry, rw, rd = box3d(b, 3, 58, 24, 16, 14, H("C23B3B"), H("EBC8C0"))
    pitched(b, rx, ry, rw, rd, H("B83232"))
    b.rect(3, ry + rd, 24, 2, H("A82A2A"))
    for i in range(2):
        gx = 5 + i * 11
        b.rect(gx, 49, 9, 9, H("D8DCE2"))
        for k in range(49, 58, 3):
            b.hline(gx, gx + 8, k, H("9AA2AE"))
        b.rect_outline(gx - 1, 48, 11, 10, H("A82A2A"))
    b.rect(8, ry + 4, 10, 4, H("8E2A2A"))
    b.rect_outline(8, ry + 4, 10, 4, H("F2C94C"))
    shadow(g, 24, 46, 6, 6, 18, alpha=50)
    box3d(b, 24, 46, 6, 6, 16, H("A82A2A"), H("D9ADA4"))
    tree(g, 3, 62, 3, H("3F9B4A"))
    return [finish(b, g)]


def telecom_mast():
    out = []
    for t in range(2):
        g, b = new_layers(32, 64)
        pad(g, 6, 44, 20, 18, H("B4B6B8"))
        g.line(16, 56, 28, 60, (10, 18, 30, 55), 2)
        # lattice mast with red/white bands
        for y in range(8, 56):
            band = H("D93636") if (y // 6) % 2 == 0 else H("F4F6F8")
            half = 1 + (y - 8) // 16
            b.set(16 - half, y, band)
            b.set(16 + half, y, band)
            if y % 4 == 0:
                b.hline(16 - half, 16 + half, y, shade(band, 0.8))
        b.rect(11, 54, 10, 4, H("D8DCE0"))
        b.rect(11, 57, 10, 1, H("8D95A0"))
        b.rect(21, 52, 5, 4, H("5B6068"))
        # dishes
        b.circle(12, 22, 2.2, H("F4F6F8"))
        b.circle(20, 30, 2.2, H("F4F6F8"))
        b.set(16, 6, H("FF3030") if t == 0 else H("7A1A1A"))
        out.append(finish(b, g))
    return out


def sportsfield():
    g, b = new_layers(64, 96)
    lawn(g, 0, 32, 64, 64, base=H("7CC25A"), seed=81)
    g.rect(4, 38, 56, 52, H("C0583F"))           # track
    g.rect(10, 44, 44, 40, H("5DAE45"))          # pitch
    for i in range(0, 44, 8):
        g.rect(10 + i, 44, 4, 40, H("69BC4F"))
    g.rect_outline(10, 44, 44, 40, H("F4F4F4"))
    g.hline(10, 53, 64, H("F4F4F4"))
    g.circle(32, 64, 5, (255, 255, 255, 0))
    g.rect_outline(26, 59, 12, 10, H("F4F4F4"))
    g.rect_outline(4, 38, 56, 52, H("A44830"))
    fence(g, 0, 32, 64, 64, sides="tblr")
    # stands on the south edge
    shadow(g, 6, 94, 40, 6, 8, alpha=50)
    box3d(b, 6, 94, 40, 6, 7, H("5B8DD0"), H("C9D3DF"))
    for i in range(0, 40, 4):
        b.set(7 + i, 90, H("F2C94C"))
    b.rect(52, 70, 1, 22, H("C9CED6"))
    b.rect(53, 70, 5, 3, H("E84545"))
    tree(b, 3, 52, 3, H("3F9B4A"))
    tree(b, 60, 90, 3, H("3F9B4A"))
    return [finish(b, g)]


def postoffice():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B4B6B8"))
    lot(g, 0, 80, 64, 16, H("5B6068"))
    shadow(g, 5, 78, 44, 24, 16)
    rx, ry, rw, rd = box3d(b, 5, 78, 44, 24, 16, H("E8C547"), H("F5EBC8"))
    b.rect(5, ry + rd, 44, 2, H("C9A52E"))
    windows(b, 8, ry + rd + 4, 38, 6, 7, 1, lit=0.4, seed=91)
    b.rect(20, 70, 12, 8, H("5B3E2E"))
    b.rect(22, 71, 8, 7, (150, 200, 235, 230))
    b.rect(8, ry + 4, 18, 8, H("C9503A"))              # sign
    b.rect_outline(8, ry + 4, 18, 8, H("F4F6F8"))
    b.rect(13, ry + 7, 8, 2, H("F4F6F8"))
    ac_unit(b, rx + 30, ry + 4)
    skylight(b, rx + 30, ry + 12, 6, 4)
    # vans
    for i in range(3):
        g.rect(8 + i * 11, 84, 9, 5, H("E8C547"))
        g.rect(8 + i * 11, 84, 9, 1, H("FFF4B8"))
        g.rect(8 + i * 11 + 6, 85, 2, 3, H("5A7E9E"))
    # mailbox
    b.rect(54, 72, 4, 6, H("3B5FB8"))
    b.rect(54, 72, 4, 1, H("6B97D6"))
    tree(g, 58, 90, 3, H("3F9B4A"))
    return [finish(b, g)]


def telecom_tower():
    out = []
    for t in range(2):
        g, b = new_layers(64, 96)
        pad(g, 0, 32, 64, 64, H("B4B6B8"))
        fence(g, 6, 60, 52, 32, col=H("5B6068"), sides="tblr")
        g.line(34, 82, 54, 90, (10, 18, 30, 55), 3)
        # concrete shaft
        for y in range(12, 82):
            w = 5 if y < 40 else 6 + (y - 40) // 14
            b.rect(32 - w // 2, y, w, 1, shade(H("D2D6DB"), 1.08 - 0.25 * ((y - 12) / 70)))
            b.set(32 + w // 2 - 1, y, H("8D95A0"))
        b.rect(26, 80, 12, 5, H("C9CED6"))
        b.rect(26, 84, 12, 1, H("6E7682"))
        # platforms with dishes
        for (py, n) in ((30, 3), (44, 4)):
            b.rect(27, py, 10, 2, H("8D95A0"))
            for k in range(n):
                dx = 24 + k * 5
                b.circle(dx + 1, py - 2, 2, H("F4F6F8"))
                b.set(dx + 1, py - 2, H("8D95A0"))
        # antenna mast with beacon
        b.rect(32, 2, 1, 12, H("C9CED6"))
        b.set(32, 1, H("FF3030") if t == 0 else H("7A1A1A"))
        # equipment shed
        shadow(g, 42, 90, 14, 10, 8, alpha=60)
        box3d(b, 42, 90, 14, 10, 8, H("8E96A0"), H("C9CED6"))
        b.rect(10, 84, 10, 3, H("E8C547"))
        out.append(finish(b, g))
    return out


def welfare():
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("BDB6A8"))
    lawn(g, 0, 82, 64, 14, base=H("78BE55"), seed=95)
    shadow(g, 5, 80, 46, 26, 16)
    rx, ry, rw, rd = box3d(b, 5, 80, 46, 26, 16, H("D9803B"), H("F6E2C6"))
    pitched(b, rx, ry, rw, rd, H("D9803B"))
    b.rect(5, ry + rd, 46, 2, H("B8652A"))
    windows(b, 8, ry + rd + 3, 40, 6, 8, 1, lit=0.45, seed=96)
    b.rect(22, 72, 10, 8, H("B8652A"))
    b.rect(24, 73, 6, 7, (150, 200, 235, 230))
    # heart sign
    b.circle(15, ry + 10, 3, H("E8467C"))
    b.circle(20, ry + 10, 3, H("E8467C"))
    b.polygon([(12, ry + 11), (23, ry + 11), (17, ry + 18)], H("E8467C"))
    bench(g, 40, 92)
    bench(g, 8, 92)
    flowerbed(g, 20, 90, 8, 5)
    tree(g, 58, 56, 4, H("3F9B4A"))
    tree(g, 59, 90, 3, H("3F9B4A"))
    return [finish(b, g)]


def police_hq():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("A9ADB2"))
    lot(g, 0, 100, 96, 28, H("5B6068"))
    parking(g, 8, 104, 50, 3, [H("F4F6F8")])
    for i in range(5):
        g.rect(10 + i * 8, 106, 5, 10, H("F2F4F6"))
        g.rect(10 + i * 8, 106, 5, 2, H("3C78F0") if i % 2 else H("E84545"))
    shadow(g, 8, 96, 56, 30, 24)
    rx, ry, rw, rd = box3d(b, 8, 96, 56, 30, 24, H("3B5FB8"), H("E6EBF2"))
    b.rect(8, ry + rd, 56, 3, H("2D4A94"))
    windows(b, 11, ry + rd + 5, 50, 12, 11, 2, lit=0.45, seed=101)
    b.rect(30, 86, 12, 10, H("2D4A94"))
    b.rect(32, 88, 8, 8, (180, 215, 245, 220))
    # tower
    shadow(g, 58, 90, 30, 28, 50, alpha=70)
    tx, ty, tw, td = box3d(b, 58, 90, 30, 28, 50, H("2D4A94"), H("DDE5F0"))
    windows(b, 61, ty + td + 4, 24, 40, 6, 7, lit=0.4, seed=103, glassy=True)
    b.polygon([(tx + 11, ty + 14), (tx + 19, ty + 14), (tx + 19, ty + 20), (tx + 15, ty + 25), (tx + 11, ty + 20)], H("F2C94C"))
    b.rect(tx + 14, ty - 14, 1, 14, H("C9CED6"))
    b.rect(tx + 15, ty - 14, 5, 3, H("3C78F0"))
    b.rect(rx + 20, ry - 3, 3, 3, H("E84545"))
    b.rect(rx + 23, ry - 3, 3, 3, H("3C78F0"))
    ac_unit(b, rx + 4, ry + 5)
    skylight(b, rx + 4, ry + 14, 8, 5)
    tree(g, 4, 120, 4, H("3F9B4A"))
    tree(g, 90, 124, 4, H("3F9B4A"))
    return [finish(b, g)]


def prison():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("A5A39B"))
    g.rect(8, 44, 80, 78, H("B8B4AA"))            # yard
    g.noise(0.05, seed=5, x=8, y=44, w=80, h=78)
    # perimeter wall
    g.rect_outline(6, 42, 84, 82, H("6B6A66"), t=2)
    shadow(g, 6, 124, 84, 4, 8, alpha=60)
    b.rect(6, 122, 84, 4, H("8D8B85"))
    b.rect(6, 122, 84, 1, H("C9C7C0"))
    for x in range(8, 88, 3):
        b.set(x, 121, H("5B5A56"))                # barbed wire hint
    # cell block
    shadow(g, 18, 100, 60, 34, 22)
    rx, ry, rw, rd = box3d(b, 18, 100, 60, 34, 22, H("8E9AA6"), H("C5C9CE"))
    b.rect(18, ry + rd, 60, 2, H("6E7882"))
    for i in range(14):
        b.rect(21 + i * 4, ry + rd + 5, 2, 4, H("2C3A52"))
        b.rect(21 + i * 4, ry + rd + 12, 2, 4, H("2C3A52"))
    b.rect(42, 88, 12, 12, H("5B6068"))
    b.rect(44, 90, 8, 10, H("2C3A52"))
    skylight(b, rx + 6, ry + 6, 8, 5)
    skylight(b, rx + 44, ry + 8, 8, 5)
    # watchtowers on the corners
    for (tx, ty) in ((6, 58), (80, 58), (80, 118)):
        shadow(g, tx, ty + 6, 8, 8, 24, alpha=60)
        box3d(b, tx, ty + 6, 8, 8, 20, H("6E7882"), H("A9ADB2"))
        b.rect(tx - 1, ty - 18, 10, 6, H("3A4150"))
        b.rect(tx, ty - 17, 8, 3, (255, 217, 138, 230))
    # exercise yard hoops
    b.rect(14, 62, 1, 7, H("C9CED6"))
    b.rect(12, 62, 5, 1, H("E8C547"))
    return [finish(b, g)]


def sortingcenter():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4B6B8"))
    lot(g, 0, 104, 96, 24, H("5B6068"))
    for i in range(5):
        g.vline(8 + i * 18, 108, 126, (235, 235, 235, 150))
    shadow(g, 6, 102, 84, 44, 22)
    rx, ry, rw, rd = box3d(b, 6, 102, 84, 44, 22, H("E8C547"), H("F5EBC8"))
    b.rect(6, ry + rd, 84, 3, H("C9A52E"))
    # loading docks
    for i in range(5):
        dx = 10 + i * 16
        b.rect(dx, 90, 12, 12, H("8E96A0"))
        for k in range(90, 102, 3):
            b.hline(dx, dx + 11, k, H("6E7682"))
        b.rect_outline(dx - 1, 89, 14, 13, H("5B6068"))
    b.rect(10, ry + 6, 30, 10, H("C9503A"))
    b.rect_outline(10, ry + 6, 30, 10, H("F4F6F8"))
    b.rect(18, ry + 10, 14, 2, H("F4F6F8"))
    ac_unit(b, rx + 50, ry + 6)
    ac_unit(b, rx + 60, ry + 6)
    skylight(b, rx + 50, ry + 18, 10, 6)
    skylight(b, rx + 66, ry + 18, 10, 6)
    # trucks at the docks
    for i in range(3):
        g.rect(12 + i * 24, 110, 14, 7, H("F4F6F8"))
        g.rect(12 + i * 24 + 14, 111, 4, 5, H("E8C547"))
        g.rect(12 + i * 24, 110, 14, 1, H("FFFFFF"))
    tree(g, 90, 124, 4, H("3F9B4A"))
    return [finish(b, g)]


def cityhall():
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("C9C3B4"))
    lawn(g, 0, 110, 96, 18, base=H("78BE55"), seed=111)
    # plaza with fountain
    g.circle(48, 116, 9, H("8FB8D8"))
    g.circle(48, 116, 5, H("C9E4F5"))
    g.rect(40, 100, 16, 4, H("D8CDB0"))
    shadow(g, 8, 98, 80, 34, 28)
    rx, ry, rw, rd = box3d(b, 8, 98, 80, 34, 24, H("E9E2D2"), H("F7F2E6"))
    b.rect(8, ry + rd, 80, 2, H("B8AD96"))
    windows(b, 11, ry + rd + 4, 74, 10, 14, 2, lit=0.4, seed=112)
    # columns and pediment
    b.rect(32, 82, 32, 3, H("F7F2E6"))
    for i in range(7):
        b.rect(33 + i * 5, 85, 3, 13, H("F7F2E6"))
        b.rect(33 + i * 5 + 2, 85, 1, 13, H("C9C0AC"))
    b.polygon([(30, 82), (48, 72), (66, 82)], H("E6DCC6"))
    # clock tower
    shadow(g, 40, 64, 16, 14, 40, alpha=60)
    box3d(b, 40, 64, 16, 14, 36, H("B8453A"), H("F7F2E6"))
    b.circle(48, 38, 4, H("FFFFFF"))
    b.set(48, 37, H("333333"))
    b.vline(48, 36, 38, H("333333"))
    b.hline(48, 50, 38, H("333333"))
    b.polygon([(39, 28), (48, 14), (57, 28)], H("B8453A"))
    b.rect(48, 4, 1, 10, H("C9CED6"))
    b.rect(49, 4, 6, 4, H("3C78F0"))
    b.rect(52, 5, 1, 2, H("F4F6F8"))
    for (cx, cy) in ((4, 56), (91, 58), (8, 124), (88, 124)):
        tree(b, cx, cy, 4, H("3F9B4A"))
    flowerbed(g, 18, 106, 8, 7)
    flowerbed(g, 70, 106, 8, 8)
    return [finish(b, g)]


def build():
    return {
        "policebox": (policebox(), None, 1),
        "police-hq": (police_hq(), None, 3),
        "prison": (prison(), None, 3),
        "firehouse": (firehouse(), None, 1),
        "sportsfield": (sportsfield(), None, 2),
        "postoffice": (postoffice(), None, 2),
        "sortingcenter": (sortingcenter(), None, 3),
        "telecom-mast": (telecom_mast(), 40, 1),
        "telecom-tower": (telecom_tower(), 40, 2),
        "cityhall": (cityhall(), None, 3),
        "welfare": (welfare(), None, 2),
    }
