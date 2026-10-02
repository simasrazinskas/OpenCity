#!/usr/bin/env python3
"""Style bible: palette, materials (day/night/winter), lighting, scale chart, sample renders.
Writes mods/city/design/iso/style/** and its Figma manifest.json."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_kit_models as km  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "design", "iso", "style"))
M = ik.mat
WALLS = ["plain", "plaster", "plaster_white", "brick", "brick_yellow", "siding", "siding_white", "wood",
         "concrete", "stone", "metal", "metal_light", "glass", "glass_dark"]
WINDOWS = ["windows_brick", "windows_plaster", "windows_siding", "windows_concrete", "windows_office",
           "shopfront", "shopfront_brick"]
ROOFS = ["roof_tiles", "roof_tiles_brown", "slate", "shingle", "roof_metal", "roof_flat", "roof_gravel", "roof_green"]
GROUNDS = ["asphalt", "paving", "concrete_ground", "grass", "grass_dry", "meadow", "dirt", "mud", "sand", "gravel",
           "rock", "forest_floor", "snow", "water"]
VEG = ["foliage", "foliage_light", "conifer", "bark", "bark_birch"]
DETAIL = ["awning", "awning_green", "chimney_bands", "door", "door_glass", "garage", "marking", "marking_yellow",
          "lamp", "neon"]


def put(spr_or_img, rel):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if isinstance(spr_or_img, np.ndarray):
        ik.write_png(path, spr_or_img)
    else:
        ik.even(spr_or_img).save(path)
    return rel


def sample(name):
    s = ik.Scene((1, 1), 5)
    if name in WALLS or name in WINDOWS:
        s.box(0.15, 0.15, 0, 0.7, 0.7, 32, name, top="roof_flat")
    elif name in ROOFS:
        s.box(0.18, 0.18, 0, 0.64, 0.64, 10, "plaster")
        s.roof_gable(0.18, 0.18, 10, 0.64, 0.64, 18, name, axis="x", gable="plaster")
    elif name in GROUNDS:
        s.tile(0, 0, name)
    elif name in VEG:
        if name.startswith("bark"):
            s.cylinder(0.5, 0.5, 0, 0.12, 30, name, top="wood")
        else:
            s.sphere(0.5, 0.5, 14, 0.3, name, rough=0.25)
    else:
        s.box(0.2, 0.2, 0, 0.6, 0.6, 20, name)
    return s


def materials(man):
    for title, names in (("Walls", WALLS), ("Windows over walls (night: lit windows)", WINDOWS), ("Roofs", ROOFS),
                         ("Ground", GROUNDS), ("Vegetation", VEG), ("Details", DETAIL)):
        g = man.group("Materials: %s (day / night / winter)" % title, columns=9, scale=2,
                      note="Each cell shows the lit left face, shaded right face and top.")
        for n in names:
            sc = sample(n)
            g.add(put(ik.render(sc), "materials/%s-day.png" % n), n)
            g.add(put(ik.render(sc, night=True), "materials/%s-night.png" % n), n + " night")
            g.add(put(ik.render(sc, season="winter"), "materials/%s-winter.png" % n), n + " winter")


def lighting(man):
    g = man.group("Lighting: one fixed light from the upper left", columns=7, scale=3,
                  note="Top 0.77, left (+Y) face 0.53, right (+X) face 0.25 of the ramp; curved surfaces smooth. "
                       "Silhouette rim darkened 2 shades; creases darken pixels just behind nearer parts.")
    white = M("plain", ramp="stone", shade=1.0)
    shapes = []
    s = ik.Scene((1, 1)); s.box(0.2, 0.2, 0, 0.6, 0.6, 24, white); shapes.append(("cube", s))
    s = ik.Scene((1, 1)); s.cylinder(0.5, 0.5, 0, 0.3, 26, white); shapes.append(("cylinder", s))
    s = ik.Scene((1, 1)); s.sphere(0.5, 0.5, 13, 0.32, white); shapes.append(("sphere", s))
    s = ik.Scene((1, 1)); s.cone(0.5, 0.5, 0, 0.32, 30, white); shapes.append(("cone", s))
    s = ik.Scene((1, 1)); s.pyramid(0.2, 0.2, 0, 0.6, 0.6, 26, white); shapes.append(("pyramid", s))
    s = ik.Scene((1, 1)); s.box(0.2, 0.25, 0, 0.6, 0.5, 14, white); s.roof_gable(0.2, 0.25, 14, 0.6, 0.5, 12, white); shapes.append(("gable", s))
    s = ik.Scene((1, 1)); s.box(0.2, 0.25, 0, 0.6, 0.5, 14, white); s.roof_hip(0.2, 0.25, 14, 0.6, 0.5, 12, white); shapes.append(("hip", s))
    for lab, sc in shapes:
        g.add(put(ik.render(sc), "lighting/%s.png" % lab), lab)
    for lab, sc in shapes:
        g.add(put(ik.render(sc, night=True), "lighting/%s-night.png" % lab), lab + " night")
    g2 = man.group("Lighting: outline options considered", columns=3, scale=3,
                   note="Chosen: 'edge' (rim darkened inside the silhouette). 'dark' adds a 1 px outline, 'none' is flat.")
    sc = km.house()
    for o in ("edge", "dark", None):
        g2.add(put(ik.render(sc, outline=o), "lighting/outline-%s.png" % o), "outline %s" % o)


def palette(man):
    g = man.group("Palette: 18 ramps x 12 shades", columns=1, scale=2,
                  note="Every pixel is one of these colours. Ramps hue-shift: cool saturated darks, warm soft lights.")
    g.add(put(ik.palette_sheet(16, 16), "palette/palette.png"), "full palette")
    g = man.group("Palette: ramps", columns=6, scale=2)
    for i, n in enumerate(ik.RAMP_NAMES):
        img = ik.palette_sheet(10, 16)[i * 16:(i + 1) * 16]
        g.add(put(img, "palette/ramp-%s.png" % n), n)


def storey_building(n, seed=0):
    s = ik.Scene((1, 1), seed)
    h = n * ik.STOREY
    if n <= 3:
        wall = M("windows_plaster", ramp="sand", shade=2.5)
        s.box(0.15, 0.15, 0, 0.7, 0.7, h, wall)
        s.roof_gable(0.15, 0.15, h, 0.7, 0.7, 12, "roof_tiles", gable=wall)
    elif n <= 10:
        wall = M("windows_brick", ramp="brick")
        s.box(0.12, 0.12, 0, 0.76, 0.76, h, wall)
        s.roof_flat(0.12, 0.12, h, 0.76, 0.76, "roof_gravel", parapet=2, rim="concrete")
    else:
        s.box(0.15, 0.15, 0, 0.7, 0.7, h, M("glass", storey=10) if n < 30 else M("windows_office"))
        s.roof_flat(0.15, 0.15, h, 0.7, 0.7, "roof_flat", parapet=2, rim="concrete")
        s.box(0.35, 0.35, h, 0.25, 0.25, 8, "concrete", top="roof_metal")
    return s


def scale_chart(man):
    g = man.group("Scale chart", columns=1, scale=1,
                  note="Cell = 64x32 px (~16 m). Storey = 10 px. Person ~8 px, car ~16 px long, bus ~40 px. "
                       "Buildings: 1, 2, 3, 5, 10, 20, 40 storeys.")
    for night in (False, True):
        cm = ik.Compositor(11, 2, top_margin=440, side_margin=8)
        for x in range(11):
            cm.ground(ik.flat_tile("grass", (x, 0)), x, 0)
            cm.ground(ik.render_tile(km.road(lamp=False), (x, 1)), x, 1)
        cm.place(ik.render(km.person(), night=night), 0, 0)
        cm.place(ik.render(km.person(1, "teal", "wood"), night=night), 0, 1)
        cm.place(ik.render(km.car(), night=night), 1, 1)
        cm.place(ik.render(km.bus(), night=night), 2, 1)
        cm.place(ik.render(ik.tree("oak", "summer", 2, 1), night=night), 1, 0)
        cm.place(ik.render(ik.tree("spruce", "summer", 2, 1), night=night), 2, 0)
        for i, n in enumerate((1, 2, 3, 5, 10, 20, 40)):
            cm.place(ik.render(storey_building(n, i), night=night), 3 + i + 1, 0)
        g.add(put(cm.render(), "scale/scale-chart%s.png" % ("-night" if night else "")), "night" if night else "day")
    g = man.group("Scale chart: 4x2 tile strip at 2x", columns=1, scale=2)
    cm = ik.Compositor(4, 2, top_margin=70)
    for x in range(4):
        cm.ground(ik.flat_tile("grass", (x, 0)), x, 0)
        cm.ground(ik.render_tile(km.road(lamp=False), (x, 1)), x, 1)
    cm.place(ik.render(km.person()), 0, 0)
    cm.place(ik.render(km.car()), 1, 1)
    cm.place(ik.render(km.bus()), 2, 1)
    cm.place(ik.render(ik.tree("linden", "summer", 2, 1)), 1, 0)
    cm.place(ik.render(storey_building(2)), 2, 0)
    cm.place(ik.render(storey_building(5)), 3, 0)
    g.add(put(cm.render(), "scale/strip-4x2.png"), "4x2 strip")


def samples(man):
    models = [("house", km.house()), ("shop", km.shop()), ("factory", km.factory()), ("tower", km.tower()),
              ("road", km.road()), ("tree", ik.tree("oak", "summer", 2, 1))]
    g = man.group("Sample renders: day / night / winter", columns=6, scale=2,
                  note="Reference models proving the look; the real sets come from ZONED/CIVIC/NET/LIFE.")
    for lab, sc in models:
        g.add(put(ik.render(sc), "samples/%s-day.png" % lab), lab)
    for lab, sc in models:
        g.add(put(ik.render(sc, night=True), "samples/%s-night.png" % lab), lab + " night")
    for lab, sc in models:
        win = ik.tree("oak", "winter", 2, 1) if lab == "tree" else sc
        g.add(put(ik.render(win, season="winter"), "samples/%s-winter.png" % lab), lab + " winter")
    g = man.group("Sample: 4 facings (front +Y, +X, -Y, -X)", columns=2, scale=2)
    for lab, sc in (("shop", km.shop()), ("factory", km.factory())):
        ik.save_strip(os.path.join(ROOT, "samples/%s-facings.png" % lab), ik.facings(sc), )
        g.add("samples/%s-facings.png" % lab, lab + ": 4 facings")
    g = man.group("Sample: engine export (day frame + '-lit' emissive companion)", columns=4, scale=2,
                  note="The game multiplies sprites by the night ambient and draws '-lit' frames unmultiplied.")
    for lab, sc in (("shop", km.shop()), ("tower", km.tower())):
        d, l = ik.save_pair(os.path.join(ROOT, "samples/%s-export.png" % lab), sc)
        g.add("samples/%s-export.png" % lab, lab + " day frame")
        g.add("samples/%s-export-lit.png" % lab, lab + " -lit frame")


def thumbs(man):
    models = [("house", km.house()), ("shop", km.shop()), ("factory", km.factory()), ("tower-3x3", km.tower3())]
    for size in (64, 96):
        g = man.group("Build-menu thumbnails %dx%d (%s)" % (size, size, "1x" if size == 64 else "1.5x"),
                      columns=4, scale=2 if size == 64 else 1,
                      note="ik.thumbnail(scene, size): own footprint ground, default facing, largest tile width "
                           "(multiple of 4) that fits, bottom-centred, transparent.")
        for lab, sc in models:
            t = ik.thumbnail(sc, size)
            g.add(put(t.img, "thumbs/%s-%d.png" % (lab, size)), "%s (tile %d)" % (lab, t.tile))
    sheet = ik.grid([ik.read_png(os.path.join(ROOT, "thumbs/%s-%d.png" % (lab, sz))) for sz in (64, 96)
                     for lab, _ in models], 4, 4)
    g = man.group("Build-menu thumbnails: sheet", columns=1, scale=2)
    g.add(put(sheet, "thumbs/thumbs-sheet.png"), "64 and 96 px")
    g = man.group("Reduced-scale projection: tile 64 / 48 / 32 / 16", columns=4, scale=2,
                  note="render(scene, tile=(w, w/2)) re-projects geometry; window grids are laid out in output "
                       "pixels, so small scales get fewer, readable windows instead of mush.")
    for tw in (64, 48, 32, 16):
        g.add(put(ik.render(km.factory(), tile=(tw, tw // 2)), "thumbs/factory-tile%d.png" % tw), "factory %d" % tw)
    for tw in (64, 48, 32, 16):
        g.add(put(ik.render(km.tower(), tile=(tw, tw // 2)), "thumbs/tower-tile%d.png" % tw), "tower %d" % tw)


def main():
    man = ik.Manifest(ROOT, "Direction & mockups", "Style bible")
    palette(man)
    lighting(man)
    scale_chart(man)
    samples(man)
    thumbs(man)
    materials(man)
    man.write()
    print("style:", sum(len(gr.items) for gr in man.groups), "items")


if __name__ == "__main__":
    main()
