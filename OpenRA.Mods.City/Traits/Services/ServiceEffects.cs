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

using System.Collections.Generic;
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Draws flames on burning buildings and garbage heaps in front of piled-up lots. Reads ServiceSimulation, never changes it.")]
	public class ServiceEffectsInfo : TraitInfo
	{
		public readonly string Image = "svc-fire";
		public readonly string FireSequence = "idle";
		public readonly string PileImage = "svc-pile";
		public readonly string HeliImage = "svc-heli";
		public readonly string Palette = "city";

		[Desc("Ticks per animation frame of the flames.")]
		public readonly int FireFrameTicks = 6;

		public override object Create(ActorInitializer init) { return new ServiceEffects(init.Self, this); }
	}

	public class ServiceEffects : IRenderAboveShroud, IWorldLoaded
	{
		readonly ServiceEffectsInfo info;
		readonly World world;
		readonly List<ServiceSimulation> sims = [];
		readonly List<Property> piles = [];
		ISpriteSequence fire;
		ISpriteSequence[] pile;
		ISpriteSequence heli;
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
			fire = seqs.GetSequence(info.Image, info.FireSequence);
			pile =
			[
				seqs.GetSequence(info.PileImage, "small"),
				seqs.GetSequence(info.PileImage, "medium"),
				seqs.GetSequence(info.PileImage, "large"),
			];

			heli = seqs.GetSequence(info.HeliImage, "idle");
			palette = wr.Palette(info.Palette);
			foreach (var p in w.Players)
			{
				var sim = p.PlayerActor.TraitOrDefault<ServiceSimulation>();
				if (sim != null && p.Playable)
					sims.Add(sim);
			}
		}

		bool IRenderAboveShroud.SpatiallyPartitionable => false;

		IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			if (sims.Count == 0 || fire == null)
				yield break;

			var map = world.Map;
			var frame = world.WorldTick / System.Math.Max(1, info.FireFrameTicks) % fire.Length;
			var view = wr.Viewport;
			var tl = view.TopLeft - new int2(160, 160);
			var br = view.BottomRight + new int2(160, 160);

			foreach (var sim in sims)
			{
				foreach (var id in sim.BurningProperties)
				{
					var p = sim.GetProperty(id);
					if (p == null)
						continue;

					var pos = Center(map, p);
					var s = wr.ScreenPxPosition(pos);
					if (s.X < tl.X || s.Y < tl.Y || s.X > br.X || s.Y > br.Y)
						continue;

					var scale = p.Width >= 3 ? 2.5f : p.Width == 2 ? 1.8f : 1.3f;
					yield return new SpriteRenderable(fire.GetSprite(frame), pos, WVec.Zero, 2048, palette, scale, 1f, new Vector3(1f, 1f, 1f), TintModifiers.None, false);
				}

				wildCells.Clear();
				sim.GetWildfires(wildCells);
				foreach (var c in wildCells)
				{
					var wp = map.CenterOfCell(c);
					var ws = wr.ScreenPxPosition(wp);
					if (ws.X < tl.X || ws.Y < tl.Y || ws.X > br.X || ws.Y > br.Y)
						continue;

					yield return new SpriteRenderable(fire.GetSprite(frame), wp, WVec.Zero, 2048, palette, 1f, 1f, new Vector3(1f, 1f, 1f), TintModifiers.None, false);
				}

				flights.Clear();
				sim.GetFlights(flights);
				foreach (var f in flights)
				{
					var from = map.CenterOfCell(f.From);
					var to = map.CenterOfCell(f.To);
					var pos = from + new WVec((to.X - from.X) * f.Permille / 1000, (to.Y - from.Y) * f.Permille / 1000, 0);
					var hs = wr.ScreenPxPosition(pos);
					if (hs.X < tl.X || hs.Y < tl.Y || hs.X > br.X || hs.Y > br.Y)
						continue;

					yield return new SpriteRenderable(heli.GetSprite(world.WorldTick / 2 % heli.Length), pos, WVec.Zero, 4096, palette, 1f, 1f, new Vector3(1f, 1f, 1f), TintModifiers.None, false);
				}

				piles.Clear();
				sim.GetPiles(piles);
				foreach (var p in piles)
				{
					var pos = map.CenterOfCell(p.Origin + new CVec(0, p.Depth - 1)) + new WVec(-384, 384, 0);
					var s = wr.ScreenPxPosition(pos);
					if (s.X < tl.X || s.Y < tl.Y || s.X > br.X || s.Y > br.Y)
						continue;

					var level = sim.GetPileLevel(p.Id);
					if (level <= 0)
						continue;

					yield return new SpriteRenderable(pile[level - 1].GetSprite(0), pos, WVec.Zero, 1536, palette, 1f, 1f, new Vector3(1f, 1f, 1f), TintModifiers.None, false);
				}
			}
		}

		static WPos Center(Map map, Property p)
		{
			return map.CenterOfCell(p.Origin) + new WVec((p.Width - 1) * 512, (p.Depth - 1) * 512, 0);
		}
	}
}
