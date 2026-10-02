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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The window changes family with the selected building. CityTheme.ApplyFamily leaves art names that already name a
	/// family alone, so before switching the art of the old family is turned back into the shared names, which the next
	/// draw themes again for the new family.
	/// </summary>
	public partial class CityBuildingInfoLogic
	{
		void SetFamily(string family)
		{
			if (family == panel.Family)
				return;

			Unthemed(panel, panel.Family + "-");
			panel.Family = family;
		}

		static string Strip(string value, string prefix)
		{
			return value != null && value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
		}

		static void Unthemed(Widget root, string prefix)
		{
			foreach (var child in root.Children)
			{
				switch (child)
				{
					case CityPanelWidget:
						break;
					case ButtonWidget b:
						b.Background = Strip(b.Background, prefix);
						break;
					case ScrollPanelWidget sp:
						sp.ScrollBarBackground = Strip(sp.ScrollBarBackground, prefix);
						sp.Background = Strip(sp.Background, prefix);
						sp.Button = Strip(sp.Button, prefix);
						sp.Decorations = Strip(sp.Decorations, prefix);
						break;
					case SliderWidget sl:
						sl.Track = Strip(sl.Track, prefix);
						sl.Thumb = Strip(sl.Thumb, prefix);
						break;
					case BackgroundWidget bg:
						bg.Background = Strip(bg.Background, prefix);
						break;
				}

				Unthemed(child, prefix);
			}
		}
	}
}
