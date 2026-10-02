"""
iso_net_props.py - NET street props as isokit models: lamps, traffic signals, signs, stops, metro entrance,
street furniture. Every model is built for one side/orientation and rotated with isokit facings.

Placement convention: models are built for the EAST sidewalk of a N-S street (x ~ 0.9) or the SE corner of a
junction; render(facing=k) rotates them to the other sides. Kerb tops are at z = 1 px.
"""
import isokit as ik
from isokit.trees import add_tree
from iso_net_kit import (CONCRETE, GALV, GLASS, POLE, SIGN_BLUE, SIGN_GREEN, SIGN_RED, SIGN_WHITE, SIGN_YELLOW,
                         STEEL, STEEL_DARK, dark_lens, flat, glow)

KERB_Z = 1.0
LAMP_HEAD = glow("yellow", day=8.0, night=11.0)
LAMP_HEAD_OFF = glow("yellow", day=8.0, lit=False)
RED_ON = glow("red", day=8.0, night=10.0)
AMBER_ON = glow("terra", day=9.0, night=10.0)
GREEN_ON = glow("teal", day=9.0, night=10.0)
LENS_OFF = dark_lens("slate", 1.5)
BENCH_WOOD = flat("wood", 1.0)
BIN = flat("leaf", -1.0)


def street_lamp(double=False, x=0.91, y=0.5):
    """Modern street light: slim pole, arm reaching over the road (toward -x), flat head with a lit lens."""
    s = ik.Scene(footprint=(1, 1), seed=11)
    s.cylinder(x, y, KERB_Z, 0.022, 27, POLE, segs=8)
    s.cylinder(x, y, KERB_Z, 0.035, 2, POLE, segs=8)
    arms = [(-1, 0.20)] + ([(1, 0.20)] if double else [])
    for sgn, L in arms:
        x0 = x if sgn > 0 else x - L
        s.box(x0, y - 0.012, 26.5, L, 0.024, 1.2, POLE)
        hx = x - L if sgn < 0 else x + L - 0.09
        s.box(hx, y - 0.035, 25.0, 0.09, 0.07, 2.0, POLE)
        s.box(hx + 0.008, y - 0.03, 23.0, 0.075, 0.06, 2.0, LAMP_HEAD)
    return s


def heritage_lamp(x=0.9, y=0.5):
    """Old-town lantern for alleys/plazas: dark post with a glass lantern."""
    s = ik.Scene(footprint=(1, 1), seed=12)
    s.cylinder(x, y, KERB_Z, 0.03, 3, STEEL_DARK, segs=8)
    s.cylinder(x, y, KERB_Z, 0.018, 17, STEEL_DARK, segs=8)
    s.box(x - 0.035, y - 0.035, 18, 0.07, 0.07, 4, LAMP_HEAD)
    s.pyramid(x - 0.045, y - 0.045, 22, 0.09, 0.09, 3, STEEL_DARK)
    return s


def signal_head(s, x, y, z, state, face="y"):
    """Three-lamp head; lenses on the +Y face (face='y') or +X face (face='x')."""
    s.box(x - 0.025, y - 0.025, z, 0.05, 0.05, 10, STEEL_DARK)
    for i, (k, on) in enumerate((("red", RED_ON), ("amber", AMBER_ON), ("green", GREEN_ON))):
        mat = on if state == k else LENS_OFF
        lz = z + 7 - i * 3
        if face == "y":
            s.box(x - 0.016, y + 0.025, lz, 0.032, 0.012, 2, mat)
        else:
            s.box(x + 0.025, y - 0.016, lz, 0.012, 0.032, 2, mat)


def traffic_signal(state="red", mast=True):
    """Signal at the SE corner of a junction for traffic arriving from the S arm (facing +Y toward the viewer).
    Mast arm reaches west over the inbound lane; a second head sits on the pole for pedestrians/near view."""
    s = ik.Scene(footprint=(1, 1), seed=13)
    x, y = 0.86, 0.86
    s.cylinder(x, y, KERB_Z, 0.03, 30, POLE, segs=8)
    s.cylinder(x, y, KERB_Z, 0.045, 2, POLE, segs=8)
    if mast:
        s.box(x - 0.36, y - 0.012, 29, 0.36, 0.024, 1.6, POLE)
        signal_head(s, x - 0.27, y, 19, state, "y")
    signal_head(s, x, y + 0.04, 13, state, "y")
    s.box(x - 0.02, y - 0.04, 9, 0.04, 0.03, 4, SIGN_YELLOW)  # push-button box
    return s


def stop_sign(kind="stop"):
    """Sign at the SE corner facing traffic from the S arm. kind: stop | yield | oneway | noentry."""
    s = ik.Scene(footprint=(1, 1), seed=14)
    x, y = 0.88, 0.88
    s.cylinder(x, y, KERB_Z, 0.014, 15, GALV, segs=6)
    z = 13
    if kind == "stop":
        w = 0.09
        s.box(x - w / 2, y + 0.014, z, w, 0.008, 8, SIGN_RED)
        s.box(x - w / 2 + 0.015, y + 0.022, z + 3.5, w - 0.03, 0.004, 1, SIGN_WHITE)
    elif kind == "yield":
        top = z + 8
        a, b, c = (x - 0.055, y + 0.02, top), (x + 0.055, y + 0.02, top), (x, y + 0.02, top - 9)
        s.tri(a, b, c, SIGN_RED, outward=(0, 1, 0))
        a2, b2, c2 = (x - 0.025, y + 0.024, top - 1.5), (x + 0.025, y + 0.024, top - 1.5), (x, y + 0.024, top - 6)
        s.tri(a2, b2, c2, SIGN_WHITE, outward=(0, 1, 0))
        s.tri((a[0], y + 0.012, a[2]), (b[0], y + 0.012, b[2]), (c[0], y + 0.012, c[2]), GALV, outward=(0, -1, 0))
    elif kind == "noentry":
        s.cylinder(x, y + 0.02, z + 3.5, 0.04, 0.008, SIGN_RED, axis="y", segs=12)
        s.box(x - 0.025, y + 0.03, z + 3, 0.05, 0.004, 1.2, SIGN_WHITE)
    else:  # one-way: blue plate with a white arrow
        s.box(x - 0.06, y + 0.014, z + 2, 0.12, 0.008, 4, SIGN_BLUE)
        s.box(x - 0.045, y + 0.022, z + 3.5, 0.08, 0.004, 1, SIGN_WHITE)
    return s


def bus_shelter(kind="bus"):
    """Stop on the E sidewalk of a N-S road: glass shelter, bench, flag pole with the mode's sign.
    kind: bus (blue), tram (with a platform edge), taxi (yellow sign, no shelter)."""
    s = ik.Scene(footprint=(1, 1), seed=15)
    x0, y0, L, D = 0.86, 0.3, 0.36, 0.12
    if kind == "tram":
        s.box(0.70, 0.12, 0, 0.12, 0.76, 3, CONCRETE)  # raised platform at the road edge
        s.box(0.70, 0.12, 3, 0.012, 0.76, 0.01, SIGN_YELLOW)
        z0 = 3
        x0 = 0.80
    else:
        z0 = KERB_Z
    if kind != "taxi":
        s.box(x0 + D - 0.015, y0, z0, 0.015, L, 11, GLASS)        # back glass
        s.box(x0, y0, z0, D, 0.012, 11, GLASS)                     # side glass (far)
        s.box(x0, y0 + L - 0.012, z0, D, 0.012, 11, GLASS)         # side glass (near)
        s.box(x0 - 0.02, y0 - 0.02, z0 + 11, D + 0.04, L + 0.04, 1.5, STEEL)  # roof
        s.box(x0 + 0.05, y0 + 0.06, z0, 0.05, L - 0.12, 3, BENCH_WOOD)
        s.box(x0 + D - 0.02, y0 + L * 0.55, z0 + 3, 0.006, L * 0.35, 6, glow("glass", day=8, night=10))  # ad panel
    pole_mat = {"bus": SIGN_BLUE, "tram": SIGN_GREEN, "taxi": SIGN_YELLOW}[kind]
    px, py = (0.84, 0.78) if kind != "taxi" else (0.88, 0.5)
    s.cylinder(px, py, z0, 0.014, 18, GALV, segs=6)
    s.box(px - 0.008, py - 0.05, z0 + 13, 0.016, 0.10, 5, pole_mat)
    if kind == "taxi":
        s.box(px - 0.06, py - 0.05, z0, 0.04, 0.04, 2, BIN)
        s.box(0.80, 0.15, 0.0, 0.015, 0.7, 1.2, SIGN_YELLOW)       # yellow kerb paint of the rank
        s.box(0.70, 0.18, 0.0, 0.1, 0.012, 0.3, SIGN_WHITE)
        s.box(0.70, 0.82, 0.0, 0.1, 0.012, 0.3, SIGN_WHITE)
    return s


def metro_entrance():
    """Stair well down to the metro on a 1x1 sidewalk/plaza cell: glass canopy, rails and a blue 'M' totem."""
    s = ik.Scene(footprint=(1, 1), seed=16)
    s.box(0.3, 0.2, 0, 0.4, 0.55, 1, CONCRETE)
    s.box(0.34, 0.24, 0.0, 0.32, 0.46, 0.5, flat("slate", -3.0))      # dark stair opening
    for i in range(5):
        s.box(0.35, 0.26 + i * 0.08, 0.0, 0.30, 0.03, 0.6, flat("grey", 0.0))
    s.box(0.30, 0.2, 1, 0.02, 0.55, 5, GLASS)
    s.box(0.68, 0.2, 1, 0.02, 0.55, 5, GLASS)
    s.box(0.30, 0.2, 1, 0.4, 0.02, 5, GLASS)
    s.box(0.28, 0.16, 10, 0.44, 0.48, 1.5, STEEL)
    s.cylinder(0.3, 0.84, 0, 0.02, 20, GALV, segs=6)
    s.box(0.26, 0.82, 16, 0.08, 0.04, 7, glow("water", day=7, night=10))
    s.box(0.285, 0.862, 18, 0.03, 0.002, 3, SIGN_WHITE)
    return s


def furniture(kind, season="summer"):
    s = ik.Scene(footprint=(1, 1), seed=17)
    x, y = 0.9, 0.5
    if kind == "bench":
        s.box(x - 0.03, y - 0.1, KERB_Z + 2, 0.06, 0.2, 1, BENCH_WOOD)
        s.box(x + 0.02, y - 0.1, KERB_Z + 3, 0.012, 0.2, 3, BENCH_WOOD)
        s.box(x - 0.02, y - 0.09, KERB_Z, 0.04, 0.012, 2, STEEL_DARK)
        s.box(x - 0.02, y + 0.078, KERB_Z, 0.04, 0.012, 2, STEEL_DARK)
    elif kind == "bin":
        s.cylinder(x, y, KERB_Z, 0.03, 5, BIN, segs=10)
    elif kind == "hydrant":
        s.cylinder(x, y, KERB_Z, 0.022, 5, SIGN_RED, segs=8)
        s.dome(x, y, KERB_Z + 5, 0.022, 2, SIGN_RED)
        s.cylinder(x, y, KERB_Z + 3, 0.012, 0.07, SIGN_RED, axis="x", segs=6)
    elif kind == "mailbox":
        s.box(x - 0.03, y - 0.03, KERB_Z + 3, 0.06, 0.06, 5, SIGN_YELLOW)
        s.cylinder(x, y, KERB_Z, 0.01, 3, STEEL_DARK, segs=6)
    elif kind == "bollards":
        for i in range(4):
            s.cylinder(0.83, 0.2 + i * 0.2, KERB_Z, 0.012, 4, STEEL_DARK, segs=6)
    elif kind == "tree":
        s.box(x - 0.05, y - 0.05, KERB_Z, 0.1, 0.1, 0.4, flat("wood", -1.0))  # tree grate
        add_tree(s, x, y, "linden", season, stage=1, seed=3, z=KERB_Z)        # KIT street tree, young stage
    elif kind == "planter":
        s.box(x - 0.06, y - 0.12, KERB_Z, 0.12, 0.24, 3, CONCRETE)
        s.blob([(x, y - 0.06, 5, 0.06, 3), (x, y + 0.06, 5, 0.06, 3)], ik.mat("foliage_light"), rough=0.4)
    return s
