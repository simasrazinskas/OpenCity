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
	[TraitLocation(SystemActors.Player)]
	[Desc("City services simulation (CS2 style): needs per property, road catchments with capacity, dispatch of service vehicles,",
		"fires, crime, sickness and death, budget sliders and upgrades. A 'day' here is one CityClock day (= a calendar month, 2400 ticks);",
		"all rates below are per such day unless noted.")]
	public class ServiceSimulationInfo : TraitInfo
	{
		[Desc("Needs are accrued for each property every this many pulses (25 ticks).")]
		public readonly int NeedIntervalPulses = 8;

		[Desc("Each catchment group is recomputed every this many pulses (staggered).")]
		public readonly int CatchmentIntervalPulses = 13;

		[Desc("Do not run service vehicles as traffic trips (everything virtual).")]
		public readonly bool VirtualOnly = false;

		[Desc("Real (traffic) service trips at the same time: min(Max, Base + population / PopDivisor).")]
		public readonly int RealVehiclesBase = 24;
		public readonly int RealVehiclesPopDivisor = 250;
		public readonly int RealVehiclesMax = 80;

		[Desc("Virtual vehicles: ticks per road cell (normal / emergency).")]
		public readonly int VirtualTicksPerCell = 8;
		public readonly int EmergencyTicksPerCell = 5;

		[Desc("Ticks a vehicle spends at a stop (garbage / mail / patient / body pick-up).")]
		public readonly int StopTicks = 20;

		[Desc("Book the monthly upkeep of bought upgrades with CityManager (category upkeep-<service>).",
			"The economy scales the base upkeep of each building by the service budget itself.")]
		public readonly bool BookUpgradeUpkeep = true;

		// ---- garbage ----
		[Desc("Garbage in kg per day: per resident / per job (commercial, industrial, office, service).")]
		public readonly int GarbagePerResident = 80;
		public readonly int GarbagePerJobCommercial = 60;
		public readonly int GarbagePerJobIndustrial = 120;
		public readonly int GarbagePerJobOffice = 30;
		public readonly int GarbagePerJobService = 40;

		[Desc("Percent less garbage per building level above 1.")]
		public readonly int GarbageLevelReduction = 8;

		[Desc("Cargo capacity of a garbage truck (kg) and of a post van (items).")]
		public readonly int TruckKg = 6000;
		public readonly int PostVanItems = 600;

		[Desc("A pile is collected once it exceeds this (kg), counts as 'piled' above PileKg and 'severe' above SeverePileKg.")]
		public readonly int CollectKg = 500;
		public readonly int PileKg = 1500;
		public readonly int SeverePileKg = 4000;

		[Desc("Kg a truck hauls per day when computing the coverage capacity of a garbage provider.")]
		public readonly int HaulKgPerVehicleDay = 20000;

		// ---- mail ----
		public readonly int MailPerResident = 12;
		public readonly int MailPerJob = 20;
		public readonly int MailCollectItems = 150;
		public readonly int MailPerVanDay = 20000;

		// ---- health and deathcare ----
		[Desc("Parts per million of residents getting sick per day (aggregate fallback when no citizen simulation).")]
		public readonly int SickPpmPerDay = 30000;
		public readonly int ResidentsPerBed = 100;
		public readonly int AmbulanceCapacity = 2;
		public readonly int StayTicks = 480;
		public readonly int UntreatedRecoverTicks = 1200;
		[Desc("Percent chance an untreated sick person dies when giving up, and per mille chance of dying in treatment.")]
		public readonly int UntreatedDeathPercent = 8;
		public readonly int TreatedDeathPermille = 3;
		public readonly int DeathPpmPerDay = 1500;
		public readonly int ResidentsPerHearse = 2500;
		public readonly int HearseCapacity = 3;
		public readonly int BodyOverdueTicks = 500;

		// ---- education ----
		[Desc("Percent of residents that are students of level 1..4 (aggregate fallback).")]
		public readonly ImmutableArray<int> StudentPercent = [17, 8, 6, 4];

		// ---- police, crime ----
		public readonly int CitizensPerPoliceVehicle = 900;
		public readonly int CrimePpmPerPressure = 250;
		public readonly int CrimeAlarmDelay = 50;
		public readonly int CrimeDuration = 150;
		public readonly int JailTicks = 600;
		public readonly int PrisonTicks = 2400;

		// ---- fire ----
		public readonly int CitizensPerFireVehicle = 1200;
		[Desc("Fire start chance per day, in ppm per hazard point.")]
		public readonly int FirePpmPerHazardPerDay = 48;
		public readonly int FireAlarmDelay = 25;
		public readonly int FireStartDamage = 10;
		public readonly int BurnTicksPerDamage = 5;
		public readonly int ExtinguishPerEngine = 2;
		public readonly int FireSpreadInterval = 50;
		public readonly int EngineHazardDivisor = 40;
		public readonly int MaxEngines = 3;

		[Desc("Actor name of the rubble left by a burnt building (1x1, one per footprint cell).")]
		public readonly string RubbleActor = "rubble";

		// ---- imported services (outside connections) ----
		public readonly int ImportDelayTicks = 350;

		[Desc("Minimum ticks between two imported vehicles of the same service.")]
		public readonly int ImportCooldownTicks = 300;

		public readonly int ImportCostAmbulance = 150;
		public readonly int ImportCostHearse = 200;
		public readonly int ImportCostFire = 500;
		public readonly int ImportCostGarbage = 200;

		// ---- wave 2: patrols, parks, fees, helicopters, disasters (neutral defaults: 0 = no effect) ----
		[Desc("Idle police cars patrol a random street of their catchment this often (ticks, 0 = no patrols). Cars needed for responses stay at home.")]
		public readonly int PatrolIntervalTicks = 800;
		public readonly int PatrolReserve = 1;

		[Desc("Crime pressure points a patrol pass takes off the lots within PatrolRadius cells (max PatrolMax); decays by 1 per need interval.")]
		public readonly int PatrolRelief = 0;
		public readonly int PatrolRadius = 4;
		public readonly int PatrolMax = 30;

		[Desc("Condition points a park loses per day (0 = parks never degrade). Below the threshold a maintenance vehicle is sent.")]
		public readonly int ParkDecayPerDay = 0;
		public readonly int ParkMaintThreshold = 60;
		public readonly int ParkRestore = 60;

		[Desc("Fee income in cents per treated patient / per student and day at 100% (0 = no fees). The player sets the percent with CitySetServiceFee.")]
		public readonly int HealthFeeCents = 0;
		public readonly int EducationFeeCentsPerStudent = 0;

		[Desc("Helicopters are sent when a patient / fire has waited this long, and fly this many ticks per cell.")]
		public readonly int HeliWaitTicks = 150;
		public readonly int HeliTicksPerCell = 3;

		[Desc("Chance per clear hot day (per mille) that a tree catches fire.",
			"Wildfires spread, burn WildfireBurnTicks and are put out by firewatch towers and fire stations.")]
		public readonly int WildfirePermille = 0;
		public readonly int WildfireMinTempX10 = 280;
		public readonly int WildfireBurnTicks = 600;
		public readonly int WildfireSpreadInterval = 50;
		public readonly int WildfireSpreadPercent = 30;
		public readonly int WildfireStationTicks = 300;
		public readonly int FirewatchSpotTicks = 150;

		[Desc("Chance per tick (per mille) of a lightning strike starting a fire during a storm.")]
		public readonly int StormLightningPermille = 0;

		// ---- coverage ----
		[Desc("Properties without an access road use the nearest road cell within this many cells.")]
		public readonly int AccessSearch = 4;

		[Desc("Coverage falls from 100% at the provider to this percent at the edge of its range.")]
		public readonly int EdgeCoveragePercent = 50;

		[Desc("Coverage spread from road cells to nearby ground: percent lost per cell, and cells.")]
		public readonly int GroundFalloff = 20;
		public readonly int GroundSpread = 2;

		public override object Create(ActorInitializer init) { return new ServiceSimulation(init.Self, this); }
	}
}
