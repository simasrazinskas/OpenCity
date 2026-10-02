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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// A small solid triangle (RCT2 sort and trade arrows, tools/iso_ui_parts arrow): pointing up or down, in GetColor. It is
	/// as wide as the widget and half as high, centred in the widget, and built from device-pixel snapped rows so it stays
	/// crisp at every UI scale.
	/// </summary>
	public class CityTriangleWidget : Widget
	{
		public bool Up;
		public Color Color = CityTheme.Ink;
		public Func<Color> GetColor;
		public Func<bool> GetUp;

		public CityTriangleWidget()
		{
			GetColor = () => Color;
			GetUp = () => Up;
		}

		protected CityTriangleWidget(CityTriangleWidget other)
			: base(other)
		{
			Up = other.Up;
			Color = other.Color;
			GetColor = other.GetColor;
			GetUp = other.GetUp;
		}

		public override CityTriangleWidget Clone() { return new CityTriangleWidget(this); }

		public override void Draw()
		{
			var rb = RenderBounds;
			var rows = Math.Max(1, (rb.Width + 1) / 2);
			var color = GetColor();
			var up = GetUp();
			var top = rb.Y + (rb.Height - rows) / 2f;
			for (var i = 0; i < rows; i++)
			{
				// The row at the tip is one pixel wide, every row further from the tip two wider.
				var width = Math.Min(rb.Width, 2 * i + 1);
				var y = up ? top + i : top + rows - 1 - i;
				CityTheme.Fill(rb.X + (rb.Width - width) / 2f, y, width, 1, color);
			}
		}
	}
}
