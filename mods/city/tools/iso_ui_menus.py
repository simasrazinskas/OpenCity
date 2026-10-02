"""
iso_ui_menus - main menu, new city, settings, pause, save/load, small dialogs, loading screen, tooltip styles.

Windows register via iso_ui_panels_a.reg (rendered standalone); full-screen mockups register via
iso_ui_screens.scr. All sizes through T.s(), fonts through T.f_*. Design phase only.
"""

import numpy as np

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG, MONEY_POS_L, MONEY_NEG_L
from iso_ui_chrome import Theme, window, button, icon_button, bevel, fam, etched_h, etched_v
from iso_ui_widgets import well, checkbox, radio, slider, list_rows, scrollbar, dropdown, textinput, arrow
from iso_ui_widgets2 import meter, graph, value_row, groupbox, money, tooltip
from iso_ui_parts import thumb, card, header, chip
from iso_ui_icons import ic
from iso_ui_panels_a import reg
from iso_ui_screens import scr, world
import iso_ui_world as WD
import iso_ui_font as F
import iso_ui_hud as H


# ---- small helpers -----------------------------------------------------------------------------------
def isz(T):
    return 16 if T.u < 1.25 else 24


def lab(c, T, x, y, h, text, colr=INK, face=None):
    """Label vertically centred in a control row of height h."""
    face = face or T.f_label
    return c.text(x, y + (h - F.cap_height(face)) // 2, text, face, colr)


def keycap(c, T, x, y, text, w=None):
    """RCT2-ish key cap chip: light raised keycap with a darker lip."""
    h = T.s(12)
    w = w or F.measure(text, T.f_small) + T.s(8)
    c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b + T.b, R("grey", 1))
    bevel(c, x, y, w, h, "grey", "out", 7, T.b, 7, 3)
    c.fill(x + T.b, y + h - 2 * T.b, w - 2 * T.b, T.b, R("grey", 5))
    c.text(x, y + (h - F.cap_height(T.f_small)) // 2 - T.b // 2, text, T.f_small, INK, align="center", w=w)
    return w


def dim(c, amount=0.55, tint="#0a0806"):
    """Darken a whole canvas (pause menu / modal backdrop)."""
    a = c.a[:, :, :3].astype(np.float32)
    t = np.array(hexc(tint)[:3], np.float32)
    c.a[:, :, :3] = (a * (1 - amount) + t * amount).astype(np.uint8)


def vgrad(c, x, y, w, h, top, bot):
    for i in range(h):
        c.fill(x, y + i, w, 1, mix(top, bot, i / max(1, h - 1)))


def btn_row(c, T, x, y, w, labels, family, gap=None, bw=None, bh=None, default=0, states=None):
    """Right-aligned row of buttons ending at x + w. Returns total width."""
    gap = T.s(4) if gap is None else gap
    bh = bh or T.s(16)
    widths = [bw or max(T.s(56), F.measure(t, T.f_label) + T.s(16)) for t in labels]
    tot = sum(widths) + gap * (len(labels) - 1)
    bx = x + w - tot
    for i, (t, bwid) in enumerate(zip(labels, widths)):
        st = (states[i] if states else None) or ("default" if i == default else "normal")
        button(c, T, bx, y, bwid, bh, t, family, st)
        bx += bwid + gap
    return tot


def footer_y(T, y, h):
    return y + h - T.s(24)


# ---- settings window (family city) ---------------------------------------------------------------------
SET_TABS = ["ui_monitor", "pnl_settings", "ui_speaker", "ui_keyboard", "pnl_game_menu"]
SET_NAMES = ["Display", "Interface", "Audio", "Input", "Gameplay"]


def tick_slider(c, T, x, y, w, family, value, labels, vtext=None):
    """Slider with tick marks + tick labels aligned to the thumb centre positions. Returns height used."""
    s = T.s
    slider(c, T, x, y, w, family, value, value_text=None)
    tw = s(8)
    n = len(labels)
    for i, t in enumerate(labels):
        cxp = x + tw // 2 + int((w - tw) * i / (n - 1))
        c.fill(cxp, y + s(12), T.b, s(3), R(fam(family)[0], 2))
        c.text(cxp - s(15), y + s(16), t, T.f_small, INK, align="center", w=s(30))
    return s(16) + F.cap_height(T.f_small)


def scale_preview(c, T, x, y, w, h, pct, family="city"):
    """Live preview well: a sample button + icon + text rendered at the chosen UI scale."""
    well(c, T, x, y, w, h, family, 4)
    inner = Canvas(w - 2 * T.b, h - 2 * T.b, R(fam(family)[0], 4))
    pu = max(0.5, T.u * pct / 100.0) * 0.8
    P = Theme(pu)
    pw, ph = P.s(52), P.s(16)
    bx, by = max(2, (inner.w - pw) // 2), max(2, inner.h // 2 - ph + 2)
    button(inner, P, bx, by, pw, ph, "Button", family, "default", icon=ic("pnl_save", 16 if pu < 1.25 else 24))
    inner.text(max(2, (inner.w - F.measure("Sample text", P.f_label)) // 2), by + ph + P.s(5), "Sample text", P.f_label, INK)
    c.blit(inner, x + T.b, y + T.b)


def settings_window(c, T, x, y, tab=0, family="city", open_dd=False):
    s = T.s
    w, h = s(420), s(300)
    tabs = [ic(n, T.icon) for n in SET_TABS]
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Settings: " + SET_NAMES[tab], family, tabs, tab)
    rh = s(18)
    lw = s(112)
    fx = cx + lw
    fw = cw - lw - s(2)
    ty = cy + s(2)
    if tab == 0:
        ty += header(c, T, cx, ty, cw, "Video", family)
        lab(c, T, cx, ty, s(13), "Window mode")
        dropdown(c, T, fx, ty, s(150), "Borderless fullscreen", family)
        ty += rh
        lab(c, T, cx, ty, s(13), "Resolution")
        res_y = ty
        dropdown(c, T, fx, ty, s(150), "1920 x 1080 (16:9)", family)
        ty += rh
        checkbox(c, T, cx, ty + s(1), "Vertical sync", family, True)
        checkbox(c, T, fx + s(40), ty + s(1), "Frame limiter", family, True)
        ty += rh
        lab(c, T, cx, ty, s(12), "Max frame rate")
        slider(c, T, fx, ty, s(150), family, 0.6, value_text="144 FPS")
        ty += rh + s(2)
        ty += header(c, T, cx, ty, cw, "Interface scale", family)
        lab(c, T, cx, ty, s(12), "UI scale")
        sw = fw - s(52)
        slider(c, T, fx, ty, sw, family, 0.4)
        c.text(fx + sw + s(8), ty + (s(12) - F.cap_height(T.f_big)) // 2, "150%", T.f_big, INK)
        ty += s(14)
        tick_slider_labels = ["50%", "100%", "150%", "200%", "250%", "300%"]
        tw = s(8)
        for i, t in enumerate(tick_slider_labels):
            cxp = fx + tw // 2 + int((sw - tw) * i / 5)
            c.fill(cxp, ty, T.b, s(3), R(fam(family)[0], 2))
            c.text(cxp - s(15), ty + s(5), t, T.f_small, MONEY_POS if i == 2 else INK, align="center", w=s(30))
        ty += s(18)
        lab(c, T, cx, ty, s(10), "Preview")
        scale_preview(c, T, fx, ty, s(120), s(48), 150, family)
        c.text(fx + s(128), ty + s(4), "Buttons, text and icons", T.f_small, INK)
        c.text(fx + s(128), ty + s(15), "scale together. Applies", T.f_small, INK)
        c.text(fx + s(128), ty + s(26), "instantly, no restart.", T.f_small, INK)
        ty += s(54)
        checkbox(c, T, cx, ty, "Pixel-perfect scaling (whole-number scales only, crisper art)", family, False)
        if open_dd:
            dropdown(c, T, fx, res_y, s(150), "1920 x 1080 (16:9)", family, "open",
                     ["2560 x 1440 (16:9)", "1920 x 1080 (16:9)", "1600 x 900 (16:9)", "1280 x 720 (16:9)", "1024 x 768 (4:3)"], 1)
    elif tab == 1:
        ty += header(c, T, cx, ty, cw, "Language and help", family)
        lab(c, T, cx, ty, s(13), "Language")
        dropdown(c, T, fx, ty, s(150), "English (US)", family)
        ty += rh
        lab(c, T, cx, ty, s(12), "Tooltip delay")
        slider(c, T, fx, ty, s(150), family, 0.3, value_text="0.4 s")
        ty += rh
        checkbox(c, T, cx, ty + s(1), "Show hotkeys in tooltips", family, True)
        ty += rh
        checkbox(c, T, cx, ty + s(1), "Show advisor tips", family, True)
        ty += rh + s(2)
        ty += header(c, T, cx, ty, cw, "Camera", family)
        checkbox(c, T, cx, ty + s(1), "Edge scrolling", family, True)
        ty += rh
        lab(c, T, cx, ty, s(12), "Scroll speed")
        slider(c, T, fx, ty, s(150), family, 0.55, value_text="Normal")
        ty += rh + s(2)
        ty += header(c, T, cx, ty, cw, "Colour-blind mode", family)
        for i, t in enumerate(["Off", "Protanopia", "Deuteranopia", "Tritanopia"]):
            radio(c, T, cx + (i % 2) * s(150), ty + (i // 2) * s(15), t, family, i == 0)
        ty += s(34)
        c.text(cx, ty, "Changes heat-map ramps and the RCI colours.", T.f_small, INK)
    elif tab == 2:
        ty += header(c, T, cx, ty, cw, "Volume", family)
        rows = [("Master", 0.8, False), ("Music", 0.45, False), ("Effects", 0.7, False), ("Ambient", 0.55, False),
                ("User interface", 0.3, True)]
        n = isz(T)
        for name, v, muted in rows:
            c.blit(ic("ui_muted" if muted else "ui_speaker", n), cx, ty - (n - s(12)) // 2)
            lab(c, T, cx + n + s(4), ty, s(12), name)
            slider(c, T, fx + s(10), ty, s(150), family, v, state="disabled" if muted else "normal",
                   value_text=("Muted" if muted else "%d%%" % round(v * 100)))
            checkbox(c, T, cx + cw - s(56), ty + s(1), "Mute", family, muted)
            ty += s(22)
        ty += s(2)
        ty += header(c, T, cx, ty, cw, "Music", family)
        lab(c, T, cx, ty, s(13), "Radio station")
        dropdown(c, T, fx, ty, s(150), "City Lights FM", family)
        ty += rh
        checkbox(c, T, cx, ty + s(1), "Mute when the window loses focus", family, True)
    elif tab == 3:
        ty += header(c, T, cx, ty, cw, "Hotkeys", family)
        rows = [("Road tool", "R"), ("Zoning tool", "Z"), ("Bulldoze", "B"), ("Info views", "I"), ("Next info view", "Shift+I"),
                ("Budget", "F"), ("Pause / resume", "Space"), ("Speed 1 / 2 / 3", "1 / 2 / 3"), ("Statistics", "G"),
                ("Chirper", "K"), ("Policies", "P")]
        rh2 = s(14)
        lh = rh2 * len(rows) + 2 * T.b
        lw2 = cw - s(14)
        well(c, T, cx, ty, lw2, lh, family)
        for i, (nm, key) in enumerate(rows):
            ry = ty + T.b + i * rh2
            if i == 4:
                c.fill(cx + T.b, ry, lw2 - 2 * T.b, rh2, R(fam(family)[0], 3))
            elif i % 2:
                c.fill(cx + T.b, ry, lw2 - 2 * T.b, rh2, mix(R(fam(family)[0], 7), R(fam(family)[0], 6), 0.55))
            lab(c, T, cx + s(5), ry, rh2, nm, WHITE if i == 4 else INK)
            kw = 0
            keys = key.split("+") if "/" not in key else [key]
            tw = sum(F.measure(k, T.f_small) + T.s(8) for k in keys) + (len(keys) - 1) * T.s(10)
            kx = cx + lw2 - s(6) - tw
            for j, k in enumerate(keys):
                if j:
                    c.text(kx - T.s(8), ry + (rh2 - F.cap_height(T.f_small)) // 2, "+", T.f_small, WHITE if i == 4 else INK)
                kx += keycap(c, T, kx, ry + s(1), k) + s(10)
        scrollbar(c, T, cx + lw2 + s(2), ty, lh, family, 0.0, 0.6)
        ty += lh + s(6)
        c.text(cx, ty + s(4), "Selected: Next info view", T.f_small, INK)
        btn_row(c, T, cx, ty, cw, ["Reset all", "Rebind..."], family, default=1, bh=s(14))
    else:
        ty += header(c, T, cx, ty, cw, "Saving", family)
        lab(c, T, cx, ty, s(13), "Autosave interval")
        dropdown(c, T, fx, ty, s(150), "Every 10 minutes", family)
        ty += rh
        lab(c, T, cx, ty, s(13), "Autosave slots")
        dropdown(c, T, fx, ty, s(150), "5 slots", family)
        ty += rh
        checkbox(c, T, cx, ty + s(1), "Pause the game after loading a city", family, True)
        ty += rh + s(2)
        ty += header(c, T, cx, ty, cw, "Notifications", family)
        n = isz(T)
        items = [("st_fire", "Fires and crime", True), ("st_no_power", "Power and water", True), ("st_traffic", "Traffic jams", False),
                 ("st_unhappy", "Citizen happiness", True), ("stat_money", "Budget and loans", True), ("pnl_chirper", "Chirper messages", False)]
        for i, (icn, t, on) in enumerate(items):
            ix = cx + (i % 2) * s(200)
            iy = ty + (i // 2) * s(20)
            checkbox(c, T, ix, iy + s(3), "", family, on)
            c.blit(ic(icn, n), ix + s(14), iy - (n - s(16)) // 2 + s(1))
            lab(c, T, ix + s(18) + n, iy, s(16), t)
        ty += s(62)
        checkbox(c, T, cx, ty, "Show disaster warnings on screen", family, True)
        ty += s(18)
        ty += header(c, T, cx, ty, cw, "Units", family)
        for i, t in enumerate(["Metric (km, C)", "Imperial (mi, F)"]):
            radio(c, T, cx + i * s(150), ty, t, family, i == 0)
    by = footer_y(T, y, h)
    btn_row(c, T, cx, by, cw, ["Apply", "Close"], family, default=0)
    lab(c, T, cx, by, s(16), "Settings are saved automatically.", R(fam(family)[0], 1), T.f_small)
    return w, h


@reg("settings-display", "Settings: Display (UI scale slider + preview)", 420, 300, tab=0)
@reg("settings-interface", "Settings: Interface", 420, 300, tab=1)
@reg("settings-audio", "Settings: Audio", 420, 300, tab=2)
@reg("settings-input", "Settings: Input (hotkeys)", 420, 300, tab=3)
@reg("settings-gameplay", "Settings: Gameplay", 420, 300, tab=4)
@reg("settings-display-dropdown", "Settings: Display, resolution dropdown open", 420, 300, tab=0, open_dd=True)
def settings(c, T, x, y, tab=0, open_dd=False):
    return settings_window(c, T, x, y, tab, "city", open_dd)


# ---- tooltip styles sheet --------------------------------------------------------------------------------
def tip_box(c, T, x, y, w, h):
    c.fill(x + T.s(2), y + T.s(2), w, h, (0, 0, 0, 255))
    c.fill(x, y, w, h, R("yellow", 0))
    bevel(c, x + T.b, y + T.b, w - 2 * T.b, h - 2 * T.b, "yellow", "out", 7, T.b, 7, 5)


def tip_title(c, T, x, y, text, colr=None):
    colr = colr or R("brown", 1)
    c.text(x, y, text, T.f_label, colr)
    c.text(x + 1, y, text, T.f_label, colr)


def tip_rich(c, T, x, y):
    s = T.s
    n = isz(T)
    w, h = s(150), s(100)
    tip_box(c, T, x, y, w, h)
    px, ty = x + s(5), y + s(5)
    tip_title(c, T, px, ty, "Residential Low")
    ty += s(13)
    c.text(px, ty, "Detached houses, 1-2 floors", T.f_small, R("brown", 3))
    ty += s(11)
    etched_h(c, T, px, ty, w - s(10), "yellow")
    ty += s(4)
    rows = [("stat_money", "Build cost", "$1,200", INK), ("stat_expenses", "Upkeep", "-$40 / month", MONEY_NEG),
            ("stat_population", "Households", "4", INK), ("info_electricity", "Needs", "Power, water", INK)]
    for icn, lb, val, col_ in rows:
        c.blit(ic(icn, n), px, ty - (n - F.cap_height(T.f_label)) // 2)
        c.text(px + n + s(3), ty, lb, T.f_label, INK)
        c.text(px, ty, val, T.f_label, col_ if col_ != INK else INK, align="right", w=w - s(10))
        ty += s(17)
    return w, h


def tip_building(c, T, x, y):
    s = T.s
    n = isz(T)
    w, h = s(150), s(106)
    tip_box(c, T, x, y, w, h)
    px, ty = x + s(5), y + s(5)
    tip_title(c, T, px, ty, "St. Mary Hospital")
    c.text(px, ty, "Level 2", T.f_small, R("brown", 3), align="right", w=w - s(10))
    ty += s(14)
    c.text(px, ty, "Patients", T.f_label, INK)
    meter(c, T, px + s(48), ty - s(1), w - s(10) - s(48) - s(48), 0.72, "tooltip", "green")
    c.text(px, ty, "115 / 160", T.f_label, INK, align="right", w=w - s(10))
    ty += s(13)
    c.text(px, ty, "Workers", T.f_label, INK)
    meter(c, T, px + s(48), ty - s(1), w - s(10) - s(48) - s(48), 0.9, "tooltip", "blue")
    c.text(px, ty, "43 / 48", T.f_label, INK, align="right", w=w - s(10))
    ty += s(14)
    etched_h(c, T, px, ty, w - s(10), "yellow")
    ty += s(4)
    for icn, t_, col_ in (("st_traffic", "Traffic jam at entrance", MONEY_NEG), ("st_no_power", "Low electricity (-6%)", R("orange", 3))):
        c.blit(ic(icn, n), px, ty - (n - F.cap_height(T.f_label)) // 2)
        c.text(px + n + s(3), ty, t_, T.f_label, col_)
        ty += s(17)
    return w, h


def tip_locked(c, T, x, y):
    s = T.s
    n = isz(T)
    w, h = s(140), s(66)
    tip_box(c, T, x, y, w, h)
    px, ty = x + s(5), y + s(5)
    c.blit(ic("ui_lock", n), px, ty - (n - F.cap_height(T.f_label)) // 2)
    tip_title(c, T, px + n + s(3), ty, "University", R("brown", 3))
    ty += s(14)
    c.text(px, ty, "Unlocks at Large Town", T.f_label, MONEY_NEG)
    ty += s(13)
    c.text(px, ty, "Population 10,000", T.f_small, INK)
    ty += s(11)
    meter(c, T, px, ty, w - s(10), 0.74, "tooltip", "yellow", h=s(11), label="7,420 / 10,000")
    return w, h


def tip_hotkey(c, T, x, y):
    s = T.s
    w, h = s(120), s(34)
    tip_box(c, T, x, y, w, h)
    px, ty = x + s(5), y + s(5)
    tip_title(c, T, px, ty, "Bulldoze")
    kw = keycap(c, T, x + w - s(5) - (F.measure("B", T.f_small) + s(8)), ty - s(2), "B")
    c.text(px, ty + s(13), "Remove buildings and roads", T.f_small, INK)
    return w, h


@reg("tooltips", "Tooltip styles sheet", 520, 250)
def tooltip_sheet(c, T, x, y):
    s = T.s
    w, h = s(520), s(236)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Tooltip styles", "city")
    col2 = cx + s(170)
    col3 = cx + s(340)
    def cap(px, py, t):
        c.text(px, py, t, T.f_small, R("brown", 1))
    cap(cx, cy + s(2), "1. Simple text")
    tooltip(c, T, cx, cy + s(14), ["Open the zoning tool"], title=None)
    tooltip(c, T, cx, cy + s(38), ["Budget (F)"], title=None)
    cap(cx, cy + s(66), "2. Hotkey")
    tip_hotkey(c, T, cx, cy + s(80))
    cap(cx, cy + s(126), "3. Disabled reason")
    tip_locked(c, T, cx, cy + s(140))
    cap(col2, cy + s(2), "4. Rich (title, icon rows, money)")
    tip_rich(c, T, col2, cy + s(14))
    cap(col3, cy + s(2), "5. Building hover (status)")
    tip_building(c, T, col3, cy + s(14))
    cap(col2, cy + s(122), "Anatomy")
    notes = ["Cream box, dark outline, 2 px hard shadow.", "Title in bold brown, rows dark ink.",
             "Money: green gain, red cost. Problems in red.", "Appears after the tooltip delay (Settings)."]
    for i, t in enumerate(notes):
        c.text(col2, cy + s(136) + i * s(11), t, T.f_small, INK)
    return w, h


# ---- small dialogs (family system) -------------------------------------------------------------------------
def wrap(text, face, maxw):
    lines, cur = [], ""
    for wd in text.split(" "):
        t = (cur + " " + wd).strip()
        if F.measure(t, face) <= maxw or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = wd
    lines.append(cur)
    return lines


def dialog(c, T, x, y, title, icon, msg, buttons, w=300, sub=None, tint=None, default=0, h=None, extra=None):
    """Standard dialog: big icon well on the left, wrapped message, right-aligned buttons."""
    s = T.s
    isz_ = s(32)
    tw = s(w) - s(8) - isz_ - s(18) - s(2 * 4)
    lines = wrap(msg, T.f_label, tw)
    lh = T.line(T.f_label) - s(1)
    body_h = max(isz_ + s(12), len(lines) * lh + (s(14) if sub else 0) + s(6))
    hh = s(h) if h else T.title_h + 2 * T.b + body_h + s(8) + s(16) + s(14) + (s(14) if extra else 0)
    cx, cy, cw, ch = window(c, T, x, y, s(w), hh, title, "system", None, 0, grip=False)
    wx, wy = cx, cy + s(2)
    box = isz_ + s(10)
    if tint:
        bevel(c, wx, wy, box, box, tint, "in", 3, T.b, 6, 1)
    else:
        well(c, T, wx, wy, box, box, "system", 5)
    c.blit(ic(icon, isz_), wx + s(5), wy + s(5))
    tx = wx + box + s(8)
    ty = wy + s(2)
    for i, ln in enumerate(wrap(msg, T.f_label, cw - box - s(10))):
        c.text(tx, ty + i * lh, ln, T.f_label, INK)
    if sub:
        n_l = len(wrap(msg, T.f_label, cw - box - s(10)))
        c.text(tx, ty + n_l * lh + s(3), sub[0], T.f_label, sub[1])
    by = y + hh - s(24)
    if extra:
        extra(c, T, cx, by - s(16), cw)
    btn_row(c, T, cx, by, cw, buttons, "system", default=default, bw=s(64))
    return s(w), hh


@reg("dialog-confirm", "Dialog: confirm bulldoze", 300, 110)
def dlg_confirm(c, T, x, y):
    return dialog(c, T, x, y, "Bulldoze Hospital?", "ui_warning",
                  "Bulldoze St. Mary Hospital? You will get $11,000 back.", ["Yes", "No"], 300,
                  sub=("Refund: +$11,000", MONEY_POS), default=1)


@reg("dialog-error", "Dialog: error", 300, 110)
def dlg_error(c, T, x, y):
    return dialog(c, T, x, y, "Cannot build", "ui_error",
                  "Not enough money. This building costs $22,000 but you only have $14,300.", ["OK"], 300,
                  tint="red", sub=("Missing: -$7,700", MONEY_NEG))


@reg("dialog-info", "Dialog: info message", 300, 110)
def dlg_info(c, T, x, y):
    return dialog(c, T, x, y, "Autosave", "ui_info", "City saved as 'Autosave 3'. Next autosave in 10 minutes.", ["OK"], 300)


@reg("dialog-unsaved", "Dialog: unsaved changes (3 buttons)", 340, 110)
def dlg_unsaved(c, T, x, y):
    return dialog(c, T, x, y, "Exit to main menu?", "ui_warning",
                  "You have unsaved changes since 'Autosave 3'. Save before leaving?", ["Save", "Don't save", "Cancel"], 340, default=0)


@reg("dialog-overwrite", "Dialog: overwrite save", 320, 110)
def dlg_overwrite(c, T, x, y):
    return dialog(c, T, x, y, "Overwrite save?", "ui_warning",
                  "A save named 'Greenvale' already exists. Overwrite it?", ["Overwrite", "Cancel"], 320, default=1)


# ---- save / load dialogs (family system) ---------------------------------------------------------------------
_SCENE = {}


def scene_crop(w, h, seed, t=0.5, lit=False, ox=None):
    k = (w, h, seed, t, lit)
    if k not in _SCENE:
        _SCENE[k] = WD.scene(w, h, seed=seed, t=t, lit=lit, ox=w // 2 + (seed * 37) % 60 - 30, oy=h // 2 - int((38 + seed) * 16 * t))
    return _SCENE[k]


SAVES = [("Greenvale", "Large Town", 48213, 1254300, "Mar 14, 2031", "12 h 40 m", "Today 14:20", 3, False),
         ("Autosave 3", "Large Town", 47890, 1190220, "Mar 02, 2031", "12 h 22 m", "Today 14:10", 3, False),
         ("Riverbend Test", "Town", 12054, 88400, "Jul 21, 2024", "3 h 05 m", "Sep 28, 21:02", 5, False),
         ("Lakeside", "Small City", 24310, -12500, "Nov 09, 2027", "6 h 48 m", "Sep 21, 19:37", 7, True),
         ("Sandbox Metro", "Metropolis", 212004, 9999999, "Jan 01, 2058", "41 h 12 m", "Sep 02, 10:15", 4, False)]


def save_dialog(c, T, x, y, mode="save", sel=1):
    s = T.s
    w, h = s(470), s(304)
    title = "Save city" if mode == "save" else "Load city"
    cx, cy, cw, ch = window(c, T, x, y, w, h, title, "system")
    fam_ = "system"
    ramp, body, _ = fam(fam_)
    lw = cw - s(160)
    rh = s(42)
    # column header strip
    n = len(SAVES)
    lh = rh * 5 + 2 * T.b
    well(c, T, cx, cy, lw - s(12), lh, fam_)
    tw, th = s(56), s(34)
    for i, (nm, tier, pop, cash, date, play, saved, seed, lit) in enumerate(SAVES):
        ry = cy + T.b + i * rh
        selected = i == sel
        if selected:
            c.fill(cx + T.b, ry, lw - s(12) - 2 * T.b, rh, R(ramp, 3))
        elif i % 2:
            c.fill(cx + T.b, ry, lw - s(12) - 2 * T.b, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        tcn = scene_crop(tw - 2 * T.b, th - 2 * T.b, seed, 0.3, lit)
        bevel(c, cx + s(4), ry + s(3), tw, th, ramp, "in", None, T.b, 7, 1)
        c.blit(tcn, cx + s(4) + T.b, ry + s(3) + T.b)
        tx = cx + s(4) + tw + s(6)
        fg = WHITE if selected else INK
        sub = CREAM if selected else R("grey", 2)
        tip_ = nm
        c.text(tx, ry + s(5), tip_, T.f_value, fg)
        c.text(tx + 1, ry + s(5), tip_, T.f_value, fg)
        c.text(cx, ry + s(5), saved, T.f_small, sub, align="right", w=lw - s(12) - s(6))
        c.blit(ic("stat_population", 16), tx - s(1), ry + s(15))
        c.text(tx + s(18), ry + s(18), "{:,}".format(pop), T.f_label, fg)
        mt = money(cash)
        c.text(tx + s(78), ry + s(18), mt, T.f_label,
               (WHITE if selected else (MONEY_NEG if cash < 0 else MONEY_POS)))
        c.text(tx, ry + s(33), date + "  -  " + play, T.f_small, sub)
    scrollbar(c, T, cx + lw - s(10), cy, lh, fam_, 0.0, 0.4)
    # right: selected details
    dx = cx + lw + s(2)
    dw = cw - lw - s(2)
    nm, tier, pop, cash, date, play, saved, seed, lit = SAVES[sel]
    dy = cy
    dy += header(c, T, dx, dy, dw, "Selected", fam_)
    pw, ph = dw, s(70)
    bevel(c, dx, dy, pw, ph, ramp, "in", None, T.b, 7, 1)
    c.blit(scene_crop(pw - 2 * T.b, ph - 2 * T.b, seed + 10 if seed else 1, 0.5, lit), dx + T.b, dy + T.b)
    dy += ph + s(5)
    c.text(dx, dy, nm, T.f_value, INK)
    c.text(dx + 1, dy, nm, T.f_value, INK)
    dy += s(14)
    for lb, val, col_ in (("Size", tier, INK), ("Population", "{:,}".format(pop), INK), ("Funds", money(cash), MONEY_NEG if cash < 0 else MONEY_POS),
                          ("City date", date, INK), ("Play time", play, INK), ("Map", "Green Valley", INK)):
        c.text(dx, dy, lb, T.f_small, R("grey", 2))
        c.text(dx, dy, val, T.f_small, col_, align="right", w=dw)
        dy += s(11)
    # bottom: name input + buttons
    by = y + h - s(24)
    iy = cy + lh + s(8)
    if mode == "save":
        c.text(cx, iy + (s(14) - F.cap_height(T.f_label)) // 2, "Name", T.f_label, INK)
        textinput(c, T, cx + s(34), iy, lw - s(46), SAVES[sel][0], fam_, "focused")
        c.text(cx, iy + s(20), "Saves to Documents/OpenCity/Saves", T.f_small, R(ramp, 1))
        btn_row(c, T, cx, by, cw, ["Save", "Delete", "Cancel"], fam_, default=0, states=[None, None, None])
    else:
        c.text(cx, iy + (s(14) - F.cap_height(T.f_label)) // 2, "Filter", T.f_label, INK)
        textinput(c, T, cx + s(34), iy, lw - s(46), "", fam_, "normal", placeholder="Search saves...")
        c.blit(ic("pnl_search", isz(T)), cx + lw - s(10) - isz(T) - s(2), iy - (isz(T) - s(14)) // 2)
        c.text(cx, iy + s(20), "5 saves, 12 autosaves", T.f_small, R(ramp, 1))
        btn_row(c, T, cx, by, cw, ["Load", "Delete", "Cancel"], fam_, default=0)
    return w, h


@reg("save-dialog", "Save dialog", 470, 318, mode="save")
@reg("load-dialog", "Load dialog", 470, 318, mode="load", sel=2)
def save_load(c, T, x, y, mode="save", sel=1):
    return save_dialog(c, T, x, y, mode, sel)


# ---- big-letter logo + menu button parts ---------------------------------------------------------------------
def _shift(m, dx, dy):
    out = np.zeros_like(m)
    h, w = m.shape
    ys0, ys1 = max(0, dy), min(h, h + dy)
    xs0, xs1 = max(0, dx), min(w, w + dx)
    out[ys0:ys1, xs0:xs1] = m[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
    return out


def _dilate(m, r):
    out = m.copy()
    for dx in range(-r, r + 1):
        for dy in range(-r, r + 1):
            if dx or dy:
                out |= _shift(m, dx, dy)
    return out


GOLD_ROWS = ["#fff6b8", "#ffe46a", "#ffc83a", "#f4a01e", "#dc7a12", "#b0520c"]


def logo(c, cx, y, k=4, text="OpenCity", tagline="Build the city of your dreams"):
    """RCT2-style chunky title: bold pixel font scaled k x, gold/orange gradient by rows, bevel highlight,
    dark outline and a hard drop shadow. cx = horizontal centre, y = top. Returns (w, h) of the logo block."""
    m, asc = F.render_mask(text, "bold")
    ys, xs = np.nonzero(m)
    m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    M = m.repeat(k, 0).repeat(k, 1)
    pad = k * 3
    big = np.zeros((M.shape[0] + 2 * pad, M.shape[1] + 2 * pad), bool)
    big[pad:pad + M.shape[0], pad:pad + M.shape[1]] = M
    outline = _dilate(big, k)
    x0 = int(cx - big.shape[1] // 2)
    # drop shadow, outline
    c.mask(_shift(outline, k * 2, k * 2), x0, y, hexc("#1a0c04"))
    c.mask(outline, x0, y, hexc("#3a1606"))
    # inner second outline (thin lighter brown) for the chunky sticker look
    inner = _dilate(big, max(1, k // 2))
    c.mask(inner, x0, y, hexc("#6a2c0a"))
    # gradient fill by rows
    rows = np.nonzero(big.any(axis=1))[0]
    r0, r1 = rows.min(), rows.max()
    n = len(GOLD_ROWS) - 1
    for r in rows:
        t = (r - r0) / max(1, r1 - r0)
        f = t * n
        i = min(n - 1, int(f))
        colr = mix(GOLD_ROWS[i], GOLD_ROWS[i + 1], f - i)
        c.mask(big[r:r + 1], x0, y + r, colr)
    # bevel: bright top edge, dark bottom edge (k px)
    top = big & ~_shift(big, 0, k)
    bot = big & ~_shift(big, 0, -k)
    left = big & ~_shift(big, k, 0)
    c.mask(bot, x0, y, hexc("#8a3c08"))
    c.mask(top | left, x0, y, hexc("#fffbe0"))
    h = big.shape[0]
    w = big.shape[1]
    if tagline:
        F_ = "12" if k < 4 else "bold"
        tw = F.measure(tagline, F_)
        ty = y + h + k * 2
        rw = tw + 4 * k
        rh = F.cap_height(F_) + 2 * k + 2
        c.fill(cx - rw // 2 - 2, ty - 2, rw + 4, rh + 4, hexc("#1a0c04"))
        c.fill(cx - rw // 2, ty, rw, rh, hexc("#7a3a10"))
        c.fill(cx - rw // 2, ty, rw, 1, hexc("#c27a30"))
        c.text(cx - tw // 2, ty + k + 1, tagline, F_, hexc("#ffe9a8"), shadow=hexc("#2a1204"))
        h += k + rh + 4
    return w, h


def mbtn(c, T, x, y, w, h, icon, label, family="city", state="normal", sub=None, face=None, isize=None, wellramp=None):
    """Big menu button: raised bevel, sunken icon well on the left, left-aligned label (+ optional sub line)."""
    ramp, body, acc = fam(family)
    face = face or T.f_big
    s = T.s
    if state == "default":
        c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R("yellow", 5))
    fill = {"normal": body, "hover": min(7, body + 1), "default": body, "pressed": body - 1, "disabled": body}[state]
    bevel(c, x, y, w, h, ramp, "in" if state == "pressed" else "out", fill, T.b, 7, 1)
    pad = s(3)
    iw = h - 2 * pad
    bevel(c, x + pad, y + pad, iw, iw, wellramp or ramp, "in", R(wellramp or ramp, 3 if wellramp else body - 2), T.b, 7, 1)
    isz_ = isize or (iw - 2 * T.b - s(4))
    c.blit(ic(icon, isz_), x + pad + (iw - isz_) // 2, y + pad + (iw - isz_) // 2)
    tx = x + pad + iw + s(8)
    if sub:
        ty = y + (h - F.cap_height(face) - s(3) - F.cap_height(T.f_small)) // 2
        c.text(tx, ty, label, face, INK)
        c.text(tx, ty + F.cap_height(face) + s(3), sub, T.f_small, R(ramp, 1))
    else:
        c.text(tx, y + (h - F.cap_height(face)) // 2, label, face, INK if state != "disabled" else R(ramp, body - 2))


# ---- map previews (simple colour blocks: water / sand / grass / forest / mountains / roads) -----------------
MAPS = [
    # name, kind, climate (icon, label), size, buildable, resources (fertile, forest, ore, oil, stone, fish), blurb
    ("Green Valley", "green-valley", ("wx_partly", "Temperate"), "3 x 3 tiles", 1820, 2304, (3, 2, 1, 0, 1, 1),
     "A gentle river valley with rich farmland and a forest ridge to the east."),
    ("Lakeside", "lakeside", ("wx_sun", "Continental"), "3 x 3 tiles", 1534, 2304, (2, 2, 0, 0, 2, 3),
     "A big freshwater lake. Great for fishing and a lively waterfront."),
    ("Riverbend", "riverbend", ("wx_rain", "Temperate"), "5 x 5 tiles", 4310, 6400, (3, 1, 2, 1, 1, 2),
     "A meandering river splits the map. Bridges are a must."),
    ("Sunny Coast", "coastal", ("wx_sun", "Mediterranean"), "5 x 5 tiles", 3925, 6400, (1, 1, 0, 2, 1, 3),
     "Long beaches, warm summers and offshore oil."),
    ("Archipelago", "islands", ("wx_heat", "Tropical"), "5 x 5 tiles", 2610, 6400, (2, 3, 0, 0, 0, 3),
     "Islands connected by bridges. Ports and ferries matter."),
    ("Highlands", "highlands", ("wx_snow", "Cold"), "7 x 7 tiles", 6240, 12544, (0, 3, 3, 1, 3, 0),
     "Rugged mountains rich in ore and stone. Flat land is precious."),
]
_MP = {}


def _blur(a, n=3):
    for _ in range(n):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
    return a


def map_preview(w, h, kind, seed=1, cell=None):
    key = (w, h, kind, seed, cell)
    if key in _MP:
        return _MP[key]
    cell = cell or max(2, w // 56)
    gw, gh = w // cell + 1, h // cell + 1
    rng = np.random.RandomState(seed * 13 + len(kind))
    def noise(n):
        a = _blur(rng.rand(gh, gw), n)
        return (a - a.min()) / (a.max() - a.min() + 1e-9)
    n1, n2, n3 = noise(4), noise(2), noise(3)
    yy, xx = np.mgrid[0:gh, 0:gw]
    xx = xx / (gw - 1.0)
    yy = yy / (gh - 1.0)
    if kind == "green-valley":
        water = np.abs((xx + yy * 0.6) - 0.95 + 0.07 * np.sin(yy * 9)) < 0.045
    elif kind == "lakeside":
        water = ((xx - 0.3) ** 2 / 0.07 + (yy - 0.5) ** 2 / 0.12 + 0.25 * (n2 - 0.5)) < 1
    elif kind == "riverbend":
        water = np.abs(yy - (0.5 + 0.2 * np.sin(xx * 7))) < 0.045
    elif kind == "coastal":
        water = (xx + 0.12 * np.sin(yy * 8) + 0.1 * (n2 - 0.5)) > 0.8
    elif kind == "islands":
        water = n1 < 0.58
    else:
        water = (n1 < 0.1)
    mount = (n1 > 0.64) & ~water if kind == "highlands" else np.zeros_like(water)
    forest = (n3 > 0.64) & ~water & ~mount
    fert = (n2 > 0.66) & ~water & ~forest & ~mount
    near = _dilate(water, 1) & ~water
    img = np.zeros((gh, gw, 3), np.uint8)
    img[:] = hexc("#58a032")[:3]
    img[(xx * 7 + yy * 5).astype(int) % 2 == 0] = hexc("#4f8a2c")[:3]
    img[fert] = hexc("#8ac04a")[:3]
    img[forest] = hexc("#2a5a1c")[:3]
    img[mount] = hexc("#8e8e86")[:3]
    img[mount & (n1 > 0.78)] = hexc("#d8d8d2")[:3]
    img[near] = hexc("#d8c890")[:3]
    dith = ((np.arange(gw)[None, :] + np.arange(gh)[:, None]) % 2 == 0)
    img[water] = hexc("#2a5cb0")[:3]
    img[water & dith] = hexc("#3a70c4")[:3]
    # entry highway from the left edge and a cross road (so every map shows the connection point)
    ry = int(gh * 0.42)
    img[ry, : int(gw * 0.42)] = hexc("#d8d8d0")[:3]
    img[ry:ry + 1, : int(gw * 0.42)] = hexc("#e8d860")[:3]
    ry2 = ry + 1
    img[ry2, : int(gw * 0.42)] = hexc("#5a5a60")[:3]
    img[ry - 1: ry2 + 1, int(gw * 0.42) - 1] = hexc("#5a5a60")[:3]
    # ore specks
    for _ in range(6 if kind in ("highlands", "riverbend", "green-valley") else 2):
        px, py = rng.randint(2, gw - 2), rng.randint(2, gh - 2)
        if not water[py, px]:
            img[py, px] = hexc("#e8c030")[:3]
    big = img.repeat(cell, 0).repeat(cell, 1)[:h, :w]
    c = Canvas(w, h)
    c.a[:, :, :3] = big
    c.a[:, :, 3] = 255
    _MP[key] = c
    return c


def pips(c, T, x, y, n, ramp="green", total=3):
    s = T.s
    for i in range(total):
        bevel(c, x + i * (s(4) + T.b), y, s(4), s(6), "grey", "in", R(ramp, 5) if i < n else R("grey", 2), T.b, 7, 1)
    return total * (s(4) + T.b)


# ---- new city chooser (window, family city) -----------------------------------------------------------------
RES_ICONS = ["nat_fertile", "nat_forest", "nat_ore", "nat_oil", "nat_stone", "nat_fish"]
RES_NAMES = ["Fertile", "Forest", "Ore", "Oil", "Stone", "Fish"]


def newcity_window(c, T, x, y, sel=0, family="city", W=520, H=332):
    s = T.s
    w, h = s(W), s(H)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "New city", family)
    ramp, body, acc = fam(family)
    lw = s(172)
    ty = cy
    ty += header(c, T, cx, ty, lw, "Choose a map", family)
    rh = s(32)
    n = len(MAPS)
    lh = rh * 6 + 2 * T.b
    well(c, T, cx, ty, lw - s(12), lh, family)
    pw, ph = s(36), s(24)
    for i, m in enumerate(MAPS):
        ry = ty + T.b + i * rh
        selected = i == sel
        if selected:
            c.fill(cx + T.b, ry, lw - s(12) - 2 * T.b, rh, R(ramp, 3))
        elif i % 2:
            c.fill(cx + T.b, ry, lw - s(12) - 2 * T.b, rh, mix(R(ramp, 7), R(ramp, 6), 0.55))
        bevel(c, cx + s(3), ry + s(3), pw + 2 * T.b, ph + 2 * T.b, ramp, "in", None, T.b, 7, 1)
        c.blit(map_preview(pw, ph, m[1], i + 1, max(1, pw // 36)), cx + s(3) + T.b, ry + s(3) + T.b)
        tx = cx + s(3) + pw + s(8)
        fg = WHITE if selected else INK
        c.text(tx, ry + s(6), m[0], T.f_label, fg)
        c.text(tx, ry + s(19), m[3], T.f_small, CREAM if selected else R("brown", 2))
    scrollbar(c, T, cx + lw - s(10), ty, lh, family, 0.0, 0.75)
    # random map entry beneath
    by_ = ty + lh + s(5)
    button(c, T, cx, by_, lw, s(16), "Random map...", family, "normal", icon=ic("ui_refresh", isz(T)))
    ly2 = by_ + s(24)
    c.text(cx, ly2, "Legend", T.f_small, R(ramp, 1))
    ly2 += s(12)
    for i, (t_, colr) in enumerate((("Water", "#3a70c4"), ("Forest", "#2a5a1c"), ("Fertile", "#8ac04a"), ("Mountain", "#8e8e86"),
                                    ("Beach", "#d8c890"), ("Entry road", "#5a5a60"))):
        lx = cx + (i % 2) * (lw // 2)
        lyy = ly2 + (i // 2) * s(13)
        bevel(c, lx, lyy, s(9), s(9), ramp, "in", hexc(colr), T.b, 7, 1)
        c.text(lx + s(13), lyy + (s(9) - F.cap_height(T.f_small)) // 2, t_, T.f_small, INK)
    # right column
    rx = cx + lw + s(8)
    rw = cw - lw - s(8)
    m = MAPS[sel]
    ry = cy
    ry += header(c, T, rx, ry, rw, m[0], family)
    pvw, pvh = s(150), s(108)
    bevel(c, rx, ry, pvw + 2 * T.b, pvh + 2 * T.b, ramp, "in", None, T.b, 7, 1)
    c.blit(map_preview(pvw, pvh, m[1], sel + 1), rx + T.b, ry + T.b)
    ix = rx + pvw + s(10)
    iw = rw - pvw - s(10)
    iy = ry
    for lb, val in (("Size", m[3]), ("Buildable", "{:,} / {:,}".format(m[4], m[5])), ("Climate", m[2][1])):
        c.text(ix, iy, lb, T.f_label, INK)
        c.text(ix, iy, val, T.f_label, INK, align="right", w=iw)
        iy += s(13)
    c.blit(ic(m[2][0], isz(T)), ix + iw - isz(T) - F.measure(m[2][1], T.f_label) - s(4), iy - s(13) - (isz(T) - F.cap_height(T.f_label)) // 2)
    iy += s(2)
    etched_h(c, T, ix, iy, iw, ramp)
    iy += s(5)
    c.text(ix, iy, "Natural resources", T.f_small, R(ramp, 1))
    iy += s(11)
    colw = iw // 2
    for i in range(6):
        gx = ix + (i % 2) * colw
        gy = iy + (i // 2) * (isz(T) + s(2))
        c.blit(ic(RES_ICONS[i], isz(T)), gx, gy)
        pips(c, T, gx + isz(T) + s(3), gy + (isz(T) - s(6)) // 2, m[6][i], ["green", "green", "yellow", "orange", "grey", "blue"][i])
    # description under preview
    dy = ry + pvh + 2 * T.b + s(5)
    for i, ln in enumerate(wrap(m[7], T.f_small, rw)[:2]):
        c.text(rx, dy + i * s(10), ln, T.f_small, INK)
    dy += s(25)
    # options group
    gh_ = y + h - s(26) - dy - s(2)
    ix0, iy0, iw0, ih0 = groupbox(c, T, rx, dy, rw, gh_, "City options", family)
    ly = iy0
    lab(c, T, ix0, ly, s(14), "City name")
    textinput(c, T, ix0 + s(66), ly, s(110), "Greenvale", family, "focused")
    ly += s(18)
    lab(c, T, ix0, ly, s(13), "Difficulty")
    dropdown(c, T, ix0 + s(66), ly, s(110), "Normal", family)
    ly += s(17)
    lab(c, T, ix0, ly, s(13), "Start funds")
    dropdown(c, T, ix0 + s(66), ly, s(110), "$50,000", family)
    ly += s(18)
    lab(c, T, ix0, ly, s(14), "Map seed")
    textinput(c, T, ix0 + s(66), ly, s(88), "48213", family)
    icon_button(c, T, ix0 + s(66) + s(92), ly, s(18), s(14), ic("ui_refresh", 16), family, "normal", flat=False)
    ly += s(18)
    lab(c, T, ix0, ly, s(12), "Disasters")
    slider(c, T, ix0 + s(66), ly, s(88), family, 0.33, value_text="Rare")
    ox = ix0 + s(66) + s(110) + s(12)
    oy = iy0 + s(2)
    for i, (t_, on) in enumerate((("Natural disasters", True), ("Unlock all", False), ("Sandbox (free money)", False))):
        checkbox(c, T, ox, oy + i * s(14), t_, family, on)
    c.text(ox, oy + s(46), "Sandbox disables milestones.", T.f_small, R(ramp, 1))
    by = y + h - s(24)
    btn_row(c, T, cx, by, cw, ["Start", "Back"], family, default=0, bw=s(72))
    lab(c, T, cx, by, s(16), "Seed 48213  -  save slot: new", R(ramp, 1), T.f_small)
    return w, h


@reg("newcity-window", "New city / map chooser", 520, 332)
@reg("newcity-window-coast", "New city: Sunny Coast selected", 520, 332, sel=3)
def newcity(c, T, x, y, sel=0):
    return newcity_window(c, T, x, y, sel)


# ---- pause / game menu window -------------------------------------------------------------------------------
PAUSE_ITEMS = [("time_play", "Resume", "Esc"), ("pnl_save", "Save city", "Ctrl+S"), ("pnl_load", "Load city", "Ctrl+L"),
               ("pnl_settings", "Settings", None), ("ui_exit", "Exit to main menu", None), ("ui_cross", "Quit to desktop", None)]


def pause_window(c, T, x, y, hover=0):
    s = T.s
    w = s(214)
    bh = s(28)
    gap = s(4)
    h = T.title_h + 2 * T.b + s(8) + s(30) + len(PAUSE_ITEMS) * (bh + gap) + s(8) + s(4)
    cx, cy, cw, ch = window(c, T, x, y, w, h, "Game menu", "city", grip=False)
    c.text(cx, cy + s(1), "Greenvale", T.f_value, INK)
    c.text(cx + 1, cy + s(1), "Greenvale", T.f_value, INK)
    c.text(cx, cy + s(14), "Large Town - Mar 14, 2031 - 12 h 40 m", T.f_small, R("brown", 1))
    ty = cy + s(26)
    etched_h(c, T, cx, ty - s(3), cw, "brown")
    for i, (icn, lbl, hk) in enumerate(PAUSE_ITEMS):
        if i == 4:
            ty += s(4)
        st = "default" if i == 0 else ("hover" if i == hover else "normal")
        mbtn(c, T, cx, ty, cw, bh, icn, lbl, "city", st, face=T.f_label, isize=isz(T), wellramp="red" if i == 5 else None)
        if hk:
            c.text(cx, ty + (bh - F.cap_height(T.f_small)) // 2, hk, T.f_small, R("brown", 1), align="right", w=cw - s(6))
        ty += bh + gap
    return w, h


@reg("pause-window", "Pause / game menu window", 214, 240)
def pause_reg(c, T, x, y):
    return pause_window(c, T, x, y, hover=2)


# ---- full-screen mockups -------------------------------------------------------------------------------------
VERSION = "OpenCity 0.9.0 preview  (bleed f000b1e)"
MENU_ITEMS = [("cat_zoning", "New City", "Start on a fresh map"), ("time_play", "Continue", "Greenvale - Mar 14, 2031"),
              ("pnl_load", "Load City", None), ("tool_brush_large", "Map Editor", None), ("pnl_settings", "Settings", None),
              ("ui_star", "Credits", None), ("ui_exit", "Quit", None)]


def screen_text(c, T, x, y, t, face=None, colr=CREAM, align="left", w=None):
    face = face or T.f_small
    c.text(x, y, t, face, colr, shadow=SHADOW, align=align, w=w)


def footer_texts(c, T, Wd, Ht):
    s = T.s
    screen_text(c, T, s(8), Ht - s(14), VERSION)
    screen_text(c, T, 0, Ht - s(14), "Based on the OpenRA engine. Press F1 for news", w=Wd - s(8), align="right")


def mainmenu_stack(Wd, Ht, u, night, seed):
    T = Theme(u)
    s = T.s
    c = world(Wd, Ht, seed, night)
    dim(c, 0.5 if night else 0.12, "#060a22" if night else "#0a1020")
    k = 6 if u < 1.25 else 8
    lw, lh = logo(c, Wd // 2, s(36), k)
    bh, gap, pw = s(34), s(4), s(264)
    ph = len(MENU_ITEMS) * (bh + gap) + s(12) + s(6)
    px, py = (Wd - pw) // 2, s(36) + lh + s(22)
    ramp = fam("city")[0]
    c.fill(px - T.b, py - T.b, pw + 2 * T.b, ph + 2 * T.b, R(ramp, 0))
    bevel(c, px, py, pw, ph, ramp, "out", 5, T.b, 7, 2)
    c.fill(px + T.b, py + T.b, pw - 2 * T.b, s(3), R(ramp, 3))
    by = py + s(8)
    for i, (icn, lbl, sub) in enumerate(MENU_ITEMS):
        if i == 6:
            by += s(6)
        st = "default" if i == 1 else ("hover" if i == 0 else "normal")
        mbtn(c, T, px + s(6), by, pw - s(12), bh, icn, lbl, "city", st, sub=sub, face="bold", wellramp="red" if i == 6 else None)
        by += bh + gap
    footer_texts(c, T, Wd, Ht)
    H.cursor(c, px + pw - s(60), py + s(14), max(1, int(u)))
    return c


def mainmenu_strip(Wd, Ht, u, night, seed):
    T = Theme(u)
    s = T.s
    c = world(Wd, Ht, seed, night)
    dim(c, 0.12 if not night else 0.5, "#060a22" if night else "#0a1020")
    k = 6 if u < 1.25 else 8
    logo(c, Wd // 2, s(40), k)
    ramp, body, _ = fam("city")
    groups = [[("cat_zoning", "New City"), ("time_play", "Continue"), ("pnl_load", "Load City")],
              [("tool_brush_large", "Map Editor"), ("pnl_settings", "Settings"), ("ui_star", "Credits")],
              [("ui_exit", "Quit")]]
    bw = s(50)
    gsp = s(6)
    tot = sum(len(g) * bw for g in groups) + gsp * (len(groups) - 1) + 2 * T.b * len(groups)
    sx = (Wd - tot) // 2
    sy = Ht - s(48) - s(62)
    x = sx
    hov = None
    for gi, g in enumerate(groups):
        gw_ = len(g) * bw + 2 * T.b
        c.fill(x - T.b, sy - T.b, gw_ + 2 * T.b, bw + 3 * T.b, R(ramp, 0))
        bevel(c, x, sy, gw_, bw + T.b, ramp, "out", body - 1, T.b, 6, 1)
        for i, (icn, lbl) in enumerate(g):
            bx = x + T.b + i * bw
            st = "hover" if lbl == "New City" else "normal"
            icon_button(c, T, bx, sy + T.b, bw, bw - T.b, ic(icn, s(32)), "city", st, flat=True, body=body)
            if lbl == "New City":
                hov = (bx, lbl)
            if lbl == "Quit":
                c.fill(bx + s(2), sy + s(3), bw - s(4), s(2), R("red", 4))
        x += gw_ + gsp
    if hov:
        tooltip(c, T, hov[0] - s(4), sy - s(40), ["Start a city on a fresh map", ("Choose terrain, climate and difficulty", R("brown", 2))], title="New City")
    footer_texts(c, T, Wd, Ht)
    H.cursor(c, hov[0] + s(30), sy + s(26), max(1, int(u)))
    return c


@scr("mainmenu-1280x720-1x", "Main menu 1280x720, UI 1x (button stack, day)", 1280, 720, 1)
def _(): return mainmenu_stack(1280, 720, 1, False, 3)


@scr("mainmenu-1920x1080-1.5x", "Main menu 1920x1080, UI 1.5x (button stack, night)", 1920, 1080, 1.5)
def _(): return mainmenu_stack(1920, 1080, 1.5, True, 5)


@scr("mainmenu-strip-1280x720-1x", "Main menu 1280x720, UI 1x (RCT2 bottom button bar, day)", 1280, 720, 1)
def _(): return mainmenu_strip(1280, 720, 1, False, 3)


@scr("mainmenu-strip-1920x1080-1.5x", "Main menu 1920x1080, UI 1.5x (RCT2 bottom button bar, night)", 1920, 1080, 1.5)
def _(): return mainmenu_strip(1920, 1080, 1.5, True, 5)


@scr("newcity-1280x720-1.5x", "New city chooser over the shell map, 1280x720 UI 1.5x", 1280, 720, 1.5)
def _():
    T = Theme(1.5)
    c = world(1280, 720, 5, False)
    dim(c, 0.4)
    w, h = T.s(520), T.s(332)
    newcity_window(c, T, (1280 - w) // 2, (720 - h) // 2)
    H.cursor(c, (1280 - w) // 2 + T.s(400), (720 - h) // 2 + T.s(295), 1)
    return c


@scr("pause-1280x720-1.5x", "Pause menu over a darkened HUD, 1280x720 UI 1.5x", 1280, 720, 1.5)
def _():
    import iso_ui_screens as S
    import iso_ui_panels_a as A
    T = Theme(1.5)
    s = T.s
    c = world(1280, 720)
    S.world_overlays(c, T, [(520, 300, "st_no_power"), (760, 420, "st_garbage")])
    tbh = s(30) + 2 * T.b
    wins = [lambda c, T: A.build_menu(c, T, s(8), tbh + s(10))]
    S.hud(c, T, 1280, 720, ("pnl_game_menu",), wins)
    dim(c, 0.55)
    w, h = pause_window(Canvas(10, 10), T, 0, 0)
    pause_window(c, T, (1280 - w) // 2, (720 - h) // 2 + s(10), hover=2)
    screen_text(c, T, 0, s(40), "PAUSED", T.f_big, hexc("#ffe46a"), "center", 1280)
    return c


@scr("loading-1280x720-1x", "Loading screen 1280x720, UI 1x (progress + tip)", 1280, 720, 1)
def _():
    T = Theme(1)
    s = T.s
    Wd, Ht = 1280, 720
    c = world(Wd, Ht, 5, True)
    dim(c, 0.5, "#060a22")
    logo(c, Wd // 2, s(50), 6)
    ramp, body, _ = fam("city")
    pw, ph = s(560), s(150)
    px, py = (Wd - pw) // 2, Ht - ph - s(70)
    c.fill(px - T.b, py - T.b, pw + 2 * T.b, ph + 2 * T.b, R(ramp, 0))
    bevel(c, px, py, pw, ph, ramp, "out", body, T.b, 7, 2)
    ix, iw = px + s(10), pw - s(20)
    ty = py + s(10)
    c.text(ix, ty, "Loading Greenvale", T.f_big, INK)
    c.text(ix + 1, ty, "Loading Greenvale", T.f_big, INK)
    c.text(ix, ty, "64%", T.f_big, INK, align="right", w=iw)
    ty += s(18)
    meter(c, T, ix, ty, iw, 0.64, "city", "green", h=s(14))
    ty += s(20)
    c.text(ix, ty, "Placing buildings (1,204 / 1,870)...", T.f_label, R(ramp, 1))
    ty += s(18)
    bh_ = ph - (ty - py) - s(10)
    well(c, T, ix, ty, iw, bh_, "city", 6)
    n = isz(T)
    c.blit(ic("ui_info", n), ix + s(6), ty + s(6))
    tipx = ix + s(6) + n + s(6)
    for i, ln in enumerate(wrap("Tip: hold Shift while dragging a road to snap it to the grid, and press Ctrl+Z to undo the last piece.", T.f_label, iw - n - s(20))):
        c.text(tipx, ty + s(8) + i * s(12), ln, T.f_label, INK)
    c.text(ix, ty + bh_ - s(10), "Tip 12 of 48", T.f_small, R(ramp, 1), align="right", w=iw - s(6))
    footer_texts(c, T, Wd, Ht)
    return c
