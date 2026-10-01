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
	/// <summary>One company: plain data living on a property (design 05 section 3.2). Money in cents, stock in milli-units.</summary>
	public sealed class Company
	{
		public int Id;
		public int PropertyId;
		public CompanyKind Kind;
		public ZoneCategory Zone;

		/// <summary>Recipe index or -1 (extractors, shops reselling a good).</summary>
		public int Recipe = -1;
		public byte Output;
		public bool Resale;
		public bool SellsToHouseholds;

		public int Cash;
		public readonly int[] StockIn = new int[3];
		public int StockOut;
		public int StockCap;
		public int Throttle = 100;
		public int Efficiency = 100;
		public int JobsMax;
		public int SlotsFull;
		public int Credit;
		public int RentDay;
		public int MaintCarry;

		public bool Powered;

		/// <summary>Its property was removed (rebuild on level-up); waits to be adopted by the new property at the same origin.</summary>
		public bool Orphan;

		/// <summary>The property this company sits on (valid while not an orphan).</summary>
		public Property Prop;

		public CPos OrphanOrigin;
		public int OrphanWidth, OrphanDepth;
		public int OrphanUntil;
		public int WorkersNow;
		public readonly int[] Filled = new int[5];
		public bool Operational;

		public int SalesDay;
		public int CostsDay;
		public long ProfitMonth;
		public int ProfitEma;
		public int InsolventDays;
		public int Age;

		/// <summary>Average units sold per economy day, milli (shops restock against it).</summary>
		public int SoldEma;
		public int SoldDay;
		public int ProducedCarry;
		public int RentCarry;
	}

	public partial class CityEconomy
	{
		readonly List<Company> companies = [];
		readonly Dictionary<int, Company> byId = [];
		readonly Dictionary<int, int> companyByProperty = [];
		readonly Dictionary<int, int> reopenAt = [];
		readonly List<Company> closures = [];
		readonly List<Company> orphans = [];
		readonly List<Company> storages = [];
		int[] stockMilli;
		List<Company>[] sellers;
		List<Company>[] shops;
		int nextCompanyId = 1;
		int closedTotal;
		int adopted;
		int bankruptTotal;
		int expired;
		readonly int[] closedByKind = new int[5];
		int spawnedTotal;
		int fillPercent = 100;
		int[] demandMilli;
		int[] supplyMilli;
		int[] retailSupply;

		void EnsureLists()
		{
			if (sellers != null)
				return;

			sellers = new List<Company>[Tables.ResourceCount + 1];
			shops = new List<Company>[Tables.ResourceCount + 1];
			for (var i = 0; i < sellers.Length; i++)
			{
				sellers[i] = [];
				shops[i] = [];
			}

			stockMilli = new int[Tables.ResourceCount + 1];
			demandMilli = new int[Tables.ResourceCount + 1];
			supplyMilli = new int[Tables.ResourceCount + 1];
			retailSupply = new int[Tables.ResourceCount + 1];
		}

		static ZoneCategory ZoneOf(CompanyKind kind)
		{
			return kind == CompanyKind.Retail ? ZoneCategory.Commercial : kind == CompanyKind.Office ? ZoneCategory.Office : ZoneCategory.Industrial;
		}

		// Hubs of an extractor source (farms, forestry, mines, wells) are registered without a zone: they count as extractors.
		CompanyKind? KindFor(Property p)
		{
			if (p.Kind == PropertyKind.Warehouse)
				return localLogistics != null ? CompanyKind.Storage : null;

			if (p.Kind == PropertyKind.None && extractors != null && extractors.Product(p.Id) > 0)
				return CompanyKind.Extractor;

			switch (p.Kind)
			{
				case PropertyKind.Commercial: return CompanyKind.Retail;
				case PropertyKind.Industrial: return CompanyKind.Processor;
				case PropertyKind.Office: return CompanyKind.Office;
				case PropertyKind.Extractor: return CompanyKind.Extractor;
				default: return null;
			}
		}

		int SlotTotal(Company c, Property p, CityBuilding cb)
		{
			if (c.Kind == CompanyKind.Extractor && extractors != null)
			{
				var q = Math.Max(1, Tables.Resources[c.Output].Q);
				return Math.Max(1, (extractors.CapacityMilliPerDay(p.Id) + q - 1) / q);
			}

			var max = cb?.Info.MaxJobs ?? p.TotalJobSlots;
			if (c.Kind == CompanyKind.Retail && cm != null)
				max = cm.AdjustedJobs(ZoneCategory.Commercial, max);

			// A warehouse employs more people the more it stores (an empty one keeps a single caretaker).
			if (c.Kind == CompanyKind.Storage)
				max = Math.Min((max + 1) / 2, 1 + StoredUnits(c) / 100);

			// District policies (small business, industrial planning) change the jobs a company offers.
			var policy = c.Kind == CompanyKind.Retail ? PolicyAt("JobsCommercialPct", RoadOf(p))
				: c.Kind == CompanyKind.Processor || c.Kind == CompanyKind.Storage ? PolicyAt("JobsIndustrialPct", RoadOf(p)) : 0;
			if (policy != 0)
				max = max * Math.Max(0, 100 + policy) / 100;

			return Math.Max(1, max);
		}

		void RefreshJobs(Company c, Property p, CityBuilding cb)
		{
			// A throttled company lays off workers: its job slots shrink with the throttle (shops always keep all slots).
			var full = SlotTotal(c, p, cb);
			var target = c.Resale ? full : Math.Max(1, (full * Math.Max(20, c.Throttle) + 99) / 100);
			EconomyMath.SplitJobs(target, EconomyMath.JobMix(c.Kind, p.Level), p.JobSlots);
			c.JobsMax = target;
			c.SlotsFull = full;
			if (c.Kind == CompanyKind.Storage)
				return;

			var q = c.Resale ? Info.ResaleQ : Tables.Resources[c.Output].Q;
			c.StockCap = Math.Max(5000, (int)Math.Min(int.MaxValue / 4, (long)full * q * Info.StockDays));
		}

		int WorkersOf(Company c, Property p)
		{
			var total = 0;
			for (var e = 0; e < 5; e++)
			{
				var n = citizens != null ? p.JobsFilled[e] : p.JobSlots[e] * fillPercent / 100;
				c.Filled[e] = n;
				total += n;
			}

			return total;
		}

		bool Mine(Property p) { return p.Actor != null && p.Actor.Owner == self.Owner; }

		void OnPropertyAdded(Property p)
		{
			if (!Mine(p))
				return;

			if (p.Kind == PropertyKind.Residential && citizens == null)
				AddHousehold(p);

			// A rebuilt building (level-up replaces the actor) takes over the company of the property it replaces.
			for (var i = 0; i < orphans.Count; i++)
			{
				var c = orphans[i];
				if (KindFor(p) != c.Kind || !Overlaps(c.OrphanOrigin, c.OrphanWidth, c.OrphanDepth, p.Origin, p.Width, p.Depth))
					continue;

				orphans.RemoveAt(i);
				adopted++;
				c.Orphan = false;
				c.PropertyId = p.Id;
				c.Prop = p;
				companyByProperty[p.Id] = c.Id;
				p.CompanyId = c.Id;
				return;
			}
		}

		static bool Overlaps(CPos a, int aw, int ad, CPos b, int bw, int bd)
		{
			return a.X < b.X + bw && b.X < a.X + aw && a.Y < b.Y + bd && b.Y < a.Y + ad;
		}

		void OnPropertyRemoved(Property p)
		{
			if (companyByProperty.TryGetValue(p.Id, out var cid) && byId.TryGetValue(cid, out var c))
			{
				companyByProperty.Remove(p.Id);
				c.Orphan = true;
				c.OrphanOrigin = p.Origin;
				c.OrphanWidth = p.Width;
				c.OrphanDepth = p.Depth;
				c.OrphanUntil = world.WorldTick + ecoDayTicks * 6;
				orphans.Add(c);
			}

			RemoveHousehold(p.Id);
		}

		void ExpireOrphans(int tick)
		{
			for (var i = orphans.Count - 1; i >= 0; i--)
				if (orphans[i].OrphanUntil <= tick)
				{
					var c = orphans[i];
					orphans.RemoveAt(i);
					expired++;
					c.PropertyId = 0;
					CloseCompany(c, false);
				}
		}

		void FlushClosures()
		{
			if (closures.Count == 0)
				return;

			for (var i = 0; i < closures.Count; i++)
				CloseCompany(closures[i], true);

			closures.Clear();
		}

		void CloseCompany(Company c, bool cooldown)
		{
			if (!byId.Remove(c.Id))
				return;

			companies.Remove(c);
			companyByProperty.Remove(c.PropertyId);
			orphans.Remove(c);
			storages.Remove(c);
			if (sellers != null && c.Output > 0)
			{
				sellers[c.Output].Remove(c);
				shops[c.Output].Remove(c);
			}

			if (c.Cash > 0)
			{
				Move(Acct.Companies, Acct.Households, LPayout, c.Cash);
				wagePool += c.Cash;
			}
			else if (c.Cash < 0)
				Move(Acct.Outside, Acct.Companies, LWriteoff, -c.Cash);

			c.Cash = 0;
			var p = registry.Get(c.PropertyId);
			if (p != null)
			{
				p.CompanyId = 0;
				if (cooldown)
					reopenAt[p.Id] = world.WorldTick + Info.ReopenMonths * ticksPerMonth;
			}

			closedTotal++;
			closedByKind[(int)c.Kind]++;
			StateHash = EconomyMath.Mix(StateHash, c.Id * 31 + 7);
		}

		int InputCostPerUnit(RecipeDef rc, int outsideDist)
		{
			var cost = 0;
			for (var j = 0; j < rc.InputRes.Length; j++)
			{
				var r = Tables.Resources[rc.InputRes[j]];
				var local = sellers[r.Id].Count > 0;
				var price = local ? r.PriceCents + FreightCents(r.Weight, 10) : r.PriceCents * Info.ImportPercent / 100 + FreightCents(r.Weight, outsideDist);
				cost += price * rc.InputQty[j];
			}

			return cost;
		}

		int FreightCents(int weight, int cells)
		{
			return EconomyMath.FreightPerUnit(weight, cells, Info.FreightMilliCents * Info.MoneyScalePercent / 100);
		}

		int AvgWageCentsPerDay()
		{
			var s = 0;
			for (var i = 0; i < 5; i++)
				s += wageCents[i];

			return s / 5 / Math.Max(1, Info.DaysPerMonth);
		}

		// Chooses output resource and recipe for a new company on property p. Returns false when nothing is viable.
		bool ChooseProduct(Property p, CityBuilding cb, CompanyKind kind, out Company c)
		{
			c = null;
			var jobs = Math.Max(1, cb?.Info.MaxJobs ?? p.TotalJobSlots);
			if (kind == CompanyKind.Retail && cm != null)
				jobs = Math.Max(1, cm.AdjustedJobs(ZoneCategory.Commercial, jobs));

			var rentDay = RentDay(p, ZoneOf(kind));
			var overheadDay = jobs * AvgWageCentsPerDay() + rentDay;

			if (kind == CompanyKind.Storage)
			{
				c = new Company
				{
					Id = nextCompanyId++,
					PropertyId = p.Id,
					Kind = kind,
					Zone = ZoneCategory.Industrial,
					Cash = (int)Math.Min(int.MaxValue / 2, (long)Info.StartupDays * (overheadDay + 1)),
					Credit = (int)Math.Min(int.MaxValue / 2, (long)Info.CreditDays * (overheadDay + 1)),
				};
				return true;
			}

			var bestScore = int.MinValue;
			var bestRes = 0;
			var bestRecipe = -1;
			var bestResale = false;

			if (kind == CompanyKind.Extractor)
			{
				if (extractors == null)
					return false;

				bestRes = extractors.Product(p.Id);
				if (bestRes <= 0 || bestRes > Tables.ResourceCount)
					return false;
			}
			else if (kind == CompanyKind.Retail)
			{
				// Resale of shop goods, plus commercial recipes (restaurants, venues).
				for (var i = 0; i < saleList.Count; i++)
				{
					var r = Tables.Resources[saleList[i]];
					var rc = r.Recipe >= 0 ? Tables.Recipes[r.Recipe] : null;
					if (r.Sale == SaleMode.Shop)
					{
						var pull = Math.Max(0, emaNeed[r.Id] - retailSupply[r.Id]);
						var s = Score(r, pull, jobs * Info.ResaleQ, emaNeed[r.Id], retailSupply[r.Id]) + EconomyMath.Hash(p.Id, r.Id) % 64;
						if (pull * 4 >= jobs * Info.ResaleQ && s > bestScore)
						{
							bestScore = s;
							bestRes = r.Id;
							bestRecipe = -1;
							bestResale = true;
						}
					}
					else if (rc != null && rc.Zone == ZoneCategory.Commercial)
					{
						var pull = Math.Max(0, emaNeed[r.Id] - supplyMilli[r.Id]);
						var s = Score(r, pull, jobs * r.Q, emaNeed[r.Id], supplyMilli[r.Id]) + EconomyMath.Hash(p.Id, r.Id) % 64;
						if (pull * 4 >= jobs * r.Q && s > bestScore)
						{
							bestScore = s;
							bestRes = r.Id;
							bestRecipe = r.Recipe;
							bestResale = false;
						}
					}
				}
			}
			else
			{
				var zone = kind == CompanyKind.Office ? ZoneCategory.Office : ZoneCategory.Industrial;
				for (var i = 0; i < Tables.Recipes.Length; i++)
				{
					var rc = Tables.Recipes[i];
					if (rc.Zone != zone)
						continue;

					var r = Tables.Resources[rc.Output];
					var capacity = jobs * r.Q;
					var outsideDist = OutsideDistance(p);

					// Household demand for direct sales (finance, media) counts like industrial demand.
					var demand = demandMilli[r.Id] + (r.Sale == SaleMode.Direct ? emaNeed[r.Id] : 0);

					// Unmet local demand sells at the full price, otherwise the surplus is exported at a discount minus freight.
					var revenue = demand > supplyMilli[r.Id]
						? r.PriceCents - FreightCents(r.Weight, 10)
						: r.PriceCents * Info.ExportPercent / 100 - FreightCents(r.Weight, outsideDist);
					var margin = revenue - InputCostPerUnit(rc, outsideDist);
					var profitDay = (long)capacity * margin / 1000 - overheadDay;
					if (profitDay * 100 <= overheadDay * 15)
						continue;

					var bonus = (int)Math.Min(500, profitDay / 50) + EconomyMath.Hash(p.Id, r.Id) % 64;
					var s = Score(r, Math.Max(0, demand - supplyMilli[r.Id]), capacity, demand, supplyMilli[r.Id]) + bonus;
					if (s > bestScore || (s == bestScore && EconomyMath.Hash(p.Id, r.Id) % 2 == 0))
					{
						bestScore = s;
						bestRes = r.Id;
						bestRecipe = i;
					}
				}
			}

			if (bestRes == 0)
				return false;

			var res = Tables.Resources[bestRes];
			c = new Company
			{
				Id = nextCompanyId++,
				PropertyId = p.Id,
				Kind = kind,
				Zone = ZoneOf(kind),
				Recipe = bestRecipe,
				Output = (byte)bestRes,
				Resale = bestResale,
				SellsToHouseholds = res.Sale != SaleMode.None && (bestResale || kind == CompanyKind.Retail || kind == CompanyKind.Office),
			};

			long inputDay = 0;
			if (bestRecipe >= 0)
			{
				var rc = Tables.Recipes[bestRecipe];
				for (var j = 0; j < rc.InputRes.Length; j++)
					inputDay += (long)jobs * res.Q * rc.InputQty[j] * Tables.Resources[rc.InputRes[j]].PriceCents / 1000;
			}

			c.Cash = (int)Math.Min(int.MaxValue / 2, (long)Info.StartupDays * (overheadDay + 1) + 2 * inputDay);
			var credit = (long)Info.CreditDays * (overheadDay + 1) + Info.InputDays * inputDay;
			c.Credit = (int)Math.Min(int.MaxValue / 2, credit);
			return true;
		}

		// Score of a candidate: unmet demand the company could serve (cents per day, dominant), then how undersupplied the
		// resource is relative to its demand (0..999) so that even a saturated market picks the neediest product.
		static int Score(ResourceDef r, int pullMilli, int capacityMilli, int demand, int supply)
		{
			var served = Math.Min(pullMilli, capacityMilli);
			var cents = (int)Math.Min(int.MaxValue / 4000, (long)served * Math.Max(1, r.PriceCents) / 1000);
			var ratio = demand <= 0 ? 0 : Math.Clamp(demand * 100 / (supply + 1), 0, 999);
			return cents * 1000 + ratio;
		}

		/// <summary>Adds a company's potential output and input demand to the supply and demand tables (milli-units per day).</summary>
		void AddToBalance(Company c)
		{
			AddMaintenanceDemand(c);
			if (c.Kind == CompanyKind.Storage)
				return;

			var res = Tables.Resources[c.Output];
			var q = c.Resale ? Info.ResaleQ : res.Q;
			var cap = (int)Math.Min(int.MaxValue / 4, (long)c.JobsMax * q);
			if (c.Resale)
			{
				retailSupply[c.Output] += cap;
				demandMilli[c.Output] += c.SoldEma;
				return;
			}

			supplyMilli[c.Output] += cap;
			if (c.Recipe < 0)
				return;

			var rc = Tables.Recipes[c.Recipe];
			for (var j = 0; j < rc.InputRes.Length; j++)
				demandMilli[rc.InputRes[j]] += (int)Math.Min(int.MaxValue / 4, (long)cap * rc.InputQty[j]);
		}

		void RecountBalance()
		{
			Array.Clear(demandMilli);
			Array.Clear(supplyMilli);
			Array.Clear(retailSupply);
			Array.Clear(stockMilli);
			for (var i = 0; i < companies.Count; i++)
			{
				AddToBalance(companies[i]);
				AddStock(companies[i]);
			}

			AddWarehouseStock();
		}

		// Takes up to `need` cents from the households (the pool first, then a quarter of each household's cash, starting
		// at a deterministic offset) and returns what was raised.
		long DrawInvestment(long need, int salt)
		{
			var raised = Math.Min(Math.Max(0, wagePool), need);
			wagePool -= raised;
			var n = households.Count;
			for (var k = 0; k < n && raised < need; k++)
			{
				var h = households[(k + salt) % n];
				var take = Math.Min(Math.Max(0, h.Cash) / 4, need - raised);
				h.Cash -= take;
				raised += take;
			}

			return raised;
		}

		void TrySpawn(Property p, int tick)
		{
			if (p.CompanyId != 0 || !p.Operational || !p.HasRoadAccess || !Mine(p))
				return;

			var kind = KindFor(p);
			if (kind == null)
				return;

			if (reopenAt.TryGetValue(p.Id, out var at) && at > tick)
				return;

			EnsureLists();
			var cb = p.Actor?.TraitOrDefault<CityBuilding>();
			if (cb == null || !ChooseProduct(p, cb, kind.Value, out var c))
				return;

			c.Prop = p;
			companies.Add(c);
			byId[c.Id] = c;
			companyByProperty[p.Id] = c.Id;
			p.CompanyId = c.Id;
			if (c.Kind == CompanyKind.Storage)
				storages.Add(c);
			else
			{
				if (!c.Resale)
					sellers[c.Output].Add(c);

				if (c.SellsToHouseholds)
					shops[c.Output].Add(c);
			}

			// Investors (the households' money pool) fund the start-up; only the shortfall is new money from outside.
			var fromPool = citizens == null ? DrawInvestment(c.Cash, c.Id) : 0;
			Move(Acct.Households, Acct.Companies, LStartup, fromPool);
			Move(Acct.Outside, Acct.Companies, LStartup, c.Cash - fromPool);
			RefreshJobs(c, p, cb);
			AddToBalance(c);
			spawnedTotal++;
			StateHash = EconomyMath.Mix(StateHash, c.Id * 131 + c.Output);
		}

		void RefreshAggregates()
		{
			EnsureLists();
			var slots = 0;
			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];

				// BALANCE: a workplace without a company has no employer: its default slots are withdrawn so nobody is hired
				// into (and paid by the city for) an empty shell. Slots come back when a company moves in (RefreshJobs).
				if (citizens != null && p.CompanyId == 0 && p.TotalJobSlots > 0 && Mine(p) && KindFor(p) != null)
					Array.Clear(p.JobSlots);

				if (p.Kind != PropertyKind.Residential && p.Operational && Mine(p))
					slots += p.TotalJobSlots;
			}

			supplyFactor = 100;
			if (utilities == null && cm != null)
				supplyFactor = Math.Max(25, Math.Min(ShortageFactor(cm.PowerProduced, cm.PowerConsumed), ShortageFactor(cm.WaterProduced, cm.WaterConsumed)));
			fillPercent = slots <= 0 ? 0 : Math.Min(100, (int)((long)(cm?.Workers ?? 0) * 100 / slots));
			RefreshHouseholdAggregates();
			CountVacancy();
			RecountBalance();
			powerDemandPercent = climate?.PowerDemandPercent ?? 100;
		}

		static int ShortageFactor(int produced, int consumed)
		{
			return consumed <= 0 || produced >= consumed ? 100 : produced * 100 / consumed;
		}

		public Company CompanyOf(int propertyId)
		{
			return companyByProperty.TryGetValue(propertyId, out var cid) && byId.TryGetValue(cid, out var c) ? c : null;
		}

		public string DescribeCompany(int propertyId)
		{
			var c = CompanyOf(propertyId);
			if (c == null)
				return null;

			if (c.Kind == CompanyKind.Storage)
				return $"Storage: {c.WorkersNow}/{c.JobsMax} workers, stored {StoredUnits(c)} units, profit ${c.ProfitEma / 100}/mo";

			var res = Tables.Resources[c.Output];
			var role = c.Kind == CompanyKind.Retail && c.Resale ? "Shop" : c.Kind.ToString();
			var state = !c.Operational ? "idle" : c.InsolventDays > 0 ? "struggling" : c.WorkersNow < c.JobsMax / 2 ? "hiring" : "active";
			return $"{role}: {res.Key}, {c.WorkersNow}/{c.JobsMax} workers, eff {c.Efficiency}%, " +
				$"stock {c.StockOut / 1000}/{c.StockCap / 1000}, profit ${c.ProfitEma / 100}/mo, {state}";
		}
	}
}
