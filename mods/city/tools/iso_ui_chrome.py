"""
iso_ui_chrome - RCT2-style window chrome and widgets drawn from code at any pixel unit.

Everything here is a recipe that the game can reproduce at runtime: whole-pixel bevels, 9-slice-able
frames (corners fixed, edges and fill stretch), pixel-font text. Theme(u) scales the layout by u
(1, 1.5, 2...) while bevels stay floor(u) device pixels and fonts switch to the nearest pixel face.
"""

from iso_ui_kit import Canvas, R, RAMPS, INK, WHITE, CREAM, SHADOW, col, mix, hexc
import iso_ui_font as F

# Window families: (ramp, body shade, accent ramp). One cohesive colour per window family, RCT2 style.
FAMILIES = {
    "city":     ("brown", 5, "orange"),   # toolbar, main menu, city-wide panels, settings, pause
    "finance":  ("green", 6, "yellow"),   # budget, taxes, loans, economy graphs
    "services": ("red", 6, "sand"),       # service build menus + service building inspectors
    "transit":  ("blue", 6, "teal"),      # transit lines, vehicles, stops, transit build menu
    "info":     ("teal", 6, "blue"),      # info views, legends, statistics
    "people":   ("purple", 6, "orange"),  # citizens, chirper, advisor, milestones, policies
    "zoning":   ("orange", 6, "brown"),   # zoning, districts, growables inspector
    "system":   ("grey", 6, "blue"),      # save/load, dialogs, debug
    "tooltip":  ("yellow", 7, "brown"),
}


class Theme:
    def __init__(self, u=1):
        self.u = u
        self.b = max(1, int(u))  # bevel thickness
        if u < 1.25:
            self.f_title, self.f_label, self.f_small, self.f_value, self.f_big = "10", "10", "8", "10", "12"
        elif u < 1.75:
            self.f_title, self.f_label, self.f_small, self.f_value, self.f_big = "12", "12", "10", "12", "bold"
        else:
            self.f_title, self.f_label, self.f_small, self.f_value, self.f_big = "bold", "reg", "12", "reg", "bold"
        self.icon = 16 if u < 1.25 else 24 if u < 1.75 else 32
        self.title_h = self.s(15)
        self.tab_w, self.tab_h = self.s(31), self.s(26)
        self.row_h = self.s(12)

    def s(self, n):
        return int(round(n * self.u))

    def line(self, face=None):
        f = F.font(face or self.f_label)
        return int(round((f.ascent - f.descent) * f.scale))


def fam(name):
    r, body, acc = FAMILIES[name]
    return r, body, acc


# ---- bevels ----------------------------------------------------------------------------------------
def bevel(c, x, y, w, h, ramp, kind="out", fill=5, b=1, light=7, dark=1):
    """RCT2 bevel: 'out' raised (light top-left), 'in' sunken (dark top-left), 'flat' fill only."""
    if fill is not None:
        c.fill(x, y, w, h, R(ramp, fill) if isinstance(fill, int) else fill)
    if kind == "flat":
        return
    tl, br = (R(ramp, light), R(ramp, dark)) if kind == "out" else (R(ramp, dark), R(ramp, light))
    for i in range(b):
        c.fill(x + i, y + i, w - 2 * i, 1, tl)
        c.fill(x + i, y + i, 1, h - 2 * i, tl)
        c.fill(x + i, y + h - 1 - i, w - 2 * i, 1, br)
        c.fill(x + w - 1 - i, y + i, 1, h - 2 * i, br)


def etched_h(c, T, x, y, w, ramp):
    c.fill(x, y, w, T.b, R(ramp, 2))
    c.fill(x, y + T.b, w, T.b, R(ramp, 7))


def etched_v(c, T, x, y, h, ramp):
    c.fill(x, y, T.b, h, R(ramp, 2))
    c.fill(x + T.b, y, T.b, h, R(ramp, 7))


def glyph_x(c, x, y, s, colr, t=1):
    for i in range(s):
        c.fill(x + i, y + i, t, t, colr)
        c.fill(x + s - 1 - i, y + i, t, t, colr)


# ---- window ----------------------------------------------------------------------------------------
def close_button(c, T, x, y, state="normal"):
    s = T.title_h - 2 * T.b - T.s(2)
    kind = "in" if state == "pressed" else "out"
    bevel(c, x, y, s, s, "red", kind, 4 if state != "hover" else 5, T.b, 7, 1)
    k = max(1, T.b)
    g = s - 2 * T.b - T.s(4)
    off = 1 if state == "pressed" else 0
    gx, gy = x + (s - g) // 2 + off, y + (s - g) // 2 + off
    glyph_x(c, gx + 1, gy + 1, g, SHADOW, k)
    glyph_x(c, gx, gy, g, WHITE, k)
    return s


def title_bar(c, T, x, y, w, title, family, close=True, active=True):
    ramp = fam(family)[0]
    h = T.title_h
    shade = 3 if active else 2
    bevel(c, x, y, w, h, ramp, "out", shade, T.b, 5 if active else 4, 1)
    tw = w - (T.title_h if close else 0)
    c.text(x, y + (h - F.cap_height(T.f_title)) // 2, title, T.f_title, WHITE if active else R(ramp, 6),
           outline=R(ramp, 0), align="center", w=tw)
    if close:
        s = h - 2 * T.b - T.s(2)
        close_button(c, T, x + w - s - T.b - T.s(1), y + T.b + T.s(1))


def window(c, T, x, y, w, h, title, family="city", tabs=None, active_tab=0, close=True, grip=True,
           active=True, hover_tab=None):
    """Draws an RCT2 window. tabs = list of icon Canvases (or None). Returns body content rect."""
    ramp, body, acc = fam(family)
    # drop shadow (hard, 2 units) so floating windows separate from the world
    sh = T.s(3)
    c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R(ramp, 0))
    bevel(c, x, y, w, h, ramp, "out", body, T.b, 7, 2)
    title_bar(c, T, x + T.b, y + T.b, w - 2 * T.b, title, family, close, active)
    cy = y + T.b + T.title_h
    if tabs:
        ty = cy + T.s(2)
        tx = x + T.s(4)
        for i, ic in enumerate(tabs):
            tab(c, T, tx, ty, ramp, body, ic, i == active_tab, hover_tab == i)
            tx += T.tab_w + T.s(1)
        cy = ty + T.tab_h
        # body edge under the tabs, broken by the active tab
        ax = x + T.s(4) + active_tab * (T.tab_w + T.s(1))
        c.fill(x + T.b, cy - T.b, w - 2 * T.b, T.b, R(ramp, 7))
        c.fill(ax + T.b, cy - T.b, T.tab_w - 2 * T.b, T.b, R(ramp, body))
    if grip:
        gx, gy = x + w - T.b - T.s(8), y + h - T.b - T.s(8)
        for i in range(3):
            for j in range(i + 1):
                px, py = gx + T.s(6) - T.s(2) * j, gy + T.s(2) * i + T.s(2) - 0
                c.fill(gx + T.s(2) * (2 - j) + T.s(1), gy + T.s(2) * i + T.s(1) + T.s(2) * 0, T.b, T.b, R(ramp, 7))
                c.fill(gx + T.s(2) * (2 - j) + T.s(1) + T.b, gy + T.s(2) * i + T.s(1) + T.b, T.b, T.b, R(ramp, 2))
    pad = T.s(4)
    return (x + pad, cy + pad, w - 2 * pad, y + h - cy - 2 * pad)


def tab(c, T, x, y, ramp, body, icon, active, hover=False):
    h = T.tab_h if active else T.tab_h - T.s(2)
    ty = y if active else y + T.s(2)
    fill = R(ramp, body) if active else R(ramp, body - 1 if not hover else body)
    c.fill(x, ty, T.tab_w, h, fill)
    b = T.b
    c.fill(x + b, ty, T.tab_w - 2 * b, b, R(ramp, 7))           # top
    c.fill(x, ty + b, b, h - b, R(ramp, 7))                      # left
    c.fill(x + T.tab_w - b, ty + b, b, h - b, R(ramp, 1))         # right
    if icon is not None:
        ix = x + (T.tab_w - icon.w) // 2
        iy = ty + (h - icon.h) // 2 + (0 if active else 0)
        c.blit(icon, ix, iy)


# ---- widgets ---------------------------------------------------------------------------------------
def button(c, T, x, y, w, h, label, family="city", state="normal", face=None, icon=None):
    ramp, body, acc = fam(family)
    face = face or T.f_label
    fills = {"normal": body, "hover": min(7, body + 1), "pressed": body - 1, "disabled": body,
             "toggled": 3, "default": body}
    kind = "in" if state in ("pressed", "toggled") else "out"
    c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R(ramp, 0)) if state == "default" else None
    bevel(c, x, y, w, h, ramp, kind, fills[state], T.b, 7, 1)
    off = T.b if kind == "in" else 0
    tx = x
    tw = w
    if icon is not None:
        iw = icon.w + T.s(3)
        total = iw + F.measure(label, face)
        ix = x + (w - total) // 2 + off
        c.blit(icon, ix, y + (h - icon.h) // 2 + off)
        tx, tw = ix + iw, F.measure(label, face)
    ty = y + (h - F.cap_height(face)) // 2 + off
    if state == "disabled":
        c.text(tx + 1, ty + 1, label, face, R(ramp, 7), align="center", w=tw)
        c.text(tx, ty, label, face, R(ramp, body - 2), align="center", w=tw)
    elif state == "toggled":
        c.text(tx + off, ty, label, face, WHITE, outline=R(ramp, 0), align="center", w=tw)
    else:
        c.text(tx + off, ty, label, face, INK, align="center", w=tw)


def icon_button(c, T, x, y, w, h, icon, family="city", state="normal", flat=True, body=None):
    ramp, fb, acc = fam(family)
    body = fb if body is None else body
    if state == "normal":
        if not flat:
            bevel(c, x, y, w, h, ramp, "out", body, T.b, 7, 1)
    elif state == "hover":
        bevel(c, x, y, w, h, ramp, "out", min(7, body + 1) if not flat else None, T.b, 7, 1)
    elif state == "pressed":
        bevel(c, x, y, w, h, ramp, "in", body - 1, T.b, 7, 1)
    elif state == "toggled":
        bevel(c, x, y, w, h, ramp, "in", body - 2, T.b, 7, 1)
    elif state == "disabled":
        if not flat:
            bevel(c, x, y, w, h, ramp, "out", body, T.b, 7, 1)
    if icon is not None:
        off = T.b if state in ("pressed", "toggled") else 0
        ic = disabled_icon(icon, ramp) if state == "disabled" else icon
        c.blit(ic, x + (w - ic.w) // 2 + off, y + (h - ic.h) // 2 + off)


def disabled_icon(icon, ramp):
    """RCT2 disabled look: silhouette embossed in two shades of the window ramp."""
    out = Canvas(icon.w + 1, icon.h + 1)
    m = icon.a[:, :, 3] > 127
    out.mask(m, 1, 1, R(ramp, 7))
    out.mask(m, 0, 0, R(ramp, 3))
    return out
