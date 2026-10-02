"""
iso_ui_panels_finance - RCT2-style finance / info panels: budget window (6 pages), statistics window
(6 pages), production & trade panel, city info dashboard.

Panel convention: def panel_x(c, T, x, y, **opts) -> (w, h); PANELS lists standalone renders.
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import Theme, window, button, icon_button, bevel, fam, etched_h, etched_v
from iso_ui_widgets import well, checkbox, slider, list_rows, scrollbar, dropdown, arrow
from iso_ui_widgets2 import meter, graph, value_row, groupbox, money, ticker
from iso_ui_parts import thumb, card, header, chip
from iso_ui_icons import ic
import iso_ui_font as F

PANELS = []  # (file stem, title, fn, w_hint, h_hint, kwargs)


def reg(stem, title, w, h, **kw):
    def deco(fn):
        PANELS.append((stem, title, fn, w, h, kw))
        return fn
    return deco


ZONE = {"R": ("Residential", "#7ED957"), "C": ("Commercial", "#5AB4F0"),
        "I": ("Industrial", "#F2C94C"), "O": ("Office", "#B07CE8")}
GREEN_L, RED_L = hexc("#7cf06c"), hexc("#ff6a50")


# ---- shared helpers --------------------------------------------------------------------------------
def isz(T):
    return 16 if T.u < 1.25 else 24


def swatch(c, T, x, y, colr, w=None, h=None):
    """Small coloured key square with a dark outline."""
    w = w or T.s(8)
    h = h or w
    c.fill(x, y, w, h, R("grey", 0))
    c.fill(x + T.b, y + T.b, w - 2 * T.b, h - 2 * T.b, hexc(colr) if isinstance(colr, str) else colr)


def cap(T, face=None):
    return F.cap_height(face or T.f_label)


def table(c, T, x, y, w, rows, family, cols, header=None, rh=None, selected=None, hover=None, total=False, zebra=True):
    """Sunken list. cols = [(width|None(flex), align)], cell = str | (str, colour) | Canvas | None.
    total=True draws the last row as a bold summary row. Returns height."""
    ramp, body, _ = fam(family)
    rh = rh or T.row_h
    n = len(rows) + (1 if header else 0)
    h = n * rh + 2 * T.b
    well(c, T, x, y, w, h, family)
    iw = w - 2 * T.b
    fixed = sum(cw for cw, _ in cols if cw is not None)
    nflex = sum(1 for cw, _ in cols if cw is None)
    widths = [cw if cw is not None else (iw - fixed) // max(1, nflex) for cw, _ in cols]
    cy = y + T.b
    if header:
        bevel(c, x + T.b, cy, iw, rh, ramp, "out", body, T.b, 7, 2)
        cx = x + T.b
        for (cw, al), wd, cell in zip(cols, widths, header):
            if isinstance(cell, str):
                c.text(cx + T.s(3), cy + (rh - cap(T, T.f_small)) // 2, cell, T.f_small, INK, align=al, w=wd - T.s(6))
            cx += wd
        cy += rh
    for i, r in enumerate(rows):
        last = total and i == len(rows) - 1
        fillc = None
        if zebra and i % 2 == 1:
            fillc = mix(R(ramp, 7), R(ramp, 6), 0.55)
        if hover == i:
            fillc = R(ramp, 5)
        if selected == i:
            fillc = R(ramp, 3)
        if last:
            fillc = R(ramp, 5)
            c.fill(x + T.b, cy, iw, T.b, R(ramp, 2))
        if fillc is not None:
            c.fill(x + T.b, cy, iw, rh, fillc)
        cx = x + T.b
        for (cw, al), wd, cell in zip(cols, widths, r):
            colr = WHITE if selected == i else INK
            if isinstance(cell, Canvas):
                bx = cx + T.s(2) if al == "left" else cx + (wd - cell.w) // 2 if al == "center" else cx + wd - cell.w - T.s(2)
                c.blit(cell, bx, cy + (rh - cell.h) // 2)
            elif cell:
                if isinstance(cell, tuple):
                    cell, colr = cell[0], (cell[1] if selected != i else WHITE)
                face = T.f_value if last else T.f_label
                c.text(cx + T.s(3), cy + (rh - cap(T, face)) // 2, cell, face, colr, align=al, w=wd - T.s(6))
            cx += wd
        cy += rh
    return h


def foot(c, T, x, y, w, h, family, label, value, colr, label2=None, value2=None):
    """Window footer: etched line, funds on the left, balance on the right (RCT2 'cash' line)."""
    ramp = fam(family)[0]
    fy = y + h - T.s(14)
    etched_h(c, T, x, fy - T.s(3), w, ramp)
    c.text(x, fy + T.s(1), label, T.f_label, INK)
    c.text(x + F.measure(label, T.f_label) + T.s(5), fy + T.s(1), value, T.f_value, MONEY_POS)
    if label2:
        vw = F.measure(value2, T.f_value)
        c.text(x, fy + T.s(1), value2, T.f_value, colr, align="right", w=w)
        c.text(x, fy + T.s(1), label2, T.f_label, INK, align="right", w=w - vw - T.s(5))


def slider_row(c, T, x, y, w, rh, family, lead, name, val, vtxt, right=None, rcol=INK, name_w=None,
               rw=None, vw=None, state="normal", fill_bg=None):
    """One list row: lead (Canvas icon | swatch colour str), name, slider, value text, right column."""
    s = T.s
    if fill_bg is not None:
        c.fill(x, y, w, rh, fill_bg)
    lx = x + s(3)
    if isinstance(lead, Canvas):
        c.blit(lead, lx, y + (rh - lead.h) // 2)
        lx += lead.w + s(4)
    elif lead:
        swatch(c, T, lx, y + (rh - s(8)) // 2, lead)
        lx += s(8) + s(5)
    name_w = name_w or s(56)
    c.text(lx, y + (rh - cap(T)) // 2, name, T.f_label, INK)
    sx = lx + name_w
    rw = rw or 0
    vw = vw or s(30)
    sw_ = x + w - rw - vw - s(8) - sx
    slider(c, T, sx, y + (rh - s(12)) // 2, sw_, family, val, state)
    c.text(sx + sw_ + s(5), y + (rh - cap(T, T.f_value)) // 2, vtxt, T.f_value, INK, align="right", w=vw)
    if right is not None:
        c.text(x + w - rw, y + (rh - cap(T, T.f_value)) // 2, right, T.f_value, rcol, align="right", w=rw - s(4))


def legend_keys(c, T, x, y, items):
    """Right-aligned (from x going left) line-key legend: [(label, colour)] -> total width."""
    s = T.s
    cx = x
    for lab, colr in reversed(items):
        tw = F.measure(lab, T.f_small)
        cx -= tw
        c.text(cx, y, lab, T.f_small, INK)
        cx -= s(14)
        c.fill(cx, y + cap(T, T.f_small) // 2 - T.b // 2, s(10), max(2, T.b * 2), colr)
        cx -= s(6)
    return x - cx


# ---- 1. Budget window ------------------------------------------------------------------------------
BUDGET_TABS = ["pnl_budget", "stat_income", "stat_expenses", "stat_tax", "stat_fee", "stat_loan"]
INCOME = [("R", "Residential tax", 12480), ("C", "Commercial tax", 8940), ("I", "Industrial tax", 6210),
          ("O", "Office tax", 4350),
          ("info_power", "Power fees", 3120), ("info_water", "Water fees", 2480), ("info_garbage", "Garbage fees", 1310),
          ("info_health", "Health fees", 1060), ("info_education", "Education fees", 940),
          ("tr_ship", "Exports (trade)", 6740), ("tr_bus", "Transit fares", 2260), ("res_paper", "Recycling sales", 820),
          ("stat_permit", "Gov. subsidy", 1500)]
SERVICES = [("cat_police", "Police", 100, 4200), ("cat_fire", "Fire", 110, 3100), ("cat_health", "Health", 100, 5600),
            ("cat_education", "Education", 120, 7800), ("cat_garbage", "Garbage", 90, 2900),
            ("cat_deathcare", "Deathcare", 100, 900), ("cat_parks", "Parks", 80, 1800), ("cat_power", "Power", 100, 6100),
            ("cat_water", "Water", 100, 2700), ("info_sewage", "Sewage", 100, 1950), ("cat_comms", "Telecom", 70, 800),
            ("info_post", "Post", 100, 1100), ("cat_admin", "Admin", 100, 3400)]
CASH_IN = [19800, 21400, 22100, 23800, 24600, 25100, 26900, 27400, 28800, 30100, 31900, 32400]   # unused scale hint
INC_12 = [38.2, 40.1, 41.6, 43.0, 44.8, 45.3, 47.2, 48.0, 49.1, 50.4, 51.6, 52.2]
EXP_12 = [36.5, 38.0, 41.9, 42.4, 43.9, 46.3, 45.8, 47.1, 48.6, 49.0, 48.3, 48.8]


def budget_graph(c, T, x, y, w, h, family):
    graph(c, T, x, y, w, h, family, [(INC_12, GREEN_L), (EXP_12, RED_L)], ymax=60,
          ylabels=["$60k", "$45k", "$30k", "$15k", "$0"], xlabels=["Jan", "Apr", "Jul", "Oct", "Dec"], fill_first=False)


@reg("budget-overview", "Budget: overview", 340, 300, tab=0)
@reg("budget-income", "Budget: income", 340, 300, tab=1)
@reg("budget-services", "Budget: expenses / service budgets", 340, 300, tab=2)
@reg("budget-taxes", "Budget: taxes", 340, 300, tab=3)
@reg("budget-fees", "Budget: fees", 340, 300, tab=4)
@reg("budget-loans", "Budget: loans", 340, 300, tab=5)
def budget(c, T, x, y, tab=0, family="finance"):
    s = T.s
    w, h = s(340), s(300)
    tabs = [ic(n, T.icon) for n in BUDGET_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "City Budget", family, tabs, tab)
    ramp = fam(family)[0]
    ch_c = ch - s(18)   # content height above the footer
    foot(c, T, cx, cy, cw, ch, family, "Funds", "$148,520", MONEY_POS, "Balance", "+$3,420 / mo")
    ty = cy
    n = isz(T)
    if tab == 0:
        ty += header(c, T, cx, ty, cw, "This month", family)
        for lab, ico, val, ramp_c, txt, tc in (("Income", "stat_income", 52210 / 60000.0, "green", "+$52,210", MONEY_POS),
                                                ("Expenses", "stat_expenses", 48790 / 60000.0, "red", "-$48,790", MONEY_NEG)):
            c.blit(ic(ico, n), cx, ty - (n - cap(T)) // 2)
            c.text(cx + n + s(4), ty, lab, T.f_label, INK)
            meter(c, T, cx + s(78), ty - s(1), cw - s(78) - s(70), val, family, ramp_c, h=s(10))
            c.text(cx, ty, txt, T.f_value, tc, align="right", w=cw)
            ty += s(15)
        etched_h(c, T, cx, ty, cw, ramp)
        ty += s(5)
        c.blit(ic("stat_balance_up", n), cx, ty - (n - cap(T)) // 2)
        c.text(cx + n + s(4), ty, "Net result", T.f_label, INK)
        c.text(cx, ty, "+$3,420", T.f_value, MONEY_POS, align="right", w=cw)
        ty += s(17)
        ty += header(c, T, cx, ty, cw, "Cashflow, last 12 months", family)
        legend_keys(c, T, cx + cw, ty, [("Income", GREEN_L), ("Expenses", RED_L)])
        ty += s(11)
        budget_graph(c, T, cx, ty, cw, cy + ch_c - ty, family)
    elif tab == 1:
        ty += header(c, T, cx, ty, cw, "Monthly income", family)
        rows = []
        for k, name, v in INCOME:
            if k in ZONE:
                lead = Canvas(s(12), s(12))
                swatch(lead, T, s(2), s(2), ZONE[k][1])
            else:
                lead = ic(k, T.icon)
            rows.append((lead, name, ("%d%%" % round(v * 100 / 52210.0), R("grey", 4)), (money(v, True), MONEY_POS)))
        rows.append((None, "Total income", None, ("+$52,210", MONEY_POS)))
        rh = s(14) if T.u < 1.25 else s(14)
        cols = [(max(T.icon, s(12)) + s(6), "left"), (None, "left"), (s(34), "right"), (s(64), "right")]
        avail = cy + ch_c - ty
        rh = min(s(15), (avail - 2 * T.b - s(12)) // (len(rows) + 1))
        table(c, T, cx, ty, cw, rows, family, cols, header=("", "Source", "Share", "Per month"), rh=rh, total=True)
    elif tab == 2:
        ty += header(c, T, cx, ty, cw, "Service budgets", family)
        rh = s(16)
        sbw = s(10)
        lw = cw - sbw - s(1)
        # layout of the framed list: header + visible rows
        avail = cy + ch_c - ty - s(14)
        nvis = max(1, min(len(SERVICES), (avail - 2 * T.b - s(12)) // rh))
        lh = nvis * rh + s(12) + 2 * T.b
        well(c, T, cx, ty, lw, lh, family)
        iw = lw - 2 * T.b
        bevel(c, cx + T.b, ty + T.b, iw, s(12), ramp, "out", fam(family)[1], T.b, 7, 2)
        hy = ty + T.b + (s(12) - cap(T, T.f_small)) // 2
        c.text(cx + s(22), hy, "Service", T.f_small, INK)
        c.text(cx + s(88), hy, "Budget", T.f_small, INK)
        c.text(cx + T.b, hy, "Upkeep / mo", T.f_small, INK, align="right", w=iw - s(4))
        ry = ty + T.b + s(12)
        for i, (k, name, pct, up) in enumerate(SERVICES[:nvis]):
            bg = mix(R(ramp, 7), R(ramp, 6), 0.55) if i % 2 else None
            slider_row(c, T, cx + T.b, ry, iw, rh, family, ic(k, T.icon), name, (pct - 50) / 100.0, "%d%%" % pct,
                       "-" + money(up)[0:], MONEY_NEG, name_w=s(56), rw=s(52), vw=s(28), fill_bg=bg,
                       state="hover" if i == 3 else "normal")
            ry += rh
        scrollbar(c, T, cx + lw + s(1), ty, lh, family, 0.0, nvis / float(len(SERVICES)))
        ty += lh + s(4)
        c.text(cx, ty, "Total services (13)", T.f_label, INK)
        c.text(cx, ty, "-$42,350", T.f_value, MONEY_NEG, align="right", w=cw)
    elif tab == 3:
        ty += header(c, T, cx, ty, cw, "Tax rates", family)
        rates = {"R": (9, 12480), "C": (10, 8940), "I": (11, 6210), "O": (12, 4350)}
        rh = s(19)
        for k, (nm, colr) in ZONE.items():
            r, inc = rates[k]
            slider_row(c, T, cx, ty, cw, rh, family, colr, nm, (r + 10) / 40.0, "%d%%" % r, money(inc, True), MONEY_POS,
                       name_w=s(62), rw=s(56), vw=s(26))
            ty += rh
        ty += s(5)
        ty += header(c, T, cx, ty, cw, "Residential tax by education", family)
        lv = [("Uneducated", 7, 1840), ("Poorly educated", 8, 3120), ("Educated", 9, 4260), ("Well educated", 10, 2310),
              ("Highly educated", 11, 950)]
        rh2 = s(17)
        lh = len(lv) * rh2 + 2 * T.b
        well(c, T, cx, ty, cw, lh, family)
        for i, (nm, r, inc) in enumerate(lv):
            bg = mix(R(ramp, 7), R(ramp, 6), 0.55) if i % 2 else None
            slider_row(c, T, cx + T.b, ty + T.b + i * rh2, cw - 2 * T.b, rh2, family, None, nm, (r + 10) / 40.0, "%d%%" % r,
                       money(inc, True), MONEY_POS, name_w=s(84), rw=s(52), vw=s(26), fill_bg=bg)
        ty += lh + s(8)
        value_row(c, T, cx, ty, cw, "Projected income", "+$31,980 / mo", MONEY_POS, icon=ic("stat_tax", n))
        ty += s(14)
        value_row(c, T, cx, ty, cw, "Change vs. last month", "+4.2%", MONEY_POS)
    elif tab == 4:
        ty += header(c, T, cx, ty, cw, "Service fees", family)
        fees = [("info_power", "Power", 100, 3120), ("info_water", "Water", 120, 2480), ("info_garbage", "Garbage", 100, 1310),
                ("info_health", "Health", 90, 1060), ("info_education", "Education", 80, 940)]
        rh = s(21)
        lh = len(fees) * rh + 2 * T.b
        well(c, T, cx, ty, cw, lh, family)
        for i, (k, nm, pct, inc) in enumerate(fees):
            bg = mix(R(ramp, 7), R(ramp, 6), 0.55) if i % 2 else None
            slider_row(c, T, cx + T.b, ty + T.b + i * rh, cw - 2 * T.b, rh, family, ic(k, T.icon), nm, (pct - 50) / 150.0,
                       "%d%%" % pct, money(inc, True), MONEY_POS, name_w=s(50), rw=s(52), vw=s(28), fill_bg=bg)
        ty += lh + s(6)
        ty += header(c, T, cx, ty, cw, "Effect of fees", family)
        value_row(c, T, cx, ty, cw, "Total fee income", "+$8,910 / mo", MONEY_POS, icon=ic("stat_fee", n))
        ty += s(14)
        c.text(cx, ty, "Happiness", T.f_label, INK)
        meter(c, T, cx + s(62), ty - s(1), cw - s(62) - s(40), 0.78, family, "yellow", h=s(10))
        c.text(cx, ty, "-2%", T.f_value, MONEY_NEG, align="right", w=cw)
        ty += s(15)
        c.blit(ic("ui_info", n), cx, ty - (n - cap(T)) // 2)
        c.text(cx + n + s(4), ty, "Fees above 100% cost happiness,", T.f_small, INK)
        c.text(cx + n + s(4), ty + s(10), "below 100% cost income.", T.f_small, INK)
    else:
        ty += header(c, T, cx, ty, cw, "Current loans", family)
        loans = [("Oakridge Bank", "$50,000", "$32,400", "3.5%", "$1,560"), ("City Bond 2", "$30,000", "$30,000", "5.0%", "$1,500")]
        cols = [(None, "left"), (s(50), "right"), (s(34), "right"), (s(50), "right"), (s(40), "left")]
        lrows = [(a, b, d, (f, MONEY_NEG), None) for a, b, _o, d, f in loans]
        lrows = [(a, o, d, (f, MONEY_NEG), None) for a, _b, o, d, f in loans]
        lrows.append(("Total owed", "$62,400", None, ("$3,060", MONEY_NEG), None))
        lh = table(c, T, cx, ty, cw, lrows, family, cols,
                   header=("Lender", "Owed", "Rate", "Per month", ""), rh=s(16), total=True)
        # repay buttons overlay the last column
        by = ty + T.b + s(16)
        for i in range(len(loans)):
            button(c, T, cx + cw - s(38) - T.b, by + i * s(16) + s(2), s(36), s(12), "Repay", family, "normal", face=T.f_small)
        ty += lh + s(5)
        c.text(cx, ty, "Credit used", T.f_label, INK)
        meter(c, T, cx + s(60), ty - s(1), cw - s(60) - s(100), 62400 / 150000.0, family, "blue", h=s(10))
        c.text(cx, ty, "$62,400 / $150,000", T.f_value, INK, align="right", w=cw)
        ty += s(17)
        ty += header(c, T, cx, ty, cw, "Take a loan", family)
        opts = [(25000, "4.0%", "$1,040", "normal"), (50000, "5.5%", "$2,290", "normal"), (100000, "7.0%", "$5,830", "disabled")]
        gw = (cw - 2 * s(5)) // 3
        for i, (amt, rate, pay, st) in enumerate(opts):
            gx = cx + i * (gw + s(5))
            gh = s(74)
            bevel(c, gx, ty, gw, gh, ramp, "out" if st != "disabled" else "in", fam(family)[1] if st != "disabled" else 4, T.b, 7, 1)
            col = INK if st != "disabled" else R(ramp, 3)
            c.text(gx, ty + s(4), money(amt), T.f_big, col, align="center", w=gw)
            c.text(gx, ty + s(4) + cap(T, T.f_big) + s(4), rate + " / year", T.f_label, col, align="center", w=gw)
            c.text(gx, ty + s(4) + cap(T, T.f_big) + s(4) + s(12), pay + " / mo", T.f_small, col, align="center", w=gw)
            button(c, T, gx + s(8), ty + gh - s(20), gw - s(16), s(14), "Borrow", family, "disabled" if st == "disabled" else "normal")
        ty += s(74) + s(6)
        c.blit(ic("ui_info", n), cx, ty - (n - cap(T)) // 2)
        c.text(cx + n + s(4), ty, "Interest is charged monthly. Early repayment is free.", T.f_small, INK)
    return w, h


# ---- 2. Statistics window --------------------------------------------------------------------------
STAT_TABS = ["stat_population", "stat_money", "pnl_city_info", "info_power", "info_pollution", "info_traffic"]
STAT_NAMES = ["Population", "Economy", "City", "Utilities", "Environment", "Traffic"]
SC = ["#7cf06c", "#ff6a50", "#5ab4f0", "#f2c94c", "#d08cff", "#f08cd0", "#50e0d0", "#f0a040", "#c8c8c8", "#a0e060",
      "#ff9a8a", "#90a8ff"]
# page -> (ymax, ylabels, [(name, end value, start fraction, noise, shown, unit)])
STATS = {
    0: (60000, ["60k", "45k", "30k", "15k", "0"], [
        ("Population", 48200, 0.35, 0.02, True, ""), ("Households", 17900, 0.35, 0.02, True, ""),
        ("Workers", 21400, 0.3, 0.03, True, ""), ("Unemployed", 2100, 0.2, 0.15, True, ""),
        ("Jobs", 23800, 0.3, 0.02, False, ""), ("Students", 9100, 0.4, 0.04, False, ""),
        ("Tourists", 3100, 0.1, 0.2, False, ""), ("Buildings", 2600, 0.4, 0.01, False, "")]),
    1: (160000, ["$160k", "$120k", "$80k", "$40k", "$0"], [
        ("Funds", 148520, 0.25, 0.04, True, "$"), ("Income", 52210, 0.5, 0.05, True, "$"),
        ("Expenses", 48790, 0.52, 0.05, True, "$")]),
    2: (100, ["100%", "75%", "50%", "25%", "0%"], [
        ("Happiness", 74, 0.55, 0.06, True, "%"), ("Health", 88, 0.8, 0.03, True, "%"),
        ("Land value", 62, 0.35, 0.03, True, ""), ("Demand: residential", 58, 0.7, 0.15, False, "%"),
        ("Demand: commercial", 41, 0.6, 0.15, False, "%"), ("Demand: industrial", 33, 0.5, 0.15, False, "%"),
        ("Crimes", 12, 0.5, 0.3, False, ""), ("Sick citizens", 9, 0.5, 0.3, False, ""),
        ("Deaths", 7, 0.5, 0.3, False, ""), ("Fires", 3, 0.3, 0.4, False, ""),
        ("Garbage generated", 64, 0.35, 0.05, False, " t"), ("Garbage collected", 59, 0.35, 0.05, False, " t")]),
    3: (50, ["50", "38", "25", "12", "0"], [
        ("Power produced", 42, 0.3, 0.03, True, " MW"), ("Power used", 38, 0.3, 0.04, True, " MW"),
        ("Water produced (kL)", 31, 0.3, 0.03, True, " kL"), ("Water used (kL)", 27, 0.3, 0.04, True, " kL")]),
    4: (100, ["100", "75", "50", "25", "0"], [
        ("Ground pollution", 24, 0.3, 0.08, True, "%"), ("Air pollution", 31, 0.4, 0.08, True, "%"),
        ("Noise pollution", 46, 0.5, 0.06, True, "%"), ("Temperature", 18, 0.5, 0.25, True, " C")]),
    5: (100, ["100%", "75%", "50%", "25%", "0%"], [
        ("Traffic flow", 84, 0.9, 0.05, True, "%"), ("Vehicles (x10)", 64, 0.3, 0.06, True, "")]),
}
SCALE_N = [12, 20, 28]
SCALE_X = [["Jan", "Apr", "Jul", "Oct", "Dec"], ["2025", "2026", "2027", "2028", "2029"], ["2022", "2024", "2026", "2028", "2029"]]
SCALE_NAMES = ["Year", "5 Years", "All"]


def stat_values(end, sf, noise, n, scale, seed):
    import random
    rng = random.Random(seed)
    sf_eff = 1 - (1 - sf) * (0.25, 0.6, 1.0)[scale]
    out = []
    for i in range(n):
        t = i / (n - 1.0)
        v = end * (sf_eff + (1 - sf_eff) * t ** 1.15) + noise * end * (rng.random() - 0.5)
        out.append(max(0.0, v))
    out[-1] = float(end)
    return out


@reg("statistics-population", "Statistics: population", 360, 260, page=0, scale=1)
@reg("statistics-economy", "Statistics: economy", 360, 260, page=1, scale=0)
@reg("statistics-city", "Statistics: city", 360, 260, page=2, scale=2)
@reg("statistics-utilities", "Statistics: utilities", 360, 260, page=3, scale=0)
@reg("statistics-environment", "Statistics: environment", 360, 260, page=4, scale=1)
@reg("statistics-traffic", "Statistics: traffic", 360, 260, page=5, scale=2)
def statistics(c, T, x, y, page=0, scale=0, family="info"):
    s = T.s
    w, h = s(360), s(260)
    tabs = [ic(n, T.icon) for n in STAT_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "City Statistics - " + STAT_NAMES[page], family, tabs, page)
    ramp, body, _ = fam(family)
    ymax, ylabels, series = STATS[page]
    lw = s(116)
    # left: series checklist ------------------------------------------------------------------------
    ty = cy
    ty += header(c, T, cx, ty, lw, "Series", family)
    rh = s(13)
    nvis = min(len(series), (cy + ch - ty - s(14) - 2 * T.b) // rh)
    sbw = s(10) if nvis < len(series) else 0
    lh = nvis * rh + 2 * T.b
    lwi = lw - (sbw + s(1) if sbw else 0)
    well(c, T, cx, ty, lwi, lh, family)
    for i, (nm, end, sf, nz, shown, unit) in enumerate(series[:nvis]):
        ry = ty + T.b + i * rh
        if i % 2:
            c.fill(cx + T.b, ry, lwi - 2 * T.b, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        colr = hexc(SC[i % len(SC)])
        checkbox(c, T, cx + s(3), ry + (rh - s(10)) // 2, "", family, shown)
        kx = cx + s(16)
        c.fill(kx, ry + rh // 2 - T.b, s(9), 2 * T.b, R("grey", 0) if shown else R(ramp, 5))
        c.fill(kx, ry + rh // 2 - T.b, s(9), T.b, colr if shown else R(ramp, 4))
        face = T.f_label if F.measure(nm, T.f_label) < lwi - s(34) else T.f_small
        c.text(kx + s(13), ry + (rh - cap(T, face)) // 2, nm, face, INK if shown else R(ramp, 3))
    if sbw:
        scrollbar(c, T, cx + lw - sbw, ty, lh, family, 0.0, nvis / float(len(series)))
    nshow = sum(1 for sr in series if sr[4])
    c.text(cx, ty + lh + s(4), "%d of %d series shown" % (nshow, len(series)), T.f_small, R(ramp, 2))
    # right: scale buttons + graph ------------------------------------------------------------------
    gx = cx + lw + s(6)
    gw = cw - lw - s(6)
    bw = s(44)
    for i, nm in enumerate(SCALE_NAMES):
        button(c, T, gx + i * (bw + s(2)), cy, bw, s(13), nm, family, "toggled" if i == scale else "normal")
    gy = cy + s(17)
    gh = cy + ch - gy - s(20)
    n_pts = SCALE_N[scale]
    plot = []
    for i, (nm, end, sf, nz, shown, unit) in enumerate(series):
        if shown:
            plot.append((stat_values(end, sf, nz, n_pts, scale, 7 + i + page * 13), hexc(SC[i % len(SC)])))
    graph(c, T, gx, gy, gw, gh, family, plot, ymax=ymax, ylabels=ylabels, xlabels=SCALE_X[scale], grid=4)
    # read-out strip (hover value) ------------------------------------------------------------------
    first = next(sr for sr in series if sr[4])
    unit = first[5]
    val = ("$" + "{:,}".format(int(first[1]))) if unit == "$" else "{:,}".format(int(first[1])) + unit
    when = {0: "Mar", 1: "2028", 2: "2027"}[scale] if False else SCALE_X[scale][-2]
    ticker(c, T, gx, cy + ch - s(16), gw, "%s: %s %s" % (when, first[0], val), family)
    return w, h


# ---- 3. Production & trade panel -------------------------------------------------------------------
# (icon, name, produced, consumed, imported, exported) units per month
RES = [("res_grain", "Grain", 3200, 1800, 0, 900), ("res_vegetables", "Vegetables", 1600, 1450, 0, 0),
       ("res_livestock", "Livestock", 1100, 900, 120, 0), ("res_cotton", "Cotton", 800, 0, 0, 640),
       ("res_wood", "Wood", 2600, 1200, 0, 1100), ("res_ore", "Ore", 1800, 1500, 0, 100),
       ("res_oil", "Oil", 1400, 1650, 380, 0), ("res_stone", "Stone", 900, 700, 0, 0),
       ("res_timber", "Timber", 1200, 1100, 0, 0), ("res_metals", "Metals", 900, 1250, 350, 0),
       ("res_food", "Food", 1700, 2100, 420, 0), ("res_electronics", "Electronics", 600, 380, 0, 150),
       ("res_textiles", "Textiles", 520, 450, 0, 0), ("res_plastics", "Plastics", 300, 410, 120, 0)]
PROD_COLS = ["Resource", "Produced", "Consumed", "In", "Out"]


@reg("production-trade", "Production & trade", 372, 276, sort=1)
@reg("production-trade-sorted", "Production & trade: sorted by imports", 372, 276, sort=3)
def production(c, T, x, y, sort=1, family="finance"):
    s = T.s
    w, h = s(372), s(276)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Production & Trade", family, None, 0)
    ramp, body, acc = fam(family)
    n = isz(T)
    # filter strip
    dropdown(c, T, cx, cy, s(100), "All resources", family)
    checkbox(c, T, cx + s(110), cy + s(1), "Hide unused", family, False)
    c.text(cx, cy + s(2), "36 resources", T.f_small, R(ramp, 2), align="right", w=cw)
    ty = cy + s(18)
    # sortable header row
    sbw = s(10)
    lw = cw - sbw - s(1)
    iw = lw - 2 * T.b
    col_icon, col_name, col_io = n + s(6), s(62), s(40)
    flex = (iw - col_icon - col_name - 2 * col_io) // 2
    xs = [0, col_icon, col_icon + col_name, col_icon + col_name + flex, col_icon + col_name + 2 * flex,
          col_icon + col_name + 2 * flex + col_io, iw]
    hh = s(14)
    order = rows = sorted(RES, key=lambda r: {0: r[1], 1: -r[2], 2: -r[3], 3: -r[4], 4: -r[5]}[sort])
    well(c, T, cx, ty, lw, hh + len(order[:12]) * s(15) + 2 * T.b, family)
    hx = cx + T.b
    for i, (a, b) in enumerate(zip([xs[0], xs[2], xs[3], xs[4], xs[5]], [xs[2], xs[3], xs[4], xs[5], xs[6]])):
        active = (i == sort)
        bevel(c, hx + a, ty + T.b, b - a, hh, ramp, "in" if active else "out", body - 1 if active else body, T.b, 7, 1)
        lab = PROD_COLS[i]
        tx = hx + a + s(4)
        c.text(tx, ty + T.b + (hh - cap(T, T.f_small)) // 2, lab, T.f_small, INK)
        if active:
            arrow(c, hx + b - s(8), ty + T.b + hh // 2 - s(1), max(2, s(3)), "up" if sort == 0 else "down", INK)
    ry = ty + T.b + hh
    rh = s(15)
    nvis = min(len(order), 12)
    smax = 4000.0
    for i, (k, nm, pr, co, im, ex) in enumerate(order[:nvis]):
        if i % 2:
            c.fill(hx, ry, iw, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        if i == 2:
            c.fill(hx, ry, iw, rh, R(ramp, 5))
        c.blit(ic(k, n), hx + s(2), ry + (rh - n) // 2)
        c.text(hx + xs[1] + s(2), ry + (rh - cap(T)) // 2, nm, T.f_label, INK)
        mh = s(9)
        vtw = s(30)
        meter(c, T, hx + xs[2] + s(2), ry + (rh - mh) // 2, flex - s(8) - vtw, pr / smax, family, "green", h=mh)
        c.text(hx + xs[2], ry + (rh - cap(T)) // 2, "{:,}".format(pr), T.f_label, INK, align="right", w=flex - s(3))
        short = co > pr
        meter(c, T, hx + xs[3] + s(2), ry + (rh - mh) // 2, flex - s(8) - vtw, co / smax, family, "red" if short else "yellow", h=mh)
        c.text(hx + xs[3], ry + (rh - cap(T)) // 2, "{:,}".format(co), T.f_label, MONEY_NEG if short else INK, align="right", w=flex - s(3))
        for col_i, (val, d, colr) in enumerate(((im, "down", hexc("#2a5aa8")), (ex, "up", hexc("#b45c12")))):
            bx = hx + xs[4 + col_i]
            if val:
                a = max(2, s(3))
                arrow(c, bx + s(5), ry + rh // 2 - a // 2 - (0 if d == "up" else 0), a, d, colr)
                c.text(bx + s(10), ry + (rh - cap(T)) // 2, "{:,}".format(val), T.f_label, colr)
            else:
                c.text(bx + s(10), ry + (rh - cap(T)) // 2, "-", T.f_label, R(ramp, 3))
        ry += rh
    scrollbar(c, T, cx + lw + s(1), ty, hh + nvis * rh + 2 * T.b, family, 0.0, nvis / 36.0)
    ty += hh + nvis * rh + 2 * T.b + s(5)
    # summary strip
    ty += header(c, T, cx, ty, cw, "Trade this month", family)
    for lab, val, colr, col in (("Imports", "-$4,240", MONEY_NEG, 0), ("Exports", "+$6,740", MONEY_POS, 1)):
        rx = cx + col * (cw // 2 + s(2))
        value_row(c, T, rx, ty, cw // 2 - s(6), lab, val, colr)
    ty += s(14)
    value_row(c, T, cx, ty, cw // 2 - s(6), "Trade balance", "+$2,500", MONEY_POS, face=T.f_value)
    value_row(c, T, cx + cw // 2 + s(2), ty, cw // 2 - s(6), "Shortages", "3", MONEY_NEG)
    return w, h


# ---- 4. City info dashboard ------------------------------------------------------------------------
AGES = [("Senior", 4400, "orange"), ("Adult", 28300, "blue"), ("Teen", 6100, "green"), ("Child", 9400, "yellow")]
EDU = [("Uneducated", 0.18, "orange"), ("Poorly edu.", 0.27, "yellow"), ("Educated", 0.31, "green"),
       ("Well edu.", 0.17, "blue"), ("Highly edu.", 0.07, "purple")]
HAPPY = [("Healthcare", 7), ("Parks", 6), ("Education", 4), ("Safety", 3), ("Entertainment", 2),
         ("Taxes", -4), ("Pollution", -7), ("Traffic", -5), ("Noise", -3), ("Garbage", -2)]


@reg("city-info", "City info dashboard", 360, 284)
def city_info(c, T, x, y, family="city"):
    s = T.s
    w, h = s(360), s(284)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "City Info - Oakridge", family, None, 0)
    ramp, body, acc = fam(family)
    n = isz(T)
    big = 24 if T.u < 1.25 else 32
    # level + XP strip ------------------------------------------------------------------------------
    sh = s(34)
    bevel(c, cx, cy, cw, sh, ramp, "in", 6, T.b, 7, 2)
    c.blit(ic("info_level", big), cx + s(4), cy + (sh - big) // 2)
    tx = cx + s(8) + big
    c.text(tx, cy + s(4), "Large Town", T.f_big, INK)
    c.text(tx, cy + s(4) + cap(T, T.f_big) + s(4), "Level 4 of 20", T.f_small, R(ramp, 2))
    bx = cx + s(134)
    bw = cw - s(134) - s(6)
    c.text(bx, cy + s(4), "XP", T.f_label, INK)
    c.text(bx, cy + s(4), "12,480 / 20,000", T.f_value, INK, align="right", w=bw)
    meter(c, T, bx, cy + s(4) + cap(T) + s(3), bw, 12480 / 20000.0, family, "yellow", h=s(9))
    c.text(bx, cy + s(4) + cap(T) + s(15), "Next: Small City at 25,000 pop.", T.f_small, R(ramp, 2))
    ty0 = cy + sh + s(6)
    colw = (cw - s(8)) // 2
    lx, rx = cx, cx + colw + s(8)
    # left: population pyramid ---------------------------------------------------------------------
    ty = ty0
    ty += header(c, T, lx, ty, colw, "Population 48,200", family)
    rh = s(14)
    ph = len(AGES) * rh + 2 * T.b
    well(c, T, lx, ty, colw, ph, family)
    labw, numw = s(36), s(34)
    barw = colw - labw - numw - 2 * T.b
    mx = 28300.0
    for i, (nm, v, rp) in enumerate(AGES):
        ry = ty + T.b + i * rh
        c.text(lx + s(3), ry + (rh - cap(T, T.f_small)) // 2, nm, T.f_small, INK)
        bwid = max(s(4), int(barw * v / mx))
        bxx = lx + T.b + labw + (barw - bwid) // 2
        c.fill(bxx, ry + s(2), bwid, rh - s(4), R(rp, 5))
        c.fill(bxx, ry + s(2), bwid, T.b, R(rp, 7))
        c.fill(bxx, ry + rh - s(2) - T.b, bwid, T.b, R(rp, 3))
        c.fill(bxx, ry + s(2), T.b, rh - s(4), R(rp, 7))
        c.fill(bxx + bwid - T.b, ry + s(2), T.b, rh - s(4), R(rp, 3))
        c.text(lx, ry + (rh - cap(T)) // 2, "{:,}".format(v), T.f_label, INK, align="right", w=colw - s(4))
    # centre axis tick
    ty += ph + s(5)
    # left: education -------------------------------------------------------------------------------
    ty += header(c, T, lx, ty, colw, "Education levels", family)
    rh2 = s(13)
    eh = len(EDU) * rh2 + 2 * T.b
    well(c, T, lx, ty, colw, eh, family)
    for i, (nm, f, rp) in enumerate(EDU):
        ry = ty + T.b + i * rh2
        if i % 2:
            c.fill(lx + T.b, ry, colw - 2 * T.b, rh2, mix(R(ramp, 7), R(ramp, 6), 0.55))
        c.text(lx + s(3), ry + (rh2 - cap(T, T.f_small)) // 2, nm, T.f_small, INK)
        meter(c, T, lx + s(52), ry + (rh2 - s(7)) // 2, colw - s(52) - s(28), f / 0.35, family, rp, h=s(7), segmented=False)
        c.text(lx, ry + (rh2 - cap(T, T.f_small)) // 2, "%d%%" % round(f * 100), T.f_small, INK, align="right", w=colw - s(4))
    ty += eh + s(5)
    # left: households ------------------------------------------------------------------------------
    ty += header(c, T, lx, ty, colw, "Households", family)
    for lab, val, colr in (("Households", "17,900", INK), ("Avg. size", "2.7", INK), ("Students", "9,100", INK), ("Homeless", "140", MONEY_NEG)):
        value_row(c, T, lx, ty, colw, lab, val, colr)
        ty += s(12)
    # right: employment -----------------------------------------------------------------------------
    ty = ty0
    ty += header(c, T, rx, ty, colw, "Employment", family)
    eh2 = s(13)
    for lab, v, val, rp in (("Jobs", 1.0, "23,800", "blue"), ("Workers", 21400 / 23800.0, "21,400", "green"),
                            ("Unemployed", 2100 / 23800.0, "2,100", "red")):
        c.text(rx, ty + s(2), lab, T.f_label, INK)
        meter(c, T, rx + s(58), ty + s(1), colw - s(58) - s(40), v, family, rp, h=s(9))
        c.text(rx, ty + s(2), val, T.f_value, INK, align="right", w=colw)
        ty += eh2
    ty += s(2)
    cxp = rx
    for lab, colr in (("Unemployed 8.8%", "orange"), ("Vacant 2,400", "blue")):
        cwid, _ = chip(c, T, cxp, ty, lab, colr)
        cxp += cwid + s(5)
    ty += s(17)
    # right: happiness ------------------------------------------------------------------------------
    ty += header(c, T, rx, ty, colw, "Happiness 74%", family)
    meter(c, T, rx, ty - s(1), colw, 0.74, family, "green", h=s(9))
    ty += s(12)
    rh3 = s(11)
    hh = len(HAPPY) * rh3 + 2 * T.b
    well(c, T, rx, ty, colw, hh, family)
    for i, (nm, v) in enumerate(HAPPY):
        ry = ty + T.b + i * rh3
        if i % 2:
            c.fill(rx + T.b, ry, colw - 2 * T.b, rh3, mix(R(ramp, 7), R(ramp, 6), 0.55))
        colr = MONEY_POS if v > 0 else MONEY_NEG
        a = max(2, s(3))
        arrow(c, rx + s(7), ry + rh3 // 2 - a // 2 + (0 if v > 0 else 0), a, "up" if v > 0 else "down", colr)
        c.text(rx + s(14), ry + (rh3 - cap(T, T.f_small)) // 2, nm, T.f_small, INK)
        c.text(rx, ry + (rh3 - cap(T)) // 2, "%+d" % v, T.f_value, colr, align="right", w=colw - s(4))
    return w, h
