"""iso_life_people_walk - walk-cycle templates (adult 8x8, child 6x6, elder 8x8) + the Walk cycles / Variety groups."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_life_people_base import *  # noqa: F401,F403
from iso_life_people_base import blank, stamp, render, person_cols, FRONT, MIRROR, DIRS

# legs per frame: (hip row, foot row) at rows 6/7
ALEGS = [("..PPPP..", ".PK..KP."), ("...PP...", "...KK..."), ("..PPPP..", ".PK..KP."), ("...PP...", "...KK...")]
# arm tops (left col 2, right col 5) per frame
AARMS = [(3, 4), (3, 3), (4, 3), (3, 3)]


def adult_grid(face, f, hat=False, long=False, stoop=0, cane=False, brim=True, w=8):
    g = blank(w, 8)
    sx = stoop
    head = ["...CCC.." if hat else "...HHH..", "...HSS.." if face == "F" else "...HHH.."]
    if hat and face == "F" and brim:
        head[0] = "...CCCC."
    stamp(g, sx, 1, head)
    torso = ["...TT...", "...TT...", "...TT..."]
    stamp(g, 0, 3, torso)
    la, ra = AARMS[f]
    stamp(g, 2, la, ["A", "S"])
    stamp(g, 5, ra, ["A", "S"])
    if long:
        stamp(g, 2 + sx if face == "F" else 2, 3 if face == "F" else 2, ["H"] if face == "F" else ["H", "H"])
    hip, foot = ALEGS[f]
    stamp(g, 0, 6, [hip, foot])
    if cane:
        stamp(g, 6, 5, ["W", "W", "W"])
    return g


def child_grid(face, f, hat=False):
    g = blank(6, 6)
    head = ["..CCC." if hat else "..HHH.", "..HSS." if face == "F" else "..HHH."]
    if hat and face == "F":
        head[0] = "..CCCC"
    stamp(g, 0, 1, head)
    stamp(g, 0, 3, [(".TTTS.", ".TTTT.", ".STTT.", ".TTTT.")[f]])
    leg = [("..PP..", ".K..K."), ("..PP..", "..KK.."), ("..PP..", ".K..K."), ("..PP..", "..KK..")][f]
    stamp(g, 0, 4, list(leg))
    return g


def elder_grid(face, f, cane=True):
    g = adult_grid(face, f, stoop=0, w=8)
    # stoop: head+first torso row lean 1 px toward the facing direction
    for r in (1, 2):
        row = g[r]
        g[r] = ["."] + row[:-1]
        g[r] = row[:]
    g2 = blank(8, 8)
    for y in range(8):
        for x in range(8):
            c = g[y][x]
            if c == ".":
                continue
            nx = x + (1 if y in (1, 2) else 0)
            if 0 <= nx < 8:
                g2[y][nx] = c
    if cane:
        stamp(g2, 6, 5, ["W", "W", "W"])
    return g2


def frames(age, d, f, cols, **kw):
    face = FRONT[d]
    if age == "adult":
        g = adult_grid(face, f, hat=kw.get("hat", False), long=kw.get("long", False))
    elif age == "child":
        g = child_grid(face, f, hat=kw.get("hat", False))
    else:
        g = elder_grid(face, f, cane=kw.get("cane", True))
    return render(g, cols, flip=MIRROR[d], outline=kw.get("outline"))


def walk_set(age, cols, **kw):
    return {d: [frames(age, d, f, cols, **kw) for f in range(4)] for d in DIRS}


ELDER_COLS = dict(skin=0, hair=5, shirt=7, pants=3)
CANE = {"W": "6b4a2a"}


def walk_group(root):
    items = []
    specs = [("adult", person_cols(skin=1, hair=1, shirt=1, pants=0)),
             ("child", person_cols(skin=0, hair=2, shirt=0, pants=2)),
             ("elder", person_cols(skin=0, hair=5, shirt=6, pants=1, extra=CANE))]
    for age, cols in specs:
        ws = walk_set(age, cols)
        for d in DIRS:
            rel = "people/walk/%s_%s_strip.png" % (age, d)
            items.append(item(root, rel, strip(ws[d], 1), "%s %s 4f" % (age, d)))
        if age == "adult":
            for d in DIRS:
                for f in range(4):
                    rel = "people/walk/%s_%s_%d.png" % (age, d, f)
                    items.append(item(root, rel, ws[d][f], "adult %s f%d" % (d, f)))
    return {"title": "Walk cycles", "note": "adult 7 px, child 5 px, elder 7 px with cane; order N, E, S, W; 4 frames each",
            "columns": 4, "scale": 3, "items": items}


if __name__ == "__main__":
    for age, cols in [("adult", person_cols(1, 1, 1, 0)), ("child", person_cols(0, 2, 0, 2)),
                      ("elder", person_cols(0, 5, 6, 1, extra=CANE))]:
        ws = walk_set(age, cols)
        rows = [strip(ws[d], 1) for d in DIRS]
        from iso_life_common import canvas as cv
        H = sum(r.shape[0] + 1 for r in rows)
        big = cv(rows[0].shape[1], H)
        y = 0
        for r in rows:
            blit(big, r, 0, y)
            y += r.shape[0] + 1
        pv("walk_%s.png" % age, big, 10)
