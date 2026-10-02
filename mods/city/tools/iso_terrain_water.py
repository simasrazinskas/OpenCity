"""Water, shore and coast tiles (beach and grass-bank styles) for the iso terrain set.

Water sits WL px below land, so banks and beaches show as lit slopes like RCT2. All shore tiles follow
the genterrain blob scheme (isokit.tiles.shore_combos): land tiles have water on the masked sides,
water tiles have land on the masked sides.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402
from isokit.palette import RAMP  # noqa: E402
from isokit.details import paint  # noqa: E402
from isokit.surfaces import pat_water  # noqa: E402
from isokit.noise import hash2  # noqa: E402

WL = -5.0                     # water level (px)
FRAMES = 8
BEACH_W = 0.26                # sand band on land (cells)
WOB = 0.05


def bed_z(d, style):
    if style == "beach":
        return np.clip(-d / 0.42 * 8.0, -12, 0)
    return np.clip(-(d - 0.01) / 0.06 * 8.0, -12, 0)


def pat_shore_water(c, m):
    """Water with shallows, a foam line where the bed meets the surface, animated with c.frame."""
    pat_water(c, m)
    mask, corners, style = m.p("mask", 0), m.p("corners", 0), m.p("style", "beach")
    if not (mask or corners):
        return
    d = ik.edge_distance(c.x, c.y, mask, corners, WOB)
    d_wl = 0.42 * (-WL) / 8.0 if style == "beach" else 0.01 + 0.06 * (-WL) / 8.0
    k = d - d_wl
    sh = k < 0.3
    c.tone[sh] += (1 - k[sh] / 0.3) * 1.8
    teal = k < 0.12
    c.ramp[teal] = RAMP["teal"]
    c.tone[teal] += 0.6
    ph = 2 * np.pi * (c.frame % FRAMES) / FRAMES
    foam_at = 0.025 + 0.015 * np.sin(ph + c.x * 3 + c.y * 3)
    foam = (k < foam_at) | ((np.abs(k - 0.07 - 0.02 * np.sin(ph)) < 0.012) & (hash2(c.sx, c.sy, 5) < 0.6))
    c.ramp[foam] = RAMP["snow"]
    c.tone[foam] = 0.5 - m.shade
    # behind back-facing bank/beach slopes the bed is above water: show the land surface instead
    paint(c, ik.mat("sand", shade=-0.3) if style == "beach" else ik.mat("dirt", shade=-1.4), k < 0, m.shade)


def pat_shore_land(c, m):
    """Land next to water: beach sand band (beach) or a dark earthy lip (bank)."""
    from isokit.surfaces import pat_grass
    pat_grass(c, m)
    mask, corners, style = m.p("mask", 0), m.p("corners", 0), m.p("style", "beach")
    d = ik.edge_distance(c.x, c.y, mask, corners, WOB)
    if style == "beach":
        edge = d < BEACH_W + (hash2(c.sx, c.sy, 3) - 0.5) * 0.03
        paint(c, "sand", edge, m.shade)
        rim = (d >= BEACH_W - 0.03) & (d < BEACH_W + 0.03) & (hash2(c.sx, c.sy, 4) < 0.5)
        c.ramp[rim] = RAMP["olive"]
    else:
        lip = d < 0.035
        c.tone[lip] -= 1.2
        paint(c, ik.mat("dirt", shade=-1.0), d < 0.012, m.shade)


def water_mat(depth="open", **kw):
    shade = {"shallow": 0.4, "open": -0.5, "deep": -1.6}[depth]
    return ik.mat("water", shade=shade, pattern=pat_shore_water, **kw)


def water_tile(mask=0, corners=0, style="beach", frame=0, depth="open", origin=(0, 0), night=False):
    s = ik.Scene((1, 1))
    if mask or corners:
        zf = lambda x, y: bed_z(ik.edge_distance(x, y, mask, corners, WOB), style)
        bank = ik.mat("dirt", shade=-0.6) if style == "bank" else None
        s.heightfield(0, 0, 1, 1, zf, ik.mat("sand", shade=-0.3) if style == "beach" else ik.mat("mud"),
                      n=24 if style == "bank" else 20, steep=bank, steep_at=0.75)
    s.ground([(-0.5, -0.5), (1.5, -0.5), (1.5, 1.5), (-0.5, 1.5)],
             water_mat(depth, mask=mask, corners=corners, style=style), z=WL, layer=5)
    return ik.render_tile(s, origin, frame=frame, night=night)


def land_tile(mask=0, corners=0, style="beach", origin=(0, 0), season="summer", night=False):
    s = ik.Scene((1, 1))
    s.tile(0, 0, ik.mat("grass", pattern=pat_shore_land, mask=mask, corners=corners, style=style))
    return ik.render_tile(s, origin, season=season, night=night)
