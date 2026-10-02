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
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 Production and trade window (design/iso/ui/panels/production-trade*.png): a filterable, sortable table with one row
	/// per resource (produced and consumed as meters, imported and exported units; IEconomyUiSource.GetResourceStat), the trade
	/// summary of the month and the twelve-month history chart of the selected resource.
	/// </summary>
	public class CityProductionLogic : ChromeLogic
	{
		[FluentReference("name")]
		const string ChartTitle = "label-production-chart-title";

		[FluentReference("count")]
		const string CountText = "label-production-count";

		[FluentReference]
		const string FilterAll = "label-production-filter-all";

		[FluentReference]
		const string FilterProducing = "label-production-filter-producing";

		[FluentReference]
		const string FilterShort = "label-production-filter-short";

		[FluentReference]
		const string FilterTrading = "label-production-filter-trading";

		[FluentReference]
		const string Produced = "label-production-produced";

		[FluentReference]
		const string Consumed = "label-production-consumed";

		[FluentReference]
		const string Imported = "label-production-imported";

		[FluentReference]
		const string Exported = "label-production-exported";

		// Row layout in logical pixels: icon, name, then two meter columns and two trade columns that share the rest.
		const int IconColumn = 22, NameColumn = 70, TradeColumn = 46, ValueWidth = 30;

		static readonly (TradeField Field, string Key, string Ramp, int Shade)[] Fields =
		[
			(TradeField.Produced, Produced, "green", 6),
			(TradeField.Consumed, Consumed, "red", 5),
			(TradeField.Imported, Imported, "blue", 5),
			(TradeField.Exported, Exported, "yellow", 5)
		];

		enum Filter { All, Producing, Short, Trading }

		enum Column { Name, Produced, Consumed, Imported, Exported }

		sealed class Row
		{
			public int Resource;
			public string Name;
			public Widget Widget;
		}

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityPanelWidget panel;
		readonly ScrollPanelWidget list;
		readonly Widget header;
		readonly LabelWidget count;
		readonly List<GraphSeries> chart = [];
		readonly Dictionary<int, Row> rows = [];
		readonly Dictionary<Column, ButtonWidget> headers = [];
		static readonly Color ImportColor = Color.FromArgb(0x2A, 0x5A, 0xA8);
		static readonly Color ExportColor = Color.FromArgb(0xB4, 0x5C, 0x12);
		static readonly Color ImportLight = Color.FromArgb(0x8C, 0xC0, 0xFF);
		static readonly Color ExportLight = Color.FromArgb(0xFF, 0xB8, 0x70);

		Filter filter = Filter.All;
		Column sort = Column.Produced;
		bool ascending;
		bool hideUnused;
		int selected = 1;
		bool picked;
		int scale = 1;
		int lastWidth = -1;
		int visibleCount;
		string signature = "";

		[ObjectCreator.UseCtor]
		public CityProductionLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = (CityPanelWidget)widget;

			list = widget.Get<ScrollPanelWidget>("LIST");
			header = widget.Get("HEADER");
			count = widget.Get<LabelWidget>("COUNT");
			count.GetText = () => FluentProvider.GetMessage(CountText, "count", visibleCount);

			var hide = widget.Get<CheckboxWidget>("HIDE_UNUSED");
			hide.IsChecked = () => hideUnused;
			hide.OnClick = () => hideUnused = !hideUnused;

			InitFilter(widget.Get<DropDownButtonWidget>("FILTER"));
			InitHeader();
			InitSummary(widget.Get("TRADE"));

			var chartHeader = widget.Get<CityHeaderWidget>("CHART_HEADER");
			chartHeader.GetText = () => ctx.Economy == null || selected > ctx.Economy.ResourceCount ? "" :
				FluentProvider.GetMessage(ChartTitle, "name", ResourceTitle(ctx.Economy, selected));

			var graph = widget.Get<CityGraphWidget>("CHART");
			graph.GetSeries = ChartSeries;
			graph.GetSampleLabel = ago => ago <= 0 ? FluentProvider.GetMessage("label-stats-now") : FluentProvider.GetMessage("label-stats-months-ago", "count", ago);
			graph.IsVisible = () => true;

			widget.Get<LogicTickerWidget>("PRODUCTION_TICKER").OnTick = Refresh;
		}

		static string ResourceTitle(ICityEconomy economy, int resource)
		{
			var key = economy.ResourceName(resource);
			return CityUi.Message("label-resource-" + key.ToLowerInvariant(), SplitWords(key));
		}

		/// <summary>"ConvenienceFood" -> "Convenience food".</summary>
		static string SplitWords(string key)
		{
			var text = new StringBuilder();
			for (var i = 0; i < key.Length; i++)
			{
				if (i > 0 && char.IsUpper(key[i]) && char.IsLower(key[i - 1]))
					text.Append(' ').Append(char.ToLowerInvariant(key[i]));
				else
					text.Append(key[i]);
			}

			return text.ToString();
		}

		void InitFilter(DropDownButtonWidget dropdown)
		{
			static string Name(Filter f) => FluentProvider.GetMessage(f switch
			{
				Filter.Producing => FilterProducing,
				Filter.Short => FilterShort,
				Filter.Trading => FilterTrading,
				_ => FilterAll
			});

			dropdown.GetText = () => Name(filter);
			dropdown.OnMouseDown = _ =>
			{
				var options = Enum.GetValues<Filter>();
				ScrollItemWidget Setup(Filter option, ScrollItemWidget template)
				{
					var item = ScrollItemWidget.Setup(template, () => filter == option, () => filter = option);
					var text = Name(option);
					item.Get<LabelWidget>("LABEL").GetText = () => text;
					return item;
				}

				dropdown.ShowDropDown("CITY_PRODUCTION_FILTER_TEMPLATE", options.Length * 14 + 4, options, Setup);
			};
		}

		void InitHeader()
		{
			foreach (var column in Enum.GetValues<Column>())
			{
				var id = column switch
				{
					Column.Name => "H_NAME",
					Column.Produced => "H_PRODUCED",
					Column.Consumed => "H_CONSUMED",
					Column.Imported => "H_IMPORTED",
					_ => "H_EXPORTED"
				};

				var button = header.Get<ButtonWidget>(id);
				var c = column;
				headers[c] = button;
				button.IsHighlighted = () => sort == c;
				button.OnClick = () =>
				{
					// A second click on the active column reverses it; a new column starts with its natural order.
					if (sort == c)
						ascending = !ascending;
					else
					{
						sort = c;
						ascending = c == Column.Name;
					}
				};

				var arrow = button.Get<CityTriangleWidget>("SORT");
				arrow.IsVisible = () => sort == c;
				arrow.GetUp = () => ascending;
				arrow.GetColor = () => CityTheme.Ink;
			}
		}

		void InitSummary(Widget trade)
		{
			CityEconomy Economy() => ctx.Get<CityEconomy>();
			long Imports() => Economy() == null ? 0 : Economy().LedgerThisMonth(CityEconomy.LImport) / 100;
			long Exports() => Economy() == null ? 0 : Economy().LedgerThisMonth(CityEconomy.LExport) / 100;
			long Balance() => Economy() == null ? 0 : Economy().TradeBalanceThisMonth / 100;

			var imports = trade.Get<LabelWidget>("IMPORTS");
			imports.GetText = () => Imports() == 0 ? CityUtils.FormatMoney(0) : "-" + CityUtils.FormatMoney(Imports());
			imports.GetColor = () => Imports() == 0 ? CityTheme.Ink : CityTheme.MoneyNegative;

			var exports = trade.Get<LabelWidget>("EXPORTS");
			exports.GetText = () => Exports() == 0 ? CityUtils.FormatMoney(0) : "+" + CityUtils.FormatMoney(Exports());
			exports.GetColor = () => Exports() == 0 ? CityTheme.Ink : CityTheme.MoneyPositive;

			var balance = trade.Get<LabelWidget>("BALANCE");
			balance.GetText = () => Balance() == 0 ? CityUtils.FormatMoney(0) : CityUi.SignedMoney(Balance());
			balance.GetColor = () => Balance() == 0 ? CityTheme.Ink : Balance() < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;

			var shortages = trade.Get<LabelWidget>("SHORTAGES");
			shortages.GetText = () => ShortageCount().ToString(CultureInfo.CurrentCulture);
			shortages.GetColor = () => ShortageCount() > 0 ? CityTheme.MoneyNegative : CityTheme.Ink;
		}

		int ShortageCount()
		{
			var source = ctx.EconomyUi;
			var economy = ctx.Economy;
			if (source == null || economy == null)
				return 0;

			var n = 0;
			for (var r = 1; r <= economy.ResourceCount; r++)
			{
				var stat = source.GetResourceStat(r);
				if (stat.Consumed > stat.Produced + stat.Imported)
					n++;
			}

			return n;
		}

		bool Matches(ResourceStat stat)
		{
			if (hideUnused && stat.Produced + stat.Consumed + stat.Imported + stat.Exported == 0)
				return false;

			return filter switch
			{
				Filter.Producing => stat.Produced > 0,
				Filter.Short => stat.Consumed > stat.Produced,
				Filter.Trading => stat.Imported > 0 || stat.Exported > 0,
				_ => true
			};
		}

		int Value(ResourceStat stat) => sort switch
		{
			Column.Produced => stat.Produced,
			Column.Consumed => stat.Consumed,
			Column.Imported => stat.Imported,
			_ => stat.Exported
		};

		/// <summary>Re-filters and re-sorts the table (cheap: a few dozen resources, run from the window's ticker).</summary>
		void Refresh()
		{
			var economy = ctx.Economy;
			var source = ctx.EconomyUi;
			if (economy == null || source == null || !panel.IsVisible())
				return;

			var total = economy.ResourceCount;
			for (var r = 1; r <= total; r++)
				if (!rows.ContainsKey(r))
					rows[r] = AddRow(r, economy, source);

			scale = 1;
			for (var r = 1; r <= total; r++)
			{
				var stat = source.GetResourceStat(r);
				scale = Math.Max(scale, Math.Max(stat.Produced, stat.Consumed));
			}

			var visible = rows.Values.Where(row => Matches(source.GetResourceStat(row.Resource))).ToList();
			visible.Sort((a, b) =>
			{
				int result;
				if (sort == Column.Name)
					result = string.Compare(a.Name, b.Name, StringComparison.CurrentCulture) * (ascending ? 1 : -1);
				else
					result = Value(source.GetResourceStat(b.Resource)).CompareTo(Value(source.GetResourceStat(a.Resource))) * (ascending ? -1 : 1);

				return result != 0 ? result : a.Resource.CompareTo(b.Resource);
			});

			visibleCount = visible.Count;
			var width = list.Bounds.Width - list.ScrollbarWidth - 2;
			var sig = string.Join(",", visible.Select(v => v.Resource));
			if (sig == signature && width == lastWidth)
				return;

			signature = sig;
			lastWidth = width;
			list.RemoveChildren();
			foreach (var row in visible)
			{
				row.Widget.Bounds.Width = width;
				LayoutColumns(row.Widget, width);
				list.AddChild(row.Widget);
			}

			list.Layout.AdjustChildren();
			LayoutHeader(width);

			// The top row is charted until the player picks another one (or the picked one is filtered out).
			if (visible.Count > 0 && (!picked || !visible.Any(v => v.Resource == selected)))
				selected = visible[0].Resource;
		}

		static int MeterWidth(int width) => (width - IconColumn - NameColumn - 2 * TradeColumn) / 2;

		void LayoutHeader(int width)
		{
			var meter = MeterWidth(width);
			var edges = new[]
			{
				0, IconColumn + NameColumn, IconColumn + NameColumn + meter, IconColumn + NameColumn + 2 * meter,
				IconColumn + NameColumn + 2 * meter + TradeColumn, width
			};

			var columns = Enum.GetValues<Column>();
			for (var i = 0; i < columns.Length; i++)
			{
				var b = headers[columns[i]];
				b.Bounds.X = edges[i];
				b.Bounds.Width = edges[i + 1] - edges[i];
				b.Get<CityTriangleWidget>("SORT").Bounds.X = b.Bounds.Width - 11;
			}

			header.Bounds.Width = width;
		}

		static void LayoutColumns(Widget row, int width)
		{
			var meter = MeterWidth(width);
			var x = IconColumn;
			var name = row.Get<LabelWidget>("NAME");
			name.Bounds.X = x;
			name.Bounds.Width = NameColumn;
			x += NameColumn;

			foreach (var (bar, label) in new[] { ("PRODUCED_BAR", "PRODUCED"), ("CONSUMED_BAR", "CONSUMED") })
			{
				var b = row.Get<CityBarWidget>(bar);
				b.Bounds.X = x + 2;
				b.Bounds.Width = meter - ValueWidth - 8;
				var l = row.Get<LabelWidget>(label);
				l.Bounds.X = x;
				l.Bounds.Width = meter - 3;
				x += meter;
			}

			foreach (var (arrow, label) in new[] { ("IMPORT_ARROW", "IMPORTED"), ("EXPORT_ARROW", "EXPORTED") })
			{
				row.Get(arrow).Bounds.X = x + 4;
				var l = row.Get<LabelWidget>(label);
				l.Bounds.X = x + 13;
				l.Bounds.Width = TradeColumn - 13;
				x += TradeColumn;
			}
		}

		Row AddRow(int resource, ICityEconomy economy, IEconomyUiSource source)
		{
			var key = economy.ResourceName(resource);
			var name = ResourceTitle(economy, resource);
			var widget = Game.LoadWidget(world, "CITY_PRODUCTION_ROW", list, []);
			list.RemoveChild(widget);

			bool Selected() => selected == resource;
			widget.Get<CityRowBackgroundWidget>("BG").IsSelected = Selected;
			Color Ink() => Selected() ? CityTheme.InkLight : CityTheme.Ink;
			Color Muted() => Selected() ? CityTheme.InkLight : CityTheme.Muted("finance");

			widget.Get<CityIconWidget>("ICON").Icon = "res_" + key.ToLowerInvariant();
			var nameLabel = widget.Get<LabelWidget>("NAME");
			nameLabel.GetText = CityUi.Fitted(nameLabel, () => name);
			nameLabel.GetColor = Ink;
			widget.Get<ButtonWidget>("SELECT").OnClick = () =>
			{
				selected = resource;
				picked = true;
			};

			ResourceStat Stat() => source.GetResourceStat(resource);
			bool Short() => Stat().Consumed > Stat().Produced;
			Color Negative() => Selected() ? CityTheme.MoneyNegativeLight : CityTheme.MoneyNegative;

			Meter(widget, "PRODUCED", () => Stat().Produced, Ink);
			widget.Get<CityBarWidget>("PRODUCED_BAR").Ramp = "green";
			Meter(widget, "CONSUMED", () => Stat().Consumed, () => Short() ? Negative() : Ink());
			widget.Get<CityBarWidget>("CONSUMED_BAR").GetRamp = () => Short() ? "red" : "yellow";

			Trade(widget, "IMPORTED", "IMPORT_ARROW", () => Stat().Imported, () => Selected() ? ImportLight : ImportColor, Muted);
			Trade(widget, "EXPORTED", "EXPORT_ARROW", () => Stat().Exported, () => Selected() ? ExportLight : ExportColor, Muted);
			return new Row { Resource = resource, Name = name, Widget = widget };
		}

		void Meter(Widget row, string id, Func<int> value, Func<Color> color)
		{
			row.Get<CityBarWidget>(id + "_BAR").GetPercentage = () => (int)(value() * 100L / Math.Max(1, scale));
			var label = row.Get<LabelWidget>(id);
			label.GetText = () => Amount(value());
			label.GetColor = color;
		}

		/// <summary>Whole units up to 9,999, then 12.3k / 4.5M so large flows fit their column.</summary>
		static string Amount(int value)
		{
			return value < 10000 ? value.ToString("N0", CultureInfo.CurrentCulture) : CityGraphWidget.Compact(value);
		}

		static void Trade(Widget row, string id, string arrowId, Func<int> value, Func<Color> color, Func<Color> muted)
		{
			var arrow = row.Get<CityTriangleWidget>(arrowId);
			arrow.IsVisible = () => value() > 0;
			arrow.GetColor = color;
			var label = row.Get<LabelWidget>(id);
			label.GetText = () => value() > 0 ? Amount(value()) : "-";
			label.GetColor = () => value() > 0 ? color() : muted();
		}

		IReadOnlyList<GraphSeries> ChartSeries()
		{
			chart.Clear();
			var economy = ctx.Get<CityEconomy>();
			if (economy == null || selected < 1 || selected > economy.ResourceCount)
				return chart;

			// The economy keeps twelve completed months of every figure.
			foreach (var (field, key, ramp, shade) in Fields)
				chart.Add(new GraphSeries
				{
					Name = FluentProvider.GetMessage(key),
					Color = CityTheme.Ramp(ramp, shade),
					Values = economy.ResourceHistory(selected, field)
				});

			return chart;
		}
	}
}
