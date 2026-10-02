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
	/// <summary>One stop of the route strip.</summary>
	public struct RouteStopView
	{
		public string Name;
		public int Waiting;
	}

	/// <summary>One vehicle of the route strip: position along the route in stops (index + fraction, 0..n-1).</summary>
	public struct RouteVehicleView
	{
		public float Position;
	}

	/// <summary>
	/// RCT2 ride-station style route of a transit line (design/iso/ui/panels/transit-line-route.png): the line, a dot per
	/// stop with its name and waiting passengers above and below in turns, and the vehicles between the stops. Routes with
	/// more stops than fit scroll with the mouse wheel or the arrows at the ends. Clicking a stop reports its index.
	/// </summary>
	public class CityRouteStripWidget : Widget
	{
		public const int MinStep = 46;

		public Func<IReadOnlyList<RouteStopView>> GetStops = () => [];
		public Func<IReadOnlyList<RouteVehicleView>> GetVehicles = () => [];
		public Func<Color> GetLineColor = () => Color.Red;
		public Func<int> GetSelected = () => -1;
		public Func<bool> GetLoop = () => false;
		public Func<int, string> GetWaitingText = n => n.ToString(CultureInfo.CurrentCulture);
		public Action<int> OnStopClick = _ => { };
		public string VehicleIcon = "tr_bus";

		const int Pad = 14;
		const int LineHeight = 10;
		const int IconSize = 16;

		int first;
		int visible;
		int count;

		public CityRouteStripWidget() { }

		protected CityRouteStripWidget(CityRouteStripWidget other)
			: base(other)
		{
			GetStops = other.GetStops;
			GetVehicles = other.GetVehicles;
			GetLineColor = other.GetLineColor;
			GetSelected = other.GetSelected;
			GetLoop = other.GetLoop;
			GetWaitingText = other.GetWaitingText;
			OnStopClick = other.OnStopClick;
			VehicleIcon = other.VehicleIcon;
		}

		public override CityRouteStripWidget Clone() { return new CityRouteStripWidget(this); }

		/// <summary>The height needed for the strip: two label blocks, the line and the vehicle icons.</summary>
		public static int PreferredHeight => 4 * LineHeight + 10 + IconSize + 8;

		int Step => visible > 1 ? (Bounds.Width - 2 * Pad) / (visible - 1) : Bounds.Width;

		float StopX(int index) { return Pad + (index - first) * Step; }

		void Layout(int stops)
		{
			count = stops;
			visible = Math.Clamp((Bounds.Width - 2 * Pad) / MinStep + 1, 2, Math.Max(2, stops));
			first = Math.Clamp(first, 0, Math.Max(0, stops - visible));
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (!EventBounds.Contains(mi.Location))
				return false;

			if (mi.Event == MouseInputEvent.Scroll)
			{
				first = Math.Clamp(first - Math.Sign(mi.Delta.Y), 0, Math.Max(0, count - visible));
				return true;
			}

			if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left)
				return false;

			var rb = RenderBounds;
			var x = mi.Location.X - rb.X;

			// The scroll arrows at the clipped ends.
			if (first > 0 && x < Pad / 2 + 4)
			{
				first = Math.Max(0, first - Math.Max(1, visible / 2));
				return true;
			}

			if (first + visible < count && x > rb.Width - Pad / 2 - 4)
			{
				first = Math.Min(count - visible, first + Math.Max(1, visible / 2));
				return true;
			}

			for (var i = first; i < Math.Min(count, first + visible); i++)
			{
				if (Math.Abs(x - StopX(i)) <= Math.Max(6, Step / 2 - 2))
				{
					OnStopClick(i);
					return true;
				}
			}

			return false;
		}

		public override void Draw()
		{
			var stops = GetStops();
			Layout(stops.Count);
			if (stops.Count == 0)
				return;

			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			var font = Game.Renderer.Fonts["Tiny"];
			var b = CityTheme.BevelLogical;
			var line = GetLineColor();
			var ink = CityTheme.Ink;
			var shadow = Color.FromArgb(0xFF, 0x20, 0x10, 0x10);
			const int Block = 2 * LineHeight;
			var ly = rb.Y + Block + 5 + IconSize / 2f;
			const float LineThick = 4f;
			var step = Step;

			// The line, with a dark outline.
			var x0 = rb.X + Pad - 10;
			var width = (visible - 1) * step + 20;
			CityTheme.Fill(x0 - b, ly - LineThick / 2 - b, width + 2 * b, LineThick + 2 * b, shadow);
			CityTheme.Fill(x0, ly - LineThick / 2, width, LineThick, line);
			CityTheme.Fill(x0, ly - LineThick / 2, width, b, Lighten(line, 0.4f));

			// Arrows where the route continues beyond the strip.
			if (first > 0)
				DrawArrow(rb.X + 6, ly, true, ink);

			if (first + visible < count)
				DrawArrow(rb.Right - 6, ly, false, ink);

			var selected = GetSelected();
			for (var i = first; i < Math.Min(count, first + visible); i++)
			{
				var px = rb.X + StopX(i);
				var terminal = i == 0 || i == count - 1;
				var ds = terminal ? 10f : 8f;
				CityTheme.Fill(px - ds / 2 - b, ly - ds / 2 - b, ds + 2 * b, ds + 2 * b, shadow);
				CityTheme.Fill(px - ds / 2, ly - ds / 2, ds, ds, Color.White);
				if (terminal)
					CityTheme.Fill(px - ds / 2 + 2 * b, ly - ds / 2 + 2 * b, ds - 4 * b, ds - 4 * b, line);

				if (i == selected)
					Frame(px - ds / 2 - b - 2, ly - ds / 2 - b - 2, ds + 2 * b + 4, ds + 2 * b + 4, CityTheme.Ramp("yellow", 6), b);

				var up = (i - first) % 2 == 0;
				var name = WidgetUtils.TruncateText(stops[i].Name ?? "", step * 2 - 6, font);
				var waiting = GetWaitingText(stops[i].Waiting);
				var waitingColor = stops[i].Waiting > 60 ? CityTheme.MoneyNegative : stops[i].Waiting > 30 ? CityUi.Warn : CityTheme.MoneyPositive;
				float ty;
				if (up)
				{
					ty = rb.Y + 3;
					CityTheme.Fill(px, ty + 2 * LineHeight, b, ly - ds / 2 - ty - 2 * LineHeight - 1, CityTheme.FamilyShade(family, 3));
				}
				else
				{
					ty = ly + IconSize / 2f + 6;
					CityTheme.Fill(px, ly + ds / 2 + 1, b, ty - ly - ds / 2 - 1, CityTheme.FamilyShade(family, 3));
				}

				DrawLabel(font, name, px, ty, rb, ink);
				DrawLabel(font, waiting, px, ty + LineHeight, rb, waitingColor);
			}

			// Vehicles between the stops.
			var icon = CityTheme.Icon(VehicleIcon, IconSize);
			foreach (var vehicle in GetVehicles())
			{
				var offset = vehicle.Position - first;
				if (offset < -0.2f || offset > visible - 0.8f)
					continue;

				var vx = rb.X + Pad + offset * step - IconSize / 2f;
				var vy = ly - IconSize / 2f;
				CityTheme.Fill(vx - b, vy - b, IconSize + 2 * b, IconSize + 2 * b, Lighten(line, -0.5f));
				CityTheme.Fill(vx, vy, IconSize, IconSize, Lighten(line, 0.75f));
				CityTheme.DrawCentered(icon, vx, vy, IconSize, IconSize);
			}
		}

		static void DrawLabel(SpriteFont font, string text, float px, float y, Rectangle rb, Color color)
		{
			var size = font.Measure(text);
			var x = Math.Clamp(px - size.X / 2f, rb.X + 2, Math.Max(rb.X + 2, rb.Right - 2 - size.X));
			font.DrawText(text, new Vector2(MathF.Round(x), MathF.Round(y)), color);
		}

		static void Frame(float x, float y, float w, float h, Color color, float t)
		{
			CityTheme.Fill(x, y, w, t, color);
			CityTheme.Fill(x, y + h - t, w, t, color);
			CityTheme.Fill(x, y, t, h, color);
			CityTheme.Fill(x + w - t, y, t, h, color);
		}

		static void DrawArrow(float x, float y, bool left, Color color)
		{
			for (var i = 0; i < 4; i++)
			{
				var dx = left ? i : -i;
				CityTheme.Fill(x + dx, y - (4 - i), 1, 2 * (4 - i) + 1, color);
			}
		}

		static Color Lighten(Color c, float t)
		{
			if (t >= 0)
				return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));

			return Color.FromArgb(c.A, (int)(c.R * (1 + t)), (int)(c.G * (1 + t)), (int)(c.B * (1 + t)));
		}
	}
}
