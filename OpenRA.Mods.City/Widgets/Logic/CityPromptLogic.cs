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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Makes the common message dialogs (ConfirmationDialogs: THREEBUTTON_PROMPT, TWOBUTTON_PROMPT, TEXT_INPUT_PROMPT) RCT2
	/// windows: the prompt title goes in the title bar, the icon follows the kind of dialog (a confirmation shows a
	/// warning, a message with a single button an info icon), the close box acts as Cancel, and once ConfirmationDialogs has
	/// added the message lines the window is sized to them with the buttons right-aligned.
	/// </summary>
	public class CityPromptLogic : ChromeLogic
	{
		const int TextX = 56, WellTop = 25, WellSize = 42, LineHeight = 12, ButtonHeight = 18, Margin = 6;

		static readonly string[] ButtonIds = ["CONFIRM_BUTTON", "ACCEPT_BUTTON", "OTHER_BUTTON", "CANCEL_BUTTON"];

		readonly CityPanelWidget panel;
		bool fitted;

		[ObjectCreator.UseCtor]
		public CityPromptLogic(Widget widget)
		{
			panel = (CityPanelWidget)widget;

			var title = widget.Get<LabelWidget>("PROMPT_TITLE");
			panel.GetTitle = () => title.GetText();

			var confirm = widget.GetOrNull<ButtonWidget>("CONFIRM_BUTTON");
			var icon = widget.GetOrNull<CityIconWidget>("PROMPT_ICON");
			if (icon != null && string.IsNullOrEmpty(icon.Icon))
				icon.GetIcon = () => confirm != null && confirm.IsVisible() ? "ui_warning" : "ui_info";

			panel.OnClose = () =>
			{
				// The close box is Cancel; a message with only an OK button closes with it.
				var button = Enabled(widget, "CANCEL_BUTTON") ?? Enabled(widget, "CONFIRM_BUTTON");
				if (button != null)
					button.OnClick();
				else
					Ui.CloseWindow();
			};

			widget.Get<LogicTickerWidget>("PROMPT_TICKER").OnTick = Fit;
		}

		static ButtonWidget Enabled(Widget widget, string id)
		{
			var button = widget.GetOrNull<ButtonWidget>(id);
			return button != null && button.IsVisible() && !button.IsDisabled() ? button : null;
		}

		void Fit()
		{
			if (fitted)
				return;

			fitted = true;
			var font = Game.Renderer.Fonts["Regular"];
			var input = panel.GetOrNull("INPUT_TEXT");

			// ButtonPrompt keeps the PROMPT_TEXT template (empty) and adds one clone per message line.
			var lines = input != null ? [] : panel.Children.OfType<LabelWidget>().Where(l => l.Id == "PROMPT_TEXT").Skip(1).ToList();

			var buttons = ButtonIds.Select(panel.GetOrNull<ButtonWidget>).Where(b => b != null && b.IsVisible()).ToList();
			var widths = buttons.ConvertAll(b => Math.Max(56, Game.Renderer.Fonts[b.Font].Measure(b.GetText()).X + 16));
			var buttonsRow = widths.Sum() + 4 * Math.Max(0, buttons.Count - 1);

			var textWidth = lines.Count == 0 ? 0 : lines.Max(l => font.Measure(l.GetText()).X);
			var width = Math.Max(panel.Bounds.Width, Math.Max(TextX + textWidth + 2 * Margin + 2, buttonsRow + 2 * Margin + 4));
			width = Math.Min(width, panel.Area.Width);

			var block = lines.Count * LineHeight;
			var bodyBottom = WellTop + (input != null ? WellSize : Math.Max(WellSize, block));
			var top = WellTop + Math.Max(0, (WellSize - block) / 2);
			var textBoxWidth = width - TextX - Margin - 4;
			for (var i = 0; i < lines.Count; i++)
				lines[i].Bounds = new WidgetBounds(TextX, top + i * LineHeight, textBoxWidth, LineHeight);

			if (input != null)
			{
				input.Bounds.Width = textBoxWidth;
				panel.Get("PROMPT_TEXT").Bounds.Width = textBoxWidth;
			}

			var buttonY = bodyBottom + 8;
			var x = width - Margin - 4 - buttonsRow;
			for (var i = 0; i < buttons.Count; i++)
			{
				buttons[i].Bounds = new WidgetBounds(x, buttonY, widths[i], ButtonHeight);
				x += widths[i] + 4;
			}

			panel.Bounds.Width = width;
			panel.Bounds.Height = buttonY + ButtonHeight + 8;
			panel.Place();
		}
	}
}
