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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Resolves CityOrders.Zone orders (zoning is free) and the CityCondemn order.")]
	public class ZoningToolInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new ZoningTool(init.Self, this); }
	}

	public class ZoningTool : IResolveOrder
	{
		public readonly ZoningToolInfo Info;

		public ZoningTool(Actor self, ZoningToolInfo info)
		{
			Info = info;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == ZoningOrders.Condemn)
			{
				ResolveCondemn(order);
				return;
			}

			if (order.OrderString != CityOrders.Zone)
				return;

			var world = self.World;
			var zoneLayer = world.WorldActor.TraitOrDefault<ZoneLayer>();
			if (zoneLayer == null)
				return;

			if (order.ExtraData > (uint)ZoningOrders.LastZone || order.Target.Type == TargetType.Invalid)
				return;

			var zone = (ZoneType)order.ExtraData;
			var growth = world.WorldActor.TraitOrDefault<ZoneGrowth>();
			if (zone != ZoneType.None && growth != null && !growth.IsZoneUnlocked(zone))
				return;

			var end = world.Map.CellContaining(order.Target.CenterPosition);
			var start = order.ExtraLocation;

			// Clamp to the playable area so absurd rectangles cannot stall the simulation.
			var bounds = world.Map.Bounds;
			var topLeft = new MPos(bounds.Left, bounds.Top).ToCPos(world.Map);
			var bottomRight = new MPos(bounds.Right - 1, bounds.Bottom - 1).ToCPos(world.Map);
			start = Clamp(start, topLeft, bottomRight);
			end = Clamp(end, topLeft, bottomRight);

			foreach (var cell in CityUtils.Rect(start, end))
			{
				if (zone == ZoneType.None || zoneLayer.CanZone(cell))
					zoneLayer.SetZone(cell, zone);
			}
		}

		static void ResolveCondemn(Order order)
		{
			// Only abandoned buildings can be condemned: they collapse into rubble at once (the rubble can be bulldozed for free).
			var target = order.Target;
			if (target.Type != TargetType.Actor || !target.IsValidFor(order.Subject))
				return;

			target.Actor.TraitOrDefault<GrowableBuilding>()?.Condemn();
		}

		static CPos Clamp(CPos c, CPos min, CPos max)
		{
			return new CPos(Math.Clamp(c.X, Math.Min(min.X, max.X), Math.Max(min.X, max.X)), Math.Clamp(c.Y, Math.Min(min.Y, max.Y), Math.Max(min.Y, max.Y)));
		}
	}
}
