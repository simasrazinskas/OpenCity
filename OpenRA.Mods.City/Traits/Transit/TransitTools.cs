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
using System.Globalization;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Resolves the Transit* orders (stops, lines) against the world's TransitLayer, paying through CityManager.")]
	public class TransitToolsInfo : TraitInfo
	{
		// Declared so the fluent linter sees the keys as used.
		[FluentReference]
		public readonly string NoMoneyNotification = TransitLayer.ErrorMoney;

		[FluentReference]
		public readonly string NotRoadNotification = TransitLayer.ErrorNotRoad;

		[FluentReference]
		public readonly string StopExistsNotification = TransitLayer.ErrorStopExists;

		[FluentReference]
		public readonly string NoDepotNotification = TransitLayer.ErrorNoDepot;

		[FluentReference]
		public readonly string BadLineNotification = TransitLayer.ErrorBadLine;

		[FluentReference]
		public readonly string NoStopNotification = TransitLayer.ErrorNoStop;

		[FluentReference]
		public readonly string NotSupportedNotification = TransitLayer.ErrorNotSupported;

		[FluentReference]
		public readonly string NotOwnedNotification = TransitLayer.ErrorNotOwned;

		[FluentReference]
		public readonly string NeedsTrackNotification = TransitLayer.ErrorNeedsTrack;

		[FluentReference]
		public readonly string TrackNeedsRoadNotification = TransitLayer.ErrorTrackNeedsRoad;

		[FluentReference]
		public readonly string RailBlockedNotification = RailLayer.ErrorBlocked;

		[FluentReference]
		public readonly string RailCrossingNotification = RailLayer.ErrorCrossing;

		[FluentReference]
		public readonly string RailTerrainNotification = RailLayer.ErrorTerrain;

		[FluentReference]
		public readonly string LockedNotification = TransitLayer.ErrorLocked;

		[FluentReference]
		public readonly string LineCutNotification = TransitLayer.NotifyLineCut;

		public override object Create(ActorInitializer init) { return new TransitTools(init.Self, this); }
	}

	public class TransitTools : IResolveOrder
	{
		public readonly TransitToolsInfo Info;
		readonly Actor self;

		public TransitTools(Actor self, TransitToolsInfo info)
		{
			this.self = self;
			Info = info;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (!order.OrderString.StartsWith("Transit", System.StringComparison.Ordinal))
				return;

			var world = self.World;
			var layer = world.WorldActor.TraitOrDefault<TransitLayer>();
			if (layer == null)
				return;

			var cm = self.TraitOrDefault<CityManager>();
			string error = null;
			switch (order.OrderString)
			{
				case TransitOrders.PlaceStop:
				{
					if (order.Target.Type == TargetType.Invalid || !TransitLayer.TryParseMode(order.TargetString, out var mode))
						return;

					var cell = world.Map.CellContaining(order.Target.CenterPosition);
					error = layer.PlaceStop(cell, mode, order.ExtraData == 0 ? -1 : (int)order.ExtraData - 1, cm);
					break;
				}

				case TransitOrders.RemoveStop:
					error = layer.RemoveStop((int)order.ExtraData, cm);
					break;

				case TransitOrders.CreateLine:
				{
					var parts = (order.TargetString ?? "").Split(';');
					if (parts.Length < 3 || !TransitLayer.TryParseMode(parts[0], out var mode))
						return;

					var vehicles = 1;
					if (parts.Length > 4)
						int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out vehicles);

					error = layer.CreateLine(mode, parts[1] == "1", ParseIds(parts[2]), (int)order.ExtraData, parts.Length > 3 ? parts[3] : "", vehicles, cm, out _);
					break;
				}

				case TransitOrders.EditLine:
				{
					var parts = (order.TargetString ?? "").Split(';');
					if (parts.Length < 2)
						return;

					error = layer.EditLine((int)order.ExtraData, parts[0] == "1", ParseIds(parts[1]), cm);
					break;
				}

				case TransitOrders.SetLine:
					error = ApplySetLine(layer, order);
					break;

				case TransitOrders.BuildTrack:
				{
					if (order.Target.Type == TargetType.Invalid)
						return;

					var end = world.Map.CellContaining(order.Target.CenterPosition);
					error = layer.BuildTrack(order.TargetString, order.ExtraLocation, end, order.ExtraData != 0, cm);
					break;
				}

				case TransitOrders.DeleteLine:
					layer.DeleteLine((int)order.ExtraData);
					break;
			}

			if (error != null)
				Notify(world, error);
		}

		static string ApplySetLine(TransitLayer layer, Order order)
		{
			int price = -1, vehicles = -1, auto = -1, color = -1;
			string name = null;
			foreach (var kv in (order.TargetString ?? "").Split(';', System.StringSplitOptions.RemoveEmptyEntries))
			{
				var eq = kv.IndexOf('=');
				if (eq <= 0)
					continue;

				var key = kv[..eq];
				var value = kv[(eq + 1)..];
				switch (key)
				{
					case "price": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out price); break;
					case "vehicles": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out vehicles); break;
					case "auto": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out auto); break;
					case "color": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out color); break;
					case "name": name = value; break;
				}
			}

			return layer.SetLine((int)order.ExtraData, price, vehicles, auto, color, name);
		}

		static List<int> ParseIds(string s)
		{
			var ids = new List<int>();
			foreach (var part in (s ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries))
				if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
					ids.Add(id);

			return ids;
		}

		void Notify(World world, string key)
		{
			if (self.Owner == world.LocalPlayer)
				TextNotificationsManager.AddTransientLine(self.Owner, key);
		}
	}
}
