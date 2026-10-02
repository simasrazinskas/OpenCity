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
	public partial class CityEconomy
	{
		sealed class PendingFreight
		{
			public Company Seller;
			public Company Buyer;
			public ShipmentEvent Load;
		}

		readonly Dictionary<int, PendingFreight> pendingFreight = [];
		readonly List<ShipmentEvent> acceptedLoads = [];
		bool collectingFreight;
		ITrafficService marketTraffic;

		void InitFreight()
		{
			marketTraffic = Find<ITrafficService>(self);
			if (localLogistics == null)
				return;

			localLogistics.Accepted += load =>
			{
				if (collectingFreight)
					acceptedLoads.Add(load);
			};
			localLogistics.Delivered += FreightDelivered;
			localLogistics.Returned += FreightReturned;
		}

		int OrderFreight(Company seller, Company buyer, int resource, int units, int price)
		{
			if (localLogistics == null || units <= 0)
				return 0;

			acceptedLoads.Clear();
			collectingFreight = true;
			try
			{
				localLogistics.TryDispatch(seller?.PropertyId ?? 0, buyer?.PropertyId ?? 0, resource, units, price);
			}
			finally
			{
				collectingFreight = false;
			}

			var acceptedMilli = 0;
			foreach (var load in acceptedLoads)
			{
				var milli = load.Units * 1000;
				var value = load.Units * load.UnitPriceCents;
				var freight = load.FreightCents;
				pendingFreight.Add(load.ShipmentId, new PendingFreight { Seller = seller, Buyer = buyer, Load = load });

				if (seller != null)
				{
					if (!load.FromStorage)
						seller.StockOut -= milli;

					seller.Cash += value - (buyer == null ? freight : 0);
					seller.ProfitMonth += value;
					seller.SalesDay += value;
					seller.SoldDay += milli;
				}

				if (buyer != null)
				{
					buyer.Cash -= value + freight;
					buyer.CostsDay += value + freight;
					if (!load.ToStorage)
						buyer.ReserveInbound(resource, milli);
				}

				if (seller == null)
				{
					Move(Acct.Companies, Acct.Outside, LImport, value);
					tradeUsedImport += milli;
					dayImported[resource] += milli;
					monthImported[resource] += milli;
					monthImportCents[resource] += value;
				}
				else if (buyer == null)
				{
					Move(Acct.Outside, Acct.Companies, LExport, value);
					tradeUsedExport += milli;
					monthExported[resource] += milli;
					monthExportCents[resource] += value;
				}
				else
					flowMonth[LGoods] += value;

				Move(Acct.Companies, Acct.Outside, LFreight, freight);
				acceptedMilli += milli;
			}

			StateHash = EconomyMath.Mix(StateHash, resource * 1009 + acceptedMilli);
			return acceptedMilli;
		}

		void FreightDelivered(ShipmentEvent load)
		{
			if (!pendingFreight.TryGetValue(load.ShipmentId, out var order))
				return;

			var buyer = order.Buyer;
			if (buyer != null && (!byId.ContainsKey(buyer.Id) || (!load.ToStorage && !ReceiveFreight(buyer, load.Resource, load.Units * 1000))))
			{
				// The business closed or changed its product before arrival. The truck takes
				// its cargo home; refund only once that return completes.
				localLogistics.RejectDelivery(load.ShipmentId);
				return;
			}

			pendingFreight.Remove(load.ShipmentId);

			StateHash = EconomyMath.Mix(StateHash, load.ShipmentId * 31 + 1);
		}

		bool ReceiveFreight(Company buyer, int resource, int milli)
		{
			return buyer.DeliverInbound(resource, milli, buyer.Recipe >= 0 ? Tables.Recipes[buyer.Recipe].InputRes : []);
		}

		void RefundCompany(Company company, int cents)
		{
			if (company == null)
				return;

			if (byId.ContainsKey(company.Id))
				company.Cash += cents;
			else
			{
				// A dissolved business already paid out its wallet. Its owners receive (or
				// repay) the later settlement, so no money becomes trapped on an orphan record.
				wagePool += cents;
				Move(Acct.Companies, Acct.Households, LPayout, cents);
			}
		}

		void FreightReturned(ShipmentEvent load)
		{
			if (!pendingFreight.Remove(load.ShipmentId, out var order))
				return;

			var original = order.Load;
			var resource = original.Resource;
			var milli = original.Units * 1000;
			var value = original.Units * original.UnitPriceCents;
			var freight = original.FreightCents;
			if (order.Buyer != null)
			{
				if (!original.ToStorage)
					order.Buyer.ReserveInbound(resource, -milli);

				RefundCompany(order.Buyer, value + freight);
				order.Buyer.CostsDay -= value + freight;
			}

			if (order.Seller != null)
			{
				if (!original.FromStorage && byId.ContainsKey(order.Seller.Id))
					order.Seller.StockOut += milli;

				RefundCompany(order.Seller, -value + (order.Buyer == null ? freight : 0));
				order.Seller.ProfitMonth -= value;
				order.Seller.SalesDay -= value;
				order.Seller.SoldDay -= milli;
			}

			if (order.Seller == null)
				Move(Acct.Companies, Acct.Outside, LImport, -value);
			else if (order.Buyer == null)
				Move(Acct.Outside, Acct.Companies, LExport, -value);
			else
				flowMonth[LGoods] -= value;

			Move(Acct.Companies, Acct.Outside, LFreight, -freight);
			StateHash = EconomyMath.Mix(StateHash, load.ShipmentId * 31 + 2);
		}
	}
}
