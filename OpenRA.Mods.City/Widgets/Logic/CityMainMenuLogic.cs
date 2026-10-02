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
using System.IO;
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>OpenCity main menu: New City, Continue, Load City, Settings, Extras, Quit. Shown over the shellmap.</summary>
	public class CityMainMenuLogic : ChromeLogic
	{
		enum MenuType { Main, Extras, None }

		[FluentReference("name", "date")]
		const string ContinueSave = "menu-continue-save";

		[FluentReference]
		const string ContinueNone = "menu-continue-none";

		[FluentReference("version")]
		const string VersionText = "menu-version";

		/// <summary>Minimum space around the title stack.</summary>
		const int Margin = 8;

		readonly Widget rootMenu;
		readonly ModData modData;
		string continueSave;
		MenuType menuType = MenuType.Main;

		void SwitchMenu(MenuType type)
		{
			menuType = type;

			// Update button mouseover
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		[ObjectCreator.UseCtor]
		public CityMainMenuLogic(Widget widget, ModData modData)
		{
			rootMenu = widget;
			this.modData = modData;

			// The title stack is centred on screen; without room for the logo (very large UI scales) only the menu stays.
			var stack = widget.Get("STACK");
			var title = stack.Get("TITLE_BLOCK");
			var menusBox = stack.Get("MENUS");
			var menuBottom = menusBox.Bounds.Y + menusBox.Bounds.Height;
			var titleOffset = menusBox.Bounds.Y;
			stack.IsVisible = () => menuType != MenuType.None;
			widget.Get<LogicTickerWidget>("LAYOUT_TICKER").OnTick = () =>
			{
				var window = Game.Renderer.Resolution;
				var showTitle = window.Height >= menuBottom + 2 * Margin;
				title.Visible = showTitle;
				var height = showTitle ? menuBottom : menusBox.Bounds.Height;
				menusBox.Bounds.Y = showTitle ? titleOffset : 0;
				stack.Bounds.Height = height;
				stack.Bounds.Y = Math.Max(Margin, showTitle ? (window.Height - height) / 6 : (window.Height - height) / 2);
			};

			var version = modData.Manifest.Metadata.Version;
			widget.Get<LabelWidget>("VERSION_LABEL").GetText = () => FluentProvider.GetMessage(VersionText, "version", version);

			var menus = menusBox;
			var mainMenu = menus.Get("MAIN_MENU");
			mainMenu.IsVisible = () => menuType == MenuType.Main;

			var hasMaps = modData.MapCache.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.Lobby));
			var newButton = mainMenu.Get<ButtonWidget>("NEW_CITY_BUTTON");
			newButton.Disabled = !hasMaps;
			newButton.OnClick = OpenNewCityPanel;

			SetupContinue(mainMenu.Get<CityMenuButtonWidget>("CONTINUE_BUTTON"));

			var loadButton = mainMenu.Get<ButtonWidget>("LOAD_CITY_BUTTON");
			loadButton.IsDisabled = () => !LoadGameBrowserLogic.IsLoadPanelEnabled(modData.Manifest);
			loadButton.OnClick = OpenGameSaveBrowserPanel;

			mainMenu.Get<ButtonWidget>("SETTINGS_BUTTON").OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("SETTINGS_PANEL", new WidgetArgs
				{
					{ "onExit", () => SwitchMenu(MenuType.Main) }
				});
			};

			mainMenu.Get<ButtonWidget>("EXTRAS_BUTTON").OnClick = () => SwitchMenu(MenuType.Extras);
			mainMenu.Get<ButtonWidget>("QUIT_BUTTON").OnClick = Game.Exit;

			var extrasMenu = menus.Get("EXTRAS_MENU");
			extrasMenu.IsVisible = () => menuType == MenuType.Extras;

			extrasMenu.Get<ButtonWidget>("REPLAYS_BUTTON").OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Ui.OpenWindow("REPLAYBROWSER_PANEL", new WidgetArgs
				{
					{ "onExit", () => SwitchMenu(MenuType.Extras) },
					{ "onStart", RemoveShellmapUI }
				});
			};

			extrasMenu.Get<ButtonWidget>("ASSETBROWSER_BUTTON").OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("ASSETBROWSER_PANEL", new WidgetArgs
				{
					{ "onExit", () => SwitchMenu(MenuType.Extras) }
				});
			};

			extrasMenu.Get<ButtonWidget>("CREDITS_BUTTON").OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Ui.OpenWindow("CREDITS_PANEL", new WidgetArgs
				{
					{ "onExit", () => SwitchMenu(MenuType.Extras) }
				});
			};

			extrasMenu.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => SwitchMenu(MenuType.Main);

			// The shellmap UI has to go away when a game starts (new game, loaded save or replay).
			Game.BeforeGameStart += RemoveShellmapUI;
		}

		/// <summary>"Continue" loads the most recently written save, if there is one.</summary>
		void SetupContinue(CityMenuButtonWidget button)
		{
			var mod = modData.Manifest;
			var folder = Path.Combine(Platform.SupportDir, "Saves", mod.Id, mod.Metadata.Version);
			string path = null;
			string mapUid = null;
			if (Directory.Exists(folder))
			{
				foreach (var candidate in Directory.GetFiles(folder, "*.orasav").OrderByDescending(File.GetLastWriteTime))
				{
					try
					{
						var save = new GameSave(candidate);
						if (modData.MapCache[save.GlobalSettings.Map].Status == MapStatus.Available)
						{
							path = candidate;
							mapUid = save.GlobalSettings.Map;
							break;
						}
					}
					catch (Exception)
					{
						// Saves of another version or broken files are skipped.
					}
				}
			}

			if (path == null)
			{
				button.GetSub = () => FluentProvider.GetMessage(ContinueNone);
				button.IsDisabled = () => true;
				button.IsDefault = () => false;
				return;
			}

			var name = Path.GetFileNameWithoutExtension(path);
			var date = File.GetLastWriteTime(path).ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
			continueSave = path;
			button.GetSub = () => FluentProvider.GetMessage(ContinueSave, "name", name, "date", date);
			button.OnClick = () =>
			{
				var orders = new List<Order>
				{
					Order.FromTargetString("LoadGameSave", Path.GetFileName(continueSave), true),
					Order.Command($"state {Session.ClientState.Ready}")
				};

				CityNewGameAutoTest.PrepareGame();
				Game.CreateAndStartLocalServer(mapUid, orders);
			};
		}

		void OpenNewCityPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("CITY_NEWGAME_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) }
			});
		}

		void OpenGameSaveBrowserPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("LOAD_GAME_BROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "onStart", RemoveShellmapUI }
			});
		}

		void RemoveShellmapUI()
		{
			rootMenu.Parent?.RemoveChild(rootMenu);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				Game.BeforeGameStart -= RemoveShellmapUI;

			base.Dispose(disposing);
		}
	}
}
