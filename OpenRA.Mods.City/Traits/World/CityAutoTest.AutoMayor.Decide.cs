#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.City.Traits
{
	public partial class CityAutoTest
	{
		const byte AvenueId = 3;
		const byte BoulevardId = 4;

		sealed class ServiceRule
		{
			public ServiceKind Kind;
			public string[] Ladder;
			public int[] MinPop;
			public int TargetCoverage;
			public int PopPerProvider;
			public bool Essential;
			public CityProblem Problem;
			public int ProblemLimit;
		}

		static readonly ServiceRule[] Rules =
		[
			new()
			{
				Kind = ServiceKind.Garbage, Ladder = ["landfill", "incinerator"], MinPop = [60, 2500], TargetCoverage = 70, PopPerProvider = 900, Essential = true,
				Problem = CityProblem.Garbage, ProblemLimit = 40
			},
			new()
			{
				Kind = ServiceKind.Fire, Ladder = ["firehouse", "firestation"], MinPop = [120, 900], TargetCoverage = 65, PopPerProvider = 700, Essential = true
			},
			new()
			{
				Kind = ServiceKind.Police, Ladder = ["policebox", "police"], MinPop = [150, 900], TargetCoverage = 65, PopPerProvider = 700, Essential = true
			},
			new()
			{
				Kind = ServiceKind.Health, Ladder = ["clinic", "hospital"], MinPop = [180, 1500], TargetCoverage = 65, PopPerProvider = 800, Essential = true,
				Problem = CityProblem.Ambulance, ProblemLimit = 20
			},
			new()
			{
				Kind = ServiceKind.Education, Ladder = ["school", "highschool"], MinPop = [250, 1500], TargetCoverage = 60, PopPerProvider = 900, Essential = false
			},
			new()
			{
				Kind = ServiceKind.Deathcare, Ladder = ["cemetery"], MinPop = [500], TargetCoverage = 60, PopPerProvider = 2500, Essential = false
			},
			new() { Kind = ServiceKind.Parks, Ladder = ["park-small", "park-large"], MinPop = [100, 800], TargetCoverage = 55, PopPerProvider = 400, Essential = false },
		];

		static readonly string[] NodeOrder =
		[
			"health-hospital", "edu-highschool", "road-avenue", "road-roundabout", "water-treatment", "road-boulevard", "garbage-incinerator", "health-crematorium",
			"police-hq", "elec-gas", "road-oneway", "edu-college", "park-sports",
		];

		static readonly ZoneType[] ZoneOrder =
		[
			ZoneType.ResidentialHigh, ZoneType.ResidentialLow, ZoneType.CommercialHigh, ZoneType.CommercialLow, ZoneType.Industrial, ZoneType.Office,
		];

		int Count(string actor) => placedCount.TryGetValue(actor, out var n) ? n : 0;

		int plannedPower, plannedWater, plannedSewage;
		int committed;
		int savingFor;
		int savingForNext;
		int ruleRotation;

		// Funds not yet claimed by orders issued in this decision (orders resolve after the decision, so cm.Funds lags).
		int Avail => cm.UnlimitedMoney ? int.MaxValue : cm.Funds - committed;
		int opIncome, opExpenses;

		void CountPlaced(World w, Player p)
		{
			placedCount.Clear();
			plannedPower = plannedWater = plannedSewage = 0;
			foreach (var a in w.Actors)
			{
				if (a.Owner != p || a.IsDead || !a.Info.HasTraitInfo<CityPlaceableInfo>())
					continue;

				placedCount[a.Info.Name] = Count(a.Info.Name) + 1;
				var up = a.Info.TraitInfoOrDefault<UtilityProducerInfo>();
				if (up != null)
				{
					plannedPower += up.Power;
					plannedWater += up.Water;
					plannedSewage += up.Sewage;
				}
			}
		}

		// Operating balance of the last full month (without construction, tiles and loan principal): the number a mayor trusts.
		int OperatingBalance => opIncome - opExpenses;

		void ReadLastMonth()
		{
			opIncome = 0;
			opExpenses = 0;
			foreach (var kv in cm.LastMonthIncome)
				if (kv.Key is not ("loan" or "milestone" or "refund" or "test"))
					opIncome += kv.Value;

			foreach (var kv in cm.LastMonthExpenses)
				if (kv.Key is not ("construction" or "tiles" or "loan-repay" or "loan"))
					opExpenses += kv.Value;
		}

		// ---- the decision loop ----
		void MayorDecide(World w, Player p)
		{
			claimed.Clear();
			committed = 0;
			CountPlaced(w, p);
			var pop = cm.Population;
			var reserve = Math.Clamp(opExpenses / 6, 1000, 8000);

			Expand(w, p, pop, reserve);
			ManageUtilities(w, p, reserve);
			BuyNodes(w, p);
			savingForNext = 0;
			ManageServices(w, p, pop, reserve);
			BuyTiles(w, p, reserve);
			UpgradeRoads(w, p, reserve);
			BuildTransit(w, p, pop);
			savingFor = savingForNext;
		}

		void ManageUtilities(World w, Player p, int reserve)
		{
			// Power: add a plant when use is above ~70% of production.
			if (cm.PowerConsumed * 100 > plannedPower * 70)
			{
				var targetCell = At(Pitch * 8, 0);
				var plant = cm.IsUnlocked("powerplant-nuclear") && Avail >= 200000 ? "powerplant-nuclear" : "powerplant-coal";
				var cost = w.Map.Rules.Actors[plant].TraitInfo<CityPlaceableInfo>().Cost;
				if (Avail >= cost + reserve / 2 && PlaceService(w, p, plant, targetCell))
					MayorLog(w, $"power {cm.PowerConsumed}/{cm.PowerProduced}: {plant}");
			}

			// Water: towers.
			var wantTowers = 0;
			if (cm.WaterConsumed * 100 > plannedWater * 65)
				wantTowers = Math.Min(3, 1 + (cm.WaterConsumed - plannedWater * 65 / 100) / 90);

			for (var i = 0; i < wantTowers && Avail >= 1200 + reserve / 3; i++)
				if (PlaceService(w, p, "watertower", At(Pitch * 2, 3)))
					MayorLog(w, $"water {cm.WaterConsumed}/{cm.WaterProduced}: tower");

			// Sewage: treatment plant or outlet when production nears capacity.
			if (utilities != null && utilities.SewageProduced * 100 > Math.Max(utilities.SewageCapacity, plannedSewage) * 75 && Avail >= 12000 + reserve / 2)
			{
				const string Plant = "treatment-plant";
				if (cm.IsUnlocked(Plant) && PlaceService(w, p, Plant, At(Pitch * 6, 3)))
					MayorLog(w, $"sewage {utilities.SewageProduced}/{utilities.SewageCapacity}: {Plant}");
			}
		}

		void BuyNodes(World w, Player p)
		{
			if (progression == null || progression.DevPoints <= 0)
				return;

			foreach (var id in NodeOrder)
			{
				var i = progression.FindNode(id);
				if (i >= 0 && progression.CanBuyNode(i))
				{
					Issue(w, ProgressionOrders.UnlockNodeOrder(p, id));
					MayorLog(w, $"dev node {id}");
					return;
				}
			}

			// Then everything else that is available, cheapest tier first by index order.
			for (var i = 0; i < progression.NodeCount; i++)
			{
				if (progression.CanBuyNode(i))
				{
					var id = progression.GetNode(i).Id;
					Issue(w, ProgressionOrders.UnlockNodeOrder(p, id));
					MayorLog(w, $"dev node {id}");
					return;
				}
			}
		}

		static readonly string[] UpgradeOrder = ["landfill", "incinerator", "hospital", "clinic", "police", "firestation", "highschool", "school"];

		// Cash-rich cities buy the in-place upgrades of their service buildings (garbage depots, hospital wings, ...), a few per month.
		void BuyUpgrades(World w, Player p, int funds, int balance)
		{
			var services = p.PlayerActor.TraitOrDefault<ServiceSimulation>();
			if (services == null || funds < 40000 || balance < 8000 || savingFor > 0)
				return;

			var bought = 0;
			foreach (var name in UpgradeOrder)
			{
				foreach (var a in w.Actors)
				{
					if (a.Owner != p || a.IsDead || a.Info.Name != name || bought >= 3)
						continue;

					var index = 0;
					foreach (var up in a.TraitsImplementing<ServiceUpgrade>())
					{
						if (!services.HasUpgrade(a, index) && funds - committed > up.Info.Cost * 3)
						{
							committed += up.Info.Cost;
							Issue(w, ServiceOrders.BuyUpgradeOrder(p, a, index));
							MayorLog(w, $"upgrade {name} #{a.ActorID} option {index}");
							bought++;
							break;
						}

						index++;
					}
				}
			}
		}

		static readonly string[] PolicyOrder = ["education-boost", "pollution-management", "free-transit"];

		static readonly ServiceKind[] BudgetKinds =
			[ServiceKind.Police, ServiceKind.Fire, ServiceKind.Health, ServiceKind.Education, ServiceKind.Garbage, ServiceKind.Deathcare];

		bool budgetBoosted;

		// A comfortable treasury pays for better public services: city policies, then 120% budgets.
		void SpendSurplus(World w, Player p, int funds, int balance)
		{
			if (progression != null && funds > 90000 && balance > 12000 && savingFor == 0)
			{
				foreach (var id in PolicyOrder)
				{
					var i = progression.FindPolicy(id);
					if (i >= 0 && progression.IsPolicyAvailable(i) && !progression.IsPolicyActive(id, 0))
					{
						Issue(w, ProgressionOrders.SetPolicyOrder(p, i, 0, 1));
						MayorLog(w, $"policy {id} on");
						break;
					}
				}
			}

			if (!budgetBoosted && funds > 100000 && balance > 10000)
			{
				foreach (var kind in BudgetKinds)
					Issue(w, EconomyOrders.SetServiceBudgetOrder(p, kind, 120));

				budgetBoosted = true;
				MayorLog(w, "service budgets 120%");
			}
			else if (budgetBoosted && funds < 30000)
			{
				foreach (var kind in BudgetKinds)
					Issue(w, EconomyOrders.SetServiceBudgetOrder(p, kind, 100));

				budgetBoosted = false;
				MayorLog(w, "service budgets back to 100%");
			}
		}

		void ManageServices(World w, Player p, int pop, int reserve)
		{
			var balanceOk = OperatingBalance > -300 || Avail > 20000;
			var placed = 0;
			ruleRotation++;
			for (var r = 0; r < Rules.Length; r++)
			{
				// Rotate the starting rule so that no service starves behind the ones that are always under pressure.
				var rule = Rules[(r + ruleRotation) % Rules.Length];
				if (placed >= 2 || (!rule.Essential && !balanceOk))
					continue;

				// Highest affordable, unlocked tier for the current size.
				string actor = null;
				for (var t = 0; t < rule.Ladder.Length; t++)
					if (pop >= rule.MinPop[t] && cm.IsUnlocked(rule.Ladder[t]))
						actor = rule.Ladder[t];

				if (actor == null)
					continue;

				var providers = 0;
				foreach (var name in rule.Ladder)
					providers += Count(name);

				CPos target;
				if (providers == 0)
					target = rule.Kind == ServiceKind.Garbage ? At(Pitch * 5, 3) : CityCentre();
				else
				{
					if (providers >= 1 + pop / rule.PopPerProvider)
						continue;

					var covered = WorstCovered(rule.Kind, out target, out var avg);
					var pressed = rule.Problem != CityProblem.None && problems != null && problems.Count(rule.Problem) > rule.ProblemLimit;
					if (!pressed && (!covered || avg >= rule.TargetCoverage))
						continue;

					if (!covered)
						target = CityCentre();
				}

				var cost = w.Map.Rules.Actors[actor].TraitInfo<CityPlaceableInfo>().Cost;
				var need = cost + (rule.Essential ? reserve / 3 : reserve);
				if (Avail < need)
				{
					// A big-ticket service the city needs: stop discretionary spending until it can be paid.
					if (rule.Essential && providers > 0)
						savingForNext = Math.Max(savingForNext, need);

					continue;
				}

				if (PlaceService(w, p, actor, target))
				{
					placed++;
					MayorLog(w, $"service {rule.Kind}: {actor} (providers={providers}, pop={pop})");
				}
			}
		}

		CPos CityCentre()
		{
			// Centroid of the built blocks, fixed scan order.
			long sx = 0, sy = 0, n = 0;
			for (var k = 0; k < KCount; k++)
				for (var j = JMin; j < JMin + JCount; j++)
					if (IsBuilt(k, j))
					{
						var c = At(Pitch * k + 10, Pitch * j + 4);
						sx += c.X;
						sy += c.Y;
						n++;
					}

			return n == 0 ? At(10, 0) : new CPos((int)(sx / n), (int)(sy / n));
		}

		// Residential property with the lowest coverage of a service (first one wins ties) and the average.
		bool WorstCovered(ServiceKind kind, out CPos cell, out int average)
		{
			cell = default;
			average = 100;
			if (properties == null || coverage == null)
				return false;

			var worst = int.MaxValue;
			long sum = 0;
			var n = 0;
			var all = properties.All;
			for (var i = 0; i < all.Count; i++)
			{
				var pr = all[i];
				if (pr.Kind != PropertyKind.Residential || !pr.Operational)
					continue;

				var s = coverage.GetServiceCoverage(kind, pr.Origin);
				sum += s;
				n++;
				if (s < worst)
				{
					worst = s;
					cell = pr.Origin;
				}
			}

			if (n == 0)
				return false;

			average = (int)(sum / n);
			return true;
		}

		// ---- expansion ----
		void Expand(World w, Player p, int pop, int reserve)
		{
			var started = 0;
			var demandTarget = CityCentre();
			var unemployedPct = cm.Workers > 0 ? cm.Unemployed * 100 / cm.Workers : 0;
			var jobsShort = unemployedPct >= 6;
			var highDensityOpen = progression != null && progression.IsUnlocked("zone:" + ZoneType.ResidentialHigh);
			foreach (var zone in ZoneOrder)
			{
				if (started >= (jobsShort ? 3 : 2))
					break;

				if (progression != null && !progression.IsUnlocked("zone:" + zone))
					continue;

				// A player with dense housing available stops sprawling low-density homes.
				if (zone == ZoneType.ResidentialLow && highDensityOpen)
					continue;

				var demand = demandModel?.GetDemand(zone) ?? 0;
				var isHome = zone.Category() == ZoneCategory.Residential;
				if (isHome && (jobsShort || TooMuchHousing(pop)))
					continue;

				// High unemployment: jobs first, unless the market clearly has no use for the zone.
				if (jobsShort && !isHome && demand > -25)
					demand = Math.Max(demand, 25);

				if (demand < 15)
					continue;

				var free = FreeZonedCells(w, zone);
				var buffer = demand >= 60 ? 24 : 12;
				if (free >= buffer)
					continue;

				// Low-density R only when no better dense option is open.
				if (!PickBlock(w, demandTarget, zone, out var k, out var j))
				{
					serviceBlockWanted = false;
					continue;
				}

				var cost = BlockRoadCost(w, k, j, out _);
				if (Avail < cost + reserve / 4)
					continue;

				if (StartBlock(w, p, k, j, BlockState.Zoned, zone))
					started++;
			}

			// Service block on request (a service site search failed or the free site cells run low).
			if (serviceBlockWanted && CountBlocks(BlockState.Service) < 3 + pop / 3000)
			{
				serviceBlockWanted = false;
				if (PickBlock(w, serviceBlockTarget, ZoneType.None, out var k, out var j))
				{
					var cost = BlockRoadCost(w, k, j, out _);
					if (Avail >= cost + reserve / 4)
						StartBlock(w, p, k, j, BlockState.Service, ZoneType.None);
				}
			}
		}

		// Housing blocks may use at most ~48% of the developed land once the city has some size (3,000+) (jobs need room too).
		bool TooMuchHousing(int pop)
		{
			if (pop < 3000)
				return false;

			var homes = 0;
			var jobs = 0;
			for (var k = 0; k < KCount; k++)
				for (var j = JMin; j < JMin + JCount; j++)
					if (blocks[k, Ji(j)] == BlockState.Zoned)
					{
						if (blockZone[k, Ji(j)].Category() == ZoneCategory.Residential)
							homes++;
						else
							jobs++;
					}

			return homes * 100 > (homes + jobs) * 48;
		}

		// When land is used up and many people are jobless, a mayor trades some low-density homes for industry: bulldoze the block, zone it for jobs.
		int jobsBadMonths;
		bool rezonePending;
		int rezoneK, rezoneJ;
		ZoneType rezoneZone;

		void RezoneForJobs(World w, Player p)
		{
			if (rezonePending)
			{
				rezonePending = false;
				blocks[rezoneK, Ji(rezoneJ)] = BlockState.Zoned;
				blockZone[rezoneK, Ji(rezoneJ)] = rezoneZone;
				Issue(w, CityOrders.ZoneOrder(p, BlockCell(anchor, rezoneK, rezoneJ, 0, 0), BlockCell(anchor, rezoneK, rezoneJ, Pitch - 2, Pitch - 2), rezoneZone));
				MayorLog(w, $"rezone block {rezoneK},{rezoneJ} to {rezoneZone}");
				return;
			}

			var unemployedPct = cm.Workers > 0 ? cm.Unemployed * 100 / cm.Workers : 0;
			jobsBadMonths = unemployedPct >= 10 ? jobsBadMonths + 1 : 0;
			if (jobsBadMonths < 2)
				return;

			for (var k = 0; k < KCount; k++)
				for (var j = JMin; j < JMin + JCount; j++)
					if (CanStartBlock(w, k, j))
						return;

			var officeDemand = demandModel?.GetDemand(ZoneType.Office) ?? 0;
			var industrialDemand = demandModel?.GetDemand(ZoneType.Industrial) ?? 0;
			var zone = progression != null && progression.IsUnlocked("zone:" + ZoneType.Office) && officeDemand > industrialDemand
				? ZoneType.Office : ZoneType.Industrial;
			for (var k = KCount - 1; k >= 0; k--)
			{
				for (var j = JMin; j < JMin + JCount; j++)
				{
					if (blocks[k, Ji(j)] != BlockState.Zoned || blockZone[k, Ji(j)] != ZoneType.ResidentialLow || BlockHasService(w, k, j))
						continue;

					Issue(w, CityOrders.BulldozeOrder(p, BlockCell(anchor, k, j, 0, 0), BlockCell(anchor, k, j, Pitch - 2, Pitch - 2)));
					blocks[k, Ji(j)] = BlockState.Clearing;
					rezonePending = true;
					rezoneK = k;
					rezoneJ = j;
					rezoneZone = zone;
					jobsBadMonths = 0;
					MayorLog(w, $"clearing block {k},{j} (low-density homes) for {zone}: unemployment {unemployedPct}%");
					return;
				}
			}
		}

		bool BlockHasService(World w, int k, int j)
		{
			for (var dv = 0; dv < Pitch - 1; dv++)
				for (var du = 0; du < Pitch - 1; du++)
					foreach (var a in w.ActorMap.GetActorsAt(BlockCell(anchor, k, j, du, dv)))
						if (a.Info.HasTraitInfo<CityPlaceableInfo>())
							return true;

			return false;
		}

		int CountBlocks(BlockState state)
		{
			var n = 0;
			for (var k = 0; k < KCount; k++)
				for (var j = JMin; j < JMin + JCount; j++)
					if (blocks[k, Ji(j)] == state)
						n++;

			return n;
		}

		int FreeServiceCells(World w)
		{
			var n = 0;
			for (var k = 0; k < KCount; k++)
				for (var j = JMin; j < JMin + JCount; j++)
					if (blocks[k, Ji(j)] == BlockState.Service)
						for (var dv = 0; dv < Pitch - 1; dv++)
							for (var du = 0; du < Pitch - 1; du++)
							{
								var c = BlockCell(anchor, k, j, du, dv);
								if (zoneLayer.GetZone(c) == ZoneType.None && CellEmpty(w, c) && !claimed.Contains(c))
									n++;
							}

			return n;
		}

		// ---- service sites: service blocks first, then empty cells of zoned blocks ----
		bool PlaceService(World w, Player p, string actor, CPos target)
		{
			if (!w.Map.Rules.Actors.TryGetValue(actor, out var ai))
				return false;

			var bi = ai.TraitInfoOrDefault<BuildingInfo>();
			var placeable = ai.TraitInfoOrDefault<CityPlaceableInfo>();
			if (bi == null || placeable == null)
				return false;

			if (!FindSite(w, ai, bi, target, BlockState.Service, out var bestCell) && !FindSite(w, ai, bi, target, BlockState.Zoned, out bestCell)
				&& !FindLooseSite(w, ai, bi, target, out bestCell))
			{
				if (FreeServiceCells(w) < 12 || bi.Dimensions.X * bi.Dimensions.Y >= 4)
				{
					serviceBlockWanted = true;
					serviceBlockTarget = target;
				}

				MayorLog(w, $"no site for {actor} near {target.X - anchor.X},{target.Y - anchor.Y}");
				return false;
			}

			foreach (var t in bi.Tiles(bestCell))
				claimed.Add(t);

			committed += placeable.Cost;
			Issue(w, CityOrders.PlaceBuildingOrder(p, actor, bestCell));
			MayorLog(w, $"place {actor} at {bestCell.X - anchor.X},{bestCell.Y - anchor.Y}");
			return true;
		}

		bool FindSite(World w, ActorInfo ai, BuildingInfo bi, CPos target, BlockState inBlocks, out CPos bestCell)
		{
			var best = int.MaxValue;
			bestCell = default;
			for (var k = 0; k < KCount; k++)
			{
				for (var j = JMin; j < JMin + JCount; j++)
				{
					if (blocks[k, Ji(j)] != inBlocks)
						continue;

					for (var dv = 0; dv < Pitch - 1; dv++)
					{
						for (var du = 0; du < Pitch - 1; du++)
						{
							var c = BlockCell(anchor, k, j, du, dv);
							var d = Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y);
							if (d >= best)
								continue;

							if (!SiteFree(w, ai, bi, c, inBlocks == BlockState.Service))
								continue;

							best = d;
							bestCell = c;
						}
					}
				}
			}

			return best != int.MaxValue;
		}

		// Last resort: any unzoned owned cell along a road within 30 cells of the target (road verges and leftovers outside the grid).
		bool FindLooseSite(World w, ActorInfo ai, BuildingInfo bi, CPos target, out CPos bestCell)
		{
			var best = int.MaxValue;
			bestCell = default;
			for (var dy = -30; dy <= 30; dy++)
			{
				for (var dx = -30; dx <= 30; dx++)
				{
					var d = Math.Abs(dx) + Math.Abs(dy);
					if (d >= best)
						continue;

					var c = target + new CVec(dx, dy);
					if (!w.Map.Contains(c) || !SiteFree(w, ai, bi, c, true))
						continue;

					best = d;
					bestCell = c;
				}
			}

			return best != int.MaxValue;
		}

		bool SiteFree(World w, ActorInfo ai, BuildingInfo bi, CPos c, bool unzonedOnly)
		{
			foreach (var t in bi.Tiles(c))
				if (claimed.Contains(t) || (unzonedOnly ? zoneLayer.GetZone(t) != ZoneType.None : zoneLayer.IsReservedCell(t)))
					return false;

			return ConstructionUtils.CheckPlacement(w, ai, c, roadLayer).Valid;
		}

		// ---- tiles ----
		void BuyTiles(World w, Player p, int reserve)
		{
			if (progression == null || progression.Permits <= 0 || savingFor > 0)
				return;

			// Only when the owned land is almost used up: no connectable block is left.
			var open = false;
			for (var k = 0; k < KCount && !open; k++)
				for (var j = JMin; j < JMin + JCount && !open; j++)
					open = CanStartBlock(w, k, j);

			if (open)
				return;

			var g = progression.TileGrid;
			var centre = CityCentre();
			var best = int.MaxValue;
			int bx = -1, by = -1;
			for (var ty = 0; ty < g; ty++)
			{
				for (var tx = 0; tx < g; tx++)
				{
					if (progression.CheckBuyTile(tx, ty) != TileBuyResult.Ok)
						continue;

					var r = progression.GetTileRect(tx, ty);
					var d = Math.Abs(r.X + r.Width / 2 - centre.X) + Math.Abs(r.Y + r.Height / 2 - centre.Y);
					if (d < best)
					{
						best = d;
						bx = tx;
						by = ty;
					}
				}
			}

			if (bx < 0 || Avail < progression.GetTilePrice(bx, by) + reserve)
				return;

			committed += progression.GetTilePrice(bx, by);
			Issue(w, ProgressionOrders.BuyTileOrder(p, bx, by));
			MayorLog(w, $"tile {bx},{by} price=${progression.GetTilePrice(bx, by)}");
		}

		// ---- roads: arterials become avenues once unlocked (2 lanes, faster) ----
		// A segment needs the upgrade when it is all road and at least one cell is a lesser type (streets and gravel < avenue < boulevard).
		bool SegmentNeedsUpgrade(CPos a, CPos b, byte target)
		{
			var needs = false;
			foreach (var c in CityUtils.RoadPath(a, b))
			{
				if (!roadLayer.IsRoad(c))
					return false;

				var t = roadLayer.GetTypeId(c);
				if (t == 5)
					continue;

				if (t < target && !(target == BoulevardId && t == 2))
					needs = true;
			}

			return needs;
		}

		// Primary arterials (become boulevards): the avenue row and every third column.
		static bool IsPrimaryColumn(int k) => k % 3 == 1;

		void UpgradeRoads(World w, Player p, int reserve)
		{
			if (progression == null || !progression.IsUnlocked("road:avenue") || Avail < 2500 + reserve / 2 || savingFor > 0)
				return;

			var issued = 0;
			var avenue = new RoadToolOptions { TypeId = AvenueId, Replace = true };
			if (TryUpgrade(w, p, At(0, 0), At(Pitch - 1, 0), avenue, AvenueId, ref issued))
				return;

			var boulevards = progression.IsUnlocked("road:boulevard") && Avail > 15000;
			var boulevard = new RoadToolOptions { TypeId = BoulevardId, Replace = true };

			// Pass 0: avenue row, 1: even columns, 2: even rows, 3: all remaining streets; then boulevards on the primary arterials.
			for (var pass = 0; pass < 5 && issued < 6; pass++)
			{
				if (pass == 4 && !boulevards)
					break;

				for (var k = 0; k < KCount && issued < 6; k++)
				{
					for (var j = JMin; j < JMin + JCount && issued < 6; j++)
					{
						var u0 = Pitch * (k + 1) - 1;
						var v0 = Pitch * j;
						switch (pass)
						{
							case 0 when j == 0:
								TryUpgrade(w, p, At(u0, 0), At(u0 + Pitch, 0), avenue, AvenueId, ref issued);
								break;
							case 1 when k % 2 == 0:
								TryUpgrade(w, p, At(u0, v0), At(u0, v0 + Pitch), avenue, AvenueId, ref issued);
								break;
							case 2 when j % 2 == 0:
								TryUpgrade(w, p, At(u0, v0), At(u0 + Pitch, v0), avenue, AvenueId, ref issued);
								break;
							case 3:
								TryUpgrade(w, p, At(u0, v0), At(u0, v0 + Pitch), avenue, AvenueId, ref issued);
								TryUpgrade(w, p, At(u0, v0), At(u0 + Pitch, v0), avenue, AvenueId, ref issued);
								break;
							case 4 when j == 0:
								TryUpgrade(w, p, At(u0, 0), At(u0 + Pitch, 0), boulevard, BoulevardId, ref issued);
								break;
							case 4 when IsPrimaryColumn(k):
								TryUpgrade(w, p, At(u0, v0), At(u0, v0 + Pitch), boulevard, BoulevardId, ref issued);
								break;
						}
					}
				}
			}
		}

		bool TryUpgrade(World w, Player p, CPos a, CPos b, RoadToolOptions options, byte target, ref int issued)
		{
			if (!SegmentNeedsUpgrade(a, b, target))
				return false;

			committed += CityUtils.RoadPath(a, b).Count * (target == BoulevardId ? 26 : 18);
			Issue(w, NetworkOrders.BuildRoad(p, a, b, options));
			if (issued == 0)
				MayorLog(w, $"{(target == BoulevardId ? "boulevard" : "avenue")} upgrade from {a.X - anchor.X},{a.Y - anchor.Y} to {b.X - anchor.X},{b.Y - anchor.Y}");

			issued++;
			return true;
		}

		// ---- transit ----
		void BuildTransit(World w, Player p, int pop)
		{
			if (transit == null || lineBuilt || pop < 900 || Avail < 25000 || OperatingBalance < 0 || savingFor > 0)
				return;

			if (!busDepotPlaced)
			{
				if (cm.IsUnlocked("busdepot") && PlaceService(w, p, "busdepot", CityCentre()))
				{
					busDepotPlaced = true;
					MayorLog(w, "bus depot");
				}

				return;
			}

			// Stops every 8 cells along the avenue as far as it is built.
			var maxU = 0;
			for (var k = 0; k < KCount; k++)
				if (IsBuilt(k, 0) || IsBuilt(k, -1))
					maxU = Pitch * (k + 2);

			var n = 0;
			for (var u = 4; u <= maxU && n < 6; u += 8, n++)
				Issue(w, TransitOrders.PlaceStopOrder(p, At(u, 0), TransitMode.Bus));

			lineBuilt = true;
			lineOrderTick = w.WorldTick;
			MayorLog(w, $"bus stops x{n}");
		}

		int lineOrderTick;
		bool lineCreated;

		void FinishBusLine(World w, Player p)
		{
			if (!lineBuilt || lineCreated || w.WorldTick < lineOrderTick + 100 || transit == null)
				return;

			var ids = transit.Stops.Select(s => s.Id).Order().ToList();
			if (ids.Count >= 2)
			{
				Issue(w, TransitOrders.CreateLineOrder(p, TransitMode.Bus, false, ids, 0xE04040, "", 2));
				lineCreated = true;
				MayorLog(w, $"bus line stops={ids.Count}");
			}
		}

		// ---- monthly review: taxes and loans ----
		void MayorMonthly(World w, Player p)
		{
			ReadLastMonth();
			committed = 0;
			var funds = Avail;
			var bal = OperatingBalance;
			var demands = string.Join("/", cm.GetDemand(ZoneCategory.Residential), cm.GetDemand(ZoneCategory.Commercial),
				cm.GetDemand(ZoneCategory.Industrial), cm.GetDemand(ZoneCategory.Office));
			MayorLog(w, $"month {cm.Date} pop={cm.Population} funds={funds} hud-balance/mo={cm.MonthlyBalance} " +
				$"(hud in {cm.ProjectedIncome} out {cm.ProjectedExpenses}) operating={bal} (in {opIncome} out {opExpenses}) " +
				$"demand R/C/I/O={demands} orders={mayorOrders} milestone={cm.MilestoneName}");

			FinishBusLine(w, p);
			RezoneForJobs(w, p);
			SpendSurplus(w, p, funds, bal);
			BuyUpgrades(w, p, funds, bal);
			if (properties != null)
			{
				var uncompanied = new int[10];
				var all = properties.All;
				for (var i = 0; i < all.Count; i++)
					if (all[i].CompanyId == 0)
						uncompanied[(int)all[i].Kind] += all[i].TotalJobsFilled;

				MayorLog(w, $"uncompanied workers by kind None/Res/Com/Ind/Off/Wh/Ext/Svc/Transit/Sig = {string.Join("/", uncompanied)}");
				var pop0 = p.PlayerActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
				if (pop0 != null)
				{
					var free = new int[5];
					for (var i = 0; i < all.Count; i++)
						if (all[i].Operational && all[i].Kind != PropertyKind.Residential)
							for (var e = 0; e < 5; e++)
								free[e] += Math.Max(0, all[i].JobSlots[e] - all[i].JobsFilled[e]);

					var jobless = new int[5];
					for (var e = 0; e < 5; e++)
						jobless[e] = pop0.UnemployedByEducation((EducationLevel)e);

					MayorLog(w, $"jobless by edu {string.Join("/", jobless)} free jobs by edu {string.Join("/", free)}");
				}

				var freeCells = new int[ZoneOrder.Length];
				for (var z = 0; z < ZoneOrder.Length; z++)
					freeCells[z] = FreeZonedCells(w, ZoneOrder[z]);

				MayorLog(w, $"free zoned frontage cells RH/RL/CH/CL/I/O = {string.Join("/", freeCells)} service-cells={FreeServiceCells(w)}");
			}

			// Taxes: up when broke, down when the treasury is comfortably full (a rich city cuts taxes to attract people).
			var rate = cm.GetTaxRate(ZoneCategory.Residential);
			var wanted = rate;
			if (funds < 4000 && bal < 0)
				wanted = Math.Min(13, rate + 1);
			else if (funds > 150000 && bal > 0)
				wanted = Math.Max(4, rate - 1);
			else if (funds > 60000 && bal > 3000 && rate > 7)
				wanted = rate - 1;
			else if (funds > 30000 && bal > 500 && rate > 9 && cm.GetDemand(ZoneCategory.Residential) < 25)
				wanted = rate - 1;

			if (wanted != rate)
			{
				SetAllTaxes(w, p, wanted);
				MayorLog(w, $"taxes {(wanted > rate ? "up" : "down")} to {wanted}%");
			}

			if (economy != null)
			{
				var principal = economy.LoanPrincipal;
				if (principal == 0 && funds < 3000 && bal > -300 && w.WorldTick - lastLoanTick > 12000 && economy.LoanLimit > 0)
				{
					var amount = Math.Min(10000, economy.LoanLimit);
					Issue(w, EconomyOrders.SetLoanOrder(p, amount));
					lastLoanTick = w.WorldTick;
					MayorLog(w, $"loan ${amount}");
				}
				else if (principal > 0 && funds > principal + 20000)
				{
					Issue(w, EconomyOrders.SetLoanOrder(p, 0));
					MayorLog(w, $"loan repaid ${principal}");
				}
			}
		}

		void SetAllTaxes(World w, Player p, int percent)
		{
			for (var c = ZoneCategory.Residential; c <= ZoneCategory.Office; c++)
				Issue(w, EconomyOrders.SetCategoryTaxOrder(p, c, percent));
		}
	}
}
