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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>The popup of a <see cref="CityDropDown"/>: a sunken list well in the colours of a window family.</summary>
	public sealed class CityMenuPanelWidget : BackgroundWidget, ICityFamilyWidget
	{
		public string Family { get; set; } = CityTheme.DefaultFamily;

		public CityMenuPanelWidget() { }

		CityMenuPanelWidget(CityMenuPanelWidget other)
			: base(other)
		{
			Family = other.Family;
		}

		public override CityMenuPanelWidget Clone() { return new CityMenuPanelWidget(this); }
	}

	/// <summary>
	/// Opens the option list of a DropDownButton in RCT style (design/iso/ui/system/ds-dropdown): 14 px rows on a sunken well,
	/// the chosen row dark, the hovered row light. The shared ScrollItem art has no window-family variant, so rows are code built.
	/// </summary>
	public static class CityDropDown
	{
		public const int RowHeight = 14;

		public static void Show<T>(DropDownButtonWidget dropdown, IReadOnlyList<T> options, Func<T, string> text, Func<T, bool> isSelected,
			Action<T> choose, string family = null, int maxRows = 12)
		{
			family ??= CityTheme.FamilyOf(dropdown);
			var rows = Math.Min(options.Count, maxRows);
			var panel = new CityMenuPanelWidget
			{
				Family = family,
				Background = CityTheme.Art(family, "well"),
				Bounds = new WidgetBounds(0, 0, dropdown.Bounds.Width, rows * RowHeight + 4)
			};

			var width = dropdown.Bounds.Width - 4;
			for (var i = 0; i < rows; i++)
			{
				var option = options[i];
				var row = new ContainerWidget { Bounds = new WidgetBounds(2, 2 + i * RowHeight, width, RowHeight) };
				var background = new CityRowBackgroundWidget
				{
					Zebra = false,
					Bounds = new WidgetBounds(0, 0, width, RowHeight),
					IsSelected = () => isSelected(option)
				};

				var label = text(option);
				var button = new ButtonWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(0, 0, width, RowHeight),
					Background = "",
					VisualHeight = 0,
					Font = "Tiny",
					Align = TextAlign.Left,
					LeftMargin = 4,
					GetText = () => label,
					GetColor = () => isSelected(option) ? CityTheme.InkLight : CityTheme.Ink,
					OnClick = () =>
					{
						choose(option);
						dropdown.RemovePanel();
					}
				};

				row.AddChild(background);
				row.AddChild(button);
				panel.AddChild(row);
			}

			dropdown.AttachPanel(panel);
		}
	}
}
