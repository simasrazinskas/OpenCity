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
using OpenRA.Mods.Common.Terrain;
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

		[Desc("Opacity of the snow tiles. The tiles are full winter versions of each terrain template, so 1 replaces the ground.")]
		public readonly float Alpha = 1f;

		[Desc("Width (in cover percent) of the light-snow band ahead of full snow: patches just beyond the full-snow",
			"threshold show the template's light (patchy) snow frame, so no patch stays bare once cover passes this value.")]
		public readonly int LightBand = 45;

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
		readonly CellLayer<int> shown;
		CellLayer<int> templateFrame;
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
			shown = new CellLayer<int>(world.Map);
			shown.Clear(-1);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			layer = new TerrainSpriteLayer(w, wr, sequence.GetSprite(0), BlendMode.Alpha, false);
			palette = wr.Palette(info.Palette);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();

			// Frame layout (sequences/environment.yaml): 2 * terrain frame of the cell's template + 0 full / 1 light snow.
			if (w.Map.Rules.TerrainInfo is ITemplatedTerrainInfo terrain)
			{
				templateFrame = new CellLayer<int>(w.Map);
				foreach (var cell in w.Map.AllCells)
				{
					var frame = -1;
					if (terrain.Templates.TryGetValue(w.Map.Tiles[cell].Type, out var t) && t is DefaultTerrainTemplateInfo dt && dt.Frames.Length > 0)
						frame = dt.Frames[0];

					templateFrame[cell] = 2 * frame + 1 < sequence.Length ? frame : -1;
				}
			}
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
				// -1 bare, else the sprite frame. Patches of 2x2 cells go from bare to light to full snow as the cover builds up
				// (and back when it melts); the light frames keep their edges snowy so every mix of neighbours joins.
				var want = -1;
				if (threshold > 0)
				{
					var patch = EnvHash.Hash(cell.X >> 1, cell.Y >> 1, 77) % 100;
					var light = patch >= threshold;
					var type = map.GetTerrainInfo(cell).Type;
					if (patch < threshold + info.LightBand && Array.IndexOf(info.ExcludedTerrainTypes, type) < 0 && (roads == null || !roads.IsRoad(cell)))
					{
						var frame = templateFrame != null ? templateFrame[cell] : -1;
						if (frame >= 0)
							want = 2 * frame + (light ? 1 : 0);
						else if (!light)
							want = EnvHash.Hash(cell.X, cell.Y, 3) % sequence.Length;
					}
				}

				if (want == shown[cell])
					continue;

				shown[cell] = want;
				if (want >= 0)
					layer.Update(cell, sequence.GetSprite(want), palette, 1f, info.Alpha);
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
