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
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The saved game loading screen (design/iso/ui/screens/loading-1280x720-1x.png): adds the percentage and a tip to OpenRA's.</summary>
	[IncludeStaticFluentReferences(typeof(GameSaveLoadingLogic))]
	public class CityGameSaveLoadingLogic : GameSaveLoadingLogic
	{
		[FluentReference]
		const string Tips = "menu-loading-tips";

		[FluentReference("n", "total")]
		const string TipCount = "menu-loading-tip-count";

		[ObjectCreator.UseCtor]
		public CityGameSaveLoadingLogic(Widget widget, ModData modData, World world)
			: base(widget, modData, world)
		{
			int Percentage() => Math.Clamp(world.GameSaveLoadingPercentage, 0, 100);
			widget.Get<ProgressBarWidget>("PROGRESS").GetPercentage = Percentage;
			widget.Get<LabelWidget>("PERCENT").GetText = () => Percentage().ToString(CultureInfo.CurrentCulture) + "%";

			var tips = FluentProvider.GetMessage(Tips).Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
			if (tips.Length == 0)
				return;

			var index = new Random().Next(tips.Length);
			widget.Get<LabelWidget>("TIP").GetText = () => tips[index];
			var count = FluentProvider.GetMessage(TipCount, "n", index + 1, "total", tips.Length);
			widget.Get<LabelWidget>("TIP_COUNT").GetText = () => count;
		}
	}
}
