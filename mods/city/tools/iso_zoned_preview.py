#!/usr/bin/env python3
"""Preview sheet for one zone/footprint: rows = variants, columns = levels 1-5, then a states row.

    python3 mods/city/tools/iso_zoned_preview.py res-low 1x1 /tmp/p.png [--k 3] [--states] [--night] [--winter]
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402

import iso_zoned  # noqa: E402,F401  (registers zones)
import iso_zoned_core as C  # noqa: E402
from isokit.png import write_png  # noqa: E402


def main():
    a = sys.argv[1:]
    prefix, fp, out = a[0], a[1], a[2]
    k = int(a[a.index("--k") + 1]) if "--k" in a else 3
    night = "--night" in a
    season = "winter" if "--winter" in a else "summer"
    sprs = []
    clipped = 0
    for v in range(C.VARIANTS):
        for L in range(1, 6):
            s, n = C.render(C.build(prefix, fp, L, v, season=season), night=night, season=season)
            sprs.append(s)
            clipped += n
    if "--states" in a:
        for st, stage in (("ok", 1), ("ok", 2), ("ok", 3), ("abandoned", 0), ("burnt", 0), ("collapsed", 0)):
            s, n = C.render(C.build(prefix, fp, 3, 0, st, stage))
            sprs.append(s)
        for f in (1, 2, 3):
            sprs.append(C.render(C.build(prefix, fp, 3, 0), facing=f)[0])
    img = C.sheet(sprs, 5)
    img = np.repeat(np.repeat(img, k, 0), k, 1)
    rgba = np.concatenate([img, np.full(img.shape[:2] + (1,), 255, np.uint8)], 2)
    write_png(out, rgba)
    print("clipped px:", clipped, "size", img.shape)


if __name__ == "__main__":
    main()
