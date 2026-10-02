"""
iso_life_models2 - rail, air and water vehicle models (same local frame as iso_life_models).

Rail segments are drawn as separate sprites (one per car) so they bend through curves; the segment origin is
its centre and consecutive segments are spaced by their length + 2 units (coupler).
Aircraft are modelled with the ground at w = 0 for their body; the renderer lifts them (`lift`) and a separate
ground shadow sprite is drawn under them.
"""
import math

from iso_life_render import box, cbox, cyl, wheels, transform
from iso_life_models import lamps


def bogies(L, W):
    hl, hw = L / 2, W / 2
    out = []
    for u in (-hl + 7, hl - 7):
        out.append(box(u - 4.5, u + 4.5, -hw + 1.5, hw - 1.5, 0.6, 3.6, "chassis"))
        out += wheels([u - 2.5, u + 2.5], hw - 2.0, 1.6, 1.4, mat="black")
    return out


def rail_car(L, W, H, col, band=None, cab_front=False, cab_back=False, windows="glass", roof="grey",
             nose=0.0, floor=3.6, win_lo=None, win_hi=None):
    """Passenger rail vehicle: shell, window band with posts, roof, optional raked cab ends."""
    hl, hw = L / 2, W / 2
    win_lo = win_lo or floor + (H - floor) * 0.42
    win_hi = win_hi or H - 3.0
    shell = box(-hl, hl, -hw, hw, floor, H, col)
    if cab_front and nose:
        shell.cut((1, 0, nose), (hl, 0, H))
    if cab_back and nose:
        shell.cut((-1, 0, nose), (-hl, 0, H))
    p = [shell]
    wb = box(-hl + 3, hl - 3, -hw - 0.2, hw + 0.2, win_lo, win_hi, windows)
    p.append(wb)
    p += [box(u - 0.7, u + 0.7, -hw - 0.3, hw + 0.3, win_lo, win_hi, col) for u in range(int(-hl + 8), int(hl - 4), 7)]
    if band:
        p.append(box(-hl - 0.05, hl + 0.05, -hw - 0.15, hw + 0.15, win_lo - 2.2, win_lo - 1.0, band))
    p.append(box(-hl + 1, hl - 1, -hw + 1.5, hw - 1.5, H, H + 1.2, roof))
    for end, on in ((1, cab_front), (-1, cab_back)):
        if on:
            scr = box(-0.6, 0.6, -hw + 1.2, hw - 1.2, win_lo, win_hi + 1.0, windows)
            scr = transform([scr], du=end * (hl - (H - win_hi) * nose * 0.5))[0]
            if nose:
                scr.cut((end, 0, nose), (end * hl + end * 0.4, 0, H))
            p.append(scr)
    p += bogies(L, W)
    return p


def tram_cab():
    p = rail_car(36, 14, 20, "tram", band="white", cab_front=True, nose=0.18, floor=2.4)
    p += lamps(18 - 0.6, -18, 7, 4.0, 5.4, tail=False)
    p.append(box(-3, 3, -0.5, 0.5, 21.2, 25.0, "black"))                    # pantograph
    p.append(box(-4, 4, -4, 4, 25.0, 25.6, "chrome"))
    p.append(box(-19.5, -18, -6, 6, 3, 18.5, "black"))                      # gangway
    return p


def tram_middle():
    p = rail_car(30, 14, 20, "tram", band="white", floor=2.4)
    p.append(box(-16.5, -15, -6, 6, 3, 18.5, "black"))
    return p


def tram_rear():
    return transform(tram_cab(), yaw_deg=180)


def train_loco():
    L, W, H = 46, 15, 23
    hl, hw = L / 2, W / 2
    p = [box(-hl, hl - 9, -hw + 1.5, hw - 1.5, 3.6, H - 2, "loco")]           # hood
    cabb = box(hl - 9, hl, -hw, hw, 3.6, H, "loco")
    cabb.cut((1, 0, 0.3), (hl, 0, H))
    p.append(cabb)
    g = box(hl - 2, hl + 0.2, -hw + 1.2, hw - 1.2, H - 7.5, H - 2.0, "glass")
    g.cut((1, 0, 0.3), (hl + 0.4, 0, H))
    p.append(g)
    p.append(box(hl - 8, hl - 3, -hw - 0.2, hw + 0.2, H - 7.5, H - 2.5, "glass"))
    p.append(box(-hl, hl + 0.1, -hw - 0.1, hw + 0.1, 4.6, 6.0, "hazard"))      # walkway stripe
    p += [box(-hl + 3 + i * 6, -hl + 6 + i * 6, -hw + 2.5, hw - 2.5, H - 2, H - 1.2, "dkgrey") for i in range(5)]
    p.append(cbox(-hl + 8, 0, H - 2, 3, 3, 2, "black"))
    p += bogies(L, W)
    p += lamps(hl, -hl, hw, 7.0, 8.4, tail=False)
    return p


def coach():
    return rail_car(50, 15, 21, "train", band="cream", floor=3.6)


def metro_cab():
    p = rail_car(46, 15, 20, "silver", band="metro", cab_front=True, nose=0.15, floor=3.0)
    return p + lamps(23 - 0.5, -23, 7.5, 4.6, 6.0, tail=False)


def metro_car():
    return rail_car(46, 15, 20, "silver", band="metro", floor=3.0)


def wagon(kind):
    L, W = 42, 15
    hl, hw = L / 2, W / 2
    p = [box(-hl, hl, -hw + 1, hw - 1, 3.6, 5.0, "chassis")]
    if kind == "box":
        p.append(box(-hl + 0.5, hl - 0.5, -hw, hw, 5.0, 20.0, "rust"))
        p.append(box(-4, 4, -hw - 0.2, hw + 0.2, 6.0, 18.5, "wood"))            # sliding door
        p.append(box(-hl + 0.5, hl - 0.5, -hw + 1, hw - 1, 20.0, 21.0, "rust"))
    elif kind == "tank":
        p.append(cyl(0, 0, 5.0, 6.8, L - 3, "black", sides=10, axis="u"))
        p.append(cyl(0, 0, 18.0, 2.2, 1.6, "black", sides=8, axis="w"))
    elif kind == "hopper":
        hop = box(-hl + 1, hl - 1, -hw, hw, 5.0, 19.0, "grey")
        hop.cut((1, 0, -0.6), (hl - 1, 0, 19.0))
        hop.cut((-1, 0, -0.6), (-hl + 1, 0, 19.0))
        p.append(hop)
        p.append(box(-hl + 3, hl - 3, -hw + 1.2, hw - 1.2, 18.0, 19.6, "ore"))
    elif kind == "container":
        p.append(box(-hl, hl, -hw, hw, 5.0, 6.0, "dkgrey"))
        p.append(box(-hl + 1, -1, -hw + 0.4, hw - 0.4, 6.0, 19.0, "container1"))
        p.append(box(1, hl - 1, -hw + 0.4, hw - 0.4, 6.0, 19.0, "container2"))
    elif kind == "logs":
        p.append(box(-hl, hl, -hw, hw, 5.0, 6.0, "wood"))
        for dv, dw in [(-4, 6), (0, 6), (4, 6), (-2, 10), (2, 10), (0, 14)]:
            p.append(cyl(0, dv, dw, 2.1, L - 4, "log", sides=6, axis="u"))
        p += [box(u - 0.6, u + 0.6, s * (hw - 0.5) - 0.6, s * (hw - 0.5) + 0.6, 6, 16, "black")
              for u in (-hl + 3, 0, hl - 3) for s in (-1, 1)]
    p += bogies(L, W)
    return p


def heli(col, trim, rotor_yaw=0.0, bucket=False):
    p = []
    body = cyl(0, 0, 4.0, 6.0, 20, col, sides=8, axis="u")
    body.cut((1, 0, 1.0), (11, 0, 10))                                     # nose slope
    p.append(body)
    g = cyl(4.5, 0, 4.5, 5.6, 8, "glass", sides=8, axis="u")
    g.cut((1, 0, 1.0), (11.3, 0, 10.2))
    g.cut((-1, 0, 0), (2.0, 0, 0))
    p.append(g)
    p.append(box(-26, -8, -1.2, 1.2, 9.0, 12.0, col))                       # tail boom
    p.append(box(-27, -23, -0.6, 0.6, 9.0, 18.0, trim))                     # fin
    p.append(box(-25, -22, -4, 4, 10.0, 11.0, trim))                        # stabiliser
    p.append(box(-6, 6, -6.2, 6.2, 9.5, 11.0, trim))                        # belly stripe
    p += [box(-8, 9, s * 6 - 0.6, s * 6 + 0.6, 0.0, 1.0, "dkgrey") for s in (-1, 1)]      # skids
    p += [box(u - 0.5, u + 0.5, s * 5.5 - 0.5, s * 5.5 + 0.5, 1.0, 4.0, "dkgrey") for u in (-4, 5) for s in (-1, 1)]
    p.append(cbox(0, 0, 16, 2.0, 2.0, 2.0, "dkgrey"))                       # mast
    for k in range(4):
        a = math.radians(rotor_yaw + 90 * k)
        blade = box(0, 20, -1.0, 1.0, 18.0, 18.6, "rotor")
        p += transform([blade], yaw_deg=math.degrees(a))
    if bucket:
        p.append(box(-0.7, 0.7, -0.7, 0.7, -12, 4.0, "black"))
        p.append(cyl(0, 0, -18, 3.2, 6, "orange", sides=8, axis="w"))
    return p


def cargo_plane(prop_yaw=0.0):
    p = []
    fus = cyl(0, 0, 4.0, 9.5, 104, "white", sides=10, axis="u")
    fus.cut((1, 0, 0.8), (52, 0, 16))
    fus.cut((-1, 0, -0.5), (-52, 0, 8))
    p.append(fus)
    p.append(box(40, 48, -6, 6, 14, 18.5, "glass"))
    p.append(box(-48, 48, -9.7, 9.7, 12.5, 15.5, "blue"))                    # cheat line
    p.append(box(-46, 46, -9.9, 9.9, 4.0, 6.0, "grey"))
    fin = box(-58, -40, -1.0, 1.0, 20, 48, "blue")
    fin.cut((1, 0, 0.6), (-40, 0, 20))
    p.append(fin)
    p.append(box(-58, -48, -26, 26, 38, 40, "white"))
    wing = box(-4, 12, -64, 64, 22, 24.5, "white")
    p.append(wing)
    for v in (-44, -22, 22, 44):
        p.append(cyl(8, v, 16.5, 3.4, 18, "grey", sides=8, axis="u"))
        for k in range(3):
            a = math.radians(prop_yaw + 120 * k)
            blade = box(-0.4, 0.4, -0.8, 0.8, 0, 7.0, "rotor")
            # blade in the v-w plane around the hub
            c, s = math.cos(a), math.sin(a)
            blade.planes[:, :3] = [[1, 0, 0], [-1, 0, 0], [0, c, s], [0, -c, -s], [0, -s, c], [0, s, -c]]
            hub_v, hub_w = v, 19.9
            blade.planes[:, 3] = [17.6, -16.8, 0.8 + c * hub_v + s * hub_w, 0.8 - c * hub_v - s * hub_w,
                                  7.0 - s * hub_v + c * hub_w, s * hub_v - c * hub_w]
            p.append(blade)
    p += [cyl(u, s, 0, 2.6, 3.0, "tyre", sides=8, axis="v") for u in (-6, 2) for s in (-8, 8)]
    p.append(cyl(36, 0, 0, 2.0, 2.0, "tyre", sides=8, axis="v"))
    return p


def cargo_ship():
    L, W = 256, 44
    hl, hw = L / 2, W / 2
    hull = box(-hl, hl, -hw, hw, -4, 16, "hull")
    hull.cut((1, 0.45, 0), (hl, 0, 0)).cut((1, -0.45, 0), (hl, 0, 0))     # bow
    hull.cut((-1, 0.2, 0), (-hl, 0, 0)).cut((-1, -0.2, 0), (-hl, 0, 0))
    p = [hull]
    boot = box(-hl, hl, -hw - 0.2, hw + 0.2, -4, 2, "hullred")
    boot.cut((1, 0.45, 0), (hl + 0.2, 0, 0)).cut((1, -0.45, 0), (hl + 0.2, 0, 0))
    boot.cut((-1, 0.2, 0), (-hl - 0.2, 0, 0)).cut((-1, -0.2, 0), (-hl - 0.2, 0, 0))
    p.append(boot)
    p.append(box(-hl + 4, hl - 30, -hw + 2, hw - 2, 15, 16.4, "deck"))
    sup = box(-hl + 8, -hl + 34, -hw + 4, hw - 4, 16, 50, "white")
    p.append(sup)
    p += [box(-hl + 8.2 - 0.4, -hl + 34.4, -hw + 3.6, hw - 3.6, z, z + 2.4, "glass") for z in (26, 34, 42)]
    p.append(box(-hl + 10, -hl + 20, -4, 4, 50, 60, "hullred"))              # funnel
    cols = ["container1", "container2", "container3", "container4", "white"]
    k = 0
    for bay in range(8):
        u0 = -hl + 40 + bay * 22
        for row in range(4):
            v0 = -hw + 4 + row * 9
            tiers = 2 + ((bay * 3 + row) % 3)
            for t in range(tiers):
                k += 1
                p.append(box(u0, u0 + 20, v0, v0 + 8.6, 16.4 + t * 8, 16.4 + t * 8 + 7.6, cols[(bay * 7 + row * 3 + t) % 5]))
    return p


def ferry():
    L, W = 80, 24
    hl, hw = L / 2, W / 2
    hull = box(-hl, hl, -hw, hw, -2, 9, "white")
    hull.cut((1, 0.6, 0), (hl, 0, 0)).cut((1, -0.6, 0), (hl, 0, 0))
    p = [hull, box(-hl, hl, -hw - 0.2, hw + 0.2, -2, 2, "blue")]
    p[-1].cut((1, 0.6, 0), (hl + 0.2, 0, 0)).cut((1, -0.6, 0), (hl + 0.2, 0, 0))
    p.append(box(-hl + 6, hl - 18, -hw + 2, hw - 2, 9, 18, "white"))
    p.append(box(-hl + 5.8, hl - 17.8, -hw + 1.8, hw - 1.8, 12, 15.5, "glass"))
    p.append(box(-hl + 10, hl - 26, -hw + 4, hw - 4, 18, 24, "white"))
    p.append(box(-hl + 9.8, hl - 25.8, -hw + 3.8, hw - 3.8, 20, 22.5, "glass"))
    p.append(box(-hl + 14, -hl + 20, -2.5, 2.5, 24, 30, "red"))
    return p


def motorboat():
    hull = box(-11, 11, -4.5, 4.5, -1, 4, "white")
    hull.cut((1, 0.8, 0), (11, 0, 0)).cut((1, -0.8, 0), (11, 0, 0))
    ws = box(0, 3, -3.5, 3.5, 4, 7, "glass")
    ws.cut((1, 0, 0.8), (3, 0, 4))
    return [hull, box(-11, 11, -4.7, 4.7, 1.5, 2.5, "red"), ws, box(-9, -2, -3.5, 3.5, 4, 4.6, "deck")]


MODELS = {
    "tram-cab": tram_cab, "tram-middle": tram_middle, "tram-rear": tram_rear,
    "train-loco": train_loco, "train-coach": coach,
    "wagon-box": lambda: wagon("box"), "wagon-tank": lambda: wagon("tank"), "wagon-hopper": lambda: wagon("hopper"),
    "wagon-container": lambda: wagon("container"), "wagon-logs": lambda: wagon("logs"),
    "metro-cab": metro_cab, "metro-car": metro_car,
    "heli-medevac": lambda: heli("white", "red"), "heli-fire": lambda: heli("fire", "hazard", bucket=True),
    "cargo-plane": cargo_plane, "cargo-ship": cargo_ship, "ferry": ferry, "motorboat": motorboat,
}
