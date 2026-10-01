# 02 - Traffic and pathfinding (research and design)

Domain 2 of the OpenCity "approach CS2" research. Scope: trips, mode choice, path cost, pathfinding, vehicle AI, intersections, congestion, traffic-flow metric, parking, pedestrians, accidents, emergency vehicles, ramps, freight. Written against the repo state at `b4b7e7ee70`.

Confidence tags used below: **[official]** Paradox/Colossal Order dev diary or feature page, **[wiki]** cs2.paradoxwikis.com, **[community]** forum/Steam/Reddit reports, **[mod]** read from a modder's description, **[memory]** my recollection of CS2 internals that I could not re-verify online, **[design]** my own proposal.

TL;DR: replace the actor-per-car `TrafficManager` with a deterministic **mesoscopic queue simulation** on a directed road-cell graph. Every car is a cheap struct (not an OpenRA actor), moves link to link under density-based speed and junction rules, follows an integer A* route that uses experienced travel times, and is drawn by a custom renderer from the same state. This gives thousands of individually tracked trips, real congestion, spillback and a traffic-flow metric, at well under 1 ms/tick of CPU.

---

## 1. How Cities: Skylines 2 does it

### 1.1 The four-part path cost [official]
Every agent (cim, car, truck, service vehicle) picks the route that minimises a **pathfinding cost** with four aspects (Dev Diary #2, Traffic AI page):

| Aspect | What it covers |
|---|---|
| **Time** | Travel time, "usually the most important"; highways win over small roads when faster overall. |
| **Comfort** | Smooth routes, few turns at intersections, a suitable parking spot or transit stop. Bike paths score highest, then on-road bike lanes. |
| **Money** | Fuel, parking fees, transit fares. For freight, the value of the goods grows with delivery distance. |
| **Behaviour** | Willingness to take "dangerous" moves (illegal U-turns, lane crossing, breaking intersection rules). Citizens avoid them; emergency vehicles have a lenient profile. |

Age sets the weights: **teens** weight Money (cheapest route, park far away to avoid fees), **adults** Time (pay fees to save time), **seniors** Comfort (park close, accept cost). Total cost is the sum of per-move costs, so a huge cost acts as a ban **[mod: CustomVehiclePathfind]**. Decompiled mod code exposes per-vehicle costs `DrivingCost`, `TurningCost`, `UTurnCost`, `UnsafeUTurnCost`, `LaneCrossCost`, `ParkingCost`, `SpawnCost`, `ForbiddenCost`, each a four-float cost **[mod]**. I found **no published defaults**; numbers in section 3 are my own. Internals **[memory, unverified]**: a lane-based path graph, queued requests serviced by worker threads, and re-requests after blockages or accidents.

### 1.2 Trips, purposes and mode choice
**Purposes [wiki, official]**: work, school (elementary to university), shopping, leisure (parks, sports, restaurants; outdoor in good weather, indoor in rain), healthcare, going home, crime. Cargo and service trips come from buildings. Commuters and tourists enter via outside connections.

**Destination limits [wiki]**: patch 1.6.0f1 added **per-trip-type pathfinding limits** (work, school, shopping, leisure) because cims made trips across the whole map. When no route exists: move-outs teleport off-map, workers drop their job, leisure seekers idle at home and retry. Leisure picks by "leisure gain vs pathfinding cost" **[official]**. A good template for OpenCity failure handling.

**Mode choice [official, bicycle diary]**: first check the household owns a car that is available (not used by another member). If yes compare walk, drive, transit; if not, compare walk, bike, transit (bikes are always available, bike parking matters). Taxis and emergency vehicles are separate. There is no fixed walking-distance cutoff, only cost comparison **[community: walking dominates]**.

**Schedules**: trips come from the citizen state machine with a day/night rhythm. The "Realistic Trips" mod exists because vanilla shopping is nearly instant and rush hours are weak **[mod]**. Lesson: schedule belongs to domain 1, but traffic only looks right if departures cluster into peaks.

### 1.3 Roads, lanes and speeds
Classes **[wiki]**: small (1-3 lanes, 30-40 km/h), medium (4-5, 50), large (6-8, 50-60), highway (1-5, 80-120). Lights are available on roads with 3+ lanes; roundabouts come in four sizes; highway ramps have acceleration/deceleration lanes **[official]**. No authoritative per-lane capacity exists; a community table (gravel ~100, 2-lane ~200, 4-lane ~400, 6-lane highway ~1200) looks inconsistent **[community, low trust]**, so section 3.4 uses standard traffic engineering instead.

### 1.4 Vehicle AI and intersections
- **Decisions are separate from pathfinding [official]**: acceleration, braking, avoiding oncoming traffic in turns, lane switching and rerouting on events. If one lane is full of cars waiting at a light, new arrivals take the emptier lane; cars overtake when other lanes are underused and avoid lanes blocked by accidents.
- **Lane connections [mod: krzychu124/Traffic]**: each intersection has lane-to-lane connections generated automatically; modders added tools to edit them. Players complain about cars turning from the wrong lane near exits **[community]**.
- **Default control [community]**: small-road junctions have no signs; junctions with medium/large roads start with lights; removing lights leaves a yield for the lower class; stop signs are all-way. Roundabouts remove all signs; entrants yield to circulating cars but may cut in **[official]**.
- **Signals [mod discussion]**: vanilla lights extend green while traffic flows; no protected left, no right-on-red. Pedestrians always wait at signalised crosswalks but can block cars at unsignalised ones.
- **Pedestrians**: sidewalks, crosswalks, pedestrian streets (only service and delivery trucks allowed). Heavy crossings of major roads cause congestion **[community]**.

### 1.5 Congestion, flow metric, despawning
- **Traffic flow %**: the info view shows *flow* (smoothness, higher = no jams) and *volume* (vehicle counts) per city and per road **[official]**. The formula is **unpublished**; best reading is speed relative to free-flow, averaged **[community inference]**. Healthy cities sit at 60-70% (a 300k city reported 60%, ~1M reported 68-70%) **[community]**, so 100% is not a goal.
- **Despawning [community]**: vehicles stuck too long despawn, releasing passengers to walk; this prevents permanent gridlock. Cars also despawn near accidents police cannot reach. No timer published. Patch 1.6.2f1 fixed cars teleporting to outside connections after parking-related despawn **[wiki]**.
- **Induced demand [community]**: easier driving lowers path cost, so more people drive.

### 1.6 Parking [official, wiki, community]
Parking is part of pathfinding: citizens prefer the destination building's spots, then roadside, lots and halls; if none is reasonable they change destination or mode. Wide sidewalks, grass strips and bike lanes remove roadside spots. Roadside fee is a **district policy** (typical $10, up to $50) and each lot has its own fee; high fees favour the wealthy **[official]**. Cars are persistent household objects that can be "left behind" **[wiki, patch 1.6.2f1]**.

### 1.7 Accidents and emergency vehicles [official]
Accident chance rises with road condition, lighting, weather, disasters. A random vehicle on the segment "loses control"; its lanes stop until police/maintenance clear it, which can trigger rerouting; severe ones call an ambulance. Emergency vehicles use the lenient profile (illegal U-turns allowed) and others switch lanes to yield. Service dispatch picks the vehicle with the lowest overall path cost, counting its projected position after current jobs.

### 1.8 Freight and outside connections [official, community]
Trucks and vans deliver everything; warehouses and cargo terminals buffer; trucks are "the last link" and a main source of congestion. Outside connections carry imports/exports and **through traffic "from one outside connection to the next"**, which reroutes when a shorter path appears.


---

## 2. Current OpenCity state and gaps

### 2.1 What exists
- **`TrafficManager` (World trait, 629 lines)** spawns one OpenRA actor per trip (`car-a..d`, `truck`, `bus`) at a road cell, queues `Mobile.MoveTo` with its **own BFS** over 4-connected road cells (hop count, ignores road type) and a trailing `RemoveSelf`.
  - Cap: `min(MaxVehicles=200, Population/6)`, 20 without a city. Spawn timer 3..30 ticks.
  - Trip mix is a dice roll: 55% home to job, 20% highway to job, 15% industry to highway, 10% home to highway. Endpoints are road cells next to buildings, refreshed every 50 ticks. **There is no link to individual citizens**, only to `CityBuilding.Residents` being non-zero.
  - Load map: every 5 ticks each vehicle adds 10 to its cell, decayed x3/4 every 25 ticks, saturating at 150. `GetTrafficLoad(cell)` feeds `InfoViewLayer` (traffic view) and nothing else.
  - Vehicles are culled after 150 ticks without moving or 3000 ticks of life.
- **Locomotor `road`** has `SharesCell: true` (up to 5 actors per cell via sub-cells) and speed 100 only on terrain `Road`. `Mobile` speed is 76-104 world units/tick (about 10 ticks per cell).
- **`WithLaneOffset`** shifts the sprite 6 px right of travel (visual only).
- **`RoadLayer`** stores one flag per cell (`road`, `locked/highway`), `NetworkVersion`, connectivity to outside, `RoadChanged` event, and autotiles. It knows no road types, one-way, lanes, speed or junction control (domain 3 will add them).
- **`CityManager`** simulates aggregates: `Residents`, `Workers`, `Jobs`, tax, happiness. **A calendar day is 25 ticks** (`TicksPerDay`), so a month is 750 ticks (30 s at speed 1).

### 2.2 Gaps against the goal
- **No capacity, speed-density or queues.** Cars share cells through sub-cells and pass through each other, so congestion cannot exist; a "jam" is only `Move` blocked-waits plus the 150-tick stuck cull.
- **Routing is hop-count BFS**: no road classes, one-way, turn costs or ramps; no junction control at all (no stops, lights, yields, roundabouts).
- **Trips are not people.** No purposes, schedules, mode choice, walking or parking, so "simulate each human" is impossible and traffic ignores where jobs, shops and schools are.
- **No feedback**: travel time never reaches happiness, land value, demand, service response or freight. Traffic is decoration.
- **Actors are expensive**: `Mobile` + `Move` child activities per cell, `ActorMap` influence, render sort, per-car `AddFrameEndTask` disposal. Hundreds are fine; thousands are not (I did not profile this repo, so 1-2k is a guess to confirm in Phase 0).
- **Time scale**: a day is 25 ticks but a 20-cell trip takes ~200 ticks (8 days), so there is no daily rhythm and no rush hour.
- **Metric and failure handling**: flow is a decayed car count, not speed-based; failed or stuck trips vanish silently; no `[VerifySync]` state.

### 2.3 Engine facts that shape the design
- OpenRA `Mobile`/`Move` (HierarchicalPathFinder, `Locomotor` blocking, nudge, `WaitAverage` retry) is built for RTS units on open ground: costly per actor and its block/wait/nudge/repath model fights car-following. Wrong tool for 2k+ vehicles.
- `world.WorldActor.Render(...)` is called every frame in `WorldRenderer.GenerateRenderables`, and the results are depth-sorted with all other renderables. **A world trait implementing `IRender` can emit `SpriteRenderable`s for non-actor vehicles** that sort correctly against buildings and trees **[verify the exact trait signature when implementing]**. `RoadLayer` already shows the pattern for terrain-level drawing (`IRenderOverlay` + `TerrainSpriteLayer`).
- Sim code must be synced: integer math, `world.SharedRandom` in a fixed order or a hash of ids, no `HashSet` order dependence. The autotest replay-playback mode (commit `77810fbcf7`) supports determinism tests.


---

## 3. Proposed design

### 3.1 Architecture: mesoscopic queue simulation, no actors
Three layers, all driven by one synced world trait `TrafficSim` (it replaces `TrafficManager` and keeps its public API `ActiveVehicles`, `GetTrafficLoad(cell)`):

1. **Trip layer (virtual).** Trips are small records: `{TripId, CitizenId, Purpose, Mode, OriginCell, DestCell, DepartTick, State}`. Walking, transit and "car hidden in a garage" legs are pure timers here, no road load.
2. **Vehicle layer (meso).** Every car/truck/bus/service vehicle on the road is a **struct in a struct-of-arrays pool**, a member of exactly one FIFO queue (the approach link it is currently in). Vehicles advance link to link by rules (3.4-3.5). This is the MATSim "queue model": individually tracked, no lateral physics, deterministic, O(moves) per tick.
3. **Presentation layer (local, unsynced).** A renderer turns vehicle state into sprites, interpolating position inside the current link (3.11). No actors, no `Mobile`.

Why not actors? Section 2.3. Why not full micro car-following? 10-50x the CPU and harder to keep deterministic; the meso model still yields queues, spillback, gridlock and signal cycles, which is what the player sees.

**What replaces what**: `TrafficManager.TrySpawnTrip` becomes `AggregateTripSource` (Phase 1, mimics today's mix but through the new engine) and is later superseded by citizen trips from domain 1 (Phase 3). `WithLaneOffset` and `car-*` actors become a sprite lookup in the renderer (the `vehicles.yaml` sequences stay).

### 3.2 Time model
Today a day is 25 ticks, far too short for a trip. I propose a **traffic clock independent of the calendar** [design]:
- `TicksPerTrafficHour = 100`, `TrafficHour = (tick / 100) % 24`, so one traffic day is 2400 ticks (96 s at speed 1, ~10 s at speed 3). The calendar and economy are untouched.
- Free-flow time per cell: **local 9, collector 6, arterial 5, highway 3 ticks** (about 3.5 / 5.3 / 6.4 / 10.7 px per tick at 32 px cells; the current cars do ~3 px/tick). A 15-cell commute on collectors takes 90 ticks, ~0.9 traffic hours. Treat the "cell" as an abstract ~1 min of driving, not a physical length.
- Walking: 20 ticks per cell. Bus and tram timings come from domain 4 in the same ticks.
- All commute-quality thresholds are expressed in ticks (good <= 60, bad >= 150) so they are independent of the calibration.
- Open question for the lead: either keep this decoupled clock or re-base the whole calendar on a 2400-tick day. Decoupling is cheaper and safe.

### 3.3 Road graph and lane model on the grid
Data comes from `RoadLayer` plus the road-type data of domain 3 (interface in section 4). The sim keeps its own flat arrays, rebuilt incrementally when `RoadLayer.NetworkVersion` changes (a version-stamped snapshot, rebuilt at most every 10 ticks as `TrafficManager` already does).

- **Node = road cell. Link = (cell, heading)** where heading is the direction the vehicle was moving when it **entered** the cell (N/E/S/W, `CityUtils.Neighbours4` order). Link index = `cellIndex * 4 + heading`. So each cell has four *approach* queues. A vehicle in `(c, h)` leaves via `route[step]` into `(c + dir, dir)`; a path is a sequence of 2-bit headings and turns are implicit.
- Per-cell attributes (from `RoadSpec`): `Class` (Local, Collector, Arterial, Highway), `LanesPerDir` (1..4), `OneWay` heading or none, `SpeedKmh`, `ParkingSlots`, `Sidewalk`, `NoTrucks`, `BusLane`, `Ramp`, `Closed`. Link attributes derive from the cell the vehicle is traversing.
- **Lane model**: lanes are a capacity multiplier of the approach FIFO, not separate entities. A queue serves up to `LanesPerDir` vehicles at its front per release, which also models overtaking and lane choice: a vehicle at position < lanes whose own movement is allowed can leave before a blocked one ahead (a left-turner waiting for a gap blocks a 1-lane approach but not a 3-lane one). Per-lane turn restrictions and visible lane changing come in Phase 5.
- **Illegal moves are absent from the graph**: no entry against `OneWay`, no U-turns except at dead ends (cost-banned but allowed so cars can leave cul-de-sacs), no highway entry/exit except through `Ramp` cells. Closed or bulldozed cells simply drop out, and any vehicle in them is rerouted/dropped (3.9).
- **Outside connections** (`OutsideConnection` actors) become **external gates**: an entry gate injects vehicles into the first highway cell, an exit gate absorbs them. This covers commuters, tourists, freight imports/exports and through traffic.

### 3.4 Link model: speed-density, storage, discharge
For each link `L` with `n` vehicles inside and storage `S = LanesPerDir * SlotsPerLane` (default 3 slots per lane per cell; trucks and buses take 2 slots):

- **Entry**: a vehicle may enter `L` only if `n < S` (**spillback**: a full link blocks the upstream node, which blocks its own queue). On entry it gets `readyTick = now + T(n)` where
  `T(n) = ffTicks * 16 / (16 - min(14, 16*n/S))` (Greenshields linear speed-density, integer lookup table of 15 entries; T = 2x free-flow at half full, capped at 8x).
- **Exit**: the vehicle at the front of the FIFO may leave when `now >= readyTick`, the next link has room, the node control allows the movement (3.5) and the per-link release timer permits (`nextRelease = now + DischargeInterval`, default 2 ticks, one vehicle per lane per release).
- Steady throughput is about `lanes * S / (4 * ffTicks)` vehicles per tick (Greenshields maximum at half-full): ~0.125 veh/tick for a 1-lane collector, ~0.75 for a 3-lane highway. These are **sim vehicles**; two calibration knobs scale them to the city: `PersonsPerVehicleTrip` (demand scale, 1 for small towns, 4-8 for 20k+ pops so in-flight vehicles stay under ~6k) and `SlotsPerLanePerCell`. Targets are in section 5.
- **Experienced travel time**: when a vehicle leaves `L`, `L.AvgTicks = (7*AvgTicks + (now - enterTick)) / 8`. This EMA is the congestion input to route choice (3.7) and to the flow metric (3.8). No separate BPR curve is needed; if a displayed volume/capacity ratio is wanted it is `n / S` smoothed.
- **Processing order**: each tick loop over links in index order (only `n > 0` does work), serving at most `lanes` front vehicles per link. Fixed order keeps it deterministic; 40k links cost ~0.1 ms.
- **Gridlock breaker**: if a link's front vehicle has been blocked for > `GridlockTicks` (200) and the blocker is itself blocked (cycle), allow it to overfill the next link by one slot (deterministic "box release"). Without this, circular spillback freezes forever.

### 3.5 Intersection control
Every junction cell gets a `Control` byte. **Defaults** (domain 3 may let the player override with a synced order `SetJunctionControl(cell, mode)`):

| Situation | Default | Rule at the node |
|---|---|---|
| Different road classes meet | **Priority** | Lower-class approaches yield; they may go if no vehicle of a higher-class conflicting approach left in the last `GapTicks` (3) ticks. Right turns are free. |
| Same class, both Local/Collector, 4 legs | **Stop** | Min wait 2 ticks at the head, then one vehicle per 2 ticks through the node, round-robin over approaches from a rotating pointer. |
| Same class, T junction | **Priority** (stem yields) | as above |
| Junction with any Arterial/Highway-ramp approach or any approach with >= 3 lanes | **Signal** | see below |
| Ring of one-way cells around an island (player-built roundabout) | **Priority**, ring is major | Entrants yield to ring occupancy `n > 0` on the upstream ring link; ring cells are one-way, so no special code beyond priority + one-way. |

**Signals**: fixed 24-tick cycle by default: phase A (N-S) green 10, all-red 2, phase B (E-W) green 10, all-red 2, with per-node random-but-deterministic offset `hash(cell) % 24` to avoid synchronised waves. During green, an approach discharges one vehicle per lane per 2 ticks. Left turns yield to the opposing through lanes (allowed only when the opposing approach released nobody in the last 3 ticks), like vanilla without protected left. Phase 2 adds CS2-like green extension (up to 6 ticks while the green approach released a vehicle in the last 2 ticks); protected-left and pedestrian-exclusive phases are Phase 5.

**Pedestrian effect**: on a cell with a crosswalk, if walkers crossed in the last 5 ticks, turning vehicles' discharge interval doubles (vanilla lets pedestrians block cars [community]).

**Emergency vehicles** ignore signal and yield rules (but not storage), may jump the queue (they are served first among the front `lanes` vehicles) and travel 1.5x faster.


### 3.6 Trips, mode choice, parking
**Trip request** (from domain 1, or `AggregateTripSource` in the MVP): `{CitizenId, HouseholdId, Purpose, AgeGroup, Origin (building actor id), Destination (building id or external gate), AllowedModes mask, DepartTick}`. `Purpose` = Work, School, Shopping, Leisure, Health, GoingHome, Commute (external), Delivery (freight), Service (garbage, mail, maintenance), Emergency (fire, police, ambulance), Through. Origin/destination are the building's access road cell (as `TryFindAccessRoad` does today).

**Mode choice** runs when the trip is accepted, in `ActorID`/`CitizenId` order [design, modelled on CS2]:
1. Candidates: **Walk** if distance <= `MaxWalkCells` (10); **Car** if the household has an available car (domain 1 `HouseholdCarAvailable`) and parking at the destination is plausible; **Transit** if domain 4 returns a quote `TransitQuote(originCell, destCell)`; **Bike/Taxi** in later phases.
2. Cost `G = Wt*Time + Wm*Money + Wc*Comfort` in tick-equivalents. Illustrative weights (CS2 only says which is "foremost"): adult (Wt 2, Wm 1, Wc 1), teen (1, 3, 1), senior (1, 1, 3). $1 = 4 tick-equivalents. Comfort = turns + transfers + unsheltered walking + parking search risk.
3. Add deterministic noise `hash(citizenId, day) % 16` ticks so near-ties split.
4. Respect per-purpose range limits (Shopping 30 cells, School 25, Leisure 40, Work 120), inspired by patch 1.6.0f1. **No route or over-range => `OnTripFailed(reason)`** and the citizen sim applies consequences (lose job, stay home), never a silent vanish.

**Parking** [design, CS2-inspired]:
- Supply: on-street `ParkingSlots` per cell from road type (e.g. local 2, collector 2, arterial 0, highway 0; removed by sidewalks-wide or bus lane), plus parking lots/garages (services: capacity, fee) and residential/commercial private spots (per building from level).
- A car trip needs a spot at the destination: first the building's own spots, then the nearest free on-street cell within `ParkSearchRadius` (4 cells, BFS over road cells), else lots within 12 cells. While searching, the car keeps **driving as an extra route leg** (cruising traffic is a real congestion source in CS2), at most `ParkSearchTicks` (120). Fail => walk the rest of the way from wherever it parked, or give up (`TripFailed: NoParking`).
- Fees: `StreetParkingFee` per district/city (policy) and per-lot fee add to Money cost, charge `CityManager.AddFunds` on arrival. Teens (high Wm) park far and walk, seniors (high Wc) take the closest spot.
- MVP keeps counters per cell (`parked[cell] <= slots`); Phase 3 makes household cars persistent records `{HouseholdId, cell}`.

### 3.7 Route choice and caching
- **Graph and algorithm**: A* over link states `(cell, heading)` (4 states per road cell, so turns, one-way and U-turn bans are exact). Binary heap with deterministic tie-break `(f, g, linkIndex)`, integer costs in 1/16-tick units, neighbours expanded in N,E,S,W order. Heuristic: Manhattan distance x min free-flow ticks x 16 (admissible). Not using OpenRA's `PathFinder`: it assumes actors/terrain, not turn costs or dynamic link costs.
- **Edge cost** `= Wt * T_link*16 + turn + control + money + comfort`:
  - `T_link = max(ffTicks, snapshotAvgTicks)` (EMA from 3.4), plus `n/S`-based queue term if the link is > 75% full.
  - turn: straight 0, right 8, left 24 (units 1/16 tick, ~0.5/1.5 ticks), U-turn 480 (legal at dead end), illegal U-turn / wrong-way = forbidden (emergency vehicles 160).
  - control: stop +32, signal +96 (half the average red), yield +16, roundabout +24.
  - money: highway toll/fuel per cell `0..4` (policy), parking fee at the destination.
  - comfort: local-road bonus for seniors, truck on `Local` + residential neighbours +64 (so freight prefers arterials).
  - Behaviour: only emergency profile relaxes the bans.
- **Snapshot costs**: congestion costs are *snapshotted* into a read-only array every `CostRefreshTicks` (50). Every route in a window sees the same costs, which removes order dependence and makes caches valid. Reroute-on-event uses the newest snapshot.
- **Preventing herd flips**: 20% of departures (by `hash(tripId)`) use the previous snapshot and a per-trip +/-5% hash jitter spreads choices; otherwise all cars swap to the same "free" street each refresh.
- **Budget and queue**: new trips go to a pending heap ordered by `(DepartTick, TripId)`. At most `PlanBudget = 32` A* runs per tick; a trip that cannot be planned in time waits at its origin (departs late, counts as delay). A* on 16k states with the heuristic expands ~200-1,000 states (my estimate: 20-100 us).
- **Cache (Phase 2+)**: reverse Dijkstra "path tree" (a `byte[linkCount]` of next headings) per popular destination cell (workplace clusters, schools, gates), reused within `RouteTTL` (200 ticks) and the same `NetworkVersion`. LRU, cap 64 trees.
- **Reroute**: a vehicle blocked at the head of a link for > 40 ticks re-plans from its current link (budgeted), CS2-style. A route made illegal by a road change replans on the vehicle's next link, or ends in a stranded despawn (3.9).
- **Routes are packed** at 2 bits per step in a pooled `uint[]` (~10 bytes per 40-step trip).

### 3.8 Congestion feedback, flow metric, info views
- **Per link** every 25 ticks: `volume` = departures in the last 100 ticks, `flow%` = `100 * ffTicks / max(ffTicks, AvgTicks)` (speed relative to free-flow, the best public reading of CS2's metric, **explicitly my definition**), `fill` = `n/S`.
- **City traffic flow %** = `100 * sum(ffTicks of completed link traversals) / sum(actual ticks)` over a 100-tick rolling window, weighted by traversals. Expect 70-90% in a healthy town, below 50% means gridlock. Also expose `AverageCommuteTicks` per purpose and a 24-hour flow history for a chart.
- **Keep the old API**: `GetTrafficLoad(cell)` returns `max(fill, 100 - flow%)` over the four approach links, so the existing `InfoViewLayer` works unchanged. Add `GetTrafficFlow(cell)` / `GetTrafficVolume(cell)` for a two-mode traffic view (volume vs flow, like CS2).
- **Feedback into the city** (read by other domains): per-building `CommuteTicksEma` (domain 1: commute happiness penalty above ~90 ticks, job-quit chance above ~150), road-side `Noise`/`AirPollution` from volume (land value, happiness, health), service-vehicle response times (domain "services": fire/ambulance/police coverage scales with actual arrival times), freight delay (domain 6: shop stock and factory input delays), and aggregate accessibility (demand and land value near arterials).
- Induced demand emerges naturally: faster routes lower `G`, so more citizens choose car over walk/transit.

### 3.9 Vehicle types, accidents, emergency, despawn, freight
- **Vehicle record**: `{Type, Slots, Flags: Emergency|Truck|Bus, OwnerId}`; type maps to a sprite.
- **Freight (domain 6)**: the industry sim emits `FreightRequest {FromBuilding, ToBuilding|Gate, Units}`; traffic converts it to trucks with `TruckCapacityUnits` (20 each), one trip per truck, plus empty return legs. Trucks: 2 slots, ff x1.25, banned on `NoTrucks` roads, avoid residential `Local` streets. External imports/exports start/end at gates. Truck lateness is reported back as `FreightDelayTicks`.
- **Service and emergency**: a service building asks `RequestServiceTrip(building, targetBuilding, kind)`; the vehicle gets the Emergency flag for fire/ambulance/police. Dispatch picks the free vehicle with the lowest estimated cost (route tree from the target), like CS2.
- **Accidents (Phase 4)**: per link traversal chance `1 / AccidentOdds` (default 1 in 20,000, x2 on roads with bad condition or without lighting, x3 in bad weather if domain exists). An accident blocks one lane (reduces `lanes` by 1, min 0 = blocked) for 300 ticks or until a police/maintenance vehicle arrives. The vehicles involved stay as obstacles. Neighbours reroute via the normal blocked-wait rule.
- **Despawn/failure rules** (all synced, all notify the trip owner):
  - Head vehicle blocked > `StuckTicks` (600 ticks) => removed (24 s of real time at speed 1); its passengers become walkers for the rest of the trip, the car is returned to a parking record at the nearest road cell (Phase 3) or just dropped (MVP); `OnTripFailed(Stuck)`.
  - Vehicle on a cell that stops being a road => the same.
  - Lifetime above `MaxTripTicks` (1,200) => removed.
  - Emergency vehicles never despawn for being blocked while a path exists; they trigger a re-plan.
- **Highway ramps**: A* treats `Ramp` as the only class-changing node. The merge from ramp to highway uses Priority (highway major). The "acceleration lane" is a ramp cell with `ffTicks` of the highway class, so merged cars do not slow the highway. Upstream spillback onto the highway happens naturally when the off-ramp link is full: the standard "single exit lane too short" failure is therefore reproduced and fixable by the player (more ramp cells = more storage).

### 3.10 Pedestrians and sidewalks
Walking trips are virtual timers `walkTicks = pathLength * 20`, planned with the same A* but on `Sidewalk` cells (all road cells have sidewalks by default; domain 3 may remove them). Crossing roads costs `+8` per crossing, `+24` at a signalised crossing, and, via 3.5, blocks vehicles when dense. Walking has no storage limit. Visible pedestrians are a sampled drawing (3.11).

### 3.11 Rendering
- A `TrafficRenderer` (part of the same world trait or a sibling) implements `IRender`/`ITickRender` for the world actor. It iterates the **links in the viewport** (cell rect from `wr.Viewport`), computes each vehicle's position and emits a `SpriteRenderable` using the existing 32-facing sprites. Cost is proportional to visible vehicles (hundreds), not total.
- Position inside a cell: `s = clamp((now - enterTick) / T(n_at_entry), 0, 1)` along the path from the entry-side midpoint to the exit-side midpoint, offset to the right-hand lane (lane index = position in the FIFO modulo lanes, 5 px per lane). Queued vehicles are spaced back from the head by `slotLength = 1/3 cell`, so jams look like queues and not like overlaps. Turns follow a quadratic Bezier through the cell centre for smooth corners (the contract demands smooth turning). Facing = tangent angle quantised to 32.
- Optional: sampled pedestrians on road edges (every Nth walking trip), parked cars drawn on `ParkingSlots`, and a "follow vehicle" camera (the sim keeps the id).

### 3.12 Performance budget and determinism
Budgets (6k in-flight vehicles, 40k links): link processing <= 0.3 ms/tick (flat arrays, no allocation); A* <= 3 ms worst case (32 runs), < 0.5 ms average; network rebuild <= 5 ms only on `NetworkVersion` change, throttled to once per 10 ticks; EMA/flow stats <= 0.2 ms every 25 ticks; rendering <= 1 ms/frame with 800 visible vehicles; memory < 10 MB (SoA arrays, packed routes).

Target: whole traffic update < 5 ms/tick average on a 50k-population city at speed 1 (40 ms tick budget), < 2 ms at 5k population. Phase 0 must measure these with `Stopwatch` counters in the autotest.

Determinism rules:
- No floats in synced state (the renderer is exempt). All costs and times are integer.
- Per-trip randomness is a pure hash of `(tripId, citizenId, tick, salt)`, never `world.SharedRandom` inside data-dependent loops. `SharedRandom` is only used where the number of calls is fixed by deterministic order (e.g. a once-per-day pick in citizen-id order).
- All iteration over arrays/lists by index, over `ActorID`/`CitizenId` ascending. No `Dictionary`/`HashSet` enumeration in synced code (use sorted lists as `TrafficManager.RefreshNetwork` does).
- Mark core state `[VerifySync]`: `TotalVehicles`, `TotalTrips`, and a rolling hash of `(vehicleId, linkIndex, routeStep)` computed every 25 ticks.
- Every world mutation arrives through an order (road/zone/junction orders), and trip creation comes from other synced traits. Save games are order replays, so no state needs serialising.


---

## 4. Interfaces

All are C# in `OpenRA.Mods.City.Traits`, synced, and tolerate a missing provider (`TraitOrDefault`).

**Traffic provides (`TrafficSim`, World trait)**
- `TripId RequestTrip(in TripRequest r)` and `bool CancelTrip(TripId)`. `TripRequest` is in 3.6. Result callbacks go to `ITripListener` (implemented by the citizen sim): `OnTripStarted`, `OnTripArrived(tripId, citizenId, departTick, arriveTick, mode)`, `OnTripFailed(tripId, citizenId, reason)` with reason NoRoute, NoParking, Stuck, OverRange, RoadClosed.
- `RequestFreight(...)`, `RequestServiceTrip(...)` (3.9).
- Queries: `ActiveVehicles`, `GetTrafficLoad(cell)`, `GetTrafficFlow(cell)`, `GetTrafficVolume(cell)`, `CityTrafficFlow`, `GetCommuteTicks(buildingId)`, `GetNoise(cell)`, `GetAirPollution(cell)`, `EstimateTravelTicks(originCell, destCell, mode)` (for citizens deciding where to live, shop or work, and for services/transit).
- `SetJunctionControl(cell, mode)` and `GetJunctionControl(cell)` for the road tool UI (via a synced order).

**Needed from citizens (domain 1)**
- Stable `CitizenId`/`HouseholdId` (uint, never reused), `AgeGroup`, income class, `HouseholdCarAvailable(householdId)` and `ReserveCar/ReleaseCar`, the daily schedule (purpose + preferred depart window) and the ability to react to `OnTripFailed` (job loss, move out, retry).
- Building access cell is derived by traffic from `CityBuilding.Cells`; buildings must expose `HasRoadAccess` as today.
- Per-building `ParkingSpots` (private) from level/zone.

**Needed from roads (domain 3)**
- `RoadSpec GetSpec(CPos)`: `Class`, `LanesPerDir`, `OneWay` heading, `SpeedKmh`, `ParkingSlots`, `Sidewalk`, `NoTrucks`, `BusLane`, `Ramp`, `Closed`, `LightsAllowed` (3+ lanes), plus a monotonically increasing `NetworkVersion` and the `RoadChanged` event (exists). Upgrading a road changes `Spec` but not connectivity, so a separate `SpecVersion` avoids full rebuilds.
- Rule: highway cells connect to non-highway cells only through `Ramp` cells. Tool for roundabouts (ring of one-way cells) and junction control overrides.
- `RoadLayer.Highways`/gates (`OutsideConnection` direction, entry/exit cell) as a public list.

**Needed from economy/industry (domain 6)**
- `FreightRequest` stream (building to building/gate, units, resource id) and consumer `OnFreightDelivered(requestId, delayTicks)`.
- Fees: traffic calls `CityManager.AddFunds(amount, "parking")`; fuel tax/tolls later. Road wear: traffic exposes cumulative `VehicleKm` per cell for maintenance upkeep.

**Needed from transport (domain 4)**
- `TransitQuote(originCell, destCell, ageGroup)` returning `{ticks, fare, comfort, transfers, available}`. Buses are vehicles in the traffic sim (they take road storage, use `BusLane`), so transit supplies `Line {stops[], headway}` and traffic simulates the bus; passengers board at stops as timer legs.
- `ParkAndRide` lots as normal parking facilities with a transit stop.

**Needed from services**: building service-vehicle depots and `OnServiceVehicleArrived`; coverage uses `GetCommuteTicks`-style response times.

---

## 5. Phased tasks and acceptance tests

Each phase is shippable on its own and keeps `TrafficManager` behaviour visible (never a regression in the autotest screenshots).

### Phase 0 - Foundations (1 builder, short)
- Add `RoadGraph` snapshot (flat arrays, `SpecVersion`), `RoadSpec` stub (everything `Local`, 1 lane) until domain 3 lands. Add `Stopwatch` counters and a `traffic` section to `CityAutoTest` log lines (**CityAutoTest is frozen, so add a new scratch test trait** or ask the lead).
- Profile the **current** actor-based traffic (200 and 1,000 vehicles) to confirm the "hundreds OK, thousands not" assumption.
- **Accept**: unit-style test builds a 20x20 grid, `RoadGraph` has `4*cells` links, A* returns the Manhattan-optimal path with the expected turn count, repeated runs give identical hashes.

### Phase 1 - MVP: meso engine with aggregate trips (replace actors)
- `TrafficSim` with link FIFOs, density travel time, storage/spillback, A* with static costs (road class ffTicks), pending-trip heap, `AggregateTripSource` (today's 55/20/15/10 mix using `CityBuilding` data), external gates, sprite renderer, old API preserved, `[VerifySync]` hash.
- **Accept**:
  1. Autotest `boot-test` for 2,000 ticks: no exceptions, `vehicles` (as before) between 10 and the cap, per-tick traffic cost < 2 ms.
  2. Replay run twice: identical `TrafficSim` hash at every 25-tick check.
  3. Funnel test: 300 vehicles routed through one 1-lane local cell show queue length >= 6, upstream links fill (spillback) and throughput stays <= the Greenshields bound +10%.
  4. Screenshots show cars in lanes, queues behind each other, smooth corners, no overlaps. Vehicles are still not selectable.
  5. Bulldoze a road cell under traffic: vehicles replan or are dropped within 50 ticks, no crash.

### Phase 2 - Junctions, congestion-aware routing, metrics
- Junction control (Priority, Stop, Signal incl. green extension), experienced-time EMA + snapshots, reroute-on-block, gridlock breaker, flow/volume maps and the two-mode traffic info view, route-tree cache.
- **Accept**: (a) a signalised 4-way carries >= 1.5x the vehicles of an all-way stop at the same load; (b) two parallel routes with a 2x capacity difference end with split ~ 60/40 within 3 refreshes, without oscillating more than 15% between refreshes; (c) city flow % is 85-100 when idle, drops below 60 when the only bridge is saturated; (d) rush-hour demand (traffic hours 7-9) lifts peak volume >= 2x over the off-peak; (e) 20 consecutive 2,000-tick runs are bit-identical.

### Phase 3 - Citizens, modes, parking (needs domain 1)
- Replace the aggregate source with `RequestTrip` from the citizen sim, mode choice (walk/car/transit), age weights, range limits, parking supply and search, persistent household cars, feedback to happiness/land value (3.8).
- **Accept**: 1,000 citizens with jobs 12+ cells away produce a car share > 70% when parking is free and < 40% after raising the street fee and removing street parking; a job 3 cells away is reached on foot by >= 80% of commuters; `OnTripFailed(NoRoute)` fires when the only road is cut, the worker loses the job after the configured retry; commute ticks per building appear in the building panel; a congested commute lowers the building happiness by the documented amount.

### Phase 4 - Freight, services, emergency, accidents
- Trucks from `FreightRequest`, truck-only restrictions, service/emergency vehicles with priority and dispatch, accidents with lane blocking and clear-up, through traffic between gates, stranded-vehicle rules.
- **Accept**: an emergency vehicle crosses a jammed 20-cell route in < 60% of the time of a normal car; a stalled accident lane reduces link throughput by the expected share and clears within 300 ticks of police arrival; freight delay appears in the industry stats; through traffic scales with outside-connection count.

### Phase 5 - Depth and polish
- Per-lane turn restrictions and visible lane changing, protected-left and pedestrian phases, roundabout tool support, bikes and taxis, sampled pedestrians, parked cars, follow-camera, platoon scaling, and a "traffic routes" view (select a road, highlight its sources and destinations).
- **Accept**: 50k-pop soak (>= 20 traffic days) stays < 5 ms/tick average traffic update, no desyncs in replay, no growth in memory after day 3.

### Calibration step (every phase)
Tune `SlotsPerLanePerCell`, `PersonsPerVehicleTrip`, `DischargeInterval`, ff ticks and signal timings with the autotest: a sensible grid town of 1,500 residents has 75-90% peak flow, a single-road town drops below 40%, and nothing stays gridlocked after demand relief (flow > 20% within 500 ticks).

---

## 6. UI and art needs

- **Traffic info view (domain F)**: Flow (green to red) and Volume (blue to red) modes, legend, city flow % and a 24-hour flow chart; selected-road panel with class, lanes, speed, volume, flow %, parking used/slots.
- **Tools**: junction control tool (click cycles None/Yield/Stop/Signal), roundabout placement, one-way arrows, parking-fee policy slider, lane count in the road tool preview.
- **Overlays and icons**: signal-state dots (3 frames) and stop/yield signs at junction corners (16x16, shown zoomed in or with the junction tool), crosswalk stripes in junction road frames, accident marker, "no parking" and "long commute" building status icons.
- **Vehicle sprites** (`vehicles.yaml`, 32 facings, 32x32): keep `car-a..d`, `truck`, `bus`; add `van`, `semi`, `taxi`, `police`, `ambulance`, `firetruck`, `garbage`, `maintenance`, with 2-4 frame emergency lights. Parked cars reuse car sprites at 0/90/180/270 (plus 45 for angled bays).
- **Pedestrians (optional)**: 8-10 px walkers, 4 directions x 4 frames, a few colours.
- **Road art (domain 3 and A2)**: lane markings for 1-3 lanes per direction inside one 32 px cell, stop lines, crosswalks, ramp/merge markings, parking bays.
- **Audio (A3)**: ambient traffic loop scaled by visible vehicle count, sirens.

---

## 7. Sources

Official and wiki
- Feature Highlight #2, Traffic AI - https://www.paradoxinteractive.com/games/cities-skylines-ii/features/traffic-ai
- Dev Diary #2, Traffic AI - https://colossalorder.fi/?p=1597 (mirror: https://forum.paradoxplaza.com/forum/developer-diary/development-diary-2-traffic-ai.1591141/)
- Feature Highlight #1, Road Tools - https://www.paradoxinteractive.com/games/cities-skylines-ii/features/road-tools
- Feature Highlight #11, Citizen Simulation and Lifepath - https://www.paradoxinteractive.com/games/cities-skylines-ii/features/citizen-simulation-lifepath
- Dev Diary, Building for Bicycles (mode choice) - https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-building-for-bicycles
- Transportation overview (public, cargo) - https://www.paradoxinteractive.com/games/cities-skylines-ii/features/public-cargo-transportation (forum Dev Diary #3 would not load)
- CS2 Wiki: https://cs2.paradoxwikis.com/Traffic, /Roads, /Citizens, /Info_views, /Services, /Patch_1.6.X, /Patch_1.2.X

Community and mods
- Steam guide, The Ultimate Traffic Guide - https://steamcommunity.com/sharedfiles/filedetails/?id=3281715600
- CustomVehiclePathfind (cost terms) - https://github.com/Jimmyokok/CustomVehiclePathfind
- krzychu124/Traffic (lane connections) - https://github.com/krzychu124/Traffic
- Traffic Lights Enhancement discussion (vanilla green extension) - https://github.com/slyh/Cities2-TrafficLightsEnhancement/discussions/3
- TrafficClearTheWay (emergency/accident handling) - https://github.com/BastiAKA/TrafficClearTheWay
- Steam threads on despawning and parking fees - https://steamcommunity.com/app/949230/discussions/0/3951406499788029158/ and https://steamcommunity.com/app/949230/discussions/0/4843148768109432236/
- Forum thread on flow stuck near 60% (only the search summary was readable) - https://forum.paradoxplaza.com/forum/threads/every-city-ive-ever-built-the-traffic-flow-is-always-60-at-all-times-of-day.1852960/
- CS2 supply chains (freight) - https://chillplacegaming.com/supply-chain-cities-skylines-ii/
- Realistic Trips mod - https://mods.paradoxplaza.com/mods/77171/Windows

Not found online, so flagged as memory or my own design: CS2's path-cost defaults, the flow % formula, the despawn timer, parking slots per segment, per-lane capacity. Decompiling `Game.dll` (`Game.Pathfind`, `CarNavigationSystem`, road flow systems) would settle them; nothing in this design depends on those exact numbers.

Repo files read: `mods/city/CONTRACT.md`, `OpenRA.Mods.City/Traits/World/TrafficManager.cs`, `Traits/Traffic/WithLaneOffset.cs`, `Traits/World/RoadLayer.cs`, `mods/city/rules/traffic.yaml`, `OpenRA.Mods.Common/Traits/Mobile.cs`, `Activities/Move/Move.cs`, `OpenRA.Game/Graphics/WorldRenderer.cs`.
