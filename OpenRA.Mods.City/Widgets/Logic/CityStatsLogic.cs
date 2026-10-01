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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Statistics panel: categories on the left, the series of the category as toggles (up to four lines at once),
	/// the line chart in the middle and 1 year / 5 years / all time scale buttons. Data: ICityStatistics.History.
	/// Only series that have at least one sample are offered, so each WP adds a graph by recording a series.
	/// </summary>
	public class CityStatsLogic : ChromeLogic
	{
		[FluentReference("count")]
		const string MonthsAgo = "label-stats-months-ago";

		[FluentReference]
		const string Now = "label-stats-now";

		[FluentReference]
		const string Empty = "label-stats-empty";

		const int MaxLines = 4;

		sealed class SeriesDef
		{
			public string Id;
			public string Category;
			public string NameKey;
			public Color Color;
		}

		// Series ids follow design 10 section 3.7; sims record them with ICityStatistics.Record.
		static readonly SeriesDef[] Catalogue =
		[
			Def("population", "population", "population", 0x7ED957),
			Def("population", "households", "households", 0x5AB4F0),
			Def("population", "workers", "workers", 0xF2C94C),
			Def("population", "unemployed", "unemployed", 0xFF6B5E),
			Def("population", "jobs", "jobs", 0xB07CE8),
			Def("population", "students", "students", 0x4FD6C6),
			Def("population", "tourists", "tourists", 0xE86BB4),
			Def("population", "buildings", "buildings", 0xB4C4D0),
			Def("economy", "funds", "money", 0xF2C94C),
			Def("economy", "income", "income", 0x5CD67A),
			Def("economy", "expenses", "expenses", 0xFF6B5E),
			Def("city", "happiness", "happiness", 0xF2C94C),
			Def("city", "health", "health", 0xFF6B5E),
			Def("city", "land-value", "landvalue", 0x5AB4F0),
			Def("city", "demand-residential", "demand-r", 0x7ED957),
			Def("city", "demand-commercial", "demand-c", 0x5AB4F0),
			Def("city", "demand-industrial", "demand-i", 0xF2C94C),
			Def("city", "svc-crimes", "crime", 0xE86BB4),
			Def("city", "svc-sick", "sick", 0x4FD6C6),
			Def("city", "svc-deaths", "deaths", 0xB4C4D0),
			Def("city", "svc-fires", "fires", 0xFF8C3A),
			Def("city", "svc-garbage-generated-t", "garbage-generated", 0xD69E6B),
			Def("city", "svc-garbage-collected-t", "garbage-collected", 0x7ED957),
			Def("utilities", "power-produced", "power-produced", 0xF2C94C),
			Def("utilities", "power-used", "power-used", 0xFF8C3A),
			Def("utilities", "water-produced", "water-produced", 0x5AB4F0),
			Def("utilities", "water-used", "water-used", 0x4FD6C6),
			Def("environment", "pollution-ground", "pollution-ground", 0xD69E6B),
			Def("environment", "pollution-air", "pollution-air", 0xB4C4D0),
			Def("environment", "pollution-noise", "pollution-noise", 0xE86BB4),
			Def("environment", "temperature", "temperature", 0xFF6B5E),
			Def("traffic", "traffic-flow", "traffic-flow", 0x5CD67A),
			Def("traffic", "vehicles", "vehicles", 0x5AB4F0),
		];

		static readonly string[] Categories = ["overview", "population", "economy", "city", "utilities", "environment", "traffic"];
		static readonly int[] Scales = [12, 60, 120];
		static readonly string[] ScaleKeys = ["button-stats-scale-year", "button-stats-scale-five", "button-stats-scale-all"];

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget categoryList;
		readonly Widget seriesList;
		readonly Widget overview;
		readonly Dictionary<string, ButtonWidget> categoryButtons = [];
		readonly List<string> enabled = [];
		readonly List<GraphSeries> graph = [];

		string category = "overview";
		int scale = Scales[0];
		string builtCategory;

		static SeriesDef Def(string category, string id, string nameKey, int rgb)
		{
			return new SeriesDef { Category = category, Id = id, NameKey = "label-stats-" + nameKey, Color = CityUi.FromArgb(rgb) };
		}

		[ObjectCreator.UseCtor]
		public CityStatsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			categoryList = widget.Get("CATEGORIES");
			seriesList = widget.Get("SERIES");

			var scaleContainer = widget.Get("SCALE");
			scaleContainer.IsVisible = () => category != "overview";
			for (var i = 0; i < Scales.Length; i++)
			{
				var months = Scales[i];
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", scaleContainer, []) as ButtonWidget;
				button.Bounds.X = i * 108;
				button.Bounds.Width = 102;
				button.Bounds.Height = 26;
				var text = FluentProvider.GetMessage(ScaleKeys[i]);
				button.GetText = () => text;
				button.IsHighlighted = () => scale == months;
				button.OnClick = () => scale = months;
			}

			var chart = widget.Get<CityGraphWidget>("GRAPH");
			chart.GetSeries = BuildSeries;
			chart.GetSampleLabel = ago => ago <= 0 ? FluentProvider.GetMessage(Now) : FluentProvider.GetMessage(MonthsAgo, "count", ago);

			overview = widget.Get("OVERVIEW");
			overview.IsVisible = () => category == "overview";
			chart.IsVisible = () => category != "overview";
			BuildOverview();

			var empty = widget.Get<LabelWidget>("EMPTY");
			empty.GetText = () => FluentProvider.GetMessage(Empty);
			empty.IsVisible = () => category != "overview" && graph.Count == 0;

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		IEnumerable<SeriesDef> Available(string forCategory)
		{
			// The overview is a snapshot of the citizens, not a recorded series.
			if (forCategory == "overview")
				yield break;

			foreach (var def in Catalogue)
				if (def.Category == forCategory && HasData(def))
					yield return def;
		}

		bool HasData(SeriesDef def)
		{
			var history = ctx.Statistics?.History(def.Id, 1);
			return history != null && history.Count > 0;
		}

		void Refresh()
		{
			// Categories without any recorded series are hidden; rebuilt when the first panel frame runs.
			if (categoryButtons.Count == 0)
				BuildCategories();

			if (builtCategory != category)
				BuildSeriesList();
		}

		void BuildCategories()
		{
			categoryList.RemoveChildren();
			var y = 0;
			foreach (var c in Categories)
			{
				var any = c == "overview" && ctx.Citizens != null;
				foreach (var _ in Available(c))
				{
					any = true;
					break;
				}

				if (!any)
					continue;

				var id = c;
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", categoryList, []) as ButtonWidget;
				button.Bounds.Y = y;
				button.Bounds.Width = 150;
				button.Bounds.Height = 24;
				var text = CityUi.Message("label-stats-category-" + c, CityUi.Prettify(c));
				button.GetText = () => text;
				button.IsHighlighted = () => category == id;
				button.OnClick = () =>
				{
					category = id;
					builtCategory = null;
				};

				categoryButtons[c] = button;
				y += 27;
			}

			// The series toggles sit below the category buttons.
			seriesList.Bounds.Y = categoryList.Bounds.Y + y + 10;

			if (categoryButtons.Count > 0 && !categoryButtons.ContainsKey(category))
				foreach (var c in categoryButtons.Keys)
				{
					category = c;
					break;
				}
		}

		void BuildOverview()
		{
			var y = 0;
			void Heading(string key)
			{
				var label = new LabelWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(0, y, overview.Bounds.Width, 20),
					Font = "Bold",
					Shadow = true,
					GetColor = () => CityUi.Accent
				};

				var text = FluentProvider.GetMessage(key);
				label.GetText = () => text;
				overview.AddChild(label);
				y += 24;
			}

			void Bar(string name, Func<int> value, Func<int> max, Color color)
			{
				var nameLabel = new LabelWidget(Game.ModData) { Bounds = new WidgetBounds(8, y, 150, 18), Font = "Small", GetText = () => name };
				var bar = new CityBarWidget
				{
					Bounds = new WidgetBounds(160, y + 2, 250, 14),
					BarColor = color,
					GetPercentage = () => (int)(value() * 100L / Math.Max(1, max()))
				};

				var valueLabel = new LabelWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(416, y, 90, 18),
					Font = "Bold",
					Align = TextAlign.Right,
					GetText = () => value().ToString("N0", System.Globalization.CultureInfo.CurrentCulture)
				};

				overview.AddChild(nameLabel);
				overview.AddChild(bar);
				overview.AddChild(valueLabel);
				y += 22;
			}

			int Population() => ctx.Citizens?.Population ?? 0;
			Heading("label-overview-age");
			foreach (var age in new[] { AgeGroup.Child, AgeGroup.Teen, AgeGroup.Adult, AgeGroup.Senior })
			{
				var group = age;
				Bar(CityUi.AgeName(age), () => ctx.Citizens?.CountByAge(group) ?? 0, Population, CityUi.CategoryColor(ZoneCategory.Residential));
			}

			y += 6;
			Heading("label-overview-education");
			foreach (var level in new[] { EducationLevel.Uneducated, EducationLevel.Poorly, EducationLevel.Educated, EducationLevel.Well, EducationLevel.Highly })
			{
				var edu = level;
				Bar(CityUi.EducationName(level), () => ctx.Citizens?.CountByEducation(edu) ?? 0, Population, CityUi.CategoryColor(ZoneCategory.Commercial));
			}

			y += 6;
			Heading("label-overview-city");
			Bar(CityUi.Message("label-overview-workers"), () => ctx.Citizens?.Workers ?? 0, Population, CityUi.Good);
			Bar(CityUi.Message("label-overview-unemployed"), () => ctx.Citizens?.Unemployed ?? 0, Population, CityUi.Bad);
			Bar(CityUi.Message("label-overview-students"), () => ctx.Citizens?.Students ?? 0, Population, CityUi.Warn);
			Bar(CityUi.Message("label-overview-homeless"), () => ctx.Citizens?.Homeless ?? 0, Population, CityUi.Bad);
			Bar(CityUi.Message("label-overview-tourists"), () => ctx.Citizens?.Tourists ?? 0, Population, CityUi.Accent);
		}

		void BuildSeriesList()
		{
			builtCategory = category;
			seriesList.RemoveChildren();
			enabled.Clear();

			var y = 0;
			foreach (var def in Available(category))
			{
				var current = def;
				if (enabled.Count < 2)
					enabled.Add(def.Id);

				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", seriesList, []) as ButtonWidget;
				button.Bounds.Y = y;
				button.Bounds.Width = 150;
				button.Bounds.Height = 20;
				var text = CityUi.Message(def.NameKey, CityUi.Prettify(def.Id));
				button.GetText = () => text;
				button.GetColor = () => enabled.Contains(current.Id) ? current.Color : CityUi.Muted;
				button.IsHighlighted = () => enabled.Contains(current.Id);
				button.OnClick = () =>
				{
					if (!enabled.Remove(current.Id) && enabled.Count < MaxLines)
						enabled.Add(current.Id);
				};

				y += 22;
			}
		}

		IReadOnlyList<GraphSeries> BuildSeries()
		{
			graph.Clear();
			if (ctx.Statistics == null)
				return graph;

			foreach (var def in Catalogue)
			{
				if (def.Category != category || !enabled.Contains(def.Id))
					continue;

				graph.Add(new GraphSeries
				{
					Name = CityUi.Message(def.NameKey, CityUi.Prettify(def.Id)),
					Color = def.Color,
					Values = ctx.Statistics.History(def.Id, scale)
				});
			}

			return graph;
		}
	}
}
