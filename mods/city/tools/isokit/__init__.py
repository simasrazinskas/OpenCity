"""isokit - the OpenCity 2.5D isometric renderer (RCT2/TTD look). See README.md.

    import isokit as ik
    s = ik.Scene(footprint=(1, 1))
    s.box(0.15, 0.15, 0, 0.7, 0.7, 20, ik.mat('windows_brick'))
    s.roof_gable(0.15, 0.15, 20, 0.7, 0.7, 12, 'roof_tiles', axis='x')
    spr = ik.render(s)               # Sprite: .img (RGBA), .ax/.ay anchor
    spr.save('house.png')
"""
from .geom import TILE_W, TILE_H, S, STOREY, LIGHT, project, Face, Ellipsoid
from .palette import PALETTE, RAMP, RAMP_NAMES, SHADES, color, palette_sheet, nearest
from .materials import Material, mat, register, plain, REGISTRY
from .scene import Scene
from . import roofs as _roofs  # noqa: F401  (attaches roof methods to Scene)
from . import library as _library  # noqa: F401  (registers named materials)
from . import organic as _organic  # noqa: F401  (Scene.limb, Scene.rock)
from .trees import tree, add_tree, SPECIES, GAME_TREES
from .tiles import shore_combos, combo_label, edge_distance, render_tile, flat_tile, diamond_mask
from .export import day_and_lit, even, footprint_overhang, clip_footprint, save_pair, thumbnail
from .render import render, Sprite, crop_to_content, level
from .sheet import facings, align, strip, save_strip, save_sprite, grid, Manifest
from .compose import Compositor, World, blit
from .post import nightify, desaturate, snap
from .png import write_png, read_png, upscale, on_bg, inspect
from .noise import hash2, value2, fbm, Rng

__all__ = [n for n in dir() if not n.startswith("_")]
