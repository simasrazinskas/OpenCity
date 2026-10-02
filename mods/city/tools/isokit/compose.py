"""Scene compositor: place pre-rendered sprites on a cell map with painter's depth order.

c = Compositor(12, 10, top_margin=160)
c.ground(tile_sprite, cx, cy)            # flat layer, drawn first (terrain, roads, decals)
c.place(building_sprite, cx, cy)         # footprint taken from sprite.footprint
img = c.render(night=False)              # RGBA canvas
"""
import numpy as np

from .geom import project


class Compositor:
    def __init__(self, w, h, top_margin=128, side_margin=8, bottom_margin=8):
        self.w, self.h = w, h
        self.layers = {0: [], 1: [], 2: []}
        self.ox = h * 32 + side_margin
        self.oy = top_margin
        self.W = (w + h) * 32 + 2 * side_margin
        self.H = (w + h) * 16 + top_margin + bottom_margin

    def screen(self, x, y, z=0.0):
        sx, sy = project(x, y, z)
        return int(round(sx)) + self.ox, int(round(sy)) + self.oy

    def ground(self, sprite, cx, cy, fp=None):
        """Flat ground layer item (terrain tile, road, decal)."""
        return self._add(0, sprite, cx, cy, fp)

    def place(self, sprite, cx, cy, fp=None, layer=1):
        """Standing object on footprint cells [cx, cx+fx) x [cy, cy+fy)."""
        return self._add(layer, sprite, cx, cy, fp)

    def overlay(self, sprite, cx, cy, fp=None):
        """Drawn after everything (icons, markers)."""
        return self._add(2, sprite, cx, cy, fp)

    def _add(self, layer, sprite, cx, cy, fp):
        fx, fy = fp or getattr(sprite, "footprint", (1, 1))
        key = (cx + fx + cy + fy, cx + fx, len(self.layers[layer]))
        self.layers[layer].append((key, sprite, cx, cy, fx, fy))

    def render(self, bg=(0, 0, 0, 0)):
        img = np.zeros((self.H, self.W, 4), np.uint8)
        img[:] = bg
        for layer in (0, 1, 2):
            for key, s, cx, cy, fx, fy in sorted(self.layers[layer], key=lambda t: t[0]):
                ax, ay = self.screen(cx + fx / 2.0, cy + fy / 2.0)
                blit(img, s.img, ax - s.ax, ay - s.ay)
        return img


def blit(dst, src, x, y):
    """Alpha-0/255 blit of src onto dst at (x, y) (clipped)."""
    h, w = src.shape[:2]
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, dst.shape[1]), min(y + h, dst.shape[0])
    if x0 >= x1 or y0 >= y1:
        return
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    a = s[..., 3:4] > 0
    dst[y0:y1, x0:x1] = np.where(a, s, dst[y0:y1, x0:x1])


class World:
    """Depth-sorted world compositor for full scenes (mockups), engine-style:
    - ground items are drawn first, in (layer, insertion) order;
    - multi-cell sprites are sliced into 32-px screen strips aligned to the footprint corners, each
      sorted at the frontmost ground point of its column (RCT2 / ENGINE-PLAN 3.1);
    - point objects (trees, props, vehicles, people) sort at their ground position x + y.
    Screen position of world (x, y, z): (ox + (x - y) * 32, oy + (x + y) * 16 - z).
    """

    def __init__(self, W, H, ox, oy, bg=(0, 0, 0, 0)):
        self.W, self.H, self.ox, self.oy = W, H, ox, oy
        self.bg = bg
        self._ground = []
        self._objs = []
        self._top = []
        self._n = 0

    def screen(self, x, y, z=0.0):
        return self.ox + (x - y) * 32.0, self.oy + (x + y) * 16.0 - z

    def visible(self, x, y, margin=200):
        sx, sy = self.screen(x, y)
        return -margin < sx < self.W + margin and -margin < sy < self.H + margin * 3

    def _n_inc(self):
        self._n += 1
        return self._n

    def ground(self, img, ax, ay, x, y, layer=0):
        """Flat item anchored at world ground point (x, y) (e.g. a tile: anchor (32, 16) at the cell centre)."""
        self._ground.append(((layer, self._n_inc()), img, ax, ay, x, y))

    def obj(self, img, ax, ay, x, y, z=0.0, bias=0.0):
        """Point object standing at world (x, y), anchor = its ground point (raised by z px)."""
        self._objs.append((x + y + bias, self._n_inc(), img, ax, ay, x, y, z, None))

    def building(self, img, ax, ay, cx, cy, fx, fy):
        """Footprint sprite (anchor = footprint ground centre) on cells [cx, cx+fx) x [cy, cy+fy)."""
        sx0, _ = self.screen(cx + fx / 2.0, cy + fy / 2.0)
        left = int(round(sx0 - ax))                  # sprite x of image column 0 in screen space
        sxL = (cx - cy - fy) * 32.0 + self.ox
        sxB = (cx + fx - cy - fy) * 32.0 + self.ox
        n = int(fx + fy)
        for k in range(n):
            s0 = sxL + 32 * k
            sc = s0 + 16
            if sc <= sxB:
                y = cy + fy
                x = (sc - self.ox) / 32.0 + y
            else:
                x = cx + fx
                y = x - (sc - self.ox) / 32.0
            c0 = int(round(s0)) - left if k > 0 else 0
            c1 = int(round(s0 + 32)) - left if k < n - 1 else img.shape[1]
            c0, c1 = max(c0, 0), min(c1, img.shape[1])
            if c1 <= c0:
                continue
            self._objs.append((x + y - 0.05, self._n_inc(), img[:, c0:c1], ax - c0, ay,
                               cx + fx / 2.0, cy + fy / 2.0, 0.0, None))

    def overlay(self, img, ax, ay, x, y, z=0.0):
        """Drawn after everything (status icons, cursors, markers)."""
        self._top.append((img, ax, ay, x, y, z))

    def overlay_screen(self, img, x, y):
        """Drawn after everything at absolute screen position (x, y) = image top-left."""
        self._top.append((img, 0, 0, None, None, (x, y)))

    def render(self):
        out = np.zeros((self.H, self.W, 4), np.uint8)
        out[:] = self.bg
        for _, img, ax, ay, x, y in sorted(self._ground, key=lambda t: t[0]):
            sx, sy = self.screen(x, y)
            blit(out, img, int(round(sx - ax)), int(round(sy - ay)))
        for key, _, img, ax, ay, x, y, z, _ in sorted(self._objs, key=lambda t: (t[0], t[1])):
            sx, sy = self.screen(x, y, z)
            blit(out, img, int(round(sx - ax)), int(round(sy - ay)))
        for img, ax, ay, x, y, z in self._top:
            if x is None:
                blit(out, img, int(z[0]), int(z[1]))
                continue
            sx, sy = self.screen(x, y, z)
            blit(out, img, int(round(sx - ax)), int(round(sy - ay)))
        return out
