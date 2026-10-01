"""More 32x32 city HUD icons (same style as uicityicons: flat shapes, stylize() adds bevel + outline).

Roads / road tools, utilities, extra zones, industry hubs, transit, service tabs, panel/stat icons
and misc UI glyphs. Appended to uicityicons.ICONS as ICONS2.
"""

import math
from uiglyphs import *  # noqa: F401,F403

W, TL = WHITE, TEAL
ASPH = (108, 98, 92, 255)
ASPH_DARK = (70, 64, 62, 255)
GRAVEL = (186, 142, 88, 255)
DIRT = (122, 84, 46, 255)
WOOD = (184, 122, 62, 255)
BROWN = (132, 88, 46, 255)
CYAN = (84, 200, 226, 255)
LEAF = (98, 176, 64, 255)


# ---- helpers ----------------------------------------------------------------

def persp(tw, bw, y0=3.0, y1=29.0, cx=16.0):
    """Perspective road mapping: (u across 0..1, y) -> point."""
    def P(u, y):
        w = tw + (bw - tw) * (y - y0) / (y1 - y0)
        return (cx - w / 2 + u * w, y)
    return P


def band(g, P, u0, u1, ya, yb, c):
    g.poly([P(u0, ya), P(u1, ya), P(u1, yb), P(u0, yb)], c)


def dashes(g, P, u, hw, c, segs=((4, 7.5), (10.5, 15), (18, 23.5), (26.5, 29))):
    for ya, yb in segs:
        band(g, P, u - hw, u + hw, ya, yb, c)


def hroad(g, y, h, c=ASPH, dash=W):
    g.rrect(1.5, y, 29, h, 1.2, fill=c)
    if dash:
        for x in (3, 10.5, 18, 25.5):
            g.rect(x, y + h / 2 - 0.8, 4, 1.6, dash)


def head(g, x, y, dx, dy, ln, hw, c):
    """Arrow head with its tip at (x,y) pointing along (dx,dy)."""
    d = math.hypot(dx, dy)
    dx, dy = dx / d, dy / d
    bx, by = x - dx * ln, y - dy * ln
    g.poly([(x, y), (bx - dy * hw, by + dx * hw), (bx + dy * hw, by - dx * hw)], c)


def arc_arrow(g, cx, cy, r, a0, a1, w, c, ln=4.2, hw=3.4):
    g.arc(cx, cy, r, a0, a1, w, c)
    a = math.radians(a1)
    sgn = 1 if a1 > a0 else -1
    tx, ty = -math.sin(a) * sgn, math.cos(a) * sgn
    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
    head(g, x + tx * ln * 0.75, y + ty * ln * 0.75, tx, ty, ln, hw, c)


def pylon(g, cx, by, k, c=W, brace=GREY):
    top = by - 23 * k
    g.line(cx - 5 * k, by, cx - 1.6 * k, top, 2.1 * k, c)
    g.line(cx + 5 * k, by, cx + 1.6 * k, top, 2.1 * k, c)
    for t0, t1 in ((0.0, 0.3), (0.3, 0.58)):
        ya, yb = by - t0 * 23 * k, by - t1 * 23 * k
        wa, wb = 5 - 3.4 * t0, 5 - 3.4 * t1
        g.line(cx - wa * k, ya, cx + wb * k, yb, 1.1 * k, brace)
        g.line(cx + wa * k, ya, cx - wb * k, yb, 1.1 * k, brace)
    g.rrect(cx - 11 * k, top + 3 * k, 22 * k, 2.2 * k, 0.6 * k, fill=c)
    g.rrect(cx - 7.5 * k, top + 9 * k, 15 * k, 2 * k, 0.6 * k, fill=c)
    for x, y in ((-10, 5.2), (10, 5.2), (-6.5, 11)):
        g.rect(cx + (x - 0.8) * k, top + y * k, 1.6 * k, 2.2 * k, TL)


def bus_side(g, x, y, w, h, body=W, win=SLATE, wheel=TL):
    g.rrect(x, y, w, h, 2.2, fill=body)
    n = max(2, int((w - 6) / 5))
    ww = (w - 7) / n
    for i in range(n):
        g.rect(x + 2 + i * ww + 0.6, y + 2.2, ww - 1.4, h * 0.36, win)
    g.rect(x + w - 4, y + 2.2, 2.4, h * 0.36, win)
    g.circle(x + w * 0.24, y + h, h * 0.24, wheel)
    g.circle(x + w * 0.78, y + h, h * 0.24, wheel)


def bus_front(g, cx, y, w, h, body=W):
    g.rrect(cx - w / 2, y, w, h, 2.6, fill=body)
    g.rect(cx - w / 2 + 2, y + 1.6, w - 4, h * 0.13, TL)                 # destination sign
    g.rrect(cx - w / 2 + 2, y + h * 0.24, w - 4, h * 0.38, 1.2, fill=SLATE)  # windscreen
    g.circle(cx - w / 2 + 3.4, y + h * 0.78, h * 0.08, AMBER)
    g.circle(cx + w / 2 - 3.4, y + h * 0.78, h * 0.08, AMBER)
    g.rect(cx - w / 2 + 1.5, y + h - 0.2, 3.6, 3.2, GREY_DIM)
    g.rect(cx + w / 2 - 5.1, y + h - 0.2, 3.6, 3.2, GREY_DIM)


def dashed_rect(g, x, y, w, h, t, c, seg=4.0, gap=2.6):
    for (x0, y0, x1, y1) in ((x, y, x + w, y), (x, y + h, x + w, y + h)):
        p = x0
        while p < x1 - 0.5:
            g.rect(p, y0 - t / 2, min(seg, x1 - p), t, c)
            p += seg + gap
    for (x0, y0, x1, y1) in ((x, y, x, y + h), (x + w, y, x + w, y + h)):
        p = y0
        while p < y1 - 0.5:
            g.rect(x0 - t / 2, p, t, min(seg, y1 - p), c)
            p += seg + gap


def house(g, x, y, w, h, roof, wall, door=SLATE):
    g.poly([(x - 1, y + h * 0.42), (x + w / 2, y), (x + w + 1, y + h * 0.42)], roof)
    g.rect(x, y + h * 0.42, w, h * 0.58, wall)
    g.rect(x + w / 2 - w * 0.15, y + h * 0.66, w * 0.3, h * 0.34, door)


def lighten(c, t=0.3):
    return tuple(int(c[i] + (255 - c[i]) * t) for i in range(3)) + (255,)


# ---- roads ------------------------------------------------------------------

def i_road_street(g):
    P = persp(11, 28)
    band(g, P, 0, 1, 3, 29, SAND)
    band(g, P, 0.13, 0.87, 3, 29, ASPH)
    dashes(g, P, 0.5, 0.045, W)


def i_road_gravel(g):
    P = persp(8, 22)
    band(g, P, 0, 1, 3, 29, GRAVEL)
    for u, y, r in ((0.3, 8, 0.7), (0.7, 11, 0.8), (0.25, 15, 0.9), (0.62, 18, 1.0), (0.4, 22, 1.1),
                    (0.78, 24.5, 1.1), (0.2, 26.5, 1.2), (0.55, 27, 1.0), (0.5, 6, 0.6), (0.8, 15.5, 0.9)):
        x, _ = P(u, y)
        g.circle(x, y, r, DIRT)
    for u, y in ((0.45, 13), (0.3, 20), (0.7, 21)):
        x, _ = P(u, y)
        g.circle(x, y, 0.8, W)


def i_road_avenue(g):
    P = persp(15, 30)
    band(g, P, 0, 1, 3, 29, SAND)
    band(g, P, 0.08, 0.92, 3, 29, ASPH)
    dashes(g, P, 0.36, 0.03, W)
    dashes(g, P, 0.64, 0.03, W)


def i_road_boulevard(g):
    P = persp(15, 30)
    band(g, P, 0, 1, 3, 29, ASPH)
    band(g, P, 0.4, 0.6, 3, 29, LEAF)
    for y in (6.5, 13.5, 22.5):
        x, _ = P(0.5, y)
        r = 2.4 + 2.6 * (y - 3) / 26
        g.circle(x, y, r, GREEN)
        g.circle(x - r * 0.3, y - r * 0.3, r * 0.45, (170, 224, 110, 255))
    for u in (0.2, 0.8):
        dashes(g, P, u, 0.025, W, ((5, 8), (12, 16.5), (20, 25)))


def i_road_highway(g):
    P = persp(14, 25)
    band(g, P, 0, 1, 3, 29, ASPH_DARK)
    band(g, P, 0.47, 0.53, 3, 29, GREY)
    for u in (0.07, 0.93):
        band(g, P, u - 0.025, u + 0.025, 3, 29, W)
    for u in (-0.1, 1.1):
        a, b = P(u, 3), P(u, 29)
        g.line(a[0], a[1], b[0], b[1], 1.6, GREY)
        for y in (6, 12, 19, 27):
            x, _ = P(u, y)
            k = 0.6 + 0.6 * (y - 3) / 26
            g.rect(x - 0.6 * k, y, 1.3 * k, 2.4 * k, W)


# ---- road tools ---------------------------------------------------------------

def i_road_draw(g):
    g.line(4, 28, 17, 15, 8.5, ASPH)
    for t in (0.12, 0.45, 0.78):
        x, y = 4 + 13 * t, 28 - 13 * t
        g.line(x - 1.1, y + 1.1, x + 1.1, y - 1.1, 1.4, W)
    # pencil from tip (14.5,17.5) up to the top-right
    d = (1 / math.sqrt(2), -1 / math.sqrt(2))
    n = (-d[1], d[0])
    tip = (15, 17)

    def p(a, b):
        return (tip[0] + d[0] * a + n[0] * b, tip[1] + d[1] * a + n[1] * b)
    g.poly([p(0, 0), p(5.5, 2.8), p(5.5, -2.8)], SAND)
    g.poly([p(0, 0), p(1.9, 1.0), p(1.9, -1.0)], SLATE)
    g.poly([p(5.5, 2.8), p(17, 2.8), p(17, -2.8), p(5.5, -2.8)], AMBER)
    g.poly([p(5.5, 0.9), p(17, 0.9), p(17, -0.9), p(5.5, -0.9)], GOLD)
    g.poly([p(17, 2.8), p(19, 2.8), p(19, -2.8), p(17, -2.8)], GREY)
    g.poly([p(19, 2.8), p(21.5, 2.8), p(21.5, -2.8), p(19, -2.8)], (232, 120, 110, 255))


def i_road_oneway(g):
    hroad(g, 7.5, 17, dash=None)
    g.rect(4.5, 14.2, 14, 3.6, W)
    g.poly([(17, 9.5), (27.5, 16), (17, 22.5)], W)


def i_road_replace(g):
    hroad(g, 20, 9)
    arc_arrow(g, 16, 13, 8, 200, 330, 2.8, TL)
    arc_arrow(g, 16, 13, 8, 20, 150, 2.8, W)


def i_road_signal(g):
    g.rect(14.6, 23, 2.8, 6.5, GREY_DIM)
    g.rrect(10.5, 28, 11, 2.2, 0.8, fill=GREY_DIM)
    g.rrect(10, 2, 12, 22.5, 3, fill=GREY)
    for y, c in ((7.2, RED), (13.2, AMBER), (19.2, GREEN)):
        g.circle(16, y, 3.6, SLATE)
        g.circle(16, y, 2.7, c)


def i_road_roundabout(g):
    for x, y, w, h in ((12.5, 1, 7, 6), (12.5, 25, 7, 6), (1, 12.5, 6, 7), (25, 12.5, 6, 7)):
        g.rect(x, y, w, h, ASPH)
    g.ring(16, 16, 9.6, 6.6, ASPH)
    g.circle(16, 16, 5.2, LEAF)
    for a in (45, 135, 225, 315):
        arc_arrow(g, 16, 16, 9.6, a + 40, a - 4, 1.5, W, ln=3.2, hw=2.4)


def i_road_interchange(g):
    for cx, cy in ((9, 9), (23, 9), (9, 23), (23, 23)):
        g.ring(cx, cy, 5, 2.6, TL)
    g.rrect(1.5, 13, 29, 6, 1, fill=W)
    g.rrect(13, 1.5, 6, 29, 1, fill=W)
    g.rect(14.5, 14.5, 3, 3, SAND)


# ---- utilities ------------------------------------------------------------------

def i_power_line(g):
    pylon(g, 16, 29.5, 1.08)
    top = 29.5 - 23 * 1.08
    for x0, y0, x1 in ((16 - 10.8, top + 7.6, 1), (16 + 10.8, top + 7.6, 31)):
        mx = (x0 + x1) / 2
        g.polyline([(x0, y0), (mx, y0 + 2.2), (x1, y0 + 1.2)], 1.0, GREY)
    g.polyline([(16 - 7, top + 13.2), (8, top + 15), (1, top + 14.6)], 1.0, GREY)


def i_pipe(g):
    g.polyline([(2, 9), (13, 9), (13, 22), (30, 22)], 6.4, BLUE)
    g.line(3, 7.6, 11, 7.6, 1.4, (190, 222, 255, 255))
    for x, y, w, h in ((6, 4.6, 2.8, 8.8), (8.6, 15.2, 8.8, 2.8), (23, 17.6, 2.8, 8.8)):
        g.rrect(x, y, w, h, 0.8, fill=W)


def i_transformer(g):
    for x in (9.5, 16, 22.5):
        g.rect(x - 0.7, 7, 1.4, 6, GREY_DIM)
        for y in (3.5, 6.2, 8.9):
            g.rrect(x - 2.3, y, 4.6, 1.8, 0.8, fill=W)
    g.rrect(5, 12.5, 22, 16.5, 1.8, fill=GREY)
    for x in (2.5, 27):
        g.rect(x, 15, 2.5, 12, GREY_DIM)
    bolt(g, 16 - 8 * 0.86, 13.4, 0.86, AMBER)


def i_battery(g):
    g.rrect(12.5, 2.5, 7, 4, 1, fill=GREY)
    g.rrect(8.5, 5.5, 15, 24, 2.6, fill=W)
    g.rrect(10.8, 15.5, 10.4, 11.6, 1.2, fill=GREEN)
    cross(g, 16, 11, 3.4, 2.0, SLATE)


def i_sewage(g):
    g.rect(1.5, 5.5, 16, 10, GREY)
    g.rect(1.5, 7, 16, 1.4, (226, 196, 150, 255))
    g.ellipse(19, 10.5, 3.6, 7.0, W)
    g.ellipse(19.6, 10.5, 2.0, 4.8, SLATE)
    drop(g, 22, 21.5, 4.4, BROWN, SAND)
    g.ellipse(21, 29, 7.5, 1.6, BROWN)


def i_networks(g):
    g.polyline([(13, 27), (20, 27), (20, 17), (30, 17)], 5.2, BLUE)
    g.rrect(23.6, 13.2, 2.4, 7.6, 0.7, fill=W)
    pylon(g, 11, 29.5, 0.95)


ICONS2 = [
    ('road-street', i_road_street), ('road-gravel', i_road_gravel), ('road-avenue', i_road_avenue),
    ('road-boulevard', i_road_boulevard), ('road-highway', i_road_highway),
    ('road-draw', i_road_draw), ('road-oneway', i_road_oneway), ('road-replace', i_road_replace),
    ('road-signal', i_road_signal), ('road-roundabout', i_road_roundabout),
    ('road-interchange', i_road_interchange),
    ('power-line', i_power_line), ('pipe', i_pipe), ('transformer', i_transformer),
    ('battery', i_battery), ('sewage', i_sewage), ('networks', i_networks),
]


# ---- zones (zone_tile frame + building glyph, like uicityicons.i_res_low) ----------

def i_res_row(g):
    c = ZONE['res-row']
    zone_tile(g, c)
    for i in range(3):
        x = 5.5 + i * 7.2
        g.poly([(x - 0.4, 15), (x + 3.6, 9 + (i % 2) * 1.5), (x + 7.6, 15)], c)
        g.rect(x, 15, 7.2, 10, c)
        g.rect(x + 2.4, 19.5, 2.6, 5.5, SLATE)
    for i in range(1, 3):
        g.rect(5.5 + i * 7.2 - 0.4, 15, 0.8, 10, col(SLATE, 150))


def i_res_med(g):
    c = ZONE['res-med']
    zone_tile(g, c)
    g.rect(5.5, 9.5, 21, 15.5, c)
    g.rect(4.5, 7.5, 23, 2.4, c)
    for r in range(3):
        for k in range(4):
            g.rect(7.6 + k * 4.6, 11.4 + r * 3.4, 3, 2, SLATE)
    g.rect(14.3, 21.5, 3.4, 3.5, SLATE)


def i_res_mixed(g):
    c = ZONE['res-mixed']
    zone_tile(g, c)
    g.poly([(7, 13), (16, 5), (25, 13)], c)
    g.rect(9, 12.5, 14, 5, c)
    g.rect(11.5, 13.6, 3, 2.4, SLATE)
    g.rect(17.5, 13.6, 3, 2.4, SLATE)
    g.rect(7, 18.5, 18, 6.5, c)
    for i in range(4):
        g.poly([(5.5 + i * 5.25, 17.2), (10.75 + i * 5.25, 17.2), (10.5 + i * 5.25, 19.8), (5.75 + i * 5.25, 19.8)],
               W if i % 2 == 0 else TL)
    g.rect(9.5, 21, 5.5, 4, SLATE)
    g.rect(18, 21, 3.5, 4, SLATE)


def i_res_lowrent(g):
    c = ZONE['res-lowrent']
    zone_tile(g, c)
    b = lighten(c, 0.28)
    g.rect(6.5, 10.5, 12, 14.5, b)
    g.rect(17.5, 7, 8, 18, b)
    for r in range(3):
        for k in range(2):
            g.rect(8.4 + k * 4.8, 12.6 + r * 3.6, 3, 2.2, SLATE)
    for r in range(4):
        g.rect(19.6, 9 + r * 3.6, 3.8, 2.2, SLATE)


def i_off_high(g):
    c = ZONE['off-high']
    zone_tile(g, c)
    g.line(16, 2.8, 16, 6, 1.2, c)
    g.poly([(11.5, 25.5), (11.5, 9), (16, 5.5), (20.5, 9), (20.5, 25.5)], c)
    g.rect(8.5, 17, 3.5, 8.5, c)
    g.rect(20, 14, 3.5, 11.5, c)
    for r in range(6):
        g.rect(13.2, 9.8 + r * 2.6, 5.6, 1.3, SLATE)


def i_warehouse(g):
    c = ZONE['warehouse']
    zone_tile(g, c)
    g.poly([(4, 13.5), (9, 9), (23, 9), (28, 13.5), (28, 25), (4, 25)], c)
    for i in range(3):
        x = 6.6 + i * 7.2
        g.rect(x, 16, 5.2, 9, SLATE)
        for y in (18, 20.4, 22.8):
            g.rect(x + 0.6, y, 4, 0.8, col(c, 200))


# ---- industry ---------------------------------------------------------------------

def i_industry(g):
    g.circle(27.5, 3.6, 2.3, GREY)
    g.circle(30, 1.8, 1.4, GREY)
    g.rect(20.5, 7, 5, 16, TL)
    g.rect(20.5, 10, 5, 1.4, W)
    g.poly([(3, 28), (3, 15), (9.5, 19.5), (9.5, 15), (16, 19.5), (16, 15), (22.5, 19.5), (29, 19.5), (29, 28)], W)
    for x in (6, 12.5, 19):
        g.rect(x, 22, 3.4, 3.4, SLATE)
    g.rect(24, 22, 3.4, 6, SLATE)


def i_hub_farm(g):
    g.rect(22, 9, 7, 19, GREY)
    g.circle(25.5, 9, 3.5, W)
    for y in (14, 19, 24):
        g.rect(22, y, 7, 0.9, GREY_DIM)
    g.poly([(2.5, 15), (6, 8.5), (13, 5.5), (20, 8.5), (23.5, 15)], RED)
    g.rect(4, 15, 18, 13, W)
    g.rect(9, 19, 8, 9, TL)
    g.line(9.5, 19.5, 16.5, 27.5, 1.1, W)
    g.line(16.5, 19.5, 9.5, 27.5, 1.1, W)
    g.rect(11.5, 10, 3, 3, SLATE)


def i_hub_forestry(g):
    g.poly([(22.5, 2), (29.5, 14), (25.5, 14), (30, 21), (15, 21), (19.5, 14), (15.5, 14)], LEAF)
    g.rect(21.5, 20, 2.4, 3, BROWN)
    for cx, cy in ((7, 25), (14, 25), (21, 25), (10.5, 18.8), (17.5, 18.8), (14, 12.6)):
        g.circle(cx, cy, 3.6, WOOD)
        g.ring(cx, cy, 1.9, 0.9, SAND)
        g.circle(cx, cy, 0.6, BROWN)


def i_hub_quarry(g):
    g.rrect(2.5, 20, 12, 8.5, 1, fill=GREY)
    g.rrect(15.5, 20, 13, 8.5, 1, fill=SAND)
    g.rrect(8, 12, 12, 8, 1, fill=W)
    g.line(10, 15.5, 25, 3.5, 2.4, WOOD)
    g.poly([(17, 1.5), (23.5, 3.2), (29.5, 9.5), (24.5, 6.8), (18, 5.5)], GREY)


def i_hub_mine(g):
    g.line(4, 16, 9.5, 2.5, 1.8, GREY)
    g.line(15, 16, 9.5, 2.5, 1.8, GREY)
    g.ring(9.5, 5, 3, 1.4, W)
    g.rect(1.5, 28.2, 29, 1.6, GREY_DIM)
    for cx, cy, r in ((14, 14.5, 3), (19, 13, 3.6), (24, 14.6, 3), (17, 16, 2.6)):
        g.circle(cx, cy, r, SLATE)
        g.circle(cx - r * 0.3, cy - r * 0.3, r * 0.4, AMBER)
    g.poly([(9, 16), (29, 16), (26.5, 25), (11.5, 25)], TL)
    g.rect(10, 18.5, 18, 1.2, (255, 214, 120, 255))
    g.circle(14.5, 26, 2.6, GREY)
    g.circle(23.5, 26, 2.6, GREY)


def i_hub_oil(g):
    g.rect(3, 26.5, 21, 3, GREY_DIM)
    g.poly([(8, 27), (12, 11), (16, 27)], GREY)
    g.line(3, 11, 21, 8, 2.6, W)
    g.poly([(20, 4), (24.5, 6.5), (24.5, 13), (21, 9.5)], W)
    g.line(22.5, 11, 22.5, 26.5, 0.9, GREY)
    g.circle(12, 10, 1.6, TL)
    g.rrect(2, 11, 4.5, 8, 1, fill=TL)
    drop(g, 27, 22, 3.4, (60, 52, 46, 255), GREY)


def i_area_paint(g):
    dashed_rect(g, 3.5, 6.5, 21, 21, 1.8, W)
    g.rect(8, 11, 12, 12, col(TL, 110))
    g.line(30, 2, 21, 11, 2.6, WOOD)
    g.line(21.6, 10.4, 19.6, 12.4, 3.6, GREY)
    g.poly([(18, 11.5), (20.5, 14), (16, 19), (13, 19.5), (13.5, 16.5)], TL)


def i_area_clear(g):
    dashed_rect(g, 3.5, 6.5, 21, 21, 1.8, W)
    g.line(18, 4, 29, 15, 3.2, RED)
    g.line(29, 4, 18, 15, 3.2, RED)


ICONS2 += [
    ('zone-res-row', i_res_row), ('zone-res-med', i_res_med), ('zone-res-mixed', i_res_mixed),
    ('zone-res-lowrent', i_res_lowrent), ('zone-off-high', i_off_high), ('zone-warehouse', i_warehouse),
    ('industry', i_industry), ('hub-farm', i_hub_farm), ('hub-forestry', i_hub_forestry),
    ('hub-quarry', i_hub_quarry), ('hub-mine', i_hub_mine), ('hub-oil', i_hub_oil),
    ('area-paint', i_area_paint), ('area-clear', i_area_clear),
]


# ---- transit ----------------------------------------------------------------------

def i_transit(g):
    bus_front(g, 16, 3, 22, 22)


def i_bus(g):
    g.rrect(1.5, 7, 29, 17, 2.6, fill=W)
    g.rect(1.5, 18.5, 29, 1.6, TL)
    for i in range(4):
        g.rect(3.5 + i * 5.6, 9.5, 4.4, 6.5, SLATE)
    g.rect(26, 9.5, 3.2, 12, SLATE)
    g.circle(8, 24.5, 3.4, TL)
    g.circle(23, 24.5, 3.4, TL)
    g.circle(8, 24.5, 1.2, SLATE)
    g.circle(23, 24.5, 1.2, SLATE)


def i_taxi(g):
    g.rrect(11.5, 4, 9, 4.5, 1, fill=W)
    g.rect(13, 5.4, 6, 1.6, SLATE)
    g.rrect(2.5, 15, 27, 9, 3.6, fill=AMBER)
    g.poly([(7.5, 15.5), (11, 9), (21, 9), (24.5, 15.5)], AMBER)
    g.poly([(10.4, 14.5), (12.4, 10.5), (15.4, 10.5), (15.4, 14.5)], SLATE)
    g.poly([(16.8, 14.5), (16.8, 10.5), (19.8, 10.5), (21.8, 14.5)], SLATE)
    for x in (6, 10, 14, 18, 22):
        g.rect(x, 18.5, 2, 2, SLATE)
    g.circle(9, 24.5, 3.4, GREY)
    g.circle(23, 24.5, 3.4, GREY)


def i_tram(g):
    g.line(10, 2.2, 22, 2.2, 1.0, GREY)
    g.polyline([(12.5, 9.5), (16, 4.5), (19.5, 2.4)], 1.3, GREY)
    g.rrect(10, 8.5, 8, 2, 0.8, fill=GREY_DIM)
    g.rrect(3, 10, 26, 15, 3, fill=TL)
    g.rect(3, 19.5, 26, 1.6, W)
    for i in range(4):
        g.rect(5.2 + i * 5.6, 12.5, 4, 5.5, SLATE)
    g.rect(1.5, 28.3, 29, 1.4, GREY)
    g.circle(9, 26, 2.3, GREY_DIM)
    g.circle(23, 26, 2.3, GREY_DIM)


def i_metro(g):
    g.rrect(3.5, 3.5, 25, 25, 6, fill=TL)
    g.polyline([(9, 23), (9, 9.5), (16, 18), (23, 9.5), (23, 23)], 3.6, W)


def i_train(g):
    g.rect(1.5, 27.5, 29, 1.6, GREY_DIM)
    g.rect(5.5, 4.5, 4.5, 7, GREY)
    g.rrect(4, 3, 7.5, 2.5, 1, fill=GREY)
    g.rrect(3, 11, 17, 10, 4, fill=W)
    g.rect(13.5, 7.5, 3.5, 4, GREY)
    g.rect(19, 5, 10.5, 16, TL)
    g.rect(18, 3.5, 12.5, 2.6, TL)
    g.rect(21.5, 8, 5.5, 5, SLATE)
    g.poly([(1, 25), (4, 20), (4, 25)], GREY)
    g.rect(3, 20, 26.5, 2.4, GREY_DIM)
    for cx, r in ((8, 3), (15, 3), (24.5, 3.2)):
        g.circle(cx, 24.5, r, GREY)
        g.circle(cx, 24.5, 1.1, SLATE)


def i_bus_stop(g):
    g.rect(14.6, 13, 2.8, 15.5, GREY)
    g.rrect(10.5, 27.5, 11, 2.3, 0.8, fill=GREY_DIM)
    g.circle(16, 10, 8.6, W)
    g.ring(16, 10, 7.2, 1.2, TL)
    bus_front(g, 16, 5.2, 8.6, 7.4, TL)


def i_bus_depot(g):
    g.poly([(1.5, 11), (16, 3), (30.5, 11), (30.5, 28.5), (1.5, 28.5)], W)
    g.rect(5, 12.5, 22, 16, SLATE)
    for y in (13.5, 15.5):
        g.rect(5, y, 22, 0.8, GREY_DIM)
    bus_front(g, 16, 17.5, 14, 9.5, TL)


def i_line(g):
    pts = [(5, 24), (14, 9), (26, 19)]
    g.polyline(pts, 3.4, TL)
    for x, y in pts:
        g.circle(x, y, 4.3, W)
        g.circle(x, y, 1.9, TL)


# ---- service tabs -------------------------------------------------------------------

def i_garbage(g):
    g.rrect(13, 2.5, 6, 3, 1.2, fill=GREY)
    g.rrect(5, 5, 22, 4, 1.5, fill=W)
    g.poly([(7, 10), (25, 10), (23, 29), (9, 29)], GREY)
    for x in (12, 16, 20):
        g.line(x, 13, x - (x - 16) * 0.08, 26, 1.6, SLATE)


def i_deathcare(g):
    g.ellipse(16, 27.5, 13.5, 2.6, LEAF)
    g.rrect(8, 6, 16, 22, 1, rr=(8, 8, 0.5, 0.5), fill=GREY)
    g.rect(14.6, 10, 2.8, 12, SLATE)
    g.rect(11, 13, 10, 2.8, SLATE)


def i_comms(g):
    g.line(10.5, 29, 16, 8, 2, W)
    g.line(21.5, 29, 16, 8, 2, W)
    g.line(12, 23, 20, 23, 1.4, GREY)
    g.line(13.4, 17, 18.6, 17, 1.4, GREY)
    g.line(12, 23, 18.6, 17, 1.1, GREY)
    g.line(20, 23, 13.4, 17, 1.1, GREY)
    g.circle(16, 7, 2.6, TL)
    for r in (6, 10.5):
        g.arc(16, 7, r, -40, 40, 1.8, W)
        g.arc(16, 7, r, 140, 220, 1.8, W)


def i_post(g):
    g.rrect(2.5, 7, 27, 18.5, 2, fill=W)
    g.polyline([(3.5, 8), (16, 18), (28.5, 8)], 1.8, GREY_DIM)
    g.line(3.5, 24.5, 12.5, 15.5, 1.1, GREY)
    g.line(28.5, 24.5, 19.5, 15.5, 1.1, GREY)


def i_admin(g):
    g.line(16, 1.5, 16, 7, 1.0, GREY)
    g.poly([(16.5, 1.5), (22, 3), (16.5, 4.5)], RED)
    g.circle(16, 11.5, 5, W)
    g.rect(3, 11.5, 26, 3, W)
    for i in range(5):
        g.rect(4.8 + i * 5, 15.5, 2.6, 9, TL)
    g.rect(2, 25, 28, 3.5, W)


# ---- panels / stats -----------------------------------------------------------------

def i_stats(g):
    g.polyline([(4, 3), (4, 28), (29, 28)], 2.4, W)
    pts = [(7.5, 22), (13, 15), (18, 18.5), (26, 7)]
    g.polyline(pts, 2.6, TL)
    for x, y in pts:
        g.circle(x, y, 2.2, AMBER)


def i_chirper(g):
    g.poly([(3, 21), (10, 16.5), (9, 23)], CYAN)                     # tail
    g.ellipse(15.5, 17.5, 9, 7.5, CYAN)                              # body
    g.circle(21, 10.5, 5.6, CYAN)                                    # head
    g.poly([(25.5, 9), (30.5, 11), (25.5, 13)], AMBER)               # beak
    g.circle(22.3, 9.4, 1.4, SLATE)
    g.poly([(9, 15), (17, 13.5), (20, 18.5), (13, 22)], (40, 140, 178, 255))   # wing
    g.line(14, 25, 13, 29, 1.2, AMBER)
    g.line(18, 25, 18, 29, 1.2, AMBER)


def i_production(g):
    for x, y, s in ((2.5, 17, 11.5), (14.5, 17, 11.5), (8.5, 6, 11)):
        g.rect(x, y, s, s, WOOD)
        g.rect(x + 0.9, y + 0.9, s - 1.8, s - 1.8, col(SAND, 255))
        g.line(x + 1.6, y + 1.6, x + s - 1.6, y + s - 1.6, 1.4, WOOD)
    gear(g, 25, 8.5, 5, GREY)
    g.circle(25, 8.5, 1.6, SLATE)


def i_policies(g):
    g.rrect(5, 4, 22, 25.5, 2, fill=WOOD)
    g.rect(7.5, 7.5, 17, 19.5, W)
    g.rrect(11, 2, 10, 4.5, 1.4, fill=GREY)
    for y in (11, 14.5, 18):
        g.rect(14, y, 8, 1.4, GREY_DIM)
    g.polyline([(9, 22), (11.5, 24.5), (16, 19.5)], 2.2, GREEN)
    for y in (11, 14.5, 18):
        g.circle(10.5, y + 0.7, 1.1, TL)


def i_progression(g):
    g.line(16, 26, 9, 16, 2, W)
    g.line(16, 26, 23, 16, 2, W)
    g.line(9, 16, 5, 6, 2, W)
    g.line(9, 16, 14, 6, 2, W)
    g.line(23, 16, 26, 6, 2, W)
    g.circle(16, 26, 3.8, AMBER)
    for x, y in ((9, 16), (23, 16)):
        g.circle(x, y, 3.3, TL)
    for x, y in ((5, 6), (14, 6), (26, 6)):
        g.circle(x, y, 3, GREY)


DISTRICT_A = [(3, 4), (13, 4), (11, 12), (11.75, 14), (14, 20), (12, 28), (3, 28)]
DISTRICT_B = [(13, 4), (29, 4), (29, 13), (22, 16), (17, 13), (11.75, 14), (11, 12)]
DISTRICT_C = [(11.75, 14), (17, 13), (22, 16), (29, 13), (29, 28), (12, 28), (14, 20)]
DISTRICT_EDGES = ([(13, 4), (11, 12), (11.75, 14), (14, 20), (12, 28)], [(11.75, 14), (17, 13), (22, 16), (29, 13)])


def districts(g, fills, edge, ew):
    for pts, c in zip((DISTRICT_A, DISTRICT_B, DISTRICT_C), fills):
        g.poly(pts, c)
    for e in DISTRICT_EDGES:
        g.polyline(e, ew, edge)


def i_districts(g):
    districts(g, (GREEN, TL, BLUE), W, 1.6)
    g.circle(22.5, 22, 2.2, W)
    g.circle(7.5, 9, 1.8, W)


def i_tiles(g):
    for r in range(3):
        for k in range(3):
            hl = (r, k) == (1, 1)
            lk = (r, k) == (0, 2)
            g.rrect(3 + k * 9, 3 + r * 9, 8, 8, 1.4,
                    fill=AMBER if hl else GREY_DIM if lk else (GREY if (r + k) % 2 else SAND))
    g.arc(25, 7, 1.7, 180, 360, 1.1, W)
    g.rrect(22.4, 6.8, 5.2, 3.8, 0.6, fill=W)
    cross(g, 16, 16, 2.6, 1.4, SLATE)


def i_xp(g):
    g.circle(16, 16, 12.5, TL)
    g.ring(16, 16, 10, 1.2, (255, 214, 120, 255))
    star(g, 16, 16.6, 8.2, 3.4, W)


def i_dev_point(g):
    g.poly([(9, 5), (23, 5), (29, 12), (16, 29), (3, 12)], CYAN)
    g.poly([(3, 12), (29, 12), (16, 29)], (40, 150, 196, 255))
    g.poly([(11, 12), (21, 12), (16, 29)], (120, 220, 240, 255))
    g.poly([(9, 5), (11, 12), (3, 12)], (190, 240, 250, 255))
    g.poly([(16, 5), (21, 12), (11, 12)], (150, 228, 246, 255))


def i_permit(g):
    g.poly([(5, 2.5), (20, 2.5), (26, 8.5), (26, 29.5), (5, 29.5)], W)
    g.poly([(20, 2.5), (20, 8.5), (26, 8.5)], GREY)
    for y in (7, 11, 15):
        g.rect(8, y, 10 if y == 7 else 14, 1.5, GREY_DIM)
    g.poly([(18, 24), (16, 31), (19.5, 29.5), (22, 31.5), (22, 25)], RED)
    g.circle(21, 22, 5.4, RED)
    g.ring(21, 22, 3.6, 0.9, (255, 170, 150, 255))


def i_loan(g):
    g.poly([(13, 2), (25, 8.5), (1, 8.5)], W)
    for i in range(4):
        g.rect(3 + i * 5.8, 10, 2.6, 11, TL)
    g.rect(1.5, 21, 22.5, 2.6, W)
    coin(g, 23, 22.5, 7.8, AMBER, (150, 98, 10, 255))


def i_fee(g):
    tag = [(16, 3), (28.5, 3), (28.5, 15.5), (15, 29), (2.5, 16.5)]
    g.poly(tag, TL)
    g.circle(23.5, 8, 2, SLATE)
    coin(g, 11, 20, 8, AMBER, (150, 98, 10, 255))


def i_citizen(g):
    g.circle(16, 10, 6.6, W)
    g.rrect(4, 18.5, 24, 12, 1.5, rr=(9, 9, 1.5, 1.5), fill=W)
    g.poly([(13, 18.6), (19, 18.6), (16, 23.5)], TL)


def i_home(g):
    g.polyline([(3, 15), (16, 3.5), (29, 15)], 3, W)
    g.polyline([(7, 13), (7, 28), (25, 28), (25, 13)], 3, W)
    g.rect(13.5, 19, 5, 9, TL)


def i_work(g):
    g.rrect(10.5, 4.5, 11, 7, 2.6, fill=WOOD)
    g.rrect(2.5, 9.5, 27, 18.5, 2.6, fill=WOOD)
    g.rect(13, 7, 6, 2.8, SLATE)
    g.rect(2.5, 16.5, 27, 1.6, BROWN)
    g.rrect(13.5, 15, 5, 4.6, 1, fill=AMBER)


def i_school(g):
    g.poly([(16, 9), (29, 5), (29, 25), (16, 28.5)], W)
    g.poly([(16, 9), (3, 5), (3, 25), (16, 28.5)], SAND)
    g.line(16, 9, 16, 28.5, 1.2, GREY_DIM)
    for i, y in enumerate((10, 14, 18)):
        g.line(6, y, 13.5, y + 2.4, 1.0, GREY)
        g.line(18.5, y + 2.4, 26, y, 1.0, GREY)
    g.poly([(22, 4.7), (26, 3.5), (26, 12), (24, 10.5), (22, 12.6)], RED)


def i_tourist(g):
    g.rrect(6, 6, 8, 4, 1, fill=GREY)
    g.rrect(2.5, 9, 27, 18, 3, fill=W)
    g.rect(2.5, 13, 27, 1.2, GREY)
    g.circle(17, 18, 7.2, SLATE)
    g.circle(17, 18, 5, TL)
    g.circle(17, 18, 2.6, SLATE)
    g.circle(15.6, 16.6, 1, W)
    g.rect(23.5, 11, 3.5, 1.6, AMBER)


def i_hotel(g):
    g.rrect(2.5, 6, 4, 22, 1.2, fill=WOOD)
    g.rrect(25.5, 15, 4, 13, 1.2, fill=WOOD)
    g.rect(2.5, 18.5, 27, 4.5, WOOD)
    g.rrect(6.5, 13.5, 7.5, 5, 2, fill=W)
    g.rrect(13, 13.5, 13, 5.5, 1.5, fill=TL)
    g.rect(6.5, 18, 19, 1.5, W)


# ---- misc ----------------------------------------------------------------------------

def i_lock(g):
    g.arc(16, 13, 7, 180, 360, 3.2, GREY)
    g.line(9, 13, 9, 15, 3.2, GREY)
    g.line(23, 13, 23, 15, 3.2, GREY)
    g.rrect(5, 14, 22, 15.5, 2.6, fill=AMBER)
    g.circle(16, 20, 2.6, SLATE)
    g.poly([(14.6, 21), (17.4, 21), (18.2, 25.5), (13.8, 25.5)], SLATE)


def i_eye(g):
    pts = []
    for i in range(33):
        t = i / 32.0
        x = 2 + 28 * t
        pts.append((x, 16 - 9.5 * math.sin(math.pi * t)))
    for i in range(33):
        t = 1 - i / 32.0
        pts.append((2 + 28 * t, 16 + 9.5 * math.sin(math.pi * t)))
    g.poly(pts, W)
    g.circle(16, 16, 6.4, TL)
    g.circle(16, 16, 3, SLATE)
    g.circle(14, 14, 1.3, W)


def i_arrow_left(g):
    g.rect(13, 12.6, 15, 6.8, W)
    g.poly([(3, 16), (15, 4.5), (15, 27.5)], W)


def i_arrow_right(g):
    g.rect(4, 12.6, 15, 6.8, W)
    g.poly([(29, 16), (17, 4.5), (17, 27.5)], W)


def i_follow(g):
    g.ring(16, 16, 9.5, 2.4, W)
    for x0, y0, x1, y1 in ((16, 1.5, 16, 9), (16, 23, 16, 30.5), (1.5, 16, 9, 16), (23, 16, 30.5, 16)):
        g.line(x0, y0, x1, y1, 2.4, W)
    g.circle(16, 16, 3, TL)


def i_locate(g):
    g.ellipse(16, 28, 7, 2, col(SLATE, 200))
    g.circle(16, 11.5, 9, RED)
    g.poly([(8, 15.5), (24, 15.5), (16, 28)], RED)
    g.circle(16, 11.5, 3.6, W)


ICONS2 += [
    ('transit', i_transit), ('bus', i_bus), ('taxi', i_taxi), ('tram', i_tram), ('metro', i_metro),
    ('train', i_train), ('bus-stop', i_bus_stop), ('bus-depot', i_bus_depot), ('line', i_line),
    ('garbage', i_garbage), ('deathcare', i_deathcare), ('comms', i_comms), ('post', i_post),
    ('admin', i_admin),
    ('stats', i_stats), ('chirper', i_chirper), ('production', i_production), ('policies', i_policies),
    ('progression', i_progression), ('districts', i_districts), ('tiles', i_tiles), ('xp', i_xp),
    ('dev-point', i_dev_point), ('permit', i_permit), ('loan', i_loan), ('fee', i_fee),
    ('citizen', i_citizen), ('home', i_home), ('work', i_work), ('school', i_school),
    ('tourist', i_tourist), ('hotel', i_hotel),
    ('lock', i_lock), ('eye', i_eye), ('arrow-left', i_arrow_left), ('arrow-right', i_arrow_right),
    ('follow', i_follow), ('locate', i_locate),
]
