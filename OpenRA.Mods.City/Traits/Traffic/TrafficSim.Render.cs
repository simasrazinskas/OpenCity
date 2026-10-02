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
			public float T;
			public int Offset;
			public bool Hidden;
		}

		// What a vehicle looks like: up to three segments (articulated bus, tram, lorry + trailer), a colour and its total length.
		struct Look
		{
			public MoverModel A, B, C;
			public int Variant;
			public float Length;
		}

		const float QueueGap = 0.06f;

		ISpriteSequence[] sequences = [];
		ISpriteSequence pedestrianSequence;
		PaletteReference palette;
		MoverArt art;
		CityAtmosphere atmosphere;
		Vector3 ambient = Vector3.One;
		float night;
		long tickStartRunTime;
		readonly List<IRenderable> frame = [];
		readonly float[] laneLimit = new float[4];
		readonly List<Pose> poses = [];
		readonly List<Pose> drawn = [];

		void LoadRenderAssets(WorldRenderer wr)
		{
			// Legacy top-down art is the fallback when the iso art (v-<model>) is missing.
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

			art = new MoverArt(world);
			atmosphere = world.WorldActor.TraitOrDefault<CityAtmosphere>();
			LoadPeople();
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
			ambient = MoverArt.Ambient(atmosphere);
			night = MoverArt.Night(atmosphere);
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
					for (var h = 0; h < 4; h++)
						if (qHead[l + h] >= 0)
							DrawLink(l + h, x, y, h, renderU);
				}
			}

			if (Showcase)
				DrawShowcase(x0, y0, x1, y1, renderU);

			DrawWrecks(x0, y0, x1, y1);
			DrawPedestrians(x0, y0, x1, y1, renderU);
			DrawAmbientWalkers(x0, y0, x1, y1, renderU);
			return frame;
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			return [];
		}

		void DrawLink(int l, int cx, int cy, int h, int renderU)
		{
			LayoutLink(l, cx, cy, h, renderU);
			var cell = l >> 2;
			foreach (var p in poses)
			{
				if (p.Hidden)
					continue;

				drawn.Add(p);
				var look = LookOf(p.Vehicle);
				if (look.A == null)
				{
					var seq = sequences[Math.Min(vTrip[p.Vehicle].Sprite, sequences.Length - 1)];
					if (seq != null)
						frame.Add(new SpriteRenderable(seq.GetSprite(0, p.Facing), p.Pos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
							ambient, TintModifiers.None, false));

					continue;
				}

				var alert = look.A.Alert != null ? (renderU / U / 3 + p.Vehicle) & 1 : -1;
				MoverArt.Draw(frame, look.A, p.Pos, p.Facing, look.Variant, alert, night, ambient, palette);
				if (look.B == null)
					continue;

				var route = vRoute[p.Vehicle];
				var r = vStep[p.Vehicle] < route.Length ? route[vStep[p.Vehicle]] : h;
				var back = look.A.Length * 0.5f + look.B.Length * 0.5f + 0.02f;
				TrailingPose(p.Vehicle, cell, h, r, p.T, back, p.Offset, out var bp, out var bf);
				MoverArt.Draw(frame, look.B, bp, bf, look.Variant % look.B.Variants, -1, night, ambient, palette);
				if (look.C == null)
					continue;

				back += look.B.Length * 0.5f + look.C.Length * 0.5f + 0.02f;
				TrailingPose(p.Vehicle, cell, h, r, p.T, back, p.Offset, out var cp, out var cf);
				MoverArt.Draw(frame, look.C, cp, cf, 0, -1, night, ambient, palette);
			}
		}

		Look LookOf(int v)
		{
			var t = vTrip[v];
			var image = t.Sprite < spriteNames.Count ? spriteNames[t.Sprite] : null;
			var hash = Hash(t.Id, 77);
			var look = new Look { A = art.ForLegacy(image, hash, out var variant, out var trailer), Variant = variant, B = trailer };
			if (look.A == null)
				return look;

			// Maintenance crews drive snowploughs while snow lies on the ground.
			if (look.A.Name == "maintenance" && atmosphere != null && atmosphere.SnowCover > 0.3f)
				look.A = art.Get("snowplough") ?? look.A;

			if (look.A.Name == "bus" && ((uint)hash >> 20) % 3 == 0 && art.Get("artic-bus-front") != null)
			{
				look.A = art.Get("artic-bus-front");
				look.B = art.Get("artic-bus-rear");
			}
			else if (look.A.Name == "tram-cab")
			{
				look.B = art.Get("tram-middle");
				look.C = look.B != null ? art.Get("tram-rear") : null;
			}

			look.Length = look.A.Length + (look.B?.Length ?? 0f) + (look.C?.Length ?? 0f);
			return look;
		}

		// Computes where every vehicle of a link is drawn (queue spacing, lanes, turns). Fills `poses`. Queued vehicles that do not
		// physically fit into the cell (cars are ~0.42 cells long) are marked hidden: the queue slots stay abstract in the simulation.
		void LayoutLink(int l, int cx, int cy, int h, int renderU)
		{
			poses.Clear();
			var cell = l >> 2;
			var simLanes = Math.Max(1, (int)cellLanes[cell]);
			var profile = ProfileOfCell(cell);
			var center = map.CenterOfCell(new CPos(cx, cy));
			for (var i = 0; i < 4; i++)
				laneLimit[i] = 1f;

			for (var v = qHead[l]; v >= 0; v = vNext[v])
			{
				var span = vReadyU[v] - vEnterU[v];
				var s = span <= 0 ? 1f : Math.Clamp((renderU - vEnterU[v]) / (float)span, 0f, 1f);
				s = (vStartCenter[v] ? 0.5f : 0f) + s * (vStartCenter[v] || vEndCenter[v] ? 0.5f : 1f);
				var lane = Math.Min(3, RenderLane(profile, vLane[v] % simLanes, simLanes, v));
				var look = art != null ? LookOf(v) : default;
				var front = look.A != null ? look.A.Length * 0.5f : 0.21f;
				var length = look.A != null ? look.Length : 0.42f;

				var pos = Math.Min(s, laneLimit[lane]);
				var hidden = pos < s - 0.001f && pos - front < 0f;
				laneLimit[lane] = pos - length - QueueGap;

				var route = vRoute[v];
				var r = vStep[v] < route.Length ? route[vStep[v]] : h;
				var offset = profile.Lanes[Math.Min(lane, profile.Lanes.Length - 1)];
				var t = Math.Clamp(pos, 0f, 1f);
				PathPose(center, h, r, t, EntryOffset(cx, cy, h, r, t, lane, offset), out var wpos, out var facing);
				poses.Add(new Pose
				{
					Vehicle = v,
					Pos = wpos,
					Facing = facing,
					Progress = RouteProgress(v),
					T = t,
					Offset = offset,
					Hidden = hidden,
				});
			}
		}

		// Where the road class (or one-way pairing) changes, vehicles glide from the previous cell's lane to this cell's lane over the
		// first third of a straight cell instead of jumping sideways at the cell edge.
		int EntryOffset(int cx, int cy, int h, int r, float t, int lane, int offset)
		{
			const float Blend = 0.35f;
			if (h != r || t >= Blend)
				return offset;

			var prev = new CPos(cx, cy) - CityUtils.Neighbours4[h];
			if (!InMap(prev) || roadFlag[Cell(prev)] == 0)
				return offset;

			var lanes = ProfileOfCell(Cell(prev)).Lanes;
			var from = lanes[Math.Min(lane, lanes.Length - 1)];
			var k = t / Blend;
			return from + (int)((offset - from) * k * k * (3 - 2 * k));
		}

		int RouteProgress(int v)
		{
			var t = vTrip[v];
			return t.Length > 0 ? Math.Clamp((int)((long)(t.Length - Math.Max(0, vRoute[v].Length - vStep[v])) * 100 / t.Length), 0, 100) : 0;
		}

		// Crashed vehicles lie skewed across their lane until they are cleared.
		void DrawWrecks(int x0, int y0, int x1, int y1)
		{
			var wreck = art?.Get("wreck");
			foreach (var inc in incidents)
			{
				var cell = inc.Link >> 2;
				var cx = cell % width;
				var cy = cell / width;
				if (cx < x0 || cx > x1 || cy < y0 || cy > y1)
					continue;

				PoseOf(map.CenterOfCell(new CPos(cx, cy)), inc.Link & 3, inc.Link & 3, 0.5f, 0, cellLanes[cell], out var wpos, out var facing);
				if (wreck != null)
				{
					MoverArt.Draw(frame, wreck, wpos, facing + new WAngle(128), 0, -1, 0f, ambient, palette);
					continue;
				}

				var seq = sequences[Math.Min(inc.Sprite, sequences.Length - 1)];
				if (seq != null)
					frame.Add(new SpriteRenderable(seq.GetSprite(0, facing + new WAngle(60)), wpos, WVec.Zero, 0, palette, seq.Scale * Info.SpriteScale, 1f,
						new Vector3(0.7f, 0.7f, 0.75f) * ambient, TintModifiers.None, false));
			}
		}
	}
}
