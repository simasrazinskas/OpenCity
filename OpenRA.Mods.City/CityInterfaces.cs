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

// Cross-domain contracts for the CS2 simulation work (see mods/city/ARCHITECTURE.md).
// Every work package implements its interface(s) in its own trait and finds the others with
//   world.WorldActor.TraitsImplementing<IFoo>().FirstOrDefault()   (world traits)
//   player.PlayerActor.TraitsImplementing<IFoo>().FirstOrDefault() (player traits)
// so nobody needs another package's concrete class to compile. A missing provider (null) must
// always be tolerated: fall back to the legacy aggregate behaviour.
//
// This file is lead-owned and append-only: request additions in your final report.
// Determinism: every method that mutates state may only be called from synced code (ITick,
// IResolveOrder, INotify* in the sim). Read-only getters may be called from UI code.
namespace OpenRA.Mods.City.Traits
{
	/// <summary>Education level of a citizen and of a job slot.</summary>
	public enum EducationLevel : byte { Uneducated = 0, Poorly = 1, Educated = 2, Well = 3, Highly = 4 }

	public enum AgeGroup : byte { Child = 0, Teen = 1, Adult = 2, Senior = 3 }

	/// <summary>What a property (building lot) is used for.</summary>
	public enum PropertyKind : byte { None = 0, Residential, Commercial, Industrial, Office, Warehouse, Extractor, Service, Transit, Signature }

	public enum TripPurpose : byte
	{
		None = 0, Work, School, Shopping, Leisure, Health, GoingHome, Commute, Tourism,
		Freight, Service, Emergency, Transit, Through
	}

	public enum TravelMode : byte { Walk = 0, Car, Transit, Taxi, Bike, Truck, ServiceVehicle, EmergencyVehicle, Bus }

	[Flags]
	public enum TravelModes : byte
	{
		None = 0, Walk = 1, Car = 2, Transit = 4, Taxi = 8, Bike = 16
	}

	public enum TripFailure : byte { NoRoute, NoParking, Stuck, OverRange, RoadClosed, Cancelled }

	/// <summary>Request to move something from A to B. Coordinates are road cells (building access roads).</summary>
	public struct TripRequest
	{
		public TripPurpose Purpose;
		public TravelModes AllowedModes;
		public AgeGroup AgeGroup;

		/// <summary>Owner-defined id echoed back in callbacks (citizen id, shipment id, service request id...).</summary>
		public int OwnerId;

		public CPos OriginRoad;
		public CPos DestinationRoad;

		/// <summary>Property ids (0 = none, e.g. outside connection or a road cell).</summary>
		public int OriginProperty;
		public int DestinationProperty;

		/// <summary>Earliest departure (world tick).</summary>
		public int DepartTick;

		/// <summary>Vehicle kind for non-citizen trips (truck, garbage truck, ambulance...). Sprite/actor image name, or null.</summary>
		public string VehicleType;
	}

	public struct TripResult
	{
		public int TripId;
		public int OwnerId;
		public TravelMode Mode;
		public int DepartTick;
		public int ArriveTick;
		public int CostCents;
	}

	public interface ITripListener
	{
		void OnTripArrived(in TripResult result);
		void OnTripFailed(int tripId, int ownerId, TripFailure reason);
	}

	public struct DemandFactor
	{
		/// <summary>Fluent key, e.g. "demand-factor-jobs".</summary>
		public string Key;

		/// <summary>Signed contribution in demand bar points.</summary>
		public int Value;
	}

	/// <summary>
	/// One record per city building (growable, service, hub, depot...). The registry is the shared
	/// blackboard between citizens, economy and services. Field ownership is strict: only the named
	/// package writes a field; everyone may read.
	/// </summary>
	public sealed class Property
	{
		// ---- identity (ZON) ----
		public int Id;                  // stable, monotonic, never reused
		public Actor Actor;             // current building actor (may be replaced on rebuild; Id stays)
		public CPos Origin;             // top-left footprint cell
		public int Width, Depth;
		public CPos AccessRoad;         // road cell vehicles/citizens use; CPos.Zero = none (map border, never a road)
		public CPos AccessCell;         // lot cell next to AccessRoad
		public PropertyKind Kind;
		public ZoneType Zone;           // None for services
		public int Level;               // 1..5
		public bool Operational;        // ZON keeps it in sync with CityBuilding.IsOperational
		public bool HasRoadAccess;      // connected to the outside (ZON)

		// ---- residential capacity (ZON) / occupancy (CIT) ----
		public int HouseholdSlots;
		public int Households;          // CIT
		public int Residents;           // CIT

		// ---- jobs: capacity by education (ECO for companies, SVC for services, ZON default) / filled (CIT) ----
		public readonly int[] JobSlots = new int[5];
		public readonly int[] JobsFilled = new int[5];

		// ---- education & health facilities (SVC) / usage (CIT) ----
		public int StudentSeats;
		public int SchoolLevel;         // 0 none, 1 elementary, 2 high school, 3 college, 4 university
		public int Students;            // CIT
		public int Beds;                // hospital/clinic beds (SVC)
		public int Patients;            // SVC

		// ---- economy (ECO) ----
		public int CompanyId;           // 0 = none / vacant

		// ---- land and money (ZON) ----
		public int LandValue;           // 0..100
		public int RentPerMonth;        // whole building, dollars

		public int TotalJobSlots => JobSlots[0] + JobSlots[1] + JobSlots[2] + JobSlots[3] + JobSlots[4];
		public int TotalJobsFilled => JobsFilled[0] + JobsFilled[1] + JobsFilled[2] + JobsFilled[3] + JobsFilled[4];
	}

	/// <summary>World trait. Owner: ZON.</summary>
	public interface IPropertyRegistry
	{
		/// <summary>Bumped on every add/remove/capacity change.</summary>
		int Version { get; }

		/// <summary>All properties ordered by Id (deterministic iteration).</summary>
		IReadOnlyList<Property> All { get; }

		Property Get(int id);

		/// <summary>Property covering this cell, or null.</summary>
		Property GetAt(CPos cell);

		Property GetByActor(Actor actor);

		event Action<Property> Added;
		event Action<Property> Removed;

		/// <summary>Capacity/level/operational changed.</summary>
		event Action<Property> Changed;

		/// <summary>A tenant (household via CIT, company via ECO) paid rent; ZON uses it for building condition and level-up.</summary>
		void ReportRentPaid(int propertyId, int dollars);
	}

	public enum RoadClass : byte { None = 0, Local, Collector, Arterial, Highway }

	public enum JunctionControl : byte { None = 0, Yield, Stop, Signal, Roundabout }

	public interface IRoadNetwork
	{
		/// <summary>Bumped when road cells, types or directions change.</summary>
		int NetworkVersion { get; }

		IEnumerable<CPos> RoadCells { get; }

		bool IsRoad(CPos cell);

		/// <summary>Whether a vehicle may step from `from` towards CityUtils.Neighbours4[dir] (respects one-way, barriers, highway ramps).</summary>
		bool CanEnter(CPos from, int dir);

		RoadClass GetClass(CPos cell);

		/// <summary>Lanes per direction (1..4).</summary>
		int GetLanes(CPos cell);

		/// <summary>Speed relative to a street (100).</summary>
		int GetSpeedPercent(CPos cell);

		/// <summary>Effective control of a junction cell (explicit or default by road classes).</summary>
		JunctionControl GetControl(CPos cell);

		/// <summary>On-street parking spaces in the cell.</summary>
		int GetParkingSlots(CPos cell);

		bool IsConnectedToOutside(CPos roadCell);

		/// <summary>Road cell a building lot should use (nearest road that gives access), or CPos.Zero.</summary>
		CPos GetAccessRoad(IEnumerable<CPos> footprint);
	}

	public enum CitizenActivity : byte { Home, Working, Studying, Shopping, Leisure, Travelling, Hospital, Prison, Moving, Dead }

	public struct CitizenView
	{
		public int Id;
		public int Age;
		public AgeGroup AgeGroup;
		public EducationLevel Education;
		public int HouseholdId;
		public int HomeProperty;
		public int WorkProperty;        // or school property for students
		public int Happiness;           // 0..100
		public int Health;              // 0..100
		public int HouseholdCash;       // dollars
		public CitizenActivity Activity;
		public string Name;             // generated deterministically from id
	}

	public interface ICitizenPopulation
	{
		int Population { get; }
		int Households { get; }
		int Homeless { get; }
		int Workers { get; }
		int Unemployed { get; }
		int Students { get; }
		int Tourists { get; }
		int AverageHappiness { get; }
		int AverageHealth { get; }

		int CountByAge(AgeGroup age);
		int CountByEducation(EducationLevel edu);
		int WorkersByEducation(EducationLevel edu);
		int UnemployedByEducation(EducationLevel edu);

		/// <summary>Average happiness factor contributions (fluent key, signed points) for the UI.</summary>
		IReadOnlyList<DemandFactor> HappinessFactors { get; }

		bool TryGetCitizen(int citizenId, out CitizenView view);

		/// <summary>Residents of a property (citizen ids), for the building panel.</summary>
		IEnumerable<int> ResidentsOf(int propertyId);

		/// <summary>Workers of a property (citizen ids).</summary>
		IEnumerable<int> WorkersOf(int propertyId);

		/// <summary>Rolling hash of the citizen state for determinism checks.</summary>
		int StateHash { get; }
	}

	public interface ITrafficService
	{
		/// <summary>Queue a trip. Returns a trip id (> 0) or 0 if impossible right now (listener is not called then).</summary>
		int RequestTrip(in TripRequest request, ITripListener listener);

		void CancelTrip(int tripId);

		/// <summary>Estimated door-to-door ticks with current congestion (-1 = unreachable).</summary>
		int EstimateTravelTicks(CPos fromRoad, CPos toRoad, TravelMode mode);

		int ActiveVehicles { get; }

		/// <summary>0..100 congestion of a road cell (legacy API used by info views).</summary>
		int GetTrafficLoad(CPos cell);

		/// <summary>0..100 speed relative to free flow.</summary>
		int GetTrafficFlow(CPos cell);

		/// <summary>City-wide traffic flow percent.</summary>
		int CityTrafficFlow { get; }

		/// <summary>0..100 traffic noise emitted at a road cell.</summary>
		int GetNoise(CPos cell);

		int StateHash { get; }
	}

	public interface ICityEconomy
	{
		/// <summary>Daily wage in cents for a worker of this education.</summary>
		int WageCentsPerDay(EducationLevel edu);

		/// <summary>Employer pays wages for its workers today (cents). Called by CIT once per worker per day.</summary>
		void ChargeWages(int propertyId, int cents);

		/// <summary>Income tax withheld from wages (cents), booked as city income.</summary>
		void BookIncomeTax(EducationLevel edu, int cents);

		/// <summary>Number of resources in the catalogue (ids 1..ResourceCount).</summary>
		int ResourceCount { get; }

		string ResourceName(int resourceId);

		/// <summary>Retail resources households consume (ids).</summary>
		IReadOnlyList<int> ConsumerResources { get; }

		/// <summary>Find a shop with stock near a cell (property id, 0 = none).</summary>
		int FindShop(int resourceId, CPos fromRoad);

		/// <summary>Household buys at a shop. Returns milli-units sold; cents charged via out param. CIT debits the household.</summary>
		int SellToHousehold(int shopPropertyId, int resourceId, int wantedMilli, int maxCents, out int centsCharged);

		int CompanyCount { get; }

		/// <summary>Short status string for a property's company (for UI), or null.</summary>
		string DescribeCompany(int propertyId);

		int StateHash { get; }
	}

	public interface ILogistics
	{
		/// <summary>Move goods between two properties (0 = best outside connection). Returns units dispatched.</summary>
		int TryDispatch(int fromProperty, int toProperty, int resourceId, int units, int unitPriceCents);

		/// <summary>Freight cost in cents per unit along the road network.</summary>
		int HaulCostCents(CPos fromRoad, CPos toRoad, int resourceId);

		int ShipmentsInTransit { get; }

		int StateHash { get; }
	}

	/// <summary>Extractor hubs (farms, forestry, mines, oil). Owner: IND. ECO's extractor companies read capacity from it.</summary>
	public interface IExtractorSource
	{
		/// <summary>Resource id produced by the hub on this property (0 = none).</summary>
		int Product(int propertyId);

		/// <summary>Maximum output (milli-units per day) given the area and richness.</summary>
		int CapacityMilliPerDay(int propertyId);

		/// <summary>Called by ECO after production so deposits deplete / trees get felled.</summary>
		void OnProduced(int propertyId, int units);
	}

	public enum ServiceKind : byte
	{
		Power = 0, Water, Sewage, Garbage, Health, Deathcare, Education, Police, Fire, Parks, Telecom, Post, Admin
	}

	public interface ICityServices
	{
		/// <summary>0..100 satisfaction of a service at a cell (coverage x capacity x efficiency).</summary>
		int GetSatisfaction(ServiceKind kind, CPos cell);

		/// <summary>Try to enrol a citizen in a school of the given level near home. Returns school property id or 0.</summary>
		int TryEnroll(int citizenId, int schoolLevel, CPos homeRoad);

		void Unenroll(int citizenId, int schoolProperty);

		/// <summary>A citizen got sick / needs a hospital. Returns true if treatment was arranged.</summary>
		bool RequestTreatment(int citizenId, int homeProperty);

		/// <summary>A citizen died at a property; deathcare will collect the body.</summary>
		void ReportDeath(int citizenId, int property);

		/// <summary>Budget percent per service (50..150).</summary>
		int GetBudget(ServiceKind kind);

		int StateHash { get; }
	}

	public interface IUtilityNetwork
	{
		int PowerProduced { get; }
		int PowerConsumed { get; }
		int WaterProduced { get; }
		int WaterConsumed { get; }
		int SewageCapacity { get; }
		int SewageProduced { get; }

		/// <summary>0..100 supply satisfaction for a building's utility.</summary>
		int PowerPercent(Actor building);
		int WaterPercent(Actor building);
		bool HasSewage(Actor building);

		int Version { get; }
	}

	public interface IPollutionMap
	{
		/// <summary>0..100.</summary>
		int GetGround(CPos cell);
		int GetAir(CPos cell);
		int GetNoise(CPos cell);
		int GetGroundwater(CPos cell);
		int GetWaterPollution(CPos cell);

		int Version { get; }
	}

	public struct PollutionEmission
	{
		/// <summary>0..100 at the source.</summary>
		public int Ground, Air, Noise, Water;

		/// <summary>Spread radius in cells (0 = default).</summary>
		public int Radius;
	}

	/// <summary>Actor trait interface: anything that pollutes (buildings, hubs, plants). ENV samples it every pulse.</summary>
	public interface IPollutionEmitter
	{
		PollutionEmission Emission { get; }
	}

	/// <summary>Monthly history and events for graphs, chirper and notifications. World trait.</summary>
	public interface ICityStatistics
	{
		/// <summary>Record a sample for a named series this month (last write wins per month).</summary>
		void Record(string series, int value);

		/// <summary>Up to `count` most recent monthly samples (oldest first).</summary>
		IReadOnlyList<int> History(string series, int count);

		/// <summary>Post a chirper-style message (fluent key + args) visible in the feed.</summary>
		void Chirp(string fluentKey, int citizenId = 0, string arg = null);
	}

	public interface IDemandModel
	{
		/// <summary>-100..100 per zone type (density-aware).</summary>
		int GetDemand(ZoneType zone);

		int GetDemand(ZoneCategory category);

		IReadOnlyList<DemandFactor> GetFactors(ZoneCategory category);
	}

	public interface IProgression
	{
		int Xp { get; }
		int NextMilestoneXp { get; }
		int MilestoneIndex { get; }
		string MilestoneName { get; }
		int DevPoints { get; }

		/// <summary>Unlock keys: actor names ("hospital"), "zone:ResidentialHigh", "road:avenue", "policy:...".</summary>
		bool IsUnlocked(string key);

		/// <summary>Gives XP (e.g. building placed). Synced callers only.</summary>
		void AddXp(int amount, string reason);

		/// <summary>Buildable-area gate (map tiles).</summary>
		bool IsCellOwned(CPos cell);

		/// <summary>Integer policy effect value (0 = off) for a key such as "TrafficSpeedPct", optionally per district.</summary>
		int GetPolicy(string key, CPos cell);

		/// <summary>District id at a cell (0 = none).</summary>
		int GetDistrict(CPos cell);
	}

	public struct TransitQuote
	{
		public bool Available;
		public int Ticks;
		public int FareCents;
		public int Transfers;
		public int ComfortPenalty;
	}

	public interface ITransitPlanner
	{
		TransitQuote Quote(CPos fromRoad, CPos toRoad, AgeGroup age);

		/// <summary>Book a transit journey for a trip already accepted by traffic mode choice.</summary>
		int StartJourney(in TripRequest request, ITripListener listener);
	}

	public interface ICityAutoTestReporter
	{
		/// <summary>Short single-line status (no newlines), e.g. "citizens pop=1234 hh=500 unemployed=12".</summary>
		string AutoTestReport();
	}
}
