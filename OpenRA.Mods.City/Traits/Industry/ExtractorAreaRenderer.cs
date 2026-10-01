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
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Draws the field tiles of extractor areas (crop rows, quarry pit, mine gravel, oil pad). Cosmetic: no sim cost, forestry draws nothing.")]
	public class ExtractorAreaRendererInfo : TraitInfo, Requires<ExtractorAreaLayerInfo>
	{
		[Desc("Sprite definition with one sequence per area kind (farm-grain, farm-vegetables, farm-livestock, farm-cotton, quarry, mine, oil).")]
		public readonly string Image = "extractor-area";

		[PaletteReference]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[Desc("Number of tile variants per sequence (chosen by cell hash).")]
		public readonly int Variants = 4;

		public override object Create(ActorInitializer init) { return new ExtractorAreaRenderer(init.Self, this); }
	}

	public class ExtractorAreaRenderer : IRenderOverlay, IWorldLoaded, ITickRender, INotifyActorDisposing
	{
		readonly ExtractorAreaRendererInfo info;
		readonly World world;
		readonly HashSet<CPos> dirty = [];
		readonly Dictionary<string, ISpriteSequence> sequences = [];

		ExtractorAreaLayer layer;
		TerrainSpriteLayer render;
		PaletteReference palette;
		bool disposed;

		public ExtractorAreaRenderer(Actor self, ExtractorAreaRendererInfo info)
		{
			this.info = info;
			world = self.World;
		}

		ISpriteSequence Sequence(string name)
		{
			if (!sequences.TryGetValue(name, out var seq))
			{
				seq = world.Map.Sequences.HasSequence(info.Image, name) ? world.Map.Sequences.GetSequence(info.Image, name) : null;
				sequences[name] = seq;
			}

			return seq;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			layer = w.WorldActor.TraitOrDefault<ExtractorAreaLayer>();
			if (layer == null || !w.Map.Sequences.HasSequence(info.Image, "quarry"))
				return;

			var empty = Sequence("quarry").GetSprite(0);
			palette = wr.Palette(info.Palette);
			render = new TerrainSpriteLayer(w, wr, empty, empty.BlendMode, false);
			layer.CellChanged += MarkDirty;
		}

		void MarkDirty(CPos cell)
		{
			dirty.Add(cell);
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (render == null || dirty.Count == 0)
				return;

			foreach (var cell in dirty)
			{
				if (!world.Map.Contains(cell))
					continue;

				var hub = layer.GetHubAt(cell);
				var name = hub == null ? null : SequenceName(hub);
				var seq = name == null ? null : Sequence(name);
				if (seq == null)
				{
					render.Clear(cell);
					continue;
				}

				var variant = IndustryHash.Mix(cell, 5) % Math.Max(1, Math.Min(info.Variants, seq.Length));
				render.Update(cell, seq.GetSprite(variant), palette, seq.Scale, 1f);
			}

			dirty.Clear();
		}

		static string SequenceName(ExtractorHub hub)
		{
			var name = hub.Info.AreaSequence;
			if (string.IsNullOrEmpty(name))
				return null;

			return hub.Farm ? name + "-" + hub.Product.ToLowerInvariant() : name;
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			render?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			if (layer != null)
				layer.CellChanged -= MarkDirty;

			render?.Dispose();
			disposed = true;
		}
	}
}
