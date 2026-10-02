# OpenCity isometric rendering: implementation plan

## 0. Findings that shape the plan

- **OpenRA's world-to-screen projection is axis-aligned.** In `OpenRA.Game/Graphics/WorldRenderer.cs:402-471`, world X maps to screen x and world (Y−Z) maps to screen y. The TS mod looks isometric for a different reason: it rotates cells inside world space (`Map.CenterOfCell`, `OpenRA.Game/Map/Map.cs:973`) and staggers MPos (`CPos.cs:75-84`, `MPos.cs:45-61`).
- **The city simulation works purely in cell space and assumes CPos == MPos with a 0-based rectangle.**
  - About 20 files index flat arrays as `cell.Y * width + cell.X`, with `width = MapSize.Width`. Examples: `TrafficSim.cs:187-205`, `ServiceSimulation.cs:76,332`, `LandValueLayer.cs:100-121`, `CityCoverageLayer.cs:68-117`, `PollutionLayer.cs:94,118`, `CityDisasters.cs:79-160`, `Logistics.Routes.cs:46-53`.
  - Other code treats `Map.Bounds` as a CPos rectangle: `ConstructionUtils.cs:234-237`, `ZoningTool.cs:61-63`, `Progression.Tiles.cs:52-75`, `RailLayer.cs:122-169`, `TransitLayer.Tracks.cs:216-221`, `NaturalResourceLayer.cs:281-294`, `CityAutoTest.cs:130,626-628`.
- **No synced city code reads world positions (WPos).** Only render code uses `CenterOfCell`, `.Yaw` and `CenterPosition`. Orders carry cells (`OpenRA.Game/Network/Order.cs:148-155, 424-428`).
- **The city's zoom settings are currently switched off.** Commit 8576851fea (a font change) removed the `WorldViewportSizes` block (`ZoomLevels: 0.5, 1, 1.5, 2, 3, 4`, `KeepViewInsideMap`) and the `CursorScaling` block from `mods/city/mod.yaml`. The engine code for snapped zoom is present, but the city mod no longer turns it on. This is fixed in step P0 below.

## 1. Grid strategy

| | (a) `RectangularIsometric` (TS) | (b1) New `Diamond` grid type: MPos==CPos, cells rotated in world space | **(b2) Keep the `Rectangular` grid, make only the projection isometric** | (c) Rectangular-isometric map with a diamond-shaped city inside |
|---|---|---|---|---|
| Map on screen | Screen rectangle; the city area is a CPos diamond with jagged edges | Diamond (RCT2) | Diamond (RCT2) | Screen rectangle with a diamond city and decorative corners |
| Engine changes | None (TS-proven: radar, viewport, editor) | `Map.cs` (`CenterOfCell` 973, `CellContaining` 1053, `Offset` 1038, `Contains` 913, `SetBounds` 1135), `MapGrid` ramp corners, every `Grid.Type ==` switch, plus the viewport, ScreenMap and radar work | About 400 lines behind a flag: WorldRenderer projection, Viewport, ScreenMap, radar, one post-process pass | None |
| City code that breaks | Every flat array (CPos y goes negative; x reaches W+H; the X<Y rule in `CellLayer.cs:107-112`, `Map.cs:913-931`), the 9×9 tile grid, `ClampToBounds`, edge connections, every cell-aligned WVec in render code, all maps, autotest coordinates and golden logs | Render-side WVec math in about 8 files must be rotated. Every actor's WPos changes. Diagonal distances shift slightly (724·√2 ≠ 1024) | No simulation code. Render-side WVec math stays valid because the world stays top-down | Same churn as (a), plus an "is this a city cell" check everywhere |
| Risk | Very high: touches every work package's simulation | Medium | Low to medium | High |

**Recommendation: (b2).** The world stays top-down, with 1024 world units per cell. Only `WorldRenderer` changes how a world position becomes a screen position:

```
sx = (X − Y)·32/1024 + originX     (originX = MapSize.Height·32, so screen x is never negative)
sy = (X + Y)·16/1024 − Z·32/1024
```

This is exactly the brief's formula. Square cell corners (`MapGrid.cs` CellRamp corners for the Rectangular grid) project to 64×32 diamonds automatically. That means `TerrainSpriteLayer` (`TerrainSpriteLayer.cs:100-108`), `InfoViewLayer`, `CityTileMarkerRenderable` and mouse picking (`Viewport.ViewToWorld`) need no geometry changes.

### Engine work items for (b2)

- **E1 `MapGrid.cs`:** add `Projection: TopDown|Isometric`. In the city mod, set `MapGrid: Projection: Isometric` (`mods/city/mod.yaml:244`) and `TileSize: 64, 32` (`tilesets/temperate.yaml:4`).
- **E2 `WorldRenderer.cs`:**
  - Projection functions at 402-471: `ScreenPosition`, `Screen3DPosition` (depth = ground sy), `ScreenVectorComponents`, and the inverse `ProjectedPosition`.
  - Sort key at 25-26: change to `Pos.X + Pos.Y + Pos.Z + ZOffset`.
  - The sort key is a static field also used by `EditorActorBrush.cs:129`, `ActorPreviewPlaceBuildingPreview.cs:86` and `Carryall.cs:288`, so it needs to become projection-aware.
- **E3 `Viewport.cs`:** these all assume the top-left and bottom-right screen corners are enough, which gives a zero-width box for a diamond. Each must use all four corners:
  - map bounds (284-289);
  - `CalculateVisibleCells` (564-588);
  - `GetScissorBounds` (547-562), which also needs extra top margin for tall sprites on the back rows;
  - `CenterRange`/`ClampCenter` and `GetBlockedDirections` (409-430, 248-260), which need a diamond-aware clamp.
  - Then verify `CandidateMouseoverCells` (475-497).
- **E4 `ScreenMap.cs:62-67`:** size the spatial partition from the projected extents. Today's `MapSize × TileSize` is too small for non-square maps such as the 64×48 shellmap.
- **E5 Post-process pass:** add `PostProcessPassType.AfterTerrain` (`TraitsInterfaces.cs:468`), call it in `WorldRenderer.Draw` after line 289, and make the pass of `TintPostProcessEffect.cs:37` configurable (needed for §3.5).
- **E6 Radar:** `RadarWidget.cs:400-413` draws an axis-aligned viewport box. Replace it with a city diamond radar (preferred) or at least draw the box as a 4-corner polygon.
- **E7 `ViewportControllerWidget.cs:444-456`:** fix the map-edge jump hotkeys. The map editor viewport (`Viewport.cs:270-283`) stays out of scope.

## 2. Rectangular-grid assumptions in `OpenRA.Mods.City`

**Simulation (no change under b2).** All of the flat-array and Bounds files listed in §0 stay as they are, as do:
- `CityTypes.cs:199` `Neighbours4` (N = −Y, which now appears as screen up-right);
- the `AllCells` loops (InfoViewLayer, ZoneLayer, NaturalResourceLayer and others);
- `MPos→CPos` conversions (`ZoningTool.cs:62`, `ConstructionUtils.cs:235`, `RailLayer.cs:169`, `TransitLayer.Tracks.cs:221`, `NaturalResourceLayer.cs:294`).

Under option (a), every one of these would need a `CityGrid` indexer.

**Render and UI code:**

| Hit | Assumption | Change |
|---|---|---|
| `TrafficSim.Render.cs:82-86` | Visible region's U/V used as cell x/y | Still valid (MPos==CPos). E3 makes the region the cell bounding box of the rotated view |
| `TrafficSim.Render.cs:126-128, 170-201`, `:204-220` | 32 facings; cell-aligned Bezier curves and lane offsets | Offsets stay valid. Switch to 8 facings. Snap progress along the lane to 64-world-unit steps so diagonal motion moves exactly 2 px across per 1 px down |
| `TrafficSim.Extras.cs:35-61` (parked cars ±410), `:84-99` (pedestrians ±450) | Cell-aligned | Still valid. New pedestrian frames (8 directions plus a walk cycle) |
| `TransitRender.cs:65-85, 155, 190-192`; `TransitLayer.Rail.cs:59-86` | Interpolation between cell centres; lane via `Rotate` | Still valid. Use 8 facings and drop ZOffset 2/4 |
| `ServiceEffects.cs:100-152` | ZOffset 1536/2048/4096 forces sprites on top; helicopter flies at Z=0 | Fire and piles get a small ZOffset; the helicopter gets real Z height |
| `WithCityStatusIcons.cs:128` | "Roof" = `CenterPosition − Y`, assuming that is screen-up | Anchor the icons to the top edge of the actor's screen bounds |
| `InfoViewLayer.cs:113-139` | Ground fill drawn after actors | Move the ground fill to the overlay pass (before actors), and tint buildings with an IRenderModifier (CS2 style) |
| `InfoViewLayer.cs:479`, `PlaceCityBuildingOrderGenerator.cs:193`, `CityDragOrderGenerator.cs:218` | ±512 in Y means above/below the cell | Use the diamond's top corner `−(512,512)` and bottom corner `+(512,512)` |
| `TileBorderOverlay.cs:73-115` | 2-point `FillRect` | Use the 4-corner `FillRect` |
| `CityTileMarkerRenderable.cs:33` (`Pos = WPos.Zero`) | Markers are drawn first, so tall buildings hide them | Also outline hovered/target cells in the annotation pass |
| `CityDragOrderGenerator.cs:195`, bulldoze and inspect tools | Hovering picks the cell under the cursor, which is behind a tall façade | Hit-test actor mouse bounds first, then fall back to the cell |
| `PlaceCityBuildingOrderGenerator.cs:162-176` | Ghost uses frame 0 | Show the facing that will be used |
| `WithGrowableSprite.cs:115-133, 163-166` | Frame = level × variant; rectangular mouse bounds | Frame = (level × variant)×4 + facing. Polygon mouse bounds (footprint diamond plus height) |
| `CityNightLights.cs:76-200`, `CityNightLights.Windows.cs` | Scans sprite pixels for window colours; glows drawn after tint with no occlusion | Authored emissive frames (§3.5) |
| `RoadLayer.Render.cs:96-107, 158-200`, plus ZoneOverlay, UtilityOverlay, TrackOverlay, CitySnowOverlay, CityFloodOverlay, ExtractorAreaRenderer | Per-cell `TerrainSpriteLayer` frames | Art swap only. Tall props (signals, lamps, pylons, catenary, railings) become sorted renderables |
| `sequences/buildings.yaml:2-4` (`Offset: 0,-16`), `vehicles.yaml:1-5` (`Facings: -32`) | Top-down frames | Regenerate (§3.3) |
| `rules/defaults.yaml:27-45` HitShape | World-space rectangle | No change |
| `chrome/ingame-player.yaml:162` radar | Square radar | E6 |
| `CityAutoTest.cs:112-114` (`mouse=` pixel probes), `289`, `472` | Screen-pixel probes | Pick new pixel coordinates. Scenario cells are unchanged |

## 3. Rendering

### 3.1 Depth sorting

**How OpenRA and TS do it today.** OpenRA sorts world renderables with a stable sort on `Pos.Y + Pos.Z + ZOffset` (`WorldRenderer.cs:160-168`). TS adds a depth buffer on top:
- `EnableDepthBuffer: True` (`mods/ts/mod.yaml:107`);
- one generic `DepthSprite: isodepth.shp` per sequence (`structures.yaml:7`);
- 3D sequence offsets (`Offset: 0,-36,36`);
- `ZOffset: 1023` for bright overlays (`structures.yaml:30`).

**Why TS's depth buffer doesn't carry over.**
- The depth range only covers `MaximumTerrainHeight` (`Game.cs:215-217`). On a flat map that is 0, which turns per-pixel depth off.
- `DefaultSpriteSequence` supports only one depth frame per sequence (`DefaultSpriteSequence.cs:377-381, 472-499`).
- With depth on, the world buffer loses its 2× surface-size cap (`Renderer.cs:226-239`). At zoom 0.5 on a 4K screen it would be 8192².

**The pitfall.** A tall multi-cell sprite sorted by its centre puts a car standing behind its far end *in front* of it. Example: a 4×1 building and a car up-right of its last cell.

**Recommendation: RCT2-style strip slicing.** Add a city render trait `WithIsoSprite`:
- Split each building frame into vertical screen strips 32 px wide, aligned to the footprint's corners. A W×D building gives W+D strips.
- Each strip sorts at the frontmost ground point of the footprint inside that column, minus 1, so a mover in the cell directly in front wins ties.
- Cache the sub-sprites per (frame, footprint).
- Vehicles, people, trees and props are single sprites sorted at their ground position.
- Drop shadows become separate ground-pass frames, not baked into buildings.
- Fallback if artefacts remain: depth buffer with per-pixel depth from isokit. That needs a city sequence loader with per-frame depth and a `DepthMargin` override, and is not planned.

### 3.2 Terrain

- Tiles are exactly 64×32 diamonds with offset 0. `TerrainSpriteLayer` places them with no changes.
- Transitions keep today's 1×1 blob scheme (genterrain ids: 4-neighbour mask plus diagonal corner bits). The mask bits now mean diamond edges:

| Mask bit | World direction | Diamond edge on screen |
|---|---|---|
| N | −Y | up-right |
| E | +X | down-right |
| S | +Y | down-left |
| W | −X | up-left |

- Animated water: a city overlay `TerrainSpriteLayer` that swaps frames for visible water cells at 4–6 fps. It can stay static in the first pass.
- Snow and seasons use the existing overlay traits with diamond masks.

### 3.3 Anchors and offsets

**How OpenRA places a sprite.** The brief's anchor (footprint ground centre) is `Building.CenterPosition` (`Building.cs:153-157, 297`). `SpriteRenderable` draws at `Screen3DPxPosition(pos) − (int)(size/2) + sprite.Offset` (`SpriteRenderable.cs:93-96`).

**What the exporter (`tools/iso_export.py`) writes.**
- Tightly cropped frames with `Frame[i] = "x,y,w,h;ox,oy"` PNG metadata (`PngSheetLoader.cs:100-110`).
- Offsets `ox = w/2 − ax` and `oy = h/2 − ay`, where (ax, ay) is the anchor pixel.
- Even frame sizes, so offsets are whole pixels and nothing lands on a half pixel.

**Sequence settings.**
- Sequence-level `Offset` becomes 0,0 (drop `^Growable` `Offset: 0,-16`).
- Keep `ZOffset` only for deliberate layering (fire, cranes, lit layer = building + 1).
- Vehicles anchor at their ground centre; overlay tiles at the cell centre.

### 3.4 Facings

**Buildings (4 facings).**
- The facing is chosen at render time from the property's `AccessRoad` relative to its footprint. No simulation change.
- Non-square lots already exist as two actors, W×D and D×W (`rules/growables.yaml` header). Each one uses the 2 facings that fit its footprint.
- Player-placed services face the adjacent road.
- A player rotate key would be a simulation change (new order field, swapped footprint). Defer it.

**Vehicles (8 facings).**
- Use `Facings: -8`, frames clockwise from world north. World north (−Y) is screen up-right; world east (+X) is screen down-right.
- This mapping is the one contract the LIFE art and the code must share.

### 3.5 Night and emissive

**Today.** `CityAtmosphere` drives the tint post-process at the `AfterActors` pass. `CityNightLights` then draws glows on top with no occlusion, which will look worse in iso where buildings overlap.

**Plan.**
- isokit exports `<image>-lit` companion frames: same regions and offsets, lit pixels only, alpha 0/255.
- They are sliced and sorted with their building at ZOffset +1, so buildings and cars in front hide them correctly.
- To keep lit frames bright:
  - Move the post-process tint to the new `AfterTerrain` pass (E5). Ground, roads and overlays stay tinted as now.
  - Multiply sorted sprites by the same ambient colour in render code: an IRenderModifier (the `WithSeasonTint.cs` pattern) for actors, and the existing tint argument in TrafficSim, Transit and ServiceEffects renderables.
  - `-lit` frames skip this multiply.
- Street lamps become prop renderables with lit heads.
- Delete the pixel scanner in `CityNightLights.Windows.cs`.

### 3.6 Zoom, pixel snapping, KeepViewInsideMap

- **Restore the dropped config.** Put the `WorldViewportSizes` and `CursorScaling` blocks back in `mods/city/mod.yaml`.
- **Snapping is projection-agnostic.** `BeginWorld` (`Renderer.cs:265-320`) still renders at 1:1 and snaps sub-pixel scroll.
- **Drop zoom level 1.5.** 2:1 stair edges are only regular at whole zooms; at 1.5 they alternate 1- and 2-pixel steps. Use `ZoomLevels: 0.5, 1, 2, 3, 4`.
- **Replace `KeepViewInsideMap`.** The current clamp (`Viewport.cs:409-430`) keeps the view inside a screen rectangle, which doesn't work for a diamond. Instead, clamp the ground point under the view centre to `Bounds` (inset a few cells), and draw a backdrop outside the diamond.
- **Pixel rules for art and code.** Even frame sizes and the mover position snapping from §2.

## 4. Maps

**How they're generated today.**
- `genmap.py` works in cell space: neighbour masks at 21-22, `map.bin` written column-major at 361-372, `Bounds: 1,1,W-2,H-2` at 374.
- It takes template ids from the `genterrain.py` header (1–22 land, 100–107 water, 200+/400+ shore).
- The lobby preview `map.png` is drawn top-down (406-434, `LockPreview: True`).

**Under (b2), existing maps stay valid as long as the terrain template ids keep their meaning.**
- `iso_terrain*.py` must write the same ids into `tilesets/temperate.yaml` (with `TileSize: 64, 32`) and `bits/terrain/terrain.png`.
- Give `genmap.py` a small shim so it imports the ids, `MOD` and `hash01` from the new module.
- Optional: a diamond `map.png` preview.
- `genworld.py` and the `gen_*.py` top-down sprite generators retire once `iso_export.py` exists. Build icons move to the UI work.

**On-screen size doubles.** A 130×130 map becomes 8320×4160 px at 1× (it is 4160×4160 today).

**Changes to avoid in the same commit.** A decorative ring outside `Bounds` (MapSize larger than Bounds) changes the `AllCells` loops, so it changes the simulation. It needs its own re-baselined commit.

**Under (a)**, `genmap.py` would need a rewrite: staggered MPos, rotated actor coordinates, highways on diagonal edges, MapSize W×2H.

## 5. Determinism

- **(b2) changes no synced code.** WPos, CPos, `TileScale` (1024) and order serialisation are all unchanged. Render traits only read simulation state (AccessRoad, CityClock) and use hashes, never `SharedRandom` or the camera.
- **The `full` scenario's coordinates do not change.**
  - `At(u,v) = anchor + CVec` (`CityAutoTest.cs:478`).
  - The anchor comes from the highway (`CityAutoTest.cs:127-130`).
  - They would change only under (a). Under (b1) WPos values change, so goldens must be re-recorded.
- **Gate every merge on the golden logs.**
  1. In P0, record the `full`, `stress` and `automayor` autotest logs (StateHash lines) plus a replay.
  2. After each merge, rerun the scenarios and replay the pre-switch replay with `autotest.sh replay:<file>`.
  3. Diff the logs; they must be identical apart from timing and fps lines.
- **These changes break identity and need a deliberate re-baseline in their own commit:**
  - map regeneration, an outside ring, or a `Bounds` change;
  - building rotation as a simulation feature;
  - footprint changes driven by the art;
  - reordering `Neighbours4`;
  - simulation code reading art metadata such as building heights.

## 6. Phases and agents

| Phase | Work | Owner | Effort | Exit criteria |
|---|---|---|---|---|
| P0 | Restore WorldViewportSizes/CursorScaling, record golden logs, settle the open questions | QA | 0.5–1 d | Baselines stored |
| P1 | E1–E7 behind the flag. Placeholder diamond terrain, old sprites drawn upright | ENG + ART | 5–7 d | Playable iso; picking, scissor, clamp, radar work; goldens identical |
| P2 | Terrain, roads, zones, overlays and the exporter (ART). Slicing, facings, mouse bounds, labels, info views, tile borders (BLD). Vehicles, people, transit, effects (MOV) | ART, BLD, MOV in parallel | 1.5–2 wk | All world art in iso; goldens identical |
| P3 | Emissive and ambient split, seasons, construction/abandoned/rubble, backdrop, zoom polish, performance | BLD, ENG, ART | ~1 wk | Night looks right; `stress` ≥ 100 ticks/s; FPS checked at 0.5× |
| P4 | Screenshot sweep at 1× and 2×, replay checks, docs (`ARCHITECTURE.md` §1.10 art rules) | QA | 3–4 d | Sign-off |

Rough total is 3–4 calendar weeks after the designs are approved. For 3 agents, merge MOV into BLD and QA into ENG.

**File ownership:**
- **ENG:**
  - Engine: `OpenRA.Game/Graphics/{WorldRenderer,Viewport}.cs`, `Map/MapGrid.cs`, `Traits/World/ScreenMap.cs`, `Traits/TraitsInterfaces.cs`.
  - Mods.Common: `TintPostProcessEffect.cs`, `ViewportControllerWidget.cs`.
  - City: new `Widgets/CityRadarWidget.cs`; the `MapGrid` and `WorldViewportSizes` blocks of `mod.yaml`.
- **ART:**
  - Tools: `tools/iso_export.py`, the `genmap.py` shim.
  - Mod files: `tilesets/**`, `sequences/**`, `bits/world/iso/**`.
  - Code: frame mapping in `RoadLayer.Render.cs` and the six overlay traits.
- **BLD:**
  - City code: new `Traits/Render/**` (`WithIsoSprite`, facing, mouse bounds, ambient), `WithGrowableSprite.cs`, `CityNightLights*.cs`, `CityAtmosphere.cs`, `WithCityStatusIcons.cs`, `InfoViewLayer.cs`, `TileBorderOverlay.cs`, `Orders/*` labels and ghosts, `CityTileMarkerRenderable.cs`.
  - Rules: `rules/defaults.yaml`.
- **MOV:**
  - Render files: `TrafficSim.Render.cs`, `TrafficSim.Extras.cs`, `TransitRender.cs`, `TransitLayer.Rail.cs` (`GetTrainPoses` only), `ServiceEffects.cs`, `CityWeatherFx.cs`.
  - Sequences: `vehicles.yaml`, `transit.yaml`.
- **QA:** `tools/autotest.sh` (golden diff mode), `CityAutoTest.cs` (shots and probes), `genmap.py` preview, docs.
- **Radar chrome:** swapping the radar in `chrome/ingame-player.yaml` touches the UI work package's file, so ENG coordinates with the UI owner.

## 7. Risks and open questions

1. **Diamond map edges.** OK to show RCT2-style black void (or a texture) at the corners, or do you want a decorative outside ring? The ring is a simulation change and needs new baselines.
2. **Map sizes.** The iso map is twice as wide on screen. Keep 130×130?
3. **Building rotation.** Is automatic facing toward the road enough, or do you want a player rotate key (a simulation and order change)?
4. **Tall buildings hiding things.** Add an RCT2-style see-through or footprint-only toggle while a tool is active?
5. **Info views.** Tint buildings (CS2 style) or keep ground-only overlays?
6. **Zoom.** OK to drop the 1.5× level?
7. **Minimap.** Diamond (RCT2) or square with a rotated viewport box?
8. **Map editor.** Not supported under the new projection in this pass. Acceptable?
9. **Performance.** Slicing multiplies building renderables by W+D, and terrain has twice as many pixels. Measure FPS at 0.5× on 130×130 with 50k population.
10. **Engine assumptions.** Mods.Common code that assumes "screen-up = −Y" (map-edge hotkeys, editor previews) may misbehave. City code avoids it.
11. **Upstream divergence.** The engine fork grows by about 400 lines, all behind the flag.
12. **Emissive approach.** The ambient-tint split (§3.5) needs every world sprite path to apply the ambient colour. If one is missed, that sprite won't darken at night. Prototype it in P1.

### Critical Files for Implementation
- /home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/OpenRA.Game/Graphics/WorldRenderer.cs
- /home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/OpenRA.Game/Graphics/Viewport.cs
- /home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/OpenRA.Mods.City/Traits/Growth/WithGrowableSprite.cs
- /home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/OpenRA.Mods.City/Traits/Traffic/TrafficSim.Render.cs
- /home/simasrazi/.t3/worktrees/OpenRA/t3code-dd3c9b2c/mods/city/mod.yaml
