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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Money flows between four accounts (city, companies, households, outside). Every transfer goes through Move, so
	// the sum of all accounts only changes through the outside account. The company and household balances are
	// cross-checked against the real per-entity cash.
	public partial class CityEconomy
	{
		public const int LWages = 0, LRent = 1, LDividend = 2, LRetail = 3, LGoods = 4, LImport = 5, LExport = 6, LFreight = 7,
			LFeePower = 8, LFeeWater = 9, LFeeGarbage = 10, LTaxIncome = 11, LTaxCommercial = 12, LTaxIndustrial = 13, LTaxOffice = 14,
			LStartup = 15, LWriteoff = 16, LUpkeepReturn = 17, LWagesServices = 18, LPayout = 19, LSubsidy = 20,
			LTourism = 21, LFeeParking = 22, LTourismTax = 23, LCount = 24;

		public static readonly string[] LedgerNames =
		[
			"wages", "rent", "dividends", "retail-sales", "goods-trade", "imports", "exports", "freight",
			"fee-power", "fee-water", "fee-garbage", "tax-residential", "tax-commercial", "tax-industrial", "tax-office",
			"startup-capital", "write-off", "upkeep-return", "wages-services", "closing-payout", "subsidy", "tourism", "fee-parking", "tourism-tax",
		];

		// City-facing key per ledger category (null = private flow, not shown in the budget).
		static readonly string[] CityKeys =
		[
			null, null, null, null, null, null, null, null,
			"fee-power", "fee-water", "fee-garbage", "tax-residential", "tax-commercial", "tax-industrial", "tax-office",
			null, null, null, "wages-services", null, "subsidy", null, "fee-parking", "tourism-tax",
		];

		readonly long[] balance = new long[4];
		readonly long[] flowMonth = new long[LCount];
		readonly long[] flowLast = new long[LCount];
		readonly long[] cityPending = new long[LCount];
		readonly long[] taxByEduMonth = new long[5];
		readonly long[] taxByEduLast = new long[5];
		long externalNet;
		readonly long[] cumFlow = new long[LCount];

		// Per-resource statistics: units in milli, money in cents.
		long[] monthProduced, monthConsumed, monthImported, monthExported, monthImportCents, monthExportCents;
		long[] lastProduced, lastConsumed, lastImported, lastExported, lastImportCents, lastExportCents;
		int[] dayProduced, dayImported, dayNeed, dayUnmet;
		int[] emaProduced, emaImported, emaNeed, emaUnmet;

		void InitLedger()
		{
			consumerList = [.. Tables.ConsumerResources.Select(b => (int)b)];
			saleList = [.. Tables.SaleResources.Select(b => (int)b)];
		}

		void InitStats()
		{
			var n = Tables.ResourceCount + 1;
			monthProduced = new long[n];
			monthConsumed = new long[n];
			monthImported = new long[n];
			monthExported = new long[n];
			monthImportCents = new long[n];
			monthExportCents = new long[n];
			lastProduced = new long[n];
			lastConsumed = new long[n];
			lastImported = new long[n];
			lastExported = new long[n];
			lastImportCents = new long[n];
			lastExportCents = new long[n];
			dayProduced = new int[n];
			dayImported = new int[n];
			dayNeed = new int[n];
			dayUnmet = new int[n];
			emaProduced = new int[n];
			emaImported = new int[n];
			emaNeed = new int[n];
			emaUnmet = new int[n];
		}

		/// <summary>Transfers money between accounts and records it under a ledger category.</summary>
		void Move(Acct from, Acct to, int cat, long cents)
		{
			if (cents == 0)
				return;

			flowMonth[cat] += cents;
			cumFlow[cat] += cents;
			balance[(int)from] -= cents;
			balance[(int)to] += cents;
			if (to == Acct.City)
				cityPending[cat] += cents;
			else if (from == Acct.City)
				cityPending[cat] -= cents;

			if (from == Acct.Outside)
				externalNet += cents;
			else if (to == Acct.Outside)
				externalNet -= cents;
		}

		/// <summary>Books pending city flows into CityManager funds as whole dollars (the remainder carries over).</summary>
		void FlushCity()
		{
			for (var c = 0; c < LCount; c++)
			{
				var key = CityKeys[c];
				if (key == null)
					continue;

				var d = (int)(cityPending[c] / 100);
				if (d == 0)
					continue;

				cityPending[c] -= d * 100L;
				if (d > 0)
					cm.AddFunds(d, key);
				else
					cm.Charge(-d, key);
			}
		}

		/// <summary>Called by CityManager when it spent money on upkeep: a share returns to households as wages of city workers.</summary>
		internal void NoteUpkeepSpent(int dollars)
		{
			// With real citizens city workers are paid through ChargeWages (wages-services) instead.
			if (citizens != null)
				return;

			var back = (long)dollars * 100 * Info.UpkeepReturnPercent / 100;
			Move(Acct.Outside, Acct.Households, LUpkeepReturn, back);
			wagePool += back;
		}

		// ---- ICityEconomy money entry points used by CIT ----
		public void ChargeWages(int propertyId, int cents)
		{
			if (cents <= 0)
				return;

			if (companyByProperty.TryGetValue(propertyId, out var cid) && byId.TryGetValue(cid, out var c))
			{
				c.Cash -= cents;
				c.CostsDay += cents;
				c.ProfitMonth -= cents;
				Move(Acct.Companies, Acct.Households, LWages, cents);
			}
			else if (registry?.Get(propertyId)?.Kind == PropertyKind.Residential)
			{
				// BALANCE: home businesses are self-employed: their income comes from customers outside the city, not the city budget.
				Move(Acct.Outside, Acct.Households, LWages, cents);
			}
			else
			{
				// BALANCE: the city carries ServiceWageCityPercent of its own payroll; regional grants cover the rest.
				var cityShare = cents * Info.ServiceWageCityPercent / 100;
				Move(Acct.City, Acct.Households, LWagesServices, cityShare);
				Move(Acct.Outside, Acct.Households, LUpkeepReturn, cents - cityShare);
			}
		}

		public void BookIncomeTax(EducationLevel edu, int cents)
		{
			if (cents <= 0)
				return;

			taxByEduMonth[(int)edu] += cents;
			Move(Acct.Households, Acct.City, LTaxIncome, cents);
		}

		// ---- loans ----
		public int LoanLimit => Info.LoanLimits[Math.Clamp(cm?.MilestoneIndex ?? 0, 0, Info.LoanLimits.Length - 1)];

		/// <summary>Annual interest rate in tenths of a percent at the current utilisation.</summary>
		public int LoanRateTenths => EconomyMath.LoanRateTenths(LoanLimit > 0 ? LoanPrincipal * 100 / LoanLimit : 0, 0);

		void SetLoan(int wanted)
		{
			if (cm == null)
				return;

			wanted = Math.Min(wanted, LoanLimit);
			var delta = wanted - LoanPrincipal;
			if (delta > 0)
			{
				cm.AddFunds(delta, "loan");
				LoanPrincipal += delta;
			}
			else if (delta < 0)
			{
				var repay = cm.UnlimitedMoney ? -delta : Math.Min(-delta, Math.Max(0, cm.Funds));
				if (repay > 0 && cm.TrySpend(repay, "loan-repay"))
					LoanPrincipal -= repay;
			}
		}

		long interestCarry;

		/// <summary>Called once per calendar month by CityManager.EndMonth, before the monthly dictionaries are published.</summary>
		internal void SettleMonth()
		{
			TaxCompanies();

			if (LoanPrincipal > 0)
			{
				interestCarry += EconomyMath.LoanInterestCents(LoanPrincipal, LoanRateTenths);
				var d = (int)(interestCarry / 100);
				if (d > 0)
				{
					interestCarry -= d * 100L;
					cm.Charge(d, "loan-interest");
				}
			}

			if (cm.Funds < 0)
				cm.Charge((int)Math.Max(1, -(long)cm.Funds * Info.OverdraftPercent / 100), "overdraft-interest");

			FlushCity();
			RollMonthStats();
			CheckConservation();
		}

		void RollMonthStats()
		{
			PushHistory();
			Array.Copy(flowMonth, flowLast, LCount);
			Array.Clear(flowMonth);
			Array.Copy(taxByEduMonth, taxByEduLast, 5);
			Array.Clear(taxByEduMonth);
			(lastProduced, monthProduced) = (monthProduced, lastProduced);
			(lastConsumed, monthConsumed) = (monthConsumed, lastConsumed);
			(lastImported, monthImported) = (monthImported, lastImported);
			(lastExported, monthExported) = (monthExported, lastExported);
			(lastImportCents, monthImportCents) = (monthImportCents, lastImportCents);
			(lastExportCents, monthExportCents) = (monthExportCents, lastExportCents);
			Array.Clear(monthProduced);
			Array.Clear(monthConsumed);
			Array.Clear(monthImported);
			Array.Clear(monthExported);
			Array.Clear(monthImportCents);
			Array.Clear(monthExportCents);
			StateHash = EconomyMath.Mix(StateHash, (int)(flowLast[LWages] & 0x7fffffff));
		}

		/// <summary>Daily statistics roll: exponential averages (milli-units per day) feed spawn scoring and demand.</summary>
		void RollDayStats()
		{
			for (var r = 1; r <= Tables.ResourceCount; r++)
			{
				emaProduced[r] += (dayProduced[r] - emaProduced[r]) / 4;
				emaImported[r] += (dayImported[r] - emaImported[r]) / 4;
				emaNeed[r] += (dayNeed[r] - emaNeed[r]) / 4;
				emaUnmet[r] += (dayUnmet[r] - emaUnmet[r]) / 4;
				dayProduced[r] = 0;
				dayImported[r] = 0;
				dayNeed[r] = 0;
				dayUnmet[r] = 0;
			}
		}

		// ---- conservation ----
		public long CompanyMoney { get; private set; }

		public long HouseholdMoney { get; private set; }

		public long PrivateMoney => CompanyMoney + HouseholdMoney;

		/// <summary>0 when tracked balances match the real per-entity cash; otherwise the drift in cents.</summary>
		public long ConservationDrift { get; private set; }

		void CheckConservation()
		{
			long co = 0;
			for (var i = 0; i < companies.Count; i++)
				co += companies[i].Cash;

			var hh = wagePool;
			for (var i = 0; i < households.Count; i++)
				hh += households[i].Cash;

			CompanyMoney = co;
			HouseholdMoney = citizens == null ? hh : balance[(int)Acct.Households];
			ConservationDrift = Math.Abs(co - balance[(int)Acct.Companies]) + (citizens == null ? Math.Abs(hh - balance[(int)Acct.Households]) : 0);
		}

		// Read-only getters for the UI.

		/// <summary>Number of private-ledger categories (indices for LedgerLastMonth / LedgerThisMonth / LedgerName).</summary>
		public int LedgerCount => LCount;

		public string LedgerName(int category) { return LedgerNames[category]; }

		/// <summary>Income tax withheld from one education level last month, in cents.</summary>
		public long IncomeTaxLastMonth(EducationLevel edu) { return taxByEduLast[(int)edu]; }

		public int CompanyCountOf(CompanyKind kind)
		{
			var n = 0;
			for (var i = 0; i < companies.Count; i++)
				if (companies[i].Kind == kind)
					n++;

			return n;
		}

		public long LedgerLastMonth(int category) { return flowLast[category]; }

		public long LedgerThisMonth(int category) { return flowMonth[category]; }

		public long TradeProducedLast(int resource) { return lastProduced[resource]; }

		public long TradeConsumedLast(int resource) { return lastConsumed[resource]; }

		public long TradeImportedLast(int resource) { return lastImported[resource]; }

		public long TradeExportedLast(int resource) { return lastExported[resource]; }

		public long TradeImportCentsLast(int resource) { return lastImportCents[resource]; }

		public long TradeExportCentsLast(int resource) { return lastExportCents[resource]; }

		/// <summary>Trade balance in cents over the last month (exports minus imports minus freight).</summary>
		public long TradeBalanceLast => flowLast[LExport] - flowLast[LImport] - flowLast[LFreight];

		public long TradeBalanceThisMonth => flowMonth[LExport] - flowMonth[LImport] - flowMonth[LFreight];
	}
}
