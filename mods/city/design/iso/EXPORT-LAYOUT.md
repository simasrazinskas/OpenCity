# Iso export: frame layouts (contract between ART and BLD / MOV / UI)

Produced by `mods/city/tools/iso_export.py` (helpers: `iso_export_lib.py`, one `iso_export_<area>.py` per area).
Sheets live in `mods/city/bits/world/iso/<area>/`; sequence files are generated (do not edit by hand).

## Every frame
- PngSheet with `Frame[i] = x,y,w,h;ox,oy`: tightly cropped, even w/h, alpha 0/255, `ox = w/2 - ax`, `oy = h/2 - ay`.
- The anchor (ax, ay) is the footprint ground centre (buildings, props), the ground centre (movers), or the cell
  centre (tiles and overlays). So `SpriteRenderable(pos = CenterPosition)` with sequence `Offset: 0,0` is correct.
- Identical frames share one region of the sheet (dedupe), so frame counts stay regular even where art repeats.

## Facing index (buildings, props)
`f` = 0 front toward +Y (screen lower-left), 1 toward +X (lower-right), 2 toward -Y (upper-right), 3 toward -X
(upper-left). This is isokit `facings()` order. A W x D actor can use all four facings: f 0/2 show the W x D model,
f 1/3 show the D x W model turned, so the footprint always matches the actor.

## Growables (`sequences/buildings.yaml`, images `<zone>-<W>x<D>`)
| sequence | frames | index |
|---|---|---|
| `idle` | 80 | `((level-1) * 4 + variant) * 4 + f` (variants 0-1 North American, 2-3 European) |
| `idle-lit` | 80 | same layout, lit (emissive) pixels only |
| `winter` | 80 | same layout, snow |
| `abandoned`, `burnt` | 80 | same layout |
| `construction` | 12 | `(stage-1) * 4 + f`, stage 1 foundation, 2 frame + crane, 3 nearly done |
Signature buildings (`sig-*`, 3x3, level 5): `idle`, `idle-lit`, `winter`, `abandoned`, `burnt` = `theme * 4 + f`
(theme 0 NA, 1 EU); `construction` as growables. Legacy 1x1 images (`res-low-1` ...) keep 1 level x 4 variants:
`idle` = `variant * 4 + f`.
Legacy images also have `idle-lit` (16 frames, same layout; only when the design has lit pixels). Legacy level per
actor number: -1/-2/-3 = level 1/3/5 of the 1x1 design; res-high-N / com-high-N reuse res-low / com-low 1x1 at levels 3/4/5.
Shared images kept for the current rules: `zon-construction.s<W>x<D>` (11 footprints, 12 frames each, `(stage - 1) * 4 + f`),
`zon-rubble.s<W>x<D>` (4 frames, `f`) and `construction.small/large` (12 frames each, `(stage - 1) * 4 + f`; res-low 1x1 /
res-med 2x2 sites). Stage index is 0-based here: stage 1..3 -> 0..2. Lot-actor and sig `construction` show level 3
(sig: level 5) variant 0; zon-construction families: 1x1/2x1/1x2 res-low, 3x1/1x3 res-row, 2x2/3x2/2x3/3x3 res-med, 4x3/3x4 com-high.
Sheets are `iso/zoned/<image>-<sequence>.png` (one PNG per image + sequence, 2048 px wide, never taller than ~800 px except tall
towers); `zon-construction`, `zon-rubble`, `construction` are one sheet per image with `Start` offsets.

## Other buildings (services, industry hubs, transit buildings, network buildings, parks)
| sequence | frames | index |
|---|---|---|
| `idle` | 4 x N | `anim * 4 + f` (N = animation frames, 1 when static; `Tick` kept) |
| `idle-lit` | 4 x N | same layout; present only when the building has lit pixels |
| `winter` | 4 x N | same layout |
`Length` is the full frame count; `Facings` is not declared (the render trait picks `anim * 4 + f`).

## Vehicles and people (MOV; sheets in `bits/world/iso/life/`)
8 facings clockwise from world north (N = -Y = screen up-right, NE, E = +X = screen down-right, SE, S, SW, W, NW),
`Facings: -8`. Animated sequences are facing-major: frame = `facing * Length + anim`. Anchor = ground centre
(vehicles), feet (people). The exact paste-ready content of `vehicles.yaml` + `transit.yaml` is
`design/iso/life-sequences.yaml`; per-file frames, layouts and anchors: `bits/world/iso/life/README` (both written by
`iso_export_life.py`). Sheets: `iso/life/vehicles/<model>.png`, `iso/life/people/<image>.png`, `iso/life/fx/*.png`.
- Images keep today's names (car-a..d, truck, van, bus, taxi, tram, train, traincar, garbage, ambulance, firetruck,
  policecar, hearse, pedestrian) and LIFE models are added under their model names (hatchback, sedan, estate, suv, mpv,
  pickup, sports, artic-bus-front/rear, delivery-van, box-truck, tanker-truck, tipper-truck, timber-truck,
  semi-tractor, trailer-*, farm-tractor, postal-van, maintenance, snowplough, wreck, tram-cab/middle/rear, train-loco,
  train-coach, wagon-*, metro-cab/car, heli-medevac, heli-fire, cargo-plane, motorboat, ferry, cargo-ship).
- Sequences per vehicle image: `idle` (Length 1; heli rotor / plane propeller: Length 4 / 3), `idle-lit` (lit lamps,
  same canvas, draw above at night), `beam` (road beam, additive), `siren` / `siren-lit` (Length 2), `beacon` (2) /
  `beacon-lit` (1), `shadow` (aircraft, ground shadow; aircraft bodies are ground anchored, MOV adds the height).
  Cars and delivery-van also have one sequence per body colour (red blue white black silver green yellow teal brown
  orange purple beige; `idle` = red).
- People (4 drawn directions, the diagonal facings reuse N/E/S/W): `pedestrian` (`idle` legacy 8 frames = 4 colours x
  2 steps; `walk0`..`walk15` Length 4), cyclist, jogger, shopper, worker (`walk`, `hammer`), umbrella, waiting, family,
  dogwalker, dog (3 coats); pigeon and gull (`peck-r/l`, `fly-r/l`, no facings); bench (`x`, `y`, `sit-*`, `sit2-*`).
- FX sheets (no sequences yet): smoke, steam, fire stages, hoses, splash, dust, sparks, fireworks, weather, pollution,
  flood and night glows in `iso/life/fx/` (anchors and suggested Tick in the README).
- `sequences/misc.yaml`: `envicons.tiles` 8 frames (ProblemTier order), `glyphs` 24 (CityProblem - 1), same anchor
  (draw both at the same centre); `svc-fire` (`idle` = stage 2, `stage1`-`stage4`, `heap`), `svc-pile` small / medium /
  large, `svc-heli` (`idle` 4 rotor frames, `fly` / `fly-fire` 8 facings x 4, body lifted 24 px with its shadow).

## Networks and overlays (TerrainSpriteLayer, one frame per cell)
Mask bits everywhere: 1 N (-Y, up-right edge), 2 E (+X, down-right), 4 S (+Y, down-left), 8 W (-X, up-left).
The per-sequence frame tables are in the headers of `sequences/networks.yaml` and `sequences/overlays.yaml`.
Roads (`sequences/networks.yaml`, image `roadnet`; full tables in its header). Per-class sequences are named
`<prefix>-<RoadType.Sequence>`; overlays are transparent except where they paint:
| sequence | frames | index |
|---|---|---|
| `<class>` (street gravel avenue boulevard highway alley) | 96 | `wear * 32 + oneWay * 16 + arms`, wear block 0 used / 1 new / 2 worn (cell hash) |
| `transition` | 200 | straight cell between two classes: `(sa * 10 + sb) * 2 + axis`, section = class * 2 + oneWay |
| `bridge-<class>` (street alley avenue boulevard highway; 64x80, anchor 32,64) | 16 | `((ew * 2 + oneWay) * 4 + piece)`, piece 0 end0, 1 span, 2 pier, 3 end1 |
| `median-<class>` (avenue boulevard highway) | 16 | sideBlock mask |
| `lanes-<class>` (street avenue boulevard) | 96 | `(oneWay * 3 + combo - 1) * 16 + arms` (combo bit0 bus, bit1 bike) |
| `strips-<class>` (street gravel alley avenue) | 240 | `(combo - 1) * 16 + (~arms & 15)`; only parking (combo bit 3) paints |
| `junction-<class>` (street alley avenue boulevard) | 128 | `(oneWay * 4 + kind) * 16 + arms`, kind 0 stop 1 signal 2 yield 3 roundabout |
| `ring-<class>` | 256 | roundabout ring cell: `entries * 16 + arms` (yield teeth on entry arms) |
| `arrows`, `arrows-<class>` | 4 | N, E, S, W (`arrows` also used by the road tool preview) |
| `control` | 14 | 0-3 highway ramp N,E,S,W; 4 island 1x1; 5-13 island 3x3 `5 + row * 3 + col` |
| `edge` | 4 | map-edge highway fade and arrows (N, E, S, W) |

Tall road props (image `roadprops`, layout in the same header) are separate depth-sorted sprites anchored at the foot of
the prop. `RoadLayer.GetProps(CPos cell, List<RoadProp> props)` lists what stands on a cell (`RoadProp`: `Sequence`,
`LitSequence`, `Frame`, `Offset` from the cell centre in world units, `ZOffset`, `NightOnly`, `Seasonal`, `SignalArm`);
`RoadLayer.PropsChanged(CPos)` fires when a cell was redrawn and `PropsVersion` counts redraws. Side props (lamps, trees,
barriers, guard rails) stand at +-448 world units towards their side, signals and signs at the +-384 corner of the arm they
serve; bridge props are lifted by 256 (8 px). Sequences: `lamp`, `lamp-lit`, `lamp-pool` (night ground decal),
`lamp-heritage`, `lamp-double`, `tree` / `median-tree` / `median-tree-c` (+ `-spring|-autumn|-winter`),
`signal-red|amber|green` (+ `-lit`), `sign-stop`, `sign-yield`, `barrier`, `guardrail`, `gantry`.

## Terrain (`tilesets/temperate.yaml`, `bits/terrain/terrain.png`, `sequences/environment.yaml`)
`TileSize: 64, 32`; template ids, sizes, categories and terrain types are unchanged (existing maps stay valid). Frame
`i` of terrain.png (64x32 diamonds, offset 0, FrameSize grid) is entry `i` of `iso_terrain_ids.templates()` (grass,
meadow, rough, dirt, sand, water 100..107, beach land 200+k, shore water 400+k; blob masks as in genterrain, now diamond
edges). Water sits 5 px below land level. `envwater.water`: animated water, frame = `water_index * 8 + f`
(100..107 -> 0..7, 400+k -> 8+k), drawn by `CityWaterAnimation` at ~5 fps; `envsnow.snow` winter version of every land template: frame = `2 * terrain frame + (0 full, 1 light)`, water templates empty;
`envflood.flood` 4 frames. Id scheme API: `tools/iso_terrain_ids.py` (genmap imports it).

## Wiring still owed by consumers (ART exports are complete; these switch the render code over)
- BLD: growables `((level-1)*4 + variant)*4 + f` and the state sequences (`construction` `(stage-1)*4 + f`, `abandoned`,
  `burnt`, `winter`, `idle-lit`); legacy 1x1 images `variant*4 + f`; other buildings `anim*4 + f` (CIVIC extras:
  `inactive`, `burnt` = `f`, `construction` = `(stage-1)*4 + f`, landfill/cemetery `fill` = `level*4 + f`, rubble
  `variant*4 + f`; non-square CIVIC models repeat f0/f2 for f1/f3); trees (variant hash, season sequence, drop or soften
  `WithSeasonTint`); props from `RoadLayer.GetProps`, `UtilityOverlay.GetProps`, `TrackProps.Rail` (signal state,
  season suffix, `-lit` at night, `lamp-pool` night-only); info views from `overlays.ramp-*`; tool ghosts from the
  `overlays` markers; status icon bubble gap (`envicons` tile and glyph share one anchor).
- MOV: paste `design/iso/life-sequences.yaml` into `sequences/vehicles.yaml` / `transit.yaml` (8 facings, `Facings: -8`);
  `svc-heli.fly` 8 facings x 4; fire stages; `transit-stop` frames 8-15 (tram, metro); FX sheets in `iso/life/fx`.
- Lobby `map.png` previews stay top-down: map.png is hashed into the map UID (format 12), which seeds fish stocks and
  identifies replays. `genmap.py --previews-only` renders the diamond previews for a deliberate golden re-baseline.
