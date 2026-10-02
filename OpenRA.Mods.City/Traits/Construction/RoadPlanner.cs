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
	public enum RoadPlanAction : byte
	{
		/// <summary>A new road cell.</summary>
		New,

		/// <summary>An existing road is replaced in place (type, direction or paired flag changes).</summary>
		Replace,

		/// <summary>An identical road is already there (or a permanent cell): nothing happens.</summary>
		Keep,

		/// <summary>Cannot be built (terrain, building, money, would isolate a building).</summary>
		Blocked,
	}

	/// <summary>One cell of a road plan. The order handler applies exactly these entries, the preview draws exactly these.</summary>
	public struct RoadPlanEntry
	{
		public CPos Cell;
		public RoadPlanAction Action;

		/// <summary>One-way direction (index into CityUtils.Neighbours4) or -1.</summary>
		public int OneWay;

		/// <summary>Side-block mask to set after building (median towards the partner carriageway).</summary>
		public int SideBlock;

		public bool Paired;

		/// <summary>Bridge span over water (deck axis east-west when BridgeEw).</summary>
		public bool Bridge;
		public bool BridgeEw;

		/// <summary>Price of this cell (net of the replace refund).</summary>
		public int Cost;
	}

	/// <summary>
	/// Plans a road drag with type and modes. Single source of truth for the preview and the order handler.
	/// The first carriageway follows the drag path in the drag direction; a paired road adds a second carriageway one cell
	/// to the left (right-hand traffic) flowing the other way, with a median (side blocks) between them.
	/// </summary>
	public static class RoadPlanner
	{
		/// <summary>Corner of an L path (start when straight) and the headings of its legs.</summary>
		static (CPos Start, CPos Corner, CPos End) Waypoints(List<CPos> path)
		{
			var start = path[0];
			var end = path[^1];
			var corner = end;
			for (var i = 1; i + 1 < path.Count; i++)
			{
				var a = path[i] - path[i - 1];
				var b = path[i + 1] - path[i];
				if (a != b)
				{
					corner = path[i];
					break;
				}
			}

			return (start, corner, end);
		}

		static CVec Heading(CPos a, CPos b)
		{
			return new CVec(Math.Sign(b.X - a.X), Math.Sign(b.Y - a.Y));
		}

		static CVec Left(CVec h) { return new CVec(h.Y, -h.X); }

		/// <summary>Cells of the opposite carriageway, in the drag order of the first carriageway (flow runs backwards).</summary>
		public static List<CPos> PartnerPath(List<CPos> path)
		{
			var (start, corner, end) = Waypoints(path);
			var h1 = start == corner ? Heading(corner, end) : Heading(start, corner);
			if (h1 == CVec.Zero)
				h1 = new CVec(1, 0);

			var h2 = corner == end ? h1 : Heading(corner, end);
			var l1 = Left(h1);
			var l2 = Left(h2);
			var sB = start + l1;
			var cB = corner + l1 + l2;
			var eB = end + l2;
			var result = new List<CPos>();
			if (corner == end)
			{
				// straight: same length
				for (var i = 0; i < path.Count; i++)
					result.Add(path[i] + l1);

				return result;
			}

			var c = sB;
			result.Add(c);
			while (c != cB)
			{
				c += new CVec(Math.Sign(cB.X - c.X), Math.Sign(cB.Y - c.Y));
				result.Add(c);
			}

			while (c != eB)
			{
				c += new CVec(Math.Sign(eB.X - c.X), Math.Sign(eB.Y - c.Y));
				result.Add(c);
			}

			return result;
		}

		static int DirIndex(CVec v)
		{
			for (var i = 0; i < 4; i++)
				if (CityUtils.Neighbours4[i] == v)
					return i;

			return -1;
		}

		/// <summary>One-way direction of each cell of a path: towards the next cell (or the previous one when reversed).</summary>
		static int[] Flow(List<CPos> path, bool reverse)
		{
			var n = path.Count;
			var d = new int[n];
			for (var i = 0; i < n; i++)
			{
				CVec v;
				if (!reverse)
					v = i + 1 < n ? path[i + 1] - path[i] : (n > 1 ? path[i] - path[i - 1] : new CVec(1, 0));
				else
					v = i > 0 ? path[i - 1] - path[i] : (n > 1 ? path[0] - path[1] : new CVec(-1, 0));

				var di = DirIndex(v);
				d[i] = di < 0 ? 1 : di;
			}

			return d;
		}

		public static RoadPlan Plan(World world, RoadLayer roads, CityManager cm, CPos from, CPos to, RoadToolOptions options, Func<CPos, bool> isPending = null)
		{
			var plan = new RoadPlan(CityUtils.RoadPath(from, to)) { Options = options };
			var type = roads.GetTypeData(options.TypeId) ?? roads.GetTypeData(roads.DefaultTypeId);
			plan.Type = type;
			var paired = (options.Paired || type.Info.AlwaysPaired) && type.Info.Paired;
			plan.Paired = paired;
			var oneWayMode = options.OneWay || paired;
			int? funds = cm == null || cm.UnlimitedMoney ? null : cm.Funds;
			var spent = 0;

			var flowA = Flow(plan.Path, options.Reverse);
			var entries = new List<RoadPlanEntry>();
			var statuses = new Dictionary<CPos, int>();

			Carriageway(world, roads, plan, type, oneWayMode, paired, plan.Path, flowA, options.Replace, funds, ref spent, isPending,
				entries, statuses, true, options.Bridge);

			if (paired)
			{
				var partner = PartnerPath(plan.Path);
				plan.PartnerPath = partner;

				// the partner flows the other way: reverse of the first carriageway's flow
				var flowB = Flow(partner, !options.Reverse);
				Carriageway(world, roads, plan, type, true, true, partner, flowB, options.Replace, funds, ref spent, isPending, entries, statuses, false, options.Bridge);
				ApplyMedian(entries);
			}

			plan.Entries = entries;
			plan.Cost = spent;
			foreach (var e in entries)
				if (e.Action == RoadPlanAction.New)
					plan.Build.Add(e.Cell);

			return plan;
		}

		static void Carriageway(World world, RoadLayer roads, RoadPlan plan, RoadTypeData type, bool oneWayMode, bool paired,
			List<CPos> path, int[] flow, bool replace, int? funds, ref int spent, Func<CPos, bool> isPending,
			List<RoadPlanEntry> entries, Dictionary<CPos, int> seen, bool first, bool allowBridge)
		{
			var bridgeAxis = AnalyzeBridges(roads, path, allowBridge && type.Info.AllowBridge, out var bridgeError);
			var stop = -1;
			for (var i = 0; i < path.Count; i++)
			{
				var c = path[i];
				var entry = new RoadPlanEntry { Cell = c, OneWay = oneWayMode ? flow[i] : -1, Paired = paired, Action = RoadPlanAction.New };
				if (!type.Info.AllowOneWay)
					entry.OneWay = -1;

				if (bridgeAxis[i] >= 0)
				{
					entry.Bridge = true;
					entry.BridgeEw = bridgeAxis[i] == 1;
				}

				if (stop >= 0)
				{
					entry.Action = RoadPlanAction.Blocked;
					entries.Add(entry);
					continue;
				}

				if (seen.ContainsKey(c))
				{
					// the two carriageways of a tight turn can touch: the first claim wins
					entry.Action = RoadPlanAction.Keep;
					entries.Add(entry);
					continue;
				}

				string error = null;
				var cost = 0;
				if (roads.IsRoad(c))
				{
					var old = roads.GetRoadType(c);
					if (roads.IsHighway(c) || !replace)
						entry.Action = RoadPlanAction.Keep;
					else if (roads.IsBridge(c) && !type.Info.AllowBridge)
						error = ConstructionUtils.ErrorTerrain;
					else if (old.Id == type.Id && roads.GetOneWay(c) == entry.OneWay && roads.IsPaired(c) == paired)
						entry.Action = RoadPlanAction.Keep;
					else
					{
						entry.Action = RoadPlanAction.Replace;
						cost = Math.Max(0, type.Info.Cost - old.Info.Cost * roads.Info.RefundPercent / 100);
						if (!type.Info.GivesAccess && old.Info.GivesAccess && WouldIsolate(world, roads, c))
							error = ConstructionUtils.ErrorWouldIsolate;
					}
				}
				else if (!world.Map.Contains(c))
					error = ConstructionUtils.ErrorOutOfBounds;
				else if (!roads.IsCellOwned(c))
					error = ConstructionUtils.ErrorNotOwned;
				else if (bridgeError[i] != null)
					error = bridgeError[i];
				else if (entry.Bridge)
				{
					if (isPending != null && isPending(c))
						error = ConstructionUtils.ErrorBlocked;
					else
						cost = type.Info.Cost * roads.Info.BridgeCostMultiplier;
				}
				else if (!roads.IsBuildableTerrain(c))
					error = ConstructionUtils.ErrorTerrain;
				else if (!roads.CanBuildRoadAt(c) || (isPending != null && isPending(c)))
					error = ConstructionUtils.ErrorBlocked;
				else
					cost = type.Info.Cost + ConstructionUtils.AutoClearCost(world, c);

				if (error == null && funds.HasValue && (long)spent + cost > funds.Value)
					error = ConstructionUtils.ErrorMoney;

				if (error != null)
				{
					entry.Action = RoadPlanAction.Blocked;
					stop = i;
					plan.ErrorKey ??= error;

					if (first)
						plan.StopIndex = i;
				}
				else
				{
					spent += cost;
					entry.Cost = cost;
				}

				seen[c] = entries.Count;
				entries.Add(entry);
			}
		}

		// Finds the water runs of a path that can be bridged: straight, at most BridgeMaxSpan cells, land at both ends.
		// Returns per cell the deck axis (0 north-south, 1 east-west) or -1; bridgeError gets the fluent key for water cells that cannot be spanned.
		static int[] AnalyzeBridges(RoadLayer roads, List<CPos> path, bool allow, out string[] errors)
		{
			var n = path.Count;
			var axis = new int[n];
			errors = new string[n];
			var i = 0;
			while (i < n)
			{
				axis[i] = -1;
				if (!roads.IsBridgeTerrain(path[i]))
				{
					i++;
					continue;
				}

				var j = i;
				while (j < n && roads.IsBridgeTerrain(path[j]))
					j++;

				var len = j - i;
				string err = null;
				var ax = -1;
				if (!allow)
					err = ConstructionUtils.ErrorTerrain;
				else if (len > roads.Info.BridgeMaxSpan)
					err = ConstructionUtils.ErrorBridgeSpan;
				else if (i == 0 || j >= n)
					err = ConstructionUtils.ErrorBridgeEnds;
				else
				{
					var v = path[i] - path[i - 1];
					var straight = path[j] - path[j - 1] == v;
					for (var k = i + 1; k < j && straight; k++)
						straight = path[k] - path[k - 1] == v;

					if (!straight)
						err = ConstructionUtils.ErrorBridgeEnds;
					else
						ax = v.X != 0 ? 1 : 0;
				}

				for (var k = i; k < j; k++)
				{
					axis[k] = err == null ? ax : -1;
					errors[k] = err;
				}

				i = j;
			}

			return axis;
		}

		// Median: block the sides between cells of the two carriageways when both are being laid.
		static void ApplyMedian(List<RoadPlanEntry> entries)
		{
			var index = new Dictionary<CPos, int>();
			for (var i = 0; i < entries.Count; i++)
				if (entries[i].Action is RoadPlanAction.New or RoadPlanAction.Replace)
					index[entries[i].Cell] = i;

			// A cell belongs to carriageway A if it appears in the first half; but the rule only needs "opposite flow neighbours".
			for (var i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				if (e.Action is not (RoadPlanAction.New or RoadPlanAction.Replace) || e.OneWay < 0)
					continue;

				var mask = 0;
				for (var d = 0; d < 4; d++)
				{
					var n = e.Cell + CityUtils.Neighbours4[d];
					if (index.TryGetValue(n, out var j) && entries[j].OneWay == ((e.OneWay + 2) & 3) && d != e.OneWay && d != ((e.OneWay + 2) & 3))
						mask |= 1 << d;
				}

				e.SideBlock = mask;
				entries[i] = e;
			}
		}

		// True if a building next to the cell would be left without any other street (access road) after the replacement.
		static bool WouldIsolate(World world, RoadLayer roads, CPos cell)
		{
			foreach (var d in CityUtils.Neighbours4)
			{
				var n = cell + d;
				foreach (var a in world.ActorMap.GetActorsAt(n))
				{
					var cb = a.TraitOrDefault<CityBuilding>();
					if (cb == null || a.IsDead)
						continue;

					var other = false;
					foreach (var fc in cb.Cells)
					{
						foreach (var fd in CityUtils.Neighbours4)
						{
							var r = fc + fd;
							if (r != cell && roads.GivesAccess(r))
								other = true;
						}
					}

					if (!other)
						return true;
				}
			}

			return false;
		}
	}
}
