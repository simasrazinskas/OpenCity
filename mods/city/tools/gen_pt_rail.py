"""Wave 2 art of the transit WP: tram track and tram, rail and trains, train station, tram depot, cargo terminals.

Imported by gen_pt.py (which owns the output folder and the sequences file).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import genworld as gw  # noqa: E402
from genworld import H, box3d, shadow, windows, pad, lot, new_layers, finish, tree  # noqa: E402
from pngkit import Canvas, save_sheet, shade, with_alpha  # noqa: E402

STEEL = H("9AA3AD")
SLEEPER = H("6B4E36")
BALLAST = H("A29A8C")
TRAM_RED = H("D9503F")
TRAIN_BLUE = H("3C6FD1")


# --------------------------------------------------------------------------- tracks (32x32 ground overlays)
def _strip(c, horizontal, cx0, cx1, kind):
    """One straight piece from cx0 to cx1 (cell coordinates 0..31) along an axis through the middle."""
    if kind == "tram":
        for off in (-3, 3):
            if horizontal:
                c.hline(cx0, cx1, 16 + off, STEEL)
                c.hline(cx0, cx1, 17 + off, shade(STEEL, 0.7))
            else:
                c.vline(16 + off, cx0, cx1, STEEL)
                c.vline(17 + off, cx0, cx1, shade(STEEL, 0.7))
        return
    # rail: ballast bed, sleepers, two rails
    if horizontal:
        c.rect(cx0, 9, cx1 - cx0 + 1, 14, with_alpha(BALLAST, 255))
        for x in range(cx0 + 1, cx1, 4):
            c.rect(x, 10, 2, 12, SLEEPER)
        for y in (12, 19):
            c.hline(cx0, cx1, y, STEEL)
            c.hline(cx0, cx1, y + 1, shade(STEEL, 0.65))
    else:
        c.rect(9, cx0, 14, cx1 - cx0 + 1, with_alpha(BALLAST, 255))
        for y in range(cx0 + 1, cx1, 4):
            c.rect(10, y, 12, 2, SLEEPER)
        for x in (12, 19):
            c.vline(x, cx0, cx1, STEEL)
            c.vline(x + 1, cx0, cx1, shade(STEEL, 0.65))


def track_frame(mask, kind):
    """16 frames by neighbour mask (bit0 N, bit1 E, bit2 S, bit3 W)."""
    c = Canvas(32, 32)
    n, e, s, w = mask & 1, mask >> 1 & 1, mask >> 2 & 1, mask >> 3 & 1
    if mask == 0:
        w = e = 1
    if kind == "rail":  # beds first so rails and sleepers of other arms stay on top
        if w or e:
            _strip(c, True, 0 if w else 9, 31 if e else 22, kind)
        if n or s:
            _strip(c, False, 0 if n else 9, 31 if s else 22, kind)
    else:
        if w or e:
            _strip(c, True, 0 if w else 13, 31 if e else 18, kind)
        if n or s:
            _strip(c, False, 0 if n else 13, 31 if s else 18, kind)
    return c


def crossing_frame(horizontal):
    """Level crossing: the rail over the road, with a striped warning frame on both sides."""
    c = Canvas(32, 32)
    if horizontal:
        c.rect(0, 10, 32, 12, with_alpha(BALLAST, 255))
        for x in range(1, 31, 4):
            c.rect(x, 11, 2, 10, SLEEPER)
        for y in (13, 18):
            c.hline(0, 31, y, STEEL)
            c.hline(0, 31, y + 1, shade(STEEL, 0.65))
        for x in range(10, 22, 4):  # warning stripes on the road either side
            c.rect(x, 2, 2, 5, H("F2D04C"))
            c.rect(x, 25, 2, 5, H("F2D04C"))
    else:
        c.rect(10, 0, 12, 32, with_alpha(BALLAST, 255))
        for y in range(1, 31, 4):
            c.rect(11, y, 10, 2, SLEEPER)
        for x in (13, 18):
            c.vline(x, 0, 31, STEEL)
            c.vline(x + 1, 0, 31, shade(STEEL, 0.65))
        for y in range(10, 22, 4):
            c.rect(2, y, 5, 2, H("F2D04C"))
            c.rect(25, y, 5, 2, H("F2D04C"))
    return c


def tracks(pt):
    save_sheet(os.path.join(pt, "tram-track.png"), [track_frame(m, "tram") for m in range(16)], cols=4)
    frames = [track_frame(m, "rail") for m in range(16)] + [crossing_frame(True), crossing_frame(False)]
    save_sheet(os.path.join(pt, "rail.png"), frames, cols=6)


# --------------------------------------------------------------------------- vehicles (32 facings)
def _vehicle_sheet(base):
    frames = []
    for i in range(32):
        f = gw.downsample(base.rotate(i * 360.0 / 32), gw.SS)
        sh = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                a = f.px[y * 32 + x][3]
                if a:
                    sh.blend(x + 1, y + 1, (10, 18, 30, int(a * 0.28)))
        sh.blit(f, 0, 0)
        frames.append(sh)
    return frames


def tram_base():
    s = gw.SSCanvas()
    W, L = 9, 29
    x, y = 16 - W / 2, 16 - L / 2
    s.rrect(x, y, W, L, 1.6, shade(TRAM_RED, 0.7))
    s.rrect(x + 0.3, y + 0.2, W - 1.0, L - 0.8, 1.5, TRAM_RED)
    s.rect(x + 1, y + 1.5, W - 2, 2.2, H("27384C"))                 # cab window
    s.rect(x + 1, y + L - 3.6, W - 2, 2.2, H("27384C"))
    s.rect(x, y + 12, W, 1.0, H("2B2F36"))                          # articulation joint
    s.rect(x + 1.2, y + 4.5, W - 2.4, 7, shade(TRAM_RED, 1.18))      # roof
    s.rect(x + 1.2, y + 14.5, W - 2.4, 7, shade(TRAM_RED, 1.18))
    s.rect(x + 3.5, y + 6, 2, 4, H("E8EEF2"))                       # roof gear
    s.rect(x + 3.5, y + 16, 2, 4, H("E8EEF2"))
    s.rect(x + 3.4, y + 0.2, 2.2, 0.8, H("FFF1B8"))
    s.rect(x, y + 3, W, 0.5, (255, 255, 255, 70))
    return s.c


def train_base(engine):
    s = gw.SSCanvas()
    W, L = 9, 31
    x, y = 16 - W / 2, 16 - L / 2
    col = TRAIN_BLUE if engine else H("DDE3EA")
    s.rrect(x, y, W, L, 1.2, shade(col, 0.68))
    s.rrect(x + 0.3, y + 0.2, W - 1.0, L - 0.8, 1.1, col)
    if engine:
        s.rect(x + 1, y + 1.5, W - 2, 3, H("27384C"))
        s.rect(x + 1, y + 6, W - 2, L - 8, shade(col, 1.2))
        s.rect(x + 3.4, y + 9, 2.2, 3, H("E8EEF2"))
        s.rect(x + 1, y + L - 4, W - 2, 1.0, H("F2D04C"))
        s.rect(x + 1.2, y + 0.3, 1.8, 0.8, H("FFF1B8"))
        s.rect(x + W - 3, y + 0.3, 1.8, 0.8, H("FFF1B8"))
    else:
        s.rect(x + 1, y + 1.2, W - 2, L - 2.4, shade(col, 1.08))
        for i in range(4):
            s.rect(x + 1.6, y + 3 + i * 6.8, W - 3.2, 3, H("7FB6DE"))
        s.rect(x, y + L - 1.2, W, 1.0, H("2B2F36"))
        s.rect(x, y + 0.2, W, 1.0, H("2B2F36"))
    return s.c


def vehicles(pt):
    save_sheet(os.path.join(pt, "tram.png"), _vehicle_sheet(tram_base()), cols=8)
    save_sheet(os.path.join(pt, "train.png"), _vehicle_sheet(train_base(True)), cols=8)
    save_sheet(os.path.join(pt, "traincar.png"), _vehicle_sheet(train_base(False)), cols=8)


# --------------------------------------------------------------------------- buildings
def tramdepot():
    """3x2: 96x96, footprint = bottom 96x64."""
    g, b = new_layers(96, 96)
    pad(g, 0, 32, 96, 64, H("A9ADB2"))
    lot(g, 0, 72, 96, 24, H("5B6068"))
    for x in (12, 44, 70):
        g.rect(x, 78, 22, 6, shade(TRAM_RED, 0.75))
        g.rect(x + 1, 78, 20, 5, TRAM_RED)
        g.rect(x + 2, 79, 3, 3, H("27384C"))
        g.rect(x + 17, 79, 3, 3, H("27384C"))
    for x in range(4, 92, 4):
        g.hline(x, x + 1, 88, STEEL)
    shadow(g, 6, 70, 84, 28, 20)
    rx, ry, rw, rd = box3d(b, 6, 70, 84, 26, 18, H("B83A2E"), H("E8E0D8"))
    b.rect(6, ry + rd, 84, 3, TRAM_RED)
    for i in range(3):
        gx = 10 + i * 27
        b.rect(gx, 60, 22, 10, H("C4CCD2"))
        for k in range(60, 70, 2):
            b.hline(gx, gx + 21, k, H("98A2AC"))
        b.rect_outline(gx - 1, 59, 24, 11, shade(TRAM_RED, 0.7))
    for i in range(3):
        b.rect(rx + 8 + i * 24, ry + 5, 14, 6, H("3E4C5C"))
    b.rect(rx + 33, ry + 14, 18, 6, H("F4F6F8"))
    b.rect(rx + 36, ry + 16, 12, 2, TRAM_RED)
    tree(g, 91, 46, 4, H("3F9B4A"))
    return finish(b, g)


def trainstation():
    """2x4: 64x160, footprint = bottom 64x128 (long platform building along the track side)."""
    g, b = new_layers(64, 160)
    pad(g, 0, 32, 64, 128, H("B9BDC2"), edge=H("8E949B"))
    for i in range(6):
        g.hline(1, 62, 44 + i * 20, with_alpha(H("8E949B"), 90))
    # platform edge on the east side with yellow safety line
    g.rect(52, 32, 12, 128, H("9AA0A8"))
    g.vline(52, 32, 159, H("F2D04C"))
    shadow(g, 6, 148, 40, 100, 20)
    rx, ry, rw, rd = box3d(b, 6, 148, 40, 100, 18, H("7C8896"), H("DDE3EA"))
    b.rect(6, ry + rd, 40, 3, TRAIN_BLUE)
    windows(b, 9, ry + rd + 5, 34, 6, 6, 1, lit=0.5, seed=31, dark_col=H("5E86AE"))
    b.rect(18, 138, 16, 10, H("27384C"))                    # entrance
    b.rect(rx + 8, ry + 8, 24, 10, H("F4F6F8"))             # roof sign
    b.rect_outline(rx + 8, ry + 8, 24, 10, TRAIN_BLUE)
    b.rect(rx + 11, ry + 11, 18, 4, TRAIN_BLUE)
    for i in range(4):                                       # canopy over the platform
        b.rect(48, 52 + i * 26, 14, 18, with_alpha(H("7FB6DE"), 190))
        b.rect_outline(48, 52 + i * 26, 14, 18, H("5E6A78"))
    tree(g, 4, 52, 3, H("3F9B4A"))
    return finish(b, g)


def railyard():
    """Cargo train terminal 3x2: 96x96."""
    g, b = new_layers(96, 96)
    pad(g, 0, 32, 96, 64, H("A5A59E"))
    lot(g, 0, 64, 96, 32, H("6A6A66"))
    for i in range(3):
        g.hline(0, 95, 74 + i * 6, STEEL)
        for x in range(1, 95, 4):
            g.rect(x, 73 + i * 6, 2, 4, SLEEPER)
    cols = ["C0392B", "2E7D6B", "3C78C8", "E0A02E", "8E5BB5"]
    for i in range(7):
        c = H(cols[i % len(cols)])
        g.rect(4 + i * 13, 84, 11, 6, shade(c, 0.78))
        g.rect(4 + i * 13, 84, 11, 5, c)
    shadow(g, 6, 62, 40, 22, 14)
    rx, ry, rw, rd = box3d(b, 6, 62, 40, 22, 14, H("8A9099"), H("D8DCE0"))
    b.rect(6, ry + rd, 40, 2, TRAIN_BLUE)
    b.rect(14, 54, 12, 8, H("5A6A80"))
    # gantry crane
    b.rect(56, 40, 3, 36, H("E0A02E"))
    b.rect(84, 40, 3, 36, H("E0A02E"))
    b.rect(54, 38, 36, 4, H("E0A02E"))
    b.rect(66, 42, 6, 8, H("5A6A80"))
    return finish(b, g)


def cargoharbor():
    """3x3 harbor: 96x128, footprint = bottom 96x96; quay on the bottom edge, ship alongside."""
    g, b = new_layers(96, 128)
    g.rect(0, 32, 96, 96, H("B2B4B4"))
    g.noise(0.04, seed=4, x=0, y=32, w=96, h=96)
    g.rect(0, 100, 96, 28, H("3E7FA8"))                   # water
    for x in range(0, 96, 8):
        g.hline(x, x + 3, 108, with_alpha(H("D8EEF8"), 140))
        g.hline(x + 4, x + 7, 118, with_alpha(H("D8EEF8"), 140))
    g.rect(0, 96, 96, 4, H("7A7E84"))                       # quay edge
    cols = ["C0392B", "2E7D6B", "3C78C8", "E0A02E"]
    for i in range(8):
        c = H(cols[i % 4])
        g.rect(4 + i * 11, 66 + (i % 2) * 10, 10, 8, shade(c, 0.78))
        g.rect(4 + i * 11, 66 + (i % 2) * 10, 10, 7, c)
    # ship
    g.polygon([(8, 106), (80, 106), (88, 114), (80, 122), (8, 122)], H("2B3A4A"))
    g.rect(10, 106, 68, 6, H("C23B3B"))
    for i in range(6):
        c = H(cols[(i + 1) % 4])
        g.rect(14 + i * 11, 108, 9, 8, c)
    shadow(g, 6, 62, 30, 20, 16)
    rx, ry, rw, rd = box3d(b, 6, 62, 30, 20, 16, H("8A9099"), H("D8DCE0"))
    b.rect(6, ry + rd, 30, 2, H("2C6FD1"))
    # quay crane
    b.rect(62, 52, 3, 46, H("E0A02E"))
    b.rect(84, 52, 3, 46, H("E0A02E"))
    b.rect(56, 50, 40, 4, H("E0A02E"))
    b.rect(70, 54, 6, 8, H("5A6A80"))
    return finish(b, g)


def cargoairport():
    """3x3 cargo airport: 96x128; apron, hangar and a freight plane."""
    g, b = new_layers(96, 128)
    pad(g, 0, 32, 96, 96, H("B4B6B8"))
    g.rect(0, 96, 96, 20, H("4A4E56"))                      # taxiway
    for x in range(2, 94, 10):
        g.rect(x, 105, 5, 1, H("F2D04C"))
    # plane (top-down, pointing right)
    g.rect(22, 103, 40, 5, H("EEF1F5"))
    g.rect(40, 94, 6, 24, H("DDE3EA"))
    g.rect(58, 99, 5, 14, H("DDE3EA"))
    g.rect(24, 104, 6, 3, H("2C6FD1"))
    shadow(g, 6, 70, 48, 28, 20)
    rx, ry, rw, rd = box3d(b, 6, 70, 48, 28, 18, H("7C8896"), H("DDE3EA"))
    pitched_x = rx
    for i in range(5):
        b.rect(pitched_x + 3 + i * 9, ry + 3, 5, rd - 6, H("6A7684"))
    b.rect(6, ry + rd, 48, 3, H("2C6FD1"))
    b.rect(14, 60, 32, 10, H("C4CCD2"))
    for k in range(60, 70, 2):
        b.hline(14, 45, k, H("98A2AC"))
    # control tower
    shadow(g, 70, 70, 10, 10, 34, alpha=60)
    box3d(b, 70, 70, 10, 10, 34, H("5A6A80"), H("DDE3EA"))
    b.rect(68, 34, 14, 6, with_alpha(H("7FB6DE"), 230))
    b.rect(74, 24, 2, 10, H("C9CED6"))
    return finish(b, g)


def buildings(pt):
    for name, fn in (("tramdepot", tramdepot), ("trainstation", trainstation), ("railyard", railyard),
                     ("cargoharbor", cargoharbor), ("cargoairport", cargoairport)):
        save_sheet(os.path.join(pt, name + ".png"), [fn()])


def sequences_text():
    t = "\n"
    t += "tram-track:\n\ttrack:\n\t\tFilename: pt/tram-track.png\n\t\tLength: 16\n\n"
    t += "rail:\n\trail:\n\t\tFilename: pt/rail.png\n\t\tLength: 18\n\n"
    for name in ("tram", "train", "traincar"):
        t += f"{name}:\n\tidle:\n\t\tFilename: pt/{name}.png\n\t\tFacings: -32\n\t\tLength: 1\n\n"
    for name in ("tramdepot", "trainstation", "railyard", "cargoharbor", "cargoairport"):
        t += f"{name}:\n\tidle:\n\t\tFilename: pt/{name}.png\n\t\tLength: *\n\t\tOffset: 0,-16\n\n"
    return t.rstrip() + "\n"


def contact_sheet(path):
    sheet = Canvas(96 * 3 * 3 + 64 * 3 + 40, 160 * 3 + 30 + 32 * 3 * 3, H("6E8C5A"))
    x = 8
    for fn in (tramdepot, railyard, cargoharbor, cargoairport):
        p = fn()
        sheet.blit(p.scale(2), x, 8)
        x += p.w * 2 + 6
    p = trainstation()
    sheet.blit(p.scale(2), x, 8)
    y = 160 * 2 + 20
    for i, (kind, fr) in enumerate([("tram", 0), ("tram", 5), ("tram", 10), ("tram", 15), ("rail", 5), ("rail", 15), ("rail", 7), ("rail", 10)]):
        bg = Canvas(32, 32, H("3A3F48"))
        bg.blit(track_frame(fr, kind), 0, 0)
        sheet.blit(bg.scale(3), 8 + i * 100, y)
    for i, h in enumerate((True, False)):
        bg = Canvas(32, 32, H("3A3F48"))
        bg.blit(crossing_frame(h), 0, 0)
        sheet.blit(bg.scale(3), 8 + (8 + i) * 100, y)
    y += 110
    for i, base in enumerate((tram_base(), train_base(True), train_base(False))):
        fr = _vehicle_sheet(base)
        for j, k in enumerate((0, 8, 4)):
            sheet.blit(fr[k].scale(3), 8 + (i * 3 + j) * 100, y)
    sheet.save(path)
