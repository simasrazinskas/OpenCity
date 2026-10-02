"""
iso_life_markers - LIFE workstream: world status icons, selection/hover markers, placement ghosts,
route lines and cursors (design phase). Deterministic. `build(root)` writes PNGs under
root/markers and root/cursors and returns the Figma manifest section dict.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from iso_life_common import canvas, blit, save_png, strip, OUT   # noqa: E402
import iso_life_mgeo as G                                        # noqa: E402
import iso_life_mglyphs_b as GB                                  # noqa: E402
from iso_life_mglyphs_a import GLYPHS                            # noqa: E402
from iso_life_mbubble import bubble, TIERS                       # noqa: E402
import iso_life_msel as S                                        # noqa: E402
import iso_life_mroute as R                                      # noqa: E402
import iso_life_mcursor as C                                     # noqa: E402
from iso_life_mscene import scene                                # noqa: E402

TIER_ORDER = ["info", "problem", "warning", "major", "error", "fatal", "good"]
TIER_LABEL = {"info": "Info", "problem": "Problem", "warning": "Warning", "major": "Major",
              "error": "Error", "fatal": "Fatal", "good": "Good"}
# default tier per problem for the single-icon sheets
DEFAULT_TIER = {"leveledup": "good", "fire": "major", "collapsed": "error", "crime": "warning",
                "abandoned": "error", "unhappy": "problem", "sick": "warning", "ambulance": "warning",
                "deadbody": "major", "flooded": "major", "garbage": "problem", "noise": "info",
                "airpollution": "warning", "groundpollution": "warning", "highrent": "info",
                "lowlandvalue": "info", "traffic": "problem"}


def tier_of(n):
    return DEFAULT_TIER.get(n, "warning" if n.startswith("no") else "problem")


def _save(root, rel, img):
    save_png(os.path.join(root, rel), img)
    return {"file": rel}


def item(root, rel, img, label):
    save_png(os.path.join(root, rel), img)
    return {"file": rel, "label": label}


def build(root=None):
    root = root or OUT
    groups = []

    # ---------------------------------------------------------------- 1. status icons
    items = []
    for n, lab in GB.PROBLEMS:
        items.append(item(root, "markers/status/%s.png" % n, bubble(GLYPHS[n](), tier_of(n)), lab))
    groups.append({"title": "Status icons: the 24 CityProblem kinds",
                   "note": "Thought bubble 22x27 px; glyph 8x8 + outline; 1 px rim colour = default severity",
                   "columns": 8, "scale": 3, "items": items})
    items = []
    for n, lab in GB.EXTRAS:
        items.append(item(root, "markers/status/%s.png" % n, bubble(GLYPHS[n](), tier_of(n)), lab))
    groups.append({"title": "Status icons: extra kinds", "columns": 9, "scale": 3, "items": items})
    frames = [bubble(GLYPHS["nopower"](), "warning", dy=d) for d in (0, 1)]
    for i, f in enumerate(frames):
        save_png(os.path.join(root, "markers/status/nopower-bob-%d.png" % i), f)
    items = [item(root, "markers/status/nopower-bob.png", strip(frames, 2), "Bob, 2 frames (1 px)")]
    groups.append({"title": "Status icons: bob animation", "note": "Alternate frames every ~500 ms",
                   "columns": 1, "scale": 3, "items": items})

    # ---------------------------------------------------------------- 2. severity tiers
    items = []
    for n in ("nopower", "fire", "unhappy"):
        for t in TIER_ORDER:
            items.append(item(root, "markers/tiers/%s-%s.png" % (n, t), bubble(GLYPHS[n](), t),
                              "%s" % TIER_LABEL[t]))
    groups.append({"title": "Severity tiers (rim + tint): no power, fire, unhappy",
                   "note": "info blue, problem yellow, warning orange, major red, error dark red, fatal black, good green",
                   "columns": 7, "scale": 3, "items": items})

    # ---------------------------------------------------------------- 3. selection & hover
    fps = [(1, 1), (2, 2), (3, 3), (1, 2), (2, 1)]
    items = []
    for nx, ny in fps:
        items.append(item(root, "markers/select/hover-%dx%d.png" % (nx, ny), S.brackets(nx, ny, "hover"),
                          "Hover %dx%d" % (nx, ny)))
    groups.append({"title": "Hover brackets (thin white)", "columns": 5, "scale": 2, "items": items})
    items = []
    for nx, ny in fps:
        items.append(item(root, "markers/select/selected-%dx%d.png" % (nx, ny), S.brackets(nx, ny, "selected", 0),
                          "Selected %dx%d" % (nx, ny)))
    groups.append({"title": "Selected brackets (thick yellow)", "columns": 5, "scale": 2, "items": items})
    items = []
    for nx, ny in [(1, 1), (2, 2)]:
        fr = [S.brackets(nx, ny, "selected", f) for f in range(4)]
        for f, im in enumerate(fr):
            save_png(os.path.join(root, "markers/select/selected-%dx%d-f%d.png" % (nx, ny, f)), im)
        items.append(item(root, "markers/select/selected-%dx%d-strip.png" % (nx, ny), strip(fr, 2),
                          "%dx%d pulse, 4 frames" % (nx, ny)))
    groups.append({"title": "Selected animation (arms pulse, colour cycles)", "columns": 1, "scale": 2, "items": items})
    items = [item(root, "markers/select/vehicle-ring.png", S.ring(24, 12), "Vehicle ring"),
             item(root, "markers/select/vehicle-ring-thin.png", S.ring(24, 12, thick=False, c="#ffffff"), "Hover ring"),
             item(root, "markers/select/citizen-ring.png", S.ring(12, 6, thick=False), "Citizen ring")]
    ch = [S.chevron(f) for f in range(4)]
    for f, im in enumerate(ch):
        save_png(os.path.join(root, "markers/select/follow-chevron-f%d.png" % f), im)
    items.append(item(root, "markers/select/follow-chevron-strip.png", strip(ch, 2), "Follow chevron, 4 frames"))
    groups.append({"title": "Vehicle and citizen markers", "columns": 4, "scale": 3, "items": items})

    # ---------------------------------------------------------------- 4. placement ghosts
    items = []
    for nx, ny in [(1, 1), (2, 2), (3, 3)]:
        for v in (True, False):
            items.append(item(root, "markers/ghost/fp-%dx%d-%s.png" % (nx, ny, "valid" if v else "invalid"),
                              S.ghost_fp(nx, ny, v), "%dx%d %s" % (nx, ny, "valid" if v else "invalid")))
    items.append(item(root, "markers/ghost/bulldoze-fp-1x1.png", S.bulldoze_fp(1, 1), "Bulldoze 1x1"))
    items.append(item(root, "markers/ghost/bulldoze-fp-2x2.png", S.bulldoze_fp(2, 2), "Bulldoze 2x2"))
    groups.append({"title": "Footprint fills (ordered dither, ground shows through)", "columns": 4, "scale": 2,
                   "items": items})
    h = G.house()
    items = [item(root, "markers/ghost/house-normal.png", h, "Normal"),
             item(root, "markers/ghost/house-valid.png", S.ghost_building(h, True), "Valid ghost"),
             item(root, "markers/ghost/house-invalid.png", S.ghost_building(h, False), "Invalid ghost"),
             item(root, "markers/ghost/house-bulldoze.png", S.bulldoze_building(h), "Bulldoze")]
    groups.append({"title": "Ghost building tint (demo house)", "note": "Checker/Bayer drop-out, solid dithered outline",
                   "columns": 4, "scale": 2, "items": items})

    # ---------------------------------------------------------------- 5. routes & paths
    items = []
    for key, conns, lab in R.TILE_SET:
        items.append(item(root, "markers/routes/bus-%s.png" % key, R.line_tile("bus", conns), lab))
    groups.append({"title": "Route line tiles (shown: bus)", "note": "World compass: N = -Y (screen up-right), E = +X (down-right), S = +Y (down-left), W = -X (up-left). Lines run along cell axes between edge midpoints",
                   "columns": 5, "scale": 2, "items": items})
    items = []
    for n in R.LINES:
        items.append(item(root, "markers/routes/%s-ew.png" % n, R.line_tile(n, ["xneg", "xpos"]),
                          R.LINE_NAMES[n]))
    groups.append({"title": "Line colours", "columns": 6, "scale": 2, "items": items})
    items = []
    for n in R.LINES:
        items.append(item(root, "markers/routes/pin-%s.png" % n, R.pin(n), "Stop " + R.LINE_NAMES[n]))
    for n in ("bus", "tram", "train", "metro"):
        items.append(item(root, "markers/routes/flag-%s.png" % n, R.flag(n), "Flag " + R.LINE_NAMES[n]))
    items.append(item(root, "markers/routes/flag-waypoint.png", R.flag(None), "Waypoint"))
    groups.append({"title": "Stop pins and waypoint flags", "columns": 11, "scale": 3, "items": items})
    items = []
    for d in ("xp", "xn", "yp", "yn"):
        items.append(item(root, "markers/routes/chevron-%s.png" % R.COMPASS[d], R.chevron_tile("tram", d), "Heading " + R.COMPASS[d].upper()))
    groups.append({"title": "Direction chevron (shown: tram; world compass)", "columns": 4, "scale": 2, "items": items})
    items = []
    for key, conns in [("ew", ["xneg", "xpos"]), ("ns", ["yneg", "ypos"]), ("corner-sw", ["ypos", "xneg"])]:
        fr = [R.path_dots(conns, f) for f in range(4)]
        items.append(item(root, "markers/routes/follow-dots-%s.png" % key, strip(fr, 2),
                          "Follow path %s, 4 frames" % key))
    groups.append({"title": "Vehicle-follow path dots", "columns": 1, "scale": 2, "items": items})

    # ---------------------------------------------------------------- 6. cursors
    hot = {}
    cur_items = []
    base = [("default", C.pointer, "Pointer"), ("hand", C.hand_open, "Hand"), ("grab", C.hand_grab, "Grab"),
            ("build", C.hammer, "Build"), ("road", C.road, "Road"), ("zone", C.zone_brush, "Zone brush"),
            ("bulldoze", C.bulldozer, "Bulldoze"), ("inspect", C.inspect, "Inspect"), ("move", C.move, "Move"),
            ("place", C.place, "Place"), ("pipe", C.pipe, "Pipe / water"), ("power", C.power, "Power line"),
            ("transit", C.transit, "Transit line"), ("blocked", C.blocked_big, "Blocked")]
    tools = {}
    for name, fn, lab in base:
        im, hs = fn()
        tools[name] = (im, hs)
        cur_items.append(item(root, "cursors/%s.png" % name, im, lab))
        hot[name] = list(hs)
    for name, lab in [("build", "Build blocked"), ("road", "Road blocked"), ("zone", "Zone blocked"),
                      ("place", "Place blocked"), ("bulldoze", "Bulldoze blocked")]:
        im, hs = tools[name]
        cur_items.append(item(root, "cursors/%s-blocked.png" % name, C.with_badge(im), lab))
        hot[name + "-blocked"] = list(hs)
    groups.append({"title": "Cursors: pointer and tools", "note": "32x32, hotspots in cursors/hotspots.json",
                   "columns": 8, "scale": 3, "items": cur_items})
    items = []
    for d in C.DIRS:
        im, hs = C.scroll_arrow(d)
        items.append(item(root, "cursors/scroll-%s.png" % d, im, "Scroll " + d.upper()))
        hot["scroll-" + d] = list(hs)
    for d in C.DIRS:
        im, hs = C.scroll_arrow(d, True)
        items.append(item(root, "cursors/scroll-%s-blocked.png" % d, im, d.upper() + " blocked"))
        hot["scroll-%s-blocked" % d] = list(hs)
    groups.append({"title": "Cursors: scroll arrows", "note": "Scroll arrows use SCREEN directions (UI), not the world compass", "columns": 8, "scale": 3, "items": items})
    with open(os.path.join(root, "cursors", "hotspots.json"), "w") as f:
        json.dump(hot, f, indent=1, sort_keys=True)

    # ---------------------------------------------------------------- 7. in context
    img = scene()
    groups.append({"title": "Markers in context",
                   "note": "4x3 patch: status bubbles, selected building, valid/invalid ghosts, bus route with stop pins",
                   "columns": 1, "scale": 1,
                   "items": [item(root, "markers/context.png", img, "World markers in context")]})

    return {"page": "World elements", "section": "World markers & cursors", "groups": groups}


if __name__ == "__main__":
    root = OUT
    sec = build(root)
    with open(os.path.join(root, "markers", "section.json"), "w") as f:
        json.dump(sec, f, indent=1)
    n = sum(len(g["items"]) for g in sec["groups"])
    print("groups", len(sec["groups"]), "items", n)
