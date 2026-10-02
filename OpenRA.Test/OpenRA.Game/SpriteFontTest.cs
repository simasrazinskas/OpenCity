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
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class SpriteFontTest
	{
		static byte[] ReadFont()
		{
			using var stream = typeof(SpriteFontTest).Assembly.GetManifestResourceStream("OpenRA.Test.FreeSans.ttf");
			using var data = new MemoryStream();
			stream.CopyTo(data);
			return data.ToArray();
		}

		[Test]
		public void OrdinaryGlyphsPreserveAntialiasingCoverage()
		{
			using var font = new FreeTypeFont(ReadFont());
			var glyph = font.CreateGlyph('S', 17, 1);
			Assert.That(glyph.Data.Length, Is.EqualTo(glyph.Size.Width * glyph.Size.Height));
			Assert.That(glyph.Data.Any(p => p > 0 && p < 255), Is.True);
			Assert.That(glyph.Advance, Is.GreaterThan(0));
		}

		[TestCase(1.25f)]
		[TestCase(1.75f)]
		public void FractionalScaleRoundsToClosestDeviceSize(float scale)
		{
			using var font = new FreeTypeFont(ReadFont());
			var scaled = font.CreateGlyph('M', 14, scale);
			var closest = font.CreateGlyph('M', (int)MathF.Floor(14 * scale + 0.5f), 1);
			Assert.That(scaled.Size, Is.EqualTo(closest.Size));
			Assert.That(scaled.Offset, Is.EqualTo(closest.Offset));
			Assert.That(scaled.Advance, Is.EqualTo(closest.Advance));
			Assert.That(scaled.Data, Is.EqualTo(closest.Data));
		}

		[Test]
		public void PixelGlyphsKeepBinaryCoverageAndMissingGlyphFallback()
		{
			using var font = new FreeTypeFont(ReadFont());
			var glyph = font.CreatePixelGlyph('S', 17);
			Assert.That(glyph.Data.All(p => p == 0 || p == 255), Is.True);
			Assert.That(font.CreatePixelGlyph('\uffff', 17).Data, Is.Null);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void PackedBitmapRowsAreExpandedWithoutReadingPadding(bool pixel)
		{
			// The nine columns span two packed bytes per row; the seven padding bits must be ignored.
			const string BitmapFont = """
				STARTFONT 2.1
				FONT -test-font-medium-r-normal--8-80-75-75-c-80-iso10646-1
				SIZE 8 75 75
				FONTBOUNDINGBOX 9 3 0 0
				STARTPROPERTIES 2
				FONT_ASCENT 3
				FONT_DESCENT 0
				ENDPROPERTIES
				CHARS 1
				STARTCHAR A
				ENCODING 65
				SWIDTH 1000 0
				DWIDTH 10 0
				BBX 9 3 0 0
				BITMAP
				8080
				7F00
				FF80
				ENDCHAR
				ENDFONT
				""";
			using var font = new FreeTypeFont(Encoding.ASCII.GetBytes(BitmapFont + "\n"));
			var glyph = pixel ? font.CreatePixelGlyph('A', 8) : font.CreateGlyph('A', 8, 1);
			Assert.That(glyph.Size, Is.EqualTo(new Size(9, 3)));
			Assert.That(glyph.Advance, Is.EqualTo(10));
			Assert.That(glyph.Offset, Is.EqualTo(new int2(0, -3)));
			Assert.That(glyph.Data, Is.EqualTo(new byte[]
			{
				255, 0, 0, 0, 0, 0, 0, 0, 255,
				0, 255, 255, 255, 255, 255, 255, 255, 0,
				255, 255, 255, 255, 255, 255, 255, 255, 255
			}));
		}

		[Test]
		public void WhitespaceKeepsAdvanceWithoutAllocatingSprites()
		{
			var platform = new DefaultPlatform();
			var data = ReadFont();
			using var native = platform.CreateFont(data);
			using var builder = new SheetBuilder(SheetType.BGRA,
				() => throw new InvalidOperationException("Whitespace must not allocate a sheet."));
			using var font = new SpriteFont(platform, "Test", data, 25, 18, 1.5f, builder);
			var advance = (int)native.CreateGlyph(' ', 25, 1.5f).Advance;
			Assert.That(font.Measure("   "), Is.EqualTo(new int2((int)Math.Ceiling(3 * advance / 1.5f), 25)));
			Assert.That(font.Measure("\n"), Is.EqualTo(new int2(0, 50)));
		}

		[Test]
		public void MeasurementUsesCurrentDeviceAdvancesForEveryLine()
		{
			var platform = new DefaultPlatform();
			var data = ReadFont();
			using var native = platform.CreateFont(data);
			using var builder = new SheetBuilder(SheetType.BGRA, 512);
			using var font = new SpriteFont(platform, "Test", data, 25, 18, 1, builder);

			// Sizes above 24 avoid game-only precache profiling and leave global logging state untouched.
			const string Text = "Population 123456789\nCity";
			foreach (var scale in new[] { 0.75f, 1.25f, 1.5f, 2.5f, 1f })
			{
				font.SetScale(scale);
				var width = Text.Split('\n').Max(line => line.Sum(c => (int)native.CreateGlyph(c, 25, scale).Advance));
				Assert.That(font.Measure(Text), Is.EqualTo(new int2((int)Math.Ceiling(width / scale - 0.001f), 50)),
					$"Device scale {scale}");
			}
		}
	}
}
