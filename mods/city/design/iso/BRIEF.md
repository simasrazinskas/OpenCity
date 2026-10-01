# OpenCity isometric art: design brief (design phase)

Goal: every graphic in OpenCity redesigned in the 2.5D look of RollerCoaster Tycoon 2 / Transport Tycoon
(Chris Sawyer era: pre-rendered-looking 3D models, isometric pixel art, ramped palette), applied to a
Cities: Skylines 2-style city. **Design phase only**: produce art + Figma manifests. Do NOT change the game
(no rules/sequences/C#/mod.yaml edits). Implementation happens after the user approves the designs in Figma.

## Projection and scale (fixed, everyone uses these)
- True 2:1 dimetric isometric. One map cell = one diamond tile of **64 x 32 px** at 1x (RCT2/TTD size).
- World axes: +X runs screen down-right, +Y runs screen down-left, +Z is up.
  screen_x = (x - y) * 32, screen_y = (x + y) * 16 - z_px. Cell (cx, cy) covers x in [cx, cx+1], y in [cy, cy+1].
- Vertical unit: **z in screen pixels at 1x**. A storey is about **10 px** (exaggerated like TTD; the kit
  owner may tune 8-12 once and document it). A cell is ~16 m; cars ~14-20 px long; people ~6-8 px tall.
- Flat terrain (no height levels) in this phase; water is a flat level. Cliffs/height are optional extras.
- Light comes from the upper left: the left visible faces (normal +Y) are lit, right visible faces (normal +X)
  are shaded, tops brightest. Shadows: soft-free, short drop shadow on the ground toward the lower right is
  allowed if the kit provides it consistently.
- Camera never rotates. Buildings with a clear front (door, driveway, shopfront) are modelled in 3D so they can
  be rendered in all **4 facings** (front toward +X, +Y, -X, -Y); Figma shows the default facing (+Y, front
  toward the viewer's lower left) plus a 4-facing strip for one representative per family.
- Pixel art rules: no anti-aliasing against transparency (alpha is 0 or 255), limited ramped palette,
  crisp 2:1 pixel stair edges, readable at 1x. Ordered dithering inside faces is allowed if it helps.

## Shared renderer: isokit
- Python package `mods/city/tools/isokit/` (numpy is available; stdlib otherwise). Owned by workstream KIT.
- It turns 3D primitives (boxes, gable/hip/flat roofs, cylinders, cones, spheres/blobs, ground polygons,
  arbitrary quads) with materials (colour ramp + procedural surface pattern such as windows, brick, siding,
  roof tiles, glass, asphalt, grass) into an RGBA sprite plus its anchor (the screen position of the
  footprint's ground centre), using a z-buffer, the fixed projection and the fixed light.
- Everyone else builds models with isokit. Model files stay in your own generator scripts. If you need a new
  primitive or material, add it in your own file first; ask KIT (via your report) to adopt generally useful ones.

## Outputs
- Generators: `mods/city/tools/iso_<area>*.py` (re-runnable, deterministic: no random without fixed seeds).
- PNGs at 1x: `mods/city/design/iso/<area>/...png` (RGBA, alpha 0/255).
- Manifest for Figma: `mods/city/design/iso/<area>/manifest.json`:
  {"page": "World elements" | "Interface" | "Direction & mockups",
   "section": "Roads & networks",
   "groups": [{"title": "Street: all 16 connection masks", "note": "optional one-liner",
               "columns": 8, "scale": 2,
               "items": [{"file": "roads/street-0101.png", "label": "N+S"}]}]}
  `file` is relative to the manifest's folder. `scale` is the integer nearest-neighbour display scale in Figma
  (2 for most world art, 1 for mockup scenes and large UI). Keep labels short. Order groups as a designer would.
- Animated things: render the frames and also a horizontal strip PNG; label "N frames".
- Quality bar: look at your renders (Read tool, crop and upscale with numpy) and iterate until each element
  reads clearly at 1x and looks like it belongs in RCT2/TTD. Consistency across the set matters most.

## Element states to cover (where they apply)
day + night (lit windows/lamps), the 4 seasons for vegetation and ground (or at least summer + winter/snow),
construction stages, abandoned, burnt/rubble, levels 1-5 for growables, upgraded variants, animation frames.

## Workstreams and ownership (each writes only its own files)
- KIT: isokit, style bible (palette sheet, materials, lighting, scale chart with people/cars/storeys),
  terrain (grass, dirt, sand, rock, forest floor, farmland, water + animated shore/coast transitions, snow),
  nature (all tree types, growth stages, seasons, bushes, rocks, flowers), and the final mockup scenes.
  Files: tools/isokit/**, tools/iso_kit*.py, tools/iso_terrain*.py, tools/iso_nature*.py, design/iso/{style,terrain,nature,mockups}/**
- NET: every road class and connection mask, junctions, crosswalks, lane markings, medians, bridges, ramps,
  roundabouts, traffic lights/signs, bus/tram lanes, rail/tram/metro tracks, pipes, power lines/pylons,
  network buildings, zone tiles, tool/placement markers, info-view overlay styles.
  Files: tools/iso_net*.py, design/iso/net/**
- ZONED: every growable (all zone types, footprints, levels, variants), signature buildings, construction,
  abandoned, rubble. Files: tools/iso_zoned*.py, design/iso/zoned/**
- CIVIC: every service, utility, industry hub/extractor (with fields/areas), transit building, park, plaza,
  landmark, highway entry. Files: tools/iso_civic*.py, design/iso/civic/**
- LIFE: all vehicles (cars, taxis, buses, trams, trains, metro, trucks, every service vehicle, helicopters,
  boats/planes for cargo terminals), pedestrians/citizens, animals if any, FX (smoke, fire, water, lights,
  weather, pollution, construction dust), world-space status icons and selection/hover markers, cursors.
  Files: tools/iso_life*.py, design/iso/life/**
- UI: RCT2-style interface for a CS2 game: window chrome, toolbars, build menus with iso thumbnails, panels,
  info views, legends, tooltips, notifications, main menu, settings; full-screen UI mockups.
  Files: tools/iso_ui*.py, design/iso/ui/**
