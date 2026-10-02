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
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Mods.City.Traits
{
	// Cosmetic extras (render only): cars parked on the parking slots and pedestrians on the sidewalks (sampled walking trips plus
	// hash-driven ambient walkers). Nothing here touches synced state.
	public sealed partial class TrafficSim
	{
		// One ambient walker per this many cells of each sidewalk line (both sides, both axes).
		const int AmbientWalkerSpacing = 5;

		int pedestrians;
		ISpriteSequence[] walkVariants, umbrellas, cyclists;
		ISpriteSequence jogger, dogWalker, shopper, family;

		// People from ART's LIFE export (sequences/vehicles.yaml): walk variants of `pedestrian` (adults, children, elders in many
		// clothes) and activity images; all 4-frame cycles with 8 facings (diagonals reuse N/E/S/W).
		void LoadPeople()
		{
			ISpriteSequence Seq(string image, string sequence) =>
				map.Sequences.HasSequence(image, sequence) ? map.Sequences.GetSequence(image, sequence) : null;

			ISpriteSequence[] All(string image, params string[] names) =>
				names.Select(n => Seq(image, n)).Where(q => q != null).ToArray();

			var walk = All("pedestrian", Enumerable.Range(0, 16).Select(i => "walk" + i).ToArray());
			if (walk.Length == 0)
				return;

			walkVariants = walk;
			umbrellas = All("umbrella", "idle", "umbrella1", "umbrella2", "umbrella3");
			cyclists = All("cyclist", "idle", "bike0", "bike2", "bike3");
			jogger = Seq("jogger", "idle");
			dogWalker = Seq("dogwalker", "idle");
			shopper = Seq("shopper", "idle");
			family = Seq("family", "idle");
		}

		// Parked cars sit at the kerb of plain road cells (no junction): one slot per side, facing the traffic of that side.
		void DrawParked(int cell, int cx, int cy)
		{
			if (ctl[cell] != 0)
				return;

			var mask = exitMask[cell];
			var horizontal = (mask & 0b1010) != 0 && (mask & 0b0101) == 0;
			var vertical = (mask & 0b0101) != 0 && (mask & 0b1010) == 0;
			if (!horizontal && !vertical)
				return;

			var profile = ProfileOfCell(cell);
			var kerb = profile.Parked > 0 ? profile.Parked : 304;
			var center = map.CenterOfCell(new CPos(cx, cy));
			var n = Math.Min((int)parkUsed[cell], 2);
			for (var slot = 0; slot < n; slot++)
			{
				var h = Hash(cell, slot + 11) & 0x7fffffff;
				var along = (h % 7 - 3) * 64;

				// Right-hand traffic: eastbound uses the south kerb, northbound the east kerb.
				WPos pos;
				WVec dir;
				if (horizontal)
				{
					var south = slot == 0;
					pos = center + new WVec(along, south ? kerb : -kerb, 0);
					dir = new WVec(south ? 1024 : -1024, 0, 0);
				}
				else
				{
					var east = slot == 0;
					pos = center + new WVec(east ? kerb : -kerb, along, 0);
					dir = new WVec(0, east ? -1024 : 1024, 0);
				}

				pos = MoverArt.SnapToPixel(pos);
				var model = art?.Get(MoverArt.PrivateCars[h / 7 % MoverArt.PrivateCars.Length]);
				if (model != null)
				{
					MoverArt.Draw(frame, model, pos, dir.Yaw, h / 61 % model.Variants, -1, 0f, ambient, palette);
					continue;
				}

				var seq = sequences[h / 7 % spriteCountCars];
				if (seq != null)
					frame.Add(new SpriteRenderable(seq.GetSprite(0, dir.Yaw), pos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
						ambient, TintModifiers.None, false));
			}
		}

		// Walkers of the sim's walking trips that got a route (up to PedestrianCap) are drawn on the sidewalk of their road cells.
		void DrawPedestrians(int x0, int y0, int x1, int y1, int renderU)
		{
			if ((pedestrianSequence == null && walkVariants == null) || walkers.Count == 0)
				return;

			var renderTick = renderU / (float)U;
			foreach (var (t, _) in walkers.UnorderedItems)
			{
				var route = t.WalkRoute;
				if (route == null || t.Cancelled)
					continue;

				var span = Math.Max(1, t.WalkEnd - t.WalkStart);
				var p = Math.Clamp((renderTick - t.WalkStart) / span, 0f, 1f) * route.Length;
				var step = Math.Min((int)p, route.Length - 1);
				var frac = p - step;

				var c = ToCPos(t.Origin);
				for (var i = 0; i < step; i++)
					c += CityUtils.Neighbours4[route[i]];

				if (c.X < x0 - 1 || c.X > x1 + 1 || c.Y < y0 - 1 || c.Y > y1 + 1)
					continue;

				var d = CityUtils.Neighbours4[route[step]];
				var pos = map.CenterOfCell(c) + new WVec((int)(d.X * frac * 1024), (int)(d.Y * frac * 1024), 0);

				// The sidewalk is on the right of the walking direction.
				var side = InMap(c) && roadFlag[Cell(c)] != 0 ? ProfileOfCell(Cell(c)).Sidewalk : 416;
				side = side > 0 ? side : 416;
				pos += new WVec(-d.Y * side, d.X * side, 0);
				if (walkVariants == null)
				{
					var colour = (Hash(t.Id, 3) & 3) * 2;
					var frameIndex = Math.Min(pedestrianSequence.Length - 1, colour + (((int)renderTick / 6 + t.Id) & 1));
					frame.Add(new SpriteRenderable(pedestrianSequence.GetSprite(frameIndex), pos, WVec.Zero, 0, palette, pedestrianSequence.Scale * 0.7f, 1f,
						ambient, TintModifiers.None, false));
					continue;
				}

				DrawPerson(Hash(t.Id, 5), MoverArt.SnapToPixel(pos), new WVec(d.X, d.Y, 0).Yaw, p);
			}
		}

		// Ambient walkers: hash-driven people strolling along every sidewalk line (row or column of cells), so streets look alive
		// beyond the sampled trips. Each walker moves along its whole line and is only drawn where the line is a sidewalk.
		void DrawAmbientWalkers(int x0, int y0, int x1, int y1, int renderU)
		{
			if (walkVariants == null)
				return;

			var time = renderU / (float)U;
			var keep = night > 0.5f ? 2 : 1;
			for (var axis = 0; axis < 2; axis++)
			{
				var lineFrom = axis == 0 ? y0 : x0;
				var lineTo = axis == 0 ? y1 : x1;
				var lo = axis == 0 ? x0 : y0;
				var hi = axis == 0 ? x1 : y1;
				var lineLength = axis == 0 ? width : height;
				var count = Math.Max(1, lineLength / AmbientWalkerSpacing);
				for (var line = lineFrom; line <= lineTo; line++)
				{
					for (var side = 0; side < 2; side++)
					{
						var seed = Hash(line * 4 + axis * 2 + side, 0x5157);
						for (var i = 0; i < count; i++)
						{
							var h = Hash(seed, i);
							if ((h & 0xff) % keep != 0)
								continue;

							var forward = (h & 0x100) != 0;
							var speed = 0.012f + (h >> 9 & 7) * 0.0012f;
							var start = (uint)h % (uint)lineLength;
							var along = start + (forward ? 1 : -1) * time * speed;
							along -= MathF.Floor(along / lineLength) * lineLength;
							var a = (int)along;
							if (a < lo || a > hi)
								continue;

							var cx = axis == 0 ? a : line;
							var cy = axis == 0 ? line : a;
							var cell = cy * width + cx;
							if (roadFlag[cell] == 0 || !RunsAlong(cx, cy, axis))
								continue;

							var sidewalk = ProfileOfCell(cell).Sidewalk;
							if (sidewalk == 0)
								continue;

							var frac = along - a;
							var off = side == 0 ? -sidewalk : sidewalk;
							var center = map.CenterOfCell(new CPos(cx, cy));
							var pos = axis == 0
								? center + new WVec((int)((frac - 0.5f) * 1024), off, 0)
								: center + new WVec(off, (int)((frac - 0.5f) * 1024), 0);

							var dir = axis == 0 ? new WVec(forward ? 1 : -1, 0, 0) : new WVec(0, forward ? 1 : -1, 0);
							DrawPerson(h, MoverArt.SnapToPixel(pos), dir.Yaw, along);
						}
					}
				}
			}
		}

		// Whether the road continues along the axis (0 = X, 1 = Y) through the cell, i.e. there is a sidewalk line along it.
		bool RunsAlong(int cx, int cy, int axis)
		{
			if (axis == 0)
				return (cx > 0 && roadFlag[cy * width + cx - 1] != 0) || (cx < width - 1 && roadFlag[cy * width + cx + 1] != 0);

			return (cy > 0 && roadFlag[(cy - 1) * width + cx] != 0) || (cy < height - 1 && roadFlag[(cy + 1) * width + cx] != 0);
		}

		// One person: clothes/age variant and activity from the hash; walk frame from the distance walked (feet match the motion).
		void DrawPerson(int hash, WPos pos, WAngle facing, float distanceCells)
		{
			var h = (uint)hash;
			var step = (int)(distanceCells * 12f) & 3;
			var raining = atmosphere != null && atmosphere.Precipitation > 0.2f && !atmosphere.Snowing;
			var r = h % 100;
			ISpriteSequence seq;
			if (raining && umbrellas.Length > 0 && r < 60)
				seq = umbrellas[(h >> 7) % (uint)umbrellas.Length];
			else if (r < 4 && jogger != null)
				seq = jogger;
			else if (r < 7 && dogWalker != null)
				seq = dogWalker;
			else if (r < 11 && shopper != null)
				seq = shopper;
			else if (r < 14 && family != null)
				seq = family;
			else if (r < 16 && cyclists.Length > 0)
				seq = cyclists[(h >> 7) % (uint)cyclists.Length];
			else
				seq = walkVariants[(h >> 12) % (uint)walkVariants.Length];

			frame.Add(new SpriteRenderable(seq.GetSprite(step, facing), pos, WVec.Zero, 0, palette, 1f, 1f, ambient, TintModifiers.None, false));
		}
	}
}
