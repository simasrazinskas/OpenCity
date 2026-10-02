# NET inventory: networks and ground overlays

What the game draws today (sources: `rules/{construction,networks,traffic,transit,zoning}.yaml`,
`sequences/{networks,transit,misc}.yaml`, `tools/gen_net*.py`, `gen_pt*.py`, `genworld.py`,
`OpenRA.Mods.City/Traits/{World,Networks,Transit}`, `Orders/*`), and what the iso redesign adds for a CS2 feel.
Mask convention everywhere: bit0 N (cell y-1), bit1 E (x+1), bit2 S (y+1), bit3 W (x-1).
In the iso view N = upper-right tile edge, E = lower-right, S = lower-left, W = upper-left.

## 1. Roads (RoadLayer.Render.cs, sequences `roadnet.*`)
| Today | Frames | Notes |
|---|---|---|
| `street` `gravel` `avenue` `boulevard` `highway` `alley` | 32 each | 0-15 two-way by arm mask, 16-31 one-way (no centre line) |
| `arrows` | 4 | one-way direction overlay (N,E,S,W), also used by the road tool preview |
| `median` | 16 | paired carriageways (avenue, boulevard, highway): kerbed strip on every `sideBlock` edge |
| `control` | 5 | 0 stop, 1 signal, 2 roundabout marking, 3 highway ramp, 4 roundabout island (non-road cell) |
| `strips` | 240 | addons per non-arm side: combo bit0 trees, bit1 sound barrier, bit2 lights, bit3 parking |
| `lanes` | 48 | addon lane paint per arm mask: bit0 bus lane, bit1 bike lane |
| `bridge` | 4 | NS / EW, two-way / one-way (gravel cannot bridge) |
| legacy `roads.road` | 16 | old genworld autotile, unused by RoadLayer |

Rules facts that shape the art: classes Local/Collector/Arterial/Highway; avenue 2 lanes/dir, boulevard 3,
highway 3 (always paired, no zoning, no pipes/cables); `JunctionControl` None/Yield/Stop/Signal/Roundabout;
prefabs `roundabout` (3x3 ring, 1 island cell), `roundabout-large` (5x5 ring, 3x3 island), `ramp`
(highway to street link cell); outside connections `highway-{n,e,s,w}` lay highway from the map edge.

**Iso redesign adds**: crosswalks + stop lines baked into junction pieces, manholes / drains / patches,
3 wear states, class transitions (street-avenue, avenue-boulevard, street-alley, street-gravel, boulevard-highway),
map-edge highway entry with sign gantry, yield marking (shark teeth), roundabout ring pieces with curved lane paint.

## 2. Bridges, ramps, elevated
Today: 4 flat bridge frames. Adds: raised deck over water with railings and pillars (per class, both axes),
abutment/approach ramps (4 directions), elevated overpass deck + columns + 2-cell ramp concept.

## 3. Junction control and furniture (3D props; today painted flat in `control`)
Traffic light poles (red / amber / green, 4 corners), stop sign, yield sign (4 approaches each),
street lamps (day, night + light pool), bus stop shelter + sign, taxi stand, tram stop platform
(TransitRender `transit-stop`: frames 0-3 bus N,E,S,W, 4-7 taxi; trams reuse bus frames), metro entrance
(the `metrostation` building itself is CIVIC), benches, bins, hydrants, tree pits (trees addon),
sound barrier (barrier addon), parking bays (parking addon).

## 4. Lanes and tracks
Bus lane (red), bike lane (green) per arm mask; tram track embedded in road (`tram-track`, 16 masks);
rail (`rail.rail`, 18 frames: 16 masks + 16 = crossing E-W rail, 17 = crossing N-S rail), buffer stops.

## 5. Utilities (UtilityOverlay.cs, shown in the network info views)
`powerline` 16, `pipe-water` 16, `pipe-sewage` 16 (mask frames). Buildings: `transformer` 2x2,
`battery` 1x1, `sewage-outlet` 1x1 (needs water within 2), `treatment-plant` 2x2.
Adds: underground x-ray ground style for pipes, HV pylons + wooden distribution poles.

## 6. Zones (ZoneOverlay `zon-overlays.zone`, ZoneOrderGenerator colours)
`ZoneType`: None(dezone) 1 ResidentialLow 2 ResidentialHigh 3 CommercialLow 4 CommercialHigh 5 Industrial
6 Office 7 ResidentialRow 8 ResidentialMedium 9 ResidentialMixed 10 ResidentialLowRent 11 OfficeHigh
12 Warehouse; plus invalid. Active alpha 1, idle 0.4. Adds: per-type pattern so they read without colour.

## 7. Tool and placement markers (CityDragOrderGenerator, CityTileMarkerRenderable)
Valid (green), invalid (red), neutral/existing (white), replace/upgrade (blue), remove/bulldoze (orange),
road drag path with direction arrows, building footprint preview (1x1..3x3), selection outline, grid.

## 8. Info views (InfoViewLayer, Widgets/InfoViews.cs)
Ramps: Good (red-yellow-green), Bad (reverse), Pollution (clear-orange-brown), Blue (pale-deep),
Green (dark-bright), Category (16 colours: districts, road classes), Resource (6 hues x richness).
Today: flat translucent quads + `overlays.heat` 11 frames. Adds: 11-step tiles per ramp, edge-blend
tiles for smooth gradients, legend bars.

## 9. Parking
Today: `parking` addon only (roadside bays). Adds: lot surfaces (stall rows, both axes, entrance),
parked cars at several occupancy levels so lots look alive.

## Not NET (other workstreams)
Vehicles and pedestrians (LIFE), transit/utility producer buildings except the 4 network buildings (CIVIC),
terrain/water/trees (KIT), status icons and cursors (LIFE/UI).

---

# Design decisions and contracts (what other workstreams must match)

Generator: `python3 mods/city/tools/iso_net.py` (all PNGs + `manifest.json`); `anchors.json` lists the ground-centre
anchor of every prop/structure sprite. Ground tiles are exact 64x32 diamonds (same pixel coverage as isokit tiles).

**Units.** q = 1/64 cell = 0.25 m. Road geometry lands on integer q, so 2-q paint lines are exact 2-px 2:1 stairs.
Curves are quarter arcs of radius 32 q (0.5 cell) around the cell corner; junctions are arm unions with 8 q kerb
fillets. Right-hand traffic.

**Sidewalks and kerbs (for ZONED/CIVIC).** Sidewalks lie inside the road cell and end exactly at the cell edge, so
lots start at the lot cell boundary. Sidewalk top = isokit `stone` 8 (184,173,154), paving joints `stone` 7 every
8 q, kerb stone `stone` 10 (223,214,193); kerb height **1 px** (the raised sidewalk). Asphalt `grey` 3 (71,74,81),
highway `slate` 2. Plazas/forecourts that touch a sidewalk should use `stone` 8 paving to read as continuous.
Sidewalk widths (q from the cell edge): street 12, alley 20 (flat concrete apron, no kerb), avenue 6, boulevard 5,
one-way avenue 10, one-way boulevard 8; gravel and highway have no sidewalk (transparent verge shows terrain).

**Lane centres (for LIFE), q from the road centreline, + = right of the travel direction:**
| piece | half-width a | lane centres |
|---|---|---|
| street two-way | 20 | +10 (each direction keeps right) |
| street one-way | 20 | -10, +10 |
| alley / gravel | 12 / 14 | 0 (single shared lane; gravel wheel ruts at +-7) |
| avenue two-way | 26 | +8, +20.5 per direction (double yellow at +-2) |
| avenue one-way (paired) | 22 | -11, +11 |
| boulevard two-way | 27 | +10, +22 per direction (raised median abs(T) < 5) |
| boulevard one-way (paired) | 24 | -16, 0, +16 |
| highway two-way | 28 | +9.5, +20.5 per direction (barrier abs(T) < 2) |
| highway one-way (paired) | 26 | -14.5, 0, +14.5 (shoulders beyond +-22) |
Bus lane = outermost lane; tram tracks centred at +-10 q (two-way) or 0 (one-way), rails at +-4 q. Cars at the
NET parked-car scale are 0.42 x 0.2 cells (about 15 px long), body 4 px + cabin 3 px. On curves follow the arc.

**Props.** 1x1 props: 64x96 canvas, anchor (32, 80). Bridges/elevated: 64x80, anchor (32, 64). HV pylons:
64x112, anchor (32, 96). 2x2 buildings: 128x144, anchor (64, 112). Side/corner variants use isokit facings:
facing k puts an east-side model on the E, N, W, S side (corners SE, NE, NW, SW). Night: `-night` sprites plus
`-lit` emissive-only frames; lamp light pools are separate emissive ground frames (`furniture/lightpool-*`).
Bridge deck 8 px above ground (abutment ramps on the first/last water cell), elevated highway deck 24 px.

**Engine notes.** Masks follow ENGINE-PLAN (N = -Y up-right, E = +X down-right, S = +Y down-left, W = -X up-left).
Medians are per class (avenue planter, boulevard lawn + tree props, highway jersey barrier). Bridge end pieces
need RoadLayer to pick `end0`/`end1` when the neighbour along the axis is not a bridge. Overlays (zones, markers,
info views) are drawn with ordered dither because alpha is 0/255; the engine may swap the dither for real alpha.
