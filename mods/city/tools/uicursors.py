"""Cursor sheet (32x32 frames) + cursors.yaml."""

import math
import os
from uidraw import G
from pngkit import save_sheet
from uitheme import *  # noqa: F401,F403

OUTLINE = (10, 14, 19, 235)
SIZE = 32


def rot(pts, deg, cx, cy):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]


def outlined(g, pts, fill, ow=2.6):
    g.polyline(pts, ow, OUTLINE, closed=True)
    g.poly(pts, fill)


ARROW = [(0, 0), (0, 17.5), (4.3, 13.6), (7.2, 20), (10, 18.8), (7.2, 12.6), (12.8, 12.4)]


def c_default(g, fill=WHITE):
    outlined(g, [(x + 4.5, y + 3.5) for x, y in ARROW], fill)
    return (4.5, 3.5)


def c_select(g):
    c_default(g, TEAL_LIGHT)
    g.ring(23, 23, 4, 3.0, OUTLINE)
    g.ring(23, 23, 4, 1.4, WHITE)
    return (4.5, 3.5)


def blocked(g):
    g.ring(16, 16, 9, 5.2, OUTLINE)
    g.ring(16, 16, 9, 3.2, RED)
    g.line(10, 22, 22, 10, 5.0, OUTLINE)
    g.line(10, 22, 22, 10, 3.0, RED)
    return (16, 16)


def move(g):
    for a in (0, 90, 180, 270):
        pts = rot([(0, -11), (5, -5), (1.8, -5), (1.8, -1.5), (-1.8, -1.5), (-1.8, -5), (-5, -5)], a, 16, 16)
        outlined(g, [(x, y) for x, y in pts], TEAL_LIGHT, 2.2)
    return (16, 16)


def sell(g):
    g.circle(16, 16, 11.5, OUTLINE)
    g.circle(16, 16, 10, AMBER)
    from uiglyphs import dollar
    dollar(g, 16, 16, 5.4, (120, 76, 6, 255))
    return (16, 16)


def scroll_arrow(g, deg, fill, tip_hot):
    base = [(0, -11), (9, 0), (4, 0), (4, 10), (-4, 10), (-4, 0), (-9, 0)]
    pts = rot(base, deg, 16, 16)
    outlined(g, pts, fill)
    if tip_hot:
        t = rot([(0, -11)], deg, 16, 16)[0]
        return t
    return (16, 16)


def crosshair(g):
    for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        x0, y0, x1, y1 = 16 + dx * 4, 16 + dy * 4, 16 + dx * 10, 16 + dy * 10
        g.line(x0, y0, x1, y1, 4.2, OUTLINE)
    for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        g.line(16 + dx * 4, 16 + dy * 4, 16 + dx * 10, 16 + dy * 10, 2.0, TEAL_LIGHT)
    g.circle(16, 16, 2.3, OUTLINE)
    g.circle(16, 16, 1.3, WHITE)


def badge(g, kind):
    g.circle(25, 25, 6.2, OUTLINE)
    g.circle(25, 25, 5.0, (46, 22, 12, 255))
    if kind == 'road':
        g.poly([(23.4, 21), (26.6, 21), (28.6, 29), (21.4, 29)], WHITE)
        g.rect(24.6, 22.5, 0.9, 1.4, TEAL)
        g.rect(24.5, 25.5, 1, 1.8, TEAL)
    elif kind == 'zone':
        g.rrect(21.3, 21.3, 7.4, 7.4, 1.4, fill=(126, 217, 87, 255))
    elif kind == 'bulldoze':
        g.line(22, 22, 28, 28, 1.8, RED)
        g.line(28, 22, 22, 28, 1.8, RED)
    elif kind == 'place':
        g.line(25, 21.5, 25, 28.5, 1.8, WHITE)
        g.line(21.5, 25, 28.5, 25, 1.8, WHITE)


def tool(kind):
    def f(g):
        crosshair(g)
        badge(g, kind)
        return (16, 16)
    return f


def pixelize(cv, shadow=(0, 0, 0, 96)):
    """Turns the anti-aliased vector render into hard-edged pixel art: every pixel is either fully opaque or empty
    (coverage >= 50% wins), plus a hard 1px drop shadow. The engine scales cursors by whole numbers only (nearest),
    so they stay crisp at every UI scale."""
    w, h = cv.w, cv.h
    solid = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            c = cv.get(x, y)
            if c[3] >= 128:
                cv.set(x, y, (c[0], c[1], c[2], 255))
                solid[y][x] = True
            else:
                cv.set(x, y, (0, 0, 0, 0))
    for y in range(1, h):
        for x in range(1, w):
            if not solid[y][x] and solid[y - 1][x - 1]:
                cv.set(x, y, shadow)


DIRS = {'t': 0, 'tr': 45, 'r': 90, 'br': 135, 'b': 180, 'bl': 225, 'l': 270, 'tl': 315}


def build(mod):
    frames = []
    entries = []

    def add(name, fn):
        g = G(SIZE, SIZE, 1)
        hx, hy = fn(g)
        pixelize(g.cv)
        frames.append(g.cv)
        entries.append((name, len(frames) - 1, int(math.floor(hx + 1e-6)) - SIZE // 2, int(math.floor(hy + 1e-6)) - SIZE // 2))

    add('default', c_default)
    add('select', c_select)
    add('generic-blocked', blocked)
    add('move', move)
    add('move-blocked', blocked)
    add('sell', sell)
    add('sell-blocked', blocked)
    add('city-road', tool('road'))
    add('city-zone', tool('zone'))
    add('city-bulldoze', tool('bulldoze'))
    add('city-place', tool('place'))
    add('city-blocked', blocked)

    def joy_all(g):
        for a in (0, 90, 180, 270):
            pts = rot([(0, -12), (4.5, -6.5), (1.5, -6.5), (1.5, -3), (-1.5, -3), (-1.5, -6.5), (-4.5, -6.5)], a, 16, 16)
            outlined(g, pts, WHITE, 2.2)
        g.circle(16, 16, 2.2, OUTLINE)
        g.circle(16, 16, 1.2, TEAL)
        return (16, 16)
    add('joystick-all', joy_all)
    for d, deg in DIRS.items():
        add('scroll-' + d, lambda g, deg=deg: scroll_arrow(g, deg, WHITE, True))
    for d, deg in DIRS.items():
        add('scroll-%s-blocked' % d, lambda g, deg=deg: scroll_arrow(g, deg, (130, 104, 78, 255), True))
    for d, deg in DIRS.items():
        add('joystick-' + d, lambda g, deg=deg: scroll_arrow(g, deg, TEAL_LIGHT, False))
    for d, deg in DIRS.items():
        add('joystick-%s-blocked' % d, lambda g, deg=deg: scroll_arrow(g, deg, (130, 104, 78, 255), False))

    out_dir = os.path.join(mod, 'bits', 'cursors')
    save_sheet(os.path.join(out_dir, 'cursors.png'), frames, cols=8)
    lines = ['# GENERATED by tools/genui.py - do not edit by hand.', 'Cursors:', '\tcursors.png:']
    for name, idx, x, y in entries:
        lines.append('\t\t%s:' % name)
        lines.append('\t\t\tStart: %d' % idx)
        lines.append('\t\t\tX: %d' % x)
        lines.append('\t\t\tY: %d' % y)
    with open(os.path.join(mod, 'cursors.yaml'), 'w') as f:
        f.write('\n'.join(lines) + '\n')
