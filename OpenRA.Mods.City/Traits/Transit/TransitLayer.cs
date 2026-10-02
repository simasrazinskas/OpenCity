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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Public transport: stops, depots, lines, buses driven as stop-to-stop traffic trips, passenger queues, fares and taxis.",
		"All state is plain data; vehicles are drawn by the traffic WP's renderer (or by TransitRender while no traffic service accepts trips).")]
	public class TransitLayerInfo : TraitInfo
	{
		[Desc("Passengers a bus carries.")]
		public readonly int BusCapacity = 30;

		[Desc("Passengers a taxi carries (a taxi serves one booking at a time).")]
		public readonly int TaxiCapacity = 3;

		[Desc("Cost of placing a stop or stand.")]
		public readonly int StopCost = 150;

		[Desc("Monthly upkeep of a stop in cents.")]
		public readonly int StopUpkeepCents = 300;

		[Desc("Monthly running cost of one active vehicle in cents.")]
		public readonly int VehicleRunningCents = 5000;

		[Desc("Metro: passengers per train, monthly running cost per train (cents), tunnel construction cost per cell and ticks per cell.")]
		public readonly int MetroCapacity = 120;
		public readonly int MetroRunningCents = 14000;
		public readonly int MetroTunnelCostPerCell = 80;
		public readonly int MetroTicksPerCell = 24;

		[Desc("Tram: passengers per tram, monthly running cost (cents), track cost per road cell,",
			"ticks per cell when driven by the virtual timer, and cells per traffic trip chunk.")]
		public readonly int TramCapacity = 90;
		public readonly int TramRunningCents = 7000;
		public readonly int TramTrackCostPerCell = 60;
		public readonly int TramTicksPerCell = 45;
		public readonly int TramChunkCells = 8;

		[Desc("Train: passengers per train, monthly running cost (cents) and ticks per rail cell. Trains are data-only.")]
		public readonly int TrainCapacity = 320;
		public readonly int TrainRunningCents = 20000;
		public readonly int TrainTicksPerCell = 18;

		[Desc("Intercity trains: one arrives at every edge-connected train station this often (ticks) and brings this many visitors (0 disables).")]
		public readonly int IntercityIntervalTicks = 1200;
		public readonly int IntercityPassengers = 24;

		[Desc("Tram and train lines, track and stops need the progression unlock (the tramdepot / trainstation keys).")]
		public readonly bool RequireUnlocks = true;

		public readonly int DefaultTicketCents = 100;
		public readonly int MaxTicketCents = 500;

		[Desc("Dwell at a stop: base + per boarding/alighting passenger, capped.")]
		public readonly int DwellBase = 250;
		public readonly int DwellPerPassenger = 25;
		public readonly int DwellMax = 750;

		[Desc("Ticks a bus needs per road cell when it is driven by the built-in virtual timer (no traffic service).")]
		public readonly int VirtualTicksPerCell = 45;

		[Desc("Ticks a pedestrian needs per cell.")]
		public readonly int WalkTicksPerCell = 288;

		[Desc("Stops within this many cells (Manhattan) of an origin or destination are candidates.")]
		public readonly int WalkRadius = 10;

		[Desc("Transfers between stops further apart than this many cells are not considered.")]
		public readonly int TransferRadius = 4;

		public readonly int TransferPenalty = 60;

		[Desc("Cap on the wait time used by the planner.")]
		public readonly int MaxPlannedWait = 7500;

		[Desc("Passengers give up waiting after this many ticks (15000 = 10 real minutes at 1x).")]
		public readonly int GiveUpTicks = 15000;

		[Desc("Waiting passengers per stop above which new passengers refuse the stop.")]
		public readonly int MaxStopQueue = 60;

		[Desc("Ticks between vehicle spawns of one line.")]
		public readonly int SpawnInterval = 250;

		[Desc("Hard cap on simultaneous transit vehicles.")]
		public readonly int MaxVehicles = 80;

		[Desc("Minimum headway (ticks) that limits the vehicle target: target <= CycleTicks / MinHeadway.")]
		public readonly int MinHeadway = 1500;

		[Desc("Auto fleet: evaluate every this many ticks; +1 above UpUsage percent, -1 below DownUsage.")]
		public readonly int AutoInterval = 600;
		public readonly int AutoUpUsage = 85;
		public readonly int AutoDownUsage = 25;

		[Desc("Aggregate passenger sampler (used until the citizen simulation books journeys): trips per resident per day, base PT share percent.")]
		public readonly int TripsPerResidentPerDay = 3;
		public readonly int ShareBasePercent = 20;

		[Desc("Percent money and comfort weights per age group (Child, Teen, Adult, Senior).")]
		public readonly int[] MoneyWeight = [100, 220, 100, 60];
		public readonly int[] ComfortWeight = [100, 70, 100, 200];

		public readonly int TaxiFareBaseCents = 300;
		public readonly int TaxiFarePerCellCents = 40;

		[Desc("Taxis wanted per stand.")]
		public readonly int TaxisPerStand = 2;

		[Desc("Ticks a taxi waits at a stand for its passenger to arrive.")]
		public readonly int TaxiWaitTicks = 400;

		[Desc("Sprite image of the stop signs (frames 0..3 = bus stop sides N,E,S,W, 4..7 = taxi stand).")]
		public readonly string StopImage = "transit-stop";

		[Desc("Sprite images of the vehicles drawn for virtual and dwelling vehicles.")]
		[FluentReference]
		public readonly string FleetLabel = "label-transit-fleet";

		[FluentReference]
		public readonly string NoRoadLabel = "label-transit-no-road";

		[FluentReference]
		public readonly string WaitingLabel = "label-transit-waiting";

		public readonly string BusImage = "bus";
		public readonly string TaxiImage = "taxi";
		public readonly string TramImage = "tram";
		public readonly string TrainImage = "train";
		public readonly string TrainCarImage = "traincar";

		public override object Create(ActorInitializer init) { return new TransitLayer(init.Self, this); }
	}

	public sealed partial class TransitLayer : ITick, IWorldLoaded, ISync, ICityAutoTestReporter, ITransitPlanner, ITripListener
	{
		public const string FareBusKey = "fares-bus";
		public const string UpkeepKey = "transit-upkeep";
		public const string ConstructionKey = "transit-construction";

		public readonly TransitLayerInfo Info;

		readonly World world;
		IRoadNetwork roads;
		ITrafficService traffic;
		IPropertyRegistry registry;
		IProgression progression;
		CityClock clock;
		CityManager cityManager;
		TransitRouter router;
		TransitRouter previewRouter;
		TransitRouter walkRouter;
		int roadVersion = -1;
		int roadChangedTick;
		bool networkDirty;

		readonly List<TransitStop> stops = [];
		readonly List<TransitLine> lines = [];
		readonly List<TransitDepotRecord> depots = [];
		readonly List<TransitVehicle> vehicles = [];
		int nextStopId = 1, nextLineId = 1, nextVehicleId = 1, nextJourneyId = 1, nextEventSeq = 1;

		// Statistics for the autotest report and the UI.
		int totalAlightings, totalGaveUp;
		bool citizenDriven;

		public TransitLayer(Actor self, TransitLayerInfo info)
		{
			Info = info;
			world = self.World;
		}

		/// <summary>Bumped on every structural change (stops, lines, depots, routes). Cache key for UI and planner.</summary>
		public int Version { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; }

		public IReadOnlyList<TransitStop> Stops => stops;
		public IReadOnlyList<TransitLine> Lines => lines;
		public IReadOnlyList<TransitDepotRecord> Depots => depots;
		public IReadOnlyList<TransitVehicle> Vehicles => vehicles;

		public int TotalBoardings { get; private set; }
		public int TotalFareCents { get; private set; }
		public int TotalJourneys { get; private set; }

		/// <summary>True once a traffic service accepted a trip: vehicles are then drawn by that service, not by TransitRender.</summary>
		public bool TrafficDrivesVehicles { get; private set; }

		int WalkingTicksPerCell => traffic is ITravelTimeCalibration timing ? timing.FreeFlowTicksPerCell(TravelMode.Walk) : Info.WalkTicksPerCell;

		JunctionControl RoadControl(CPos cell)
		{
			var legs = 0;
			for (var d = 0; d < 4; d++)
			{
				var next = cell + CityUtils.Neighbours4[d];
				if (world.Map.Contains(next) && roads.IsRoad(next) && (roads.CanEnter(cell, d) || roads.CanEnter(next, (d + 2) & 3)))
					legs++;
			}

			return legs >= 3 ? roads.GetControl(cell) : JunctionControl.None;
		}

		int RoadDelay(CPos cell)
		{
			var info = (traffic as TrafficSim)?.Info;
			return RoadControl(cell) switch
			{
				JunctionControl.Stop => info?.StopDelayTicks ?? 50,
				JunctionControl.Signal => TravelTimeCalibration.SignalWaitTicks(info?.SignalCycleTicks ?? 1500, info?.SignalClearanceTicks ?? 50),
				_ => 0,
			};
		}

		int RoadRoutingCost(CPos cell)
		{
			var baseline = traffic is ITravelTimeCalibration timing ? timing.FreeFlowTicksPerCell(TravelMode.Car) : 36;
			return TravelTimeCalibration.ScaleSpeed(baseline, roads.GetSpeedPercent(cell)) + RoadDelay(cell);
		}

		int PathTravelTicks(CPos[] path, TransitMode mode)
		{
			if (path.Length <= 1)
				return 1;

			if (DataOnly(mode) || mode == TransitMode.Tram)
				return (path.Length - 1) * TicksPerCell(mode);

			long twiceTotal = 0;
			for (var i = 0; i < path.Length; i++)
			{
				var travel = TravelTimeCalibration.ScaleSpeed(TicksPerCell(mode), roads.GetSpeedPercent(path[i]));
				twiceTotal += (i == 0 || i == path.Length - 1) ? travel : 2L * travel;
				if (i < path.Length - 1)
					twiceTotal += 2L * RoadDelay(path[i]);
			}

			return (int)System.Math.Min(int.MaxValue, (twiceTotal + 1) / 2);
		}

		void IWorldLoaded.WorldLoaded(World w, OpenRA.Graphics.WorldRenderer wr)
		{
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			traffic = w.WorldActor.TraitsImplementing<ITrafficService>().FirstOrDefault();
			citizenDriven = w.WorldActor.TraitsImplementing<ICitizenPopulation>().Any()
				|| w.Players.Any(p => p.Playable && p.PlayerActor.TraitsImplementing<ICitizenPopulation>().Any());
			registry = w.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			clock = w.WorldActor.TraitOrDefault<CityClock>();
			if (roads != null)
			{
				router = new TransitRouter(w.Map, roads, RoadRoutingCost);
				previewRouter = new TransitRouter(w.Map, roads, RoadRoutingCost);
				walkRouter = new TransitRouter(w.Map,
					c => roads.IsRoad(c) && (traffic is TrafficSim sim ? sim.HasSidewalk(c) : roads.GetClass(c) != RoadClass.Highway),
					(c, d) => roads.CanEnter(c, d) || roads.CanEnter(c + CityUtils.Neighbours4[d], (d + 2) & 3));
			}

			InitTracks(w, wr);
			w.AddFrameEndTask(w2 => w2.Add(new TransitRender(this, w)));
		}

		IProgression Progression()
		{
			if (progression != null)
				return progression;

			progression = world.WorldActor.TraitsImplementing<IProgression>().FirstOrDefault();
			if (progression != null)
				return progression;

			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				progression = p.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault();
				if (progression != null)
					break;
			}

			return progression;
		}

		CityManager Funds()
		{
			if (cityManager != null)
				return cityManager;

			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				var cm = p.PlayerActor.TraitOrDefault<CityManager>();
				if (cm != null)
				{
					cityManager = cm;
					break;
				}
			}

			return cityManager;
		}

		public TransitStop GetStop(int id)
		{
			for (var i = 0; i < stops.Count; i++)
				if (stops[i].Id == id)
					return stops[i];

			return null;
		}

		public TransitLine GetLine(int id)
		{
			for (var i = 0; i < lines.Count; i++)
				if (lines[i].Id == id)
					return lines[i];

			return null;
		}

		public TransitVehicle GetVehicle(int id)
		{
			for (var i = 0; i < vehicles.Count; i++)
				if (vehicles[i].Id == id)
					return vehicles[i];

			return null;
		}

		/// <summary>Stop of the given mode on a road cell, or null.</summary>
		public TransitStop StopAt(CPos cell, TransitMode mode)
		{
			for (var i = 0; i < stops.Count; i++)
				if (stops[i].Cell == cell && stops[i].Mode == mode)
					return stops[i];

			return null;
		}

		/// <summary>Any stop on the cell (used by the UI for hit testing).</summary>
		public TransitStop StopAtAny(CPos cell)
		{
			for (var i = 0; i < stops.Count; i++)
				if (stops[i].Cell == cell)
					return stops[i];

			return null;
		}

		/// <summary>Nearest stop of the given mode within `maxDist` cells (Manhattan), or null. For UI hit testing and tool previews.</summary>
		public TransitStop NearestStop(CPos cell, TransitMode mode, int maxDist)
		{
			TransitStop best = null;
			var bestDist = maxDist + 1;
			for (var i = 0; i < stops.Count; i++)
			{
				if (stops[i].Mode != mode)
					continue;

				var d = System.Math.Abs(stops[i].Cell.X - cell.X) + System.Math.Abs(stops[i].Cell.Y - cell.Y);
				if (d < bestDist)
				{
					bestDist = d;
					best = stops[i];
				}
			}

			return best;
		}

		void ITick.Tick(Actor self)
		{
			if (roads == null)
				return;

			var now = world.WorldTick;
			if (roads.NetworkVersion != roadVersion)
			{
				roadVersion = roads.NetworkVersion;
				roadChangedTick = now;
				networkDirty = true;
			}

			if (networkDirty && now - roadChangedTick >= 5)
			{
				networkDirty = false;
				for (var i = 0; i < lines.Count; i++)
					lines[i].NeedsRebuild = true;

				RefreshDepotRoads();
				PurgeTram();
				stationsDirty = true;
			}

			if (stationsDirty)
				RefreshStations();

			CheckRailVersion();
			RebuildOneLine();
			if (uiVersion != Version)
				RefreshUiLines();

			ProcessEvents(now);
			TickVehicles(now);

			if (now % Info.SpawnInterval == 0)
				ManageFleets(now);

			if (now % 25 == 0)
				Pulse(now);

			if (clock != null && clock.IsNewDay)
				MonthlyRollover();
		}

		void Pulse(int now)
		{
			SnapshotStops();
			GiveUpWaiting(now);
			SampleAggregatePassengers(now);
			AutoTuneFleets(now);
			PayAndCollect();
			RefreshUiLines();
			RecordStatistics();
			StateHash = ComputeHash();
		}

		int ComputeHash()
		{
			unchecked
			{
				var h = 17;
				h = h * 31 + stops.Count;
				h = h * 31 + lines.Count;
				h = h * 31 + depots.Count;
				for (var i = 0; i < stops.Count; i++)
					h = h * 31 + stops[i].Id * 7 + stops[i].WaitingCount;

				for (var i = 0; i < lines.Count; i++)
				{
					var l = lines[i];
					h = h * 31 + l.Id * 13 + l.TicketCents + l.TargetVehicles + (l.Broken ? 1 : 0) + l.StopIds.Count + l.CycleTicks;
				}

				for (var i = 0; i < vehicles.Count; i++)
				{
					var v = vehicles[i];
					h = h * 31 + v.Id * 11 + (int)v.State * 3 + v.PaxCount + v.LegIndex + v.Cell.X * 5 + v.Cell.Y;
				}

				h = h * 31 + TotalBoardings + TotalFareCents * 3 + TotalJourneys * 5 + totalGaveUp;
				return h;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			int waiting = 0, virt = 0, onboard = 0, broken = 0, taxis = 0;
			for (var i = 0; i < stops.Count; i++)
				waiting += stops[i].WaitingCount;

			for (var i = 0; i < vehicles.Count; i++)
			{
				if (vehicles[i].Virtual)
					virt++;

				if (vehicles[i].Mode == TransitMode.Taxi)
					taxis++;

				onboard += vehicles[i].PaxCount;
			}

			for (var i = 0; i < lines.Count; i++)
				if (lines[i].Broken)
					broken++;

			return $"transit stops={stops.Count} lines={lines.Count} broken={broken} depots={depots.Count} vehicles={vehicles.Count} (virtual={virt} taxis={taxis}) " +
				$"waiting={waiting} onboard={onboard} boardings={TotalBoardings} alightings={totalAlightings} journeys={TotalJourneys} gaveup={totalGaveUp} " +
				$"fares=${TotalFareCents / 100} hash={StateHash}";
		}

		static int Hash(int a, int b, int c)
		{
			unchecked
			{
				var x = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA6Bu ^ (uint)c * 0xC2B2AE35u;
				x ^= x >> 15;
				x *= 0x2C1B3C6Du;
				x ^= x >> 12;
				x *= 0x297A2D39u;
				x ^= x >> 15;
				return (int)(x & 0x7FFFFFFF);
			}
		}

		public static string ModeName(TransitMode mode)
		{
			return mode switch
			{
				TransitMode.Taxi => "taxi",
				TransitMode.Tram => "tram",
				TransitMode.Metro => "metro",
				TransitMode.Train => "train",
				_ => "bus",
			};
		}

		static string FareKey(TransitMode mode)
		{
			return mode switch
			{
				TransitMode.Taxi => "fares-taxi",
				TransitMode.Tram => "fares-tram",
				TransitMode.Metro => "fares-metro",
				TransitMode.Train => "fares-train",
				_ => FareBusKey,
			};
		}

		public static bool TryParseMode(string s, out TransitMode mode)
		{
			switch ((s ?? "").Trim().ToLowerInvariant())
			{
				case "bus": mode = TransitMode.Bus; return true;
				case "taxi": mode = TransitMode.Taxi; return true;
				case "tram": mode = TransitMode.Tram; return true;
				case "metro": mode = TransitMode.Metro; return true;
				case "train": mode = TransitMode.Train; return true;
				default: mode = TransitMode.Bus; return false;
			}
		}
	}
}
