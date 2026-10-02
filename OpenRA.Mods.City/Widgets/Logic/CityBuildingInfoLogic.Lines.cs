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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public enum LineKind : byte { Value, Meter, Header }

	/// <summary>One line of an inspector page: a section header, "label ... value" or "label [meter] value".</summary>
	public struct PageLine
	{
		public LineKind Kind;
		public string Label;
		public string Value;
		public string Ramp;
		public string Tag;
		public int Percent;
		public Color Color;
	}

	struct Chip
	{
		public string Icon;
		public string Text;
		public string Ramp;
	}

	/// <summary>
	/// A fixed pool of CITY_BUILDING_LINE rows that show a list of lines (rebuilt by the logic a few times per second).
	/// Lines beyond the pool size are dropped.
	/// </summary>
	public sealed class LinePool
	{
		public const int HeaderHeight = 16;
		public const int ValueHeight = 14;

		public readonly List<PageLine> Lines = [];
		readonly Widget[] rows;
		readonly int nameWidth;

		public LinePool(
			World world, Widget container, int count, int nameWidth,
			Func<PageLine, string> hoverTitle = null, Func<FactorHoverWidget> hoverFactory = null)
		{
			this.nameWidth = nameWidth;
			rows = new Widget[count];
			for (var i = 0; i < count; i++)
			{
				var index = i;
				var row = Game.LoadWidget(world, "CITY_BUILDING_LINE", container, []);
				rows[i] = row;
				row.Bounds.Width = container.Bounds.Width;
				row.IsVisible = () => index < Lines.Count;

				var header = row.Get<CityHeaderWidget>("HEADER");
				header.GetText = () => At(index).Label;
				header.IsVisible = () => At(index).Kind == LineKind.Header;

				var name = row.Get<LabelWidget>("NAME");
				name.Bounds.Width = nameWidth;
				name.GetText = CityUi.Fitted(name, () => At(index).Label);
				name.IsVisible = () => At(index).Kind != LineKind.Header;

				var bar = row.Get<CityBarWidget>("BAR");
				bar.Bounds.X = nameWidth + 2;
				bar.Bounds.Width = row.Bounds.Width - nameWidth - 2 - 58;
				bar.IsVisible = () => At(index).Kind == LineKind.Meter;
				bar.GetPercentage = () => At(index).Percent;
				bar.GetRamp = () => At(index).Ramp;

				var value = row.Get<LabelWidget>("VALUE");
				value.IsVisible = () => At(index).Kind != LineKind.Header;
				value.GetText = () => At(index).Value;
				value.GetColor = () => At(index).Color;

				if (hoverFactory != null)
				{
					var hover = hoverFactory();
					hover.Bounds = new WidgetBounds(0, 0, row.Bounds.Width, ValueHeight);
					hover.IsVisible = () => At(index).Tag == "happiness";
					hover.GetTitle = () => hoverTitle(At(index));
					row.AddChild(hover);
				}
			}
		}

		PageLine At(int index) { return index < Lines.Count ? Lines[index] : default; }

		public int Count => Lines.Count;

		public void Clear() { Lines.Clear(); }

		bool Add(PageLine line)
		{
			if (Lines.Count >= rows.Length)
				return false;

			Lines.Add(line);
			return true;
		}

		public void AddHeader(string label) { Add(new PageLine { Kind = LineKind.Header, Label = label }); }

		public void AddValue(string label, string value, Color? color = null, string tag = null)
		{
			Add(new PageLine { Kind = LineKind.Value, Label = label, Value = value, Color = color ?? CityTheme.Ink, Tag = tag });
		}

		public void AddMeter(string label, string value, int percent, string ramp, string tag = null)
		{
			Add(new PageLine
			{
				Kind = LineKind.Meter,
				Label = label,
				Value = value,
				Percent = Math.Clamp(percent, 0, 100),
				Ramp = ramp,
				Color = CityTheme.Ink,
				Tag = tag,
			});
		}

		/// <summary>Positions the rows after the lines changed.</summary>
		public void Apply()
		{
			var y = 0;
			for (var i = 0; i < rows.Length; i++)
			{
				rows[i].Bounds.Y = y;
				if (i >= Lines.Count)
					continue;

				var line = Lines[i];
				var value = rows[i].Get<LabelWidget>("VALUE");
				var width = rows[i].Bounds.Width;

				// Meters leave room for the bar, plain values use the right part of the row.
				var valueWidth = line.Kind == LineKind.Meter ? 58 : width - nameWidth;
				value.Bounds.X = width - valueWidth;
				value.Bounds.Width = valueWidth;
				y += line.Kind == LineKind.Header ? HeaderHeight : ValueHeight;
			}
		}
	}
}
