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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	/// <summary>
	/// Helpers for mapping UI coordinates (logical pixels) onto whole device pixels.
	/// The UI is laid out in logical pixels and drawn at <c>Game.Renderer.WindowScale</c> device pixels per logical pixel,
	/// which may be fractional (e.g. 1.25, 1.5). Snapping keeps edges, lines and texels on the device pixel grid.
	/// </summary>
	public static class PixelSnap
	{
		/// <summary>Spans narrower than this many device pixels keep a uniform width independent of their position.</summary>
		public const float ThinSpan = 4f;

		// Bias so that edges exactly half way between device pixels resolve consistently despite float error.
		const float Bias = 1e-4f;

		/// <summary>
		/// Device pixel index of a logical edge coordinate, matching the rasterizer's pixel-centre rule
		/// (at integer scales this is exactly the edge the GPU would pick for an unsnapped quad).
		/// </summary>
		public static int ToDevice(float logical, float scale)
		{
			return (int)MathF.Ceiling(logical * scale - 0.5f - Bias);
		}

		/// <summary>Logical coordinate of the nearest device pixel edge.</summary>
		public static float Snap(float logical, float scale)
		{
			return scale > 0 ? ToDevice(logical, scale) / scale : logical;
		}

		/// <summary>Whole number of device pixels used for a span of the given logical length (at least 1 if non-zero).</summary>
		public static int DeviceLength(float logical, float scale)
		{
			if (logical == 0)
				return 0;

			var d = MathF.Abs(logical * scale);
			return Math.Sign(logical) * Math.Max(1, (int)MathF.Floor(d + 0.5f));
		}

		/// <summary>
		/// Snaps a 1D span (origin and signed length, in logical pixels) to whole device pixels.
		/// Wide spans snap both edges independently so that adjacent spans tile without gaps;
		/// thin spans (lines, borders) keep a uniform device width.
		/// </summary>
		public static void SnapSpan(float scale, ref float origin, ref float length)
		{
			if (scale <= 0)
				return;

			var a = ToDevice(origin, scale);
			var d = length * scale;
			var b = MathF.Abs(d) < ThinSpan ? a + DeviceLength(length, scale) : ToDevice(origin + length, scale);
			origin = a / scale;
			length = (b - a) / scale;
		}

		/// <summary>Converts a logical rectangle to the device pixel rectangle that the UI pass will cover.</summary>
		public static Rectangle ToDevice(Rectangle r, float scale)
		{
			float x = r.X, y = r.Y, w = r.Width, h = r.Height;
			SnapSpan(scale, ref x, ref w);
			SnapSpan(scale, ref y, ref h);
			var x0 = (int)MathF.Round(x * scale);
			var y0 = (int)MathF.Round(y * scale);
			return new Rectangle(x0, y0, (int)MathF.Round((x + w) * scale) - x0, (int)MathF.Round((y + h) * scale) - y0);
		}
	}
}
