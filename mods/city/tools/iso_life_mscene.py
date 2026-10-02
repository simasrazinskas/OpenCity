"""iso_life_mscene - composite 'markers in context' scene: a 4x3 patch with grass, a road, two houses, markers."""
import numpy as np

from iso_life_common import canvas, blit
from iso_life_mdraw import col, shade, BAYER4
import iso_life_mgeo as G
import iso_life_msel as S
import iso_life_mroute as R
from iso_life_mbubble import bubble
from iso_life_mglyphs_a import GLYPHS
import iso_life_mglyphs_b  # noqa: F401

W, H = 256, 184
OX, OY = 112, 66           # screen position of the top corner of cell (0, 0)
BUBBLE_ANCHOR = (11, 26)   # bottom-centre of the 22x28 bubble sprite


def cell_top(cx, cy):
    return OX + (cx - cy) * 32, OY + (cx + cy) * 16


def _tile(kind, cx, cy):
    """64x32 ground tile with ordered-dither speckle (grass) or asphalt (road)."""
    m = G.poly_mask(64, 32, [(32, 0), (64, 16), (32, 32), (0, 16)])
    img = canvas(64, 32)
    ys, xs = np.mgrid[0:32, 0:64]
    if kind == "grass":
        base, alt = "#5f9a46", "#6aa84e"
        img[m] = col(base)
        sp = m & (((xs * 7 + ys * 13 + cx * 5 + cy * 11) % 9) == 0)
        img[sp] = col(alt)
        sp2 = m & (((xs * 5 + ys * 3 + cx * 3 + cy * 7) % 17) == 0)
        img[sp2] = col("#4f8a3c")
    else:
        img[m] = col("#6a6c74")
        sp = m & (((xs * 3 + ys * 5) % 11) == 0)
        img[sp] = col("#74767e")
        # centre dashes along X
        cl = m & (np.abs((ys + 0.5) - (16 + (xs + 0.5 - 32) * 0.5)) < 0.9) & (((xs // 4) % 2) == 0)
        img[cl] = col("#d8c860")
    # darker lower-right edges, light upper-left edges
    edge = m & ~np.roll(m, -1, 0)
    img[edge] = col("#3f6a30" if kind == "grass" else "#4a4c54")
    return img


def scene():
    img = canvas(W, H)
    bg = canvas(W, H)
    # ground
    for d in range(0, 7):
        for cx in range(4):
            cy = d - cx
            if 0 <= cy < 3:
                t = _tile("road" if cy == 2 else "grass", cx, cy)
                x, y = cell_top(cx, cy)
                blit(img, t, x - 32, y)
    house = G.house()
    house2 = G.house(wall="#c8d0c0", roof="#4a6a98")
    ghost_ok = S.ghost_building(G.house(), True)
    ghost_bad = S.ghost_building(G.house(), False)

    def spot(cx, cy):
        x, y = cell_top(cx, cy)
        return x - 32, y - 32

    def fp(nx_, ny_, cx, cy, im):
        x, y = cell_top(cx, cy)
        ox, oy = G.fp_origin(nx_, ny_)
        blit(img, im, x - ox, y - oy)

    # footprints under things
    fp(1, 1, 0, 1, S.ghost_fp(1, 1, True))
    fp(1, 1, 2, 1, S.ghost_fp(1, 1, False))
    # route line on the road row (cy = 2): cap, straight, straight, cap  (W = -X, E = +X)
    seq = [(0, ["xpos"]), (1, ["xneg", "xpos"]), (2, ["xneg", "xpos"]), (3, ["xneg"])]
    for cx, conns in seq:
        x, y = cell_top(cx, 2)
        blit(img, R.line_tile("bus", conns), x - 32, y)
    # selected brackets (back part drawn under the house is not needed: draw after house for clarity)
    # depth-sorted objects
    objs = [
        (1, "house", 1, 0, house),
        (3, "houseSel", 3, 0, house2),
        (1, "ghostok", 0, 1, ghost_ok),
        (3, "ghostbad", 2, 1, ghost_bad),
    ]
    for _, kind, cx, cy, im in sorted(objs, key=lambda o: o[0]):
        x, y = spot(cx, cy)
        blit(img, im, x, y)
    fp(1, 1, 3, 0, S.brackets(1, 1, "selected", 0))
    # stop pins at the line ends (tip at the line centre)
    for cx in (0, 3):
        x, y = cell_top(cx, 2)
        p = R.pin("bus")
        blit(img, p, x - p.shape[1] // 2, y + 16 - p.shape[0] + 1)
    # status bubbles above roofs (bottom-centre 9 px above (cell centre y - 24 px building height))
    for (cx, cy, name, tier) in [(1, 0, "nopower", "warning"), (3, 0, "unhappy", "problem")]:
        x, y = cell_top(cx, cy)
        b = bubble(GLYPHS[name](), tier)
        blit(img, b, x - BUBBLE_ANCHOR[0], y + 16 - 24 - 9 - BUBBLE_ANCHOR[1])
    return img
