"""iso_export_lib.py - shared helpers for tools/iso_export.py (design art -> game assets).

Asset contract (design/iso/IMPLEMENTATION.md):
- every frame RGBA, alpha 0/255, tightly cropped, even width and height;
- the anchor pixel (footprint ground centre / mover ground centre / cell centre) lands on the actor or cell
  CenterPosition: PngSheet `Frame[i] = "x,y,w,h;ox,oy"` with ox = w/2 - ax, oy = h/2 - ay;
- sequence-level Offset 0,0.

Frames come from three anchor styles, all normalised to `Frame(img, ax, ay)`:
- isokit Sprites (re-rendered from the generators, or PNGs with `Anchor`/`Offset` tEXt metadata: ZONED/CIVIC/KIT);
- LIFE `anchors.json` (`frame_from_png(path, anchor=(ax, ay))`);
- NET fixed canvases (`frame_from_canvas(path, ax, ay)`: e.g. ground tiles 64x32 anchor (32, 16), props 64x96 (32, 80)).

Determinism: pure functions of the generators (fixed seeds), stable packing order, zlib level 9, no timestamps.
"""
import hashlib
import multiprocessing as mp
import os
import struct
import sys
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

MOD = os.path.normpath(os.path.join(HERE, ".."))
WORLD = os.path.join(MOD, "bits", "world")
ISO = os.path.join(WORLD, "iso")
SEQ = os.path.join(MOD, "sequences")
DESIGN = os.path.join(MOD, "design", "iso")

SHEET_MAX_W = 1024   # packed sheet width limit (frames never exceed the 2048 atlas)


class Frame:
    """One sprite frame: img (H, W, 4) uint8 with alpha 0/255, anchor pixel (ax, ay)."""
    __slots__ = ("img", "ax", "ay")

    def __init__(self, img, ax, ay):
        img = np.ascontiguousarray(img, dtype=np.uint8)
        if img.ndim != 3 or img.shape[2] != 4:
            raise ValueError("frame must be (H, W, 4)")
        a = img[..., 3]
        img = img.copy()
        img[a < 128] = 0
        img[a >= 128, 3] = 255
        self.img, self.ax, self.ay = img, int(ax), int(ay)

    @property
    def w(self):
        return self.img.shape[1]

    @property
    def h(self):
        return self.img.shape[0]

    def tight(self):
        """Crop to content, then pad right/bottom to even size (anchor kept)."""
        a = self.img[..., 3] > 0
        if not a.any():
            return EMPTY
        rows, cols = np.where(a.any(1))[0], np.where(a.any(0))[0]
        r0, r1, c0, c1 = rows[0], rows[-1] + 1, cols[0], cols[-1] + 1
        img = self.img[r0:r1, c0:c1]
        h, w = img.shape[:2]
        H, W = h + (h & 1), w + (w & 1)
        if (H, W) != (h, w):
            out = np.zeros((H, W, 4), np.uint8)
            out[:h, :w] = img
            img = out
        f = Frame.__new__(Frame)
        f.img, f.ax, f.ay = img, self.ax - c0, self.ay - r0
        return f

    def offset(self):
        return self.w // 2 - self.ax, self.h // 2 - self.ay

    def key(self):
        return hashlib.sha1(self.img.tobytes() + struct.pack(">3i", self.w, self.ax, self.ay)).digest()

    def empty(self):
        return not (self.img[..., 3] > 0).any()


_e = Frame.__new__(Frame)
_e.img, _e.ax, _e.ay = np.zeros((2, 2, 4), np.uint8), 1, 1
EMPTY = _e


def frame_from_sprite(spr):
    """isokit Sprite (img, ax, ay) -> Frame."""
    return Frame(spr.img, spr.ax, spr.ay)


def frame_from_array(img, ax, ay):
    return Frame(img, ax, ay)


def lit_of(night_spr):
    """'-lit' companion: only the emissive pixels of an isokit night render (same anchor)."""
    img = np.zeros_like(night_spr.img)
    img[night_spr.emit] = night_spr.img[night_spr.emit]
    return Frame(img, night_spr.ax, night_spr.ay)


# ---------------------------------------------------------------------------------------------- PNG I/O
def _chunk(tag, data):
    c = struct.pack(">I", len(data)) + tag + data
    return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def png_bytes(img, meta=None):
    img = np.ascontiguousarray(img, dtype=np.uint8)
    h, w = img.shape[:2]
    raw = np.zeros((h, w * 4 + 1), np.uint8)
    raw[:, 1:] = img.reshape(h, w * 4)
    out = [b"\x89PNG\r\n\x1a\n", _chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))]
    for k, v in (meta or []):
        out.append(_chunk(b"tEXt", k.encode("latin-1") + b"\0" + str(v).encode("latin-1")))
    out.append(_chunk(b"IDAT", zlib.compress(raw.tobytes(), 9)))
    out.append(_chunk(b"IEND", b""))
    return b"".join(out)


def write_if_changed(path, data):
    """Write bytes (or str) only when the content differs (keeps re-runs quiet in git)."""
    if isinstance(data, str):
        data = data.encode("utf-8")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        with open(path, "rb") as f:
            if f.read() == data:
                return False
    with open(path, "wb") as f:
        f.write(data)
    return True


def read_png(path):
    """Read an 8-bit RGB/RGBA non-interlaced PNG -> ((H, W, 4) uint8, {tEXt key: value})."""
    with open(path, "rb") as f:
        data = f.read()
    pos, idat, meta = 8, [], {}
    w = h = ct = 0
    while pos < len(data):
        ln, tag = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
            if bd != 8 or ct not in (2, 6):
                raise ValueError("%s: unsupported PNG (bit depth %d, colour type %d)" % (path, bd, ct))
        elif tag == b"IDAT":
            idat.append(body)
        elif tag == b"tEXt":
            k, v = body.split(b"\0", 1)
            meta[k.decode("latin-1")] = v.decode("latin-1")
        pos += 12 + ln
    bpp = 4 if ct == 6 else 3
    raw = np.frombuffer(zlib.decompress(b"".join(idat)), np.uint8).reshape(h, w * bpp + 1)
    out = np.zeros((h, w * bpp), np.uint8)
    prev = np.zeros(w * bpp, np.int32)
    for y in range(h):
        ft, line = raw[y, 0], raw[y, 1:].astype(np.int32)
        if ft == 0:
            cur = line
        elif ft == 2:
            cur = (line + prev) & 255
        elif ft == 1:
            cur = line.copy()
            for x in range(bpp, w * bpp):
                cur[x] = (cur[x] + cur[x - bpp]) & 255
        else:
            cur = np.zeros_like(line)
            for x in range(w * bpp):
                a = int(cur[x - bpp]) if x >= bpp else 0
                b = int(prev[x])
                c = int(prev[x - bpp]) if x >= bpp else 0
                if ft == 3:
                    p = (a + b) // 2
                else:
                    pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                    p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                cur[x] = (int(line[x]) + p) & 255
        out[y] = cur
        prev = cur.astype(np.int32)
    img = out.reshape(h, w, bpp)
    if bpp == 3:
        img = np.concatenate([img, np.full((h, w, 1), 255, np.uint8)], 2)
    return img, meta


def frame_from_png(path, anchor=None):
    """Design PNG -> Frame. Anchor: explicit (LIFE anchors.json), else tEXt `Anchor`, else tEXt `Offset`
    (ox = w/2 - ax), else the image centre."""
    img, meta = read_png(path)
    h, w = img.shape[:2]
    if anchor is not None:
        ax, ay = anchor
    elif "Anchor" in meta:
        ax, ay = (int(v) for v in meta["Anchor"].split(","))
    elif "Offset" in meta:
        ox, oy = (int(float(v)) for v in meta["Offset"].split(","))
        ax, ay = w // 2 - ox, h // 2 - oy
    else:
        ax, ay = w // 2, h // 2
    return Frame(img, ax, ay)


def frame_from_canvas(path, ax, ay):
    """NET fixed canvas (anchor at a fixed pixel of a fixed-size canvas)."""
    img, _ = read_png(path)
    return Frame(img, ax, ay)


# ---------------------------------------------------------------------------------------------- sheets
def write_sheet(rel, frames, max_w=SHEET_MAX_W):
    """Pack frames (list of Frame) into bits/world/iso/<rel> with Frame[i] metadata.
    Frames are tight-cropped and even; identical frames share one region. Returns the frame count."""
    if not frames:
        raise ValueError("%s: no frames" % rel)
    tf = [f.tight() for f in frames]
    uniq, idx = {}, []
    for f in tf:
        k = f.key()
        if k not in uniq:
            uniq[k] = len(uniq)
        idx.append(uniq[k])
    order = list(uniq.values())
    reps = [None] * len(order)
    for i, f in enumerate(tf):
        if reps[idx[i]] is None:
            reps[idx[i]] = f
    # shelf packing, tallest first (stable)
    sorted_ids = sorted(range(len(reps)), key=lambda i: (-reps[i].h, -reps[i].w, i))
    width = max(max(r.w for r in reps), min(max_w, sum(r.w for r in reps)))
    pos = [None] * len(reps)
    x = y = shelf = 0
    for i in sorted_ids:
        r = reps[i]
        if x + r.w > width:
            x, y, shelf = 0, y + shelf, 0
        pos[i] = (x, y)
        x += r.w
        shelf = max(shelf, r.h)
    height = y + shelf
    width, height = width + (width & 1), height + (height & 1)
    sheet = np.zeros((height, width, 4), np.uint8)
    for i, r in enumerate(reps):
        px, py = pos[i]
        sheet[py:py + r.h, px:px + r.w] = r.img
    meta = []
    for n, f in enumerate(tf):
        u = idx[n]
        px, py = pos[u]
        r = reps[u]
        ox, oy = r.offset()
        meta.append(("Frame[%d]" % n, "%d,%d,%d,%d;%d,%d" % (px, py, r.w, r.h, ox, oy)))
    write_if_changed(os.path.join(ISO, rel), png_bytes(sheet, meta))
    return len(frames)


def write_png_plain(path, img, meta=None):
    """Write a plain PNG (absolute path), e.g. terrain.png or map previews."""
    return write_if_changed(path, png_bytes(img, list((meta or {}).items()) if isinstance(meta, dict) else meta))


# ---------------------------------------------------------------------------------------------- sequences
class SeqFile:
    """Deterministic MiniYaml writer for sequences/<name>.yaml.

        sf = SeqFile('networks.yaml', 'Road art. Frame layouts: ...')
        sf.seq('roadnet', 'street', 'iso/net/street.png', length=32, comment='0-15 two-way by mask')
        sf.write()
    Filenames are relative to bits/world (so 'iso/...')."""

    def __init__(self, name, header):
        self.name, self.header = name, header
        self.images = {}       # image -> {"inherits": str|None, "defaults": dict, "comment": str, "seqs": [...]}

    def image(self, image, inherits=None, defaults=None, comment=None):
        d = self.images.setdefault(image, {"inherits": None, "defaults": {}, "comment": None, "seqs": []})
        if inherits:
            d["inherits"] = inherits
        if defaults:
            d["defaults"].update(defaults)
        if comment:
            d["comment"] = comment
        return d

    def seq(self, image, seq, filename, length="*", start=None, comment=None, **props):
        """props: Tick, Facings, ZOffset, BlendMode, Frames, IgnoreWorldTint, ... (MiniYaml keys, values as str/int)."""
        d = self.image(image)
        p = {"Filename": filename}
        if start is not None:
            p["Start"] = start
        if length is not None:
            p["Length"] = length
        for k, v in props.items():
            if v is not None:
                p[k] = v
        d["seqs"].append((seq, p, comment))

    def text(self):
        out = []
        for line in self.header.strip("\n").split("\n"):
            out.append(("# " + line).rstrip())
        out.append("# Generated by tools/iso_export.py - do not edit by hand.")
        out.append("")
        for image, d in self.images.items():
            if d["comment"]:
                for line in d["comment"].split("\n"):
                    out.append(("# " + line).rstrip())
            out.append("%s:" % image)
            if d["inherits"]:
                out.append("\tInherits: %s" % d["inherits"])
            if d["defaults"]:
                out.append("\tDefaults:")
                for k, v in d["defaults"].items():
                    out.append("\t\t%s: %s" % (k, _yv(v)))
            for seq, p, comment in d["seqs"]:
                if comment:
                    out.append("\t# " + comment)
                out.append("\t%s:" % seq)
                for k, v in p.items():
                    out.append("\t\t%s: %s" % (k, _yv(v)))
            out.append("")
        return "\n".join(out).rstrip("\n") + "\n"

    def write(self):
        return write_if_changed(os.path.join(SEQ, self.name), self.text())


def _yv(v):
    if isinstance(v, bool):
        return "True" if v else "False"
    if isinstance(v, (list, tuple)):
        return ", ".join(str(x) for x in v)
    return str(v)


# ---------------------------------------------------------------------------------------------- parallel
def pmap(fn, items, procs=None, chunksize=1):
    """Ordered parallel map (fork). fn must be a module-level function; results must be picklable
    (Frames and isokit Sprites are). Set ISO_EXPORT_PROCS=1 to run serially."""
    items = list(items)
    n = int(os.environ.get("ISO_EXPORT_PROCS", procs or max(1, min(12, (os.cpu_count() or 2) - 2))))
    if n <= 1 or len(items) < 4:
        return [fn(x) for x in items]
    ctx = mp.get_context("fork")
    with ctx.Pool(n) as pool:
        return pool.map(fn, items, chunksize=chunksize)


def preview(path, frames, cols=8, k=2, bg=(96, 104, 96)):
    """Anchor-aligned contact sheet of frames (for reviewing an export; writes to an absolute path)."""
    if not frames:
        return
    L = max(f.ax for f in frames)
    T = max(f.ay for f in frames)
    R = max(f.w - f.ax for f in frames)
    B = max(f.h - f.ay for f in frames)
    cw, ch = L + R + 4, T + B + 4
    rows = (len(frames) + cols - 1) // cols
    out = np.zeros((rows * ch, cols * cw, 4), np.uint8)
    out[..., :3] = bg
    out[..., 3] = 255
    for i, f in enumerate(frames):
        r, c = divmod(i, cols)
        y0, x0 = r * ch + 2 + T - f.ay, c * cw + 2 + L - f.ax
        a = f.img[..., 3] > 0
        reg = out[y0:y0 + f.h, x0:x0 + f.w]
        reg[a] = f.img[a]
        out[r * ch + 2 + T, c * cw + 2 + L] = (255, 0, 255, 255)   # anchor dot
    out = np.repeat(np.repeat(out, k, 0), k, 1)
    write_if_changed(path, png_bytes(out))
