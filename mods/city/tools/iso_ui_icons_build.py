"""OpenCity RCT2-style icons: build-menu category tabs, toolbar tools, network tools (see iso_ui_icon_dsl)."""

import math

from iso_ui_icon_dsl import icon
from iso_ui_icons_core import grass_tile, bolt, drop, person, lens, badge   # noqa: F401  (shared motifs)


# ---- local helpers -------------------------------------------------------------------------------------
# Tab scenes sit on a small tile (l=6): tile-local coords x, y in [-6, 0] (front corner = 0, 0), z above the
# grass surface. G() maps them to the 16-grid.
def tile(I, l=6, ramp="green", shade=5):
    I.isotile(8, 15, l, l, ramp, shade, 1.5, "brown")


def G(x, y, z=0.0):
    return I_P(8, 15, x, y, z + 1.5)


def I_P(fx, fy, x, y, z):
    return (fx + (x - y), fy + (x + y) * 0.5 - z)


def Q(I, pts, c, s=5):
    """Polygon from tile-local 3D points (x, y, z)."""
    I.poly([G(*p) for p in pts], c, s)


def box(I, x0, y0, lx, ly, h, ramp, top=6, left=5, right=3, z=0.0):
    fx, fy = G(x0, y0, z)
    I.isobox(fx, fy, lx, ly, h, ramp, top, left, right)


def lface(I, y, xa, xb, za, zb, c, s=5):
    """Rect on a +Y (left, lit) face at plane y."""
    Q(I, [(xa, y, za), (xb, y, za), (xb, y, zb), (xa, y, zb)], c, s)


def rface(I, x, ya, yb, za, zb, c, s=5):
    """Rect on a +X (right, shaded) face at plane x."""
    Q(I, [(x, ya, za), (x, yb, za), (x, yb, zb), (x, ya, zb)], c, s)


def tree(I, cx, by, h=8, c="green"):
    """Round-canopy tree; cx = trunk x, by = ground y (16-grid)."""
    I.rect(cx - 0.75, by - h * 0.45, 1.5, h * 0.45, "brown", 3)
    I.ellipse(cx, by - h * 0.65, h * 0.38, h * 0.36, c, 3)
    I.ellipse(cx - 0.2, by - h * 0.7, h * 0.34, h * 0.31, c, 4)
    I.ellipse(cx - h * 0.1, by - h * 0.78, h * 0.18, h * 0.16, c, 6)


def pipe(I, x0, y0, x1, y1, ramp, t=3.0, cap=True):
    """Pipe along the iso x-axis (screen down-right), 16-grid endpoints."""
    I.line(x0, y0, x1, y1, ramp, 3, t)
    I.line(x0 - 0.2, y0 - 0.5, x1 - 0.2, y1 - 0.5, ramp, 5, t * 0.6)
    I.line(x0 - 0.4, y0 - 0.9, x1 - 0.4, y1 - 0.9, ramp, 7, max(1.0, t * 0.25))
    if cap:
        I.ellipse(x1, y1, t * 0.5, t * 0.62, ramp, 4)
        I.ellipse(x1, y1, t * 0.28, t * 0.35, ramp, 1)
    for u in (0.12, 0.88):
        fx, fy = x0 + (x1 - x0) * u, y0 + (y1 - y0) * u
        I.ellipse(fx, fy - 0.5, 0.75, t * 0.58, "grey", 4)
        I.ellipse(fx - 0.2, fy - 0.7, 0.4, t * 0.45, "grey", 7)


def star(I, cx, cy, r, c="yellow", s=5):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    I.poly(pts, c, s)


def arrow(I, cx, y0, y1, w=7, sw=3, c="green", up=True):
    """Vertical arrow between y0 (top) and y1 (bottom) with chunky shaft and lit left half."""
    hh = w * 0.75
    if up:
        head = [(cx, y0), (cx + w / 2, y0 + hh), (cx - w / 2, y0 + hh)]
        I.rect(cx - sw / 2, y0 + hh - 0.5, sw, y1 - y0 - hh + 0.5, c, 5)
        I.rect(cx - sw / 2, y0 + hh - 0.5, sw / 2, y1 - y0 - hh + 0.5, c, 6)
    else:
        head = [(cx, y1), (cx + w / 2, y1 - hh), (cx - w / 2, y1 - hh)]
        I.rect(cx - sw / 2, y0, sw, y1 - y0 - hh + 0.5, c, 5)
        I.rect(cx - sw / 2, y0, sw / 2, y1 - y0 - hh + 0.5, c, 6)
    I.poly(head, c, 5)
    if up:
        I.poly([head[0], head[2], (cx, y0 + hh)], c, 6)
    else:
        I.poly([head[0], head[2], (cx, y1 - hh)], c, 6)


def puff(I, cx, cy, r=1.6):
    I.ellipse(cx, cy, r, r * 0.8, "grey", 6)
    I.ellipse(cx - r * 0.5, cy + r * 0.3, r * 0.7, r * 0.6, "grey", 7)


# ---- build tabs ----------------------------------------------------------------------------------------
@icon("cat_roads", "build", "Roads")
def _(I):
    tile(I, 7)
    Q(I, [(0, -2.2, 0), (-7, -2.2, 0), (-7, -4.8, 0), (0, -4.8, 0)], "grey", 3)
    Q(I, [(-2.2, 0, 0), (-4.8, 0, 0), (-4.8, -7, 0), (-2.2, -7, 0)], "grey", 3)
    Q(I, [(-2.2, -2.2, 0), (-4.8, -2.2, 0), (-4.8, -4.8, 0), (-2.2, -4.8, 0)], "grey", 4)
    for xa in (-0.3, -5.5):
        Q(I, [(xa, -3.4, 0), (xa - 1.3, -3.4, 0), (xa - 1.3, -3.6, 0), (xa, -3.6, 0)], "yellow", 6)
    for ya in (-0.3, -5.5):
        Q(I, [(-3.4, ya, 0), (-3.4, ya - 1.3, 0), (-3.6, ya - 1.3, 0), (-3.6, ya, 0)], "yellow", 6)
    # kerb highlights + a little car
    Q(I, [(0, -2.2, 0), (-2.2, -2.2, 0), (-2.2, -2.5, 0), (0, -2.5, 0)], "grey", 6)
    box(I, -0.9, -2.6, 2.4, 1.5, 0.9, "red", 6, 5, 3)
    box(I, -1.5, -2.9, 1.2, 1.0, 0.8, "grey", 7, 6, 4, z=0.9)


@icon("cat_zoning", "build", "Zoning")
def _(I):
    I.isobox(8, 15, 7, 7, 1.5, "brown", 4, 4, 2)
    I.isoquad(8, 15, [(0, 0, 1.5), (-7, 0, 1.5), (-7, -7, 1.5), (0, -7, 1.5)], "grey", 3)
    q = lambda x0, y0, x1, y1, c: I.isoquad(8, 15, [(x0, y0, 1.5), (x1, y0, 1.5), (x1, y1, 1.5), (x0, y1, 1.5)], c, 5)
    q(-0.5, -0.5, -3.25, -3.25, "green")
    q(-3.75, -0.5, -6.5, -3.25, "blue")
    q(-0.5, -3.75, -3.25, -6.5, "yellow")
    q(-3.75, -3.75, -6.5, -6.5, "orange")
    # lit front strip on the two front squares
    I.isoquad(8, 15, [(-0.5, -0.5, 1.5), (-3.25, -0.5, 1.5), (-3.25, -1.1, 1.5), (-0.5, -1.1, 1.5)], "green", 6)
    I.isoquad(8, 15, [(-3.75, -0.5, 1.5), (-6.5, -0.5, 1.5), (-6.5, -1.1, 1.5), (-3.75, -1.1, 1.5)], "blue", 6)


@icon("cat_networks", "build", "Networks")
def _(I):
    tile(I, 7)
    ax, ay = G(-6.2, -1.6, 0.8)
    bx, by = G(-0.8, -1.6, 0.8)
    pipe(I, ax, ay, bx, by, "blue", 2.6)
    # pylon (A-frame lattice) standing behind the pipe
    I.poly([(8, 0.8), (9.2, 0.8), (11, 10.0), (9.5, 10.0), (8.6, 5.5), (7.7, 10.0), (6.2, 10.0)], "grey", 5)
    I.rect(6.0, 3.0, 6.0, 1, "grey", 4)
    I.rect(6.6, 6.2, 4.6, 1, "grey", 4)
    I.rect(7.9, 4.0, 1.2, 1, "grey", 7)
    I.line(1.5, 5.2, 6.0, 3.5, "yellow", 5, 1)
    I.line(12, 3.5, 14.5, 5.8, "yellow", 5, 1)


@icon("cat_power", "build", "Electricity")
def _(I):
    tile(I)
    # cooling tower
    I.poly([(2.5, 12.2), (3.6, 8.2), (4.4, 5.6), (7.8, 5.6), (8.6, 8.2), (9.8, 12.2)], "grey", 5)
    I.poly([(6.1, 5.6), (7.8, 5.6), (8.6, 8.2), (9.8, 12.2), (7, 12.2)], "grey", 3)
    I.poly([(2.5, 12.2), (3.6, 8.2), (4.4, 5.6), (5.4, 5.6), (4.8, 8.2), (4.2, 12.2)], "grey", 6)
    I.ellipse(6.1, 5.6, 1.9, 0.7, "grey", 2)
    puff(I, 5, 3.4, 1.9)
    puff(I, 8, 2.6, 1.4)
    # chimney + bolt
    I.rect(11.5, 5.5, 2, 6.5, "red", 5)
    I.rect(11.5, 5.5, 0.8, 6.5, "red", 6)
    I.rect(11.5, 7.2, 2, 0.9, "grey", 7)
    I.rect(11.5, 9.0, 2, 0.9, "grey", 7)
    bolt(I, 3.4, 8.0, 0.62)


@icon("cat_water", "build", "Water")
def _(I):
    tile(I)
    # water tower: legs, tank, cone roof
    I.line(5, 11.5, 6.2, 6.5, "brown", 3, 1.2)
    I.line(11, 11.5, 9.8, 6.5, "brown", 3, 1.2)
    I.line(5.8, 9, 10.2, 9, "brown", 4, 1)
    I.line(6.2, 8.6, 9.8, 11.5, "brown", 3, 0.8)
    I.rect(4.4, 3.4, 7.2, 4.0, "blue", 4)
    I.rect(4.4, 3.4, 2.4, 4.0, "blue", 6)
    I.rect(9.6, 3.4, 2.0, 4.0, "blue", 3)
    I.rect(4.4, 5.0, 7.2, 0.8, "blue", 2)
    I.poly([(8, 0.8), (12, 3.4), (4, 3.4)], "red", 5)
    I.poly([(8, 0.8), (4, 3.4), (7, 3.4)], "red", 6)
    drop(I, 12.4, 11.6, 1.7, "blue", 5)


@icon("cat_police", "build", "Police")
def _(I):
    tile(I)
    box(I, -1.2, -1.2, 4.6, 3.6, 5, "blue", 6, 5, 3)
    lface(I, -1.2, -1.2, -5.8, 3.1, 3.9, "grey", 7)             # white band
    rface(I, -1.2, -1.2, -4.8, 3.1, 3.9, "grey", 5)
    lface(I, -1.2, -2.0, -3.2, 0, 2.4, "grey", 2)               # door
    lface(I, -1.2, -4.0, -5.0, 0.8, 2.4, "blue", 7)             # window
    # siren on the roof
    I.rect(6.2, 1.8, 3.6, 1.5, "grey", 3)
    I.rect(6.4, 0.8, 1.5, 2.2, "red", 5)
    I.rect(8.1, 0.8, 1.5, 2.2, "blue", 6)
    I.rect(6.4, 0.8, 0.7, 1.2, "red", 7)


@icon("cat_fire", "build", "Fire")
def _(I):
    tile(I)
    box(I, -0.8, -0.8, 5.2, 4.2, 4.6, "red", 6, 5, 3)
    lface(I, -0.8, -1.4, -4.2, 0, 3.2, "grey", 6)               # garage door
    for z in (0.9, 1.8, 2.7):
        lface(I, -0.8, -1.4, -4.2, z, z + 0.3, "grey", 3)
    lface(I, -0.8, -0.1, -5.1, 4.6, 5.2, "grey", 7)
    # bell tower
    I.rect(6.7, 0.8, 2.6, 2.6, "red", 4)
    I.poly([(6.3, 0.9), (9.7, 0.9), (8, -0.2)], "grey", 3)
    I.ellipse(8, 2.3, 0.9, 0.9, "yellow", 6)
    # hydrant
    I.rect(12.3, 10.2, 2, 2.6, "red", 5)
    I.ellipse(13.3, 10.2, 1.2, 0.9, "red", 6)
    I.rect(11.7, 11.0, 3.2, 0.8, "yellow", 5)


@icon("cat_health", "build", "Health")
def _(I):
    tile(I)
    box(I, -0.5, -0.5, 5.2, 4.6, 5.5, "grey", 7, 7, 5)
    rface(I, -0.5, -0.5, -4.1, 0, 5.5, "grey", 4)
    # red cross on the front face
    lface(I, -0.5, -2.1, -3.7, 1.0, 5.0, "red", 5)
    lface(I, -0.5, -1.0, -4.8, 2.2, 3.8, "red", 5)
    rface(I, -0.5, -1.2, -2.2, 2.0, 3.8, "blue", 4)
    rface(I, -0.5, -2.6, -3.4, 2.0, 3.8, "blue", 4)


def pyramid(I, x0, y0, l, z, h, ramp, lit=6, dark=3):
    a = G(x0, y0, z); b = G(x0 - l, y0, z); c = G(x0, y0 - l, z)
    ap = G(x0 - l / 2.0, y0 - l / 2.0, z + h)
    I.poly([a, b, ap], ramp, lit)
    I.poly([a, c, ap], ramp, dark)


@icon("cat_education", "build", "Education")
def _(I):
    tile(I)
    box(I, -0.5, -0.5, 6, 4.8, 3.2, "orange", 6, 5, 3)
    fx, fy = G(-0.5, -0.5)
    I.gable(fx, fy, 6, 4.8, 3.2, 2.2, "purple", "x", 6, 3)
    lface(I, -0.5, -1.0, -2.0, 0, 1.9, "brown", 3)         # door
    lface(I, -0.5, -3.0, -4.0, 0.8, 2.4, "blue", 6)
    lface(I, -0.5, -4.6, -5.6, 0.8, 2.4, "blue", 6)
    # bell tower on the roof ridge
    box(I, -2.2, -1.8, 1.8, 1.8, 3.0, "sand", 7, 6, 4, z=4.4)
    lface(I, -1.8, -2.6, -3.4, 5.4, 6.8, "yellow", 5)
    pyramid(I, -2.2, -1.8, 1.8, 7.4, 2.2, "purple", 6, 3)


@icon("cat_parks", "build", "Parks")
def _(I):
    tile(I, 7, "green", 6)
    # sandy path curving to the front corner
    Q(I, [(0, -2.4, 0), (-3.4, -2.4, 0), (-3.4, -7, 0), (-5.2, -7, 0), (-5.2, -1.0, 0), (0, -1.0, 0)], "sand", 6)
    tree(I, 5.0, 10.0, 11)
    tree(I, 11.8, 8.8, 7, "green")
    # bench on the grass at the right
    box(I, -0.6, -3.6, 1.0, 3.0, 0.9, "brown", 6, 5, 3)
    rface(I, -0.6, -3.6, -6.6, 0.9, 2.2, "brown", 3)


@icon("cat_garbage", "build", "Garbage")
def _(I):
    tile(I, 6, "grey", 5)
    box(I, -0.8, -0.8, 5.0, 3.6, 3.8, "green", 5, 4, 2)
    lface(I, -0.8, -0.8, -5.8, 2.6, 3.0, "green", 2)
    lface(I, -0.8, -0.8, -5.8, 1.2, 1.5, "green", 2)
    # lid propped open, sack and litter
    Q(I, [(-0.6, -0.4, 3.8), (-5.4, -0.4, 3.8), (-5.4, -3.2, 5.1), (-0.6, -3.2, 5.1)], "grey", 4)
    I.ellipse(10.4, 4.0, 2.0, 1.7, "yellow", 4)
    I.ellipse(9.9, 3.5, 1.0, 0.8, "yellow", 6)
    I.rect(9.9, 1.8, 1.0, 1.2, "yellow", 3)
    I.rect(4.0, 11.6, 1.4, 1.4, "grey", 7)
    I.rect(11.2, 11.0, 1.4, 1.2, "red", 5)


@icon("cat_deathcare", "build", "Deathcare")
def _(I):
    tile(I, 7, "green", 4)
    # back cross and stone, front rounded tombstone
    I.rect(9.8, 3.4, 3.2, 6.2, "grey", 3)
    I.ellipse(11.4, 3.8, 1.6, 1.6, "grey", 3)
    I.rect(9.8, 4.0, 1.0, 5.6, "grey", 5)
    I.rect(3.3, 4.0, 1.6, 6.0, "grey", 4)
    I.rect(2.0, 5.6, 4.4, 1.5, "grey", 4)
    I.rect(3.3, 4.0, 0.7, 6.0, "grey", 6)
    I.rect(5.6, 6.0, 4.6, 6.2, "grey", 5)
    I.ellipse(7.9, 6.2, 2.3, 2.0, "grey", 5)
    I.rect(5.6, 6.2, 1.6, 6.0, "grey", 7)
    I.rect(9.0, 7.0, 1.2, 5.2, "grey", 3)
    I.rect(6.8, 8.0, 2.4, 0.8, "grey", 3)
    I.rect(7.6, 7.2, 0.8, 2.6, "grey", 3)
    I.rect(5.0, 12.2, 5.8, 0.9, "green", 7)


@icon("cat_comms", "build", "Communications")
def _(I):
    tile(I)
    # radio waves (upper arcs only), then the mast over them
    for r in (3.4, 5.4):
        I.ring(6.4, 3.4, r, 1, "yellow", 6)
    I.rect(0, 3.4, 16, 12, None)
    tile(I)
    I.poly([(5.8, 1.8), (7.0, 1.8), (8.8, 11.2), (7.4, 11.2), (6.4, 5), (5.4, 11.2), (4.0, 11.2)], "teal", 5)
    I.rect(4.9, 4.8, 3.0, 0.9, "teal", 3)
    I.rect(4.4, 7.8, 4.0, 0.9, "teal", 3)
    I.rect(5.8, 1.8, 0.7, 3.0, "teal", 7)
    I.ellipse(6.4, 1.6, 1.0, 1.0, "red", 5)
    # envelope
    I.rect(8.4, 8.4, 6.6, 4.8, "grey", 7)
    I.rect(8.4, 12.4, 6.6, 0.8, "grey", 4)
    I.poly([(8.4, 8.4), (15, 8.4), (11.7, 11.0)], "grey", 5)
    I.line(8.6, 12.6, 11.7, 10.4, "grey", 4, 0.8)
    I.line(14.8, 12.6, 11.7, 10.4, "grey", 4, 0.8)
    I.ellipse(11.7, 10.9, 0.9, 0.9, "red", 5)


@icon("cat_admin", "build", "Administration")
def _(I):
    tile(I, 7, "green", 5)
    box(I, -0.2, -0.4, 6.6, 4.8, 4.2, "sand", 7, 7, 5)
    lface(I, -0.4, -0.2, -6.8, 3.2, 4.2, "sand", 5)              # entablature shadow
    for xa in (-0.9, -2.5, -4.1, -5.7):
        lface(I, -0.4, xa, xa - 0.9, 0, 3.2, "sand", 4)           # columns
        lface(I, -0.4, xa - 0.5, xa - 0.9, 0, 3.2, "sand", 2)
    lface(I, -0.4, -2.8, -4.4, 0, 2.0, "brown", 3)
    # dome on a drum, flag on top
    I.ellipse(7.3, 5.2, 2.7, 2.9, "teal", 4)
    I.ellipse(6.6, 4.4, 1.6, 1.8, "teal", 6)
    I.rect(4.6, 5.4, 5.4, 1.4, "sand", 6)
    I.rect(4.6, 5.4, 5.4, 0.5, "sand", 4)
    I.rect(7.0, 0.4, 0.8, 2.6, "grey", 4)
    I.rect(7.8, 0.4, 1.8, 1.1, "red", 5)


@icon("cat_industry", "build", "Industry")
def _(I):
    tile(I, 7, "green", 4)
    # brick chimney with smoke at the back
    box(I, -0.2, -5.0, 1.6, 1.6, 8.4, "red", 5, 5, 3)
    I.rect(10.6, 3.4, 2.6, 0.9, "grey", 6)
    puff(I, 12.2, 2.2, 1.7)
    puff(I, 14.0, 1.6, 1.2)
    # factory hall with sawtooth roof
    box(I, -0.4, -0.4, 6.4, 4.4, 3.6, "orange", 6, 5, 3)
    lface(I, -0.4, -1.0, -2.2, 0, 2.0, "grey", 3)                # loading door
    lface(I, -0.4, -3.0, -4.0, 1.4, 2.6, "blue", 6)
    lface(I, -0.4, -4.6, -5.6, 1.4, 2.6, "blue", 6)
    h, rh, w, ly = 3.6, 1.5, 2.1, 4.4
    for k in range(3):
        xa = -0.4 - k * w
        xb = xa - w
        y0 = -0.4
        # slope surface (rises toward +x side), glazed vertical face on the dark side, front triangle
        Q(I, [(xb, y0, h), (xa, y0, h + rh), (xa, y0 - ly, h + rh), (xb, y0 - ly, h)], "grey", 6)
        Q(I, [(xa, y0, h), (xa, y0 - ly, h), (xa, y0 - ly, h + rh), (xa, y0, h + rh)], "blue", 4)
        Q(I, [(xb, y0, h), (xa, y0, h + rh), (xa, y0, h)], "orange", 5)


@icon("cat_transit", "build", "Transit")
def _(I):
    tile(I, 7, "grey", 4)
    Q(I, [(0, -2.6, 0), (-7, -2.6, 0), (-7, -6.6, 0), (0, -6.6, 0)], "grey", 3)
    Q(I, [(0, -4.5, 0), (-1.2, -4.5, 0), (-1.2, -4.7, 0), (0, -4.7, 0)], "yellow", 6)
    Q(I, [(-3.2, -4.5, 0), (-4.4, -4.5, 0), (-4.4, -4.7, 0), (-3.2, -4.7, 0)], "yellow", 6)
    # bus: long iso box, windows band, wheels
    box(I, -0.8, -2.8, 6.2, 3.2, 3.4, "teal", 6, 5, 3, z=0.5)
    lface(I, -2.8, -1.0, -6.0, 1.9, 3.3, "grey", 7)             # window band on the lit side
    for xa in (-2.6, -4.2):
        lface(I, -2.8, xa, xa - 0.25, 1.9, 3.3, "teal", 5)
    rface(I, -0.8, -3.0, -5.6, 1.6, 3.2, "blue", 6)
    for wx in (-1.8, -4.6):
        cx, cy = G(wx, -2.8, 0.9)
        I.ellipse(cx, cy, 1.1, 1.1, "grey", 1)
        I.ellipse(cx - 0.2, cy - 0.2, 0.5, 0.5, "grey", 4)
    # bus-stop sign
    I.rect(2.2, 3.0, 0.8, 8.0, "grey", 5)
    I.rect(1.0, 1.4, 3.2, 2.8, "blue", 5)
    I.rect(1.6, 2.0, 2.0, 1.2, "grey", 7)


@icon("cat_signature", "build", "Landmarks")
def _(I):
    tile(I)
    # needle tower: stem, saucer, spire
    I.poly([(7.2, 6.2), (8.8, 6.2), (9.6, 12.2), (6.4, 12.2)], "grey", 5)
    I.rect(7.2, 6.2, 0.8, 6.0, "grey", 7)
    I.poly([(6.4, 12.2), (9.6, 12.2), (11, 13), (5, 13)], "grey", 4)
    I.ellipse(8, 6.4, 4.6, 1.8, "teal", 4)
    I.ellipse(7.4, 6.0, 3.2, 1.1, "teal", 6)
    I.rect(3.6, 6.4, 8.8, 0.9, "teal", 2)
    I.rect(7.5, 4.0, 1.0, 2.2, "grey", 5)
    star(I, 8, 2.8, 2.6, "yellow", 5)
    I.ellipse(7.4, 2.4, 0.8, 0.8, "yellow", 7)


@icon("cat_landscaping", "build", "Landscaping")
def _(I):
    I.isotile(8, 15, 7, 7, "green", 5, 1.5, "brown")
    # raised terrace hill
    I.isobox(8, 12.6, 5.4, 5.4, 2.2, "brown", 4, 4, 2)
    I.isoquad(8, 12.6, [(0, 0, 2.2), (-5.4, 0, 2.2), (-5.4, -5.4, 2.2), (0, -5.4, 2.2)], "green", 6)
    I.isobox(8, 9.8, 2.8, 2.8, 1.6, "brown", 4, 4, 2)
    I.isoquad(8, 9.8, [(0, 0, 1.6), (-2.8, 0, 1.6), (-2.8, -2.8, 1.6), (0, -2.8, 1.6)], "green", 7)
    tree(I, 8, 7.4, 7)


@icon("cat_districts", "build", "Districts")
def _(I):
    tile(I, 7, "green", 5)
    # dashed district boundary
    for t in (0.2, 2.4, 4.6):
        Q(I, [(-t, -0.2, 0), (-t - 1.6, -0.2, 0), (-t - 1.6, -1.2, 0), (-t, -1.2, 0)], "yellow", 6)
        Q(I, [(-0.2, -t, 0), (-0.2, -t - 1.6, 0), (-1.2, -t - 1.6, 0), (-1.2, -t, 0)], "yellow", 6)
    # flag
    I.rect(7.4, 2.0, 0.9, 9.2, "grey", 5)
    I.poly([(8.3, 2.0), (13.6, 3.8), (8.3, 6.0)], "red", 5)
    I.poly([(8.3, 2.0), (13.6, 3.8), (8.3, 3.6)], "red", 6)
    I.ellipse(7.8, 11.4, 1.8, 0.8, "grey", 3)


# ---- tools ---------------------------------------------------------------------------------------------
@icon("tool_bulldoze", "tools", "Bulldozer")
def _(I):
    # tracks
    I.rect(2.0, 10.4, 9.6, 3.8, "grey", 2)
    I.ellipse(2.6, 12.3, 1.9, 1.9, "grey", 2)
    I.ellipse(11.0, 12.3, 1.9, 1.9, "grey", 2)
    for x in (3.4, 5.6, 7.8, 10.0):
        I.rect(x, 12.0, 0.9, 0.9, "grey", 5)
    I.ellipse(4.0, 12.3, 1.1, 1.1, "grey", 4)
    I.ellipse(9.6, 12.3, 1.1, 1.1, "grey", 4)
    # body, cab, exhaust
    I.rect(3.0, 6.4, 8.4, 4.4, "yellow", 5)
    I.rect(3.0, 6.4, 8.4, 1.2, "yellow", 6)
    I.rect(3.0, 9.6, 8.4, 1.2, "yellow", 3)
    I.rect(3.0, 2.6, 4.6, 4.2, "yellow", 5)
    I.rect(3.0, 2.6, 4.6, 0.9, "yellow", 7)
    I.rect(4.0, 3.6, 2.6, 2.2, "blue", 6)
    I.rect(8.6, 3.0, 1.1, 3.4, "grey", 3)
    # blade with arm
    I.rect(11.0, 9.0, 2.6, 1.0, "yellow", 3)
    I.rect(12.6, 5.6, 2.2, 8.0, "grey", 5)
    I.rect(12.6, 5.6, 0.8, 8.0, "grey", 7)
    I.rect(14.2, 5.6, 0.6, 8.0, "grey", 3)


@icon("tool_dezone", "tools", "De-zone")
def _(I):
    I.isobox(8, 14.5, 7, 7, 1.5, "brown", 4, 4, 2)
    I.isoquad(8, 14.5, [(0, 0, 1.5), (-7, 0, 1.5), (-7, -7, 1.5), (0, -7, 1.5)], "green", 5)
    I.isoquad(8, 14.5, [(0, 0, 1.5), (-7, 0, 1.5), (-7, -1, 1.5), (0, -1, 1.5)], "green", 6)
    for (a, b, c, d) in ((4.8, 4.0, 11.2, 11.0), (11.2, 4.0, 4.8, 11.0)):
        I.line(a, b, c, d, "red", 0, 3.6)
    for (a, b, c, d) in ((4.8, 4.0, 11.2, 11.0), (11.2, 4.0, 4.8, 11.0)):
        I.line(a, b, c, d, "red", 5, 2.4)
    I.line(5.0, 3.8, 8.0, 7.1, "red", 7, 1.0)


@icon("tool_area_paint", "tools", "Area paint")
def _(I):
    tile(I, 6, "blue", 5)
    I.isoquad(8, 15, [(-0.5, -0.5, 1.5), (-3.2, -0.5, 1.5), (-3.2, -5.5, 1.5), (-0.5, -5.5, 1.5)], "blue", 6)
    # roller: head, frame, handle
    I.rect(2.2, 1.6, 8.8, 3.6, "red", 5)
    I.rect(2.2, 1.6, 8.8, 1.1, "red", 7)
    I.rect(2.2, 4.2, 8.8, 1.0, "red", 3)
    I.rect(10.4, 3.0, 2.4, 1.0, "grey", 5)
    I.rect(11.8, 3.0, 1.0, 5.2, "grey", 5)
    I.rect(8.8, 7.6, 4.0, 1.0, "grey", 5)
    I.rect(8.8, 8.4, 1.1, 5.2, "grey", 4)
    I.rect(8.4, 9.6, 1.9, 4.4, "orange", 5)
    I.rect(8.4, 9.6, 0.7, 4.4, "orange", 7)


@icon("tool_area_clear", "tools", "Area clear")
def _(I):
    I.isobox(10.5, 13.0, 7.5, 3.6, 3.4, "blue", 6, 5, 3)
    I.isobox(10.5, 13.0, 3.0, 3.6, 3.4, "red", 7, 6, 4)
    I.rect(1.5, 14.0, 1.0, 0.8, "grey", 6)
    I.rect(3.4, 14.4, 0.8, 0.8, "grey", 5)
    I.rect(12.6, 14.2, 1.2, 0.8, "grey", 6)


@icon("tool_upgrade", "tools", "Upgrade")
def _(I):
    tile(I, 6)
    box(I, -0.8, -0.8, 5.2, 4.0, 4.0, "sand", 7, 6, 4)
    fx, fy = G(-0.8, -0.8)
    I.gable(fx, fy, 5.2, 4.0, 4.0, 2.4, "red", "x", 5, 3)
    lface(I, -0.8, -3.6, -4.6, 0.8, 2.4, "blue", 6)
    arrow(I, 11.6, 1.0, 9.8, 6, 2.6, "green", True)


@icon("tool_move", "tools", "Move")
def _(I):
    c = "blue"
    I.rect(6.9, 3.0, 2.4, 10.0, c, 5)
    I.rect(3.0, 6.9, 10.0, 2.4, c, 5)
    I.rect(6.9, 3.0, 1.0, 10.0, c, 6)
    I.rect(3.0, 6.9, 10.0, 1.0, c, 6)
    I.poly([(8.1, 1.2), (11, 4.6), (5.2, 4.6)], c, 5)
    I.poly([(8.1, 14.8), (11, 11.4), (5.2, 11.4)], c, 4)
    I.poly([(1.2, 8.1), (4.6, 5.2), (4.6, 11)], c, 5)
    I.poly([(14.8, 8.1), (11.4, 5.2), (11.4, 11)], c, 4)
    I.poly([(8.1, 1.2), (5.2, 4.6), (8.1, 4.6)], c, 7)
    I.poly([(1.2, 8.1), (4.6, 5.2), (4.6, 8.1)], c, 7)
    I.rect(7.2, 7.2, 1.8, 1.8, c, 3)


def _brush(I, r, c="orange"):
    for k in range(12):
        a = k * math.pi / 6
        if k % 2 == 0:
            for dr in (0.0,):
                x = 8 + 6.2 * math.cos(a); y = 8 + 6.2 * math.sin(a)
                I.ellipse(x, y, 0.9, 0.9, "grey", 7)
    I.ellipse(8, 8, r, r, c, 3)
    I.ellipse(8, 8, r - 0.9, r - 0.9, c, 4)
    I.ellipse(7.7, 7.7, max(0.6, r - 1.9), max(0.6, r - 1.9), c, 5)
    I.ellipse(7.0, 7.0, max(0.7, r * 0.3), max(0.7, r * 0.3), c, 7)


@icon("tool_brush_small", "tools", "Small brush")
def _(I):
    _brush(I, 2.7)


@icon("tool_brush_large", "tools", "Large brush")
def _(I):
    _brush(I, 5.0)


@icon("tool_fill", "tools", "Fill")
def _(I):
    # bucket with handle, paint surface and a drip
    I.ring(8, 6.4, 5.4, 1.1, "grey", 4)
    I.rect(2.0, 6.4, 12.0, 1.0, None) if False else None
    I.poly([(3.2, 6.2), (12.8, 6.2), (11.6, 14.0), (4.4, 14.0)], "grey", 5)
    I.poly([(3.2, 6.2), (5.6, 6.2), (6.0, 14.0), (4.4, 14.0)], "grey", 7)
    I.poly([(12.8, 6.2), (10.4, 6.2), (10.2, 14.0), (11.6, 14.0)], "grey", 3)
    I.ellipse(8, 6.2, 4.8, 1.6, "blue", 5)
    I.ellipse(7.2, 6.0, 2.6, 0.8, "blue", 7)
    I.rect(3.2, 9.4, 9.0, 1.0, "grey", 3)
    drop(I, 13.2, 11.8, 1.5, "blue", 5)


@icon("tool_marquee", "tools", "Select area")
def _(I):
    I.rect(2.0, 3.0, 12.0, 9.0, "blue", 5, after=False)
    I.rect(2.0, 3.0, 12.0, 9.0, "blue", 6)
    I.rect(3.0, 4.0, 10.0, 7.0, None)
    I.rect(3.0, 4.0, 10.0, 7.0, "blue", 5)
    for k in range(5):
        x = 2.0 + k * 2.4
        I.rect(x, 3.0, 1.4, 1.0, "grey", 7)
        I.rect(x, 11.0, 1.4, 1.0, "grey", 7)
    for k in range(4):
        y = 3.0 + k * 2.4
        I.rect(2.0, y, 1.0, 1.4, "grey", 7)
        I.rect(13.0, y, 1.0, 1.4, "grey", 7)
    I.poly([(9.6, 7.0), (14.6, 11.0), (12.0, 11.4), (13.2, 14.4), (11.8, 14.8), (10.6, 11.8), (9.6, 13.4)], "grey", 7)


def _land(I, h=2.6):
    I.isobox(8, 14.5, 6.5, 6.5, h, "brown", 4, 4, 2)
    I.isoquad(8, 14.5, [(0, 0, h), (-6.5, 0, h), (-6.5, -6.5, h), (0, -6.5, h)], "green", 5)
    I.isoquad(8, 14.5, [(0, 0, h), (-6.5, 0, h), (-6.5, -0.9, h), (0, -0.9, h)], "green", 6)


@icon("tool_terrain_raise", "tools", "Raise terrain")
def _(I):
    _land(I)
    arrow(I, 8, 0.8, 9.0, 8, 3, "yellow", True)


@icon("tool_terrain_lower", "tools", "Lower terrain")
def _(I):
    _land(I)
    arrow(I, 8, 0.8, 9.0, 8, 3, "red", False)


@icon("tool_terrain_level", "tools", "Level terrain")
def _(I):
    _land(I)
    I.rect(2.4, 4.6, 11.2, 2.4, "blue", 5)
    I.rect(2.4, 4.6, 11.2, 0.8, "blue", 7)
    I.rect(2.4, 6.2, 11.2, 0.8, "blue", 3)
    I.poly([(8, 1.0), (10.6, 3.8), (5.4, 3.8)], "yellow", 5)
    I.poly([(8, 10.4), (10.6, 7.6), (5.4, 7.6)], "yellow", 4)


@icon("tool_tree", "tools", "Plant tree")
def _(I):
    tile(I, 6)
    tree(I, 6.6, 11.6, 12)
    I.ellipse(13.0, 3.6, 2.1, 2.1, "green", 3)
    I.ellipse(12.8, 3.4, 1.7, 1.7, "green", 5)
    I.rect(11.9, 3.0, 2.2, 0.9, "grey", 7)
    I.rect(12.5, 2.4, 0.9, 2.2, "grey", 7)


@icon("tool_water", "tools", "Water")
def _(I):
    I.isobox(8, 14.5, 7, 7, 1.5, "brown", 4, 4, 2)
    I.isoquad(8, 14.5, [(0, 0, 1.5), (-7, 0, 1.5), (-7, -7, 1.5), (0, -7, 1.5)], "blue", 5)
    for (x0, y0, x1, y1) in ((4.0, 11.4, 8.0, 12.8), (8.0, 12.8, 11.0, 11.6), (3.6, 9.6, 6.2, 10.6), (9.6, 10.0, 12.4, 9.2)):
        I.line(x0, y0, x1, y1, "blue", 7, 1.0)
    drop(I, 8, 6.6, 3.0, "blue", 5)


# ---- networks ------------------------------------------------------------------------------------------
def _pylon(I, bx, by, h):
    I.poly([(bx - 0.6, by - h), (bx + 0.6, by - h), (bx + 2.4, by), (bx + 0.9, by), (bx + 0.0, by - h * 0.55), (bx - 0.9, by), (bx - 2.4, by)], "grey", 5)
    I.rect(bx - 3.0, by - h + 1.6, 6.0, 1, "grey", 4)
    I.rect(bx - 2.2, by - h * 0.5, 4.4, 1, "grey", 4)
    I.rect(bx - 0.5, by - h + 0.6, 0.6, h * 0.5, "grey", 7)


@icon("net_powerline", "networks", "Power line")
def _(I):
    tile(I, 6)
    _pylon(I, 8, 11.5, 10.4)
    I.line(1.4, 4.4, 5.0, 3.4, "yellow", 6, 1)
    I.line(11.0, 3.4, 14.6, 4.6, "yellow", 6, 1)
    I.rect(2.2, 4.6, 0.9, 1.4, "grey", 6)
    I.rect(12.8, 4.6, 0.9, 1.4, "grey", 6)


def _flange(I, x, y, ramp):
    I.ellipse(x, y, 1.0, 1.9, ramp, 2)
    I.ellipse(x - 0.2, y - 0.3, 0.6, 1.3, ramp, 6)


@icon("net_pipe_water", "networks", "Water pipe")
def _(I):
    tile(I, 6)
    a, b = G(-5.6, -3.0, 1.4), G(-0.4, -3.0, 1.4)
    pipe(I, a[0], a[1], b[0], b[1], "blue", 3.8)
    c = G(-3.0, -3.0, 1.4)
    I.rect(c[0] - 0.6, c[1] - 3.6, 1.2, 2.2, "grey", 5)
    I.rect(c[0] - 1.8, c[1] - 4.8, 3.6, 1.4, "red", 5)


@icon("net_pipe_sewage", "networks", "Sewage pipe")
def _(I):
    tile(I, 6)
    a, b = G(-5.6, -3.0, 1.4), G(-0.4, -3.0, 1.4)
    pipe(I, a[0], a[1], b[0], b[1], "brown", 3.8)
    c = G(-3.0, -3.0, 1.4)
    I.ellipse(c[0], c[1] - 2.4, 1.3, 1.1, "green", 4)
    I.rect(c[0] - 0.4, c[1] - 3.0, 0.8, 1.6, "brown", 2)


@icon("net_pipe_both", "networks", "Water + sewage pipes")
def _(I):
    tile(I, 7)
    a, b = G(-6.2, -1.2, 1.4), G(-0.8, -1.2, 1.4)
    pipe(I, a[0], a[1], b[0], b[1], "blue", 2.6)
    a, b = G(-6.2, -5.4, 1.4), G(-0.8, -5.4, 1.4)
    pipe(I, a[0], a[1], b[0], b[1], "brown", 2.6)


@icon("net_transformer", "networks", "Substation")
def _(I):
    tile(I, 6, "grey", 5)
    box(I, -0.8, -0.8, 5.0, 4.0, 4.0, "grey", 6, 5, 3)
    lface(I, -0.8, -0.8, -5.8, 0.0, 0.8, "grey", 3)
    for xa in (-1.6, -3.0, -4.4):
        cx, cy = G(xa, -0.8, 5.0)
        I.rect(cx - 0.6, cy - 1.0, 1.2, 1.6, "yellow", 5)
        I.rect(cx - 0.6, cy - 1.0, 0.5, 1.6, "yellow", 7)
    bolt(I, 5.0, 6.8, 0.5)
    for x0 in (1.8, 6.2, 11.4):
        I.rect(x0, 1.2, 0.7, 2.4, "grey", 5)
    I.line(2.2, 1.4, 12.0, 1.4, "yellow", 6, 0.8)


@icon("net_battery", "networks", "Battery")
def _(I):
    tile(I, 6)
    box(I, -1.2, -1.2, 4.6, 3.6, 6.0, "green", 6, 5, 3)
    lface(I, -1.2, -1.2, -5.8, 4.2, 5.0, "yellow", 5)
    rface(I, -1.2, -1.2, -4.8, 4.2, 5.0, "yellow", 3)
    box(I, -2.6, -2.2, 1.6, 1.6, 0.9, "grey", 7, 6, 4, z=6.0)
    bolt(I, 5.2, 7.0, 0.42)


@icon("net_remove", "networks", "Remove line")
def _(I):
    tile(I, 6)
    a, b = G(-5.6, -3.0, 1.4), G(-0.4, -3.0, 1.4)
    pipe(I, a[0], a[1], b[0], b[1], "grey", 3.0)
    for (x0, y0, x1, y1) in ((5.0, 3.0, 11.0, 10.0), (11.0, 3.0, 5.0, 10.0)):
        I.line(x0, y0, x1, y1, "red", 0, 3.6)
    for (x0, y0, x1, y1) in ((5.0, 3.0, 11.0, 10.0), (11.0, 3.0, 5.0, 10.0)):
        I.line(x0, y0, x1, y1, "red", 5, 2.3)
    I.line(5.2, 2.8, 7.6, 5.6, "red", 7, 1.0)
