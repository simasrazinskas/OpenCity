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
	/// <summary>Initial level of a growable created in code (ZoneGrowth spawns buildings above level 1 on expensive land).</summary>
	public class GrowableLevelInit : ValueActorInit<int>, ISingleInstanceInit
	{
		public GrowableLevelInit(int value)
			: base(value) { }
	}

	/// <summary>
	/// Integer formulas for lot capacity, utilities, upkeep and rent (design/04 sections 3.4 and 3.6).
	/// Everything is derived from the archetype data in <see cref="GrowableBuildingInfo"/> plus (cells, level, land value),
	/// so levels are state and never need other actors.
	/// </summary>
	public static class LotMath
	{
		/// <summary>Land value floor used by the rent formula.</summary>
		public const int MinRentLandValue = 20;

		/// <summary>100 + perLevel * (level - 1), in percent.</summary>
		public static int LevelPercent(int level, int perLevel)
		{
			return 100 + perLevel * (Math.Max(1, level) - 1);
		}

		public static int HouseholdSlots(GrowableBuildingInfo info, int cells, int level)
		{
			if (info.HouseholdsPerCellMilli <= 0)
				return 0;

			if (info.SingleHousehold)
				return 1;

			var milli = (long)info.HouseholdsPerCellMilli * cells * LevelPercent(level, info.LevelCapacityPercent) / 100;
			return Math.Max(1, (int)((milli + 500) / 1000));
		}

		public static int Jobs(GrowableBuildingInfo info, int cells, int level)
		{
			var jobs = 0;
			if (info.JobsPerCellMilli > 0)
			{
				var milli = (long)info.JobsPerCellMilli * cells * LevelPercent(level, info.LevelCapacityPercent) / 100;
				jobs = Math.Max(1, (int)((milli + 500) / 1000));
			}

			if (info.HomeBusinessJobs > 0 && level >= info.HomeBusinessLevel)
				jobs += info.HomeBusinessJobs;

			return jobs;
		}

		/// <summary>Job slots by education for the level: the base mix shifts up the education ladder with each level.</summary>
		public static void JobSlots(GrowableBuildingInfo info, int cells, int level, int[] slots)
		{
			Array.Clear(slots);
			var total = Jobs(info, cells, level);
			if (total <= 0)
				return;

			Span<int> mix = stackalloc int[5];
			for (var i = 0; i < 5 && i < info.JobMix.Length; i++)
				mix[i] = info.JobMix[i];

			ShiftMix(mix, info.JobMixShiftPerLevel * (Math.Max(1, level) - 1));
			PropertyRegistry.Distribute(total, mix, slots);
		}

		/// <summary>Moves `points` percent from the lowest non-empty tier one tier up (repeatedly), in place.</summary>
		static void ShiftMix(Span<int> mix, int points)
		{
			while (points > 0)
			{
				var lowest = -1;
				for (var i = 0; i < mix.Length - 1; i++)
					if (mix[i] > 0)
					{
						lowest = i;
						break;
					}

				if (lowest < 0)
					return;

				var move = Math.Min(points, mix[lowest]);
				mix[lowest] -= move;
				mix[lowest + 1] += move;
				points -= move;
			}
		}

		/// <summary>Power or water use for the lot: per-cell base, falling by UtilityDiscountPercent per level above 1.</summary>
		public static int Utility(int perCellMilli, int discountPercent, int cells, int level)
		{
			if (perCellMilli <= 0)
				return 0;

			var factor = Math.Max(30, 100 - discountPercent * (Math.Max(1, level) - 1));
			var milli = (long)perCellMilli * cells * factor / 100;
			return Math.Max(1, (int)((milli + 500) / 1000));
		}

		/// <summary>Monthly upkeep of the whole building in dollars.</summary>
		public static int Upkeep(GrowableBuildingInfo info, int cells, int level)
		{
			return Math.Max(1, info.UpkeepPerCell * cells * LevelPercent(level, info.UpkeepLevelPercent) / 100);
		}

		/// <summary>
		/// Land value as a lot of this zone reads it: capped (industry prefers cheap land) plus the zone bonus.
		/// </summary>
		public static int ZoneLandValue(GrowableBuildingInfo info, int landValue)
		{
			return Math.Clamp(Math.Min(landValue, info.LandValueCap) + info.LandValueBonus, 0, 100);
		}

		/// <summary>
		/// Monthly rent of the whole building in dollars: ((LV + zoneFactor * level) * cells * space) / 10, scaled by the
		/// city-wide RentScalePercent (tuned so a household pays about a quarter of its income).
		/// </summary>
		public static int Rent(GrowableBuildingInfo info, int cells, int level, int landValue, int rentScalePercent)
		{
			// Even worthless land costs something to rent: keeps cheap industrial and commercial lots viable.
			var lv = Math.Max(MinRentLandValue, ZoneLandValue(info, landValue));
			var basis = (long)(lv + info.RentZoneFactor * Math.Max(1, level)) * cells * info.RentSpacePercent / 100;
			return (int)(basis * rentScalePercent / 1000);
		}
	}
}
