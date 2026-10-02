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
using System.Globalization;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Helpers for the scripted UI tours of CityAutoTest (UI state only, nothing synced).</summary>
	public static class CityUiTour
	{
		/// <summary>"tab:N" clicks tab N of the frontmost open window, "tab:PANEL_ID:N" of a given window.</summary>
		public static void ClickTab(string spec, Action<string> report)
		{
			var parts = spec.Split(':');
			CityPanelWidget window;
			if (parts.Length > 1)
				window = Ui.Root.GetOrNull<CityPanelWidget>(parts[0]);
			else
				window = Frontmost(Ui.Root);

			if (window == null || !int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= window.Tabs.Count)
			{
				report($"ui tab {spec}: not found");
				return;
			}

			window.Tabs[index].OnClick();
			report($"ui tab {spec}: {window.Id} tab {index}");
		}

		static CityPanelWidget Frontmost(Widget root)
		{
			CityPanelWidget found = null;
			foreach (var child in root.Children)
			{
				if (!child.IsVisible())
					continue;

				if (child is CityPanelWidget panel && panel.Tabs.Count > 0)
					found = panel;

				found = Frontmost(child) ?? found;
			}

			return found;
		}
	}
}
