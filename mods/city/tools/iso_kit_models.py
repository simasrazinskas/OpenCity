"""Reference models for the style bible (house, shop, factory, tower, road piece, people, cars).

These only prove the look; the real building/vehicle sets belong to ZONED/CIVIC/NET/LIFE.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402

M = ik.mat


def house(seed=1):
    s = ik.Scene((1, 1), seed)
    s.tile(0, 0, 'grass')
    s.ground([(0.44, 0.8), (0.56, 0.8), (0.56, 1.0), (0.44, 1.0)], 'paving', layer=1)
    s.box(0.15, 0.22, 0, 0.7, 0.58, 2, 'stone')
    s.box(0.16, 0.23, 2, 0.68, 0.56, 18, M('windows_plaster', ramp='sand', shade=3, win_w=4, period=9))
    s.box(0.45, 0.79, 2, 0.1, 0.015, 8, 'door')
    s.roof_shed(0.42, 0.79, 11, 0.16, 0.06, 2, 'roof_tiles', high='-y')
    s.box(0.64, 0.33, 20, 0.08, 0.08, 18, 'brick', top='roof_flat')
    s.roof_gable(0.16, 0.23, 20, 0.68, 0.56, 15, 'roof_tiles', axis='x', gable=M('windows_plaster', ramp='sand', shade=3))
    return s


def shop(seed=2):
    s = ik.Scene((1, 1), seed)
    s.tile(0, 0, 'paving')
    wall = M('shopfront', base=M('plaster', ramp='sand', shade=2.5), sign='teal', ramp='sand', shade=2.5)
    s.box(0.1, 0.1, 0, 0.8, 0.8, 26, wall)
    for (a, b) in (((0.13, 0.9), (0.87, 0.9)),):
        s.poly([(a[0], a[1], 12), (b[0], b[1], 12), (b[0], b[1] + 0.09, 8), (a[0], a[1] + 0.09, 8)], 'awning',
               outward=(0, 1, 1), cull=False)
    s.poly([(0.9, 0.13, 12), (0.9, 0.87, 12), (0.99, 0.87, 8), (0.99, 0.13, 8)], M('awning', ramp='teal'),
           outward=(1, 0, 1), cull=False)
    s.roof_flat(0.1, 0.1, 26, 0.8, 0.8, 'roof_flat', parapet=2, rim=M('plaster', ramp='sand', shade=1.5))
    s.box(0.25, 0.25, 26, 0.18, 0.14, 5, 'metal_light')
    s.box(0.55, 0.3, 26, 0.1, 0.1, 3, 'metal')
    return s


def factory(seed=3):
    s = ik.Scene((2, 2), seed)
    s.tile(0, 0, 'concrete_ground'); s.tile(1, 0, 'concrete_ground')
    s.tile(0, 1, 'concrete_ground'); s.tile(1, 1, 'concrete_ground')
    hall = M('windows_brick', storey=24, win_w=5, win_h=10, period=9, sill=5, glass='slate')
    s.box(0.1, 0.25, 0, 1.4, 1.5, 24, hall)
    s.box(0.5, 1.75, 0, 0.3, 0.015, 16, 'garage')
    for i in range(4):
        s.roof_shed(0.1 + i * 0.35, 0.25, 24, 0.35, 1.5, 10, 'roof_metal', high='+x',
                    wall=M('glass', panel=3))
    s.box(1.55, 1.15, 0, 0.35, 0.65, 20, M('windows_concrete', win_w=4, period=7))
    s.roof_flat(1.55, 1.15, 20, 0.35, 0.65, 'roof_gravel', parapet=1, rim='concrete')
    s.box(1.62, 1.82, 0, 0.1, 0.015, 8, 'door_glass')
    s.cylinder(1.72, 0.55, 0, 0.1, 64, 'chimney_bands', top=M('plain', ramp='grey', shade=-3))
    s.cylinder(1.72, 0.55, 64, 0.12, 3, M('plain', ramp='grey', shade=-1))
    s.cylinder(1.62, 0.92, 0, 0.12, 18, M('metal_light'), top='roof_metal')
    s.dome(1.62, 0.92, 18, 0.12, 4, 'roof_metal')
    return s


def tower(seed=4, storeys=20):
    s = ik.Scene((1, 1), seed)
    s.tile(0, 0, 'paving')
    s.box(0.06, 0.06, 0, 0.88, 0.88, 12, M('shopfront', base='concrete', ramp='grey', shade=1.2, sign='slate',
                                          shop_h=12))
    s.roof_flat(0.06, 0.06, 12, 0.88, 0.88, 'roof_flat', parapet=1, rim='concrete')
    h = storeys * ik.STOREY
    s.box(0.16, 0.16, 12, 0.68, 0.68, h, M('glass', storey=10))
    s.box(0.2, 0.2, 12 + h, 0.6, 0.6, 8, 'concrete', top='roof_flat')
    s.box(0.36, 0.36, 20 + h, 0.24, 0.2, 6, 'metal_light')
    s.box(0.48, 0.48, 20 + h, 0.02, 0.02, 26, M('plain', ramp='grey', shade=-1))
    s.box(0.475, 0.475, 45 + h, 0.03, 0.03, 2, M('lamp', ramp='red', eramp='red'))
    return s


def road(seed=5, lamp=True):
    s = ik.Scene((1, 1), seed)
    s.tile(0, 0, 'asphalt')
    for y0 in (0.0, 0.82):
        s.box(0, y0, 0, 1, 0.18, 2, M('plain', ramp='stone', shade=1.5), top='paving')
    for k in range(3):
        x = k / 3 + 0.06
        s.ground([(x, 0.49), (x + 0.2, 0.49), (x + 0.2, 0.52), (x, 0.52)], 'marking', layer=1)
    if not lamp:
        return s
    s.box(0.7, 0.88, 2, 0.03, 0.03, 26, M('plain', ramp='slate', shade=0))
    s.box(0.6, 0.88, 26, 0.13, 0.03, 2, M('plain', ramp='slate', shade=0))
    s.box(0.6, 0.88, 25, 0.06, 0.03, 1, 'lamp')
    return s


def person(seed=6, shirt='red', trousers='slate', skin='sand'):
    s = ik.Scene((1, 1), seed)
    c = 0.5
    s.box(c - 0.02, c - 0.012, 0, 0.04, 0.024, 3, M('plain', ramp=trousers, shade=-1))
    s.box(c - 0.024, c - 0.014, 3, 0.048, 0.028, 3, M('plain', ramp=shirt, shade=0))
    s.box(c - 0.014, c - 0.012, 6, 0.028, 0.024, 2, M('plain', ramp=skin, shade=1))
    return s


def car(seed=7, paint='red'):
    s = ik.Scene((1, 1), seed)
    body = M('plain', ramp=paint, shade=0.5, snow=False)
    x0, y0, L, W = 0.36, 0.44, 0.28, 0.12
    for wx in (x0 + 0.05, x0 + L - 0.06):
        for wy in (y0, y0 + W):
            s.cylinder(wx, wy - 0.01, 2, 0.022, 0.02, M('plain', ramp='grey', shade=-6), axis='y', segs=8)
    s.box(x0, y0, 2, L, W, 4, body)
    s.box(x0 + 0.07, y0 + 0.01, 6, 0.14, W - 0.02, 3, M('plain', ramp='glass', shade=-1.5, snow=False), top=body)
    return s


def bus(seed=8, paint='yellow'):
    s = ik.Scene((1, 1), seed)
    x0, y0, L, W = 0.12, 0.42, 0.76, 0.16
    for wx in (x0 + 0.1, x0 + L - 0.12):
        for wy in (y0, y0 + W):
            s.cylinder(wx, wy - 0.01, 2.5, 0.03, 0.02, M('plain', ramp='grey', shade=-6), axis='y', segs=8)
    side = M('windows_plaster', base=M('plain', ramp=paint, shade=0.5), ramp=paint, shade=0.5, storey=11,
             win_w=6, win_h=4, period=8, sill=5, margin=2, glass='glass')
    s.box(x0, y0, 2, L, W, 11, side, top=M('plain', ramp='grey', shade=2))
    return s


def tower3(seed=9, storeys=30):
    """3x3 office tower on a podium (thumbnail / scale test)."""
    s = ik.Scene((3, 3), seed)
    for cx in range(3):
        for cy in range(3):
            s.tile(cx, cy, 'paving')
    s.box(0.15, 0.15, 0, 2.7, 2.7, 20, M('shopfront', base='concrete', ramp='grey', shade=1.2, sign='slate', shop_h=12))
    s.roof_flat(0.15, 0.15, 20, 2.7, 2.7, 'roof_gravel', parapet=2, rim='concrete')
    h = storeys * ik.STOREY
    s.box(0.6, 0.6, 20, 1.8, 1.8, h, M('glass', storey=10))
    s.box(0.8, 0.8, 20 + h, 1.4, 1.4, 12, 'concrete', top='roof_flat')
    s.box(1.3, 1.3, 32 + h, 0.4, 0.4, 10, 'metal_light')
    s.box(1.48, 1.48, 42 + h, 0.04, 0.04, 40, M('plain', ramp='grey', shade=-1))
    return s
