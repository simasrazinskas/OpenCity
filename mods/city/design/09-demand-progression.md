# 09 - Demand, progression, policies, districts and tourism

Research + design for OpenCity (OpenRA mod, 2D, 32px grid, deterministic integer sim, maps about 130x130).
Domain 9 of 10. Interfaces with: citizens (1), economy (5), zoning (4), services (7), UI (10).
Confidence tags: **[H]** official/wiki-confirmed, **[M]** several community sources agree, **[L]** single source or memory of game internals, **[?]** unknown - we invent our own value.

---------------------------------------------------------------------------------------------------

## 1. CS2 mechanics (what we are approaching)

### 1.1 Demand: the big idea

CS2 has no single "R/C/I" scalar fed by population ratios like CS1. Demand is **emergent from agents**:
households, companies and buildings each publish a need, and the demand systems aggregate it into
bars. The player sees a small number of **named demand factors** (a tooltip list on each bar, with
a plus or minus sign and a bar contribution), which is the thing we most want to copy.

Three layers exist **[L]**:

1. **Household demand** (residential only): do we want more families? Driven by happiness,
   homelessness, taxes, jobs, students. Official Economy 2.0 patch text: "Household spawns are
   based on average citizen happiness, homelessness, tax, available student positions, and
   available jobs." **[H]**
2. **Building demand per density** (low / medium / high): is there room for another building of
   this kind? Driven by free (unoccupied) properties. Shown as the R bar (CS2 shows one bar per
   zone group; the three densities share household demand but have separate building demand). **[M]**
3. **Resource demand** (commercial / industrial / office): which *company types* may spawn. A
   negative resource demand blocks spawning of that company type. **[M]**

Zone spawning then works per lot: a zoned, road-adjacent empty lot is picked; if building demand
for that zone and density is positive a building prefab fitting the lot is spawned. Exact per-frame
sampling and the probability formula are **[?]** (not documented anywhere I could reach).

### 1.2 Demand factors shown per zone (the "demand factors UI")

Compiled from the Paradox forum thread "Elucidation of every demand factor", Economy 2.0 patch
notes, the Infixo RealEco mod README (documents vanilla neutral points) and wiki pages. Tags show
confidence; thresholds are as reported by players.

**Residential**
| Factor | Sign logic | Notes |
|---|---|---|
| Happiness | + above neutral, - below | City average happiness. **[H]** |
| Homelessness | - when homeless citizens exist | Players report homeless with jobs can crush demand to 0. **[M]** |
| Taxes | + below 10%, - above 10% | Rate range is -10..30 per zone; residential tax is a wage tax. **[H]** |
| Students | small + | Only medium/high density; student places available. **[M]** |
| Jobs available / Unemployment | + when free jobs, - when unemployed | **[H]** (patch text) |
| Empty (free) buildings | + when < 10 free properties (5 for low density), - when above | Large negative when oversupplied. **[M]** |

**Commercial**
| Factor | Sign logic | Notes |
|---|---|---|
| Local demand | + when citizens/companies need a retail resource | Excludes lodging and petrochemical. **[M]** |
| Hotel (lodging) demand | + when tourists lack beds | Separate resource. **[M]** |
| Taxes | + below 10, - above 10 | RealEco says vanilla sensitivity is half of its modded value. **[M]** |
| Empty buildings | as residential | Also fires when a resource tax is too high. **[M]** |
| Service availability | neutral at **70%** | How well local demand is served. **[M]** |
| Sales capacity | neutral at **100%** | Shop capacity vs. consumption. **[M]** |
| Employee capacity | neutral at **75%** | Companies' filled jobs. **[M]** |

**Industrial and office**
| Factor | Sign logic | Notes |
|---|---|---|
| Local demand | primary factor | Driven by the square of (needed resources minus produced), averaged over resources. **[L]** |
| Labor availability | + if uneducated/poorly educated workers > **1.25x** free workplaces for those levels | Industrial mainly. **[M]** |
| High-skill labor availability | same 1.25x rule for educated/well/highly educated | Office mainly. **[M]** |
| Taxes | + below 10, - above 10 | **[M]** |
| Empty buildings | as above | **[M]** |
| Storage / export | surplus pushes demand down | Warehouses cap overproduction. **[L]** |

Take-aways for design: (a) factors are **signed integer contributions that sum to the bar**, (b)
"free properties" is a hard feedback that stops overbuilding, (c) labour is matched **per
education level**, (d) tax neutral point is 10%.

### 1.3 Progression: XP and the 20 milestones

**XP sources [H]:** passive XP is awarded 16 times per in-game day from *increases* in population
and happiness. Active XP is granted immediately when placing or upgrading service buildings,
placing signature buildings, laying roads, and adding outside connections. Colossal Order notes
passive XP is deliberately sluggish; building things is the fast path.

**Player-logged active XP values [L]** (one player's log, inconsistent with tooltips): road service
150, clinic 150, police 300, fire 300, elementary school 300, high school 500, college 1,000,
university 2,000, small park 100, post office 500, harbour 1,000, telecom tower 2,000, bronze
statue 4,000, Grand Hotel landmark 2,000.

**Cumulative XP thresholds [L]** (same player log / NamuWiki; the log's Tiny Village = 750):
750, 2,500, 4,800, 8,300, 13,600, 21,300, 32,100, 46,700, 65,700, 89,700, 119,700 for milestones
1..11; milestone 15 = 305,700, 16 = 369,700, 17 = 439,700, 19 about 592,700. Milestones 12-14 and
18-20 are not documented; extrapolated increments (about +30k per step growing to +70k) give
roughly 155k, 205k, 255k (12-14), 515k (18), 672k (20). Treat as indicative only.

**The 20 milestones [H]** (cs2.paradoxwikis.com/Progression; reward is on reaching the milestone,
DP = development points, Tiles = expansion permits, all cumulative in the right half):

| # | Name | Money | DP | Tiles | Cum DP | Cum Tiles | Notable unlocks |
|--:|---|--:|--:|--:|--:|--:|---|
| 1 | Tiny Village | 25k | 1 | 3 | 1 | 3 | Row housing, farming, coal, healthcare+deathcare, garbage, map tiles, taxation, budget, statistics, 4-lane roads |
| 2 | Small Village | 50k | 2 | 4 | 3 | 7 | Medium housing, education and research, production panel |
| 3 | Large Village | 75k | 3 | 5 | 6 | 12 | Ore mining, fire and rescue, police and admin |
| 4 | Grand Village | 100k | 4 | 6 | 10 | 18 | Low-rent housing, low-density offices, oil, import services, taxi fare, transport, parks, **district tool**, **policies**, disasters |
| 5 | Tiny Town | 125k | 5 | 7 | 15 | 25 | Mixed housing, pre-release, parking fee, recycling, speed bumps, energy awareness, communications |
| 6 | Boom Town | 150k | 6 | 8 | 21 | 33 | Heavy traffic ban, advanced pollution mgmt |
| 7 | Busy Town | 175k | 7 | 9 | 28 | 42 | Gated community |
| 8 | Big Town | 200k | 8 | 10 | 36 | 52 | High-density housing |
| 9 | Great Town | 225k | 9 | 12 | 45 | 64 | High-density business |
| 10 | Small City | 250k | 10 | 15 | 55 | 79 | High-density offices, high-speed highways, combustion ban, city promotion |
| 11 | Big City | 275k | 11 | 18 | 66 | 97 | (none) |
| 12 | Large City | 300k | 12 | 21 | 78 | 118 | |
| 13 | Huge City | 325k | 13 | 24 | 91 | 142 | |
| 14 | Grand City | 350k | 14 | 28 | 105 | 170 | |
| 15 | Metropolis | 375k | 15 | 32 | 120 | 202 | |
| 16 | Thriving Metropolis | 400k | 17 | 36 | 137 | 238 | |
| 17 | Flourishing Metropolis | 425k | 19 | 41 | 156 | 279 | |
| 18 | Expansive Metropolis | 450k | 21 | 46 | 177 | 325 | |
| 19 | Massive Metropolis | 475k | 25 | 51 | 202 | 376 | |
| 20 | Megalopolis | 500k | 30 | 56 | 232 | 432 | |

Patch 1.1.5f1 reduced milestone money substantially (so the table above is "pre-nerf"; **[M]**).
Loan limits also grow with milestones (100k -> 4M). Achievement "The Last Mile Marker" = milestone 20.

### 1.4 Development tree [H]

When a service is unlocked by a milestone, only its tier-1 basics exist. Further buildings are
**nodes** bought with development points. Tiers cost **1 / 2 / 4 / 8 DP** (Tier 1..4). Some
trees have branches; you do not need every node of a tier to proceed. All milestones together give
232 DP vs. 228 needed for the whole tree, so the player chooses order, not whether.

Representative trees (Dexerto / IndieGameCulture, **[H]**):
- **Roads:** Roundabouts 1, Advanced Road Services 1 -> Highways 2, Large Roads 2 -> Intersections 4 -> Grand Bridge 8; Parking Areas 2 -> Underground 4 -> Automated 8.
- **Electricity:** Gas 2 -> Coal 4 -> Nuclear 8; Emergency Battery 1 -> Geothermal 2 / Hydro 2 / Solar 4.
- **Water:** Water Treatment 2 -> Advanced Pumping 8.
- **Healthcare:** Hospital 2 -> Disease Control 8 / Health Research 8; Crematorium 1.
- **Garbage:** Incinerator 2, Recycling 2 -> Industrial Waste 8.
- **Education:** College 1 -> University 2 -> Radio Telescope 4 / Geological Research 4 -> Hadron Collider 8; Technical 2; Medical 2.
- **Fire:** Small Shelter 1 -> Large Shelter 4 -> Early Warning 8; Fire Station 2; Firewatch 1 -> Helicopter 2.
- **Police/Admin:** Welfare Office 1 -> City Hall 4 -> Central Bank 8; Police HQ 2 -> Prison 4 / Intelligence 8.
- **Transport:** Train 1 -> Tram 2 -> Subway 4, Intl Airport 8; Water 2; Air 4 -> Space Center 8.
- **Parks:** Park Maintenance 1 -> Large Parks 2, Sports Parks 2 -> Large Sports 4 -> **Tourist Attractions 8**.
- **Communications:** Post Sorting 2, Server Farm 4, Telecom Tower 4 -> Satellite 8.

### 1.5 Map tiles [H unless tagged]

- 441 purchasable tiles of about 0.4 km2 each (23x23 grid = 529 including unreachable edge tiles).
- Start with **9 free tiles**; expansion unlocks at milestone 1.
- Each purchase costs **1 expansion permit + money**. Money scales with buildable area (about
  0.1184 per m2 of buildable land), plus value of resources inside the tile (16.9..55.6 per unit
  by resource) plus a roughly 125 base increment. Water-heavy tiles are cheapest. **[M]**
- Price rises with tiles already owned (exact multiplier **[?]**). **Tile upkeep** = a percentage
  of the tile's purchase price, the percentage rising on a curve **5% -> 25%** as more tiles are
  owned, applied to *all* owned tiles; the first 9 are free of upkeep.
- Outside connections (highways, rail, ports) live on border tiles, so expansion also opens trade.
- Achievements: Explorer = 50 tiles, Everything the Light Touches = 150 tiles.

### 1.6 Policies [H]

Three scopes: **city**, **district**, **building**. Unlock via milestones (4+).

| Scope | Policy | Effect | Unlock |
|---|---|---|---|
| City | Taxi Minimum Fare | minimum taxi charge | MS4 |
| City | Import City Services | neighbours supply police/health/death/garbage/fire | MS4 |
| City | Pre-Release Programs | prisoners educated before release | MS5 |
| City | Advanced Pollution Management | industrial filters cut air+ground pollution, more garbage | MS6 |
| City | City Promotion | + attractiveness (tourists), + crime near attractions | MS10 |
| City | High-Speed Highways | no highway speed limit, faster, more noise/accidents | MS10 |
| District | Energy Consumption Awareness | electricity use -5% | MS5 |
| District | Recycling | less resource use, less free time | MS5 |
| District | Roadside Parking Fee | fee 1..50 per stay, changes mode choice | MS5 |
| District | Speed Bumps | slower cars, fewer accidents, less noise | MS5 |
| District | Heavy Traffic Ban | trucks barred, less noise/air pollution | MS6 |
| District | Gated Community | only residents/workers enter | MS7 |
| District | Combustion Engine Ban | fuel vehicles barred unless local | MS10 |
| Building | Parking Fee (lot) | per-lot fee 1..50 | - |

CS1 had many more (Smoking Ban, Schools Out, Free Public Transport, Energy/Water Saving, Education
Boost, Small Business Enthusiast, Industrial Space Planning...). Those are good inspiration for
OpenCity's yaml-driven catalogue. Policies cost **upkeep per month** in CS1; in CS2 they are mostly
free to enable but cost indirect side-effects (trade-off design). Achievements tie in: "Calling
the Shots" = 5 city policies active.

### 1.7 Districts [H]

- Drawn with the **District Tool**: place corner nodes, close the polygon; nodes can be moved or
  added to edges. Auto-named, renameable. Unlocks MS4.
- Panel shows citizens, average wealth, education levels, enabled policies, and a happiness
  tooltip listing every benefit/drawback of the area.
- **Service assignment**: services can be restricted to chosen districts (unassigned = whole city).
- District policies (table above). Achievements: Happy to Be of Service, Executive Decision,
  Wide Variety (10 districts with policies).

### 1.8 Tourism [M]

- **Attractiveness** (global score, shown on the Tourism info view, 0..100+ scale; the
  achievement "Simply Irresistible" needs 90) is the main factor in how many tourists visit.
  Parks and Recreation buildings carry their own Attractiveness value that adds up. Examples:
  Observation Tower 40, Bronze Statue 50, Water Park 75, Medieval Castle 85; signature
  buildings add small percentages. City Promotion policy adds more. Weather/seasons modulate. **[M/L]**
- Tourists arrive via outside connections (highway first; later rail, air, sea).
- They need **lodging**: hotels are commercial companies selling the immaterial resource
  "lodging", spawned by commercial demand (hotel factor). Tourists choose each day between
  staying at the hotel and visiting leisure spots, weighted by distance to hotel and the
  attractiveness of the spot. They spend at shops/restaurants and pay transit fares, i.e. they
  are citizens with a `TouristHousehold` flag, not a separate economy. **[L]**
- Achievement "Welcome, One and All!" = 10,000 tourist visits.

### 1.9 Statistics and achievements [H]

CS2 tracks time-series statistics (population, jobs, finances, education, tourism, services
usage, transport) in an in-game Statistics panel with history graphs. 40 achievements; the ones
touching this domain: My First City, Strength Through Diversity (all 4 zone types), All Smiles
(1000 pop and happiness 75), Six Figures (100k pop), Top of the Class (20% university),
Making a Mark (5 signature buildings), Welcome One and All, Last Mile Marker (MS20).

---------------------------------------------------------------------------------------------------

## 2. Current OpenCity state and gaps

Source files read: `CityManager.cs`, `CityManager.Economy.cs`, `CityManager.Daily.cs`,
`CityManager.Stats.cs`, `CityPlaceable.cs`, `ZoneGrowth.cs`, `DemandBarsWidget.cs`,
`CityTopBarLogic.cs`, `CONTRACT.md`.

### 2.1 What exists

- **Demand** (`UpdateDemand`, daily): four scalars R/C/I/O in -100..100, each computed from
  aggregate counters (pop, workforce, unemployment rate, job slots per category, happiness,
  tax, utility shortage), then eased (`DemandRiseStep` 3 up, `DemandFallStep` 6 down per day).
  Residential also has a vacancy penalty and a separate `ResidentialAttraction`.
  There is **no factor breakdown**, only the final integer. The widget tooltip shows the number.
- **Growth** (`ZoneGrowth`): every 10 ticks, per zone type with demand > 0, spawn
  `ceil(demand/25)` level-1 buildings (cap 8 under construction per zone type). Zone density
  (low/high) is just a different zone type; demand is per *category*, not per density.
- **Milestones** (`CheckMilestones`): 8 milestones by **peak population** (0/250/1k/2.5k/5k/10k/25k/50k),
  cash bonus 5k..200k, notification key per milestone. `IsUnlocked(actor)` = `PeakPopulation >=
  CityPlaceable.UnlockPopulation`. Only 3 placeables use `UnlockPopulation` (school 250, park-large
  500, solarplant 1000). Zones are **not** gated.
- **Taxes**: one rate per category (0..30, default 10), `SetTax` order. Tax feeds demand
  (+/-3 per percentage point), happiness and income.
- **UI**: `DemandBarsWidget` (4 bipolar bars + number tooltip), milestone label/bar in
  `CityTopBarLogic` (population / next threshold), budget panel with tax sliders.
- Orders available: BuildRoad, Bulldoze, PlaceBuilding, Zone, SetTax, SetSpeed.

### 2.2 Gaps versus the CS2 goal

| Area | Gap |
|---|---|
| Demand | No factor list; one scalar per category. Not agent-driven (citizens are counters on buildings). No education-level matching, no free-property (vacancy) feedback for C/I/O, no goods/resource flow, no tourism/lodging. |
| Demand to spawn | Linear `ceil(demand/25)` with no density split, no per-lot scoring, no free-properties brake for non-residential. |
| Progression | Pop-only milestones; no XP, no development points, no tree, no map tiles, no per-milestone unlock of zones/policies/tools. |
| Policies | None. No city or district policy system, no yaml catalogue, no order. |
| Districts | None. No district id on cells, no paint tool, no per-district stats, no service restriction. |
| Tourism | None. No outside-visitor citizens, hotels, attractions, attractiveness stat. |
| Statistics | Only live counters and last-month budget dictionaries; no time-series, no achievements. |
| Map expansion | Whole map (up to 128x128) is buildable from day one. |
| Determinism hooks | Existing design is good: integer maths, ActorID order, `[VerifySync]` on state. New systems must keep that. |

### 2.3 Reuse

- Keep `CityManager` as the owner of funds/calendar. Put new systems in **new partial files**
  (`CityManager.Progression.cs`, `.Policies.cs`) or separate player traits that CityManager
  queries, so WP-E's files stay small.
- Keep `GetDemand(cat)` as the stable public API (ZoneGrowth, widgets, autotest use it). The new
  model fills the same array and *additionally* exposes a factor breakdown.
- Reuse `CityOrders` pattern: new order strings for unlock-node, buy-tile, set-policy, paint-district.

---------------------------------------------------------------------------------------------------

## 3. Proposed design

Principles: (1) **everything is a signed integer factor** so the UI can always explain a number;
(2) **data in yaml**, logic in few generic systems; (3) every state change is an **order**;
(4) iterate in `ActorID` / index order, no floats, no `Random` except `world.SharedRandom`;
(5) old public API (`GetDemand`, `IsUnlocked`, `MilestoneIndex`...) stays valid.

### 3.1 Demand model driven by agent sims, with factor breakdown

**Data flow.** Once per day, after citizens/companies have ticked, the sims fill a plain struct
(`DemandInputs`, below). `DemandModel.Compute(inputs, taxes, policies)` is a pure integer function
that returns `int[cat][factorId]`. The sum, clamped to -100..100, is the *target*; the bar value
still eases (+3 up / -6 down per day as today). Pure function = unit-testable, same result on all clients.

```
struct DemandInputs {            // all counts, filled by citizens (1), economy (5), zoning (4)
  int Pop, Households, Homeless, Unemployed;
  int[5] WorkersByEdu, FreeJobsByEdu, FilledJobsByEdu;   // edu 0..4 (uneducated..highly)
  int FreeStudentSeats; int AvgHappiness;
  int[3] FreeResUnits, ResBuildingsFree;                 // by density: low, mid, high (vacant units / buildings)
  int[CatCount] FreeBuildings;                           // non-res buildings with unfilled jobs AND no company
  int ShopNeedPerDay, ShopCapacityPerDay, TripsServedPct;// retail: demand vs capacity, "service availability"
  int GoodsNeedPerDay, GoodsProducedPerDay, StockPct;    // industrial: needed vs produced; warehouse fill
  int OfficeNeedPerDay, OfficeProducedPerDay;
  int TouristsUnhoused, LodgingOccupancyPct, Attractiveness;
  int PowerShortagePct, WaterShortagePct;
}
```
**MVP shortcut:** until agents exist, the current counters fill the same struct (`Jobs`,
`Workers`, `Unemployed`, `jobSlots`) so the factor UI ships first and the sims plug in later.

**Factor catalogue** (ids are an enum; each has a fluent key `demand-factor-<id>` and a sign).
Values are bar points; bar = clamp(sum, -100, 100). `T` = tax rate percent, neutral 10.

| Cat | Factor | Formula (integer) | Range |
|---|---|---|---|
| R | Start appeal | +30 while Pop < 150, linear fade to 0 at 600 | 0..30 |
| R | Jobs | `(FreeJobs - Unemployed) * 100 / (FreeJobs + Unemployed + 20) / 2` | -50..50 |
| R | Happiness | `(AvgHappiness - 50) / 2` | -25..25 |
| R | Taxes | `(10 - T) * 3` clamped | -30..30 |
| R | Homelessness | `-min(40, Homeless * 200 / (Pop + 100))` | -40..0 |
| R | Students | `+min(10, FreeStudentSeats / 10)` (mid/high density only) | 0..10 |
| R | Utilities | `-(PowerShortagePct + WaterShortagePct) / 4`, max 25 | -25..0 |
| R | Free housing (per density d) | `K = 10 (mid/high), 5 (low)`; `+(K - free) * 50 / K` if `free <= K`, else `-(free - K) * 50 / K`, clamp -60..50 | -60..50 |
| C | Local demand | `clamp((ShopNeed * 100 / (ShopCapacity + 1) - 100) / 2, -50, 50)` | -50..50 |
| C | Service availability | neutral 70%: `clamp((70 - TripsServedPct) * 3 / 2, -30, 45)` | -30..45 |
| C | Goods available | `-min(30, shopsOutOfStockPct / 2)` (retail starved; pushes I) | -30..0 |
| C | Workforce | neutral 75% filled: `clamp((fill - 75) * 2, -40, 10)` | -40..10 |
| C | Hotels | `+min(25, TouristsUnhoused)` if lodging full; `-(40 - Occ)/2` if Occ < 40 | -20..25 |
| C | Taxes / Free buildings | as R / free-property rule (K = 10) | |
| I | Local (resource) demand | `clamp((GoodsNeed * 100 / (GoodsProduced + 1) - 100) / 2, -50, 60)` | -50..60 |
| I | Labour (low edu) | `+` if `WorkersByEdu[0..1] > 1.25 * FreeJobsByEdu[0..1]` else `-`, scaled to 30 | -30..30 |
| I | Storage | `-(StockPct - 60) / 2` above 60% (overproduction) | -20..0 |
| I | Taxes / Free buildings | as above | |
| O | Local demand | same as I with OfficeNeed/Produced | -50..60 |
| O | Labour (high edu) | 1.25x rule on edu 2..4 | -30..30 |
| O | Taxes / Free buildings | as above; Office stays locked until its unlock node/milestone | |

All categories also get **Utilities** (`-shortage/4`). The UI shows, per bar, the factors sorted
by absolute value with a signed chip, exactly like CS2's tooltip.

**Spawn pipeline** (zoning WP D, small change in `ZoneGrowth`):
1. Demand is also published per *zone type* (density): `GetDemand(ZoneType z)` = category factors
   minus category free-housing plus *this density's* free-housing factor. `GetDemand(cat)` stays = max over its zone types.
2. Replace `ceil(demand/25)` (today demand 1 already spawns one building per attempt, i.e. no
   proportionality) with an **accumulator**: `acc[z] += max(0, demand); n = acc / 25; acc %= 25;`.
   Demand 10 now spawns one building per ~3 attempts, demand 100 four per attempt. Deterministic, int-only.
3. Lot choice: among candidates score `= LandValue/2 + roadDepthBonus + (SharedRandom % 16)`; take
   the best of 3 random picks. Candidates must be on an **owned map tile** (3.4).
4. Per-zone cap `MaxUnderConstruction` stays, scaled with owned tiles.

**Feedback loops to keep stable:** free-property brake (R, C, I, O) prevents overbuilding;
easing step prevents flapping; homeless and unemployment factors are *capped* so one factor can
never alone pin a bar at -100; start-appeal bootstraps an empty city.

### 3.2 XP and milestones (replaces population-only milestones)

New player trait `Progression` (partial of `CityManager`, file `CityManager.Progression.cs`); all
numbers live in `rules/progression.yaml` (loaded with `FieldLoader.LoadUsing`).

**XP sources** (synced `int Xp`, `[VerifySync]`; shown in the top bar as bar + number):
| Source | XP | Anti-farm rule |
|---|---|---|
| New resident (passive, daily) | 2 per resident above `PeakPopulation` high-water | peak-based, so bulldoze/regrow gives nothing |
| New filled job (passive) | 1 per worker above `PeakEmployed` | same |
| Happiness bonus (passive) | `+ (AvgHappiness - 60) * Pop / 200` per day while > 60, max 50/day | tiny; CS2 also rewards happiness |
| Road cell | 2 | only above `PeakRoadCells` high-water |
| Service/utility building | `CityPlaceable.Xp` (clinic 40, police 60, fire 60, school 60, power 50, water 30, small park 10, large park 40) | full XP for the first 5 of each type, 25% after |
| Landmark / signature | 300..1500 | unique building, once |
| Growable level-up | `level` | none needed (rare) |
| Outside connection | 150 | once per connection |
| Dev node bought | 0 | (points are the sink, not a source) |

**Milestone table** (20 rows; 1-12 have content, 13-20 are "prestige" tiers that give money, DP and
tiles so big cities keep a goal). Names keep the 8 existing OpenCity names, then extend. XP is
cumulative. Money is deliberately lower than CS2 (1.1.5f1 nerfed it too). Loan limit omitted (no loans yet).

| # | Name | XP | Money | DP | Tiles | Unlocks |
|--:|---|--:|--:|--:|--:|---|
| 0 | Village | 0 | - | 0 | 9 start | roads, res-low, com-low, ind, power coal/wind, water tower/pump, police, fire, clinic, park-small, Taxes + Budget panel |
| 1 | Hamlet | 300 | 5k | 1 | 2 | Statistics panel, school (node tree opens), plaza |
| 2 | Small Town | 1,000 | 10k | 1 | 2 | res-high, com-high, Districts tool |
| 3 | Town | 2,000 | 15k | 2 | 2 | Policies panel (tier-1 policies), garbage |
| 4 | Large Town | 3,500 | 20k | 2 | 3 | Office zone, tier-2 policies, Tourism info view |
| 5 | Small City | 5,500 | 30k | 3 | 3 | Hotels (lodging demand), solar plant |
| 6 | City | 8,500 | 40k | 3 | 3 | Heavy Traffic Ban, Gated Community, parks tree tier 2 |
| 7 | Big City | 13,000 | 50k | 4 | 3 | Landmark slot 1, City Promotion |
| 8 | Metropolis | 19,000 | 60k | 4 | 4 | High-speed highway, Combustion ban |
| 9 | Large Metropolis | 27,000 | 75k | 5 | 4 | tier-3 nodes |
| 10 | Great Metropolis | 37,000 | 90k | 5 | 4 | Landmark slot 2 |
| 11 | Grand Metropolis | 49,000 | 100k | 6 | 4 | |
| 12 | Capital | 63,000 | 110k | 6 | 4 | tier-4 nodes |
| 13..20 | Megacity I..VIII | 80k, 100k, 125k, 155k, 190k, 230k, 275k, 325k | 120k..190k | 7,7,8,8,9,9,10,10 | 4,5,5,5,5,5,3,2 | prestige; MS20 = achievement "Last Mile Marker" |

Tile column = permits granted (sum 72 = 81 tiles - 9 start). DP sum = 42 (milestones 1-12) + 68 (13-20) = **110**; the dev tree must total **<= 110** (lint test).
Thresholds are **[?]-tuned**: they must be calibrated with the autotest (acceptance 5.3).

Orders: none for XP (derived). A milestone grants rewards inside `CheckMilestones` (synced,
daily). `Notify("notification-city-milestone-N")` and a **milestone popup** (UI) with unlock list.

**Unlock registry:** `IsUnlocked(string key)` where key is an actor type, `zone:res-high`,
`tool:districts`, `policy:speed-bumps`, `node:elec.gas`. Sources: milestone `Unlocks:` lists, bought
nodes, and the legacy `CityPlaceable.UnlockPopulation` (kept as fallback so existing yaml works).
`CityPlaceable` gains `UnlockMilestone`, `UnlockNode`, `Xp`, `Attractiveness`, `Landmark`.
`ZoningTool` and the toolbar call `IsUnlocked("zone:...")`.

### 3.3 Development points and tree (data-driven)

`rules/progression.yaml`:
```yaml
Progression:
  Tree@elec:  # one node list per service tree
    Node@elec.basic: Cost: 0, Unlocks: windturbine powerplant-coal, Requires: MS0
    Node@elec.gas:   Cost: 2, Requires: elec.basic, Unlocks: powerplant-gas
    Node@elec.solar: Cost: 4, Requires: elec.gas,   Unlocks: solarplant
```
- Tier costs follow CS2: **1 / 2 / 4 / 8**. Tier-1 nodes auto-unlock with the milestone that opens the tree.
- A node has `Requires` (one or more node ids), `Unlocks` (actor types, policies, tools), `Tree`, icon, fluent name.
- State: `ulong[] nodeBits` + `int DevPoints` (unspent). Order **`CityUnlockNode`** (TargetString = node id)
  validates `DevPoints >= Cost`, requirements met, not owned.
- Candidate OpenCity trees mapped to what exists/will exist: Roads (roundabout, avenue, highway), Electricity
  (wind, coal, gas, solar, battery), Water (tower, pump, treatment), Health (clinic, hospital), Education
  (school, college, university), Fire (station, helicopter), Police (station, HQ, prison), Parks
  (small, large, sports, **attractions 8**), Garbage (landfill, incinerator, recycling), Transport (bus, train, subway, airport), Comms.
  About 40 nodes with total cost <= 110 DP; cheap early nodes (1-2) are the "first picks".
- UI: a hex or box tree per service (CS2 style) in the Progression panel; locked nodes show requirement.

### 3.4 Map tiles: purchasable buildable area

- **Grid:** map playable bounds divided into a **9x9 grid** of tiles. On 128x128 each tile is
  14x14 cells (remainder border = last row/column tiles absorb the extra cells). 81 tiles, **9 owned
  at start** (3x3 block that contains the highway end), 72 purchasable. Other map sizes use the same 9x9 grid.
- **World trait `TileGrid`** (new file, WP C-adjacent): `bool IsOwned(CPos)`, `ulong[2] ownedBits`,
  `int OwnedCount`, `event TileOwnershipChanged`. Cells of unowned tiles are *unbuildable*: roads
  (`RoadLayer.CanBuildRoadAt`), zoning (`ZoneLayer.CanZone`), placement (`PlaceCityBuildingOrderGenerator`),
  growth candidates and bulldoze all consult it. Existing actors on a tile never change owner.
- **Purchase:** order **`CityBuyTile`** (ExtraLocation = tile x,y). Valid if `Permits > 0`, funds
  suffice, tile **adjacent (4-neigh) to an owned tile** or containing an outside connection. Spends 1
  permit and the price via `TrySpend(price, "tiles")`.
- **Price** (ints): `price = (2000 + 8 * buildableCells + resourceValue) * (100 + 6 * purchased) / 100`
  where `buildableCells` excludes water. A 196-cell land tile costs about 3.6k first and about 19k
  as the 72nd; water-heavy tiles are cheap (as in CS2). `resourceValue` is 0 until a resource layer exists.
- **Tile upkeep:** per month `price_paid * pct / 100 / 20` with `pct = 5 + 20 * purchased / 72`
  (CS2: 5%->25% curve applied to all owned tiles; the /20 divisor is ours to keep it affordable). The 9 start tiles are free.
- **Visuals:** unowned tiles darkened with a border; hover shows price and permit use; owned
  border thin. Achievement hooks: 20 / 50 tiles.
- **Outside connections** only exist on tiles you own; a new highway tile also raises arriving traffic and tourists.

### 3.5 Policies (data-driven)

`rules/policies.yaml` defines a catalogue; code only knows **effect keys**. A policy is
`Scope: City | District`, `UnlockMilestone` (or node), `Upkeep` (money/month, may scale by affected
population), an optional `Slider` (min, max, default) and a list of `Effect: key value`.

```yaml
Policies:
  Policy@speed-bumps: Scope: District, Unlock: MS3
    Effect: TrafficSpeedPct -25, Effect: AccidentPct -30, Effect: NoisePct -20
  Policy@energy-saving: Scope: District, Unlock: MS3, Effect: PowerUsePct -5, Upkeep: 1 per 100 residents
  Policy@parking-fee: Scope: District, Slider: 1..50, Effect: ParkingFee @slider, Effect: ParkingIncome
```
Effect keys are integer percent/flat modifiers summed over **city + the district of the cell**:
`PowerUsePct`, `WaterUsePct`, `GarbagePct`, `NoisePct`, `PollutionPct`, `CrimePct`, `AccidentPct`,
`TrafficSpeedPct`, `TruckBan`, `ThroughTrafficBan`, `FuelVehicleBan`, `ParkingFee`, `AttractivenessPct`,
`HappinessFlat`, `LandValuePct`, `ServiceRadiusPct`, `EduBoostPct`, `TaxDemandPct`, `JobsPct`.
Consumers query `PolicyState.Get(key, cell)` (O(1): per-district cache rebuilt when a policy order
arrives). Owners: traffic (3) reads TrafficSpeed/Ban/Parking, services (7) ServiceRadius/Crime,
citizens (1) Happiness/Edu, economy (5) Upkeep/Parking income/Tax.

**OpenCity catalogue** (CS2 set plus CS1 favourites; "M" = milestone):
| Scope | Policy | Effect | M |
|---|---|---|---|
| City | Taxi/transit minimum fare | + fare income, - ridership | 3 |
| City | Import services | pay per use, no building needed (police, fire, garbage, health) | 3 |
| City | Advanced pollution mgmt | `PollutionPct -40` industry, `GarbagePct +15` | 6 |
| City | City Promotion | `AttractivenessPct +25`, `CrimePct +10` near attractions, upkeep scales with pop | 7 |
| City | High-speed highways | highway speed +, `NoisePct +`, `AccidentPct +10` | 8 |
| City | Smoking ban | `HappinessFlat +2`, health +, small businesses (bars) `JobsPct -3` | 4 |
| City | Schools out / Education boost | `EduBoostPct +20`, upkeep + | 5 |
| City | Free public transport | ridership +, fare income 0 | 5 |
| District | Energy saving | `PowerUsePct -5`, happiness -1 | 3 |
| District | Water saving | `WaterUsePct -10` | 3 |
| District | Recycling | `GarbagePct -30`, `PollutionPct -5`, upkeep per resident | 3 |
| District | Speed bumps | speed -25%, accidents -30%, noise -20% | 3 |
| District | Roadside parking fee | slider 1..50, income, mode shift to transit | 3 |
| District | Heavy traffic ban | trucks barred, noise/air -, industry efficiency - | 6 |
| District | Gated community | `ThroughTrafficBan`, `CrimePct -40`, `LandValuePct +10` | 6 |
| District | Combustion engine ban | only electric/locals, air pollution - | 8 |
| District | Small business enthusiast | commercial low `JobsPct +10` | 5 |
| District | Industrial space planning | industry `JobsPct +15`, `PollutionPct +10` | 6 |

State: `ulong cityPolicies`, `ulong[] districtPolicies`, `byte[] sliderValues`. Orders: **`CitySetPolicy`**
(ExtraLocation.X = policy id, ExtraLocation.Y = district id or 0, ExtraData = on/off or slider value).
Monthly upkeep is charged in `EndMonth` under key `policies`. Achievement hook: 5 active city policies.

### 3.6 Districts

- **Layer:** `DistrictLayer` world trait: `byte[] districtOfCell` (0 = none, 1..63), 63 districts max
  (matches the `ulong` masks above). `GetDistrict(cell)`, `event DistrictChanged`, names in `string[64]`.
- **Paint tool** (`CityDistrict` order, same drag generator as zoning): rectangle paint
  (ExtraData = district id, 0 = erase) plus **brush** (radius 1..4) via repeated orders. Orders:
  `CityDistrict` (paint), `CityDistrictName` (TargetString), `CityDistrictDelete`. New district id
  = lowest free id (deterministic, chosen in the order handler). A district must own at least one cell
  or it is auto-removed at month end. Painting is free; unlocked at MS2.
- **Stats** (`DistrictStats[64]`, recomputed daily in the existing building loop, O(buildings)):
  population, households, jobs, employed, avg happiness, avg land value, education histogram (5),
  tax income per category, power and water use, pollution avg, tourists present, enabled policies.
  Per-district happiness exposes its factor list like demand does (tooltip).
- **Service restriction:** `ServiceBuilding.DistrictMask` (`ulong`, 0 = whole city). The coverage layer and
  dispatchers (services 7) only count the building inside masked districts; set by order `CitySetServiceDistricts`.
- **Policies** apply by cell lookup; overlapping is impossible (one district per cell).
- **Rendering:** translucent 16-colour palette per district id + bold boundary line, drawn by a
  `DistrictOverlay` render trait (like `ZoneOverlay`), plus floating district name labels.

### 3.7 Tourism

- **Attractiveness** (0..100, synced): `raw = sum(Attractiveness of operational placeables x service
  efficiency/100) + landmarkBonus + scenery (park cells + water-adjacent park cells) / 8`;
  `pollutionPenalty = avgPollution / 3`; `policy = raw * (100 + AttractivenessPct) / 100`;
  `attr = 100 * x / (x + 150)` (diminishing returns). CS2 reference values: tower 40, statue 50, water park
  75, castle 85 -> OpenCity landmarks: Observation Tower 40, Statue 50, Water Park 75, Castle 85, plus
  parks 3..12, plaza 5, stadium 25.
- **Arrival:** each day, `n = (attr * attr / 400) + connectionBonus` visitor-groups spawn at an owned
  outside connection (citizens sim creates `Tourist` agents with 1-4 members). If `LodgingFree == 0`
  only 25% become day-trippers, the rest leave (feeds the *Hotels* demand factor).
- **Behaviour (citizens 1):** go to a hotel with free beds, each day pick one attraction/leisure/shop
  weighted by `attractiveness / (distance + 4)`, spend money (retail goods, hotel nights, ride fares),
  leave after 2..6 days. Spending is real company income and thus taxed.
- **Hotels:** commercial growables `hotel-1..3` (beds 8/20/60) spawned by the commercial *Hotels* factor
  (needs MS5); also bought-as-signature "Grand Hotel". `lodging` is a retail resource in the economy domain.
- **Stats:** tourists present, tourists per month, hotel occupancy, attractiveness, tourism income.
  Info view `Tourism` (heat = per-cell attractiveness of the nearest attraction).

### 3.8 Statistics and achievements

- `CityStats` player trait: monthly samples in ring buffers (120 months): population, jobs,
  unemployed, income, expenses, happiness, XP, tourists, attractiveness, demand R/C/I/O, education
  histogram, tiles owned. Everything read-only derived => no sync cost beyond existing `[VerifySync]` fields.
- `Achievements`: yaml list `Achievement@six-figures: Stat: Population, Op: >=, Value: 100000` (stat
  names are the ring-buffer keys) plus counters (bulldozed buildings, policies active, districts with
  policies, landmarks). Bitmask `ulong`, notification on unlock, panel with checkmarks. First pass
  ports ~20 CS2 achievements (My First City, Strength Through Diversity, All Smiles, Six Figures,
  Top of the Class, Making a Mark, Welcome One and All, Explorer, Calling the Shots, Wide Variety...).

---------------------------------------------------------------------------------------------------

## 4. Interfaces with other domains

| Domain | I consume | I provide |
|---|---|---|
| **1 Citizens** | per-citizen `Education 0..4`, `Employed`, `Homeless`, `IsStudent`, `IsTourist`; counts per daily snapshot (`DemandInputs`); spawn/leave of tourists at outside connections | `Attractiveness`, `LodgingFree`, tourist arrival count, `PolicyState.Get(Happiness/Edu/..., cell)`, demand bars (new residents move in only if `R` target > 0) |
| **5 Economy** | shop/goods/office need and production per day, stock fill, shops out of stock, tax collected by category, hotel revenue | `Xp`, milestone money via `AddFunds("milestone")`, tile purchase and tile upkeep and policy upkeep via `TrySpend`/`EndMonth`, tax rates (`taxRates` stay), `TaxDemandPct` |
| **4 Zoning** | free buildings and vacant units per zone/density, lot candidates, building level | `GetDemand(ZoneType)`, `IsUnlocked("zone:...")`, `TileGrid.IsOwned(cell)` gate (`CanZone`, growth), `DistrictLayer` cell lookup |
| **7 Services** | coverage, service efficiency, hospital/school seats (`FreeStudentSeats`) | `UnlockNode` / `UnlockMilestone` / `Xp` / `Attractiveness` fields on `CityPlaceable`, `DistrictMask` on `ServiceBuilding`, `ServiceRadiusPct`/`CrimePct` policy keys |
| **3 Traffic** (if present) | - | `PolicyState` keys: `TrafficSpeedPct`, `TruckBan`, `ThroughTrafficBan`, `FuelVehicleBan`, `ParkingFee`; outside-connection tile ownership |
| **10 UI** | - | fluent keys, factor arrays, `ProgressionView` (XP, DP, permits, next milestone), `TileGrid` data, `DistrictStats`, `PolicyState`, `CityStats` series |

Stable API additions on `CityManager` (all read-only except orders):
`Xp`, `DevPoints`, `Permits`, `IsUnlocked(string key)`, `GetDemand(ZoneType)`,
`IReadOnlyList<DemandFactor> GetDemandFactors(ZoneCategory)`, `MilestoneInfo(index)`,
`NextMilestoneXp`. New order constants: `CityUnlockNode`, `CityBuyTile`, `CitySetPolicy`,
`CityDistrict`, `CityDistrictName`, `CityDistrictDelete`, `CitySetServiceDistricts` in `CityOrders`
(CityTypes.cs is frozen: the lead must add the constants and factory methods).

---------------------------------------------------------------------------------------------------

## 5. Phased tasks and acceptance tests

Each phase ends with `dotnet build` (Release + Debug lint), `./utility.sh city --check-yaml` = 0
errors, and a **replay determinism run** (the autotest replay playback mode already exists). CityAutoTest
is lead-owned: the lead adds the named scenarios below (they only issue orders and print log lines).

**Phase 0 - Demand factors (MVP, 2-3 days).** Owner: WP E (+F for UI).
- Refactor `UpdateDemand` into `DemandModel` (pure) + `DemandInputs` filled from current counters; add
  factor arrays and `GetDemandFactors`. Accumulator spawn in `ZoneGrowth`. Tooltip `DemandFactorsWidget` on the bars.
- *Accept:* (a) log line per month `demand R=.. C=.. I=.. O=.. factors{...}`; sum of factors clamped equals
  the pre-easing target for every category; (b) in the existing stress scenario bars stay within -100..100 and
  never flap more than 6 points/day; (c) empty city: R > 0, C <= 0, I > 0 (as today); (d) demand 10 spawns about 1 building per 3 growth
  attempts (counted in test); (e) replay hash identical.

**Phase 1 - XP, 20 milestones, unlock registry (3-4 days).** `rules/progression.yaml`, `CityManager.Progression.cs`,
milestone popup, top-bar XP bar, `CityStats` ring buffers.
- *Accept:* (a) peak-based XP cannot be farmed (bulldoze/rebuild road 100x adds 0 XP after the first build);
  (b) test `scenario=progression` reaches milestone 3 within N game days (calibrate N once, then lock as a regression bound +-20%);
  (c) milestone reward paid exactly once; (d) legacy `UnlockPopulation` placeables still unlock; (e) zones `res-high`/`com-high`
  are locked in the palette until Small Town.

**Phase 2 - Map tiles + development tree (4-5 days).** `TileGrid`, `CityBuyTile`, tile overlay, price/upkeep;
dev tree yaml + `CityUnlockNode` + Progression panel.
- *Accept:* (a) roads/zones/placement/growth on unowned cells rejected (order returns no effect, no funds spent);
  (b) buying a non-adjacent tile fails; price follows the formula (table test for owned=0,10,36,72);
  (c) tile upkeep appears under `tiles` in the budget; (d) tree lint: total cost <= total DP, no cycles, every
  `Unlocks` target exists; (e) buying a node with insufficient DP is rejected; unlocked actor becomes placeable.

**Phase 3 - Districts and policies (5-6 days).** `DistrictLayer`, paint tool, overlay, `DistrictStats`, `PolicyState`,
policies yaml with a first batch of 8 policies wired to existing systems (power/water use, tax/happiness, upkeep).
- *Accept:* (a) painting then erasing leaves no ghost cells; (b) district stats sum to city totals
  (pop, jobs) within 0 error when every cell is in some district; (c) `Energy saving` lowers `PowerConsumed`
  only for buildings inside the district; (d) policy upkeep charged monthly; (e) save/replay reproduces district + policy state.

**Phase 4 - Agent-driven demand (after domains 1 and 5 land; 3-4 days).** Fill `DemandInputs` from the
citizen/company sims; add per-education labour, free-property per density, goods/stock factors; keep the old path
behind a yaml flag `DemandSource: Aggregate | Agents` until the new one is calibrated.
- *Accept:* removing all schools lowers the Office labour factor within 3 months; a starved commercial area
  (no industry) shows `Goods available` negative and Industrial demand positive; ignoring tax at 30% drives R/C/I/O negative within 2 months.

**Phase 5 - Tourism, hotels, landmarks, achievements (5-6 days).** Attractiveness, tourist arrivals, hotels, landmarks,
tourism info view, achievements panel.
- *Accept:* building one landmark raises `Attractiveness` and tourists/month; with no hotel, `Hotels` factor goes positive,
  hotels spawn, occupancy rises; tourist spending shows in company income; `Welcome One and All` unlocks at 10,000 visits.

**Phase 6 - Polish and balance.** Service district restriction, remaining policies, calibration runs on the
three maps, tutorial hints ("zone more commercial": clicking a factor chip highlights the cause).

## 6. UI and art needs

- **Widgets (F):** `DemandFactorsWidget` (tooltip list, sign-coloured chips, sorted), top-bar XP bar and DP/permit chips,
  `MilestonePopupLogic`, `ProgressionPanelLogic` (milestones list + dev tree boxes with lock/cost/state),
  `TileOverlay` renderer + hover price label + buy button, `PoliciesPanelLogic` (city/district tabs, toggles, slider),
  `DistrictToolLogic` (palette entry, list, rename text field, per-district stats panel), `StatsPanelLogic` with a simple
  line-graph widget (`CityGraphWidget`), `AchievementsPanelLogic`, Tourism info view + legend.
- **Art (A3, procedural via genui.py):** D2k-style 34x35 order icons for: districts, policies, progression/tree, statistics,
  tiles/permits, tourism, achievements (normal/disabled/active); chrome icons `xp`, `dev-point`, `permit`, `hotel`, `tourist`;
  16-colour district overlay frames (`overlays: district`, 16 frames) plus a thick border frame; `tile-locked` and
  `tile-owned-border` overlay sequences; milestone badge (20 small star/rank glyphs); tree node boxes (normal/locked/bought).
- **World art (A2):** `hotel-1..3`, 4 landmark sprites (tower, statue, water park, castle), tourist/bus sprites optional.
- **Fluent:** `demand-factor-*` (about 30 keys), `milestone-*`, `policy-*`, `node-*`, `achievement-*`.

## 7. Sources

CS2 progression and milestones: https://cs2.paradoxwikis.com/Progression (rewards, DP, permits, unlocks, tier costs 1/2/4/8, map tile cost components);
dev diary https://colossalorder.fi/news/development-diary-10-game-progression/ and https://www.paradoxinteractive.com/games/cities-skylines-ii/features/game-progression (passive XP 16x/day, active XP, trees);
XP thresholds and per-building XP (player log, **[L]**): https://endlessvolo.com/blogs/navigating-the-cities-skylines-2-milestones.html, https://en.namu.wiki (CS2 milestone page);
development tree: https://www.dexerto.com/gaming/cities-skylines-2-development-trees-unlockables-and-costs-2349659/, https://indiegameculture.com/uncategorized/cities-skylines-2-development-nodes-progression-guide/, https://gamerant.com/cities-skylines-2-every-development-tree-and-what-to-buy-first/;
tile upkeep: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/tile-upkeep-explained, https://steamcommunity.com/app/949230/discussions/0/4406291330029429131/;
policies and districts: https://cs2.paradoxwikis.com/Policies, https://www.paradoxinteractive.com/games/cities-skylines-ii/features/city-services-districts-policies;
demand: https://forum.paradoxplaza.com/forum/threads/elucidation-of-every-demand-factor.1855666/ (factors/thresholds, via search summary), https://github.com/Infixo/CS2-RealEco (vanilla commercial neutral points), https://gameranx.com/updates/id/502137/article/cities-skylines-2-overhauls-economy-with-massive-new-patch/ (Economy 2.0 household factors), https://cs2.paradoxwikis.com/Zoning, https://cs2.paradoxwikis.com/Economy (tax -10..30);
tourism: https://cs2.paradoxwikis.com/Tourism, https://www.thegamer.com/cities-skylines-2-how-to-increase-attract-tourism/, https://www.modscities2.com/cities-skylines-2-leisure-and-tourists/;
achievements: https://www.escapistmagazine.com/all-achievements-cities-skylines-2/.

Uncertain items: exact XP thresholds and XP per action, passive XP formula, tile price multiplier,
`ZoneSpawnSystem` probabilities, exact demand coefficients (we use our own tuned integers), attractiveness
scale. All are yaml-tunable in this design.
