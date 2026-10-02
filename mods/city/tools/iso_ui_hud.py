"""
iso_ui_hud - RCT2-style HUD for a CS2 game: top toolbar (game/speed | build | panels), bottom status bar
(money, balance, population, happiness | news ticker + alerts | date, weather, RCI demand), world overlays.

Toolbar tiers by logical width (screen px / UI scale): wide >= 1600 (every build category), medium >= 1100
(build categories grouped), compact < 1100 (panels also grouped behind "More").
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG, MONEY_POS_L, MONEY_NEG_L
from iso_ui_chrome import Theme, bevel, fam, icon_button, etched_v
from iso_ui_widgets2 import meter, rci, tooltip
from iso_ui_icons import ic
import iso_ui_font as F

LEFT = [["pnl_game_menu", "pnl_save", "pnl_settings"], ["time_pause", "time_play", "time_fast", "time_fastest"],
        ["ui_zoom_out", "ui_zoom_in", "pnl_photo"]]
BUILD_WIDE = [["cat_roads", "cat_zoning", "cat_districts", "cat_landscaping"],
              ["cat_networks", "cat_power", "cat_water", "cat_garbage"],
              ["cat_police", "cat_fire", "cat_health", "cat_deathcare", "cat_education", "cat_parks", "cat_comms", "cat_admin"],
              ["cat_transit", "cat_industry", "cat_signature"], ["tool_bulldoze"]]
BUILD_GROUPED = [["cat_roads", "cat_zoning", "cat_power", "cat_health", "cat_parks", "cat_transit", "cat_industry",
                  "cat_signature", "cat_landscaping"], ["tool_bulldoze"]]
RIGHT_FULL = [["pnl_infoviews", "pnl_budget", "pnl_stats", "pnl_policies", "pnl_districts", "pnl_progression",
               "pnl_transit_lines", "pnl_production", "pnl_tiles"], ["pnl_chirper", "pnl_advisor", "pnl_achievements", "pnl_notifications"]]
RIGHT_COMPACT = [["pnl_infoviews", "pnl_budget", "pnl_stats", "pnl_policies", "pnl_progression", "pnl_transit_lines"],
                 ["pnl_chirper", "pnl_notifications", "ui_list"]]
TB_FAM = {"left": "city", "build": "zoning", "right": "info"}


def tier(width_px, u):
    lw = width_px / u
    return "wide" if lw >= 1600 else "medium" if lw >= 1100 else "compact"


def tb_icon_size(T):
    return 24 if T.u < 1.25 else 36 if T.u < 1.75 else 48


def strip(c, T, x, y, names, family, active=(), bw=None):
    """One toolbar group: outset frame with flat icon buttons; active ones pressed in. Returns width."""
    ramp, body, _ = fam(family)
    bw = bw or T.s(30)
    bh = T.s(28)
    w = len(names) * bw + 2 * T.b
    c.fill(x - T.b, y - T.b, w + 2 * T.b, bh + 3 * T.b, R(ramp, 0))
    bevel(c, x, y, w, bh + T.b, ramp, "out", body - 1, T.b, 6, 1)
    n = tb_icon_size(T)
    for i, nm in enumerate(names):
        bx = x + T.b + i * bw
        st = "toggled" if nm in active else "normal"
        icon_button(c, T, bx, y + T.b, bw, bh - T.b, ic(nm, n), family, st, flat=True, body=body)
    return w


def toolbar(c, T, W, active=(), mode=None):
    mode = mode or tier(W, T.u)
    gap, ggap = T.s(2), T.s(8)
    y = 0
    build = BUILD_WIDE if mode == "wide" else BUILD_GROUPED
    right = RIGHT_FULL if mode != "compact" else RIGHT_COMPACT
    left = LEFT if mode != "compact" else [LEFT[0], LEFT[1]]
    x = 0
    for g in left:
        x += strip(c, T, x, y, g, TB_FAM["left"], active) + gap
    # right groups are right-aligned
    rw = sum(len(g) * T.s(30) + 2 * T.b for g in right) + gap * (len(right) - 1)
    rx = W - rw
    for g in right:
        rx += strip(c, T, rx, y, g, TB_FAM["right"], active) + gap
    # build groups centred between left and right
    bwid = sum(len(g) * T.s(30) + 2 * T.b for g in build) + gap * (len(build) - 1)
    bx = x + ggap + max(0, ((W - rw - ggap) - (x + ggap) - bwid) // 2)
    for g in build:
        bx += strip(c, T, bx, y, g, TB_FAM["build"], active) + gap
    return T.s(28) + 2 * T.b


def statusbar(c, T, W, H, money=1254300, balance=12400, pop=48213, happy=0.76, msg=None, alerts=None,
              date="Mar 14, 2031", time="14:20", temp="18°C", weather="wx_partly", demand=(0.8, 0.35, -0.3, 0.55)):
    s = T.s
    h = s(38)
    y = H - h
    fam_ = "city"
    ramp, body, _ = fam(fam_)
    c.fill(0, y - T.b, W, h + T.b, R(ramp, 0))
    n = 16 if T.u < 1.25 else 24
    # left: money / balance / population / happiness
    lw = s(250)
    bevel(c, 0, y, lw, h, ramp, "out", body, T.b, 7, 1)
    r1, r2 = y + s(6), y + s(22)
    c.blit(ic("stat_money", n), s(5), r1 - s(3))
    mc = MONEY_POS if money >= 0 else MONEY_NEG
    c.text(s(5) + n + s(4), r1, "${:,}".format(money), T.f_value, mc)
    c.text(s(5) + n + s(4) + 1, r1, "${:,}".format(money), T.f_value, mc)
    c.blit(ic("stat_balance_up" if balance >= 0 else "stat_balance_down", n), s(5), r2 - s(3))
    c.text(s(5) + n + s(4), r2, ("+" if balance >= 0 else "-") + "${:,} / month".format(abs(balance)), T.f_label,
           MONEY_POS if balance >= 0 else MONEY_NEG)
    px = s(132)
    etched_v(c, T, px - s(6), y + s(4), h - s(8), ramp)
    c.blit(ic("stat_population", n), px, r1 - s(3))
    c.text(px + n + s(4), r1, "{:,}".format(pop), T.f_value, INK)
    c.text(px + n + s(4) + 1, r1, "{:,}".format(pop), T.f_value, INK)
    c.text(px + n + s(4) + F.measure("{:,}".format(pop), T.f_value) + s(5), r1, "▲", T.f_small, MONEY_POS)
    c.blit(ic("stat_happiness", n), px, r2 - s(3))
    meter(c, T, px + n + s(4), r2, lw - px - n - s(10), happy, fam_, "green", h=s(8))
    # right: date/time/weather + RCI
    rw = s(250)
    rx = W - rw
    bevel(c, rx, y, rw, h, ramp, "out", body, T.b, 7, 1)
    c.blit(ic("time_calendar", n), rx + s(6), r1 - s(3))
    c.text(rx + s(10) + n, r1, date, T.f_value, INK)
    c.blit(ic("time_clock", n), rx + s(6), r2 - s(3))
    c.text(rx + s(10) + n, r2, time, T.f_label, INK)
    c.blit(ic(weather, n), rx + s(66), r2 - s(3))
    c.text(rx + s(70) + n, r2, temp, T.f_label, INK)
    rci_w = 4 * (s(7) + s(3)) + s(3)
    rci(c, T, rx + rw - rci_w - s(6), y + s(3), h - s(5), list(demand), family=fam_)
    c.text(rx + rw - rci_w - s(40), r1, "Demand", T.f_small, INK)
    # centre: ticker + alert chips
    cx, cw = lw + T.s(2), W - lw - rw - T.s(4)
    bevel(c, cx, y, cw, h, ramp, "out", body, T.b, 7, 1)
    tx, ty, tw, th = cx + s(4), y + s(4), cw - s(8), s(15)
    bevel(c, tx, ty, tw, th, ramp, "in", 1, T.b, 5, 0)
    c.blit(ic("pnl_chirper", n), tx + s(3), ty + (th - n) // 2)
    c.text(tx + n + s(7), ty + (th - F.cap_height(T.f_label)) // 2,
           msg or "@MayorBot: Traffic on Elm Avenue cleared, buses back on time!", T.f_label, CREAM)
    ax = tx
    for nm, count in (alerts or [("st_no_power", 3), ("st_garbage", 12), ("st_traffic", 5), ("st_fire", 1)]):
        icn = ic(nm, n)
        c.blit(icn, ax, ty + th + s(2) + (s(15) - n) // 2)
        c.text(ax + n + s(2), ty + th + s(6), str(count), T.f_label, INK)
        ax += n + s(4) + F.measure(str(count), T.f_label) + s(6)
    return h


def cursor(c, x, y, k=1):
    """Placeholder RCT2-like arrow pointer (LIFE owns real cursors)."""
    shape = ["X", "XX", "XWX", "XWWX", "XWWWX", "XWWWWX", "XWWWWWX", "XWWWWWWX", "XWWWWXXXX", "XWXWWX", "XX XWWX",
             "X  XWWX", "    XWWX", "    XWWX", "     XX"]
    for j, row in enumerate(shape):
        for i, ch in enumerate(row):
            if ch == "X":
                c.fill(x + i * k, y + j * k, k, k, hexc("#000000"))
            elif ch == "W":
                c.fill(x + i * k, y + j * k, k, k, hexc("#ffffff"))


def select_diamond(c, cx, cy, fw, fh, t=1.0, colr=None):
    """Selection marker on the ground: dashed yellow footprint outline (cells cx..cx+fw)."""
    colr = colr or hexc("#ffe040")
    S = lambda x, y: (int(c.w // 2 + (x - y) * 32 * t), int(-c.h // 2 + (x + y) * 16 * t))
    return S
