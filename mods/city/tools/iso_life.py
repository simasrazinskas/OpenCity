"""
iso_life - runs every LIFE generator and writes the Figma manifest.

    python3 mods/city/tools/iso_life.py            # everything
    python3 mods/city/tools/iso_life.py vehicles   # one or more of: vehicles people fx markers

mods/city/design/iso/life/manifest.json is a top-level LIST of section objects (page "World elements"):
Vehicles, People, Effects & weather, World markers & cursors. Every `file` is relative to design/iso/life/.
Each generator also leaves its own <area>/section.json.
"""
import importlib
import json
import os
import sys

from iso_life_common import OUT

# World-art areas whose hand-drawn PNGs are snapped to the shared isokit palette after generation.
# Not snapped: vehicles (already rendered on it), markers/cursors (saturated signal colours of the overlay/UI layer),
# fx/night (additive light layers: their dark ramps must stay neutral).
QUANTIZE = {"people": [], "fx": ["night"]}

AREAS = [("vehicles", "iso_life_vehicles"), ("people", "iso_life_people"), ("fx", "iso_life_fx"),
         ("markers", "iso_life_markers")]


def quantize_tree(area, skip):
    import numpy as np
    import isokit as ik
    from iso_life_common import load_png, save_png
    base = os.path.join(OUT, area)
    n = 0
    for dirpath, _, files in os.walk(base):
        rel = os.path.relpath(dirpath, base).split(os.sep)[0]
        if rel in skip:
            continue
        for fn in files:
            if fn.endswith(".png"):
                p = os.path.join(dirpath, fn)
                img = load_png(p)
                m = img[..., 3] > 0
                q = img.copy()
                q[m, :3] = ik.nearest(img[m, :3])
                if not np.array_equal(q, img):
                    save_png(p, q)
                    n += 1
    return n


def main(args):
    want = set(args) or {a for a, _ in AREAS}
    for area, module in AREAS:
        if area not in want:
            continue
        sec = importlib.import_module(module).build(OUT)
        with open(os.path.join(OUT, area, "section.json"), "w") as f:
            json.dump(sec, f, indent=1)
        print("%-9s %4d items in %2d groups" % (area, sum(len(g["items"]) for g in sec["groups"]), len(sec["groups"])))
        if area in QUANTIZE:
            print("          %d PNGs snapped to the isokit palette" % quantize_tree(area, QUANTIZE[area]))
    sections = []
    for area, _ in AREAS:
        p = os.path.join(OUT, area, "section.json")
        if os.path.exists(p):
            sections.append(json.load(open(p)))
    # The overview street (needs every area's PNGs) opens the first section.
    if sections and all(os.path.exists(os.path.join(OUT, a, "section.json")) for a, _ in AREAS):
        overview = importlib.import_module("iso_life_scene").build(OUT)
        sections[0]["groups"] = [g for g in sections[0]["groups"] if not g["title"].startswith("Overview")]
        sections[0]["groups"].insert(0, overview)
    missing = [it["file"] for s in sections for g in s["groups"] for it in g["items"]
               if not os.path.exists(os.path.join(OUT, it["file"]))]
    if missing:
        raise SystemExit("manifest references missing files: %s" % missing[:5])
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(sections, f, indent=1)
    print("manifest.json: %d sections" % len(sections))


if __name__ == "__main__":
    main(sys.argv[1:])
