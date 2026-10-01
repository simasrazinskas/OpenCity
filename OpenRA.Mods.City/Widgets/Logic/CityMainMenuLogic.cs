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

using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>OpenCity main menu: New City, Load City, Settings, Extras, Quit. Shown over the shellmap.</summary>
	public class CityMainMenuLogic : ChromeLogic
	{
		enum MenuType { Main, Extras, None }

		readonly Widget rootMenu;
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

			// The big title block would shine through the translucent panels, so only show it with the menu.
			widget.Get("TITLE_BLOCK").IsVisible = () => menuType != MenuType.None;

			var menus = widget.Get("MENUS");
			var mainMenu = menus.Get("MAIN_MENU");
			mainMenu.IsVisible = () => menuType == MenuType.Main;

			var hasMaps = modData.MapCache.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.Lobby));
			var newButton = mainMenu.Get<ButtonWidget>("NEW_CITY_BUTTON");
			newButton.Disabled = !hasMaps;
			newButton.OnClick = OpenNewCityPanel;

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
