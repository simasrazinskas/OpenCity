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

using System.Collections.Immutable;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("A city service building (garbage, health, deathcare, education, police, fire, parks, post, telecom, admin).",
		"Capacity, fleet and throughput feed ServiceSimulation; the legacy radius stamp is only used when that trait is absent.")]
	public class ServiceBuildingInfo : TraitInfo, Requires<CityBuildingInfo>
	{
		[Desc("Which service this building belongs to.")]
		public readonly ServiceKind Kind = ServiceKind.Police;

		[Desc("Legacy/radius coverage radius in cells (telecom, welfare, and the fallback when ServiceSimulation is absent).")]
		public readonly int Radius = 12;

		[Desc("Magnitude of the passive coverage at the building (0..100).")]
		public readonly int Strength = 100;

		[Desc("Catchment range in road cells (multi-source road BFS). 0 = use Radius.")]
		public readonly int Range = 0;

		[Desc("Kind-specific capacity: garbage storage (kg), hospital beds, cemetery body slots, school seats,",
			"jail/prison cells (police), leisure visits (parks), users (telecom/welfare).")]
		public readonly int Capacity = 0;

		[Desc("Units processed per day: garbage kg burnt/recycled, bodies cremated, mail handled.")]
		public readonly int Throughput = 0;

		[Desc("Number of service vehicles (garbage trucks, ambulances, hearses, patrol cars, fire engines, post vans).")]
		public readonly int Fleet = 0;

		[Desc("Vehicle sprite / actor name requested from traffic (garbage, ambulance, hearse, policecar, firetruck, van).")]
		public readonly string VehicleType = null;

		[Desc("Education: 1 elementary, 2 high school, 3 college, 4 university.")]
		public readonly int SchoolLevel = 0;

		[Desc("Police: this is a prison (takes inmates from police jails, no vehicles).")]
		public readonly bool Prison = false;

		[Desc("Garbage: money per kg of garbage processed (cents), e.g. recycling.")]
		public readonly int RevenueCentsPerKg = 0;

		[Desc("Fire/police: base fire hazard and crime pressure of the building itself (0 = default by kind).")]
		public readonly int FireHazard = 0;

		[Desc("Job mix by education (percent, edu 0..4) written to Property.JobSlots. Empty = leave the registry default.")]
		public readonly ImmutableArray<int> JobMix = [];

		[Desc("Effects apply to the whole city (city hall: less crime everywhere).")]
		public readonly bool CityWide = false;

		[Desc("Bit mask of the PRG districts this provider serves (bit n = district n, bit 0 = outside districts). 0 = unrestricted.",
			"The player can change it with CitySetServiceDistricts.")]
		public readonly int DistrictMask = 0;

		[Desc("Helicopter base: no road catchment; its helicopters fly straight to the target within FlightRange cells.")]
		public readonly bool Helicopter = false;

		public readonly int FlightRange = 60;

		[Desc("Firewatch tower: spots wildfires in this radius (cells) and puts them out; 0 = not a tower.")]
		public readonly int FirewatchRadius = 0;

		[Desc("Whether the building needs water to work at full efficiency.")]
		public readonly bool NeedsWater = true;

		public override object Create(ActorInitializer init) { return new ServiceBuilding(this); }
	}

	public class ServiceBuilding
	{
		public readonly ServiceBuildingInfo Info;

		/// <summary>Simulation-owned state (set by ServiceSimulation).</summary>
		internal ServiceState State;

		public ServiceBuilding(ServiceBuildingInfo info)
		{
			Info = info;
		}
	}
}
