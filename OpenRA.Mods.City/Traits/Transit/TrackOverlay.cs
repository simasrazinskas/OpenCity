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
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>A tall track prop (render only): draw it as a sorted sprite of image TrackProps.Image at the cell centre plus Offset.</summary>
	public readonly struct TrackProp
	{
		public readonly string Sequence;
		public readonly int Frame;
		public readonly WVec Offset;
		public readonly int ZOffset;

		public TrackProp(string sequence, int frame, WVec offset = default, int zOffset = 0)
		{
			Sequence = sequence;
			Frame = frame;
			Offset = offset;
			ZOffset = zOffset;
		}
	}

	/// <summary>
	/// Props that stand on rail cells: catenary masts, buffer stops, level-crossing gates and signals.
	/// Sequences of image "trackprops" (layout in sequences/overlays.yaml); every frame is anchored on the cell centre.
	/// </summary>
	public static class TrackProps
	{
		public const string Image = "trackprops";

		/// <summary>Adds the static props of a rail cell: a gate pair at a level crossing (barriers up), a buffer stop at a dead end, a catenary mast on every second cell of a straight track.</summary>
		public static void Rail(CPos cell, int mask, bool crossing, List<TrackProp> props)
		{
			if (crossing)
			{
				props.Add(Gate(false, 0, (mask & 5) == 0));
				return;
			}

			// A single arm: the buffer frames are indexed by the open arm (N, E, S, W = bit index).
			if (mask is 1 or 2 or 4 or 8)
			{
				props.Add(new TrackProp("buffer", mask == 1 ? 0 : mask == 2 ? 1 : mask == 4 ? 2 : 3));
				return;
			}

			if (mask == 5 && (cell.Y & 1) == 0)
				props.Add(new TrackProp("catenary", 0));
			else if (mask == 10 && (cell.X & 1) == 0)
				props.Add(new TrackProp("catenary", 1));
		}

		/// <summary>Level-crossing gate pair. lamp 0 or 1 is the flash phase (alternate while a train approaches).</summary>
		public static TrackProp Gate(bool down, int lamp, bool railEastWest, bool lit = false)
		{
			var name = (down ? "gate-down" : "gate-up") + (lit ? "-lit" : "");
			return new TrackProp(name, (lamp & 1) + (railEastWest ? 2 : 0));
		}

		/// <summary>Rail signal head standing in the cell (not placed by Rail: the signal state belongs to the caller).</summary>
		public static TrackProp Signal(bool red, bool lit = false)
		{
			return new TrackProp((red ? "signal-red" : "signal-green") + (lit ? "-lit" : ""), 0);
		}
	}

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
