"""
gen_net_ext.py - wave 2 road art for gen_net.py: roadside add-on strips (trees, sound barrier, lights, parking bays),
lane paint (bus and bike lanes) and the bridge deck. Registered by gen_net.main().

  road-strips.png : 240 frames, (combo - 1) * 16 + side mask. combo bit0 trees, bit1 barrier, bit2 lights, bit3 parking;
                    side mask bit i (N, E, S, W) = draw the strip on that edge (the sides without a road arm).
  road-lanes.png  : 48 frames, (lanes - 1) * 16 + arm mask. lanes bit0 bus lane, bit1 bike lane.
  road-bridge.png : 4 frames: N-S two-way, E-W two-way, N-S one-way, E-W one-way.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, shade, with_alpha, hexcolor  # noqa: E402

H = hexcolor
WHITE = H("EEF0F2")
YELLOW = H("F2D16B")


# --------------------------------------------------------------------------- strips (drawn for the north edge)
def strip_parking(c):
    for x in range(1, 32, 2):
        if (x // 2) % 2 == 0:
            c.set(x, 10, with_alpha(WHITE, 210))
    for x in (4, 12, 20, 28):
        for y in range(5, 11):
            c.set(x, y, with_alpha(WHITE, 210))


def strip_barrier(c):
    for x in range(32):
        c.set(x, 2, H("B7C4CF"))
        c.set(x, 3, H("8294A4"))
        c.set(x, 4, H("5F7284"))
        c.set(x, 5, (10, 18, 30, 70))
    for x in range(0, 32, 8):
        c.set(x, 1, H("5F7284"))
        c.set(x, 2, H("DDE5EC"))


def strip_lights(c):
    for x in (9, 25):
        c.circle(x, 6, 4, (255, 224, 130, 46))
        c.set(x, 3, H("2B2F36"))
        c.set(x, 4, H("2B2F36"))
        c.set(x, 5, H("2B2F36"))
        c.set(x + 1, 3, H("FFE9A0"))
        c.set(x, 2, H("FFF6CC"))
        c.set(x + 1, 2, H("FFE9A0"))
        c.set(x - 1, 2, H("5F6874"))


def strip_trees(c):
    for x, y, r in ((5, 3, 3), (16, 3, 3), (27, 3, 3)):
        c.circle(x + 1, y + 2, r, (20, 40, 20, 90))
        c.circle(x, y, r, H("3E7A35"))
        c.circle(x - 1, y - 1, max(1, r - 1), H("5AA04A"))
        c.set(x - 1, y - 1, H("8CCB74"))


def strip_frame(combo, sidemask):
    n = Canvas(32, 32)
    if combo & 8:
        strip_parking(n)
    if combo & 2:
        strip_barrier(n)
    if combo & 4:
        strip_lights(n)
    if combo & 1:
        strip_trees(n)
    out = Canvas(32, 32)
    for side in range(4):
        if sidemask & (1 << side):
            out.blit(n.rotate90(side) if side else n, 0, 0)
    return out


# --------------------------------------------------------------------------- lanes
def lane_frame(lanes, arms):
    c = Canvas(32, 32)
    bus, bike = lanes & 1, lanes & 2
    N, E, S, W = arms & 1, arms & 2, arms & 4, arms & 8

    def band(x0, y0, x1, y1, col, mark):
        for y in range(y0, y1):
            for x in range(x0, x1):
                c.blend(x, y, col)
        return mark

    def arm_n(col, x0, w, kind):
        for y in range(0, 16):
            for x in range(x0, x0 + w):
                c.blend(x, y, col)
        for y in range(2, 16, 8):
            if kind == "bus":
                for i in range(3):
                    c.set(x0 + w // 2, y + i, with_alpha(WHITE, 220))
            else:
                c.set(x0 + 1, y + 1, with_alpha(WHITE, 230))
                c.set(x0 + 1, y + 3, with_alpha(WHITE, 230))
                c.set(x0, y + 2, with_alpha(WHITE, 230))
                c.set(x0 + 2, y + 2, with_alpha(WHITE, 230))

    def make(col, x0, w, kind):
        cv = Canvas(32, 32)
        keep = c.copy()
        c.px = [list(p) for p in cv.px]
        arm_n(col, x0, w, kind)
        art = c.copy()
        c.px = [list(p) for p in keep.px]
        return art

    layers = []
    if bus:
        layers.append(make((196, 64, 52, 150), 24, 4, "bus"))
    if bike:
        layers.append(make((70, 170, 90, 150), 5 if not bus else 20, 3, "bike"))
    for art in layers:
        for rot, present in ((0, N), (1, E), (2, S), (3, W)):
            if present:
                c.blit(art.rotate90(rot) if rot else art, 0, 0)
    return c


# --------------------------------------------------------------------------- bridge deck
def bridge_frame(ew, oneway):
    c = Canvas(32, 32)
    asph = H("4D5159")
    rail = H("C4CBD2")
    lo, hi = 5, 27       # deck spans lo..hi across the road axis
    for a in range(32):  # along the road
        for b in range(lo - 2, hi + 3):
            x, y = (a, b) if ew else (b, a)
            if b < lo or b > hi:
                col = rail if (b in (lo - 2, hi + 2)) else shade(H("8D96A0"), 0.9)
                c.set(x, y, col)
            else:
                n = ((x * 73856093) ^ (y * 19349663)) & 7
                c.set(x, y, shade(asph, 0.97 + n * 0.005))
    # drop shadow on the water side (south-east)
    for a in range(32):
        for k in range(3):
            b = hi + 3 + k
            x, y = (a, b) if ew else (b, a)
            c.blend(x + (1 if not ew else 0), y + (1 if ew else 0), (10, 30, 50, 90 - k * 25))
    # rail posts
    for a in range(2, 32, 8):
        for b in (lo - 2, hi + 2):
            x, y = (a, b) if ew else (b, a)
            c.set(x, y, H("8D96A0"))
            c.set(x + 1, y, H("8D96A0"))
    # centre line
    if not oneway:
        mid = (lo + hi) // 2
        for a in range(32):
            if (a % 8) < 4:
                for k in (0, 1):
                    x, y = (a, mid + k) if ew else (mid + k, a)
                    c.set(x, y, YELLOW)
    return c


# --------------------------------------------------------------------------- registration
def register(net):
    out = net.NET

    def write():
        strips = [strip_frame(combo, side) for combo in range(1, 16) for side in range(16)]
        save_sheet(os.path.join(out, "road-strips.png"), strips, cols=16)
        lanes = [lane_frame(l, a) for l in range(1, 4) for a in range(16)]
        save_sheet(os.path.join(out, "road-lanes.png"), lanes, cols=16)
        save_sheet(os.path.join(out, "road-bridge.png"), [bridge_frame(0, 0), bridge_frame(1, 0), bridge_frame(0, 1), bridge_frame(1, 1)])

    net.EXTRA_WRITERS.append(write)
    seq = ("\tstrips:\n\t\tFilename: net/road-strips.png\n\t\tLength: 240\n"
           "\tlanes:\n\t\tFilename: net/road-lanes.png\n\t\tLength: 48\n"
           "\tbridge:\n\t\tFilename: net/road-bridge.png\n\t\tLength: 4\n")
    net.EXTRA_SEQUENCES.append(seq)

    def contact(put, newrow):
        for combo in (1, 2, 4, 8, 15):
            put(strip_frame(combo, 5))
            put(strip_frame(combo, 10))
        newrow()
        for l in (1, 2, 3):
            put(lane_frame(l, 5))
            put(lane_frame(l, 10))
        for ew in (0, 1):
            for ow in (0, 1):
                put(bridge_frame(ew, ow))
        newrow()

    net.EXTRA_CONTACT.append(contact)
