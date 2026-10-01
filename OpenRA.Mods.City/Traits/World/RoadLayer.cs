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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Stores the road network (one road per cell with a type, direction and control), keeps Map.CustomTerrain in sync,",
		"tracks connectivity to outside connections, implements IRoadNetwork and renders the roads (autotiled by neighbour mask).")]
	public class RoadLayerInfo : TraitInfo, NotBefore<SpawnMapActorsInfo>
	{
		[Desc("Terrain type written to Map.CustomTerrain for road cells. Vehicles' locomotor only allows this type.")]
		public readonly string TerrainType = "Road";

		[Desc("Legacy construction cost per street cell (the RoadType registry has the per-type costs).")]
		public readonly int CostPerCell = 10;

		[Desc("Legacy monthly upkeep per street cell. Use RoadLayer.MonthlyUpkeep for the real per-type sum.")]
		public readonly int UpkeepPerCellPerMonth = 1;

		[Desc("Name of the RoadType used for old-style orders, seeds and AddRoad(cell).")]
		public readonly string DefaultType = "street";

		[Desc("Name of the RoadType laid by OutsideConnection actors (permanent, two-way).")]
		public readonly string OutsideType = "highway";

		[Desc("Refund (percent of the type cost) when a road cell is bulldozed or replaced.")]
		public readonly int RefundPercent = 50;

		[Desc("Terrain types roads can be built on.")]
		public readonly HashSet<string> BuildableTerrain = ["Clear", "Rough"];

		[Desc("Sprite image of the overlay sequences (arrows, control, median).")]
		public readonly string Image = "roadnet";

		[Desc("Overlay sequence with 4 frames (N, E, S, W) drawn on one-way cells.")]
		public readonly string ArrowSequence = "arrows";

		[Desc("Overlay sequence for junction control, ramps and islands (frames: 0 stop, 1 signal, 2 roundabout, 3 ramp, 4 island).")]
		public readonly string ControlSequence = "control";

		[Desc("Overlay sequence with 16 frames indexed by the side-block mask (median strips).")]
		public readonly string MedianSequence = "median";

		[Desc("Overlay sequence for lane paint (bus and bike lanes): 48 frames, (lane combo - 1) * 16 + arm mask.")]
		public readonly string LaneSequence = "lanes";

		[Desc("Overlay sequence for roadside add-ons (trees, barrier, lights, parking): 240 frames, (combo - 1) * 16 + free-side mask.")]
		public readonly string StripSequence = "strips";

		[Desc("Sequence with the bridge deck: 4 frames (N-S two-way, E-W two-way, N-S one-way, E-W one-way).")]
		public readonly string BridgeSequence = "bridge";

		[Desc("Terrain types a bridge may span.")]
		public readonly HashSet<string> BridgeTerrain = ["Water"];

		[Desc("Longest bridge, in water cells.")]
		public readonly int BridgeMaxSpan = 6;

		[Desc("A bridge cell costs this many times the road cost.")]
		public readonly int BridgeCostMultiplier = 3;

		[PaletteReference]
		[Desc("Palette used for road rendering (the art is RGBA, so this only has to exist).")]
		public readonly string Palette = "terrain";

		[Desc("Actor type name of the marker actors that are converted into roads on world load.")]
		public readonly string SeedActor = "roadseed";

		public override object Create(ActorInitializer init) { return new RoadLayer(init.Self, this); }
	}

	public partial class RoadLayer : IWorldLoaded, IRenderOverlay, ITickRender, INotifyActorDisposing, IRoadNetwork, ICityAutoTestReporter, ISync
	{
		// Bits of the per-cell `dir` byte.
		const byte OneWayMask = 7;       // 0 two-way, 1..4 = N/E/S/W
		const byte RampBit = 8;
		const byte LockedBit = 16;
		const byte PairedBit = 32;

		// Bits of the per-cell `bridge` byte.
		const byte BridgeBit = 1;
		const byte BridgeEwBit = 2;

		public readonly RoadLayerInfo Info;

		readonly World world;
		readonly Map map;
		readonly CellLayer<byte> typeId;
		readonly CellLayer<byte> dir;
		readonly CellLayer<byte> sideBlock;
		readonly CellLayer<byte> control;
		readonly CellLayer<byte> addons;
		readonly CellLayer<byte> bridge;
		readonly List<RoadAddonData> addonData = [];
		readonly int[] addonCounts = new int[8];
		readonly CellLayer<byte> previousTerrain;
		readonly CellLayer<bool> connected;
		readonly HashSet<CPos> roadCells = [];
		readonly HashSet<CPos> islands = [];
		readonly List<RoadTypeData> types = [];
		readonly int[] typeCounts;
		readonly byte outsideTypeId;

		// Highway start cells: the neighbour (index into CityUtils.Neighbours4) that lies off the map counts as connected when drawing.
		readonly Dictionary<CPos, int> edgeConnections = [];
		readonly HashSet<CPos> dirtyCells = [];
		readonly byte roadTerrainIndex = byte.MaxValue;

		bool hasOutsideConnections;
		int connectivityVersion = -1;
		readonly Queue<CPos> floodQueue = new();
		bool disposed;

		public RoadLayer(Actor self, RoadLayerInfo info)
		{
			Info = info;
			world = self.World;
			map = world.Map;
			typeId = new CellLayer<byte>(map);
			dir = new CellLayer<byte>(map);
			sideBlock = new CellLayer<byte>(map);
			control = new CellLayer<byte>(map);
			addons = new CellLayer<byte>(map);
			bridge = new CellLayer<byte>(map);
			foreach (var ai in self.Info.TraitInfos<RoadAddonInfo>())
			{
				var flag = RoadAddonData.FlagOf(ai.Name);
				if (flag != RoadAddons.None)
					addonData.Add(new RoadAddonData(flag, ai));
			}

			previousTerrain = new CellLayer<byte>(map);
			connected = new CellLayer<bool>(map);

			// Road registry: yaml order, id = index + 1.
			foreach (var ti in self.Info.TraitInfos<RoadTypeInfo>())
				types.Add(new RoadTypeData((byte)(types.Count + 1), ti));

			if (types.Count == 0)
				types.Add(new RoadTypeData(1, new RoadTypeInfo()));

			typeCounts = new int[types.Count + 1];
			DefaultTypeId = FindTypeId(info.DefaultType, 1);
			outsideTypeId = FindTypeId(info.OutsideType, DefaultTypeId);

			var terrainInfo = map.Rules.TerrainInfo;
			try
			{
				roadTerrainIndex = terrainInfo.GetTerrainIndex(info.TerrainType);
			}
			catch (InvalidOperationException)
			{
				Log.Write("debug", $"RoadLayer: tileset has no terrain type '{info.TerrainType}'; roads will not affect pathfinding.");
			}
		}

		byte FindTypeId(string name, byte fallback)
		{
			foreach (var t in types)
				if (t.Name == name)
					return t.Id;

			return fallback;
		}

		/// <summary>Fired (synchronously, inside order resolution) after a cell gains, loses or changes a road.</summary>
		public event Action<CPos> RoadChanged;

		/// <summary>Incremented whenever the road network, a type, a direction or a control changes. Use it to invalidate caches.</summary>
		public int NetworkVersion { get; private set; }

		public int RoadCellCount => roadCells.Count;

		public IEnumerable<CPos> RoadCells => roadCells;

		/// <summary>The registry in id order (id = index + 1). UI uses it for the road palette.</summary>
		public IReadOnlyList<RoadTypeData> Types => types;

		public byte DefaultTypeId { get; }

		public RoadTypeData GetTypeData(byte id) { return id >= 1 && id <= types.Count ? types[id - 1] : null; }

		public RoadTypeData GetTypeData(string name)
		{
			foreach (var t in types)
				if (t.Name == name)
					return t;

			return null;
		}

		/// <summary>Type of the road in a cell, or null.</summary>
		public RoadTypeData GetRoadType(CPos cell)
		{
			return map.Contains(cell) ? GetTypeData(typeId[cell]) : null;
		}

		public byte GetTypeId(CPos cell) { return map.Contains(cell) ? typeId[cell] : (byte)0; }

		/// <summary>Direction index into CityUtils.Neighbours4 of a one-way cell, or -1 for two-way and non-road cells.</summary>
		public int GetOneWay(CPos cell)
		{
			return IsRoad(cell) ? (dir[cell] & OneWayMask) - 1 : -1;
		}

		public bool IsRamp(CPos cell) { return IsRoad(cell) && (dir[cell] & RampBit) != 0; }

		public bool IsPaired(CPos cell) { return IsRoad(cell) && (dir[cell] & PairedBit) != 0; }

		/// <summary>Bit i set = the cell does not connect towards Neighbours4[i] (median, barrier).</summary>
		public int GetSideBlock(CPos cell) { return IsRoad(cell) ? sideBlock[cell] : 0; }

		/// <summary>Explicit junction control set by the player, or null for "default".</summary>
		public JunctionControl? GetExplicitControl(CPos cell)
		{
			if (!IsRoad(cell) || control[cell] == 0)
				return null;

			return (JunctionControl)(control[cell] - 1);
		}

		/// <summary>True for the centre cell of a roundabout (not a road, blocks building and roads).</summary>
		public bool IsIsland(CPos cell) { return islands.Contains(cell); }

		/// <summary>Sum of the monthly upkeep of every road cell, by type.</summary>
		public int MonthlyUpkeep
		{
			get
			{
				var sum = 0;
				for (var i = 0; i < types.Count; i++)
					sum += typeCounts[i + 1] * types[i].Info.Upkeep;

				foreach (var a in addonData)
					sum += addonCounts[BitIndex(a.Flag)] * a.Info.UpkeepPer10 / 10;

				return sum;
			}
		}

		public int CountOfType(byte id) { return id < typeCounts.Length ? typeCounts[id] : 0; }

		public bool IsRoad(CPos cell)
		{
			return map.Contains(cell) && typeId[cell] != 0;
		}

		/// <summary>True for road cells laid by an OutsideConnection. They cannot be removed.</summary>
		public bool IsHighway(CPos cell)
		{
			return map.Contains(cell) && typeId[cell] != 0 && (dir[cell] & LockedBit) != 0;
		}

		/// <summary>True if the cell is a road of the highway class (permanent or built by the player).</summary>
		public bool IsHighwayClass(CPos cell)
		{
			var t = GetRoadType(cell);
			return t != null && t.IsHighway;
		}

		/// <summary>True if the terrain of this cell allows building a road (ignores actors and existing roads).</summary>
		public bool IsBuildableTerrain(CPos cell)
		{
			if (!map.Contains(cell))
				return false;

			return Info.BuildableTerrain.Contains(map.GetTerrainInfo(cell).Type);
		}

		/// <summary>
		/// True if a road could be placed here right now: in bounds, no road yet, buildable terrain and no blocking building.
		/// Auto-clearable actors (trees) do not block.
		/// </summary>
		public bool CanBuildRoadAt(CPos cell)
		{
			if (!map.Contains(cell) || IsRoad(cell) || islands.Contains(cell))
				return false;

			if (!IsBuildableTerrain(cell))
				return false;

			return !ConstructionUtils.HasBlockingActor(world, cell);
		}

		public IEnumerable<CPos> AdjacentRoadCells(CPos cell)
		{
			foreach (var d in CityUtils.Neighbours4)
			{
				var n = cell + d;
				if (IsRoad(n))
					yield return n;
			}
		}

		public bool IsAdjacentToRoad(CPos cell)
		{
			foreach (var d in CityUtils.Neighbours4)
				if (IsRoad(cell + d))
					return true;

			return false;
		}

		/// <summary>
		/// Adds a street (no cost handling, no actor checks: callers clear or check actors themselves).
		/// Returns false if the cell is out of bounds, already a road or has unbuildable terrain.
		/// Call only from order handlers / world load.
		/// </summary>
		public bool AddRoad(CPos cell)
		{
			return AddRoadInner(cell, DefaultTypeId, -1, false, false, true);
		}

		/// <summary>Adds a road of a given type (one-way direction index 0..3 or -1). Same rules as <see cref="AddRoad(CPos)"/>.</summary>
		public bool AddRoad(CPos cell, byte type, int oneWay = -1, bool paired = false)
		{
			return AddRoadInner(cell, type, oneWay, paired, false, true);
		}

		/// <summary>
		/// Changes type, direction and paired flag of an existing road in place (keeps control and side blocks).
		/// Returns false if there is no road, it is permanent, or nothing changes.
		/// </summary>
		public bool ReplaceRoad(CPos cell, byte type, int oneWay, bool paired)
		{
			if (!IsRoad(cell) || IsHighway(cell) || GetTypeData(type) == null)
				return false;

			var data = GetTypeData(type);
			if (IsBridge(cell) && !data.Info.AllowBridge)
				return false;

			if (!data.Info.AllowOneWay)
				oneWay = -1;

			var newDir = (byte)(((oneWay + 1) & OneWayMask) | (dir[cell] & (RampBit | LockedBit)) | (paired ? PairedBit : 0));
			if (typeId[cell] == type && dir[cell] == newDir)
				return false;

			typeCounts[typeId[cell]]--;
			typeCounts[type]++;
			typeId[cell] = type;
			dir[cell] = newDir;
			var keep = (byte)(addons[cell] & (byte)AllowedAddons(cell));
			if (keep != addons[cell])
			{
				CountAddons(addons[cell], -1);
				addons[cell] = keep;
				CountAddons(keep, 1);
			}

			Changed(cell);
			return true;
		}

		/// <summary>Removes a road. Returns false if there was none or it is a permanent highway cell.</summary>
		public bool RemoveRoad(CPos cell)
		{
			if (!IsRoad(cell) || IsHighway(cell))
				return false;

			typeCounts[typeId[cell]]--;
			CountAddons(addons[cell], -1);
			typeId[cell] = 0;
			dir[cell] = 0;
			sideBlock[cell] = 0;
			control[cell] = 0;
			addons[cell] = 0;
			bridge[cell] = 0;
			roadCells.Remove(cell);
			if (roadTerrainIndex != byte.MaxValue)
				map.CustomTerrain[cell] = previousTerrain[cell];

			Changed(cell);

			// A removed carriageway frees the side blocks of its partner.
			foreach (var d in CityUtils.Neighbours4)
			{
				var n = cell + d;
				if (IsRoad(n) && sideBlock[n] != 0)
				{
					var i = Array.IndexOf(CityUtils.Neighbours4, new CVec(-d.X, -d.Y));
					if ((sideBlock[n] & (1 << i)) != 0)
					{
						sideBlock[n] &= (byte)~(1 << i);
						Changed(n);
					}
				}
			}

			return true;
		}

		public void SetSideBlock(CPos cell, int mask)
		{
			if (!IsRoad(cell) || sideBlock[cell] == (byte)(mask & 15))
				return;

			sideBlock[cell] = (byte)(mask & 15);
			Changed(cell);
		}

		public void SetRamp(CPos cell, bool ramp)
		{
			if (!IsRoad(cell))
				return;

			var isRamp = (dir[cell] & RampBit) != 0;
			if (isRamp == ramp)
				return;

			dir[cell] = (byte)(ramp ? dir[cell] | RampBit : dir[cell] & ~RampBit);
			Changed(cell);
		}

		/// <summary>Sets the explicit junction control of a cell (null = back to the default).</summary>
		public bool SetControl(CPos cell, JunctionControl? value)
		{
			if (!IsRoad(cell))
				return false;

			var b = value == null ? (byte)0 : (byte)((int)value.Value + 1);
			if (control[cell] == b)
				return false;

			control[cell] = b;
			Changed(cell);
			return true;
		}

		public void SetIsland(CPos cell, bool on)
		{
			if (on ? islands.Add(cell) : islands.Remove(cell))
				Changed(cell);
		}

		bool AddRoadInner(CPos cell, byte type, int oneWay, bool paired, bool locked, bool checkTerrain, int bridgeAxis = -1)
		{
			if (!map.Contains(cell) || GetTypeData(type) == null)
				return false;

			if (typeId[cell] != 0)
			{
				// Upgrading an existing road to a permanent highway cell is allowed.
				if (locked && (dir[cell] & LockedBit) == 0)
				{
					typeCounts[typeId[cell]]--;
					typeCounts[type]++;
					typeId[cell] = type;
					dir[cell] = (byte)((dir[cell] & ~OneWayMask) | LockedBit);
					Changed(cell);
					return true;
				}

				return false;
			}

			if (bridgeAxis >= 0)
			{
				if (!IsBridgeTerrain(cell) || !GetTypeData(type).Info.AllowBridge)
					return false;
			}
			else if (checkTerrain && !IsBuildableTerrain(cell))
				return false;

			if (!GetTypeData(type).Info.AllowOneWay)
				oneWay = -1;

			typeId[cell] = type;
			dir[cell] = (byte)(((oneWay + 1) & OneWayMask) | (locked ? LockedBit : 0) | (paired ? PairedBit : 0));
			sideBlock[cell] = 0;
			control[cell] = 0;
			addons[cell] = 0;
			bridge[cell] = bridgeAxis < 0 ? (byte)0 : (byte)(BridgeBit | (bridgeAxis == 1 ? BridgeEwBit : 0));
			typeCounts[type]++;
			islands.Remove(cell);
			roadCells.Add(cell);
			if (roadTerrainIndex != byte.MaxValue)
			{
				previousTerrain[cell] = map.CustomTerrain[cell];
				map.CustomTerrain[cell] = roadTerrainIndex;
			}

			Changed(cell);
			return true;
		}

		void Changed(CPos cell)
		{
			NetworkVersion++;
			MarkDirty(cell);
			foreach (var d in CityUtils.Neighbours4)
				MarkDirty(cell + d);

			RoadChanged?.Invoke(cell);
		}

		void MarkDirty(CPos cell)
		{
			if (map.Contains(cell))
				dirtyCells.Add(cell);
		}
	}
}
