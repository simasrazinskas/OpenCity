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

using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only flood water on the cells CityDisasters reports as flooded. Drawn with the terrain.")]
	public class CityFloodOverlayInfo : TraitInfo
	{
		public readonly string Image = "envflood";

		[SequenceReference(nameof(Image))]
		public readonly string Sequence = "flood";

		[PaletteReference]
		public readonly string Palette = "city";

		public readonly float Alpha = 0.8f;

		[Desc("Real-time milliseconds per water animation frame.")]
		public readonly int FrameMs = 700;

		public override object Create(ActorInitializer init) { return new CityFloodOverlay(init.Self, this); }
	}

	public class CityFloodOverlay : IRenderOverlay, IWorldLoaded, INotifyActorDisposing
	{
		readonly CityFloodOverlayInfo info;
		readonly World world;
		readonly ISpriteSequence sequence;
		readonly CellLayer<int> shown;
		TerrainSpriteLayer layer;
		PaletteReference palette;
		CityDisasters disasters;
		int lastVersion = -1;
		int lastFrame = -1;
		bool disposed;

		public CityFloodOverlay(Actor self, CityFloodOverlayInfo info)
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
			disasters = w.WorldActor.TraitOrDefault<CityDisasters>();
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (layer == null || disasters == null || !disasters.Enabled)
				return;

			var frame = (int)(Game.RunTime / System.Math.Max(1, info.FrameMs)) % sequence.Length;
			if (disasters.FloodVersion != lastVersion || frame != lastFrame)
			{
				Rebuild(frame);
				lastVersion = disasters.FloodVersion;
				lastFrame = frame;
			}

			if (disasters.FloodLevel > 0)
				layer.Draw(wr.Viewport);
		}

		void Rebuild(int frame)
		{
			foreach (var cell in world.Map.AllCells)
			{
				var want = disasters.IsFlooded(cell) ? frame : -1;
				if (want == shown[cell])
					continue;

				shown[cell] = want;
				if (want < 0)
					layer.Clear(cell);
				else
					layer.Update(cell, sequence.GetSprite(want), palette, 1f, info.Alpha);
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
