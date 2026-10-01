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

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>The OpenCity logo (skyline emblem + gold wordmark), ported from tools/uilogo.py.</summary>
	public sealed partial class CityChromeGenerator
	{
		static readonly (float X, float W, float H, int Kind)[] LogoBuildings =
		[
			(-48, 14, 36, 0), (-34, 12, 52, 0), (22, 14, 46, 0), (35, 12, 34, 0),
			(-38, 18, 26, 1), (-14, 16, 64, 1), (6, 18, 78, 1), (26, 18, 42, 1),
			(-22, 18, 34, 2), (-2, 10, 50, 2), (12, 20, 30, 2), (-34, 12, 20, 2), (30, 14, 24, 2),
		];

		static readonly (float T, Rgba C)[] LogoSky =
		[
			(0f, new Rgba(22, 8, 10)), (0.45f, new Rgba(96, 30, 18)), (0.75f, new Rgba(214, 104, 40)), (1f, new Rgba(255, 190, 90))
		];

		static Rgba Sky(float t)
		{
			t = Math.Clamp(t, 0, 1);
			for (var i = 1; i < LogoSky.Length; i++)
				if (t <= LogoSky[i].T)
					return Rgba.Lerp(LogoSky[i - 1].C, LogoSky[i].C, (t - LogoSky[i - 1].T) / (LogoSky[i].T - LogoSky[i - 1].T));

			return LogoSky[^1].C;
		}

		/// <summary>Bronze lit from the top-left (tools/uistyle.metal_frame).</summary>
		static Paint MetalFrame(float w, float h, float ox, float oy)
		{
			var light = new Rgba(246, 190, 108);
			var mid = new Rgba(196, 120, 46);
			var dark = new Rgba(104, 56, 22);
			var sheenColor = new Rgba(255, 236, 190);
			return (x, y) =>
			{
				var t = ((x - ox) / w + (y - oy) / h) / 2f;
				var sheen = MathF.Exp(-MathF.Pow((t - 0.18f) / 0.12f, 2)) * 0.35f;
				var c = t < 0.5f ? Rgba.Lerp(light, mid, Math.Clamp(t * 1.6f, 0, 1)) : Rgba.Lerp(mid, dark, Math.Clamp((t - 0.5f) * 2, 0, 1));
				return Rgba.Lerp(c, sheenColor, sheen);
			};
		}

		ChromeCanvas Logo(int w, int h)
		{
			int dw = D(w), dh = D(h);
			var px = iconPixel;
			int aw = (dw + px - 1) / px, ah = (dh + px - 1) / px;
			var g = new ChromeCanvas(aw, ah, Math.Min(aw / (float)w, ah / (float)h));
			Emblem(g, 128, 82, 66);

			// Gold wordmark: drawn light, tinted with a vertical gradient, then bevelled and outlined
			var word = new ChromeCanvas(aw, (int)Math.Ceiling(80 * g.Scale), g.Scale);
			const float K = 2.08f;
			var width = WordWidth("OpenCity", K);
			Word(word, "OpenCity", 128 - width / 2, 60, K, 4.6f, ChromeCanvas.Solid(new Rgba(250, 228, 180)));
			var top = new Rgba(255, 236, 176);
			var bottom = new Rgba(206, 120, 40);
			var wh = word.Height;
			for (var y = 0; y < wh; y++)
				for (var x = 0; x < word.Width; x++)
				{
					var c = word.Get(x, y);
					if (c.A > 0)
						word.Set(x, y, Rgba.Lerp(top, bottom, y / (float)Math.Max(1, wh - 1)).WithAlpha(c.A));
				}

			word.Stylize(C("Outline"), 1.5f, 1.8f, 1.1f, 1f, 1f);
			g.Blit(word, 0, (int)Math.Round(160 * g.Scale));
			g.RRect(60, 241, 136, 2, 1, ChromeCanvas.Solid(new Rgba(206, 120, 40, 200)));
			return g.Upscale(px).Resize(dw, dh);
		}

		void Emblem(ChromeCanvas g, float cx, float cy, float r)
		{
			var ground = cy + 38;
			g.Circle(cx, cy, r + 8, ChromeCanvas.Solid(new Rgba(0, 0, 0, 90)));
			g.Circle(cx, cy, r + 7, ChromeCanvas.Solid(C("Outline")));
			g.Circle(cx, cy, r + 6, MetalFrame(2 * r + 14, 2 * r + 14, cx - r - 7, cy - r - 7));
			g.Ring(cx, cy, r + 1.5f, 1.2f, ChromeCanvas.Solid(new Rgba(60, 26, 8)));
			g.Circle(cx, cy, r, (_, y) => Sky((y - (cy - r)) / (ground - (cy - r) + 6f)));

			// Sun and moon
			g.Circle(cx + 30, ground - 26, 20, ChromeCanvas.Solid(new Rgba(255, 200, 100, 40)));
			g.Circle(cx + 30, ground - 26, 12, ChromeCanvas.Solid(new Rgba(255, 226, 150)));
			g.Circle(cx - 40, cy - 40, 5, ChromeCanvas.Solid(new Rgba(238, 222, 196)));
			g.Circle(cx - 38, cy - 41, 4.4f, ChromeCanvas.Solid(new Rgba(26, 10, 10, 120)));

			var cols = new[] { new Rgba(104, 44, 26), new Rgba(60, 26, 16), new Rgba(30, 12, 8) };
			foreach (var (bx, bw, bh, kind) in LogoBuildings)
			{
				var x0 = cx + bx;
				g.Rect(x0, ground - bh, bw, bh, cols[kind]);
				g.Rect(x0, ground - bh, bw, 1.2f, kind < 2 ? new Rgba(150, 84, 44) : new Rgba(92, 44, 24));
				if (kind == 1 && bh > 60)
					g.Rect(x0 + bw / 2 - 1, ground - bh - 9, 2, 9, cols[kind]);

				var ny = (int)(bh / 9);
				var nx = Math.Max(1, (int)(bw / 6));
				for (var iy = 0; iy < ny; iy++)
				{
					for (var ix = 0; ix < nx; ix++)
					{
						if ((ix * 3 + iy * 5 + (int)bx) % 4 == 0)
							continue;

						var lit = (ix + iy * 2 + (int)bx) % 3 != 0;
						var wc = lit && kind > 0 ? new Rgba(255, 196, 86) : kind == 0 ? new Rgba(30, 14, 8) : new Rgba(74, 36, 18);
						g.Rect(x0 + 2 + ix * 5.2f, ground - bh + 4 + iy * 8.2f, 2.6f, 3.6f, wc);
					}
				}
			}

			// Ground below the horizon, clipped to the circle
			var a0 = MathF.Asin((ground - cy) / r) * 180 / MathF.PI;
			var seg = new (float, float)[25];
			for (var i = 0; i < 25; i++)
			{
				var a = (a0 + (180 - 2 * a0) * i / 24f) * MathF.PI / 180;
				seg[i] = (cx + r * MathF.Cos(a), cy + r * MathF.Sin(a));
			}

			g.Poly(seg, ChromeCanvas.Solid(new Rgba(18, 8, 5)));
			for (var i = -2; i <= 2; i++)
				g.Rect(cx + i * 17 - 5, ground + 9, 10, 2.4f, new Rgba(232, 156, 56));

			g.Ring(cx, cy, r - 0.6f, 1.2f, ChromeCanvas.Solid(new Rgba(255, 220, 150, 70)));
		}

		static float LogoGlyph(ChromeCanvas g, char ch, float x, float baseline, float k, float sw, Paint c)
		{
			void Ln(float ax, float ay, float bx, float by) => g?.Line(x + ax * k, baseline + ay * k, x + bx * k, baseline + by * k, sw, c);

			void Ar(float acx, float acy, float r, float a0, float a1)
			{
				if (g == null)
					return;

				var steps = Math.Max(6, (int)(Math.Abs(a1 - a0) / 8));
				float px = 0, py = 0;
				for (var i = 0; i <= steps; i++)
				{
					var a = (a0 + (a1 - a0) * i / steps) * MathF.PI / 180;
					var qx = x + acx * k + r * k * MathF.Cos(a);
					var qy = baseline + acy * k + r * k * MathF.Sin(a);
					if (i > 0)
						g.Line(px, py, qx, qy, sw, c);

					(px, py) = (qx, qy);
				}
			}

			switch (ch)
			{
				case 'O': Ar(7, -7, 7, 0, 360); return 15;
				case 'C': Ar(7, -7, 7, 38, 322); return 14;
				case 'p': Ln(1, -10, 1, 4); Ar(6, -5, 5, 0, 360); return 12;
				case 'e': Ln(1.2f, -5, 10.8f, -5); Ar(6, -5, 5.2f, 0, -320); return 12;
				case 'n': Ln(1, -10, 1, 0); Ar(6, -5, 5, 180, 360); Ln(11, -5, 11, 0); return 12;
				case 'i':
					Ln(1, -10, 1, 0);
					g?.Circle(x + k, baseline - 14.6f * k, sw * 0.62f, c);
					return 3;
				case 't': Ln(3, -14, 3, -2.5f); Ar(6.2f, -2.7f, 3.2f, 90, 180); Ln(0, -10, 7.5f, -10); return 8;
				case 'y': Ln(0.5f, -10, 6, 0); Ln(11.5f, -10, 4, 4); return 12;
				default: return 8;
			}
		}

		static float WordWidth(string text, float k, float gap = 3.2f)
		{
			var w = 0f;
			foreach (var ch in text)
				w += LogoGlyph(null, ch, 0, 0, 1, 0.1f, null) + gap;

			return (w - gap) * k;
		}

		static void Word(ChromeCanvas g, string text, float x, float baseline, float k, float sw, Paint c, float gap = 3.2f)
		{
			foreach (var ch in text)
				x += LogoGlyph(g, ch, x, baseline, k, sw, c) * k + gap * k;
		}
	}
}
