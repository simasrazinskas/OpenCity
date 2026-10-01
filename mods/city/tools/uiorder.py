"""D2k-style 34x35 order tiles (city-order-icons): notched beveled tile + tan glyph, 3 states."""

import math
from uidraw import G
from uitheme import *  # noqa: F401,F403
from uistyle import fbm, lerpc, stylize
import uicityicons as ci
import uicityicons2 as ci2

W_, H_ = 34, 35
# tile outline, chamfered top-left and a notch at the top-right, like the D2k order tiles
TILE = [(1, 12), (11, 1.5), (33, 1.5), (30.2, 5.5), (30.2, 34), (1, 34)]


def area(p):
    return sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p))) / 2


def inset(p, d):
    """Offset a simple polygon inward by d (miter joins)."""
    n = len(p)
    sgn = 1 if area(p) > 0 else -1
    lines = []
    for i in range(n):
        (x0, y0), (x1, y1) = p[i], p[(i + 1) % n]
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy)
        nx, ny = -dy / ln * sgn, dx / ln * sgn          # inward normal (screen coords)
        # (the sign convention: for positive area in y-down coords the interior is on the right)
        lines.append(((x0 + nx * d, y0 + ny * d), (dx, dy), (nx, ny)))
    out = []
    for i in range(n):
        (a, da, _), (b, db, _) = lines[i - 1], lines[i]
        den = da[0] * db[1] - da[1] * db[0]
        if abs(den) < 1e-9:
            out.append(b)
            continue
        t = ((b[0] - a[0]) * db[1] - (b[1] - a[1]) * db[0]) / den
        out.append((a[0] + da[0] * t, a[1] + da[1] * t))
    return out, [l[2] for l in lines]


BODY = {
    'bulldoze': ((156, 52, 22), (210, 96, 40)),
    'infoviews': ((22, 62, 158), (60, 120, 230)),
    'budget': ((18, 110, 30), (50, 190, 60)),
    'stats': ((12, 82, 96), (36, 150, 168)),            # blue-teal
    'chirper': ((10, 112, 146), (48, 196, 230)),        # cyan
    'production': ((92, 80, 22), (162, 140, 52)),       # olive / brown
    'policies': ((78, 28, 128), (144, 72, 214)),        # purple
    'progression': ((72, 104, 14), (176, 182, 40)),     # green-gold
    'districts': ((178, 96, 8), (246, 160, 40)),        # orange
    'tiles': ((96, 62, 32), (164, 116, 66)),            # earthy brown
    'transit': ((18, 56, 150), (56, 122, 224)),         # blue
}


def mono(g, light=(244, 206, 158), dark=(110, 70, 44)):
    """Map glyph colours to a tan monochrome by luminance (keeps alpha)."""
    for p in g.cv.px:
        if p[3]:
            lum = (0.3 * p[0] + 0.59 * p[1] + 0.11 * p[2]) / 255.0
            lum = min(1.0, lum * 1.15)
            p[0], p[1], p[2] = lerpc(dark, light, lum)[:3]


def glyph_bulldoze(g):
    g.rrect(8, 21, 22, 7.5, 3.75, fill=WHITE)             # tracks
    for cx in (12.5, 19, 25.5):
        g.circle(cx, 24.75, 1.7, SLATE)
    g.poly([(10, 21), (10, 15), (22, 15), (22, 21)], SAND)   # engine hood
    g.rrect(18, 7, 11, 11, 1.8, fill=WHITE)               # cab (rear)
    g.rect(20.5, 9.5, 6, 5, SLATE)
    g.line(11, 17.5, 5.5, 14, 2.6, GREY)                  # lift arm
    g.poly([(1, 7), (7.5, 9), (7.5, 27), (1, 28)], WHITE)  # blade
    g.rect(1, 25.5, 6.5, 2, GREY)


def glyph_layers(g):
    ci.i_infoviews(g)


def glyph_budget(g):
    for i, h in enumerate((8, 13, 18)):
        g.rrect(2 + i * 6, 29 - h, 4.6, h, 1, fill=SAND)
    g.circle(22, 13, 9.5, WHITE)
    g.ring(22, 13, 7.3, 1.2, GREY)
    from uiglyphs import dollar
    dollar(g, 22, 13, 4.6, SLATE)
    g.line(2, 12, 11, 8, 1.6, SAND)


def glyph_districts(g):
    """Map split in blocks; blocks inset so the outline draws dark gaps and it survives the monochrome tint."""
    for pts, c in zip((ci2.DISTRICT_A, ci2.DISTRICT_B, ci2.DISTRICT_C), (WHITE, GREY, SAND)):
        g.poly(inset(pts, 1.1)[0], c)
    g.circle(22.5, 22, 2.2, SLATE)
    g.circle(7, 9, 1.8, SLATE)


GLYPHS = {'bulldoze': glyph_bulldoze, 'infoviews': glyph_layers, 'budget': glyph_budget,
          'stats': ci2.i_stats, 'chirper': ci2.i_chirper, 'production': ci2.i_production,
          'policies': ci2.i_policies, 'progression': ci2.i_progression, 'districts': glyph_districts,
          'tiles': ci2.i_tiles, 'transit': ci2.i_transit}


def body_fill(kind, state):
    dark, light = BODY[kind]
    if state == 'disabled':
        lum = int(0.3 * dark[0] + 0.59 * dark[1] + 0.11 * dark[2])
        dark = light = (lum + 18, lum + 16, lum + 14)
        dark = tuple(int(c * 0.7) for c in dark)
    base = lerpc(dark, light, 0.75 if state == 'active' else 0.0)
    return base


def draw_tile(kind, state):
    def draw(g):
        s = g.s
        frame_light, frame_dark = ((255, 236, 196), (170, 110, 90)) if state == 'active' else \
            ((206, 160, 130), (120, 80, 64)) if state == 'disabled' else ((232, 184, 148), (140, 92, 72))
        outline = (52, 8, 10, 255)
        g.poly(TILE, outline)
        fp, normals = inset(TILE, 0.9)
        g.poly(fp, lerpc(frame_light, frame_dark, 0.45) + (255,))
        # bevel: lit edges (facing up/left) light, others dark
        for i in range(len(fp)):
            a, b = fp[i], fp[(i + 1) % len(fp)]
            nx, ny = normals[i]
            lit = (-nx - ny)    # inward normal pointing right/down means edge faces up/left
            g.line(a[0], a[1], b[0], b[1], 1.1, (frame_light if lit > 0.1 else frame_dark) + (255,), cap=False)
        bp, _ = inset(TILE, 2.4)
        base = body_fill(kind, state)
        body = G(W_, H_, s)
        body.poly(bp, base + (255,))
        # marbled texture + soft top-left light
        for yy in range(body.cv.h):
            for xx in range(body.cv.w):
                p = body.cv.px[yy * body.cv.w + xx]
                if p[3]:
                    n = (fbm(xx / s * 0.45, yy / s * 0.45, 7, 2) - 0.5) * 0.5 + (_h(xx, yy) - 0.5) * 0.08
                    k = 1 + n + (0.12 - 0.22 * (xx / s / W_ + yy / s / H_) / 2 * 2)
                    for j in range(3):
                        p[j] = max(0, min(255, int(p[j] * k)))
        g.blit(body, 0, 0)
        # glyph
        gs = 0.66 if state != 'disabled' else 0.62
        sub = G(32, 32, s * gs)
        GLYPHS[kind](sub)
        if state == 'disabled':
            mono(sub, (170, 160, 150), (80, 74, 70))
        else:
            mono(sub, (250, 214, 168) if state == 'active' else (232, 192, 146), (120, 76, 48))
        stylize(sub, outline=1.2, bevel=1.4, outline_color=(40, 12, 8, 255) if state != 'active' else (40, 12, 8, 255))
        gx = 15.5 - 16 * gs
        gy = 19.5 - 16 * gs
        g.blit(sub, gx, gy)
        if state == 'active':
            # glow: bright rim just inside the frame
            g.poly(inset(TILE, 2.4)[0], (255, 230, 150, 0))
            for i in range(len(bp)):
                a, b = bp[i], bp[(i + 1) % len(bp)]
                g.line(a[0], a[1], b[0], b[1], 1.0, (255, 236, 160, 120), cap=False)
    return draw


def _h(x, y):
    n = (x * 374761393 + y * 668265263) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    return ((n ^ (n >> 16)) & 0xffff) / 65535.0


ORDER_KINDS = ['bulldoze', 'infoviews', 'budget',
               'stats', 'chirper', 'production', 'policies', 'progression', 'districts', 'tiles', 'transit']
ORDER_STATES = [('', 'normal'), ('-disabled', 'disabled'), ('-active', 'active')]
