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

namespace OpenRA.Mods.City.Traits
{
	// Cosmetic extras (render only): cars parked on the parking slots and pedestrians sampled from walking trips.
	public sealed partial class TrafficSim
	{
		int pedestrians;

		// Parked cars sit at the curb of plain road cells (no junction): one slot per side, facing the traffic of that side.
		void DrawParked(int cell, int cx, int cy)
		{
			if (ctl[cell] != 0)
				return;

			var mask = exitMask[cell];
			var horizontal = (mask & 0b1010) != 0 && (mask & 0b0101) == 0;
			var vertical = (mask & 0b0101) != 0 && (mask & 0b1010) == 0;
			if (!horizontal && !vertical)
				return;

			var center = map.CenterOfCell(new CPos(cx, cy));
			var n = Math.Min((int)parkUsed[cell], 2);
			for (var slot = 0; slot < n; slot++)
			{
				var h = Hash(cell, slot + 11) & 0x7fffffff;
				var along = h % 400 - 200;
				var seq = sequences[h / 7 % spriteCountCars];
				if (seq == null)
					continue;

				// Right-hand traffic: eastbound uses the south curb, northbound the east curb.
				WPos pos;
				WVec dir;
				if (horizontal)
				{
					var south = slot == 0;
					pos = center + new WVec(along, south ? 410 : -410, 0);
					dir = new WVec(south ? 1024 : -1024, 0, 0);
				}
				else
				{
					var east = slot == 0;
					pos = center + new WVec(east ? 410 : -410, along, 0);
					dir = new WVec(0, east ? -1024 : 1024, 0);
				}

				frame.Add(new SpriteRenderable(seq.GetSprite(0, dir.Yaw), pos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
					Vector3.One, TintModifiers.None, false));
			}
		}

		// Walkers of the sim's walking trips that got a route (up to PedestrianCap) are drawn on the sidewalk of their road cells.
		void DrawPedestrians(int x0, int y0, int x1, int y1, int renderU)
		{
			if (pedestrianSequence == null || walkers.Count == 0)
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

				// The sidewalk is on the right of the walking direction, a bit further out than the parked cars.
				pos += new WVec(-d.Y * 450, d.X * 450, 0);
				var colour = (Hash(t.Id, 3) & 3) * 2;
				var frameIndex = Math.Min(pedestrianSequence.Length - 1, colour + (((int)renderTick / 6 + t.Id) & 1));
				frame.Add(new SpriteRenderable(pedestrianSequence.GetSprite(frameIndex), pos, WVec.Zero, 0, palette, pedestrianSequence.Scale * 0.7f, 1f,
					Vector3.One, TintModifiers.None, false));
			}
		}
	}
}
