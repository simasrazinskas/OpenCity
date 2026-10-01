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
	public enum XpSource : byte { Population, Workers, Roads, Happiness, Placement, Achievement, Other }

	public struct XpSourceEntry
	{
		/// <summary>Fluent key 'xp-source-&lt;name&gt;'.</summary>
		public string NameKey;
		public XpSource Source;
		public int Xp;
	}

	public struct MilestoneHistoryEntry
	{
		public int Index;
		public string Name;
		public int Tick;

		/// <summary>CityClock day (= month) index when it was reached, -1 without a clock.</summary>
		public int Month;
		public int Xp;
	}

	// Statistics the UI and achievements read: XP sources, milestone history, the stat snapshot and monthly series.
	public partial class Progression
	{
		readonly int[] xpBySource = new int[Enum.GetValues<XpSource>().Length];
		int[] milestoneTicks = [];
		int[] milestoneMonths = [];
		int[] milestoneXp = [];
		IPropertyRegistry registry;
		ICityEconomy economy;
		ITransitUiSource transitUi;

		void InitStats()
		{
			milestoneTicks = new int[milestones.Count];
			milestoneMonths = new int[milestones.Count];
			milestoneXp = new int[milestones.Count];
			for (var i = 1; i < milestones.Count; i++)
				milestoneTicks[i] = milestoneMonths[i] = -1;
		}

		void ResolveStats()
		{
			registry = ZoningLookup.Find<IPropertyRegistry>(world);
			economy = ZoningLookup.Find<ICityEconomy>(world);
			transitUi = ZoningLookup.Find<ITransitUiSource>(world);
		}

		/// <summary>Where the XP came from, in enum order (population, workers, roads, happiness, placement, achievement, other).</summary>
		public IReadOnlyList<XpSourceEntry> XpSources
		{
			get
			{
				var list = new List<XpSourceEntry>(xpBySource.Length);
				foreach (var s in Enum.GetValues<XpSource>())
					list.Add(new XpSourceEntry { NameKey = "xp-source-" + s.ToString().ToLowerInvariant(), Source = s, Xp = xpBySource[(int)s] });

				return list;
			}
		}

		/// <summary>Milestones reached so far, oldest first.</summary>
		public IReadOnlyList<MilestoneHistoryEntry> MilestoneHistory
		{
			get
			{
				var list = new List<MilestoneHistoryEntry>();
				for (var i = 1; i <= MilestoneIndex && i < milestones.Count; i++)
					list.Add(new MilestoneHistoryEntry { Index = i, Name = milestones[i].Name, Tick = milestoneTicks[i], Month = milestoneMonths[i], Xp = milestoneXp[i] });

				return list;
			}
		}

		void OnMilestoneReached(int index)
		{
			milestoneTicks[index] = world.WorldTick;
			milestoneMonths[index] = clock?.DayIndex ?? -1;
			milestoneXp[index] = Xp;
		}

		void OnMonthStats()
		{
			TouristVisits += citizens != null ? citizens.Tourists : VisitorGroupsPerMonth * 3;
			TouristSpendingLastMonth = TouristSpendingThisMonth;
			TouristSpendingThisMonth = 0;
			if (statistics == null)
				return;

			statistics.Record("xp", Xp);
			statistics.Record("milestone", MilestoneIndex);
			statistics.Record("dev-points", DevPoints);
			statistics.Record("attractiveness", Attractiveness);
			statistics.Record("tourism-spending", TouristSpendingLastMonth / 100);
			statistics.Record("tiles-owned", OwnedTileCount);
			statistics.Record("achievements", UnlockedAchievementCount);
		}

		void SetStat(string name, int value)
		{
			stats[Array.IndexOf(StatNames, name)] = value;
		}

		// Fills the stat snapshot used by achievements. Integer only; iteration in list order.
		void ComputeStats()
		{
			SetStat("population", CurrentPopulation);
			SetStat("happiness", citizens?.AverageHappiness ?? manager?.AverageHappiness ?? 50);
			SetStat("funds", manager?.Funds ?? 0);
			SetStat("monthly_balance", manager != null ? manager.LastMonthIncomeTotal - manager.LastMonthExpensesTotal : 0);
			SetStat("tiles_owned", OwnedTileCount);
			SetStat("city_policies", ActiveCityPolicyCount);
			SetStat("districts", DistrictCount);
			SetStat("districts_with_policies", DistrictsWithPolicies());
			SetStat("attractiveness", Attractiveness);
			SetStat("tourist_visits", TouristVisits);
			SetStat("milestone", MilestoneIndex);
			SetStat("nodes_owned", OwnedNodeCount);
			SetStat("roads", roadLayer?.RoadCellCount ?? 0);
			SetStat("achievements", UnlockedAchievementCount);
			SetStat("companies", economy?.CompanyCount ?? 0);

			var signaturesBuilt = 0;
			if (signatures != null)
				foreach (var sig in signatures.Signatures)
					if (signatures.IsBuilt(sig))
						signaturesBuilt++;

			SetStat("signatures", signaturesBuilt);

			var passengers = 0;
			if (transitUi != null)
				foreach (var line in transitUi.Lines)
					passengers += line.PassengersThisMonth;

			SetStat("transit_passengers", passengers);
			SetStat("service_categories", ServiceCategories());

			var edu = 0;
			if (citizens != null && citizens.Population > 0)
				edu = (citizens.CountByEducation(EducationLevel.Well) + citizens.CountByEducation(EducationLevel.Highly)) * 100 / citizens.Population;
			else if (manager != null)
				edu = manager.EducatedRatio;

			SetStat("educated_pct", edu);
			ComputeRegistryStats();
		}

		int DistrictsWithPolicies()
		{
			var n = 0;
			var count = policies.Count;
			for (var d = 1; d <= MaxDistricts && count > 0; d++)
			{
				if (!DistrictExists(d))
					continue;

				for (var pi = 0; pi < count; pi++)
				{
					if (policyValues[d * count + pi] > 0)
					{
						n++;
						break;
					}
				}
			}

			return n;
		}

		// Distinct service categories among the player's placed buildings (power, water, police, fire, health, ...).
		int ServiceCategories()
		{
			var seen = new List<string>();
			foreach (var a in world.ActorsHavingTrait<CityPlaceable>())
			{
				if (a.Owner != self.Owner || !a.IsInWorld)
					continue;

				var cat = a.Trait<CityPlaceable>().Info.Category;
				if (cat != "special" && cat != "industry" && !seen.Contains(cat))
					seen.Add(cat);
			}

			return seen.Count;
		}

		// Industrial job slots and an average pollution sample over the property registry (stride sampled).
		void ComputeRegistryStats()
		{
			var industrial = 0;
			long pollutionSum = 0;
			var samples = 0;
			if (registry != null)
			{
				var all = registry.All;
				var stride = Math.Max(1, all.Count / 64);
				for (var i = 0; i < all.Count; i++)
				{
					var p = all[i];
					if (p.Kind == PropertyKind.Industrial || p.Kind == PropertyKind.Warehouse)
						industrial += p.TotalJobSlots;

					if (pollution != null && i % stride == 0 && p.Kind == PropertyKind.Residential)
					{
						pollutionSum += Math.Max(pollution.GetAir(p.AccessCell), pollution.GetGround(p.AccessCell));
						samples++;
					}
				}
			}
			else if (manager != null)
				industrial = manager.GetJobSlots(ZoneCategory.Industrial);

			SetStat("industrial_jobs", industrial);
			SetStat("pollution", samples > 0 ? (int)(pollutionSum / samples) : 0);
		}
	}
}
