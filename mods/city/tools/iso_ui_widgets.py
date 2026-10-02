"""
iso_ui_widgets - RCT2-style form widgets (checkbox, radio, slider, scrollbar, dropdown, text input,
list rows, meters, graph frame, tooltip, ticker, group box). All whole-pixel recipes; see iso_ui_chrome.
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix
from iso_ui_chrome import bevel, fam, etched_h, glyph_x
import iso_ui_font as F

WELL_TXT = INK


def well(c, T, x, y, w, h, family, shade=None):
    """Sunken well (list/scroll/graph background): inset bevel, paper-light fill."""
    ramp, body, _ = fam(family)
    bevel(c, x, y, w, h, ramp, "in", shade if shade is not None else 7, T.b, 7, 2)


def tick(c, T, x, y, s, colr):
    k = T.b
    pts = [(0, 3), (1, 4), (2, 5), (3, 4), (4, 3), (5, 2), (6, 1)]
    sc = s / 8.0
    for px, py in pts:
        c.fill(x + int(px * sc), y + int(py * sc), max(k, int(sc)) + k, max(k, int(sc)) + k, colr)


def checkbox(c, T, x, y, label, family, checked=False, state="normal"):
    ramp, body, _ = fam(family)
    s = T.s(10)
    fill = 7 if state != "disabled" else body
    bevel(c, x, y, s, s, ramp, "in", fill if state != "hover" else 6, T.b, 7, 1)
    if checked:
        tick(c, T, x + T.s(2), y + T.s(1), T.s(7), INK if state != "disabled" else R(ramp, 3))
    if label:
        tc = INK if state != "disabled" else R(ramp, body - 2)
        c.text(x + s + T.s(4), y + (s - F.cap_height(T.f_label)) // 2, label, T.f_label, tc)
    return s


def radio(c, T, x, y, label, family, on=False, state="normal"):
    ramp, body, _ = fam(family)
    s = T.s(10)
    # pixel circle: square with knocked corners, inset shading
    fill = R(ramp, 7 if state != "disabled" else body)
    dk, lt = R(ramp, 1), R(ramp, 7)
    e = max(1, s // 4)
    c.fill(x + e, y, s - 2 * e, s, fill)
    c.fill(x, y + e, s, s - 2 * e, fill)
    c.fill(x + T.b, y + T.b, s - 2 * T.b, s - 2 * T.b, fill)
    c.fill(x + e, y, s - 2 * e, T.b, dk); c.fill(x, y + e, T.b, s - 2 * e, dk)
    c.fill(x + e, y + s - T.b, s - 2 * e, T.b, lt); c.fill(x + s - T.b, y + e, T.b, s - 2 * e, lt)
    c.fill(x + T.b, y + T.b, e - T.b, e - T.b, dk); c.fill(x + s - e, y + s - e, e - T.b, e - T.b, lt)
    c.fill(x + s - e, y + T.b, e - T.b, e - T.b, R(ramp, 4)); c.fill(x + T.b, y + s - e, e - T.b, e - T.b, R(ramp, 4))
    if on:
        d = s // 2 - T.b
        c.fill(x + (s - d) // 2, y + (s - d) // 2, d, d, INK if state != "disabled" else R(ramp, 3))
    if label:
        tc = INK if state != "disabled" else R(ramp, body - 2)
        c.text(x + s + T.s(4), y + (s - F.cap_height(T.f_label)) // 2, label, T.f_label, tc)


def slider(c, T, x, y, w, family, value=0.5, state="normal", ticks=0, label=None, value_text=None):
    """Horizontal slider: sunken groove + raised thumb (RCT2 style)."""
    ramp, body, acc = fam(family)
    h = T.s(12)
    gy = y + h // 2 - T.s(2)
    bevel(c, x, gy, w, T.s(5), ramp, "in", 2, T.b, 7, 1)
    fillw = int((w - 2 * T.b) * value)
    c.fill(x + T.b, gy + T.b, fillw, T.s(5) - 2 * T.b, R(acc, 5 if state != "disabled" else 3))
    for i in range(ticks):
        tx = x + int(i * (w - 1) / max(1, ticks - 1))
        c.fill(tx, y + h, T.b, T.s(3), R(ramp, 2))
    tw = T.s(8)
    tx = x + int((w - tw) * value)
    tf = {"normal": body, "hover": min(7, body + 1), "pressed": body - 1, "disabled": body - 1}[state]
    bevel(c, tx, y, tw, h, ramp, "in" if state == "pressed" else "out", tf, T.b, 7, 1)
    c.fill(tx + tw // 2 - T.b // 2 - (1 if T.b == 1 else 0) + 1, y + T.s(3), T.b, h - T.s(6), R(ramp, 2))
    if value_text:
        c.text(x + w + T.s(6), y + (h - F.cap_height(T.f_value)) // 2, value_text, T.f_value, INK)


def arrow(c, x, y, s, d, colr):
    """Solid pixel triangle arrow of size s pointing d in up|down|left|right."""
    for i in range(s):
        if d == "up":
            c.fill(x - i, y + i, 2 * i + 1, 1, colr)
        elif d == "down":
            c.fill(x - (s - 1 - i), y + i, 2 * (s - 1 - i) + 1, 1, colr)
        elif d == "left":
            c.fill(x + i, y - i, 1, 2 * i + 1, colr)
        elif d == "right":
            c.fill(x + i, y - (s - 1 - i), 1, 2 * (s - 1 - i) + 1, colr)


def scrollbar(c, T, x, y, h, family, pos=0.2, size=0.3, state="normal", horizontal=False):
    """Vertical (or horizontal) RCT2 scrollbar: arrow buttons + sunken trough + raised thumb."""
    ramp, body, _ = fam(family)
    t = T.s(10)
    if horizontal:
        bevel(c, x, y, h, t, ramp, "in", body - 1, T.b, 7, 1)
        bevel(c, x, y, t, t, ramp, "out", body, T.b, 7, 1)
        bevel(c, x + h - t, y, t, t, ramp, "out", body, T.b, 7, 1)
        a = max(2, T.s(3))
        arrow(c, x + t // 2 - a // 2, y + t // 2, a, "left", INK)
        arrow(c, x + h - t + t // 2 - a // 2, y + t // 2, a, "right", INK)
        tr = h - 2 * t
        bevel(c, x + t + int(tr * pos), y, max(T.s(8), int(tr * size)), t, ramp, "out",
              body + (1 if state == "hover" else 0) - (1 if state == "pressed" else 0), T.b, 7, 1)
        return
    bevel(c, x, y, t, h, ramp, "in", body - 1, T.b, 7, 1)
    bevel(c, x, y, t, t, ramp, "out", body, T.b, 7, 1)
    bevel(c, x, y + h - t, t, t, ramp, "out" if state != "pressed_down" else "in", body, T.b, 7, 1)
    a = max(2, T.s(3))
    arrow(c, x + t // 2, y + t // 2 - a // 2, a, "up", INK)
    arrow(c, x + t // 2, y + h - t + t // 2 - a // 2, a, "down", INK)
    tr = h - 2 * t
    th = max(T.s(8), int(tr * size))
    ty = y + t + int((tr - th) * pos)
    tf = body + (1 if state == "hover" else 0) - (1 if state == "pressed" else 0)
    bevel(c, x, ty, t, th, ramp, "out", tf, T.b, 7, 1)
    for i in (-T.s(2), 0, T.s(2)):
        c.fill(x + T.s(2), ty + th // 2 + i, t - T.s(4), T.b, R(ramp, 2))


def dropdown(c, T, x, y, w, text, family, state="normal", items=None, sel=0):
    ramp, body, _ = fam(family)
    h = T.s(13)
    bevel(c, x, y, w, h, ramp, "in", 7 if state != "disabled" else body, T.b, 7, 1)
    c.text(x + T.s(4), y + (h - F.cap_height(T.f_label)) // 2, text, T.f_label,
           INK if state != "disabled" else R(ramp, body - 2))
    bw = h - 2 * T.b
    bx = x + w - bw - T.b
    bevel(c, bx, y + T.b, bw, bw, ramp, "in" if state == "open" else "out", body + (1 if state == "hover" else 0), T.b, 7, 1)
    a = max(2, T.s(3))
    arrow(c, bx + bw // 2, y + T.b + bw // 2 - a // 2, a, "down", INK)
    if state == "open" and items:
        ly = y + h
        lh = T.row_h * len(items) + 2 * T.b
        c.fill(x - T.b, ly - T.b, w + 2 * T.b, lh + 2 * T.b, R(ramp, 0))
        bevel(c, x, ly, w, lh, ramp, "out", 7, T.b, 7, 2)
        for i, it in enumerate(items):
            ry = ly + T.b + i * T.row_h
            if i == sel:
                c.fill(x + T.b, ry, w - 2 * T.b, T.row_h, R(ramp, 3))
            c.text(x + T.s(4), ry + (T.row_h - F.cap_height(T.f_label)) // 2, it, T.f_label,
                   WHITE if i == sel else INK)
    return h


def textinput(c, T, x, y, w, text, family, state="normal", placeholder=None, caret=True):
    ramp, body, _ = fam(family)
    h = T.s(14)
    if state == "focused":
        c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R("yellow", 5))
    bevel(c, x, y, w, h, ramp, "in", 7 if state != "disabled" else body, T.b, 7, 1)
    ty = y + (h - F.cap_height(T.f_label)) // 2
    if text:
        tw = c.text(x + T.s(4), ty, text, T.f_label, INK if state != "disabled" else R(ramp, body - 2))
    else:
        tw = 0
        if placeholder:
            c.text(x + T.s(4), ty, placeholder, T.f_label, R(ramp, 4))
    if state == "focused" and caret:
        c.fill(x + T.s(4) + tw + T.b, y + T.s(3), T.b, h - T.s(6), INK)
    return h


def list_rows(c, T, x, y, w, rows, family, hover=None, selected=None, cols=None, zebra=True, header=None):
    """rows: list of tuples of cell strings. cols: list of (width, align). Draws inside a well."""
    ramp, body, _ = fam(family)
    rh = T.row_h
    n = len(rows) + (1 if header else 0)
    h = n * rh + 2 * T.b
    well(c, T, x, y, w, h, family)
    cy = y + T.b
    cols = cols or [(w - 2 * T.b, "left")]
    if header:
        bevel(c, x + T.b, cy, w - 2 * T.b, rh, ramp, "out", body, T.b, 7, 2)
        cx = x + T.b
        for (cw, al), cell in zip(cols, header):
            c.text(cx + T.s(3), cy + (rh - F.cap_height(T.f_small)) // 2, cell, T.f_small, INK, align=al, w=cw - T.s(6))
            cx += cw
        cy += rh
    for i, r in enumerate(rows):
        fillc = None
        if zebra and i % 2 == 1:
            fillc = mix(R(ramp, 7), R(ramp, 6), 0.55)
        if hover == i:
            fillc = R(ramp, 5)
        if selected == i:
            fillc = R(ramp, 3)
        if fillc is not None:
            c.fill(x + T.b, cy, w - 2 * T.b, rh, fillc)
        cx = x + T.b
        for (cw, al), cell in zip(cols, r):
            colr = WHITE if selected == i else INK
            if isinstance(cell, tuple):
                cell, colr = cell[0], (cell[1] if selected != i else WHITE)
            c.text(cx + T.s(3), cy + (rh - F.cap_height(T.f_label)) // 2, cell, T.f_label, colr, align=al, w=cw - T.s(6))
            cx += cw
        cy += rh
    return h
