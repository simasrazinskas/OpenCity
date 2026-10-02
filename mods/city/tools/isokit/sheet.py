"""Facings, strips/sheets and the Figma manifest writer."""
import json
import os

import numpy as np

from .png import write_png
from .render import render, Sprite


def facings(scene, **kw):
    """Render all 4 facings (front +Y, +X, -Y, -X) -> list of 4 Sprites."""
    return [render(scene, facing=k, **kw) for k in range(4)]


def align(sprites):
    """Pad sprites to a common size with a common anchor. Returns (imgs, ax, ay)."""
    L = max(s.ax for s in sprites)
    T = max(s.ay for s in sprites)
    R = max(s.w - s.ax for s in sprites)
    B = max(s.h - s.ay for s in sprites)
    out = []
    for s in sprites:
        im = np.zeros((T + B, L + R, 4), np.uint8)
        im[T - s.ay:T - s.ay + s.h, L - s.ax:L - s.ax + s.w] = s.img
        out.append(im)
    return out, L, T


def strip(sprites, gap=0):
    """Horizontal strip of equally sized frames (aligned on the anchor). Returns (img, frame_w, ax, ay)."""
    imgs, ax, ay = align(sprites)
    fh, fw = imgs[0].shape[:2]
    out = np.zeros((fh, len(imgs) * (fw + gap) - gap, 4), np.uint8)
    for i, im in enumerate(imgs):
        out[:, i * (fw + gap):i * (fw + gap) + fw] = im
    return out, fw, ax, ay


def save_strip(path, sprites, gap=0):
    img, fw, ax, ay = strip(sprites, gap)
    _mk(path)
    write_png(path, img, {"FrameSize": "%d,%d" % (fw, img.shape[0]), "FrameAmount": str(len(sprites)),
                          "Anchor": "%d,%d" % (ax, ay)})
    return path


def save_sprite(path, sprite):
    _mk(path)
    return sprite.save(path)


def grid(imgs, cols, gap=2, bg=None):
    """Pack images of any size into a grid (cells sized to the largest)."""
    ch = max(i.shape[0] for i in imgs)
    cw = max(i.shape[1] for i in imgs)
    rows = (len(imgs) + cols - 1) // cols
    out = np.zeros((rows * (ch + gap) - gap, cols * (cw + gap) - gap, 4), np.uint8)
    if bg is not None:
        out[..., :3] = bg
        out[..., 3] = 255
    for k, im in enumerate(imgs):
        r, c = divmod(k, cols)
        y = r * (ch + gap) + (ch - im.shape[0])
        x = c * (cw + gap) + (cw - im.shape[1]) // 2
        a = im[..., 3:4] > 0
        out[y:y + im.shape[0], x:x + im.shape[1]] = np.where(a, im, out[y:y + im.shape[0], x:x + im.shape[1]])
    return out


def _mk(path):
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)


class Manifest:
    """Builds manifest.json for Figma: page / section / groups of items (file paths relative to root).

    m = Manifest(root, "World elements", "Nature")
    g = m.group("Trees: summer", columns=8, scale=2, note="...")
    g.add("trees/oak.png", "Oak")
    m.write()
    """

    def __init__(self, root, page, section):
        self.root, self.page, self.section = root, page, section
        self.groups = []

    def group(self, title, columns=8, scale=2, note=None):
        g = _Group(title, columns, scale, note)
        self.groups.append(g)
        return g

    def write(self):
        data = {"page": self.page, "section": self.section,
                "groups": [g.as_dict() for g in self.groups if g.items]}
        _mk(os.path.join(self.root, "manifest.json"))
        with open(os.path.join(self.root, "manifest.json"), "w") as f:
            json.dump(data, f, indent=1)
        missing = [it["file"] for g in self.groups for it in g.items
                   if not os.path.exists(os.path.join(self.root, it["file"]))]
        if missing:
            raise FileNotFoundError("manifest references missing files: %s" % missing[:5])
        return data


class _Group:
    def __init__(self, title, columns, scale, note):
        self.title, self.columns, self.scale, self.note = title, columns, scale, note
        self.items = []

    def add(self, file, label):
        self.items.append({"file": file, "label": label})
        return self

    def as_dict(self):
        d = {"title": self.title, "columns": self.columns, "scale": self.scale, "items": self.items}
        if self.note:
            d["note"] = self.note
        return d
