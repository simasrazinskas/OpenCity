# OpenCity: CS2 simulation architecture (wave 1)

This document turns OpenCity's aggregate simulation into a Cities: Skylines 2-style agent
simulation: individual citizens and households, a mesoscopic traffic sim, companies and production
chains, freight, capacity-based services, and road and utility networks.

It is **binding** for all work packages (WPs) of this wave. Read it **together with**:
- `mods/city/CONTRACT.md`: older, but its rules on ownership, determinism, the test harness and
  code style still apply unless overridden here;
- your WP's design doc in `mods/city/design/` (the research behind each system, with formulas,
  numbers and phased plans);
- the shared C# contracts in `OpenRA.Mods.City/CityInterfaces.cs`.

The game must stay playable after every merge. Each system degrades gracefully when another one is
missing, which today is the legacy aggregate behaviour.

---------------------------------------------------------------------------------------------------

## 1. Global decisions (override the design docs where they disagree)

1. **Time: `CityClock`** (implemented, `Traits/World/CityClock.cs`)
   - 100 ticks per game hour; 2,400 ticks per day.
   - **One day/night cycle is one calendar month** (as in CS2). That is 96 s at speed 1, 24 s at
     speed 3, and a year takes 19 minutes at 1x.
   - API: `Hour`, `Minute`, `MinuteOfDay`, `DayIndex`, `Month`, `Year`, `Season`, `IsNewDay`,
     `IsNewHour`, `IsPulse` (every 25 ticks), `TicksPerDay`, `IsDaytime`.
   - Citizens use the hour for schedules (work 8–17, shifts, rush hours). Traffic uses real ticks.
   - **Ageing**: one citizen year per game day (month), so a lifespan is about 80 days, roughly
     2 h at 1x or 32 min at 3x. Tunable in yaml.
   - `CityManager`'s legacy 25-tick "day" is now a **pulse**. Monthly settlement (taxes, upkeep)
     happens when the clock day changes (already wired).
   - **Calibrate every rate to the clock.** A "per day" rate in a design doc that assumed 25-tick
     days usually means "per pulse" (96 pulses per day). Use whichever gives sensible real-time
     behaviour, and document the choice.

2. **Scale.** 1 cell ≈ 16 m, i.e. 2×2 CS2 cells. Roads are 1 cell wide. Zone depth on the grid is
   3 cells for lots, with the paintable band staying at 4. Lots are 1×1 to 4×3.

3. **Data, not actors.** Citizens, households, companies, shipments, trips, service requests and
   most vehicles are **plain data** (struct arrays or pooled arrays) inside world/player traits.
   - Only buildings, trees and map markers are actors.
   - Vehicles are drawn by renderers from sim state.
   - The only vehicle actors allowed are a small hash-sampled set, if a WP really needs them.

4. **One traffic sim for everything that moves on roads.** The traffic WP owns the
   `ITrafficService` implementation. Cars, trucks, buses, service and emergency vehicles all
   request trips through it.
   - The PT WP drives buses by requesting stop-to-stop legs (`Purpose = Transit`,
     `VehicleType = "bus"`) and handling boarding in `OnTripArrived`.
   - Services do the same for garbage trucks, ambulances and so on.
   - Logistics does it for trucks, at a sampled visible rate. Freight load is added as congestion
     weight.

5. **Shared blackboard: `IPropertyRegistry`** (ZON).
   - Every city building is a `Property` with stable id, access road, kind, level, slots
     (households, jobs by education, students, beds) and occupancy.
   - Citizens fill homes, jobs and seats. The economy sets job slots for companies, services set
     them for service buildings, and ZON provides defaults.
   - **Field ownership is listed in `CityInterfaces.cs`.**
   - **A working baseline already exists:** `Traits/Property/PropertyRegistry.cs`, registered in
     `rules/world.yaml`. It derives properties from `CityBuilding` actors: household slots =
     `MaxResidents/3`, job slots by a default education mix, access roads via `IRoadNetwork`. So
     CIT, ECO and SVC can build on it immediately. ZON owns it from now on and extends it in
     place (it may move the registration into `rules/zoning.yaml`).

6. **Cross-WP calls only through `CityInterfaces.cs`**, so every WP compiles alone.
   - Look up providers with `TraitsImplementing<IFoo>().FirstOrDefault()`.
   - Always tolerate `null` and fall back to the legacy behaviour.
   - Need a new member? Put it in your own interface or class, and list it in your report under
     "interface requests". The lead merges requests into `CityInterfaces.cs`, which is append-only.

7. **Orders.** Each WP defines its own order strings and factory methods in its own file, e.g.
   `Traits/Economy/EconomyOrders.cs`, and resolves them in its own player trait (`IResolveOrder`).
   - `CityTypes.cs` and `CityInterfaces.cs` are lead-owned and append-only. The lead has already
     appended zone types 7–12 and the new `CityInfoView` values.
   - Order payload limits: `TargetString`, `ExtraData` (uint), `ExtraLocation` (CPos, ±2047),
     `Target` cell.

8. **Determinism (hard rules).** Saves and replays are order replays, so they only work if every
   rule here holds.
   - Integer math only in synced state. Use milli-units and cents for fractions.
   - Randomness comes only from `world.SharedRandom`, called a fixed number of times in a fixed
     order, or from pure hashes of (id, day, salt).
   - Iterate arrays by index and actors by `ActorID`. Never enumerate a `Dictionary` or `HashSet`
     in synced code. No LINQ in hot paths.
   - Create and dispose actors only inside `world.AddFrameEndTask`.
   - Expose an `int StateHash` and include it in your autotest report line. The lead's replay check
     compares the whole log.
   - UI code reads only; it never mutates sim state. Sampling of visible vehicles must never depend
     on the camera.

9. **Performance budget** at 50k population, speed 1 (40 ms tick):
   - total city sim < 10 ms per tick on average;
   - spikes < 25 ms — spread daily work across ticks (bucketing by `id % something`);
   - traffic < 5 ms, citizens < 2 ms, economy < 1 ms, services < 1 ms, everything else < 1 ms.
   - The autotest prints `ticks/s`; the `stress` scenario must stay at ≥ 100 ticks/s.

10. **Art** is generated by Python with `tools/pngkit.py`. You may *import* helpers from
    `tools/genworld.py` and `tools/genui.py`, but don't edit them.
    - **UI chrome is not pre-rendered.** Every collection with `Generator: CityChrome` (chrome.yaml,
      chrome-city.yaml, chrome-buildings.yaml) is drawn at runtime by `OpenRA.Mods.City/UIArt`
      (`CityChromeGenerator`, engine hook `IChromeGenerator` in `ChromeProvider`) at the exact device scale
      (window scale x UI scale) and redrawn when it changes. Colours: `uistyle.yaml`. Icon and glyph designs are
      the python recipes `tools/uicityicons*.py` / `tools/uibaseglyphs.py`, exported as vector ops to
      `bits/chrome/cityicons.vec` by `tools/uiexport.py` (re-run it after editing a recipe). Build icons stay
      1x PNGs and are upscaled by whole factors at runtime. Don't add 2x/3x image atlases.
    - Each WP that adds actors or overlays writes its own generator `tools/gen_<wp>.py`, its own PNGs
      under `bits/world/<wp>/` (referenced as `Filename: <wp>/name.png`; subfolders of the
      `city|bits/world` package resolve directly), and its own sequences file.
    - Style: match the existing 3/4 top-down look (`genworld.py`). 1×1 = 32×64,
      2×2 = 64×96, 3×3 = 96×128 frames, `Offset: 0,-16`. Footprint is the bottom square.
    - Chrome PNGs must be power-of-two sized.

11. **UI.** D2k-style sidebar and dialogs (see `chrome/ingame-player.yaml`, `chrome/city-panels.yaml`).
    - Each sim WP exposes **read-only getters and order factories** for its data.
    - The UI WP builds panels, tools and info views on top of them, in parallel, against the
      interfaces.
    - Sim WPs do **not** edit chrome layouts. They may add fluent keys for notifications in their
      own `.ftl` file.

---------------------------------------------------------------------------------------------------

## 2. Work packages (wave 1)

| WP | Name | Design doc | Main deliverable (this wave) |
|---|---|---|---|
| **ENV** | Environment, time visuals, stats | `10-environment-time-ui.md` | Day/night tint and season visuals, `PollutionLayer` (ground, air, noise, groundwater, water), `ICityStatistics` (monthly ring buffers + chirper feed), notification/problem-icon catalogue (replaces `WithCityStatusIcons`) |
| **CIT** | Citizens & households | `01-citizens.md` | `CitizenSim` (`ICitizenPopulation`): households, ages, birth/death, education, jobs by education, homes via `IPropertyRegistry`, wages/rent/shopping via `ICityEconomy`, schools/health via `ICityServices`, schedules → trips via `ITrafficService`, happiness factors, tourists (basic) |
| **TRF** | Traffic | `02-traffic.md` | `TrafficSim` (`ITrafficService`, replaces `TrafficManager`): meso link queues, A* routing with congestion snapshots, junction control, parking-lite, vehicle renderer for thousands of cars (no actors), freight and service trips, flow metrics. Includes an aggregate trip source until CIT lands |
| **NET** | Road & utility networks | `03-networks.md` | Road type registry (street, gravel, avenue, boulevard, highway), one-way and paired roads, `CanEnter`, junction control and roundabout orders, upgrade-by-drag, per-type autotile art (`IRoadNetwork` on `RoadLayer`); `UtilityNetwork` (`IUtilityNetwork`): LV/pipes along roads, HV lines, transformers, sewage outlets and treatment, component solver, import/export; writes `CityBuilding.HasPower/HasWater` |
| **ZON** | Zoning, lots & buildings | `04-zoning-buildings.md` | `PropertyRegistry` (`IPropertyRegistry`), lot finder (1×1..4×3), multi-cell growable catalogue with levels 1–5 as state (not actor swaps), condition and rent loop, `LandValueLayer`, abandonment and condemn, new zone types 7–12, signature buildings, zone overlay frames for the new zones, growable art for multi-cell lots |
| **ECO** | Economy & companies | `05-economy.md` | `CityEconomy` (`ICityEconomy`): 25 resources and recipes (`rules/economy.yaml`), companies on properties, market/import/export, wages and taxes (income by education, profit tax), fees, service budgets hook, loans, ledger with conservation check. **Owns all `CityManager*.cs` files**: delegate `Population/Workers/...` to `ICitizenPopulation`, `GetDemand` to `IDemandModel`, milestones/`IsUnlocked` to `IProgression`, and utilities to `IUtilityNetwork` when present, and skip the legacy aggregate steps they replace |
| **IND** | Industry & logistics | `06-industry-logistics.md` | `NaturalResourceLayer` (map resource bytes: fertile, ore, oil; forests from trees, stone), five extractor hubs plus area tool (`IExtractorSource`), `Logistics` (`ILogistics`): shipments, fleets, timing wheel, warehouses, outside trade, freight load into traffic; may edit `tools/genmap.py` (resource bytes) and regenerate maps |
| **SVC** | City services | `07-services.md` | `ServiceSimulation` (`ICityServices`): needs (garbage, sickness, crime, fire hazard, mail, students), road-distance catchment with capacity, dispatch via traffic trips, fires, crime, sickness/death, budget sliders, upgrades, the new service catalogue and its art. **Owns `CityCoverageLayer`**: keep its API and delegate land value to ZON and pollution to ENV |
| **PRG** | Demand & progression | `09-demand-progression.md` | `DemandModel` (`IDemandModel`, factor breakdown, accumulator spawning hook for ZON), XP and 20 milestones, development points and tree (yaml), map tiles 9×9 purchase (`IsCellOwned`), policies (yaml), districts, tourism basics (`IProgression`) |
| **PT** | Public transport | `08-public-transport.md` | `TransitLayer` (`ITransitPlanner`): bus stops/depots/lines and tool orders, buses as traffic trips leg by leg, passenger queues, mode quote, fares and line stats; taxis; metro as data-only (stretch) |
| **UI** | Player interface | all | D2k-style UI for everything above: road type palette and modes, utility tools, zone types, hub area tool, transit line tool, policies, districts, tiles, budget/taxes/fees/loans, production panel, statistics graphs, chirper, citizen and building inspection, demand factor tooltips, all new info views (renders `InfoViewLayer`) |

### 2.1 File ownership (new and existing)

Paths: C# relative to `OpenRA.Mods.City/`, mod files relative to `mods/city/`. Create new C#
files under your folder; other files only where listed.

| WP | C# | Mod files |
|---|---|---|
| ENV | `Traits/Environment/**`, `Traits/Simulation/WithCityStatusIcons.cs` | `rules/environment.yaml`, `sequences/environment.yaml`, `fluent/environment.ftl`, `bits/world/env/**`, `tools/gen_env.py` |
| CIT | `Traits/Citizens/**` | `rules/citizens.yaml`, `fluent/citizens.ftl` |
| TRF | `Traits/Traffic/**`, `Traits/World/TrafficManager.cs` (delete or replace) | `rules/traffic.yaml`, `sequences/vehicles.yaml`, `fluent/rules-traffic.ftl`, `bits/world/traffic/**`, `tools/gen_traffic.py` |
| NET | `Traits/World/RoadLayer.cs`, `Traits/World/OutsideConnection.cs`, `Traits/Construction/**`, `Traits/Player/ConstructionTools.cs`, `Traits/Buildings/CityPlaceable.cs`, `Traits/Buildings/Bulldozable.cs`, `Traits/Buildings/UtilityProducer.cs`, `Orders/RoadOrderGenerator.cs`, `Orders/BulldozeOrderGenerator.cs`, `Orders/PlaceCityBuildingOrderGenerator.cs`, `Orders/CityDragOrderGenerator.cs`, `Traits/Networks/**`, `Orders/Networks/**` | `rules/construction.yaml`, `rules/networks.yaml`, `sequences/networks.yaml`, `sequences/misc.yaml` (road frames), `fluent/rules-construction.ftl`, `fluent/networks.ftl`, `bits/world/net/**`, `tools/gen_net.py` |
| ZON | `Traits/World/ZoneLayer.cs`, `Traits/World/ZoneGrowth.cs`, `Traits/Buildings/GrowableBuilding.cs`, `Traits/Buildings/CityBuilding.cs`, `Traits/Growth/**`, `Traits/Player/ZoningTool.cs`, `Orders/ZoneOrderGenerator.cs`, `Traits/Property/**` | `rules/zoning.yaml`, `rules/growables.yaml`, `sequences/buildings.yaml`, `fluent/rules-zoning.ftl`, `bits/world/zon/**`, `tools/gen_zon.py` (may replace growable PNGs in `bits/world/`) |
| ECO | `Traits/Player/CityManager.cs`, `Traits/Simulation/CityManager.*.cs`, `Traits/Economy/**` | `rules/economy.yaml`, `rules/simulation.yaml`, `rules/player.yaml` (CityManager config only), `fluent/economy.ftl` |
| IND | `Traits/Industry/**` | `rules/industry.yaml`, `sequences/industry.yaml`, `fluent/industry.ftl`, `bits/world/ind/**`, `tools/gen_ind.py`, `tools/genmap.py`, `maps/**` (regenerate keeping anchors) |
| SVC | `Traits/Services/**`, `Traits/Buildings/ServiceBuilding.cs`, `Traits/World/CityCoverageLayer.cs` | `rules/services.yaml`, `sequences/services.yaml`, `fluent/rules-services.ftl`, `bits/world/svc/**`, `tools/gen_svc.py`, `chrome-buildings.yaml` + `bits/chrome/buildicons.png` (build icons for every placeable, including other WPs' new placeables; ask for the list in your report) |
| PRG | `Traits/Progression/**` | `rules/progression.yaml`, `fluent/progression.ftl` |
| PT | `Traits/Transit/**`, `Orders/Transit/**` | `rules/transit.yaml`, `sequences/transit.yaml`, `fluent/transit.ftl`, `bits/world/pt/**`, `tools/gen_pt.py` |
| UI | `Widgets/**`, `Traits/World/InfoViewLayer.cs`, `Orders/Ui/**` (tool order generators that only *issue* other WPs' orders) | `chrome/*.yaml` (except the lead's), `chrome-city.yaml`, `bits/chrome/cityicons*.png`, `tools/genui.py` and its helpers, `fluent/chrome.ftl`, `fluent/hotkeys.ftl`, `hotkeys.yaml`, `rules/ui.yaml`, **only** the `ChromeLayout:`/`Hotkeys:` sections of `mod.yaml` |
| lead | `CityTypes.cs`, `CityInterfaces.cs`, `Traits/World/CityClock.cs`, `Traits/World/CityAutoTest.cs` | `mod.yaml` (except UI sections), `rules/defaults.yaml`, `rules/world.yaml`, `rules/player.yaml` (except CityManager), `rules/palettes.yaml`, `tools/pngkit.py`, `tools/autotest.sh`, `ARCHITECTURE.md`, `CONTRACT.md` |

Every rules file merges `World:` and `Player:` nodes, so register your traits in **your own** rules
file. The lead pre-registered empty rules, sequences and fluent files for every WP in `mod.yaml`,
and `bits/world/<wp>/` subfolders need no `mod.yaml` change (`Filename: <wp>/foo.png`).

**Temporary local hacks** in files you don't own are fine for testing, but **revert them before
committing**. Stubs of other WPs' interfaces that you need to test belong in **scratch traits
you do not commit**.

---------------------------------------------------------------------------------------------------

## 3. Key cross-WP flows (who calls whom)

- **Move-in.** CIT asks PRG/ECO demand (via `CityManager.GetDemand` or `IDemandModel`) and finds
  free `Property.HouseholdSlots`. It writes `Households/Residents` and reports rent through
  `ReportRentPaid`.
- **Jobs.**
  - ECO sets `JobSlots[edu]` per company property, SVC per service, ZON gives defaults.
  - CIT matches workers and writes `JobsFilled`.
  - Each day CIT pays each worker `WageCentsPerDay(edu)`, calls `ChargeWages(employer)` and books
    income tax.
  - ECO reads `JobsFilled` for production.
- **Shopping.** CIT schedules a shopping trip, calls `FindShop` and travels via `ITrafficService`.
  On arrival it calls `SellToHousehold` and debits household cash.
- **Freight.** ECO's market calls `ILogistics.TryDispatch`. IND moves goods in time, requesting
  sampled visible truck trips from TRF and adding freight load.
- **Services.**
  - SVC computes needs from `Property` occupancy plus CIT callbacks (`RequestTreatment`,
    `ReportDeath`, `TryEnroll`).
  - Vehicles are TRF trips with `VehicleType` set.
  - Satisfaction flows into the coverage maps, and from there into happiness and land value.
- **Utilities.** NET solves networks every pulse and writes `CityBuilding.HasPower/HasWater`
  (+ `PowerPercent`). `CityManager` stops distributing utilities when an `IUtilityNetwork` exists.
- **Pollution.** ENV samples `IPollutionEmitter` actors and traffic noise
  (`ITrafficService.GetNoise`). ZON (land value), CIT (health) and IND (fertility) read
  `IPollutionMap`.
- **Unlocks.** Placement (NET), zoning (ZON), road types (NET), transit (PT) and policies all ask
  `IProgression.IsUnlocked(key)` and `IsCellOwned(cell)`. Without PRG they fall back to
  `CityPlaceable.UnlockPopulation`.

---------------------------------------------------------------------------------------------------

## 4. Testing

- **Build:**
  - `make all` in a fresh worktree;
  - incremental builds: `dotnet build OpenRA.Mods.City/OpenRA.Mods.City.csproj -c Release -nologo -p:TargetPlatform=linux-x64`;
  - Debug build: no warnings in your files.
- **Lint:** `./utility.sh city --check-yaml` must report no `Error` lines.
- **Headless autotest:** `mods/city/tools/autotest.sh <map|menu|replay:file> "ticks=..;shots=..;timestep=..;log=..;scenario=basic|bulldoze|stress"`.
  - The test logs a stats line and every `ICityAutoTestReporter` line (`report ...`) at each log
    interval, plus `ticks/s`.
  - **Implement `ICityAutoTestReporter`** on your main trait (counts plus `StateHash`).
  - Read screenshots with the Read tool. For zoom crops, write a small Python crop with
    `tools/pngkit.py` (PNG decode via zlib).
- **Determinism check:**
  1. Run a scenario.
  2. Copy `<supportdir>/Replays/**.orarep`.
  3. Run `autotest.sh replay:<file>` with the same spec.
  4. Strip the `[autotest t=N]` prefix (keep `t=N`) and `diff` every `date=`/`report` line. They
     must be identical.
- **Scenarios:**
  - `basic`: roads, zones, services on the main map;
  - `bulldoze`;
  - `stress`: the whole map plus $2M.
  - `full`: `basic` plus scheduled steps that exercise every system: utilities, an extended avenue, a farm hub
    and its field area, a landfill, a bus depot and bus line, then school, park, high school and hospital once
    unlocked (locked orders are refused and retried).
  - `automayor`: an adaptive scripted player (`Traits/World/CityAutoTest.AutoMayor*.cs`) that builds and manages a city through real
    orders only (zoning by demand, utilities, services by coverage, roads, tiles, taxes, policies) and logs `report mayor ...` lines.
    It runs "dry" in replays, so its lines take part in the replay diff. Balance runs and results: `design/BALANCE.md`.
  - WPs may add scenario names **only via the lead**; put extra checks in a scratch trait.
- **Fallback test:** the game must still run with your WP's traits removed from rules, which is
  how the other WPs test before merge.

## 5. Merge order (lead)

Each merge is followed by build, lint, autotest of all scenarios and the replay check, plus a
performance check:

1. NET, ZON, ENV, TRF — foundations: properties, roads, clock visuals, traffic engine.
2. ECO, CIT, IND, SVC — agent simulations.
3. PRG, PT, UI — progression, transit, interface.

When you finish, **report**:
- what was built (by phase of your design doc);
- files;
- verification (lint, autotest lines, `StateHash`, ticks/s, screenshots checked);
- known issues;
- **interface requests**;
- what other WPs must do to fully integrate (e.g. "ECO must call X").
