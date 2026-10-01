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
	// Deterministic local market plus import and export at the outside connections. Transfers are instant; freight
	// is paid by the buyer (and leaves the private sector). ILogistics.TryDispatch is told about every transfer so
	// that trucks can be shown.
	public partial class CityEconomy
	{
		readonly List<CPos> outsideCells = [];
		int tradeWindow = -1;
		int tradeUsedImport;
		int tradeUsedExport;

		void InitOutside()
		{
			outsideCells.Clear();
			foreach (var kv in world.ActorsWithTrait<OutsideConnection>())
				outsideCells.Add(kv.Actor.Location);
		}

		static int Manhattan(CPos a, CPos b) { return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); }

		static CPos RoadOf(Property p) { return p.AccessRoad != CPos.Zero ? p.AccessRoad : p.Origin; }

		int OutsideDistance(Property p)
		{
			if (outsideCells.Count == 0)
				return Info.DefaultOutsideDistance;

			var from = RoadOf(p);
			var best = int.MaxValue;
			for (var i = 0; i < outsideCells.Count; i++)
				best = Math.Min(best, Manhattan(from, outsideCells[i]));

			return best;
		}

		// Each connection carries TradeUnitsPerDay in each direction per economy day.
		int TradeBudgetMilli(bool export)
		{
			var window = world.WorldTick / ecoDayTicks;
			if (window != tradeWindow)
			{
				tradeWindow = window;
				tradeUsedImport = 0;
				tradeUsedExport = 0;
			}

			return Math.Max(0, Math.Max(1, outsideCells.Count) * Info.TradeUnitsPerDay * 1000 - (export ? tradeUsedExport : tradeUsedImport));
		}

		void Dispatch(int from, int to, int res, int milli, int price)
		{
			if (logistics != null && milli >= 1000)
				logistics.TryDispatch(from, to, res, milli / 1000, price);
		}

		/// <summary>Buys up to wantMilli of a resource for a company: cheapest landed price of nearby sellers and imports.</summary>
		int Buy(Company buyer, Property bp, int resource, int wantMilli)
		{
			var r = Tables.Resources[resource];
			var got = 0;
			var bpRoad = RoadOf(bp);
			var importDist = OutsideDistance(bp);
			var importUnit = Wholesale(r) * Info.ImportPercent / 100 + FreightCents(r.Weight, importDist);
			var list = sellers[resource];

			for (var round = 0; round < 6 && wantMilli - got >= 500; round++)
			{
				var credit = (long)buyer.Cash + buyer.Credit;
				if (credit <= 0)
					break;

				Company best = null;
				Property bestProp = null;
				var bestUnit = int.MaxValue;
				for (var i = 0; i < list.Count; i++)
				{
					var s = list[i];
					if (s == buyer || s.StockOut < 500 || !s.Operational || s.Orphan)
						continue;

					var sp = s.Prop;

					var unit = Wholesale(r) + FreightCents(r.Weight, Manhattan(bpRoad, RoadOf(sp)));
					if (unit < bestUnit)
					{
						bestUnit = unit;
						best = s;
						bestProp = sp;
					}
				}

				var want = wantMilli - got;

				// Warehouses sell their stored goods slightly above wholesale.
				if (storages.Count > 0)
				{
					var storeUnit = r.Tradable ? TryStorage(buyer, bpRoad, resource, want, bestUnit, importUnit, credit, ref got) : 0;
					if (storeUnit > 0)
						continue;
				}

				if (best != null && bestUnit <= importUnit)
				{
					var take = Math.Min(want, best.StockOut);
					take = (int)Math.Min(take, credit * 1000 / bestUnit);
					if (take <= 0)
						break;

					var value = Math.Max(1, (int)((long)take * Wholesale(r) / 1000));
					var freight = (int)((long)take * (bestUnit - Wholesale(r)) / 1000);
					best.StockOut -= take;
					best.Cash += value;
					best.ProfitMonth += value;
					best.SalesDay += value;
					best.SoldDay += take;
					buyer.Cash -= value + freight;
					buyer.CostsDay += value + freight;
					flowMonth[LGoods] += value;
					if (freight > 0)
						Move(Acct.Companies, Acct.Outside, LFreight, freight);

					Dispatch(best.PropertyId, buyer.PropertyId, resource, take, Wholesale(r));
					got += take;
					continue;
				}

				var budget = r.Tradable ? TradeBudgetMilli(false) : 0;
				if (budget <= 0)
					break;

				var amount = Math.Min(want, budget);
				amount = (int)Math.Min(amount, credit * 1000 / Math.Max(1, importUnit));
				if (amount <= 0)
					break;

				var cost = Math.Max(1, (int)((long)amount * (Wholesale(r) * Info.ImportPercent / 100) / 1000));
				var fr = (int)((long)amount * FreightCents(r.Weight, importDist) / 1000);
				buyer.Cash -= cost + fr;
				buyer.CostsDay += cost + fr;
				Move(Acct.Companies, Acct.Outside, LImport, cost);
				Move(Acct.Companies, Acct.Outside, LFreight, fr);
				tradeUsedImport += amount;
				dayImported[resource] += amount;
				monthImported[resource] += amount;
				monthImportCents[resource] += cost;
				Dispatch(0, buyer.PropertyId, resource, amount, Wholesale(r));
				got += amount;
				_ = bestProp;
			}

			return got;
		}

		// Output beyond what local buyers take is exported at a discount minus freight. Profitable exports keep only a
		// small local reserve; at a loss only a nearly full stock is dumped (the throttle then lays workers off).
		void ExportSurplus(Company c, Property p)
		{
			if (c.Resale || c.StockCap <= 0 || c.Kind == CompanyKind.Storage)
				return;

			var r = Tables.Resources[c.Output];
			if (!r.Tradable)
				return;

			var dist = OutsideDistance(p);
			var unitPrice = Wholesale(r) * Info.ExportPercent / 100;
			var unitFreight = FreightCents(r.Weight, dist);
			if (unitPrice <= unitFreight)
				return;

			var profitable = c.Recipe < 0 || unitPrice - unitFreight > InputCostPerUnit(Tables.Recipes[c.Recipe], dist);
			var reserve = profitable ? Math.Max(c.SoldEma * 3, c.StockCap * 5 / 100) : c.StockCap * 70 / 100;
			var take = Math.Min(c.StockOut - reserve, TradeBudgetMilli(true));
			if (take < 1000)
				return;

			var revenue = (int)((long)take * unitPrice / 1000);
			var fr = (int)((long)take * unitFreight / 1000);
			c.StockOut -= take;
			c.Cash += revenue - fr;
			c.SalesDay += revenue;
			Move(Acct.Outside, Acct.Companies, LExport, revenue);
			Move(Acct.Companies, Acct.Outside, LFreight, fr);
			tradeUsedExport += take;
			monthExported[c.Output] += take;
			monthExportCents[c.Output] += revenue;
			Dispatch(c.PropertyId, 0, c.Output, take, unitPrice);
		}

		// ---- shops (ICityEconomy) ----
		Company BestShop(int resource, CPos fromRoad)
		{
			if (resource < 1 || resource > Tables.ResourceCount || shops == null)
				return null;

			var r = Tables.Resources[resource];
			var list = shops[resource];
			Company best = null;
			var bestCost = int.MaxValue;
			var perCell = Info.ShopDistanceCents * Info.MoneyScalePercent / 100;
			for (var i = 0; i < list.Count; i++)
			{
				var s = list[i];
				if (!s.Operational || s.StockOut < 1000 || s.Orphan)
					continue;

				var sp = s.Prop;

				var cost = Retail(r) + Manhattan(fromRoad, RoadOf(sp)) * perCell;
				if (cost < bestCost)
				{
					bestCost = cost;
					best = s;
				}
			}

			return best;
		}

		public IReadOnlyList<int> ConsumerResources => consumerList;

		public int FindShop(int resourceId, CPos fromRoad)
		{
			var shop = BestShop(resourceId, fromRoad);
			if (shop == null && resourceId >= 1 && resourceId <= Tables.ResourceCount)
				dayUnmet[resourceId] += 1000;

			return shop?.PropertyId ?? 0;
		}

		public int SellToHousehold(int shopPropertyId, int resourceId, int wantedMilli, int maxCents, out int centsCharged)
		{
			centsCharged = 0;
			var s = CompanyOf(shopPropertyId);
			if (s == null || s.Output != resourceId || !s.SellsToHouseholds || wantedMilli <= 0)
				return 0;

			var sold = Sell(s, wantedMilli, maxCents, out centsCharged);
			if (sold < wantedMilli)
				dayUnmet[resourceId] += wantedMilli - sold;

			return sold;
		}

		int Sell(Company s, int wantedMilli, int maxCents, out int cents)
		{
			var r = Tables.Resources[s.Output];
			var units = Math.Min(wantedMilli, s.StockOut);
			if (maxCents >= 0)
				units = (int)Math.Min(units, (long)maxCents * 1000 / Retail(r));

			if (units <= 0)
			{
				cents = 0;
				return 0;
			}

			cents = (int)Math.Min(int.MaxValue / 2, ((long)units * Retail(r) + 999) / 1000);
			if (maxCents >= 0 && cents > maxCents)
				cents = maxCents;

			s.StockOut -= units;
			s.Cash += cents;
			s.ProfitMonth += cents;
			s.SalesDay += cents;
			s.SoldDay += units;
			Move(Acct.Households, Acct.Companies, LRetail, cents);
			monthConsumed[s.Output] += units;
			return units;
		}
	}
}
