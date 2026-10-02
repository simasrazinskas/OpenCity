"""
iso_life_palette - LIFE materials on the shared isokit palette (18 ramps x 12 shades).

PAL maps a material to (isokit ramp, shade offset in ramp steps). The lit level of a face (0..1, same light as
isokit: top ~0.75, left/+Y ~0.51, right/+X ~0.22) picks index round(level * 11) + offset.
EMIT maps emissive materials (lamps on, sirens, lit glass) to a fixed (ramp, index): they ignore lighting.
BASE keeps hex colours for the 2D helpers (people, FX, markers) that draw by hand; those are quantized with
`isokit.nearest` where exact palette entries matter.
"""
from iso_life_common import make_ramp

try:
    import isokit as ik
    PALETTE = ik.PALETTE
    RAMP = ik.palette.RAMP
except Exception:          # isokit missing: never expected after ISOKIT-M1
    ik = None

BODY_COLOURS = {
    "red": "#b8312a", "blue": "#2f5fa8", "white": "#d8d8d0", "black": "#3a3c44",
    "silver": "#9aa0a6", "green": "#3f7a3a", "yellow": "#d8b030", "teal": "#2f8a8a",
    "brown": "#7a5236", "orange": "#d0702a", "purple": "#6a4a8a", "beige": "#c8b48a",
}
BODY_IK = {
    "red": ("red", 0), "blue": ("water", 0), "white": ("grey", 3), "black": ("slate", -4),
    "silver": ("slate", 2), "green": ("leaf", 0), "yellow": ("yellow", 0), "teal": ("teal", 0),
    "brown": ("wood", -1), "orange": ("terra", 0), "purple": ("purple", 0), "beige": ("sand", 1),
}

PAL = {
    "body": ("red", 0), "glass": ("glass", -3), "tyre": ("grey", -5), "chassis": ("grey", -4),
    "chrome": ("grey", 2), "black": ("slate", -4), "head": ("yellow", 4), "tail": ("red", -2),
    "white": ("grey", 3), "grey": ("grey", 0), "dkgrey": ("grey", -3), "yellow": ("yellow", 0),
    "taxi": ("yellow", 1), "red": ("red", 0), "fire": ("red", 0), "blue": ("water", 0), "police": ("water", -3),
    "green": ("leaf", 0), "orange": ("terra", 1), "post": ("yellow", 0), "rust": ("brick", -1),
    "wood": ("wood", 1), "log": ("wood", 0), "ore": ("stone", -3), "silver": ("slate", 3), "cream": ("sand", 3),
    "tram": ("red", 0), "metro": ("water", 0), "train": ("water", -1), "loco": ("terra", 0),
    "hull": ("slate", -2), "hullred": ("red", -2), "deck": ("stone", 0),
    "container1": ("brick", 1), "container2": ("water", 1), "container3": ("leaf", 1), "container4": ("yellow", 0),
    "container1_rib": ("brick", 0), "container2_rib": ("water", 0), "container3_rib": ("leaf", 0),
    "container4_rib": ("yellow", -1), "white_rib": ("grey", 2),
    "rotor": ("slate", -5), "hazard": ("yellow", 1), "taxi_sign": ("sand", 4),
    "siren_a": ("red", 1), "siren_b": ("water", 1), "beacon": ("terra", 2), "dest": ("slate", -5),
    "tyre_hub": ("grey", 0), "wreck": ("stone", -4), "amber": ("terra", 2), "wine": ("rose", -4),
    "occ": ("grey", -20),
}

EMIT = {
    "head_on": ("yellow", 11), "tail_on": ("red", 8), "siren_r": ("red", 9), "siren_bl": ("water", 10),
    "beacon_on": ("terra", 10), "dest_on": ("terra", 9), "glass_lit": ("yellow", 9), "off": ("slate", 3),
    "beam1": ("sand", 5), "beam2": ("sand", 3), "shadow": ("slate", 1),
}


def with_body(colour, extra=None):
    """Palette with the car body on another colour (name from BODY_COLOURS, or an (ramp, offset) pair)."""
    p = dict(PAL)
    p["body"] = BODY_IK[colour] if isinstance(colour, str) else colour
    if extra:
        p.update(extra)
    return p


def adopt_isokit():
    return ik is not None
