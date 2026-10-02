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

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 "Map" window (decision 5): the diamond radar (RadarWidget in isometric mode) in a dark well. The window
	/// keeps the 2:1 shape of the map diamond and takes about a quarter of the screen width (between 220 and 520 logical
	/// pixels), following window resizes and UI scale changes.
	/// </summary>
	public class CityMapLogic : ChromeLogic
	{
		readonly Widget panel;
		readonly Widget well;
		readonly Widget radar;
		int laidOutVersion = -1;
		int2 laidOutOrigin;

		[ObjectCreator.UseCtor]
		public CityMapLogic(Widget widget)
		{
			panel = widget;
			well = widget.Get("WELL");
			radar = well.Get("RADAR");
		}

		public override void Tick()
		{
			// The radar caches its screen rectangle: re-lay it out whenever the window moved (anchoring, dragging).
			if (radar.RenderOrigin != laidOutOrigin)
			{
				laidOutOrigin = radar.RenderOrigin;
				radar.Relayout();
			}

			var version = CityLayout.Version;
			if (version == laidOutVersion)
				return;

			laidOutVersion = version;
			var area = CityLayout.WorkArea();

			// The map diamond is twice as wide as high.
			var width = Math.Clamp(area.Width / 4, 220, 520);
			var mapHeight = (width - 12) / 2;
			var height = Math.Min(area.Height, CityPanelWidget.ContentTop + mapHeight + 8);

			panel.Bounds.Width = width;
			panel.Bounds.Height = height;
			well.Bounds = new WidgetBounds(
				CityPanelWidget.Padding, CityPanelWidget.ContentTop,
				width - 2 * CityPanelWidget.Padding, height - CityPanelWidget.ContentTop - CityPanelWidget.Padding);
			radar.Bounds = new WidgetBounds(2, 2, well.Bounds.Width - 4, well.Bounds.Height - 4);
			radar.Relayout();
			laidOutOrigin = radar.RenderOrigin;
		}
	}
}
