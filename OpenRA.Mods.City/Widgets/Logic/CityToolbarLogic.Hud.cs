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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 top toolbar (design/iso/ui/screens/hud-*.png, tools/iso_ui_hud.py): three groups of icon-button strips.
	/// Left: game menu, save, settings | pause and speeds | camera (zoom, see-through, map, photo). Centre: the build
	/// categories (every category on wide screens, grouped on narrower ones) and the bulldozer. Right: the panels.
	/// Tiers by logical width: wide >= 1600, medium >= 1100, compact below; whatever still does not fit goes into a
	/// "More" popup (lowest priority first). Laid out again whenever the logical window size changes.
	/// </summary>
	public partial class CityToolbarLogic
	{
		const int ButtonWidth = 30;
		const int ButtonHeight = 27;
		const int StripHeight = 31;
		const int GroupGap = 2;
		const int SideGap = 8;

		/// <summary>Logical height the toolbar occupies at the top of the screen.</summary>
		public const int ToolbarHeight = StripHeight;

		sealed class HudButton
		{
			public string Id;
			public string Icon;
			public string Hotkey;
			public Func<string> Tooltip = () => null;
			public Func<bool> Available = () => true;
			public string Unlock;
			public Func<bool> Active = () => false;
			public Action OnClick = () => { };

			/// <summary>Overflow order: buttons with the lowest priority move into "More" first.</summary>
			public int Priority;
			public ButtonWidget Widget;
			public bool InMore;
		}

		sealed class HudGroup
		{
			public string Family;
			public List<HudButton> Buttons = [];
			public BackgroundWidget Frame;
		}

		Widget toolbar;
		ButtonWidget menuButton;
		Widget menuContainer;
		HudButton moreButton;
		BackgroundWidget morePopup;
		bool moreOpen;
		readonly Dictionary<string, HudButton> hudButtons = [];
		readonly List<HudGroup> shownGroups = [];
		int laidOutVersion = -1;
		int seenChirps;
		bool transientsHooked;

		HudButton Hud(string id, string icon, Action onClick, Func<bool> active = null, string hotkey = null, int priority = 50,
			Func<bool> available = null, string unlock = null, string tooltipKey = null)
		{
			if (hudButtons.TryGetValue(id, out var existing))
				return existing;

			var title = CityUi.Message(tooltipKey ?? "button-city-toolbar-" + id);
			var b = new HudButton
			{
				Id = id,
				Icon = icon,
				Hotkey = hotkey,
				OnClick = onClick,
				Active = active ?? (() => false),
				Priority = priority,
				Available = available ?? (() => true),
				Unlock = unlock
			};

			b.Tooltip = () =>
			{
				var text = title;
				if (hotkey != null)
				{
					var key = Game.ModData.Hotkeys[hotkey].GetValue();
					if (key.IsValid())
						text += " (" + key.DisplayString() + ")";
				}

				return Locked(b) ? text + "\n" + FluentProvider.GetMessage("label-zone-locked") : text;
			};

			hudButtons[id] = b;
			return b;
		}

		bool Locked(HudButton b) => b.Unlock != null && ctx.Progression != null && !ctx.Progression.IsUnlocked(b.Unlock);

		HudButton PanelButton(string id, string icon, string panelId, string hotkey, int priority, Func<bool> available, string unlock = null)
		{
			return Hud(id, icon, () => TogglePanel(panelId), () => PanelOpen(panelId), hotkey, priority, available, unlock);
		}

		void InitToolbar(Widget root)
		{
			toolbar = root.Get("CITY_TOOLBAR");
			menuContainer = toolbar.Get("MENU_BUTTONS");
			menuButton = menuContainer.Get<ButtonWidget>("OPTIONS_BUTTON");

			// Left: game
			Hud("menu", "pnl_game_menu", null, priority: 100);
			Hud("save", "pnl_save", OpenSave, priority: 30, available: () => world.Type == WorldType.Regular && world.LobbyInfo.NonBotClients.Count() == 1);
			Hud("settings", "pnl_settings", OpenSettings, priority: 40);

			// Left: speed (button ids kept for scripted UI tours)
			Hud("SPEED_PAUSE", "time_pause", () => world.SetPauseState(!world.PredictedPaused), () => world.PredictedPaused, "CityPause", 99,
				tooltipKey: "button-city-speed-pause-tooltip");
			for (var i = 1; i <= 3; i++)
			{
				var speed = i;
				Hud("SPEED_" + i, speed == 1 ? "time_play" : speed == 2 ? "time_fast" : "time_fastest", () =>
				{
					if (world.LocalPlayer == null)
						return;

					world.IssueOrder(CityOrders.SetSpeedOrder(world.LocalPlayer, speed));
					if (world.PredictedPaused)
						world.SetPauseState(false);
				}, () => manager != null && !world.PredictedPaused && manager.Speed == speed, "CitySpeed" + speed, 98, () => manager != null,
				tooltipKey: "button-city-speed-" + speed + "-tooltip");
			}

			// Left: camera
			Hud("zoomout", "ui_zoom_out", () => worldRenderer.Viewport.AdjustZoom(-1), priority: 20);
			Hud("zoomin", "ui_zoom_in", () => worldRenderer.Viewport.AdjustZoom(1), priority: 21);
			Hud("seethrough", "ui_eye_off", CityView.ToggleSeeThrough, () => CityView.SeeThrough, CityView.SeeThroughHotkey, 35);
			PanelButton("map", "pnl_minimap", "CITY_MAP_PANEL", "CityMap", 45, () => true);
			Hud("photo", "pnl_photo", Game.TakeScreenshot, priority: 10);

			// Centre: build categories and the bulldozer
			foreach (var category in TabOrder.Concat(placeables.Keys.Where(k => !TabOrder.Contains(k)).OrderBy(k => k, StringComparer.Ordinal)))
			{
				if (!TabHasItems(category))
					continue;

				var cat = category;
				Hud("cat-" + cat, CategoryIcon(cat), () => ToggleBuild(cat), () => BuildOpenFor(cat), CategoryHotkey(cat), 90,
					tooltipKey: "button-city-tool-" + cat);
			}

			foreach (var group in BuildGroups)
			{
				var (name, icon, _, categories) = group;
				if (!categories.Any(TabHasItems))
					continue;

				Hud("group-" + name, icon, () => ToggleBuild(categories.First(TabHasItems)), () => BuildOpenForGroup(name), null, 90,
					tooltipKey: "button-city-build-group-" + name);
			}

			Hud("bulldoze", "tool_bulldoze", ToggleBulldoze, () => activeToolId == "bulldoze" && ToolActive(), "CityToolBulldoze", 97,
				() => manager != null, tooltipKey: "button-city-tool-bulldoze");

			// Right: panels
			PanelButton("infoviews", "pnl_infoviews", "CITY_INFOVIEWS_PANEL", "CityInfoViews", 95, () => true);
			PanelButton("budget", "pnl_budget", "CITY_BUDGET_PANEL", "CityBudget", 94, () => manager != null);
			PanelButton("stats", "pnl_stats", "CITY_STATS_PANEL", "CityStats", 80, () => ctx.Statistics != null || ctx.Citizens != null, "tool:statistics");
			PanelButton("cityinfo", "pnl_city_info", "CITY_INFO_PANEL", "CityInfo", 55, () => manager != null);
			PanelButton("policies", "pnl_policies", "CITY_POLICIES_PANEL", "CityPolicies", 75, () => ctx.ProgressionUi != null, "tool:policies");
			PanelButton("districts", "pnl_districts", "CITY_DISTRICTS_PANEL", "CityDistricts", 60, () => ctx.ProgressionUi != null, "tool:districts");
			PanelButton("progression", "pnl_progression", "CITY_PROGRESS_PANEL", "CityProgression", 78, () => ctx.ProgressionUi != null);
			PanelButton("transit", "pnl_transit_lines", "CITY_TRANSIT_PANEL", "CityTransit", 70, () => ctx.TransitUi != null);
			PanelButton("production", "pnl_production", "CITY_PRODUCTION_PANEL", "CityProduction", 50, () => ctx.EconomyUi != null && ctx.Economy != null);
			Hud("tiles", "pnl_tiles", ToggleTileTool, () => ctx.IsToolActive("tiles"), "CityTiles", 52,
				() => ctx.ProgressionUi != null && ctx.ProgressionUi.TileGridSize > 0);
			PanelButton("chirper", "pnl_chirper", "CITY_CHIRPER_PANEL", "CityChirper", 85, () => ctx.Chirper != null);
			Hud("advisor", "pnl_advisor", () => CityAdvisorLogic.Dismissed = !CityAdvisorLogic.Dismissed, () => !CityAdvisorLogic.Dismissed,
				"CityAdvisor", 54, () => manager != null);
			Hud("achievements", "pnl_achievements", CityProgressLogic.ToggleAchievements, CityProgressLogic.AchievementsOpen, "CityAchievements", 58,
				() => ctx.Achievements != null && ctx.Achievements.Entries.Count > 0);
			PanelButton("notifications", "pnl_notifications", "CITY_NOTIFICATIONS_PANEL", "CityNotifications", 88, () => true);

			moreButton = Hud("more", "ui_list", () => moreOpen = !moreOpen, () => moreOpen, priority: 1000);

			foreach (var b in hudButtons.Values)
				if (b.Id != "menu")
					CreateButtonWidget(b);

			hudButtons["menu"].Widget = menuButton;

			morePopup = new BackgroundWidget { Background = "city-window", Visible = false };
			morePopup.IsVisible = () => moreOpen && morePopup.Children.Count > 0;

			// The popup hangs below the toolbar, over the windows: it lives in the HUD root, not in the toolbar strip.
			toolbar.Parent.AddChild(morePopup);
		}

		void CreateButtonWidget(HudButton b)
		{
			var button = (ButtonWidget)Game.LoadWidget(world, "CITY_TOOL_BUTTON", toolbar, []);
			button.Id = b.Id.StartsWith("SPEED_", StringComparison.Ordinal) ? b.Id : "TOOLBAR_" + b.Id.ToUpperInvariant().Replace('-', '_');
			button.GetTooltipText = b.Tooltip;
			button.IsDisabled = () => Locked(b) || !b.Available();
			button.IsHighlighted = () => b.Active();
			button.OnClick = () =>
			{
				if (b.InMore)
					moreOpen = false;

				b.OnClick();
			};

			if (b.Hotkey != null)
				button.Key = Game.ModData.Hotkeys[b.Hotkey];

			var icon = button.Get<CityIconWidget>("ICON");
			icon.Icon = b.Icon;
			if (b.Id == "chirper")
			{
				// A new chirp blinks the bird until the chirper is opened.
				icon.GetIcon = () =>
				{
					if (b.Active())
						seenChirps = ctx.Chirper?.Version ?? 0;

					var unread = ctx.Chirper != null && ctx.Chirper.Version != seenChirps && !b.Active();
					return unread && Game.RunTime / 500 % 2 == 0 ? "ui_like" : b.Icon;
				};
			}

			b.Widget = button;
		}

		/// <summary>Build groups: the build window shows one group, with a tab per category (roads and transit are split further).</summary>
		static readonly (string Name, string Icon, string Family, string[] Categories)[] BuildGroups =
		[
			("roads", "cat_roads", "city", ["road"]),
			("zoning", "cat_zoning", "zoning", ["zoning"]),
			("utilities", "cat_power", "city", ["networks", "power", "water", "garbage"]),
			("services", "cat_health", "services", ["police", "fire", "health", "deathcare", "education", "comms", "admin"]),
			("parks", "cat_parks", "services", ["parks"]),
			("transit", "cat_transit", "transit", ["transit"]),
			("industry", "cat_industry", "zoning", ["industry"]),
			("signature", "cat_signature", "people", ["signature"])
		];

		static string CategoryIcon(string category)
		{
			return category switch
			{
				"road" => "cat_roads",
				_ => "cat_" + category
			};
		}

		static string CategoryHotkey(string category)
		{
			foreach (var (tab, hotkey) in TabHotkeys)
				if (tab == category)
					return hotkey;

			return null;
		}

		static string GroupOf(string category)
		{
			foreach (var (name, _, _, categories) in BuildGroups)
				if (categories.Contains(category))
					return name;

			return "signature";
		}

		/// <summary>The groups of the current tier, left to right.</summary>
		List<(string Side, string Family, string[] Ids)> TierGroups(string tier)
		{
			var groups = new List<(string, string, string[])>
			{
				("left", "city", ["menu", "save", "settings"]),
				("left", "city", ["SPEED_PAUSE", "SPEED_1", "SPEED_2", "SPEED_3"]),
				("left", "city", ["zoomout", "zoomin", "seethrough", "map", "photo"])
			};

			if (tier == "wide")
			{
				string[][] build =
				[
					["cat-road", "cat-zoning"],
					["cat-networks", "cat-power", "cat-water", "cat-garbage"],
					["cat-police", "cat-fire", "cat-health", "cat-deathcare", "cat-education", "cat-parks", "cat-comms", "cat-admin"],
					["cat-transit", "cat-industry", .. placeables.Keys.Where(k => !TabOrder.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).Select(k => "cat-" + k)]
				];

				foreach (var ids in build)
					groups.Add(("build", "zoning", ids));
			}
			else
				groups.Add(("build", "zoning", BuildGroups.Select(g => "group-" + g.Name).ToArray()));

			groups.Add(("build", "zoning", ["bulldoze"]));
			groups.Add(("right", "info", ["infoviews", "budget", "stats", "cityinfo", "policies", "districts", "progression", "transit", "production", "tiles"]));
			groups.Add(("right", "info", ["chirper", "advisor", "achievements", "notifications"]));
			return groups;
		}

		static string TierFor(int logicalWidth)
		{
			return logicalWidth >= 1600 ? "wide" : logicalWidth >= 1100 ? "medium" : "compact";
		}

		/// <summary>Buttons the compact tier always moves into "More" (tools/iso_ui_hud.py RIGHT_COMPACT and the camera strip).</summary>
		static readonly string[] CompactOverflow = ["zoomout", "zoomin", "photo", "cityinfo", "districts", "production", "tiles", "advisor", "achievements"];

		void LayoutToolbar()
		{
			var width = CityLayout.Window.X;
			var tier = TierFor(width);
			var groups = TierGroups(tier);

			foreach (var b in hudButtons.Values)
			{
				b.InMore = false;
				if (b.Widget != null)
					b.Widget.Visible = false;
			}

			// Buttons that exist in this game (availability is fixed per game: providers do not come and go).
			var present = groups.ConvertAll(
				g => (g.Side, g.Family, Ids: g.Ids.Where(id => hudButtons.TryGetValue(id, out var b) && b.Available()).ToList()));
			if (tier == "compact")
				foreach (var (_, _, ids) in present)
					foreach (var id in ids)
						if (CompactOverflow.Contains(id))
							hudButtons[id].InMore = true;

			// Overflow: move the lowest priorities into "More" until everything fits.
			int Needed()
			{
				var total = 0;
				var count = 0;
				foreach (var g in present)
				{
					var n = g.Ids.Count(id => !hudButtons[id].InMore);
					if (g.Side == "right" && g == present.Last(p => p.Side == "right") && hudButtons.Values.Any(b => b.InMore))
						n++;

					if (n == 0)
						continue;

					total += n * ButtonWidth + 4 + GroupGap;
					count++;
				}

				return total + 2 * SideGap;
			}

			while (Needed() > width)
			{
				var candidate = present.SelectMany(g => g.Ids).Select(id => hudButtons[id]).Where(b => !b.InMore && b.Id != "menu")
					.OrderBy(b => b.Priority).FirstOrDefault();
				if (candidate == null || candidate.Priority >= 99)
					break;

				candidate.InMore = true;
			}

			var overflow = present.SelectMany(g => g.Ids).Select(id => hudButtons[id]).Where(b => b.InMore).ToList();

			// Frames
			foreach (var g in shownGroups)
				toolbar.RemoveChild(g.Frame);

			shownGroups.Clear();
			var lastRight = present.FindLastIndex(p => p.Side == "right");
			var rows = new List<(string Side, HudGroup Group)>();
			for (var i = 0; i < present.Count; i++)
			{
				var (side, family, ids) = present[i];
				var group = new HudGroup { Family = family };
				group.Buttons.AddRange(ids.Select(id => hudButtons[id]).Where(b => !b.InMore));
				if (i == lastRight && overflow.Count > 0)
					group.Buttons.Add(moreButton);

				if (group.Buttons.Count == 0)
					continue;

				group.Frame = new BackgroundWidget
				{
					Background = CityTheme.Art(family, "toolbar"),
					Bounds = new WidgetBounds(0, 0, group.Buttons.Count * ButtonWidth + 4, StripHeight)
				};
				toolbar.AddChild(group.Frame);
				rows.Add((side, group));
				shownGroups.Add(group);
			}

			// Frames are drawn under the buttons: move the buttons (and the menu) after the frames
			foreach (var b in hudButtons.Values)
			{
				if (b.Widget == null || b.Widget == menuButton)
					continue;

				toolbar.RemoveChild(b.Widget);
				toolbar.AddChild(b.Widget);
			}

			toolbar.RemoveChild(menuContainer);
			toolbar.AddChild(menuContainer);

			// Left groups from the left edge, right groups from the right edge, build groups centred in between
			var x = 0;
			foreach (var (side, g) in rows.Where(r => r.Side == "left"))
				x = PlaceGroup(g, x) + GroupGap;

			var leftEnd = x;
			var rightWidth = rows.Where(r => r.Side == "right").Sum(r => r.Group.Frame.Bounds.Width + GroupGap) - GroupGap;
			var rx = width - rightWidth;
			foreach (var (side, g) in rows.Where(r => r.Side == "right"))
				rx = PlaceGroup(g, rx) + GroupGap;

			var buildWidth = rows.Where(r => r.Side == "build").Sum(r => r.Group.Frame.Bounds.Width + GroupGap) - GroupGap;
			var free = width - rightWidth - leftEnd;
			var bx = leftEnd + Math.Max(SideGap, (free - buildWidth) / 2);
			foreach (var (side, g) in rows.Where(r => r.Side == "build"))
				bx = PlaceGroup(g, bx) + GroupGap;

			LayoutMorePopup(overflow);
		}

		int PlaceGroup(HudGroup g, int x)
		{
			g.Frame.Bounds.X = x;
			g.Frame.Bounds.Y = 0;
			for (var i = 0; i < g.Buttons.Count; i++)
			{
				var b = g.Buttons[i];
				var w = b.Widget;
				var bx = x + 2 + i * ButtonWidth;
				if (w == menuButton)
				{
					menuContainer.Bounds = new WidgetBounds(bx, 2, ButtonWidth, ButtonHeight);
					w.Bounds = new WidgetBounds(0, 0, ButtonWidth, ButtonHeight);
					w.Visible = true;
				}
				else
				{
					w.Bounds = new WidgetBounds(bx, 2, ButtonWidth, ButtonHeight);
					w.Visible = true;
				}

				SetFamily(w, g.Family);
			}

			return x + g.Frame.Bounds.Width;
		}

		static void SetFamily(ButtonWidget button, string family)
		{
			button.Background = CityTheme.Art(family, "iconbutton");
		}

		void LayoutMorePopup(List<HudButton> overflow)
		{
			morePopup.RemoveChildren();
			if (overflow.Count == 0)
			{
				moreOpen = false;
				return;
			}

			const int PerRow = 5;
			var columns = Math.Min(PerRow, overflow.Count);
			var rows = (overflow.Count + PerRow - 1) / PerRow;
			var w = columns * ButtonWidth + 8;
			var h = rows * ButtonHeight + 8;
			var more = moreButton.Widget.Bounds;
			morePopup.Bounds = new WidgetBounds(Math.Max(0, Math.Min(more.X + more.Width - w, CityLayout.Window.X - w)), StripHeight + 2, w, h);
			for (var i = 0; i < overflow.Count; i++)
			{
				var b = overflow[i].Widget;
				if (b == menuButton)
					continue;

				toolbar.RemoveChild(b);
				morePopup.AddChild(b);
				b.Bounds = new WidgetBounds(4 + i % PerRow * ButtonWidth, 4 + i / PerRow * ButtonHeight, ButtonWidth, ButtonHeight);
				b.Visible = true;
				SetFamily(b, "info");
			}
		}

		void TickToolbar()
		{
			// Transient messages float over the world view; they make way for open windows.
			if (!transientsHooked && Ui.Root.GetOrNull("TRANSIENTS_PANEL") is { } transients)
			{
				transientsHooked = true;
				transients.IsVisible = () => !AnyPanelOpen();
			}

			// An open popup stays above the windows opened after it.
			var siblings = morePopup.Parent.Children;
			if (moreOpen && siblings[^1] != morePopup)
				Game.RunAfterTick(() =>
				{
					siblings.Remove(morePopup);
					siblings.Add(morePopup);
				});

			var version = CityLayout.Version;
			if (version == laidOutVersion)
				return;

			laidOutVersion = version;

			// Buttons moved into the popup go back to the toolbar before the new layout.
			foreach (var child in morePopup.Children.ToList())
			{
				morePopup.RemoveChild(child);
				toolbar.AddChild(child);
			}

			LayoutToolbar();
		}

		/// <summary>Hotkeys of buttons in the "More" popup (hidden widgets do not see key presses).</summary>
		bool HandleOverflowHotkey(KeyInput e)
		{
			foreach (var b in hudButtons.Values)
			{
				if (!b.InMore || b.Hotkey == null || !Game.ModData.Hotkeys[b.Hotkey].IsActivatedBy(e))
					continue;

				if (!Locked(b) && b.Available())
					b.OnClick();

				return true;
			}

			return false;
		}

		void OpenSave()
		{
			var wasPaused = world.PredictedPaused;
			world.SetPauseState(true);
			Ui.OpenWindow("GAMESAVE_BROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => world.SetPauseState(wasPaused) },
				{ "onStart", () => { } },
				{ "world", world }
			});
		}

		void OpenSettings()
		{
			Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs
			{
				{ "world", world },
				{ "worldRenderer", worldRenderer },
				{ "onExit", () => { } },
			});
		}

		void ToggleBulldoze()
		{
			var wasActive = activeToolId == "bulldoze" && ToolActive();
			CloseBuild();
			CancelTool();
			if (!wasActive)
				Activate("bulldoze", new BulldozeOrderGenerator(world));
		}
	}
}
