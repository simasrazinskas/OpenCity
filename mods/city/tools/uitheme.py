"""Shared Dune 2000 style palette (sand / gold / bronze on dark rust) for the OpenCity UI generators.

The names TEAL/WHITE/SLATE/... are kept from the first (modern) theme so the icon recipes did not
have to change: TEAL is now the orange-bronze accent, WHITE is light sand, SLATE is dark brown.
"""

from uidraw import col

# ---- palette ------------------------------------------------------------
WHITE = (250, 228, 180, 255)        # light sand (main glyph colour)
SAND = (232, 196, 138, 255)
GOLD = (255, 205, 100, 255)
TEAL = (226, 128, 40, 255)          # accent: orange bronze
TEAL_LIGHT = (255, 214, 120, 255)   # bright gold
TEAL_DARK = (140, 76, 24, 255)
GREY = (178, 142, 102, 255)
GREY_DIM = (112, 86, 60, 255)
SLATE = (46, 22, 12, 255)           # dark brown (holes / windows)
SLATE_DARK = (24, 11, 6, 255)
OUTLINE = (28, 12, 5, 255)
RED = (226, 64, 44, 255)
AMBER = (255, 196, 64, 255)
GREEN = (126, 190, 70, 255)
BLUE = (96, 156, 226, 255)

BRONZE_LIGHT = (246, 190, 108, 255)
BRONZE = (196, 120, 46, 255)
BRONZE_DARK = (104, 56, 22, 255)

ZONE = {
    'res-low': (126, 217, 87, 255),
    'res-high': (46, 158, 62, 255),
    'com-low': (90, 180, 240, 255),
    'com-high': (44, 111, 209, 255),
    'ind': (242, 201, 76, 255),
    'off': (176, 124, 232, 255),
    'res-row': (168, 224, 99, 255),       # #A8E063
    'res-med': (79, 191, 79, 255),        # #4FBF4F
    'res-mixed': (143, 211, 200, 255),    # #8FD3C8
    'res-lowrent': (47, 111, 63, 255),    # #2F6F3F
    'off-high': (128, 74, 204, 255),
    'warehouse': (201, 162, 39, 255),     # #C9A227
}
