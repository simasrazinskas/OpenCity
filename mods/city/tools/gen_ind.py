#!/usr/bin/env python3
"""
gen_ind.py - OpenCity industry art (WP IND): extractor hubs, field tiles, oil derrick.

    python3 mods/city/tools/gen_ind.py [--sheet /tmp/ind.png]

Writes bits/world/ind/*.png and mods/city/sequences/industry.yaml. Reuses the drawing helpers of genworld.py
(imported, not edited). Hubs: 2x2 = 64x96, 3x3 = 96x128 frames, Offset 0,-16 (footprint = the bottom square).
Field tiles are 32x32 (4 variants per sequence).
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, shade, mix, hexcolor  # noqa: E402
import genworld as gw  # noqa: E402
from genworld import H, shadow, box3d, pitched, windows, tank, stack, smoke, finish, new_layers, pad, tree  # noqa: E402

OUTDIR = os.path.join(gw.WORLD, "ind")
SEQ = os.path.join(gw.MOD, "sequences", "industry.yaml")


def R(*key):
    return random.Random("opencity-ind:" + ":".join(str(k) for k in key))


def yard(g, w, h, col, seed):
    """Ground pad of a hub: the whole footprint square, bottom of the frame."""
    pad(g, 0, h - w, w, w, col)
    r = R("yard", seed)
    for _ in range(w * 2):
        g.blend(r.randrange(w), h - w + r.randrange(w), (0, 0, 0, 26))


def silo(b, g, cx, yb, h, r=5, col=H("C9D1D9")):
    shadow(g, cx - r, yb, 2 * r, 5, h, alpha=55)
    for i in range(h):
        t = i / max(1, h - 1)
        b.rect(cx - r, yb - i, 2 * r, 1, mix(shade(col, 1.12), shade(col, 0.72), t * 0.6))
    for i in range(0, h, 5):
        b.rect(cx - r, yb - i, 2 * r, 1, (0, 0, 0, 40))
    b.rect(cx + r - 2, yb - h, 2, h, (0, 0, 0, 45))
    b.ellipse(cx, yb - h, r, r * 0.5, shade(col, 1.2))
    b.ellipse(cx, yb - h - 3, r * 0.55, r * 0.3, shade(col, 0.9))
    b.ellipse(cx, yb, r, r * 0.45, shade(col, 0.6))


def crate(b, x, y, w=5, h=4, col=H("C98A3A")):
    b.rect(x, y, w, h, col)
    b.rect(x, y, w, 1, shade(col, 1.3))
    b.rect(x + w - 1, y, 1, h, shade(col, 0.7))


def bale(b, x, y, col=H("E3C75A")):
    b.circle(x, y, 3, shade(col, 0.8))
    b.circle(x - 0.5, y - 0.5, 2, col)
    b.hline(x - 2, x + 2, y, shade(col, 0.7))


def log_pile(b, g, x, y, w=14, rows=3):
    shadow(g, x, y + 2, w, 3, 4, alpha=45)
    for j in range(rows):
        for i in range(w // 3 - j // 2):
            cx = x + 2 + i * 3 + (j % 2) * 1.5
            cy = y - j * 2.5
            b.circle(cx, cy, 1.8, H("7A5233"))
            b.circle(cx, cy, 0.9, H("D9AE7A"))


# ---------------------------------------------------------------------- hubs
def farm_hub():
    g, b = new_layers(96, 128)
    yard(g, 96, 128, H("B9A27A"), 1)
    for i in range(0, 96, 6):  # fence along the south edge
        b.rect(i, 124, 1, 3, H("8B6A45"))
    b.hline(0, 95, 125, H("D4B88A"))
    # barn
    shadow(g, 6, 100, 48, 28, 22)
    rx, ry, rw, rd = box3d(b, 6, 100, 48, 28, 20, H("A8453A"), H("C8553F"))
    pitched(b, rx, ry, rw, rd, H("A8453A"), ridge_vertical=False)
    b.rect(6 + 14, 100 - 14, 20, 14, H("F2EBDD"))      # big door with white X trim
    b.rect_outline(6 + 14, 100 - 14, 20, 14, H("F2EBDD"), 1)
    b.rect(6 + 15, 100 - 13, 18, 12, H("7E2F2A"))
    b.line(6 + 15, 100 - 13, 6 + 32, 100 - 2, H("F2EBDD"), 1)
    b.line(6 + 32, 100 - 13, 6 + 15, 100 - 2, H("F2EBDD"), 1)
    # silos
    silo(b, g, 66, 84, 34, 7)
    silo(b, g, 82, 90, 28, 6, H("D9D2BE"))
    # shed + hay + trough
    shadow(g, 62, 120, 26, 12, 8)
    box3d(b, 62, 120, 26, 12, 8, H("9AA3A0"), H("C5CAD0"))
    for (x, y) in ((10, 112), (17, 116), (24, 112)):
        bale(b, x, y)
    crate(b, 54, 118)
    tree(b, 91, 66, 5)
    return finish(b, g)


def forestry_hub():
    g, b = new_layers(64, 96)
    yard(g, 64, 96, H("A38966"), 2)
    shadow(g, 4, 76, 36, 22, 14)
    rx, ry, rw, rd = box3d(b, 4, 76, 36, 22, 12, H("6E7B52"), H("B08A5C"))
    pitched(b, rx, ry, rw, rd, H("5F6E47"), ridge_vertical=True)
    b.rect(10, 70, 10, 6, H("5A3E26"))                  # saw bay
    b.rect(24, 62, 4, 2, H("3E2A1A"))
    stack(b, g, 36, 62, 20, 4, seed=3, puff=True)
    log_pile(b, g, 6, 92, 20, 4)
    log_pile(b, g, 40, 90, 18, 3)
    for i in range(3):                                   # sawn planks
        b.rect(44, 78 + i * 2, 14, 1, H("E1C48C"))
        b.rect(44, 79 + i * 2, 14, 1, H("B99A60"))
    tree(b, 56, 52, 5, H("2F7F45"))
    tree(b, 50, 50, 4, H("3F9B4A"))
    return finish(b, g)


def quarry_hub():
    g, b = new_layers(96, 128)
    yard(g, 96, 128, H("A8A39A"), 3)
    # stepped pit
    for i, (x, y, w, h, col) in enumerate(((46, 60, 46, 56, "9C968B"), (52, 66, 34, 44, "86806F"), (58, 72, 22, 32, "6E6859"), (63, 78, 12, 20, "4F4A3E"))):
        g.rect(x, y + 32, w, h, H(col))
        g.rect(x, y + 32, w, 1, shade(H(col), 1.25))
        g.rect(x + w - 1, y + 32, 1, h, shade(H(col), 0.7))
    shadow(g, 4, 112, 36, 20, 16)
    rx, ry, rw, rd = box3d(b, 4, 112, 36, 20, 14, H("6F7782"), H("B8BFC7"))
    for i in range(0, rw - 3, 6):
        b.rect(rx + i, ry + 1, 3, rd - 2, shade(H("6F7782"), 1.15))
    b.rect(4, ry + rd, 36, 2, gw.YELLOW)
    windows(b, 7, ry + rd + 3, 30, 4, 6, 1, lit=0.5, seed=4)
    # crusher + conveyor over the pit
    b.line(40, 84, 66, 70, H("5E6670"), 3)
    b.line(40, 83, 66, 69, H("A0A8B2"), 1)
    box3d(b, 62, 76, 10, 8, 10, H("D9A441"), H("B8832C"))
    for (x, y) in ((8, 120), (14, 124), (20, 121)):      # rock heaps
        g.ellipse(x, y, 6, 3, H("8C877E"))
        g.ellipse(x - 1, y - 1, 4, 2, H("B0AAA0"))
    crate(b, 74, 118, 6, 5, H("D2873A"))
    return finish(b, g)


def mine_hub():
    g, b = new_layers(96, 128)
    yard(g, 96, 128, H("7D7A74"), 4)
    for i in range(0, 90, 5):                              # rails
        g.rect(8 + i, 120, 2, 3, H("5A4A3A"))
    g.hline(6, 92, 121, H("A5ACB5"))
    g.hline(6, 92, 123, H("A5ACB5"))
    shadow(g, 4, 112, 34, 22, 14)
    rx, ry, rw, rd = box3d(b, 4, 112, 34, 22, 12, H("5F6772"), H("A9B0B8"))
    pitched(b, rx, ry, rw, rd, H("5F6772"))
    b.rect(10, 104, 8, 8, H("2A2420"))                     # adit mouth
    b.rect(10, 104, 8, 1, H("6B6258"))
    # headframe
    shadow(g, 60, 98, 22, 12, 40, alpha=55)
    for dx in (0, 14):
        b.line(62 + dx, 98, 70 + dx // 2, 52, H("4C525C"), 2)
    b.rect(62, 78, 20, 2, H("4C525C"))
    b.rect(62, 90, 20, 2, H("4C525C"))
    b.circle(70, 52, 5, H("8A929C"))
    b.circle(70, 52, 2, H("2C3038"))
    b.line(70, 52, 70, 98, H("D9D2BE"), 1)
    b.rect(60, 98, 24, 8, H("4C525C"))
    for (x, y) in ((16, 118), (24, 118)):                  # ore carts
        crate(b, x, y - 4, 7, 5, H("7A6A5A"))
        b.rect(x + 1, y - 5, 5, 1, H("6A8BB0"))
    for (x, y) in ((84, 122), (88, 118), (80, 119)):
        g.ellipse(x, y, 5, 3, H("3B3F46"))
        g.ellipse(x - 1, y - 1, 3, 1.5, H("5E6672"))
    return finish(b, g)


def pumpjack(b, g, cx, yb, t=0, s=1.0):
    """Beam pumpjack (side view). t in 0..1 drives the rocking beam."""
    import math
    shadow(g, cx - 10, yb, 22, 6, 12, alpha=50)
    b.rect(cx - 8, yb - 3, 17, 3, H("6A7078"))             # base
    b.line(cx - 2, yb - 3, cx - 2, yb - 17, H("D9A441"), 2)  # samson post
    b.line(cx + 3, yb - 3, cx - 2, yb - 17, H("D9A441"), 2)
    a = math.sin(t * math.tau) * 3
    b.line(cx - 10, yb - 15 + a, cx + 9, yb - 19 - a, H("C9852A"), 2)    # walking beam
    b.rect(cx - 12, yb - 18 + a, 4, 6, H("E2B04A"))        # horse head
    b.line(cx - 10, yb - 12 + a, cx - 10, yb - 3, H("2C3038"), 1)         # polished rod
    b.circle(cx + 8, yb - 19 - a, 2, H("8A929C"))
    b.rect(cx + 6, yb - 8, 6, 5, H("B8832C"))             # motor


def oil_hub():
    g, b = new_layers(64, 96)
    yard(g, 64, 96, H("3E4046"), 5)
    for xx in range(0, 64, 8):
        g.rect(xx, 94, 4, 1, gw.YELLOW)
    shadow(g, 4, 76, 30, 20, 12)
    rx, ry, rw, rd = box3d(b, 4, 76, 30, 20, 10, H("8A929C"), H("D8DCE0"))
    for i in range(0, rw - 3, 6):
        b.rect(rx + i, ry + 1, 3, rd - 2, shade(H("8A929C"), 1.15))
    b.rect(4, ry + rd, 30, 2, gw.YELLOW)
    windows(b, 7, ry + rd + 3, 24, 3, 4, 1, lit=0.5, seed=5)
    tank(b, 46, 64, 7, H("B9C6D0"))
    tank(b, 52, 82, 6, H("D9D2BE"))
    pumpjack(b, g, 18, 94)
    b.hline(40, 62, 90, H("D9A441"))                      # pipe rack
    return finish(b, g)


def derrick(t):
    g, b = new_layers(32, 64)
    g.rect(2, 34, 28, 28, H("3E4046"))
    pumpjack(b, g, 15, 56, t / 6.0)
    return finish(b, g)


# ---------------------------------------------------------------------- field tiles (32x32)
def field_base(col, seed, variant):
    c = Canvas(32, 32, col)
    c.noise(0.06, seed=seed * 11 + variant)
    return c


def crop_field(base, row, seed, variant, plant=None, spacing=4):
    c = field_base(base, seed, variant)
    r = R("crop", seed, variant)
    horizontal = variant % 2 == 0
    for i in range(1, 32, spacing):
        if horizontal:
            c.rect(0, i, 32, 2, row)
            c.rect(0, i + 2, 32, 1, shade(base, 0.8))
            if plant:
                for x in range(0, 32, 3):
                    c.set(x + r.randrange(2), i, plant)
        else:
            c.rect(i, 0, 2, 32, row)
            c.rect(i + 2, 0, 1, 32, shade(base, 0.8))
            if plant:
                for y in range(0, 32, 3):
                    c.set(i, y + r.randrange(2), plant)
    c.rect_outline(0, 0, 32, 32, (0, 0, 0, 22))
    return c


def livestock_field(variant):
    c = field_base(H("7DB25A"), 7, variant)
    r = R("live", variant)
    for _ in range(20):
        c.set(r.randrange(32), r.randrange(32), H("5C9944"))
    if variant % 2 == 0:
        c.hline(0, 31, 0, H("C4A878"))
        c.hline(0, 31, 31, H("C4A878"))
    for _ in range(1 + variant % 2):
        x, y = 6 + r.randrange(18), 6 + r.randrange(18)
        c.rect(x, y, 5, 3, H("F2EEE6"))
        c.rect(x + 1, y, 2, 2, H("2C2A28"))
        c.rect(x + 4, y, 1, 2, H("DDD6C8"))
    c.rect_outline(0, 0, 32, 32, (0, 0, 0, 20))
    return c


def quarry_tile(variant):
    c = field_base(H("9A948A"), 3, variant)
    r = R("quarry", variant)
    for _ in range(26):
        x, y = r.randrange(30), r.randrange(30)
        c.rect(x, y, 2, 2, shade(H("9A948A"), r.choice((0.75, 1.15, 1.3))))
    ox, oy = (variant % 2) * 12, (variant // 2) * 12
    c.rect(2 + ox // 2, 3 + oy // 2, 18, 14, H("7C766B"))     # terrace
    c.rect(2 + ox // 2, 3 + oy // 2, 18, 1, H("C2BCB0"))
    c.rect(2 + ox // 2, 16 + oy // 2, 18, 1, H("4F4A3F"))
    c.rect_outline(0, 0, 32, 32, (0, 0, 0, 25))
    return c


def mine_tile(variant):
    c = field_base(H("5A5650"), 4, variant)
    r = R("mine", variant)
    for _ in range(34):
        c.set(r.randrange(32), r.randrange(32), shade(H("5A5650"), r.choice((0.6, 1.3, 1.5))))
    if variant % 2 == 0:
        for x in range(0, 32, 4):
            c.rect(x, 14, 2, 4, H("5A4A3A"))
        c.hline(0, 31, 15, H("9CA4AE"))
        c.hline(0, 31, 17, H("9CA4AE"))
    for _ in range(2):
        x, y = r.randrange(26), r.randrange(26)
        c.rect(x, y, 4, 3, H("7C8CA0"))
        c.rect(x, y, 4, 1, H("B4C4D8"))
    c.rect_outline(0, 0, 32, 32, (0, 0, 0, 30))
    return c


def oil_tile(variant):
    c = field_base(H("3A3C42"), 5, variant)
    r = R("oil", variant)
    for _ in range(22):
        c.set(r.randrange(32), r.randrange(32), H("55585F"))
    for _ in range(2):
        x, y = r.randrange(8, 24), r.randrange(8, 24)
        c.ellipse(x, y, 5, 3, H("15161A"))
        c.ellipse(x - 1, y - 1, 2, 1, H("4B5A7A"))
    if variant % 2 == 1:
        c.rect(0, 28, 32, 2, H("D9A441"))                   # pipe
        c.rect(0, 28, 32, 1, H("F0C86A"))
    c.rect_outline(0, 0, 32, 32, (0, 0, 0, 30))
    return c


def fish_hub():
    g, b = new_layers(64, 96)
    yard(g, 64, 96, H("C8B88A"), 6)
    shadow(g, 6, 78, 30, 20, 12)
    rx, ry, rw, rd = box3d(b, 6, 78, 30, 20, 10, H("3F6E9E"), H("D8D2BE"))
    pitched(b, rx, ry, rw, rd, H("3F6E9E"), ridge_vertical=True)
    b.rect(12, 70, 8, 8, H("5A3E26"))                      # door
    for x in (38, 46, 54):                                 # net poles with a net between them
        b.rect(x, 62, 2, 18, H("7A5233"))
    b.line(39, 66, 55, 66, H("9CC4D8"), 1)
    b.line(39, 72, 55, 72, H("9CC4D8"), 1)
    for k in range(0, 16, 3):
        b.line(39 + k, 66, 39 + k, 72, (156, 196, 216, 150), 1)
    crate(b, 8, 90, 7, 5, H("B98A52"))                     # fish crates
    crate(b, 18, 92, 7, 5, H("B98A52"))
    b.rect(9, 91, 5, 1, H("C9D6DF"))
    b.rect(19, 93, 5, 1, H("C9D6DF"))
    # pier planks to the south edge, a lamp and a gull-white bollard
    for i in range(0, 14):
        b.rect(44, 82 + i, 12, 1, shade(H("9A7348"), 1.0 + (i % 2) * 0.12))
    b.rect(44, 82, 1, 14, H("5A3E26"))
    b.rect(55, 82, 1, 14, H("5A3E26"))
    return finish(b, g)


def fish_tile(variant):
    c = Canvas(32, 32)
    r = R("fish", variant)
    for _ in range(4):
        x, y = 4 + r.randrange(22), 4 + r.randrange(22)
        c.hline(x, x + 5, y, (255, 255, 255, 70))
        c.hline(x + 1, x + 4, y + 1, (255, 255, 255, 45))
    x, y = 6 + r.randrange(16), 8 + r.randrange(14)
    c.ellipse(x, y, 4, 2, (24, 60, 96, 110))               # a dark fish shadow
    c.rect(x + 4, y - 1, 2, 3, (24, 60, 96, 110))
    return c


# ---------------------------------------------------------------------- output
AREAS = [
    ("farm-grain", lambda v: crop_field(H("C9B25A"), H("B79A3F"), 1, v, H("E8D27A"))),
    ("farm-vegetables", lambda v: crop_field(H("7A5B3A"), H("5D4429"), 2, v, H("5FB24A"), 5)),
    ("farm-livestock", lambda v: livestock_field(v)),
    ("farm-cotton", lambda v: crop_field(H("8C7650"), H("6F5B3C"), 3, v, H("F4F1EA"), 5)),
    ("quarry", quarry_tile),
    ("mine", mine_tile),
    ("oil", oil_tile),
    ("fish", fish_tile),
]

HUBS = {"farm-hub": (farm_hub, 3), "forestry-hub": (forestry_hub, 2), "quarry-hub": (quarry_hub, 3),
        "mine-hub": (mine_hub, 3), "oil-hub": (oil_hub, 2), "fish-hub": (fish_hub, 2)}


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    hubs = {}
    for name, (fn, _size) in HUBS.items():
        c = fn()
        hubs[name] = c
        save_sheet(os.path.join(OUTDIR, name + ".png"), [c])
    save_sheet(os.path.join(OUTDIR, "oil-derrick.png"), [derrick(t) for t in range(6)])
    frames = []
    for _name, fn in AREAS:
        frames += [fn(v) for v in range(4)]
    save_sheet(os.path.join(OUTDIR, "extractor-area.png"), frames, cols=4)

    t = "# Extractor hubs, field tiles and props (WP IND). Generated by tools/gen_ind.py.\n"
    for name in HUBS:
        t += f"{name}:\n\tidle:\n\t\tFilename: ind/{name}.png\n\t\tOffset: 0,-16\n\n"
    t += "oil-derrick:\n\tidle:\n\t\tFilename: ind/oil-derrick.png\n\t\tLength: *\n\t\tTick: 120\n\t\tOffset: 0,-16\n\n"
    t += "extractor-area:\n"
    for i, (name, _fn) in enumerate(AREAS):
        t += f"\t{name}:\n\t\tFilename: ind/extractor-area.png\n\t\tStart: {i * 4}\n\t\tLength: 4\n"
    with open(SEQ, "w") as f:
        f.write(t)

    if "--sheet" in sys.argv:
        out = sys.argv[sys.argv.index("--sheet") + 1]
        sheet = Canvas(96 * 5 + 20, 128 + 32 * 7 + 60, H("5B8A3C"))
        for i, (name, c) in enumerate(hubs.items()):
            sheet.blit(c, 4 + i * 100, 4)
        for j, (name, fn) in enumerate(AREAS):
            for v in range(4):
                sheet.blit(fn(v), 4 + v * 33 + (j % 3) * 140, 140 + (j // 3) * 36)
        for t in range(6):
            sheet.blit(derrick(t), 4 + t * 34, 140 + 3 * 36)
        sheet.save(out)
    print("gen_ind: done")


if __name__ == "__main__":
    main()
