"""Scene -> palette-quantized RGBA Sprite (z-buffer, fixed light, materials, night, snow, outline)."""
import numpy as np

from .geom import LIGHT, Face, Ellipsoid, project
from .raster import Buffers, raster_face, raster_ellipsoid
from .palette import PALETTE, SHADES, RAMP, shade_lookup, nearest
from .noise import value2, hash2, fbm
from .png import write_png

AMB, DIF = 0.25, 0.60          # level = AMB + DIF * max(0, n.L); top 0.77, left 0.53, right 0.25
NIGHT_SKY = np.array([22, 32, 78], np.float64)


class Ctx:
    pass


class Sprite:
    """Rendered sprite. img (H, W, 4) uint8 alpha 0/255; (ax, ay) = anchor pixel = screen position of
    the footprint's ground centre; emit = bool mask of emissive pixels (night lights)."""

    def __init__(self, img, ax, ay, emit=None, footprint=(1, 1)):
        self.img, self.ax, self.ay = img, int(ax), int(ay)
        self.emit = emit if emit is not None else np.zeros(img.shape[:2], bool)
        self.footprint = footprint

    @property
    def anchor(self):
        return (self.ax, self.ay)

    @property
    def w(self):
        return self.img.shape[1]

    @property
    def h(self):
        return self.img.shape[0]

    def save(self, path, meta=True):
        m = {"Offset": "%d,%d" % (self.w // 2 - self.ax, self.h // 2 - self.ay),
             "Anchor": "%d,%d" % (self.ax, self.ay)} if meta else None
        write_png(path, self.img, m)
        return path


def level(n):
    return AMB + DIF * np.maximum(0.0, n @ LIGHT)


def render(scene, facing=0, night=False, season="summer", frame=0, outline="edge",
           origin=(0, 0), pad=2, crop=True, light_scale=1.0, creases=2.0, snow=1.0, scale=1.0, tile=None,
           yaw=None, outline_ground=False):
    """Render a Scene. facing 0..3 rotates the model about its footprint (front +Y, +X, -Y, -X).

    night: dark ambient + emissive windows/lamps. season: 'spring'|'summer'|'autumn'|'winter'
    (winter puts snow on up-facing surfaces of materials with snow=True). outline: None | 'edge'
    (darken silhouette rim) | 'dark' (1-px dark outline outside). origin: world cell offset used for
    global pattern coordinates (seamless ground when compositing). creases: shade steps to darken
    pixels lying just behind a nearer, different primitive (eave shadow lines, tree clump separation);
    0 disables. snow: winter snow cover amount 0..1 (patchy below 1).
    scale / tile: reduced-scale projection (tile=(32, 16) == scale=0.5). Geometry is re-projected, and
    patterns are evaluated in output pixels, so window grids get fewer, still-readable windows. Use
    tile widths that are multiples of 4 for clean 2:1 stairs.
    yaw: rotate by any angle (degrees, yaw = 90 * facing), e.g. 45 * k for 8-direction vehicles;
    overrides facing. outline_ground: also outline 'ground' faces (tiles/decals); off by default so
    ground tiles and decals join seamlessly.
    """
    if tile is not None:
        scale = tile[0] / 64.0
    items, fp = scene.items(facing, yaw)
    faces = [it for it in items if isinstance(it, Face)]
    ells = [it for it in items if isinstance(it, Ellipsoid)]
    from .geom import VIEW_M
    faces = [f for f in faces if not f.cull or f.n @ VIEW_M > 1e-6]
    xs, ys = [], []
    for f in faces:
        px, py = project(f.verts[:, 0], f.verts[:, 1], f.verts[:, 2])
        xs += [px.min(), px.max()]
        ys += [py.min(), py.max()]
    for e in ells:
        for sx_ in (-1, 1):
            for sy_ in (-1, 1):
                for sz_ in (-1, 1):
                    px, py = project(e.c[0] + sx_ * e.r[0], e.c[1] + sy_ * e.r[1], e.c[2] + sz_ * e.r[2])
                    xs.append(px)
                    ys.append(py)
    ax_, ay_ = project(fp[0] / 2.0, fp[1] / 2.0, 0.0)
    ax_, ay_ = ax_ * scale, ay_ * scale
    xs = [v * scale for v in xs]
    ys = [v * scale for v in ys]
    if not xs:
        return Sprite(np.zeros((1, 1, 4), np.uint8), 0, 0, footprint=fp)
    x0 = int(np.floor(min(xs))) - pad - 1
    y0 = int(np.floor(min(ys))) - pad - 1
    w = int(np.ceil(max(xs))) + pad + 1 - x0
    h = int(np.ceil(max(ys))) + pad + 1 - y0
    buf = Buffers(x0, y0, w, h, scale)
    allp = faces + ells
    for i, it in enumerate(allp):
        if isinstance(it, Face):
            raster_face(buf, it, i)
        else:
            raster_ellipsoid(buf, it, i)
    ox, oy = project(origin[0], origin[1], 0)
    crease = _creases(buf, allp) * creases if creases else None
    rgb, ramp, idx, emit = _shade(buf, allp, night, season, frame, int(round(ox * scale)), int(round(oy * scale)), light_scale,
                                  crease, snow)
    alpha = buf.fid >= 0
    if outline:
        solid = alpha
        if not outline_ground:
            gk = np.array([_is_ground(it) for it in allp] + [False])
            solid = alpha & ~gk[buf.fid]
        rgb = _outline(rgb, ramp, idx, alpha, outline, night, solid)
        if outline == "dark":
            alpha = alpha | _dilate(solid)
    img = np.zeros((h, w, 4), np.uint8)
    img[..., :3] = rgb
    img[..., 3] = np.where(alpha, 255, 0)
    img[~alpha] = 0
    ax, ay = ax_ - x0, ay_ - y0
    if crop:
        img, emit, ax, ay = crop_to_content(img, emit, ax, ay)
    return Sprite(img, round(ax), round(ay), emit, fp)


def crop_to_content(img, emit, ax, ay, pad=0):
    a = img[..., 3] > 0
    if not a.any():
        return img[:1, :1], emit[:1, :1], ax, ay
    rows, cols = np.where(a.any(1))[0], np.where(a.any(0))[0]
    r0, r1 = max(rows[0] - pad, 0), rows[-1] + 1 + pad
    c0, c1 = max(cols[0] - pad, 0), cols[-1] + 1 + pad
    return img[r0:r1, c0:c1], emit[r0:r1, c0:c1], ax - c0, ay - r0


def _dilate(m):
    d = np.zeros_like(m)
    d[1:] |= m[:-1]
    d[:-1] |= m[1:]
    d[:, 1:] |= m[:, :-1]
    d[:, :-1] |= m[:, 1:]
    return d & ~m


def _creases(buf, allp, thr=0.06):
    """1.0 where a pixel is behind a 4-neighbour of another primitive by more than thr depth units."""
    d, f = buf.depth, buf.fid
    out = np.zeros(d.shape)
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        nd = np.full_like(d, -np.inf)
        nf = np.full_like(f, -1)
        ys = slice(max(dy, 0), d.shape[0] + min(dy, 0))
        yd = slice(max(-dy, 0), d.shape[0] + min(-dy, 0))
        xs = slice(max(dx, 0), d.shape[1] + min(dx, 0))
        xd = slice(max(-dx, 0), d.shape[1] + min(-dx, 0))
        nd[yd, xd] = d[ys, xs]
        nf[yd, xd] = f[ys, xs]
        with np.errstate(invalid="ignore"):
            out[(f >= 0) & (nf >= 0) & (nf != f) & (nd - d > thr)] = 1.0
    ground = np.array([getattr(it, "kind", "") == "ground" for it in allp] + [False])
    out[ground[f]] = 0.0
    return out


def _shade(buf, allp, night, season, frame, gox, goy, light_scale, crease=None, snow=1.0):
    h, w = buf.fid.shape
    rgb = np.zeros((h, w, 3), np.uint8)
    rampb = np.zeros((h, w), np.int64)
    idxb = np.zeros((h, w))
    emitb = np.zeros((h, w), bool)
    for fid in np.unique(buf.fid):
        if fid < 0:
            continue
        it = allp[fid]
        m = it.mat
        jj, ii = np.where(buf.fid == fid)
        c = Ctx()
        P, N = buf.P[jj, ii], buf.N[jj, ii]
        c.x, c.y, c.z, c.n = P[:, 0], P[:, 1], P[:, 2], N
        c.sx = ii + buf.x0 + gox
        c.sy = jj + buf.y0 + goy
        c.kind, c.seed, c.night, c.frame, c.season = it.kind, int(it.seed), night, frame, season
        c.info = it.info
        if isinstance(it, Face):
            d = P - it.o
            k = buf.scale
            c.u, c.v = (d @ it.ua) * k, (d @ it.va) * k
            c.u0, c.u1, c.v0, c.v1 = (e * k for e in it.uv_extent())
        else:
            c.u, c.v = c.x * 32.0 * buf.scale, c.y * 32.0 * buf.scale
            c.u0, c.u1, c.v0, c.v1 = 0.0, 1.0, 0.0, 1.0
        npx = len(jj)
        c.ramp = np.full(npx, m.ramp, np.int64)
        c.tone = np.zeros(npx)
        c.emit = np.zeros(npx, bool)
        c.eramp = np.full(npx, RAMP["yellow"], np.int64)
        c.eshade = np.full(npx, 10.0)
        c.flat = np.full(npx, np.nan)
        if m.pattern is not None:
            m.pattern(c, m)
        lv = level(N) * light_scale
        idx = lv * (SHADES - 1) + m.shade + c.tone
        if crease is not None:
            idx = idx - crease[jj, ii]
        fl = ~np.isnan(c.flat)
        idx[fl] = c.flat[fl]
        ramp = c.ramp
        if season == "winter" and m.snow:
            nz = N[:, 2]
            cov = value2(c.sx, c.sy, 3.0, 99) * 0.25 + hash2(c.sx, c.sy, 98) * 0.15
            sn = (nz + cov > m.p("snow_at", 0.5)) & ~c.emit
            if snow < 1.0:
                patch = fbm(c.sx, c.sy * 2.0, 9.0, 3, 96) + (hash2(c.sx, c.sy, 95) - 0.5) * 0.12
                sn &= patch < snow * 1.3 - 0.15
            ramp = ramp.copy()
            ramp[sn] = RAMP["snow"]
            idx[sn] = lv[sn] * (SHADES - 1) + 2.4 + (hash2(c.sx, c.sy, 97)[sn] - 0.5) * 0.6
        dith = m.dither
        if night:
            idx = idx * 0.68 - 1.3
        col = shade_lookup(ramp, idx, jj, ii, dith)
        if night:
            col = night_color(col, ramp)
            e = c.emit
            if e.any():
                col[e] = PALETTE[c.eramp[e], np.clip(c.eshade[e], 0, SHADES - 1).astype(int)]
        rgb[jj, ii] = col
        rampb[jj, ii] = ramp
        idxb[jj, ii] = idx
        emitb[jj, ii] = c.emit if night else False
    return rgb, rampb, idxb, emitb


def night_color(col, ramp, amt=0.38):
    """Blend palette colours toward the night sky and snap back to the pixel's own ramp or slate."""
    mix = col.astype(np.float64) * (1 - amt) + NIGHT_SKY * amt
    cand = np.concatenate([PALETTE[ramp], np.broadcast_to(PALETTE[RAMP["slate"]], (len(ramp), SHADES, 3))], 1)
    w = np.array([0.30, 0.59, 0.11])
    d = (((cand.astype(np.float64) - mix[:, None, :]) ** 2) * w).sum(-1)
    d[:, SHADES:] *= 1.15          # prefer keeping the own hue
    return cand[np.arange(len(ramp)), np.argmin(d, 1)]


def _is_ground(it):
    """Ground decals/tiles: kind 'ground', or flat up-facing faces lying at z <= 0."""
    k = getattr(it, "kind", "")
    if k == "ground":
        return True
    return k == "top" and isinstance(it, Face) and float(it.verts[:, 2].max()) <= 0.01


def _outline(rgb, ramp, idx, alpha, mode, night, solid=None):
    out = rgb.copy()
    solid = alpha if solid is None else solid
    if mode == "edge":
        edge = solid & _border(alpha)
        jj, ii = np.where(edge)
        out[jj, ii] = PALETTE[ramp[jj, ii], np.clip(np.round(idx[jj, ii] - 2.0), 0, SHADES - 1).astype(int)]
    elif mode == "dark":
        ring = _dilate(solid) & ~alpha
        out[ring] = (12, 12, 20)
    return out


def _border(a):
    """Opaque pixels with a transparent 4-neighbour."""
    t = ~a
    b = np.zeros_like(a)
    b[1:] |= t[:-1]
    b[:-1] |= t[1:]
    b[:, 1:] |= t[:, :-1]
    b[:, :-1] |= t[:, 1:]
    b[0, :] = True
    b[-1, :] = True
    b[:, 0] = True
    b[:, -1] = True
    return b
