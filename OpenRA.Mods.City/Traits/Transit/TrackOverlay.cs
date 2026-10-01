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
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Ground-level overlay of one frame per cell (track strips). Render only; the owner decides which frame a cell shows.</summary>
	public sealed class TrackOverlay : IDisposable
	{
		readonly TerrainSpriteLayer layer;
		readonly ISpriteSequence sequence;
		readonly PaletteReference palette;

		public TrackOverlay(World world, WorldRenderer wr, string image, string sequenceName, string paletteName)
		{
			var sequences = world.Map.Sequences;
			if (!sequences.HasSequence(image, sequenceName))
				return;

			sequence = sequences.GetSequence(image, sequenceName);
			palette = wr.Palette(paletteName);
			var first = sequence.GetSprite(0);
			layer = new TerrainSpriteLayer(world, wr, new Sprite(first.Sheet, Rectangle.Empty, TextureChannel.Alpha), first.BlendMode, true);
		}

		public bool Available => layer != null;

		public void Set(CPos cell, int frame)
		{
			layer?.Update(cell, sequence, palette, Math.Min(frame, sequence.Length - 1));
		}

		public void Clear(CPos cell)
		{
			layer?.Clear(cell);
		}

		public void Draw(Viewport viewport)
		{
			layer?.Draw(viewport);
		}

		public void Dispose()
		{
			layer?.Dispose();
		}
	}
}
