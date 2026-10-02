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

using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Draws an RCT2 tooltip plate (the `tooltip` panel: pale yellow, dark edge) behind the text of a sibling label (sized to
	/// the text, not the label), so text that floats over the busy world view stays readable. List it before the label so
	/// it draws underneath; the label uses the dark ink.
	/// </summary>
	public class CityTextBackdropWidget : Widget
	{
		public string Target = "TEXT";
		public string Background = "tooltip";
		public int Padding = 4;

		LabelWidget target;

		public CityTextBackdropWidget() { }

		protected CityTextBackdropWidget(CityTextBackdropWidget other)
			: base(other)
		{
			Target = other.Target;
			Background = other.Background;
			Padding = other.Padding;
		}

		public override CityTextBackdropWidget Clone() { return new CityTextBackdropWidget(this); }

		public override void Draw()
		{
			target ??= Parent?.GetOrNull<LabelWidget>(Target);
			if (target == null || !target.IsVisible())
				return;

			var text = target.GetText();
			if (string.IsNullOrEmpty(text))
				return;

			var size = Game.Renderer.Fonts[target.Font].Measure(text);
			var rb = target.RenderBounds;
			var x = target.Align == TextAlign.Right ? rb.Right - size.X : target.Align == TextAlign.Center ? rb.X + (rb.Width - size.X) / 2 : rb.X;
			CityTheme.DrawPanel(Background, x - Padding, rb.Y, size.X + 2 * Padding, rb.Height);
		}
	}
}
