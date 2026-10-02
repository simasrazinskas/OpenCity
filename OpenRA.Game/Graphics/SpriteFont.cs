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
using System.Globalization;
using System.IO;
using System.Numerics;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Graphics
{
	public sealed class SpriteFont : IDisposable
	{
		public int TopOffset { get; }
		readonly int size;
		readonly SheetBuilder builder;
		readonly IFont font;
		readonly Cache<char, GlyphInfo> glyphs;
		readonly Cache<(char C, int Radius), Sprite> contrastGlyphs;
		readonly Cache<int, float[]> dilationElements;

		readonly int ascender;
		readonly PixelFace[] pixelFaces;
		readonly PixelFontMetrics pixelMetrics;

		float deviceScale;

		// Pixel font state for the current device scale
		int pixelScale;
		int embolden;
		float baselineOffset;

		/// <summary>True if this font renders pixel fonts at whole multiples of their design pixel grid.</summary>
		public bool IsPixelFont => pixelFaces.Length > 0;

		/// <summary>Device pixels per font design pixel at the current UI scale (1 for normal fonts).</summary>
		public int PixelScale => IsPixelFont ? pixelScale : 1;

		/// <summary>The pixel face used at the current UI scale, or null for normal fonts.</summary>
		public PixelFace CurrentPixelFace { get; private set; }

		public SpriteFont(IPlatform platform, string name, byte[] data, int size, int ascender, float scale, SheetBuilder builder)
			: this(platform, name, data, size, ascender, scale, builder, [], null) { }

		public SpriteFont(IPlatform platform, string name, byte[] data, int size, int ascender, float scale, SheetBuilder builder,
			PixelFace[] pixelFaces, PixelFontMetrics pixelMetrics)
		{
			if (builder.Type != SheetType.BGRA)
				throw new ArgumentException("The sheet builder must create BGRA sheets.", nameof(builder));

			deviceScale = scale;
			this.size = size;
			this.ascender = ascender;
			this.builder = builder;
			this.pixelFaces = pixelFaces;
			this.pixelMetrics = pixelMetrics ?? new PixelFontMetrics();
			SelectPixelFace();

			font = platform.CreateFont(data);
			glyphs = new Cache<char, GlyphInfo>(c => IsPixelFont ? CreatePixelGlyph(c) : CreateGlyph(c));
			contrastGlyphs = new Cache<(char, int), Sprite>(CreateContrastGlyph);
			dilationElements = new Cache<int, float[]>(CreateCircularWeightMap);

			// Pre-cache small font sizes so glyphs are immediately available when we need them
			if (size <= 24)
				using (new PerfTimer($"Precache {name} {size}px"))
					for (var n = (char)0x20; n < (char)0x7f; n++)
						if (glyphs[n] == null)
							throw new InvalidOperationException();

			TopOffset = size - ascender;
		}

		/// <summary>Makes this pixel font always pick a larger cap height than <paramref name="other"/>.</summary>
		public void SetLargerThan(SpriteFont other)
		{
			pixelMetrics.LargerThan = other;
			SelectPixelFace();
			glyphs.Clear();
			contrastGlyphs.Clear();
		}

		public void SetScale(float scale)
		{
			if (scale == deviceScale)
				return;

			deviceScale = scale;
			SelectPixelFace();
			glyphs.Clear();
			contrastGlyphs.Clear();
		}

		/// <summary>
		/// Pick the pixel face and whole multiple whose cap height best matches the requested size at the current device scale
		/// (but at least the minimum readable cap height). Candidates within the size tolerance are preferred in this order:
		/// real bold faces for bold fonts, then the fewest device pixels per font pixel (most detail), then the smallest
		/// size error. Faces whose text would be much wider than the layout expects are only used if nothing else fits.
		/// </summary>
		void SelectPixelFace()
		{
			baselineOffset = size;
			embolden = 0;
			if (pixelFaces.Length == 0)
				return;

			var m = pixelMetrics;

			// Fonts that must stay larger than another font pick a bigger cap height than its current one
			var minCap = m.MinCapHeight;
			if (m.LargerThan != null && m.LargerThan != this)
			{
				m.LargerThan.SetScale(deviceScale);
				if (m.LargerThan.IsPixelFont)
					minCap = Math.Max(minCap, m.LargerThan.PixelScale * m.LargerThan.CurrentPixelFace.CapHeight + 1);
			}

			var targetCap = Math.Max(size * deviceScale * m.CapRatio, m.MinCapHeight);
			var widthLimit = Math.Max(size * deviceScale, targetCap / m.CapRatio) * m.AdvanceRatio * m.WidthLimit;
			var maxError = MathF.Log(1 + m.SizeTolerance);
			var bestKey = (Class: int.MaxValue, Scale: int.MaxValue, Error: float.MaxValue);
			foreach (var f in pixelFaces)
			{
				for (var k = 1; k <= 64; k++)
				{
					var tooWide = f.AverageAdvance * k > widthLimit;
					if (tooWide && k > 1)
						break;

					var cap = f.CapHeight * k;
					var error = MathF.Abs(MathF.Log(cap / targetCap));
					var weightMismatch = m.Bold && !f.IsBold ? 1 : 0;
					var fits = error <= maxError && !tooWide && cap >= minCap;
					var penalty = (tooWide ? 2 : 0) + (cap < minCap ? 4 : 0);
					var key = fits ? (weightMismatch, k, error) : (2 + weightMismatch + penalty, 0, error);
					if (key.CompareTo(bestKey) < 0)
					{
						bestKey = key;
						CurrentPixelFace = f;
						pixelScale = k;
					}
				}
			}

			// Bold fonts drawn with a regular face are emboldened by half a font pixel (whole device pixels, keeps counters open)
			if (m.Bold && !CurrentPixelFace.IsBold)
				embolden = pixelScale / 2;

			// Keep the cap height centred where the configured ascender would have put it, so vertical alignment
			// in existing layouts works for any face and multiple
			var capHeight = pixelScale * CurrentPixelFace.CapHeight / deviceScale;
			baselineOffset = size - (ascender - capHeight) / 2;
		}

		int2 ScreenOrigin(Vector2 location)
		{
			return new int2(PixelSnap.ToDevice(location.X, deviceScale), PixelSnap.ToDevice(location.Y + baselineOffset, deviceScale));
		}

		int ContrastRadius(int logicalOffset)
		{
			// Pixel fonts outline whole design pixels so the outline is as crisp as the glyphs
			if (IsPixelFont)
				return pixelScale * Math.Max(1, (int)(logicalOffset * deviceScale / pixelScale + 0.5f));

			return (int)(logicalOffset * deviceScale);
		}

		float ShadowOffset(int logicalOffset)
		{
			if (IsPixelFont)
				return Math.Sign(logicalOffset) * ContrastRadius(Math.Abs(logicalOffset)) / deviceScale;

			return (int)(logicalOffset * deviceScale) / deviceScale;
		}

		void DrawTextContrast(string text, Vector2 location, Color contrastColor, int contrastOffset)
		{
			// Calculate positions in screen pixel coordinates
			var screenContrast = ContrastRadius(contrastOffset);
			var screen = ScreenOrigin(location);
			var contrastVector = new Vector2(screenContrast, screenContrast);
			var tint = contrastColor.ToVector3();
			foreach (var s in text)
			{
				if (s == '\n')
				{
					location += new Vector2(0, size);
					screen = ScreenOrigin(location);
					continue;
				}

				var g = glyphs[s];

				// Convert screen coordinates back to UI coordinates for drawing
				if (g.Sprite != null)
				{
					var contrastSprite = contrastGlyphs[(s, screenContrast)];
					Game.Renderer.RgbaSpriteRenderer.DrawSprite(contrastSprite,
						(((screen + g.Offset).ToVector2() - contrastVector) / deviceScale).AsVector3(),
						1f / deviceScale,
						tint, 1f);
				}

				screen += new int2(g.ScreenAdvance, 0);
			}
		}

		public void DrawText(string text, Vector2 location, Color c)
		{
			// Calculate positions in screen pixel coordinates
			var screen = ScreenOrigin(location);
			var tint = c.ToVector3();
			foreach (var s in text)
			{
				if (s == '\n')
				{
					location += new Vector2(0, size);
					screen = ScreenOrigin(location);
					continue;
				}

				var g = glyphs[s];

				// Convert screen coordinates back to UI coordinates for drawing
				if (g.Sprite != null)
					Game.Renderer.RgbaSpriteRenderer.DrawSprite(g.Sprite,
					((screen + g.Offset).ToVector2() / deviceScale).AsVector3(),
					1f / deviceScale,
					tint, 1f);

				screen += new int2(g.ScreenAdvance, 0);
			}
		}

		public void DrawText(string text, Vector2 location, Color c, float angle)
		{
			// Offset from the baseline position to the top-left of the glyph for rendering
			// All positions are calculated in UI coordinates
			var offset = new Vector2(0, baselineOffset);
			var tint = c.ToVector3();
			var transform = Matrix3x2.CreateRotation(-angle) * Matrix3x2.CreateTranslation(location);

			var p = offset;
			foreach (var s in text)
			{
				if (s == '\n')
				{
					offset += new Vector2(0, size);
					p = offset;
					continue;
				}

				var g = glyphs[s];
				if (g.Sprite != null)
				{
					var tl = new Vector2(
						p.X + g.Offset.X / deviceScale,
						p.Y + g.Offset.Y / deviceScale);
					var br = tl + g.Sprite.Size.AsVector2() / deviceScale;
					var tr = new Vector2(br.X, tl.Y);
					var bl = new Vector2(tl.X, br.Y);

					var ra = Vector2.Transform(tl, transform);
					var rb = Vector2.Transform(tr, transform);
					var rc = Vector2.Transform(br, transform);
					var rd = Vector2.Transform(bl, transform);

					// Offset rotated glyph to align the top-left corner with the screen pixel grid
					var screenOffset = new Vector2(PixelSnap.ToDevice(ra.X, deviceScale), PixelSnap.ToDevice(ra.Y, deviceScale)) / deviceScale - ra;

					// Promoted Vector2 positions to Vector3 (with Z = 0) to match DrawSprite's expected signature
					Game.Renderer.RgbaSpriteRenderer.DrawSprite(g.Sprite,
						(ra + screenOffset).AsVector3(),
						(rb + screenOffset).AsVector3(),
						(rc + screenOffset).AsVector3(),
						(rd + screenOffset).AsVector3(),
						tint, 1f);
				}

				p += new Vector2(g.ScreenAdvance / deviceScale, 0);
			}
		}

		public void DrawTextWithContrast(string text, Vector2 location, Color fg, Color bg, int offset)
		{
			if (offset > 0)
				DrawTextContrast(text, location, bg, offset);

			DrawText(text, location, fg);
		}

		public void DrawTextWithContrast(string text, Vector2 location, Color fg, Color bgDark, Color bgLight, int offset)
		{
			DrawTextWithContrast(text, location, fg, GetContrastColor(fg, bgDark, bgLight), offset);
		}

		public void DrawTextWithShadow(string text, Vector2 location, Color fg, Color bg, int offset)
		{
			if (offset != 0)
			{
				// Shadow offsets are rounded to an integer number of screen pixels (font pixels for pixel fonts).
				// This makes sure the shadow will be positioned consistently everywhere on the screen.
				var screenOffset = ShadowOffset(offset);
				DrawText(text, location + new Vector2(screenOffset, screenOffset), bg);
			}

			DrawText(text, location, fg);
		}

		public void DrawTextWithShadow(string text, Vector2 location, Color fg, Color bgDark, Color bgLight, int offset)
		{
			DrawTextWithShadow(text, location, fg, GetContrastColor(fg, bgDark, bgLight), offset);
		}

		public void DrawTextWithShadow(string text, Vector2 location, Color fg, Color bg, int offset, float angle)
		{
			if (offset != 0)
			{
				// Shadow offsets are rounded to an integer number of screen pixels (font pixels for pixel fonts).
				// This makes sure the shadow will be positioned consistently everywhere on the screen.
				var screenOffset = ShadowOffset(offset);
				DrawText(text, location + new Vector2(screenOffset, screenOffset), bg, angle);
			}

			DrawText(text, location, fg, angle);
		}

		public void DrawTextWithShadow(string text, Vector2 location, Color fg, Color bgDark, Color bgLight, int offset, float angle)
		{
			DrawTextWithShadow(text, location, fg, GetContrastColor(fg, bgDark, bgLight), offset, angle);
		}

		public int2 Measure(string text)
		{
			if (string.IsNullOrEmpty(text))
				return new int2(0, size);

			var maxWidth = 0f;
			var rows = 1;
			var start = 0;
			for (var i = 0; i < text.Length; i++)
			{
				if (text[i] != '\n')
					continue;

				maxWidth = Math.Max(maxWidth, LineWidth(text.AsSpan(start, i - start)));
				start = i + 1;
				rows++;
			}

			maxWidth = Math.Max(maxWidth, LineWidth(text.AsSpan(start)));

			return new int2((int)Math.Ceiling(maxWidth - 0.001f), rows * size);
		}

		float LineWidth(ReadOnlySpan<char> line)
		{
			// Sum the same whole-pixel advances that DrawText uses so measurements match the drawn text exactly
			var result = 0;
			foreach (var c in line)
				result += glyphs[c].ScreenAdvance;

			return result / deviceScale;
		}

		GlyphInfo CreateGlyph(char c)
		{
			var glyph = font.CreateGlyph(c, size, deviceScale);
			if (glyph.Data == null || glyph.Size.Width == 0 || glyph.Size.Height == 0)
			{
				return new GlyphInfo
				{
					Sprite = null,
					Advance = glyph.Advance,
					Offset = int2.Zero
				};
			}

			var s = builder.Allocate(glyph.Size);
			var g = new GlyphInfo
			{
				Sprite = s,
				Advance = glyph.Advance,
				Offset = glyph.Offset
			};

			var dest = s.Sheet.GetData();
			var destStride = s.Sheet.Size.Width * 4;

			for (var j = 0; j < s.Size.Y; j++)
			{
				for (var i = 0; i < s.Size.X; i++)
				{
					var p = glyph.Data[j * glyph.Size.Width + i];
					if (p != 0)
					{
						var q = destStride * (j + s.Bounds.Top) + 4 * (i + s.Bounds.Left);
						dest[q] = p;
						dest[q + 1] = p;
						dest[q + 2] = p;
						dest[q + 3] = p;
					}
				}
			}

			s.Sheet.CommitBufferedData(s.Bounds);

			return g;
		}

		GlyphInfo CreatePixelGlyph(char c)
		{
			// Soft hyphens are shown as hyphens
			if (c == '\u00AD')
				return glyphs['-'];

			var face = CurrentPixelFace;
			var k = pixelScale;
			var glyph = face.Font.CreatePixelGlyph(c, face.Grid);

			// Characters that the face lacks come from the other faces at the closest size, or are shown as '?'
			if (glyph.Data == null && c != ' ' && !char.IsControl(c))
			{
				var capDevice = (float)k * face.CapHeight;
				var bestError = float.MaxValue;
				foreach (var f in pixelFaces)
				{
					if (f == face)
						continue;

					var fk = Math.Max(1, (int)MathF.Round(capDevice / f.CapHeight));
					var error = MathF.Abs(MathF.Log(f.CapHeight * fk / capDevice));
					if (error >= bestError)
						continue;

					var g = f.Font.CreatePixelGlyph(c, f.Grid);
					if (g.Data == null)
						continue;

					glyph = g;
					k = fk;
					bestError = error;
				}

				if (glyph.Data == null)
					return c != '?' ? glyphs['?'] : new GlyphInfo { Advance = k * face.Grid / 2 };
			}

			var bold = embolden;
			if (glyph.Data == null || glyph.Size.Width == 0 || glyph.Size.Height == 0)
				return new GlyphInfo { Advance = glyph.Advance * k + bold, Offset = int2.Zero };

			var deviceSize = new Size(glyph.Size.Width * k + bold, glyph.Size.Height * k);
			var device = Upscale(glyph.Data, glyph.Size, k, bold);
			return new GlyphInfo
			{
				Sprite = CopyIntoSheet(device, deviceSize),
				Advance = glyph.Advance * k + bold,
				Offset = new int2(glyph.Offset.X * k, glyph.Offset.Y * k),
				Device = device,
				DeviceSize = deviceSize,
				Scale = k
			};
		}

		/// <summary>Enlarges a design bitmap by k (each font pixel becomes a k x k block) and smears it right by bold device pixels.</summary>
		static byte[] Upscale(byte[] design, Size designSize, int k, int bold)
		{
			var w = designSize.Width * k + bold;
			var h = designSize.Height * k;
			var device = new byte[w * h];
			for (var j = 0; j < h; j++)
			{
				for (var i = 0; i < designSize.Width * k; i++)
				{
					if (design[j / k * designSize.Width + i / k] == 0)
						continue;

					for (var b = 0; b <= bold; b++)
						device[j * w + i + b] = 255;
				}
			}

			return device;
		}

		Sprite CopyIntoSheet(byte[] device, Size deviceSize)
		{
			// Glyph bitmaps are already at device resolution and are drawn 1:1 on the device pixel grid
			var s = builder.Allocate(deviceSize);
			var dest = s.Sheet.GetData();
			var destStride = s.Sheet.Size.Width * 4;
			for (var j = 0; j < deviceSize.Height; j++)
			{
				for (var i = 0; i < deviceSize.Width; i++)
				{
					var p = device[j * deviceSize.Width + i];
					if (p == 0)
						continue;

					var q = destStride * (j + s.Bounds.Top) + 4 * (i + s.Bounds.Left);
					dest[q] = p;
					dest[q + 1] = p;
					dest[q + 2] = p;
					dest[q + 3] = p;
				}
			}

			s.Sheet.CommitBufferedData(s.Bounds);
			return s;
		}

		Sprite CreatePixelContrastGlyph(GlyphInfo glyph, int screenRadius)
		{
			// Dilate by a disc of whole font pixels (offsets in steps of the glyph's pixel scale), so the outline is as
			// blocky and crisp as the glyph. The sprite is padded by screenRadius device pixels on every side.
			var k = glyph.Scale;
			var r = Math.Max(1, screenRadius / k);
			var pad = screenRadius;
			var w = glyph.DeviceSize.Width + 2 * pad;
			var h = glyph.DeviceSize.Height + 2 * pad;
			var outline = new byte[w * h];
			for (var j = 0; j < glyph.DeviceSize.Height; j++)
			{
				for (var i = 0; i < glyph.DeviceSize.Width; i++)
				{
					if (glyph.Device[j * glyph.DeviceSize.Width + i] == 0)
						continue;

					for (var dj = -r; dj <= r; dj++)
					{
						for (var di = -r; di <= r; di++)
						{
							if (di * di + dj * dj > r * r + r)
								continue;

							var x = i + pad + di * k;
							var y = j + pad + dj * k;
							if (x >= 0 && x < w && y >= 0 && y < h)
								outline[y * w + x] = 255;
						}
					}
				}
			}

			return CopyIntoSheet(outline, new Size(w, h));
		}

		float[] CreateCircularWeightMap(int r)
		{
			// Create circular weight maps that are used by CreateContrastGlyph for
			// both the structuring element and to weight the resulting pixel value.
			// The output is a 2 * r + 1 square array giving the pixel intersection
			// with a circle of radius (r + 0.5).
			//
			// Example output for r=1:
			// 0.60 1.00 0.60
			// 1.00 1.00 1.00
			// 0.60 1.00 0.60
			//
			// Example output for r=3:
			// 0.00 0.44 0.80 1.00 0.80 0.44 0.00
			// 0.44 1.00 1.00 1.00 1.00 1.00 0.44
			// 0.80 1.00 1.00 1.00 1.00 1.00 0.80
			// 1.00 1.00 1.00 1.00 1.00 1.00 1.00
			// 0.80 1.00 1.00 1.00 1.00 1.00 0.80
			// 0.44 1.00 1.00 1.00 1.00 1.00 0.44
			// 0.00 0.44 0.80 1.00 0.80 0.44 0.00
			var stride = 2 * r + 1;
			var elem = new float[stride * stride];

			for (var j = 0; j <= 2 * r; j++)
			{
				for (var i = 0; i <= 2 * r; i++)
				{
					var di = i - r;
					var dj = j - r;

					// No intersection with circle
					if (di * di + dj * dj > (r + 1) * (r + 1))
						continue;

					// Fully contained within circle
					if (di * di + dj * dj < (r - 1) * (r - 1))
					{
						elem[j * stride + i] = 1;
						continue;
					}

					// Approximate sub-pixel intersection using a 5x5 grid
					for (var jj = 0; jj < 5; jj++)
					{
						for (var ii = 0; ii < 5; ii++)
						{
							var si = di - (float)Math.Sign(di) * ii / 5;
							var sj = dj - (float)Math.Sign(dj) * jj / 5;
							if (si * si + sj * sj <= r * r)
								elem[j * stride + i] += 0.04f;
						}
					}
				}
			}

			return elem;
		}

		Sprite CreateContrastGlyph((char C, int Radius) c)
		{
			var glyph = glyphs[c.C];
			if (glyph.Device != null)
				return CreatePixelContrastGlyph(glyph, c.Radius);

			var r = c.Radius;

			var s = builder.Allocate(new Size(glyph.Sprite.Bounds.Width + 2 * r, glyph.Sprite.Bounds.Height + 2 * r));
			var dest = s.Sheet.GetData();
			var destStride = s.Sheet.Size.Width * 4;

			var glyphData = glyph.Sprite.Sheet.GetData();
			var glyphStride = glyph.Sprite.Sheet.Size.Width * 4;
			var glyphBounds = glyph.Sprite.Bounds;

			var elem = dilationElements[r];
			var elemStride = 2 * r + 1;

			// Expand the glyph by applying the greyscale dilation operator to the source glyph's alpha channel
			for (var j = 0; j < s.Size.Y; j++)
			{
				for (var i = 0; i < s.Size.X; i++)
				{
					// Apply the weight map to the source glyph and find the largest weighted alpha
					var first = true;
					var alpha = (byte)0;
					for (var wj = 0; wj <= 2 * r; wj++)
					{
						for (var wi = 0; wi <= 2 * r; wi++)
						{
							// Ignore pixels that are outside the source glyph bounds
							var ii = i + wi - 2 * r;
							var jj = j + wj - 2 * r;
							if (ii < 0 || ii >= glyphBounds.Width || jj < 0 || jj >= glyphBounds.Height)
								continue;

							// Weighted alpha for this pixel
							var weighted = (byte)(elem[wj * elemStride + wi] * glyphData[glyphStride * (jj + glyphBounds.Top) + 4 * (ii + glyphBounds.Left) + 3]);
							if (first || weighted > alpha)
							{
								alpha = weighted;
								first = false;
							}
						}
					}

					if (alpha > 0)
					{
						var q = destStride * (j + s.Bounds.Top) + 4 * (i + s.Bounds.Left);
						dest[q] = alpha;
						dest[q + 1] = alpha;
						dest[q + 2] = alpha;
						dest[q + 3] = alpha;
					}
				}
			}

			s.Sheet.CommitBufferedData(s.Bounds);
			return s;
		}

		static Color GetContrastColor(Color fgColor, Color bgDark, Color bgLight)
		{
			return fgColor == Color.White || fgColor.GetBrightness() > 0.33 ? bgDark : bgLight;
		}

		public void Dispose()
		{
			font.Dispose();
			foreach (var f in pixelFaces)
				f.Dispose();
		}
	}

	sealed class GlyphInfo
	{
		public float Advance;
		public int2 Offset;
		public Sprite Sprite;

		// Pixel fonts: the glyph bitmap at device resolution (0 or 255 per pixel) and its device pixels per font pixel,
		// used to build crisp outlines
		public byte[] Device;
		public Size DeviceSize;
		public int Scale = 1;

		public int ScreenAdvance => (int)(Advance + 0.5f);
	}

	/// <summary>How a font picks its pixel face (see the Pixel* fields of FontData).</summary>
	public sealed class PixelFontMetrics
	{
		public bool Bold;
		public SpriteFont LargerThan;
		public float CapRatio = 0.643f;
		public float AdvanceRatio = 0.48f;
		public float WidthLimit = 1.25f;
		public float SizeTolerance = 0.15f;
		public int MinCapHeight = 0;
	}

	/// <summary>A pixel font face: a font whose outlines lie on a pixel grid of <see cref="Grid"/> pixels per em.</summary>
	public sealed class PixelFace : IDisposable
	{
		/// <summary>Text used to measure the average advance of a face (FontData.PixelAdvanceRatio is relative to it).</summary>
		public const string ReferenceText = "The quick brown fox jumps over the lazy dog 0123456789";

		public readonly IFont Font;
		public readonly int Grid;
		public readonly bool IsBold;
		public readonly int CapHeight;
		public readonly float AverageAdvance;

		public PixelFace(IFont font, int grid, bool bold)
		{
			Font = font;
			Grid = grid;
			IsBold = bold;
			var h = font.CreatePixelGlyph('H', grid);
			CapHeight = h.Data != null && h.Size.Height > 0 ? h.Size.Height : Math.Max(1, grid * 2 / 3);

			var total = 0f;
			foreach (var c in ReferenceText)
				total += font.CreatePixelGlyph(c, grid).Advance;

			AverageAdvance = total / ReferenceText.Length;
		}

		/// <summary>Parses `file grid [bold]` (see FontData.PixelFaces).</summary>
		public static PixelFace Parse(IPlatform platform, string spec, Func<string, byte[]> readFile)
		{
			var parts = spec.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2 || parts.Length > 3 || (parts.Length == 3 && parts[2] != "bold"))
				throw new InvalidDataException($"Invalid pixel face `{spec}`: expected `<file> <grid px> [bold]`.");

			var grid = int.Parse(parts[1], NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
			return new PixelFace(platform.CreateFont(readFile(parts[0])), grid, parts.Length == 3);
		}

		public void Dispose()
		{
			Font.Dispose();
		}
	}
}
