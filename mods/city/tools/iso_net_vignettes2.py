"""
iso_net_vignettes2.py - the NET vignettes themselves (6x6 blocks) built with iso_net_vignettes.Block.

    python3 mods/city/tools/iso_net_vignettes2.py
"""
import os

import numpy as np

import iso_net_bldg as BL
import iso_net_props as PR
import isokit as ik
from isokit.trees import add_tree
from iso_net_core import OUT, load_png, over, save_png, write_manifest_fragment
from iso_net_kit import render_fixed
from iso_net_out_ground import lot_tile
from iso_net_power import power_tile
from iso_net_rail import crossing_tile, rail_tile
from iso_net_roadx import control_extra, island_scene, tile
from iso_net_vignettes import Block

SIDE = ["E", "N", "W", "S"]


def png(rel):
    return load_png(os.path.join(OUT, rel)).astype(np.float32)


def tree_scene(x=0.5, y=0.5, big=1.0, season="summer", species="oak"):
    """KIT tree (isokit.trees) so NET vignettes match the nature workstream."""
    s = ik.Scene(footprint=(1, 1), seed=int(x * 100 + y * 10))
    add_tree(s, x, y, species, season, stage=2 if big >= 1 else 1, seed=int(x * 10 + y * 3))
    return s


def lamp(b, x, y, f):
    b.prop(x, y, PR.street_lamp(), f)
    b.pools.append((x, y, SIDE[f]))


def downtown(night=False, season='summer'):
    b = Block(night=night, season=season)
    b.road([(x, 2) for x in range(6)], "avenue")
    b.roads[(0, 2)]["edge"] = 8
    b.roads[(5, 2)]["edge"] = 2
    b.road([(2, y) for y in (0, 1, 3, 5)], "street")
    b.roads[(2, 0)]["edge"] = 1
    b.roads[(2, 5)]["edge"] = 4
    b.road([(2, 2)], "avenue", extra=control_extra("signal"))
    b.road([(x, 4) for x in range(2, 6)], "street")
    b.roads[(5, 4)]["edge"] = 2
    b.roads[(2, 4)]["extra"] = control_extra("stop")
    b.lay()
    for (x, y) in ((4, 3), (5, 3)):
        b.put_ground(x, y, lot_tile(2))
    for k in range(4):
        b.prop(2, 2, PR.traffic_signal(["red", "green", "red", "green"][k]), k)
    for k in (0, 2):
        b.prop(2, 4, PR.stop_sign("stop"), k)
    for (x, y, f) in ((0, 2, 1), (4, 2, 3), (2, 0, 0), (2, 5, 2), (5, 4, 3), (3, 4, 1)):
        lamp(b, x, y, f)
    b.prop(3, 2, PR.bus_shelter("bus"), 1)
    b.prop(4, 4, PR.furniture("tree", season), 3)
    b.prop(2, 3, PR.furniture("hydrant"), 0)
    b.prop(4, 3, BL.lot_cars(2, 2, seed=3))
    b.prop(5, 3, BL.lot_cars(2, 3, seed=4))
    b.building(0, 0, 2, 2, 3, 1)
    b.building(3, 0, 3, 2, 4, 2)
    b.building(0, 3, 2, 3, 2, 3)
    b.building(3, 3, 1, 1, 2, 4)
    b.building(3, 5, 3, 1, 2, 5)
    return b


def river(night=False, season='summer'):
    b = Block(night=night, season=season)
    b.water([(x, y) for x in range(6) for y in (2, 3, 4)])
    b.road([(2, 0), (2, 1)], "street")
    b.roads[(2, 0)]["edge"] = 1
    b.road([(2, 2)], "street", bridge="end0")
    b.road([(2, 3)], "street", bridge="pier")
    b.road([(2, 4)], "street", bridge="end1")
    b.road([(x, 5) for x in range(6)], "street")
    b.roads[(0, 5)]["edge"] = 8
    b.roads[(5, 5)]["edge"] = 2
    b.lay()
    b.put_obj(4, 1, render_fixed(BL.sewage_outlet(), 64, 96, 32, 80, night=night, season=season), 32, 80)
    for (x, y) in ((0, 1), (1, 1), (3, 1)):
        b.prop(x, y, tree_scene(0.45, 0.4, season=season))
    b.prop(5, 1, tree_scene(0.5, 0.5, 1.2, season=season, species='linden'))
    for (x, y, f) in ((2, 1, 0), (2, 0, 2), (4, 5, 1), (0, 5, 1)):
        lamp(b, x, y, f)
    b.prop(2, 5, PR.stop_sign("yield"), 2)
    b.building(0, 0, 2, 1, 3, 6)
    b.building(3, 0, 1, 1, 2, 7)
    b.building(4, 0, 2, 1, 4, 8)
    return b


def roundabout(night=False):
    b = Block(night=night)
    ring = {(1, 1): 2, (2, 1): 3, (3, 1): 3, (1, 2): 2, (1, 3): 1, (2, 3): 1, (3, 3): 0, (3, 2): 0}
    for c, d in ring.items():
        b.road([c], "street", ow=d)
    b.road([(2, 0)], "street", edge=1)
    b.road([(2, 4), (2, 5)], "street")
    b.roads[(2, 5)]["edge"] = 4
    b.road([(0, 2)], "street", edge=8)
    b.road([(4, 2), (5, 2)], "boulevard")
    b.roads[(5, 2)]["edge"] = 2
    b.lay()
    b.put_ground(2, 2, tile(island_scene()))
    b.prop(2, 2, tree_scene(0.5, 0.5, 1.3, species='maple'))
    for (x, y, f) in ((2, 5, 0), (0, 2, 3), (5, 2, 1)):
        lamp(b, x, y, f)
    b.prop(4, 2, PR.stop_sign("yield"), 2)
    b.prop(2, 4, PR.stop_sign("yield"), 0)
    b.building(0, 0, 1, 1, 3, 9)
    b.building(3, 0, 3, 1, 3, 10)
    b.building(4, 3, 2, 3, 3, 11)
    b.building(0, 4, 2, 2, 2, 12)
    return b


def corridor(night=False):
    """Paired highway with gantry and sound wall, rail with a level crossing, power line and a transformer."""
    b = Block(night=night)
    b.road([(x, 0) for x in range(6)], "highway", ow=3, sb=4)
    b.road([(x, 1) for x in range(6)], "highway", ow=1, sb=1)
    for y in (0, 1):
        b.roads[(0, y)]["edge"] = 8
        b.roads[(5, y)]["edge"] = 2
    b.road([(2, 2), (2, 4), (2, 5)], "street")
    b.road([(2, 3)], "street", draw=False)
    b.roads[(2, 2)]["sb"] = 1
    b.roads[(2, 5)]["edge"] = 4
    b.lay()
    b.put_ground(2, 3, crossing_tile(False))
    for x in range(6):
        if x != 2:
            b.put_ground(x, 3, rail_tile(2 | 8))
    for x in range(6):
        if x != 2:
            b.prop(x, 1, BL.sound_barrier(), 3)
    b.prop(3, 1, BL.gantry("ew"))
    b.prop(2, 3, BL.crossing_gate(True, 0))
    b.prop(4, 3, BL.rail_signal("red"), 1)
    for x in (3, 4, 5):
        b.put_obj(x, 5, power_tile(2 | 8), 32, 96)
    tr = render_fixed(BL.transformer(), 128, 144, 64, 112, night=night)
    sx, sy = b.centre(0.5, 4.5)
    b.objs.append(((6, 1), sx - 64, sy - 112, tr))
    lamp(b, 2, 4, 0)
    b.building(4, 2, 2, 1, 2, 13)
    b.building(0, 2, 2, 1, 3, 14)
    return b


def tools_view():
    """Zoning and tool overlays in context: zoned lots along a street, drag-path preview, bulldoze, footprint."""
    b = Block()
    b.road([(x, 1) for x in range(6)], "street")
    b.roads[(0, 1)]["edge"] = 8
    b.roads[(5, 1)]["edge"] = 2
    b.road([(3, y) for y in (2, 3, 4, 5)], "street")
    b.roads[(3, 5)]["edge"] = 4
    b.lay()
    zones = {(0, 0): "res-low", (1, 0): "res-low", (2, 0): "res-low", (4, 0): "com-low", (5, 0): "com-low",
             (0, 2): "res-medium", (1, 2): "res-medium", (2, 2): "res-medium", (2, 3): "res-medium",
             (4, 2): "office", (5, 2): "office", (4, 3): "industrial", (5, 3): "industrial"}
    for c, z in zones.items():
        b.put_ground(c[0], c[1], png(f"zones/{z}.png"))
    b.put_ground(3, 0, png("zones/res-low-idle.png"))
    for (x, y) in ((0, 4), (1, 4), (2, 4)):
        b.put_ground(x, y, png("markers/path-e.png"))
    b.put_ground(0, 5, png("markers/invalid.png"))
    b.put_ground(4, 5, png("markers/bulldoze.png"))
    b.put_ground(5, 5, png("markers/bulldoze.png"))
    b.building(4, 5, 2, 1, 2, 15)
    b.building(0, 0, 1, 1, 2, 16)
    return b


def build():
    items = []
    for name, fn, lab in (("downtown", downtown, "Downtown: avenue x street signals, stop, lamps, bus stop, parking"),
                          ("river", river, "River: street bridge with pier, sewage outlet, yield"),
                          ("roundabout", roundabout, "Roundabout (3x3 ring + island), boulevard arm"),
                          ("corridor", corridor, "Paired highway, gantry, sound wall, rail crossing, power, transformer")):
        for night in (False, True):
            out = fn(night).render()
            rel = f"vignettes/{name}{'-night' if night else ''}.png"
            save_png(os.path.join(OUT, rel), out)
            items.append({"file": rel, "label": lab + (" (night)" if night else "")})
    for name, fn in (("downtown", downtown), ("river", river)):
        out = fn(False, "winter").render()
        rel = f"vignettes/{name}-winter.png"
        save_png(os.path.join(OUT, rel), out)
        items.append({"file": rel, "label": f"{name} (winter: snowy sidewalks and verges, cleared asphalt)"})
    tv = tools_view()
    out = tv.render()
    fp = png("markers/footprint-2x2-ok.png")
    sx, sy = tv.centre(4.5, 3.5)
    over(out, fp, int(sx - fp.shape[1] // 2), int(sy - fp.shape[0] // 2))
    save_png(os.path.join(OUT, "vignettes/tools.png"), out)
    items.append({"file": "vignettes/tools.png", "label": "Zoning, drag path, bulldoze and footprint overlays in context"})
    groups = [{"title": "Vignettes: 6x6 blocks in context (grey boxes = placeholder buildings)", "columns": 2, "scale": 1,
               "note": "day and night; night = ground night tint + emissive lamps and light pools", "items": items}]
    write_manifest_fragment(os.path.join(OUT, "_frag", "05-vignettes.json"), groups)
    return groups


if __name__ == "__main__":
    build()
