"""
iso_net_vignettes.py - small composed city blocks (6x6 cells) that put the NET tiles and props in context.
Buildings are plain grey placeholder boxes (other workstreams own buildings). Each vignette is written
procedurally with a Block: terrain, roads (masks computed from neighbours), props, buildings; rendered with
ground first and objects in depth order (cx + cy), like the engine will sort them.
"""
import os

import numpy as np

import iso_net_bldg as BL
import iso_net_pal as P
import iso_net_props as PR
import isokit as ik
from iso_net_core import OUT, blank, over, save_png, write_manifest_fragment
from iso_net_kit import night_tile, render_fixed, winter_tile
from iso_net_out_ground import lot_tile
from iso_net_out_props import light_pool
from iso_net_power import power_tile
from iso_net_rail import crossing_tile, rail_tile
from iso_net_roads import road_scene, road_tile
from iso_net_roadx import arrow_scene, control_extra, island_scene, median_tile, tile
from iso_net_struct import H as BH, bridge_piece

CLS = {"s": "street", "a": "avenue", "v": "boulevard", "h": "highway", "l": "alley", "g": "gravel"}
DIR = {"^": 0, ">": 1, "v": 2, "<": 3}
NB = [(0, -1), (1, 0), (0, 1), (-1, 0)]
MARGIN_TOP = 110


def render_fixed_sprite(spr):
    from iso_net_kit import fixed
    return fixed(spr, 64, 32, 32, 16)


def _terrain(kind, cx, cy, season="summer"):
    s = ik.Scene(footprint=(1, 1))
    s.tile(0, 0, "water" if kind == "w" else "grass")
    return ik.render(s, origin=(cx, cy), crop=False, season=season)


class Block:
    def __init__(self, w=6, h=6, night=False, season='summer'):
        self.w, self.h = w, h
        self.night = night
        self.season = season
        self.terr = {(x, y): "grass" for x in range(w) for y in range(h)}
        self.roads = {}    # (cx, cy) -> dict(cls, ow, sb, bridge, extra, edge)
        self.ground = []   # (sx, sy, img 64x32)
        self.overlays = []
        self.objs = []     # (depth, sx, sy, img)
        self.pools = []
        W = (w + h) * 32 + 16
        Hh = (w + h) * 16 + MARGIN_TOP + 24
        self.img = blank(W, Hh)
        self.ox = h * 32 + 8
        self.oy = MARGIN_TOP

    def water(self, cells):
        for c in cells:
            self.terr[c] = "water"

    def road(self, cells, cls="street", ow=None, sb=0, bridge=None, extra=None, edge=0, draw=True):
        for c in cells:
            self.roads[c] = dict(cls=cls, ow=ow, sb=sb, bridge=bridge, extra=extra, edge=edge, draw=draw)

    def mask(self, c):
        r = self.roads[c]
        m = 0
        for i, (dx, dy) in enumerate(NB):
            if r["sb"] & (1 << i):
                continue
            n = (c[0] + dx, c[1] + dy)
            if n in self.roads and not (self.roads[n]["sb"] & (1 << ((i + 2) % 4))):
                m |= 1 << i
        return m | r["edge"]

    def lay(self):
        for (cx, cy), t in self.terr.items():
            spr = _terrain("w" if t == "water" else ".", cx, cy, self.season)
            self.put_ground(cx, cy, render_fixed_sprite(spr))
        for c, r in self.roads.items():
            m = self.mask(c)
            if not r["draw"]:
                continue
            if r["bridge"]:
                axis = "ns" if m & 5 else "ew"
                img = bridge_piece(r["cls"], axis, r["bridge"], oneway=r["ow"] is not None)
                if self.season == "winter":
                    img = winter_tile(img)
                self.put_obj(c[0], c[1], img, 32, BH + 16)
                continue
            t = tile(road_scene(r["cls"], m, r["ow"] is not None, extra=r["extra"]))
            if r["sb"] and r["cls"] in ("avenue", "boulevard", "highway"):
                over(t, median_tile(r["cls"], r["sb"]), 0, 0)
            if r["ow"] is not None:
                over(t, tile(arrow_scene(r["ow"])), 0, 0)
            self.put_ground(c[0], c[1], t)

    # ---- placement helpers
    def centre(self, cx, cy):
        return self.ox + (cx - cy) * 32, self.oy + (cx + cy) * 16 + 16

    def put_ground(self, cx, cy, img):
        sx, sy = self.centre(cx, cy)
        self.ground.append((sx - 32, sy - 16, img))

    def put_obj(self, cx, cy, img, ax, ay, depth=None):
        sx, sy = self.centre(cx, cy)
        d = depth if depth is not None else (cx + cy, cx)
        self.objs.append((d, sx - ax, sy - ay, img))

    def prop(self, cx, cy, model, facing=0):
        self.put_obj(cx, cy, render_fixed(model, 64, 96, 32, 80, facing=facing, night=self.night, season=self.season), 32, 80)

    def building(self, cx, cy, fw, fh, storeys, seed=0):
        s = ik.Scene(footprint=(fw, fh), seed=seed)
        hgt = storeys * 10
        s.box(0.08, 0.08, 0, fw - 0.16, fh - 0.16, hgt, ik.mat("plain"))
        s.roof_flat(0.08, 0.08, hgt, fw - 0.16, fh - 0.16, ik.mat("plain", shade=0.5), parapet=1)
        W, Hh = (fw + fh) * 32, (fw + fh) * 16 + hgt + 24
        ax, ay = fh * 32, Hh - (fw + fh) * 8
        img = render_fixed(s, W, Hh, ax, ay, night=self.night, season=self.season)
        gx, gy = cx + (fw - 1) / 2, cy + (fh - 1) / 2
        sx, sy = self.ox + (gx - gy) * 32, self.oy + (gx + gy) * 16 + 16
        self.objs.append(((cx + fw - 1 + cy + fh - 1, cx + fw - 1), int(round(sx - ax)), int(round(sy - ay)), img))

    def render(self):
        img = self.img
        img[..., 3] = 0
        for (sx, sy, t) in self.ground:
            t = P.quantize(t)
            if self.season == "winter":
                t = winter_tile(t, int(sx * 7 + sy))
            if self.night:
                t = night_tile(t)
            over(img, t, int(sx), int(sy))
        if self.night:
            for (cx, cy, side) in self.pools:
                sx, sy = self.centre(cx, cy)
                over(img, light_pool(side), sx - 32, sy - 16)
        for d, sx, sy, t in sorted(self.objs, key=lambda o: o[0]):
            over(img, t, int(sx), int(sy))
        bg = np.array([22, 26, 44] if self.night else [0, 0, 0], np.float32)
        out = img.copy()
        m = out[..., 3] < 128
        out[m, :3] = bg
        out[m, 3] = 255 if self.night else 0
        return out
