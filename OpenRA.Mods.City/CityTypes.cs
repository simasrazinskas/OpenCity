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
using System.Globalization;
using OpenRA.Traits;

namespace OpenRA.Mods.City
{
	/// <summary>Zone painted on a cell. Byte values are part of the order protocol (CityOrders.Zone ExtraData).</summary>
	public enum ZoneType : byte
	{
		None = 0,
		ResidentialLow = 1,
		ResidentialHigh = 2,
		CommercialLow = 3,
		CommercialHigh = 4,
		Industrial = 5,
		Office = 6,

		// Appended for the CS2 work (design/04). Growth/art for these is owned by the zoning WP.
		ResidentialRow = 7,
		ResidentialMedium = 8,
		ResidentialMixed = 9,
		ResidentialLowRent = 10,
		OfficeHigh = 11,
		Warehouse = 12,
	}

	/// <summary>Demand / tax category. Several zone types share one category.</summary>
	public enum ZoneCategory : byte
	{
		None = 0,
		Residential = 1,
		Commercial = 2,
		Industrial = 3,
		Office = 4,
	}

	/// <summary>Coverage-based city services provided by ServiceBuilding actors.</summary>
	public enum CityService : byte
	{
		Police = 0,
		Fire = 1,
		Health = 2,
		Education = 3,
		Parks = 4,
	}

	/// <summary>Info view (map overlay) modes, selected locally by the UI. Not synced.</summary>
	public enum CityInfoView : byte
	{
		None = 0,
		Power,
		Water,
		LandValue,
		Pollution,
		Police,
		Fire,
		Health,
		Education,
		Parks,
		Traffic,
		Happiness,

		// Appended for the CS2 work (design/03, 06, 07, 09, 10). Rendering is owned by the UI WP.
		PowerGrid,
		WaterGrid,
		Sewage,
		Roads,
		Garbage,
		Deathcare,
		Crime,
		Telecom,
		Post,
		NaturalResources,
		Production,
		Freight,
		Noise,
		AirPollution,
		GroundPollution,
		Groundwater,
		BuildingLevel,
		Education2,
		Wealth,
		Age,
		Districts,
		Transit,
		Tourism,
	}

	/// <summary>Calendar date derived from the number of elapsed in-game days.</summary>
	public readonly struct CityDate
	{
		public readonly int Year;
		public readonly int Month;
		public readonly int Day;

		/// <summary>Length of a simulation month in days (Day ranges 1..DaysPerMonth). 1 = CityClock style (day = month).</summary>
		public readonly int DaysPerMonth;

		/// <summary>Clock time (CityClock), or -1 when the date has no time of day.</summary>
		public readonly int Hour;
		public readonly int Minute;

		public CityDate(int year, int month, int day, int daysPerMonth = 30, int hour = -1, int minute = 0)
		{
			Year = year;
			Month = month;
			Day = day;
			DaysPerMonth = Math.Max(1, daysPerMonth);
			Hour = hour;
			Minute = minute;
		}

		static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
		static readonly int[] DaysInMonth = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

		/// <summary>Simulation months have DaysPerMonth days; spread them over the real month length for display.</summary>
		public override string ToString()
		{
			var m = Math.Max(0, Month - 1) % 12;

			// CityClock dates: one day/night cycle is a whole month, so show month, year and clock time.
			if (Hour >= 0)
				return $"{MonthNames[m]} {Year}  {Hour:D2}:{Minute:D2}";

			var dpm = DaysPerMonth > 0 ? DaysPerMonth : 30;
			var day = Math.Min(DaysInMonth[m], 1 + Math.Max(0, Day - 1) * DaysInMonth[m] / dpm);
			return $"{day} {MonthNames[m]} {Year}";
		}
	}

	/// <summary>
	/// Implemented by actor traits that can switch a city building off (e.g. under construction, abandoned).
	/// CityBuilding treats the building as non-operational if any implementation returns false.
	/// </summary>
	public interface ICityBuildingState
	{
		bool IsOperational { get; }
	}

	/// <summary>
	/// Order strings and helpers. ALL city state changes go through these orders so that
	/// the simulation stays deterministic (and save games / replays work).
	/// Every order's Subject is the issuing player's PlayerActor.
	/// </summary>
	public static class CityOrders
	{
		/// <summary>Target = end cell, ExtraLocation = start cell. Builds an L-shaped road (see CityUtils.RoadPath). Handled by ConstructionTools.</summary>
		public const string BuildRoad = "CityBuildRoad";

		/// <summary>Target = end cell, ExtraLocation = start cell. Removes roads and Bulldozable actors in the rectangle. Handled by ConstructionTools.</summary>
		public const string Bulldoze = "CityBulldoze";

		/// <summary>Target = top-left footprint cell, TargetString = actor type. Handled by ConstructionTools.</summary>
		public const string PlaceBuilding = "CityPlaceBuilding";

		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData = (uint)ZoneType (0 = dezone). Handled by ZoningTool.</summary>
		public const string Zone = "CityZone";

		/// <summary>ExtraLocation.X = (int)ZoneCategory, ExtraData = tax percent. Handled by CityManager.</summary>
		public const string SetTax = "CitySetTax";

		/// <summary>ExtraData = speed index (1..3). Handled by CityManager.</summary>
		public const string SetSpeed = "CitySetSpeed";

		public static Order BuildRoadOrder(Player p, CPos from, CPos to) =>
			new(BuildRoad, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from };

		public static Order BulldozeOrder(Player p, CPos from, CPos to) =>
			new(Bulldoze, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from };

		public static Order PlaceBuildingOrder(Player p, string actorType, CPos topLeft) =>
			new(PlaceBuilding, p.PlayerActor, Target.FromCell(p.World, topLeft), false) { TargetString = actorType };

		public static Order ZoneOrder(Player p, CPos from, CPos to, ZoneType zone) =>
			new(Zone, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = (uint)zone };

		public static Order SetTaxOrder(Player p, ZoneCategory category, int percent) =>
			new(SetTax, p.PlayerActor, false) { ExtraLocation = new CPos((int)category, 0), ExtraData = (uint)percent };

		public static Order SetSpeedOrder(Player p, int speed) =>
			new(SetSpeed, p.PlayerActor, false) { ExtraData = (uint)speed };
	}

	public static class CityUtils
	{
		/// <summary>4-neighbourhood in a fixed order: N, E, S, W. Bit i of a road/shore mask refers to Neighbours4[i].</summary>
		public static readonly CVec[] Neighbours4 = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

		public static ZoneCategory Category(this ZoneType z)
		{
			switch (z)
			{
				case ZoneType.ResidentialLow:
				case ZoneType.ResidentialHigh:
				case ZoneType.ResidentialRow:
				case ZoneType.ResidentialMedium:
				case ZoneType.ResidentialMixed:
				case ZoneType.ResidentialLowRent:
					return ZoneCategory.Residential;
				case ZoneType.CommercialLow:
				case ZoneType.CommercialHigh:
					return ZoneCategory.Commercial;
				case ZoneType.Industrial:
				case ZoneType.Warehouse:
					return ZoneCategory.Industrial;
				case ZoneType.Office:
				case ZoneType.OfficeHigh:
					return ZoneCategory.Office;
				default:
					return ZoneCategory.None;
			}
		}

		/// <summary>Actor-name prefix for growables of this zone type, e.g. "res-low" -> res-low-1, res-low-2, res-low-3.</summary>
		public static string GrowablePrefix(this ZoneType z)
		{
			switch (z)
			{
				case ZoneType.ResidentialLow: return "res-low";
				case ZoneType.ResidentialHigh: return "res-high";
				case ZoneType.CommercialLow: return "com-low";
				case ZoneType.CommercialHigh: return "com-high";
				case ZoneType.Industrial: return "ind";
				case ZoneType.Office: return "off";
				case ZoneType.ResidentialRow: return "res-row";
				case ZoneType.ResidentialMedium: return "res-med";
				case ZoneType.ResidentialMixed: return "res-mixed";
				case ZoneType.ResidentialLowRent: return "res-lowrent";
				case ZoneType.OfficeHigh: return "off-high";
				case ZoneType.Warehouse: return "warehouse";
				default: return null;
			}
		}

		/// <summary>All cells in the axis-aligned rectangle spanned by a and b (inclusive), row-major.</summary>
		public static IEnumerable<CPos> Rect(CPos a, CPos b)
		{
			var x0 = Math.Min(a.X, b.X);
			var x1 = Math.Max(a.X, b.X);
			var y0 = Math.Min(a.Y, b.Y);
			var y1 = Math.Max(a.Y, b.Y);
			for (var y = y0; y <= y1; y++)
				for (var x = x0; x <= x1; x++)
					yield return new CPos(x, y);
		}

		/// <summary>
		/// The cells a road drag from `from` to `to` covers: along the dominant axis first, then the other axis
		/// (an L shape). Used by both the road tool preview and the BuildRoad order handler, so they always agree.
		/// </summary>
		public static List<CPos> RoadPath(CPos from, CPos to)
		{
			var cells = new List<CPos>();
			var dx = Math.Sign(to.X - from.X);
			var dy = Math.Sign(to.Y - from.Y);
			var horizontalFirst = Math.Abs(to.X - from.X) >= Math.Abs(to.Y - from.Y);
			var c = from;
			cells.Add(c);
			if (horizontalFirst)
			{
				while (c.X != to.X) { c = new CPos(c.X + dx, c.Y); cells.Add(c); }
				while (c.Y != to.Y) { c = new CPos(c.X, c.Y + dy); cells.Add(c); }
			}
			else
			{
				while (c.Y != to.Y) { c = new CPos(c.X, c.Y + dy); cells.Add(c); }
				while (c.X != to.X) { c = new CPos(c.X + dx, c.Y); cells.Add(c); }
			}

			return cells;
		}

		/// <summary>Format money for display, e.g. 12500 -> "$12,500", -300 -> "-$300".</summary>
		public static string FormatMoney(long amount)
		{
			return amount < 0 ? $"-${-amount:N0}" : $"${amount:N0}";
		}

		/// <summary>Short money text for tight HUD slots: full digits below <paramref name="fullBelow"/>, then 12.3k / 4.56M / 1.2B.</summary>
		public static string FormatMoneyCompact(long amount, long fullBelow = 1000000, bool dollar = true)
		{
			var sign = amount < 0 ? "-" : "";
			var a = Math.Abs(amount);
			var d = dollar ? "$" : "";
			if (a < fullBelow)
				return $"{sign}{d}{a:N0}";

			var c = CultureInfo.InvariantCulture;
			if (a < 1000000)
				return $"{sign}{d}{(a / 1000.0).ToString(a < 100000 ? "0.0" : "0", c)}k";

			if (a < 1000000000)
				return $"{sign}{d}{(a / 1000000.0).ToString(a < 10000000 ? "0.00" : a < 100000000 ? "0.0" : "0", c)}M";

			return $"{sign}{d}{(a / 1000000000.0).ToString("0.0", c)}B";
		}
	}
}
