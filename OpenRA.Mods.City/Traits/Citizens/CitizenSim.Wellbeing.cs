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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Happiness = 50 + sum of visible factors. Home-environment factors are computed per household once a day,
	// personal factors (health, employment, leisure) per citizen. Factor averages are exposed for the UI.
	public partial class CitizenSim
	{
		enum Factor
		{
			Power, Water, Access, Police, Fire, Health, Education, Parks, Pollution, LandValue, Taxes, Wealth, Crowding,
			Homeless, PersonalHealth, Employment, Leisure, Noise, Policies, Commute, Crime,
		}

		static readonly string[] FactorKeys =
		[
			"happiness-factor-power", "happiness-factor-water", "happiness-factor-roads", "happiness-factor-police",
			"happiness-factor-fire", "happiness-factor-healthcare", "happiness-factor-education", "happiness-factor-parks",
			"happiness-factor-pollution", "happiness-factor-landvalue", "happiness-factor-taxes", "happiness-factor-wealth",
			"happiness-factor-crowding", "happiness-factor-homeless", "happiness-factor-health", "happiness-factor-employment",
			"happiness-factor-leisure", "happiness-factor-noise", "happiness-factor-policies", "happiness-factor-commute", "happiness-factor-crime",
		];

		readonly long[] factorSum = new long[FactorKeys.Length];
		readonly int[] factorN = new int[FactorKeys.Length];
		DemandFactor[] factorView = [];
		IPollutionMap pollutionMap;
		bool pollutionLooked;

		public IReadOnlyList<DemandFactor> HappinessFactors => factorView;

		void AddFactor(Factor f, int value)
		{
			factorSum[(int)f] += value;
			factorN[(int)f]++;
		}

		void RotateFactors()
		{
			var list = new List<DemandFactor>(FactorKeys.Length);
			for (var i = 0; i < FactorKeys.Length; i++)
			{
				if (factorN[i] == 0)
					continue;

				var avg = (int)(factorSum[i] * 10 / factorN[i]);
				if (avg != 0)
					list.Add(new DemandFactor { Key = FactorKeys[i], Value = avg / 10 + (avg % 10 >= 5 ? 1 : 0) - (avg % 10 <= -5 ? 1 : 0) });

				factorSum[i] = 0;
				factorN[i] = 0;
			}

			factorView = list.ToArray();
		}

		int Satisfaction(ServiceKind kind, CityService legacy, CPos cell)
		{
			if (services != null)
				return services.GetSatisfaction(kind, cell);

			return coverage != null ? coverage.GetCoverage(legacy, cell) : 0;
		}

		void UpdateHouseholdEnv(int hh, Property home)
		{
			if (!pollutionLooked)
			{
				pollutionLooked = true;
				pollutionMap = world.WorldActor.TraitsImplementing<IPollutionMap>().FirstOrDefault();
			}

			ref var h = ref hhs[hh];
			var cell = home.AccessCell;
			var cb = home.Actor?.TraitOrDefault<CityBuilding>();
			var power = cb == null || cb.HasPower ? 0 : -30;
			var water = cb == null || cb.HasWater ? 0 : -25;
			var access = home.HasRoadAccess ? 0 : -20;
			var police = Satisfaction(ServiceKind.Police, CityService.Police, cell) * 12 / 100;
			var fire = Satisfaction(ServiceKind.Fire, CityService.Fire, cell) * 10 / 100;
			var healthSat = Satisfaction(ServiceKind.Health, CityService.Health, cell);
			var parks = Satisfaction(ServiceKind.Parks, CityService.Parks, cell) * 15 / 100;
			var edu = Satisfaction(ServiceKind.Education, CityService.Education, cell) * (h.Kids > 0 ? 16 : 8) / 100;
			var pollution = 0;
			var noise = 0;
			if (pollutionLayer != null)
			{
				pollutionLayer.Exposure(cell, out var ground, out var air, out var noiseValue);
				pollution = -Math.Max(ground, air) * 30 / 100;
				noise = -noiseValue * 15 / 100;
			}
			else if (pollutionMap != null)
				pollution = -Math.Max(pollutionMap.GetGround(cell), pollutionMap.GetAir(cell)) * 30 / 100;
			else if (coverage != null)
				pollution = -coverage.GetPollution(cell) * 30 / 100;

			var landValue = (cb?.LandValue ?? home.LandValue) * 10 / 100;
			var rate = IncomeTaxPercent;
			var taxes = rate > 10 ? -(rate - 10) : Math.Min(5, (10 - rate) / 2);
			var wealth = h.Cash < 0 ? -10 : (h.Cash > h.Rent * 10 ? 8 : 0);
			var crowd = h.Size > Math.Max(1, home.HouseholdSlots) * 4 ? -8 : 0;

			var policies = progression != null ? progression.GetPolicy("HappinessFlat", cell) : 0;

			var crime = -CrimeHeatPoints(home.Id) * info.CrimeVictimWellbeing / 3;

			h.EnvScore = crime + noise + policies + power + water + access + police + fire + healthSat * 12 / 100 + edu
				+ parks + pollution + landValue + taxes + wealth + crowd;
			h.HealthCov = (byte)Math.Clamp(healthSat, 0, 100);
			h.HealthMod = (short)(healthSat * 20 / 100 + pollution * 2 + noise);

			AddFactor(Factor.Power, power);
			AddFactor(Factor.Water, water);
			AddFactor(Factor.Access, access);
			AddFactor(Factor.Police, police);
			AddFactor(Factor.Fire, fire);
			AddFactor(Factor.Health, healthSat * 12 / 100);
			AddFactor(Factor.Education, edu);
			AddFactor(Factor.Parks, parks);
			AddFactor(Factor.Pollution, pollution);
			AddFactor(Factor.LandValue, landValue);
			AddFactor(Factor.Taxes, taxes);
			AddFactor(Factor.Wealth, wealth);
			AddFactor(Factor.Crowding, crowd);
			AddFactor(Factor.Noise, noise);
			AddFactor(Factor.Crime, crime);
			AddFactor(Factor.Policies, policies);

			// Household happiness: mean of the members' wellbeing.
			var sum = 0;
			var n = 0;
			for (var m = h.FirstMember; m >= 0; m = cits[m].NextInHousehold)
			{
				sum += cits[m].Wellbeing;
				n++;
			}

			h.Happiness = (byte)(n > 0 ? sum / n : 50);
		}

		void UpdateWellbeing(int ci, int age, int today)
		{
			var hh = cits[ci].Household;
			var c = cits[ci];
			var env = hh >= 0 ? hhs[hh].EnvScore : 0;
			var homeless = hh >= 0 && hhs[hh].Home == 0;
			if (homeless)
			{
				env = -60;
				AddFactor(Factor.Homeless, -60);
			}

			var health = (c.Health - 170) / 10;
			var work = 0;
			if (age >= info.WorkAge && age <= info.AdultMaxAge && (c.Flags & CitFlags.Student) == 0)
				work = (c.Flags & CitFlags.Worker) != 0 ? 3 : -8;

			if ((c.Flags & CitFlags.Sick) != 0)
				health -= 10;

			// Leisure counter grows daily; it is reset by a leisure visit (trip arrival, or abstractly when no traffic service exists).
			var leisure = c.Leisure < 150 ? 0 : -6;
			AddFactor(Factor.PersonalHealth, health);
			AddFactor(Factor.Employment, work);
			AddFactor(Factor.Leisure, leisure);

			var commute = c.Commute > 0 ? -CommutePenalty(ci) : 0;
			if (commute != 0)
				AddFactor(Factor.Commute, commute);

			var target = Math.Clamp(50 + env + health + work + leisure + commute, 0, 100);
			var w = c.Wellbeing + Math.Clamp(target - c.Wellbeing, -12, 12);
			cits[ci].Wellbeing = (byte)w;
			cits[ci].Leisure = (byte)Math.Min(255, c.Leisure + 30 + Hash(ci, today, 60) % 20);
			if (!UseTrips && cits[ci].Leisure >= 130 && (cits[ci].Flags & CitFlags.Sick) == 0)
				cits[ci].Leisure = 0;
		}
	}
}
