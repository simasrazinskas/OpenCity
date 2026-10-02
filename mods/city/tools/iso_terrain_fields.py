"""Farmland crop-row patterns for iso terrain tiles (top faces: u = x*32, v = y*32 texels)."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.palette import RAMP  # noqa: E402
from isokit.noise import hash2, fbm  # noqa: E402

FL = np.floor


def pat_field(c, m):
    """Crop rows. Params: stage in plowed|sown|sprout|growing|ripe|stubble|flowering|rows,
    crop ramp (crop), axis 'x'|'y' (row direction), period (texels)."""
    per = m.p("period", 4)
    along = c.u if m.p("axis", "x") == "y" else c.v      # rows run along x -> stripes across v
    across = c.v if m.p("axis", "x") == "y" else c.u
    r = FL(along) % per
    ridge = r < per / 2
    furrow = r == per - 1
    stage = m.p("stage", "plowed")
    crop = RAMP[m.p("crop", "grass")]
    ct = m.p("crop_tone", 0.0)
    h = hash2(FL(across), FL(along), 7)
    c.tone += (fbm(c.sx, c.sy * 2.0, 10.0, 2, 3) - 0.5) * 0.8
    c.tone[furrow] -= 1.2
    c.tone[r == 0] += 0.6
    if stage == "plowed":
        return
    if stage == "sown":
        sel = ridge & (h < 0.12)
    elif stage == "sprout":
        sel = ridge & (FL(across) % 2 == 0) & (h < 0.8)
    elif stage == "growing":
        sel = (r < per - 1) & (h < 0.92)
    elif stage in ("ripe", "flowering"):
        sel = (r < per - 1) | (h < 0.5)
    elif stage == "stubble":
        sel = (FL(across) % 2 == 0) & (h < 0.7)
    elif stage == "rows":        # vegetables: separate round heads
        sel = ridge & (FL(across / 2) % 2 == 0)
    else:
        sel = np.zeros_like(ridge)
    c.ramp[sel] = crop
    c.tone[sel] = ct + (h[sel] - 0.5) * 1.2
    c.tone[sel & (r == 0)] += 0.8
    c.tone[sel & furrow] -= 1.0
    if stage == "flowering":
        fl = sel & (hash2(c.sx, c.sy, 9) < 0.3)
        c.tone[fl] += 1.5
    if stage == "ripe" and m.p("heads", False):
        hd = sel & (hash2(c.sx, c.sy, 11) < 0.12)
        c.ramp[hd] = RAMP[m.p("heads", "yellow")]
        c.tone[hd] = 1.0


SOIL = ik.mat("dirt", ramp="wood", shade=-0.8, pattern=pat_field)

CROPS = {
    # name: [(stage label, params)]
    "wheat": [("plowed", {}), ("sown", dict(crop="grass", crop_tone=-1.0)),
              ("sprout", dict(crop="grass", crop_tone=-1.2)), ("growing", dict(crop="grass", crop_tone=-1.0)),
              ("ripening", dict(stage="ripe", crop="olive", crop_tone=0.0)),
              ("ripe", dict(stage="ripe", crop="yellow", crop_tone=-1.2)),
              ("stubble", dict(stage="stubble", crop="sand", crop_tone=-0.5))],
    "corn": [("sprout", dict(crop="grass", crop_tone=-1.2)), ("growing", dict(crop="leaf", crop_tone=0.0)),
             ("ripe", dict(stage="ripe", crop="olive", crop_tone=-0.5, heads="yellow"))],
    "rapeseed": [("growing", dict(crop="leaf", crop_tone=0.5)),
                 ("flowering", dict(stage="flowering", crop="yellow", crop_tone=0.0))],
    "vegetables": [("sprout", dict(crop="grass", crop_tone=-0.8)),
                   ("ripe", dict(stage="rows", crop="leaf", crop_tone=1.0))],
    "sunflower": [("growing", dict(crop="leaf", crop_tone=0.0)),
                  ("ripe", dict(stage="ripe", crop="leaf", crop_tone=-0.5, heads="yellow"))],
    "lavender": [("ripe", dict(stage="ripe", crop="purple", crop_tone=-0.3))],
}


def field_material(crop, stage_label, axis="x"):
    for lab, params in CROPS[crop]:
        if lab == stage_label:
            p = dict(params)
            p.setdefault("stage", lab)
            return SOIL.with_(axis=axis, **p)
    raise KeyError((crop, stage_label))
