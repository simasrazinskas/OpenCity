#!/usr/bin/env python3
"""Generate the small OpenCity UI pixel faces (OpenCityPixel8/10/12.ttf).

Dev-only tool; its outputs (mods/city/fonts/*.ttf) are committed.  Requires fontTools:

    pip install fonttools

The faces are Latin/symbol subsets of TakWolf's OFL pixel fonts (no Reserved Font Name), renamed:
  OpenCityPixel8.ttf   <- Fusion Pixel Font 8px proportional (latin)   8 px grid, cap 6
  OpenCityPixel10.ttf  <- Ark Pixel Font 10px proportional (latin)     10 px grid, cap 7
  OpenCityPixel12.ttf  <- Ark Pixel Font 12px proportional (latin)     12 px grid, cap 9
Together with OpenCityPixel-Regular/Bold (16 px grid, cap 9) and Tiny5 (8 px grid, cap 5) they give the engine
enough design sizes to keep text within ~15% of its intended size at every UI scale (FontData.PixelFaces).

Usage: python3 genfontsubset.py [--src DIR]   (DIR holds the three upstream latin .ttf files; default: download)
"""
import argparse
import io
import os
import urllib.request
import zipfile

from fontTools import subset
from fontTools.ttLib import TTFont

RELEASE = "2026.09.25"
SOURCES = [
    ("fusion-pixel-font", "fusion-pixel-font-8px-proportional-ttf", "fusion-pixel-8px-proportional-latin.ttf", "OpenCityPixel8.ttf",
     "OpenCity Pixel 8"),
    ("ark-pixel-font", "ark-pixel-font-10px-proportional-ttf", "ark-pixel-10px-proportional-latin.ttf", "OpenCityPixel10.ttf",
     "OpenCity Pixel 10"),
    ("ark-pixel-font", "ark-pixel-font-12px-proportional-ttf", "ark-pixel-12px-proportional-latin.ttf", "OpenCityPixel12.ttf",
     "OpenCity Pixel 12"),
]

# Latin-1, Latin Extended-A, general punctuation, currency, arrows, maths, geometric shapes, misc symbols, dingbats
UNICODES = [*range(0x20, 0x7F), *range(0xA0, 0x180), *range(0x2010, 0x2070), *range(0x20A0, 0x20D0), *range(0x2190, 0x2200),
            *range(0x2200, 0x2300), *range(0x25A0, 0x2600), *range(0x2600, 0x2700), *range(0x2700, 0x27C0)]


def fetch(repo, asset, member):
    url = f"https://github.com/TakWolf/{repo}/releases/download/{RELEASE}/{asset}-v{RELEASE}.zip"
    data = urllib.request.urlopen(url, timeout=600).read()
    return zipfile.ZipFile(io.BytesIO(data)).read(member)


def rename(font, family):
    for rec in font["name"].names:
        if rec.nameID in (1, 4, 16):
            rec.string = family
        elif rec.nameID == 3:
            rec.string = family + " " + RELEASE
        elif rec.nameID == 6:
            rec.string = family.replace(" ", "")
        elif rec.nameID in (2, 17):
            rec.string = "Regular"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", help="directory containing the upstream latin .ttf files")
    args = ap.parse_args()
    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fonts")
    for repo, asset, member, out, family in SOURCES:
        if args.src:
            with open(os.path.join(args.src, member), "rb") as f:
                data = f.read()
        else:
            data = fetch(repo, asset, member)

        font = TTFont(io.BytesIO(data))
        options = subset.Options()
        options.layout_features = []
        options.hinting = False
        options.name_IDs = ["*"]
        options.notdef_outline = True
        sub = subset.Subsetter(options)
        sub.populate(unicodes=UNICODES)
        sub.subset(font)
        rename(font, family)
        path = os.path.join(out_dir, out)
        font.save(path)
        print(out, os.path.getsize(path), "bytes,", len(font.getBestCmap()), "chars")


if __name__ == "__main__":
    main()
