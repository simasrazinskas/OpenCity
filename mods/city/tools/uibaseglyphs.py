"""Base UI glyphs (checkmarks, arrows, music controls, lobby badges, editor tools) as vector recipes.

Each recipe draws into a uidraw.G-like canvas in design units using only the primitive methods that
uiexport.Recorder understands (rrect/circle/ellipse/ring/line/polyline/arc/poly/rect). The game replays
them at runtime and applies the stylize post-process (dark outline + bevel + vertical shading), so keep
strokes >= 1.6 units and ~1.5 units of margin for the outline.
"""

import math
from uiglyphs import *  # noqa: F401,F403

W = WHITE


def fade(c, a):
    return (c[0], c[1], c[2], a)


FAINT = 110


class Xf:
    """Transforming proxy around a G/Recorder: p -> (ox + sx * x, oy + k * y), sx = -k when mirrored."""

    def __init__(self, g, k=1.0, ox=0.0, oy=0.0, mirror=False):
        self.g, self.k, self.ox, self.oy, self.m = g, k, ox, oy, mirror
        self.sx = -k if mirror else k

    def P(self, x, y):
        return (self.ox + self.sx * x, self.oy + self.k * y)

    def rrect(self, x, y, w, h, r, fill=None, stroke=None, sw=1, rr=None):
        k = self.k
        nx = self.ox + (self.sx * (x + w) if self.m else self.sx * x)
        if rr and self.m:
            rr = (rr[1], rr[0], rr[3], rr[2])
        if rr:
            rr = tuple(v * k for v in rr)
        self.g.rrect(nx, self.oy + k * y, w * k, h * k, r * k, fill=fill, stroke=stroke, sw=sw * k, rr=rr)

    def circle(self, cx, cy, r, fill=None, stroke=None, sw=1):
        x, y = self.P(cx, cy)
        self.g.circle(x, y, r * self.k, fill=fill, stroke=stroke, sw=sw * self.k)

    def ellipse(self, cx, cy, rx, ry, fill):
        x, y = self.P(cx, cy)
        self.g.ellipse(x, y, rx * self.k, ry * self.k, fill)

    def ring(self, cx, cy, r, w, c):
        x, y = self.P(cx, cy)
        self.g.ring(x, y, r * self.k, w * self.k, c)

    def line(self, x0, y0, x1, y1, w, c, cap=True):
        a, b = self.P(x0, y0), self.P(x1, y1)
        self.g.line(a[0], a[1], b[0], b[1], w * self.k, c, cap)

    def polyline(self, pts, w, c, closed=False):
        self.g.polyline([self.P(x, y) for x, y in pts], w * self.k, c, closed)

    def arc(self, cx, cy, r, a0, a1, w, c):
        x, y = self.P(cx, cy)
        if self.m:
            a0, a1 = 180 - a0, 180 - a1
        self.g.arc(x, y, r * self.k, a0, a1, w * self.k, c)

    def poly(self, pts, c):
        self.g.poly([self.P(x, y) for x, y in pts], c)

    def rect(self, x, y, w, h, c):
        self.rrect(x, y, w, h, 0, fill=c)


def tri(g, pts, c, rnd=1.2):
    """Filled polygon with softly rounded corners (fill + same-colour capsule border)."""
    g.poly(pts, c)
    g.polyline(pts, rnd, c, closed=True)


def head(g, x, y, dx, dy, size, c):
    """Arrow head: tip at (x, y) + dir * size, base centred on (x, y)."""
    n = math.hypot(dx, dy)
    dx, dy = dx / n, dy / n
    px, py = -dy, dx
    tri(g, [(x + dx * size, y + dy * size), (x + px * size, y + py * size), (x - px * size, y - py * size)], c, 0.8)


# ---- 16x16 --------------------------------------------------------------------------------------

def _check(c):
    def f(g):
        g.polyline([(3, 8.6), (6.4, 12), (13, 4.4)], 2.6, c)
    return f


def _cross(c):
    def f(g):
        g.line(4, 4, 12, 12, 2.6, c)
        g.line(12, 4, 4, 12, 2.6, c)
    return f


def _speaker_body(g, c):
    tri(g, [(1.6, 6.2), (3.8, 6.2), (6.8, 3.2), (6.8, 12.8), (3.8, 9.8), (1.6, 9.8)], c, 1.0)


def g_speaker(g):
    _speaker_body(g, W)
    g.arc(6.6, 8, 3.5, -42, 42, 1.6, W)
    g.arc(6.6, 8, 6.7, -46, 46, 1.6, W)


def g_speaker_muted(g):
    _speaker_body(g, W)
    g.line(10, 5.8, 14, 10.2, 1.9, RED)
    g.line(14, 5.8, 10, 10.2, 1.9, RED)


def _arrow(direction):
    pts = [(8, 4.6), (13, 10.6), (3, 10.6)]   # up, visually centred

    def f(g):
        if direction == 'up':
            P = pts
        elif direction == 'down':
            P = [(x, 16 - y) for x, y in pts]
        elif direction == 'left':
            P = [(y, x) for x, y in pts]
        else:
            P = [(16 - y, x) for x, y in pts]
        tri(g, P, W)
    return f


def g_play(g):
    tri(g, [(5, 3.2), (12.6, 8), (5, 12.8)], W)


def g_pause(g):
    g.rrect(3.8, 3.2, 3.2, 9.6, 0.8, fill=W)
    g.rrect(9, 3.2, 3.2, 9.6, 0.8, fill=W)


def g_stop(g):
    g.rrect(3.6, 3.6, 8.8, 8.8, 1.2, fill=W)


def g_next(g):
    tri(g, [(3.4, 3.6), (10, 8), (3.4, 12.4)], W)
    g.rrect(10.4, 3.2, 2.6, 9.6, 0.7, fill=W)


def g_prev(g):
    g_next(Xf(g, 1, 16, 0, True))


def g_fastforward(g):
    tri(g, [(2.2, 4), (8, 8), (2.2, 12)], W)
    tri(g, [(8.4, 4), (14.2, 8), (8.4, 12)], W)


def g_reload(g):
    cx, cy, r = 8, 8.4, 4.8
    g.arc(cx, cy, r, 20, 290, 2.0, W)
    a = math.radians(290)
    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
    head(g, x, y, -math.sin(a), math.cos(a), 2.7, W)


def _spinner(frame):
    def f(g):
        g.ring(8, 8, 5.2, 2.2, fade(GREY, 150))
        a0 = -90 + frame * 30
        g.arc(8, 8, 5.2, a0, a0 + 100, 2.4, TEAL_LIGHT)
    return f


def g_clock(g):
    g.ring(8, 8, 5.6, 1.8, W)
    g.line(8, 8, 8, 4.6, 1.7, W)
    g.line(8, 8, 10.6, 9.4, 1.7, W)


def _bolt(c):
    def f(g):
        g.poly([(10.4, 1.4), (3, 9.4), (7.2, 9.4), (5.6, 14.6), (13, 6.4), (8.8, 6.4)], c)
    return f


def g_coin(g):
    g.circle(8, 8, 6.3, fill=GOLD)
    d = BRONZE_DARK
    g.arc(8, 6.4, 1.7, -20, -250, 1.3, d)
    g.arc(8, 9.6, 1.7, -70, 160, 1.3, d)
    g.line(8, 3.4, 8, 12.6, 1.1, d)


def _person(c, admin):
    def f(g):
        if admin:
            person(g, 6.6, 9.4, 0.8, c)
            star(g, 12.6, 4.1, 2.9, 1.25, GOLD)
        else:
            person(g, 8, 8.5, 0.9, c)
    return f


def g_bot(g):
    c = SAND
    g.line(8, 4.6, 8, 2.6, 1.6, c)
    g.circle(8, 2.4, 1.3, fill=c)
    g.rect(1.6, 7.6, 1.6, 3.6, c)
    g.rect(12.8, 7.6, 1.6, 3.6, c)
    g.rrect(3.2, 4.6, 9.6, 9, 2, fill=c)
    g.circle(6, 8.4, 1.35, fill=SLATE)
    g.circle(10, 8.4, 1.35, fill=SLATE)
    g.rect(5.6, 11, 4.8, 1.2, SLATE)


def g_select(g):
    g.poly([(4, 1.6), (4, 13.2), (6.9, 10.6), (8.8, 14.6), (10.9, 13.6), (9, 9.7), (12.7, 9.5)], W)


def g_tiles(g):
    for x in (2, 8.6):
        for y in (2, 8.6):
            g.rrect(x, y, 5.4, 5.4, 1, fill=SAND)


def g_overlays(g):
    g.poly([(8, 1.8), (14, 4.8), (8, 7.8), (2, 4.8)], W)
    g.polyline([(2.2, 8), (8, 10.9), (13.8, 8)], 1.7, SAND)
    g.polyline([(2.2, 11), (8, 13.9), (13.8, 11)], 1.7, SAND)


def g_actors(g):
    g.poly([(8, 2), (14.4, 8), (12.4, 8), (12.4, 14), (3.6, 14), (3.6, 8), (1.6, 8)], SAND)
    g.rect(6.8, 9.8, 2.4, 4.2, SLATE)


def g_tools(g):
    cx, cy = 10.6, 5.4
    g.line(cx, cy, 3.4, 12.6, 2.6, SAND)
    g.arc(cx, cy, 2.5, 0, 270, 2.3, SAND)


def g_history(g):
    cx, cy, r = 8.6, 8.2, 5.2
    g.arc(cx, cy, r, 215, 520, 1.8, W)
    a = math.radians(215)
    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
    head(g, x, y, math.sin(a), -math.cos(a), 2.6, W)
    g.line(cx, cy, cx, cy - 2.8, 1.6, W)
    g.line(cx, cy, cx + 2.2, cy + 1.4, 1.6, W)


def g_erase(g):
    u, n = (0.7071, -0.7071), (0.7071, 0.7071)
    cx, cy, hl, hw = 8.6, 7.0, 5.0, 2.7

    def p(t, s):
        return (cx + u[0] * t + n[0] * s, cy + u[1] * t + n[1] * s)
    g.poly([p(-1.4, -hw), p(hl, -hw), p(hl, hw), p(-1.4, hw)], SAND)
    g.poly([p(-hl, -hw), p(-1.4, -hw), p(-1.4, hw), p(-hl, hw)], TEAL)
    g.line(8.2, 14.2, 14, 14.2, 1.5, SAND)


def g_copy(g):
    g.poly([(5.6, 1.6), (14, 1.6), (14, 11.4), (11.6, 11.4), (11.6, 3.6), (5.6, 3.6)], SAND)
    g.rrect(2, 4.6, 8.6, 9.8, 1, fill=W)
    g.rect(3.8, 7.4, 5, 1.2, SLATE)
    g.rect(3.8, 10.4, 5, 1.2, SLATE)


def g_paste(g):
    g.rrect(2.6, 2.8, 10.8, 11.8, 1.4, fill=BRONZE)
    g.rect(4.4, 5.6, 7.2, 7.6, W)
    g.rrect(5.4, 1.4, 5.2, 3.2, 1, fill=SAND)
    g.rect(5.6, 8, 4.8, 1.1, SLATE)
    g.rect(5.6, 10.4, 4.8, 1.1, SLATE)


def g_undo(g):
    w = 2.0
    g.line(5.2, 5.2, 8.6, 5.2, w, W)
    g.arc(8.6, 9.2, 4, 270, 450, w, W)
    g.line(8.6, 13.2, 5.4, 13.2, w, W)
    tri(g, [(1.8, 5.2), (5.6, 2), (5.6, 8.4)], W, 0.8)


def g_redo(g):
    g_undo(Xf(g, 1, 16, 0, True))


# ---- 22x22 spawn markers / colour swatch ----------------------------------------------------------

def g_spawn_claimed(g):
    g.circle(11, 11, 8.6, fill=GOLD)
    g.ring(11, 11, 4.4, 2.0, SLATE)


def g_spawn_unclaimed(g):
    g.ring(11, 11, 7.6, 2.6, SAND)
    g.circle(11, 11, 1.8, fill=SAND)


def g_spawn_disabled(g):
    g.ring(11, 11, 7.6, 2.4, GREY)
    g.line(5.8, 16.2, 16.2, 5.8, 2.4, GREY)


def g_colorpicker(g):
    g.ring(11, 11, 8.2, 2.2, SAND)


# ---- 22x21 flags ----------------------------------------------------------------------------------

def _pole(g, c):
    g.line(5, 3, 5, 18.4, 1.9, c)
    g.line(2.6, 18.6, 7.4, 18.6, 1.8, c)
    g.circle(5, 2.4, 1.4, fill=c)


FLAG = [(6.2, 3.4), (10, 2.4), (14.4, 4), (19.6, 3), (19.6, 13), (14.4, 14), (10, 12.4), (6.2, 13.4)]


def g_flag_city(g):
    _pole(g, GOLD)
    g.poly(FLAG, RED)
    # a small gold skyline (the OpenCity emblem)
    for x0, top in ((9.4, 8.2), (11.8, 5.6), (14.2, 7.0), (16.4, 9.0)):
        g.rect(x0, top, 2.0, 11.6 - top, GOLD)


def g_flag_random(g):
    _pole(g, GREY)
    g.poly(FLAG, SAND)
    d = SLATE
    g.arc(12.9, 6.6, 2.1, 180, 400, 1.6, d)
    g.line(14.5, 8, 12.9, 9.2, 1.6, d)
    g.line(12.9, 9.2, 12.9, 9.8, 1.6, d)
    g.circle(12.9, 11.8, 0.95, fill=d)


def g_flag_spectator(g):
    pts = []
    for i in range(13):
        t = i / 12.0
        x = 2 + 18 * t
        pts.append((x, 10.5 - 6.4 * math.sin(math.pi * t)))
    for i in range(1, 12):
        t = 1 - i / 12.0
        x = 2 + 18 * t
        pts.append((x, 10.5 + 6.4 * math.sin(math.pi * t)))
    g.poly(pts, SAND)
    g.circle(11, 10.5, 4, fill=TEAL_DARK)
    g.circle(11, 10.5, 1.8, fill=SLATE_DARK)
    g.circle(12.4, 9.1, 0.9, fill=WHITE)


# ---- 12x13 lock / key -----------------------------------------------------------------------------

def _lock(c):
    def f(g):
        g.arc(6, 5.2, 2.7, 180, 360, 1.8, c)
        g.line(3.3, 5.2, 3.3, 7, 1.8, c)
        g.line(8.7, 5.2, 8.7, 7, 1.8, c)
        g.rrect(1.6, 6.4, 8.8, 5.8, 1.2, fill=c)
        g.circle(6, 8.6, 1.05, fill=SLATE)
        g.rect(5.5, 8.8, 1, 2.1, SLATE)
    return f


def _key(c):
    def f(g):
        g.ring(6, 4, 2.3, 1.9, c)
        g.line(6, 6.3, 6, 11.6, 1.9, c)
        g.line(6, 9.2, 8.6, 9.2, 1.7, c)
        g.line(6, 11.4, 8.6, 11.4, 1.7, c)
    return f


# ---- misc -----------------------------------------------------------------------------------------

def g_kick(g):
    g.line(2.6, 2.6, 8.4, 8.4, 2.4, RED)
    g.line(8.4, 2.6, 2.6, 8.4, 2.4, RED)


def g_admin_tiny(g):
    star(g, 3, 2.75, 2.75, 1.15, GOLD)


def g_hue_marker(g):
    g.rect(2.7, 2, 1.6, 11, W)
    g.poly([(0.6, 0.6), (6.4, 0.6), (3.5, 3.8)], W)
    g.poly([(0.6, 14.4), (6.4, 14.4), (3.5, 11.2)], W)


def g_muted_indicator(g):
    g_speaker_muted(Xf(g, 1.5, 1, 0))


GLYPHS = [
    ('check', 16, 16, _check(W)),
    ('check-faint', 16, 16, _check(fade(W, FAINT))),
    ('cross', 16, 16, _cross(RED)),
    ('cross-faint', 16, 16, _cross(fade(RED, FAINT))),
    ('speaker', 16, 16, g_speaker),
    ('speaker-muted', 16, 16, g_speaker_muted),
    ('arrow-up', 16, 16, _arrow('up')),
    ('arrow-down', 16, 16, _arrow('down')),
    ('arrow-left', 16, 16, _arrow('left')),
    ('arrow-right', 16, 16, _arrow('right')),
    ('play', 16, 16, g_play),
    ('pause', 16, 16, g_pause),
    ('stop', 16, 16, g_stop),
    ('next', 16, 16, g_next),
    ('prev', 16, 16, g_prev),
    ('fastforward', 16, 16, g_fastforward),
    ('reload', 16, 16, g_reload),
] + [('spinner-%d' % i, 16, 16, _spinner(i)) for i in range(12)] + [
    ('clock', 16, 16, g_clock),
    ('bolt', 16, 16, _bolt(GOLD)),
    ('bolt-critical', 16, 16, _bolt(RED)),
    ('coin', 16, 16, g_coin),
    ('admin-registered', 16, 16, _person(TEAL_LIGHT, True)),
    ('admin-anonymous', 16, 16, _person(GREY, True)),
    ('player-registered', 16, 16, _person(TEAL_LIGHT, False)),
    ('player-anonymous', 16, 16, _person(GREY, False)),
    ('bot', 16, 16, g_bot),
    ('select', 16, 16, g_select),
    ('tiles', 16, 16, g_tiles),
    ('overlays', 16, 16, g_overlays),
    ('actors', 16, 16, g_actors),
    ('tools', 16, 16, g_tools),
    ('history', 16, 16, g_history),
    ('erase', 16, 16, g_erase),
    ('copy', 16, 16, g_copy),
    ('paste', 16, 16, g_paste),
    ('undo', 16, 16, g_undo),
    ('redo', 16, 16, g_redo),
    ('spawn-claimed', 22, 22, g_spawn_claimed),
    ('spawn-unclaimed', 22, 22, g_spawn_unclaimed),
    ('spawn-disabled', 22, 22, g_spawn_disabled),
    ('colorpicker', 22, 22, g_colorpicker),
    ('flag-city', 22, 21, g_flag_city),
    ('flag-random', 22, 21, g_flag_random),
    ('flag-spectator', 22, 21, g_flag_spectator),
    ('lock', 12, 13, _lock(GOLD)),
    ('lock-disabled', 12, 13, _lock(GREY)),
    ('key', 12, 13, _key(GOLD)),
    ('key-disabled', 12, 13, _key(GREY)),
    ('kick', 11, 11, g_kick),
    ('admin-tiny', 6, 5, g_admin_tiny),
    ('hue-marker', 7, 15, g_hue_marker),
    ('muted-indicator', 26, 24, g_muted_indicator),
]

ALIASES = {}
