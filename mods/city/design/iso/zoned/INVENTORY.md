# ZONED inventory (iso redesign, design phase)

Sources: `rules/growables.yaml` (92 actors), `rules/zoning.yaml`, `sequences/buildings.yaml`, `CityTypes.cs` (ZoneType),
`Traits/Buildings/GrowableBuilding*.cs`, `Traits/Growth/{WithGrowableSprite,Rubble,LotCatalog,SignatureBuilding}.cs`,
`tools/gen_zon*.py`, `design/04-zoning-buildings.md`.

## Facts that shape the art
- One actor per zone x footprint (`<prefix>-<W>x<D>`). Level 1-5 is state on the actor, not separate actors.
- `WithGrowableSprite` picks frame `(level-1) * variants + variant`; variant = stable hash of the cell.
  Variants are split by **theme**: first half North American (NA), second half European (EU).
- Lot shape: W cells along the road, D deep. `WxD` serves a road to the north/south, `DxW` the same shape for a road
  east/west. So every `WxD` / `DxW` pair is the same building turned 90 degrees; the front always faces the road.
- States in code: under construction (separate image `zon-construction`, per footprint, animated frames), upgrading
  (a level-up plays construction again while tenants stay), abandoned (today: tinted 0.5 dark), collapse ->
  `rubble-<WxD>` actor (per footprint), fires exist (`IFireStarter`, storms) -> burnt state. Night lights are found
  from lit-window pixels in the sprite.
- Signature buildings: 3x3, fixed level 5, once per city, both themes.

## Growable lot actors (56)
| Zone (ZoneType) | Actor prefix | Footprints | Character |
|---|---|---|---|
| ResidentialLow | res-low | 1x1 2x1 1x2 2x2 | detached house, garden, fence, driveway, car |
| ResidentialRow | res-row | 1x2 2x1 1x3 3x1 2x2 | terraces, party walls join neighbours seamlessly |
| ResidentialMedium | res-med | 2x2 3x2 2x3 3x3 | 3-5 storey apartment blocks |
| ResidentialHigh | res-high | 2x2 3x2 2x3 3x3 | 10-40 storey towers on a podium |
| ResidentialLowRent | res-lowrent | 2x2 3x2 2x3 | older worn slab blocks, balconies, satellite dishes |
| ResidentialMixed | res-mixed | 2x2 3x2 2x3 | shops on the ground floor, flats above |
| CommercialLow | com-low | 1x1 2x1 1x2 2x2 | corner shop, cafe, small store, awnings, signs |
| CommercialHigh | com-high | 2x2 3x2 2x3 3x3 4x3 3x4 | department store, mall, hotel |
| Office | off | 1x1 2x1 1x2 2x2 2x3 3x2 3x3 | low offices: brick, stone, glass |
| OfficeHigh | off-high | 2x2 3x2 2x3 3x3 | glass towers |
| Industrial | ind | 1x1 2x2 3x2 2x3 3x3 4x3 3x4 | factories, chimneys, sheds, tanks, yards, trucks |
| Warehouse | warehouse | 3x2 2x3 3x3 4x3 3x4 | big sheds, loading docks, trailers |

## Signature buildings (7, all 3x3, level 5)
sig-villa (res-low), sig-garden-row (res-row), sig-sky-tower (res-high), sig-bazaar (com-low), sig-mall (com-high),
sig-glass-hub (office), sig-fuel-plant (industrial). The brief names the first four; all seven get designs.

## Other actors in growables.yaml
- Legacy 1x1 fixed-level decoration (18): res-low-1..3, res-high-1..3, com-low-1..3, com-high-1..3, ind-1..3,
  off-1..3. Only used by old maps (shellmap). Covered by the 1x1 designs of the same zone at levels 1/3/5
  (res-high / com-high legacy 1x1 reuse res-low-1x1 L5 / com-low-1x1 L5 art); no separate art.
- Rubble (11): rubble-1x1, 2x1, 1x2, 3x1, 1x3, 2x2, 3x2, 2x3, 3x3, 4x3, 3x4.

## Per lot actor: states designed
| State | What |
|---|---|
| Level 1-5 | a visible quality step each level (materials, trims, size/height, landscaping, cars) |
| Variants | 4 per level: v0-v1 NA theme, v2-v3 EU theme |
| Facings | front toward +Y (default), +X, -Y, -X (all rendered for one representative per zone) |
| Construction | 3 stages: foundation, frame + scaffold + crane, nearly done (per footprint, per zone family) |
| Abandoned | boarded windows, grey, weeds, broken fence, no car |
| Burnt | blackened shell, soot, open roof |
| Rubble | debris heaps with wall stubs (per footprint) |
| Night | lit windows; emissive mask = lit-window pixels only |
| Winter | snow on roofs, ground and trees |

## Produced (tools/iso_zoned.py, 1980 PNGs, manifest: 97 groups / 1916 items)
| Zone | Footprints | Day designs (fp x L1-5 x 4 var) | Build 1-3 + abandoned/burnt/collapsed | Night + lit, winter (rep fp) | Facings |
|---|---|---|---|---|---|
| res-low | 4 | 80 | 24 | 10 + 10, 10 (1x1) | 8 |
| res-row | 5 | 100 | 30 | 10 + 10, 10 (2x2) | 8 |
| res-med | 4 | 80 | 24 | 10 + 10, 10 (2x2) | 8 |
| res-high | 4 | 80 | 24 | 10 + 10, 10 (2x2) | 8 |
| res-lowrent | 3 | 60 | 18 | 10 + 10, 10 (2x2) | 8 |
| res-mixed | 3 | 60 | 18 | 10 + 10, 10 (2x2) | 8 |
| com-low | 4 | 80 | 24 | 10 + 10, 10 (1x1) | 8 |
| com-high | 6 | 120 | 36 | 10 + 10, 10 (2x2) | 8 |
| off | 7 | 140 | 42 | 10 + 10, 10 (2x2) | 8 |
| off-high | 4 | 80 | 24 | 10 + 10, 10 (2x2) | 8 |
| ind | 7 | 140 | 42 | 10 + 10, 10 (2x2) | 8 |
| warehouse | 5 | 100 | 30 | 10 + 10, 10 (3x3) | 8 |
| **total** | **56** | **1120** | **336** | **240 + 120** | **96** |

Signature: 7 x (NA + EU) day, + night/lit, winter, build, abandoned per building, 4-facing strips for Villa and
Bazaar (59 files). Rubble: 11 footprints. Vignettes: suburban street, downtown block, industrial strip (on NET's
street/avenue tiles). Every generator can also render any other combination (all facings, all variants at night or
in winter): `iso_zoned_core.build(prefix, fp, level, var, state, stage, season)` + `iso_zoned_core.render(...)`.
