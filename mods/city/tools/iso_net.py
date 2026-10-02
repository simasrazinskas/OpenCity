#!/usr/bin/env python3
"""
iso_net.py - NET workstream (design phase): regenerates every network / ground-overlay design PNG and merges
the module fragments into design/iso/net/manifest.json (page "World elements", section "Roads & networks").

    python3 mods/city/tools/iso_net.py            # everything
    python3 mods/city/tools/iso_net.py --manifest # only re-merge the fragments

Modules: iso_net_out_ground (flat tiles), iso_net_out_props (props/structures), iso_net_overlays (zones,
markers, info views), iso_net_vignettes2 (6x6 context blocks). Deterministic.
"""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from iso_net_core import OUT  # noqa: E402

# designer order: context first, then roads, junctions, structures, furniture, transit, utilities, overlays
ORDER = ["Vignettes", "Street:", "Street: one-way", "Avenue:", "Boulevard:", "Highway:", "Alley:", "Gravel road:",
         "Paired carriageways: median", "Paired carriageways composed", "One-way arrows", "Road wear", "Class transitions",
         "Junction control road paint", "Traffic signals:", "Traffic signals at night", "Signs:", "Roundabout",
         "Highway outside connection", "Bridge over water: street", "Bridge over water: avenue",
         "Bridge over water: boulevard", "Bridge over water: highway", "Elevated highway", "Street lamps (day)",
         "Street lamps at night", "Transit stops", "Street furniture", "Road add-ons", "Bus lanes", "Tram track",
         "Rail track", "Rail props", "Water pipes", "Sewage pipes", "High-voltage", "Wooden distribution",
         "Network buildings", "Treatment plant", "Parking lot surfaces", "Parking lots with", "Zone tiles: active",
         "Zone tiles: idle", "Tool markers", "Building footprint", "Info view", "Smooth gradient", "Legend"]


def rank(title):
    best = (len(ORDER), 0)
    for i, p in enumerate(ORDER):
        if title.startswith(p) and len(p) >= best[1]:
            best = (i, len(p))
    return best[0]


def merge():
    groups = []
    for f in sorted(glob.glob(os.path.join(OUT, "_frag", "*.json"))):
        with open(f) as fh:
            groups += json.load(fh)
    groups.sort(key=lambda g: rank(g["title"]))
    for g in groups:
        if "night" in g["title"].lower():
            g["bg"] = "dark"     # figma_publish: dark tile behind night sprites
    missing = [it["file"] for g in groups for it in g["items"] if not os.path.exists(os.path.join(OUT, it["file"]))]
    if missing:
        raise SystemExit("missing files: %s" % missing[:10])
    man = {"page": "World elements", "section": "Roads & networks", "groups": groups}
    with open(os.path.join(OUT, "manifest.json"), "w") as fh:
        json.dump(man, fh, indent=1)
    n = sum(len(g["items"]) for g in groups)
    print(f"manifest: {len(groups)} groups, {n} items")
    return man


def main():
    if "--manifest" not in sys.argv:
        import iso_net_out_ground
        import iso_net_out_props
        import iso_net_overlays
        import iso_net_vignettes2
        iso_net_out_ground.build()
        iso_net_out_props.build()
        if hasattr(iso_net_overlays, "build"):
            iso_net_overlays.build()
        else:
            iso_net_overlays.main()
        iso_net_vignettes2.build()
    merge()


if __name__ == "__main__":
    main()
