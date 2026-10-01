#!/usr/bin/env python3
"""OpenCity public transport art (WP PT). Imports helpers from genworld.py, writes bits/world/pt/ and sequences/transit.yaml.

Run: python3 mods/city/tools/gen_pt.py [--sheet out.png]
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import genworld as gw  # noqa: E402
from genworld import H, box3d, shadow, windows, pad, lot, new_layers, finish, tree  # noqa: E402
from pngkit import Canvas, save_sheet, shade, with_alpha  # noqa: E402
import gen_pt_rail as rail  # noqa: E402

PT = os.path.join(gw.WORLD, "pt")
BUS_BLUE = H("2FA0B8")
TAXI_YELLOW = H("EDB92E")
SIGN_BLUE = H("2C6FD1")


# --------------------------------------------------------------------------- stops (32x32 overlays on a road cell)
def stop_frame(side, taxi):
    """Pavement bay, shelter and sign pole on one side (0 N, 1 E, 2 S, 3 W) of a road cell.

    The frame is 32x48: the road cell is the bottom 32 rows (OY = 16), the sign pole may rise above it.
    """
    OY = 16
    c = Canvas(32, 48)
    accent = TAXI_YELLOW if taxi else SIGN_BLUE
    line = with_alpha(H("F2D04C"), 230)
    bays = {0: (6, 0, 20, 8), 1: (24, 6, 8, 20), 2: (6, 24, 20, 8), 3: (0, 6, 8, 20)}
    bx, by, bw, bh = bays[side]
    by += OY
    c.rect(bx, by, bw, bh, H("C9D1D6"))
    c.rect_outline(bx, by, bw, bh, shade(H("C9D1D6"), 0.78))
    if side == 0:
        c.hline(bx, bx + bw - 1, by + bh, line)
    elif side == 2:
        c.hline(bx, bx + bw - 1, by - 1, line)
    elif side == 1:
        c.vline(bx - 1, by, by + bh - 1, line)
    else:
        c.vline(bx + bw, by, by + bh - 1, line)
    if taxi:  # checker band on the lane-side edge marks a stand
        for i in range(0, 20, 4):
            if side in (0, 2):
                yy = by + (bh - 2 if side == 0 else 0)
                c.rect(bx + i, yy, 2, 2, H("1B2230"))
                c.rect(bx + i + 2, yy, 2, 2, H("F4F4F4"))
            else:
                xx = bx + (bw - 2 if side == 1 else 0)
                xx = bx + (0 if side == 1 else bw - 2)
                c.rect(xx, by + i, 2, 2, H("1B2230"))
                c.rect(xx, by + i + 2, 2, 2, H("F4F4F4"))
    # shelter: a small canopy on the pavement
    if side in (0, 2):
        sx, sw, sd = bx + 2, 11, 3
        yb = by + bh - 1
    else:
        sx, sw, sd = bx + 1, 4, 8
        yb = by + 15
    shadow(c, sx, yb, sw, sd, 6, alpha=60)
    box3d(c, sx, yb, sw, sd, 6, accent, H("DCE4EA"))
    # pole and sign disc at the downstream end
    if side == 0:
        px, py = bx + bw - 4, by + bh - 1
    elif side == 2:
        px, py = bx + 3, by + bh - 1
    elif side == 1:
        px, py = bx + 5, by + 3
    else:
        px, py = bx + 2, by + bh - 2
    c.rect(px, py - 11, 1, 12, H("6A737C"))
    c.circle(px, py - 13, 4, H("1B2230"))
    c.circle(px, py - 13, 3, H("F4F6F8"))
    c.circle(px, py - 13, 2, accent)
    if taxi:
        c.rect(px - 1, py - 14, 3, 1, H("1B2230"))
        c.rect(px - 1, py - 12, 3, 1, H("F4F4F4"))
    else:
        c.rect(px - 1, py - 14, 3, 2, H("F4F6F8"))
        c.set(px - 1, py - 12, H("F4F6F8"))
        c.set(px + 1, py - 12, H("F4F6F8"))
    return c


def stops():
    frames = [stop_frame(s, False) for s in range(4)] + [stop_frame(s, True) for s in range(4)]
    save_sheet(os.path.join(PT, "stops.png"), frames, cols=8)


# --------------------------------------------------------------------------- depots
def parked_bus(g, x, y, col):
    """Top-down parked bus (vertical, 6x14)."""
    g.rect(x, y, 6, 14, shade(col, 0.72))
    g.rect(x + 1, y, 4, 13, col)
    g.rect(x + 1, y + 1, 4, 2, H("27384C"))
    g.rect(x + 1, y + 4, 4, 8, shade(col, 1.2))
    for i in range(2):
        g.rect(x + 2, y + 5 + i * 4, 2, 2, H("E8EEF2"))
    g.rect(x, y + 13, 6, 1, (10, 18, 30, 70))


def busdepot():
    """3x2 footprint: 96x96 frame, footprint = bottom 96x64."""
    g, b = new_layers(96, 96)
    pad(g, 0, 32, 96, 64, H("A9ADB2"))
    lot(g, 0, 70, 96, 26, H("5B6068"))
    for i in range(6):
        g.vline(8 + i * 14, 72, 94, (235, 235, 235, 130))
    for i, x in enumerate((10, 24, 52, 80)):
        parked_bus(g, x, 76, BUS_BLUE if i != 2 else H("E0534A"))
    shadow(g, 6, 68, 84, 28, 22)
    rx, ry, rw, rd = box3d(b, 6, 68, 84, 28, 20, H("2A7F92"), H("DDE6EA"))
    b.rect(6, ry + rd, 84, 3, BUS_BLUE)               # coloured fascia
    for i in range(4):                                  # rolling garage doors
        gx = 10 + i * 20
        b.rect(gx, 57, 16, 11, H("C4CCD2"))
        for k in range(57, 68, 2):
            b.hline(gx, gx + 15, k, H("98A2AC"))
        b.rect(gx, 57, 16, 1, H("EEF1F4"))
        b.rect_outline(gx - 1, 56, 18, 12, shade(BUS_BLUE, 0.7))
    # roof: vents, solar panels and the "BUS" board
    for i in range(3):
        b.rect(rx + 8 + i * 22, ry + 6, 12, 7, H("3E4C5C"))
        b.rect(rx + 8 + i * 22, ry + 6, 12, 1, H("6A7C90"))
    b.rect(rx + 34, ry + 16, 16, 7, H("F4F6F8"))
    b.rect_outline(rx + 34, ry + 16, 16, 7, SIGN_BLUE)
    b.rect(rx + 37, ry + 18, 10, 3, SIGN_BLUE)
    windows(b, 12, ry + rd + 5, 70, 4, 10, 1, lit=0.5, seed=21)
    tree(g, 91, 46, 4, H("3F9B4A"))
    tree(g, 4, 44, 3, H("3F9B4A"))
    return finish(b, g)


def taxidepot():
    """2x2 footprint: 64x96 frame."""
    g, b = new_layers(64, 96)
    pad(g, 0, 32, 64, 64, H("B1B4B8"))
    lot(g, 0, 72, 64, 24, H("5B6068"))
    for x in (8, 18, 38, 48):
        g.rect(x, 78, 6, 11, shade(TAXI_YELLOW, 0.75))
        g.rect(x + 1, 78, 4, 10, TAXI_YELLOW)
        g.rect(x + 1, 80, 4, 2, H("27384C"))
        g.rect(x + 2, 83, 2, 1, H("1B2230"))
    shadow(g, 6, 70, 52, 26, 18)
    rx, ry, rw, rd = box3d(b, 6, 70, 52, 24, 16, H("F2D04C"), H("F4EDD0"))
    for i in range(0, 52, 6):                           # checker band
        b.rect(6 + i, ry + rd, 3, 2, H("1B2230"))
        b.rect(9 + i, ry + rd, 3, 2, H("F4F4F4"))
        b.rect(6 + i, ry + rd + 2, 3, 2, H("F4F4F4"))
        b.rect(9 + i, ry + rd + 2, 3, 2, H("1B2230"))
    for i in range(2):
        gx = 10 + i * 24
        b.rect(gx, 60, 20, 10, H("C4CCD2"))
        for k in range(60, 70, 2):
            b.hline(gx, gx + 19, k, H("98A2AC"))
        b.rect_outline(gx - 1, 59, 22, 11, H("B8901C"))
    b.rect(rx + 16, ry + 3, 20, 8, H("1B2230"))          # roof TAXI sign
    b.rect_outline(rx + 16, ry + 3, 20, 8, H("F2D04C"))
    for i in range(4):
        b.rect(rx + 19 + i * 4, ry + 5, 2, 4, H("F2D04C"))
    tree(g, 59, 44, 3, H("3F9B4A"))
    return finish(b, g)



def metrostation():
    """1x2 footprint: 32x96 frame (footprint = bottom 32x64): stair-well, glass kiosk, and a roundel sign."""
    g, b = new_layers(32, 96)
    pad(g, 0, 32, 32, 64, H("B9BDC2"), edge=H("8E949B"))
    for i in range(4):                                  # paving joints
        g.hline(1, 30, 40 + i * 14, with_alpha(H("8E949B"), 90))
    # stair-well down (upper cell)
    g.rect(6, 44, 20, 14, H("2A2F38"))
    for i in range(5):
        g.rect(7, 46 + i * 2, 18, 1, shade(H("59616C"), 1.0 - i * 0.12))
    g.rect_outline(5, 43, 22, 16, H("E8ECF0"))
    g.rect(5, 43, 22, 1, H("FFFFFF"))
    # glass kiosk (lower cell)
    shadow(g, 4, 90, 24, 16, 14)
    rx, ry, rw, rd = box3d(b, 4, 90, 24, 16, 14, H("2C6FD1"), H("BFD9EE"))
    b.rect(4, ry + rd, 24, 2, H("1E4FA0"))
    for i in range(3):
        b.rect(7 + i * 7, ry + rd + 3, 5, 8, H("7FB6DE"))
        b.rect(7 + i * 7, ry + rd + 3, 5, 1, H("DDEEFA"))
    b.rect(rx + 4, ry + 3, 16, 10, shade(H("2C6FD1"), 1.15))
    # roundel sign on a pole
    b.rect(26, 52, 1, 24, H("6A737C"))
    b.circle(26, 48, 6, H("1B2230"))
    b.circle(26, 48, 5, H("E0534A"))
    b.rect(21, 47, 11, 3, H("F4F6F8"))
    b.rect(24, 44, 5, 2, H("F4F6F8"))
    b.set(24, 50, H("1B2230"))
    return finish(b, g)

# --------------------------------------------------------------------------- taxi vehicle
def taxi_sheet():
    base = gw.vehicle_base("car", TAXI_YELLOW)
    s = gw.SS

    def r(x, y, w, h, col):
        base.rect(round(x * s), round(y * s), round(w * s), round(h * s), col)

    r(10.6, 12.2, 1.0, 5.5, H("1B2230"))                 # side checker stripes
    r(20.4, 12.2, 1.0, 5.5, H("1B2230"))
    r(13.3, 13.2, 5.4, 1.6, H("1B2230"))                 # roof sign
    r(13.7, 13.5, 4.6, 0.9, H("FFF1B8"))
    frames = []
    for i in range(32):
        f = gw.downsample(base.rotate(i * 360.0 / 32), s)
        sh = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                a = f.px[y * 32 + x][3]
                if a:
                    sh.blend(x + 1, y + 1, (10, 18, 30, int(a * 0.28)))
        sh.blit(f, 0, 0)
        frames.append(sh)
    save_sheet(os.path.join(PT, "taxi.png"), frames, cols=8)


# --------------------------------------------------------------------------- sequences
def write_sequences():
    t = "# Public transport sprites. Generated by tools/gen_pt.py (frames: stops 0-3 bus N,E,S,W; 4-7 taxi stand).\n"
    t += "transit-stop:\n\tidle:\n\t\tFilename: pt/stops.png\n\t\tLength: *\n\t\tOffset: 0,-8\n\n"
    for name in ("busdepot", "taxidepot", "metrostation"):
        t += f"{name}:\n\tidle:\n\t\tFilename: pt/{name}.png\n\t\tLength: *\n\t\tOffset: 0,-16\n\n"
    t += "taxi:\n\tidle:\n\t\tFilename: pt/taxi.png\n\t\tFacings: -32\n\t\tLength: 1\n"
    t += rail.sequences_text()
    with open(os.path.join(gw.MOD, "sequences", "transit.yaml"), "w") as f:
        f.write(t)


def contact_sheet(path):
    """Zoomed preview (3x) of the generated art on a grass background, for visual checks."""
    sheet = Canvas(96 * 3 + 64 * 3 + 32 * 3 + 40, 96 * 3 + 40 + 48 * 3 * 2 + 12, H("6E8C5A"))
    x = 8
    for p in (busdepot(), taxidepot(), metrostation()):
        sheet.blit(p.scale(3), x, 8)
        x += p.w * 3 + 8
    for i in range(8):
        gx, gy = 8 + (i % 4) * (32 * 3 + 6), 96 * 3 + 24 + (i // 4) * (48 * 3 + 6)
        bg = Canvas(32, 48, H("3A3F48"))
        bg.rect(0, 16, 32, 32, H("2E333B"))
        bg.blit(stop_frame(i % 4, i >= 4), 0, 0)
        sheet.blit(bg.scale(3), gx, gy)
    sheet.save(path)


def main():
    os.makedirs(PT, exist_ok=True)
    stops()
    save_sheet(os.path.join(PT, "busdepot.png"), [busdepot()])
    save_sheet(os.path.join(PT, "taxidepot.png"), [taxidepot()])
    save_sheet(os.path.join(PT, "metrostation.png"), [metrostation()])
    taxi_sheet()
    rail.tracks(PT)
    rail.vehicles(PT)
    rail.buildings(PT)
    write_sequences()
    if "--sheet2" in sys.argv:
        rail.contact_sheet(sys.argv[sys.argv.index("--sheet2") + 1])
    if "--sheet" in sys.argv:
        contact_sheet(sys.argv[sys.argv.index("--sheet") + 1])
    print("gen_pt: done")


if __name__ == "__main__":
    main()
