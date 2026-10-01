"""Build-menu icons (48x48) for placeables that SVC's gen_svc.py does not cover (transit, signature buildings, battery, fish hub).

The icons are rendered from the buildings' existing sprites with the same helper SVC uses (genworld.build_icon) into a second atlas,
bits/chrome/buildicons-extra.png, whose regions are appended to chrome-city.yaml as the collection `city-buildicons-extra`.
The toolbar looks an actor's icon up in `city-buildicons` first and in `city-buildicons-extra` second.
"""

import os
import struct
import zlib

import genworld as gw
from pngkit import Canvas, save_atlas

# actor -> (sprite under bits/world, icon tint)
EXTRA = {
    'taxidepot': ('pt/taxidepot.png', '2E9E6A'),
    'tramdepot': ('pt/tramdepot.png', '2E9E6A'),
    'metrostation': ('pt/metrostation.png', '2E9E6A'),
    'trainstation': ('pt/trainstation.png', '2E9E6A'),
    'railyard': ('pt/railyard.png', '6E7882'),
    'cargoharbor': ('pt/cargoharbor.png', '4FA3E8'),
    'cargoairport': ('pt/cargoairport.png', '9B7BE0'),
    'fish-hub': ('ind/fish-hub.png', '4FA3E8'),
    'battery': ('net/battery.png', 'F2B84B'),
    'sig-villa': ('zon/sig-villa.png', 'C9A52E'),
    'sig-garden-row': ('zon/sig-garden-row.png', 'C9A52E'),
    'sig-sky-tower': ('zon/sig-sky-tower.png', 'C9A52E'),
    'sig-bazaar': ('zon/sig-bazaar.png', 'C9A52E'),
    'sig-mall': ('zon/sig-mall.png', 'C9A52E'),
    'sig-glass-hub': ('zon/sig-glass-hub.png', 'C9A52E'),
    'sig-fuel-plant': ('zon/sig-fuel-plant.png', 'C9A52E'),
}


def load_png(path):
    """Decode an 8-bit RGBA / RGB PNG (stdlib only) into a pngkit Canvas."""
    data = open(path, 'rb').read()
    pos, idat = 8, b''
    w = h = ct = None
    while pos < len(data):
        n, = struct.unpack('>I', data[pos:pos + 4])
        tag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + n]
        if tag == b'IHDR':
            w, h, _, ct = struct.unpack('>IIBB', body[:10])
        elif tag == b'IDAT':
            idat += body
        pos += 12 + n
    raw = zlib.decompress(idat)
    bpp = {2: 3, 6: 4}[ct]
    stride = w * bpp
    prev = bytearray(stride)
    cv = Canvas(w, h)
    i = 0
    for y in range(h):
        f = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1:
                line[x] = (line[x] + a) & 255
            elif f == 2:
                line[x] = (line[x] + b) & 255
            elif f == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else (b if pb <= pc else c))) & 255
        for x in range(w):
            px = line[x * bpp:x * bpp + bpp]
            cv.px[y * w + x] = [px[0], px[1], px[2], px[3] if bpp == 4 else 255]
        prev = line
    return cv


def build(mod):
    regions = {}
    for name, (sprite, tint) in EXTRA.items():
        gw.ICON_TINT[name] = tint
        frame = load_png(os.path.join(mod, 'bits', 'world', sprite))
        regions[name] = gw.build_icon(name, frame, 2)

    placed = save_atlas(os.path.join(mod, 'bits', 'chrome', 'buildicons-extra.png'), regions, width=256)
    lines = ['', '# Build icons of the transit, signature, battery and fish hub placeables (tools/uibuildicons.py).',
             'city-buildicons-extra:', '\tInherits: ^CityChrome', '\tImage: buildicons-extra.png', '\tRegions:']
    for name in sorted(placed):
        x, y, w, h = placed[name]
        lines.append('\t\t%s: %d, %d, %d, %d' % (name, x, y, w, h))

    # Replace the generated section at the end of chrome-city.yaml (the rest of that file is hand written)
    path = os.path.join(mod, 'chrome-city.yaml')
    text = open(path).read()
    marker = text.find(lines[1])
    if marker >= 0:
        text = text[:marker].rstrip('\n') + '\n'
    with open(path, 'w') as f:
        f.write(text + '\n'.join(lines) + '\n')
