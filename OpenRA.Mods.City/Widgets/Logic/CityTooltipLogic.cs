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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Tooltip template CITY_TOOLTIP: the first line of the text is the title (TITLE label), the remaining lines are the
	/// body (BODY label), word-wrapped at MaxWidth. The panel hugs the text.
	/// </summary>
	public class CityTooltipLogic : ChromeLogic
	{
		const int MaxWidth = 320;

		[ObjectCreator.UseCtor]
		public CityTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Func<string> getText)
		{
			var title = widget.Get<LabelWidget>("TITLE");
			var body = widget.Get<LabelWidget>("BODY");
			var titleFont = Game.Renderer.Fonts[title.Font];
			var bodyFont = Game.Renderer.Fonts[body.Font];
			var pad = title.Bounds.X;

			var cachedText = (string)null;
			var titleText = "";
			var bodyText = "";
			title.GetText = () => titleText;
			body.GetText = () => bodyText;

			tooltipContainer.BeforeRender = () =>
			{
				var text = getText() ?? "";
				if (text == cachedText)
					return;

				cachedText = text;
				var split = text.IndexOf('\n');
				titleText = split < 0 ? text : text[..split];
				var rest = split < 0 ? "" : text[(split + 1)..].Trim('\n');

				var titleSize = titleFont.Measure(titleText);
				var bodyWidth = Math.Min(MaxWidth, bodyFont.Measure(rest).X);
				bodyText = rest.Length > 0 ? WidgetUtils.WrapText(rest, Math.Max(bodyWidth, titleSize.X), bodyFont) : "";
				var bodySize = bodyText.Length > 0 ? bodyFont.Measure(bodyText) : int2.Zero;

				var width = Math.Max(titleSize.X, bodySize.X);
				title.Bounds.Width = width;
				body.Bounds.Width = width;
				body.Bounds.Y = title.Bounds.Y + title.Bounds.Height;
				body.Bounds.Height = bodySize.Y;
				body.Visible = bodyText.Length > 0;

				widget.Bounds.Width = width + 2 * pad;
				widget.Bounds.Height = (body.Visible ? body.Bounds.Bottom + 2 : title.Bounds.Bottom) + pad - 2;
			};
		}
	}
}
