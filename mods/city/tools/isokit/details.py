"""Detail patterns: stripes, bands, doors, emissive lamps."""
import numpy as np

from .palette import RAMP
from .materials import _fl


def pat_stripes(c, m):
    """Alternating vertical stripes (awnings, hazard paint). Params: ramp2, width, tone2."""
    w = m.p("width", 3)
    sel = (_fl(c.u - c.u0) // w) % 2 == 1
    c.ramp[sel] = RAMP[m.p("ramp2", "snow")]
    c.tone[sel] += m.p("tone2", 1.0)


def pat_bands(c, m):
    """Horizontal colour bands up the face (chimneys, silos). Params: ramp2, height, tone2."""
    hgt = m.p("height", 6)
    sel = (_fl(c.v - c.v0) // hgt) % 2 == 1
    c.ramp[sel] = RAMP[m.p("ramp2", "snow")]
    c.tone[sel] += m.p("tone2", 1.0)


def pat_door(c, m):
    """Panel door / garage: lighter frame rim, optional slats, optional lit glass at night."""
    u, v = c.u - c.u0, c.v - c.v0
    rim = (u < 1) | (u >= c.u1 - c.u0 - 1) | (v >= c.v1 - c.v0 - 1)
    c.tone[rim] += 1.2
    if m.p("slats", False):
        c.tone[(_fl(v) % 2 == 0) & ~rim] -= 0.6
    if m.p("lit", False) and c.night:
        c.emit |= ~rim
        c.eramp[~rim] = RAMP["yellow"]
        c.eshade[~rim] = 9.0


def pat_emissive(c, m):
    """Whole surface glows at night (lamp heads, neon, signal lights). Params: eshade, eramp."""
    if c.night:
        c.emit[:] = True
        c.eramp[:] = RAMP[m.p("eramp", "yellow")]
        c.eshade[:] = m.p("eshade", 10.0)


def paint(c, mat_, sel, base_shade=0.0):
    """Inside a pattern: render material `mat_` (ramp, shade, pattern) on the pixels `sel` only.
    base_shade is the shade of the material currently being rendered (its m.shade)."""
    from .windows import pat_part
    from .materials import mat as _m
    mm = _m(mat_)
    if not np.any(sel):
        return

    def fn(cc, _):
        cc.ramp[:] = mm.ramp
        cc.tone[:] = mm.shade - base_shade
        if mm.pattern is not None:
            mm.pattern(cc, mm)
    pat_part(c, fn, mm, sel)
