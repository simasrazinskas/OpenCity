"""iso_life_people_act2 - jogging (8x8), umbrella in rain (12x12), construction worker walk + hammering (8x8)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, person_cols, item, strip, pv, FRONT, MIRROR, DIRS, canvas, blit
from iso_life_people_walk import adult_grid, AARMS

UMB = ["d8383a", "3b6fd9", "ecc92c", "34a84a", "8d4cbc", "ec8a2a", "2fb8b2", "efefef"]


def jog_grid(face, f):
    air = f in (1, 3)
    g = blank(8, 8)
    y0 = 0 if air else 1
    stamp(g, 0, y0, ["....HHH.", "....HSS." if face == "F" else "....HHH."])
    stamp(g, 0, y0 + 2, ["...TT...", "...TT...", "...TT..."])
    if f in (0, 3):
        stamp(g, 5, y0 + 2, ["SS"]); stamp(g, 1, y0 + 3, ["SS"])
    else:
        stamp(g, 5, y0 + 4, ["S"]); stamp(g, 5, y0 + 3, ["S"]); stamp(g, 2, y0 + 2, ["S"]); stamp(g, 1, y0 + 1, ["S"])
    if air:
        stamp(g, 0, 5, ["..PPP..."])
        stamp(g, 0, 6, ["..SK.SK."] if f == 1 else [".SK.SK.."])
    else:
        stamp(g, 0, 6, ["..PPPP.."])
        stamp(g, 0, 7, [".KS..SK."] if f == 0 else ["..K..SK."])
    return g


def jog(d, f, cols):
    return render(jog_grid(FRONT[d], f), cols, flip=MIRROR[d])


def umb_grid(face, f, hat=False):
    g = blank(12, 12)
    p = adult_grid(face, f, hat=hat)
    for r in (3, 4):
        p[r][5] = "."
    p[5][5] = "."
    rows = ["".join(r) for r in p]
    stamp(g, 2, 4, rows)
    stamp(g, 0, 1, ["......UUU...", ".....UUUUU..", "....UUUUUUU."])
    for y in (4, 5, 6):
        g[y][8] = "W"
    stamp(g, 7, 7, ["AS"])
    return g


def umbrella(d, f, cols, ucol):
    c = dict(cols)
    c.update({"U": ucol, "W": "4a3a2a"})
    return render(umb_grid(FRONT[d], f), c, flip=MIRROR[d])


def worker_cols(shirt=4, skin=1, hair=1):
    c = person_cols(skin, hair, shirt, 0, hat="f0c020")
    c.update({"V": "f06a18", "R": "e8ecb8", "M": "55575e", "W": "6b4a2a"})
    return c


def vest(g):
    for y, k in ((3, "V"), (4, "R"), (5, "V")):
        for x in range(8):
            if g[y][x] == "T":
                g[y][x] = k
    return g


def worker_walk(d, f, cols):
    g = vest(adult_grid(FRONT[d], f, hat=True))
    return render(g, cols, flip=MIRROR[d])


def hammer_grid(face, f):
    g = vest(adult_grid(face, 1, hat=True))
    for r in (3, 4):
        g[r][5] = "."
    if f == 0:     # hammer raised
        stamp(g, 5, 3, ["A"]); stamp(g, 6, 2, ["S"]); stamp(g, 6, 1, ["W"]); stamp(g, 6, 0, ["MM"])
    else:          # hammer down
        stamp(g, 5, 4, ["A"]); stamp(g, 6, 5, ["S"]); stamp(g, 7, 5, ["W"]); stamp(g, 6, 6, ["MM"])
    return g


def hammer(d, f, cols):
    return render(hammer_grid(FRONT[d], f), cols, flip=MIRROR[d])


def act2_items(root):
    items = []
    jc = person_cols(2, 1, 7, 0)
    for d in DIRS:
        items.append(item(root, "people/jog/jog_%s_strip.png" % d, strip([jog(d, f, jc) for f in range(4)], 1), "jogger %s 4f" % d))
    pc = person_cols(1, 1, 3, 1)
    for i in range(4):
        for d in DIRS:
            fr = [umbrella(d, f, pc, UMB[i * 2]) for f in range(4)]
            items.append(item(root, "people/rain/umbrella%d_%s_strip.png" % (i, d), strip(fr, 1), "umbrella %d %s 4f" % (i + 1, d)))
    wc = worker_cols()
    for d in DIRS:
        items.append(item(root, "people/worker/walk_%s_strip.png" % d, strip([worker_walk(d, f, wc) for f in range(4)], 1), "worker %s 4f" % d))
    for d in DIRS:
        items.append(item(root, "people/worker/hammer_%s_strip.png" % d, strip([hammer(d, f, wc) for f in range(2)], 1), "hammering %s 2f" % d))
    return items


if __name__ == "__main__":
    pc = person_cols(1, 1, 3, 1)
    rows = [strip([jog(d, f, person_cols(2, 1, 7, 0)) for f in range(4)], 1) for d in DIRS]
    rows += [strip([umbrella(d, f, pc, UMB[0]) for f in range(4)], 1) for d in DIRS]
    rows += [strip([worker_walk(d, f, worker_cols()) for f in range(4)], 1) for d in DIRS[:2]]
    rows += [strip([hammer(d, f, worker_cols()) for f in range(2)], 1) for d in DIRS[:2]]
    H = sum(r.shape[0] + 1 for r in rows)
    big = canvas(max(r.shape[1] for r in rows), H)
    y = 0
    for r in rows:
        blit(big, r, 0, y)
        y += r.shape[0] + 1
    pv("act2.png", big, 8)
