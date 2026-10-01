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
using System.Diagnostics;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Mesoscopic traffic simulation: vehicles are plain data moving along link queues (cell, heading) with speed-density",
		"travel times, spillback, junction control and A* routing. Replaces the actor-based TrafficManager. Vehicles are drawn by this",
		"trait (no actors). Implements ITrafficService.")]
	public class TrafficSimInfo : TraitInfo
	{
		[Desc("Vehicle slots a link holds per lane (a car takes 1, trucks and buses 2).")]
		public readonly int SlotsPerLane = 2;

		[Desc("Ticks a car needs to cross one street cell at free flow, in thousandths. 3300 gives ~1 game hour (100 ticks) for 30 cells.")]
		public readonly int FreeFlowMilliTicksPerCell = 3300;

		[Desc("Minimum ticks between two vehicles leaving the same lane of a link, in thousandths.")]
		public readonly int DischargeMilliTicks = 1500;

		[Desc("Planned trips (A* runs) per tick.")]
		public readonly int PlanBudget = 32;

		[Desc("Ticks between congestion snapshots used by routing.")]
		public readonly int CostRefreshTicks = 50;

		[Desc("Ticks a front vehicle may be blocked before it is allowed to overfill the next link (gridlock breaker).")]
		public readonly int GridlockTicks = 200;

		[Desc("Ticks a front vehicle may be blocked before the trip fails with Stuck and the vehicle is removed.")]
		public readonly int StuckTicks = 900;

		[Desc("Fixed signal cycle in ticks (two green phases, each followed by an all-red).")]
		public readonly int SignalCycleTicks = 24;

		[Desc("Ticks a car stays parked after arriving.")]
		public readonly int ParkDwellTicks = 400;

		[Desc("Search radius (cells) for a free parking space near the destination.")]
		public readonly int ParkSearchRadius = 3;

		[Desc("Ticks to walk one cell for walk-only trips.")]
		public readonly int WalkTicksPerCell = 20;

		[Desc("Hard cap on simultaneous vehicles.")]
		public readonly int MaxVehicles = 6000;

		[Desc("Aggregate trip source: population per in-flight vehicle (used while no citizen simulation requests trips).")]
		public readonly int PopulationPerVehicle = 10;

		[Desc("Aggregate trip source: vehicle target without a CityManager (shellmap, tests).")]
		public readonly int FallbackVehicles = 20;

		[Desc("Aggregate trip source: maximum trips created per tick.")]
		public readonly int MaxSpawnPerTick = 12;

		[Desc("Aggregate trip source: percentage of trips from industry made by trucks.")]
		public readonly int TruckChance = 55;

		[Desc("Aggregate trip source: percentage of regular trips made by buses.")]
		public readonly int BusChance = 4;

		[Desc("Sprite image names of passenger cars (also the sprite fallback for unknown vehicle types).")]
		public readonly string[] Cars = ["car-a", "car-b", "car-c", "car-d"];

		[Desc("Vehicle types as 'type:sprite image', requested through TripRequest.VehicleType. Unlisted types are drawn as cars.",
			"Trucks and buses take 2 slots and drive at 80% speed; emergency vehicles drive at 150% and ignore junction control.")]
		public readonly string[] Trucks = ["truck:truck", "freight:truck", "semi:truck", "garbage:garbage", "garbage-truck:garbage", "van:van", "maintenance:van"];

		public readonly string[] Buses = ["bus:bus", "metro:bus"];

		[Desc("Trams use rails (ITrafficRoadFlags.HasTramTrack) without taking road capacity. Without rails they behave like buses.")]
		public readonly string[] Trams = ["tram:tram"];

		public readonly string[] Emergency = ["ambulance:ambulance", "firetruck:firetruck", "fire-truck:firetruck", "policecar:policecar", "police:policecar"];

		[Desc("Ordinary-sized vehicles with their own sprite.")]
		public readonly string[] Others = ["taxi:taxi", "hearse:hearse"];

		[Desc("Accidents: one per this many link traversals (0 = off). Scaled by CityClimate.AccidentRiskPercent and the AccidentPct policy.")]
		public readonly int AccidentOdds = 40000;

		[Desc("Ticks after which a wreck is removed even if no responder came.")]
		public readonly int AccidentTimeoutTicks = 900;

		[Desc("Ticks a responder needs to clear the wreck once it arrived.")]
		public readonly int AccidentClearTicks = 60;

		[Desc("Vehicle type of the responder dispatched from the nearest police building.")]
		public readonly string AccidentResponder = "policecar";

		[Desc("Signals: a green phase may be extended by up to this many ticks while its traffic keeps flowing (0 = off).")]
		public readonly int SignalExtendTicks = 6;

		[Desc("Route trees: build a reverse-Dijkstra tree for a destination after this many route requests in a window (0 = off).")]
		public readonly int RouteTreeMinHits = 40;

		[Desc("Route trees: maximum cached trees and how long they stay valid (ticks).")]
		public readonly int RouteTreeCapacity = 8;
		public readonly int RouteTreeTtl = 200;

		[Desc("Trips of at most this many cells are walked when walking is an allowed mode (0 = off).")]
		public readonly int WalkMaxCells = 4;

		[Desc("Cosmetic pedestrians: maximum number of walking trips that get a visible walker.")]
		public readonly int PedestrianCap = 200;

		[Desc("Sprite image of pedestrians and parked cars are taken from Cars; pedestrians use this image (8 frames: 4 colours x 2 steps).")]
		public readonly string PedestrianImage = "pedestrian";

		[PaletteReference]
		public readonly string Palette = "city";

		[Desc("Sideways shift of vehicles from the road centre (right-hand traffic) in 1/1024 cell.")]
		public readonly int LaneOffset = 190;

		[Desc("Draw scale of vehicle sprites.")]
		public readonly float SpriteScale = 0.85f;

		public override object Create(ActorInitializer init) { return new TrafficSim(init.Self, this); }
	}

	public sealed partial class TrafficSim : ITick, IWorldLoaded, ITrafficService, ICityAutoTestReporter, IFreightFlow, IVehicleInspector,
		ITrafficIncidents, ITrafficInfo, ISync, IRender
	{
		// Time unit: 1/U tick. Travel times, ready times and release times are all in this unit (integer, synced).
		const int U = 64;

		// Heading constants follow CityUtils.Neighbours4: 0 N, 1 E, 2 S, 3 W.
		const int NoVehicle = -1;

		public readonly TrafficSimInfo Info;
		readonly World world;
		readonly Map map;
		readonly int width, height, cellCount, linkCount;

		IRoadNetwork net;
		CityClock clock;
		CityManager cityManager;
		bool cityManagerSearched;
		int tick;
		int nowU;

		[VerifySync]
		int totalTrips;

		int totalArrived, totalFailed;

		int rng = 12345;

		// Callbacks collected during the tick and delivered at its end, so listeners may request trips from them.
		struct Notification
		{
			public TripRec Trip;
			public bool Arrived;
			public TripFailure Failure;
			public int ArriveTick;
		}

		readonly List<Notification> notifications = [];

		readonly Stopwatch watch = new();
		long watchTicks;
		int watchSamples;
		long worstTicks;

		public TrafficSim(Actor self, TrafficSimInfo info)
		{
			Info = info;
			world = self.World;
			map = world.Map;
			width = map.MapSize.Width;
			height = map.MapSize.Height;
			cellCount = width * height;
			linkCount = cellCount * 4;
			InitGraph();
			InitLinks();
			InitVehicles();
			InitTypes();
		}

		[VerifySync]
		public int ActiveVehicles { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; }

		int Cell(CPos c) { return c.Y * width + c.X; }

		CPos ToCPos(int cell) { return new CPos(cell % width, cell / width); }

		bool InMap(CPos c) { return c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height && map.Contains(c); }

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			net = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			clock = w.WorldActor.TraitOrDefault<CityClock>();
			LoadRenderAssets(wr);
		}

		// Cheap deterministic pseudo random numbers (not SharedRandom: the number of calls varies with traffic).
		int NextRandom(int max)
		{
			rng = unchecked(rng * 1664525 + 1013904223);
			return (int)(((uint)rng >> 8) % (uint)Math.Max(1, max));
		}

		static int Hash(int a, int b)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 40503u;
				h ^= h >> 15;
				h *= 2246822519u;
				h ^= h >> 13;
				return (int)h;
			}
		}

		CityManager FindCity()
		{
			if (!cityManagerSearched)
			{
				cityManagerSearched = true;

				// Every player actor has a CityManager; only playable players run a city (Neutral owns the shellmap town).
				foreach (var p in world.Players)
				{
					var cm = p.Playable ? p.PlayerActor.TraitOrDefault<CityManager>() : null;
					if (cm != null)
					{
						cityManager = cm;
						break;
					}
				}
			}

			return cityManager;
		}

		void ITick.Tick(Actor self)
		{
			if (net == null)
				return;

			watch.Restart();

			tick = world.WorldTick;
			nowU = tick * U;
			tickStartRunTime = Game.RunTime;

			EnsureGraph();
			CheckPolicies();
			CheckRoadFlags();
			if (tick % Math.Max(1, Info.CostRefreshTicks) == 0)
				RefreshCostSnapshot();

			TickSource();
			PlanAndSpawn();
			TickWalkers();
			TickParking();
			TickFlow();
			TickIncidents();
			DeliverNotifications();

			if (tick % 25 == 0)
				UpdateStats();

			watch.Stop();
			watchTicks += watch.ElapsedTicks;
			worstTicks = Math.Max(worstTicks, watch.ElapsedTicks);
			watchSamples++;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			// Timings are machine-dependent, so they go to separate [autotest-perf] lines (see LogPerf).
			return $"traffic vehicles={ActiveVehicles} pending={PendingCount} trips={totalTrips} arrived={totalArrived} " +
				$"failed={totalFailed}(noroute={failBy[0]},stuck={failBy[2]},closed={failBy[4]}) flow={CityTrafficFlow} " +
				$"blocked={blockedFronts} gridlock={gridlockReleases} parked={parkedNow} parkSearch={parkSearches} " +
				$"parkOverflow={parkOverflow} incidents={incidents.Count}/{totalAccidents} trees={treeRoutes}/{treeBuilds} rebuilds={graphRebuilds} " +
				$"peds={pedestrians} aggregate={(aggregateActive ? 1 : 0)} citizenTrips={externalCitizenTrips} " +
				$"ticksPer100Cells={(tripCellSum > 0 ? tripTickSum * 100 / tripCellSum : 0)} hash={StateHash}";
		}
	}
}
