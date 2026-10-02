"""iso_terrain_ids.py - the OpenCity terrain template-id scheme (moved out of tools/genterrain.py).

Pure python (stdlib only). Used by genmap.py (map generation + previews) and iso_export_terrain.py (tileset,
terrain.png, water animation). The values are identical to genterrain.py (checked by iso_export_terrain.py while
genterrain still exists), so existing maps stay valid.

Template ids
  1..6     grass variants (1 = plain)                         Clear
  10,11,18,19  meadow (flowers)                               Clear
  12..14,20..22 rough / stony grass, shrubs                   Rough
  15,16    dirt                                               Rough
  17       sand                                               Clear
  100..103 shallow water, 104,105 mid, 106,107 deep           Water
  LAND_BASE  + SHORE_INDEX[(mask, corners)]  land "beach" tiles (land that meets water)      Clear (200..245)
  WATER_BASE + SHORE_INDEX[(mask, corners)]  water "shore" tiles (beach slope in the water)  Water (400..445)
Mask bits (edge neighbours of the OTHER kind: water for beach tiles, land for water tiles):
  1=N (-Y, up-right diamond edge)  2=E (+X, down-right)  4=S (+Y, down-left)  8=W (-X, up-left)
Corner bits (diagonal-only neighbours of the other kind): 1=NE 2=SE 4=SW 8=NW

API
  MOD                      absolute path of mods/city
  hash01(x, y, seed)       deterministic hash in [0, 1]
  tone(rgba, f)            scale an RGB colour (used by the map previews)
  CORNER_SIDES, SIDE_BITS  corner bit -> the two side bits that make it "covered"
  SHORE_COMBOS / SHORE_INDEX / LAND_BASE / WATER_BASE   the 46 blob (mask, corners) pairs and their id offsets
  GRASS_IDS, MEADOW_IDS, ROUGH_IDS, DIRT_IDS, SAND_ID, WATER_IDS (shallow, mid, deep)
  templates()              [(template id, terrain type)] in frame order (frame i of terrain.png = entry i)
  water_templates()        template ids that animate, in water-sheet order (index k -> frames k*8 .. k*8+7)
  describe(tid)            human-readable meaning of an id
"""
import os

MOD = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))


def hash01(x, y, seed):
    n = (x * 374761393 + y * 668265263 + seed * 2147483647) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    n ^= n >> 16
    return (n & 0xffff) / 65535.0


def tone(c, f):
    return (max(0, min(255, int(c[0] * f))), max(0, min(255, int(c[1] * f))),
            max(0, min(255, int(c[2] * f))), 255)


SIDE_BITS = (1, 2, 4, 8)  # N E S W
CORNER_SIDES = {1: (1, 2), 2: (2, 4), 4: (4, 8), 8: (8, 1)}  # NE SE SW NW


def shore_combos():
    out = []
    for mask in range(16):
        free = [c for c, (a, b) in CORNER_SIDES.items() if not (mask & a) and not (mask & b)]
        for k in range(1 << len(free)):
            corners = 0
            for j, c in enumerate(free):
                if k & (1 << j):
                    corners |= c
            if mask or corners:
                out.append((mask, corners))
    return out


SHORE_COMBOS = shore_combos()
SHORE_INDEX = {k: i for i, k in enumerate(SHORE_COMBOS)}
LAND_BASE, WATER_BASE = 200, 400

GRASS_IDS = tuple(range(1, 7))
MEADOW_IDS = (10, 11, 18, 19)
ROUGH_IDS = (12, 13, 14, 20, 21, 22)
DIRT_IDS = (15, 16)
SAND_ID = 17
WATER_IDS = ((100, 101, 102, 103), (104, 105), (106, 107))  # shallow, mid, deep
OPEN_WATER_IDS = tuple(i for g in WATER_IDS for i in g)


def templates():
    """[(template id, terrain type)] in tileset frame order (the order genterrain.py wrote its frames in)."""
    out = [(i, "Clear") for i in GRASS_IDS]
    out += [(i, "Clear") for i in MEADOW_IDS[:2]]       # 10, 11
    out += [(i, "Clear") for i in MEADOW_IDS[2:]]       # 18, 19
    out += [(i, "Rough") for i in ROUGH_IDS[:3]]        # 12..14
    out += [(i, "Rough") for i in ROUGH_IDS[3:]]        # 20..22
    out += [(i, "Rough") for i in DIRT_IDS]
    out += [(SAND_ID, "Clear")]
    out += [(i, "Water") for i in OPEN_WATER_IDS]
    out += [(LAND_BASE + i, "Clear") for i in range(len(SHORE_COMBOS))]
    out += [(WATER_BASE + i, "Water") for i in range(len(SHORE_COMBOS))]
    return out


def water_templates():
    return list(OPEN_WATER_IDS) + [WATER_BASE + i for i in range(len(SHORE_COMBOS))]


def describe(tid):
    if tid in GRASS_IDS:
        return "grass %d" % tid
    if tid in MEADOW_IDS:
        return "meadow %d" % tid
    if tid in ROUGH_IDS:
        return "rough %d" % tid
    if tid in DIRT_IDS:
        return "dirt %d" % tid
    if tid == SAND_ID:
        return "sand"
    for name, ids in zip(("shallow", "mid", "deep"), WATER_IDS):
        if tid in ids:
            return "%s water %d" % (name, tid)
    if LAND_BASE <= tid < LAND_BASE + len(SHORE_COMBOS):
        return "beach land %s" % (SHORE_COMBOS[tid - LAND_BASE],)
    if WATER_BASE <= tid < WATER_BASE + len(SHORE_COMBOS):
        return "shore water %s" % (SHORE_COMBOS[tid - WATER_BASE],)
    return "unknown %d" % tid
