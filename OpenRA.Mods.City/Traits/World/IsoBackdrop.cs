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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("RCT2-style void around the isometric (diamond) map: a dark backdrop with a faint tile lattice,",
		"and the earth 'skirt' below the two front edges of the map. Render-only; does nothing in the top-down projection.")]
	public class IsoBackdropInfo : TraitInfo
	{
		[Desc("Void colour.")]
		public readonly Color Background = Color.FromArgb(14, 16, 21);

		[Desc("Colour of the faint diamond lattice drawn on the void.")]
		public readonly Color Lattice = Color.FromArgb(22, 26, 33);

		[Desc("Height (world px) of the earth skirt below the front map edges. 0 disables it.")]
		public readonly int SkirtDepth = 24;

		[Desc("Skirt bands from top to bottom: grass lip, soil, rock (lit face, normal +Y).")]
		public readonly Color[] SkirtLit = [Color.FromArgb(79, 122, 51), Color.FromArgb(122, 90, 58), Color.FromArgb(107, 98, 89)];

		[Desc("Skirt bands from top to bottom for the shaded face (normal +X).")]
		public readonly Color[] SkirtShaded = [Color.FromArgb(62, 97, 40), Color.FromArgb(90, 64, 41), Color.FromArgb(78, 71, 64)];

		[Desc("Band heights (world px) from the top; the last band fills the rest of the skirt.")]
		public readonly int[] SkirtBands = [2, 10];

		public override object Create(ActorInitializer init) { return new IsoBackdrop(this); }
	}

	public sealed class IsoBackdrop : IRenderBackdrop, IWorldLoaded, INotifyActorDisposing
	{
		// The lattice texture holds 8 x 8 tiles so few quads cover the screen; the pattern repeats per tile.
		const int TilesPerSide = 8;

		readonly IsoBackdropInfo info;
		Sheet sheet;
		Sprite pattern;
		Size tileSize;
		int2 anchor;

		public IsoBackdrop(IsoBackdropInfo info)
		{
			this.info = info;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			if (!wr.IsIsometric)
				return;

			tileSize = wr.TileSize;
			var size = new Size(tileSize.Width * TilesPerSide, tileSize.Height * TilesPerSide);
			sheet = new Sheet(SheetType.BGRA, size.NextPowerOf2());
			var data = sheet.GetData();
			var stride = sheet.Size.Width;
			var bg = info.Background.ToArgb();
			var line = info.Lattice.ToArgb();

			// 2:1 pixel stairs through every cell corner: x + 2y or x - 2y is (almost) a multiple of the tile width.
			var tw = tileSize.Width;
			for (var y = 0; y < size.Height; y++)
			{
				for (var x = 0; x < size.Width; x++)
				{
					var a = ((x + 2 * y) % tw + tw) % tw;
					var b = ((x - 2 * y) % tw + tw) % tw;
					var c = a < 2 || b < 2 ? line : bg;
					var i = 4 * (y * stride + x);
					data[i] = (byte)(c & 0xFF);
					data[i + 1] = (byte)((c >> 8) & 0xFF);
					data[i + 2] = (byte)((c >> 16) & 0xFF);
					data[i + 3] = 0xFF;
				}
			}

			sheet.CommitBufferedData();
			pattern = new Sprite(sheet, new Rectangle(0, 0, size.Width, size.Height), TextureChannel.RGBA);

			// Align the lattice with the cell corners (world origin).
			var origin = wr.ScreenPxPosition(WPos.Zero);
			anchor = new int2(Mod(origin.X, tw), Mod(origin.Y, tileSize.Height));
		}

		static int Mod(int a, int m) => (a % m + m) % m;

		void IRenderBackdrop.RenderBackdrop(WorldRenderer wr)
		{
			if (pattern == null)
				return;

			var vp = wr.Viewport;
			var tl = vp.TopLeft;
			var br = vp.BottomRight;
			var pw = (int)pattern.Size.X;
			var ph = (int)pattern.Size.Y;
			var x0 = tl.X - Mod(tl.X - anchor.X, pw);
			var y0 = tl.Y - Mod(tl.Y - anchor.Y, ph);
			var rgba = Game.Renderer.WorldRgbaSpriteRenderer;
			for (var y = y0; y < br.Y; y += ph)
				for (var x = x0; x < br.X; x += pw)
					rgba.DrawSprite(pattern, new Vector3(x, y, 0));

			if (info.SkirtDepth > 0)
				DrawSkirt(wr);
		}

		void DrawSkirt(WorldRenderer wr)
		{
			var map = wr.World.Map;
			var b = map.Bounds;
			var ts = map.Grid.TileScale;
			var left = wr.ScreenPxPosition(new WPos(b.Left * ts, b.Bottom * ts, 0));
			var bottom = wr.ScreenPxPosition(new WPos(b.Right * ts, b.Bottom * ts, 0));
			var right = wr.ScreenPxPosition(new WPos(b.Right * ts, b.Top * ts, 0));

			// Lit face below the front-left edge (normal +Y), shaded face below the front-right edge (normal +X).
			DrawFace(left, bottom, info.SkirtLit);
			DrawFace(bottom, right, info.SkirtShaded);
		}

		void DrawFace(int2 from, int2 to, Color[] colors)
		{
			var top = 0;
			var cr = Game.Renderer.WorldRgbaColorRenderer;
			for (var i = 0; i < colors.Length && top < info.SkirtDepth; i++)
			{
				var bottom = i < info.SkirtBands.Length ? Math.Min(info.SkirtDepth, top + info.SkirtBands[i]) : info.SkirtDepth;
				cr.FillRect(
					new Vector3(from.X, from.Y + top, 0), new Vector3(to.X, to.Y + top, 0),
					new Vector3(to.X, to.Y + bottom, 0), new Vector3(from.X, from.Y + bottom, 0), colors[i]);
				top = bottom;
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			sheet?.Dispose();
			sheet = null;
		}
	}
}
