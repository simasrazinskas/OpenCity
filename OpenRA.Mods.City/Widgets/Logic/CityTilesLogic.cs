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
	/// The RCT2 Map tiles window (design/iso/ui/panels/tiles-*.png), shown while the tile tool is active: the tile overview with the
	/// owned, purchasable and locked tiles, and for the selected tile (clicked in the overview or under the cursor on the map) its
	/// status, price, upkeep, buildable land and natural resources (ITileInfoSource), the city's tile figures and a Buy tile button.
	/// </summary>
	public class CityTilesLogic : ChromeLogic
	{
		[FluentReference("name")]
		const string TileName = "label-tiles-tile";

		[FluentReference]
		const string RowStatus = "label-tiles-status";

		[FluentReference]
		const string StatusOwned = "label-tiles-owned";

		[FluentReference]
		const string StatusForSale = "label-tiles-for-sale";

		[FluentReference]
		const string StatusLocked = "label-tiles-locked";

		[FluentReference]
		const string RowCost = "label-tiles-cost";

		[FluentReference]
		const string RowUpkeep = "label-tiles-upkeep";

		[FluentReference]
		const string RowBuildable = "label-tiles-buildable";

		[FluentReference]
		const string HeaderResources = "label-tiles-resources";

		[FluentReference]
		const string RowOwned = "label-tiles-owned-count";

		[FluentReference("owned", "total")]
		const string OwnedOf = "label-tiles-owned-of";

		[FluentReference]
		const string RowPermits = "label-tiles-permits";

		[FluentReference("amount")]
		const string PerMonth = "label-building-per-month";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		static readonly string[] ResourceIcons = ["nat_fertile", "nat_forest", "nat_ore", "nat_oil", "nat_stone", "nat_fish"];

		static readonly Color[] ResourceColors =
		[
			Color.FromArgb(0xA8, 0xB8, 0x4A), Color.FromArgb(0x2F, 0x7A, 0x3A), Color.FromArgb(0x8A, 0x8A, 0x90),
			Color.FromArgb(0x5A, 0x4A, 0x3C), Color.FromArgb(0xB0, 0xA8, 0x98), Color.FromArgb(0x4A, 0x8E, 0xD0)
		];

		static readonly Color Grass = Color.FromArgb(0x58, 0xA0, 0x32);
		static readonly Color ForSaleInk = Color.FromArgb(0x9A, 0x6A, 0x08);

		/// <summary>One line of the details column; Height 0 while hidden.</summary>
		sealed class Line
		{
			public Widget Widget;
			public Func<bool> Visible = () => true;
			public int Height;
			public int Gap;
		}

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityPanelWidget panel;
		readonly Widget details;
		readonly ButtonWidget buy;
		readonly List<Line> lines = [];
		readonly int grid;
		readonly TileDetail[] cache;
		readonly CityTileState[] states;
		readonly Color[] land;
		readonly int[] kindMax = new int[Progression.ResourceKindCount];

		NaturalResourceLayer naturalResources;
		int2 selected = new(-1, -1);
		int2 lastHover = new(-1, -1);
		int sweep;

		[ObjectCreator.UseCtor]
		public CityTilesLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = (CityPanelWidget)widget;
			panel.OnClose = () => ctx.CancelTool();

			grid = ctx.ProgressionUi?.TileGridSize ?? 9;
			cache = new TileDetail[grid * grid];
			states = new CityTileState[grid * grid];
			land = new Color[grid * grid];
			Array.Fill(land, Grass);

			var tiles = widget.Get<CityTileGridWidget>("GRID");
			tiles.Size = grid;
			tiles.Cell = Math.Max(8, (tiles.Bounds.Width - 4) / grid);
			tiles.GetState = (x, y) => states[y * grid + x];
			tiles.GetBase = (x, y) => land[y * grid + x];
			tiles.GetSelected = () => selected;
			tiles.OnSelect = (x, y) => selected = new int2(x, y);

			InitLegend(widget.Get("LEGEND"));

			details = widget.Get("DETAILS");
			BuildDetails();

			buy = widget.Get<ButtonWidget>("BUY");
			buy.IsDisabled = () => !CanBuy();
			buy.OnClick = () =>
			{
				if (world.LocalPlayer != null && selected.X >= 0)
					world.IssueOrder(UiOrders.Tile(world.LocalPlayer, selected.X, selected.Y));
			};

			widget.Get<LogicTickerWidget>("TILES_TICKER").OnTick = Refresh;
			widget.IsVisible = () => ctx.IsToolActive("tiles") && ctx.TileInfo != null;
		}

		static void InitLegend(Widget legend)
		{
			legend.Get<CitySwatchWidget>("OWNED_SWATCH").GetColor = () => Grass;
			legend.Get<CitySwatchWidget>("SALE_SWATCH").GetColor = () => Color.FromArgb(0xAB, 0xC3, 0x6E);
			legend.Get<CitySwatchWidget>("LOCKED_SWATCH").GetColor = () => Color.FromArgb(0x41, 0x50, 0x3D);
		}

		TileDetail Detail()
		{
			return selected.X >= 0 ? cache[selected.Y * grid + selected.X] : default;
		}

		bool CanBuy()
		{
			var detail = Detail();
			return world.LocalPlayer != null && detail.Valid && !detail.Owned && detail.BlockedKey == null;
		}

		static CityTileState StateOf(TileDetail detail)
		{
			return detail.Owned ? CityTileState.Owned : detail.Adjacent ? CityTileState.ForSale : CityTileState.Locked;
		}

		void BuildDetails()
		{
			Line Add(string template, Func<bool> visible, int height, int gap = 0)
			{
				var widget = Game.LoadWidget(world, template, details, []);
				var line = new Line { Widget = widget, Visible = visible ?? (() => true), Height = height, Gap = gap };
				lines.Add(line);
				return line;
			}

			Widget Row(string key, Func<string> value, Func<Color> color, Func<bool> visible = null, int gap = 0)
			{
				var line = Add("CITY_TILE_ROW", visible, 13, gap);
				var name = key == null ? "" : FluentProvider.GetMessage(key);
				line.Widget.Get<LabelWidget>("NAME").GetText = () => name;
				var label = line.Widget.Get<LabelWidget>("VALUE");
				label.GetText = value;
				label.GetColor = color;
				return line.Widget;
			}

			var header = (CityHeaderWidget)Add("CITY_TILE_HEADER", null, 14).Widget;
			header.GetText = () => selected.X < 0 ? "" :
				FluentProvider.GetMessage(TileName, "name", "" + (char)('A' + selected.X) + (selected.Y + 1));

			bool Valid() => Detail().Valid;
			bool ForSale() => Detail().Valid && !Detail().Owned && Detail().Adjacent;

			Row(RowStatus, StatusText, StatusColor, Valid);
			Row(RowCost, () => CityUtils.FormatMoney(Detail().Price), () => CityTheme.MoneyNegative, ForSale);
			Row(RowUpkeep, () => FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(Detail().UpkeepIncrease)),
				() => CityTheme.MoneyNegative, ForSale);

			var reason = Row(null, () => Detail().BlockedKey == null ? "" : CityUi.Message(Detail().BlockedKey), () => CityTheme.MoneyNegative,
				() => Detail().Valid && !Detail().Owned && Detail().Adjacent && Detail().BlockedKey != null);

			var buildable = Row(RowBuildable, () => FluentProvider.GetMessage(Percent, "value", BuildablePercent()), () => CityTheme.Ink, Valid);
			var buildableBar = buildable.Get<CityBarWidget>("BAR");
			buildableBar.Visible = true;
			buildableBar.Ramp = "green";
			buildableBar.Bounds.X = 80;
			buildableBar.Bounds.Width = details.Bounds.Width - 80 - 36;
			buildableBar.GetPercentage = BuildablePercent;

			bool HasResources() => Detail().Valid;
			var resourcesHeader = (CityHeaderWidget)Add("CITY_TILE_HEADER", HasResources, 14, 3).Widget;
			var resourcesText = FluentProvider.GetMessage(HeaderResources);
			resourcesHeader.GetText = () => resourcesText;
			for (var k = 0; k < kindMax.Length; k++)
			{
				var kind = k;
				var row = Add("CITY_TILE_ROW", HasResources, 14).Widget;
				var icon = row.Get<CityIconWidget>("ICON");
				icon.Visible = true;
				icon.Icon = ResourceIcons[Math.Min(k, ResourceIcons.Length - 1)];
				var name = row.Get<LabelWidget>("NAME");
				name.Bounds.X = 18;
				name.Bounds.Width = 58;
				var text = WorldInfoViewSource.KindName(kind);
				name.GetText = CityUi.Fitted(name, () => text);
				var bar = row.Get<CityBarWidget>("BAR");
				bar.Visible = true;
				bar.Smooth = true;
				bar.Ramp = "yellow";
				bar.Bounds.X = 80;
				bar.Bounds.Width = details.Bounds.Width - 80 - 36;
				bar.GetPercentage = () => ResourcePercent(kind);
				var value = row.Get<LabelWidget>("VALUE");
				value.GetText = () => FluentProvider.GetMessage(Percent, "value", ResourcePercent(kind));
				value.GetColor = () => CityTheme.Ink;
			}

			Add("CITY_TILE_LINE", null, 2, 3);
			Row(RowOwned, () => FluentProvider.GetMessage(OwnedOf, "owned", ctx.TileInfo?.OwnedTiles ?? 0, "total", ctx.TileInfo?.TotalTiles ?? 0),
				() => CityTheme.Ink, null, 3);
			Row(RowPermits, () => (ctx.ProgressionUi?.Permits ?? 0).ToString(CultureInfo.CurrentCulture),
				() => (ctx.ProgressionUi?.Permits ?? 0) > 0 ? CityTheme.MoneyPositive : CityTheme.MoneyNegative);
		}

		int BuildablePercent()
		{
			var detail = Detail();
			return detail.TotalCells <= 0 ? 0 : detail.BuildableCells * 100 / detail.TotalCells;
		}

		int ResourcePercent(int kind)
		{
			var resources = Detail().Resources;
			return resources == null || kind >= resources.Length || kindMax[kind] <= 0 ? 0 : (int)(resources[kind] * 100L / kindMax[kind]);
		}

		string StatusText()
		{
			return FluentProvider.GetMessage(StateOf(Detail()) switch
			{
				CityTileState.Owned => StatusOwned,
				CityTileState.ForSale => StatusForSale,
				_ => StatusLocked
			});
		}

		Color StatusColor()
		{
			return StateOf(Detail()) switch
			{
				CityTileState.Owned => CityTheme.MoneyPositive,
				CityTileState.ForSale => ForSaleInk,
				_ => CityTheme.MoneyNegative
			};
		}

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			var info = ctx.TileInfo;
			if (source == null || info == null)
				return;

			// The tile under the cursor on the map becomes the selected tile when it changes.
			var hover = new int2(ctx.HoverTileX, ctx.HoverTileY);
			if (hover != lastHover)
			{
				lastHover = hover;
				if (hover.X >= 0 && hover.X < grid && hover.Y >= 0 && hover.Y < grid)
					selected = hover;
			}

			// A few tiles are re-read per tick (details walk the tile's cells), the selected one every tick.
			for (var i = 0; i < 3; i++)
			{
				sweep = (sweep + 1) % cache.Length;
				cache[sweep] = Read(info, source, sweep % grid, sweep / grid);
			}

			if (selected.X >= 0)
				cache[selected.Y * grid + selected.X] = Read(info, source, selected.X, selected.Y);

			Array.Clear(kindMax);
			for (var t = 0; t < cache.Length; t++)
			{
				var resources = cache[t].Resources;
				if (resources == null)
					continue;

				for (var k = 0; k < kindMax.Length && k < resources.Length; k++)
					kindMax[k] = Math.Max(kindMax[k], resources[k]);
			}

			var first = -1;
			for (var t = 0; t < cache.Length; t++)
			{
				var x = t % grid;
				var y = t / grid;
				var state = source.IsTileOwned(x, y) ? CityTileState.Owned : source.TilePrice(x, y) >= 0 ? CityTileState.ForSale : CityTileState.Locked;
				states[t] = state;
				if (state == CityTileState.ForSale && first < 0)
					first = t;

				land[t] = LandColour(cache[t]);
			}

			if (selected.X < 0)
				selected = first >= 0 ? new int2(first % grid, first / grid) : new int2(grid / 2, grid / 2);

			Layout();
		}

		/// <summary>The tile's details; the provider leaves out the resources of owned tiles, which are read from the resource layer here.</summary>
		TileDetail Read(ITileInfoSource info, IProgressionUiSource source, int x, int y)
		{
			var detail = info.Detail(x, y);
			if (!detail.Owned || detail.Resources == null)
				return detail;

			naturalResources ??= world.WorldActor.TraitOrDefault<NaturalResourceLayer>();
			if (naturalResources == null)
				return detail;

			source.TileBounds(x, y, out var topLeft, out var bottomRight);
			var kinds = Enum.GetValues<NaturalResourceKind>();
			foreach (var cell in CityUtils.Rect(topLeft, bottomRight))
				if (world.Map.Contains(cell))
					for (var k = 0; k < kinds.Length && k < detail.Resources.Length; k++)
						detail.Resources[k] += naturalResources.GetAmount(kinds[k], cell);

			return detail;
		}

		/// <summary>The colour of the richest natural resource of a tile, grass when it has none worth showing.</summary>
		Color LandColour(TileDetail detail)
		{
			var best = -1;
			var bestShare = 0.3f;
			if (detail.Resources != null)
			{
				for (var k = 0; k < detail.Resources.Length && k < kindMax.Length; k++)
				{
					var share = kindMax[k] <= 0 ? 0f : detail.Resources[k] / (float)kindMax[k];
					if (share > bestShare)
					{
						bestShare = share;
						best = k;
					}
				}
			}

			return best < 0 ? Grass : ResourceColors[Math.Min(best, ResourceColors.Length - 1)];
		}

		void Layout()
		{
			var y = 0;
			foreach (var line in lines)
			{
				var visible = line.Visible();
				line.Widget.Visible = visible;
				if (!visible)
					continue;

				y += line.Gap;
				line.Widget.Bounds.Y = y;
				line.Widget.Bounds.Width = details.Bounds.Width;
				y += line.Height;
			}
		}
	}
}
