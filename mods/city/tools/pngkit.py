"""
pngkit - tiny dependency-free RGBA drawing + PNG writer for OpenCity art generation.

All OpenCity art is generated procedurally by scripts in this folder, so the mod
ships without any third-party assets. Only the python3 standard library is used.

Conventions
-----------
* Colors are (r, g, b, a) tuples of ints 0..255 (a defaults to 255 if omitted).
* Canvas coordinates: x to the right, y down, origin top-left.
* Canvas.save(path, meta) writes a single-IDAT RGBA PNG. `meta` is a dict that
  becomes tEXt chunks placed BEFORE the IDAT chunk. OpenRA's PngSheetLoader reads:
    FrameSize   "w,h"             -> slice the sheet into frames of this size
    FrameAmount "n"               -> optional frame count
    Offset      "x,y"             -> sprite offset (applies to all frames)
    Frame[i]    "x,y,w,h;ox,oy"   -> explicit frame regions (overrides FrameSize)
* save_sheet(path, frames, cols=None) packs equally sized canvases into a grid
  and writes FrameSize/FrameAmount metadata automatically.
"""

import math
import os
import random
import struct
import zlib


def rgba(c):
    if len(c) == 3:
        return (c[0], c[1], c[2], 255)
    return tuple(c)


def mix(c1, c2, t):
    """Linear blend between two colors, t in 0..1."""
    c1, c2 = rgba(c1), rgba(c2)
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


def shade(c, f):
    """Multiply rgb by f (f<1 darker, f>1 lighter), keep alpha."""
    c = rgba(c)
    return (min(255, max(0, int(c[0] * f))), min(255, max(0, int(c[1] * f))),
            min(255, max(0, int(c[2] * f))), c[3])


def with_alpha(c, a):
    c = rgba(c)
    return (c[0], c[1], c[2], a)


def hexcolor(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


class Canvas:
    def __init__(self, w, h, fill=(0, 0, 0, 0)):
        self.w = w
        self.h = h
        f = rgba(fill)
        self.px = [list(f) for _ in range(w * h)]

    # ---- pixel access -------------------------------------------------
    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return tuple(self.px[y * self.w + x])
        return (0, 0, 0, 0)

    def set(self, x, y, c):
        """Overwrite a pixel (no blending)."""
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y * self.w + x] = list(rgba(c))

    def blend(self, x, y, c):
        """Alpha-composite color c over the pixel (source-over)."""
        x, y = int(x), int(y)
        if not (0 <= x < self.w and 0 <= y < self.h):
            return
        c = rgba(c)
        sa = c[3] / 255.0
        if sa <= 0:
            return
        d = self.px[y * self.w + x]
        da = d[3] / 255.0
        oa = sa + da * (1 - sa)
        if oa <= 0:
            self.px[y * self.w + x] = [0, 0, 0, 0]
            return
        for i in range(3):
            d[i] = int(round((c[i] * sa + d[i] * da * (1 - sa)) / oa))
        d[3] = int(round(oa * 255))

    # ---- primitives ---------------------------------------------------
    def fill(self, c):
        c = list(rgba(c))
        self.px = [list(c) for _ in range(self.w * self.h)]

    def rect(self, x, y, w, h, c, blend=True):
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                (self.blend if blend else self.set)(xx, yy, c)

    def rect_outline(self, x, y, w, h, c, t=1):
        self.rect(x, y, w, t, c)
        self.rect(x, y + h - t, w, t, c)
        self.rect(x, y + t, t, h - 2 * t, c)
        self.rect(x + w - t, y + t, t, h - 2 * t, c)

    def hline(self, x0, x1, y, c):
        for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1):
            self.blend(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(int(min(y0, y1)), int(max(y0, y1)) + 1):
            self.blend(x, y, c)

    def line(self, x0, y0, x1, y1, c, width=1):
        """Bresenham-ish line with square brush."""
        dx, dy = x1 - x0, y1 - y0
        steps = int(max(abs(dx), abs(dy), 1))
        r = (width - 1) / 2.0
        for i in range(steps + 1):
            t = i / steps
            x, y = x0 + dx * t, y0 + dy * t
            if width <= 1:
                self.blend(round(x), round(y), c)
            else:
                self.rect(round(x - r), round(y - r), width, width, c)

    def circle(self, cx, cy, r, c, aa=True):
        """Filled circle with optional soft edge."""
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if d <= r - 0.5:
                    self.blend(x, y, c)
                elif aa and d < r + 0.5:
                    cc = rgba(c)
                    self.blend(x, y, (cc[0], cc[1], cc[2], int(cc[3] * (r + 0.5 - d))))

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    self.blend(x, y, c)

    def polygon(self, pts, c):
        """Scanline fill of a simple polygon given as [(x, y), ...]."""
        ys = [p[1] for p in pts]
        n = len(pts)
        for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            sy = y + 0.5
            xs = []
            for i in range(n):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
                if (y0 <= sy < y1) or (y1 <= sy < y0):
                    xs.append(x0 + (sy - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(math.ceil(xs[i] - 0.5)), int(math.floor(xs[i + 1] - 0.5)) + 1):
                    self.blend(x, y, c)

    def vgradient(self, x, y, w, h, c_top, c_bottom):
        for yy in range(int(h)):
            t = yy / max(1, h - 1)
            self.rect(x, y + yy, w, 1, mix(c_top, c_bottom, t))

    def noise(self, amount=0.08, seed=0, x=0, y=0, w=None, h=None, mono=True):
        """Brightness noise on opaque-ish pixels inside a region."""
        rnd = random.Random(seed)
        w = self.w if w is None else w
        h = self.h if h is None else h
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                if not (0 <= xx < self.w and 0 <= yy < self.h):
                    continue
                p = self.px[yy * self.w + xx]
                if p[3] == 0:
                    continue
                if mono:
                    f = 1 + rnd.uniform(-amount, amount)
                    for i in range(3):
                        p[i] = min(255, max(0, int(p[i] * f)))
                else:
                    for i in range(3):
                        p[i] = min(255, max(0, int(p[i] * (1 + rnd.uniform(-amount, amount)))))

    def blit(self, src, dx, dy, blend=True):
        for y in range(src.h):
            for x in range(src.w):
                c = src.px[y * src.w + x]
                if blend:
                    self.blend(dx + x, dy + y, c)
                else:
                    self.set(dx + x, dy + y, c)

    def outline(self, c, only_transparent=True):
        """Draw a 1px outline around opaque content (useful for readability)."""
        src = [p[:] for p in self.px]
        for y in range(self.h):
            for x in range(self.w):
                if src[y * self.w + x][3] != 0:
                    continue
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + ox, y + oy
                    if 0 <= xx < self.w and 0 <= yy < self.h and src[yy * self.w + xx][3] > 128:
                        self.set(x, y, c)
                        break

    def drop_shadow(self, dx=2, dy=2, alpha=70):
        """Add a soft-ish shadow under existing content (drawn behind it)."""
        shadow = Canvas(self.w, self.h)
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y * self.w + x][3] > 100:
                    shadow.set(x + dx, y + dy, (0, 0, 0, alpha))
        shadow.blit(self, 0, 0)
        self.px = shadow.px

    def copy(self):
        c = Canvas(self.w, self.h)
        c.px = [p[:] for p in self.px]
        return c

    def flip_h(self):
        c = Canvas(self.w, self.h)
        for y in range(self.h):
            for x in range(self.w):
                c.px[y * self.w + x] = self.px[y * self.w + (self.w - 1 - x)][:]
        return c

    def rotate90(self, times=1):
        """Rotate clockwise by 90 degrees `times` times (square canvases only)."""
        c = self.copy()
        for _ in range(times % 4):
            n = Canvas(c.h, c.w)
            for y in range(c.h):
                for x in range(c.w):
                    n.px[x * n.w + (c.h - 1 - y)] = c.px[y * c.w + x][:]
            c = n
        return c

    def rotate(self, angle_deg, cx=None, cy=None):
        """Nearest-neighbour rotation around a centre (clockwise, screen coords)."""
        cx = self.w / 2.0 if cx is None else cx
        cy = self.h / 2.0 if cy is None else cy
        a = math.radians(angle_deg)
        ca, sa = math.cos(a), math.sin(a)
        n = Canvas(self.w, self.h)
        for y in range(self.h):
            for x in range(self.w):
                fx, fy = x + 0.5 - cx, y + 0.5 - cy
                sx = ca * fx + sa * fy + cx
                sy = -sa * fx + ca * fy + cy
                n.px[y * self.w + x] = list(self.get(int(math.floor(sx)), int(math.floor(sy))))
        return n

    def scale(self, factor):
        """Nearest-neighbour integer scale (for 2x/3x chrome)."""
        n = Canvas(self.w * factor, self.h * factor)
        for y in range(n.h):
            for x in range(n.w):
                n.px[y * n.w + x] = self.px[(y // factor) * self.w + (x // factor)][:]
        return n

    # ---- output -------------------------------------------------------
    def save(self, path, meta=None):
        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        raw = bytearray()
        for y in range(self.h):
            raw.append(0)
            for x in range(self.w):
                raw.extend(bytes(self.px[y * self.w + x]))

        def chunk(tag, data):
            body = tag + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xffffffff)

        out = bytearray(b"\x89PNG\r\n\x1a\n")
        out += chunk(b"IHDR", struct.pack(">IIBBBBB", self.w, self.h, 8, 6, 0, 0, 0))
        for k, v in (meta or {}).items():
            out += chunk(b"tEXt", k.encode("ascii") + b"\x00" + str(v).encode("ascii"))
        out += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        out += chunk(b"IEND", b"")
        with open(path, "wb") as f:
            f.write(out)


def save_sheet(path, frames, cols=None, meta=None):
    """Pack equally sized frames into a grid PNG with FrameSize/FrameAmount metadata."""
    if not frames:
        raise ValueError("no frames")
    fw, fh = frames[0].w, frames[0].h
    for f in frames:
        if (f.w, f.h) != (fw, fh):
            raise ValueError("all frames must have the same size")
    n = len(frames)
    cols = cols or n
    rows = (n + cols - 1) // cols
    sheet = Canvas(fw * cols, fh * rows)
    for i, f in enumerate(frames):
        sheet.blit(f, (i % cols) * fw, (i // cols) * fh, blend=False)
    m = {"FrameSize": f"{fw},{fh}", "FrameAmount": str(n)}
    if meta:
        m.update(meta)
    sheet.save(path, m)
    return sheet


def save_atlas(path, regions, width=None, meta=None, padding=1):
    """
    Pack named canvases into one image (simple shelf packer) for chrome.yaml use.
    Returns {name: (x, y, w, h)} so the caller can emit chrome yaml Regions.
    """
    items = sorted(regions.items(), key=lambda kv: -kv[1].h)
    width = width or max(256, max(c.w for _, c in items) + 2 * padding)
    x = y = padding
    shelf_h = 0
    placed = {}
    for name, c in items:
        if x + c.w + padding > width:
            x = padding
            y += shelf_h + padding
            shelf_h = 0
        placed[name] = (x, y, c.w, c.h)
        x += c.w + padding
        shelf_h = max(shelf_h, c.h)
    height = y + shelf_h + padding
    # Chrome sheets must be power-of-two sized or the engine throws "Non-power-of-two array".
    height = 1 << max(0, (height - 1).bit_length())
    width = 1 << max(0, (width - 1).bit_length())
    atlas = Canvas(width, height)
    for name, c in items:
        px, py, _, _ = placed[name]
        atlas.blit(c, px, py, blend=False)
    atlas.save(path, meta)
    return placed
