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

namespace OpenRA.Mods.City.Traits
{
	// Immigration at outside connections, household types, home choice, move-out, homelessness.
	public partial class CitizenSim
	{
		int immigrants, emigrants, refused;
		int immigAccumMilli;
		int immigQuotaToday;

		/// <summary>0..100 attraction of the city for new households (household demand, ignoring housing vacancy).</summary>
		int ResidentialDemand()
		{
			if (demandConcrete != null)
				return demandConcrete.ResidentialAttraction;

			if (cm != null)
				return cm.ResidentialAttraction;

			return demandModel != null ? demandModel.GetDemand(ZoneCategory.Residential) : 50;
		}

		/// <summary>Households per day that would move in right now (demand-limited, scaled by the free homes).</summary>
		void ComputeImmigrationQuota()
		{
			var demand = ResidentialDemand();
			if (demand <= 0 || freeHomeSlots == 0)
			{
				immigQuotaToday = 0;
				return;
			}

			var want = Math.Max(1, freeHomeSlots * demand * info.ImmigrationFillPercent / 10000);
			var cap = info.ImmigrationBase + Population / Math.Max(1, info.ImmigrationPopDivisor);
			immigQuotaToday = Math.Min(want, cap);
		}

		void Immigrate()
		{
			ComputeImmigrationQuota();
			if (immigQuotaToday <= 0)
			{
				immigAccumMilli = 0;
				return;
			}

			immigAccumMilli += immigQuotaToday * 25;
			var spawned = 0;
			while (immigAccumMilli >= TicksPerDay && spawned < 400)
			{
				immigAccumMilli -= TicksPerDay;
				spawned++;
				if (homeCands.Count == 0)
				{
					refused++;
					continue;
				}

				SpawnImmigrantHousehold();
			}
		}

		HhType PickType()
		{
			var w = info.HouseholdTypeWeights;
			var total = 0;
			for (var i = 0; i < w.Length; i++)
				total += w[i];

			var r = NextRandom(Math.Max(1, total));
			for (var i = 0; i < w.Length; i++)
			{
				if (r < w[i])
					return (HhType)i;

				r -= w[i];
			}

			return HhType.Single;
		}

		/// <summary>Education of a working-age immigrant: baseline plus vacant jobs at that level (open jobs pull matching people in).</summary>
		int SampleImmigrantEducation()
		{
			var total = 0;
			for (var e = 0; e < 5; e++)
				total += info.ImmigrantEducationBaseline[e] + Math.Min(vacantByEdu[e], 100);

			var r = NextRandom(Math.Max(1, total));
			for (var e = 0; e < 5; e++)
			{
				var w = info.ImmigrantEducationBaseline[e] + Math.Min(vacantByEdu[e], 100);
				if (r < w)
					return e;

				r -= w;
			}

			return 1;
		}

		int NewCitizen(int hh, int age, int education, bool male)
		{
			var ci = AllocCitizen();
			var y = Math.Max(1, info.DaysPerCitizenYear);
			cits[ci].BirthDay = Today - age * y - NextRandom(y);
			cits[ci].Education = (byte)education;
			cits[ci].Health = (byte)(200 + NextRandom(56));
			cits[ci].Wellbeing = 60;
			cits[ci].Cooldown = (byte)NextRandom(3);
			cits[ci].JobLevel = 0;
			if (male)
				cits[ci].Flags |= CitFlags.Male;

			if (age > info.AdultMaxAge)
				cits[ci].Flags |= CitFlags.Retired;

			AddMember(hh, ci);
			return ci;
		}

		void SpawnImmigrantHousehold()
		{
			var type = PickType();
			var hh = AllocHousehold();
			hhs[hh].Type = type;
			hhs[hh].Cash = info.StartCashCents + NextRandom(info.StartCashCents);
			switch (type)
			{
				case HhType.Single:
					NewCitizen(hh, 20 + NextRandom(36), SampleImmigrantEducation(), NextRandom(2) == 0);
					break;
				case HhType.Couple:
					NewCitizen(hh, 22 + NextRandom(34), SampleImmigrantEducation(), true);
					NewCitizen(hh, 22 + NextRandom(34), SampleImmigrantEducation(), false);
					break;
				case HhType.Family:
				{
					NewCitizen(hh, 26 + NextRandom(20), SampleImmigrantEducation(), true);
					NewCitizen(hh, 24 + NextRandom(20), SampleImmigrantEducation(), false);
					var kids = 1 + NextRandom(Math.Max(1, info.MaxKids));
					for (var k = 0; k < kids; k++)
					{
						var age = NextRandom(16);
						NewCitizen(hh, age, age > info.ChildMaxAge ? 1 : 0, NextRandom(2) == 0);
					}

					break;
				}

				case HhType.Senior:
					NewCitizen(hh, info.AdultMaxAge + 1 + NextRandom(16), NextRandom(3), NextRandom(2) == 0);
					if (NextRandom(2) == 0)
						NewCitizen(hh, info.AdultMaxAge + 1 + NextRandom(16), NextRandom(3), false);
					break;
				default:
					NewCitizen(hh, 18 + NextRandom(8), 2, NextRandom(2) == 0);
					break;
			}

			var home = ChooseHome(hh, -1);
			if (home == null)
			{
				refused++;
				FreeHousehold2(hh);
				return;
			}

			MoveIn(hh, home);
			immigrants++;
			SettleNewcomers(hh);
		}

		/// <summary>Event-driven first searches: the newcomers look for school seats and jobs right away instead of waiting for their daily bucket.</summary>
		void SettleNewcomers(int hh)
		{
			var today = Today;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
			{
				var age = AgeOf(m);
				UpdateSchooling(m, age, today);
				SeekJob(m, age);
				PlanDay(m, age, today);
			}
		}

		/// <summary>Drops a household that never moved in (members never reached the city).</summary>
		void FreeHousehold2(int hh)
		{
			var m = hhs[hh].FirstMember;
			while (m >= 0)
			{
				var next = cits[m].NextInHousehold;
				FreeCitizen(m);
				m = next;
			}

			FreeHousehold(hh);
		}

		void MoveIn(int hh, Property p)
		{
			hhs[hh].Home = p.Id;
			hhs[hh].Flags &= ~(HhFlags.Homeless | HhFlags.WantsMove);
			hhs[hh].Rent = RentCents(p);
			hhs[hh].SearchDays = 0;
			hhs[hh].HomelessDays = 0;
			hhs[hh].MovedInDays = 0;
			p.Households++;
			p.Residents += hhs[hh].Size;
		}

		int RentCents(Property p)
		{
			if (p.RentPerMonth > 0)
				return Math.Max(1, p.RentPerMonth * 100 / Math.Max(1, p.HouseholdSlots));

			return info.FallbackRentBaseCents + info.FallbackRentPerLevelCents * Math.Max(1, p.Level);
		}

		int WageOf(int level)
		{
			if (economy != null)
				return economy.WageCentsPerDay((EducationLevel)Math.Clamp(level, 0, 4));

			return info.FallbackWageCents[Math.Clamp(level, 0, info.FallbackWageCents.Length - 1)];
		}

		/// <summary>Cents per day the household can count on: wages by education for working-age members, else benefits.</summary>
		int ExpectedIncome(int hh)
		{
			var income = 0;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
			{
				var age = AgeOf(m);
				if (age >= info.WorkAge && age <= info.AdultMaxAge && (cits[m].Flags & CitFlags.Student) == 0)
					income += (cits[m].Flags & CitFlags.Worker) != 0 ? WageOf(cits[m].JobLevel) : WageOf(cits[m].Education) * 3 / 4;
				else if (age > info.AdultMaxAge)
					income += info.PensionCents;
				else if (age <= info.TeenMaxAge)
					income += info.ChildAllowanceCents;
			}

			return Math.Max(1, income);
		}

		bool IsStudentHousehold(int hh)
		{
			if (hhs[hh].Type == HhType.Student)
				return true;

			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
				if ((cits[m].Flags & CitFlags.Student) != 0 && AgeOf(m) > info.TeenMaxAge)
					return true;

			return false;
		}

		/// <summary>Best affordable home among sampled candidates (null if none). `current` is the present home id for relocation.</summary>
		Property ChooseHome(int hh, int current)
		{
			var n = homeCands.Count;
			if (n == 0)
				return null;

			var income = ExpectedIncome(hh);
			var size = hhs[hh].Size;
			var family = hhs[hh].Kids > 0;
			var student = IsStudentHousehold(hh);
			Property best = null;
			var bestScore = int.MinValue;
			for (var s = 0; s < info.SearchSamples; s++)
			{
				var p = homeCands[NextRandom(n)];
				if (p.Id == current || p.Households >= p.HouseholdSlots || !Alive(p))
					continue;

				var rent = RentCents(p);
				if (rent * 100 > income * info.RentToIncomeMax)
					continue;

				var quality = 50;
				var cb = p.Actor?.TraitOrDefault<CityBuilding>();
				if (cb != null)
				{
					if (!cb.HasPower || !cb.HasWater)
						continue;

					quality = cb.Happiness;
				}

				var fit = family == p.HouseholdSlots <= 3 ? 1 : 0;
				var crowd = (p.Residents + size) * 100 / Math.Max(1, p.HouseholdSlots * 3);

				// Student flats (low rent, ZON) go to students first; other households only take them when nothing else is free.
				var studentFlat = propReg != null && propReg.GetStudentHousing(p.Id) > 0 ? (student ? 40 : -25) : 0;
				var score = studentFlat + quality * 3 + p.LandValue - rent * 200 / income + fit * 20 - crowd / 10;
				if (score > bestScore)
				{
					bestScore = score;
					best = p;
				}
			}

			return best;
		}

		/// <summary>Daily household update: payday (benefits, rent, shopping), debt, housing search, homelessness, happiness.</summary>
		void UpdateHousehold(int hh, int today)
		{
			ref var h = ref hhs[hh];
			var home = h.Home != 0 ? registry.Get(h.Home) : null;
			if (h.Home != 0 && home == null)
			{
				// Building gone (bulldozed / replaced): the household is out on the street.
				h.Home = 0;
				h.Flags |= HhFlags.Homeless;
				h.Rent = 0;
				h.SearchDays = 0;
			}

			if (h.Size == 0)
			{
				DissolveHousehold(hh);
				return;
			}

			if (home != null && !home.Operational && h.MovedInDays > 1)
				Evict(hh);

			// Income that does not come from a workplace.
			var benefits = 0;
			for (var m = h.FirstMember; m >= 0; m = cits[m].NextInHousehold)
			{
				var age = AgeOf(m);
				if (age > info.AdultMaxAge)
					benefits += info.PensionCents;
				else if (age <= info.TeenMaxAge)
					benefits += info.ChildAllowanceCents;
				else if ((cits[m].Flags & (CitFlags.Worker | CitFlags.Student | CitFlags.Sick)) == 0 && age >= info.WorkAge)
					benefits += info.UnemploymentBenefitCents;
			}

			h.Cash += benefits;
			benefitsPaidCents += benefits;

			if (home != null)
			{
				h.Cash -= h.Rent;
				rentPaidCents += h.Rent;
				rentToReport += h.Rent;
				if (rentToReport >= 100)
				{
					registry.ReportRentPaid(home.Id, rentToReport / 100);
					rentToReport %= 100;
				}
			}

			Shop(hh);
			h.MovedInDays = (byte)Math.Min(255, h.MovedInDays + 1);

			if (h.Cash < -info.DebtLimitCents)
			{
				h.DebtDays++;
				if (h.DebtDays >= info.DebtDaysToEvict && home != null)
				{
					Evict(hh);
					h.DebtDays = 0;
					h.Cash = 0;
					home = null;
				}
			}
			else
				h.DebtDays = 0;

			if (home == null)
			{
				SearchHome(hh);
				return;
			}

			UpdateHouseholdEnv(hh, home);

			// BALANCE: a household whose adults have all been jobless for months moves on (the job market pulls people in, and pushes them out).
			if (info.JoblessMonthsToEmigrate > 0 && JoblessHousehold(hh) && Hash(hh, today, 73) % 100 < info.JoblessEmigratePercent)
			{
				Emigrate(hh);
				return;
			}

			// Unhappy households look for something better.
			if (h.Happiness < 25 && Hash(hh, today, 71) % 100 < 20)
				h.Flags |= HhFlags.WantsMove;

			if ((h.Flags & HhFlags.WantsMove) != 0)
			{
				var better = ChooseHome(hh, h.Home);
				if (better != null && (better.Actor?.TraitOrDefault<CityBuilding>()?.Happiness ?? 50) > h.Happiness + 15)
				{
					Evict(hh);
					MoveIn(hh, better);
				}
				else if (Hash(hh, today, 72) % 100 < 30)
					h.Flags &= ~HhFlags.WantsMove;
			}
		}

		bool JoblessHousehold(int hh)
		{
			var adults = 0;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
			{
				var age = AgeOf(m);
				if (age < info.WorkAge || age > info.AdultMaxAge)
					continue;

				if ((cits[m].Flags & (CitFlags.Worker | CitFlags.Student)) != 0 || cits[m].UnemployedDays < info.JoblessMonthsToEmigrate)
					return false;

				adults++;
			}

			return adults > 0;
		}

		void SearchHome(int hh)
		{
			ref var h = ref hhs[hh];
			h.SearchDays++;
			var p = ChooseHome(hh, -1);
			if (p != null)
			{
				MoveIn(hh, p);
				return;
			}

			if ((h.Flags & HhFlags.Homeless) != 0)
			{
				h.HomelessDays++;
				h.Cash = Math.Max(h.Cash, -info.DebtLimitCents);
				if (h.HomelessDays >= info.HomelessDaysToEmigrate)
					Emigrate(hh);

				return;
			}

			if (h.SearchDays >= info.GiveUpDays)
				h.Flags |= HhFlags.Homeless;
		}
	}
}
