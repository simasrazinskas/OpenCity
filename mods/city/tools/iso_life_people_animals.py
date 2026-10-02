"""iso_life_people_animals - dog (8x6), pigeon (8x6), seagull (12x8). Designed facing screen-right, mirrored for S/W."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, item, strip, pv, FRONT, MIRROR, DIRS

COATS = {"brown": ("b9773a", "6a3f1c"), "blackwhite": ("e8e8ec", "26262c"), "golden": ("dcae5a", "9a6a2a")}
DOG_LEGS = [(1, 4), (2, 3), (2, 4), (1, 3)]


def dog_grid(face, f):
    g = blank(8, 6)
    if f % 2 == 0:
        stamp(g, 1, 1, ["T"])
    else:
        stamp(g, 0, 2, ["T"])
    stamp(g, 0, 1, [".....EH.", ".DDDDDN."] if face == "F" else [".....EE.", ".DDDDDD."])
    stamp(g, 0, 3, [".DDDD..."])
    for x in DOG_LEGS[f]:
        stamp(g, x, 4, ["L"])
    return g


def dog_cols(coat="brown"):
    base, dk = COATS[coat]
    return {"D": base, "T": base, "H": base, "N": "2a2118", "E": dk, "L": dk, "l": dk}


def dog(d, f, coat="brown"):
    return render(dog_grid(FRONT[d], f), dog_cols(coat) | {}, flip=MIRROR[d])


PIGEON = {"B": "8f93a0", "W": "6a6e7c", "N": "6fa08a", "K": "f0a050", "E": "1c1816"}


def pigeon_grid(f, kind):
    g = blank(6, 4) if kind == "peck" else blank(8, 6)
    if kind == "peck":
        if f == 0:
            stamp(g, 0, 0, ["....NK", "WBBBN.", ".BBB..", ".K.K.."])
        else:
            stamp(g, 0, 0, ["......", "WBBB..", ".BBBNK", ".K.K.."])
    else:
        fr = [(0, [".WW.....", "..WW....", ".BBBBNK.", "..BB...."]),
              (1, [".WBBBNK.", "WW.BB..."]),
              (1, [".BBBBNK.", "..WWW...", "...WW..."]),
              (1, [".WBBBNK.", "WW.BB..."])][f]
        stamp(g, 0, fr[0], fr[1])
    return g


def pigeon(f, kind="peck", flip=False):
    return render(pigeon_grid(f, kind), PIGEON, flip=flip)


GULL = {"B": "f2f2f4", "W": "bfc4cc", "K": "26262c", "O": "f0a020", "N": "dfe3e8"}


def gull_grid(f):
    g = blank(12, 8)
    stamp(g, 3, 3, ["BBBBN", ".BBBBO"])
    shapes = [
        [(2, 0, ["K.....K"]), (2, 1, [".W...W."]), (2, 2, ["..WWW.."])],
        [(1, 2, ["KWW...WWK"])],
        [(2, 4, ["..WWW.."]), (2, 5, [".W...W."]), (2, 6, ["K.....K"])],
        [(1, 2, ["KWW...WWK"])],
    ]
    for x, y, r in shapes[f]:
        stamp(g, x, y, r)
    return g


def gull(f, flip=False):
    return render(gull_grid(f), GULL, flip=flip)


def animal_items(root):
    items = []
    for coat in COATS:
        for d in DIRS:
            fr = [dog(d, f, coat) for f in range(4)]
            items.append(item(root, "people/animals/dog_%s_%s_strip.png" % (coat, d), strip(fr, 1), "dog %s %s 4f" % (coat, d)))
    items.append(item(root, "people/animals/pigeon_peck_strip.png", strip([pigeon(f) for f in (0, 1)], 1), "pigeon peck 2f"))
    items.append(item(root, "people/animals/pigeon_fly_strip.png", strip([pigeon(f, "fly") for f in range(4)], 1), "pigeon fly 4f"))
    items.append(item(root, "people/animals/gull_fly_strip.png", strip([gull(f) for f in range(4)], 1), "seagull fly 4f"))
    return items


if __name__ == "__main__":
    from iso_life_common import canvas, blit
    rows = [strip([dog(d, f, c) for f in range(4)], 1) for c in COATS for d in DIRS[:2]]
    rows += [strip([pigeon(f) for f in (0, 1)] + [pigeon(f, "fly") for f in range(4)], 1), strip([gull(f) for f in range(4)], 1)]
    H = sum(r.shape[0] + 1 for r in rows)
    big = canvas(max(r.shape[1] for r in rows), H)
    y = 0
    for r in rows:
        blit(big, r, 0, y)
        y += r.shape[0] + 1
    pv("animals.png", big, 10)
