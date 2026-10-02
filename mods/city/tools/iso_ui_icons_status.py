"""Status / problem / alert / severity-tier badges for the RCT2-style OpenCity icon set (group "status").

All 34 icons share one badge: a round speech-bubble pin with a pointer at the bottom, a light rim (so it
separates from busy world art; the DSL adds the dark outline outside the rim), a darker lower-right
crescent for volume, and a tier-coloured face. Fill colour = severity tier; the symbol is dark on light
tiers and light on dark tiers. The symbol area is about x 4..12, y 3.2..11.4 (16-grid).
"""

import math

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import bolt  # noqa: F401  (kept for parity with other icon files)


# ---- severity tiers -----------------------------------------------------------------------------------
DARK = "#2a1608"          # symbol ink on light / mid tiers
LIGHT = "#ffffff"         # symbol ink on dark tiers
RIM = "#fff6dc"
TIER = {
    "minimal": ("#9a9ca4", DARK),
    "info":    ("#6bb8f0", DARK),
    "problem": ("#f2d04c", DARK),
    "warning": ("#f28c30", DARK),
    "major":   ("#e54b3c", LIGHT),
    "error":   ("#a82020", LIGHT),
    "fatal":   ("#301010", "#ff4a3a"),
    "good":    ("#5cd67a", DARK),
}


def _mix(h, to, t):
    h, to = h.lstrip("#"), to.lstrip("#")
    a = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    b = [int(to[i:i + 2], 16) for i in (0, 2, 4)]
    return "#%02x%02x%02x" % tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def _rrect(I, x0, y0, x1, y1, r, c, p=2.7):
    """Superellipse blob (smooth at both raster sizes); r is unused, kept for readability."""
    cx, cy, rx, ry = (x0 + x1) / 2.0, (y0 + y1) / 2.0, (x1 - x0) / 2.0, (y1 - y0) / 2.0
    pts = []
    for i in range(64):
        t = 2 * math.pi * i / 64
        ct, st = math.cos(t), math.sin(t)
        pts.append((cx + rx * math.copysign(abs(ct) ** (2 / p), ct), cy + ry * math.copysign(abs(st) ** (2 / p), st)))
    I.poly(pts, c)


def sbadge(I, tier):
    """Status bubble pin. Identical shape for every tier. Body x 1..15, y 1..13, pointer to y 15."""
    base = TIER[tier][0]
    dark = _mix(base, "#000000", 0.30)
    I.poly([(4.6, 11.6), (11.4, 11.6), (8, 15)], RIM)
    _rrect(I, 1.07, 1.07, 14.93, 12.93, 5.6, RIM)
    I.poly([(5.7, 11.4), (10.3, 11.4), (8, 13.7)], dark)
    _rrect(I, 2.07, 2.07, 13.93, 11.93, 4.6, dark)
    I.poly([(5.7, 11.2), (9.9, 11.2), (7.7, 13.4)], base)
    _rrect(I, 2.07, 2.07, 13.53, 11.53, 4.4, base)


def _arc(I, cx, cy, r, a0, a1, c, t=1.2, n=7):
    pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
            cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        I.line(x0, y0, x1, y1, c, 0, t)


def _wave(I, x0, x1, y, amp, c, t=1.4, n=10, periods=1.5):
    pts = [(x0 + (x1 - x0) * i / n, y - amp * math.sin(2 * math.pi * periods * i / n)) for i in range(n + 1)]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        I.line(ax, ay, bx, by, c, 0, t)


def _drop(I, cx, cy, r, c, hl):
    I.poly([(cx, cy - r * 1.9), (cx + r * 0.92, cy - r * 0.25), (cx - r * 0.92, cy - r * 0.25)], c)
    I.ellipse(cx, cy, r, r, c)
    I.ellipse(cx - r * 0.38, cy - r * 0.15, r * 0.26, r * 0.4, hl)


def _cross_out(I, x0, y0, x1, y1, ink, c="#e54b3c", t=1.7):
    """Bold X with a dark halo so it reads on any symbol."""
    for a, b, cc, tt in ((1, 0, ink, t + 1.6), (0, 0, c, t)):
        I.line(x0, y0, x1, y1, cc, 0, tt)
        I.line(x1, y0, x0, y1, cc, 0, tt)


def _bang(I, x, ink, w=2.0, top=3.4, bot=8.4):
    I.poly([(x - w / 2, top), (x + w / 2, top), (x + w * 0.35, bot), (x - w * 0.35, bot)], ink)
    I.ellipse(x, 10.3, w * 0.6, w * 0.6, ink)


def _car(I, x, y, w, body, glass, wheel, rear="right", light=None):
    """Side-view car, facing right; (x, y) = top-left, h about 3."""
    I.rect(x, y + 1.1, w, 1.5, body)
    I.poly([(x + w * 0.22, y + 1.2), (x + w * 0.34, y), (x + w * 0.7, y), (x + w * 0.82, y + 1.2)], body)
    I.rect(x + w * 0.36, y + 0.35, w * 0.32, 0.8, glass)
    I.ellipse(x + w * 0.24, y + 2.7, 0.85, 0.85, wheel)
    I.ellipse(x + w * 0.76, y + 2.7, 0.85, 0.85, wheel)
    if light:
        I.rect(x + (0 if rear == "left" else w - 0.7), y + 1.2, 0.7, 0.8, light)


# ---- symbols: fn(I, ink, bg) draws inside the bubble ---------------------------------------------------


def s_bolt(I, ink, bg):
    pts = [(9.4, 3.2), (5.2, 8.0), (7.5, 8.0), (6.4, 11.6), (10.8, 6.4), (8.4, 6.4), (9.8, 3.2)]
    I.poly([(10.2, 3.1), (6.0, 3.1), (4.6, 7.7), (7.0, 7.7), (5.6, 11.6), (11.6, 6.0), (8.8, 6.0)], ink)


def s_drop(I, ink, bg):
    _drop(I, 8, 8.4, 3.2, ink, bg)


def s_sewage(I, ink, bg):
    I.rect(5.6, 2.9, 4.8, 1.3, ink)
    I.rect(6.8, 4.1, 2.4, 1.7, ink)
    _drop(I, 8, 9.7, 1.9, ink, bg)
    I.rect(4.4, 9.6, 1.0, 1.0, ink)
    I.rect(10.8, 8.4, 1.0, 1.0, ink)


def s_dirty(I, ink, bg):
    _drop(I, 8, 8.4, 3.5, ink, bg)
    I.ellipse(6.6, 8.2, 1.15, 1.2, bg)
    I.ellipse(9.4, 8.2, 1.15, 1.2, bg)
    I.rect(7.5, 9.6, 1.0, 1.0, bg)
    I.rect(6.2, 10.2, 0.8, 1.2, bg)
    I.rect(9.0, 10.2, 0.8, 1.2, bg)


def s_noroad(I, ink, bg):
    I.poly([(6.2, 3.2), (9.8, 3.2), (12.6, 11.5), (3.4, 11.5)], ink)
    I.rect(7.5, 4.2, 1.0, 1.4, bg)
    I.rect(7.5, 6.6, 1.0, 1.6, bg)
    I.rect(7.5, 9.2, 1.0, 1.7, bg)
    _cross_out(I, 4.9, 4.6, 11.1, 10.8, ink)


def s_abandoned(I, ink, bg):
    I.poly([(8, 2.8), (13.4, 6.8), (2.6, 6.8)], ink)
    I.rect(9.8, 3.4, 1.6, 2.4, ink)
    I.rect(3.9, 7.5, 8.2, 4.3, ink)
    I.line(4.8, 8.4, 11.2, 11.2, bg, 0, 1.2)
    I.line(4.8, 11.2, 11.2, 8.4, bg, 0, 1.2)


def s_unhappy(I, ink, bg):
    I.ellipse(8, 7.3, 4.3, 4.3, ink)
    I.rect(5.6, 5.5, 1.3, 1.9, bg)
    I.rect(9.1, 5.5, 1.3, 1.9, bg)
    I.line(5.6, 10.4, 6.9, 8.9, bg, 0, 1.2)
    I.line(6.9, 8.9, 9.1, 8.9, bg, 0, 1.2)
    I.line(9.1, 8.9, 10.4, 10.4, bg, 0, 1.2)


def s_noworkers(I, ink, bg):
    # worker with hard hat (left) and a question mark (right)
    skin = "#f6c898"
    I.ellipse(5.9, 7.2, 2.0, 2.0, skin)
    I.poly([(3.6, 6.4), (4.2, 3.6), (7.6, 3.6), (8.2, 6.4)], ink)
    I.rect(3.0, 5.7, 5.8, 1.1, ink)
    I.poly([(2.8, 11.7), (3.4, 9.6), (8.4, 9.6), (9.0, 11.7)], ink)
    _arc(I, 10.8, 5.4, 1.7, 190, 450, ink, 1.6, 9)
    I.line(10.8, 7.0, 10.8, 8.2, ink, 0, 1.6)
    I.rect(10.0, 9.4, 1.7, 1.7, ink)


def s_nocust(I, ink, bg):
    I.poly([(3.9, 5.4), (12.1, 5.4), (12.8, 11.7), (3.2, 11.7)], ink)
    I.poly([(5.3, 6.8), (10.7, 6.8), (11.1, 10.3), (4.9, 10.3)], bg)
    _arc(I, 8, 5.8, 2.3, 180, 360, ink, 1.2)


def s_nogoods(I, ink, bg):
    I.poly([(8, 3.4), (12.6, 5.9), (8, 8.4), (3.4, 5.9)], ink)
    I.poly([(8, 4.7), (10.5, 5.9), (8, 7.2), (5.5, 5.9)], bg)
    I.poly([(3.4, 6.8), (7.5, 9.2), (7.5, 11.8), (3.4, 9.4)], ink)
    I.poly([(8.5, 9.2), (12.6, 6.8), (12.6, 9.4), (8.5, 11.8)], ink)


def s_garbage(I, ink, bg):
    I.ellipse(8, 8.5, 4.3, 3.2, ink)
    I.rect(7.0, 5.0, 2.0, 2.0, ink)
    I.poly([(5.8, 3.0), (10.2, 3.0), (9.2, 5.2), (6.8, 5.2)], ink)
    I.ellipse(6.3, 8.0, 0.55, 1.0, bg)


def s_fire(I, ink, bg):
    I.poly([(8.6, 2.9), (10.0, 5.0), (11.6, 7.4), (11.6, 9.4), (10.2, 11.4), (8, 11.9), (5.8, 11.4),
            (4.4, 9.6), (4.5, 7.6), (5.7, 6.0), (6.6, 7.2), (7.5, 5.4)], ink)
    I.ellipse(8, 9.6, 3.6, 2.2, ink)
    I.poly([(8.2, 7.2), (9.6, 9.0), (9.6, 10.4), (8, 11.2), (6.4, 10.4), (6.4, 9.2)], "#f2d04c")


def s_crime(I, ink, bg):
    I.ellipse(8, 7.2, 3.1, 3.0, "#f6c898")
    I.poly([(4.4, 6.0), (5.2, 3.0), (10.8, 3.0), (11.6, 6.0)], ink)
    I.rect(4.2, 5.6, 7.6, 1.0, ink)
    I.rect(4.7, 6.6, 6.6, 1.7, ink)
    I.rect(5.7, 6.9, 1.2, 1.0, "#ffffff")
    I.rect(9.1, 6.9, 1.2, 1.0, "#ffffff")
    I.poly([(3.2, 11.7), (4.0, 10.0), (12.0, 10.0), (12.8, 11.7)], ink)
    I.rect(4.0, 10.9, 8.0, 0.5, bg)


def s_sick(I, ink, bg):
    I.line(10.8, 3.6, 5.8, 8.6, ink, 0, 2.5)
    I.ellipse(5.2, 9.4, 2.6, 2.6, ink)
    I.line(10.0, 4.4, 7.4, 7.0, bg, 0, 0.8)
    I.ellipse(5.2, 9.4, 1.4, 1.4, "#e54b3c")
    I.line(5.2, 9.4, 7.2, 7.4, "#e54b3c", 0, 0.8)


def s_cross(I, ink, bg):
    I.rect(6.4, 3.3, 3.2, 8.2, ink)
    I.rect(3.8, 5.9, 8.4, 3.2, ink)


def s_highrent(I, ink, bg):
    I.poly([(8, 2.8), (13.2, 6.6), (2.8, 6.6)], ink)
    I.rect(3.9, 6.4, 8.2, 5.4, ink)
    I.text(6.5, 5.9, "$", bg, 0)


def s_traffic(I, ink, bg):
    for x, lt in ((2.6, None), (8.0, "#e54b3c")):
        I.rect(x, 7.0, 5.2, 2.4, ink)
        I.poly([(x + 1.2, 7.1), (x + 1.9, 5.0), (x + 3.5, 5.0), (x + 4.2, 7.1)], ink)
        I.rect(x + 2.0, 5.6, 1.4, 1.1, bg)
        I.ellipse(x + 1.3, 9.9, 1.15, 1.15, ink)
        I.ellipse(x + 3.9, 9.9, 1.15, 1.15, ink)
        if lt:
            I.rect(x + 4.4, 7.5, 0.8, 1.0, lt)


def s_smoke(I, ink, bg):
    I.rect(3.4, 8.6, 9.2, 3.1, ink)
    I.rect(4.4, 5.8, 2.2, 3.0, ink)
    I.rect(4.4, 7.0, 2.2, 0.7, bg)
    I.ellipse(7.6, 5.3, 1.3, 1.2, ink)
    I.ellipse(9.8, 4.4, 1.6, 1.3, ink)
    I.ellipse(12.0, 4.4, 1.0, 1.0, ink)
    for x in (6.0, 8.6, 11.2):
        I.rect(x, 9.8, 0.9, 0.9, bg)


def s_ground(I, ink, bg):
    I.ellipse(8, 9.6, 5.0, 2.2, ink)
    I.ellipse(6.8, 9.0, 1.7, 0.6, bg)
    I.ellipse(5.9, 5.8, 1.3, 1.3, ink)
    I.ellipse(9.2, 4.6, 1.7, 1.7, ink)
    I.ellipse(11.0, 7.2, 1.0, 1.0, ink)
    I.ellipse(8.7, 4.1, 0.55, 0.55, bg)
    I.ellipse(5.5, 5.4, 0.45, 0.45, bg)


def s_noise(I, ink, bg):
    I.poly([(3.2, 6.0), (5.2, 6.0), (7.8, 3.6), (7.8, 11.0), (5.2, 8.6), (3.2, 8.6)], ink)
    _arc(I, 7.6, 7.3, 2.6, -50, 50, ink, 1.2)
    _arc(I, 7.6, 7.3, 4.4, -48, 48, ink, 1.2)


def s_levelup(I, ink, bg):
    I.poly([(8, 3.0), (12.8, 7.8), (9.6, 7.8), (9.6, 11.6), (6.4, 11.6), (6.4, 7.8), (3.2, 7.8)], ink)


def s_collapsed(I, ink, bg):
    C = [(8.4, 3.0), (7.2, 5.4), (8.8, 7.4), (7.4, 9.4), (8.2, 11.9)]
    I.poly([(3.8, 3.0)] + [(x - 0.55, y) for x, y in C] + [(3.8, 11.9)], ink)
    I.poly([(7.9, 5.5), (12.2, 5.5), (12.2, 11.9), (8.9, 11.9), (8.0, 9.4), (9.4, 7.4)], ink)
    I.rect(4.9, 4.8, 1.4, 1.6, bg)
    I.rect(4.9, 8.6, 1.4, 1.6, bg)


def s_noservice(I, ink, bg):
    I.line(4.6, 11.0, 9.0, 6.4, ink, 0, 2.3)
    I.ellipse(10.0, 5.2, 2.7, 2.7, ink)
    I.poly([(10.0, 5.2), (13.4, 3.4), (13.6, 6.0)], bg)
    _cross_out(I, 7.4, 7.2, 12.2, 11.8, ink)


def s_flooded(I, ink, bg):
    for y in (5.0, 7.7, 10.4):
        _wave(I, 3.4, 12.6, y, 0.7, ink, 1.5, 12, 2.0)


def s_wildfire(I, ink, bg):
    fl = "#f2d04c"
    I.poly([(8, 3.6), (11.2, 7.0), (4.8, 7.0)], ink)
    I.poly([(8, 5.4), (12.0, 10.0), (4.0, 10.0)], ink)
    I.rect(7.2, 9.8, 1.6, 2.0, ink)
    I.poly([(8.6, 2.4), (9.8, 4.2), (10.0, 6.2), (8, 7.4), (6.2, 6.2), (6.4, 4.6), (7.4, 5.2)], fl)
    I.poly([(4.4, 6.4), (5.2, 8.2), (6.0, 9.4), (4.2, 9.6), (3.6, 8.0)], fl)
    I.poly([(11.6, 6.4), (12.4, 8.0), (11.8, 9.6), (10.0, 9.4), (10.8, 8.0)], fl)


def s_accident(I, ink, bg):
    pts = []
    for i in range(20):
        r = 5.0 if i % 2 == 0 else 3.5
        a = math.radians(-90 + i * 18)
        pts.append((8 + r * math.cos(a), 7.1 + r * math.sin(a)))
    I.poly(pts, ink)
    I.poly([(6.9, 4.3), (9.1, 4.3), (8.7, 8.3), (7.3, 8.3)], bg)
    I.ellipse(8, 9.7, 1.15, 1.15, bg)


def s_dot(I, ink, bg):
    I.ellipse(8, 7.3, 2.2, 2.2, ink)


def s_i(I, ink, bg):
    I.ellipse(8, 4.5, 1.3, 1.3, ink)
    I.rect(6.6, 6.4, 2.8, 0.9, ink)
    I.rect(7.0, 6.4, 2.0, 5.2, ink)
    I.rect(6.2, 10.7, 3.6, 0.9, ink)


def s_bang(I, ink, bg):
    _bang(I, 8, ink, 2.4)


def s_bang2(I, ink, bg):
    _bang(I, 5.8, ink, 2.0)
    _bang(I, 10.2, ink, 2.0)


def s_x(I, ink, bg):
    I.line(4.8, 3.8, 11.2, 10.6, ink, 0, 2.2)
    I.line(11.2, 3.8, 4.8, 10.6, ink, 0, 2.2)


def s_skull(I, ink, bg):
    I.ellipse(8, 6.7, 4.0, 3.6, ink)
    I.rect(5.9, 8.6, 4.2, 2.8, ink)
    I.ellipse(6.3, 6.9, 1.1, 1.3, bg)
    I.ellipse(9.7, 6.9, 1.1, 1.3, bg)
    I.poly([(8, 8.0), (8.7, 9.2), (7.3, 9.2)], bg)
    I.rect(7.5, 10.0, 0.5, 1.4, bg)
    I.rect(8.6, 10.0, 0.5, 1.4, bg)


def s_tick(I, ink, bg):
    I.line(4.6, 7.8, 7.0, 10.4, ink, 0, 2.3)
    I.line(7.0, 10.4, 11.6, 4.4, ink, 0, 2.3)


# ---- registration --------------------------------------------------------------------------------------
_ICONS = [
    # name, group label, tier, symbol
    ("st_no_power", "No electricity", "warning", s_bolt),
    ("st_no_water", "No water", "warning", s_drop),
    ("st_no_sewage", "No sewage", "warning", s_sewage),
    ("st_dirty_water", "Dirty water", "warning", s_dirty),
    ("st_no_road", "No road access", "problem", s_noroad),
    ("st_abandoned", "Abandoned", "problem", s_abandoned),
    ("st_unhappy", "Unhappy citizens", "info", s_unhappy),
    ("st_no_workers", "Not enough workers", "info", s_noworkers),
    ("st_no_customers", "Not enough customers", "info", s_nocust),
    ("st_no_goods", "No goods", "problem", s_nogoods),
    ("st_garbage", "Garbage", "warning", s_garbage),
    ("st_fire", "Fire", "major", s_fire),
    ("st_crime", "Crime", "info", s_crime),
    ("st_sick", "Sick citizens", "problem", s_sick),
    ("st_ambulance", "Ambulance needed", "major", s_cross),
    ("st_high_rent", "High rent", "info", s_highrent),
    ("st_traffic", "Traffic jam", "warning", s_traffic),
    ("st_air_pollution", "Air pollution", "warning", s_smoke),
    ("st_ground_pollution", "Ground pollution", "warning", s_ground),
    ("st_noise", "Noise", "warning", s_noise),
    ("st_leveled_up", "Leveled up", "good", s_levelup),
    ("st_collapsed", "Collapsed", "error", s_collapsed),
    ("st_no_service", "No service", "problem", s_noservice),
    ("st_flooded", "Flooded", "warning", s_flooded),
    ("st_wildfire", "Wildfire", "major", s_wildfire),
    ("st_accident", "Accident", "warning", s_accident),
    ("tier_minimal", "Minimal", "minimal", s_dot),
    ("tier_info", "Info", "info", s_i),
    ("tier_problem", "Problem", "problem", s_bang),
    ("tier_warning", "Warning", "warning", s_bang),
    ("tier_major", "Major", "major", s_bang2),
    ("tier_error", "Error", "error", s_x),
    ("tier_fatal", "Fatal", "fatal", s_skull),
    ("tier_good", "Good", "good", s_tick),
]


def _register(name, label, tier, sym):
    @icon(name, "status", label)
    def _(I):
        sbadge(I, tier)
        sym(I, TIER[tier][1], TIER[tier][0])


for _n, _l, _t, _s in _ICONS:
    _register(_n, _l, _t, _s)
