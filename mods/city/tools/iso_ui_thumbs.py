"""
iso_ui_thumbs - placeholder build-menu thumbnails modelled with isokit (KIT's shared renderer).
The real thumbnails come from ZONED/CIVIC renders; these only show the card layout and scale.

Contract: 64x64 at UI 1x (96 at 1.5x), transparent, building on its footprint, default facing,
bottom-centred, 4 px margin. Larger footprints are reduced by an integer factor here (placeholder);
the final art should be rendered by isokit at a reduced tile scale instead (requested from KIT).
"""

import numpy as np

import isokit as ik
from iso_ui_kit import Canvas

_cache = {}


def _model(kind):
    s = None
    if kind == "hospital":
        s = ik.Scene(footprint=(2, 2), seed=1)
        s.ground([(0, 0), (2, 0), (2, 2), (0, 2)], "paving")
        s.box(0.2, 0.2, 0, 1.6, 1.2, 34, ik.mat("windows_plaster", ramp="snow"))
        s.box(0.2, 1.4, 0, 0.8, 0.45, 14, ik.mat("windows_plaster", ramp="snow"))
        s.roof_flat(0.2, 0.2, 34, 1.6, 1.2, "roof_flat", parapet=2)
        s.box(0.85, 0.7, 34, 0.3, 0.1, 2, ik.mat("plain", ramp="red", shade=2))
        s.box(0.95, 0.55, 34, 0.1, 0.4, 2, ik.mat("plain", ramp="red", shade=2))
    elif kind == "clinic":
        s = ik.Scene(footprint=(1, 1), seed=2)
        s.tile(0, 0, "grass")
        s.box(0.15, 0.2, 0, 0.7, 0.6, 16, ik.mat("windows_plaster", ramp="snow"))
        s.roof_flat(0.15, 0.2, 16, 0.7, 0.6, "roof_flat", parapet=1)
        s.box(0.45, 0.4, 16, 0.1, 0.25, 2, ik.mat("plain", ramp="red", shade=2))
    elif kind in ("police", "police_hq"):
        n = 1 if kind == "police" else 2
        s = ik.Scene(footprint=(n, n), seed=3)
        s.ground([(0, 0), (n, 0), (n, n), (0, n)], "paving")
        s.box(0.15, 0.15, 0, n - 0.3, n - 0.4, 18 * n, ik.mat("windows_concrete", ramp="water"))
        s.roof_flat(0.15, 0.15, 18 * n, n - 0.3, n - 0.4, "roof_flat", parapet=1)
    elif kind in ("fire", "firestation"):
        s = ik.Scene(footprint=(1, 1), seed=4)
        s.ground([(0, 0), (1, 0), (1, 1), (0, 1)], "concrete_ground")
        s.box(0.1, 0.1, 0, 0.8, 0.6, 18, ik.mat("brick", ramp="red"))
        s.roof_flat(0.1, 0.1, 18, 0.8, 0.6, "roof_flat", parapet=1)
        s.box(0.2, 0.7, 0, 0.5, 0.01, 11, ik.mat("plain", ramp="grey", shade=2))
    elif kind in ("school", "university"):
        s = ik.Scene(footprint=(2, 2), seed=5)
        s.ground([(0, 0), (2, 0), (2, 2), (0, 2)], "grass")
        s.box(0.2, 0.3, 0, 1.6, 0.8, 20, "windows_brick")
        s.roof_gable(0.2, 0.3, 20, 1.6, 0.8, 12, "slate", axis="x", gable="brick")
        s.box(0.8, 0.9, 20, 0.25, 0.25, 22, "brick")
        s.pyramid(0.8, 0.9, 42, 0.25, 0.25, 10, "roof_metal")
    elif kind == "park":
        s = ik.Scene(footprint=(1, 1), seed=6)
        s.tile(0, 0, "meadow")
        s.blob([(0.3, 0.35, 14, 0.18)], "foliage", rough=0.3)
        s.cylinder(0.3, 0.35, 0, 0.03, 8, "bark")
        s.blob([(0.7, 0.65, 11, 0.14)], "foliage_light", rough=0.3)
        s.cylinder(0.7, 0.65, 0, 0.03, 6, "bark")
    elif kind == "cemetery":
        s = ik.Scene(footprint=(1, 1), seed=7)
        s.tile(0, 0, "grass")
        for i in range(3):
            for j in range(3):
                s.box(0.15 + i * 0.28, 0.2 + j * 0.28, 0, 0.1, 0.04, 6, "stone")
    elif kind == "watertower":
        s = ik.Scene(footprint=(1, 1), seed=8)
        s.tile(0, 0, "grass")
        for dx, dy in ((0.35, 0.35), (0.65, 0.35), (0.35, 0.65), (0.65, 0.65)):
            s.cylinder(dx, dy, 0, 0.03, 26, "metal")
        s.cylinder(0.5, 0.5, 26, 0.28, 16, ik.mat("metal_light", ramp="water"))
        s.cone(0.5, 0.5, 42, 0.3, 6, ik.mat("metal", ramp="water"))
    elif kind == "house":
        s = ik.Scene(footprint=(1, 1), seed=9)
        s.tile(0, 0, "grass")
        s.box(0.2, 0.25, 0, 0.6, 0.5, 12, "windows_siding")
        s.roof_gable(0.2, 0.25, 12, 0.6, 0.5, 9, "roof_tiles", axis="x", gable="siding")
    elif kind == "tower":
        s = ik.Scene(footprint=(1, 1), seed=10)
        s.tile(0, 0, "paving")
        s.box(0.15, 0.15, 0, 0.7, 0.7, 60, "windows_office")
        s.roof_flat(0.15, 0.15, 60, 0.7, 0.7, "roof_gravel", parapet=2)
    elif kind in ("factory", "coal"):
        s = ik.Scene(footprint=(2, 2), seed=11)
        s.ground([(0, 0), (2, 0), (2, 2), (0, 2)], "concrete_ground")
        s.box(0.2, 0.3, 0, 1.2, 1.2, 16, "metal")
        s.roof_shed(0.2, 0.3, 16, 1.2, 1.2, 6, "roof_metal")
        s.cylinder(1.6, 0.5, 0, 0.12, 40, "brick")
    else:
        s = ik.Scene(footprint=(1, 1), seed=12)
        s.tile(0, 0, "grass")
        s.box(0.2, 0.2, 0, 0.6, 0.6, 16, "windows_plaster")
        s.roof_flat(0.2, 0.2, 16, 0.6, 0.6, "roof_flat", parapet=1)
    return s


def thumb(kind, size=64):
    key = (kind, size)
    if key in _cache:
        return _cache[key]
    spr = ik.render(_model(kind))
    img = spr.img
    h, w = img.shape[:2]
    k = 1
    while w // k > size or h // k > size - 2:
        k += 1
    if k > 1:
        img = img[k // 2::k, k // 2::k]
    if size >= 96 and k == 1:
        # 1.5x: the real pipeline renders at 1.5x tile scale; placeholder uses nearest 3/2 enlargement
        ys = (np.arange(int(h * 1.5)) / 1.5).astype(int)
        xs = (np.arange(int(w * 1.5)) / 1.5).astype(int)
        big = img[ys][:, xs]
        if big.shape[0] <= size - 2 and big.shape[1] <= size:
            img = big
    h, w = img.shape[:2]
    c = Canvas(size, size)
    sub = Canvas(w, h)
    sub.a[:, :, :] = img
    c.blit(sub, (size - w) // 2, size - 2 - h)
    _cache[key] = c
    return c
