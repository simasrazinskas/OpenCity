"""iso_export_civic.py - CIVIC buildings (services, industry hubs, transit buildings) -> game sheets.

Re-renders every model of tools/iso_civic*.py in-process (exact anchors) for all 4 facings and writes
    bits/world/iso/civic/<image>.png        idle, winter, inactive, burnt, construction, fill (one sheet, Start/Length)
    bits/world/iso/civic/<image>-lit.png    idle-lit (emissive pixels of the night render), only when there are any
    sequences/services.yaml, industry.yaml, transit-buildings.yaml   (generated whole)
Frame layouts: see HEADER below and design/iso/EXPORT-LAYOUT.md ("Other buildings").
"""
import os
import sys
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import iso_export_lib as lib  # noqa: E402
from iso_civic_states import REG, St, dim, char_img, construction, height_of, rubble  # noqa: E402
from iso_civic_kit import ik  # noqa: E402
import iso_civic_models  # noqa: E402,F401  (registers every model)

SERVICE_CATS = ("power", "water", "police", "fire", "health", "education", "parks", "garbage", "deathcare",
                "comms", "admin")
FILES = [
    ("services.yaml", SERVICE_CATS, True),
    ("industry.yaml", ("industry",), False),
    ("transit-buildings.yaml", ("transit",), False),
]
TICK = {"windturbine": 90, "powerplant-coal": 160, "powerplant-nuclear": 200, "incinerator": 160,
        "crematorium": 160, "oil-derrick": 120}
DEFAULT_TICK = 120
FILL_LEVELS = (0.0, 0.25, 0.5, 0.75, 1.0)
ZONE_WATER = ("hydro-dam", "cargoharbor", "fish-hub")

HEADER = """{title}
Art: tools/iso_export.py civic (re-rendered from tools/iso_civic*.py). Sheets: bits/world/iso/civic/<image>.png.
Layout per image (A = animation frames, 1 when static; f = facing 0 front +Y, 1 +X, 2 -Y, 3 -X):
  idle          4*A   anim * 4 + f          Tick declared when A > 1 (render trait picks anim * 4 + f)
  idle-lit      4*A   same layout, emissive night pixels only (sheet <image>-lit.png), only when there are any
  winter        4*A   same layout, snow
  inactive      4     f   no power / no water: dimmed, machines and smoke stopped (not for parks, derrick)
  burnt         4     f   charred shell (not for the derrick)
  construction  8     (stage - 1) * 4 + f   stage 1 foundations + rebar, stage 2 concrete frame + crane
  fill          20    level * 4 + f   landfill / cemetery only: levels 0, 25, 50, 75, 100 %
Anchor = footprint ground centre, sequence Offset 0,0. Frames are clipped to the footprint x-extent.
Non-square footprints (3x2, 1x2, 2x4): the models are not parametric, so f 1/3 cannot show the transposed model
inside the same footprint; f 1 = f 0 and f 3 = f 2 in idle/idle-lit/winter/inactive/burnt (construction sites are
parametric and do turn). Square footprints use all four facings.
Buildings tied to water (hydro-dam, cargoharbor, fish-hub): the water side turns with the facing, so the render
trait should pick the facing whose water side faces the water.
{extra}"""

RUBBLE_NOTE = """rubble: 1x1 debris, 4 variants. idle / winter = variant * 4 + f (pick the variant per cell, no animation,
no Tick)."""


# ---------------------------------------------------------------------------------------------- rendering
class _Ctx:
    """Per-building render context: clips frames to the footprint extent and counts clipped pixels."""

    def __init__(self):
        self.clipped = 0
        self.total = 0

    def day(self, spr):
        before = int((spr.img[..., 3] > 0).sum())
        c = ik.clip_footprint(spr)
        self.clipped += before - int((c.img[..., 3] > 0).sum())
        self.total += before
        return c


def _src_facings(fp):
    """Source facing for each of the 4 output facings (non-square footprints cannot turn the model)."""
    return [0, 1, 2, 3] if fp[0] == fp[1] else [0, 0, 2, 2]


def _facing_sprites(make, fp, **kw):
    """4 Sprites for facings 0..3 of make(k) (a Scene), reusing renders for fallback facings."""
    cache = {}
    out = []
    for k in _src_facings(fp):
        if k not in cache:
            cache[k] = ik.render(make(k), facing=k, **kw)
        out.append(cache[k])
    return out


def _var(name, k):
    """Per-facing model variant (only the wind turbine's rotor plane depends on it)."""
    return k % 2 if name == "windturbine" else 0


def _frames(sprs, ctx):
    return [lib.frame_from_sprite(ctx.day(s)) for s in sprs]


def _lit_frames(sprs, ctx):
    return [lib.lit_of(ctx.day(s)) for s in sprs]


def render_building(name):
    sp = REG[name]
    fp = sp.fp
    ctx = _Ctx()
    n = max(1, sp.anim)
    seed = zlib.crc32(name.encode()) % 997
    res = {"name": name, "fp": fp, "anim": n, "idle": [], "lit": [], "winter": [], "inactive": [], "burnt": [],
           "construction": [], "fill": []}
    for a in range(n):
        st = dict(frame=a)
        res["idle"] += _frames(_facing_sprites(lambda k: sp.fn(St(variant=_var(name, k), **st)), fp, frame=a * 2), ctx)
        night = _facing_sprites(lambda k: sp.fn(St(night=True, variant=_var(name, k), **st)), fp, night=True, frame=a * 2)
        res["lit"] += _lit_frames(night, ctx)
        res["winter"] += _frames(_facing_sprites(lambda k: sp.fn(St(season="winter", variant=_var(name, k), **st)), fp,
                                                 season="winter", frame=a * 2), ctx)
    base = _facing_sprites(lambda k: sp.fn(St(inactive=True, variant=_var(name, k))), fp)
    if sp.power:
        res["inactive"] = [lib.frame_from_sprite(ctx.day(ik.Sprite(dim(s.img), s.ax, s.ay, None, s.footprint)))
                           for s in base]
    if sp.build:
        res["burnt"] = [lib.frame_from_sprite(ctx.day(ik.Sprite(char_img(s.img, seed), s.ax, s.ay, None,
                                                                s.footprint))) for s in base]
        day0 = ik.render(sp.fn(St()))
        h = height_of(day0)
        for stage in (1, 2):
            sprs = []
            for k in range(4):
                # a W x D site turned by 90 degrees must be built as D x W to cover W x D again
                sfp = fp if k % 2 == 0 else (fp[1], fp[0])
                sprs.append(ik.render(construction(sfp, h, stage, seed=seed), facing=k))
            res["construction"] += _frames(sprs, ctx)
    if sp.fill:
        for lv in FILL_LEVELS:
            res["fill"] += _frames(_facing_sprites(lambda k: sp.fn(St(fill=lv, variant=_var(name, k))), fp), ctx)
    res["clipped"], res["pixels"] = ctx.clipped, ctx.total
    return res


def render_rubble(_):
    ctx = _Ctx()
    res = {"name": "rubble", "fp": (1, 1), "anim": 1, "idle": [], "lit": [], "winter": [], "inactive": [],
           "burnt": [], "construction": [], "fill": []}
    for v in range(4):
        for season in ("summer", "winter"):
            sprs = [ik.render(rubble(v), facing=k, season=season) for k in range(4)]
            res["idle" if season == "summer" else "winter"] += _frames(sprs, ctx)
        night = [ik.render(rubble(v), facing=k, night=True) for k in range(4)]
        res["lit"] += _lit_frames(night, ctx)
    res["clipped"], res["pixels"] = ctx.clipped, ctx.total
    return res


def _job(name):
    return render_rubble(name) if name == "rubble" else render_building(name)


# ---------------------------------------------------------------------------------------------- export
def _any_lit(frames):
    return any(not f.empty() for f in frames)


def _write_building(res, sf):
    name = res["name"]
    n = res["anim"]
    frames, spans = [], {}
    for key in ("idle", "winter", "inactive", "burnt", "construction", "fill"):
        if res[key]:
            spans[key] = (len(frames), len(res[key]))
            frames += res[key]
    lib.write_sheet("civic/%s.png" % name, frames)
    sheet = "iso/civic/%s.png" % name
    sf.seq(name, "idle", sheet, length=spans["idle"][1], start=spans["idle"][0],
           Tick=(TICK.get(name, DEFAULT_TICK) if n > 1 else None))
    if _any_lit(res["lit"]):
        lib.write_sheet("civic/%s-lit.png" % name, res["lit"])
        sf.seq(name, "idle-lit", "iso/civic/%s-lit.png" % name, length=len(res["lit"]), start=0,
               Tick=(TICK.get(name, DEFAULT_TICK) if n > 1 else None))
    else:
        res["lit"] = []
    for key in ("winter", "inactive", "burnt", "construction", "fill"):
        if key in spans:
            sf.seq(name, key, sheet, length=spans[key][1], start=spans[key][0],
                   Tick=(TICK.get(name, DEFAULT_TICK) if (key == "winter" and n > 1) else None))


def export(only=None, preview=os.environ.get("ISO_CIVIC_PREVIEW")):
    names_by_file = []
    for fname, cats, with_rubble in FILES:
        names = [s.name for s in sorted(REG.values(), key=lambda s: s.order) if s.cat in cats]
        if with_rubble:
            names.append("rubble")
        names_by_file.append((fname, names))
    todo = [n for _, names in names_by_file for n in names if not only or n in only]
    results = dict((r["name"], r) for r in lib.pmap(_job, todo))
    summary = []
    table = []
    for fname, names in names_by_file:
        title = {"services.yaml": "Services, power and water producers, parks, admin, comms, garbage, deathcare "
                                  "(CIVIC).",
                 "industry.yaml": "Industry hubs and the oil derrick (CIVIC). Extractor area tiles: overlays.yaml.",
                 "transit-buildings.yaml": "Transit buildings (CIVIC). Stops, tram track and rail: overlays.yaml."}[fname]
        sf = lib.SeqFile(fname, HEADER.format(title=title, extra=RUBBLE_NOTE if fname == "services.yaml" else ""))
        for name in names:
            if name not in results:
                continue
            res = results[name]
            _write_building(res, sf)
            table.append(res)
        if not only:
            sf.write()
    if preview:
        _preview(results, preview)
    px = sum(r["pixels"] for r in table)
    cl = sum(r["clipped"] for r in table)
    for r in table:
        if r["clipped"] > 0:
            print("  clipped %-20s %5d px of %d" % (r["name"], r["clipped"], r["pixels"]))
    frames = sum(len(r["idle"]) + len(r["winter"]) + len(r["lit"]) + len(r["inactive"]) + len(r["burnt"]) +
                 len(r["construction"]) + len(r["fill"]) for r in table)
    summary.append("%d images, %d frames, %d px clipped of %d" % (len(table), frames, cl, px))
    return "; ".join(summary)


def _preview(results, outdir):
    """Contact sheets: per building idle (anim 0) x 4 facings, lit overlay, winter."""
    os.makedirs(outdir, exist_ok=True)
    for name, r in results.items():
        rows = []
        rows += r["idle"][0::1][:4]
        if r["lit"]:
            rows += _overlay(r["idle"][:4], r["lit"][:4])
        else:
            rows += r["idle"][:4]
        rows += r["winter"][:4]
        lib.preview(os.path.join(outdir, name + ".png"), rows, cols=4, k=2)


def _overlay(day, lit):
    out = []
    for d, l in zip(day, lit):
        img = d.img.copy() // 2
        img[..., 3] = d.img[..., 3]
        if not l.empty():
            # lit frame is tightly cropped with its own anchor: paste aligned on the anchor
            ox, oy = d.ax - l.ax, d.ay - l.ay
            region = img[max(0, oy):oy + l.h, max(0, ox):ox + l.w]
            lp = l.img[max(0, -oy):max(0, -oy) + region.shape[0], max(0, -ox):max(0, -ox) + region.shape[1]]
            m = lp[..., 3] > 0
            region[m] = lp[m]
        f = lib.Frame(img, d.ax, d.ay)
        out.append(f)
    return out


if __name__ == "__main__":
    print(export(only=sys.argv[1:] or None, preview="/tmp/civic-prev"))
