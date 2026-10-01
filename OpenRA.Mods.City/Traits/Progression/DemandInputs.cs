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
	/// <summary>
	/// Optional contract for other work packages: implement on any world or player trait and the demand model asks it
	/// to overwrite the fields it knows better than the legacy fallbacks (citizens, economy, zoning, tourism).
	/// Called from synced code once per pulse. Fields left untouched keep the fallback value; -1 marks "unknown" where noted.
	/// </summary>
	public interface IDemandInputProvider
	{
		void FillDemandInputs(DemandInputs inputs);
	}

	/// <summary>
	/// Snapshot of everything the demand factors need. Plain integers, so DemandMath is a pure function and
	/// every client computes the same bars. Education index is 0..4 (uneducated..highly), zone index is (int)ZoneType.
	/// </summary>
	public sealed class DemandInputs
	{
		public const int ZoneCount = 13;
		public const int EduCount = 5;

		public int Population, Households, Homeless, Unemployed, AverageHappiness = 50, FreeStudentSeats;

		/// <summary>Workers currently employed by education (informational).</summary>
		public readonly int[] WorkersByEdu = new int[EduCount];

		/// <summary>Jobseekers by education.</summary>
		public readonly int[] UnemployedByEdu = new int[EduCount];

		/// <summary>Open job slots in operational workplaces by education.</summary>
		public readonly int[] FreeJobsByEdu = new int[EduCount];

		/// <summary>Residential: vacant household slots / all household slots, per zone type (density).</summary>
		public readonly int[] FreeUnits = new int[ZoneCount];
		public readonly int[] TotalUnits = new int[ZoneCount];

		/// <summary>Workplaces without any worker / company ("free properties") and all workplaces, per zone type.</summary>
		public readonly int[] FreeBuildings = new int[ZoneCount];
		public readonly int[] TotalBuildings = new int[ZoneCount];

		/// <summary>Commercial: daily retail need vs capacity (any unit, same for both).</summary>
		public int ShopNeed, ShopCapacity;

		/// <summary>0..100 share of local demand that is served (neutral 70), or -1 = unknown.</summary>
		public int ShopServedPct = -1;

		/// <summary>0..100 share of shops that are out of stock.</summary>
		public int ShopsOutOfStockPct;

		/// <summary>0..100 filled share of commercial job slots (neutral 75), or -1 = unknown.</summary>
		public int ShopStaffFillPct = -1;

		/// <summary>Industrial: goods needed by the city vs produced, warehouse fill 0..100 (-1 unknown).</summary>
		public int GoodsNeed, GoodsProduced;
		public int StockPct = -1;

		/// <summary>Office: weighted need vs produced.</summary>
		public int OfficeNeed, OfficeProduced;

		/// <summary>Tourism: tourists without a bed, lodging occupancy 0..100 (-1 = no hotels / unknown), city attractiveness 0..100.</summary>
		public int TouristsUnhoused;
		public int LodgingOccupancyPct = -1;
		public int Attractiveness;

		/// <summary>0..100 percent of demand not covered by supply.</summary>
		public int PowerShortagePct, WaterShortagePct;

		/// <summary>Tax percent per ZoneCategory index (0 unused).</summary>
		public readonly int[] TaxRates = [10, 10, 10, 10, 10];

		/// <summary>Percent modifier of the tax factor (policy key TaxDemandPct).</summary>
		public int TaxSensitivityPct;

		public int TotalFreeJobs
		{
			get
			{
				var n = 0;
				for (var i = 0; i < EduCount; i++)
					n += FreeJobsByEdu[i];

				return n;
			}
		}

		public void Clear()
		{
			Population = Households = Homeless = Unemployed = FreeStudentSeats = 0;
			AverageHappiness = 50;
			Array.Clear(WorkersByEdu);
			Array.Clear(UnemployedByEdu);
			Array.Clear(FreeJobsByEdu);
			Array.Clear(FreeUnits);
			Array.Clear(TotalUnits);
			Array.Clear(FreeBuildings);
			Array.Clear(TotalBuildings);
			ShopNeed = ShopCapacity = ShopsOutOfStockPct = 0;
			ShopServedPct = ShopStaffFillPct = StockPct = LodgingOccupancyPct = -1;
			GoodsNeed = GoodsProduced = OfficeNeed = OfficeProduced = 0;
			TouristsUnhoused = Attractiveness = PowerShortagePct = WaterShortagePct = 0;
			for (var i = 0; i < TaxRates.Length; i++)
				TaxRates[i] = 10;

			TaxSensitivityPct = 0;
		}
	}
}
