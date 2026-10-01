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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only snow cover on open ground in winter. Drawn with the terrain (inside the tinted pass), patchy while it builds up or melts.")]
	public class CitySnowOverlayInfo : TraitInfo
	{
		public readonly string Image = "envsnow";

		[SequenceReference(nameof(Image))]
		public readonly string Sequence = "snow";

		[PaletteReference]
		public readonly string Palette = "city";

		public readonly float Alpha = 0.92f;

		[Desc("Terrain types that never get snow.")]
		public readonly string[] ExcludedTerrainTypes = ["Water"];

		[Desc("Snow cover steps (the overlay is rebuilt when the cover moves to another step).")]
		public readonly int Steps = 32;

		public override object Create(ActorInitializer init) { return new CitySnowOverlay(init.Self, this); }
	}

	public class CitySnowOverlay : IRenderOverlay, IWorldLoaded, INotifyActorDisposing
	{
		readonly CitySnowOverlayInfo info;
		readonly World world;
		readonly ISpriteSequence sequence;
		readonly CellLayer<bool> shown;
		TerrainSpriteLayer layer;
		PaletteReference palette;
		CityAtmosphere atmosphere;
		IRoadNetwork roads;
		int lastStep = -1;
		int lastRoadVersion = -1;
		bool disposed;

		public CitySnowOverlay(Actor self, CitySnowOverlayInfo info)
		{
			this.info = info;
			world = self.World;
			sequence = world.Map.Sequences.GetSequence(info.Image, info.Sequence);
			shown = new CellLayer<bool>(world.Map);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			layer = new TerrainSpriteLayer(w, wr, sequence.GetSprite(0), BlendMode.Alpha, false);
			palette = wr.Palette(info.Palette);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (layer == null || atmosphere == null)
				return;

			var steps = Math.Max(1, info.Steps);
			var cover = atmosphere.SnowCover;
			var step = (int)(cover * steps + 0.5f);
			var roadVersion = roads?.NetworkVersion ?? 0;
			if (step != lastStep || roadVersion != lastRoadVersion)
			{
				Rebuild(step, steps);
				lastStep = step;
				lastRoadVersion = roadVersion;
			}

			if (step > 0)
				layer.Draw(wr.Viewport);
		}

		void Rebuild(int step, int steps)
		{
			var map = world.Map;
			var threshold = step * 100 / steps;
			foreach (var cell in map.AllCells)
			{
				var show = false;
				if (threshold > 0)
				{
					// Patches of 2x2 cells melt away first (a blocky but natural look).
					var patch = EnvHash.Hash(cell.X >> 1, cell.Y >> 1, 77) % 100;
					var type = map.GetTerrainInfo(cell).Type;
					show = patch < threshold && Array.IndexOf(info.ExcludedTerrainTypes, type) < 0 && (roads == null || !roads.IsRoad(cell));
				}

				if (show == shown[cell])
					continue;

				shown[cell] = show;
				if (show)
					layer.Update(cell, sequence.GetSprite(EnvHash.Hash(cell.X, cell.Y, 3) % sequence.Length), palette, 1f, info.Alpha);
				else
					layer.Clear(cell);
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			layer?.Dispose();
			disposed = true;
		}
	}
}
