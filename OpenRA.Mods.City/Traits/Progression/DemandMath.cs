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
	/// <summary>Output of <see cref="DemandMath.Compute"/>: signed factor lists per category plus the per-zone free-property factor.</summary>
	public sealed class DemandResult
	{
		/// <summary>Factors shared by every zone type of a category (without the free-property factor), nonzero only.</summary>
		public readonly List<DemandFactor>[] BaseFactors = new List<DemandFactor>[5];

		public readonly int[] BaseSum = new int[5];

		/// <summary>Free-property factor per zone type (housing vacancy for residential, free buildings otherwise).</summary>
		public readonly int[] FreeValue = new int[DemandInputs.ZoneCount];

		/// <summary>Residential target ignoring vacancy: new households move in while this is positive.</summary>
		public int ResidentialAttraction;

		public DemandResult()
		{
			for (var i = 0; i < BaseFactors.Length; i++)
				BaseFactors[i] = [];
		}

		public int ZoneTarget(ZoneType zone)
		{
			var cat = (int)zone.Category();
			return cat == 0 ? 0 : Math.Clamp(BaseSum[cat] + FreeValue[(int)zone], -100, 100);
		}
	}

	/// <summary>
	/// Pure integer demand factor model (design 09, 3.1): every number the bars show is a signed factor so the UI can explain it.
	/// The same inputs always give the same result on every client.
	/// </summary>
	public static class DemandMath
	{
		public const string KeyStart = "demand-factor-start";
		public const string KeyJobs = "demand-factor-jobs";
		public const string KeyHappiness = "demand-factor-happiness";
		public const string KeyTaxes = "demand-factor-taxes";
		public const string KeyHomeless = "demand-factor-homeless";
		public const string KeyStudents = "demand-factor-students";
		public const string KeyUtilities = "demand-factor-utilities";
		public const string KeyFreeHousing = "demand-factor-free-housing";
		public const string KeyFreeBuildings = "demand-factor-free-buildings";
		public const string KeyLocal = "demand-factor-local-demand";
		public const string KeyService = "demand-factor-service";
		public const string KeyGoods = "demand-factor-goods";
		public const string KeyWorkforce = "demand-factor-workforce";
		public const string KeyHotels = "demand-factor-hotels";
		public const string KeyLabor = "demand-factor-labor";
		public const string KeyLaborSkilled = "demand-factor-labor-skilled";
		public const string KeyStorage = "demand-factor-storage";
		public const string KeyTourism = "demand-factor-tourism";

		public static string FreeKey(ZoneCategory category)
		{
			return category == ZoneCategory.Residential ? KeyFreeHousing : KeyFreeBuildings;
		}

		/// <summary>Low-density zone types tolerate fewer empty properties before the free-property brake kicks in.</summary>
		public static bool IsLowDensity(ZoneType zone)
		{
			return zone == ZoneType.ResidentialLow || zone == ZoneType.ResidentialRow
				|| zone == ZoneType.ResidentialLowRent || zone == ZoneType.CommercialLow;
		}

		/// <summary>
		/// +(K - free) * 50 / K while free &lt;= K, then -(free - K) * 50 / K, clamped to -60..50.
		/// K grows with the number of properties (10% tolerance) so big cities do not flap.
		/// </summary>
		public static int FreePropertyFactor(int free, int total, int baseK)
		{
			var k = Math.Max(1, baseK + total / 10);
			var v = free <= k ? (k - free) * 50 / k : -(free - k) * 50 / k;
			return Math.Clamp(v, -60, 50);
		}

		/// <summary>
		/// Accumulator spawning for the zoning growth loop: adds max(0, demand) and returns how many buildings to spawn
		/// (one per perSpawn points). Demand 10 gives one building per ~3 attempts, demand 100 four per attempt.
		/// </summary>
		public static int Accumulate(ref int accumulator, int demand, int perSpawn = 25)
		{
			perSpawn = Math.Max(1, perSpawn);
			accumulator += Math.Max(0, demand);
			var n = accumulator / perSpawn;
			accumulator %= perSpawn;
			return n;
		}

		public static void Compute(DemandInputs i, DemandModelInfo t, DemandResult r)
		{
			for (var c = 0; c < 5; c++)
			{
				r.BaseFactors[c].Clear();
				r.BaseSum[c] = 0;
			}

			Array.Clear(r.FreeValue);

			var utilities = -Math.Min(25, (i.PowerShortagePct + i.WaterShortagePct) / 4);

			ComputeResidential(i, t, r, utilities);
			ComputeCommercial(i, t, r, utilities);
			ComputeIndustrial(i, t, r, utilities);
			ComputeOffice(i, t, r, utilities);

			for (var c = 1; c < 5; c++)
				for (var f = 0; f < r.BaseFactors[c].Count; f++)
					r.BaseSum[c] += r.BaseFactors[c][f].Value;

			var res = r.BaseSum[(int)ZoneCategory.Residential];
			r.ResidentialAttraction = Math.Clamp(res, -100, 100);

			for (var z = 1; z < DemandInputs.ZoneCount; z++)
			{
				var zone = (ZoneType)z;
				var cat = zone.Category();
				if (cat == ZoneCategory.None)
					continue;

				var baseK = IsLowDensity(zone) ? t.FreeLowDensity : t.FreeHighDensity;
				if (cat == ZoneCategory.Residential)
					r.FreeValue[z] = FreePropertyFactor(i.FreeUnits[z], i.TotalUnits[z], baseK);
				else
					r.FreeValue[z] = FreePropertyFactor(i.FreeBuildings[z], i.TotalBuildings[z], baseK);
			}
		}

		static void Add(DemandResult r, ZoneCategory cat, string key, int value)
		{
			if (value != 0)
				r.BaseFactors[(int)cat].Add(new DemandFactor { Key = key, Value = value });
		}

		static int TaxFactor(DemandInputs i, ZoneCategory cat, DemandModelInfo t)
		{
			var v = Math.Clamp((t.NeutralTax - i.TaxRates[(int)cat]) * t.TaxPointsPerPercent, -30, 30);
			return v * (100 + i.TaxSensitivityPct) / 100;
		}

		static void ComputeResidential(DemandInputs i, DemandModelInfo t, DemandResult r, int utilities)
		{
			const ZoneCategory R = ZoneCategory.Residential;
			var pop = i.Population;

			// Bootstraps an empty city: fades out between StartAppealFull and StartAppealEnd residents.
			if (pop < t.StartAppealEnd)
			{
				var start = pop < t.StartAppealFull
					? t.StartAppeal
					: t.StartAppeal * (t.StartAppealEnd - pop) / Math.Max(1, t.StartAppealEnd - t.StartAppealFull);
				Add(r, R, KeyStart, start);
			}

			var freeJobs = i.TotalFreeJobs;
			var unemployed = i.Unemployed;
			Add(r, R, KeyJobs, (freeJobs - unemployed) * 100 / (freeJobs + unemployed + 20) / 2);

			if (pop > 0)
				Add(r, R, KeyHappiness, (i.AverageHappiness - 50) / 2);

			Add(r, R, KeyTaxes, TaxFactor(i, R, t));
			Add(r, R, KeyHomeless, -Math.Min(40, i.Homeless * 200 / (pop + 100)));
			Add(r, R, KeyStudents, Math.Min(10, i.FreeStudentSeats / 10));
			Add(r, R, KeyUtilities, utilities);
		}

		static void ComputeCommercial(DemandInputs i, DemandModelInfo t, DemandResult r, int utilities)
		{
			const ZoneCategory C = ZoneCategory.Commercial;
			Add(r, C, KeyLocal, Math.Clamp((i.ShopNeed * 100 / (i.ShopCapacity + 1) - 100) / 2, -50, 50));

			if (i.ShopServedPct >= 0)
				Add(r, C, KeyService, Math.Clamp((t.ServiceNeutral - i.ShopServedPct) * 3 / 2, -30, 45));

			Add(r, C, KeyGoods, -Math.Min(30, i.ShopsOutOfStockPct / 2));

			if (i.ShopStaffFillPct >= 0)
				Add(r, C, KeyWorkforce, Math.Clamp((i.ShopStaffFillPct - t.StaffNeutral) * 2, -40, 10));

			if (i.LodgingOccupancyPct >= 0)
			{
				var hotels = i.LodgingOccupancyPct >= 90 ? Math.Min(25, i.TouristsUnhoused)
					: i.LodgingOccupancyPct < 40 ? -(40 - i.LodgingOccupancyPct) / 2 : 0;
				Add(r, C, KeyHotels, hotels);
			}
			else if (i.TouristsUnhoused > 0)
				Add(r, C, KeyHotels, Math.Min(25, i.TouristsUnhoused));

			// Tourists spend in shops: a famous city lifts commercial demand a little.
			Add(r, C, KeyTourism, Math.Min(10, i.Attractiveness / 8));
			Add(r, C, KeyTaxes, TaxFactor(i, C, t));
			Add(r, C, KeyUtilities, utilities);
		}

		// Jobseekers above 1.25x the open slots for these education levels push demand up, a surplus of slots pushes it down (max 30).
		static int LaborFactor(DemandInputs i, int fromEdu, int toEdu)
		{
			var seekers = 0;
			var slots = 0;
			for (var e = fromEdu; e <= toEdu; e++)
			{
				seekers += i.UnemployedByEdu[e];
				slots += i.FreeJobsByEdu[e];
			}

			return Math.Clamp((seekers - slots * 125 / 100) * 30 / (seekers + slots + 10), -30, 30);
		}

		static void ComputeIndustrial(DemandInputs i, DemandModelInfo t, DemandResult r, int utilities)
		{
			const ZoneCategory I = ZoneCategory.Industrial;
			Add(r, I, KeyLocal, Math.Clamp((i.GoodsNeed * 100 / (i.GoodsProduced + 1) - 100) / 2, -50, 60));
			Add(r, I, KeyLabor, LaborFactor(i, 0, 1));
			if (i.StockPct > 60)
				Add(r, I, KeyStorage, -Math.Min(20, (i.StockPct - 60) / 2));

			Add(r, I, KeyTaxes, TaxFactor(i, I, t));
			Add(r, I, KeyUtilities, utilities);
		}

		static void ComputeOffice(DemandInputs i, DemandModelInfo t, DemandResult r, int utilities)
		{
			const ZoneCategory O = ZoneCategory.Office;
			Add(r, O, KeyLocal, Math.Clamp((i.OfficeNeed * 100 / (i.OfficeProduced + 1) - 100) / 2, -50, 60));
			Add(r, O, KeyLaborSkilled, LaborFactor(i, 2, 4));
			Add(r, O, KeyTaxes, TaxFactor(i, O, t));
			Add(r, O, KeyUtilities, utilities);
		}
	}
}
