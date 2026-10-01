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
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Colour ramp of an info view. The legend strip draws the same ramp.</summary>
	public enum InfoRamp : byte
	{
		/// <summary>Red (0) to green (100): higher is better.</summary>
		Good,

		/// <summary>Green (0) to red (100): higher is worse.</summary>
		Bad,

		/// <summary>Clear (0) to orange to brown (100).</summary>
		Pollution,

		/// <summary>Pale blue (0) to deep blue (100): density, value, wealth.</summary>
		Blue,

		/// <summary>Dark (0) to bright green (100): availability.</summary>
		Green,

		/// <summary>Distinct colour per index (districts, road classes).</summary>
		Category,

		/// <summary>Natural resources: value = kind * 16 + step 0..10; the hue is the kind, the strength the richness.</summary>
		Resource
	}

	/// <summary>One entry of the info view catalogue shared by the InfoViewLayer and the info view panel.</summary>
	public sealed class InfoViewDef
	{
		public CityInfoView Mode;

		/// <summary>Suffix of the fluent keys label-infoview-{Id}, -desc, -low and -high.</summary>
		public string Id;
		public string Group;
		public InfoRamp Ramp;
		public Func<CityUiContext, bool> Available;

		public string Name => CityUi.Message("label-infoview-" + Id, CityUi.Prettify(Id));
		public string Description => CityUi.Message("label-infoview-" + Id + "-desc", "");
		public string Low => CityUi.Message("label-infoview-" + Id + "-low", DefaultLow);
		public string High => CityUi.Message("label-infoview-" + Id + "-high", DefaultHigh);

		string DefaultLow => FluentProvider.GetMessage(Ramp == InfoRamp.Good ? "label-infoviews-legend-low" : "label-infoviews-legend-min");
		string DefaultHigh => FluentProvider.GetMessage(Ramp == InfoRamp.Good ? "label-infoviews-legend-high" : "label-infoviews-legend-max");
	}

	public static class InfoViews
	{
		const int HeatAlpha = 180;

		public static readonly string[] Groups = ["services", "networks", "environment", "city"];

		static readonly Color[] CategoryColors =
		[
			Color.FromArgb(0xE5, 0x4B, 0x3C), Color.FromArgb(0x3F, 0xA9, 0xF5), Color.FromArgb(0x7E, 0xD9, 0x57), Color.FromArgb(0xF2, 0xC9, 0x4C),
			Color.FromArgb(0xB0, 0x7C, 0xE8), Color.FromArgb(0xFF, 0x8C, 0x3A), Color.FromArgb(0x4F, 0xD6, 0xC6), Color.FromArgb(0xE8, 0x6B, 0xB4),
			Color.FromArgb(0x9A, 0xC9, 0x3C), Color.FromArgb(0x5A, 0x6F, 0xE0), Color.FromArgb(0xD6, 0x9E, 0x6B), Color.FromArgb(0x6B, 0xC9, 0x9B),
			Color.FromArgb(0xC9, 0x5A, 0x7B), Color.FromArgb(0x8F, 0xA8, 0xB8), Color.FromArgb(0xE0, 0xE0, 0x5A), Color.FromArgb(0x5A, 0xA0, 0x8A)
		];

		static InfoViewDef Def(CityInfoView mode, string id, string group, InfoRamp ramp, Func<CityUiContext, bool> available)
		{
			return new InfoViewDef { Mode = mode, Id = id, Group = group, Ramp = ramp, Available = available };
		}

		static bool Source(CityUiContext c, CityInfoView mode)
		{
			foreach (var s in c.Sources)
				if (s.Supports(mode))
					return true;

			return false;
		}

		/// <summary>All views in panel order. Each is offered only when the provider(s) it reads exist.</summary>
		public static readonly IReadOnlyList<InfoViewDef> All =
		[
			Def(CityInfoView.Power, "power", "services", InfoRamp.Good, _ => true),
			Def(CityInfoView.Water, "water", "services", InfoRamp.Good, _ => true),
			Def(CityInfoView.Garbage, "garbage", "services", InfoRamp.Good, c => c.Services != null),
			Def(CityInfoView.Health, "health", "services", InfoRamp.Good, c => c.Services != null || c.Coverage),
			Def(CityInfoView.Deathcare, "deathcare", "services", InfoRamp.Good, c => c.Services != null),
			Def(CityInfoView.Education, "education", "services", InfoRamp.Good, c => c.Services != null || c.Coverage),
			Def(CityInfoView.Police, "police", "services", InfoRamp.Good, c => c.Services != null || c.Coverage),
			Def(CityInfoView.Crime, "crime", "services", InfoRamp.Bad, c => c.Services != null),
			Def(CityInfoView.Fire, "fire", "services", InfoRamp.Good, c => c.Services != null || c.Coverage),
			Def(CityInfoView.Parks, "parks", "services", InfoRamp.Good, c => c.Services != null || c.Coverage),
			Def(CityInfoView.Telecom, "telecom", "services", InfoRamp.Good, c => c.Services != null),
			Def(CityInfoView.Post, "post", "services", InfoRamp.Good, c => c.Services != null),

			// Networks
			Def(CityInfoView.PowerGrid, "powergrid", "networks", InfoRamp.Good, c => c.Utilities != null),
			Def(CityInfoView.WaterGrid, "watergrid", "networks", InfoRamp.Good, c => c.Utilities != null),
			Def(CityInfoView.Sewage, "sewage", "networks", InfoRamp.Good, c => c.Utilities != null),
			Def(CityInfoView.Roads, "roads", "networks", InfoRamp.Category, c => c.Roads != null),
			Def(CityInfoView.Traffic, "traffic", "networks", InfoRamp.Bad, c => c.Roads != null),
			Def(CityInfoView.Transit, "transit", "networks", InfoRamp.Green, c => Source(c, CityInfoView.Transit)),
			Def(CityInfoView.Freight, "freight", "networks", InfoRamp.Bad, c => Source(c, CityInfoView.Freight)),

			// Environment
			Def(CityInfoView.Pollution, "pollution", "environment", InfoRamp.Pollution, c => c.Pollution != null || c.Coverage),
			Def(CityInfoView.AirPollution, "air", "environment", InfoRamp.Pollution, c => c.Pollution != null),
			Def(CityInfoView.GroundPollution, "ground", "environment", InfoRamp.Pollution, c => c.Pollution != null),
			Def(CityInfoView.Noise, "noise", "environment", InfoRamp.Pollution, c => c.Pollution != null),
			Def(CityInfoView.Groundwater, "groundwater", "environment", InfoRamp.Pollution, c => c.Pollution != null),
			Def(CityInfoView.NaturalResources, "resources", "environment", InfoRamp.Resource, c => Source(c, CityInfoView.NaturalResources)),
			Def(CityInfoView.LandValue, "landvalue", "environment", InfoRamp.Blue, c => c.Coverage || c.Properties != null),

			// City
			Def(CityInfoView.Happiness, "happiness", "city", InfoRamp.Good, _ => true),
			Def(CityInfoView.BuildingLevel, "level", "city", InfoRamp.Good, c => c.Properties != null),
			Def(CityInfoView.Education2, "attainment", "city", InfoRamp.Blue, c => c.Citizens != null && c.Properties != null),
			Def(CityInfoView.Wealth, "wealth", "city", InfoRamp.Blue, c => c.Citizens != null && c.Properties != null),
			Def(CityInfoView.Age, "age", "city", InfoRamp.Blue, c => c.Citizens != null && c.Properties != null),
			Def(CityInfoView.Production, "production", "city", InfoRamp.Green, c => Source(c, CityInfoView.Production)),
			Def(CityInfoView.Tourism, "tourism", "city", InfoRamp.Blue, c => Source(c, CityInfoView.Tourism)),
			Def(CityInfoView.Districts, "districts", "city", InfoRamp.Category, c => c.Progression != null),
		];

		static readonly Dictionary<CityInfoView, InfoViewDef> ByMode = BuildIndex();

		static Dictionary<CityInfoView, InfoViewDef> BuildIndex()
		{
			var index = new Dictionary<CityInfoView, InfoViewDef>();
			foreach (var def in All)
				index[def.Mode] = def;

			return index;
		}

		public static InfoViewDef Get(CityInfoView mode)
		{
			return ByMode.TryGetValue(mode, out var def) ? def : null;
		}

		public static Color CategoryColor(int index)
		{
			return CategoryColors[(index % CategoryColors.Length + CategoryColors.Length) % CategoryColors.Length];
		}

		static Color Lerp(Color a, Color b, float t)
		{
			t = t < 0 ? 0 : t > 1 ? 1 : t;
			return Color.FromArgb(
				(int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
		}

		/// <summary>Colour of a 0..100 value (or a category index for InfoRamp.Category) on a ramp.</summary>
		static readonly Color[] ResourceHues =
		[
			Color.FromArgb(235, 205, 60), Color.FromArgb(60, 185, 75), Color.FromArgb(70, 130, 235),
			Color.FromArgb(70, 70, 90), Color.FromArgb(170, 160, 150), Color.FromArgb(60, 200, 210)
		];

		public static Color RampColor(InfoRamp ramp, int value)
		{
			if (ramp == InfoRamp.Resource)
			{
				var hue = ResourceHues[Math.Clamp(value / 16, 0, ResourceHues.Length - 1)];
				return Color.FromArgb(70 + Math.Clamp(value % 16, 0, 10) * 14, hue);
			}

			var t = Math.Clamp(value, 0, 100) / 100f;
			switch (ramp)
			{
				case InfoRamp.Good:
					return Color.FromArgb(HeatAlpha, CityUi.HeatColor(t));
				case InfoRamp.Bad:
					return Color.FromArgb(HeatAlpha, CityUi.HeatColor(1 - t));
				case InfoRamp.Pollution:
					return t < 0.5f
						? Lerp(Color.FromArgb(40, 150, 220, 130), Color.FromArgb(170, 240, 170, 40), t * 2)
						: Lerp(Color.FromArgb(170, 240, 170, 40), Color.FromArgb(225, 105, 45, 20), (t - 0.5f) * 2);
				case InfoRamp.Blue:
					return Lerp(Color.FromArgb(70, 205, 230, 255), Color.FromArgb(205, 40, 85, 215), t);
				case InfoRamp.Green:
					return Lerp(Color.FromArgb(150, 25, 35, 25), Color.FromArgb(210, 70, 215, 95), t);
				default:
					return Color.FromArgb(175, CategoryColor(value));
			}
		}

		/// <summary>Short "label: value" lines under the legend (up to 3) with city-wide numbers of a view.</summary>
		public static IEnumerable<(string Label, string Value)> Summary(CityInfoView mode, CityUiContext c)
		{
			string Number(long v) => v.ToString("N0", CultureInfo.CurrentCulture);
			string Percent(int v) => FluentProvider.GetMessage("label-city-percent", "value", v);

			switch (mode)
			{
				case CityInfoView.Power:
				case CityInfoView.PowerGrid:
					if (c.Utilities != null)
					{
						yield return (CityUi.Message("label-summary-produced", "Produced"), Number(c.Utilities.PowerProduced));
						yield return (CityUi.Message("label-summary-used", "Used"), Number(c.Utilities.PowerConsumed));
					}

					break;
				case CityInfoView.Water:
				case CityInfoView.WaterGrid:
					if (c.Utilities != null)
					{
						yield return (CityUi.Message("label-summary-produced", "Produced"), Number(c.Utilities.WaterProduced));
						yield return (CityUi.Message("label-summary-used", "Used"), Number(c.Utilities.WaterConsumed));
					}

					break;
				case CityInfoView.Sewage:
					if (c.Utilities != null)
					{
						yield return (CityUi.Message("label-summary-capacity", "Capacity"), Number(c.Utilities.SewageCapacity));
						yield return (CityUi.Message("label-summary-used", "Used"), Number(c.Utilities.SewageProduced));
					}

					break;
				case CityInfoView.Traffic:
				case CityInfoView.Roads:
					if (c.Traffic != null)
					{
						yield return (CityUi.Message("label-summary-flow", "City traffic flow"), Percent(c.Traffic.CityTrafficFlow));
						yield return (CityUi.Message("label-summary-vehicles", "Vehicles"), Number(c.Traffic.ActiveVehicles));
					}

					break;
				case CityInfoView.Freight:
					if (c.Get<ILogistics>() is { } logistics)
						yield return (CityUi.Message("label-summary-shipments", "Shipments in transit"), Number(logistics.ShipmentsInTransit));

					break;
				case CityInfoView.Tourism:
					if (c.Get<ITourism>() is { } tourism)
					{
						yield return (CityUi.Message("label-summary-attractiveness", "Attractiveness"), Percent(tourism.Attractiveness));
						yield return (CityUi.Message("label-summary-visitors", "Visitor groups per month"), Number(tourism.VisitorGroupsPerMonth));
					}

					if (c.Citizens != null)
						yield return (CityUi.Message("label-summary-tourists", "Tourists now"), Number(c.Citizens.Tourists));

					break;
				case CityInfoView.Happiness:
					if (c.Citizens != null)
					{
						yield return (CityUi.Message("label-summary-happiness", "Average happiness"), Percent(c.Citizens.AverageHappiness));
						yield return (CityUi.Message("label-summary-health", "Average health"), Percent(c.Citizens.AverageHealth));
					}

					break;
				case CityInfoView.Education2:
					if (c.Citizens != null)
						foreach (var level in new[] { EducationLevel.Uneducated, EducationLevel.Educated, EducationLevel.Highly })
							yield return (CityUi.EducationName(level), Number(c.Citizens.CountByEducation(level)));

					break;
				case CityInfoView.Age:
					if (c.Citizens != null)
						foreach (var age in new[] { AgeGroup.Child, AgeGroup.Adult, AgeGroup.Senior })
							yield return (CityUi.AgeName(age), Number(c.Citizens.CountByAge(age)));

					break;
				case CityInfoView.Garbage:
				case CityInfoView.Deathcare:
				case CityInfoView.Health:
				case CityInfoView.Education:
				case CityInfoView.Police:
				case CityInfoView.Crime:
				case CityInfoView.Fire:
				case CityInfoView.Parks:
				case CityInfoView.Telecom:
				case CityInfoView.Post:
					var kind = ServiceOf(mode);
					if (c.Services != null)
						yield return (CityUi.Message("label-summary-budget", "Budget"), Percent(c.Services.GetBudget(kind)));

					break;
				case CityInfoView.Pollution:
				case CityInfoView.AirPollution:
				case CityInfoView.GroundPollution:
				case CityInfoView.Noise:
				case CityInfoView.Groundwater:
					if (c.Citizens != null)
						yield return (CityUi.Message("label-summary-health", "Average health"), Percent(c.Citizens.AverageHealth));

					break;
			}
		}

		public static ServiceKind ServiceOf(CityInfoView mode)
		{
			switch (mode)
			{
				case CityInfoView.Garbage: return ServiceKind.Garbage;
				case CityInfoView.Deathcare: return ServiceKind.Deathcare;
				case CityInfoView.Health: return ServiceKind.Health;
				case CityInfoView.Education: return ServiceKind.Education;
				case CityInfoView.Police:
				case CityInfoView.Crime: return ServiceKind.Police;
				case CityInfoView.Fire: return ServiceKind.Fire;
				case CityInfoView.Parks: return ServiceKind.Parks;
				case CityInfoView.Telecom: return ServiceKind.Telecom;
				case CityInfoView.Post: return ServiceKind.Post;
				default: return ServiceKind.Admin;
			}
		}
	}
}
