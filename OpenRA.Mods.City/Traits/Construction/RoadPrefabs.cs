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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Result of planning a road prefab (roundabout ring or highway ramp). Shared by order and preview.</summary>
	public sealed class RoadPrefabPlan
	{
		public string Prefab;
		public CPos Centre;

		/// <summary>Ring cells in flow order, or the single ramp cell.</summary>
		public readonly List<RoadPlanEntry> Entries = [];

		/// <summary>Cells that become the island (interior of a roundabout).</summary>
		public readonly List<CPos> Island = [];

		/// <summary>Existing road cells inside the island that are removed.</summary>
		public readonly List<CPos> RemoveRoads = [];

		public int Cost;
		public int Refund;
		public string ErrorKey;

		public bool Valid => ErrorKey == null && Entries.Count > 0;
	}

	public static class RoadPrefabs
	{
		public const int RoundaboutCost = 150;
		public const int LargeRoundaboutCost = 400;
		public const int RampCost = 100;

		/// <summary>Ring perimeter in counter-clockwise (screen) order, starting at the top-left corner.</summary>
		public static List<CPos> Ring(CPos centre, int r)
		{
			var cells = new List<CPos>();
			for (var y = -r; y <= r; y++)
				cells.Add(new CPos(centre.X - r, centre.Y + y));

			for (var x = -r + 1; x <= r; x++)
				cells.Add(new CPos(centre.X + x, centre.Y + r));

			for (var y = r - 1; y >= -r; y--)
				cells.Add(new CPos(centre.X + r, centre.Y + y));

			for (var x = r - 1; x > -r; x--)
				cells.Add(new CPos(centre.X + x, centre.Y - r));

			return cells;
		}

		public static RoadPrefabPlan Plan(World world, RoadLayer roads, CityManager cm, CPos centre, string prefab, Func<CPos, bool> isPending = null)
		{
			var plan = new RoadPrefabPlan { Prefab = prefab, Centre = centre };
			int? funds = cm == null || cm.UnlimitedMoney ? null : cm.Funds;
			if (prefab == NetworkOrders.PrefabRamp)
			{
				PlanRamp(roads, plan, funds);
				return plan;
			}

			int r;
			int baseCost;
			if (prefab == NetworkOrders.PrefabRoundabout)
			{
				r = 1;
				baseCost = RoundaboutCost;
			}
			else if (prefab == NetworkOrders.PrefabRoundaboutLarge)
			{
				r = 2;
				baseCost = LargeRoundaboutCost;
			}
			else
			{
				plan.ErrorKey = ConstructionUtils.ErrorNotPlaceable;
				return plan;
			}

			var map = world.Map;
			var ring = Ring(centre, r);
			var cost = baseCost;
			for (var i = 0; i < ring.Count; i++)
			{
				var c = ring[i];
				var next = ring[(i + 1) % ring.Count];
				var flow = Array.IndexOf(CityUtils.Neighbours4, next - c);
				var e = new RoadPlanEntry { Cell = c, OneWay = flow, Action = RoadPlanAction.New };
				if (!map.Contains(c))
					Fail(plan, ConstructionUtils.ErrorOutOfBounds);
				else if (roads.IsRoad(c))
				{
					if (roads.IsHighway(c) || roads.IsHighwayClass(c))
						Fail(plan, ConstructionUtils.ErrorRoadOnFootprint);

					e.Action = RoadPlanAction.Replace;
				}
				else if (!roads.IsCellOwned(c))
					Fail(plan, ConstructionUtils.ErrorNotOwned);
				else if (!roads.IsBuildableTerrain(c))
					Fail(plan, ConstructionUtils.ErrorTerrain);
				else if (!roads.CanBuildRoadAt(c) || (isPending != null && isPending(c)))
					Fail(plan, ConstructionUtils.ErrorBlocked);
				else
					cost += ConstructionUtils.AutoClearCost(world, c);

				plan.Entries.Add(e);
			}

			for (var dy = -r + 1; dy <= r - 1; dy++)
				for (var dx = -r + 1; dx <= r - 1; dx++)
				{
					var c = new CPos(centre.X + dx, centre.Y + dy);
					plan.Island.Add(c);
					if (!map.Contains(c))
						Fail(plan, ConstructionUtils.ErrorOutOfBounds);
					else if (roads.IsRoad(c))
					{
						if (roads.IsHighway(c) || roads.IsHighwayClass(c))
							Fail(plan, ConstructionUtils.ErrorRoadOnFootprint);

						plan.RemoveRoads.Add(c);
						plan.Refund += roads.RefundFor(c);
					}
					else if (roads.IsIsland(c))
					{
						// part of another island
					}
					else if (!roads.IsCellOwned(c))
						Fail(plan, ConstructionUtils.ErrorNotOwned);
					else if (!roads.IsBuildableTerrain(c))
						Fail(plan, ConstructionUtils.ErrorTerrain);
					else if (!roads.CanBuildRoadAt(c) || (isPending != null && isPending(c)))
						Fail(plan, ConstructionUtils.ErrorBlocked);
					else
						cost += ConstructionUtils.AutoClearCost(world, c);
				}

			plan.Cost = cost;
			if (plan.ErrorKey == null && funds.HasValue && cost - plan.Refund > funds.Value)
				plan.ErrorKey = ConstructionUtils.ErrorMoney;

			return plan;
		}

		static void Fail(RoadPrefabPlan plan, string key)
		{
			plan.ErrorKey ??= key;
		}

		static void PlanRamp(RoadLayer roads, RoadPrefabPlan plan, int? funds)
		{
			var c = plan.Centre;
			if (!roads.IsRoad(c) || !roads.IsHighwayClass(c))
			{
				plan.ErrorKey = ConstructionUtils.ErrorNeedsHighway;
				return;
			}

			plan.Entries.Add(new RoadPlanEntry { Cell = c, OneWay = roads.GetOneWay(c), Action = RoadPlanAction.Replace });

			// Removing a ramp is free.
			plan.Cost = roads.IsRamp(c) ? 0 : RampCost;
			if (funds.HasValue && plan.Cost > funds.Value)
				plan.ErrorKey = ConstructionUtils.ErrorMoney;
		}
	}
}
