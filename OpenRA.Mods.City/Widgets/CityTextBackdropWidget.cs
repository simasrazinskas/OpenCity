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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Draws a translucent dark box behind the text of a sibling label (sized to the text, not the label), so text
	/// that floats over the busy world view stays readable. List it before the label so it draws underneath.
	/// </summary>
	public class CityTextBackdropWidget : Widget
	{
		public string Target = "TEXT";
		public Color Color = Color.FromArgb(170, 8, 12, 18);
		public int Padding = 3;

		LabelWidget target;

		public CityTextBackdropWidget() { }

		protected CityTextBackdropWidget(CityTextBackdropWidget other)
			: base(other)
		{
			Target = other.Target;
			Color = other.Color;
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
			WidgetUtils.FillRectWithColor(new Rectangle(x - Padding, rb.Y, size.X + 2 * Padding, rb.Height), Color);
		}
	}
}
