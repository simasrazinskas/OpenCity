"""iso_life_people_act3 - shopper with bags (10x8), parent + child (12x10), person walking a dog (16x12)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, person_cols, item, strip, pv, FRONT, MIRROR, DIRS, canvas, blit, rgba
from iso_life_people_walk import adult_grid, child_grid, AARMS
from iso_life_people_animals import dog, COATS
from iso_life_people_act import line


def shopper(d, f, cols, bags=("e8a020", "d8383a")):
    g = adult_grid(FRONT[d], f)
    la, ra = AARMS[f]
    big = blank(10, 8)
    stamp(big, 1, 0, ["".join(r) for r in g])
    stamp(big, 7, ra + 2, ["GG", "GG"])        # bag in the (screen right) hand, before mirroring
    stamp(big, 7, ra + 2, ["gg"][:0] or ["G"])
    stamp(big, 1, la + 2, ["BB", "BB"])        # second bag at the other hand
    c = dict(cols)
    c.update({"G": bags[0], "g": bags[0], "B": bags[1]})
    return render(big, c, flip=MIRROR[d])


def side_by_side(a, b, d, off_b):
    """Compose two rendered sprites: a at (0,0) feet-aligned bottom of canvas, b at offset (off_b)."""
    w, h = 12, 10
    out = canvas(w, h)
    return out


def parent_child(d, f, pc, cc):
    adult = render(adult_grid(FRONT[d], f), pc, flip=MIRROR[d])
    kid = render(child_grid(FRONT[d], (f + 2) % 4), cc, flip=MIRROR[d])
    out = canvas(12, 10)
    ax, ay = (4, 0) if d in "EW" else (0, 0)
    kx, ky = (0, 4) if d in "EW" else (5, 4)
    if MIRROR[d] and d in "NS" or False:
        pass
    blit(out, adult, ax, ay)
    blit(out, kid, kx + 1, ky)
    # held hands: one skin pixel between the two bodies
    hx, hy = (4, 6) if d in "EW" else (5, 6)
    out[hy, hx] = rgba(SKIN[(pc.get("skin_i", 1)) % 5])
    return out


def dog_walker(d, f, cols, coat="brown"):
    """Walker with the dog close ahead on a short 1 px dark-grey leash (canvas 12x12)."""
    W, H = 12, 12
    out = canvas(W, H)
    person = render(adult_grid(FRONT[d], f), cols, flip=MIRROR[d])
    dg = dog(d, f, coat)
    up = d in "NW"                       # N and W walk up the screen: dog ahead and higher
    px, py = 0, 0
    dx, dy = (4, 1) if up else (4, 5)
    if d in "SW":
        px, dx = W - 8 - px, W - 8 - dx
    blit(out, person, px, py)
    blit(out, dg, dx, dy)
    la, ra = AARMS[f]
    hand = (px + (2 if MIRROR[d] else 5), py + ra + 1)
    neck = (dx + (2 if MIRROR[d] else 5), dy + 2)
    pts = line(hand, neck)[1:-1][:4]
    for x, y in pts:
        out[y, x] = rgba("4a4e58")
    return out


def act3_items(root):
    items = []
    sc = person_cols(3, 0, 1, 2)
    for d in DIRS:
        items.append(item(root, "people/shopper/shopper_%s_strip.png" % d, strip([shopper(d, f, sc) for f in range(4)], 1), "shopper %s 4f" % d))
    pc, cc = person_cols(1, 3, 6, 0), person_cols(1, 3, 5, 2)
    for d in DIRS:
        items.append(item(root, "people/family/parent_child_%s_strip.png" % d, strip([parent_child(d, f, pc, cc) for f in range(4)], 1), "parent+child %s 4f" % d))
    dc = person_cols(0, 4, 7, 1)
    for d in DIRS:
        items.append(item(root, "people/dogwalk/dogwalker_%s_strip.png" % d, strip([dog_walker(d, f, dc) for f in range(4)], 1), "dog walker %s 4f" % d))
    return items


if __name__ == "__main__":
    rows = [strip([shopper(d, f, person_cols(3, 0, 1, 2)) for f in range(4)], 1) for d in DIRS]
    rows += [strip([parent_child(d, f, person_cols(1, 3, 6, 0), person_cols(1, 3, 5, 2)) for f in range(4)], 1) for d in DIRS]
    rows += [strip([dog_walker(d, f, person_cols(0, 4, 7, 1)) for f in range(4)], 1) for d in DIRS]
    H = sum(r.shape[0] + 1 for r in rows)
    big = canvas(max(r.shape[1] for r in rows), H)
    y = 0
    for r in rows:
        blit(big, r, 0, y)
        y += r.shape[0] + 1
    pv("act3.png", big, 6)
