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
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Order strings and factories of the public transport WP. All are issued by the player's PlayerActor and resolved by TransitTools.
	/// Payloads:
	///   TransitPlaceStop    Target = road cell, TargetString = mode ("bus" | "taxi"), ExtraData = 0 (automatic) or side + 1 (side 0..3 = N, E, S, W).
	///   TransitRemoveStop   ExtraData = stop id.
	///   TransitCreateLine   TargetString = "mode;loop(0/1);1,5,9;name;vehicles", ExtraData = colour: palette index 0..11 or 24-bit RGB.
	///   TransitEditLine     ExtraData = line id, TargetString = "loop(0/1);1,5,9".
	///   TransitSetLine      ExtraData = line id, TargetString = "price=150;vehicles=4;auto=1;color=3;name=Foo" (any subset; price is percent of the default fare).
	///   TransitDeleteLine   ExtraData = line id.
	///   TransitBuildTrack   Target = end cell, ExtraLocation = start cell, TargetString = "tram" (existing road cells) or "rail", ExtraData = 0 build / 1 remove (L-shaped drag).
	/// </summary>
	public static class TransitOrders
	{
		public const string PlaceStop = "TransitPlaceStop";
		public const string RemoveStop = "TransitRemoveStop";
		public const string CreateLine = "TransitCreateLine";
		public const string EditLine = "TransitEditLine";
		public const string SetLine = "TransitSetLine";
		public const string DeleteLine = "TransitDeleteLine";
		public const string BuildTrack = "TransitBuildTrack";

		public static Order PlaceStopOrder(Player p, CPos cell, TransitMode mode, int side = -1) =>
			new(PlaceStop, p.PlayerActor, Target.FromCell(p.World, cell), false)
			{
				TargetString = TransitLayer.ModeName(mode),
				ExtraData = side < 0 || side > 3 ? 0 : (uint)side + 1,
			};

		public static Order RemoveStopOrder(Player p, int stopId) =>
			new(RemoveStop, p.PlayerActor, false) { ExtraData = (uint)stopId };

		public static Order CreateLineOrder(Player p, TransitMode mode, bool loop, IEnumerable<int> stopIds, int color, string name = "", int vehicles = 1) =>
			new(CreateLine, p.PlayerActor, false)
			{
				TargetString = $"{TransitLayer.ModeName(mode)};{(loop ? 1 : 0)};{JoinIds(stopIds)};{name};{vehicles}",
				ExtraData = (uint)color,
			};

		public static Order EditLineOrder(Player p, int lineId, bool loop, IEnumerable<int> stopIds) =>
			new(EditLine, p.PlayerActor, false) { ExtraData = (uint)lineId, TargetString = $"{(loop ? 1 : 0)};{JoinIds(stopIds)}" };

		/// <summary>Pass a negative number (or null name) to leave a value unchanged.</summary>
		public static Order SetLineOrder(Player p, int lineId, int ticketPercent = -1, int vehicles = -1, int auto = -1, int color = -1, string name = null)
		{
			var sb = new StringBuilder();
			Append(sb, "price", ticketPercent);
			Append(sb, "vehicles", vehicles);
			Append(sb, "auto", auto);
			Append(sb, "color", color);
			if (!string.IsNullOrWhiteSpace(name))
			{
				if (sb.Length > 0)
					sb.Append(';');

				sb.Append("name=").Append(name.Replace(';', ' ').Replace('=', ' '));
			}

			return new Order(SetLine, p.PlayerActor, false) { ExtraData = (uint)lineId, TargetString = sb.ToString() };
		}

		/// <summary>Drag tram track over roads, or rail over free land (level crossings over straight roads). Set `remove` to take track away again.</summary>
		public static Order BuildTrackOrder(Player p, string kind, CPos from, CPos to, bool remove = false) =>
			new(BuildTrack, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, TargetString = kind, ExtraData = remove ? 1u : 0u };

		public static Order DeleteLineOrder(Player p, int lineId) =>
			new(DeleteLine, p.PlayerActor, false) { ExtraData = (uint)lineId };

		static void Append(StringBuilder sb, string key, int value)
		{
			if (value < 0)
				return;

			if (sb.Length > 0)
				sb.Append(';');

			sb.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
		}

		static string JoinIds(IEnumerable<int> ids)
		{
			var sb = new StringBuilder();
			foreach (var id in ids)
			{
				if (sb.Length > 0)
					sb.Append(',');

				sb.Append(id.ToString(CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}
	}
}
