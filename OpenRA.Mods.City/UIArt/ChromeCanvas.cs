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
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>A straight-alpha colour with float channels in 0..255.</summary>
	public readonly struct Rgba : IEquatable<Rgba>
	{
		public readonly float R, G, B, A;

		public Rgba(float r, float g, float b, float a = 255)
		{
			R = r;
			G = g;
			B = b;
			A = a;
		}

		public static readonly Rgba Transparent = new(0, 0, 0, 0);

		/// <summary>Parses RRGGBB or RRGGBBAA.</summary>
		public static Rgba Parse(string hex)
		{
			hex = hex.Trim().TrimStart('#');
			if (hex.Length != 6 && hex.Length != 8)
				throw new FormatException($"Invalid colour `{hex}`: expected RRGGBB or RRGGBBAA.");

			var v = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			if (hex.Length == 6)
				v = (v << 8) | 0xFF;

			return new Rgba((v >> 24) & 0xFF, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
		}

		public Rgba WithAlpha(float a) { return new Rgba(R, G, B, a); }
		public Rgba Shade(float f) { return new Rgba(Clamp(R * f), Clamp(G * f), Clamp(B * f), A); }

		public static Rgba Lerp(Rgba a, Rgba b, float t)
		{
			return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
		}

		/// <summary>Mixes towards grey (t = 0 keeps the colour, 1 is fully grey) and scales the brightness.</summary>
		public Rgba Desaturate(float t, float brightness = 1f)
		{
			var l = 0.3f * R + 0.59f * G + 0.11f * B;
			return new Rgba(Clamp((R + (l - R) * t) * brightness), Clamp((G + (l - G) * t) * brightness), Clamp((B + (l - B) * t) * brightness), A);
		}

		static float Clamp(float v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

		public bool Equals(Rgba other) { return R == other.R && G == other.G && B == other.B && A == other.A; }
		public override bool Equals(object obj) { return obj is Rgba o && Equals(o); }
		public override int GetHashCode() { return HashCode.Combine(R, G, B, A); }
		public static bool operator ==(Rgba a, Rgba b) { return a.Equals(b); }
		public static bool operator !=(Rgba a, Rgba b) { return !a.Equals(b); }
	}

	/// <summary>Colour as a function of the design-unit position (gradients).</summary>
	public delegate Rgba Paint(float x, float y);

	/// <summary>
	/// A small software rasterizer for the procedural chrome: pixel-exact fills in device pixels plus
	/// anti-aliased vector shapes in design units (multiplied by Scale), ported from tools/uidraw.py.
	/// </summary>
	public sealed class ChromeCanvas
	{
		public readonly int Width;
		public readonly int Height;

		/// <summary>Device pixels per design unit for the vector shapes.</summary>
		public readonly float Scale;

		// Straight alpha, 0..255, RGBA per pixel
		readonly float[] px;

		public ChromeCanvas(int width, int height, float scale = 1)
		{
			Width = Math.Max(0, width);
			Height = Math.Max(0, height);
			Scale = scale;
			px = new float[4 * Width * Height];
		}

		public Rgba Get(int x, int y)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return Rgba.Transparent;

			var i = 4 * (y * Width + x);
			return new Rgba(px[i], px[i + 1], px[i + 2], px[i + 3]);
		}

		public void Set(int x, int y, Rgba c)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return;

			var i = 4 * (y * Width + x);
			px[i] = c.R;
			px[i + 1] = c.G;
			px[i + 2] = c.B;
			px[i + 3] = c.A;
		}

		/// <summary>Source-over composite of c (scaled by coverage) onto a pixel.</summary>
		public void Blend(int x, int y, Rgba c, float coverage = 1f)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return;

			var sa = c.A * coverage / 255f;
			if (sa <= 0.0001f)
				return;

			var i = 4 * (y * Width + x);
			var da = px[i + 3] / 255f;
			var oa = sa + da * (1 - sa);
			px[i] = (c.R * sa + px[i] * da * (1 - sa)) / oa;
			px[i + 1] = (c.G * sa + px[i + 1] * da * (1 - sa)) / oa;
			px[i + 2] = (c.B * sa + px[i + 2] * da * (1 - sa)) / oa;
			px[i + 3] = oa * 255f;
		}

		/// <summary>Fills (overwrites) a device pixel rectangle.</summary>
		public void Fill(int x, int y, int w, int h, Rgba c)
		{
			for (var yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
				for (var xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
					Set(xx, yy, c);
		}

		/// <summary>Composites a colour over a device pixel rectangle.</summary>
		public void FillBlend(int x, int y, int w, int h, Rgba c)
		{
			for (var yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
				for (var xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
					Blend(xx, yy, c);
		}

		public void Clear(int x, int y, int w, int h)
		{
			Fill(x, y, w, h, Rgba.Transparent);
		}

		public void Blit(ChromeCanvas src, int dx, int dy, bool blend = true)
		{
			for (var y = 0; y < src.Height; y++)
			{
				for (var x = 0; x < src.Width; x++)
				{
					var c = src.Get(x, y);
					if (blend)
						Blend(dx + x, dy + y, c);
					else
						Set(dx + x, dy + y, c);
				}
			}
		}

		/// <summary>Nearest-neighbour upscale by a whole factor.</summary>
		public ChromeCanvas Upscale(int k)
		{
			if (k <= 1)
				return this;

			var o = new ChromeCanvas(Width * k, Height * k, Scale * k);
			for (var y = 0; y < o.Height; y++)
				for (var x = 0; x < o.Width; x++)
					o.Set(x, y, Get(x / k, y / k));

			return o;
		}

		/// <summary>Returns a copy cropped or padded (transparent, centred) to the given size.</summary>
		public ChromeCanvas Resize(int w, int h)
		{
			if (w == Width && h == Height)
				return this;

			var o = new ChromeCanvas(w, h, Scale);
			o.Blit(this, (w - Width) / 2, (h - Height) / 2, false);
			return o;
		}

		/// <summary>Hard alpha edges: pixels below the threshold become transparent, the others opaque.</summary>
		public void ThresholdAlpha(float threshold)
		{
			for (var i = 3; i < px.Length; i += 4)
				px[i] = px[i] >= threshold ? 255 : 0;
		}

		/// <summary>Applies a colour transform to every pixel.</summary>
		public void Map(Func<Rgba, Rgba> f)
		{
			for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
					Set(x, y, f(Get(x, y)));
		}

		public ChromeBitmap ToBitmap()
		{
			var data = new byte[4 * Width * Height];
			for (var i = 0; i < data.Length; i++)
				data[i] = (byte)Math.Clamp((int)Math.Round(px[i]), 0, 255);

			return new ChromeBitmap(Width, Height, data);
		}

		static float Coverage(float d)
		{
			var v = 0.5f - d;
			return v < 0 ? 0 : v > 1 ? 1 : v;
		}

		(int X0, int Y0, int X1, int Y1) Bounds(float x0, float y0, float x1, float y1)
		{
			const int Pad = 2;
			return (Math.Max(0, (int)Math.Floor(x0 * Scale) - Pad), Math.Max(0, (int)Math.Floor(y0 * Scale) - Pad),
				Math.Min(Width, (int)Math.Ceiling(x1 * Scale) + Pad), Math.Min(Height, (int)Math.Ceiling(y1 * Scale) + Pad));
		}

		void PaintAt(int x, int y, Paint p, float a)
		{
			if (a <= 0.003f)
				return;

			var c = p(x / Scale, y / Scale);

			// Match the 8 bit alpha quantisation of the python reference renderer
			var al = (float)Math.Round(c.A * a);
			if (al > 0)
				Blend(x, y, c.WithAlpha(al));
		}

		void Shape((int X0, int Y0, int X1, int Y1) b, Func<float, float, float> sdf, Paint fill, Paint stroke, float sw)
		{
			var swp = sw * Scale;
			for (var y = b.Y0; y < b.Y1; y++)
			{
				for (var x = b.X0; x < b.X1; x++)
				{
					var d = sdf(x + 0.5f, y + 0.5f);
					if (d > 0.5f)
						continue;

					var ao = Coverage(d);
					if (stroke != null && swp > 0)
					{
						var ai = Coverage(d + swp);
						var ring = ao - Math.Min(ao, ai);
						if (fill != null && ai > 0)
							PaintAt(x, y, fill, Math.Min(ai, ao));

						if (ring > 0)
							PaintAt(x, y, stroke, ring);
					}
					else if (fill != null)
						PaintAt(x, y, fill, ao);
				}
			}
		}

		public static Paint Solid(Rgba c) { return (_, _) => c; }

		public void RRect(float x, float y, float w, float h, float r, Paint fill, Paint stroke = null, float sw = 1, float[] radii = null)
		{
			var s = Scale;
			var cx = (x + w / 2) * s;
			var cy = (y + h / 2) * s;
			var hw = w * s / 2;
			var hh = h * s / 2;
			float Sdf(float px, float py)
			{
				var dx = px - cx;
				var dy = py - cy;
				float rad;
				if (radii != null)
				{
					rad = dx < 0 && dy < 0 ? radii[0] : dx >= 0 && dy < 0 ? radii[1] : dx >= 0 && dy >= 0 ? radii[2] : radii[3];
					rad = Math.Min(Math.Min(rad * s, hw), hh);
				}
				else
					rad = Math.Min(Math.Min(r * s, hw), hh);

				var qx = Math.Abs(dx) - hw + rad;
				var qy = Math.Abs(dy) - hh + rad;
				return MathF.Sqrt(MathF.Pow(Math.Max(qx, 0), 2) + MathF.Pow(Math.Max(qy, 0), 2)) + Math.Min(Math.Max(qx, qy), 0) - rad;
			}

			Shape(Bounds(x, y, x + w, y + h), Sdf, fill, stroke, sw);
		}

		public void Circle(float cx, float cy, float r, Paint fill, Paint stroke = null, float sw = 1)
		{
			var s = Scale;
			Shape(Bounds(cx - r, cy - r, cx + r, cy + r), (px, py) => Hypot(px - cx * s, py - cy * s) - r * s, fill, stroke, sw);
		}

		public void Ellipse(float cx, float cy, float rx, float ry, Paint fill)
		{
			var s = Scale;
			float Sdf(float px, float py)
			{
				var k = Hypot((px - cx * s) / (rx * s), (py - cy * s) / (ry * s));
				return (k - 1) * Math.Min(rx * s, ry * s);
			}

			Shape(Bounds(cx - rx, cy - ry, cx + rx, cy + ry), Sdf, fill, null, 0);
		}

		public void Ring(float cx, float cy, float r, float w, Paint c)
		{
			var s = Scale;
			Shape(Bounds(cx - r - w, cy - r - w, cx + r + w, cy + r + w),
				(px, py) => Math.Abs(Hypot(px - cx * s, py - cy * s) - r * s) - w * s / 2, c, null, 0);
		}

		public void Line(float x0, float y0, float x1, float y1, float w, Paint c, bool cap = true)
		{
			var s = Scale;
			float ax = x0 * s, ay = y0 * s, bx = x1 * s, by = y1 * s;
			var hw = w * s / 2;
			float dx = bx - ax, dy = by - ay;
			var ll = dx * dx + dy * dy;
			float Sdf(float px, float py)
			{
				if (ll == 0)
					return Hypot(px - ax, py - ay) - hw;

				var t = ((px - ax) * dx + (py - ay) * dy) / ll;
				if (cap)
					t = t < 0 ? 0 : t > 1 ? 1 : t;
				else if (t < 0 || t > 1)
				{
					var t2 = t < 0 ? 0 : 1;
					return Hypot(px - (ax + dx * t2), py - (ay + dy * t2)) + 1;
				}

				return Hypot(px - (ax + dx * t), py - (ay + dy * t)) - hw;
			}

			Shape(Bounds(Math.Min(x0, x1) - w, Math.Min(y0, y1) - w, Math.Max(x0, x1) + w, Math.Max(y0, y1) + w), Sdf, c, null, 0);
		}

		/// <summary>Anti-aliased polygon fill (4 vertical subsamples, exact horizontal coverage).</summary>
		public void Poly(IReadOnlyList<(float X, float Y)> pts, Paint c)
		{
			const int Sub = 4;
			var n = pts.Count;
			if (n < 3)
				return;

			var p = new (float X, float Y)[n];
			float minY = float.MaxValue, maxY = float.MinValue;
			for (var i = 0; i < n; i++)
			{
				p[i] = (pts[i].X * Scale, pts[i].Y * Scale);
				minY = Math.Min(minY, p[i].Y);
				maxY = Math.Max(maxY, p[i].Y);
			}

			var yLo = Math.Max(0, (int)Math.Floor(minY));
			var yHi = Math.Min(Height, (int)Math.Ceiling(maxY));
			var acc = new float[Width + 2];
			var xs = new List<float>();
			for (var y = yLo; y < yHi; y++)
			{
				Array.Clear(acc);
				var any = false;
				for (var k = 0; k < Sub; k++)
				{
					var sy = y + (k + 0.5f) / Sub;
					xs.Clear();
					for (var i = 0; i < n; i++)
					{
						var (ax, ay) = p[i];
						var (bx, by) = p[(i + 1) % n];
						if ((ay <= sy && sy < by) || (by <= sy && sy < ay))
							xs.Add(ax + (sy - ay) * (bx - ax) / (by - ay));
					}

					xs.Sort();
					for (var i = 0; i + 1 < xs.Count; i += 2)
					{
						float a = xs[i], b = xs[i + 1];
						int xa = (int)Math.Floor(a), xb = (int)Math.Floor(b);
						if (xa == xb)
							Add(acc, xa, (b - a) / Sub);
						else
						{
							Add(acc, xa, (xa + 1 - a) / Sub);
							for (var xx = xa + 1; xx < xb; xx++)
								Add(acc, xx, 1f / Sub);

							Add(acc, xb, (b - xb) / Sub);
						}

						any = true;
					}
				}

				if (!any)
					continue;

				for (var x = 0; x < Width; x++)
					if (acc[x] > 0)
						PaintAt(x, y, c, Math.Min(1f, acc[x]));
			}
		}

		static void Add(float[] acc, int x, float v)
		{
			if (x >= 0 && x < acc.Length)
				acc[x] += v;
		}

		/// <summary>Axis aligned rect, rounded to device pixels.</summary>
		public void Rect(float x, float y, float w, float h, Rgba c)
		{
			var s = Scale;
			var x0 = (int)Math.Round(x * s, MidpointRounding.ToEven);
			var y0 = (int)Math.Round(y * s, MidpointRounding.ToEven);
			FillBlend(x0, y0, Math.Max(1, (int)Math.Round(w * s, MidpointRounding.ToEven)), Math.Max(1, (int)Math.Round(h * s, MidpointRounding.ToEven)), c);
		}

		static float Hypot(float x, float y) { return MathF.Sqrt(x * x + y * y); }

		static float[] BoxBlur(float[] a, int w, int h, int r)
		{
			if (r < 1)
				return a;

			var n = 2 * r + 1;
			var tmp = new float[w * h];
			for (var y = 0; y < h; y++)
			{
				var acc = 0f;
				for (var x = 0; x < Math.Min(r, w); x++)
					acc += a[y * w + x];

				for (var x = 0; x < w; x++)
				{
					if (x + r < w)
						acc += a[y * w + x + r];
					if (x - r - 1 >= 0)
						acc -= a[y * w + x - r - 1];
					tmp[y * w + x] = acc / n;
				}
			}

			var res = new float[w * h];
			for (var x = 0; x < w; x++)
			{
				var acc = 0f;
				for (var y = 0; y < Math.Min(r, h); y++)
					acc += tmp[y * w + x];

				for (var y = 0; y < h; y++)
				{
					if (y + r < h)
						acc += tmp[(y + r) * w + x];
					if (y - r - 1 >= 0)
						acc -= tmp[(y - r - 1) * w + x];
					res[y * w + x] = acc / n;
				}
			}

			return res;
		}

		/// <summary>
		/// Turns flat coloured shapes into bevelled, vertically shaded metal with a dark outline.
		/// outline and bevel are design units; outlinePixels overrides the outline width in device pixels.
		/// </summary>
		public void Stylize(Rgba outlineColor, float outline = 1f, float bevel = 1.3f, float strength = 1f,
			float gradTop = 1.14f, float gradBottom = 0.8f, int outlinePixels = 0)
		{
			int w = Width, h = Height;
			var s = Scale;
			var alpha = new float[w * h];
			for (var i = 0; i < alpha.Length; i++)
				alpha[i] = px[4 * i + 3] / 255f;

			var r = Math.Max(1, (int)Math.Round(bevel * s, MidpointRounding.ToEven));
			var height = BoxBlur(BoxBlur(alpha, w, h, r), w, h, Math.Max(1, r / 2 + 1));
			var k = strength * r * 1.7f;
			var ro = outlinePixels > 0 ? outlinePixels : Math.Max(1, (int)Math.Round(outline * s, MidpointRounding.ToEven));
			var offs = new List<(int X, int Y)>();
			if (outline > 0 || outlinePixels > 0)
				for (var dy = -ro; dy <= ro; dy++)
					for (var dx = -ro; dx <= ro; dx++)
						if (dx * dx + dy * dy <= ro * ro + 0.5f)
							offs.Add((dx, dy));

			var src = (float[])px.Clone();
			for (var y = 0; y < h; y++)
			{
				for (var x = 0; x < w; x++)
				{
					var i = y * w + x;
					var a = alpha[i];
					float or = 0, og = 0, ob = 0, oa = 0;
					if (a < 1f)
					{
						var m = 0f;
						foreach (var (dx, dy) in offs)
						{
							int xx = x + dx, yy = y + dy;
							if (xx >= 0 && yy >= 0 && xx < w && yy < h)
								m = Math.Max(m, alpha[yy * w + xx]);
						}

						m *= 1 - a;
						if (m > 0.01f)
						{
							or = outlineColor.R;
							og = outlineColor.G;
							ob = outlineColor.B;
							oa = (float)Math.Floor(255 * m * 0.95f) / 255f;
						}
					}

					var j = 4 * i;
					if (a <= 0.004f)
					{
						px[j] = or;
						px[j + 1] = og;
						px[j + 2] = ob;
						px[j + 3] = oa * 255;
						continue;
					}

					var gx = height[x + 1 < w ? i + 1 : i] - height[x > 0 ? i - 1 : i];
					var gy = height[y + 1 < h ? i + w : i] - height[y > 0 ? i - w : i];
					var sh = Math.Clamp((gx + gy) * k, -0.55f, 0.55f);
					var f = (gradTop + (gradBottom - gradTop) * (y / (float)Math.Max(1, h - 1))) * (1 + sh);
					var c = new float[3];
					for (var ch = 0; ch < 3; ch++)
						c[ch] = Math.Clamp((float)Math.Floor(src[j + ch] * f), 0, 255);

					if (sh > 0.2f)
					{
						var t = Math.Min(1f, (sh - 0.2f) * 1.8f);
						for (var ch = 0; ch < 3; ch++)
							c[ch] = (float)Math.Floor(c[ch] + (255 - c[ch]) * t * 0.45f);
					}

					var ra = a + oa * (1 - a);
					px[j] = (c[0] * a + or * oa * (1 - a)) / ra;
					px[j + 1] = (c[1] * a + og * oa * (1 - a)) / ra;
					px[j + 2] = (c[2] * a + ob * oa * (1 - a)) / ra;
					px[j + 3] = ra * 255;
				}
			}
		}
	}
}
