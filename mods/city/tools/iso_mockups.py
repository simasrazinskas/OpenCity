#!/usr/bin/env python3
"""Main-view mockup scenes composed from every workstream's real art (design phase).

Writes mods/city/design/iso/mockups/{scenes,hud-bg}/ and mockups/manifest.json
(page "Direction & mockups", section "Main view mockups"). The hud-bg/ backgrounds are picked up by
tools/iso_ui_screens.py so the UI's HUD screens sit on the real city.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
import iso_mock_assets as A  # noqa: E402
import iso_mock_city as MC  # noqa: E402
import iso_mock_fill as MF  # noqa: E402
import iso_mock_scene as MS  # noqa: E402
import iso_mock_extras as EX  # noqa: E402

ROOT = os.path.join(A.ISO, "mockups")


def city(variant="full"):
    c = MF.fill(MC.City())
    EX.smoke(c)
    EX.train(c, MC.RAIL_X + 0.5, 15.2, "N")
    EX.boats(c, [("ferry", 18.0, 38.4, "E"), ("motorboat", 41.0, 38.7, "W"), ("motorboat", 10.0, 37.8, "E")])
    EX.heli(c, 37.5, 17.0)
    picks = [(lambda b: b["kind"] == "zoned" and b["args"]["prefix"] == "res-low" and b["cx"] == 17, "nopower"),
             (lambda b: b["kind"] == "zoned" and b["args"]["prefix"] == "com-low" and b["cy"] > 41, "garbage"),
             (lambda b: b["kind"] == "zoned" and b["args"]["prefix"] == "ind" and b["cx"] > 48, "noworkers"),
             (lambda b: b["kind"] == "zoned" and b["args"]["prefix"] == "res-med" and b["cy"] < 20, "unhappy")]
    if variant == "full":
        EX.bubbles(c, picks)
    if variant == "build":
        construction(c)
    return c


def construction(c):
    """(f): a suburb block being zoned and built, a placement ghost and a road drag in progress."""
    rng = ik.Rng(91)
    zone_block = (33, 42, 5, 4)
    ghost_block = (39, 42, 5, 4)
    for (x0, y0, w, h) in (zone_block, ghost_block):
        cells = {(x, y) for x in range(x0, x0 + w) for y in range(y0, y0 + h)}
        c.buildings = [b for b in c.buildings if (b["cx"], b["cy"]) not in cells]
        c.trees = [t for t in c.trees if (int(t[0]), int(t[1])) not in cells]
        for cell in cells:
            c.occ.discard(cell)
            c.terr[cell] = "grass"
    x0, y0, w, h = zone_block
    zone = [(x, y) for x in range(x0, x0 + w) for y in range(y0, y0 + 2)]
    stages = [1, 2, 3, 2, 1]
    for i, x in enumerate(range(x0, x0 + w)):
        c.place("zoned", dict(prefix="res-low", fp="1x1", level=2, var=rng.randint(0, 3), facing=0,
                              stage=stages[i]), x, y0 + 3, 1, 1)
    def dust(w_, m, tf, xs=list(range(x0, x0 + w))):
        for i, x in enumerate(xs):
            if stages[i] in (1, 2):
                img, ax, ay = A.fx("life/fx/construction/dust_strip.png", 6, i % 6)
                w_.obj(img, ax, ay, x + 0.5, y0 + 3.5, z=4, bias=0.2)
    c.extras.append(dust)
    gx, gy = ghost_block[0] + 1, ghost_block[1]
    path = [(x, ghost_block[1] + 3, "valid") for x in range(ghost_block[0], ghost_block[0] + 4)]
    path.append((ghost_block[0] + 4, ghost_block[1] + 3, "path-e"))
    EX.tools(c, zone, (gx, gy, "school", 2, 2), path, (ghost_block[0] + 4.6, ghost_block[1] + 3.6))


def land_value(c):
    out = {}
    for x in range(MC.N):
        for y in range(MC.N):
            if c.terr.get((x, y)) == "water":
                continue
            d = np.hypot((x - 32) / 40.0, (y - 26) / 34.0)
            v = 1.0 - d
            v += 0.18 * np.exp(-((y - 38) / 3.0) ** 2)
            if c.district(x, y) == "industry":
                v -= 0.35
            if c.district(x, y) == "farm":
                v -= 0.15
            v = int(round(np.clip(v, 0, 1) * 10)) * 10
            out[(x, y)] = A.png("net/infoviews/good-%03d.png" % v)
    return out


def save(img, rel):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ik.write_png(path, img)
    return rel


def main():
    man = ik.Manifest(ROOT, "Direction & mockups", "Main view mockups")
    g = man.group("Main view: one city, every workstream's real art (1920x1080, 1x)", columns=1, scale=1,
                  note="Terrain/coasts/trees (KIT), roads/bridges/lamps/signals (NET), growables (ZONED), services "
                       "(CIVIC), vehicles in NET lanes + people + smoke + bubbles (LIFE). Composed by isokit.World "
                       "with the engine's 32-px strip sorting.")
    bg = {}
    c = city("full")
    day = MS.compose(c, MS.Mode()).render()
    g.add(save(day, "scenes/day-summer.png"), "(a) Day, summer")
    night = MS.compose(city("full"), MS.Mode(night=True)).render()
    g.add(save(night, "scenes/night.png"), "(b) Night: lit windows, lamps, light pools, headlights")
    winter = MS.compose(city("full"), MS.Mode(season="winter")).render()
    g.add(save(winter, "scenes/winter.png"), "(c) Winter: snow, bare trees, chimney smoke")
    ci = city("info")
    info = MS.compose(ci, MS.Mode(tf=lambda im: ik.desaturate(im), info=land_value(ci))).render()
    save(info, "hud-bg/infoview-1920x1080.png")
    info = info.copy()
    leg = A.png("net/infoviews/legend-good.png")
    ik.blit(info, ik.upscale(leg, 2), 24, 1080 - 24 - leg.shape[0] * 2)
    g.add(save(info, "scenes/infoview-landvalue.png"), "(d) Info view: land value (NET overlay style)")
    w = MS.compose(c, MS.Mode())
    jx, jy = w.screen(MC.AVENUE_X + 0.5, 28.5)
    x0, y0 = int(jx - 480), int(jy - 300)
    close = ik.upscale(day[y0:y0 + 540, x0:x0 + 960], 2)
    g.add(save(close, "scenes/closeup-junction-2x.png"), "(e) 2x close-up: avenue x boulevard junction")
    build = MS.compose(city("build"), MS.Mode()).render()
    g.add(save(build, "scenes/construction-zoning.png"), "(f) Zoning, placement ghost, road drag, construction")
    # backgrounds for the UI workstream's HUD screens
    save(day, "hud-bg/day-1920x1080.png")
    save(day[180:900, 320:1600], "hud-bg/day-1280x720.png")
    hx, hy = w.screen(39.5, 15.5)
    for u, (vw, vh) in ((1, (300, 110)), (1.5, (450, 165))):
        vx = min(max(int(hx - vw / 2), 0), 1920 - vw)
        vy = min(max(int(hy - vh * 0.75), 0), 1080 - vh)
        save(day[vy:vy + vh, vx:vx + vw], "hud-bg/viewport-%dx%d.png" % (vw, vh))
    g2 = man.group("Main view thumbnails of the variants (for side-by-side review)", columns=3, scale=1)
    for rel, lab in (("scenes/day-summer.png", "day"), ("scenes/night.png", "night"), ("scenes/winter.png", "winter"),
                     ("scenes/infoview-landvalue.png", "info view"), ("scenes/construction-zoning.png", "construction")):
        im = ik.read_png(os.path.join(ROOT, rel))[::3, ::3]
        g2.add(save(im, rel.replace("scenes/", "scenes/small-")), lab)
    hud_group(man)
    man.write()
    print("mockups written")


def hud_group(man):
    ui = os.path.join(A.ISO, "ui", "screens")
    stems = [("hud-1920x1080-1x", "HUD 1920x1080, UI 1x"), ("hud-1920x1080-1.5x", "HUD 1920x1080, UI 1.5x"),
             ("hud-1280x720-1x", "HUD 1280x720, UI 1x"), ("hud-1280x720-1.5x", "HUD 1280x720, UI 1.5x"),
             ("hud-infoview-1920x1080-1x", "Info view HUD 1920x1080"),
             ("hud-sidebar-1280x720-1x", "Sidebar variant 1280x720")]
    g = man.group("HUD on the real city (UI workstream screens, re-rendered on these mockups)", columns=2, scale=1)
    for stem, lab in stems:
        src = os.path.join(ui, stem + ".png")
        if os.path.exists(src):
            g.add(save(ik.read_png(src), "hud/%s.png" % stem), lab)


if __name__ == "__main__":
    main()
