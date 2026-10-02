"""
iso_life_people_base - templates, palette and drawing for peeps/animals (LIFE workstream).

Sprites are character grids designed for a body facing screen-RIGHT (E = face view, N = back view).
S and W are the mirror images. Colour keys: H hair, S skin, T shirt (A = sleeve, same group), P trousers,
K shoe (fixed), C hat/cap, U umbrella, V hi-vis, any other key via the colour dict (fixed colour).
Shaded keys get left=lit, right=dark per row run (light from upper left).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_common import canvas, hexrgb, rgba, save_png, preview, blit  # noqa: E402,F401
from iso_life_common import strip as _strip  # noqa: E402

DIRS = ["N", "E", "S", "W"]
FRONT = {"N": "B", "E": "F", "S": "F", "W": "B"}      # face visible when facing the viewer (E, S)
MIRROR = {"N": False, "E": False, "S": True, "W": True}
SHADED = set("HSTPCUVJD")
GROUP = {"A": "T"}

SKIN = ["f4cfae", "e3ac7c", "c68642", "8d5524", "5c3a21"]
HAIR = ["1c1816", "55361f", "d9b45c", "b4532a", "c9c9c9", "f2f2f2"]
SHIRT = ["d8383a", "3b6fd9", "ecc92c", "34a84a", "efefef", "ec8a2a", "8d4cbc", "2fb8b2"]
PANTS = ["2c3b63", "4a4a50", "b39d6c", "6b4a30"]
SHOE = "2a2118"


def tri(hexc):
    """(lit, mid, dark) of a base colour; shadows cool, lights warm."""
    b = hexrgb(hexc)
    lit = b + (255 - b) * 0.22 + np.array([6, 3, -6])
    dark = b * 0.66 + np.array([-4, -1, 10])
    f = lambda c: np.clip(np.round(c), 0, 255).astype(np.uint8)
    return f(lit), f(b), f(dark)


def blank(w, h):
    return [["."] * w for _ in range(h)]


def stamp(g, x, y, rows, over=True):
    """Draw string rows into grid g at (x, y); '.' is transparent, ' ' leaves existing pixel."""
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in ". ":
                continue
            xx, yy = x + i, y + j
            if 0 <= yy < len(g) and 0 <= xx < len(g[0]) and (over or g[yy][xx] == "."):
                g[yy][xx] = ch
    return g


def mirror(g):
    return [r[::-1] for r in g]


def render(g, cols, flip=False, outline=None):
    """Grid (list of char lists) -> RGBA. cols: key -> hex (SHADED keys get lit/mid/dark by row run)."""
    if flip:
        g = mirror(g)
    h, w = len(g), len(g[0])
    out = canvas(w, h)
    tabs = {}
    for k, v in cols.items():
        tabs[k] = tri(v) if k in SHADED else rgba(v)
    if "T" in cols and "A" not in cols:
        tabs["A"] = tabs["T"]
    for y in range(h):
        x = 0
        while x < w:
            ch = g[y][x]
            if ch == "." or ch not in tabs:
                x += 1
                continue
            grp = GROUP.get(ch, ch)
            x2 = x
            while x2 < w and GROUP.get(g[y][x2], g[y][x2]) == grp and g[y][x2] in tabs:
                x2 += 1
            n = x2 - x
            for xx in range(x, x2):
                t = tabs[g[y][xx]]
                if isinstance(t, tuple):
                    i = 1
                    if n >= 2 and xx == x:
                        i = 0
                    elif n >= 2 and xx == x2 - 1:
                        i = 2
                    elif n == 2:
                        i = 1
                    out[y, xx] = rgba(t[i])
                else:
                    out[y, xx] = t
            x = x2
    if outline is not None:
        out = add_outline(out, outline)
    return out


def add_outline(img, c):
    """1 px outline on a canvas grown by 1 px on every side."""
    h, w = img.shape[:2]
    o = canvas(w + 2, h + 2)
    a = img[..., 3] > 0
    p = np.pad(a, 1)
    n = np.zeros_like(p)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        n |= np.roll(np.roll(p, dx, 1), dy, 0)
    o[n & ~p] = rgba(c)
    blit(o, img, 1, 1)
    return o


def person_cols(skin=0, hair=1, shirt=0, pants=0, hat=None, extra=None):
    c = {"S": SKIN[skin % 5], "H": HAIR[hair % 6], "T": SHIRT[shirt % 8], "P": PANTS[pants % 4], "K": SHOE}
    if hat:
        c["C"] = hat
    if extra:
        c.update(extra)
    return c


def strip(frames, gap=0):
    """Strips are always gapless so the engine can slice them by frame width."""
    return _strip(frames, 0)


def sheet(frames_by_dir, gap=1):
    """dict dir -> list of frames -> list of strips (N,E,S,W order)."""
    return [strip(frames_by_dir[d], gap) for d in DIRS]


def write(root, rel, img):
    save_png(os.path.join(root, rel), img)
    return rel


def item(root, rel, img, label):
    write(root, rel, img)
    return {"file": rel, "label": label}


def pv(name, img, k=8, bg=(120, 150, 110)):
    d = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "scratch_life", "people"))
    os.makedirs(d, exist_ok=True)
    preview(os.path.join(d, name), img, k, bg)
