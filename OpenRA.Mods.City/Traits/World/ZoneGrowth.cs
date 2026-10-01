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
	[Desc("Spawns lot buildings (1x1 up to 4x3) on zoned frontage next to connected roads. Spawn pressure per zone comes from the demand",
		"accumulator: acc += max(0, demand); one lot per DemandPerSpawn points.")]
	public class ZoneGrowthInfo : TraitInfo, Requires<ZoneLayerInfo>
	{
		[Desc("Ticks between growth attempts.")]
		public readonly int Interval = 10;

		[Desc("Each full step of this much accumulated demand allows one new building.")]
		public readonly int DemandPerSpawn = 25;

		[Desc("Maximum number of buildings of one zone type that may be under construction at the same time.")]
		public readonly int MaxUnderConstruction = 12;

		[Desc("Number of random frontage anchors planned per spawn; the best scoring lot is built.")]
		public readonly int Candidates = 3;

		[Desc("Visual theme of new lots: Any (neighbourhoods of 8x8 cells alternate), NA (North American) or EU (European).")]
		public readonly string Theme = "Any";

		[Desc("Frontage is searched this many cells either way from an anchor.")]
		public readonly int RunReach = 12;

		public override object Create(ActorInitializer init) { return new ZoneGrowth(init.Self, this); }
	}

	public partial class ZoneGrowth : ITick, IWorldLoaded, INotifyActorDisposing, ICityAutoTestReporter
	{
		struct Anchor
		{
			public CPos Cell;
			public int Side;
		}

		public readonly ZoneGrowthInfo Info;

		readonly World world;
		readonly List<Anchor>[] anchors = new List<Anchor>[Enum.GetValues<ZoneType>().Length];
		readonly int[] underConstruction = new int[Enum.GetValues<ZoneType>().Length];
		readonly int[] accumulator = new int[Enum.GetValues<ZoneType>().Length];
		readonly int[] spawned = new int[Enum.GetValues<ZoneType>().Length];
		readonly HashSet<CPos> reserved = [];
		readonly List<Actor> clearList = [];

		LotCatalog catalog;
		ZoneLayer zones;
		IRoadNetwork roads;
		ILandValueSource landValue;
		CityCoverageLayer coverage;
		IDemandModel demandModel;
		IProgression progression;
		Player cityPlayer;
		CityManager manager;
		bool dirty = true;
		int roadVersion = -1;
		int rejected;
		int lastUnlockMask = -1;
		bool disposed;

		public ZoneGrowth(Actor self, ZoneGrowthInfo info)
		{
			Info = info;
			world = self.World;
			for (var i = 0; i < anchors.Length; i++)
				anchors[i] = [];
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			zones = w.WorldActor.TraitOrDefault<ZoneLayer>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			landValue = w.WorldActor.TraitsImplementing<ILandValueSource>().FirstOrDefault();
			coverage = w.WorldActor.TraitOrDefault<CityCoverageLayer>();
			demandModel = ZoningLookup.Find<IDemandModel>(w);
			progression = ZoningLookup.Find<IProgression>(w);
			catalog = new LotCatalog(w.Map.Rules);
			if (zones == null || roads == null)
				return;

			zones.ZoneChanged += OnCellChanged;
			w.ActorRemoved += OnActorRemoved;
		}

		void OnCellChanged(CPos cell)
		{
			dirty = true;
		}

		void OnActorRemoved(Actor a)
		{
			// A freed cell may become a candidate again. New actors need no invalidation: anchors are re-validated when picked.
			if (a.Info.HasTraitInfo<BuildingInfo>())
				dirty = true;
		}

		void ResolveCityPlayer()
		{
			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				var m = p.PlayerActor.TraitOrDefault<CityManager>();
				if (m == null)
					continue;

				cityPlayer = p;
				manager = m;

				// Player traits (demand, progression) must come from the city's own player, not the first one found.
				demandModel = p.PlayerActor.TraitsImplementing<IDemandModel>().FirstOrDefault() ?? demandModel;
				progression = p.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault() ?? progression;
				return;
			}
		}

		int Demand(ZoneType zone)
		{
			return demandModel != null ? demandModel.GetDemand(zone) : manager.GetDemand(zone.Category());
		}

		void ITick.Tick(Actor self)
		{
			if (zones == null || roads == null || world.WorldTick % Math.Max(1, Info.Interval) != 0)
				return;

			if (manager == null)
			{
				ResolveCityPlayer();
				if (manager == null)
					return;
			}

			// Lots reserved by the previous attempt exist as actors now.
			reserved.Clear();

			// Unlocks arrive with milestones: rebuild when the set of unlocked zones changes.
			var unlockMask = 0;
			for (var z = 1; z < anchors.Length; z++)
				if (IsZoneUnlocked((ZoneType)z))
					unlockMask |= 1 << z;

			if (unlockMask != lastUnlockMask)
			{
				lastUnlockMask = unlockMask;
				dirty = true;
			}

			if (dirty || roadVersion != roads.NetworkVersion)
				RebuildAnchors();

			CountUnderConstruction();

			for (var z = 1; z < anchors.Length; z++)
			{
				var zone = (ZoneType)z;
				if (!catalog.CanGrow(zone))
					continue;

				var list = anchors[z];
				if (list.Count == 0)
				{
					accumulator[z] = 0;
					continue;
				}

				// Accumulator (design 09 3.1): demand 10 spawns one building per ~3 attempts, demand 100 four per attempt.
				accumulator[z] += Math.Max(0, Demand(zone));
				var wanted = accumulator[z] / Math.Max(1, Info.DemandPerSpawn);
				accumulator[z] %= Math.Max(1, Info.DemandPerSpawn);
				wanted = Math.Min(wanted, Info.MaxUnderConstruction - underConstruction[z]);

				for (var i = 0; i < wanted && list.Count > 0; i++)
					TrySpawn(zone, list);
			}
		}

		void CountUnderConstruction()
		{
			Array.Clear(underConstruction);
			foreach (var a in world.ActorsHavingTrait<GrowableBuilding>())
			{
				var g = a.Trait<GrowableBuilding>();
				if (g.IsManaged && g.UnderConstruction && !g.IsUpgrading)
					underConstruction[(int)g.Zone]++;
			}
		}

		// Frontage anchors: zoned empty cells with a connected road on one of their four sides. Row-major, so the order is deterministic.
		void RebuildAnchors()
		{
			foreach (var l in anchors)
				l.Clear();

			foreach (var cell in world.Map.AllCells)
			{
				var zone = zones.GetZone(cell);
				if (zone == ZoneType.None || !catalog.CanGrow(zone) || !IsZoneUnlocked(zone) || !IsFree(cell))
					continue;

				for (var side = 0; side < 4; side++)
				{
					var road = cell + CityUtils.Neighbours4[side];
					if (zones.IsFrontageRoad(road) && roads.IsConnectedToOutside(road))
						anchors[(int)zone].Add(new Anchor { Cell = cell, Side = side });
				}
			}

			dirty = false;
			roadVersion = roads.NetworkVersion;
		}

		/// <summary>Locked zones (progression) keep their paint but never grow.</summary>
		public bool IsZoneUnlocked(ZoneType zone)
		{
			return progression == null || zone == ZoneType.None || progression.IsUnlocked("zone:" + zone);
		}

		/// <summary>In bounds, buildable terrain, owned, empty (AutoClear actors such as trees do not count) and not a road.</summary>
		bool IsFree(CPos cell, List<Actor> autoClear = null)
		{
			if (!world.Map.Contains(cell) || roads.IsRoad(cell) || zones.IsReservedCell(cell))
				return false;

			if (zones.Info.BlockedTerrain.Contains(world.Map.GetTerrainInfo(cell).Type))
				return false;

			if (progression != null && !progression.IsCellOwned(cell))
				return false;

			foreach (var a in world.ActorMap.GetActorsAt(cell))
			{
				var bulldozable = a.Info.TraitInfoOrDefault<BulldozableInfo>();
				if (bulldozable == null || !bulldozable.AutoClear)
					return false;

				autoClear?.Add(a);
			}

			return true;
		}

		bool Usable(CPos cell, ZoneType zone)
		{
			return zones.GetZone(cell) == zone && !reserved.Contains(cell) && IsFree(cell);
		}

		int LandValueAt(CPos cell, ZoneType zone)
		{
			if (landValue != null)
				return landValue.GetLandValue(cell, zone);

			return coverage != null ? coverage.GetLandValue(cell) : 30;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			if (zones != null)
				zones.ZoneChanged -= OnCellChanged;

			world.ActorRemoved -= OnActorRemoved;
			disposed = true;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var parts = new List<string>();
			for (var z = 1; z < spawned.Length; z++)
				if (spawned[z] > 0)
					parts.Add($"{(ZoneType)z}:{spawned[z]}");

			var open = 0;
			var perZone = new List<string>();
			for (var z = 1; z < anchors.Length; z++)
			{
				open += anchors[z].Count;
				if (anchors[z].Count > 0)
					perZone.Add($"{(ZoneType)z}:{anchors[z].Count}");
			}

			return $"growth spawned=[{string.Join(",", parts)}] anchors={open}[{string.Join(",", perZone)}] rejected={rejected}";
		}
	}
}
