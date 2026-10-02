"""
iso_ui_panels_transit - RCT2-style windows: transit lines (list + line detail), info views panel and
legends, notifications centre, map tiles, road inspector, and the tool option palettes
(road / zoning / terraform).

Panel convention (see iso_ui_panels_a): def panel_x(c, T, x, y, **opts) -> (w, h); PANELS registers renders.
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG, MONEY_POS_L, MONEY_NEG_L
from iso_ui_chrome import Theme, window, button, icon_button, bevel, fam, etched_h, etched_v
from iso_ui_widgets import well, checkbox, radio, slider, list_rows, scrollbar, dropdown, textinput, arrow
from iso_ui_widgets2 import meter, graph, value_row, groupbox, money
from iso_ui_parts import thumb, card, header, chip
from iso_ui_icons import ic
import iso_ui_icon_dsl as D
import iso_ui_font as F

PANELS = []  # (file stem, title, fn, w_hint, h_hint, kwargs)


def reg(stem, title, w, h, **kw):
    def deco(fn):
        PANELS.append((stem, title, fn, w, h, kw))
        return fn
    return deco


# ---- shared helpers --------------------------------------------------------------------------------
TIER = {"minimal": "#9aa0a6", "info": "#6bb8f0", "problem": "#f2d04c", "warning": "#f28c30",
        "major": "#e54b3c", "error": "#a82020", "fatal": "#301010", "good": "#5cd67a"}


def shade(colr, t):
    """t < 0 darkens, t > 0 lightens."""
    colr = hexc(colr) if isinstance(colr, str) else colr
    return mix(colr, (0, 0, 0, 255), -t) if t < 0 else mix(colr, (255, 255, 255, 255), t)


def trunc(text, face, maxw):
    if F.measure(text, face) <= maxw:
        return text
    while text and F.measure(text + "..", face) > maxw:
        text = text[:-1]
    return text + ".."


def tbsize(T):
    return 24 if T.u < 1.25 else 36


def line_chip(c, T, x, y, w, h, colr, text, face=None):
    """Coloured transit line number chip."""
    colr = hexc(colr) if isinstance(colr, str) else colr
    face = face or T.f_small
    c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, shade(colr, -0.7))
    c.fill(x, y, w, h, colr)
    for i in range(T.b):
        c.fill(x + i, y + i, w - 2 * i, 1, shade(colr, 0.5))
        c.fill(x + i, y + i, 1, h - 2 * i, shade(colr, 0.5))
        c.fill(x + i, y + h - 1 - i, w - 2 * i, 1, shade(colr, -0.4))
        c.fill(x + w - 1 - i, y + i, 1, h - 2 * i, shade(colr, -0.4))
    c.text(x, y + (h - F.cap_height(face)) // 2, text, face, WHITE, outline=shade(colr, -0.7), align="center", w=w)


def swatch(c, T, x, y, w, h, colr, frame=True):
    colr = hexc(colr) if isinstance(colr, str) else colr
    if frame:
        c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, SHADOW)
    c.fill(x, y, w, h, colr)
    c.fill(x, y, w, T.b, shade(colr, 0.45))
    c.fill(x, y, T.b, h, shade(colr, 0.45))
    c.fill(x, y + h - T.b, w, T.b, shade(colr, -0.35))
    c.fill(x + w - T.b, y, T.b, h, shade(colr, -0.35))


def sel_frame(c, T, x, y, w, h, colr=None):
    c.rect(x - T.s(2), y - T.s(2), w + T.s(4), h + T.s(4), colr or R("yellow", 6), T.b)


def led(c, T, x, y, on):
    s = T.s(8)
    bevel(c, x, y, s, s, "grey", "in", 1, T.b, 6, 0)
    if on:
        c.fill(x + T.b, y + T.b, s - 2 * T.b, s - 2 * T.b, R("green", 5))
        c.fill(x + T.b, y + T.b, s - 2 * T.b, T.b, R("green", 7))
    else:
        c.fill(x + T.b, y + T.b, s - 2 * T.b, s - 2 * T.b, R("red", 2))


def small_btn(c, T, x, y, s, icon_name, family, state="normal"):
    icon_button(c, T, x, y, s, s, ic(icon_name, T.s(12)), family, state, flat=False)


def vtext(c, x, y, s, face, colr, down=True):
    """Rotated (reading top-to-bottom) pixel text, top-left at (x, y). Returns (w, h) of rotated block."""
    import numpy as np
    m, asc = F.render_mask(s, face)
    m = np.rot90(m, -1 if down else 1)
    c.mask(m, x, y, colr)
    return m.shape[1], m.shape[0]


def pct_colr(v):
    return MONEY_POS if v >= 0.66 else (hexc("#a8780c") if v >= 0.4 else MONEY_NEG)


# ================================================================================================
# 1. TRANSIT LINES
# ================================================================================================
LINE_PALETTE = ["#e54b3c", "#f28c30", "#f2c94c", "#8ed04a", "#2fa84f", "#2fb5a8", "#4aa8f0", "#3a64d8",
                "#7a4ad8", "#c64ab8", "#8a5a38", "#5a6068"]

MODE_TABS = [("tr_bus", "Bus"), ("tr_tram", "Tram"), ("tr_metro", "Metro"), ("tr_train", "Train"), ("tr_taxi", "Taxi")]

# (number, colour, name, stops, vehicles, passengers/month, active)
LINES = {
    0: [("1", 0, "Harbor - Central Station", 14, 6, 8420, True), ("3", 3, "Maple Hill Loop", 11, 4, 5110, True),
        ("7", 1, "Airport Express", 6, 5, 6980, True), ("12", 6, "Old Town - University", 16, 7, 9350, True),
        ("15", 8, "Riverside Shuttle", 9, 3, 2240, False), ("22", 4, "Factory Row - Eastgate", 13, 5, 4870, True),
        ("24", 2, "Night Owl", 18, 2, 960, True), ("31", 5, "Lakeview - Market Sq.", 10, 4, 3180, True),
        ("40", 9, "Hospital Ring", 8, 3, 1720, True), ("44", 10, "Stadium Special", 5, 2, 880, False)],
    1: [("T1", 1, "Market Sq. - Old Town", 12, 5, 7640, True), ("T2", 4, "Gardens Line", 9, 4, 4120, True),
        ("T3", 7, "Waterfront Tram", 14, 6, 6310, True), ("T4", 8, "University Loop", 8, 3, 2790, False)],
    2: [("M1", 0, "Northgate - Central", 11, 8, 21480, True), ("M2", 6, "Harbor - Airport", 9, 6, 15620, True),
        ("M3", 3, "Eastside Orbital", 13, 7, 11050, True)],
    3: [("R1", 7, "Central - Lakeview Intercity", 6, 4, 12900, True), ("R2", 2, "Coast Regional", 5, 3, 7430, True)],
}
TAXI_ROWS = [("Taxi stand", "Central Station", 3, 8, 2140, True), ("Taxi stand", "Harbor Plaza", 2, 5, 1380, True),
             ("Taxi stand", "Maple Hill Mall", 2, 4, 1190, True), ("Taxi stand", "University", 1, 3, 820, True),
             ("Taxi stand", "Airport T1", 4, 9, 3260, True), ("Taxi stand", "Stadium", 1, 2, 410, False)]


def _list_header(c, T, x, y, w, cols, labels, family):
    ramp, body, _ = fam(family)
    rh = T.row_h
    bevel(c, x + T.b, y, w - 2 * T.b, rh, ramp, "out", body, T.b, 7, 2)
    cx = x + T.b
    for (cw, al), lab in zip(cols, labels):
        c.text(cx + T.s(3), y + (rh - F.cap_height(T.f_small)) // 2, lab, T.f_small, INK, align=al, w=cw - T.s(6))
        cx += cw


@reg("transit-lines-bus", "Transit lines: bus", 380, 272, mode=0)
@reg("transit-lines-tram", "Transit lines: tram", 380, 272, mode=1)
@reg("transit-lines-metro", "Transit lines: metro", 380, 272, mode=2)
@reg("transit-lines-train", "Transit lines: train", 380, 272, mode=3)
@reg("transit-lines-taxi", "Transit lines: taxi", 380, 272, mode=4)
def transit_lines(c, T, x, y, mode=0, family="transit"):
    s = T.s
    w, h = s(380), s(272)
    tabs = [ic(n, T.icon) for n, _ in MODE_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Transit lines - " + MODE_TABS[mode][1], family, tabs, mode)
    ramp, body, acc = fam(family)
    sbw = s(10)
    lw = cw - sbw - s(2)
    taxi = mode == 4
    rh = s(17)
    if taxi:
        cols = [(s(24), "left"), (lw - s(24) - s(40) - s(36) - s(56) - s(26) - 2 * T.b, "left"),
                (s(40), "right"), (s(36), "right"), (s(56), "right"), (s(26), "center")]
        labels = ("", "Taxi stand", "Stands", "Cabs", "Rides/mo", "On")
    else:
        cols = [(s(32), "left"), (lw - s(32) - s(34) - s(34) - s(56) - s(28) - 2 * T.b, "left"),
                (s(34), "right"), (s(34), "right"), (s(56), "right"), (s(28), "center")]
        labels = ("Line", "Name", "Stops", "Veh.", "Pass./mo", "On")
    rows = TAXI_ROWS if taxi else LINES[mode]
    nvis = 8 if not taxi else 6
    listh = T.row_h + nvis * rh + 2 * T.b
    well(c, T, cx, cy, lw, listh, family)
    _list_header(c, T, cx, cy + T.b, lw, cols, labels, family)
    ry = cy + T.b + T.row_h
    sel = 3 if mode == 0 else 0
    for i in range(nvis):
        if i < len(rows):
            r = rows[i]
            fillc = None
            if i % 2 == 1:
                fillc = mix(R(ramp, 7), R(ramp, 6), 0.55)
            if i == 1:
                fillc = R(ramp, 5)
            if i == sel:
                fillc = R(ramp, 3)
            if fillc is not None:
                c.fill(cx + T.b, ry, lw - 2 * T.b, rh, fillc)
            tc = WHITE if i == sel else INK
            xx = cx + T.b
            if taxi:
                tcol = LINE_PALETTE[(i * 3 + 1) % 12]
                num, name, stops, veh, pax, on = r
                swatch(c, T, xx + s(6), ry + (rh - s(10)) // 2, s(10), s(10), tcol)
                name = name
            else:
                num, ci, name, stops, veh, pax, on = r
                line_chip(c, T, xx + s(3), ry + s(2), cols[0][0] - s(6), rh - s(4), LINE_PALETTE[ci], num)
            xx += cols[0][0]
            c.text(xx + s(3), ry + (rh - F.cap_height(T.f_label)) // 2, trunc(name, T.f_label, cols[1][0] - s(6)), T.f_label, tc)
            xx += cols[1][0]
            for k, v in enumerate((stops, veh, "{:,}".format(pax))):
                c.text(xx + s(3), ry + (rh - F.cap_height(T.f_label)) // 2, str(v), T.f_label, tc,
                       align="right", w=cols[2 + k][0] - s(6))
                xx += cols[2 + k][0]
            led(c, T, xx + (cols[5][0] - s(8)) // 2, ry + (rh - s(8)) // 2, on)
        ry += rh
    scrollbar(c, T, cx + lw + s(2), cy, listh, family, 0.0, min(1.0, nvis / max(nvis, len(rows) + 3)))
    # footer: totals and actions
    fy = cy + listh + s(6)
    tot_l = len(rows)
    tot_v = sum(r[3] if taxi else r[4] for r in rows)
    tot_p = sum(r[4] if taxi else r[5] for r in rows)
    ic_s = T.icon
    cwid = cw // 3
    items = [("tr_line" if not taxi else "tr_taxi_stand", "{} {}".format(tot_l, "stands" if taxi else "lines")),
             ("stat_vehicles", "{} vehicles".format(tot_v)),
             ("stat_citizen", "{:,} / month".format(tot_p))]
    for i, (n, t) in enumerate(items):
        c.blit(ic(n, ic_s), cx + i * cwid, fy - (ic_s - F.cap_height(T.f_label)) // 2)
        c.text(cx + i * cwid + ic_s + s(3), fy, t, T.f_label, INK)
    fy += ic_s + s(2)
    etched_h(c, T, cx, fy, cw, ramp)
    fy += s(5)
    c.text(cx, fy, "Income", T.f_label, INK)
    inc = tot_p * (0.6 if mode < 2 else 0.9)
    c.text(cx + s(40), fy, money(int(inc), True), T.f_value, MONEY_POS)
    c.text(cx + s(110), fy, "Upkeep", T.f_label, INK)
    c.text(cx + s(152), fy, money(-int(tot_v * (180 if taxi else 740)), True), T.f_value, MONEY_NEG)
    by = y + h - s(24)
    bw = s(78)
    button(c, T, cx + cw - bw, by, bw, s(16), "Delete", family, "normal", icon=ic("ui_trash", 16 if T.u < 1.25 else 24)
           if False else None)
    button(c, T, cx + cw - 2 * bw - s(4), by, bw, s(16), "Edit stand" if taxi else "Edit line", family, "default")
    button(c, T, cx + cw - 3 * bw - s(8), by, bw, s(16), "New line" if not taxi else "New stand", family, "normal")
    checkbox(c, T, cx, by + s(3), "Show on map", family, True)
    return w, h


# ---- line detail -----------------------------------------------------------------------------------
STOPS = ["Harbor Terminal", "Fish Market", "Dock Street", "Old Town Hall", "Cathedral Sq.", "Central Station",
         "Park Avenue", "City Museum", "University", "Maple Hill", "Stadium", "Mill Road", "Garden City", "Eastgate"]
WAITING = [42, 12, 8, 55, 31, 96, 22, 18, 64, 27, 9, 14, 38, 71]
VEHICLE_POS = [0.45, 2.55, 4.4, 6.55, 9.45, 11.55]  # fractional stop index along the line


def stop_strip(c, T, x, y, w, family, colr, first=0, count=8, vehicles=VEHICLE_POS, icon_name="tr_bus", sel=5):
    """RCT2 ride-station style: horizontal line, stop dots, staggered names + waiting counts, vehicles between."""
    s = T.s
    ramp = fam(family)[0]
    lh = T.line(T.f_small) + s(1)
    block = 2 * lh
    ico = T.icon
    h = block * 2 + s(10) + ico + s(8)
    well(c, T, x, y, w, h, family, shade=7)
    inner = w - 2 * T.b
    pad = s(14)
    step = (w - 2 * pad) // max(1, count - 1)
    ly = y + T.b + block + s(5) + ico // 2
    colr = hexc(colr) if isinstance(colr, str) else colr
    lt = s(4)
    # the line
    c.fill(x + T.b, ly - lt // 2 - T.b, pad - s(6), lt + 2 * T.b, SHADOW)
    c.fill(x + pad - s(10), ly - lt // 2 - T.b, (count - 1) * step + s(20), lt + 2 * T.b, SHADOW)
    c.fill(x + pad - s(10), ly - lt // 2, (count - 1) * step + s(20), lt, colr)
    c.fill(x + pad - s(10), ly - lt // 2, (count - 1) * step + s(20), T.b, shade(colr, 0.4))
    # continuation arrows at the clipped ends
    if first > 0:
        arrow(c, x + T.b + s(3), ly, s(4), "left", INK)
    if first + count < len(STOPS):
        arrow(c, x + w - T.b - s(8), ly, s(4), "right", INK)
    for i in range(count):
        si = first + i
        if si >= len(STOPS):
            break
        px = x + pad + i * step
        term = si in (0, len(STOPS) - 1)
        ds = s(10) if term else s(8)
        c.fill(px - ds // 2 - T.b, ly - ds // 2 - T.b, ds + 2 * T.b, ds + 2 * T.b, SHADOW)
        c.fill(px - ds // 2, ly - ds // 2, ds, ds, WHITE)
        if term:
            c.fill(px - ds // 2 + T.b * 2, ly - ds // 2 + T.b * 2, ds - 4 * T.b, ds - 4 * T.b, colr)
        if si == sel:
            sel_frame(c, T, px - ds // 2 - T.b, ly - ds // 2 - T.b, ds + 2 * T.b, ds + 2 * T.b)
        up = i % 2 == 0
        name = trunc(STOPS[si], T.f_small, step * 2 - s(6))
        wt = WAITING[si]
        wc = MONEY_NEG if wt > 60 else (hexc("#9a6a08") if wt > 30 else MONEY_POS)
        wtxt = "{} waiting".format(wt)

        def lab2(ty_, txt, colr_):
            tw_ = F.measure(txt, T.f_small)
            lx_ = max(x + T.b + s(2), min(px - tw_ // 2, x + w - T.b - s(2) - tw_))
            c.text(lx_, ty_, txt, T.f_small, colr_)
        if up:
            ty = y + T.b + s(3)
            lab2(ty, name, INK)
            lab2(ty + lh, wtxt, wc)
            c.fill(px, ty + 2 * lh, T.b, ly - ds // 2 - ty - 2 * lh - s(1), R(ramp, 3))
        else:
            ty = ly + ico // 2 + s(6)
            c.fill(px, ly + ds // 2 + s(1), T.b, ty - ly - ds // 2 - s(1), R(ramp, 3))
            lab2(ty, name, INK)
            lab2(ty + lh, wtxt, wc)
    # vehicles between stops
    vi = ic(icon_name, ico)
    for v in vehicles:
        if first + 0.2 <= v <= first + count - 1.2:
            vx = x + pad + int((v - first) * step) - ico // 2
            c.fill(vx - T.b, ly - ico // 2 - T.b, ico + 2 * T.b, ico + 2 * T.b, shade(colr, -0.5))
            c.fill(vx, ly - ico // 2, ico, ico, shade(colr, 0.75))
            c.blit(vi, vx, ly - ico // 2)
    return h


DETAIL_TABS = ["tr_line", "tr_bus", "ui_chart_line"]


@reg("transit-line-route", "Line detail: route", 380, 300, tab=0)
@reg("transit-line-route-metro", "Line detail: route (metro)", 380, 300, tab=0, mode="metro")
@reg("transit-line-vehicles", "Line detail: vehicles", 380, 300, tab=1)
@reg("transit-line-passengers", "Line detail: passengers", 380, 300, tab=2)
def transit_line_detail(c, T, x, y, tab=0, mode="bus", family="transit"):
    s = T.s
    w, h = s(380), s(300)
    metro = mode == "metro"
    vname = "tr_metro" if metro else "tr_bus"
    tabs = [ic("tr_line", T.icon), ic(vname, T.icon), ic("ui_chart_line", T.icon)]
    colr = LINE_PALETTE[0 if not metro else 6]
    num = "12" if not metro else "M2"
    title = ("Bus line 12" if not metro else "Metro line M2")
    cx, cy, cw, ch = window(c, T, x, y, w, h, title, family, tabs, tab)
    ramp, body, acc = fam(family)
    lab = T.f_label
    cap = F.cap_height(lab)
    if tab == 0:
        ty = cy
        # name + colour
        chw, chh = s(34), s(18)
        line_chip(c, T, cx + cw - chw - s(2), ty + s(1), chw, chh, colr, num, T.f_label)
        c.text(cx, ty + (s(14) - cap) // 2, "Name", lab, INK)
        textinput(c, T, cx + s(40), ty, cw - s(40) - chw - s(10), "Old Town - University" if not metro else "Harbor - Airport", family, "focused")
        ty += s(21)
        c.text(cx, ty + (s(12) - cap) // 2, "Colour", lab, INK)
        sw = s(13)
        for i, pc in enumerate(LINE_PALETTE):
            sx = cx + s(40) + i * (sw + s(3))
            swatch(c, T, sx, ty, sw, s(12), pc)
            if pc == colr:
                sel_frame(c, T, sx, ty, sw, s(12))
        ty += s(19)
        ty += header(c, T, cx, ty, cw, "Route: 14 stops, 11.6 km", family)
        sh = stop_strip(c, T, cx, ty, cw, family, colr, 0 if not metro else 3, 8, VEHICLE_POS[:], vname, sel=5)
        ty += sh + s(5)
        # vehicles +/-
        bs = s(16)
        c.text(cx, ty + (bs - cap) // 2, "Vehicles", lab, INK)
        small_btn(c, T, cx + s(60), ty, bs, "ui_minus", family)
        c.text(cx + s(60) + bs, ty + (bs - cap) // 2, "6", T.f_value, INK, align="center", w=s(22))
        small_btn(c, T, cx + s(82) + bs, ty, bs, "ui_plus", family)
        c.text(cx + s(112) + bs, ty + (bs - cap) // 2, "of 12 depot slots", T.f_small, R(ramp, 2))
        c.text(cx, ty + (bs - cap) // 2, "Interval 2:40 min", T.f_small, R(ramp, 2), align="right", w=cw)
        ty += bs + s(5)
        c.text(cx, ty + s(1), "Ticket price", lab, INK)
        slider(c, T, cx + s(60), ty - s(1), cw - s(130), family, 0.4, value_text="$2.40")
        ty += s(17)
        etched_h(c, T, cx, ty, cw, ramp)
        ty += s(5)
        for i, (l, v, cc) in enumerate((("Passengers", "9,350 / mo", INK), ("Income", "+$5,610", MONEY_POS), ("Upkeep", "-$5,180", MONEY_NEG))):
            col = cw // 3
            c.text(cx + i * col, ty, l, T.f_small, R(ramp, 2))
            c.text(cx + i * col, ty + T.line(T.f_small), v, T.f_value, cc)
        by = y + h - s(24)
        checkbox(c, T, cx, by + s(3), "Line active", family, True)
        bw = s(76)
        button(c, T, cx + cw - bw, by, bw, s(16), "Delete line", family, "normal")
        button(c, T, cx + cw - 2 * bw - s(4), by, bw, s(16), "Locate", family, "normal", icon=ic("ui_locate", s(12) if T.u < 1.25 else T.icon))
    elif tab == 1:
        ty = cy
        bs = s(16)
        c.text(cx, ty + (bs - cap) // 2, "Vehicles", lab, INK)
        small_btn(c, T, cx + s(60), ty, bs, "ui_minus", family)
        c.text(cx + s(60) + bs, ty + (bs - cap) // 2, "6", T.f_value, INK, align="center", w=s(22))
        small_btn(c, T, cx + s(82) + bs, ty, bs, "ui_plus", family)
        c.text(cx, ty + (bs - cap) // 2, "Average load 71%", T.f_small, R(ramp, 2), align="right", w=cw)
        ty += bs + s(5)
        vrows = [("Bus 1204", "Dock Street", "38 / 60", ("Moving", MONEY_POS)), ("Bus 1205", "Old Town Hall", "52 / 60", ("Boarding", hexc("#9a6a08"))),
                 ("Bus 1206", "Park Avenue", "21 / 60", ("Moving", MONEY_POS)), ("Bus 1207", "University", "60 / 60", ("Full", MONEY_NEG)),
                 ("Bus 1208", "Garden City", "17 / 60", ("Moving", MONEY_POS)), ("Bus 1209", "Eastgate", "9 / 60", ("Terminus", INK)),
                 ("Bus 1210", "Central Station", "44 / 60", ("Boarding", hexc("#9a6a08"))), ("Bus 1211", "Fish Market", "27 / 60", ("Moving", MONEY_POS)),
                 ("Bus 1212", "Dock Street", "31 / 60", ("Moving", MONEY_POS)), ("Bus 1213", "Stadium", "12 / 60", ("Moving", MONEY_POS)),
                 ("Bus 1214", "Mill Road", "40 / 60", ("Boarding", hexc("#9a6a08")))]
        if metro:
            vrows = [(r[0].replace("Bus", "Train"), r[1], r[2].replace("60", "240"), r[3]) for r in vrows]
        sbw = s(10)
        lw = cw - sbw - s(2)
        rh = list_rows(c, T, cx, ty, lw, vrows, family, hover=1, selected=3,
                       cols=[(s(70), "left"), (lw - s(70) - s(56) - s(66), "left"), (s(56), "right"), (s(66), "left")],
                       header=("Vehicle", "Next stop", "Load", "Status"))
        scrollbar(c, T, cx + lw + s(2), ty, rh, family, 0.0, 0.7)
        ty += rh + s(6)
        c.text(cx, ty + s(1), "Budget", lab, INK)
        slider(c, T, cx + s(60), ty - s(1), cw - s(130), family, 0.55, value_text="105%")
        ty += s(17)
        by = y + h - s(24)
        button(c, T, cx + cw - s(100), by, s(100), s(16), "Follow vehicle", family, "normal", icon=ic("ui_follow", s(12) if T.u < 1.25 else T.icon))
        button(c, T, cx + cw - s(176), by, s(72), s(16), "Locate", family, "normal")
    else:
        ty = cy
        ty += header(c, T, cx, ty, cw, "Passengers per month", family)
        gh = s(118)
        graph(c, T, cx, ty, cw, gh, family,
              [([5800, 6300, 6900, 7100, 7600, 8100, 8300, 8800, 8600, 9100, 9350, 9350], hexc("#f6e080")),
               ([4800, 4800, 5400, 5400, 5400, 6000, 6000, 6000, 6600, 6600, 6600, 7200], hexc("#6ab4ff"))],
              ymax=10000, ylabels=["10k", "5k", "0"], xlabels=["Nov", "Feb", "May", "Aug"], fill_first=True)
        ty += gh + s(4)
        ix = cx
        for colr2, t in ((hexc("#f6e080"), "Riders"), (hexc("#6ab4ff"), "Capacity")):
            c.fill(ix, ty + s(1), s(8), s(6), colr2)
            c.text(ix + s(11), ty, t, T.f_small, INK)
            ix += s(60)
        ty += s(12)
        for l, v, f, rr in (("Load factor", "78%", 0.78, "green"), ("On time", "91%", 0.91, "blue")):
            c.text(cx, ty, l, lab, INK)
            meter(c, T, cx + s(70), ty - s(1), cw - s(120), f, family, rr)
            c.text(cx, ty, v, T.f_value, INK, align="right", w=cw)
            ty += s(13)
        ty += s(2)
        ty += header(c, T, cx, ty, cw, "Busiest stops", family)
        for nme, f, v in (("Central Station", 1.0, "2,140"), ("University", 0.71, "1,520"), ("Old Town Hall", 0.55, "1,180")):
            c.text(cx, ty, nme, lab, INK)
            meter(c, T, cx + s(100), ty - s(1), cw - s(160), f, family, "yellow", segmented=False)
            c.text(cx, ty, v, T.f_value, INK, align="right", w=cw)
            ty += s(13)
    return w, h


# ================================================================================================
# 2. INFO VIEWS PANEL + LEGENDS
# ================================================================================================
INFO_GROUPS = [
    ("Services", [("info_power", "Power"), ("info_water", "Water"), ("info_garbage", "Garbage"), ("info_health", "Health"),
                  ("info_deathcare", "Deathcare"), ("info_education", "Education"), ("info_police", "Police"),
                  ("info_crime", "Crime"), ("info_fire", "Fire"), ("info_parks", "Parks"), ("info_telecom", "Telecom"),
                  ("info_post", "Post")]),
    ("Networks", [("info_powergrid", "Power grid"), ("info_watergrid", "Water grid"), ("info_sewage", "Sewage"),
                  ("info_roads", "Roads"), ("info_traffic", "Traffic"), ("info_transit", "Transit"),
                  ("info_freight", "Freight")]),
    ("Environment", [("info_pollution", "Pollution"), ("info_air", "Air"), ("info_ground", "Ground"), ("info_noise", "Noise"),
                     ("info_groundwater", "Groundwater"), ("info_resources", "Resources"), ("info_landvalue", "Land value")]),
    ("City", [("info_happiness", "Happiness"), ("info_level", "Level"), ("info_attainment", "Attainment"),
              ("info_wealth", "Wealth"), ("info_age", "Age"), ("info_production", "Production"),
              ("info_tourism", "Tourism"), ("info_districts", "Districts")]),
]

HEAT_RAMPS = {
    "good": ["#d8382c", "#e8742c", "#f2c94c", "#a8d84a", "#3eb858"],
    "bad": ["#3eb858", "#a8d84a", "#f2c94c", "#e8742c", "#d8382c"],
    "pollution": ["#e8f0d8", "#f2d070", "#f29a34", "#c86a24", "#7a4018"],
    "blue": ["#d8ecfa", "#8ec8f0", "#4a96dc", "#2a5cb4", "#12328a"],
    "green": ["#183a20", "#2a6a30", "#48a040", "#88d058", "#d4f890"],
    "value": ["#2a50b0", "#3a9ac8", "#58c880", "#e8d84a", "#e86a2c", "#c82828"],
}


def ramp_col(stops, t):
    t = max(0.0, min(1.0, t)) * (len(stops) - 1)
    i = min(int(t), len(stops) - 2)
    return mix(stops[i], stops[i + 1], t - i)


def heat_strip(c, T, x, y, w, h, ramp, n=11):
    stops = HEAT_RAMPS[ramp]
    bevel(c, x, y, w, h, "grey", "in", 1, T.b, 6, 0)
    iw = w - 2 * T.b
    for i in range(n):
        x0 = x + T.b + iw * i // n
        x1 = x + T.b + iw * (i + 1) // n
        c.fill(x0, y + T.b, x1 - x0, h - 2 * T.b, ramp_col(stops, i / (n - 1)))
        if i:
            c.fill(x0, y + T.b, T.b, h - 2 * T.b, shade(ramp_col(stops, i / (n - 1)), -0.25))


def tile_button(c, T, x, y, w, h, label, icon, family, state="normal"):
    ramp, body, _ = fam(family)
    fills = {"normal": body, "hover": min(7, body + 1), "toggled": 3}
    bevel(c, x, y, w, h, ramp, "in" if state == "toggled" else "out", fills[state], T.b, 7, 1)
    off = T.b if state == "toggled" else 0
    ix = x + T.s(3) + off
    c.blit(icon, ix, y + (h - icon.h) // 2 + off)
    tx = ix + icon.w + T.s(3)
    ty = y + (h - F.cap_height(T.f_label)) // 2 + off
    if state == "toggled":
        c.text(tx, ty, label, T.f_label, WHITE, outline=R(ramp, 0))
    else:
        c.text(tx, ty, trunc(label, T.f_label, x + w - tx - T.s(2)), T.f_label, INK)


SUMMARY = {
    "info_landvalue": ("value", "Low", "High", [("Average land value", "$142", INK), ("Highest", "Old Town $480", MONEY_POS),
                                                ("Lowest", "Eastgate $38", MONEY_NEG)]),
    "info_traffic": ("bad", "Free flow", "Jammed", [("Average flow", "62%", INK), ("Worst road", "Harbor Rd 21%", MONEY_NEG),
                                                    ("Vehicles on road", "3,418", INK)]),
}


@reg("infoviews-landvalue", "Info views panel: land value", 404, 330, active="info_landvalue")
@reg("infoviews-traffic", "Info views panel: traffic", 404, 330, active="info_traffic")
def infoviews_panel(c, T, x, y, active="info_landvalue", family="info"):
    s = T.s
    w = s(404)
    gap = s(3)
    bh = s(19) if T.u < 1.25 else s(20)
    cw_ = w - 2 * s(4)
    colw = (cw_ - 3 * gap) // 4
    # compute height: groups
    hdr = F.cap_height(T.f_label) + s(6)
    gh = sum(hdr + ((len(g[1]) + 3) // 4) * (bh + gap) + s(2) for g in INFO_GROUPS)
    foot = s(58) + (s(44) if active == "info_traffic" else 0)
    h = T.b + T.title_h + s(4) + gh + foot + s(8)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Info views", family)
    ramp, body, acc = fam(family)
    ty = cy
    hover = "info_crime" if active != "info_traffic" else "info_roads"
    for gname, views in INFO_GROUPS:
        ty += header(c, T, cx, ty, cw, gname, family)
        for i, (n, lab) in enumerate(views):
            bx = cx + (i % 4) * (colw + gap)
            by = ty + (i // 4) * (bh + gap)
            st = "toggled" if n == active else ("hover" if n == hover else "normal")
            tile_button(c, T, bx, by, colw, bh, lab, ic(n, T.icon), family, st)
        ty += ((len(views) + 3) // 4) * (bh + gap) + s(2)
    # footer: legend ramp + summary + Off
    etched_h(c, T, cx, ty, cw, ramp)
    ty += s(5)
    rampname, lo, hi, rows = SUMMARY[active]
    lw = cw - s(66)
    heat_strip(c, T, cx, ty, lw, s(11), rampname)
    c.text(cx, ty + s(14), lo, T.f_small, INK)
    c.text(cx, ty + s(14), hi, T.f_small, INK, align="right", w=lw)
    button(c, T, cx + cw - s(60), ty - s(1), s(60), s(16), "Off", family, "normal", icon=ic("ui_eye_off", s(12) if T.u < 1.25 else T.icon))
    ty += s(26)
    for l, v, cc in rows:
        value_row(c, T, cx, ty, cw - s(66), l, v, cc)
        ty += s(11)
    if active == "info_traffic":
        ty += s(3)
        radio(c, T, cx, ty, "Flow", family, True)
        radio(c, T, cx + s(60), ty, "Volume", family, False)
        graph(c, T, cx + s(150), ty - s(3), cw - s(150), s(30), family,
              [([22, 18, 14, 16, 30, 58, 74, 60, 48, 52, 55, 60, 62, 58, 56, 64, 76, 84, 66, 50, 38, 30, 26, 22], hexc("#f6e080"))],
              ymax=100, fill_first=True, grid=2)
    return w, h


def legend_window(c, T, x, y, title, h, body_fn, family="info", icon=None):
    s = T.s
    w = s(200)
    cx, cy, cw, ch = window(c, T, x, y, w, s(h), title, family, None, 0, grip=False)
    body_fn(c, T, cx, cy, cw, ch, family)
    return w, s(h)


def _desc(c, T, x, y, txt, family):
    c.text(x, y, txt, T.f_small, R(fam(family)[0], 2))
    return T.line(T.f_small) + T.s(3)


def _heat_body(ramp, lo, hi, mid, desc, rows, v_lo, v_hi):
    def fn(c, T, cx, cy, cw, ch, family):
        s = T.s
        ty = cy + _desc(c, T, cx, cy, desc, family)
        heat_strip(c, T, cx, ty, cw, s(13), ramp)
        ty += s(13)
        for i in range(0, 11, 2):
            c.fill(cx + T.b + (cw - 2 * T.b) * i // 11 + (cw - 2 * T.b) // 22, ty, T.b, s(3), R("teal", 2))
        ty += s(5)
        c.text(cx, ty, lo, T.f_small, INK)
        c.text(cx, ty, mid, T.f_small, INK, align="center", w=cw)
        c.text(cx, ty, hi, T.f_small, INK, align="right", w=cw)
        ty += T.line(T.f_small) + s(1)
        c.text(cx, ty, v_lo, T.f_small, R("teal", 2))
        c.text(cx, ty, v_hi, T.f_small, R("teal", 2), align="right", w=cw)
        ty += T.line(T.f_small) + s(3)
        etched_h(c, T, cx, ty, cw, fam(family)[0])
        ty += s(6)
        for l, v, cc in rows:
            value_row(c, T, cx, ty, cw, l, v, cc)
            ty += s(12)
    return fn


@reg("legend-landvalue", "Legend: land value (heat ramp)", 200, 132)
def legend_landvalue(c, T, x, y):
    return legend_window(c, T, x, y, "Land value", 132,
                         _heat_body("value", "Low", "High", "Medium", "Average value per tile",
                                    [("City average", "$142", INK), ("Highest", "Old Town", MONEY_POS), ("Lowest", "Eastgate", MONEY_NEG)],
                                    "$0", "$480"))


@reg("legend-pollution", "Legend: pollution (heat ramp)", 200, 132)
def legend_pollution(c, T, x, y):
    return legend_window(c, T, x, y, "Pollution", 132,
                         _heat_body("pollution", "None", "Heavy", "Moderate", "Air + ground + noise",
                                    [("City average", "18%", INK), ("Worst", "Factory Row", MONEY_NEG), ("Clean tiles", "64%", MONEY_POS)],
                                    "0", "100"))


@reg("legend-happiness", "Legend: happiness (good/bad ramp)", 200, 126)
def legend_happiness(c, T, x, y):
    def body(c, T, cx, cy, cw, ch, family):
        s = T.s
        ty = cy + _desc(c, T, cx, cy, "Citizen happiness per building", family)
        ico = T.icon
        sw = cw - 2 * (ico + s(3))
        c.blit(ic("stat_unhappy", ico), cx, ty - s(2))
        heat_strip(c, T, cx + ico + s(3), ty + (ico - s(13)) // 2 - s(2), sw, s(13), "good")
        c.blit(ic("stat_happiness", ico), cx + cw - ico, ty - s(2))
        ty += ico + s(2)
        c.text(cx, ty, "Unhappy", T.f_small, MONEY_NEG)
        c.text(cx, ty, "Happy", T.f_small, MONEY_POS, align="right", w=cw)
        ty += T.line(T.f_small) + s(4)
        etched_h(c, T, cx, ty, cw, fam(family)[0])
        ty += s(6)
        c.text(cx, ty, "City average", T.f_label, INK)
        meter(c, T, cx + s(70), ty - s(1), cw - s(100), 0.74, family, "green")
        c.text(cx, ty, "74%", T.f_value, INK, align="right", w=cw)
        ty += s(14)
        value_row(c, T, cx, ty, cw, "Happy citizens", "61%", MONEY_POS)
        ty += s(12)
        value_row(c, T, cx, ty, cw, "Unhappy citizens", "9%", MONEY_NEG)
    return legend_window(c, T, x, y, "Happiness", 128, body)


ZONE_LEG = [("Res. low", "#7ed957"), ("Res. high", "#3ea83a"), ("Row houses", "#5cc8a0"), ("Res. medium", "#58b848"),
            ("Mixed use", "#b8d84a"), ("Low rent", "#a8c878"), ("Com. low", "#5ab4f0"), ("Com. high", "#2a78c8"),
            ("Industrial", "#f2c94c"), ("Office", "#b07ce8"), ("Office high", "#7a4cc0"), ("Warehouse", "#d8943a")]


@reg("legend-zones", "Legend: zones (category)", 200, 138)
def legend_zones(c, T, x, y):
    def body(c, T, cx, cy, cw, ch, family):
        s = T.s
        ty = cy + _desc(c, T, cx, cy, "Zoned districts", family)
        colw = cw // 2
        rh = s(13)
        for i, (nm, col_) in enumerate(ZONE_LEG):
            bx = cx + (i // 6) * colw
            by = ty + (i % 6) * rh
            swatch(c, T, bx + T.b, by, s(11), s(9), col_)
            c.text(bx + s(16), by + (s(9) - F.cap_height(T.f_label)) // 2, nm, T.f_label, INK)
        ty += 6 * rh + s(2)
        etched_h(c, T, cx, ty, cw, fam(family)[0])
        ty += s(6)
        value_row(c, T, cx, ty, cw, "Zoned tiles", "1,284", INK)
    return legend_window(c, T, x, y, "Zones", 140, body)


@reg("legend-fire", "Legend: fire coverage", 200, 126)
def legend_fire(c, T, x, y):
    def body(c, T, cx, cy, cw, ch, family):
        s = T.s
        ty = cy + _desc(c, T, cx, cy, "Fire station coverage", family)
        rows = [("Covered", "#58c46a", 412), ("Partly covered", "#f2c94c", 96), ("Not covered", "#e54b3c", 38)]
        for nm, col_, n in rows:
            swatch(c, T, cx + T.b, ty, s(14), s(10), col_)
            c.text(cx + s(20), ty + (s(10) - F.cap_height(T.f_label)) // 2, nm, T.f_label, INK)
            c.text(cx, ty + (s(10) - F.cap_height(T.f_label)) // 2, "{} bldg.".format(n), T.f_value, INK, align="right", w=cw)
            ty += s(14)
        ty += s(2)
        etched_h(c, T, cx, ty, cw, fam(family)[0])
        ty += s(6)
        c.text(cx, ty, "Coverage", T.f_label, INK)
        meter(c, T, cx + s(56), ty - s(1), cw - s(90), 0.75, family, "green")
        c.text(cx, ty, "75%", T.f_value, INK, align="right", w=cw)
        ty += s(14)
        value_row(c, T, cx, ty, cw, "Fire stations", "4", INK, icon=ic("cat_fire", T.icon))
    return legend_window(c, T, x, y, "Fire coverage", 128, body)


NAT = [("nat_fertile", "Fertile land", "#8ed04a", "62%"), ("nat_forest", "Forest", "#2f7a3a", "48%"), ("nat_ore", "Ore", "#a0a8b8", "81%"),
       ("nat_oil", "Oil", "#4a2c5a", "23%"), ("nat_stone", "Stone", "#d0c090", "70%"), ("nat_fish", "Fish", "#4aa8f0", "55%")]


@reg("legend-resources", "Legend: natural resources", 200, 150)
def legend_resources(c, T, x, y):
    def body(c, T, cx, cy, cw, ch, family):
        s = T.s
        ty = cy + _desc(c, T, cx, cy, "Deposits remaining", family)
        rh = T.icon + s(3)
        for n, nm, col_, v in NAT:
            c.blit(ic(n, T.icon), cx, ty)
            swatch(c, T, cx + T.icon + s(3), ty + (T.icon - s(10)) // 2, s(8), s(10), col_)
            c.text(cx + T.icon + s(15), ty + (T.icon - F.cap_height(T.f_label)) // 2, nm, T.f_label, INK)
            c.text(cx, ty + (T.icon - F.cap_height(T.f_value)) // 2, v, T.f_value, INK, align="right", w=cw)
            ty += rh
    return legend_window(c, T, x, y, "Resources", 40 + 6 * (19 if True else 0), body)


# ================================================================================================
# 3. NOTIFICATIONS CENTRE
# ================================================================================================
NOTIF = {
    "all": [("major", "st_fire", "Fire at Pier 9 Warehouse", "Harbor District", "1 min", True),
            ("warning", "st_no_power", "No electricity in 14 buildings", "Maple Hill", "6 min", True),
            ("good", "st_leveled_up", "Old Town Hall leveled up to 3", "Old Town", "14 min", True),
            ("warning", "st_traffic", "Heavy traffic on Harbor Road", "Harbor District", "21 min", False),
            ("info", "st_unhappy", "Ann W.: Bus 12 is late again!", "Riverside", "26 min", False),
            ("problem", "st_no_water", "No water supply: 6 buildings", "Eastgate", "42 min", False),
            ("major", "st_wildfire", "Wildfire spreading near Pine Ridge", "Pine Ridge", "1 h", False),
            ("problem", "st_garbage", "Garbage piling up", "Factory Row", "2 h", False),
            ("warning", "st_flooded", "Flood warning for Riverside", "Riverside", "3 h", False)],
    "problems": [("warning", "st_no_power", "No electricity in 14 buildings", "Maple Hill", "6 min", True),
                 ("warning", "st_traffic", "Heavy traffic on Harbor Road", "Harbor District", "21 min", True),
                 ("problem", "st_no_water", "No water supply: 6 buildings", "Eastgate", "42 min", False),
                 ("problem", "st_garbage", "Garbage piling up", "Factory Row", "2 h", False),
                 ("info", "st_no_workers", "Not enough workers: Mill Road", "Mill Road", "3 h", False),
                 ("warning", "st_crime", "Crime rate rising", "Old Town", "5 h", False),
                 ("problem", "st_no_road", "No road access: 3 buildings", "Garden City", "1 d", False),
                 ("info", "st_high_rent", "High rent: citizens moving out", "Maple Hill", "1 d", False)],
    "events": [("major", "st_fire", "Fire at Pier 9 Warehouse", "Harbor District", "1 min", True),
               ("good", "st_leveled_up", "Old Town Hall leveled up to 3", "Old Town", "14 min", True),
               ("major", "st_wildfire", "Wildfire spreading near Pine Ridge", "Pine Ridge", "1 h", False),
               ("warning", "st_flooded", "Flood warning for Riverside", "Riverside", "3 h", False),
               ("warning", "st_accident", "Accident blocks Central Bridge", "Central Station", "4 h", False),
               ("good", "st_leveled_up", "Milestone: Small City reached", "City Hall", "1 d", False),
               ("good", "st_leveled_up", "New map tile available", "Map", "2 d", False)],
    "chirps": [("info", "st_unhappy", "Ann W.: Bus 12 is late again!", "Riverside", "26 min", True),
               ("info", "st_noise", "Tom R.: Airport noise is unbearable", "Eastgate", "1 h", False),
               ("minimal", "st_leveled_up", "Mia C.: Love the new park!", "Old Town", "2 h", False),
               ("info", "st_high_rent", "Leo N.: Rent went up again...", "Maple Hill", "3 h", False),
               ("minimal", "st_no_service", "Ava B.: No clinic nearby", "Garden City", "5 h", False),
               ("info", "st_garbage", "Sam O.: Bins are overflowing", "Factory Row", "6 h", False),
               ("minimal", "st_leveled_up", "Ivy L.: Graduated today!", "University", "1 d", False)],
}
NOTIF_TABS = ["pnl_notifications", "ui_warning", "pnl_milestone", "pnl_chirper"]
NOTIF_KEYS = ["all", "problems", "events", "chirps"]


@reg("notifications-all", "Notifications centre: all", 390, 300, tab=0)
@reg("notifications-problems", "Notifications centre: problems", 390, 300, tab=1)
@reg("notifications-events", "Notifications centre: city events", 390, 300, tab=2)
@reg("notifications-chirps", "Notifications centre: chirps", 390, 300, tab=3)
def notifications(c, T, x, y, tab=0, family="city"):
    s = T.s
    w, h = s(390), s(300)
    tabs = [ic(n, T.icon) for n in NOTIF_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Notifications", family, tabs, tab)
    ramp, body, acc = fam(family)
    lab = T.f_label
    # filter bar
    dropdown(c, T, cx, cy, s(112), "Severity: All" if tab != 1 else "Severity: Problem+", family)
    checkbox(c, T, cx + s(122), cy + s(2), "Hide resolved", family, False)
    c.text(cx, cy + (s(13) - F.cap_height(T.f_small)) // 2, "{} unread".format(sum(1 for r in NOTIF[NOTIF_KEYS[tab]] if r[5])),
           T.f_small, R(ramp, 2), align="right", w=cw)
    ty = cy + s(18)
    rows = NOTIF[NOTIF_KEYS[tab]]
    rh = s(27)
    nvis = 7
    sbw = s(10)
    lw = cw - sbw - s(2)
    lh_ = nvis * rh + 2 * T.b
    well(c, T, cx, ty, lw, lh_, family)
    ry = ty + T.b
    sel = 1 if tab != 3 else None
    for i in range(nvis):
        if i < len(rows):
            tier, icn, msg, place, ago, unread = rows[i]
            fillc = None
            if i % 2 == 1:
                fillc = mix(R(ramp, 7), R(ramp, 6), 0.55)
            if i == sel:
                fillc = R(ramp, 5)
            if fillc is not None:
                c.fill(cx + T.b, ry, lw - 2 * T.b, rh, fillc)
            tcol = hexc(TIER[tier])
            c.fill(cx + T.b, ry, s(4), rh, tcol)
            c.fill(cx + T.b + s(4), ry, T.b, rh, shade(tcol, -0.5))
            ix = cx + T.b + s(8)
            c.blit(ic(icn, T.icon), ix, ry + (rh - T.icon) // 2)
            tx = ix + T.icon + s(5)
            bw = s(20)
            tw_ = s(46)
            maxw = lw - (tx - cx) - bw - tw_ - s(10)
            c.text(tx, ry + s(4), trunc(msg, lab, maxw), lab, INK)
            if unread:
                c.text(tx + 1, ry + s(4), trunc(msg, lab, maxw), lab, INK)
            c.text(tx, ry + s(4) + T.line(lab) + s(1) - (1 if T.u < 1.25 else 0), trunc(place, T.f_small, maxw), T.f_small, R(ramp, 2))
            c.text(cx + lw - T.b - bw - tw_ - s(4), ry + (rh - F.cap_height(T.f_small)) // 2, ago, T.f_small, R(ramp, 2),
                   align="right", w=tw_)
            icon_button(c, T, cx + lw - T.b - bw - s(2), ry + (rh - s(18)) // 2, bw, s(18), ic("ui_locate", T.icon), family, "normal", flat=False)
            if unread:
                c.fill(ix - s(4) + 0, ry + s(2), s(3), s(3), R("blue", 4))
        ry += rh
    scrollbar(c, T, cx + lw + s(2), ty, lh_, family, 0.0, min(1.0, nvis / max(nvis, len(rows) + 4)))
    by = y + h - s(24)
    c.text(cx, by + (s(16) - F.cap_height(T.f_small)) // 2, "{} messages".format(len(rows) * 3), T.f_small, R(ramp, 2))
    bw = s(84)
    button(c, T, cx + cw - bw, by, bw, s(16), "Clear all", family, "normal", icon=None)
    button(c, T, cx + cw - 2 * bw - s(4), by, bw, s(16), "Mark all read", family, "normal")
    checkbox(c, T, cx + s(62), by + s(3), "Sound", family, True)
    return w, h


# ================================================================================================
# 4. MAP TILES
# ================================================================================================
TERRAIN = {"grass": "#58a032", "forest": "#2f7a3a", "water": "#4a8ed0", "ore": "#8a8a90", "field": "#a8b84a"}


def _tile_terrain(r, cc):
    v = (r * 7 + cc * 13 + (r * cc) % 5) % 9
    return "water" if v == 0 else "forest" if v in (1, 2) else "ore" if v == 3 else "field" if v == 4 else "grass"


OWNED = {(r, cc) for r in range(3, 6) for cc in range(2, 6)}


def _tile_state(r, cc):
    if (r, cc) in OWNED:
        return "owned"
    for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if (r + dr, cc + dc) in OWNED:
            return "buy"
    return "locked"


@reg("tiles-buy", "Map tiles: purchasable tile", 436, 262, sel=(2, 3))
@reg("tiles-owned", "Map tiles: owned tile", 436, 262, sel=(4, 4))
@reg("tiles-locked", "Map tiles: locked tile", 436, 262, sel=(0, 7))
def map_tiles(c, T, x, y, sel=(2, 3), family="zoning"):
    s = T.s
    w, h = s(436), s(262)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Map tiles", family)
    ramp, body, acc = fam(family)
    lab = T.f_label
    N = 9
    ts = s(20)
    gw = N * ts + 2 * T.b
    bevel(c, cx, cy, gw, gw, ramp, "in", R("grey", 1), T.b, 7, 1)
    for r in range(N):
        for cc in range(N):
            px, py = cx + T.b + cc * ts, cy + T.b + r * ts
            st = _tile_state(r, cc)
            base = hexc(TERRAIN[_tile_terrain(r, cc)])
            if st == "owned":
                c.fill(px, py, ts, ts, base)
                c.rect(px, py, ts, ts, shade(base, -0.45), T.b)
                c.fill(px + T.b, py + T.b, ts - 2 * T.b, T.b, shade(base, 0.35))
                # a few little buildings
                if (r + cc) % 2 == 0:
                    c.fill(px + s(4), py + s(5), s(4), s(4), hexc("#e8dcc0"))
                    c.fill(px + s(4), py + s(4), s(4), T.b, hexc("#c84a3a"))
                if (r * 3 + cc) % 3 == 0:
                    c.fill(px + s(9), py + s(9), s(5), s(4), hexc("#9a9ab0"))
            elif st == "buy":
                c.fill(px, py, ts, ts, mix(base, hexc("#f0e0a0"), 0.55))
                for k in range(0, ts, s(4)):
                    c.fill(px + k, py, s(2), T.b, R("yellow", 5))
                    c.fill(px + k, py + ts - T.b, s(2), T.b, R("yellow", 5))
                    c.fill(px, py + k, T.b, s(2), R("yellow", 5))
                    c.fill(px + ts - T.b, py + k, T.b, s(2), R("yellow", 5))
                c.text(px, py + (ts - F.cap_height(T.f_small)) // 2, "$", T.f_small, R("yellow", 1), align="center", w=ts)
            else:
                dk = mix(base, hexc("#3a3a40"), 0.78)
                c.fill(px, py, ts, ts, dk)
                for k in range(-ts, ts, s(5)):
                    for j in range(ts):
                        xx = k + j
                        if 0 <= xx < ts:
                            c.fill(px + xx, py + j, T.b, T.b, shade(dk, 0.08))
                c.rect(px, py, ts, ts, shade(dk, -0.3), T.b)
    sr, sc_ = sel
    sx, sy = cx + T.b + sc_ * ts, cy + T.b + sr * ts
    c.rect(sx - T.b, sy - T.b, ts + 2 * T.b, ts + 2 * T.b, R("yellow", 6), T.b * 2)
    c.rect(sx - T.b * 2, sy - T.b * 2, ts + 4 * T.b, ts + 4 * T.b, SHADOW, T.b)
    # legend under grid
    ly = cy + gw + s(6)
    lx = cx
    for nm, st in (("Owned", "owned"), ("For sale", "buy"), ("Locked", "locked")):
        bx = lx
        base = hexc("#58a032")
        if st == "owned":
            swatch(c, T, bx, ly, s(10), s(10), base)
        elif st == "buy":
            c.fill(bx, ly, s(10), s(10), mix(base, hexc("#f0e0a0"), 0.55))
            c.rect(bx, ly, s(10), s(10), R("yellow", 5), T.b)
        else:
            c.fill(bx, ly, s(10), s(10), mix(base, hexc("#3a3a40"), 0.78))
            c.rect(bx, ly, s(10), s(10), SHADOW, T.b)
        c.text(bx + s(14), ly + (s(10) - F.cap_height(T.f_small)) // 2, nm, T.f_small, INK)
        lx += s(14) + F.measure(nm, T.f_small) + s(8)
    # details
    dx = cx + gw + s(10)
    dw = cw - gw - s(10)
    st = _tile_state(sr, sc_)
    ty = cy
    col_name = "ABCDEFGHI"[sc_]
    ty += header(c, T, dx, ty, dw, "Tile {}{}".format(col_name, sr + 1), family, T.f_label)
    status = {"owned": ("Owned", MONEY_POS), "buy": ("For sale", hexc("#9a6a08")), "locked": ("Locked", MONEY_NEG)}[st]
    value_row(c, T, dx, ty, dw, "Status", status[0], status[1])
    ty += s(13)
    if st == "buy":
        value_row(c, T, dx, ty, dw, "Cost", money(40000), MONEY_NEG)
    elif st == "owned":
        value_row(c, T, dx, ty, dw, "Value", money(40000), INK)
    else:
        value_row(c, T, dx, ty, dw, "Unlocks at", "Big City", INK, icon=None)
    ty += s(13)
    value_row(c, T, dx, ty, dw, "Upkeep", "$120 / month" if st != "locked" else "-", MONEY_NEG if st != "locked" else INK)
    ty += s(13)
    c.text(dx, ty, "Buildable", lab, INK)
    meter(c, T, dx + s(54), ty - s(1), dw - s(90), 0.82, family, "green")
    c.text(dx, ty, "82%", T.f_value, INK, align="right", w=dw)
    ty += s(15)
    ty += header(c, T, dx, ty, dw, "Resources", family)
    for n, nm, v in (("nat_fertile", "Fertile", 0.6), ("nat_forest", "Forest", 0.2), ("nat_ore", "Ore", 0.0), ("nat_water" if False else "nat_fish", "Fish", 0.35)):
        c.blit(ic(n, T.icon), dx, ty - (T.icon - F.cap_height(lab)) // 2)
        c.text(dx + T.icon + s(3), ty, nm, lab, INK)
        meter(c, T, dx + s(58) + T.icon - s(10), ty - s(1), dw - s(100) - T.icon + s(10), v, family, "yellow", segmented=False)
        c.text(dx, ty, "{}%".format(int(v * 100)), T.f_value, INK, align="right", w=dw)
        ty += max(s(13), T.icon + s(1))
    ty += s(2)
    etched_h(c, T, dx, ty, dw, ramp)
    ty += s(5)
    value_row(c, T, dx, ty, dw, "Tiles owned", "12 / 81", INK, icon=None)
    ty += s(12)
    value_row(c, T, dx, ty, dw, "Permits", "3", MONEY_POS, icon=None)
    ty += s(12)
    value_row(c, T, dx, ty, dw, "Terrain", ["Grass, flat", "Forest, hilly", "Field, flat"][(sel[0] + sel[1]) % 3], INK, icon=None)
    ty += s(12)
    value_row(c, T, dx, ty, dw, "Ground water", "Good", MONEY_POS, icon=None)
    by = y + h - s(24)
    bw = s(96)
    if st == "buy":
        button(c, T, cx + cw - bw, by, bw, s(16), "Buy tile", family, "default")
    else:
        button(c, T, cx + cw - bw, by, bw, s(16), "Buy tile", family, "disabled")
    checkbox(c, T, cx, by + s(3), "Show tile borders", family, True)
    return w, h


# ================================================================================================
# 5. ROAD INSPECTOR
# ================================================================================================
def road_section(c, T, x, y, w, h):
    """Top-down cross-section viewport: grass, sidewalks, lanes with arrows, tree median, a few cars."""
    s = T.s
    bevel(c, x, y, w, h, "brown", "in", hexc("#58a032"), T.b, 7, 1)
    ix, iy, iw, ih = x + T.b, y + T.b, w - 2 * T.b, h - 2 * T.b
    # horizontal road strip made of bands
    bands = [("grass", 0.10), ("walk", 0.08), ("lane", 0.15), ("lane", 0.15), ("median", 0.10), ("lane", 0.15), ("lane", 0.15),
             ("walk", 0.08), ("grass", 0.04)]
    tot = sum(b[1] for b in bands)
    yy = iy
    asph = hexc("#4a4c54")
    li = 0
    for kind, f in bands:
        bh = max(2, int(ih * f / tot))
        if kind == "grass":
            for xx in range(ix, ix + iw, s(9)):
                c.fill(xx, yy, s(9), bh, hexc("#58a032") if (xx // s(9)) % 2 else hexc("#4e9a2c"))
        elif kind == "walk":
            c.fill(ix, yy, iw, bh, hexc("#c8c4b8"))
        elif kind == "median":
            c.fill(ix, yy, iw, bh, hexc("#b8b4a0"))
            for xx in range(ix + s(8), ix + iw - s(8), s(26)):
                c.fill(xx - s(2), yy + bh // 2 - s(2), s(5), s(5), hexc("#2f7a3a"))
                c.fill(xx - s(1), yy + bh // 2 - s(2), s(2), T.b, hexc("#58b84a"))
        else:
            c.fill(ix, yy, iw, bh, asph)
            right = li < 2
            for xx in range(ix + s(14), ix + iw - s(14), s(40)):
                a = s(3)
                arrow(c, xx if right else xx + a, yy + bh // 2, a + 1, "right" if right else "left", hexc("#d8d4c0"))
            li += 1
        yy += bh
    # lane dashes
    c.fill(ix, iy + ih, iw, 0, SHADOW)
    # cars
    for (cxx, lane, colr) in ((0.15, 0, "#e54b3c"), (0.42, 0, "#f2c94c"), (0.74, 1, "#4aa8f0"), (0.30, 2, "#e8e8e8"), (0.62, 3, "#8ed04a")):
        # lane y positions
        pass
    lane_y = []
    yy = iy
    for kind, f in bands:
        bh = max(2, int(ih * f / tot))
        if kind == "lane":
            lane_y.append((yy, bh))
        yy += bh
    for cxx, ln, colr in ((0.15, 0, "#e54b3c"), (0.46, 0, "#f2c94c"), (0.74, 1, "#4aa8f0"), (0.30, 2, "#e8e8e8"), (0.62, 3, "#8ed04a")):
        ly, lh = lane_y[ln]
        cw_, ch_ = s(11), max(2, lh - s(4))
        px = ix + int(iw * cxx)
        c.fill(px, ly + (lh - ch_) // 2, cw_, ch_, hexc(colr))
        c.fill(px + cw_ - s(3), ly + (lh - ch_) // 2 + T.b, s(2), max(1, ch_ - 2 * T.b), hexc("#303844"))


ADDONS = [("addon_trees", "Trees"), ("addon_barrier", "Barrier"), ("addon_lights", "Lights"), ("addon_parking", "Parking"),
          ("addon_buslane", "Bus lane"), ("addon_bikelane", "Bike lane")]


@reg("road-inspector-overview", "Road inspector: overview", 300, 280, tab=0)
@reg("road-inspector-traffic", "Road inspector: traffic", 300, 280, tab=1)
def road_inspector(c, T, x, y, tab=0, family="city"):
    s = T.s
    w, h = s(300), s(280)
    tabs = [ic("ui_eye", T.icon), ic("stat_traffic", T.icon)]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Maple Avenue", family, tabs, tab)
    ramp, body, acc = fam(family)
    lab = T.f_label
    cap = F.cap_height(lab)
    ty = cy
    if tab == 0:
        side = s(26)
        mw = cw - side - s(4)
        road_section(c, T, cx, ty, mw, s(56))
        bx = cx + cw - side + T.b
        for i, (n, st) in enumerate((("ui_locate", "normal"), ("ui_follow", "normal"))):
            icon_button(c, T, bx, ty + i * (side + s(2)), side, side, ic(n, T.icon), family, st, flat=False)
        ty += s(62)
        # type + title line
        c.blit(ic("road_avenue", T.icon), cx, ty - (T.icon - cap) // 2)
        c.text(cx + T.icon + s(4), ty, "Avenue", T.f_value, INK)
        c.text(cx + T.icon + s(4) + F.measure("Avenue", T.f_value) + s(6), ty, "4 lanes", T.f_small, R(ramp, 2))
        c.text(cx, ty, "Paired", T.f_small, R(ramp, 2), align="right", w=mw)
        ty += max(T.icon, s(12)) + s(3)
        for l, v, f, rr in (("Traffic flow", "72%", 0.72, "yellow"), ("Condition", "88%", 0.88, "green")):
            c.text(cx, ty, l, lab, INK)
            meter(c, T, cx + s(70), ty - s(1), cw - s(120), f, family, rr)
            c.text(cx, ty, v, T.f_value, INK, align="right", w=cw)
            ty += s(13)
        ty += s(2)
        half = cw // 2
        bs = s(16)
        c.text(cx, ty + (bs - cap) // 2, "Speed limit", lab, INK)
        small_btn(c, T, cx + s(62), ty, bs, "ui_minus", family)
        c.text(cx + s(62) + bs, ty + (bs - cap) // 2, "60", T.f_value, INK, align="center", w=s(22))
        small_btn(c, T, cx + s(84) + bs, ty, bs, "ui_plus", family)
        c.text(cx + s(88) + 2 * bs, ty + (bs - cap) // 2, "km/h", T.f_small, R(ramp, 2))
        ty += bs + s(4)
        value_row(c, T, cx, ty, half - s(8), "Length", "14 tiles", INK)
        value_row(c, T, cx + half + s(8), ty, half - s(8), "Upkeep", "$38/mo", MONEY_NEG)
        ty += s(13)
        etched_h(c, T, cx, ty, cw, ramp)
        ty += s(5)
        c.text(cx, ty + (s(22) - cap) // 2, "Add-ons", lab, INK)
        ab = s(22)
        on = {"addon_trees": True, "addon_lights": True, "addon_buslane": False}
        for i, (n, nm) in enumerate(ADDONS):
            bx = cx + s(50) + i * (ab + s(3))
            icon_button(c, T, bx, ty, ab, ab, ic(n, T.icon if T.icon <= ab - 2 else 16), family, "toggled" if on.get(n) else "normal", flat=False)
        ty += ab + s(4)
        c.text(cx + s(50), ty, "Trees, lights", T.f_small, R(ramp, 2))
        c.text(cx, ty, "+$6 / tile", T.f_small, MONEY_NEG, align="right", w=cw)
        by = y + h - s(24)
        bw = (cw - 2 * s(4)) // 3
        button(c, T, cx, by, bw, s(16), "Upgrade", family, "normal", icon=ic("tool_upgrade", s(12) if T.u < 1.25 else T.icon))
        button(c, T, cx + bw + s(4), by, bw, s(16), "One-way", family, "toggled", icon=ic("mode_oneway", s(12) if T.u < 1.25 else T.icon))
        button(c, T, cx + 2 * (bw + s(4)), by, bw, s(16), "Bulldoze", family, "normal", icon=ic("tool_bulldoze", s(12) if T.u < 1.25 else T.icon))
    else:
        ty += header(c, T, cx, ty, cw, "Traffic flow, last 24 hours", family)
        gh = s(100)
        graph(c, T, cx, ty, cw, gh, family,
              [([22, 18, 14, 16, 30, 58, 74, 91, 66, 52, 55, 60, 64, 58, 56, 66, 80, 88, 70, 52, 38, 30, 26, 22], hexc("#f6e080"))],
              ymax=100, ylabels=["100%", "50%", "0%"], xlabels=["0h", "6h", "12h", "18h", "24h"], fill_first=True)
        ty += gh + s(6)
        ty += header(c, T, cx, ty, cw, "Vehicle mix", family)
        for l, f, rr, v in (("Cars", 0.71, "blue", "71%"), ("Trucks", 0.14, "orange", "14%"), ("Buses", 0.09, "green", "9%"), ("Bikes", 0.06, "purple", "6%")):
            c.text(cx, ty, l, lab, INK)
            meter(c, T, cx + s(60), ty - s(1), cw - s(100), f, family, rr)
            c.text(cx, ty, v, T.f_value, INK, align="right", w=cw)
            ty += s(13)
        value_row(c, T, cx, ty + s(2), cw, "Busiest hour", "08:00 (91%)", MONEY_NEG)
    return w, h


# ================================================================================================
# 6. TOOL OPTION PALETTES (small floating toolbars, RCT2 construction style)
# ================================================================================================
def tool_btn(c, T, x, y, w, h, icon_name, family, state="normal", tint=None):
    """Toolbar-size icon toggle (state normal|toggled|hover); tint = category colour bar along the bottom."""
    tb = tbsize(T)
    icon_button(c, T, x, y, w, h, ic(icon_name, tb), family, state, flat=False)
    if tint is not None:
        bh = T.s(3)
        off = T.b if state == "toggled" else 0
        c.fill(x + T.b * 2 + off, y + h - T.b * 2 - bh + off, w - 4 * T.b, bh, SHADOW)
        c.fill(x + T.b * 3 + off, y + h - T.b * 2 - bh + off + T.b, w - 6 * T.b, bh - 2 * T.b, hexc(tint))


def palette_window(c, T, x, y, w, h, title, family):
    return window(c, T, x, y, w, h, title, family, None, 0, grip=False)


def cost_well(c, T, x, y, w, left, right, family):
    h = T.s(18)
    bevel(c, x, y, w, h, fam(family)[0], "in", 1, T.b, 5, 0)
    ty = y + (h - F.cap_height(T.f_value)) // 2
    c.text(x + T.s(5), ty, left, T.f_value, CREAM)
    c.text(x, ty, right, T.f_value, MONEY_NEG_L, align="right", w=w - T.s(5))
    return h


ROAD_TYPES = [("road_street", "Street", "2 lanes", 60), ("road_gravel", "Gravel", "2 lanes", 30), ("road_avenue", "Avenue", "4 lanes", 120),
              ("road_boulevard", "Boulevard", "4 lanes + trees", 160), ("road_highway", "Highway", "6 lanes", 400), ("road_alley", "Alley", "1 lane", 25)]
ROAD_MODES = [("mode_straight", "Straight"), ("mode_curve", "Curve"), ("mode_freeform", "Freeform"), ("mode_grid", "Grid")]


@reg("tool-road", "Tool options: road", 212, 190, mode=0, rtype=2)
@reg("tool-road-curve", "Tool options: road (curve, street)", 212, 190, mode=1, rtype=0)
def tool_road(c, T, x, y, mode=0, rtype=2, family="city"):
    s = T.s
    tb = tbsize(T)
    bs = tb + s(6)
    gap = s(3)
    cw = 6 * bs + 5 * gap
    w = cw + 2 * s(4) + 2 * T.b
    lh = T.line(T.f_small)
    hdr = F.cap_height(T.f_label) + s(6)
    h = T.b + T.title_h + s(4) + hdr + bs + s(3) + hdr + bs + s(3) + lh + s(5) + hdr + 2 * s(13) + s(4) + s(18) + s(4) + s(4)
    cx, cy, _, _ = palette_window(c, T, x, y, w, h, "Road tool", family)
    ty = cy
    ty += header(c, T, cx, ty, cw, "Drawing mode", family)
    for i, (n, nm) in enumerate(ROAD_MODES):
        tool_btn(c, T, cx + i * (bs + gap), ty, bs, bs, n, family, "toggled" if i == mode else ("hover" if i == 3 and mode == 0 else "normal"))
    c.text(cx + 4 * (bs + gap), ty + s(4), ROAD_MODES[mode][1], T.f_label, INK)
    c.text(cx + 4 * (bs + gap), ty + s(4) + T.line(T.f_label), "[R]", T.f_small, R(fam(family)[0], 2))
    ty += bs + s(3)
    ty += header(c, T, cx, ty, cw, "Road type", family)
    for i, (n, nm, ln, cost) in enumerate(ROAD_TYPES):
        tool_btn(c, T, cx + i * (bs + gap), ty, bs, bs, n, family, "toggled" if i == rtype else "normal")
    ty += bs + s(3)
    nm, ln, cost = ROAD_TYPES[rtype][1], ROAD_TYPES[rtype][2], ROAD_TYPES[rtype][3]
    c.text(cx, ty, nm, T.f_label, INK)
    c.text(cx + F.measure(nm, T.f_label) + s(6), ty + (F.cap_height(T.f_label) - F.cap_height(T.f_small)), ln, T.f_small, R(fam(family)[0], 2))
    c.text(cx, ty, money(cost) + " / tile", T.f_value, MONEY_NEG, align="right", w=cw)
    ty += lh + s(5)
    ty += header(c, T, cx, ty, cw, "Snapping", family)
    half = cw // 2
    checkbox(c, T, cx, ty, "Road ends", family, True)
    checkbox(c, T, cx + half, ty, "Grid", family, True)
    checkbox(c, T, cx, ty + s(13), "Angle", family, False)
    checkbox(c, T, cx + half, ty + s(13), "Parallel", family, False)
    ty += 2 * s(13) + s(4)
    cost_well(c, T, cx, ty, cw, "Length 12 tiles", money(12 * cost), family)
    return w, h


ZONE_ROW1 = [("zone_res_low", "Residential low", "#7ed957"), ("zone_res_high", "Residential high", "#3ea83a"),
             ("zone_res_row", "Row houses", "#5cc8a0"), ("zone_res_med", "Residential medium", "#58b848"),
             ("zone_res_mixed", "Mixed use", "#b8d84a"), ("zone_res_lowrent", "Low rent housing", "#a8c878")]
ZONE_ROW2 = [("zone_com_low", "Commercial low", "#5ab4f0"), ("zone_com_high", "Commercial high", "#2a78c8"),
             ("zone_ind", "Industrial", "#f2c94c"), ("zone_off", "Office", "#b07ce8"),
             ("zone_off_high", "Office high", "#7a4cc0"), ("zone_warehouse", "Warehouse", "#d8943a")]


@reg("tool-zoning", "Tool options: zoning", 240, 210, zsel=0, tsel="paint")
@reg("tool-zoning-fill", "Tool options: zoning (commercial, fill)", 240, 210, zsel=7, tsel="fill")
def tool_zoning(c, T, x, y, zsel=0, tsel="paint", brush=1, family="zoning"):
    s = T.s
    tb = tbsize(T)
    bs = tb + s(6)
    bh = tb + s(10)
    gap = s(3)
    sep = s(8)
    cw = 6 * bs + 5 * gap + 2 * sep // 2 * 0 + s(12)
    w = cw + 2 * s(4) + 2 * T.b
    lh = T.line(T.f_small)
    hdr = F.cap_height(T.f_label) + s(6)
    h = T.b + T.title_h + s(4) + hdr + 2 * (bh + gap) + lh + s(4) + hdr + bs + s(5) + s(13) + s(4) + s(13) + s(2)
    cx, cy, _, _ = palette_window(c, T, x, y, w, h, "Zoning tool", family)
    ty = cy
    ty += header(c, T, cx, ty, cw, "Zone type", family)
    gx = cx + (cw - (6 * bs + 5 * gap)) // 2
    for r, row in enumerate((ZONE_ROW1, ZONE_ROW2)):
        for i, (n, nm, col_) in enumerate(row):
            idx = r * 6 + i
            tool_btn(c, T, gx + i * (bs + gap), ty, bs, bh, n, family, "toggled" if idx == zsel else "normal", tint=col_)
        ty += bh + gap
    allz = ZONE_ROW1 + ZONE_ROW2
    n, nm, col_ = allz[zsel]
    swatch(c, T, cx + T.b, ty + (lh - s(8)) // 2, s(10), s(8), col_)
    c.text(cx + s(16), ty + (lh - F.cap_height(T.f_label)) // 2 + s(1), nm, T.f_label, INK)
    c.text(cx, ty + (lh - F.cap_height(T.f_small)) // 2 + s(1), "Demand", T.f_small, R(fam(family)[0], 2), align="right", w=cw - s(54))
    meter(c, T, cx + cw - s(48), ty + (lh - s(7)) // 2 + s(1), s(48), 0.68 if zsel < 6 else 0.4, family, "green" if zsel < 6 else "blue", h=s(7))
    ty += lh + s(4)
    ty += header(c, T, cx, ty, cw, "Tool", family)
    x0 = cx + (cw - (6 * bs + 5 * gap)) // 2
    for i, (n2, key) in enumerate((("tool_area_paint", "paint"), ("tool_fill", "fill"), ("tool_marquee", "marquee"))):
        tool_btn(c, T, x0 + i * (bs + gap), ty, bs, bs, n2, family, "toggled" if tsel == key else "normal")
    etched_v(c, T, x0 + 3 * (bs + gap) - gap // 2 - T.b, ty, bs, fam(family)[0])
    for i, n2 in enumerate(("tool_brush_small", "tool_brush_large")):
        tool_btn(c, T, x0 + (3 + i) * (bs + gap) + gap, ty, bs, bs, n2, family, "toggled" if (brush == i + 0) else "normal")
    tool_btn(c, T, x0 + 5 * (bs + gap) + 2 * gap, ty, bs, bs, "tool_dezone", family, "normal")
    ty += bs + s(5)
    half = cw // 2
    checkbox(c, T, cx, ty, "Show zone grid", family, True)
    checkbox(c, T, cx + half + s(10), ty, "Overlay", family, True)
    ty += s(13) + s(2)
    c.text(cx, ty, "Brush: small (1 tile)" if brush == 0 else "Brush: large (3 x 3)", T.f_small, R(fam(family)[0], 2))
    c.text(cx, ty, "Free of charge", T.f_small, MONEY_POS, align="right", w=cw)
    return w, h


@reg("tool-terraform", "Tool options: terraform", 212, 160, tsel=0)
@reg("tool-terraform-lower", "Tool options: terraform (lower)", 212, 160, tsel=1)
def tool_terraform(c, T, x, y, tsel=0, family="city"):
    s = T.s
    tb = tbsize(T)
    bs = tb + s(6)
    gap = s(3)
    cw = 6 * bs + 5 * gap
    w = cw + 2 * s(4) + 2 * T.b
    hdr = F.cap_height(T.f_label) + s(6)
    h = T.b + T.title_h + s(4) + hdr + bs + s(6) + 2 * s(17) + s(2) + s(18) + s(4) + s(6)
    cx, cy, _, _ = palette_window(c, T, x, y, w, h, "Terraform", family)
    ty = cy
    ty += header(c, T, cx, ty, cw, "Operation", family)
    names = [("tool_terrain_raise", "Raise"), ("tool_terrain_lower", "Lower"), ("tool_terrain_level", "Level")]
    for i, (n, nm) in enumerate(names):
        tool_btn(c, T, cx + i * (bs + gap), ty, bs, bs, n, family, "toggled" if i == tsel else "normal")
    # brush preview
    px = cx + 3 * (bs + gap) + s(2)
    pw = cw - 3 * (bs + gap) - s(2)
    bevel(c, px, ty, pw, bs, fam(family)[0], "in", 1, T.b, 5, 0)
    rad = (min(pw, bs) - 2 * T.b) // 2 - s(1)
    mx, my = px + pw // 2, ty + bs // 2
    base = hexc("#8a6a3a") if tsel != 1 else hexc("#4a3a2a")
    for yy in range(-rad, rad + 1):
        for xx in range(-rad, rad + 1):
            d = (xx * xx + yy * yy) ** 0.5 / rad
            if d <= 1.0:
                t = (1 - d) ** 1.2
                col_ = mix(hexc("#3a2a1a"), hexc("#f2d8a0") if tsel != 1 else hexc("#1a120a"), t) if tsel != 2 else mix(hexc("#5a4a38"), hexc("#bcae8a"), 0.5 if d < 0.8 else 0.2)
                c.fill(mx + xx, my + yy, 1, 1, col_)
    ty += bs + s(6)
    lab = T.f_label
    cap = F.cap_height(lab)
    for l, v, vt in (("Brush size", 0.45, "12 m"), ("Strength", 0.7, "70%")):
        c.text(cx, ty + (s(12) - cap) // 2, l, lab, INK)
        slider(c, T, cx + s(62), ty, cw - s(62) - s(40), family, v, value_text=vt)
        ty += s(17)
    ty += s(2)
    cost_well(c, T, cx, ty, cw, "Area 48 tiles", money(960), family)
    return w, h
