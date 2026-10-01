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
	// The daily citizen update: ageing, health, sickness, death, birth, leaving home.
	public partial class CitizenSim
	{
		int births, deaths, sicknessCases;
		int birthsMonth, deathsMonth;

		void UpdateCitizen(int ci, int today)
		{
			var age = AgeOf(ci);

			CheckWork(ci);

			if (RollDeath(ci, age, today))
			{
				Die(ci);
				return;
			}

			UpdateHealth(ci, age, today);

			if ((cits[ci].Flags & CitFlags.Jailed) != 0)
			{
				ServeSentence(ci);
				return;
			}

			if (age > info.AdultMaxAge && (cits[ci].Flags & CitFlags.Retired) == 0)
				Retire(ci);

			UpdateSchooling(ci, age, today);
			UpdateWork(ci, age, today);
			UpdateWellbeing(ci, age, today);
			UpdateCrime(ci, age, today);

			if (cits[ci].Cooldown > 0)
				cits[ci].Cooldown--;

			TryLeaveHome(ci, age, today);
			TryBirth(ci, age, today);
			PlanDay(ci, age, today);
		}

		static int HazardBand(int age)
		{
			if (age < 50)
				return 0;
			if (age < 65)
				return 1;
			if (age < 75)
				return 2;
			if (age < 85)
				return 3;
			return age < 95 ? 4 : 5;
		}

		bool RollDeath(int ci, int age, int today)
		{
			var health = cits[ci].Health;
			var ppm = info.DeathPPMByAgeBand[HazardBand(age)] * (50 + (255 - health) * 150 / 255) / 100 / Math.Max(1, info.DaysPerCitizenYear);
			if ((cits[ci].Flags & CitFlags.Sick) != 0)
				ppm += 40000 + (255 - health) * (255 - health) / 2;

			return Hash(ci, today, 5) % 1000000 < ppm;
		}

		void Die(int ci)
		{
			var hh = cits[ci].Household;
			var home = hh >= 0 ? hhs[hh].Home : 0;
			deaths++;
			deathsMonth++;
			Announce("chirp-citizen-died", ci, 2, AgeOf(ci) > info.AdultMaxAge ? 35 : 100);
			services?.ReportDeath(ci + 1, home);
			CancelActiveTrip(ci);

			KillCitizen(ci);
		}

		void Retire(int ci)
		{
			ReleaseWork(ci);
			cits[ci].Flags |= CitFlags.Retired;
		}

		void UpdateHealth(int ci, int age, int today)
		{
			var hh = cits[ci].Household;
			var sick = (cits[ci].Flags & CitFlags.Sick) != 0;
			var target = 235 - Math.Max(0, age - 60) * 5;
			if (hh >= 0)
				target += hhs[hh].HealthMod;

			if (sick)
				target -= 100;

			target = Math.Clamp(target, 0, 255);
			var health = cits[ci].Health;
			health = (byte)Math.Clamp(health + Math.Clamp(target - health, -40, 40), 0, 255);
			cits[ci].Health = health;

			if (sick)
			{
				var chance = info.RecoverPercent + ((cits[ci].Flags & CitFlags.Treated) != 0 ? info.TreatedRecoverBonus : 0);
				if (Hash(ci, today, 6) % 100 < chance)
				{
					cits[ci].Flags &= ~(CitFlags.Sick | CitFlags.Treated);
					cits[ci].Health = (byte)Math.Min(255, health + 40);
				}
				else if (hh >= 0 && (cits[ci].Flags & CitFlags.Treated) == 0 && TryTreat(ci, hh))
					cits[ci].Flags |= CitFlags.Treated;

				return;
			}

			var ppm = (long)info.SickPPM * (320 - health) / 160 / Math.Max(1, info.DaysPerCitizenYear);
			if (Hash(ci, today, 7) % 1000000 < ppm)
			{
				cits[ci].Flags |= CitFlags.Sick;
				sicknessCases++;
				if (hh >= 0 && TryTreat(ci, hh))
					cits[ci].Flags |= CitFlags.Treated;
			}
		}

		bool TryTreat(int ci, int hh)
		{
			if (services != null)
				return services.RequestTreatment(ci + 1, hhs[hh].Home);

			// Fallback without a services provider: treated if the home is covered by a clinic.
			return hhs[hh].HealthCov >= 40;
		}

		/// <summary>Teens that turn adult may leave their parents' household (and look for their own home).</summary>
		void TryLeaveHome(int ci, int age, int today)
		{
			if (age != info.TeenMaxAge + 1)
				return;

			var hh = cits[ci].Household;
			if (hh < 0 || hhs[hh].Size < 2 || hhs[hh].Home == 0 || Hash(ci, today, 8) % 100 >= 40)
				return;

			var adults = 0;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
				if (m != ci && AgeOf(m) > info.TeenMaxAge)
					adults++;

			if (adults == 0)
				return;

			var home = registry.Get(hhs[hh].Home);
			if (home != null)
				home.Residents = Math.Max(0, home.Residents - 1);

			RemoveMember(hh, ci);
			var give = Math.Max(0, Math.Min(hhs[hh].Cash / 4, 4000));
			hhs[hh].Cash -= give;

			var nh = AllocHousehold();
			hhs[nh].Type = (cits[ci].Flags & CitFlags.Student) != 0 ? HhType.Student : HhType.Single;
			hhs[nh].Cash = give + info.StartCashCents / 2;
			AddMember(nh, ci);
		}

		void TryBirth(int ci, int age, int today)
		{
			var c = cits[ci];
			if ((c.Flags & CitFlags.Male) != 0 || age < 18 || age > 44 || c.Cooldown > 0 || c.Household < 0)
				return;

			var hh = c.Household;
			ref var h = ref hhs[hh];
			if (h.Home == 0 || h.Kids >= info.MaxKids || h.Happiness < 40 || h.Cash <= 0 || h.Size >= 8)
				return;

			if (h.Type != HhType.Couple && h.Type != HhType.Family && h.Type != HhType.Single)
				return;

			if (h.Type == HhType.Single && Hash(ci, today, 9) % 3 != 0)
				return;

			if (Hash(ci, today, 10) % 1000000 >= info.BirthPPM / Math.Max(1, info.DaysPerCitizenYear))
				return;

			var p = registry.Get(h.Home);
			if (p == null)
				return;

			NewCitizen(hh, 0, 0, Hash(ci, today, 12) % 2 == 0);
			p.Residents++;
			cits[ci].Cooldown = 2;
			births++;
			birthsMonth++;
			Announce("chirp-citizen-born", ci, 2, 35);
			if (h.Type == HhType.Single)
				h.Type = HhType.Family;
		}

		void RotateDaily()
		{
			Births = birthsMonth;
			Deaths = deathsMonth;
			ImmigrantsLastMonth = immigrants - immigrantsBase;
			EmigrantsLastMonth = emigrants - emigrantsBase;
			immigrantsBase = immigrants;
			emigrantsBase = emigrants;
			birthsMonth = 0;
			deathsMonth = 0;
			CrimesLastMonth = crimesMonth;
			ArrestsLastMonth = arrestsMonth;
			crimesMonth = 0;
			arrestsMonth = 0;
			chirpsToday = 0;
			if (tripsAccepted == 0 && Today % 10 == 0)
				tripsRefused = Math.Min(tripsRefused, 1900);

			PayCommuters();
			RotateFactors();
			ComputeImmigrationQuota();
			UpdateTourists();
		}

		int immigrantsBase, emigrantsBase;
	}
}
