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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The game (pause) menu (design/iso/ui/panels/pause-window.png): OpenRA's IngameMenuLogic builds the buttons, this
	/// adds the city header, icons, the "Quit to desktop" entry and the PAUSED banner.
	/// </summary>
	[IncludeStaticFluentReferences(typeof(IngameMenuLogic))]
	public class CityIngameMenuLogic : IngameMenuLogic
	{
		[FluentReference("milestone", "date", "population")]
		const string InfoText = "menu-pause-info";

		[FluentReference]
		const string Paused = "menu-pause-paused";

		[FluentReference]
		const string QuitTitle = "menu-pause-quit-title";

		[FluentReference]
		const string QuitPrompt = "menu-pause-quit-prompt";

		[FluentReference]
		const string QuitConfirm = "menu-pause-quit-confirm";

		[FluentReference]
		const string QuitCancel = "menu-pause-quit-cancel";

		const int ButtonHeight = 28;
		const int ButtonGap = 4;
		const int HeaderHeight = 30;
		const int SectionGap = 4;

		static readonly Dictionary<string, (string Icon, string Hint)> Entries = new()
		{
			["RESUME"] = ("time_play", "Esc"),
			["SAVE_GAME"] = ("pnl_save", null),
			["LOAD_GAME"] = ("pnl_load", null),
			["SETTINGS"] = ("pnl_settings", null),
			["MUSIC"] = ("ui_music", null),
			["ABORT_MISSION"] = ("ui_exit", null),
		};

		[ObjectCreator.UseCtor]
		public CityIngameMenuLogic(Widget widget, ModData modData, World world, Action onExit, WorldRenderer worldRenderer,
			IngameInfoPanel initialPanel, Dictionary<string, MiniYaml> logicArgs)
			: base(widget, modData, world, onExit, worldRenderer, initialPanel, logicArgs)
		{
			var menu = widget.Get("INGAME_MENU");
			var panel = menu.Get<CityPanelWidget>("MENU_BUTTONS");
			panel.OnClose = () => menu.Get<ButtonWidget>("RESUME").OnClick();

			var manager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
			var name = menu.Get<LabelWidget>("HEADER_NAME");
			name.GetText = () => world.Map.Title;
			var info = menu.Get<LabelWidget>("HEADER_INFO");
			info.GetText = () => manager == null ? "" : FluentProvider.GetMessage(InfoText,
				"milestone", manager.MilestoneName,
				"date", DateText(manager.Date),
				"population", manager.Population.ToString("N0", CultureInfo.CurrentCulture));

			var banner = menu.Get<LabelWidget>("PAUSED_LABEL");
			banner.IsVisible = () => world.PredictedPaused;
			banner.GetText = () => FluentProvider.GetMessage(Paused);

			// Icons, texts and the room between the entries
			var y = panel.ContentY + HeaderHeight;
			CityMenuButtonWidget quit = null;
			foreach (var button in panel.Children.OfType<CityMenuButtonWidget>().ToList())
			{
				if (button.Id == "QUIT_BUTTON")
				{
					quit = button;
					continue;
				}

				if (Entries.TryGetValue(button.Id, out var entry))
				{
					button.Icon = entry.Icon;
					if (entry.Hint != null)
						button.GetHint = () => entry.Hint;
				}

				if (button.Id == "ABORT_MISSION")
					y += SectionGap;

				button.Bounds.X = CityPanelWidget.Padding;
				button.Bounds.Y = y;
				button.Bounds.Width = panel.Bounds.Width - 2 * CityPanelWidget.Padding;
				button.Bounds.Height = ButtonHeight;
				y += ButtonHeight + ButtonGap;
			}

			quit.Bounds.X = CityPanelWidget.Padding;
			quit.Bounds.Y = y;
			quit.Bounds.Width = panel.Bounds.Width - 2 * CityPanelWidget.Padding;
			quit.Bounds.Height = ButtonHeight;
			quit.OnClick = () => ConfirmationDialogs.ButtonPrompt(modData,
				title: QuitTitle,
				text: QuitPrompt,
				onConfirm: Game.Exit,
				confirmText: QuitConfirm,
				onCancel: () => { },
				cancelText: QuitCancel);

			panel.Bounds.Height = y + ButtonHeight + CityPanelWidget.Padding + 4;
		}

		static string DateText(CityDate d)
		{
			var month = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(Math.Clamp(d.Month, 1, 12));
			return d.Hour >= 0 ? $"{month} {d.Year}, {d.Hour:D2}:{d.Minute:D2}" : $"{month} {d.Year}";
		}
	}
}
