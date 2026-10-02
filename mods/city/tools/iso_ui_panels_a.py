"""
iso_ui_panels_a - exemplar panels: build menu (with iso thumbnails) and the building inspector
(RCT2 ride-window style: tabs overview / occupants / finances / upkeep / efficiency).

Panel convention (all iso_ui_panels_*.py): def panel_x(c, T, x, y, **opts) -> (w, h) draws the window
with its top-left at (x, y) in canvas pixels, sizes from T.s(); PANELS list registers standalone renders.
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import Theme, window, button, icon_button, bevel, fam, etched_h
from iso_ui_widgets import well, checkbox, slider, list_rows, scrollbar, dropdown
from iso_ui_widgets2 import meter, graph, value_row, groupbox, money
from iso_ui_parts import thumb, card, header, chip
from iso_ui_icons import ic
import iso_ui_font as F

PANELS = []  # (file stem, title, fn, w_hint, h_hint, kwargs)


def reg(stem, title, w, h, **kw):
    def deco(fn):
        PANELS.append((stem, title, fn, w, h, kw))
        return fn
    return deco


BUILD_ITEMS = {
    "health": [("clinic", "Clinic", 4800, "ok"), ("hospital", "Hospital", 22000, "sel"), ("cemetery", "Medevac", 9500, "ok"),
               ("hospital", "Research Hosp.", 60000, "lock")],
    "deathcare": [("cemetery", "Cemetery", 6000, "ok"), ("factory", "Crematorium", 14000, "ok")],
}


@reg("build-menu-health", "Build menu: Health & Deathcare", 330, 260)
def build_menu(c, T, x, y, tab=0, family="services"):
    s = T.s
    w, h = s(330), s(236)
    tabs = [ic("cat_health", T.icon), ic("cat_deathcare", T.icon), ic("cat_police", T.icon), ic("cat_fire", T.icon)]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Health & Deathcare", family, tabs, tab)
    th = 64 if T.u < 1.25 else 96
    items = BUILD_ITEMS["health"] + BUILD_ITEMS["deathcare"][:0]
    ix = cx
    for i, (k, name, cost, st) in enumerate(items):
        state = {"ok": "hover" if i == 0 else "normal", "sel": "selected", "lock": "locked"}[st]
        cwid, chh = card(c, T, ix, cy, thumb(k, th), name, money(cost), family, state)
        ix += cwid + s(4)
    # detail strip for the selected item (RCT2 construction window footer)
    dy = cy + chh + s(6)
    header(c, T, cx, dy, cw, "Hospital", family)
    dy += s(14)
    rows = [("stat_money", "Cost", money(22000)), ("stat_expenses", "Upkeep", "$640 / month"),
            ("stat_health", "Patients", "160"), ("stat_jobs", "Workers", "48"), ("info_health", "Coverage", "4 tiles")]
    for i, (icn, lab, val) in enumerate(rows):
        col = i % 2
        rx = cx + col * (cw // 2 + s(2))
        ry = dy + (i // 2) * s(14)
        value_row(c, T, rx, ry, cw // 2 - s(6), lab, val, MONEY_NEG if lab == "Upkeep" else INK, icon=ic(icn, 16 if T.u < 1.25 else 24))
    by = y + h - s(24)
    checkbox(c, T, cx, by + s(3), "Show coverage", family, True)
    button(c, T, cx + cw - s(80), by, s(80), s(16), "Place", family, "default")
    return w, h


INSPECT_TABS = ["ui_eye", "stat_population", "stat_money", "stat_fee", "ui_chart_line"]


@reg("inspector-overview", "Building inspector: overview", 300, 250, tab=0)
@reg("inspector-occupants", "Building inspector: occupants", 300, 250, tab=1)
@reg("inspector-finances", "Building inspector: finances", 300, 250, tab=2)
@reg("inspector-upkeep", "Building inspector: upkeep", 300, 250, tab=3)
@reg("inspector-efficiency", "Building inspector: efficiency graph", 300, 250, tab=4)
def inspector(c, T, x, y, tab=0, family="services", viewport=None):
    s = T.s
    w, h = s(300), s(250)
    tabs = [ic(n, T.icon) for n in INSPECT_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "St. Mary Hospital", family, tabs, tab)
    side = s(26)
    bx = cx + cw - side + T.b
    for i, n in enumerate(["ui_locate", "ui_follow", "tool_upgrade", "tool_move", "tool_bulldoze"]):
        icon_button(c, T, bx, cy + i * (side + s(2)), side, side, ic(n, T.icon), family, "normal", flat=False)
    mw = cw - side - s(4)
    if tab == 0:
        vh = s(96)
        bevel(c, cx, cy, mw, vh, fam(family)[0], "in", 1, T.b, 7, 1)
        if viewport is not None:
            vp = viewport.crop(0, 0, min(viewport.w, mw - 2 * T.b), min(viewport.h, vh - 2 * T.b))
            c.blit(vp, cx + T.b, cy + T.b)
        else:
            th = thumb("hospital", vh - 2 * T.b)
            c.blit(th, cx + (mw - th.w) // 2, cy + T.b)
        ty = cy + vh + s(6)
        c.text(cx, ty, "Status:", T.f_label, INK)
        c.text(cx + s(44), ty, "Operating normally", T.f_label, MONEY_POS)
        ty += s(14)
        c.text(cx, ty, "Patients", T.f_label, INK)
        meter(c, T, cx + s(60), ty - s(1), mw - s(110), 0.72, family, "green")
        c.text(cx, ty, "115 / 160", T.f_value, INK, align="right", w=mw)
        ty += s(14)
        c.text(cx, ty, "Workers", T.f_label, INK)
        meter(c, T, cx + s(60), ty - s(1), mw - s(110), 0.9, family, "blue")
        c.text(cx, ty, "43 / 48", T.f_value, INK, align="right", w=mw)
        ty += s(14)
        c.text(cx, ty, "Efficiency", T.f_label, INK)
        meter(c, T, cx + s(60), ty - s(1), mw - s(110), 0.86, family, "yellow")
        c.text(cx, ty, "86%", T.f_value, INK, align="right", w=mw)
        ty += s(16)
        ix = cx
        for n in ("st_traffic", "st_no_power"):
            cw2, _ = chip(c, T, ix, ty, "Traffic" if n == "st_traffic" else "Low power", "orange", icon=ic(n, 16 if T.u < 1.25 else 24))
            ix += cw2 + s(4)
    elif tab == 1:
        rows = [("Ann Walker", "Patient", "3 d"), ("Tom Reyes", "Doctor", "Well ed."), ("Mia Chen", "Patient", "1 d"),
                ("Leo Novak", "Nurse", "Educated"), ("Ava Brooks", "Patient", "6 d"), ("Sam Okafor", "Patient", "2 d"),
                ("Ivy Laine", "Doctor", "Highly ed."), ("Ben Ford", "Patient", "4 d"), ("Zoe Hart", "Cleaner", "Basic"),
                ("Max Grey", "Patient", "5 d"), ("Eli Moss", "Patient", "1 d")]
        rh = list_rows(c, T, cx, cy, mw - s(12), rows[:12], family, hover=2, selected=4,
                       cols=[(s(100), "left"), (s(60), "left"), (mw - s(12) - s(160), "right")], header=("Name", "Role", "Stay/Edu"))
        from iso_ui_widgets import scrollbar as sb
        sb(c, T, cx + mw - s(11), cy, rh, family, 0.0, 0.5)
    elif tab == 2:
        ty = cy
        ty += header(c, T, cx, ty, mw, "This month", family)
        for lab, v in (("Service fees", 1240), ("Upkeep", -640), ("Wages", -920), ("City subsidy", 600)):
            value_row(c, T, cx, ty, mw, lab, money(v, True), MONEY_POS if v > 0 else MONEY_NEG)
            ty += s(13)
        etched_h(c, T, cx, ty, mw, fam(family)[0]); ty += s(5)
        value_row(c, T, cx, ty, mw, "Net", money(280, True), MONEY_POS, face=T.f_value)
        ty += s(18)
        graph(c, T, cx, ty, mw, cy + ch - ty, family, [([3, 4, 3, 5, 6, 5, 7, 6, 8, 7, 9, 8], hexc("#7cf06c")), ([5, 5, 6, 5, 6, 6, 6, 7, 6, 7, 7, 7], hexc("#ff6a50"))],
              ymax=10, ylabels=["$2k", "$1k", "$0"], fill_first=False)
    elif tab == 3:
        ty = cy
        ty += header(c, T, cx, ty, mw, "Budget", family)
        c.text(cx, ty, "Service budget", T.f_label, INK)
        slider(c, T, cx + s(90), ty - s(3), mw - s(140), family, 0.6, value_text="110%")
        ty += s(18)
        c.text(cx, ty, "Upkeep", T.f_label, INK); c.text(cx, ty, "$704 / month", T.f_value, MONEY_NEG, align="right", w=mw)
        ty += s(18)
        ty += header(c, T, cx, ty, mw, "Upgrades", family)
        for i, (n, lab, cost, on) in enumerate((("cat_health", "Helipad", "$8,000", True), ("stat_health", "Extra ward", "$12,000", False),
                                                ("info_power", "Solar roof", "$5,500", False))):
            bevel(c, cx, ty, mw, s(22), fam(family)[0], "in" if on else "out", 7 if on else 6, T.b, 7, 2)
            c.blit(ic(n, T.icon), cx + s(3), ty + (s(22) - T.icon) // 2)
            c.text(cx + T.icon + s(8), ty + s(7), lab, T.f_label, INK)
            c.text(cx, ty + s(7), "Built" if on else cost, T.f_value, MONEY_POS if on else INK, align="right", w=mw - s(6))
            ty += s(25)
    else:
        ty = cy
        ty += header(c, T, cx, ty, mw, "Efficiency (12 months)", family)
        graph(c, T, cx, ty, mw, s(110), family, [([62, 70, 74, 71, 80, 84, 83, 86, 88, 85, 86, 86], hexc("#f6e080"))], ymax=100,
              ylabels=["100%", "50%", "0%"], xlabels=["Jan", "Apr", "Jul", "Dec"], fill_first=True)
        ty += s(118)
        for lab, v, good in (("Staffing", "90%", True), ("Electricity", "-6%", False), ("Budget", "+10%", True), ("Road access", "OK", True)):
            value_row(c, T, cx, ty, mw, lab, v, MONEY_POS if good else MONEY_NEG)
            ty += s(12)
    return w, h
