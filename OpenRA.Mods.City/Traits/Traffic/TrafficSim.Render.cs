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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// Render-only: draws every vehicle in the viewport from the sim state. Nothing here touches synced state.
	public sealed partial class TrafficSim
	{
		struct Pose
		{
			public int Vehicle;
			public WPos Pos;
			public WAngle Facing;
			public int Progress;
		}

		ISpriteSequence[] sequences = [];
		ISpriteSequence pedestrianSequence;
		PaletteReference palette;
		long tickStartRunTime;
		readonly List<IRenderable> frame = [];
		readonly float[] laneLimit = new float[4];
		readonly List<Pose> poses = [];
		readonly List<Pose> drawn = [];

		void LoadRenderAssets(WorldRenderer wr)
		{
			// Missing art falls back to the first car sprite.
			var list = new List<ISpriteSequence>();
			foreach (var n in spriteNames)
			{
				if (map.Sequences.HasSequence(n, "idle"))
					list.Add(map.Sequences.GetSequence(n, "idle"));
				else
					list.Add(list.Count > 0 ? list[0] : null);
			}

			sequences = list.ToArray();
			if (map.Sequences.HasSequence(Info.PedestrianImage, "idle"))
				pedestrianSequence = map.Sequences.GetSequence(Info.PedestrianImage, "idle");

			palette = wr.Palette(Info.Palette);
		}

		// Render time in sim units: the current tick plus how far into the next tick we are (render only).
		int RenderU()
		{
			var frac = 0;
			if (!world.Paused)
			{
				var elapsed = Game.RunTime - tickStartRunTime;
				frac = (int)Math.Clamp(elapsed * U / Math.Max(1, world.Timestep), 0, U);
			}

			return tick * U + frac;
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			drawn.Clear();
			if (net == null || sequences.Length == 0 || palette == null)
				return SpriteRenderable.None;

			frame.Clear();
			var renderU = RenderU();

			var region = wr.Viewport.AllVisibleCells;
			var x0 = Math.Max(0, region.TopLeft.U - 1);
			var y0 = Math.Max(0, region.TopLeft.V - 1);
			var x1 = Math.Min(width - 1, region.BottomRight.U + 1);
			var y1 = Math.Min(height - 1, region.BottomRight.V + 1);
			for (var y = y0; y <= y1; y++)
			{
				for (var x = x0; x <= x1; x++)
				{
					var cell = y * width + x;
					if (roadFlag[cell] == 0)
						continue;

					if (parkUsed[cell] > 0)
						DrawParked(cell, x, y);

					var l = cell * 4;
					if (qHead[l] >= 0)
						DrawLink(l, x, y, 0, renderU);

					if (qHead[l + 1] >= 0)
						DrawLink(l + 1, x, y, 1, renderU);

					if (qHead[l + 2] >= 0)
						DrawLink(l + 2, x, y, 2, renderU);

					if (qHead[l + 3] >= 0)
						DrawLink(l + 3, x, y, 3, renderU);
				}
			}

			DrawWrecks(x0, y0, x1, y1);
			DrawPedestrians(x0, y0, x1, y1, renderU);
			return frame;
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			return [];
		}

		void DrawLink(int l, int cx, int cy, int h, int renderU)
		{
			LayoutLink(l, cx, cy, h, renderU);
			foreach (var p in poses)
			{
				var seq = sequences[Math.Min(vTrip[p.Vehicle].Sprite, sequences.Length - 1)];
				if (seq == null)
					continue;

				frame.Add(new SpriteRenderable(seq.GetSprite(0, p.Facing), p.Pos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
					Vector3.One, TintModifiers.None, false));
				drawn.Add(p);
			}
		}

		// Computes where every vehicle of a link is drawn (queue spacing, lanes, turns). Fills `poses`.
		void LayoutLink(int l, int cx, int cy, int h, int renderU)
		{
			poses.Clear();
			var cell = l >> 2;
			var lanes = cellLanes[cell];
			var center = map.CenterOfCell(new CPos(cx, cy));
			var spl = (float)Info.SlotsPerLane;
			for (var i = 0; i < 4; i++)
				laneLimit[i] = float.MaxValue;

			for (var v = qHead[l]; v >= 0; v = vNext[v])
			{
				var span = vReadyU[v] - vEnterU[v];
				var s = span <= 0 ? 1f : Math.Clamp((renderU - vEnterU[v]) / (float)span, 0f, 1f);
				var lane = vLane[v] % lanes;
				var pos = Math.Min(s, laneLimit[lane]);
				laneLimit[lane] = pos - Math.Max((int)vSlots[v], 1) / spl;

				var route = vRoute[v];
				var r = vStep[v] < route.Length ? route[vStep[v]] : h;
				PoseOf(center, h, r, Math.Clamp(pos, 0f, 1f), lane, lanes, out var wpos, out var facing);
				poses.Add(new Pose { Vehicle = v, Pos = wpos, Facing = facing, Progress = RouteProgress(v) });
			}
		}

		int RouteProgress(int v)
		{
			var t = vTrip[v];
			return t.Length > 0 ? Math.Clamp((int)((long)(t.Length - Math.Max(0, vRoute[v].Length - vStep[v])) * 100 / t.Length), 0, 100) : 0;
		}

		void PoseOf(WPos center, int h, int r, float t, int lane, int lanes, out WPos pos, out WAngle facing)
		{
			var din = CityUtils.Neighbours4[h];
			var dout = CityUtils.Neighbours4[r];

			// Quadratic Bezier from the entry edge midpoint through the cell centre to the exit edge midpoint.
			float ex = -din.X * 512, ey = -din.Y * 512;
			float xx = dout.X * 512, xy = dout.Y * 512;
			var u = 1 - t;
			var bx = u * u * ex + t * t * xx;
			var by = u * u * ey + t * t * xy;
			var tx = u * din.X + t * dout.X;
			var ty = u * din.Y + t * dout.Y;
			var len = MathF.Sqrt(tx * tx + ty * ty);
			if (len < 0.01f)
			{
				tx = din.X;
				ty = din.Y;
				len = 1;
			}

			tx /= len;
			ty /= len;

			// Right-hand traffic: the lane lies to the right of the direction of travel (screen: x right, y down).
			var offset = lanes == 1 ? Info.LaneOffset : 40 + (lane * 2 + 1) * 220 / lanes;
			bx += -ty * offset;
			by += tx * offset;

			pos = new WPos(center.X + (int)bx, center.Y + (int)by, 0);
			facing = new WVec((int)(tx * 1024), (int)(ty * 1024), 0).Yaw;
		}

		// Crashed vehicles lie across their lane until they are cleared.
		void DrawWrecks(int x0, int y0, int x1, int y1)
		{
			foreach (var inc in incidents)
			{
				var cell = inc.Link >> 2;
				var cx = cell % width;
				var cy = cell / width;
				if (cx < x0 || cx > x1 || cy < y0 || cy > y1)
					continue;

				PoseOf(map.CenterOfCell(new CPos(cx, cy)), inc.Link & 3, inc.Link & 3, 0.5f, 0, cellLanes[cell], out var wpos, out var facing);
				var seq = sequences[Math.Min(inc.Sprite, sequences.Length - 1)];
				if (seq == null)
					continue;

				frame.Add(new SpriteRenderable(seq.GetSprite(0, facing + new WAngle(60)), wpos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
					new Vector3(0.7f, 0.7f, 0.75f), TintModifiers.None, false));
			}
		}
	}
}
