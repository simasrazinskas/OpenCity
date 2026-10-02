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
using System.Linq;
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only depth-sorted world FX: chimney smoke and steam from the roofs of growables, construction dust, puddles on roads in",
		"the rain, cargo ships and planes at the cargo terminals and fireworks when the city reaches a milestone. Reads simulation state, never changes it; variation is hash-driven.")]
	public class CityAmbientFxInfo : TraitInfo
	{
		public readonly string Image = "fx";
		public readonly string Palette = "city";

		[Desc("Houses smoke from their chimneys below this temperature (degrees C x 10).")]
		public readonly int ChimneyTemperatureX10 = 100;

		[Desc("Share of road cells (percent) that get a puddle while it rains.")]
		public readonly int PuddlePercent = 22;

		[Desc("Seconds of fireworks after a milestone.")]
		public readonly int FireworksSeconds = 10;

		public override object Create(ActorInitializer init) { return new CityAmbientFx(init.Self, this); }
	}

	public class CityAmbientFx : IRender, IWorldLoaded
	{
		readonly CityAmbientFxInfo info;
		readonly World world;
		readonly List<IRenderable> frame = [];
		ISpriteSequence smokeHouse, smokeStack, smokeStackLight, steam, dust, puddle;
		ISpriteSequence[] fireworks;
		PaletteReference palette;
		CityAtmosphere atmosphere;
		CityClimate climate;
		IRoadNetwork roads;
		Progression progression;
		MoverArt art;
		int lastMilestone = -1;
		long fireworksUntil;

		public CityAmbientFx(Actor self, CityAmbientFxInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var seqs = w.Map.Sequences;
			ISpriteSequence Seq(string name) => seqs.HasSequence(info.Image, name) ? seqs.GetSequence(info.Image, name) : null;
			smokeHouse = Seq("smoke-house");
			smokeStack = Seq("smoke-stack");
			smokeStackLight = Seq("smoke-stack-light") ?? smokeStack;
			steam = Seq("steam");
			dust = Seq("dust");
			puddle = Seq("puddle");
			fireworks = new[] { Seq("fireworks-red"), Seq("fireworks-gold"), Seq("fireworks-green"), Seq("fireworks-blue") }
				.Where(s => s != null).ToArray();
			palette = wr.Palette(info.Palette);
			art = new MoverArt(w);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			climate = w.WorldActor.TraitOrDefault<CityClimate>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			progression = w.Players.Select(p => p.PlayerActor.TraitOrDefault<Progression>()).FirstOrDefault(p => p != null);
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			return [];
		}

		static int Hash(int a, int b)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 40503u;
				h ^= h >> 15;
				h *= 2246822519u;
				return (int)((h ^ (h >> 13)) & 0x7fffffff);
			}
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			frame.Clear();
			if (palette == null)
				return frame;

			var ambient = MoverArt.Ambient(atmosphere);
			var night = MoverArt.Night(atmosphere);
			var tick = world.WorldTick;
			var vp = wr.Viewport;
			var cold = climate != null && climate.TemperatureX10 < info.ChimneyTemperatureX10;
			foreach (var a in world.ScreenMap.RenderableActorsInBox(vp.TopLeft - new int2(64, 64), vp.BottomRight + new int2(64, 160)))
			{
				if (!a.IsInWorld || a.Disposed)
					continue;

				var g = a.TraitOrDefault<GrowableBuilding>();
				if (g != null)
					DrawBuildingFx(wr, a, g, cold, tick, ambient);
				else if (a.Info.Name == "cargoharbor")
					DrawShip(a, ambient, night);
				else if (a.Info.Name == "cargoairport")
					DrawPlane(a, ambient, night);
			}

			DrawPuddles(wr, ambient);
			DrawFireworks(wr);
			return frame;
		}

		// Smoke rises from the top of the building's screen bounds (its roof or chimney tip); dust swirls over construction sites.
		void DrawBuildingFx(WorldRenderer wr, Actor a, GrowableBuilding g, bool cold, int tick, Vector3 ambient)
		{
			var pos = a.CenterPosition;
			var h = Hash((int)a.ActorID, 31);
			if (g.UnderConstruction && !g.IsUpgrading)
			{
				if (dust != null)
					frame.Add(new SpriteRenderable(dust.GetSprite(tick / 3 + h), pos, WVec.Zero, (g.Width + g.Depth) * 512 + 2, palette, 1f, 1f, ambient,
						TintModifiers.None, false));

				return;
			}

			if (!g.IsOperational)
				return;

			ISpriteSequence plume = null;
			var cat = g.Zone.Category();
			if (cat == ZoneCategory.Industrial)
				plume = g.Level >= 3 ? smokeStackLight : smokeStack;
			else if (cold && g.Zone == ZoneType.ResidentialLow)
				plume = h % 3 == 0 ? smokeHouse : null;
			else if (cold && cat == ZoneCategory.Residential)
				plume = h % 2 == 0 ? steam : null;

			if (plume == null)
				return;

			var bounds = Rectangle.Empty;
			foreach (var r in a.ScreenBounds(wr))
				bounds = bounds == Rectangle.Empty ? r : Rectangle.Union(bounds, r);

			if (bounds == Rectangle.Empty)
				return;

			// Emitter: the roof top, a little right of the centre (chimneys sit off-centre); expressed as world height above the anchor.
			var anchor = wr.ScreenPxPosition(pos);
			var dx = bounds.Width / 6 + h % 5 - 2;
			var up = Math.Max(4, anchor.Y - bounds.Top - 2);
			var emitter = pos + new WVec(dx * 16, -dx * 16, up * 32);
			frame.Add(new SpriteRenderable(plume.GetSprite(tick / 4 + h), emitter, WVec.Zero, 1, palette, 1f, 1f, ambient, TintModifiers.None, false));
		}

		// A cargo ship sails out of the harbour onto the nearest water and back (one-minute loop).
		void DrawShip(Actor a, Vector3 ambient, float night)
		{
			var ship = art.Get("cargo-ship");
			if (ship == null)
				return;

			var map = world.Map;
			var center = a.Location + new CVec(1, 1);
			var dir = -1;
			for (var r = 2; r <= 4 && dir < 0; r++)
				for (var d = 0; d < 4 && dir < 0; d++)
					if (map.Contains(center + CityUtils.Neighbours4[d] * r) && map.GetTerrainInfo(center + CityUtils.Neighbours4[d] * r).Type == "Water")
						dir = d;

			if (dir < 0)
				return;

			var phase = (Game.RunTime + a.ActorID * 7919) % 60000 / 60000f;
			var outbound = phase < 0.5f;
			var reach = 2.5f + 4f * (0.5f - 0.5f * MathF.Cos(phase * MathF.PI * 2f));
			var v = CityUtils.Neighbours4[dir];
			var pos = map.CenterOfCell(center) + new WVec((int)(v.X * reach * 1024), (int)(v.Y * reach * 1024), 0);
			var facing = new WVec(outbound ? v.X : -v.X, outbound ? v.Y : -v.Y, 0).Yaw;
			MoverArt.Draw(frame, ship, MoverArt.SnapToPixel(pos), facing, 0, -1, night, ambient, palette);
		}

		// A cargo plane rolls along the airport's runway (+X), lifts off and climbs away; then the next one lands (40-second loop).
		void DrawPlane(Actor a, Vector3 ambient, float night)
		{
			var plane = art.Get("cargo-plane");
			if (plane == null)
				return;

			var phase = (Game.RunTime + a.ActorID * 4973) % 40000 / 40000f;
			if (phase > 0.75f)
				return;

			var start = world.Map.CenterOfCell(a.Location + new CVec(0, 1));
			var roll = phase < 0.3f ? 0f : (phase - 0.3f) / 0.45f;
			var along = (int)(roll * roll * 9f * 1024);
			var lift = (int)(Math.Max(0f, roll - 0.35f) * 1.6f * 60 * 32);
			var ground = start + new WVec(along, 0, 0);
			var facing = new WVec(1, 0, 0).Yaw;
			if (plane.Shadow != null)
				frame.Add(new SpriteRenderable(plane.Shadow.GetSprite(0, facing), MoverArt.SnapToPixel(ground), WVec.Zero, 0, palette, 1f, 0.55f,
					Vector3.One, TintModifiers.None, false));

			var prop = (int)(Game.RunTime / 60 % plane.Variants);
			MoverArt.Draw(frame, plane, MoverArt.SnapToPixel(ground + new WVec(0, 0, lift)), facing, prop, -1, night, ambient, palette);
		}

		// Puddles on a hashed share of the visible road cells while it rains (ground decals, sorted before everything in the cell).
		void DrawPuddles(WorldRenderer wr, Vector3 ambient)
		{
			if (puddle == null || roads == null || atmosphere == null || atmosphere.Snowing || atmosphere.Precipitation < 0.2f)
				return;

			var map = world.Map;
			foreach (var c in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				var cell = c.ToCPos(map);
				var h = Hash(cell.X * 977 + cell.Y, 41);
				if (h % 100 >= info.PuddlePercent || !roads.IsRoad(cell))
					continue;

				var offset = new WVec(h / 100 % 9 * 64 - 256, h / 900 % 9 * 64 - 256, 0);
				frame.Add(new SpriteRenderable(puddle.GetSprite(h / 8100 % 3), map.CenterOfCell(cell) + offset, WVec.Zero, -1000, palette, 1f, 1f,
					ambient, TintModifiers.None, false));
			}
		}

		// Milestone fireworks: staggered rockets around the view centre for a few seconds after the city reaches a milestone.
		void DrawFireworks(WorldRenderer wr)
		{
			if (progression == null || fireworks.Length == 0)
				return;

			var milestone = progression.MilestoneIndex;
			if (lastMilestone >= 0 && milestone > lastMilestone)
				fireworksUntil = Game.RunTime + info.FireworksSeconds * 1000L;

			lastMilestone = milestone;
			if (Game.RunTime >= fireworksUntil)
				return;

			var center = wr.Viewport.CenterPosition;
			var now = Game.RunTime;
			for (var i = 0; i < 6; i++)
			{
				// Each rocket repeats every 1.1-1.6 s from a new hashed launch point.
				var period = 1100 + i * 100;
				var cycle = (int)((now + i * 370) / period);
				var age = (now + i * 370) % period;
				var seq = fireworks[Hash(i, cycle) % fireworks.Length];
				var f = (int)(age * seq.Length / 800);
				if (f >= seq.Length)
					continue;

				var h = Hash(cycle, i + 50);
				var pos = new WPos(center.X + (h % 9 - 4) * 1024, center.Y + (h / 9 % 9 - 4) * 1024, 48 * 32);
				frame.Add(new SpriteRenderable(seq.GetSprite(f), pos, WVec.Zero, 4096, palette, 1f, 1f, Vector3.One, TintModifiers.IgnoreWorldTint, false));
			}
		}
	}
}
