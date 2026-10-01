"""
gen_net_util.py - utility network art for gen_net.py: HV power line and pipe overlays, transformer, battery,
sewage outlet and treatment plant. Imports drawing helpers from genworld.py (not edited).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, shade, mix, with_alpha, hexcolor  # noqa: E402
import genworld as gw  # noqa: E402

H = hexcolor
WIRE = H("2B2F36")
WIRE_L = H("6C7480")


# --------------------------------------------------------------------------- overlays
def powerline_frame(mask):
    c = Canvas(32, 32)
    N, E, S, W = mask & 1, mask & 2, mask & 4, mask & 8
    top = 8
    # wires first (behind the pylon)
    if W:
        c.hline(0, 16, top, WIRE)
        c.hline(0, 16, top + 1, with_alpha(WIRE, 90))
    if E:
        c.hline(16, 31, top, WIRE)
        c.hline(16, 31, top + 1, with_alpha(WIRE, 90))
    if N:
        c.vline(16, 0, top, WIRE)
    if S:
        c.vline(16, top, 31, WIRE)
    # ground shadow of the wires
    if E or W:
        c.hline(0 if W else 16, 31 if E else 16, 24, (10, 18, 30, 40))
    # pylon: A-frame with cross arm
    for (x, y) in ((13, 27), (20, 27)):
        c.ellipse(x, y, 2, 1, (10, 18, 30, 80))
    c.line(12, 26, 15, top, H("8D97A3"))
    c.line(20, 26, 17, top, H("626B77"))
    c.line(13, 22, 19, 17, H("B2BBC6"))
    c.line(19, 22, 13, 17, H("B2BBC6"))
    c.line(13, 16, 19, 12, H("B2BBC6"))
    c.line(19, 16, 13, 12, H("B2BBC6"))
    c.rect(9, top + 1, 15, 2, H("8D97A3"))
    c.rect(9, top + 1, 15, 1, H("C9D2DC"))
    for x in (9, 15, 23):
        c.rect(x, top + 3, 1, 3, H("E8E4D8"))
    c.rect(15, top - 3, 3, 3, H("9AA4B0"))
    return c


def pipe_frame(mask, lane, colour, dashed=True):
    c = Canvas(32, 32)
    N, E, S, W = mask & 1, mask & 2, mask & 4, mask & 8
    dark = shade(colour, 0.65)

    def px(x, y):
        c.set(x, y, colour)
        c.set(x + 1, y, colour)

    def py(x, y):
        c.set(x, y, colour)
        c.set(x, y + 1, colour)

    for i in range(0, 17):
        if dashed and (i % 6) >= 4:
            continue
        if W:
            c.rect(i, lane, 1, 2, colour)
        if E:
            c.rect(31 - i, lane, 1, 2, colour)
        if N:
            c.rect(lane, i, 2, 1, colour)
        if S:
            c.rect(lane, 31 - i, 2, 1, colour)
    c.rect(lane - 2, lane - 2, 6, 6, dark)
    c.rect(lane - 1, lane - 1, 4, 4, colour)
    return c


# --------------------------------------------------------------------------- buildings
def transformer():
    g, b = gw.new_layers(64, 96)
    gw.pad(g, 2, 34, 60, 60, H("9A9E96"))
    gw.fence(g, 3, 35, 58, 58, H("C9CDD2"))
    gw.shadow(g, 8, 70, 22, 12, 16, alpha=60)
    gw.shadow(g, 34, 70, 22, 12, 16, alpha=60)
    for (x, yb) in ((8, 70), (34, 70)):
        gw.box3d(b, x, yb, 22, 12, 14, H("8FA3B4"), H("5C7A94"))
        for i in range(0, 22, 3):
            b.rect(x + i, yb - 14 + 12, 1, 14, shade(H("5C7A94"), 0.8))
        for k in range(3):
            ix = x + 4 + k * 7
            b.rect(ix, yb - 24, 3, 6, H("EDE8DC"))
            b.rect(ix, yb - 24, 1, 6, H("FFFFFF"))
            b.rect(ix, yb - 27, 3, 3, H("C25B3A"))
    # control hut and hazard stripe
    gw.shadow(g, 20, 88, 22, 10, 8, alpha=60)
    rx, ry, rw, rd = gw.box3d(b, 20, 88, 22, 10, 8, H("7C8C99"), H("D8DEE3"))
    gw.door(b, 29, 85, 4, 4, H("46525E"))
    for i in range(0, 60, 4):
        g.rect(2 + i, 92, 2, 2, H("F2C94C"))
    # bolt badge
    b.polygon([(50, 42), (56, 42), (53, 48), (57, 48), (49, 58), (51, 50), (47, 50)], H("F2C94C"))
    return [gw.finish(b, g)]


def battery():
    g, b = gw.new_layers(32, 64)
    gw.pad(g, 1, 33, 30, 30, H("A5AAA2"))
    gw.shadow(g, 4, 58, 24, 14, 14, alpha=60)
    gw.box3d(b, 4, 58, 24, 14, 14, H("5FA85A"), H("3E7B45"))
    for i in range(4):
        b.rect(7 + i * 5, 46, 3, 8, H("2E5C36"))
        b.rect(7 + i * 5, 46, 3, 1, H("8BD582"))
    b.rect(10, 38, 12, 3, H("4B5058"))
    b.rect(22, 39, 2, 1, H("4B5058"))
    b.polygon([(18, 47), (13, 54), (16, 54), (14, 60), (21, 52), (18, 52)], H("F2D94C"))
    return [gw.finish(b, g)]


def sewage_outlet():
    g, b = gw.new_layers(32, 64)
    gw.pad(g, 1, 33, 30, 30, H("9FA89B"))
    g.ellipse(22, 54, 9, 5, H("6B5A3B"))
    g.ellipse(22, 54, 7, 3.5, H("8A7550"))
    gw.shadow(g, 4, 55, 14, 10, 10, alpha=60)
    rx, ry, rw, rd = gw.box3d(b, 4, 55, 14, 10, 10, H("6E7B6B"), H("C9C2AA"))
    gw.door(b, 8, 52, 3, 4, H("4F4A3C"))
    b.rect(18, 51, 12, 3, H("7A6A4A"))      # outfall pipe
    b.rect(18, 51, 12, 1, H("A39270"))
    b.rect(24, 40, 3, 11, H("8D8F92"))
    b.rect(24, 40, 1, 11, H("B8BBBE"))
    b.rect(22, 38, 7, 3, H("9A9C9F"))
    b.circle(25, 36, 2, (110, 90, 55, 160))
    return [gw.finish(b, g)]


def treatment_plant():
    g, b = gw.new_layers(64, 96)
    gw.pad(g, 2, 34, 60, 60, H("A8B1A5"))
    # settling tanks seen from above
    for (cx, cy) in ((18, 56), (42, 56)):
        g.ellipse(cx, cy, 13, 9, H("5B6B73"))
        g.ellipse(cx, cy, 11.5, 7.5, H("6FA2B8"))
        g.ellipse(cx, cy, 6, 4, H("8FC2D6"))
        g.line(cx - 11, cy, cx + 11, cy, with_alpha(H("D8EEF5"), 140))
    gw.shadow(g, 6, 90, 52, 12, 12, alpha=60)
    rx, ry, rw, rd = gw.box3d(b, 6, 90, 52, 12, 12, H("7C8C99"), H("DDE3E8"))
    for x in range(10, 54, 9):
        b.rect(x, 80, 5, 3, H("46525E"))
    # digester dome and stack
    b.ellipse(48, 40, 9, 5, H("B9C4CC"))
    b.rect(39, 28, 18, 12, H("CFD8DE"))
    b.ellipse(48, 28, 9, 5, H("E8EEF2"))
    b.rect(39, 28, 3, 12, (255, 255, 255, 60))
    b.rect(54, 28, 3, 12, (0, 0, 40, 40))
    b.rect(14, 34, 5, 24, H("9A8F86"))
    b.rect(14, 34, 2, 24, H("C2B8AE"))
    b.rect(14, 34, 5, 2, H("5C524A"))
    gw.smoke(b, 16, 30, n=2, seed=3, base=5)
    return [gw.finish(b, g)]


# --------------------------------------------------------------------------- registration
def register(net):
    out = net.NET

    def write():
        save_sheet(os.path.join(out, "powerline.png"), [powerline_frame(m) for m in range(16)], cols=8)
        save_sheet(os.path.join(out, "pipe-water.png"), [pipe_frame(m, 12, H("3E8FE0")) for m in range(16)], cols=8)
        save_sheet(os.path.join(out, "pipe-sewage.png"), [pipe_frame(m, 18, H("9A6B3A")) for m in range(16)], cols=8)
        for name, fn in (("transformer", transformer), ("battery", battery), ("sewage-outlet", sewage_outlet),
                         ("treatment-plant", treatment_plant)):
            save_sheet(os.path.join(out, name + ".png"), fn())

    net.EXTRA_WRITERS.append(write)
    seq = "utilnet:\n"
    for n in ("powerline", "pipe-water", "pipe-sewage"):
        seq += f"\t{n}:\n\t\tFilename: net/{n}.png\n\t\tLength: 16\n"
    for n in ("transformer", "battery", "sewage-outlet", "treatment-plant"):
        seq += f"{n}:\n\tidle:\n\t\tFilename: net/{n}.png\n\t\tLength: *\n\t\tOffset: 0,-16\n"
    net.EXTRA_SEQUENCES.append("\n" + seq)

    def contact(put, newrow):
        for m in range(16):
            put(powerline_frame(m))
        newrow()
        for m in range(16):
            put(pipe_frame(m, 12, H("3E8FE0")))
        newrow()
        for fn in (transformer, battery, sewage_outlet, treatment_plant):
            put(fn()[0])
        newrow()

    net.EXTRA_CONTACT.append(contact)
