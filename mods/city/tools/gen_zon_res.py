"""gen_zon_res.py - low density residential lots: res-low (houses) and res-row (terraces)."""
import genworld as G
from genworld import H
from pngkit import shade, mix
from gen_zon_base import (layers, finish, shadow, lawn, paving, hedge, pool, trees, car,
                          roof_gear)

GRASS = [H("78BE55"), H("6FB850"), H("82C25C"), H("74B954")]
ROOFS = G.ROOFS_RES
WALLS = G.WALLS_RES
VILLA_ROOF = [H("9A9187"), H("6E7F96"), H("B0634A"), H("5E6B5E")]
VILLA_WALL = [H("F7F3EA"), H("F4E6C8"), H("FFFFFF"), H("E9D9C4")]


def hipped(b, x, y, w, d, col):
    """Hipped roof seen from above: four shaded faces."""
    for yy in range(d):
        for xx in range(w):
            m = min(yy, d - 1 - yy, xx, w - 1 - xx)
            if m == yy:
                f = 1.16
            elif m == d - 1 - yy:
                f = 0.8
            elif m == xx:
                f = 1.06
            else:
                f = 0.88
            b.set(x + xx, y + yy, shade(col, f))
    b.rect(x, y + d - 1, w, 1, shade(col, 0.6))


def house(b, g, L, x, yb, w, d, h, roof, wall, seed, kind="gable", ridge_v=False, lit=0.4):
    shadow(g, L, x, yb, w, d, h + 3)
    rx, ry, rw, rd = G.box3d(b, x, yb, w, d, h, roof, wall)
    if kind == "gable":
        G.pitched(b, rx, ry - 3, rw, rd + 3, roof, ridge_vertical=ridge_v)
    elif kind == "hip":
        hipped(b, rx, ry - 3, rw, rd + 3, roof)
    else:  # flat with parapet
        b.rect(rx, ry, rw, 1, shade(roof, 1.3))
        b.rect(rx, ry + rd - 1, rw, 1, shade(roof, 0.7))
    floors = 2 if h >= 9 else 1
    fh = h // floors
    for f in range(floors):
        wy = yb - h + f * fh + (fh - 3) // 2
        G.windows(b, x + 2, wy, w - 4, 3, max(2, (w - 4) // 5), 1, lit=lit, seed=seed + f, ww=2, wh=2)
    return (rx, ry - 3, rw, rd + 3)


def res_low(L):
    g, b = layers(L.W, L.H)
    r = L.rng("rl")
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    roof = ROOFS[v] if T < 3 else VILLA_ROOF[v]
    wall = WALLS[(v + T) % 4] if T < 3 else VILLA_WALL[v]
    if L.up:
        wall = shade(wall, 1.05)
    lit = 0.35 + 0.25 * L.up
    lawn(g, 0, y0, fw, fd, GRASS[v], seed=L.seed("lawn"))
    g.rect(0, y0, fw, 1, shade(GRASS[v], 0.8))
    if T < 3:
        G.fence(g, 1, y0 + 1, fw - 2, fd - 2, sides="tblr")
    else:
        hedge(g, 1, y0 + 1, fw - 2)
        g.rect(1, y0 + 1, 2, fd - 3, H("3E8E45"))
        g.rect(fw - 3, y0 + 1, 2, fd - 3, H("2F7A3A"))
    m = 8 if L.cy == 1 else 12 + 4 * (L.cy - 2)
    yb = yB - m
    w = {1: 13 + 12 * (L.cx - 1), 2: 17 + 15 * (L.cx - 1), 3: 21 + 20 * (L.cx - 1)}[T]
    d = {1: 10, 2: 11, 3: 13}[T] + 5 * (L.cy - 1)
    h = {1: 6, 2: 9, 3: 11}[T]
    if L.cx >= 2:
        w = min(w, fw - 16)
    w = min(w, fw - 6)
    side = 1 if v % 2 else -1  # garage / driveway side
    if L.cx == 1:
        x = (fw - w) // 2 + side * (1 if T < 3 else 0)
    else:
        slack = fw - 12 - w
        x = [3, slack + 2, slack // 2 + 3, 5][v] if side > 0 else [fw - 4 - w, 14, slack // 2 + 11, fw - 6 - w][v]
        x = max(3, min(x, fw - 13 - w)) if side > 0 else max(13, min(x, fw - 3 - w))
    # back garden (deep lots): props in slots between the fence and the roof line
    if L.cy >= 2 or L.cx >= 2:
        top = y0 + 3
        bot = (yb - d - h - 3) if L.cy >= 2 else yb - 2
        garden_props(g, b, L, 3, top, fw - 3, bot, x, x + w, yb - d)
    # driveway + path
    pth = H("D8D2C2")
    dx = x + w // 2 - 2 + (v % 3) - 1
    g.rect(dx, yb, 4, yB - yb, pth)
    if L.cx >= 2:
        gx = x + w + 1 if side > 0 else x - 10
        g.rect(gx + 1, yb, 8, yB - yb, H("BDB7A8"))
    # house
    kind = "gable" if T == 1 else ("gable" if v % 2 == 0 else "hip") if T == 2 else ("hip" if v % 2 else "flat")
    rr = house(b, g, L, x, yb, w, d, h, roof, wall, L.seed("h"), kind, ridge_v=(T == 1 and v % 2 == 1), lit=lit)
    G.door(b, x + w // 2 - 1 + (v % 3) - 1, yb - 4, 3, 4)
    if T >= 2 and kind != "flat":
        G.chimney(b, x + w - 6 - 3 * (v % 2), rr[1] + 4, 4)
    if T == 3:
        # front wing of the villa (L-shape), terrace roof
        ww = max(9, w // 3)
        wx = x if v % 2 == 0 else x + w - ww
        wrr = house(b, g, L, wx, yb + 4, ww, 8, 7, shade(roof, 1.05), wall, L.seed("wing"), "flat", lit=lit)
        b.rect(wrr[0] + 2, wrr[1] + 5, wrr[2] - 4, 3, H("C9A27A"))  # timber deck on terrace
        b.rect(wx + 2, yb, ww - 4, 1, H("6FB3D8"))  # glazing strip
        if kind == "flat":
            roof_gear(b, (rr[0], rr[1] + 3, rr[2], rr[3] - 3), L, 0.5, solar=L.up, sky=True)
    # garage annex on wide lots
    if L.cx >= 2:
        gx = x + w if side > 0 else x - 10
        gw = 10
        shadow(g, L, gx, yb, gw, 9, 5)
        grr = G.box3d(b, gx, yb, gw, 9, 5, shade(roof, 0.92), shade(wall, 0.94))
        b.rect(gx + 2, yb - 4, gw - 4, 3, shade(wall, 0.62))
        for yy in range(yb - 4, yb - 1, 1):
            b.hline(gx + 2, gx + gw - 3, yy, (0, 0, 0, 30 if yy % 2 else 0))
        G.pitched(b, grr[0], grr[1] - 1, grr[2], grr[3] + 1, shade(roof, 0.95), ridge_vertical=True)
        if T >= 2 or L.up:
            car(g, gx + 3, yb + 2, G.CARCOLS[(v + L.level) % 6], horizontal=False)
    elif L.up or T >= 2:
        car(g, 3 if side < 0 else fw - 7, yb + 1, G.CARCOLS[(v + L.level) % 6], horizontal=False)
    # upgrade: solar, flowers, extra greenery
    if L.up:
        if kind != "flat":
            G.solar_panel(b, rr[0] + 2, rr[1] + rr[3] // 2 + 1, min(rr[2] - 4, 10), 3)
        G.flowerbed(g, x + 1, yb + 1, min(6, w // 2), seed=v)
        G.flowerbed(g, x + w - 7, yb + 1, 6, seed=v + 3)
    # front garden trees / bushes
    pts = [(fw - 5 if side < 0 else 5, yB - 7, 3)]
    if L.cx >= 2 or T >= 2:
        pts.append((5 if side < 0 else fw - 5, y0 + 6, 4))
    if L.up and L.cx >= 2:
        pts.append((fw // 2 + 6 * side, yB - 6, 3))
    trees(g, pts, L)
    G.bush(g, x - 2 if x > 3 else x + w + 2, yb - 2)
    if T == 3:
        for xx in range(4, fw - 4, 6):
            G.bush(g, xx, yB - 3, H("3E8E45"))
    return finish(b, g)


def garden_props(g, b, L, x0, y0, x1, y1, hx0, hx1, hy):
    """Fill a garden strip with tier-appropriate props (slots ~14px wide)."""
    if y1 - y0 < 6:
        return
    r = L.rng("garden")
    T = L.tier
    if T == 3:
        pw = min(26, x1 - x0 - 10)
        px = x0 + (x1 - x0 - pw) // 2 + r.randrange(-3, 4)
        ph = min(8, y1 - y0 - 3)
        if ph >= 4:
            pool(g, px, y0 + 2, pw, ph)
            g.rect(px - 3, y0 + 2 + ph + 1, pw + 6, 2, H("D9CDB4"))  # pool deck
            for k in range(2):
                g.rect(px + 2 + k * 5, y0 + ph + 4, 3, 1, H("F4F4F4"))
            x0, x1 = x0, px - 4
    kinds = {1: ["veg", "shed", "tree", "line"], 2: ["tramp", "shed", "tree", "sand", "veg"], 3: ["tree", "bush", "tree"]}[T]
    xx = x0
    used = set()
    while xx + 9 <= x1:
        k = r.choice(kinds)
        if k in used and k != "tree":
            k = "tree" if r.random() < 0.6 else "bush"
        used.add(k)
        cy = y0 + 2 + r.randrange(max(1, y1 - y0 - 8))
        if k == "veg":
            g.rect(xx, cy, 9, 6, H("8B6A45"))
            for yy in range(cy + 1, cy + 6, 2):
                g.hline(xx + 1, xx + 7, yy, H("5FA347"))
        elif k == "shed":
            shadow(g, L, xx, cy + 6, 7, 4, 3)
            G.box3d(b, xx, cy + 6, 7, 4, 3, H("9C7A55"), H("B89A72"))
        elif k == "tree":
            G.tree(g, xx + 4, cy + 3, 3 + r.randrange(2), r.choice([H("3F9B4A"), H("52A857"), H("2F8A44")]))
        elif k == "bush":
            G.bush(g, xx + 3, cy + 3)
            G.bush(g, xx + 7, cy + 4, H("5DB062"))
        elif k == "line":
            g.vline(xx, cy, cy + 3, H("8A8A8A"))
            g.vline(xx + 8, cy, cy + 3, H("8A8A8A"))
            g.hline(xx, xx + 8, cy, H("DADADA"))
            for i in range(1, 8, 2):
                g.set(xx + i, cy + 1, [H("F26B6B"), H("FFFFFF"), H("6FA8DC")][i % 3])
        elif k == "tramp":
            g.circle(xx + 4, cy + 3, 3.5, H("2C3440"))
            g.circle(xx + 4, cy + 3, 2.5, H("3A4656"))
        elif k == "sand":
            g.rect(xx, cy, 6, 5, H("E8D8A0"))
            g.rect_outline(xx, cy, 6, 5, H("B08A5A"))
        xx += 13 + r.randrange(8)


# =========================================================================== res-row
BRICKS = [[H("B5654A"), H("9E5D4C"), H("C4775A"), H("A85A44")],
          [H("D9B48F"), H("C9A57A"), H("E2CDA8"), H("D1AE84")],
          [H("F2D0DC"), H("CFE6F2"), H("F7EBC0"), H("D6EBD0")],
          [H("E6E3DC"), H("CFCBC4"), H("BFC4C9"), H("DAD4C6")]]
ROW_ROOFS = [H("5F6670"), H("8B4A3C"), H("4E5A6E"), H("6B5B4E")]


def res_row(L):
    g, b = layers(L.W, L.H)
    v, T, fw, fd, y0, yB = L.v, L.tier, L.fw, L.fd, L.y0, L.yB
    r = L.rng("row")
    lit = 0.35 + 0.25 * L.up
    lawn(g, 0, y0, fw, fd, GRASS[(v + 1) % 4], seed=L.seed("lawn"))
    g.rect(0, y0, fw, 1, shade(GRASS[v], 0.8))
    uw_t = {1: 11, 2: 13, 3: 16}[T]
    n = max(2, round(fw / uw_t))
    xs = [round(i * fw / n) for i in range(n + 1)]
    h = {1: 11, 2: 15, 3: 20}[T]
    m = {1: 6, 2: 7, 3: 8}[T] + (fd - 32) * 3 // 10
    yb = yB - m
    d = 12 + (2 if T == 3 else 0) + 2 * (L.cy - 1)
    sw = H("CFCBC2")
    g.rect(0, yB - 3, fw, 3, sw)  # pavement along the street
    g.rect(0, yB - 3, fw, 1, shade(sw, 1.08))
    # back gardens: fences between units, sheds, trees
    if L.cy >= 2:
        vis = yb - d - h - 4  # lowest visible garden row (roof hides the rest)
        for i in range(n):
            g.vline(xs[i], y0, yb - d, H("C9B58F"))
            ux, uw = xs[i], xs[i + 1] - xs[i]
            for row, gy in enumerate(range(y0 + 3, vis - 5, 13)):
                k = r.random()
                if k < 0.35:
                    G.tree(g, ux + uw // 2, gy + 4, 3, r.choice([H("3F9B4A"), H("52A857"), H("2F8A44")]))
                elif k < 0.55:
                    g.rect(ux + 2, gy, 6, 4, H("9C7A55"))
                    g.rect(ux + 2, gy, 6, 1, H("B89A72"))
                    g.rect(ux + 2, gy + 4, 6, 1, (10, 18, 30, 60))
                elif k < 0.75:
                    g.rect(ux + 2, gy, uw - 4, 6, H("8B6A45"))
                    for yy in range(gy + 1, gy + 6, 2):
                        g.hline(ux + 3, ux + uw - 4, yy, H("5FA347"))
                elif k < 0.9:
                    g.rect(ux + 1, gy + 1, uw - 2, 5, H("D9CDB4"))  # patio
                    g.circle(ux + uw // 2, gy + 3, 1.5, H("F4F4F4"))
    shadow(g, L, 0, yb, fw, d, h + 3)
    roofc = ROW_ROOFS[v]
    pal = BRICKS[v]
    for i in range(n):
        x0, ww = xs[i], xs[i + 1] - xs[i]
        wall = pal[(i * 3 + i // 2) % 4] if T < 3 else pal[i % 2]
        if L.up:
            wall = shade(wall, 1.06)
        rx, ry, rw, rd = G.box3d(b, x0, yb, ww, d, h, roofc, wall, light=False)
        ytop = ry - 3
        if T == 3 and v % 2 == 1:  # mansard: flat top, dark slate band with dormers
            b.rect(rx, ytop, rw, rd + 3, shade(roofc, 0.9))
            b.rect(rx, ytop + rd - 1, rw, 4, shade(roofc, 0.7))
            b.rect(rx + ww // 2 - 2, ytop + rd, 4, 2, H("E8E4DA"))
            b.rect(rx + ww // 2 - 1, ytop + rd + 1, 2, 1, H("2C3A52"))
        else:
            G.pitched(b, rx, ytop, rw, rd + 3, shade(roofc, 1.0 + 0.05 * (i % 2)))
            if T >= 2:  # dormer
                b.rect(rx + ww // 2 - 2, ytop + rd // 2 + 2, 4, 3, shade(wall, 1.1))
                b.rect(rx + ww // 2 - 1, ytop + rd // 2 + 3, 2, 1, H("2C3A52"))
        b.rect(x0, ry + rd, 1, h, shade(wall, 0.75))  # party wall line
        floors = h // 5
        for f in range(floors):
            wy = yb - h + 1 + f * 5
            if f == floors - 1:
                continue
            G.windows(b, x0 + 1, wy, ww - 2, 3, 2, 1, lit=lit, seed=L.seed("w", i, f))
        # ground floor: door + window (bay window on T2+)
        dside = (i + v) % 2
        dx = x0 + (2 if dside == 0 else ww - 5)
        G.door(b, dx, yb - 5, 3, 5, [H("2E4C7A"), H("7A2E2E"), H("2E6B4C"), H("3A3A3A")][(i + v) % 4])
        wx = x0 + (ww - 6 if dside == 0 else 2)
        if T >= 2:
            b.rect(wx - 1, yb - 5, 5, 4, shade(wall, 1.12))
            b.rect(wx, yb - 4, 3, 2, H("FFD98A") if r.random() < lit else H("2C3A52"))
            b.rect(wx - 1, yb - 1, 5, 1, (10, 18, 30, 90))
        else:
            b.rect(wx, yb - 4, 3, 2, H("FFD98A") if r.random() < lit else H("2C3A52"))
        if i > 0 and (i % 2 == 0 or T == 1):
            G.chimney(b, x0 - 1, ry + 3, 4, 3, shade(wall, 0.8))
        if L.up and i % 2 == v % 2:
            if T == 1:
                G.solar_panel(b, rx + 1, ytop + rd // 2 + 2, ww - 2, 3)
            else:
                G.flowerbed(b, x0 + 1, yb - 9, ww - 2, seed=i)
        # front garden + path
        g.rect(dx, yb, 3, yB - 3 - yb, H("D8D2C2"))
        if T >= 2:
            g.rect(x0, yb + 1, 1, yB - 4 - yb, H("3E8E45"))
        if T == 3:
            b.rect(dx - 1, yb, 5, 2, H("D9D4C8"))  # stoop
        if (m > 9 or L.up) and r.random() < 0.6:
            G.bush(g, x0 + ww // 2 + (3 if dside == 0 else -3), yb + 4)
    b.rect(0, ry + rd, fw, 1, (255, 255, 255, 60))  # cornice line
    if T == 3:
        for x in range(6, fw - 2, 14):
            G.tree(g, x, yB - 6, 3, H("3F9B4A"))
    elif L.cy == 1 and L.up:
        G.tree(g, fw - 5, yB - 7, 3, H("52A857"))
    return finish(b, g)
