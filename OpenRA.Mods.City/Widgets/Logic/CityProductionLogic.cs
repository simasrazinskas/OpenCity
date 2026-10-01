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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Production and trade panel: one row per resource with produced, consumed, imported and exported units, the stock and a
	/// surplus / deficit bar (IEconomyUiSource.GetResourceStat); selecting a row charts its history when statistics exist.
	/// </summary>
	public class CityProductionLogic : ChromeLogic
	{
		[FluentReference("name")]
		const string ChartTitle = "label-production-chart";

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		readonly LabelWidget chartTitle;
		readonly List<GraphSeries> chart = [];

		int builtFor = -1;
		int selected = 1;

		[ObjectCreator.UseCtor]
		public CityProductionLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			list = widget.Get<ScrollPanelWidget>("LIST");

			chartTitle = widget.Get<LabelWidget>("CHART_TITLE");
			chartTitle.GetText = () => ctx.Economy == null || selected > ctx.Economy.ResourceCount ? "" :
				FluentProvider.GetMessage(ChartTitle, "name", ctx.Economy.ResourceName(selected));

			var graph = widget.Get<CityGraphWidget>("CHART");
			graph.GetSeries = ChartSeries;
			graph.GetSampleLabel = ago => ago <= 0 ? FluentProvider.GetMessage("label-stats-now") : FluentProvider.GetMessage("label-stats-months-ago", "count", ago);
			graph.IsVisible = () => true;

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Build();

				return visible;
			};
		}

		void Build()
		{
			var economy = ctx.Economy;
			var count = economy?.ResourceCount ?? 0;
			if (builtFor == count)
				return;

			builtFor = count;
			list.RemoveChildren();
			if (economy == null || ctx.EconomyUi == null)
				return;

			for (var r = 1; r <= count; r++)
				AddRow(r, economy);

			list.Layout.AdjustChildren();
		}

		void AddRow(int resource, ICityEconomy economy)
		{
			var source = ctx.EconomyUi;
			var row = Game.LoadWidget(world, "CITY_PRODUCTION_ROW", list, []);
			row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;

			var name = CityUi.Message("label-resource-" + economy.ResourceName(resource).ToLowerInvariant(), economy.ResourceName(resource));
			var button = row.Get<ButtonWidget>("NAME");
			button.GetText = () => name;
			button.GetColor = () => selected == resource ? CityUi.Accent : Color.White;
			button.OnClick = () => selected = resource;

			Bind(row, "PRODUCED", () => source.GetResourceStat(resource).Produced);
			Bind(row, "CONSUMED", () => source.GetResourceStat(resource).Consumed);
			Bind(row, "IMPORTED", () => source.GetResourceStat(resource).Imported);
			Bind(row, "EXPORTED", () => source.GetResourceStat(resource).Exported);
			Bind(row, "STOCK", () => source.GetResourceStat(resource).Stock);

			// The bar shows surplus (green, right) or deficit (red) relative to the larger of production and consumption.
			var bar = row.Get<CityBarWidget>("BALANCE");
			bar.GetPercentage = () =>
			{
				var stat = source.GetResourceStat(resource);
				var big = Math.Max(1, Math.Max(stat.Produced, stat.Consumed));
				return Math.Abs(stat.Produced - stat.Consumed) * 100 / big;
			};

			bar.GetBarColor = () =>
			{
				var stat = source.GetResourceStat(resource);
				return stat.Produced >= stat.Consumed ? CityUi.Good : CityUi.Bad;
			};
		}

		static void Bind(Widget row, string id, Func<int> value)
		{
			row.Get<LabelWidget>(id).GetText = () => value().ToString("N0", CultureInfo.CurrentCulture);
		}

		static readonly (TradeField Field, string Key, int Rgb)[] Fields =
		[
			(TradeField.Produced, "label-production-produced", 0x5CD67A),
			(TradeField.Consumed, "label-production-consumed", 0xFF6B5E),
			(TradeField.Imported, "label-production-imported", 0x5AB4F0),
			(TradeField.Exported, "label-production-exported", 0xF2C94C)
		];

		IReadOnlyList<GraphSeries> ChartSeries()
		{
			chart.Clear();
			var economy = ctx.Get<CityEconomy>();
			if (economy == null || selected < 1 || selected > economy.ResourceCount)
				return chart;

			// The economy keeps twelve completed months of every figure.
			foreach (var (field, key, rgb) in Fields)
				chart.Add(new GraphSeries
				{
					Name = FluentProvider.GetMessage(key),
					Color = CityUi.FromArgb(rgb),
					Values = economy.ResourceHistory(selected, field)
				});

			return chart;
		}
	}
}
