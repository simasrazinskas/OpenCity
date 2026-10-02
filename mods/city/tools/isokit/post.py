"""Image post-processing that stays on the palette: night conversion of finished sprites, desaturation
(info views), palette snapping."""
import numpy as np

from .palette import PALETTE, FLAT, SHADES, RAMP, shade_lookup
from .render import night_color

_W = np.array([0.30, 0.59, 0.11])
_NIGHT_LUT = None


def _nearest_idx(rgb):
    """Index into FLAT of the nearest palette colour for (N, 3) colours."""
    pf = FLAT.astype(np.float64)
    out = np.empty(len(rgb), np.int64)
    for a in range(0, len(rgb), 32768):
        c = rgb[a:a + 32768].astype(np.float64)
        d = (((c[:, None, :] - pf[None]) ** 2) * _W).sum(-1)
        out[a:a + 32768] = np.argmin(d, 1)
    return out


def _night_lut():
    global _NIGHT_LUT
    if _NIGHT_LUT is None:
        n = len(FLAT)
        ramps = np.arange(n) // SHADES
        shades = np.arange(n) % SHADES
        idx = shades * 0.68 - 1.3                     # same rule as render(night=True)
        col = shade_lookup(ramps, idx)
        _NIGHT_LUT = night_color(col, ramps)
    return _NIGHT_LUT


def _map_unique(img, fn):
    a = img[..., 3] > 0
    px = img[..., :3][a]
    if not len(px):
        return img.copy()
    key = (px[:, 0].astype(np.int64) << 16) | (px[:, 1].astype(np.int64) << 8) | px[:, 2]
    uk, inv = np.unique(key, return_inverse=True)
    ucol = np.stack([(uk >> 16) & 255, (uk >> 8) & 255, uk & 255], 1)
    out = img.copy()
    out[..., :3][a] = fn(ucol)[inv]
    return out


def nightify(img, keep=None):
    """Night version of a finished day sprite/tile: every colour snaps to its palette entry and takes the
    same night mapping as render(night=True). keep: bool mask of pixels left unchanged (lit pixels)."""
    lut = _night_lut()
    out = _map_unique(img, lambda c: lut[_nearest_idx(c)])
    if keep is not None:
        out[keep] = img[keep]
    return out


def desaturate(img, amount=1.0, shade_shift=0.0):
    """Info-view base: map colours onto the grey ramp by luminance (amount 1 = fully grey)."""
    grey = PALETTE[RAMP["grey"]].astype(np.float64)
    gl = (grey * _W).sum(1)

    def fn(c):
        lum = (c.astype(np.float64) * _W).sum(1)
        g = grey[np.clip(np.searchsorted(gl, lum) + int(shade_shift), 0, SHADES - 1)]
        return np.round(c * (1 - amount) + g * amount).astype(np.uint8) if amount < 1 else g.astype(np.uint8)
    return _map_unique(img, fn)


def snap(img):
    """Snap all colours to the nearest palette entry."""
    return _map_unique(img, lambda c: FLAT[_nearest_idx(c)])
