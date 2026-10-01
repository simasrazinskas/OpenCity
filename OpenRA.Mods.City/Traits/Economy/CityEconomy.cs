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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Companies, resources, trade, wages, taxes, fees and loans (design/05-economy.md). Needs the PropertyRegistry.")]
	public class CityEconomyInfo : TraitInfo
	{
		[Desc("Percent applied to wages, prices, fees and rents so that a CityClock month (2,400 ticks) feels right.")]
		public readonly int MoneyScalePercent = 320;

		[Desc("Economy days per CityClock day (= month). Companies step once per economy day, spread over the ticks.")]
		public readonly int DaysPerMonth = 30;

		[Desc("Monthly wage in dollars per education level 0..4 (before MoneyScalePercent).")]
		public readonly int[] Wages = [25, 30, 35, 40, 45];

		[Desc("Import price as percent of wholesale, export price as percent of wholesale.")]
		public readonly int ImportPercent = 100;
		public readonly int ExportPercent = 100;

		[Desc("Freight in milli-cents per weight class per cell (before MoneyScalePercent).")]
		public readonly int FreightMilliCents = 25;

		[Desc("Units per outside connection that can be imported, and again exported, per economy day.")]
		public readonly int TradeUnitsPerDay = 2000;

		[Desc("Cells assumed to the outside when the map has no OutsideConnection.")]
		public readonly int DefaultOutsideDistance = 24;

		[Desc("Days of input consumption companies keep in stock, and days of production a stock holds.")]
		public readonly int InputDays = 5;
		public readonly int StockDays = 14;

		[Desc("Sales capacity of a shop worker in milli-units per day.")]
		public readonly int ResaleQ = 4000;

		[Desc("Cents (before scaling) a household adds per road cell to a shop's price when choosing.")]
		public readonly int ShopDistanceCents = 3;

		[Desc("Rent factor per zone category (commercial, industrial, office) and percent of the CS2 rent formula.")]
		public readonly int[] ZoneRentFactor = [0, 0, 6, 3, 8];
		public readonly int RentPercent = 40;

		[Desc("Startup capital of a new company in days of payroll+rent, and the cash kept before dividends flow to households (days).")]
		public readonly int StartupDays = 15;
		public readonly int CashBufferDays = 60;

		[Desc("Percent of cash above the buffer paid out as dividends per economy day.")]
		public readonly int DividendPercent = 4;

		[Desc("Consecutive economy days with negative cash after which a (non-extractor) company closes.")]
		public readonly int CloseAfterInsolventDays = 45;

		[Desc("Months a vacant building waits before a new company moves in after a closure.")]
		public readonly int ReopenMonths = 2;

		[Desc("Default fees in cents (before scaling): power and water per use unit per month, garbage per resident per month.")]
		public readonly int PowerFee = 130;
		public readonly int WaterFee = 150;
		public readonly int GarbageFee = 30;

		[Desc("Percent of the city's road and service upkeep that flows back to households as wages of city workers.")]
		public readonly int UpkeepReturnPercent = 55;

		[Desc("Loan limit in dollars by milestone index.")]
		public readonly int[] LoanLimits = [10000, 25000, 60000, 120000, 250000, 500000, 1000000, 2000000];

		[Desc("Overdraft interest in percent of the negative balance per month.")]
		public readonly int OverdraftPercent = 2;

		[Desc("Percent of every company's rent that buys Concrete and Timber for building upkeep (the rest goes to the owners).")]
		public readonly int MaintenanceRentPercent = 25;

		[Desc("Use the rent the zoning package publishes (Property.RentPerMonth, dollars) instead of the economy's own formula.",
			"The percent scales it into the economy's money (100 = as published).")]
		public readonly bool UseZoningRent = true;
		public readonly int ZoningRentPercent = 100;

		[Desc("Optional supply/demand price rule: prices move up to +-PriceMaxSwing percent with the market's shortage or surplus.",
			"0 disables it (prices stay constant).")]
		public readonly int PriceElasticity = 0;
		public readonly int PriceMaxSwing = 25;

		[Desc("Warehouses (stock and prices belong to the logistics package): share of a resource's capacity a warehouse company buys per economy day.")]
		public readonly int StorageBuyPerDayPercent = 10;

		[Desc("Percent of the money tourists spend (reported by the citizen simulation) that the city collects as tourism tax.")]
		public readonly int TouristTaxPercent = 10;

		[Desc("ParkingFee policy: cents per resident per month for each point of the slider (before MoneyScalePercent).")]
		public readonly int ParkingCentsPerPoint = 4;

		[Desc("Days of payroll and rent a company may owe suppliers before it stops buying.")]
		public readonly int CreditDays = 30;

		[FieldLoader.LoadUsing(nameof(LoadResources))]
		public readonly List<ResourceDef> Resources = [];

		[FieldLoader.LoadUsing(nameof(LoadRecipes))]
		public readonly List<RecipeDef> Recipes = [];

		static object LoadResources(MiniYaml yaml)
		{
			var ret = new List<ResourceDef>();
			var node = yaml.NodeWithKeyOrDefault("Resources");
			if (node != null)
				foreach (var n in node.Value.Nodes)
					ret.Add(new ResourceDef(n.Value) { Key = n.Key });

			return ret;
		}

		static object LoadRecipes(MiniYaml yaml)
		{
			var ret = new List<RecipeDef>();
			var node = yaml.NodeWithKeyOrDefault("Recipes");
			if (node != null)
				foreach (var n in node.Value.Nodes)
					ret.Add(new RecipeDef(n.Value) { Key = n.Key });

			return ret;
		}

		EconomyTables tables;

		/// <summary>Resolved resource and recipe tables, built once and shared by every player's economy (immutable afterwards).</summary>
		public EconomyTables GetTables() { return tables ??= new EconomyTables(Resources, Recipes, MoneyScalePercent); }

		public override object Create(ActorInitializer init) { return new CityEconomy(init.Self, this); }
	}

	public enum Acct { City = 0, Companies, Households, Outside }

	public partial class CityEconomy : IResolveOrder, ITick, ISync, ICityEconomy, ICityAutoTestReporter, ITouristSpendingSink
	{
		public readonly CityEconomyInfo Info;
		public readonly EconomyTables Tables;
		readonly Actor self;
		readonly World world;
		readonly int[] feePercent = [100, 100, 100];
		readonly int[] serviceBudget = new int[13];
		readonly int[] eduTaxRate = new int[5];
		readonly int[] resourceTaxRate;
		readonly int[] wageCents = new int[5];

		CityManager cm;
		IPropertyRegistry registry;
		ICitizenPopulation citizens;
		ILogistics logistics;
		IExtractorSource extractors;
		IProgression progression;
		IUtilityNetwork utilities;
		int supplyFactor = 100;
		int powerDemandPercent = 100;
		CityClimate climate;
		ICityServices services;
		CityClock clock;
		bool initialized;
		int ecoDayTicks = 80;
		int ticksPerMonth = 2400;

		public CityEconomy(Actor self, CityEconomyInfo info)
		{
			this.self = self;
			world = self.World;
			Info = info;
			Tables = info.GetTables();
			resourceTaxRate = new int[Tables.ResourceCount + 1];
			Array.Fill(resourceTaxRate, EconomyOrders.UnsetRate);
			Array.Fill(eduTaxRate, EconomyOrders.UnsetRate);
			Array.Fill(serviceBudget, 100);
			for (var i = 0; i < 5; i++)
				wageCents[i] = Math.Max(1, info.Wages[Math.Min(i, info.Wages.Length - 1)] * 100 * info.MoneyScalePercent / 100);

			InitLedger();
			InitStats();
			InitExtras();
		}

		static T Find<T>(Actor self) where T : class
		{
			return self.World.WorldActor.TraitsImplementing<T>().FirstOrDefault() ?? self.TraitsImplementing<T>().FirstOrDefault();
		}

		void Init()
		{
			initialized = true;
			cm = self.TraitOrDefault<CityManager>();
			registry = Find<IPropertyRegistry>(self);
			citizens = Find<ICitizenPopulation>(self);
			logistics = Find<ILogistics>(self);
			extractors = Find<IExtractorSource>(self);
			services = Find<ICityServices>(self);
			utilities = Find<IUtilityNetwork>(self);
			progression = Find<IProgression>(self);
			InitExtrasLate();
			climate = world.WorldActor.TraitOrDefault<CityClimate>();
			clock = world.WorldActor.TraitOrDefault<CityClock>();
			ticksPerMonth = clock?.TicksPerMonth ?? 2400;
			ecoDayTicks = Math.Max(1, ticksPerMonth / Math.Max(1, Info.DaysPerMonth));
			if (registry != null)
			{
				registry.Removed += OnPropertyRemoved;
				for (var i = 0; i < registry.All.Count; i++)
					OnPropertyAdded(registry.All[i]);

				registry.Added += OnPropertyAdded;
			}

			InitOutside();
			cm?.AttachEconomy(this);
		}

		/// <summary>True when a citizen simulation drives households (otherwise aggregate households stand in).</summary>
		public bool HasCitizens => citizens != null;

		[VerifySync]
		public int StateHash { get; private set; } = 17;

		[VerifySync]
		public int CompanyCount => companies.Count;

		[VerifySync]
		public int LoanPrincipal { get; private set; }

		/// <summary>True when a services provider books the budget slider's upkeep adjustments itself (CityManager must not).</summary>
		internal bool ServicesBookBudget => services != null;

		public int FeePercent(FeeKind kind) { return feePercent[(int)kind]; }

		public int ServiceBudgetPercent(ServiceKind kind)
		{
			var s = services?.GetBudget(kind) ?? 0;
			return s > 0 ? s : serviceBudget[(int)kind];
		}

		/// <summary>Income tax rate (percent) applied to wages of this education level.</summary>
		public int IncomeTaxRate(EducationLevel edu)
		{
			var r = eduTaxRate[(int)edu];
			return EconomyMath.ClampTax(r == EconomyOrders.UnsetRate ? cm?.GetTaxRate(ZoneCategory.Residential) ?? 10 : r);
		}

		/// <summary>Profit tax rate (percent) of companies producing a resource in a zone category.</summary>
		public int ProfitTaxRate(ZoneCategory zone, int resourceId)
		{
			var r = resourceId > 0 && resourceId < resourceTaxRate.Length ? resourceTaxRate[resourceId] : EconomyOrders.UnsetRate;
			return EconomyMath.ClampTax(r == EconomyOrders.UnsetRate ? cm?.GetTaxRate(zone) ?? 10 : r);
		}

		public int ResourceTaxRaw(int resourceId) { return resourceTaxRate[resourceId]; }

		// ---- orders ----
		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case EconomyOrders.SetTaxDetail:
				{
					var rate = EconomyMath.ClampTax((int)order.ExtraData - 10);
					var kind = (TaxDetailKind)order.ExtraLocation.X;
					var index = order.ExtraLocation.Y;
					if (kind == TaxDetailKind.Category && index >= 1 && index <= 4)
						cm?.SetTaxRate((ZoneCategory)index, rate);
					else if (kind == TaxDetailKind.Education && index >= 0 && index < 5)
						eduTaxRate[index] = rate;
					else if (kind == TaxDetailKind.Resource && index >= 1 && index <= Tables.ResourceCount)
						resourceTaxRate[index] = rate;

					break;
				}

				case EconomyOrders.SetFee:
				{
					var i = order.ExtraLocation.X;
					if (i >= 0 && i < feePercent.Length)
						feePercent[i] = (int)Math.Clamp(order.ExtraData, 50u, 200u);

					break;
				}

				case EconomyOrders.SetServiceBudget:
				{
					var i = order.ExtraLocation.X;
					if (i >= 0 && i < serviceBudget.Length)
						serviceBudget[i] = (int)Math.Clamp(order.ExtraData, 50u, 150u);

					break;
				}

				case EconomyOrders.SetLoan:
					SetLoan((int)Math.Min(order.ExtraData, int.MaxValue / 4u));
					break;
			}
		}

		/// <summary>Drops per-education / per-resource overrides when the base rate of a zone category is set.</summary>
		internal void ResetTaxOverrides(ZoneCategory category)
		{
			if (category == ZoneCategory.Residential)
				Array.Fill(eduTaxRate, EconomyOrders.UnsetRate);
			else
				for (var r = 1; r <= Tables.ResourceCount; r++)
					if (CategoryOfResource(r) == category)
						resourceTaxRate[r] = EconomyOrders.UnsetRate;
		}

		/// <summary>The zone category whose companies produce this resource (processor/office/commercial recipe), else industrial.</summary>
		ZoneCategory CategoryOfResource(int resourceId)
		{
			var recipe = Tables.Resources[resourceId].Recipe;
			return recipe >= 0 ? Tables.Recipes[recipe].Zone : ZoneCategory.Industrial;
		}

		// ---- tick ----
		void ITick.Tick(Actor self)
		{
			if (!initialized)
				Init();

			if (registry == null || cm == null || !self.Owner.Playable)
				return;

			var tick = world.WorldTick;
			if (tick % 25 == 0)
				RefreshAggregates();

			if (tick % ecoDayTicks == 0)
			{
				if (citizens != null)
					AccrueCitizenNeed();

				RollDayStats();
				UpdatePrices();
				StepTourists();
			}

			if (tick % ecoDayTicks == 0)
				ExpireOrphans(tick);

			StepProperties(tick);
			StepCompanies(tick);
			FlushClosures();
			if (tick % ecoDayTicks == 0)
				FlushCity();
		}

		// ---- ICityEconomy (static data) ----
		public int WageCentsPerDay(EducationLevel edu) { return wageCents[(int)edu]; }

		public int ResourceCount => Tables.ResourceCount;

		public string ResourceName(int resourceId) { return Tables.NameOf(resourceId); }

		List<int> consumerList;
		List<int> saleList;
	}
}
