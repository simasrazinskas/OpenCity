"""
iso_net_out_props.py - writes every NET prop / structure sprite (bridges, elevated highway, signals, signs, lamps,
light pools, stops, furniture, add-on strips, rail props, power lines, network buildings, parked cars),
the manifest fragment and anchors.json (file -> [ax, ay] ground-centre anchor pixel).
"""
import json
import os

import numpy as np

import iso_net_bldg as B
import iso_net_pal as P
import iso_net_props as PR
import isokit as ik
from iso_net_core import OUT, bayer, blank, over, render_flat, save_png, write_manifest_fragment
from iso_net_kit import render_fixed
from iso_net_power import AX as PAX, AY as PAY, CH as PCH, CW as PCW, power_tile
from iso_net_roads import mask_label, mask_name, road_tile
from iso_net_struct import H as BH, bridge_piece, overpass_piece
from iso_net_out_ground import lot_tile

ANCHORS = {}
SIDES = ["E", "N", "W", "S"]           # isokit facing k puts an east-side model on this side
CORNERS = ["SE", "NE", "NW", "SW"]


def put(rel, img, anchor):
    save_png(os.path.join(OUT, rel), img)
    ANCHORS[rel] = list(anchor)
    return rel


def item(rel, img, label, anchor=(32, 80)):
    return {"file": put(rel, img, anchor), "label": label}


def g(title, items, columns=8, scale=2, note=None):
    d = {"title": title, "columns": columns, "scale": scale, "items": items}
    if note:
        d["note"] = note
    return d


def ctx(rel, img, label, ground):
    """Save the prop-only sprite (for the engine, listed in anchors.json) and show it on its road cell in Figma."""
    put(rel, img, (32, 80))
    return item(rel.replace(".png", "-ctx.png"), on_ground(ground, img), label)


GROUND = {}


def ground(key):
    if key not in GROUND:
        from iso_net_roads import road_scene
        from iso_net_roadx import control_extra, tile
        if key == "cross-signal":
            GROUND[key] = tile(road_scene("street", 15, extra=control_extra("signal")))
        elif key in ("cross-stop", "cross-yield"):
            GROUND[key] = tile(road_scene("street", 15, extra=control_extra(key.split("-")[1])))
        elif key == "cross":
            GROUND[key] = road_tile("street", 15)
        elif key == "plaza":
            sc = ik.Scene(footprint=(1, 1))
            sc.tile(0, 0, "paving")
            GROUND[key] = render_fixed(sc, 64, 32, 32, 16)
        else:
            GROUND[key] = road_tile("street", 5 if key in ("E", "W") else 10)
    return GROUND[key]


def cell(model, facing=0, night=False, lit_only=False):
    return render_fixed(model, 64, 96, 32, 80, facing=facing, night=night, lit_only=lit_only)


def on_ground(ground, *props):
    """Compose a 64x32 ground tile and 64x96 prop canvases (anchor 32,80) into one 64x96 cell image."""
    img = np.zeros((96, 64, 4), np.float32)
    over(img, P.quantize(ground), 0, 64)
    for p in props:
        over(img, p, 0, 0)
    return img


def light_pool(side="E"):
    """Emissive ground decal under a lamp head (the '-lit' ground frame): warm dither fading out."""
    hx, hy = {"E": (0.71, 0.5), "N": (0.5, 0.29), "W": (0.29, 0.5), "S": (0.5, 0.71)}[side]
    c1 = np.array(ik.color("yellow", 9), np.float32)
    c2 = np.array(ik.color("yellow", 7), np.float32)

    def fn(u, v, px, py):
        r = np.hypot(u - hx, v - hy) / 0.34
        t = np.clip(1 - r, 0, 1)
        b = bayer(32, 64)[py, px]
        on = b < t * 0.75
        col = np.where((t > 0.55)[:, None], c1, c2)
        return col, on
    return render_flat(fn)


def bridges():
    groups = []
    for cls in ("street", "avenue", "boulevard", "highway"):
        items = []
        for ax in ("ns", "ew"):
            for pc, lab in (("end0", "ramp up"), ("span", "span"), ("pier", "pier"), ("end1", "ramp down")):
                rail = "parapet" if cls == "highway" else "rail"
                img = bridge_piece(cls, ax, pc, oneway=(cls == "highway"), rail=rail,
                                   girder=B.flat("brick", 0.0) if cls == "boulevard" else None or __import__("iso_net_struct").GIRDER)
                items.append(item(f"bridges/{cls}-{ax}-{pc}.png", img, f"{ax.upper()} {lab}", (32, BH + 16)))
        groups.append(g(f"Bridge over water: {cls} (deck 8 px, abutment ramps)", items, columns=4))
    ov = []
    for ax in ("ns", "ew"):
        for pc, lab in (("rampA0", "ramp 0-12"), ("rampB0", "ramp 12-24"), ("span", "span"), ("pier", "T-pier")):
            ov.append(item(f"bridges/overpass-{ax}-{pc}.png", overpass_piece(ax, pc), f"{ax.upper()} {lab}", (32, BH + 16)))
    groups.append(g("Elevated highway concept (deck 24 px, 2-cell ramps)", ov, columns=4,
                    note="idea for overpasses/interchanges; the engine has no elevation yet"))
    return groups


def control():
    items = []
    for st in ("red", "amber", "green"):
        for k in range(4):
            items.append(ctx(f"junction/signal-{st}-{CORNERS[k]}.png", cell(PR.traffic_signal(st), k), f"{st} {CORNERS[k]}",
                             ground("cross-signal")))
    night = []
    for st in ("red", "amber", "green"):
        night.append(item(f"junction/signal-{st}-SE-night.png", cell(PR.traffic_signal(st), 0, True), f"{st} night"))
        night.append(item(f"junction/signal-{st}-SE-lit.png", cell(PR.traffic_signal(st), 0, True, True), f"{st} lit frame"))
    signs = []
    for kind in ("stop", "yield", "oneway", "noentry"):
        for k in range(4):
            gk = {"stop": "cross-stop", "yield": "cross-yield"}.get(kind, "cross")
            signs.append(ctx(f"junction/sign-{kind}-{CORNERS[k]}.png", cell(PR.stop_sign(kind), k), f"{kind} {CORNERS[k]}", ground(gk)))
    return [g("Traffic signals: red, amber, green at each junction corner", items, columns=4,
              note="mast arm over the inbound lane; lenses face the approach, backs show on far corners"),
            g("Traffic signals at night + emissive '-lit' frames", night, columns=6),
            g("Signs: stop, yield, one-way, no entry (4 corners)", signs, columns=8)]


def lamps():
    items = []
    for k in range(4):
        items.append(ctx(f"furniture/lamp-{SIDES[k]}.png", cell(PR.street_lamp(), k), f"lamp {SIDES[k]} side", ground(SIDES[k])))
    for k in range(2):
        items.append(item(f"furniture/lamp-double-{'ns' if k == 0 else 'ew'}.png",
                          cell(PR.street_lamp(True, x=0.5), k), "median double " + ("N-S" if k == 0 else "E-W")))
    items.append(item("furniture/lamp-heritage.png", cell(PR.heritage_lamp()), "heritage lantern"))
    night = []
    for k in range(4):
        night.append(item(f"furniture/lamp-{SIDES[k]}-night.png", cell(PR.street_lamp(), k, True), f"night {SIDES[k]}"))
        night.append(item(f"furniture/lamp-{SIDES[k]}-lit.png", cell(PR.street_lamp(), k, True, True), f"lit frame {SIDES[k]}"))
        night.append(item(f"furniture/lightpool-{SIDES[k]}.png", light_pool(SIDES[k]), f"light pool {SIDES[k]}", (32, 16)))
    night.append(item("furniture/lamp-heritage-night.png", cell(PR.heritage_lamp(), 0, True), "heritage night"))
    return [g("Street lamps (day)", items, columns=7),
            g("Street lamps at night: sprite, emissive '-lit' frame, ground light pool", night, columns=6,
              note="light pools are separate emissive ground frames drawn after the night tint")]


def stops():
    items = []
    for kind, lab in (("bus", "bus stop"), ("tram", "tram stop"), ("taxi", "taxi stand")):
        for k in range(4):
            items.append(ctx(f"stops/{kind}-{SIDES[k]}.png", cell(PR.bus_shelter(kind), k), f"{lab} {SIDES[k]}", ground(SIDES[k])))
    for k in range(4):
        items.append(ctx(f"stops/metro-entrance-{k}.png", cell(PR.metro_entrance(), k), f"metro entrance f{k}", ground("plaza")))
    items.append(item("stops/bus-E-night.png", cell(PR.bus_shelter("bus"), 0, True), "bus stop night"))
    items.append(item("stops/metro-entrance-night.png", cell(PR.metro_entrance(), 0, True), "metro night"))
    return [g("Transit stops: bus shelter, tram platform, taxi stand, metro entrance", items, columns=4,
              note="E/N/W/S = sidewalk side of the road cell")]


def furniture():
    items = [item(f"furniture/{k}.png", cell(PR.furniture(k)), k) for k in
             ("bench", "bin", "hydrant", "mailbox", "bollards", "tree", "planter")]
    for k in range(2):
        items.append(item(f"furniture/sound-barrier-{SIDES[k]}.png", cell(B.sound_barrier(), k), f"sound wall {SIDES[k]}"))
        items.append(item(f"furniture/guard-rail-{SIDES[k]}.png", cell(B.guard_rail(), k), f"guard rail {SIDES[k]}"))
    for k in range(2):
        ax = "ns" if k == 0 else "ew"
        items.append(item(f"furniture/gantry-{ax}.png", cell(B.gantry(ax)), f"highway gantry {ax.upper()}"))
    return [g("Street furniture and road structures", items, columns=7)]


def addons():
    st = road_tile("street", 5)
    trees = [cell(PR.furniture("tree"), 0), cell(PR.furniture("tree"), 2)]
    lights = [cell(PR.street_lamp(), 0), cell(PR.street_lamp(), 2)]
    barrier = [cell(B.sound_barrier(), 0), cell(B.sound_barrier(), 2)]
    parking = [cell(B.kerb_cars(seed=1), 0), cell(B.kerb_cars(seed=2), 2)]
    items = [item("addons/street-trees.png", on_ground(st, *trees), "trees"),
             item("addons/street-lights.png", on_ground(st, *lights), "lights"),
             item("addons/street-barrier.png", on_ground(st, *barrier), "sound barrier"),
             item("addons/street-parking.png", on_ground(st, *parking), "parking"),
             item("addons/street-all.png", on_ground(st, trees[0], lights[1], parking[0]), "trees+lights+parking")]
    bv = road_tile("boulevard", 5)
    mt = ik.Scene(footprint=(1, 1), seed=5)
    from isokit.trees import add_tree
    add_tree(mt, 0.5, 0.3, "linden", "summer", stage=1, seed=5, z=1.0)
    add_tree(mt, 0.5, 0.8, "linden", "summer", stage=1, seed=6, z=1.0)
    med_trees = cell(mt)
    put("addons/boulevard-median-trees-ns.png", med_trees, (32, 80))
    items.append(item("addons/boulevard-median.png", on_ground(bv, med_trees), "boulevard median trees"))
    items.append(item("addons/boulevard-median-lamps.png",
                      on_ground(bv, cell(PR.street_lamp(True, x=0.5, y=0.5), 0)), "boulevard median lamps"))
    av = road_tile("avenue", 10)
    items.append(item("addons/avenue-trees-lights.png",
                      on_ground(av, cell(PR.furniture("tree"), 1), cell(PR.street_lamp(), 3)), "avenue trees+lights"))
    return [g("Road add-ons on a street (strips: trees, lights, barrier, parking)", items, columns=6)]


def rail_props():
    items = [item(f"rail/buffer-{SIDES[k]}.png", cell(B.buffer_stop(), (k + 1) % 4), f"buffer stop arm {['N', 'W', 'S', 'E'][k]}")
             for k in range(4)]
    gates = []
    for down in (True, False):
        for lamp in (0, 1):
            nm = f"rail/crossing-gate-{'down' if down else 'up'}-{lamp}.png"
            gates.append(item(nm, cell(B.crossing_gate(down, lamp)), f"gate {'down' if down else 'up'} f{lamp}"))
    strip = np.concatenate([cell(B.crossing_gate(True, 0)), cell(B.crossing_gate(True, 1))], axis=1)
    gates.append(item("rail/crossing-gate-down-strip.png", strip, "2 frames", (32, 80)))
    for st in ("red", "green"):
        gates.append(item(f"rail/signal-{st}.png", cell(B.rail_signal(st)), f"signal {st}"))
    for ax in ("ns", "ew"):
        gates.append(item(f"rail/catenary-{ax}.png", cell(B.catenary_mast(ax)), f"catenary {ax.upper()}"))
    return [g("Rail props: buffer stops, crossing gates (flashing, 2 frames), signals, catenary", items + gates, columns=8)]


def power():
    groups = []
    for kind, title in (("hv", "High-voltage pylons"), ("lv", "Wooden distribution poles")):
        items = [item(f"util/power-{kind}-{mask_name(m)}.png", power_tile(m, kind), mask_label(m), (PAX, PAY)) for m in range(16)]
        groups.append(g(f"{title}: 16 masks (wires meet at edge midpoints)", items, columns=8))
    return groups


def buildings():
    items = []
    big = dict(w=128, h=144, ax=64, ay=112)
    one = dict(w=64, h=96, ax=32, ay=80)
    for name, fn, sz in (("transformer", B.transformer, big), ("battery", B.battery, one),
                         ("sewage-outlet", B.sewage_outlet, one), ("treatment-plant", B.treatment_plant, big)):
        for night in (False, True):
            img = render_fixed(fn(), sz["w"], sz["h"], sz["ax"], sz["ay"], night=night)
            items.append(item(f"buildings/{name}{'-night' if night else ''}.png", img,
                              f"{name}{' night' if night else ''}", (sz["ax"], sz["ay"])))
    fac = [item(f"buildings/treatment-plant-f{k}.png", render_fixed(B.treatment_plant(), 128, 144, 64, 112, facing=k),
                f"facing {k}", (64, 112)) for k in range(4)]
    strip = np.concatenate([render_fixed(B.treatment_plant(), 128, 144, 64, 112, facing=k) for k in range(4)], axis=1)
    fac.append(item("buildings/treatment-plant-facings-strip.png", strip, "4 facings strip", (64, 112)))
    return [g("Network buildings: transformer 2x2, battery, sewage outlet, treatment plant 2x2 (day / night)", items,
              columns=4, scale=2),
            g("Treatment plant: 4 facings", fac, columns=5, scale=2)]


def parking_props():
    items = []
    for s in range(4):
        for occ in (0, 1, 2, 3):
            items.append(item(f"parking/lot-{['N', 'E', 'S', 'W'][s]}-cars{occ}.png",
                              on_ground(lot_tile(s), cell(B.lot_cars(s, occ, seed=s * 4 + occ))), f"{['N', 'E', 'S', 'W'][s]} {occ} cars"))
    return [g("Parking lots with parked cars (occupancy 0-3)", items, columns=8)]


def build():
    groups = []
    for fn in (bridges, control, lamps, stops, furniture, addons, rail_props, power, buildings, parking_props):
        groups += fn()
    write_manifest_fragment(os.path.join(OUT, "_frag", "20-props.json"), groups)
    with open(os.path.join(OUT, "anchors.json"), "w") as f:
        json.dump(dict(sorted(ANCHORS.items())), f, indent=0)
    return groups


if __name__ == "__main__":
    gs = build()
    print(sum(len(x["items"]) for x in gs), "prop sprites in", len(gs), "groups")
