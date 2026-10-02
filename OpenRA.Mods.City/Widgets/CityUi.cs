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

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Small helpers shared by the OpenCity widgets and chrome logic.</summary>
	public static class CityUi
	{
		// Text colours for the light RCT2 window bodies (uistyle.yaml Ink: MoneyPositive / MoneyNegative, ramp shades).
		public static readonly Color Good = Color.FromArgb(0x0E, 0x5A, 0x16);
		public static readonly Color Bad = Color.FromArgb(0xA0, 0x14, 0x10);
		public static readonly Color Warn = Color.FromArgb(0x8A, 0x42, 0x0A);
		public static readonly Color Muted = Color.FromArgb(0x5C, 0x3A, 0x1C);
		public static readonly Color Accent = Color.FromArgb(0xB4, 0x5C, 0x12);

		/// <summary>Muted text over the world (annotations): stays light on the map.</summary>
		public static readonly Color WorldMuted = Color.FromArgb(0xB4, 0xC4, 0xD0);

		static readonly Color HeatRed = Color.FromArgb(0xE5, 0x4B, 0x3C);
		static readonly Color HeatYellow = Color.FromArgb(0xF2, 0xD0, 0x4C);
		static readonly Color HeatGreen = Color.FromArgb(0x3F, 0xC9, 0x5A);

		/// <summary>The local player's CityManager, or null (observers, shellmap, missing trait).</summary>
		public static CityManager GetManager(World world)
		{
			return world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
		}

		public static Color CategoryColor(ZoneCategory category)
		{
			switch (category)
			{
				case ZoneCategory.Residential: return Color.FromArgb(0x7E, 0xD9, 0x57);
				case ZoneCategory.Commercial: return Color.FromArgb(0x5A, 0xB4, 0xF0);
				case ZoneCategory.Industrial: return Color.FromArgb(0xF2, 0xC9, 0x4C);
				case ZoneCategory.Office: return Color.FromArgb(0xB0, 0x7C, 0xE8);
				default: return Color.White;
			}
		}

		static Color Lerp(Color a, Color b, float t)
		{
			return Color.FromArgb(
				(int)(a.A + (b.A - a.A) * t),
				(int)(a.R + (b.R - a.R) * t),
				(int)(a.G + (b.G - a.G) * t),
				(int)(a.B + (b.B - a.B) * t));
		}

		/// <summary>Heat-map colour for a 0 (bad) .. 1 (good) value: red, yellow, green.</summary>
		public static Color HeatColor(float t)
		{
			t = t < 0 ? 0 : t > 1 ? 1 : t;
			return t < 0.5f ? Lerp(HeatRed, HeatYellow, t * 2) : Lerp(HeatYellow, HeatGreen, (t - 0.5f) * 2);
		}

		/// <summary>Localized display name of a zone type.</summary>
		public static string ZoneName(ZoneType zone)
		{
			var suffix = zone switch
			{
				ZoneType.ResidentialLow => "res-low",
				ZoneType.ResidentialHigh => "res-high",
				ZoneType.CommercialLow => "com-low",
				ZoneType.CommercialHigh => "com-high",
				ZoneType.Industrial => "ind",
				ZoneType.Office => "off",
				ZoneType.ResidentialRow => "res-row",
				ZoneType.ResidentialMedium => "res-med",
				ZoneType.ResidentialMixed => "res-mixed",
				ZoneType.ResidentialLowRent => "res-lowrent",
				ZoneType.OfficeHigh => "off-high",
				ZoneType.Warehouse => "warehouse",
				_ => "dezone"
			};

			return FluentProvider.GetMessage("label-zone-" + suffix);
		}

		public static string SignedMoney(long amount)
		{
			return amount >= 0 ? "+" + CityUtils.FormatMoney(amount) : CityUtils.FormatMoney(amount);
		}

		/// <summary>
		/// Wraps a label's text source so the text is shortened with an ellipsis when it does not fit the label's
		/// current width (re-measured only when the text or the width changes).
		/// </summary>
		public static Func<string> Fitted(OpenRA.Mods.Common.Widgets.LabelWidget label, Func<string> text)
		{
			string lastText = null;
			var lastWidth = -1;
			var fitted = "";
			return () =>
			{
				var current = text() ?? "";
				if (current != lastText || label.Bounds.Width != lastWidth)
				{
					lastText = current;
					lastWidth = label.Bounds.Width;
					fitted = OpenRA.Mods.Common.Widgets.WidgetUtils.TruncateText(current, lastWidth, Game.Renderer.Fonts[label.Font]);
				}

				return fitted;
			};
		}

		/// <summary>Gives a label the first font (largest first) its current text fits in.</summary>
		public static void FitFont(OpenRA.Mods.Common.Widgets.LabelWidget label, string[] fonts)
		{
			var text = label.GetText() ?? "";
			foreach (var font in fonts)
			{
				if (!Game.Renderer.Fonts.TryGetValue(font, out var f))
					continue;

				label.Font = font;
				if (f.Measure(text).X <= label.Bounds.Width)
					return;
			}
		}

		/// <summary>A count for tight readouts: full digits below 10,000, then 12.3k / 4.56M.</summary>
		public static string Compact(long value)
		{
			return CityUtils.FormatMoneyCompact(value, 10000, false);
		}

		public static string SignedNumber(int value)
		{
			return value > 0 ? "+" + value.ToString(System.Globalization.CultureInfo.CurrentCulture) : value.ToString(System.Globalization.CultureInfo.CurrentCulture);
		}

		/// <summary>Localized name of a factor key ("demand-factor-jobs"); unknown keys are made presentable.</summary>
		public static string FactorName(string key)
		{
			if (string.IsNullOrEmpty(key))
				return "";

			if (FluentProvider.TryGetMessage(key, out var text))
				return text;

			return Prettify(key);
		}

		/// <summary>"demand-factor-free_jobs" -> "Free jobs": drops the common prefixes and title-cases the rest.</summary>
		public static string Prettify(string key)
		{
			foreach (var prefix in new[] { "demand-factor-", "happiness-factor-", "label-", "chirp-", "policy-", "node-" })
				if (key.StartsWith(prefix, System.StringComparison.Ordinal))
				{
					key = key[prefix.Length..];
					break;
				}

			var words = key.Replace('-', ' ').Replace('_', ' ');
			return words.Length == 0 ? key : char.ToUpperInvariant(words[0]) + words[1..];
		}

		/// <summary>Message for a dynamic key, or the prettified key when the fluent file has none.</summary>
		public static string Message(string key, string fallback = null)
		{
			if (!string.IsNullOrEmpty(key) && FluentProvider.TryGetMessage(key, out var text))
				return text;

			return fallback ?? Prettify(key ?? "");
		}

		public static string EducationName(EducationLevel level)
		{
			return Message("label-education-" + level.ToString().ToLowerInvariant(), level.ToString());
		}

		public static string AgeName(AgeGroup age)
		{
			return Message("label-age-" + age.ToString().ToLowerInvariant(), age.ToString());
		}

		public static string ActivityName(CitizenActivity activity)
		{
			return Message("label-activity-" + activity.ToString().ToLowerInvariant(), activity.ToString());
		}

		public static string KindName(PropertyKind kind)
		{
			return Message("label-kind-" + kind.ToString().ToLowerInvariant(), kind.ToString());
		}

		public static Color ToneColor(int tone)
		{
			switch (tone)
			{
				case 1: return Good;
				case 2: return Warn;
				case 3: return Bad;
				default: return CityTheme.Ink;
			}
		}

		/// <summary>Colour of a 0..100 happiness / satisfaction value.</summary>
		public static Color PercentColor(int value)
		{
			return value >= 60 ? Good : value >= 35 ? Warn : Bad;
		}

		/// <summary>RCT2 icon of a budget ledger key (tax-*, fee-*, fares-*, upkeep-*, ...).</summary>
		public static string LedgerIcon(string key)
		{
			if (key == null)
				return null;

			switch (key)
			{
				case "tax-residential": return "zone_res_low";
				case "tax-commercial": return "zone_com_low";
				case "tax-industrial": return "zone_ind";
				case "tax-office": return "zone_off";
				case "fee-parking": return "addon_parking";
				case "upkeep-roads": return "cat_roads";
				case "upkeep-sewage": return "info_sewage";
				case "upkeep-telecom": return "cat_comms";
				case "upkeep-post": return "info_post";
				case "upkeep-services": return "cat_health";
				case "trade": return "tr_ship";
				case "recycling": return "res_paper";
				case "industry": return "cat_industry";
				case "crime": return "info_crime";
				case "milestone": return "stat_permit";
				case "construction": return "tool_upgrade";
			}

			if (key.StartsWith("fee-", StringComparison.Ordinal))
				return "info_" + key[4..];

			if (key.StartsWith("fares-", StringComparison.Ordinal))
				return "tr_" + key[6..];

			if (key.StartsWith("upkeep-", StringComparison.Ordinal))
				return "cat_" + key[7..];

			if (key.StartsWith("loan", StringComparison.Ordinal) || key.Contains("interest"))
				return "stat_loan";

			if (key.StartsWith("transit", StringComparison.Ordinal))
				return "tr_bus";

			return key.Contains("upkeep") || key.Contains("wages") ? "stat_expenses" : "stat_income";
		}

		public static Color FromArgb(int argb)
		{
			return Color.FromArgb(255, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
		}

		/// <summary>Display name of a property: its actor's tooltip, else the kind.</summary>
		public static string PropertyName(Property property)
		{
			if (property == null)
				return "";

			var tooltip = property.Actor?.Info.TraitInfoOrDefault<OpenRA.Mods.Common.Traits.TooltipInfo>();
			return tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : KindName(property.Kind);
		}
	}
}
