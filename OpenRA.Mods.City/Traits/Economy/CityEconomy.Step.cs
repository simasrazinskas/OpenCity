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
	// The daily company step. Every company runs once per economy day (1/30 of a CityClock month); the work is spread
	// over the ticks by (tick + id) % ecoDayTicks, so 5,000 companies cost about 60 updates per tick.
	public partial class CityEconomy
	{
		void StepProperties(int tick)
		{
			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];
				if ((tick + p.Id) % ecoDayTicks != 0)
					continue;

				if (p.Kind == PropertyKind.Residential)
				{
					if (citizens != null)
						BillCitizenFees(p);
					else
						BillParking(p);
				}
				else if (p.CompanyId == 0)
					TrySpawn(p, tick);
			}

			StepHouseholds(tick);
		}

		void StepCompanies(int tick)
		{
			for (var i = 0; i < companies.Count; i++)
			{
				var c = companies[i];
				if ((tick + c.Id) % ecoDayTicks == 0)
					StepCompany(c);
			}

			// Complete this tick's local procurement before considering outside sales.
			foreach (var c in companies)
				if (!c.Orphan && (tick + c.Id) % ecoDayTicks == 0 && c.Prop != null)
					ExportSurplus(c, c.Prop);
		}

		void StepCompany(Company c)
		{
			if (c.Orphan)
				return;

			c.Age++;
			var p = registry.Get(c.PropertyId);
			if (p == null)
			{
				closures.Add(c);
				return;
			}

			UpdateCrop(c, p);
			var cb = p.Actor?.TraitOrDefault<CityBuilding>();
			c.Operational = cb != null && p.Operational && cb.IsOperational;
			RefreshJobs(c, p, cb);

			// A utility network (NET) switches single buildings off through HasPower/HasWater; without one the legacy pooled
			// supply only slows every company down in proportion to the city-wide shortage.
			var powered = cb != null && (utilities == null || (cb.HasPower && cb.HasWater));
			c.Powered = powered;
			if (c.Operational && (powered || citizens != null))
				c.WorkersNow = WorkersOf(c, p);
			else
			{
				c.WorkersNow = 0;
				Array.Clear(c.Filled);
			}

			var spec = c.Kind == CompanyKind.Storage ? 0 : SpecialisationBonus(c.Output);
			c.Efficiency = powered && c.Operational ? EconomyMath.Efficiency(cm.AverageHappiness, (feePercent[0] + feePercent[1]) / 2, spec) * supplyFactor / 100 : 0;
			var cashBefore = c.Cash;
			c.SalesDay = 0;
			c.CostsDay = 0;

			if (c.Operational && powered)
			{
				PayWagesDirect(c);
				PayRent(c, p);
				PayFees(c, cb);
			}

			if (c.Kind == CompanyKind.Storage)
				StepStorage(c);
			else
			{
				Produce(c, p);
				Procure(c, p);
			}

			Adapt(c);
			var paidOut = Dividend(c);

			c.SoldEma += (c.SoldDay - c.SoldEma) / 8;
			c.SoldDay = 0;
			c.ProfitMonth += c.Cash - cashBefore + paidOut;
			StateHash = EconomyMath.Mix(StateHash, c.Id ^ c.Cash ^ (c.StockOut << 3) ^ c.WorkersNow);
		}

		// Without a citizen simulation companies pay wages every economy day and withhold income tax themselves.
		void PayWagesDirect(Company c)
		{
			if (citizens != null)
				return;

			long gross = 0;
			long tax = 0;
			for (var e = 0; e < 5; e++)
			{
				if (c.Filled[e] == 0)
					continue;

				var w = (long)c.Filled[e] * wageCents[e] / Math.Max(1, Info.DaysPerMonth);
				gross += w;
				tax += EconomyMath.Tax(w, IncomeTaxRate((EducationLevel)e));
			}

			if (gross == 0)
				return;

			c.Cash -= (int)gross;
			Move(Acct.Companies, Acct.Households, LWages, gross);
			Move(Acct.Households, Acct.City, LTaxIncome, tax);
			wagePool += gross - tax;
		}

		void PayRent(Company c, Property p)
		{
			var rent = RentDay(p, c.Zone);
			c.RentDay = rent;
			if (rent <= 0)
				return;

			// Part of the rent pays for the building's upkeep materials (Concrete and Timber) bought from the market, so it
			// re-enters the economy; the rest goes to the owners.
			var maint = (int)((long)rent * Math.Clamp(Info.MaintenanceRentPercent, 0, 100) / 100);
			var owners = rent - maint;
			c.Cash -= owners;
			if (citizens == null)
			{
				Move(Acct.Companies, Acct.Households, LRent, owners);
				wagePool += owners;
			}
			else
				Move(Acct.Companies, Acct.Outside, LRent, owners);

			if (maint > 0)
				BuyMaintenance(c, p, maint);

			c.RentCarry += rent;
			if (c.RentCarry >= 100)
			{
				registry.ReportRentPaid(p.Id, c.RentCarry / 100);
				c.RentCarry %= 100;
			}
		}

		// Utility fees: use units per month x fee price x fee percent, per economy day.
		void PayFees(Company c, CityBuilding cb)
		{
			var occupancy = c.JobsMax > 0 ? Math.Min(100, c.WorkersNow * 100 / c.JobsMax) : 0;
			var power = cb.HasPower ? FeePerDay(cb.Info.PowerUse, occupancy, Info.PowerFee, feePercent[(int)FeeKind.Power], powerDemandPercent) : 0;
			var water = cb.HasWater ? FeePerDay(cb.Info.WaterUse, occupancy, Info.WaterFee, feePercent[(int)FeeKind.Water], 100) : 0;
			c.Cash -= power + water;
			Move(Acct.Companies, Acct.City, LFeePower, power);
			Move(Acct.Companies, Acct.City, LFeeWater, water);
		}

		// Fee per economy day. `demandPercent` is the climate's power demand (heating and cooling) for power fees.
		int FeePerDay(int baseUse, int occupancyPercent, int feeCents, int feePct, int demandPercent)
		{
			if (baseUse <= 0)
				return 0;

			var use = Math.Max(1, (baseUse * (25 + 75 * occupancyPercent / 100) * demandPercent / 100 + 99) / 100);
			return (int)((long)use * feeCents * Info.MoneyScalePercent / 100 * feePct / 100 / Math.Max(1, Info.DaysPerMonth));
		}

		// Production from filled jobs, limited by input stock, output room, extractor capacity and the throttle.
		void Produce(Company c, Property p)
		{
			if (c.Resale || !c.Operational || c.WorkersNow <= 0 || c.Efficiency <= 0 || CropChangePending(c, p))
				return;

			var res = Tables.Resources[c.Output];
			var potential = (long)c.WorkersNow * res.Q * c.Efficiency / 100;
			if (c.Kind == CompanyKind.Extractor && extractors != null)
				potential = Math.Min(potential, extractors.CapacityMilliPerDay(p.Id));

			potential = Math.Min(potential, Math.Max(0, c.StockCap - c.StockOut));
			var rc = c.Recipe >= 0 ? Tables.Recipes[c.Recipe] : null;
			if (rc != null)
				for (var j = 0; j < rc.InputRes.Length; j++)
					potential = Math.Min(potential, c.StockIn[j] / rc.InputQty[j]);

			if (potential <= 0)
				return;

			var made = (int)potential;
			if (rc != null)
				for (var j = 0; j < rc.InputRes.Length; j++)
				{
					var used = made * rc.InputQty[j];
					c.StockIn[j] -= used;
					monthConsumed[rc.InputRes[j]] += used;
				}

			c.StockOut += made;

			// Manufacturing counts towards the city's specialisation; extractor hubs report themselves.
			if (c.Kind == CompanyKind.Processor)
				localLogistics?.ReportProducedMilli(c.Output, made);

			dayProduced[c.Output] += made;
			monthProduced[c.Output] += made;
			if (c.Kind == CompanyKind.Extractor && extractors != null)
			{
				c.ProducedCarry += made;
				if (c.ProducedCarry >= 1000)
				{
					extractors.OnProduced(p.Id, c.ProducedCarry / 1000);
					c.ProducedCarry %= 1000;
				}
			}
		}

		// Orders inputs (or restocks a shop) from the market for the next days.
		void Procure(Company c, Property p)
		{
			if (!c.Operational || !c.Powered || c.Cash < -c.Credit)
				return;

			var res = Tables.Resources[c.Output];
			if (c.Resale)
			{
				var target = Math.Min(c.StockCap, Math.Max(8000, c.SoldEma * Info.InputDays));
				var need = target - c.StockOut - c.InboundMilli(c.Output);
				if (need >= 1000)
					_ = Buy(c, p, c.Output, need);

				return;
			}

			if (c.Recipe < 0 || c.WorkersNow <= 0)
				return;

			var rc = Tables.Recipes[c.Recipe];
			var perDay = (long)c.WorkersNow * res.Q * Math.Max(50, c.Efficiency) / 100;
			for (var j = 0; j < rc.InputRes.Length; j++)
			{
				var target = Math.Min(int.MaxValue / 4, perDay * rc.InputQty[j] * Info.InputDays);
				var need = (int)Math.Max(0, target - c.StockIn[j] - c.InboundMilli(rc.InputRes[j]));
				if (need >= 1000)
					_ = Buy(c, p, rc.InputRes[j], need);
			}
		}

		void Adapt(Company c)
		{
			if (!c.Resale && c.StockCap > 0 && c.Kind != CompanyKind.Storage)
			{
				if (c.StockOut > c.StockCap * 85 / 100)
					c.Throttle = Math.Max(20, c.Throttle - 10);
				else if (c.StockOut < c.StockCap * 60 / 100)
					c.Throttle = Math.Min(100, c.Throttle + 5);
			}

			if (c.Cash < 0 && c.Operational && c.Powered)
			{
				c.InsolventDays++;
				if (c.Kind == CompanyKind.Extractor)
					c.Throttle = Math.Max(20, c.Throttle - 5);
				else if (c.InsolventDays >= Info.CloseAfterInsolventDays && c.Kind != CompanyKind.Storage)
				{
					closures.Add(c);
					bankruptTotal++;
				}
			}
			else if (c.Cash >= 0)
				c.InsolventDays = 0;
		}

		// Cash above the buffer flows to the owners (households), keeping the money supply bounded.
		int Dividend(Company c)
		{
			if (citizens != null || c.Cash <= 0 || c.Output == 0)
				return 0;

			var daily = c.JobsMax * AvgWageCentsPerDay() + 1;
			var excess = c.Cash - (long)Info.CashBufferDays * daily;
			if (excess <= 0)
				return 0;

			var pay = (int)(excess * Info.DividendPercent / 100);
			if (pay <= 0)
				return 0;

			c.Cash -= pay;
			Move(Acct.Companies, Acct.Households, LDividend, pay);
			wagePool += pay;
			return pay;
		}

		// Monthly profit tax, called from SettleMonth.
		void TaxCompanies()
		{
			for (var i = 0; i < companies.Count; i++)
			{
				var c = companies[i];
				var rate = ProfitTaxRate(c.Zone, c.Output);
				var tax = EconomyMath.Tax(Math.Max(0, c.ProfitMonth), rate);
				if (tax != 0)
				{
					c.Cash -= (int)tax;
					var cat = c.Zone == ZoneCategory.Commercial ? LTaxCommercial : c.Zone == ZoneCategory.Office ? LTaxOffice : LTaxIndustrial;
					Move(Acct.Companies, Acct.City, cat, tax);
				}

				c.ProfitEma = (int)Math.Clamp((c.ProfitEma * 2L + c.ProfitMonth) / 3, int.MinValue / 2, int.MaxValue / 2);
				c.ProfitMonth = 0;
			}
		}
	}
}
