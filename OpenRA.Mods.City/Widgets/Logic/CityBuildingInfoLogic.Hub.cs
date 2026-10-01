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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The action buttons of the building panel: extractor hub areas and the farm crop selector (design 06 section 3.2).</summary>
	public partial class CityBuildingInfoLogic
	{
		[FluentReference]
		const string AreaPaint = "label-tool-area-paint";

		[FluentReference]
		const string AreaClear = "label-tool-area-clear";

		[FluentReference("count")]
		const string AreaCells = "label-tool-cells-count";

		readonly Widget actions;
		int actionsHeight;

		ExtractorHub Hub => actor?.TraitOrDefault<ExtractorHub>();

		void BuildActions()
		{
			if (world.LocalPlayer == null)
				return;

			BuildUpgrades();
			BuildDistrictRestriction();
			var hub = Hub;
			if (hub == null)
				return;

			var hubActor = actor;
			var top = actionsHeight / 34;
			AddAction(0, top, AreaPaint, () => StartArea(hubActor, true));
			AddAction(1, top, AreaClear, () => StartArea(hubActor, false));
			actionsHeight += 34;

			// Forestry hubs can fell every tree of their area at once for more wood (and a bare landscape).
			if (hub.Info.Kind == NaturalResourceKind.Forest)
			{
				var clearcut = AddAction(0, top + 1, "label-hub-clearcut", () =>
				{
					if (world.LocalPlayer != null)
						world.IssueOrder(IndustryOrders.SetPolicyOrder(world.LocalPlayer, hubActor, "clearcut", !hub.ClearCut));
				});

				clearcut.Bounds.Width = 252;
				clearcut.IsHighlighted = () => hub.ClearCut;
				actionsHeight += 34;
			}

			// Hubs with a choice of products (the farm: grain, vegetables, livestock, cotton) get one button per product.
			if (!hub.HasProductChoice)
				return;

			var products = hub.Info.Products;
			for (var i = 0; i < products.Length; i++)
			{
				var product = products[i];
				var button = AddAction(i % 2, top + 1 + i / 2, "label-resource-" + product.ToLowerInvariant(), () =>
				{
					if (world.LocalPlayer != null)
						world.IssueOrder(UiOrders.HubProduct(world.LocalPlayer, hubActor, product));
				});

				button.IsHighlighted = () => string.Equals(hub.Product, product, StringComparison.OrdinalIgnoreCase);
			}

			actionsHeight += 34 * ((products.Length + 1) / 2);
		}

		/// <summary>One buy button per in-place upgrade of the selected building (SVC, design 07 section 3.9).</summary>
		void BuildUpgrades()
		{
			var upgrades = ctx.Upgrades?.UpgradesOf(actor);
			if (upgrades == null || upgrades.Count == 0)
				return;

			var target = actor;
			for (var i = 0; i < upgrades.Count; i++)
			{
				var index = i;
				var entry = upgrades[i];
				var text = CityUi.Message(entry.NameKey) + "  " + CityUtils.FormatMoney(entry.Cost);
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", actions, []) as ButtonWidget;
				button.Bounds.Y = i * 34;
				button.Bounds.Width = 256;
				button.Bounds.Height = 28;
				button.GetText = () => text;
				button.IsDisabled = () => ctx.Upgrades.UpgradesOf(target)[index].Owned || (manager != null && !manager.CanAfford(entry.Cost));
				button.IsHighlighted = () => ctx.Upgrades.UpgradesOf(target)[index].Owned;
				button.OnClick = () => world.IssueOrder(UiOrders.Upgrade(world.LocalPlayer, target, index));
			}

			actionsHeight = upgrades.Count * 34;
		}

		/// <summary>
		/// Service buildings can be restricted to some districts: a button per district toggles its bit in the building's mask
		/// (0 = serves the whole city, which the "All" button restores). Shown only when districts exist.
		/// </summary>
		void BuildDistrictRestriction()
		{
			var services = ctx.Get<ServiceSimulation>();
			var districts = ctx.ProgressionUi?.Districts;
			if (services == null || districts == null || districts.Count == 0 || actor.Info.TraitInfoOrDefault<ServiceBuildingInfo>() == null)
				return;

			var target = actor;
			var top = actionsHeight / 34;
			var label = new LabelWidget(Game.ModData)
			{
				Bounds = new WidgetBounds(0, top * 34, 256, 18),
				Font = "TinyBold",
				GetText = () => CityUi.Message("label-service-districts"),
				GetColor = () => CityUi.Muted
			};

			actions.AddChild(label);
			var all = AddAction(0, top, "label-service-districts-all", () =>
				world.IssueOrder(UiOrders.ServiceDistricts(world.LocalPlayer, target, 0)));

			all.Bounds.Y = top * 34 + 20;
			all.Bounds.Width = 56;
			all.IsHighlighted = () => services.GetDistrictMask(target) == 0;

			var shown = Math.Min(districts.Count, 5);
			for (var i = 0; i < shown; i++)
			{
				var id = districts[i].Id;
				var name = string.IsNullOrEmpty(districts[i].Name) ? "#" + id : districts[i].Name;
				var button = AddAction(0, top, "label-service-districts-all", () =>
				{
					var mask = services.GetDistrictMask(target) ^ (1 << id);
					world.IssueOrder(UiOrders.ServiceDistricts(world.LocalPlayer, target, mask));
				});

				button.GetText = () => name.Length > 6 ? name[..6] : name;
				button.Bounds.X = 60 + i * 40;
				button.Bounds.Y = top * 34 + 20;
				button.Bounds.Width = 38;
				button.IsHighlighted = () => (services.GetDistrictMask(target) >> id & 1) != 0;
			}

			actionsHeight += 68;
		}

		ButtonWidget AddAction(int column, int row, string key, Action click)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", actions, []) as ButtonWidget;
			button.Bounds.X = column * 128;
			button.Bounds.Y = row * 34;
			button.Bounds.Width = 124;
			button.Bounds.Height = 28;
			var text = CityUi.Message(key);
			button.GetText = () => text;
			button.OnClick = click;
			return button;
		}

		void StartArea(Actor hub, bool add)
		{
			ctx.ActivateTool(add ? "area-paint" : "area-clear", new UiAreaToolGenerator(world,
				(p, a, b) => UiOrders.HubArea(p, hub, a, b, add),
				add ? Color.FromArgb(110, 120, 220, 90) : Color.FromArgb(120, 255, 150, 30),
				(a, b) => FluentProvider.GetMessage(AreaCells, "count", CityUtils.Rect(a, b).Count())));
		}
	}
}
