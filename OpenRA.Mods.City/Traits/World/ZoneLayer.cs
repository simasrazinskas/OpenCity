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
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Per-cell zone designation (residential/commercial/industrial/office).")]
	public class ZoneLayerInfo : TraitInfo
	{
		[Desc("Cells can only be zoned if within this many cells (Chebyshev distance) of a road.")]
		public readonly int ZoneDepth = 4;

		[Desc("Terrain types that can never be zoned.")]
		public readonly ImmutableArray<string> BlockedTerrain = ["Water", "Road"];

		public override object Create(ActorInitializer init) { return new ZoneLayer(init.Self, this); }
	}

	public class ZoneLayer : IWorldLoaded, ITick
	{
		public readonly ZoneLayerInfo Info;

		readonly World world;
		readonly CellLayer<byte> zones;
		readonly int[] counts = new int[Enum.GetValues<ZoneType>().Length];

		IRoadNetwork roads;
		RoadLayer roadLayer;
		UtilityNetwork utilities;
		ExtractorAreaLayer extractorAreas;
		int clearedVersion = -1;

		// Cached "is within ZoneDepth of a road" flags, lazily rebuilt when the road network changed.
		readonly CellLayer<bool> nearRoad;
		int nearRoadVersion = -1;
		bool nearRoadDirty = true;

		public ZoneLayer(Actor self, ZoneLayerInfo info)
		{
			Info = info;
			world = self.World;
			zones = new CellLayer<byte>(world.Map);
			nearRoad = new CellLayer<bool>(world.Map);
		}

		/// <summary>Fired after a cell's zone changes.</summary>
		public event Action<CPos> ZoneChanged;

		/// <summary>Incremented whenever any zone changes (cache invalidation for listeners).</summary>
		public int Version { get; private set; }

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			roadLayer = roads as RoadLayer;
			utilities = w.WorldActor.TraitOrDefault<UtilityNetwork>();
			extractorAreas = w.WorldActor.TraitOrDefault<ExtractorAreaLayer>();
		}

		/// <summary>True if lots may use this road cell as their frontage: a street type that gives access and allows zoning (never highways).</summary>
		public bool IsFrontageRoad(CPos road)
		{
			if (roadLayer == null)
				return roads != null && roads.IsRoad(road);

			return roadLayer.IsRoad(road) && roadLayer.GivesAccess(road) && roadLayer.IsZoneable(road);
		}

		/// <summary>Cells that never take zones or lots: roundabout islands, HV power line cells and industry hub areas.</summary>
		public bool IsReservedCell(CPos cell)
		{
			if (roadLayer != null && roadLayer.IsIsland(cell))
				return true;

			if (utilities != null && utilities.HasPowerLine(cell))
				return true;

			return extractorAreas != null && extractorAreas.GetHub(cell) != 0;
		}

		void ITick.Tick(Actor self)
		{
			if (roads == null || roads.NetworkVersion == clearedVersion)
				return;

			clearedVersion = roads.NetworkVersion;
			nearRoadDirty = true;

			// A new road replaces any zone below it. Removing a road keeps the zones (as in Cities: Skylines).
			foreach (var cell in roads.RoadCells)
				if (zones.Contains(cell) && zones[cell] != 0)
					SetZone(cell, ZoneType.None);
		}

		public ZoneType GetZone(CPos cell)
		{
			return zones.Contains(cell) ? (ZoneType)zones[cell] : ZoneType.None;
		}

		/// <summary>True if the cell may be zoned now (in bounds, not road/water, no non-growable building, near a road).</summary>
		public bool CanZone(CPos cell)
		{
			if (roads == null || !world.Map.Contains(cell) || roads.IsRoad(cell) || IsReservedCell(cell))
				return false;

			if (Info.BlockedTerrain.Contains(world.Map.GetTerrainInfo(cell).Type))
				return false;

			if (!IsNearRoad(cell))
				return false;

			foreach (var a in world.ActorMap.GetActorsAt(cell))
			{
				if (a.Info.HasTraitInfo<GrowableBuildingInfo>())
					continue;

				var bulldozable = a.Info.TraitInfoOrDefault<BulldozableInfo>();
				if (bulldozable != null && bulldozable.AutoClear)
					continue;

				if (a.Info.HasTraitInfo<BuildingInfo>())
					return false;
			}

			return true;
		}

		bool IsNearRoad(CPos cell)
		{
			if (nearRoadDirty || nearRoadVersion != roads.NetworkVersion)
				RebuildNearRoad();

			return nearRoad[cell];
		}

		void RebuildNearRoad()
		{
			nearRoad.Clear(false);
			var depth = Info.ZoneDepth;
			var zoning = roadLayer != null ? roadLayer.ZoningCells : roads.RoadCells;
			foreach (var road in zoning)
			{
				for (var dy = -depth; dy <= depth; dy++)
				{
					for (var dx = -depth; dx <= depth; dx++)
					{
						var c = new CPos(road.X + dx, road.Y + dy);
						if (nearRoad.Contains(c))
							nearRoad[c] = true;
					}
				}
			}

			nearRoadDirty = false;
			nearRoadVersion = roads.NetworkVersion;
		}

		/// <summary>Sets the zone (ZoneType.None clears). Returns true if changed. Call only from order handlers.</summary>
		public bool SetZone(CPos cell, ZoneType zone)
		{
			if (!zones.Contains(cell))
				return false;

			var old = (ZoneType)zones[cell];
			if (old == zone)
				return false;

			counts[(int)old]--;
			counts[(int)zone]++;
			zones[cell] = (byte)zone;
			Version++;
			ZoneChanged?.Invoke(cell);
			return true;
		}

		public int CountZoned(ZoneType zone) { return zone == ZoneType.None ? 0 : counts[(int)zone]; }

		/// <summary>Cells with the given zone, in row-major order (scans the map; do not call per frame).</summary>
		public IEnumerable<CPos> ZonedCells(ZoneType zone)
		{
			if (zone == ZoneType.None || counts[(int)zone] == 0)
				yield break;

			var z = (byte)zone;
			foreach (var cell in world.Map.AllCells)
				if (zones[cell] == z)
					yield return cell;
		}
	}
}
