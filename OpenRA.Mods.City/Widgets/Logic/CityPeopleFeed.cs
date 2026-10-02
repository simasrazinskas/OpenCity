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
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public enum ChirpCategory { Citizens, Services, Alerts }

	/// <summary>Shared helpers of the PEOPLE windows (chirper, notifications): classification, icons, ages, window fitting.</summary>
	public static class CityPeopleFeed
	{
		/// <summary>The filter category of a chirp: citizen life events, city services and milestones, or alerts (problems, disasters, weather).</summary>
		public static ChirpCategory CategoryOf(ChirpEntry entry)
		{
			var key = entry.Key ?? "";
			if (entry.CitizenId != 0 || key.StartsWith("chirp-citizen-", StringComparison.Ordinal))
				return ChirpCategory.Citizens;

			if (key.Contains("problem") || key.Contains("fire") || key.Contains("blackout") || key.Contains("shortage") || key.Contains("flood")
				|| key.Contains("storm") || key.Contains("lightning") || key.Contains("crime") || key.Contains("broke") || key.Contains("blocked")
				|| key.Contains("snow") || key.Contains("heat") || key.Contains("cold") || key.Contains("dying") || key.Contains("depleted")
				|| key.Contains("full"))
				return ChirpCategory.Alerts;

			return ChirpCategory.Services;
		}

		/// <summary>The RCT2 icon of a chirp and the colour ramp of its avatar box.</summary>
		public static (string Icon, string Ramp) ChirpLook(ChirpEntry entry)
		{
			var key = entry.Key ?? "";
			bool Has(string part) => key.Contains(part);

			if (Has("citizen-born")) return ("stat_citizen", "purple");
			if (Has("citizen-graduated")) return ("cat_education", "purple");
			if (Has("citizen-died")) return ("cat_deathcare", "purple");
			if (Has("citizen-arrested")) return ("cat_police", "purple");
			if (Has("citizen")) return ("stat_citizen", "purple");
			if (Has("wildfire")) return ("st_wildfire", "red");
			if (Has("fire")) return ("st_fire", "red");
			if (Has("nopower") || Has("blackout") || Has("power-shortage")) return ("st_no_power", "blue");
			if (Has("nowater") || Has("water-shortage")) return ("st_no_water", "blue");
			if (Has("noroad")) return ("st_no_road", "orange");
			if (Has("abandoned")) return ("st_abandoned", "orange");
			if (Has("crime")) return ("st_crime", "red");
			if (Has("garbage") || Has("landfill")) return ("st_garbage", "orange");
			if (Has("traffic") || Has("freight")) return ("st_traffic", "orange");
			if (Has("flood")) return ("st_flooded", "teal");
			if (Has("lightning") || Has("storm")) return ("wx_storm", "teal");
			if (Has("snow")) return ("wx_snow", "teal");
			if (Has("cold")) return ("wx_cold", "teal");
			if (Has("heat")) return ("wx_heat", "teal");
			if (Has("milestone")) return ("pnl_milestone", "orange");
			if (Has("population")) return ("stat_population", "yellow");
			if (Has("prg-")) return ("pnl_progression", "yellow");
			if (Has("industry")) return ("cat_industry", "brown");
			if (Has("broke")) return ("stat_expenses", "red");
			if (Has("dying")) return ("nat_forest", "green");
			return ("pnl_chirper", "blue");
		}

		/// <summary>The severity tier a chirp is shown with in the notification centre.</summary>
		public static ProblemTier ChirpTier(ChirpEntry entry)
		{
			switch (CategoryOf(entry))
			{
				case ChirpCategory.Alerts:
					return entry.Likes >= 20 ? ProblemTier.Major : ProblemTier.Warning;
				case ChirpCategory.Citizens:
					return ProblemTier.Info;
				default:
					return (entry.Key ?? "").Contains("milestone") || (entry.Key ?? "").Contains("prg-") ? ProblemTier.Good : ProblemTier.Minimal;
			}
		}

		/// <summary>The severity stripe colour of a tier (the colours of the status icon tiles).</summary>
		public static Color TierColor(ProblemTier tier)
		{
			return tier switch
			{
				ProblemTier.Minimal => Color.FromArgb(0x9A, 0xA0, 0xA6),
				ProblemTier.Info => Color.FromArgb(0x6B, 0xB8, 0xF0),
				ProblemTier.Problem => Color.FromArgb(0xF2, 0xD0, 0x4C),
				ProblemTier.Warning => Color.FromArgb(0xF2, 0x8C, 0x30),
				ProblemTier.Major => Color.FromArgb(0xE5, 0x4B, 0x3C),
				ProblemTier.Error => Color.FromArgb(0xA8, 0x20, 0x20),
				ProblemTier.Fatal => Color.FromArgb(0x30, 0x10, 0x10),
				_ => Color.FromArgb(0x5C, 0xD6, 0x7A),
			};
		}

		/// <summary>Severity as a number for the "this bad or worse" filter (Good counts as Info).</summary>
		public static int Rank(ProblemTier tier)
		{
			return tier switch
			{
				ProblemTier.Minimal => 0,
				ProblemTier.Info or ProblemTier.Good => 1,
				ProblemTier.Problem => 2,
				ProblemTier.Warning => 3,
				_ => 4,
			};
		}

		public static CityClock Clock(World world)
		{
			return world.WorldActor.TraitOrDefault<CityClock>();
		}

		/// <summary>How long ago a tick was, in game time: "now", "5 min", "3 h", "2 d".</summary>
		public static string Age(World world, int tick)
		{
			var clock = Clock(world);
			var ticks = Math.Max(0, world.WorldTick - tick);
			var perHour = clock?.TicksPerHour ?? 100;
			var perDay = clock?.TicksPerDay ?? 2400;
			var minutes = ticks * 60 / perHour;
			if (minutes < 1)
				return FluentProvider.GetMessage(CityChirperLogic.AgeNow);

			if (minutes < 60)
				return FluentProvider.GetMessage(CityChirperLogic.AgeMinutes, "count", minutes);

			if (ticks < perDay)
				return FluentProvider.GetMessage(CityChirperLogic.AgeHours, "count", ticks / perHour);

			return FluentProvider.GetMessage(CityChirperLogic.AgeDays, "count", ticks / perDay);
		}

		/// <summary>The district name at a cell, or an empty string.</summary>
		public static string PlaceName(CityUiContext ctx, CPos cell)
		{
			if (cell == CPos.Zero || ctx.Progression == null)
				return "";

			var id = ctx.Progression.GetDistrict(cell);
			if (id == 0)
				return "";

			var districts = ctx.ProgressionUi?.Districts;
			if (districts != null)
				foreach (var d in districts)
					if (d.Id == id)
						return string.IsNullOrEmpty(d.Name) ? "#" + id : d.Name;

			return "";
		}

		/// <summary>
		/// Sizes a window to its design height, but never taller than the screen area allows (the window never resizes itself
		/// otherwise). Returns true when the height changed, so the caller can lay out its lists again.
		/// </summary>
		public static bool FitHeight(Widget panel, int designHeight, int minHeight)
		{
			var max = panel is CityPanelWidget cp ? cp.MaxHeight : designHeight;
			var height = Math.Max(minHeight, Math.Min(designHeight, max));
			if (panel.Bounds.Height == height)
				return false;

			panel.Bounds.Height = height;
			return true;
		}
	}
}
