"""Reusable glyph drawing recipes (design units, drawn into a uidraw.G)."""

import math
from uitheme import *  # noqa: F401,F403


def T(pts, ox=0.0, oy=0.0, k=1.0):
    return [(ox + x * k, oy + y * k) for x, y in pts]


BOLT = [(9.5, 1), (3.2, 9.2), (7.4, 9.2), (6.3, 15), (12.8, 6.6), (8.6, 6.6)]


def bolt(g, ox, oy, k, c):
    g.poly(T(BOLT, ox, oy, k), c)


def drop(g, cx, cy, r, c, hl=None):
    """Water drop: circle body + pointed top."""
    g.circle(cx, cy + r * 0.25, r, c)
    g.poly([(cx, cy - r * 1.55), (cx - r * 0.93, cy - r * 0.1), (cx + r * 0.93, cy - r * 0.1)], c)
    if hl:
        g.arc(cx, cy + r * 0.25, r * 0.55, 110, 170, max(0.8, r * 0.22), hl)


def coin(g, cx, cy, r, fill, mark):
    g.circle(cx, cy, r, fill)
    if r >= 9:
        g.ring(cx, cy, r * 0.72, max(0.8, r * 0.14), mark)
    dollar(g, cx, cy, r * (0.55 if r >= 9 else 0.5), mark)


def dollar(g, cx, cy, h, c):
    """A '$' sign of half-height h."""
    w = max(0.9, h * 0.30)
    r = h * 0.5
    g.arc(cx, cy - r, r, -30, -270, w, c)       # upper bowl
    g.arc(cx, cy + r, r, -90, 150, w, c)        # lower bowl
    g.line(cx, cy - h * 1.15, cx, cy + h * 1.15, max(0.7, w * 0.7), c)


def person(g, cx, cy, k, c):
    """Head + shoulders; (cx,cy) is the centre of the 12x14 figure scaled by k."""
    g.circle(cx, cy - 4.3 * k, 3.0 * k, c)
    g.poly(T([(-5.2, 7), (-5.2, 3.8), (-3.2, 0.6), (3.2, 0.6), (5.2, 3.8), (5.2, 7)], cx, cy, k), c)


def chevron(g, cx, cy, k, direction, w, c):
    d = {'down': (0, 1), 'up': (0, -1), 'left': (-1, 0), 'right': (1, 0)}[direction]
    px, py = -d[1], d[0]
    a = (cx - d[0] * 2.5 * k + px * 4 * k, cy - d[1] * 2.5 * k + py * 4 * k)
    b = (cx + d[0] * 2.5 * k, cy + d[1] * 2.5 * k)
    e = (cx - d[0] * 2.5 * k - px * 4 * k, cy - d[1] * 2.5 * k - py * 4 * k)
    g.polyline([a, b, e], w, c)


def speaker(g, ox, oy, k, c):
    g.poly(T([(1, 5.5), (4.2, 5.5), (8, 2), (8, 14), (4.2, 10.5), (1, 10.5)], ox, oy, k), c)


def gear(g, cx, cy, r, c):
    for i in range(8):
        a = math.radians(i * 45)
        g.line(cx + math.cos(a) * r * 0.6, cy + math.sin(a) * r * 0.6,
               cx + math.cos(a) * r * 1.0, cy + math.sin(a) * r * 1.0, r * 0.42, c, cap=False)
    g.circle(cx, cy, r * 0.75, c)


def star(g, cx, cy, ro, ri, c):
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        rr = ro if i % 2 == 0 else ri
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    g.poly(pts, c)


def clock(g, cx, cy, r, c, w=1.3):
    g.ring(cx, cy, r, w, c)
    g.line(cx, cy, cx, cy - r * 0.6, w, c)
    g.line(cx, cy, cx + r * 0.45, cy + r * 0.2, w, c)


def crown(g, ox, oy, k, c):
    g.poly(T([(1, 12), (0.5, 4.5), (4.5, 8), (8, 2.5), (11.5, 8), (15.5, 4.5), (15, 12)], ox, oy, k), c)
    g.rect(ox + 1 * k, oy + 12.5 * k, 14 * k, 1.6 * k, c)


def shield(g, cx, top, w, h, fill, stroke=None):
    pts = [(cx - w / 2, top), (cx, top - 0.0), (cx + w / 2, top)]
    # flat top, curved bottom approximated by polygon
    poly = [(cx - w / 2, top + h * 0.05), (cx, top - h * 0.04), (cx + w / 2, top + h * 0.05),
            (cx + w / 2, top + h * 0.5), (cx + w * 0.38, top + h * 0.72), (cx, top + h),
            (cx - w * 0.38, top + h * 0.72), (cx - w / 2, top + h * 0.5)]
    g.poly(poly, fill)


def flame(g, cx, by, k, outer, inner):
    pts = [(0, -16), (3.5, -10), (7, -6), (7.5, -1.5), (5, 3), (0, 5), (-5, 3), (-7.5, -1.5), (-6, -7), (-3.5, -6), (-2.5, -11)]
    g.poly(T(pts, cx, by, k * 0.9), outer)
    ip = [(0, -6), (2.8, -2.5), (3, 1), (0, 3.2), (-3, 1), (-3, -2.5)]
    g.poly(T(ip, cx, by, k * 0.9), inner)


def tree(g, cx, by, k, canopy, trunk):
    g.rect(cx - 1.6 * k, by - 7 * k, 3.2 * k, 8 * k, trunk)
    g.circle(cx, by - 12 * k, 6.5 * k, canopy)
    g.circle(cx - 5 * k, by - 8 * k, 4.8 * k, canopy)
    g.circle(cx + 5 * k, by - 8 * k, 4.8 * k, canopy)


def cap(g, cx, cy, k, c, tassel):
    g.poly(T([(0, -7), (14, -1), (0, 5), (-14, -1)], cx, cy, k), c)
    g.poly(T([(-7.5, 3.4), (0, 6.6), (7.5, 3.4), (7.5, 8), (0, 11.4), (-7.5, 8)], cx, cy, k), c)
    g.line(cx + 12.5 * k, cy - 0.3 * k, cx + 12.5 * k, cy + 7 * k, 1.5 * k, tassel)
    g.circle(cx + 12.5 * k, cy + 8 * k, 1.8 * k, tassel)


def cross(g, cx, cy, arm, thick, c):
    g.rrect(cx - thick / 2, cy - arm, thick, arm * 2, thick * 0.2, fill=c)
    g.rrect(cx - arm, cy - thick / 2, arm * 2, thick, thick * 0.2, fill=c)


def zone_tile(g, c):
    g.rrect(1.5, 1.5, 29, 29, 6, fill=col(c, 56), stroke=col(c, 235), sw=1.5)
