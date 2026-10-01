# OpenCity design 01 - Citizens, households and life cycle

Domain 1 of 10. Scope: the per-citizen agent simulation (cims, households, ageing, birth/death, education,
jobs, wages, rent, shopping, leisure, schedules, health, crime, wellbeing, tourists).
Owner of this doc: research agent 1. Consumers: lead (architecture), implementation agents.

Confidence tags used below: **[W]** = stated by a Paradox/wiki page I fetched (URL in section 7),
**[M]** = from a CS2 mod description that documents vanilla behaviour, **[R]** = my recollection of the
decompiled game code / modder write-ups (treat as approximate), **[D]** = my own design choice for OpenCity.
Public sources give few hard numbers for CS2 happiness, health and demand; I mark every gap instead of inventing.

---------------------------------------------------------------------------------------------------

## 0. One-page summary

- Replace the "residents per building" counters (`CityBuilding.Residents/Workers`, 55% workforce rule, `Households = pop/3`)
  with a **plain-data citizen simulation**: struct arrays `Citizen[]` and `Household[]` inside one new player trait
  `CitizenSim`. **Not actors.** Buildings stay the only actors; each owns a *lot* (home beds, job slots by education level,
  school seats) keyed by its **cell**, so level-up actor replacement keeps its people.
- Existing public stats (`Population`, `Workers`, `Jobs`, `Unemployed`, `Households`, `EducatedRatio`, `AverageHappiness`,
  `ResidentialAttraction`) keep their names and meaning but are computed from the arrays. HUD, growth, tax and traffic code
  keep working in phase A.
- **Time**: recommend `TicksPerDay = 24` (1 tick = 1 sim hour, was 25), code reads it everywhere so 96..240 also works.
  Citizen ageing uses a **citizen year = 30 days** (one calendar month): lifespan ~80 y = 38 min at 1x.
- **Scheduling**: every citizen/household/lot has a fixed daily bucket (`index` range by tick), so each is processed once per
  game day: 4,200 citizens per tick at 100k, ~0.8 ms. Queues (immigrants, job seekers, applicants, corpses) have per-tick budgets.
- **Determinism**: ints only, per-citizen pure hash rolls, `SharedRandom` for global picks, index-ordered arrays, one rolling
  `[VerifySync] StateHash`. Saves are order replays, so arrays are never serialised.
- **Traffic hand-off**: citizens generate hourly `Trip` records; `TrafficManager` spawns a **sampled** subset as labelled actors,
  the rest are *virtual* (they still add congestion weight). Local UI state never influences sampling.
- Phases: **A** households, ages, birth/death, jobs by education (MVP); **B** schools, wages/rent/wealth, health, deathcare,
  happiness factors; **C** schedules + sampled trips, leisure, crime, tourists, commuters.

Confidence tags: **[W]** stated on a Paradox/wiki page I fetched, **[M]** from a CS2 mod description documenting vanilla,
**[R]** my recollection of decompiled code (approximate), **[D]** my own OpenCity design value. Public sources give few hard
CS2 numbers for happiness, health and demand; gaps are marked, not invented.

---------------------------------------------------------------------------------------------------

## 1. How Cities: Skylines 2 simulates citizens

**Architecture.** Citizens are ECS entities and primary agents: residents, workforce, customers **[W: Dev Diary 11]**. A
**household** owns 1..N citizens, money, a rent contract, needs and trips; it is the unit that moves in, moves away, pays rent,
shops and becomes homeless **[W/R]**. CS2 simulates every citizen with real pathfinding, round-robin over 16 update frames
**[R]**. One game day = 24 h = **72 real minutes at 1x** (the calendar treats a day as a month) **[W: Steam time-scales guide]**.
This cost is why OpenCity must not copy per-citizen pathfinding.

**Life stages [W].** Child: cannot work, always attends elementary school if a seat exists, starts Uneducated. Teen: may work or
study. Adult: may work or study (College/University). Senior: cannot work, gets a pension, health slowly decays until death.
Ages are counted in game days: vanilla thresholds **21 / 36 / 84**, death from ~**108** with "all die within 12 days", cims live
"around 100 days" **[M: Population Rebalance]**; the mod retunes to 12/20/75 for ~15% children / 10% teens / 60% adults / 15% seniors.
Vanilla birth gives ~**3.8 children per family**, all spawned at age 0, a Couple household often has 1 adult (bug), student
households are one adult of age 36 **[M]**.

**Deaths [W].** Old age, low health (sick/injured citizens die with probability rising as health falls), traffic accidents,
building collapse; prisoners die of old age. **Deathcare**: hearses carry bodies to cemeteries (storage) or crematoriums; with none,
hearses come from an outside connection. Unprocessed corpses vanish after a while (mod default 30% chance) **[M]**.

**Households and moving.** `HouseholdSpawnSystem` spawns households at outside connections; `HouseholdFindPropertySystem` scores
free homes by quality against rent affordability, throttled per update; homeless are handled in a separate list; a household can
`MoveAway` **[R]**. Which household type spawns depends on average happiness, homelessness, residential tax, free education
spots and open jobs **[W: Economy 2.0]**. Hidden **household demand** gates spawning, **building demand** drives zoning **[M: InfoLoom]**.
Residential demand factor thresholds: unemployment < 20% positive, free workplaces > 10% positive, happiness > 45 positive, tax
< 10% positive **[W, forum post, search snippet]**. Low density is the most expensive (one household pays the whole building's rent
and upkeep); medium/high split it; wealthy households raise low-density demand, students raise high-density demand; families want
low/medium, singles accept towers **[W]**. Households that cannot pay rent seek cheaper homes, else leave via an outside connection,
else become **homeless** and live in parks **[W]**.

**Education [W].** Five levels (Uneducated, Poorly, Educated, Well, Highly); each graduation +1. Elementary: children; High School:
teens; College: teens and adults; University: adults (vanilla also allowed High School -> University **[M]**). Teens and adults compare
income of working now vs at the higher level; adults without diploma re-apply with low probability. Graduation is probabilistic and
vanilla graduates in 1-2 days, "rarely more" **[M]**. Tuition per month: 50 / 100 / 200 / 200 credits. Education sets the highest
job level, the wage, work efficiency, and lowers water/power use and garbage.

**Work [W].** Workplaces have a *complexity* mapping to a job mix over the five education levels (industry low, commercial and office
higher as they level up); city services have fixed mixes **[M]**. Citizens take any job at their education level **or lower**. Vanilla job
search ignores commute distance **[M]**. Without local jobs they commute to neighbouring cities. Higher-educated workers are more
efficient; missing high-skill staff hurts efficiency most; sick workers cannot work; happiness changes company efficiency; companies
grow/shrink workforce with profit. Wages depend on education: **1500 / 1800 / 2100 / 2400 / 2700** credits per month, commuter x1.1,
tax-free 1400; unemployment benefit 800 (max 10 days), family allowance 400, pension 1200 **[W: Citizens wiki v1.3.3]**. Shifts Day /
Evening / Night with per-workplace probabilities **[R/M]**; vanilla spreads commutes without strong rush hours **[M]**.

**Consumption, leisure, tourism [W].** Households hold money and consume goods; low resources create shopping needs; they pay rent and
garbage fee before shopping; teens buy electronics, seniors financial services. **Leisure** is a growing counter; the citizen picks a
destination by leisure gain per travel cost: parks/plazas/sports/landmarks, shops/malls, restaurants/bars (paid), or travel abroad;
weather shifts indoor/outdoor. **Tourists** arrive via outside connections in proportion to **attractiveness**, sleep in hotels, do
daily leisure choices and spend in shops.

**Health, sickness, wellbeing [W].** Happiness = Wellbeing + Health (bytes per citizen **[R]**). Wellbeing: electricity, water, sewage,
garbage, air/ground/noise pollution, crime probability, service coverage, mail, internet, home size vs household size, ability to buy
goods, leisure, education access, entertainment, wealth, taxes. Health: nearby healthcare (passive boost), pollution, sewage backup,
garbage, sickness, injury; children/teens get a boost. Conditions: Sick, Injured, Weak (low health), Unwell (low wellbeing, crime
risk), Homeless, Distressed, Evacuated. The wiki gives **no numeric weights**; thresholds for Weak/sick are unknown to me.

**Crime [W].** Each building has a crime probability; low-wellbeing citizens may become criminals; the criminal targets the building
with highest probability, steals (victim loses wellbeing), escapes; a patrol car arriving first arrests him; jail, some sentenced to
prison (city or outside), then reset. Patrol cars and stations lower crime probability.

**Take-aways.** Keep: household as economic unit; citizen as age/education/health carrier; job match by education-or-lower; rent vs
income drives moves and homelessness; visible happiness factors. Drop: per-citizen pathfinding, per-citizen inventories, individual
crime pursuit, 24-hour trips for everyone. Keep all numbers in `rules/citizens.yaml`.

---------------------------------------------------------------------------------------------------

## 2. Current OpenCity state and gaps

### 2.1 What exists (read from the code)

| Area | Today | File |
|---|---|---|
| Calendar | `TicksPerDay = 25`, `DaysPerMonth = 30`, year = 12 months; `DailyUpdate()` runs once per 25 ticks inside `CityManager.ITick` | `Traits/Player/CityManager.cs` |
| Population | Per building integer `CityBuilding.Residents` (0..`MaxResidents`, 4..44 for res-low-1..res-high-3). Move-in: `Hash(SortId, TotalDays) % 100 < MoveInChance(12) + attraction/5` then `+1+max/24` residents; move-out when happiness < 25, no utilities, abandoned | `CityManager.Daily.cs` `UpdateResidents` |
| Workforce | `Workers = Residents * 55%` (rounded) per building; city `Workers` = sum | `SimulateBuildings` |
| Jobs | `CityBuilding.Info.MaxJobs` per workplace (com 4..30, ind 10..24, off 12..32, services 1..20). Employed = `min(workforce, jobs)`; jobs filled proportionally to size, remainder by ActorID | `CityManager.Economy.cs` `FillJobsAndCollect` |
| Education | Only `EducatedRatio` = resident-weighted *school coverage* (0..100). Unlocks Office demand at >= 20 or pop >= 800. No levels, no schools staffed by students | `SimulateBuildings` |
| Happiness | Per building 0..100: base 55, -30 no power, -25 no water, -20 no road, + coverage*(12/10/12/15/8)%, - pollution*30%, + land value*10%, tax term, - unemployment/3; moves +/-3 per day. City value = resident-weighted mean | `UpdateHappiness` |
| Households | Display only: `(Population + 2) / 3` | `CityManager.Stats.cs` |
| Taxes | `persons * base * levelPct * landValuePct * rate/10`, base 15/38/30/60 tenths per resident/worker; collected monthly | `FillJobsAndCollect`, `EndMonth` |
| Demand | R/C/I/O targets from job-vs-workforce balance, unemployment, happiness, tax, utilities, vacancy; eased 3 up / 6 down per day | `UpdateDemand` |
| Traffic | `TrafficManager` picks random home/job endpoints each 15-40 ticks, cap `min(200, pop/6)`; trips are *not* tied to a citizen; some trips start/end at highway | `Traits/World/TrafficManager.cs` |
| UI | Building info panel shows residents/jobs current/max, happiness, land value; top bar shows population, happiness; demand bars | `Widgets/Logic/CityBuildingInfoLogic.cs` |
| Level-up | `GrowableBuilding` replaces the actor with `UpgradesTo` and "carries over Residents and Workers" (so actor id changes, location stays) | `GrowableBuilding.cs` |
| Autotest | `CityAutoTest.LogStats` prints pop/workers/jobs/unemployed/demand/happy/vehicles; frozen file (lead owns) | `Traits/World/CityAutoTest.cs` |

### 2.2 Gaps vs the CS2-style goal

1. **No individuals.** People are an integer per building. No ages, genders, households, families, schooling,
   death, birth. Population only changes by "move in/out" rolls.
2. **No education levels or job matching.** Jobs are anonymous slots; the 55% workforce share ignores ageing
   (children, seniors) and education; unemployment is a single aggregate.
3. **No money flow at household level.** No wages, rent, wealth, shopping, homelessness; tax is a formula
   over head-counts.
4. **No lifecycle services demand.** No deathcare (no cemetery/crematorium actor), no sickness/health
   (clinic is a coverage blob), no crime events (police is a coverage blob), schools do not hold students.
5. **No tie between citizens and traffic.** `TrafficManager` samples random endpoints; nothing says *who* is
   driving, so congestion cannot feed back to commute time, wellbeing, job quitting or happiness.
6. **No explainable happiness.** CS2 shows each factor per household/citizen; OpenCity has one 0..100 number
   per building computed from coverage maps.
7. **No immigration/emigration model** beyond a global "attraction" scalar (`ResidentialAttraction`).
   Outside connections are not used as a source of new households, commuters or tourists.
8. **Building identity** changes on level-up (new actor, new `ActorID`), so anything that stores per-citizen
   "home building" must key off something stable (the lot cell), not an `Actor` reference.
9. Perf: nothing is O(citizens) today. `DailyUpdate` is O(buildings) and runs in one tick.

### 2.3 What to keep

- `CityBuilding` remains the building-side registry (power/water/road access, happiness cache, land value).
  It simply gains a slot-allocation handle (`CitizenSim.LotId`) and stops being the *source of truth* for people.
- `CityManager` keeps money, calendar, taxes, demand, milestones; it reads people statistics from `CitizenSim`.
- All existing public stats (`Population`, `Workers`, `Jobs`, `Unemployed`, `HousingCapacity`, `Households`,
  `EducatedRatio`, `AverageHappiness`, `ResidentialAttraction`) stay as **properties with the same meaning**,
  now computed from the citizen arrays. No UI or growth code has to change in phase A.

---------------------------------------------------------------------------------------------------

## 3. Proposed design for OpenCity

### 3.1 Time scale (decision needed from the lead; my recommendation first)

CS2: 1 game day = 24 h = **72 real minutes at 1x**; the calendar treats a game day as one *month*, so a cim
that lives ~100 days lives ~8 game years (the 72 min figure is **[W]**, the month equivalence and the 8 years are my inference from it). OpenCity must be far faster (a session is minutes, not
days). Design rules so the citizen sim works with **any** `TicksPerDay` that is a multiple of 24:

| Parameter | Value now | Recommended | Notes |
|---|---|---|---|
| `CityManagerInfo.TicksPerDay` | 25 | **24** (1 tick = 1 sim hour) now; 96..240 later | 25 is not a multiple of 24. Changing 25 -> 24 shifts economy pace by 4% only. All citizen code reads `TicksPerDay`; nothing hardcodes 25 |
| Sim hour | n/a | `hour = ticksInDay * 24 / TicksPerDay` | Exposed as `CityManager.HourOfDay` (0..23) for day/night tint, shifts, traffic rush hours |
| Schedule slot | n/a | 6 slots of 4 h: night 0-4, early 4-8, morning 8-12, noon 12-16, evening 16-20, late 20-24 | Coarse enough for 24 ticks/day, fine enough for shifts and rush hours |
| Citizen year | n/a | `DaysPerCitizenYear = 30` (one calendar month) | Lifespan 80 y = 2,400 days = 57,600 ticks = **38 min at 1x, ~10 min at 3x**. Founding cohort sees children -> adults -> seniors in one long session. Yaml-tunable; use 40 for slower generations |
| Day length real | 1 s at 1x | keep until the environment/time agent (domain 10) decides | If domain 10 raises `TicksPerDay` to 96-240, citizen buckets scale automatically (ranges of `N / TicksPerDay` per tick) |

Alternative (rejected): CS2-style "age counted in game days" with 100-day lifespan. With 25 ticks/day that is
a 100 s lifespan: whole cohorts would die in the first minutes. Do not do this.

### 3.2 Data model: struct arrays in a player trait

New **player trait** `CitizenSim` (file `Traits/Simulation/CitizenSim.cs` plus partials). Not an actor per
person. Everything is plain data in arrays, grown by doubling, with free-lists for reuse.

```csharp
// Logical layout; implement as struct arrays (AoS) or parallel arrays (SoA) - AoS first, simplest.
struct Citizen            // 32 bytes
{
    public int BirthDay;        // TotalDays at birth (negative for the founding cohort). Age = (TotalDays - BirthDay) / DaysPerCitizenYear
    public int Household;       // index into households[]; -1 = free slot
    public int NextInHousehold; // intrusive singly linked list (max 8 members)
    public int Place;           // workplace lot id (if Occupation == Worker) or school lot id (if Student); -1 none
    public byte Education;      // 0..4: Uneducated, Poorly, Educated, Well, Highly
    public byte JobLevel;       // level of the job slot actually held (<= Education; lower = under-employed)
    public byte Health;         // 0..255
    public byte Wellbeing;      // 0..255
    public byte Leisure;        // 0..255 counter, grows daily, reset by leisure visit
    public byte Flags;          // bit0 male | bit1-2 occupation (None/Worker/Student/Retired) | bit3 sick | bit4 injured | bit5 criminal | bit6 dead (awaiting hearse) | bit7 pregnant/new parent cooldown
    public byte Shift;          // 0 day, 1 evening, 2 night
    public byte StudyDays;      // days in current school (graduation needs min days)
}

struct Household          // 40 bytes
{
    public int Lot;             // home lot id; -1 = homeless
    public int FirstMember;     // head of the citizen linked list
    public int Wealth;          // credits; may go negative (debt) down to a floor
    public int MonthIncome;     // last month wages + benefits
    public ushort Rent;         // monthly rent currently paid
    public ushort Spent;        // shopping spend last month
    public byte Type;           // Single, Couple, Family, Senior, Student, Commuter, Tourist
    public byte Size;           // members, 1..8
    public byte Kids;           // children + teens
    public byte Flags;          // moved-in | homeless | wants-to-move | has-car | tourist | commuter | evicted-this-month
    public byte Happiness;      // cached mean of members' (Health+Wellbeing)/2 scaled 0..255
    public byte NeedMask;       // bit per shopping category pending
    public ushort Reserved;
}

struct Lot                // one per CityBuilding (home / workplace / school / service); keyed by CELL, not actor
{
    public int Cell;            // y * mapWidth + x of footprint[0]; survives level-up actor replacement
    public int RoadCell;        // access road cell (traffic endpoint)
    public byte Kind;           // Home | Work | School | Hospital | Cemetery | Police | Hotel | Park | ...
    public byte Level;          // building level 1..3 (re-read on level-up)
    public byte Flags;          // operational | abandoned | lowDensity
    public byte SchoolLevel;    // 1..4 for schools (which education level it grants)
    public short HouseSlots;    // max households (low density = 1)
    public short Beds;          // max residents
    public short Residents;     // current
    public short Households;    // current
    public short Rent;          // monthly rent per household for this lot (set from land value + level)
    public short StudentCap, Students;
    // job slots: jobCap[lot*5 + level], jobFill[lot*5 + level] in parallel short arrays
}
```

Sizes: 100k citizens x 32 B = 3.2 MB; 40k households x 40 B = 1.6 MB; 6k lots x ~48 B + 10 shorts = 0.5 MB.
Whole sim state under **6 MB at 100k**, one allocation per array, no GC pressure.

Rules:
- **Index = identity.** A citizen is `int index`. Death frees the slot (`Household = -1`, pushed to a free-stack);
  births pop the stack (LIFO, deterministic). External references (a sampled vehicle carrying "citizen #123")
  store `(index, BirthDay)`; if the pair no longer matches the vehicle simply drops the label.
- **Lots keyed by cell.** `CityBuilding` calls `CitizenSim.RegisterLot(cell, info)` on add-to-world and
  `UnregisterLot` on removal. A level-up replaces the actor in the same cell: `RegisterLot` finds the existing
  lot by cell, **updates capacity/level, keeps residents and workers** (shrink needs a forced-eviction pass but
  levels only go up). A bulldozed or collapsed building evicts its households (they become "seeking home").
- `lotIndexByCell` is an `int[mapW*mapH]` (-1 none) for O(1) lookup; lots iterate in creation order.
- **Intrusive lists** for household membership and (phase B) per-lot household lists avoid per-entity
  allocations; max household size 8 (clamp births).

### 3.3 Update scheduling (spread over ticks)

Let `T = ticksInDay` (0..TicksPerDay-1) and `D = TicksPerDay`.

- **Citizen daily update**: the index range `[T*N/D, (T+1)*N/D)` (N = array high-water mark). Each citizen is processed
  **once per game day** at a fixed phase. 100k citizens at D=24 = 4,167 per tick.
- **Household daily update** uses the same range scheme over `households[]`; payday for `(TotalDays + h) % DaysPerMonth == 0`
  (every household once per month, evenly spread).
- **Lot update** (same range scheme over `lots[]`): refresh capacity/operational/road access, rent price (from
  `CityCoverageLayer.GetLandValue`), crime accumulation.
- **Global/hourly** (`T == 0` or per hour): demand refresh, aggregate recount audit, immigration batches.
- **Event queues** instead of scanning: `moveInQueue` (households spawned at outside connections waiting for a
  home), `seekingJob` list (unemployed adults, round-robin cursor), `schoolApplicants`, `hearseQueue`. Each is
  processed with a **per-tick budget** (e.g. 64 matches/tick) so a big batch cannot spike the tick.
- **Incremental counters** keep UI cheap: `popByAge[4]`, `popByEdu[5]`, `workersByEdu[5]`,
  `jobsByLevel[5]`, `unemployedByEdu[5]`, `homeless`, `households`. Updated at every transition. An
  `Audit()` recount (debug / autotest only) asserts counters == recount.
- `CityManager.DailyUpdate` stays and keeps its role (utilities, taxes, demand, milestones) but reads
  `CitizenSim` counters instead of looping `b.Residents`.

Budget (per tick at D=24, estimated per-citizen update ~60-150 integer ops, ~100-250 ns in .NET):

| Citizens | Per tick | Cost per tick | Share of a 40 ms tick (1x) | Share of a 10 ms tick (3x) |
|---:|---:|---:|---:|---:|
| 10,000 | 417 | ~0.1 ms | 0.3% | 1% |
| 50,000 | 2,083 | ~0.4 ms | 1% | 4% |
| 100,000 | 4,167 | ~0.8 ms | 2% | 8% |

Households and lots add about 30%. Queued matching adds at most 64 x ~2 us. The real cost is **job/home
search** (see 3.4/3.5), which must use the per-level free-slot lists, never scans. Contiguous ranges (not
`i % D`) keep memory access sequential. Because N changes during the day, compute the range from the N captured at
`T == 0`, so every index is visited exactly once per day.

---------------------------------------------------------------------------------------------------

### 3.4 Households: formation, move-in, move-out, homelessness

**Sources of households** (all produce `Household` + `Citizen` rows):
1. **Immigration** at outside connections (daily, `T == 0`). Budget:
   `spawn = min(freeHouseSlots, MaxImmigrantsPerDay, max(0, Attraction) * (4 + Population / 50) / 100)`
   with `Attraction = CityManager.ResidentialAttraction` (-100..100, existing). Each admitted household is
   pushed to `moveInQueue`. Highway capacity (domain 3) can cap it further.
2. **Birth/leave-home**: teens turning 18 leave the parents with 40% chance per citizen-year (hash roll) and
   form a Single/Student household in `moveInQueue` (phase B).
3. **Founding cohort**: when a lot first becomes operational and attraction > 0, households spawn as in 1.
   No special case; an empty city simply starts with `Population = 0`.

**Household type mix at spawn** (weights, [D]; modifiers in brackets):

| Type | Members | Weight | Modifiers |
|---|---|---:|---|
| Single | 1 adult 20-55 | 30 | x2 if only high-density slots free |
| Couple | 2 adults 22-55 | 20 | |
| Family | 2 adults + 1..3 kids (ages 0..17, spread, not all 0) | 35 | x2 if low-density slots free; x0 if no slot has Beds >= 3 |
| Senior | 1-2 seniors 65-80 | 8 | x0.5 if no clinic coverage; spawns pension recipients |
| Student | 1 adult 18-25, Education 2 | 7 | x(1 + free college seats / 50) |

Education of working-age immigrants is sampled from **current unfilled job levels**:
`P(level L) ~ baseline[L] + vacantJobs[L]` with baseline `[20, 40, 25, 10, 5]`. A city with empty office
floors thus attracts educated people, a factory town attracts the uneducated. This is the CS2 loop "open
positions pull in matching households" **[W: Economy 2.0 'open job positions' spawn factor]**.

**Home choice** (`moveInQueue`, <= `HomeSearchBudget` = 48 households/tick):
1. Candidate lots: the **free-home list** (`homesWithFree`, swap-remove array, one per density class). Sample
   `K = 8` indices with `SharedRandom`; discard lots that are non-operational, lack power/water/road access, are
   in a different road component than the household's entry road, or have `Households >= HouseSlots` /
   `Residents + Size > Beds`.
2. `score = quality*3 + landValue - rentShare*2 + fit*20 - crowd*10` where `quality = lot.HappinessCache` (0..100),
   `rentShare = rent*100/expectedIncome`, `fit = 1` if household size/beds match (families prefer low density),
   `crowd = residents*100/beds`. Affordable only if `rentShare <= RentToIncomeMax` (default 45).
3. Take best score. No candidate in 3 consecutive days -> **leave** (counts as `refusedImmigrants`, a
   statistic the UI can show) or, for local households, become **homeless**.

**Rent**: `Rent(lot) = BaseRent[kind][level] * (70 + landValue * 6 / 10) / 100` per household per month; low
density charges the whole building to the one household (CS2 rule **[W]**), high density divides:
res-low-1 12, res-low-3 28, res-high-1 8, res-high-3 14 dollars (monthly, wage scale in 3.8). Rent flows to the
building's owner = the city economy (not a player income in MVP; maybe "property tax" later).

**Move-out triggers** (daily bucket of the household): (a) `Wealth < -MaxDebt` for 2 consecutive months ->
evicted; (b) `Happiness < 25` (existing threshold) -> 20% per day wants to move; (c) lot lost power or water for
> 10 days; (d) lot abandoned/bulldozed -> immediately homeless; (e) family too big for the lot -> wants larger
home. A "wants-to-move" household re-enters the search with a reservation: only lots with `score > current + 15`.
No home found within `GiveUpDays` (default 6) -> `Emigrate` (leaves the city; frees rows) unless
`homelessShelterCapacity` exists.

**Homelessness** (CS2: cannot pay rent, no move possible -> live in parks **[W]**): household keeps its rows but
`Lot = -1`, `Flags.Homeless`. Effects: Wellbeing -60, Health -2/day, no payday rent, still shops at 30%,
can still hold a job (commutes from park cell = nearest park lot; fall back to its last home cell).
Homeless count feeds residential demand (CS2 factor "Homelessness" **[W]**) and happiness. Shelters (future
service) give 20-50 beds. After `HomelessMonths = 3` they emigrate. Stat `Homeless` shown in the top bar tooltip.

### 3.5 Job matching

Per workplace lot, `jobCap[lot*5+L]` slots per level from a **job mix table** in yaml (CS2 "complexity"
**[W/R]**; numbers [D]):

| Workplace | Edu0 | Edu1 | Edu2 | Edu3 | Edu4 |
|---|---:|---:|---:|---:|---:|
| ind-1 / 2 / 3 | 70/45/25 | 25/35/35 | 5/15/25 | 0/5/10 | 0/0/5 |
| com-low-1..3 | 50/40/30 | 35/35/30 | 15/20/25 | 0/5/10 | 0/0/5 |
| com-high-1..3 | 40/30/20 | 35/30/25 | 20/25/30 | 5/12/17 | 0/3/8 |
| off-1 / 2 / 3 | 5/0/0 | 25/10/5 | 45/35/20 | 20/35/40 | 5/20/35 |
| clinic | 20 | 20 | 20 | 30 | 10 |
| school | 10 | 10 | 10 | 50 | 20 |
| police/fire | 10 | 40 | 40 | 10 | 0 |
| power/water | 40 | 40 | 20 | 0 | 0 |

(Percent of `MaxJobs`; rounding remainder to the lowest level that rounds nonzero; services keep
fixed counts like CS2 **[M]**.)

**Seeker algorithm** (run at the citizen's own daily bucket; unemployed or underemployed adults/teens only):
```
seek(c):                                  // c.Education = e, age >= 16, not student, not sick
  for L in [e, e-1, ..., maxDown(e, daysUnemployed)]:     // prefer own level; allow lower after 10 unemployed days (reservation wage)
     cand = sample K=6 lots from openJobs[L]               // swap-remove arrays kept per level
     best = argmin over cand( dist(homeRoad, lotRoad) + 3*(L<e ? 1 : 0) ), same road component only
     if best: take slot (jobFill[best*5+L]++), c.Place=best, c.JobLevel=L, c.Flags.occupation=Worker, c.Shift=pickShift(lot)
  none -> unemployed counter++, c.Leisure += 4 (boredom)
```
- Over-qualified workers (JobLevel < Education) re-search each month with 20% chance (job switch), freeing
  the slot. This produces visible "under-employment" (CS2/InfoLoom tracks it **[M]**).
- Workplaces that shrink (abandoned, power outage > 10 days, level change not applicable) release workers
  in **descending citizen index**; they enter `seeking`.
- **Shift** at hire: workplace `ShiftWeights` (industry 70/20/10, commerce 75/20/5, office 100/0/0, services
  50/30/20 day/evening/night), roll by hash. Night shift workers are in `Home` during day slots.
- **Commute check** (phase C): `commuteDays = trafficTime/...` stored per worker; > threshold twice -> quit.
- **Efficiency** per workplace: `eff = 100 * sum(fill_L * w_L) / sum(cap_L * w_L)`, `w = [1,2,3,4,5]`; a worker
  of lower level than the slot counts 60%. `Lot.Efficiency` feeds tax/production and a status icon.
- Replaces `Workers = Residents*55%`: **Workers = employed citizens**, `Unemployed` = seeking adults,
  `Jobs = sum(jobCap)` of operational workplaces, as today.
- Commuters in/out: if `jobs > workforce` the surplus is filled by **commuter citizens** (flag, no household)
  counted at 10% of vacant slots per level until `MaxCommutersPct` (20% of jobs). They need a highway trip and
  pay no residential tax. If `workforce > jobs`, up to 10% of unemployed may commute out (reduce unemployment,
  pay home tax) (phase C).

### 3.6 Ageing, birth, death, health and deathcare

Age groups in **citizen years** (1 y = `DaysPerCitizenYear` = 30 days): Child 0-11, Teen 12-17, Adult 18-64,
Senior 65+. With lifespan ~80 this gives about 15% / 7.5% / 59% / 19% (the shape the community wants **[M]**).

Daily citizen update (bucket), in order:
1. `age` -> group; on group change: Child->Teen (seek HS), Teen->Adult (leave-home roll, may leave school if
   not mid-term), Adult->Senior (**retire**: free job slot, collect pension).
2. **Health**: `Health += healthRegen - healthDrain`, clamped 0..255:
   regen: +2 base for age < 18; +1 if clinic coverage >= 50 (`CityCoverageLayer.GetCoverage(Health, lotCell)`);
   drain: age >= 65 `(age - 64) / 4` per day (seniors lose max health: cap `255 - (age-64)*4`); pollution
   `GetPollution(cell) / 25`; uncollected-garbage and sewage hooks (domain 6/7) 1-3; sick `-6`.
3. **Sickness**: `if !sick && roll(1e6) < sickPPM * (320 - Health) / 160` -> sick (`sickPPM = 4000`: 1,625 ppm = 0.16%/day
   at Health 255 up to 8,000 ppm = 0.8%/day at Health 0). Sick: cannot work/study, makes 1 trip to the nearest clinic/hospital
   (hospital capacity via domain 7); recovery chance per day `5% + 25% if treated`, treated heals health +60.
4. **Death** `deathPPM = ageHazard[band(age)] * hazardMult(Health) / 100 + (sick ? sickDeath(Health) : 0)`:

| Age band (y) | <50 | 50-64 | 65-74 | 75-84 | 85-94 | 95+ |
|---|---:|---:|---:|---:|---:|---:|
| Annual death prob. | 0.1% | 0.6% | 2% | 6% | 18% | 40% |
| Daily ppm (= annual/30) | 33 | 200 | 667 | 2000 | 6000 | 13333 |

`hazardMult = 50 + (255 - Health) * 150 / 255` (percent). `sickDeath = 40 + (255-Health)^2 / 200` ppm.
Expected median lifespan ~80 y (2,400 days). All integer; roll = `Hash(citizenIndex, TotalDays) % 1_000_000`.
5. **Birth** (households of type Couple/Family with an adult female aged 18-44, or Single mothers 20-40 at 1/3
   probability): per day `BirthPPM = 6000` (~18% per citizen-year) if `Kids < 3`, happiness >= 40, wealth > 0,
   `bedsFree >= 1`, cooldown (flag bit7, 2 citizen-years) clear. New citizen index from free-stack with
   `BirthDay = TotalDays`, Education 0, Health 220. Average ~2.1 children per couple over their fertile window
   (tunable; CS2 vanilla 3.8 is too many **[M]**).
6. Leisure counter `++`; wellbeing recompute (3.12).

**Deathcare**: dead citizens are *removed from the household immediately* (benefit/ rent recomputed) and enqueue a
**corpse** record `{cell, day}` in `hearseQueue` (struct list, FIFO). Services domain provides
`ICityDeathCare.TryCollect(corpse)`. In MVP (no cemetery building) a **virtual outside hearse** collects one
corpse per `OutsideCollectDays = 3` per 1000 pop for a fee (`$2` each) so nothing breaks. With cemeteries/
crematoria: each has `CorpseCapacity`; hearses (vehicles, domain 7/8) carry them. Uncollected corpses older than
`CorpseWarnDays = 6` give -wellbeing (`-15` for homes within 6 cells) and +2 sickness ppm; they vanish at 20 days.
`DeathPerMonth` and `UncollectedCorpses` are exposed stats (death count must be > 0 after ~one lifespan).

### 3.7 Education and schools

Schools are `Lot` rows with `SchoolLevel` 1..4 and `StudentCap`, hosted by actors with `ServiceBuilding`
(existing `school` = level 1; add `highschool`, `college`, `university` actors in domain 5; art needed).

| Level | Grants | Eligible (age, current Edu) | Study length (y / days) | Tuition $/mo | Capacity (suggested) |
|---|---|---|---|---:|---:|
| 1 Elementary | Edu 1 | child 6-11, Edu 0 | 6 / 180 | 1 | 60 |
| 2 High School | Edu 2 | teen 12-17, Edu 1 (adult 18-24 Edu 1: 30%) | 4 / 120 | 2 | 120 |
| 3 College | Edu 3 | age 16-40, Edu 2 | 3 / 90 | 4 | 150 |
| 4 University | Edu 4 | age 18-45, Edu 3 | 4 / 120 | 4 | 200 |

- **Enrolment** (bucket day): children always apply if a school with a free seat in the same road component
  exists (CS2 **[W]**), sampling K=4 nearest by manhattan distance. Teens: `ApplyHS = 70%`; adults: College
  `base 6%/y` x3 if unemployed or under-employed x0.3 if wage need (household wealth < 0); University 4%/y x3 if
  unemployed. All values yaml-tunable. A citizen **chooses study over work** by comparing
  `(wage[e+1] - wage[e]) * remainingWorkDays` against `lostWages = wage[e] * studyDays/30 + tuition` (CS2 compares
  income at the higher level **[W]**): study if gain > 1.5 x loss; otherwise only the random chance applies.
- **Studying**: `StudyDays++` per day when the school is operational and the citizen is not sick; at `StudyDays >=
  required` roll `pass = 70 + schoolQuality/5` percent (quality = building happiness, staffing efficiency).
  Pass -> `Education++`, leave school (`Place = -1`), seek job/next school. Fail -> `StudyDays -= required/4`.
- **No school available**: children stay at home as Uneducated (they still count for school demand), teens
  with Edu 0 may work Edu-0 jobs from 16. Education demand feeds `ResidentialDemand` factor "students/seats"
  and office demand (educated unemployed).
- `EducatedRatio` = share of adults with Edu >= 2, used for the existing Office unlock (>= 20%).
- Working: Edu lowers water/power use and garbage (CS2 **[W]**): use `utilityMult[e] = [110,100,95,90,85]` percent
  on the household's lot demand (phase C, needs hook in `CityManager.Use`).

---------------------------------------------------------------------------------------------------

### 3.8 Income, wages, rent, wealth, shopping

Money scale: today's economy is about 1/50 of CS2's credits (`ResidentialTaxBase` = $1.5 per resident per month at
10%). Use **CS2 numbers / 50** so ratios stay recognisable **[W: wage table]**:

| Item | CS2 credits/mo | OpenCity $/mo |
|---|---:|---:|
| Wage by job level 0..4 | 1500 / 1800 / 2100 / 2400 / 2700 | 30 / 36 / 42 / 48 / 54 |
| Unemployment benefit (max 10 days) | 800 | 16 |
| Family allowance per child | 400 | 8 |
| Pension (senior) | 1200 | 24 |
| Tuition (elementary..university) | 50 / 100 / 200 / 200 | 1 / 2 / 4 / 4 |

(Wage by *job level held*, not education: a deliberate deviation from CS2 so under-employment hurts the worker **[D]**.)

- **Payday** per household once a month: `income = sum(wage(JobLevel)) + benefits`; `Wealth += income - rent -
  tuition - garbageFee`; residential tax `= TaxRate(Residential)% * wage` goes to `CityManager.AddFunds(..,
  "tax-residential")` (replaces the head-count formula in phase B; commercial/industrial/office tax keep the
  existing formulas until the economy domain supplies company profit).
- **Shopping**: each household day: `spend = min(Wealth, Size * perCapitaSpend * incomeFactor)` with
  `perCapitaSpend` ~ 0.4 $/day (so ~45% of income). Spend is credited to commercial lots through
  `ICommerceSink.Sell(homeLot, amount)` (economy domain picks the shop by distance/stock) and creates a **shop
  trip** with probability 35%/day. Failure to buy (no reachable shop) sets `NeedMask` and a wellbeing malus
  (CS2: "ability to buy goods affects wellbeing" **[W]**). Goods categories (food, retail, leisure, ...) are the economy
  domain's concern; the citizen side only needs *amount* and *category bit*.
- **Wealth bracket** `0..4` from `Wealth / (2 * rent)`; feeds happiness (wealth factor), low/high density demand
  (rich -> low density **[W]**), and housing score.
- **Debt**: floor `-MaxDebt = -3 x monthly rent`; below it two consecutive months -> eviction (3.4).

### 3.9 Daily schedules, commuting, trips -> traffic hand-off

Schedules are **tables** (yaml), not agents. Per citizen per game day the bucket tick plans at most 2 commute legs,
and per household 0..2 optional trips.

| Role | Depart home | At place | Return | Optional |
|---|---|---|---|---|
| Worker, day shift | 7 (jitter +-1 h by hash) | 8-16 | 16-17 | 35% leisure 18-21, 30% shop 17-19 |
| Worker, evening | 15 | 16-24 | 0 | - |
| Worker, night | 23 | 0-8 | 8 | sleeps 9-16 |
| Student (child/teen) | 7 | 8-15 | 15 | leisure 16-19 |
| Unemployed / retired / child < 6 | - | - | - | shop 10-16 (45%), leisure 10-12/14-18 |

Hours map to ticks via `hour * TicksPerDay / 24` (1 tick = 1 hour at D=24). **Peak spreading**: jitter by hash so a
tick has at most ~25% of the day's commuters.

**Trip generation** appends `Trip { int Citizen; int FromLot; int ToLot; byte Purpose; byte Hour; byte Mode }` to an
**hourly ring queue** `tripQueue[hour]` (struct list). Mode (decided at generation): walk if manhattan <= 6 cells; car
if household `HasCar` (wealth bracket >= 1, ~70%); transit if domain 8 reports a route cheaper than driving; else car.
Counts per hour and purpose are statistics (`TripsPerHour[24]`) and **the load model** for traffic.

**Hand-off to traffic (key decision)**: `TrafficManager` consumes trips through an interface and spawns actors for a
**sampled** subset only (`VisibleBudget`, e.g. 400 vehicles, cap by actor cost):

```csharp
interface ITripSource { int PendingThisTick { get; } bool TryDequeue(out Trip trip); void ReportDone(int citizen, int birthDay, int travelTicks); }
```
- Sampling rule (pure function of synced state): spawn a trip if `Hash(trip.Citizen, TotalDays, hour) % sampleDiv == 0`
  with `sampleDiv = max(1, tripsPerHour / (VisibleBudget / avgTripHours))`. Unsampled trips are **virtual**: they
  still add `weight = sampleDiv` to the congestion field along an estimated route, so traffic load, commute times and
  info views stay meaningful at 100k citizens (domain 2/3 decides how). **Local UI state (a followed citizen) must
  not influence sampling** (determinism); the UI may only *show* info for whichever citizen a sampled actor carries.
- Each visible vehicle/pedestrian stores `CitizenRef (index, BirthDay)` for the info card; if the pair no longer
  matches the citizen row, the label is dropped.
- `ReportDone(travelTicks)` gives **commute time feedback**: the worker's `LastCommute` (EMA) drives the commute
  wellbeing term, job quitting (> 8 sim hours-equivalent / 2 x free-flow twice) and `HomeSearch` score.
- Freight/trucks, services vehicles (ambulance, hearse, patrol), public transport are other domains; they read the
  same queue types (`Purpose = Sick, Corpse, Crime`) from `CitizenSim`.

### 3.10 Leisure and tourists

- **Leisure counter** `Leisure += 2 + hash%3` per day; at `LeisureThreshold = 100 + hash%80` the citizen produces a
  leisure trip at the next free evening/noon slot. Destination: sample K=4 of {park/plaza lots, commercial lots with
  `Entertainment` flag} in the same road component; `value = leisureGain*10 - distance*2`; outdoor lots x0.5 in
  rain/cold (hook from domain 10; **[W]** weather shifts preference). Visit sets `Leisure = 0`, `Wellbeing +8` for 3 days.
  No destination available -> `Wellbeing -10` and `LeisureUnmet` stat. Entertainment spend (restaurant/bar) is a
  `Sell()` with category Leisure (CS2 **[W]**).
- **Tourists** (phase C): `Attractiveness` 0..100 = park-coverage average x0.5 + landmark/attraction value + hotel
  quality x0.2. Arrivals per day `= Attractiveness * (1 + hotelBedsFree/20) / 100`, capped by free hotel beds and
  `MaxTouristsPct` of population (5%). A tourist household (1-4 members, `Flags.Tourist`, not in `Population`) occupies
  hotel beds for 2-5 days, makes 1-2 leisure/shop trips per day, spends `$1-3` per trip into commerce, leaves via the
  highway. Without hotels, only day-trippers (arrive by highway, shop, leave) at 1/4 the rate **[W: Tourism wiki]**.

### 3.11 Crime (aggregate-first)

Each home lot keeps `CrimeProb` 0..255 updated at its bucket: `+ unhappyShare*2 + poverty(brk 0)*20 + unemployedShare*30
- policeCoverage(lot)*2 - patrolRecent`, clamped. Monthly, per lot: with `rand(255) < CrimeProb / 4` a household
member with `Wellbeing < 100` becomes a **criminal** (flag, 10-day cooldown). The criminal picks the lot with highest
`CrimeProb` in 20 cells (K=5 samples), the event `{lot, victimResidents, day}` resolves **without a pursuit actor**:
`arrest% = 15 + policeCoverage(targetCell) * 0.7`; arrested -> jail 10 days (no work, no tax) or, 30% of the time,
prison (leaves city 60 days; city prison building later); escaped -> victim lot residents `Wellbeing -20` for 5 days,
building shows an icon. Optional visual: a sampled police car + a thief dot (domain 7/8). Stats: `CrimesPerMonth`,
`ArrestRate`, `CrimeAvg`. Mirrors CS2's target-by-crime-probability and wellbeing loop **[W]** at O(lots) cost.

### 3.12 Wellbeing and happiness with visible factors

CS2 happiness = Wellbeing + Health, shown as per-factor list **[W]**. OpenCity: **household happiness 0..100** =
`clamp(50 + sum(factor_i), 0, 100)`. Building/lot factors are computed once per day per lot and stored
(`short lotFactor[lot*F + i]`, F = 20) so the UI can explain *why*. Personal factors are computed on demand for the UI.

| Factor | Range (points) | Source / note |
|---|---|---|
| Electricity | -30 .. 0 | `CityBuilding.HasPower` (today) |
| Water | -25 .. 0 | `HasWater` |
| Sewage, garbage | -15 .. 0 each | future utility domains (hook returns 0 now) |
| Road access / commute | -20 .. 0 / -10 .. 0 | access today; commute from `LastCommute` |
| Police coverage | 0 .. +12 | coverage map (today +12*cov) |
| Crime | -15 .. 0 | lot `CrimeProb` (new) |
| Fire | 0 .. +10 | coverage |
| Health care | 0 .. +12 | coverage + own `Health` term `(Health-128)/12` |
| Education access | 0 .. +8 | school coverage; families with children get x2 near elementary **[W]** |
| Leisure / parks | 0 .. +15; unmet -10 | parks coverage + leisure counter |
| Entertainment / shopping | -8 .. +8 | reachable commercial within 12 cells |
| Air/ground pollution | -30 .. 0 | `GetPollution` (today -30%) |
| Noise | -15 .. 0 | domain 10 |
| Mail, telecom | 0 .. +5 each | future |
| Wealth | -10 .. +10 | wealth bracket |
| Home size fit | -8 .. +5 | beds per person; low density bonus **[W]** |
| Taxes | -20 .. +5 | existing rate term |
| Employment | -8 (unemployed member) .. +3 | |
| Land value | 0 .. +10 | existing 10% |
| Homeless | -60 | state |

The **existing** `UpdateHappiness` is kept as the *lot-factor producer* (same numbers); `CityBuilding.Happiness` becomes the
resident-weighted mean of household happiness (empty homes keep the formula), so level-up and abandonment rules in
`GrowableBuilding` keep working untouched. Citizen `Wellbeing` byte = household factors + personal events (crime
victim, leisure) decayed each day; `Health` is separate (3.6); household `Happiness` byte = `(mean(Health)+mean(Wellbeing))/2`.
Conditions mirror CS2: `Weak` = Health < 64, `Unwell` = Wellbeing < 80 (crime risk, -20% work efficiency), `Sick`,
`Homeless`.

### 3.13 Aggregates feeding the existing UI and demand

| Existing member | New definition |
|---|---|
| `Population` | residents in non-tourist households (excludes commuters) |
| `Workers` | employable residents: age >= 16, not student, not retired, not sick-long-term |
| `Jobs`, `FreeJobs` | `sum(jobCap)` of operational workplaces |
| `Unemployed` | employable residents without a job (seekers) |
| `Households` | count (was `pop/3`) |
| `EducatedRatio` | adults with Edu >= 2, percent |
| `HousingCapacity` | `sum(Beds)` of non-abandoned homes (today) |
| `AverageHappiness` | resident-weighted mean household happiness |
| `ResidentialAttraction` | recomputed from: jobs-vs-seekers balance **by level**, unemployment %, happiness vs 45, tax vs 10%, free homes %, homeless, free school seats (CS2 factors **[W]**) |
| `CityBuilding.Residents/Workers` | mirrored from `Lot.Residents` / `sum(jobFill)` on every change (no recount) |

New read-only members: `PopByAge[4]`, `PopByEdu[5]`, `WorkersByLevel[5]`, `VacantByLevel[5]`, `Homeless`, `Births`,
`Deaths`, `Immigrants`, `Emigrants` (last month), `CommuteAvgHours`, `Crimes`, `Tourists`, `UncollectedCorpses`.
Demand terms: office demand uses `VacantByLevel[2..4]` and `unemployedByEdu[2..4]`; industrial demand uses
`unemployedByEdu[0..1]` (cheap labour); commercial uses household `Spent`.

---------------------------------------------------------------------------------------------------

### 3.14 Determinism rules (hard requirements)

1. **Synced only.** `CitizenSim` mutates state only in its own `ITick` (player trait) and in methods called from other
   synced traits (`CityBuilding` register/unregister, `GrowableBuilding` level-up/abandon, order resolution). Never from
   widgets, `InfoViewLayer`, render code or `Game.RunAfterDelay`.
2. **Randomness.** Per-citizen rolls use a **pure hash** `Hash(index, TotalDays, salt)` (the existing `CityManager.Hash`
   pattern): independent of processing order, so queue budgets cannot change outcomes. Anything that needs a global
   sequence (candidate sampling, immigration picks) uses `world.SharedRandom`, always called in a fixed order.
3. **Integers only.** No `float`/`double` state. Percent maths as `a * p / 100` in `long` where products can pass 2^31
   (`Wealth * rate`). Use a floor-div helper for negatives.
4. **Iteration order.** Arrays by index; free-stacks LIFO; swap-remove lists mutate only inside deterministic event order;
   **no `Dictionary`/`HashSet` iteration** (lookups only); no LINQ in the loops; no threads/`Parallel`. Lots register in
   `ActorID` order like `CityManager.Register`.
5. **Level-up/replace**: `RegisterLot` is idempotent per cell and runs inside the frame-end replacement task, so the
   occupant lists never see a half-state.
6. **Sync hash**: `[VerifySync] int StateHash` (rolling hash over each processed bucket's mutable fields + `Population`
   etc.). Arrays cannot carry `[VerifySync]` ("unhashable type", see `Sync.cs`) and hashing 100k rows every tick would be
   costly, so the rolling hash is the contract. `Audit()` recounts in autotest.
7. **Actors for trips** are created in `AddFrameEndTask`, sampling is a pure hash (3.9), `CitizenRef` never alters sim.
8. **Version const** `CitizenSim.Version` mixed into the hash; yaml balance edits invalidate old replays/saves (expected).

### 3.15 Save games and replays

OpenRA saves are order replays that are fast-forwarded on load (CONTRACT.md intro rule 4), and `World.RequestGameSave`
also stores `IGameSaveTraitData` blobs. Consequences:
- **Do not serialise the citizen arrays.** Determinism recreates them. This also keeps saves tiny.
- **Load time = re-simulation**: 60 minutes of play at 25 ticks/s = 90k ticks; at ~1 ms/tick (50k citizens) that is
  ~90 s of fast-forward in addition to the existing sim. Keep `CitizenSim` below ~0.5 ms/tick at 50k; offer a progress bar
  (domain 10) and later **checkpoint blobs** through `IGameSaveTraitData` (compressed arrays, ~6 MB at 100k) if load times hurt.
- **Version drift**: any change to constants/yaml/algorithms breaks old saves. Gate by `CitizenSim.Version` and warn.
- `ISync` hash must stay O(1): see 3.14.

---------------------------------------------------------------------------------------------------

## 4. Interfaces and dependencies (what I need / what I provide)

**Provided by `CitizenSim`** (player trait, null-safe via `TraitOrDefault`):
`RegisterLot/UnregisterLot/EvictLot(cell)`, counters (3.13), `ITripSource`, `GetHouseholdCard(lot)` and
`GetCitizenCard(index, birthDay)` (read-only DTOs for UI), `LotFactors(lot)`, `DrainCorpses(n)`, `Version`, `StateHash`,
`DebugLine()`, `Audit()`, `RecentEvents` (passive ring of 256 `{day, kind, citizen, lot}` for the lifepath UI).

**Needed from other domains** (names are proposals for the architecture review):

| Domain | Need |
|---|---|
| 02/03 traffic and networks | `RoadLayer.ComponentOf(roadCell)`; `EstimateTravelTicks(fromRoad, toRoad)` (cached distance/congestion); consume `ITripSource`, spawn sampled actors with `CitizenRef`; `ReportDone`; virtual-trip congestion weight; highway capacity per day for immigration/commuters/tourists |
| 04 zoning and buildings | Yaml fields on `CityBuildingInfo`: `HouseSlots`, `Beds` (replaces `MaxResidents`), `JobMix` (percent by level), `ShiftWeights`, `Entertainment`; call `CitizenSim.RegisterLot/EvictLot` from growth/abandon/collapse/level-up; stable lot identity = cell |
| 05 economy | `ICommerceSink.Sell(lot, amount, category)`; company payroll/revenue hooks; tax split between income tax (citizen side) and company tax; rent/land value formula; `Efficiency` per lot (read) |
| 06 utilities | per-lot sewage/garbage/telecom/mail booleans or 0..100 values (default 100 = OK); garbage fee |
| 07 public services | school actors with `SchoolLevel`, `StudentCap`; hospital/clinic capacity (`ISickSink`); cemetery/crematorium `CorpseCapacity` + hearse spawn; police patrol/jail/prison; shelters; hotels (`Beds`); all via `ServiceBuilding` extensions |
| 08 public transport | mode availability + cost for `Mode` choice; transit trips as `Trip.Mode = Transit` |
| 10 environment/time/UI | `CityManager.HourOfDay`, `TicksPerDay` multiple of 24 (ask: keep 24 now), weather indoor/outdoor flag, noise and air pollution per cell, day/night, load-screen progress for save fast-forward |
| lead | extend frozen `CityAutoTest.LogStats` with `cm.Citizens?.DebugLine()` and the `citizen-audit` message |

---------------------------------------------------------------------------------------------------

## 5. Phased implementation plan and acceptance tests

All tests run through `mods/city/tools/autotest.sh green-valley "<spec>"` (log lines contain
`cit: pop=.. hh=.. age=c/t/a/s edu=n0..n4 emp=.. unemp=.. homeless=.. births=.. deaths=.. imm=.. emi=.. hap=.. hash=..`).
Determinism test = run twice with the same seed and compare `hash=` lines, then `autotest.sh replay:<file>` and expect no
`REPLAY ENDED EARLY OR WENT OUT OF SYNC`.

### Phase A - Households, ages, jobs by education (MVP, replaces residents counters)

Tasks:
- A1 `CitizenSim` trait + arrays + free-stacks + `Lot` registry keyed by cell; hook `CityBuilding`/`GrowableBuilding`.
- A2 Bucketed daily update loop, incremental counters, `Audit()`, `StateHash`.
- A3 Immigration + household types + home choice + move-out/emigration (no rent yet: rent shape only in score).
- A4 Age groups, birth, death (age hazard only), virtual outside hearse.
- A5 Job mix tables, job seeker matching, shifts as a field only, `Workers/Unemployed/Jobs` aggregates; mirror to
  `CityBuilding.Residents/Workers`; attraction recomputed.
- A6 `rules/citizens.yaml` (all constants), `DebugLine`, building info panel shows households.

Acceptance (scenario `basic`, `ticks=6000;timestep=5;log=250`):
1. `citizen-audit` OK at every log; `hash` identical in two runs and in replay.
2. After day 200: `pop >= 400`; `hh/pop` in 0.30..0.45; age shares children 8-25%, teens 3-12%, adults 55-80%, seniors < 15%.
3. `Workers` = employable within 5% of the recount; `emp + unemp = workers`; with jobs >= workers, `unemp% < 12`; with jobs = 0,
   `emp = 0` and attraction falls (no new immigrants after 30 days).
4. Old-system regressions: top bar `Population`, demand bars, tax income within +-25% of the previous build on the same scenario.
5. `scenario=stress` (zoned whole map): per-tick time of `CitizenSim` logged (`simMs avg/max`) `< 1.0 ms avg` at 20k+ citizens.
6. Lifecycle shortcut: with test yaml `DaysPerCitizenYear: 2`, after 1,500 days births > 0, deaths > 0, population stays within
   +-35% of its day-500 value (stable pyramid, no extinction/explosion).

### Phase B - Education, money, health, deathcare, happiness factors

Tasks:
- B1 School levels (actors from domain 7) + enrolment + study + graduation + education-driven wages.
- B2 Payday: wages, rent, benefits, tuition, wealth; residential tax from wages; `ICommerceSink` shopping; debt/eviction/homelessness.
- B3 Health/sickness/hospital queue, death by sickness, corpse queue + cemetery capacity (domain 7).
- B4 Lot factor table (3.12) and per-household happiness; UI breakdown.
- B5 Households leave-home and couple formation; commuters (10% of vacancies).

Acceptance:
1. With `school` built by day 100: >= 80% of children aged 6-11 enrolled until capacity; `edu1` grows; first graduations by day 100+180.
2. Payroll check: sum of paid wages in the month = sum over workers of `wage(JobLevel)` +-1; `Wealth` average rises from 0 to
   a positive steady state (> 1 month rent) in a balanced city; with rent x3 `homeless > 0` within 120 days.
3. `UncollectedCorpses < 5` with a cemetery; without one, outside hearse keeps it < 10 at 5k pop.
4. Residential tax income within +-30% of the old formula on `basic`; no negative-funds regression.
5. Happiness breakdown sums to the household value (UI test: `sum(factors) + 50` clamps to the displayed value).

### Phase C - Schedules, sampled trips, leisure, crime, tourists

Tasks:
- C1 `ITripSource` + hourly queues; `TripsPerHour[24]` stats; traffic integration with sampling and virtual load.
- C2 Shifts, peak spreading, commute-time feedback (quit job, move closer).
- C3 Leisure counter + destination choice; weather hook.
- C4 Crime events + jail/prison; `CrimeProb` heat map for info views.
- C5 Tourists + hotels + attractiveness; commuter out-flow; utility multipliers by education.

Acceptance:
1. Hourly trip histogram at 10k pop shows rush peaks: hours 7-8 and 16-17 each >= 3x the mean of hours 1-4.
2. Visible actors <= `VisibleBudget`; per-cell congestion changes when population doubles (load grows >= 1.5x on the main avenue).
3. Commute average (`CommuteAvgHours`) reported; moving a workplace far away raises quits within 60 days.
4. Crime: `CrimesPerMonth` with no police > 2x with police coverage 80% on the same seed.
5. Tourists > 0 only with a hotel; `Tourists / Population <= 5%`.
6. Perf: all citizen code (A+B+C) < 1.5 ms average per tick at 50k citizens, < 3 ms at 100k on the dev machine, frame tick budget
   respected at speed 3 (10 ms).

---------------------------------------------------------------------------------------------------

## 6. UI and art needs

**Panels/UI (domain 10 / widgets):**
- Population panel: age pyramid (4 bars), education bars (5), employment by level (workers vs jobs vs unemployed), homeless,
  households, births/deaths/immigrants/emigrants per month, commuters, tourists.
- Building info panel: households list (type, size, happiness face), residents/beds, rent, jobs by level fill/capacity,
  efficiency, "why unhappy" top-3 factors.
- Citizen card (select a sampled vehicle/pedestrian): generated name from `(index, BirthDay)` hash, age group + age, education
  badge, job and workplace, home, happiness with factor list, a short **lifepath** list from `RecentEvents` (born, graduated,
  hired, moved, retired).
- Info views: employment (jobs vs workers per building), education coverage (existing), crime heat, homeless markers, commuter
  routes (later). Top bar tooltips: births/deaths, unemployment %, homeless.
- Notifications: "Homeless citizens", "Not enough schools", "Uncollected corpses", "Crime wave", "Unemployment high".

**Art (procedural, 32x32 grid, tools in `mods/city/tools`):**
- `statusicons` additions (16x16): sick, homeless, crime, unemployed-building (no workers), corpse, graduation cap.
- Chrome icons: 4 age icons, 5 education badges, employed/unemployed, health cross, leisure, shopping bag, tourist, jail.
- Pedestrian sprites (optional, phase C): 4 age skins x 8 facings x 4 walk frames, 10x10 px tint variants; commuter/tourist skin.
- Vehicles from other domains: ambulance, hearse, police car (domain 7/8).
- Service actors and sequences: `highschool`, `college`, `university`, `cemetery`, `crematorium`, `hotel`, `shelter` (domains 4/7).

---------------------------------------------------------------------------------------------------

## 7. Sources

CS2 official and wiki:
- Dev Diary 11 Citizen Simulation and Lifepath: https://colossalorder.fi/?p=1851 and https://www.paradoxinteractive.com/games/cities-skylines-ii/features/citizen-simulation-lifepath (forum copy: https://forum.paradoxplaza.com/forum/threads/development-diary-11-citizen-simulation-lifepath.1596988/ , blocked for fetch)
- Economy 2.0 Part 1: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-one
- Wiki Citizens (wages, benefits, tuition, conditions): https://cs2.paradoxwikis.com/Citizens
- Wiki Education: https://cs2.paradoxwikis.com/Education ; Deathcare: https://cs2.paradoxwikis.com/Deathcare ; Crime: https://cs2.paradoxwikis.com/Crime ; Tourism: https://cs2.paradoxwikis.com/Tourism ; Happiness factor table: https://cs2.paradoxwikis.com/Happiness
- Household overview: https://www.modscities2.com/cities-skylines-2-households/
- Time scales (72 real minutes per game day at 1x): https://steamcommunity.com/sharedfiles/filedetails/?id=3072493428 ; Performance monitor mod: https://www.nexusmods.com/citiesskylines2/mods/147
- Demand factor thresholds (unemployment 20%, free workplaces 10%, happiness 45, tax 10%): https://forum.paradoxplaza.com/forum/threads/elucidation-of-every-demand-factor.1855666/ (seen via search snippet only)

Mod descriptions that document vanilla behaviour:
- Population Rebalance (age limits 21/36/84, death from 108 d, 3.8 children/family, CoupleHousehold bug, graduation 1-2 days): https://thunderstore.io/c/cities-skylines-ii/p/Infixo/Population_Rebalance/ and https://old.thunderstore.io/c/cities-skylines-ii/p/Infixo/Population_Rebalance/v/0.8.0/
- InfoLoom (hidden household demand, workforce by education, underemployment): https://github.com/Infixo/CS2-InfoLoom
- Realistic Workplaces and Households (vanilla fixed households, complexity levels): https://forum.paradoxplaza.com/forum/threads/realistic-workplaces-and-households.1699586/page-10
- Realistic Trips (three shifts, vanilla trip timing): https://mods.paradoxplaza.com/mods/77171/Windows
- CimRejuvenator (DeathCheckSystem role): https://github.com/decodxr/CimRejuvenator
- ECS system index (AgingSystem, HouseholdFindPropertySystem names): https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/Game/Simulation/AgingSystem/

Not found publicly (marked [D] design values above): exact sickness/death probabilities, happiness weights, rent formula, household spawn
weights, school graduation chances, shift hours. If exact values are required, decompile `Game.dll` (`AgingSystem`, `DeathCheckSystem`,
`HouseholdSpawnSystem`, `HouseholdFindPropertySystem`, `ResidentialDemandSystem`, `CitizenHappinessSystem`).

Repo references used: `mods/city/CONTRACT.md`; `OpenRA.Mods.City/Traits/Player/CityManager.cs`; `Traits/Simulation/CityManager.{Daily,Economy,Stats}.cs`;
`Traits/Buildings/{CityBuilding,GrowableBuilding}.cs`; `Traits/World/{TrafficManager,CityCoverageLayer,CityAutoTest}.cs`; `OpenRA.Game/{World,Sync}.cs`.
