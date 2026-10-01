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
	// Jobs, taxes, upkeep and the demand model.
	public partial class CityManager
	{
		static readonly string[] TaxKeys = ["", "tax-residential", "tax-commercial", "tax-industrial", "tax-office"];

		// Upkeep category per service kind when the economy splits the budget ("upkeep-services" is the legacy single key).
		static readonly string[] UpkeepKeys =
		[
			"upkeep-power", "upkeep-water", "upkeep-sewage", "upkeep-garbage", "upkeep-health", "upkeep-deathcare", "upkeep-education",
			"upkeep-police", "upkeep-fire", "upkeep-parks", "upkeep-telecom", "upkeep-post", "upkeep-admin", "upkeep-services",
		];

		readonly int[] upkeepByKind = new int[14];

		static int UpkeepKind(CityBuilding b)
		{
			if (b.Producer != null)
				return b.Producer.Info.Water > 0 && b.Producer.Info.Power <= 0 ? (int)ServiceKind.Water : (int)ServiceKind.Power;

			if (b.Service != null)
				return (int)b.Service.Info.Kind;

			return 13;
		}

		// Employed = min(workforce, jobs). Without a citizen simulation jobs are filled proportionally to each workplace's size
		// (remainder in ActorID order). Also computes the projected monthly taxes and upkeep from the final occupancy.
		void FillJobsAndCollect()
		{
			var fill = citizens == null;
			var employed = Workers - Unemployed;
			var totalJobs = Jobs;
			var assigned = 0;
			if (fill)
				for (var i = 0; i < buildings.Count; i++)
				{
					var b = buildings[i];
					if (b.Category == ZoneCategory.Residential || b.Info.MaxJobs <= 0 || !b.OperationalToday)
						continue;

					var max = AdjustedJobs(b.Category, b.Info.MaxJobs);
					b.Workers = employed >= totalJobs ? max : (int)((long)max * employed / Math.Max(1, totalJobs));
					assigned += b.Workers;
				}

			var remainder = employed - assigned;
			var baseTax = new[] { 0, Info.ResidentialTaxBase, Info.CommercialTaxBase, Info.IndustrialTaxBase, Info.OfficeTaxBase };
			Array.Clear(projectedTaxTenths);
			Array.Clear(upkeepByKind);
			var serviceUpkeep = 0;
			var scale = Math.Max(1, Info.MonthlyScalePercent);
			var upkeepScale = Math.Max(1, Info.UpkeepScalePercent);
			for (var i = 0; i < buildings.Count; i++)
			{
				var b = buildings[i];
				var info = b.Info;
				var cat = b.Category;
				if (!b.OperationalToday)
					continue;

				if (fill && cat != ZoneCategory.Residential && info.MaxJobs > 0 && remainder > 0 && b.Workers < AdjustedJobs(cat, info.MaxJobs))
				{
					b.Workers++;
					remainder--;
				}

				if (cat == ZoneCategory.None)
				{
					var kind = UpkeepKind(b);
					var u = info.Upkeep;
					if (economy != null)
					{
						// A services provider books budget adjustments itself; apply the economy's slider only without one.
						if (kind < 13 && !economy.ServicesBookBudget)
							u = u * economy.ServiceBudgetPercent((ServiceKind)kind) / 100;

						upkeepByKind[kind] += u * upkeepScale / 100;
					}

					serviceUpkeep += u * upkeepScale / 100;
					continue;
				}

				if (economy != null)
					continue;

				// tenths of $ = persons * base * level bonus * land value factor * rate / 10
				var persons = cat == ZoneCategory.Residential ? b.Residents : b.Workers;
				var levelPct = 100 + 25 * (info.Level - 1);
				var lvPct = 70 + b.LandValue * 6 / 10;
				projectedTaxTenths[(int)cat] += (long)persons * baseTax[(int)cat] * levelPct / 100 * lvPct / 100 * taxRates[(int)cat] / 10 * scale / 100;
			}

			var tax = 0;
			for (var c = 1; c < 5; c++)
				tax += (int)(projectedTaxTenths[c] / 10);

			ProjectedRoadUpkeep = roads != null ? roads.MonthlyUpkeep * upkeepScale / 100 : 0;
			ProjectedServiceUpkeep = serviceUpkeep;
			ProjectedIncome = economy != null ? economy.ProjectedMonthlyIncome() : tax;
			ProjectedExpenses = ProjectedRoadUpkeep + serviceUpkeep;
		}

		void EndMonth()
		{
			if (economy == null)
				for (var c = 1; c < 5; c++)
				{
					var income = (int)(projectedTaxTenths[c] / 10);
					if (income > 0)
					{
						Funds += income;
						Add(incomeThisMonth, TaxKeys[c], income);
					}
				}

			var spent = 0;
			if (ProjectedRoadUpkeep > 0)
			{
				Funds -= ProjectedRoadUpkeep;
				Add(expensesThisMonth, "upkeep-roads", ProjectedRoadUpkeep);
				spent += ProjectedRoadUpkeep;
			}

			if (economy == null)
			{
				if (ProjectedServiceUpkeep > 0)
				{
					Funds -= ProjectedServiceUpkeep;
					Add(expensesThisMonth, "upkeep-services", ProjectedServiceUpkeep);
				}
			}
			else
				for (var k = 0; k < upkeepByKind.Length; k++)
					if (upkeepByKind[k] > 0)
					{
						Funds -= upkeepByKind[k];
						Add(expensesThisMonth, UpkeepKeys[k], upkeepByKind[k]);
						spent += upkeepByKind[k];
					}

			economy?.NoteUpkeepSpent(spent);
			economy?.SettleMonth();

			var inc = 0;
			foreach (var v in incomeThisMonth.Values)
				inc += v;

			var exp = 0;
			foreach (var v in expensesThisMonth.Values)
				exp += v;

			LastMonthIncomeTotal = inc;
			LastMonthExpensesTotal = exp;
			incomeLastMonth = new System.Collections.Generic.Dictionary<string, int>(incomeThisMonth);
			expensesLastMonth = new System.Collections.Generic.Dictionary<string, int>(expensesThisMonth);
			incomeThisMonth.Clear();
			expensesThisMonth.Clear();
		}

		static int Clamp100(int v) { return Math.Clamp(v, -100, 100); }

		// Demand model (targets in -100..100, then eased towards the target: faster down than up).
		//  R: 55 + jobs-vs-workers balance/2 (+25 while empty), minus unemployment above 10%, +/- happiness, tax, utility shortage,
		//     minus a penalty for housing vacancy above 15% (+12 units). ResidentialAttraction is R without the vacancy penalty.
		//  C: wants pop/4 commercial jobs; I: wants 8 + 25% of workforce + 10% of commercial jobs;
		//  O: wants (8 + 20% of educated ratio)% of workforce once pop >= 800 or education coverage exists.
		//  Taxes: +/-3 per percentage point away from 10%.
		static int ShortagePenalty(int consumed, int produced)
		{
			if (consumed <= produced || consumed <= 0)
				return 0;

			return Math.Min(25, 5 + (consumed - produced) * 200 / consumed);
		}

		void UpdateDemand()
		{
			var pop = Population;
			var workforce = Workers;
			var u = UnemploymentRate;
			var pipelineJobs = jobSlots[(int)ZoneCategory.Commercial] + jobSlots[(int)ZoneCategory.Industrial]
				+ jobSlots[(int)ZoneCategory.Office] + jobSlots[(int)ZoneCategory.None];

			// Utility shortages lower all demand in proportion to the deficit (max 25 each), not as a cliff.
			var shortage = ShortagePenalty(PowerConsumed, PowerProduced) + ShortagePenalty(WaterConsumed, WaterProduced);
			var jc = jobSlots[(int)ZoneCategory.Commercial];
			var ji = jobSlots[(int)ZoneCategory.Industrial];
			var jo = jobSlots[(int)ZoneCategory.Office];

			if (!OfficeUnlocked && (pop >= Info.OfficePopulationThreshold || EducatedRatio >= 20))
				OfficeUnlocked = true;

			// Residential
			var balance = (pipelineJobs - workforce) * 100 / (pipelineJobs + workforce + 20);
			var r = 55 + balance / 2 + (pop == 0 ? 25 : 0);

			// Unemployment pushes people away and asks for jobs instead (below).
			if (u > 6)
				r -= (u - 6) * 2;
			if (pop > 0)
				r += (AverageHappiness - 50) / 2;
			r += (10 - taxRates[(int)ZoneCategory.Residential]) * 3;
			r -= shortage;
			ResidentialAttraction = Clamp100(r);

			var vacancy = HousingCapacity - pop;
			var excess = Math.Max(0, vacancy - (HousingCapacity * 15 / 100 + 12));
			demandTarget[(int)ZoneCategory.Residential] = Clamp100(r - excess * 100 / (HousingCapacity + 30));

			// Commercial: about 1 job per 4 residents.
			var desiredC = pop / 4;
			var c = (desiredC - jc) * 100 / (desiredC + 10);
			c += (10 - taxRates[(int)ZoneCategory.Commercial]) * 3;
			c -= shortage;
			if (pop > 0)
				c += (AverageHappiness - 50) / 4;
			if (jc > 0 && ji * 3 < jc)
				c -= 10;
			if (u > 8)
				c += u - 8;
			if (pop == 0)
				c = Math.Min(c, 0);
			demandTarget[(int)ZoneCategory.Commercial] = Clamp100(c);

			// Industrial: jobs for the workforce and goods for commercial. Starts moderately positive.
			var desiredI = 8 + workforce * 25 / 100 + jc * 10 / 100;
			var ind = (desiredI - ji) * 100 / (desiredI + 10);
			if (u > 6)
				ind += (u - 6) * 2;
			ind += (10 - taxRates[(int)ZoneCategory.Industrial]) * 3;
			ind -= shortage;
			demandTarget[(int)ZoneCategory.Industrial] = Clamp100(ind);

			// Office: needs an educated population.
			var o = 0;
			if (OfficeUnlocked)
			{
				var desiredO = workforce * (8 + EducatedRatio * 20 / 100) / 100;
				o = (desiredO - jo) * 100 / (desiredO + 10);
				o += (10 - taxRates[(int)ZoneCategory.Office]) * 3;
				o -= shortage;
				if (u > 8)
					o += u - 8;
			}

			demandTarget[(int)ZoneCategory.Office] = Clamp100(o);

			// With companies on the map the C/I/O targets come from the economy: unmet household needs, imports of goods and
			// vacancy of buildings that have no company yet (design 05 section 3.8). Taxes and utilities apply as before.
			if (economy != null && economy.CompanyCount > 0)
			{
				var vacC = Math.Min(30, economy.VacantSlots(ZoneCategory.Commercial) * 3);
				var vacI = Math.Min(30, economy.VacantSlots(ZoneCategory.Industrial) * 3);
				var vacO = Math.Min(30, economy.VacantSlots(ZoneCategory.Office) * 3);
				var ec = economy.RetailPressure() + (10 - taxRates[(int)ZoneCategory.Commercial]) * 3 - shortage - vacC;
				if (pop > 0)
					ec += (AverageHappiness - 50) / 4;
				if (u > 8)
					ec += u - 8;
				if (pop == 0)
					ec = Math.Min(ec, 0);

				demandTarget[(int)ZoneCategory.Commercial] = Clamp100(ec);

				var ei = economy.IndustrialPressure() + (10 - taxRates[(int)ZoneCategory.Industrial]) * 3 - shortage - vacI;
				if (u > 6)
					ei += (u - 6) * 2;
				if (jc > 0 && ji * 3 < jc)
					ei += 10;

				demandTarget[(int)ZoneCategory.Industrial] = Clamp100(ei);

				var eo = 0;
				if (OfficeUnlocked)
				{
					eo = economy.OfficePressure() + (10 - taxRates[(int)ZoneCategory.Office]) * 3 - shortage - vacO;
					if (u > 8)
						eo += u - 8;
				}

				demandTarget[(int)ZoneCategory.Office] = Clamp100(eo);
			}

			for (var cat = 1; cat < 5; cat++)
			{
				var t = demandTarget[cat];
				var d = demand[cat];
				if (!demandInitialized)
					d = t;
				else if (t > d)
					d = Math.Min(t, d + Info.DemandRiseStep);
				else
					d = Math.Max(t, d - Info.DemandFallStep);

				demand[cat] = d;
			}

			demandInitialized = true;
		}
	}
}
