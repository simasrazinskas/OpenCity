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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityBudgetLogic
	{
		[FluentReference("name")]
		const string DetailHeader = "label-budget-tax-details";

		[FluentReference]
		const string DetailNone = "label-budget-tax-details-none";

		[FluentReference("budget", "eff")]
		const string ServiceValue = "label-budget-service-value";

		[FluentReference("principal", "limit", "rate")]
		const string LoanInfo = "label-budget-loan-info";

		[FluentReference("amount")]
		const string LoanPreview = "label-budget-loan-preview";

		const int MinTax = -10;
		const int MaxTax = 30;

		static readonly (ZoneCategory Category, string Key, string Icon)[] TaxCategories =
		[
			(ZoneCategory.Residential, "residential", "zone_res_low"),
			(ZoneCategory.Commercial, "commercial", "zone_com_low"),
			(ZoneCategory.Industrial, "industrial", "zone_ind"),
			(ZoneCategory.Office, "office", "zone_off")
		];

		static readonly ServiceKind[] ServiceKinds =
		[
			ServiceKind.Police, ServiceKind.Fire, ServiceKind.Health, ServiceKind.Education, ServiceKind.Garbage, ServiceKind.Deathcare,
			ServiceKind.Parks, ServiceKind.Power, ServiceKind.Water, ServiceKind.Sewage, ServiceKind.Telecom, ServiceKind.Post, ServiceKind.Admin
		];

		ScrollPanelWidget taxDetail;
		CityHeaderWidget taxDetailHeader;
		ZoneCategory detailCategory = ZoneCategory.Residential;
		ZoneCategory detailBuilt = ZoneCategory.None;
		int serviceRowCount;

		void Issue(Order order)
		{
			if (world.LocalPlayer != null && order != null)
				world.IssueOrder(order);
		}

		// ---- Taxes ----
		void BuildTaxes()
		{
			var taxPage = pages["taxes"];
			var rows = taxPage.Get("TAX_ROWS");
			taxDetail = taxPage.Get<ScrollPanelWidget>("TAX_DETAIL");
			taxDetailHeader = taxPage.Get<CityHeaderWidget>("TAX_DETAIL_HEADER");

			// With the economy provider the rates go down to -10 (subsidised) and use the detailed order.
			var detailed = ctx.EconomyUi != null;
			var y = 0;
			foreach (var (category, key, icon) in TaxCategories)
			{
				var cat = category;
				var name = FluentProvider.GetMessage("label-budget-tax-" + key);
				var incomeKey = "tax-" + key;
				var row = CityRows.AddSlider(world, rows, y, name, Color.White, detailed ? MinTax : 0, MaxTax, 1,
					() => manager?.GetTaxRate(cat) ?? 0,
					value =>
					{
						if (world.LocalPlayer == null)
							return;

						Issue(detailed ? UiOrders.CategoryTax(world.LocalPlayer, cat, value) : CityOrders.SetTaxOrder(world.LocalPlayer, cat, value));
					},
					null, () => manager == null, rows.Bounds.Width, 92, 28, icon,
					() => "+" + CityUtils.FormatMoney(manager?.LastMonthIncome?.GetValueOrDefault(incomeKey) ?? 0), 56);

				row.Row.Bounds.Height = 19;
				row.Extra.GetColor = () => CityTheme.MoneyPositive;
				row.Value.GetColor = () => row.Slider.DisplayValue > 12 ? CityTheme.MoneyNegative : CityTheme.Ink;
				row.Name.OnClick = () => detailCategory = cat;
				row.Name.GetColor = () => detailed && detailCategory == cat ? CityTheme.FamilyShade("finance", 1) : CityTheme.Ink;
				y += 19;
			}

			taxDetailHeader.IsVisible = () => detailed;
			taxDetail.IsVisible = () => detailed;
		}

		void RefreshTaxDetail()
		{
			if (taxDetail == null || ctx.EconomyUi == null || detailBuilt == detailCategory)
				return;

			detailBuilt = detailCategory;
			taxDetail.RemoveChildren();
			var catName = FluentProvider.GetMessage("label-budget-tax-" + TaxCategories[Array.FindIndex(TaxCategories, t => t.Category == detailCategory)].Key);
			var headerText = FluentProvider.GetMessage(DetailHeader, "name", catName);
			taxDetailHeader.GetText = () => headerText;

			var source = ctx.EconomyUi;
			var y = 0;
			var width = taxDetail.Bounds.Width - taxDetail.ScrollbarWidth - 2;
			if (detailCategory == ZoneCategory.Residential)
			{
				for (var i = 0; i < 5; i++)
				{
					var index = i;
					var level = (EducationLevel)i;
					AddDetailRow(CityUi.EducationName(level), () => source.GetTaxDetail(1, index), v => UiOrders.EducationTax(world.LocalPlayer, level, v), ref y, width);
				}
			}
			else if (ctx.Economy != null)
			{
				for (var r = 1; r <= ctx.Economy.ResourceCount; r++)
				{
					var id = r;
					AddDetailRow(ctx.Economy.ResourceName(r), () => source.GetTaxDetail(2, id), v => UiOrders.ResourceTax(world.LocalPlayer, id, v), ref y, width);
				}
			}

			if (y == 0)
			{
				var none = FluentProvider.GetMessage(DetailNone);
				var label = Game.LoadWidget(world, "CITY_BUDGET_ROW", taxDetail, []);
				label.Get<LabelWidget>("NAME").GetText = () => none;
			}

			taxDetail.Layout.AdjustChildren();
		}

		void AddDetailRow(string name, Func<int> get, Func<int, Order> order, ref int y, int width)
		{
			var row = CityRows.AddSlider(world, taxDetail, y, name, Color.White, MinTax, MaxTax, 1, get,
				value => Issue(world.LocalPlayer != null ? order(value) : null), null, null, width, 110, 28);

			row.Row.Bounds.Height = 15;
			y += 15;
		}

		// ---- Expenses: service budgets, then the other expenses ----
		static string ServiceKey(ServiceKind kind) { return kind.ToString().ToLowerInvariant(); }

		void BuildServices()
		{
			serviceRowCount = 0;
			if (ctx.Services == null)
				return;

			var width = serviceList.Bounds.Width - serviceList.ScrollbarWidth - 2;
			foreach (var kind in ServiceKinds)
			{
				var k = kind;
				var name = CityUi.Message("label-service-" + ServiceKey(kind), kind.ToString());
				var upkeepKey = "upkeep-" + ServiceKey(kind);
				var row = CityRows.AddSlider(world, serviceList, 0, name, Color.White, 50, 150, 5,
					() => ctx.Services.GetBudget(k),
					value => Issue(world.LocalPlayer != null ? UiOrders.ServiceBudget(world.LocalPlayer, k, value) : null),
					value => FluentProvider.GetMessage("label-city-percent", "value", value),
					null, width, 86, 30, CityUi.LedgerIcon(upkeepKey),
					() => "-" + CityUtils.FormatMoney(manager?.LastMonthExpenses?.GetValueOrDefault(upkeepKey) ?? 0), 50);

				row.Extra.GetColor = () => CityTheme.MoneyNegative;
				row.Name.GetTooltipText = () => FluentProvider.GetMessage(ServiceValue, "budget", row.Slider.DisplayValue, "eff", Efficiency(row.Slider.DisplayValue));
				serviceRowCount++;
			}

			serviceList.Layout.AdjustChildren();
		}

		void RefreshExpenses()
		{
			var values = manager?.LastMonthExpenses;
			if (expenseShown != null && ReferenceEquals(values, expenseShown))
				return;

			expenseShown = values;

			// Keep the slider rows (one may be dragged right now), rebuild what follows them.
			while (serviceList.Children.Count > serviceRowCount)
				serviceList.RemoveChild(serviceList.Children[^1]);

			var other = values?.Where(kv => !IsServiceUpkeep(kv.Key)).OrderByDescending(kv => kv.Value).ToList() ?? [];
			if (other.Count > 0)
			{
				var header = AddLedgerRow(serviceList, null, "", CityTheme.Ink);
				var text = FluentProvider.GetMessage(OtherExpenses);
				var headerLabel = header.Get<LabelWidget>("NAME");
				headerLabel.GetText = () => text;
				foreach (var kv in other)
					AddLedgerRow(serviceList, kv.Key, "-" + CityUtils.FormatMoney(kv.Value), CityTheme.MoneyNegative);
			}

			serviceList.Layout.AdjustChildren();
			var total = pages["services"].Get<LabelWidget>("EXPENSE_TOTAL");
			var totalText = "-" + CityUtils.FormatMoney(manager?.LastMonthExpensesTotal ?? 0);
			total.GetText = () => totalText;
			total.GetColor = () => CityTheme.MoneyNegative;
		}

		bool IsServiceUpkeep(string key)
		{
			return ctx.Services != null && ServiceKinds.Any(k => key == "upkeep-" + ServiceKey(k));
		}

		/// <summary>Effect of a budget percentage on a service (design 07 section 3.8): 50 gives 25, 100 gives 100, 150 gives 125.</summary>
		static int Efficiency(int budget)
		{
			return budget < 100 ? 25 + 3 * (budget - 50) / 2 : 100 + (budget - 100) / 2;
		}

		// ---- Fees ----
		void BuildFees()
		{
			var fees = pages["fees"].Get("FEE_ROWS");
			var y = 0;

			// Electricity, water and garbage belong to the economy, health care and education fees to the services.
			var entries = new List<(string Key, Func<int> Get, Func<int, Order> Order)>();
			if (ctx.EconomyUi != null)
				foreach (var key in UiOrders.FeeKeys)
				{
					var fee = key;
					entries.Add((key, () => ctx.EconomyUi.GetFee(fee), v => UiOrders.Fee(world.LocalPlayer, fee, v)));
				}

			var sim = ctx.Get<ServiceSimulation>();
			if (sim != null)
				foreach (var (key, fee) in new[] { ("health", ServiceFee.Health), ("education", ServiceFee.Education) })
				{
					var kind = fee;
					entries.Add((key, () => sim.GetFeePercent(kind), v => UiOrders.ServiceFeeOrder(world.LocalPlayer, kind, v)));
				}

			foreach (var (key, get, order) in entries)
			{
				var read = get;
				var make = order;
				var name = CityUi.Message("label-budget-fee-" + key);
				var incomeKey = "fee-" + key;
				var row = CityRows.AddSlider(world, fees, y, name, Color.White, 50, 200, 5, read,
					value => Issue(world.LocalPlayer != null ? make(value) : null), null, null, fees.Bounds.Width, 76, 30,
					CityUi.LedgerIcon(incomeKey), () => "+" + CityUtils.FormatMoney(manager?.LastMonthIncome?.GetValueOrDefault(incomeKey) ?? 0), 50);

				row.Row.Bounds.Height = 18;
				row.Extra.GetColor = () => CityTheme.MoneyPositive;
				row.Value.GetColor = () => row.Slider.DisplayValue > 120 ? CityTheme.MoneyNegative : row.Slider.DisplayValue < 80 ? CityTheme.MoneyPositive : CityTheme.Ink;
				y += 18;
			}

			var well = pages["fees"].Get("FEE_WELL");
			well.Bounds.Height = Math.Max(20, y + 2);
			var hintY = well.Bounds.Y + well.Bounds.Height + 6;
			pages["fees"].Get("INFO_ICON").Bounds.Y = hintY;
			pages["fees"].Get("FEES_HINT").Bounds.Y = hintY;
		}

		// ---- Loan ----
		void BuildLoan()
		{
			var loanPage = pages["loan"];
			var rows = loanPage.Get("LOAN_ROWS");
			if (ctx.EconomyUi == null)
				return;

			var name = FluentProvider.GetMessage("label-budget-loan-slider");
			var slider = CityRows.AddSlider(world, rows, 2, name, Color.White, 0, Math.Max(1000, ctx.EconomyUi.LoanLimit), 1000,
				() => ctx.EconomyUi.LoanPrincipal,
				value => Issue(world.LocalPlayer != null ? UiOrders.Loan(world.LocalPlayer, value) : null),
				value => CityUtils.FormatMoney(value), null, rows.Bounds.Width, 60, 70);

			var info = loanPage.Get<LabelWidget>("LOAN_INFO");
			info.GetText = () =>
			{
				// The limit grows with the milestone.
				slider.Slider.MaximumValue = Math.Max(1000, ctx.EconomyUi.LoanLimit);
				return FluentProvider.GetMessage(LoanInfo,
				"principal", CityUtils.FormatMoney(ctx.EconomyUi.LoanPrincipal),
				"limit", CityUtils.FormatMoney(ctx.EconomyUi.LoanLimit),
				"rate", (ctx.EconomyUi.LoanInterestTenthsPercent / 10f).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture));
			};

			loanPage.Get<CityBarWidget>("CREDIT_BAR").GetPercentage = () =>
				(int)(ctx.EconomyUi.LoanPrincipal * 100L / Math.Max(1, ctx.EconomyUi.LoanLimit));
			loanPage.Get<LabelWidget>("CREDIT_VALUE").GetText = () =>
				CityUtils.FormatMoney(ctx.EconomyUi.LoanPrincipal) + " / " + CityUtils.FormatMoney(ctx.EconomyUi.LoanLimit);

			var preview = loanPage.Get<LabelWidget>("LOAN_PREVIEW");
			preview.GetText = () => FluentProvider.GetMessage(LoanPreview,
				"amount", CityUtils.FormatMoney((long)slider.Slider.DisplayValue * ctx.EconomyUi.LoanInterestTenthsPercent / 1000));
			preview.GetColor = () => CityTheme.MoneyNegative;
		}
	}
}
