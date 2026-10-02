#!/usr/bin/env python3
"""Contact sheet of a design manifest (approximates the Figma layout, for quick review).

usage: iso_kit_preview.py <manifest.json> <out.png> [group-substring] [max_groups]
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isokit as ik  # noqa: E402


def sheet(manifest, flt=None, max_groups=99, bg=(96, 112, 96)):
    root = os.path.dirname(manifest)
    data = json.load(open(manifest))
    blocks = []
    for g in data["groups"]:
        if flt and flt.lower() not in g["title"].lower():
            continue
        imgs = [ik.read_png(os.path.join(root, it["file"])) for it in g["items"]]
        k = g.get("scale", 2)
        imgs = [ik.upscale(i, k) for i in imgs]
        blocks.append(ik.grid(imgs, g.get("columns", 8), 6))
        if len(blocks) >= max_groups:
            break
    W = max(b.shape[1] for b in blocks)
    H = sum(b.shape[0] + 16 for b in blocks)
    out = np.zeros((H, W, 4), np.uint8)
    out[..., :3] = bg
    out[..., 3] = 255
    y = 0
    for b in blocks:
        ik.blit(out, b, 0, y)
        y += b.shape[0] + 8
        out[y:y + 2, :, :3] = (60, 70, 60)
        y += 8
    return out


if __name__ == "__main__":
    img = sheet(sys.argv[1], sys.argv[3] if len(sys.argv) > 3 else None,
                int(sys.argv[4]) if len(sys.argv) > 4 else 99)
    ik.write_png(sys.argv[2], img)
