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
	/// <summary>Pure integer formulas of the economy (no world access, unit-testable). Money is in cents.</summary>
	public static class EconomyMath
	{
		public const int MinTax = -10;
		public const int MaxTax = 30;

		// Job mix per (kind: extractor, processor, retail, office; level 1..3): percent for education 0..4 (design 05 section 3.2).
		static readonly int[][][] JobMixTable =
		[
			[[70, 30, 0, 0, 0], [50, 40, 10, 0, 0], [40, 40, 20, 0, 0]],
			[[40, 40, 20, 0, 0], [20, 40, 30, 10, 0], [10, 25, 35, 25, 5]],
			[[50, 40, 10, 0, 0], [30, 40, 25, 5, 0], [20, 35, 30, 15, 0]],
			[[0, 20, 50, 30, 0], [0, 10, 35, 40, 15], [0, 0, 20, 45, 35]],
			[[40, 40, 20, 0, 0], [20, 40, 30, 10, 0], [10, 25, 35, 25, 5]],
		];

		public static int[] JobMix(CompanyKind kind, int level)
		{
			return JobMixTable[(int)kind][Math.Clamp(level, 1, 3) - 1];
		}

		/// <summary>Splits `total` into slots by percent; the remainder goes to the largest share (deterministic).</summary>
		public static void SplitJobs(int total, int[] percent, int[] slots)
		{
			Array.Clear(slots);
			if (total <= 0)
				return;

			var assigned = 0;
			var best = 0;
			for (var i = 0; i < slots.Length && i < percent.Length; i++)
			{
				slots[i] = total * percent[i] / 100;
				assigned += slots[i];
				if (percent[i] > percent[best])
					best = i;
			}

			slots[best] += total - assigned;
		}

		public static int ClampTax(int percent) { return Math.Clamp(percent, MinTax, MaxTax); }

		/// <summary>Tax in cents on an amount at a percent rate (negative rate = subsidy).</summary>
		public static long Tax(long cents, int ratePercent) { return cents * ClampTax(ratePercent) / 100; }

		/// <summary>Freight in cents per unit of a resource: weight x cells x milli-cents per weight-cell.</summary>
		public static int FreightPerUnit(int weight, int cells, int milliCentsPerWeightCell)
		{
			if (weight <= 0 || cells <= 0)
				return 0;

			return Math.Max(1, (int)((long)weight * cells * milliCentsPerWeightCell / 1000));
		}

		/// <summary>Company efficiency in percent (0..160) before staffing: happiness, fee and specialisation modifiers.</summary>
		public static int Efficiency(int avgHappiness, int feePercent, int specialisationBonus)
		{
			var happy = (avgHappiness - 50) * 3 / 10;
			var fee = feePercent > 100 ? -4 * (feePercent - 100) / 10 : 2 * (100 - feePercent) / 10;
			return Math.Clamp(100 + happy + fee + specialisationBonus, 0, 160);
		}

		/// <summary>Production-specialisation bonus: up to `maxBonus` percent at `fullAtUnits` produced per month.</summary>
		public static int Specialisation(int producedUnitsMonth, int fullAtUnits, int maxBonus)
		{
			return fullAtUnits <= 0 ? 0 : Math.Min(maxBonus, (int)((long)producedUnitsMonth * maxBonus / fullAtUnits));
		}

		/// <summary>Annual loan interest rate in tenths of a percent: 3% rising to 20% with utilisation, minus discounts.</summary>
		public static int LoanRateTenths(int utilisationPercent, int discountPoints)
		{
			var u = Math.Clamp(utilisationPercent, 0, 100);
			return Math.Max(10, 30 + 170 * u / 100 - discountPoints * 10);
		}

		/// <summary>Monthly interest in cents on a principal in dollars.</summary>
		public static long LoanInterestCents(int principalDollars, int annualRateTenths)
		{
			return (long)principalDollars * 100 * annualRateTenths / 1000 / 12;
		}

		/// <summary>Daily rent (cents) of a company: (landValue + zoneFactor x level) x lotCells x space / 30.</summary>
		public static int RentPerDay(int landValue, int zoneFactor, int level, int lotCells, int spacePercent, int daysPerMonth, int moneyScalePercent)
		{
			var monthly = (long)(landValue + zoneFactor * level) * Math.Max(1, lotCells) * spacePercent * moneyScalePercent / 10000;
			return (int)(monthly / Math.Max(1, daysPerMonth));
		}

		/// <summary>Elastic consumption multiplier in percent: households with savings spend more, poor ones less.</summary>
		public static int SpendingPercent(long cashCents, long monthlyBaseSpendCents)
		{
			if (monthlyBaseSpendCents <= 0)
				return 100;

			var months100 = cashCents * 100 / monthlyBaseSpendCents;
			return (int)Math.Clamp(60 + months100 / 4, 40, 220);
		}

		public static int Hash(int a, int b)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 2246822519u;
				h ^= h >> 15;
				h *= 2654435761u;
				h ^= h >> 13;
				return (int)(h & 0x7fffffff);
			}
		}

		public static int Mix(int hash, int value)
		{
			unchecked
			{
				return (hash ^ value) * 16777619 + 0x2545F491;
			}
		}
	}
}
