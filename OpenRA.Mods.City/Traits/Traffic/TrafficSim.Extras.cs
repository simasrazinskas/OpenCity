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
		int[] sidewalkRunStart, sidewalkRunEnd;
		int sidewalkRunVersion = -1;
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
				if (route == null || t.Cancelled || !t.VisibleWalker)
					continue;

				var span = Math.Max(1, t.WalkEnd - t.WalkStart);
				var p = Math.Clamp((renderTick - t.WalkStart) / span, 0f, 1f) * route.Length;

				// Walk through connected cell paths, with half a cell at each endpoint. Turns use
				// the same continuous corner geometry as vehicles, avoiding sideways jumps.
				var step = Math.Min((int)(p + 0.5f), route.Length);
				var frac = p + 0.5f - step;
				var c = ToCPos(t.Origin);
				for (var i = 0; i < step; i++)
					c += CityUtils.Neighbours4[route[i]];

				if (c.X < x0 - 1 || c.X > x1 + 1 || c.Y < y0 - 1 || c.Y > y1 + 1)
					continue;

				var h = step == 0 ? route[0] : route[step - 1];
				var r = step < route.Length ? route[step] : h;
				var side = ProfileOfCell(Cell(c)).Sidewalk;
				PathPose(map.CenterOfCell(c), h, r, frac, side, out var pos, out var facing);

				if (walkVariants == null)
				{
					var colour = (Hash(t.Id, 3) & 3) * 2;
					var frameIndex = Math.Min(pedestrianSequence.Length - 1, colour + (((int)renderTick / 12 + t.Id) & 1));
					frame.Add(new SpriteRenderable(pedestrianSequence.GetSprite(frameIndex), pos, WVec.Zero, 0, palette, pedestrianSequence.Scale * 0.7f, 1f,
						ambient, TintModifiers.None, false));
					continue;
				}

				DrawPerson(Hash(t.Id, 5), MoverArt.SnapToPixel(pos), facing, p);
			}
		}

		// Ambient people stroll back and forth on one connected sidewalk run. They never
		// disappear into buildings/water and reappear on a separate street across the map.
		void DrawAmbientWalkers(int x0, int y0, int x1, int y1, int renderU)
		{
			if (walkVariants == null)
				return;

			if (sidewalkRunVersion != graphVersion)
			{
				sidewalkRunStart ??= new int[cellCount * 2];
				sidewalkRunEnd ??= new int[cellCount * 2];
				Array.Fill(sidewalkRunStart, -1);
				sidewalkRunVersion = graphVersion;
			}

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

							var start = (int)((uint)h % (uint)lineLength);
							var anchor = axis == 0 ? line * width + start : start * width + line;
							if (!sidewalk[anchor])
								continue;

							SidewalkRun(anchor, axis, out var first, out var last);
							var span = last - first;
							if (span <= 0)
								continue;

							var forward = (h & 0x100) != 0;
							var speed = (0.85f + (h >> 9 & 7) * 0.04f) / WalkingTicks;
							var phase = start - first + (forward ? 1 : -1) * time * speed;
							phase -= MathF.Floor(phase / (2 * span)) * (2 * span);
							var along = first + 0.5f + (phase <= span ? phase : 2 * span - phase);
							var movingForward = forward ? phase < span : phase >= span;
							var a = Math.Min(last, (int)along);
							if (a < lo || a > hi)
								continue;

							var cx = axis == 0 ? a : line;
							var cy = axis == 0 ? line : a;
							var cell = cy * width + cx;
							var off = ProfileOfCell(cell).Sidewalk * (side == 0 ? -1 : 1);
							var center = map.CenterOfCell(new CPos(cx, cy));
							var pos = axis == 0
								? center + new WVec((int)((along - a - 0.5f) * 1024), off, 0)
								: center + new WVec(off, (int)((along - a - 0.5f) * 1024), 0);
							var dir = axis == 0 ? new WVec(movingForward ? 1 : -1, 0, 0) : new WVec(0, movingForward ? 1 : -1, 0);
							DrawPerson(h, MoverArt.SnapToPixel(pos), dir.Yaw, time * speed);
						}
					}
				}
			}
		}

		void SidewalkRun(int cell, int axis, out int first, out int last)
		{
			var key = cell * 2 + axis;
			if (sidewalkRunStart[key] < 0)
			{
				var increment = axis == 0 ? 1 : width;
				var forward = axis == 0 ? 1 : 2;
				var back = (forward + 2) & 3;
				var low = cell;
				var high = cell;
				while ((walkMask[low] & (1 << back)) != 0)
					low -= increment;

				while ((walkMask[high] & (1 << forward)) != 0)
					high += increment;

				for (var c = low; c <= high; c += increment)
				{
					sidewalkRunStart[c * 2 + axis] = axis == 0 ? low % width : low / width;
					sidewalkRunEnd[c * 2 + axis] = axis == 0 ? high % width : high / width;
				}
			}

			first = sidewalkRunStart[key];
			last = sidewalkRunEnd[key];
		}

		// One person: clothes/age variant and activity from the hash; walk frame from the distance walked (feet match the motion).
		void DrawPerson(int hash, WPos pos, WAngle facing, float distanceCells)
		{
			var h = (uint)hash;
			var step = (int)(distanceCells * 48f) & 3;
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
