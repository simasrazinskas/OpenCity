"""
iso_life_scene - LIFE overview: one street with everything that moves or lives on it, day and night.

Ground = NET street tiles + KIT grass, street lamps = NET furniture, houses = isokit stand-ins (until ZONED art);
vehicles, people, FX and markers are the LIFE PNGs, placed by their anchors in painter's order (depth x + y).
Lane and sidewalk offsets follow the NET contract (street lanes +-10 q, sidewalk centre 26 q).
"""
import os

import numpy as np

import isokit as ik
from iso_life_common import OUT, canvas, blit, load_png, save_png, crop
import iso_life_vehicles as V

NIGHT = np.array([0.34, 0.38, 0.56])
LANE = 10 / 64.0
WALK = 26 / 64.0
ISO = os.path.normpath(os.path.join(OUT, ".."))
NX, NY = 6, 3


def darken(img):
    out = img.copy()
    m = out[..., 3] > 0
    out[m, :3] = (out[m, :3] * NIGHT).astype(np.uint8)
    return out


def iso_png(rel):
    return load_png(os.path.join(ISO, rel))


def houses(night, row):
    s = ik.Scene(footprint=(NX, NY), seed=7)
    spots = {0: [(0.9, 0.25, "windows_plaster", "roof_tiles"), (2.25, 0.2, "windows_brick", "slate"),
                 (4.2, 0.25, "windows_siding", "roof_tiles_brown")],
             2: [(1.6, 2.3, "windows_brick", "roof_tiles"), (4.4, 2.3, "windows_plaster", "slate")]}[row]
    for x, y, wall, roof in spots:
        s.box(x, y, 0, 0.62, 0.5, 20, wall)
        s.roof_gable(x, y, 20, 0.62, 0.5, 11, roof, axis="x")
        s.box(x + 0.1, y + 0.1, 24, 0.08, 0.08, 10, "brick")
    return ik.render(s, night=night, crop=False)


class Stage:
    def __init__(self, night):
        self.ox, self.oy = NY * 32 + 8, 110
        self.img = canvas((NX + NY) * 32 + 16, (NX + NY) * 16 + 130)
        road, grass = iso_png("net/roads/street/street-0101.png"), iso_png("terrain/ground/grass-1.png")
        for cy in range(NY):
            for cx in range(NX):
                t = road if cy == 1 else grass
                x, y = self.at(cx, cy)
                blit(self.img, darken(t) if night else t, x - 32, y)
        self.items = []

    def at(self, wx, wy, wz=0.0):
        return int(round(self.ox + (wx - wy) * 32)), int(round(self.oy + (wx + wy) * 16 - wz))

    def add(self, depth, img, anchor, wx, wy, wz=0.0, mode="over"):
        self.items.append((depth, len(self.items), img, anchor, wx, wy, wz, mode))

    def add_scene(self, depth, spr):
        x, y = self.at(NX / 2.0, NY / 2.0)
        self.items.append((depth, len(self.items), spr.img, (spr.ax, spr.ay), NX / 2.0, NY / 2.0, 0.0, "over"))

    def draw(self):
        for depth, _, img, (ax, ay), wx, wy, wz, mode in sorted(self.items, key=lambda t: (t[0], t[1])):
            x, y = self.at(wx, wy, wz)
            if mode == "add":
                tmp = canvas(*self.img.shape[1::-1])
                blit(tmp, img, x - ax, y - ay)
                m = tmp[..., 3] > 0
                self.img[m, :3] = np.clip(self.img[m, :3].astype(int) + tmp[m, :3], 0, 255).astype(np.uint8)
            else:
                blit(self.img, img, x - ax, y - ay)
        self.items = []


def frame0(rel, n=4):
    img = load_png(os.path.join(OUT, rel))
    return img[:, :img.shape[1] // n]


TRAFFIC = [("sedan", 0.5, "E", "red"), ("bus", 1.7, "E", None), ("hatchback", 3.0, "E", "teal"),
           ("garbage-truck", 4.3, "E", None), ("suv", 5.4, "E", "white"), ("taxi", 0.8, "W", None),
           ("police", 2.4, "W", None), ("estate", 3.6, "W", "blue"), ("delivery-van", 5.0, "W", None)]
WALKERS = [("people/walk/adult_E_strip.png", 0.4, 1), ("people/walk/child_W_strip.png", 1.1, 1),
           ("people/walk/elder_E_strip.png", 2.6, -1), ("people/dogwalk/dogwalker_W_strip.png", 3.4, -1),
           ("people/shopper/shopper_E_strip.png", 4.0, 1), ("people/walk/adult_W_strip.png", 5.6, -1),
           ("people/jog/jog_E_strip.png", 1.9, -1), ("people/family/parent_child_W_strip.png", 4.8, 1)]


def build(root=OUT):
    out = []
    for night in (False, True):
        st = Stage(night)
        st.add_scene(0.5, houses(night, 0))                    # behind the street
        st.add_scene(NX + 2.3, houses(night, 2))               # in front of the street
        for name, along, d, col in TRAFFIC:
            prims = V.MODELS[name]()
            size = V.frame_box([prims], [])
            day = V.shot(prims, d, size, V.with_body(col) if col else V.PAL)
            wy = 1.5 + (LANE if d == "E" else -LANE)
            st.add(along + wy, darken(day) if night else day, (size[2], size[3]), along, wy)
            if night:
                lit, beam = V.night_overlay(prims, d, size)
                if beam is not None:
                    st.add(along + wy + 0.01, beam, (size[2], size[3]), along, wy, mode="add")
                st.add(along + wy + 0.02, lit, (size[2], size[3]), along, wy)
        for rel, along, side in WALKERS:
            if os.path.exists(os.path.join(OUT, rel)):
                img = frame0(rel)
                wy = 1.5 + side * WALK
                st.add(along + wy, darken(img) if night else img, (img.shape[1] // 2, img.shape[0] - 1), along, wy)
        for cx in (0, 2, 4):
            lamp = iso_png("net/furniture/lamp-N-night.png" if night else "net/furniture/lamp-N.png")
            st.add(cx + 0.5 + 1.1, lamp, (32, 80), cx + 0.5, 1.5)
            if night:
                st.add(cx + 0.5 + 1.1 + 0.01, iso_png("net/furniture/lamp-N-lit.png"), (32, 80), cx + 0.5, 1.5)
                pool = load_png(os.path.join(OUT, "fx/night/pool_m.png"))
                st.add(0.1, pool, (pool.shape[1] // 2, pool.shape[0] // 2), cx + 0.6, 1.25, mode="add")
        smoke = load_png(os.path.join(OUT, "fx/smoke/house_3.png"))
        st.add(NX + 3, darken(smoke) if night else smoke, (7, smoke.shape[0] - 1), 2.39, 0.34, 27)
        bubble = load_png(os.path.join(OUT, "markers/status/garbage.png"))
        st.add(NX + 4, bubble, (11, 26), 4.51, 0.5, 40)
        st.draw()
        rel = "overview/street-%s.png" % ("night" if night else "day")
        save_png(os.path.join(root, rel), crop(st.img)[0])
        out.append({"file": rel, "label": "night" if night else "day"})
    return {"title": "Overview: a street with everything LIFE draws (day and night)", "columns": 1, "scale": 2,
            "note": "NET street + lamps, KIT grass, isokit stand-in houses; traffic in NET lanes, people on sidewalks",
            "items": out}


if __name__ == "__main__":
    print(build())
