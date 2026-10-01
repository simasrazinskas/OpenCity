# OpenCity balance pass (AutoMayor, wave 2)

How the numbers were found, what was changed and why. Everything was measured with the scripted player
`scenario=automayor` on `green-valley`:

```
mods/city/tools/autotest.sh green-valley "ticks=96000;timestep=1;log=2400;scenario=automayor;shots=6000,18000,36000,60000,96000;center=40,0;zoom=0.5"
```

1 month = 2,400 ticks (one CityClock day). Targets were: population about 500 at month 3, 2,000 at month 8 and 5,000+ at
month 18; money dips early and is positive by month 3-5 without runaway wealth; unemployment mostly 3-12%; happiness 50-80;
traffic flow 50-85%; buildings level up, abandonment only under bad conditions.

## 1. The AutoMayor (`Traits/World/CityAutoTest.AutoMayor*.cs`)

An adaptive scripted player. It reads synced state only, acts only through real orders (`CityOrders`, `NetworkOrders`,
`ProgressionOrders`, `EconomyOrders`, `ServiceOrders`, `TransitOrders`) and scans blocks and cells in a fixed order. In a replay
it runs "dry": it re-derives the same decisions and logs them but issues nothing (the orders come from the recording), so its
`report mayor ...` lines take part in the replay diff.

- **Layout.** A 7-cell street grid anchored at the outside connection. Block (k, j) is a 6x6 interior between roads, the main
  avenue is the row v = 0. Roads are laid with `BuildRoad`, blocks are zoned with `Zone` (a block is one zone type).
- **Opening.** Avenue, three residential, one commercial and one industrial block, one service block (power, water).
- **Decisions every 300 ticks.** Expand first (cheap), then utilities, dev-tree nodes, services, tiles, road upgrades, transit.
  The monthly review (every 2,400 ticks) handles taxes, loans, policies, service budgets, upgrades and rezoning.
  - *Expansion.* Per zone type: when demand >= 15 and fewer than 12 (24 at demand >= 60) empty frontage cells are zoned, start the
    nearest connectable block (industry goes east, dense zones prefer the avenue). Once high density is unlocked it stops
    zoning low-density homes. Housing is held back when unemployment >= 6% or housing blocks pass 48% of developed land
    (3,000+ pop). Job zones are forced when unemployment is high unless the market clearly has no use for them (demand > -25).
  - *Utilities.* Power plant when use > 70% of planned capacity (nuclear when unlocked and cash >= $200k, else coal), water towers
    when use > 65%, treatment plant on sewage > 75%. "Planned" capacity counts buildings still under construction, so it never
    double-buys.
  - *Services.* Rules per `ServiceKind` with a ladder of buildings by population (clinic -> hospital, school -> high school,
    landfill -> incinerator ...). A new provider is placed next to the worst-covered home when average coverage < 60-70%, or when
    the matching problem count is high (garbage piles, ambulances waiting). Non-essential services wait for a non-negative
    operating balance. Rules rotate so none starves. A needed big-ticket service makes the mayor save: tiles, road upgrades,
    upgrades, policies and transit pause until it is affordable. Sites: service blocks first, then empty zoned frontage, then any
    free road verge within 30 cells.
  - *Roads.* Avenue row first, then every street becomes an avenue, primary arterials become boulevards once unlocked.
  - *Land.* Buys the nearest tile when no connectable block is left and a permit is available.
  - *Jobs.* If unemployment stays >= 10% for two months and no land is left, a low-density home block is bulldozed and rezoned
    for industry or offices.
  - *Money.* Operating balance = last month's income - expenses, without construction, tiles and loan principal. Taxes +1 when
    broke (max 13), -1 per month when rich (down to 4%). Loan of $10k only if funds < $3k with a sustainable balance, repaid
    when cash is plentiful. When cash-rich: city policies, 120% budgets on essential services, service upgrades.
  - *Dev points.* Priority list, then everything else.
  - *Transit.* One bus line (depot, six stops on the avenue) from 900 pop.
- **Log.** `report mayor ...` lines: every block, placement, upgrade, tax and policy decision, plus a monthly summary (HUD balance
  vs operating balance, demand bars, jobless by education vs free jobs by education, free zoned cells).

## 2. What was wrong, and what was changed

Findings in the integrated build, in the order they were found.

### 2.1 Logic changes (small, API-compatible)

| Where | What | Why | Before -> after |
|---|---|---|---|
| `CityEconomy.Companies` (`RefreshAggregates`) | Workplaces without a company have no job slots | Citizens were hired into empty commercial lots and the **city** paid their wages (206 of 880 residents' jobs at month 12, `wages-services` $46k/month at 1,500 pop). Slots return when a company moves in. | slots = ZON default -> 0 until a company exists |
| `CityEconomy.Ledger` (`ChargeWages`) | Home-business jobs (residential) are paid from outside the city | Self-employed income is not a city expense | city pays -> outside pays |
| `CityEconomy.Ledger` + `CityEconomyInfo.ServiceWageCityPercent` (new) | The city carries only part of its own payroll, the rest is booked as regional grants (`upkeep-return`) | Upkeep and service wages were charged on top of each other; early cities could not carry a coal plant ($1,000 upkeep + 20 wages) | 100% -> 80% (yaml) |
| `CityEconomy.Demand` (`ProjectedMonthlyServiceWages`, `ProjectedMonthlyIncome`), `CityManager.Economy` | HUD balance includes service payroll and last month's company profit tax | The HUD showed +$3.6k/month while the city lost $15k/month (and later -$38k while it earned +$24k: company tax is booked once per month and was missing from the run rate) | HUD error ~ -15k..+60k -> within ~8k of the real balance |
| `CitizenSim` (new `ShoppingUnitsMilliPerPerson`, `ShoppingItemsPerVisit`) | A household shops for several goods per visit and may buy more units | Households saved ~85% of wages (hh cash $2M, retail 8% of wages). Shops starved, 83 closed, jobs vanished | 1 item, 2 units/person -> 4 items, 6 units/person (yaml) |
| `CitizenSim` (new `JoblessMonthsToEmigrate`, `JoblessEmigratePercent`) | Households whose adults were all jobless for N months may leave (35%/month) | Jobless uneducated people never left and kept the R demand and unemployment high | off -> 4 months |

### 2.2 Numbers

| File | Setting | Why | Before -> after |
|---|---|---|---|
| `rules/citizens.yaml` | `ImmigrationBase` | Early growth was capped at 40+pop/8 households per month while homes sat empty | 40 -> 60 |
| | `ShoppingCentsPerPerson` | see 2.1 (spending as a share of income) | 900 -> 3000 |
| | `ShoppingUnitsMilliPerPerson`, `ShoppingItemsPerVisit` | see 2.1 | 2000/1 -> 6000/4 |
| | `JoblessMonthsToEmigrate` | see 2.1 | 0 -> 4 |
| | `SickPPM` | 6% of the city fell sick **every month** (3,000 sick at 12k pop), the hospital system was always overloaded | 60000 -> 35000 |
| `rules/economy.yaml` | `ServiceWageCityPercent` | see 2.1 | 100 -> 80 |
| | `PowerFee` | Coal plants lose money at the default fee (plant: $400 upkeep + wages for 300 units, fee income about $4/unit) | 130 -> 180 |
| | `StartupDays`, `CloseAfterInsolventDays`, `ReopenMonths` | Companies were seeded with 15 days of cash and closed after 45 insolvent days, so every cash dip became a bankruptcy and a lay-off wave (157 bankruptcies in 36k ticks) | 15/45/2 -> 45/120/1 |
| | `TradeUnitsPerDay` | **The outside connection's import cap (2,000 units/day) was binding.** One processor chain (office -> electronics -> plastics -> petrochemicals) needs thousands of units a day, so firms starved in rotation and offices lost $80-135k/month. At the old value the economy could not employ more than ~4,000 people | 2000 -> 30000 |
| `rules/player.yaml` (CityManager) | `UpkeepScalePercent` | Upkeep was counted twice (budget + wages). Combined with the payroll share the early game stays near break-even and the late game does not run away | 250 -> 130 |
| `rules/traffic.yaml` | `SlotsPerLane` | 1 cell = 16 m holds about 5 cars, not 2. With 2 slots the grid gridlocked at ~5k pop (flow 7-20%, 6,000 vehicles = the cap) | 2 -> 5 |
| | `DischargeMilliTicks` | follow-up of the above | 1500 -> 1200 |
| | `WalkMaxCells` | Trips under 4 cells (65 m) drove; shopping and leisure inside two blocks now walk | 4 -> 14 |
| `rules/services.yaml` | `MaxJobs` of staff-heavy services (coal 20->10, gas 30->15, nuclear 60->30, incinerator 25->14, recycling 20->12, clinic 15->10, hospital 60->36, school 15->10, high school 30->20, college 50->30, university 80->45, police 12->8, HQ 50->30, prison 30->20, post office 14->8, sorting centre 40->20, city hall 40->24, welfare 20->12) | The city pays these wages: one coal plant cost $3,000/month for 300 power | about -45% |
| | `Cost` of big-ticket services (hospital 60,000->28,000, incinerator 55,000->20,000, recycling 40,000->15,000, high school 25,000->15,000, college 60,000->35,000, university 120,000->70,000, police HQ 40,000->20,000, prison 35,000->20,000, sorting centre 60,000->30,000, city hall 30,000->20,000) | A hospital cost a month of income at 12k pop and 8x that at 1.5k: unaffordable when it is needed (health coverage 40% for 20 months, 300 piled garbage buildings) | see list |
| | `ServiceSimulation.PatrolRelief` (wave-2 knob) | Patrols should matter | 0 -> 5 |
| | `ServiceSimulation.WildfirePermille` (wave-2 knob) | Rare wildfires on hot clear days, no effect on a built-up city, flavour only | 0 -> 3 |
| `tools/gen_zon_rules.py` -> `rules/growables.yaml` | `HouseholdsPerCellMilli` high-density residential | 18 residents per cell against 4-6 jobs per cell: 20% unemployment, congestion | 6000 -> 4500 |
| | `JobsPerCellMilli` commercial high / office / industrial | jobs per cell are the only thing that scales employment on limited land | 6000 -> 7000 / 6000 -> 7500 / 4000 -> 5000 |
| `rules/progression.yaml` | `Tiles` per milestone 1-13 | Land stopped growth: tiles only come from milestones, XP comes from population, so a full map froze the city at 7-9k | +1 each (2 -> 3, 3 -> 4) |

### 2.3 Tried and reverted

| Experiment | Result |
|---|---|
| `ImportPercent` 70-85 and `ExportPercent` 120-135 | Arbitrage: buy inputs below, sell outputs above wholesale. Company profit $0.9M/month, city funds > $1M. Back to the defaults (100/100). Real fix was the trade cap above. |
| Low-density homes only | 1 household per lot, the map filled at 3-4k pop. The mayor stops zoning them once high density is available. |
| `UpkeepScalePercent` 100 + wage share 55% | Wealth ($350k at month 22). 130 / 80 gives $10-70k and a positive but shrinking surplus. |

### 2.4 New wave-2 knobs left neutral

`ParkDecayPerDay`, `HealthFeeCents`, `EducationFeeCentsPerStudent` (SVC): the economy has a comfortable surplus without
fees, parks need maintenance fleets the mayor does not build. `PriceElasticity` (ECO, 60 caused bankruptcies),
`MaintenanceRentPercent`, `CornerLandValueBonus`, `AccidentOdds` (defaults are fine), `TerminalLegCells`/`HighwayLegCells` (IND),
`CropGrowthPercent` (ENV, not applied to farms yet; a winter would zero farm output, so it stays off), disasters (lobby option, off).

## 3. Results (final build, `scenario=automayor`, 96,000 ticks = 40 months)

| Month | Date | Pop | Funds | Balance/mo | Milestone | Unemployment | Happiness | Traffic flow |
|---:|---|---:|---:|---:|---|---:|---:|---:|
| 1 | Apr 2026 | 121 | 44,024 | -1,208 | Hamlet | 0% | 59 | 77% |
| 2 | May 2026 | 298 | 27,145 | 510 | Small Town | 0% | 59 | 77% |
| 3 | Jun 2026 | 577 | 31,482 | 409 | Town | 0% | 61 | 80% |
| 4 | Jul 2026 | 943 | 32,971 | 1,746 | Large Town | 0% | 63 | 79% |
| 5 | Aug 2026 | 1,467 | 7,571 | 3,367 | Large Town | 0% | 65 | 71% |
| 6 | Sep 2026 | 2,069 | 2,792 | 4,332 | Small City | 3% | 67 | 67% |
| 7 | Oct 2026 | 2,938 | 6,314 | 10,187 | City | 0% | 66 | 67% |
| 8 | Nov 2026 | 3,862 | 5,510 | 18,253 | City | 0% | 66 | 67% |
| 9 | Dec 2026 | 4,940 | 6,069 | 23,619 | Big City | 0% | 67 | 64% |
| 10 | Jan 2027 | 6,096 | 13,608 | 32,288 | Big City | 1% | 67 | 58% |
| 11 | Feb 2027 | 6,675 | 15,869 | 38,286 | Metropolis | 4% | 72 | 60% |
| 12 | Mar 2027 | 7,060 | 13,955 | 34,688 | Metropolis | 5% | 77 | 64% |
| 14 | May 2027 | 7,438 | 7,991 | 27,621 | Metropolis | 8% | 83 | 64% |
| 16 | Jul 2027 | 7,510 | 11,374 | 19,643 | Metropolis | 10% | 84 | 65% |
| 18 | Sep 2027 | 7,566 | 11,022 | 16,353 | Metropolis | 12% | 86 | 65% |
| 20 | Nov 2027 | 7,620 | 13,852 | 11,245 | Metropolis | 13% | 87 | 62% |
| 22 | Jan 2028 | 7,846 | 9,051 | 11,123 | Metropolis | 12% | 87 | 55% |
| 24 | Mar 2028 | 8,051 | 10,848 | 15,856 | Metropolis | 12% | 87 | 62% |
| 26 | May 2028 | 9,238 | 25,069 | 5,925 | Large Metropolis | 9% | 84 | 63% |
| 28 | Jul 2028 | 10,135 | 17,258 | 22,547 | Large Metropolis | 10% | 84 | 64% |
| 30 | Sep 2028 | 10,382 | 15,116 | 35,635 | Large Metropolis | 9% | 84 | 61% |
| 32 | Nov 2028 | 10,372 | 51,708 | 35,194 | Large Metropolis | 10% | 87 | 62% |
| 34 | Jan 2029 | 10,288 | 24,643 | 26,307 | Large Metropolis | 9% | 83 | 54% |
| 36 | Mar 2029 | 10,182 | 73,451 | 13,442 | Large Metropolis | 10% | 84 | 64% |
| 38 | May 2029 | 10,062 | 66,586 | 1,167 | Large Metropolis | 11% | 87 | 64% |
| 40 | Jul 2029 | 10,001 | 60,785 | | Large Metropolis | 11% | 85 | 64% |

"Balance/mo" is the mayor's operating balance of the last full month (income - expenses, without construction, tiles and loans).

- **Population:** 577 at month 3, 2,938 at month 7, 4,940 at month 9, 7k at month 12, then land-limited at 7.5-10k.
- **Money:** the opening spends the start funds ($70k -> $44k), the balance is positive from month 2-3, the treasury stays
  between $3k and $75k because the mayor keeps investing (services, roads, tiles); taxes are lowered when cash piles up.
- **Milestones:** Hamlet at month 1, Small Town (high density) at month 2, Town at month 3, Large Town at month 4, Small City
  at month 6, City at month 7, Big City at month 9, Metropolis at month 11, Large Metropolis at month 26. XP is mostly 2 per
  resident, so milestones follow population. At speed 3 a month is 24 s, so the first three milestones fall in the first two
  minutes of play. A flatter early curve would need larger XP values in `rules/progression.yaml` (all milestones x2 would put
  Small Town at month 4); left as is because the requested population pace needs high density early.
- **Unemployment:** 0-5% until month 12, 8-13% afterwards (education mismatch: the jobless are uneducated, the open jobs need
  education; the economy cannot place them).
- **Happiness** 59-67 early, 83-87 late (services meet 75-95% of homes), **flow** 54-80%.
- **Levels:** L1-L5 buildings all appear; abandonment stays at 0-2 buildings.
- **Services (month 24, pop 8k):** garbage 100% of homes covered, health 95%, death care 83%, elementary school 80%,
  high school 94%, police 92%, fire 94%, parks 48%.

### Determinism and performance

- Replay check: `autotest.sh green-valley "...scenario=automayor"` recorded, then `autotest.sh replay:<orarep>` with the same spec:
  1,495 `date=` and `report` lines (including every `report mayor` line and the final tick) identical, `drift=0` throughout.
- Speed: 565-830 ticks/s for the long AutoMayor run (>= 150 required), `stress` 620-730 ticks/s (>= 100 required).
- `basic`, `bulldoze`, `stress` and `full` still run; `full` now ends at -$16k instead of -$88k (it zones too little to be a
  balance test).

## 4. Remaining issues

- **Land caps the city** at about 10k pop. Permits come only from milestones and XP comes from population. A real player
  would also densify (levels) and rezone. The mayor rezones low-density homes only when unemployment stays >= 10%.
- **Unemployment is education-driven** (about 10-14% late): uneducated jobless, open jobs for the educated. More schools do not
  change adults; low-skill jobs are full. An adult-education path or a lower immigrant education spread would fix it.
- **Transit is irrelevant**: one line, almost no riders (`gaveup` > `boardings`). Roads (avenues, boulevards) and walking carry the load.
- **Garbage and ambulances still pile up** in dense districts (140-300 buildings flagged) because trucks queue in traffic; upgrades help only partly.
- **Chaotic sensitivity**: the economy (company spawn order, closures) and the traffic flow differ by 10-20 points between runs that
  differ by one early decision (before the final traffic capacity change one run fell to 22-31% flow for six months).
- **Power is expensive** (coal 300 units per plant, 20+ plants at 10k pop). More compact plants (gas, solar) are worse per unit; nuclear is
  the only land-efficient option and only affordable late.
