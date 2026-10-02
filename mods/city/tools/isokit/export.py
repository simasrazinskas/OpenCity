"""Engine-facing export helpers (see design/iso/ENGINE-PLAN.md):
- day + '-lit' companion frames (same size/anchor, lit pixels only, alpha 0/255)
- even frame sizes
- footprint-extent check (buildings are sliced into 32-px strips inside the footprint's x-extent)
"""
import numpy as np

from .render import render, Sprite, crop_to_content
from .png import write_png


def day_and_lit(scene, facing=0, **kw):
    """Render a day sprite and its night-emissive companion with identical size and anchor.
    The lit sprite holds only emissive pixels (windows, lamps, signs) in their night colours."""
    kw.pop("night", None)
    d = render(scene, facing=facing, crop=False, **kw)
    n = render(scene, facing=facing, night=True, crop=False, **kw)
    lit = np.zeros_like(d.img)
    lit[n.emit] = n.img[n.emit]
    a = d.img[..., 3] > 0
    rows, cols = np.where(a.any(1))[0], np.where(a.any(0))[0]
    if len(rows) == 0:
        return d, Sprite(lit, d.ax, d.ay, n.emit, d.footprint)
    r0, r1, c0, c1 = rows[0], rows[-1] + 1, cols[0], cols[-1] + 1
    ds = Sprite(d.img[r0:r1, c0:c1], d.ax - c0, d.ay - r0, None, d.footprint)
    ls = Sprite(lit[r0:r1, c0:c1], d.ax - c0, d.ay - r0, n.emit[r0:r1, c0:c1], d.footprint)
    return ds, ls


def even(sprite):
    """Pad right/bottom so width and height are even (anchor unchanged)."""
    h, w = sprite.img.shape[:2]
    H, W = h + (h % 2), w + (w % 2)
    if (H, W) == (h, w):
        return sprite
    img = np.zeros((H, W, 4), np.uint8)
    img[:h, :w] = sprite.img
    em = np.zeros((H, W), bool)
    em[:h, :w] = sprite.emit
    return Sprite(img, sprite.ax, sprite.ay, em, sprite.footprint)


def footprint_overhang(sprite):
    """Pixels of content left/right of the footprint's horizontal screen extent: (left_px, right_px).
    The engine slices buildings into strips within [ax - (fx+fy)*16, ax + (fx+fy)*16)."""
    fx, fy = sprite.footprint
    half = (fx + fy) * 16
    a = sprite.img[..., 3] > 0
    cols = np.where(a.any(0))[0]
    if len(cols) == 0:
        return 0, 0
    left = max(0, (sprite.ax - half) - cols[0])
    right = max(0, cols[-1] + 1 - (sprite.ax + half))
    return int(left), int(right)


def clip_footprint(sprite):
    """Cut content outside the footprint's horizontal extent (alpha 0)."""
    fx, fy = sprite.footprint
    half = (fx + fy) * 16
    img = sprite.img.copy()
    x0, x1 = sprite.ax - half, sprite.ax + half
    if x0 > 0:
        img[:, :x0] = 0
    if x1 < img.shape[1]:
        img[:, x1:] = 0
    return Sprite(img, sprite.ax, sprite.ay, sprite.emit, sprite.footprint)


def save_pair(path, scene, facing=0, **kw):
    """Write <path> (day) and <path minus .png>-lit.png; both even-sized, same anchor."""
    d, l = day_and_lit(scene, facing, **kw)
    d, l = even(d), even(l)
    d.save(path)
    lp = path[:-4] + "-lit.png"
    l.save(lp)
    return path, lp


def thumbnail(scene, size=64, ground="grass", facing=0, margin=1, max_tile=None, **kw):
    """Build-menu thumbnail: the scene on its own footprint ground, default facing, rendered at the
    largest reduced projection (tile width a multiple of 4, <= max_tile, default 1.5 x size)
    that fits size x size, then bottom-centred on a transparent square. Returns a Sprite whose
    .tile is the tile width used."""
    from .scene import Scene
    fx, fy = scene.footprint
    s = scene
    if ground:
        s = Scene(scene.footprint, scene.seed)
        for cx in range(int(fx)):
            for cy in range(int(fy)):
                s.ground([(cx, cy), (cx + 1, cy), (cx + 1, cy + 1), (cx, cy + 1)], ground, layer=-1)
        s.merge(scene)
    max_tile = max_tile or int(size * 1.5) // 4 * 4
    inner = size - 2 * margin
    spr, tw = None, 4
    for tw in range(max_tile, 3, -4):
        spr = render(s, facing=facing, tile=(tw, tw // 2), **kw)
        if spr.w <= inner and spr.h <= inner:
            break
    img = np.zeros((size, size, 4), np.uint8)
    x = (size - spr.w) // 2
    y = size - margin - spr.h
    img[y:y + spr.h, x:x + spr.w] = spr.img[:size, :size]
    em = np.zeros((size, size), bool)
    em[y:y + spr.h, x:x + spr.w] = spr.emit[:size, :size]
    out = Sprite(img, spr.ax + x, spr.ay + y, em, spr.footprint)
    out.tile = tw
    return out
