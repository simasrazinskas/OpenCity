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
	/// <summary>Simulation state of one service building (provider). Plain data, owned by ServiceSimulation.</summary>
	sealed class ServiceState
	{
		public Property Prop;
		public PropData Need;
		public Actor Actor;
		public CityBuilding Building;
		public ServiceBuilding Svc;
		public ServiceBuildingInfo Info;
		public ServiceUpgradeInfo[] Upgrades = [];
		public int UpgradeMask;
		public CatchmentGroup Group;
		public bool Alive = true;
		public bool JobsWritten;
		public int DistrictMask;
		public int Condition = 100;
		public int PatrolDue;
		public int MaintSince;
		public readonly List<NamedInmate> Named = [];

		// Derived by RefreshProvider (efficiency, upgrades, budget).
		public bool Active;
		public int Efficiency;
		public int Satisfaction = 100;
		public int Fleet;
		public int Capacity;
		public int Throughput;
		public int Range;

		// Dynamic.
		public int FleetInUse;
		public int Stored;         // kg of garbage, bodies, ...
		public int Reserved;       // incoming reserved (kg, beds, slots)
		public int Enrolled;
		public int Inmates;
		public int Load;
		public long ProcessMilli;
		public int RevCents;
		public readonly Queue<int> Discharge = new();
		public readonly List<int> Assigned = [];

		// Totals for reports.
		public int Served;
		public int Processed;
	}

	/// <summary>Per property needs and event state (indexed by Property.Id).</summary>
	sealed class PropData
	{
		public Property Prop;
		public CityBuilding Building;
		public Actor ActorRef;
		public ServiceState Service;
		public bool Alive = true;
		public int LastAccrue;

		public int GarbageKg, GarbageMilli, GarbageReserved;
		public int MailItems, MailMilli, MailReserved;
		public int SickWaiting, SickSince, SickReserved, SickMicro, DeathMicro;
		public int Bodies, BodySince, BodiesReserved;

		public int CrimePressure, FireHazard;

		// Fire: FireDamage > 0 while burning (100 = destroyed).
		public int FireDamage, FireStart, EnginesOnSite, EnginesWanted, EnginesEnRoute;

		// Crime: CrimeEnd > 0 while a crime is in progress.
		public int CrimeAlarm, CrimeEnd, CrimeCar;

		public int UnservedSince = -1;
		public int PatrolRelief;
		public int CrimeCriminal;
		public ICrimeListener CrimeListener;
		public CPos Access;
		public int AccessVersion = -1;

		public readonly byte[] Sat = new byte[ServiceMath.GroupCount];
		public readonly int[] ProviderId = new int[ServiceMath.GroupCount];
		public readonly short[] Dist = new short[ServiceMath.GroupCount];
	}

	struct NamedInmate
	{
		public int Id, Until;
		public ICrimeListener Listener;
	}

	enum VehiclePhase : byte { ToStop, Working, ToDest, ToHome }

	/// <summary>A service vehicle: real (a traffic trip) or virtual (timer), same effects.</summary>
	sealed class ServiceVehicle
	{
		public bool Active;
		public int Slot;
		public int Reserved;
		public ServiceKind Kind;
		public ServiceState Home;
		public ServiceState Dest;
		public bool UsesFleet = true;
		public bool Emergency;

		public readonly int[] Stops = new int[8];
		public int StopCount, StopIdx;
		public readonly int[] Taken = new int[8];

		public int Load, MaxLoad;
		public VehiclePhase Phase;
		public int TripId;
		public int DueTick;
		public CPos Position;
		public int Target;     // property id for respond missions (fire / crime)
		public bool Imported;
		public bool Patrol;
		public bool Maintenance;
		public bool Flying;
		public CPos LegFrom, LegTo;
		public int LegStart;
	}
}
