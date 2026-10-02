"""Window, curtain-wall and shopfront patterns (wall overlays on a base material)."""
import numpy as np

from .noise import hash2, fbm
from .palette import RAMP
from .geom import STOREY
from .materials import _base, _fl


def _glass(c, sel, m, bay, fl, top_row):
    """Paint glass pixels `sel` (bool) with day reflection / night lit state per (bay, fl)."""
    g = RAMP[m.p("glass", "slate")]
    c.ramp[sel] = g
    c.tone[sel] = m.p("glass_tone", -1.6)
    c.tone[sel & top_row] += 1.2
    # diagonal sky reflection across the facade
    refl = ((_fl(c.u + c.v * 0.5 + c.seed) % 11) < 2)
    c.tone[sel & refl] += 1.0
    if c.night:
        h = hash2(bay, fl, c.seed + 31)
        lit = sel & (h < m.p("lit", 0.55))
        warm = hash2(bay, fl, c.seed + 32)
        c.emit |= lit
        c.eramp[lit] = np.where(warm[lit] < 0.8, RAMP["yellow"], RAMP["sand"])
        c.eshade[lit] = np.where(warm[lit] < 0.4, 10.0, 9.0)
        c.eshade[lit & top_row] -= 1.0


def pat_windows(c, m):
    """Punched windows in storeys over a base wall. Params: base, storey, ground, win_w, win_h,
    period, sill, margin, glass, lit, top_margin."""
    _base(c, m)
    st = m.p("storey", STOREY)
    gz = m.p("ground", 0)
    ww, wh, per = m.p("win_w", 4), m.p("win_h", 5), m.p("period", 8)
    sill = m.p("sill", 3)
    margin = m.p("margin", 3)
    wl = c.u1 - c.u0
    n = int(max(0, (wl - 2 * margin + (per - ww)) // per))
    start = c.u0 + np.floor((wl - (n * per - (per - ww))) / 2.0)
    uu = c.u - start
    bay = np.floor(uu / per)
    inb = uu - bay * per
    inx = (inb >= 0) & (inb < ww) & (bay >= 0) & (bay < n)
    vv = c.v - c.v0 - gz
    floors = int(max(0, (c.v1 - c.v0 - gz - m.p("top_margin", 0)) // st))
    fl = np.floor(vv / st)
    inf = vv - fl * st
    okf = (fl >= 0) & (fl < floors)
    win = inx & okf & (inf >= sill) & (inf < sill + wh)
    sill_row = inx & okf & (inf >= sill - 1) & (inf < sill)
    lint = inx & okf & (inf >= sill + wh) & (inf < sill + wh + 1)
    c.tone[sill_row] += m.p("sill_tone", 1.6)
    c.tone[lint] -= 0.8
    top_row = inf >= sill + wh - 1
    if ww >= 5 and m.p("mullion", True):
        mul = win & (np.floor(inb) == ww // 2)
        c.tone[mul] += 1.0
        win = win & ~mul
    _glass(c, win, m, bay, fl, top_row)


def pat_curtain(c, m):
    """Glass curtain wall: mullion grid, spandrel bands per storey, sky reflections, lit offices."""
    st = m.p("storey", STOREY)
    pu = m.p("panel", 4)
    u, v = c.u - c.u0, c.v - c.v0
    fl = np.floor(v / st)
    inf = v - fl * st
    col = np.floor(u / pu)
    mull = (_fl(u) % pu) == 0
    spand = inf < m.p("spandrel", 2)
    refl = fbm(c.u * 0.6 - c.v * 0.6, c.v * 0.15 + c.u * 0.2, 9.0, 2, c.seed + 5)
    c.tone += (refl - 0.5) * 3.0 + 0.3 * (v / max(1.0, float(np.max(c.v1 - c.v0))))
    frame = RAMP[m.p("frame", "grey")]
    c.ramp[mull | spand] = frame
    c.tone[mull | spand] = m.p("frame_tone", 0.5)
    c.tone[spand & ~mull] -= 0.8
    glass = ~(mull | spand)
    if c.night:
        h = hash2(col // 2, fl, c.seed + 41)
        lit = glass & (h < m.p("lit", 0.5))
        c.emit |= lit
        c.eramp[lit] = np.where(hash2(col // 3, fl, c.seed + 9)[lit] < 0.6, RAMP["sand"], RAMP["glass"])
        c.eshade[lit] = 10.0


def pat_shopfront(c, m):
    """Ground-floor shop windows + sign band; upper floors get punched windows (pat_windows params)."""
    gh = m.p("shop_h", STOREY + 3)
    v = c.v - c.v0
    upper = v >= gh
    pat_part(c, pat_windows, m.with_(ground=gh), upper)
    pat_part(c, lambda cc, mm: _base(cc, mm), m, ~upper)
    u = c.u - c.u0
    wl = c.u1 - c.u0
    sign = (~upper) & (v >= gh - 3)
    plinth = v < 1
    pane = (~upper) & (v >= 1) & (v < gh - 4)
    mul = pane & ((_fl(u) % m.p("pane", 7)) == 0)
    door_c = np.floor(wl * m.p("door_at", 0.5))
    door = pane & (np.abs(u - door_c) < 2) & ~mul
    c.ramp[sign] = RAMP[m.p("sign", "red")]
    c.tone[sign] = 0.5
    c.tone[sign & ((_fl(u) % 3) == 1) & (np.floor(v) == gh - 2)] += 2.5  # lettering dots
    c.tone[plinth] -= 1.0
    c.tone[mul] += 0.6
    gl = pane & ~mul
    c.ramp[gl] = RAMP[m.p("glass", "glass")]
    c.tone[gl] = -1.2
    c.tone[gl & (v >= gh - 5)] += 0.8
    c.tone[door] = -2.5
    c.tone[gl & ((_fl(u + v) % 9) < 2)] += 1.2
    if c.night:
        lit = gl & ~door
        c.emit |= lit
        c.eramp[lit] = RAMP["yellow"]
        c.eshade[lit] = 10.0
        s2 = sign & (c.tone > 2)
        c.emit |= sign
        c.eramp[sign] = RAMP[m.p("sign", "red")]
        c.eshade[sign] = np.where(s2[sign], 11.0, 7.0)


def pat_part(c, fn, m, sel):
    """Apply pattern fn only where `sel` is True (keeps other pixels)."""
    if not sel.any():
        return
    keep = {k: getattr(c, k).copy() for k in ("ramp", "tone", "emit", "eramp", "eshade", "flat")}
    fn(c, m)
    for k, old in keep.items():
        arr = getattr(c, k)
        arr[~sel] = old[~sel]
