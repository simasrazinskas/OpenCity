"""
iso_ui_panels_people - RCT2-style windows for people and city management: citizen inspector, vehicle
inspector, chirper feed, advisor, milestones / development tree / achievements, policies, districts.

Panel convention (see iso_ui_panels_a): def panel_x(c, T, x, y, **opts) -> (w, h); PANELS registers renders.
"""

import numpy as np

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import Theme, window, button, icon_button, bevel, fam, etched_h, etched_v
from iso_ui_widgets import well, checkbox, radio, slider, list_rows, scrollbar, dropdown, textinput, arrow
from iso_ui_widgets2 import meter, graph, value_row, groupbox, money
from iso_ui_parts import thumb, card, header, chip
from iso_ui_icons import ic
import iso_ui_world as W
import iso_ui_font as F

PANELS = []  # (file stem, title, fn, w_hint, h_hint, kwargs)


def reg(stem, title, w, h, **kw):
    def deco(fn):
        PANELS.append((stem, title, fn, w, h, kw))
        return fn
    return deco


# ---- shared helpers --------------------------------------------------------------------------------
_SCENES = {}


def _scene(w, h, seed, t):
    key = (w, h, seed, round(t, 2))
    if key not in _SCENES:
        _SCENES[key] = W.scene(w, h, seed=seed, t=t, ox=w // 2 - int(96 * t), oy=h // 2 - int(64 * t))
    return _SCENES[key]


def fit(text, face, w):
    """Truncate text with '..' so it fits in w pixels."""
    if F.measure(text, face) <= w:
        return text
    while text and F.measure(text + "..", face) > w:
        text = text[:-1]
    return text.rstrip() + ".."


def wrap(text, face, w):
    lines, cur = [], ""
    for word in text.split():
        t = (cur + " " + word).strip()
        if F.measure(t, face) <= w or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


def _dim(icon, ramp, t=0.6):
    """Locked look: icon colours washed toward a mid grey of the window ramp."""
    out = Canvas(icon.w, icon.h)
    a = icon.a.copy()
    tgt = np.array(R(ramp, 3)[:3], np.float32)
    a[:, :, :3] = (a[:, :, :3] * (1 - t) + tgt * t).astype(np.uint8)
    out.a = a
    return out


def _loc(c, T, x, y, family, side=None, name="ui_locate", state="normal"):
    side = side or T.s(18)
    icon_button(c, T, x, y, side, side, ic(name, T.icon), family, state, flat=False)
    return side


def _person(c, T, px, py, shirt="#d84a3a", arrow_on=True):
    """Tiny pixel citizen standing with feet at (px, py) + yellow locator arrow."""
    k = max(1, int(T.u))
    c.fill(px - 3 * k, py - k, 6 * k, 2 * k, (0, 0, 0, 255))
    c.fill(px - 2 * k, py - 4 * k, k + 0, 4 * k - k, hexc("#2a3a6a"))
    c.fill(px + 0, py - 4 * k, k, 3 * k, hexc("#2a3a6a"))
    c.fill(px - 2 * k, py - 8 * k, 4 * k, 4 * k, hexc(shirt))
    c.fill(px - 3 * k, py - 8 * k, k, 3 * k, hexc(shirt))
    c.fill(px + 2 * k, py - 8 * k, k, 3 * k, hexc(shirt))
    c.fill(px - k, py - 11 * k, 3 * k - k, 3 * k, hexc("#f0c8a0"))
    c.fill(px - k, py - 11 * k, 2 * k, k, hexc("#4a2a14"))
    if arrow_on:
        a = 4 * k
        arrow(c, px, py - 20 * k, a, "down", hexc("#ffe040"))


def _vehicle(c, T, px, py):
    """Small iso bus standing at viewport centre (cell centre = (px, py))."""
    t = T.u
    ox, oy = px - 96 * t, py - 64 * t
    wall = ([hexc("#6aa0e8"), hexc("#4a7ac8"), hexc("#2a5aa8")])
    roof = ([hexc("#a8c8f0"), hexc("#c4dcf8"), hexc("#7a9ac8")])
    W.building(c, ox, oy, 3.05, 0.36, 3.95, 0.64, 9, [hexc("#4a7ac8"), hexc("#4a7ac8"), hexc("#2a5aa8")], roof, t, True, "flat", False)
    arrow(c, px, py - int(30 * t), max(3, int(4 * t)), "down", hexc("#ffe040"))


def _viewport(c, T, x, y, w, h, family, fig, seed=5):
    bevel(c, x, y, w, h, fam(family)[0], "in", 1, T.b, 7, 1)
    iw, ih = w - 2 * T.b, h - 2 * T.b
    c.blit(_scene(iw, ih, seed, T.u), x + T.b, y + T.b)
    fig(c, T, x + T.b + iw // 2, y + T.b + ih // 2)


def _mrow(c, T, x, y, w, label, val, family, ramp, txt, lw=54):
    s = T.s
    c.text(x, y, label, T.f_label, INK)
    meter(c, T, x + s(lw), y - s(1), w - s(lw) - s(40), val, family, ramp)
    c.text(x, y, txt, T.f_value, INK, align="right", w=w)


# ---- 1. citizen inspector --------------------------------------------------------------------------
CIT_TABS = ["pnl_citizen", "stat_households", "time_calendar"]
THOUGHTS = [("st_no_workers", "I can't find a job nearby"), ("st_high_rent", "The rent here is too high"),
            ("stat_happiness", "I love the new park on Elm Street")]


@reg("citizen-overview", "Citizen inspector: overview", 280, 256, tab=0)
@reg("citizen-household", "Citizen inspector: household", 280, 256, tab=1)
@reg("citizen-lifepath", "Citizen inspector: life path", 280, 256, tab=2)
def citizen(c, T, x, y, tab=0, family="people"):
    s = T.s
    w, h = s(280), s(256)
    tabs = [ic(n, T.icon) for n in CIT_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Emma Walker", family, tabs, tab)
    if tab == 0:
        vw, vh = s(104), s(76)
        _viewport(c, T, cx, cy, vw, vh, family, lambda c, T, px, py: _person(c, T, px, py + s(6)), seed=5)
        rx = cx + vw + s(6)
        rw = cw - vw - s(6)
        ty = cy + s(1)
        c.text(rx, ty, "Emma Walker", T.f_big, R("purple", 1))
        c.text(rx + 1, ty, "Emma Walker", T.f_big, R("purple", 1))
        ty += s(15)
        c.text(rx, ty, "Adult, 34 years", T.f_label, INK)
        ty += s(12)
        c.text(rx, ty, "Educated", T.f_label, INK)
        ty += s(12)
        c.text(rx, ty, "Going to work", T.f_label, R("blue", 2))
        c.blit(ic("tr_bus", T.icon), rx + rw - T.icon, ty - s(4))
        ty += s(14)
        _mrow(c, T, rx, ty, rw, "Happy", 0.72, family, "green", "72%", 40)
        ty += s(12)
        _mrow(c, T, rx, ty, rw, "Health", 0.88, family, "blue", "88%", 40)
        ty = cy + vh + s(5)
        value_row(c, T, cx, ty, cw, "Cash", money(2340), MONEY_POS)
        ty += s(14)
        for icn, lab, val in (("stat_home", "Home", "14 Elm Street"), ("stat_work", "Work", "Greenfield Mall")):
            bevel(c, cx, ty, cw, s(18), fam(family)[0], "in", 7, T.b, 7, 2)
            c.blit(ic(icn, T.icon), cx + s(2), ty + (s(18) - T.icon) // 2)
            c.text(cx + T.icon + s(6), ty + (s(18) - F.cap_height(T.f_label)) // 2, lab + ":", T.f_small, R("purple", 2))
            c.text(cx + T.icon + s(40), ty + (s(18) - F.cap_height(T.f_label)) // 2, val, T.f_label, INK)
            _loc(c, T, cx + cw - s(18), ty, family)
            ty += s(20)
        ty += s(2)
        ty += header(c, T, cx, ty, cw, "Thoughts", family)
        rh = T.icon + s(2)
        well(c, T, cx, ty, cw, 3 * rh + 2 * T.b, family)
        for i, (n, txt) in enumerate(THOUGHTS):
            ry = ty + T.b + i * rh
            if i % 2:
                c.fill(cx + T.b, ry, cw - 2 * T.b, rh, mix(R("purple", 7), R("purple", 6), 0.55))
            c.blit(ic(n, T.icon), cx + s(3), ry + s(1))
            c.text(cx + T.icon + s(8), ry + (rh - F.cap_height(T.f_label)) // 2, txt, T.f_label, INK)
    elif tab == 1:
        ty = cy
        ty += header(c, T, cx, ty, cw, "Household 14 Elm Street", family)
        rows = [("Emma Walker", "Self", "34", "Mall clerk"), ("Jack Walker", "Partner", "36", "Engineer"),
                ("Lily Walker", "Child", "9", "School"), ("Noah Walker", "Child", "5", "Home"),
                ("Grace Hill", "Parent", "68", "Retired")]
        rh = list_rows(c, T, cx, ty, cw, rows, family, hover=None, selected=0,
                       cols=[(s(86), "left"), (s(52), "left"), (s(30), "right"), (cw - s(168), "left")],
                       header=("Name", "Role", "Age", "Occupation"))
        ty += rh + s(8)
        ty += header(c, T, cx, ty, cw, "Household finances", family)
        for lab, v, col in (("Income / month", money(5240, True), MONEY_POS), ("Rent", money(-1380), MONEY_NEG),
                            ("Savings", money(8420), MONEY_POS)):
            value_row(c, T, cx, ty, cw, lab, v, col)
            ty += s(13)
        ty += s(2)
        c.text(cx, ty, "Happiness", T.f_label, INK)
        meter(c, T, cx + s(60), ty - s(1), cw - s(100), 0.68, family, "green")
        c.text(cx, ty, "68%", T.f_value, INK, align="right", w=cw)
        ty += s(18)
        bevel(c, cx, ty, cw, s(18), fam(family)[0], "in", 7, T.b, 7, 2)
        c.blit(ic("stat_home", T.icon), cx + s(2), ty + (s(18) - T.icon) // 2)
        c.text(cx + T.icon + s(6), ty + (s(18) - F.cap_height(T.f_label)) // 2, "14 Elm Street, 5 residents", T.f_label, INK)
        _loc(c, T, cx + cw - s(18), ty, family)
    else:
        ty = cy
        ty += header(c, T, cx, ty, cw, "Life events", family)
        ev = [("stat_home", "Mar 2071", "Moved in at 14 Elm Street", "yellow"), ("stat_work", "Sep 2068", "Got a job at Greenfield Mall", "green"),
              ("cat_education", "Jun 2065", "Graduated from Westside College", "blue"),
              ("cat_education", "Jun 2062", "Graduated from Riverside High", "blue"),
              ("cat_education", "Sep 2051", "Started school", "teal"), ("cat_health", "Apr 2043", "Born at St. Mary Hospital", "red")]
        rh = s(28)
        wh = min(len(ev) * rh + 2 * T.b, ch - (ty - cy))
        well(c, T, cx, ty, cw - s(12), wh, family)
        nd = T.icon + s(4)
        lx = cx + T.b + s(4) + nd // 2
        c.fill(lx - T.b, ty + T.b + rh // 2, 2 * T.b, (len(ev) - 1) * rh, R("purple", 3))
        for i, (n, date, txt, rc) in enumerate(ev):
            ry = ty + T.b + i * rh
            if i % 2:
                c.fill(cx + T.b, ry, cw - s(12) - 2 * T.b, rh, mix(R("purple", 7), R("purple", 6), 0.55))
            if i == 0:
                c.fill(lx - T.b, ry, 2 * T.b, rh // 2, R("purple", 7) if i % 2 == 0 else mix(R("purple", 7), R("purple", 6), 0.55))
            bevel(c, lx - nd // 2, ry + (rh - nd) // 2, nd, nd, rc, "out", 4, T.b, 7, 1)
            c.blit(ic(n, T.icon), lx - T.icon // 2, ry + (rh - T.icon) // 2)
            c.text(lx + nd // 2 + s(6), ry + s(5), txt, T.f_label, INK)
            c.text(lx + nd // 2 + s(6), ry + s(16), date, T.f_small, R("purple", 3))
        scrollbar(c, T, cx + cw - s(10), ty, wh, family, 0.0, 0.6)
    return w, h


# ---- 2. vehicle inspector --------------------------------------------------------------------------
VEH_TABS = ["pnl_vehicle", "tr_line"]
STOPS = [("Central Station", "now", 1), ("Elm Street", "1 min", 0), ("Riverside Park", "3 min", 0),
         ("Westside College", "5 min", 0), ("Greenfield Mall", "8 min", 0), ("Harbor Terminal", "11 min", 0)]


@reg("vehicle-overview", "Vehicle inspector: overview", 260, 196, tab=0)
@reg("vehicle-route", "Vehicle inspector: route", 260, 196, tab=1)
def vehicle(c, T, x, y, tab=0, family="transit"):
    s = T.s
    w, h = s(260), s(196)
    tabs = [ic(n, T.icon) for n in VEH_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Bus 4-012", family, tabs, tab)
    side = s(22)
    bx = cx + cw - side + T.b
    for i, n in enumerate(["ui_locate", "ui_follow"]):
        icon_button(c, T, bx, cy + i * (side + s(2)), side, side, ic(n, T.icon), family, "toggled" if i == 1 else "normal", flat=False)
    mw = cw - side - s(4)
    if tab == 0:
        vw, vh = s(96), s(60)
        _viewport(c, T, cx, cy, vw, vh, family, _vehicle, seed=6)
        rx = cx + vw + s(6)
        rw = mw - vw - s(6)
        ty = cy
        c.text(rx, ty, "City Bus", T.f_value, R("blue", 1)); c.text(rx + 1, ty, "City Bus", T.f_value, R("blue", 1))
        ty += s(14)
        chip(c, T, rx, ty, "Line 4", "blue", icon=ic("tr_line", 16 if T.u < 1.25 else 24))
        ty += s(20)
        c.text(rx, ty, "Driver: Tom Reyes", T.f_label, INK)
        ty = cy + vh + s(6)
        _mrow(c, T, cx, ty, mw, "Passengers", 38 / 60.0, family, "green", "38 / 60", 62)
        ty += s(13)
        _mrow(c, T, cx, ty, mw, "Condition", 0.81, family, "yellow", "81%", 62)
        ty += s(14)
        value_row(c, T, cx, ty, mw, "Speed", "42 km/h", INK)
        ty += s(13)
        value_row(c, T, cx, ty, mw, "Status", "In service", MONEY_POS)
        ty += s(15)
        bevel(c, cx, ty, mw, s(18), fam(family)[0], "in", 7, T.b, 7, 2)
        c.text(cx + s(4), ty + (s(18) - F.cap_height(T.f_label)) // 2, "To:", T.f_small, R("blue", 2))
        c.text(cx + s(22), ty + (s(18) - F.cap_height(T.f_label)) // 2, "Riverside Park", T.f_label, INK)
        _loc(c, T, cx + mw - s(18), ty, family)
    else:
        ty = cy
        ty += header(c, T, cx, ty, mw, "Line 4: Central Station - Harbor Terminal", family)
        rh = s(16)
        well(c, T, cx, ty, mw, len(STOPS) * rh + 2 * T.b, family)
        lx = cx + T.b + s(10)
        for i, (nm, eta, cur) in enumerate(STOPS):
            ry = ty + T.b + i * rh
            if cur:
                c.fill(cx + T.b, ry, mw - 2 * T.b, rh, R("blue", 5))
            elif i % 2:
                c.fill(cx + T.b, ry, mw - 2 * T.b, rh, mix(R("blue", 7), R("blue", 6), 0.55))
            d = s(7)
            bevel(c, lx - d // 2, ry + (rh - d) // 2, d, d, "blue", "out", 7 if not cur else 2, T.b, 7, 1)
            c.text(lx + s(10), ry + (rh - F.cap_height(T.f_label)) // 2, nm, T.f_label, WHITE if cur else INK)
            c.text(cx, ry + (rh - F.cap_height(T.f_label)) // 2, eta, T.f_label, WHITE if cur else R("blue", 2), align="right", w=mw - s(6))
        for i in range(len(STOPS)):
            ry = ty + T.b + i * rh
            d = s(7)
            if i < len(STOPS) - 1:
                c.fill(lx - T.b + T.b // 2, ry + (rh + d) // 2, 2 * T.b, rh - d, R("blue", 2))
            bevel(c, lx - d // 2, ry + (rh - d) // 2, d, d, "blue", "out", 7 if not STOPS[i][2] else 2, T.b, 7, 1)
        ty += len(STOPS) * rh + s(8)
        value_row(c, T, cx, ty, mw, "Round trip", "22 min", INK)
        ty += s(13)
        value_row(c, T, cx, ty, mw, "Ticket price", "$2", MONEY_POS)
    return w, h


# ---- 3. chirper feed -------------------------------------------------------------------------------
CHIRPS = [
    ("pnl_milestone", "orange", "@CityHall", "2 min ago", "Congratulations! We have reached Large Town. 2 new map tiles and the Highway road are unlocked.", 142),
    ("st_high_rent", "purple", "@Emma_W", "9 min ago", "The rent on Elm Street is too high. Is anyone at City Hall listening?", 37),
    ("st_no_power", "blue", "@PowerCo", "14 min ago", "Power shortage in Riverside! The transformers are overloaded.", 58),
    ("cat_education", "purple", "@Jack_W", "1 hr ago", "Finally graduated from Westside College. Time to find a real job!", 91),
    ("wx_storm", "teal", "@WeatherDesk", "2 hr ago", "A storm is rolling in. Expect heavy rain tonight.", 12),
    ("tr_bus", "blue", "@Line4Fan", "3 hr ago", "Free transit weekends? Yes please!", 64),
]


@reg("chirper-feed", "Chirper feed", 300, 384, flt=0)
@reg("chirper-filter-open", "Chirper feed: filter dropdown open", 300, 384, flt=1)
def chirper(c, T, x, y, flt=0, family="people"):
    s = T.s
    w, h = s(300), s(384)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Chirper", family)
    ramp = fam(family)[0]
    # header: avatar-bird + filter
    c.blit(ic("pnl_chirper", T.icon), cx, cy)
    c.text(cx + T.icon + s(4), cy + (T.icon - F.cap_height(T.f_value)) // 2, "Filter", T.f_label, INK)
    dx = cx + T.icon + s(40)
    side = T.icon + 2 * T.b
    bw = side * 3 + s(4)
    dd_w = cw - (dx - cx) - bw - s(6)
    dd_y = cy + (T.icon - s(13)) // 2
    for i, n in enumerate(["ui_info", "stat_citizen", "ui_warning"]):
        icon_button(c, T, cx + cw - bw + i * (side + s(2)), cy + (T.icon - side) // 2 + T.b, side, side, ic(n, T.icon), family,
                    "toggled" if i != 1 else "normal", flat=False)
    ty = cy + max(T.icon, side) + s(6)
    fh = ch - (ty - cy)
    well(c, T, cx, ty, cw - s(12), fh, family)
    scrollbar(c, T, cx + cw - s(10), ty, fh, family, 0.0, 0.35)
    iy = ty + T.b
    aw = s(28)
    tw = cw - s(12) - 2 * T.b - aw - s(14)
    fside = T.icon + 2 * T.b
    for i, (n, rc, name, when, msg, likes) in enumerate(CHIRPS):
        lines = wrap(msg, T.f_label, tw)
        lh = s(11)
        ih = s(3) + s(12) + len(lines) * lh + s(2) + fside + s(3)
        if iy + ih > ty + fh - T.b:
            break
        if i % 2:
            c.fill(ty and cx + T.b, iy, cw - s(12) - 2 * T.b, ih, mix(R(ramp, 7), R(ramp, 6), 0.55))
        ax, ay = cx + T.b + s(4), iy + s(4)
        bevel(c, ax, ay, aw, aw, rc, "out", 4, T.b, 7, 1)
        c.blit(ic(n, T.icon), ax + (aw - T.icon) // 2, ay + (aw - T.icon) // 2)
        tx = ax + aw + s(6)
        c.text(tx, iy + s(4), name, T.f_label, R("purple", 2))
        c.text(tx + 1, iy + s(4), name, T.f_label, R("purple", 2))
        c.text(cx, iy + s(5), when, T.f_small, R(ramp, 3), align="right", w=cw - s(12) - s(6))
        yy = iy + s(4) + s(12)
        for ln in lines:
            c.text(tx, yy, ln, T.f_label, INK)
            yy += lh
        yy += s(2)
        c.blit(ic("ui_like", T.icon), tx, yy + T.b)
        c.text(tx + T.icon + s(3), yy + (fside - F.cap_height(T.f_label)) // 2, str(likes), T.f_value, R("red", 3) if likes > 50 else INK)
        icon_button(c, T, cx + cw - s(12) - s(8) - fside, yy, fside, fside, ic("ui_locate", T.icon), family, "normal", flat=False)
        iy += ih
        if i < len(CHIRPS) - 1:
            c.fill(cx + T.b, iy - T.b, cw - s(12) - 2 * T.b, T.b, R(ramp, 4))
    if flt:
        dropdown(c, T, dx, dd_y, dd_w, "Citizens", family, "open", ["All messages", "Citizens", "City services", "Alerts"], 1)
    else:
        dropdown(c, T, dx, dd_y, dd_w, "All messages", family)
    return w, h


# ---- 4. advisor ------------------------------------------------------------------------------------
TUT_STEPS = ["Roads", "Zoning", "Power", "Water", "People", "Services", "Budget", "Info views", "Growth"]


@reg("advisor-tutorial", "Advisor: tutorial step", 360, 180, urgent=False)
@reg("advisor-urgent", "Advisor: urgent message", 360, 180, urgent=True)
def advisor(c, T, x, y, urgent=False, family="people"):
    s = T.s
    w, h = s(360), s(180)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Your advisor", family)
    ramp = fam(family)[0]
    pw = s(72)
    # portrait well: purple backdrop, big advisor icon
    bevel(c, cx, cy, pw, pw + s(14), ramp, "in", 2, T.b, 7, 1)
    inner = Canvas(pw - 2 * T.b, pw - 2 * T.b)
    for r in range(inner.h):
        inner.fill(0, r, inner.w, 1, mix(R(ramp, 4), R(ramp, 2), r / max(1, inner.h - 1)))
    c.blit(inner, cx + T.b, cy + T.b)
    big = s(48)
    c.blit(ic("pnl_advisor", big), cx + (pw - big) // 2, cy + (pw - big) // 2)
    bevel(c, cx + T.b, cy + pw, pw - 2 * T.b, s(14) - T.b, ramp, "out", 3, T.b, 6, 1)
    c.text(cx, cy + pw + s(4), "Ms. Hartley", T.f_small, WHITE, outline=R(ramp, 0), align="center", w=pw)
    rx = cx + pw + s(8)
    rw = cw - pw - s(8)
    ty = cy
    title = "Power shortage in Riverside" if urgent else "Build a power plant"
    c.text(rx, ty, title, T.f_big, R(ramp, 1))
    c.text(rx + 1, ty, title, T.f_big, R(ramp, 1))
    if urgent:
        cw2, _ = chip(c, T, rx + rw - s(58), ty - s(1), "URGENT", "red", icon=None)
    else:
        c.text(rx, ty, "Step 3 of 9", T.f_small, R(ramp, 2), align="right", w=rw)
    ty += s(16)
    bh = s(58)
    well(c, T, rx, ty, rw, bh, family, 7)
    msg = ("Riverside has 14 buildings without power. Build a Wind Turbine or upgrade a Transformer nearby."
           if urgent else
           "Your homes and shops need electricity to grow. Open the Power tab and place a wind turbine, then connect it with power lines.")
    yy = ty + s(4)
    for ln in wrap(msg, T.f_label, rw - s(8))[:4]:
        c.text(rx + s(4), yy, ln, T.f_label, INK)
        yy += s(11)
    ty += bh + s(4)
    if not urgent:
        c.blit(ic("ui_info", 16 if T.u < 1.25 else 24), rx, ty - s(2))
        c.text(rx + T.icon + s(4), ty + s(1), "Hint: press E for networks", T.f_small, MONEY_POS)
        ty += s(16)
        meter(c, T, rx, ty, rw - s(4), 3 / 9.0, family, "yellow", h=s(8))
        # step ticks as dots under the bar
        for i in range(9):
            dxx = rx + int((rw - s(4)) * (i + 0.5) / 9) - s(2)
            c.fill(dxx, ty + s(11), s(4), s(4), R("yellow", 5) if i < 3 else R(ramp, 3))
    else:
        c.blit(ic("st_no_power", T.icon), rx, ty - s(2))
        c.text(rx + T.icon + s(4), ty + s(1), "Riverside: 14 buildings affected", T.f_small, MONEY_NEG)
        ty += s(18)
        c.text(rx, ty, "Supply", T.f_small, INK)
        meter(c, T, rx + s(36), ty - s(1), rw - s(36) - s(70), 0.84, family, "red")
        c.text(rx, ty, "3.6 / 4.3 MW", T.f_small, MONEY_NEG, align="right", w=rw - s(4))
    by = cy + ch - s(18)
    bx = rx
    _loc(c, T, bx, by, family)
    bx += s(22)
    if not urgent:
        button(c, T, bx, by, s(54), s(18), "Previous", family, "normal")
        bx += s(58)
    nb = rx + rw - s(54) - s(4)
    button(c, T, nb - s(62), by, s(58), s(18), "Dismiss", family, "normal")
    button(c, T, nb, by, s(54), s(18), "Next" if not urgent else "Got it", family, "default")
    return w, h


# ---- 5. milestones / development tree / achievements -----------------------------------------------
MS_NAMES = ["Hamlet", "Small Town", "Town", "Large Town", "Small City", "City", "Big City", "Metropolis", "Large Metropolis",
            "Great Metropolis", "Grand Metropolis", "Capital", "Megacity I", "Megacity II", "Megacity III", "Megacity IV",
            "Megacity V", "Megacity VI", "Megacity VII", "Megacity VIII"]
MS_XP = [0, 300, 800, 1800, 3600, 6500, 10500, 16000, 23000, 32000, 45000, 62000, 80000, 100000, 120000, 140000, 155000, 170000,
         183000, 195000]
PROG_TABS = ["pnl_milestone", "pnl_progression", "pnl_achievements"]
PROG_W, PROG_H = 500, 344


def _tick(c, T, x, y, colr):
    from iso_ui_widgets import tick
    tick(c, T, x, y, T.s(8), colr)


def _milestones_page(c, T, cx, cy, cw, ch, family):
    s = T.s
    ramp = fam(family)[0]
    lw = s(222)
    ty = cy
    ty += header(c, T, cx, ty, lw, "Current milestone", family)
    c.blit(ic("pnl_milestone", s(32)), cx, ty)
    c.text(cx + s(38), ty + s(2), "Large Town", T.f_big, R(ramp, 1))
    c.text(cx + s(39), ty + s(2), "Large Town", T.f_big, R(ramp, 1))
    c.text(cx + s(38), ty + s(18), "Population 4,120", T.f_label, INK)
    ty += s(38)
    c.text(cx, ty, "Experience", T.f_label, INK)
    c.text(cx, ty, "2,740 / 3,600 XP", T.f_value, INK, align="right", w=lw)
    ty += s(12)
    meter(c, T, cx, ty, lw, (2740 - 1800) / float(3600 - 1800), family, "yellow", h=s(10))
    ty += s(15)
    c.text(cx, ty, "Next: Small City", T.f_small, R(ramp, 2))
    c.text(cx, ty, "860 XP to go", T.f_small, R(ramp, 2), align="right", w=lw)
    ty += s(14)
    ty += header(c, T, cx, ty, lw, "Reward for Small City", family)
    rows = [("stat_money", "Money", "+$50,000", MONEY_POS), ("stat_dev_point", "Development points", "3", INK),
            ("stat_permit", "Map tiles", "1", INK)]
    for icn, lab, val, col in rows:
        c.blit(ic(icn, T.icon), cx, ty - s(1))
        c.text(cx + T.icon + s(4), ty + (T.icon - F.cap_height(T.f_label)) // 2 - s(1), lab, T.f_label, INK)
        c.text(cx, ty + (T.icon - F.cap_height(T.f_value)) // 2 - s(1), val, T.f_value, col, align="right", w=lw)
        ty += T.icon + s(1)
    ty += s(2)
    c.text(cx, ty, "Unlocks", T.f_label, R(ramp, 1))
    ty += s(13)
    uw = (lw - 2 * s(4)) // 3
    for i, (icn, lab) in enumerate((("road_boulevard", "Boulevard"), ("cat_education", "College"), ("cat_police", "Police HQ"),
                                    ("cat_health", "Hospital"), ("pnl_policies", "Policies"), ("pnl_tiles", "Map tile"))):
        ux = cx + (i % 3) * (uw + s(4))
        uy = ty + (i // 3) * (s(22) + s(4))
        bevel(c, ux, uy, uw, s(22), ramp, "in", 7, T.b, 7, 2)
        c.blit(ic(icn, T.icon), ux + s(2), uy + (s(22) - T.icon) // 2)
        c.text(ux + T.icon + s(4), uy + (s(22) - F.cap_height(T.f_small)) // 2, fit(lab, T.f_small, uw - T.icon - s(8)), T.f_small, INK)
    # right: all 20 milestones
    rx = cx + lw + s(10)
    rw = cw - lw - s(10) - s(12)
    ty = cy
    ty += header(c, T, rx, ty, rw + s(12), "All milestones", family)
    rows = []
    for i, (n, xp) in enumerate(zip(MS_NAMES, MS_XP)):
        rows.append((str(i + 1), n, "{:,}".format(xp), ""))
    cols = [(s(22), "right"), (rw - s(22) - s(70) - s(18), "left"), (s(70), "right"), (s(18), "left")]
    rh = list_rows(c, T, rx, ty, rw, rows, family, hover=None, selected=3, cols=cols)
    scrollbar(c, T, rx + rw + s(2), ty, rh, family, 0.0, 0.8)
    rowh = T.row_h
    for i in range(len(rows)):
        ry = ty + T.b + i * rowh
        ix = rx + T.b + rw - s(18) - T.b * 2 + s(3)
        if i < 3:
            _tick(c, T, ix, ry + s(2), MONEY_POS)
        elif i == 3:
            _tick(c, T, ix, ry + s(2), WHITE)
        else:
            c.blit(ic("ui_lock", s(10)), ix, ry + s(1))


DEV_TREES = [
    ("Roads", "road_street", [("prefab_roundabout", "Roundabout", 1, "u"), ("mode_oneway", "One-way", 1, "u"), ("road_avenue", "Avenue", 2, "u"),
                              ("road_highway", "Highway", 4, "a"), ("road_boulevard", "Boulevard", 8, "l")]),
    ("Electricity", "cat_power", [("cat_power", "Gas plant", 1, "u"), ("info_power", "Battery", 2, "u"), ("cat_power", "Solar", 2, "a"),
                                  ("cat_power", "Hydro dam", 4, "l"), ("cat_power", "Nuclear", 8, "l")]),
    ("Water", "cat_water", [("cat_water", "Treatment", 2, "u"), ("cat_water", "Large pump", 4, "a")]),
    ("Health", "cat_health", [("cat_health", "Hospital", 2, "u"), ("cat_deathcare", "Crematorium", 4, "l")]),
    ("Education", "cat_education", [("cat_education", "High school", 1, "u"), ("cat_education", "College", 4, "a"), ("cat_education", "University", 8, "l")]),
    ("Police", "cat_police", [("cat_police", "Police HQ", 2, "a"), ("cat_police", "Prison", 4, "l")]),
    ("Garbage", "cat_garbage", [("cat_garbage", "Incinerator", 2, "a"), ("cat_garbage", "Recycling", 4, "l")]),
    ("Parks", "cat_parks", [("cat_parks", "Sports", 2, "a")]),
    ("Comms", "cat_comms", [("cat_comms", "Tower", 2, "l"), ("cat_comms", "Sorting", 4, "l")]),
    ("Admin", "cat_admin", [("cat_admin", "Welfare", 2, "a"), ("cat_admin", "City Hall", 8, "l")]),
    ("Transport", "tr_metro", [("tr_metro", "Metro", 4, "l")]),
]


def _node(c, T, x, y, w, h, icn, cost, st, family, sel=False):
    """Development tree node box. st: u = unlocked, a = available, l = locked."""
    ramp = fam(family)[0]
    s = T.s
    if sel:
        c.fill(x - 2 * T.b, y - 2 * T.b, w + 4 * T.b, h + 4 * T.b, R("yellow", 5))
    if st == "u":
        bevel(c, x, y, w, h, "green", "out", 4, T.b, 7, 1)
    elif st == "a":
        bevel(c, x, y, w, h, ramp, "out", 7, T.b, 7, 1)
    else:
        bevel(c, x, y, w, h, "grey", "in", 3, T.b, 5, 1)
    ix = x + (w - T.icon) // 2
    iy = y + s(2)
    if st == "l":
        c.blit(_dim(ic(icn, T.icon), "grey", 0.7), ix, iy)
    else:
        c.blit(ic(icn, T.icon), ix, iy)
    ty = y + h - F.cap_height(T.f_small) - s(3)
    if st == "u":
        c.text(x, ty, "OWNED", T.f_small, WHITE, outline=R("green", 1), align="center", w=w)
    else:
        c.text(x, ty, "%d pt" % cost, T.f_small, INK if st == "a" else R("grey", 6), align="center", w=w)


def _devtree_page(c, T, cx, cy, cw, ch, family):
    s = T.s
    ramp = fam(family)[0]
    ty = cy
    c.blit(ic("stat_dev_point", T.icon), cx, ty - s(1))
    c.text(cx + T.icon + s(4), ty + (T.icon - F.cap_height(T.f_value)) // 2 - s(1), "Development points: 4", T.f_value, R(ramp, 1))
    # legend
    lx = cx + cw
    for lab, st in (("Locked", "l"), ("Available", "a"), ("Owned", "u")):
        tw = F.measure(lab, T.f_small)
        lx -= tw
        c.text(lx, ty + s(3), lab, T.f_small, INK)
        lx -= s(14)
        bevel(
            c, lx, ty + s(1), s(10), s(10), "green" if st == "u" else ramp if st == "a" else "grey", "in" if st == "l" else "out",
            4 if st == "u" else 7 if st == "a" else 3, T.b, 7, 1)
        lx -= s(8)
    ty += T.icon + s(4)
    ncols = 2
    gap = s(10)
    colw = (cw - gap) // 2
    bw, bh = s(28), s(32)
    lab_w = s(54)
    rowh = bh + s(4)
    for i, (tname, ticon, nodes) in enumerate(DEV_TREES):
        col, row = i // 6, i % 6
        gx = cx + col * (colw + gap)
        gy = ty + row * rowh
        bevel(c, gx, gy, colw, rowh - s(1), ramp, "in", mix(R(ramp, 6), R(ramp, 7), 0.5), T.b, 7, 2)
        c.blit(ic(ticon, T.icon), gx + s(3), gy + s(2))
        c.text(gx + s(3), gy + s(2) + T.icon + s(2), fit(tname, T.f_small, lab_w), T.f_small, INK)
        nx = gx + lab_w + s(4)
        step = bw + s(6)
        for k, (icn, nm, cost, st) in enumerate(nodes):
            x0 = nx + k * step
            if k > 0:
                lc = R("green", 4) if (st == "u") else R(ramp, 3) if nodes[k - 1][3] == "u" else R("grey", 3)
                c.fill(x0 - s(6), gy + s(2) + bh // 2, s(6), 2 * T.b, lc)
            _node(c, T, x0, gy + s(2) - (0), bw, bh - s(3), icn, cost, st, family, sel=(tname == "Roads" and k == 3))
    # detail strip
    dy = ty + 6 * rowh + s(4)
    dh = cy + ch - dy
    bevel(c, cx, dy, cw, dh, ramp, "in", 7, T.b, 7, 2)
    c.blit(ic("road_highway", T.icon), cx + s(4), dy + s(4))
    c.text(cx + T.icon + s(10), dy + s(4), "Highway", T.f_value, R(ramp, 1))
    c.text(cx + T.icon + s(10), dy + s(16), "Requires Avenue. Unlocks highways and ramps.", T.f_small, INK)
    bwid = s(96)
    button(c, T, cx + cw - bwid - s(4), dy + (dh - s(18)) // 2, bwid, s(18), "Unlock (4 pt)", family, "default")


ACH = ["First Steps", "Small Town Spirit", "Big City Life", "Metropolis", "Six Figures", "All Smiles", "In The Black", "Balanced Books",
       "Millionaire", "Full Service", "Transit City", "Mass Transit", "Eco City", "Industrial Giant", "Business Hub", "Top Of The Class",
       "Explorer", "Land Baron", "Calling The Shots", "Wide Variety", "City Planner", "Making A Mark", "Tourist Trap",
       "Simply Irresistible", "Welcome One And All", "Scholar", "Small City", "Last Mile Marker"]
ACH_PROG = [1, 1, 1, 0.25, 1, 1, 1, 1, 0.3, 1, 1, 0.55, 0.8, 0.12, 0.4, 0.66, 1, 0.2, 1, 0.5, 1, 1, 0.1, 0.0, 0.0, 0.7, 1, 0.9]


def _ach_page(c, T, cx, cy, cw, ch, family):
    s = T.s
    ramp = fam(family)[0]
    done = sum(1 for p in ACH_PROG if p >= 1)
    ty = cy
    c.blit(ic("pnl_achievements", T.icon), cx, ty - s(1))
    c.text(cx + T.icon + s(4), ty + (T.icon - F.cap_height(T.f_value)) // 2 - s(1), "%d / 28 unlocked" % done, T.f_value, R("yellow", 2))
    c.text(cx, ty + (T.icon - F.cap_height(T.f_value)) // 2 - s(1), "XP earned: 9,600", T.f_value, INK, align="right", w=cw)
    ty += T.icon + s(4)
    cols, rows = 7, 4
    gap = s(4)
    tw = (cw - (cols - 1) * gap) // cols
    th = (cy + ch - ty - (rows - 1) * gap) // rows
    big = s(28)
    for i, nm in enumerate(ACH):
        p = ACH_PROG[i]
        gx = cx + (i % cols) * (tw + gap)
        gy = ty + (i // cols) * (th + gap)
        sel = i == 11
        if sel:
            c.fill(gx - 2 * T.b, gy - 2 * T.b, tw + 4 * T.b, th + 4 * T.b, R("yellow", 5))
        if p >= 1:
            bevel(c, gx, gy, tw, th, ramp, "out", 7, T.b, 7, 1)
        else:
            bevel(c, gx, gy, tw, th, ramp, "in", 6, T.b, 6, 1)
        icon = ic("pnl_achievements", big)
        if p >= 1:
            c.fill(gx + (tw - big) // 2 - s(2), gy + s(3), big + s(4), big + s(2), R("yellow", 4))
            c.blit(icon, gx + (tw - big) // 2, gy + s(4))
        else:
            c.blit(_dim(icon, ramp, 0.65), gx + (tw - big) // 2, gy + s(4))
        lines = wrap(nm, T.f_small, tw - s(4))[:2]
        ly = gy + s(4) + big + s(3)
        for ln in lines:
            c.text(gx, ly, ln, T.f_small, INK if p >= 1 else R(ramp, 1), align="center", w=tw)
            ly += s(10)
        by = gy + th - s(8)
        if p >= 1:
            c.text(gx, by - s(1), "+300 XP", T.f_small, MONEY_POS, align="center", w=tw)
        else:
            meter(c, T, gx + s(4), by, tw - s(8), p, family, "yellow", segmented=False, h=s(5))


@reg("milestones-milestones", "Progression: milestones", PROG_W, PROG_H, tab=0)
@reg("milestones-devtree", "Progression: development tree", PROG_W, PROG_H, tab=1)
@reg("milestones-achievements", "Progression: achievements", PROG_W, PROG_H, tab=2)
def progression(c, T, x, y, tab=0, family="people"):
    s = T.s
    w, h = s(PROG_W), s(PROG_H)
    tabs = [ic(n, T.icon) for n in PROG_TABS]
    title = ["Milestones", "Development tree", "Achievements"][tab]
    cx, cy, cw, ch = window(c, T, x, y, w, h, title, family, tabs, tab)
    [_milestones_page, _devtree_page, _ach_page][tab](c, T, cx, cy, cw, ch, family)
    return w, h


# ---- 6. policies -----------------------------------------------------------------------------------
from iso_ui_kit import MONEY_POS_L, MONEY_NEG_L

# (icon, name, effect, monthly cost (negative = expense), state on|off|lock, slider value or None, description)
CITY_POLICIES = [
    ("tr_bus", "Minimum Fare", "Raises the ticket price floor to $2", 420, "off", None,
     "Transit lines never charge less than $2 per ticket. Raises transit income but may lower ridership."),
    ("stat_expenses", "Import Services", "Buy missing services from neighbours", -900, "off", None,
     "Missing power, water and garbage services are imported from neighbouring cities at a premium."),
    ("stat_health", "Smoking Ban", "Healthier citizens, fewer bar visits", -150, "on", None,
     "Citizens live longer and get sick less often. Leisure commerce loses some customers."),
    ("cat_education", "Education Boost", "More graduates, higher school costs", -1800, "on", 0.4,
     "Raises school funding. More citizens graduate, but every school costs more to run."),
    ("tr_metro", "Free Public Transport", "Transit is free for everyone", -2400, "off", None,
     "All tickets are free. Traffic drops, but line income falls to zero."),
    ("st_air_pollution", "Pollution Management", "Cleaner industry, lower profits", -600, "on", None,
     "Factories filter their exhaust. Air pollution falls, industrial profit drops."),
    ("stat_tourist", "City Promotion", "More tourists arrive each month", -1200, "off", None,
     "Advertising campaign. Increases tourism and commercial income."),
    ("road_highway", "High-Speed Highways", "Higher speed limit on highways", 0, "lock", None,
     "Unlocks at Big City. Raises the speed limit on highways, increasing noise."),
]
DIST_POLICIES = [
    ("info_power", "Energy Saving", "-10% power use, lower comfort", 180, "on", None, "Buildings use 10% less electricity."),
    ("cat_water", "Water Saving", "-15% water use", 140, "on", None, "Buildings use 15% less water and make less sewage."),
    ("cat_garbage", "Recycling", "-20% garbage, extra recycling", -260, "off", None, "Less garbage is produced and sent to landfills."),
    ("ctl_stop", "Speed Bumps", "Safer streets, slower traffic", -90, "off", None, "Fewer accidents but vehicles drive slower."),
    ("addon_parking", "Parking Fee", "Income from parked cars", 320, "on", None, "Parked cars pay a fee. Raises income, lowers happiness."),
    ("cat_zoning", "Small Business", "Boosts small shops, hurts big ones", -120, "off", None, "Encourages small commercial buildings."),
    ("tr_truck", "Heavy Traffic Ban", "No trucks in this district", -200, "off", None, "Trucks avoid the district. Noise falls, goods arrive slower."),
    ("stat_home", "Gated Community", "Less crime, fewer visitors", -240, "off", None, "Lower crime and noise for homes, reduced commerce."),
    ("cat_industry", "Industrial Planning", "Efficient industry, more pollution", -300, "off", None, "Factories produce more and pollute more."),
    ("st_air_pollution", "Combustion Ban", "No fossil fuel heating", -150, "off", None, "Cleaner air. Residents pay more for heating."),
]
POL_W, POL_H = 430, 346


def _pol_cost(v):
    return "free" if v == 0 else money(v, True) + "/mo"


@reg("policies-city", "Policies: city", POL_W, POL_H, tab=0)
@reg("policies-district", "Policies: district", POL_W, POL_H + 52, tab=1)
def policies(c, T, x, y, tab=0, family="people"):
    s = T.s
    w, h = s(POL_W), s(POL_H + (0 if tab == 0 else 52))
    tabs = [ic("pnl_city_info", T.icon), ic("pnl_districts", T.icon)]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Policies: city" if tab == 0 else "Policies: district", family, tabs, tab)
    ramp = fam(family)[0]
    pols = CITY_POLICIES if tab == 0 else DIST_POLICIES
    sel = 2 if tab == 0 else 0
    active = sum(1 for p in pols if p[4] == "on")
    total = sum(p[3] for p in pols if p[4] == "on")
    ty = cy
    hh = s(16)
    if tab == 1:
        c.text(cx, ty + (hh - F.cap_height(T.f_label)) // 2, "District", T.f_label, INK)
        bs = s(14)
        icon_button(c, T, cx + s(46), ty + (hh - bs) // 2, bs, bs, ic("ui_left", s(10)), family, "normal", flat=False)
        dropdown(c, T, cx + s(46) + bs + s(2), ty + (hh - s(13)) // 2, s(130), "Riverside", family)
        icon_button(c, T, cx + s(46) + bs + s(2) + s(130) + s(2), ty + (hh - bs) // 2, bs, bs, ic("ui_right", s(10)), family, "normal", flat=False)
    else:
        c.text(cx, ty + (hh - F.cap_height(T.f_label)) // 2, "Applies to the whole city", T.f_label, INK)
    c.text(cx, ty + (hh - F.cap_height(T.f_label)) // 2, "%d of %d active" % (active, len(pols)), T.f_small, R(ramp, 2), align="right", w=cw)
    ty += hh + s(4)
    lw = cw - s(12)
    rh = s(26)
    n = len(pols)
    well(c, T, cx, ty, lw, n * rh + 2 * T.b, family)
    scrollbar(c, T, cx + lw + s(2), ty, n * rh + 2 * T.b, family, 0.0, 1.0)
    ck = s(10)
    for i, (icn, name, eff, cost, st, sl, desc) in enumerate(pols):
        ry = ty + T.b + i * rh
        lock = st == "lock"
        seld = i == sel
        if seld:
            c.fill(cx + T.b, ry, lw - 2 * T.b, rh, R(ramp, 3))
        elif i % 2:
            c.fill(cx + T.b, ry, lw - 2 * T.b, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        tcol = WHITE if seld else (R(ramp, 4) if lock else INK)
        scol = CREAM if seld else (R(ramp, 4) if lock else R(ramp, 2))
        ico = ic(icn, T.icon)
        c.blit(_dim(ico, ramp, 0.75) if lock else ico, cx + s(4), ry + (rh - T.icon) // 2)
        nx = cx + s(4) + T.icon + s(6)
        c.text(nx, ry + s(4), name, T.f_label, tcol)
        c.text(nx, ry + s(15), fit(eff, T.f_small, s(196)), T.f_small, scol)
        if sl is not None:
            slider(c, T, cx + s(212), ry + (rh - s(12)) // 2, s(70), family, sl, value_text="%d%%" % int(sl * 100))
        bx = cx + lw - T.b - s(8) - ck
        if lock:
            c.blit(ic("ui_lock", s(14)), bx - s(2), ry + (rh - s(14)) // 2)
            c.text(cx, ry + (rh - F.cap_height(T.f_small)) // 2, "Big City", T.f_small, R(ramp, 4), align="right", w=lw - s(34))
        else:
            checkbox(c, T, bx, ry + (rh - ck) // 2, None, family, st == "on")
            if cost != 0:
                good = cost > 0
                col = (MONEY_POS_L if good else MONEY_NEG_L) if seld else (MONEY_POS if good else MONEY_NEG)
            else:
                col = scol
            c.text(cx, ry + (rh - F.cap_height(T.f_value)) // 2, _pol_cost(cost), T.f_value, col, align="right", w=lw - s(28))
    ty += n * rh + 2 * T.b + s(6)
    # description strip for the selected policy
    dh = cy + ch - ty
    bevel(c, cx, ty, cw, dh, ramp, "in", 7, T.b, 7, 2)
    d = pols[sel]
    c.text(cx + s(5), ty + s(4), d[1], T.f_value, R(ramp, 1))
    c.text(cx + s(6), ty + s(4), d[1], T.f_value, R(ramp, 1))
    c.text(cx, ty + s(4), "Monthly total: " + money(total, True), T.f_small, MONEY_POS if total >= 0 else MONEY_NEG, align="right", w=cw - s(6))
    yy = ty + s(16)
    for ln in wrap(d[6], T.f_label, cw - s(12))[:3]:
        c.text(cx + s(5), yy, ln, T.f_label, INK)
        yy += s(12)
    return w, h


# ---- 7. districts ----------------------------------------------------------------------------------
DIST_COLS = ["#e8503a", "#3a9ae8", "#5ac84a", "#e8c030", "#b060d8", "#f08a30", "#30c8b8", "#e86aa8"]
DISTRICTS = [("Downtown", 18420, 3), ("Riverside", 12480, 4), ("Westside", 9120, 1), ("Harbor", 4310, 2), ("Greenfield", 7650, 0),
             ("Old Town", 5980, 2), ("Industrial Park", 1240, 3), ("University Hill", 3870, 1)]
DIST_W, DIST_H = 340, 368


def swatch(c, T, x, y, s_, colr):
    c.fill(x, y, s_, s_, R("grey", 0))
    c.fill(x + T.b, y + T.b, s_ - 2 * T.b, s_ - 2 * T.b, hexc(colr))
    c.fill(x + T.b, y + T.b, s_ - 2 * T.b, T.b, mix(hexc(colr), "#ffffff", 0.45))
    c.fill(x + T.b, y + T.b, T.b, s_ - 2 * T.b, mix(hexc(colr), "#ffffff", 0.45))
    c.fill(x + T.b, y + s_ - 2 * T.b, s_ - 2 * T.b, T.b, mix(hexc(colr), "#000000", 0.35))


@reg("districts-list", "Districts: list", DIST_W, DIST_H, mode="list")
@reg("districts-rename", "Districts: renaming", DIST_W, DIST_H, mode="rename")
@reg("districts-paint", "Districts: painting", DIST_W, DIST_H, mode="paint")
def districts(c, T, x, y, mode="list", family="zoning"):
    s = T.s
    w, h = s(DIST_W), s(DIST_H)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Districts", family)
    ramp = fam(family)[0]
    sel = 1
    ty = cy
    bh = s(20)
    bx = cx
    for lab, icn, wd, st in (("Paint district", "ui_colour", s(106), "toggled" if mode == "paint" else "normal"),
                             ("Erase", "tool_dezone", s(62), "normal"), ("Rename", "ui_edit", s(74), "toggled" if mode == "rename" else "normal")):
        button(c, T, bx, ty, wd, bh, lab, family, st, icon=ic(icn, T.icon))
        bx += wd + s(4)
    icon_button(c, T, cx + cw - bh, ty, bh, bh, ic("ui_trash", T.icon), family, "normal", flat=False)
    ty += bh + s(6)
    # list header + rows (custom: swatch, editable name, population, policies count)
    rh = s(20)
    lw = cw - s(12)
    hh = s(13)
    lh = hh + len(DISTRICTS) * rh + 2 * T.b
    well(c, T, cx, ty, lw, lh, family)
    scrollbar(c, T, cx + lw + s(2), ty, lh, family, 0.0, 0.9)
    bevel(c, cx + T.b, ty + T.b, lw - 2 * T.b, hh, ramp, "out", 6, T.b, 7, 2)
    popw, polw = s(66), s(40)
    namex = cx + T.b + s(26)
    for lab, hx, al, wd in (("Name", namex, "left", s(100)), ("Population", cx + lw - popw - polw - T.b, "right", popw - s(2)),
                            ("Policies", cx + lw - polw - T.b, "right", polw - s(4))):
        c.text(hx, ty + T.b + (hh - F.cap_height(T.f_small)) // 2, lab, T.f_small, INK, align=al, w=wd)
    ry0 = ty + T.b + hh
    for i, (nm, pop, pc) in enumerate(DISTRICTS):
        ry = ry0 + i * rh
        seld = i == sel
        if seld:
            c.fill(cx + T.b, ry, lw - 2 * T.b, rh, R(ramp, 3))
        elif i % 2:
            c.fill(cx + T.b, ry, lw - 2 * T.b, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        sw = s(12)
        swatch(c, T, cx + T.b + s(6), ry + (rh - sw) // 2, sw, DIST_COLS[i])
        ty_text = ry + (rh - F.cap_height(T.f_label)) // 2
        if seld and mode == "rename":
            tiw = s(120)
            textinput(c, T, namex - s(2), ry + (rh - s(14)) // 2, tiw, "Riverside Heights", family, "focused")
        else:
            c.text(namex, ty_text, nm, T.f_label, WHITE if seld else INK)
        c.text(cx + lw - popw - polw - T.b, ty_text, "{:,}".format(pop), T.f_label, WHITE if seld else INK, align="right", w=popw - s(2))
        pic = ic("pnl_policies", s(12))
        c.blit(pic, cx + lw - polw - T.b + s(4), ry + (rh - pic.h) // 2)
        c.text(cx + lw - polw - T.b, ty_text, str(pc), T.f_label, WHITE if seld else INK, align="right", w=polw - s(4))
    ty += lh + s(8)
    ty += header(c, T, cx, ty, cw, "Riverside Heights" if mode == "rename" else "Riverside", family)
    for icn, lab, val, col in (("stat_population", "Population", "12,480", INK), ("stat_households", "Households", "4,610", INK),
                               ("stat_jobs", "Jobs", "3,920", INK), ("stat_land_value", "Land value", "$1,240", MONEY_POS)):
        c.blit(ic(icn, s(12)), cx, ty - s(1))
        c.text(cx + s(16), ty, lab, T.f_label, INK)
        c.text(cx, ty, val, T.f_value, col, align="right", w=cw)
        ty += s(13)
    c.blit(ic("stat_happiness", s(12)), cx, ty - s(1))
    c.text(cx + s(16), ty, "Happiness", T.f_label, INK)
    meter(c, T, cx + s(90), ty - s(1), cw - s(90) - s(40), 0.74, family, "green")
    c.text(cx, ty, "74%", T.f_value, INK, align="right", w=cw)
    ty += s(15)
    # colour palette + hint
    c.text(cx, ty + s(1), "Colour", T.f_label, INK)
    px = cx + s(46)
    for i, col in enumerate(DIST_COLS):
        if i == sel:
            c.fill(px - 2 * T.b, ty - 2 * T.b, s(14) + 4 * T.b, s(14) + 4 * T.b, R("yellow", 5))
        swatch(c, T, px, ty, s(14), col)
        px += s(18)
    ty += s(20)
    hint = {"list": "Select a district, then Paint to add cells.", "rename": "Type a new name, Enter to confirm, Esc to cancel.",
            "paint": "Drag a rectangle on the map. Right-click to erase."}[mode]
    bevel(c, cx, ty, cw, cy + ch - ty, ramp, "in", 7, T.b, 7, 2)
    c.blit(ic("ui_info", T.icon), cx + s(3), ty + (cy + ch - ty - T.icon) // 2)
    c.text(cx + T.icon + s(8), ty + (cy + ch - ty - F.cap_height(T.f_small)) // 2, fit(hint, T.f_small, cw - T.icon - s(14)), T.f_small, INK)
    return w, h


# --- END PART 2 ---
