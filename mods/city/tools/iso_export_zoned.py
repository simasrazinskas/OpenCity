"""iso_export_zoned.py - ZONED area of tools/iso_export.py: growables, signatures, construction, rubble.

Writes bits/world/iso/zoned/<image>-<seq>.png (one sheet per image + sequence; the construction and rubble
images share one sheet per image) and sequences/buildings.yaml. Everything is re-rendered in-process from the
generators (iso_zoned.py registry), with all four facings, night '-lit', winter, abandoned and burnt.

Facings: an actor W x D shows build('WxD') at facing 0/2 and build('DxW') at facing 1/3 (rotated 90 degrees), so
the sprite footprint is always W x D (asserted). Frames are clipped to the footprint's screen x-extent.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import iso_zoned  # noqa: E402,F401  (registers every zone and signature)
import iso_zoned_core as C  # noqa: E402
import iso_zoned_build as B  # noqa: E402
import iso_zoned_sig as SG  # noqa: E402
from iso_export_lib import (SeqFile, frame_from_sprite, lit_of, pmap, write_sheet)  # noqa: E402

SHEET_W = 2048      # packed sheet width: keeps the tall sheets as low as possible
LEVELS = 5
VARIANTS = 4

HEADER = """Growable buildings, signatures, construction sites and rubble (ZONED art, iso).
Sheets: iso/zoned/<image>-<sequence>.png (one PngSheet per image and sequence, Frame[i] = x,y,w,h;ox,oy);
the shared images zon-construction and zon-rubble keep one sheet each (Start offsets). Anchor = footprint
ground centre, so every sequence has Offset 0,0.
Facing f: 0 front toward +Y (screen lower-left), 1 toward +X (lower-right), 2 toward -Y, 3 toward -X. An actor
W x D shows its W x D model at f 0/2 and the D x W model turned at f 1/3, so the footprint always matches.

Lot actors <zone>-<W>x<D> (56):
  idle         80 frames   ((level - 1) * 4 + variant) * 4 + f   (variants 0-1 North American, 2-3 European)
  idle-lit     80 frames   same layout, emissive (lit window) pixels of the night render only, same anchor
  winter       80 frames   same layout, snow
  abandoned    80 frames   same layout
  burnt        80 frames   same layout
  construction 12 frames   (stage - 1) * 4 + f  (1 foundation, 2 frame + crane, 3 nearly done); level 3, variant 0
Signature buildings sig-* (3x3, level 5):
  idle, idle-lit, winter, abandoned, burnt   8 frames   theme * 4 + f   (theme 0 North American, 1 European)
  construction                               12 frames  (stage - 1) * 4 + f
Legacy 1x1 decoration res-low-1..3, res-high-1..3, com-low-1..3, com-high-1..3, ind-1..3, off-1..3:
  idle, idle-lit   16 frames   variant * 4 + f   (4 variants; the actor number picks the level of the 1x1 design:
                   -1 / -2 / -3 = level 1 / 3 / 5; res-high-N and com-high-N reuse res-low / com-low 1x1 at levels
                   3 / 4 / 5)
Shared images (the render trait picks f from the actor facing; the stage cycles while it is built):
  construction.small / .large    12 frames  (stage - 1) * 4 + f   (res-low 1x1 / res-med 2x2 family, level 3)
  zon-construction.s<W>x<D>      12 frames  (stage - 1) * 4 + f   (level 3 site of a neutral family: 1x1 2x1 1x2 res-low,
                                 3x1 1x3 res-row, 2x2 3x2 2x3 3x3 res-med, 4x3 3x4 com-high)
  zon-rubble.s<W>x<D>            4 frames   f                      (generic rubble heap + wall stubs)
"""

# zones whose 1x1 design is used for the legacy images: image prefix -> (design zone, levels of -1 / -2 / -3)
LEGACY = {
    "res-low": ("res-low", (1, 3, 5)), "res-high": ("res-low", (3, 4, 5)),
    "com-low": ("com-low", (1, 3, 5)), "com-high": ("com-low", (3, 4, 5)),
    "ind": ("ind", (1, 3, 5)), "off": ("off", (1, 3, 5)),
}

# neutral construction-site family per footprint (zon-construction / lot-independent sites)
FAMILY = {
    "1x1": "res-low", "2x1": "res-low", "1x2": "res-low", "3x1": "res-row", "1x3": "res-row",
    "2x2": "res-med", "3x2": "res-med", "2x3": "res-med", "3x3": "res-med", "4x3": "com-high", "3x4": "com-high",
}
FOOTPRINTS = "1x1 2x1 1x2 3x1 1x3 2x2 3x2 2x3 3x3 4x3 3x4".split()

SIGS = list(SG.SIGS)


def flip(fp):
    W, D = C.dims(fp)
    return "%dx%d" % (D, W)


def facings_of(scene_a, scene_b, W, D, **kw):
    """4 sprites of an actor W x D: f 0/2 from scene_a (W x D model), f 1/3 from scene_b (D x W model)."""
    out = []
    for f in range(4):
        spr, _ = C.render(scene_a if f % 2 == 0 else scene_b, facing=f, **kw)
        if spr.footprint != (W, D):
            raise AssertionError("facing %d footprint %s != %s" % (f, spr.footprint, (W, D)))
        out.append(spr)
    return out


def lot_scenes(prefix, fp, level, var, state="ok", stage=0, season="summer"):
    return (C.build(prefix, fp, level, var, state, stage, season),
            C.build(prefix, flip(fp), level, var, state, stage, season))


def frames_day(prefix, fp, level, var, state="ok", stage=0, season="summer"):
    W, D = C.dims(fp)
    a, b = lot_scenes(prefix, fp, level, var, state, stage, season)
    return [frame_from_sprite(s) for s in facings_of(a, b, W, D, season=season)]


def frames_lit(prefix, fp, level, var):
    W, D = C.dims(fp)
    a, b = lot_scenes(prefix, fp, level, var)
    return [lit_of(s) for s in facings_of(a, b, W, D, night=True)]


def lot_state_frames(prefix, fp, level, var):
    """All five full-detail sequences of one (level, variant): {seq: [4 Frames]}."""
    return {
        "idle": frames_day(prefix, fp, level, var),
        "idle-lit": frames_lit(prefix, fp, level, var),
        "winter": frames_day(prefix, fp, level, var, season="winter"),
        "abandoned": frames_day(prefix, fp, level, var, "abandoned"),
        "burnt": frames_day(prefix, fp, level, var, "burnt"),
    }


def construction_frames(prefix, fp, level, var):
    """12 frames (stage - 1) * 4 + f."""
    out = []
    for stage in (1, 2, 3):
        out += frames_day(prefix, fp, level, var, "ok", stage)
    return out


def tight_all(frames):
    """Shrink a frame list to tight crops in the worker (keeps the result small to pickle)."""
    return [f.tight() for f in frames]


# ------------------------------------------------------------------------------------------- tasks
def task_lot(args):
    prefix, fp = args
    seqs = {s: [] for s in ("idle", "idle-lit", "winter", "abandoned", "burnt")}
    for level in range(1, LEVELS + 1):
        for var in range(VARIANTS):
            got = lot_state_frames(prefix, fp, level, var)
            for s, fr in got.items():
                seqs[s] += tight_all(fr)
    name = "%s-%s" % (prefix, fp)
    for s, fr in seqs.items():
        write_sheet("zoned/%s-%s.png" % (name, s), fr, SHEET_W)
    cons = tight_all(construction_frames(prefix, fp, 3, 0))
    write_sheet("zoned/%s-construction.png" % name, cons, SHEET_W)
    counts = {s: len(fr) for s, fr in seqs.items()}
    counts["clipped"] = C.CLIPPED[0]
    return name, counts


def task_sig(name):
    seqs = {s: [] for s in ("idle", "idle-lit", "winter", "abandoned", "burnt")}
    for var in (0, 2):                    # theme 0 NA (variant 0), theme 1 EU (variant 2)
        for s, fr in lot_state_frames(name, "3x3", 5, var).items():
            seqs[s] += tight_all(fr)
    for s, fr in seqs.items():
        write_sheet("zoned/%s-%s.png" % (name, s), fr, SHEET_W)
    write_sheet("zoned/%s-construction.png" % name, tight_all(construction_frames(name, "3x3", 5, 0)), SHEET_W)
    counts = {s: len(fr) for s, fr in seqs.items()}
    counts["clipped"] = C.CLIPPED[0]
    return name, counts


def task_legacy(args):
    image, num = args
    zone, levels = LEGACY[image]
    level = levels[num - 1]
    idle, lit = [], []
    for var in range(VARIANTS):
        idle += tight_all(frames_day(zone, "1x1", level, var))
        lit += tight_all(frames_lit(zone, "1x1", level, var))
    name = "%s-%d" % (image, num)
    write_sheet("zoned/%s-idle.png" % name, idle, SHEET_W)
    has_lit = any(not f.empty() for f in lit)
    if has_lit:
        write_sheet("zoned/%s-idle-lit.png" % name, lit, SHEET_W)
    return name, {"idle": len(idle), "idle-lit": len(lit) if has_lit else 0}


def task_site(fp):
    """zon-construction s<fp>: 12 frames from the neutral family of the footprint."""
    return tight_all(construction_frames(FAMILY[fp], fp, 3, 0))


def task_rubble(fp):
    W, D = C.dims(fp)
    a = B.rubble(W, D, C.seed_of("rubble", fp))
    b = B.rubble(D, W, C.seed_of("rubble", flip(fp)))
    return tight_all([frame_from_sprite(s) for s in facings_of(a, b, W, D)])


def task_legacy_site(name):
    prefix, fp = {"small": ("res-low", "1x1"), "large": ("res-med", "2x2")}[name]
    return tight_all(construction_frames(prefix, fp, 3, 0))


# ------------------------------------------------------------------------------------------- export
def lot_actors():
    out = []
    for prefix, (title, fps, fn) in C.ZONES.items():
        if prefix.startswith("sig-"):
            continue
        for fp in fps:
            out.append((prefix, fp))
    return out


def export():
    actors = lot_actors()
    sf = SeqFile("buildings.yaml", HEADER)
    stats = []

    # lot actors, signatures, legacy: the workers write their own sheets
    for name, counts in pmap(task_lot, actors):
        stats.append(counts)
    for name, counts in pmap(task_sig, SIGS):
        stats.append(counts)
    legacy = [(img, n) for img in LEGACY for n in (1, 2, 3)]
    legacy_res = pmap(task_legacy, legacy)

    # shared sheets (zon-construction, zon-rubble, construction)
    sites = pmap(task_site, FOOTPRINTS)
    write_sheet("zoned/zon-construction.png", [f for fr in sites for f in fr], SHEET_W)
    rub = pmap(task_rubble, FOOTPRINTS)
    write_sheet("zoned/zon-rubble.png", [f for fr in rub for f in fr], SHEET_W)
    small, large = pmap(task_legacy_site, ["small", "large"])
    write_sheet("zoned/construction.png", small + large, SHEET_W)

    # ---- sequences
    for prefix, fp in actors:
        name = "%s-%s" % (prefix, fp)
        for s, n in (("idle", 80), ("idle-lit", 80), ("winter", 80), ("abandoned", 80), ("burnt", 80),
                     ("construction", 12)):
            sf.seq(name, s, "iso/zoned/%s-%s.png" % (name, s), length=n)
    for name in ["%s-%d" % (img, n) for img, n in legacy]:
        sf.seq(name, "idle", "iso/zoned/%s-idle.png" % name, length=16)
    for (name, counts) in legacy_res:
        if counts["idle-lit"]:
            sf.seq(name, "idle-lit", "iso/zoned/%s-idle-lit.png" % name, length=16)
    sf.seq("construction", "small", "iso/zoned/construction.png", length=12, start=0, Tick=200)
    sf.seq("construction", "large", "iso/zoned/construction.png", length=12, start=12, Tick=200)
    for i, fp in enumerate(FOOTPRINTS):
        sf.seq("zon-construction", "s" + fp, "iso/zoned/zon-construction.png", length=12, start=i * 12, Tick=200)
    for i, fp in enumerate(FOOTPRINTS):
        sf.seq("zon-rubble", "s" + fp, "iso/zoned/zon-rubble.png", length=4, start=i * 4)
    for name in SIGS:
        for s, n in (("idle", 8), ("idle-lit", 8), ("winter", 8), ("abandoned", 8), ("burnt", 8),
                     ("construction", 12)):
            sf.seq(name, s, "iso/zoned/%s-%s.png" % (name, s), length=n)
    sf.write()

    files = [f for f in os.listdir(os.path.join(os.path.dirname(HERE), "bits", "world", "iso", "zoned"))
             if f.endswith(".png")]
    size = sum(os.path.getsize(os.path.join(os.path.dirname(HERE), "bits", "world", "iso", "zoned", f)) for f in files)
    return "%d lot actors, %d signatures, %d legacy, %d sheets, %.1f MB, clipped px (main proc) %d" % (
        len(actors), len(SIGS), len(legacy), len(files), size / 1e6, C.CLIPPED[0])
