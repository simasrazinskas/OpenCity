"""Tiny scene renderer for the FX composites (3x3 patch with road, pavement, two box buildings, lamps, cars).
Not a general renderer: just enough so the designer sees FX in context. Real art comes from isokit."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_fx_util import *  # noqa
import iso_life_fx_night as night
import iso_life_fx_weather as weather
import iso_life_fx_pollution as pollution
import iso_life_fx_fire as fire
import iso_life_fx_smoke as smoke
from iso_life_common import load_png

W, H = 216, 168
OX, OY = 108, 58                 # screen position of the patch's top corner (cell 0,0 north corner)

GRASS = ramp("#4e7c3c", "#5f8f48", "#70a055")
ASPH = ramp("#4a4d54", "#575a62", "#62656d")
PAVE = ramp("#8e8a80", "#a8a49a", "#bcb8ac")
LINE = (224, 216, 160)


def iso(X, Y, z=0.0):
    return OX + (X - Y) * 32.0, OY + (X + Y) * 16.0 - z


def inv(x, y):
    sx, sy = x + 0.5 - OX, y + 0.5 - OY
    return (sx / 32.0 + sy / 16.0) / 2.0, (sy / 16.0 - sx / 32.0) / 2.0


def ground(img):
    for y in range(H):
        for x in range(W):
            X, Y = inv(x, y)
            if not (0 <= X < 3 and 0 <= Y < 3):
                continue
            if 1.15 < Y < 1.85:
                c = tone(ASPH, 0.3 + 0.35 * pnoise(X % 1, Y % 1, 9), x, y)
                if abs(Y - 1.5) < 0.025 and (X * 4) % 1.0 < 0.5:
                    c = LINE
            elif 0.97 < Y <= 1.15 or 1.85 <= Y < 2.03:
                c = tone(PAVE, 0.5 + 0.2 * ((int(X * 6) + int(Y * 12)) % 2), x, y, False)
            else:
                c = tone(GRASS, 0.25 + 0.55 * pnoise(X % 1, Y % 1, 4), x, y)
            px(img, x, y, c)


def fill_quad(img, pts, col):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        for x in range(int(min(xs)), int(max(xs)) + 1):
            inside = True
            sg = 0
            for i in range(4):
                a, b = pts[i], pts[(i + 1) % 4]
                cr = (b[0] - a[0]) * (y + 0.5 - a[1]) - (b[1] - a[1]) * (x + 0.5 - a[0])
                if cr != 0:
                    s = 1 if cr > 0 else -1
                    if sg == 0:
                        sg = s
                    elif s != sg:
                        inside = False
                        break
            if inside:
                px(img, x, y, col(x, y) if callable(col) else col)


def window_px(img, x, y, ww, wh, face, col):
    for cx_ in range(ww):
        off = cx_ // 2 if face == "L" else (ww - 1 - cx_) // 2
        for ry in range(wh):
            px(img, x + cx_, y + ry + off, col)


def box(img, X0, Y0, X1, Y1, h, left, right, top, lit=None, glass=(62, 90, 120), seed=0):
    """Draw a box. Returns list of (x, y, ww, wh, face, is_lit) of its windows (for glow overlay)."""
    A, B, C, D = iso(X0, Y0), iso(X1, Y0), iso(X1, Y1), iso(X0, Y1)
    up = lambda p: (p[0], p[1] - h)
    fill_quad(img, [D, C, up(C), up(D)], left)
    fill_quad(img, [B, C, up(C), up(B)], right)
    fill_quad(img, [up(A), up(B), up(C), up(D)], top)
    wins = []
    rs = np.random.RandomState(seed)
    nfl = max(1, int(h // 10))
    # left face (down-right): windows every 8 px along the bottom edge
    span = int(C[0] - D[0])
    for k in range(5, span - 5, 8):
        for fl in range(nfl):
            wx = int(D[0]) + k
            wy = int(D[1] + k / 2.0 - fl * 10 - 8)
            on = bool(rs.rand() < 0.55)
            window_px(img, wx, wy, 3, 4, "L", (255, 233, 160) if (on and lit) else glass)
            wins.append((wx, wy, 3, 4, "L", on))
    span = int(C[0] - B[0])
    for k in range(5, abs(span) - 5, 8):
        for fl in range(nfl):
            wx = int(C[0]) + k - 3 + 1
            wy = int(C[1] - k / 2.0 - fl * 10 - 8)
            on = bool(rs.rand() < 0.5)
            window_px(img, wx, wy, 3, 4, "R", (190, 225, 255) if (on and lit) else glass)
            wins.append((wx, wy, 3, 4, "R", on))
    return wins


def shade(c, f):
    return tuple(int(min(255, v * k)) for v, k in zip(c, f))


def tint_night(img, mask=None):
    f = (0.30, 0.37, 0.66)
    rgb = img[..., :3].astype(np.float64)
    out = rgb * np.array(f)
    img[..., :3] = np.clip(out, 0, 255).astype(np.uint8)


def add_blend(dst, src, x, y):
    h, w = src.shape[:2]
    for yy in range(h):
        for xx in range(w):
            if src[yy, xx, 3] and 0 <= x + xx < dst.shape[1] and 0 <= y + yy < dst.shape[0]:
                dst[y + yy, x + xx, :3] = np.minimum(
                    255, dst[y + yy, x + xx, :3].astype(np.int32) + src[yy, xx, :3].astype(np.int32)).astype(np.uint8)


def new_scene(bg):
    img = canvas(W, H)
    img[..., :3] = bg
    img[..., 3] = 255
    ground(img)
    return img


def draw_city(img, lit=False):
    """Two buildings; returns windows lists."""
    w = []
    w += box(img, 0.25, 0.1, 0.95, 0.75, 32, (200, 168, 120), (143, 116, 82), (216, 196, 152), lit, seed=1)
    w += box(img, 1.55, 0.2, 2.45, 0.8, 42, (176, 106, 74), (126, 74, 52), (200, 138, 104), lit, seed=2)
    return w


def lamp_post(img, X, Y, h=20):
    bx, by = iso(X, Y)
    for k in range(h):
        px(img, bx, by - k, (70, 74, 82))
    px(img, bx + 1, by - h, (70, 74, 82))
    px(img, bx + 2, by - h, (70, 74, 82))
    px(img, bx + 2, by - h + 1, (255, 240, 190))
    return bx + 2, by - h + 1, bx, by


def car(img, X, Y, dirn, colour):
    """Small box car ~16 px long along dirn (NE/SE/SW/NW)."""
    if dirn in ("SE", "NW"):
        box(img, X - 0.2, Y - 0.07, X + 0.2, Y + 0.07, 5, shade(colour, (0.95, 0.95, 0.95)), shade(colour, (0.6, 0.6, 0.65)),
            shade(colour, (1.15, 1.15, 1.15)))
    else:
        box(img, X - 0.07, Y - 0.2, X + 0.07, Y + 0.2, 5, shade(colour, (0.95, 0.95, 0.95)), shade(colour, (0.6, 0.6, 0.65)),
            shade(colour, (1.15, 1.15, 1.15)))


def night_composite():
    img = new_scene((10, 14, 28))
    draw_city(img, lit=False)
    lamps = [lamp_post(img, 0.5, 1.0), lamp_post(img, 1.5, 1.96), lamp_post(img, 2.5, 1.0)]
    car(img, 1.2, 1.32, "SE", (176, 50, 44))
    car(img, 1.85, 1.68, "NW", (60, 98, 168))
    tint_night(img)
    # emissive things drawn after tint: windows lit (re-draw windows list), lamp heads
    wins = []
    wins += box_windows_only(img)
    for (hx, hy, bx, by) in lamps:
        px(img, hx, hy, (255, 244, 200))
    # glow layers (additive)
    for (hx, hy, bx, by) in lamps:
        add_blend(img, night.pool(22), int(bx - 22 - 1.5), int(by - 12 - 1.5))
        add_blend(img, night.bulb(), int(hx - 4), int(hy - 4))
    for (x, y, ww, wh, face, on) in wins:
        if on:
            add_blend(img, night.window(ww, wh, face, night.WARM if face == "L" else night.COOL), x - 2, y - 2)
    hx, hy = iso(1.2 + 0.2, 1.32)
    add_blend(img, night.cone("SE"), int(hx - 22), int(hy - 14))
    tx, ty = iso(1.2 - 0.2, 1.32)
    add_blend(img, night.tail("SE"), int(tx - 10), int(ty - 7))
    hx, hy = iso(1.85 - 0.2, 1.68)
    add_blend(img, night.cone("NW"), int(hx - 22), int(hy - 14))
    tx, ty = iso(1.85 + 0.2, 1.68)
    add_blend(img, night.tail("NW"), int(tx - 10), int(ty - 7))
    # a shop sign on the second building's left face
    sx, sy = iso(1.75, 0.8)
    add_blend(img, night.sign(9, 3, night.NEON["red"]), int(sx), int(sy - 5))
    return img


def box_windows_only(img):
    """Redraw the windows of both buildings emissive (after the night tint); returns the window list."""
    out = []
    # replicate box() window layout with the same seeds, drawing only lit windows
    for (X0, Y0, X1, Y1, h, seed) in ((0.25, 0.1, 0.95, 0.75, 32, 1), (1.55, 0.2, 2.45, 0.8, 42, 2)):
        tmp = canvas(W, H)
        wins = box(tmp, X0, Y0, X1, Y1, h, (0, 0, 0), (0, 0, 0), (0, 0, 0), True, seed=seed)
        for (x, y, ww, wh, face, on) in wins:
            if on:
                window_px(img, x, y, ww, wh, face, (255, 226, 140) if face == "L" else (170, 215, 250))
        out += wins
    return out


def fog_composite():
    img = new_scene((120, 140, 150))
    draw_city(img, False)
    for j in range(3):
        for i in range(3):
            dens = 0.82 if (i, j) == (1, 1) else 0.42
            tile = pollution_or_fog(dens)
            blit(img, tile, int(OX + (i - j) * 32 - 32), int(OY + (i + j) * 16))
    return img


def pollution_or_fog(dens):
    return weather.fog_tile(dens)


def smog_composite():
    img = new_scene((120, 128, 96))
    draw_city(img, False)
    for j in range(3):
        for i in range(3):
            lv = min(2, (i + j) // 2)
            tile = pollution.smog_tile((0.28, 0.5, 0.78)[lv], 11)
            blit(img, tile, int(OX + (i - j) * 32 - 32), int(OY + (i + j) * 16))
    # stains on the road tiles
    return img


def flood_composite():
    img = new_scene((120, 150, 130))
    # flooded cells: centre column of the patch (cells (0..2, 1)) -> the road, plus edges on (1,0) (1,2)
    for i in range(3):
        blit(img, pollution.flood_frame(0), int(OX + (i - 1) * 32 - 32), int(OY + (i + 1) * 16))
    blit(img, load_png(os.path.join(ROOT, "fx/flood/edge_s.png")), int(OX + (1 - 0) * 32 - 32), int(OY + (1 + 0) * 16))
    blit(img, load_png(os.path.join(ROOT, "fx/flood/edge_n.png")), int(OX + (1 - 2) * 32 - 32), int(OY + (1 + 2) * 16))
    draw_city(img, False)
    return img


def fire_composite():
    """Four 1-cell box buildings, each with a fire stage (frame 0) on top."""
    Wc, Hc = 4 * 72 + 8, 120
    img = canvas(Wc, Hc)
    img[..., :3] = (96, 128, 84)
    img[..., 3] = 255
    for k in range(4):
        cx, by = 40 + k * 72, Hc - 20
        fill = canvas(Wc, Hc)
        # box centred on cx: footprint diamond 64x32 -> X,Y in [0.0,1.0]
        top = (by - 16)
        left = [(cx - 28, by - 14), (cx, by), (cx, by - 28), (cx - 28, by - 42)]
        # ground diamond
        for y in range(32):
            for x in range(64):
                if DIAMOND[y, x]:
                    px(img, cx - 32 + x, by - 16 + y, tone(GRASS, 0.4, x, y))
        fill_quad(img, [(cx - 28, by - 14 + 0), (cx, by - 0), (cx, by - 28), (cx - 28, by - 42 + 0)], (200, 168, 120))
        fill_quad(img, [(cx, by), (cx + 28, by - 14), (cx + 28, by - 42), (cx, by - 28)], (143, 116, 82))
        fill_quad(img, [(cx - 28, by - 42), (cx, by - 28), (cx + 28, by - 42), (cx, by - 56)], (216, 196, 152))
        window_px(img, cx - 16, by - 20, 4, 4, "L", (62, 90, 120))
        window_px(img, cx + 10, by - 26, 4, 4, "R", (62, 90, 120))
        fr = load_png(os.path.join(ROOT, "fx/fire/stage%d_0.png" % (k + 1)))
        blit(img, fr, cx - fr.shape[1] // 2, by - fr.shape[0] + 1)
    return img


def weather_composite():
    img = new_scene((100, 118, 130))
    draw_city(img, False)
    rs = np.random.RandomState(8)
    for (X, Y, n) in ((0.4, 1.3, "m"), (1.1, 1.62, "s"), (2.3, 1.45, "l")):
        p = load_png(os.path.join(ROOT, "fx/weather/puddle_%s.png" % n))
        x, y = iso(X, Y)
        blit(img, p, int(x - p.shape[1] / 2), int(y - p.shape[0] / 2))
    for _ in range(46):
        n = ("s", "m", "l")[rs.randint(0, 3)]
        p = load_png(os.path.join(ROOT, "fx/weather/rain_%s.png" % n))
        blit(img, p, int(rs.randint(6, W - 10)), int(rs.randint(2, H - 20)))
    for _ in range(10):
        p = load_png(os.path.join(ROOT, "fx/weather/rain_splash_%d.png" % rs.randint(0, 4)))
        blit(img, p, int(rs.randint(50, 160)), int(rs.randint(90, 150)))
    return img


ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "design", "iso", "life"))


def build(root):
    global ROOT
    ROOT = root
    items = []
    save_still(root, "fx/composites/night.png", night_composite(), items, "night: lamps, windows, headlights")
    save_still(root, "fx/composites/fog.png", fog_composite(), items, "fog 3x3")
    save_still(root, "fx/composites/smog.png", smog_composite(), items, "smog 3 levels")
    save_still(root, "fx/composites/flood.png", flood_composite(), items, "flood 3x3")
    save_still(root, "fx/composites/fire_stages.png", fire_composite(), items, "fire stages 1-4")
    save_still(root, "fx/composites/rain.png", weather_composite(), items, "rain, puddles")
    return items


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", "design", "iso", "life"))
    it = build(root)
    sc = os.path.normpath(os.path.join(here, "..", "..", "..", "scratch_life", "fx"))
    for i in it:
        prev(sc, "c_" + os.path.basename(i["file"]), load_png(os.path.join(root, i["file"])), 3, (0, 0, 0))
    print(len(it))
