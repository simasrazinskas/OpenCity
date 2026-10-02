"""
iso_life_vehicles - renders every LIFE vehicle in 8 screen directions, its night light overlay, siren/beacon,
rotor and propeller frames, car colour variants and composites; returns the "Vehicles" manifest section.

Output (root = mods/city/design/iso/life):
  vehicles/<cat>/<model>/<model>-<DIR>.png          day sprite, one per direction (same canvas, same anchor)
  vehicles/<cat>/<model>/<model>-8dirs.png          strip N, NE, E, SE, S, SW, W, NW
  vehicles/<cat>/<model>/<model>-lit-<DIR>.png      emissive companion: lit lamps only (engine "-lit" frames)
  vehicles/<cat>/<model>/<model>-beam-<DIR>.png     head-light beam on the road (dithered, draw additive)
Strips have no gaps: slice by frame width.
  vehicles/<cat>/<model>/<model>-<anim>-<DIR>-<f>.png animation frames (siren, beacon, rotor, prop)
  vehicles/anchors.json                              {model: [x, y]} ground-centre anchor inside every frame
Directions are screen directions; the world yaw of each is iso_life_render.DIR_YAW.
"""
import json
import math
import os

import numpy as np

from iso_life_common import OUT, canvas, blit, crop, strip, unify, save_png, make_ramp
from iso_life_render import render, colourise, transform, vertices, DIRS, DIR_YAW, WZ, Prim
from iso_life_palette import PAL, EMIT, BODY_COLOURS, with_body
import iso_life_models as M1
import iso_life_models2 as M2

MODELS = dict(M1.MODELS)
MODELS.update(M2.MODELS)

CATEGORIES = [
    ("cars", "Private cars", ["hatchback", "sedan", "estate", "suv", "mpv", "pickup", "sports"]),
    ("transit", "Taxi & buses", ["taxi", "bus", "artic-bus-front", "artic-bus-rear"]),
    ("trucks", "Trucks", ["delivery-van", "box-truck", "tanker-truck", "tipper-truck", "timber-truck",
                          "semi-tractor", "trailer-box", "trailer-tank", "trailer-container", "trailer-flat",
                          "farm-tractor"]),
    ("services", "Service vehicles", ["garbage-truck", "fire-engine", "police", "ambulance", "hearse",
                                      "postal-van", "maintenance", "snowplough", "wreck"]),
    ("rail", "Trams, trains & metro", ["tram-cab", "tram-middle", "tram-rear", "train-loco", "train-coach",
                                       "wagon-box", "wagon-tank", "wagon-hopper", "wagon-container", "wagon-logs",
                                       "metro-cab", "metro-car"]),
    ("air", "Aircraft", ["heli-medevac", "heli-fire", "cargo-plane"]),
    ("water", "Boats & ships", ["motorboat", "ferry", "cargo-ship"]),
]

SIRENS = {"police": ("siren_r", "siren_bl"), "ambulance": ("siren_r", "siren_bl"), "fire-engine": ("siren_r", "siren_r")}
BEACONS = ["garbage-truck", "maintenance", "snowplough"]
LIFT = {"heli-medevac": 26, "heli-fire": 34}

NIGHT_PAL = dict(PAL)
ANCHORS = {}


def frame_box(prims_list, lifts):
    """Canvas size and anchor that fit every direction of every variant."""
    R, Z = 1.0, 1.0
    for prims in prims_list:
        for p in prims:
            V = vertices(p)
            if len(V):
                R = max(R, float(np.max(np.hypot(V[:, 0], V[:, 1]))))
                Z = max(Z, float(V[:, 2].max()))
    lift = max(lifts) if lifts else 0
    w = 2 * int(math.ceil(0.7072 * R)) + 6                 # even
    top = int(math.ceil(0.3536 * R + Z * WZ + lift)) + 3
    bottom = int(math.ceil(0.3536 * R)) + 3
    bottom += (top + bottom) % 2                            # even height
    return w, top + bottom, w // 2, top


def shot(prims, d, size, pal=PAL, emissive=(), lift=0.0, keep=None, outline=True):
    w, h, ax, ay = size
    m, s, mats = render(prims, DIR_YAW[d], w, h, ax, ay, lift)
    img = colourise(m, s, mats, pal, emissive=emissive, outline=outline and keep is None)
    if keep is not None:                       # overlay: keep only some materials
        sel = np.isin(m, [i for i, k in enumerate(mats) if k in keep])
        img[~sel] = 0
    return img


def swap(prims, mapping):
    out = []
    for p in prims:
        q = Prim(p.planes, mapping.get(p.mat, p.mat), {k: mapping.get(v, v) for k, v in p.facemats.items()})
        out.append(q)
    return out


def flatten(prims, k=0.02):
    out = []
    for p in prims:
        q = Prim(p.planes.copy(), "shadow")
        q.planes[:, 2] = q.planes[:, 2] / k
        out.append(q)
    return out


def beams(prims):
    """Head-light beam prims on the ground ahead of the vehicle (only for vehicles with head lamps)."""
    heads = [p for p in prims if p.mat == "head"]
    if not heads:
        return []
    V = np.vstack([vertices(p) for p in heads])
    front = V[:, 0].max()
    hw = np.abs(V[:, 1]).max() + 1
    b1 = Prim([[1, 0, 0, front + 13], [-1, 0, 0, -front - 0.5], [0, 1, 0.0, hw + 3], [0, -1, 0, hw + 3],
               [0, 0, 1, 0.25], [0, 0, -1, 0]], "beam1")
    b2 = Prim([[1, 0, 0, front + 26], [-1, 0, 0, -front - 13], [0, 1, 0.0, hw + 7], [0, -1, 0, hw + 7],
               [0, 0, 1, 0.25], [0, 0, -1, 0]], "beam2")
    for b in (b1, b2):   # widen with distance
        b.planes[2] = [-0.35, 1, 0, hw + 3 - 0.35 * front]
        b.planes[3] = [-0.35, -1, 0, hw + 3 - 0.35 * front]
    return [b1, b2]


def dither(img, keep_parity):
    """Checkerboard-dither an overlay layer (alpha 0/255 falloff)."""
    yy, xx = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    img[((xx + yy) % 2) != keep_parity] = 0
    return img


def night_overlay(prims, d, size, lift=0.0):
    """Returns (lit, beam): lit lamp pixels (opaque, skip the night multiply) and the dithered road beam."""
    lit = swap(prims, {"head": "head_on", "tail": "tail_on", "dest": "dest_on"})
    lit = swap(lit, {"glass": "occ", "siren_a": "occ", "siren_b": "occ", "beacon": "occ"})
    pal = dict(NIGHT_PAL)
    keep = {"head_on", "tail_on", "dest_on"}
    lamps = shot(lit, d, size, pal, emissive=keep, lift=lift, keep=keep)
    bm = beams(prims)
    if not bm:
        return lamps, None
    occl = swap(prims, {}) + bm
    near = dither(shot(occl, d, size, pal, lift=lift, keep={"beam1"}), 0)
    far = shot(occl, d, size, pal, lift=lift, keep={"beam2"})
    yy, xx = np.mgrid[0:far.shape[0], 0:far.shape[1]]
    far[((xx % 2) == 1) | (((xx // 2 + yy) % 2) == 1)] = 0          # 25 % ordered pattern
    blit(near, far, 0, 0)
    return lamps, near


def model_prims(name, **kw):
    if name.startswith("heli-"):
        return M2.heli("white", "red", **kw) if name == "heli-medevac" else M2.heli("fire", "hazard", bucket=True, **kw)
    if name == "cargo-plane":
        return M2.cargo_plane(**kw)
    return MODELS[name]()


def rel(cat, name, stem):
    return "vehicles/%s/%s/%s.png" % (cat, name, stem)


def save(root, path, img):
    save_png(os.path.join(root, path), img)
    return path


def render_model(root, cat, name, size, prims, lift=0.0):
    """Day frames + 8-dir strip + night overlays. Returns (strip path, frame paths, day frames)."""
    frames, files = [], []
    for d in DIRS:
        img = shot(prims, d, size, lift=lift)
        frames.append(img)
        files.append(save(root, rel(cat, name, "%s-%s" % (name, d)), img))
    st = save(root, rel(cat, name, name + "-8dirs"), strip(frames, 0))
    lights = None
    if any(p.mat in ("head", "tail") for p in prims):
        ov = [night_overlay(prims, d, size, lift) for d in DIRS]
        for d, (lit, beam) in zip(DIRS, ov):
            save(root, rel(cat, name, "%s-lit-%s" % (name, d)), lit)
            if beam is not None:
                save(root, rel(cat, name, "%s-beam-%s" % (name, d)), beam)
        lights = [save(root, rel(cat, name, name + "-lit-8dirs"), strip([o[0] for o in ov], 0))]
        if ov[0][1] is not None:
            lights.append(save(root, rel(cat, name, name + "-beam-8dirs"), strip([o[1] for o in ov], 0)))
    return st, files, frames, lights


def night_look(day, overlay, bg="#23252e"):
    """Preview of the night look: day sprite darkened by the night tint, plus the additive overlay."""
    h, w = day.shape[:2]
    out = np.zeros((h, w, 4), np.uint8)
    out[..., :3] = np.array([int(bg[i:i + 2], 16) for i in (1, 3, 5)])
    out[..., 3] = 255
    m = day[..., 3] > 0
    out[m, :3] = (day[m, :3] * np.array([0.34, 0.38, 0.56])).astype(np.uint8)
    if overlay is not None:
        o = overlay[..., 3] > 0
        out[o, :3] = np.clip(out[o, :3].astype(int) + overlay[o, :3].astype(int) * 0.85, 0, 255).astype(np.uint8)
    return out


def item(path, label):
    return {"file": path, "label": label}


def build(root=OUT):
    groups = []
    showcase = {}          # model -> (day frames, overlay frames) for composites
    for cat, title, names in CATEGORIES:
        strips, lights, hero, anims = [], [], [], []
        for name in names:
            lift = LIFT.get(name, 0)
            variants = [model_prims(name)]
            if name.startswith("heli-"):
                variants = [model_prims(name, rotor_yaw=22.5 * f) for f in range(4)]
            if name == "cargo-plane":
                variants = [model_prims(name, prop_yaw=40.0 * f) for f in range(3)]
            size = frame_box(variants, [lift])
            ANCHORS[name] = [size[2], size[3]]
            st, files, frames, lt = render_model(root, cat, name, size, variants[0], lift)
            showcase[name] = (frames, lt, size)
            strips.append(item(st, name + " 8 dirs"))
            if lt:
                lights.append(item(lt[0], name + " lit"))
                if len(lt) > 1:
                    lights.append(item(lt[1], name + " beam"))
            if name in ("sedan", "bus", "train-loco", "heli-medevac", "cargo-ship"):
                hero.append({"title": "%s: %s in 8 directions" % (title, name), "columns": 8, "scale": 2,
                             "note": "World compass, clockwise: N (screen up-right), NE, E (down-right), SE, "
                                     "S (down-left), SW, W (up-left), NW. Anchor = ground centre, see anchors.json",
                             "items": [item(f, d) for f, d in zip(files, DIRS)]})
            anims += extra_anims(root, cat, name, size, variants, lift)
        groups += hero
        if cat == "cars":
            groups += colour_groups(root)
        groups.append({"title": title + ": all models, 8 directions", "columns": 1, "scale": 2,
                       "note": "strip order N, NE, E, SE, S, SW, W, NW (world compass); no gaps, slice by frame width",
                       "items": strips})
        groups += anims
        if lights:
            groups.append({"title": title + ": night lights (lit lamps, road beams)", "columns": 2, "scale": 2,
                           "note": "lit = emissive companion frames (no night multiply); beam = dithered road light, additive",
                           "items": lights})
    groups[0:0] = composites(root, showcase)
    with open(os.path.join(root, "vehicles", "anchors.json"), "w") as f:
        json.dump(ANCHORS, f, indent=1, sort_keys=True)
    return {"page": "World elements", "section": "Vehicles", "groups": groups}


def colour_groups(root):
    items = []
    for name in ["hatchback", "sedan", "estate", "suv", "mpv", "pickup", "sports"]:
        prims = MODELS[name]()
        size = frame_box([prims], [])
        row = []
        for cname in BODY_COLOURS:
            pal = with_body(cname)
            frames = [shot(prims, d, size, pal) for d in DIRS]
            for d, f in zip(DIRS, frames):
                save(root, rel("cars", name, "colours/%s-%s-%s" % (name, cname, d)), f)
            save(root, rel("cars", name, "colours/%s-%s-8dirs" % (name, cname)), strip(frames, 0))
            row.append(frames[4])                                   # S (screen down-left)
        items.append(item(save(root, rel("cars", name, name + "-colours-S"), strip(row, 2)),
                          "%s %d colours" % (name, len(row))))
    return [{"title": "Private cars: body colours (S shown, 8 dirs each on disk)", "columns": 1, "scale": 2,
             "note": " / ".join(BODY_COLOURS), "items": items}]


def extra_anims(root, cat, name, size, variants, lift):
    groups = []
    prims = variants[0]
    if name in SIRENS:
        a, b = SIRENS[name]
        seqs = [swap(prims, {"siren_a": a, "siren_b": "off"}), swap(prims, {"siren_a": "off", "siren_b": b})]
        per_dir = []
        for d in DIRS:
            fr = [shot(q, d, size, NIGHT_PAL, emissive={a, b}, lift=lift) for q in seqs]
            for i, f in enumerate(fr):
                save(root, rel(cat, name, "siren/%s-siren-%s-%d" % (name, d, i)), f)
            lit = [shot(q, d, size, NIGHT_PAL, emissive={a, b}, lift=lift, keep={(a, b)[i]}) for i, q in enumerate(seqs)]
            for i, f in enumerate(lit):
                save(root, rel(cat, name, "siren/%s-siren-lit-%s-%d" % (name, d, i)), f)
            save(root, rel(cat, name, "siren/%s-siren-lit-%s" % (name, d)), strip(lit, 0))
            per_dir.append(save(root, rel(cat, name, "siren/%s-siren-%s" % (name, d)), strip(fr, 0)))
        groups.append({"title": "%s: siren light bar" % name, "columns": 8, "scale": 2,
                       "note": "2 frames per direction, alternate every ~8 ticks; -siren-lit-* companions hold the lit lamps only",
                       "items": [item(p, "%s 2 frames" % d) for p, d in zip(per_dir, DIRS)]})
    if name in BEACONS:
        seqs = [swap(prims, {"beacon": "beacon_on"}), swap(prims, {"beacon": "off"})]
        fr = [shot(q, "S", size, NIGHT_PAL, emissive={"beacon_on"}) for q in seqs]
        for d in DIRS:
            for i, q in enumerate(seqs):
                save(root, rel(cat, name, "beacon/%s-beacon-%s-%d" % (name, d, i)),
                     shot(q, d, size, NIGHT_PAL, emissive={"beacon_on"}))
            save(root, rel(cat, name, "beacon/%s-beacon-lit-%s" % (name, d)),
                 shot(seqs[0], d, size, NIGHT_PAL, emissive={"beacon_on"}, keep={"beacon_on"}))
        groups.append({"title": "%s: amber beacon" % name, "columns": 2, "scale": 2,
                       "items": [item(save(root, rel(cat, name, "beacon/%s-beacon-S" % name), strip(fr, 0)), "S 2 frames")]})
    if len(variants) > 1:
        kind = "rotor" if name.startswith("heli-") else "prop"
        rows = []
        for d in DIRS:
            fr = [shot(v, d, size, lift=lift) for v in variants]
            for i, f in enumerate(fr):
                save(root, rel(cat, name, "%s/%s-%s-%s-%d" % (kind, name, kind, d, i)), f)
            rows.append(item(save(root, rel(cat, name, "%s/%s-%s-%s" % (kind, name, kind, d)), strip(fr, 0)),
                             "%s %d frames" % (d, len(fr))))
        groups.append({"title": "%s: %s animation" % (name, kind), "columns": 4, "scale": 2, "items": rows})
    if name in LIFT or name == "cargo-plane":
        flat = flatten(prims)
        sh = [dither(shot(flat, d, size, PAL, outline=False), 0) for d in DIRS]
        for d, f in zip(DIRS, sh):
            save(root, rel(cat, name, "%s-shadow-%s" % (name, d)), f)
        groups.append({"title": "%s: ground shadow (drawn %d px below the flying body)" % (name, LIFT.get(name, 40)),
                       "columns": 1, "scale": 2,
                       "items": [item(save(root, rel(cat, name, name + "-shadow-8dirs"), strip(sh, 0)), "8 dirs")]})
    return groups


CONSISTS = [
    ("tram", [("tram-cab", 36), ("tram-middle", 30), ("tram-rear", 36)], 1.5),
    ("train", [("train-loco", 46), ("train-coach", 50), ("train-coach", 50)], 2.0),
    ("freight", [("train-loco", 46), ("wagon-box", 42), ("wagon-tank", 42), ("wagon-hopper", 42),
                 ("wagon-container", 42), ("wagon-logs", 42)], 2.0),
    ("metro", [("metro-cab", 46), ("metro-car", 46), ("metro-cab-rev", 46)], 1.5),
    ("artic-bus", [("artic-bus-front", 40), ("artic-bus-rear", 40)], 2.5),
    ("semi", [("semi-tractor", 20), ("trailer-container", 44)], -15.0),
]


def consist_prims(parts, gap):
    prims, u = [], 0.0
    for i, (name, L) in enumerate(parts):
        if i:
            u -= L / 2
        q = transform(MODELS["metro-cab"](), yaw_deg=180) if name == "metro-cab-rev" else MODELS[name]()
        prims += transform(q, du=u)
        u -= L / 2 + gap
    total = -u
    return transform(prims, du=total / 2 - parts[0][1] / 2), total


def ground(w, h, ax, ay, fn):
    """Paint a ground layer: fn(x, y) in cells -> RGB array (vectorised) for each pixel at z = 0."""
    yy, xx = np.mgrid[0:h, 0:w]
    sx, sy = xx + 0.5 - ax, yy + 0.5 - ay
    x = (sy / 16 + sx / 32) / 2
    y = (sy / 16 - sx / 32) / 2
    img = np.zeros((h, w, 4), np.uint8)
    img[..., :3] = fn(x, y)
    img[..., 3] = 255
    return img


def street_x(x, y):
    """A simple 2-lane street along the world X axis at y in [0, 1] (centre y = 0.5) for lane-fit checks."""
    off = (y - 0.5) * 64                   # units across the street
    c = np.zeros(x.shape + (3,), np.uint8)
    c[:] = (96, 140, 72)                                   # grass
    c[np.abs(off) <= 32] = (176, 172, 162)                 # pavement
    c[np.abs(off) <= 22] = (84, 86, 92)                    # asphalt (lanes 0..22 each way)
    dash = (np.abs(off) <= 0.7) & ((np.floor(x * 8) % 2) == 0)
    c[dash] = (230, 226, 200)
    edge = (np.abs(np.abs(off) - 22) <= 0.6)
    c[edge] = (140, 138, 132)
    return c


ISO = os.path.normpath(os.path.join(OUT, ".."))
LANE_Q = 10            # NET contract: street lane centre, q (1/64 cell) right of the centreline
WALK_Q = 26            # sidewalk centre of a street (sidewalk = outer 12 q of the cell)


def net_ground(W, H, ax, ay, ncells=4, street_row=0, rows=(-1, 0, 1)):
    """Ground from NET's street tiles (E+W mask 0101) and KIT's grass; falls back to the flat stand-in."""
    from iso_life_common import load_png
    road = os.path.join(ISO, "net", "roads", "street", "street-0101.png")
    grass = os.path.join(ISO, "terrain", "ground", "grass-1.png")
    if not (os.path.exists(road) and os.path.exists(grass)):
        return ground(W, H, ax, ay, street_x)
    tiles = {"road": load_png(road), "grass": load_png(grass)}
    img = canvas(W, H)
    img[..., :3] = (96, 140, 72)
    img[..., 3] = 255
    for cy in rows:
        for cx in range(-1, ncells + 1):
            t = tiles["road" if cy == street_row else "grass"]
            sx = ax + (cx - cy) * 32 - 32
            sy = ay + (cx + cy) * 16
            blit(img, t, int(sx), int(sy))
    return img


def place(scene, sprite, anchor, wx, wy, ax, ay):
    sx = int(round((wx - wy) * 32 + ax)) - anchor[0]
    sy = int(round((wx + wy) * 16 + ay)) - anchor[1]
    blit(scene, sprite, sx, sy)


def composites(root, showcase):
    groups = []
    # consists
    items = []
    for cname, parts, gap in CONSISTS:
        prims, total = consist_prims(parts, gap)
        size = frame_box([prims], [])
        fr = unify([crop(shot(prims, d, size))[0] for d in ("E", "S", "NE")])
        items.append(item(save(root, "vehicles/composites/consist-%s.png" % cname, strip(fr, 6)),
                          "%s (%d units)" % (cname, round(total))))
    groups.append({"title": "Multi-segment vehicles assembled (E, S, NE)", "columns": 1, "scale": 2,
                   "note": "segments are separate sprites spaced by length + coupler gap", "items": items})
    # lane fit: 4 cells of street along X; cars heading +X in the right lane (+y side), -X in the other
    W, H, ax, ay = 300, 170, 150, 40
    cars = [("sedan", 0.6, 1, "E", "blue"), ("suv", 1.25, 1, "E", "silver"), ("bus", 2.4, 1, "E", None),
            ("hatchback", 3.4, 1, "E", "green"), ("taxi", 0.9, -1, "W", None), ("box-truck", 2.0, -1, "W", None),
            ("police", 3.2, -1, "W", None), ("estate", 0.2, -1, "W", "white"), ("sports", 3.8, -1, "W", "yellow")]
    draw = []
    for name, along, side, d, col in cars:
        prims = MODELS[name]()
        size = showcase[name][2]
        pal = with_body(col) if col else PAL
        day = shot(prims, d, size, pal)
        lit, beam = night_overlay(prims, d, size)
        ov = beam if beam is not None else canvas(*lit.shape[1::-1])
        blit(ov, lit, 0, 0)
        wy = 0.5 + side * LANE_Q / 64.0
        draw.append((along + wy, along, wy, day, ov, size))
    draw.sort(key=lambda t: t[0])
    for night in (False, True):
        scene = net_ground(W, H, ax, ay)
        if night:
            scene[..., :3] = (scene[..., :3] * np.array([0.34, 0.38, 0.56])).astype(np.uint8)
        for _, along, wy, day, ov, size in draw:
            spr = day
            if night:
                spr = night_look(day, None)
                spr[day[..., 3] == 0, 3] = 0
            place(scene, spr, (size[2], size[3]), along, wy, ax, ay)
        if night:
            for _, along, wy, day, ov, size in draw:
                layer = canvas(W, H)
                place(layer, ov, (size[2], size[3]), along, wy, ax, ay)
                o = layer[..., 3] > 0
                scene[o, :3] = np.clip(scene[o, :3].astype(int) + layer[o, :3].astype(int), 0, 255).astype(np.uint8)
        name = "street-night" if night else "lane-fit"
        groups.append({"title": "In context: 2-lane street, right-hand traffic" + (" at night" if night else ""),
                       "columns": 1, "scale": 2,
                       "note": "NET street tiles; lane centre 10 q (1/64 cell) right of the centreline (NET contract)",
                       "items": [item(save(root, "vehicles/composites/%s.png" % name, crop(scene)[0]), name)]})
    # size line-up
    names = ["sports", "sedan", "suv", "delivery-van", "garbage-truck", "fire-engine", "bus", "tram-cab", "heli-medevac"]
    row = [crop(showcase[n][0][4])[0] for n in names]
    h = max(r.shape[0] for r in row)
    line = canvas(sum(r.shape[1] + 4 for r in row), h)
    x = 0
    for r in row:
        blit(line, r, x, h - r.shape[0])
        x += r.shape[1] + 4
    groups.append({"title": "Size line-up (S)", "columns": 1, "scale": 2,
                   "items": [item(save(root, "vehicles/composites/lineup.png", line), " / ".join(names))]})
    return groups


if __name__ == "__main__":
    sec = build()
    with open(os.path.join(OUT, "vehicles", "section.json"), "w") as f:
        json.dump(sec, f, indent=1)
    print(sum(len(g["items"]) for g in sec["groups"]), "items in", len(sec["groups"]), "groups")
