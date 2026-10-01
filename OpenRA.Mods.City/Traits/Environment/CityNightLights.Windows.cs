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
using System.Runtime.InteropServices;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	// Finds the windows of a building from its sprite, so the night glows sit on the real windows (also on big multi-cell lots,
	// where the sprite contains a garden and the building is not in the middle). The generated art draws windows in two fixed
	// colours (lit warm and dark blue-grey): the lit ones are used as glow anchors. Render only.
	public partial class CityNightLights
	{
		const int MaxWindows = 16;
		const int NewSpritesPerFrame = 2;
		const long RefreshMs = 2000;

		sealed class LightEntry
		{
			public long Time;
			public Rectangle Bounds;

			/// <summary>Screen position of the sprite's top-left corner (zero-size entries use Bounds only).</summary>
			public Vector2 Origin;
			public Vector2[] Windows;
			public bool[] Baked;
		}

		sealed class SpriteWindows
		{
			public Vector2[] Points;
			public bool[] Baked;
		}

		readonly Dictionary<uint, LightEntry> lightCache = [];
		readonly Dictionary<Sprite, SpriteWindows> windowCache = [];
		readonly HashSet<Sheet> touchedSheets = [];
		int newSpritesThisFrame;

		void BeginFrame()
		{
			newSpritesThisFrame = 0;
		}

		void EndFrame()
		{
			// Hand the temporarily read-back sheet buffers back to the GPU copy.
			if (touchedSheets.Count == 0)
				return;

			foreach (var sheet in touchedSheets)
				sheet.ReleaseBuffer();

			touchedSheets.Clear();
		}

		LightEntry LightsOf(Actor actor, WorldRenderer wr)
		{
			var now = Game.RunTime;
			if (lightCache.TryGetValue(actor.ActorID, out var entry) && now - entry.Time < RefreshMs)
				return entry;

			entry ??= new LightEntry();
			lightCache[actor.ActorID] = entry;
			entry.Time = now;

			var rect = Rectangle.Empty;
			var first = true;
			foreach (var r in actor.ScreenBounds(wr))
			{
				rect = first ? r : Rectangle.Union(rect, r);
				first = false;
			}

			entry.Bounds = rect;
			entry.Windows = null;
			entry.Baked = null;

			// The sprite the building body currently shows (public APIs of the sprite traits, no reflection).
			var best = BodySpriteOf(actor);
			if (best == null)
				return entry;

			if (!windowCache.TryGetValue(best, out var set))
			{
				if (newSpritesThisFrame >= NewSpritesPerFrame)
					return entry;

				newSpritesThisFrame++;
				set = Scan(best);
				windowCache[best] = set;
			}

			if (set.Points == null || set.Points.Length < 3)
				return entry;

			var p = wr.ScreenPosition(actor.CenterPosition);
			entry.Origin = new Vector2(p.X - (int)(0.5f * best.Size.X) + best.Offset.X, p.Y - (int)(0.5f * best.Size.Y) + best.Offset.Y);
			entry.Windows = set.Points;
			entry.Baked = set.Baked;
			return entry;
		}

		// Growables pick a frame by level and a stable variant of their cell (WithGrowableSprite's public rule); other buildings
		// animate through WithSpriteBody. Returns null while under construction or for anything else.
		static Sprite BodySpriteOf(Actor actor)
		{
			var growableInfo = actor.Info.TraitInfoOrDefault<WithGrowableSpriteInfo>();
			if (growableInfo != null)
			{
				var growable = actor.TraitOrDefault<GrowableBuilding>();
				var rs = actor.TraitOrDefault<RenderSprites>();
				if (growable == null || rs == null || growable.UnderConstruction)
					return null;

				var sequence = actor.World.Map.Sequences.GetSequence(rs.GetImage(actor), growableInfo.Sequence);
				var count = sequence.Length;
				var levels = Math.Max(1, growableInfo.Levels);
				var frame = levels > 1 && count >= levels && count % levels == 0
					? (Math.Clamp(growable.Level, 1, levels) - 1) * (count / levels) + WithGrowableSpriteInfo.VariantFor(actor.Location, count / levels)
					: WithGrowableSpriteInfo.VariantFor(actor.Location, count);
				return sequence.GetSprite(frame);
			}

			return actor.TraitOrDefault<WithSpriteBody>()?.DefaultAnimation.Image;
		}

		SpriteWindows Scan(Sprite sprite)
		{
			var result = new SpriteWindows();
			var sheet = sprite.Sheet;
			if (sheet == null || sprite.Channel != TextureChannel.RGBA)
				return result;

			var pixels = MemoryMarshal.Cast<byte, uint>(sheet.GetData());
			touchedSheets.Add(sheet);

			var b = sprite.Bounds;
			var stride = sheet.Size.Width;
			var found = new List<(int X, int Y, bool Baked)>();
			for (var y = 0; y < b.Height; y++)
			{
				for (var x = 0; x < b.Width; x++)
				{
					var c = pixels[(b.Top + y) * stride + b.Left + x];
					var r = (int)((c >> 16) & 255);
					var g = (int)((c >> 8) & 255);
					var bl = (int)(c & 255);
					var a = (int)(c >> 24);
					if (a < 200)
						continue;

					// The lit window colour (255,217,138) of the generated art; dark windows share colours with roofs, so they are not used.
					if (Math.Abs(r - 255) < 10 && Math.Abs(g - 217) < 12 && Math.Abs(bl - 138) < 16)
						found.Add((x, y, true));
				}
			}

			if (found.Count < 3)
				return result;

			// Evenly spread sample of at most MaxWindows windows.
			var n = Math.Min(MaxWindows, found.Count);
			result.Points = new Vector2[n];
			result.Baked = new bool[n];
			for (var i = 0; i < n; i++)
			{
				var (x, y, baked) = found[i * found.Count / n];
				result.Points[i] = new Vector2(x, y);
				result.Baked[i] = baked;
			}

			return result;
		}
	}
}
