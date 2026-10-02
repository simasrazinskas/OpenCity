"""CIVIC models: parks (small park, plaza, large park, sportsfield). Accent: green. No power needed."""
import math

import numpy as np

from iso_civic_kit import ik, M, P, ST, rect, lot, disc, ring, pole, beam, hcyl, cone, block, door
from iso_civic_props import tree, person, bench, lamp, hedge, flowers, floodlight, fountain
from iso_civic_states import model

PAVE = M("paving", shade=0.8)
PAVE2 = M("paving", shade=-0.4)
KERB = M("concrete_ground", shade=1.0)
SAND = M("sand", shade=0.5)
GRASS2 = M("grass", shade=0.9)


# ---------------------------------------------------------------- local helpers
def curve(pts, n=8):
    """Catmull-Rom spline through (x, y) points."""
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(tuple(pts[-1]))
    return out


def stroke(s, pts, w, mat, layer=2):
    """A ground ribbon of width w (cells) along a polyline, with round-ish joints."""
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L * w / 2, dx / L * w / 2
        s.ground([(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)], mat, layer=layer)
    for (x, y) in pts[1:-1]:
        disc(s, x, y, w / 2, mat, layer=layer, segs=8)


def path(s, pts, w=0.12, mat=PAVE, edge=KERB, layer=2, n=8):
    """Paved footpath with a lighter kerb edge along a spline."""
    c = curve(pts, n)
    stroke(s, c, w + 0.035, edge, layer)
    stroke(s, c, w, mat, layer + 1)
    return c


def blobpoly(cx, cy, rx, ry, seed=0, wob=0.12, segs=18):
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28, 3)
    pts = []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        r = 1 + wob * (math.sin(2 * a + ph[0]) * 0.6 + math.sin(3 * a + ph[1]) * 0.4)
        pts.append((cx + rx * r * math.cos(a), cy + ry * r * math.sin(a)))
    return pts


def planter(s, x, y, dx, dy, ramp="rose", h=3, season="summer"):
    """Stone planter box with flowers / low shrubs."""
    s.box(x, y, 0, dx, dy, h, M("stone", shade=1.5), top=M("dirt", shade=-0.5))
    if season == "winter":
        return
    n = max(1, int(max(dx, dy) / 0.07))
    for i in range(n):
        t = (i + 0.5) / n
        px, py = (x + dx * t, y + dy / 2) if dx >= dy else (x + dx / 2, y + dy * t)
        s.ellipsoid(px, py, h + 1.2, 0.035, 0.035, 2.4,
                    P(ramp if i % 2 else "leaf", 1.0 if i % 2 else 0.5, snow=False), rough=0.3)


def duck(s, x, y, white=True):
    s.box(x, y, 0.5, 0.034, 0.02, 1.4, P("snow" if white else "wood", 1.5 if white else 0, snow=False))
    s.box(x + 0.022, y + 0.004, 1.9, 0.014, 0.012, 1.5, P("snow" if white else "wood", 1.5 if white else 0,
                                                          snow=False))
    s.box(x + 0.034, y + 0.007, 2.3, 0.01, 0.006, 0.7, P("yellow", 1.5, snow=False))


def flower_bed(s, x0, y0, x1, y1, ramp, season="summer", layer=3):
    rect(s, x0 - 0.012, y0 - 0.012, x1 + 0.012, y1 + 0.012, M("stone", shade=1.8), layer=layer)
    if season == "winter":
        rect(s, x0, y0, x1, y1, M("dirt", shade=-0.5), layer=layer + 1)
    else:
        rect(s, x0, y0, x1, y1, M("meadow", ramp=ramp, shade=-0.3, flowers=0.45), layer=layer + 1)


def round_bed(s, cx, cy, r, ramp, season="summer", layer=3):
    disc(s, cx, cy, r + 0.018, M("stone", shade=1.8), layer=layer)
    if season == "winter":
        disc(s, cx, cy, r, M("dirt", shade=-0.5), layer=layer + 1)
    else:
        disc(s, cx, cy, r, M("meadow", ramp=ramp, shade=-0.3, flowers=0.45), layer=layer + 1)


def pergola_roof(s, cx, cy, r, z, h, mat, segs=6):
    """Hexagonal pyramid roof."""
    cone(s, cx, cy, z, r, h, mat, segs=segs)


def gazebo(s, cx, cy, r=0.13, z=0.0, season="summer"):
    base = M("stone", shade=1.2)
    s.cylinder(cx, cy, z, r + 0.02, 2, base, top=M("wood", shade=1.5), segs=12)
    wood = P("snow", 0.8)
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        pole(s, cx + r * 0.86 * math.cos(a), cy + r * 0.86 * math.sin(a), 13, wood, w=0.02, z=z + 2)
    s.cylinder(cx, cy, z + 15, r * 0.95, 1.5, P("snow", 0.2), segs=12)
    cone(s, cx, cy, z + 16.5, r + 0.04, 10, M("roof_tiles", ramp="teal", shade=0.3), segs=12)
    s.box(cx - 0.008, cy - 0.008, z + 26, 0.016, 0.016, 3, P("grey", 3))


def swing(s, x, y, axis_y=True):
    """Swing set along +x: A-frame ends, beam, two seats."""
    fr = P("red", 0.5, snow=False)
    for xx in (x, x + 0.2):
        beam(s, (xx, y - 0.05, 0), (xx, y, 12), fr, w=0.014, hpx=1.2)
        beam(s, (xx, y + 0.05, 0), (xx, y, 12), fr, w=0.014, hpx=1.2)
    beam(s, (x, y, 12), (x + 0.2, y, 12), fr, w=0.016, hpx=1.4)
    for xx in (x + 0.06, x + 0.14):
        beam(s, (xx, y, 12), (xx, y, 3), P("grey", 3, snow=False), w=0.006, hpx=0.8)
        s.box(xx - 0.025, y - 0.012, 2.4, 0.05, 0.024, 1, P("wood", 1, snow=False))


def slide(s, x, y):
    """Climbing tower (stairs) with a slide ramp toward +x."""
    s.box(x, y, 0, 0.06, 0.06, 9, P("yellow", 0.5, snow=False), top=P("terra", 1.5, snow=False))
    # sloped slide surface: from top of tower (z=8) down to ground at x+0.3
    s.poly([(x + 0.06, y + 0.012, 8), (x + 0.28, y + 0.012, 0.5), (x + 0.28, y + 0.052, 0.5),
            (x + 0.06, y + 0.052, 8)], P("snow", 0.8, snow=False), cull=False, outward=(0.2, 0, 1))
    for yy in (y + 0.008, y + 0.054):
        beam(s, (x + 0.06, yy, 8), (x + 0.28, yy, 0.5), P("red", 0.5, snow=False), w=0.008, hpx=1)
    # ladder at back
    for k in range(4):
        s.box(x - 0.012, y + 0.008, 1 + k * 2, 0.012, 0.044, 0.6, P("grey", 3, snow=False))


def seesaw(s, x, y):
    s.box(x + 0.1, y, 0, 0.012, 0.03, 2, P("grey", 2, snow=False))
    s.poly([(x, y + 0.004, 2.2), (x + 0.2, y + 0.004, 2.8), (x + 0.2, y + 0.04, 2.8), (x, y + 0.04, 2.2)],
           P("terra", 1.5, snow=False), cull=False, outward=(0, 0, 1))


def sprinkle_trees(s, items, st):
    for (x, y, h, r, kind) in items:
        tree(s, x, y, h, r, kind, season=st.season, seed=int(x * 13 + y * 7))


# ---------------------------------------------------------------- park-small
@model("park-small", (1, 1), "parks", "Small park", power=False,
       note="Curved footpaths, benches, flower beds, lamp.")
def park_small(st):
    s = ik.Scene((1, 1), 61)
    lot(s, "grass")
    # grass mowing contrast
    rect(s, 0.0, 0.0, 1.0, 0.28, GRASS2, layer=1)
    # curved path from the front-left edge through the middle out to the right edge
    path(s, [(0.28, 1.0), (0.32, 0.78), (0.5, 0.56), (0.7, 0.42), (1.0, 0.36)], 0.11)
    path(s, [(0.5, 0.56), (0.3, 0.38), (0.0, 0.3)], 0.08)
    disc(s, 0.5, 0.56, 0.16, KERB, layer=2)
    disc(s, 0.5, 0.56, 0.135, PAVE2, layer=3)
    round_bed(s, 0.5, 0.56, 0.07, "rose", st.season, layer=4)
    # benches round the hub
    bench(s, 0.37, 0.46, "x", 0.12)
    bench(s, 0.58, 0.72, "x", 0.12)
    # flower beds
    flower_bed(s, 0.64, 0.66, 0.9, 0.84, "yellow", st.season)
    flower_bed(s, 0.08, 0.5, 0.2, 0.7, "purple", st.season)
    # hedge edge on the back
    for (x0, y0, dx, dy) in ((0.05, 0.03, 0.34, 0.05), (0.62, 0.03, 0.34, 0.05)):
        hedge(s, x0, y0, dx, dy, 4, st.season)
    # trees
    sprinkle_trees(s, [(0.2, 0.2, 21, 0.12, "round"), (0.8, 0.14, 18, 0.11, "round"),
                       (0.9, 0.6, 14, 0.09, "bush"), (0.12, 0.85, 12, 0.09, "bush")], st)
    lamp(s, 0.4, 0.88, 15)
    person(s, 0.46, 0.7, "red")
    person(s, 0.68, 0.5, "teal", "wood")
    return s


# ---------------------------------------------------------------- plaza
@model("plaza", (1, 1), "parks", "Plaza", power=False, anim=4,
       note="Animated fountain (4 frames).")
def plaza(st):
    s = ik.Scene((1, 1), 62)
    rect(s, 0, 0, 1, 1, KERB, 0)
    rect(s, 0.04, 0.04, 0.96, 0.96, PAVE, 1)
    # chequered paving: alternating tile tones over the whole square
    n = 6
    for i in range(n):
        for j in range(n):
            if (i + j) % 2:
                rect(s, 0.04 + 0.92 * i / n, 0.04 + 0.92 * j / n, 0.04 + 0.92 * (i + 1) / n,
                     0.04 + 0.92 * (j + 1) / n, PAVE2, 2)
    ring(s, 0.5, 0.5, 0.26, 0.32, PAVE2, layer=2, segs=28)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        stroke(s, [(0.5 + 0.3 * math.cos(a), 0.5 + 0.3 * math.sin(a)),
                   (0.5 + 0.44 * math.cos(a), 0.5 + 0.44 * math.sin(a))], 0.05, M("paving", ramp="sand", shade=0), 3)
    fountain(s, 0.5, 0.5, 0.19, st.frame, 12)
    # benches facing the fountain
    bench(s, 0.4, 0.78, "x", 0.2)
    bench(s, 0.4, 0.17, "x", 0.2)
    bench(s, 0.8, 0.4, "y", 0.2)
    bench(s, 0.15, 0.4, "y", 0.2)
    # planters at the corners
    planter(s, 0.09, 0.09, 0.14, 0.1, "rose", 4, st.season)
    planter(s, 0.77, 0.09, 0.14, 0.1, "yellow", 4, st.season)
    planter(s, 0.09, 0.81, 0.14, 0.1, "purple", 4, st.season)
    planter(s, 0.77, 0.81, 0.14, 0.1, "rose", 4, st.season)
    lamp(s, 0.3, 0.7, 15)
    lamp(s, 0.7, 0.3, 15)
    person(s, 0.62, 0.74, "yellow", "slate")
    person(s, 0.28, 0.5, "teal", "wood")
    person(s, 0.7, 0.55, "rose", "slate")
    return s


# ---------------------------------------------------------------- park-large
@model("park-large", (2, 2), "parks", "Large park", power=False, anim=4,
       note="Playground, pond with fountain and ducks, gazebo (4 frames).")
def park_large(st):
    s = ik.Scene((2, 2), 63)
    lot(s, "grass")
    rect(s, 0, 0, 2, 0.5, GRASS2, layer=1)
    rect(s, 0, 1.1, 2, 1.45, GRASS2, layer=1)
    # main loop path + entrance from the front corner
    path(s, [(0.55, 2.0), (0.62, 1.7), (0.9, 1.45), (1.3, 1.5), (1.6, 1.3), (1.65, 0.95), (1.45, 0.6), (1.0, 0.45),
             (0.55, 0.6), (0.4, 1.0), (0.55, 1.4), (0.9, 1.45)], 0.1, n=6)
    path(s, [(1.3, 1.5), (1.45, 1.75), (1.6, 2.0)], 0.09)
    path(s, [(1.65, 0.95), (2.0, 0.9)], 0.09)
    path(s, [(0.4, 1.0), (0.0, 1.05)], 0.09)
    # pond in the middle of the loop
    pc = (1.02, 0.98)
    ring_pts = blobpoly(pc[0], pc[1], 0.36, 0.26, 3, 0.1)
    s.ground(blobpoly(pc[0], pc[1], 0.4, 0.3, 3, 0.1), M("sand", shade=1.2), layer=2)
    s.ground(ring_pts, "water", layer=3)
    # fountain jet in the pond
    spray = ik.Material("water", 7.0, None, dither=0.0, snow=False)
    s.cylinder(pc[0], pc[1], 0, 0.045, 3, M("stone", shade=1), top=M("stone", shade=1.5))
    jh = 10 * (0.75 + 0.25 * math.sin(st.frame * math.pi / 2))
    s.ellipsoid(pc[0], pc[1], 3 + jh * 0.5, 0.022, 0.022, jh * 0.55, spray)
    for k in range(6):
        a = 2 * math.pi * k / 6 + st.frame * 0.4
        d = 0.05 + 0.02 * ((st.frame + k) % 2)
        s.ellipsoid(pc[0] + d * math.cos(a), pc[1] + d * math.sin(a), 3.4, 0.016, 0.016, 1.4, spray)
    # ducks drift
    duck(s, 0.78 + 0.01 * st.frame, 0.9, True)
    duck(s, 1.22 - 0.01 * st.frame, 1.12, False)
    duck(s, 0.95, 1.12 + 0.01 * (st.frame % 2), True)
    # reeds on the shore
    for (x, y) in ((0.72, 1.12), (1.32, 0.82)):
        for k in range(3):
            s.box(x + k * 0.015, y, 0, 0.01, 0.01, 4 + k, P("leaf", 1, snow=False))
    # playground (back corner): sand pit with equipment
    rect(s, 1.18, 0.1, 1.9, 0.4, M("sand", shade=0.4), layer=2)
    rect(s, 1.16, 0.08, 1.92, 0.42, KERB, layer=1)
    slide(s, 1.25, 0.2)
    swing(s, 1.62, 0.28)
    seesaw(s, 1.54, 0.12)
    person(s, 1.5, 0.35, "red", "slate")
    person(s, 1.78, 0.2, "yellow", "wood")
    # gazebo / bandstand (left corner), linked by a spur path
    path(s, [(0.55, 1.4), (0.5, 1.55), (0.38, 1.62)], 0.08)
    disc(s, 0.3, 1.62, 0.24, KERB, layer=2)
    disc(s, 0.3, 1.62, 0.21, PAVE2, layer=3)
    gazebo(s, 0.3, 1.62, 0.14, season=st.season)
    # trees (tall ones at the back and sides, low ones in front)
    sprinkle_trees(s, [(0.25, 0.22, 28, 0.15, "round"), (0.62, 0.14, 24, 0.13, "round"), (0.95, 0.2, 26, 0.14, "conifer"),
                       (0.12, 0.7, 22, 0.13, "round"), (0.14, 1.2, 18, 0.11, "round"), (0.12, 1.9, 16, 0.1, "conifer"),
                       (1.9, 1.2, 20, 0.12, "round"), (1.45, 0.7, 12, 0.1, "bush"), (1.85, 1.62, 16, 0.11, "round"),
                       (0.95, 1.85, 10, 0.1, "bush"), (1.12, 1.75, 12, 0.1, "bush")], st)
    # flower beds
    flower_bed(s, 1.35, 1.7, 1.6, 1.84, "rose", st.season)
    flower_bed(s, 0.78, 1.62, 0.96, 1.74, "yellow", st.season)
    # furniture
    bench(s, 0.68, 0.66, "x", 0.12)
    bench(s, 1.3, 1.34, "x", 0.12)
    bench(s, 0.62, 1.2, "y", 0.12)
    lamp(s, 0.7, 1.82, 14)
    lamp(s, 1.65, 1.65, 14)
    lamp(s, 1.2, 0.54, 15)
    person(s, 0.62, 1.58, "teal")
    person(s, 1.26, 1.45, "rose", "slate")
    person(s, 0.5, 0.9, "yellow", "wood")
    return s


# ---------------------------------------------------------------- sportsfield
def goal(s, x, y, axis, depth=0.07, w=0.2, h=8, side=1):
    """Football goal: posts + bar + net box. axis = 'x' (mouth on a line of constant x)."""
    fr = P("snow", 1.5, snow=False)
    net = M("glass", shade=1.5, snow=False)
    if axis == "x":  # mouth spans y..y+w at x, net extends toward side
        for yy in (y, y + w - 0.014):
            s.box(x - 0.007, yy, 0, 0.014, 0.014, h, fr)
        s.box(x - 0.007, y, h - 1, 0.014, w, 1.2, fr)
        # back net: low frame + side nets
        xb = x + side * depth
        for yy in (y, y + w - 0.01):
            beam(s, (x, yy, h), (xb, yy, 3), fr, w=0.008, hpx=0.8)
        beam(s, (xb, y, 3), (xb, y + w, 3), fr, w=0.008, hpx=0.8)
        s.poly([(x, y, h), (x, y + w, h), (xb, y + w, 3), (xb, y, 3)], net, cull=False, outward=(0, 0, 1))
    else:
        for xx in (x, x + w - 0.014):
            s.box(xx, y - 0.007, 0, 0.014, 0.014, h, fr)
        s.box(x, y - 0.007, h - 1, w, 0.014, 1.2, fr)
        yb = y + side * depth
        for xx in (x, x + w - 0.01):
            beam(s, (xx, y, h), (xx, yb, 3), fr, w=0.008, hpx=0.8)
        beam(s, (x, yb, 3), (x + w, yb, 3), fr, w=0.008, hpx=0.8)
        s.poly([(x, y, h), (x + w, y, h), (x + w, yb, 3), (x, yb, 3)], net, cull=False, outward=(0, 0, 1))


def _lit_grass(shade):
    """Pitch grass that stays bright at night (floodlit)."""
    base = ik.mat("grass")

    def pat(c, m):
        base.pattern(c, m)
        if c.night:
            c.emit[:] = True
            c.eramp[:] = ik.RAMP["grass"]
            c.eshade[:] = 6.2 + (c.tone * 0.5)
    return ik.Material("grass", shade, pat, dither=base.dither, **base.params)


def _lit_white():
    def pat(c, m):
        if c.night:
            c.emit[:] = True
            c.eramp[:] = ik.RAMP["snow"]
            c.eshade[:] = 8.0
    return ik.Material("snow", 2.5, pat, dither=0.0, snow=False)


@model("sportsfield", (2, 2), "parks", "Sports field", power=False,
       note="Floodlights glow at night; stands along the back.")
def sportsfield(st):
    s = ik.Scene((2, 2), 64)
    lot(s, "grass")
    # running track (terra) around the pitch
    TRACK = M("gravel", ramp="terra", shade=0.0)
    rect(s, 0.2, 0.46, 1.8, 1.84, TRACK, layer=1)
    rect(s, 0.31, 0.58, 1.69, 1.72, "grass", layer=2)
    # pitch (mowed stripes)
    px0, py0, px1, py1 = 0.36, 0.66, 1.64, 1.68
    n = 8
    for i in range(n):
        x0 = px0 + (px1 - px0) * i / n
        x1 = px0 + (px1 - px0) * (i + 1) / n
        rect(s, x0, py0, x1, py1, _lit_grass(0.9) if i % 2 else _lit_grass(0.1), layer=3)
    W = _lit_white()
    wl = 0.024
    # touchlines, goal lines, halfway line
    rect(s, px0, py0, px1, py0 + wl, W, layer=5)
    rect(s, px0, py1 - wl, px1, py1, W, layer=5)
    rect(s, px0, py0, px0 + wl, py1, W, layer=5)
    rect(s, px1 - wl, py0, px1, py1, W, layer=5)
    cx, cy = (px0 + px1) / 2, (py0 + py1) / 2
    rect(s, cx - wl / 2, py0, cx + wl / 2, py1, W, layer=5)
    ring(s, cx, cy, 0.14, 0.14 + wl, W, layer=5, segs=24)
    # penalty boxes
    for (xa, xb) in ((px0, px0 + 0.2), (px1 - 0.2, px1)):
        ya, yb = cy - 0.2, cy + 0.2
        rect(s, xa, ya, xb, ya + wl, W, layer=5)
        rect(s, xa, yb - wl, xb, yb, W, layer=5)
        xe = xb if xa == px0 else xa
        rect(s, xe - wl / 2, ya, xe + wl / 2, yb, W, layer=5)
    # goals
    goal(s, px0 + 0.01, cy - 0.1, "x", 0.05, 0.2, 9, -1)
    goal(s, px1 - 0.01, cy - 0.1, "x", 0.05, 0.2, 9, 1)
    # players
    for (x, y, sh) in ((0.9, 1.0, "red"), (1.1, 1.3, "red"), (1.25, 0.95, "water"), (0.7, 1.4, "red"),
                       (1.35, 1.4, "water")):
        person(s, x, y, sh, "snow" if sh == "red" else "slate")
    s.sphere(1.02, 1.18, 1.0, 0.014, P("snow", 3, snow=False))
    # stands along the back (y small): rising tiers toward -y, canopy over them
    sx0, sx1 = 0.3, 1.7
    seat_cols = ("red", "water", "yellow")
    for t in range(4):
        y = 0.38 - t * 0.075
        h = 3 + t * 3
        s.box(sx0, y, 0, sx1 - sx0, 0.075, h, M("concrete", shade=0.6 + 0.2 * t), top=M("concrete", shade=1.2))
        # seat row on the tier
        s.box(sx0 + 0.02, y + 0.012, h, sx1 - sx0 - 0.04, 0.03, 1.6, M("plaster", ramp=seat_cols[t % 3], shade=0.6),
              top=P(seat_cols[t % 3], 1.5, snow=False))
    # spectators
    for k, x in enumerate(np.linspace(0.4, 1.6, 9)):
        t = k % 3
        y = 0.38 - t * 0.075 + 0.02
        s.box(x - 0.016, y + 0.006, 3 + t * 3 + 1.6, 0.032, 0.02, 3, P(("red", "yellow", "snow", "teal")[k % 4], 1,
                                                                        snow=False))
        s.box(x - 0.011, y + 0.008, 3 + t * 3 + 4.6, 0.022, 0.016, 2, P("sand", 1, snow=False))
    # canopy on poles over the stand
    for x in (sx0 + 0.02, 1.0, sx1 - 0.04):
        pole(s, x, 0.12, 22, P("grey", 2, snow=False), w=0.03)
    s.box(sx0 - 0.02, 0.0, 22, sx1 - sx0 + 0.04, 0.38, 2, M("roof_metal", ramp="teal", shade=0.5),
          top=M("roof_metal", ramp="teal", shade=1.0))
    # small clubhouse / kiosk at the front-left
    block(s, 0.08, 1.7, 0.24, 0.24, ST + 1, M("plaster", ramp="sand", shade=0.5), roof="hip",
          roof_mat=M("roof_tiles", ramp="terra"), roof_h=7)
    door(s, 0.15, 1.94, 0.1, 7, "+y", "door")
    # entrance path with kiosk
    path(s, [(0.5, 2.0), (0.55, 1.85), (0.5, 1.76)], 0.1)
    rect(s, 1.78, 0.5, 1.95, 1.9, KERB, layer=1)
    for (x, y) in ((1.85, 0.9), (1.85, 1.3)):
        tree(s, x, y, 18, 0.1, "round", season=st.season, seed=int(y * 10))
    bench(s, 0.7, 1.85, "x", 0.14)
    bench(s, 1.2, 1.85, "x", 0.14)
    person(s, 0.95, 1.88, "rose")
    person(s, 1.45, 1.86, "teal", "wood")
    # floodlights in the four corners
    for (x, y) in ((0.1, 0.46), (1.92, 0.44), (0.1, 1.5), (1.92, 1.5)):
        floodlight(s, x, y, 34)
    return s
