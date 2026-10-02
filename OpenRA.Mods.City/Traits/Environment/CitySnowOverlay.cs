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
	[Desc("Render-only snow cover on open ground in winter. Terrain-matched light and full snow blend smoothly as cover changes.")]
	public class CitySnowOverlayInfo : TraitInfo
	{
		public readonly string Image = "envsnow";

		[SequenceReference(nameof(Image))]
		public readonly string Sequence = "snow";

		[PaletteReference]
		public readonly string Palette = "city";

		[Desc("Opacity of the snow tiles. The tiles are full winter versions of each terrain template, so 1 replaces the ground.")]
		public readonly float Alpha = 1f;

		[Desc("Cover percentage at which the light-snow texture is fully visible. Above this, blend towards full snow.")]
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
		CellLayer<int> templateFrame;
		TerrainSpriteLayer lightLayer;
		TerrainSpriteLayer fullLayer;
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
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			lightLayer = new TerrainSpriteLayer(w, wr, sequence.GetSprite(0), BlendMode.Alpha, false);
			fullLayer = new TerrainSpriteLayer(w, wr, sequence.GetSprite(0), BlendMode.Alpha, false);
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
			if (lightLayer == null || atmosphere == null)
				return;

			var steps = Math.Max(1, info.Steps);
			var cover = atmosphere.SnowCover;
			var step = Math.Clamp((int)(cover * steps + 0.5f), 0, steps);
			var roadVersion = roads?.NetworkVersion ?? 0;
			if (step != lastStep || roadVersion != lastRoadVersion)
			{
				Rebuild(step, steps);
				lastStep = step;
				lastRoadVersion = roadVersion;
			}

			if (step > 0)
			{
				lightLayer.Draw(wr.Viewport);
				fullLayer.Draw(wr.Viewport);
			}
		}

		void Rebuild(int step, int steps)
		{
			var map = world.Map;
			var cover = (float)step / steps;
			var lightBand = Math.Clamp(info.LightBand / 100f, 0.01f, 0.99f);
			var lightOpacity = Math.Min(1f, cover / lightBand) * info.Alpha;
			var fullOpacity = Math.Clamp((cover - lightBand) / (1f - lightBand), 0f, 1f) * info.Alpha;
			foreach (var cell in map.AllCells)
			{
				var type = map.GetTerrainInfo(cell).Type;
				if (step == 0 || Array.IndexOf(info.ExcludedTerrainTypes, type) >= 0 || (roads != null && roads.IsRoad(cell)))
				{
					lightLayer.Clear(cell);
					fullLayer.Clear(cell);
					continue;
				}

				// Use the authored texture's detail, not hard 2x2-cell noise patches. Both layers follow the
				// ground template so fractional cover cannot turn the landscape into a checkerboard.
				var frame = templateFrame != null ? templateFrame[cell] : -1;
				var fullFrame = frame >= 0 ? 2 * frame : 2 * (EnvHash.Hash(cell.X, cell.Y, 3) % Math.Max(1, sequence.Length / 2));
				lightLayer.Update(cell, sequence.GetSprite(Math.Min(fullFrame + 1, sequence.Length - 1)), palette, 1f, lightOpacity);
				if (fullOpacity > 0)
					fullLayer.Update(cell, sequence.GetSprite(fullFrame), palette, 1f, fullOpacity);
				else
					fullLayer.Clear(cell);
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			lightLayer?.Dispose();
			fullLayer?.Dispose();
			disposed = true;
		}
	}
}
