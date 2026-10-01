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

namespace OpenRA.Mods.City.Traits
{
	// The daily simulation step. Everything is integer maths over buildings in ActorID order, so it is deterministic.
	public partial class CityManager
	{
		readonly int[] jobSlots = new int[5];
		readonly int[] demandTarget = new int[5];
		RoadLayer roads;
		CityCoverageLayer coverage;
		ZoneLayer zoneLayer;
		bool worldTraitsCached;
		int lastRoadVersion = -1;
		bool demandInitialized;

		static int Hash(uint a, int b)
		{
			unchecked
			{
				var h = a * 2654435761u ^ (uint)b * 2246822519u;
				h ^= h >> 15;
				h *= 2654435761u;
				h ^= h >> 13;
				return (int)(h & 0x7fffffff);
			}
		}

		CityClock clock;
		int lastClockDay;

		T FindProvider<T>() where T : class
		{
			return self.World.WorldActor.TraitsImplementing<T>().FirstOrDefault() ?? self.TraitsImplementing<T>().FirstOrDefault();
		}

		void DailyUpdate()
		{
			if (!worldTraitsCached)
			{
				worldTraitsCached = true;
				economy ??= self.TraitOrDefault<CityEconomy>();
				citizens = FindProvider<ICitizenPopulation>();
				demandModel = FindProvider<IDemandModel>();
				progression = FindProvider<IProgression>();
				utilities = FindProvider<IUtilityNetwork>();
				propertyRegistry = FindProvider<IPropertyRegistry>();
				roads = self.World.WorldActor.TraitOrDefault<RoadLayer>();
				coverage = self.World.WorldActor.TraitOrDefault<CityCoverageLayer>();
				zoneLayer = self.World.WorldActor.TraitOrDefault<ZoneLayer>();
				clock = self.World.WorldActor.TraitOrDefault<CityClock>();
				lastClockDay = clock?.DayIndex ?? 0;
			}

			// TotalDays counts aggregate pulses (the legacy "day"); calendar months come from CityClock when present.
			TotalDays++;

			RefreshRoadAccess();
			if (utilities == null)
				DistributeUtilities();
			else
				MirrorUtilities();

			SimulateBuildings();
			FillJobsAndCollect();

			// Legacy demand also drives ResidentialAttraction (legacy move-in): skip only when both providers replace it.
			if (demandModel == null || citizens == null)
				UpdateDemand();

			CheckMilestones();

			if (clock != null)
			{
				var day = clock.DayIndex;
				if (day != lastClockDay)
				{
					lastClockDay = day;
					EndMonth();
				}
			}
			else if (TotalDays % Math.Max(1, Info.DaysPerMonth) == 0)
				EndMonth();

			if (Funds < 0 && !bankruptNotified)
			{
				bankruptNotified = true;
				Notify("notification-city-bankrupt");
			}
			else if (Funds >= 0)
				bankruptNotified = false;
		}

		// Road access only changes when the road network does (or a building appears).
		void RefreshRoadAccess()
		{
			var version = roads?.NetworkVersion ?? 0;
			var all = version != lastRoadVersion;
			lastRoadVersion = version;
			for (var i = 0; i < buildings.Count; i++)
			{
				var b = buildings[i];
				if (all || b.RoadAccessDirty)
				{
					// Growables use the zone depth (lots a few rows behind a street can grow); services need a 4-adjacent road.
					b.HasRoadAccess = roads == null || (b.Growable != null && zoneLayer != null
						? roads.HasRoadAccessWithin(b.Cells[0], zoneLayer.Info.ZoneDepth)
						: roads.HasRoadAccess(b.Cells));
					b.RoadAccessDirty = false;
				}
			}
		}

		// Occupancy in percent: residents / capacity for housing, filled / total jobs for workplaces, 100 otherwise.
		int Occupancy(CityBuilding b)
		{
			if (b.Category == ZoneCategory.Residential)
				return Math.Min(100, b.Residents * 100 / Math.Max(1, b.Info.MaxResidents));

			if (b.Info.MaxJobs > 0)
				return Math.Min(100, b.Workers * 100 / Math.Max(1, AdjustedJobs(b.Category, b.Info.MaxJobs)));

			return 100;
		}

		// Consumption scales from 25% (empty) to 100% (full), rounded up.
		static int Use(int baseUse, int occupancyPercent)
		{
			return baseUse <= 0 ? 0 : Math.Max(1, (baseUse * (25 + 75 * occupancyPercent / 100) + 99) / 100);
		}

		// An IUtilityNetwork writes HasPower/HasWater itself; we only keep the operational flag and the totals.
		void MirrorUtilities()
		{
			for (var i = 0; i < buildings.Count; i++)
				buildings[i].OperationalToday = buildings[i].IsOperational;

			PowerProduced = utilities.PowerProduced;
			PowerConsumed = utilities.PowerConsumed;
			WaterProduced = utilities.WaterProduced;
			WaterConsumed = utilities.WaterConsumed;
		}

		// Producers with road access that are operational fill a city-wide pool. Consumers are served in ActorID order:
		// a building is supplied if the remaining pool covers its full use. Only operational buildings draw from the pool;
		// inoperable ones (under construction / abandoned) just report whether supply would be available.
		void DistributeUtilities()
		{
			var power = 0;
			var water = 0;
			for (var i = 0; i < buildings.Count; i++)
			{
				var b = buildings[i];
				b.OperationalToday = b.IsOperational;
				if (b.Producer != null && b.OperationalToday && b.HasRoadAccess)
				{
					power += b.Producer.Info.Power;
					water += b.Producer.Info.Water;
				}
			}

			PowerProduced = power;
			WaterProduced = water;
			var powerLeft = power;
			var waterLeft = water;
			var powerWanted = 0;
			var waterWanted = 0;
			for (var i = 0; i < buildings.Count; i++)
			{
				var b = buildings[i];
				var op = b.OperationalToday;
				var occupancy = Occupancy(b);
				var pu = Use(b.Info.PowerUse, occupancy);
				var wu = Use(b.Info.WaterUse, occupancy);
				if (pu <= 0)
					b.HasPower = true;
				else
				{
					b.HasPower = powerLeft >= pu;
					if (op)
					{
						powerWanted += pu;
						if (b.HasPower)
							powerLeft -= pu;
					}
				}

				if (wu <= 0)
					b.HasWater = true;
				else
				{
					b.HasWater = waterLeft >= wu;
					if (op)
					{
						waterWanted += wu;
						if (b.HasWater)
							waterLeft -= wu;
					}
				}
			}

			PowerConsumed = powerWanted;
			WaterConsumed = waterWanted;
		}

		// Residents, workforce, happiness and land value per building. With a citizen simulation the counts come from the
		// property registry (mirrored into CityBuilding for the UI) and the legacy occupancy model is skipped.
		void SimulateBuildings()
		{
			var pop = 0;
			var capacity = 0;
			var workforce = 0;
			var jobsOp = 0;
			long happyWeighted = 0;
			long eduWeighted = 0;
			Array.Clear(jobSlots);
			var unemployment = UnemploymentRate;
			var wfPercent = Info.WorkforcePercent;

			for (var i = 0; i < buildings.Count; i++)
			{
				var b = buildings[i];
				var info = b.Info;
				var cat = b.Category;
				var cell = b.Cells.Length > 0 ? b.Cells[0] : default;
				var prop = citizens != null && b.Cells.Length > 0 ? propertyRegistry?.GetAt(cell) : null;

				b.LandValue = coverage != null ? coverage.GetLandValue(cell) : 30;
				UpdateHappiness(b, cell, cat, unemployment);

				if (cat == ZoneCategory.Residential)
				{
					if (citizens == null)
					{
						UpdateResidents(b, unemployment);
						b.Workers = (b.Residents * wfPercent + 50) / 100;
					}
					else
					{
						b.Residents = prop?.Residents ?? 0;
						b.Workers = 0;
					}

					pop += b.Residents;
					workforce += b.Workers;
					if (b.NotAbandoned)
						capacity += info.MaxResidents;

					happyWeighted += (long)b.Happiness * b.Residents;
					if (coverage != null)
						eduWeighted += (long)coverage.GetCoverage(CityService.Education, cell) * b.Residents;
				}
				else if (info.MaxJobs > 0)
				{
					var slots = prop != null ? prop.TotalJobSlots : AdjustedJobs(cat, info.MaxJobs);
					if (b.NotAbandoned)
						jobSlots[(int)cat] += slots;

					if (b.OperationalToday)
					{
						jobsOp += slots;
						if (prop != null)
							b.Workers = prop.TotalJobsFilled;
					}
					else
						b.Workers = 0;
				}
			}

			HousingCapacity = capacity;
			if (citizens != null)
			{
				Population = citizens.Population;
				Workers = citizens.Workers;
				Jobs = jobsOp;
				AverageHappiness = Population > 0 ? citizens.AverageHappiness : 50;
				var educated = citizens.CountByEducation(EducationLevel.Educated) + citizens.CountByEducation(EducationLevel.Well)
					+ citizens.CountByEducation(EducationLevel.Highly);
				EducatedRatio = Population > 0 ? educated * 100 / Population : 0;
				Unemployed = citizens.Unemployed;
				return;
			}

			Population = pop;
			Workers = workforce;
			Jobs = jobsOp;
			AverageHappiness = pop > 0 ? (int)(happyWeighted / pop) : 50;
			EducatedRatio = pop > 0 ? (int)(eduWeighted / pop) : 0;
			Unemployed = Math.Max(0, workforce - jobsOp);
		}

		void UpdateHappiness(CityBuilding b, CPos cell, ZoneCategory cat, int unemployment)
		{
			var t = 55;
			if (!b.HasPower)
				t -= 30;
			if (!b.HasWater)
				t -= 25;
			if (!b.HasRoadAccess)
				t -= 20;

			if (coverage != null)
			{
				t += coverage.GetCoverage(CityService.Police, cell) * 12 / 100;
				t += coverage.GetCoverage(CityService.Fire, cell) * 10 / 100;
				t += coverage.GetCoverage(CityService.Health, cell) * 12 / 100;
				t += coverage.GetCoverage(CityService.Parks, cell) * 15 / 100;
				if (cat != ZoneCategory.Industrial)
					t -= coverage.GetPollution(cell) * 30 / 100;
				if (cat == ZoneCategory.Residential)
					t += coverage.GetCoverage(CityService.Education, cell) * 8 / 100;
			}

			t += b.LandValue * 10 / 100;

			if (cat != ZoneCategory.None)
			{
				var rate = taxRates[(int)cat];
				t += rate > 10 ? -(rate - 10) : Math.Min(5, (10 - rate) / 2);
			}

			if (cat == ZoneCategory.Residential)
				t -= Math.Min(12, unemployment / 3);

			t = Math.Clamp(t, 0, 100);
			b.Happiness += Math.Clamp(t - b.Happiness, -3, 3);
		}

		// Occupancy dynamics: move in while attraction is positive (about +1..+step per building every few days),
		// move out when unhappy, unserved or abandoned.
		void UpdateResidents(CityBuilding b, int unemployment)
		{
			var max = b.Info.MaxResidents;
			var step = 1 + max / 24;
			if (!b.NotAbandoned)
			{
				b.Residents = Math.Max(0, b.Residents - 1 - max / 4);
				return;
			}

			if (!b.OperationalToday)
			{
				b.Residents = Math.Max(0, b.Residents - step);
				return;
			}

			var roll = Hash(b.SortId, TotalDays) % 100;
			if (!b.HasPower || !b.HasWater || !b.HasRoadAccess)
			{
				if (roll < 30)
					b.Residents = Math.Max(0, b.Residents - 1);
			}
			else if (b.Happiness < 25)
			{
				if (roll < 50)
					b.Residents = Math.Max(0, b.Residents - step);
			}
			else if (ResidentialAttraction < -40 && unemployment > 40)
			{
				if (roll < 15)
					b.Residents = Math.Max(0, b.Residents - 1);
			}
			else if (b.Residents < max && ResidentialAttraction > 0 && roll < Info.MoveInChance + ResidentialAttraction / 5)
				b.Residents = Math.Min(max, b.Residents + step);

			if (b.Residents > max)
				b.Residents = max;
		}
	}
}
