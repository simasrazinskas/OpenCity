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
	[Desc("Render-only animated water: a looping water frame over every visible water cell (open and shore water templates).",
		"The frame comes from the real-time clock, never from simulation state.")]
	public class CityWaterAnimationInfo : TraitInfo
	{
		public readonly string Image = "envwater";

		[SequenceReference(nameof(Image))]
		public readonly string Sequence = "water";

		[PaletteReference]
		public readonly string Palette = "city";

		[Desc("Animation frames per water template in the sequence (frame = waterIndex * Frames + step).")]
		public readonly int Frames = 8;

		[Desc("Real-time milliseconds per animation frame (190 is about 5 fps).")]
		public readonly int FrameMs = 190;

		[Desc("First template id of the open water templates (waterIndex = id - OpenBase).")]
		public readonly int OpenBase = 100;

		[Desc("Number of open water templates.")]
		public readonly int OpenCount = 8;

		[Desc("First template id of the shore water templates (waterIndex = OpenCount + id - ShoreBase).")]
		public readonly int ShoreBase = 400;

		[Desc("Number of shore water templates.")]
		public readonly int ShoreCount = 46;

		public override object Create(ActorInitializer init) { return new CityWaterAnimation(init.Self, this); }
	}

	public class CityWaterAnimation : IRenderOverlay, IWorldLoaded, INotifyActorDisposing
	{
		readonly CityWaterAnimationInfo info;
		readonly World world;
		readonly ISpriteSequence sequence;
		readonly CellLayer<int> waterIndex;
		readonly CellLayer<int> shown;
		TerrainSpriteLayer layer;
		PaletteReference palette;
		bool anyWater;
		bool disposed;

		public CityWaterAnimation(Actor self, CityWaterAnimationInfo info)
		{
			this.info = info;
			world = self.World;
			sequence = world.Map.Sequences.GetSequence(info.Image, info.Sequence);
			waterIndex = new CellLayer<int>(world.Map);
			shown = new CellLayer<int>(world.Map);
			shown.Clear(-1);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			layer = new TerrainSpriteLayer(w, wr, sequence.GetSprite(0), BlendMode.Alpha, false);
			palette = wr.Palette(info.Palette);

			var map = w.Map;
			foreach (var cell in map.AllCells)
			{
				var id = map.Tiles[cell].Type;
				var index = -1;
				if (id >= info.OpenBase && id < info.OpenBase + info.OpenCount)
					index = id - info.OpenBase;
				else if (id >= info.ShoreBase && id < info.ShoreBase + info.ShoreCount)
					index = info.OpenCount + id - info.ShoreBase;

				if (index >= 0 && (index + 1) * info.Frames > sequence.Length)
					index = -1;

				waterIndex[cell] = index;
				anyWater |= index >= 0;
			}
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (layer == null || !anyWater)
				return;

			var frames = System.Math.Max(1, info.Frames);
			var step = (int)(Game.RunTime / System.Math.Max(1, info.FrameMs) % frames);
			var map = world.Map;
			foreach (var puv in wr.Viewport.AllVisibleCells)
			{
				var cell = ((MPos)puv).ToCPos(map);
				if (!waterIndex.Contains(cell))
					continue;

				var index = waterIndex[cell];
				if (index < 0)
					continue;

				var want = index * frames + step;
				if (want == shown[cell])
					continue;

				shown[cell] = want;
				layer.Update(cell, sequence.GetSprite(want), palette, 1f, 1f);
			}

			layer.Draw(wr.Viewport);
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
