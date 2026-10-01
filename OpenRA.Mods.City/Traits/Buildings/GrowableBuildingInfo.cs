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

using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("A zoned building (lot archetype) that spawns automatically (ZoneGrowth). Level 1..5, condition, rent and abandonment are state.",
		"One actor exists per zone and footprint; levels never swap the actor. See design/04-zoning-buildings.md.")]
	public class GrowableBuildingInfo : TraitInfo, Requires<CityBuildingInfo>
	{
		public readonly ZoneType Zone = ZoneType.ResidentialLow;

		[Desc("Initial level (legacy decoration actors such as res-low-3 use it).")]
		public readonly int Level = 1;

		public readonly int MaxLevel = 5;

		[Desc("Level never changes (signature buildings are level 5 landmarks).")]
		public readonly bool FixedLevel = false;

		// ---- capacity (per cell at level 1; levels add LevelCapacityPercent each) ----
		[Desc("Households per cell in thousandths (6000 = 6 households per cell). 0 = not residential.")]
		public readonly int HouseholdsPerCellMilli = 0;

		[Desc("One household regardless of lot size and level (low density).")]
		public readonly bool SingleHousehold = false;

		[Desc("Average residents per household, used for the legacy aggregate capacity (CityBuilding.Info.MaxResidents).")]
		public readonly int ResidentsPerHousehold = 3;

		[Desc("Jobs per cell in thousandths. 0 = no jobs.")]
		public readonly int JobsPerCellMilli = 0;

		[Desc("Default job mix by education (percent, edu 0..4) at level 1; every level above 1 shifts JobMixShiftPerLevel points up.")]
		public readonly int[] JobMix = [40, 40, 20, 0, 0];

		public readonly int JobMixShiftPerLevel = 10;

		[Desc("Capacity growth in percent per level above 1.")]
		public readonly int LevelCapacityPercent = 25;

		[Desc("Extra jobs of a home business (low density), from HomeBusinessLevel on.")]
		public readonly int HomeBusinessJobs = 0;

		public readonly int HomeBusinessLevel = 3;

		public readonly int PowerPerCellMilli = 1000;

		public readonly int WaterPerCellMilli = 1000;

		[Desc("Utility use per cell falls by this many percent per level above 1.")]
		public readonly int UtilityDiscountPercent = 8;

		// ---- money ----
		[Desc("Monthly upkeep per cell at level 1 (dollars).")]
		public readonly int UpkeepPerCell = 4;

		[Desc("Upkeep grows by this many percent per level above 1.")]
		public readonly int UpkeepLevelPercent = 30;

		[Desc("Zone factor of the rent formula: rent ~ (landValue + factor * level) * cells.")]
		public readonly int RentZoneFactor = 6;

		[Desc("Space multiplier (percent) of the rent formula. High density lots pack many tenants into a cell.")]
		public readonly int RentSpacePercent = 100;

		[Desc("Land value as read by this zone is capped here (industry prefers cheap land).")]
		public readonly int LandValueCap = 100;

		[Desc("Added to the land value as read by this zone.")]
		public readonly int LandValueBonus = 0;

		// ---- construction ----
		[Desc("Ticks spent under construction after spawning (1x1 lot).")]
		public readonly int ConstructionTicks = 150;

		[Desc("Extra construction ticks per cell beyond the first.")]
		public readonly int ConstructionTicksPerCell = 40;

		[Desc("Construction time varies by up to this many percent either way (random per building).")]
		public readonly int ConstructionVariance = 25;

		[Desc("Ticks of scaffolding after a level-up. The building stays operational.")]
		public readonly int UpgradeConstructionTicks = 50;

		// ---- condition loop (design 3.5) ----
		[Desc("Ticks between condition updates (staggered per building).")]
		public readonly int ConditionInterval = 100;

		[Desc("Condition change per update when the building is financially healthy (the same size down when it is not).")]
		public readonly int ConditionStep = 10;

		[Desc("Condition needed to level up is LevelUpBase + LevelUpPerLevel * level.")]
		public readonly int LevelUpBase = 200;

		public readonly int LevelUpPerLevel = 100;

		[Desc("Condition at or below -LevelDownAt drops a level (level 1: abandoned).")]
		public readonly int LevelDownAt = 300;

		[Desc("Land value needed to reach level 2, 3, 4 and 5.")]
		public readonly int[] LevelLandValue = [28, 38, 48, 58];

		[Desc("Progression milestone index needed to reach level 2, 3, 4 and 5 (only when IProgression exists).")]
		public readonly int[] LevelMilestone = [0, 1, 2, 3];

		[Desc("Happiness needed to level up.")]
		public readonly int LevelUpHappiness = 40;

		[Desc("Without any tenant paying rent (no citizen/economy sim yet) the condition follows happiness + land value:",
			"step * clamp(happiness + landValue - this, -40, 40) / 40.")]
		public readonly int FallbackBreakEven = 90;

		// ---- spawning ----
		[Desc("Low density: the largest lot area shrinks as land value rises (area <= clamp(5 - LV/20, 1, 4)).")]
		public readonly bool AreaByLandValue = false;

		[Desc("Spawn at level 2 when the land value at the lot is at least this (0 = always level 1).")]
		public readonly int SpawnLevel2LandValue = 0;

		// ---- abandonment (legacy days = CityManager pulses) ----
		[Desc("Days without power, water or road access before the building is abandoned.")]
		public readonly int AbandonDaysWithoutService = 45;

		[Desc("Happiness below this value counts as unhappy.")]
		public readonly int AbandonHappiness = 20;

		[Desc("Days of unhappiness before the building is abandoned.")]
		public readonly int AbandonDaysUnhappy = 60;

		[Desc("Days an abandoned building stands before it collapses into rubble.")]
		public readonly int CollapseDays = 30;

		[Desc("An abandoned building recovers if services are restored and happiness is at least this high. Set to -1 to disable recovery.")]
		public readonly int RecoverHappiness = 30;

		public override object Create(ActorInitializer init) { return new GrowableBuilding(init, this); }
	}
}
