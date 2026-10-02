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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// A bar centred in the widget whose width is the percentage (the population pyramid of the city info window,
	/// tools/iso_ui_panels_finance.city_info): a smooth RCT bar of the ramp, no trough.
	/// </summary>
	public class CityCenterBarWidget : Widget
	{
		public int Percentage;
		public string Ramp = "green";
		public Func<int> GetPercentage;
		public Func<string> GetRamp;

		public CityCenterBarWidget()
		{
			GetPercentage = () => Percentage;
		}

		protected CityCenterBarWidget(CityCenterBarWidget other)
			: base(other)
		{
			Percentage = other.Percentage;
			Ramp = other.Ramp;
			GetPercentage = other.GetPercentage;
			GetRamp = other.GetRamp;
		}

		public override CityCenterBarWidget Clone() { return new CityCenterBarWidget(this); }

		public override void Draw()
		{
			var rb = RenderBounds;
			var percentage = Math.Clamp(GetPercentage(), 0, 100);
			var width = Math.Max(4, rb.Width * percentage / 100f);
			CityTheme.DrawPanel("bar-" + (GetRamp?.Invoke() ?? Ramp) + "-smooth", rb.X + (rb.Width - width) / 2, rb.Y, width, rb.Height);
		}
	}
}
