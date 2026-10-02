# OpenCity isometric redesign: implementation brief

The user approved the RCT2-style redesign published in Figma and asked us to implement all of it in this game,
deciding every open question ourselves. Sources of truth:
- Art spec: `BRIEF.md`. Engine plan: `ENGINE-PLAN.md` (read it fully; it has file:line references).
- Designs: `mods/city/design/iso/<area>/` (PNG + manifest.json + INVENTORY.md per area), produced by the
  re-runnable generators `mods/city/tools/iso_*.py` on top of the shared renderer `mods/city/tools/isokit/`.
  Mockups of the target look: `design/iso/mockups/` (scenes/ and hud/).

## Decisions (final, do not re-ask)
1. Grid: option (b2) from ENGINE-PLAN: keep `MapGridType.Rectangular` cells and make only the projection isometric
   (`MapGrid: Projection: Isometric`, `TileSize: 64, 32`). CPos, WPos, orders and every synced system stay unchanged.
2. Determinism: no synced state may change. `mods/city/tools/golden.sh check` must print GOLDEN OK for full, stress
   and automayor after every change (seeded runs; baselines in `mods/city/tests/golden/`). Render code never uses
   `SharedRandom`; it uses hashes of actor id / cell.
3. Map edge: RCT2-style dark void outside the diamond with a subtle backdrop pattern. The view clamp keeps the ground
   point under the view centre inside `Bounds` (inset ~2 cells). No decorative ring (would change the sim).
4. Building facing: automatic, toward the property's access road (render-time only). No rotate key.
5. Minimap: diamond, shown in an RCT2-style "Map" window (toolbar button + hotkey), not permanently on the HUD.
6. Zoom levels: 0.5, 1, 2, 3, 4 (1.5 dropped). Default zoom 1; 2 on displays where 1 looks tiny (>= 2560 px wide
   logical viewport) via the existing ViewportDistance setting logic.
7. Info views: use NET's per-view ramps (red→green for "quality" metrics, heat ramp for intensities). UI legends
   must match NET's colours exactly.
8. HUD: the RCT2 layout from the UI designs (top toolbar with three groups, bottom status bar, floating windows with
   per-family colours). The old right sidebar is removed. Everything stays dynamic and pixel-crisp at every UI scale
   (code-drawn chrome via the CityChrome generator, pixel fonts, whole-pixel bevels), as built in the previous UI pass.
9. Night: `-lit` companion frames (lit pixels only) sorted with their owner at ZOffset +1; ambient tint split so lit
   frames stay bright (ENGINE-PLAN §3.5). Delete the pixel-scanning night-light code once replaced.
10. Tall buildings: RCT2-style 32-px strip slicing for depth (ENGINE-PLAN §3.1). Add a "see-through buildings" toggle
    (toolbar + hotkey) that draws buildings as their lot ground plus a 1-storey stub.
11. Build-menu thumbnails: isokit `thumbnail()` from the real models; towers that would shrink below tile width 32 are
    rendered at tile 32 and cropped at the top.
12. Map editor: not supported for the city mod under the iso projection (other mods unaffected).
13. Map size: unchanged (maps stay valid; terrain template ids keep their meaning).
14. Small art fixes from the design review are allowed while exporting (e.g. the snowplough shape, cyclist size,
    plane propellers, info-view legend colours, CIVIC night/winter for all facings) — keep the look consistent.

## Asset contract (exporter output)
- Every world sprite frame: RGBA, alpha 0/255, even width and height, anchored so the sprite's anchor (footprint
  ground centre for buildings and props, vehicle/person ground centre for movers, cell centre for tiles) lands on
  the actor/cell `CenterPosition`. Frames are tightly cropped; offsets go into the PngSheet `Frame[i]` metadata
  (`x,y,w,h;ox,oy`, see PngSheetLoader). Sequence-level `Offset: 0,0`.
- Anchors from the design sets are recorded three ways (PNG tEXt metadata for ZONED/CIVIC/KIT, `anchors.json` for
  LIFE vehicles, fixed canvases for NET). The exporter normalises all of them.
- Growables: frame = ((level - 1) * variants + variant) * 4 + facing; facing 0 = front toward +Y, 1 = +X, 2 = -Y,
  3 = -X (isokit `facings()` order). States (construction stages, abandoned, burnt, collapsed/rubble) are separate
  sequences with the same facing layout where applicable. `-lit` sequences mirror their day sequence frame layout.
- Vehicles and people: 8 facings clockwise from world north (N = -Y = screen up-right, E = +X = screen down-right);
  `Facings: -8`. Lane centres and sizes: NET's contract in `design/iso/net/INVENTORY.md`.
- Terrain: 64x32 diamonds, offset 0; transition/coast sets use the existing genterrain blob ids.

## Workstreams (each owns its files; tiny hooks elsewhere must be called out in the report)
- ENG: engine projection E1-E7, diamond radar, view clamp + backdrop, zoom levels, post-process pass split, mouse
  picking, ScreenMap, scissor/visible cells; placeholder rendering so the game runs in iso before the art lands.
- ART: `tools/iso_export.py` (all world art → `bits/world/iso/**`, sequences, tileset `TileSize: 64,32`, terrain.png),
  frame mapping in `RoadLayer.Render.cs` and the TerrainSpriteLayer overlays (zones, utilities, tracks, snow, flood,
  extractor areas), `genmap.py` shim + diamond map previews, retiring the old top-down generators.
- BLD: buildings/props rendering (`WithIsoSprite` slicing, facings, polygon mouse bounds, `WithGrowableSprite`
  states), status icons, InfoViewLayer, TileBorderOverlay, tool ghosts/labels/markers, night/emissive + ambient,
  seasons, see-through toggle, `rules/defaults.yaml`.
- MOV: TrafficSim render (8 facings, NET lanes, scale 1.0, pixel-snapped movement, at most the visually fitting number
  of cars per lane cell — render-only), pedestrians, parked cars, transit/rail render, service effects, helicopters
  with real height, weather/smoke/fire FX, `sequences/vehicles.yaml`, `sequences/transit.yaml`.
- UI: RCT2 HUD and windows per `design/iso/ui/`, icons and build thumbnails at runtime, info-view legends, Map window
  with ENG's diamond radar, main menu/settings restyle, loading screen.

## Gates (every workstream, before its final commit)
`make all` (0 warnings), `make check`, `make tests`, `./utility.sh city --check-yaml` (0 errors, 0 warnings; also ra,
cnc, d2k, ts if shared engine code changed), `mods/city/tools/golden.sh check` (all GOLDEN OK), stress perf
>= 100 ticks/s (`[autotest-perf` lines), headless screenshots reviewed at 1280x720, 1920x1080 and 2560x1440, zoom
1 and 2, UI scale 1 and 1.5. Never open a visible window (the user is using this machine); never `git stash`; never push.
