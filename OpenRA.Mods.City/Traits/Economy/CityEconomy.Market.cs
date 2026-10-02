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
	// Deterministic local market. Accepted freight reserves incoming goods; stock becomes usable only
	// when Logistics reports delivery. Reachable local suppliers serve demand before outside imports.
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

		/// <summary>Orders up to wantMilli. Returns accepted milli-units, not stock already delivered.</summary>
		int Buy(Company buyer, Property bp, int resource, int wantMilli)
		{
			if (localLogistics == null || wantMilli < 1000)
				return 0;

			var r = Tables.Resources[resource];
			var got = 0;
			var bpRoad = RoadOf(bp);
			var list = sellers[resource];
			var attempted = new HashSet<int>();
			var deferred = false;
			var waitingLocal = false;
			for (var round = 0; round < 6 && wantMilli - got >= 1000; round++)
			{
				var credit = (long)buyer.Cash + buyer.Credit;
				if (credit <= 0)
					break;

				Company best = null;
				var bestUnit = int.MaxValue;
				var bestTravel = int.MaxValue;
				for (var i = 0; i < list.Count; i++)
				{
					var seller = list[i];
					if (seller == buyer || seller.StockOut < 1000 || !seller.Operational || seller.Orphan || attempted.Contains(seller.Id))
						continue;

					var road = RoadOf(seller.Prop);
					var travel = r.Weight == 0 ? 0 : localLogistics.TravelTicks(road, bpRoad);
					if (travel == Logistics.RouteBusy)
						deferred = true;
					if (travel < 0)
						continue;

					var unit = Wholesale(r) + localLogistics.HaulCostCents(road, bpRoad, resource);
					if (unit < bestUnit || (unit == bestUnit && travel < bestTravel))
					{
						bestUnit = unit;
						bestTravel = travel;
						best = seller;
					}
				}

				var want = wantMilli - got;

				// Stored local goods compete with local producers; a cheaper outside offer never bypasses either.
				if (r.Tradable && TryStorage(buyer, bpRoad, resource, want, bestUnit, int.MaxValue, credit, ref got, ref deferred) > 0)
					continue;

				if (best == null)
					break;

				attempted.Add(best.Id);
				var units = (int)Math.Min(Math.Min(want, best.StockOut) / 1000, credit / Math.Max(1, bestUnit));
				if (units > 0)
				{
					var sent = OrderFreight(best, buyer, resource, units, Wholesale(r));
					got += sent;
					if (sent < units && best.StockOut >= 1000)
						waitingLocal = true;
				}
			}

			// A route search that was deferred is not a missing local supply chain. Retry later.
			if (!r.Tradable || deferred || waitingLocal || wantMilli - got < 1000)
				return got;

			var importFreight = localLogistics.OutsideFreightCents(bp.Id, resource, true);
			if (importFreight < 0)
				return got;

			var importPrice = Math.Max(1, Wholesale(r) * Info.ImportPercent / 100);
			var importUnits = (int)Math.Min(Math.Min(wantMilli - got, TradeBudgetMilli(false)) / 1000,
				Math.Max(0, (long)buyer.Cash + buyer.Credit) / Math.Max(1, importPrice + importFreight));
			if (importUnits > 0)
				got += OrderFreight(null, buyer, resource, importUnits, importPrice);

			return got;
		}

		int localNeedTick = -1;
		int[] localNeedMilli;

		int LocalNeedMilli(int resource)
		{
			if (localNeedTick == world.WorldTick)
				return localNeedMilli[resource];

			localNeedTick = world.WorldTick;
			localNeedMilli ??= new int[Tables.ResourceCount + 1];
			Array.Clear(localNeedMilli);
			foreach (var buyer in companies)
			{
				if (buyer.Orphan || !buyer.Operational)
					continue;

				if (buyer.Resale)
				{
					var target = Math.Min(buyer.StockCap, Math.Max(8000, buyer.SoldEma * Info.InputDays));
					var need = Math.Max(0, target - buyer.StockOut - buyer.InboundMilli(buyer.Output));
					localNeedMilli[buyer.Output] = (int)Math.Min(int.MaxValue / 4, (long)localNeedMilli[buyer.Output] + need);
				}
				else if (buyer.Recipe >= 0)
				{
					var recipe = Tables.Recipes[buyer.Recipe];
					for (var j = 0; j < recipe.InputRes.Length; j++)
					{
						var input = recipe.InputRes[j];
						var perDay = (long)buyer.WorkersNow * Tables.Resources[buyer.Output].Q * Math.Max(50, buyer.Efficiency) / 100;
						var target = Math.Min(int.MaxValue / 4, perDay * recipe.InputQty[j] * Info.InputDays);
						var need = Math.Max(0, target - buyer.StockIn[j] - buyer.InboundMilli(input));
						localNeedMilli[input] = (int)Math.Min(int.MaxValue / 4, localNeedMilli[input] + need);
					}
				}
			}

			return localNeedMilli[resource];
		}

		void ExportSurplus(Company c, Property p)
		{
			if (localLogistics == null || c.Resale || !c.Operational || c.StockCap <= 0 || c.Kind == CompanyKind.Storage)
				return;

			var r = Tables.Resources[c.Output];
			if (!r.Tradable)
				return;

			var unitFreight = localLogistics.OutsideFreightCents(p.Id, c.Output, false);
			var unitPrice = Wholesale(r) * Info.ExportPercent / 100;
			if (unitFreight < 0 || unitPrice <= unitFreight)
				return;

			var profitable = c.Recipe < 0 || unitPrice - unitFreight > InputCostPerUnit(Tables.Recipes[c.Recipe], OutsideDistance(p));
			var reserve = CropChangePending(c, p) ? 0 : Math.Max(LocalNeedMilli(c.Output), profitable
				? Math.Max(c.SoldEma * 3, c.StockCap * 20 / 100) : c.StockCap * 70 / 100);
			var units = Math.Min(c.StockOut - reserve, TradeBudgetMilli(true)) / 1000;
			if (units > 0)
				OrderFreight(c, null, c.Output, units, unitPrice);
		}

		// ---- shops (ICityEconomy) ----
		Company BestShop(int resource, CPos fromRoad)
		{
			if (resource < 1 || resource > Tables.ResourceCount || shops == null)
				return null;

			var r = Tables.Resources[resource];
			var list = shops[resource];
			Company best = null;
			var bestCost = long.MaxValue;
			var perCell = Info.ShopDistanceCents * Info.MoneyScalePercent / 100;
			for (var i = 0; i < list.Count; i++)
			{
				var s = list[i];
				if (!s.Operational || s.StockOut < 1000 || s.Orphan)
					continue;

				var sp = s.Prop;

				var travel = marketTraffic != null ? marketTraffic.EstimateTravelTicks(fromRoad, RoadOf(sp), TravelMode.Walk)
					: localLogistics?.TravelTicks(fromRoad, RoadOf(sp)) ?? -1;
				if (travel < 0)
					continue;

				var cost = Retail(r) + (long)travel * perCell;
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
