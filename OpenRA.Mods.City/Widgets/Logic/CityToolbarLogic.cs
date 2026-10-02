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
	/// RCT2-style HUD controller: the top toolbar (.Hud.cs), the build window with the tools and buildings of every
	/// category (.Build.cs, items in .Items.cs and .Signatures.cs), the panel windows it toggles, the map tile tool
	/// (.Orders.cs) and the keyboard: tool hotkeys, Escape cancels tools and closes windows.
	/// </summary>
	public partial class CityToolbarLogic : ChromeLogic
	{
		[FluentReference]
		const string CategoryRoad = "label-city-category-road";

		sealed class ToolItem
		{
			public string Id;

			/// <summary>RCT2 icon name (tools/iso_ui_icons_*.py).</summary>
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

		/// <summary>Build categories in toolbar order. Road and zoning always exist, the others appear when they have items.</summary>
		static readonly string[] TabOrder =
		[
			"road", "zoning", "networks", "power", "water", "police", "fire", "health", "education", "parks", "garbage", "deathcare",
			"comms", "admin", "industry", "transit"
		];

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		readonly UiToolState toolState;
		readonly List<string> tabs = [];
		readonly Dictionary<string, List<ToolItem>> placeables = [];

		IOrderGenerator activeGenerator;
		string activeToolId;
		CityInfoView infoViewBeforeTool;
		bool infoViewOverridden;

		[ObjectCreator.UseCtor]
		public CityToolbarLogic(Widget widget, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);
			toolState = UiToolState.For(world);

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
			InitBuildWindow();
			InitToolbar(widget);

			widget.Get<LogicKeyListenerWidget>("CITY_KEYS").AddHandler(HandleKey);
		}

		public override void Tick()
		{
			TickToolbar();

			// Remember the category of the active tool so the build window opens there again.
			if (BuildOpen && laidOutBuildVersion != laidOutVersion)
			{
				laidOutBuildVersion = laidOutVersion;
				SizeBuildWindow();
			}
		}

		int laidOutBuildVersion = -1;

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
				if (moreOpen)
				{
					moreOpen = false;
					return true;
				}

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

				CancelTool();
				OpenBuild(tab, tab == "road");
				return true;
			}

			if (Game.ModData.Hotkeys["CityInfoViewNext"].IsActivatedBy(e))
				return CycleInfoView();

			return HandleOverflowHotkey(e);
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
			toolFromBuild = false;
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

		/// <summary>Every window the toolbar opens (Escape closes them all).</summary>
		public static readonly string[] PanelIds =
		[
			"CITY_BUILD_PANEL", "CITY_BUDGET_PANEL", "CITY_INFOVIEWS_PANEL", "CITY_STATS_PANEL", "CITY_CHIRPER_PANEL",
			"CITY_PRODUCTION_PANEL", "CITY_POLICIES_PANEL", "CITY_PROGRESS_PANEL", "CITY_DISTRICTS_PANEL", "CITY_TRANSIT_PANEL",
			"CITY_ACHIEVEMENTS_PANEL", "CITY_MAP_PANEL", "CITY_INFO_PANEL", "CITY_NOTIFICATIONS_PANEL"
		];

		static bool PanelOpen(string id)
		{
			var panel = Ui.Root.GetOrNull(id);
			return panel != null && panel.IsVisible();
		}

		static bool AnyPanelOpen()
		{
			return PanelIds.Any(PanelOpen);
		}

		void CloseAllPanels()
		{
			CloseBuild();
			foreach (var id in PanelIds)
			{
				var panel = Ui.Root.GetOrNull(id);
				if (panel != null)
					panel.Visible = false;
			}
		}

		/// <summary>Opens or closes one window; other windows stay open (RCT2: several windows at once).</summary>
		static void TogglePanel(string id)
		{
			var panel = Ui.Root.GetOrNull(id);
			if (panel == null)
				return;

			var wasOpen = panel.IsVisible();
			panel.Visible = !wasOpen;
			if (!wasOpen && panel is CityPanelWidget window)
				window.BringToFront();

			// The advisor's "open the budget / info views" steps complete when the player has looked at them.
			if (!wasOpen && id == "CITY_BUDGET_PANEL")
				CityAdvisorLogic.OpenedBudget = true;
			else if (!wasOpen && id == "CITY_INFOVIEWS_PANEL")
				CityAdvisorLogic.OpenedInfoViews = true;
		}

		// ---- categories ----
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
		}

		// ---- build cards ----
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
				Icon = item.Icon,
				Thumbnail = item.ActorType,
				Name = item.Name,
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
						toolFromBuild = true;
						return;
					}

					if (activeToolId == item.Id && ToolActive())
						CancelTool();
					else
					{
						CancelTool();
						Activate(item.Id, item.Create());
						toolFromBuild = true;
					}
				}
			};
		}
	}
}
