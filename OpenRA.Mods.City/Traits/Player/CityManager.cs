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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("The city economy: funds, calendar, population/jobs, demand, taxes, utilities balance, milestones.")]
	public class CityManagerInfo : TraitInfo
	{
		public readonly int StartingFunds = 70000;

		[Desc("World ticks per in-game day at normal speed.")]
		public readonly int TicksPerDay = 25;

		public readonly int DaysPerMonth = 30;

		public readonly int StartYear = 2026;

		public readonly int DefaultTaxRate = 10;

		[Desc("World.Timestep (ms) for speed 1, 2 and 3.")]
		public readonly int[] SpeedTimesteps = [40, 20, 10];

		[Desc("Share of residents (percent) that are working-age.")]
		public readonly int WorkforcePercent = 55;

		[Desc("Monthly tax per resident / employed worker in tenths of a dollar, at a 10% tax rate.",
			"Scaled linearly by the tax rate, +25% per building level above 1 and 70..130% by land value.")]
		public readonly int ResidentialTaxBase = 15;

		public readonly int CommercialTaxBase = 38;

		public readonly int IndustrialTaxBase = 30;

		public readonly int OfficeTaxBase = 60;

		[Desc("Percent chance per day that a building with free space gains new residents (before the demand bonus).")]
		public readonly int MoveInChance = 12;

		[Desc("Population needed to unlock office demand without education coverage.")]
		public readonly int OfficePopulationThreshold = 800;

		[Desc("Maximum change of a demand value per day when rising / falling.")]
		public readonly int DemandRiseStep = 3;

		public readonly int DemandFallStep = 6;

		[Desc("Percent applied to monthly city amounts (taxes, road and service upkeep). A CityClock month is 2,400 ticks,",
			"3.2 times the legacy 750-tick month, so this keeps money flowing at the old real-time rate.")]
		public readonly int MonthlyScalePercent = 320;

		[Desc("Percent applied to road and service upkeep (own scale: income comes from the economy, upkeep is a fixed price list).")]
		public readonly int UpkeepScalePercent = 250;

		[Desc("Percent of the commercial buildings' MaxJobs that become jobs (about one commercial job per 8 residents).")]
		public readonly int CommercialJobPercent = 50;

		public override object Create(ActorInitializer init) { return new CityManager(init.Self, this); }
	}

	public partial class CityManager : IResolveOrder, ITick, ISync
	{
		/// <summary>Recent city message (milestones, bankruptcy). Text is a fluent key.</summary>
		public struct Notification
		{
			public int Day;
			public string Text;
		}

		public static readonly int[] MilestonePopulation = [0, 250, 1000, 2500, 5000, 10000, 25000, 50000];
		public static readonly int[] MilestoneBonus = [0, 5000, 10000, 20000, 40000, 60000, 100000, 200000];
		public static readonly string[] MilestoneNames = ["Village", "Hamlet", "Small Town", "Town", "Large Town", "Small City", "City", "Metropolis"];

		public readonly CityManagerInfo Info;
		readonly Actor self;
		readonly List<Notification> notifications = [];
		readonly int[] taxRates = new int[5];
		readonly int[] demand = new int[5];
		readonly Dictionary<string, int> incomeThisMonth = [];
		readonly Dictionary<string, int> expensesThisMonth = [];
		readonly Dictionary<string, int> unlockCache = [];
		Dictionary<string, int> incomeLastMonth = [];
		Dictionary<string, int> expensesLastMonth = [];
		int ticksInDay;
		bool bankruptNotified;

		// Optional providers of the CS2 simulation (null = legacy aggregate behaviour for that part).
		CityEconomy economy;
		ICitizenPopulation citizens;
		IDemandModel demandModel;
		IProgression progression;
		IUtilityNetwork utilities;
		IPropertyRegistry propertyRegistry;

		public CityManager(Actor self, CityManagerInfo info)
		{
			this.self = self;
			Info = info;
			Funds = info.StartingFunds;
			for (var i = 0; i < taxRates.Length; i++)
				taxRates[i] = info.DefaultTaxRate;

			// Sensible values before the first daily simulation step (like a fresh CS city).
			demand[(int)ZoneCategory.Residential] = 80;
			demand[(int)ZoneCategory.Industrial] = 40;
			ResidentialAttraction = 80;
		}

		// ---- money ----
		[VerifySync]
		public int Funds { get; private set; }

		public bool CanAfford(int amount) { return Funds >= amount; }

		/// <summary>Deducts cost if affordable and records it under an expense category ("construction", ...). Returns false if not affordable.</summary>
		public bool TrySpend(int amount, string category)
		{
			if (amount <= 0)
				return true;

			if (Funds < amount)
				return false;

			Funds -= amount;
			Add(expensesThisMonth, category ?? "other", amount);
			return true;
		}

		/// <summary>Deducts money even if it drives the balance negative (interest, wages, subsidies).</summary>
		internal void Charge(int amount, string category)
		{
			if (amount <= 0)
				return;

			Funds -= amount;
			Add(expensesThisMonth, category ?? "other", amount);
		}

		/// <summary>Adds money (refunds, milestone rewards) under an income category.</summary>
		public void AddFunds(int amount, string category)
		{
			if (amount == 0)
				return;

			Funds += amount;
			Add(incomeThisMonth, category ?? "other", amount);
		}

		/// <summary>Projected monthly income minus expenses (taxes minus upkeep as of today), for the HUD.</summary>
		public int MonthlyBalance => ProjectedIncome - ProjectedExpenses;

		/// <summary>Income by category for the last completed month (e.g. "tax-residential", "refund", "milestone").</summary>
		public IReadOnlyDictionary<string, int> LastMonthIncome => incomeLastMonth;

		/// <summary>Expenses by category for the last completed month (e.g. "upkeep-roads", "upkeep-services", "construction").</summary>
		public IReadOnlyDictionary<string, int> LastMonthExpenses => expensesLastMonth;

		/// <summary>Income recorded so far this month.</summary>
		public IReadOnlyDictionary<string, int> ThisMonthIncome => incomeThisMonth;

		/// <summary>Expenses recorded so far this month.</summary>
		public IReadOnlyDictionary<string, int> ThisMonthExpenses => expensesThisMonth;

		public int LastMonthIncomeTotal { get; private set; }
		public int LastMonthExpensesTotal { get; private set; }

		static void Add(Dictionary<string, int> d, string key, int amount)
		{
			d.TryGetValue(key, out var v);
			d[key] = v + amount;
		}

		// ---- calendar ----
		[VerifySync]
		public int TotalDays { get; private set; }

		public CityDate Date
		{
			get
			{
				var cityClock = self.World.WorldActor.TraitOrDefault<CityClock>();
				if (cityClock != null)
					return cityClock.Date;

				var dpm = Math.Max(1, Info.DaysPerMonth);
				var month = TotalDays / dpm;
				return new CityDate(Info.StartYear + month / 12, month % 12 + 1, TotalDays % dpm + 1, dpm);
			}
		}

		/// <summary>Speed index 1..3 (normal, fast, fastest). Pause uses OpenRA's normal pause.</summary>
		[VerifySync]
		public int Speed
		{
			get
			{
				// Derived from the world timestep, which is global: one source of truth for every player's HUD.
				var steps = Info.SpeedTimesteps;
				for (var i = 0; i < steps.Length; i++)
					if (self.World.Timestep >= steps[i])
						return i + 1;

				return steps.Length;
			}
		}

		// ---- taxes / demand ----
		public int GetTaxRate(ZoneCategory category) { return taxRates[(int)category]; }

		/// <summary>Sets the base tax rate (-10..30) of a zone category. Used by orders only.</summary>
		internal void SetTaxRate(ZoneCategory category, int percent)
		{
			if (category < ZoneCategory.Residential || category > ZoneCategory.Office)
				return;

			taxRates[(int)category] = Math.Clamp(percent, EconomyMath.MinTax, EconomyMath.MaxTax);
			economy?.ResetTaxOverrides(category);
		}

		internal void AttachEconomy(CityEconomy e) { economy = e; }

		/// <summary>Jobs a building of this category offers (commercial buildings are scaled down, see CommercialJobPercent).</summary>
		internal int AdjustedJobs(ZoneCategory category, int maxJobs)
		{
			return category == ZoneCategory.Commercial ? Math.Max(maxJobs > 0 ? 1 : 0, (maxJobs * Info.CommercialJobPercent + 50) / 100) : maxJobs;
		}

		/// <summary>Demand in -100..100 for a category (drives ZoneGrowth and the RCIO bars).</summary>
		public int GetDemand(ZoneCategory category)
		{
			return demandModel != null ? demandModel.GetDemand(category) : demand[(int)category];
		}

		// ---- progression ----
		[VerifySync]
		public int PeakPopulation { get; private set; }

		int legacyMilestone;

		[VerifySync]
		public int MilestoneIndex => progression != null ? progression.MilestoneIndex : legacyMilestone;

		public string MilestoneName => progression != null ? progression.MilestoneName : MilestoneNames[legacyMilestone];

		/// <summary>Population of the next milestone (the last milestone's own population once everything is reached).</summary>
		public int NextMilestonePopulation => MilestonePopulation[Math.Clamp(MilestoneIndex + 1, 0, MilestonePopulation.Length - 1)];

		/// <summary>Cash bonus granted when milestone `index` is reached.</summary>
		public int MilestoneBonusAmount(int index) { return MilestoneBonus[Math.Clamp(index, 0, MilestoneBonus.Length - 1)]; }

		/// <summary>0..100 progress from the current to the next milestone.</summary>
		public int MilestoneProgress
		{
			get
			{
				if (progression != null)
					return progression.NextMilestoneXp <= 0 ? 100 : Math.Clamp(progression.Xp * 100 / progression.NextMilestoneXp, 0, 100);

				if (MilestoneIndex >= MilestonePopulation.Length - 1)
					return 100;

				var lo = MilestonePopulation[MilestoneIndex];
				var hi = MilestonePopulation[MilestoneIndex + 1];
				return Math.Clamp((Population - lo) * 100 / Math.Max(1, hi - lo), 0, 100);
			}
		}

		public IReadOnlyList<Notification> Notifications => notifications;

		/// <summary>Whether the player can build this CityPlaceable actor yet (peak population >= UnlockPopulation).</summary>
		public bool IsUnlocked(string actorType)
		{
			if (progression != null)
				return progression.IsUnlocked(actorType);

			if (!unlockCache.TryGetValue(actorType, out var required))
			{
				var info = self.World.Map.Rules.Actors.TryGetValue(actorType, out var ai) ? ai.TraitInfoOrDefault<CityPlaceableInfo>() : null;
				required = info?.UnlockPopulation ?? 0;
				unlockCache[actorType] = required;
			}

			return PeakPopulation >= required;
		}

		void Notify(string fluentKey)
		{
			notifications.Add(new Notification { Day = TotalDays, Text = fluentKey });
			if (notifications.Count > 20)
				notifications.RemoveAt(0);

			TextNotificationsManager.AddTransientLine(self.Owner, fluentKey);
		}

		void CheckMilestones()
		{
			if (Population > PeakPopulation)
				PeakPopulation = Population;

			// With a progression provider milestones, rewards and notifications are its business.
			if (progression != null)
				return;

			while (legacyMilestone < MilestonePopulation.Length - 1 && PeakPopulation >= MilestonePopulation[legacyMilestone + 1])
			{
				legacyMilestone++;
				AddFunds(MilestoneBonus[legacyMilestone], "milestone");
				Notify("notification-city-milestone-" + legacyMilestone);
			}
		}

		// ---- orders ----
		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case CityOrders.SetTax:
				{
					var cat = order.ExtraLocation.X;
					if (cat >= (int)ZoneCategory.Residential && cat <= (int)ZoneCategory.Office)
						taxRates[cat] = (int)Math.Min(order.ExtraData, 30u);

					break;
				}

				case CityOrders.SetSpeed:
				{
					var speed = (int)Math.Clamp(order.ExtraData, 1u, 3u);
					var steps = Info.SpeedTimesteps;
					self.World.Timestep = steps[Math.Min(speed, steps.Length) - 1];
					break;
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (++ticksInDay < Info.TicksPerDay)
				return;

			ticksInDay = 0;
			DailyUpdate();
		}
	}
}
