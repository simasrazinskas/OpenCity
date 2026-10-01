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
	// Warehouses and cargo terminals (design/06 3.8), monthly statistics for the production panel and the specialization bonus.
	//
	// A warehouse (property kind Warehouse, ZON zone 12) or a cargo terminal (actor with ICargoTerminal) is a node with its own stock.
	// Goods reach it through TryDispatch(seller, warehouseProperty, ...): Logistics clamps the units to the free space and puts them into
	// the stock on arrival. TryDispatch(warehouseProperty, buyer, ...) sells from the stock (units clamped to what is there).
	// Stock between warehouses of the same type is balanced once per clock day. ECO owns the money: it asks for offers and settles prices.
	public partial class Logistics
	{
		const int ResSlots = 64;
		const int StatSlots = 5;
		const int HistoryMonths = 12;

		readonly long[] statCurrent = new long[StatSlots * ResSlots];
		readonly int[] statHistory = new int[StatSlots * ResSlots * HistoryMonths];
		readonly int[] producedCarryMilli = new int[ResSlots];
		int historyPos;
		int historyCount;

		void AddStat(IndustryStat stat, int resource, long units)
		{
			statCurrent[(int)stat * ResSlots + Math.Clamp(resource, 0, ResSlots - 1)] += units;
		}

		/// <summary>The economy reports units produced (manufacturing). Extractor hubs report themselves, do not report those again.</summary>
		public void ReportProduced(int resourceId, int units) { ReportProducedMilli(resourceId, units * 1000); }

		public void ReportProducedMilli(int resourceId, int milli)
		{
			if (milli <= 0)
				return;

			var r = Math.Clamp(resourceId, 0, ResSlots - 1);
			producedCarryMilli[r] += milli;
			var units = producedCarryMilli[r] / 1000;
			producedCarryMilli[r] -= units * 1000;
			if (units > 0)
				AddStat(IndustryStat.Produced, r, units);
		}

		/// <summary>Units of a series: monthsAgo 0 = the running month, 1 = last month and so on (up to 12).</summary>
		public int GetStat(IndustryStat stat, int resourceId, int monthsAgo = 0)
		{
			var r = Math.Clamp(resourceId, 0, ResSlots - 1);
			if (monthsAgo <= 0)
				return (int)Math.Min(int.MaxValue, statCurrent[(int)stat * ResSlots + r]);

			if (monthsAgo > historyCount)
				return 0;

			var pos = ((historyPos - monthsAgo) % HistoryMonths + HistoryMonths) % HistoryMonths;
			return statHistory[((int)stat * ResSlots + r) * HistoryMonths + pos];
		}

		/// <summary>Up to `count` completed months of a series, oldest first (for the 12-month line chart).</summary>
		public IReadOnlyList<int> History(IndustryStat stat, int resourceId, int count)
		{
			var n = Math.Min(Math.Min(count, HistoryMonths), historyCount);
			var list = new List<int>(n);
			for (var m = n; m >= 1; m--)
				list.Add(GetStat(stat, resourceId, m));

			return list;
		}

		void RotateStats()
		{
			for (var i = 0; i < StatSlots * ResSlots; i++)
			{
				statHistory[i * HistoryMonths + historyPos] = (int)Math.Min(int.MaxValue, statCurrent[i]);
				statCurrent[i] = 0;
			}

			historyPos = (historyPos + 1) % HistoryMonths;
			historyCount = Math.Min(HistoryMonths, historyCount + 1);
		}

		/// <summary>
		/// City specialization (design/05): efficiency percent (100..100+SpecializationMaxPercent) of companies producing this resource, growing with the
		/// units the city produced per month (the larger of this month so far and last month). ECO multiplies production efficiency by it.
		/// </summary>
		public int SpecializationPercent(int resourceId)
		{
			var volume = Math.Max(GetStat(IndustryStat.Produced, resourceId, 0), GetStat(IndustryStat.Produced, resourceId, 1));
			var bonus = (long)Info.SpecializationMaxPercent * Math.Min(volume, Info.SpecializationThresholdUnits) / Math.Max(1, Info.SpecializationThresholdUnits);
			return 100 + (int)bonus;
		}

		int Capacity(LogiNode n, int resource)
		{
			if (n.Kind == NodeKind.Terminal)
				return n.StoragePerResource;

			var wc = WeightClass(resource);
			return wc == 0 ? 0 : n.CapBase / wc;
		}

		bool HasType(LogiNode n, int resource)
		{
			if (n.AnyType)
				return true;

			for (var i = 0; i < Math.Min(n.Types.Length, Info.WarehouseTypes); i++)
				if (n.Types[i] == resource)
					return true;

			return false;
		}

		bool EnsureType(LogiNode n, int resource)
		{
			if (HasType(n, resource))
				return true;

			if (n.Kind != NodeKind.Storage || WeightClass(resource) == 0)
				return false;

			for (var i = 0; i < Math.Min(n.Types.Length, Info.WarehouseTypes); i++)
				if (n.Types[i] == 0)
				{
					n.Types[i] = resource;
					return true;
				}

			return false;
		}

		int StockOf(LogiNode n, int resource)
		{
			return n.Stock != null && HasType(n, resource) ? n.Stock[Math.Min(resource, ResSlots - 1)] : 0;
		}

		int FreeSpace(LogiNode n, int resource)
		{
			if (n.Stock == null || !EnsureType(n, resource))
				return 0;

			var r = Math.Min(resource, ResSlots - 1);
			return Math.Max(0, Capacity(n, resource) - n.Stock[r] - n.Inbound[r]);
		}

		LogiNode StorageOf(int propertyId)
		{
			return nodeByProperty.TryGetValue(propertyId, out var n) && n.IsStorage && n.Alive ? n : null;
		}

		/// <summary>Property ids of all live warehouses and cargo terminals, in creation order.</summary>
		public IReadOnlyList<int> WarehouseProperties()
		{
			var list = new List<int>();
			foreach (var n in storageNodes)
				if (n.Alive)
					list.Add(n.PropertyId);

			return list;
		}

		public bool IsWarehouse(int propertyId) { return StorageOf(propertyId) != null; }

		public bool IsTerminal(int propertyId) { return StorageOf(propertyId)?.Kind == NodeKind.Terminal; }

		/// <summary>Resource ids a warehouse stores (terminals: every resource with stock).</summary>
		public IReadOnlyList<int> WarehouseResources(int propertyId)
		{
			var list = new List<int>();
			var n = StorageOf(propertyId);
			if (n == null)
				return list;

			if (n.AnyType)
			{
				for (var r = 1; r < ResSlots; r++)
					if (n.Stock[r] > 0)
						list.Add(r);
			}
			else
			{
				foreach (var t in n.Types)
					if (t > 0)
						list.Add(t);
			}

			return list;
		}

		public int WarehouseStock(int propertyId, int resourceId)
		{
			var n = StorageOf(propertyId);
			return n == null ? 0 : StockOf(n, resourceId);
		}

		public int WarehouseCapacity(int propertyId, int resourceId)
		{
			var n = StorageOf(propertyId);
			return n == null || !HasType(n, resourceId) ? 0 : Capacity(n, resourceId);
		}

		/// <summary>Units the warehouse still wants to buy (up to the target fill, counting goods on the way).</summary>
		public int BuyOfferUnits(int propertyId, int resourceId)
		{
			var n = StorageOf(propertyId);
			if (n == null || !HasType(n, resourceId) || !(registry?.Get(propertyId)?.Operational ?? true))
				return 0;

			var r = Math.Min(resourceId, ResSlots - 1);
			var target = Capacity(n, resourceId) * Info.WarehouseTargetPercent / 100;
			return Math.Max(0, target - n.Stock[r] - n.Inbound[r]);
		}

		/// <summary>Price (cents/unit) the warehouse pays a producer, 0 when it wants no more of the resource.</summary>
		public int BuyOfferCents(int propertyId, int resourceId, int basePriceCents)
		{
			return BuyOfferUnits(propertyId, resourceId) > 0 ? Math.Max(1, basePriceCents * Info.BuyDiscountPercent / 100) : 0;
		}

		public int SellOfferUnits(int propertyId, int resourceId)
		{
			var n = StorageOf(propertyId);
			return n == null || !(registry?.Get(propertyId)?.Operational ?? true) ? 0 : StockOf(n, resourceId);
		}

		/// <summary>Price (cents/unit) the warehouse asks a buyer, 0 when it has no stock.</summary>
		public int SellOfferCents(int propertyId, int resourceId, int basePriceCents)
		{
			return SellOfferUnits(propertyId, resourceId) > 0 ? Math.Max(1, basePriceCents * Info.SellMarkupPercent / 100) : 0;
		}

		public int WarehouseStockTotal(int resourceId)
		{
			long sum = 0;
			foreach (var n in storageNodes)
				if (n.Alive)
					sum += StockOf(n, resourceId);

			return (int)Math.Min(int.MaxValue, sum);
		}

		public int WarehouseCapacityTotal(int resourceId)
		{
			long sum = 0;
			foreach (var n in storageNodes)
				if (n.Alive && HasType(n, resourceId))
					sum += Capacity(n, resourceId);

			return (int)Math.Min(int.MaxValue, sum);
		}

		void AssignTypes()
		{
			// A free slot goes to the resource with the largest traffic (imports and dispatches this and last month),
			// divided by how many warehouses already store it, so warehouses appear where the economy is short.
			foreach (var n in storageNodes)
			{
				if (!n.Alive || n.Kind != NodeKind.Storage)
					continue;

				for (var slot = 0; slot < Math.Min(n.Types.Length, Info.WarehouseTypes); slot++)
				{
					if (n.Types[slot] != 0)
						continue;

					var best = 0;
					long bestScore = 0;
					for (var r = 1; r < ResSlots; r++)
					{
						if (WeightClass(r) == 0 || HasType(n, r))
							continue;

						long signal = GetStat(IndustryStat.Imported, r) + GetStat(IndustryStat.Imported, r, 1)
							+ GetStat(IndustryStat.Dispatched, r) + GetStat(IndustryStat.Dispatched, r, 1);
						if (signal <= 0)
							continue;

						var coverage = 0;
						foreach (var o in storageNodes)
							if (o.Alive && o.Kind == NodeKind.Storage && HasType(o, r))
								coverage++;

						var score = signal * 100 / (1 + coverage);
						if (score > bestScore)
						{
							bestScore = score;
							best = r;
						}
					}

					if (best == 0)
						break;

					n.Types[slot] = best;
				}
			}
		}

		void BalanceWarehouses()
		{
			for (var r = 1; r < ResSlots; r++)
			{
				LogiNode donor = null;
				foreach (var n in storageNodes)
				{
					if (!n.Alive || !HasType(n, r))
						continue;

					var cap = Capacity(n, r);
					if (cap > 0 && n.Stock[r] * 100 / cap > Info.BalanceHighPercent)
					{
						donor = n;
						break;
					}
				}

				if (donor == null)
					continue;

				var move = donor.Stock[r] * Info.BalanceMaxMovePercent / 100;
				while (move > 0)
				{
					// Emptiest receiver below the low mark first (ties: creation order).
					LogiNode recv = null;
					var recvFill = int.MaxValue;
					foreach (var n in storageNodes)
					{
						if (!n.Alive || n == donor || !HasType(n, r) || !(registry?.Get(n.PropertyId)?.Operational ?? true))
							continue;

						var cap = Capacity(n, r);
						if (cap <= 0)
							continue;

						var fill = n.Stock[r] * 100 / cap;
						if (fill < Info.BalanceLowPercent && fill < recvFill && FreeSpace(n, r) > 0)
						{
							recv = n;
							recvFill = fill;
						}
					}

					if (recv == null)
						break;

					var length = RouteLength(donor.Road, recv.Road);
					if (length < 0)
						break;

					var amount = Math.Min(move, FreeSpace(recv, r));
					var sent = Dispatch(donor, recv, r, amount, 0, WeightClass(r), length, false, false);
					if (sent <= 0)
						break;

					move -= sent;
				}
			}
		}

		string StorageReport()
		{
			var wh = 0;
			var term = 0;
			long stock = 0;
			foreach (var n in storageNodes)
			{
				if (!n.Alive)
					continue;

				if (n.Kind == NodeKind.Terminal)
					term++;
				else
					wh++;

				for (var r = 1; r < ResSlots; r++)
					stock += n.Stock[r];
			}

			return $"warehouses={wh} terminals={term} stored={stock} viaTerminals={tradedViaTerminals}";
		}
	}
}
