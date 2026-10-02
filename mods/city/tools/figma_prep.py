#!/usr/bin/env python3
# Figma publishing for design reviews. Flow: run figma_serve.py (serves /tmp/figma-serve on 127.0.0.1:8765 with CORS),
# figma_prep.py <manifest.json> <slug> (nearest-upscales the art), then in the Figma tab (claude-in-chrome javascript_tool):
# eval(await (await fetch('http://127.0.0.1:8765/publish.js')).text()); await __publish('<slug>')  [copy figma_publish.js to /tmp/figma-serve/publish.js first]
"""prep.py <manifest.json> <slug> : upscale manifest images (nearest) into /tmp/figma-serve/<slug>/ and write plan.json"""
import json, os, sys, zlib, struct
sys.path.insert(0, '/home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/mods/city/tools')

import subprocess
def upscale(src, dst, s):
    subprocess.run(['magick', src, '-sample', f'{s*100}%', 'PNG32:' + dst], check=True)
    w, h = subprocess.run(['magick', 'identify', '-format', '%w %h', dst], check=True, capture_output=True, text=True).stdout.split()
    return int(w), int(h)

def main():
    man_path, slug = sys.argv[1], sys.argv[2]
    base = os.path.dirname(os.path.abspath(man_path))
    man = json.load(open(man_path))
    sections = man if isinstance(man, list) else man.get('sections', [man])
    outdir = f'/tmp/figma-serve/{slug}'; os.makedirs(outdir, exist_ok=True)
    plan, n = [], 0
    for si, sec in enumerate(sections):
        sbase = base
        if isinstance(sec, str):  # list of sub-manifest paths
            sub = os.path.join(base, sec); sbase = os.path.dirname(sub); sec = json.load(open(sub))
        psec = {'page': sec.get('page', man.get('page', 'World elements') if isinstance(man, dict) else 'World elements'), 'section': sec['section'], 'note': sec.get('note', ''), 'groups': []}
        for gi, g in enumerate(sec['groups']):
            scale = int(g.get('scale', 2)); pg = {'title': g.get('title', ''), 'note': g.get('note', ''), 'columns': int(g.get('columns', 8)), 'bg': g.get('bg', ''), 'items': []}
            for ii, it in enumerate(g['items']):
                src = os.path.join(sbase, it['file'])
                if not os.path.exists(src): print('MISSING', src); continue
                s = int(it.get('scale', scale)); name = f's{si}g{gi}i{ii}.png'
                w, h = upscale(src, os.path.join(outdir, name), s)
                pg['items'].append({'url': f'http://127.0.0.1:8765/{slug}/{name}', 'w': w, 'h': h, 'label': it.get('label', '')}); n += 1
            psec['groups'].append(pg)
        plan.append(psec)
    json.dump(plan, open(os.path.join(outdir, 'plan.json'), 'w'))
    print(f'{slug}: {len(plan)} sections, {n} images')

main()
