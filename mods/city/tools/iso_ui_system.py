"""
iso_ui_system - design-system sheets: palette ramps, window families, window anatomy (9-slice), widget
states, typography, UI-scale demo. Each sheet is drawn at UI 1x (or the scale named in its stem).
"""

from iso_ui_kit import Canvas, R, RAMPS, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import Theme, FAMILIES, window, button, icon_button, bevel, fam, etched_h, etched_v, close_button
from iso_ui_widgets import checkbox, radio, slider, scrollbar, dropdown, textinput, list_rows, well
from iso_ui_widgets2 import meter, rci, graph, tooltip, ticker, groupbox, value_row
from iso_ui_parts import chip, header
from iso_ui_icons import ic
import iso_ui_font as F

SHEETS = []  # (stem, title, fn)
BG = hexc("#e9e4da")  # neutral Figma-ish sheet background (opaque so bevels read)


def sheet(stem, title):
    def deco(fn):
        SHEETS.append((stem, title, fn))
        return fn
    return deco


def cap(c, x, y, s, face="8"):
    c.text(x, y, s, face, hexc("#4a4036"))


@sheet("ds-palette", "Colour ramps (8 steps, 0 = dark)")
def _():
    names = list(RAMPS)
    c = Canvas(8 * 44 + 64, len(names) * 30 + 20, BG)
    for i in range(8):
        cap(c, 60 + i * 44 + 18, 4, str(i))
    for j, nm in enumerate(names):
        y = 16 + j * 30
        cap(c, 4, y + 6, nm)
        for i in range(8):
            c.fill(60 + i * 44, y, 42, 18, R(nm, i))
            c.text(60 + i * 44 + 1, y + 21, RAMPS[nm][i].lstrip("#"), "8", hexc("#4a4036"))
    return c


@sheet("ds-families", "Window families (one colour per window type)")
def _():
    T = Theme(1)
    fams = [f for f in FAMILIES if f != "tooltip"]
    use = {"city": "City, toolbar, menus", "finance": "Budget, taxes, loans", "services": "Service buildings",
           "transit": "Lines, vehicles, stops", "info": "Info views, stats", "people": "Citizens, chirper, policies",
           "zoning": "Zoning, districts", "system": "Save/load, dialogs"}
    c = Canvas(4 * 176 + 8, 2 * 128 + 8, BG)
    for i, f in enumerate(fams):
        x, y = 8 + (i % 4) * 176, 8 + (i // 4) * 128
        cx, cy, cw, ch = window(c, T, x, y, 166, 118, f.title(), f, [ic("ui_eye", 16), ic("stat_money", 16), ic("ui_chart_line", 16)], 0)
        c.text(cx, cy, use[f], T.f_label, INK)
        button(c, T, cx, cy + 14, 74, 14, "Button", f)
        button(c, T, cx + 80, cy + 14, 74, 14, "Active", f, "toggled")
        meter(c, T, cx, cy + 34, cw, 0.6, f, FAMILIES[f][2] if FAMILIES[f][2] in RAMPS else "green")
        list_rows(c, T, cx, cy + 46, cw, [("Row", "12"), ("Row", "34")], f, cols=[(cw - 40, "left"), (40, "right")])
    return c


@sheet("ds-window-anatomy", "Window anatomy and 9-slice (UI 1x, units = pixels at 1x)")
def _():
    T = Theme(1)
    c = Canvas(560, 250, BG)
    window(c, T, 20, 20, 240, 190, "Window title", "city", [ic("ui_eye", 16), ic("stat_population", 16), ic("stat_money", 16)], 0, hover_tab=1)
    # annotations
    notes = [(15, "1px outline (ramp 0)"), (24, "title bar 15: ramp 3, light 5 / dark 1 bevel"),
             (36, "close box: red 4, white X with shadow"), (52, "tabs 31x26, gap 1; active = body colour"),
             (90, "body: ramp body shade, 1px light 7 / dark 2 bevel"), (205, "resize grip 8x8")]
    for y, t in notes:
        c.hline(262, y + 3, 14, hexc("#c03020"))
        c.text(280, y, t, "8", hexc("#4a4036"))
    # 9-slice grid lines over a copy
    ox = 290
    window(c, T, ox, 110, 150, 100, "9-slice", "city", None)
    for gx in (ox + 6, ox + 150 - 6):
        for yy in range(108, 214, 2):
            c.px(gx, yy, hexc("#c03020"))
    for gy in (110 + 17, 110 + 100 - 6):
        for xx in range(ox - 2, ox + 154, 2):
            c.px(xx, gy, hexc("#c03020"))
    c.text(ox + 156, 120, "corners fixed,", "8", hexc("#4a4036"))
    c.text(ox + 156, 130, "edges + fill stretch;", "8", hexc("#4a4036"))
    c.text(ox + 156, 140, "bevel = floor(scale) px", "8", hexc("#4a4036"))
    return c


@sheet("ds-buttons", "Buttons: text, icon (flat), toolbar; states")
def _():
    T = Theme(1)
    states = ["normal", "hover", "pressed", "toggled", "disabled", "default"]
    c = Canvas(560, 200, BG)
    for i, st in enumerate(states):
        x = 10 + i * 90
        cap(c, x, 6, st)
        button(c, T, x, 18, 80, 16, "Build", "city", st)
        button(c, T, x, 40, 80, 16, "Place", "finance", st)
        icon_button(c, T, x, 64, 24, 24, ic("cat_roads", 16), "city", st, flat=True)
        icon_button(c, T, x + 28, 64, 24, 24, ic("ui_trash", 16), "system", st, flat=False)
        bevel(c, x, 96, 30, 30, "orange", "out", 4, 1, 6, 1)
        icon_button(c, T, x, 96, 30, 29, ic("cat_power", 24), "zoning", st, flat=True, body=5)
        button(c, T, x, 134, 80, 18, "Hospital", "services", st, icon=ic("cat_health", 16))
    cap(c, 10, 160, "text button (min 16 high) | icon button flat / raised 24 | toolbar 30x28 with 24 icon | icon + label")
    close_button(c, T, 10, 176); close_button(c, T, 26, 176, "hover"); close_button(c, T, 42, 176, "pressed")
    cap(c, 60, 178, "close box: normal / hover / pressed")
    return c


@sheet("ds-forms", "Checkbox, radio, slider, dropdown, text input")
def _():
    T = Theme(1)
    f = "city"
    c = Canvas(520, 200, BG)
    window(c, T, 6, 6, 508, 188, "Form controls", f, None, close=False, grip=False)
    x, y = 14, 28
    checkbox(c, T, x, y, "Checked", f, True); checkbox(c, T, x, y + 16, "Unchecked", f)
    checkbox(c, T, x, y + 32, "Hover", f, False, "hover"); checkbox(c, T, x, y + 48, "Disabled", f, True, "disabled")
    radio(c, T, x + 100, y, "Selected", f, True); radio(c, T, x + 100, y + 16, "Option", f)
    radio(c, T, x + 100, y + 32, "Disabled", f, False, "disabled")
    for i, st in enumerate(["normal", "hover", "pressed", "disabled"]):
        slider(c, T, x + 200, y + i * 18, 120, f, [0.3, 0.5, 0.7, 0.4][i], st, value_text=["30%", "50%", "70%", st][i])
    slider(c, T, x + 200, y + 76, 120, f, 0.5, ticks=6, value_text="ticks")
    dropdown(c, T, x, y + 72, 120, "Medium", f)
    dropdown(c, T, x, y + 92, 120, "Disabled", f, "disabled")
    dropdown(c, T, x + 130, y + 100, 110, "Fortnightly", f, "open", ["Weekly", "Fortnightly", "Monthly"], 1)
    textinput(c, T, x + 250, y + 112, 130, "Riverton", f, "focused")
    textinput(c, T, x + 250, y + 132, 130, "", f, placeholder="City name...")
    textinput(c, T, x, y + 120, 110, "Locked", f, "disabled")
    return c


@sheet("ds-lists", "Scrollbars and list rows (zebra, hover, selected, header)")
def _():
    T = Theme(1)
    f = "info"
    c = Canvas(420, 160, BG)
    rows = [("Riverside", "12,480", ("+4%", MONEY_POS)), ("Old Town", "8,210", ("-1%", MONEY_NEG)), ("Harbour", "5,904", ("+2%", MONEY_POS)),
            ("Hilltop", "3,377", ("0%", INK)), ("Industrial Park", "1,020", ("+9%", MONEY_POS)), ("Airport", "0", ("-", INK))]
    h = list_rows(c, T, 8, 8, 240, rows, f, hover=1, selected=3, cols=[(120, "left"), (70, "right"), (50, "right")], header=("District", "Pop.", "Trend"))
    scrollbar(c, T, 250, 8, h, f, 0.1, 0.5)
    for i, st in enumerate(["normal", "hover", "pressed"]):
        scrollbar(c, T, 280 + i * 16, 8, 90, f, 0.4, 0.3, st)
        cap(c, 280 + i * 16, 100, st[0])
    scrollbar(c, T, 8, 108, 240, f, 0.3, 0.25, horizontal=True)
    cap(c, 8, 124, "rows 12 high, zebra = mix(7, 6); hover = 5; selected = 3 + white text")
    return c


@sheet("ds-data", "Meters, RCI demand, graph well, chips")
def _():
    T = Theme(1)
    f = "info"
    c = Canvas(460, 170, BG)
    for i, (v, r) in enumerate(((0.8, "green"), (0.55, "yellow"), (0.3, "orange"), (0.12, "red"), (0.6, "blue"))):
        meter(c, T, 8, 8 + i * 12, 120, v, f, r)
    meter(c, T, 8, 70, 120, 0.45, f, "teal", segmented=False, h=12, label="45% smooth")
    rci(c, T, 140, 8, 70, [0.9, 0.4, -0.35, 0.6], family="city")
    graph(c, T, 200, 8, 250, 100, f, [([3, 5, 4, 6, 8, 7, 9, 11, 10, 12], hexc("#7cf06c")), ([4, 4, 5, 5, 6, 6, 7, 7, 8, 8], hexc("#ff6a50")),
                                      ([1, 2, 2, 3, 3, 5, 4, 6, 7, 7], hexc("#84b4e6"))], ymax=13, ylabels=["13k", "6k", "0"], xlabels=["2028", "2029", "2030"])
    x = 8
    for r, t in (("green", "Line 4"), ("blue", "Metro A"), ("red", "3 fires"), ("purple", "Policy"), ("yellow", "Tier 2")):
        w, _ = chip(c, T, x, 128, t, r)
        x += w + 6
    chip(c, T, 8, 146, "No power x3", "orange", icon=ic("st_no_power", 16))
    return c


@sheet("ds-typography", "Typography: OpenCity Pixel faces per role and UI scale")
def _():
    c = Canvas(1240, 400, BG)
    y = 6
    for u in (1, 1.5, 2):
        T = Theme(u)
        cap(c, 6, y, "UI %sx  title=%s label=%s small=%s value=%s big=%s" % (u, T.f_title, T.f_label, T.f_small, T.f_value, T.f_big))
        y += 12
        h = T.s(64)
        bevel(c, 6, y, T.s(300), h, "brown", "out", 5, T.b, 7, 2)
        bevel(c, 6 + T.b, y + T.b, T.s(300) - 2 * T.b, T.title_h, "brown", "out", 3, T.b, 5, 1)
        c.text(6, y + T.b + (T.title_h - F.cap_height(T.f_title)) // 2, "Title: Budget", T.f_title, WHITE, outline=R("brown", 0), align="center", w=T.s(300))
        ty = y + T.title_h + T.s(5)
        c.text(12, ty, "Label: Residential tax", T.f_label, INK)
        ty += T.s(13)
        c.text(12, ty, "Small: per month", T.f_small, R("brown", 1))
        c.text(T.s(150), ty - T.s(13), "$12,400", T.f_value, MONEY_POS)
        c.text(T.s(230), ty - T.s(13), "-$3,150", T.f_value, MONEY_NEG)
        c.text(T.s(150), ty, "48,213", T.f_big, INK)
        bevel(c, T.s(300) + 20, y, T.s(300), h, "brown", "in", 0, T.b, 5, 0)
        c.text(T.s(300) + 28, y + T.s(6), "On dark wells: cream", T.f_label, CREAM)
        c.text(T.s(300) + 28, y + T.s(20), "+$12,400", T.f_value, hexc("#7cf06c"))
        c.text(T.s(300) + 28 + T.s(110), y + T.s(20), "-$3,150", T.f_value, hexc("#ff6a50"))
        c.text(T.s(300) + 28, y + T.s(34), "Chirp text and graph labels", T.f_small, CREAM)
        y += h + 10
    cap(c, 6, y, "Rules: 1px outline on titles only; values right-aligned; money green/red; no anti-aliasing; faces 8/10/12/16 px grids")
    return c


@sheet("ds-scale", "Same window redrawn at UI 1x / 1.5x / 2x (not upscaled)")
def _():
    c = Canvas(840, 330, BG)
    x = 6
    for u in (1, 1.5, 2):
        T = Theme(u)
        cx, cy, cw, ch = window(c, T, x, 20, T.s(160), T.s(120), "Water Tower", "services",
                                [ic("ui_eye", T.icon), ic("stat_money", T.icon)], 0)
        c.text(cx, cy, "Output 40 kL", T.f_label, INK)
        meter(c, T, cx, cy + T.s(14), cw, 0.7, "services", "blue")
        button(c, T, cx, cy + T.s(28), T.s(70), T.s(16), "Upgrade", "services")
        cap(c, x, 6, "UI %sx  (bevel %d px, icons %d)" % (u, T.b, T.icon))
        x += T.s(160) + 20
    return c


@sheet("ds-cards", "Build-menu cards: thumbnail 64 px (96 at 1.5x), states")
def _():
    from iso_ui_parts import card, thumb
    T = Theme(1)
    c = Canvas(330, 110, BG)
    for i, st in enumerate(["normal", "hover", "selected", "locked"]):
        card(c, T, 8 + i * 78, 16, thumb("police"), "Police", "$9,800", "services", st)
        cap(c, 8 + i * 78, 4, st)
    return c


@sheet("ds-overlays", "Tooltip, news ticker, group box, separators, headers")
def _():
    T = Theme(1)
    c = Canvas(480, 170, BG)
    tooltip(c, T, 8, 8, ["Builds a 2-lane street.", ("Cost: $40 / tile", MONEY_NEG), "Hotkey: R"], title="Street")
    tooltip(c, T, 8, 70, ["Unlocks at Large Town"])
    ticker(c, T, 170, 8, 300, "Chirper: Riverton reaches 50,000 citizens!", "city", ic("pnl_chirper", 16))
    ticker(c, T, 170, 30, 300, "Fire at 12 Oak Street, crews dispatched", "services", ic("st_fire", 16))
    groupbox(c, T, 170, 56, 150, 60, "Taxes", "city")
    header(c, T, 330, 60, 140, "Header", "city")
    etched_h(c, T, 330, 84, 140, "brown")
    etched_v(c, T, 400, 92, 24, "brown")
    cap(c, 330, 120, "etched separators: dark 2 + light 7")
    return c
