"""
iso_life_people - LIFE workstream: people and animals (RCT2-peep style), design phase.

build(root) writes PNGs under root/people/... and returns the Figma manifest section
{"page": "World elements", "section": "People", "groups": [...]}.  As __main__ writes root/people/section.json.
Sprite contract (1x, alpha 0/255, no outline, feet at bottom centre of an even canvas, no baked shadow):
  adult/elder 8x8 (7 px figure), child 6x6 (5 px), jogger 8x8, shopper 10x8, umbrella 12x12, cyclist 10x12,
  parent+child 12x10, dog walker 16x12, dog 8x6, pigeon 6x4 (peck) / 8x6 (fly), seagull 12x8, bench 16x16.
  Walkers: strips ordered N, E, S, W (world compass); E/S show the face, N/W the back; S = mirror of E, W = mirror of N.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from iso_life_common import OUT  # noqa: E402
import iso_life_people_walk as walk  # noqa: E402
import iso_life_people_var as var  # noqa: E402
import iso_life_people_act as act  # noqa: E402
import iso_life_people_act2 as act2  # noqa: E402
import iso_life_people_act3 as act3  # noqa: E402
import iso_life_people_animals as animals  # noqa: E402
import iso_life_people_scene as scene  # noqa: E402


def build(root):
    groups = [walk.walk_group(root), var.variety_group(root)]
    items = act.act_group(root) + act2.act2_items(root) + act3.act3_items(root)
    groups.append({"title": "Activities", "note": "bench (both axes), sitters, waiting, cycling, jogging, rain, construction, shopping, family, dog walking",
                   "columns": 4, "scale": 3, "items": items})
    groups.append({"title": "Animals", "note": "dog in 3 coats (4 dirs x 4 frames), pigeons, seagull; birds face screen-right (mirror for left)",
                   "columns": 4, "scale": 3, "items": animals.animal_items(root)})
    groups.append({"title": "In context", "note": "peeps on asphalt, pavement and grass at 1x (shown at 2x)",
                   "columns": 1, "scale": 2, "items": scene.scene_items(root)})
    return {"page": "World elements", "section": "People", "groups": groups}


if __name__ == "__main__":
    root = OUT
    data = build(root)
    os.makedirs(os.path.join(root, "people"), exist_ok=True)
    with open(os.path.join(root, "people", "section.json"), "w") as f:
        json.dump(data, f, indent=1)
    n = sum(len(g["items"]) for g in data["groups"])
    print("people section: %d groups, %d items" % (len(data["groups"]), n))
    for g in data["groups"]:
        print("  %-12s %d" % (g["title"], len(g["items"])))
