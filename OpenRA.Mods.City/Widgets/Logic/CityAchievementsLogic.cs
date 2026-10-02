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

using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The achievements live on the third tab of the progression window (CityProgressLogic). This invisible proxy keeps the id
	/// CITY_ACHIEVEMENTS_PANEL working for code that opens panels by id (the autotest): making it visible opens the progression
	/// window on the achievements tab. The toolbar button toggles that tab directly (CityProgressLogic.ToggleAchievements).
	/// </summary>
	public class CityAchievementsLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public CityAchievementsLogic(Widget widget)
		{
			var requested = widget.IsVisible;
			widget.IsVisible = () =>
			{
				if (requested())
				{
					widget.Visible = false;
					CityProgressLogic.OpenAchievements();
				}

				return false;
			};
		}
	}
}
