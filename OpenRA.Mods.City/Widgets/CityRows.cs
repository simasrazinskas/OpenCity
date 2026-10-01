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
	/// <summary>A "name, slider, value" row (template CITY_SLIDER_ROW) created from code.</summary>
	public sealed class SliderRow
	{
		public Widget Row;
		public ButtonWidget Name;
		public CitySliderWidget Slider;
		public LabelWidget Value;
	}

	public static class CityRows
	{
		/// <summary>
		/// Adds a slider row to a container. `get` is the live value, `commit` fires once on release (issue the order there),
		/// `format` renders the shown value (default "{n}%").
		/// </summary>
		public static SliderRow AddSlider(World world, Widget container, int y, string name, Color nameColor, int min, int max, int step,
			Func<int> get, Action<int> commit, Func<int, string> format = null, Func<bool> disabled = null,
			int width = 560, int nameWidth = 150, int valueWidth = 114)
		{
			var row = Game.LoadWidget(world, "CITY_SLIDER_ROW", container, []);
			row.Bounds.Y = y;
			row.Bounds.Width = width;

			var nameButton = row.Get<ButtonWidget>("NAME");
			nameButton.Bounds.Width = nameWidth;
			nameButton.GetText = () => name;
			nameButton.GetColor = () => nameColor;
			nameButton.Background = "";
			nameButton.VisualHeight = 0;

			var slider = row.Get<CitySliderWidget>("SLIDER");
			slider.MinimumValue = min;
			slider.MaximumValue = max;
			slider.Step = step;
			slider.IsDisabled = disabled ?? (() => false);
			slider.GetValue = get;
			slider.OnCommit = commit;

			var value = row.Get<LabelWidget>("VALUE");
			value.Bounds.Width = valueWidth;
			value.Bounds.X = width - valueWidth;
			value.GetText = () => format != null ? format(slider.DisplayValue) :
				FluentProvider.GetMessage("label-city-percent", "value", slider.DisplayValue);

			// The slider takes the space between the name and the value.
			slider.Bounds.X = nameWidth + 8;
			slider.Bounds.Width = Math.Max(60, width - nameWidth - valueWidth - 16);

			return new SliderRow { Row = row, Name = nameButton, Slider = slider, Value = value };
		}
	}
}
