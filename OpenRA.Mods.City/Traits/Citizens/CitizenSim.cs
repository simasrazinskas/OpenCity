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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("The per-citizen agent simulation: households, ageing, birth/death, schooling, jobs by education, wages, rent,",
		"shopping, health, happiness factors and daily schedules that become trips. Plain struct arrays, no actors.",
		"All rates are per game day (= one calendar month) unless stated; one citizen year passes per DaysPerCitizenYear days.")]
	public class CitizenSimInfo : TraitInfo
	{
		[Desc("Game days (months) per citizen year.")]
		public readonly int DaysPerCitizenYear = 1;

		[Desc("Highest age (years) of a child, teen and adult. Older citizens are seniors.")]
		public readonly int ChildMaxAge = 11;
		public readonly int TeenMaxAge = 17;
		public readonly int AdultMaxAge = 64;

		[Desc("Youngest age that may hold a job.")]
		public readonly int WorkAge = 16;

		[Desc("Immigration is continuous (settled every pulse), demand-limited: free home slots * attraction% * FillPercent% per day,",
			"capped at ImmigrationBase + population / ImmigrationPopDivisor households per day (day = one calendar month).")]
		public readonly int ImmigrationFillPercent = 400;
		public readonly int ImmigrationBase = 40;
		public readonly int ImmigrationPopDivisor = 8;

		[Desc("How often per day every citizen/household is scanned for job and home searches (event-driven searches also run on move-in).")]
		public readonly int SearchScansPerDay = 8;

		[Desc("Failed searches after which unemployed citizens accept jobs up to two education levels below their own.")]
		public readonly int TriesBeforeLowerJobs = 2;

		[Desc("Household type weights: single, couple, family, senior, student.")]
		public readonly int[] HouseholdTypeWeights = [30, 20, 35, 8, 7];

		[Desc("Education of immigrants: baseline weights per level, plus the vacant jobs at that level.")]
		public readonly int[] ImmigrantEducationBaseline = [20, 40, 25, 10, 5];

		[Desc("Candidate homes/jobs/schools sampled per search.")]
		public readonly int SearchSamples = 8;

		[Desc("Rent may use at most this percent of the expected household income.")]
		public readonly int RentToIncomeMax = 60;

		[Desc("Days a household searches for a home before it leaves the city (or, if it lives here, becomes homeless).")]
		public readonly int GiveUpDays = 3;

		[Desc("Days a homeless household stays before it emigrates.")]
		public readonly int HomelessDaysToEmigrate = 4;

		[Desc("Households whose cash is below minus this (cents) for DebtDaysToEvict days are evicted.")]
		public readonly int DebtLimitCents = 15000;
		public readonly int DebtDaysToEvict = 2;

		[Desc("Starting cash of an immigrant household, in cents.")]
		public readonly int StartCashCents = 9000;

		[Desc("Fallback rent per household and day (cents) = Base + PerLevel * level, when ZON sets no rent.")]
		public readonly int FallbackRentBaseCents = 600;
		public readonly int FallbackRentPerLevelCents = 200;

		[Desc("Fallback wage per day (cents) by job level, used when no ICityEconomy exists.")]
		public readonly int[] FallbackWageCents = [3000, 3600, 4200, 4800, 5400];

		[Desc("Benefits per day (cents): unemployment (adult without job), pension (senior), allowance per child.")]
		public readonly int UnemploymentBenefitCents = 1000;
		public readonly int PensionCents = 1800;
		public readonly int ChildAllowanceCents = 400;

		[Desc("Shopping budget per person and day, in cents, and the share (percent) of households shopping in person per day.")]
		public readonly int ShoppingCentsPerPerson = 900;
		public readonly int ShopTripPercent = 60;

		[Desc("Annual birth chance (ppm per citizen year) for eligible couples and singles, and the max kids per household.")]
		public readonly int BirthPPM = 150000;
		public readonly int MaxKids = 3;

		[Desc("Annual death probability (ppm) by age band: <50, 50-64, 65-74, 75-84, 85-94, 95+.")]
		public readonly int[] DeathPPMByAgeBand = [1000, 6000, 20000, 60000, 180000, 400000];

		[Desc("Annual sickness chance (ppm) at full health; scaled up as health falls.")]
		public readonly int SickPPM = 60000;

		[Desc("Chance (percent) per day that a sick citizen recovers, and the extra when treated.")]
		public readonly int RecoverPercent = 55;
		public readonly int TreatedRecoverBonus = 35;

		[Desc("Days a citizen needs to study per school level (1 elementary .. 4 university), and the pass chance (percent).")]
		public readonly int[] StudyDaysByLevel = [0, 6, 4, 3, 4];
		public readonly int StudyPassPercent = 85;

		[Desc("Fallback seats of an education service building without ZON/SVC seats (level 1).")]
		public readonly int FallbackSchoolSeats = 60;

		[Desc("Percent chance per day that a teen (high school), adult (college) or adult (university) applies when a school exists.")]
		public readonly int TeenApplyPercent = 70;
		public readonly int CollegeApplyPercent = 25;
		public readonly int UniversityApplyPercent = 15;

		[Desc("Hours (0..23) of the daily schedule: work start, work duration, school start, school duration.")]
		public readonly int WorkStartHour = 8;
		public readonly int WorkHours = 8;
		public readonly int SchoolStartHour = 8;
		public readonly int SchoolHours = 6;

		[Desc("Transit cost in ticks per 100 cents of fare when comparing with the car.")]
		public readonly int FareTicksPerCent = 50;

		[Desc("Departure jitter in ticks and the cap on trip requests per tick.")]
		public readonly int DepartJitterTicks = 100;
		public readonly int MaxTripRequestsPerTick = 120;

		[Desc("Share of vacant jobs (percent) filled by commuters from outside, and tourist arrivals per day at full park coverage.")]
		public readonly int CommuterPercentOfVacancies = 30;
		public readonly int TouristsPerDayAtFullAttraction = 6;
		public readonly int TouristStayDays = 2;

		[Desc("Crime: daily chance (ppm) per 100 risk points of an adult turning to crime; risk points come from unhappiness, unemployment and poverty,",
			"reduced by police coverage. Arrest chance = ArrestBasePercent + coverage * ArrestPerCoveragePercent / 100.")]
		public readonly int CrimeBasePPM = 8000;
		public readonly int CrimeTargetSamples = 5;
		public readonly int ArrestBasePercent = 15;
		public readonly int ArrestPerCoveragePercent = 70;

		[Desc("Sentences in days (months): jail, and the share (percent) that goes to prison instead for PrisonDays. Victims lose wellbeing points.")]
		public readonly int JailDays = 2;
		public readonly int PrisonPercent = 30;
		public readonly int PrisonDays = 6;
		public readonly int CrimeVictimWellbeing = 15;

		[Desc("Lodging for tourists: beds per job of a hotel (an actor whose name contains 'hotel'),",
			"and spare beds of any other commercial property = jobs / divisor.")]
		public readonly int HotelBedsPerJob = 3;
		public readonly int SpareLodgingJobsDivisor = 4;

		[Desc("Tourist spending per person and day in cents (at a shop via ICityEconomy, else booked as tourism income), and per leisure visit of a resident.")]
		public readonly int TouristSpendCents = 500;
		public readonly int LeisureSpendCents = 300;

		[Desc("Commute: ticks of a work trip considered fine, the penalty divisor (ticks per happiness point, max 10),",
			"ticks above which the worker starts looking for a nearer job, and the bad days in a row before quitting.")]
		public readonly int CommuteOkTicks = 150;
		public readonly int CommutePenaltyDivisor = 40;
		public readonly int CommuteQuitTicks = 500;
		public readonly int CommuteStrikesToQuit = 2;

		[Desc("Maximum life-event chirps (births, graduations, deaths, arrests) per day.")]
		public readonly int ChirpsPerDay = 6;

		public override object Create(ActorInitializer init) { return new CitizenSim(init.Self, this); }
	}

	[Flags]
	enum CitFlags : byte { None = 0, Male = 1, Worker = 2, Student = 4, Sick = 8, Alive = 16, Treated = 32, Retired = 64, Jailed = 128 }

	[Flags]
	enum HhFlags : byte { None = 0, Alive = 1, Homeless = 2, WantsMove = 4 }

	enum HhType : byte { Single = 0, Couple, Family, Senior, Student }

	struct Citizen
	{
		public int BirthDay;        // clock day index at birth (negative for the founding cohort)
		public int Household;       // household index, -1 = free slot
		public int NextInHousehold; // intrusive list, -1 = end
		public int Work;            // property id of the job or school, 0 = none
		public int TripId;          // active trip, 0 = none
		public int Loc;             // property id the citizen currently is at (0 = home)
		public CitFlags Flags;
		public byte Education;
		public byte JobLevel;
		public byte Health;         // 0..255
		public byte Wellbeing;      // 0..100
		public byte Leisure;
		public byte UnemployedDays;
		public byte Cooldown;       // years until the next birth
		public byte Shift;
		public byte Act;            // Leg of the active/last trip
		public bool ByTransit;      // the active trip is a transit/taxi journey (booked at the planner)
		public ushort StudyDays;
		public ushort Commute;      // moving average of the work trip duration in ticks
		public byte CommuteStrikes;
		public byte JailDays;       // sentence days left (Jailed flag)
		public bool InPrison;       // serving outside the city
		public int TripDest;
	}

	struct Household
	{
		public int Home;            // property id, 0 = homeless / not yet housed
		public int FirstMember;
		public int Cash;            // cents
		public int Rent;            // cents per day
		public int EnvScore;        // sum of home-environment happiness factors (points)
		public int Spent;           // cents spent shopping today
		public short HealthMod;     // health target modifier from clinics and pollution
		public byte HealthCov;      // 0..100 health-care coverage at home
		public HhFlags Flags;
		public HhType Type;
		public byte Size;
		public byte Kids;
		public byte Happiness;      // 0..100
		public byte SearchDays;
		public byte HomelessDays;
		public byte DebtDays;
		public byte MovedInDays;
	}

	public partial class CitizenSim : ITick, IWorldLoaded, ITripListener, ICityAutoTestReporter, ICitizenPopulation
	{
		const int Version = 1;
		readonly CitizenSimInfo info;
		readonly Actor self;
		readonly World world;

		Citizen[] cits = new Citizen[1024];
		int citCount;
		int[] freeCits = new int[256];
		int freeCitN;

		Household[] hhs = new Household[512];
		int hhCount;
		int[] freeHhs = new int[128];
		int freeHhN;

		// Providers (all optional).
		CityClock clock;
		IPropertyRegistry registry;
		CityManager cm;
		CityCoverageLayer coverage;
		ICityEconomy economy;
		ICityServices services;
		ITrafficService traffic;
		IDemandModel demandModel;
		ITransitPlanner planner;
		IIntercityRail rail;
		TransitLayer transitLayer;
		IProgression progression;
		ITourism tourism;
		DemandModel demandConcrete;
		CityEconomy economyConcrete;
		PollutionLayer pollutionLayer;
		CityClimate climate;
		ICityStatistics stats;

		bool initialised;
		bool inert;

		// Scheduling state.
		int lastDay = -1;
		int dayCitizens;
		int dayHouseholds;
		int rng = 12345;

		public CitizenSim(Actor self, CitizenSimInfo info)
		{
			this.self = self;
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, OpenRA.Graphics.WorldRenderer wr)
		{
			inert = !self.Owner.Playable;
		}

		void Init()
		{
			initialised = true;
			var wa = world.WorldActor;
			clock = wa.TraitOrDefault<CityClock>();
			registry = wa.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			coverage = wa.TraitOrDefault<CityCoverageLayer>();
			economy = wa.TraitsImplementing<ICityEconomy>().FirstOrDefault() ?? self.TraitsImplementing<ICityEconomy>().FirstOrDefault();
			services = wa.TraitsImplementing<ICityServices>().FirstOrDefault() ?? self.TraitsImplementing<ICityServices>().FirstOrDefault();
			traffic = self.TraitsImplementing<ITrafficService>().FirstOrDefault() ?? wa.TraitsImplementing<ITrafficService>().FirstOrDefault();
			demandModel = wa.TraitsImplementing<IDemandModel>().FirstOrDefault() ?? self.TraitsImplementing<IDemandModel>().FirstOrDefault();
			cm = self.TraitOrDefault<CityManager>();
			planner = wa.TraitsImplementing<ITransitPlanner>().FirstOrDefault() ?? self.TraitsImplementing<ITransitPlanner>().FirstOrDefault();
			transitLayer = planner as TransitLayer;
			rail = planner as IIntercityRail;
			progression = self.TraitsImplementing<IProgression>().FirstOrDefault() ?? wa.TraitsImplementing<IProgression>().FirstOrDefault();
			tourism = self.TraitsImplementing<ITourism>().FirstOrDefault();
			demandConcrete = demandModel as DemandModel;
			economyConcrete = economy as CityEconomy;
			pollutionLayer = wa.TraitOrDefault<PollutionLayer>();
			climate = wa.TraitOrDefault<CityClimate>();
			stats = wa.TraitsImplementing<ICityStatistics>().FirstOrDefault() ?? self.TraitsImplementing<ICityStatistics>().FirstOrDefault();
			cityCrime = self.TraitsImplementing<ICityCrime>().FirstOrDefault();
			if (cityCrime != null)
				cityCrime.ExternalCrimeSource = true;
			roads = wa.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
		}

		int TicksPerDay => clock?.TicksPerDay ?? 2400;

		int Today => clock?.DayIndex ?? world.WorldTick / 2400;

		int TickOfDay => clock?.TickOfDay ?? world.WorldTick % 2400;

		int NextRandom(int bound)
		{
			unchecked
			{
				rng = rng * 1103515245 + 12345;
				var v = (rng >> 8) & 0x7fffff;
				return bound <= 1 ? 0 : v % bound;
			}
		}

		static int Hash(int a, int b, int salt)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 2246822519u ^ (uint)salt * 3266489917u;
				h ^= h >> 15;
				h *= 2654435761u;
				h ^= h >> 13;
				return (int)(h & 0x7fffffff);
			}
		}

		int AgeOf(int ci) { return (Today - cits[ci].BirthDay) / Math.Max(1, info.DaysPerCitizenYear); }

		AgeGroup GroupOf(int age)
		{
			if (age <= info.ChildMaxAge)
				return AgeGroup.Child;
			if (age <= info.TeenMaxAge)
				return AgeGroup.Teen;
			return age <= info.AdultMaxAge ? AgeGroup.Adult : AgeGroup.Senior;
		}

		void ITick.Tick(Actor self)
		{
			if (inert)
				return;

			if (!initialised)
				Init();

			if (registry == null)
				return;

			var start = System.Diagnostics.Stopwatch.GetTimestamp();
			var tick = world.WorldTick;
			var today = Today;
			var d = TicksPerDay;
			var t = TickOfDay;

			if (today != lastDay)
			{
				lastDay = today;
				dayCitizens = citCount;
				dayHouseholds = hhCount;
				OnNewDay();
			}

			// Each citizen/household is processed exactly once per day: the index range of this tick.
			var cFrom = (int)((long)t * dayCitizens / d);
			var cTo = (int)((long)(t + 1) * dayCitizens / d);
			for (var i = cFrom; i < cTo; i++)
				if ((cits[i].Flags & CitFlags.Alive) != 0)
					UpdateCitizen(i, today);

			var hFrom = (int)((long)t * dayHouseholds / d);
			var hTo = (int)((long)(t + 1) * dayHouseholds / d);
			for (var i = hFrom; i < hTo; i++)
				if ((hhs[i].Flags & HhFlags.Alive) != 0)
					UpdateHousehold(i, today);

			if (tick % 25 == 0)
				OnPulse();

			RunScheduledTrips(t);

			// Diagnostics only (wall clock, never read by the simulation).
			var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
			perfTotal += elapsed;
			perfTicks++;
			if (elapsed > perfMax)
				perfMax = elapsed;
		}

		long perfTotal, perfMax, perfTicks;

		string PerfReport()
		{
			var freq = System.Diagnostics.Stopwatch.Frequency;
			var avgUs = perfTicks > 0 ? perfTotal * 1000000 / freq / perfTicks : 0;
			var maxUs = perfMax * 1000000 / freq;
			perfMax = 0;
			return $"simUs avg/max={avgUs}/{maxUs}";
		}

		void OnNewDay()
		{
			RotateDaily();
		}

		void OnPulse()
		{
			RebuildCandidates();
			FireExcessWorkers();
			RecountStats();
			Immigrate();
			ScanSeekers();
			ExpirePendingCrimes();
			TickTourists();
			FlushMoney();
			FlushTourism();
			UpdateHash();
		}
	}
}
