"""iso_export_life_vehicles - vehicle sheets for MOV (part of the `life` exporter area).

Re-renders every LIFE vehicle from the generator (iso_life_vehicles / iso_life_render) in 8 facings clockwise from
world north (N = -Y = screen up-right, NE, E = +X = screen down-right, SE, S, SW, W, NW). Anchor = ground centre.
Sheets: bits/world/iso/life/vehicles/<model>[-lit|-beam|-siren|-siren-lit|-beacon|-beacon-lit|-shadow|-rotor].png
Layout is always facing-major: frame = facing * Length + anim (Length 1 for the plain 8-frame sheets).
Small art fixes (IMPLEMENTATION.md decision 14): snowplough shape, cargo plane propellers.
"""
import math

import iso_export_lib as L
from iso_export_lib import Frame
import iso_life_vehicles as V
import iso_life_models as M1
import iso_life_models2 as M2
from iso_life_render import box, cbox, cyl, transform, DIRS
from iso_life_palette import PAL, BODY_COLOURS, with_body

SUB = "life/vehicles/"          # below bits/world/iso/

# model -> category (order = README / yaml order)
ORDER = [
    ("cars", ["hatchback", "sedan", "estate", "suv", "mpv", "pickup", "sports"]),
    ("transit", ["taxi", "bus", "artic-bus-front", "artic-bus-rear"]),
    ("trucks", ["delivery-van", "box-truck", "tanker-truck", "tipper-truck", "timber-truck", "semi-tractor",
                "trailer-box", "trailer-tank", "trailer-container", "trailer-flat", "farm-tractor"]),
    ("services", ["garbage-truck", "fire-engine", "police", "ambulance", "hearse", "postal-van", "maintenance",
                  "snowplough", "wreck"]),
    ("rail", ["tram-cab", "tram-middle", "tram-rear", "train-loco", "train-coach", "wagon-box", "wagon-tank",
              "wagon-hopper", "wagon-container", "wagon-logs", "metro-cab", "metro-car"]),
    ("air", ["heli-medevac", "heli-fire", "cargo-plane"]),
    ("water", ["motorboat", "ferry", "cargo-ship"]),
]
MODELS_ALL = [m for _, ms in ORDER for m in ms]
CATEGORY = {m: c for c, ms in ORDER for m in ms}

# today's image names -> LIFE model (same sheets, same sequences)
LEGACY = {"car-a": "hatchback", "car-b": "sedan", "car-c": "estate", "car-d": "suv", "truck": "box-truck",
          "van": "delivery-van", "garbage": "garbage-truck", "firetruck": "fire-engine", "policecar": "police",
          "tram": "tram-cab", "train": "train-loco", "traincar": "train-coach"}

COLOURED = ["hatchback", "sedan", "estate", "suv", "mpv", "pickup", "sports", "delivery-van"]
COLOUR_NAMES = list(BODY_COLOURS)
SIRENS = V.SIRENS
BEACONS = V.BEACONS
LIFT = V.LIFT            # heli body lift in the design (we export the body at ground level, MOV adds height)
AIR_SHADOW = ["heli-medevac", "heli-fire", "cargo-plane"]
ROTOR_FRAMES = 4
PROP_FRAMES = 3


# ------------------------------------------------------------------------------------------ art fixes
def snowplough():
    """Orange cab-over truck with a salt-spreader body and an angled yellow/black plough blade in front
    (the design version read as a tanker with a stub)."""
    Lm, W = 36, 14
    hl, hw = Lm / 2, W / 2
    tub = box(-hl, hl - 10, -hw + 0.5, hw - 0.5, 3.2, 11.0, "orange")
    tub.cut((0, 1, 0.5), (0, hw - 0.5, 9.0)).cut((0, -1, 0.5), (0, -hw + 0.5, 9.0))
    salt = box(-hl + 1.2, hl - 11.2, -hw + 2.2, hw - 2.2, 10.0, 12.2, "ore")           # salt load
    spread = box(-hl - 2.5, -hl, -3.5, 3.5, 3.4, 6.6, "dkgrey")                         # spinner plate at the back
    frame = box(hl - 1, hl + 4.5, -3.2, 3.2, 3.0, 5.0, "dkgrey")                        # plough mounting arm
    blade = box(-1.2, 1.2, -10.5, 10.5, 1.2, 9.0, "hazard")
    blade_top = box(-1.3, 1.3, -10.6, 10.6, 8.0, 9.4, "orange")
    pl = transform([blade, blade_top], yaw_deg=24, du=hl + 6.5)
    p = M1.rigid_truck(Lm, W, 9, 15, [tub, salt, spread, frame], "orange") + pl
    return p + [cbox(hl - 4.5, 0, 15.0, 2.0, 2.0, 1.3, "beacon")]


def cargo_plane(prop_yaw=0.0):
    """Design plane with bigger propellers (blade 11 long, 2 wide, plus a spinner) so they read at 1x."""
    p = [q for q in M2.cargo_plane(prop_yaw=0.0) if q.mat != "rotor"]
    for v in (-44, -22, 22, 44):
        p.append(cyl(17.8, v, 18.2, 1.8, 1.6, "grey", sides=6, axis="u"))               # spinner
        for k in range(3):
            a = math.radians(prop_yaw + 120 * k)
            c, s = math.cos(a), math.sin(a)
            blade = box(-0.4, 0.4, -0.8, 0.8, 0, 7.0, "rotor")
            hub_v, hub_w = v, 20.0
            blade.planes[:, :3] = [[1, 0, 0], [-1, 0, 0], [0, c, s], [0, -c, -s], [0, -s, c], [0, s, -c]]
            blade.planes[:, 3] = [18.8, -17.4, 1.1 + c * hub_v + s * hub_w, 1.1 - c * hub_v - s * hub_w,
                                  11.0 - s * hub_v + c * hub_w, s * hub_v - c * hub_w]
            p.append(blade)
    return p


V.MODELS["snowplough"] = snowplough          # design generator picks the fixed shape up (in-process only)


def prims_of(name, **kw):
    if name == "cargo-plane":
        return cargo_plane(**kw)
    return V.model_prims(name, **kw)


# ------------------------------------------------------------------------------------------ rendering
def fr(img, size):
    return Frame(img, size[2], size[3])


def render_model(name):
    """All frames of one model, as Frame lists in DIRS order (see keys)."""
    out = {}
    lift = 0.0
    variants = [prims_of(name)]
    if name.startswith("heli-"):
        variants = [prims_of(name, rotor_yaw=22.5 * f) for f in range(ROTOR_FRAMES)]
    if name == "cargo-plane":
        variants = [prims_of(name, prop_yaw=40.0 * f) for f in range(PROP_FRAMES)]
    size = V.frame_box(variants, [V.LIFT.get(name, 0)] if name in AIR_SHADOW else [0])
    if name == "heli-fire":                       # bucket hangs below the ground point
        size = (size[0], size[1] + 14, size[2], size[3])
    prims = variants[0]
    out["size"] = size
    out["day"] = [fr(V.shot(prims, d, size, lift=lift), size) for d in DIRS]
    if name in COLOURED:
        out["colours"] = {c: [fr(V.shot(prims, d, size, with_body(c)), size) for d in DIRS] for c in COLOUR_NAMES}
    if any(p.mat in ("head", "tail") for p in prims):
        ov = [V.night_overlay(prims, d, size) for d in DIRS]
        out["lit"] = [fr(o[0], size) for o in ov]
        if ov[0][1] is not None:
            out["beam"] = [fr(o[1], size) for o in ov]
    if name in SIRENS:
        a, b = SIRENS[name]
        seqs = [V.swap(prims, {"siren_a": a, "siren_b": "off"}), V.swap(prims, {"siren_a": "off", "siren_b": b})]
        out["siren"] = [[fr(V.shot(q, d, size, V.NIGHT_PAL, emissive={a, b}), size) for q in seqs] for d in DIRS]
        out["siren-lit"] = [[fr(V.shot(q, d, size, V.NIGHT_PAL, emissive={a, b}, keep={(a, b)[i]}), size)
                             for i, q in enumerate(seqs)] for d in DIRS]
    if name in BEACONS:
        seqs = [V.swap(prims, {"beacon": "beacon_on"}), V.swap(prims, {"beacon": "off"})]
        out["beacon"] = [[fr(V.shot(q, d, size, V.NIGHT_PAL, emissive={"beacon_on"}), size) for q in seqs] for d in DIRS]
        out["beacon-lit"] = [[fr(V.shot(seqs[0], d, size, V.NIGHT_PAL, emissive={"beacon_on"}, keep={"beacon_on"}),
                                 size)] for d in DIRS]
    if len(variants) > 1:
        out["anim"] = [[fr(V.shot(v, d, size), size) for v in variants] for d in DIRS]
        out["day"] = [a[0] for a in out["anim"]]
    if name in AIR_SHADOW:
        flat = V.flatten(prims)
        sh = [V.dither(V.shot(flat, d, size, PAL, outline=False), 0) for d in DIRS]
        out["shadow"] = [fr(s, size) for s in sh]
    return out


def _render(name):
    return name, render_model(name)


def flat(lists):
    return [f for x in lists for f in x]


class Sheets:
    """Result of the vehicle export: per model the sequences (name -> dict(file, start, length, ...))."""

    def __init__(self):
        self.models = {}       # model -> list of (seq, file, start, length, comment)
        self.size = {}
        self.nframes = 0
        self.written = set()

    def add(self, model, seq, rel, frames, length, start=0, comment=None):
        self.nframes += L.write_sheet(rel, frames) if rel not in self.written else 0
        self.written.add(rel)
        self.models.setdefault(model, []).append((seq, "iso/" + rel, start, length, comment))




def export_vehicles():
    S = Sheets()
    results = dict(L.pmap(_render, MODELS_ALL))
    preview_rows = []
    for name in MODELS_ALL:
        r = results[name]
        base = SUB + name
        S.size[name] = r["size"]
        if "colours" in r:
            frames = flat([r["day"]] + [r["colours"][c] for c in COLOUR_NAMES])
            S.add(name, "idle", base + ".png", frames, 1, 0, "colour 0 = default red; sequences per colour below")
            for i, c in enumerate(COLOUR_NAMES):
                S.models[name].append((c, "iso/" + base + ".png", 8 * (i + 1), 1, None))
        elif "anim" in r:
            n = len(r["anim"][0])
            S.add(name, "idle", base + ".png", flat(r["anim"]), n,
                  comment="%d %s frames per facing" % (n, "rotor" if name.startswith("heli") else "propeller"))
        else:
            S.add(name, "idle", base + ".png", r["day"], 1)
        if "lit" in r:
            S.add(name, "idle-lit", base + "-lit.png", r["lit"], 1, comment="lit lamps only (draw above idle at night)")
        if "beam" in r:
            S.add(name, "beam", base + "-beam.png", r["beam"], 1, comment="dithered road beam, draw additive")
        for k, seq in (("siren", "siren"), ("siren-lit", "siren-lit"), ("beacon", "beacon"), ("beacon-lit", "beacon-lit")):
            if k in r:
                n = len(r[k][0])
                S.add(name, seq, base + "-" + k + ".png", flat(r[k]), n, comment="%d frames per facing" % n)
        if "shadow" in r:
            S.add(name, "shadow", base + "-shadow.png", r["shadow"], 1, comment="ground shadow, draw at ground level")
        preview_rows.append((name, r))
    # contact sheets for review
    for cat, models in ORDER:
        frames = []
        for m in models:
            frames += results[m]["day"]
        L.preview("/tmp/life/prev-veh-%s.png" % cat, frames, cols=8, k=3)
    return S, results
