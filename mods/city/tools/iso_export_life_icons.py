"""iso_export_life_icons - status icons, fire, garbage heaps and helicopter for sequences/misc.yaml (part of `life`).

envicons: `tiles` = 8 empty thought bubbles, one per ProblemTier (Minimal grey, Info, Problem, Warning, Major, Error,
Fatal, Good green); `glyphs` = 24 glyph layers, frame = CityProblem - 1. Tile and glyph frames share one anchor (the
glyph centre inside the bubble body), so UISpriteRenderable draws the glyph exactly inside the bubble when both are
placed at the same centre (the sprite offsets carry the rest). `extras` = 9 more glyph layers (no CityProblem yet).
"""
import math
import os

import numpy as np

import iso_export_lib as L
from iso_export_lib import Frame
import iso_life_mbubble as B
import iso_life_mglyphs_b as GB
from iso_life_mglyphs_a import GLYPHS
from iso_life_mdraw import add_outline, col
from iso_life_common import canvas, blit, load_png
import iso_life_fx_util as U
from iso_life_fx_fire import HEAP

SUB = "life/icons/"
TIERS = ["minimal", "info", "problem", "warning", "major", "error", "fatal", "good"]    # = ProblemTier order
B.TIERS["minimal"] = "#8e929c"
ANCHOR = (11, 11)           # glyph centre inside the 22 x 27 bubble canvas


def bubble_parts(glyph, tier):
    empty = B.bubble(canvas(1, 1), tier)
    full = B.bubble(glyph, tier)
    diff = np.any(full != empty, axis=-1) & (full[..., 3] > 0)
    layer = np.zeros_like(full)
    layer[diff] = full[diff]
    return empty, layer


def export_icons():
    """Returns {seq: (rel, frames)} for the envicons image."""
    out = {}
    probe = GB.PROBLEMS[0][0]
    tiles = [Frame(bubble_parts(GLYPHS[probe](), t)[0], *ANCHOR) for t in TIERS]
    L.write_sheet(SUB + "problem-tiles.png", tiles)
    glyphs = [Frame(bubble_parts(GLYPHS[n](), "warning")[1], *ANCHOR) for n, _ in GB.PROBLEMS]
    L.write_sheet(SUB + "problem-glyphs.png", glyphs)
    extras = [Frame(bubble_parts(GLYPHS[n](), "warning")[1], *ANCHOR) for n, _ in GB.EXTRAS]
    L.write_sheet(SUB + "problem-extras.png", extras)
    out["tiles"] = ("iso/" + SUB + "problem-tiles.png", len(tiles))
    out["glyphs"] = ("iso/" + SUB + "problem-glyphs.png", len(glyphs))
    out["extras"] = ("iso/" + SUB + "problem-extras.png", len(extras))
    L.preview("/tmp/life/prev-icons.png", tiles + glyphs + extras, cols=8, k=3)
    return out


# ------------------------------------------------------------------------------------------ fire and heaps
DESIGN_FX = os.path.join(L.DESIGN, "life", "fx")


def design_frames(rel_base, n, anchor="bottom"):
    fr = []
    for i in range(n):
        img = load_png(os.path.join(DESIGN_FX, "%s_%d.png" % (rel_base, i)))
        h, w = img.shape[:2]
        fr.append(Frame(img, w // 2, h - 1 if anchor == "bottom" else h // 2))
    return fr


def heap_frame(size):
    """Garbage heap (no fire): bumpy dark mound with specks of bags and cans; anchor = ground centre of the heap."""
    W, H = {1: (18, 12), 2: (26, 16), 3: (36, 22)}[size]
    img = canvas(W, H)
    cx, rx, hh = W // 2, W // 2 - 2, H - 5
    for y in range(H):
        for x in range(W):
            dx = (x - cx) / float(rx)
            if abs(dx) > 1:
                continue
            top = H - 3 - hh * (1 - dx * dx) ** 0.8 + 1.1 * math.sin(x * 1.9 + size)
            base = H - 3 + 2.0 * (1 - dx * dx) ** 0.5
            if top <= y <= base:
                t = 0.30 + 0.5 * ((H - 3 - y) / float(hh)) - 0.22 * dx
                U.px(img, x, y, U.tone(HEAP, t, x, y))
    rng = [(-0.45, 0.55, "#9aa0a6"), (0.25, 0.7, "#c8c0a0"), (-0.1, 0.4, "#5a7aa0"), (0.5, 0.35, "#a04a3a"),
           (-0.6, 0.25, "#d8d8d0"), (0.05, 0.8, "#6a9a5a"), (0.4, 0.62, "#e0b030")]
    for k, (fx, fy, c) in enumerate(rng[:2 + size * 2]):
        x = int(round(cx + fx * rx))
        y = int(round(H - 3 - fy * hh))
        U.px(img, x, y, col(c)[:3])
    return Frame(img, cx, H - 3)


def export_fire_piles():
    res = {}
    names = ["stage1", "stage2", "stage3", "stage4"]
    frames, starts = [], {}
    for n in names:
        starts[n] = len(frames)
        frames += design_frames("fire/" + n, 6)
    starts["heap"] = len(frames)
    frames += design_frames("fire/garbage", 4)
    L.write_sheet(SUB + "fire.png", frames)
    res["fire"] = ("iso/" + SUB + "fire.png", starts)
    heaps = [heap_frame(s) for s in (1, 2, 3)]
    L.write_sheet(SUB + "pile.png", heaps)
    res["pile"] = ("iso/" + SUB + "pile.png", len(heaps))
    L.preview("/tmp/life/prev-fire.png", frames[:24], cols=6, k=2)
    L.preview("/tmp/life/prev-pile.png", heaps, cols=3, k=6)
    return res


def lift_composite(body, shadow, lift):
    """Body drawn `lift` px above its ground anchor over its ground shadow."""
    h, w = body.img.shape[:2]
    out = np.zeros((h + lift, w, 4), np.uint8)
    s = shadow.img
    m = s[..., 3] > 0
    out[lift:][m] = s[m]
    m = body.img[..., 3] > 0
    out[:h][m] = body.img[m]
    return Frame(out, body.ax, body.ay + lift)


def export_heli(results, lift=24):
    """svc-heli sheet: `fly` 8 facings x 4 rotor frames (lifted, with shadow), medevac then fire; `idle` = SE facing."""
    res = {}
    frames = []
    for model in ("heli-medevac", "heli-fire"):
        r = results[model]
        for f8 in range(8):
            for k in range(len(r["anim"][f8])):
                frames.append(lift_composite(r["anim"][f8][k], r["shadow"][f8], lift))
    L.write_sheet(SUB + "heli.png", frames)
    res["heli"] = "iso/" + SUB + "heli.png"
    L.preview("/tmp/life/prev-heli.png", frames[:32], cols=8, k=3)
    return res
