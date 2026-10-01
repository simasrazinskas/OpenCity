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
	/// Sidebar layout: the minimap block stays at the top, the bottom cap at the window bottom, the order rows and
	/// the status block stack up from the cap and the build palette takes whatever height is left (it scrolls when
	/// its items do not fit). Short windows switch the status block to a 2x2 grid. Re-run whenever the logical
	/// window size changes (window resize or UI scale).
	/// </summary>
	public partial class CityToolbarLogic
	{
		/// <summary>Bottom of the minimap block: where the palette starts.</summary>
		const int PaletteTop = 279;

		const int RowHeight = 48;
		const int CapHeight = 28;
		const int StatPitch = 24;
		const int StatHeight = 20;

		/// <summary>Below this window height the status readouts use two columns, so the palette keeps at least five rows.</summary>
		const int CompactBelow = PaletteTop + 5 * RowHeight + 3 * RowHeight + 2 * RowHeight + CapHeight;

		static readonly string[] StatIds = ["MILESTONE_STAT", "HAPPINESS_STAT", "POWER_STAT", "WATER_STAT"];

		int orderRows;
		int laidOutVersion = -1;
		int tabSlots = 9;

		bool transientsHooked;

		public override void Tick()
		{
			// Transient messages float over the world view; they make way for the big panels.
			if (!transientsHooked && Ui.Root.GetOrNull("TRANSIENTS_PANEL") is { } transients)
			{
				transientsHooked = true;
				transients.IsVisible = () => !AnyPanelOpen();
			}

			var version = CityLayout.Version;
			if (version == laidOutVersion)
				return;

			laidOutVersion = version;
			LayoutSidebar();
		}

		void LayoutSidebar()
		{
			var height = CityLayout.Window.Y;
			var compact = height < CompactBelow;
			var statusHeight = (compact ? 2 : 3) * RowHeight;
			var capY = height - CapHeight;
			var ordersY = capY - orderRows * RowHeight;
			var statusY = ordersY - statusHeight;
			var paletteHeight = Math.Max(RowHeight, statusY - PaletteTop);

			var production = widget.Get("CITY_PRODUCTION");
			production.Bounds.Y = PaletteTop;
			production.Bounds.Height = paletteHeight;
			palette.Bounds.Height = paletteHeight;
			tabContainer.Bounds.Height = paletteHeight - 4;

			var slots = Math.Max(3, (paletteHeight - 4) / TabPitch);
			if (slots != tabSlots)
			{
				tabSlots = slots;
				LayoutTabs();
			}

			var status = widget.Get("CITY_STATUS");
			status.Bounds.Y = statusY;
			status.Bounds.Height = statusHeight;
			status.Get("ROW3").Visible = !compact;

			for (var i = 0; i < StatIds.Length; i++)
			{
				status.Get(StatIds[i]).Bounds = compact
					? new WidgetBounds(40 + i % 2 * 92, 4 + i / 2 * StatPitch, 84, StatHeight)
					: new WidgetBounds(40, 4 + i * StatPitch, 176, StatHeight);
			}

			status.Get("DEMAND_BARS").Bounds.Y = statusHeight - RowHeight + 4;

			widget.Get("CITY_ORDERS").Bounds.Y = ordersY;
			widget.Get("SIDEBAR_CAP").Bounds.Y = capY;
		}
	}
}
