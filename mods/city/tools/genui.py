#!/usr/bin/env python3
"""
genui - procedurally generates the OpenCity-specific UI art (Dune 2000 style).

  python3 -B mods/city/tools/genui.py [--fast]     (--fast renders only the 1x images)

The base chrome (chrome.yaml, bits/chrome/{chrome,dialog,glyphs*}.png) is OpenRA's own D2k UI art
and is NOT touched by this script. Outputs (all under mods/city/):
  bits/chrome/cityicons{,-2x,-3x}.png    city-icons (32px), city-icons-small (16px) and the
                                         city-panel/-toolbar/-tooltip/-button 9-slice styles
  chrome-city.yaml                       regions for the above
  bits/chrome/loadscreen{,-2x,-3x}.png   logo (0,0,256,256) + tiled stripe (258,0,253,256)
  icon.png, icon-2x.png, icon-3x.png     mod icons
  bits/cursors/cursors.png + cursors.yaml

Only the python3 standard library is used (see pngkit.py / uidraw.py / uistyle.py).
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
MOD = os.path.dirname(HERE)
CHROME = os.path.join(MOD, 'bits', 'chrome')

from uitheme import *  # noqa: E402,F401,F403
from uicityicons import ICONS, ALIASES  # noqa: E402

SCALES = [1] if '--fast' in sys.argv else [1, 2, 3]


def suffix(s):
    return '' if s == 1 else '-%dx' % s


def save_atlas(atlas, base):
    placed = atlas.layout()
    for s in SCALES:
        cv, _ = atlas.render(s)
        cv.save(os.path.join(CHROME, base + suffix(s) + '.png'))
    return placed


if __name__ == '__main__':
    from genextra import build_city, build_loadscreen_and_icons, build_cursors
    build_city(SCALES, MOD, ICONS, ALIASES, save_atlas)
    import uibuildicons
    uibuildicons.build(MOD)
    build_loadscreen_and_icons(SCALES, MOD)
    build_cursors(MOD)
    print('genui: done (scales %s)' % SCALES)
