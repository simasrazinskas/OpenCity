"""iso_life_mbubble - RCT2 thought-bubble builder for world status icons."""
import numpy as np

from iso_life_common import canvas, blit
from iso_life_mdraw import (col, px, ellipse_mask, outline_mask, add_outline, mix)

DARK = "#26262c"
CREAM = "#f6efd8"
TIERS = {
    "info": "#3a78d8", "problem": "#f0c020", "warning": "#f08020", "major": "#d83020",
    "error": "#8a1a1a", "fatal": "#101014", "good": "#38b848",
}
BW, BH = 20, 15          # bubble body
CW, CH = 20, 21          # full sprite incl. trailing dots


def body_mask():
    w, h = BW, BH + 1
    m = np.zeros((h, w), bool)
    ys, xs = np.mgrid[0:h, 0:w]
    # rounded rect body
    rr = (ys >= 3) & (ys <= BH - 1) & (xs >= 0) & (xs <= BW - 1)
    for cx, cy in [(0, 3), (BW - 1, 3), (0, BH - 1), (BW - 1, BH - 1)]:
        corner = (abs(xs - cx) + abs(ys - cy)) == 0
        rr &= ~corner
    m |= rr
    m |= ellipse_mask(w, h, 5.5, 5.0, 5.2, 4.6)
    m |= ellipse_mask(w, h, 13.5, 5.0, 5.4, 4.6)
    m |= ellipse_mask(w, h, 9.5, 3.2, 3.6, 3.0)
    m[BH:] = False
    return m[:BH]


_BM = body_mask()


def bubble(glyph, tier="problem", dy=0, tint=True):
    """glyph: fill art (<=8x8). Returns CW x (CH+dy margin) sprite; dy shifts the bubble down (bob)."""
    tcol = col(TIERS[tier])
    img2 = canvas(CW + 2, CH + 4)
    m2 = np.zeros((CH + 4, CW + 2), bool)
    m2[2:2 + BH, 1:1 + BW] = _BM
    rim = m2 & outline_mask_inv(m2)
    fill = col(mix(CREAM, tcol, 0.16) if tint else col(CREAM))
    img2[m2] = fill
    img2[rim] = tcol
    # dark outline
    img2[outline_mask(m2)] = col(DARK)
    # dots (below body, centred x=11)
    bx = 10
    y0 = 2 + BH + 1
    for (x, y) in [(bx - 1, y0 + 1), (bx, y0 + 1), (bx - 2, y0 + 2), (bx + 1, y0 + 2),
                   (bx - 1, y0 + 3), (bx, y0 + 3)]:
        px(img2, x, y, DARK)
    px(img2, bx - 1, y0 + 2, fill)
    px(img2, bx, y0 + 2, fill)
    for (x, y) in [(bx - 2, y0 + 5), (bx - 1, y0 + 5), (bx - 2, y0 + 6), (bx - 1, y0 + 6)]:
        px(img2, x, y, DARK)
    # glyph
    g = add_outline(glyph, DARK)
    gh, gw = g.shape[:2]
    gx = 1 + (BW - gw) // 2
    gy = 2 + (BH - gh) // 2 + 1
    blit(img2, g, gx, gy)
    # shift for bob and crop vertical margin to constant height
    out = canvas(CW + 2, CH + 4 + 3)
    blit(out, img2, 0, dy + 1)
    return out


def outline_mask_inv(m):
    """Pixels of m that touch the outside (1 px inner rim, 4-adjacent)."""
    n = np.zeros_like(m)
    p = np.pad(m, 1)
    n = p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
    return ~n
