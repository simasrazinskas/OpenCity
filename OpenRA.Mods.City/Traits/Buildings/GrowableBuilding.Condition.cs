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
	// The CS2 loop (design/04 3.5): tenants pay rent, rent covers upkeep, a healthy balance fills the condition bar and the
	// building levels up; an unhealthy balance empties it, the building drops a level and finally is abandoned.
	public partial class GrowableBuilding
	{
		/// <summary>-LevelDownAt .. LevelUpAt(level). Reaching the top levels the building up, the bottom drops it.</summary>
		[VerifySync]
		public int Condition { get; private set; }

		// Rent received minus upkeep accrued, in milli-dollars (clamped to +/- two months of upkeep).
		[VerifySync]
		int balanceMilli;

		PropertyRegistry registry;

		/// <summary>Many tenants cannot afford the rent (reported by CIT): the condition does not rise while it lasts.</summary>
		[VerifySync]
		public bool HighRent { get; internal set; }

		/// <summary>Condition needed to leave the current level.</summary>
		public int LevelUpAt => Info.LevelUpBase + Info.LevelUpPerLevel * Level;

		/// <summary>0..100 progress of the condition towards the next level (0 at max level or when not positive).</summary>
		public int UpgradeProgress => Level >= Info.MaxLevel || Condition <= 0 ? 0 : Math.Min(100, Condition * 100 / Math.Max(1, LevelUpAt));

		/// <summary>True when the condition is falling (financial trouble).</summary>
		public bool Declining => Condition < 0;

		/// <summary>Monthly upkeep of the whole building in dollars.</summary>
		public int Upkeep => LotMath.Upkeep(Info, Cells, Level);

		/// <summary>The tenant (CIT/ECO) paid rent: ZON feeds it into the condition.</summary>
		internal void AddRentPaid(int dollars)
		{
			if (dollars <= 0)
				return;

			balanceMilli = (int)Math.Min(balanceMilli + (long)dollars * 1000, MaxBalance);
		}

		int MonthlyUpkeepMilli => Upkeep * 1000;

		int MaxBalance => 2 * MonthlyUpkeepMilli;

		internal void ResetBalance() { balanceMilli = MonthlyUpkeepMilli; }

		void ConditionTick()
		{
			registry ??= self.World.WorldActor.TraitOrDefault<PropertyRegistry>();
			if (registry == null || Info.FixedLevel)
				return;

			var interval = Math.Max(1, Info.ConditionInterval);
			var step = Info.ConditionStep;
			var property = registry.GetByActor(self);
			var delta = 0;

			if (registry.PaymentsActive)
			{
				// Tenants pay: compare their rent with the upkeep that accrued. Vacant buildings stand still.
				var occupied = property != null && (property.Households > 0 || property.Residents > 0 || property.TotalJobsFilled > 0 || property.CompanyId != 0);
				if (occupied)
				{
					var due = (long)MonthlyUpkeepMilli * interval / Math.Max(1, registry.TicksPerMonth);
					balanceMilli = (int)Math.Clamp(balanceMilli - due, -MaxBalance, MaxBalance);
					delta = balanceMilli >= 0 ? step : -step;
				}
			}
			else
			{
				// MVP fallback (no citizen/economy payments yet): a virtual landlord keeps the books, the condition follows
				// happiness and land value.
				var landValue = LotMath.ZoneLandValue(Info, property != null ? property.LandValue : city.LandValue);
				delta = step * Math.Clamp(city.Happiness + landValue - Info.FallbackBreakEven, -40, 40) / 40;

				// Gentle: without real tenants a level 1 building is never abandoned for money reasons.
				if (Level <= 1)
					delta = Math.Max(0, delta);

				balanceMilli = MonthlyUpkeepMilli;
			}

			if (HighRent && delta > 0)
				delta = 0;

			if (delta == 0)
				return;

			Condition = Math.Clamp(Condition + delta, -Info.LevelDownAt, LevelUpAt);

			if (Condition >= LevelUpAt)
				TryLevelUp(property);
			else if (Condition <= -Info.LevelDownAt)
				LevelDown();
		}

		int MaxAllowedLevel()
		{
			var max = Math.Max(1, Info.MaxLevel);
			var milestone = registry?.MilestoneIndex ?? -1;
			if (milestone < 0)
				return max;

			// Level n+1 needs LevelMilestone[n - 1].
			var level = 1;
			while (level < max)
			{
				var need = level - 1 < Info.LevelMilestone.Length ? Info.LevelMilestone[level - 1] : 0;
				if (milestone < need)
					break;

				level++;
			}

			return level;
		}

		void TryLevelUp(Property property)
		{
			if (Level >= MaxAllowedLevel())
				return;

			var needLandValue = Level - 1 < Info.LevelLandValue.Length ? Info.LevelLandValue[Level - 1] : 0;
			var landValue = LotMath.ZoneLandValue(Info, property != null ? property.LandValue : city.LandValue);
			if (landValue < needLandValue || city.Happiness < Info.LevelUpHappiness || LacksServices())
				return;

			Level++;
			Condition = 0;
			ResetBalance();
			ApplyCapacity(true);

			IsUpgrading = true;
			ConstructionTotal = Math.Max(1, Info.UpgradeConstructionTicks);
			constructionLeft = ConstructionTotal;
		}

		void LevelDown()
		{
			if (Level <= 1)
			{
				Abandon();
				return;
			}

			Level--;
			Condition = 0;
			ResetBalance();
			ApplyCapacity(true);
		}
	}
}
