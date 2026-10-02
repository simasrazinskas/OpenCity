#!/usr/bin/env python3
"""iso_export.py - export the isometric design art into game assets (deterministic, re-runnable).

    python3 mods/city/tools/iso_export.py              # every area
    python3 mods/city/tools/iso_export.py net zoned    # selected areas
    python3 mods/city/tools/iso_export.py --list

Areas (one module each, `iso_export_<area>.py`, exposing `export()`):
  terrain   tilesets/temperate.yaml (TileSize 64,32), bits/terrain/terrain.png, sequences/environment.yaml
            (snow, flood), genterrain ids (iso_terrain_ids.py), lobby map previews
  net       roads: sequences/networks.yaml (roadnet + road props)
  overlays  utilities, zones, tracks, extractor areas, info-view ramps, markers, network buildings, stops:
            sequences/overlays.yaml
  zoned     growables, signatures, construction, rubble: sequences/buildings.yaml
  civic     services, industry hubs, transit buildings: sequences/{services,industry,transit-buildings}.yaml
  nature    trees with seasons: sequences/nature.yaml
  life      vehicle/people/FX sheets for MOV (bits/world/iso/life), status icons and FX: sequences/misc.yaml

Shared helpers and the asset contract: iso_export_lib.py. Output sheets: bits/world/iso/<area>/*.png.
"""
import importlib
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

AREAS = ["terrain", "net", "overlays", "zoned", "civic", "nature", "life"]


def main(argv):
    if "--list" in argv:
        print(" ".join(AREAS))
        return 0
    todo = [a for a in argv if not a.startswith("-")] or AREAS
    for a in todo:
        if a not in AREAS:
            print("unknown area: %s (have: %s)" % (a, " ".join(AREAS)))
            return 2
    for a in todo:
        try:
            mod = importlib.import_module("iso_export_" + a)
        except ModuleNotFoundError as e:
            if e.name == "iso_export_" + a:
                print("[%s] not implemented yet, skipped" % a)
                continue
            raise
        t = time.time()
        summary = mod.export()
        print("[%s] %s (%.1fs)" % (a, summary or "done", time.time() - t))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
