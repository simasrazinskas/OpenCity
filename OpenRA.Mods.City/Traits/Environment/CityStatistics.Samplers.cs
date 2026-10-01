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
using System.Globalization;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// Default series (sampled when a month closes) and the built-in chirper events.
	// Providers are looked up lazily and every one of them may be missing (legacy aggregate fallbacks).
	public partial class CityStatistics : IWorldLoaded
	{
		CityManager manager;
		CityEconomy economy;
		ServiceSimulation services;
		IDemandModel demand;
		IUtilityNetwork utilities;
		IPropertyRegistry properties;
		IRoadNetwork roadNetwork;
		TransitLayer transit;
		bool playerTraitsResolved;
		ICitizenPopulation citizens;
		ITrafficService traffic;
		PollutionLayer pollution;
		CityClimate climate;
		int exposureGround;
		int exposureAir;
		int exposureNoise;
		int landValue;
		int trafficLoad;
		int crime;
		int jobs;
		int lastBoardings;
		int transitPassengers;
		int lastMilestone = -1;
		int lastPopulationThousand;
		CityWeather lastWeather;

		CityManager Manager
		{
			get
			{
				ResolvePlayerTraits();
				return manager;
			}
		}

		// Player-owned providers (the first playable player's city), resolved lazily because player actors may not exist yet at WorldLoaded.
		void ResolvePlayerTraits()
		{
			if (playerTraitsResolved)
				return;

			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				playerTraitsResolved = true;
				var pa = p.PlayerActor;
				manager = pa.TraitOrDefault<CityManager>();
				economy = pa.TraitOrDefault<CityEconomy>();
				services = pa.TraitOrDefault<ServiceSimulation>();
				demand = pa.TraitsImplementing<IDemandModel>().FirstOrDefault() ?? world.WorldActor.TraitsImplementing<IDemandModel>().FirstOrDefault();
				citizens ??= pa.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
				break;
			}
		}

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			var wa = w.WorldActor;
			citizens = wa.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
			traffic = wa.TraitsImplementing<ITrafficService>().FirstOrDefault();
			utilities = wa.TraitsImplementing<IUtilityNetwork>().FirstOrDefault();
			properties = wa.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			roadNetwork = wa.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			transit = wa.TraitOrDefault<TransitLayer>();
			pollution = wa.TraitOrDefault<PollutionLayer>();
			climate = wa.TraitOrDefault<CityClimate>();
			clock ??= wa.TraitOrDefault<CityClock>();

			// Series ids are the ones the statistics panel charts (CityStatsLogic.Catalogue).
			// People and jobs (the citizen simulation takes over when it exists).
			Register("population", () => citizens?.Population ?? Manager?.Population ?? 0);
			Register("households", () => citizens?.Households ?? Manager?.Households ?? 0);
			Register("workers", () => citizens?.Workers ?? Manager?.Workers ?? 0);
			Register("unemployed", () => citizens?.Unemployed ?? Manager?.Unemployed ?? 0);
			Register("jobs", () => jobs);
			Register("students", () => citizens?.Students ?? 0);
			Register("tourists", () => citizens?.Tourists ?? 0);
			Register("buildings", () => Manager?.BuildingCount ?? 0);

			// Money.
			Register("money", () => Manager?.Funds ?? 0);
			Register("income", () => Manager?.LastMonthIncomeTotal ?? 0);
			Register("expenses", () => Manager?.LastMonthExpensesTotal ?? 0);
			Register("loan", () => Manager == null ? 0 : economy?.LoanPrincipal ?? 0);
			Register("tax.residential", () => TaxLastMonth(CityEconomy.LTaxIncome));
			Register("tax.commercial", () => TaxLastMonth(CityEconomy.LTaxCommercial));
			Register("tax.industrial", () => TaxLastMonth(CityEconomy.LTaxIndustrial));
			Register("tax.office", () => TaxLastMonth(CityEconomy.LTaxOffice));

			// Citizens.
			Register("happiness", () => citizens?.AverageHappiness ?? Manager?.AverageHappiness ?? 50);
			Register("health", () => citizens?.AverageHealth ?? 0);
			Register("landvalue", () => landValue);
			Register("crime", () => crime);

			// Demand (-100..100).
			Register("demand.r", () => demand?.GetDemand(ZoneCategory.Residential) ?? Manager?.DemandResidential ?? 0);
			Register("demand.c", () => demand?.GetDemand(ZoneCategory.Commercial) ?? Manager?.DemandCommercial ?? 0);
			Register("demand.i", () => demand?.GetDemand(ZoneCategory.Industrial) ?? Manager?.DemandIndustrial ?? 0);
			Register("demand.o", () => demand?.GetDemand(ZoneCategory.Office) ?? 0);

			// Utilities.
			Register("power.produced", () => utilities?.PowerProduced ?? Manager?.PowerProduced ?? 0);
			Register("power.used", () => utilities?.PowerConsumed ?? Manager?.PowerConsumed ?? 0);
			Register("water.produced", () => utilities?.WaterProduced ?? Manager?.WaterProduced ?? 0);
			Register("water.used", () => utilities?.WaterConsumed ?? Manager?.WaterConsumed ?? 0);

			// Environment.
			Register("pollution.ground", () => exposureGround);
			Register("pollution.air", () => exposureAir);
			Register("pollution.noise", () => exposureNoise);
			Register("temperature", () => climate?.TemperatureX10 ?? 0);
			Register("snow-depth", () => climate?.SnowDepth ?? 0);

			// Traffic and transit.
			Register("traffic.flow", () => traffic?.CityTrafficFlow ?? 100);
			Register("traffic.load", () => trafficLoad);
			Register("vehicles", () => traffic?.ActiveVehicles ?? 0);
			Register("transit.passengers", () => transitPassengers);
		}

		// Registered lazily on the first month close, when the player's economy exists: one produced/consumed pair per resource.
		bool resourceSeriesRegistered;

		void RegisterResourceSeries()
		{
			ResolvePlayerTraits();
			if (resourceSeriesRegistered || economy == null)
				return;

			resourceSeriesRegistered = true;
			for (var r = 1; r <= economy.ResourceCount; r++)
			{
				var id = r;
				var key = economy.ResourceName(id)?.ToLowerInvariant();
				if (key == null)
					continue;

				Register("res." + key + ".produced", () => (int)(economy.TradeProducedLast(id) / 1000));
				Register("res." + key + ".consumed", () => (int)(economy.TradeConsumedLast(id) / 1000));
			}
		}

		// Dollars taxed last month in one ledger category (the ledger counts cents).
		int TaxLastMonth(int category)
		{
			ResolvePlayerTraits();
			return economy == null ? 0 : (int)(economy.LedgerLastMonth(category) / 100);
		}

		// Resident-weighted pollution exposure and average land value over the city's buildings.
		void RefreshExposure()
		{
			ResolvePlayerTraits();
			long g = 0;
			long a = 0;
			long n = 0;
			long weight = 0;
			long lv = 0;
			long lvWeight = 0;
			long crimeSum = 0;
			var jobSlots = 0;

			if (properties != null)
			{
				// The property registry is the shared blackboard: residents, jobs and land value live there.
				var all = properties.All;
				for (var i = 0; i < all.Count; i++)
				{
					var p = all[i];
					lv += p.LandValue;
					lvWeight++;
					if (p.Operational)
						jobSlots += p.TotalJobSlots;

					if (services != null)
						crimeSum += services.GetCrimePressure(p.Id);

					if (p.Residents <= 0 || pollution == null)
						continue;

					pollution.Exposure(p.Origin, out var eg, out var ea, out var en);
					g += (long)eg * p.Residents;
					a += (long)ea * p.Residents;
					n += (long)en * p.Residents;
					weight += p.Residents;
				}

				crime = lvWeight > 0 ? (int)(crimeSum / lvWeight) : 0;
				jobs = jobSlots;
			}
			else
			{
				foreach (var pair in world.ActorsWithTrait<CityBuilding>())
				{
					var b = pair.Trait;
					if (b.Cells.Length == 0)
						continue;

					lv += b.LandValue;
					lvWeight++;

					var residents = b.Residents;
					if (residents <= 0 || pollution == null)
						continue;

					pollution.Exposure(b.Cells[0], out var eg, out var ea, out var en);
					g += (long)eg * residents;
					a += (long)ea * residents;
					n += (long)en * residents;
					weight += residents;
				}

				jobs = Manager?.Jobs ?? 0;
				crime = 0;
			}

			exposureGround = weight > 0 ? (int)(g / weight) : 0;
			exposureAir = weight > 0 ? (int)(a / weight) : 0;
			exposureNoise = weight > 0 ? (int)(n / weight) : 0;
			landValue = lvWeight > 0 ? (int)(lv / lvWeight) : 0;

			// Average congestion over the road network.
			long loadSum = 0;
			var roadCount = 0;
			if (traffic != null && roadNetwork != null)
			{
				foreach (var cell in roadNetwork.RoadCells)
				{
					loadSum += traffic.GetTrafficLoad(cell);
					roadCount++;
				}
			}

			trafficLoad = roadCount > 0 ? (int)(loadSum / roadCount) : 0;

			// Boardings in the month that just ended.
			if (transit != null)
			{
				transitPassengers = transit.TotalBoardings - lastBoardings;
				lastBoardings = transit.TotalBoardings;
			}
		}

		string Variant(string baseKey, int variants)
		{
			return baseKey + "-" + (1 + EnvHash.Hash(world.WorldTick, baseKey.Length, 9) % variants).ToString(CultureInfo.InvariantCulture);
		}

		// Cheap event detection every quarter hour.
		void DetectEvents()
		{
			var m = Manager;
			if (m != null)
			{
				if (lastMilestone < 0)
					lastMilestone = m.MilestoneIndex;

				if (m.MilestoneIndex > lastMilestone)
				{
					lastMilestone = m.MilestoneIndex;
					Post(Variant("chirp-milestone", 2), 0, m.MilestoneName, 20, CPos.Zero);
				}

				var thousand = m.Population / 1000;
				if (thousand > lastPopulationThousand)
				{
					lastPopulationThousand = thousand;
					Post(Variant("chirp-population", 2), 0, (thousand * 1000).ToString(CultureInfo.InvariantCulture), 10, CPos.Zero);
				}

				if (m.PowerConsumed > 0 && m.PowerProduced * 100 < m.PowerConsumed * 95)
				{
					var deficit = 100 - m.PowerProduced * 100 / m.PowerConsumed;
					Post(Variant("chirp-power-shortage", 2), 0, deficit.ToString(CultureInfo.InvariantCulture), Math.Max(1, m.BuildingCount * deficit / 100), CPos.Zero);
				}

				if (m.WaterConsumed > 0 && m.WaterProduced * 100 < m.WaterConsumed * 95)
				{
					var deficit = 100 - m.WaterProduced * 100 / m.WaterConsumed;
					Post(Variant("chirp-water-shortage", 2), 0, deficit.ToString(CultureInfo.InvariantCulture), Math.Max(1, m.BuildingCount * deficit / 100), CPos.Zero);
				}

				if (m.Funds < 0)
					Post(Variant("chirp-broke", 2), 0, null, 25, CPos.Zero);
			}

			if (climate != null)
			{
				var weather = climate.Weather;
				if (weather != lastWeather)
				{
					if (weather == CityWeather.Snow)
						Post(Variant("chirp-snow", 2), 0, null, 2, CPos.Zero);
					else if (weather == CityWeather.Storm)
						Post(Variant("chirp-storm", 2), 0, null, 2, CPos.Zero);

					lastWeather = weather;
				}

				if (climate.TemperatureX10 >= 300)
					Post(Variant("chirp-heat", 2), 0, (climate.TemperatureX10 / 10).ToString(CultureInfo.InvariantCulture), 2, CPos.Zero);
				else if (climate.TemperatureX10 <= -150)
					Post(Variant("chirp-cold", 2), 0, (climate.TemperatureX10 / 10).ToString(CultureInfo.InvariantCulture), 2, CPos.Zero);
			}
		}
	}
}
