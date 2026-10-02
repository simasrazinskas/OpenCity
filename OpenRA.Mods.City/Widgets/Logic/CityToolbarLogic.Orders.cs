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

using System.Collections.Generic;
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityToolbarLogic
	{
		[FluentReference("price")]
		const string TilePriceLabel = "label-tiles-price";

		[FluentReference]
		const string TileOwned = "label-tiles-owned";

		[FluentReference]
		const string TileUnavailable = "label-tiles-unavailable";

		[FluentReference]
		const string TileNoPermit = "label-tiles-no-permit";

		// ---- map tiles ----
		void ToggleTileTool()
		{
			if (ctx.IsToolActive("tiles"))
			{
				CancelTool();
				return;
			}

			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			CloseAllPanels();
			ctx.ActivateTool("tiles", new UiClickToolGenerator(world,
				(p, cell) => source.TileAt(cell, out var x, out var y) && source.TilePrice(x, y) >= 0 ? UiOrders.Tile(p, x, y) : null,
				cell => TileCells(source, cell),
				cell => TileStyle(source, cell)));
		}

		static IEnumerable<CPos> TileCells(IProgressionUiSource source, CPos cell)
		{
			if (!source.TileAt(cell, out var x, out var y))
				return [];

			source.TileBounds(x, y, out var topLeft, out var bottomRight);
			return CityUtils.Rect(topLeft, bottomRight);
		}

		(Color Color, string Label) TileStyle(IProgressionUiSource source, CPos cell)
		{
			if (!source.TileAt(cell, out var x, out var y))
			{
				ctx.HoverTileX = ctx.HoverTileY = -1;
				return (CityDragOrderGenerator.InvalidColor, null);
			}

			ctx.HoverTileX = x;
			ctx.HoverTileY = y;

			if (source.IsTileOwned(x, y))
				return (CityDragOrderGenerator.NeutralColor, FluentProvider.GetMessage(TileOwned));

			var price = source.TilePrice(x, y);
			if (price < 0)
				return (CityDragOrderGenerator.InvalidColor, FluentProvider.GetMessage(TileUnavailable));

			if (source.Permits <= 0)
				return (CityDragOrderGenerator.InvalidColor, FluentProvider.GetMessage(TileNoPermit));

			return (CityDragOrderGenerator.ValidColor, FluentProvider.GetMessage(TilePriceLabel, "price", CityUtils.FormatMoney(price)));
		}
	}
}
