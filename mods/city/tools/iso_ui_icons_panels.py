"""RCT2-style OpenCity icons: window/panel buttons ("panels") and generic glyphs ("ui")."""

import math

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import grass_tile, bolt, drop, person, lens, badge   # noqa: F401

WHITE = "#ffffff"
PAPER = "#f4f0e4"
SKIN = "#f0b888"


# ---- shared helpers ------------------------------------------------------------------------------------
def _arrow(I, d, cx, cy, L=5.5, c="grey", s=6, sw=1.5, hw=4.2, hl=4.5):
    """Chunky block arrow pointing d in 'u','d','l','r'; L = half length; sw = half shaft width."""
    pts = [(-sw, -L), (sw, -L), (sw, L - hl), (hw, L - hl), (0, L), (-hw, L - hl), (-sw, L - hl)]
    out = []
    for a, b in pts:                       # local: b along direction (down), a across
        out.append({"d": (a, b), "u": (a, -b), "r": (b, a), "l": (-b, a)}[d])
    I.poly([(cx + x, cy + y) for x, y in out], c, s)


def _star(I, cx, cy, r, c="yellow", s=6, ri=0.45):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * ri
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    I.poly(pts, c, s)


def _arc(I, cx, cy, r, a0, a1, t, c, s, mirror=False):
    n = max(2, int(abs(a1 - a0) / 10))
    pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
            cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if mirror:
            x0, x1 = 16 - x0, 16 - x1
        I.line(x0, y0, x1, y1, c, s, t)


def _arcarrow(I, cx, cy, r, a0, a1, c="grey", s=6, t=2.4, mirror=False, hs=3.6):
    """Arc drawn clockwise (screen) from a0 to a1 degrees with an arrowhead at a1."""
    _arc(I, cx, cy, r, a0, a1 - 6, t, c, s, mirror)
    a = math.radians(a1)
    px, py = cx + r * math.cos(a), cy + r * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)
    nx, ny = math.cos(a), math.sin(a)
    pts = [(px + tx * hs * 0.9, py + ty * hs * 0.9), (px + nx * hs - tx * 0.6, py + ny * hs - ty * 0.6),
           (px - nx * hs - tx * 0.6, py - ny * hs - ty * 0.6)]
    if mirror:
        pts = [(16 - x, y) for x, y in pts]
    I.poly(pts, c, s)


def _band(I, p0, d, a, b, hw, c, s):
    """Quad along unit direction d from distance a to b, half width hw."""
    nx, ny = -d[1], d[0]
    q = lambda t, w: (p0[0] + d[0] * t + nx * w, p0[1] + d[1] * t + ny * w)
    I.poly([q(a, -hw), q(b, -hw), q(b, hw), q(a, hw)], c, s)


def _gear(I, cx, cy, r, ramp="grey", s=5, hole=True, teeth=4):
    for i in range(teeth):
        a = math.pi * i / teeth
        I.line(cx - math.cos(a) * (r + 1.4), cy - math.sin(a) * (r + 1.4),
               cx + math.cos(a) * (r + 1.4), cy + math.sin(a) * (r + 1.4), ramp, s - 1, r * 0.55)
    I.ellipse(cx, cy, r, r, ramp, s)
    I.ellipse(cx - r * 0.2, cy - r * 0.2, r * 0.7, r * 0.7, ramp, min(7, s + 1))
    if hole:
        I.ellipse(cx, cy, r * 0.42, r * 0.42, "grey", 1)
        I.ellipse(cx - 0.2, cy - 0.2, r * 0.3, r * 0.3, "grey", 3)


def _mag(I, cx, cy, r=3.8, handle=True, fill="blue"):
    if handle:
        d = (0.707, 0.707)
        _band(I, (cx + d[0] * r * 0.8, cy + d[1] * r * 0.8), d, 0, r * 1.15, 1.3, "brown", 4)
    I.ellipse(cx, cy, r, r, "grey", 3)
    I.ellipse(cx, cy, r - 1.2, r - 1.2, fill, 6)
    I.ellipse(cx - r * 0.3, cy - r * 0.3, r * 0.35, r * 0.28, fill, 7)


def _disk(I, ramp="blue"):
    I.poly([(2, 2), (12, 2), (14, 4), (14, 14), (2, 14)], ramp, 4)
    I.rect(2, 2, 12, 1, ramp, 6)
    I.rect(4.5, 2, 6, 5, "grey", 6)
    I.rect(5, 2.5, 5, 1.2, "grey", 7)
    I.rect(8.5, 3.5, 1.6, 2.8, "grey", 2)
    I.rect(4, 9, 8, 5, WHITE)
    I.rect(5, 10.5, 6, 0.9, "red", 4)
    I.rect(5, 12.2, 4, 0.9, "grey", 4)


def _card(I, band="blue"):
    I.rect(1.5, 2.5, 13, 11.5, "grey", 3)
    I.rect(1.5, 2.5, 12.2, 10.8, PAPER)
    I.rect(1.5, 2.5, 12.2, 3, band, 5)
    I.rect(1.5, 2.5, 12.2, 1, band, 7)


def _house(I, cx, by, w=8, wall="sand", roof="red"):
    h = w * 0.6
    I.rect(cx - w / 2 + 0.6, by - h, w - 1.2, h, wall, 6)
    I.rect(cx - w / 2 + 0.6, by - h, (w - 1.2) / 2, h, wall, 7)
    I.poly([(cx - w / 2 - 0.4, by - h + 0.2), (cx, by - h - w * 0.45), (cx + w / 2 + 0.4, by - h + 0.2)], roof, 5)
    I.poly([(cx, by - h - w * 0.45), (cx + w / 2 + 0.4, by - h + 0.2), (cx, by - h + 0.2)], roof, 3)
    I.rect(cx - 0.7, by - h * 0.55, 1.4, h * 0.55, "brown", 3)


# ---- PANELS --------------------------------------------------------------------------------------------
@icon("pnl_game_menu", "panels", "Game menu")
def _(I):
    _disk(I)


@icon("pnl_save", "panels", "Save")
def _(I):
    I.poly([(1.5, 1.5), (9.5, 1.5), (11, 3), (11, 11), (1.5, 11)], "blue", 4)
    I.rect(3, 1.5, 5, 3.5, "grey", 6)
    I.rect(6.5, 2.2, 1.3, 2.2, "grey", 2)
    I.rect(2.8, 6.5, 6.5, 4.5, WHITE)
    I.rect(3.6, 8, 5, 0.9, "red", 4)
    _arrow(I, "d", 11.7, 10, 4.3, "green", 5, 1.5, 3.2, 3.5)
    I.rect(10.2, 5.7, 1.4, 4, "green", 6)


@icon("pnl_load", "panels", "Load")
def _(I):
    _arrow(I, "u", 8, 6, 5, "green", 5, 1.6, 4.2, 4.5)
    I.rect(6.4, 2, 1.4, 5, "green", 6)
    I.rect(1.5, 6, 5, 2.5, "orange", 4)
    I.rect(1.5, 8, 13, 6, "yellow", 5)
    I.rect(1.5, 8, 13, 1, "yellow", 7)
    I.rect(1.5, 12.5, 13, 1.5, "yellow", 3)
    I.rect(1.5, 6, 5, 1, "orange", 6)


@icon("pnl_settings", "panels", "Settings")
def _(I):
    _gear(I, 8, 8, 4.8, "grey", 5)


@icon("pnl_infoviews", "panels", "Info views")
def _(I):
    I.rect(1.5, 2, 10.5, 9.5, "green", 5)
    I.rect(1.5, 2, 5, 4.5, "green", 6)
    I.rect(7, 6.5, 5, 5, "sand", 5)
    I.rect(1.5, 8, 4, 3.5, "blue", 5)
    I.line(1.5, 7, 12, 7, "yellow", 6, 0.8)
    _mag(I, 10.3, 9.8, 4)


@icon("pnl_budget", "panels", "Budget")
def _(I):
    I.rect(1.5, 4, 13, 9.5, "red", 3)
    I.poly([(2.5, 3.5), (8, 4.7), (8, 12.5), (2.5, 11.3)], "sand", 7)
    I.poly([(13.5, 3.5), (8, 4.7), (8, 12.5), (13.5, 11.3)], "sand", 6)
    for y in (6, 7.8, 9.6):
        I.line(3.4, y - 0.3, 7, y + 0.5, "grey", 4, 0.8)
    I.line(9, 6.7, 12, 6.1, "grey", 4, 0.8)
    I.ellipse(11.2, 11.2, 3.6, 3.6, "yellow", 3)
    I.ellipse(10.8, 10.8, 2.9, 2.9, "yellow", 5)
    I.ellipse(10.2, 10.2, 1.4, 1.4, "yellow", 7)
    I.rect(11.4, 9.5, 1, 3.4, "yellow", 2)


@icon("pnl_stats", "panels", "Statistics")
def _(I):
    I.rect(1.5, 1.5, 13, 13, "grey", 3)
    I.rect(1.5, 1.5, 12.2, 12.2, PAPER)
    for x, h, r in ((2.8, 4, "red"), (5.8, 6.5, "yellow"), (8.8, 8.5, "green"), (11.5, 10.5, "blue")):
        I.rect(x, 12.5 - h, 2.2, h, r, 5)
        I.rect(x, 12.5 - h, 1, h, r, 7)
        I.rect(x + 1.6, 12.5 - h, 0.6, h, r, 3)
    I.rect(2, 12.5, 11.5, 0.8, "grey", 2)


@icon("pnl_chirper", "panels", "Chirper")
def _(I):
    I.line(6.5, 12, 6.5, 14.5, "orange", 5, 1)
    I.line(9.5, 12, 9.5, 14.5, "orange", 5, 1)
    I.poly([(5, 8), (1, 4.5), (2.5, 10)], "blue", 3)
    I.ellipse(7.8, 9, 5.5, 4.2, "blue", 5)
    I.ellipse(7.8, 10.6, 3.6, 2.4, "blue", 7)
    I.ellipse(11, 5.5, 3.3, 3.2, "blue", 5)
    I.ellipse(10.3, 4.6, 1.6, 1.4, "blue", 7)
    I.poly([(13.5, 4.8), (15, 6.2), (13.4, 7.4)], "orange", 6)
    I.ellipse(11.6, 5, 1, 1, WHITE)
    I.pixel(12, 5, "grey", 0)
    I.ellipse(5.7, 8.5, 3, 2, "blue", 3)


@icon("pnl_production", "panels", "Production")
def _(I):
    I.isobox(6.5, 14.5, 5, 4.5, 5.5, "orange", 6, 5, 3)
    I.isoquad(6.5, 14.5, [(-1, 0, 1), (-1, 0, 4.5), (-2, 0, 4.5), (-2, 0, 1)], "brown", 3)
    I.line(3, 9, 6.5, 10.8, "brown", 2, 0.7)
    _gear(I, 11.3, 5.2, 3.2, "grey", 5)


@icon("pnl_policies", "panels", "Policies")
def _(I):
    I.rect(3.5, 2.5, 9, 10.5, "sand", 7)
    I.rect(3.5, 2.5, 3, 10.5, "sand", 7)
    I.rect(10.5, 2.5, 2, 10.5, "sand", 5)
    I.ellipse(8, 2.8, 5.5, 1.5, "sand", 4)
    I.ellipse(8, 2.5, 4.8, 1, "sand", 6)
    I.ellipse(8, 13, 5.5, 1.5, "sand", 4)
    for y in (5.5, 7.5, 9.5):
        I.rect(5, y, 6, 0.9, "grey", 4)
    I.poly([(8.5, 11), (11, 15), (12.5, 13.3), (11, 11.5)], "blue", 5)
    I.ellipse(10.6, 10.8, 2.6, 2.6, "red", 4)
    I.ellipse(10.2, 10.4, 1.6, 1.6, "red", 6)


@icon("pnl_progression", "panels", "Progression")
def _(I):
    for x0, y0, x1, y1 in ((8, 12.5, 4, 8), (8, 12.5, 12, 8), (4, 8, 8, 3.5), (12, 8, 8, 3.5), (4, 8, 4, 8)):
        I.line(x0, y0, x1, y1, "grey", 2, 1.3)
    for x, y, r, s in ((8, 12.5, "blue", 5), (4, 8, "green", 5), (12, 8, "green", 5), (8, 3.5, "yellow", 5)):
        I.ellipse(x, y, 2.5, 2.5, r, s - 2)
        I.ellipse(x - 0.2, y - 0.2, 2, 2, r, s)
        I.ellipse(x - 0.7, y - 0.8, 0.9, 0.8, r, 7)


@icon("pnl_districts", "panels", "Districts")
def _(I):
    I.poly([(1.5, 6), (6, 5), (10, 6), (14.5, 5), (14.5, 14), (10, 15), (6, 14), (1.5, 15)], "green", 5)
    I.poly([(1.5, 6), (6, 5), (6, 14), (1.5, 15)], "green", 6)
    I.poly([(10, 6), (14.5, 5), (14.5, 14), (10, 15)], "green", 4)
    I.line(8, 5.5, 8, 14.5, "blue", 5, 1.5)
    I.line(2, 10, 14, 10, "yellow", 6, 0.8)
    I.rect(5, 1.5, 1, 8, "grey", 6)
    I.poly([(6, 1.5), (12.5, 3.5), (6, 6)], "red", 5)
    I.poly([(6, 1.5), (12.5, 3.5), (6, 3.5)], "red", 6)


@icon("pnl_tiles", "panels", "Tiles")
def _(I):
    I.isobox(8, 15, 7, 7, 1.5, "brown", 4, 4, 2)
    q = lambda x0, y0, x1, y1, s: I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], "green", s)
    q(0, 0, -3.3, -3.3, 6); q(-3.7, -3.7, -7, -7, 6)
    q(-3.7, 0, -7, -3.3, 5); q(0, -3.7, -3.3, -7, 5)
    I.rect(6.8, 5.5, 2.4, 7, WHITE)
    I.rect(4.6, 7.7, 6.8, 2.4, WHITE)
    I.rect(6.8, 5.5, 2.4, 1, "grey", 7)


@icon("pnl_transit_lines", "panels", "Transit lines")
def _(I):
    I.rect(1.5, 2, 13, 12, "grey", 3)
    I.rect(1.5, 2, 12.2, 11.2, PAPER)
    for (x0, y0, x1, y1), c in (((3, 5, 13, 5), "red"), ((3, 11, 13, 11), "blue"), ((4, 3.5, 12, 12.5), "green")):
        I.line(x0, y0, x1, y1, c, 5, 2)
    for x, y in ((3, 5), (13, 5), (3, 11), (13, 11), (4, 3.5), (12, 12.5)):
        I.ellipse(x, y, 1.3, 1.3, "grey", 1)
        I.ellipse(x, y, 0.8, 0.8, WHITE)


@icon("pnl_achievements", "panels", "Achievements")
def _(I):
    _arc(I, 3.7, 5.5, 2.5, 90, 270, 1.4, "yellow", 4)
    _arc(I, 12.3, 5.5, 2.5, -90, 90, 1.4, "yellow", 3)
    I.poly([(3.5, 1.8), (12.5, 1.8), (11.5, 8), (9.5, 10.5), (6.5, 10.5), (4.5, 8)], "yellow", 5)
    I.poly([(3.5, 1.8), (7, 1.8), (6.5, 10.5), (4.5, 8)], "yellow", 6)
    I.rect(4.7, 3, 1.2, 4, "yellow", 7)
    I.rect(7, 10, 2, 2.5, "yellow", 4)
    I.rect(4.5, 12.2, 7, 2.3, "brown", 5)
    I.rect(4.5, 12.2, 7, 0.8, "brown", 7)
    _star(I, 8, 6, 2.4, "#ffffff", 7)


@icon("pnl_advisor", "panels", "Advisor")
def _(I):
    person(I, 5.6, 14.5, 11.5, "purple", SKIN, 5)
    I.ellipse(11.5, 4.5, 3, 3, "yellow", 5)
    I.ellipse(10.8, 3.8, 1.5, 1.5, "yellow", 7)
    I.rect(10.3, 7.2, 2.4, 1.8, "grey", 4)
    I.line(8.3, 1.8, 7.6, 1.2, "yellow", 6, 0.8)
    I.line(14.7, 1.8, 15, 1.2, "yellow", 6, 0.8)


@icon("pnl_milestone", "panels", "Milestone")
def _(I):
    I.rect(2.5, 1.5, 1.4, 13.5, "brown", 5)
    I.poly([(3.9, 2), (14.5, 5.8), (3.9, 9.6)], "red", 5)
    I.poly([(3.9, 2), (14.5, 5.8), (3.9, 5.8)], "red", 6)
    _star(I, 7.3, 5.8, 2.3, "yellow", 7)


@icon("pnl_notifications", "panels", "Notifications")
def _(I):
    I.rect(7.2, 1.5, 1.6, 2, "yellow", 3)
    I.ellipse(8, 7, 4.3, 4.6, "yellow", 5)
    I.poly([(3.7, 7), (12.3, 7), (14.5, 12), (1.5, 12)], "yellow", 5)
    I.poly([(8, 7), (12.3, 7), (14.5, 12), (8, 12)], "yellow", 4)
    I.ellipse(6.5, 6, 1.6, 3, "yellow", 7)
    I.ellipse(8, 13.2, 1.9, 1.5, "brown", 4)


@icon("pnl_photo", "panels", "Photo")
def _(I):
    I.rect(5, 3, 4.5, 2.5, "grey", 3)
    I.rect(1.5, 5, 13, 8.5, "grey", 3)
    I.rect(1.5, 5, 13, 2, "grey", 5)
    I.rect(1.5, 5, 13, 0.8, "grey", 7)
    I.rect(11, 3, 2.5, 2, "yellow", 6)
    I.ellipse(8, 9.5, 3.6, 3.6, "grey", 6)
    I.ellipse(8, 9.5, 2.7, 2.7, "grey", 1)
    I.ellipse(8, 9.5, 1.9, 1.9, "blue", 5)
    I.ellipse(7.4, 8.8, 0.7, 0.6, "blue", 7)
    I.pixel(3, 6, "red", 6)


@icon("pnl_radio", "panels", "Radio")
def _(I):
    I.line(11, 5, 14.2, 1.3, "grey", 6, 1)
    I.rect(1.5, 5, 13, 9, "brown", 4)
    I.rect(1.5, 5, 13, 1, "brown", 6)
    I.rect(1.5, 12.5, 13, 1.5, "brown", 2)
    I.ellipse(5.5, 9.5, 3, 3, "grey", 2)
    I.ellipse(5.5, 9.5, 2.2, 2.2, "grey", 4)
    I.pixel(4.5, 8.5, "grey", 7); I.pixel(6.5, 10.5, "grey", 2)
    I.rect(9.5, 6.8, 4, 2, "yellow", 6)
    I.ellipse(10.7, 11.3, 1.2, 1.2, "red", 5)
    I.ellipse(13, 11.3, 1.2, 1.2, "grey", 6)


@icon("pnl_search", "panels", "Search")
def _(I):
    _mag(I, 6.5, 6.5, 4.8)


@icon("pnl_city_info", "panels", "City info")
def _(I):
    I.poly([(1.5, 6), (8, 1.5), (14.5, 6)], "sand", 5)
    I.poly([(1.5, 6), (8, 1.5), (8, 6)], "sand", 7)
    I.rect(1.5, 6, 13, 1.2, "sand", 4)
    for x in (2.5, 6.5, 10.5):
        I.rect(x, 7.2, 2.6, 5, "sand", 7)
        I.rect(x + 1.8, 7.2, 0.8, 5, "sand", 5)
    I.rect(1.5, 12, 13, 2.5, "grey", 5)
    I.rect(1.5, 12, 13, 0.8, "grey", 7)
    I.ellipse(11.8, 11, 3.8, 3.8, "blue", 3, after=True)
    I.ellipse(11.8, 11, 3.1, 3.1, "blue", 5, after=True)
    I.ellipse(11, 10.2, 1.5, 1.3, "blue", 6, after=True)
    I.rect(11, 8.4, 1.6, 1.5, WHITE, after=True)
    I.rect(11, 10.4, 1.6, 3.2, WHITE, after=True)


@icon("pnl_help", "panels", "Help")
def _(I):
    I.ellipse(8, 8, 6.7, 6.7, "blue", 3)
    I.ellipse(8, 8, 5.7, 5.7, "blue", 5)
    I.ellipse(6.8, 6.6, 3.8, 3.2, "blue", 6)
    _arc(I, 8, 6.2, 2.4, 170, 450, 2, WHITE, 7)
    I.rect(7, 8.2, 2, 2, WHITE)
    I.rect(7, 11, 2, 2, WHITE)


@icon("pnl_camera_home", "panels", "Camera home")
def _(I):
    I.ring(8, 8, 6.8, 2.2, "green", 5)
    for a, b, c2, d in ((8, 1.2, 8, 4.2), (8, 11.8, 8, 14.8), (1.2, 8, 4.2, 8), (11.8, 8, 14.8, 8)):
        I.line(a, b, c2, d, "green", 5, 1.8)
    _house(I, 8, 11.5, 7, "sand", "red")


@icon("pnl_minimap", "panels", "Minimap")
def _(I):
    I.poly([(1.5, 4), (6, 3), (6, 13), (1.5, 14)], "green", 6)
    I.poly([(6, 3), (10.5, 4), (10.5, 14), (6, 13)], "green", 4)
    I.poly([(10.5, 4), (14.5, 3), (14.5, 13), (10.5, 14)], "green", 5)
    I.line(3, 9, 5.5, 8.2, "blue", 5, 1.4)
    I.line(7, 9.4, 10, 10.3, "blue", 4, 1.4)
    I.line(11.5, 6, 14, 5.4, "sand", 6, 1.2)
    I.ellipse(8, 7, 1.5, 1.5, "red", 5, after=True)


@icon("pnl_citizen", "panels", "Citizen")
def _(I):
    _card(I, "blue")
    I.ellipse(7.6, 8.6, 1.9, 2, SKIN)
    I.poly([(4, 13), (4.5, 11.2), (7.6, 10.7), (10.7, 11.2), (11.2, 13)], "blue", 5)


@icon("pnl_vehicle", "panels", "Vehicle")
def _(I):
    _card(I, "orange")
    I.poly([(3, 12), (3, 10.2), (4.5, 9.8), (6, 8), (9, 8), (10.5, 9.8), (12, 10.2), (12, 12)], "red", 5)
    I.poly([(6.5, 8.5), (8.7, 8.5), (9.7, 9.8), (5.8, 9.8)], "blue", 7)
    I.ellipse(5, 12, 1.3, 1.3, "grey", 1); I.ellipse(10, 12, 1.3, 1.3, "grey", 1)


@icon("pnl_building", "panels", "Building")
def _(I):
    _card(I, "green")
    _house(I, 7.5, 13, 7.4, "sand", "red")


# ---- UI glyphs -----------------------------------------------------------------------------------------
def _x(I, c, s, t=2.2, lo=4.5, hi=11.5):
    I.line(lo, lo, hi, hi, c, s, t)
    I.line(hi, lo, lo, hi, c, s, t)


@icon("ui_close", "ui", "Close")
def _(I):
    I.rect(2, 2, 12, 12, "red", 3)
    I.rect(2, 2, 11, 11, "red", 5)
    I.rect(2, 2, 11, 1, "red", 7)
    _x(I, WHITE, 7)


@icon("ui_check", "ui", "Check")
def _(I):
    I.line(3, 8.5, 6.5, 12, "green", 4, 3)
    I.line(6.5, 12, 13, 4, "green", 4, 3)
    I.line(2.9, 8.2, 6.4, 11.5, "green", 6, 1.4)
    I.line(6.6, 11.5, 12.8, 3.8, "green", 6, 1.4)


@icon("ui_cross", "ui", "Cross")
def _(I):
    _x(I, "red", 4, 3, 3.5, 12.5)
    I.line(3.4, 3.4, 12, 12, "red", 6, 1.2)
    I.line(12.6, 3.4, 4, 12, "red", 6, 1.2)


@icon("ui_plus", "ui", "Plus")
def _(I):
    I.rect(6.5, 2.5, 3, 11, "grey", 6)
    I.rect(2.5, 6.5, 11, 3, "grey", 6)
    I.rect(8.5, 6.5, 1, 7, "grey", 5)
    I.rect(8.5, 8.5, 5, 1, "grey", 5)


@icon("ui_minus", "ui", "Minus")
def _(I):
    I.rect(2.5, 6.5, 11, 3, "grey", 6)
    I.rect(2.5, 8.5, 11, 1, "grey", 5)


@icon("ui_up", "ui", "Up")
def _(I):
    _arrow(I, "u", 8, 8, 6.5, "grey", 6)


@icon("ui_down", "ui", "Down")
def _(I):
    _arrow(I, "d", 8, 8, 6.5, "grey", 6)


@icon("ui_left", "ui", "Left")
def _(I):
    _arrow(I, "l", 8, 8, 6.5, "grey", 6)


@icon("ui_right", "ui", "Right")
def _(I):
    _arrow(I, "r", 8, 8, 6.5, "grey", 6)


@icon("ui_info", "ui", "Info")
def _(I):
    I.ellipse(8, 8, 6.7, 6.7, "blue", 3)
    I.ellipse(8, 8, 5.7, 5.7, "blue", 5)
    I.ellipse(6.8, 6.6, 3.8, 3.2, "blue", 6)
    I.rect(6.9, 3.8, 2.2, 2.2, WHITE)
    I.rect(6.9, 7, 2.2, 5, WHITE)


@icon("ui_warning", "ui", "Warning")
def _(I):
    I.poly([(8, 1.5), (15, 14), (1, 14)], "yellow", 5)
    I.poly([(8, 1.5), (8, 14), (1, 14)], "yellow", 6)
    I.rect(7, 6, 2, 4.5, "grey", 0)
    I.rect(7, 11.5, 2, 1.8, "grey", 0)


@icon("ui_error", "ui", "Error")
def _(I):
    I.ellipse(8, 8, 6.7, 6.7, "red", 3)
    I.ellipse(8, 8, 5.7, 5.7, "red", 5)
    I.ellipse(6.8, 6.6, 3.8, 3.2, "red", 6)
    I.rect(6.9, 3.5, 2.2, 5.5, WHITE)
    I.rect(6.9, 10, 2.2, 2.2, WHITE)


@icon("ui_lock", "ui", "Locked")
def _(I):
    I.ring(8, 7, 3.9, 1.8, "grey", 6)
    I.rect(3.5, 7, 9, 7.5, "yellow", 5)
    I.rect(3.5, 7, 9, 1, "yellow", 7)
    I.rect(10.5, 8, 2, 6.5, "yellow", 3)
    I.ellipse(8, 10.2, 1.3, 1.3, "grey", 0)
    I.rect(7.3, 10.5, 1.4, 2.5, "grey", 0)


@icon("ui_unlock", "ui", "Unlocked")
def _(I):
    I.ring(8.5, 5, 3.9, 1.8, "grey", 6)
    I.rect(11.2, 5.5, 3, 2, None)
    I.rect(3.5, 7.5, 9, 7, "yellow", 5)
    I.rect(3.5, 7.5, 9, 1, "yellow", 7)
    I.rect(10.5, 8.5, 2, 6, "yellow", 3)
    I.ellipse(8, 10.7, 1.3, 1.3, "green", 2)
    I.rect(7.3, 11, 1.4, 2.5, "green", 2)


@icon("ui_eye", "ui", "Show")
def _(I):
    I.ellipse(8, 8, 7, 4.3, "grey", 3)
    I.ellipse(8, 8, 6.2, 3.5, WHITE)
    I.ellipse(8, 8, 2.6, 2.6, "blue", 5)
    I.ellipse(8, 8, 1.2, 1.2, "grey", 0)
    I.pixel(7, 7, WHITE)


@icon("ui_eye_off", "ui", "Hide")
def _(I):
    I.ellipse(8, 8, 7, 4.3, "grey", 3)
    I.ellipse(8, 8, 6.2, 3.5, "grey", 6)
    I.ellipse(8, 8, 2.6, 2.6, "grey", 4)
    I.ellipse(8, 8, 1.2, 1.2, "grey", 1)
    I.line(3, 13, 13, 3, "red", 2, 3.4)
    I.line(3, 13, 13, 3, "red", 5, 1.8)


@icon("ui_locate", "ui", "Locate")
def _(I):
    I.ring(8, 8, 5.2, 1.7, "red", 5)
    for a, b, c, d in ((8, 1.5, 8, 5), (8, 11, 8, 14.5), (1.5, 8, 5, 8), (11, 8, 14.5, 8)):
        I.line(a, b, c, d, "red", 5, 1.5)
    I.ellipse(8, 8, 1.4, 1.4, "red", 6)


@icon("ui_follow", "ui", "Follow")
def _(I):
    I.rect(3.5, 2.5, 4, 2, "grey", 3)
    I.rect(1.5, 4, 10, 7, "grey", 4)
    I.rect(1.5, 4, 10, 1, "grey", 6)
    I.ellipse(6.5, 7.7, 2.6, 2.6, "grey", 6)
    I.ellipse(6.5, 7.7, 1.7, 1.7, "blue", 4)
    _arrow(I, "r", 11.5, 12.5, 3.6, "green", 5, 1.2, 3, 3.2)


@icon("ui_pin", "ui", "Pin")
def _(I):
    I.line(8, 9, 8, 14.8, "grey", 5, 1.2)
    I.ellipse(8, 5.2, 3.8, 3.8, "red", 4)
    I.ellipse(7.6, 4.8, 2.8, 2.8, "red", 5)
    I.ellipse(7, 4, 1.2, 1.1, "red", 7)
    I.rect(5, 8.2, 6, 1.6, "red", 3)


@icon("ui_minimize", "ui", "Minimize")
def _(I):
    I.rect(2.5, 9.5, 11, 3.5, "grey", 6)
    I.rect(2.5, 11.8, 11, 1.2, "grey", 5)


@icon("ui_zoom_in", "ui", "Zoom in")
def _(I):
    _mag(I, 6.5, 6.5, 4.8)
    I.rect(5.4, 3.9, 2.2, 5.2, WHITE, after=True)
    I.rect(3.9, 5.4, 5.2, 2.2, WHITE, after=True)


@icon("ui_zoom_out", "ui", "Zoom out")
def _(I):
    _mag(I, 6.5, 6.5, 4.8)
    I.rect(3.9, 5.4, 5.2, 2.2, WHITE, after=True)


@icon("ui_rotate_left", "ui", "Rotate left")
def _(I):
    _arcarrow(I, 8, 8.5, 5.3, -30, 250, "grey", 6, 2, True)


@icon("ui_rotate_right", "ui", "Rotate right")
def _(I):
    _arcarrow(I, 8, 8.5, 5.3, -30, 250, "grey", 6, 2)


@icon("ui_undo", "ui", "Undo")
def _(I):
    I.line(5, 5, 9, 5, "grey", 6, 2.4)
    _arc(I, 9, 9, 4, -90, 90, 2.4, "grey", 6)
    I.line(9, 13, 5.5, 13, "grey", 6, 2.4)
    I.poly([(1.5, 5), (5.5, 1.5), (5.5, 8.5)], "grey", 6)


@icon("ui_redo", "ui", "Redo")
def _(I):
    I.line(11, 5, 7, 5, "grey", 6, 2.4)
    _arc(I, 7, 9, 4, -90, 90, 2.4, "grey", 6, True)
    I.line(7, 13, 10.5, 13, "grey", 6, 2.4)
    I.poly([(14.5, 5), (10.5, 1.5), (10.5, 8.5)], "grey", 6)


@icon("ui_trash", "ui", "Delete")
def _(I):
    I.rect(6, 1.5, 4, 2, "grey", 5)
    I.poly([(3.5, 5), (12.5, 5), (11.5, 14.5), (4.5, 14.5)], "grey", 5)
    I.poly([(8, 5), (12.5, 5), (11.5, 14.5), (8, 14.5)], "grey", 4)
    I.rect(2.5, 3.5, 11, 2, "grey", 6)
    for x in (6.2, 8, 9.8):
        I.rect(x - 0.4, 7, 0.8, 6, "grey", 1)


@icon("ui_sort", "ui", "Sort")
def _(I):
    for y, w, r in ((2.5, 11, "blue"), (6.5, 8, "blue"), (10.5, 5, "blue")):
        I.rect(2, y, w, 3, r, 5)
        I.rect(2, y, w, 1, r, 7)
        I.rect(2 + w - 1, y, 1, 3, r, 3)
    _arrow(I, "d", 13, 9.5, 4, "grey", 6, 1.1, 2.4, 2.8)


@icon("ui_filter", "ui", "Filter")
def _(I):
    I.poly([(1.5, 2.5), (14.5, 2.5), (9.5, 8), (9.5, 14), (6.5, 12.5), (6.5, 8)], "blue", 5)
    I.poly([(1.5, 2.5), (8, 2.5), (8, 8), (6.5, 8)], "blue", 6)
    I.rect(1.5, 2.5, 13, 1.2, "blue", 7)
    I.poly([(8, 2.5), (14.5, 2.5), (9.5, 8), (9.5, 14), (8, 13.3)], "blue", 4)


@icon("ui_speaker", "ui", "Sound on")
def _(I):
    I.rect(1.5, 5.8, 3.5, 4.4, "grey", 6)
    I.poly([(5, 5.8), (9, 2.5), (9, 13.5), (5, 10.2)], "grey", 5)
    I.poly([(5, 5.8), (9, 2.5), (9, 8), (5, 8)], "grey", 6)
    _arc(I, 9, 8, 3.2, -50, 50, 1.4, "yellow", 6)
    _arc(I, 9, 8, 5.6, -50, 50, 1.4, "yellow", 6)


@icon("ui_muted", "ui", "Sound off")
def _(I):
    I.rect(1.5, 5.8, 3.5, 4.4, "grey", 6)
    I.poly([(5, 5.8), (9, 2.5), (9, 13.5), (5, 10.2)], "grey", 5)
    I.poly([(5, 5.8), (9, 2.5), (9, 8), (5, 8)], "grey", 6)
    I.line(10.5, 5.5, 14.2, 10.5, "red", 5, 1.7)
    I.line(14.2, 5.5, 10.5, 10.5, "red", 5, 1.7)


@icon("ui_refresh", "ui", "Refresh")
def _(I):
    _arcarrow(I, 8, 8, 5.3, 200, 335, "green", 5, 2, hs=3.4)
    _arcarrow(I, 8, 8, 5.3, 20, 155, "green", 5, 2, hs=3.4)


@icon("ui_star", "ui", "Star")
def _(I):
    _star(I, 8, 8.4, 7, "yellow", 5, 0.45)
    I.poly([(8, 1.4), (9.6, 5.8), (8, 8.4), (6.4, 5.8)], "yellow", 6)
    I.rect(7.2, 3.5, 1, 1.5, "yellow", 7)


@icon("ui_like", "ui", "Like")
def _(I):
    I.ellipse(5, 5.5, 3.6, 3.6, "red", 5)
    I.ellipse(11, 5.5, 3.6, 3.6, "red", 4)
    I.poly([(1.5, 6.5), (14.5, 6.5), (8, 14.5)], "red", 4)
    I.poly([(1.5, 6.5), (8, 6.5), (8, 14.5)], "red", 5)
    I.ellipse(4.3, 4.6, 1.3, 1.1, "red", 7)


@icon("ui_chart_line", "ui", "Line chart")
def _(I):
    I.rect(1.5, 1.5, 13, 13, "grey", 3)
    I.rect(1.5, 1.5, 12.2, 12.2, PAPER)
    I.line(3, 11, 6, 7, "red", 5, 1.5)
    I.line(6, 7, 9, 9.5, "red", 5, 1.5)
    I.line(9, 9.5, 12.5, 3.8, "red", 5, 1.5)
    for x, y in ((3, 11), (6, 7), (9, 9.5), (12.5, 3.8)):
        I.ellipse(x, y, 1.1, 1.1, "red", 3, after=True)
    I.rect(2, 12.5, 11.5, 0.8, "grey", 2)


@icon("ui_list", "ui", "List")
def _(I):
    for y in (2.5, 6.5, 10.5):
        I.rect(2, y, 3, 3, "blue", 5)
        I.rect(2, y, 3, 1, "blue", 7)
        I.rect(6.5, y, 7.5, 3, "grey", 6)
        I.rect(6.5, y + 2, 7.5, 1, "grey", 5)


@icon("ui_grid_view", "ui", "Grid")
def _(I):
    for x in (2, 8.5):
        for y in (2, 8.5):
            I.rect(x, y, 5.5, 5.5, "blue", 5)
            I.rect(x, y, 5.5, 1.2, "blue", 7)
            I.rect(x + 4.5, y, 1, 5.5, "blue", 3)


@icon("ui_edit", "ui", "Edit")
def _(I):
    d = (-0.707, 0.707)
    p0 = (13.7, 2.3)
    _band(I, p0, d, 0, 2.6, 1.7, "red", 6)
    _band(I, p0, d, 2.6, 4, 1.7, "grey", 6)
    _band(I, p0, d, 4, 11, 1.7, "yellow", 5)
    _band(I, p0, d, 4, 11, 0.6, "yellow", 7)
    I.poly([(p0[0] + d[0] * 11 - 1.2, p0[1] + d[1] * 11 - 1.2),
            (p0[0] + d[0] * 11 + 1.2, p0[1] + d[1] * 11 + 1.2), (2, 14.2)], "sand", 6)
    I.poly([(2, 14.2), (3.2, 13), (3.4, 13.2), (2.2, 14.4)], "grey", 1)


@icon("ui_colour", "ui", "Colour")
def _(I):
    I.ellipse(8, 8.5, 7, 6, "sand", 5)
    I.ellipse(7, 7.5, 5, 4, "sand", 6)
    I.ellipse(11, 11.3, 1.9, 1.7, None)
    for x, y, r in ((4.8, 6.3, "red"), (8.3, 4.6, "blue"), (11.8, 7, "green"), (4.2, 10.4, "yellow")):
        I.ellipse(x, y, 1.6, 1.6, r, 5, after=True)


@icon("ui_music", "ui", "Music")
def _(I):
    I.ellipse(4.5, 12.2, 2.5, 2, "purple", 4)
    I.ellipse(11.5, 10.7, 2.5, 2, "purple", 4)
    I.rect(5.9, 3.5, 1.4, 8.8, "purple", 5)
    I.rect(12.9, 2, 1.4, 8.8, "purple", 5)
    I.poly([(5.9, 3), (14.3, 1.5), (14.3, 4.5), (5.9, 6)], "purple", 6)


@icon("ui_keyboard", "ui", "Keyboard")
def _(I):
    I.rect(1.5, 3.5, 13, 9.5, "grey", 3)
    I.rect(1.5, 3.5, 13, 1, "grey", 5)
    for y in (5.2, 8):
        for x in (2.8, 5.3, 7.8, 10.3, 12.2):
            I.rect(x, y, 1.8, 1.8, "grey", 6)
    I.rect(4, 10.7, 8, 1.6, "grey", 6)


@icon("ui_mouse", "ui", "Mouse")
def _(I):
    I.ellipse(8, 8.5, 4.8, 6.4, "grey", 6)
    I.ellipse(7, 7, 3, 4.5, "grey", 6)
    I.rect(3.2, 8.5, 9.6, 1, "grey", 3, after=True)
    I.rect(7.5, 2.4, 1, 6.2, "grey", 3, after=True)
    I.rect(4.2, 3.5, 3, 4.5, "red", 5, after=True)
    I.rect(9.3, 5.5, 2.2, 2.8, "grey", 4, after=True)


@icon("ui_monitor", "ui", "Display")
def _(I):
    I.rect(1.5, 2, 13, 9.5, "grey", 3)
    I.rect(2.7, 3.2, 10.6, 7, "blue", 5)
    I.poly([(2.7, 3.2), (9, 3.2), (6, 10.2), (2.7, 10.2)], "blue", 6)
    I.rect(6.5, 11.5, 3, 1.8, "grey", 4)
    I.rect(4.5, 13, 7, 1.5, "grey", 5)


@icon("ui_globe", "ui", "Language")
def _(I):
    I.ellipse(8, 8, 6.8, 6.8, "blue", 4)
    I.ellipse(7.3, 7.3, 5.4, 5.4, "blue", 5)
    I.poly([(3.5, 5), (6.5, 3), (7.5, 5.5), (6, 8), (4.5, 7.5)], "green", 5, after=True)
    I.poly([(8.5, 8.5), (12, 7.5), (12.5, 10.5), (10, 13), (8.5, 11)], "green", 4, after=True)
    I.ellipse(5.5, 4.8, 1.5, 1, "blue", 7, after=True)


@icon("ui_play_video", "ui", "Play video")
def _(I):
    I.rect(1.5, 3, 13, 10, "grey", 3)
    I.rect(2.7, 4.2, 10.6, 7.6, "grey", 1)
    I.poly([(6.3, 5.2), (11.5, 8), (6.3, 10.8)], "red", 5)
    I.poly([(6.3, 5.2), (11.5, 8), (6.3, 8)], "red", 6)


@icon("ui_exit", "ui", "Exit")
def _(I):
    I.rect(1.5, 2, 7, 12.5, "brown", 4)
    I.rect(1.5, 2, 7, 1, "brown", 6)
    I.rect(2.7, 3.2, 4.6, 10, "brown", 2)
    I.ellipse(6.3, 8.5, 0.7, 0.7, "yellow", 6, after=True)
    _arrow(I, "r", 11.3, 8.3, 3.7, "green", 5, 1.5, 3.4, 3.4)
