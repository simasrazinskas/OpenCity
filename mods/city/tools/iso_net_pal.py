"""
iso_net_pal.py - NET surface colours, all taken from the isokit palette (ramp, shade) so ground tiles,
props and the other workstreams' art share one RCT2-like palette. Everything is also quantized with
isokit.nearest() when saved.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402


def C(ramp, shade):
    return np.array(ik.color(ramp, shade), np.float32)


# asphalt (cool dark grey) and its wear tones
ASPH = C("grey", 3)
ASPH_L = C("grey", 4)
ASPH_D = C("grey", 2)
ASPH_PATCH = C("grey", 2)
ASPH_PATCH_OLD = C("grey", 5)
CRACK = C("grey", 1)
HWY = C("slate", 2)
HWY_L = C("slate", 3)
# sidewalk paving, kerb stone (sidewalk top = stone 8; kerb = stone 10; kerb face via render shading)
WALK = C("stone", 8)
WALK_J = C("stone", 7)
WALK_D = C("stone", 7)
KERB = C("stone", 10)
# paint
WHITE = C("grey", 11)
YELLOW = C("yellow", 8)
PAINT_OLD = C("grey", 8)
BUS_RED = C("brick", 5)
BIKE_GREEN = C("leaf", 7)
# alley concrete, gravel, verge
CONC = C("grey", 7)
CONC_J = C("grey", 6)
GRAVEL = C("sand", 5)
GRAVEL_L = C("sand", 7)
GRAVEL_D = C("sand", 4)
RUT = C("sand", 4)
SHOULDER = C("stone", 6)
# metal / dark
IRON = C("grey", 1)
IRON_L = C("grey", 4)
GRASS = C("grass", 7)
GRASS_D = C("grass", 6)
GRASS_L = C("grass", 8)


def quantize(img):
    """Map every opaque pixel of an HxWx4 image to the nearest isokit palette colour."""
    img = np.asarray(img, np.float32).copy()
    m = img[..., 3] >= 128
    if m.any():
        img[m, :3] = ik.nearest(img[m, :3])
    return img
