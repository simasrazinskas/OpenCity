"""
iso_life_fx - LIFE workstream: effects, night lights, weather, pollution, flood (design phase).

build(root) writes PNGs under root/fx/... and returns the Figma manifest section dict.
Run as a script: writes root/fx/section.json. Deterministic (fixed seeds). Needs numpy only.
Sub-generators: iso_life_fx_{smoke,fire,water,fireworks,night,weather,pollution,scene}.py
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import iso_life_fx_smoke as smoke  # noqa: E402
import iso_life_fx_fire as fire  # noqa: E402
import iso_life_fx_water as water  # noqa: E402
import iso_life_fx_fireworks as fireworks  # noqa: E402
import iso_life_fx_night as night  # noqa: E402
import iso_life_fx_weather as weather  # noqa: E402
import iso_life_fx_pollution as pollution  # noqa: E402
import iso_life_fx_scene as scene  # noqa: E402

DEFAULT_ROOT = os.path.normpath(os.path.join(HERE, "..", "design", "iso", "life"))


def pick(items, *keys):
    return [i for i in items if any(k in i["file"] for k in keys)]


def group(title, items, columns, scale, note=None):
    g = {"title": title, "columns": columns, "scale": scale, "items": items}
    if note:
        g["note"] = note
    return g


def build(root=DEFAULT_ROOT):
    groups = []

    sm = smoke.build(root)
    groups.append(group("Smoke & steam", sm, 3, 2,
                        "8-frame loops, emitter = bottom centre, wind drifts right. House ~14x36, stack plume 44x60."))

    fr = fire.build(root)
    groups.append(group("Building fire: 4 stages + garbage heap", fr, 2, 2,
                        "1-cell overlay, anchor = footprint centre. Window flame at (cx-14, h-17), roof fire at (cx, h-30)."))

    wa, co = water.build(root)
    groups.append(group("Water: hose, splash, helicopter drop", wa, 2, 2,
                        "Directions in world compass: N = screen up-right, E = down-right, S = down-left, W = up-left. Hose nozzle: left canvas edge for N/E, right edge for S/W."))
    groups.append(group("Construction dust, demolition, sparks", co, 2, 2))

    fw = fireworks.build(root)
    groups.append(group("Fireworks (milestones)", fw, 2, 2, "32x40, anchor = launch point; burst centre at (16, 13)."))

    pools, windows, signs, cones = night.build(root)
    groups.append(group("Night: light pools", pools, 5, 2,
                        "ADDITIVE blend in game: black = no light. Anchor = canvas centre (source on the ground)."))
    groups.append(group("Night: window glows (warm / cool, 2x3 and 3x4, left and right walls)", windows, 8, 3,
                        "Core is the lit window (sheared 1 px per 2 columns), halo 1-2 px. Additive."))
    groups.append(group("Night: shop sign glows", signs, 4, 3, "9x3 px sign on a left wall. Additive."))
    groups.append(group("Night: vehicle head- and tail-lights", cones, 4, 2,
                        "Directions in world compass: N = screen up-right. Headlight cones 44x28, origin at canvas centre = headlamp point; tail glow 20x14. Additive."))

    wx = weather.build(root)
    groups.append(group("Weather: rain and snow", pick(wx, "rain_", "snow_"), 8, 3,
                        "Streaks slant down-left; splash 4 frames on ground."))
    groups.append(group("Weather: puddles and fog", pick(wx, "puddle", "fog"), 5, 2,
                        "Fog tiles are periodic noise: neighbouring tiles join seamlessly."))
    groups.append(group("Weather: cloud shadows", pick(wx, "cloud_shadow"), 2, 1,
                        "~200x100 soft dithered blobs. Draw at ~35-45% opacity in game (alpha is 0/255 in the file)."))
    groups.append(group("Weather: lightning", pick(wx, "lightning"), 1, 2, "Anchor = strike point (bottom)."))

    po, fl = pollution.build(root)
    groups.append(group("Pollution: smog haze (3 intensities)", pick(po, "smog"), 3, 2,
                        "64x32 diamond overlays, ordered dither."))
    groups.append(group("Pollution: ground stains (3 levels)", pick(po, "stain"), 3, 2))
    groups.append(group("Pollution: toxic water, noise, dead grass", pick(po, "toxic", "noise", "dead"), 2, 2,
                        "Noise rings: concentric iso arcs, 4-frame loop, anchor = centre."))
    groups.append(group("Flood", fl, 3, 2, "Directions in world compass: N = screen up-right. Water 4-frame ripple loop; edge pieces cover half of a tile (water side in the name)."))

    comp = scene.build(root)
    groups.append(group("Composites (scale 1)", comp, 2, 1,
                        "Stand-in boxes for context only. Night uses additive glow; fire stages sit on 28-px walls."))

    return {"page": "World elements", "section": "Effects & weather", "groups": groups}


if __name__ == "__main__":
    root = DEFAULT_ROOT
    sec = build(root)
    os.makedirs(os.path.join(root, "fx"), exist_ok=True)
    with open(os.path.join(root, "fx", "section.json"), "w") as f:
        json.dump(sec, f, indent=1)
    print("groups:", len(sec["groups"]), "items:", sum(len(g["items"]) for g in sec["groups"]))
