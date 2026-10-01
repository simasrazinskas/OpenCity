# 03 - Road and utility networks (research and design)

Domain 3 of the OpenCity "approach full Cities: Skylines 2" research. Scope: road types and hierarchy, road upgrades and services, intersections, zoning frontage, bridges/tunnels/elevation, outside connections, electricity grid, water and sewage pipes, info views. Written against the code in `OpenRA.Mods.City` as of commit `b4b7e7ee70`.

Confidence tags used below: **[W]** = from the CS2 wiki or a Paradox page, **[C]** = community or player reports (may be patched), **[M]** = from memory or inference, verify in game, **[D]** = OpenCity design decision (not CS2).

---------------------------------------------------------------------------------------------------

## 1. Cities: Skylines 2 mechanics

### 1.1 Road families and hierarchy

- Roads come in four families: **small, medium, large, highway**. Each family has two-way, one-way and asymmetric variants, plus elevated versions and bridges. **[W]** (Paradox Road Tools highlight)
- Detailer's Patch #2 added 3 one-tile-wide one-way roads and 5 asymmetric roads (4-lane 3+1, 6-lane 4+2, 7-lane 5+2, highway 3+1 and 3+2). **[W]**
- Larger roads have higher speed limit and capacity. Pathfinding is time-weighted, so a longer highway beats a shorter small road if it is faster overall. **[W]**
- Vehicles use all available lanes, change lanes around blocked ones, and can overtake. **[W]** (Dev Diary 2, Traffic AI)

Per-road data from the CS2 wiki Roads page **[W]** (prices are the wiki's "per km" numbers, "costs depend on many factors"):

| Road | Cost/km | Upkeep/km | Speed | Zoning | Traffic lights |
|---|---|---|---|---|---|
| Alley | 3.0K | 337 | 30 km/h | yes | no |
| Gravel Road | 2.5K | 412 | 30 km/h | yes | no |
| One-Lane One-Way | 3.25K | 375 | 40 km/h | yes | no |
| Two-Lane Road | 4.0K | 487 | 40 km/h | yes | no |
| Four-Lane Road | 5.5K | 712 | 50 km/h | yes | yes |
| Six-Lane Road | 7.0K | 938 | 50 km/h | yes | yes |
| Eight-Lane Divided | 8.5K | 1.16K | 60 km/h | yes | yes |
| Two-Lane Highway | 2.0K | 300 | 100 km/h | **no** | no |

Other facts:
- A secondary source (Beef Suplex wiki, mph) lists alley/gravel 25, two-lane 30, four to seven-lane 35, eight-lane 45, highways 55 to 70; it disagrees with the wiki in units and values, so use the ordering only. **[C]**
- Highway-class roads **do not allow zoning** next to them. **[W]**
- Pollution multipliers: noise 1x for most roads, 2x gravel and pedestrian, 3x highways. Air pollution 1x, highways 3x. **[W]**
- Elevated versions cost 7.5K to 28.5K extra per km, tunnels 18.75K to 78K per km. **[W]**
- Roads are also the utility carrier (see 1.5): all road types except highways carry low-voltage cable (40 MW) and water+sewage pipes; bridges carry cable but no pipes. **[W]**

### 1.2 Road upgrades and services

- **Replace/upgrade tool**: pick a road type, click an existing segment, it is replaced in place and keeps its connections. It also fine-tunes with grass, trees, wider sidewalks, sound barriers. **[W]**
- **Add-ons without changing type**: bus lane, tram tracks, bicycle lanes, parking (perpendicular/angled parking variants for small and medium roads). Bicycle lanes get cyclists out of the car lane so cars keep speed. **[W]**
- **Advanced Road Services** (unlocked via development tree): add or remove traffic lights, crosswalks, stop signs, streetlights, turn-lane control. **[W/C]**
- **Road Maintenance Depot** sends vehicles to fix road condition; poor condition raises accident probability; snowplows in winter. Roads info view shows condition and parking. **[W]**
- Sound barriers on highways cut traffic noise. **[W]**

### 1.3 Intersections

- Default control: small-road junctions have no signs, any junction with a medium or large road gets traffic lights. Players can switch an intersection to yield, stop signs, traffic lights or a roundabout. Stop signs are per intersection, not per road (a known limitation, possibly fixed since). **[C]**
- **Roundabouts** come in 4 sizes (about 200 to 1,500 credits), replace signs and lights, and adapt lane counts to the connecting roads. **[W]**
- Premade intersections can be dropped over empty land or existing roads and auto-connect. **[W]**
- Highways use ramps with acceleration/deceleration lanes. **[W]**

### 1.4 Zoning frontage

- A zone cell is **8 m x 8 m**; zoning extends **6 cells (48 m) deep** on each side of a road; the largest lot is 6x6, the smallest 1x2. Two parallel roads 12 cells apart are fully zoned. **[C]** (Steam guide "Efficient Grids", forum thread)
- Lot sizes are width along the road x depth away from it. Low-density housing tops out at 4 wide. Spawner always tries the largest, deepest lot first. **[W/C]**
- Zoning tools: fill, marquee, paint. **[W]**
- Roads create the block; roads without zoning (highways) create none. Upgrading a road keeps zones as long as the block still fits. **[M]**

### 1.5 Elevation, bridges, tunnels, outside connections

- Roads support cut-and-fill into terrain, transition to tunnels using negative elevation, and elevated roads stack over existing roads with clearance. **[W]**
- Outside connections: **highway (1 to 5 lanes), rail, seaway, airplane**, and utility connections: **high-voltage line, water/sewage pipe**. They appear automatically when a network is extended to the playable-area edge. Roads with built-in cable/pipes accidentally create utility connections too. **[W]** (Map Creation: Outside Connections)
- Import/export prices **[W]** (Economy page): electricity import 5,000/MW and export 2,500/MW (raised later in patch 1.1.5, so treat as stale); water import about 0.1/m3, export about 0.05/m3; sewage export about 0.1/m3, cannot be imported. "Import City Services" policy toggles imported service vehicles.
- Excess production is always sold if possible while keeping enough for the city. **[W]**

### 1.6 Electricity

- Two voltage layers: **low voltage cables** (inside roads, or standalone) and **high voltage power lines** (separate placed lines on pylons, over or under ground). A **transformer station** converts HV to LV or LV to HV for export; it has limited transformation capacity. **[W]**
- Each cable and line has a **maximum capacity**; exceeding it creates a bottleneck and surplus is wasted. Hover in the Electricity info view shows flow, direction and capacity. **[W]**
- Capacities reported by players: **HV line 400 MW, LV cable and road cable 40 MW, transformer effectively 40 MW per road connection (80 MW with two)**. The "80 MW" figure is disputed. **[C]**
- Plants (MW, community table, mixed patch levels) **[C]**: wind 5 to 8.5, small coal 20, gas 250 to 350, coal 300 to 400, geothermal 150 to 300, nuclear 750, solar about 100 to 150 average, hydro variable (about 50 to 2,200+). Battery Station about 200 MW / 500 MWh, plant battery extension about 50 MW / 50 MWh.
- Demand depends on weather (hot and cold raise it), building level (higher level uses less per capita) and fee: each 1% below 100% fee gives +0.2% consumption. **[W]**
- Shortage: citizens lose well-being and eventually leave; companies lose efficiency and go bankrupt. **[W]**
- Buildings on a road auto-connect; off-road buildings need manual cable. **[W]**

### 1.7 Water and sewage

- Pipes are built into most roads, auto-connect adjacent buildings, and can be built separately (single water, single sewage, or dual pipe) for off-road buildings. **Pipes have no capacity to monitor**; limits are at the source buildings. **[W/C]**
- Sources: surface pumping station about 100,000 (+50,000 upgrade), groundwater pump 75,000, water tower 30,000 (highest upkeep), advanced pumping station up to 1,000,000. Groundwater can run dry and is vulnerable to ground pollution. **[C]** (TheGamer guide)
- Sewage: outlet 100,000 (dumps into water, pollutes), treatment plant 400,000 (+100,000 unit), treated water returns to the fresh network. Water use is roughly equal to sewage production. **[W/C]**
- Shortage: low water hurts well-being and health and company efficiency; backed-up sewage hurts health. **[W]**
- Water and sewage can both be exported through pipe outside connections; sewage cannot be imported. **[W]**

---------------------------------------------------------------------------------------------------

## 2. Current OpenCity state and gaps

### 2.1 What exists

| Area | Current implementation | File |
|---|---|---|
| Road storage | `CellLayer<byte> flags` (bit0 road, bit1 locked/highway). One road per cell, no type, no direction. `HashSet<CPos> roadCells`. | `Traits/World/RoadLayer.cs` |
| Connectivity | 4-neighbour; flood fill from locked (highway) cells, cached by `NetworkVersion`. `IsConnectedToOutside`, `HasRoadAccess`, `HasRoadAccessWithin(cell, depth)`. | `RoadLayer.cs` |
| Pathfinding terrain | Writes `Road` terrain into `Map.CustomTerrain`; restores `previousTerrain` on removal. Vehicles use locomotor `road`. | `RoadLayer.cs`, `rules/traffic.yaml` |
| Rendering | `TerrainSpriteLayer`, image `roads`, sequence `road`, 16 frames, frame = N/E/S/W mask (bit0 N, bit1 E, bit2 S, bit3 W). The highway's first cell draws a connection towards the map edge (`edgeConnections`). | `RoadLayer.cs`, `tools/genworld.py` `road_frame()` |
| Build | `CityBuildRoad` order: `ExtraLocation` = start, target = end. `CityUtils.RoadPath` L-path. `ConstructionUtils.PlanRoad` shared by order and preview; flat `CostPerCell = 10`, `UpkeepPerCellPerMonth = 1`. Clears trees, dezones, `AddRoad`. | `ConstructionTools.cs`, `ConstructionUtils.cs`, `RoadOrderGenerator.cs` |
| Bulldoze | Removes non-highway roads in a rectangle; no refund for roads. | `ConstructionUtils.PlanBulldoze` |
| Zoning | `ZoneLayer.ZoneDepth = 4`: a cell is zonable if any road cell is within Chebyshev distance 4, cached in `nearRoad`. Zones under new roads are cleared; removing roads keeps zones. | `ZoneLayer.cs` |
| Growth access | Growables need `HasRoadAccessWithin(cell, ZoneDepth)`, services need a 4-adjacent road. | `CityManager.Daily.cs` `RefreshRoadAccess` |
| Utilities | `UtilityProducer` (Power, Water) on plants/towers/pumps. `DistributeUtilities()` sums all operational producers that have road access into **one city-wide pool**, then serves consumers in `ActorID` order; `HasPower`/`HasWater` per building. Consumption 1 to 5 per building, scaled 25 to 100% by occupancy. No sewage. | `CityManager.Daily.cs` |
| Supply sizes | coal 300, solar 90, wind 12, water tower 90, pump 300 | `rules/services.yaml` |
| Outside connection | `highway-w/e/n/s` actors lay `Length` (12) locked road cells; those are the only "outside" connection. No trade. | `OutsideConnection.cs` |
| Traffic | `TrafficManager` snapshots components, own BFS over 4-connected `IsRoad` cells, `Mobile.MoveTo(path)`; `WithLaneOffset` draws the right-hand lane purely visually; `GetTrafficLoad(cell)` 0..100. | `TrafficManager.cs` |
| Info views | `CityInfoView.Power/Water` colour **buildings** only (green/red), traffic colours road cells; no network overlay. | `InfoViewLayer.cs` |
| UI | Road tab has one tool ("road"). | `CityToolbarLogic.RoadItems()` |

Order payloads: `Order.ExtraData` (uint) is already used by zone orders for the zone id, so a road type id can ride in `ExtraData` of `CityBuildRoad` without changing the order shape.

### 2.2 Gaps against CS2

1. **One road type**: no hierarchy, speed, capacity, lanes, noise or upkeep differences.
2. **No directionality**: `IsRoad` is an undirected graph; BFS and lane offset assume two-way, so one-way, divided roads and ramps are impossible.
3. **No junction control**: every junction is free-flowing.
4. **Zoning is a Chebyshev blob**: ignores road class (highways would zone) and frontage; any road within 4 cells grants access.
5. **Utilities are a global pool**: distance, connectivity and capacity do not matter. No blackout districts, bottlenecks, transformers, batteries, imports or sewage.
6. **Highway is not a class**: `LockedFlag` means "permanent outside connection", otherwise it is a normal street that allows zoning.
7. **No trade** and no rail/sea/air connection; no road wear or depots; no network info views; no elevated or underground layers.

---------------------------------------------------------------------------------------------------

## 3. Proposed design

Principles **[D]**: (a) everything is data in yaml (road types, plant capacities) and integer math; (b) one **connection rule** function decides if two neighbouring cells connect, and drawing, traffic graph, flood fill, zoning access and utilities all call it; (c) every state change is an order; (d) keep the 32px grid, one road piece per cell, 4-neighbour links; (e) CS2 3D features become **logical layers** (see 3.9).

Scale **[D]**: one cell is about 16 m, the width of a CS2 small road. CS2's 48 m zone depth is then 3 cells; we keep `ZoneDepth = 4` because it already balances growth.

### 3.1 Road type registry

Add `RoadTypeInfo` as a repeatable trait on the World actor (`RoadType@street:`, `RoadType@avenue:` and so on). `RoadLayer` reads `world.WorldActor.Info.TraitInfos<RoadTypeInfo>()` in yaml order; the **index + 1 is the stable `byte` type id** (0 = no road). Per-cell storage:

- `CellLayer<byte> typeId`
- `CellLayer<byte> dirFlags`: bits 0-2 one-way direction (0 two-way, 1..4 = N/E/S/W), bit 3 `Ramp`, bit 4 `Locked` (outside-connection road), bit 5 `Elevated` (bridge)
- `CellLayer<byte> sideBlock`: bits N/E/S/W where the cell must not connect to that neighbour (median, barrier)
- `CellLayer<byte> control`: junction control, 0 default, 1 none/yield, 2 stop, 3 signal
- `CellLayer<byte> addons`: bit0 trees, bit1 sound barrier, bit2 lights, bit3 bike lane, bit4 parking, bit5 bus lane

The existing `flags` byte is replaced by these (`RoadFlag` becomes `typeId != 0`, `LockedFlag` becomes the `Locked` bit). The public API (`IsRoad`, `IsHighway`, `AddRoad(cell)`, `RoadCells`, `NetworkVersion`, ...) stays; `AddRoad(cell)` becomes `AddRoad(cell, typeId = default street)`.

Fields of `RoadTypeInfo` (all `[Desc]`-documented): `Name`, `Class` (`Local`, `Collector`, `Arterial`, `Highway`), `Lanes` (per direction), `SpeedPercent` (street = 100), `Cost`, `Upkeep`, `Noise` (0..100 at the source), `AirPollution`, `Zoneable`, `GivesAccess` (buildings may use it as their street), `CarriesUtilities` (LV cable plus water and sewage pipes inside), `DefaultControl`, `Paired` (laid as two one-way carriageways), `UnlockPopulation`, `Image`/`Sequence`, `Terrain` (see 3.4).

Proposed roster (money is in OpenCity dollars per cell, tuning values **[D]**, relative ratios follow the CS2 wiki table in 1.1):

| id | Type | CS2 analogue | Lanes/dir | Speed % | Cost | Upkeep/mo | Noise | Zoneable | Utilities | Control default | Unlock |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `street` | Two-Lane Road | 1 | 100 | 10 | 1 | 20 | yes | yes | none | 0 |
| 2 | `gravel` | Gravel Road | 1 | 70 | 6 | 1 | 40 | yes | cable only | none | 0 |
| 3 | `avenue` | Four-Lane Road | 2 | 125 | 18 | 2 | 35 | yes | yes | signal | 250 |
| 4 | `boulevard` | Six-Lane Road | 3 | 125 | 26 | 3 | 45 | yes | yes | signal | 1,000 |
| 5 | `highway` | Highway | 3 (paired) | 250 | 35 per cell (70 per pair) | 3 | 70 | **no** | **no** | ramps only | 2,500 |
| 6 | `alley` (later) | Alley | 1 | 70 | 6 | 1 | 15 | yes | yes | none | 250 |

One-way is a **flag**, not a separate type (a one-way street is `street` plus direction), so CS2's one-way variants cost nothing extra in art. `Paired` types (`highway`, optional `divided avenue`) are two adjacent one-way cells (see 3.3). Per-lane vehicle slots: a cell holds `Lanes x 3` vehicles per direction (a car is about 5 m, a cell 16 m); that is the **capacity** used by traffic.

### 3.2 Connection rule and directed graph

One function in `RoadLayer`, used by everything:

```
bool Connects(CPos a, CPos b)   // geometric: undirected, true if cells may touch as network neighbours
bool CanEnter(CPos from, int dir) // directed: may a vehicle step from `from` towards Neighbours4[dir]
```

`Connects(a, b)` rules (symmetric):
1. both are roads;
2. neither cell has `sideBlock` towards the other;
3. highway-class cells connect only to other highway-class cells, except where a cell has the `Ramp` bit (then it also connects to non-highway neighbours);
4. an `Elevated` cell connects only along its own bridge axis (see 3.8).

`CanEnter(from, dir)` additionally rejects reverse movement: moving along `v` out of a one-way cell with direction `d` requires `v != -d`, and into a one-way cell requires `v != -d` as well (entering against flow is illegal, side entry is legal). Two-way cells accept everything `Connects` allows. This is local, needs no global state, and a divided road falls out naturally: two one-way cells with `sideBlock` on the inner sides.

Consumers: autotile mask (3.5) uses `Connects`; `EnsureConnectivity` flood fill uses `Connects`; `TrafficManager.FindPath` (BFS today) uses `CanEnter` and, with road speeds, a Dijkstra with cost `1000 / SpeedPercent(cell)` plus junction delay. Cached **directed adjacency** is not necessary at this scale; recompute from the layers. Strong-connectivity checks ("every house has a legal path out and back") are a traffic-domain concern; the layer offers `IsConnectedToOutside` (undirected) for access and `CanEnter` for routing.

### 3.3 Divided roads, highways, ramps

- **Paired placement** ("parallel mode" in CS2): the drag defines one carriageway; a second one is laid at an offset of one cell to its left (right-hand traffic), flowing the opposite way. For an L path, offset the three waypoints (start, corner, end) and rasterise both with `CityUtils.RoadPath`. The inner sides get `sideBlock`, the outer sides stay open (so avenues take side streets on the outer side only).
- Cost is cost x 2. The `Paired` flag stays on both cells so bulldoze, replace and flip operate on the pair (`GetPartner(cell)` = the neighbour across the `sideBlock` side with the opposite direction).
- Optional `median` option leaves a one-cell gap (2 apart) filled with a tree/grass cell tagged `Median` (blocks zones and growth); MVP skips it.
- **Highways** are paired `highway` cells with `Zoneable = no`, `GivesAccess = no`, no utilities. They carry long-distance traffic and **outside commuters**. A ground road only meets a highway at a **Ramp**: the interchange tool places a 1-cell ramp (a `highway` cell with the `Ramp` bit) that connects to any non-highway road neighbour and to the highway behind it. Acceleration lanes are just art. A ramp costs extra (about 100) and must sit next to a highway carriageway.
- **Existing highways** (`highway-w/e/n/s` actors) get lane type `highway`, paired, `Locked`. `OutsideConnection` gets `Kind: Road` (see 3.7).

### 3.4 Speed, capacity and noise hooks (for the traffic domain)

`RoadLayer` publishes read-only per-cell data: `GetType(cell)`, `GetSpeedPercent(cell)`, `GetLanes(cell)`, `GetCapacity(cell)`, `GetNoise(cell)`, `Control(cell)`, `Addons(cell)`. Two ways to apply speed to `Mobile` (decide in the traffic design):
- **Terrain route (cheap):** add terrain types `RoadLocal`, `RoadFast`, `RoadHighway` to `tilesets/temperate.yaml` and set `Map.CustomTerrain[cell]` per type. The `road` locomotor already supports per-terrain speed (`TerrainSpeeds: Road: 100`), so speed per cell is free and OpenRA's own pathfinder cost reflects it. Requires an A1 tileset change. **[M]** custom-terrain indices must exist as tileset terrain types.
- **Modifier route:** an `ISpeedModifier` trait on vehicles that asks `RoadLayer.GetSpeedPercent(CellPos)`. No tileset change.

Congestion factor = `load / capacity` (traffic domain); the road layer only supplies capacity.

Noise and air: `Noise` and `AirPollution` per cell feed `CityCoverageLayer` (an extra source term next to building `Pollution`, scaled by the cell's traffic load for a "traffic noise" map). Sound-barrier addon halves noise 4-adjacent to the road side (CS2: sound barriers on highways). Highways x3 as in CS2.

### 3.5 Rendering and autotile

- `RoadLayer` keeps one `TerrainSpriteLayer` per road type image (the engine layer takes one sequence and palette), or a single layer with a combined sheet. Recommended: **one sheet per type, 16 frames each, same 32x32 and bit order as today** (bit0 N, bit1 E, bit2 S, bit3 W), so existing code and `NeighbourMask` stay valid. The mask is built with `Connects(cell, neighbour)`, not `IsRoad`.
- Mixed neighbours (street meets avenue): each cell draws its own type; the arm toward the other type is drawn at the cell's own width. Add a 2px asphalt "shoulder" in the wider type's art to hide the seam. No transition frames needed.
- **Overlay layers** (second `TerrainSpriteLayer` over the roads, same cell grid):
  - `road-arrows`: 4 frames (N, E, S, W), drawn on one-way cells (one arrow centred, or two per cell for avenue/boulevard).
  - `road-median`: 16 frames indexed by `sideBlock` mask: a 4px kerb plus grass/tree strip along each blocked edge of a paired carriageway.
  - `road-control`: 4 frames: stop line plus sign, signal heads (red/green drawn static, animated only for the selected cell), roundabout island, none. Drawn only when a cell is a junction (3+ connections) and not `none`.
  - `road-addons`: trees (frame per side), lights, parking bays (angled stripes), bus lane (red tint), bike lane (green). Per-side frames use the same 4-bit mask idea.
- `WithLaneOffset` (constant 6px to the right) should become a function of `GetLanes(cell)` (1 lane 6px, 2 lanes 8px, 3 lanes 10px).

### 3.6 Intersections and roundabouts

A **junction cell** is a road cell with 3 or 4 `Connects` arms. Per-cell `control` overrides the default of the road classes meeting there:

| Default (control = 0) | Rule |
|---|---|
| all arms `Local` | no control: yield to the right (traffic domain), fastest to build |
| any arm `Collector` or higher | signal |
| highway ramp | merge (no stop) |

Explicit values: none/yield, stop (all arms stop for a short fixed time), signal. A signal is deterministic: phase = `(world.WorldTick / CycleLength + Hash(cell)) % 2` selects the N-S or E-W axis; `CycleLength` about 30 ticks (about 1.2 days at 25 ticks per day). Cars on the red axis do not enter the cell. A **stop** forces each vehicle to wait `StopTicks` (about 8) before entering. `RoadLayer.GetControl(cell)` exposes the effective value (explicit or default). The control tool toggles a junction cell `none -> stop -> signal -> none` via order `CitySetRoadControl`.

**Roundabouts** need no special traffic logic if built from cells: the **Roundabout prefab** (3x3 small, 5x5 large, mirroring CS2's 4 sizes with a smaller set) is a ring of one-way cells flowing counter-clockwise (right-hand traffic) with the centre cell cleared to an `Island` decoration (blocks zones and growth). The prefab is placed by order `CityPlaceRoadPrefab` over a junction, reuses existing arms (cells already road at the ring boundary connect via `Connects`), refunds nothing for the replaced cells. CS2 says a roundabout removes signs and lights; here the ring cells have `control = none`. Roundabout cost: 3x3 about 150, 5x5 about 400 **[D]**.

### 3.7 Road tool UX and orders

UI (toolbar road tab, see section 6): a **type palette** (one icon per `RoadType` the player has unlocked) plus **mode buttons**: Draw (default), One-way (draw direction is flow), Replace, Junction control, Roundabout, Interchange. Hotkeys: `R` opens roads, `Tab` flips direction of a pending one-way drag, `Shift` while dragging forces a straight line, `Ctrl` toggles paired mode on types that allow it (`Paired` is default on for highway).

Orders (all resolved in `ConstructionTools`, no randomness):

| Order | Payload | Effect |
|---|---|---|
| `CityBuildRoad` (exists) | `ExtraLocation` start, target end, `ExtraData` = typeId (bits 0-7) \| mode bits (8 one-way, 9 reverse, 10 paired, 11 replace) | lays/replaces the L path |
| `CitySetRoadControl` | target cell, `ExtraData` = control value | per-junction control |
| `CityPlaceRoadPrefab` | target centre cell, `TargetString` = prefab name | roundabout / interchange |
| `CityRoadAddon` | `ExtraLocation`/target = drag, `ExtraData` = addon bit \| on/off | trees, barrier, lights, bike lane, parking |

`PlanRoad` is extended and stays the single source of truth for preview and order: per path cell it decides `New`, `Replace` (existing road of a different type or flags), `Keep` (identical) or `Blocked`. **Replace cost** = new cost minus 50% of the old cost, floor 0; bulldozing a road refunds 50% of its type cost (today: nothing). Paired mode plans both polylines in one plan and one payment. The preview colours (valid/neutral/invalid markers) are reused; add arrows via the `road-arrows` sprite in the preview and a cost label (exists).

**Upgrade by dragging**: with a type selected, dragging over roads of another type replaces them in place; zones and buildings stay (footprint is unchanged). Replacing a street with a highway is refused if an adjacent building would lose all access (`ErrorWouldIsolate`). One-way flips follow the drag.

### 3.8 Zoning and building access

Current rules use any road cell within Chebyshev distance 4. Change to:
- `ZoneLayer.RebuildNearRoad` iterates `roads.ZoningCells` (cells whose type has `Zoneable`) instead of `RoadCells`, so highways never create zoneable land.
- `HasRoadAccessWithin` and `HasRoadAccess` count only `GivesAccess` cells (`street`, `avenue`...). Ramps and highways do not provide street access. Gravel provides access but houses on gravel get a land-value malus (CS2: gravel roads are cheap and noisy).
- A building's **access cell** = the nearest `GivesAccess` road cell (ties broken by cell order). `RoadLayer.GetAccessCell(CPos lot)` replaces the 4-adjacent assumption in `TrafficManager` endpoint search. A building with no legal directed path to and from its access cell counts as `HasRoadAccess = false`.
- A road type change never clears zones. A new road clears zones under itself (as today).
- Keep the current rule that removing a road keeps the zone, but growth stops without access (already true).

### 3.9 Bridges, tunnels and elevation in 2D

**Impossible** with one sprite per cell and 2D `CustomTerrain`: real height clearance, roads stacked at arbitrary offsets, cut-and-fill terrain, a tunnel cut-away, long curved bridge spans, slopes. **Possible as logical layers:**

- **Bridge cell** (`Elevated` bit): road over water or over another road. It connects only along its own axis (`Connects` rule 4); a ground road may run **underneath** in the same cell via a second layer (`underType`, `underDir`). Draw the under-road, then a `road-bridge` deck (2 frames, N-S and E-W) with shadow; vehicles on the deck are only drawn on top. Over water, `Elevated` pieces may use `Water` terrain (cost x3, span at most 6 cells). Bridges carry cable but no pipes.
- **Tunnel**: a per-cell `tunnelMask`, nothing drawn on the surface except portals; an **underground view** toggle shows tunnels, pipes and cables.
- Both are Phase P8; the MVP simply forbids roads on water and crossings of highways.

### 3.10 Outside connections

Extend `OutsideConnection` with `Kind` (`Road` today, `Rail`, `Sea`, `Air`, `Power`, `Water`, `Sewage`), `Capacity` and trade prices. A `Road` actor lays a paired `Locked` `highway`. It is the spawn point for commuters, trucks and tourists (capacity in cars per day, for the traffic domain) and counts for `IsConnectedToOutside`. `Rail` only marks the edge; stations connect later (public-transport domain). `Sea` and `Air` need a `harbour` or `airport` building near the edge (hook only). `Power`, `Water` and `Sewage` are network nodes, see 3.15.

### 3.11 Utility networks: layers

Replace the city-wide pool with a **graph per utility**, owned by a new world trait `UtilityNetwork` (file `Traits/World/UtilityNetwork.cs`; `CityManager.DistributeUtilities` becomes a thin reader). Layers **[D]**, modelled on CS2:

| Layer | Carried by | Placed by | Capacity |
|---|---|---|---|
| **LV cable** | every road with `CarriesUtilities` (implicit, free) | automatic | `LvCapacity` per cell, default 60 units (CS2 40 MW; our units are not MW) |
| **HV power line** | `PowerLineLayer`: per-cell flag, independent of roads (can share a cell, drawn as pylons on the cell edge) | drag tool (L path), cost 5/cell, crosses water at x2 | `HvCapacity` per cell, default 600 |
| **Water pipe** and **sewage pipe** | roads with `CarriesUtilities` (implicit), or `PipeLayer` cells (water, sewage or dual) for off-road buildings | automatic / drag tool, cost 3/cell | **none** (as CS2); limits live at sources |
| **Transformer** | placeable 2x2 building bridging HV and LV | `CityPlaceable` category `power` | `TransformerCapacity` default 150 |

Connection: a building or plant joins a network through its **access cell** (a road cell with cable/pipes) or an adjacent HV/pipe cell. Roads without utilities (highways, `gravel` for pipes) do not connect. Bridges carry cable but not pipes (as CS2).

### 3.12 Electricity solver

Daily (and on `NetworkVersion` / producer-set change), in integer maths and `ActorID`/row-major order:
1. **Phase A, components (MVP):** union-find over LV cells; HV cells form separate components; each transformer joins one HV and one LV component with a throughput limit. Per component: supply = sum of connected operational producers (and imports); demand = sum of consumer `Use`. Satisfaction = `min(100, 100 * supply / demand)` for everyone in the component (brownout shared); consumers are not served in ActorID order any more.
2. **Phase B, flow (full):** a max-flow graph with node-split cell capacities: super-source to producers (cap = output), consumers to super-sink (cap = demand), cell node capacity `LvCapacity` / `HvCapacity`, transformer edges `TransformerCapacity`, outside connection edges. Dinic with deterministic edge order (N,E,S,W, row-major, building edges by ActorID). Result gives per-building satisfaction and per-cell flow for the info view. To bound cost: compress chains of degree-2 cells into one edge with the minimum capacity; solve at most every 25 ticks and only when dirty or on a demand change above 10%; expected a few hundred to a few thousand nodes.
3. **Batteries:** `battery` building with `Charge` (ISync) and `MaxCharge`, `MaxRate`. After the flow: surplus charges, deficit discharges up to `MaxRate`; battery is a producer (cap = rate while charge > 0) and a sink (while < MaxCharge).
4. **Overproduction is wasted** unless exported (see 3.15). Plants are unconnected producers if their access cell has no cable: UI warns "not connected".

Results on `CityBuilding` (frozen API stays): `HasPower` = satisfaction >= 50; add `PowerPercent` (0..100) for happiness scaling; the existing proportional shortage penalty in `CityManager.UpdateHappiness` keeps working. `PowerProduced/PowerConsumed` become totals over all components (HUD unchanged), add `PowerLost` (overproduction or bottleneck loss).

### 3.13 Water and sewage solver

- **Water:** per pipe component: supply = sum of connected producers (`UtilityProducer.Water`), demand = sum of consumer `WaterUse`. Same shared-satisfaction rule as Phase A (pipes have no capacity). Producers: `watertower` 90, `waterpump` 300 (needs Water terrain within 2, as now). Add a **groundwater** later (finite `Reserve` regenerating daily; drops when over-used).
- **Sewage (new):** consumers produce `SewageUse` = delivered `WaterUse`. Sinks: `sewage-outlet` (1x1, needs Water nearby, capacity 150, pollutes water cells near it so pumps there read contaminated) and `treatment-plant` (2x2, capacity 500, upkeep high, returns 70% to the water supply of its component). When sewage exceeds sink capacity of the component, `HasSewage = false` for the lowest-priority consumers (`ActorID` tail) and a health/happiness malus applies (new term in `UpdateHappiness`: -15 for residential, mirrors "no water").
- **Pollution link:** outlet dumps add `Pollution` to the cell layer used by `CityCoverageLayer`; a pump within `PollutedRadius` of an active outlet loses 50% output.

### 3.14 Placement rules

HV lines and pipes are drag tools with a shared preview and cost label (orders `CityBuildPowerLine`, `CityBuildPipe`, resolved in a new `UtilityTools` player trait so C's `ConstructionTools` stays small). Upkeep: line 0.2/cell, pipe 0.1/cell, rounded per 10 cells. Utilities are drawn only while an info view or the Networks tab is active (underground view); pylons and plants are always visible.

### 3.15 Outside trade (power, water, sewage)

`OutsideConnection` with `Kind: Power`, `Water` or `Sewage` is a node on a **map-edge cell**; the player must run an HV line (or pipe) to it. Parameters per connection: `ImportCapacity`, `ExportCapacity` (default 150 power, 200 water), `ImportPricePer10`, `ExportPricePer10` (CS2: import costs about 2x the export price; electricity import 5,000 and export 2,500 per MW). Solver treats it as a producer of capacity `ImportCapacity` with a high cost (used only after local plants) and as a consumer of surplus up to `ExportCapacity` (second flow pass); monthly settlement via `CityManager.AddFunds/TrySpend("trade")`. Sewage export only (no import), as CS2. Road/rail/air/sea connections are handled in 3.10.

### 3.16 Info views and tooltips

New `CityInfoView` values (needs the lead's frozen `CityTypes.cs`): `PowerGrid`, `WaterGrid`, `Sewage`, `Roads` (type colours + condition later). `PowerGrid` colours **cable cells** (road cells, HV cells) by flow/capacity (green below 70%, yellow, red above 100%) and shows building satisfaction as today; hovering a cell shows `flow / capacity` and direction arrow. `Roads` colours cells by class and shows capacity vs load.

---------------------------------------------------------------------------------------------------

## 4. Interfaces with other domains

| Consumer | Needs | Provided by this design |
|---|---|---|
| **Traffic / vehicles / citizens** | directed legal moves, speed and capacity per cell, junction control, building access cell, outside spawn points | `RoadLayer.CanEnter(from, dir)`, `Connects`, `GetSpeedPercent`, `GetLanes`, `GetCapacity`, `GetControl`, `GetAccessCell(lot)`, `OutsideConnection` list with `Kind`/`Capacity`. `NetworkVersion` and `RoadChanged(cell)` stay as invalidation signals. Traffic keeps its own BFS/Dijkstra and the `Mobile.MoveTo(path)` pattern; it needs **no pathfinder change** because directedness lives in its own search. `TrafficManager` must stop using `IsRoad` for steps and use `CanEnter`. |
| **Zoning / growth** | which cells are zoneable, which road gives access | `RoadLayer.ZoningCells`, `GivesAccess`, `GetAccessCell`; `ZoneLayer.RebuildNearRoad` iterates only zoneable road cells. `CityManager.RefreshRoadAccess` uses the access cell and directed reachability. |
| **Simulation / services / economy** | utility results per building, upkeep, trade | `UtilityNetwork.GetSatisfaction(actor, kind)`; `CityBuilding.HasPower/HasWater/HasSewage` set by `UtilityNetwork`; `RoadLayer.MonthlyUpkeep` (sum of type upkeep plus addons) replaces `RoadCellCount * UpkeepPerCellPerMonth`; trade settles via `CityManager.AddFunds/TrySpend` with category `"trade"`. |
| **Pollution / land value** | road noise and air, traffic noise | `RoadLayer.GetNoise(cell)` and `GetAirPollution(cell)` read by `CityCoverageLayer`; sound-barrier addon halves noise. |
| **UI** | road palette, modes, info views, tooltips | `RoadLayer.Types` (ordered list with unlock population); `RoadOrderGenerator(World, RoadToolOptions)`; the existing `new RoadOrderGenerator(world)` keeps working as street + draw mode (CONTRACT.md signature kept). New `CityInfoView` values need an edit to the frozen `CityTypes.cs` (lead). |
| **Public transport / rail** (another domain) | bus lanes, rail edge connection | `addons` bit `BusLane`; `OutsideConnection.Kind: Rail`. |

Determinism: all new state is in `CellLayer`s changed only inside order handlers; the solver uses only ints and ordered iteration; `world.SharedRandom` is not needed. State that matters for sync (`Charge`, reserves, per-cell flags) should implement `ISync` or be derivable from orders so the replay-based autotest (`replay` mode) stays in sync.

---------------------------------------------------------------------------------------------------

## 5. Phased tasks and acceptance tests

Verification harness: `mods/city/tools/autotest.sh <map> "ticks=...;scenario=...;log=..."` plus the `./utility.sh city --check-yaml` (Errors: 0) lint and the replay-determinism mode. `CityAutoTest.cs` is frozen, so each phase asks the lead for one `scenario=` branch (listed). Tests assert stats lines in the log, and screenshots are checked with Read.

| Phase | Work | Files (new/changed) | Acceptance tests |
|---|---|---|---|
| **P1 Road types** (MVP) | `RoadTypeInfo` registry; per-cell `typeId`; `AddRoad(cell, typeId)`; `BuildRoad` `ExtraData` type; replace mode; cost and upkeep by type; roster `street`, `gravel`, `avenue`; 3 sheets x 16 frames; palette tab | `RoadLayer.cs`, `ConstructionUtils.cs`, `ConstructionTools.cs`, `RoadOrderGenerator.cs`, `CityToolbarLogic.RoadItems`, `rules/construction.yaml`, `genworld.py` | (1) `scenario=roads`: build 10 street + 10 avenue cells: funds drop by 10x10 + 10x18; (2) replace 5 streets with avenue pays `18 - 5` per cell; (3) after one month expenses include per-type upkeep (hand sum); (4) old order with no `ExtraData` builds `street` (old saves/replays still valid); (5) same stats on replay |
| **P2 Direction and control** | `dirFlags`, `sideBlock`, `Connects`, `CanEnter`; one-way tool with Tab flip; arrows overlay; junction control default rules and signal/stop orders; traffic uses `CanEnter`; road speed % | `RoadLayer.cs`, `TrafficManager.cs`, new `Orders/RoadToolOptions.cs`, `road-arrows` and `road-control` art | (1) one-way east street: log counter of vehicles moving west on it = 0 over 2,000 ticks; (2) a loop with one one-way cell still routes a car around the loop; (3) signal junction: cars on the red axis do not enter in sampled ticks; (4) all-`street` network speed equals current behaviour (regression of throughput within 5%) |
| **P3 Paired roads, highways, ramps, zoning** | paired placement with partner polyline; `highway` type; ramps and interchange tool; `ZoningCells`; `GivesAccess`; `GetAccessCell`; median overlay; map `highway-*` actors use paired highway | `RoadLayer.cs`, `ZoneLayer.cs`, `CityManager.Daily.cs`, `TrafficManager.cs` endpoints | (1) cells next to a highway are not zoneable and `CanZone` false; (2) building whose only adjacent road is highway has `HasRoadAccess = false` and is flagged; (3) a ramp links a street to the highway and a car completes the trip; (4) no ground road can attach to a highway without a ramp (`Connects` unit check); (5) divided avenue pair has `sideBlock` and no vehicle crosses the median |
| **P4 Utility graph (components)** | `UtilityNetwork` world trait; LV on roads; `PowerLineLayer` and HV tool; transformer building; `PowerPercent`; remove city-wide pool; info view overlay for cable cells | `Traits/World/UtilityNetwork.cs`, `PowerLineLayer.cs`, `CityManager.Daily.cs`, `InfoViewLayer.cs`, `rules/services.yaml` | (1) plant on an unconnected road island powers only that island; (2) bulldozing the one connecting road cell blacks out the far district within 1 day; (3) a plant joined by HV + transformer powers a district that has no plant on its roads; (4) totals equal the old pool result on a fully connected map |
| **P5 Capacity flow and batteries** | max-flow solver, per-cell caps, chain compression, bottleneck display, `battery` building | `UtilityNetwork.cs`, `InfoViewLayer.cs`, art | (1) a 10-cell trunk with `LvCapacity` 60 feeding 100 units of demand serves exactly 60; (2) adding a parallel road (second path) lifts it to 100; (3) battery discharges during a deficit and charge never goes negative or above max; (4) solver time budget under 5 ms per solve on 5,000 road cells (stopwatch log) |
| **P6 Water and sewage** | pipes layer and tool; `SewageUse`; outlet and treatment plant; pump contamination; `HasSewage` and happiness term | `PipeLayer.cs`, `UtilityNetwork.cs`, `CityBuilding.cs`, `rules/services.yaml`, fluent | (1) pipe-only connected building (off-road) gets water; (2) sewage above sink capacity sets `HasSewage = false` and happiness drops; (3) pump next to an outlet loses 50%; (4) gravel roads carry no pipes: a house on gravel only is flagged until a pipe is laid |
| **P7 Trade and outside connections** | `OutsideConnection.Kind`; power/water/sewage connections with import/export and monthly settlement; trade report in budget | `OutsideConnection.cs`, `UtilityNetwork.cs`, `CityBudgetLogic.cs` | (1) deficit of 100 with import capacity 150 and no plants: no blackout, monthly expense equals 100 x import price; (2) surplus of 80 exports and income appears; (3) cutting the HV line to the edge cancels trade; (4) sewage export works, import refused |
| **P8 Layers and polish** | bridges over water and roads (`Elevated`, `underType`), tunnels and underground view, roundabout prefabs, addons (trees, barriers, lights, bike, parking, bus lane), road wear and depots, noise map, rail/sea/air hooks | many | roundabout 3x3: all ring cells one-way counter-clockwise and cars complete a loop; bridge over a river connects both banks and a street runs underneath without connecting; tunnel hidden on surface but visible in underground view; sound barrier lowers cell noise by 50% |

Ordering note: P1 and P2 unblock the traffic domain (speed, direction). P4 unblocks the services domain. P3 should land before the zoning/people domain relies on access cells.

---------------------------------------------------------------------------------------------------

## 6. UI and art needs

All art is procedural Python (`tools/genworld.py`, `genui.py`). 32x32 cells unless noted.

**World art (`bits/world`, `sequences/misc.yaml`):**
- `roads-street`, `roads-gravel`, `roads-avenue`, `roads-boulevard`, `roads-highway`, `roads-alley`: 16 frames each, same mask order as `roads.png` (existing `roads.png` stays as `street` fallback). Gravel: sandy colour, no sidewalks; avenue/boulevard: wider asphalt, 2 or 3 dashed lines; highway: dark asphalt, solid edge lines, guard rails, no sidewalk.
- `road-arrows` 4 frames; `road-median` 16 frames (by blocked mask, grass/trees/kerb strip); `road-control` 4 frames (none, stop, signal, island); `road-addons` about 24 frames (trees x4, lights, barrier, parking, bike, bus x4 sides); `road-bridge` 2 frames + shadow, `road-ramp` 4 frames; `roundabout-island` 2 sizes.
- Utilities: `powerline` 16 frames (pylon plus wire by mask, drawn over roads), `pipe` 16 frames x 3 variants (water, sewage, dual; dashed blue/brown/both) for the underground view, `transformer` (2x2, 64x96), `battery` (1x1), `sewage-outlet` (1x1), `treatment-plant` (2x2), `gaspower`/`nuclear` later. Add `overlays: grid` variants for network heat (reuse `heat` 11 frames).
- Status icon: add frame 6 "no sewage" (brown drop) to `statusicons` (16x16), frame 7 "no connection to grid" for plants.

**Chrome (`city-icons`, 32x32 and D2k order-icon 34x35 style):** `road-street`, `road-gravel`, `road-avenue`, `road-boulevard`, `road-highway`, `road-oneway`, `road-replace`, `road-signal`, `road-roundabout`, `road-interchange`, `power-line`, `pipe`, `transformer`, `battery`, `sewage`, `networks` (tab). Fluent keys for tooltips: name, cost, upkeep, speed, lanes, noise ("Noise: High").

**UI behaviour:** the road tab shows type icons with unlock text and a mode row (draw, one-way, replace, control, roundabout, interchange); the cursor cost label also shows type and lanes. A new **Networks** tab holds the HV line, pipe tool, transformer, battery, outlet and plants and auto-activates the grid info view. The top bar shows power and water as `supply/demand` with a "lost" flag. Selecting a cable or pipe cell shows flow, capacity and satisfaction.

---------------------------------------------------------------------------------------------------

## 7. Sources

- Paradox, CS2 Feature Highlight: Road Tools: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/road-tools
- Paradox, CS2 Feature Highlight #6: Electricity & Water: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/electricity-water
- Paradox forum Dev Diary #1 Road Tools / #2 Traffic AI (via summaries): https://forum.paradoxplaza.com/forum/developer-diary/development-diary-1-road-tools.1590300/ and https://forum.paradoxplaza.com/forum/developer-diary/development-diary-2-traffic-ai.1591141/
- Paradox, Detailer's Patch #2: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/detailers-patch-2
- CS2 wiki, Roads: https://cs2.paradoxwikis.com/Roads (cost, upkeep, speed, zoning, noise table)
- CS2 wiki, Services: https://cs2.paradoxwikis.com/Services (electricity, water, sewage, import/export text)
- CS2 wiki, Economy: https://cs2.paradoxwikis.com/Economy (trade prices)
- CS2 wiki, Zoning: https://cs2.paradoxwikis.com/Zoning (lot sizes)
- CS2 wiki, Map Creation: Outside Connections: https://cs2.paradoxwikis.com/Map_Creation:_Outside_Connections
- CS2 wiki, Traffic: https://cs2.paradoxwikis.com/Traffic
- Beef Suplex Wiki, CS2 road construction guide (speed limits, mph): https://wiki.beefsuplex.com/wiki/Cities:_Skylines_2/Road_Construction_Guide
- Steam guide "Practical Engineering: Efficient Grids" (8 m cells, 6-cell depth): https://steamcommunity.com/sharedfiles/filedetails/?id=3062339423
- Steam discussion threads on 40 MW / 400 MW capacity and bottlenecks: https://steamcommunity.com/app/949230/discussions/0/3877095833488782449/ and https://www.gamepressure.com/newsroom/how-to-deal-with-electricity-bottleneck-in-cities-skylines-2/zc62bb
- Power plant numbers: https://www.modscities2.com/the-best-power-plant-in-numbers/ and https://www.destructoid.com/the-best-types-of-power-electricity-in-cities-skylines-2/
- Water numbers: https://www.thegamer.com/cities-skylines-2-guide-to-provide-water/
- Intersection types: https://gamerant.com/cities-skylines-2-how-edit-intersections/
