"""Services art, part E (wave 2): helicopter pads, firewatch tower and the helicopter sprite."""
import math

from genworld import (H, new_layers, finish, pad, lot, shadow, box3d, windows, door, tree, lawn, fence, shade, mix, Canvas)


def helipad(body, accent, seed):
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("A9ADB2"))
    lawn(g, 0, 84, 64, 12, base=H("78BE55"), seed=seed)
    # landing pad
    g.circle(24, 62, 17, H("4A5360"))
    g.circle(24, 62, 15, H("5B6068"))
    g.rect(18, 54, 2, 16, H("F4F6F8"))
    g.rect(28, 54, 2, 16, H("F4F6F8"))
    g.rect(18, 61, 12, 2, H("F4F6F8"))
    # hangar / station
    shadow(g, 38, 82, 22, 22, 14)
    rx, ry, rw, rd = box3d(b, 38, 82, 22, 22, 14, accent, body)
    b.rect(38, ry + rd, 22, 2, shade(accent, 0.7))
    windows(b, 40, ry + rd + 3, 18, 4, 4, 1, lit=0.5, seed=seed)
    door(b, 46, 76, 6, 6, H("5B3E2E"))
    # windsock
    b.rect(8, 44, 1, 12, H("C9CED6"))
    b.polygon([(9, 44), (16, 46), (9, 48)], H("E8672E"))
    tree(g, 4, 90, 3, H("3F9B4A"))
    return [finish(b, g)]


def firewatch():
    g, b = new_layers(32, 64)
    pad(g, 2, 44, 28, 18, H("B4B6B8"))
    shadow(g, 10, 58, 12, 10, 34, alpha=60)
    for y in range(18, 56):
        half = 3 + (y - 18) // 12
        b.rect(16 - half, y, 2 * half, 1, shade(H("8A5A3C"), 1.1 - 0.3 * (y - 18) / 38))
        if (y - 18) % 6 == 0:
            b.hline(16 - half, 16 + half, y, H("5E3E28"))
    b.rect(8, 12, 16, 8, H("C23B3B"))
    b.rect(8, 12, 16, 2, H("E26B52"))
    b.rect(10, 15, 12, 3, (170, 215, 240, 230))
    b.polygon([(6, 12), (16, 4), (26, 12)], H("7A3026"))
    b.rect(16, 0, 1, 5, H("C9CED6"))
    b.rect(17, 0, 4, 2, H("E8C547"))
    tree(g, 4, 56, 3, H("3F9B4A"))
    return [finish(b, g)]


def heli_frames():
    """Two 32x32 frames of a small helicopter seen from above (rotor in two positions)."""
    out = []
    for t in range(2):
        c = Canvas(32, 32)
        c.ellipse(18, 27, 8, 3, (10, 18, 30, 55))
        c.ellipse(15, 14, 5, 7, H("E8E8EE"))
        c.ellipse(15, 15, 4, 5, H("D93636"))
        c.rect(14, 20, 3, 8, H("C9CED6"))
        c.rect(12, 27, 7, 1, H("8D95A0"))
        c.circle(15, 11, 2, H("5A7E9E"))
        a0 = math.radians(t * 45)
        for k in range(2):
            a = a0 + k * math.pi / 2
            dx, dy = math.cos(a) * 13, math.sin(a) * 13
            c.line(15 - dx, 14 - dy, 15 + dx, 14 + dy, (60, 60, 70, 200), 1)
        c.circle(15, 14, 1.5, H("2A2A2A"))
        c.outline(H("1B2230"))
        out.append(c)
    return out


def build():
    return {
        "medevac-helipad": (helipad(H("F4F6F8"), H("D93636"), 301), None, 2),
        "fire-helipad": (helipad(H("EBC8C0"), H("C23B3B"), 302), None, 2),
        "firewatch": (firewatch(), None, 1),
    }
