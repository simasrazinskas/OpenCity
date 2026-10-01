#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
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
	/// Map tile purchase panel, shown while the tile tool is active: permits, owned tiles and tile upkeep, and for the tile under the
	/// cursor its status, price, upkeep increase, buildable land and natural resources (ITileInfoSource).
	/// </summary>
	public class CityTilesLogic : ChromeLogic
	{
		[FluentReference("owned", "total")]
		const string Owned = "label-tiles-count";

		[FluentReference]
		const string RowTiles = "label-tiles-row-tiles";

		[FluentReference]
		const string RowPermits = "label-tiles-row-permits";

		[FluentReference]
		const string RowStatus = "label-tiles-row-status";

		[FluentReference]
		const string RowPrice = "label-tiles-row-price";

		[FluentReference]
		const string RowUpkeep = "label-tiles-row-upkeep";

		[FluentReference]
		const string RowLand = "label-tiles-row-land";

		[FluentReference]
		const string RowResources = "label-tiles-row-resources";

		[FluentReference]
		const string StatusBuyable = "label-tiles-status-buyable";

		[FluentReference]
		const string StatusOwned = "label-tiles-owned";

		[FluentReference("amount")]
		const string PerMonth = "label-building-per-month";

		[FluentReference("buildable", "total")]
		const string Land = "label-tiles-land";

		[FluentReference]
		const string None = "label-tiles-no-resources";

		[FluentReference]
		const string LegendOwned = "label-tiles-legend-owned";

		[FluentReference]
		const string LegendBuyable = "label-tiles-legend-buyable";

		[FluentReference]
		const string LegendLocked = "label-tiles-legend-locked";

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly Widget rows;
		readonly List<Widget> rowWidgets = [];

		[ObjectCreator.UseCtor]
		public CityTilesLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;
			rows = widget.Get("ROWS");

			AddRow(RowTiles, () => ctx.TileInfo == null ? "" :
				FluentProvider.GetMessage(Owned, "owned", ctx.TileInfo.OwnedTiles, "total", ctx.TileInfo.TotalTiles), () => Color.White, () => true);
			AddRow(RowPermits, () => (ctx.ProgressionUi?.Permits ?? 0).ToString(CultureInfo.CurrentCulture),
				() => (ctx.ProgressionUi?.Permits ?? 0) > 0 ? CityUi.Good : CityUi.Bad, () => true);

			AddRow(RowStatus, StatusText, StatusColor, () => Detail().Valid);
			AddRow(RowPrice, () => CityUtils.FormatMoney(Detail().Price), () => Color.White, () => Detail().Valid && !Detail().Owned);
			AddRow(RowUpkeep, () => FluentProvider.GetMessage(PerMonth, "amount", CityUi.SignedMoney(Detail().UpkeepIncrease)),
				() => CityUi.Warn, () => Detail().Valid && !Detail().Owned);

			AddRow(RowLand, () => FluentProvider.GetMessage(Land, "buildable", Detail().BuildableCells, "total", Detail().TotalCells),
				() => Color.White, () => Detail().Valid);

			AddRow(RowResources, ResourceText, () => Color.White, () => Detail().Valid && !Detail().Owned);

			BuildLegend(widget.Get("LEGEND"));

			widget.IsVisible = () =>
			{
				if (!ctx.IsToolActive("tiles") || ctx.TileInfo == null)
					return false;

				Layout();
				return true;
			};
		}

		TileDetail Detail()
		{
			return ctx.TileInfo != null && ctx.HoverTileX >= 0 ? ctx.TileInfo.Detail(ctx.HoverTileX, ctx.HoverTileY) : default;
		}

		void AddRow(string key, Func<string> value, Func<Color> color, Func<bool> visible)
		{
			var row = Game.LoadWidget(world, "CITY_INFO_ROW", rows, []);
			row.Bounds.Width = 376;
			var name = FluentProvider.GetMessage(key);
			row.Get<LabelWidget>("NAME").GetText = () => name;
			var label = row.Get<LabelWidget>("VALUE");
			label.Bounds.X = 130;
			label.Bounds.Width = 246;
			label.GetText = value;
			label.GetColor = color;
			row.IsVisible = visible;
			rowWidgets.Add(row);
		}

		string StatusText()
		{
			var detail = Detail();
			if (detail.Owned)
				return FluentProvider.GetMessage(StatusOwned);

			return detail.BlockedKey != null ? CityUi.Message(detail.BlockedKey) : FluentProvider.GetMessage(StatusBuyable);
		}

		Color StatusColor()
		{
			var detail = Detail();
			return detail.Owned ? CityUi.Muted : detail.BlockedKey != null ? CityUi.Bad : CityUi.Good;
		}

		string ResourceText()
		{
			var resources = Detail().Resources;
			if (resources == null)
				return FluentProvider.GetMessage(None);

			var parts = new List<string>();
			for (var kind = 0; kind < resources.Length; kind++)
				if (resources[kind] > 0)
					parts.Add(WorldInfoViewSource.KindName(kind));

			return parts.Count == 0 ? FluentProvider.GetMessage(None) : string.Join(", ", parts);
		}

		void Layout()
		{
			var y = 0;
			foreach (var row in rowWidgets)
			{
				if (!row.IsVisible())
					continue;

				row.Bounds.Y = y;
				y += 22;
			}

			rows.Bounds.Height = y;
			panel.Bounds.Height = 32 + y + 32;
			var legend = panel.Get("LEGEND");
			legend.Bounds.Y = 32 + y + 6;
		}

		static void BuildLegend(Widget legend)
		{
			void Entry(int x, Color color, string key)
			{
				legend.AddChild(new ColorBlockWidget(Game.ModData) { Bounds = new WidgetBounds(x, 3, 14, 14), GetColor = () => color });
				var text = FluentProvider.GetMessage(key);
				legend.AddChild(new LabelWidget(Game.ModData) { Bounds = new WidgetBounds(x + 20, 0, 100, 20), Font = "Small", GetText = () => text });
			}

			Entry(0, Color.FromArgb(0xE8, 0xC4, 0x60), LegendOwned);
			Entry(124, Color.FromArgb(0x70, 0xDC, 0x5A), LegendBuyable);
			Entry(248, Color.FromArgb(0x60, 0x60, 0x70), LegendLocked);
		}
	}
}
