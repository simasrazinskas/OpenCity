"""Materials: a palette ramp + shade offset + procedural pattern in face-local UV.

A pattern is a function pattern(c, m) that edits the per-pixel context `c` in place:
  c.ramp  (int array)   palette ramp per pixel (starts as m.ramp)
  c.tone  (float array) shade offset in ramp steps, added to lighting
  c.emit  (bool array)  emissive pixels (stay bright at night)
  c.eramp, c.eshade     ramp/shade used for emissive pixels
  c.flat  (float array) if not NaN: absolute shade (ignores lighting), e.g. specular glints
Inputs available on c: u, v (face-local, 32 per cell horizontally, px vertically), x, y, z (world),
sx, sy (global integer screen px), n (N,3 metric normal), u0/u1/v0/v1 (face UV extent), kind, seed,
night (bool), frame (int, for animation), season (str).
"""
import numpy as np

from .noise import hash2, value2, fbm
from .palette import RAMP, ramp as ramp_id
from .geom import STOREY

REGISTRY = {}


class Material:
    def __init__(self, ramp, shade=0.0, pattern=None, dither=0.35, snow=True, name=None, **params):
        self.ramp = ramp_id(ramp)
        self.shade = float(shade)
        self.pattern = pattern
        self.dither = dither
        self.snow = snow
        self.params = params
        self.name = name

    def with_(self, **kw):
        """Copy with changed fields/params, e.g. mat('brick').with_(ramp='stone', shade=1)."""
        m = Material(self.ramp, self.shade, self.pattern, self.dither, self.snow, self.name, **self.params)
        for k, v in kw.items():
            if k == "ramp":
                m.ramp = ramp_id(v)
            elif k in ("shade", "pattern", "dither", "snow", "name"):
                setattr(m, k, v)
            else:
                m.params[k] = v
        return m

    def p(self, key, default):
        return self.params.get(key, default)


def mat(name_or_mat, **kw):
    """Look up a registered material by name (optionally overriding fields), or pass one through."""
    if isinstance(name_or_mat, Material):
        return name_or_mat.with_(**kw) if kw else name_or_mat
    m = REGISTRY[name_or_mat]
    return m.with_(**kw) if kw else m


def register(name, m):
    m.name = name
    REGISTRY[name] = m
    return m


def plain(ramp, shade=0.0, **kw):
    return Material(ramp, shade, None, **kw)


# ---------------------------------------------------------------- pattern helpers
def _fl(a):
    return np.floor(a).astype(np.int64)


def _speckle(c, amt, seed=0):
    c.tone += (hash2(c.sx, c.sy, c.seed + seed) - 0.5) * 2 * amt


# ---------------------------------------------------------------- wall patterns
def pat_noise(c, m):
    _speckle(c, m.p("amt", 0.25))


def pat_brick(c, m):
    v = _fl(c.v)
    course = m.p("course", 3)
    row = v // course
    mortar = (v % course) == course - 1
    off = (row % 2) * m.p("brick", 6) // 2
    jx = _fl(c.u + off) % m.p("brick", 6) == 0
    bid = hash2(_fl(c.u + off) // m.p("brick", 6), row, c.seed + 7)
    c.tone += (bid - 0.5) * 0.7
    c.tone[mortar] += m.p("mortar", 0.9)
    c.tone[jx & ~mortar] -= 0.6


def pat_siding(c, m):
    v = _fl(c.v)
    board = m.p("board", 3)
    c.tone[(v % board) == 0] -= 1.0
    c.tone[(v % board) == board - 1] += 0.4


def pat_concrete(c, m):
    pu, pv = m.p("panel_u", 16), m.p("panel_v", STOREY)
    u, v = _fl(c.u - c.u0), _fl(c.v)
    c.tone[(u % pu) == 0] -= 0.7
    c.tone[(v % pv) == 0] -= 0.7
    _speckle(c, 0.25, 3)


def pat_corrugated(c, m):
    u = _fl(c.u)
    c.tone[(u % 2) == 0] -= 0.9
    c.tone[(u % 4) == 1] += 0.3


def pat_planks(c, m):
    v = _fl(c.v)
    c.tone[(v % 2) == 0] -= 0.6
    _speckle(c, 0.3, 4)


REG_WALL = {}


def _base(c, m):
    """Apply the base wall pattern. The outer material's ramp/shade win (so mat('windows_brick',
    ramp='sand') recolours the wall) unless param base_color=True."""
    b = m.params.get("base")
    if b is not None:
        b = mat(b)
        if m.p("base_color", False):
            c.ramp[:] = b.ramp
            c.tone += b.shade - m.shade
        if b.pattern is not None:
            b.pattern(c, b)
