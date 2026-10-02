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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The Upkeep page: the service budget slider (shared by all buildings of the service), the in-place upgrades with their
	/// price, the district restriction of service buildings and the production choices of extractor hubs. Built once per
	/// selected building; every control issues the existing order.
	/// </summary>
	public partial class CityBuildingInfoLogic
	{
		readonly Widget upkeepContent;
		bool upkeepAvailable;

		bool HasUpkeepPage() { return building != null && upkeepAvailable; }

		Widget AddLine(int y)
		{
			var row = Game.LoadWidget(world, "CITY_BUILDING_LINE", upkeepContent, []);
			row.Bounds.Y = y;
			row.Bounds.Width = upkeepContent.Bounds.Width;
			row.Get("HEADER").Visible = false;
			return row;
		}

		int AddHeaderLine(int y, string text)
		{
			var row = AddLine(y);
			var header = row.Get<CityHeaderWidget>("HEADER");
			header.Visible = true;
			header.GetText = () => text;
			row.Get("NAME").Visible = false;
			row.Get("BAR").Visible = false;
			row.Get("VALUE").Visible = false;
			return LinePool.HeaderHeight;
		}

		ButtonWidget AddTextButton(int x, int y, int width, int height, string text, Action click)
		{
			var button = (ButtonWidget)Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", upkeepContent, []);
			button.Bounds = new WidgetBounds(x, y, width, height);
			button.GetText = () => text;
			button.OnClick = click;
			return button;
		}

		void BuildUpkeepPage()
		{
			upkeepAvailable = false;
			var width = upkeepContent.Bounds.Width;
			var y = 0;

			var service = actor.Info.TraitInfoOrDefault<ServiceBuildingInfo>();
			if (service != null && ctx.Services != null)
			{
				upkeepAvailable = true;
				var kind = service.Kind;
				y += AddHeaderLine(y, Msg("label-inspect-header-budget"));
				var name = CityUi.Message("label-service-" + kind.ToString().ToLowerInvariant(), kind.ToString());
				var budget = CityRows.AddSlider(world, upkeepContent, y, name, Color.White, 50, 150, 5,
					() => ctx.Services.GetBudget(kind),
					value =>
					{
						if (world.LocalPlayer != null)
							world.IssueOrder(UiOrders.ServiceBudget(world.LocalPlayer, kind, value));
					},
					PercentText, () => world.LocalPlayer == null, width, 90, 34);

				budget.Row.Get("BG").Bounds.Width = width;
				var tip = name + "\n" + FluentProvider.GetMessage("label-inspect-budget-desc", "name", name);
				budget.Name.GetTooltipText = () => tip;
				y += 18;

				var upkeep = building.Info.Upkeep;
				if (upkeep > 0)
				{
					var row = AddLine(y);
					row.Get("BAR").Visible = false;
					row.Get<LabelWidget>("NAME").GetText = () => Msg("label-building-upkeep");
					var value = row.Get<LabelWidget>("VALUE");
					value.Bounds.X = 100;
					value.Bounds.Width = width - 100;
					value.GetText = () => FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(-(building.Info.Upkeep + UpgradeUpkeep())));
					value.GetColor = () => CityTheme.MoneyNegative;
					y += LinePool.ValueHeight + 2;
				}
			}

			y = BuildUpgrades(y, width);
			y = BuildDistrictRestriction(y, width);
			BuildHubProduction(y, width);
		}

		/// <summary>One buy button per in-place upgrade of the selected building (SVC, design 07 section 3.9).</summary>
		int BuildUpgrades(int y, int width)
		{
			var upgrades = ctx.Upgrades?.UpgradesOf(actor);
			if (upgrades == null || upgrades.Count == 0)
				return y;

			upkeepAvailable = true;
			y += AddHeaderLine(y, Msg("label-inspect-header-upgrades"));
			var target = actor;
			for (var i = 0; i < upgrades.Count; i++)
			{
				var index = i;
				var entry = upgrades[i];
				var button = (ButtonWidget)Game.LoadWidget(world, "CITY_BUILDING_UPGRADE", upkeepContent, []);
				button.Bounds = new WidgetBounds(0, y, width, 20);
				y += 22;

				var nameText = CityUi.Message(entry.NameKey);
				var name = button.Get<LabelWidget>("NAME");
				name.GetText = CityUi.Fitted(name, () => nameText);
				var cost = button.Get<LabelWidget>("COST");
				var price = CityUtils.FormatMoney(entry.Cost);
				var built = Msg("label-inspect-upgrade-built");
				bool Owned() => ctx.Upgrades.UpgradesOf(target)[index].Owned;
				cost.GetText = () => Owned() ? built : price;
				cost.GetColor = () => Owned() ? CityTheme.MoneyPositive : CityTheme.Ink;

				var tip = nameText;
				if (entry.UpkeepPerMonth > 0)
					tip += "\n" + FluentProvider.GetMessage("label-inspect-upgrade-upkeep-desc", "amount", CityUtils.FormatMoney(entry.UpkeepPerMonth));

				button.GetTooltipText = () => tip;
				button.IsDisabled = () => Owned() || world.LocalPlayer == null || (manager != null && !manager.CanAfford(entry.Cost));
				button.IsHighlighted = Owned;
				button.OnClick = () => world.IssueOrder(UiOrders.Upgrade(world.LocalPlayer, target, index));
			}

			return y + 2;
		}

		/// <summary>
		/// Service buildings can be restricted to some districts: a button per district toggles its bit in the building's mask
		/// (0 = serves the whole city, which the "All" button restores). Shown only when districts exist.
		/// </summary>
		int BuildDistrictRestriction(int y, int width)
		{
			var services = ctx.Get<ServiceSimulation>();
			var districts = ctx.ProgressionUi?.Districts;
			if (!HasDistricts || services == null || districts == null)
				return y;

			upkeepAvailable = true;
			var target = actor;
			y += AddHeaderLine(y, Msg("label-service-districts"));

			var all = AddTextButton(0, y, 40, 14, Msg("label-service-districts-all"), () =>
				world.IssueOrder(UiOrders.ServiceDistricts(world.LocalPlayer, target, 0)));

			all.IsHighlighted = () => services.GetDistrictMask(target) == 0;
			all.IsDisabled = () => world.LocalPlayer == null;

			var shown = Math.Min(districts.Count, 5);
			var step = (width - 44) / Math.Max(1, shown);
			for (var i = 0; i < shown; i++)
			{
				var id = districts[i].Id;
				var name = string.IsNullOrEmpty(districts[i].Name) ? "#" + id : districts[i].Name;
				var button = AddTextButton(44 + i * step, y, step - 2, 14, name, () =>
				{
					var mask = services.GetDistrictMask(target) ^ (1 << id);
					world.IssueOrder(UiOrders.ServiceDistricts(world.LocalPlayer, target, mask));
				});

				var font = Game.Renderer.Fonts[button.Font];
				button.GetText = () => WidgetUtils.TruncateText(name, button.Bounds.Width - 6, font);
				button.IsHighlighted = () => (services.GetDistrictMask(target) >> id & 1) != 0;
				button.IsDisabled = () => world.LocalPlayer == null;
			}

			return y + 18;
		}

		/// <summary>Forestry hubs can fell every tree at once; hubs with a choice of products (the farm) get one button per product.</summary>
		void BuildHubProduction(int y, int width)
		{
			var hub = Hub;
			if (hub == null)
				return;

			var hubActor = actor;
			var started = false;
			void Start()
			{
				if (started)
					return;

				started = true;
				upkeepAvailable = true;
				y += AddHeaderLine(y, Msg("label-inspect-header-production"));
			}

			if (hub.Info.Kind == NaturalResourceKind.Forest)
			{
				Start();
				var clearcut = AddTextButton(0, y, width, 16, Msg("label-hub-clearcut"), () =>
				{
					if (world.LocalPlayer != null)
						world.IssueOrder(IndustryOrders.SetPolicyOrder(world.LocalPlayer, hubActor, "clearcut", !hub.ClearCut));
				});

				clearcut.IsHighlighted = () => hub.ClearCut;
				clearcut.IsDisabled = () => world.LocalPlayer == null;
				y += 18;
			}

			if (!hub.HasProductChoice)
				return;

			Start();
			var products = hub.Info.Products;
			var half = width / 2;
			for (var i = 0; i < products.Length; i++)
			{
				var product = products[i];
				var button = AddTextButton(i % 2 * half, y + i / 2 * 18, half - 2, 16, CityUi.Message("label-resource-" + product.ToLowerInvariant()), () =>
				{
					if (world.LocalPlayer != null)
						world.IssueOrder(UiOrders.HubProduct(world.LocalPlayer, hubActor, product));
				});

				button.IsHighlighted = () => string.Equals(hub.Product, product, StringComparison.OrdinalIgnoreCase);
				button.IsDisabled = () => world.LocalPlayer == null;
			}
		}
	}
}
