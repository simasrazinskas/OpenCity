# 06 - Industry, Specialized Industry and Logistics (OpenCity design)

Domain 6 of the CS2-parity research. Scope: natural resources, specialized industry areas and extractors, generic manufacturing, production chains, warehouses, cargo and trucks, import and export, industrial pollution, office (immaterial) production, and the production info views.

Confidence tags: **[W]** documented on the CS2 wiki or a Paradox dev diary, **[C]** community report, **[I]** my inference or memory, verify before relying on it, **[D]** a design decision for OpenCity. CS2 publishes almost no internal numbers (per-cell amounts, truck capacities, work-per-unit), so every number in section 3 is a **[D]** value to be tuned, not a CS2 value.

Aligned with: `05-economy.md` (25-resource catalogue, company struct, `ILogistics.HaulCost`), `04-zoning-buildings.md` (zone types 13-16, warehouse lots, hubs), `10-environment-time-ui.md` (`CityClock`, `PollutionLayer`, natural-resources info view), `02-traffic.md`, `08-public-transport.md` (`ICargoTerminal`) and `03-networks.md` (`OutsideConnection.Kind`).

---

## 1. CS2 mechanics (what we are approximating)

### 1.1 Three tiers of goods [W]

CS2 has 36 resources: 10 raw materials, 18 material goods and 8 immaterial goods [C: in-game Production tab as reported in guides; the wiki Economy page lists them in three tables]. Each resource has three properties [W: [Economy & Production feature page](https://www.paradoxinteractive.com/games/cities-skylines-ii/features/economy-production)]:

- **Price**: what buyers pay, and how much households buy.
- **Weight**: drives transport cost (steel and stone are heavy, pharmaceuticals light).
- **Space**: how much floor area a company needs per unit produced, which drives manufacturing profitability and location choice.

Material companies live in industrial buildings, immaterial producers (software, telecom, financial, media) in offices, and consumer companies (shops, restaurants) in commercial buildings. Companies are virtual entities with logos and a focus on one material [W: [wiki Economy](https://cs2.paradoxwikis.com/Economy)].

### 1.2 The production chain [W: wiki Economy page tables]

Raw materials come only from specialized industry areas (or imports). The full CS2 recipes, as listed by the wiki:

| Output | Inputs | Output | Inputs |
|---|---|---|---|
| Metals | Ore | Textiles | Cotton + Livestock + Petrochemicals (wiki lists all three as uses; exact input split not published) |
| Steel | Coal + Metals | Timber | Wood |
| Minerals | Rock (stone) | Paper | Timber |
| Concrete | Rock | Furniture | Timber |
| Machinery | Metals + Steel | Food | Livestock + Vegetables |
| Petrochemicals | Crude Oil + Grain | Beverages | Grain + Vegetables |
| Chemicals | Crude Oil + Minerals | Convenience Food | Grain + Livestock |
| Plastics | Chemicals + Petrochemicals | Pharmaceuticals | Chemicals |
| Electronics | Minerals + Plastics | Vehicles | Metals + Plastics |
| Software | Electronics (office) | Telecom | Electronics + Software (office) |
| Financial | Software (office) | Media | Software (office) |
| Lodging, Meals | Food (commercial) | Entertainment | Beverages (commercial) |

Raw materials: Wood (forestry), Grain, Livestock, Vegetables, Cotton (farming), Crude Oil, Metal Ore, Coal, Rock (stone mining). Coal also feeds electricity; Wood feeds heating. Concrete is consumed as building upkeep. Each industrial company has at most **two inputs and one output** [I: matches `IndustrialProcessData` with Input1/Input2/Output, per modding crash logs].

### 1.3 Natural resources [W: [wiki Natural resources](https://cs2.paradoxwikis.com/Natural_resources)]

- Five map layers: **groundwater**, **fertile land** (yellow overlay), **forest** (green), **ore** (blue), **oil** (black). Darker shade = better land. **Fish** was added later as a sixth layer with the Bridges & Ports expansion [C: Paradox forum / Steam threads].
- **Renewable**: groundwater, fertile land, forest, each with a constant renewal rate shown in the info view. Fertile land and forest are **destroyed by ground pollution**; forest also by forest fires. Groundwater dries temporarily if overdrawn and is polluted by ground pollution.
- **Non-renewable**: ore and oil are "eventually depleted"; extraction gets less and less over time [W: Paradox feature page]. No per-cell amounts or depletion rates are published. Players report farms dropping to about 50% efficiency and oil depleting fast after a 2024 patch, and one estimate that a 100k city needs 2-3 map tiles of oil [C: [Steam thread](https://steamcommunity.com/app/949230/discussions/0/691994366768732533/)]. Treat as anecdote.
- Stone mining and livestock farming need no resource and can be built anywhere.

### 1.4 Specialized industry areas [W]

- Nine types: livestock, grain, vegetable, cotton (textile fibre) farming; forestry; stone, coal, ore mining; oil drilling [W: [Industry page](https://cs2.paradoxwikis.com/Industry), [dev diary #9](https://colossalorder.fi/?p=1809)]. Unlocked by milestone, from Large Village (livestock, stone) to Big Town (oil) [C: guide summary].
- **Placement**: first place a central **hub** building, then use the Specialized Industry Area tool to place corner nodes (like the district tool) inside a **ring around the hub**. The main building automatically harvests resources inside the area. Vehicles (tractors, harvesters, trucks inside the zone) run on simulation-made paths, not on roads, and only inside the area [W: dev diary #9].
- Areas work without resources but earn less; they "cannot become abandoned" and "cannot go bankrupt: they downsize production and employees when profits are down" [W].
- Companies move in and out of the extractor buildings as the area becomes more or less attractive [W].
- Forestry: clear-cutting without replanting creates long-term supply problems [W: chillplacegaming guide].
- Extractors are normally polluting. Wiki: stone and coal mining, ore mining, oil drilling have **medium noise, ground and air pollution**; oil drilling has higher fire hazard; **farming and forestry do not emit ground or air pollution** [W: Industry and Pollution pages].
- The player does not profit from the extractors directly: the company that moved in exports the goods; the city gets taxes and cheaper local inputs for manufacturers [C: Steam production thread].

### 1.5 Generic industry, companies and efficiency [W]

- Industrial zones manufacture goods from materials delivered by trucks from outside connections or from local specialized industry. Companies prefer selling locally, but surplus is exported, and "selling to the local market is always more profitable" [W: Industry page].
- Companies pick a site by resource access, production value, **land value (they prefer cheap land)** and worker availability, and scale production up and down by profit, hiring and firing [W: dev diary #9].
- Transport cost = f(distance, weight); it is deducted from profit. Heavy goods push production toward the input source, light goods toward customers.
- **Efficiency** defaults to 100% and depends on employee happiness and availability, utilities, fees, signature buildings and a **city production specialization bonus: up to +15% (115%) at 10 kt of production of a resource** [W: wiki Economy].
- Higher level buildings use fewer utilities per unit, produce faster and hire more educated workers [W].
- Industrial demand rests on: citizens wanting industrial jobs (labor availability: uneducated/poorly educated employables above 1.25x free slots [C: forum thread]), commercial need for goods, warehouse availability (storage capacity below need) and local extraction [W/C]. It is the larger of building demand and storage demand [I: InfoLoom description].
- **Economy 2.0** (patch 1.1.5): work per unit reduced for all products, two-tier pricing (industry buys at a discount, retail at normal price), commercial demand tied to what households consume, subsidies removed [W: [dev diary](https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-one)].

### 1.6 Cargo and logistics [W unless tagged]

- Companies **order** resources; an order is fulfilled by delivery trucks or vans from the cheapest seller, where order price = resource price + transport cost. Sellers can be other companies, **warehouses** and outside connections.
- **Warehouses** are industrial buildings that produce nothing but trade; they try to stay stocked and balanced and redistribute stock evenly across the city. "Several smaller warehouses near production clusters beat one mega warehouse" [W/C: [chillplacegaming](https://chillplacegaming.com/supply-chain-cities-skylines-ii/)].
- **Cargo train terminal and cargo harbor** double as warehouses with an outside connection: 15.5 kt storage each, harbor ships carry 1,000 tons, a rail yard handles about 10 vehicles; cargo planes are the fastest but smallest [W: [wiki Transportation](https://cs2.paradoxwikis.com/Transportation), [feature page #3](https://www.paradoxinteractive.com/games/cities-skylines-ii/features/public-cargo-transportation)]. Players report terminals export only via their own hard-capped truck fleet (12-16 vehicles) and that warehouse trucks only deliver to other warehouses [C: unreliable, early-2023 builds].
- Surplus is auto-exported through outside connections, deficits auto-imported, both via trucks on the highway connection. Imports cost more, exports fetch less. Trade of services has fixed rates (electricity 5,000 C/MW import, 2,500 export) [W].
- One player post: commercial buildings hold about 8 t and use a 4 t van [C]. **[I]** The game keeps per-company fleets whose trucks return to the home building.
- Offices need no goods trucks for their *output* (immaterial goods are "transmitted wirelessly") but Software and Telecom offices need Electronics delivered [W].

### 1.7 Pollution from industry [W: [Pollution page](https://cs2.paradoxwikis.com/Pollution)]

Ground pollution is wind-independent, lingers, kills trees and fertile land and seeps into groundwater. Air pollution follows wind and is washed by rain. Noise comes from industry, commerce and especially large trucks.

**Unpublished:** per-cell amounts, extraction and depletion rates, truck capacities, storage limits, work-per-unit and truck choice. Section 3 keeps CS2's structure (requests, landed cost, fleets, warehouses, depletion shape) with **[D]** numbers calibrated to `05-economy.md`.

---

## 2. Current OpenCity state and gaps

### 2.1 What exists (read from the code)

| Area | State | File |
|---|---|---|
| Industrial zone | One zone `Industrial` (type 5), growables `ind-1..3` with `MaxJobs` 10/16/24, `Pollution` 40/30/20 | `rules/growables.yaml` |
| Production | **None.** Industry is "jobs that pay tax". No goods, inventory, recipes or inputs. Industrial demand is `8 + 25% workforce + 10% commercial jobs` minus existing industrial jobs | `CityManager.Economy.cs` `UpdateDemand` |
| Natural resources | None in the sim. The map has trees as `tree-1..4` actors (AutoClear), water terrain, rough/dirt decoration only. The `map.bin` resource bytes (type, density) are written as zeros by `genmap.py` | `tools/genmap.py`, `rules/nature.yaml` |
| Extractors | None. `ZoneType` is frozen at 6 values (`CityTypes.cs`); `CityPlaceable` categories are fixed (power, water, police, fire, health, education, parks, special) | `CityTypes.cs`, `CityPlaceable.cs` |
| Trucks | `TrafficManager` spawns a real `truck` actor on 55% of trips that start at industry (or enter from a highway toward industry). Trips are random endpoints: no cargo, no buyer, no seller. 629 lines, cap `min(200, pop/6)`, own BFS over 4-connected road cells | `TrafficManager.cs` |
| Outside connections | `highway-w/e/n/s` mark map edges. They lay a road and are spawn points for random commuter trips. No trade, no capacity, no prices | `OutsideConnection.cs` |
| Pollution | One byte map, `CityBuildingInfo.Pollution` stamped every 100 ticks with radius 6 and linear falloff. Industry buildings are exempt from pollution unhappiness | `CityCoverageLayer.cs`, `CityManager.Daily.cs` |
| Info views | `CityInfoView` enum (frozen): power, water, land value, pollution, services, traffic, happiness. No resource, production or freight view | `CityTypes.cs`, `InfoViewLayer.cs` |
| Time | 1 day = 25 ticks = 1 s at speed 1. A 40-cell truck trip is about 425 ticks = 17 days | `CityManager`, `TrafficManager` |

### 2.2 Gaps against the goal

1. **No resources, no recipes, no stock.** There is nothing for industry to make, buy or sell, so industrial zoning is only a job source.
2. **No map resource data.** Forests exist only as decorative actors; there are no fertile land, ore or oil layers and no generator for them.
3. **No extractor concept**: no hub, no area tool, no area-to-company binding, no depletion or renewal.
4. **Trucks are decoration.** They cannot represent a shipment, have no payload, and nothing is delivered. Visible actor trucks cannot scale to real freight volumes (see 3.7).
5. **No logistics layer**: requests, dispatch, fleets, warehouses, cargo terminals, import and export with throughput limits.
6. **Pollution is one scalar** and does not hurt farms or forests; domain 10 replaces this with `PollutionLayer`.
7. **No production UI**: no resource map, no per-resource supply/demand panel, no freight view.
8. **Day length is incompatible with trips** (17 "days" per trip). Shipments need real time in ticks and `CityClock` (domain 10: day = 2,400 ticks).

**Reusable:** `TrafficManager`'s road components, BFS, sampled actors and `GetTrafficLoad`; the `CityBuilding` registry; `CityPlaceable` and `CityDragOrderGenerator` for hubs and the area tool; the `map.bin` resource bytes; the `truck` sprite.

---

## 3. Proposed design

### 3.0 Principles

1. **Plain data, not actors.** Deposits are per-cell arrays, companies and stock live in `05`'s `Company[]`, shipments are structs in a pooled array. Only a small sampled set of trucks, and a few cosmetic extractor props, are actors.
2. **Integer and deterministic.** Everything in synced code uses `int`/`long`, `world.SharedRandom` or a cell/tick hash, iteration in id order, no `Dictionary` enumeration without sorting. Same rules as `CONTRACT.md` section 7.
3. **Tick-based timing.** Shipment times are in world ticks. Rates for companies stay "per `CityClock` day" as in `05`; the logistics code converts with `TicksPerDay` (2,400 by domain 10, 25 today). Everything below is written so that only the rules value changes if the clock changes.
4. **Staged delivery.** Each phase works with the previous phase's economy fallback: P1-P2 use **instant transfer with a distance cost** (`05` fallback); P3 swaps in real shipments behind the same call.
5. **One new world trait per concern**, each in its own file, so domain owners do not collide: `NaturalResourceLayer`, `ExtractorAreaLayer`, `Logistics` (plus `FreightLoad`). Names avoid `ResourceLayer`, which already exists in `OpenRA.Mods.Common.Traits`.

### 3.1 Natural resource layer (`NaturalResourceLayer`, world trait)

**Layers** (one `CellLayer<ushort>` each, all integer):

| Layer | Source of initial data | Value meaning | Renewable | Consumers |
|---|---|---|---|---|
| `Fertile` | `map.bin` resource type 1, density 1..255 | richness 0..1000 (`density * 4`) | yes, polluted soil dies | grain, vegetable, cotton farms |
| `Forest` | tree actors on the map: each tree actor counts 100 stock on its cell | stand 0..100 per cell | yes by replanting | forestry |
| `Ore` | type 2, density 1..255 | units remaining = `density * OreScale` (default 2, max 510) | **no** | ore mines |
| `Oil` | type 3, density 1..255 | units remaining = `density * OilScale` (default 2) | **no** | oil wells |
| `Stone` | none: `Clear`/`Rough` land counts 100, `Rough` terrain 150 | yield factor only, never depletes | n/a | quarries |
| `Fish` (P5) | water cells, from distance to shore | stock 0..1000 | yes | fishing piers |
| groundwater | owned by domain 10 (`gwDeposit`) | read-only here, shown in the resource view | per `10` | water pumps |

Why `map.bin`'s resource bytes: `genmap.py` already writes 2 zero bytes per cell ("resource type, density" in `CONTRACT.md` section 9), `Map.Resources` is a `CellLayer<ResourceTile>(Type, Index)` the engine loads for free, and it is included in the map UID. One resource type per cell is enough because forest is separate (actors) and fertile/ore/oil patches do not need to overlap. The layer copies these bytes into its own mutable arrays at world load; it never writes back to `Map.Resources`. If a map has all-zero resource bytes (old maps, `boot-test`, `shellmap`) the layer **generates a deterministic fallback** from `Seed` (default: map UID hash) with integer value noise so every map has some deposits (`GenerateIfEmpty: true`).

**Map generation (`tools/genmap.py`, WP A1)** - for each of the three playable maps (`green-valley`, `lakeside`, `riverbend`):

- **Fertile**: 3-5 patches, radius 8-14, density from an fbm noise; prefer cells within 14 of a river or lake (`water_distance`), kinds `grass`/`meadow` only; never `rough`, `dirt`, shore or water. Density 120..255 in the core, fading to 40 at the edge.
- **Ore**: 2 blobs (radius 5-8) on `rough`/`dirt` cells (hills), density 100..255; one blob 15-40 cells from the highway anchor, one far corner, so the player must choose and later import.
- **Oil**: 1 field (radius 4-6), far (35-70 cells from the anchor), density 90..220, never overlapping ore.
- Nothing inside `guard` (the guaranteed 38x21 build area) except fertile soil under meadow, so the autotest scenario is unaffected.
- Forests stay as `tree-*` actors (already generated).
- Preview PNG tints ore (blue-grey), oil (black) and fertile (olive) so players can read the map in the New City screen.

**Dynamics** (pulse = 25 ticks, applied to cells in an *active set*, i.e. cells inside any extractor area or with ground pollution above 100, so the cost is proportional to activity, not map size):

```
Fertile:  fert += RenewPerPulse (1)                       // up to base richness
          fert -= max(0, ground - 100) / 128             // ground: 0..1000 from PollutionLayer.Ground
          fert -= useWear                                  // optional soil fatigue, default 0
Forest:   no per-pulse change; changed by felling and replanting (3.3)
Ore/Oil:  amount -= extracted units                        // only extractors change it
Stone:    constant
```

At 96 pulses per `CityClock` day (= month), fertile land regains about 96 points of 1,000 per month, i.e. recovers fully in about 10 months, and a cell with ground pollution 500 loses about 3 points per pulse net of renewal, so dies in roughly 3-4 months. Domain 10 proposed `Fertility = Base - ground/2` as an instant formula; this stateful version replaces it (it recovers once the pollution is gone, as CS2 groundwater does [W]).

**Depletion shape** [D]. Output of an ore/oil area is multiplied by a *richness factor* `rf = clamp(100 * remaining / (initial * 25%), 20, 100)`: full output until the area has 25% of its original stock left, then a linear taper down to 20%, so "less and less is extracted" [W]. At 0 stock the hub's company goes dormant (`Throttle 0`) and the notification `industry-deposit-depleted` fires (CS1 converted depleted extractors into processors; CS2 does not publish this, so we just warn). A `DepletionMultiplier` rule (default 100, sandbox 0, hard 300) scales the units removed per unit produced.

**Calibration** [D]: a full-richness ore cell holds 510 units; an area cell yields `1.5 * rf` units per `CityClock` day at full workforce (3.3). A 5x5 area at average richness 250 holds 25 x 500 = 12,500 units and yields about 37 units per month, lasting roughly 28 game years before the taper (about 9 real hours at 1x, 3 hours at 3x). Deposits therefore deplete visibly in a long session but not in the first hour. Rich-vs-poor and "how many months left" are shown in the hub info panel.

**Public API** (read-only for others):

```csharp
int GetAmount(NaturalResourceKind kind, CPos cell);       // 0..1000 / units
int GetRichnessPercent(NaturalResourceKind kind, CPos cell);  // 0..100 against initial
int Remove(NaturalResourceKind kind, CPos cell, int units);   // returns removed (synced callers only)
int Version { get; }                                      // bumped when any cell changes (overlay cache)
IEnumerable<CPos> CellsWith(NaturalResourceKind kind);   // for the info view
[VerifySync] int StateHash { get; }                       // sum over arrays, hashed every 10 pulses
```

`enum NaturalResourceKind : byte { Fertile, Forest, Ore, Oil, Stone, Fish }` lives in the new file (not in frozen `CityTypes.cs`).

### 3.2 Specialized industry: hubs and the area tool

**Hub buildings** (`CityPlaceable`, new toolbar category `industry`, WP F adds the button). Five hubs cover CS2's nine types; the farm hub has a **crop selector** instead of four separate buildings (saves art, lets the player re-assign):

| Hub actor | Footprint | Product (recipe output) | Needs on the area cells | Cost | Unlock pop | Max area cells | Ring radius |
|---|---|---|---|---|---|---|---|
| `farm-hub` | 3x3 | Grain, Vegetables, Cotton, Livestock (selector) | Fertile (not Livestock) | $4,000 | 250 | 144 | 8 |
| `forestry-hub` | 2x2 | Wood | Forest stand | $3,500 | 500 | 144 | 8 |
| `quarry-hub` | 3x3 | Stone | none (any land, `Rough` +50%) | $6,000 | 250 | 64 | 6 |
| `mine-hub` | 3x3 | Ore | Ore | $9,000 | 1,000 | 64 | 6 |
| `oil-hub` | 2x2 | Oil | Oil | $14,000 | 2,500 | 36 | 6 |

All hubs `RequiresRoad: true` (the hub is the loading dock), carry `CityBuilding` (so power, water, jobs, land value and happiness apply), `Bulldozable` (RefundPercent 0; bulldozing a hub frees its area cells) and a new `ExtractorHub` trait (`Product`, `Radius`, `MaxAreaCells`, `Kind`). Unlock thresholds follow CS2's milestone gating [C], re-based onto OpenCity's milestone table (`CONTRACT.md` section 7).

**Area tool** (CS2's corner-node tool, adapted to the grid):

1. Select a hub (click) or place a new one; the info panel gets **Paint area** and **Clear area** buttons (or hotkey `A` with the hub selected).
2. Drag a rectangle exactly like the zone tool (`CityDragOrderGenerator`). Rectangles union into the hub's area. The preview colours each cell by resource value: heat frame 0 (red, none) to 10 (green, rich) using the existing `overlays/heat` sequence, a white outline marks the legal ring around the hub, and a tooltip near the cursor shows `+N cells, capacity X units/month, jobs Y, cost $Z`.
3. Order: `CityExtractorArea`: `Target` = end cell, `ExtraLocation` = start cell, `ExtraData` = hub `ActorID`, `TargetString` = `add` or `remove`. A second order `CityExtractorSetProduct` (`ExtraData` = hub id, `TargetString` = resource name) changes the farm crop. Handler: new player trait `ExtractorTool` (own file; `ConstructionTools` and `CityTypes.cs` are untouched). Costs go through `CityManager.TrySpend(amount, "industry")`.

**Validation** (identical on preview and in the handler): hub exists, owned, `IsOperational` not required; cell within `Radius` (Chebyshev) of the hub footprint; terrain not `Water`/`Road`; no actor except `AutoClear` ones; cell not in another hub's area; total area <= `MaxAreaCells`. Cost: $2 per cell, +$5 per tree cleared on non-forestry areas (existing `Bulldozable.Cost`). A cell with zero resource value is **allowed** (CS2 allows resource-less areas with lower income [W]) and flagged yellow in the preview.

**Area state** lives in a new world trait `ExtractorAreaLayer`: `CellLayer<uint>` of hub `ActorID` (0 = none), `GetHub(cell)`, `HubCells(hubId)` (sorted row-major), `Version`. `ZoneGrowth.CanGrowAt` must reject cells with a hub (one-line hook for WP D); `ZoneLayer.SetZone(cell, None)` is called when a cell joins an area. Roads inside an area are never part of it (the cell is skipped on `add`).

**Not zone types.** `04-zoning-buildings.md` proposes `ZoneType` 13-16 for extractor areas. I recommend **not** extending the frozen enum and order protocol: cell-to-hub binding needs a hub id, which a byte zone cannot carry. Domain 4 keeps its warehouse zone; the area overlay gets its own sequence (section 6).

**Cosmetics** (no sim cost): `ExtractorAreaRenderer` draws field tiles with a `TerrainSpriteLayer` (crop rows, quarry pit, mine gravel, oil pad), 4 variants chosen by cell hash. Forestry areas draw nothing (the trees are the art). Oil hubs spawn up to 6 `oil-derrick` prop actors on the richest cells (P2). Props are plain actors with no `CityBuilding`, disposed with the hub.

**Specialized industry never abandons or bankrupts** [W]: `GrowableBuilding` is not attached to hubs, and their company runs the extractor rules in 3.3.

### 3.3 Extractor company and production

Extractors are `CompanyKind.Extractor` records in `05`'s array, bound 1:1 to the hub building (`BuildingActorId`). Domain 6 supplies the *capacity* and the *depletion*; domain 5's daily step does hiring, wages, cash and throttle.

**Capacity** (summed over the hub's area cells, recomputed on area change and every 25 pulses, cached in `ExtractorHub`):

```
capMilliPerDay  = sum over area cells of  CapPerCellMilli[kind] * factor(cell) / 1000
factor(cell)    = Fertile: richness 0..1000 (livestock: 1000 on Clear terrain)
                  Forest : stand 0..100 * 10
                  Ore/Oil: rf (richness factor) * 10, 0 when stock = 0
                  Stone  : 1000, 1500 on Rough
CapPerCellMilli = Grain 3000, Vegetables 2000, Livestock 1500, Cotton 2000, Wood 4000, Stone 3000, Ore 1500, Oil 1000
JobsMax         = min(hub MaxJobs, ceil(capMilliPerDay / (q[product] * 1000)))
```

`q` is `05`'s units per worker per day (Grain 7.0, Ore 3.1, ...). So a 5x5 full-fertility grain field gives 75 grain per month and employs about 11 workers; a 5x5 ore patch employs 12; richer or larger areas employ more. **Area size and richness drive jobs and output**, replacing the old "industrial building = 10-24 jobs".

**Production** (called from `05` step 3): `produced = min(filledWorkers * q * eff * throttle, capMilliPerDay)`. After producing, the economy calls `hub.OnProduced(units)`:

- Ore/Oil: spread `units * DepletionMultiplier / 100` over area cells proportionally to remaining stock (`NaturalResourceLayer.Remove`), deterministic cell order.
- Forestry: `fellProgress += units`; each `WoodPerTree` (20) units fells one tree actor (the lowest-hash tree cell in the area, `AddFrameEndTask` dispose, stand 0 on that cell).
- Fertile: optional `useWear` (rules, default 0; CS2 behaviour is contested [C]).

**Forest renewal**: every `ReplantTicks` (600, scaled by staffing) the hub plants one `tree-1..4` actor on a free in-area non-road cell (cell hash picks variant). Steady state is about 80% stand, so default forestry is sustainable; a policy order `CityExtractorSetPolicy` (P3, `ClearCut` off by default) disables replanting for +25% output, reproducing CS2's "clear-cutting creates long-term supply problems" [W].

**Rent, cash, pollution**: rent per `05`. Pollution per `PollutionLayer` keys on the hub (3.9). Efficiency, specialization bonus and happiness modifiers as in `05` 3.2.

**Never bankrupt** [W]: if `ProfitEma < 0` the throttle falls (min 30) and workers are fired; the company never closes while the hub stands. If stock is zero the company idles at 0 jobs but the hub stays, with a notification.

### 3.4 Production chain catalogue and generic industry

The resource list, prices, weights, recipes and `q` values are **owned by `05-economy.md` 3.1** (25 resources, ids 1-25, loader accepts ids to 63). Domain 6 adds only the *logistics and pollution attributes* below, as extra columns in the same `rules/economy.yaml` resource nodes.

| Weight class (05) | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Units per truck load | 40 | 30 | 24 | 16 | 12 |

(`TruckLoadPoints / WeightPoints`; rules `UnitsPerTruck: [0, 40, 30, 24, 16, 12]`. Class 0 = immaterial, no truck.)

Emission per fully staffed company at 100% throttle, `Ground/Air/Noise` on the 0..100 scale of `10-environment-time-ui.md` 3.4 [D, scaled by output via `Scale: Output`]:

| Host / recipe | Ground | Air | Noise | Notes |
|---|---|---|---|---|
| Farm hub (any crop), forestry hub | 0 | 0 | 8 | CS2: farming and forestry do not pollute [W] |
| Quarry (Stone) | 20 | 30 (dust) | 60 | medium noise, ground, air [W] |
| Ore mine | 50 | 20 | 50 | |
| Oil well | 60 | 20 | 25 | + `FireRisk` flag (P5) |
| Timber, Concrete | 20 / 30 | 25 / 40 | 35 / 40 | sawmill, batch plant |
| Metals | 55 | 50 | 45 | smelter |
| Petrochemicals, Plastics | 60 / 45 | 60 / 45 | 30 / 30 | refinery, polymer |
| Food, Beverages, Textiles | 15 / 15 / 25 | 10 / 10 / 20 | 20 | |
| Furniture, Electronics, Vehicles, Machinery | 15 / 25 / 30 / 25 | 25 / 15 / 25 / 20 | 25 | |
| Warehouse | 0 | 5 | 20 + truck noise | trucks add road noise via freight load |
| Office | 0 | 0 | 0 | |

**Generic industrial zone** (`ind-1..3` and, per domain 4, warehouse lots): each building hosts one `Processor` company (05) whose recipe is chosen by profit scoring. The building's static `Pollution` becomes the **recipe's** emission scaled by output (an empty or idle factory stops polluting, as domain 10 intends). Higher building level = more jobs and more throughput per cell (`SpaceMultiplier`, domain 4) but lower pollution per unit (CS2: higher levels are cleaner and use less utility [W]).

**Chains the player can see and build toward** (arrows are `05` recipes):

- Wood -> Timber -> Furniture
- Stone -> Concrete (also the building upkeep input, phase 5)
- Ore -> Metals -> Vehicles (with Plastics), Electronics (with Plastics), Machinery
- Oil -> Petrochemicals -> Plastics; Cotton + Petrochemicals -> Textiles
- Livestock + Vegetables -> Food; Grain + Vegetables -> Beverages
- Electronics -> Software -> Financial, Media (offices)

A city can run entirely on **imports** (CS2 allows this [C]); local extraction cuts landed cost and raises the profit score of the matching recipe, which is how "local resources increase industrial demand" [W] enters the model (3.11). Phase 5 adds Coal, Steel, Minerals, Chemicals, Pharmaceuticals, Paper, Convenience Food, Telecom, Lodging, Recreation and Fish (the other 11 CS2 resources) by YAML only.

### 3.5 Logistics core: nodes, dispatch, shipments

`Logistics` is a new **player trait** (it must see `CityEconomy` and the player's buildings) living in `Traits/Simulation/Logistics.cs`. All state is plain data.

```csharp
enum NodeKind : byte { Company, Storage, Terminal, Outside }
enum FreightMode : byte { Truck, Rail, Ship, Air, Wire }   // Wire = immaterial, no vehicle

struct LogiNode
{
    public int Id;              // monotonic, never reused
    public NodeKind Kind;
    public int CompanyId;       // 05 company or 0 (terminal, outside)
    public int ActorId;         // building actor, for removal handling
    public CPos Road;           // access road cell (4-adjacent road of the building)
    public ushort Component;    // road network component (from the TrafficManager labelling)
    public int FleetOffset; public byte FleetSize;   // slice of int[] fleetFreeAt
    public int OutboxCap;       // max units in StockOut before the company throttles (from 05)
}

struct Shipment
{
    public int Id, FromNode, ToNode;
    public byte Resource, Mode;
    public ushort Units;
    public int DepartTick, ArriveTick, ReturnTick;   // truck is busy until ReturnTick
    public int UnitPriceCents, FreightCents;          // settled by 05 at dispatch
    public ushort PathCells;
    public int VisibleActorId;                        // 0 when not sampled
}
```

**Who requests.** The economy's market step (05 3.4) picks the seller (cheapest *landed* cost: price + freight) and calls `Logistics.TryDispatch(fromNode, toNode, resource, units, unitPriceCents)`. Domain 6 does not decide *what* to buy, only whether and when it can move. Retail shoppers (05 3.5) do not use trucks: retail stock is refilled by Logistics from processors and warehouses; household shopping trips are citizen trips (domain 1/2).

**Dispatch (synchronous, in the economy's tick):**

1. Both nodes alive and `Component` equal. Route length `L` (cells) comes from `RouteCache` (3.6). If unreachable: return false, the market tries the next candidate.
2. `units = min(units, seller.StockOut, buyer.FreeInputSpace - buyer.InTransit)`. Split into `n = ceil(units / UnitsPerTruck[class])` loads.
3. For each load take the earliest free truck slot in the seller's fleet. `Depart = max(now + LoadTicks, slot.FreeAt)`, `Arrive = Depart + L * TruckTicksPerCell`, `ReturnAt = Arrive + L * TruckTicksPerCell + TurnaroundTicks`. If `Depart - now > MaxQueueTicks` (default 2,400), stop: the remainder stays in the outbox, so a seller with too few trucks backs up and 05's throttle rule (`stock > 80% cap`) lowers output. **A fleet that is too small genuinely limits production**, which is why warehouses and short chains matter.
4. Move the units out of the seller's `StockOut` into the shipment, add them to the buyer's `InTransit` (so it does not re-order), append to `shipments[]`, insert the id into the **timing wheel** bucket `Arrive & 8191` (a `List<int>[8192]`, 24 bytes per empty bucket).
5. Return the unit count so 05 can settle money (buyer to seller at `UnitPriceCents`, freight to the `freight` ledger line, `05` decides who pays).

**Arrival** (once per tick, drain bucket `tick & 8191`, shipments in id order): if the destination still exists and is connected, `buyer.StockIn += units * 1000`, `InTransit -= units`, trade statistics `consumed/imported` are updated, and the shipment slot is freed. Shipments are the *only* path by which goods change companies after P3, and all of it is integer arithmetic.

**Costs**: freight cents per unit = `05`'s formula until `HaulCost(from, to, resource)` uses the real path: `weightClass * L * FreightMilliCentsPerWeightCell / 1000 * modeMultiplier`, `modeMultiplier` = Truck 100, Rail 35, Ship 25, Air 300 (air is flat: distance-insensitive [W]).

### 3.6 Fleets, routes, timing, failure

- **Fleet size** (`TruckSlots`): hub 4; processor `2 + level`; warehouse `6 + lotCells/4` (max 16); cargo terminal 16 [C: 12-16]; outside connection: unlimited slots but `TradeUnitsPerDay` (05 default 600, shared import and export) is the real cap. Slots are an `int[] fleetFreeAt` array; no truck objects exist.
- **`RouteCache`**: BFS over the 4-connected road cells (reusing `TrafficManager`'s component labelling and BFS, later domain 2's router) from a node's `Road` cell to all cells, lazily per source node, invalidated by `RoadLayer.NetworkVersion`, at most 2 BFS per tick (queue the rest). A BFS on a 16k-cell map is about 0.1 ms. Stores `dist[]` as `ushort` per source; memory bounded by an LRU of 256 sources. Until the shared router lands, hop count is the length (matches how `TrafficManager` moves today).
- **Timing**: `TruckTicksPerCell` default 14 (the existing `truck` has `Mobile.Speed 76`, so 1024/76 = 13.5). With the old 25-tick day a 40-cell trip would last about 22 days; with `CityClock` (2,400 ticks/day) it is 0.23 day (5.6 game hours). **P3 requires `CityClock`** or a rules switch `ShipmentTimeScale` that divides ticks (documented fallback).
- **Failure**: roads removed while goods are on the way. On `NetworkVersion` change the cache is dirty; at arrival, if the components differ the shipment **returns**: units go back to the seller's `StockOut` (or are scrapped if the seller is gone, ledger `loss`), the money is refunded, and a stranded-shipments counter per resource feeds a warning ("freight blocked: roads cut"). Buyer closed or bulldozed: redirect to the nearest connected `Storage`/`Terminal` node, else return. Seller closed: shipments already left still arrive.
- **Determinism**: shipment ids monotonic; the wheel stores ids; no `Dictionary` enumeration; fleet slot selection takes the lowest index with the lowest `FreeAt`; `RouteCache` BFS expands neighbours in `CityUtils.Neighbours4` order.

### 3.7 Visible trucks and the traffic sim

Real freight volume is far above what actors can carry (a 50k-pop city moves roughly 4,000 loads per `CityClock` day [D estimate]), so **shipments are data and trucks are a sampled picture of them**.

- **Sampling**: when a shipment departs, `Logistics` calls `TrafficManager.SpawnFreightTruck(fromRoad, toRoad, shipmentId, arriveTick)` if `(shipmentId hash) % SampleDiv == 0` and the visible budget `MaxVisibleTrucks = 24 + population / 600` (cap 120, counts against the existing `MaxVehicles`) has room. The choice is a hash, never camera visibility (actors created from non-synced decisions would desync). The trip home is sampled the same way.
- **Puppet mode (P3)**: the truck follows the stored BFS path at exactly the speed that makes it reach the destination at `ArriveTick` (`TruckTicksPerCell`), via a tiny `FreightTruck` trait that sets position each tick (no collisions, no stuck vehicles, no effect on the shipment if it is disposed early). It reuses `^Vehicle` art and `WithLaneOffset`.
- **Real mode (P4, with domain 2)**: the sampled truck is a normal pathing vehicle in traffic; it carries `loadWeight = SampleDiv` shipments' worth of road load. The shipment still arrives on schedule (data wins), the truck is disposed when it gets there or after `MaxLifetime`.
- **Congestion without actors**: at dispatch, `Logistics` calls `IFreightFlow.AddFlow(pathCells, vehicleEquivalents)` (heavy trucks count 2.5 cars). Per-cell `FreightLoad` (decaying like `TrafficManager.load`) is added into `GetTrafficLoad`, so a freight corridor really shows red in the traffic view and domain 10 can derive truck noise and air pollution from it. Cost: one increment per path cell per load, about 70 per tick at 50k pop.
- Imports and exports spawn their (sampled) truck at the highway end cell, so the player sees trucks stream in from the edge.
- The old random "55% of industry trips are trucks" rule in `TrafficManager.ChooseVehicle` is removed once P3 lands (commuters use cars only).

### 3.8 Warehouses, outside connections, cargo terminals

- **Warehouse** = `CompanyKind.Storage` (add to 05's enum), hosted by the industrial-zone warehouse lot (domain 4) and by a placeable `warehouse` (P4). Holds up to **3 resource types**, capacity `lotCells * 60 / weightClass` units per type, target fill 50%. Each day it posts **buy offers** for its types at a discount (price x 0.9) from producers whose outbox is above 60%, and **sells** to any buyer like a producer at base price + 5%. Balance between warehouses (P4): a warehouse above 80% pushes to one below 30% of the same type through ordinary shipments. Which resources it stores is picked at spawn from the largest unmet or imported resources (05 spawn scoring), so warehouses appear where the economy is short. Several small warehouses beat one big one automatically, because landed cost depends on distance [W: chillplacegaming].
- **Outside connection** node (`NodeKind.Outside`, one per `OutsideConnection` actor, domain 3 adds `Kind`): sells any resource at `base * 115% + freight(highway end -> buyer)` and buys at `base * 90% - freight`, bounded by `TradeUnitsPerDay` (05: 600 per highway) shared by import and export. Throughput is metered per tick in 25-tick pulses, so a saturated highway queues loads and **imports compete with commuters on the same road** (via freight flow).
- **Cargo terminals** (P5, `ICargoTerminal` from `08-public-transport.md`): `Terminal` nodes are warehouses with an outside link. Rail yard and cargo harbor each store 600 units per resource type (CS2 15.5 kt) [W] and import/export at `TerminalUnitsPerDay` (rail 400, harbor 900, air 80) at **mode multipliers** (Rail 35, Ship 25, Air 300 per 3.5). Local companies reach them with trucks, so the last-mile road is where congestion appears [W]. Harbor requires water within 2 cells (`RequiresTerrainNearby`), as for `waterpump`. Ship and train sprites are cosmetic, running on straight paths.
- **Surplus export** is automatic: if a company's outbox stays above 80% for a day, the market posts the surplus to the best Outside node (05 3.4 step 4).

### 3.9 Industrial pollution

Domain 6 only **declares** emission. Each hub and company registers `Pollution { Ground, Air, Noise, Scale = Output }` in `CityBuildingInfo` (the nested struct proposed by domain 10), with values from the 3.4 table. `PollutionLayer` stamps them every pulse. Two consumers live here:

- **Fertile land and forest** read `PollutionLayer.GetGround(cell)` (3.1 dynamics). Trees on cells above 600 for 3 days are removed (domain 10 rule), which also lowers forest stand.
- **Industrial exemption stays**: industry buildings are not unhappy from pollution (`CityManager.UpdateHappiness`), but their workers' homes are (domain 10 effects).

Freight air and noise come from `FreightLoad` through domain 10's per-road emission (`AirPerCar`, `NoisePerCar` with a x2 truck factor).

### 3.10 Offices and immaterial goods

- Offices (`Office` zone, 05 `CompanyKind.Office`) produce **Software, Financial, Media** (and later Telecom). Output is **immaterial**: `weightClass 0`, shipments use `FreightMode.Wire`: zero road distance, zero trucks, instant arrival (queued for the next pulse), delivered to a buyer company or a household consumer pool with `HaulCost = 0`.
- **Inputs are physical.** Software needs Electronics: that is a truck shipment to the office's access road. Financial and Media need Software (Wire). So an office district still generates some freight, but mostly from the Electronics plant, and it needs educated workers (05 job mix).
- Demand for Software comes from industry (CS2: software feeds industry, financial and telecom [W]), Financial and Media from households (adults/seniors favour banking, children and teens favour media [W], domain 1 supplies age mix).

### 3.11 Industrial demand and zone demand

Replace `CityManager.UpdateDemand`'s industrial term (owner: domain 5 with these inputs from domain 6). `DemandIndustrial = clamp(max(buildingDemand, storageDemand))` [I: InfoLoom] with:

- `buildingDemand` = jobs-for-workforce (existing) + `goodsPull`, where `goodsPull = 100 * (unmetLocalDemandValue + importValue) / (totalProductionValue + 100)`, summed over resources whose recipe needs no unavailable input, in 05's money units, per month.
- `storageDemand` = 40 when any stored resource type has fill above 85% or any imported resource has no warehouse within 30 cells, else 0.
- Extractor areas **do not change zone demand** [W per 04]; their effect is through the profit score of recipes that use their output (cheaper landed input, 05 spawn scoring).
- Office demand (domain 5) adds `immaterialPull` the same way for Software/Financial/Media.

### 3.12 Info views and statistics

New info views (WP F; the frozen `CityInfoView` enum needs additions or a second enum `IndustryInfoView` in a new file):

| View | Data | Colouring |
|---|---|---|
| Natural resources | `NaturalResourceLayer`, sub-modes Fertile / Forest / Ore / Oil / Groundwater (10), plus hub areas as outlines | yellow, green, blue, black (CS2 colours [W]), darker = richer |
| Production | per-resource stock fill of companies on each building | heat 0..10 (red = starved, green = full) |
| Freight | `FreightLoad` on road cells | heat (as traffic view), warehouse and terminal fill on building cells |
| Company profitability | `ProfitEma` per industrial building | heat (domain 5's view) |

**Production panel** (CS2 "Production" tab [W], `CITY_PRODUCTION_PANEL` in `chrome/city-panels.yaml`): rows for 25 resources with icon, produced, consumed, imported, exported, stock and surplus/deficit bar; selecting a row shows its recipe as a small input -> output -> uses diagram and a 12-month line chart. Data from `Logistics.Stats` (ring buffers of 12 monthly samples per resource, plus `ProducedToday`).

---

## 4. Interfaces with other domains

**Provided by domain 6**

| Surface | Members | Used by |
|---|---|---|
| `NaturalResourceLayer` (world) | `GetAmount`, `GetRichnessPercent`, `Remove`, `CellsWith`, `Version`, `StateHash` | info views (10, F), `PollutionLayer` consumers, hubs |
| `ExtractorAreaLayer` (world) | `GetHub(cell)`, `HubCells(id)`, `Version` | `ZoneGrowth` (4), info view |
| `ExtractorHub` (actor trait) | `Product`, `CapacityMilliPerDay`, `JobsMax`, `OnProduced(units)`, `MonthsLeft` | 05 daily step, hub info panel |
| `Logistics` (player) | `TryDispatch`, `HaulCost`, `TravelTicks`, `RegisterNode/UnregisterNode`, `Stats`, `FreightLoad(cell)` | 05 market, TrafficManager, panels |
| Orders | `CityExtractorArea`, `CityExtractorSetProduct`, `CityExtractorSetPolicy` | `ExtractorTool` (new player trait) |

**Needed from others**

| Domain | Needs from 6 / gives to 6 | Concrete asks |
|---|---|---|
| **5 Economy** | calls `TryDispatch` from the market; reads extractor capacity; owns money | add `CompanyKind.Storage`; call `OnProduced`; settle at dispatch and refund on failure (`Logistics` calls `Refund(sellerCo, buyerCo, cents)`); ledger lines `freight`, `loss`; trade stats feed the Production panel; instant-transfer fallback behind the same `TryDispatch` signature |
| **2 Traffic** | sampled trucks and freight flow | `TrafficManager.SpawnFreightTruck(fromRoad, toRoad, shipmentId, arriveTick)`, `IFreightFlow.AddFlow(path, equivalents)`, a shared router `TryRoute(fromRoad, toRoad, out cells, out ticks)`, per-road speeds; drop the random 55% truck rule; count freight trucks in `MaxVehicles` |
| **4 Zoning** | hosts of processors and warehouses | warehouse lot type, `SpaceMultiplier`, company-to-building binding carried over on level-up; `ZoneGrowth.CanGrowAt` rejects hub-area cells; **no `ZoneType` 13-16** (use `ExtractorAreaLayer`) |
| **10 Environment** | time and pollution | `CityClock.TicksPerDay`; `PollutionLayer.GetGround(cell)` and the nested `Pollution { Ground, Air, Noise, Scale }`; tree removal rule; groundwater read-only; its Natural-resources view reads my layer |
| **3 Networks** | outside links | `OutsideConnection.Kind/Capacity/trade prices`; rail layer for terminals (P5) |
| **8 Public transport** | cargo terminals | `ICargoTerminal` implementation registers a `Terminal` node |
| **1 Citizens** | staffing | workers fill extractor jobs by education; hub road cell is a commute endpoint |
| **Lead** | frozen files | extend `CityInfoView` (or add `IndustryInfoView`), `CityPlaceable` category `industry`, autotest scenario `industry`; `genmap.py` resource bytes (A1) |

---

## 5. Phased tasks and acceptance tests

Estimates are for one implementing agent.

**P0 - Resource data and view (2 days).** `genmap.py` writes ore, oil and fertile deposits into `map.bin` for the 3 playable maps (+ tinted preview); `NaturalResourceLayer` with fallback generator and `StateHash`; Natural-resources info view (4 colours); `industry` autotest scenario stub.
- Each playable map has >= 400 fertile, >= 60 ore, >= 25 oil cells; none of ore/oil inside the `guard` rectangle; the nearest ore patch is 15-60 cells from the highway anchor.
- Two headless runs of 2,000 ticks give the same `StateHash`; a replay of the first run also matches.
- `boot-test` (all-zero bytes) gets fallback deposits; `./utility.sh city --check-yaml` ends with `Errors: 0`.
- Screenshot of the view shows yellow, green, blue and black regions.

**P1 - Hubs, areas, extractors (4 days).** 5 hub actors with placeholder sprites, `ExtractorHub`, `ExtractorAreaLayer`, `ExtractorTool` + orders, area preview, `farm-hub` crop selector, forestry felling and replanting, extractors as 05 `Extractor` companies with raw output exported through highways instantly.
- Paint a full 5x5 on ore (avg richness 250): reported capacity = `25 * 1500 * rf / 1000` units/day within +/-1 and jobs = `ceil(cap / (3.1 * 1000))`.
- After 12 game months at 100% staffing `Ore` stock dropped by exactly the units produced (within rounding); taper below 25% remaining verified by a lowered-stock test map.
- Area cannot overlap another hub, roads, water, or leave the ring; the preview and handler agree (same function).
- Ground pollution 500 on a farm cell drops its fertility by >= 150 in 100 pulses and recovers after the source is bulldozed.
- Forestry at default policy keeps stand >= 70% over 10 game months; with `ClearCut` it falls below 30%.
- Bulldozing the hub frees its area; save/replay determinism passes.

**P2 - Chains and production panel (4 days).** Processors read recipes from 05, instant transfers priced with `HaulCost` Manhattan, Production panel and Production/Natural-resources views, 05 trade statistics.
- Scenario: farm (grain), farm (vegetables), industrial zone: a Beverages company appears within 6 game months and sells to commercial; commercial Beverages stock > 0.
- Conservation: over one month `produced - consumed - exported + imported = delta stock` per resource (exact in units).
- Disconnecting the highway stops imports within one day and the panel shows deficits.

**P3 - Shipments, fleets, sampled trucks, freight load (6 days).** `Logistics`, `RouteCache`, timing wheel, fleet slots, `SpawnFreightTruck` puppet trucks, `FreightLoad` in `GetTrafficLoad`, outside throughput, failure handling.
- A 40-cell shipment arrives `Depart + 40 * 14 = 560` ticks later (+/- 1), the truck is free again at `ReturnAt`.
- Halving a fleet cuts that company's output by >= 25% when the outbox is the bottleneck (throttle falls); adding a warehouse near it restores it.
- Cutting a road under in-flight loads: shipments return, money is refunded, the stranded counter rises, and nothing is lost (`loss` ledger = 0 when the seller exists).
- Visible trucks <= `MaxVisibleTrucks`, none removed by the stuck detector, and removing every truck actor does not change shipment arrivals.
- Shipment-array hash identical across a replay. Cost at 5,000 companies below 0.5 ms per tick (autotest `log` line).

**P4 - Warehouses, balancing, real-mode trucks (4 days).** `Storage` companies and `warehouse` building, stock balancing, real traffic mode via domain 2, depletion warnings.
- A city with a warehouse near the industry has lower average landed cost for its stored resource than without; a second warehouse near a distant cluster reduces mean shipment length.
- Warehouses never exceed capacity; balancing never moves more than 20% of stock per day.

**P5 - Terminals, completeness, polish (5 days).** `Terminal` nodes (rail, harbor, air), Fish and Coal/Steel/etc., fire hazard on oil, specialization bonus (+15% at the `05` threshold), tuning pass with a 20-year headless run.
- Rail import of Ore costs <= 40% of the same route by truck; harbor requires water; a 20-year `green-valley` run remains solvent with default taxes and at least one deposit shows depletion above 50%.

---

## 6. UI and art needs

**Art (procedural, `tools/genworld.py`, WP A2):**

| Asset | Spec |
|---|---|
| Hub sprites `farm-hub`, `quarry-hub`, `mine-hub` (3x3), `forestry-hub`, `oil-hub` (2x2) | 96x128 / 64x96, `Offset: 0,-16`; barn+silos, sawmill with log pile, pit crane, headframe, pumpjack shed |
| `oil-derrick` prop | 32x64, 6 animation frames, pumpjack |
| `warehouse` (P4), `cargo-rail`, `cargo-harbor` (P5) | 64x96 / 96x96, doors and containers |
| `extractor-area` tiles | 32x32, sequences `farm`, `quarry`, `mine`, `oil`, 4 variants each (crop rows, grey stepped pit, dark gravel with rails, black pad) |
| Truck variants (optional) | `truck-box`, `truck-log`, `truck-tank`, `truck-ore`, 32 facings, chosen by resource class |
| `statusicons` additions | no inputs, storage full, deposit depleted (16x16 each, three new frames) |
| `overlays` frames | `area-outline`, resource heat via existing `heat` |

**Chrome (WP A3/F):** `city-icons`: `industry` category icon, 5 hub build icons (48x48 `city-buildicons`), 25 resource icons (16x16 and 32x32, defined by domain 5), area-paint and area-clear buttons, cursor `paint-area`. New panels: `CITY_PRODUCTION_PANEL` (resource table, recipe diagram, chart), hub info (product selector, area cells, capacity, jobs, richness %, months left, trucks out, last shipments), freight legend. Fluent keys: `industry-*`, `notification-industry-depleted`, `notification-freight-blocked`, one name per resource.

---

## 7. Sources

- Paradox, Feature Highlight #9 Economy & Production: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/economy-production
- Colossal Order, Development Diary #9: https://colossalorder.fi/?p=1809
- Paradox, Dev Diary Economy 2.0 Part 1: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-one
- Paradox, Feature Highlight #3 Public & Cargo Transportation: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/public-cargo-transportation
- CS2 wiki: Economy https://cs2.paradoxwikis.com/Economy (Materials / Material goods / Immaterial goods tables, efficiency, specialization 115% at 10 kt, trade rates); Natural resources https://cs2.paradoxwikis.com/Natural_resources; Industry https://cs2.paradoxwikis.com/Industry; Pollution https://cs2.paradoxwikis.com/Pollution; Transportation (cargo) https://cs2.paradoxwikis.com/Transportation; Zoning https://cs2.paradoxwikis.com/Zoning; Supply Chains pack https://cs2.paradoxwikis.com/Supply_Chains
- Chill Place Gaming, supply chains and industry areas: https://chillplacegaming.com/supply-chain-cities-skylines-ii/ and https://chillplacegaming.com/industry-areas-cities-skylines-ii/
- Mods Cities 2: https://www.modscities2.com/cities-skylines-2-production , https://www.modscities2.com/cities-skylines-2-cargo-types/ , https://www.modscities2.com/cities-skylines-2-zone-demand
- Steam threads (community, unverified): fertility and oil depletion https://steamcommunity.com/app/949230/discussions/0/691994366768732533/ ; cargo terminals https://steamcommunity.com/app/949230/discussions/0/4031347072448117383/ ; production https://steamcommunity.com/app/949230/discussions/0/3877095833477723872/
- Infixo, Economy Rebalance / CS2-RealEco (consumption and extractor output remarks): https://github.com/Infixo/CS2-RealEco ; InfoLoom (industrial demand panel): https://github.com/Infixo/CS2-InfoLoom
- PCGamesN on CS2 economy: https://www.pcgamesn.com/cities-skylines-2/economy
- Fish resource and Bridges & Ports: https://www.paradoxinteractive.com/games/cities-skylines-ii/add-ons/cities-skylines-ii-bridges-and-ports
- Sister docs: `05-economy.md`, `04-zoning-buildings.md`, `10-environment-time-ui.md`, `02-traffic.md`, `03-networks.md`, `08-public-transport.md`; repo files `mods/city/CONTRACT.md`, `OpenRA.Mods.City/Traits/World/TrafficManager.cs`, `CityCoverageLayer.cs`, `ZoneGrowth.cs`, `mods/city/tools/genmap.py`.
