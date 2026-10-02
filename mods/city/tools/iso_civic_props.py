"""CIVIC toolkit, part 2: props (parked vehicles, trees, people, park furniture, flags, smoke, water).

Animated props take `frame` (0..3). Seasonal props take `season`.
Parked vehicles follow the kit's reference car (iso_kit_models.car): body 4 px + cabin 3 px on 2 px wheels.
"""
import math

from iso_civic_kit import ik, M, P, rect, pole, beam, cone, hcyl

TYRE = P("grey", -6, snow=False)


# ---------------------------------------------------------------- vehicles (static, parked)
def vehicle(s, x, y, axis="x", kind="car", paint="red", L=None):
    """Parked vehicle with its rear-left corner at (x, y). axis: direction of travel.
    kind: car | police | taxi | van | ambulance | truck | fireengine | bus | hearse | tractor | forklift."""
    spec = {  # length, width, body h, cabin (start frac, len frac, h), cabin ramp
        "car": (0.26, 0.12, 4, (0.25, 0.5, 3)), "police": (0.26, 0.12, 4, (0.25, 0.5, 3)),
        "taxi": (0.26, 0.12, 4, (0.25, 0.5, 3)), "hearse": (0.32, 0.12, 4, (0.2, 0.7, 4)),
        "van": (0.3, 0.13, 7, None), "ambulance": (0.32, 0.14, 8, None),
        "truck": (0.5, 0.15, 9, None), "fireengine": (0.5, 0.15, 8, None), "bus": (0.7, 0.16, 11, None),
        "tractor": (0.2, 0.12, 4, (0.45, 0.4, 5)), "forklift": (0.14, 0.09, 3, (0.4, 0.5, 5)),
    }[kind]
    L = L or spec[0]
    W, bh, cab = spec[1], spec[2], spec[3]
    body = P(paint, 0.5, snow=False)
    if axis == "x":
        bx, by, dx, dy = x, y, L, W
    else:
        bx, by, dx, dy = x, y, W, L
    # wheels (just dark blocks under the body ends)
    for t in (0.18, 0.78):
        if axis == "x":
            s.box(bx + dx * t - 0.02, by, 0, 0.04, dy, 2, TYRE)
        else:
            s.box(bx, by + dy * t - 0.02, 0, dx, 0.04, 2, TYRE)
    if kind in ("van", "ambulance", "truck", "fireengine", "bus"):
        side = body
        if kind == "bus":
            side = M("windows_plaster", base=body, ramp=paint, shade=0.5, storey=bh, win_w=5, win_h=4,
                     period=7, sill=5, margin=2, glass="glass", snow=False)
        if kind == "ambulance":
            side = ik.Material("snow", 0.5, ik.mat("awning").pattern, ramp2="red", width=4, tone2=-6, snow=False)
            side = P("snow", 1.0, snow=False)
        s.box(bx, by, 2, dx, dy, bh, side, top=P("grey", 2, snow=False) if kind == "fireengine" else body)
        if kind == "ambulance":
            # red stripe + blue light bar
            if axis == "x":
                s.box(bx, by + dy, 5, dx, 0.004, 1.5, P("red", 1, snow=False))
            else:
                s.box(bx + dx, by, 5, 0.004, dy, 1.5, P("red", 1, snow=False))
        if kind == "fireengine":
            # ladder on the roof
            if axis == "x":
                s.box(bx + 0.06, by + 0.04, 2 + bh, dx - 0.1, 0.06, 1.5, P("grey", 4, snow=False))
            else:
                s.box(bx + 0.04, by + 0.06, 2 + bh, 0.06, dy - 0.1, 1.5, P("grey", 4, snow=False))
        if kind in ("police", "ambulance", "fireengine"):
            pass
        # windscreen at the front end (+axis)
        g = P("glass", -1, snow=False)
        if kind != "bus":
            if axis == "x":
                s.box(bx + dx - 0.004, by + 0.01, 2 + bh * 0.5, 0.006, dy - 0.02, bh * 0.4, g)
            else:
                s.box(bx + 0.01, by + dy - 0.004, 2 + bh * 0.5, dx - 0.02, 0.006, bh * 0.4, g)
        top_z = 2 + bh
    else:
        s.box(bx, by, 2, dx, dy, bh, body)
        c0, cl, ch = cab
        cm = P("glass", -1.5, snow=False)
        if kind == "tractor" or kind == "forklift":
            cm = P("glass", -0.5, snow=False)
        if axis == "x":
            s.box(bx + dx * c0, by + 0.01, 2 + bh, dx * cl, dy - 0.02, ch, cm, top=body)
        else:
            s.box(bx + 0.01, by + dy * c0, 2 + bh, dx - 0.02, dy * cl, ch, cm, top=body)
        top_z = 2 + bh + ch
        if kind == "police":
            # white doors band
            if axis == "x":
                s.box(bx + dx * 0.3, by + dy, 3, dx * 0.4, 0.004, 2, P("snow", 1, snow=False))
            else:
                s.box(bx + dx, by + dy * 0.3, 3, 0.004, dy * 0.4, 2, P("snow", 1, snow=False))
        if kind == "taxi":
            if axis == "x":
                s.box(bx + dx * 0.45, by + dy * 0.35, top_z, 0.04, 0.04, 1, P("snow", 2, snow=False))
            else:
                s.box(bx + dx * 0.35, by + dy * 0.45, top_z, 0.04, 0.04, 1, P("snow", 2, snow=False))
    if kind in ("police", "ambulance", "fireengine"):
        lc = "slate" if kind == "police" else "red"
        lamp_m = ik.mat("lamp", ramp=lc, eramp="water" if kind == "police" else "red")
        if axis == "x":
            s.box(bx + dx * 0.55, by + dy * 0.3, top_z, 0.03, dy * 0.4, 1, lamp_m)
        else:
            s.box(bx + dx * 0.3, by + dy * 0.55, top_z, dx * 0.4, 0.03, 1, lamp_m)


def bay_row(s, x0, y0, n, pitch, axis="y", kind="car", paints=("red",), lines=True, depth=0.3):
    """Row of parking bays starting at (x0, y0), running along `axis`; cars park across it."""
    for i in range(n):
        p = paints[i % len(paints)]
        if axis == "y":
            y = y0 + i * pitch
            if lines:
                rect(s, x0, y - 0.006, x0 + depth, y + 0.006, "marking", layer=3)
            if p:
                vehicle(s, x0 + 0.02, y + (pitch - 0.12) / 2, "x", kind, p)
        else:
            x = x0 + i * pitch
            if lines:
                rect(s, x - 0.006, y0, x + 0.006, y0 + depth, "marking", layer=3)
            if p:
                vehicle(s, x + (pitch - 0.12) / 2, y0 + 0.02, "y", kind, p)
    if lines:
        if axis == "y":
            y = y0 + n * pitch
            rect(s, x0, y - 0.006, x0 + depth, y + 0.006, "marking", layer=3)
        else:
            x = x0 + n * pitch
            rect(s, x - 0.006, y0, x + 0.006, y0 + depth, "marking", layer=3)


# ---------------------------------------------------------------- vegetation
_KIT_SPECIES = {"round": ("linden", "oak", "maple"), "conifer": ("spruce", "fir"), "poplar": ("poplar",)}
_FULL_H = {}


def _full_height(species):
    """Height in px of a mature kit tree (cached)."""
    if species not in _FULL_H:
        s = ik.Scene((1, 1), 0)
        ik.add_tree(s, 0.5, 0.5, species, "summer", 2, 0)
        spr = ik.render(s)
        import numpy as np
        _FULL_H[species] = max(8, spr.ay - int(np.argmax(spr.img[..., 3].any(1))))
    return _FULL_H[species]


def tree(s, x, y, h=22, r=0.13, kind="round", season="summer", seed=0):
    """Tree of about h px: kind round | conifer | poplar | bush. round/conifer/poplar use the kit's species
    (isokit.trees: linden/oak/maple, spruce/fir, poplar) scaled to h, so lots match KIT's street trees and
    seasons (bare in winter, coloured in autumn). bush is a local leafy blob."""
    if kind == "bush":
        leaf = _leaf(season, seed)
        s.blob([(x, y, h * 0.4, r, h * 0.45)], leaf, rough=0.35)
        return
    import isokit.trees as kt
    names = _KIT_SPECIES.get(kind, _KIT_SPECIES["round"])
    sp = names[(seed + int(x * 7) + int(y * 13)) % len(names)]
    k = min(1.0, max(0.3, h / float(_full_height(sp))))
    stage = 2 if k > 0.8 else (1 if k > 0.5 else 0)
    old = kt.STAGE_SCALE
    kt.STAGE_SCALE = (k, k, k)       # kit has no free scale parameter yet (asked KIT for add_tree(scale=))
    try:
        ik.add_tree(s, x, y, sp, season, stage, seed + int(x * 100) + int(y * 1000))
    finally:
        kt.STAGE_SCALE = old


def _leaf(season, seed=0):
    if season == "winter":
        return ik.Material("wood", -2.0, ik.mat("forest_floor").pattern, pebbles=0.2, patch=1.0, dither=0.8,
                           snow=True)
    if season == "autumn":
        return M("foliage", ramp="terra" if seed % 2 else "yellow", shade=-1.5)
    if season == "spring":
        return M("foliage_light")
    return M("foliage")


def hedge(s, x, y, dx, dy, h=4, season="summer"):
    s.box(x, y, 0, dx, dy, h, M("foliage", shade=-1) if season != "winter" else P("leaf", -3))


def flowers(s, x0, y0, x1, y1, ramp="rose", layer=3):
    """Flower bed decal: soil with coloured speckles (meadow pattern recoloured)."""
    rect(s, x0, y0, x1, y1, M("meadow", ramp=ramp, shade=-0.5, flowers=0.2), layer=layer)


# ---------------------------------------------------------------- people + furniture
def person(s, x, y, shirt="red", trousers="slate"):
    s.box(x - 0.02, y - 0.012, 0, 0.04, 0.024, 3, P(trousers, -1, snow=False))
    s.box(x - 0.024, y - 0.014, 3, 0.048, 0.028, 3, P(shirt, 0, snow=False))
    s.box(x - 0.014, y - 0.012, 6, 0.028, 0.024, 2, P("sand", 1, snow=False))


def bench(s, x, y, axis="x", L=0.12):
    w = P("wood", 1)
    if axis == "x":
        s.box(x, y, 1.5, L, 0.04, 1, w)
        s.box(x, y - 0.012, 2.5, L, 0.012, 2, w)
    else:
        s.box(x, y, 1.5, 0.04, L, 1, w)
        s.box(x - 0.012, y, 2.5, 0.012, L, 2, w)


def lamp(s, x, y, h=14, head="lamp"):
    pole(s, x, y, h, P("slate", 0, snow=False), w=0.018)
    s.box(x - 0.025, y - 0.025, h, 0.05, 0.05, 1.5, head)


def floodlight(s, x, y, h=36):
    pole(s, x, y, h, P("grey", 1, snow=False), w=0.024)
    s.box(x - 0.06, y - 0.03, h, 0.12, 0.06, 4, M("lamp", ramp="snow", eramp="yellow", eshade=11))


def flag(s, x, y, h=26, ramp="red", frame=0, w=0.16, fh=6, ramp2=None):
    """Flag pole with a waving flag along +x (4-frame wave)."""
    pole(s, x, y, h + 2, P("grey", 3, snow=False), w=0.016)
    n = 4
    m = P(ramp, 1, snow=False)
    m2 = P(ramp2, 2, snow=False) if ramp2 else m
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        a0 = math.sin((t0 * 2.2 + frame * 0.5) * math.pi) * 1.4 * t0
        a1 = math.sin((t1 * 2.2 + frame * 0.5) * math.pi) * 1.4 * t1
        s.poly([(x + w * t0, y, h - fh + a0), (x + w * t1, y, h - fh + a1), (x + w * t1, y, h + a1),
                (x + w * t0, y, h + a0)], m if i % 2 == 0 else m2, cull=False, outward=(0, 1, 0))


def smoke(s, x, y, z, frame=0, n=4, ramp="snow", shade=-1.0, rise=10, r0=0.06, grow=0.025, drift=(0.0, -0.02),
          rough=0.4):
    """A smoke/steam plume of `n` puffs cycling upward over 4 frames."""
    mat = ik.Material(ramp, shade, None, dither=0.0, snow=False)
    fx, fy = s.footprint
    for i in range(n):
        t = i + frame / 4.0
        px = min(max(x + drift[0] * t, 0.05), fx - 0.05)
        py = min(max(y + drift[1] * t, 0.05), fy - 0.05)
        r = r0 + grow * t
        s.ellipsoid(px, py, z + rise * t + r * 30, r, r, r * 40, mat, rough=rough)


WATER_Z = -5.0   # natural water level (KIT/NET contract): quays, dams, piers meet water 5 px below ground


def sea(s, x0, y0, x1, y1, layer=2, bank=None):
    """Natural water (river / sea / lake) at WATER_Z. Do not cover the same area with z=0 ground.
    bank: material for the visible back banks (faces at x0 facing +x and y0 facing +y)."""
    rect(s, x0, y0, x1, y1, "water", layer=layer, z=WATER_Z)
    if bank is not None:
        s.poly([(x0, y0, WATER_Z), (x0, y1, WATER_Z), (x0, y1, 0), (x0, y0, 0)], bank, outward=(1, 0, 0))
        s.poly([(x0, y0, WATER_Z), (x1, y0, WATER_Z), (x1, y0, 0), (x0, y0, 0)], bank, outward=(0, 1, 0))


def water_rect(s, x0, y0, x1, y1, z=0.0, layer=2, wall=None, depth=3):
    """Water surface (animates with render frame); optional low stone edge."""
    rect(s, x0, y0, x1, y1, "water", layer=layer, z=z)
    if wall:
        for (ax, ay, bx, by) in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x0, y1, x1, y1), (x0, y0, x0, y1)):
            if ax == bx:
                s.box(ax - 0.02, ay, 0, 0.04, by - ay, z + depth, wall)
            else:
                s.box(ax, ay - 0.02, 0, bx - ax, 0.04, z + depth, wall)


def fountain(s, cx, cy, r=0.16, frame=0, h=10):
    """Round basin with a jet that pulses over 4 frames."""
    stone = M("stone", shade=1)
    s.cylinder(cx, cy, 0, r, 3, stone, top=stone)
    s.ground([(cx + (r - 0.025) * math.cos(a), cy + (r - 0.025) * math.sin(a))
              for a in [2 * math.pi * k / 20 for k in range(20)]], "water", z=3.01, layer=6)
    s.cylinder(cx, cy, 3, 0.025, 4, stone)
    spray = ik.Material("water", 7.0, None, dither=0.0, snow=False)
    jh = h * (0.75 + 0.25 * math.sin(frame * math.pi / 2))
    s.ellipsoid(cx, cy, 7 + jh * 0.5, 0.025, 0.025, jh * 0.5, spray)
    for k in range(6):
        a = 2 * math.pi * k / 6 + frame * 0.3
        d = r * (0.35 + 0.15 * ((frame + k) % 2))
        s.ellipsoid(cx + d * math.cos(a), cy + d * math.sin(a), 4.5, 0.02, 0.02, 1.5, spray)


def gravestone(s, x, y, kind=0):
    st = M("stone", ramp="grey", shade=2)
    if kind % 3 == 0:
        s.box(x, y, 0, 0.05, 0.02, 4, st, top=st)
    elif kind % 3 == 1:
        s.box(x + 0.018, y, 0, 0.014, 0.014, 6, st)
        s.box(x, y, 3.5, 0.05, 0.014, 1.2, st)
    else:
        s.box(x, y, 0, 0.06, 0.03, 2, st)


def heap(s, cx, cy, r, h, mat, rough=0.35):
    """Bulk material heap (coal, gravel, logs, garbage)."""
    s.ellipsoid(cx, cy, 0, r, r * 0.85, h, mat, zmin=0, rough=rough)


def crate_stack(s, x, y, nx, ny, nz, size=0.08, ramps=("terra", "teal", "slate", "yellow")):
    """Shipping containers / crates in a block."""
    k = 0
    for iz in range(nz):
        for ix in range(nx):
            for iy in range(ny):
                r = ramps[(ix * 7 + iy * 3 + iz * 5) % len(ramps)]
                s.box(x + ix * size * 2.0, y + iy * size, iz * 5, size * 2.0 - 0.01, size - 0.01, 5,
                      M("metal", ramp=r, shade=0.5))
                k += 1
