# 07 - City services (design research for OpenCity)

Domain 7 of the CS2-parity research. Scope: electricity, water and sewage, garbage, healthcare and deathcare, education, police, fire and rescue, parks and leisure, communications (telecom, post), administration, service vehicle dispatch, upgrades, efficiency and budget.

Conventions in this document:
- **[W]** = number taken from the CS2 wiki building tables (`cs2.paradoxwikis.com/Service_buildings`, "last verified for 1.0", later patches may differ).
- **[G]** = number from a community guide or forum post (about 1000 days old at time of research, treat as approximate).
- **[?]** = no reliable source found; value is an OpenCity design choice or an estimate.
- CS2 sizes are in 8 m cells; OpenCity cells are 32 px. Do not map CS2 footprints 1:1 (a CS2 hospital is 23x10 cells, ours will be 3x3).
- CS2 money is shown with a currency symbol; we use dollars and scale to OpenCity's economy (start funds $70,000, road cell $10, monthly upkeep in the hundreds).

---

## 1. How CS2 does city services

### 1.0 The shared service model

CS2 services combine two layers (Paradox "Feature Highlight #5: City Services", wiki "Services"):

1. **Passive coverage** ("immediate area effect"). A service building has `Range` (max distance in meters **along roads**), `Capacity` (rough number of people it can cover) and `Magnitude` (max effect). Effect is strongest at the building and falls off with road distance. Examples: clinics and hospitals give a passive Health boost, fire houses lower fire hazard, police stations lower crime accumulation, parks give leisure/well-being, telecom gives signal strength. Dense housing "uses up" capacity faster than suburbs. Imported services (from an Outside Connection) have **no** passive effect.
2. **Active coverage**. Vehicles and citizen visits: ambulances, hearses, garbage trucks, fire engines, patrol cars, post vans, park maintenance vehicles; citizens walking or driving to a clinic, school, park. The vehicle count is a building stat (fleet), and it is what actually answers a request.

So **capacity + fleet + road travel time** decide outcomes, not a circle on the map. A service with too few vehicles, or vehicles stuck in traffic, fails even when "covered".

Common rules:
- **Budget slider per service**, 50% to 150% [W]; efficiency is not linear: 50% gives 25%, 150% gives 125%. It scales upkeep, fleet, processing speed, capacity and sometimes range. Advice: stay near 100%, trim garbage/parks, never emergency services [G].
- **Efficiency of a building** also depends on: electricity and water (large penalty if missing), employees (staffed, happy, educated enough), required resources (fuel for plants, pharmaceuticals for medical), service budget. [W: "Services" page]
- **Service fees**: electricity, water and sewage, healthcare, garbage, education, transport. Lower fee gives more consumption and more happiness; higher gives less. For electricity/water: each 1% below 100% adds 0.2% consumption and +0.05 happiness; each 1% above takes 0.4% and 0.1 happiness [W]. Default fees: electricity 0.2, water 0.3, garbage 1.0, healthcare 100 per hospitalised citizen per month, education 50/100/200 per student per month (elementary/high school/college and university) [W: Economy].
- **Outside connections** can sell or lend services: electricity and water (pipes/lines), ambulances, hearses, police, fire engines (slow, no passive effect), students (university). "Excess output is always sold, while keeping enough for the city" [Paradox].
- **Districts** can restrict which buildings serve which area. Passive coverage stays local.
- **Upgrades come in three kinds** [Paradox]: operational upgrades (no visual change, e.g. exhaust filter, dispatch centre, library), extensions (visible, attached; more capacity or fleet, e.g. ambulance depot, extension wing, extra turbine), sub-buildings (separate placed buildings on the lot, e.g. fuel storage, playground, children's clinic). Upgrades cost money and raise upkeep.
- **Info views**: one per service (33 in total): grid/water pipes coloured by load and pollution, roads or buildings green-red by coverage, garbage accumulation, crime probability, fire coverage [W: Info views].

### 1.1 Electricity

CS2 has a real grid: buildings connect to low-voltage cables along roads, plants to high-voltage lines, **transformer stations** convert between them. Every line has a maximum capacity, so a district can black out while the city has enough total power. Consumption rises with city size and temperature. Solar output follows time of day (zero at night) and the plant has a 50 MWh battery [W]; hydro depends on water flow; geothermal needs a groundwater deposit and ruins it for drinking. The **Emergency Battery Station** (500 MWh, 200 MW, 150K) charges on surplus [W].

| Plant | Output | Cost | Upkeep/month | Notes |
|---|---|---|---|---|
| Wind turbine (15x15) | 6 MW | 25K | 7.5K | no fuel, noise [W] |
| Small coal (14x16) | 20 MW | 100K | 5K | coal, 37 workers [W] |
| Coal (50x40) | 300 MW | 1.0M | 75K | coal, 140 workers, heavy air pollution [W] |
| Gas (27x38) | 250 MW | 650K | 300K | petrochemicals [W] |
| Geothermal (44x35) | 150 MW | 750K | 150K | groundwater deposit [W] |
| Solar (36x69) | 200 MW peak | 1.5M | 150K | day only [W] |
| Hydro (6x9) | varies | 1.57M | 50K | flowing water [W] |
| Nuclear (72x41) | 750 MW | 5.0M | 500K | uranium, 300 workers, 400K water [W] |

Upgrades [W]: additional turbine (+100 MW), exhaust filter (-50% air pollution), advanced furnace (fuel x0.85), fuel storage, battery banks (+250 MWh). Buildings without power lose well-being; companies and services lose efficiency.

### 1.2 Water and sewage

Two sources: **surface water** (pumping station on river/lake/sea; can be polluted) and **groundwater** (deposits on every map, slow replenishment, "very susceptible to ground pollution", can run dry). The **water tower** works anywhere with limited capacity and high upkeep. Pipes auto-connect along roads and have no capacity limit. Sewage goes to a **sewage outlet** (cheap, dumps 100% polluted water into open water, possibly upstream of your pumps) or a **wastewater treatment plant** (cleans it, returns it to the fresh network).

| Building | Capacity | Cost | Upkeep/month |
|---|---|---|---|
| Water pumping station (4x6) | 100,000 | 25K | 5K [W] (10K in a launch guide [G]) |
| Groundwater pumping station | 75,000 | 40K | 20K [G] |
| Advanced pumping station (11x21) | 1,000,000 | 240K | 47.5K [W] |
| Sewage outlet (2x2) | 100,000 | 25K | 10K [W] |
| Wastewater treatment plant | about 4x outlet | 400K | 120K [G] |

Missing fresh water: unhappy citizens, health loss, companies' efficiency "decreases greatly". Backed-up sewage hurts health. Default water fee 0.3 [W]. Per-citizen consumption: [?].

### 1.3 Garbage

"Every building accumulates garbage as a by-product", by building and zone type, level, size and residents' education (higher level and better educated give less). Since patch 1.1.5 the amount counts students, workers, prisoners, occupants and homeless [G]. Trucks **reserve capacity for the expected load** and **skip buildings with minor build-up** [G: PCGamesN]. Uncollected garbage lowers health and well-being, shows "garbage piling up" and eventually drives residents and businesses out.

| Facility | Storage | Trucks | Processing | Cost | Upkeep |
|---|---|---|---|---|---|
| Landfill (17x15) | 100 t | 20 | 100 t/month | 70K | 15K [W] |
| Incineration plant (25x27) | 3 kt | 50 | 3000 t/month, +40 MW | 1.0M | 105K [W] |
| Recycling center (22x18) | 1.5 kt | 15 | 1500 t/month, makes materials | 880K | 80K [W] |

Upgrades [W]: truck depot (+10 to +25 trucks), hazardous waste unit, recycling unit (-50% pollution), storage extension, extra furnace (+20 MW, +1500 t/month). Emptying a full landfill uses a separate dump truck [G]. Garbage per household: [?].

### 1.4 Healthcare and deathcare

Each citizen has **Health**. Sickness probability falls as Health rises; sickness cuts Health sharply; sick or injured citizens can die (higher risk at low Health). Pollution, sewage problems, uncollected garbage and earlier illness reduce Health; children and teens get a boost, seniors lose maximum Health and die of old age. Sick citizens walk to a clinic or hospital, or an ambulance fetches them; ambulances also serve traffic accidents. No local facility means an Outside Connection serves.

| Building | Patients | Ambulances | Range | Cost | Upkeep |
|---|---|---|---|---|---|
| Small clinic (5x5) | 25 | 3 | 2500 m | 30K | 10K [W] |
| Medical clinic (11x6) | 100 | 5 | 5000 m | 60K | 20K [W] |
| Hospital (23x10) | 500 | 30 | 7500 m | 1.88M | 225K [W] |
| Small cemetery (14x6) | 1500 slots | 8 hearses | n/a | 42K | 12K [W] |
| Cemetery (16x25) | 5000 slots | 15 hearses | n/a | 114K | 27.5K [W] |
| Crematorium (8x10) | 100/month | 10 hearses | n/a | 240K | 22.5K [W] |

Upgrades [W]: clinic ambulance depot (+5 ambulances, 25K) and extension wing (+25 patients, 15K); hospital helipad, trauma center (+15 treatment); cemetery columbarium (+1000), temple (+2 well-being); crematorium incinerator (+50/month), hearse garage. Clinics alone keep average Health at about 50-60%; hospitals push it higher [G]. Medical buildings consume pharmaceuticals; low stock lowers efficiency [W]. Cemeteries give +2 to +3 well-being nearby. Citizens die of old age, poor health, accidents or collapse; hearses collect bodies.

### 1.5 Education

Five levels: Uneducated, Poorly Educated, Educated, Well Educated, Highly Educated. **Each graduation raises the level by one**: elementary (child), high school (teen), college (teen or adult), university (adult). Children always attend if a place exists; teens and adults compare expected income of study vs work and may skip. Students can fail and re-decide. Higher levels fill better-paid jobs. Elementary schools give nearby families a well-being bonus. If companies cannot fill positions the education service loses efficiency; graduation chance depends on efficiency (budget, libraries).

| Building | Capacity | Cost | Upkeep | Fee/student/month |
|---|---|---|---|---|
| Elementary (18x8) | 1000 | 100K | 12.5K | 50 |
| High school (22x16) | 800 | 300K | 22.5K | 100 |
| College (22x16) | 10000 | 750K | 75K | 200 |
| University (40x28) | 15000 | 1.5M | 100K | 200 |

All [W]. Upgrades [W]: extension wing (+100 to +500 seats, 8K-40K), playground (+1 well-being), library (+0.1 to +0.15 graduation modifier, 85K-250K), sports field.

### 1.6 Police and crime

- Every building has a **crime probability**; police coverage lowers its accumulation. It does **not** create criminals, it only chooses the **target** (random among the highest values). Living in a high-probability area lowers well-being.
- A citizen **becomes a criminal** with low probability that rises as Well-being falls (no power/water, pollution, garbage, no leisure). The welfare office lifts well-being below 50.
- The criminal travels to the target; entering triggers an **alarm**, a patrol car is dispatched after a short delay, and if it arrives before the criminal leaves he is arrested, otherwise he escapes. Idle patrol cars drive and lower crime probability along their roads.
- Arrested criminals fill the station jail; a share is sentenced to prison (or an Outside Connection if none), then reset. Parameter names seen in data mining: `PoliceConfigurationData` (accumulation, tolerance), `CrimeData` (occurrence, recurrence), alarm delay, crime duration, jail/prison time. Values [?]. Players report runaway criminal counts in 2.x, so recurrence needs a cap.

| Building | Range | Cars | Cost | Upkeep | Notes |
|---|---|---|---|---|---|
| Police house (5x5) | 5000 m | 4 | 80K | 8K | [W] |
| Police station (14x18) | 10000 m | 10 | 1.5M | 87.5K | has jail [W] |
| Police HQ (24x20) | n/a | 15 | 2.25M | 150K | 250 workers [W] |
| Small prison / prison | 500 / 2000 cells | n/a | 200K / 600K | 37.5K / 100K | [W] |

Upgrades [W]: garage (+5 cars), helipad, extra cell block (+500).

### 1.7 Fire and rescue

- Each building has a **fire hazard**; stations lower it in their passive coverage along roads. Hazard is higher for industrial/dense buildings and for dry-weather forests. Formula [?].
- Fires start at random weighted by hazard, damage the building over time and **spread** to neighbours and trees. Engines are dispatched by road; an unextinguished building burns down or collapses. Helicopters handle forest fires and road-less targets; firewatch towers lower forest-fire risk.
- Collapsed buildings trap citizens; engines with a **disaster response unit** search for survivors. Disasters use an early warning system, emergency shelters and evacuation buses.

| Building | Range | Engines | Cost | Upkeep |
|---|---|---|---|---|
| Fire house (5x5) | 5000 m | 4 | 120K | 10K [W] |
| Fire station (14x18) | 10000 m | 10 | 2.15M | 117.5K [W] |
| Helicopter depot | n/a | 5 helis | 1.22M | 72.5K [W] |

### 1.8 Parks and recreation

Citizens have a **leisure** need served by parks, plazas, sports venues, attractions, shops and bars. Weather picks outdoor (sunny) or indoor (rain, cold). Parks raise **land value** and well-being; every park has **attractiveness** which draws tourists. Park maintenance vehicles keep parks in shape, and maintenance scales efficiency. Examples [W] (cost, upkeep, attractiveness): small park 10K, 2K, +1; small plaza 20K, 4K, +5; large city park 42K, 8.5K, +10; large plaza 56K, 12.5K, +15.

### 1.9 Communications

- **Post**: mail is generated by citizens, workplaces and services. Vans collect from mailboxes (homes) and directly at shops and industry. Post office: 20 vans, 15000 mail, 250K; small post office 4 vans, 75K; **sorting facility**: 5 trucks, 500K storage, 50K/month, 650K, batches of 25K [W/G]. Global mail leaves via outside connections. Good mail service gives a well-being bonus.
- **Telecom**: each citizen, worker or student uses 1 Gbit/s [Paradox]. Radio mast 1 km, 3000 Gbit/s, 25K; telecom tower 5 km, 20,000 Gbit/s, 125K; server farm 2 km, 30,000 Gbit/s, 200K [W]. Signal falls with distance. Effects: leisure/well-being, commercial sales, industrial input cost.

### 1.10 Administration

Welfare office (6x6, 100K, 20K upkeep): boosts well-being when happiness is below 50. City hall (12x12, 300K, 50K): lowers loan interest, import costs, crime and building costs. Central bank (8x8, 400K, 60K): lowers interest and import costs, raises export profit [W].

---

## 2. Current OpenCity state and gaps

Source files read: `Traits/Buildings/ServiceBuilding.cs`, `UtilityProducer.cs`, `CityBuilding.cs`, `Traits/World/CityCoverageLayer.cs`, `Traits/Simulation/CityManager.{Daily,Economy,Stats}.cs`, `Traits/Player/CityManager.cs`, `Traits/World/TrafficManager.cs`, `Traits/World/InfoViewLayer.cs`, `rules/services.yaml`, `CityTypes.cs`, `CONTRACT.md`.

### 2.1 What exists

- **Services are radius stamps.** `ServiceBuilding` has only `Service` (Police, Fire, Health, Education, Parks), `Radius` (cells) and `Strength`. `CityCoverageLayer.Recompute()` every 100 ticks stamps a linear falloff (`Strength * (R-d)/R`, Euclidean distance from the footprint, ignores roads) into five `byte[]` maps, combined as `a + b - a*b/100`. Needs `IsOperational` and `HasPower`.
- **Coverage feeds happiness and land value**: `UpdateHappiness` adds police 12%, fire 10%, health 12%, parks 15%, education 8% (residential), minus pollution 30%; land value uses parks 0.30, police+fire+health 0.10, education 0.12, pollution -0.40.
- **Utilities are two city-wide pools.** `DistributeUtilities()` sums `UtilityProducer.Power/Water` of operational buildings with road access, then serves consumers in `ActorID` order if the remaining pool covers the full use. Scaled use is 25%..100% by occupancy. No network topology, no capacity per line, no storage, no pollution of water, no fuel.
- **Pollution** is a single static `CityBuilding.Pollution` stamped with radius 6 (coal plant 70). One map for air/ground/noise.
- **Upkeep** is a flat monthly number per `CityBuilding.Upkeep`, charged only while operational (`serviceUpkeep` in `FillJobsAndCollect`). No budget slider; no per-service accounting (single key `upkeep-services`).
- **Services have no vehicles, no queues, no citizens served**. `ServiceBuilding` has no `Tick`. `MaxJobs` of services counts into city jobs (police 12, clinic 15, school 15) but staffing never affects output.
- **Catalog (12 actors)**: coal 300, wind 12, solar 90, water tower 90, pump 300, police/fire/clinic/school 2x2 (radius 12-14), three parks (r4-8). Costs $300-$12,000, upkeep $10-$400.
- **UI**: info views Power, Water, LandValue, Pollution, Police, Fire, Health, Education, Parks, Traffic, Happiness; `WithCityStatusIcons` (no power/water/road, abandoned, unhappy).
- **Traffic**: `TrafficManager` spawns only resident/commuter/freight vehicles (cap `min(200, pop/6)`), BFS over road cells, `Mobile.MoveTo` + `RemoveSelf`, stuck/lifetime cleanup. No service vehicles.
- **Frozen**: `CityTypes.cs` (`CityService` enum has 5 values; `CityInfoView` fixed). Any new service needs the lead to unfreeze it (see section 4).

### 2.2 Gaps versus CS2 (priority order)

| Gap | Impact | Severity |
|---|---|---|
| No per-building needs (garbage, sickness, crime, fire hazard, mail, students) | Services are decoration; nothing to fail | Critical |
| No service vehicles / dispatch | Cannot see services working; no traffic interaction | Critical |
| Coverage = radius circle, ignores roads and capacity | A clinic covers 5000 people as well as 50 | High |
| No sewage / garbage / deathcare / hospital / prison / telecom / post / admin | Half the CS2 service list is missing | High |
| Power/water: single pool, no grid or storage, no pollution propagation | No blackouts by district, no water quality | Medium |
| No budget slider, no efficiency, no service fees | Cannot tune cost vs quality | High |
| No upgrades/extensions | Single-size services only | Medium |
| No staffing/education link | Education has no effect on jobs beyond a coverage number | Medium (owned by citizens/economy docs) |
| No disasters/fire | No risk, no reason for fire stations | Medium |
| Service art is 1x1/2x2 only | Cannot show hospital/university/prison scale | Medium |

---

## 3. Proposed design for OpenCity

### 3.1 Principles and the time scale that shapes everything

- **Integers only, fixed order.** All service state is `int`/`long`, iterated in `ActorID` (`SortId`) order, random only via `world.SharedRandom` or `CityManager.Hash(sortId, day)`. Fractions are kept as milli-unit accumulators (no floats).
- **Time scale (measured from current code).** 1 day = 25 ticks = 1 s at speed 1; 1 month = 750 ticks = 30 s. A car (`Mobile.Speed 96`, 1024 per cell) needs about 10.7 ticks per cell, so a 30-cell trip is 320 ticks, about 13 days. Service vehicles get `Speed 140` (7.3 ticks/cell; emergency 170). Consequences: we cannot dispatch one truck per building (1,400 buildings in a 10k city). **Route sweeps** (one truck clears a street segment) and **batched patients** are mandatory, and fire/crime windows are in ticks (below), not days.
- **Needs are data, vehicles are the answer.** Every building carries accumulators; services are queues that drain them. Where a vehicle cap is hit, the same request is resolved by a **virtual vehicle** (timer from road distance), so outcomes never depend on how many actors exist.
- **Aggregate now, citizens later.** Needs are computed from `Residents`/`Workers`/zone/level. When the citizen module (domain 1) exists it supplies per-person values through the interface in section 4; the formulas stay, only the inputs get finer.
- **No new engine order types needed in frozen files.** `CityTypes.cs` stays frozen. New enums live in `Traits/Services/ServiceTypes.cs`; new orders (`CitySetBudget`, `CityPlaceUpgrade`, `CitySetPolicy`) are string constants in a new `ServiceOrders` static class and are resolved by the new service trait via `IResolveOrder`.

### 3.2 Architecture (new files under `OpenRA.Mods.City/Traits/Services/`)

| Piece | Kind | Role |
|---|---|---|
| `ServiceType` enum | type | Power, Water, Sewage, Garbage, Health, Deathcare, Education, Police, Fire, Parks, Telecom, Post, Admin. Maps to the legacy `CityService` (Police, Fire, Health, Education, Parks) for the existing coverage maps and info views. |
| `ServiceBuilding` (extend) | actor trait | Existing `Service/Radius/Strength` stay. Add `Type`, `Capacity` (type-specific unit), `Fleet`, `VehicleActor`, `Range` (road cells), `ExtensionSlots`, `StaffRequired`. |
| `ServiceStorage` | actor trait data | Stock held in a facility: garbage kg, bodies, patients, prisoners, students, mail. Lives as plain fields on a per-building `ServiceState` struct inside the simulation (not a trait tick). |
| `ServiceSimulation` | **Player trait** (E) | One daily step after `CityManager.DailyUpdate`: accrue needs, create requests, run dispatcher, advance virtual vehicles, update efficiency, charge upkeep by budget. Owns all per-building `NeedState[]` arrays aligned to `CityManager.buildings`. Deterministic, no per-building `ITick`. |
| `ServiceNetwork` (catchment) | helper inside the simulation | Multi-source road BFS per `ServiceType` (provider id + road distance per road cell). Output: assignment, load, satisfaction, nearest-free-provider. |
| `ServiceVehicle` | actor trait | Lightweight state machine on a vehicle actor: `Idle, ToStop, Working, ToFacility, Unloading, Returning`. Created by the dispatcher via `AddFrameEndTask`. |
| `ServiceExtension` | actor trait | A placeable extension actor (depot, wing, filter) that attaches to a host building and modifies its numbers. |
| `FireSystem`, `CrimeSystem`, `HealthSystem` | partial classes of `ServiceSimulation` | Event generation (fire start/spread, crime attempts, sickness/death). |

Tick schedule (all inside the existing 25-tick day, spread by `world.WorldTick % 25`): tick 0 needs accrual; tick 5 garbage and mail dispatch; tick 10 health/death dispatch; tick 15 education enrolment; every 5 ticks police/fire dispatch and event timers (these need finer timing); every 100 ticks catchment recompute (also when `RoadLayer.NetworkVersion` or a service building changes).

### 3.3 Per-building and per-citizen needs (data)

Rates are per month at "base", accrued daily as `rate*1000/30` milli-units. Growables get them by **formula from zone, level, residents, jobs** defined in a `ServiceRules` config node in `rules/services.yaml`; no edit to `growables.yaml` is required.

| Need | Source | Base rate | Modifiers |
|---|---|---|---|
| Garbage (kg) | Residential | 80 per resident | -8% per level above 1, -4% per average education level; +50% while a building is abandoned |
| | Commercial / Industrial / Office | 60 / 120 / 30 per job | same level modifier |
| | Service buildings | 40 per worker | hospitals x2 |
| Sickness | each resident | 3% per month (about 1000 ppm per day) | x(1 + ground+air pollution/100) x1.5 with no water or sewage x1.3 per 20 days of garbage on the lot; -20% inside clinic/hospital catchment |
| Death (old age) | residents | 0.15% per month base | scales with senior share; citizens module overrides |
| Death (illness) | sick citizen | 3% per sick-month untreated, 0.3% treated | hospital treatment bonus lowers |
| Crime pressure (0..100) | per building | res-low 8, res-high 14, com 18, ind 10, off 12, service 4 | +(100-Happiness)/4, +unemployment/10, -0.6 x police satisfaction, -3 per patrol pass (decays 1/day) |
| Criminal attempts | residents | `max(0, 40-Happiness)` ppm per day per resident | cap 1 attempt per building per 5 days (stops the 2.0-style runaway recurrence) |
| Fire hazard (0..100) | per building | res-low 6, res-high 10, com 12, ind 25, off 8, coal/gas 30, service 8 | +5 without power (candles, generators); +10 next to an abandoned building; coverage lowers by `0.7 x fire satisfaction` |
| Fire start chance | per building per day | `hazard x 1.6 ppm` | target about 0.7 fires per month in a 1,400-building city with no fire cover |
| Mail | per resident / per job | 12 / 20 per month | collected by vans when the building has at least 60% of a van load queued |
| Students | residents by age | child 17%, teen 8%, adult 55%, senior 20% (aggregate fallback) | citizens module gives real ages |
| Staff | service building | `MaxJobs` | filled by existing `FillJobsAndCollect`; staffing ratio scales efficiency |
| Leisure demand | residents | 1 unit/resident | satisfied by park catchment capacity |

### 3.4 Coverage, capacity, catchment (replaces radius circles)

1. For each `ServiceType`, run a **multi-source BFS on road cells** from the access cell of every operational provider (`Range` road cells, default 24 for fire/police/health, 30 for schools, 12 for parks). Each road cell stores `(providerId, distance)`. A building belongs to the provider of its access road cell (nearest, ties by ActorID). Cost: one BFS over at most the road cell count per type per recompute (about 5,000 cells), trivial.
2. `Load(provider)` = sum of the need units of its assigned buildings (residents for health/education, buildings weighted by size for fire/police, kg/month for garbage, mail).
3. `Satisfaction(provider)` = `min(100, 100 x Capacity x Efficiency / Load)`. Building coverage value = `Magnitude x falloff(distance) x Satisfaction / 100`, written into the existing `CityCoverageLayer` byte maps so happiness, land value and info views keep working unchanged. Cells not adjacent to a road take the value of the nearest road cell minus 10 per cell.
4. A **second provider in range** (overflow) takes load from a saturated provider: iterate providers in ActorID order, redistribute buildings farthest-first until each is at 100%. This gives overlapping coverage the meaning "extra capacity".
5. Coverage computed this way also yields the **dispatch catchment**: requests go first to the owning provider, then to any provider in range with a free vehicle.

### 3.5 Dispatch model (requests to vehicles)

1. **Request** = `{Kind, BuildingSortId, TargetRoadCell, Units, Priority, CreatedTick}`. Created when a need crosses its threshold: bin at 60% of one truck load (garbage), mail at 60% of a van, a sick citizen who cannot walk (30% of the sick), a body, a fire start, a crime alarm. Priority: fire 100, crime in progress 90, critical patient 80, hearse 40, garbage 30 + pile%, mail 10, plus `age/50`.
2. **Provider choice**: per kind, every 5 to 25 ticks, run **one multi-source BFS from providers that have a free vehicle and room at the destination facility**. The nearest provider (road distance, ties by ActorID) takes the request if distance is at most 1.5 x `Range`. It **reserves** the vehicle slot and the destination capacity (landfill kg, hospital bed, cemetery slot, jail cell), as CS2 trucks reserve capacity.
3. **Batching**: garbage and mail trucks plan up to 8 stops, greedy nearest-next within 6 cells, until the reserved load fills the truck (6,000 kg; mail van 600 items). Ambulance carries 2 patients, hearse 3 bodies. Fire sends `1 + hazard/40` engines (max 3). Police sends 1 car.
4. **Vehicle life**: spawn at the provider's access road cell, `MoveTo` along the road, `Working` at the stop (garbage 20 ticks, ambulance 25, hearse 25, police 40, fire until extinguished), then go to the nearest facility with room, unload, return, dispose; the fleet slot frees on return. If it makes no progress for 4x the expected travel ticks (road cut, jam) the request returns to the queue and the slot is restored.
5. **Actor cap and virtual vehicles**: at most `min(80, 24 + population/250)` service actors exist. Emergency kinds get actors first. Over the cap, a **virtual vehicle** `{provider, stops, phase, dueTick}` advances by `distance x 7` ticks and applies identical effects, so the simulation result never depends on rendering.
6. **Imported service fallback** (as CS2 outside connections): if a kind has no provider in range, a virtual vehicle arrives from the nearest highway end after `3 x distance` ticks, costs $150 (ambulance), $200 (hearse), $300 (police), $500 (fire) per call, and gives no passive coverage. Keeps a tiny city playable.
7. **Traffic coupling**: service vehicles drive on the same road cells and count in `GetTrafficLoad`. They use `Speed 140/170` and are exempt from stuck cleanup for 600 ticks. A priority-yield rule is requested from the traffic domain (2).

### 3.6 Service catalog (OpenCity)

Existing actors keep their names. "Unlock" is population (`CityPlaceable.UnlockPopulation`). Costs fit the $70,000 start. Capacity units: see section 3.3.

| Service | Actor (size) | Capacity / fleet | Cost | Upkeep/mo | Unlock | New art |
|---|---|---|---|---|---|---|
| Power | `windturbine` (1x1) | 12, gust 50-100% | 1,500 | 60 | 0 | exists |
| | `powerplant-coal` (2x2) | 300, air 70, fuel | 8,000 | 400 | 0 | exists |
| | `solarplant` (2x2) | 90, day curve | 12,000 | 200 | 1,000 | exists |
| | `battery` (2x2) | store 400, 60/day | 15,000 | 250 | 2,500 | new |
| | `powerplant-gas` (3x3) | 450, air 25 | 24,000 | 1,100 | 5,000 | new |
| | `hydro-dam` (3x3, water adjacent) | 350 | 45,000 | 700 | 10,000 | new |
| | `powerplant-nuclear` (3x3) | 2,000, water 40 | 140,000 | 5,000 | 25,000 | new |
| Water | `watertower` (1x1) / `waterpump` (1x1) | 90 / 300 | 1,200 / 2,500 | 60 / 100 | 0 | exists |
| | `waterpump-large` (2x2) | 900 | 9,000 | 300 | 2,500 | new |
| | `wellfield` (2x2, groundwater cell) | 160, ground-pollution sensitive | 4,000 | 150 | 1,000 | new |
| Sewage | `sewage-outlet` (1x1, near water) | 400, dumps pollution r8 | 2,000 | 80 | 0 | new |
| | `treatment-plant` (3x3) | 1,500, cleans 90% | 30,000 | 900 | 2,500 | new |
| Garbage | `landfill` (3x3) | 4,000,000 kg store, 3 trucks, ground 30 | 6,000 | 150 | 250 | new |
| | `incinerator` (3x3) | 400,000 kg/mo, 5 trucks, air 40, +40 power | 55,000 | 1,300 | 2,500 | new |
| | `recycling` (3x3) | 250,000 kg/mo, 4 trucks, +$10/t | 40,000 | 1,000 | 5,000 | new |
| Health | `clinic` (2x2) | 30 beds, 2 ambulances | 5,000 | 300 | 0 | exists |
| | `hospital` (3x3) | 200 beds, 8 ambulances, 60 jobs | 60,000 | 2,500 | 2,500 | new |
| Deathcare | `cemetery` (3x3) | 1,500 slots, 3 hearses | 8,000 | 200 | 1,000 | new |
| | `crematorium` (2x2) | 30/month, 3 hearses, air 15 | 20,000 | 500 | 2,500 | new |
| Education | `school` (2x2) elementary | 120 seats | 6,000 | 350 | 250 | exists |
| | `highschool` (3x3) | 250 | 25,000 | 900 | 2,500 | new |
| | `college` / `university` (3x3) | 400 / 600 | 60,000 / 120,000 | 1,800 / 3,500 | 10,000 / 25,000 | new |
| Police | `policebox` (1x1) | 1 car, jail 2, range 10 | 1,500 | 80 | 250 | new |
| | `police` (2x2) | 4 cars, jail 8 | 4,000 | 250 | 0 | exists |
| | `police-hq` (3x3) | 8 cars, jail 20 | 40,000 | 1,400 | 10,000 | new |
| | `prison` (3x3) | 80 cells | 35,000 | 1,200 | 5,000 | new |
| Fire | `firehouse` (1x1) | 2 engines, range 16 | 2,000 | 100 | 0 | new |
| | `firestation` (2x2) | 4 engines | 4,000 | 250 | 0 | exists |
| Parks | `park-small` / `plaza` / `park-large` | leisure 30 / 40 / 150 | 300 / 500 / 2,000 | 10 / 15 / 60 | 0/0/500 | exists |
| | `sportsfield` (2x2) | leisure 100, attract +3 | 6,000 | 120 | 1,000 | new |
| Comms | `postoffice` (2x2) | 3 vans, 50,000 mail/mo | 9,000 | 350 | 2,500 | new |
| | `sortingcenter` (3x3) | 500,000/mo, 4 trucks | 60,000 | 1,500 | 10,000 | new |
| | `telecom-mast` (1x1) / `telecom-tower` (2x2) | 1,500 / 8,000 users, radius 8 / 20 | 3,000 / 20,000 | 100 / 500 | 1,000 / 5,000 | new |
| Admin | `cityhall` (3x3) | -10% crime, unlocks policies | 30,000 | 1,500 | 2,500 | new |
| | `welfare` (2x2) | +8 happiness below 50, halves crime attempts, range 20 | 12,000 | 700 | 5,000 | new |

Power/water stay city-wide pools in the MVP; the networks domain (3) may add grid topology later. Telecom is the only **radius** (not road-BFS) service because signal crosses terrain; it still uses capacity/satisfaction. Sewage is a pool: demand = 85% of water consumed; overflow gives x1.5 sickness, -10 happiness and water pollution at outlets.

### 3.7 Events: fire, crime, sickness (tick-level rules)

| System | Rule (ticks, integers) |
|---|---|
| **Fire** | Start chance per building per day `hazard x 1.6 ppm`. A burning building has `hp 100`; it loses 1 hp per 5 ticks (burns down in about 500 ticks = 20 s) and each engine on site restores 2 hp per 5 ticks (so 2 engines put out the fire in about 10 s). Every 50 ticks each 8-neighbour ignites with chance `hazard/2 %`. At hp 0 the actor is replaced by rubble (zone stays, regrows), residents of the lot move out and 5% die (hearse requests). Alarm to dispatch delay 25 ticks. Engine speed 170 (6 ticks/cell): 20 cells = 120 ticks = 24% damage, so a station within about 25 cells saves nearly everything. |
| **Crime** | Attempt: pick target from the 5 highest `CrimePressure` buildings in 15 cells (SharedRandom). Alarm after 50 ticks, crime lasts 150 ticks. A patrol car that arrives inside that window arrests (jail +1; if jail full the criminal is released and recurrence +20%). Otherwise the target loses 5 happiness for 10 days and a commercial target loses $50. Patrol: idle cars drive a random route in their catchment, each road cell passed lowers `CrimePressure` of adjacent buildings by 3. Prisoners move to a prison over 10 days; sentence 40 days; released inmates reset. |
| **Sickness** | Daily sick accrual (3.3). Patients occupy a bed for 6 days (150 ticks); cured, or 3% / 0.3% die per sick-month (untreated / treated). No free bed in catchment: treatment goes to the next hospital, else untreated. Dead bodies request a hearse; if a body waits more than 20 days (500 ticks) happiness -8 and sickness x1.5 on adjacent buildings, status icon "dead body". |
| **Garbage pile** | Uncollected bin above 100% of a truck load: happiness -5; above 300%: sickness x1.3, land value -10 and a "garbage piling up" icon; 60 days uncollected on a residential lot starts `Abandoned` countdown (shares D's abandon rule). |
| **Landfill full** | Storage at 100%: trucks queue, all garbage piles up; processing slowly reclaims space. Notification `notification-service-landfill-full`. |

Disasters-lite: only **fires** (random, hazard-driven) and **collapse** (abandoned building). Optional later: `blackout` (power plant lost), `wildfire` on tree cells with firewatch tower. No earthquakes or floods.

### 3.8 Efficiency and the budget slider

- `Efficiency (0..125) = BudgetEff x StaffFactor x UtilityFactor`. `StaffFactor = max(40, 100 x Workers / MaxJobs)`, `UtilityFactor` = 100 with power and water, 50 without water, 0 without power (matches current `HasPower` gating).
- **Budget slider** per `ServiceType` (13), 50..150 in steps of 5, default 100. `BudgetEff` = `25 + 1.5 x (b-50)` below 100, `100 + 0.5 x (b-100)` above (CS2: 50 gives 25, 150 gives 125). Upkeep multiplier = `b/100`.
- Efficiency scales: usable fleet `ceil(Fleet x eff/100)`, capacity and processing speed `x eff/100`, passive magnitude `x (50 + eff/2)/100`, graduation chance, treatment bonus.
- New order `CitySetBudget` (ExtraLocation.X = `ServiceType`, ExtraData = percent), resolved by `ServiceSimulation`. Per-service expenses reported as `upkeep-<service>` keys for the budget panel.
- Utility fees (electricity, water, garbage) are a later economy-domain knob; hook: `ServiceSimulation.FeePercent[ServiceType]` scaling consumption and happiness like CS2 (+0.2% consumption per 1% below 100).

### 3.9 Upgrades and extensions

**In-place upgrades**, not separate placed footprints (no new placement rules, no extra art beyond an overlay). Each `ServiceBuilding` lists up to 3 `Upgrades` in YAML (`Name, Cost, Upkeep, AddFleet, AddCapacity, AddEfficiency, Overlay frame`). The building info panel offers "Buy". Order `CityBuyUpgrade` (Subject = player actor, `TargetActor` = building, ExtraData = index).

| Host | Upgrades (cost / upkeep add) |
|---|---|
| clinic | ambulance depot +3 ambulances (3,000 / 120); wing +20 beds (2,500 / 100) |
| hospital | trauma center +15 treatment (15,000 / 600); helipad (phase 6) |
| police / hq | garage +2 cars (3,000 / 100); cell block +6 jail (4,000 / 120) |
| firestation | engine bay +2 engines (3,000 / 120) |
| school / highschool | wing +60 / +100 seats (2,500 / 80); library +10% graduation (6,000 / 150) |
| landfill | truck depot +3 trucks (3,000 / 80); recycling unit -50% ground pollution (4,000 / 100) |
| incinerator | furnace +200,000 kg/mo and +40 power (30,000 / 700) |
| powerplant-coal/gas | exhaust filter -50% air pollution (4,000 / 150); extra turbine +100 power (10,000 / 400) |
| sewage-outlet | chemical purification, pollution x0.5 (3,000 / 60) |
| cemetery | columbarium +500 slots (2,000 / 40) |

### 3.10 Info views per service

Extend `CityInfoView` (frozen enum, lead must add): `Garbage, Deathcare, Telecom, Post, Welfare`. Existing views keep meaning but read from the new catchment data: `Power`/`Water` (building green/red, plus water pollution overlay), `Health` (road/cell coverage green-red and sick count), `Police` (building crime pressure), `Fire` (hazard, burning cells flash), `Education` (seat usage), `Parks`. While a service view is active the provider buildings and their vehicles get an outline, and the selected provider draws its catchment edge. Data API: `ServiceSimulation.GetOverlay(CityInfoView, CPos)` returning 0..100 (cached per `Version`).

---

## 4. Interfaces with other domains

| Domain | We need from them | We provide |
|---|---|---|
| **1 Citizens** | Per-building age buckets, health, education level, criminal flag, deaths (`ICitizenServiceSource`); until it exists we derive from `Residents` with the fixed 17/8/55/20 split. | `TryEnroll(citizen, level)` returns seat or none; `Treat(citizen)`; `GetSatisfaction(type, cell)` (0..100); graduation, death and arrest events; leisure and mail satisfaction for well-being. |
| **2 Traffic** | A road distance and route API (`RoadRouter.TryRoute(from, to, path)`) or permission to reuse `TrafficManager.FindPath`; `RegisterServiceVehicle(actor)` (load counted, stuck exemption, optional yield rule). | Six new vehicle actors; a per-tick count of active service vehicles; accident hook `ReportAccident(cell)` for ambulances (phase 6). |
| **3 Networks** | Keep `HasPower/HasWater` per building (district blackouts allowed); groundwater and water-pollution fields (`GetGroundwater(cell)`, `GetWaterPollution(cell)`); optional grid nodes. | `UtilityProducer` extended (`Variable`, `Storage`, `Sewage`, `Fuel`); `SewageDischarge(cell, amount)`; pollution sources (air/ground/noise as separate channels); incinerator power output. |
| **5 Economy** | Fee/tax hooks; job education requirements; loan interest for the city hall effect. | Monthly upkeep by service (`upkeep-<service>`), imported-service charges, recycling income, garbage per company (efficiency input), `Efficiency` and `StaffFactor` per service. |
| **10 UI** | Budget panel with 13 sliders, building info panel (capacity, fleet used/total, storage bar, efficiency, upgrades, catchment toggle), toolbar categories, notifications. | `GetOverlay`, `GetRequestQueue(type)`, `GetFleetStatus(building)`, status-icon flags, fluent keys. |
| **Lead** | Unfreeze/extend `CityService` and `CityInfoView` (or accept the new `ServiceType` enum and the five extra info views); register `ServiceSimulation` in `rules/player.yaml`. | `rules/services.yaml` and `fluent/rules-services.ftl` content. |
| **C (construction)** | New `CityPlaceable.Category` values `garbage`, `deathcare`, `comms`, `admin`; `NearbyRange` for hydro. | None. |

---

## 5. Phased tasks and acceptance tests

All acceptance tests run headless via `mods/city/tools/autotest.sh` with new scenarios (`scenario=services`, `fire`, `garbage`) logging a `ServiceHash` (hash of all need arrays, queues and fleet states) every 250 ticks, and `./utility.sh city --check-yaml` ending in `Errors: 0`.

| Phase | Tasks | Acceptance tests |
|---|---|---|
| **P1 MVP** (core, garbage, fire, budget) | T1 `ServiceTypes`, `ServiceSimulation` skeleton, need accrual, `ServiceRules` config, `CitySetBudget`, per-type upkeep. T2 road-BFS catchment, capacity/satisfaction, writes into `CityCoverageLayer` for the legacy 5. T3 request queue, dispatcher, virtual vehicles, imported fallback. T4 garbage chain: `landfill`, `incinerator`, truck actor, bins, pile effects. T5 fire: hazard, ignition, burn, spread, engines, `firehouse`, rubble. | **A1 determinism**: two runs, same seed, identical `ServiceHash` at every sample; replay equal. **A2 garbage conservation**: generated = collected + stored + piled within 1%. No landfill: after 60 days at least 50% of buildings above 100% pile and average happiness at least 5 below control. With `landfill` and 2 trucks: pile below 20% within 90 days. **A3 fire**: forced ignition 20 cells from a firestation: building keeps hp at least 60; no station: burns down by tick 520 and at least one neighbour ignites in 20 runs. **A4 capacity**: 100 sick, one 30-bed clinic: occupancy never above 30, satisfaction 30%; second clinic raises satisfaction. **A5 budget**: 50% halves upkeep and fleet; 150% raises fleet by 25%. **A6 caps**: active service actors never exceed `min(80, 24 + pop/250)`. |
| **P2** health, deathcare, police | `hospital`, `cemetery`, `crematorium`, `policebox`, `police-hq`, `prison`; sickness, death, crime systems; ambulance, hearse, patrol car; in-place upgrades; info views Health/Police/Fire/Garbage. | Patient flow: beds never exceeded; bodies never above storage plus piled; body waiting 20 days applies the penalty. Crime: with no police, crime events per month at least 3x the covered case; arrest rate above 60% with 4 cars within 15 cells; jail overflow releases criminals. Upgrade buy changes fleet and upkeep exactly by the table. |
| **P3** education, parks, comms, admin | `highschool`, `college`, `university`, `sportsfield`, `postoffice`, `sortingcenter`, `telecom-*`, `cityhall`, `welfare`; enrolment, graduation; leisure and mail satisfaction. | Seats never exceeded; graduates at least 55% of enrolled after the course length; an educated ratio rise feeds `EducatedRatio` and office demand; `welfare` lifts happiness below 50 by 8 inside range; telecom satisfaction follows capacity. |
| **P4** utility depth (with domain 3) | `battery`, `powerplant-gas`, `hydro-dam`, `powerplant-nuclear`, `wellfield`, `waterpump-large`, `sewage-outlet`, `treatment-plant`; gust and solar curves; sewage pool; water pollution. | Battery shifts at least 70% of a surplus to a deficit window; sewage overflow raises sickness 1.5x; outlet upstream of a pump marks the pump polluted; efficiency respects `UtilityFactor`. |
| **P5** polish | park maintenance, helicopters, wildfire, fees, districts, accident hook, sirens. | No regression in A1..A6; performance: `ServiceSimulation` daily step under 2 ms at 1,500 buildings (stopwatch log). |

Rollout rule: P1 must land alone (it changes coverage computation), with a switch `ServiceSimulation: LegacyCoverage: true` to compare against the radius model in the autotest for one release.

---

## 6. UI and art needs

- **Buildings** (`tools/genworld.py`, A2): 2x2 frames 64x96 as today; new **3x3 frames 96x128**, `Offset: 0,-16`, 1 frame each (animated: incinerator smoke, hydro water, nuclear steam, battery blink). New sprites: `battery, powerplant-gas, hydro-dam, powerplant-nuclear, waterpump-large, wellfield, sewage-outlet, treatment-plant, landfill, incinerator, recycling, hospital, cemetery, crematorium, highschool, college, university, policebox, police-hq, prison, firehouse, sportsfield, postoffice, sortingcenter, telecom-mast, telecom-tower, cityhall, welfare` (28). Build icons (48x48) for each in `city-buildicons`.
- **Upgrade overlays**: 1 to 2 frames per host (ambulance bay, wing, garage, filter, stack), drawn on top of the host.
- **Vehicles** (`vehicles.yaml`, 32 facings, 32x32): `ambulance, firetruck, policecar, garbagetruck, hearse, postvan`, with a 2-frame siren flash variant for emergency types.
- **Overlays/effects**: animated `fire` (4 frames 32x32 + a smoke column), `rubble`, burning-building tint, `garbage-pile` sprite on the lot (3 sizes), body marker.
- **Status icons** (16x16, extend `statusicons`): garbage piling, dead body, fire, crime, no staff, storage full, no hospital bed.
- **Chrome icons** (32x32 `city-icons`, A3): `garbage, deathcare, comms, admin, sewage, budget-slider` plus toolbar categories; legends for the new info views.
- **Panels** (F): budget panel with 13 sliders and effects readout; building info panel with capacity bars, fleet X/Y, storage, efficiency, upgrade buttons; request queue list per service in the budget panel; notifications `notification-service-*`.
- **Sound** (`gensfx.py`): siren (police/fire/ambulance), truck beep, fire crackle (loop near burning building).

---

## 7. Sources

- Paradox, Feature Highlight #5 City Services: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/city-services-districts-policies
- Paradox, Feature Highlight #6 Electricity & Water: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/electricity-water (forum copy: https://forum.paradoxplaza.com/forum/threads/development-diary-6-electricity-water.1593173/)
- Paradox, Feature Highlight #11 Citizen Simulation & Lifepath: https://www.paradoxinteractive.com/games/cities-skylines-ii/features/citizen-simulation-lifepath
- CS2 wiki, Service buildings (all [W] numbers): https://cs2.paradoxwikis.com/Service_buildings (read via `?action=raw`; water section absent from the page)
- CS2 wiki: https://cs2.paradoxwikis.com/Services , /Economy , /Citizens , /Info_views , /Education , /Police , /Healthcare , /Deathcare , /Garbage_Management , /Electricity
- Guides (garbage, power, healthcare, deathcare, water, education, coverage, mail): twinfinite.net/guides/cities-skylines-2-garbage-guide, pcgamesn.com/cities-skylines-2/logic-and-behavior-garbage-trucks, steamcommunity.com/sharedfiles/filedetails/?id=3068846564, old.strateggames.com (power plants), thegamer.com (healthcare, deathcare, water, education guides), chillplacegaming.com/city-services-cities-skylines-ii, gamesradar.com/cities-skylines-2-money, steamcommunity.com/app/949230/discussions/0/634541741925503535/ (mail).
- Crime/fire parameter names only: github.com/TurboRyder/Realistic-Crime-Healthcare-and-Fire-Burden-CS2
- Repo files read: `mods/city/CONTRACT.md`, `OpenRA.Mods.City/Traits/Buildings/{ServiceBuilding,UtilityProducer,CityBuilding}.cs`, `Traits/World/{CityCoverageLayer,TrafficManager,InfoViewLayer}.cs`, `Traits/Simulation/CityManager.*.cs`, `rules/{services,simulation,traffic}.yaml`, `tools/genworld.py`.

**Uncertainty summary.** Exact CS2 formulas for crime, fire hazard, garbage per household, sickness rates and per-citizen water/electricity use are not public (marked [?] or design choices). Wiki prices date from version 1.0 and several guides conflict with the wiki (water pumping upkeep 5K vs 10K, recycling 650K vs 880K); OpenCity numbers in section 3 are balance proposals, to be tuned in P1 with the autotest logs.
