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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Result of planning a road drag. Shared by the order handler and the preview so that they always agree.</summary>
	public sealed class RoadPlan
	{
		/// <summary>Every cell of the L-shaped drag path.</summary>
		public readonly List<CPos> Path;

		/// <summary>Cells that will actually get a new road (the affordable, unobstructed prefix of the path, without existing roads).</summary>
		public readonly List<CPos> Build = [];

		/// <summary>Every planned cell of every carriageway with its action (new, replace, keep, blocked). The preview and the order use this.</summary>
		public List<RoadPlanEntry> Entries = [];

		/// <summary>Cells of the second carriageway when the plan is paired, else null.</summary>
		public List<CPos> PartnerPath;

		public RoadToolOptions Options;

		/// <summary>Resolved road type (never null for plans made by <see cref="RoadPlanner"/>).</summary>
		public RoadTypeData Type;

		public bool Paired;

		/// <summary>Index into <see cref="Path"/> of the first cell that is not built because it is blocked or unaffordable. Path.Count if the whole path is fine.</summary>
		public int StopIndex;

		/// <summary>Total price: road cells plus clearing trees.</summary>
		public int Cost;

		/// <summary>Fluent key explaining why the path stops early, or null.</summary>
		public string ErrorKey;

		public RoadPlan(List<CPos> path)
		{
			Path = path;
			StopIndex = path.Count;
		}
	}

	/// <summary>What a bulldoze drag removes. Shared by the order handler and the preview.</summary>
	public sealed class BulldozePlan
	{
		/// <summary>Road cells that will be removed (highway cells are excluded).</summary>
		public readonly List<CPos> RoadCells = [];

		/// <summary>Roundabout island cells in the rectangle (they are cleared together with the ring).</summary>
		public readonly List<CPos> IslandCells = [];

		/// <summary>Bulldozable actors that are inside or touch the rectangle, sorted by ActorID.</summary>
		public readonly List<Actor> Actors = [];

		/// <summary>Sum of Bulldozable.Cost (clearing trees etc.).</summary>
		public int Cost;

		/// <summary>Sum of refunds (RefundPercent of CityPlaceable.Cost).</summary>
		public int Refund;

		public bool IsEmpty => RoadCells.Count == 0 && Actors.Count == 0 && IslandCells.Count == 0;
	}

	/// <summary>Result of checking whether a CityPlaceable actor can be put at a location.</summary>
	public sealed class PlacementCheck
	{
		/// <summary>Footprint cells.</summary>
		public readonly List<CPos> Tiles = [];

		/// <summary>AutoClear actors (trees) that have to be removed first.</summary>
		public readonly List<Actor> Clear = [];

		/// <summary>Cost of clearing.</summary>
		public int ClearCost;

		/// <summary>Fluent key of the problem, or null if the placement is valid (money and unlock state excluded).</summary>
		public string ErrorKey;

		public bool Valid => ErrorKey == null;
	}

	/// <summary>Shared rules for roads, bulldozing and placement. Used by ConstructionTools and the order generators.</summary>
	public static class ConstructionUtils
	{
		// Fluent keys (rules-construction.ftl).
		[FluentReference]
		public const string ErrorMoney = "notification-city-not-enough-money";
		[FluentReference]
		public const string ErrorBlocked = "notification-city-blocked";
		[FluentReference]
		public const string ErrorTerrain = "notification-city-bad-terrain";
		[FluentReference]
		public const string ErrorOutOfBounds = "notification-city-out-of-bounds";
		[FluentReference]
		public const string ErrorRoadOnFootprint = "notification-city-road-in-the-way";
		[FluentReference]
		public const string ErrorNeedsRoad = "notification-city-needs-road";
		[FluentReference]
		public const string ErrorNeedsTerrain = "notification-city-needs-terrain";
		[FluentReference]
		public const string ErrorLocked = "notification-city-locked";
		[FluentReference]
		public const string ErrorNotPlaceable = "notification-city-not-placeable";
		[FluentReference]
		public const string ErrorWouldIsolate = "notification-city-would-isolate";
		[FluentReference]
		public const string ErrorNotOwned = "notification-city-not-owned";
		[FluentReference]
		public const string ErrorBridgeSpan = "notification-city-bridge-span";
		[FluentReference]
		public const string ErrorBridgeEnds = "notification-city-bridge-ends";
		[FluentReference]
		public const string ErrorAddonNotAllowed = "notification-city-addon-not-allowed";
		[FluentReference]
		public const string ErrorRoadLocked = "notification-city-road-locked";
		[FluentReference]
		public const string ErrorNeedsHighway = "notification-city-needs-highway";
		[FluentReference]
		public const string ErrorNeedsJunction = "notification-city-needs-junction";
		[FluentReference]
		public const string ErrorNeedsPowerLine = "notification-city-needs-power-line";
		[FluentReference]
		public const string ErrorNothingToBulldoze = "notification-city-nothing-to-bulldoze";

		// Sound notifications in the "Sounds" notification set (added by the UI/sound work package; skipped if undefined).
		public const string SoundBuild = "CityBuild";
		public const string SoundBulldoze = "CityBulldoze";
		public const string SoundRoad = "CityRoad";
		public const string SoundError = "CityError";

		public static BulldozableInfo BulldozableOf(Actor a)
		{
			return a.Info.TraitInfoOrDefault<BulldozableInfo>();
		}

		public static bool IsAutoClear(Actor a)
		{
			return a.Info.TraitInfoOrDefault<BulldozableInfo>()?.AutoClear == true;
		}

		/// <summary>True if an actor that is neither auto-clearable nor a vehicle occupies the cell.</summary>
		public static bool HasBlockingActor(World world, CPos cell)
		{
			foreach (var a in world.ActorMap.GetActorsAt(cell))
			{
				if (a.IsDead || !a.IsInWorld)
					continue;

				if (IsAutoClear(a))
					continue;

				if (a.Info.HasTraitInfo<BuildingInfo>() || a.Info.HasTraitInfo<BulldozableInfo>())
					return true;
			}

			return false;
		}

		public static void CollectAutoClearActors(World world, CPos cell, List<Actor> into)
		{
			foreach (var a in world.ActorMap.GetActorsAt(cell))
				if (!a.IsDead && a.IsInWorld && IsAutoClear(a) && !into.Contains(a))
					into.Add(a);
		}

		/// <summary>Price of clearing the AutoClear actors on a cell.</summary>
		public static int AutoClearCost(World world, CPos cell)
		{
			var cost = 0;
			foreach (var a in world.ActorMap.GetActorsAt(cell))
				if (!a.IsDead && a.IsInWorld && IsAutoClear(a))
					cost += BulldozableOf(a).Cost;

			return cost;
		}

		/// <summary>Disposes the AutoClear actors on a cell (at the end of the frame). Returns what they cost to clear.</summary>
		public static int ClearAutoClearActors(World world, CPos cell)
		{
			var actors = new List<Actor>();
			CollectAutoClearActors(world, cell, actors);
			return DisposeActors(world, actors);
		}

		/// <summary>Disposes actors at the end of the frame. Returns the sum of their Bulldozable.Cost.</summary>
		public static int DisposeActors(World world, List<Actor> actors)
		{
			var cost = 0;
			foreach (var a in actors)
				cost += BulldozableOf(a)?.Cost ?? 0;

			if (actors.Count > 0)
			{
				world.AddFrameEndTask(_ =>
				{
					foreach (var a in actors)
						if (!a.Disposed)
							a.Dispose();
				});
			}

			return cost;
		}

		/// <summary>Plans a plain street drag (legacy signature, keeps existing roads).</summary>
		public static RoadPlan PlanRoad(World world, RoadLayer roads, CityManager cm, CPos from, CPos to, Func<CPos, bool> isPending = null)
		{
			return RoadPlanner.Plan(world, roads, cm, from, to, RoadToolOptions.Default, isPending);
		}

		/// <summary>Plans a road drag with a type and modes (see <see cref="RoadPlanner"/>). Preview and order handler both call this.</summary>
		public static RoadPlan PlanRoad(World world, RoadLayer roads, CityManager cm, CPos from, CPos to, RoadToolOptions options, Func<CPos, bool> isPending = null)
		{
			return RoadPlanner.Plan(world, roads, cm, from, to, options, isPending);
		}

		/// <summary>Plans a bulldoze drag over the rectangle spanned by two cells.</summary>
		/// <summary>Clamps a cell into the playable map bounds (orders may carry arbitrary coordinates).</summary>
		public static CPos ClampToBounds(World world, CPos cell)
		{
			var bounds = world.Map.Bounds;
			var tl = new MPos(bounds.Left, bounds.Top).ToCPos(world.Map);
			var br = new MPos(bounds.Right - 1, bounds.Bottom - 1).ToCPos(world.Map);
			return new CPos(Math.Clamp(cell.X, tl.X, br.X), Math.Clamp(cell.Y, tl.Y, br.Y));
		}

		public static BulldozePlan PlanBulldoze(World world, RoadLayer roads, CPos from, CPos to)
		{
			var plan = new BulldozePlan();
			from = ClampToBounds(world, from);
			to = ClampToBounds(world, to);
			foreach (var c in CityUtils.Rect(from, to))
			{
				if (!world.Map.Contains(c))
					continue;

				if (roads != null && roads.IsRoad(c) && !roads.IsHighway(c))
				{
					plan.RoadCells.Add(c);
					plan.Refund += roads.RefundFor(c);
				}

				if (roads != null && roads.IsIsland(c))
					plan.IslandCells.Add(c);

				foreach (var a in world.ActorMap.GetActorsAt(c))
					if (!a.IsDead && a.IsInWorld && BulldozableOf(a) != null && !plan.Actors.Contains(a))
						plan.Actors.Add(a);
			}

			plan.Actors.Sort((a, b) => a.ActorID.CompareTo(b.ActorID));
			foreach (var a in plan.Actors)
			{
				var bulldozable = BulldozableOf(a);
				plan.Cost += bulldozable.Cost;
				var placeable = a.Info.TraitInfoOrDefault<CityPlaceableInfo>();
				if (placeable != null)
					plan.Refund += placeable.Cost * bulldozable.RefundPercent / 100;
			}

			return plan;
		}

		/// <summary>
		/// Checks the terrain, footprint and road requirements of placing a CityPlaceable actor.
		/// Does not check money or unlock state. Road requirement: the footprint must touch (4-adjacent) any road cell,
		/// connected to the outside or not.
		/// </summary>
		public static PlacementCheck CheckPlacement(World world, ActorInfo ai, CPos topLeft, RoadLayer roads, Func<CPos, bool> isPending = null)
		{
			var result = new PlacementCheck();
			var bi = ai.TraitInfoOrDefault<BuildingInfo>();
			var placeable = ai.TraitInfoOrDefault<CityPlaceableInfo>();
			if (bi == null || placeable == null)
			{
				result.ErrorKey = ErrorNotPlaceable;
				return result;
			}

			var map = world.Map;
			var utilities = world.WorldActor.TraitOrDefault<UtilityNetwork>();
			result.Tiles.AddRange(bi.Tiles(topLeft));

			foreach (var t in result.Tiles)
			{
				if (!map.Contains(t))
				{
					result.ErrorKey = ErrorOutOfBounds;
					return result;
				}
			}

			foreach (var t in result.Tiles)
			{
				if (roads != null && !roads.IsCellOwned(t))
				{
					result.ErrorKey = ErrorNotOwned;
					return result;
				}

				if (roads != null && (roads.IsRoad(t) || roads.IsIsland(t)))
				{
					result.ErrorKey = ErrorRoadOnFootprint;
					return result;
				}

				if (utilities != null && utilities.HasPowerLine(t))
				{
					result.ErrorKey = ErrorBlocked;
					return result;
				}

				if (bi.TerrainTypes.Count > 0 && !bi.TerrainTypes.Contains(map.GetTerrainInfo(t).Type))
				{
					result.ErrorKey = ErrorTerrain;
					return result;
				}

				if (HasBlockingActor(world, t) || (isPending != null && isPending(t)))
				{
					result.ErrorKey = ErrorBlocked;
					return result;
				}

				CollectAutoClearActors(world, t, result.Clear);
			}

			foreach (var a in result.Clear)
				result.ClearCost += BulldozableOf(a).Cost;

			if (placeable.RequiresRoad)
			{
				var found = false;
				foreach (var t in result.Tiles)
				{
					foreach (var d in CityUtils.Neighbours4)
					{
						var n = t + d;
						if (roads != null && !result.Tiles.Contains(n) && roads.GivesAccess(n))
						{
							found = true;
							break;
						}
					}

					if (found)
						break;
				}

				if (!found)
				{
					result.ErrorKey = ErrorNeedsRoad;
					return result;
				}
			}

			if (placeable.RequiresTerrainNearby.Count > 0 && !HasTerrainNearby(world, result.Tiles, placeable.RequiresTerrainNearby, placeable.NearbyRange))
				result.ErrorKey = ErrorNeedsTerrain;

			return result;
		}

		static bool HasTerrainNearby(World world, List<CPos> tiles, HashSet<string> types, int range)
		{
			var map = world.Map;
			foreach (var t in tiles)
			{
				for (var dy = -range; dy <= range; dy++)
				{
					for (var dx = -range; dx <= range; dx++)
					{
						var c = t + new CVec(dx, dy);
						if (map.Contains(c) && types.Contains(map.GetTerrainInfo(c).Type))
							return true;
					}
				}
			}

			return false;
		}

		/// <summary>Cursor name to use: the preferred one if the mod defines it, otherwise the fallback.</summary>
		public static string Cursor(string preferred, string fallback)
		{
			return Game.ModData.Cursors.ContainsKey(preferred) ? preferred : fallback;
		}

		/// <summary>Shows a transient message line (fluent key) and plays the error sound, for the local player only.</summary>
		public static void NotifyError(World world, Player player, string fluentKey)
		{
			if (player != world.LocalPlayer)
				return;

			TextNotificationsManager.AddTransientLine(player, fluentKey);
			PlaySound(world, player, SoundError);
		}

		/// <summary>Plays a UI notification from the "Sounds" set for the local player, if the mod defines it.</summary>
		public static void PlaySound(World world, Player player, string notification)
		{
			if (player != world.LocalPlayer || Game.Sound == null)
				return;

			var rules = world.Map.Rules;
			if (rules.Notifications == null || !rules.Notifications.TryGetValue("sounds", out var sounds))
				return;

			if (!sounds.NotificationsPools.Value.ContainsKey(notification))
				return;

			Game.Sound.PlayNotification(rules, player, "Sounds", notification, null);
		}
	}
}
