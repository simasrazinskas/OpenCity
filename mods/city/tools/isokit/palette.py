"""The OpenCity iso palette: RCT2-like hue ramps of 12 shades each (index 0 = darkest).

Every rendered pixel is a palette colour. Materials pick a ramp by name and a shade
offset; lighting moves along the ramp. Shades are hue-shifted (cool, saturated darks;
warm, softer lights) like hand-made pixel-art ramps.
"""
import numpy as np

SHADES = 12

# name: (darkest, mid, lightest) anchors in sRGB; ramps are piecewise-interpolated through them.
_ANCHORS = {
    "grey":   ((18, 20, 28), (118, 122, 128), (238, 238, 232)),
    "stone":  ((30, 24, 22), (150, 138, 120), (244, 236, 214)),
    "wood":   ((26, 14, 10), (126, 80, 46), (236, 196, 146)),
    "sand":   ((44, 30, 18), (196, 164, 108), (252, 240, 200)),
    "brick":  ((32, 10, 12), (160, 66, 44), (250, 178, 140)),
    "terra":  ((38, 14, 8), (200, 98, 42), (255, 214, 150)),
    "yellow": ((44, 26, 6), (222, 168, 40), (255, 248, 188)),
    "olive":  ((24, 24, 10), (136, 132, 64), (232, 230, 168)),
    "grass":  ((10, 26, 12), (88, 148, 50), (214, 240, 146)),
    "leaf":   ((6, 20, 14), (46, 110, 52), (170, 214, 120)),
    "teal":   ((6, 24, 28), (40, 138, 130), (176, 236, 214)),
    "water":  ((8, 16, 44), (40, 96, 168), (176, 222, 244)),
    "glass":  ((14, 24, 46), (92, 148, 186), (224, 244, 250)),
    "slate":  ((14, 16, 30), (84, 94, 120), (206, 214, 228)),
    "purple": ((20, 10, 34), (116, 70, 148), (226, 196, 240)),
    "rose":   ((34, 8, 22), (190, 76, 112), (255, 206, 216)),
    "red":    ((40, 4, 6), (196, 30, 30), (255, 180, 150)),
    "snow":   ((40, 50, 80), (178, 192, 214), (255, 255, 255)),
}
RAMP_NAMES = list(_ANCHORS)
RAMP = {n: i for i, n in enumerate(RAMP_NAMES)}


def _build():
    pal = np.zeros((len(RAMP_NAMES), SHADES, 3), np.float64)
    mid = (SHADES - 1) * 0.55
    for i, n in enumerate(RAMP_NAMES):
        d, m, l = (np.array(c, np.float64) for c in _ANCHORS[n])
        for s in range(SHADES):
            if s <= mid:
                t = (s / mid) ** 0.9
                c = d + (m - d) * t
            else:
                t = (s - mid) / (SHADES - 1 - mid)
                c = m + (l - m) * t ** 1.1
            pal[i, s] = c
    return np.clip(np.round(pal), 0, 255).astype(np.uint8)


PALETTE = _build()               # (ramps, SHADES, 3) uint8
FLAT = PALETTE.reshape(-1, 3)    # (ramps*SHADES, 3)

BAYER4 = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]],
                   np.float64) + 0.5) / 16.0


def ramp(name):
    """Return the ramp index for a name (or pass an int through)."""
    return name if isinstance(name, (int, np.integer)) else RAMP[name]


def color(name, shade):
    """RGB tuple of a palette entry."""
    return tuple(int(v) for v in PALETTE[ramp(name), int(np.clip(shade, 0, SHADES - 1))])


def bayer(ys, xs):
    """Ordered-dither thresholds (0..1) for integer pixel coordinates."""
    return BAYER4[np.asarray(ys) % 4, np.asarray(xs) % 4]


def shade_lookup(ramp_ids, level, ys=None, xs=None, dither=0.0):
    """Map continuous shade level (0..SHADES-1) to RGB with optional ordered dither.

    dither=0 rounds; dither=1 dithers the full fractional part (2-colour ordered mix).
    """
    level = np.clip(level, 0, SHADES - 1)
    if dither and ys is not None:
        base = np.floor(level)
        frac = level - base
        th = 0.5 + (bayer(ys, xs) - 0.5) * dither
        idx = base + (frac > th)
    else:
        idx = np.round(level)
    idx = np.clip(idx, 0, SHADES - 1).astype(np.int64)
    return PALETTE[np.asarray(ramp_ids, np.int64), idx]


def nearest(rgb, ys=None, xs=None, dither=0.0, ramps=None):
    """Quantize arbitrary RGB (N, 3) to the palette (optionally restricted to `ramps`)."""
    rgb = np.asarray(rgb, np.float64)
    pal = FLAT if ramps is None else PALETTE[[ramp(r) for r in ramps]].reshape(-1, 3)
    if dither and ys is not None:
        rgb = rgb + (bayer(ys, xs)[..., None] - 0.5) * 24.0 * dither
    pf = pal.astype(np.float64)
    out = np.empty(rgb.shape[:-1] + (3,), np.uint8)
    flat = rgb.reshape(-1, 3)
    o = out.reshape(-1, 3)
    w = np.array([0.30, 0.59, 0.11]) * 3
    for a in range(0, len(flat), 65536):
        chunk = flat[a:a + 65536]
        d = (((chunk[:, None, :] - pf[None]) ** 2) * w).sum(-1)
        o[a:a + 65536] = pal[np.argmin(d, 1)]
    return out


def palette_sheet(sw=12, sh=12):
    """Return an RGBA image of the palette: one row per ramp."""
    n = len(RAMP_NAMES)
    img = np.zeros((n * sh, SHADES * sw, 4), np.uint8)
    for i in range(n):
        for s in range(SHADES):
            img[i * sh:(i + 1) * sh, s * sw:(s + 1) * sw, :3] = PALETTE[i, s]
    img[..., 3] = 255
    return img
