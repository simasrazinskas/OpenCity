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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>A flat horizontal progress bar (0..100) with a dark track and a coloured fill.</summary>
	public class CityBarWidget : Widget
	{
		public int Percentage = 0;
		public Color BarColor = CityUi.Accent;
		public Color TrackColor = Color.FromArgb(150, 8, 14, 22);
		public Func<int> GetPercentage;
		public Func<Color> GetBarColor;

		public CityBarWidget()
		{
			GetPercentage = () => Percentage;
			GetBarColor = () => BarColor;
		}

		protected CityBarWidget(CityBarWidget other)
			: base(other)
		{
			Percentage = other.Percentage;
			BarColor = other.BarColor;
			TrackColor = other.TrackColor;
			GetPercentage = other.GetPercentage;
			GetBarColor = other.GetBarColor;
		}

		public override CityBarWidget Clone() { return new CityBarWidget(this); }

		public override void Draw()
		{
			var rb = RenderBounds;
			WidgetUtils.FillRectWithColor(rb, TrackColor);

			var percentage = Math.Clamp(GetPercentage(), 0, 100);
			var width = rb.Width * percentage / 100;
			if (width > 0)
				WidgetUtils.FillRectWithColor(new Rectangle(rb.X, rb.Y, width, rb.Height), GetBarColor());
		}
	}
}
