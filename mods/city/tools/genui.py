#!/usr/bin/env python3
"""
genui - regenerates the OpenCity UI art that is still produced offline.

  python3 -B mods/city/tools/genui.py [--fast]     (--fast renders only the 1x window icons)

The chrome itself (panels, buttons, icons, glyphs, sidebar, logo, load screen) is drawn at runtime at the exact
device scale by OpenRA.Mods.City/UIArt (CityChromeGenerator); colours are in mods/city/uistyle.yaml. This script:
  bits/chrome/cityicons.vec                vector ops of the icon/glyph recipes (uiexport.py: uicityicons*.py, uibaseglyphs.py)
  (the RCT2 icon and build-thumbnail atlases bits/chrome/iso/ come from iso_ui_export.py)
  icon.png, icon-2x.png, icon-3x.png       window icons (uilogo.py)
  bits/cursors/cursors.png + cursors.yaml

Only the python3 standard library is used (see pngkit.py / uidraw.py / uistyle.py).
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
MOD = os.path.dirname(HERE)
CHROME = os.path.join(MOD, 'bits', 'chrome')


SCALES = [1] if '--fast' in sys.argv else [1, 2, 3]


if __name__ == '__main__':
    from genextra import build_mod_icons, build_cursors
    import uiexport
    uiexport.main()
    build_mod_icons(SCALES, MOD)
    build_cursors(MOD)
    print('genui: done (scales %s)' % SCALES)
