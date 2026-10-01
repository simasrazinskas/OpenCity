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
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Tooltip with a title and a list of signed factors (demand factors, happiness factors): name on the left,
	/// coloured "+12" / "-8" on the right, strongest first. Template CITY_FACTOR_TOOLTIP.
	/// </summary>
	public class CityFactorTooltipLogic : ChromeLogic
	{
		[FluentReference]
		const string NoFactors = "label-factors-none";

		const int MaxRows = 8;

		[ObjectCreator.UseCtor]
		public CityFactorTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Func<string> getTitle, Func<IReadOnlyList<DemandFactor>> getFactors)
		{
			var title = widget.Get<LabelWidget>("TITLE");
			var name = widget.Get<LabelWidget>("NAME");
			var value = widget.Get<LabelWidget>("VALUE");
			widget.RemoveChildren();

			var titleFont = Game.Renderer.Fonts[title.Font];
			var rowFont = Game.Renderer.Fonts[name.Font];
			var signature = new StringBuilder();
			string cached = null;

			tooltipContainer.BeforeRender = () =>
			{
				var titleText = getTitle();
				var factors = getFactors();
				var sorted = new List<DemandFactor>();
				if (factors != null)
					foreach (var f in factors)
						if (f.Value != 0)
							sorted.Add(f);

				sorted.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
				if (sorted.Count > MaxRows)
					sorted.RemoveRange(MaxRows, sorted.Count - MaxRows);

				signature.Clear();
				signature.Append(titleText);
				foreach (var f in sorted)
					signature.Append('|').Append(f.Key).Append(':').Append(f.Value);

				var current = signature.ToString();
				if (current == cached)
					return;

				cached = current;
				widget.RemoveChildren();

				var width = titleFont.Measure(titleText).X;
				var y = title.Bounds.Y;
				var titleLabel = title.Clone();
				titleLabel.GetText = () => titleText;
				titleLabel.Bounds.Y = y;
				widget.AddChild(titleLabel);
				y += title.Bounds.Height + 2;

				var rows = new List<(string Name, string Value, Color Color)>();
				foreach (var f in sorted)
					rows.Add((CityUi.FactorName(f.Key), CityUi.SignedNumber(f.Value), f.Value > 0 ? CityUi.Good : CityUi.Bad));

				if (rows.Count == 0)
					rows.Add((FluentProvider.GetMessage(NoFactors), "", CityUi.Muted));

				var nameWidth = 0;
				var valueWidth = 0;
				foreach (var r in rows)
				{
					nameWidth = Math.Max(nameWidth, rowFont.Measure(r.Name).X);
					valueWidth = Math.Max(valueWidth, rowFont.Measure(r.Value).X);
				}

				var inner = Math.Max(width, nameWidth + (valueWidth > 0 ? 16 + valueWidth : 0));
				foreach (var r in rows)
				{
					var nameLabel = name.Clone();
					nameLabel.GetText = () => r.Name;
					nameLabel.GetColor = () => r.Color == CityUi.Muted ? r.Color : Color.White;
					nameLabel.Bounds.Y = y;
					nameLabel.Bounds.Width = inner;
					widget.AddChild(nameLabel);

					if (r.Value.Length > 0)
					{
						var valueLabel = value.Clone();
						valueLabel.GetText = () => r.Value;
						valueLabel.GetColor = () => r.Color;
						valueLabel.Bounds.X = name.Bounds.X;
						valueLabel.Bounds.Y = y;
						valueLabel.Bounds.Width = inner;
						widget.AddChild(valueLabel);
					}

					y += name.Bounds.Height;
				}

				widget.Bounds.Width = name.Bounds.X * 2 + inner;
				widget.Bounds.Height = y + 6;
			};
		}
	}
}
