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

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>One bevel ring of a box: the top/left and the bottom/right colours (null = leave transparent).</summary>
	public readonly record struct Ring(Rgba? TopLeft, Rgba? BottomRight)
	{
		public Ring(Rgba both)
			: this(both, both) { }
	}

	/// <summary>
	/// Draws pixel-art boxes into a device-pixel canvas. Every line is a whole number of device pixels:
	/// one art pixel is Unit x Unit device pixels.
	/// </summary>
	public sealed class PixelPainter
	{
		public readonly ChromeCanvas Canvas;
		public readonly int Unit;

		public PixelPainter(ChromeCanvas canvas, int unit)
		{
			Canvas = canvas;
			Unit = Math.Max(1, unit);
		}

		/// <summary>Fills a device-pixel rectangle.</summary>
		public void Fill(int x, int y, int w, int h, Rgba c) { Canvas.Fill(x, y, w, h, c); }

		/// <summary>Composites a colour over a device-pixel rectangle.</summary>
		public void Blend(int x, int y, int w, int h, Rgba c) { Canvas.FillBlend(x, y, w, h, c); }

		/// <summary>
		/// Draws nested one-unit bevel rings (outermost first) inside a device-pixel rectangle, then fills the rest.
		/// Top and left edges take the TopLeft colour (including the top-right and bottom-left corners), the bottom and
		/// right edges the BottomRight colour. cut removes the outer corner unit for a rounded pixel corner.
		/// </summary>
		public void Box(int x, int y, int w, int h, IReadOnlyList<Ring> rings, Rgba? fill, bool cut = false)
		{
			var u = Unit;
			for (var i = 0; i < rings.Count; i++)
			{
				var d = i * u;
				int rx = x + d, ry = y + d, rw = w - 2 * d, rh = h - 2 * d;
				if (rw <= 0 || rh <= 0)
					return;

				var r = rings[i];
				if (r.TopLeft is Rgba tl)
				{
					Fill(rx, ry, rw, u, tl);
					Fill(rx, ry, u, rh, tl);
				}

				if (r.BottomRight is Rgba br)
				{
					Fill(rx + u, ry + rh - u, rw - u, u, br);
					Fill(rx + rw - u, ry + u, u, rh - u, br);
				}
			}

			var n = rings.Count * u;
			if (fill is Rgba f && w - 2 * n > 0 && h - 2 * n > 0)
				Fill(x + n, y + n, w - 2 * n, h - 2 * n, f);

			if (cut && rings.Count > 0)
				CutCorners(x, y, w, h, rings.Count > 1 ? rings[0].TopLeft : null);
		}

		/// <summary>Clears the outer corner units; if outline is given, the corner of the next ring takes the outline colour.</summary>
		public void CutCorners(int x, int y, int w, int h, Rgba? outline)
		{
			var u = Unit;
			Canvas.Clear(x, y, u, u);
			Canvas.Clear(x + w - u, y, u, u);
			Canvas.Clear(x, y + h - u, u, u);
			Canvas.Clear(x + w - u, y + h - u, u, u);
			if (outline is Rgba o && w > 4 * u && h > 4 * u)
			{
				Fill(x + u, y + u, u, u, o);
				Fill(x + w - 2 * u, y + u, u, u, o);
				Fill(x + u, y + h - 2 * u, u, u, o);
				Fill(x + w - 2 * u, y + h - 2 * u, u, u, o);
			}
		}

		/// <summary>A horizontal line one unit thick.</summary>
		public void HLine(int x, int y, int w, Rgba c) { Fill(x, y, w, Unit, c); }

		/// <summary>A vertical line one unit thick.</summary>
		public void VLine(int x, int y, int h, Rgba c) { Fill(x, y, Unit, h, c); }

		/// <summary>A small square stud (2x2 units) with a highlight unit.</summary>
		public void Stud(int x, int y, Rgba body, Rgba light, Rgba dark)
		{
			var u = Unit;
			Fill(x, y, 2 * u, 2 * u, body);
			Fill(x, y, u, u, light);
			Fill(x + u, y + u, u, u, dark);
		}
	}
}
