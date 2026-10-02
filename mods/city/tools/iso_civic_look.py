#!/usr/bin/env python3
"""Preview helper: python3 iso_civic_look.py OUT.png K file1.png [file2.png ...] (paths relative to
design/iso/civic). Writes an upscaled grid (4 columns) on a flat background for inspection."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.png import read_png  # noqa: E402

D = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "design", "iso", "civic")

if __name__ == "__main__":
    out, k, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
    cols = 4
    if files and files[0].startswith("cols="):
        cols = int(files.pop(0)[5:])
    imgs = [read_png(os.path.join(D, f)) for f in files]
    ik.inspect(out, ik.grid(imgs, min(cols, len(imgs))), k=k)
