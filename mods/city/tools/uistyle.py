"""Dune 2000 look: bevel/metal post-processing for glyphs, procedural marble/leather, bronze frames."""

import math
from uidraw import G, col
from uitheme import *  # noqa: F401,F403

# ---- noise --------------------------------------------------------------------------


def _h(ix, iy, seed):
    n = (ix * 374761393 + iy * 668265263 + seed * 144665) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    return ((n ^ (n >> 16)) & 0xffff) / 65535.0


def vnoise(x, y, seed=0):
    ix, iy = int(math.floor(x)), int(math.floor(y))
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = _h(ix, iy, seed), _h(ix + 1, iy, seed)
    c, d = _h(ix, iy + 1, seed), _h(ix + 1, iy + 1, seed)
    return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy


def fbm(x, y, seed=0, octaves=3):
    v, amp, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        v += vnoise(x, y, seed + o * 17) * amp
        tot += amp
        x, y, amp = x * 2.03, y * 2.03, amp * 0.5
    return v / tot


def lerpc(a, b, t):
    t = 0.0 if t < 0 else 1.0 if t > 1 else t
    a = tuple(a) + (255,) * (4 - len(a))
    b = tuple(b) + (255,) * (4 - len(b))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def shadec(c, f):
    return (max(0, min(255, int(c[0] * f))), max(0, min(255, int(c[1] * f))), max(0, min(255, int(c[2] * f))), c[3] if len(c) > 3 else 255)


# ---- textures ---------------------------------------------------------------------------

def marble(base, vein, seed=1, scale=0.09, contrast=1.0):
    """Red-marble style fill function f(x, y) -> colour (design coordinates)."""
    def f(x, y):
        n = fbm(x * scale, y * scale, seed, 3)
        v = abs(fbm(x * scale * 1.7 + 9, y * scale * 1.7 + 4, seed + 5, 2) - 0.5) * 2   # vein ridges
        t = (n - 0.5) * contrast + 0.5
        c = lerpc(base, shadec(base, 1.5), t)
        veins = max(0.0, 1 - v * 9) * 0.28 * contrast
        c = lerpc(c, vein, veins)
        return (c[0], c[1], c[2], base[3] if len(base) > 3 else 255)
    return f


def leather(base, seed=3, grain=0.07):
    def f(x, y):
        n = fbm(x * 0.35, y * 0.35, seed, 2) - 0.5
        g = _h(int(x * 2), int(y * 2), seed) - 0.5
        k = 1 + n * 0.22 + g * grain
        return shadec(base, k)
    return f


# ---- frames ----------------------------------------------------------------------------------

def metal_frame(w, h, light=BRONZE_LIGHT, mid=BRONZE, dark=BRONZE_DARK, invert=False):
    """Stroke colour function: bronze lit from the top-left, with a soft sheen band."""
    def f(x, y):
        t = (x / w + y / h) / 2.0
        if invert:
            t = 1 - t
        sheen = math.exp(-((t - 0.18) / 0.12) ** 2) * 0.35
        c = lerpc(light, mid, t * 1.6) if t < 0.5 else lerpc(mid, dark, (t - 0.5) * 2)
        c = lerpc(c, (255, 236, 190, 255), sheen)
        return c
    return f


def framed_panel(g, r, fill, thickness=3.0, invert=False, outline=OUTLINE, frame=None):
    """Dark outline + bronze bevelled frame + inner shadow + textured fill."""
    w, h = g.dw, g.dh
    g.rrect(0, 0, w, h, r, fill=outline)
    g.rrect(0.8, 0.8, w - 1.6, h - 1.6, max(0.5, r - 0.8), fill=fill,
            stroke=metal_frame(w, h, *(frame or (BRONZE_LIGHT, BRONZE, BRONZE_DARK)), invert=invert), sw=thickness)
    t = 0.8 + thickness
    g.rrect(t, t, w - 2 * t, h - 2 * t, max(0.5, r - t), stroke=(0, 0, 0, 150), sw=1.0)
    g.rrect(t + 1, t + 1, w - 2 * t - 2, h - 2 * t - 2, max(0.5, r - t - 1), stroke=(255, 190, 120, 22), sw=1.0)


# ---- glyph post-process --------------------------------------------------------------------------

def _blur(A, w, h, r):
    if r < 1:
        return A
    out = [0.0] * (w * h)
    n = 2 * r + 1
    for y in range(h):                       # horizontal
        row = A[y * w:(y + 1) * w]
        acc = sum(row[:r]) if r <= w else 0.0
        for x in range(w):
            if x + r < w:
                acc += row[x + r]
            if x - r - 1 >= 0:
                acc -= row[x - r - 1]
            out[y * w + x] = acc / n
    res = [0.0] * (w * h)
    for x in range(w):                       # vertical
        acc = sum(out[yy * w + x] for yy in range(min(r, h)))
        for y in range(h):
            if y + r < h:
                acc += out[(y + r) * w + x]
            if y - r - 1 >= 0:
                acc -= out[(y - r - 1) * w + x]
            res[y * w + x] = acc / n
    return res


def stylize(g, outline=1.0, bevel=1.3, strength=1.0, vgrad=(1.14, 0.80), outline_color=OUTLINE, shadow=True):
    """Turn flat coloured shapes into beveled, vertically shaded metal with a dark outline."""
    cv = g.cv
    w, h, s = cv.w, cv.h, g.s
    A = [p[3] / 255.0 for p in cv.px]
    r = max(1, int(round(bevel * s)))
    H = _blur(_blur(A, w, h, r), w, h, max(1, r // 2 + 1))
    k = strength * r * 1.7
    pad = int(math.ceil(outline * s)) + (2 if shadow else 0)
    ro = max(1, int(round(outline * s)))
    offs = [] if outline <= 0 else [(dx, dy) for dy in range(-ro, ro + 1) for dx in range(-ro, ro + 1) if dx * dx + dy * dy <= ro * ro + 0.5]
    out = [[0, 0, 0, 0] for _ in range(w * h)]
    for y in range(h):
        for x in range(w):
            i = y * w + x
            a = A[i]
            # outline / shadow underneath
            m = 0.0
            if a < 1.0:
                for dx, dy in offs:
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < w and 0 <= yy < h:
                        v = A[yy * w + xx]
                        if v > m:
                            m = v
                m *= (1 - a)
            if m > 0.01:
                out[i] = [outline_color[0], outline_color[1], outline_color[2], int(255 * m * 0.95)]
            if a <= 0.004:
                continue
            p = cv.px[i]
            gx = H[i + 1 if x + 1 < w else i] - H[i - 1 if x > 0 else i]
            gy = H[i + w if y + 1 < h else i] - H[i - w if y > 0 else i]
            sh = (gx + gy) * k
            sh = max(-0.55, min(0.55, sh))
            f = (vgrad[0] + (vgrad[1] - vgrad[0]) * (y / max(1, h - 1))) * (1 + sh)
            c = [min(255, max(0, int(p[j] * f))) for j in range(3)]
            if sh > 0.2:
                t = min(1.0, (sh - 0.2) * 1.8)
                c = [int(c[j] + (255 - c[j]) * t * 0.45) for j in range(3)]
            # composite glyph over the outline pixel
            o = out[i]
            oa = o[3] / 255.0
            ga = a
            ra = ga + oa * (1 - ga)
            if ra <= 0:
                continue
            out[i] = [int((c[j] * ga + o[j] * oa * (1 - ga)) / ra) for j in range(3)] + [int(ra * 255)]
    cv.px = out


def metal_tint(g, c_light=(255, 232, 170), c_dark=(190, 112, 44)):
    """Map any glyph colours to a vertical gold gradient (keeps alpha). Used for the wordmark."""
    cv = g.cv
    for y in range(cv.h):
        t = y / max(1, cv.h - 1)
        c = [int(c_light[j] + (c_dark[j] - c_light[j]) * t) for j in range(3)]
        for x in range(cv.w):
            p = cv.px[y * cv.w + x]
            if p[3]:
                p[0], p[1], p[2] = c
