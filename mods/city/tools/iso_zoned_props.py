"""iso_zoned_props.py - lot dressing for ZONED buildings: trees, hedges, fences, cars, pools, lamps,
roof gear, awnings, signs, yard clutter. Every prop respects the lot state (no cars when abandoned,
charred trees when burnt, nothing during construction)."""
import isokit as ik

import iso_zoned_mats as M

CAR_RAMPS = ["red", "glass", "snow", "slate", "yellow", "teal", "grey", "rose", "olive", "brick"]


def _dressing(lot):
    return lot.stage == 0 and lot.state != "collapsed"


KIND_SPECIES = {"round": ["linden", "oak", "maple", "linden"], "tall": ["poplar", "birch"],
                "conifer": ["spruce", "fir", "pine"], "small": ["apple", "cherry"]}


def tree(lot, x, y, size=1.0, kind=None):
    """Lot tree from isokit's species library (seasonal: bare in winter, blossom in spring).
    kind: 'round' | 'tall' | 'conifer' | 'small'; size >= 1 gives a mature tree, else a young one."""
    if not _dressing(lot):
        return
    h = int(abs(x * 977 + y * 613) * 1000 + lot.seed) & 0xFFFF
    kind = kind or ["round", "round", "tall", "conifer"][h % 4]
    names = KIND_SPECIES[kind]
    sp = names[(h // 7) % len(names)]
    stage = 2 if size >= 1.0 else (1 if size >= 0.55 else 0)
    # keep the crown inside the footprint's horizontal screen extent (engine strip slicing):
    # slide the tree along the screen-vertical diagonal if it sits too close to the left/right corner
    m = (0.36 if stage == 2 else 0.26) if kind != "tall" else 0.16
    d = x - y
    if d < -lot.D + m:
        sh = (-lot.D + m - d) / 2
        x, y = x + sh, y - sh
    elif d > lot.W - m:
        sh = (d - lot.W + m) / 2
        x, y = x - sh, y + sh
    state = "alive"
    if lot.state == "burnt":
        state = "burnt"
    elif lot.state in ("abandoned", "collapsed") and h % 3 == 0:
        state = "dead"
    ik.add_tree(lot.s, x, y, sp, lot.season, stage, h, state)


def scatter_trees(lot, n, avoid, size=1.0, kinds=None, margin=0.1, x0=0.0, y0=0.0, x1=None, y1=None):
    """Place up to n trees at random free spots (not inside the avoid rects (x, y, dx, dy), spaced)."""
    x1 = lot.W if x1 is None else x1
    y1 = lot.D if y1 is None else y1
    from isokit.noise import Rng
    rng = Rng(lot.seed + 77)
    placed = []
    tries = 0
    while len(placed) < n and tries < n * 30:
        tries += 1
        x, y = rng.uniform(x0 + margin, x1 - margin), rng.uniform(y0 + margin, y1 - margin)
        if any(ax - 0.08 < x < ax + adx + 0.08 and ay - 0.08 < y < ay + ady + 0.1 for ax, ay, adx, ady in avoid):
            continue
        if any((x - px) ** 2 + (y - py) ** 2 < 0.2 ** 2 for px, py in placed):
            continue
        placed.append((x, y))
    for i, (x, y) in enumerate(placed):
        tree(lot, x, y, size * rng.uniform(0.85, 1.15), kinds[i % len(kinds)] if kinds else rng.choice(['round', 'round', 'tall', 'conifer']))
    return placed


def bush(lot, x, y, r=0.06, mat="foliage"):
    if not _dressing(lot) or lot.state == "burnt":
        return
    lot.s.blob([(x, y, 3 * r / 0.06, r, 4 * r / 0.06)], mat if lot.state == "ok" else "foliage_light", rough=0.4)


def hedge(lot, x, y, dx, dy, h=5):
    if not _dressing(lot) or lot.state == "burnt":
        return
    mat = ik.mat("foliage", clump=0.8) if lot.state == "ok" else ik.mat("foliage_light")
    lot.s.box(x, y, 0, dx, dy, h if lot.state == "ok" else h + 2, mat)


def fence(lot, x, y, dx, dy, kind="picket", h=None):
    """Thin fence/wall along a rect. kind: picket | wall | chain | rail."""
    if not _dressing(lot):
        return
    mats = {"picket": ik.mat("plaster_white"), "wall": ik.mat("stone"), "chain": ik.mat("metal_light"),
            "rail": ik.mat("wood"), "brick": ik.mat("brick")}
    hh = h or {"picket": 3, "wall": 3, "chain": 5, "rail": 3, "brick": 4}[kind]
    m = lot.m(mats[kind])
    if lot.state in ("abandoned", "burnt"):
        # broken: draw in two pieces with a gap
        if dx >= dy:
            lot.s.box(x, y, 0, dx * 0.4, dy, hh, m)
            lot.s.box(x + dx * 0.65, y, 0, dx * 0.35, dy, hh - 1, m)
        else:
            lot.s.box(x, y, 0, dx, dy * 0.45, hh, m)
            lot.s.box(x, y + dy * 0.7, 0, dx, dy * 0.3, hh - 1, m)
        return
    lot.s.box(x, y, 0, dx, dy, hh, m)


def car(lot, x, y, along="y", col=None):
    """Parked car centred at (x, y); along = axis of its length."""
    if not lot.live:
        return
    col = col or lot.pick(CAR_RAMPS)
    L, Wd = 0.30, 0.15
    dx, dy = (L, Wd) if along == "x" else (Wd, L)
    body = ik.mat("plain", ramp=col, shade=1.5)
    s = lot.s
    s.box(x - dx / 2, y - dy / 2, 1, dx, dy, 4, body)
    gl = ik.mat("plain", ramp="glass", shade=-1.0)
    if along == "x":
        s.box(x - dx * 0.28, y - dy / 2 + 0.015, 5, dx * 0.5, dy - 0.03, 3, gl, top=body)
    else:
        s.box(x - dx / 2 + 0.015, y - dy * 0.28, 5, dx - 0.03, dy * 0.5, 3, gl, top=body)


def pool(lot, x, y, dx, dy):
    if lot.stage in (1, 2):
        return
    lot.pave(x - 0.05, y - 0.05, dx + 0.1, dy + 0.1, "paving", layer=1)
    if lot.live:
        lot.s.ground([(x, y), (x + dx, y), (x + dx, y + dy), (x, y + dy)], "water", layer=2)
    elif lot.stage == 0:
        lot.s.ground([(x, y), (x + dx, y), (x + dx, y + dy), (x, y + dy)], "mud", layer=2)


def lamp(lot, x, y, h=12):
    if not _dressing(lot):
        return
    lot.s.box(x - 0.01, y - 0.01, 0, 0.02, 0.02, h, lot.m("plain", ramp="slate"))
    lot.s.box(x - 0.025, y - 0.025, h, 0.05, 0.05, 2, M.lamp_mat() if lot.live else lot.m("plain"))


def ac_units(lot, x, y, z, n=2):
    if not lot.roofed() or lot.stage == 3:
        return
    for i in range(n):
        lot.s.box(x + i * 0.1, y, z, 0.07, 0.07, 4, lot.m("metal_light"))


def dish(lot, x, y, z):
    if not lot.roofed():
        return
    lot.s.box(x, y, z, 0.01, 0.01, 4, lot.m("plain"))
    lot.s.ellipsoid(x + 0.02, y + 0.02, z + 5, 0.03, 0.03, 2, lot.m("plaster_white"))


def chimney(lot, x, y, z, h=8, mat="brick", w=0.06):
    if not lot.roofed():
        return
    lot.s.box(x, y, z, w, w, h, lot.m(mat))


def panels(lot, x, y, z, dx, dy, tilt=3):
    """Solar panels on a flat surface (rows tilted toward +Y)."""
    if not lot.roofed() or lot.state != "ok":
        return
    rows = max(1, int(dy / 0.12))
    for i in range(rows):
        yy = y + i * dy / rows
        lot.s.roof_shed(x, yy, z, dx, dy / rows * 0.8, tilt, M.PANELS, high="-y", overhang=0.0)


def awning(lot, x, y, z, dx, ramp="red", depth=0.08, drop=3, face="+y"):
    """Striped shop awning sloping out from a wall at height z."""
    if lot.stage in (1, 2) or lot.state in ("collapsed", "burnt"):
        return
    m = M.stripes(ramp) if lot.state == "ok" else M.st(M.stripes(ramp), "abandoned")
    s = lot.s
    if face == "+y":
        s.quad((x, y, z), (x + dx, y, z), (x + dx, y + depth, z - drop), (x, y + depth, z - drop), m,
               outward=(0, 1, 1))
        s.quad((x, y + depth, z - drop), (x + dx, y + depth, z - drop), (x + dx, y + depth, z - drop - 2),
               (x, y + depth, z - drop - 2), m, outward=(0, 1, 0))
    else:
        s.quad((x, y, z), (x, y + dx, z), (x + depth, y + dx, z - drop), (x + depth, y, z - drop), m,
               outward=(1, 0, 1))
        s.quad((x + depth, y, z - drop), (x + depth, y + dx, z - drop), (x + depth, y + dx, z - drop - 2),
               (x + depth, y, z - drop - 2), m, outward=(1, 0, 0))


def sign(lot, x, y, z, dx, h=4, ramp="red", text="snow", face="+y"):
    """Flat sign board on a wall (+y: front face at y, +x: side face at x)."""
    if lot.stage in (1, 2) or lot.state in ("collapsed", "burnt"):
        return
    m = M.signboard(ramp, text, "ok" if lot.state == "ok" else "abandoned")
    if lot.state != "ok":
        m = M.st(m, "abandoned")
    if face == "+y":
        lot.s.box(x, y, z, dx, 0.02, h, m)
    else:
        lot.s.box(x, y, z, 0.02, dx, h, m)


def cafe_tables(lot, x, y, dx, n=3, ramp="red"):
    if not lot.live:
        return
    for i in range(n):
        cx = x + dx * (i + 0.5) / n
        lot.s.box(cx - 0.005, y - 0.005, 0, 0.01, 0.01, 6, ik.mat("plain"))
        lot.s.cone(cx, y, 6, 0.06, 3, ik.mat("plain", ramp=ramp, shade=2), segs=8)


def crates(lot, x, y, n=3):
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    for i in range(n):
        h = 3 + (i % 2) * 2
        lot.s.box(x + (i % 3) * 0.07, y + (i // 3) * 0.07, 0, 0.06, 0.06, h, lot.m("wood"))


def container(lot, x, y, along="x", ramp=None, z=0):
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    ramp = ramp or lot.pick(["red", "teal", "glass", "yellow", "terra", "grey"])
    L, Wd = 0.38, 0.14
    dx, dy = (L, Wd) if along == "x" else (Wd, L)
    lot.s.box(x, y, z, dx, dy, 7, lot.m("metal", ramp=ramp, shade=0.5))


def truck(lot, x, y, along="x", ramp=None):
    """Semi truck: cab + trailer (parked)."""
    if not lot.live:
        return
    ramp = ramp or lot.pick(["red", "glass", "snow", "yellow", "teal"])
    s = lot.s
    trailer = ik.mat("metal_light")
    cab = ik.mat("plain", ramp=ramp, shade=1.5)
    if along == "x":
        s.box(x, y, 1, 0.5, 0.14, 8, trailer)
        s.box(x + 0.52, y, 1, 0.12, 0.14, 7, cab)
    else:
        s.box(x, y, 1, 0.14, 0.5, 8, trailer)
        s.box(x, y + 0.52, 1, 0.14, 0.12, 7, cab)


def tank(lot, x, y, r, h, mat="metal_light"):
    if lot.stage in (1, 2):
        return
    hh = h if lot.state != "collapsed" else h * 0.4
    lot.s.cylinder(x, y, 0, r, hh, lot.m(mat), segs=16)
    if lot.state != "collapsed":
        lot.s.dome(x, y, hh, r, r * 14, lot.m(mat))


def smokestack(lot, x, y, r, h, mat="brick", band="red"):
    if lot.stage in (1, 2):
        return
    hh = h if lot.state != "collapsed" else h * 0.3
    lot.s.cylinder(x, y, 0, r, hh, lot.m(mat), segs=12)
    if band and lot.state != "collapsed":
        lot.s.cylinder(x, y, hh - 5, r * 1.08, 3, lot.m("plain", ramp=band, shade=1), segs=12)


def dumpster(lot, x, y, ramp="teal"):
    if lot.stage in (1, 2) or lot.state == "collapsed":
        return
    lot.s.box(x, y, 0, 0.12, 0.08, 4, lot.m("metal", ramp=ramp))


def bench(lot, x, y, along="x"):
    if not _dressing(lot):
        return
    dx, dy = (0.1, 0.03) if along == "x" else (0.03, 0.1)
    lot.s.box(x, y, 0, dx, dy, 2, lot.m("wood"))


def mailbox(lot, x, y):
    if not _dressing(lot):
        return
    lot.s.box(x, y, 0, 0.01, 0.01, 4, lot.m("wood"))
    lot.s.box(x - 0.01, y - 0.015, 4, 0.03, 0.03, 2, lot.m("plain", ramp="slate"))


def flagpole(lot, x, y, h=24, ramp="red"):
    if not _dressing(lot):
        return
    lot.s.box(x, y, 0, 0.012, 0.012, h, lot.m("metal_light"))
    if lot.live:
        lot.s.box(x + 0.012, y, h - 6, 0.12, 0.01, 5, ik.mat("plain", ramp=ramp, shade=1.5))
