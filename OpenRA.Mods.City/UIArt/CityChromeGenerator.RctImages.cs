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
using System.IO;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	public sealed partial class CityChromeGenerator
	{
		/// <summary>Logical sizes of the icon collections `icons-N` (and `icons-N-disabled` for the first ones).</summary>
		static readonly int[] IconLogicalSizes = [12, 16, 20, 24, 32];
		static readonly int[] DisabledIconSizes = [16, 24];

		/// <summary>Logical sizes of the build-menu thumbnail collections `thumbs-N`.</summary>
		static readonly int[] ThumbLogicalSizes = [64];

		string iconAtlas;
		string thumbAtlas;

		void GenerateRctImages()
		{
			// Close box (always red): normal, hover, pressed
			foreach (var state in new[] { "normal", "hover", "pressed" })
				ctx.AddImage("rct-close", state, CloseBox(state).ToBitmap());

			if (iconAtlas != null)
				GenerateAtlasSet(iconAtlas, "icons", IconLogicalSizes, DisabledIconSizes);

			if (thumbAtlas != null)
				GenerateAtlasSet(thumbAtlas, "thumbs", ThumbLogicalSizes, [.. ThumbLogicalSizes]);
		}

		/// <summary>tools/iso_ui_chrome.close_button: red bevelled square with a white X and its shadow.</summary>
		ChromeCanvas CloseBox(string state)
		{
			var s = S(15) - 2 * bv - S(2);
			var c = new ChromeCanvas(s, s);
			var pressed = state == "pressed";
			Bevel(c, 0, 0, s, s, pressed ? Kind.In : Kind.Out, Rr("red", state == "hover" ? 5 : 4), Rr("red", 7), Rr("red", 1), bv);
			var g = Math.Max(3, s - 2 * bv - S(4));
			var off = pressed ? bv : 0;
			var gx = (s - g) / 2 + off;
			var gy = (s - g) / 2 + off;
			GlyphX(c, gx + 1, gy + 1, g, style.Ink("Shadow"), bv);
			GlyphX(c, gx, gy, g, new Rgba(255, 255, 255), bv);
			return c;
		}

		static void GlyphX(ChromeCanvas c, int x, int y, int size, Rgba color, int t)
		{
			for (var i = 0; i < size; i++)
			{
				c.Fill(x + i, y + i, t, t, color);
				c.Fill(x + size - 1 - i, y + i, t, t, color);
			}
		}

		/// <summary>Solid pixel triangle of size a pointing in a direction (tools/iso_ui_widgets.arrow); canvas is (2a-1) x a or a x (2a-1).</summary>
		static ChromeCanvas Arrow(int a, string dir, Rgba color)
		{
			var vertical = dir is "up" or "down";
			var c = vertical ? new ChromeCanvas(2 * a - 1, a) : new ChromeCanvas(a, 2 * a - 1);
			var x = a - 1;
			for (var i = 0; i < a; i++)
			{
				switch (dir)
				{
					case "up": c.Fill(x - i, i, 2 * i + 1, 1, color); break;
					case "down": c.Fill(x - (a - 1 - i), i, 2 * (a - 1 - i) + 1, 1, color); break;
					case "left": c.Fill(i, x - i, 1, 2 * i + 1, color); break;
					default: c.Fill(i, x - (a - 1 - i), 1, 2 * (a - 1 - i) + 1, color); break;
				}
			}

			return c;
		}

		/// <summary>The tick of checked checkboxes (tools/iso_ui_widgets.tick), in a box of `size` device pixels.</summary>
		ChromeCanvas Tick(int size, Rgba color)
		{
			var c = new ChromeCanvas(size, size);
			var s = S(7);
			var sc = s / 8f;
			var k = bv;
			var dot = Math.Max(k, (int)sc) + k;
			var ox = (size - s) / 2;
			var oy = (size - s) / 2 - S(1) / 2;
			foreach (var (px, py) in new[] { (0, 3), (1, 4), (2, 5), (3, 4), (4, 3), (5, 2), (6, 1) })
				c.Fill(ox + (int)(px * sc), oy + (int)(py * sc), dot, dot, color);

			return c;
		}

		void GenerateFamilyImages(string p, CityFamily f)
		{
			var ink = style.Ink("Text");
			var faint = Rr(f.Ramp, f.Body - 2);
			var a = Math.Max(2, S(3));

			foreach (var dir in new[] { "up", "down", "left", "right" })
			{
				ctx.AddImage(p + "scrollpanel-decorations", dir, Arrow(a, dir, ink).ToBitmap());
				ctx.AddImage(p + "scrollpanel-decorations", dir + "-disabled", Arrow(a, dir, faint).ToBitmap());
			}

			// Dropdown marker: a small raised button with a down arrow
			foreach (var disabled in new[] { false, true })
			{
				var s = S(11);
				var c = new ChromeCanvas(s, s);
				Bevel(c, 0, 0, s, s, Kind.Out, Rr(f.Ramp, f.Body), Rr(f.Ramp, 7), Rr(f.Ramp, 1), bv);
				var arrow = Arrow(a, "down", disabled ? faint : ink);
				c.Blit(arrow, (s - arrow.Width) / 2, (s - arrow.Height) / 2);
				ctx.AddImage(p + "dropdown-decorations", disabled ? "marker-disabled" : "marker", c.ToBitmap());
			}

			foreach (var sep in new[] { "separator", "separator-hover", "separator-pressed", "separator-disabled" })
				ctx.AddImage(p + "dropdown-separators", sep, new ChromeBitmap(0, 0));

			// Checkmarks (ticks and crosses), also as the -highlighted collections
			var box = S(10);
			foreach (var hl in new[] { "", "-highlighted" })
			{
				var tick = "checkmark-" + p + "tick" + hl;
				ctx.AddImage(tick, "checked", Tick(box, ink).ToBitmap());
				ctx.AddImage(tick, "checked-pressed", Tick(box, ink).ToBitmap());
				ctx.AddImage(tick, "checked-disabled", Tick(box, Rr(f.Ramp, 3)).ToBitmap());
				ctx.AddImage(tick, "unchecked", new ChromeBitmap(0, 0));
				ctx.AddImage(tick, "unchecked-pressed", Tick(box, Rr(f.Ramp, 4)).ToBitmap());

				var cross = "checkmark-" + p + "cross" + hl;
				var g = Math.Max(3, box - 2 * S(2));
				foreach (var (name, color) in new[]
				{
					("checked", ink), ("checked-pressed", ink), ("checked-disabled", Rr(f.Ramp, 3)), ("unchecked-pressed", Rr(f.Ramp, 4)),
				})
				{
					var c = new ChromeCanvas(box, box);
					GlyphX(c, (box - g) / 2, (box - g) / 2, g, color, bv);
					ctx.AddImage(cross, name, c.ToBitmap());
				}

				ctx.AddImage(cross, "unchecked", new ChromeBitmap(0, 0));
			}

			// Slider tick marks and the window resize grip
			var t = new ChromeCanvas(bv, S(3));
			t.Fill(0, 0, bv, S(3), Rr(f.Ramp, 2));
			ctx.AddImage(p + "slider", "tick", t.ToBitmap());
			ctx.AddImage(p + "rct-bits", "grip", Grip(f).ToBitmap());
		}

		/// <summary>The resize grip in the bottom-right corner of windows (tools/iso_ui_chrome.window).</summary>
		ChromeCanvas Grip(CityFamily f)
		{
			var size = S(8);
			var c = new ChromeCanvas(size, size);
			for (var i = 0; i < 3; i++)
			{
				for (var j = 0; j <= i; j++)
				{
					var x = S(2) * (2 - j) + S(1) - S(2);
					var y = S(2) * i + S(1) - S(2);
					c.Fill(x + S(2), y + S(2), bv, bv, Rr(f.Ramp, 7));
					c.Fill(x + S(2) + bv, y + S(2) + bv, bv, bv, Rr(f.Ramp, 2));
				}
			}

			return c;
		}

		// ---- icon and thumbnail atlases ------------------------------------------------------------

		/// <summary>The atlas sizes present for an atlas base path (`iso/icons` -> iso/icons-N.png), read from its name list's neighbours.</summary>
		List<(int Size, Png Png)> LoadAtlases(string basePath, out string[] names)
		{
			using (var s = ctx.FileSystem.Open(basePath + ".txt"))
			using (var reader = new StreamReader(s))
				names = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			var found = new List<(int, Png)>();
			for (var n = 4; n <= 256; n++)
			{
				if (!ctx.FileSystem.TryOpen($"{basePath}-{n}.png", out var stream))
					continue;

				using (stream)
					found.Add((n, new Png(stream)));
			}

			return found;
		}

		/// <summary>
		/// Makes collections `prefix-L` (one image per atlas entry, drawn at L logical pixels) for every logical size L.
		/// Each image is the atlas rendering of size a, enlarged by the whole factor k, with a * k the largest such size
		/// that fits the device size (native pixel art, never resampled by a fraction); leftover pixels become margin.
		/// </summary>
		void GenerateAtlasSet(string basePath, string prefix, int[] logicalSizes, int[] disabledSizes)
		{
			var atlases = LoadAtlases(basePath, out var names);
			Log.Write("debug", $"CityChromeGenerator: {atlases.Count} atlases for {basePath} ({names.Length} names).");
			if (atlases.Count == 0)
				return;

			foreach (var logical in logicalSizes)
			{
				var d = S(logical);
				var (size, png, k) = PickAtlas(atlases, d);
				var cell = size + 2;
				var cols = png.Width / cell;
				for (var i = 0; i < names.Length; i++)
				{
					var sx = i % cols * cell + 1;
					var sy = i / cols * cell + 1;
					var img = new ChromeCanvas(d, d);
					var drawn = size * k;
					var off = (d - drawn) / 2;
					for (var y = 0; y < drawn; y++)
					{
						for (var x = 0; x < drawn; x++)
						{
							var px = PngPixel(png, sx + Math.Min(size - 1, x * size / drawn), sy + Math.Min(size - 1, y * size / drawn));
							if (px.A > 0)
								img.Set(off + x, off + y, px);
						}
					}

					ctx.AddImage($"{prefix}-{logical}", names[i], img.ToBitmap());
					if (disabledSizes.Contains(logical))
						ctx.AddImage($"{prefix}-{logical}-disabled", names[i], Embossed(img).ToBitmap());
				}
			}
		}

		/// <summary>The atlas size a and whole factor k with a * k closest to (not above) d; atlases larger than d are shrunk.</summary>
		static (int Size, Png Png, int K) PickAtlas(List<(int Size, Png Png)> atlases, int d)
		{
			var best = (Size: 0, Png: (Png)null, K: 1);
			foreach (var (size, png) in atlases)
			{
				var k = d / size;
				if (k < 1)
					continue;

				if (size * k > best.Size * best.K || (size * k == best.Size * best.K && size > best.Size))
					best = (size, png, k);
			}

			if (best.Png != null)
				return best;

			// Smaller than the smallest atlas: nearest-neighbour shrink of the smallest one
			var smallest = atlases.MinBy(a => a.Size);
			return (smallest.Size, smallest.Png, 1);
		}

		/// <summary>RCT2 disabled look: the silhouette embossed in a light and a dark grey-brown (tools/iso_ui_chrome.disabled_icon).</summary>
		ChromeCanvas Embossed(ChromeCanvas icon)
		{
			// A flat silhouette loses the glyph (two locked buttons side by side read as blobs), so the drawing stays:
			// greyscale with compressed contrast in sand tones, over a light emboss shadow one bevel down-right.
			var c = new ChromeCanvas(icon.Width, icon.Height);
			var light = Rr("sand", 7);
			var dark = Rr("sand", 1);
			var bright = Rr("sand", 6);
			for (var y = 0; y < icon.Height; y++)
				for (var x = 0; x < icon.Width; x++)
					if (icon.Get(x, y).A > 127)
						c.Set(Math.Min(icon.Width - 1, x + bv), Math.Min(icon.Height - 1, y + bv), light);

			for (var y = 0; y < icon.Height; y++)
			{
				for (var x = 0; x < icon.Width; x++)
				{
					var p = icon.Get(x, y);
					if (p.A <= 127)
						continue;

					var l = (0.3f * p.R + 0.59f * p.G + 0.11f * p.B) / 255f;
					c.Set(x, y, Rgba.Lerp(dark, bright, 0.25f + 0.6f * l));
				}
			}

			return c;
		}
	}
}
