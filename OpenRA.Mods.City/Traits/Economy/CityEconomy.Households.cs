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
	/// <summary>Aggregate household of one residential building, standing in until a citizen simulation exists.</summary>
	public sealed class AggregateHousehold
	{
		public int PropertyId;
		public int Residents;
		public long Cash;
		public int[] Need;
	}

	// Aggregate households: wages and dividends flow into one pool that is shared out by residents; each household
	// pays fees, accrues needs and shops at the cheapest nearby shop with stock.
	public partial class CityEconomy
	{
		readonly List<AggregateHousehold> households = [];
		readonly Dictionary<int, AggregateHousehold> householdByProperty = [];
		long wagePool;
		long residentsTotal;
		int baseSpendPerResidentMonth;

		void AddHousehold(Property p)
		{
			if (householdByProperty.ContainsKey(p.Id))
				return;

			var h = new AggregateHousehold { PropertyId = p.Id, Need = new int[consumerList.Count] };
			households.Add(h);
			householdByProperty[p.Id] = h;
		}

		void RemoveHousehold(int propertyId)
		{
			if (!householdByProperty.TryGetValue(propertyId, out var h))
				return;

			wagePool += h.Cash;
			households.Remove(h);
			householdByProperty.Remove(propertyId);
		}

		void RefreshHouseholdAggregates()
		{
			if (citizens != null)
			{
				// With real citizens the needs are theirs; the economy only estimates them from the residents for demand.
				long sum = 0;
				var all = registry.All;
				for (var i = 0; i < all.Count; i++)
					if (all[i].Kind == PropertyKind.Residential && Mine(all[i]))
						sum += all[i].Residents;

				residentsTotal = sum;
				return;
			}

			if (baseSpendPerResidentMonth == 0)
				for (var k = 0; k < consumerList.Count; k++)
				{
					var r = Tables.Resources[consumerList[k]];
					baseSpendPerResidentMonth += (int)((long)r.Consumption * r.RetailCents / 1000);
				}

			long total = 0;
			for (var i = 0; i < households.Count; i++)
			{
				var h = households[i];
				var p = registry.Get(h.PropertyId);
				var cb = p?.Actor?.TraitOrDefault<CityBuilding>();
				h.Residents = cb != null && p.Operational ? cb.Residents : 0;
				total += h.Residents;
			}

			residentsTotal = total;
		}

		// CIT mode: estimated need of all residents for one economy day, added once per day for the demand statistics.
		void AccrueCitizenNeed()
		{
			var days = Math.Max(1, Info.DaysPerMonth);
			for (var k = 0; k < consumerList.Count; k++)
			{
				var r = Tables.Resources[consumerList[k]];
				dayNeed[r.Id] += (int)Math.Min(int.MaxValue / 4, residentsTotal * r.Consumption / days);
			}
		}

		void StepHouseholds(int tick)
		{
			if (citizens != null)
				return;

			for (var i = 0; i < households.Count; i++)
			{
				var h = households[i];
				if ((tick + h.PropertyId) % ecoDayTicks == 0 && h.Residents > 0)
					StepHousehold(h);
			}
		}

		void StepHousehold(AggregateHousehold h)
		{
			var p = registry.Get(h.PropertyId);
			if (p == null)
				return;

			// Share of the wage pool.
			if (wagePool > 0 && residentsTotal > 0)
			{
				var take = Math.Min(wagePool, wagePool * h.Residents / residentsTotal);
				wagePool -= take;
				h.Cash += take;
			}

			// Fees to the city.
			var cb = p.Actor.TraitOrDefault<CityBuilding>();
			var occupancy = cb != null ? Math.Min(100, h.Residents * 100 / Math.Max(1, cb.Info.MaxResidents)) : 100;
			var power = cb != null && cb.HasPower ? FeePerDay(cb.Info.PowerUse, occupancy, Info.PowerFee, feePercent[(int)FeeKind.Power], powerDemandPercent) : 0;
			var water = cb != null && cb.HasWater ? FeePerDay(cb.Info.WaterUse, occupancy, Info.WaterFee, feePercent[(int)FeeKind.Water], 100) : 0;
			var garbageMonth = (long)h.Residents * Info.GarbageFee * Info.MoneyScalePercent / 100 * feePercent[(int)FeeKind.Garbage] / 100;
			var garbage = (int)(garbageMonth / Math.Max(1, Info.DaysPerMonth));
			PayHouseholdFee(h, power, LFeePower);
			PayHouseholdFee(h, water, LFeeWater);
			PayHouseholdFee(h, garbage, LFeeGarbage);

			// Needs and shopping.
			var days = Math.Max(1, Info.DaysPerMonth);
			var spend = EconomyMath.SpendingPercent(h.Cash, (long)h.Residents * baseSpendPerResidentMonth);
			var road = RoadOf(p);
			for (var k = 0; k < consumerList.Count; k++)
			{
				var res = consumerList[k];
				var r = Tables.Resources[res];
				var accrual = (int)((long)h.Residents * r.Consumption * spend / 100 / days);
				h.Need[k] = Math.Min(h.Need[k] + accrual, 4000 + h.Residents * 40);
				dayNeed[res] += accrual;
				if (h.Need[k] < 1000)
					continue;

				var shop = BestShop(res, road);
				if (shop == null)
				{
					dayUnmet[res] += h.Need[k];
					continue;
				}

				var sold = Sell(shop, h.Need[k], (int)Math.Min(int.MaxValue / 2, Math.Max(0, h.Cash)), out var cents);
				h.Cash -= cents;
				h.Need[k] -= sold;
				if (h.Need[k] >= 1000)
					dayUnmet[res] += h.Need[k];
			}

			StateHash = EconomyMath.Mix(StateHash, (int)(h.Cash & 0x7fffffff) ^ h.PropertyId);
		}

		// With real citizens the households' cash lives in CIT, so the fees are billed to the city without debiting it
		// (booked as money from outside the economy's own accounts).
		void BillCitizenFees(Property p)
		{
			if (!Mine(p) || !p.Operational || p.Residents <= 0)
				return;

			var cb = p.Actor.TraitOrDefault<CityBuilding>();
			if (cb == null)
				return;

			var occupancy = Math.Min(100, p.Residents * 100 / Math.Max(1, cb.Info.MaxResidents));
			var power = cb.HasPower ? FeePerDay(cb.Info.PowerUse, occupancy, Info.PowerFee, feePercent[(int)FeeKind.Power], powerDemandPercent) : 0;
			var water = cb.HasWater ? FeePerDay(cb.Info.WaterUse, occupancy, Info.WaterFee, feePercent[(int)FeeKind.Water], 100) : 0;
			var garbageMonth = (long)p.Residents * Info.GarbageFee * Info.MoneyScalePercent / 100 * feePercent[(int)FeeKind.Garbage] / 100;
			Move(Acct.Outside, Acct.City, LFeePower, power);
			Move(Acct.Outside, Acct.City, LFeeWater, water);
			Move(Acct.Outside, Acct.City, LFeeGarbage, garbageMonth / Math.Max(1, Info.DaysPerMonth));
			BillParking(p);
		}

		void PayHouseholdFee(AggregateHousehold h, int fee, int cat)
		{
			if (fee <= 0)
				return;

			var pay = (int)Math.Min(fee, Math.Max(0, h.Cash));
			h.Cash -= pay;
			Move(Acct.Households, Acct.City, cat, pay);
		}
	}
}
