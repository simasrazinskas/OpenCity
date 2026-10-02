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
	/// RCT2-style line chart (tools/iso_ui_widgets2.graph, the park rating graph look): a sunken dark green plot area with
	/// dotted grid lines and value labels, one pixel polyline per series (oldest sample left) and a tooltip-style read-out
	/// of the sample under the pointer. Samples are evenly spaced.
	/// </summary>
	public class CityGraphWidget : Widget
	{
		public Func<IReadOnlyList<GraphSeries>> GetSeries = () => [];

		/// <summary>Label of the sample at a given distance from the newest one (0 = newest), e.g. "3 months ago".</summary>
		public Func<int, string> GetSampleLabel = _ => "";

		public string Font = "Tiny";
		public int LeftMargin = 40;

		/// <summary>Draws the value labels left of the plot (set false for small sparkline-style charts).</summary>
		public bool ShowAxis = true;

		/// <summary>Fills the area under the first series with a translucent shade of its colour (the RCT2 park rating look).</summary>
		public bool FillFirst;

		readonly List<KeyValuePair<string, Color>> legend = [];

		/// <summary>Distance of the sample under the pointer from the newest one (0 = newest), -1 when the pointer is elsewhere.</summary>
		public int HoverAgo { get; private set; } = -1;

		public CityGraphWidget() { }

		protected CityGraphWidget(CityGraphWidget other)
			: base(other)
		{
			GetSeries = other.GetSeries;
			GetSampleLabel = other.GetSampleLabel;
			Font = other.Font;
			LeftMargin = other.LeftMargin;
			ShowAxis = other.ShowAxis;
			FillFirst = other.FillFirst;
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

			var family = CityTheme.FamilyOf(this);
			var b = CityTheme.BevelLogical;
			var frame = new Rectangle(rb.X + LeftMargin, rb.Y + 2, rb.Width - LeftMargin - 2, rb.Height - 4);
			CityTheme.DrawPanel(CityTheme.Art(family, "graph"), frame);
			var plot = new Rectangle(frame.X + 2, frame.Y + 3, frame.Width - 4, frame.Height - 5);

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

			HoverAgo = -1;
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
				if (i > 0 && i < 4)
					for (var gx = (float)plot.X; gx < plot.Right; gx += 3)
						CityTheme.Fill(gx, gridY, b, b, CityTheme.InkColor("GraphGrid"));

				if (!ShowAxis)
					continue;

				var text = Compact(value);
				var size = font.Measure(text);
				font.DrawText(text, new Vector2(frame.X - size.X - 3, MathF.Round(y - size.Y / 2f - (i == 0 ? 2 : 0))), CityTheme.Ink);
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

			var first = true;
			foreach (var s in series)
			{
				if (s.Values == null || s.Values.Count == 0)
					continue;

				var length = s.Values.Count;
				var previous = Point(0, length, s.Values[0]);
				if (FillFirst && first && length > 1)
				{
					var shade = Color.FromArgb(70, s.Color.R, s.Color.G, s.Color.B);
					for (var i = 1; i < length; i++)
					{
						var a = Point(i - 1, length, s.Values[i - 1]);
						var c = Point(i, length, s.Values[i]);
						var columns = Math.Max(1, (int)Math.Round((c.X - a.X) / Math.Max(1, b)));
						for (var k = 0; k < columns; k++)
						{
							var x = a.X + (c.X - a.X) * k / columns;
							var top = a.Y + (c.Y - a.Y) * k / columns;
							CityTheme.Fill(x, top, Math.Max(1, b), plot.Bottom - top, shade);
						}
					}
				}

				first = false;
				var dot = 2 * b + (b < 1 ? 0 : b);
				CityTheme.Fill(previous.X - dot / 2, previous.Y - dot / 2, dot, dot, s.Color);
				for (var i = 1; i < length; i++)
				{
					var next = Point(i, length, s.Values[i]);
					color.DrawLine(previous, next, Math.Max(1, b), s.Color);
					if (length <= 24)
						CityTheme.Fill(next.X - dot / 2, next.Y - dot / 2, dot, dot, s.Color);

					previous = next;
				}
			}

			if (hover < 0)
				return;

			HoverAgo = count - 1 - hover;
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

			var lineHeight = font.Measure("Ag").Y + 2;
			var boxHeight = (legend.Count + (title.Length > 0 ? 1 : 0)) * lineHeight + 8;
			var boxX = hx + 8 + width + 10 > plot.Right ? hx - 8 - width - 10 : hx + 8;
			var box = new Rectangle(boxX, plot.Y + 4, width + 10, boxHeight);
			CityTheme.DrawPanel("tooltip", box);
			var ty = box.Y + 4;
			if (title.Length > 0)
			{
				font.DrawText(title, new Vector2(box.X + 4, ty), CityTheme.Ink);
				ty += lineHeight;
			}

			foreach (var line in legend)
			{
				CityTheme.Fill(box.X + 4, ty + lineHeight / 2f - 2, 4, 3, line.Value);
				font.DrawText(line.Key, new Vector2(box.X + 10, ty), CityTheme.Ink);
				ty += lineHeight;
			}
		}
	}
}
