# 10 - Environment, time and information UI

Research and design for OpenCity (OpenRA mod `city`), domain 10 of the CS2 parity effort: pollution, climate and weather, the clock and day/night, disasters (lite), info views, statistics, notifications, Chirper, citizen and building inspection, budget and demand panels, tutorials. Domain numbers 01 (citizens), 03 (networks), 04 (zoning), 08 (public transport) follow their design files; the rest are by role.

Confidence tags used below: **[W]** = documented on the CS2 wiki or a Paradox dev diary, **[C]** = community measurement or guide, **[I]** = my inference or memory, verify before relying on it. CS2 publishes few internal numbers; where it does not, I say so and propose OpenCity values.

---

## 1. CS2 mechanics

### 1.1 Pollution

Five types [W: cs2.paradoxwikis.com/Pollution]: ground, air, noise, surface water, groundwater.

| Type | Sources | Spread | Decay | Effects |
|---|---|---|---|---|
| Ground | Industry (not farming/forestry), fossil power, garbage facilities, sewage plants | Stays put; **not wind-driven** | Slow, "takes some time" after the source goes; flowing water removes it | Health down, trees die, Fertile Land destroyed, leaks into water |
| Air | Traffic, industry, fossil power, garbage, sewage | **Advected by wind** (static field in 1.0 [C]), dilutes | Fades; **rain reduces it** | Health down near homes |
| Noise | Traffic (trucks louder), industry, busy commercial | Local, follows roads | Instant when source stops | Well-being down, residential most sensitive |
| Surface water | Sewage, ground pollution on water | Flows with water, dilutes | Dilution | Polluted supply "quickly reduces health of all citizens" |
| Groundwater | Dirty building over a deposit | Per deposit | Recovers at the deposit's replenishment rate | Same, if pumped |

Mitigation [W]: electric vehicles, road trees, sound barriers, tunnels (elevated roads add noise), intakes upstream of sewage outlets, homes upwind of industry. Pollution also lowers rent and land value as "urban blight" [C]. Not published [I]: grid resolution, per-building amounts, decay rates, health curves. 

**Happiness** = Well-being + Health [W: Dev Diary 11]. Well-being: utilities, pollution, crime, garbage, goods, housing size, leisure. Health: pollution exposure, sewage, garbage, medical coverage; sickness is random and rarer at high health.

### 1.2 Climate, seasons, weather, time

From Dev Diary #8 [W: colossalorder.fi/?p=1788] and the Climate wiki:

- Per-map **climate** (Temperate, Continental, Polar) sets seasons, day length (longer winter nights) and weather.
- **One day-night cycle = one month; a season is about three months; a year is twelve in-game days.**
- About **72 min real time per day/month at speed 1** [C: Steam guide 3072493428; Paradox: "a bit over an hour"]; 1 in-game hour = 3 real minutes. Speed 3 is 3x per Paradox (about 24 min), 4x per the guide [conflict]. Inferred [I]: 262,144 simulation frames per day at 60 fps.
- **Electricity demand follows temperature** [W]: lowest at 18-22 degC, up to **200%** at or below -18 degC or at or above 58 degC. Cloudy days cut solar by **25%**. No heating network in CS2 1.0.
- Citizens prefer indoor leisure when cold or raining. Rain cuts air pollution. Snowplows keep roads open.
- Citizens follow daily schedules (sleep, work, school, shop, leisure); the Traffic view shows a flow-through-the-day chart.

### 1.3 Disasters (optional per game) [W]

Forest fire (dry and warm; firewatch towers, helicopters), hail storm (cold, not freezing; damage and accidents), tornado (worst; shelters, early warning). No frequencies published.

### 1.4 Info views, statistics

33 info views [W: wiki Info_views]: **Services** (Roads, Traffic, Electricity, Water & Sewage, Healthcare, Garbage, Fire, Disaster, Police, Administration, Education, Transportation, Post, Telecom, Leisure, Tourism, Outside Connections), **Zones/economy** (Residential, Commercial, Industrial, Office, Building Level, Land Value, Company Profitability, Natural Resources), **Citizens** (Population, Happiness, Wealth, Workplace Availability), **Pollution** (Air, Ground, Noise, Water). Build menus auto-open the related view. Legends: buildings/roads **green-to-red**; terrain **blue-to-white** (land value, density), **green-to-black** (workplaces, water pollution), **blue-to-red** (pollution), **white-to-orange** (pollution emitted by buildings). Air view adds wind arrows, water view flow arrows. Each view has a panel with charts (age chart, education capacity, jobs per education level, building levels per zone, average profit, resource utilisation).

Statistics: a menu panel with expandable category tree, charts and a time-scale slider; Finance holds income and expense groups [C: patch notes only, category names unconfirmed].

### 1.5 Notifications

Icons over buildings in **7 tiers** by background colour: Minimal (grey), Info (light blue), Problem (yellow), Warning (orange), Major Problem (red), Error, Fatal [W: wiki Notifications]. Examples: not enough customers, no workers, rent too high (Info); abandoned, collapsed, no road access, leveled up (Problem); not enough electricity, dirty water, traffic jam, air/noise/ground pollution "affecting occupants" (Warning); no running water, garbage piling up, waiting for ambulance, on fire, power overload (Major). A prolonged problem ends in abandonment.

### 1.6 Chirper, inspection, economy UI, tutorials

- **Chirper** [W: Dev Diary 11]: citizens and services post about real events (missing or restored services, life events such as new job, move, graduation). **Likes** show importance, so many likes mean a widespread problem. Players report queue lag and over-dramatic single-building complaints [C].
- **Citizen panel** [W]: name, home, workplace link, happiness (hover shows factors), health, conditions (sick, homeless, distressed...), age stage, education level (5). **Follow** adds the citizen to a list under Chirper with a **Lifepath Journal** (graduate, job, marriage, move) and their feed. Activities: work, school, shop, sleep, study, leisure, medical, move. Exact current-action wording not documented.
- **Building panel** [W/C]: efficiency (hover for needs), employees by education, upkeep breakdown (patch 1.1.5), level and condition (rises while tenants pay full upkeep); zoned levels 1-5.
- **Economy** [W]: tabs Budget, Taxation (-10 to 30%, per zone and education level), Loans, Production. **Demand bars** with a City Information panel listing positive and negative factors [C].
- **Tutorials** [W: colossalorder.fi/?p=1924]: balloons (green outline), task cards, center cards, hints; **Advisor** (question mark) lists unlocked tutorials in five groups (Services, City, Info Views & Notifications, Citizens, Interface & Tools); green "new" markers.

---

## 2. Current OpenCity state and gaps

### 2.1 What exists

| Area | Current code | Gap |
|---|---|---|
| Pollution | `CityCoverageLayer.Recompute()` every 100 ticks re-stamps one byte map from `CityBuildingInfo.Pollution` (ind 20/30/40, coal 70), linear falloff radius 6, combine `a+b-ab/100` | One type, no memory, wind, decay, noise, groundwater, traffic link, health link |
| Effects | Happiness `-pollution*30/100` (not industrial); land value `-pollution*40/100` | no crops, trees, health |
| Time | `CityManager`: 25 ticks = 1 day, 30 days = 1 month (30 s at 1x, 7.5 s at 3x); speeds 40/20/10 ms; `CityDate` "d Mon yyyy" | no hours, day/night, seasons, weather |
| Rendering | `InfoViewLayer` (`IRenderAboveWorld`, `TerrainSpriteLayer`, 11-frame red-yellow-green `heat`); `WithCityStatusIcons` (5 icons, cycling) | `world.yaml` has no `TintPostProcessEffect`, `TerrainLighting` or `WeatherOverlay` |
| Info views | 11 modes, button grid, one legend strip with low/high text | one ramp, no per-view legend or chart |
| Stats | live values; `CityManager.Notifications` (max 20) stored but **never displayed**; milestones go to transient lines | no history |
| Panels | Budget (lists + tax sliders), Info views, Building (static rows); D2k sidebar with demand bars, minimap, cash/date strip | engine `LineGraphWidget` available |
| Citizens | none (integers per building; `Households = pop/3`) | Chirper and citizen panel need domain 01 |

### 2.2 Engine facts

- `TintPostProcessEffect` multiplies the frame by `(R,G,B)*Ambient` in pass `AfterActors`; its fields are **public and mutable**. It runs **before** `IRenderAboveWorld`, shroud and annotations, so the info view overlay, status icons, selection and HUD are **not** darkened. Roads and zone overlays (`IRenderOverlay`, drawn by `TerrainRenderer`) are inside the tinted pass.
- `TerrainLighting`'s tint is readonly (unsuited to a clock). `FlashPostProcessEffect` is for bursts. `WeatherOverlay` is a particle rain/snow `ConditionalTrait`, enabled via `ExternalCondition`.
- `BlendMode.Additive`/`Screen` exist for night glow. `world.WorldTick` is synced and stops on pause, so a clock derived from it needs no sync state. Render-only code may read sim state, never write it.

---

## 3. Proposed design

### 3.1 Time model: `CityClock`

**Problem.** Today 1 day = 25 ticks = 1 s at speed 1. Domain 01 (`01-citizens.md`) also assumes "25 ticks = 24 sim hours". At that rate a day/night cycle would flash by in one second (0.25 s at speed 3), and a 40-cell car trip (about 425 ticks at `Mobile.Speed 96`) would last 17 "days". Day length must be decoupled from the 25-tick economy pulse.

**Proposal.** One world trait `CityClock` (new file `Traits/World/CityClock.cs`) is the **single source of time**. It is a pure function of `world.WorldTick` (synced, stops on pause) plus map-configurable offsets, so it needs no sync fields and no orders.

```
TicksPerHour   = 100          (rules, default)
HoursPerDay    = 24
TicksPerDay    = 2400         -> 96 s at 1x (25 tps), 48 s at 2x, 24 s at 3x (100 tps)
DaysPerMonth   = 1            (CS2 rule: one day-night cycle IS one month)
MonthsPerYear  = 12, SeasonMonths = 3
StartMonth/StartHour = 3 (March) / 7:00
PulseTicks     = 25           (the existing "DailyUpdate" cadence, kept as a quarter-hour pulse)
```

Derived (all integer): `tickOfDay = (WorldTick + StartOffset) % TicksPerDay`; `Hour = tickOfDay / TicksPerHour`; `Minute = tickOfDay % TicksPerHour * 60 / TicksPerHour` (1 tick = 0.6 game minutes); `DayIndex`, `Month`, `Year`, `Season` (0 Spring ... 3 Winter), `SeasonProgress` (0..99). Fires C# events `HourChanged`, `DayChanged` (= month end), `SeasonChanged` used by sim and UI (handlers must not mutate sim state outside the synced tick).

Real-time table at the recommended values (25 ticks/s at speed 1, 100 ticks/s at speed 3):

| Unit | Ticks | 1x | 2x | 3x |
|---|---|---|---|---|
| Hour | 100 | 4 s | 2 s | 1 s |
| Day = month | 2,400 | 96 s | 48 s | 24 s |
| Season (3 months) | 7,200 | 4.8 min | 2.4 min | 1.6 min |
| Year | 28,800 | 19.2 min | 9.6 min | 6.4 min |

CS2 needs 72 min per month at 1x; OpenCity is about 45x faster for short sessions but keeps the ratios (day = month, season = 3 months).

**Why `DaysPerMonth = 1`.** It mirrors CS2: day/night, taxes, monthly statistics and seasons share one beat. All "monthly" work (taxes, upkeep, ring-buffer sample) fires at 00:00. The value stays a rules option.

**Compatibility.** `CityManager.DailyUpdate` keeps firing every `PulseTicks = 25`, now a **quarter-hour pulse**. Flags for other domains:

1. Counters named "days" (20-day level-up, 45-day abandonment) are really pulses; keep them as pulses (no balance change) or convert at 96 pulses per day.
2. Real-time income must be preserved: the old month was 30 pulses (30 s at 1x), the new one is 96 pulses, so monthly tax and upkeep constants need about x3.2. Domain 02 tunes it; I only require `CityClock.MonthLengthTicks` to be read, not hard-coded.
3. Domain 01's bucket `id % 25` becomes `id % PulseTicks`; schedules key on `Hour` ("08:15"), not "tick 8".
4. Calibrate vehicle speed to the clock: trips should take 25-150 ticks. At `Speed 96` a car covers 0.094 cells/tick, so 40 cells take 425 ticks (4.2 h). Use `Mobile.Speed` about 300-400 or `TicksPerHour = 200` (domains 03/05 decide; one rules line).
5. `CityDate` gains `Hour`, `Minute`; the HUD date label shows `Mon yyyy 14:30` (84 px strip, TinyBold).

Pause and speed are unchanged.

### 3.2 Climate, seasons, weather (sim)

New world trait `CityClimate` (`Traits/World/CityClimate.cs`), synced integer state, updated once per game hour (`HourChanged`).

**Static data** (rules on the world actor, overridable per map via `Rules:` in `map.yaml`): a `ClimateInfo` with 12 monthly rows. Default **Temperate** (degC x10 mean day temperature, daylight hours, precipitation chance %):

| Month | Mean temp | Day length (h) | Rain/snow chance per day |
|---|---|---|---|
| Jan | -20 | 8 | 35 |
| Feb | 0 | 9 | 30 |
| Mar | 60 | 11 | 35 |
| Apr | 110 | 13 | 35 |
| May | 160 | 15 | 30 |
| Jun | 200 | 16 | 25 |
| Jul | 230 | 16 | 20 |
| Aug | 220 | 15 | 25 |
| Sep | 170 | 13 | 30 |
| Oct | 100 | 11 | 35 |
| Nov | 40 | 9 | 40 |
| Dec | -10 | 8 | 40 |

`continental` and `polar` presets are rules snippets. All values are design choices [I], not CS2 data.

**Dynamic state** (all `[VerifySync]` ints): `TemperatureX10` = monthly mean + diurnal curve (cold at 05:00, warm at 15:00, amplitude 40-60 in x10) + a slow weather offset random-walked once per day with `SharedRandom` (+-50). `Cloud` 0..100 and `Weather` enum `Clear, Cloudy, Rain, Snow, Storm` from a small Markov chain advanced hourly with `SharedRandom` and the day's precipitation chance. Precipitation is `Snow` when `TemperatureX10 <= 10`, else `Rain`. `SnowDepth` 0..100: +4 per snowing hour, -1 per hour above 2 degC, -3 per hour above 8 degC. `WindDir` (0..7, eight compass directions) and `WindSpeed` (0..3): CS2 has a static wind; we use a **slow drifting wind** that changes at most one step every 2-3 game days so air pollution visibly moves, but a map-rules option `FixedWind: true` reproduces CS2.

**Effects** (each is one read of `CityClimate` in someone else's code):

| Effect | Formula | Consumer |
|---|---|---|
| Electricity demand | `percent = 100 + 100*(18-T)/36` for T < 18 degC, `100 + 100*(T-22)/36` for T > 22 degC, clamped 100..200 (CS2 curve [W]) applied to `PowerUse` of every consumer in `DistributeUtilities` | `CityManager` (domain 02/utilities) via `CityClimate.PowerDemandPercent` |
| Solar | output x 75% when `Cloud > 60` (CS2: -25% cloudy [W]), x 0 at night, x 50% under snow cover > 50 | `UtilityProducer` |
| Air pollution wash | rain: air decay x 3 | `PollutionLayer` (3.4) |
| Park and leisure use | outdoor leisure weight x 0.3 when `Rain/Snow` or `T < 0`; indoor x 1.5 [W qualitative] | citizen sim (domain 01) |
| Traffic | vehicle speed x 85% when snow depth > 30 and no road maintenance; x 92% in rain (optional, G) | traffic |
| Crops | farm yield by season: growing months only (Apr-Sep), x 0 under snow | industry (domain farming) |

"Snow affecting heating": CS2 1.0 has no heating buildings, so heating is **only the electricity multiplier**. Winter blackouts then arrive naturally (+55% at -2 degC, the January mean; +78% at -10 degC), which is an emergent story for one multiplier.

### 3.3 Day/night and season visuals in OpenRA

New **render-only** world trait `CityAtmosphere` (`Traits/World/CityAtmosphere.cs`, `ITickRender`), added to `world.yaml` together with the engine `TintPostProcessEffect`:

```yaml
World:
	TintPostProcessEffect:       # engine trait, AfterActors pass
	CityClock:
	CityClimate:
	CityAtmosphere:
```

Each render frame it finds the `TintPostProcessEffect` instance once and sets `Red/Green/Blue/Ambient` from `CityClock` with **sub-tick interpolation** (use `tickOfDay` plus the fraction `(Game.RunTime - lastTickTime) / Timestep`; reading the clock is a render-side read, fine). Key colours (linear multipliers, hour of day):

| Time | R | G | B | Ambient |
|---|---|---|---|---|
| Night 00-04 | 0.45 | 0.52 | 0.80 | 0.75 |
| Dawn 06 | 1.00 | 0.82 | 0.72 | 0.92 |
| Day 10-15 | 1.00 | 1.00 | 1.00 | 1.00 |
| Dusk 19 | 1.00 | 0.78 | 0.66 | 0.92 |
| Night 22 | 0.48 | 0.54 | 0.82 | 0.78 |

Piecewise-linear between keys, anchored to sunrise = 12 - dayLength/2 and sunset = 12 + dayLength/2 from the climate table. Weather multiplies in: `Cloud` lowers Ambient by up to 10%, `Storm` by 20% plus grey shift. Winter shifts blue +0.05 (cold light), summer red +0.03.

**Not darkened** (because the tint runs before them): info view overlay, status icons, selection, HUD. Therefore an info view looks fine at night.

**Info view dimming.** When `InfoViewLayer.Mode != None`, `CityAtmosphere` multiplies Ambient by 0.6 and desaturates by lerping RGB toward a grey, so heat colours pop (CS2 greys the world the same way).

**Night lights** (optional, phase T3): a second render trait `CityNightLights` (`IRenderAboveWorld`, runs after the tint pass) draws small **additive** glow sprites (`BlendMode.Additive`, image `lights`, 32x32) at lit windows of operational buildings with power (`HasPower`), at street-lamp cells (every 3rd road cell by `Hash(cell)`) and at car headlights, with alpha = `darkness` (0..255 from the clock) and a stable per-building hash deciding which windows are lit. Unpowered buildings stay dark: a free blackout visual.

**Seasons** (phase T3, art-light):

- Terrain: ship a `SNOW` overlay sequence using the existing `TerrainSpriteLayer` approach: a world trait `SeasonOverlay` (`IRenderOverlay`, so it is drawn with the tinted pass) draws a translucent white-blue cell sprite (`overlays/snow`, 4 variants by cell hash) on non-road, non-water cells with alpha = `SnowDepth`. Updated only when `SnowDepth` bucket changes (8 buckets) or on road changes: cost is one pass over `AllCells` per bucket change. Roads get a thinner snow tint with a "plowed" strip (if road maintenance exists, domain 03).
- Trees: sprites are RGBA with no palette shift, so a small `WithSeasonTint` trait (`IRenderModifier`, per-sprite tint vector) turns `tree-*` fresh green (spring), deep green (summer), orange-brown (autumn), grey-white (winter). No extra art.
- Weather particles: two engine `WeatherOverlay` traits on the world actor, enabled by `ExternalCondition` grants from `CityAtmosphere`: `rain` (lines, `UseSquares: false`, gravity 9-12, blue-grey colours, density 10) and `snow` (squares, white, gravity 1.5-3, wind 2-5, density 8). Wind level maps from `WindSpeed`. Both are `IRenderAboveWorld`, so they are not tinted; modulate the particle colours' alpha in rules, not code.
- Storm lightning: `FlashPostProcessEffect` (engine) triggered with `Enable(ticks)` at random hours during `Storm`.

**Cost:** one full-screen shader draw for the tint; `CityNightLights` draws visible buildings only (about 1,000 sprites worst case).

### 3.4 Pollution model: `PollutionLayer`

A new world trait `PollutionLayer` (`Traits/World/PollutionLayer.cs`) **owns four per-cell layers** (`ushort[w*h]`, 0..1000 internally for slow decay; public API 0..100) and keeps `CityCoverageLayer.GetPollution(cell)` as a facade returning the worst of ground/air/noise, so existing callers keep working. Integer maths, index-order iteration, a rolling `[VerifySync] int StateHash`. **Cadence:** emission, diffusion and decay once per pulse (25 ticks); noise every 4th pulse. Cost at 128x128 about 0.3 ms per pulse (1.2 ms at 256x256); spread over row bands if profiling asks.

| Layer | Emission per pulse | Spread | Decay |
|---|---|---|---|
| `Ground` (persistent) | sources stamp `emission*(R-d)/R`, R=5 cells, into the cell value (cap 1000) | none (not wind-driven, as CS2); 1/16 blur to 4 neighbours every 4th pulse | `v -= v/768 + 1`: half-life about 5.5 game days, so a removed source fades over days |
| `Air` (persistent, advected) | industry by output, fossil power, garbage, **traffic** `TrafficLoad(cell)*AirPerCar` | each pulse `WindSpeed*12%` (levels 0..3) of a cell moves one cell along `WindDir` (8 directions), then diffusion `0.6 self + 0.1 per 4-neighbour`; open map edges | `v -= v/32` (half-life about 5.5 game hours); rain/storm adds `v/10`, snow `v/20` |
| `Noise` (instant) | per road cell `TrafficLoad*NoisePerCar` (trucks x2), industry, busy commercial; stamp radius 3-5 | recomputed from scratch every 4th pulse, combined `a+b-ab/100`; parks and trees subtract 8 per adjacent tile | n/a; road `Underground` flag (domain 03) removes it |
| `Groundwater` (quality) | ground above 300 seeps: `gw += (ground-300)/64` | static aquifer (lite) | `gw -= max(1, deposit/64)`; `deposit` is a static 0..100 field from terrain (high near water) |
| `WaterSurface` (T4, optional) | sewage outlets, ground pollution on water cells | diffusion over connected water cells only | `v -= v/64` |

Pumps and towers sample `WaterQuality = 100 - avg(gw)` over their radius: below 70 raises "Dirty water", below 40 gives a city-wide health penalty weighted by that pump's share of supply.

**Emission data.** Replace the scalar `CityBuildingInfo.Pollution` with a rules struct (domain 04/02 own the actor rules, I own the keys):

```yaml
Pollution:
	Ground: 40
	Air: 30
	Noise: 25
	Scale: Output       # Fixed | Occupancy | Output
```

`Scale` makes an empty or abandoned factory stop polluting. Back-compat: old `Pollution: N` maps to `Ground: N, Air: N/2`.

**Effects** (read via `PollutionLayer.Exposure(cell)` returning `(ground, air, noise)`, 0..100):

| Consumer | Rule |
|---|---|
| Land value | `-(ground*25 + air*25 + noise*15)/100` (replaces `-pollution*40/100`) |
| Building happiness | residential/office `-(ground+air)*25/100 - noise*15/100`; commercial half; industrial none |
| Citizen health (domain 01) | per citizen per day at home: `health -= (ground+air)/20` plus water-quality penalty; clinics recover |
| Trees, fertility | trees on cells with `ground > 600` for 3 game days die (`AutoClear` path); farm `Fertility = Base - ground/2` |
| Notifications | `Pollution` Warning icon when one exposure exceeds 50 (residential) or 70 (commercial) |
| Demand | pop-weighted average exposure above 30 lowers residential attraction by 1 per 5 points |

Levers for the player: clean power, electric vehicles (`AirPerCar x 0`), parks and trees (air decay x1.5 within 2 cells), sound barriers, putting industry downwind (the air view's wind arrows show where).

---

### 3.5 Info views catalog

Keep `CityInfoView` (frozen enum in `CityTypes.cs`; the lead appends values) and `InfoViewLayer.Mode`. Replace the single ramp with a **`InfoViewDef` table** (one static list in `Widgets/InfoViews.cs`, shared by layer and panel): `{ Mode, NameKey, Ramp, Source delegate, Legend labels, SidePanelId }`. The layer gets cell values from `IInfoViewSource` (below), so domains expose data without the layer knowing internals.

Ramps are separate 11-frame sequences in `overlays` (`heat` stays; add `ramp-pollution` clear to orange to brown, `ramp-blue` for density/land value, `ramp-green` for availability). Legend strip colours must match them (extend `HeatLegendWidget` with a `Ramp` property).

| View | Data source (owner) | Cell logic | Ramp / legend | Phase |
|---|---|---|---|---|
| Power, Water (exists) | `CityBuilding.HasPower/HasWater` (02) | building cells green/red | heat | done |
| Coverage x5 (exists) | `CityCoverageLayer.GetCoverage` | per cell | heat | done |
| Land value (exists) | `GetLandValue` | terrain cells | **blue-white** (CS2) | T2 restyle |
| Happiness (exists) | `CityBuilding.Happiness` | building cells | heat green-grey | done |
| Traffic (exists) | `TrafficManager.GetTrafficLoad` | road cells; add flow vs volume toggle | heat inverted | done |
| **Pollution: Air** | `PollutionLayer.Air` | terrain blue-red + source buildings white-orange + **wind arrows** (8 glyphs every 6 cells) | pollution | T1 |
| **Pollution: Ground** | `PollutionLayer.Ground` | same without arrows | pollution | T1 |
| **Pollution: Noise** | `PollutionLayer.Noise` | road and building cells | pollution | T1 |
| **Pollution: Water** | `Groundwater` + `WaterSurface` | green-black on water/pump radius | green-black | T4 |
| Residential / Commercial / Industrial / Office | zones (04) + occupancy | zone cells tinted by occupancy fill (empty to full) | heat | T2 |
| Building level | `GrowableBuilding.Level` | building cells L1 red to L3 green (CS2: 5 levels) | heat | T2 |
| Population | citizen sim (01): density per 8x8 block | terrain blue-white | blue | T2 |
| Citizen wealth | citizen sim (01) | building cells | heat | T3 |
| Workplace availability | free jobs per building (01) | green-black | green | T2 |
| Company profitability | industry domain | building cells | heat | T3 |
| Natural resources | resource map (industry domain) | fertility yellow, wood green, ore blue, oil black | 4 colours | T3 |
| Education, Health, Fire, Police | exists as coverage; add **panel chart** (capacity vs demand) | | heat | T2 |
| Garbage, Transport, Leisure, Tourism, Outside connections, Post, Telecom, Disaster | their domains | per domain | per domain | T3+ |

`IInfoViewSource` (new interface in `OpenRA.Mods.City`): `bool TryGetCell(CityInfoView mode, CPos cell, out int value0to100)` plus `int Version` so `InfoViewLayer` rebuilds only when versions change (already done for coverage and roads). Sources are world traits found once via `TraitsImplementing<IInfoViewSource>()`.

**Panel redesign (D2k style).** Keep the 556x194 `CITY_INFOVIEWS_PANEL` pattern but make it **grouped**: tabs along the top of the panel (Services, Zones, Citizens, Environment) as `Button`s using `command-button`; the mode grid below; **legend strip with real labels** (`LEGEND_LOW`, `LEGEND_HIGH` from the def, e.g. "Clean" / "Polluted"); and a **side summary block** per view (3 numbers + one `LineGraph` of the last 12 months). Hotkey `I` opens, `Shift+I` cycles to the next view. When the panel opens through a build tab (as in CS2), the matching view activates (`ConstructionTools` tab -> `InfoViewLayer.Mode`), and clears on cancel.

### 3.6 Notifications and problem icons

Extend `WithCityStatusIcons` from 5 fixed frames to a **catalog with severity**. New data type `CityProblem` (enum, `OpenRA.Mods.City`) and `CityBuilding.Problems` (a `uint` bit mask, recomputed each pulse in the sim, **not** in the renderer). The renderer only reads the mask.

| Id | Icon | Tier | Trigger (default thresholds) |
|---|---|---|---|
| NoPower | bolt | Warning, Major if 3 days | `!HasPower` |
| NoWater | drop | Warning | `!HasWater` |
| NoSewage / Dirty water | drop-dirty | Warning | sewage domain; `WaterQuality < 70` |
| NoRoad | sign | Problem | `!HasRoadAccess` |
| Abandoned | boards | Problem | `Growable.Abandoned` |
| Unhappy | frown | Info, Warning below 15 | `Happiness < 25` |
| NoWorkers | people-slash | Info | workplace fill < 50% for 5 days |
| NoCustomers | cart-slash | Info | commercial sales < 30% |
| NoGoods / High resource cost | crate | Problem | industry domain |
| Garbage | bag | Major | garbage domain |
| Fire, Crime scene, Waiting ambulance | flame, mask, cross | Major / Info / Major | services |
| Traffic jam, Accident | car-red | Warning | traffic |
| AirPollution, GroundPollution, Noise | cloud, skull-ground, speaker | Warning | exposure > 50 (res) / 70 (com) |
| LeveledUp | arrow | Problem tier colour green, 4 s | `UpgradesTo` fired |

Rendering: a 20x20 **tier tile** sprite (one per tier) with the 16x16 glyph drawn on top (composited at runtime so art is N glyphs + 5 tiles, not N x 5). Show the **2 worst** problems, alternating, sorted by tier; fade at low zoom and **cluster** per 4x4 block with a count.

**Notification feed.** Reuse `CityManager.Notifications` (exists, max 20, unused). New record: `{ Tick, Kind, Severity, Cell (or -1), ActorId, TextKey, Args }`. Sim code calls `CityNotifier.Post(kind, severity, cell, args)`, which de-duplicates by `(kind, cell-bucket)` for 3 game hours. A small **alert strip** above the minimap shows the latest 3 with tier colours; clicking one calls `Viewport.Center(cell)` and selects the actor. `AddTransientLine` stays for milestones and money warnings.

---

### 3.7 Statistics history

New player trait `CityStatistics` (`Traits/Simulation/CityStatistics.cs`, synced). **Ring buffers of `int`, one slot per game month** (= per day, `DayChanged`), 12 slots per year, `Years = 10` by default (120 slots). Series are registered by id so other domains add their own with one line:

```csharp
stats.Register("population", () => manager.Population);     // sampled at month end
stats.Register("money", () => manager.Funds);
```

Default series: population, households, workers, unemployed, jobs, funds, income, expenses, tax by category, average happiness and health, power and water produced/used, pollution (ground/air/noise, pop-weighted), land value, traffic load, vehicles, buildings by zone, demand R/C/I/O, crime, temperature. About 30 series x 120 slots x 4 B = 14 KB. A 24-slot hourly sub-sample of the last day feeds the Traffic view's "flow through the day" chart.

Panel `CITY_STATS_PANEL` (`city-panels.yaml`, `CityStatsLogic`): left a `ScrollPanel` category tree (Population, Economy, Utilities, Environment, Traffic, Services); centre the engine `LineGraphWidget`; time-scale buttons (1 year / 5 years / all) replace CS2's slider; up to 4 coloured series via checkboxes. Opened by a new sidebar order button `ORDER_STATS` (hotkey `G`). Ring buffers are synced state, so replays regenerate them.

### 3.8 Chirper

A **sim-side event feed + unsynced UI**. Sim posts `ChirpEvent { Tick, Author, Kind, TextKey, Args, Cell, Likes }` into `CityManager.Notifications`-style ring (200 entries) in new file `Traits/Simulation/CityChirper.cs` (player trait, synced because it feeds saves/replays). Authors: a citizen id (from `CitizenSim`), the city service name, or "City Hall". Posting rules (only integers, de-duplicated per `(kind, area bucket)` per game day, max 6 per day):

| Kind | Trigger | Likes (importance) |
|---|---|---|
| Service lacking | NoPower/NoWater/Garbage/Crime problem affecting N buildings | `N` (affected buildings or citizens) |
| Service good | coverage > 80% after being low | 3 |
| Pollution | pop-weighted exposure > threshold | exposed citizens / 10 |
| Traffic | average load > 60 on commute roads | cars / 20 |
| Taxes high | tax > 15% for a category | 5 per point above |
| Life event | followed citizen only: new job, move, graduate, marriage, birth, death | 1 |
| Milestone, disaster, first snow, heat wave | system / `CityClimate` | 20 / 2 |

**Text** is Fluent keys (`chirp-power-N`, ...) with 2-3 variants chosen by `Hash(tick)`; sim stores key + args, UI formats, so the language stays in `fluent/chirper.ftl`. A **Chirper panel** (D2k scroll list, 260x200, collapsed to a bird icon in the strip above the minimap that blinks on new posts) shows the last 20 with author, text, likes, and a **locate** button that centres the camera on `Cell`. Citizens' events are filtered to **followed** citizens plus a 10% sample of others (keeps the feed readable). Provide a toggle in settings (`Game.Settings`) to hide it.

### 3.9 Inspection panels

**Building panel (extend `CityBuildingInfoLogic`).** Keep the row-list pattern (`CITY_INFO_ROW`) but make the row set a **per-kind template**: Residential (households, residents, vacancy, rent, happiness with **hover factor list**, power/water/road, level and level progress bar, pollution exposure, land value); Workplace (jobs by education filled/total, efficiency %, wages, production in/out, profit, level progress); Service (coverage radius, capacity used, upkeep, budget slider); Utility (output/used). Level progress = `levelUpTimer / required` exposed by `GrowableBuilding` (domain 04). The **happiness hover** lists up to 6 signed factors (e.g. "No power -30", "Parks +9", "Air pollution -7"), computed by the same function the sim uses (`CityManager.HappinessBreakdown(b)`), so UI and sim never disagree. A lower **Residents** sub-list (ScrollPanel, 6 rows, name, age stage, job icon) with a click-through to the citizen panel.

**Citizen panel** (`CITY_CITIZEN_PANEL`, `CityCitizenInfoLogic`). Citizens are plain data (domain 01), so selection works by (a) a click in the building's residents list or (b) clicking a **pedestrian or car actor** carrying a `CitizenRef { id }` trait (only sampled trips become actors). The panel reads `CitizenSim.Get(id)`: generated name (two Fluent lists, `Hash(id)`), age stage, education (5 levels), household, home and workplace links, happiness bar with factor tooltip, health, wealth, **current activity** (`Sleeping, Working, Commuting, Shopping, Leisure, At school, Going home, Seeking care, Moving`), **trip** (from, to, ETA = `ArrivalTick - WorldTick`) and conditions. Buttons: **Follow** (list of up to 5 under the Chirper bird; camera follow sets `Viewport.Center` each render frame while she is on an actor, else centres the building), **Home**, **Work**. The **lifepath journal** is a per-followed-citizen ring of 32 `{tick, kind, a, b}` events written by the citizen sim.

**Household panel**: a 3-line summary on the home building panel (members, combined income, rent, happiness).

### 3.10 Budget, demand panel, advisor

- **Budget panel (extend `CITY_BUDGET_PANEL`)**: tabs `Budget | Taxes | Services | Stats` (stats opens 3.7). Budget tab: income and expense lists with **expandable groups** (taxes by category, service fees; salaries, maintenance by service, loan interest) and a month selector (this month, last month, 12-month sparkline from `CityStatistics`). Tax sliders: widen range to -10..30 per CS2 [W] (domain 02 decides), optionally per education level later (domain 01). Service budget sliders (50-150%) for police/fire/health/education from domain 02.
- **Demand panel**: clicking the R/C/I/O bars (sidebar `DEMAND_BARS`) opens a small popup (`CITY_DEMAND_PANEL`) listing the **signed factors** per category (CS2 "City Information"). `CityManager.DemandFactors(cat)` returns up to 8 `(TextKey, int value)` pairs sorted by magnitude (jobs available, unemployment, education, taxes, happiness, vacancy, goods supply...), computed alongside the demand so the numbers add up to the bar. Today `UpdateDemand` already has the components: expose them, no new rules.
- **Advisor and tutorials** (T6): unsynced `CityAdvisor` with data-driven `tutorials.yaml` (id, group, trigger, steps). Step types: `Balloon` (anchor widget id, green outline), `TaskCard` (text, key hint, completion predicate over `CityManager` state, e.g. "zone 5 residential cells"), `CenterCard`. An Advisor button (question mark, sidebar order row) lists tutorials in the five CS2 groups. Triggers: first panel open, first milestone, first occurrence of a notification. Completion is stored client-side, not in the sim.

---

## 4. Interfaces with other domains

**What I publish (new, read-only for others unless noted).**

| API | Owner file | Consumers |
|---|---|---|
| `CityClock`: `Hour, Minute, TickOfDay, Season, MonthIndex, IsNight, DaylightPercent, TicksPerHour, event HourChanged/DayChanged` | `Traits/World/CityClock.cs` | citizens (schedules), traffic (rush hour), economy (monthly pulse), UI date |
| `CityClimate`: `TemperatureX10, Weather, Cloud, SnowDepth, WindDir, WindSpeed, PowerDemandPercent, SolarPercent` | `Traits/World/CityClimate.cs` | utilities (02), farms, citizens (leisure), traffic |
| `PollutionLayer`: `Ground/Air/Noise/Groundwater(cell)`, `Exposure(cell)`, `WaterQuality(cell range)`, facade `CityCoverageLayer.GetPollution` | `Traits/World/PollutionLayer.cs` | happiness, land value, citizen health, farms, water |
| `IInfoViewSource`, `InfoViewDef` table | `Widgets/InfoViews.cs` | every domain adds its view |
| `CityStatistics.Register(id, Func<int>)` | `Traits/Simulation/CityStatistics.cs` | every domain adds series |
| `CityNotifier.Post(...)`, `CityBuilding.Problems` mask, `CityProblem` enum | sim files | every domain raises problems |
| `CityChirper.Post(ChirpEvent)` | `Traits/Simulation/CityChirper.cs` | citizens, services, disasters |

**What I need from others.**

- **01 Citizens:** `CitizenSim.Get(id)` (age, education, household, home/work ids, happiness factor list, health, wealth, activity enum, trip `{from, to, ArrivalTick}`); life-event callback into `CityChirper` and the journal; `CitizenRef` on sampled actors; schedules keyed to `CityClock.Hour`.
- **02 Economy/utilities:** `DemandFactors(cat)`; `PowerDemandPercent` in `DistributeUtilities`; month-length re-base (3.1); sewage outlets as water-pollution sources; service budget sliders.
- **03/05 Networks, traffic:** `GetTrafficLoad` plus vehicle type and truck share; road flags `Underground`, `SoundBarrier`; speed calibrated to the clock; snow slowdown hook.
- **04 Zoning:** `GrowableBuilding.LevelProgress` (0..100); `Pollution:` emission keys in actor rules.
- **07 Industry:** resource map for the Natural Resources view; `Fertility(cell)` reads `Ground`; production fill for `Scale: Output`.
- **08 Public transport, 09 Services:** problem and chirp events, stats series, info sources (Transportation, Garbage, Disaster); fire and flood events.

All synced state added here is integer and `[VerifySync]`; render traits (`CityAtmosphere`, `CityNightLights`, `SeasonOverlay`, panels) are unsynced and must never write sim state. Orders added: none for MVP (all time/weather derive from `WorldTick` + `SharedRandom`); later `CitySetClimate` (map option) is a lobby/map rule, not an order.

---

## 5. Phased tasks and acceptance tests

Each phase is independently shippable, builds with `dotnet build -c Release`, passes `./utility.sh city --check-yaml` with `Errors: 0`, and runs the `autotest.sh` determinism replay (`CityAutoTest` replay mode) unchanged.

| Phase | Tasks | Acceptance tests |
|---|---|---|
| **T0 Clock (1 session)** | `CityClock` trait; `CityDate` gets Hour/Minute (additive field, lead edits frozen file); HUD date shows `HH:MM`; `PulseTicks` constant; rules `TicksPerHour/DaysPerMonth` | 1. Replay of 2,000 ticks: identical state hash twice. 2. With `StartHour 7` and `TicksPerHour 100`, hour 12 starts exactly at tick 500 (autotest log). 3. Pausing freezes the clock. 4. Changing speed does not change the hour at a given tick. |
| **T1 Pollution v2** | `PollutionLayer` (ground, air with wind, noise), `Pollution:` struct in rules (service + growable ind), CoverageLayer facade, 3 pollution info views with ramp + wind arrows, effects on happiness and land value | 1. Place coal plant: ground at its edge > 50 after 10 pulses; remove it: value < 50% after 6 game days and never negative. 2. Wind E, WindSpeed 2: air value 8 cells east > 8 cells west by at least 3x. 3. Rain halves the air half-life (log). 4. Road with 50 load shows noise 3 cells away > 20. 5. Determinism replay with `StateHash` equal. 6. Old `GetPollution` callers unchanged (build passes). |
| **T2 Info and notifications** | `IInfoViewSource` + `InfoViewDef`; grouped panel with real legends and ramps; building level, workplace, zone fill, land value restyle; `CityProblem` mask, severity tiles, 2-worst-icon rendering, alert strip, click-to-locate | 1. Each view draws within 500 ms of selection on 130x130 (profile log). 2. Legend text changes per view (screenshot). 3. Remove power plant: NoPower icon (orange then red after 3 days), alert strip shows it, click centres camera. 4. No more than 2 icons per building at once. |
| **T3 Day/night, weather, seasons** | `CityClimate`, `CityAtmosphere` + engine `TintPostProcessEffect`, info view dimming, night lights, `WeatherOverlay` rain/snow via conditions, `SeasonOverlay`, tree tint, electricity multiplier hook | 1. Screenshots at 03:00, 07:00, 12:00, 19:00 differ in mean brightness: night < 0.6 x day. 2. HUD and info view overlay brightness unchanged at night (pixel sample). 3. Winter at -15 degC: `PowerConsumed` >= 1.5x summer same city. 4. Snow depth rises while snowing, melts above 2 degC, deterministic across two replays. 5. Frame time cost of tint pass < 0.3 ms. |
| **T4 History and panels** | `CityStatistics` ring buffers + `CITY_STATS_PANEL` with `LineGraphWidget`; groundwater layer + Water pollution view (pump/tower sampling); budget tabs and expandable groups; demand factors popup; building panel per-kind rows with happiness hover | 1. After 24 game months the population series has 24 samples, each equal to `Population` at that 00:00. 2. Graph shows hover value; time scale buttons change range. 3. Demand factors sum (within rounding) to the displayed bar. 4. Happiness hover lists the same signed factors the sim used (assert sum == delta). |
| **T5 Chirper and citizens UI** (needs domain 01 phase B) | `CityChirper`, panel, bird alert; citizen panel with Follow, journal, activity/trip, camera follow; residents list in building panel | 1. Remove power: within 2 game hours a chirp "No power" appears with likes = affected buildings and a working locate button. 2. Follow a citizen: camera tracks her car, panel shows activity changing across a day (sleep -> commute -> work). 3. Feed never exceeds 6 posts/game day. |
| **T6 Disasters lite and advisor** | forest fire (dry + hot, spreads to adjacent trees/buildings, fire stations extinguish), storm (hail: random -happiness, building damage, traffic accidents), optional flood; `CityAdvisor` with 8 starter tutorials | 1. Disasters off by default; map option on: fire starts only when `Cloud < 30` and `T > 25`. 2. Fire station coverage reduces burned cells (A/B run). 3. Tutorial `roads-zones` completes via real orders. |

Order: T0, T1, T2, T3 (T0 unlocks domain 01 schedules), then T4-T6; T1 and T2 can run alongside T0.

**Risks.** (a) Changing `TicksPerDay` touches domains 01/02/05: keep `PulseTicks` and the shims in 3.1. (b) Sim-side weather and Chirper use `SharedRandom` only; visuals use `LocalRandom`. (c) `CityAtmosphere` tolerates a missing `TintPostProcessEffect`. (d) Icon and chirp floods: clustering and the 6/day cap.

---

## 6. UI and art needs

| Item | Spec | Owner / generator |
|---|---|---|
| Status icon glyphs | extend `statusicons` from 6 to ~24 glyphs at 16x16 (list in 3.6) plus 5 tier tiles 20x20; D2k bronze/gold look to match `city-icons-small` | A3, `genui.py` / `genworld.py` (`statusicons.png` is A2) |
| Ramps | `overlays/ramp-pollution`, `ramp-blue`, `ramp-green`, 11 frames 32x32 each, translucent solid | `genworld.py` (`overlay-heat.png` family) |
| Wind arrow glyphs | 8 directions, 24x24, white with dark outline, in `overlays` sequence `wind` (8 frames) | `genworld.py` |
| Snow cell overlay | `overlays/snow` 4 variants 32x32, soft white-blue noise, alpha 0.6 | `genworld.py` |
| Night lights | `lights` image: window glow 32x64 per growable level (3 frames), street lamp 32x32, headlight 16x16, all additive-friendly (black background removed, soft alpha) | `genworld.py` |
| Chrome icons | new `city-icons` regions: `stats`, `chirper` (bird), `advisor` (?), `follow`, `locate`, `weather-clear/cloud/rain/snow/storm`, `sun`/`moon` for the clock, `thermometer`, `wind`, `news`; both 32 and 16 px; order-tile variants (34x35, normal/disabled/active) for **stats** and **advisor** in `city-order-icons` | A3, `genui.py` |
| Panels | `CITY_STATS_PANEL` (about 640x420), `CITY_DEMAND_PANEL` (240x180), `CITY_CHIRPER_PANEL` (260x220, collapses to a 34x35 bird tile), `CITY_CITIZEN_PANEL` (280x300), alert strip above the minimap (3 x 20 px), clock label with sun/moon glyph in the top strip | F |
| Weather, sidebar | rain and snow are engine particles: no art. The order row (bulldoze, infoviews, budget, options at X=14, 54, 94, 144, 34x35 tiles) needs **stats** and **advisor**: 36 px spacing or a second row | F + A3 |
| Fluent | `fluent/chirper.ftl` (30-40 messages), `fluent/problems.ftl`, `fluent/climate.ftl` (month, weather, season names), info view names/descriptions/legends | F, domain files |

---

## 7. Sources

CS2 (accessed 2026-10):
- Pollution: https://cs2.paradoxwikis.com/Pollution ; guide https://www.ludo.guide/guide/cities-skylines-ii/19-pollution-management-air-ground-noise-and-water ; Steam air-pollution thread https://steamcommunity.com/app/949230/discussions/0/3877095833487076734/
- Climate and seasons: Dev Diary #8 https://colossalorder.fi/?p=1788 ; Paradox feature https://www.paradoxinteractive.com/games/cities-skylines-ii/features/climate-seasons ; wiki https://cs2.paradoxwikis.com/Climate
- Time scale: Steam guide https://steamcommunity.com/sharedfiles/filedetails/?id=3072493428 ; mirror https://steamah.com/cities-skylines-ii-time-scales-guide/
- Disasters: https://cs2.paradoxwikis.com/Disasters
- Info views: https://cs2.paradoxwikis.com/Info_views
- Notifications: https://cs2.paradoxwikis.com/Notifications
- Citizens, Chirper, lifepath: Dev Diary #11 https://colossalorder.fi/?p=1851 ; https://www.paradoxinteractive.com/games/cities-skylines-ii/features/citizen-simulation-lifepath ; wiki https://cs2.paradoxwikis.com/Citizens
- Economy panel: https://cs2.paradoxwikis.com/Economy ; demand: https://www.modscities2.com/cities-skylines-2-zone-demand ; rent code deep dive https://steamcommunity.com/app/949230/discussions/0/3937895062995797379/
- Building inspection and economy patch 1.1.5f1: https://www.dsogaming.com/patches/cities-skylines-2-patch-1-1-5f1-overhauls-the-games-economy-brings-performance-and-modding-improvements-fixes-a-lot-of-bugs-and-issues/
- Tutorials and Advisor: https://colossalorder.fi/?p=1924
- Statistics panel (patch notes only): https://en.namu.wiki/w/%EC%8B%9C%ED%8B%B0%EC%A6%88:%20%EC%8A%A4%EC%B9%B4%EC%9D%B4%EB%9D%BC%EC%9D%B8%20II/%EC%97%85%EB%8D%B0%EC%9D%B4%ED%8A%B8%20%EB%82%B4%EC%97%AD

Code read: `mods/city/CONTRACT.md`, `CityCoverageLayer`, `InfoViewLayer`, `RoadLayer`, `TrafficManager`, `CityManager.*`, `WithCityStatusIcons`, `Widgets/Logic/*`, `chrome/{ingame-player,city-panels}.yaml`, `chrome-city.yaml`; engine `TintPostProcessEffect`, `TerrainLighting`, `WeatherOverlay`, `WorldRenderer` (pass order), `glsl/postprocess_tint.frag`; cross-read `design/01-citizens.md`, `03-networks.md`, `08-public-transport.md`.
