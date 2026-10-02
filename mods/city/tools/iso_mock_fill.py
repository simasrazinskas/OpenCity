"""Populate the mockup City: civic buildings, zoned lots per district, farms, nature, props, traffic."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import iso_mock_city as MC  # noqa: E402

FP = {"res-low": ["1x1", "2x1", "1x2", "2x2"], "res-row": ["2x1", "3x1", "1x2", "1x3", "2x2"],
      "res-med": ["2x2", "3x2", "2x3", "3x3"], "res-high": ["2x2", "3x2", "2x3", "3x3"],
      "res-mixed": ["2x2", "3x2", "2x3"], "com-low": ["1x1", "2x1", "1x2", "2x2"],
      "com-high": ["2x2", "3x2", "2x3", "3x3", "4x3"], "off": ["1x1", "2x1", "2x2", "3x2", "2x3"],
      "off-high": ["2x2", "3x2", "2x3", "3x3"], "ind": ["1x1", "2x2", "3x2", "2x3", "3x3", "4x3"],
      "warehouse": ["3x2", "2x3", "3x3", "4x3"]}
MIX = {"downtown": [("com-high", 3, 5, 4), ("off-high", 3, 5, 4), ("res-high", 3, 5, 3), ("res-mixed", 3, 5, 1)],
       "mid": [("res-med", 2, 4, 4), ("res-mixed", 2, 4, 2), ("com-low", 2, 4, 2), ("off", 2, 4, 1),
               ("res-row", 2, 4, 1)],
       "suburb": [("res-low", 1, 5, 8), ("res-row", 2, 4, 1), ("com-low", 1, 3, 1)],
       "industry": [("ind", 1, 4, 3), ("warehouse", 2, 4, 2)]}

# (name, cx, cy, fx, fy)
CIVIC = [("cityhall", 28, 24, 3, 3), ("plaza", 31, 26, 1, 1), ("hospital", 38, 14, 3, 3),
         ("police", 23, 14, 2, 2), ("firestation", 18, 24, 2, 2), ("trainstation", 43, 19, 2, 4),
         ("powerplant-coal", 53, 9, 2, 2), ("farm-hub", 2, 14, 3, 3), ("windturbine", 3, 24, 1, 1),
         ("windturbine", 5, 25, 1, 1), ("windturbine", 3, 27, 1, 1), ("school", 23, 42, 2, 2),
         ("park-large", 29, 47, 2, 2), ("park-small", 33, 42, 1, 1), ("clinic", 39, 47, 2, 2),
         ("watertower", 12, 47, 1, 1), ("firehouse", 17, 52, 1, 1), ("park-large", 45, 42, 2, 2),
         ("postoffice", 33, 29, 2, 2), ("sportsfield", 17, 42, 2, 2), ("telecom-mast", 58, 24, 1, 1)]


def fill(city):
    rng = city.rng
    for name, cx, cy, fx, fy in CIVIC:
        city.place("civic", dict(name=name), cx, cy, fx, fy)
    for b in city.blocks:
        _fill_block(city, *b)
    _riverside(city)
    _farm_edges(city)
    _lamps_and_signals(city)
    _traffic(city)
    return city


def _pick(rng, mix):
    tot = sum(w for *_, w in mix)
    r = rng.uniform(0, tot)
    for p, lo, hi, w in mix:
        r -= w
        if r <= 0:
            return p, rng.randint(lo, hi)
    return mix[0][0], mix[0][1]


def _fill_block(city, x0, y0, w, h):
    rng = city.rng
    dist = city.district(x0 + w / 2, y0 + h / 2)
    if dist == "farm":
        return _farm_block(city, x0, y0, w, h)
    halves = [(y0, h // 2, 2), (y0 + h // 2, h - h // 2, 0)] if h >= 2 else [(y0, h, 0)]
    for (hy, hd, facing) in halves:
        x = x0
        while x < x0 + w:
            prefix, lvl = _pick(rng, MIX[dist])
            opts = []
            for fp in FP[prefix]:
                fw, fd = (int(v) for v in fp.split("x"))
                if fd <= hd and x + fw <= x0 + w and (dist != "suburb" or fd <= 2):
                    opts.append((fw, fd, fp))
            if not opts:
                x += 1
                continue
            big = max(o[0] * o[1] for o in opts)
            opts = [o for o in opts if o[0] * o[1] >= big * (0.5 if dist in ("downtown", "industry") else 0.25)]
            fw, fd, fp = rng.choice(opts)
            cy = hy if facing == 0 else hy + hd - fd
            if facing == 2:
                cy = hy
            else:
                cy = hy + hd - fd
            if city.place("zoned", dict(prefix=prefix, fp=fp, level=lvl, var=rng.randint(0, 3), facing=facing),
                          x, cy, fw, fd):
                if dist == "suburb" and fd < hd:
                    for xx in range(x, x + fw):
                        yy = cy + fd if facing == 2 else cy - 1
                        if city.free([(xx, yy)]) and rng.chance(0.6):
                            city.trees.append((xx + 0.5 + rng.uniform(-0.15, 0.15), yy + 0.5, rng.choice(
                                ["linden", "oak", "birch", "apple", "maple"]), rng.randint(1, 2), rng.randint(0, 99)))
                            city.occ.add((xx, yy))
                x += fw
            else:
                x += 1
    # leftover cells: small parks / trees / paving
    for xx in range(x0, x0 + w):
        for yy in range(y0, y0 + h):
            if city.free([(xx, yy)]):
                city.occ.add((xx, yy))
                if dist == "downtown":
                    city.terr[(xx, yy)] = "paving"
                    if rng.chance(0.5):
                        city.trees.append((xx + 0.5, yy + 0.5, "linden", 1, rng.randint(0, 99)))
                elif dist == "industry":
                    city.terr[(xx, yy)] = "gravel"
                else:
                    city.trees.append((xx + 0.5, yy + 0.5, rng.choice(["oak", "linden", "maple", "birch"]),
                                       2, rng.randint(0, 99)))


def _farm_block(city, x0, y0, w, h):
    rng = city.rng
    crops = [("wheat", "ripe"), ("wheat", "ripening"), ("corn", "growing"), ("rapeseed", "flowering"),
             ("vegetables", "ripe"), ("wheat", "stubble"), ("sunflower", "ripe"), ("wheat", "growing")]
    crop = crops[(x0 * 7 + y0 * 3) % len(crops)]
    axis = "x" if (x0 + y0) % 2 else "y"
    for x in range(x0, x0 + w):
        for y in range(y0, y0 + h):
            if city.free([(x, y)]):
                city.terr[(x, y)] = "field:%s:%s:%s" % (crop[0], crop[1], axis)
                city.occ.add((x, y))


def _riverside(city):
    rng = city.rng
    for x in range(MC.N):
        for y in (36, 40):
            if not city.free([(x, y)]):
                continue
            city.occ.add((x, y))
            if x < 12:
                city.terr[(x, y)] = "meadow"
                if rng.chance(0.3):
                    city.small.append(("reeds", x + 0.5, y + 0.5, x))
                continue
            if rng.chance(0.55):
                city.trees.append((x + 0.5, y + (0.35 if y == 36 else 0.65), rng.choice(["willow", "linden", "poplar"]),
                                   2, x * 3 + y))
            elif rng.chance(0.3):
                city.small.append(("bush", x + 0.5, y + 0.5, x))


def _farm_edges(city):
    rng = city.rng
    for x in range(0, 12):
        for y in range(0, 36):
            c = (x, y)
            if city.free([c]):
                city.occ.add(c)
                if rng.chance(0.55):
                    city.trees.append((x + 0.5, y + 0.5, rng.choice(["spruce", "fir", "pine", "oak", "birch"]),
                                       rng.randint(1, 2), x * 11 + y))
    for x in range(0, MC.N):
        for y in range(0, MC.N):
            c = (x, y)
            if city.free([c]) and (y < 2 or y > 61 or x < 4 or x > 61):
                city.occ.add(c)
                if rng.chance(0.6):
                    city.trees.append((x + 0.5, y + 0.5, rng.choice(["spruce", "oak", "birch", "pine"]), 2, x + y * 7))


def _lamps_and_signals(city):
    for (x, y), r in city.roads.items():
        if r.get("bridge") or r["cls"] == "gravel":
            continue
        m = city.mask((x, y))
        if r["ctrl"] == "signal":
            for k, st in enumerate(["red", "green", "red", "green"]):
                city.props.append(("signal", x, y, k, st))
            continue
        if r["ctrl"] == "stop":
            city.props.append(("stop", x, y, (x + y) % 2 * 2, "stop"))
            continue
        straight_x = m == 0b1010
        straight_y = m == 0b0101
        if straight_x and (x + y) % 3 == 0:
            city.lamps.append((x, y, 1 if (x // 3) % 2 else 3))
        if straight_y and (x + y) % 3 == 0:
            city.lamps.append((x, y, 0 if (y // 3) % 2 else 2))
    city.props.append(("bus", MC.AVENUE_X, 21, 2, None))
    city.props.append(("bus", MC.AVENUE_X, 44, 0, None))


def _lane_points(city, c, r, m, rng, dist):
    """Yield (x, y, dir) for cars on a straight road cell (right-hand traffic, LIFE lane contract)."""
    x, y = c
    q = [10 / 64.0] if r["cls"] in ("street", "gravel") else [10 / 64.0, 22 / 64.0]
    out = []
    along_x = m == 0b1010
    for off in q:
        for sgn in (1, -1):
            if rng.chance(0.55):
                t = rng.uniform(0.15, 0.85)
                if along_x:
                    out.append((x + t, y + 0.5 + sgn * off, "E" if sgn > 0 else "W"))
                else:
                    out.append((x + 0.5 - sgn * off, y + t, "S" if sgn > 0 else "N"))
    return out


LEN = {"bus": 0.95, "semi-tractor": 0.6, "box-truck": 0.6, "tipper-truck": 0.6, "farm-tractor": 0.5}


def _traffic(city):
    rng = city.rng
    lanes = {}
    dens = {"downtown": 0.75, "mid": 0.45, "suburb": 0.18, "farm": 0.05, "industry": 0.35}
    for c, r in sorted(city.roads.items()):
        if r.get("bridge") == "pier":
            m = 0b0101
        elif r.get("bridge") or r.get("crossing") or r["ctrl"]:
            continue
        else:
            m = city.mask(c)
        if m not in (0b1010, 0b0101):
            continue
        dist = city.district(c[0] + 0.5, c[1] + 0.5)
        near = abs(c[0] - MC.AVENUE_X) <= 6 and abs(c[1] - 28) <= 6
        if not rng.chance(1.0 if near else dens[dist] * (1.4 if r["cls"] != "street" else 1.0)):
            continue
        for (x, y, d) in _lane_points(city, c, r, m, rng, dist):
            if dist == "industry":
                model = rng.choice(["box-truck", "semi-tractor", "delivery-van", "tipper-truck", "sedan"])
            elif dist == "farm":
                model = rng.choice(["farm-tractor", "pickup"])
            elif r["cls"] == "avenue" and rng.chance(0.12):
                model = "bus"
            else:
                model = rng.choice(["sedan", "hatchback", "estate", "suv", "mpv", "taxi" if dist == "downtown" else
                                    "pickup", "delivery-van", "sports"])
            z = 8.0 if r.get("bridge") == "pier" else 0.0
            key = (d, round(y if d in ("E", "W") else x, 2))
            pos = x if d in ("E", "W") else y
            ln = LEN.get(model, 0.45)
            if any(abs(pos - p) < (ln + l2) / 2 + 0.08 for p, l2 in lanes.get(key, [])):
                continue
            lanes.setdefault(key, []).append((pos, ln))
            city.vehicles.append((model, x, y, d, rng.choice(MC.COLOURS), z))
        if dist in ("downtown", "mid", "suburb") and rng.chance({"downtown": 0.9, "mid": 0.5, "suburb": 0.25}[dist]):
            for _ in range(rng.randint(1, 3)):
                side = rng.choice([1, -1])
                t = rng.uniform(0.1, 0.9)
                if m == 0b1010:
                    city.people.append((rng.randint(0, 31), c[0] + t, c[1] + 0.5 + side * 26 / 64.0,
                                        rng.choice(["E", "W"])))
                else:
                    city.people.append((rng.randint(0, 31), c[0] + 0.5 + side * 26 / 64.0, c[1] + t,
                                        rng.choice(["N", "S"])))
