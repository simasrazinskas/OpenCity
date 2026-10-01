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
using System.Globalization;
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	public sealed class GraphSeries
	{
		public string Name;
		public Color Color;
		public IReadOnlyList<int> Values;
	}

	/// <summary>
	/// A small multi-series line chart: dark plot area, 4 grid lines with value labels, one coloured polyline per series
	/// (oldest sample left) and a hover read-out of the sample under the pointer. Samples are evenly spaced.
	/// </summary>
	public class CityGraphWidget : Widget
	{
		public Func<IReadOnlyList<GraphSeries>> GetSeries = () => [];

		/// <summary>Label of the sample at a given distance from the newest one (0 = newest), e.g. "3 months ago".</summary>
		public Func<int, string> GetSampleLabel = _ => "";

		public string Font = "Small";
		public int LeftMargin = 40;

		readonly List<KeyValuePair<string, Color>> legend = [];

		public CityGraphWidget() { }

		protected CityGraphWidget(CityGraphWidget other)
			: base(other)
		{
			GetSeries = other.GetSeries;
			GetSampleLabel = other.GetSampleLabel;
			Font = other.Font;
			LeftMargin = other.LeftMargin;
		}

		public override CityGraphWidget Clone() { return new CityGraphWidget(this); }

		/// <summary>Compact number format: 1,250 -> "1.2k", 3,400,000 -> "3.4M".</summary>
		public static string Compact(long value)
		{
			var abs = Math.Abs(value);
			if (abs >= 1_000_000)
				return (value / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";

			if (abs >= 10_000)
				return (value / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "k";

			return value.ToString(CultureInfo.InvariantCulture);
		}

		static int NiceCeil(int value)
		{
			if (value <= 10)
				return 10;

			var magnitude = 1;
			while (magnitude * 10 <= value)
				magnitude *= 10;

			foreach (var step in new[] { 1, 2, 4, 5, 10 })
				if (step * magnitude >= value)
					return step * magnitude;

			return 10 * magnitude;
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var series = GetSeries();
			var font = Game.Renderer.Fonts[Font];
			var color = Game.Renderer.RgbaColorRenderer;

			var plot = new Rectangle(rb.X + LeftMargin, rb.Y + 4, rb.Width - LeftMargin - 6, rb.Height - 8);
			WidgetUtils.FillRectWithColor(plot, Color.FromArgb(255, 12, 8, 8));
			WidgetUtils.DrawFrame(new Rectangle(plot.X - 1, plot.Y - 1, plot.Width + 2, plot.Height + 2), Color.FromArgb(230, 90, 70, 50));

			var count = 0;
			var min = 0;
			var max = 0;
			var any = false;
			foreach (var s in series)
			{
				if (s.Values == null)
					continue;

				count = Math.Max(count, s.Values.Count);
				foreach (var v in s.Values)
				{
					if (!any)
					{
						min = max = v;
						any = true;
					}

					min = Math.Min(min, v);
					max = Math.Max(max, v);
				}
			}

			if (!any || count == 0)
				return;

			// Scale: start at zero for non-negative data, round the top up to a "nice" number.
			if (min >= 0)
				min = 0;
			else
				min = -NiceCeil(-min);

			max = Math.Max(NiceCeil(Math.Max(max, 1)), min + 10);
			var range = Math.Max(1, max - min);

			for (var i = 0; i <= 4; i++)
			{
				var y = plot.Bottom - i * plot.Height / 4;
				var value = min + (long)range * i / 4;
				var gridY = Math.Min(y, plot.Bottom - 1);
				color.DrawLine(new Vector2(plot.X, gridY), new Vector2(plot.Right, gridY), 1, Color.FromArgb(i == 0 ? 120 : 50, 255, 230, 190));
				var text = Compact(value);
				var size = font.Measure(text);
				font.DrawTextWithShadow(text, new Vector2(plot.X - size.X - 4, y - size.Y / 2f - (i == 0 ? 2 : 0)), CityUi.Muted, Color.Black, 1);
			}

			var span = Math.Max(1, count - 1);
			Vector2 Point(int index, int length, int value)
			{
				// Right-align shorter series so all end at "now".
				var offset = count - length;
				var x = plot.X + (index + offset) * (plot.Width - 1) / span;
				var y = plot.Bottom - 1 - (int)((long)(value - min) * (plot.Height - 2) / range);
				return new Vector2(x, y);
			}

			var mouse = Viewport.LastMousePos;
			var hover = -1;
			if (plot.Contains(mouse) && Ui.MouseOverWidget == this)
			{
				// Snap to the nearest sample.
				hover = (int)Math.Round((mouse.X - plot.X) * (double)span / Math.Max(1, plot.Width - 1));
				hover = Math.Clamp(hover, 0, span);
			}

			foreach (var s in series)
			{
				if (s.Values == null || s.Values.Count == 0)
					continue;

				var length = s.Values.Count;
				var previous = Point(0, length, s.Values[0]);
				if (length == 1)
					WidgetUtils.FillRectWithColor(new Rectangle((int)previous.X - 1, (int)previous.Y - 1, 3, 3), s.Color);

				for (var i = 1; i < length; i++)
				{
					var next = Point(i, length, s.Values[i]);
					color.DrawLine(previous, next, 2, s.Color);
					previous = next;
				}
			}

			if (hover < 0)
				return;

			var hx = plot.X + hover * (plot.Width - 1) / span;
			color.DrawLine(new Vector2(hx, plot.Y), new Vector2(hx, plot.Bottom), 1, Color.FromArgb(140, 255, 255, 255));

			// Read-out: one line per series, placed left or right of the cursor line.
			legend.Clear();
			foreach (var s in series)
			{
				if (s.Values == null || s.Values.Count == 0)
					continue;

				var index = hover - (count - s.Values.Count);
				if (index < 0 || index >= s.Values.Count)
					continue;

				legend.Add(new KeyValuePair<string, Color>(s.Name + ": " + s.Values[index].ToString("N0", CultureInfo.CurrentCulture), s.Color));
			}

			var title = GetSampleLabel(count - 1 - hover);
			var width = font.Measure(title).X;
			foreach (var line in legend)
				width = Math.Max(width, font.Measure(line.Key).X);

			var boxHeight = (legend.Count + (title.Length > 0 ? 1 : 0)) * 14 + 6;
			var boxX = hx + 8 + width + 8 > plot.Right ? hx - 8 - width - 8 : hx + 8;
			var box = new Rectangle(boxX, plot.Y + 4, width + 8, boxHeight);
			WidgetUtils.FillRectWithColor(box, Color.FromArgb(215, 10, 6, 6));
			var ty = box.Y + 3;
			if (title.Length > 0)
			{
				font.DrawTextWithShadow(title, new Vector2(box.X + 4, ty), Color.White, Color.Black, 1);
				ty += 14;
			}

			foreach (var line in legend)
			{
				font.DrawTextWithShadow(line.Key, new Vector2(box.X + 4, ty), line.Value, Color.Black, 1);
				ty += 14;
			}
		}
	}
}
