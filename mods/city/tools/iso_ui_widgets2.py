"""
iso_ui_widgets2 - RCT2-style data widgets: meters, RCI demand, graph frame, tooltip, ticker, group box.
"""

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import bevel, fam, etched_h, etched_v
from iso_ui_widgets import well
import iso_ui_font as F


def meter(c, T, x, y, w, value, family, ramp="green", segmented=True, h=None, label=None):
    """Progress/meter bar: sunken trough, ramped fill with 1px highlight. segmented = RCT2 block bar."""
    fr, body, _ = fam(family)
    h = h or T.s(8)
    bevel(c, x, y, w, h, fr, "in", 1, T.b, 7, 1)
    iw = w - 2 * T.b
    fw = int(iw * max(0.0, min(1.0, value)))
    ih = h - 2 * T.b
    c.fill(x + T.b, y + T.b, fw, ih, R(ramp, 4))
    c.fill(x + T.b, y + T.b, fw, max(1, ih // 3), R(ramp, 6))
    c.fill(x + T.b, y + h - T.b - T.b, fw, T.b, R(ramp, 3))
    if segmented:
        step = T.s(5)
        for sx in range(x + T.b + step - 1, x + T.b + fw, step):
            c.fill(sx, y + T.b, T.b, ih, R(ramp, 2))
    if label:
        c.text(x, y + h // 2 - F.cap_height(T.f_small) // 2, label, T.f_small, WHITE, outline=SHADOW, align="center", w=w)


def rci(c, T, x, y, h, values, labels=("R", "C", "I", "O"), ramps=("green", "blue", "yellow", "purple"), family="city", bw=None):
    """RCI(O) demand bars, growing up (positive) or down (negative) from a centre line."""
    fr, body, _ = fam(family)
    bw = bw or T.s(7)
    gap = T.s(3)
    w = len(values) * (bw + gap) + gap
    lh = F.cap_height(T.f_small) + T.s(3)
    bevel(c, x, y, w, h - lh, fr, "in", 1, T.b, 7, 1)
    mid = y + (h - lh) // 2
    c.fill(x + T.b, mid, w - 2 * T.b, T.b, R(fr, 4))
    half = (h - lh) // 2 - T.b - T.s(1)
    for i, v in enumerate(values):
        bx = x + gap + i * (bw + gap)
        bh = int(abs(v) * half)
        by = mid - bh if v >= 0 else mid + T.b
        r = ramps[i]
        c.fill(bx, by, bw, bh, R(r, 5))
        c.fill(bx, by, T.b, bh, R(r, 7))
        c.fill(bx + bw - T.b, by, T.b, bh, R(r, 3))
        c.text(bx, y + h - lh + T.s(2), labels[i], T.f_small, INK, align="center", w=bw)
    return w


def graph(c, T, x, y, w, h, family, series, ymax=None, xlabels=None, ylabels=None, fill_first=False, grid=4):
    """Graph frame: dark sunken plot area with grid and pixel polylines (RCT2 park-rating graph look)."""
    fr, body, _ = fam(family)
    lw = T.s(26) if ylabels else 0
    bh = T.s(10) if xlabels else 0
    px, py, pw, ph = x + lw, y, w - lw, h - bh
    bevel(c, px, py, pw, ph, fr, "in", None, T.b, 7, 1)
    c.fill(px + T.b, py + T.b, pw - 2 * T.b, ph - 2 * T.b, hexc("#10180e"))
    for i in range(1, grid):
        gy = py + T.b + i * (ph - 2 * T.b) // grid
        for gx in range(px + T.b, px + pw - T.b, T.s(3)):
            c.fill(gx, gy, T.b, T.b, hexc("#2e4a2a"))
    ymax = ymax or max(max(s[0]) for s in series)
    iw, ih = pw - 2 * T.b - T.s(2), ph - 2 * T.b - T.s(3)
    ox, oy = px + T.b + T.s(1), py + T.b + T.s(2)
    for k, (vals, colr) in enumerate(series):
        n = len(vals)
        pts = [(ox + int(i * (iw - 1) / max(1, n - 1)), oy + ih - int(v / ymax * ih)) for i, v in enumerate(vals)]
        if fill_first and k == 0:
            for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
                for xx in range(x0, x1 + 1):
                    t = (xx - x0) / max(1, x1 - x0)
                    yy = int(round(y0 + (y1 - y0) * t))
                    c.fill(xx, yy, 1, oy + ih - yy, mix(colr, "#10180e", 0.6))
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            steps = max(abs(x1 - x0), abs(y1 - y0), 1)
            for s in range(steps + 1):
                xx = x0 + (x1 - x0) * s // steps
                yy = y0 + (y1 - y0) * s // steps
                c.fill(xx, yy, T.b, T.b, colr)
        for xx, yy in pts:
            c.fill(xx - T.b, yy - T.b, 2 * T.b + (0 if T.b > 1 else 1), 2 * T.b + (0 if T.b > 1 else 1), colr)
    if ylabels:
        for i, lab in enumerate(ylabels):
            ly = py + T.b + i * (ph - 2 * T.b) // (len(ylabels) - 1) - F.cap_height(T.f_small) // 2
            c.text(x, ly, lab, T.f_small, INK, align="right", w=lw - T.s(3))
    if xlabels:
        for i, lab in enumerate(xlabels):
            lx = ox + i * (iw - 1) // max(1, len(xlabels) - 1)
            c.text(lx - T.s(15), py + ph + T.s(3), lab, T.f_small, INK, align="center", w=T.s(30))


def tooltip(c, T, x, y, lines, w=None, title=None):
    """Tooltip: cream box, 1px dark outline, title line bold-ish, small drop shadow."""
    pad = T.s(4)
    lh = T.line(T.f_label) - T.s(1)
    w = w or max(F.measure(s if isinstance(s, str) else s[0], T.f_label) for s in lines + ([title] if title else [])) + 2 * pad
    h = (len(lines) + (1 if title else 0)) * lh + 2 * pad - T.s(3)
    c.fill(x + T.s(2), y + T.s(2), w, h, (0, 0, 0, 255))
    c.fill(x, y, w, h, R("yellow", 0))
    bevel(c, x + T.b, y + T.b, w - 2 * T.b, h - 2 * T.b, "yellow", "out", 7, T.b, 7, 5)
    ty = y + pad
    if title:
        c.text(x + pad, ty, title, T.f_label, R("brown", 1))
        c.text(x + pad + 1, ty, title, T.f_label, R("brown", 1))
        ty += lh
    for s in lines:
        colr = INK
        if isinstance(s, tuple):
            s, colr = s
        c.text(x + pad, ty, s, T.f_label, colr)
        ty += lh
    return w, h


def ticker(c, T, x, y, w, msg, family="city", icon=None, h=None):
    """News/notification ticker strip (RCT2 bottom-bar news item): dark sunken strip, light text."""
    fr, body, _ = fam(family)
    h = h or T.s(16)
    bevel(c, x, y, w, h, fr, "in", 1, T.b, 5, 0)
    tx = x + T.s(4)
    if icon is not None:
        c.blit(icon, tx, y + (h - icon.h) // 2)
        tx += icon.w + T.s(4)
    c.text(tx, y + (h - F.cap_height(T.f_label)) // 2, msg, T.f_label, CREAM)
    return h


def groupbox(c, T, x, y, w, h, label, family):
    """RCT2 group box: etched rectangle, label breaking the top edge."""
    fr, body, _ = fam(family)
    cy = y + F.cap_height(T.f_label) // 2
    lw = F.measure(label, T.f_label) + T.s(6) if label else 0
    etched_h(c, T, x, cy, T.s(4), fr)
    etched_h(c, T, x + T.s(4) + lw, cy, w - T.s(4) - lw, fr)
    etched_h(c, T, x, y + h - 2 * T.b, w, fr)
    etched_v(c, T, x, cy, y + h - cy, fr)
    etched_v(c, T, x + w - 2 * T.b, cy, y + h - cy, fr)
    if label:
        c.text(x + T.s(7), y, label, T.f_label, INK)
    return (x + T.s(5), y + T.s(12), w - T.s(10), h - T.s(16))


def value_row(c, T, x, y, w, label, value, colr=INK, face=None, icon=None):
    """Label on the left, value right-aligned (inspector/budget rows)."""
    tx = x
    if icon is not None:
        c.blit(icon, x, y - (icon.h - F.cap_height(T.f_label)) // 2)
        tx += icon.w + T.s(3)
    c.text(tx, y, label, T.f_label, INK)
    c.text(x, y, value, face or T.f_value, colr, align="right", w=w)


def money(v, plus=False):
    s = "{:,}".format(abs(int(v)))
    if v < 0:
        return "-$" + s
    return ("+$" if plus else "$") + s
