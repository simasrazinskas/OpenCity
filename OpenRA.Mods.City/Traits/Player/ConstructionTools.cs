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

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Resolves CityOrders.BuildRoad, Bulldoze and PlaceBuilding orders (cost checks via CityManager).")]
	public class ConstructionToolsInfo : TraitInfo
	{
		// These fluent keys are used through ConstructionUtils; declaring them here lets the fluent linter see they are in use.
		[FluentReference]
		public readonly string NotEnoughMoneyNotification = ConstructionUtils.ErrorMoney;

		[FluentReference]
		public readonly string BlockedNotification = ConstructionUtils.ErrorBlocked;

		[FluentReference]
		public readonly string BadTerrainNotification = ConstructionUtils.ErrorTerrain;

		[FluentReference]
		public readonly string OutOfBoundsNotification = ConstructionUtils.ErrorOutOfBounds;

		[FluentReference]
		public readonly string RoadInTheWayNotification = ConstructionUtils.ErrorRoadOnFootprint;

		[FluentReference]
		public readonly string NeedsRoadNotification = ConstructionUtils.ErrorNeedsRoad;

		[FluentReference]
		public readonly string NeedsTerrainNotification = ConstructionUtils.ErrorNeedsTerrain;

		[FluentReference]
		public readonly string LockedNotification = ConstructionUtils.ErrorLocked;

		[FluentReference]
		public readonly string NotPlaceableNotification = ConstructionUtils.ErrorNotPlaceable;

		[FluentReference]
		public readonly string NothingToBulldozeNotification = ConstructionUtils.ErrorNothingToBulldoze;

		[FluentReference]
		public readonly string WouldIsolateNotification = ConstructionUtils.ErrorWouldIsolate;

		[FluentReference]
		public readonly string BridgeSpanNotification = ConstructionUtils.ErrorBridgeSpan;

		[FluentReference]
		public readonly string BridgeEndsNotification = ConstructionUtils.ErrorBridgeEnds;

		[FluentReference]
		public readonly string AddonNotAllowedNotification = ConstructionUtils.ErrorAddonNotAllowed;

		[FluentReference]
		public readonly string NotOwnedNotification = ConstructionUtils.ErrorNotOwned;

		[FluentReference]
		public readonly string RoadLockedNotification = ConstructionUtils.ErrorRoadLocked;

		[FluentReference]
		public readonly string NeedsHighwayNotification = ConstructionUtils.ErrorNeedsHighway;

		[FluentReference]
		public readonly string NeedsJunctionNotification = ConstructionUtils.ErrorNeedsJunction;

		[FluentReference]
		public readonly string NeedsPowerLineNotification = ConstructionUtils.ErrorNeedsPowerLine;

		public override object Create(ActorInitializer init) { return new ConstructionTools(init.Self, this); }
	}

	public class ConstructionTools : IResolveOrder
	{
		public readonly ConstructionToolsInfo Info;

		readonly Actor self;

		// Cells promised to buildings whose actors are only created at the end of the frame.
		// Stops two orders resolved in the same tick from placing overlapping things.
		readonly HashSet<CPos> pendingCells = [];

		public ConstructionTools(Actor self, ConstructionToolsInfo info)
		{
			this.self = self;
			Info = info;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case CityOrders.BuildRoad:
					BuildRoad(order);
					break;
				case CityOrders.Bulldoze:
					Bulldoze(order);
					break;
				case NetworkOrders.SetRoadControl:
					SetRoadControl(order);
					break;
				case NetworkOrders.PlaceRoadPrefab:
					PlaceRoadPrefab(order);
					break;
				case NetworkOrders.RoadAddon:
					RoadAddon(order);
					break;
				case CityOrders.PlaceBuilding:
					PlaceBuilding(order);
					break;
			}
		}

		bool Pay(int amount)
		{
			if (amount <= 0)
				return true;

			var cm = self.TraitOrDefault<CityManager>();
			return cm == null || cm.TrySpend(amount, "construction");
		}

		void Refund(int amount)
		{
			if (amount <= 0)
				return;

			self.TraitOrDefault<CityManager>()?.AddFunds(amount, "refund");
		}

		void BuildRoad(Order order)
		{
			var world = self.World;
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			if (roads == null || order.Target.Type == TargetType.Invalid)
				return;

			var from = order.ExtraLocation;
			var to = world.Map.CellContaining(order.Target.CenterPosition);
			var options = RoadToolOptions.FromExtraData(order.ExtraData);
			var type = roads.GetTypeData(options.TypeId) ?? roads.GetTypeData(roads.DefaultTypeId);
			if (!roads.IsTypeUnlocked(self.Owner, type))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorRoadLocked);
				return;
			}

			if (options.Bridge && roads.Progression != null && !roads.Progression.IsUnlocked("road:bridge"))
				options.Bridge = false;

			var plan = ConstructionUtils.PlanRoad(world, roads, self.TraitOrDefault<CityManager>(), from, to, options, pendingCells.Contains);

			var changes = 0;
			foreach (var e in plan.Entries)
				if (e.Action is RoadPlanAction.New or RoadPlanAction.Replace)
					changes++;

			if (changes == 0)
			{
				if (plan.ErrorKey != null)
					ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey);

				return;
			}

			if (!Pay(plan.Cost))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			var zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			foreach (var e in plan.Entries)
			{
				if (e.Action == RoadPlanAction.New)
				{
					ConstructionUtils.ClearAutoClearActors(world, e.Cell);
					zones?.SetZone(e.Cell, ZoneType.None);
					if (e.Bridge)
						roads.AddBridge(e.Cell, plan.Type.Id, e.OneWay, e.Paired, e.BridgeEw);
					else
						roads.AddRoad(e.Cell, plan.Type.Id, e.OneWay, e.Paired);
				}
				else if (e.Action == RoadPlanAction.Replace)
					roads.ReplaceRoad(e.Cell, plan.Type.Id, e.OneWay, e.Paired);
			}

			// Medians last, when both carriageways exist.
			foreach (var e in plan.Entries)
				if (e.Action is RoadPlanAction.New or RoadPlanAction.Replace)
					roads.SetSideBlock(e.Cell, e.SideBlock);

			if (plan.ErrorKey != null)
				ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey);
			else
				ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundRoad);
		}

		void RoadAddon(Order order)
		{
			var world = self.World;
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			if (roads == null || order.Target.Type == TargetType.Invalid)
				return;

			var flag = (RoadAddons)(order.ExtraData & 0xff);
			var remove = (order.ExtraData & 256) != 0;
			var data = roads.GetAddonData(flag);
			if (data == null)
				return;

			if (!remove && roads.Progression != null && !roads.Progression.IsUnlocked("road-addon:" + data.Name))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorRoadLocked);
				return;
			}

			var from = order.ExtraLocation;
			var to = world.Map.CellContaining(order.Target.CenterPosition);
			var plan = RoadAddonPlanner.Plan(world, roads, self.TraitOrDefault<CityManager>(), from, to, flag, remove);
			if (plan.Cells.Count == 0)
			{
				ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey ?? ConstructionUtils.ErrorNothingToBulldoze);
				return;
			}

			if (plan.Cost > 0 && !Pay(plan.Cost))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			if (plan.Cost < 0)
				Refund(-plan.Cost);

			foreach (var c in plan.Cells)
				roads.SetAddon(c, flag, !remove);

			ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundRoad);
		}

		void SetRoadControl(Order order)
		{
			var world = self.World;
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			if (roads == null || order.Target.Type == TargetType.Invalid)
				return;

			var cell = world.Map.CellContaining(order.Target.CenterPosition);
			if (!roads.IsRoad(cell))
				return;

			JunctionControl? value = order.ExtraData == 0 ? null : (JunctionControl)(order.ExtraData - 1);
			if (value != null && (int)value.Value > (int)JunctionControl.Roundabout)
				return;

			if (value != null && value != JunctionControl.None && !roads.IsJunction(cell))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorNeedsJunction);
				return;
			}

			roads.SetControl(cell, value);
		}

		void PlaceRoadPrefab(Order order)
		{
			var world = self.World;
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			if (roads == null || order.Target.Type == TargetType.Invalid || string.IsNullOrEmpty(order.TargetString))
				return;

			var centre = world.Map.CellContaining(order.Target.CenterPosition);
			var plan = RoadPrefabs.Plan(world, roads, self.TraitOrDefault<CityManager>(), centre, order.TargetString, pendingCells.Contains);
			if (!plan.Valid)
			{
				ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey ?? ConstructionUtils.ErrorNotPlaceable);
				return;
			}

			var net = plan.Cost - plan.Refund;
			if (net > 0 && !Pay(net))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			if (net < 0)
				Refund(-net);

			if (plan.Prefab == NetworkOrders.PrefabRamp)
			{
				roads.SetRamp(centre, !roads.IsRamp(centre));
				ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundRoad);
				return;
			}

			var zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			foreach (var c in plan.RemoveRoads)
				roads.RemoveRoad(c);

			foreach (var c in plan.Island)
			{
				ConstructionUtils.ClearAutoClearActors(world, c);
				zones?.SetZone(c, ZoneType.None);
				roads.SetIsland(c, true);
			}

			foreach (var e in plan.Entries)
			{
				if (e.Action == RoadPlanAction.New)
				{
					ConstructionUtils.ClearAutoClearActors(world, e.Cell);
					zones?.SetZone(e.Cell, ZoneType.None);
					roads.AddRoad(e.Cell, roads.DefaultTypeId, e.OneWay);
				}
				else
					roads.ReplaceRoad(e.Cell, roads.DefaultTypeId, e.OneWay, false);

				roads.SetSideBlock(e.Cell, 0);
				roads.SetControl(e.Cell, JunctionControl.Roundabout);
			}

			ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundRoad);
		}

		void Bulldoze(Order order)
		{
			var world = self.World;
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			if (order.Target.Type == TargetType.Invalid)
				return;

			var from = order.ExtraLocation;
			var to = world.Map.CellContaining(order.Target.CenterPosition);

			var plan = ConstructionUtils.PlanBulldoze(world, roads, from, to);
			if (plan.IsEmpty)
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorNothingToBulldoze);
				return;
			}

			// Settle the net amount, matching the preview (refunds can pay for clearing costs).
			var net = plan.Refund - plan.Cost;
			if (net < 0 && !Pay(-net))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			if (net > 0)
				Refund(net);

			foreach (var c in plan.RoadCells)
				roads.RemoveRoad(c);

			foreach (var c in plan.IslandCells)
				roads.SetIsland(c, false);

			ConstructionUtils.DisposeActors(world, plan.Actors);
			ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundBulldoze);
		}

		void PlaceBuilding(Order order)
		{
			var world = self.World;
			if (order.Target.Type == TargetType.Invalid || string.IsNullOrEmpty(order.TargetString))
				return;

			if (!world.Map.Rules.Actors.TryGetValue(order.TargetString, out var ai))
				return;

			var placeable = ai.TraitInfoOrDefault<CityPlaceableInfo>();
			if (placeable == null)
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorNotPlaceable);
				return;
			}

			var cm = self.TraitOrDefault<CityManager>();
			if (cm != null && !cm.IsUnlocked(ai.Name))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorLocked);
				return;
			}

			var topLeft = world.Map.CellContaining(order.Target.CenterPosition);
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			var check = ConstructionUtils.CheckPlacement(world, ai, topLeft, roads, pendingCells.Contains);
			if (!check.Valid)
			{
				ConstructionUtils.NotifyError(world, self.Owner, check.ErrorKey);
				return;
			}

			if (!Pay(placeable.Cost + check.ClearCost))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			var zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			foreach (var t in check.Tiles)
			{
				zones?.SetZone(t, ZoneType.None);
				pendingCells.Add(t);
			}

			ConstructionUtils.DisposeActors(world, check.Clear);

			var owner = self.Owner;
			world.AddFrameEndTask(w =>
			{
				foreach (var t in check.Tiles)
					pendingCells.Remove(t);

				w.CreateActor(ai.Name, [
					new LocationInit(topLeft),
					new OwnerInit(owner),
				]);
			});

			ConstructionUtils.PlaySound(world, owner, ConstructionUtils.SoundBuild);
		}
	}
}
