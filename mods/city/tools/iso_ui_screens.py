"""
iso_ui_screens - full-screen HUD mockups (1280x720 / 1920x1080, UI 1x / 1.5x) and the sidebar variant.
"""

import numpy as np

from iso_ui_kit import Canvas, R, INK, WHITE, hexc, MONEY_POS, MONEY_NEG
from iso_ui_chrome import Theme
from iso_ui_widgets2 import tooltip
from iso_ui_icons import ic
import iso_ui_world as W
import iso_ui_hud as H
import iso_ui_panels_a as A

SCREENS = []  # (stem, title, fn, w, h, u)


def scr(stem, title, w, h, u):
    def deco(fn):
        SCREENS.append((stem, title, fn, w, h, u))
        return fn
    return deco


MOCK_BG = __import__("os").path.join(__import__("os").path.dirname(__import__("os").path.abspath(__file__)),
                                     "..", "design", "iso", "mockups", "hud-bg")


def world(w, h, seed=3, night=False, kind=None):
    """World backdrop. Uses KIT's real-city mockup renders (design/iso/mockups/hud-bg, written by
    tools/iso_mockups.py) when present; falls back to the placeholder city of iso_ui_world."""
    import os
    from iso_ui_kit import Canvas
    import isokit as ik
    kind = kind or ("viewport" if seed == 5 else "day")
    path = os.path.join(MOCK_BG, "%s-%dx%d.png" % (kind, w, h))
    if os.path.exists(path):
        c = Canvas(w, h)
        c.a[:] = ik.read_png(path)
        return c
    return W.scene(w, h, seed=seed, lit=night)


def world_overlays(c, T, pts):
    """Status bubbles floating over buildings (world-space, LIFE style) and a selection footprint."""
    n = 16 if T.u < 1.25 else 24
    for (x, y, nm) in pts:
        c.blit(ic(nm, n), x, y)


def footprint(c, ox, oy, x0, y0, x1, y1, colr=None):
    colr = colr or hexc("#ffe040")
    S = lambda x, y: (ox + (x - y) * 32, oy + (x + y) * 16)
    corners = [S(x0, y0), S(x1, y0), S(x1, y1), S(x0, y1)]
    for i in range(4):
        (ax, ay), (bx, by) = corners[i], corners[(i + 1) % 4]
        n = int(max(abs(bx - ax), abs(by - ay)))
        for k in range(n + 1):
            if (k // 4) % 2 == 0:
                c.fill(int(ax + (bx - ax) * k / n), int(ay + (by - ay) * k / n), 2, 1, colr)


def hud(c, T, Wd, Ht, active=("cat_health",), windows=(), tip=None, cursor_at=None, mode=None):
    for fn in windows:
        fn(c, T)
    H.toolbar(c, T, Wd, active, mode)
    H.statusbar(c, T, Wd, Ht)
    if tip:
        x, y, title, lines = tip
        tooltip(c, T, x, y, lines, title=title)
    if cursor_at:
        H.cursor(c, cursor_at[0], cursor_at[1], max(1, int(T.u)))


def _main(Wd, Ht, u, extra=False):
    T = Theme(u)
    s = T.s
    c = world(Wd, Ht)
    world_overlays(c, T, [(Wd // 2 - 180, Ht // 2 - 40, "st_no_power"), (Wd // 2 + 90, Ht // 2 + 60, "st_garbage"),
                          (Wd // 2 - 40, Ht // 2 + 140, "st_traffic")])
    vp = world(s(300), s(110), seed=5)
    tbh = s(30) + 2 * T.b
    wins = [lambda c, T: A.build_menu(c, T, s(8), tbh + s(10)),
            lambda c, T: A.inspector(c, T, Wd - s(310), tbh + s(10), tab=0, viewport=vp)]
    if extra:
        import iso_ui_panels_finance as PF
        wins.append(lambda c, T: PF.budget(c, T, s(8), tbh + s(256), tab=4))
    hud(c, T, Wd, Ht, ("cat_health",), wins,
        tip=(Wd // 2 + s(30), Ht // 2 + s(10), "Hospital", ["Cost: $22,000", ("Upkeep: -$640 / month", MONEY_NEG), "Coverage: 4 tiles"]),
        cursor_at=(Wd // 2 + s(14), Ht // 2 - s(6)))
    return c


@scr("hud-1280x720-1x", "Main HUD 1280x720, UI 1x (medium toolbar)", 1280, 720, 1)
def _(): return _main(1280, 720, 1)


@scr("hud-1920x1080-1x", "Main HUD 1920x1080, UI 1x (wide toolbar)", 1920, 1080, 1)
def _(): return _main(1920, 1080, 1, extra=True)


@scr("hud-1920x1080-1.5x", "Main HUD 1920x1080, UI 1.5x", 1920, 1080, 1.5)
def _(): return _main(1920, 1080, 1.5)


@scr("hud-1280x720-1.5x", "Main HUD 1280x720, UI 1.5x (compact toolbar)", 1280, 720, 1.5)
def _(): return _main(1280, 720, 1.5)


def _minimap(c, x, y, w, h):
    """Top-down minimap: one pixel per (cell/2), colours from the placeholder city layout."""
    import numpy as np
    ys, xs = np.mgrid[0:h, 0:w]
    cx, cy = xs // 2 - 10, ys // 2 - 10
    a = c.a[y:y + h, x:x + w]
    a[:] = hexc("#4f8a2c")
    zone = ((cx // 7) * 5 + (cy // 6) * 3) % 7
    for z, colr in ((1, "#b8402c"), (2, "#c84a32"), (3, "#a89c88"), (4, "#a4583e"), (5, "#6a9ac0"), (6, "#c8b060")):
        a[zone == z] = hexc(colr)
    a[(cy % 6 == 0) | (cx % 7 == 0)] = hexc("#5a5a60")
    d = np.abs((cx - cy) - 3 - ((cx + cy) // 9) % 2)
    a[(d <= 1) & (cx + cy > 28)] = hexc("#2a5cb0")


@scr("hud-sidebar-1280x720-1x", "Alternative: current right-sidebar layout in RCT2 chrome (1280x720, 1x)", 1280, 720, 1)
def sidebar_variant(Wd=1280, Ht=720, u=1):
    from iso_ui_chrome import bevel, fam, icon_button, button
    from iso_ui_widgets import well
    from iso_ui_widgets2 import meter, rci, ticker
    from iso_ui_parts import thumb, card, header
    import iso_ui_font as F
    T = Theme(u)
    s = T.s
    c = world(Wd, Ht)
    sw = s(226)
    sx = Wd - sw
    ramp, body, _ = fam("city")
    c.fill(sx - T.b, 0, sw + T.b, Ht, R(ramp, 0))
    bevel(c, sx, 0, sw, Ht, ramp, "out", body, T.b, 7, 2)
    x, y, iw = sx + s(5), s(5), sw - s(10)
    # cash row (dark sunken readout like RCT2's money display)
    bevel(c, x, y, iw, s(18), ramp, "in", 0, T.b, 5, 0)
    c.blit(ic("stat_money", 16 if u < 1.25 else 24), x + s(3), y + s(1))
    c.text(x + s(24), y + s(5), "$1,254,300", T.f_value, hexc("#7cf06c"))
    c.text(x, y + s(5), "+$12,400/mo", T.f_small, hexc("#7cf06c"), align="right", w=iw - s(4))
    y += s(22)
    mh = s(110)
    bevel(c, x, y, iw, mh, ramp, "in", 1, T.b, 7, 1)
    _minimap(c, x + T.b, y + T.b, iw - 2 * T.b, mh - 2 * T.b)
    y += mh + s(4)
    # status block: milestone, happiness, power, water + RCI
    n = 16 if u < 1.25 else 24
    rows = [("pnl_milestone", "Large Town", 0.62, "yellow"), ("stat_happiness", "76%", 0.76, "green"),
            ("info_power", "48 / 60 MW", 0.8, "orange"), ("info_water", "31 / 40 kL", 0.77, "blue")]
    for i, (icn, lab, v, r) in enumerate(rows):
        ry = y + i * s(17)
        c.blit(ic(icn, n), x, ry)
        c.text(x + n + s(3), ry + s(4), lab, T.f_small, INK)
        meter(c, T, x + s(86), ry + s(3), iw - s(86) - s(44), v, "city", r)
    rci(c, T, x + iw - s(41), y, s(66), [0.8, 0.35, -0.3, 0.55], family="city")
    y += s(70)
    # panel buttons (order rows): 2 x 6 raised icon buttons
    pn = ["pnl_infoviews", "pnl_budget", "pnl_stats", "pnl_chirper", "pnl_production", "pnl_policies",
          "pnl_progression", "pnl_districts", "pnl_tiles", "pnl_transit_lines", "pnl_achievements", "pnl_advisor"]
    bw = iw // 6
    for i, nm in enumerate(pn):
        icon_button(c, T, x + (i % 6) * bw, y + (i // 6) * s(26), bw - s(1), s(25), ic(nm, n), "city",
                    "toggled" if nm == "pnl_budget" else "normal", flat=False)
    y += s(56)
    # build category tabs: 3 rows x 6
    cats = ["cat_roads", "cat_zoning", "cat_networks", "cat_power", "cat_water", "cat_garbage", "cat_police", "cat_fire",
            "cat_health", "cat_deathcare", "cat_education", "cat_parks", "cat_comms", "cat_admin", "cat_industry",
            "cat_transit", "cat_signature", "tool_bulldoze"]
    for i, nm in enumerate(cats):
        icon_button(c, T, x + (i % 6) * bw, y + (i // 6) * s(26), bw - s(1), s(25), ic(nm, n), "zoning",
                    "toggled" if nm == "cat_health" else "normal", flat=False)
    y += s(82)
    y += header(c, T, x, y, iw, "Health", "city")
    th = 64 if u < 1.25 else 96
    items = [("clinic", "Clinic", "$4,800", "normal"), ("hospital", "Hospital", "$22,000", "selected"), ("cemetery", "Medevac", "$9,500", "locked")]
    for i, (k, nm, cost, st) in enumerate(items):
        card(c, T, x + i * (iw // 3), y, thumb(k, th), nm, cost, "city", st)
    # left-top: speed strip + date; bottom-left ticker
    H.strip(c, T, 0, 0, ["pnl_game_menu", "time_pause", "time_play", "time_fast", "time_fastest"], "city", ("time_play",))
    bevel(c, s(160), 0, s(110), s(29), ramp, "out", body, T.b, 7, 1)
    c.text(s(166), s(5), "Mar 14, 2031", T.f_value, INK)
    c.text(s(166), s(17), "14:20  18°C", T.f_small, INK)
    ticker(c, T, s(4), Ht - s(20), sx - s(8), "@MayorBot: Traffic on Elm Avenue cleared, buses back on time!", "city", ic("pnl_chirper", n))
    vp = world(s(300), s(110), seed=5)
    A.inspector(c, T, sx - s(310), s(40), tab=0, viewport=vp)
    H.cursor(c, Wd // 2 - s(60), Ht // 2, 1)
    return c


def heat_overlay(c, fn):
    """Info-view look: desaturate the world, then tint every pixel by a heat value 0..1 (blue -> green -> yellow -> red)."""
    import numpy as np
    a = c.a[:, :, :3].astype(float)
    lum = (a[:, :, 0] * 0.3 + a[:, :, 1] * 0.59 + a[:, :, 2] * 0.11) / 255.0
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    v = fn(xs, ys)
    ramp = np.array([[40, 80, 200], [60, 180, 110], [235, 205, 60], [210, 50, 40]], float)  # matches legend low..high
    t = np.clip(v, 0, 1) * 3
    i = np.minimum(t.astype(int), 2)
    f = (t - i)[..., None]
    col = ramp[i] * (1 - f) + ramp[i + 1] * f
    out = col * (0.45 + 0.75 * lum[..., None]) * 0.7 + (lum[..., None] * 255) * 0.3
    c.a[:, :, :3] = np.clip(out, 0, 255).astype(np.uint8)


@scr("hud-infoview-1920x1080-1x", "Info view active: land value overlay + info-view panel + legend (1920x1080, 1x)", 1920, 1080, 1)
def infoview_screen(Wd=1920, Ht=1080, u=1):
    import numpy as np
    import iso_ui_panels_transit as PT
    T = Theme(u)
    s = T.s
    c = world(Wd, Ht, kind="infoview")
    if not __import__("os").path.exists(__import__("os").path.join(MOCK_BG, "infoview-%dx%d.png" % (Wd, Ht))):
        heat_overlay(c, lambda x, y: 1.0 - np.hypot((x - Wd * 0.55) / Wd, (y - Ht * 0.45) / Ht) * 1.8
                     + 0.08 * np.sin(x / 70.0) * np.cos(y / 50.0))
    tbh = s(30) + 2 * T.b
    from iso_ui_kit import Canvas as _C
    pw, _ = PT.infoviews_panel(_C(Wd, Ht), T, 0, 0)
    lw, lh = PT.legend_landvalue(_C(Wd, Ht), T, 0, 0)
    wins = [lambda c, T: PT.infoviews_panel(c, T, Wd - pw - s(4), tbh + s(10)),
            lambda c, T: PT.legend_landvalue(c, T, s(8), Ht - s(48) - lh)]
    hud(c, T, Wd, Ht, ("pnl_infoviews",), wins, cursor_at=(Wd // 2, Ht // 2))
    return c
