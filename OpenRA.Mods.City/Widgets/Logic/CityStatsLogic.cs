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
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 statistics window (design/iso/ui/panels/statistics-*.png): an icon tab per category, a checklist of the
	/// category's series (colour key, zebra rows), Year / 5 Years / All buttons, the line chart and a read-out strip with the
	/// sample under the pointer. Data: ICityStatistics.History. Only series that have at least one sample are offered, so
	/// each work package adds a graph by recording a series. (The citizen overview moved to the city info window.)
	/// </summary>
	public class CityStatsLogic : ChromeLogic
	{
		[FluentReference("count")]
		const string MonthsAgo = "label-stats-months-ago";

		[FluentReference]
		const string Now = "label-stats-now";

		[FluentReference]
		const string Empty = "label-stats-empty";

		[FluentReference("category")]
		const string WindowTitle = "label-stats-window-title";

		[FluentReference("shown", "total")]
		const string SeriesShown = "label-stats-series-shown";

		const int DefaultShown = 4;

		enum Unit { Number, Money, Percent, TenthsCelsius }

		sealed class SeriesDef
		{
			public string Id;
			public string Category;
			public string NameKey;
			public Unit Unit;
			public Color Color;
		}

		// The key colours of the design (SC in tools/iso_ui_panels_finance.py): bright, for the dark chart.
		static readonly int[] Palette =
		[
			0x7CF06C, 0xFF6A50, 0x5AB4F0, 0xF2C94C, 0xD08CFF, 0xF08CD0, 0x50E0D0, 0xF0A040, 0xC8C8C8, 0xA0E060, 0xFF9A8A, 0x90A8FF
		];

		// Series ids follow design 10 section 3.7; sims record them with ICityStatistics.Record.
		static readonly SeriesDef[] Catalogue = BuildCatalogue(
		[
			("population", "population", "population", Unit.Number),
			("population", "households", "households", Unit.Number),
			("population", "workers", "workers", Unit.Number),
			("population", "unemployed", "unemployed", Unit.Number),
			("population", "jobs", "jobs", Unit.Number),
			("population", "students", "students", Unit.Number),
			("population", "tourists", "tourists", Unit.Number),
			("population", "buildings", "buildings", Unit.Number),
			("economy", "funds", "money", Unit.Money),
			("economy", "income", "income", Unit.Money),
			("economy", "expenses", "expenses", Unit.Money),
			("city", "happiness", "happiness", Unit.Percent),
			("city", "health", "health", Unit.Percent),
			("city", "land-value", "landvalue", Unit.Number),
			("city", "demand-residential", "demand-r", Unit.Percent),
			("city", "demand-commercial", "demand-c", Unit.Percent),
			("city", "demand-industrial", "demand-i", Unit.Percent),
			("city", "svc-crimes", "crime", Unit.Number),
			("city", "svc-sick", "sick", Unit.Number),
			("city", "svc-deaths", "deaths", Unit.Number),
			("city", "svc-fires", "fires", Unit.Number),
			("city", "svc-garbage-generated-t", "garbage-generated", Unit.Number),
			("city", "svc-garbage-collected-t", "garbage-collected", Unit.Number),
			("utilities", "power-produced", "power-produced", Unit.Number),
			("utilities", "power-used", "power-used", Unit.Number),
			("utilities", "water-produced", "water-produced", Unit.Number),
			("utilities", "water-used", "water-used", Unit.Number),
			("environment", "pollution-ground", "pollution-ground", Unit.Percent),
			("environment", "pollution-air", "pollution-air", Unit.Percent),
			("environment", "pollution-noise", "pollution-noise", Unit.Percent),
			("environment", "temperature", "temperature", Unit.TenthsCelsius),
			("traffic", "traffic-flow", "traffic-flow", Unit.Percent),
			("traffic", "vehicles", "vehicles", Unit.Number),
		]);

		static readonly (string Id, string Icon)[] Categories =
		[
			("population", "stat_population"),
			("economy", "stat_money"),
			("city", "pnl_city_info"),
			("utilities", "info_power"),
			("environment", "info_pollution"),
			("traffic", "info_traffic"),
		];

		static readonly int[] Scales = [12, 60, 120];
		static readonly string[] ScaleKeys = ["label-stats-scale-year", "label-stats-scale-five", "label-stats-scale-all"];

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget seriesList;
		readonly CityGraphWidget chart;
		readonly Dictionary<string, HashSet<string>> enabled = [];
		readonly List<GraphSeries> graph = [];
		readonly List<Unit> graphUnits = [];

		string category = Categories[0].Id;
		int scale = Scales[0];
		string builtCategory;

		static SeriesDef[] BuildCatalogue((string Category, string Id, string Name, Unit Unit)[] rows)
		{
			// Each series keeps the colour of its position within its category.
			var index = new Dictionary<string, int>();
			return rows.Select(r =>
			{
				index.TryGetValue(r.Category, out var i);
				index[r.Category] = i + 1;
				return new SeriesDef
				{
					Category = r.Category,
					Id = r.Id,
					NameKey = "label-stats-" + r.Name,
					Unit = r.Unit,
					Color = CityUi.FromArgb(Palette[i % Palette.Length])
				};
			}).ToArray();
		}

		[ObjectCreator.UseCtor]
		public CityStatsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			var window = (CityPanelWidget)widget;
			window.GetTitle = () => FluentProvider.GetMessage(WindowTitle, "category", CategoryName(category));
			window.SetTabs(Categories.Select(c =>
			{
				var id = c.Id;
				var text = CategoryName(id);
				return new CityWindowTab
				{
					Icon = c.Icon,
					GetTooltip = () => text,
					IsActive = () => category == id,
					IsDisabled = () => !Available(id).Any(),
					OnClick = () => category = id
				};
			}));

			seriesList = widget.Get("LEFT").Get<ScrollPanelWidget>("SERIES");
			var count = widget.Get("LEFT").Get<LabelWidget>("COUNT");
			count.GetText = () => FluentProvider.GetMessage(SeriesShown, "shown", ShownCount(), "total", Available(category).Count());
			count.GetColor = () => CityTheme.Muted("info");

			var right = widget.Get("RIGHT");
			var scaleContainer = right.Get("SCALE");
			for (var i = 0; i < Scales.Length; i++)
			{
				var months = Scales[i];
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", scaleContainer, []) as ButtonWidget;
				button.Bounds.X = i * 52;
				button.Bounds.Width = 50;
				button.Bounds.Height = 14;
				var text = FluentProvider.GetMessage(ScaleKeys[i]);
				button.GetText = () => text;
				button.IsHighlighted = () => scale == months;
				button.OnClick = () => scale = months;
			}

			chart = right.Get<CityGraphWidget>("GRAPH");
			chart.GetSeries = BuildSeries;
			chart.GetSampleLabel = SampleLabel;

			var empty = right.Get<LabelWidget>("EMPTY");
			empty.GetText = () => FluentProvider.GetMessage(Empty);
			empty.GetColor = () => CityTheme.InkLight;
			empty.IsVisible = () => graph.Count == 0;

			var readout = right.Get("READOUT").Get<LabelWidget>("READOUT_TEXT");
			readout.GetText = ReadOut;
			readout.GetColor = () => CityTheme.InkLight;

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		static string CategoryName(string id) { return CityUi.Message("label-stats-category-" + id, CityUi.Prettify(id)); }

		static string SampleLabel(int ago)
		{
			return ago <= 0 ? FluentProvider.GetMessage(Now) : FluentProvider.GetMessage(MonthsAgo, "count", ago);
		}

		IEnumerable<SeriesDef> Available(string forCategory)
		{
			foreach (var def in Catalogue)
				if (def.Category == forCategory && HasData(def))
					yield return def;
		}

		bool HasData(SeriesDef def)
		{
			var history = ctx.Statistics?.History(def.Id, 1);
			return history != null && history.Count > 0;
		}

		int ShownCount()
		{
			return enabled.TryGetValue(category, out var set) ? Available(category).Count(d => set.Contains(d.Id)) : 0;
		}

		void Refresh()
		{
			// A category without recorded series is not selectable: move to the first that has some.
			if (!Available(category).Any())
			{
				var (firstId, _) = Categories.FirstOrDefault(c => Available(c.Id).Any());
				if (firstId != null)
					category = firstId;
			}

			if (builtCategory != category)
				BuildSeriesList();
		}

		void BuildSeriesList()
		{
			builtCategory = category;
			seriesList.RemoveChildren();
			var defs = Available(category).ToList();
			if (!enabled.TryGetValue(category, out var set))
				enabled[category] = set = defs.Take(DefaultShown).Select(d => d.Id).ToHashSet();

			foreach (var def in defs)
			{
				var current = def;
				var row = Game.LoadWidget(world, "CITY_STATS_ROW", seriesList, []);
				row.Bounds.Width = seriesList.Bounds.Width - seriesList.ScrollbarWidth - 2;
				void Toggle()
				{
					if (!set.Remove(current.Id))
						set.Add(current.Id);
				}

				var check = row.Get<CheckboxWidget>("CHECK");
				check.IsChecked = () => set.Contains(current.Id);
				check.OnClick = Toggle;

				var key = row.Get<CitySwatchWidget>("KEY");
				key.Color = def.Color;
				key.IsDimmed = () => !set.Contains(current.Id);

				var name = row.Get<ButtonWidget>("NAME");
				var text = CityUi.Message(def.NameKey, CityUi.Prettify(def.Id));
				name.Background = "";
				name.Bounds.Width = row.Bounds.Width - name.Bounds.X;
				row.Get("BG").Bounds.Width = row.Bounds.Width;
				name.GetText = () => text;
				name.GetColor = () => set.Contains(current.Id) ? CityTheme.Ink : CityTheme.Muted("info");
				name.OnClick = Toggle;
			}

			seriesList.Layout.AdjustChildren();
			seriesList.ScrollToTop();
		}

		IReadOnlyList<GraphSeries> BuildSeries()
		{
			graph.Clear();
			graphUnits.Clear();
			if (ctx.Statistics == null || !enabled.TryGetValue(category, out var set))
				return graph;

			foreach (var def in Catalogue)
			{
				if (def.Category != category || !set.Contains(def.Id) || !HasData(def))
					continue;

				graph.Add(new GraphSeries
				{
					Name = CityUi.Message(def.NameKey, CityUi.Prettify(def.Id)),
					Color = def.Color,
					Values = ctx.Statistics.History(def.Id, scale)
				});
				graphUnits.Add(def.Unit);
			}

			return graph;
		}

		static string Format(Unit unit, int value)
		{
			switch (unit)
			{
				case Unit.Money: return CityUtils.FormatMoney(value);
				case Unit.Percent: return FluentProvider.GetMessage("label-city-percent", "value", value);
				case Unit.TenthsCelsius: return (value / 10f).ToString("0.#", CultureInfo.CurrentCulture) + " C";
				default: return value.ToString("N0", CultureInfo.CurrentCulture);
			}
		}

		/// <summary>"3 months ago: Population 1,204" for the first shown series and the sample under the pointer (newest when none).</summary>
		string ReadOut()
		{
			var firstIndex = graph.FindIndex(g => g.Values != null && g.Values.Count > 0);
			if (firstIndex < 0)
				return "";

			var first = graph[firstIndex];

			var ago = Math.Max(0, chart.HoverAgo);
			var index = first.Values.Count - 1 - ago;
			if (index < 0)
				return "";

			return SampleLabel(ago) + ": " + first.Name + " " + Format(graphUnits[firstIndex], first.Values[index]);
		}
	}
}
