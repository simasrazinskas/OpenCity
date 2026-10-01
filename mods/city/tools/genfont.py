#!/usr/bin/env python3
"""Generate the OpenCity UI pixel fonts (OpenCityPixel-Regular/Bold.ttf).

Dev-only tool; its outputs (mods/city/fonts/*.ttf) are committed, so normal
builds and players never need to run it.  Requires fontTools:

    pip install fonttools

OpenCity Pixel is "Pixel Operator" by Jayvee Enaguas (HarvettFox96), CC0 1.0,
with a handful of extra glyphs (currency/legal/superscript/fraction signs,
arrows, triangles, check/cross, comparison signs, infinity, circles, star,
heart) drawn as pixel bitmaps in the matching style.  Pixel Operator is
unitsPerEm 1600 with 100 font units = 1 px (em 16 px, ascent 13, descent 3),
so every outline coordinate here stays on the 100-unit grid and renders
crisply (no grey) at exactly 16 px.

Usage:
    python3 genfont.py [--src DIR]

--src DIR   directory containing PixelOperator.ttf and PixelOperator-Bold.ttf
            (default: download them from the pinned upstream commit).
Output goes to ../fonts/ relative to this script.  The script is idempotent.
"""
import argparse
import os
import sys
import tempfile
import urllib.request

from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

UPSTREAM = "https://github.com/bauripalash/ttf-pixeloperator-fork"
COMMIT = "496045526d00b66e37ca11762c446b7423145822"
RAW = "https://raw.githubusercontent.com/bauripalash/ttf-pixeloperator-fork/%s/ttf/%%s" % COMMIT

PX = 100  # font units per pixel

# ---------------------------------------------------------------------------
# Glyph bitmaps.  '#' = ink, '.' = empty.  `top` is the height in pixels of
# the top edge of the first row above the baseline; row i then covers
# y = top - i - 1 .. top - i.  Left bearing is 1 px and advance = width + 2 px,
# like Pixel Operator.  The Bold weight is derived by thickening every ink
# pixel one pixel to the right (2-px stems, as in Pixel Operator Bold) unless
# an explicit bold bitmap is supplied.
# ---------------------------------------------------------------------------
G = {}


def glyph(cp, top, rows, bold=None, bold_top=None):
    G[cp] = dict(top=top, rows=rows.split(), bold=bold.split() if bold else None,
                 bold_top=bold_top if bold_top is not None else top)


glyph(0x00A4, 7, """
#...#
.###.
.#.#.
.###.
#...#
""")
glyph(0x00A7, 9, """
.###.
#...#
#....
.###.
#...#
.###.
....#
#...#
.###.
""")
glyph(0x00AA, 9, """
.##.
...#
.###
#..#
.###
....
####
""")
glyph(0x00BA, 9, """
.##.
#..#
#..#
.##.
....
####
""")
glyph(0x00AF, 9, "#####")
glyph(0x00B9, 9, """
.#.
##.
.#.
.#.
###
""")
glyph(0x00B2, 9, """
##.
..#
.#.
#..
###
""")
glyph(0x00B3, 9, """
##.
..#
.##
..#
##.
""")

# fractions: 3x4 numerator, 45-degree slash, 3x4 denominator (9 px wide)
glyph(0x00BC, 9, """
.#......#
##.....#.
.#....#..
###..#...
....#....
...#..#.#
..#...#.#
.#....###
#.......#""", bold="""
.##.....##
###....##.
.##...##..
####.##...
....##....
...##..#.#
..##...#.#
.##....###
##.......#
""")
glyph(0x00BD, 9, """
.#......#
##.....#.
.#....#..
###..#...
....#....
...#..##.
..#.....#
.#.....#.
#.....###
""")
glyph(0x00BE, 9, """
##......#
..#....#.
.##...#..
##...#...
....#....
...#..#.#
..#...#.#
.#....###
#.......#""", bold="""
###.....##
..##...##.
.###..##..
###..##...
....##....
...##..#.#
..##...#.#
.##....###
##.......#
""")

# arrows
glyph(0x2192, 7, """
....#..
.....#.
#######
.....#.
....#..
""")
glyph(0x2190, 7, """
..#....
.#.....
#######
.#.....
..#....
""")
glyph(0x2191, 8, """
..#..
.###.
#.#.#
..#..
..#..
..#..
..#..
""")
glyph(0x2193, 8, """
..#..
..#..
..#..
..#..
#.#.#
.###.
..#..
""")
glyph(0x25B2, 7, """
...#...
..###..
.#####.
#######
""")
glyph(0x25BC, 7, """
#######
.#####.
..###..
...#...
""")
glyph(0x25B6, 8, """
#...
##..
###.
####
###.
##..
#...
""")
glyph(0x25C0, 8, """
...#
..##
.###
####
.###
..##
...#
""")
glyph(0x2713, 7, """
......#
.....#.
#...#..
.#.#...
..#....
""")
glyph(0x2717, 8, """
#.....#
.#...#.
..#.#..
...#...
..#.#..
.#...#.
#.....#
""")
glyph(0x2212, 5, "#####")
glyph(0x2264, 7, """
..#
.#.
#..
.#.
..#
...
###
""")
glyph(0x2265, 7, """
#..
.#.
..#
.#.
#..
...
###
""")
glyph(0x221E, 6, """
.##.##.
#..#..#
.##.##.
""")
glyph(0x25CF, 8, """
..###..
.#####.
#######
#######
#######
.#####.
..###..
""")
glyph(0x25CB, 8, """
..###..
.#...#.
#.....#
#.....#
#.....#
.#...#.
..###..
""")
glyph(0x2605, 8, """
...#...
..###..
#######
.#####.
..###..
.##.##.
##...##
""")
glyph(0x2665, 7, """
.##.##.
#######
#######
.#####.
..###..
...#...
""")

# soft hyphen is mapped straight onto the existing hyphen glyph
ALIASES = {0x00AD: 0x002D}


def embolden(rows):
    """Thicken each ink pixel one pixel to the right (Bold = 2-px stems)."""
    w = len(rows[0]) + 1
    out = []
    for r in rows:
        cells = ["."] * w
        for i, ch in enumerate(r):
            if ch == "#":
                cells[i] = "#"
                cells[i + 1] = "#"
        out.append("".join(cells))
    return out


def rects(rows):
    """Decompose a bitmap into rectangles (x0, y0, x1, y1) in pixel cells
    (y counted in rows from the top); identical runs on adjacent rows merge."""
    done, active = [], {}
    for y, r in enumerate(rows + ["." * len(rows[0])]):
        runs, x = set(), 0
        while x < len(r):
            if r[x] == "#":
                x0 = x
                while x < len(r) and r[x] == "#":
                    x += 1
                runs.add((x0, x))
            else:
                x += 1
        for run in list(active):
            if run not in runs:
                done.append((run[0], active.pop(run), run[1], y))
        for run in runs:
            active.setdefault(run, y)
    return sorted(done, key=lambda t: (t[1], t[0]))


def make_glyph(rows, top):
    pen = TTGlyphPen(None)
    for x0, y0, x1, y1 in rects(rows):
        # clockwise outer contour (TrueType): up, right, down, close
        xl, xr = (1 + x0) * PX, (1 + x1) * PX
        yt, yb = (top - y0) * PX, (top - y1) * PX
        pen.moveTo((xl, yb))
        pen.lineTo((xl, yt))
        pen.lineTo((xr, yt))
        pen.lineTo((xr, yb))
        pen.closePath()
    return pen.glyph()


def build(src, dst, bold, family="OpenCity Pixel"):
    font = TTFont(src, recalcTimestamp=False)
    assert font["head"].unitsPerEm == 1600
    glyf, hmtx = font["glyf"], font["hmtx"]
    order = list(font.getGlyphOrder())
    cmap_tables = [t for t in font["cmap"].tables
                   if t.isUnicode() and t.format in (4, 12)]
    best = font.getBestCmap()

    for cp, g in sorted(G.items()):
        assert cp not in best, "U+%04X already present" % cp
        rows = g["rows"] if not bold else (g["bold"] or embolden(g["rows"]))
        top = g["bold_top"] if bold else g["top"]
        assert len({len(r) for r in rows}) == 1, hex(cp)
        name = "uni%04X" % cp
        gl = make_glyph(rows, top)
        gl.recalcBounds(glyf)
        order.append(name)
        glyf.glyphs[name] = gl
        hmtx.metrics[name] = ((len(rows[0]) + 2) * PX, gl.xMin)
        for t in cmap_tables:
            t.cmap[cp] = name
    for cp, target in ALIASES.items():
        for t in cmap_tables:
            t.cmap[cp] = best[target]

    font.setGlyphOrder(order)
    glyf.glyphOrder = order
    font["maxp"].numGlyphs = len(order)

    style = "Bold" if bold else "Regular"
    ps = "OpenCityPixel-" + style
    names = {1: family, 2: style, 3: ps + ":1.0:genfont",
             4: family + (" Bold" if bold else ""), 6: ps}
    for rec in font["name"].names:
        if rec.nameID in names:
            rec.string = names[rec.nameID]
        elif rec.nameID == 16:
            rec.string = family
        elif rec.nameID == 17:
            rec.string = style
    font["OS/2"].recalcUnicodeRanges(font)
    font.save(dst)


def fetch(tmp):
    for fn in ("PixelOperator.ttf", "PixelOperator-Bold.ttf"):
        urllib.request.urlretrieve(RAW % fn, os.path.join(tmp, fn))
    return tmp


def main():
    ap = argparse.ArgumentParser(description="Generate OpenCity Pixel fonts from Pixel Operator.")
    ap.add_argument("--src", help="dir with PixelOperator.ttf / PixelOperator-Bold.ttf")
    args = ap.parse_args()
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fonts")
    os.makedirs(out, exist_ok=True)
    tmp = None
    src = args.src
    if not src:
        tmp = tempfile.TemporaryDirectory()
        src = fetch(tmp.name)
    build(os.path.join(src, "PixelOperator.ttf"), os.path.join(out, "OpenCityPixel-Regular.ttf"), False)
    build(os.path.join(src, "PixelOperator-Bold.ttf"), os.path.join(out, "OpenCityPixel-Bold.ttf"), True)
    print("wrote", os.path.normpath(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
