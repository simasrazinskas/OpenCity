"""iso_export_overlays.py - area `overlays` of tools/iso_export.py: ground overlays, networks' props, markers,
info-view ramps, network buildings and transit stops.

    ISO_EXPORT_PROCS=4 python3 mods/city/tools/iso_export.py overlays

Writes bits/world/iso/overlays/*.png and sequences/overlays.yaml (the frame layouts are in that file's header,
generated from the tables below). Everything is re-rendered in-process from the NET and CIVIC generators.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import iso_export_lib as L  # noqa: E402
import iso_net_bldg as B  # noqa: E402
import iso_net_overlays as OV  # noqa: E402
import iso_net_pal as P  # noqa: E402
import iso_net_props as PR  # noqa: E402
import iso_net_rail as RL  # noqa: E402
from iso_net_core import (BAYER4, hx, lerp, render_flat, render_ground, scale,  # noqa: E402
                          tile_grid)
from iso_net_kit import render_fixed  # noqa: E402
from iso_net_paths import paths  # noqa: E402
from iso_net_power import AX as PW_AX, AY as PW_AY, CH as PW_CH, CW as PW_CW, power_tile  # noqa: E402
from iso_net_roads import road_scene  # noqa: E402
from iso_net_roadx import tile as road_tile_of, tram_extra  # noqa: E402

N, E, S, W = 1, 2, 4, 8
SUB = "overlays"          # bits/world/iso/<SUB>/


def fr(arr, ax, ay):
    """float/uint8 RGBA array -> Frame."""
    a = np.asarray(arr)
    if a.dtype != np.uint8:
        a = np.clip(np.rint(a), 0, 255).astype(np.uint8)
    return L.Frame(a, ax, ay)


def ground(arr):
    """NET ground tile (64x32, cell centre = (32, 16)); palette-quantized like the design PNGs."""
    return fr(P.quantize(arr), 32, 16)


def exact(arr):
    """Overlay tile that keeps its exact RGB (zones, markers, info-view ramps: the UI legends match these)."""
    return fr(arr, 32, 16)


# =========================================================================================== 1. utilities
def _corridor(mask):
    """Ground-level footprint of a power line cell: concrete pylon footing + dashed cable-corridor markers
    along every arm (the pylon and the wires themselves are the `utilprops` sprites)."""
    def scene(u, v):
        U, V = u * 64.0, v * 64.0
        n = U.shape[0]
        col = np.zeros((n, 3), np.float32)
        alpha = np.zeros(n, bool)
        raised = np.zeros(n, bool)
        for bit in (N, E, S, W):
            if not mask & bit:
                continue
            along = {N: 32 - V, S: V - 32, E: U - 32, W: 32 - U}[bit]
            across = {N: U - 32, S: U - 32, E: V - 32, W: V - 32}[bit]
            strip = (along > 0) & (np.abs(across) < 2.0) & ((np.floor(along) % 8) < 4)
            col = np.where(strip[:, None], P.GRAVEL_D, col)
            alpha |= strip
        pad = (np.abs(U - 32) < 7) & (np.abs(V - 32) < 7)
        rim = pad & ((np.abs(U - 32) >= 5) | (np.abs(V - 32) >= 5))
        col = np.where(pad[:, None], np.where(rim[:, None], P.KERB, P.CONC), col)
        alpha |= pad
        raised |= pad
        return col, alpha, raised, None, None
    return scene


def _line_tile(m):
    return ground(render_ground(_corridor(m)))


def _pipe_tile(a):
    m, kind = a
    return ground(RL.pipe_tile(m, kind))


def _prop_tile(a):
    m, kind = a
    return fr(P.quantize(power_tile(m, kind)), PW_AX, PW_AY)


# =========================================================================================== 2. zones
ZONE_FRAMES = ["dezone", "res-low", "res-high", "com-low", "com-high", "industrial", "office", "res-row",
               "res-medium", "res-mixed", "res-lowrent", "office-high", "warehouse", "invalid"]
ZONE_COLOUR = {k: c for k, _, c in OV.ZONES}
ZONE_NAMES = ["None (dezone)", "ResidentialLow", "ResidentialHigh", "CommercialLow", "CommercialHigh", "Industrial",
              "Office", "ResidentialRow", "ResidentialMedium", "ResidentialMixed", "ResidentialLowRent",
              "OfficeHigh", "Warehouse", "invalid"]


def _stripe(Q, period, width, centre):
    return OV.stripe(Q, period, width, centre)


def zone_dim_tile(key, active):
    """'Can never take a lot' look: grey wash, the zone colour only in the border, horizontal hatch bands."""
    c = hx(ZONE_COLOUR[key])
    grey = np.array([150, 150, 156], np.float32)
    hatch_col = np.array([70, 70, 80], np.float32)
    bcol = scale(c, 0.72)

    def fn(u, v, px, py):
        U, V = OV.qpos(u, v)
        n = len(u)
        rgb = np.zeros((n, 3), np.float32)
        rgb[:] = grey
        a = OV.thr(px, py) < (0.40 if active else 0.20)
        hatch = _stripe(U + V, 24, 4, 12)
        if not active:
            hatch = hatch & (OV.thr(px, py, 1, 1) < 0.5)
        rgb[hatch] = hatch_col
        a = a | hatch
        e = OV.edge_dist(U, V)
        border = (e >= 2) & (e < 4)
        rgb[border] = bcol
        return rgb, a | border
    return render_flat(fn)


def zone_idle_tile(key, mask, dim=False):
    """Light idle look (grass must dominate): a sparse dot lattice in the zone colour, plus a thin dashed outline
    only on the edges where the neighbour has another zone (mask bit 1 N, 2 E, 4 S, 8 W set = draw that edge).
    dim (a cell that can never take a lot): grey dots and one dashed line through the middle instead."""
    c = hx(ZONE_COLOUR[key])
    dot_col = np.array([90, 90, 100], np.float32) if dim else lerp(c, OV.WHITE, 0.15)
    edge_col = c

    def fn(u, v, px, py):
        U, V = OV.qpos(u, v)
        n = len(u)
        rgb = np.zeros((n, 3), np.float32)
        a = np.zeros(n, bool)
        dots = _stripe(U, 16, 3, 8) & _stripe(V, 16, 3, 8)
        rgb[dots] = dot_col
        a |= dots
        if dim:
            mid = _stripe(U + V, 128, 2, 64) & ((U % 8) < 4)
            rgb[mid] = dot_col
            a |= mid
        for bit, dist, along in ((1, V, U), (2, 63 - U, V), (4, 63 - V, U), (8, U, V)):
            if mask & bit:
                e = (dist < 3) & ((along % 16) < 11)
                rgb[e] = edge_col
                a |= e
        return rgb, a
    return render_flat(fn)


def _zone_idle(a):
    key, mask, dim = a
    return exact(zone_idle_tile(key, mask, dim))


def _zone_tile(a):
    key, active = a
    return exact(OV.zone_tile(key, ZONE_COLOUR[key], active))


def _zone_dim(a):
    key, active = a
    return exact(zone_dim_tile(key, active))


# =========================================================================================== 3. tracks
def _tram_overlay(m):
    """Tram rails + paved inlay as a transparent overlay: the NET avenue tile with the tram minus the plain tile."""
    with_tram = P.quantize(road_tile_of(road_scene("avenue", m, extra=tram_extra)))
    plain = P.quantize(road_tile_of(road_scene("avenue", m)))
    diff = (np.abs(with_tram[..., :3] - plain[..., :3]).sum(-1) > 0) & (with_tram[..., 3] >= 128)
    # keep only the rail and groove pixels (the paved inlay is the same colour as the asphalt after quantizing,
    # except for dither noise at junctions)
    keep = P.quantize(np.array([[[176, 182, 188, 255], [P.IRON[0], P.IRON[1], P.IRON[2], 255]]], np.float32))[0, :, :3]
    diff &= (with_tram[..., None, :3] == keep[None, None]).all(-1).any(-1)
    out = np.zeros_like(with_tram)
    out[diff] = with_tram[diff]
    return ground(out)


def _rail_tile(m):
    if m < 16:
        return ground(RL.rail_tile(m))
    return ground(RL.crossing_tile(m == 17))     # 16 = rail E-W (road N-S), 17 = rail N-S (road E-W)


def _cell_prop(scene, facing=0, night=False, lit_only=False, season="summer"):
    kw = {"facing": facing}
    if night:
        kw["night"] = True
    if season != "summer":
        kw["season"] = season
    return fr(render_fixed(scene, 64, 96, 32, 80, lit_only=lit_only, **kw), 32, 80)


# trackprops layout (see header): name -> list of scene factories
def _trackprop_frames():
    out = {}
    out["catenary"] = [_cell_prop(B.catenary_mast("ns")), _cell_prop(B.catenary_mast("ew"))]
    # buffer stop: model faces the track arm N at facing 0; frames by the OPEN ARM N, E, S, W (see header)
    out["buffer"] = [_cell_prop(B.buffer_stop(), f) for f in (0, 3, 2, 1)]
    # level-crossing gates: frame = lamp phase (0/1) + 2 if the rail runs E-W (model turned by one facing)
    for state, down in (("up", False), ("down", True)):
        out["gate-" + state] = [_cell_prop(B.crossing_gate(down, lamp), f) for f in (0, 1) for lamp in (0, 1)]
        out["gate-%s-lit" % state] = [_cell_prop(B.crossing_gate(down, lamp), f, night=True, lit_only=True)
                                      for f in (0, 1) for lamp in (0, 1)]
    out["signal-red"] = [_cell_prop(B.rail_signal("red"))]
    out["signal-green"] = [_cell_prop(B.rail_signal("green"))]
    out["signal-red-lit"] = [_cell_prop(B.rail_signal("red"), night=True, lit_only=True)]
    out["signal-green-lit"] = [_cell_prop(B.rail_signal("green"), night=True, lit_only=True)]
    return out


# =========================================================================================== 4. extractor areas
AREA_SEQS = [  # sequence, CIVIC model, crop (frames = growth stages), variant list
    ("farm-grain", "area-grain", True),
    ("farm-vegetables", "area-vegetables", True),
    ("farm-livestock", "area-livestock", False),
    ("farm-cotton", "area-cotton", True),
    ("quarry", "area-quarry", False),
    ("mine", "area-mine", False),
    ("oil", "area-oil", False),
    ("fish", "area-fish", False),
    ("forestry", "area-forestry", False),
]


def _area_frames(a):
    seq, model, crop = a
    import iso_civic_models  # noqa: F401  (registers every model)
    import iso_civic_kit as ck
    from iso_civic_states import REG, St, replace
    sp = REG[model]
    ik = ck.ik
    order = [1, 2, 3, 0] if crop else [0, 1, 2, 3]
    day, winter = [], []
    for v in order:
        sc = sp.fn(replace(St(), variant=v))
        s = ik.render(sc)
        day.append(L.frame_from_sprite(s))
        sw = ik.render(sp.fn(replace(St(), variant=v, season="winter")), season="winter")
        winter.append(L.frame_from_sprite(sw))
    return seq, day, winter


# =========================================================================================== 5. markers / ramps
MARKERS = [("valid", "46DC5A"), ("invalid", "E63C32"), ("neutral", "FFFFFF"), ("replace", "4696FF"), ("remove", "FF961E")]
RAMP_KEYS = ["good", "pollution", "blue", "green"]
RESOURCE_STEPS = 11            # value = kind * 16 + step 0..10 (InfoViews.RampColor); frame = kind * 11 + step


def ramp_level_rgb(name, lv):
    return np.rint(OV.ramp_rgb(name, np.array([lv / 100.0]))[0]).astype(int)


def ramp_density(name, lv):
    return float(OV.RAMPS[name][2](lv / 100.0))


def resource_density(step):
    return 0.30 + 0.50 * step / 10.0


def footprint_tile(nx, ny, valid):
    """NET footprint ghost generalised to nx x ny cells ((nx+ny)*32 x (nx+ny)*16, anchor = image centre)."""
    c = hx("46DC5A" if valid else "E63C32")
    fill = lerp(c, OV.WHITE, 0.15)
    edge = lerp(c, OV.WHITE, 0.45)
    cellgrid = lerp(c, OV.WHITE, 0.6)

    def fn(u, v, px, py):
        U, V = np.floor(u * 64).astype(np.int64), np.floor(v * 64).astype(np.int64)
        rgb = np.zeros((len(u), 3), np.float32)
        rgb[:] = fill
        a = OV.thr(px, py) < 0.4
        ib_u = OV.stripe(U, 64, 2, 0) & (U > 4) & (U < nx * 64 - 4)
        ib_v = OV.stripe(V, 64, 2, 0) & (V > 4) & (V < ny * 64 - 4)
        grid = (ib_u | ib_v) & (((px + py) % 2) == 0)
        rgb[grid] = cellgrid
        a = a | grid
        e = OV.edge_dist(U, V, nx, ny)
        s = OV.perimeter_s(U, V, nx, ny)
        dash = (e < 4) & ((s % 16) < 11)
        rgb[dash] = edge
        a = a | dash
        return rgb, a
    img = OV.render_n(fn, nx, ny)
    w, h = (nx + ny) * 32, (nx + ny) * 16
    return fr(img, w // 2, h // 2)


def _footprint(a):
    nx, ny, valid = a
    return footprint_tile(nx, ny, valid)


def marker_frames():
    out = {}
    for key, colour in MARKERS:
        out[key] = [exact(OV.marker_tile(colour))]
    out["bulldoze"] = [exact(OV.bulldoze_tile())]
    out["select"] = [exact(OV.ants_tile(0)), exact(OV.ants_tile(1))]
    out["path"] = [exact(OV.marker_tile("46DC5A", 0.25, arrow=d)) for d in "NESW"]
    out["grid"] = [exact(OV.gridtile())]
    return out


def ramp_frames():
    out = {}
    for key in RAMP_KEYS:
        out["ramp-" + key] = [exact(OV.level_tile(key, lv)) for lv in OV.LEVELS]
    out["ramp-bad"] = list(reversed(out["ramp-good"]))
    out["category"] = [exact(OV.flat_colour_tile(c, 0.6)) for c in OV.CATEGORY]
    res = []
    for _, c in OV.RESOURCES:
        for step in range(RESOURCE_STEPS):
            res.append(exact(OV.flat_colour_tile(c, resource_density(step))))
    out["resource"] = res
    out["heat"] = list(out["ramp-good"])
    return out


# =========================================================================================== 6/7. buildings, stops
BUILDINGS = [("transformer", B.transformer, dict(w=128, h=144, ax=64, ay=112)),
             ("battery", B.battery, dict(w=64, h=96, ax=32, ay=80)),
             ("sewage-outlet", B.sewage_outlet, dict(w=64, h=96, ax=32, ay=80)),
             ("treatment-plant", B.treatment_plant, dict(w=128, h=144, ax=64, ay=112))]


def _building(a):
    name, kind, f = a
    fn, sz = [(fn, sz) for n, fn, sz in BUILDINGS if n == name][0]
    kw = {"facing": f}
    if kind == "lit":
        kw["night"] = True
        kw["lit_only"] = True
    elif kind == "winter":
        kw["season"] = "winter"
    return fr(render_fixed(fn(), sz["w"], sz["h"], sz["ax"], sz["ay"], **kw), sz["ax"], sz["ay"])


# stop side (index into CityUtils.Neighbours4: 0 N, 1 E, 2 S, 3 W) -> isokit facing of the E-side design
STOP_FACING = [1, 0, 3, 2]


def _stop(a):
    kind, side, mode = a
    scene = PR.metro_entrance() if kind == "metro" else PR.bus_shelter(kind)
    f = side if kind == "metro" else STOP_FACING[side]
    return _cell_prop(scene, f, night=(mode == "lit"), lit_only=(mode == "lit"))


# =========================================================================================== header text
def _hex(rgb):
    return "%02X%02X%02X" % tuple(int(x) for x in rgb)


def ramp_table():
    lines = ["Info-view ramps (frame = level step, 0..10 = value 0,10..100). RGB per step (exact tile colour, no palette quantizing;",
             "the dither keeps this fraction of pixels; UI legends must use exactly these colours):"]
    for key in RAMP_KEYS:
        cols = " ".join(_hex(ramp_level_rgb(key, lv)) for lv in OV.LEVELS)
        dens = " ".join("%.2f" % ramp_density(key, lv) for lv in OV.LEVELS)
        lines.append("  ramp-%-9s %s" % (key, cols))
        lines.append("  %-14s density %s" % ("", dens))
    lines.append("  ramp-bad       = ramp-good reversed (frame 0 = green 3FC95A ... 10 = red E54B3C)")
    lines.append("  category (16, 60% dither): " + " ".join(OV.CATEGORY))
    lines.append("  resource hues (kind 0..5): " + " ".join("%s=%s" % (n, c) for n, c in OV.RESOURCES))
    lines.append("  resource density by step 0..10: " + " ".join("%.2f" % resource_density(s) for s in range(RESOURCE_STEPS)))
    return lines


HEADER = """
Ground overlays, network props, markers, info-view ramps, network buildings and transit stops (iso).
Masks: bit 1 = N (-Y, up-right edge), 2 = E (+X, down-right), 4 = S (+Y, down-left), 8 = W (-X, up-left).
All cell overlays are 64x32 diamonds anchored on the cell centre; props and buildings on the ground centre of
their footprint; sequence offsets are 0. Translucency is an ordered (Bayer 4x4) dither because alpha is 0/255.

utilnet (TerrainSpriteLayer, one frame per cell, frame = neighbour mask)
  powerline 16: ground footing of a high-voltage line (concrete pad + dashed corridor markers along the arms)
  pipe-water 16, pipe-sewage 16: underground x-ray trench (shown in the water/sewage/power info views)
utilprops (tall sprites, anchored on the cell centre; UtilityOverlay.GetProps lists them for BLD)
  hv 16: lattice pylon + sagging wires toward the arms, frame = neighbour mask (64x112 canvas, anchor 32,96)
  lv 16: wooden distribution pole + wires, frame = neighbour mask
zon-overlays (ZoneOverlay: frame = (int) ZoneType)
  0 dezone, 1 ResidentialLow, 2 ResidentialHigh, 3 CommercialLow, 4 CommercialHigh, 5 Industrial, 6 Office,
  7 ResidentialRow, 8 ResidentialMedium, 9 ResidentialMixed, 10 ResidentialLowRent, 11 OfficeHigh, 12 Warehouse,
  13 invalid (red X). 14 frames in each sequence:
  zone = active look (zone brush selected), dim = zoned cell that can never take a lot (grey wash + hatch)
  zone-idle, dim-idle: 224 frames each, frame = ZoneType * 16 + edge mask: sparse dot lattice in the zone colour plus a thin
  dashed outline on every edge whose neighbour cell has another zone (mask bit 1 N, 2 E, 4 S, 8 W; off-map counts as other)
tram-track
  track 16: rails + paved inlay as a transparent overlay on the road, frame = neighbour mask
rail
  rail 18: 0-15 neighbour mask, 16 level crossing with E-W rail (road N-S), 17 level crossing with N-S rail
trackprops (cell-centre props; static helper TrackProps in TrackOverlay.cs)
  catenary 2 (0 N-S track, 1 E-W track), buffer 4 (frame = the OPEN arm N,E,S,W: the buffer stands on the opposite end),
  gate-up 4 / gate-down 4 (frame = lamp phase 0/1, +2 when the rail runs E-W; barriers stand at the road edges),
  signal-red 1, signal-green 1, plus -lit companions (gate-up-lit 4, gate-down-lit 4, signal-red-lit 1, signal-green-lit 1)
extractor-area (ExtractorAreaRenderer, 1x1 ground tiles, 4 frames per sequence)
  farm-grain, farm-vegetables, farm-cotton (crops): frame = growth stage 0 ploughed, 1 sprouting, 2 growing, 3 ripe
  farm-livestock, quarry, mine, oil, fish, forestry: frame = variant 0..3 (chosen by cell hash)
  <name>-winter: the same 4 frames in the snow season (crops: fallow field)
overlays (info views, markers, tool ghosts)
  ramp-good, ramp-pollution, ramp-blue, ramp-green, ramp-bad, heat (= ramp-good): 11 frames, frame = level step 0..10
  category 16: frame = category index (districts, road classes)
  resource 66: frame = kind * 11 + step (kind 0..5 yellow, green, blue, coal, stone, cyan; step 0..10)
  valid, invalid, neutral, replace, remove, bulldoze, grid: 1 frame, cell tile
  select 2 (marching ants, alternate at ~4 fps), path 4 (drag path arrow toward N, E, S, W)
  footprint-ok, footprint-bad: 16 frames, frame = (depth - 1) * 4 + (width - 1) for a width (X) x depth (Y) footprint of 1..4
  cells; sprite is (w+d)*32 x (w+d)*16 anchored on the footprint centre.
transit-stop (TransitRender, 1x1 props at the kerb, side = index into CityUtils.Neighbours4: 0 N, 1 E, 2 S, 3 W)
  idle 16: 0-3 bus stop sides N,E,S,W, 4-7 taxi stand N,E,S,W, 8-11 tram stop N,E,S,W, 12-15 metro entrance (isokit facings 0-3)
  idle-lit 16: emissive companion (same layout)
transformer 2x2, battery 1x1, sewage-outlet 1x1, treatment-plant 2x2 (network buildings)
  idle 4 (anim * 4 + facing; facing 0 front +Y, 1 +X, 2 -Y, 3 -X), idle-lit 4, winter 4
"""


def header():
    return HEADER.strip("\n") + "\n" + "\n".join(ramp_table())


# =========================================================================================== export
def sheet(name, frames):
    L.write_sheet("%s/%s.png" % (SUB, name), frames)
    return "iso/%s/%s.png" % (SUB, name)


def export():
    sf = L.SeqFile("overlays.yaml", header())
    n_frames = 0

    # ---- utilities
    line = L.pmap(_line_tile, range(16))
    fn = sheet("utilnet-line", line)
    sf.seq("utilnet", "powerline", fn, 16)
    for kind in ("water", "sewage"):
        fr_ = L.pmap(_pipe_tile, [(m, kind) for m in range(16)])
        sf.seq("utilnet", "pipe-" + kind, sheet("utilnet-" + kind, fr_), 16)
        n_frames += 16
    props = L.pmap(_prop_tile, [(m, "hv") for m in range(16)] + [(m, "lv") for m in range(16)])
    fn = sheet("utilprops", props)
    sf.seq("utilprops", "hv", fn, 16, start=0)
    sf.seq("utilprops", "lv", fn, 16, start=16)
    n_frames += 48

    # ---- zones
    zf = []
    zf += L.pmap(_zone_tile, [(k, True) for k in ZONE_FRAMES])
    zf += L.pmap(_zone_dim, [(k, True) for k in ZONE_FRAMES])
    fn = sheet("zones", zf)
    sf.seq("zon-overlays", "zone", fn, 14, start=0)
    sf.seq("zon-overlays", "dim", fn, 14, start=14)
    idle = []
    for dim in (False, True):
        idle += L.pmap(_zone_idle, [(k, m, dim) for k in ZONE_FRAMES for m in range(16)])
    fn = sheet("zones-idle", idle)
    sf.seq("zon-overlays", "zone-idle", fn, 224, start=0)
    sf.seq("zon-overlays", "dim-idle", fn, 224, start=224)
    n_frames += len(zf) + len(idle)

    # ---- tracks
    tram = L.pmap(_tram_overlay, range(16))
    sf.seq("tram-track", "track", sheet("tram-track", tram), 16)
    rail = L.pmap(_rail_tile, range(18))
    sf.seq("rail", "rail", sheet("rail", rail), 18)
    tp = _trackprop_frames()
    flat, starts = [], {}
    for k, v in tp.items():
        starts[k] = len(flat)
        flat += v
    fn = sheet("trackprops", flat)
    for k, v in tp.items():
        sf.seq("trackprops", k, fn, len(v), start=starts[k])
    n_frames += 34 + len(flat)

    # ---- extractor areas
    areas = L.pmap(_area_frames, AREA_SEQS)
    flat, order = [], []
    for seq, day, winter in areas:
        order.append((seq, len(flat)))
        flat += day
        order.append((seq + "-winter", len(flat)))
        flat += winter
    fn = sheet("areas", flat)
    for seq, st in order:
        sf.seq("extractor-area", seq, fn, 4, start=st)
    n_frames += len(flat)

    # ---- markers and ramps
    mk = marker_frames()
    rp = ramp_frames()
    allf = dict(rp)
    allf.update(mk)
    flat, starts = [], {}
    for k, v in allf.items():
        starts[k] = len(flat)
        flat += v
    fps = []
    for ok in (True, False):
        for d in range(1, 5):
            for w in range(1, 5):
                fps.append((w, d, ok))
    fpf = L.pmap(_footprint, fps)
    starts["footprint-ok"] = len(flat)
    flat += fpf[:16]
    starts["footprint-bad"] = len(flat)
    flat += fpf[16:]
    fn = sheet("overlays", flat)
    lens = {k: len(v) for k, v in allf.items()}
    lens["footprint-ok"] = lens["footprint-bad"] = 16
    for k in starts:
        sf.seq("overlays", k, fn, lens[k], start=starts[k])
    n_frames += len(flat)

    # ---- transit stops
    stops = []
    for kind in ("bus", "taxi", "tram", "metro"):
        stops += [(kind, s, "day") for s in range(4)]
    day = L.pmap(_stop, stops)
    lit = L.pmap(_stop, [(k, s, "lit") for k, s, _ in stops])
    sf.seq("transit-stop", "idle", sheet("stops", day), len(day))
    sf.seq("transit-stop", "idle-lit", sheet("stops-lit", lit), len(lit))
    n_frames += 32

    # ---- network buildings
    for name, _, sz in BUILDINGS:
        for kind in ("day", "lit", "winter"):
            frames = L.pmap(_building, [(name, kind, f) for f in range(4)])
            seqname = {"day": "idle", "lit": "idle-lit", "winter": "winter"}[kind]
            if kind == "lit" and all(f.empty() for f in frames):
                continue
            sf.seq(name, seqname, sheet("%s-%s" % (name, seqname), frames), 4)
            n_frames += 4

    sf.write()
    return "%d frames, %d sequences" % (n_frames, sum(len(d["seqs"]) for d in sf.images.values()))
