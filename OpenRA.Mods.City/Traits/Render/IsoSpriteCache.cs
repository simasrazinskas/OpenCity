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
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Where a sprite comes from: image, sequence, frame and facing index (reverse lookup for state swaps).</summary>
	public readonly struct IsoSpriteSource
	{
		public readonly ISpriteSequence Sequence;
		public readonly string Image;
		public readonly int Frame;
		public readonly int Facing;
		public readonly int Facings;

		public IsoSpriteSource(string image, ISpriteSequence sequence, int frame, int facing, int facings)
		{
			Image = image;
			Sequence = sequence;
			Frame = frame;
			Facing = facing;
			Facings = facings;
		}
	}

	/// <summary>One cached strip of a sliced sprite: the sub-sprite and the footprint column it sorts with.</summary>
	public readonly struct IsoStrip
	{
		public readonly Sprite Sprite;
		public readonly int Column;

		public IsoStrip(Sprite sprite, int column)
		{
			Sprite = sprite;
			Column = column;
		}
	}

	/// <summary>
	/// Render-only caches of the iso renderer, one per sequence set: strips per (sprite, footprint, column origin),
	/// sprite reverse lookups and companion sequences (lit, seasons, states). Nothing here is synced.
	/// </summary>
	public sealed class IsoSpriteCache
	{
		static readonly ConditionalWeakTable<SequenceSet, IsoSpriteCache> Caches = [];

		readonly SequenceSet sequences;
		readonly Dictionary<(Sprite Sprite, int Origin, int Columns, int Width), IsoStrip[]> strips = [];
		readonly Dictionary<Sprite, IsoSpriteSource> sources = [];
		readonly HashSet<string> indexedImages = [];
		readonly Dictionary<(string Image, string Sequence), ISpriteSequence> lookups = [];
		readonly Dictionary<(Sprite Sprite, string Suffix), Sprite> companions = [];

		IsoSpriteCache(SequenceSet sequences) { this.sequences = sequences; }

		public static IsoSpriteCache For(World world) { return Caches.GetValue(world.Map.Sequences, s => new IsoSpriteCache(s)); }

		/// <summary>
		/// Vertical strips of a sprite. <paramref name="origin"/> is the screen x of the footprint's left corner relative
		/// to the sprite's left edge (unscaled sprite pixels), <paramref name="columns"/> = W + D and
		/// <paramref name="columnWidth"/> the screen width of one column (32 px at 1x). The first and last strips extend
		/// to the sprite's edges; empty strips are dropped.
		/// </summary>
		public IsoStrip[] Strips(Sprite sprite, int origin, int columns, int columnWidth)
		{
			var key = (sprite, origin, columns, columnWidth);
			if (strips.TryGetValue(key, out var cached))
				return cached;

			var result = new List<IsoStrip>(columns);
			var w = sprite.Bounds.Width;
			var h = sprite.Bounds.Height;
			for (var k = 0; k < columns; k++)
			{
				var x0 = k == 0 ? 0 : Math.Clamp(origin + k * columnWidth, 0, w);
				var x1 = k == columns - 1 ? w : Math.Clamp(origin + (k + 1) * columnWidth, 0, w);
				if (x1 <= x0)
					continue;

				if (x0 == 0 && x1 == w)
				{
					result.Add(new IsoStrip(sprite, k));
					continue;
				}

				var bounds = new Rectangle(sprite.Bounds.X + x0, sprite.Bounds.Y, x1 - x0, h);

				// Drawn from the full sprite's top-left (IsoStripRenderable keeps the full half size), so shift by x0 only.
				var offset = sprite.Offset + new Vector3(x0, 0, 0);
				result.Add(new IsoStrip(new Sprite(sprite.Sheet, bounds, sprite.ZRamp, offset, sprite.Channel, sprite.BlendMode), k));
			}

			var array = result.ToArray();
			strips[key] = array;
			return array;
		}

		/// <summary>Image, sequence, frame and facing of a sprite of <paramref name="image"/>, if known.</summary>
		public bool TryGetSource(string image, Sprite sprite, out IsoSpriteSource source)
		{
			Index(image);
			return sources.TryGetValue(sprite, out source);
		}

		/// <summary>A sequence of an image, or null (cached, no exception).</summary>
		public ISpriteSequence Sequence(string image, string sequence)
		{
			if (image == null || sequence == null)
				return null;

			var key = (image, sequence);
			if (!lookups.TryGetValue(key, out var seq))
			{
				seq = sequences.HasSequence(image, sequence) ? sequences.GetSequence(image, sequence) : null;
				lookups[key] = seq;
			}

			return seq;
		}

		/// <summary>
		/// Companion sprite of a sprite of <paramref name="image"/>: the same frame and facing in sequence
		/// "&lt;sequence&gt;&lt;suffix&gt;" (e.g. "idle-lit") or, failing that, in the bare state sequence
		/// <paramref name="state"/> (e.g. "winter"). State sequences with fewer frames wrap (frame % length), so a
		/// 4-frame state of a growable keeps the facing. Returns null when there is none.
		/// </summary>
		public Sprite Companion(string image, Sprite sprite, string suffix, string state = null)
		{
			var key = (sprite, suffix);
			if (companions.TryGetValue(key, out var result))
				return result;

			result = null;
			if (TryGetSource(image, sprite, out var src))
			{
				var target = Sequence(image, src.Sequence.Name + suffix) ?? Sequence(image, state);
				if (target != null && target != src.Sequence && target.Length > 0)
					result = SpriteOf(target, src.Frame % target.Length, src.Facing, src.Facings);
			}

			companions[key] = result;
			return result;
		}

		/// <summary>Frame of a sequence at facing index <paramref name="facing"/> of <paramref name="facings"/>.</summary>
		public static Sprite SpriteOf(ISpriteSequence sequence, int frame, int facing, int facings)
		{
			var angle = facings <= 1 ? WAngle.Zero : new WAngle(facing * 1024 / facings);
			return sequence.GetSprite(frame, angle);
		}

		void Index(string image)
		{
			if (image == null || !indexedImages.Add(image))
				return;

			foreach (var name in sequences.Sequences(image))
			{
				var seq = sequences.GetSequence(image, name);
				var facings = Math.Max(1, seq.Facings);
				for (var f = 0; f < facings; f++)
				{
					for (var i = 0; i < seq.Length; i++)
					{
						Sprite s;
						try
						{
							s = SpriteOf(seq, i, f, facings);
						}
						catch (InvalidOperationException)
						{
							continue;
						}

						// First registration wins: "idle" before its aliases (summer) keeps the canonical source.
						if (s != null && !sources.ContainsKey(s))
							sources[s] = new IsoSpriteSource(image, seq, i, f, facings);
					}
				}
			}
		}
	}
}
