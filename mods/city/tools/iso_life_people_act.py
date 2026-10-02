"""iso_life_people_act - bench sitters, waiting at a stop, cycling (canvas 10x12)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, person_cols, item, strip, pv, FRONT, MIRROR, DIRS
from iso_life_people_walk import adult_grid
from iso_life_people_bench import bench, bench_with

BIKE_COLS = ["e0302e", "2f6fe0", "2fb04a", "f0a020"]


def wait_grid(face, f):
    g = adult_grid(face, 1)
    if f == 1:      # sigh: head dips 1 px
        for x in range(8):
            g[3][x] = g[2][x] if g[2][x] != "." else g[3][x]
            g[2][x] = g[1][x]
            g[1][x] = "."
    if f == 2:      # looks at wrist watch: right arm raised to chest
        for r in (3, 4):
            g[r][5] = "."
        stamp(g, 5, 3, ["A", "S"])
        stamp(g, 5, 2, ["S"]) if False else None
        stamp(g, 4, 3, ["S"])
    return g


def waiting(d, f, cols):
    return render(wait_grid(FRONT[d], f), cols, flip=MIRROR[d])


def line(a, b):
    (x0, y0), (x1, y1) = a, b
    n = max(abs(x1 - x0), abs(y1 - y0), 1)
    return [(round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n)) for i in range(n + 1)]


RING = [(0, 0), (1, 0), (2, 0), (2, 1), (2, 2), (1, 2), (0, 2), (0, 1)]
PEDAL = [(6, 8), (4, 9), (2, 8), (4, 7)]       # foot positions around the crank (4, 8)


def cyc_grid(face, f, fc="F"):
    """Cyclist facing screen-right (E = face view, N = back view). Wheels are 3x3 rings on the iso diagonal."""
    g = blank(10, 12)
    e = face == "F"
    ry, fy = (6, 9) if e else (9, 6)            # top rows of rear / front wheel
    for wx, wy, off in ((0, ry, 0), (6, fy, 4)):
        for i, (dx, dy) in enumerate(RING):
            g[wy + dy][wx + dx] = "v" if i == (2 * f + off) % 8 else "W"
        g[wy + 1][wx + 1] = "w"                 # lighter hub
    rh, fh = (1, ry + 1), (7, fy + 1)
    bar_y = fy - 2
    for a, b in ((rh, fh), (fh, (7, bar_y + 1)), ((3, 6), (4, 7 if e else 8))):
        for p in line(a, b):
            if g[p[1]][p[0]] != "w":
                g[p[1]][p[0]] = fc
    for k, dark in ((1, True), (0, False)):
        fx, fy2 = PEDAL[(f + 2 * k) % 4]
        for p in line((4, 5), (fx, fy2))[:-1]:
            g[p[1]][p[0]] = "p" if dark else "P"
        g[fy2][fx] = "K"
    stamp(g, 0, 0, ["...HHH....", "...HSS...." if e else "...HHH...."])
    stamp(g, 0, 2, ["...TTT....", "...TTT....", "...TTT....", "...PPP...."])
    g[bar_y][7] = "B"
    g[bar_y - 1][6] = "S"
    g[bar_y - 2][6] = "A"
    return g


def cyc_cols(cols, i=0):
    c = dict(cols)
    c.update({"F": BIKE_COLS[i % 4], "W": "2a2c34", "v": "5a5f6c", "w": "b4bac6", "B": "2a2c34", "K": SHOE,
              "P": "d6c796", "p": "9c8c5c"})
    return c


def cycling(d, f, cols, i=0):
    c = cyc_cols(cols, i)
    return render(cyc_grid(FRONT[d], f, "F"), c, flip=MIRROR[d])


def act_group(root):
    items = []
    c = person_cols(1, 1, 1, 0)
    for a, nm in (("x", "X axis"), ("y", "Y axis")):
        items.append(item(root, "people/bench/bench_%s.png" % a, bench(a), "bench along %s" % nm.split()[0]))
        fr = [bench_with(a, k, c) for k in (0, 1)]
        for k in (0, 1):
            items.append(item(root, "people/bench/sit_%s_%d.png" % (a, k), fr[k], "sitter %s f%d" % (a, k)))
        items.append(item(root, "people/bench/sit_%s_strip.png" % a, strip(fr, 1), "sitter %s 2f" % a))
        fr2 = [bench_with(a, k, c, person_cols(3, 5, 5, 2)) for k in (0, 1)]
        items.append(item(root, "people/bench/sit2_%s_strip.png" % a, strip(fr2, 1), "two sitters %s 2f" % a))
    wc = person_cols(2, 3, 5, 1)
    for d in DIRS:
        fr = [waiting(d, f, wc) for f in range(3)]
        items.append(item(root, "people/wait/wait_%s_strip.png" % d, strip(fr, 1), "waiting %s 3f" % d))
    for d in DIRS:
        fr = [cycling(d, f, person_cols(0, 2, 0, 1), 1) for f in range(4)]
        items.append(item(root, "people/cycle/cycle_%s_strip.png" % d, strip(fr, 1), "cyclist %s 4f" % d))
    return items


if __name__ == "__main__":
    from iso_life_common import canvas, blit
    cc = person_cols(0, 2, 4, 1)
    rows = [strip([cycling(d, f, cc, 0) for f in range(4)], 1) for d in DIRS]
    rows += [strip([waiting(d, f, person_cols(2, 3, 5, 1)) for f in range(3)], 1) for d in DIRS]
    H = sum(r.shape[0] + 1 for r in rows)
    big = canvas(max(r.shape[1] for r in rows), H)
    y = 0
    for r in rows:
        blit(big, r, 0, y)
        y += r.shape[0] + 1
    pv("act1.png", big, 8)
