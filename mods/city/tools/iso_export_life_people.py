"""iso_export_life_people - people, bikes, animals and benches for MOV (part of the `life` exporter area).

The design peeps are drawn for the four world directions N, E, S, W (sidewalks run along the cell axes). The exported
sheets use the same 8-facing contract as vehicles (`Facings: -8`, frame = facing * Length + anim); the four diagonal
facings reuse the neighbouring cardinal drawing: NE -> N, SE -> E, SW -> S, NW -> W.
Anchor = feet (bottom row, horizontal centre of the design canvas). Sheets: bits/world/iso/life/people/*.png.
Small art fix (decision 14): the cyclist is redrawn at true scale (bike 7 px long, rider 8 px tall; was 10 x 12).
"""
import numpy as np

import iso_export_lib as L
from iso_export_lib import Frame
import iso_life_people_base as PB
import iso_life_people_walk as W
import iso_life_people_var as VAR
import iso_life_people_act as ACT
import iso_life_people_act2 as ACT2
import iso_life_people_act3 as ACT3
import iso_life_people_animals as AN
import iso_life_people_bench as BE
from iso_life_people_base import person_cols, blank, stamp, render, FRONT, MIRROR
from iso_life_common import canvas, rgba

SUB = "life/people/"
CARD = ["N", "E", "S", "W"]
FACING8 = ["N", "N", "E", "E", "S", "S", "W", "W"]          # N NE E SE S SW W NW -> drawn direction
WALKERS = list(range(16))                                     # variety walkers (VAR.walker): 4 children, 2 elders...
BIKE_COLS = ACT.BIKE_COLS


def snap(img):
    """Same palette snapping as the design generator (iso_life.quantize_tree)."""
    import isokit as ik
    q = img.copy()
    m = q[..., 3] > 0
    if m.any():
        q[m, :3] = ik.nearest(img[m, :3])
    return q


def feet(img):
    """Frame anchored at the feet: bottom row, horizontal centre of the canvas."""
    img = snap(img)
    return Frame(img, img.shape[1] // 2, img.shape[0] - 1)


# ------------------------------------------------------------------------------------------ cyclist (fixed)
def _line(a, b):
    return ACT.line(a, b)


def small_cyc_grid(face, f, fc="F"):
    """Cyclist at true scale, facing screen-right (E = face view, N = back view). Canvas 8 x 10, wheels 3 x 3."""
    g = blank(8, 10)
    e = face == "F"
    ry, fy = (5, 7) if e else (7, 5)             # top rows of the rear / front wheel
    for wx, wy, off in ((0, ry, 0), (4, fy, 4)):
        for i, (dx, dy) in enumerate(ACT.RING):
            g[wy + dy][wx + dx] = "v" if i == (2 * f + off) % 8 else "W"
        g[wy + 1][wx + 1] = "w"
    rear, front = (1, ry + 1), (5, fy + 1)
    crank = (3, (ry + fy) // 2 + 1)
    seat = (2, ry - 1)
    bar = (5, fy - 2)
    for a, b in ((rear, crank), (crank, seat), (seat, rear), (front, bar), (crank, front)):
        for p in _line(a, b):
            if g[p[1]][p[0]] not in ("w",):
                g[p[1]][p[0]] = fc
    foot = [(4, crank[1] + 1), (3, crank[1] + 1), (2, crank[1] + 1), (3, crank[1] - 1)]
    for k, dark in ((1, True), (0, False)):
        fx, fy2 = foot[(f + 2 * k) % 4]
        if 0 <= fy2 < 10:
            g[fy2][fx] = "K"
    sy = seat[1]
    stamp(g, 2, sy - 4, ["HH", "HS" if e else "HH"])
    stamp(g, 2, sy - 2, ["TT", "TT"])
    stamp(g, 2, sy, ["PP"])
    g[bar[1]][bar[0]] = "B"
    g[bar[1] - 1][bar[0] - 1] = "S"
    g[bar[1] - 2][bar[0] - 2] = "A"
    return g


def cycling_small(d, f, i=0):
    c = ACT.cyc_cols(person_cols(0, 2, 0, 1), i)
    return render(small_cyc_grid(FRONT[d], f, "F"), c, flip=MIRROR[d])


# ------------------------------------------------------------------------------------------ sets
def walker_frames(i):
    return {d: [VAR.walker(i, d, f) for f in range(4)] for d in CARD}


def sets():
    """image -> list of (sequence, {dir: [img...]}, comment) in output order."""
    out = {}
    wc = person_cols(1, 1, 1, 0)
    # pedestrian: compatibility idle + variety walks
    out["pedestrian"] = [("walk%d" % i, walker_frames(i), None) for i in WALKERS]
    out["cyclist"] = [("idle", {d: [cycling_small(d, f, 1) for f in range(4)] for d in CARD}, "blue bike")] + \
        [("bike%d" % k, {d: [cycling_small(d, f, k) for f in range(4)] for d in CARD}, None) for k in (0, 2, 3)]
    out["jogger"] = [("idle", {d: [ACT2.jog(d, f, person_cols(2, 1, 7, 0)) for f in range(4)] for d in CARD}, None)]
    out["shopper"] = [("idle", {d: [ACT3.shopper(d, f, person_cols(3, 0, 1, 2)) for f in range(4)] for d in CARD}, None)]
    wk = ACT2.worker_cols()
    out["worker"] = [("walk", {d: [ACT2.worker_walk(d, f, wk) for f in range(4)] for d in CARD}, None),
                     ("hammer", {d: [ACT2.hammer(d, f, wk) for f in range(2)] for d in CARD}, "2 frames, loop")]
    pc = person_cols(1, 1, 3, 1)
    out["umbrella"] = [("idle" if k == 0 else "umbrella%d" % k,
                        {d: [ACT2.umbrella(d, f, pc, ACT2.UMB[k * 2]) for f in range(4)] for d in CARD}, None)
                       for k in range(4)]
    out["waiting"] = [("idle", {d: [ACT.waiting(d, f, person_cols(2, 3, 5, 1)) for f in range(3)] for d in CARD},
                       "3 frames: stand, sigh, check watch")]
    out["family"] = [("idle", {d: [ACT3.parent_child(d, f, person_cols(1, 3, 6, 0), person_cols(1, 3, 5, 2))
                                   for f in range(4)] for d in CARD}, "parent and child holding hands")]
    out["dogwalker"] = [("idle", {d: [ACT3.dog_walker(d, f, person_cols(0, 4, 7, 1)) for f in range(4)]
                                  for d in CARD}, None)]
    out["dog"] = [("idle" if c == "brown" else c, {d: [AN.dog(d, f, c) for f in range(4)] for d in CARD}, None)
                  for c in AN.COATS]
    return out


def birds():
    """image -> [(sequence, [frames], comment)] (no facings: right-facing and mirrored left-facing variants)."""
    out = {}
    pk = [AN.pigeon(f) for f in (0, 1)]
    pf = [AN.pigeon(f, "fly") for f in range(4)]
    out["pigeon"] = [("peck-r", pk, "2 frames, faces screen right"), ("peck-l", [AN.pigeon(f, flip=True) for f in (0, 1)], None),
                     ("fly-r", pf, "4 frames wing flap"), ("fly-l", [AN.pigeon(f, "fly", flip=True) for f in range(4)], None)]
    out["gull"] = [("fly-r", [AN.gull(f) for f in range(4)], "4 frames, faces screen right"),
                   ("fly-l", [AN.gull(f, flip=True) for f in range(4)], None)]
    return out


def benches():
    """image 'bench': static benches and seated peeps. Anchor = bench footprint centre."""
    res = []
    c = person_cols(1, 1, 1, 0)
    for a in ("x", "y"):
        u, v = BE.UX[a]
        ox = 8 - (u[0] * 4 + v[0] * 1.8) // 2
        ax = int(round(ox + u[0] * 2 + v[0] * 0.9))
        ay = int(round(8 + u[1] * 2 + v[1] * 0.9))
        res.append((a, [Frame(snap(BE.bench(a)), ax, ay)], "empty bench along the %s axis" % a.upper()))
        res.append(("sit-" + a, [Frame(snap(BE.bench_with(a, k, c)), ax, ay) for k in (0, 1)],
                    "one sitter, 2 frames (0 sits, 1 reads)"))
        res.append(("sit2-" + a, [Frame(snap(BE.bench_with(a, k, c, person_cols(3, 5, 5, 2))), ax, ay) for k in (0, 1)],
                    "two sitters, 2 frames"))
    return {"bench": res}


def export_people():
    """Writes the sheets, returns (image -> [(seq, rel file, start, length, comment, facings)], preview frames)."""
    info = {}
    prev = []

    for image, seqs in sets().items():
        rel = SUB + image + ".png"
        frames, starts = [], []
        for seq, per_dir, comment in seqs:
            n = len(per_dir["N"])
            starts.append((seq, len(frames), n, comment))
            for fc in FACING8:
                frames += [feet(im) for im in per_dir[fc]]
        L.write_sheet(rel, frames)
        for seq, st, n, comment in starts:
            info.setdefault(image, []).append((seq, "iso/" + rel, st, n, comment, 8))
        prev += frames[:32]
    for image, seqs in birds().items():
        rel = SUB + image + ".png"
        frames = []
        starts = []
        for seq, fr, comment in seqs:
            starts.append((seq, len(frames), len(fr), comment))
            frames += [feet(im) for im in fr]
        L.write_sheet(rel, frames)
        for seq, st, n, comment in starts:
            info.setdefault(image, []).append((seq, "iso/" + rel, st, n, comment, 1))
    for image, seqs in benches().items():
        rel = SUB + image + ".png"
        frames, starts = [], []
        for seq, fr, comment in seqs:
            starts.append((seq, len(frames), len(fr), comment))
            frames += fr
        L.write_sheet(rel, frames)
        for seq, st, n, comment in starts:
            info.setdefault(image, []).append((seq, "iso/" + rel, st, n, comment, 1))
    # legacy `pedestrian` idle: 8 frames = 4 colours x 2 steps, facing E (no facings), compat with PedestrianImage
    legacy = []
    for i in range(4):
        w = walker_frames(i)["E"]
        legacy += [feet(w[0]), feet(w[2])]
    L.write_sheet(SUB + "pedestrian-idle.png", legacy)
    info["pedestrian"].insert(0, ("idle", "iso/" + SUB + "pedestrian-idle.png", 0, 8,
                                  "legacy: 4 colours x 2 steps (frame = colour * 2 + step), facing E, no facings", 1))
    return info, prev
