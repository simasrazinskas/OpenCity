#!/usr/bin/env python3
"""Iso terrain set (design phase): ground tiles, seasons, snow, farmland, water, shores, transitions,
map edge, cliffs. Writes mods/city/design/iso/terrain/** and its Figma manifest.json."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_terrain_fields as F  # noqa: E402
import iso_terrain_water as W  # noqa: E402
import iso_terrain_edges as E  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "design", "iso", "terrain"))
VARIANTS = 4
VORIG = [(0, 0), (7, 3), (13, 11), (5, 19)]

GROUND = [("grass", "Grass"), ("grass_dry", "Dry grass"), ("meadow", "Meadow"), ("dirt", "Dirt"),
          ("sand", "Sand"), ("rock", "Rock"), ("gravel", "Gravel"), ("forest_floor", "Forest floor"),
          ("mud", "Mud")]
SEASON_GRASS = {"spring": ik.mat("grass", shade=-2.2, flowers=0.012),
                "summer": ik.mat("grass"),
                "autumn": ik.mat("grass", ramp="olive", shade=-1.6, flowers=0.0)}
KEY_COMBOS = [(1, 0), (2, 0), (4, 0), (8, 0), (0, 1), (0, 2), (0, 4), (0, 8)]


def save(spr, rel):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ik.even(spr).save(path)
    return rel


def save_anim(frames, rel):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ik.save_strip(path, frames)
    return rel


def main():
    man = ik.Manifest(ROOT, "World elements", "Terrain & water")
    combos = ik.shore_combos()

    g = man.group("Ground: base types, 4 variants each", columns=8, scale=2,
                  note="64x32 diamonds. Variants use different noise origins so large areas don't tile visibly.")
    for key, label in GROUND:
        for k in range(VARIANTS):
            g.add(save(ik.flat_tile(key, VORIG[k]), "ground/%s-%d.png" % (key, k + 1)), "%s %d" % (label, k + 1))

    g = man.group("Ground: grass through the seasons", columns=8, scale=2)
    for season, m in SEASON_GRASS.items():
        for k in range(2):
            g.add(save(ik.flat_tile(m, VORIG[k]), "seasons/grass-%s-%d.png" % (season, k + 1)), "%s %d" % (season, k + 1))
    for k in range(2):
        g.add(save(ik.flat_tile("grass", VORIG[k], season="winter", snow=0.45), "seasons/grass-winter-light-%d.png" % (k + 1)),
              "winter light %d" % (k + 1))
        g.add(save(ik.flat_tile("grass", VORIG[k], season="winter"), "seasons/grass-winter-%d.png" % (k + 1)),
              "winter %d" % (k + 1))

    g = man.group("Ground: snow-covered", columns=9, scale=2, note="Light snow (patchy) and full snow per type.")
    for key, label in GROUND:
        g.add(save(ik.flat_tile(key, VORIG[1], season="winter", snow=0.45), "snow/%s-light.png" % key), label + " light")
    for key, label in GROUND:
        g.add(save(ik.flat_tile(key, VORIG[2], season="winter"), "snow/%s-full.png" % key), label + " full")

    for crop, stages in F.CROPS.items():
        g = man.group("Farmland: %s" % crop, columns=8, scale=2,
                      note="Growth stages; rows along X and along Y." if crop == "wheat" else None)
        for lab, _ in stages:
            for axis in ("x", "y"):
                g.add(save(ik.flat_tile(F.field_material(crop, lab, axis)), "farm/%s-%s-%s.png" % (crop, lab, axis)),
                      "%s %s" % (lab, axis.upper()))
    g = man.group("Farmland: winter", columns=8, scale=2)
    for lab in ("plowed", "stubble"):
        m = F.field_material("wheat", lab)
        g.add(save(ik.flat_tile(m, season="winter", snow=0.55), "farm/winter-%s.png" % lab), lab + " snowy")

    g = man.group("Water: open, shallow, deep (frame 0) + 8-frame loops", columns=6, scale=2,
                  note="Water level sits 5 px below land. Loop strips: 8 frames.")
    for depth in ("shallow", "open", "deep"):
        fr = [W.water_tile(0, 0, "beach", f, depth) for f in range(W.FRAMES)]
        g.add(save(fr[0], "water/water-%s.png" % depth), depth)
        g.add(save_anim(fr, "water/water-%s-anim.png" % depth), "%s: 8 frames" % depth)

    for style in ("beach", "bank"):
        sname = "Beach" if style == "beach" else "Grass bank"
        g1 = man.group("Coast (%s): land tiles, all 46 edge/corner masks" % sname, columns=8, scale=2,
                       note="Label: sides with water (N=up-right E=down-right S=down-left W=up-left) / water corners.")
        g2 = man.group("Coast (%s): water tiles, all 46 edge/corner masks" % sname, columns=8, scale=2,
                       note="Label: sides with land / land corners.")
        for i, (mask, corners) in enumerate(combos):
            lab = ik.combo_label(mask, corners)
            g1.add(save(W.land_tile(mask, corners, style), "coast/%s-land-%02d.png" % (style, i)), lab)
            g2.add(save(W.water_tile(mask, corners, style), "coast/%s-water-%02d.png" % (style, i)), lab)
        g3 = man.group("Coast (%s): animated surf, 8 frames" % sname, columns=4, scale=2)
        for mask, corners in KEY_COMBOS:
            fr = [W.water_tile(mask, corners, style, f) for f in range(W.FRAMES)]
            g3.add(save_anim(fr, "coast/%s-water-anim-%s.png" % (style, ik.combo_label(mask, corners).replace("/", "_"))),
                   ik.combo_label(mask, corners) + ": 8 frames")

    g = man.group("Transitions: edge masks (white = other terrain), all 46", columns=8, scale=2,
                  note="Same blob ids as the coast. Blend any pair of ground types in-engine with these.")
    for i, (mask, corners) in enumerate(combos):
        g.add(save(E.mask_tile(mask, corners), "transitions/mask-%02d.png" % i), ik.combo_label(mask, corners))
    g = man.group("Transitions: grass to dirt, all 46", columns=8, scale=2)
    for i, (mask, corners) in enumerate(combos):
        g.add(save(E.transition_tile("grass", "dirt", mask, corners), "transitions/grass-dirt-%02d.png" % i),
              ik.combo_label(mask, corners))
    for b, lab in (("sand", "sand"), ("forest_floor", "forest floor"), ("rock", "rock"), ("gravel", "gravel"),
                   ("grass_dry", "dry grass"), ("snow", "snow")):
        g = man.group("Transitions: grass to %s (edges + corners)" % lab, columns=8, scale=2)
        for mask, corners in KEY_COMBOS:
            g.add(save(E.transition_tile("grass", b, mask, corners), "transitions/grass-%s-%s.png" %
                       (b, ik.combo_label(mask, corners).replace("/", "_"))), ik.combo_label(mask, corners))

    g = man.group("Map edge border + cliffs", columns=6, scale=2,
                  note="Cross-section drawn under border cells (S = down-left side, E = down-right side). "
                       "Cliffs are optional, for future height support.")
    g.add(save(E.edge_face("S"), "edge/edge-land-S.png"), "land S")
    g.add(save(E.edge_face("E"), "edge/edge-land-E.png"), "land E")
    g.add(save(E.edge_face("S", water=True), "edge/edge-water-S.png"), "water S")
    g.add(save(E.edge_face("E", water=True), "edge/edge-water-E.png"), "water E")
    g.add(save(E.cliff_face("S"), "edge/cliff-S.png"), "cliff S")
    g.add(save(E.cliff_face("E"), "edge/cliff-E.png"), "cliff E")
    g.add(save(E.raised_block(8), "edge/raised-8.png"), "raised 8 px")
    g.add(save(E.raised_block(16, "grass_dry"), "edge/raised-16.png"), "raised 16 px")

    g = man.group("Preview: tiles composed into small maps", columns=2, scale=2)
    for style in ("beach", "bank"):
        g.add(save_preview(style), ("Beach" if style == "beach" else "Grass bank") + " coast")
    man.write()
    print("terrain:", sum(len(gr.items) for gr in man.groups), "items")


def save_preview(style):
    import iso_terrain_preview as P
    img = P.island(style)
    rel = "preview/coast-%s.png" % style
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ik.write_png(path, img)
    return rel


if __name__ == "__main__":
    main()
