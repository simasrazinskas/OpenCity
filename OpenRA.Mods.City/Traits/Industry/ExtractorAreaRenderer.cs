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
		[Desc("Sprite definition with one sequence per area kind (farm-grain, farm-vegetables, farm-livestock, farm-cotton, quarry, mine, oil, fish)",
			"and a '-winter' twin of each.")]
		public readonly string Image = "extractor-area";

		[Desc("Sequences whose frames are growth stages (ploughed, sprouting, growing, ripe) that follow the calendar instead of a per-cell variant.")]
		public readonly string[] CropSequences = ["farm-grain", "farm-vegetables", "farm-cotton"];

		[Desc("Snow cover (0..100, CityClimate.SnowDepth) from which the '-winter' sequences are drawn.")]
		public readonly int WinterSnowDepth = 25;

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
		readonly HashSet<CPos> shown = [];
		readonly Dictionary<string, ISpriteSequence> sequences = [];

		ExtractorAreaLayer layer;
		CityClimate climate;
		int lookEpoch = -1;
		bool lookWinter;
		float lookMonth = -1;
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

			climate = w.WorldActor.TraitOrDefault<CityClimate>();
			var empty = Sequence("quarry").GetSprite(0);
			palette = wr.Palette(info.Palette);
			render = new TerrainSpriteLayer(w, wr, empty, empty.BlendMode, false);
			layer.CellChanged += MarkDirty;
		}

		void MarkDirty(CPos cell)
		{
			dirty.Add(cell);
		}

		// The look (growth stage, snow) changes in 0.2 month steps; every shown cell is redrawn when it does.
		int SeasonEpoch(out bool winter, out float month)
		{
			winter = false;
			month = -1;
			if (climate?.Clock == null)
				return 0;

			month = climate.MonthFloat(climate.Clock.Now);
			winter = climate.SnowDepth >= info.WinterSnowDepth;
			return (int)(month * 5) * 2 + (winter ? 1 : 0);
		}

		// Calendar of the crop fields (northern year, Jan = 0): ploughed until May, sprouting, growing, ripe in August and
		// September, ploughed again after the harvest. The cell hash shifts the dates by up to 0.4 month so fields do not flip at once.
		static int CropStage(CPos cell, float month)
		{
			var m = month + (IndustryHash.Mix(cell, 6) % 9 - 4) * 0.1f;
			if (m < 0)
				m += 12;
			else if (m >= 12)
				m -= 12;

			if (m < 4f || m >= 9.5f)
				return 0;

			return m < 5.5f ? 1 : m < 7.5f ? 2 : 3;
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (render == null)
				return;

			var epoch = SeasonEpoch(out var winter, out var month);
			if (epoch != lookEpoch)
			{
				lookEpoch = epoch;
				lookWinter = winter;
				lookMonth = month;
				foreach (var c in shown)
					dirty.Add(c);
			}

			if (dirty.Count == 0)
				return;

			foreach (var cell in dirty)
			{
				if (!world.Map.Contains(cell))
					continue;

				var hub = layer.GetHubAt(cell);
				var baseName = hub == null ? null : SequenceName(hub);
				var name = baseName != null && lookWinter && Sequence(baseName + "-winter") != null ? baseName + "-winter" : baseName;
				var seq = name == null ? null : Sequence(name);
				if (seq == null)
				{
					render.Clear(cell);
					shown.Remove(cell);
					continue;
				}

				var frames = Math.Max(1, Math.Min(info.Variants, seq.Length));
				var crop = lookMonth >= 0 && Array.IndexOf(info.CropSequences, baseName) >= 0;
				var variant = crop ? Math.Min(CropStage(cell, lookMonth), frames - 1) : IndustryHash.Mix(cell, 5) % frames;
				render.Update(cell, seq.GetSprite(variant), palette, seq.Scale, 1f);
				shown.Add(cell);
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
