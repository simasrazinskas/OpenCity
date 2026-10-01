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
	/// <summary>Monthly per-resource figures kept in the 12-month history.</summary>
	public enum TradeField { Produced = 0, Consumed, Imported, Exported }

	// Design 05 phases 3-5: price rule, warehouses, tourist spending, building upkeep materials, specialisation from the
	// local production volume, rent from the zoning package and the 12-month production/trade history for the UI.
	public partial class CityEconomy
	{
		public const int HistoryMonths = 12;

		int[] priceMult;
		int concreteId;
		int timberId;
		int[][] history;
		string[][] historyKeys;
		int historyPos;
		int historyCount;
		ICityStatistics statistics;
		Logistics localLogistics;

		void InitExtras()
		{
			var n = Tables.ResourceCount + 1;
			priceMult = new int[n];
			Array.Fill(priceMult, 100);
			history = new int[4][];
			for (var f = 0; f < 4; f++)
				history[f] = new int[HistoryMonths * n];

			string[] fields = ["produced", "consumed", "imported", "exported"];
			historyKeys = new string[4][];
			for (var f = 0; f < 4; f++)
			{
				historyKeys[f] = new string[n];
				for (var r = 1; r < n; r++)
					historyKeys[f][r] = "res." + Tables.Resources[r].Key.ToLowerInvariant() + "." + fields[f];
			}

			concreteId = Tables.TryFind("Concrete", out var c) ? c : 0;
			timberId = Tables.TryFind("Timber", out var t) ? t : 0;
		}

		void InitExtrasLate()
		{
			statistics = Find<ICityStatistics>(self);
			localLogistics = self.TraitOrDefault<Logistics>();
		}

		// ---- prices ----

		/// <summary>Wholesale price in cents including the optional supply/demand multiplier.</summary>
		int Wholesale(ResourceDef r)
		{
			return Info.PriceElasticity <= 0 ? r.PriceCents : Math.Max(1, (int)((long)r.PriceCents * priceMult[r.Id] / 100));
		}

		int Retail(ResourceDef r)
		{
			return Info.PriceElasticity <= 0 ? r.RetailCents : Math.Max(1, (int)((long)r.RetailCents * priceMult[r.Id] / 100));
		}

		public int CurrentPriceCents(int resourceId) { return Wholesale(Tables.Resources[resourceId]); }

		/// <summary>Price multiplier in percent (100 while the price rule is off).</summary>
		public int PriceMultiplier(int resourceId) { return priceMult[resourceId]; }

		// Moves every price one point per economy day towards the level its shortage or surplus asks for.
		void UpdatePrices()
		{
			if (Info.PriceElasticity <= 0)
				return;

			var swing = Math.Clamp(Info.PriceMaxSwing, 0, 90);
			for (var r = 1; r <= Tables.ResourceCount; r++)
			{
				var def = Tables.Resources[r];
				long demand = demandMilli[r] + (def.Sale != SaleMode.None ? emaNeed[r] : 0);
				long supply = supplyMilli[r];
				var pressure = (int)((demand - supply) * 100 / (demand + supply + 1000));
				var target = Math.Clamp(100 + Info.PriceElasticity * pressure / 100, 100 - swing, 100 + swing);
				if (priceMult[r] < target)
					priceMult[r]++;
				else if (priceMult[r] > target)
					priceMult[r]--;
			}
		}

		// ---- zoning rent and building upkeep ----

		/// <summary>Rent per economy day in cents: the zoning package's published rent, else the economy's own formula.</summary>
		int RentDay(Property p, ZoneCategory zone)
		{
			if (Info.UseZoningRent && p.RentPerMonth > 0)
				return (int)Math.Min(int.MaxValue / 2, (long)p.RentPerMonth * 100 * Info.ZoningRentPercent / 100 / Math.Max(1, Info.DaysPerMonth));

			return EconomyMath.RentPerDay(p.LandValue, Info.ZoneRentFactor[(int)zone], p.Level, Math.Max(1, p.Width * p.Depth),
				Info.RentPercent, Info.DaysPerMonth, Info.MoneyScalePercent);
		}

		void BuyMaintenance(Company c, Property p, int cents)
		{
			var res = ((p.Id + c.Age) & 1) == 0 ? concreteId : timberId;
			if (res == 0 || !c.Operational || c.Cash < -c.Credit)
				return;

			var def = Tables.Resources[res];
			var total = (long)c.MaintCarry + cents;
			var want = (int)Math.Min(int.MaxValue / 4, total * 1000 / Wholesale(def));
			if (want < 500)
			{
				c.MaintCarry = (int)total;
				return;
			}

			var got = Buy(c, p, res, want);
			monthConsumed[res] += got;
			var spent = (long)got * Wholesale(def) / 1000;
			c.MaintCarry = (int)Math.Clamp(total - spent, 0, cents * 10L);
		}

		// Upkeep materials are demand like any input: half Concrete, half Timber, from the rent share.
		void AddMaintenanceDemand(Company c)
		{
			if (c.RentDay <= 0 || Info.MaintenanceRentPercent <= 0)
				return;

			var cents = (long)c.RentDay * Math.Clamp(Info.MaintenanceRentPercent, 0, 100) / 100 / 2;
			if (concreteId != 0)
				demandMilli[concreteId] += (int)Math.Min(int.MaxValue / 4, cents * 1000 / Wholesale(Tables.Resources[concreteId]));

			if (timberId != 0)
				demandMilli[timberId] += (int)Math.Min(int.MaxValue / 4, cents * 1000 / Wholesale(Tables.Resources[timberId]));
		}

		// ---- specialisation ----

		/// <summary>Efficiency bonus (percent points) for producing a resource in volume, from the logistics package's statistics.</summary>
		int SpecialisationBonus(int resource)
		{
			return resource <= 0 || localLogistics == null ? 0 : Math.Max(0, localLogistics.SpecializationPercent(resource) - 100);
		}

		void AddWarehouseStock()
		{
			if (localLogistics == null)
				return;

			for (var r = 1; r <= Tables.ResourceCount; r++)
				stockMilli[r] += (int)Math.Min(int.MaxValue / 2, localLogistics.WarehouseStockTotal(r) * 1000L);
		}

		void AddStock(Company c)
		{
			if (c.Kind == CompanyKind.Storage)
				return;

			if (c.Output > 0)
				stockMilli[c.Output] += c.StockOut;

			if (c.Recipe >= 0)
			{
				var rc = Tables.Recipes[c.Recipe];
				for (var j = 0; j < rc.InputRes.Length; j++)
					stockMilli[rc.InputRes[j]] += c.StockIn[j];
			}
		}

		/// <summary>Units of a resource in company stock across the city (output, input and warehouse stock), as of the last pulse.</summary>
		public int StockUnits(int resourceId)
		{
			return stockMilli == null || resourceId < 1 || resourceId > Tables.ResourceCount ? 0 : stockMilli[resourceId] / 1000;
		}

		// The logistics package owns the physical stock (capacity, balancing, trucks). A Storage company is the business on top:
		// it pays producers for surplus the warehouse still wants and charges buyers for what the warehouse sells.
		int StoredUnits(Company c)
		{
			var sum = 0;
			if (localLogistics == null)
				return sum;

			foreach (var r in localLogistics.WarehouseResources(c.PropertyId))
				sum += localLogistics.WarehouseStock(c.PropertyId, r);

			return sum;
		}

		// Buys surplus (stock above half the producer's capacity) from producers, within the warehouse's buy offer.
		void StepStorage(Company c)
		{
			if (!c.Operational || sellers == null || localLogistics == null || !localLogistics.IsWarehouse(c.PropertyId))
				return;

			for (var r = 1; r <= Tables.ResourceCount; r++)
			{
				var def = Tables.Resources[r];
				if (!def.Tradable || def.Weight <= 0)
					continue;

				var unit = localLogistics.BuyOfferCents(c.PropertyId, r, Wholesale(def));
				if (unit <= 0)
					continue;

				var perDay = Math.Max(1, localLogistics.WarehouseCapacity(c.PropertyId, r) * Info.StorageBuyPerDayPercent / 100);
				var left = Math.Min(localLogistics.BuyOfferUnits(c.PropertyId, r), perDay);
				var list = sellers[r];
				for (var i = 0; i < list.Count && left > 0; i++)
				{
					var s = list[i];
					if (s.Orphan || !s.Operational)
						continue;

					var affordable = ((long)c.Cash + c.Credit) / unit;
					var take = (int)Math.Min(Math.Min((s.StockOut - s.StockCap / 2) / 1000, left), affordable);
					if (take <= 0)
						continue;

					var sent = localLogistics.TryDispatch(s.PropertyId, c.PropertyId, r, take, unit);
					if (sent <= 0)
						continue;

					var value = sent * unit;
					s.StockOut -= sent * 1000;
					s.Cash += value;
					s.ProfitMonth += value;
					s.SalesDay += value;
					s.SoldDay += sent * 1000;
					c.Cash -= value;
					c.CostsDay += value;
					left -= sent;
					flowMonth[LGoods] += value;
				}
			}
		}

		// Offers a warehouse's stock to a buyer when it undercuts both the best local seller and the import. Returns the unit
		// price paid (0 = no deal) and adds the bought amount (milli-units) to `got`.
		int TryStorage(Company buyer, CPos buyerRoad, int resource, int want, int localUnit, int importUnit, long credit, ref int got)
		{
			if (localLogistics == null)
				return 0;

			var def = Tables.Resources[resource];
			Company best = null;
			var bestUnit = Math.Min(localUnit, importUnit);
			var bestCents = 0;
			for (var i = 0; i < storages.Count; i++)
			{
				var s = storages[i];
				if (s == buyer || s.Orphan || !s.Operational)
					continue;

				var cents = localLogistics.SellOfferCents(s.PropertyId, resource, Wholesale(def));
				if (cents <= 0)
					continue;

				var unit = cents + FreightCents(def.Weight, Manhattan(buyerRoad, RoadOf(s.Prop)));
				if (unit < bestUnit)
				{
					bestUnit = unit;
					best = s;
					bestCents = cents;
				}
			}

			if (best == null)
				return 0;

			var wanted = (int)Math.Min(Math.Min(want / 1000, localLogistics.SellOfferUnits(best.PropertyId, resource)), credit / bestUnit);
			if (wanted <= 0)
				return 0;

			var sold = localLogistics.TryDispatch(best.PropertyId, buyer.PropertyId, resource, wanted, bestCents);
			if (sold <= 0)
				return 0;

			var value = sold * bestCents;
			var freight = sold * (bestUnit - bestCents);
			best.Cash += value;
			best.ProfitMonth += value;
			best.SalesDay += value;
			buyer.Cash -= value + freight;
			buyer.CostsDay += value + freight;
			flowMonth[LGoods] += value;
			if (freight > 0)
				Move(Acct.Companies, Acct.Outside, LFreight, freight);

			got += sold * 1000;
			return bestUnit;
		}

		// ---- tourists ----

		/// <summary>Money tourists spent in the city (cents, reported by the citizen simulation via the progression package):
		/// the city collects a share as tourism tax.</summary>
		void ITouristSpendingSink.OnTouristSpending(int cents)
		{
			if (cents > 0)
				Move(Acct.Outside, Acct.City, LTourismTax, (long)cents * Math.Clamp(Info.TouristTaxPercent, 0, 100) / 100);
		}

		// Policy-driven city income and job changes (district policies, read through the progression provider).
		int PolicyAt(string key, CPos cell) { return progression != null ? progression.GetPolicy(key, cell) : 0; }

		void BillParking(Property p)
		{
			var fee = PolicyAt("ParkingFee", RoadOf(p));
			if (fee <= 0 || p.Residents <= 0)
				return;

			var month = (long)p.Residents * fee * Info.ParkingCentsPerPoint * Info.MoneyScalePercent / 100;
			Move(Acct.Outside, Acct.City, LFeeParking, month / Math.Max(1, Info.DaysPerMonth));
		}

		// Tourists (from the citizen simulation) buy lodging, meals, entertainment and recreation at the companies that offer
		// them. Their money comes from outside the city. Unserved wishes count as unmet need, which pulls new companies in.
		void StepTourists()
		{
			var tourists = citizens?.Tourists ?? 0;
			if (tourists <= 0 || shops == null)
				return;

			var days = Math.Max(1, Info.DaysPerMonth);
			for (var k = 0; k < saleList.Count; k++)
			{
				var def = Tables.Resources[saleList[k]];
				if (def.Tourist <= 0)
					continue;

				// Goods households buy are bought by tourists through the citizen simulation's own shopping.
				if (def.Consumption > 0)
					continue;

				var left = (int)Math.Min(int.MaxValue / 4, (long)tourists * def.Tourist / days);
				dayNeed[def.Id] += left;
				var list = shops[def.Id];
				for (var i = 0; i < list.Count && left >= 1; i++)
				{
					var s = list[i];
					if (s.Orphan || !s.Operational || s.StockOut < 1000)
						continue;

					var take = Math.Min(left, s.StockOut);
					var cents = (int)Math.Min(int.MaxValue / 2, ((long)take * Retail(def) + 999) / 1000);
					s.StockOut -= take;
					s.Cash += cents;
					s.ProfitMonth += cents;
					s.SalesDay += cents;
					s.SoldDay += take;
					Move(Acct.Outside, Acct.Companies, LTourism, cents);
					monthConsumed[def.Id] += take;
					left -= take;
				}

				if (left >= 1000)
					dayUnmet[def.Id] += left;
			}
		}

		void PushHistory()
		{
			var n = Tables.ResourceCount + 1;
			for (var r = 1; r < n; r++)
			{
				var at = historyPos * n + r;
				history[0][at] = Units(monthProduced[r]);
				history[1][at] = Units(monthConsumed[r]);
				history[2][at] = Units(monthImported[r]);
				history[3][at] = Units(monthExported[r]);
				if (statistics == null)
					continue;

				for (var f = 0; f < 4; f++)
					statistics.Record(historyKeys[f][r], history[f][at]);
			}

			historyPos = (historyPos + 1) % HistoryMonths;
			historyCount = Math.Min(HistoryMonths, historyCount + 1);
		}

		static int Units(long milli) { return (int)Math.Clamp(milli / 1000, 0, int.MaxValue); }

		/// <summary>Up to 12 completed months of one figure of a resource, oldest first (units).</summary>
		public int[] ResourceHistory(int resourceId, TradeField field, int months = HistoryMonths)
		{
			var count = Math.Min(Math.Min(months, HistoryMonths), historyCount);
			var result = new int[count];
			if (resourceId < 1 || resourceId > Tables.ResourceCount)
				return result;

			var n = Tables.ResourceCount + 1;
			for (var i = 0; i < count; i++)
			{
				var slot = ((historyPos - count + i) % HistoryMonths + HistoryMonths) % HistoryMonths;
				result[i] = history[(int)field][slot * n + resourceId];
			}

			return result;
		}

		/// <summary>Income tax percent for wages of an education level (same as IncomeTaxRate).</summary>
		public int IncomeTaxPercent(EducationLevel edu) { return IncomeTaxRate(edu); }
	}
}
