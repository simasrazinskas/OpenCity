# 04 - Zoning, Buildings and Land (OpenCity design)

Domain 4 of the CS2-parity research. Scope: zone types, lots, building spawn, levels, rent and land value, abandonment, signature buildings, and how buildings host households and companies for the citizen sim (domain 1) and the economy / industry sims (domains 5, 6).

Confidence tags used below: **[W]** = official wiki or Paradox dev diary / patch notes, **[C]** = community observation (Steam, forums), **[M]** = modder or my own recollection of the decompiled game (treat as uncertain), **[D]** = OpenCity design decision (ours, not CS2).

TL;DR for the lead (ADHD-friendly):
- Today a building is a 1x1 cell with a fixed stat block, grown on a random zoned cell, levelled by land value + happiness.
- CS2 buildings are **lots** (width x depth cells on a road frontage) that **host tenants** (households or companies). Tenants pay rent, rent minus upkeep fills a **condition** bar, the bar fills = level up, the bar empties = level down, then abandoned, then collapse.
- Proposal: introduce a `Lot` model (1x1 up to 4x3 cells), one actor type per lot archetype with `Level` (1-5) as synced state, a `Property` trait that exposes **household slots** and **company slots**, a `LandValueLayer` that propagates along roads, and a rent + condition loop. Phase it so each step keeps the game playable.

---

## 1. CS2 mechanics (what we are approximating)

### 1.1 Zone types [W]

Residential (6 types in the base game, plus waterfront in the Beach Properties DLC):

| CS2 zone | Typical building | Lot range (tiles, WxD) | Households |
|---|---|---|---|
| Low density | detached / semi-detached houses | 4-24 tiles, 2x2 .. 4x6 | always 1 per building |
| Medium row | narrow wall-to-wall row houses | 2-6 tiles, 1x2 .. 1x6 | 2-12 |
| Medium | small apartment blocks | 4-36 tiles, 2x2 .. 6x6 | 6-108 |
| Mixed | shops on the ground floor, flats above | 4-36 tiles | 8-144 |
| Low rent | large blocks with many small cheap flats (students, young adults) | 4-36 tiles | 16-288 |
| High density | large apartment towers | 4-36 tiles | 24-432 |

Commercial: low density (stores, boutiques, gas stations, supermarkets, restaurants, motels, bars; 4-30 tiles) and high density (supermarkets, malls, hotels, cinemas, concert halls; 4-36 tiles).
Office: low and high density (4-36 tiles). Offices make immaterial goods (software, telecom, finance, media).
Industrial: one **manufacturing** zone that also spawns **warehouses / storage yards** (4-36 tiles).
Specialised industry (not a painted zone): a central building with extractable "corners" bound to a natural resource. Nine types [W]: livestock, grain, vegetable and textile-fibre farming (fertile land, except livestock), forestry (forest), stone mining (none), coal and ore mining (ore), oil drilling (oil). They do **not** affect industrial zone demand, they never bankrupt (they downsize instead), and share a "specialisation bonus" when several companies make the same resource.
Themes: North American and European building sets; the player can paint both in one city. Signature buildings exist in both sets.
Zoning tools: Fill, Marquee, Paint; right mouse button de-zones. Some roads (highways) do not allow zoning.

### 1.2 Lots, depth and spawn rules [W unless tagged]

- One zone cell is **8 m x 8 m**. Lot size is written Width x Depth: width = cells along the road, depth = cells away from the road.
- Minimum lot **1x2** (8x16 m), maximum **6x6** (48x48 m).
- Zone depth from a road is **6 cells** (48 m) [C: efficient-grids guide, matches max lot depth 6].
- Spawn algorithm: "always tries to spawn the **largest possible lot**. It first selects the **deepest** option, then the **widest**" among assets that fit the free zoned cells.
- "As land value changes during gameplay, the zoning algorithm will adapt by subdividing larger plots into smaller lots to account for the higher rent associated with increased land value." So expensive land spawns smaller lots; this is the main CS2 lever against the high-rent problem on low density.
- Players report that corners and short frontage spawn lots of 2x2 / 2x3; roads built after the zone produce cleaner lots [C]. Demand for the very large residential lots fades; the game then spawns narrow 2x6 lots [C].
- Spawning is gated by **zone demand** (per zone type, see 1.7) and by services (road, power, water, sewage are needed to keep tenants, not to spawn) [C].
- Level 1 buildings spawn first. Levels 1-5 have 3 unique meshes (1, 3, 5); levels 2 and 4 are non-visual upgrades [W]. Modders are advised to supply a full level 1-5 set per lot size [W].

### 1.3 Building levels [W]

Levels 1-5 "reflect how wealthy its inhabitants are". Higher levels:
- raise **upkeep**, therefore raise **rent**;
- lower electricity, water and garbage per household / per unit of output;
- raise production efficiency (manufacturing, offices, commercial speed);
- lower pollution per output;
- increase household capacity: **+25% per level above 1**, so level 5 = 2.0x level 1 [W].

Household density per tile by level (capacity = density x tiles) [W, Residential wiki page]:

| Zone | L1 | L2 | L3 | L4 | L5 |
|---|---|---|---|---|---|
| Medium row | 1.00 | 1.25 | 1.50 | 1.75 | 2.00 |
| Medium | 1.50 | 1.88 | 2.25 | 2.63 | 3.00 |
| Mixed | 2.00 | 2.50 | 3.00 | 3.50 | 4.00 |
| Low rent | 4.00 | 5.00 | 6.00 | 7.00 | 8.00 |
| High | 6.00 | 7.50 | 9.00 | 10.50 | 12.00 |
| Low density | one household regardless of size and level | | | | |

Workplaces [C]: an office has roughly 60 workplaces in the smallest buildings up to ~225 at 6x6, usually 100-150. A level-5 low-density commercial building is ~25% poorly educated, 50% educated, 25% well educated jobs; software / telecom offices are ~25% well educated, 75% highly educated (4 education tiers). Industry education mix depends on the product's "complexity" [W: economy dev diary].

### 1.4 Rent, upkeep, condition: the level-up loop [W, patch 1.1.5f1 "Economy 2.0"]

- Pre-patch there was a "virtual landlord" who absorbed some of the cost. Removed in 1.1.5f1: **upkeep is paid equally by all renters** of the building.
- Official rent formula (patch notes and dev diary): `Rent = (LandValue + ZoneType * BuildingLevel) * LotSize * SpaceMultiplier`. The numeric ZoneType factors are **not published**. Only one SpaceMultiplier example is public: industrial manufacturing went from 1 to 5, "allowing more workers per grid, so fewer buildings are needed".
- Each building has a **Condition**. If tenants pay the full upkeep, condition rises by a constant per update; if they cannot, it falls by the same constant [W]. At upper thresholds the building levels up (and tenants start paying the next level's upkeep); at lower thresholds it levels down, then becomes **abandoned**, then **collapses** [W/C]. The numeric thresholds and leveling cost are internal [M: `BuildingUtils.GetLevelingCost`, `BuildingCondition` component, cost grows with lot size and roughly exponentially with level; the LandValueOverhaul mod author states upkeep and upgrade costs "scale exponentially" in vanilla].
- Community summary [C]: buildings only earn level progress while rent is being paid; service fees (garbage etc.) subtract from that progress; upkeep is due whether or not the building is full, so a new half-empty low-rent building often gets abandoned quickly.
- Companies work the same way, but out of **profit**: surplus after rent, wages, resources and taxes funds upkeep and leveling. Industry in a high-rent zone cannot level [C].
- **High rent warning** [W]: since 1.1.5f1 it is based on **household income**: a household that cannot cover rent from its current cash reduces consumption first and only complains when its income is below the rent.
- Low density suffers most because one household carries the whole rent; medium and high density spread it across many households [W]. Low-rent zone only helps students / poor households; it has its own demand bar and does not lower rents elsewhere [C].

### 1.5 Land value [W / M, uncertain]

- Land value is a per-cell field that "spreads through roads to the nearby vicinities of buildings" [W/C]. It reflects desirability: large homes, nearby shops, services, schools, workplaces, and pollution-free air [W].
- Since 1.1.0 it is reportedly driven by five coverage factors: **transport, healthcare, education, police, commercial** [M: LandValueOverhaul README]. Services no longer add land value directly; they satisfy tenants, who pay more rent, who level the building, which raises land value [W: services feature page].
- Negative: pollution (ground, air, noise), crime, abandonment, homelessness [C].
- Known vanilla quirks: road edges with no buildings get extreme values and spread widely; mods add faster distance decay and per-zone-type values [M: LandValueTuning].
- Clustering expensive services (e.g. coal plants plus depots) inside industrial land raised its value and rent [C].

### 1.6 Abandonment and condemnation [W/C]

Tenants cannot pay upkeep -> condition falls -> abandoned (tenants leave, homeless may squat) -> collapses if it stays abandoned; rubble remains until cleared. A building also becomes abandoned when a company goes bankrupt. CS2 has no "condemned" state as such; the closest is Abandoned plus the collapse. Abandonment bugs and over-aggressive abandonment were repeated patch topics, so make ours gentle and predictable [C].

### 1.7 Demand interplay [W/C]

- Residential has **three bars**: low, medium, high density (row, medium, mixed and low-rent sit under medium / high, with a separate low-rent meter). Each density has its own vacancy stat, so empty towers do not block new houses.
- Drivers: jobs vs workers and unemployment, vacancy, taxes, happiness, homelessness, education opportunity, household **wealth and size** (families and rich households want low / medium density, students and singles want cheap high-density flats).
- Commercial: goods available from industry plus customers nearby ("not enough customers" if shops are far from homes). Industrial: workers wanting jobs, goods demand, resources. Office: workers and demand for immaterial goods. Warehouses are demanded by storage need.
- Unoccupied buildings lower demand until filled.

### 1.8 Signature buildings [W]

Unique, free, ploppable "zoned" buildings; once per city; relocatable; act like normal zoned buildings plus a bonus (well-being radius, attractiveness, efficiency, graduation rate). Unlock conditions combine milestone, zone-cell counts **only for cells that hold buildings**, counts of level-5 buildings, happiness %, production totals, other services. Examples:

| Building (NA / EU) | Zone | Condition | Bonus |
|---|---|---|---|
| Rock Musician Mansion | low res | 2,500 low-density cells | +2 well-being, ~500 m |
| Painter Mansion | low res | 25,000 low cells + 60% happiness | +6 well-being |
| Polaris Suites | medium | 500 medium cells | +4 well-being |
| Baltar Pines | row | 1,000 row cells | +3 well-being |
| Watanabe Tower | high | 50 level-5 high buildings | +6 well-being |
| Colossal Tower | high | 200 level-5 high buildings | +12 well-being |
| Streamline Diner | low commercial | 4,000 low-business cells | +3 well-being, +5 attractiveness, -1% import costs |
| Capacitor Building | high commercial | 240 level-5 high-business buildings | +30 attractiveness |
| Fuel Plant | industrial | 50 level-5 industrial buildings | +2% efficiency, -5% pollution |
| Waterfall Array | office | 1 server farm + 6,000 high-office cells | +20 attractiveness |

### 1.9 Sources of uncertainty

CS2 does not publish: ZoneType rent factors, SpaceMultiplier per zone, condition constants and level thresholds, workplace counts per tile, exact land value kernel. Everything numeric in section 3 that is not in the tables above is **[D]**: tuned for OpenCity's much smaller scale and for a deterministic integer sim.

---

## 2. Current OpenCity state and gaps

### 2.1 What exists (read from the code)

| Area | Today | File |
|---|---|---|
| Zone types | 6: ResLow, ResHigh, ComLow, ComHigh, Industrial, Office. `ZoneType : byte`, 1..6, part of the order protocol. `ZoningTool` rejects `ExtraData > Office`. | `CityTypes.cs` (frozen), `Traits/Player/ZoningTool.cs` |
| Zone storage | `CellLayer<byte>` per cell, `CanZone` = in map, not road / water, within `ZoneDepth` (4, Chebyshev) of any road, no non-growable building. | `Traits/World/ZoneLayer.cs` |
| Spawn | Every 10 ticks per zone type with demand > 0: `ceil(demand/25)` buildings, capped by 8 under construction. Candidate = any single zoned empty cell with `HasRoadAccessWithin(cell, 4)`. Picked by `SharedRandom`. Always level 1, always 1x1. | `Traits/World/ZoneGrowth.cs` |
| Building | `GrowableBuilding` per-level actor (`res-low-1..3`); Level, UpgradesTo, ConstructionTicks; abandonment timers; collapse then regrow. | `Traits/Buildings/GrowableBuilding.cs` |
| Level-up | LandValue >= 40 (L2) / 65 (L3) and Happiness >= 55 held 20 days; actor replaced (residents carried over). | same |
| Capacity | Static per actor in yaml, e.g. res-low 4/6/9, res-high 16/28/44, com-low jobs 4/6/9, ind jobs 10/16/24. | `rules/growables.yaml` |
| People | Aggregate: residents per building, `Workers = 55%`, jobs filled proportionally. No individuals, no households. | `Simulation/CityManager.Daily.cs` |
| Land value | `CityCoverageLayer`: base 25 + water bonus (<=15) + parks*0.30 + (police+fire+health)*0.10 + education*0.12 - pollution*0.40, recomputed every 100 ticks. Read at the building's first cell. No road propagation, no zone-specific value, no accessibility. | `Traits/World/CityCoverageLayer.cs` |
| Tax | `persons * base * (100+25*(level-1))% * (70+0.6*LV)% * rate`. No rent, no upkeep per building. | `CityManager.Economy.cs` |
| Demand | 4 categories (R, C, I, O), not per density / zone type. Both Res types share one bar. | `CityManager.Economy.cs` |
| Abandon | 45 days without power / water / road or 60 days happiness < 20; recover if services return; collapse after 30 days. | `GrowableBuilding.cs` |
| Art | 32x64 frame, 4 variants, 3 levels x 6 zones, procedural (`tools/genworld.py`); 2x2 services use 64x96. `construction` small 32x64, large 64x96. Variant by hash of cell. | `bits/world/*`, `sequences/buildings.yaml` |
| Overlay | Zone overlay frame = `(int)ZoneType`, 7 frames. | `Traits/Growth/ZoneOverlay.cs`, art contract 6 |

### 2.2 Gaps vs CS2 (and vs the "simulate each human, cars, traffic, industry" goal)

1. **No lots.** Everything is 1x1, so no variety in density, no frontage logic, no big buildings, no lot subdivision as land value rises.
2. **Road access is a distance test** (`HasRoadAccessWithin(cell, 4)` accepts any connected road cell inside a 9x9 window, even behind other buildings). Deep interior cells are "connected" with no path. Cars (domain 2) need a real **access point** (one road cell, one side) per building.
3. **Zones are density-poor**: only low / high residential, one commercial pair, one industrial, one office. No row, medium, mixed, low-rent, warehouse, specialised industry, mixed-use.
4. **Capacity is static per actor** and not tied to lot size, so a future 2x3 building cannot scale by area.
5. **No tenants.** Residents are a counter. Domain 1 needs household slots; domain 5 needs company slots.
6. **No rent / upkeep / condition.** Level-up uses land value and happiness thresholds, not money, so there is no "high rent" problem, no profit-driven leveling, no wealth sorting of density.
7. **Land value is a flat service-coverage sum.** No road spreading, no per-zone value, no accessibility (transit, shops, jobs), no crime / abandonment effects, and it feeds nothing except happiness and level thresholds.
8. **Levels are three actors per zone.** CS2 has five levels; actor-per-level multiplies actor definitions (5 levels x zones x footprints) and complicates tenant carry-over.
9. **Demand is per category**, not per density; empty towers suppress the single residential bar.
10. **No signature buildings, no themes.**
11. **Zone paint is cell-wise and unconditional**: lots are not previewed, there is no "can't fit" feedback.
12. **Abandonment is service based.** It should come from unpaid upkeep (the CS2 loop), keeping the service rule as a secondary trigger.
13. **ZoneType enum is frozen in `CityTypes.cs`** and used in the order payload (byte). Adding zone types needs the lead to edit the frozen file (append-only: keep values 1-6).

---

## 3. Proposed design for OpenCity

Design rules: grid cells (32 px), deterministic integer sim, all state changes through orders, no floats in synced state, iteration in ActorID / row-major order, `world.SharedRandom` only inside synced code.

### 3.0 Scale decision [D]

1 OpenCity cell = **2 x 2 CS2 cells (16 m)**. A road is 1 cell wide. That gives:
- CS2 min lot 1x2 (8x16 m) -> OpenCity 1x1; CS2 2x2 -> 1x1; CS2 4x4 -> 2x2; CS2 6x6 (max) -> 3x3.
- CS2 zone depth 6 -> OpenCity depth **3** (lots never deeper than 3 cells). The existing `ZoneDepth: 4` stays as the **paintable band** (so the player can zone a 4th row for parking / future widening), but lots use at most 3 rows.
- OpenCity max lot: 4 wide x 3 deep (12 cells) for industry / warehouses and malls, 3x3 for the rest. Anything bigger is a service or a signature building.

### 3.1 Zone type list

Keep enum values 1-6 as they are (order protocol, saves, overlay frames). Append new values. Category (demand / tax) stays 4 groups, but **demand becomes per zone group** (3.9).

| Value | ZoneType | Category | CS2 analogue | Households / jobs | Phase |
|---|---|---|---|---|---|
| 1 | ResidentialLow | R | Low density | 1 household per building | MVP |
| 2 | ResidentialHigh | R | High density | many | MVP |
| 3 | CommercialLow | C | Low business | jobs | MVP |
| 4 | CommercialHigh | C | High business | jobs | MVP |
| 5 | Industrial | I | Manufacturing (+ warehouses) | jobs, goods | MVP |
| 6 | Office | O | Offices (low and high merged) | jobs | MVP |
| 7 | ResidentialRow | R | Medium row | few households, narrow lots | P2 |
| 8 | ResidentialMedium | R | Medium density | mid households | P2 |
| 9 | ResidentialMixed | R + C | Mixed housing | flats above, shop jobs below | P3 |
| 10 | ResidentialLowRent | R | Low rent | many tiny households (students) | P3 |
| 11 | OfficeLow / OfficeHigh | O | Low / high office | split Office into two | P3 |
| 12 | Warehouse | I | Warehouse / storage | few jobs, big goods storage | P3 (domain 6) |
| 13 | AgricultureArea, 14 ForestryArea, 15 OreArea, 16 OilArea | I (extractors) | Specialised industry | few jobs, raw material output | P4 (domain 6) |

Notes:
- ResLow = single-family. ResRow = 2 narrow households per cell of frontage. ResMedium = 3-6 storey flats. ResHigh = towers. Low rent = tall cheap block, tied to Education buildings in demand.
- Extractor areas are **not painted per cell like normal zones**: the player paints a resource-tied area (`ZoneType` 13-16) on cells that contain the resource (a new `ResourceLayer`, domain 6) and the lot generator places one **hub** building plus extractor props. They do not use the road-frontage rule beyond one access point.
- Themes (EU / NA): a catalog tag `Theme: EU|NA|Any`. MVP ships one theme. The ZoneOverlay frame count grows with the enum (generate 17 frames, or switch to a colour-by-lookup table).
- Dezone stays `ZoneType.None`.

### 3.2 Lot model on the grid

**Lot** = axis-aligned rectangle of zoned, empty cells, `W x D`, where the side of length W lies **along a road** (frontage) and D extends away from it.

Data:
```
struct LotShape { byte W, D; }          // canonical, W along the road
struct Lot      { CPos Origin; byte W, D; byte Side /*N,E,S,W of the road*/; CPos Access; CPos RoadCell; }
```
- **Frontage detection** (`LotFinder`, new, in `Traits/Growth/`): for every zoned cell that is 4-adjacent to a connected road cell, the road side facing the cell is its *frontage side*. A **frontage run** is a maximal run of such cells along one road, all with the same zone type and no building in them.
- **Depth** of a column of the run = number of consecutive same-zone empty cells going away from the road, up to 3 (max lot depth). Cells beyond 3, or with a different zone, are ignored.
- **Candidate lots** at a run: scan the run left to right; for each start cell take the **largest shape from the catalog that fits**, ordered by the CS2 rule *deepest first, then widest* (`D desc, W desc`). The catalog (zone x shape) decides which shapes exist (3.4). A shape fits if all W x D cells are same zone, empty, in map, not water, and (for depth > 1) not already claimed by a lot from the other side of the block (blocks narrower than 2D just yield smaller D).
- **Land value subdivision** [W: CS2 behaviour]: the maximum allowed shape depends on the lot's land value. `maxArea = clamp(5 - LV/20, 1, 4)` cells for ResLow only (LV 0-100 with integer division: LV < 40 allows 2x2, LV 40-79 up to 2x1, LV >= 80 1x1 only): expensive land spawns smaller houses, so rent per household stays bounded. Other zones use their max area unconditionally.
- **Access point**: the road cell at the middle of the frontage (round down) is the lot's `AccessRoad`; the lot cell next to it is `AccessCell`. Domain 2 (cars) and 3 (networks) use these two cells: vehicles enter / leave the lot there, pedestrians appear there. This replaces `HasRoadAccessWithin` for lots (road access = `AccessRoad` is connected to the outside).
- **Corner lots**: a lot touching two roads uses the longer frontage; its second road gives a land value bonus (+5).
- Because `Building.Footprint` is fixed per actor and the art is south-facing, a lot with a **transposed** footprint (D x W on screen) needs either its own actor (`res-row-1x2` vs `res-row-2x1`) or a rotated sprite. Rule: **square shapes only in the MVP** (1x1, 2x2, 3x3); rectangular shapes (2x1, 1x2, 3x2, 2x3, 4x3, 3x4) come with P2 and are defined per orientation in the catalog (both are separate actors, both generated by the same Python function with swapped W / D).

**Reservation**: the lot picked at spawn time is stored as a `LotReservation` (cells flagged "claimed") until the construction actor appears (frame end task), so two lots in the same tick cannot overlap. Claims are not synced state of their own: they are recomputed from actors on load.

### 3.3 Spawn rules (replaces `ZoneGrowth.RebuildCandidates`)

1. Every `Interval` (10 ticks) and for each **zone group** (3.9) with demand > 0: `wanted = ceil(demand / DemandPerSpawn)`, cap `MaxUnderConstruction` per group (raise from 8 to 12 once lots are bigger).
2. Candidate pool = **frontage runs**, not cells. Rebuilt on zone / road / actor change (as today: `dirty`), row-major, deterministic.
3. Pick a run by `SharedRandom`, weighted by `LandValue` of its access cell for high-density zones and uniform for low (so towers go where land is valuable).
4. Within the run choose the start cell at the end of the run nearest to an existing building of the same zone (compact growth), tie broken by position, then take the largest fitting shape from the catalog (3.2).
5. Level 1 starts; **spawn level** can be higher when the lot has high land value and the player has unlocked it: `startLevel = 1 + (LV >= 70 ? 1 : 0)` for ResHigh / Office / ComHigh only (CS2 spawns level-1 buildings but converts quickly).
6. `ConstructionTicks` scales with area: `base 150 + 40 * (cells-1)`, variance +/-25% as today.
7. Zones with no frontage run (interior cells deeper than 3, single-cell gaps no shape fits) stay as painted but empty; the zone overlay marks them **dim** (hatched) so the player sees what will not grow. CS2 does the same by not rendering cells that cannot take a lot.

### 3.4 Building catalog (data-driven)

One **archetype actor per lot shape and zone**, e.g. `res-low-1x1`, `res-low-2x2`, `res-high-2x2`, `com-low-2x1`, `ind-3x3`. **Level (1-5) is synced state on the actor**, not a separate actor (changes vs today's `res-low-1/2/3`). Art picks the sprite **tier** `(level+1)/2` -> L1-2 tier 1, L3-4 tier 2, L5 tier 3 (CS2 has meshes for 1, 3, 5 only [W]). Levels 2 and 4 get a small overlay (lit windows / awning) so the upgrade is visible.

Capacity is **computed**, not listed per actor. Per cell densities (households or jobs per cell at level 1; multiplier `100 + 25*(level-1)` percent [W]):

| Zone | Shapes (WxD) | HH / cell | Jobs / cell | Upkeep $/month/cell L1 | Notes |
|---|---|---|---|---|---|
| ResLow | 1x1, 2x1, 2x2 | one household per building | 0 | 4 | home-business job slot 1 at L3+ |
| ResRow | 1x2, 2x1, 1x3 | 1.0 | 0 | 5 | wall-to-wall, no side gap |
| ResMedium | 2x2, 2x3, 3x2, 3x3 | 1.5 | 0 | 6 | |
| ResHigh | 2x2, 3x2, 3x3 | 6.0 | 0 | 9 | tiers: block, slab, tower |
| ResLowRent | 2x2, 3x2 | 4.0 | 0 | 5 | tiny households |
| ComLow | 1x1, 2x1, 2x2 | 0 | 3 | 5 | 1-2 company slots |
| ComHigh | 2x2, 3x2, 3x3, 4x3 | 0 | 6 | 8 | mall = 4x3 |
| Office | 2x2, 2x3, 3x3 | 0 | 8 | 8 | |
| Industrial | 2x2, 3x2, 3x3, 4x3 | 0 | 4 | 4 | space multiplier x2 at L3+ |
| Warehouse | 3x2, 3x3, 4x3 | 0 | 1 | 3 | storage capacity per cell |
| Mixed | 2x2, 3x2 | 1.5 (upper) | 2 (ground) | 7 | |

Examples: ResHigh 2x2 L1 = 24 households, L5 = 48. ResMedium 3x3 L3 = 9 x 1.5 x 1.5 = 20 households. ComHigh 4x3 L5 = 12 x 6 x 2.0 = 144 jobs. ResLow is always 1 household (CS2 behaviour); its land value rent pressure is what lets it drive CS2's "high rent" story. The pop target of the existing balance (res-low 4-9 residents) becomes household size 2-4 from the citizen sim (domain 1). All values **[D]**, tuned so a 1x1 ResLow L1 roughly equals today's `res-low-1`.

`Property` per building (new trait, replaces `CityBuildingInfo.MaxResidents / MaxJobs` for growables, which stay as fallback for services):
```
int HouseholdSlots, CompanySlots, WorkplaceSlots[4 education tiers], StorageCapacity;
int SpaceMultiplier;   // industrial/warehouse: more workers per cell
```
Utility use also scales with area and **falls with level** (-8% water / power per level above 1 [W direction, number D]).

### 3.5 Levels, condition and level-up (CS2 loop)

State on `GrowableBuilding`: `Level` 1-5, `Condition` (int, -1000..+1000, synced), `Abandoned`, `Construction` as today.

Monthly (every `DaysPerMonth` at the building's staggered day, plus a light daily tick for status):
```
rentDue      = Rent(lot)                          // 3.6
upkeep       = Upkeep(lot, level)                 // catalog * cells * (100 + 30*(level-1))% (linear, not exponential, see LandValueOverhaul note [M])
paid         = sum over tenants of min(tenantShare, tenantCanAfford)
tenantShare  = upkeep / numTenants                // upkeep split equally [W]
condition   += ConditionStep * (fullyPaid ? +1 : -1)       // constant step, same size up and down [W]
// partial payment: +1 only if paid >= upkeep; else -1
if condition >= LevelUpAt(level)   -> level++, condition = 0, BeginUpgrade()   // keeps tenants, plays small construction
if condition <= -LevelDownAt       -> level-- , condition = 0 (level 1: -> Abandoned)
```
- `ConditionStep = 100`, `LevelUpAt = 600 + 200 * level` (so 7 paid months for L1->2, 13 for L4->5), `LevelDownAt = 400`. **[D]**; 1 game month = 30 days = 750 ticks, so L1->L5 takes about 3-4 in-game years if everything is paid. Expose all as `GrowableBuildingInfo` fields.
- **Gates** (a building at risk can never level up regardless of money): needs power, water, road access; no tenant has an unserved basic need flag; `Happiness >= 40`; plus a **zone-specific level cap** by city state: L3 needs the milestone "Small Town", L4 "Town", L5 "Small City" (like CS2 development tied to progress) [D].
- **Residential**: tenants pay rent from household income (domain 1). **Commercial / office / industrial**: companies pay rent + upkeep from **profit** (domain 5): `profit = revenue - wages - input costs - rent - upkeep - tax` per month; if the company cannot pay, condition falls and the company counts a loss month; 3 loss months = **bankrupt** -> slot freed (domain 5).
- Fast fallback for the MVP (before rents exist): `condition += (happinessScore + landValueScore - 60) / 10` per month, so Phase 1 reproduces the current LV / happiness thresholds with the new 5-level model.

### 3.6 Rent and land value

**Land value (LV)**: new `LandValueLayer` (world trait, replaces the LV half of `CityCoverageLayer`; coverage and pollution maps stay). Integer cell map 0..255 (scaled x2.55 for the 0..100 API). Recomputed in **three passes** every 100 ticks, spread over 4 ticks:
1. **Local score per cell** (all 0..100 ints): `base 20 + water 15 (proximity) + park coverage * 0.25 + police * 0.08 + fire * 0.06 + health * 0.12 + education * 0.12 + transport(access to bus / station, domain 3) * 0.15 + commercial coverage (shops within 6 cells, from `ShopAccess`, domain 5) * 0.10 + jobs access (workplaces within 10 road-steps) * 0.10 - pollution * 0.45 - noise * 0.2 - crime * 0.2 - abandonedNeighbours * 6 each (max 30) - traffic load * 0.1`.
2. **Road propagation**: value flows along the road graph, not through buildings. For each road cell, `roadLV = max(local scores of adjacent lot cells, neighbour road LV - 3)` (Dijkstra-like bounded 12 steps, integer). Lot cells take `max(local, roadLV at the lot's access road - distance*5)`. This is CS2's "spreads through roads" [W], but with a **fade per step** to avoid the road-end explosion bug [M].
3. **Smoothing** with a 3x3 box (cheap) so there are no cliffs.
Per-zone modifiers applied when a lot **reads** LV: Industrial LV is capped at 60 (industry prefers low value [W]); ComHigh adds +10 for commercial coverage bonus; ResLow gets +10 from parks.

**Rent** (integer dollars per month for the whole building):
```
rent = ((LV + zoneFactor * level) * cells * spaceMultiplier) / 10
zoneFactor: ResLow 6, ResRow 4, ResMedium 4, ResHigh 3, ResLowRent 2, ComLow 5, ComHigh 6, Office 8, Industrial 3, Warehouse 2, Mixed 5      // [D], analogue of CS2's ZoneType factor
```
Per household rent = `rent / households`, per company = `rent / companies`. Tenants pay **rent + (upkeep share)**; the sum above `upkeep` is the **landlord surplus**: it is **not** city income (CS2 has no landlord; tenants spend it into condition). Taxes stay separate. Example: ResLow 1x1, LV 50, L1: `(50 + 6) * 1 * 1 / 10` -> 5.6 dollars per month per cell, i.e. $6; ResHigh 2x2 L1 LV 50: `(50+3)*4/10` = $21 across 24 households = ~$1 each. That asymmetry (single-household low density pays the whole land value rent, towers split it) is exactly CS2's finding [W]; it is what pushes wealthy households to low density, the poor to towers and low rent.

### 3.7 High rent, abandonment, condemnation

- **Rent scale**: a constant `RentScale` (default 10 above) is tuned so median rent is **25-30% of median household income** (domain 1 owns incomes). If the median ratio drifts, change only `RentScale`.
- **High rent flag** (per household, domain 1 evaluates, we publish `RentPerHousehold`): shown only if `income < rent` (CS2 1.1.5 rule [W]); otherwise the household cuts consumption first. A building where > 30% of tenants are flagged for 2 months gets a "High rent" status icon and an **UnaffordableRent** counter that lowers its **condition step** (no level-up) and later triggers tenant turnover (poorer replacement or lower-level slot).
- **Levers the player has** (same as CS2 [C]): lower taxes, add / remove services (changes LV), re-zone to a denser or cheaper type, raise education so incomes rise. We add one that CS2 lacks: a per-category **rent cap policy** is out of scope.
- **Abandonment** (any one): (a) condition <= -LevelDownAt at level 1, (b) 45 days without power / water / road (as today), (c) company bankrupt and no replacement in 90 days, (d) 60 days happiness < 20 (as today). On abandon: tenants evicted (they become homeless / looking for housing, domain 1), status icon, `Abandoned = true`, building stays in the sim for its **collapse timer** (30 days; shortened to 10 when adjacent to a fire).
- **Recovery** (existing behaviour kept): if the cause clears and happiness >= 30 the building re-opens at level 1 with condition 0.
- **Condemned** = abandoned for the full collapse time: the actor is replaced by a **rubble** actor (2 months, blocks the lot, pollution-neutral, +crime) that the player can bulldoze at once (free) or leave; after the timer it vanishes and the zone regrows. A **Condemn** order (`CityCondemn`) lets the player demolish an *abandoned* building immediately. Prevents the CS2 complaint "rubble stays forever" [C].
- **Land value feedback**: each abandoned building lowers LV of cells within 4 (-6 each, cap -30) [C: urban blight].

### 3.8 Signature buildings

Catalog entry `Signature: true` with `UnlockRule`s evaluated monthly by a new `SignatureUnlocks` player trait (counts only **occupied** zone cells [W]):
```
Rule kinds: Milestone(n) | ZoneCells(zone, n) | Level5Count(zone, n) | Happiness(pct) | Population(n) | ServiceBuilt(type)
```
Ploppable through `CityPlaceable` (cost 0, once per city, relocatable, `Category: signature`, `Footprint` 3x3 or 4x4), act as normal zoned buildings of their zone with a bonus: `+WellBeing (happiness) radius R`, `+Attractiveness` (tourists / demand), `+Efficiency%` for industry, `-pollution`. Starter set (7), scaled to our smaller cities [D]:

| Signature | Zone | Unlock | Bonus |
|---|---|---|---|
| Mayor's Villa | ResLow | 60 ResLow cells + happiness 60% | +4 happiness r=12 |
| Garden Row | ResRow | 40 ResRow cells | +3 happiness r=10 |
| Sky Tower | ResHigh | 6 level-5 ResHigh | +6 happiness r=14 |
| Grand Bazaar | ComLow | 80 ComLow cells | +10 attractiveness, -1% imports |
| City Mall | ComHigh | 8 level-5 ComHigh | +20 attractiveness |
| Glass Hub | Office | 6 level-5 Office + 1 school | +2% efficiency, +1% graduation |
| Fuel Plant | Industrial | 6 level-5 Industrial | +2% efficiency, -5% pollution |

EU / NA names come from the theme tag. Zone-cell counts, not building counts, as in CS2.

### 3.9 Zone demand interplay

Move from 4 category bars to **zone groups**: R-low (ResLow), R-medium (Row + Medium + Mixed residential half), R-high (ResHigh + LowRent), C (Low+High), I (Industrial), W (Warehouse), O (Office). UI shows the existing 4 bars plus a **density split tooltip**. Group drivers (all `-100..100`, eased as now):
- **R groups**: jobs vs workforce, unemployment, **vacancy per group** (towers vacant do not block houses [W]), tax, happiness, and a **wealth/size mix** from domain 1: share of households with income above the ResLow rent -> R-low; singles / students -> R-high / LowRent (education capacity adds "+students" to LowRent).
- **C**: goods stock from I plus population purchasing power (domain 5), minus "no customers" from far shops. **I**: workforce wanting jobs + goods demand + import need. **W**: storage utilisation above 80%. **O**: educated workforce, immaterial goods demand.
- Extractor areas do not touch I demand [W].
- Spawn pressure per group uses its demand; **vacancy** is computed from household / company **slots**, not residents.

### 3.10 Hosting households and companies

Each growable exposes arrays (synced, stable indexes, filled in ActorID / slot order):
```
HouseholdSlot[i] { uint HouseholdId; int Rent; }      // domain 1 allocates / frees ids
CompanySlot[j]   { uint CompanyId;  int Rent; }       // domain 5
WorkplaceSlot[e] { int Capacity; int Filled; }        // per education tier 0..3, computed from jobs * mix
```
`PropertyRegistry` (world trait, new) maps `BuildingActorId -> Property`, publishes `FreeHouseholdSlots(zone, level, maxRent)` and `FreeCompanySlots(zone, ...)` sorted by (rent, ActorID), so a moving household / new company picks a property **by id order, not by random list**. A building that levels up keeps its slots (new slots appear, empty). A collapse evicts tenants (events: `TenantsEvicted(buildingId)`).
Education mix of jobs (workplace tiers 0..3): ComLow 25 / 50 / 25 / 0, ComHigh 15 / 40 / 35 / 10, Office 0 / 10 / 40 / 50 (levels shift 10 points up per level above 1; **[D]**, from CS2 forum observations [C]).

---

## 4. Interfaces

| With | We provide | We need |
|---|---|---|
| **Citizens (1)** | `PropertyRegistry.FreeHouseholdSlots(...)`, per-slot `Rent`, `AccessCell` (where a citizen spawns / leaves), `Level`, `LandValue`, events `BuildingCompleted`, `BuildingAbandoned`, `TenantsEvicted`. | Household income, wealth, size (density preference); `HouseholdMovedIn/Out`; per-household "can pay rent" flag. |
| **Economy (5)** | `FreeCompanySlots`, `WorkplaceSlot[]` by education tier, `StorageCapacity`, per-company `Rent`, `Upkeep`. | Company monthly profit, bankrupt / expanded events, tax per building. |
| **Industry (6)** | Warehouse + extractor lot types, `SpaceMultiplier`, hub access points, ResourceLayer hook for areas. | Raw material output rates, pollution per building. |
| **Networks (3)** | Utility draw per building (area, level, occupancy). | `HasPower / HasWater / HasSewage` (as `CityBuilding` bools), coverage layers. |
| **Traffic (2)** | `AccessRoad`, `AccessCell`, lot cells, building type (trip generation weight by zone and level). | Per-road-cell load for LV. |
| **UI** | Zone palette (+7 types), lot preview (ghost footprints), selection panel (level, condition, rent, tenants, LV), Land Value and Building Level info views, signature list. | Orders: `CityZone` (extended enum), `CityCondemn`, `CityPlaceBuilding` (signature). |

Compatibility with existing code: keep `CityBuilding` fields (`Residents`, `Workers`, `Happiness`, `LandValue`, `HasPower` ...). While the citizen sim is not there, `Residents = households * avgHouseholdSize` and `Workers` are filled by the aggregate as today. `CityCoverageLayer.GetLandValue(cell)` keeps its signature and delegates to `LandValueLayer`.

## 5. Phased tasks and acceptance tests

Tests use `mods/city/tools/autotest.sh <map> "<spec>"` with a new `scenario=lots` (builds a street grid with every zone) and extra `LogStats` fields (lot sizes, level histogram, average rent, abandoned count). Every phase must end with `./utility.sh city --check-yaml` = `Errors: 0`, the Debug build style-clean, and a replay determinism run (`autotest.sh replay:<file>` logs equal to the original).

**P1 - Lots + multi-cell buildings (MVP, 1-2 weeks)**
- Add `LotFinder` and frontage-run spawning; catalog yaml for the 6 existing zones with shapes 1x1 and 2x2; `GrowableBuilding` with `Level 1-5` as state; `CityBuilding` capacity computed from cells x density x level; keep the old condition rule (LV + happiness) mapped to 5 levels. ZoneOverlay marks cells that cannot take a lot.
- Art: 2x2 sprites for the 6 zones (3 tiers); reuse 1x1 art.
- Tests: (a) painting a 6x3 block next to a road yields buildings with depth 3 and no overlap (log: `lots=...`); (b) two parallel roads 5 cells apart produce lots with depth 2 on each side, no building straddles the middle; (c) `HasRoadAccess` of interior lots uses `AccessRoad`; (d) 2000-tick run: same city stats as before within +/-15% pop (no regression), 0 exceptions, replay in sync.

**P2 - Land value + rent + condition (money loop)**
- `LandValueLayer` with road propagation, `PropertyRegistry`, rent, upkeep split, condition and level-up/down; `ResRow` and `ResMedium` zones; rectangular shapes; economy hooks (tax unchanged).
- Tests: (a) a park next to a ResLow block raises LV of that block by >= 8 within 200 ticks and an industrial plant next to it lowers it; (b) a level-1 building with services reaches L2 within 8 months and L5 never without the milestone gate; (c) power cut at 45 days abandons it, condemn order removes it; (d) ResLow rent per household is > 5x ResHigh per household at the same LV (the CS2 asymmetry).

**P3 - Tenants, mixed, low rent, signatures (needs domain 1 and 5 slot APIs)**
- Household / company slots live; demand per zone group; Mixed, LowRent, Warehouse, Office split; `SignatureUnlocks` and 7 signature buildings; high-rent flag; rubble actor.
- Tests: (a) `sum(householdSlots filled) == population` every 500 ticks; (b) leveling a building keeps tenants; collapse evicts them and the households find another building within 60 days; (c) a city with towers empty keeps R-low demand > 0; (d) a signature unlocks when its rule is met and not before (scripted counts).

**P4 - Extractors, themes, polish**
- Extractor areas with `ResourceLayer`, EU / NA themes, building variety (3 variants per tier), ResLow subdivision by LV, balance pass.
- Tests: extractor spawns hub + produces; a 20-year run stays solvent with default taxes in `green-valley`; screenshot review of each tier and shape.

Risks: (1) frozen `CityTypes.cs` (ZoneType) needs a lead edit in P2 (append-only values); (2) lot spawning performance - candidate pools are per run, not per cell; (3) lots that rotate (transposed art) need double sprites.

## 6. UI and art needs

**UI**
- Zoning panel grows from 6 to ~12 buttons (grouped: Residential x5, Commercial x2, Office, Industry x2, Dezone); a **density tooltip** (households per lot, jobs, rent trend).
- Lot preview: while painting, draw ghost rectangles of the lots that would fit; unfittable cells hatched.
- Selection panel: level + condition bar (upward / downward arrow), rent per tenant, tenants list link, land value, upkeep, lot size, signature bonus.
- Info views: Land Value (needs the new layer), Building Level, Rent / Affordability (green = affordable, red = high rent).
- Status icons (add): high rent (coin), rubble / condemned, level down. Existing 6 icons stay.
- Demand panel: R shown as 3 sub-bars, others as today.
- New zone overlay colours (frame per zone): ResRow `#A8E063`, ResMedium `#4FBF4F`, LowRent `#2F6F3F`, Mixed `#8FD3C8`, Warehouse `#C9A227`, extractors brown / green / grey / black.

**Art (procedural, `tools/genworld.py`, 32-bit PNG)**
- Frame size = `(32*W) x (32*D + 32*Hrows)`, bottom `W x D` cells are the footprint, `Offset: 0,-16`; `Hrows` = 1 (low) or 2 (towers L4-5, malls, offices).

| Shape | Frame px (low / tall) | Variants | Tiers |
|---|---|---|---|
| 1x1 | 32x64 / 32x96 | 4 | 3 |
| 2x1 and 1x2 | 64x64 / 32x96 + 32x128 | 3 | 3 |
| 2x2 | 64x96 / 64x128 | 4 | 3 |
| 3x2, 2x3 | 96x96, 64x128 / +32 | 3 | 3 |
| 3x3 | 96x128 / 96x160 | 3 | 3 |
| 4x3, 3x4 | 128x128, 96x160 / +32 | 2 | 3 |

- Estimated count: ~14 zone-shapes x 3 tiers x 3.5 variants = ~150 frames; generator functions take `(w, d, tier, variant)` and compose from existing helpers (`box3d`, `windows`, `facade`, `lot`, `parking`).
- Construction art: `construction` small (1x1), large (2x2 exists), new `construction-3x3`, `construction-4x3`.
- Level 2 / 4 micro-variants: lit windows, awnings, solar panels, rooftop AC. Rubble sprite per shape (1x1, 2x2, 3x3). Signature sprites (7, 3x3 / 4x4, hand-composed).
- Abandoned look reuses `WithGrowableSprite` darkening (already exists).

## 7. Sources

- CS2 Zoning wiki: https://cs2.paradoxwikis.com/Zoning
- CS2 Residential wiki (household density table): https://cs2.paradoxwikis.com/Residential
- CS2 Asset Pipeline: Buildings (lot sizes, spawn algorithm): https://cs2.paradoxwikis.com/index.php?title=Asset_Pipeline%3A_Buildings
- CS2 Industry wiki (specialised industry): https://cs2.paradoxwikis.com/Industry
- CS2 Signature buildings wiki: https://cs2.paradoxwikis.com/Signature_buildings
- Paradox feature highlight #4 Zones and Signature Buildings: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/zones-signature-buildings
- Dev Diary: Economy 2.0 part two (rent formula, condition, upkeep): https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-two
- Feature highlight #9 Economy and Production: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/economy-production
- Feature highlight #5 City Services (land value effect): https://www.paradoxinteractive.com/games/cities-skylines-ii/features/city-services-districts-policies
- Patch 1.1.5f1 notes: https://forum.paradoxplaza.com/forum/threads/patch-notes-1-1-5f1.1687527/ and GamesRadar summary: https://www.gamesradar.com/games/city-builder/cities-skylines-2-finally-unleashes-its-huge-economy-20-patch-with-reworked-rent-and-a-fix-for-death-waves-but-itll-also-kill-a-bunch-of-your-citizens/
- Game Rant signature building tables: https://gamerant.com/cities-skylines-2-how-to-unlock-and-place-signature-buildings/
- LandValueOverhaul (modder analysis): https://github.com/Jimmyokok/LandValueOverhaul ; LandValueTuning: https://github.com/Noel-leoN/LandValueTuning
- Land value and building levels (community): https://www.modscities2.com/cities-skylines-2-land-value-and-building-levels/
- Steam discussions on abandonment, level-up and high rent: https://steamcommunity.com/app/949230/discussions/0/628941283089889546/ , https://steamcommunity.com/app/949230/discussions/0/3877095833481124129/
- Workplace and education distribution threads: https://forum.paradoxplaza.com/forum/threads/rebalance-the-quantity-of-workplaces-in-different-sectors-commercial-office-city-service.1621706/ , https://forum.paradoxplaza.com/forum/threads/education-distribution-and-job-distribution.1606699/
- OpenCity files read: `mods/city/CONTRACT.md`, `OpenRA.Mods.City/Traits/World/{ZoneLayer,ZoneGrowth,CityCoverageLayer}.cs`, `Traits/Buildings/{GrowableBuilding,CityBuilding}.cs`, `Traits/Simulation/CityManager.{Daily,Economy,Stats}.cs`, `rules/growables.yaml`, `tools/genworld.py`.
