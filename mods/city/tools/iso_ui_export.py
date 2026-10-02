#!/usr/bin/env python3
"""
iso_ui_export - exports the runtime UI art of the RCT2-style interface (design: mods/city/design/iso/ui/).

    python3 mods/city/tools/iso_ui_export.py            # icons + thumbnails
    python3 mods/city/tools/iso_ui_export.py icons      # only the icon atlases
    python3 mods/city/tools/iso_ui_export.py thumbs     # only the build-menu thumbnails

Outputs (mods/city/bits/chrome/iso/):
    icons-<n>.png, icons.txt      every icon of the iso_ui_icons_* recipes rendered natively at n x n device pixels
                                  (n in ICON_SIZES); icons.txt lists the names in atlas order.
    thumbs-<n>.png, thumbs.txt    build-menu thumbnails (isokit thumbnail() of the real CIVIC / NET / ZONED models)
                                  at n x n (n in THUMB_SIZES).
Atlas layout: COLS cells per row, each cell (n + 2) pixels square with the image at (1, 1).
At runtime CityChromeGenerator picks, for a device size d, the atlas size a and whole factor k with a * k <= d
closest to d (see CityChromeGenerator.Rct.cs), so icons stay native pixel art at every UI scale.
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from iso_ui_kit import Canvas, ROOT  # noqa: E402

OUT = os.path.join(ROOT, "bits", "chrome", "iso")
ICON_SIZES = [8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48]
THUMB_SIZES = [64, 96, 128]
COLS = 24

# Decision 11: towers that would shrink below this tile width are rendered at it and cropped at the top.
MIN_TILE = 32


def atlas(images, n, path, names_path, names):
    rows = (len(images) + COLS - 1) // COLS
    cell = n + 2
    c = Canvas(COLS * cell, max(1, rows) * cell)
    for i, img in enumerate(images):
        c.blit(img, (i % COLS) * cell + 1, (i // COLS) * cell + 1)
    c.save(path)
    with open(names_path, "w") as f:
        f.write("\n".join(names) + "\n")


def export_icons():
    import iso_ui_icons as I
    import iso_ui_icon_dsl as D
    if I.MISSING:
        raise SystemExit("icon modules failed to load: %r" % I.MISSING)
    names = list(D.ORDER)
    os.makedirs(OUT, exist_ok=True)
    for n in ICON_SIZES:
        atlas([D.get(nm, n) for nm in names], n, os.path.join(OUT, "icons-%d.png" % n), os.path.join(OUT, "icons.txt"), names)
    print("icons: %d names x %d sizes" % (len(names), len(ICON_SIZES)))


# ---- thumbnails --------------------------------------------------------------------------------------
def _models():
    """actor name -> zero-argument function returning an isokit Scene of the finished building (default facing)."""
    out = {}
    from iso_civic_states import REG, St
    import iso_civic_models  # noqa: F401  (registers every model)
    for name, sp in REG.items():
        out[name] = (lambda sp=sp: sp.fn(St()))
    import iso_net_bldg as NB
    out["transformer"] = NB.transformer
    out["battery"] = NB.battery
    out["sewage-outlet"] = NB.sewage_outlet
    out["treatment-plant"] = NB.treatment_plant
    import iso_zoned as Z
    import iso_zoned_sig as SG
    for k in SG.SIGS:
        out[k] = (lambda k=k: Z.C.build(k, "3x3", 5, 0))
    return out


def thumbnail(scene, size, ground="grass"):
    """isokit thumbnail() with decision 11: never below tile MIN_TILE; too tall renders are cropped at the top."""
    import isokit as ik
    from isokit.scene import Scene
    margin = 1
    inner = size - 2 * margin
    s = Scene(scene.footprint, scene.seed)
    fx, fy = scene.footprint
    for cx in range(int(fx)):
        for cy in range(int(fy)):
            s.ground([(cx, cy), (cx + 1, cy), (cx + 1, cy + 1), (cx, cy + 1)], ground, layer=-1)
    s.merge(scene)
    max_tile = int(size * 1.5) // 4 * 4
    min_tile = max(4, MIN_TILE * size // 64 // 4 * 4)
    spr = None
    for tw in range(max_tile, min_tile - 1, -4):
        spr = ik.render(s, facing=0, tile=(tw, tw // 2))
        if spr.w <= inner and spr.h <= inner:
            break
    img = spr.img
    h, w = img.shape[:2]
    if w > inner:  # very wide footprints: centre crop (rare)
        x0 = (w - inner) // 2
        img, w = img[:, x0:x0 + inner], inner
    if h > inner:  # tall towers: keep the bottom (decision 11)
        img, h = img[h - inner:], inner
    out = Canvas(size, size)
    sub = Canvas(w, h)
    sub.a[:, :, :] = img
    out.blit(sub, (size - w) // 2, size - margin - h)
    return out


def export_thumbs(only=None):
    models = _models()
    names = sorted(n for n in models if not only or n in only)
    os.makedirs(OUT, exist_ok=True)
    for n in THUMB_SIZES:
        imgs = []
        for nm in names:
            imgs.append(thumbnail(models[nm](), n))
        atlas(imgs, n, os.path.join(OUT, "thumbs-%d.png" % n), os.path.join(OUT, "thumbs.txt"), names)
        print("thumbs %d: %d" % (n, len(names)), flush=True)


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or "icons" in args:
        export_icons()
    if not args or "thumbs" in args:
        export_thumbs()
