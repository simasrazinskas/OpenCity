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
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Four vertical bipolar bars (R, C, I, O) showing zone demand from -100 to 100.</summary>
	public class DemandBarsWidget : Widget
	{
		[FluentReference]
		const string DemandResidential = "label-demand-residential";

		[FluentReference]
		const string DemandCommercial = "label-demand-commercial";

		[FluentReference]
		const string DemandIndustrial = "label-demand-industrial";

		[FluentReference]
		const string DemandOffice = "label-demand-office";

		[FluentReference("value")]
		const string DemandValue = "label-demand-value";

		static readonly ZoneCategory[] Categories =
		[
			ZoneCategory.Residential, ZoneCategory.Commercial, ZoneCategory.Industrial, ZoneCategory.Office
		];

		static readonly string[] Letters = ["R", "C", "I", "O"];

		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "SIMPLE_TOOLTIP";
		public string Font = "TinyBold";

		readonly World world;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		[ObjectCreator.UseCtor]
		public DemandBarsWidget(World world)
		{
			this.world = world;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected DemandBarsWidget(DemandBarsWidget other)
			: base(other)
		{
			world = other.world;
			Font = other.Font;
			TooltipContainer = other.TooltipContainer;
			TooltipTemplate = other.TooltipTemplate;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override DemandBarsWidget Clone() { return new DemandBarsWidget(this); }

		int ColumnAt(int2 location)
		{
			var rb = RenderBounds;
			var column = (location.X - rb.X) * Categories.Length / Math.Max(1, rb.Width);
			return Math.Clamp(column, 0, Categories.Length - 1);
		}

		static string ColumnName(int column)
		{
			return FluentProvider.GetMessage(column switch
			{
				0 => DemandResidential,
				1 => DemandCommercial,
				2 => DemandIndustrial,
				_ => DemandOffice
			});
		}

		int DemandOf(int column)
		{
			var model = CityUiContext.For(world).Demand;
			if (model != null)
				return model.GetDemand(Categories[column]);

			return CityUi.GetManager(world)?.GetDemand(Categories[column]) ?? 0;
		}

		string GetTooltip()
		{
			var column = ColumnAt(Viewport.LastMousePos);
			return ColumnName(column) + "\n" + FluentProvider.GetMessage(DemandValue, "value", DemandOf(column));
		}

		string GetFactorTitle()
		{
			var column = ColumnAt(Viewport.LastMousePos);
			return ColumnName(column) + "  " + CityUi.SignedNumber(DemandOf(column));
		}

		IReadOnlyList<DemandFactor> GetFactors()
		{
			var model = CityUiContext.For(world).Demand;
			return model?.GetFactors(Categories[ColumnAt(Viewport.LastMousePos)]);
		}

		public override void MouseEntered()
		{
			if (TooltipContainer == null)
				return;

			// The factor breakdown needs the demand model; without it the legacy single value is shown.
			if (CityUiContext.For(world).Demand != null)
				tooltipContainer.Value.SetTooltip("CITY_FACTOR_TOOLTIP",
					new WidgetArgs { { "getTitle", (Func<string>)GetFactorTitle }, { "getFactors", (Func<IReadOnlyList<DemandFactor>>)GetFactors } });
			else
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "getText", (Func<string>)GetTooltip } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null || !tooltipContainer.IsValueCreated)
				return;

			tooltipContainer.Value.RemoveTooltip();
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var model = CityUiContext.For(world).Demand;
			var manager = CityUi.GetManager(world);
			var font = Game.Renderer.Fonts[Font];
			var columnWidth = rb.Width / Categories.Length;
			var letterWidth = font.Measure("W").X + 4;
			var barWidth = Math.Max(6, columnWidth - letterWidth - 6);
			var barArea = new Rectangle(0, rb.Y + 1, barWidth, rb.Height - 2);
			var mid = barArea.Y + barArea.Height / 2;
			var half = barArea.Height / 2;

			for (var i = 0; i < Categories.Length; i++)
			{
				var left = rb.X + i * columnWidth;
				var x = left + letterWidth;
				var color = CityUi.CategoryColor(Categories[i]);

				// Letter on the left, track, bar and centre line.
				var size = font.Measure(Letters[i]);
				font.DrawTextWithContrast(Letters[i],
					new System.Numerics.Vector2(left + (letterWidth - 2 - size.X) / 2f, rb.Y + (rb.Height - size.Y) / 2f - 1),
					color, Color.FromArgb(220, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 1);

				WidgetUtils.FillRectWithColor(new Rectangle(x - 1, barArea.Y - 1, barWidth + 2, barArea.Height + 2), Color.FromArgb(230, 90, 90, 90));
				WidgetUtils.FillRectWithColor(new Rectangle(x, barArea.Y, barWidth, barArea.Height), Color.FromArgb(255, 10, 10, 14));

				var demand = Math.Clamp(model?.GetDemand(Categories[i]) ?? manager?.GetDemand(Categories[i]) ?? 0, -100, 100);
				var height = Math.Abs(demand) * half / 100;
				if (demand != 0 && height == 0)
					height = 1;

				if (demand > 0)
					WidgetUtils.FillRectWithColor(new Rectangle(x, mid - height, barWidth, height), color);
				else if (demand < 0)
					WidgetUtils.FillRectWithColor(new Rectangle(x, mid, barWidth, height),
						Color.FromArgb(255, color.R * 3 / 4, color.G * 3 / 4, color.B * 3 / 4));

				WidgetUtils.FillRectWithColor(new Rectangle(x, mid, barWidth, 1), Color.White);
			}
		}
	}
}
