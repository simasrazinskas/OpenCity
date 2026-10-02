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
	/// <summary>The info view legend strip: NET's ramp in its 11 steps (InfoViewRamps, exactly the world colours) in a sunken trough.</summary>
	public class HeatLegendWidget : Widget
	{
		public int Steps = 11;

		/// <summary>Colour at 0 (low) .. 1 (high); the red-yellow-green heat ramp when not set.</summary>
		public Func<float, Color> GetColor;

		public HeatLegendWidget() { }

		protected HeatLegendWidget(HeatLegendWidget other)
			: base(other)
		{
			Steps = other.Steps;
			GetColor = other.GetColor;
		}

		public override HeatLegendWidget Clone() { return new HeatLegendWidget(this); }

		public override void Draw()
		{
			var rb = RenderBounds;
			var steps = Steps < 2 ? 2 : Steps;
			var family = CityTheme.FamilyOf(this);
			CityTheme.DrawPanel(CityTheme.Art(family, "trough"), rb);
			var b = CityTheme.BevelLogical;
			var inner = rb.Width - 2 * b;
			for (var i = 0; i < steps; i++)
			{
				var x0 = rb.X + b + i * inner / steps;
				var x1 = rb.X + b + (i + 1) * inner / steps;
				var t = i / (float)(steps - 1);
				var c = GetColor != null ? GetColor(t) : CityUi.HeatColor(t);
				CityTheme.Fill(x0, rb.Y + b, x1 - x0, rb.Height - 2 * b, Color.FromArgb(255, c.R, c.G, c.B));
			}
		}
	}
}
