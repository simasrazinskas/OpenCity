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

using System.Collections.Generic;

namespace OpenRA.Mods.City.Traits
{
	// Public simulation state (read by UI, growth and traffic). Updated once per in-game day by CityManager.DailyUpdate.
	public partial class CityManager
	{
		readonly List<CityBuilding> buildings = [];
		readonly long[] projectedTaxTenths = new long[5];

		// ---- people & jobs ----
		[VerifySync]
		public int Population { get; private set; }

		/// <summary>Total workforce (working-age residents, about 55% of the population).</summary>
		[VerifySync]
		public int Workers { get; private set; }

		/// <summary>Job slots in operational workplaces (including service buildings).</summary>
		[VerifySync]
		public int Jobs { get; private set; }

		/// <summary>Workforce members without a job.</summary>
		[VerifySync]
		public int Unemployed { get; private set; }

		/// <summary>Workers that hold a job: min(Workers, Jobs).</summary>
		public int Employed => Workers - Unemployed;

		public int FreeJobs => Jobs - Employed;

		/// <summary>Unemployed share of the workforce, percent.</summary>
		public int UnemploymentRate => Unemployed * 100 / (Workers + 1);

		/// <summary>Approximate households (3 residents each), for display.</summary>
		public int Households => citizens != null ? citizens.Households : (Population + 2) / 3;

		/// <summary>Residents the (non-abandoned) residential buildings can hold.</summary>
		[VerifySync]
		public int HousingCapacity { get; private set; }

		/// <summary>0..100: residents covered by education, weighted by coverage strength.</summary>
		[VerifySync]
		public int EducatedRatio { get; private set; }

		/// <summary>Latches true once population or education makes the Office zone worthwhile.</summary>
		[VerifySync]
		public bool OfficeUnlocked { get; private set; }

		/// <summary>Number of simulated (registered) buildings.</summary>
		public int BuildingCount => buildings.Count;

		/// <summary>Job slots (all non-abandoned workplaces, including under construction) of a zone category.</summary>
		public int GetJobSlots(ZoneCategory category) { return jobSlots[(int)category]; }

		// ---- utilities ----
		[VerifySync]
		public int PowerProduced { get; private set; }

		/// <summary>Power wanted by all operational buildings. A deficit exists when this exceeds PowerProduced.</summary>
		[VerifySync]
		public int PowerConsumed { get; private set; }

		[VerifySync]
		public int WaterProduced { get; private set; }

		[VerifySync]
		public int WaterConsumed { get; private set; }

		// ---- wellbeing ----

		/// <summary>Resident-weighted average happiness of residential buildings (50 while empty).</summary>
		[VerifySync]
		public int AverageHappiness { get; private set; } = 50;

		/// <summary>Residential demand ignoring excess vacancy: new residents move in while this is positive.</summary>
		[VerifySync]
		public int ResidentialAttraction { get; private set; }

		[VerifySync]
		public int DemandResidential => GetDemand(ZoneCategory.Residential);

		[VerifySync]
		public int DemandCommercial => GetDemand(ZoneCategory.Commercial);

		[VerifySync]
		public int DemandIndustrial => GetDemand(ZoneCategory.Industrial);

		[VerifySync]
		public int DemandOffice => GetDemand(ZoneCategory.Office);

		[VerifySync]
		public int TaxResidential => taxRates[(int)ZoneCategory.Residential];

		[VerifySync]
		public int TaxCommercial => taxRates[(int)ZoneCategory.Commercial];

		[VerifySync]
		public int TaxIndustrial => taxRates[(int)ZoneCategory.Industrial];

		[VerifySync]
		public int TaxOffice => taxRates[(int)ZoneCategory.Office];

		// ---- finances (projections for this month) ----

		/// <summary>Taxes (and fees) that would be collected if the month ended today. With the economy: this month's run rate.</summary>
		[VerifySync]
		public int ProjectedIncome { get; private set; }

		/// <summary>Road and service upkeep that would be charged if the month ended today.</summary>
		[VerifySync]
		public int ProjectedExpenses { get; private set; }

		public int ProjectedRoadUpkeep { get; private set; }
		public int ProjectedServiceUpkeep { get; private set; }

		/// <summary>Projected monthly tax income of one category, in dollars.</summary>
		public int GetMonthlyTaxIncome(ZoneCategory category) { return (int)(projectedTaxTenths[(int)category] / 10); }

		// ---- registry (used by CityBuilding) ----
		internal void Register(CityBuilding b, uint sortId)
		{
			b.SortId = sortId;
			var i = buildings.Count;
			while (i > 0 && buildings[i - 1].SortId > sortId)
				i--;

			buildings.Insert(i, b);
		}

		internal void Unregister(CityBuilding b)
		{
			buildings.Remove(b);
		}
	}
}
