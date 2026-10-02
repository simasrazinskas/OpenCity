"""iso_export_net_props.py - NET road props for the exporter: image `roadprops` (tall, depth-sorted sprites).

Every frame is the NET 64x96 prop canvas (design anchor = cell centre (32, 80)) re-anchored at the prop's own ground
point: the sprite anchor is the foot of the pole / tree / wall, and the frame layout below says which world offset from
the cell centre that foot sits at (RoadLayer.GetProps returns exactly these offsets). Offsets are multiples of 1/16 cell
so the re-anchoring is a whole number of pixels: a side prop stands 448 world units (7/16 cell) from the cell centre
towards its side, a corner prop 384 (3/8 cell) on both axes. Pixels never move: only the sort point changes.

Side index (everywhere): 0 N (-Y, up-right edge), 1 E (+X), 2 S (+Y), 3 W (-X). `-lit` companions: same frames,
emissive pixels only (draw at ZOffset + 1 at night, not ambient-tinted).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import iso_net_bldg as B  # noqa: E402
import iso_net_props as PR  # noqa: E402
import isokit as ik  # noqa: E402
from isokit.trees import add_tree  # noqa: E402
from iso_export_lib import Frame, pmap, write_sheet  # noqa: E402
from iso_net_kit import flat, render_fixed  # noqa: E402
from iso_net_out_props import light_pool  # noqa: E402

N, E, S, W = 0, 1, 2, 3
SIDE_K = {N: 1, E: 0, S: 3, W: 2}                       # isokit facing that puts an east-side model on this side
SIDE_SHIFT = {N: (14, -7), E: (14, 7), S: (-14, 7), W: (-14, -7)}     # px from the cell centre (448 world units)
ARM_K = {N: 2, E: 1, S: 0, W: 3}                        # signal/sign facing: the pole stands at the right-hand corner of its arm
ARM_SHIFT = {N: (0, -12), E: (24, 0), S: (0, 12), W: (-24, 0)}        # px (384, 384 world units)
SEASONS = [("", "summer"), ("-spring", "spring"), ("-autumn", "autumn"), ("-winter", "winter")]
TREE_VARIANTS = [("linden", 3), ("maple", 7)]

HEADER_PROPS = """roadprops frame layouts (side/arm index: 0 N, 1 E, 2 S, 3 W; all frames re-anchored at the prop's foot)
  Side props stand 448 world units from the cell centre towards the side (offset (0,-448) N, (448,0) E, (0,448) S,
  (-448,0) W) on the sidewalk of a free side that has a road running along it. Corner props (signals, signs) stand at
  the right-hand corner of the arm they serve: offset (-384,-384) arm N, (384,-384) E, (384,384) S, (-384,384) W.
  lamp / lamp-lit         4   street lamp on side i (-lit: emissive head only)
  lamp-pool               4   emissive ground light pool under the lamp on side i (anchor = cell centre, night only)
  lamp-heritage(-lit)     4   old-town lantern on side i (alleys)
  lamp-double(-lit)       2   double lamp on a median, 0 along N-S, 1 along E-W (anchor = cell centre)
  tree[-spring|-autumn|-winter]        8   street tree on side i: variant * 4 + i (variant 0 linden, 1 maple)
  median-tree[-season]    4   tree in the median strip along the edge of side i (paired carriageways; offset as side props)
  median-tree-c[-season]  2   two trees in a two-way boulevard's centre median, 0 N-S, 1 E-W (anchor = cell centre)
  signal-red|amber|green  4   traffic signal for the traffic arriving from arm i (corner offset); -lit variants too
  sign-stop / sign-yield  4   sign for the traffic arriving from arm i (corner offset)
  barrier                 4   sound barrier wall along side i (offset as side props)
  guardrail               4   highway guard rail along side i (offset as side props)
  gantry                  2   highway sign gantry across the carriageway, 0 over a N-S road, 1 over E-W (anchor = cell centre)
"""


def prop(model, k, shift, night=False, lit=False, season="summer"):
    img = render_fixed(model, 64, 96, 32, 80, facing=k, night=night, lit_only=lit, season=season)
    return Frame(img, 32 + shift[0], 80 + shift[1])


def sides(model, night=False, lit=False, season="summer"):
    return [prop(model, SIDE_K[i], SIDE_SHIFT[i], night, lit, season) for i in range(4)]


def arms(model, night=False, lit=False):
    return [prop(model, ARM_K[i], ARM_SHIFT[i], night, lit) for i in range(4)]


def centre(model, ks, night=False, lit=False, season="summer"):
    return [Frame(render_fixed(model, 64, 96, 32, 80, facing=k, night=night, lit_only=lit, season=season), 32, 80)
            for k in ks]


def tree_model(season, species, seed, pos, grate=True):
    s = ik.Scene(footprint=(1, 1), seed=17)
    x, y = pos
    if grate:
        s.box(x - 0.05, y - 0.05, PR.KERB_Z, 0.1, 0.1, 0.4, flat("wood", -1.0))
    add_tree(s, x, y, species, season, stage=1, seed=seed, z=PR.KERB_Z)
    return s


def tree_sides(season, species, seed, d, grate=True):
    """Trees are built at the side position with facing 0 (isokit's rotation drops the trunk of bare winter trees)."""
    pos = {N: (0.5, 1 - d), E: (d, 0.5), S: (0.5, d), W: (1 - d, 0.5)}
    return [prop(tree_model(season, species, seed, pos[i], grate), 0, SIDE_SHIFT[i], season=season) for i in range(4)]


def centre_trees(season, axis):
    s = ik.Scene(footprint=(1, 1), seed=5)
    for t, seed in ((0.3, 5), (0.8, 6)):
        add_tree(s, 0.5 if axis == 0 else t, t if axis == 0 else 0.5, "linden", season, stage=1, seed=seed, z=1.0)
    return Frame(render_fixed(s, 64, 96, 32, 80, season=season), 32, 80)


def pools():
    side = {N: "N", E: "E", S: "S", W: "W"}
    return [Frame(light_pool(side[i]), 32, 16) for i in range(4)]


def build(name):
    if name == "lamp":
        return sides(PR.street_lamp())
    if name == "lamp-lit":
        return sides(PR.street_lamp(), True, True)
    if name == "lamp-pool":
        return pools()
    if name == "lamp-heritage":
        return sides(PR.heritage_lamp())
    if name == "lamp-heritage-lit":
        return sides(PR.heritage_lamp(), True, True)
    if name == "lamp-double":
        return centre(PR.street_lamp(True, x=0.5), (0, 1))
    if name == "lamp-double-lit":
        return centre(PR.street_lamp(True, x=0.5), (0, 1), True, True)
    for suffix, season in SEASONS:
        if name == "tree" + suffix:
            fr = []
            for species, seed in TREE_VARIANTS:
                fr += tree_sides(season, species, seed, 0.9)
            return fr
        if name == "median-tree" + suffix:
            return tree_sides(season, "linden", 9, 0.9375, grate=False)
        if name == "median-tree-c" + suffix:
            return [centre_trees(season, 0), centre_trees(season, 1)]
    for st in ("red", "amber", "green"):
        if name == "signal-" + st:
            return arms(PR.traffic_signal(st))
        if name == "signal-%s-lit" % st:
            return arms(PR.traffic_signal(st), True, True)
    if name == "sign-stop":
        return arms(PR.stop_sign("stop"))
    if name == "sign-yield":
        return arms(PR.stop_sign("yield"))
    if name == "barrier":
        return sides(B.sound_barrier())
    if name == "guardrail":
        return sides(B.guard_rail())
    if name == "gantry":
        return centre(B.gantry("ns"), (0,)) + centre(B.gantry("ew"), (0,))
    raise KeyError(name)


def names():
    n = ["lamp", "lamp-lit", "lamp-pool", "lamp-heritage", "lamp-heritage-lit", "lamp-double", "lamp-double-lit"]
    n += ["tree" + s for s, _ in SEASONS] + ["median-tree" + s for s, _ in SEASONS] + ["median-tree-c" + s for s, _ in SEASONS]
    for st in ("red", "amber", "green"):
        n += ["signal-" + st, "signal-%s-lit" % st]
    return n + ["sign-stop", "sign-yield", "barrier", "guardrail", "gantry"]


def run(name):
    return name, build(name)


def add_props(sf):
    """Render every prop sequence, write the sheets and register them in `sf` (image roadprops)."""
    sf.image("roadprops", comment="Tall road props, one depth-sorted sprite each (RoadLayer.GetProps). Layout: header.")
    total = 0
    for name, frames in pmap(run, names()):
        rel = "net/props-%s.png" % name
        n = write_sheet(rel, frames)
        sf.seq("roadprops", name, "iso/" + rel, length=n)
        total += n
    return total
