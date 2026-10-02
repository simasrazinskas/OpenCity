"""iso_life_mroute - transit route line tiles, stop pins, flags and path dots (64x32 iso tiles)."""
import numpy as np

from iso_life_common import canvas, blit
from iso_life_mdraw import col, outline_mask, shade, px, rect, line
import iso_life_mgeo as G

DARK = "#26262c"
LINES = {
    "bus": "#f08a20", "tram": "#d83a30", "train": "#2f64c8",
    "metro": "#8a4ac0", "taxi": "#f0d020", "cargo": "#8a5a30",
}
LINE_NAMES = {"bus": "Bus", "tram": "Tram", "train": "Train", "metro": "Metro", "taxi": "Taxi", "cargo": "Cargo"}

CENTRE = (32, 16)
MID = {"xneg": (16, 8), "xpos": (48, 24), "yneg": (48, 8), "ypos": (16, 24)}
DIAMOND = G.poly_mask(64, 32, [(32, 0), (64, 16), (32, 32), (0, 16)])


def _seg_mask(p0, p1, half, ext=0.0):
    """Mask of pixels within `half` vertical px of segment p0->p1 (x range inclusive), 64x32."""
    ys, xs = np.mgrid[0:32, 0:64]
    x0, y0 = p0
    x1, y1 = p1
    if ext:
        dx, dy = x1 - x0, y1 - y0
        x1, y1 = x1 + dx * ext, y1 + dy * ext
    lo, hi = min(x0, x1), max(x0, x1)
    t = (xs + 0.5 - x0) / (x1 - x0)
    yl = y0 + t * (y1 - y0)
    return (xs + 0.5 >= lo - 0.5) & (xs + 0.5 <= hi + 0.5) & (np.abs(ys + 0.5 - yl) <= half)


def line_mask(conns, width=4):
    half = width / 2.0
    m = np.zeros((32, 64), bool)
    for c in conns:
        m |= _seg_mask(CENTRE, MID[c], half, ext=0.35)
    if len(conns) == 1:                                 # round-ish cap at the centre
        ys, xs = np.mgrid[0:32, 0:64]
        m |= ((xs + 0.5 - 32) / 3.0) ** 2 + ((ys + 0.5 - 16) / 2.0) ** 2 <= 1.0
    return m


def line_tile(name, conns, width=4):
    """3 px wide route line with bevel (light top row, dark bottom row) and 1 px dark outline. 64x32."""
    base = LINES[name]
    m = line_mask(conns, width)
    out_m = outline_mask(m)
    img = canvas(64, 32)
    ys, xs = np.mgrid[0:32, 0:64]
    img[m] = col(base)
    up = m & ~np.roll(m, 1, 0)
    dn = m & ~np.roll(m, -1, 0)
    img[up] = col(shade(base, 1.28))
    img[dn] = col(shade(base, 0.78))
    img[out_m] = col(DARK)
    img[~DIAMOND] = 0
    return img


TILE_SET = [
    ("ew", ["xneg", "xpos"], "Straight E-W"),
    ("ns", ["yneg", "ypos"], "Straight N-S"),
    ("c-wn", ["xneg", "yneg"], "Corner W-N"),
    ("c-ne", ["yneg", "xpos"], "Corner N-E"),
    ("c-es", ["xpos", "ypos"], "Corner E-S"),
    ("c-sw", ["ypos", "xneg"], "Corner S-W"),
    ("cap-w", ["xneg"], "End cap W"),
    ("cap-e", ["xpos"], "End cap E"),
    ("cap-n", ["yneg"], "End cap N"),
    ("cap-s", ["ypos"], "End cap S"),
]
COMPASS = {"xp": "e", "xn": "w", "yp": "s", "yn": "n"}


def chevron_tile(name, direction):
    """Straight line tile with a white direction chevron on top. direction in xp, xn, yp, yn."""
    axis = ["xneg", "xpos"] if direction[0] == "x" else ["yneg", "ypos"]
    img = line_tile(name, axis)
    sgn = 1 if direction[1] == "p" else -1
    fx, fy = (sgn, 0) if direction[0] == "x" else (0, sgn)
    sx_, sy_ = (0, 1) if direction[0] == "x" else (1, 0)

    def P(x, y):
        return G.proj(x, y, 0, 32.0, 0.0)

    cx, cy = 0.5, 0.5
    pts = [
        (cx + fx * .17, cy + fy * .17),                              # tip
        (cx - fx * .06 + sx_ * .19, cy - fy * .06 + sy_ * .19),
        (cx - fx * .01, cy - fy * .01),                              # notch
        (cx - fx * .06 - sx_ * .19, cy - fy * .06 - sy_ * .19),
    ]
    mk = G.poly_mask(64, 32, [P(*p) for p in pts])
    o = outline_mask(mk)
    img[o] = col(DARK)
    img[mk] = col("#ffffff")
    return img


def path_dots(conns, frame, period=8, color="#ffffff"):
    """Follow-path dots along the route tile centreline; frame 0..3 shifts by period/4 px."""
    img = canvas(64, 32)
    if len(conns) == 2:
        a, b = conns
        pts = [MID[a], CENTRE, MID[b]]
    else:
        pts = [CENTRE, MID[conns[0]]]
    # walk along dx-arc
    total = sum(abs(pts[i + 1][0] - pts[i][0]) for i in range(len(pts) - 1))
    s = (frame * period / 4.0) % period
    dots = []
    while s <= total:
        acc = 0
        for i in range(len(pts) - 1):
            seg = abs(pts[i + 1][0] - pts[i][0])
            if acc + seg >= s:
                t = (s - acc) / seg
                x = pts[i][0] + t * (pts[i + 1][0] - pts[i][0])
                y = pts[i][1] + t * (pts[i + 1][1] - pts[i][1])
                dots.append((int(round(x)), int(round(y))))
                break
            acc += seg
        s += period
    m = np.zeros((32, 64), bool)
    for x, y in dots:
        m[y - 1:y + 1, x - 1:x + 1] = True
    img[outline_mask(m)] = col(DARK)
    img[m] = col(color)
    img[~DIAMOND] = 0
    return img


def pin(name):
    """Stop marker: round pin in line colour, white centre, dark outline. 13x17, tip at bottom centre."""
    w, h = 13, 17
    img = canvas(w, h)
    c = col(LINES[name])
    ys, xs = np.mgrid[0:h, 0:w]
    head = ((xs - 6.0) / 5.6) ** 2 + ((ys - 6.0) / 5.6) ** 2 <= 1.0
    tail = (ys >= 8) & (ys <= 14) & (np.abs(xs - 6.0) <= (14.5 - ys) * 0.62)
    m = head | tail
    m2 = np.pad(m, 1)
    big = canvas(w + 2, h + 2)
    big[1:-1, 1:-1][m] = c
    mm = np.pad(m, 1)
    big[outline_mask(mm)] = col(DARK)
    # shade right half darker, highlight left
    ys2, xs2 = np.mgrid[0:h + 2, 0:w + 2]
    right = mm & (xs2 >= 9) & (ys2 >= 5)
    big[right] = col(shade(LINES[name], 0.78))
    cen = ((xs2 - 7.0) / 2.6) ** 2 + ((ys2 - 7.0) / 2.6) ** 2 <= 1.0
    big[cen] = col("#ffffff")
    out = canvas(16, 20)
    out[:19, :15] = big
    return out


def flag(name=None):
    """Waypoint flag. White/dark pole with a 6x4 pennant in the line colour (or red if None). 10x18."""
    img = canvas(10, 18)
    fc = LINES[name] if name else "#e03a2e"
    rect(img, 1, 0, 1, 17, DARK)
    rect(img, 2, 0, 2, 16, "#d8d8d0")
    for y, wd in enumerate([7, 7, 6, 5, 3, 2]):
        for x in range(wd):
            px(img, 3 + x, y + 1, fc)
    for y in range(7):
        pass
    # shade bottom pennant row
    for x in range(3, 8):
        pass
    m = img[..., 3] > 0
    big = canvas(12, 20)
    big[1:-1, 1:-1] = img
    mm = big[..., 3] > 0
    big[outline_mask(mm)] = col(DARK)
    # re-draw the pole shaft over its outline on the left for a cleaner look
    return big
