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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Draws the zone colours on zoned cells that have no building. Strong while the zone paint tool is active, subtle otherwise.")]
	public class ZoneOverlayInfo : TraitInfo, Requires<ZoneLayerInfo>
	{
		[Desc("Sprite definition.")]
		public readonly string Image = "overlays";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence with one frame per ZoneType.")]
		public readonly string Sequence = "zone";

		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("Sequence for zoned cells that can never take a lot (hatched). Optional.")]
		public readonly string DimSequence = "dim";

		[PaletteReference]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[Desc("Opacity while a ZoneOrderGenerator is the active order generator.")]
		public readonly float ActiveAlpha = 1f;

		[Desc("Opacity otherwise.")]
		public readonly float IdleAlpha = 0.4f;

		public override object Create(ActorInitializer init) { return new ZoneOverlay(init.Self, this); }
	}

	public class ZoneOverlay : IRenderOverlay, IWorldLoaded, ITickRender, INotifyActorDisposing
	{
		readonly ZoneOverlayInfo info;
		readonly World world;
		readonly ISpriteSequence sequence;
		readonly ISpriteSequence dimSequence;
		readonly HashSet<CPos> dirty = [];

		ZoneLayer zoneLayer;
		ZoneGrowth growth;
		IRoadNetwork roads;
		int roadVersion = -1;
		TerrainSpriteLayer render;
		PaletteReference palette;
		bool active;
		bool disposed;

		public ZoneOverlay(Actor self, ZoneOverlayInfo info)
		{
			this.info = info;
			world = self.World;
			sequence = world.Map.Sequences.GetSequence(info.Image, info.Sequence);
			if (!string.IsNullOrEmpty(info.DimSequence) && world.Map.Sequences.HasSequence(info.Image, info.DimSequence))
				dimSequence = world.Map.Sequences.GetSequence(info.Image, info.DimSequence);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			zoneLayer = w.WorldActor.TraitOrDefault<ZoneLayer>();
			if (zoneLayer == null)
				return;

			growth = w.WorldActor.TraitOrDefault<ZoneGrowth>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			var empty = sequence.GetSprite(0);
			palette = wr.Palette(info.Palette);
			render = new TerrainSpriteLayer(w, wr, empty, empty.BlendMode, false);

			zoneLayer.ZoneChanged += MarkDirty;
			w.ActorAdded += OnActorChanged;
			w.ActorRemoved += OnActorChanged;

			RefreshAll();
		}

		void MarkDirty(CPos cell)
		{
			dirty.Add(cell);
		}

		void OnActorChanged(Actor a)
		{
			if (zoneLayer == null || !a.Info.HasTraitInfo<BuildingInfo>() || a.OccupiesSpace == null)
				return;

			foreach (var (cell, _) in a.OccupiesSpace.OccupiedCells())
				if (zoneLayer.GetZone(cell) != ZoneType.None)
					dirty.Add(cell);
		}

		void RefreshAll()
		{
			for (var z = ZoneType.ResidentialLow; z <= ZoningOrders.LastZone; z++)
				foreach (var cell in zoneLayer.ZonedCells(z))
					dirty.Add(cell);
		}

		bool HasBuilding(CPos cell)
		{
			foreach (var a in world.ActorMap.GetActorsAt(cell))
			{
				if (!a.Info.HasTraitInfo<BuildingInfo>())
					continue;

				var bulldozable = a.Info.TraitInfoOrDefault<BulldozableInfo>();
				if (bulldozable != null && bulldozable.AutoClear)
					continue;

				return true;
			}

			return false;
		}

		void UpdateCell(CPos cell, float alpha)
		{
			var zone = zoneLayer.GetZone(cell);
			if (zone == ZoneType.None || HasBuilding(cell))
				render.Clear(cell);
			else
			{
				var seq = dimSequence != null && growth != null && !growth.CanEverGrow(cell) ? dimSequence : sequence;
				render.Update(cell, seq.GetSprite(Math.Min((int)zone, seq.Length - 1)), palette, seq.Scale, alpha);
			}
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (render == null)
				return;

			// Roads decide which cells can take lots: refresh the dim marks when the network changed.
			if (roads != null && roads.NetworkVersion != roadVersion)
			{
				roadVersion = roads.NetworkVersion;
				RefreshAll();
			}

			var nowActive = world.OrderGenerator is ZoneOrderGenerator;
			if (nowActive != active)
			{
				active = nowActive;
				RefreshAll();
			}

			if (dirty.Count == 0)
				return;

			var alpha = active ? info.ActiveAlpha : info.IdleAlpha;
			foreach (var cell in dirty)
				if (world.Map.Contains(cell))
					UpdateCell(cell, alpha);

			dirty.Clear();
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			render?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			if (zoneLayer != null)
				zoneLayer.ZoneChanged -= MarkDirty;

			world.ActorAdded -= OnActorChanged;
			world.ActorRemoved -= OnActorChanged;
			render?.Dispose();
			disposed = true;
		}
	}
}
