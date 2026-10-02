"""
iso_ui_parts - composite pieces shared by panels and screens: iso building thumbnails (placeholder until
the world workstreams deliver real renders), build-menu cards, section headers, toolbar strips, badges.

Thumbnail contract (world workstreams must match): 64x64 px at 1x (96 at 1.5x, 128 at 2x), RGBA, transparent
background, the building on its own footprint ground, default facing, bottom-centred with 4 px margin,
footprint diagonal scaled to ~56 px (isokit tile scale = 56 / (64 * max(fp_w, fp_h))).
"""

import numpy as np

from iso_ui_kit import Canvas, R, INK, WHITE, CREAM, SHADOW, hexc, mix, MONEY_POS, MONEY_NEG
from iso_ui_chrome import bevel, fam, etched_h, icon_button
from iso_ui_widgets import well
import iso_ui_world as W
import iso_ui_font as F

THUMB_KINDS = {
    # name: (footprint w, h, height px at 1x, wall key or (light, mid, dark) hex, roof key, roof kind, ground)
    "hospital": (2, 2, 40, ("#f4f4f0", "#d8d8d4", "#b0b0ac"), ("#c83030", "#e04040", "#902020"), "flat", "grass"),
    "clinic": (1, 1, 16, ("#f4f4f0", "#d8d8d4", "#b0b0ac"), ("#c83030", "#e04040", "#902020"), "flat", "grass"),
    "police": (1, 1, 18, ("#7a9ad8", "#5a7ab8", "#3a5a98"), ("#2a3a6a", "#3a4a7a", "#1a2a4a"), "flat", "paved"),
    "police_hq": (2, 2, 34, ("#7a9ad8", "#5a7ab8", "#3a5a98"), ("#2a3a6a", "#3a4a7a", "#1a2a4a"), "flat", "paved"),
    "fire": (1, 1, 16, ("#d84a3a", "#b83a2a", "#8a2a1a"), ("#5a5a60", "#7a7a80", "#4a4a50"), "flat", "paved"),
    "firestation": (2, 1, 20, ("#d84a3a", "#b83a2a", "#8a2a1a"), ("#5a5a60", "#7a7a80", "#4a4a50"), "flat", "paved"),
    "school": (2, 2, 18, ("#d8a868", "#c09058", "#987040"), ("#7a3a8a", "#9a4aaa", "#5a2a6a"), "gable", "grass"),
    "university": (3, 3, 30, ("#e0d0b0", "#c8b898", "#a09070"), ("#4a6a9a", "#5a7aaa", "#2a4a7a"), "flat", "grass"),
    "park": (1, 1, 0, None, None, "park", "grass"),
    "watertower": (1, 1, 34, ("#8ab8e8", "#6a98c8", "#4a78a8"), ("#4a78a8", "#6a98c8", "#2a5888"), "flat", "grass"),
    "coal": (3, 2, 26, ("#a09080", "#887868", "#605448"), ("#5a5a60", "#7a7a80", "#4a4a50"), "flat", "dirt"),
    "solar": (2, 2, 3, ("#2a3a6a", "#3a4a8a", "#1a2a4a"), ("#5a7ad8", "#7a9af8", "#3a5ab8"), "flat", "grass"),
    "wind": (1, 1, 0, None, None, "wind", "grass"),
    "landfill": (3, 3, 4, ("#8a7a4a", "#6a5a3a", "#4a3a2a"), ("#9a8a5a", "#aa9a6a", "#7a6a4a"), "flat", "dirt"),
    "cemetery": (2, 2, 0, None, None, "cemetery", "grass"),
    "cityhall": (2, 2, 30, ("#ece4cc", "#d4ccb4", "#aca48c"), ("#6a8a5a", "#7a9a6a", "#4a6a3a"), "flat", "paved"),
    "busdepot": (2, 2, 14, ("#5a8ad8", "#4a7ac8", "#2a5aa8"), ("#8a8a90", "#a4a4aa", "#6a6a70"), "flat", "paved"),
    "trainstation": (3, 2, 20, ("#c87848", "#a86838", "#804828"), ("#5a6a7a", "#6a7a8a", "#3a4a5a"), "gable", "paved"),
    "house": (1, 1, 12, "house", "house", "gable", "grass"),
    "tower": (1, 1, 60, "office", "office", "flat", "paved"),
    "factory": (2, 2, 16, "factory", "factory", "flat", "dirt"),
}
GROUND = {"grass": ["#58a032", "#3c7a24"], "paved": ["#b8b4a8", "#8a867c"], "dirt": ["#a88a5a", "#7a6040"]}


def _cols(spec, idx):
    if spec is None:
        return None
    if isinstance(spec, str):
        return W.WALLS[spec][idx]
    return [hexc(c) for c in spec]


def thumb_flat(kind, size=64, lit=False):
    """Older flat placeholder (no isokit)."""
    fw, fh, h, wall, roof, rk, gk = THUMB_KINDS[kind]
    hh = h if rk not in ("wind",) else 52
    t = min((size - 8) / (32.0 * (fw + fh)), (size - 8) / (16.0 * (fw + fh) + hh + 3))
    c = Canvas(size, size)
    ox = size // 2 + int((fh - fw) * 16 * t)
    oy = size - 4 - int((fw + fh) * 16 * t)
    S = lambda x, y, z=0: (ox + (x - y) * 32 * t, oy + (x + y) * 16 * t - z * t)
    g0, g1 = [hexc(x) for x in GROUND[gk]]
    for pts, colr in (([S(0, fh), S(fw, fh), S(fw, fh, -3), S(0, fh, -3)], g1),
                      ([S(fw, fh), S(fw, 0), S(fw, 0, -3), S(fw, fh, -3)], mix(g1, "#000000", 0.25)),
                      ([S(0, 0), S(fw, 0), S(fw, fh), S(0, fh)], g0)):
        r = W._poly_mask(c, pts)
        if r:
            ya, yb, xa, xb, m, _, _ = r
            c.a[ya:yb, xa:xb][m] = colr
    if rk == "park":
        W.tree(c, ox, oy, 0.35, 0.4, t, 1.0)
        W.tree(c, ox, oy, 0.75, 0.7, t, 0.8)
    elif rk == "wind":
        bx, by = S(0.5, 0.5)
        c.fill(bx - 1, by - 46 * t, 2, 46 * t, hexc("#f0f0f0"))
        for dx, dy in ((0, -12), (10, 6), (-10, 6)):
            for i in range(10):
                c.fill(bx + dx * t * i / 10, by - 46 * t + dy * t * i / 10, 2, 1, hexc("#e8e8e8"))
    elif rk == "cemetery":
        for i in range(3):
            for j in range(3):
                W.building(c, ox, oy, 0.3 + i * 0.6, 0.3 + j * 0.6, 0.45 + i * 0.6, 0.38 + j * 0.6, 7,
                           [hexc("#c8c8c8"), hexc("#a8a8a8"), hexc("#808080")],
                           [hexc("#d8d8d8"), hexc("#d8d8d8"), hexc("#d8d8d8")], t, False)
    else:
        W.building(c, ox, oy, 0.1, 0.1, fw - 0.1, fh - 0.1, h, _cols(wall, 0), _cols(roof, 1), t, True,
                   "gable" if rk == "gable" else "flat", lit)
        if kind in ("hospital", "clinic"):
            cx, cy = S(fw / 2, fh / 2, h)
            k = max(1, int(3 * t * max(fw, fh)))
            c.fill(cx - 3 * k, cy - k, 6 * k, 2 * k, hexc("#e83030"))
            c.fill(cx - k, cy - 3 * k + 1, 2 * k, 6 * k - 2, hexc("#e83030"))
    return c


def thumb(kind, size=64, lit=False):
    """Placeholder thumbnail rendered with isokit (iso_ui_thumbs); flat fallback if isokit is missing."""
    try:
        import iso_ui_thumbs
        return iso_ui_thumbs.thumb(kind, size)
    except ImportError:
        return thumb_flat(kind, size, lit)


def card(c, T, x, y, th, name, cost, family="city", state="normal", upkeep=None):
    """Build-menu item card: sunken thumbnail well, name, cost. state: normal|hover|selected|locked."""
    ramp, body, acc = fam(family)
    w = th.w + 2 * T.b + T.s(2)
    h = w + T.s(22)
    if state == "selected":
        c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R("yellow", 6))
    kind = "in" if state == "selected" else "out"
    bevel(c, x, y, w, h, ramp, kind, body + (1 if state == "hover" else 0) - (1 if state == "selected" else 0), T.b, 7, 1)
    wf = mix(R(ramp, 2), R("grey", 3), 0.6)
    bevel(c, x + T.b, y + T.b, w - 2 * T.b, w - 2 * T.b, ramp, "in", wf, T.b, 4, 0)
    c.blit(th, x + T.b + T.s(1), y + T.b + T.s(1))
    if state == "locked":
        sh = Canvas(th.w, th.h)
        m = th.a[:, :, 3] > 0
        sh.mask(m, 0, 0, mix(R(ramp, 1), R("grey", 2), 0.6))
        c.blit(sh, x + T.b + T.s(1), y + T.b + T.s(1))
    ty = y + w + T.s(2)
    c.text(x, ty, name, T.f_small, INK if state != "locked" else R(ramp, body - 2), align="center", w=w)
    cc = MONEY_POS if state != "locked" else R(ramp, body - 2)
    c.text(x, ty + T.s(10), cost, T.f_small, cc, align="center", w=w)
    return w, h


def header(c, T, x, y, w, text, family, face=None):
    """Section header: text then an etched line to the right edge."""
    ramp = fam(family)[0]
    face = face or T.f_label
    tw = c.text(x, y, text, face, R(ramp, 1))
    c.text(x + 1, y, text, face, R(ramp, 1))
    ly = y + F.cap_height(face) // 2
    etched_h(c, T, x + tw + T.s(5), ly, w - tw - T.s(5), ramp)
    return F.cap_height(face) + T.s(6)


def chip(c, T, x, y, text, ramp="green", face=None, icon=None):
    """Small coloured pill (tags, alert chips, line numbers)."""
    face = face or T.f_small
    tw = F.measure(text, face)
    iw = (icon.w + T.s(2)) if icon is not None else 0
    w, h = tw + iw + T.s(8), max(F.cap_height(face) + T.s(6), (icon.h + T.s(2)) if icon is not None else 0)
    c.fill(x - T.b, y - T.b, w + 2 * T.b, h + 2 * T.b, R(ramp, 0))
    bevel(c, x, y, w, h, ramp, "out", 4, T.b, 6, 2)
    if icon is not None:
        c.blit(icon, x + T.s(3), y + (h - icon.h) // 2)
    c.text(x + T.s(4) + iw, y + (h - F.cap_height(face)) // 2, text, face, WHITE, outline=R(ramp, 1))
    return w, h
