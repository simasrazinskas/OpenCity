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
using System.Globalization;
using System.Text;

namespace OpenRA.Mods.City.Traits
{
	// The autotest report line (counts, trade, money supply, ledger totals, StateHash). Deterministic content only.
	public partial class CityEconomy
	{
		static string D(long cents) { return (cents / 100).ToString(CultureInfo.InvariantCulture); }

		static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }

		static void AppendLedger(StringBuilder sb, string label, IReadOnlyDictionary<string, int> values)
		{
			sb.Append(label);
			if (values != null)
			{
				var keys = new List<string>(values.Keys);
				keys.Sort(StringComparer.Ordinal);
				for (var i = 0; i < keys.Count; i++)
					sb.Append(i > 0 ? "," : "").Append(keys[i]).Append('=').Append(values[keys[i]]);
			}

			sb.Append(']');
		}

		public string AutoTestReport()
		{
			CheckConservation();
			var counts = new int[5];
			var cashBy = new long[5];
			var profitBy = new long[5];
			var workers = 0;
			var slots = 0;
			for (var i = 0; i < companies.Count; i++)
			{
				var c = companies[i];
				counts[(int)c.Kind]++;
				cashBy[(int)c.Kind] += c.Cash;
				profitBy[(int)c.Kind] += c.ProfitEma;
				workers += c.WorkersNow;
				slots += c.JobsMax;
			}

			var sb = new StringBuilder();
			sb.Append("economy companies=").Append(companies.Count)
				.Append(" (ext/proc/shop/office=").Append(counts[0]).Append('/').Append(counts[1]).Append('/').Append(counts[2]).Append('/').Append(counts[3])
				.Append(" storage=").Append(counts[4]).Append(") jobs=").Append(workers).Append('/').Append(slots)
				.Append(" spawned=").Append(spawnedTotal).Append(" bankrupt=").Append(bankruptTotal).Append(" closed=").Append(closedTotal)
				.Append(" (").Append(closedByKind[0]).Append('/').Append(closedByKind[1]).Append('/').Append(closedByKind[2]).Append('/').Append(closedByKind[3])
				.Append('/').Append(closedByKind[4])
				.Append(") adopted=").Append(adopted).Append(" expired=").Append(expired).Append(" tourism$=").Append(D(cumFlow[LTourism]));

			sb.Append(" cash$(ext/proc/shop/office)=").Append(D(cashBy[0])).Append('/').Append(D(cashBy[1]))
				.Append('/').Append(D(cashBy[2])).Append('/').Append(D(cashBy[3]));
			sb.Append(" profit$/mo=").Append(D(profitBy[0])).Append('/').Append(D(profitBy[1]))
				.Append('/').Append(D(profitBy[2])).Append('/').Append(D(profitBy[3]));

			sb.Append(" trade$/mo imp=").Append(D(flowMonth[LImport])).Append(" exp=").Append(D(flowMonth[LExport]))
				.Append(" freight=").Append(D(flowMonth[LFreight])).Append(" balance=").Append(D(TradeBalanceThisMonth));

			sb.Append(" money$ co=").Append(D(CompanyMoney)).Append(" hh=").Append(D(HouseholdMoney))
				.Append(" supply=").Append(D(PrivateMoney)).Append(" drift=").Append(N(ConservationDrift))
				.Append(" external=").Append(D(externalNet));

			sb.Append(" cum$ startup=").Append(D(cumFlow[LStartup])).Append(" writeoff=").Append(D(cumFlow[LWriteoff]))
				.Append(" upkeep-return=").Append(D(cumFlow[LUpkeepReturn])).Append(" wages-svc=").Append(D(cumFlow[LWagesServices]));

			var pop = cm?.Population ?? 0;
			var income = cm?.LastMonthIncomeTotal ?? 0;
			sb.Append(" city$/mo income=").Append(income).Append(" expenses=").Append(cm?.LastMonthExpensesTotal ?? 0)
				.Append(" income-per-resident=").Append(pop > 0 ? N(income * 100L / pop) : "0").Append('c');

			sb.Append(" last-month$ wages=").Append(D(flowLast[LWages])).Append(" retail=").Append(D(flowLast[LRetail]))
				.Append(" tax-residential=").Append(D(flowLast[LTaxIncome]))
				.Append(" tax-c/i/o=").Append(D(flowLast[LTaxCommercial])).Append('/').Append(D(flowLast[LTaxIndustrial])).Append('/').Append(D(flowLast[LTaxOffice]))
				.Append(" fees=").Append(D(flowLast[LFeePower] + flowLast[LFeeWater] + flowLast[LFeeGarbage]));

			AppendLedger(sb, " city-income[", cm?.LastMonthIncome);
			AppendLedger(sb, " city-expenses[", cm?.LastMonthExpenses);
			sb.Append(" ships=").Append(logistics?.ShipmentsInTransit ?? 0);
			sb.Append(" loan=").Append(LoanPrincipal).Append(" mode=").Append(citizens != null ? "citizens" : "aggregate")
				.Append(" hash=").Append(StateHash);
			return sb.ToString();
		}
	}
}
