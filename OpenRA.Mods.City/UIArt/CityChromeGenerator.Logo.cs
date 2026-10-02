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
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// The OpenCity logo: the RCT2-style chunky pixel wordmark (gold gradient, bevel, dark outline, hard drop shadow),
	/// drawn from the pixel mask of the bold pixel font at a whole multiple of device pixels (tools/iso_ui_menus.logo).
	/// </summary>
	public sealed partial class CityChromeGenerator
	{
		/// <summary>The wordmark mask is this many art pixels wide / high, including the padding the outline needs.</summary>
		const int LogoPadCells = 3, LogoShadowCells = 2;

		/// <summary>The word "OpenCity" in the 16 pixel bold face of OpenCityPixel (tools/iso_ui_font.render_mask).</summary>
		static readonly string[] WordmarkRows =
		[
			".####............................####....##................",
			"##..##..........................##..##.........##..........",
			"##..##..#####....####...#####...##.......##...#####..##..##",
			"##..##..##..##..##..##..##..##..##.......##....##....##..##",
			"##..##..##..##..##..##..##..##..##.......##....##....##..##",
			"##..##..##..##..######..##..##..##.......##....##....##..##",
			"##..##..##..##..##......##..##..##.......##....##....##..##",
			"##..##..##..##..##..##..##..##..##..##...##....##.....#####",
			".####...#####....####...##..##...####....##.....###......##",
			"........##...........................................##..##",
			"........##............................................####.",
		];

		static readonly Rgba[] LogoGold =
		[
			Rgba.Parse("#fff6b8"), Rgba.Parse("#ffe46a"), Rgba.Parse("#ffc83a"), Rgba.Parse("#f4a01e"), Rgba.Parse("#dc7a12"), Rgba.Parse("#b0520c")
		];

		static bool[,] Shift(bool[,] m, int dx, int dy)
		{
			int h = m.GetLength(0), w = m.GetLength(1);
			var o = new bool[h, w];
			for (var y = 0; y < h; y++)
			{
				var sy = y - dy;
				if (sy < 0 || sy >= h)
					continue;

				for (var x = 0; x < w; x++)
				{
					var sx = x - dx;
					if (sx >= 0 && sx < w)
						o[y, x] = m[sy, sx];
				}
			}

			return o;
		}

		/// <summary>Square dilation by radius r (separable).</summary>
		static bool[,] Dilate(bool[,] m, int r)
		{
			int h = m.GetLength(0), w = m.GetLength(1);
			var a = new bool[h, w];
			for (var y = 0; y < h; y++)
			{
				for (var x = 0; x < w; x++)
				{
					if (!m[y, x])
						continue;

					for (var xx = Math.Max(0, x - r); xx <= Math.Min(w - 1, x + r); xx++)
						a[y, xx] = true;
				}
			}

			var o = new bool[h, w];
			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
					if (a[y, x])
						for (var yy = Math.Max(0, y - r); yy <= Math.Min(h - 1, y + r); yy++)
							o[yy, x] = true;

			return o;
		}

		static void PaintMask(ChromeCanvas g, bool[,] m, int ox, int oy, Rgba c)
		{
			for (var y = 0; y < m.GetLength(0); y++)
				for (var x = 0; x < m.GetLength(1); x++)
					if (m[y, x])
						g.Set(ox + x, oy + y, c);
		}

		/// <summary>Device pixels of one wordmark art pixel for a logo canvas of the given size (whole, as large as fits).</summary>
		static int LogoPixel(int dw, int dh)
		{
			var cols = WordmarkRows[0].Length + 2 * LogoPadCells + LogoShadowCells;
			var rows = WordmarkRows.Length + 2 * LogoPadCells + LogoShadowCells;
			return Math.Max(1, Math.Min(dw / cols, dh / rows));
		}

		/// <summary>Logical size of the logo image (menu-logos/logo in chrome-menus.yaml).</summary>
		public const int LogoWidth = 420, LogoHeight = 120;

		/// <summary>The logo for the boot load screen, which is drawn before the chrome exists.</summary>
		public static ChromeBitmap RenderLogo(IReadOnlyFileSystem fileSystem, float deviceScale, string stylePath, string iconPath)
		{
			var g = new CityChromeGenerator();
			g.Setup(fileSystem, deviceScale, stylePath, iconPath);
			return g.Logo(LogoWidth, LogoHeight).ToBitmap();
		}

		ChromeCanvas Logo(int w, int h)
		{
			int dw = D(w), dh = D(h);
			var g = new ChromeCanvas(dw, dh, scale);
			var k = LogoPixel(dw, dh);
			var rows = WordmarkRows.Length;
			var cols = WordmarkRows[0].Length;
			var pad = LogoPadCells * k;
			var big = new bool[rows * k + 2 * pad, cols * k + 2 * pad];
			for (var y = 0; y < rows * k; y++)
				for (var x = 0; x < cols * k; x++)
					big[pad + y, pad + x] = WordmarkRows[y / k][x / k] == '#';

			var bh = big.GetLength(0);
			var bw = big.GetLength(1);
			var x0 = (dw - bw) / 2;
			var y0 = (dh - bh - LogoShadowCells * k) / 2;

			var outline = Dilate(big, k);
			PaintMask(g, Shift(outline, LogoShadowCells * k, LogoShadowCells * k), x0, y0, Rgba.Parse("#1a0c04"));
			PaintMask(g, outline, x0, y0, Rgba.Parse("#3a1606"));
			PaintMask(g, Dilate(big, Math.Max(1, k / 2)), x0, y0, Rgba.Parse("#6a2c0a"));

			// Gold gradient by rows, then the bevel: bright top and left edges, dark bottom edge (one art pixel)
			int r0 = int.MaxValue, r1 = 0;
			for (var y = 0; y < bh; y++)
			{
				for (var x = 0; x < bw; x++)
				{
					if (big[y, x])
					{
						r0 = Math.Min(r0, y);
						r1 = Math.Max(r1, y);
					}
				}
			}

			var shiftDown = Shift(big, 0, k);
			var shiftUp = Shift(big, 0, -k);
			var shiftRight = Shift(big, k, 0);
			var n = LogoGold.Length - 1;
			for (var y = 0; y < bh; y++)
			{
				var t = (y - r0) / (float)Math.Max(1, r1 - r0);
				var f = Math.Clamp(t, 0, 1) * n;
				var i = Math.Min(n - 1, (int)f);
				var row = Rgba.Lerp(LogoGold[i], LogoGold[i + 1], f - i);
				for (var x = 0; x < bw; x++)
				{
					if (!big[y, x])
						continue;

					var c = row;
					if (!shiftUp[y, x])
						c = Rgba.Parse("#8a3c08");
					if (!shiftDown[y, x] || !shiftRight[y, x])
						c = Rgba.Parse("#fffbe0");

					g.Set(x0 + x, y0 + y, c);
				}
			}

			return g;
		}
	}
}
