#!/usr/bin/env python3
"""
iso_mov_sequences - writes sequences/vehicles.yaml and sequences/transit.yaml (MOV) from ART's LIFE export.

Input: design/iso/life-sequences.yaml (written by tools/iso_export.py; sheets in bits/world/iso/life/{vehicles,people,fx}).
- transit.yaml: taxi, buses, trams, trains, wagons, metro (new model names plus the legacy aliases taxi/bus/tram/train/traincar).
- vehicles.yaml: every other vehicle and person image, plus the image `fx` (smoke, fire, water, dust, fireworks, weather,
  pollution, flood, night glows) whose sequence names the render code uses (ServiceEffects, CityWeatherFx, CityAmbientFx).
Deterministic; re-run after every ART re-export:  python3 mods/city/tools/iso_mov_sequences.py
"""
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.normpath(os.path.join(HERE, ".."))
SRC = os.path.join(MOD, "design", "iso", "life-sequences.yaml")
FX_DIR = os.path.join(MOD, "bits", "world", "iso", "life", "fx")

TRANSIT = {"taxi", "bus", "artic-bus-front", "artic-bus-rear", "tram", "tram-cab", "tram-middle", "tram-rear", "train", "traincar",
           "train-loco", "train-coach", "wagon-box", "wagon-tank", "wagon-hopper", "wagon-container", "wagon-logs", "metro-cab",
           "metro-car"}

# fx sequence -> (sheet, start, length, tick ms, additive, anchor note). Anchors are baked into the sheets by ART.
FX = [
    ("smoke-house", "smoke-house", 0, 8, 120, False, "chimney smoke, emitter = bottom centre"),
    ("smoke-stack", "smoke-industry", 0, 8, 120, False, "industrial smoke, emitter = bottom centre"),
    ("smoke-stack-light", "smoke-plume-light", 0, 8, 120, False, "light stack plume, emitter = bottom centre"),
    ("smoke-dark", "smoke-plume", 0, 8, 120, False, "dark plume (fires), emitter = bottom centre"),
    ("steam", "steam", 0, 8, 100, False, "steam puffs, emitter = bottom centre"),
    ("cooling-cloud", "cooling-cloud", 0, 8, 120, False, "cooling tower cloud, emitter = bottom centre"),
    ("fire-1", "fire-stage1", 0, 6, 90, False, "building fire stage 1, anchor = footprint centre"),
    ("fire-2", "fire-stage2", 0, 6, 90, False, "stage 2"),
    ("fire-3", "fire-stage3", 0, 6, 90, False, "stage 3 (roof fire)"),
    ("fire-4", "fire-stage4", 0, 6, 120, False, "stage 4 (burnt out, embers)"),
    ("fire-garbage", "fire-heap", 0, 4, 100, False, "burning garbage heap"),
    ("hose-n", "hose-n", 0, 4, 80, False, "fire hose jet, anchor = nozzle"),
    ("hose-e", "hose-e", 0, 4, 80, False, "fire hose jet, anchor = nozzle"),
    ("hose-s", "hose-s", 0, 4, 80, False, "fire hose jet, anchor = nozzle"),
    ("hose-w", "hose-w", 0, 4, 80, False, "fire hose jet, anchor = nozzle"),
    ("water-splash", "splash", 0, 6, 80, False, "water splash ring, anchor = ring centre"),
    ("heli-drop", "heli-drop", 0, 4, 100, False, "helicopter water drop, anchor = impact point"),
    ("dust", "dust", 0, 6, 100, False, "construction dust, anchor = bottom centre"),
    ("demolition", "demolition", 0, 6, 100, False, "demolition cloud, anchor = bottom centre"),
    ("sparks", "sparks-weld", 0, 4, 70, False, "welding sparks, anchor = centre"),
    ("sparks-short", "sparks-short", 0, 4, 70, False, "short sparks, anchor = centre"),
    ("fireworks-red", "fireworks-red", 0, 8, 100, False, "rocket, burst, sparkles; anchor = launch point"),
    ("fireworks-gold", "fireworks-gold", 0, 8, 100, False, None),
    ("fireworks-green", "fireworks-green", 0, 8, 100, False, None),
    ("fireworks-blue", "fireworks-blue", 0, 8, 100, False, None),
    ("rain", "rain", 0, 3, 0, False, "rain streaks short/medium/long (slant down-left)"),
    ("snow", "rain", 3, 3, 0, False, "snowflakes small/medium/large"),
    ("splash", "rain-splash", 0, 4, 70, False, "rain splash on the ground"),
    ("puddle", "puddles", 0, 3, 0, False, "puddles small/medium/large, anchor = centre"),
    ("fog", "fog", 0, 2, 0, False, "seamless 64x32 fog tiles light/dense"),
    ("cloud-shadow", "cloud-shadow", 0, 2, 0, False, "~200x100 blobs, draw at 35-45 % opacity"),
    ("lightning", "lightning", 0, 3, 60, False, "bolt, anchor = strike point"),
    ("smog", "smog", 0, 3, 0, False, "64x32 smog haze, 3 intensities"),
    ("stain", "stain", 0, 3, 0, False, "64x32 ground stains, 3 levels"),
    ("toxic-sheen", "toxic-sheen", 0, 4, 150, False, "64x32 toxic water sheen"),
    ("noise-rings", "noise-rings", 0, 4, 150, False, "noise rings, anchor = centre"),
    ("dead-grass", "dead-grass", 0, 2, 0, False, "64x32 dead grass"),
    ("flood", "flood-water", 0, 4, 200, False, "64x32 flood water ripple"),
    ("flood-edge", "flood-edges", 0, 4, 0, False, "half-tile edges N, E, S, W"),
    ("pool", "night-pools", 0, 4, 0, True, "light pools small/medium/large/cool, anchor = light source"),
    ("bulb", "night-pools", 4, 1, 0, True, "lamp bulb halo"),
    ("headlight", "night-headlights", 0, 4, 0, True, "44x28 cones N, E, S, W, anchor = headlamp"),
    ("taillight", "night-taillights", 0, 4, 0, True, "tail glows N, E, S, W"),
]


def frame_count(path):
    """Number of Frame[i] tEXt entries of a PngSheet."""
    with open(path, "rb") as f:
        data = f.read()
    pos, n = 8, 0
    while pos < len(data):
        ln, tag = struct.unpack(">I4s", data[pos:pos + 8])
        if tag == b"tEXt" and data[pos + 8:pos + 8 + ln].startswith(b"Frame["):
            n += 1
        pos += 12 + ln
    return n


def blocks(text):
    """Split MiniYaml into (image name, block text) for top-level nodes; comments before a node stay with it."""
    out, cur, name, pending = [], [], None, []
    for line in text.splitlines():
        if line and not line.startswith(("\t", "#")):
            if name is not None:
                out.append((name, "\n".join(cur).rstrip() + "\n"))
            name, cur = line.rstrip(":").strip(), pending + [line]
            pending = []
        elif line.startswith("#") and name is not None and not cur[-1].strip():
            pending.append(line)
        elif name is None:
            continue
        else:
            cur.append(line)
    if name is not None:
        out.append((name, "\n".join(cur).rstrip() + "\n"))
    return out


def fx_block():
    lines = ["fx:"]
    for name, sheet, start, length, tick, additive, note in FX:
        path = os.path.join(FX_DIR, sheet + ".png")
        have = frame_count(path)
        assert start + length <= have, (name, sheet, have)
        if note:
            lines.append("\t# " + note)
        lines.append("\t%s:" % name)
        lines.append("\t\tFilename: iso/life/fx/%s.png" % sheet)
        if start:
            lines.append("\t\tStart: %d" % start)
        lines.append("\t\tLength: %d" % length)
        if tick:
            lines.append("\t\tTick: %d" % tick)
        if additive:
            lines.append("\t\tBlendMode: Additive")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


HEADER = """# {what}
# Generated by tools/iso_mov_sequences.py from design/iso/life-sequences.yaml (ART's export) - do not edit by hand.
# Vehicles: Facings: -8, clockwise from world north (N = -Y = screen up-right, E = +X = screen down-right); frame = facing * Length + anim;
# anchor = ground centre (aircraft: the body; their `shadow` is ground anchored). `idle-lit` = lamps (no night tint), `beam` additive.
"""


def main():
    with open(SRC) as f:
        src = f.read()
    vehicles, transit = [], []
    for name, text in blocks(src):
        # Road beams light the ground: additive (ART's paste-ready file only notes it in a comment).
        text = text.replace("\tbeam:\n", "\tbeam:\n\t\tBlendMode: Additive\n")
        (transit if name in TRANSIT else vehicles).append(text)
    with open(os.path.join(MOD, "sequences", "vehicles.yaml"), "w") as f:
        f.write(HEADER.format(what="Vehicles, people and LIFE effects (MOV)."))
        f.write("\n" + "\n".join(vehicles) + "\n" + fx_block())
    with open(os.path.join(MOD, "sequences", "transit.yaml"), "w") as f:
        f.write(HEADER.format(what="Public transport vehicles (MOV): taxi, buses, trams, trains, wagons, metro."))
        f.write("\n" + "\n".join(transit))
    print("vehicles.yaml: %d images + fx (%d sequences); transit.yaml: %d images" % (len(vehicles), len(FX), len(transit)))


if __name__ == "__main__":
    main()
