window.__publish = async function (slug) {
  const st = window.__pubStatus = { slug, done: 0, total: 0, phase: 'start', error: null };
  try {
    const plan = await (await fetch(`http://127.0.0.1:8765/${slug}/plan.json?x=${Date.now()}`)).json();
    st.total = plan.reduce((a, s) => a + s.groups.reduce((b, g) => b + g.items.length, 0), 0);
    await figma.loadFontAsync({ family: 'Inter', style: 'Regular' });
    await figma.loadFontAsync({ family: 'Inter', style: 'Bold' });
    const hex = h => { h = h.replace('#', ''); return { r: parseInt(h.slice(0, 2), 16) / 255, g: parseInt(h.slice(2, 4), 16) / 255, b: parseInt(h.slice(4, 6), 16) / 255 }; };
    const text = (s, size, bold, color) => { const t = figma.createText(); t.fontName = { family: 'Inter', style: bold ? 'Bold' : 'Regular' }; t.characters = s; t.fontSize = size; t.fills = [{ type: 'SOLID', color: hex(color || '1e1e1e') }]; return t; };
    const autoV = (name, gap, pad) => { const f = figma.createFrame(); f.name = name; f.layoutMode = 'VERTICAL'; f.primaryAxisSizingMode = 'AUTO'; f.counterAxisSizingMode = 'AUTO'; f.itemSpacing = gap; f.paddingTop = f.paddingBottom = f.paddingLeft = f.paddingRight = pad; f.fills = []; return f; };
    for (const sec of plan) {
      st.phase = 'section ' + sec.section;
      const page = figma.root.children.find(p => p.name === sec.page) || figma.root.children[0];
      await page.loadAsync?.();
      let pos = null;
      for (const old of page.children.filter(n => n.name === sec.section)) { pos = { x: old.x, y: old.y }; old.remove(); }
      if (!pos) { let maxY = 0; for (const n of page.children) maxY = Math.max(maxY, n.y + n.height); pos = { x: 0, y: page.children.length ? maxY + 240 : 0 }; }
      const body = autoV(sec.section, 40, 64);
      body.fills = [{ type: 'SOLID', color: hex('f4f3ef') }];
      body.cornerRadius = 16;
      body.appendChild(text(sec.section, 56, true));
      if (sec.note) body.appendChild(text(sec.note, 18, false, '5a5a5a'));
      for (const g of sec.groups) {
        const card = autoV(g.title || 'group', 14, 28);
        card.fills = [{ type: 'SOLID', color: hex('ffffff') }]; card.cornerRadius = 12;
        card.strokes = [{ type: 'SOLID', color: hex('dddad2') }]; card.strokeWeight = 1;
        if (g.title) card.appendChild(text(g.title, 24, true));
        if (g.note) card.appendChild(text(g.note, 14, false, '6a6a6a'));
        const grid = figma.createFrame(); grid.name = 'items'; grid.fills = [];
        grid.layoutMode = 'HORIZONTAL'; grid.layoutWrap = 'WRAP'; grid.itemSpacing = 16; grid.counterAxisSpacing = 20;
        grid.primaryAxisSizingMode = 'FIXED'; grid.counterAxisSizingMode = 'AUTO';
        const maxW = Math.max(64, ...g.items.map(i => i.w));
        const cols = Math.max(1, Math.min(g.columns || 8, g.items.length));
        card.appendChild(grid);
        grid.resize(Math.min(4800, cols * (maxW + 24) + (cols - 1) * 16), 10);
        grid.counterAxisSizingMode = 'AUTO'; grid.clipsContent = false;
        const bg = g.bg === 'dark' ? '2a2d33' : g.bg === 'none' ? null : (g.bg && g.bg.startsWith('#') ? g.bg : 'c3c8bd');
        for (const it of g.items) {
          const cell = autoV(it.label || 'item', 6, 0); cell.counterAxisAlignItems = 'CENTER';
          const tile = figma.createFrame(); tile.name = 'art'; tile.resize(it.w + 24, it.h + 24); tile.cornerRadius = 6;
          tile.fills = bg ? [{ type: 'SOLID', color: hex(bg) }] : [];
          const bytes = new Uint8Array(await (await fetch(it.url)).arrayBuffer());
          const img = figma.createImage(bytes);
          const r = figma.createRectangle(); r.name = it.label || 'image'; r.resize(it.w, it.h); r.x = 12; r.y = 12;
          r.fills = [{ type: 'IMAGE', imageHash: img.hash, scaleMode: 'FILL' }];
          tile.appendChild(r); cell.appendChild(tile);
          if (it.label) cell.appendChild(text(it.label, 13, false, '333333'));
          grid.appendChild(cell);
          st.done++;
        }
        body.appendChild(card);
      }
      const section = figma.createSection(); section.name = sec.section;
      page.appendChild(section); section.appendChild(body); body.x = 0; body.y = 0;
      section.resizeWithoutConstraints(body.width, body.height);
      section.x = pos.x; section.y = pos.y;
    }
    st.phase = 'done';
  } catch (e) { st.error = String(e && e.stack || e); st.phase = 'error'; }
  return window.__pubStatus;
};
'publisher loaded';
