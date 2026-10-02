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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "SA1310:FieldNamesMustNotContainUnderscore",
		Justification = "C-style naming is kept for consistency with the underlying native API.")]
	static class FreeType
	{
		internal const uint OK = 0x00;
		internal const int FT_LOAD_NO_HINTING = 0x02;
		internal const int FT_LOAD_RENDER = 0x04;
		internal const int BitmapRowsOffset = 0; // offsetof(FT_Bitmap, rows)
		internal const int BitmapWidthOffset = 4; // offsetof(FT_Bitmap, width)

		internal const int BitmapPitchOffset = 8; // offsetof(FT_Bitmap, pitch)
		internal static readonly int FaceRecGlyphOffset = IntPtr.Size == 8 ? 152 : 84; // offsetof(FT_FaceRec, glyph)
		internal static readonly int GlyphSlotMetricsOffset = IntPtr.Size == 8 ? 48 : 24; // offsetof(FT_GlyphSlotRec, metrics)
		internal static readonly int GlyphSlotBitmapOffset = IntPtr.Size == 8 ? 152 : 76; // offsetof(FT_GlyphSlotRec, bitmap)
		internal static readonly int GlyphSlotBitmapLeftOffset = IntPtr.Size == 8 ? 192 : 100; // offsetof(FT_GlyphSlotRec, bitmap_left)
		internal static readonly int GlyphSlotBitmapTopOffset = IntPtr.Size == 8 ? 196 : 104; // offsetof(FT_GlyphSlotRec, bitmap_top)
		internal static readonly int MetricsAdvanceOffset = IntPtr.Size == 8 ? 32 : 16; // offsetof(FT_Glyph_Metrics, horiAdvance)
		internal static readonly int BitmapBufferOffset = IntPtr.Size == 8 ? 16 : 12; // offsetof(FT_Bitmap, buffer)
		internal static readonly int BitmapPixelModeOffset = IntPtr.Size == 8 ? 26 : 18; // offsetof(FT_Bitmap, pixel_mode)

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_Init_FreeType(out IntPtr library);

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_New_Memory_Face(IntPtr library, IntPtr file_base, int file_size, int face_index, out IntPtr aface);

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_Done_Face(IntPtr face);

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_Set_Pixel_Sizes(IntPtr face, uint pixel_width, uint pixel_height);

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_Load_Char(IntPtr face, uint char_code, int load_flags);

		[DllImport("freetype6", CallingConvention = CallingConvention.Cdecl)]
		internal static extern uint FT_Get_Char_Index(IntPtr face, uint charcode);
	}

	public sealed class FreeTypeFont : IFont
	{
		static readonly FontGlyph EmptyGlyph = new()
		{
			Offset = int2.Zero,
			Size = new Size(0, 0),
			Advance = 0,
			Data = null
		};

		static IntPtr library = IntPtr.Zero;
		readonly GCHandle faceHandle;
		readonly IntPtr face;
		bool disposed;

		public FreeTypeFont(byte[] data)
		{
			if (library == IntPtr.Zero && FreeType.FT_Init_FreeType(out library) != FreeType.OK)
				throw new InvalidOperationException("Failed to initialize FreeType");

			faceHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
			if (FreeType.FT_New_Memory_Face(library, faceHandle.AddrOfPinnedObject(), data.Length, 0, out face) != FreeType.OK)
				throw new InvalidDataException("Failed to initialize font");
		}

		public FontGlyph CreateGlyph(char c, int size, float deviceScale)
		{
			// Round to the closest device pixel instead of always making fractional UI sizes smaller.
			var scaledSize = (uint)Math.Max(1, MathF.Floor(size * deviceScale + 0.5f));
			if (FreeType.FT_Set_Pixel_Sizes(face, scaledSize, scaledSize) != FreeType.OK)
				return EmptyGlyph;

			if (FreeType.FT_Load_Char(face, c, FreeType.FT_LOAD_RENDER) != FreeType.OK)
				return EmptyGlyph;

			return ReadGlyph(false);
		}

		public FontGlyph CreatePixelGlyph(char c, int pixelSize)
		{
			if (FreeType.FT_Get_Char_Index(face, c) == 0)
				return EmptyGlyph;

			if (FreeType.FT_Set_Pixel_Sizes(face, (uint)pixelSize, (uint)pixelSize) != FreeType.OK)
				return EmptyGlyph;

			// Pixel fonts are drawn on whole pixels at their design size: hinting would only distort them
			if (FreeType.FT_Load_Char(face, c, FreeType.FT_LOAD_RENDER | FreeType.FT_LOAD_NO_HINTING) != FreeType.OK)
				return EmptyGlyph;

			return ReadGlyph(true);
		}

		FontGlyph ReadGlyph(bool threshold)
		{
			// HACK: This uses raw pointer offsets to avoid defining structs and types that are 95% unnecessary.
			var glyph = Marshal.ReadIntPtr(IntPtr.Add(face, FreeType.FaceRecGlyphOffset)); // face->glyph
			var metrics = IntPtr.Add(glyph, FreeType.GlyphSlotMetricsOffset); // face->glyph->metrics
			var metricsAdvance = Marshal.ReadIntPtr(IntPtr.Add(metrics, FreeType.MetricsAdvanceOffset));

			var bitmap = IntPtr.Add(glyph, FreeType.GlyphSlotBitmapOffset); // face->glyph->bitmap
			var rows = Marshal.ReadInt32(IntPtr.Add(bitmap, FreeType.BitmapRowsOffset));
			var width = Marshal.ReadInt32(IntPtr.Add(bitmap, FreeType.BitmapWidthOffset));
			var pitch = Marshal.ReadInt32(IntPtr.Add(bitmap, FreeType.BitmapPitchOffset));
			var buffer = Marshal.ReadIntPtr(IntPtr.Add(bitmap, FreeType.BitmapBufferOffset));
			var pixelMode = Marshal.ReadByte(IntPtr.Add(bitmap, FreeType.BitmapPixelModeOffset));
			var bitmapLeft = Marshal.ReadInt32(IntPtr.Add(glyph, FreeType.GlyphSlotBitmapLeftOffset));
			var bitmapTop = Marshal.ReadInt32(IntPtr.Add(glyph, FreeType.GlyphSlotBitmapTopOffset));

			var g = new FontGlyph
			{
				// The rasterized bitmap bounds can differ from the outline metrics.
				// Copy its actual width and rows, and keep the pen advance separate.
				Advance = ((int)metricsAdvance + 32) >> 6,
				Offset = new int2(bitmapLeft, -bitmapTop),
				Size = new Size(width, rows),
				Data = new byte[width * rows]
			};

			if (width == 0 || rows == 0)
				return g;

			// Outlines render as 8-bit grayscale. Embedded bitmap fonts may use packed coverage instead.
			var bits = pixelMode switch
			{
				1 => 1, // FT_PIXEL_MODE_MONO
				2 => 8, // FT_PIXEL_MODE_GRAY
				3 => 2, // FT_PIXEL_MODE_GRAY2
				4 => 4, // FT_PIXEL_MODE_GRAY4
				_ => throw new InvalidDataException($"Unsupported font bitmap pixel mode: {pixelMode}.")
			};

			unsafe
			{
				// FreeType's signed pitch is the offset to the next row, including any padding.
				var p = (byte*)buffer;
				var mask = (1 << bits) - 1;
				for (var j = 0; j < rows; j++)
				{
					for (var i = 0; i < width; i++)
					{
						var shift = 8 - bits - i * bits % 8;
						var coverage = (byte)(((p[i * bits / 8] >> shift) & mask) * 255 / mask);
						g.Data[j * width + i] = threshold ? (coverage >= 128 ? (byte)255 : (byte)0) : coverage;
					}

					p += pitch;
				}
			}

			return g;
		}

		public void Dispose()
		{
			if (!disposed && faceHandle.IsAllocated)
			{
				FreeType.FT_Done_Face(face);

				faceHandle.Free();
				disposed = true;
			}
		}
	}
}
