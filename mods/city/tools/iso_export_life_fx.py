"""iso_export_life_fx - effect sheets for MOV / BLD (part of `life`): smoke, steam, fire, water, dust, sparks,
fireworks, weather, pollution, flood, night glows. No sequences (MOV adopts them); documented in the README.

Sheets: bits/world/iso/life/fx/<name>.png, frames in the order listed in FX below, anchors per the `anchor` rule:
  bottom  = emitter / ground point: bottom row, horizontal centre of the design canvas
  centre  = canvas centre (ground glows, tiles, particles)
  (x, y)  = explicit pixel of the design canvas
Night glows and light cones are for ADDITIVE blending (black = no light); cloud shadows are meant for ~40 % opacity.
"""
import os

import iso_export_lib as L
from iso_export_lib import Frame
from iso_life_common import load_png

SUB = "life/fx/"
ROOT = os.path.join(L.DESIGN, "life", "fx")


def seq(dirname, base, n):
    return ["%s/%s_%d.png" % (dirname, base, i) for i in range(n)]


def one(dirname, *names):
    return ["%s/%s.png" % (dirname, n) for n in names]


# name, [design files], anchor, suggested Tick (ms per frame, 0 = static), blend, comment
FX = [
    ("smoke-house", seq("smoke", "house", 8), "bottom", 120, "alpha", "chimney smoke, 8-frame loop, wind drifts right"),
    ("smoke-industry", seq("smoke", "industry", 8), "bottom", 120, "alpha", "dark industrial smoke, 8-frame loop"),
    ("smoke-plume", seq("smoke", "stack_plume", 8), "bottom", 120, "alpha", "tall stack plume (dark), 8-frame loop"),
    ("smoke-plume-light", seq("smoke", "stack_plume_light", 8), "bottom", 120, "alpha", "tall stack plume (light), 8-frame loop"),
    ("steam", seq("smoke", "steam", 8), "bottom", 100, "alpha", "steam puffs, 8-frame loop"),
    ("cooling-cloud", seq("smoke", "cooling_cloud", 8), "bottom", 120, "alpha", "cooling tower cloud, 8-frame loop"),
    ("fire-stage1", seq("fire", "stage1", 6), "bottom", 90, "alpha", "building fire stage 1 (window), anchor = footprint centre"),
    ("fire-stage2", seq("fire", "stage2", 6), "bottom", 90, "alpha", "stage 2 (medium)"),
    ("fire-stage3", seq("fire", "stage3", 6), "bottom", 90, "alpha", "stage 3 (roof fire)"),
    ("fire-stage4", seq("fire", "stage4", 6), "bottom", 120, "alpha", "stage 4 (burnt out, embers)"),
    ("fire-heap", seq("fire", "garbage", 4), "bottom", 100, "alpha", "burning garbage heap"),
    ("hose-n", seq("water", "hose_n", 4), (2, 25), 80, "alpha", "fire hose jet toward N (screen up-right), anchor = nozzle"),
    ("hose-e", seq("water", "hose_e", 4), (2, 12), 80, "alpha", "jet toward E (screen down-right), anchor = nozzle"),
    ("hose-s", seq("water", "hose_s", 4), (33, 12), 80, "alpha", "jet toward S (screen down-left), anchor = nozzle"),
    ("hose-w", seq("water", "hose_w", 4), (33, 25), 80, "alpha", "jet toward W (screen up-left), anchor = nozzle"),
    ("splash", seq("water", "splash", 6), (14, 19), 80, "alpha", "water splash ring, plays once, anchor = ring centre"),
    ("heli-drop", seq("water", "heli_drop", 4), "bottom", 100, "alpha", "helicopter water drop, anchor = impact point"),
    ("dust", seq("construction", "dust", 6), "bottom", 100, "alpha", "construction dust cloud"),
    ("demolition", seq("construction", "demolition", 6), "bottom", 100, "alpha", "demolition cloud"),
    ("sparks-short", seq("construction", "sparks_short", 4), "centre", 70, "alpha", "short sparks"),
    ("sparks-weld", seq("construction", "sparks_weld", 4), "centre", 70, "alpha", "welding sparks"),
    ("fireworks-red", seq("fireworks", "red", 8), "bottom", 100, "alpha", "rocket, burst, falling sparkles; anchor = launch point"),
    ("fireworks-gold", seq("fireworks", "gold", 8), "bottom", 100, "alpha", "see fireworks-red"),
    ("fireworks-green", seq("fireworks", "green", 8), "bottom", 100, "alpha", "see fireworks-red"),
    ("fireworks-blue", seq("fireworks", "blue", 8), "bottom", 100, "alpha", "see fireworks-red"),
    ("rain", one("weather", "rain_s", "rain_m", "rain_l", "snow_s", "snow_m", "snow_l"), "centre", 0, "alpha",
     "0-2 rain streaks short/medium/long (slant down-left), 3-5 snowflakes small/medium/large"),
    ("rain-splash", seq("weather", "rain_splash", 4), "centre", 70, "alpha", "ground splash, 4 frames"),
    ("puddles", one("weather", "puddle_s", "puddle_m", "puddle_l"), "centre", 0, "alpha", "ground puddles small/medium/large"),
    ("fog", one("weather", "fog_light", "fog_dense"), "centre", 0, "alpha", "64x32 seamless fog tiles"),
    ("cloud-shadow", one("weather", "cloud_shadow_0", "cloud_shadow_1"), "centre", 0, "alpha", "~200x100 blobs, draw at 35-45 % opacity"),
    ("lightning", seq("weather", "lightning", 3), "bottom", 60, "alpha", "bolt, anchor = strike point"),
    ("smog", one("pollution", "smog_1", "smog_2", "smog_3"), "centre", 0, "alpha", "64x32 smog haze tiles, 3 intensities"),
    ("stain", one("pollution", "stain_1", "stain_2", "stain_3"), "centre", 0, "alpha", "64x32 ground stain tiles, 3 levels"),
    ("toxic-sheen", seq("pollution", "toxic_sheen", 4), "centre", 150, "alpha", "64x32 toxic water sheen, 4-frame loop"),
    ("noise-rings", seq("pollution", "noise_rings", 4), "centre", 150, "alpha", "concentric iso arcs, 4-frame loop"),
    ("dead-grass", one("pollution", "dead_grass_1", "dead_grass_2"), "centre", 0, "alpha", "64x32 dead grass tiles"),
    ("flood-water", seq("flood", "water", 4), "centre", 200, "alpha", "64x32 flood water, 4-frame ripple loop"),
    ("flood-edges", one("flood", "edge_n", "edge_e", "edge_s", "edge_w"), "centre", 0, "alpha",
     "half-tile water edges, water side in the name (N = up-right)"),
    ("night-pools", one("night", "pool_s", "pool_m", "pool_l", "pool_cool_m", "bulb"), "centre", 0, "additive",
     "ground light pools small/medium/large/cool + bulb, anchor = light source on the ground"),
    ("night-windows", one("night", "window_warm_2x3_L", "window_warm_2x3_R", "window_warm_3x4_L", "window_warm_3x4_R",
                          "window_cool_2x3_L", "window_cool_2x3_R", "window_cool_3x4_L", "window_cool_3x4_R"),
     "centre", 0, "additive", "window glows: warm then cool; 2x3 and 3x4; L = on a left wall, R = on a right wall"),
    ("night-signs", one("night", "sign_amber", "sign_blue", "sign_green", "sign_red"), "centre", 0, "additive", "shop sign glows"),
    ("night-headlights", one("night", "headlight_n", "headlight_e", "headlight_s", "headlight_w"), "centre", 0, "additive",
     "44x28 cones, anchor = headlamp point (world compass N E S W)"),
    ("night-taillights", one("night", "taillight_n", "taillight_e", "taillight_s", "taillight_w"), "centre", 0, "additive",
     "20x14 glows (world compass N E S W)"),
]


def export_fx():
    rows = []
    for name, files, anchor, tick, blend, comment in FX:
        frames = []
        for f in files:
            img = load_png(os.path.join(ROOT, f))
            h, w = img.shape[:2]
            if anchor == "bottom":
                a = (w // 2, h - 1)
            elif anchor == "centre":
                a = (w // 2, h // 2)
            else:
                a = anchor
            frames.append(Frame(img, *a))
        n = L.write_sheet(SUB + name + ".png", frames)
        rows.append((name, n, anchor, tick, blend, comment))
    return rows
