#!/usr/bin/env python3
"""OpenCity service and utility vehicle art (WP TRF). Imports helpers from genworld.py, writes bits/world/traffic/*.png.

Same top-down style as the cars: 32 facings on a 32x32 grid, frames clockwise from north (Facings: -32 in
sequences/vehicles.yaml). Vehicles: van, garbage, ambulance, firetruck, policecar, hearse.

Run: python3 mods/city/tools/gen_traffic.py
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import genworld as gw  # noqa: E402
from genworld import H, SSCanvas, downsample  # noqa: E402
from pngkit import Canvas, save_sheet, shade  # noqa: E402

OUT = os.path.join(gw.WORLD, "traffic")
GLASS = H("27384C")
TYRE = H("1A1D22")
HEAD = H("FFF1B8")
TAIL = H("D83A3A")
WHITE = H("F1F3F5")
RED = H("D2392F")
BLUE = H("2E6FE0")


def wheels(s, x, y, W, ys, h=3.2):
    for wy in ys:
        s.rect(x - 0.6, y + wy, 1.4, h, TYRE)
        s.rect(x + W - 0.8, y + wy, 1.4, h, TYRE)


def lights(s, x, y, W, L):
    s.rect(x + 1.2, y + 0.4, 1.8, 0.8, HEAD)
    s.rect(x + W - 3.0, y + 0.4, 1.8, 0.8, HEAD)
    s.rect(x + 1.2, y + L - 1.0, 1.8, 0.8, TAIL)
    s.rect(x + W - 3.0, y + L - 1.0, 1.8, 0.8, TAIL)


def body(s, x, y, W, L, col, r=2.0):
    s.rrect(x, y, W, L, r, shade(col, 0.72))
    s.rrect(x + 0.3, y + 0.2, W - 1.2, L - 0.8, r - 0.2, col)


def cab(s, x, y, W, col, glass_y=2.6):
    body(s, x, y, W, 8, col, 1.8)
    s.rrect(x + 1.2, y + glass_y, W - 2.4, 2.4, 0.8, GLASS)
    s.rect(x + 1.3, y + glass_y + 0.1, 3.0, 0.6, (255, 255, 255, 90))


def lightbar(s, x, y, W, ys, a=RED, b=BLUE):
    s.rect(x + 1.2, y + ys, (W - 2.4) / 2, 1.6, a)
    s.rect(x + W / 2, y + ys, (W - 2.4) / 2, 1.6, b)
    s.rect(x + 1.2, y + ys, W - 2.4, 0.4, (255, 255, 255, 140))


def car_like(col, L=18, W=10, roof=None):
    s = SSCanvas()
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, L - 6), 3)
    body(s, x, y, W, L, col, 2.2)
    s.rrect(x + 1.2, y + 4.2, W - 2.4, 3.0, 0.9, GLASS)
    s.rrect(x + 1.2, y + L - 5.0, W - 2.4, 2.0, 0.8, GLASS)
    s.rrect(x + 1.0, y + 7.4, W - 2.0, L - 14.0 + 0.4, 1.0, roof or shade(col, 1.18))
    lights(s, x, y, W, L)
    return s, x, y, W, L


def van():
    s = SSCanvas()
    W, L = 10, 20
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, L - 6))
    body(s, x, y, W, L, H("E4E7EB"), 2.0)
    s.rrect(x + 1.2, y + 2.4, W - 2.4, 2.6, 0.9, GLASS)
    s.rect(x + 1.0, y + 6.0, W - 2.0, L - 8.0, WHITE)
    s.rect(x + 1.0, y + 9.0, W - 2.0, 1.4, H("3A8DDE"))
    lights(s, x, y, W, L)
    return s.c


def garbage():
    s = SSCanvas()
    W, L = 10, 25
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, 15, 20))
    cab(s, x, y, W, H("3F8F4F"))
    s.rect(x + 1, y + 8, W - 2, 1, H("3A3F48"))
    s.rrect(x - 0.2, y + 9, W + 0.4, L - 9, 1.2, H("2E7A44"))      # hopper body
    s.rect(x + 0.6, y + 9.6, W - 1.2, 0.8, H("5BB36E"))
    s.rect(x + 1.0, y + L - 5.5, W - 2.0, 4.2, H("1F5A31"))          # loading hatch
    s.rect(x + 1.0, y + L - 5.5, W - 2.0, 0.6, H("F2C230"))
    for i in range(3):
        s.rect(x + 0.4, y + 11 + i * 2.6, W - 0.8, 0.4, H("256B3B"))
    s.rect(x + 1.0, y + L - 1.0, 2, 0.8, TAIL)
    s.rect(x + W - 3.0, y + L - 1.0, 2, 0.8, TAIL)
    return s.c


def ambulance():
    s = SSCanvas()
    W, L = 10, 21
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, L - 6))
    body(s, x, y, W, L, WHITE, 2.0)
    s.rrect(x + 1.2, y + 2.4, W - 2.4, 2.6, 0.9, GLASS)
    s.rect(x + 0.4, y + 8.2, W - 0.8, 1.2, RED)
    s.rect(x + 0.4, y + L - 5.0, W - 0.8, 1.2, RED)
    cx, cy = x + W / 2, y + 12.6                                    # red cross on the roof
    s.rect(cx - 0.9, cy - 2.6, 1.8, 5.2, RED)
    s.rect(cx - 2.6, cy - 0.9, 5.2, 1.8, RED)
    lightbar(s, x, y, W, 5.4)
    lights(s, x, y, W, L)
    return s.c


def firetruck():
    s = SSCanvas()
    W, L = 10, 28
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, 16, 21))
    cab(s, x, y, W, H("D2392F"))
    lightbar(s, x, y, W, 5.2, RED, WHITE)
    s.rect(x + 1, y + 8, W - 2, 1, H("3A3F48"))
    s.rrect(x - 0.2, y + 9, W + 0.4, L - 9, 1.2, H("C0302A"))
    s.rect(x + 0.4, y + 9.4, W - 0.8, 0.6, H("F26B5F"))
    s.rect(x + 3.4, y + 10.5, 3.2, L - 12.5, H("D6DBE0"))            # ladder
    for i in range(0, L - 13, 2):
        s.rect(x + 3.4, y + 11 + i, 3.2, 0.5, H("8E96A0"))
    s.rect(x + 0.4, y + 14, 1.6, 5, H("F2C230"))
    s.rect(x + W - 2.0, y + 14, 1.6, 5, H("F2C230"))
    s.rect(x + 1.0, y + L - 1.0, 2, 0.8, TAIL)
    s.rect(x + W - 3.0, y + L - 1.0, 2, 0.8, TAIL)
    return s.c


def policecar():
    s, x, y, W, L = car_like(H("262B33"), 18, 10, roof=H("EEF1F4"))
    s.rect(x + 0.3, y + 7.6, W - 0.6, 0.9, H("EEF1F4"))               # white doors
    s.rect(x + 0.3, y + 11.6, W - 0.6, 0.9, H("EEF1F4"))
    lightbar(s, x, y, W, 8.6)
    return s.c


def hearse():
    s = SSCanvas()
    W, L = 10, 22
    x, y = 16 - W / 2, 16 - L / 2
    wheels(s, x, y, W, (3, L - 7))
    body(s, x, y, W, L, H("2A2D33"), 2.2)
    s.rrect(x + 1.2, y + 4.2, W - 2.4, 3.0, 0.9, GLASS)
    s.rrect(x + 1.0, y + 7.6, W - 2.0, L - 10.0, 1.0, H("1B1D22"))    # rear compartment glass roof
    s.rect(x + 1.8, y + 9.0, W - 3.6, L - 13.0, H("5A4A6A"))          # flowers
    s.rect(x + 3.6, y + 11.0, 0.8, 0.8, H("E8C2D0"))
    s.rect(x + 5.4, y + 13.0, 0.8, 0.8, H("F2E6A0"))
    lights(s, x, y, W, L)
    return s.c


def sheet(base):
    frames = []
    for i in range(32):
        f = downsample(base.rotate(i * 360.0 / 32), gw.SS)
        sh = Canvas(32, 32)
        for yy in range(32):
            for xx in range(32):
                a = f.px[yy * 32 + xx][3]
                if a:
                    sh.blend(xx + 1, yy + 1, (10, 18, 30, int(a * 0.28)))
        sh.blit(f, 0, 0)
        frames.append(sh)
    return frames


SHIRTS = [H("D8473A"), H("3B73D1"), H("EDB92E"), H("7BC96F")]


def pedestrian(shirt, step):
    """16x16 top-down walker (8 frames: 4 shirt colours x 2 steps)."""
    c = Canvas(16, 16)
    c.rect(5, 6, 6, 5, (10, 18, 30, 70))                  # shadow
    off = 1 if step else 0
    c.rect(6, 11 + off, 2, 3, H("2F3B4F"))                # legs
    c.rect(8, 12 - off, 2, 3, H("2F3B4F"))
    c.rect(4, 6, 8, 5, shade(shirt, 0.72))                # shoulders
    c.rect(5, 6, 6, 4, shirt)
    c.rect(4, 7 + off, 1, 3, H("E2B48C"))                 # arms
    c.rect(11, 8 - off, 1, 3, H("E2B48C"))
    c.rect(6, 3, 4, 4, H("E2B48C"))                       # head
    c.rect(6, 3, 4, 2, H("4A3320"))                       # hair
    return c


def pedestrians():
    frames = []
    for colour in SHIRTS:
        for step in (0, 1):
            frames.append(pedestrian(colour, step))

    save_sheet(os.path.join(OUT, "pedestrian.png"), frames, cols=8)


VEHICLES = {"van": van, "garbage": garbage, "ambulance": ambulance, "firetruck": firetruck,
            "policecar": policecar, "hearse": hearse}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in VEHICLES.items():
        save_sheet(os.path.join(OUT, f"{name}.png"), sheet(fn()), cols=8)
        print("wrote", name)

    pedestrians()
    print("wrote pedestrian")


if __name__ == "__main__":
    main()
