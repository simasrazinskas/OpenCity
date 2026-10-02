"""iso_life_msel - selection/hover markers, vehicle rings, placement ghosts, bulldoze highlight."""
import numpy as np

from iso_life_common import canvas, blit
from iso_life_mdraw import col, outline_mask, BAYER4, ellipse_mask, px, shade, mix
import iso_life_mgeo as G

DARK = "#26262c"
YEL, YEL_L, YEL_D = "#f8d020", "#fff390", "#d89a10"


def _fp_canvas(nx, ny):
    w, h = G.fp_size(nx, ny)
    return canvas(w, h)


def brackets(nx, ny, kind="hover", frame=0):
    """Corner brackets around an nx x ny footprint (kind: hover | selected)."""
    img = _fp_canvas(nx, ny)
    if kind == "hover":
        band = G.fp_edge_band(nx, ny, 1, arm=10 + 4 * max(nx, ny))
        img[band] = col("#ffffff")
        img[outline_mask(band)] = col(DARK)
        return img
    arm = 10 + 4 * max(nx, ny) + [0, 2, 4, 2][frame % 4]
    c = [YEL, YEL_L, YEL, YEL_D][frame % 4]
    band = G.fp_edge_band(nx, ny, 2, arm=arm)
    img[band] = col(c)
    img[outline_mask(band)] = col(DARK)
    return img


def ghost_fp(nx, ny, valid=True):
    """Dithered footprint fill (ground shows through). valid green / invalid red."""
    img = _fp_canvas(nx, ny)
    X, Y, w, h = G.fp_coords(nx, ny)
    inside = G.fp_inside(nx, ny)
    base = "#3ed05a" if valid else "#e03a2e"
    lite = "#a8f8b0" if valid else "#ffb0a0"
    dk = "#1c7a30" if valid else "#8a1a14"
    ys, xs = np.mgrid[0:h, 0:w]
    dith = BAYER4[ys % 4, xs % 4] < 7
    fill = inside & dith
    img[fill] = col(base)
    cells = G.fp_cell_lines(nx, ny) & ((xs + ys) % 2 == 0)
    img[cells] = col(lite)
    band = G.fp_edge_band(nx, ny, 1)
    img[band] = col(lite)
    band2 = G.fp_edge_band(nx, ny, 2) & ~band & ((xs + ys) % 2 == 0)
    img[band2] = col(dk)
    return img


def _tint_dither(src, tint, strength, keep_below, outline=None):
    """Tint sprite toward `tint`, drop pixels by Bayer threshold (ghost look)."""
    h, w = src.shape[:2]
    out = src.copy()
    m = src[..., 3] > 0
    t = col(tint).astype(float)
    rgb = out[..., :3].astype(float)
    rgb = rgb * (1 - strength) + t[:3] * strength
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    ys, xs = np.mgrid[0:h, 0:w]
    drop = m & (BAYER4[ys % 4, xs % 4] < keep_below)
    out[drop] = 0
    if outline is not None:
        o = outline_mask(m, diag=False) & ((xs + ys) % 2 == 0)
        out[o] = col(outline)
    return out


def ghost_building(src, valid=True):
    if valid:
        return _tint_dither(src, "#40e070", 0.55, 4, "#7affa0")
    return _tint_dither(src, "#ff4030", 0.55, 4, "#ff6a58")


def bulldoze_building(src):
    h, w = src.shape[:2]
    out = src.copy()
    m = src[..., 3] > 0
    ys, xs = np.mgrid[0:h, 0:w]
    red = m & (BAYER4[ys % 4, xs % 4] < 8)
    out[red] = col("#d82a20")
    out[outline_mask(m, diag=True)] = col("#ff3a2a")
    out[outline_mask(m | outline_mask(m, True), diag=False) & ~m & ~outline_mask(m, True)] = col(DARK)
    return out


def bulldoze_fp(nx, ny):
    img = _fp_canvas(nx, ny)
    X, Y, w, h = G.fp_coords(nx, ny)
    ys, xs = np.mgrid[0:h, 0:w]
    inside = G.fp_inside(nx, ny)
    img[inside & (BAYER4[ys % 4, xs % 4] < 5)] = col("#d82a20")
    band = G.fp_edge_band(nx, ny, 1)
    img[band] = col("#ff4a3a")
    return img


# ----------------------------------------------------------------------------- vehicle / citizen markers
def ring(w, h, thick=True, c=YEL):
    """2:1 ellipse ring with dark outline. Returns (w+2) x (h+2) sprite."""
    img = canvas(w + 2, h + 2)
    cx, cy = (w + 2) / 2.0, (h + 2) / 2.0
    ys, xs = np.mgrid[0:h + 2, 0:w + 2]
    d = ((xs + 0.5 - cx) / (w / 2.0)) ** 2 + ((ys + 0.5 - cy) / (h / 2.0)) ** 2
    inner = 0.70 if thick else 0.82
    m = (d <= 1.0) & (d >= inner)
    img[m] = col(c)
    img[outline_mask(m, diag=False)] = col(DARK)
    return img


def chevron(frame=0, c=YEL):
    """Down-pointing follow chevron, bobbing over 4 frames (dy 0,1,2,1). 14x14 sprite."""
    img = canvas(14, 14)
    dy = [0, 1, 2, 1][frame % 4]
    rows = [
        "ccccc..ccccc",
        ".ccccc.ccccc"[:12],
    ]
    shape = [
        "cc........cc",
        "ccc......ccc",
        ".ccc....ccc.",
        "..ccc..ccc..",
        "...cccccc...",
        "....cccc....",
        ".....cc.....",
    ]
    m = np.zeros((14, 14), bool)
    for y, r in enumerate(shape):
        for x, ch in enumerate(r):
            if ch == "c":
                m[y + 1 + dy, x + 1] = True
    img[m] = col(c)
    ys, xs = np.mgrid[0:14, 0:14]
    hi = m & np.roll(~m, 1, 0) & (xs % 2 == 0)
    img[outline_mask(m)] = col(DARK)
    return img
