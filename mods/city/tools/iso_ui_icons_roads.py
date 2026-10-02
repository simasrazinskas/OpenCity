"""OpenCity RCT2-style icons: road types, road tools, prefabs, junction control, add-ons, zones.

Roads run along world x (lower-right to upper-left on screen) across a grass tile, like core's cat_roads.
A world-y strip of width w is about w pixels thick on screen (16-grid)."""

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import grass_tile, bolt, drop, person, lens, badge

Z = 1.5      # road surface height on a grass tile
MID = -3.5   # road centre line (world y)


# ---- helpers -----------------------------------------------------------------------------------------
def strip(I, y0, y1, c, s, x0=0.0, x1=-7.0, z=Z):
    """Flat strip along world x between world y0 > y1 (both <= 0)."""
    I.isoquad(8, 15, [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], c, s)


def dashes(I, y, c, s, t=0.8, n=3, x0=-0.5, x1=-6.5, frac=0.55, z=Z):
    step = (x0 - x1) / n
    for i in range(n):
        a = x0 - i * step
        strip(I, y + t / 2, y - t / 2, c, s, a, a - step * frac, z)


def road(I, w, base="grey", bs=3, y=MID):
    grass_tile(I)
    strip(I, y + w / 2, y - w / 2, base, bs)
    return y + w / 2, y - w / 2


def node(I, x, y, c="red"):
    I.ellipse(x, y, 1.9, 1.9, c, 3, after=True)
    I.ellipse(x, y, 1.4, 1.4, c, 5, after=True)
    I.ellipse(x, y, 0.7, 0.7, "#ffffff", after=True)


def path(I, pts, t=2.6, c="grey", s=3):
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        I.line(ax, ay, bx, by, c, s, t)
    if t > 1.5:
        for x, y in pts:
            I.ellipse(x, y, t / 2, t / 2, c, s)


def bez(p0, p1, p2, n=8):
    return [((1 - u) ** 2 * p0[0] + 2 * u * (1 - u) * p1[0] + u * u * p2[0],
             (1 - u) ** 2 * p0[1] + 2 * u * (1 - u) * p1[1] + u * u * p2[1])
            for u in [i / n for i in range(n + 1)]]


def tree(I, x, y, r=1.4):
    I.rect(x - 0.4, y, 0.8, 1.2, "brown", 3)
    I.ellipse(x, y - 0.2, r, r, "green", 1)
    I.ellipse(x - 0.2, y - 0.4, r * 0.82, r * 0.82, "green", 3)
    I.ellipse(x - 0.5, y - 0.8, r * 0.45, r * 0.4, "green", 6)


# ---- road types ---------------------------------------------------------------------------------------
@icon("road_street", "roads", "Street")
def _(I):
    a, b = road(I, 4.0)
    strip(I, a, a - 0.5, "grey", 5)
    strip(I, b + 0.5, b, "grey", 2)
    dashes(I, MID, "yellow", 6, 0.8, 3)


@icon("road_gravel", "roads", "Gravel road")
def _(I):
    a, b = road(I, 4.0, "brown", 5)
    strip(I, a, a - 0.5, "brown", 6)
    strip(I, b + 0.5, b, "brown", 3)
    for x, y in ((-1.5, -3.0), (-3.0, -4.0), (-4.5, -3.0), (-5.8, -3.9), (-2.2, -3.9)):
        strip(I, y + 0.3, y - 0.3, "brown", 3, x, x - 0.6)


@icon("road_avenue", "roads", "Avenue")
def _(I):
    a, b = road(I, 5.6)
    strip(I, a, a - 0.5, "grey", 5)
    strip(I, b + 0.5, b, "grey", 2)
    strip(I, MID + 0.7, MID + 0.05, "yellow", 6)
    strip(I, MID - 0.05, MID - 0.7, "yellow", 5)
    dashes(I, MID + 1.7, "grey", 6, 0.5, 3)
    dashes(I, MID - 1.7, "grey", 6, 0.5, 3)


@icon("road_boulevard", "roads", "Boulevard")
def _(I):
    a, b = road(I, 6.6)
    strip(I, MID + 0.9, MID - 0.9, "green", 5)
    strip(I, MID + 0.9, MID + 0.6, "green", 7)
    strip(I, MID - 0.6, MID - 0.9, "green", 2)
    dashes(I, MID + 2.0, "grey", 6, 0.5, 3)
    dashes(I, MID - 2.0, "grey", 6, 0.5, 3)
    for x in (-2.0, -5.0):
        px, py = I.P(8, 15, x, MID, Z)
        tree(I, px, py - 1.0, 1.3)


@icon("road_highway", "roads", "Highway")
def _(I):
    a, b = road(I, 6.4, "grey", 2)
    strip(I, a, a - 0.5, "#ffffff", 7)
    strip(I, b + 0.5, b, "#ffffff", 5)
    strip(I, MID + 0.5, MID - 0.5, "grey", 6, 0, -7, Z + 0.8)
    strip(I, MID - 0.5, MID - 0.9, "grey", 3, 0, -7, Z + 0.8)
    dashes(I, MID + 1.8, "#ffffff", 7, 0.5, 3)
    dashes(I, MID - 1.8, "#ffffff", 6, 0.5, 3)


@icon("road_alley", "roads", "Alley")
def _(I):
    a, b = road(I, 2.4, "grey", 2)
    strip(I, a, a - 0.5, "grey", 4)
    strip(I, b + 0.5, b, "grey", 1)


@icon("road_path", "roads", "Path")
def _(I):
    a, b = road(I, 2.8, "sand", 5)
    strip(I, a, a - 0.5, "sand", 7)
    strip(I, b + 0.5, b, "sand", 3)
    for x in (-1.5, -3.5, -5.5):
        strip(I, MID + 0.15, MID - 0.15, "sand", 3, x, x - 0.15)
        strip(I, a, b, "sand", 3, x, x - 0.35)


# ---- road tool modes ----------------------------------------------------------------------------------
import math


def rd(I, pts, t=3.4):
    """Road polyline in screen space: light kerb edge, grey surface."""
    path(I, pts, t + 1.2, "grey", 6)
    path(I, pts, t, "grey", 3)


def arrow(I, x0, y0, x1, y1, w=2.2, c="#ffffff", s=7):
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy)
    ux, uy = dx / L, dy / L
    nx, ny = -uy, ux
    hx, hy = x1 - ux * w * 1.1, y1 - uy * w * 1.1
    I.line(x0, y0, hx, hy, c, s, 0.9, after=True)
    I.poly([(x1, y1), (hx + nx * w * 0.6, hy + ny * w * 0.6), (hx - nx * w * 0.6, hy - ny * w * 0.6)], c, s, after=True)


def circ_arrow(I, cx, cy, r, a0, a1, c="#ffffff", s=7, t=1.5, hs=1.0):
    steps = max(4, int(abs(a1 - a0) / 12))
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        I.ellipse(cx + r * math.cos(a), cy + r * math.sin(a), t / 2, t / 2, c, s, after=True)
    a = math.radians(a1)
    px, py = cx + r * math.cos(a), cy + r * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)
    nx, ny = math.cos(a), math.sin(a)
    I.poly([(px + tx * 2.2 * hs, py + ty * 2.2 * hs), (px + nx * 1.9 * hs, py + ny * 1.9 * hs), (px - nx * 1.9 * hs, py - ny * 1.9 * hs)], c, s, after=True)


@icon("mode_straight", "roads", "Straight road")
def _(I):
    rd(I, [(3.5, 11.5), (12.5, 4.5)])
    node(I, 3.5, 11.5)
    node(I, 12.5, 4.5)


@icon("mode_curve", "roads", "Curved road")
def _(I):
    rd(I, bez((3, 12.5), (3, 3.5), (13, 3.5), 10))
    I.line(3, 12.5, 3, 3.5, "blue", 6, 0.8, after=True)
    I.line(3, 3.5, 13, 3.5, "blue", 6, 0.8, after=True)
    node(I, 3, 12.5)
    node(I, 13, 3.5)
    I.rect(2, 2.5, 2, 2, "blue", 5, after=True)
    I.rect(2.4, 2.9, 1.2, 1.2, "blue", 7, after=True)


@icon("mode_freeform", "roads", "Freeform road")
def _(I):
    rd(I, bez((3, 13), (3, 7), (8, 8.5), 6) + bez((8, 8.5), (13, 10), (13, 3.5), 6)[1:])
    node(I, 3, 13)
    node(I, 13, 3.5)


@icon("mode_grid", "roads", "Road grid")
def _(I):
    grass_tile(I)
    for c in (-1.7, -5.3):
        strip(I, c + 0.55, c - 0.55, "grey", 3)
        I.isoquad(8, 15, [(c + 0.55, 0, Z), (c + 0.55, -7, Z), (c - 0.55, -7, Z), (c - 0.55, 0, Z)], "grey", 3)
    for c in (-1.7, -5.3):
        strip(I, c + 0.55, c - 0.55, "grey", 4)


@icon("mode_oneway", "roads", "One-way road")
def _(I):
    rd(I, [(2.5, 11.5), (13.5, 5)], 3.8)
    arrow(I, 4.5, 10.3, 11.5, 6.2, 3.0)


@icon("mode_replace", "roads", "Replace road")
def _(I):
    rd(I, [(2, 12), (14, 5.5)], 3.6)
    for c, sh, t, hs in (("grey", 0, 3.2, 1.35), ("orange", 6, 1.5, 1.0)):
        circ_arrow(I, 8, 8.3, 3.8, 200, 320, c, sh, t, hs)
        circ_arrow(I, 8, 8.3, 3.8, 20, 140, c, sh, t, hs)


@icon("mode_paired", "roads", "Paired one-way roads")
def _(I):
    rd(I, [(2, 7.5), (11, 3)], 2.8)
    rd(I, [(5, 13), (14, 8.5)], 2.8)
    arrow(I, 4.5, 6.4, 9.5, 3.9, 2.2)
    arrow(I, 12.5, 9.5, 7.0, 12.2, 2.2)


@icon("mode_bridge", "roads", "Bridge")
def _(I):
    I.isotile(8, 15, 7, 7, "blue", 5, 1.5, "sand")
    I.isobox(10, 12.5, 7, 3, 3.5, "grey", 3, 5, 2)
    strip(I, -2, -2.6, "grey", 7, 0, -7, 5)
    dashes(I, -3.5, "yellow", 6, 0.6, 3, z=5)
    pts = [(6.5 + 2.6 * math.cos(math.radians(a)), 11 - 2.8 * math.sin(math.radians(a))) for a in range(0, 181, 20)]
    I.poly(pts, "blue", 2)


@icon("mode_tunnel", "roads", "Tunnel")
def _(I):
    grass_tile(I)
    I.ellipse(8, 8.5, 6.6, 5.2, "green", 4)
    I.ellipse(6.8, 7.2, 4.6, 3.3, "green", 6)
    I.ellipse(8, 9.6, 3.6, 3.6, "grey", 5)
    I.rect(4.4, 9.6, 7.2, 3.4, "grey", 5)
    I.ellipse(8, 9.8, 2.6, 2.7, "grey", 1)
    I.rect(5.4, 9.8, 5.2, 3.2, "grey", 1)
    I.poly([(6.2, 13), (9.8, 13), (10.4, 14), (5.6, 14)], "grey", 3)


# ---- prefabs ------------------------------------------------------------------------------------------
def island(I, rx, ry, cx=8.0, cy=10.0):
    I.ellipse(cx, cy, rx, ry, "grey", 3)
    I.ellipse(cx, cy - 0.2, rx - 1.0, ry - 0.7, "grey", 6)
    I.ellipse(cx, cy - 0.3, rx - 1.7, ry - 1.1, "green", 5)


@icon("prefab_roundabout", "roads", "Roundabout")
def _(I):
    grass_tile(I)
    strip(I, MID + 0.9, MID - 0.9, "grey", 3)
    I.isoquad(8, 15, [(MID + 0.9, 0, Z), (MID + 0.9, -7, Z), (MID - 0.9, -7, Z), (MID - 0.9, 0, Z)], "grey", 3)
    island(I, 4.4, 2.5)
    I.ellipse(8, 9.5, 1.0, 0.8, "green", 7)


@icon("prefab_roundabout_large", "roads", "Large roundabout")
def _(I):
    grass_tile(I)
    for c in (MID,):
        strip(I, c + 1.2, c - 1.2, "grey", 3)
        I.isoquad(8, 15, [(c + 1.2, 0, Z), (c + 1.2, -7, Z), (c - 1.2, -7, Z), (c - 1.2, 0, Z)], "grey", 3)
    island(I, 6.2, 3.3)
    tree(I, 8, 9.6, 1.4)


@icon("prefab_ramp", "roads", "Highway ramp")
def _(I):
    grass_tile(I)
    strip(I, -1.6, -4.6, "grey", 2)
    dashes(I, -3.1, "#ffffff", 7, 0.7, 3, -0.8, -6.6)
    I.isoquad(8, 15, [(-0.2, -5.0, Z), (-2.4, -5.0, Z), (-5.6, -3.4, Z), (-6.9, -3.4, Z), (-6.9, -4.6, Z), (-3.4, -6.4, Z), (-0.2, -6.4, Z)], "grey", 5)
    strip(I, -5.4, -5.8, "grey", 7, -0.2, -3.0)


# ---- junction control ---------------------------------------------------------------------------------
def oct_pts(cx, cy, r):
    return [(cx + r * math.cos(math.radians(22.5 + 45 * i)), cy + r * math.sin(math.radians(22.5 + 45 * i))) for i in range(8)]


@icon("ctl_yield", "roads", "Yield")
def _(I):
    I.rect(7.2, 10, 1.6, 5, "grey", 4)
    I.poly([(1.5, 1.5), (14.5, 1.5), (8, 13)], "red", 4)
    I.poly([(3.9, 3.2), (12.1, 3.2), (8, 10.4)], "#ffffff")
    I.poly([(8, 9.4), (4.9, 3.9), (8, 3.9)], "grey", 6)


@icon("ctl_stop", "roads", "Stop")
def _(I):
    I.rect(7.2, 11, 1.6, 4, "grey", 4)
    I.poly(oct_pts(8, 7, 6.4), "red", 3)
    I.poly(oct_pts(8, 7, 5.6), "#ffffff")
    I.poly(oct_pts(8, 7, 4.5), "red", 5)
    I.rect(4.6, 6.1, 6.8, 1.8, "#ffffff")


@icon("ctl_signal", "roads", "Traffic signal")
def _(I):
    I.rect(7.2, 12, 1.6, 3, "grey", 4)
    I.rect(4.5, 1, 7, 12, "grey", 2)
    I.rect(4.5, 1, 1.5, 12, "grey", 4)
    I.ellipse(8, 3.8, 1.8, 1.8, "red", 5)
    I.ellipse(7.5, 3.3, 0.7, 0.7, "red", 7)
    I.ellipse(8, 7, 1.8, 1.8, "yellow", 3)
    I.ellipse(8, 10.2, 1.8, 1.8, "green", 3)


@icon("ctl_default", "roads", "Default junction")
def _(I):
    grass_tile(I)
    for c in (MID,):
        strip(I, c + 1.3, c - 1.3, "grey", 3)
        I.isoquad(8, 15, [(c + 1.3, 0, Z), (c + 1.3, -7, Z), (c - 1.3, -7, Z), (c - 1.3, 0, Z)], "grey", 3)
    for k in (-0.6, -2.0, -5.0, -6.4):
        I.isoquad(8, 15, [(k, MID + 0.5, Z), (k - 0.5, MID + 0.5, Z), (k - 0.5, MID - 0.5, Z), (k, MID - 0.5, Z)], "#ffffff", 7)
        I.isoquad(8, 15, [(MID + 0.5, k, Z), (MID + 0.5, k - 0.5, Z), (MID - 0.5, k - 0.5, Z), (MID - 0.5, k, Z)], "#ffffff", 7)


# ---- road add-ons -------------------------------------------------------------------------------------
def lane_road(I, w=3.0, y=-3.2):
    grass_tile(I)
    strip(I, y + w / 2, y - w / 2, "grey", 3)
    return y + w / 2, y - w / 2


@icon("addon_trees", "roads", "Street trees")
def _(I):
    lane_road(I, 2.4, -2.6)
    for x, y in ((-1.2, -5.4), (-4.4, -5.4)):
        px, py = I.P(8, 15, x, y, Z)
        tree(I, px, py - 1.3, 1.9)


@icon("addon_barrier", "roads", "Sound barrier")
def _(I):
    lane_road(I, 2.6, -2.6)
    I.isobox(13.4, 10.4, 6.8, 1.0, 4.5, "grey", 7, 6, 3)
    for k in (2, 4.2):
        I.isoquad(13.4, 10.4, [(-k, 0, 1.4), (-k - 0.3, 0, 1.4), (-k - 0.3, 0, 4.2), (-k, 0, 4.2)], "grey", 4)


@icon("addon_lights", "roads", "Street lamp")
def _(I):
    lane_road(I, 2.4, -2.6)
    I.rect(11.8, 3.2, 1.0, 7.6, "grey", 3)
    I.rect(11.8, 3.2, 0.4, 7.6, "grey", 5)
    I.rect(8.8, 2.2, 3.6, 1.0, "grey", 4)
    I.ellipse(8.6, 4.0, 1.7, 1.1, "yellow", 5)
    I.ellipse(8.2, 3.7, 0.8, 0.5, "yellow", 7)


@icon("addon_parking", "roads", "Parking")
def _(I):
    I.rect(7.2, 11, 1.6, 4, "grey", 4)
    I.rect(2, 1, 12, 11, "#ffffff")
    I.rect(3, 2, 10, 9, "blue", 4)
    I.rect(3, 2, 10, 1, "blue", 5)
    I.text(5.6, 2.6, "P", "#ffffff")


@icon("addon_buslane", "roads", "Bus lane")
def _(I):
    grass_tile(I)
    strip(I, -1.0, -5.8, "grey", 3)
    strip(I, -1.0, -3.4, "red", 4)
    strip(I, -3.4, -3.8, "#ffffff", 6)
    I.isobox(8.6, 12.0, 3.4, 1.4, 2.4, "yellow", 6, 5, 3)
    I.isoquad(8.6, 12.0, [(-0.6, 0, 1.0), (-1.4, 0, 1.0), (-1.4, 0, 1.8), (-0.6, 0, 1.8)], "blue", 2)
    I.isoquad(8.6, 12.0, [(-2.0, 0, 1.0), (-2.8, 0, 1.0), (-2.8, 0, 1.8), (-2.0, 0, 1.8)], "blue", 2)


@icon("addon_bikelane", "roads", "Bike lane")
def _(I):
    grass_tile(I)
    strip(I, -0.6, -6.2, "green", 7)
    strip(I, -0.6, -1.1, "#ffffff", 7)
    strip(I, -5.7, -6.2, "#ffffff", 6)
    for cx in (4.4, 11.6):
        I.ring(cx, 9.8, 2.8, 1.0, "grey", 1, after=True)
    for seg in ((4.4, 9.8, 7.6, 6.2), (7.6, 6.2, 11.6, 9.8), (4.4, 9.8, 8.6, 9.8), (9.8, 5.2, 11.6, 9.8), (8.8, 5.2, 10.6, 5.2), (6.4, 6.2, 8.8, 6.2)):
        I.line(*seg, "grey", 1, 0.9, after=True)


@icon("addon_remove", "roads", "Remove add-on")
def _(I):
    lane_road(I, 2.4, -2.6)
    px, py = I.P(8, 15, -1.6, -5.4, Z)
    tree(I, px, py - 1.3, 1.9)
    I.line(7.0, 1.5, 14.5, 9.0, "red", 0, 3.4, after=True)
    I.line(14.5, 1.5, 7.0, 9.0, "red", 0, 3.4, after=True)
    I.line(7.4, 2.0, 14.1, 8.6, "red", 5, 2.0, after=True)
    I.line(14.1, 2.0, 7.4, 8.6, "red", 5, 2.0, after=True)


# ---- zones --------------------------------------------------------------------------------------------
def zt(I, ramp):
    """Zone-coloured tile: darker rim, lighter painted centre."""
    I.isotile(8, 15, 7, 7, ramp, 4, 1.5, "brown")
    I.isoquad(8, 15, [(-0.7, -0.7, Z), (-6.3, -0.7, Z), (-6.3, -6.3, Z), (-0.7, -6.3, Z)], ramp, 5)


def I_P(x, y, z=Z):
    """Screen position of a ground point on the zone tile (world x, y <= 0)."""
    return (8 + (x - y), 15 + (x + y) * 0.5 - z)


def wl(fx, fy, I, xs, z0, z1, c="blue", s=2, w=0.8):
    """Windows on the lit +Y face (x positions are the right edges)."""
    for x in xs:
        I.isoquad(fx, fy, [(x, 0, z0), (x - w, 0, z0), (x - w, 0, z1), (x, 0, z1)], c, s)


def wr(fx, fy, I, ys, z0, z1, c="blue", s=2, w=0.8):
    """Windows on the shaded +X face (y positions are the front edges)."""
    for y in ys:
        I.isoquad(fx, fy, [(0, y, z0), (0, y - w, z0), (0, y - w, z1), (0, y, z1)], c, s)


def band_l(fx, fy, I, lx, z0, z1, c, s, m=0.3):
    I.isoquad(fx, fy, [(-m, 0, z0), (-lx + m, 0, z0), (-lx + m, 0, z1), (-m, 0, z1)], c, s)


def band_r(fx, fy, I, ly, z0, z1, c, s, m=0.3):
    I.isoquad(fx, fy, [(0, -m, z0), (0, -ly + m, z0), (0, -ly + m, z1), (0, -m, z1)], c, s)


@icon("zone_res_low", "zones", "Low-density residential")
def _(I):
    zt(I, "green")
    fx, fy = I_P(-1.6, -1.6)
    I.isobox(fx, fy, 3.2, 2.6, 2.2, "sand", 7, 6, 4)
    I.gable(fx, fy, 3.2, 2.6, 2.2, 1.7, "red", "x", 5, 3)
    wl(fx, fy, I, (-0.7,), 0.6, 1.8, "brown", 3, 0.9)
    wl(fx, fy, I, (-2.2,), 0.9, 1.8, "blue", 3, 0.9)


@icon("zone_res_row", "zones", "Row houses")
def _(I):
    zt(I, "green")
    fx, fy = I_P(-0.7, -2.0)
    I.isobox(fx, fy, 5.6, 2.6, 2.2, "sand", 7, 6, 4)
    I.gable(fx, fy, 5.6, 2.6, 2.2, 1.6, "red", "x", 5, 3)
    wl(fx, fy, I, (-0.8, -2.5, -4.2), 0.5, 1.7, "blue", 3, 0.9)


@icon("zone_res_med", "zones", "Medium-density residential")
def _(I):
    zt(I, "green")
    fx, fy = I_P(-1.4, -1.4)
    I.isobox(fx, fy, 3.6, 3.2, 5.2, "sand", 7, 6, 4)
    for z in (0.7, 2.4, 4.0):
        wl(fx, fy, I, (-0.5, -2.0), z, z + 1.1, "blue", 3, 0.9)
        wr(fx, fy, I, (-0.6, -1.9), z, z + 1.1, "blue", 2, 0.8)


@icon("zone_res_high", "zones", "High-density residential")
def _(I):
    zt(I, "green")
    fx, fy = I_P(-1.8, -1.8)
    I.isobox(fx, fy, 2.8, 2.8, 8.4, "sand", 7, 6, 4)
    for z in (0.8, 2.6, 4.4, 6.2):
        wl(fx, fy, I, (-0.5, -1.8), z, z + 1.0, "blue", 3, 0.8)
        wr(fx, fy, I, (-0.5, -1.7), z, z + 1.0, "blue", 2, 0.8)


@icon("zone_res_mixed", "zones", "Mixed-use residential")
def _(I):
    zt(I, "green")
    I.isoquad(8, 15, [(-0.7, -3.7, Z), (-6.3, -3.7, Z), (-6.3, -6.3, Z), (-0.7, -6.3, Z)], "blue", 5)
    fx, fy = I_P(-1.0, -1.1)
    I.isobox(fx, fy, 3.4, 2.6, 5.0, "sand", 7, 6, 4)
    I.isoquad(fx, fy, [(-0.4, 0, 0.3), (-3.0, 0, 0.3), (-3.0, 0, 1.7), (-0.4, 0, 1.7)], "blue", 7)
    I.isoquad(fx, fy, [(0.2, 0.6, 1.7), (-3.6, 0.6, 1.7), (-3.4, 0, 2.5), (0, 0, 2.5)], "blue", 4)
    wl(fx, fy, I, (-0.6, -2.0), 3.2, 4.4, "green", 3, 0.9)
    wr(fx, fy, I, (-0.5, -1.5), 3.2, 4.4, "green", 2, 0.8)


@icon("zone_res_lowrent", "zones", "Low-rent residential")
def _(I):
    zt(I, "green")
    fx, fy = I_P(-1.2, -1.2)
    I.isobox(fx, fy, 3.8, 3.4, 4.2, "grey", 6, 4, 3)
    for z in (0.8, 2.5):
        wl(fx, fy, I, (-0.6, -2.2), z, z + 1.0, "grey", 1, 0.9)
        wr(fx, fy, I, (-0.7, -2.0), z, z + 1.0, "grey", 1, 0.8)


@icon("zone_com_low", "zones", "Low-density commercial")
def _(I):
    zt(I, "blue")
    fx, fy = I_P(-1.0, -1.4)
    I.isobox(fx, fy, 4.2, 3.0, 3.2, "sand", 7, 6, 4)
    wl(fx, fy, I, (-0.5,), 0.0, 1.6, "brown", 3, 0.9)
    wl(fx, fy, I, (-1.9, -3.1), 0.5, 1.5, "blue", 6, 1.0)
    for i in range(3):
        c, s = (("red", 5), ("#ffffff", 7))[i % 2]
        x0 = 0.4 - i * 1.4
        I.isoquad(fx, fy, [(x0, 0.8, 2.0), (x0 - 1.4, 0.8, 2.0), (x0 - 1.4, 0, 2.9), (x0, 0, 2.9)], c, s)


@icon("zone_com_high", "zones", "High-density commercial")
def _(I):
    zt(I, "blue")
    fx, fy = I_P(-1.8, -1.8)
    I.isobox(fx, fy, 2.8, 2.8, 7.6, "teal", 7, 5, 3)
    for x in (-0.6, -1.6, -2.6):
        wl(fx, fy, I, (x + 0.35,), 0.4, 7.2, "blue", 7, 0.35)
    for y in (-0.6, -1.6, -2.6):
        wr(fx, fy, I, (y + 0.35,), 0.4, 7.2, "blue", 5, 0.35)
    I.rect(fx - 0.4, fy - 1.4 - 7.6 - 1.4, 0.8, 1.8, "grey", 4)


@icon("zone_ind", "zones", "Industrial")
def _(I):
    zt(I, "yellow")
    fx, fy = I_P(-0.8, -2.0)
    I.isobox(fx, fy, 4.8, 3.4, 2.6, "grey", 6, 5, 3)
    I.gable(fx, fy, 4.8, 3.4, 2.6, 1.2, "grey", "x", 7, 4)
    wl(fx, fy, I, (-0.7, -2.2, -3.7), 0.6, 1.6, "grey", 1, 0.9)
    cx, cy = I_P(-1.2, -6.0)
    I.rect(cx - 0.9, cy - 6.4, 1.8, 6.0, "red", 4)
    I.rect(cx - 0.9, cy - 6.4, 0.7, 6.0, "red", 6)
    I.rect(cx - 0.9, cy - 6.4, 1.8, 0.7, "grey", 2)
    I.ellipse(cx - 0.4, cy - 7.6, 1.1, 0.9, "grey", 7)
    I.ellipse(cx + 0.6, cy - 8.8, 1.0, 0.8, "grey", 6)


@icon("zone_warehouse", "zones", "Warehouse")
def _(I):
    zt(I, "yellow")
    fx, fy = I_P(-0.7, -1.6)
    I.isobox(fx, fy, 5.6, 3.4, 2.2, "grey", 6, 5, 3)
    I.gable(fx, fy, 5.6, 3.4, 2.2, 1.0, "brown", "x", 5, 3)
    I.isoquad(fx, fy, [(-0.8, 0, 0), (-3.4, 0, 0), (-3.4, 0, 1.8), (-0.8, 0, 1.8)], "grey", 2)
    for x in (-1.5, -2.2):
        I.isoquad(fx, fy, [(x, 0, 0), (x - 0.15, 0, 0), (x - 0.15, 0, 1.8), (x, 0, 1.8)], "grey", 4)


@icon("zone_off", "zones", "Office")
def _(I):
    zt(I, "purple")
    fx, fy = I_P(-1.4, -1.4)
    I.isobox(fx, fy, 3.6, 3.2, 5.2, "grey", 7, 6, 4)
    for z in (0.8, 2.4, 4.0):
        band_l(fx, fy, I, 3.6, z, z + 1.0, "blue", 5, 0.3)
        band_r(fx, fy, I, 3.2, z, z + 1.0, "blue", 3, 0.3)


@icon("zone_off_high", "zones", "High-rise office")
def _(I):
    zt(I, "purple")
    fx, fy = I_P(-1.8, -1.8)
    I.isobox(fx, fy, 2.8, 2.8, 7.4, "grey", 7, 6, 4)
    for z in (0.6, 2.0, 3.4, 4.8):
        band_l(fx, fy, I, 2.8, z, z + 0.9, "blue", 5, 0.2)
        band_r(fx, fy, I, 2.8, z, z + 0.9, "blue", 3, 0.2)
    tc = fy - 1.4 - 7.4
    I.isobox(fx, tc + 0.8, 1.2, 1.2, 0.8, "grey", 7, 5, 3)
    I.rect(fx - 0.25, tc - 1.6, 0.5, 1.8, "grey", 3)
