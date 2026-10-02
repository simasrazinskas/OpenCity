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
	[TraitLocation(SystemActors.World)]
	[Desc("Draws building fires (4 stages, smoke, fire hoses), wildfires, garbage heaps and service helicopters (flying at real height",
		"with a ground shadow). Reads ServiceSimulation, never changes it.")]
	public class ServiceEffectsInfo : TraitInfo
	{
		public readonly string Image = "svc-fire";
		public readonly string FireSequence = "idle";
		public readonly string PileImage = "svc-pile";
		public readonly string HeliImage = "svc-heli";

		[Desc("Iso FX image (fire-1..4, smoke-dark, hose-n/e/s/w, heli-drop). Falls back to Image when missing.")]
		public readonly string FxImage = "fx";

		public readonly string Palette = "city";

		[Desc("Ticks per animation frame of the flames.")]
		public readonly int FireFrameTicks = 6;

		[Desc("Cruise height of helicopters in screen pixels at zoom 1.")]
		public readonly int HeliCruisePixels = 26;

		public override object Create(ActorInitializer init) { return new ServiceEffects(init.Self, this); }
	}

	public class ServiceEffects : IRender, IWorldLoaded
	{
		// Wall height of the fire art's host building: smoke rises from the roof line.
		const int RoofZ = 28 * 32;

		readonly ServiceEffectsInfo info;
		readonly World world;
		readonly List<ServiceSimulation> sims = [];
		readonly List<Property> piles = [];
		readonly List<IRenderable> frame = [];
		ISpriteSequence fire;
		ISpriteSequence[] pile;
		ISpriteSequence heli;
		ISpriteSequence[] fireStages;
		ISpriteSequence smoke, heliDrop;
		ISpriteSequence[] hoses;
		MoverArt art;
		CityAtmosphere atmosphere;
		readonly List<CPos> wildCells = [];
		readonly List<HelicopterFlight> flights = [];
		PaletteReference palette;

		public ServiceEffects(Actor self, ServiceEffectsInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var seqs = w.Map.Sequences;
			ISpriteSequence Fx(string name) => seqs.HasSequence(info.FxImage, name) ? seqs.GetSequence(info.FxImage, name) : null;

			fire = seqs.GetSequence(info.Image, info.FireSequence);
			pile =
			[
				seqs.GetSequence(info.PileImage, "small"),
				seqs.GetSequence(info.PileImage, "medium"),
				seqs.GetSequence(info.PileImage, "large"),
			];

			heli = seqs.GetSequence(info.HeliImage, "idle");
			var stages = new[] { Fx("fire-1"), Fx("fire-2"), Fx("fire-3"), Fx("fire-4") };
			fireStages = Array.TrueForAll(stages, s => s != null) ? stages : null;
			smoke = Fx("smoke-dark");
			heliDrop = Fx("heli-drop");
			var hose = new[] { Fx("hose-n"), Fx("hose-e"), Fx("hose-s"), Fx("hose-w") };
			hoses = Array.TrueForAll(hose, s => s != null) ? hose : null;
			art = new MoverArt(w);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			palette = wr.Palette(info.Palette);
			foreach (var p in w.Players)
			{
				var sim = p.PlayerActor.TraitOrDefault<ServiceSimulation>();
				if (sim != null && p.Playable)
					sims.Add(sim);
			}
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			return [];
		}

		// Part of the world's depth sort (not drawn above it), so buildings and vehicles in front hide flames, heaps and helicopters.
		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			frame.Clear();
			if (sims.Count == 0 || fire == null)
				return frame;

			var view = wr.Viewport;
			var tl = view.TopLeft - new int2(160, 200);
			var br = view.BottomRight + new int2(160, 160);
			bool Visible(WPos pos)
			{
				var s = wr.ScreenPxPosition(pos);
				return s.X >= tl.X && s.Y >= tl.Y && s.X <= br.X && s.Y <= br.Y;
			}

			var ambient = MoverArt.Ambient(atmosphere);
			var night = MoverArt.Night(atmosphere);
			var map = world.Map;
			var tick = world.WorldTick;
			var fireFrame = tick / Math.Max(1, info.FireFrameTicks);
			foreach (var sim in sims)
			{
				foreach (var id in sim.BurningProperties)
				{
					var p = sim.GetProperty(id);
					if (p != null && Visible(Center(map, p)))
						DrawBuildingFire(sim, p, fireFrame);
				}

				wildCells.Clear();
				sim.GetWildfires(wildCells);
				foreach (var c in wildCells)
				{
					var wp = map.CenterOfCell(c);
					if (!Visible(wp))
						continue;

					// Wildfires burn in the trees of the cell: sorted just in front of the cell's props.
					var seq = fireStages != null ? fireStages[1 + (Hash(c.X, c.Y) & 1)] : fire;
					frame.Add(new SpriteRenderable(seq.GetSprite(fireFrame + Hash(c.Y, c.X)), wp, WVec.Zero, 512, palette, 1f, 1f, Vector3.One,
						TintModifiers.IgnoreWorldTint, false));
				}

				flights.Clear();
				sim.GetFlights(flights);
				foreach (var f in flights)
					DrawHelicopter(map, f, ambient, night, Visible);

				if (TrafficSim.Showcase)
					DrawShowcase(wr, ambient, night, fireFrame, Visible);

				piles.Clear();
				sim.GetPiles(piles);
				foreach (var p in piles)
				{
					var pos = map.CenterOfCell(p.Origin + new CVec(0, p.Depth - 1)) + new WVec(-384, 384, 0);
					var level = sim.GetPileLevel(p.Id);
					if (level <= 0 || !Visible(pos))
						continue;

					// Heaps lie on the lot's front edge: a small ZOffset keeps them over the lot ground but behind the street.
					frame.Add(new SpriteRenderable(pile[level - 1].GetSprite(0), pos, WVec.Zero, 16, palette, 1f, 1f, ambient, TintModifiers.None, false));
				}
			}

			return frame;
		}

		// Flames on 1-4 footprint cells (more as the fire grows), dark smoke from the roof line, and a hose from the access road while
		// engines are on site. Each flame sorts just in front of the building (front footprint corner + 1).
		void DrawBuildingFire(ServiceSimulation sim, Property p, int fireFrame)
		{
			var (damage, engines) = sim.GetFireState(p.Id);
			DrawFire(p, damage, engines, fireFrame);
		}

		void DrawFire(Property p, int damage, int engines, int fireFrame)
		{
			var map = world.Map;
			if (fireStages == null)
			{
				var scale = p.Width >= 3 ? 2.5f : p.Width == 2 ? 1.8f : 1.3f;
				var center = Center(map, p);
				frame.Add(new SpriteRenderable(fire.GetSprite(fireFrame), center, WVec.Zero, (p.Width + p.Depth) * 512 + 1, palette, scale, 1f,
					Vector3.One, TintModifiers.IgnoreWorldTint, false));
				return;
			}

			var stage = Math.Clamp(damage / 25, 0, 3);
			var cells = p.Width * p.Depth;
			var count = Math.Min(cells, 1 + stage);
			var start = Hash(p.Id, 9) & 0x7fff;
			for (var k = 0; k < count; k++)
			{
				var index = (start + k * 7) % cells;
				var i = index % p.Width;
				var j = index / p.Width;
				var pos = map.CenterOfCell(p.Origin + new CVec(i, j));
				var front = (p.Width - i) * 1024 - 512 + (p.Depth - j) * 1024 - 512 + 1;
				var cellStage = Math.Max(0, stage - (k == 0 ? 0 : 1));
				frame.Add(new SpriteRenderable(fireStages[cellStage].GetSprite(fireFrame + k * 3), pos, WVec.Zero, front, palette, 1f, 1f, Vector3.One,
					TintModifiers.IgnoreWorldTint, false));

				if (smoke != null && k == 0)
					frame.Add(new SpriteRenderable(smoke.GetSprite(fireFrame / 2 + p.Id), pos + new WVec(0, 0, RoofZ), WVec.Zero, front, palette, 1f, 1f,
						MoverArt.Ambient(atmosphere), TintModifiers.None, false));
			}

			if (engines <= 0 || hoses == null || p.AccessRoad == CPos.Zero)
				return;

			// The hose sprays from the road cell's edge towards the building.
			var d = p.AccessCell - p.AccessRoad;
			var dir = Array.IndexOf(CityUtils.Neighbours4, d);
			if (dir < 0)
				return;

			var nozzle = map.CenterOfCell(p.AccessRoad) + new WVec(d.X * 352, d.Y * 352, 0);
			frame.Add(new SpriteRenderable(hoses[dir].GetSprite(fireFrame), nozzle, WVec.Zero, 2, palette, 1f, 1f, Vector3.One, TintModifiers.None, false));
		}

		// Helicopters climb to cruise height after take-off and descend before landing; the shadow stays on the ground under them.
		void DrawHelicopter(Map map, HelicopterFlight f, Vector3 ambient, float night, Func<WPos, bool> visible)
		{
			var from = map.CenterOfCell(f.From);
			var to = map.CenterOfCell(f.To);
			var ground = from + new WVec((to.X - from.X) * f.Permille / 1000, (to.Y - from.Y) * f.Permille / 1000, 0);
			if (!visible(ground))
				return;

			var climb = Math.Clamp(Math.Min(f.Permille, 1000 - f.Permille) / 150f, 0f, 1f);
			var lift = (int)(info.HeliCruisePixels * 32 * climb * climb * (3 - 2 * climb));
			var model = art.Get(f.Kind == ServiceKind.Fire ? "heli-fire" : "heli-medevac");
			var facing = from == to ? WAngle.Zero : (to - from).Yaw;
			var rotor = (int)(Game.RunTime / 50 % 4);
			if (model == null)
			{
				var sprite = heli.GetSprite(world.WorldTick / 2 % heli.Length);
				frame.Add(new SpriteRenderable(sprite, ground + new WVec(0, 0, lift), WVec.Zero, 0, palette, 1f, 1f, ambient, TintModifiers.None, false));
				return;
			}

			// The body art is anchored at the body (its design lift removed), so the real height is the world Z.
			var air = MoverArt.SnapToPixel(ground + new WVec(0, 0, lift));
			if (model.Shadow != null)
				frame.Add(new SpriteRenderable(model.Shadow.GetSprite(0, facing), MoverArt.SnapToPixel(ground), WVec.Zero, 0, palette, 1f, 0.55f,
					Vector3.One, TintModifiers.None, false));

			MoverArt.Draw(frame, model, air, facing, rotor, -1, night, ambient, palette);

			if (heliDrop != null && f.Kind == ServiceKind.Fire && f.Permille > 900)
				frame.Add(new SpriteRenderable(heliDrop.GetSprite(world.WorldTick / 3), MoverArt.SnapToPixel(ground), WVec.Zero, lift, palette, 1f, 1f,
					Vector3.One, TintModifiers.None, false));
		}

		// Render-only showcase for headless screenshots (CityAutoTest movers=demo): two helicopters crossing the view and fires on a
		// hashed share of the visible growables, so every effect can be checked without waiting for real incidents.
		void DrawShowcase(WorldRenderer wr, Vector3 ambient, float night, int fireFrame, Func<WPos, bool> visible)
		{
			var map = world.Map;
			var center = map.CellContaining(wr.Viewport.CenterPosition);
			var cycle = (int)(Game.RunTime / 40 % 1000);
			DrawHelicopter(map, new HelicopterFlight { From = center + new CVec(-6, -2), To = center + new CVec(6, 3), Permille = cycle, Kind = ServiceKind.Fire },
				ambient, night, visible);
			DrawHelicopter(map, new HelicopterFlight { From = center + new CVec(3, -6), To = center + new CVec(-2, 6), Permille = 400, Kind = ServiceKind.Health },
				ambient, night, visible);

			foreach (var a in world.ScreenMap.RenderableActorsInBox(wr.Viewport.TopLeft, wr.Viewport.BottomRight))
			{
				var g = a.TraitOrDefault<GrowableBuilding>();
				if (g == null || Hash((int)a.ActorID, 3) % 9 != 0)
					continue;

				var p = new Property { Id = (int)a.ActorID, Origin = a.Location, Width = g.Width, Depth = g.Depth };
				DrawFire(p, Hash((int)a.ActorID, 4) % 100, 0, fireFrame);
			}
		}

		static int Hash(int a, int b)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 40503u;
				h ^= h >> 15;
				return (int)(h & 0x7fffffff);
			}
		}

		static WPos Center(Map map, Property p)
		{
			return map.CenterOfCell(p.Origin) + new WVec((p.Width - 1) * 512, (p.Depth - 1) * 512, 0);
		}
	}
}
