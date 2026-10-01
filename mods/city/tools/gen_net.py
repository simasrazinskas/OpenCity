#!/usr/bin/env python3
"""
gen_net.py - OpenCity road and utility network art (work package NET).

Writes bits/world/net/*.png and sequences/networks.yaml. Deterministic.

    python3 mods/city/tools/gen_net.py [--sheet /tmp/net-contact.png]

Road sheets have 32 frames of 32x32: frames 0..15 are the two-way autotiles, frames 16..31 the one-way
autotiles (no centre line, arrows are drawn by the `arrows` overlay). Frame index = N/E/S/W mask
(bit0 N, bit1 E, bit2 S, bit3 W), like the legacy roads.png.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngkit import Canvas, save_sheet, shade, mix, with_alpha, hexcolor  # noqa: E402

MOD = os.path.dirname(HERE)
NET = os.path.join(MOD, "bits", "world", "net")


def H(s, a=255):
    return hexcolor(s, a)


CURB = H("9AA0A8")
YELLOW = H("F2D16B")
WHITE = H("EEF0F2")
GRASS = H("5E8C45")
GRASS_D = H("4C7A38")

# name: style. R = margin (non-asphalt border width), lanes = lanes per direction drawn.
STYLES = {
    "street": dict(asph="4D5159", out="C9CCD1", curb="9AA0A8", R=4, lanes=1, centre="dash", edge=None, grid=True),
    "gravel": dict(asph="B59F70", out="6F9A4F", curb="8A7650", R=4, lanes=1, centre="none", edge=None, grid=False, sand=True),
    "avenue": dict(asph="474B53", out="D2D5DA", curb="9AA0A8", R=2, lanes=2, centre="double", edge=None, grid=True),
    "boulevard": dict(asph="42464E", out="D6D9DE", curb="8F959E", R=2, lanes=3, centre="double", edge=None, grid=True),
    "highway": dict(asph="34373D", out="6C8A58", curb="B8BEC6", R=3, lanes=2, centre="double", edge="E9EBEE", grid=False, shoulder=True),
    "alley": dict(asph="5A5E66", out="BDBFC4", curb="8E949C", R=7, lanes=1, centre="none", edge=None, grid=True),
}


def road_frame(style, mask, oneway):
    s = STYLES[style]
    R = s["R"]
    c = Canvas(32, 32)
    N, E, S, W = mask & 1, mask & 2, mask & 4, mask & 8

    def asphalt(x, y):
        if R <= x < 32 - R and R <= y < 32 - R:
            return True
        if N and R <= x < 32 - R and y < R:
            return True
        if S and R <= x < 32 - R and y >= 32 - R:
            return True
        if W and R <= y < 32 - R and x < R:
            return True
        if E and R <= y < 32 - R and x >= 32 - R:
            return True
        for (a, b2, cx, cy) in ((N, E, 32, 0), (E, S, 32, 32), (S, W, 0, 32), (W, N, 0, 0)):
            if a and b2:
                inx = (x >= 32 - R) if cx == 32 else (x < R)
                iny = (y >= 32 - R) if cy == 32 else (y < R)
                if inx and iny:
                    return math.hypot(x + 0.5 - cx, y + 0.5 - cy) >= R
        return False

    asph, out = H(s["asph"]), H(s["out"])
    for y in range(32):
        for x in range(32):
            n = ((x * 73856093) ^ (y * 19349663)) & 15
            if asphalt(x, y):
                col = shade(asph, 0.97 + n * 0.004)
                if s.get("sand") and n > 11:
                    col = shade(asph, 0.9)
            else:
                col = out
                if s.get("grid") and ((x % 8) == 0 or (y % 8) == 0) and n > 5:
                    col = shade(out, 0.93)
                elif not s.get("grid"):
                    col = shade(out, 0.94 + n * 0.008)
            c.set(x, y, col)
    # curb / guard rail on the first non-asphalt pixel next to asphalt, edge line on the asphalt side
    cur = c.copy()
    for y in range(32):
        for x in range(32):
            if asphalt(x, y):
                continue
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if asphalt(x + ox, y + oy):
                    cur.set(x, y, H(s["curb"]))
                    break
    c = cur
    if s["edge"]:
        ed = H(s["edge"])
        for y in range(32):
            for x in range(32):
                if not asphalt(x, y):
                    continue
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + ox, y + oy
                    if 0 <= nx < 32 and 0 <= ny < 32 and not asphalt(nx, ny):
                        c.set(x, y, ed)
                        break
    if style == "gravel":
        # two faint tyre tracks along the arms
        for (arm, horiz) in ((W, True), (E, True), (N, False), (S, False)):
            if not arm:
                continue
            for t in (11, 20):
                for k in range(0, 16):
                    if horiz:
                        x = k if arm == W else 31 - k
                        c.set(x, t, shade(asph, 0.88))
                    else:
                        y = k if arm == N else 31 - k
                        c.set(t, y, shade(asph, 0.88))
    if style != "gravel" and s["curb"]:
        for (cx, cy) in ((R, R), (31 - R, R), (R, 31 - R), (31 - R, 31 - R)):
            arm_h = (W if cx < 16 else E)
            arm_v = (N if cy < 16 else S)
            if not arm_h and not arm_v and R >= 3:
                c.set(cx, cy, H(s["curb"]))
    draw_markings(c, s, N, E, S, W, oneway)
    return c


def lane_positions(s, oneway):
    """Offsets (0..31) of the lane lines across the road; (position, kind) with kind dash|solid|double."""
    R = s["R"]
    width = 32 - 2 * R
    lanes = s["lanes"] if oneway else 2 * s["lanes"]
    out = []
    for k in range(1, lanes):
        pos = R + width * k // lanes
        centre = (not oneway) and k == lanes // 2
        if centre:
            if s["centre"] == "double":
                out.append((pos - 1, "yellow"))
                out.append((pos, "yellow"))
            elif s["centre"] == "dash":
                out.append((pos - 1, "dashyellow"))
                out.append((pos, "dashyellow"))
        else:
            out.append((pos, "dashwhite"))
    return out


def draw_markings(c, s, N, E, S, W, oneway):
    arms = [bool(N), bool(E), bool(S), bool(W)]
    cnt = sum(arms)
    if cnt not in (1, 2):
        return
    lines = lane_positions(s, oneway)

    def col(kind):
        return YELLOW if "yellow" in kind else WHITE

    def dashed(kind, i):
        return kind.startswith("dash") and (i % 8) >= 4

    def seg_v(x, y0, y1, kind):
        for y in range(y0, y1):
            if not dashed(kind, y):
                c.set(x, y, col(kind))

    def seg_h(y, x0, x1, kind):
        for x in range(x0, x1):
            if not dashed(kind, x):
                c.set(x, y, col(kind))

    for pos, kind in lines:
        if cnt == 2 and N and S:
            seg_v(pos, 0, 32, kind)
        elif cnt == 2 and E and W:
            seg_h(pos, 0, 32, kind)
        else:
            if N:
                seg_v(pos, 0, 16, kind)
            if S:
                seg_v(pos, 16, 32, kind)
            if W:
                seg_h(pos, 0, 16, kind)
            if E:
                seg_h(pos, 16, 32, kind)


def road_sheet(style):
    return [road_frame(style, m, False) for m in range(16)] + [road_frame(style, m, True) for m in range(16)]


# --------------------------------------------------------------------------- overlays
def arrow_frame(direction):
    """White arrow pointing N, E, S or W (0..3)."""
    c = Canvas(32, 32)
    a = Canvas(32, 32)
    # arrow pointing north: shaft and head
    for y in range(13, 21):
        for x in (15, 16):
            a.set(x, y, WHITE)
    for i in range(4):
        for x in range(15 - i, 17 + i):
            a.set(x, 10 + i, WHITE)
    a = a.rotate90(direction) if direction else a
    sh = a.copy()
    for y in range(32):
        for x in range(32):
            if a.get(x, y)[3] == 0:
                for ox, oy in ((1, 1), (1, 0), (0, 1)):
                    if a.get(x - ox, y - oy)[3] > 0:
                        sh.set(x, y, (20, 22, 28, 150))
                        break
    c.blit(sh, 0, 0)
    for y in range(32):
        for x in range(32):
            p = a.get(x, y)
            if p[3] > 0:
                c.set(x, y, with_alpha(p, 215))
    return c


def median_frame(mask):
    """Grass strip with kerbs along every blocked side (bit0 N, bit1 E, bit2 S, bit3 W)."""
    c = Canvas(32, 32)
    T = 6
    for y in range(32):
        for x in range(32):
            d = None
            if mask & 1 and y < T:
                d = y
            if mask & 4 and y >= 32 - T:
                d = min(d, 31 - y) if d is not None else 31 - y
            if mask & 2 and x >= 32 - T:
                d = min(d, 31 - x) if d is not None else 31 - x
            if mask & 8 and x < T:
                d = min(d, x) if d is not None else x
            if d is None:
                continue
            n = ((x * 73856093) ^ (y * 19349663)) & 7
            if d == 0 or d == T - 1:
                c.set(x, y, CURB)
            else:
                c.set(x, y, shade(GRASS, 0.92 + n * 0.02))
    # tiny bushes
    for (x, y) in ((6, 3), (22, 3), (9, 28), (25, 28), (3, 10), (3, 24), (28, 8), (28, 22)):
        if (mask & 1 and y < 6 and 3 <= y) or (mask & 4 and y >= 26) or (mask & 8 and x < 6) or (mask & 2 and x >= 26):
            c.set(x, y, GRASS_D)
            c.set(x + 1, y, GRASS_D)
    return c


def control_frames():
    out = []
    # 0 stop sign: red octagon on a post at the NE corner plus a white stop line
    c = Canvas(32, 32)
    for y in range(3, 11):
        for x in range(22, 30):
            if abs(x - 25.5) + abs(y - 6.5) <= 5.2:
                c.set(x, y, H("C8372D"))
    for x in range(24, 28):
        c.set(x, 6, WHITE)
    c.set(25, 11, H("5A5E66"))
    c.set(26, 11, H("5A5E66"))
    out.append(c)
    # 1 signal: pole with three lamps at NE and SW corners
    c = Canvas(32, 32)
    for (ox, oy) in ((24, 2), (4, 21)):
        c.rect(ox, oy, 4, 10, H("2A2E35"))
        c.set(ox + 1, oy + 1, H("E04B3C"))
        c.set(ox + 2, oy + 1, H("E04B3C"))
        c.set(ox + 1, oy + 4, H("3A3F47"))
        c.set(ox + 2, oy + 4, H("F2C94C"))
        c.set(ox + 1, oy + 7, H("4CD964"))
        c.set(ox + 2, oy + 7, H("4CD964"))
    out.append(c)
    # 2 roundabout: ring of counter-clockwise chevrons around the centre
    c = Canvas(32, 32)
    for k in range(4):
        ang = k * math.pi / 2 + math.pi / 4
        cx = 15.5 + 9 * math.cos(ang)
        cy = 15.5 - 9 * math.sin(ang)
        tx, ty = -math.sin(ang), -math.cos(ang)  # counter-clockwise tangent in screen space
        for t in range(-3, 4):
            px, py = cx + tx * t, cy + ty * t
            c.set(round(px), round(py), WHITE)
        hx, hy = cx + tx * 4, cy + ty * 4
        c.set(round(hx), round(hy), WHITE)
        c.set(round(cx + tx * 2 - ty * 2), round(cy + ty * 2 + tx * 2), WHITE)
        c.set(round(cx + tx * 2 + ty * 2), round(cy + ty * 2 - tx * 2), WHITE)
    out.append(c)
    # 3 ramp: yellow chevron band across the cell centre
    c = Canvas(32, 32)
    for i in range(3):
        y0 = 8 + i * 6
        for k in range(8):
            c.set(16 - k, y0 + k // 2, YELLOW)
            c.set(15 + k, y0 + k // 2, YELLOW)
            c.set(16 - k, y0 + k // 2 + 1, shade(YELLOW, 0.7))
            c.set(15 + k, y0 + k // 2 + 1, shade(YELLOW, 0.7))
    out.append(c)
    # 4 island: grass disc with a tree in a kerb ring (roundabout centre)
    c = Canvas(32, 32)
    for y in range(32):
        for x in range(32):
            d = math.hypot(x - 15.5, y - 15.5)
            if d < 14.5:
                if d >= 13:
                    c.set(x, y, CURB)
                else:
                    n = ((x * 73856093) ^ (y * 19349663)) & 7
                    c.set(x, y, shade(GRASS, 0.92 + n * 0.02))
    for (tx, ty, r) in ((15, 15, 5), (10, 18, 3), (21, 12, 3)):
        c.circle(tx + 1, ty + 1, r, (20, 40, 20, 90))
        c.circle(tx, ty, r, H("3E7A35"))
        c.circle(tx - 1, ty - 1, max(1, r - 2), H("5AA04A"))
    out.append(c)
    return out


def write_roads():
    os.makedirs(NET, exist_ok=True)
    for name in STYLES:
        save_sheet(os.path.join(NET, f"road-{name}.png"), road_sheet(name), cols=8)
    save_sheet(os.path.join(NET, "road-arrows.png"), [arrow_frame(d) for d in range(4)])
    save_sheet(os.path.join(NET, "road-median.png"), [median_frame(m) for m in range(16)], cols=8)
    save_sheet(os.path.join(NET, "road-control.png"), control_frames())


# --------------------------------------------------------------------------- sequences / main
EXTRA_SEQUENCES = []   # extra yaml text blocks (strings) appended by gen_net_util
EXTRA_WRITERS = []     # callables writing more PNGs
EXTRA_CONTACT = []     # callables (put, newrow) drawing into the contact sheet


def write_sequences():
    t = "# Road and utility network art. Generated by tools/gen_net.py.\n"
    t += "roadnet:\n"
    for name in STYLES:
        t += f"\t{name}:\n\t\tFilename: net/road-{name}.png\n\t\tLength: 32\n"
    t += "\tarrows:\n\t\tFilename: net/road-arrows.png\n\t\tLength: 4\n"
    t += "\tmedian:\n\t\tFilename: net/road-median.png\n\t\tLength: 16\n"
    t += "\tcontrol:\n\t\tFilename: net/road-control.png\n\t\tLength: 5\n"
    for extra in EXTRA_SEQUENCES:
        t += extra
    with open(os.path.join(MOD, "sequences", "networks.yaml"), "w") as f:
        f.write(t)


def contact_sheet(path):
    Z = 2
    W = 1500
    sheet = Canvas(W, 1500, (104, 148, 78, 255))
    st = {"x": 4, "y": 4, "rowh": 0}

    def put(cv, zoom=Z):
        f = cv.scale(zoom)
        if st["x"] + f.w > W:
            st["x"] = 4
            st["y"] += st["rowh"] + 4
            st["rowh"] = 0
        sheet.blit(f, st["x"], st["y"])
        st["x"] += f.w + 2
        st["rowh"] = max(st["rowh"], f.h)

    def newrow():
        st["x"] = 4
        st["y"] += st["rowh"] + 8
        st["rowh"] = 0

    for name in STYLES:
        frames = road_sheet(name)
        for fr in frames[:16]:
            put(fr)
        newrow()
        for fr in frames[16:]:
            put(fr)
        newrow()
    for d in range(4):
        put(arrow_frame(d))
    for m in (1, 2, 4, 8, 5, 10, 15):
        put(median_frame(m))
    newrow()
    for fr in control_frames():
        put(fr)
    newrow()
    for fn in EXTRA_CONTACT:
        fn(put, newrow)
    sheet.save(path)


def main():
    os.makedirs(NET, exist_ok=True)
    write_roads()
    try:
        import gen_net_ext
        gen_net_ext.register(sys.modules[__name__])
    except ImportError:
        pass
    try:
        import gen_net_util
        gen_net_util.register(sys.modules[__name__])
    except ImportError:
        pass
    for fn in EXTRA_WRITERS:
        fn()
    write_sequences()
    if "--sheet" in sys.argv:
        contact_sheet(sys.argv[sys.argv.index("--sheet") + 1])
    print("gen_net: done")


if __name__ == "__main__":
    main()
