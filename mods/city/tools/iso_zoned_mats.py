"""iso_zoned_mats.py - ZONED wall/facade/state materials on top of isokit.

pat_facade: punched windows with sills, optional shutters, doors, garage doors and a shop band,
plus the building states (abandoned: boarded windows + grime, burnt: black windows + soot).
st(): wraps any isokit material so it follows the building state (grime / soot, no night light).
"""
import numpy as np

import isokit as ik
from isokit.materials import Material, _base, _fl
from isokit.noise import hash2, value2
from isokit.palette import RAMP


def _grime(c, state, amt=1.0):
    """Shared state look for any surface: abandoned = darker, streaky, desaturated patches;
    burnt = charred black with soot plumes; frame = raw concrete."""
    if state == "worn":
        # weathered but inhabited: rain streaks and stains, lights stay on
        streak = value2(c.sx * 0.9, c.sy * 0.12, 3.0, 75)
        c.tone -= (streak > 0.55) * 0.9 * amt
        stain = value2(c.sx, c.sy, 5.0, 76) > 0.7
        c.tone[stain] -= 0.6 * amt
    elif state == "abandoned":
        streak = value2(c.sx * 0.9, c.sy * 0.12, 3.0, 71)
        c.tone -= 0.9 * amt + (streak > 0.6) * 0.9 * amt
        patch = value2(c.sx, c.sy, 6.0, 72) > 0.62
        c.ramp[patch] = RAMP["stone"]
        c.tone += (hash2(c.sx, c.sy, 73) - 0.5) * 0.8
        c.emit[:] = False
    elif state == "burnt":
        soot = value2(c.sx * 0.7, c.sy * 0.25, 4.0, 74)
        c.tone -= 3.0 * amt + soot * 2.5
        black = soot > 0.55
        c.ramp[black] = RAMP["slate"]
        c.tone[black] -= 1.0
        c.emit[:] = False


def pat_state(c, m):
    inner = m.p("inner", None)
    if inner is not None:
        c.ramp[:] = inner.ramp
        if inner.pattern is not None:
            inner.pattern(c, inner)
    _grime(c, m.p("state", "ok"), m.p("amt", 1.0))


def st(material, state="ok", amt=1.0):
    """Material following a building state. 'ok' returns it unchanged."""
    m = ik.mat(material)
    if state in ("ok", None):
        return m
    return Material(m.ramp, m.shade, pat_state, dither=m.dither, snow=m.snow, inner=m, state=state, amt=amt)


def pat_facade(c, m):
    """Facade with punched windows. Params:
    base (wall material), storey, ground (px before the first window floor), floors (max, 0 = fill),
    win_w, win_h, period, sill, margin, frame (ramp of sill/lintel), shutter (ramp or None),
    glass, lit, doors (u fractions on the ground floor), door_w, door_h, door (ramp), garage
    ((frac, width_px) or None), shop (px height of a shop band on the ground floor, 0 = none), sign (ramp),
    band (tone of a string course under each floor, 0 = none), state."""
    _base(c, m)
    state = m.p("state", "ok")
    st_h = m.p("storey", ik.STOREY)
    gz = m.p("ground", 0)
    ww, wh, per = m.p("win_w", 3), m.p("win_h", 5), m.p("period", 7)
    sill, margin = m.p("sill", 3), m.p("margin", 3)
    u = c.u - c.u0
    v = c.v - c.v0
    wl = c.u1 - c.u0
    hgt = c.v1 - c.v0
    shop = m.p("shop", 0)
    # ---- ground floor: doors / garage / shop band
    occupied = np.zeros(len(u), bool)
    doors = []
    for f in m.p("doors", ()):
        dc = np.floor(wl * f)
        dw, dh = m.p("door_w", 4), m.p("door_h", 8)
        d = (np.abs(u - dc) < dw / 2) & (v < dh)
        doors.append((d, dc, dw, dh))
        occupied |= np.abs(u - dc) < dw / 2 + 2
    g = m.p("garage", None)
    gar = np.zeros(len(u), bool)
    if g is not None:
        gc, gw = np.floor(wl * g[0]), g[1]
        gar = (np.abs(u - gc) < gw / 2) & (v < 8)
        occupied |= np.abs(u - gc) < gw / 2 + 2
    if shop:
        sb = v < shop
        sign = sb & (v >= shop - 3)
        pane = sb & (v >= 1) & (v < shop - 4)
        mul = pane & ((_fl(u) % m.p("pane", 6)) == 0)
        gl = pane & ~mul & ~occupied
        c.ramp[sign] = RAMP[m.p("sign", "red")]
        c.tone[sign] = 0.8
        c.tone[sign & ((_fl(u) % 3) == 1) & (_fl(v) == shop - 2)] += 2.5
        c.tone[sb & (v < 1)] -= 1.2
        c.ramp[gl] = RAMP[m.p("glass", "glass")]
        c.tone[gl] = -1.0 + ((_fl(u[gl] + v[gl]) % 7) < 2) * 1.3
        c.tone[mul] += 0.8
        if c.night and state == "ok":
            c.emit |= gl
            c.eramp[gl] = RAMP["yellow"]
            c.eshade[gl] = 10.0
            c.emit |= sign
            c.eramp[sign] = RAMP[m.p("sign", "red")]
            c.eshade[sign] = 8.0
        gz = max(gz, shop)
    # ---- window grid
    n = int(max(0, (wl - 2 * margin + (per - ww)) // per))
    start = np.floor((wl - (n * per - (per - ww))) / 2.0)
    uu = u - start
    bay = np.floor(uu / per)
    inb = uu - bay * per
    inx = (inb >= 0) & (inb < ww) & (bay >= 0) & (bay < n)
    vv = v - gz
    floors = int(max(0, (hgt - gz + 2) // st_h))
    if m.p("floors", 0):
        floors = min(floors, m.p("floors", 0))
    fl = np.floor(vv / st_h)
    inf = vv - fl * st_h
    okf = (fl >= 0) & (fl < floors) & ~((fl == 0) & occupied & (gz == 0))
    win = inx & okf & (inf >= sill) & (inf < sill + wh)
    srow = inx & okf & (inf >= sill - 1) & (inf < sill)
    lrow = inx & okf & (inf >= sill + wh) & (inf < sill + wh + 1)
    if m.p("band", 0):
        c.tone[(vv >= 0) & (_fl(inf) == 0) & (fl >= 1)] += m.p("band", 0)
    fr = RAMP[m.p("frame", "snow")]
    c.ramp[srow] = fr
    c.tone[srow] = m.p("frame_tone", 0.0)
    c.ramp[lrow] = fr
    c.tone[lrow] = m.p("frame_tone", 0.0) - 1.0
    shut = m.p("shutter", None)
    if shut:
        rows = okf & (inf >= sill) & (inf < sill + wh)
        sh = rows & (((bay >= 0) & (bay < n) & (np.floor(inb) == ww))
                     | ((bay >= -1) & (bay < n - 1) & (np.floor(inb) == per - 1)))
        c.ramp[sh] = RAMP[shut]
        c.tone[sh] = 0.0
    key = hash2(bay, fl, c.seed + 5)
    if state == "abandoned":
        board = win & (key < 0.65)
        c.ramp[board] = RAMP["wood"]
        c.tone[board] = 0.5 - ((_fl(v[board]) % 2) == 0) * 1.0
        broken = win & ~board
        c.ramp[broken] = RAMP["slate"]
        c.tone[broken] = -4.5
    elif state == "burnt":
        c.ramp[win] = RAMP["slate"]
        c.tone[win] = -7.0
        above = inx & okf & (inf >= sill + wh) & (inf < sill + wh + 4)
        c.tone[above] -= 2.5
    elif state == "frame":
        c.ramp[win] = RAMP["slate"]
        c.tone[win] = -5.0
    else:
        c.ramp[win] = RAMP[m.p("glass", "glass")]
        top = inf >= sill + wh - 1
        c.tone[win] = m.p("glass_tone", -3.4)
        c.tone[win & (np.floor(inf) == sill)] -= 0.8
        c.tone[win & (np.floor(inb) == 0) & top] += 3.4
        if ww >= 4:
            mul = win & (np.floor(inb) == ww // 2)
            c.ramp[mul] = fr
            c.tone[mul] = -0.5
        if c.night:
            lit = win & (key < m.p("lit", 0.5))
            warm = hash2(bay, fl, c.seed + 6)
            c.emit |= lit
            c.eramp[lit] = np.where(warm[lit] < 0.75, RAMP["yellow"], RAMP["sand"])
            c.eshade[lit] = np.where(warm[lit] < 0.4, 10.0, 9.0)
    # ---- doors and garage drawn last
    for d, dc, dw, dh in doors:
        c.ramp[d] = RAMP[m.p("door", "wood")]
        c.tone[d] = -1.0
        c.tone[d & (np.abs(u - dc) >= dw / 2 - 1)] = 1.5        # frame
        c.ramp[d & (np.abs(u - dc) >= dw / 2 - 1)] = fr
        if state == "abandoned":
            c.ramp[d] = RAMP["wood"]
            c.tone[d] = 0.5 - ((_fl(v[d]) % 2) == 0) * 1.0
        elif state == "burnt":
            c.ramp[d] = RAMP["slate"]
            c.tone[d] = -7.0
        elif c.night:
            lamp = (np.abs(u - dc - dw / 2 - 1) < 0.6) & (_fl(v) == dh - 2)
            c.emit |= lamp
            c.eramp[lamp] = RAMP["yellow"]
            c.eshade[lamp] = 11.0
    if gar.any():
        c.ramp[gar] = RAMP[m.p("garage_ramp", "stone")]
        c.tone[gar] = 2.0 - ((_fl(v[gar]) % 2) == 0) * 1.2
        if state == "burnt":
            c.ramp[gar] = RAMP["slate"]
            c.tone[gar] = -6.0
    _grime(c, state, 0.8 if state == "abandoned" else 0.7)


def facade(base, state="ok", **kw):
    """Facade material over base wall material (name or Material)."""
    b = ik.mat(base)
    return Material(b.ramp, b.shade, pat_facade, dither=b.dither, base=b, state=state, **kw)


def pat_stripes(c, m):
    """Awning / sign stripes along u. Params: a, b (ramps), width."""
    w = m.p("width", 3)
    alt = (_fl(c.u - c.u0) // w) % 2 == 1
    c.ramp[alt] = RAMP[m.p("b", "snow")]
    c.tone[alt] += m.p("b_tone", 1.5)
    hem = (c.v - c.v0) < 1
    c.tone[hem] -= 1.0


def stripes(a, b="snow", width=3, shade=0.5):
    return Material(a, shade, pat_stripes, dither=0.0, b=b, width=width)


def pat_panels(c, m):
    """Solar panel grid."""
    u, v = _fl(c.u), _fl(c.v)
    c.tone[(u % 4 == 0) | (v % 3 == 0)] += 1.6
    c.tone += ((u + v) % 9 == 0) * 1.2


def pat_lattice(c, m):
    """Crane / pylon lattice: diagonal braces."""
    u, v = _fl(c.u), _fl(c.v)
    k = (u + v) % 4 == 0
    c.tone[~k] -= 0.8
    c.tone[(v % 4) == 0] += 0.6


def pat_signboard(c, m):
    """Sign with light 'lettering' blocks; glows at night."""
    u, v = _fl(c.u - c.u0), _fl(c.v - c.v0)
    hgt = c.v1 - c.v0
    let = (v >= 1) & (v < hgt - 1) & (u >= 1) & (u < c.u1 - c.u0 - 1) & (hash2(u // 2, v // 2, c.seed) < 0.55)
    c.ramp[let] = RAMP[m.p("text", "snow")]
    c.tone[let] = 2.5
    if c.night and m.p("state", "ok") == "ok":
        c.emit[:] = True
        c.eramp[:] = m.ramp
        c.eshade[:] = 8.0
        c.eramp[let] = RAMP[m.p("text", "snow")]
        c.eshade[let] = 11.0


def signboard(ramp, text="snow", state="ok"):
    return Material(ramp, 0.5, pat_signboard, dither=0.0, snow=False, text=text, state=state)


def lamp_mat():
    """Emissive lamp head (street / porch lights)."""
    def pat(c, m):
        if c.night:
            c.emit[:] = True
            c.eramp[:] = RAMP["yellow"]
            c.eshade[:] = 11.0
    return Material("yellow", 2.0, pat, dither=0.0, snow=False)


PANELS = Material("slate", -1.5, pat_panels, dither=0.0, snow=False)
LATTICE_Y = Material("yellow", 0.0, pat_lattice, dither=0.0)
LATTICE_R = Material("red", 0.0, pat_lattice, dither=0.0)
