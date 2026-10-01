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
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

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

		sealed class OrderButtonSpec
		{
			public string Id;
			public string Hotkey;
			public Func<bool> Available;

			/// <summary>Progression unlock key ("tool:policies"): the button is shown but disabled until it is unlocked.</summary>
			public string Unlock;
			public Func<bool> Active;
			public Action OnClick;
		}

		const int OrdersPerRow = 5;

		int seenChirps;

		void BuildOrderRows(Widget widget)
		{
			var specs = new List<OrderButtonSpec>
			{
				Panel("stats", "CityStats", "CITY_STATS_PANEL", () => ctx.Statistics != null || ctx.Citizens != null, "tool:statistics"),
				Panel("chirper", "CityChirper", "CITY_CHIRPER_PANEL", () => ctx.Chirper != null),
				Panel("production", "CityProduction", "CITY_PRODUCTION_PANEL", () => ctx.EconomyUi != null && ctx.Economy != null),
				Panel("policies", "CityPolicies", "CITY_POLICIES_PANEL", () => ctx.ProgressionUi != null, "tool:policies"),
				Panel("progression", "CityProgression", "CITY_PROGRESS_PANEL", () => ctx.ProgressionUi != null),
				Panel("districts", "CityDistricts", "CITY_DISTRICTS_PANEL", () => ctx.ProgressionUi != null, "tool:districts"),
				new()
				{
					Id = "tiles",
					Hotkey = "CityTiles",
					Available = () => ctx.ProgressionUi != null && ctx.ProgressionUi.TileGridSize > 0,
					Active = () => ctx.IsToolActive("tiles"),
					OnClick = ToggleTileTool
				},
				Panel("transit", "CityTransit", "CITY_TRANSIT_PANEL", () => ctx.TransitUi != null),
				Panel("achievements", "CityAchievements", "CITY_ACHIEVEMENTS_PANEL", () => ctx.Achievements != null && ctx.Achievements.Entries.Count > 0),
				new()
				{
					Id = "advisor",
					Hotkey = "CityAdvisor",
					Available = () => manager != null,
					Active = () => !CityAdvisorLogic.Dismissed,
					OnClick = () => CityAdvisorLogic.Dismissed = !CityAdvisorLogic.Dismissed
				}
			};

			var container = widget.Get("CITY_ORDERS");
			var visible = 0;
			foreach (var spec in specs)
			{
				if (!spec.Available())
					continue;

				AddOrderButton(container, spec, visible++);
			}

			var rows = (visible + OrdersPerRow - 1) / OrdersPerRow;
			container.Get<ImageWidget>("ORDER_ROW1").IsVisible = () => rows >= 1;
			container.Get<ImageWidget>("ORDER_ROW2").IsVisible = () => rows >= 2;

			// The sidebar cap moves below the extra rows.
			var cap = widget.GetOrNull<ImageWidget>("CAP");
			if (cap != null)
				cap.Bounds.Y += rows * 48;
		}

		OrderButtonSpec Panel(string id, string hotkey, string panelId, Func<bool> available, string unlock = null)
		{
			return new OrderButtonSpec
			{
				Id = id,
				Hotkey = hotkey,
				Available = available,
				Unlock = unlock,
				Active = () => PanelOpen(panelId),
				OnClick = () => TogglePanel(panelId)
			};
		}

		void AddOrderButton(Widget container, OrderButtonSpec spec, int index)
		{
			var button = Game.LoadWidget(world, "CITY_ORDER_BUTTON", container, []) as ButtonWidget;
			button.Bounds.X = 9 + index % OrdersPerRow * 43;
			button.Bounds.Y = index / OrdersPerRow * 48 + 6;
			button.Key = Game.ModData.Hotkeys[spec.Hotkey];
			bool Locked() => spec.Unlock != null && ctx.Progression != null && !ctx.Progression.IsUnlocked(spec.Unlock);
			button.IsDisabled = () => manager == null || Locked();
			button.OnClick = spec.OnClick;

			var title = CityUi.Message("button-city-tool-" + spec.Id);
			button.GetTooltipText = () => Locked() ? title + "\n" + FluentProvider.GetMessage("label-zone-locked") : title;

			var useCity = ChromeProvider.TryGetImage("city-order-icons", spec.Id) != null;
			var image = button.Get<ImageWidget>("ICON");
			image.GetImageName = () =>
			{
				var icon = useCity ? spec.Id : "options";
				if (!useCity)
					return icon;

				if (manager == null || Locked())
					return icon + "-disabled";

				var unread = spec.Id == "chirper" && ctx.Chirper != null && ctx.Chirper.Version != seenChirps;
				if (spec.Active())
				{
					seenChirps = ctx.Chirper?.Version ?? 0;
					return icon + "-active";
				}

				// A new chirp blinks the bird.
				return unread && Game.RunTime / 500 % 2 == 0 ? icon + "-active" : icon;
			};
		}

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
