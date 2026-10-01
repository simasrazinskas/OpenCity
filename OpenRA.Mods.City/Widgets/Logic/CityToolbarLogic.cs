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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Orders;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Dune 2000 style sidebar controller: category tabs (roads, zoning, networks, the service categories, industry,
	/// transit; only the ones that have something to offer), the icon palette, the order buttons (bulldoze, info views,
	/// budget and the rows of panel buttons) and Escape handling for cancelling tools and closing panels.
	/// </summary>
	public partial class CityToolbarLogic : ChromeLogic
	{
		[FluentReference]
		const string CategoryRoad = "label-city-category-road";

		const int TabPitch = 31;

		sealed class ToolItem
		{
			public string Id;
			public string Collection;
			public string Icon;
			public string Name;
			public string Description;
			public string Cost = "";
			public string ActorType;
			public Func<IOrderGenerator> Create;

			/// <summary>Runs instead of the default activate-the-generator behaviour (mode toggles).</summary>
			public Action OnSelect;
			public Func<bool> IsActive;
			public Func<bool> IsDisabled;
			public Func<string> ExtraTooltip;
		}

		/// <summary>Tabs in sidebar order. Road and zoning always exist, the others appear when they have items.</summary>
		static readonly string[] TabOrder =
		[
			"road", "zoning", "networks", "power", "water", "police", "fire", "health", "education", "parks", "garbage", "deathcare",
			"comms", "admin", "industry", "transit"
		];

		readonly World world;
		readonly Widget widget;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		readonly UiToolState toolState;
		readonly CityPaletteWidget palette;
		readonly Widget tabContainer;
		readonly List<string> tabs = [];
		readonly Dictionary<string, List<ToolItem>> placeables = [];

		IOrderGenerator activeGenerator;
		string activeToolId;
		string selectedTab;
		int tabOffset;
		CityInfoView infoViewBeforeTool;
		bool infoViewOverridden;

		[ObjectCreator.UseCtor]
		public CityToolbarLogic(Widget widget, World world)
		{
			this.world = world;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);
			toolState = UiToolState.For(world);
			this.widget = widget;
			palette = widget.Get<CityPaletteWidget>("CITY_PALETTE");
			tabContainer = widget.Get("CITY_TABS");

			// Tools started from a panel (district painting) leave that panel open.
			ctx.ActivateTool = (id, generator) =>
			{
				CancelTool();
				Activate(id, generator);
			};

			ctx.IsToolActive = id => activeToolId == id && ToolActive();
			ctx.AnyToolActive = ToolActive;
			ctx.CancelTool = CancelTool;

			CityAdvisorLogic.OpenedBudget = CityAdvisorLogic.OpenedInfoViews = CityAdvisorLogic.Dismissed = false;
			CollectPlaceables();
			BuildTabs();

			SetupOrderButton(widget.Get<ButtonWidget>("ORDER_BULLDOZE"), "bulldoze", "repair",
				() => activeToolId == "bulldoze" && ToolActive(),
				() =>
				{
					var wasActive = activeToolId == "bulldoze" && ToolActive();
					CloseAllPanels();
					CancelTool();
					if (!wasActive)
						Activate("bulldoze", new BulldozeOrderGenerator(world));
				});

			SetupOrderButton(widget.Get<ButtonWidget>("ORDER_INFOVIEWS"), "infoviews", "beacon",
				() => PanelOpen("CITY_INFOVIEWS_PANEL"), () => TogglePanel("CITY_INFOVIEWS_PANEL"));

			SetupOrderButton(widget.Get<ButtonWidget>("ORDER_BUDGET"), "budget", "sell",
				() => PanelOpen("CITY_BUDGET_PANEL"), () => TogglePanel("CITY_BUDGET_PANEL"));

			BuildOrderRows(widget);

			widget.Get<LogicKeyListenerWidget>("CITY_KEYS").AddHandler(HandleKey);

			SelectTab("road", false);
		}

		void SetupOrderButton(ButtonWidget button, string cityIcon, string fallbackIcon, Func<bool> isActive, Action onClick)
		{
			// Use the city order tiles when the art is available, otherwise the Dune 2000 order icons.
			var useCity = ChromeProvider.TryGetImage("city-order-icons", cityIcon) != null;
			var icon = useCity ? cityIcon : fallbackIcon;

			var image = button.Get<ImageWidget>("ICON");
			image.GetImageCollection = () => useCity ? "city-order-icons" : "order-icons";
			image.GetImageName = () => manager == null ? icon + "-disabled" : isActive() ? icon + "-active" : icon;
			button.IsDisabled = () => manager == null;
			button.OnClick = onClick;
		}

		// ---- keys, tools and panels ----
		static readonly (string Tab, string Hotkey)[] TabHotkeys =
		[
			("road", "CityToolRoad"), ("zoning", "CityToolZoning"), ("networks", "CityToolNetworks"),
			("industry", "CityToolIndustry"), ("transit", "CityToolTransit")
		];

		bool HandleKey(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			if (e.Key == Keycode.ESCAPE && e.Modifiers == Modifiers.None)
			{
				var handled = AnyPanelOpen() || ToolActive() || ctx.SelectedCitizen != 0 || ctx.SelectedVehicle != 0;
				CloseAllPanels();
				CancelTool();
				ctx.SelectedCitizen = 0;
				ctx.SelectedVehicle = 0;
				ctx.FollowTarget = null;
				return handled;
			}

			if (e.Modifiers == Modifiers.None && ToolActive() && world.OrderGenerator is IUiKeyTool tool && tool.HandleKey(e))
				return true;

			// Tab flips the direction of a pending one-way road drag.
			if (e.Key == Keycode.TAB && e.Modifiers == Modifiers.None && ToolActive() && world.OrderGenerator is RoadOrderGenerator road)
			{
				road.Options.Reverse = !road.Options.Reverse;
				toolState.Road.Reverse = road.Options.Reverse;
				return true;
			}

			if (manager == null)
				return false;

			foreach (var (tab, hotkey) in TabHotkeys)
			{
				if (!tabs.Contains(tab) || !Game.ModData.Hotkeys[hotkey].IsActivatedBy(e))
					continue;

				CloseAllPanels();
				CancelTool();
				EnsureTabVisible(tab);
				SelectTab(tab, tab == "road");
				return true;
			}

			if (Game.ModData.Hotkeys["CityInfoViewNext"].IsActivatedBy(e))
				return CycleInfoView();

			return false;
		}

		void EnsureTabVisible(string tab)
		{
			var index = tabs.IndexOf(tab);
			var visible = tabs.Count > tabSlots ? tabSlots - 2 : tabs.Count;
			if (index < tabOffset || index >= tabOffset + visible)
			{
				tabOffset = index;
				LayoutTabs();
			}
		}

		/// <summary>Shift+I: the next info view that has data, wrapping around through "off".</summary>
		bool CycleInfoView()
		{
			var layer = world.WorldActor.TraitOrDefault<InfoViewLayer>();
			if (layer == null)
				return false;

			var available = InfoViews.All.Where(d => d.Available(ctx)).Select(d => d.Mode).ToList();
			var index = available.IndexOf(layer.Mode);
			layer.Mode = index + 1 >= available.Count ? CityInfoView.None : available[index + 1];
			return true;
		}

		bool ToolActive()
		{
			return activeGenerator != null && world.OrderGenerator == activeGenerator;
		}

		void CancelTool()
		{
			if (ToolActive())
				world.CancelInputMode();

			activeGenerator = null;
			activeToolId = null;
			RestoreInfoView();
		}

		void Activate(string id, IOrderGenerator generator)
		{
			world.OrderGenerator = generator;
			activeGenerator = generator;
			activeToolId = id;
			ApplyToolInfoView(id);
		}

		/// <summary>Grid tools show their network while they are active (the Networks tab auto-activates the grid view).</summary>
		void ApplyToolInfoView(string id)
		{
			var layer = world.WorldActor.TraitOrDefault<InfoViewLayer>();
			if (layer == null)
				return;

			var view = id switch
			{
				"powerline" => CityInfoView.PowerGrid,
				"pipe" => CityInfoView.WaterGrid,
				_ => CityInfoView.None
			};

			if (view == CityInfoView.None || !InfoViews.Get(view).Available(ctx))
				return;

			if (!infoViewOverridden)
			{
				infoViewBeforeTool = layer.Mode;
				infoViewOverridden = true;
			}

			layer.Mode = view;
		}

		void RestoreInfoView()
		{
			if (!infoViewOverridden)
				return;

			infoViewOverridden = false;
			var layer = world.WorldActor.TraitOrDefault<InfoViewLayer>();
			if (layer != null)
				layer.Mode = infoViewBeforeTool;
		}

		static readonly string[] PanelIds =
		[
			"CITY_BUDGET_PANEL", "CITY_INFOVIEWS_PANEL", "CITY_STATS_PANEL", "CITY_CHIRPER_PANEL", "CITY_PRODUCTION_PANEL",
			"CITY_POLICIES_PANEL", "CITY_PROGRESS_PANEL", "CITY_DISTRICTS_PANEL", "CITY_TRANSIT_PANEL", "CITY_ACHIEVEMENTS_PANEL"
		];

		bool PanelOpen(string id)
		{
			var panel = Ui.Root.GetOrNull(id);
			return panel != null && panel.IsVisible();
		}

		bool AnyPanelOpen()
		{
			return PanelIds.Any(PanelOpen);
		}

		static void CloseAllPanels()
		{
			foreach (var id in PanelIds)
			{
				var panel = Ui.Root.GetOrNull(id);
				if (panel != null)
					panel.Visible = false;
			}
		}

		void TogglePanel(string id)
		{
			var wasOpen = PanelOpen(id);
			CloseAllPanels();
			CancelTool();
			var panel = Ui.Root.GetOrNull(id);
			if (panel != null)
				panel.Visible = !wasOpen;

			// The advisor's "open the budget / info views" steps complete when the player has looked at them.
			if (!wasOpen && id == "CITY_BUDGET_PANEL")
				CityAdvisorLogic.OpenedBudget = true;
			else if (!wasOpen && id == "CITY_INFOVIEWS_PANEL")
				CityAdvisorLogic.OpenedInfoViews = true;
		}

		// ---- tabs ----
		void CollectPlaceables()
		{
			var list = new List<(string Category, int Order, string Name, ToolItem Item)>();
			foreach (var actor in world.Map.Rules.Actors.Values)
			{
				if (actor.Name.StartsWith('^'))
					continue;

				var placeable = actor.TraitInfoOrDefault<CityPlaceableInfo>();
				if (placeable == null || string.IsNullOrEmpty(placeable.Category))
					continue;

				list.Add((placeable.Category, placeable.DisplayOrder, actor.Name, MakePlaceableItem(actor, placeable)));
			}

			foreach (var group in list.GroupBy(i => i.Category))
				placeables[group.Key] = group.OrderBy(i => i.Order).ThenBy(i => i.Name, StringComparer.Ordinal).Select(i => i.Item).ToList();
		}

		bool TabHasItems(string tab)
		{
			switch (tab)
			{
				case "road":
				case "zoning":
					return true;
				case "networks":
					return UtilityToolsAvailable || placeables.ContainsKey(tab);
				case "transit":
					return TransitToolsAvailable || placeables.ContainsKey(tab);
				default:
					return placeables.ContainsKey(tab);
			}
		}

		void BuildTabs()
		{
			foreach (var tab in TabOrder)
				if (TabHasItems(tab))
					tabs.Add(tab);

			// Categories the UI does not know yet still get a tab, after the known ones.
			foreach (var category in placeables.Keys.OrderBy(k => k, StringComparer.Ordinal))
				if (!tabs.Contains(category))
					tabs.Add(category);

			LayoutTabs();
		}

		void LayoutTabs()
		{
			tabContainer.RemoveChildren();
			var overflow = tabs.Count > tabSlots;
			var visible = overflow ? tabSlots - 2 : tabs.Count;
			tabOffset = Math.Clamp(tabOffset, 0, Math.Max(0, tabs.Count - visible));

			var slot = 0;
			if (overflow)
				AddArrowTab(slot++, "up", () => tabOffset > 0, () => { tabOffset--; LayoutTabs(); });

			for (var i = 0; i < visible; i++)
				AddTab(slot++, tabs[tabOffset + i]);

			if (overflow)
				AddArrowTab(slot, "down", () => tabOffset < tabs.Count - visible, () => { tabOffset++; LayoutTabs(); });
		}

		void AddArrowTab(int slot, string icon, Func<bool> enabled, Action onClick)
		{
			var button = Game.LoadWidget(world, "CITY_TAB_BUTTON", tabContainer, []) as ButtonWidget;
			button.Bounds.Y = slot * TabPitch;
			button.IsDisabled = () => !enabled();
			button.OnClick = onClick;
			SetTabIcon(button, icon);
		}

		static void SetTabIcon(ButtonWidget button, string icon)
		{
			var image = button.Get<ImageWidget>("ICON");
			var collection = "city-icons-small";
			var name = icon;
			var sprite = ChromeProvider.TryGetImage(collection, name);
			if (sprite == null)
			{
				collection = "city-icons";
				sprite = ChromeProvider.TryGetImage(collection, name);
			}

			if (sprite == null)
			{
				collection = "city-icons-small";
				name = "services";
				sprite = ChromeProvider.TryGetImage(collection, name);
			}

			if (sprite == null)
				return;

			image.ImageCollection = collection;
			image.ImageName = name;
			image.Bounds.X = (button.Bounds.Width - (int)sprite.Size.X) / 2;
			image.Bounds.Y = (button.Bounds.Height - (int)sprite.Size.Y) / 2;
		}

		void AddTab(int slot, string tab)
		{
			var button = Game.LoadWidget(world, "CITY_TAB_BUTTON", tabContainer, []) as ButtonWidget;
			button.Bounds.Y = slot * TabPitch;
			button.IsDisabled = () => manager == null;
			button.IsHighlighted = () => selectedTab == tab;
			SetTabIcon(button, tab);

			var title = CityUi.Message("button-city-tool-" + tab, CityUi.Message("label-city-category-" + tab));
			button.GetTooltipText = () => title;
			button.OnClick = () =>
			{
				CloseAllPanels();
				CancelTool();
				SelectTab(tab, tab == "road");
			};
		}

		void SelectTab(string tab, bool activateSingleTool)
		{
			selectedTab = tab;
			var items = ItemsFor(tab);
			palette.SetItems(items.Select(MakePaletteItem));

			if (activateSingleTool && items.Count == 1)
				Activate(items[0].Id, items[0].Create());
			else if (activateSingleTool && tab == "road")
				StartRoadTool();
		}

		// ---- palette ----
		string LockedText(ToolItem item, CityPlaceableInfo placeable)
		{
			if (placeable == null || manager == null || manager.IsUnlocked(item.ActorType) || IsSignature(item.ActorType))
				return "";

			return "\n" + FluentProvider.GetMessage(UnlocksAt, "population", placeable.UnlockPopulation.ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
		}

		PaletteItem MakePaletteItem(ToolItem item)
		{
			var placeable = item.ActorType != null ? world.Map.Rules.Actors[item.ActorType].TraitInfoOrDefault<CityPlaceableInfo>() : null;

			bool Locked() => manager == null || (item.ActorType != null && !manager.IsUnlocked(item.ActorType)) || (item.IsDisabled?.Invoke() ?? false);
			bool Active() => item.IsActive != null ? item.IsActive() : activeToolId == item.Id && ToolActive();

			return new PaletteItem
			{
				Collection = item.Collection,
				Icon = item.Icon,
				CostText = item.Cost,
				IsDisabled = Locked,
				IsAffordable = () => placeable == null || manager == null || manager.CanAfford(placeable.Cost),
				IsActive = Active,
				GetTooltip = () => item.Name + "\n" + item.Description + LockedText(item, placeable) + (item.ExtraTooltip != null ? "\n" + item.ExtraTooltip() : ""),
				OnClick = () =>
				{
					if (item.OnSelect != null)
					{
						item.OnSelect();
						return;
					}

					if (activeToolId == item.Id && ToolActive())
						CancelTool();
					else
					{
						CancelTool();
						Activate(item.Id, item.Create());
					}
				}
			};
		}
	}
}
