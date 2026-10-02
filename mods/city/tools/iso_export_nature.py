"""iso_export_nature.py - trees (tree-1..tree-4) as iso sheets + sequences/nature.yaml.

One sheet per game tree image (`bits/world/iso/nature/tree-N.png`), anchored at the trunk base = the cell centre
(= the actor's CenterPosition: 1x1 Building footprint). Species come from isokit GAME_TREES (tree-1 linden,
tree-2 oak, tree-3 spruce, tree-4 birch), variants are seeds 1..VARIANTS of the same species.

Sequences (V = VARIANTS = 4; frame = variant unless noted):
  idle, summer   0..3    summer, mature; idle frame 0 is the default look
  spring         0..3
  autumn         0..3
  winter         0..3    bare crowns (snow on conifers)
  growth         0..11   stage * 4 + variant, stage 0 sapling, 1 young, 2 mature (summer)
  dead, burnt, stump 0..3
Pick the variant with a cell hash (render only), e.g. (cell.X * 73 + cell.Y * 151) & 3.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_export_lib as L  # noqa: E402

VARIANTS = 4
SEASONS = ("summer", "spring", "autumn", "winter")
STAGES = (0, 1, 2)
STATES = ("dead", "burnt", "stump")
HEADER = """
Trees (OpenCity iso). One sheet per image: bits/world/iso/nature/<image>.png, anchored at the trunk base = cell
centre = the actor's CenterPosition (1x1 footprint), so Offset stays 0,0. Species: tree-1 linden, tree-2 oak,
tree-3 spruce, tree-4 birch (isokit GAME_TREES). V = 4 variants (seeds 1..4 of the species), pick with a cell hash.
  idle, summer   V frames (variant), mature summer foliage; idle frame 0 is the default
  spring, autumn, winter   V frames (variant), same layout (winter: bare crowns, snow on conifers)
  growth   3 * V frames: stage * V + variant, stage 0 sapling, 1 young, 2 mature (summer)
  dead, burnt, stump   V frames (variant)
Sheet order: summer(0..3) spring autumn winter growth(12) dead burnt stump.
"""


def _render(job):
    species, season, stage, seed, state = job
    s = ik.tree(species, season, stage, seed, state)
    spr = ik.render(s, season="winter" if season == "winter" else "summer")
    return L.frame_from_sprite(spr)


def _jobs(species):
    jobs = []
    for se in SEASONS:
        jobs += [(species, se, 2, v + 1, "alive") for v in range(VARIANTS)]
    for st in STAGES:
        jobs += [(species, "summer", st, v + 1, "alive") for v in range(VARIANTS)]
    for state in STATES:
        jobs += [(species, "summer", 2, v + 1, state) for v in range(VARIANTS)]
    return jobs


def export():
    sf = L.SeqFile("nature.yaml", HEADER)
    total = 0
    sheets = {}
    for image, species in ik.GAME_TREES.items():
        frames = L.pmap(_render, _jobs(species))
        sheets[image] = frames
        total += L.write_sheet("nature/%s.png" % image, frames)
        fn = "iso/nature/%s.png" % image
        V = VARIANTS
        sf.image(image, comment="%s" % species)
        sf.seq(image, "idle", fn, start=0, length=V)
        for i, se in enumerate(SEASONS):
            if se != "summer":
                sf.seq(image, se, fn, start=i * V, length=V)
        sf.seq(image, "summer", fn, start=0, length=V)
        sf.seq(image, "growth", fn, start=4 * V, length=3 * V)
        for i, state in enumerate(STATES):
            sf.seq(image, state, fn, start=7 * V + i * V, length=V)
    sf.write()
    if os.environ.get("ISO_EXPORT_PREVIEW"):
        for image, frames in sheets.items():
            L.preview("/tmp/nature-%s.png" % image, frames, cols=VARIANTS, k=2)
    return "%d tree images, %d frames" % (len(sheets), total)
