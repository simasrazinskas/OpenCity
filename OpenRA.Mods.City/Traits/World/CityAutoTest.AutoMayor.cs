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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// AutoMayor: an adaptive scripted player for balance runs ("scenario=automayor").
	// It only reads synced state and only acts through real orders, on a fixed schedule (decisions every 300 ticks, a monthly
	// review every 2,400 ticks), scanning blocks and cells in fixed order, so a replay re-derives the same decisions
	// (in replays it runs "dry": it logs but the orders come from the recording).
	//
	// City layout: street grid with 7-cell pitch. Block (k, j) has its interior at u = 7+7k..12+7k, v = 7j+1..7j+6 relative to the
	// anchor; the main avenue is the row v = 0 (bottom edge of j = -1, top edge of j = 0).
	public partial class CityAutoTest
	{
		const int Pitch = 7;
		const int KCount = 12;
		const int JMin = -7;
		const int JCount = 14;

		enum BlockState : byte { None, Zoned, Service, Failed, Clearing }

		readonly BlockState[,] blocks = new BlockState[KCount, JCount];
		readonly ZoneType[,] blockZone = new ZoneType[KCount, JCount];

		bool mayorInit;
		bool mayorDry;
		CityManager cm;
		IDemandModel demandModel;
		Progression progression;
		IPropertyRegistry properties;
		RoadLayer roadLayer;
		ZoneLayer zoneLayer;
		IUtilityNetwork utilities;
		CityCoverageLayer coverage;
		CityEconomy economy;
		TransitLayer transit;
		ICityProblems problems;
		int mayorOrders;
		int lastLoanTick = -100000;
		bool lineBuilt;
		bool busDepotPlaced;
		bool serviceBlockWanted;
		CPos serviceBlockTarget;
		readonly HashSet<CPos> claimed = [];
		readonly Dictionary<string, int> placedCount = [];

		static CPos BlockCell(CPos anchor, int k, int j, int du, int dv) => anchor + new CVec(Pitch * (k + 1) + du, Pitch * j + 1 + dv);

		// ---- schedule ----
		void MayorTick(World w, Player p)
		{
			var tick = w.WorldTick;
			if (p == null || tick < 10)
				return;

			if (!mayorInit)
			{
				mayorDry = w.IsReplay;
				MayorInit(w, p);
				if (cm == null)
					return;
			}

			if (tick % 300 != 10)
				return;

			if (tick % 2400 == 10)
				MayorMonthly(w, p);

			MayorDecide(w, p);
		}

		void MayorInit(World w, Player p)
		{
			mayorInit = true;
			cm = p.PlayerActor.TraitOrDefault<CityManager>();
			demandModel = p.PlayerActor.TraitsImplementing<IDemandModel>().FirstOrDefault();
			progression = p.PlayerActor.TraitOrDefault<Progression>();
			economy = p.PlayerActor.TraitOrDefault<CityEconomy>();
			properties = w.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			roadLayer = w.WorldActor.TraitOrDefault<RoadLayer>();
			zoneLayer = w.WorldActor.TraitOrDefault<ZoneLayer>();
			utilities = w.WorldActor.TraitsImplementing<IUtilityNetwork>().FirstOrDefault();
			coverage = w.WorldActor.TraitOrDefault<CityCoverageLayer>();
			transit = w.WorldActor.TraitOrDefault<TransitLayer>();
			problems = w.WorldActor.TraitsImplementing<ICityProblems>().FirstOrDefault();
			if (cm == null || roadLayer == null || zoneLayer == null)
			{
				MayorLog(w, "disabled: missing city traits");
				cm = null;
				return;
			}

			// Opening: main avenue, then a few blocks of low-density R/C/I and a service block with power and water.
			Issue(w, CityOrders.BuildRoadOrder(p, At(0, 0), At(Pitch - 1, 0)));
			StartBlock(w, p, 0, -1, BlockState.Zoned, ZoneType.ResidentialLow);
			StartBlock(w, p, 0, 0, BlockState.Service, ZoneType.None);
			StartBlock(w, p, 1, -1, BlockState.Zoned, ZoneType.ResidentialLow);
			StartBlock(w, p, 1, 0, BlockState.Zoned, ZoneType.CommercialLow);
			StartBlock(w, p, 2, 0, BlockState.Zoned, ZoneType.Industrial);
			StartBlock(w, p, 2, -1, BlockState.Zoned, ZoneType.ResidentialLow);
		}

		// ---- helpers: logging and orders ----
		static void MayorLog(World w, string text)
		{
			Report(w, "report mayor " + text);
		}

		void Issue(World w, Order order)
		{
			mayorOrders++;
			if (!mayorDry)
				w.IssueOrder(order);
		}

		static int Ji(int j) => j - JMin;

		// ---- blocks ----
		static bool BlockInGrid(int k, int j) => k >= 0 && k < KCount && j >= JMin && j < JMin + JCount;

		bool IsBuilt(int k, int j) => BlockInGrid(k, j) && blocks[k, Ji(j)] is BlockState.Zoned or BlockState.Service;

		bool CanStartBlock(World w, int k, int j)
		{
			if (!BlockInGrid(k, j) || blocks[k, Ji(j)] != BlockState.None)
				return false;

			// Must connect to what exists (or the avenue start) and lie in owned, in-map land.
			var connected = (k == 0 && (j == 0 || j == -1)) || IsBuilt(k - 1, j) || IsBuilt(k + 1, j) || IsBuilt(k, j - 1) || IsBuilt(k, j + 1);
			if (!connected)
				return false;

			var tl = At(Pitch * (k + 1) - 1 + 0, Pitch * j);
			var br = At(Pitch * (k + 2), Pitch * (j + 1));
			if (!w.Map.Contains(tl) || !w.Map.Contains(br))
				return false;

			return progression == null || (progression.IsCellOwned(tl) && progression.IsCellOwned(br)
				&& progression.IsCellOwned(new CPos(tl.X, br.Y)) && progression.IsCellOwned(new CPos(br.X, tl.Y)));
		}

		int BlockRoadCost(World w, int k, int j, out bool blocked)
		{
			blocked = false;
			var cost = 0;
			foreach (var (a, b) in BlockRoads(k, j))
			{
				var plan = ConstructionUtils.PlanRoad(w, roadLayer, cm, a, b);
				cost += plan.Cost;
				if (plan.StopIndex < plan.Path.Count)
					blocked = true;
			}

			return cost;
		}

		IEnumerable<(CPos From, CPos To)> BlockRoads(int k, int j)
		{
			var u0 = Pitch * (k + 1) - 1;
			var u1 = u0 + Pitch;
			var v0 = Pitch * j;
			var v1 = v0 + Pitch;
			yield return (At(u0, v0), At(u1, v0));
			yield return (At(u0, v1), At(u1, v1));
			yield return (At(u0, v0), At(u0, v1));
			yield return (At(u1, v0), At(u1, v1));
		}

		bool StartBlock(World w, Player p, int k, int j, BlockState state, ZoneType zone)
		{
			var cost = BlockRoadCost(w, k, j, out var blocked);
			if (blocked)
			{
				blocks[k, Ji(j)] = BlockState.Failed;
				MayorLog(w, $"block {k},{j} blocked, skipped");
				return false;
			}

			if (Avail < cost)
				return false;

			committed += cost;
			foreach (var (a, b) in BlockRoads(k, j))
				Issue(w, CityOrders.BuildRoadOrder(p, a, b));

			blocks[k, Ji(j)] = state;
			blockZone[k, Ji(j)] = zone;
			if (zone != ZoneType.None)
				Issue(w, CityOrders.ZoneOrder(p, BlockCell(anchor, k, j, 0, 0), BlockCell(anchor, k, j, Pitch - 2, Pitch - 2), zone));

			MayorLog(w, $"block {k},{j} {(state == BlockState.Service ? "service" : zone.ToString())} roads=${cost}");
			return true;
		}

		// Zoned, empty, buildable cells of a zone type (what growth can still use).
		int FreeZonedCells(World w, ZoneType zone)
		{
			var n = 0;
			for (var k = 0; k < KCount; k++)
			{
				for (var j = JMin; j < JMin + JCount; j++)
				{
					if (blocks[k, Ji(j)] != BlockState.Zoned || blockZone[k, Ji(j)] != zone)
						continue;

					for (var dv = 0; dv < Pitch - 1; dv++)
						for (var du = 0; du < Pitch - 1; du++)
						{
							var c = BlockCell(anchor, k, j, du, dv);
							if (zoneLayer.GetZone(c) == zone && CellEmpty(w, c) && HasRoadNeighbour(c))
								n++;
						}
				}
			}

			return n;
		}

		bool HasRoadNeighbour(CPos c)
		{
			foreach (var d in CityUtils.Neighbours4)
				if (roadLayer.IsRoad(c + d))
					return true;

			return false;
		}

		static bool CellEmpty(World w, CPos c)
		{
			foreach (var a in w.ActorMap.GetActorsAt(c))
			{
				var b = a.Info.TraitInfoOrDefault<BulldozableInfo>();
				if (b == null || !b.AutoClear)
					return false;
			}

			return true;
		}

		// Best connectable block: nearest to `target` (cells from the anchor), with an optional bias per zone.
		bool PickBlock(World w, CPos target, ZoneType zone, out int bk, out int bj)
		{
			bk = bj = 0;
			var best = int.MaxValue;
			var found = false;
			for (var k = 0; k < KCount; k++)
			{
				for (var j = JMin; j < JMin + JCount; j++)
				{
					if (!CanStartBlock(w, k, j))
						continue;

					var centre = At(Pitch * k + 10, Pitch * j + 4);
					var d = Math.Abs(centre.X - target.X) + Math.Abs(centre.Y - target.Y);

					// Industry goes east (away from homes), dense uses frontage on the avenue.
					if (zone == ZoneType.Industrial)
						d -= 12 * k;
					else if (zone is ZoneType.ResidentialHigh or ZoneType.CommercialHigh or ZoneType.Office)
						d += (j is 0 or -1) ? 0 : 6;
					else if (zone is ZoneType.ResidentialLow)
						d += (j is 0 or -1) ? 5 : 0;

					if (d < best)
					{
						best = d;
						bk = k;
						bj = j;
						found = true;
					}
				}
			}

			return found;
		}
	}
}
