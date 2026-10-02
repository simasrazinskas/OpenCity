"""iso_life_people_var - crowd variety: 32 walkers (8 shirts, 4 trousers, 5 skins, 6 hair colours, hats)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import person_cols, item, canvas, blit, pv
from iso_life_people_walk import frames

HATS = ["c83a2e", "2f4f8f", "e8d8a0", "3c3c44", "e8a020"]


def walker(i, d="E", f=None):
    age = "child" if i % 9 == 4 else ("elder" if i % 11 == 7 else "adult")
    skin, hair = (i * 3 + i // 5) % 5, (i * 5 + i // 3) % 4
    if i % 13 == 5:
        hair = 5
    if age == "elder":
        hair = 4 + i % 2
    cols = person_cols(skin, hair, i % 8, (i // 8 + i * 3) % 4, hat=HATS[i % 5] if i % 6 == 2 else None,
                       extra={"W": "6b4a2a"})
    kw = dict(hat=(i % 6 == 2), long=(i % 4 == 1 and age == "adult"))
    return frames(age, d, (i % 4) if f is None else f, cols, **kw)


def variety_sheet(n=32, cols=8, d="E"):
    cw, ch, g = 10, 10, 1
    rows = (n + cols - 1) // cols
    img = canvas(cols * (cw + g), rows * (ch + g))
    for i in range(n):
        s = walker(i, d)
        h, w = s.shape[:2]
        blit(img, s, (i % cols) * (cw + g) + (cw - w) // 2, (i // cols) * (ch + g) + ch - h)
    return img


def variety_group(root):
    items = [item(root, "people/variety/crowd_E_32.png", variety_sheet(32, 8, "E"), "32 walkers E")]
    items.append(item(root, "people/variety/crowd_N_32.png", variety_sheet(32, 8, "N"), "32 walkers N (backs)"))
    for i, k in enumerate((0, 2, 6, 9, 13, 20)):
        items.append(item(root, "people/variety/single_%d.png" % i, walker(k, "E", 0), "single %d" % (i + 1)))
    return {"title": "Variety", "note": "8 shirt colours, 4 trouser colours, 5 skin tones, 6 hair colours, caps, long hair, children and elders mixed in",
            "columns": 4, "scale": 3, "items": items}


if __name__ == "__main__":
    pv("variety.png", variety_sheet(32, 8, "E"), 8)
    pv("variety_n.png", variety_sheet(32, 8, "N"), 8)
