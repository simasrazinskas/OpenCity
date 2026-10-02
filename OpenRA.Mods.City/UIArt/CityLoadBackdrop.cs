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
using System.Numerics;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// The darkened isometric city picture behind the loading screens (bits/chrome/loading-backdrop.png, a 1x scene). It is
	/// drawn centred and covering the screen at a whole multiple of device pixels, so the pixel art stays crisp.
	/// </summary>
	public sealed class CityLoadBackdrop : IDisposable
	{
		public const string File = "city|bits/chrome/loading-backdrop.png";

		/// <summary>How much of the night tint is mixed over the picture (0..1).</summary>
		const float Dim = 0.4f;

		readonly IReadOnlyFileSystem fileSystem;
		Sheet sheet;
		Sprite sprite;
		bool failed;

		public CityLoadBackdrop(IReadOnlyFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		/// <summary>Draws the picture to cover the current logical screen (call between BeginUI and EndFrame).</summary>
		public void Draw(Renderer r)
		{
			if (failed)
				return;

			if (sprite == null)
				Create();

			if (sprite == null)
				return;

			var windowScale = r.WindowScale;
			var res = r.Resolution;
			var factor = Math.Max(1, (int)Math.Ceiling(Math.Max(res.Width * windowScale / sprite.Size.X, res.Height * windowScale / sprite.Size.Y) - 0.001f));
			var scale = factor / windowScale;
			var x = MathF.Round((res.Width - sprite.Size.X * scale) / 2 * windowScale) / windowScale;
			var y = MathF.Round((res.Height - sprite.Size.Y * scale) / 2 * windowScale) / windowScale;
			r.RgbaSpriteRenderer.DrawSprite(sprite, new Vector3(x, y, 0), new Vector3(scale, scale, 1));
		}

		void Create()
		{
			try
			{
				Png png;
				using (var s = fileSystem.Open(File))
					png = new Png(s);

				var data = new byte[4 * png.Width * png.Height];
				for (var i = 0; i < png.Width * png.Height; i++)
				{
					byte cr, cg, cb, ca = 255;
					switch (png.Type)
					{
						case SpriteFrameType.Indexed8:
						{
							var c = png.Palette[png.Data[i]];
							(cr, cg, cb, ca) = (c.R, c.G, c.B, c.A);
							break;
						}

						case SpriteFrameType.Rgba32:
							(cr, cg, cb, ca) = (png.Data[4 * i], png.Data[4 * i + 1], png.Data[4 * i + 2], png.Data[4 * i + 3]);
							break;
						case SpriteFrameType.Rgb24:
							(cr, cg, cb) = (png.Data[3 * i], png.Data[3 * i + 1], png.Data[3 * i + 2]);
							break;
						default:
							throw new InvalidDataException($"Unsupported png pixel format {png.Type}.");
					}

					data[4 * i] = Mix(cr, 6);
					data[4 * i + 1] = Mix(cg, 10);
					data[4 * i + 2] = Mix(cb, 34);
					data[4 * i + 3] = ca;
				}

				sheet = new Sheet(SheetType.BGRA, new Size(Exts.NextPowerOf2(png.Width), Exts.NextPowerOf2(png.Height)));
				sheet.CreateBuffer();
				sprite = new Sprite(sheet, new Rectangle(0, 0, png.Width, png.Height), TextureChannel.RGBA, 1f);
				Util.FastCopyIntoChannel(sprite, data, SpriteFrameType.Rgba32);
				sheet.CommitBufferedData();
			}
			catch (Exception e)
			{
				// The backdrop is decoration only: a missing or unreadable picture leaves the screen plain.
				Log.Write("debug", "CityLoadBackdrop: " + e.Message);
				failed = true;
			}
		}

		static byte Mix(byte value, int tint) { return (byte)(value * (1 - Dim) + tint * Dim); }

		public void Dispose()
		{
			sheet?.Dispose();
			sheet = null;
			sprite = null;
		}
	}
}
