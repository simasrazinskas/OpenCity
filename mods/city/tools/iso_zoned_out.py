"""iso_zoned_out.py - writes every ZONED PNG + design/iso/zoned/manifest.json (called by iso_zoned.py).

Layout under design/iso/zoned/:
  <prefix>/<WxD>/L<l>-v<v>.png            day, default facing (front +Y), levels 1-5 x variants 0-3
  <prefix>/<WxD>/st-<state>.png           construction 1-3, abandoned, burnt, collapsed (L3, v0)
  <prefix>/<rep>/L<l>-v<v>-night.png      night render; -lit.png = emissive-only companion (engine)
  <prefix>/<rep>/L<l>-v<v>-winter.png     winter
  <prefix>/<rep>/L3-v<v>-f<k>.png         facings 0-3 (+ -facings.png strip)
  sig/<name>-<na|eu>[...].png, rubble/rubble-<WxD>.png, vignettes/*.png
"""
import os
import sys
import time

import isokit as ik
import iso_zoned_core as C
import iso_zoned_build as B

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "design", "iso", "zoned")
TH = ["NA a", "NA b", "EU a", "EU b"]
STATES = [("ok", 1, "c1", "Build 1: foundation"), ("ok", 2, "c2", "Build 2: frame+crane"),
          ("ok", 3, "c3", "Build 3: nearly done"), ("abandoned", 0, "abandoned", "Abandoned"),
          ("burnt", 0, "burnt", "Burnt"), ("collapsed", 0, "collapsed", "Collapsed")]
REP = {"res-low": "1x1", "res-row": "2x2", "res-med": "2x2", "res-high": "2x2", "res-lowrent": "2x2",
       "res-mixed": "2x2", "com-low": "1x1", "com-high": "2x2", "off": "2x2", "off-high": "2x2", "ind": "2x2",
       "warehouse": "3x3"}
FOOT = {}          # clipped pixel counts per prefix
COUNT = {}


def save(spr, rel, prefix):
    n = sum(ik.footprint_overhang(spr))
    if n:
        spr = ik.clip_footprint(spr)
        FOOT[prefix] = FOOT.get(prefix, 0) + n
    spr = ik.even(spr)
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    spr.save(path)
    COUNT[prefix] = COUNT.get(prefix, 0) + 1
    return rel, spr.w


def scale_for(widths):
    return 1 if max(widths or [0]) > 300 else 2


class Group:
    def __init__(self, man, title, columns, note=None):
        self.man, self.title, self.columns, self.note = man, title, columns, note
        self.items, self.widths = [], []

    def add(self, saved, label):
        rel, w = saved
        self.items.append((rel, label))
        self.widths.append(w)

    def flush(self):
        if not self.items:
            return
        g = self.man.group(self.title, columns=self.columns, scale=scale_for(self.widths), note=self.note)
        for rel, label in self.items:
            g.add(rel, label)


def night_and_lit(scene, rel, prefix, facing=0):
    d, lit = ik.day_and_lit(scene, facing)
    n = C.render(scene, facing=facing, night=True)[0]
    a = save(n, rel + "-night.png", prefix)
    lit = ik.Sprite(lit.img, lit.ax, lit.ay, lit.emit, lit.footprint)
    b = save(lit, rel + "-lit.png", prefix)
    return a, b


def zone(man, prefix):
    title, fps, fn = C.ZONES[prefix]
    t0 = time.time()
    c0 = C.CLIPPED[0]
    for fp in fps:
        g = Group(man, "%s %s: levels 1-5" % (title, fp), 5,
                  "rows: NA a, NA b, EU a, EU b; columns: level 1 to 5; front faces down-left (+Y)")
        for v in range(C.VARIANTS):
            for L in range(1, C.LEVELS + 1):
                spr = C.render(C.build(prefix, fp, L, v))[0]
                g.add(save(spr, "%s/%s/L%d-v%d.png" % (prefix, fp, L, v), prefix), "L%d %s" % (L, TH[v]))
        g.flush()
    g = Group(man, "%s: construction and decay (every footprint, level 3)" % title, 6,
              "per row one footprint: foundation, frame + crane, nearly done, abandoned, burnt, collapsed")
    for fp in fps:
        for st, stage, key, label in STATES:
            spr = C.render(C.build(prefix, fp, 3, 0, st, stage))[0]
            g.add(save(spr, "%s/%s/st-%s.png" % (prefix, fp, key), prefix), "%s %s" % (fp, label))
    g.flush()
    rep = REP.get(prefix, fps[0])
    g = Group(man, "%s %s: night, lit mask, winter" % (title, rep), 5,
              "rows: night NA, night EU, emissive -lit frames (engine), winter NA, winter EU")
    nights, lits = [], []
    for v in (0, 2):
        for L in range(1, 6):
            a, b = night_and_lit(C.build(prefix, rep, L, v), "%s/%s/L%d-v%d" % (prefix, rep, L, v), prefix)
            nights.append((a, "L%d %s night" % (L, TH[v][:2])))
            if v == 0:
                lits.append((b, "L%d lit mask" % L))
    for a, lab in nights + lits:
        g.add(a, lab)
    for v in (0, 2):
        for L in range(1, 6):
            spr = C.render(C.build(prefix, rep, L, v, season="winter"), season="winter")[0]
            g.add(save(spr, "%s/%s/L%d-v%d-winter.png" % (prefix, rep, L, v), prefix), "L%d %s winter" % (L, TH[v][:2]))
    g.flush()
    g = Group(man, "%s %s: 4 facings (level 3)" % (title, rep), 4,
              "front toward +Y (default), +X, -Y, -X; non-square lots only use the two facings that fit")
    for v in (0, 2):
        sc = C.build(prefix, rep, 3, v)
        fs = []
        for k in range(4):
            spr = C.render(sc, facing=k)[0]
            fs.append(spr)
            g.add(save(spr, "%s/%s/L3-v%d-f%d.png" % (prefix, rep, v, k), prefix),
                  "%s %s" % (TH[v][:2], ["+Y", "+X", "-Y", "-X"][k]))
        ik.save_strip(os.path.join(OUT, "%s/%s/L3-v%d-facings.png" % (prefix, rep, v)), fs)
    g.flush()
    print("%-12s %5d files %6.1fs clipped px %d" % (prefix, COUNT.get(prefix, 0), time.time() - t0,
                                                     C.CLIPPED[0] - c0), flush=True)


def signatures(man):
    import iso_zoned_sig as SG
    g = Group(man, "Signature buildings (3x3, level 5): NA and EU sets", 4)
    gs = Group(man, "Signature buildings: night, winter, construction, abandoned", 4)
    for k, (t, fn) in SG.SIGS.items():
        for v, th in ((0, "na"), (2, "eu")):
            sc = C.build(k, "3x3", 5, v)
            g.add(save(C.render(sc)[0], "sig/%s-%s.png" % (k, th), "sig"), "%s %s" % (t, th.upper()))
            if v == 0:
                a, b = night_and_lit(sc, "sig/%s-%s" % (k, th), "sig")
                gs.add(a, t + " night")
                gs.add(save(C.render(C.build(k, "3x3", 5, v, season="winter"), season="winter")[0], "sig/%s-%s-winter.png" % (k, th), "sig"), t + " winter")
                sp = C.render(C.build(k, "3x3", 5, v, "ok", 2))[0]
                gs.add(save(sp, "sig/%s-c2.png" % k, "sig"), t + " build")
                sp = C.render(C.build(k, "3x3", 5, v, "abandoned", 0))[0]
                gs.add(save(sp, "sig/%s-abandoned.png" % k, "sig"), t + " abandoned")
    g.flush()
    gs.flush()
    gf = Group(man, "Signature: 4 facings (Villa, Bazaar)", 4)
    for k in ("sig-villa", "sig-bazaar"):
        sc = C.build(k, "3x3", 5, 0)
        fs = [C.render(sc, facing=f)[0] for f in range(4)]
        for f, spr in enumerate(fs):
            gf.add(save(spr, "sig/%s-f%d.png" % (k, f), "sig"), "%s %s" % (k[4:], ["+Y", "+X", "-Y", "-X"][f]))
        ik.save_strip(os.path.join(OUT, "sig/%s-facings.png" % k), fs)
    gf.flush()


def rubble(man):
    g = Group(man, "Rubble (rubble-WxD actors, every footprint)", 6)
    for fp in "1x1 2x1 1x2 3x1 1x3 2x2 3x2 2x3 3x3 4x3 3x4".split():
        W, D = C.dims(fp)
        spr = C.render(B.rubble(W, D, C.seed_of("rubble", fp)))[0]
        g.add(save(spr, "rubble/rubble-%s.png" % fp, "rubble"), fp)
    g.flush()


def main(argv):
    only = None
    if "--only" in argv:
        only = argv[argv.index("--only") + 1].split(",")
    man = ik.Manifest(OUT, "World elements", "Zoned buildings")
    vg = Group(man, "Vignettes: suburban street, downtown block, industrial strip", 1,
               "built from the ZONED sprites on NET street tiles")
    if not only or "vignettes" in only:
        import iso_zoned_vignettes as V
        for name, label in V.VIGNETTES:
            img = V.build(name)
            rel = "vignettes/%s.png" % name
            os.makedirs(os.path.join(OUT, "vignettes"), exist_ok=True)
            ik.write_png(os.path.join(OUT, rel), img)
            vg.add((rel, img.shape[1]), label)
    vg.flush()
    for prefix in C.ZONES:
        if prefix.startswith("sig-") or (only and prefix not in only):
            continue
        zone(man, prefix)
    if not only or "sig" in only:
        signatures(man)
    if not only or "rubble" in only:
        rubble(man)
    if not only:
        man.write()
    print("total files:", sum(COUNT.values()), "pixels clipped at the footprint edge:", C.CLIPPED[0])
