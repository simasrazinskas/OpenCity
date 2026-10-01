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
	/// <summary>A red-yellow-green gradient strip used as the info view legend.</summary>
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
			WidgetUtils.FillRectWithColor(rb, Color.FromArgb(255, 20, 16, 14));
			for (var i = 0; i < steps; i++)
			{
				var x0 = rb.X + i * rb.Width / steps;
				var x1 = rb.X + (i + 1) * rb.Width / steps;
				var t = i / (float)(steps - 1);
				WidgetUtils.FillRectWithColor(new Rectangle(x0, rb.Y, x1 - x0, rb.Height), GetColor != null ? GetColor(t) : CityUi.HeatColor(t));
			}
		}
	}
}
