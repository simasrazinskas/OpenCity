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
using System.Collections.Generic;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// An invisible hover region that shows the factor list tooltip (CITY_FACTOR_TOOLTIP) of whatever the logic assigns:
	/// happiness factors, demand factors, ... It draws nothing, so it can sit on top of bars and labels.
	/// </summary>
	public class FactorHoverWidget : Widget
	{
		public string TooltipContainer;
		public readonly string TooltipTemplate = "CITY_FACTOR_TOOLTIP";

		public Func<string> GetTitle = () => "";
		public Func<IReadOnlyList<DemandFactor>> GetFactors = () => [];

		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		public FactorHoverWidget()
		{
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected FactorHoverWidget(FactorHoverWidget other)
			: base(other)
		{
			TooltipContainer = other.TooltipContainer;
			TooltipTemplate = other.TooltipTemplate;
			GetTitle = other.GetTitle;
			GetFactors = other.GetFactors;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override FactorHoverWidget Clone() { return new FactorHoverWidget(this); }

		public override void MouseEntered()
		{
			if (TooltipContainer == null)
				return;

			tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "getTitle", GetTitle }, { "getFactors", GetFactors } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null && tooltipContainer.IsValueCreated)
				tooltipContainer.Value.RemoveTooltip();
		}

		public override void Draw() { }
	}
}
