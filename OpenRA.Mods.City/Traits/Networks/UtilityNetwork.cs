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

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Electricity, water and sewage networks (IUtilityNetwork): LV cable and pipes ride on Roads, HV power lines and extra pipes are",
		"drawn by the player. Every solve builds the connected components of each network, runs a small max-flow over them",
		"(producers, consumers, transformers, map-edge import/export) and writes CityBuilding.HasPower / HasWater.")]
	public class UtilityNetworkInfo : TraitInfo, Requires<RoadLayerInfo>
	{
		[Desc("Ticks between two solves (the city pulse).")]
		public readonly int SolveInterval = 25;

		[Desc("How many cells behind its frontage a building may reach a cable or pipe (Chebyshev distance).")]
		public readonly int AttachRange = 4;

		[Desc("Percent supply at or above which a building counts as supplied (HasPower / HasWater).")]
		public readonly int SuppliedPercent = 50;

		[Desc("Electricity that can be imported through one map-edge connection (a cable or HV cell on the playable-area border).")]
		public readonly int ImportPowerCapacity = 150;

		[Desc("Electricity that can be exported through one map-edge connection.")]
		public readonly int ExportPowerCapacity = 150;

		public readonly int ImportWaterCapacity = 200;
		public readonly int ExportWaterCapacity = 200;

		[Desc("Sewage that can be exported through one map-edge connection (sewage can not be imported).")]
		public readonly int ExportSewageCapacity = 200;

		[Desc("Actors whose power output follows CityClimate.SolarPercent (day, clouds, snow).")]
		public readonly HashSet<string> SolarActors = ["solarplant"];

		[Desc("Actors whose power output follows the wind: 40% when calm up to 100% at wind speed 3.")]
		public readonly HashSet<string> WindActors = ["windturbine"];

		[Desc("Septic tanks: every connected district can drain this much sewage without a plant (0 = sewage plants are mandatory).")]
		public readonly int SepticCapacity = 400;

		[Desc("Most connections per component that count for trade.")]
		public readonly int MaxEdgeConnections = 3;

		[Desc("Monthly price of one imported unit (dollars).")]
		public readonly int ImportPowerPrice = 3;

		[Desc("Monthly income of one exported unit (dollars).")]
		public readonly int ExportPowerPrice = 1;

		public readonly int ImportWaterPrice = 2;
		public readonly int ExportWaterPrice = 1;
		public readonly int ExportSewagePrice = 1;

		[Desc("Construction cost per HV power line cell (water cells cost double).")]
		public readonly int PowerLineCost = 5;

		[Desc("Construction cost per pipe cell and pipe kind.")]
		public readonly int PipeCost = 3;

		[Desc("Monthly upkeep per 10 HV cells.")]
		public readonly int PowerLineUpkeepPer10 = 2;

		[Desc("Monthly upkeep per 10 pipe cells (per kind).")]
		public readonly int PipeUpkeepPer10 = 1;

		[Desc("Share of the build price that is refunded when a line or pipe is removed.")]
		public readonly int RefundPercent = 50;

		[Desc("Sprite image with the line and pipe sequences.")]
		public readonly string Image = "utilnet";

		[Desc("16 frames: HV line with pylons, indexed by the neighbour mask.")]
		public readonly string PowerLineSequence = "powerline";

		[Desc("16 frames: water pipe.")]
		public readonly string WaterPipeSequence = "pipe-water";

		[Desc("16 frames: sewage pipe.")]
		public readonly string SewagePipeSequence = "pipe-sewage";

		[PaletteReference]
		public readonly string Palette = "terrain";

		public override object Create(ActorInitializer init) { return new UtilityNetwork(init.Self, this); }
	}

	public enum PipeKind : byte { Water = 1, Sewage = 2, Both = 3 }

	public sealed partial class UtilityNetwork : IUtilityNetwork, ITick, ICityAutoTestReporter, ISync
	{
		public readonly UtilityNetworkInfo Info;

		readonly World world;
		readonly Map map;
		RoadLayer roadsCache;
		readonly CellLayer<byte> hv;
		readonly CellLayer<byte> pipes;

		RoadLayer Roads => roadsCache ??= world.WorldActor.TraitOrDefault<RoadLayer>();

		public UtilityNetwork(Actor self, UtilityNetworkInfo info)
		{
			Info = info;
			world = self.World;
			map = world.Map;
			hv = new CellLayer<byte>(map);
			pipes = new CellLayer<byte>(map);
			hvLabel = new CellLayer<int>(map);
			lvLabel = new CellLayer<int>(map);
			waterLabel = new CellLayer<int>(map);
			sewerLabel = new CellLayer<int>(map);
		}

		/// <summary>Bumped when a line, pipe or any building-visible result changes (topology and results).</summary>
		public int Version { get; private set; }

		/// <summary>Bumped only when lines or pipes are laid or removed (rendering cache key).</summary>
		public int LayoutVersion { get; private set; }

		public bool HasPowerLine(CPos cell) { return map.Contains(cell) && hv[cell] != 0; }

		/// <summary>Pipe bits laid by the player in this cell (1 water, 2 sewage); road pipes are not included.</summary>
		public int PipeBits(CPos cell) { return map.Contains(cell) ? pipes[cell] : 0; }

		public bool HasWaterPipe(CPos cell) { return map.Contains(cell) && ((pipes[cell] & 1) != 0 || (Roads != null && Roads.CarriesPipes(cell))); }

		public bool HasSewagePipe(CPos cell) { return map.Contains(cell) && ((pipes[cell] & 2) != 0 || (Roads != null && Roads.CarriesPipes(cell))); }

		public bool HasCable(CPos cell) { return Roads != null && Roads.CarriesCable(cell); }

		public int PowerLineCells { get; private set; }
		public int WaterPipeCells { get; private set; }
		public int SewagePipeCells { get; private set; }

		/// <summary>Monthly upkeep of all lines and pipes (0.2 per HV cell, 0.1 per pipe cell by default, rounded per 10 cells).</summary>
		public int MonthlyUpkeep => PowerLineCells / 10 * Info.PowerLineUpkeepPer10 + (WaterPipeCells + SewagePipeCells) / 10 * Info.PipeUpkeepPer10;

		/// <summary>True if the terrain allows an HV line (land or water). Actors are checked by the planner.</summary>
		public bool IsLineTerrain(CPos cell)
		{
			if (!map.Contains(cell))
				return false;

			var type = map.GetTerrainInfo(cell).Type;
			return type == "Water" || Roads == null || Roads.Info.BuildableTerrain.Contains(type);
		}

		public bool IsWaterCell(CPos cell)
		{
			return map.Contains(cell) && map.GetTerrainInfo(cell).Type == "Water";
		}

		/// <summary>Adds an HV line. Call only from order handlers.</summary>
		public bool AddPowerLine(CPos cell)
		{
			if (!map.Contains(cell) || hv[cell] != 0 || !IsLineTerrain(cell))
				return false;

			hv[cell] = 1;
			PowerLineCells++;
			Touch(true);
			return true;
		}

		public bool RemovePowerLine(CPos cell)
		{
			if (!map.Contains(cell) || hv[cell] == 0)
				return false;

			hv[cell] = 0;
			PowerLineCells--;
			Touch(true);
			return true;
		}

		/// <summary>Adds the pipe kinds (bit mask 1 water, 2 sewage) to a land cell. Returns true if anything changed.</summary>
		public bool AddPipe(CPos cell, int kind)
		{
			if (!map.Contains(cell) || IsWaterCell(cell) || Roads == null || !Roads.IsBuildableTerrain(cell))
				return false;

			var add = (byte)(kind & 3 & ~pipes[cell]);
			if (add == 0)
				return false;

			pipes[cell] |= add;
			if ((add & 1) != 0)
				WaterPipeCells++;

			if ((add & 2) != 0)
				SewagePipeCells++;

			Touch(true);
			return true;
		}

		public bool RemovePipe(CPos cell, int kind)
		{
			if (!map.Contains(cell))
				return false;

			var rem = (byte)(kind & 3 & pipes[cell]);
			if (rem == 0)
				return false;

			pipes[cell] &= (byte)~rem;
			if ((rem & 1) != 0)
				WaterPipeCells--;

			if ((rem & 2) != 0)
				SewagePipeCells--;

			Touch(true);
			return true;
		}

		void Touch(bool layout)
		{
			Version++;
			topologyDirty = true;
			if (layout)
				LayoutVersion++;
		}

		// ---- IUtilityNetwork ----
		public int PowerProduced { get; private set; }
		public int PowerConsumed { get; private set; }
		public int WaterProduced { get; private set; }
		public int WaterConsumed { get; private set; }
		public int SewageCapacity { get; private set; }
		public int SewageProduced { get; private set; }

		/// <summary>Electricity wasted: production nobody could use or move (surplus that could not be exported).</summary>
		public int PowerLost { get; private set; }

		/// <summary>Total energy stored in all batteries.</summary>
		public int BatteryCharge { get; private set; }

		public int PowerImported { get; private set; }
		public int PowerExported { get; private set; }
		public int WaterImported { get; private set; }
		public int WaterExported { get; private set; }
		public int SewageExported { get; private set; }

		/// <summary>Number of buildings (consumers) without power / water / sewage right now.</summary>
		public int UnpoweredBuildings { get; private set; }
		public int DryBuildings { get; private set; }
		public int SewageBackedUp { get; private set; }

		public int PowerPercent(Actor building)
		{
			return nodeByActor.TryGetValue(building.ActorID, out var n) ? n.PowerPct : 100;
		}

		public int WaterPercent(Actor building)
		{
			return nodeByActor.TryGetValue(building.ActorID, out var n) ? n.WaterPct : 100;
		}

		public bool HasSewage(Actor building)
		{
			return !nodeByActor.TryGetValue(building.ActorID, out var n) || n.Sewage;
		}

		/// <summary>Stable hash of the solved state for determinism checks.</summary>
		[VerifySync]
		public int StateHash { get; private set; }

		string ICityAutoTestReporter.AutoTestReport()
		{
			return $"utility power={PowerConsumed}/{PowerProduced} lost={PowerLost} imp={PowerImported} exp={PowerExported} " +
				$"water={WaterConsumed}/{WaterProduced} imp={WaterImported} exp={WaterExported} sewage={SewageProduced}/{SewageCapacity} exp={SewageExported} " +
				$"battery={BatteryCharge} dark={UnpoweredBuildings} dry={DryBuildings} backedup={SewageBackedUp} " +
				$"hv={PowerLineCells} pipes={WaterPipeCells}/{SewagePipeCells} " +
				$"comps={lvComps}/{hvComps}/{waterComps}/{sewerComps} v={Version} hash={StateHash}";
		}
	}
}
