"""iso_zoned_build.py - construction stages, collapse debris and generic rubble for ZONED lots.

finish(lot) is called by every zone generator after the model: it turns the recorded masses into the
foundation (stage 1), the concrete frame with scaffolding and a tower crane (stage 2) or the
scaffolding around a nearly finished building (stage 3), and adds debris to collapsed buildings.
"""
import isokit as ik
from isokit.noise import Rng

import iso_zoned_mats as M

ST = ik.STOREY
CONC = ik.mat("concrete")
SCAF = ik.mat("metal_light", ramp="grey", shade=1.0)
PLANK = ik.mat("wood", shade=1.5)
HOARD = ik.mat("wood", ramp="sand", shade=1.0)


def _site_clutter(lot, rng, n=2):
    s, W, D = lot.s, lot.W, lot.D
    # hoarding along the front edge with a gate in the middle
    g0, g1 = W * 0.4, W * 0.6
    s.box(0.04, D - 0.06, 0, g0 - 0.04, 0.03, 5, HOARD)
    s.box(g1, D - 0.06, 0, W - g1 - 0.04, 0.03, 5, HOARD)
    for i in range(n):
        x, y = rng.uniform(0.1, W - 0.3), rng.uniform(0.1, 0.3)
        k = i % 3
        if k == 0:     # pallets of bricks
            s.box(x, y, 0, 0.1, 0.1, 1, PLANK)
            s.box(x + 0.01, y + 0.01, 1, 0.08, 0.08, 4, ik.mat("brick"))
        elif k == 1:   # skip container
            s.box(x, y, 0, 0.2, 0.1, 5, ik.mat("metal", ramp="yellow", shade=0.5))
        else:          # site cabin
            s.box(x, y, 0, 0.22, 0.1, 9, ik.mat("windows_siding", ramp="glass", base=ik.mat("siding", ramp="glass", shade=1.5)))
    # site toilet
    s.box(W - 0.14, 0.08, 0, 0.06, 0.06, 8, ik.mat("plain", ramp="glass", shade=1.0))


def _scaffold(s, x, y, z0, dx, dy, top, faces=("+y", "+x"), frac=1.0):
    """Scaffold poles + plank decks a little outside the given faces."""
    off = 0.05
    if "+y" in faces:
        n = max(2, int(dx * frac / 0.16) + 1)
        yy = y + dy + off
        for i in range(n):
            s.box(x + i * dx * frac / (n - 1) - 0.008, yy, z0, 0.016, 0.016, top - z0 + 4, SCAF)
        for z in range(int(z0) + ST, int(top) + 5, ST):
            s.box(x, yy - 0.03, z, dx * frac, 0.05, 1, PLANK)
            s.box(x, yy + 0.015, z + 4, dx * frac, 0.008, 1, SCAF)
    if "+x" in faces:
        n = max(2, int(dy * frac / 0.16) + 1)
        xx = x + dx + off
        y0 = y + dy * (1 - frac)
        for i in range(n):
            s.box(xx, y0 + i * dy * frac / (n - 1) - 0.008, z0, 0.016, 0.016, top - z0 + 4, SCAF)
        for z in range(int(z0) + ST, int(top) + 5, ST):
            s.box(xx - 0.03, y0, z, 0.05, dy * frac, 1, PLANK)
            s.box(xx + 0.015, y0, z + 4, 0.008, dy * frac, 1, SCAF)


def _crane(lot, height):
    s, W = lot.s, lot.W
    mx, my = 0.18, 0.18
    s.box(mx - 0.04, my - 0.04, 0, 0.08, 0.08, 3, CONC)
    s.box(mx - 0.025, my - 0.025, 3, 0.05, 0.05, height, M.LATTICE_Y)
    jl = max(0.6, W - 0.35)
    s.box(mx - 0.02, my - 0.02, height, jl + 0.02, 0.04, 3, M.LATTICE_Y)
    s.box(mx - 0.12, my - 0.03, height, 0.1, 0.06, 4, CONC)                 # counterweight
    s.box(mx - 0.03, my - 0.03, height + 3, 0.06, 0.06, 6, M.LATTICE_Y)       # apex
    s.box(mx + 0.02, my - 0.04, height - 6, 0.06, 0.08, 6, ik.mat("windows_siding", ramp="yellow",
                                                                     base=ik.mat("plain", ramp="yellow", shade=1)))
    hx = mx + jl * 0.7
    s.box(hx - 0.003, my - 0.003, height * 0.55, 0.006, 0.006, height * 0.45, ik.mat("plain", ramp="slate"))
    s.box(hx - 0.03, my - 0.03, height * 0.55 - 3, 0.06, 0.06, 3, ik.mat("plain", ramp="yellow", shade=1))


def finish(lot):
    """Complete the model for the lot's stage/state. Returns the isokit Scene."""
    s = lot.s
    rng = Rng(lot.seed + 991)
    if lot.stage == 1:
        for (x, y, z, dx, dy, dz) in lot.masses:
            if z > 0:
                continue
            s.box(x - 0.03, y - 0.03, 0, dx + 0.06, dy + 0.06, 2, CONC)
            s.box(x - 0.05, y - 0.05, 0, dx + 0.1, 0.02, 3, PLANK)
            s.box(x - 0.05, y + dy + 0.03, 0, dx + 0.1, 0.02, 3, PLANK)
            for cx in (x + 0.04, x + dx - 0.04):
                for cy in (y + 0.04, y + dy - 0.04):
                    s.box(cx - 0.01, cy - 0.01, 2, 0.02, 0.02, 6, ik.mat("plain", ramp="red", shade=-1))
        s.blob([(lot.W - 0.25, 0.3, 2, 0.14, 7)], ik.mat("dirt", shade=1.0), rough=0.4)
        _site_clutter(lot, rng, 2)
    elif lot.stage == 2:
        top = max([z + dz for (x, y, z, dx, dy, dz) in lot.masses] or [ST])
        cut = max(ST, int(top * 0.6 / ST) * ST)
        for (x, y, z, dx, dy, dz) in lot.masses:
            h = min(z + dz, cut) - z
            if h <= 0:
                continue
            nf = max(1, int(h // ST))
            for k in range(nf + 1):
                s.box(x, y, z + k * ST, dx, dy, 1.5, CONC)
            nx, ny = max(2, int(dx / 0.2) + 1), max(2, int(dy / 0.2) + 1)
            for i in range(nx):
                for j in range(ny):
                    if 0 < i < nx - 1 and 0 < j < ny - 1:
                        continue
                    px = x + i * (dx - 0.03) / (nx - 1)
                    py = y + j * (dy - 0.03) / (ny - 1)
                    s.box(px, py, z, 0.03, 0.03, nf * ST, CONC)
            # stair/lift core
            s.box(x + dx * 0.4, y + dy * 0.4, z, min(0.15, dx * 0.2), min(0.15, dy * 0.2), nf * ST + 6,
                  ik.mat("concrete", shade=-0.5))
            _scaffold(s, x, y, z, dx, dy, z + nf * ST, frac=1.0)
        if top >= 35:
            _crane(lot, max(top, cut) + 22)
        _site_clutter(lot, rng, 3)
    elif lot.stage == 3:
        for i, (x, y, z, dx, dy, dz) in enumerate(lot.masses):
            if z > 0 and i > 0:
                continue
            _scaffold(s, x, y, z, dx, dy, z + dz, faces=("+y",), frac=0.55)
        if lot.top >= 60:
            _crane(lot, lot.top + 18)
        _site_clutter(lot, rng, 2)
    if lot.state == "collapsed" and lot.stage == 0:
        for (x, y, z, dx, dy, dz) in lot.masses:
            if z > 0:
                continue
            debris(s, rng, x + 0.04, y + 0.04, dx - 0.08, dy - 0.08, max(6.0, min(dz, 80) * 0.2))
    return s


def debris(s, rng, x, y, dx, dy, h, n=None):
    """A rubble heap: a low mound (higher in the middle) of broken concrete and brick, loose chunks,
    and a few beams and rebar sticking out."""
    conc = ik.mat("rock", ramp="grey", shade=-0.3)
    conc2 = ik.mat("rock", ramp="stone", shade=-0.8)
    brick = ik.mat("rock", ramp="brick", shade=-0.3)
    cx0, cy0 = x + dx / 2, y + dy / 2
    n = n or max(4, int(dx * dy * 10))
    # core mound: one jagged heightfield heap (faceted, flat-shaded), higher in the middle
    import numpy as np
    from isokit.noise import value2
    sd = rng.randint(0, 9999)

    def zfn(X, Y):
        u = np.clip(1 - np.abs((X - cx0) / (dx / 2)), 0, 1)
        v = np.clip(1 - np.abs((Y - cy0) / (dy / 2)), 0, 1)
        bump = (u * v) ** 0.6
        rough = value2(X * 9.0, Y * 9.0, 1.0, sd) - 0.5
        return np.maximum(0.0, h * bump * (1.0 + 0.6 * rough) - 1.0)
    nn = max(6, int(max(dx, dy) * 7))
    s.heightfield(x, y, dx, dy, zfn, conc, n=nn, steep=conc2, steep_at=0.7)
    # faceted chunks on and around the heap
    for i in range(n * 2):
        u, v = rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4)
        cx, cy = cx0 + u * dx, cy0 + v * dy
        z = float(zfn(np.array(cx), np.array(cy))) * 0.75
        r = rng.uniform(0.05, 0.1)
        s.rock(cx, cy, z, r, r * 30, rng.choice([conc, conc2, brick]), seed=rng.randint(0, 9999))
    # loose chunks
    for i in range(n * 2):
        cx, cy = x + dx * rng.uniform(0.05, 0.85), y + dy * rng.uniform(0.05, 0.85)
        w = rng.uniform(0.03, 0.06)
        m = ik.mat("concrete", shade=rng.uniform(-0.5, 1.0)) if rng.chance(0.6) else ik.mat("brick")
        s.box(cx, cy, 0, w, w * rng.uniform(0.6, 1.4), rng.uniform(1.5, 3.5), m)
    # beams / rebar
    for i in range(max(2, n // 3)):
        cx, cy = x + dx * rng.uniform(0.15, 0.6), y + dy * rng.uniform(0.15, 0.7)
        L = rng.uniform(0.15, 0.3)
        hz = h * rng.uniform(0.6, 1.2)
        mat = ik.mat("plain", ramp="wood", shade=-0.5) if rng.chance(0.5) else ik.mat("plain", ramp="red", shade=-1.5)
        if rng.chance(0.5):
            s.limb((cx, cy, h * 0.3), (cx + L, cy, hz), 0.012, 0.012, mat)
        else:
            s.limb((cx, cy, h * 0.3), (cx, cy + L, hz), 0.012, 0.012, mat)


def rubble(W, D, seed=0):
    """Generic rubble lot (the rubble-WxD actors)."""
    s = ik.Scene((W, D), seed=seed)
    rng = Rng(seed + 5)
    s.ground([(0, 0), (W, 0), (W, D), (0, D)], "dirt")
    for _ in range(int(4 * W * D)):
        x, y = rng.uniform(0.05, W - 0.3), rng.uniform(0.05, D - 0.3)
        s.ground([(x, y), (x + 0.25, y), (x + 0.25, y + 0.18), (x, y + 0.18)], "gravel", layer=1)
    debris(s, rng, 0.1, 0.1, W - 0.2, D - 0.2, 12 + 3 * min(W, D))
    # wall stubs
    for _ in range(max(1, W * D // 2)):
        x, y = rng.uniform(0.15, W - 0.5), rng.uniform(0.15, D - 0.5)
        if rng.chance(0.5):
            s.box(x, y, 0, rng.uniform(0.2, 0.4), 0.05, rng.uniform(6, 14), M.st(ik.mat("brick"), "abandoned"))
        else:
            s.box(x, y, 0, 0.05, rng.uniform(0.2, 0.4), rng.uniform(6, 14), M.st(ik.mat("concrete"), "abandoned"))
    return s
