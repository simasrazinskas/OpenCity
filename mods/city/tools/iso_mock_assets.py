"""Asset access for the main-view mockups: every workstream's real art, in day / night / winter.

Each loader returns (img uint8 RGBA, ax, ay) with (ax, ay) = the sprite's ground anchor.
Sources: KIT terrain/nature (isokit, rendered live), NET roads/props (NET's own python, rendered live so
every mask/facing/season exists), ZONED buildings (ZONED's build/render, live), CIVIC buildings (PNGs with
Anchor metadata), LIFE vehicles/people/fx/markers (PNGs; anchors from vehicles/anchors.json, feet at
bottom centre for people, bottom centre for fx and markers).
"""
import functools
import json
import os
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import isokit as ik  # noqa: E402

ISO = os.path.normpath(os.path.join(HERE, "..", "design", "iso"))
VORIG = [(0, 0), (7, 3), (13, 11), (5, 19)]
SIDE = ["E", "N", "W", "S"]


def u8(a):
    return np.clip(np.round(np.asarray(a, np.float32)), 0, 255).astype(np.uint8)


def read_meta(path):
    d = open(path, "rb").read()
    pos, out = 8, {}
    while pos < len(d):
        ln, tag = struct.unpack(">I4s", d[pos:pos + 8])
        if tag == b"tEXt":
            k, v = d[pos + 8:pos + 8 + ln].split(b"\0", 1)
            out[k.decode()] = v.decode()
        if tag == b"IDAT":
            break
        pos += 12 + ln
    return out


@functools.lru_cache(maxsize=None)
def png(rel):
    return ik.read_png(os.path.join(ISO, rel))


@functools.lru_cache(maxsize=None)
def png_anchor(rel):
    m = read_meta(os.path.join(ISO, rel))
    img = png(rel)
    if "Anchor" in m:
        ax, ay = (int(v) for v in m["Anchor"].split(","))
    else:
        ax, ay = img.shape[1] // 2, img.shape[0] - 16
    return img, ax, ay


# ------------------------------------------------------------------ terrain (KIT)
@functools.lru_cache(maxsize=None)
def ground(mat, var, night, season, snow=1.0):
    t = ik.flat_tile(mat, VORIG[var % 4], night=night, season=season, snow=snow)
    return t.img


@functools.lru_cache(maxsize=None)
def field(crop, stage, axis, night, season):
    import iso_terrain_fields as F
    t = ik.flat_tile(F.field_material(crop, stage, axis), night=night, season=season, snow=0.7)
    return t.img


@functools.lru_cache(maxsize=None)
def coast(kind, mask, corners, style, night, season, frame=0):
    import iso_terrain_water as W
    if kind == "water":
        t = W.water_tile(mask, corners, style, frame, "open", night=night)
    else:
        t = W.land_tile(mask, corners, style, season=season, night=night)
    return t.img


@functools.lru_cache(maxsize=None)
def tree(species, season, stage, seed, night, scale=1.0):
    s = ik.tree(species, season if season != "summer" else "summer", stage, seed, scale=scale)
    spr = ik.render(s, night=night, season="winter" if season == "winter" else "summer")
    return spr.img, spr.ax, spr.ay


@functools.lru_cache(maxsize=None)
def nature(kind, season, seed, night):
    import iso_nature_small as N
    s = {"bush": lambda: N.bush(1.0, season, seed), "hedge-x": lambda: N.hedge("x", season, seed),
         "hedge-y": lambda: N.hedge("y", season, seed), "bed": lambda: N.flower_bed(seed=seed, season=season),
         "rock": lambda: N.boulder(1.0, seed, 2), "grass": lambda: N.tall_grass(season, seed),
         "reeds": lambda: N.tall_grass(season, seed, True)}[kind]()
    spr = ik.render(s, night=night, season="winter" if season == "winter" else "summer")
    return spr.img, spr.ax, spr.ay


# ------------------------------------------------------------------ NET (live)
def _net():
    import iso_net_roads as NR
    import iso_net_roadx as RX
    import iso_net_kit as NK
    import iso_net_pal as NP
    return NR, RX, NK, NP


@functools.lru_cache(maxsize=None)
def road(cls, mask, night, season, ctrl=None, seed=0):
    NR, RX, NK, NP = _net()
    extra = RX.control_extra(ctrl) if ctrl else None
    t = NP.quantize(RX.tile(NR.road_scene(cls, mask, False, extra=extra)))
    if season == "winter":
        t = NK.winter_tile(t, seed)
    if night:
        t = NK.night_tile(t)
    return u8(t)


@functools.lru_cache(maxsize=None)
def rail(mask, night, season, crossing=None):
    import iso_net_rail as RL
    _, _, NK, NP = _net()
    t = RL.crossing_tile(crossing == "ns") if crossing else RL.rail_tile(mask)
    t = NP.quantize(t)
    if season == "winter":
        t = NK.winter_tile(t, 3)
    if night:
        t = NK.night_tile(t)
    return u8(t)


@functools.lru_cache(maxsize=None)
def bridge(cls, axis, piece, night, season):
    import iso_net_struct as ST
    _, _, NK, _ = _net()
    img = ST.bridge_piece(cls, axis, piece)
    if season == "winter":
        img = NK.winter_tile(img)
    if night:
        img = ik.nightify(u8(img))
    return u8(img), 32, ST.H + 16


@functools.lru_cache(maxsize=None)
def prop(name, facing, night, season, arg=None):
    import iso_net_props as PR
    import iso_net_bldg as BL
    _, _, NK, _ = _net()
    model = {"lamp": lambda: PR.street_lamp(), "signal": lambda: PR.traffic_signal(arg or "red"),
             "stop": lambda: PR.stop_sign(arg or "stop"), "bus": lambda: PR.bus_shelter("bus"),
             "tram": lambda: PR.bus_shelter("tram"), "metro": lambda: PR.metro_entrance(),
             "furniture": lambda: PR.furniture(arg, season), "lot_cars": lambda: BL.lot_cars(2, arg or 2, seed=3),
             "rail_signal": lambda: BL.rail_signal(arg or "red")}[name]()
    img = NK.render_fixed(model, 64, 96, 32, 80, facing=facing, night=night, season=season)
    return u8(img), 32, 80


@functools.lru_cache(maxsize=None)
def light_pool(side):
    import iso_net_out_props as OP
    return u8(OP.light_pool(side))


@functools.lru_cache(maxsize=None)
def lot_tile(side, night, season):
    import iso_net_out_ground as OG
    _, _, NK, NP = _net()
    t = NP.quantize(OG.lot_tile(side))
    if season == "winter":
        t = NK.winter_tile(t, 5)
    if night:
        t = NK.night_tile(t)
    return u8(t)


@functools.lru_cache(maxsize=None)
def net_png(rel, night=False):
    img = png("net/" + rel)
    return ik.nightify(img) if night else img


# ------------------------------------------------------------------ ZONED (live)
@functools.lru_cache(maxsize=None)
def zoned(prefix, fp, level, var, facing, night, season, state="ok", stage=0):
    import iso_zoned  # noqa: F401  (registers zones)
    import iso_zoned_core as C
    sc = C.build(prefix, fp, level, var, state=state, stage=stage, season=season)
    spr, _ = C.render(sc, facing=facing, night=night, season=season)
    return spr.img, spr.ax, spr.ay


# ------------------------------------------------------------------ CIVIC (PNGs)
def civic(name, night, season, state=None):
    folder = civic_folder(name)
    suf = state or ("night" if night else "winter" if season == "winter" else "day")
    rel = "civic/%s/%s-%s.png" % (folder, name, suf)
    if not os.path.exists(os.path.join(ISO, rel)):
        rel = "civic/%s/%s-day.png" % (folder, name)
    img, ax, ay = png_anchor(rel)
    if night and not rel.endswith("-night.png"):
        img = ik.nightify(img)
    return img, ax, ay


@functools.lru_cache(maxsize=None)
def civic_folder(name):
    for d in sorted(os.listdir(os.path.join(ISO, "civic"))):
        if os.path.exists(os.path.join(ISO, "civic", d, name + "-day.png")):
            return d
    raise KeyError(name)


# ------------------------------------------------------------------ LIFE (PNGs)
@functools.lru_cache(maxsize=None)
def _anchors():
    return json.load(open(os.path.join(ISO, "life", "vehicles", "anchors.json")))


@functools.lru_cache(maxsize=None)
def _vehicle_dir(model):
    for root, dirs, files in os.walk(os.path.join(ISO, "life", "vehicles")):
        if os.path.basename(root) == model and any(f.startswith(model + "-") for f in files):
            return os.path.relpath(root, ISO)
    raise KeyError(model)


@functools.lru_cache(maxsize=None)
def vehicle(model, d, night, colour=None):
    """d: N NE E SE S SW W NW (LIFE world compass)."""
    base = _vehicle_dir(model)
    rel = "%s/%s-%s.png" % (base, model, d)
    if colour:
        crel = "%s/colours/%s-%s-%s.png" % (base, model, colour, d)
        if os.path.exists(os.path.join(ISO, crel)):
            rel = crel
    img = png(rel)
    ax, ay = _anchors()[model]
    beam = None
    if night:
        img = ik.nightify(img)
        lit = os.path.join(ISO, "%s/%s-lit-%s.png" % (base, model, d))
        if os.path.exists(lit):
            l = png(os.path.relpath(lit, ISO))
            m = l[..., 3] > 0
            img = img.copy()
            img[m] = l[m]
        b = os.path.join(ISO, "%s/%s-beam-%s.png" % (base, model, d))
        if os.path.exists(b):
            beam = png(os.path.relpath(b, ISO))
    return img, ax, ay, beam


@functools.lru_cache(maxsize=None)
def person(k, night, d="E"):
    """Walker k heading d (N/E/S/W world compass): LIFE walk frames (adult/child/elder) and variety singles."""
    kind = k % 6
    if kind < 3:
        img = png("life/people/walk/adult_%s_%d.png" % (d, k % 4))
    elif kind == 3:
        img = strip_frame("life/people/walk/child_%s_strip.png" % d, 4, k % 4)
    elif kind == 4:
        img = strip_frame("life/people/walk/elder_%s_strip.png" % d, 4, k % 4)
    else:
        img = png("life/people/variety/single_%d.png" % (k % 6))
    img = np.ascontiguousarray(img)
    if night:
        img = ik.nightify(img)
    return img, img.shape[1] // 2, img.shape[0] - 1


def strip_frame(rel, n, k=0):
    img = png(rel)
    fw = img.shape[1] // n
    return img[:, k * fw:(k + 1) * fw]


def fx(rel, n, k=0):
    f = strip_frame(rel, n, k)
    return f, f.shape[1] // 2, f.shape[0] - 1


def marker(name):
    img = png("life/markers/status/%s.png" % name)
    return img, img.shape[1] // 2, img.shape[0] - 1
