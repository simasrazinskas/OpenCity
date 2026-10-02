#!/usr/bin/env python3
"""CIVIC art generator: every service, utility, industry hub, transit building, park and highway entry,
in the RCT2/TTD-style isometric look (design phase).

    python3 mods/city/tools/iso_civic.py                 # render everything + manifest.json
    python3 mods/city/tools/iso_civic.py hospital clinic --preview /tmp/p.png [--k 2]   # subset + preview

Writes design/iso/civic/<category>/<actor>-<state>.png (+ animation / facing strips) and
design/iso/civic/manifest.json. Models live in iso_civic_m_*.py; shared helpers in iso_civic_kit.py,
iso_civic_props.py and iso_civic_states.py.
"""
import os
import zlib
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso_civic_states import REG, St, OUT, dim, char_img, construction, height_of, rubble, replace  # noqa: E402
from iso_civic_kit import ik  # noqa: E402
import iso_civic_models  # noqa: E402,F401  (registers every model)

CATS = [("power", "Power"), ("water", "Water"), ("police", "Police"), ("fire", "Fire"), ("health", "Health"),
        ("education", "Education"), ("parks", "Parks & plazas"), ("garbage", "Garbage"),
        ("deathcare", "Deathcare"), ("comms", "Communications & post"), ("admin", "Administration"),
        ("industry", "Industry hubs"), ("areas", "Extractor areas"), ("transit", "Transit"),
        ("roads", "Outside connections"), ("misc", "Shared: rubble")]
FACING = ["+Y", "+X", "-Y", "-X"]


def _save(spr, rel):
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ik.even(spr).save(path)
    return rel


def _save_img(img, rel, like):
    spr = ik.Sprite(img, like.ax, like.ay, None, like.footprint)
    return _save(spr, rel)


def render_model(sp):
    """Render all states of one model. Returns (items, strips): items = [(file, label, img)]."""
    items, strips = [], []
    base = "%s/%s" % (sp.cat, sp.name)

    def R(st, **kw):
        return ik.render(sp.fn(st), frame=st.frame * 2, season=st.season, night=st.night, **kw)

    day = R(St())
    items.append((_save(day, base + "-day.png"), "day", day.img))
    if sp.variants:
        for i, (label, ov) in enumerate(sp.variants):
            v = R(replace(St(), **ov))
            items.append((_save(v, "%s-v%d.png" % (base, i + 1)), label, v.img))
    nt = ik.render(sp.fn(St(night=True)), night=True)
    items.append((_save(nt, base + "-night.png"), "night", nt.img))
    _, lit = ik.day_and_lit(sp.fn(St()))
    items.append((_save(lit, base + "-lit.png"), "lit layer", lit.img))
    wi = ik.render(sp.fn(St(season="winter")), season="winter")
    items.append((_save(wi, base + "-winter.png"), "winter", wi.img))
    if sp.power:
        ina = ik.render(sp.fn(St(inactive=True)))
        img = dim(ina.img)
        items.append((_save_img(img, base + "-inactive.png", ina), "no power", img))
    if sp.anim:
        frames = [R(St(frame=k)) for k in range(sp.anim)]
        for k, f in enumerate(frames):
            items.append((_save(f, "%s-anim%d.png" % (base, k + 1)), "anim %d/%d" % (k + 1, sp.anim), f.img))
        ik.save_strip(os.path.join(OUT, base + "-anim-strip.png"), frames)
        strips.append((base + "-anim-strip.png", "%s: %d frames" % (sp.title, sp.anim)))
    if sp.fill:
        for k, f in enumerate((0.0, 0.25, 0.5, 0.75, 1.0)):
            spr = R(St(fill=f))
            items.append((_save(spr, "%s-fill%d.png" % (base, int(f * 100))), "fill %d%%" % int(f * 100), spr.img))
    if sp.ups:
        for uid, label in sp.ups:
            spr = R(St(ups=frozenset([uid])))
            items.append((_save(spr, "%s-up-%s.png" % (base, uid)), "+ " + label, spr.img))
        if len(sp.ups) > 1:
            spr = R(St(ups=frozenset(u for u, _ in sp.ups)))
            items.append((_save(spr, base + "-up-all.png"), "all upgrades", spr.img))
    if sp.front:
        fs = ik.facings(sp.fn(St()))
        for k in range(1, 4):
            items.append((_save(fs[k], "%s-facing%d.png" % (base, k)), "front " + FACING[k], fs[k].img))
        ik.save_strip(os.path.join(OUT, base + "-facings-strip.png"), fs)
        strips.append((base + "-facings-strip.png", "%s: 4 facings" % sp.title))
    if sp.build:
        h = height_of(day)
        for stg in (1, 2):
            spr = ik.render(construction(sp.fp, h, stg, seed=zlib.crc32(sp.name.encode()) % 997))
            items.append((_save(spr, "%s-build%d.png" % (base, stg)), "construction %d" % stg, spr.img))
        b = ik.render(sp.fn(St(inactive=True)))
        img = char_img(b.img, zlib.crc32(sp.name.encode()) % 997)
        items.append((_save_img(img, base + "-burnt.png", b), "burnt", img))
    return items, strips


def check_extent(sp, day):
    """Engine rule: nothing past the footprint diamond's left/right corners."""
    fx, fy = sp.fp
    left, right = day.ax - (fx + fy) * 16, day.ax + (fx + fy) * 16
    cols = day.img[..., 3].any(0).nonzero()[0]
    if len(cols) and (cols[0] < left - 1 or cols[-1] > right):
        print("  WARN %s pokes out of its footprint extent (%d..%d vs %d..%d)" % (sp.name, cols[0], cols[-1],
                                                                                 left, right))


def main(argv):
    names = [a for a in argv if not a.startswith("--")]
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    k = int(argv[argv.index("--k") + 1]) if "--k" in argv else 2
    if preview:
        names = [n for n in names if n != preview and n != str(k)]
    todo = [REG[n] for n in names] if names else sorted(REG.values(), key=lambda s: s.order)
    allitems, allstrips = {}, []
    for sp in todo:
        items, strips = render_model(sp)
        check_extent(sp, ik.render(sp.fn(St())))
        allitems[sp.name] = items
        allstrips += strips
        print("%-20s %d states" % (sp.name, len(items)))
    if preview:
        imgs = [im for sp in todo for (_, _, im) in allitems[sp.name]]
        ik.inspect(preview, ik.grid(imgs, min(8, len(imgs))), k=k)
    if not names:
        write_manifest(allitems, allstrips)


def write_manifest(allitems, allstrips):
    rub = [rubble(v) for v in range(4)]
    for v, s in enumerate(rub):
        _save(ik.render(s), "misc/rubble-%d.png" % (v + 1))
        _save(ik.render(s, season="winter"), "misc/rubble-%d-winter.png" % (v + 1))
    m = ik.Manifest(OUT, "World elements", "Services, utilities, industry & transit")
    for cat, ctitle in CATS:
        specs = sorted([s for s in REG.values() if s.cat == cat], key=lambda s: s.order)
        for sp in specs:
            its = allitems[sp.name]
            wide = max(im.shape[1] for _, _, im in its) > 300
            g = m.group("%s: %s (%dx%d)" % (ctitle, sp.title, sp.fp[0], sp.fp[1]), columns=len(its),
                        scale=1 if wide else 2, note=sp.note)
            for f, label, _ in its:
                g.add(f, label)
        if cat == "misc":
            g = m.group("Shared: rubble (1x1, one per footprint cell)", columns=8, scale=2)
            for v in range(4):
                g.add("misc/rubble-%d.png" % (v + 1), "rubble %d" % (v + 1))
                g.add("misc/rubble-%d-winter.png" % (v + 1), "winter")
    g = m.group("Animation and facing strips (engine-ready, anchor-aligned frames)", columns=4, scale=1)
    for f, label in allstrips:
        g.add(f, label)
    m.write()
    print("manifest: %d groups" % len(m.groups))


if __name__ == "__main__":
    main(sys.argv[1:])
