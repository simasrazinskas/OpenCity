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
	/// RCT2 meter (tools/iso_ui_widgets2.meter): a sunken dark trough with a ramp-coloured fill (light top third, dark
	/// bottom line), segmented into 5-pixel blocks unless Smooth. The fill ramp is Ramp / GetRamp, or the ramp closest
	/// to BarColor / GetBarColor for logic that picks colours (good / bad / warning).
	/// </summary>
	public class CityBarWidget : Widget
	{
		public int Percentage = 0;
		public Color BarColor = CityUi.Accent;
		public Color TrackColor = Color.FromArgb(150, 8, 14, 22);
		public string Ramp;
		public bool Smooth;
		public Func<int> GetPercentage;
		public Func<Color> GetBarColor;
		public Func<string> GetRamp;

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
			Ramp = other.Ramp;
			Smooth = other.Smooth;
			GetPercentage = other.GetPercentage;
			GetBarColor = other.GetBarColor;
			GetRamp = other.GetRamp;
		}

		public override CityBarWidget Clone() { return new CityBarWidget(this); }

		Color lastColor;
		string lastRamp;

		string CurrentRamp()
		{
			var ramp = GetRamp?.Invoke() ?? Ramp;
			if (ramp != null)
				return ramp;

			var color = GetBarColor();
			if (lastRamp == null || color != lastColor)
			{
				lastColor = color;
				lastRamp = CityTheme.RampFor(color);
			}

			return lastRamp;
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			CityTheme.DrawPanel(CityTheme.Art(family, "trough"), rb);

			var b = CityTheme.BevelLogical;
			var percentage = Math.Clamp(GetPercentage(), 0, 100);
			var inner = rb.Width - 2 * b;
			var width = inner * percentage / 100f;
			if (width >= b)
				CityTheme.DrawPanel("bar-" + CurrentRamp() + (Smooth ? "-smooth" : ""), rb.X + b, rb.Y + b, width, rb.Height - 2 * b);
		}
	}
}
