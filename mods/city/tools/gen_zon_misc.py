"""gen_zon_misc.py - construction sites, rubble, zone overlays (+ signature hookup) for gen_zon.py."""
import os
import random

import genworld as G
from genworld import H
from pngkit import Canvas, save_sheet, shade, mix, with_alpha
from gen_zon_base import layers, finish, shadow, Lot

ZONE_COLS = ["7ED957", "2E9E3E", "5AB4F0", "2C6FD1", "F2C94C", "B07CE8",
             "A8E063", "4FBF4F", "8FD3C8", "2F6F3F", "7A4FC4", "C9A227"]


def dims(fp):
    cx, cy = fp.split("x")
    return int(cx), int(cy)


# =========================================================================== construction
def construction(cx, cy, t):
    L = Lot("construction", cx, cy, 1, 1, 0)
    g, b = layers(L.W, L.H)
    fw, fd, y0, yB = L.fw, L.fd, L.y0, L.yB
    g.rect(0, y0, fw, fd, H("B79F76"))
    g.noise(0.08, seed=cx * 10 + cy, x=0, y=y0, w=fw, h=fd)
    g.rect_outline(0, y0, fw, fd, H("8E7650"))
    for i in range(fw // 8):  # barrier
        g.rect(2 + i * 8, yB - 4, 4, 2, H("E8E8E8") if i % 2 else H("E04A2B"))
    r = random.Random(f"cons{cx}x{cy}")
    bw = fw - 12 if fw > 32 else 18
    bx = fw - bw - 4 if fw > 32 else 8
    bd = min(fd - 14, 12 + 12 * (cy - 1))
    yb = yB - 8
    hh = min(8 + 6 * min(cx, cy) + (t % 2) * 2, yb - bd - 12)
    shadow(g, L, bx, yb, bw, bd, hh + 6)
    rr = G.box3d(b, bx, yb, bw, bd, hh, H("A8ADB4"), H("C4C8CE"))
    for xx in range(rr[0] + 3, rr[0] + rr[2] - 2, 5):  # rebar starts on the slab
        for yy in range(rr[1] + 2, rr[1] + rr[3] - 1, 4):
            b.set(xx, yy, H("8A5A3C"))
    sh = hh + bd // 2
    G.scaffold(b, bx - 2, yb - sh, bw + 3, sh, t)
    # material piles
    g.rect(3, y0 + 4, 6, 3, H("C27B54"))
    g.rect(3, y0 + 4, 6, 1, H("DDA27C"))
    if fw > 32 or fd > 32:
        g.ellipse(fw - 10, y0 + 8, 5, 3, H("D8C8A0"))
        g.rect(6, yB - 10, 8, 3, H("7B5B3A"))
        g.rect(6, yB - 10, 8, 1, H("9C7A55"))
    # crane: mast on the left, jib swings over the site
    mx = 7
    mh = yB - 6 - 10
    jib = min(fw - mx - 2, 44)
    G.crane(b, mx, yB - 6, mh, jib, t, big=(fw >= 64))
    return finish(b, g)


# =========================================================================== rubble
def rubble(cx, cy):
    L = Lot("rubble", cx, cy, 1, 1, 0)
    g, b = layers(L.W, L.H)
    fw, fd, y0, yB = L.fw, L.fd, L.y0, L.yB
    r = random.Random(f"rubble{cx}x{cy}")
    g.rect(0, y0, fw, fd, H("8F877A"))
    g.noise(0.10, seed=cx * 7 + cy, x=0, y=y0, w=fw, h=fd)
    g.rect(0, y0, fw, 1, H("6E675C"))
    for _ in range(fw * fd // 60):  # dust + scattered fragments
        x, y = r.randrange(fw), y0 + r.randrange(fd)
        g.set(x, y, r.choice([H("A59C8E"), H("6E675C"), H("B5A898")]))
    # mound(s)
    n = 1 if cx * cy <= 3 else (2 if cx * cy <= 8 else 3)
    mounds = []
    for i in range(n):
        mx = fw * (i + 0.5) / n + r.uniform(-2, 2)
        my = yB - fd * 0.45 + (fd * 0.12 if i % 2 else 0)
        rx = min(fw / n * 0.5, fw * 0.45) + 2
        ry = min(fd * 0.36, rx * 0.62)
        mounds.append((mx, my, rx, ry))
        g.ellipse(mx + 3, my + 3, rx, ry, (20, 18, 15, 80))
    for (mx, my, rx, ry) in mounds:
        hgt = int(min(4 + rx * 0.45, 20))
        jx = [r.uniform(-1.5, 1.5) for _ in range(hgt)]
        for k in range(hgt):  # stacked shrinking, jittered ellipses = heap with volume
            t = k / max(1, hgt - 1)
            col = mix(H("72675A"), H("ADA18F"), t)
            b.ellipse(mx - t * 2 + jx[k], my - k, rx * (1 - 0.55 * t), ry * (1 - 0.5 * t), col)
        # chunks: concrete slabs, bricks, rebar
        for _ in range(int(rx * ry / 6)):
            a = r.uniform(0, 6.283)
            d = r.random() ** 0.7
            px = mx + rx * 0.9 * d * __import__("math").cos(a)
            py = my - (1 - d) * hgt + ry * 0.9 * d * __import__("math").sin(a)
            k = r.random()
            if k < 0.45:
                w, h = r.randrange(2, 5), r.randrange(1, 3)
                b.rect(px, py, w, h, r.choice([H("B9B6AE"), H("A3A099"), H("CFCBC2")]))
                b.rect(px, py + h, w, 1, (30, 26, 22, 120))
            elif k < 0.8:
                b.rect(px, py, 2, 1, r.choice([H("A0523C"), H("8A4636"), H("B8664A")]))
            else:
                ex, ey = px + r.randrange(-4, 5), py - r.randrange(2, 6)
                b.line(px, py, ex, ey, H("5A3A28"))
    # broken wall stub with a jagged top at the back-left of the pad
    wx, wyb = 3, int(y0 + fd * 0.45)
    ww = min(14, fw // 3)
    for i in range(ww):
        wh = 5 + int(6 * abs(((i * 7) % 11) / 11 - 0.5) * 2) + (i < ww // 2) * 4
        b.rect(wx + i, wyb - wh, 1, wh, shade(H("B3AA9A"), 1.0 - 0.15 * (i % 3 == 0)))
    b.rect(wx, wyb - 1, ww, 1, H("7A7064"))
    b.line(wx + 2, wyb - 9, wx + 1, wyb - 13, H("5A3A28"))
    b.line(wx + 6, wyb - 7, wx + 7, wyb - 11, H("5A3A28"))
    return finish(b, g)


# =========================================================================== overlays
def pattern(i, x, y):
    if i <= 6:
        return (x + y) % 8 == 0
    return {7: y % 4 == 0, 8: x % 4 == 1 and y % 4 == 1, 9: (x // 2 + y // 2) % 2 == 0 and (x % 8 < 4) == (y % 8 < 4),
            10: x % 4 == 0, 11: (x + y) % 6 == 0 or (x - y) % 6 == 0, 12: y % 5 == 0 and x % 6 < 3}[i]


def overlay_zone():
    out = [Canvas(32, 32)]
    for i, hx in enumerate(ZONE_COLS, 1):
        col = H(hx)
        c = Canvas(32, 32, with_alpha(col, 84))
        for y in range(32):
            for x in range(32):
                if pattern(i, x, y):
                    c.blend(x, y, with_alpha(col if i <= 6 else shade(col, 1.35), 40 if i <= 6 else 70))
        c.rect_outline(0, 0, 32, 32, with_alpha(col, 215))
        c.rect_outline(1, 1, 30, 30, with_alpha(shade(col, 1.2), 70))
        out.append(c)
    return out


def overlay_dim():
    out = [Canvas(32, 32)]
    for i, hx in enumerate(ZONE_COLS, 1):
        col = H(hx)
        c = Canvas(32, 32, with_alpha(col, 22))
        for y in range(32):
            for x in range(32):
                if (x - y) % 6 in (0, 1):
                    c.blend(x, y, with_alpha(col, 70))
        for k in range(0, 32, 4):  # dashed border
            for (x, y) in ((k, 0), (k + 1, 0), (k, 31), (k + 1, 31), (0, k), (0, k + 1), (31, k), (31, k + 1)):
                c.blend(x, y, with_alpha(col, 150))
        out.append(c)
    return out


# =========================================================================== driver hooks
def _sigs():
    try:
        import gen_zon_sig as S
        return S
    except ImportError:
        return None


def jobs(footprints):
    out = [("construction", fp) for fp in footprints] + [("rubble", fp) for fp in footprints] + [("overlays", "")]
    S = _sigs()
    if S:
        out += [("sig", n) for n in S.SIGS]
    return out


def job(kind, arg, zon):
    if kind == "construction":
        cx, cy = dims(arg)
        save_sheet(os.path.join(zon, f"construction-{arg}.png"), [construction(cx, cy, t) for t in range(4)])
    elif kind == "rubble":
        cx, cy = dims(arg)
        save_sheet(os.path.join(zon, f"rubble-{arg}.png"), [rubble(cx, cy)])
    elif kind == "overlays":
        save_sheet(os.path.join(zon, "overlay-zone.png"), overlay_zone())
        save_sheet(os.path.join(zon, "overlay-dim.png"), overlay_dim())
    elif kind == "sig":
        S = _sigs()
        save_sheet(os.path.join(zon, f"sig-{arg}.png"), [S.SIGS[arg][1]()])
    return f"{kind}-{arg}"


def yaml(footprints):
    t = "zon-construction:\n"
    for fp in footprints:
        t += f"\ts{fp}:\n\t\tFilename: zon/construction-{fp}.png\n\t\tLength: *\n\t\tTick: 200\n\t\tOffset: 0,-16\n"
    t += "\nzon-rubble:\n"
    for fp in footprints:
        t += f"\ts{fp}:\n\t\tFilename: zon/rubble-{fp}.png\n\t\tLength: 1\n\t\tOffset: 0,-16\n"
    t += ("\nzon-overlays:\n\tzone:\n\t\tFilename: zon/overlay-zone.png\n\t\tLength: 13\n"
          "\tdim:\n\t\tFilename: zon/overlay-dim.png\n\t\tLength: 13\n\n")
    S = _sigs()
    if S:
        t += "# Signature buildings: one 3x3 frame each, Offset 0,-(16*HR).\n"
        for n, (hr, fn) in S.SIGS.items():
            t += f"sig-{n}:\n\tidle:\n\t\tFilename: zon/sig-{n}.png\n\t\tLength: 1\n\t\tOffset: 0,-{16 * hr}\n\n"
    return t


def contact(path, zoom=2):
    from gen_zon import FOOTPRINTS
    rows = [[construction(*dims(fp), t) for t in range(4)] + [rubble(*dims(fp))] for fp in FOOTPRINTS[:6]]
    rows += [[construction(*dims(fp), 0), rubble(*dims(fp))] for fp in FOOTPRINTS[6:]]
    rows.append([c.scale(2) for c in overlay_zone()])
    rows.append([c.scale(2) for c in overlay_dim()])
    S = _sigs()
    if S:
        rows.append([fn() for (hr, fn) in S.SIGS.values()])
    W = max(sum(c.w * zoom + 4 for c in row) for row in rows) + 4
    Hh = sum(max(c.h for c in row) * zoom + 8 for row in rows) + 4
    sheet = Canvas(W, Hh, (104, 148, 78, 255))
    y = 4
    for row in rows:
        x = 4
        for c in row:
            sheet.blit(c.scale(zoom), x, y)
            x += c.w * zoom + 4
        y += max(c.h for c in row) * zoom + 8
    sheet.save(path)
