"""CIVIC toolkit, part 1: lots, buildings, roofs, signs, structures (stacks, towers, tanks, lattices).

Every helper takes an isokit Scene first and works in world units (x, y in cells, z in px).
Keep all geometry inside the footprint rectangle [0, fx] x [0, fy] (the engine cuts sprites into
32-px strips at the footprint diamond corners: nothing may poke out sideways; height is unlimited).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402

M = ik.mat
ST = ik.STOREY


def P(ramp, shade=0.0, **kw):
    """Plain flat-colour material."""
    return ik.Material(ramp, shade, None, **kw)


# Category accents (CS2 service colours mapped to the iso palette).
ACCENT = {
    "power": "yellow", "water": "water", "police": "slate", "fire": "red", "health": "teal",
    "education": "terra", "parks": "grass", "garbage": "wood", "deathcare": "grey", "comms": "purple",
    "admin": "sand", "industry": "olive", "transit": "teal",
}


# ---------------------------------------------------------------- ground
def rect(s, x0, y0, x1, y1, mat, layer=1, z=0.0):
    return s.ground([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], mat, z=z, layer=layer)


def lot(s, mat="grass", layer=0):
    """Fill the whole footprint with one ground material."""
    fx, fy = s.footprint
    return rect(s, 0, 0, fx, fy, mat, layer)


def kerb_lot(s, inner="paving", edge=0.06, outer="concrete_ground"):
    """A paved lot with a slightly darker rim (reads as a kerb)."""
    fx, fy = s.footprint
    rect(s, 0, 0, fx, fy, outer, 0)
    rect(s, edge, edge, fx - edge, fy - edge, inner, 1)


def stripes(s, x0, y0, x1, y1, n, axis="x", mat="marking", w=0.02, layer=3):
    """n parking-bay lines across a rectangle, perpendicular to `axis`."""
    for i in range(n + 1):
        if axis == "x":
            x = x0 + (x1 - x0) * i / n
            rect(s, x - w / 2, y0, x + w / 2, y1, mat, layer)
        else:
            y = y0 + (y1 - y0) * i / n
            rect(s, x0, y - w / 2, x1, y + w / 2, mat, layer)


def letter_h(s, cx, cy, size, mat="marking", layer=4):
    """A helipad 'H' decal (screen-readable: legs along y)."""
    a, t = size / 2, size * 0.16
    rect(s, cx - a, cy - a, cx - a + t, cy + a, mat, layer)
    rect(s, cx + a - t, cy - a, cx + a, cy + a, mat, layer)
    rect(s, cx - a, cy - t / 2, cx + a, cy + t / 2, mat, layer)


def disc(s, cx, cy, r, mat, layer=2, segs=24, z=0.0):
    pts = [(cx + r * math.cos(2 * math.pi * k / segs), cy + r * math.sin(2 * math.pi * k / segs))
           for k in range(segs)]
    return s.ground(pts, mat, z=z, layer=layer)


def ring(s, cx, cy, r0, r1, mat, layer=2, segs=24):
    for k in range(segs):
        a, b = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
        s.ground([(cx + r0 * math.cos(a), cy + r0 * math.sin(a)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a)),
                  (cx + r1 * math.cos(b), cy + r1 * math.sin(b)), (cx + r0 * math.cos(b), cy + r0 * math.sin(b))],
                 mat, layer=layer)


# ---------------------------------------------------------------- buildings
def wall_mat(kind="plaster", ramp=None, shade=None, **kw):
    """Window wall: kind = plaster | brick | concrete | office | siding | brick_yellow."""
    base = {"plaster": "windows_plaster", "brick": "windows_brick", "concrete": "windows_concrete",
            "office": "windows_office", "siding": "windows_siding"}[kind]
    o = dict(kw)
    if ramp is not None:
        o["ramp"] = ramp
        o["base"] = M({"plaster": "plaster", "brick": "brick", "concrete": "concrete", "office": "concrete",
                       "siding": "siding"}[kind], ramp=ramp, **({"shade": shade} if shade is not None else {}))
    if shade is not None:
        o["shade"] = shade
    return M(base, **o)


def block(s, x, y, dx, dy, h, wall, roof="flat", roof_mat="roof_flat", roof_h=8, rim=None, axis="x",
          gable=None, z=0.0, parapet=2):
    """A building volume with a roof. roof: flat | gable | hip | shed | none."""
    s.box(x, y, z, dx, dy, h, wall, top=roof_mat if roof in ("flat", "none") else None)
    top = z + h
    if roof == "flat":
        s.roof_flat(x, y, top, dx, dy, roof_mat, parapet=parapet, rim=rim or wall)
    elif roof == "gable":
        s.roof_gable(x, y, top, dx, dy, roof_h, roof_mat, axis=axis, gable=gable or wall)
    elif roof == "hip":
        s.roof_hip(x, y, top, dx, dy, roof_h, roof_mat)
    elif roof == "shed":
        s.roof_shed(x, y, top, dx, dy, roof_h, roof_mat, high=axis, wall=gable or wall)
    return top


def door(s, x, y, w, h, face="+y", mat="door_glass", z=0.0, t=0.015):
    """A door / bay panel standing proud of a facade. `x, y` = left end on the facade line."""
    if face == "+y":
        s.box(x, y, z, w, t, h, mat)
    elif face == "+x":
        s.box(x, y, z, t, w, h, mat)
    elif face == "-y":
        s.box(x, y - t, z, w, t, h, mat)
    else:
        s.box(x - t, y, z, t, w, h, mat)


def panel(s, x, y, z, w, h, mat, face="+y", t=0.012):
    """Flat sign panel on a facade (centre x/y on the facade line)."""
    if face == "+y":
        s.box(x - w / 2, y, z, w, t, h, mat)
    else:
        s.box(x, y - w / 2, z, t, w, h, mat)


def red_cross(s, x, y, z, size=8, face="+y", bg=True):
    """Hospital sign: white square with a red cross, on a facade or as a roof decal (face='top')."""
    W = size / 32.0
    from isokit.details import pat_emissive
    red = ik.Material("red", 1.5, pat_emissive, dither=0.0, snow=False, eramp="red", eshade=8.0)
    white = P("snow", 1.0, snow=False)
    if face == "top":
        if bg:
            rect(s, x - W / 2, y - W / 2, x + W / 2, y + W / 2, white, layer=4, z=z)
        a, t = W * 0.38, W * 0.13
        rect(s, x - a, y - t, x + a, y + t, red, layer=5, z=z)
        rect(s, x - t, y - a, x + t, y + a, red, layer=5, z=z)
        return
    if bg:
        panel(s, x, y, z, W, size, white, face, t=0.01)
    a, t = W * 0.38, max(W * 0.13, 1.2 / 32)
    th = max(size * 0.26, 2)
    o = 0.02
    if face == "+y":
        s.box(x - a, y + o - 0.005, z + size / 2 - th / 2, 2 * a, 0.006, th, red)
        s.box(x - t, y + o - 0.005, z + size * 0.12, 2 * t, 0.006, size * 0.76, red)
    else:
        s.box(x + o - 0.005, y - a, z + size / 2 - th / 2, 0.006, 2 * a, th, red)
        s.box(x + o - 0.005, y - t, z + size * 0.12, 0.006, 2 * t, size * 0.76, red)


def canopy(s, x, y, dx, dy, z, mat, posts=True, post_mat=None, t=1.5):
    """Flat canopy slab on thin posts."""
    s.box(x, y, z, dx, dy, t, mat)
    if posts:
        pm = post_mat or P("grey", 2)
        for px, py in ((x + 0.02, y + 0.02), (x + dx - 0.05, y + 0.02), (x + 0.02, y + dy - 0.05),
                       (x + dx - 0.05, y + dy - 0.05)):
            s.box(px, py, 0, 0.03, 0.03, z, pm)


# ---------------------------------------------------------------- structures
def stack(s, cx, cy, r, h, mat="chimney_bands", z=0.0, cap=None):
    s.cylinder(cx, cy, z, r, h, mat, top=P("grey", -4))
    s.cylinder(cx, cy, z + h - 2, r * 1.15, 2, cap or P("grey", 0))
    return z + h


def lathe(s, cx, cy, profile, mat, segs=24, top=None, z=0.0):
    """Surface of revolution: profile = [(z_px, r_cells), ...] bottom to top. Open top unless `top`."""
    m = ik.mat(mat)
    ang = [2 * math.pi * k / segs for k in range(segs)]
    for (z0, r0), (z1, r1) in zip(profile, profile[1:]):
        for i in range(segs):
            a, b = ang[i], ang[(i + 1) % segs]
            am = a + math.pi / segs
            slope = (r0 - r1) * ik.S / max(z1 - z0, 1e-6)
            s.poly([(cx + r0 * math.cos(a), cy + r0 * math.sin(a), z + z0),
                    (cx + r0 * math.cos(b), cy + r0 * math.sin(b), z + z0),
                    (cx + r1 * math.cos(b), cy + r1 * math.sin(b), z + z1),
                    (cx + r1 * math.cos(a), cy + r1 * math.sin(a), z + z1)], m,
                   outward=(math.cos(am), math.sin(am), slope / ik.S), kind="curved")
    if top is not None:
        zt, rt = profile[-1]
        s.poly([(cx + rt * math.cos(a), cy + rt * math.sin(a), z + zt) for a in ang], top, outward=(0, 0, 1))


def tank(s, cx, cy, r, h, mat="metal_light", roof="dome", z=0.0):
    s.cylinder(cx, cy, z, r, h, mat, top=P("grey", 2))
    if roof == "dome":
        s.dome(cx, cy, z + h, r, max(3, r * 18), P("grey", 2.5))
    elif roof == "cone":
        cone(s, cx, cy, z + h, r, max(3, r * 16), P("grey", 2))


def pole(s, x, y, h, mat=None, w=0.025, z=0.0):
    s.box(x - w / 2, y - w / 2, z, w, w, h, mat or P("grey", 1))


def lattice(s, x, y, dx, dy, h, mat=None, taper=0.0, levels=6, legs=True):
    """Truss tower: 4 legs (optionally tapering) with horizontal rings every h/levels px."""
    m = mat or P("grey", 1, snow=False)
    for i in range(levels + 1):
        t = i / levels
        ins = taper * t
        z = h * t
        x0, y0, x1, y1 = x + ins, y + ins, x + dx - ins, y + dy - ins
        if legs and i < levels:
            ins2 = taper * (i + 1) / levels
            z2 = h * (i + 1) / levels
            for (ax, ay), (bx, by) in (((x0, y0), (x + ins2, y + ins2)), ((x1, y0), (x + dx - ins2, y + ins2)),
                                       ((x0, y1), (x + ins2, y + dy - ins2)),
                                       ((x1, y1), (x + dx - ins2, y + dy - ins2))):
                beam(s, (ax, ay, z), (bx, by, z2), m)
            # diagonal bracing on the two visible faces
            beam(s, (x0, y1, z), (x + dx - ins2, y + dy - ins2, z2), m)
            beam(s, (x1, y0, z), (x + dx - ins2, y + dy - ins2, z2), m)
        for (ax, ay), (bx, by) in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x0, y1), (x1, y1)),
                                   ((x0, y0), (x0, y1))):
            beam(s, (ax, ay, z), (bx, by, z), m)


def beam(s, a, b, mat, w=0.018, hpx=1.2):
    """A thin straight member between two world points (2 crossed ribbons, double-sided)."""
    ax, ay, az = a
    bx, by, bz = b
    dx, dy = bx - ax, by - ay
    L = math.hypot(dx, dy)
    if L < 1e-6:
        s.box(ax - w / 2, ay - w / 2, min(az, bz), w, w, max(abs(bz - az), 1), mat)
        return
    nx, ny = -dy / L * w / 2, dx / L * w / 2
    s.poly([(ax + nx, ay + ny, az), (bx + nx, by + ny, bz), (bx - nx, by - ny, bz), (ax - nx, ay - ny, az)],
           mat, cull=False, outward=(0, 0, 1))
    s.poly([(ax, ay, az - hpx / 2), (bx, by, bz - hpx / 2), (bx, by, bz + hpx / 2), (ax, ay, az + hpx / 2)],
           mat, cull=False, outward=(-dy, dx, 0) if abs(dx) > abs(dy) else (dy, -dx, 0))


def pipe(s, a, b, r=0.03, mat=None):
    """Horizontal pipe along x or y from point a to b (z = axis height)."""
    m = mat or P("grey", 2)
    ax, ay, az = a
    bx, by, bz = b
    if abs(bx - ax) >= abs(by - ay):
        hcyl(s, min(ax, bx), ay, az, r, abs(bx - ax), m, "x", 8)
    else:
        hcyl(s, ax, min(ay, by), az, r, abs(by - ay), m, "y", 8)


# isokit.Scene.cone and the horizontal Scene.cylinder take the last segment's outward vector from
# (a + b) / 2 with b wrapped to 0, which flips that segment (a missing / wrong sliver). Fixed copies:
def cone(s, cx, cy, z, r, h, mat, segs=20):
    """Upright cone: base centre (cx, cy, z), radius r cells, height h px."""
    m = ik.mat(mat)
    k = (r * ik.S) / max(h, 1e-6)
    apex = (cx, cy, z + h)
    for i in range(segs):
        a, b = 2 * math.pi * i / segs, 2 * math.pi * (i + 1) / segs
        am = (a + b) / 2
        s.poly([(cx + r * math.cos(a), cy + r * math.sin(a), z), (cx + r * math.cos(b), cy + r * math.sin(b), z),
                apex], m, outward=(math.cos(am), math.sin(am), k), kind="curved")
    return s


def hcyl(s, x, y, z, r, L, mat, axis="x", segs=10, caps=True):
    """Horizontal cylinder: (x, y, z) = start of the axis (z in px), radius r cells, length L cells."""
    m = ik.mat(mat)
    rz = r * ik.S
    ang = [2 * math.pi * k / segs for k in range(segs + 1)]

    def pa(t, aa):
        if axis == "x":
            return (x + t, y + r * math.cos(aa), z + rz * math.sin(aa))
        return (x + r * math.cos(aa), y + t, z + rz * math.sin(aa))
    for i in range(segs):
        a, b = ang[i], ang[i + 1]
        am = (a + b) / 2
        o = (0, math.cos(am), math.sin(am)) if axis == "x" else (math.cos(am), 0, math.sin(am))
        s.poly([pa(0, a), pa(0, b), pa(L, b), pa(L, a)], m, outward=o, kind="curved")
    if caps:
        for t, sg in ((0, -1), (L, 1)):
            s.poly([pa(t, aa) for aa in ang[:-1]], m, outward=(sg, 0, 0) if axis == "x" else (0, sg, 0))
    return s


def fence(s, pts, h=4, mat=None, closed=False, posts=True):
    """See-through fence: top rail + posts (wire/rail look)."""
    m = mat or P("grey", 2, snow=False)
    seq = list(pts) + ([pts[0]] if closed else [])
    for (ax, ay), (bx, by) in zip(seq, seq[1:]):
        beam(s, (ax, ay, h), (bx, by, h), m, w=0.012, hpx=1)
        beam(s, (ax, ay, h * 0.45), (bx, by, h * 0.45), m, w=0.01, hpx=0.8)
        if posts:
            L = math.hypot(bx - ax, by - ay)
            n = max(1, int(L / 0.2))
            for k in range(n + 1):
                t = k / n
                pole(s, ax + (bx - ax) * t, ay + (by - ay) * t, h, m, w=0.014)


def solid_wall(s, pts, h, t=0.04, mat="stone", top=None):
    """Thick wall along a polyline of axis-aligned segments."""
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        x0, x1 = min(ax, bx), max(ax, bx)
        y0, y1 = min(ay, by), max(ay, by)
        if x1 - x0 >= y1 - y0:
            s.box(x0, y0 - t / 2, 0, x1 - x0 + t / 2, t, h, mat, top=top)
        else:
            s.box(x0 - t / 2, y0, 0, t, y1 - y0 + t / 2, h, mat, top=top)


def clamp_fp(s, x, y, m=0.02):
    fx, fy = s.footprint
    return min(max(x, m), fx - m), min(max(y, m), fy - m)
