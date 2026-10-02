"""iso_export_net.py - NET export: iso road ground tiles (sequences/networks.yaml, image `roadnet`) and the tall
road props (image `roadprops`, built by iso_export_net_props.py).

Everything is re-rendered in-process from the NET generators (iso_net_roads / iso_net_roadx / iso_net_struct) so every
mask / one-way / class / wear combination exists, not only the design-set subset. Ground tiles are 64x32 diamonds
anchored at the cell centre (32, 16). Paint that depends on game state (junction control, bus/bike lanes, arrows,
parking bays, ramps, map-edge fade) is exported as transparent overlay frames: the pixels that differ from the plain
road tile, so RoadLayer can stack them in its overlay layers.

Frame layouts: see HEADER below (copied into the sequences/networks.yaml header and EXPORT-LAYOUT.md).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import iso_net_pal as P  # noqa: E402
from iso_export_lib import EMPTY, Frame, SeqFile, pmap, preview, write_sheet  # noqa: E402
from iso_export_net_props import HEADER_PROPS, add_props  # noqa: E402
from iso_net_roadx import (MEDIAN_W, arrow_paint, control_extra, island_scene, map_edge_extra, median_tile, rot,  # noqa: E402
                           tile, transition_scene)
from iso_net_core import render_ground  # noqa: E402
from iso_net_roads import E, N, S, W, arm_coords, road_scene, road_tile  # noqa: E402

CLASSES = ["street", "gravel", "avenue", "boulevard", "highway", "alley"]
CROSS = ["street", "alley", "avenue", "boulevard"]      # classes with junction-control paint
LANE_CLASSES = ["street", "avenue", "boulevard"]
PARK_CLASSES = ["street", "gravel", "alley", "avenue"]
MEDIAN_CLASSES = ["avenue", "boulevard", "highway"]
BRIDGE_CLASSES = ["street", "alley", "avenue", "boulevard", "highway"]
TRANS_CLASSES = ["street", "gravel", "alley", "avenue", "boulevard"]
SECTIONS = [(c, ow) for c in TRANS_CLASSES for ow in (0, 1)]    # transition section index = class * 2 + one-way
WEAR_ORDER = (1, 0, 2)                                           # frame block 0 used, 1 new, 2 worn
ARROW_LANES = {"street": (-10, 10), "alley": (0,), "gravel": (0,), "avenue": (-11, 11), "boulevard": (-16, 0, 16),
               "highway": (-14.5, 0, 14.5)}
ARROW_GENERIC = (-11, 11)
BRIDGE_PIECES = ["end0", "span", "pier", "end1"]
BR_H = 48

HEADER = """Road art (image roadnet: ground tiles; image roadprops: tall props). Generated from the NET generators.
All roadnet frames are 64x32 ground tiles anchored at the cell centre (bridges: 64x80, deck 8 px up, anchor (32, 64)).
Mask bits: 1 N (-Y, up-right edge), 2 E (+X, down-right), 4 S (+Y, down-left), 8 W (-X, up-left). ow = one-way (0/1).
Class sequence names = RoadType.Sequence (street gravel avenue boulevard highway alley). Overlay frames are transparent
except where they paint (they stack on the base tile).
roadnet frame layouts
  <class>              96   base road: wear * 32 + ow * 16 + arms (wear block 0 used, 1 new, 2 worn; chosen by cell hash)
  arrows               4    generic one-way arrows N,E,S,W (road order preview)
  arrows-<class>       4    one-way arrows N,E,S,W with that class's lane positions (all six classes)
  median-<class>       16   avenue, boulevard, highway: median strip by sideBlock mask (frame 0 empty)
  lanes-<class>        96   street, avenue, boulevard: (ow * 3 + combo - 1) * 16 + arms, combo bit0 bus lane, bit1 bike lane
  strips-<class>       240  street, gravel, alley, avenue: ground part of the add-ons, (combo - 1) * 16 + (~arms & 15),
                            combo bit0 trees bit1 barrier bit2 lights bit3 parking; only parking has ground art (bay marks
                            on lined free sides), other combos are empty (trees/lamps/barriers are roadprops)
  junction-<class>     128  street, alley, avenue, boulevard: control paint on junctions (3+ arms):
                            (ow * 4 + kind) * 16 + arms, kind 0 stop 1 signal 2 yield 3 roundabout (mini, two-way only)
  ring-<class>         256  same classes, one-way ring cells of a roundabout: entries * 16 + arms (yield teeth on the
                            entry arms; entries = arms that are not ring neighbours)
  control              14   0-3 highway ramp chevrons toward the street end N,E,S,W; 4 island 1x1; 5-13 island 3x3 by
                            (gy * 3 + gx) (gx, gy = column, row of the cell inside the 3x3 island, 0 = N/W)
  edge                 4    map-edge highway fade + in/out arrows, edge toward N,E,S,W (overlay on the highway tile)
  transition           200  straight cell between two classes: ((sa * 10 + sb) * 2 + axis); section s = classIndex * 2 + ow
                            with classIndex street 0 gravel 1 alley 2 avenue 3 boulevard 4; sa at the N (axis 0) / W
                            (axis 1) edge, sb at the S / E edge
  bridge-<class>       16   street, alley, avenue, boulevard, highway deck pieces: ((axis * 2 + ow) * 4 + piece),
                            axis 0 = N-S, 1 = E-W; piece 0 end0 (land at the N / W edge) 1 span 2 pier 3 end1 (land at S / E)
"""


# ---------------------------------------------------------------------------------------------- helpers
def gframe(img):
    return Frame(P.quantize(img), 32, 16)


def overlay(full, base):
    """Pixels of `full` that differ from `base` (both float RGBA tiles), as an overlay Frame."""
    f, b = P.quantize(full), P.quantize(base)
    fa, ba = f[..., 3] >= 128, b[..., 3] >= 128
    ch = (fa & ~ba) | (fa & ba & (f[..., :3] != b[..., :3]).any(-1))
    out = np.zeros_like(f)
    out[ch] = f[ch]
    if not ch.any():
        return EMPTY
    return Frame(out, 32, 16)


def arms_of(mask):
    return [a for a in (N, E, S, W) if mask & a]


def stop_extra(thick, oneway):
    def extra(c, col, alpha, raised):
        U, V, a, road, mask = c["U"], c["V"], c["a"], c["road"], c["mask"]
        paint = np.zeros(U.shape, bool)
        for arm in arms_of(mask):
            Sx, Tx = arm_coords(U, V, arm)
            lane = (np.abs(Tx) < a - 1) if oneway else ((Tx < -1) & (Tx > -a + 1))
            paint |= lane & (Sx >= a + 2) & (Sx < a + 2 + thick)
        return np.where((paint & road)[:, None], P.WHITE, col), alpha, raised
    return extra


def parking_extra(free, ap):
    def extra(c, col, alpha, raised):
        U, V, road = c["U"], c["V"], c["road"]
        paint = np.zeros(U.shape, bool)
        for bit in arms_of(free):
            T, al = {N: (32 - V, U), E: (U - 32, V), S: (V - 32, U), W: (32 - U, V)}[bit]
            band = (T >= ap - 8) & (T < ap)
            line = band & (T < ap - 6)
            sep = band & ((np.abs(al - 32) < 1.0) | (al < 1.0) | (al >= 63.0))
            paint |= line | sep
        return np.where((paint & road)[:, None], P.WHITE, col), alpha, raised
    return extra


def lined(free):
    """Free sides of a cell (mask of the sides without an arm) that have a road running along them."""
    arms = ~free & 15
    out = 0
    for side, para in ((N, E | W), (S, E | W), (E, N | S), (W, N | S)):
        if free & side and (arms & para) == para:
            out |= side
    return out


def arrow_scene_lanes(direction, lanes):
    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        hit = arrow_paint(U, V, direction, lanes)
        col = np.zeros((U.shape[0], 3), np.float32) + P.WHITE
        return col, hit, np.zeros(U.shape, bool), None, None
    return scene


# ---------------------------------------------------------------------------------------------- builders
def b_road(cls):
    fr = []
    for w in WEAR_ORDER:
        for ow in (0, 1):
            for m in range(16):
                fr.append(gframe(road_tile(cls, m, bool(ow), wear=w, stop_line=False)))
    return fr


def b_junction(cls):
    fr = []
    for ow in (0, 1):
        for kind in range(4):
            for arms in range(16):
                if bin(arms).count("1") < 3 or (kind == 3 and ow):
                    fr.append(EMPTY)
                    continue
                if kind == 0:
                    ex = stop_extra(3, ow)
                elif kind == 1:
                    ex = stop_extra(2, ow)
                elif kind == 2:
                    ex = control_extra("yield")
                else:
                    ex = control_extra("mini")
                base = road_tile(cls, arms, bool(ow), stop_line=False)
                fr.append(overlay(road_tile(cls, arms, bool(ow), stop_line=False, extra=ex), base))
    return fr


def b_ring(cls):
    fr = []
    for entries in range(16):
        for arms in range(16):
            if not entries or entries & ~arms or entries == arms:
                fr.append(EMPTY)
                continue
            base = road_tile(cls, arms, True, stop_line=False)
            full = road_tile(cls, arms, True, stop_line=False, extra=control_extra("yield", arms=entries))
            fr.append(overlay(full, base))
    return fr


def b_median(cls):
    return [EMPTY if sb == 0 else gframe(median_tile(cls, sb)) for sb in range(16)]


def b_arrows(cls):
    lanes = ARROW_GENERIC if cls is None else ARROW_LANES[cls]
    return [gframe(tile(arrow_scene_lanes(d, lanes))) for d in range(4)]


def b_lanes(cls):
    fr = []
    for ow in (0, 1):
        for combo in (1, 2, 3):
            for arms in range(16):
                base = road_tile(cls, arms, bool(ow), stop_line=False)
                full = road_tile(cls, arms, bool(ow), stop_line=False, bus=bool(combo & 1), bike=bool(combo & 2))
                fr.append(overlay(full, base))
    return fr


def b_strips(cls):
    from iso_net_roads import section
    ap = min(section(cls, False)["a"], section(cls, True)["a"])
    fr = []
    for combo in range(1, 16):
        for free in range(16):
            lf = lined(free)
            if not combo & 8 or not lf:
                fr.append(EMPTY)
                continue
            arms = ~free & 15
            base = road_tile(cls, arms, False, stop_line=False)
            fr.append(overlay(road_tile(cls, arms, False, stop_line=False, extra=parking_extra(lf, ap)), base))
    return fr


def b_bridge(cls):
    import iso_net_bldg as B
    from iso_net_struct import GIRDER, bridge_piece
    girder = B.flat("brick", 0.0) if cls == "boulevard" else GIRDER
    fr = []
    for axis in ("ns", "ew"):
        for ow in (0, 1):
            for piece in BRIDGE_PIECES:
                img = bridge_piece(cls, axis, piece, oneway=bool(ow), rail="parapet" if cls == "highway" else "rail",
                                   girder=girder)
                fr.append(Frame(img, 32, BR_H + 16))
    return fr


def b_transition(_):
    fr = []
    for sa, (ca, oa) in enumerate(SECTIONS):
        for sb, (cb, ob) in enumerate(SECTIONS):
            for axis in (0, 1):
                if ca == cb:
                    fr.append(EMPTY)
                    continue
                sc = transition_scene(ca, cb, bool(oa), bool(ob))
                if axis == 1:
                    sc = rot(sc, 3)
                fr.append(gframe(tile(sc)))
    return fr


def b_edge(_):
    fr = []
    for d in range(4):
        m = 5 if d % 2 == 0 else 10
        base = road_tile("highway", m, False, stop_line=False)
        fr.append(overlay(road_tile("highway", m, False, stop_line=False, extra=map_edge_extra(d)), base))
    return fr


def b_control(_):
    fr = []
    for d in range(4):
        base = rot(road_scene("highway", 5, True, stop_line=False), d)
        full = rot(road_scene("highway", 5, True, stop_line=False, extra=control_extra("ramp")), d)
        fr.append(overlay(render_ground(full), render_ground(base)))
    fr.append(gframe(tile(island_scene())))
    for gy in range(3):
        for gx in range(3):
            fr.append(gframe(tile(island_scene(gx, gy, 3))))
    return fr


BUILDERS = {"road": b_road, "junction": b_junction, "ring": b_ring, "median": b_median, "arrows": b_arrows,
            "lanes": b_lanes, "strips": b_strips, "bridge": b_bridge, "transition": b_transition, "edge": b_edge,
            "control": b_control}


def run_job(job):
    kind, cls = job
    return job, BUILDERS[kind](cls)


def jobs():
    j = [("road", c) for c in CLASSES]
    j += [("junction", c) for c in CROSS] + [("ring", c) for c in CROSS]
    j += [("median", c) for c in MEDIAN_CLASSES]
    j += [("arrows", None)] + [("arrows", c) for c in CLASSES]
    j += [("lanes", c) for c in LANE_CLASSES]
    j += [("strips", c) for c in PARK_CLASSES]
    j += [("bridge", c) for c in BRIDGE_CLASSES]
    j += [("transition", None), ("edge", None), ("control", None)]
    return j


def seq_name(kind, cls):
    if kind == "road":
        return cls
    if kind == "arrows":
        return "arrows" if cls is None else "arrows-" + cls
    if cls is None:
        return kind
    return "%s-%s" % (kind, cls)


def export():
    results = pmap(run_job, jobs())
    sf = SeqFile("networks.yaml", HEADER + HEADER_PROPS)
    sf.image("roadnet", comment="Ground tiles and overlays (TerrainSpriteLayer, one frame per cell).")
    total = 0
    sheets = {}
    for (kind, cls), frames in results:
        name = seq_name(kind, cls)
        rel = "net/%s.png" % (("road-" + name) if kind == "road" else name)
        n = write_sheet(rel, frames)
        sheets[name] = frames
        total += n
        sf.seq("roadnet", name, "iso/" + rel, length=n)
    pn = add_props(sf)
    sf.write()
    if os.environ.get("ISO_EXPORT_PREVIEW"):
        for name in ("street", "avenue", "highway"):
            preview("/tmp/net-%s.png" % name, sheets[name][:32], cols=16)
    return "%d roadnet frames in %d sequences, %d roadprops frames" % (total, len(results), pn)


if __name__ == "__main__":
    print(export())
