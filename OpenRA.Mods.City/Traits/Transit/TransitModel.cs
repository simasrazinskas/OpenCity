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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Transit modes. Bus and Tram drive on roads as traffic trips; Taxi uses stands and a dispatcher; Metro and Train are data-only.</summary>
	public enum TransitMode : byte { Bus = 0, Taxi, Tram, Metro, Train }

	public enum TransitVehicleState : byte
	{
		/// <summary>Driving a leg (a traffic trip or a virtual timer).</summary>
		Leg = 0,

		/// <summary>Standing at a stop: alighting, boarding, dwelling.</summary>
		Dwell,

		/// <summary>Driving back to the depot (line deleted, too many vehicles, depot lost).</summary>
		ToDepot,

		/// <summary>Taxi parked at a stand, free.</summary>
		Idle,

		/// <summary>Taxi driving to a pickup stand.</summary>
		ToPickup,

		/// <summary>Taxi parked at the stand waiting for its passenger to walk up.</summary>
		WaitingPassenger,

		/// <summary>Taxi driving a passenger.</summary>
		Carrying,

		/// <summary>Removed; kept until the end of the tick so listeners can still resolve ids.</summary>
		Gone
	}

	/// <summary>A group of passengers (1 for a citizen, more for aggregate sampling) travelling together.</summary>
	public struct PaxGroup
	{
		/// <summary>Journey id handed out by StartJourney (0 = aggregate sample).</summary>
		public int JourneyId;
		public ITripListener Listener;
		public int OwnerId;
		public int Count;

		/// <summary>Line to ride now.</summary>
		public int LineId;
		public int AlightStopId;

		/// <summary>Maximum number of legs the planned ride takes; a vehicle heading the other way round a ping-pong line is rejected.</summary>
		public int RideLegs;

		/// <summary>Optional second ride (a transfer): board Line2 at Board2Stop, alight at Alight2Stop.</summary>
		public int Line2;
		public int Board2Stop;
		public int Alight2Stop;
		public int RideLegs2;
		public int TransferWalkTicks;

		/// <summary>Ticks to walk from the last stop to the destination.</summary>
		public int WalkOutTicks;

		/// <summary>Tick at which the passenger started waiting at the current stop.</summary>
		public int WaitStart;

		/// <summary>Tick the whole journey was booked (for TripResult).</summary>
		public int StartTick;

		/// <summary>Fares paid so far (cents), reported in TripResult.</summary>
		public int FarePaid;
	}

	public sealed class TransitStop
	{
		public int Id;
		public TransitMode Mode;
		public CPos Cell;

		/// <summary>Side of the road (index into CityUtils.Neighbours4) the sign is drawn on.</summary>
		public int Side;

		/// <summary>Actor id of the station building that owns this stop (metro), 0 for road stops.</summary>
		public uint StationActorId;

		public readonly List<PaxGroup> Waiting = [];
		public int WaitingCount;

		/// <summary>Lines serving this stop (ids ascending). Rebuilt when lines change.</summary>
		public readonly List<int> LineIds = [];

		/// <summary>Other line stops within transfer walking distance (ids ascending).</summary>
		public readonly List<int> NearIds = [];

		/// <summary>Snapshot of WaitingCount refreshed every pulse (planner comfort input, so quotes do not depend on tick order).</summary>
		public int CrowdSnapshot;

		/// <summary>Residents living within the walking radius (aggregate sampler input).</summary>
		public int Catchment;

		public int SampleAccumMilli;

		/// <summary>Train stations: tick of the next intercity arrival (0 = not scheduled).</summary>
		public int NextIntercityTick;

		public int BoardedThisMonth, AlightedThisMonth, GaveUpThisMonth;
		public int BoardedLastMonth, AlightedLastMonth, GaveUpLastMonth;
	}

	public struct LineStats
	{
		public int Passengers;
		public int FareCents;
		public int CostCents;
		public int GaveUp;
		public long WaitTicks;
		public int Boardings;

		/// <summary>Sum of load percent samples (each time a vehicle leaves a stop) and their number.</summary>
		public int LoadSum, LoadSamples;

		public readonly int UsagePercent => LoadSamples == 0 ? 0 : LoadSum / LoadSamples;
		public readonly int AverageWaitTicks => Boardings == 0 ? 0 : (int)(WaitTicks / Boardings);
	}

	/// <summary>One stop-to-stop part of a line's route.</summary>
	public sealed class TransitLeg
	{
		public int FromStopId, ToStopId;

		/// <summary>Road cells from the first stop to the next, inclusive; empty when unreachable.</summary>
		public CPos[] Cells = [];

		/// <summary>Estimated ticks to drive the leg (no dwell).</summary>
		public int Ticks;
		public bool Reachable;
	}

	public sealed class TransitLine
	{
		public int Id;
		public TransitMode Mode;
		public int Color;
		public string Name;
		public bool Loop;

		/// <summary>Ordered, unique stop ids. A loop line returns from the last stop to the first; others run back and forth.</summary>
		public readonly List<int> StopIds = [];

		public int TicketCents;
		public int TargetVehicles = 1;
		public bool Auto;

		public readonly List<TransitLeg> Legs = [];
		public bool Broken;
		public bool NeedsRebuild = true;

		/// <summary>Ticks of one full cycle (all legs plus dwell estimates).</summary>
		public int CycleTicks;

		/// <summary>Smoothed observed wait at the stops of this line (ticks).</summary>
		public int ObservedWait;

		/// <summary>Per stop index pair: legs and ride ticks of the quickest ride (-1 when impossible). Row-major n x n.</summary>
		public int[] RideLegs = [];
		public int[] RideTicks = [];

		public readonly List<TransitVehicle> Vehicles = [];

		public LineStats ThisMonth, LastMonth;

		public int LastAutoTick;
		public int LastSpawnTick = -100000;
		public int AutoLoadSum, AutoLoadSamples;

		public int LegCount => Legs.Count;

		public int StopCount => StopIds.Count;
	}

	public sealed class TransitVehicle
	{
		public int Id;
		public TransitMode Mode;
		public int LineId;

		/// <summary>Actor id of the depot this vehicle belongs to.</summary>
		public uint DepotId;

		public TransitVehicleState State;

		/// <summary>Index of the leg just completed (the stop the vehicle is at); LegCount - 1 right after leaving the depot.</summary>
		public int Pos;

		/// <summary>Index of the leg being driven; -1 on the depot run.</summary>
		public int LegIndex = -1;

		public int TripId;
		public bool Virtual;
		public int LegStartTick;
		public int LegTicks;
		public CPos[] LegCells = [];
		public CPos Cell;
		public int DwellUntil;
		public int FailCount;
		public bool Recall;

		/// <summary>Index into LegCells where the current traffic trip chunk starts / ends (trams are sent to the traffic sim in chunks so they follow their track). -1 = whole leg.</summary>
		public int ChunkStart, ChunkEnd = -1;

		/// <summary>An intercity train: arrives from the map edge, stays at the station, leaves again.</summary>
		public bool Intercity;
		public int PaxCount;
		public readonly List<PaxGroup> Pax = [];

		/// <summary>Stop the vehicle currently stands at / drives to (taxis: the stand).</summary>
		public int StopId;

		/// <summary>Last departure tick of this vehicle from a stop (bunching hold).</summary>
		public int LastDeparture;

		// ---- taxi booking ----
		public PaxGroup Fare;
		public bool HasFare;
		public CPos DestRoad;
		public int PassengerReadyTick;
	}

	public sealed class TransitDepotRecord
	{
		public uint ActorId;
		public Actor Actor;
		public TransitMode Mode;
		public int Capacity;
		public CPos Road;
		public int Used;
	}

	/// <summary>Scheduled passenger event (walk to stop finished, walk to destination finished).</summary>
	public struct TransitEvent
	{
		public int Tick;
		public int Seq;
		public byte Kind;
		public int StopId;
		public PaxGroup Group;
	}
}
