# OpenCity — build contract

OpenCity is a **Cities: Skylines 2–style city builder** built on the OpenRA engine.
It is a new mod (`mods/city`) plus a new assembly (`OpenRA.Mods.City`). Other mods are untouched.

The view is **2D top-down** (32×32 px cells, Rectangular grid). Buildings are drawn in a gentle
"3/4 top-down" style: you see the roof and the south facade, and tall buildings rise above their cell.
**All art is generated procedurally by Python scripts in `mods/city/tools/`**, using only the
standard library via `tools/pngkit.py`. There are no third-party assets. Art is 32-bit RGBA PNG, so
palettes are irrelevant, but the named palettes in `rules/palettes.yaml` must keep existing.

This file is the single source of truth. Several agents build in parallel, each owning a
**work package** (WP) and its files. Follow these rules:

1. **Only edit files your WP owns** (see the ownership table below). If you need something from
   another WP, use the public API listed here. If the API is missing something, work around it
   locally in your own files and **list it under "Integration notes" in your final report**.
2. **Never change the public signatures in another WP's stub.** You may *add* public members to
   your own classes and fully rewrite your own files.
3. `OpenRA.Mods.City/CityTypes.cs` and `Traits/World/CityAutoTest.cs` are **frozen**. Do not edit them.
4. All sim state changes go through **orders** (`CityOrders`) and synced code. That keeps the game
   deterministic, which also makes save games and replays work (OpenRA saves are order replays).

---------------------------------------------------------------------------------------------------

## 1. Game design (MVP)

Core loop, as in Cities: Skylines 2:

1. The map starts with a **highway connection** at the map edge: a road laid automatically from a
   `highway-*` actor.
2. The player **builds roads**: drag for a straight or L-shaped road at $10 per cell.
3. The player **paints zones** next to roads: Residential low/high, Commercial low/high, Industrial,
   Office.
4. **Buildings grow automatically** on zoned cells that have road access to the highway, when the
   zone's **demand** is positive.
5. Citizens move in. Workers fill jobs. **Taxes** come in monthly. **Upkeep** for roads and
   services goes out.
6. Buildings need **electricity and water**: city-wide pools from power plants, wind turbines,
   water towers and pumps.
7. **Services** (police, fire, health, education, parks) cover a radius. They raise **happiness**
   and **land value**. Pollution from industry and coal lowers them.
8. Buildings **level up** (1→3) with good land value and happiness. They are **abandoned** without
   power, water or road access, or when very unhappy, then collapse and regrow.
9. **Milestones** are reached at population thresholds. Each grants a cash bonus and unlocks
   buildings (`CityPlaceable.UnlockPopulation`).
10. **Traffic**: cars drive along roads between homes and workplaces, and from the highway.
11. **Info views**: heatmaps for power, water, land value, pollution, each service, traffic and
    happiness.
12. **Time**: `CityClock` uses 180,000 ticks per day, or 120 real minutes at 1x. The legacy
    25-tick update is an aggregate pulse. Speeds 1/2/3 use 40/20/10 ms; pause uses OpenRA pause.
13. **Sandbox**: New City offers all development, policies and land unlocked with unlimited money.
    The option is serialized in the lobby settings for saves and replays; normal games default to off.

Money is an `int` (`CityManager.Funds`). Starting funds are $70,000.

---------------------------------------------------------------------------------------------------

## 2. Work packages and file ownership

| WP | Name | Owns |
|----|------|------|
| **A1** | Terrain, nature & maps | `tilesets/temperate.yaml`, `bits/terrain/**`, `maps/**` (may delete `boot-*` maps once its own maps work), `rules/nature.yaml`, `sequences/nature.yaml`, `fluent/rules-nature.ftl`, `bits/world/tree-*.png`, `tools/genterrain.py`, `tools/genmap.py` |
| **A2** | World art | `bits/world/**` (except `tree-*`), `sequences/buildings.yaml`, `sequences/services.yaml`, `sequences/vehicles.yaml`, `sequences/misc.yaml`, `chrome-buildings.yaml`, `bits/chrome/buildicons.png`, `tools/genworld.py` |
| **A3** | UI art, cursors & sound | `chrome.yaml`, `chrome-city.yaml`, `bits/chrome/**` (except `buildicons.png`), `cursors.yaml`, `bits/cursors/**`, `metrics.yaml`, `bits/audio/**`, `audio/notifications.yaml`, `icon*.png` (mod icons, see `mods/ra/icon*.png`), `tools/genui.py`, `tools/gensfx.py` |
| **C** | Construction tools | `Traits/World/RoadLayer.cs`, `Traits/World/OutsideConnection.cs`, `Traits/Buildings/CityPlaceable.cs`, `Traits/Buildings/Bulldozable.cs`, `Traits/Player/ConstructionTools.cs`, `Orders/RoadOrderGenerator.cs`, `Orders/BulldozeOrderGenerator.cs`, `Orders/PlaceCityBuildingOrderGenerator.cs`, `Orders/CityDragOrderGenerator.cs` (shared drag base class, **C writes it first**, see §5), new files under `Traits/Construction/`, `rules/construction.yaml`, `fluent/rules-construction.ftl` |
| **D** | Zoning & growth | `Traits/World/ZoneLayer.cs`, `Traits/World/ZoneGrowth.cs`, `Traits/Buildings/GrowableBuilding.cs`, `Traits/Player/ZoningTool.cs`, `Orders/ZoneOrderGenerator.cs`, new files under `Traits/Growth/`, `rules/zoning.yaml`, `rules/growables.yaml`, `fluent/rules-zoning.ftl` |
| **E** | Simulation & services | `Traits/Player/CityManager.cs`, `Traits/Buildings/CityBuilding.cs`, `Traits/Buildings/UtilityProducer.cs`, `Traits/Buildings/ServiceBuilding.cs`, `Traits/World/CityCoverageLayer.cs`, new files under `Traits/Simulation/`, `rules/simulation.yaml`, `rules/services.yaml`, `rules/player.yaml` (CityManager config only), `fluent/rules-services.ftl` |
| **F** | UI / HUD / menus | `Widgets/**`, `Traits/World/InfoViewLayer.cs`, `chrome/mainmenu.yaml`, `chrome/ingame-player.yaml`, `chrome/ingame-observer.yaml`, `chrome/city-panels.yaml`, `fluent/chrome.ftl`, `fluent/hotkeys.ftl`, `hotkeys.yaml`, `rules/ui.yaml`, and **only** the `ChromeLayout:` and `Hotkeys:` sections of `mod.yaml` |
| **G** | Traffic | `Traits/World/TrafficManager.cs`, new files under `Traits/Traffic/`, `rules/traffic.yaml`, `fluent/rules-traffic.ftl` |
| lead | Integration | everything else: `mod.yaml` (other sections), `rules/defaults.yaml`, `rules/world.yaml`, `rules/palettes.yaml`, `CityTypes.cs`, `CityAutoTest.cs`, `tools/pngkit.py`, `tools/autotest.sh` |

C# paths are relative to `OpenRA.Mods.City/`. Mod paths are relative to `mods/city/`.
Register your world and player traits **in your own rules file**. MiniYaml merges `World:` and
`Player:` nodes across rules files, for example:

```yaml
World:
	RoadLayer:
Player:
	ConstructionTools:
```

---------------------------------------------------------------------------------------------------

## 3. Shared C# API (stubs already exist and compile; owners replace the bodies)

Namespace `OpenRA.Mods.City` (types, orders) / `OpenRA.Mods.City.Traits` (traits).

**CityTypes.cs (frozen)**
- `ZoneType`: `None`, `ResidentialLow`, `ResidentialHigh`, `CommercialLow`, `CommercialHigh`,
  `Industrial`, `Office`.
- `ZoneCategory`: `Residential`, `Commercial`, `Industrial`, `Office`.
- `CityService`: `Police`, `Fire`, `Health`, `Education`, `Parks`.
- `CityInfoView`: the info view modes.
- `CityDate`.
- `ICityBuildingState { bool IsOperational }`.
- `CityOrders`: the order string constants plus factory methods.
  - `BuildRoadOrder(p, from, to)`
  - `BulldozeOrder(p, from, to)`
  - `PlaceBuildingOrder(p, actorType, topLeft)`
  - `ZoneOrder(p, from, to, zone)`
  - `SetTaxOrder(p, category, percent)`
  - `SetSpeedOrder(p, speed)`

  Every order's subject is `player.PlayerActor`. Issue them with `world.IssueOrder(...)`.
- `CityUtils`:
  - `Neighbours4` (N, E, S, W, which is the bit order for masks)
  - `zone.Category()`
  - `zone.GrowablePrefix()` (`res-low`, `res-high`, `com-low`, `com-high`, `ind`, `off`)
  - `Rect(a, b)`
  - `RoadPath(from, to)` (the L-shaped path; the road tool preview and the order handler both use it)
  - `FormatMoney`

**RoadLayer (World, C)**
- Members: `Info`, `event Action<CPos> RoadChanged`, `NetworkVersion`, `RoadCellCount`,
  `RoadCells`, `IsRoad(c)`, `CanBuildRoadAt(c)`, `AdjacentRoadCells(c)`, `IsAdjacentToRoad(c)`,
  `IsConnectedToOutside(roadCell)`, `HasRoadAccess(IEnumerable<CPos> cells)`, `AddRoad(c)`,
  `RemoveRoad(c)`.
- Keeps `Map.CustomTerrain[cell]` set to the `Road` terrain type index on road cells, which feeds
  vehicle pathfinding.
- Renders roads by autotiling: image `roads`, sequence `road`, frame = N/E/S/W neighbour mask.

**OutsideConnection (C)**
- Actor trait on `highway-w/e/n/s`, with `Direction` and `Length`.
- On world load RoadLayer lays `Length` road cells from the actor's cell along `Direction`.

**CityPlaceable (C)**
- `Cost`, `Category`, `DisplayOrder`, `Description` (fluent key), `RequiresRoad`,
  `RequiresTerrainNearby`, `NearbyRange`, `UnlockPopulation`.
- Categories: `power`, `water`, `police`, `fire`, `health`, `education`, `parks`, `special`.

**Bulldozable (C)**
- `RefundPercent`, `Cost`, `AutoClear`.
- `AutoClear` actors (trees) are removed automatically when roads, growth or placement need the cell.

**ConstructionTools (Player, C)**
- Resolves `CityBuildRoad`, `CityBulldoze` and `CityPlaceBuilding`.
- Pays through `CityManager.TrySpend` and refunds through `CityManager.AddFunds`.
- Clears zones under new roads and buildings via `ZoneLayer.SetZone(cell, ZoneType.None)`.

**ZoneLayer (World, D)**
- `Info.ZoneDepth`, `event ZoneChanged`, `GetZone(c)`, `CanZone(c)`, `SetZone(c, z)`,
  `CountZoned(z)`, `ZonedCells(z)`.

**GrowableBuilding (actor, D)**
- Implements `ICityBuildingState`.
- Members: `Zone`, `Level`, `UnderConstruction`, `Abandoned`, `IsOperational`, `Info.UpgradesTo`.

**ZoningTool (Player, D)**
- Resolves `CityZone`.

**CityManager (Player, E)**
- Money:
  - `Funds` (int), `CanAfford`, `TrySpend(amount, category)`, `AddFunds(amount, category)`
  - `MonthlyBalance`, `LastMonthIncome`, `LastMonthExpenses`
- Calendar: `TotalDays`, `Date`, `Speed`.
- People and demand: `Population`, `Workers`, `Jobs`, `Unemployed`, `GetDemand(cat)` (-100..100),
  `GetTaxRate(cat)`.
- Utilities: `PowerProduced`, `PowerConsumed`, `WaterProduced`, `WaterConsumed`.
- Wellbeing and progression: `AverageHappiness`, `MilestoneIndex`, `MilestoneName`,
  `NextMilestonePopulation`, `IsUnlocked(actorType)`.
- Resolves `CitySetTax` and `CitySetSpeed`.

**CityBuilding (actor, E)**
- `Info`: `Zone`, `Level`, `MaxResidents`, `MaxJobs`, `PowerUse`, `WaterUse`, `Upkeep`, `Pollution`.
- State: `Residents`, `Workers`, `HasPower`, `HasWater`, `HasRoadAccess`, `Happiness` (0..100),
  `LandValue` (0..100), `IsOperational`.

**UtilityProducer (actor, E)**
- `Info.Power`, `Info.Water`.

**ServiceBuilding (actor, E)**
- `Info.Service`, `Info.Radius` (cells), `Info.Strength`.

**CityCoverageLayer (World, E)**
- `Version`, `GetCoverage(service, cell)`, `GetLandValue(cell)`, `GetPollution(cell)`, all 0..100.

**TrafficManager (World, G)**
- `ActiveVehicles`, `GetTrafficLoad(cell)` (0..100).

**InfoViewLayer (World, F)**
- `Mode` (local, unsynced).
- Renders the heatmap using image `overlays`, sequence `heat`.

**Robustness rules**
- Every trait must tolerate actors owned by a player **without** `CityManager`. Neutral owns the
  shellmap city; treat those actors as static decoration and do not crash.
- Every trait must tolerate the world having no `TrafficManager` or `InfoViewLayer`, and so on.
  Use `TraitOrDefault`.

---------------------------------------------------------------------------------------------------

## 4. Actors (rules data contract)

The current versions in `rules/*.yaml` were written by the lead as a starting point. Owners may
tune numbers and add traits, but must keep the actor names.

**Growables** (`rules/growables.yaml`, D)
- `res-low-{1,2,3}`, `res-high-{1,2,3}`, `com-low-{1,2,3}`, `com-high-{1,2,3}`, `ind-{1,2,3}`,
  `off-{1,2,3}`.
- All are 1×1 and have `GrowableBuilding`, `CityBuilding` and `Bulldozable`.
- `UpgradesTo` links each level to the next.

**Services** (`rules/services.yaml`, E). Name, footprint and category:

| Actor | Footprint | Category |
|-------|-----------|----------|
| `powerplant-coal` | 2×2 | power |
| `windturbine` | 1×1 | power |
| `solarplant` | 2×2 | power, unlock at 1000 |
| `watertower` | 1×1 | water |
| `waterpump` | 1×1 | water, needs Water within 2 cells |
| `police` | 2×2 | police |
| `firestation` | 2×2 | fire |
| `clinic` | 2×2 | health |
| `school` | 2×2 | education, unlock at 250 |
| `park-small` | 1×1 | parks |
| `plaza` | 1×1 | parks |
| `park-large` | 2×2 | parks, unlock at 500 |

**Nature** (`rules/nature.yaml`, A1)
- `tree-1` … `tree-4`: 1×1, `Bulldozable: AutoClear: true`.

**Vehicles** (`rules/traffic.yaml`, G)
- `car-a`, `car-b`, `car-c`, `car-d`, `truck`, `bus`.
- They use `Mobile` with locomotor `road` (only terrain type `Road` is passable).

**System actors** (`rules/construction.yaml`, C)
- `highway-w` sits on the west edge and runs east; `highway-e`, `highway-n` and `highway-s` follow
  the same pattern.
- `roadseed` (C must add it): an invisible marker. RoadLayer turns each one into a road cell on
  world load and then disposes it. Maps and the shellmap use it to pre-build roads.

Base templates live in `rules/defaults.yaml` (lead): `^CitySprite`, `^CityStructure`,
`^1x1Shape`, `^2x2Shape`, `^3x3Shape`.

---------------------------------------------------------------------------------------------------

## 5. Shared drag order generator (C writes it first)

`Orders/CityDragOrderGenerator.cs` is an abstract `IOrderGenerator`. Road, bulldoze and zone tools
subclass it. **C must create and commit it early.** D needs it; until then, D may copy it as a
private nested class and switch over at integration.

Behaviour:
- **Left mouse down:** start the drag at that cell.
- **Move with left held:** update the current cell.
- **Left up:** call `abstract IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)`.
- **Move without left held while dragging:** cancel the drag. This is the mouse released over the
  chrome.
- **Right up / Escape:** `world.CancelInputMode()`.
- Subclasses implement the preview, typically by yielding
  `MarkerTileRenderable(cell, color)` from `RenderAnnotations`, plus `GetCursor`.

Mouse events reach `Order()` for Down, Move and Up. In `Move` events, `mi.Button` holds the held
buttons. Use `Game.Settings.Game.ResolveActionButton(MouseActionType.PlaceBuilding)` as the action
button. The UI activates a tool with `world.OrderGenerator = new RoadOrderGenerator(world)`, and
toggles it off with `world.CancelInputMode()`.

Constructors the UI (F) relies on:
- `new RoadOrderGenerator(World world)`
- `new BulldozeOrderGenerator(World world)`
- `new PlaceCityBuildingOrderGenerator(World world, string actorType)`
- `new ZoneOrderGenerator(World world, ZoneType zone)` (`ZoneType.None` dezones)

---------------------------------------------------------------------------------------------------

## 6. Art contract (images, sequences, chrome regions)

General rules:
- All world images live in `bits/world/`, all chrome images in `bits/chrome/`.
- Sequence files use `DefaultSpriteSequence`.
- Multi-frame PNGs carry `FrameSize` and `FrameAmount` tEXt metadata; use `pngkit.save_sheet`.
- **The frame size and frame count below are the contract.** Draw anything inside them.

| Image (sequence file) | Sequence | Frames | Frame size / offset | Notes |
|---|---|---|---|---|
| `res-low-1` … `off-3` (`buildings.yaml`) | `idle` | 4 variants | 32×64, `Offset: 0,-16` | Footprint is the bottom 32×32. Higher level and higher density look bigger, richer and taller. Residential: houses → apartments → towers. Commercial: shops → stores → malls, with signage. Industrial: workshop → factory with chimney → plant. Office: glass blocks. |
| `construction` (`buildings.yaml`) | `small` / `large` | 4 each (animated) | small 32×64, large 64×96, `Offset: 0,-16` | Scaffolding, crane. |
| services (`services.yaml`) | `idle` | ≥1 (windturbine animated) | 1×1: 32×64; 2×2: 64×96; `Offset: 0,-16` | Recognisable silhouettes: chimney and smoke, wind turbine blades, solar panels, water tower tank, police blue, fire red with garage doors, clinic white with red cross, school with yard, green parks with trees and paths, plaza with fountain. |
| `roads` (`misc.yaml`) | `road` | 16 | 32×32 | Frame = mask of road neighbours (bit0 N, bit1 E, bit2 S, bit3 W). Asphalt with sidewalks/curbs on unconnected sides and dashed centre lines; mask 0 is an isolated patch; T-junctions and crossings have no centre line in the middle. |
| `overlays` (`misc.yaml`) | `zone` | 7 | 32×32 | Frame = `(int)ZoneType`; frame 0 is fully transparent. Translucent fill plus a thin cell border. Colours: ResLow `#7ED957`, ResHigh `#2E9E3E`, ComLow `#5AB4F0`, ComHigh `#2C6FD1`, Ind `#F2C94C`, Off `#B07CE8`. |
| `overlays` | `heat` | 11 | 32×32 | Translucent solid. Frame 0 = bad/low (red) → 5 yellow → 10 = good/high (green). |
| `overlays` | `valid`, `invalid`, `grid` | 1 each | 32×32 | Green / red translucent cell; a faint white cell outline. |
| `statusicons` (`misc.yaml`) | `icons` | 6 | 16×16 | 0 no power (yellow bolt), 1 no water (blue drop), 2 no road (road sign), 3 abandoned (boarded), 4 unhappy (frown), 5 level up (green arrow). |
| `car-a/b/c/d`, `truck`, `bus` (`vehicles.yaml`) | `idle` | 32 facings | 32×32 | Top-down vehicle; facing 0 = north, clockwise. |
| `tree-1..4` (`nature.yaml`, A1) | `idle` | ≥1 | 32×48, `Offset: 0,-8` | Varied deciduous and conifer trees with a soft shadow. |

**Chrome collections (chrome yaml)**
- `city-buildicons` (A2, `chrome-buildings.yaml`): one 48×48 region per `CityPlaceable` actor name.
- `city-icons` (A3, `chrome-city.yaml`): 32×32 regions:
  `road`, `zone-res-low`, `zone-res-high`, `zone-com-low`, `zone-com-high`, `zone-ind`, `zone-off`,
  `dezone`, `bulldoze`, `power`, `water`, `police`, `fire`, `health`, `education`, `parks`,
  `infoviews`, `budget`, `pause`, `play`, `fast`, `faster`, `money`, `population`, `happiness`,
  `close`, `milestone`, `zoning`, `services`.
  A3 may add more; F may only rely on these.
- A3 also adds HUD panel styles in `chrome-city.yaml`, all `PanelRegion` 9-slices:
  - `city-panel`: the translucent dark HUD panel.
  - `city-toolbar`.
  - `city-button`, plus `-hover`, `-pressed`, `-highlighted`, `-disabled` (and their
    `-highlighted` variants).
  - `city-tooltip`.
- **Base chrome** (`chrome.yaml`): currently RA's, copied as a bootstrap. A3 restyles it into a clean,
  modern, light-on-dark blue/teal CS2-like look.
  - **Every collection and region name must stay**, because the common layouts need them.
  - Generate new PNGs and regenerate the yaml coordinates.
  - Logo: `logos: logo` 256×256, an "OpenCity" skyline logo; the loadscreen uses the same image.

**Fonts**: `common|FreeSans.ttf` / `FreeSansBold.ttf` only.

---------------------------------------------------------------------------------------------------

## 7. Simulation design (E implements; D and G consume)

All of this runs in synced code. Use `world.SharedRandom`, iterate actors in a deterministic order
(for example by `ActorID`), and avoid floats in synced state (prefer ints).

**Calendar**
- Day = 25 ticks. Month = 30 days.
- Each month: taxes are collected and upkeep is paid. Upkeep covers road cells × $1 plus
  `CityBuilding.Upkeep` for operational services.
- Keep running `LastMonthIncome` and `LastMonthExpenses`, and set `MonthlyBalance` from them.

**Utilities**
- Every 25 ticks, sum `UtilityProducer` output from operational buildings with road access.
- Distribute to consumers in `ActorID` order until the supply runs out.
- Set `HasPower` and `HasWater` on each building.

**People**
- Residential occupancy grows towards `MaxResidents` when R demand > 0 and the building is
  operational, powered and watered. Roughly +1 per building every 2–5 days.
- Occupancy shrinks when happiness is below 25.
- Workers ≈ 55% of residents.
- Employed = min(workforce, jobs). Fill jobs across buildings deterministically.

**Demand** (-100..100). It must feel like CS:
- Residential is high at the start. It rises with available jobs and falls with unemployment.
- Industrial starts moderately positive so the first jobs appear.
- Commercial follows population: about 1 commercial job per 4 residents.
- Office is unlocked by education coverage, or by population above about 800.
- Tax rates above 10% reduce demand; rates below 10% increase it.

**Happiness and land value**
- Building happiness depends on:
  - power and water
  - police, fire and health coverage
  - education (residential only)
  - parks
  - pollution (negative)
  - taxes
  - unemployment (residential only)
- Land value (`CityCoverageLayer`) depends on:
  - parks and services coverage (positive)
  - water proximity (positive)
  - pollution (negative)
  - plus a small base value.
- Recompute every ~100 ticks, spread across ticks if that is expensive.

**Milestones**

| Population | Milestone | Cash bonus |
|-----------:|-----------|-----------:|
| 0 | Village | — |
| 250 | Hamlet | $5,000 |
| 1,000 | Small Town | $10,000 |
| 2,500 | Town | $20,000 |
| 5,000 | Large Town | $40,000 |
| 10,000 | Small City | $60,000 |
| 25,000 | City | $100,000 |
| 50,000 | Metropolis | $200,000 |

- `IsUnlocked(type)` is true when population ≥ `CityPlaceable.UnlockPopulation`. Once a milestone is
  reached, keep it unlocked.
- Announce each milestone with `TextNotificationsManager.AddTransientLine(player, text)`.

**Speed**
- `CitySetSpeed` with values 1/2/3 sets `world.Timestep` to 40/20/10 ms and stores `Speed`.

**Status icons**
- E owns a render trait (for example `WithCityStatusIcons`, added to `^CityStructure` via E's own
  rules, or by appending to D's/E's actor templates).
- It draws a `statusicons` icon above buildings that lack power, water or road access, or that are
  abandoned.

**Growth** (D)
- About every 10 ticks, for each zone type with demand > 0, try to spawn `ceil(demand / 25)` level-1
  growables (`{prefix}-1`).
- A candidate cell must:
  - be zoned
  - be empty: no actor except `AutoClear` ones, which get cleared
  - not be water or road
  - satisfy `RoadLayer.HasRoadAccess({cell})`
- Pick candidates with `SharedRandom`.
- Construction lasts `ConstructionTicks`, rendered with `construction/small`.
- **Level up** to `UpgradesTo` when `LandValue` ≥ 40 (to L2) or ≥ 65 (to L3), and happiness ≥ 55,
  sustained for ~20 days. Replace the actor at the same cell and carry over `Residents` and
  `Workers`.
- **Abandon** after 45 days without power, water or road access, or after 60 days with happiness
  below 20. Collapse 30 days later; the zone stays, so it regrows.
- D owns the growable render trait. It picks the variant frame by a stable hash of the cell and
  shows `construction` while building.

**Traffic** (G)
- Spawn cars (`car-*`, sometimes `truck` to or from industrial) at the road cell next to a residential
  building that has residents. Each car drives to the road cell next to a random workplace
  (commercial, industrial or office, or a service) and is disposed on arrival.
- Some trips start or end at a highway road end, as commuters.
- Cap active vehicles at roughly min(200, population / 6).
- Keep per-cell traffic counts with decay. That feeds `GetTrafficLoad` and the traffic info view.
- Vehicles must not be selectable. Make them look smooth, turning nicely on corners.

---------------------------------------------------------------------------------------------------

## 8. UI design (F)

Clean and modern, like CS2: dark translucent panels, white text, teal accents.

**Top bar**
- Left: milestone name with a progress bar to the next milestone.
- Centre: date, plus speed buttons (pause, 1, 2, 3; hotkeys `Space`, `1`, `2`, `3`).
- Right:
  - money, with the monthly balance in green or red
  - population
  - happiness
  - power use/supply (red on deficit)
  - water use/supply (red on deficit)

**Bottom-centre toolbar** (one category button per entry)
- Roads (`R`), Zoning (`Z`), Electricity, Water, Police, Fire, Health, Education, Parks,
  Bulldoze (`B`), Info Views (`I`), Budget (`F`).
- Clicking a category opens a panel above the toolbar with its items:
  - Zoning: the 6 zone types plus Dezone.
  - Services: their `city-buildicons`, name and cost. Disabled with "Unlocks at N population" when
    locked.
  - Tooltips show the cost, monthly upkeep and description.

**Bottom-left**
- R/C/I/O demand bars: vertical, bipolar, coloured as in the zone colours.

**Info views panel**
- Choose an info view; this sets `InfoViewLayer.Mode`.
- Shows a legend.
- `InfoViewLayer` renders a per-cell heat overlay:
  - Coverage, land value and pollution use the coverage maps.
  - Power and water: each building's cell is green or red.
  - Traffic: per road cell load.
  - Happiness: per building.

**Budget panel**
- Last month's income and expenses by category.
- Tax sliders per category (0–30%), issuing `SetTaxOrder` on release.

**Selection**
- Clicking a building shows an info panel with:
  - name, zone and level
  - residents and jobs (current/max)
  - power, water and road status
  - happiness and land value

**Tools**
- Cancel with right-click or Escape.
- Show a cost preview near the cursor while dragging a road.

**Main menu** (`chrome/mainmenu.yaml`, your own logic, e.g. `CityMainMenuLogic`)
- Shellmap in the background.
- An "OpenCity" title (logo image) and the buttons New City, Load City, Settings, Extras, Quit.
- **New City** shows our Lobby-visibility maps (name, size, preview) and starts directly with
  `Game.CreateAndStartLocalServer(map.Uid, new[] { Order.Command("option gamespeed default"),
  Order.Command($"state {Session.ClientState.Ready}") })`. There is no lobby.
- **Load City** uses the common game-save browser.

**Camera at game start**
- Centre on the end of the first highway road.

The common `ingame.yaml` provides `INGAME_ROOT` and requires `PLAYER_WIDGETS` to contain
`LogicTicker@SIDEBAR_TICKER`. Widget IDs must be unique across all layouts. Chrome yaml `Text:`
values must be **fluent keys**.

---------------------------------------------------------------------------------------------------

## 9. Terrain and maps (A1)

**Tileset `TEMPERATE`** (`tilesets/temperate.yaml`)
- `TileSize: 32,32`.
- Terrain types (keep): `Clear`, `Rough`, `Water`, `Road`. You may add `Beach` (buildable like Clear,
  so add it to building and road terrain lists), but only coordinate through the integration notes.
- The first template must be plain grass.
- Provide several grass variants, meadow, dirt, sand and beach, water and deep water, plus
  shoreline tiles selected by land-neighbour mask so coasts look smooth.
- The map generator is the only consumer of template IDs, so A1 chooses them.

**map.bin format** (little-endian, see `Map.SaveBinaryData`)
- Header `<BHHIII` = `(2, W, H, 17, 0, 17 + 3*W*H)`.
- Then `for x in range(W): for y in range(H):` write `<HB` (template id, tile index).
- Then 2 zero bytes per cell (resource type, density), in the same x-major order.
- Tile index 255 is reserved.
- `MapSize` includes a 1-cell border; use `Bounds: 1,1,W-2,H-2`.
- `maps/boot-test` is a working example (generated inline by the lead).

**Maps** (generated by `tools/genmap.py`, writing `map.yaml` + `map.bin`)
- Format: `MapFormat: 12`, `RequiresMod: city`, `Tileset: TEMPERATE`, `Categories: City`,
  `LockPreview: True`, with a `map.png` preview generated from the terrain.
- Players: a `Neutral` player (`OwnsWorld`, `NonCombatant`, `Faction: city`) and one `Multi0` player
  (`Playable`, `Required`, `AllowBots: False`, `LockFaction: True`, `Faction: city`).
- `green-valley` (≈128×128, the main map): a river, a lake and forests.
  - The **first actor** is `highway-w` on the west edge (x = 1). Its road (12 cells east) ends at
    "anchor".
  - **Guarantee that the rectangle anchor + (−1..36, −10..10) is clear land with no trees or water.**
    The autotest scenario builds there.
- At least two more maps with different layouts (for example `lakeside` and `riverbend`), each with a
  highway on some edge.
- `shellmap` (`Visibility: Shellmap`): a small, attractive pre-built town for the main menu
  background.
  - Roads via `roadseed` actors.
  - Growables, services and trees owned by Neutral.
  - Camera motion via the `CameraOvalMover` world trait in the map's `Rules:`.

---------------------------------------------------------------------------------------------------

## 10. How to build and test

From the repo root:

```bash
make all                                   # first time in a fresh worktree (builds engine + City)
dotnet build OpenRA.Mods.City/OpenRA.Mods.City.csproj -c Release -nologo -p:TargetPlatform=linux-x64   # fast incremental
./utility.sh city --check-yaml             # MUST end with "Errors: 0" (warnings are fine)
mods/city/tools/autotest.sh boot-test "ticks=2000;shots=300,2000;timestep=5;log=250"
```

- **`autotest.sh`** runs the game **headless** (SDL offscreen, no window, no sound) with the
  `CityAutoTest` harness.
  - It issues the standard scenario orders: roads, zones, services, speed 3.
  - It logs city stats every `log` ticks and saves screenshots at the `shots` ticks, centred on the
    test area.
  - It prints any exception log, then exits.
  - **Look at the screenshots with the Read tool** to verify visuals.
  - `scenario=bulldoze` also bulldozes a road segment and dezones some cells.
- For your own experiments, run your own copy of the harness. For example, put a scratch
  `MyDebugTest` world trait in your own files, but do **not** commit scratch traits.
- `boot-test` and `boot-shellmap` are bootstrap maps (flat grass, water on the east side,
  `highway-w` at 1,48).
- Run `dotnet build -c Debug` on the City project at the end. It enforces OpenRA code style; fix the
  warnings in your files.

**Code style** (OpenRA conventions)
- Tabs, Allman braces, and the license header on every `.cs` file.
- `[Desc]` on trait info fields.
- Implement `ITick`, `INotify*`, `IRender*` and similar engine interfaces **explicitly**
  (`void ITick.Tick(Actor self)`).
- `readonly` where possible.
- No LINQ in per-tick hot paths.
- Create and dispose actors inside `world.AddFrameEndTask`.

**Git**
- You work in an isolated git worktree. Commit your work there (several commits are fine) with
  clear messages.
- Do not push.
- Never use `git stash`.

**Final report** (keep it tight)
1. What you built.
2. Files touched.
3. How you verified it (lint result, autotest stats, which screenshots you checked).
4. Known issues.
5. **Integration notes** for other WPs.
