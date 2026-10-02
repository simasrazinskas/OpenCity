"""
iso_life_models - road vehicle models for iso_life_render (local units: 1 = 1/64 cell, u forward, v left, w up).

Scale: a sedan is 24 units long (0.375 cell, ~18 px wide on screen in diagonal views), 11.5 wide, 10.5 high.
Lane: right-hand traffic, lane centre 12 units (190/1024 cell) right of the street centre line; vehicles are at most
14 units wide so they stay inside a 16-unit lane of a 2-lane street.
Lamps are thin prims with materials head/tail; light bars use siren_a/siren_b; beacons use beacon.
Every model function returns a list of prims; MODELS maps name -> (function, info dict).
"""
from iso_life_render import box, cbox, cyl, wheels, Prim


def lamps(front_u, back_u, half_w, w0, w1, lamp_w=2.0, inset=1.0, tail=True):
    out = []
    for side in (-1, 1):
        v = side * (half_w - inset - lamp_w / 2)
        out.append(cbox(front_u + 0.3, v, w0, 0.7, lamp_w, w1 - w0, "head"))
        if tail:
            out.append(cbox(back_u - 0.3, v, w0, 0.7, lamp_w, w1 - w0, "tail"))
    return out


def lightbar(u, w, half_w=3.6, length=2.2):
    return [cbox(u, half_w / 2, w, length, half_w, 1.4, "siren_a"),
            cbox(u, -half_w / 2, w, length, half_w, 1.4, "siren_b")]


def car(L, W, sill, belt, roof, cab_u0, cab_u1, ws=0.9, rw=0.9, nose=1.0, tail=0.6, wr=2.4, wb=None,
        body="body", glass="glass"):
    """Generic car: lower body with sloped bonnet/boot, glass house with raked screens, roof plate, pillar."""
    hl, hw = L / 2, W / 2
    lower = box(-hl, hl, -hw, hw, sill, belt, body)
    lower.cut((0.35, 0, 1), (hl, 0, belt - nose))
    lower.cut((-0.35, 0, 1), (-hl, 0, belt - tail))
    gw = hw - 0.8
    house = box(cab_u0, cab_u1, -gw, gw, belt - 0.2, roof - 0.4, glass)
    house.cut((1, 0, ws), (cab_u1, 0, belt))
    house.cut((-1, 0, rw), (cab_u0, 0, belt))
    top = box(cab_u0, cab_u1, -gw, gw, roof - 1.2, roof, body)
    top.cut((1, 0, ws), (cab_u1 - 0.3, 0, belt))
    top.cut((-1, 0, rw), (cab_u0 + 0.3, 0, belt))
    mid = (cab_u0 + cab_u1) / 2
    p = [lower, house, top, cbox(mid, 0, belt - 0.2, 1.3, 2 * gw + 0.3, roof - belt - 0.5, body),
         box(-hl + 1, hl - 1, -hw + 0.6, hw - 0.6, 0.8, sill + 0.1, "chassis")]
    wb = wb if wb is not None else hl - wr - 1.2
    p += wheels([-wb, wb], hw - 1.0, wr, 1.8)
    p += lamps(hl - nose * 0.3, -hl, hw, belt - 2.4 - nose * 0.5, belt - 1.0 - nose * 0.5)
    return p


def scaled(prims, k=1.08, kw=1.0):
    """Scale models in the ground plane (u, v) by k and height by kw (plane normals scale inversely)."""
    out = []
    for p in prims:
        q = Prim(p.planes.copy(), p.mat, dict(p.facemats))
        q.planes[:, 0] /= k
        q.planes[:, 1] /= k
        q.planes[:, 2] /= kw
        out.append(q)
    return out


def hatchback():
    return car(21, 11, 2.0, 6.8, 11.0, -9.5, 4.0, ws=0.55, rw=0.2, tail=0.2)


def sedan():
    return car(24, 11.5, 2.0, 6.8, 11.0, -7.5, 5.0, ws=0.6, rw=0.5)


def estate():
    return car(25, 11.5, 2.0, 6.8, 11.0, -11.5, 5.0, ws=0.6, rw=0.15, tail=0.2)


def suv():
    return car(25, 12.5, 3.0, 8.6, 13.0, -10.5, 4.5, ws=0.7, rw=0.15, nose=0.6, tail=0.1, wr=3.0)


def mpv():
    return car(24, 12, 2.4, 7.4, 13.0, -10.5, 6.5, ws=0.45, rw=0.1, nose=0.8, tail=0.1)


def sports():
    return car(23, 12, 1.4, 5.0, 8.8, -6.5, 3.5, ws=1.0, rw=0.9, nose=1.0, tail=0.4, wr=2.2)


def pickup(body="body"):
    L, W = 26, 12
    hl, hw = L / 2, W / 2
    p = car(L, W, 2.6, 7.6, 12.5, -3.5, 6.5, ws=0.6, rw=0.05, nose=0.8, tail=0.0, wr=2.8, body=body)
    p[0].cut((-1, 0, 0), (-3.6, 0, 0))                                       # lower body only under the cab
    # open bed: floor + three walls (replaces nothing; bed sits behind the cab)
    p.append(box(-hl, -3.6, -hw, hw, 2.6, 4.0, body))
    p.append(box(-hl, -3.6, hw - 1.6, hw, 4.0, 7.4, body))
    p.append(box(-hl, -3.6, -hw, -hw + 1.6, 4.0, 7.4, body))
    p.append(box(-hl, -hl + 1.6, -hw, hw, 4.0, 7.4, body))
    p.append(box(-hl + 1.6, -3.6, -hw + 1.6, hw - 1.6, 4.0, 4.4, "dkgrey"))
    return p


def taxi():
    p = car(24, 11.5, 2.0, 6.8, 11.0, -7.5, 5.0, ws=0.6, rw=0.5, body="taxi")
    p.append(cbox(-1.5, 0, 10.4, 3.0, 5.0, 1.6, "taxi_sign"))
    return p


def police():
    p = car(24, 11.5, 2.0, 6.8, 11.0, -7.5, 5.0, ws=0.6, rw=0.5, body="white")
    p.append(box(-12.05, 12.05, -5.8, 5.8, 3.6, 5.6, "police"))             # door band
    return p + lightbar(-1.0, 10.8)


def hearse():
    return car(30, 11.5, 2.0, 6.8, 11.5, -14.0, 5.0, ws=0.6, rw=0.1, tail=0.1, body="black")


def van_body(L, W, H, sill=2.4, cab_len=7.0, ws=0.55, nose_h=None, body="body", wr=2.8):
    """One-box van: tall body, sloped screen, short bonnet."""
    hl, hw = L / 2, W / 2
    nose_h = nose_h or H * 0.55
    shell = box(-hl, hl, -hw, hw, sill, H, body)
    shell.cut((ws, 0, 1), (hl - cab_len * 0.35, 0, H))           # screen slope down to the bonnet
    shell.cut((1, 0, 0.0), (hl, 0, 0))
    shell.cut((0.6, 0, 1), (hl, 0, nose_h))                         # bonnet
    gw = hw - 0.5
    glass = box(hl - cab_len, hl, -hw - 0.2, hw + 0.2, nose_h + 0.3, H - 0.9, "glass")
    glass.cut((ws, 0, 1), (hl - cab_len * 0.35 + 0.25, 0, H))
    glass.cut((0.6, 0, 1), (hl + 0.3, 0, nose_h + 0.5))
    p = [shell, glass, box(-hl + 1, hl - 1, -hw + 0.6, hw - 0.6, 0.8, sill + 0.1, "chassis")]
    p += wheels([-hl + wr + 2.5, hl - wr - 2.5], hw - 1.0, wr, 1.8)
    p += lamps(hl, -hl, hw, nose_h - 2.2, nose_h - 0.6)
    return p


def delivery_van(body="body"):
    return van_body(27, 12, 15.0, body=body)


def ambulance():
    p = van_body(30, 13, 17.0, body="white")
    p.append(box(-15.05, 15.05, -6.55, 6.55, 8.0, 9.6, "red"))      # red belt stripe
    return p + lightbar(7.5, 17.0, half_w=4.2)


def postal_van():
    return van_body(27, 12, 15.0, body="post")


def cab(u_back, u_front, W, H, body, sill=3.0, ws=0.25):
    """Truck cab (cab-over): a tall box with a slightly raked screen and a black grille."""
    hw = W / 2
    c = box(u_back, u_front, -hw, hw, sill, H, body)
    c.cut((1, 0, ws), (u_front, 0, H - 0.5))
    g = box(u_front - 2.0, u_front + 0.05, -hw + 0.5, hw - 0.5, H - 6.5, H - 1.5, "glass")
    g.cut((1, 0, ws), (u_front + 0.3, 0, H - 0.5))
    gr = box(u_front - 0.4, u_front + 0.3, -hw + 2.0, hw - 2.0, sill + 1.0, sill + 4.0, "black")
    side = box(u_front - 5.0, u_front - 2.5, -hw - 0.2, hw + 0.2, H - 6.0, H - 1.8, "glass")
    return [c, g, gr, side]


def rigid_truck(L, W, cab_len, H_cab, body_prims, cab_col="body", axles=None, wr=3.2):
    hl, hw = L / 2, W / 2
    p = cab(hl - cab_len, hl, W, H_cab, cab_col)
    p.append(box(-hl, hl - 1, -hw + 1, hw - 1, 1.2, 3.2, "chassis"))
    p += body_prims
    axles = axles or [-hl + wr + 3, hl - wr - 2]
    p += wheels(axles, hw - 1.2, wr, 2.2)
    p += lamps(hl, -hl, hw, 4.0, 5.6, tail=False)
    p += [cbox(-hl - 0.3, s * (hw - 2), 3.2, 0.7, 2.0, 1.4, "tail") for s in (-1, 1)]
    return p


def box_truck(col="white", cab_col="body"):
    L, W = 36, 14
    hl, hw = L / 2, W / 2
    return rigid_truck(L, W, 9, 15, [box(-hl, hl - 9.8, -hw, hw, 3.2, 20.0, col)], cab_col)


def garbage_truck():
    L, W = 36, 14
    hl, hw = L / 2, W / 2
    b = box(-hl, hl - 9.8, -hw, hw, 3.2, 18.5, "green")
    b.cut((-1, 0, 1.0), (-hl, 0, 13.5))
    hopper = box(-hl - 1.5, -hl + 4, -hw + 0.5, hw - 0.5, 4.0, 13.0, "dkgrey")
    stripe = box(-hl - 1.55, -hl + 4.05, -hw + 0.45, hw - 0.45, 4.0, 5.2, "hazard")
    return rigid_truck(L, W, 9, 15, [b, hopper, stripe], "white") + [cbox(hl - 4.5, 0, 15.0, 2.0, 2.0, 1.3, "beacon")]


def fire_engine():
    L, W = 42, 14
    hl, hw = L / 2, W / 2
    body = box(-hl, hl - 11, -hw, hw, 3.2, 14.0, "fire")
    lockers = [box(-hl + 2 + i * 7, -hl + 8 + i * 7, -hw - 0.2, hw + 0.2, 5.0, 12.5, "chrome") for i in range(3)]
    ladder = [box(-hl - 3, hl - 6, s * 2.5 - 0.5, s * 2.5 + 0.5, 15.0, 16.2, "chrome") for s in (-1, 1)]
    rungs = [box(-hl - 3 + i * 4, -hl - 2 + i * 4, -2.5, 2.5, 15.0, 15.6, "chrome") for i in range(9)]
    base = box(-6, 0, -3.5, 3.5, 14.0, 15.0, "dkgrey")
    stripe = box(-hl - 0.05, hl - 10.95, -hw - 0.05, hw + 0.05, 4.0, 5.0, "white")
    return rigid_truck(L, W, 11, 16, [body, stripe] + lockers + [base] + ladder + rungs, "fire",
                       axles=[-hl + 6, -hl + 13, hl - 6]) + lightbar(hl - 5, 16.0, half_w=4.6)


def tanker_truck(col="silver"):
    L, W = 38, 14
    hl, hw = L / 2, W / 2
    tank = cyl(-3, 0, 3.6, 6.4, 26, col, sides=10, axis="u")
    return rigid_truck(L, W, 9, 15, [tank], "body")


def tipper_truck():
    """Ore / gravel tipper with a heap of ore."""
    L, W = 36, 14
    hl, hw = L / 2, W / 2
    tub = [box(-hl, hl - 10, -hw, hw, 3.2, 5.0, "rust"), box(-hl, hl - 10, hw - 1.2, hw, 5.0, 12.0, "rust"),
           box(-hl, hl - 10, -hw, -hw + 1.2, 5.0, 12.0, "rust"), box(-hl, -hl + 1.2, -hw, hw, 5.0, 12.0, "rust"),
           box(hl - 11.2, hl - 10, -hw, hw, 5.0, 14.0, "rust")]
    heap = box(-hl + 1.2, hl - 11.2, -hw + 1.2, hw - 1.2, 5.0, 14.5, "ore")
    heap.cut((0, 1, 1.1), (0, 0, 14.5)).cut((0, -1, 1.1), (0, 0, 14.5))
    heap.cut((1, 0, 1.6), (hl - 13, 0, 14.5)).cut((-1, 0, 1.6), (-hl + 3, 0, 14.5))
    return rigid_truck(L, W, 9, 15, tub + [heap], "orange")


def timber_truck():
    L, W = 40, 14
    hl, hw = L / 2, W / 2
    logs = []
    for row, (dv, dw) in enumerate([(-3.4, 3.4), (0, 3.4), (3.4, 3.4), (-1.7, 6.6), (1.7, 6.6), (0, 9.8)]):
        logs.append(cyl(-4, dv, dw, 1.8, 28, "log", sides=6, axis="u", ))
    posts = [box(u - 0.6, u + 0.6, s * (hw - 0.6) - 0.6, s * (hw - 0.6) + 0.6, 3.2, 14.0, "black")
             for u in (-hl + 2, -4, hl - 12) for s in (-1, 1)]
    ends = [cbox(-hl + 0.6, dv, dw, 1.0, 3.0, 3.2, "wood") for dv, dw in [(-3.4, 3.6), (0, 3.6), (3.4, 3.6), (0, 10.0)]]
    return rigid_truck(L, W, 9, 15, logs + posts + ends, "green")


def tractor_unit(col="body"):
    """Semi tractor (front segment of the articulated lorry). Origin = segment centre."""
    L, W = 20, 14
    hl, hw = L / 2, W / 2
    p = cab(hl - 10, hl, W, 17, col)
    p.append(box(-hl, hl - 1, -hw + 1, hw - 1, 1.2, 3.6, "chassis"))
    p.append(box(-hl + 1, hl - 11, -4.5, 4.5, 3.6, 4.4, "dkgrey"))          # fifth wheel
    p += wheels([-hl + 4, hl - 5], hw - 1.2, 3.2, 2.2)
    p += lamps(hl, -hl, hw, 4.2, 5.8, tail=False)
    return p


def trailer(kind="box", col="white"):
    """Semi trailer (rear segment). Origin = segment centre; kingpin near the front."""
    L, W = 44, 14
    hl, hw = L / 2, W / 2
    p = [box(-hl, hl, -hw + 1, hw - 1, 3.0, 4.6, "chassis")]
    if kind == "box":
        p.append(box(-hl, hl, -hw, hw, 4.6, 21.0, col))
    elif kind == "tank":
        p.append(cyl(0, 0, 4.6, 6.6, L - 2, col, sides=10, axis="u"))
    elif kind == "container":
        p.append(box(-hl + 1, hl - 1, -hw + 0.3, hw - 0.3, 4.6, 19.5, col))
        p += [box(-hl + 1 + i * 6, -hl + 1.6 + i * 6, -hw + 0.1, hw - 0.1, 4.8, 19.3, col + "_rib") for i in range(8)]
    elif kind == "flat":
        p.append(box(-hl, hl, -hw, hw, 4.6, 5.6, "wood"))
    p += wheels([-hl + 4, -hl + 10], hw - 1.2, 3.2, 2.2)
    p += [cbox(-hl - 0.3, s * (hw - 2), 3.4, 0.7, 2.0, 1.4, "tail") for s in (-1, 1)]
    return p


def farm_tractor():
    L, W = 20, 13
    hl, hw = L / 2, W / 2
    p = [box(-2, hl, -3.5, 3.5, 4.0, 10.0, "green")]                         # bonnet
    p[0].cut((1, 0, 1.2), (hl, 0, 9.0))
    p.append(box(-hl + 1, -1, -4.5, 4.5, 4.0, 17.0, "glass"))                # cab glass
    p.append(box(-hl + 0.5, -0.5, -5.0, 5.0, 16.0, 17.5, "green"))           # roof
    p.append(box(-hl + 1, -1, -4.5, 4.5, 4.0, 8.5, "green"))
    p += [cyl(-hl + 4, s * (hw - 1.5), 0, 4.8, 3.0, "tyre", sides=10, axis="v") for s in (-1, 1)]
    p += [cyl(hl - 3.5, s * (hw - 2.5), 0, 2.6, 2.0, "tyre", sides=8, axis="v") for s in (-1, 1)]
    p.append(cbox(3, 2, 10, 1.0, 1.0, 4, "black"))                           # exhaust
    p += lamps(hl, -hl, 7, 7.0, 8.2, tail=False)
    return p


def maintenance_pickup():
    return pickup(body="orange") + [cbox(1.0, 0, 12.5, 2.0, 2.0, 1.3, "beacon")]


def snowplough():
    L, W = 36, 14
    hl, hw = L / 2, W / 2
    hopper = box(-hl, hl - 10, -hw + 0.5, hw - 0.5, 3.2, 14.0, "orange")
    hopper.cut((0, 1, 0.8), (0, hw - 0.5, 9)).cut((0, -1, 0.8), (0, -hw + 0.5, 9))
    blade = box(hl + 1, hl + 3, -hw - 2.5, hw + 2.5, 0.5, 7.0, "hazard")
    blade.cut((1, 1.2, 0), (hl + 3, -hw - 2.5, 0))                             # angled blade
    return rigid_truck(L, W, 9, 15, [hopper, blade, cbox(hl + 0.5, 0, 2, 2, 6, 3, "black")], "orange") + \
        [cbox(hl - 4.5, 0, 15.0, 2.0, 2.0, 1.3, "beacon")]


def bus(col="blue", L=58, articulated_half=None):
    W, H = 15, 22
    hl, hw = L / 2, W / 2
    shell = box(-hl, hl, -hw, hw, 2.6, H, col)
    shell.cut((1, 0, 0.12), (hl, 0, H))
    band = box(-hl + 2, hl + 0.2, -hw - 0.2, hw + 0.2, 10.5, H - 2.5, "glass")
    band.cut((1, 0, 0.12), (hl + 0.25, 0, H))
    screen = box(hl - 1, hl + 0.3, -hw + 0.8, hw - 0.8, 6.0, H - 1.5, "glass")
    screen.cut((1, 0, 0.12), (hl + 0.35, 0, H))
    posts = [box(u - 0.7, u + 0.7, -hw - 0.3, hw + 0.3, 10.5, H - 2.5, col) for u in range(int(-hl + 9), int(hl - 4), 9)]
    p = [shell, band, screen] + posts
    p.append(box(-hl + 4, hl - 12, -hw + 3, hw - 3, H, H + 1.4, "grey"))   # roof equipment
    p.append(box(-hl + 1, hl - 1, -hw + 0.6, hw - 0.6, 0.8, 2.7, "chassis"))
    p += wheels([-hl + 9, hl - 8], hw - 1.0, 3.2, 2.2)
    p += lamps(hl - 0.2, -hl, hw, 4.0, 5.6)
    p.append(box(hl - 0.3, hl + 0.4, -4, 4, H - 3.2, H - 1.6, "dest"))    # destination board
    return p


def bus_segment(front=True, col="blue"):
    """Articulated bus halves (each drawn as its own sprite, joined by a bellows)."""
    L = 40
    hl = L / 2
    p = bus(col, L=L)
    if front:
        p = [q for q in p if q.mat != "tail"]
        p.append(box(-hl - 2.5, -hl, -6.5, 6.5, 3.5, 19.5, "black"))       # bellows
    else:
        p = [q for q in p if q.mat not in ("head", "dest")]
        p = [q for q in p if not (q.mat == "glass" and q.planes[0, 3] > hl + 0.25)]
    return p


def wreck():
    """Crashed sedan: crumpled nose, scorched body, skewed on its lane."""
    p = car(24, 11.5, 2.0, 6.8, 10.0, -7.5, 4.0, ws=0.9, rw=0.5, body="wreck", glass="black")
    p[0].cut((1, 0.7, 0.4), (10, -2, 5))
    return [q for q in p if q.mat != "head"]


MODELS = {
    "hatchback": lambda: scaled(hatchback()), "sedan": lambda: scaled(sedan()), "estate": lambda: scaled(estate()), "suv": lambda: scaled(suv()), "mpv": lambda: scaled(mpv()), "pickup": lambda: scaled(pickup()),
    "sports": lambda: scaled(sports()), "taxi": lambda: scaled(taxi()), "police": lambda: scaled(police()), "hearse": lambda: scaled(hearse()), "delivery-van": delivery_van,
    "ambulance": ambulance, "postal-van": postal_van, "box-truck": box_truck, "garbage-truck": garbage_truck,
    "fire-engine": fire_engine, "tanker-truck": tanker_truck, "tipper-truck": tipper_truck,
    "timber-truck": timber_truck, "semi-tractor": tractor_unit, "farm-tractor": farm_tractor,
    "maintenance": lambda: scaled(maintenance_pickup()), "snowplough": snowplough, "bus": bus,
    "artic-bus-front": lambda: bus_segment(True), "artic-bus-rear": lambda: bus_segment(False),
    "trailer-box": lambda: trailer("box"), "trailer-tank": lambda: trailer("tank", "silver"),
    "trailer-container": lambda: trailer("container", "container2"), "trailer-flat": lambda: trailer("flat"),
    "wreck": lambda: scaled(wreck()),
}
