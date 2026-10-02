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
	/// A colour swatch (tools/iso_ui_panels_*.swatch): the colour in a one pixel dark frame, on whole device pixels at
	/// every UI scale. Bevel: true adds a light top/left and a dark bottom edge (district and line colours); IsDimmed draws
	/// it washed out (a series that is switched off).
	/// </summary>
	public class CitySwatchWidget : Widget
	{
		public Color Color = Color.Gray;
		public bool Bevel;
		public Func<Color> GetColor;
		public Func<bool> IsDimmed = () => false;

		public CitySwatchWidget()
		{
			GetColor = () => Color;
		}

		protected CitySwatchWidget(CitySwatchWidget other)
			: base(other)
		{
			Color = other.Color;
			Bevel = other.Bevel;
			GetColor = other.GetColor;
			IsDimmed = other.IsDimmed;
		}

		public override CitySwatchWidget Clone() { return new CitySwatchWidget(this); }

		static Color Mix(Color a, Color b, float t)
		{
			return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var b = CityTheme.BevelLogical;
			var c = GetColor();
			c = Color.FromArgb(255, c.R, c.G, c.B);
			if (IsDimmed())
				c = Mix(c, Color.FromArgb(160, 160, 160), 0.5f);

			var frame = Bevel ? CityTheme.Ramp("grey", 0) : CityTheme.FamilyShade(CityTheme.FamilyOf(this), 1);
			CityTheme.Fill(rb.X, rb.Y, rb.Width, rb.Height, frame);
			float x = rb.X + b, y = rb.Y + b, w = rb.Width - 2 * b, h = rb.Height - 2 * b;
			CityTheme.Fill(x, y, w, h, c);
			if (!Bevel)
				return;

			CityTheme.Fill(x, y, w, b, Mix(c, Color.White, 0.45f));
			CityTheme.Fill(x, y, b, h, Mix(c, Color.White, 0.45f));
			CityTheme.Fill(x, y + h - b, w, b, Mix(c, Color.Black, 0.35f));
		}
	}
}
