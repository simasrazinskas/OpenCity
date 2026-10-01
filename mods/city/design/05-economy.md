# 05 - Economy and Companies (CS2 "Economy 2.0" for OpenCity)

Research domain 5 of 10. Scope: companies as agents, resources and recipes, prices, household consumption, trade, taxes, budget, loans, fees, demand. Written against the code at branch `t3code/openra-city-builder` (`CityManager.*.cs`, `CityBudgetLogic.cs`, `CONTRACT.md`).

Confidence tags used below: **(W)** = stated on the CS2 wiki, an official dev diary or patch notes. **(M)** = modder/community statement, partly outdated. **(U)** = uncertain: recalled from game internals or inferred; verify before relying on it. CS2 currency is shown as `C` (the CS2 credit sign).

---

## 1. CS2 mechanics (what we are approximating)

### 1.1 The loop

CS2 simulates a *closed money loop with leaks and injections* (W, dev diary #9):

- **Households** earn wages, pay rent, pay fees, and spend what is left on resources when their AI sees a deficit. A household picks a product by per-member preference weights, then one member travels to a shop that has it.
- **Companies** buy inputs, produce, sell, pay wages, rent, upkeep, utilities and tax. They go bankrupt when profit cannot cover costs.
- **Money sinks:** rent (the virtual landlord was removed in Economy 2.0; renters now share building upkeep), imports, company profit leaving, player expenses. **Money sources:** export income, tourists, wages of outside commuters, and (removed in Economy 2.0) government subsidies.
- Prices of resources are *constant*. What varies is **transport cost** (distance times weight), so availability and distance decide profit margin (W). Economy 2.0 split price into two parts: a discounted part paid by industrial buyers and a "service" part added for retail; households pay both (W).

### 1.2 Resource catalogue (W: wiki Economy page)

36 resources in three groups (the wiki counts 9 materials, 18 material goods, 8 immaterial; the game enum also holds money, mail and garbage - U).

**Raw materials (extracted in nine "specialized industry" areas; extractors cannot go bankrupt, they downsize):**
Wood, Grain, Livestock, Vegetables, Cotton, Crude Oil, Metal Ore, Coal, Rock/Stone. Oil and ore are finite. Fertile land and forests regrow but suffer from pollution.

**Processed goods and recipes (W, wiki Economy page; "A+B" = both inputs):**
Metals = Ore. Steel = Coal+Metals. Minerals and Concrete = Rock. Machinery = Metals+Steel. Petrochemicals = Oil+Grain. Chemicals = Oil+Minerals. Plastics = Chemicals+Petrochemicals. Pharmaceuticals = Chemicals. Electronics = Minerals+Plastics. Vehicles = Metals+Plastics. Beverages = Grain+Vegetables. Convenience Food = Grain+Livestock. Food = Livestock+Vegetables. Textiles = Cotton+Livestock+Petrochemicals. Timber = Wood. Paper and Furniture = Timber. Concrete and Timber also feed building upkeep; Food also feeds fire/rescue.

**Immaterial goods (offices and commercial):** Software (Electronics), Telecom (Electronics + Software), Financial (Software), Media (Software), all from Office zones. Lodging and Meals (Food) and Entertainment (Beverages) and Recreation (nothing) come from Commercial zones.

Hidden per-resource factors (W): **price** (how many units a household can buy per restock), **weight** (transport cost modifier: pharmaceuticals light, stone and steel heavy) and **space** (manufacturing footprint, drives rent exposure). Exact values are not exposed and no reliable public table exists (U). A modder dump shows the prefab fields `Wage0=1330` and `UnemploymentBenefit=600` as of March 2024, i.e. before later patches changed them (M).

### 1.3 Company agents

- **Three company classes** (W): material producers live in Industrial buildings, immaterial producers in Offices, consumer companies in Commercial buildings. Each has a brand (logo, colours) and a focus resource.
- **Location choice** (W): companies evaluate expected profit including transport cost (weight times distance), land value (rent), building size, workforce availability and education match. Industrial firms prefer low land value. Commercial firms follow residential unmet demand ("grocery need triggers a store"). Offices depend on workforce, not on customers.
- **Efficiency** (W, wiki): starts at 100%. Changes with average employee happiness, staffing (negative only when understaffed), city production specialization (bonus to firms whose resource is produced in volume in the city, max **+15% at 10 kt**), signature buildings ("Commercial/Industrial Efficiency"), electricity fee, water fee, and mail handling.
- **Profit** (W): sales minus wages, rent, input cost, utilities, transport, tax. Companies scale workforce and output to the profit-maximizing level; they go bankrupt when profit cannot cover rent, upkeep and inputs (W). They hire by education, and a small bonus applies to matching education (W, dev diary #9).
- **Building level** follows rent/upkeep payments: full payment raises condition by a constant until level-up; missed payment lowers it equally; degraded buildings collapse (W). Higher level means fewer utilities per unit, faster production, higher upkeep and rent.
- **Rent formula** (W, patch 1.1.5f1): `Rent = (LandValue + ZoneType * BuildingLevel) * LotSize * SpaceMultiplier`.
- **Production work** (W): work per unit is a preconfigured per-resource value since Economy 2.0, so production per worker is a table.
- **Trade** (W): overproduction is exported with transport fees; deficits are imported, creating truck traffic. Warehouses redistribute; trains and ships cut cost for heavy goods; "everything costs more when it enters the map as an import" (GameRant).
- **Workforce** (W): five education levels. Wages per month by version 1.3.3f1 (W): Uneducated 1,500; Poorly 1,800; Educated 2,100; Well 2,400; Highly 2,700 C. Outside commuters earn 1.1x and pay no local tax. Unemployment benefit 800 C/month for at most 10 days; family allowance 400 C; pension 1,200 C; tax applies only to income above a "Residential Minimum Earnings" of 1,400 C (W, Citizens page).
- **Demand** (W, wiki Zoning): commercial follows goods in stock and household purchasing power; industrial follows workforce, material access and storage demand (M: InfoLoom); office follows job seekers and business need. Vanilla neutral points: 100% sales capacity, 70% service availability, 75% employee capacity (M, RealEco).

### 1.4 Households

- Needs derive from **spendable money = income minus rent minus garbage fee** (W, Economy 2.0). A household that cannot cover rent spends less rather than complaining. Max shopping amount per household was cut from 4,000 to 2,000 (M).
- Preferences differ by age: children and teens consume more Media; adults and seniors more Financial (W). Biggest consumption: Food, Convenience Food, Beverages, Textiles (M).
- Density choice follows wealth and family size (W).

### 1.5 Taxes, fees, budget, loans

- **Tax rates** from **-10% to +30%**, per zone, then per group (W). Residential tax is on wages with *separate rates per education level*; Commercial/Industrial/Office tax is on **monthly profit, adjustable per resource** (negative tax subsidizes under-produced goods; positive tax damps overproduction) (W). Lower taxes level buildings up faster (W).
- **Service fees** (W): electricity default 0.2, water 0.3, garbage 1.0 (unit-less multipliers), healthcare 100 C/month per hospitalized citizen, education 50/100/200/200 C per student-month, parking 0-50 C, transit fares per line. Each 1% fee **below** default: +0.2% electricity use, +0.2% company efficiency, +0.05 happiness; each 1% **above**: -0.4%, -0.4%, -0.1.
- **Service budget** slider 50%-150%; at 50% a service runs at 25% efficiency, at 150% at 125% (W). Whether 100% maps to 100% or is a different point is not stated (U).
- **Service trade** (W): electricity import 5,000 C/MW, export 2,500 C/MW; water import about 0.1 and export about 0.05 C/m3; sewage export 0.1; imported police and fire 50 C/citizen, health 25, garbage 5. Economy 2.0 added a policy "Import City Services" (default off) and removed subsidies (W). City-service employee wages are now paid from player money (W).
- **Loans** (W): one loan, adjustable at any time, monthly cost "2.3% to 20% of the loaned amount" (as written on the wiki; the period of that rate is ambiguous - U), limit 100,000 to 26,100,000 C by milestone; City Hall lowers interest by 1 point, Central Bank by 2.
- **Inflation:** none documented. Prices are constants (W). OpenCity should not model inflation.

---

## 2. Current OpenCity state and gaps

### 2.1 What exists

All in `OpenRA.Mods.City/Traits/Player/CityManager.cs` (config, orders, calendar) and `Traits/Simulation/CityManager.{Daily,Economy,Stats}.cs`.

- **Money:** `Funds` is a `[VerifySync] int`. `TrySpend` / `AddFunds` record into per-month `Dictionary<string,int>`; `EndMonth()` publishes `LastMonthIncome` / `LastMonthExpenses`. Start $70,000. Negative funds only raise a notification.
- **Taxes are headcount formulas, not money flows** (`FillJobsAndCollect`):
  `tenths$ = persons * base * (100 + 25*(level-1))% * (70 + 0.6*landValue)% * taxRate / 10`,
  with bases (tenths of $ per person per month at 10%): residential 15, commercial 38, industrial 30, office 60. Persons = residents (R) or filled jobs (C/I/O). Collected once per 30-day month.
- **Expenses:** road cells x $1 (`upkeep-roads`), service `Upkeep` (`upkeep-services`), `construction`. Extra income: `milestone`, `refund`.
- **Jobs:** workforce = 55% of residents. `MaxJobs` is fixed per building level (`rules/growables.yaml`: com 4/6/9 low, 12/20/30 high; ind 10/16/24; off 12/20/32), filled proportionally. No education per worker; `EducatedRatio` only unlocks Office.
- **Demand** (`UpdateDemand`) is headcount-based: commercial wants `pop/4` jobs; industrial `8 + 25% of workforce + 10% of commercial jobs`; office `workforce * (8 + 0.2*EducatedRatio)%` after pop 800. Tax term +/-3 per point from 10%. Easing 3/day up, 6/day down.
- **Budget UI:** `CityBudgetLogic` lists last month's dictionaries by key (unknown keys are title-cased, so new categories appear with no UI work) and has four 0-30 tax sliders issuing `CityOrders.SetTaxOrder`.
- **Utilities** are a pooled balance with no price; only the producer's upkeep costs money.
- **Sync:** `OpenRA.Game/Sync.cs` hashes only `int`, `bool` and engine types, so `long` money needs a hi/lo split or a checksum int. `CityAutoTest` (frozen) already logs stats and supports replay comparison, the determinism test to reuse.

### 2.2 Gaps against CS2-style economy

| Gap | Effect today |
|---|---|
| No goods, no recipes, no inventory | Industry is "jobs that pay tax"; trucks (domain G) are decoration; no reason to zone industry except jobs |
| No company entity | Jobs belong to buildings; no profit, bankruptcy, efficiency, specialization, or per-building P&L |
| No wages / household cash | Citizens have no money; consumption does not exist; commercial demand is `pop/4`, unrelated to spending |
| Tax = f(headcount) | Raising tax cannot hurt company viability or wages; no -10..0% subsidies; no per-education or per-resource tax |
| No trade | Highway connections carry no goods and no money; no import/export prices, no trade balance |
| No fees | No price on power or water; no garbage, health or education fees |
| No service budgets, loans, or overdraft | Only "tax vs upkeep"; `Funds < 0` has no consequence |
| One category key set | Budget cannot show sectors (retail, industry, office), fees, interest, trade |
| Demand ignores supply chain | Players cannot be asked to "build a farm to feed the city" |

### 2.3 What can be kept

- `Funds`, `TrySpend`, `AddFunds`, the monthly dictionary pattern, and `LastMonthIncome/Expenses` stay as the **public API** other WPs already use (`ConstructionTools`, UI). The new economy plugs underneath.
- The daily tick (`TicksPerDay = 25`, `DaysPerMonth = 30`) stays the economic cadence.
- Tax-rate order plumbing stays; it is extended with new order strings in a **new file** (see 3.9), because `CityTypes.cs` is frozen.

---

## 3. Proposed design

### 3.0 Principles

1. **Plain data, not actors.** Companies and households-as-economic-records are structs in arrays inside one player trait `CityEconomy`. Thousands of entries cost nothing in actor overhead.
2. **Integer only.** Prices in cents (`int`), quantities in *milli-units* where fractional production is needed (`int`, 1000 = 1 unit), percentages as `int`. Cash per entity is `int` dollars; city-wide totals use `long` internally.
3. **Everything is orders-driven.** Player-facing changes (tax, fee, budget, loan) are new orders resolved by `CityEconomy`; replay/save works unchanged.
4. **Conservation first.** A per-month `Ledger` (long) records every transfer between sectors (household, company, city, outside). Tests assert that private money supply is bounded. Without this, a closed-loop economy silently inflates or collapses.
5. **Graceful fallback.** Until Citizens (domain 1) and Logistics (domain 6) land, the economy runs on aggregate households (one synthetic household per residential building) and instant "teleport" shipments with a distance cost. The interface to both is defined now (section 4).
6. **Prices constant, transport variable** (as in CS2). One optional mild supply/demand price multiplier is left for phase 5.

### 3.1 Resource catalogue (25 resources, expandable to CS2's 36)

Resources are data in `rules/economy.yaml` (a `Player: CityEconomy:` node with `Resources:` and `Recipes:` children), resolved at load to a dense `byte ResourceId` (0..31) so inventories are small fixed arrays. Names are fluent keys; icons are `res-<name>` in a new chrome collection (see section 6).

Prices are cents per unit (wholesale, what a buyer company pays). `Weight` is a 0-5 transport class (0 = immaterial). `q` is units produced per **fully staffed worker per day** at 100% efficiency (this is the "work per unit" table of CS2, inverted). All numbers were generated by a calibration script so that a worker adds roughly $42 of value per month (wage ~$30 + rent/fees ~$4 + profit ~$8); see 3.10.

| Id | Resource | Tier | Inputs per unit of output | Price | Weight | q/worker/day | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Grain | raw | - | 20 | 3 | 7.0 | farm; renewable, pollution-sensitive |
| 2 | Vegetables | raw | - | 30 | 2 | 4.7 | farm |
| 3 | Livestock | raw | - | 40 | 3 | 3.5 | farm |
| 4 | Cotton | raw | - | 35 | 2 | 4.0 | farm |
| 5 | Wood | raw | - | 25 | 4 | 5.6 | forestry; renewable |
| 6 | Ore | raw | - | 45 | 5 | 3.1 | mine; **finite** |
| 7 | Oil | raw | - | 60 | 4 | 2.3 | well; **finite** |
| 8 | Stone | raw | - | 18 | 5 | 7.8 | quarry |
| 9 | Timber | T1 | Wood 1 | 48 | 4 | 6 | sawmill |
| 10 | Petrochemicals | T1 | Oil 1 | 88 | 3 | 5 | refinery |
| 11 | Metals | T1 | Ore 1 | 73 | 4 | 5 | smelter |
| 12 | Concrete | T1 | Stone 1 | 41 | 5 | 6 | building upkeep input |
| 13 | Food | T1 | Livestock 1 + Vegetables 1 | 98 | 2 | 5 | |
| 14 | Beverages | T1 | Grain 1 + Vegetables 1 | 78 | 3 | 5 | |
| 15 | Textiles | T1 | Cotton 1 + Petrochemicals 1 | 158 | 1 | 4 | |
| 16 | Plastics | T2 | Petrochemicals 1 | 123 | 2 | 4 | |
| 17 | Furniture | T2 | Timber 2 | 143 | 3 | 3 | |
| 18 | Electronics | T2 | Plastics 2 + Metals 1 | 389 | 1 | 2 | |
| 19 | Vehicles | T2 | Metals 2 + Plastics 1 | 362 | 4 | 1.5 | |
| 20 | Machinery | T2 | Metals 2 + Electronics 1 | 605 | 4 | 2 | phase 3: B2B maintenance input of industry |
| 21 | Software | immaterial | Electronics 1 | 506 | 0 | 1.2 | office |
| 22 | Financial | immaterial | Software 1 | 599 | 0 | 1.5 | office, sold to households and companies |
| 23 | Media | immaterial | Software 1 | 599 | 0 | 1.5 | office |
| 24 | Meals | service | Food 1 | 196 (retail) | 0 | 1.4 | restaurant |
| 25 | Entertainment | service | Beverages 1 | 172 (retail) | 0 | 1.5 | venue |

Differences from CS2, deliberately: no Coal/Steel (Steel and Coal can come back in phase 5; the coal power plant keeps its current non-resource model), no Minerals/Chemicals (Electronics uses Plastics+Metals, Plastics uses Petrochemicals), no Convenience Food, Paper, Telecom, Pharmaceuticals, Lodging, Recreation. These ten are the **phase 5** additions; the loader must accept ids up to 63 so adding them is a YAML-only change.

Retail goods sold by Commercial companies: Food, Beverages, Textiles, Furniture, Vehicles (retail price = wholesale x 1.5), Meals, Entertainment (service companies). Office companies sell Financial and Media directly to households at wholesale x 1.6 (immaterial goods need no shop), and Software/Electronics-derived goods to other companies.

### 3.2 Company data model

One struct per company in `Company[] companies` with a free-list. Ids are monotonically increasing `int` (never reused) so ordering by id is stable. All fields integer.

```csharp
enum CompanyKind : byte { Extractor, Processor, Retail, Office }

struct Company
{
    public int Id;
    public int BuildingActorId;      // owning CityBuilding actor (0 = none, company is dormant)
    public int BuildingSortId;       // for deterministic ordering with CityManager.buildings
    public CompanyKind Kind;
    public byte Recipe;              // index into Recipes[] (output resource, inputs, q)
    public byte Level;               // mirrors building level 1..3 (CS2: up to 5)
    public byte State;               // Hiring, Active, Struggling, Closing
    public int Cash;                 // dollars; may go negative (insolvency counter)
    public int StockIn0, StockIn1;   // milli-units of the two inputs
    public int StockOut;             // milli-units of output
    public int StockCap;             // per resource, from building size x level
    public int WorkProgress;         // milli-batches carried over between days
    public byte Throttle;            // 0..100 percent of capacity the company tries to run
    public byte JobsMax0, JobsMax1, JobsMax2, JobsMax3, JobsMax4;   // slots by education
    public byte Filled0, Filled1, Filled2, Filled3, Filled4;        // workers by education
    public ushort EfficiencyPct;     // computed daily, 0..160
    public int SalesToday, CostsToday;           // cents, reset daily
    public int ProfitMonth;          // dollars accumulated this month (tax base)
    public int ProfitEma;            // dollars/month, smoothed (spawn/close decisions)
    public byte InsolventDays, LossMonths;
    public int Age;                  // days
}
```

Rules:
- **Ownership:** 1 company per non-residential growable in the MVP (the building is the "lot"). `CityBuilding` gets an `int CompanyId` (`[VerifySync]`), carried over when D replaces the actor on level-up, like `Residents` and `Workers`. High-density commercial/office may later hold 2-4 companies (`CompanySlots` in `CityBuildingInfo`); the array design already supports it.
- **Kind by zone:** Commercial -> Retail; Industrial -> Processor; Office -> Office; **Extractor** is placed by domain 6 on resource deposits (farm, lumber, quarry, mine, oil well) and carries no inputs. Extractors cannot go bankrupt: they lower `Throttle` and fire workers instead (W).
- **Jobs by education:** from `CityBuildingInfo.MaxJobs` split with a job-mix table per (kind, level), percent for edu 0..4:

| Kind, level | Edu0 | Edu1 | Edu2 | Edu3 | Edu4 |
|---|---|---|---|---|---|
| Extractor L1-3 | 70/50/40 | 30/40/40 | 0/10/20 | 0 | 0 |
| Processor L1 | 40 | 40 | 20 | 0 | 0 |
| Processor L2 | 20 | 40 | 30 | 10 | 0 |
| Processor L3 | 10 | 25 | 35 | 25 | 5 |
| Retail L1 | 50 | 40 | 10 | 0 | 0 |
| Retail L2 | 30 | 40 | 25 | 5 | 0 |
| Retail L3 | 20 | 35 | 30 | 15 | 0 |
| Office L1 | 0 | 20 | 50 | 30 | 0 |
| Office L2 | 0 | 10 | 35 | 40 | 15 |
| Office L3 | 0 | 0 | 20 | 45 | 35 |

  Office jobs need education, so Office demand depends on schools (as today, but now through real workers). Tune these in `rules/economy.yaml`.
- **Wages** (cents per day, paid daily = monthly / 30): edu0..4 = $25, $30, $35, $40, $45 per month (CS2's 1500..2700 divided by 60, which keeps income-tax revenue near today's ~$1.5 per resident). Wage is by the *worker's* education (U for CS2; some builds may pay by job level). A worker holding a job below their education earns their own education's wage but adds productivity only of the job's level: overqualified is a cost, which makes "educate more, but build office jobs" a real trade-off. A small matched-education bonus (+5% efficiency if >=80% of slots filled by exact-level workers) mirrors CS2 (W).
- **Efficiency** (percent, integer, recomputed daily):
  `eff = 100 + happinessMod + feeMod + specBonus + signatureBonus` then multiplied by `staffing = filled / jobsMax` when staffing < 100% (and 0 if no power or water). `happinessMod = (avgWorkerHappiness - 50) * 3 / 10` (+/-15). `feeMod = -4*(feeAbove100) / 10` per percent above default or `+2*(feeBelow100) / 10` below (CS2 constants, W). `specBonus = min(15, cityProductionOfResource * 15 / SpecializationFullAt)` with `SpecializationFullAt = 2000` units/month (CS2 uses 10 kt, W; our map is smaller).
- **Cash, rent and upkeep:** rent per day `= (landValue + zoneFactor*level) * lotCells * spaceMultiplier(res) / 30` cents using CS2's formula shape (W), with `zoneFactor` Commercial 6, Industrial 3, Office 8 (U, tuned) and `spaceMultiplier` from a per-resource `Space` value. Rent is a **sink** (to nominal landlords) in the MVP; phase 5 routes it into Concrete/Timber demand for building upkeep so it re-enters the loop.

### 3.3 Daily company step (staggered 1/25 per tick)

`CityEconomy` ticks every world tick and processes companies whose `Id % TicksPerDay == tickInDay`, so each company runs once per day and the load is flat (5,000 companies = 200 updates per tick). Order inside one update:

1. Recompute `EfficiencyPct`, `JobsMax*` (building level, abandonment, power/water).
2. **Order inputs:** target stock = 10 days of throttled consumption; the shortfall is posted to the market (3.4).
3. **Produce:** `batches = filledWorkers * q * eff/100 * throttle/100`, limited by input stocks and free output capacity; carry the fraction in `WorkProgress`. Consume inputs, add output (milli-units).
4. **Sell:** retail companies sell to shoppers during the day (3.5); processors/extractors sell through market matching or export.
5. **Pay:** wages (to household cash or, in aggregate mode, to the household pool), rent, utility fees, trade costs. Record `CostsToday`, `SalesToday`.
6. **Adapt:** if output stock is above 80% of cap, `Throttle -= 5` (min 20); if below 30%, `Throttle += 5`. `ProfitEma = (ProfitEma*29 + monthlyEquivalent)/30`. If `Cash < 0` then `InsolventDays++` else reset; at 20 insolvent days a non-extractor company **closes** (fire all workers, write off debt as a ledger sink, clear stocks, building stays and becomes a vacant slot). If `ProfitEma` is high and stock demand is unmet for 30 days, **expand** by raising `Throttle` first, level-up of the building second (domain 4).
7. **Month end:** tax (3.6), then `ProfitMonth = 0`.

**Spawn:** each day, for each vacant slot (id order) while zone demand `> 0`, score every allowed recipe by `expectedProfitPerMonth = capacityUnits * (sellPrice - landedInputCost) - payroll - rent - fees` (sell price = best of unmet local demand and export; input cost via the 3.4 lookup). Exclude recipes whose education mix cannot be staffed (unemployed of the needed levels under 50% of slots; W). Pick among the top 3 with `SharedRandom`. New companies get `Cash = 30 days of payroll + rent` (external injection, ledger `startup-capital`). Cap spawns per day at `1 + demand/25` per category.

### 3.4 Market and trade

**Local market, once per day per resource, deterministic.**

1. Buyers = processor input orders + retail restock orders + office input orders; sellers = companies with `StockOut > reserve`. Both lists are in ascending company id.
2. For each buyer, candidates are: up to 6 nearest sellers (precomputed per 8x8 cell block, refreshed every 5 days) and the **outside connection**. `landedCost = price + freight(weight, distance)`. Local seller price = base wholesale; import price = base x 115% + freight from the nearest highway end.
3. Buy from the cheapest landed candidate, partial fills allowed, until the order is filled or no candidate has stock. Money moves buyer -> seller; freight is paid by the buyer and leaves the private sector (ledger `freight`, later paid to truck companies).
4. **Export:** a seller whose stock stays above 80% of cap gets the surplus bought by the outside at `base x 90% - freight`. Money arrives from outside (ledger `export`).
5. **Freight** = `weight * manhattanCells * FreightMilliCentsPerWeightCell / 1000` cents per unit, `FreightMilliCentsPerWeightCell = 100` (0.1 cent). Ore over 40 cells costs about 20 cents on a 45 cent price; Textiles 4 cents. When domain 6 provides `ILogistics.HaulCost(from, to, resource)` (real road path, rail, port discounts) it replaces the formula.
6. **Throughput cap:** each `OutsideConnection` handles `TradeUnitsPerDay` (default 600 per highway) shared by import and export. Excess waits (stock builds up, throttle falls). With domain 6, a shipment = one truck (`capacity` units) and goods move on arrival; until then they transfer instantly.
7. **Trade statistics** are kept per resource per month: produced, consumed, imported, exported, in units and dollars; the Production/Trade panel shows surplus and deficit (CS2 "Production" tab, W).

### 3.5 Household consumption and shopping

Households come from domain 1. The economy reads and writes only these fields (interface in section 4): `Cash`, `Members[4]` by age group (child, teen, adult, elder), `WorkersByEdu[5]`, `HomeCell`, `Rent`.

- **Daily need accrual** per resource `r`: `need[r] += sum(age) members[age] * baseCons[r][age] / 30` (milli-units). Base consumption per resident per month (adult weight shown): Food 1.6, Beverages 1.0, Textiles 0.5, Furniture 0.37, Vehicles 0.18 (units, retail), Meals 0.8, Entertainment 0.6, Financial 0.07, Media 0.06 (Media 3x for teens, Financial 2x for elders, W). Total spend about $10.5 per resident per month.
- **Spendable money** `= cash - reserve - fees due`, with `reserve = 10 days of rent+fees`. Purchases are capped by spendable (CS2: needs derive from money after rent, W). Per-trip cap `MaxShoppingBudget = $60` (CS2 halved this cap, M).
- **Shopping event:** when `need[r] >= 1000` and spendable > price, the household chooses the retail company with stock that minimizes `price + 3 cents per road cell`, buys `min(need, stock, affordable)` units, pays the company. With domain 1 this is a real trip (citizen walks/drives and buys on arrival). In aggregate mode it happens once a day per building against the nearest 3 sellers within 25 road cells.
- **Unmet need** (no seller in reach or no stock) accumulates in `unmetNeed[r]` and is the primary input to commercial demand and a happiness penalty ("services availability", neutral at 70%, W).
- **Rent and fees:** rent is a sink to the landlord, paid daily; fees (power, water, garbage) are paid to the city.

### 3.6 Wages and taxes

- **Wages** flow company -> worker cash daily. **Income tax** is withheld at payment: `tax = wage * rate(edu)/100` where `rate(edu) = clamp(residentialRate + eduOffset[edu], -10, 30)`. Default `residentialRate = 10`, offsets 0. Negative rates are a subsidy (cost to the city). Optional later: a tax-free threshold (CS2: 1,400 C = 23 of our $; W), omitted in the MVP.
- **Company profit tax:** at month end `tax = max(0, ProfitMonth) * rate/100`, `rate = clamp(zoneRate + resourceOffset[res], -10, 30)` (W: per product type). Losses are not refunded and do not carry over (U for CS2). Default `zoneRate` Commercial 15, Industrial 15, Office 15 and all offsets 0; the +/-3 demand-per-point effect of today stays, now applied to the relevant demand factors (3.8).
- **Tax effect on viability:** because tax is on profit, a raised rate cannot bankrupt a company by itself, but a lowered rate raises `ProfitEma` and so spawn scores. Income tax lowers household spendable money, so it reduces commercial revenue. This is the CS2 coupling that is missing today.
- Booked as `tax-income-edu0..4` (UI aggregates under "tax-residential" for compatibility), `tax-commercial`, `tax-industrial`, `tax-office` plus an optional per-resource breakdown for the Taxation panel.

### 3.7 City budget, fees, service budgets, loans

- **Ledger categories** (string keys, so `CityBudgetLogic` shows them with no code change; add fluent names for polish). Income: `tax-income`, `tax-commercial`, `tax-industrial`, `tax-office`, `fee-power`, `fee-water`, `fee-garbage`, `fee-health`, `fee-education`, `export-services`, `milestone`, `refund`, `loan`. Expenses: `upkeep-roads`, `upkeep-<service>` (power, water, police, fire, health, education, parks), `wages-services`, `import-services`, `loan-interest`, `loan-repay`, `construction`. Subsidies do not exist (W: removed).
- **Fees:** `ElectricityFee`, `WaterFee`, `GarbageFee` as percent of a default (50-200%, default 100%). City income = fee * consumption. Defaults: power 130 cents per unit-month, water 150 (so a coal plant's $400 upkeep is covered at about 100% fee, U, tuned). CS2 coupling (W): each 1% above default lowers use and company efficiency 0.4% and happiness 0.1; each 1% below raises use and efficiency 0.2% and happiness 0.05. Households pay fees from cash before spendable money is computed; companies pay them as costs.
- **Service budget** per service, 50-150% (default 100). Upkeep scales linearly with it; `ServiceBuilding.Strength` (domain 7) scales 25% at 50%, 100% at 100%, 125% at 150% (piecewise; CS2 only gives the endpoints, U). City service workers' wages are an expense (`wages-services`) that **returns to household cash**, closing the loop (W: paid from player money since 1.1.5).
- **Loan:** one loan. Limit by milestone index: $10k, 25k, 60k, 120k, 250k, 500k, 1M, 2M (CS2 100k-26.1M scaled down, U). Monthly interest = `principal * annualRate / 12`; `annualRate` rises with utilization from 3% to 20% (CS2 2.3-20%, W) minus 1 point (City Hall) and 2 points (Central Bank) once those special buildings exist. Borrow and repay at any time through an order; the order clamps to the limit and to available funds when repaying. `Funds < 0` ("overdraft") charges 2% per month of the negative balance and blocks construction (existing `TrySpend` already refuses), plus the existing bankruptcy notification.
- **Import services** (domain 7) bills `import-services = population * perCitizenFee` while the policy is on (CS2: 50 police/fire, 25 health, 5 garbage per citizen, scaled; W for the shape).

### 3.8 Demand from the economy

`GetDemand(cat)` keeps its range and easing (up 3/day, down 6/day) but each target is now a sum of **named factors**, kept in an `int[]` per category so the UI can show the top five (CS2 shows factors in the demand tooltip, W). Factor sums are clamped to -100..100.

- **Commercial:** `localDemand` = `(100 - salesCapacityRatio%)` where ratio = retail capacity / household consumption over all retail resources (neutral 100%; RealEco shows vanilla neutral at 100%, M), plus `unmetNeed%`; `freeBuildings` = -min(30, vacantCommercialSlots*3); `workers`, `taxes` (+/-3 per point from 15), `happiness`, `utilities` as today.
- **Industrial:** `inputDemand` = for each recipe, unmet orders as % of orders; `exportDemand` = +10 per resource whose export price exceeds cost; `freeBuildings`; `workers` (unemployed with edu <= 2).
- **Office:** `serviceDemand` = unmet Financial/Media plus B2B Software orders; `educated` = share of unemployed with edu >= 2; `freeBuildings`.
- **Residential** (domain 4 and 1 own the formula): economy contributes `jobs/wages` (open job slots by education, average wage vs rent) and `affordability` = median household cash flow after rent.

### 3.9 Orders, synchronization, determinism, performance

- **Orders** live in a **new file** `Traits/Economy/EconomyOrders.cs` (the frozen `CityTypes.cs` stays untouched): `CitySetTaxDetail` (`ExtraLocation = (kind, index)`: 0 category, 1 education, 2 resource; `ExtraData` = percent + 10), `CitySetFee`, `CitySetServiceBudget`, `CitySetLoan`. Existing `CitySetTax` stays valid and maps to the zone rate.
- **Sync:** `CityEconomy` exposes `[VerifySync] int`: `Funds`, `LoanPrincipal`, `EconomyChecksum` (rolling hash of every company's id, cash, stocks, throttle and every ledger total, updated at each day), `CompanyCount`, and `PrivateMoneyHi/Lo` (the `long` supply split). Nothing of type `long` or `Dictionary` may be tagged.
- **Determinism rules:** arrays only in sync-relevant loops; no `Dictionary`/`HashSet` iteration; no floats; no LINQ; tie-breaks by id; randomness only via `world.SharedRandom` inside `Tick`; actor creation/disposal in `AddFrameEndTask`. The replay compare in `CityAutoTest.LogStats` must print `EconomyChecksum`.
- **Performance budget:** 5,000 companies and 20,000 shopping events per day average under 1 ms per tick on the dev machine; precomputed seller lists per block avoid per-event scans.

### 3.10 Balancing approach and targets

**Calibration script** (kept in `mods/city/tools/economy_calc.py`, not in the sim): for a target resident count, take workforce 55%, consumption per resident, retail markup 1.5, and value-added target $42 per worker-month, then explode the demand down the recipe tree to workers per sector. Result with the table above, **per resident**: retail and services 0.084 jobs, processors 0.066, extractors 0.088; total **0.24 jobs per resident (43% of the workforce)** for a fully self-sufficient chain. The rest of the 0.55 workers must come from city services (~0.03), offices (B2B, software and finance exports) and **export-oriented production**. Important consequence: today's rule "1 commercial job per 4 residents" would make retail jobs about 3x too many for $10.5 of spending per resident. Either retail jobs per resident fall to about 1 per 12 (fewer `MaxJobs` per commercial building, more industrial and office levels carry the workforce) or spending per resident rises. Choose the first.

**Money identity** (monthly, per resident, at default rates): gross wages about $16.5 (if everyone worked), income tax 10% = $1.65, spending $10.5, rent+savings the rest; company profit about $2.4 of which 15% tax = $0.36; fees about $0.5. City income about **$2.5-3.0 per resident**, expenses 80-90% of income at default service levels (a mildly profitable city, as today's ~$3.3).

| Population | Workers | Companies (retail / proc / extractor) | Wages $/mo | Consumer spend $/mo | City income $/mo |
|---|---|---|---|---|---|
| 1,000 | 550 | 8 / 4 / 4 | 16.5k | 10.5k | 2.5-3k |
| 10,000 | 5,500 | 85 / 43 / 43 | 165k | 105k | 25-30k |
| 50,000 | 27,500 | 420 / 220 / 220 | 825k | 525k | 125-150k |

(Company counts assume 10, 15 and 20 workers per company; adding export producers and offices roughly doubles them, so expect 1,000 to 3,000 company records at 50k population.)

---

## 4. Interfaces with other domains

`CityEconomy` is a `Player` trait owned by the economy WP. `CityManager.DailyUpdate` calls `economy.Daily()` after `SimulateBuildings()`; `CityManager.Funds` stays the single city treasury.

| Domain | Economy provides | Economy needs |
|---|---|---|
| **1 Citizens** | `ReceiveWage(householdId, edu, cents)` accounting, `TryBuy(householdId, resource, qtyMilli, shopCompanyId)` returning units bought and cents paid, `FindShop(resource, fromCell)` returning a company id and cell, `JobOffers` (per company per education: open slots, wage), tax and benefit rules (unemployment $27/month for 10 days, pension, family allowance) | `HouseholdView` records: `Cash`, members by age, workers by education, home cell, rent; `Hire(companyId, citizenId)` / `Fire` callbacks; education distribution; commute time to a job (reduces effective `Filled`). Until it exists: `AggregateHouseholds` adapter derived from `CityBuilding.Residents` and `EducatedRatio` |
| **6 Industry and logistics** | `ShipmentRequest {fromCompany, toCompany, resource, qty, priceCents}` queue; `StockOut` and `StockIn` hooks so trucks load and unload; per-resource `Weight`; trade statistics | `ILogistics.HaulCost(fromCell, toCell, resource)`; `OutsideConnection.TradeUnitsPerDay` and rail/port variants; extractor buildings with deposit left/rate (finite ore and oil) and a `CompanyKind.Extractor` company per extractor; warehouses (a company kind with no recipe whose stock is `StockCap` large); truck capacity |
| **4 Zoning and growth** | `VacantSlots` (buildings with `CompanyId == 0`), `Demand factors` for Commercial/Industrial/Office, "company spawned/left" events, building wealth/upkeep condition for level-up | `CityBuilding.CompanyId` carried across level-up; lot size and level; `Abandoned` flag when a company closes and the slot stays empty for N days; rent inputs (land value) |
| **7 Services** | per-service budget %, `FeeMultiplier` for power/water/garbage/health/education, upkeep and `wages-services` posting, service import policy cost | `ServiceBuilding` employees by education (from the same job-offer mechanism), upkeep, coverage strength; hospital and school usage for per-use fees |
| **10 UI** | read-only getters: ledger by category, trade stats, company list/summary for a selected building, demand factor arrays, fee/tax/loan state | orders only (`CitySetTaxDetail`, `CitySetFee`, `CitySetServiceBudget`, `CitySetLoan`) |
| **Utilities** (E) | `FeeMultiplier` | electricity/water use per building for fee billing (already `Use()` in `CityManager.Daily.cs`) |

Compatibility contract: the existing `CityManager` members (`Funds`, `TrySpend`, `AddFunds`, `MonthlyBalance`, `LastMonthIncome/Expenses`, `GetDemand`, `GetTaxRate`, `Population`, `Workers`, `Jobs`, `Unemployed`) keep their signatures. `ProjectedIncome/Expenses` become projections from the running ledger instead of the old headcount formula.

---

## 5. Phased implementation and acceptance tests

**Phase 0 - data and pure logic (1 task).** `rules/economy.yaml` with the 25 resources and 15 recipes, loader into immutable arrays (`ResourceTable`, `RecipeTable`), pure static `EconomyMath` (efficiency, freight, tax, wage, loan interest) with unit tests in `OpenRA.Test`. *Accept:* tests cover each formula; `./utility.sh city --check-yaml` shows `Errors: 0`.

**Phase 1 - MVP: companies on aggregate households (3-4 tasks).**
- Ledger and categories: `Ledger` (long), refactor `CityManager` taxes/upkeep into it; keep API.
- Company arrays, job mix, wages, daily step, spawn/close, instant local market + import/export at highway cells, retail shopping against aggregate households, income tax by education (aggregate) and profit tax.
- Demand factors replace `UpdateDemand` for C/I/O; commercial jobs per building lowered per 3.10.
- Economy log line (companies, bankruptcies, trade balance, money supply, income per category) for the autotest harness.
*Accept:* (a) `boot-test` scenario for 24 simulated months at default rates never drives `PrivateMoney` outside 0.5x-2x of its month-6 value; (b) at 10k population city income is within +/-30% of $2.5-3.0 per resident and expenses are 70-100% of income; (c) bankruptcies per month are under 5% of companies once past month 6; (d) a city with no farms imports food and the import bill shows in the ledger; adding a farm and a food factory cuts food imports; (e) **determinism:** two runs with the same orders and one replay print identical `EconomyChecksum` at every logged day; (f) average tick cost with 2,000 companies under 0.5 ms.

**Phase 2 - player levers (2 tasks).** New orders and getters: per-education income tax, per-resource profit tax, fees, service budgets, loan, overdraft. *Accept:* raising residential tax 10 -> 20% lowers consumer spending and commercial profit within 2 months; lowering Food profit tax to -5% raises Food company count within 4 months; a loan of the milestone limit shows interest in `loan-interest`; repay clears it; orders survive save/load (replay) with identical checksum.

**Phase 3 - real citizens (1 task, coordinated with domain 1).** Swap the aggregate adapter for `HouseholdView`: wages into household cash, shopping trips, job hiring by education, unemployment benefits. *Accept:* the money-supply test still passes; shopping trip counts equal retail sales within 1%; no household has `Cash < -$20`.

**Phase 4 - real logistics (1 task, with domain 6).** Shipments become truck trips, finite deposits, outside-connection throughput, freight from real path cost. *Accept:* a surplus of processor goods creates export truck traffic; cutting a highway stops imports and companies without inputs throttle to 20% within 10 days; steel/stone style heavy goods choose near sellers over far ones.

**Phase 5 - depth.** The remaining resources (Coal, Steel, Minerals, Chemicals, Pharmaceuticals, Convenience Food, Paper, Telecom, Lodging, Recreation), specialization bonus UI, signature buildings, per-building upkeep consuming Concrete/Timber (rent loop), tourists, optional mild price multiplier, building level driven by payment condition. *Accept:* YAML-only addition of a resource works without C# changes; all 36 resources have a producer in a test map.

---

## 6. UI and art needs (domain 10 and art WPs)

- **Budget panel** (extend `CityBudgetLogic`/`city-panels.yaml`): tabs Budget, Taxes, Fees and budgets, Loan, Production (trade). Income and expense lists by new category keys; an Income/Expense bar and a monthly balance sparkline. Expandable tax rows: Residential -> 5 education sliders; Commercial/Industrial/Office -> per-resource sliders (-10..+30). Slider range changes from 0..30 to -10..30. Loan slider with interest preview.
- **Production/trade panel:** one row per resource: produced, consumed, imported, exported (units and $), surplus/deficit bar (red/green), and the taxes applied. Sorted by tier.
- **Demand tooltip:** top factors per RCIO bar (Economy provides names and values).
- **Building info panel:** company name, kind, recipe, workers by education, stock, efficiency, monthly profit, status (Hiring, Struggling, Closing).
- **Art:** 25 resource icons 16x16 and 32x32 (`res-grain`, `res-food`, ...), tier backgrounds, a money-flow sparkline atlas, tax/fee/loan icons (extend `city-icons`), status icon "company closed". Fluent keys for each resource, recipe and category (`label-budget-fee-power`, ...). Optional: brand names list per resource (CS2 has logos; skip for MVP).

---

## 7. Sources

- Colossal Order, Dev Diary #9 Economy & Production: https://colossalorder.fi/news/development-diary-9-economy-production/ and https://www.paradoxinteractive.com/games/cities-skylines-ii/features/economy-production
- Dev Diary Economy 2.0 Part 1: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-one ; Part 2: https://www.paradoxinteractive.com/games/cities-skylines-ii/news/dev-diary-economy-part-two
- Patch 1.1.5f1 notes (rent formula, two-part price, subsidies removed): https://updatecrazy.com/cities-skylines-2-update-1-1-5f1-patch-notes-economy-2-0/
- CS2 Wiki: Economy https://cs2.paradoxwikis.com/Economy (raw tables via `?title=Economy&action=raw`), Citizens https://cs2.paradoxwikis.com/Citizens (wages, benefits), Services https://cs2.paradoxwikis.com/Services (fees, budget), Zoning https://cs2.paradoxwikis.com/Zoning, Company https://cs2.paradoxwikis.com/Company, Supply Chains https://cs2.paradoxwikis.com/Supply_Chains
- GameRant supply chain notes: https://gamerant.com/cities-skylines-2-how-handle-supply-chain/
- Modder sources (M): Infixo RealEco https://github.com/Infixo/CS2-RealEco and PrefabStore.cs; Economy Rebalance https://thunderstore.io/c/cities-skylines-ii/p/Infixo/Economy_Rebalance/; InfoLoom https://github.com/Infixo/CS2-InfoLoom
- Not found: per-resource price, weight, work-per-unit tables and base-consumption values for vanilla CS2; no public decompile writeup of `CommercialDemandSystem` or `ResourceBuyerSystem`. All OpenCity numbers in section 3 are our own and must be tuned by simulation.
