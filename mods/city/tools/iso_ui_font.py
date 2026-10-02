"""
iso_ui_font - dependency-free TrueType reader + 1-bit rasterizer for the OpenCity pixel fonts.

The OpenCity Pixel faces have all outline points on whole pixels (see mods/city/fonts/README.md),
so a nonzero-winding scanline fill sampled at pixel centres reproduces the engine output exactly.
Only stdlib + numpy. Used by the iso_ui* design generators.
"""

import os
import struct

import numpy as np

FONT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fonts")


class TTF:
    def __init__(self, path, px):
        self.data = open(path, "rb").read()
        self.px = px
        self.tables = {}
        num = struct.unpack(">H", self.data[4:6])[0]
        for i in range(num):
            tag, _, off, ln = struct.unpack(">4sIII", self.data[12 + i * 16:28 + i * 16])
            self.tables[tag.decode("latin1")] = (off, ln)
        head = self.tables["head"][0]
        self.upem = struct.unpack(">H", self.data[head + 18:head + 20])[0]
        self.loca_long = struct.unpack(">h", self.data[head + 50:head + 52])[0] == 1
        hhea = self.tables["hhea"][0]
        self.ascent, self.descent = struct.unpack(">hh", self.data[hhea + 4:hhea + 8])
        self.num_hmetrics = struct.unpack(">H", self.data[hhea + 34:hhea + 36])[0]
        maxp = self.tables["maxp"][0]
        self.num_glyphs = struct.unpack(">H", self.data[maxp + 4:maxp + 6])[0]
        self.scale = px / self.upem
        self._read_cmap()
        self._read_loca()
        self.cache = {}

    def _read_cmap(self):
        base = self.tables["cmap"][0]
        n = struct.unpack(">H", self.data[base + 2:base + 4])[0]
        self.cmap = {}
        best = None
        for i in range(n):
            pid, eid, off = struct.unpack(">HHI", self.data[base + 4 + i * 8:base + 12 + i * 8])
            fmt = struct.unpack(">H", self.data[base + off:base + off + 2])[0]
            if fmt in (4, 12) and (best is None or fmt == 12):
                best = (fmt, base + off)
        fmt, o = best
        d = self.data
        if fmt == 4:
            segx2 = struct.unpack(">H", d[o + 6:o + 8])[0]
            seg = segx2 // 2
            ends = struct.unpack(">%dH" % seg, d[o + 14:o + 14 + segx2])
            st = o + 16 + segx2
            starts = struct.unpack(">%dH" % seg, d[st:st + segx2])
            deltas = struct.unpack(">%dh" % seg, d[st + segx2:st + 2 * segx2])
            ro = st + 2 * segx2
            ranges = struct.unpack(">%dH" % seg, d[ro:ro + segx2])
            for s in range(seg):
                for c in range(starts[s], ends[s] + 1):
                    if c == 0xFFFF:
                        continue
                    if ranges[s] == 0:
                        g = (c + deltas[s]) & 0xFFFF
                    else:
                        a = ro + s * 2 + ranges[s] + (c - starts[s]) * 2
                        g = struct.unpack(">H", d[a:a + 2])[0]
                        if g:
                            g = (g + deltas[s]) & 0xFFFF
                    if g:
                        self.cmap[c] = g
        else:
            ng = struct.unpack(">I", d[o + 12:o + 16])[0]
            for i in range(ng):
                s, e, g = struct.unpack(">III", d[o + 16 + i * 12:o + 28 + i * 12])
                for c in range(s, e + 1):
                    self.cmap[c] = g + (c - s)

    def _read_loca(self):
        o = self.tables["loca"][0]
        n = self.num_glyphs + 1
        if self.loca_long:
            self.loca = struct.unpack(">%dI" % n, self.data[o:o + 4 * n])
        else:
            self.loca = [v * 2 for v in struct.unpack(">%dH" % n, self.data[o:o + 2 * n])]

    def advance(self, gid):
        o = self.tables["hmtx"][0]
        i = min(gid, self.num_hmetrics - 1)
        return struct.unpack(">H", self.data[o + i * 4:o + i * 4 + 2])[0]

    def contours(self, gid):
        g0 = self.tables["glyf"][0]
        start, end = self.loca[gid], self.loca[gid + 1]
        if end <= start:
            return []
        d = self.data
        o = g0 + start
        nc = struct.unpack(">h", d[o:o + 2])[0]
        if nc < 0:  # composite
            res = []
            p = o + 10
            while True:
                flags, cg = struct.unpack(">HH", d[p:p + 4])
                p += 4
                if flags & 1:
                    dx, dy = struct.unpack(">hh", d[p:p + 4]); p += 4
                else:
                    dx, dy = struct.unpack(">bb", d[p:p + 2]); p += 2
                if flags & 8: p += 2
                elif flags & 0x40: p += 4
                elif flags & 0x80: p += 8
                for c in self.contours(cg):
                    res.append([(x + dx, y + dy) for x, y in c])
                if not flags & 0x20:
                    break
            return res
        ends = struct.unpack(">%dH" % nc, d[o + 10:o + 10 + nc * 2])
        p = o + 10 + nc * 2
        il = struct.unpack(">H", d[p:p + 2])[0]
        p += 2 + il
        npts = ends[-1] + 1
        flags = []
        while len(flags) < npts:
            f = d[p]; p += 1
            flags.append(f)
            if f & 8:
                r = d[p]; p += 1
                flags.extend([f] * r)
        xs, ys = [], []
        for arr, short, same in ((xs, 2, 16), (ys, 4, 32)):
            v = 0
            for f in flags:
                if f & short:
                    dv = d[p]; p += 1
                    v += dv if f & same else -dv
                elif not f & same:
                    v += struct.unpack(">h", d[p:p + 2])[0]; p += 2
                arr.append(v)
        res, s = [], 0
        for e in ends:
            res.append(list(zip(xs[s:e + 1], ys[s:e + 1])))
            s = e + 1
        return res

    def glyph(self, ch):
        """Returns (bitmap uint8 HxW 0/1, left, top_from_baseline, advance_px)."""
        if ch in self.cache:
            return self.cache[ch]
        gid = self.cmap.get(ord(ch), self.cmap.get(ord("?"), 0))
        adv = int(round(self.advance(gid) * self.scale))
        cs = [[(x * self.scale, y * self.scale) for x, y in c] for c in self.contours(gid)]
        if not cs:
            r = (np.zeros((0, 0), np.uint8), 0, 0, adv)
            self.cache[ch] = r
            return r
        allx = [x for c in cs for x, _ in c]
        ally = [y for c in cs for _, y in c]
        x0, x1 = int(np.floor(min(allx))), int(np.ceil(max(allx)))
        y0, y1 = int(np.floor(min(ally))), int(np.ceil(max(ally)))
        h, w = y1 - y0, x1 - x0
        bm = np.zeros((h, w), np.uint8)
        edges = []
        for c in cs:
            for i in range(len(c)):
                a, b = c[i], c[(i + 1) % len(c)]
                if a[1] != b[1]:
                    edges.append((a, b))
        for row in range(h):
            yc = y1 - row - 0.5  # font units up
            xs = []
            for (ax, ay), (bx, by) in edges:
                if (ay <= yc < by) or (by <= yc < ay):
                    t = (yc - ay) / (by - ay)
                    xs.append((ax + t * (bx - ax), 1 if by > ay else -1))
            xs.sort()
            wnd = 0
            for i in range(len(xs) - 1):
                wnd += xs[i][1]
                if wnd != 0:
                    for col in range(w):
                        xc = x0 + col + 0.5
                        if xs[i][0] <= xc < xs[i + 1][0]:
                            bm[row, col] = 1
        r = (bm, x0, y1, adv)
        self.cache[ch] = r
        return r


_FONTS = {}

# name -> (file, pixel size, ascent px used for line layout, line height)
FACES = {
    "8": ("OpenCityPixel8.ttf", 8),
    "10": ("OpenCityPixel10.ttf", 10),
    "12": ("OpenCityPixel12.ttf", 12),
    "reg": ("OpenCityPixel-Regular.ttf", 16),
    "bold": ("OpenCityPixel-Bold.ttf", 16),
}


def font(name):
    if name not in _FONTS:
        f, px = FACES[name]
        _FONTS[name] = TTF(os.path.join(FONT_DIR, f), px)
    return _FONTS[name]


def measure(text, face="10"):
    f = font(face)
    return sum(f.glyph(c)[3] for c in text)


def cap_height(face):
    bm, _, top, _ = font(face).glyph("H")
    return top


def render_mask(text, face="10"):
    """Returns (mask HxW bool, baseline_row). Height spans ascent..descent of the face."""
    f = font(face)
    asc = int(round(f.ascent * f.scale))
    desc = int(round(-f.descent * f.scale))
    w = max(1, measure(text, face))
    m = np.zeros((asc + desc, w), bool)
    x = 0
    for c in text:
        bm, left, top, adv = f.glyph(c)
        if bm.size:
            yy = asc - top
            xx = x + left
            h, ww = bm.shape
            ys0, xs0 = max(0, -yy), max(0, -xx)
            ys1, xs1 = min(h, m.shape[0] - yy), min(ww, m.shape[1] - xx)
            if ys1 > ys0 and xs1 > xs0:
                m[yy + ys0:yy + ys1, xx + xs0:xx + xs1] |= bm[ys0:ys1, xs0:xs1].astype(bool)
        x += adv
    return m, asc
