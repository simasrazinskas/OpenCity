"""
iso_ui_kit - numpy RGBA canvas, PNG writer, RCT2-style colour ramps and pixel-font text for the
OpenCity iso UI design (design phase; see mods/city/design/iso/BRIEF.md).

Colours: (r, g, b) or (r, g, b, a) tuples, or "#rrggbb" strings. Alpha is always 0 or 255 in output.
"""

import os
import struct
import zlib

import numpy as np

import iso_ui_font as F

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "design", "iso", "ui")


def hexc(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def col(c):
    if isinstance(c, str):
        return hexc(c)
    if len(c) == 3:
        return tuple(c) + (255,)
    return tuple(c)


def mix(a, b, t):
    a, b = col(a), col(b)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


# RCT2-style 8-step ramps, 0 = darkest .. 7 = lightest. Every window family picks one ramp.
RAMPS = {
    "brown":  ["#24160a", "#3e2712", "#5c3a1c", "#7a5028", "#986838", "#b6864e", "#d2a86e", "#ecd09c"],
    "orange": ["#341604", "#5e2a06", "#8a420a", "#b45c12", "#d8781e", "#ec983e", "#f6ba6c", "#fcdca6"],
    "grey":   ["#18191c", "#2e3036", "#484a52", "#62646e", "#7e808a", "#9c9ea6", "#bcbec4", "#dedfe2"],
    "green":  ["#0a2210", "#123c1a", "#1c5826", "#287432", "#3a9040", "#58ac56", "#84c87a", "#b6e2a6"],
    "blue":   ["#0a1634", "#122854", "#1c3c78", "#28549a", "#3a70bc", "#5890d4", "#84b4e6", "#b6d4f4"],
    "teal":   ["#062628", "#0c3e40", "#165a5a", "#227674", "#349490", "#52b0aa", "#80ccc4", "#b2e6de"],
    "red":    ["#280808", "#480c0c", "#6a1412", "#8e201c", "#b0322a", "#cc5446", "#e69a8c", "#f6cbbf"],
    "purple": ["#1a0c2a", "#2c1648", "#422068", "#5a2e86", "#7442a2", "#905ebc", "#b088d4", "#d2b8ec"],
    "yellow": ["#382a06", "#60480a", "#8a6c12", "#b2901c", "#d4b02c", "#eaca4c", "#f6e080", "#fdf2c0"],
    "sand":   ["#2a2216", "#463a28", "#665640", "#86745a", "#a69476", "#c4b496", "#ded2b8", "#f4ecdc"],
}

INK = hexc("#140e08")       # body text on light panels
WHITE = hexc("#ffffff")
CREAM = hexc("#fff4d6")      # light text on dark wells
SHADOW = hexc("#0c0804")
MONEY_POS = hexc("#0e5a16")  # on light bodies
MONEY_NEG = hexc("#a01410")
MONEY_POS_L = hexc("#7cf06c")  # on dark wells
MONEY_NEG_L = hexc("#ff6a50")
CLEAR = (0, 0, 0, 0)


def R(ramp, i):
    return hexc(RAMPS[ramp][max(0, min(7, i))])


class Canvas:
    def __init__(self, w, h, bg=CLEAR):
        self.w, self.h = int(w), int(h)
        self.a = np.zeros((self.h, self.w, 4), np.uint8)
        if bg != CLEAR:
            self.a[:, :] = col(bg)

    # ---- primitives -------------------------------------------------------------------------------
    def fill(self, x, y, w, h, c):
        x, y, w, h = int(x), int(y), int(w), int(h)
        x0, y0, x1, y1 = max(0, x), max(0, y), min(self.w, x + w), min(self.h, y + h)
        if x1 > x0 and y1 > y0:
            self.a[y0:y1, x0:x1] = col(c)

    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.a[int(y), int(x)] = col(c)

    def hline(self, x, y, w, c):
        self.fill(x, y, w, 1, c)

    def vline(self, x, y, h, c):
        self.fill(x, y, 1, h, c)

    def rect(self, x, y, w, h, c, t=1):
        self.fill(x, y, w, t, c)
        self.fill(x, y + h - t, w, t, c)
        self.fill(x, y, t, h, c)
        self.fill(x + w - t, y, t, h, c)

    def mask(self, m, x, y, c):
        """Paint colour c where boolean mask m is true, with top-left at (x, y)."""
        x, y = int(x), int(y)
        h, w = m.shape
        x0, y0 = max(0, x), max(0, y)
        x1, y1 = min(self.w, x + w), min(self.h, y + h)
        if x1 <= x0 or y1 <= y0:
            return
        sub = m[y0 - y:y1 - y, x0 - x:x1 - x]
        self.a[y0:y1, x0:x1][sub] = col(c)

    def blit(self, img, x, y):
        src = img.a if isinstance(img, Canvas) else img
        x, y = int(x), int(y)
        h, w = src.shape[:2]
        x0, y0 = max(0, x), max(0, y)
        x1, y1 = min(self.w, x + w), min(self.h, y + h)
        if x1 <= x0 or y1 <= y0:
            return
        s = src[y0 - y:y1 - y, x0 - x:x1 - x]
        m = s[:, :, 3] > 127
        self.a[y0:y1, x0:x1][m] = s[m]

    def crop(self, x, y, w, h):
        c = Canvas(w, h)
        c.a[:, :] = self.a[y:y + h, x:x + w]
        return c

    def scaled(self, k):
        c = Canvas(self.w * k, self.h * k)
        c.a = self.a.repeat(k, 0).repeat(k, 1)
        return c

    # ---- text -------------------------------------------------------------------------------------
    def text(self, x, y, s, face="10", c=INK, shadow=None, outline=None, align="left", w=None):
        """Draws s with its cap-top at y (so y is the visual top of capitals). Returns text width.
        align left|center|right inside width w (when given)."""
        if not s:
            return 0
        m, asc = F.render_mask(s, face)
        tw = m.shape[1]
        if w is not None:
            if align == "center":
                x = x + (w - tw) // 2
            elif align == "right":
                x = x + w - tw
        top = y - (asc - F.cap_height(face))
        if outline is not None:
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)):
                self.mask(m, x + dx, top + dy, outline)
        if shadow is not None:
            self.mask(m, x + 1, top + 1, shadow)
        self.mask(m, x, top, c)
        return tw

    def save(self, rel):
        path = rel if os.path.isabs(rel) else os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        a = self.a.copy()
        a[:, :, 3] = np.where(a[:, :, 3] > 127, 255, 0)
        a[a[:, :, 3] == 0] = 0
        raw = b"".join(b"\x00" + a[r].tobytes() for r in range(self.h))

        def chunk(t, d):
            return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
        png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", self.w, self.h, 8, 6, 0, 0, 0))
        png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
        with open(path, "wb") as f:
            f.write(png)
        return path


def load_png(path):
    """Minimal PNG reader (8-bit RGBA/RGB, non-interlaced) for reusing renders from other workstreams."""
    d = open(path, "rb").read()
    p, idat, w = 8, b"", 0
    while p < len(d):
        ln = struct.unpack(">I", d[p:p + 4])[0]
        t = d[p + 4:p + 8]
        body = d[p + 8:p + 8 + ln]
        if t == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif t == b"IDAT":
            idat += body
        p += 12 + ln
    ch = {6: 4, 2: 3}[ct]
    raw = zlib.decompress(idat)
    out = np.zeros((h, w * ch), np.int32)
    stride = w * ch
    prev = np.zeros(stride, np.int32)
    for r in range(h):
        f = raw[r * (stride + 1)]
        line = np.frombuffer(raw[r * (stride + 1) + 1:(r + 1) * (stride + 1)], np.uint8).astype(np.int32)
        cur = np.zeros(stride, np.int32)
        for i in range(stride):
            a = cur[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 0: v = line[i]
            elif f == 1: v = line[i] + a
            elif f == 2: v = line[i] + b
            elif f == 3: v = line[i] + (a + b) // 2
            else:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                v = line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)
            cur[i] = v & 255
        out[r] = cur
        prev = cur
    img = out.reshape(h, w, ch).astype(np.uint8)
    cv = Canvas(w, h)
    cv.a[:, :, :ch] = img
    if ch == 3:
        cv.a[:, :, 3] = 255
    return cv
