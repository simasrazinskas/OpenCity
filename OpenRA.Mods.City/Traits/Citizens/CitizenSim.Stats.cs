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

namespace OpenRA.Mods.City.Traits
{
	// Counters (recounted once per pulse, so they can never drift), ICitizenPopulation, hashing and the autotest report.
	public partial class CitizenSim
	{
		readonly int[] byAge = new int[4];
		readonly int[] byEdu = new int[5];
		readonly int[] workersByEdu = new int[5];
		readonly int[] unemployedByEdu = new int[5];
		readonly int[] jobLevelCount = new int[5];

		public int StateHash { get; private set; }

		public int Population { get; private set; }
		public int Households { get; private set; }
		public int Homeless { get; private set; }
		public int Workers { get; private set; }
		public int Unemployed { get; private set; }
		public int Students { get; private set; }
		public int AverageHappiness { get; private set; } = 50;
		public int AverageHealth { get; private set; } = 100;
		public int Sick { get; private set; }
		public int OverEducatedWorkers { get; private set; }

		/// <summary>Out-of-city workers filling a share of the vacant jobs (statistic, they do not occupy job slots).</summary>
		public int Commuters { get; private set; }

		public int Births { get; private set; }
		public int Deaths { get; private set; }
		public int ImmigrantsLastMonth { get; private set; }
		public int EmigrantsLastMonth { get; private set; }

		public int Retired { get; private set; }

		public int CountByAge(AgeGroup age) { return byAge[(int)age]; }

		public int CountByEducation(EducationLevel edu) { return byEdu[(int)edu]; }

		public int WorkersByEducation(EducationLevel edu) { return workersByEdu[(int)edu]; }

		public int UnemployedByEducation(EducationLevel edu) { return unemployedByEdu[(int)edu]; }

		void RecountStats()
		{
			Array.Clear(byAge);
			Array.Clear(byEdu);
			Array.Clear(workersByEdu);
			Array.Clear(unemployedByEdu);
			Array.Clear(jobLevelCount);
			int away = 0, inmates = 0, pop = 0, workers = 0, unemployed = 0, students = 0, sick = 0, retired = 0, over = 0;
			long happy = 0, health = 0;
			for (var i = 0; i < citCount; i++)
			{
				var c = cits[i];
				if ((c.Flags & CitFlags.Alive) == 0)
					continue;

				pop++;
				var age = AgeOf(i);
				byAge[(int)GroupOf(age)]++;
				byEdu[c.Education]++;
				happy += c.Wellbeing;
				health += c.Health;
				if ((c.Flags & CitFlags.Sick) != 0)
					sick++;

				if ((c.Flags & CitFlags.Jailed) != 0)
				{
					if (c.InPrison)
						away++;
					else
						inmates++;

					continue;
				}

				if ((c.Flags & CitFlags.Student) != 0)
				{
					students++;
					continue;
				}

				if (age < info.WorkAge || age > info.AdultMaxAge)
				{
					if (age > info.AdultMaxAge)
						retired++;

					continue;
				}

				workers++;
				if ((c.Flags & CitFlags.Worker) != 0)
				{
					workersByEdu[c.Education]++;
					jobLevelCount[c.JobLevel]++;
					if (c.JobLevel < c.Education)
						over++;
				}
				else
				{
					unemployed++;
					unemployedByEdu[c.Education]++;
				}
			}

			Inmates = inmates;
			PrisonersAway = away;
			Population = pop;
			Workers = workers;
			Unemployed = unemployed;
			Students = students;
			Sick = sick;
			Retired = retired;
			OverEducatedWorkers = over;
			AverageHappiness = pop > 0 ? (int)(happy / pop) : 50;
			AverageHealth = pop > 0 ? (int)(health * 100 / 255 / pop) : 100;
			Households = hhCount - freeHhN;
			var hl = 0;
			for (var i = 0; i < hhCount; i++)
				if ((hhs[i].Flags & (HhFlags.Alive | HhFlags.Homeless)) == (HhFlags.Alive | HhFlags.Homeless))
					hl += hhs[i].Size;

			Homeless = hl;

			Commuters = commuterTotal;
		}

		void UpdateHash()
		{
			unchecked
			{
				var h = Version;
				h = h * 31 + Population;
				h = h * 31 + Households;
				h = h * 31 + Workers;
				h = h * 31 + Unemployed;
				h = h * 31 + births;
				h = h * 31 + deaths;
				h = h * 31 + immigrants;
				h = h * 31 + emigrants;
				h = h * 31 + graduates;
				h = h * 31 + AverageHappiness;
				h = h * 31 + rng;
				h = h * 31 + (int)(wagesPaidCents ^ (rentPaidCents << 3) ^ (spentCents << 7));
				for (var e = 0; e < 5; e++)
					h = h * 31 + byEdu[e] + workersByEdu[e] * 7;

				// Sample the arrays so the hash also reflects individual state without an O(N) pass per pulse.
				var step = Math.Max(1, citCount / 64);
				for (var i = world.WorldTick / 25 % step; i < citCount; i += step)
					h = h * 31 + cits[i].BirthDay + cits[i].Health * 3 + cits[i].Education + cits[i].Household * 5 + cits[i].Work;

				StateHash = h;
			}
		}

		public bool TryGetCitizen(int citizenId, out CitizenView view)
		{
			view = default;
			var ci = citizenId - 1;
			if (ci < 0 || ci >= citCount || (cits[ci].Flags & CitFlags.Alive) == 0)
				return false;

			var c = cits[ci];
			var age = AgeOf(ci);
			var hh = c.Household;
			view = new CitizenView
			{
				Id = citizenId,
				Age = age,
				AgeGroup = GroupOf(age),
				Education = (EducationLevel)c.Education,
				HouseholdId = hh + 1,
				HomeProperty = hh >= 0 ? hhs[hh].Home : 0,
				WorkProperty = c.Work,
				Happiness = c.Wellbeing,
				Health = c.Health * 100 / 255,
				HouseholdCash = hh >= 0 ? hhs[hh].Cash / 100 : 0,
				Activity = ActivityOf(ci),
				Name = NameOf(ci, c.BirthDay, (c.Flags & CitFlags.Male) != 0),
			};

			return true;
		}

		static readonly string[] MaleNames =
			["Alex", "Ben", "Carl", "Dan", "Eli", "Finn", "Gus", "Hugo", "Ivan", "Jack", "Karl", "Leo", "Max", "Nils", "Otto", "Paul"];

		static readonly string[] FemaleNames =
			["Anna", "Bea", "Cleo", "Dora", "Eva", "Fay", "Gina", "Hana", "Iris", "Jana", "Kira", "Lena", "Mia", "Nora", "Olga", "Pia"];

		static readonly string[] Surnames =
			["Smith", "Novak", "Rossi", "Jensen", "Kovac", "Silva", "Meyer", "Dubois", "Ivanov", "Costa", "Berg", "Horvat", "Lindt", "Moreau", "Varga", "Weiss"];

		static string NameOf(int ci, int birth, bool male)
		{
			var h = Hash(ci, birth, 99);
			return (male ? MaleNames : FemaleNames)[h & 15] + " " + Surnames[(h >> 4) & 15];
		}

		public IEnumerable<int> ResidentsOf(int propertyId)
		{
			for (var hh = 0; hh < hhCount; hh++)
			{
				if ((hhs[hh].Flags & HhFlags.Alive) == 0 || hhs[hh].Home != propertyId)
					continue;

				for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
					yield return m + 1;
			}
		}

		public IEnumerable<int> WorkersOf(int propertyId)
		{
			for (var i = 0; i < citCount; i++)
				if ((cits[i].Flags & (CitFlags.Alive | CitFlags.Worker)) == (CitFlags.Alive | CitFlags.Worker) && cits[i].Work == propertyId)
					yield return i + 1;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			// Timing is real-time data: print it on a perf line, never in the deterministic report.
			System.Console.WriteLine($"[autotest-perf t={self.World.WorldTick}] citizens {PerfReport()}");
			if (inert)
				return "citizens inert";

			var trips = tripsAccepted > 0 ? $" trips={tripsRequested}/{tripsAccepted}/{tripsArrived}/{tripsFailed}" : $" trips={tripsRequested}(refused={tripsRefused})";
			return $"citizens pop={Population} hh={Households} homeless={Homeless} age={byAge[0]}/{byAge[1]}/{byAge[2]}/{byAge[3]} " +
				$"edu={byEdu[0]}/{byEdu[1]}/{byEdu[2]}/{byEdu[3]}/{byEdu[4]} workers={Workers} unemp={Unemployed} " +
				$"employedByEdu={workersByEdu[0]}/{workersByEdu[1]}/{workersByEdu[2]}/{workersByEdu[3]}/{workersByEdu[4]} overEdu={OverEducatedWorkers} " +
				$"students={Students} sick={Sick} births={births} deaths={deaths} sickCases={sicknessCases} jobs+/-={jobsFound}/{jobsLost} " +
				$"enrolled={enrollments} benefits={benefitsPaidCents / 100} imm={immigrants} emi={emigrants} refused={refused} " +
				$"grads={graduates} hap={AverageHappiness} health={AverageHealth} tourists={Tourists}/{TouristsLodged}/{LodgingBeds} commuters={Commuters} " +
				$"crimes={crimes}/{arrests}/{escapes} jail={Inmates}/{PrisonersAway} commuteQuits={commuteQuits} tourismCents={tourismCents} " +
				$"touristTrips={touristTripsRequested} railTourists={railTourists} " +
				$"wages={wagesPaidCents / 100} rent={rentPaidCents / 100} spent={spentCents / 100} unmetShop={unmetShopping} quota={immigQuotaToday} homes={homeCands.Count}{trips} tph={string.Join(",", tripsByHour)} StateHash={StateHash}";
		}
	}
}
