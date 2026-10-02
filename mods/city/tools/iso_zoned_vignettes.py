"""iso_zoned_vignettes.py - street-context vignettes of ZONED buildings on NET's street tiles.

Each vignette: a cell map with roads (NET tiles from design/iso/net/roads) and lots placed with the
facing that points their front at the road (north side of a street: facing 0 = front +Y; south side:
facing 2 = front -Y).
"""
import os

import isokit as ik
import iso_zoned_core as C

HERE = os.path.dirname(os.path.abspath(__file__))
NET = os.path.join(os.path.dirname(HERE), "design", "iso", "net", "roads")

VIGNETTES = [("suburban-street", "Suburban street (res-low, res-row, corner shops)"),
             ("downtown-block", "Downtown block (towers, offices, mixed use, dept store)"),
             ("industrial-strip", "Industrial strip (factories, warehouses)")]

# name -> (map w, h, roads [(kind, axis, fixed coordinate)], lots [(prefix, fp, level, var, cx, cy, facing)])
LAYOUTS = {
    "suburban-street": (12, 8, [("street", "x", 4), ("street", "y", 6)], [
        ("res-low", "1x1", 1, 0, 0, 3, 0), ("res-low", "2x1", 3, 2, 1, 3, 0), ("res-low", "1x1", 4, 1, 3, 3, 0),
        ("res-low", "2x2", 5, 3, 4, 2, 0), ("res-low", "1x1", 2, 3, 0, 2, 0), ("res-low", "1x2", 3, 0, 0, 0, 0),
        ("res-row", "3x1", 3, 2, 7, 3, 0), ("res-row", "2x1", 4, 2, 10, 3, 0), ("res-row", "3x1", 2, 0, 7, 2, 0),
        ("res-low", "1x1", 3, 1, 0, 5, 2), ("res-low", "1x1", 1, 2, 1, 5, 2), ("res-low", "2x2", 4, 0, 2, 5, 2),
        ("res-low", "1x1", 5, 2, 4, 5, 2), ("res-low", "1x1", 2, 3, 5, 5, 2), ("com-low", "1x1", 3, 2, 7, 5, 2),
        ("com-low", "2x1", 4, 1, 8, 5, 2), ("res-row", "2x1", 2, 3, 10, 5, 2), ("res-low", "1x1", 3, 0, 4, 6, 2),
    ]),
    "downtown-block": (10, 8, [("avenue", "x", 4), ("street", "y", 4)], [
        ("res-high", "2x2", 4, 1, 0, 2, 0), ("off-high", "2x2", 5, 0, 2, 2, 0), ("com-high", "3x3", 4, 2, 5, 1, 0),
        ("res-mixed", "2x2", 3, 2, 8, 2, 0), ("res-mixed", "2x2", 4, 0, 0, 5, 2), ("off-high", "2x2", 3, 3, 2, 5, 2),
        ("res-high", "3x3", 5, 2, 5, 5, 2), ("com-high", "2x2", 3, 0, 8, 5, 2), ("off", "2x2", 4, 2, 0, 0, 0),
        ("res-med", "2x2", 3, 3, 2, 0, 0),
    ]),
    "industrial-strip": (12, 7, [("street", "x", 3)], [
        ("ind", "3x2", 2, 1, 0, 1, 0), ("warehouse", "4x3", 3, 0, 3, 0, 0), ("ind", "2x2", 4, 3, 7, 1, 0),
        ("ind", "3x3", 5, 0, 9, 0, 0), ("warehouse", "3x2", 1, 2, 0, 4, 2), ("ind", "1x1", 1, 0, 3, 4, 2),
        ("ind", "2x3", 3, 2, 4, 4, 2), ("warehouse", "3x3", 5, 1, 6, 4, 2), ("ind", "3x2", 3, 3, 9, 4, 2),
    ]),
}


def _road_tile(kind, mask):
    img = ik.read_png(os.path.join(NET, kind, "%s-%s.png" % (kind, mask)))
    return ik.Sprite(img, 32, 16, None, (1, 1))


def build(name, night=False, season="summer"):
    w, h, roads, lots = LAYOUTS[name]
    comp = ik.Compositor(w, h, top_margin=300)
    road = {}
    for kind, axis, k in roads:
        cells = [(x, k) for x in range(w)] if axis == "x" else [(k, y) for y in range(h)]
        for c in cells:
            road[c] = kind if c not in road or kind == "avenue" else road[c]
    for (x, y), kind in road.items():
        mask = "".join("1" if b else "0" for b in (
            (x, y - 1) in road or (y == 0 and _on_axis(roads, "y", x)),
            (x + 1, y) in road or (x == w - 1 and _on_axis(roads, "x", y)),
            (x, y + 1) in road or (y == h - 1 and _on_axis(roads, "y", x)),
            (x - 1, y) in road or (x == 0 and _on_axis(roads, "x", y))))
        comp.ground(_road_tile(kind, mask), x, y)
    for x in range(w):
        for y in range(h):
            if (x, y) not in road:
                comp.ground(ik.flat_tile("grass", origin=(x, y)), x, y)
    for prefix, fp, L, v, cx, cy, facing in lots:
        spr = C.render(C.build(prefix, fp, L, v, season=season), facing=facing, night=night, season=season)[0]
        comp.place(spr, cx, cy)
    img = comp.render(bg=(0, 0, 0, 0))
    a = img[..., 3] > 0
    rows, cols = a.any(1).nonzero()[0], a.any(0).nonzero()[0]
    return img[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]


def _on_axis(roads, axis, k):
    return any(a == axis and kk == k for _, a, kk in roads)
