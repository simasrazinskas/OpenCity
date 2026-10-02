#!/usr/bin/env python3
"""Iso nature set (design phase): trees (game tree-1..4 + more species), growth stages, seasons, dead /
burnt / stumps, groves, bushes, hedges, flower beds, rocks, tall grass, reeds. Writes
mods/city/design/iso/nature/** and its Figma manifest.json."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_nature_small as N  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "design", "iso", "nature"))
SEASONS = ("spring", "summer", "autumn", "winter")
STAGES = ("sapling", "young", "mature")
LABEL = {"linden": "Linden", "oak": "Oak", "maple": "Maple", "birch": "Birch", "poplar": "Poplar",
         "willow": "Willow", "apple": "Apple", "cherry": "Cherry", "spruce": "Spruce", "fir": "Fir", "pine": "Pine"}


def out(scene, rel, season="summer", night=False):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    spr = ik.render(scene, season="winter" if season == "winter" else "summer", night=night)
    ik.even(spr).save(path)
    return rel


def main():
    man = ik.Manifest(ROOT, "World elements", "Nature")
    g = man.group("Game trees tree-1..4: four seasons", columns=4, scale=2,
                  note="tree-1 linden, tree-2 oak, tree-3 spruce, tree-4 birch. Anchor = cell centre.")
    for gid, sp in ik.GAME_TREES.items():
        for se in SEASONS:
            g.add(out(ik.tree(sp, se, 2, 1), "trees/%s-%s.png" % (gid, se), se), "%s %s" % (gid, se))

    g = man.group("All species, summer (mature)", columns=11, scale=2)
    for sp in ik.SPECIES:
        g.add(out(ik.tree(sp, "summer", 2, 1), "species/%s-summer.png" % sp), LABEL[sp])
    for se in ("spring", "autumn", "winter"):
        g = man.group("All species, %s" % se, columns=11, scale=2)
        for sp in ik.SPECIES:
            g.add(out(ik.tree(sp, se, 2, 1), "species/%s-%s.png" % (sp, se), se), LABEL[sp])

    g = man.group("Growth stages (sapling, young, mature)", columns=9, scale=2)
    for sp in ik.SPECIES:
        for st in range(3):
            g.add(out(ik.tree(sp, "summer", st, 2), "growth/%s-%s.png" % (sp, STAGES[st])), "%s %s" % (LABEL[sp], STAGES[st]))

    g = man.group("Variants: same species, different seeds", columns=8, scale=2)
    for sp in ("oak", "linden", "spruce", "birch"):
        for sd in (3, 4):
            g.add(out(ik.tree(sp, "summer", 2, sd), "variants/%s-v%d.png" % (sp, sd)), "%s v%d" % (LABEL[sp], sd))

    g = man.group("Dead, burnt and stumps", columns=8, scale=2)
    for sp in ("oak", "birch", "spruce", "pine"):
        for state in ("dead", "burnt", "stump"):
            g.add(out(ik.tree(sp, "summer", 2, 1, state), "dead/%s-%s.png" % (sp, state)), "%s %s" % (LABEL[sp], state))

    g = man.group("Groves (forest cells): deciduous, conifer, mixed x seasons", columns=4, scale=2)
    for kind in ("deciduous", "conifer", "mixed"):
        for se in SEASONS:
            for sd in (1, 2):
                g.add(out(N.grove(kind, se, sd), "groves/%s-%s-%d.png" % (kind, se, sd), se), "%s %s %d" % (kind, se, sd))

    g = man.group("Bushes", columns=8, scale=2)
    for i, (size, fl, lab) in enumerate(((0.8, None, "small"), (1.0, None, "medium"), (1.4, None, "large"),
                                         (1.1, "rose", "rose"), (1.1, "yellow", "forsythia"), (1.1, "snow", "white"))):
        for se in ("summer", "autumn", "winter"):
            g.add(out(N.bush(size, se, i, fl), "bushes/bush-%s-%s.png" % (lab, se), se), "%s %s" % (lab, se))

    g = man.group("Hedges (join edge to edge)", columns=4, scale=2)
    for kind in ("x", "y", "corner", "end"):
        for se in ("summer", "winter"):
            g.add(out(N.hedge(kind, se, 1), "hedges/hedge-%s-%s.png" % (kind, se), se), "%s %s" % (kind, se))

    g = man.group("Flower beds", columns=6, scale=2)
    beds = ((("rose", "yellow"), "rect", "rose+yellow"), (("purple", "snow"), "rect", "lavender+white"),
            (("red", "yellow"), "round", "red+yellow round"), (("purple", "rose"), "round", "violet+rose round"))
    for i, (cols, shape, lab) in enumerate(beds):
        g.add(out(N.flower_bed(cols, shape, i), "beds/bed-%d.png" % i), lab)
    g.add(out(N.flower_bed(("rose",), "rect", 9, "winter"), "beds/bed-winter.png", "winter"), "rect winter")

    g = man.group("Rocks and boulders", columns=6, scale=2)
    for i, (size, n, lab) in enumerate(((0.6, 1, "small"), (1.0, 1, "medium"), (1.6, 1, "large"),
                                        (1.0, 3, "cluster"), (1.3, 4, "outcrop"))):
        g.add(out(N.boulder(size, i + 1, n), "rocks/rock-%s.png" % lab), lab)
        g.add(out(N.boulder(size, i + 1, n), "rocks/rock-%s-snow.png" % lab, "winter"), lab + " snow")

    g = man.group("Ground cover: tall grass, reeds, logs", columns=8, scale=2)
    for se in SEASONS:
        g.add(out(N.tall_grass(se, 1), "cover/tall-grass-%s.png" % se, se), "tall grass " + se)
    g.add(out(N.tall_grass("summer", 2, True), "cover/reeds-summer.png"), "reeds summer")
    g.add(out(N.tall_grass("autumn", 2, True), "cover/reeds-autumn.png"), "reeds autumn")
    g.add(out(N.log(1), "cover/log.png"), "fallen log")
    g.add(out(N.log(1), "cover/log-snow.png", "winter"), "fallen log snow")

    g = man.group("Night: trees under the night ambient", columns=6, scale=2)
    for sp in ("linden", "oak", "spruce", "birch", "pine", "willow"):
        g.add(out(ik.tree(sp, "summer", 2, 1), "night/%s-night.png" % sp, night=True), LABEL[sp])
    man.write()
    print("nature:", sum(len(gr.items) for gr in man.groups), "items")


if __name__ == "__main__":
    main()
