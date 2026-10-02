"""
iso_net_out_ground.py - writes every flat NET tile (roads, medians, arrows, wear, transitions, junction paint,
roundabouts, map edge, lanes, tram, rail, pipes, parking surfaces) and its manifest fragment.
"""
import os

import numpy as np

import iso_net_pal as P
from iso_net_core import OUT, hash2, render_ground, save_png, write_manifest_fragment
from iso_net_rail import crossing_tile, pipe_tile, rail_tile
from iso_net_roads import mask_label, mask_name, road_scene, road_tile
from iso_net_roadx import (arrow_scene, combine, control_extra, island_scene, map_edge_extra, median_tile, rot,
                           tile, tram_extra, transition_scene)

CLASS_ORDER = ["street", "avenue", "boulevard", "highway", "alley", "gravel"]
TITLE = {"street": "Street", "avenue": "Avenue", "boulevard": "Boulevard", "highway": "Highway", "alley": "Alley",
         "gravel": "Gravel road"}
DIRS = ["N", "E", "S", "W"]
SAMPLE = [5, 10, 3, 6, 12, 9, 7, 15]  # straight x2, curves x4, T, cross


def put(rel, img):
    save_png(os.path.join(OUT, rel), P.quantize(img))
    return rel


def item(rel, img, label):
    return {"file": put(rel, img), "label": label}


def g(title, items, columns=8, scale=2, note=None):
    d = {"title": title, "columns": columns, "scale": scale, "items": items}
    if note:
        d["note"] = note
    return d


def roads():
    groups = []
    for cls in CLASS_ORDER:
        tw = [item(f"roads/{cls}/{cls}-{mask_name(m)}.png", road_tile(cls, m), mask_label(m)) for m in range(16)]
        groups.append(g(f"{TITLE[cls]}: all 16 connection masks", tw,
                        note="two-way; masks N/E/S/W = upper-right/lower-right/lower-left/upper-left edge"))
        ow = [item(f"roads/{cls}/{cls}-ow-{mask_name(m)}.png", road_tile(cls, m, True), mask_label(m)) for m in range(16)]
        groups.append(g(f"{TITLE[cls]}: one-way, 16 masks", ow, note="direction comes from the arrow overlay"))
    return groups


def medians():
    items = []
    for cls in ("avenue", "boulevard", "highway"):
        for sb, lab in ((1, "N"), (2, "E"), (4, "S"), (8, "W"), (5, "N+S"), (10, "E+W")):
            items.append(item(f"roads/median/{cls}-median-{mask_name(sb)}.png", median_tile(cls, sb), f"{cls} {lab}"))
    paired = []
    for cls in ("avenue", "boulevard", "highway"):
        for m, sb, d in ((5, 2, 0), (5, 8, 2), (10, 4, 1), (10, 1, 3)):
            t = road_tile(cls, m, True)
            from iso_net_core import over
            over(t, median_tile(cls, sb), 0, 0)
            over(t, tile(arrow_scene(d)), 0, 0)
            paired.append(item(f"roads/median/{cls}-paired-{mask_name(m)}-{mask_name(sb)}.png", t, f"{cls} {DIRS[d]}-bound"))
    arrows = [item(f"roads/arrows/arrow-{DIRS[d]}.png", tile(arrow_scene(d)), f"to {DIRS[d]}") for d in range(4)]
    return [g("Paired carriageways: median overlays (sideBlock)", items, columns=6,
              note="avenue planter, boulevard tree lawn (trees are props), highway jersey barrier"),
            g("Paired carriageways composed (one-way + median + arrows)", paired, columns=4),
            g("One-way arrows overlay", arrows, columns=4)]


def wear():
    items = []
    for cls, m in (("street", 5), ("street", 15), ("avenue", 10)):
        for w, lab in ((0, "new"), (1, "used"), (2, "worn")):
            items.append(item(f"roads/wear/{cls}-{mask_name(m)}-wear{w}.png", road_tile(cls, m, wear=w), f"{cls} {lab}"))
    return [g("Road wear: new, used, worn (manholes, patches, cracks)", items, columns=3)]


def transitions():
    pairs = [("street", "avenue"), ("avenue", "boulevard"), ("street", "boulevard"), ("alley", "street"),
             ("gravel", "street"), ("street", "alley")]
    items = []
    for a, b in pairs:
        for k in range(4):
            sc = rot(transition_scene(a, b), k)
            items.append(item(f"roads/transition/{a}-{b}-{k}.png", tile(sc), f"{a}>{b} {['N-S', 'E-W', 'S-N', 'W-E'][k]}"))
    return [g("Class transitions (first class toward the arrow's start)", items, columns=4,
              note="kerb tapers at 45 degrees; first class on the N/E/S/W side for rotations 0-3")]


def control():
    items = []
    for kind, lab in (("signal", "signal stop lines"), ("stop", "all-way stop"), ("yield", "yield teeth"), ("mini", "mini roundabout")):
        for cls, m in (("street", 15), ("street", 7), ("avenue", 15)):
            items.append(item(f"junction/paint-{kind}-{cls}-{mask_name(m)}.png",
                              tile(road_scene(cls, m, extra=control_extra(kind))), f"{lab} {cls} {mask_label(m)}"))
    for d in range(4):
        sc = rot(road_scene("highway", 5, True, extra=control_extra("ramp")), d)
        items.append(item(f"junction/ramp-{DIRS[d]}.png", tile(sc), f"highway ramp {DIRS[d]}"))
    return [g("Junction control road paint", items, columns=6)]


def roundabouts():
    isl = [item("roundabout/island-1x1.png", tile(island_scene()), "island 1x1")]
    for y in range(3):
        for x in range(3):
            isl.append(item(f"roundabout/island-3x3-{x}{y}.png", tile(island_scene(x, y, 3)), f"island 3x3 ({x},{y})"))
    return [g("Roundabout islands (ring cells are one-way street pieces)", isl, columns=5)]


def map_edge():
    items = [item(f"roads/edge/highway-edge-{DIRS[d]}.png", tile(road_scene("highway", 5 if d % 2 == 0 else 10, extra=map_edge_extra(d))),
                  f"map edge {DIRS[d]}") for d in range(4)]
    return [g("Highway outside connection at the map edge", items, columns=4,
              note="road fades into the map border; in/out arrows; a sign gantry prop stands on it")]


def lanes():
    items = []
    for cls, ow in (("street", False), ("avenue", False), ("boulevard", True)):
        for m in (5, 3, 7, 15):
            items.append(item(f"lanes/bus-{cls}-{mask_name(m)}.png", road_tile(cls, m, ow, bus=True), f"bus {cls} {mask_label(m)}"))
    for cls in ("street", "avenue"):
        for m in (5, 3, 7, 15):
            items.append(item(f"lanes/bike-{cls}-{mask_name(m)}.png", road_tile(cls, m, bike=True), f"bike {cls} {mask_label(m)}"))
    tram = [item(f"lanes/tram-avenue-{mask_name(m)}.png", tile(road_scene("avenue", m, extra=tram_extra)), mask_label(m)) for m in range(16)]
    tram += [item(f"lanes/tram-street-{mask_name(m)}.png", tile(road_scene("street", m, extra=tram_extra)), "street " + mask_label(m))
             for m in SAMPLE]
    return [g("Bus lanes (red) and bike lanes (green)", items, columns=8),
            g("Tram track in the road: avenue all 16 masks, street samples", tram, columns=8)]


def rail():
    items = [item(f"rail/rail-{mask_name(m)}.png", rail_tile(m), mask_label(m)) for m in range(16)]
    items += [item("rail/crossing-rail-ns.png", crossing_tile(True), "level crossing, rail N-S"),
              item("rail/crossing-rail-ew.png", crossing_tile(False), "level crossing, rail E-W")]
    return [g("Rail track: 16 masks (switches at T), road crossings", items, columns=9)]


def pipes():
    groups = []
    for kind in ("water", "sewage"):
        items = [item(f"util/pipe-{kind}-{mask_name(m)}.png", pipe_tile(m, kind), mask_label(m)) for m in range(16)]
        groups.append(g(f"{kind.capitalize()} pipes (underground view): 16 masks", items, columns=8))
    return groups


# --------------------------------------------------------------------------- parking surfaces
LOT = np.array([84, 88, 95], np.float32)


def lot_scene(stalls_side, border=0):
    """stalls_side: 0..3 = stall row along the N/E/S/W half (-1 = aisle only, 4 = planter island).
    border: edge mask with a kerb + hedge (lot boundary)."""
    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        h = hash2(u, v, 71)
        col = np.zeros((U.shape[0], 3), np.float32) + np.where((h < 0.05)[:, None], LOT * 1.08, LOT)
        raised = np.zeros(U.shape, bool)
        if stalls_side >= 0 and stalls_side < 4:
            # local coords: depth from the stall edge, along the row
            depth, along = [(V, U), (64 - U, V), (64 - V, U), (U, V)][stalls_side]
            row = depth < 30
            div = row & (np.abs(((along + 64) % 21.33) - 0.5) < 1.0) & (depth > 2)
            endl = (np.abs(depth - 29) < 1.0)
            col = np.where((div | endl)[:, None], P.WHITE, col)
            stop = row & (depth < 6) & (depth > 3) & (np.abs(((along + 64) % 21.33) - 10.7) < 4)
            col = np.where(stop[:, None], P.CONC, col)
            raised |= stop
        elif stalls_side == 4:
            r = np.hypot(U - 32, V - 32)
            isl = r < 12
            col = np.where(isl[:, None], np.where((r > 10)[:, None], P.KERB, P.GRASS), col)
            raised |= isl
        else:
            arrow = (np.abs(U - 32) < 1.2) & (np.abs(V - 32) < 12)
            col = np.where(arrow[:, None], P.WHITE, col)
        for bit, dd in ((1, V), (2, 64 - U), (4, 64 - V), (8, U)):
            if border & bit:
                b = dd < 6
                col = np.where(b[:, None], np.where((dd > 4)[:, None], P.KERB, np.where((h < 0.4)[:, None], P.GRASS_D, P.GRASS)), col)
                raised |= b
        return col, np.ones(U.shape, bool), raised, None, None
    return scene


def lot_tile(side, border=0):
    return render_ground(lot_scene(side, border))


def parking():
    items = [item(f"parking/lot-stalls-{DIRS[s]}.png", lot_tile(s), f"stalls {DIRS[s]} half") for s in range(4)]
    items += [item("parking/lot-aisle.png", lot_tile(-1), "aisle"), item("parking/lot-island.png", lot_tile(4), "planter")]
    items += [item(f"parking/lot-stalls-{DIRS[s]}-edge.png", lot_tile(s, 1 << s), f"stalls {DIRS[s]} + hedge") for s in range(4)]
    return [g("Parking lot surfaces", items, columns=6, note="two stall-row cells back to back make a double row")]


def build():
    groups = []
    for fn in (roads, medians, wear, transitions, control, roundabouts, map_edge, lanes, rail, pipes, parking):
        groups += fn()
    write_manifest_fragment(os.path.join(OUT, "_frag", "10-ground.json"), groups)
    return groups


if __name__ == "__main__":
    gs = build()
    print(sum(len(x["items"]) for x in gs), "ground tiles in", len(gs), "groups")
