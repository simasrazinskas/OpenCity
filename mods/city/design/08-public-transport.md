# 08 - Public transport, pedestrians and taxis (OpenCity design)

Research domain 8 of the CS2-parity effort. Scope: bus, tram, train, subway, taxi, ferry/harbor, airport, line tool UX, passenger mode choice, line economics, pedestrians, bicycles. Written against the code at branch `t3code/openra-city-builder` (commit `b4b7e7ee70`).

Confidence tags used below: **[W]** = from the CS2 wiki or an official Paradox page, **[C]** = community or forum report (not official), **[CS1]** = known only from Cities: Skylines 1 and not confirmed for CS2, **[D]** = my design decision (not a CS2 fact).

---

## 1. CS2 mechanics

### 1.1 The one pipeline for all modes

Paradox standardised the "line tool" across every land mode, passenger and cargo. The order is always: **depot, then stops/stations, then tracks/roads, then lines** [W: dev diary #3]. Compared with CS1:

- Every land mode now has a depot (bus depot, taxi depot, tram depot, subway yard, rail yard). In CS1 only buses, taxis and trams had depots; trains and metros spawned from stations. Each depot supports a fixed vehicle count that an "extra garage" upgrade raises [W].
- Modes are unlocked with **development points**, not population milestones (bus and taxi still arrive with early growth) [W, PC Gamer summary of the diary].
- The line tool lets you place **waypoints** between stops for road vehicles. Waypoints steer the path (avoid a busy road) but are not stops. Stops must sit on or be dragged onto a stop building. Lines can be edited later by dragging stops [C: Steam threads].
- Lines are one-way or two-way loops. A one-way loop only serves the side of the road the vehicle drives on, so players place stops on both sides and often run A-B-C-D then back D-C-B-A [C, CS1 tip that carries over].
- Per-line controls: **ticket price, vehicle count, operating hours (day / night / both), colour, name**. Line stats: length, number of stops, current passengers, **usage %** [W]. A Transportation Overview panel lists all lines with usage; the Transportation info view shows lines, tourists carried and passengers per month by type [W].

### 1.2 Per-mode facts

All costs are CS2's own "Moneys" at wiki values; use them only for relative scale.

| Mode | Depot / yard | Station or stop | Vehicles and capacity | Notes |
|---|---|---|---|---|
| Bus | Bus Depot: 25 vehicles, 150k build, upkeep 23k-62k/mo (sources disagree) [W/C]. Extra Garage +10 vehicles. Compact Depot (City Stations DLC) holds 10 | Bus stop on a road. Bus stop makes a partial bus lane, removes parking up to about 150 m before and after [C]. Bus stations: 3 to 6 platforms, 100k-200k | Bus capacity not found for CS2. CS1 was 30 [CS1]. Electric bus variant via depot upgrade | "Cheap and flexible", "very much affected by traffic" [W]. Intercity buses to outside connections need a depot, and can serve a bus station or ordinary stops [W] |
| Taxi | Small depot 8 vehicles (35k). Taxi Depot 25 vehicles (100k) | Taxi stand | Low capacity | **No lines.** Taxis answer requests from citizens and tourists. Idle taxis drive to the nearest stand and wait while they have shift time. Dispatch Center upgrade removes the "stand only" limit so taxis can pick up anywhere. Taxis also appear without depots, arriving from outside [W/C] |
| Tram | Tram Depot: 10 vehicles (+5 upgrade), 100k | Tram stop or station, 2 platforms | Capacity not found [CS1: 90] | Tracks are **added to an existing road** with the Replace tool, or built on terrain. Prices around 625 per km one-way, 1,250 per km double [C: TheGamer]. Quiet, no air pollution [W] |
| Subway | Subway Yard 10 vehicles (200k), Terminal Yard 5 | Underground station, 2 platforms | Cars around 1,080 per train [C, unofficial] | Exclusive tracks, tunnels, compact on the surface [W] |
| Train | Rail Yard 10 vehicles, Railway Depot 8 | Small station (2 platforms, 75k), Train Station (3 platforms, 125k) | Around 840 per train [C, unofficial] | Needs track connected to the map edge. Single, double and one-way tracks, switches by combining them, bridge, tunnel, elevated [W] |
| Ferry / ship | Harbor (1 pier, 420k) | Ferry stop | Ferry carries 100 [W] | Seaways to the map edge; harbors must connect to them. Cargo ships carry 1,000 t [W] |
| Plane | Airport 5 gates (750k), International 18 gates (4M) | Gate | Low capacity, high speed [W] | Air lines need no infrastructure beyond airports. Large airports limit building height under approach paths [W] |
| Cargo | Cargo Train Terminal, Cargo Harbor (15.5 kt storage), airport cargo upgrade | n/a | Ship 1,000 t | Terminal acts like a warehouse with an extra connection. Early-2023 reports said exports often did not leave [C, about 1,000 days old] |

### 1.3 Outside connections and tourism

- To get passengers or tourists from outside you must **draw a line to the outside-connection icon** at the map edge and return, forming a loop. A station alone brings nobody [C: Steam, confirmed by the Paradox diary text "trains need to be connected between the city and the existing train infrastructure"].
- Rail to the edge may need purchased map tiles first [C].
- Buses, trains, ships and planes all bring both immigrants and tourists. Tourists stranded at a station with no local PT leave unhappy; players add a subway or bus feeder [C].
- Citizens can also leave for leisure through an outside connection by car or PT as a household [W: Citizens page].

### 1.4 How a citizen chooses (the part that matters most)

CS2 gives every agent a **pathfinding cost with four parts: Time, Comfort, Money, Behavior** [W: Traffic AI diary, Traffic wiki].

- **Time** dominates for adults. **Money** dominates for teens. **Comfort** dominates for seniors (smooth route, stop or parking near the destination) [W].
- Ticket price feeds directly into the Money part. Money "can sway citizens between public transport and a private vehicle" [W].
- Walking is a candidate in the same comparison. There is no documented hard cap on walking distance; players report cims walking from anywhere if the bus saves time [C]. CS1 had a hard 1,024 m (128 cell) cap [CS1].
- Stops are pathfinding targets exactly like parking spots. Comfort adds the cost of the stop or parking spot to the path cost [W].
- Vehicle count affects **waiting time**, which affects pathfinding. If a line is near 100% usage, adding vehicles shortens waits [W].
- Transfers are not documented as an explicit penalty in official text. Wait time and comfort are the likely paths [inference]. A mod ("Realistic PathFinding") exists that adds wait, crowding and transfer preferences, which suggests vanilla under-weights them [C].
- Ticket default is around 8 (per-line slider, max 50) [C: one forum thread, one mode unspecified]. Setting a line free gets 40-50% usage on some lines [C]. Fares did not move ridership much in one forum test, so the Money weight is probably modest [C].
- Subways attract time-sensitive riders because of speed [W].

### 1.5 Economics

- Depots are the largest fixed cost; running cost per vehicle is small next to it. Players aim for break-even and use PT to reduce traffic rather than to earn [C, CS1 guides, consistent with CS2 reports].
- Fares are income; vehicle upkeep, depot upkeep and station upkeep are expenses. Lines show usage % rather than a single profit number in the in-game panel. The Economy panel aggregates by service [W/C].
- Low usage is common: players report 0-26% on whole networks when stops are badly placed [C].

### 1.6 Pedestrians and bicycles

- Pedestrians are agents on sidewalks and pedestrian paths. Walking is compared in the same cost function as car and PT [W].
- Bicycles arrived in a free update. Teens, adults and seniors can cycle; children cannot. **Every household has enough bikes for all members** (no ownership limit like cars). Whether a citizen cycles depends on car availability, bicycle parking in the city, and pathfinding cost. Standalone bike paths have the highest comfort, then on-road lanes. Parking sizes: stand 2, stands 8, shelter 10, storage building 16 up to hall 452+. Policies: Urban Cycling Initiative, Bicycle Restriction [W: dev diary "Building for Bicycles"].
- A citizen with a car compares walk / drive / transit. Without a car they compare walk / cycle / transit [W].

### 1.7 Not found

Per-mode CS2 capacities, default fares per mode, the exact transfer penalty and the vehicles-per-line formula (the wiki says only "depends on the line's length") are unpublished. Numbers labelled [D] in section 3 are tuned design values, not measurements.

---

## 2. Current OpenCity state and gaps

### 2.1 What exists (verified in code)

- **Roads**: `RoadLayer` (`Traits/World/RoadLayer.cs`) stores one road per cell in a `CellLayer<byte>`, sets `Map.CustomTerrain` to the `Road` terrain index, autotiles by a 4-bit N/E/S/W mask, and exposes `NetworkVersion`, `RoadChanged`, `AdjacentRoadCells`, `IsConnectedToOutside`. Highway cells are locked (cannot be removed).
- **Vehicles**: `TrafficManager` (`Traits/World/TrafficManager.cs`, 629 lines) spawns trips as real actors (`car-a..d`, `truck`, `bus`) with `Mobile` on locomotor `road` (`SharesCell: true`, speed 96 wu/tick for cars, 76 for `bus`), a private 4-connected BFS `FindPath`, `mobile.MoveTo(pathFunc)` then `RemoveSelf`. Cap is `min(200, Population / 6)`. Per-cell load is sampled every 5 ticks and decays x3/4 every 25 ticks, exposed as `GetTrafficLoad(cell)` (0..100). `WithLaneOffset` draws vehicles 6 px to the right of travel so opposing traffic separates visually.
- **The "bus" today is fake**: `BusChance = 4` makes 4% of ordinary home-to-job trips look like a bus. It carries nobody, has no stops, no line and no ticket.
- **People are aggregate**: `CityBuilding.Residents` and `Workers` are integers per building; `CityManager` (`Traits/Player/CityManager.cs` and `Traits/Simulation/*`) computes demand, tax and upkeep daily from those counts. There is no per-citizen record yet (domain 1 will add one).
- **Money**: `CityManager.TrySpend(amount, category)` and `AddFunds(amount, category)` record per-category lines in the monthly ledger. Upkeep today is `RoadCellCount x UpkeepPerCellPerMonth` plus the sum of `CityBuilding.Info.Upkeep` of operational non-zoned buildings (`FillJobsAndCollect`). Transit upkeep can ride on the same "services" bucket via `CityBuilding.Upkeep`, or use a new ledger key.
- **Placement**: services are `CityPlaceable` actors placed by `PlaceCityBuildingOrderGenerator` and the `CityPlaceBuilding` order (`ConstructionTools.PlaceBuilding`): cost check, unlock check by `UnlockPopulation`, footprint check via `ConstructionUtils.CheckPlacement` (supports `RequiresRoad` and `RequiresTerrainNearby`, e.g. waterpump needs water within 2 cells).
- **Tools**: `CityDragOrderGenerator` is the shared press-drag-release base with a hover cell, `Marker(cell, color)` previews and `Label` text. Click-sequence tools (as a line tool needs) must implement `IOrderGenerator` directly like `PlaceCityBuildingOrderGenerator` does.
- **UI**: Dune 2000 style sidebar. `ingame-player.yaml` has `CITY_TABS` buttons (`TAB_ROAD`, `TAB_ZONING`, `TAB_POWER`, ... `TAB_PARKS`), a `CityPalette` icon grid, order buttons (bulldoze, infoviews, budget) and `CityBuildingInfoLogic` for the selected building. Info views are the frozen `CityInfoView` enum rendered by `InfoViewLayer`.
- **Frozen files**: `CityTypes.cs` (the order string constants, `CityService`, `CityInfoView` enums) and `CityAutoTest.cs` are frozen per `CONTRACT.md`. New orders must live in a new file; new overlays must not extend the frozen enum without the lead's approval.

### 2.2 Gaps against CS2 (public transport domain)

| Gap | Impact | Effort |
|---|---|---|
| No stops, lines, depots, tickets | No PT at all | Core of this document |
| No passenger model: no waiting queues, boarding, alighting | Cannot carry anyone | Core |
| No mode choice: every trip is a car trip (or a fake bus) | PT cannot relieve traffic | Needs a planner API (section 3.5) shared with domain 1 |
| No second network layer: rails cannot exist; roads are one per cell | No trains, trams-on-reserved-track, rail cargo | RailLayer (section 3.8) |
| No pedestrian model | Walk legs cannot be costed or shown | Walk cost model plus cosmetic renderer |
| Per-cell traffic load exists but nothing reads it for travel time | Congestion cannot slow buses or push people to PT | One read in the planner and the bus driver |
| `BusChance` random buses count against the vehicle cap | Wasted actors, misleading visuals | Set to 0 when PT ships (integration item for domain 2) |
| No outside rail/sea/air connections and no arrivals | No tourism or intercity PT | P4-P5 |
| `CityInfoView` frozen, no transit overlay | Cannot show lines | Separate local overlay trait owned by the transit package |
| No fares in the ledger | PT cannot earn or cost money | New ledger keys `fares-*`, `upkeep-transit` |

### 2.3 Constraints that shape the design

1. **2D grid, one road per cell.** A stop cannot occupy a second layer on the same cell as a lot, so it must attach to a road cell as data plus an overlay sprite, not as a lot-eating building.
2. **Deterministic integer sim.** Orders alter state; ticks only derive from state. No floats in synced state. Iterate by stable id.
3. **Vehicles as actors, limited count.** Cars and buses already use `Mobile`. Hundreds of extra actors cost pathfinding and render time; the design caps PT actors and uses data-only vehicles where they do not need to interact with cars.
4. **One road network graph, shared.** Domain 3 should expose road-graph search so transit does not copy `TrafficManager.FindPath` (currently private).
5. **Viewport-independent sim.** Spawning actors "only when visible" would desync clients; any lazy rendering must be cosmetic and derived from synced state.

---

## 3. Proposed design

### 3.1 What is feasible in 2D, and in which order

| Mode | Feasible in 2D grid? | Network it runs on | Vehicle representation | Phase |
|---|---|---|---|---|
| Bus | Yes, trivially | Existing roads | Actor (`bus`, `Mobile`, road locomotor) | P1 |
| Taxi | Yes | Existing roads | Actor (`taxi`) | P2 |
| Tram | Yes: a track flag on existing road cells, no new layer | Roads with a `Tram` flag | Actor (`tram`) on road locomotor, route restricted to flagged cells | P3 |
| Metro (subway) | Yes, and cheaper than surface rail: stations are 1x2 buildings; tunnels are L-shaped polylines between consecutive stations drawn as a thin overlay and consuming no cell | None (virtual tunnel graph) | Data-only, drawn by the transit renderer | P4a |
| Train | Yes, needs a rail layer (cells with tracks, 16-mask autotile, mutually exclusive with road except straight level crossings) | `RailLayer` (new) | Data-only trains of 1 to 4 cars | P4b |
| Cargo rail, harbor, airport | Yes as buildings plus abstract arrivals; ships and planes are cosmetic sprites on straight paths | Rail layer, water edge, none | Data-only | P5 |
| Bicycles | Yes as a cost-model mode; bike lanes as a road flag | Roads (flag) | Cosmetic only | P6 |

Rule of thumb [D]: **anything that shares cells with cars is an actor so congestion and lane use are real; anything on its own network is data-only so it scales and avoids a second pathfinder.**

### 3.2 Data model (new world trait `TransitLayer`)

One new world trait owns all transit state. All state is plain ints and lists, mutated only by order handlers and by its own `ITick`. Stable ids come from per-type counters (never reused within a session).

```csharp
public enum TransitMode : byte { Bus, Taxi, Tram, Metro, Train, Ferry, Plane }

sealed class Stop    // Id, Mode, Cell, StationActorId (0 for road stops), Waiting (List<PaxGroup>),
{                    // WaitingCount, BoardedThisMonth, AlightedThisMonth, SumWaitTicksThisMonth
}

struct PaxGroup      // LineId (0 = taxi), AlightStopId, Count (1 for citizens), CitizenId (0 = aggregate),
{                    // ArrivalTick, FinalDest (packed CPos, for transfers)
}

sealed class Line    // Id, Mode, ColorIndex, Name, StopIds (first == last means loop), TicketCents (0..500),
{                    // TargetVehicles, AutoVehicles, VehicleIds, ThisMonth/LastMonth LineStats
                     // derived on network change: Path (one full cycle), StopPathIndex, CycleTicks, Broken
}
```

Depots and stations are **actors** placed with the existing `CityPlaceable` pipeline so placement, refund, bulldoze, upkeep, status icons and selection reuse existing code:

- `busdepot` (3x2), `taxidepot` (2x2), `tramdepot` (3x2): `CityPlaceable` with `RequiresRoad`, plus a new trait `TransitDepot` (mode, `FleetCapacity`, optional upgrades). The depot's spawn cell is its first road-adjacent cell (same lookup as `TrafficManager.TryFindAccessRoad`).
- `metrostation` (1x2), `trainstation` (2x4), `harbor` (3x3, needs `RequiresTerrainNearby: Water`), `airport` (5x4): `CityPlaceable` plus `TransitStation` trait registering one or more stops.
- Road stops (bus, tram, taxi stand) are **not actors**. They are records in `TransitLayer` anchored to a road cell and drawn as a small overlay by the layer (same pattern as `RoadLayer`'s `TerrainSpriteLayer`). This keeps lots free and lets a stop sit on a road cell as in CS2. A stop's anchor is a road cell with a side (0..3, index into `CityUtils.Neighbours4`) used only for the sprite.

Everything else (waiting queues, vehicles, stats) lives in `TransitLayer` and is hashed into `[VerifySync] int StateHash` (sum of stop ids x waiting counts, vehicle positions and line ids) so desyncs show up in OpenRA's sync check.

### 3.3 Orders (new file `Orders/TransitOrders.cs`; `CityTypes.cs` stays frozen)

All issued by `player.PlayerActor`, resolved by a new `Player` trait `TransitTools : IResolveOrder`. `Order` carries `TargetString`, `ExtraData` (uint), `ExtraLocation` (CPos), so id lists travel as comma-separated strings.

| Order string | Payload | Effect |
|---|---|---|
| `TransitPlaceStop` | Target cell, `TargetString` = mode | Validates road cell, no duplicate stop in the cell, pays stop cost, creates stop |
| `TransitRemoveStop` | `ExtraData` = stop id | Removes stop; lines containing it drop the stop and re-route (or are deleted if fewer than 2 stops remain) |
| `TransitCreateLine` | `TargetString` = `"mode;loop;1,5,9"`, `ExtraData` = color | Requires a depot of that mode with free fleet slots; computes the path; line starts with vehicle target 1 |
| `TransitEditLine` | `ExtraData` = line id, `TargetString` = new stop list | Insert, remove or reorder stops (line panel and tool use the same order) |
| `TransitSetLine` | `ExtraData` = line id, `TargetString` = `"price=150;vehicles=4;auto=0"` | Ticket price, target vehicle count, auto mode, name |
| `TransitDeleteLine` | `ExtraData` = line id | Vehicles finish the current leg then return to depot and despawn; waiting passengers re-plan |
| `TransitBuildTrack` | Target and `ExtraLocation` (drag), `TargetString` = `"tram"` / `"rail"` | Marks road cells with tram track, or lays rail cells (P3/P4b) |
| `TransitSetTunnelLine` | stop list | Metro: pays per tunnel cell |

Money flows through `CityManager.TrySpend(cost, "transit-construction")` and refunds through `AddFunds(..., "refund")`, as `ConstructionTools` does. Each handler must tolerate a missing `CityManager` (the Neutral shellmap player), as the robustness rules in `CONTRACT.md` demand.

### 3.4 Routes: how a line becomes a path

- When a line is created or edited, or whenever `RoadLayer.NetworkVersion` changes, `TransitRouter` rebuilds `Line.Path`: for each consecutive stop pair it runs a breadth-first search over road cells (bus) or road cells with the tram flag (tram), using the same N/E/S/W neighbour order as `TrafficManager.FindPath` so ties break identically on every client. Non-loop lines run the path forward then reversed (ping-pong); loops append the leg from the last stop back to the first.
- **Waypoints** (CS2) become an optional extra: a stop-like intermediate "via cell" in the ordered list (`StopIds` entries with a negative id encode via cells). MVP skips them; the BFS path is deterministic and predictable. [D]
- If any leg has no path, `Line.Broken = true`, vehicles are recalled, and a transient notification ("Line N is cut: road missing") is shown through `TextNotificationsManager.AddTransientLine`.
- `CycleTicks` = sum of per-cell ticks. Bus per-cell time = `1024 / busSpeed` (76 wu/tick: about 13.5 ticks) scaled by `(100 + load/2)/100` where `load` is `TrafficManager.GetTrafficLoad(cell)`; stops add the dwell estimate. Used for fleet sizing and for the planner's ride cost.
- One shared BFS helper is needed. Propose that domain 3 exposes `IRoadGraph.TryFindPath(from, to, predicate, buffer)`; until then `TransitRouter` carries its own copy with a visited-stamp `CellLayer<int>` like `TrafficManager`.

### 3.5 Passenger model and mode choice

**One planner, used by citizens (domain 1).** `TransitPlanner.TryPlan(in TripQuery q, out Journey j)` returns the best PT journey (or taxi) for an origin and destination, and `EstimateWalk` / `EstimateCar` helpers return comparable costs. The citizen layer picks the lowest total cost among car, PT, taxi, walk (and bike in P6). This mirrors CS2's single cost comparison [W].

Cost in integer "ticks-equivalent" (1 unit = one tick of travel time) [D]:

```
cost = time + comfort + money * MoneyWeight[age] / 100 + transfers * TransferPenalty
time    = walkIn*WalkTicksPerCell + wait + ride + dwell*stopsPassed + walkOut*WalkTicksPerCell
wait    = max(headway/2, observedAvgWait[stop,line])        // observed from stop stats, capped by 600
comfort = crowdedPenalty (load > 80%: +40) + stopCrowdPenalty (waiting > 30: +20)
          + modeComfort[mode] (bus 0, tram -10, metro -20, train -10; negative = nicer)
money   = TicketCents (PT), FareCents (taxi), fuel+parking cents per cell (car)
```

Defaults [D]: `WalkTicksPerCell = 42` (about a quarter of car speed, since a car crosses a cell in about 11 ticks at speed 96), `TransferPenalty = 60`, `MoneyWeight` per age group: teen 220, adult 100, senior 60, with `ComfortWeight` per age: teen 70, adult 100, senior 200 (mirrors the CS2 age weighting [W]). Car cost uses the drive-time estimate along the road path with `1 + GetTrafficLoad(cell)/150` per cell, plus a parking walk of 3 cells. Taxi cost: wait (nearest idle taxi ETA or stand queue) + drive + fare, with money weight as for PT.

**Candidate stops.** Each end gets up to 3 stops within `WalkRadius = 10` cells (Manhattan), taken from a cached `StopCoverage` layer (nearest stop ids per cell, rebuilt on stop or network change, multi-source BFS bounded by the radius). Total walking per journey is capped at `MaxWalkCells = 24`; beyond it the journey is invalid (a performance guard, not a CS2 rule). Walking itself is a candidate "mode" with the same cost function and a hard limit of `MaxWalkOnlyCells = 20`.

**Journey search.** Dijkstra over nodes `(stop, line)`: ride edge to the next stop on the same line (leg ticks + dwell); transfer edge to another line at the same or a nearby stop (`<= 4` cells walking, plus `TransferPenalty` and the next line's wait); board edge from the origin walk. With up to about 200 stops and 30 lines this is a few thousand edges per query. Cache per destination stop (version-stamped by `TransitLayer.Version`) and cap at 8 new plans per tick, served FIFO by request id.

**Queues, boarding and giving up.** A planned passenger becomes a `PaxGroup` in the origin stop's queue. A vehicle arriving at a stop alights every group whose `AlightStopId` equals that stop (final-destination groups raise `OnArrived` to the citizen layer; transfer groups re-enter the stop queue with the next line), then boards groups for its line in FIFO order until capacity. Groups waiting longer than `GiveUpTicks = 1500` (about 60 in-game days) leave the queue, are charged a "stood up" happiness hit through the citizen API, and re-plan without that line. Per-stop cap 60 groups; beyond it the planner adds the crowd penalty and then refuses the stop.

**Aggregate fallback.** Until individual citizens exist, a daily sampler (domain 5's population numbers) generates `k = residents/25` aggregate groups per residential building, picks a random workplace, runs the same planner and the same car-versus-PT comparison, and enqueues groups with `Count > 1`. The vehicles, queues, ticket revenue and statistics are identical either way, so P1 does not block on domain 1.

### 3.6 Vehicles: depots, fleet size, scheduling, dwell

- **Fleet and depot.** `TransitDepot.FleetCapacity` (bus 12, upgrade +6 for `busdepot-garage`; taxi 8; tram 6) is the shared pool for all lines of that mode served by this depot. A line draws vehicles from its nearest depot by path distance.
- **Fleet size per line.** Player slider `TargetVehicles` (1 to `MaxByHeadway = CycleTicks / 40`, so the minimum headway is 40 ticks). Auto mode [D]: every 5 days, `+1` if the average load over the last 10 days exceeds 85%, `-1` if below 25% and more than 1 vehicle, starting from `ceil(CycleTicks / 300)` (target headway 300 ticks, about 12 days).
- **Spawn and recall.** `TransitLayer.Tick` spawns at most one vehicle per line per 20 ticks while `VehicleIds.Count < TargetVehicles`, at the depot's access road cell via `world.AddFrameEndTask`. A vehicle above target finishes its cycle at the first stop, then drives to the depot and is disposed. Both use `AddFrameEndTask`, as `CONTRACT.md` requires.
- **Driver activity.** A new `TransitRun : Activity` per vehicle replaces `TrafficManager`'s random trip: for each leg it calls `mobile.MoveTo(pathFunc)` with the precomputed `Line.Path` slice (no per-vehicle BFS), then at a stop cell runs the **stop routine**: alight, board, dwell `DwellBase 12 + 2 x (boarded + alighted)` ticks (cap 60), mark departure. Dwell uses `Activity` waiting, not blocking the cell (`SharesCell` allows overtaking).
- **Bunching control.** A vehicle may not leave a stop earlier than `headwayTarget / 2` after the previous departure of the same line from that stop (deterministic hold). This is the same idea Transit Timetables-style mods add to CS2 because vanilla bunches [C].
- **Congestion coupling.** Bus speed is the `Mobile` speed, so it is slowed by blocked subcells and queues the traffic domain adds. The driver also adds `+30 load` to the stop cell while dwelling (via `TrafficManager`'s load layer) so heavily used stops show up red in the traffic view.
- **Actor budget.** PT actors are capped at `MaxTransitActors = 80` (bus plus taxi plus tram), counted separately from `TrafficManager.MaxVehicles` so a full city of cars cannot starve buses.
- **Stuck vehicles.** Reuse `StuckTicks` semantics: a vehicle that has not moved for 150 ticks is recalled to its depot (not disposed in place), so passengers on board are re-queued at the next stop instead of lost.

### 3.7 Line economics and effect on traffic

- **Fare.** On boarding, `TicketCents` of the line is added to `ThisMonth.FareCents`. At month end `TransitLayer` credits `AddFunds(fareCents / 100, "fares-bus")` (carrying remainder cents) so integer funds stay exact.
- **Costs.** Per vehicle per month `RunningCents` when active (bus 1,200 cents, tram 2,000, metro car 4,000, train 6,000 [D]); depot, station and track upkeep through `CityBuilding.Info.Upkeep` (already summed by `CityManager`); stops 200 cents per month. Line profit shown in the panel = fares - (vehicle running + stops on that line / lines sharing the stop).
- **Calibration target [D]:** a bus line with usage around 50% should earn 60-120% of its running cost at the default fare, and a free line should add 30-50% ridership (matches the CS2 report). `FareScale` is one tunable constant to match the citizen layer's trip rate: with 3 trips per citizen per day and 25 ticks per day a naive fare of $1 would out-earn taxes, so the default fare is about $0.20-0.40 per boarding until domain 1 fixes the trip rate. The acceptance test checks the ratio, not absolute dollars.
- **Effect on traffic.** Every PT trip chosen by the planner **removes one car trip** from the citizen layer (the citizen does not request a car; `TrafficManager` never sees it). Buses count as `LoadPerSample = 20` instead of 10 on road cells (they are bigger), so they add congestion on corridors but one bus replaces up to 30 cars. The transit overview shows "car trips avoided this month" = boardings x `CarShareOfTripsReplaced (0.8)`.
- **Taxis** do the opposite: they add vehicles and each trip is both a car trip and an empty-return leg, as in CS2 ("contribute to congestion" [W]).
- **Free PT / pricing levers.** Ticket slider 0 to $5 per line is the only lever in P1. A "free transit" policy is not in CS2's published text for CS2 and is out of scope [D].

### 3.8 The other modes, pedestrians and bikes

- **Taxi (P2).** `taxidepot` (8 taxis), `taxistand` stops, `TaxiDispatcher` inside `TransitLayer`. Request from the planner: pick the nearest idle taxi by Manhattan distance among the fleet (at most 25 candidates), then one BFS for the chosen taxi only (CS2's dispatcher is a known pathfinding hog [C], so no per-candidate BFS). Stand-only pickup in P2 (walk to a stand within `WalkRadius`); a `taxidepot-dispatch` upgrade enables door-to-door. Idle taxis return to the nearest stand. Fare = `300 + 40 x cells` cents; income to the city ledger (`fares-taxi`) [D, CS2 ledger treatment unconfirmed].
- **Tram (P3).** `TransitBuildTrack` sets a `Tram` bit in a `CellLayer<byte>` on existing road cells (cost 60 per cell); rendered as a rail overlay above the road (16-mask autotile). Lines may only route over tracked cells; trams are actors on the road locomotor so they share lanes with cars. Higher capacity (90) and comfort bonus than buses; one depot per 6 trams.
- **Metro (P4a).** `metrostation` (1x2). `TransitSetTunnelLine` builds tunnel polylines (L-shaped, `|dx|` then `|dy|`) between consecutive stations at 80 per cell; rendered as a thin dashed overlay only when the transit overlay is on. Trains are data-only: position = (segment index, progress in 1/1024 cell), drawn by a transit renderer. Capacity 240 per train, speed 3x bus, dwell 16 ticks. No surface footprint beyond stations, so this is the first mass mode to ship.
- **Train and rail layer (P4b).** `RailLayer` mirrors `RoadLayer`: `CellLayer<byte> flags`, 16-mask autotile, `RailChanged` event, `NetworkVersion`. It does **not** write `CustomTerrain` (no tileset change). Rule: rail and road never share a cell except a **level crossing** (straight rail crossing a straight road, flag `Crossing`; cars wait while `CrossingClosed`, a tick-derived flag read by `TrafficManager`). `OutsideRail` actor trait (like `OutsideConnection`) lays permanent track from the map edge. Stations (2x4) hold 2 platforms. Trains are data-only, up to 4 cars (80 each, 320 per train [D]), switching only at junction cells (3+ rail neighbours). **Intercity trains** arrive automatically every `IntercityInterval` days once a station connects to edge rail (simplifying CS2's explicit "draw a line to the edge icon" [C]) and deliver immigrants and tourists; this is the outside-connection hook.
- **Harbor and airport (P5).** `harbor` (needs water within 2 cells, existing `RequiresTerrainNearby`) and `airport` register stops of mode Ferry or Plane. Vessels and planes are cosmetic sprites sliding between the building and the nearest map edge; arrival counts per month = `capacity x attractiveness`. Cargo variants expose `ICargoTerminal` (see section 4). Approach-path building height limits from CS2 are skipped [D].
- **Pedestrians (P1, cost only; P2 visuals).** Walking is a cost, not an actor: `WalkTicksPerCell` over Manhattan distance, treating road cells and free land as walkable (sidewalks exist on every road cell, per the road art). Cosmetic walkers: a `PedestrianRenderer` draws up to 60 small dots between building and stop, positions derived from `(tripStartTick, path, tick)` so no state is synced. Footpaths and pedestrian zones belong to domain 3.
- **Bicycles (P6).** Mode with speed 2x walking, comfort bonus on roads with a `BikeLane` flag, no ownership limit (matches CS2 [W]); chosen by the same planner and drawn cosmetically. No actors.

---

## 4. Interfaces with other domains

| Domain | PT provides | PT needs |
|---|---|---|
| **1 Citizens** | `ITransitPlanner.TryPlan(TripQuery, out Journey)`; `Journey{Mode, TotalCost, TicketCents, WalkCells, Transfers}`; callbacks `OnBoarded(citizenId, lineId)`, `OnArrived(citizenId, cell)`, `OnGaveUp(citizenId)`; per-citizen age group on `TripQuery` | Trip volumes and the choice itself (`min cost`), citizen position while waiting, happiness hooks for long waits |
| **2 Traffic** | Bus/tram/taxi actors; stop dwell load; `CrossingClosed(cell)` | `GetTrafficLoad`, `SetBusChance(0)` when lines exist, an `IRoadGraph.TryFindPath`, vehicle cap accounting that excludes PT actors, optional `AddLoad(cell, amount)` |
| **3 Networks** | `RailLayer` trait (same shape as `RoadLayer`); `TramTrack` flag; pedestrian-friendly cells list | Shared graph search, `RoadChanged` events, footpath layer, optional `CellLayer` for sidewalks |
| **5 Economy** | Ledger keys `fares-bus/tram/metro/train/taxi`, `upkeep-transit`, `transit-construction`; line profit stats; `CarTripsAvoided` | `CityManager.TrySpend/AddFunds`, trip rate per citizen (to calibrate `FareScale`), cargo demand, tourism income |
| **10 UI** | Tool classes, `TransitLinePanel` data (`LineSnapshot`), overlay trait, icon names | Sidebar tab, panel layout, hotkeys, info-view entry (enum is frozen) |
| **Industry / cargo** | `ICargoTerminal { Capacity, Stored, Offer(resource, qty), Take(resource, qty) }` for rail terminal and cargo harbor | Resource flows, import/export prices |

Where an interface is missing the transit package uses its own copy and lists it under "Integration notes" in its report, per `CONTRACT.md`.

---

## 5. Phased tasks and acceptance tests

Tests run through a new world trait `TransitAutoTest` (the existing `CityAutoTest` is frozen) plus `mods/city/tools/autotest.sh`, which already logs stats and saves screenshots. Every phase must also end with `./utility.sh city --check-yaml` showing `Errors: 0` and a clean `dotnet build -c Debug`.

**Determinism test (all phases).** Run the same order script twice with the same seed and compare `TransitLayer.StateHash` every 250 ticks; they must match. Also play a replay and confirm no OpenRA sync error.

| Phase | Tasks | Acceptance tests |
|---|---|---|
| **P1 Bus lines (MVP)** | (a) `TransitLayer`, `TransitRouter`, `TransitPlanner`, `TransitTools` + `TransitOrders`. (b) `busdepot` actor + `TransitDepot`, bus stop records and overlay. (c) `TransitRun` activity, `bus` driving real lines, boarding and alighting, fares to ledger. (d) Aggregate demand sampler. (e) Set `BusChance: 0`. (f) `TransitLineOrderGenerator` (stop tool, line tool), line panel, ticket/vehicle sliders | 1. Straight road, depot, 2 stops 20 cells apart, create line: a bus spawns within 100 ticks and visits both stops. 2. After 1 game month `ThisMonth.Passengers > 0` and `FareCents == passengers x ticket`. 3. Removing a middle road cell sets `Line.Broken` within 20 ticks and recalls buses; rebuilding restores the line. 4. Lowering ticket to 0 raises boardings by 30-50% on a controlled scenario. 5. Max PT actors never exceeds `MaxTransitActors`. 6. Car trips drop when 3 lines cover a residential block (compare `TrafficManager.ActiveVehicles` with and without lines). 7. Money: line revenue/cost ratio in 0.4-1.5 at default settings |
| **P2 Taxis** | `taxidepot`, `taxistand`, `TaxiDispatcher`, `taxi` sprite and actor, stand pickup, dispatch upgrade | 1. A request near a stand is served within 300 ticks with an idle taxi. 2. Idle taxis end at stands. 3. 25 simultaneous requests cost under 1 ms per tick (profile log). 4. Fares recorded as `fares-taxi` |
| **P3 Trams** | `Tram` track flag + overlay, `tramdepot`, `tram` actor, track build tool (drag, reuse `CityDragOrderGenerator`), line restriction to tracked cells | 1. Lines refuse non-tracked cells. 2. Tram capacity 90 respected. 3. Bulldozing a road under tracks removes the flag and breaks the line |
| **P4a Metro** | `metrostation`, tunnel polyline tool and overlay, data-only vehicles and renderer | 1. Metro journey beats bus for distances over 25 cells in planner unit test. 2. Tunnels cost `80 x cells`. 3. Trains render smoothly over 1,000 ticks without actors (actor count unchanged) |
| **P4b Rail and trains** | `RailLayer` + autotile art, `OutsideRail`, `trainstation`, `raildepot`, level crossings, intercity arrivals | 1. Rail cannot be placed on a road except a straight crossing. 2. Connected station receives scheduled intercity trains and raises `Population` or tourist count. 3. Crossing closes cars while a train passes |
| **P5 Cargo rail, harbor, airport** | `ICargoTerminal`, cargo trains, `harbor`, `airport`, cosmetic ships and planes, tourist arrivals by mode | 1. Rail cargo moves tonnage in and out without trucks on the road. 2. Harbor requires water. 3. Arrival rate scales with capacity |
| **P6 Bikes and polish** | `BikeLane` flag, bike mode in planner, cosmetic walkers and bikes, auto vehicle tuning, overlay legend | 1. Bike mode chosen on short trips along lanes. 2. Pedestrian dots never exceed 60 |

Performance budget [D]: transit tick under 0.3 ms with 30 lines, 200 stops and 80 vehicles; planner under 50 microseconds average with the destination cache warm.

---

## 6. UI and art needs

**Sidebar and tools (domain 10 builds the widgets; this package supplies logic).**
- New category tab "Transit" in `CITY_TABS` with sub-tools: Bus stop, Bus depot, Bus line, Taxi stand/depot, Tram track/stop/depot, Metro station/tunnel, Train station/rail. Hotkey `T` for the tab.
- **Line tool UX** (implement `IOrderGenerator` directly): click a stop to append; hovering a second stop previews the BFS path between them as coloured `MarkerTileRenderable` cells in the chosen line colour with a `Label` showing length in cells, estimated cycle time and suggested vehicle count; click the first stop again or press Enter to close a loop; Backspace removes the last stop; Right-click or Escape cancels. Issuing creates the line through `TransitCreateLine`.
- **Line panel** on selecting a stop or vehicle: name, colour swatch, ticket slider, vehicle slider with auto toggle, usage bar, passengers this month, revenue, cost, profit, average wait, waiting now.
- **Transit overlay** (local, not synced): all line paths in their colours, stop catchment radius of the hovered stop, waiting counts as bubbles. Implemented as its own world trait toggled by the UI, because `CityInfoView` is frozen.
- Transit overview list: lines with usage bars (like CS2's overview).

**Sprites** (all procedural via `tools/genworld.py`; sizes follow `CONTRACT.md` section 6):
- Bus stop overlay 32x32 (4 sides), taxi stand overlay; `busdepot` 96x96, `taxidepot` 64x96, `tramdepot` 96x96.
- Vehicles with 32 facings: `taxi` (yellow), second bus colour, `tram` (long, 48x32 or two linked sprites).
- Tram track overlay and metro tunnel dashed overlay: 16 frames each, 32x32.
- `metrostation` 32x64 (1x2), rail autotile 16 frames plus 2 crossing frames, `trainstation` 128x96, train engine and car (32 facings or straight/curved frames), `raildepot`.
- `harbor` 96x96, ship (32 facings), `airport` 160x128, plane with shadow.
- Pax dots (waiting), walker and bike dots (8x8, 4 frames).
- Chrome: `city-icons` entries `transit`, `bus`, `taxi`, `tram`, `metro`, `train`, `ferry`, `plane`; `city-buildicons` (48x48) for each placeable; status icon frame for "line broken".

---

## 7. Sources

- Paradox, Feature Highlight #3 Public & Cargo Transportation: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/public-cargo-transportation
- Paradox, Feature Highlight #2 Traffic AI (pathfinding cost): https://www.paradoxinteractive.com/games/cities-skylines-ii/features/traffic-ai
- Paradox forum, Development Diary #3: https://forum.paradoxplaza.com/forum/developer-diary/development-diary-3-public-cargo-transportation.1591948/
- Paradox, Dev Diary Building for Bicycles: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-building-for-bicycles
- CS2 wiki, Transportation (depots, stations, costs): https://cs2.paradoxwikis.com/Transportation
- CS2 wiki, Traffic: https://cs2.paradoxwikis.com/Traffic
- CS2 wiki, Citizens (leisure travel, age route preference): https://cs2.paradoxwikis.com/Citizens
- CS2 wiki, City Stations DLC: https://cs2.paradoxwikis.com/City_Stations
- TheGamer, bus routes and tram setup: https://www.thegamer.com/cities-skylines-2-bus-routes-basic-transportation-set-up/ and https://www.thegamer.com/cities-skylines-2-tram-lines-trams-set-up/
- Steam discussion on outside connections: https://steamcommunity.com/app/949230/discussions/0/3877095833482632634/
- GitHub, Transit Timetables (vanilla bunching evidence): https://github.com/AmicusDeus/TransitTimetables

Repository files read are listed in section 2.1 (all under `OpenRA.Mods.City/` and `mods/city/`).
