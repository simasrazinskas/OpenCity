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
using System.Globalization;
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 City Budget window (design/iso/ui/panels/budget-*.png): finance colours, six icon tabs. Overview (this
	/// month's income and expenses as meters, net result, 12-month cashflow graph), Income (sources with share), Expenses
	/// (service budget sliders with their upkeep, then the other expenses), Taxes (category rates, per-education or
	/// per-resource details), Fees and Loans; the footer shows funds and the monthly balance. Tabs whose provider is
	/// missing are disabled. Every change is an order issued on slider release.
	/// </summary>
	public partial class CityBudgetLogic : ChromeLogic
	{
		[FluentReference("balance")]
		const string BalanceFooter = "label-budget-footer-balance";

		[FluentReference("funds")]
		const string FundsFooter = "label-budget-footer-funds";

		[FluentReference]
		const string TabBudget = "label-budget-tab-budget";

		[FluentReference]
		const string TabIncome = "label-budget-tab-income";

		[FluentReference]
		const string TabServices = "label-budget-tab-services";

		[FluentReference]
		const string TabTaxes = "label-budget-tab-taxes";

		[FluentReference]
		const string TabFees = "label-budget-tab-fees";

		[FluentReference]
		const string TabLoan = "label-budget-tab-loan";

		[FluentReference]
		const string Income = "label-budget-trend-income";

		[FluentReference]
		const string Expenses = "label-budget-trend-expenses";

		[FluentReference]
		const string OtherExpenses = "label-budget-other-expenses";

		readonly World world;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget incomeList;
		readonly ScrollPanelWidget serviceList;
		readonly Dictionary<string, Widget> pages = [];
		readonly List<GraphSeries> trend = [];

		// CityManager replaces the last-month dictionaries at each month end, so reference equality detects changes.
		object incomeShown;
		object expenseShown;
		string page = "budget";

		[ObjectCreator.UseCtor]
		public CityBudgetLogic(Widget widget, World world)
		{
			this.world = world;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);

			foreach (var id in new[] { "budget", "income", "services", "taxes", "fees", "loan" })
				pages[id] = widget.Get("PAGE_" + id.ToUpperInvariant());

			var window = (CityPanelWidget)widget;
			window.SetTabs(new (string Id, string Icon, string Key, Func<bool> Available)[]
			{
				("budget", "pnl_budget", TabBudget, () => true),
				("income", "stat_income", TabIncome, () => manager != null),
				("services", "stat_expenses", TabServices, () => manager != null),
				("taxes", "stat_tax", TabTaxes, () => manager != null),
				("fees", "stat_fee", TabFees, () => ctx.EconomyUi != null || ctx.Get<ServiceSimulation>() != null),
				("loan", "stat_loan", TabLoan, () => ctx.EconomyUi != null)
			}.Select(t =>
			{
				var text = FluentProvider.GetMessage(t.Key);
				return new CityWindowTab
				{
					Icon = t.Icon,
					GetTooltip = () => text,
					IsActive = () => page == t.Id,
					IsDisabled = () => !t.Available(),
					OnClick = () => ShowPage(t.Id)
				};
			}));

			incomeList = pages["income"].Get<ScrollPanelWidget>("INCOME_LIST");
			serviceList = pages["services"].Get<ScrollPanelWidget>("SERVICE_ROWS");

			InitOverview();
			InitFooter(widget);
			BuildTaxes();
			BuildServices();
			BuildFees();
			BuildLoan();

			widget.Get<LogicTickerWidget>("BUDGET_TICKER").OnTick = RefreshLists;
			ShowPage("budget");
		}

		void InitOverview()
		{
			var overview = pages["budget"];
			int IncomeTotal() => manager?.LastMonthIncomeTotal ?? 0;
			int ExpenseTotal() => manager?.LastMonthExpensesTotal ?? 0;
			int Scale() => Math.Max(1, Math.Max(IncomeTotal(), ExpenseTotal()) * 115 / 100);

			var incomeTotal = overview.Get<LabelWidget>("INCOME_TOTAL");
			incomeTotal.GetText = () => "+" + CityUtils.FormatMoney(IncomeTotal());
			incomeTotal.GetColor = () => CityTheme.MoneyPositive;
			overview.Get<CityBarWidget>("INCOME_BAR").GetPercentage = () => IncomeTotal() * 100 / Scale();

			var expenseTotal = overview.Get<LabelWidget>("EXPENSE_TOTAL");
			expenseTotal.GetText = () => "-" + CityUtils.FormatMoney(ExpenseTotal());
			expenseTotal.GetColor = () => CityTheme.MoneyNegative;
			overview.Get<CityBarWidget>("EXPENSE_BAR").GetPercentage = () => ExpenseTotal() * 100 / Scale();

			overview.Get<CityIconWidget>("NET_ICON").GetIcon = () => manager != null && manager.MonthlyBalance < 0 ? "stat_balance_down" : "stat_balance_up";
			var balance = overview.Get<LabelWidget>("BALANCE");
			balance.GetText = () => manager == null ? "" : CityUi.SignedMoney(manager.MonthlyBalance);
			balance.GetColor = () => manager != null && manager.MonthlyBalance < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;

			var chart = overview.Get<CityGraphWidget>("TREND");
			chart.GetSeries = TrendSeries;
			chart.IsVisible = () => ctx.Statistics != null;
			chart.GetSampleLabel = ago => ago <= 0 ? FluentProvider.GetMessage("label-stats-now") : FluentProvider.GetMessage("label-stats-months-ago", "count", ago);
			overview.Get("TREND_HEADER").IsVisible = chart.IsVisible;
		}

		void InitFooter(Widget widget)
		{
			var funds = widget.Get<LabelWidget>("FUNDS");
			funds.GetText = () => manager == null ? "" : FluentProvider.GetMessage(FundsFooter, "funds", CityUtils.FormatMoney(manager.Funds));
			var balance = widget.Get<LabelWidget>("FOOTER_BALANCE");
			balance.GetText = () => manager == null ? "" : FluentProvider.GetMessage(BalanceFooter, "balance", CityUi.SignedMoney(manager.MonthlyBalance));
		}

		void ShowPage(string id)
		{
			page = id;
			foreach (var (key, widget) in pages)
				widget.Visible = key == id;
		}

		IReadOnlyList<GraphSeries> TrendSeries()
		{
			trend.Clear();
			var stats = ctx.Statistics;
			if (stats == null)
				return trend;

			trend.Add(new GraphSeries { Name = FluentProvider.GetMessage(Income), Color = CityTheme.Ramp("green", 6), Values = stats.History("income", 12) });
			trend.Add(new GraphSeries { Name = FluentProvider.GetMessage(Expenses), Color = CityTheme.Ramp("red", 5), Values = stats.History("expenses", 12) });
			return trend;
		}

		static string CategoryName(string key)
		{
			return CityUi.Message("label-budget-" + key);
		}

		void RefreshLists()
		{
			RefreshIncome();
			RefreshExpenses();
			RefreshTaxDetail();
		}

		void RefreshIncome()
		{
			var values = manager?.LastMonthIncome;
			if (incomeShown != null && ReferenceEquals(values, incomeShown))
				return;

			incomeShown = values;
			incomeList.RemoveChildren();
			var total = Math.Max(1, values?.Values.Sum() ?? 0);
			foreach (var kv in values?.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal) ?? Enumerable.Empty<KeyValuePair<string, int>>())
			{
				var share = (kv.Value * 100L / total).ToString(CultureInfo.CurrentCulture) + "%";
				AddLedgerRow(incomeList, kv.Key, "+" + CityUtils.FormatMoney(kv.Value), CityTheme.MoneyPositive, share);
			}

			incomeList.Layout.AdjustChildren();
			var totalLabel = pages["income"].Get<LabelWidget>("INCOME_TOTAL");
			var text = "+" + CityUtils.FormatMoney(manager?.LastMonthIncomeTotal ?? 0);
			totalLabel.GetText = () => text;
			totalLabel.GetColor = () => CityTheme.MoneyPositive;
		}

		/// <summary>A ledger line: icon, name, optional share, amount.</summary>
		Widget AddLedgerRow(ScrollPanelWidget list, string key, string amount, Color color, string share = null)
		{
			var row = Game.LoadWidget(world, "CITY_BUDGET_ROW", list, []);
			var width = list.Bounds.Width - list.ScrollbarWidth - 2;
			row.Bounds.Width = width;
			row.Bounds.Height = 15;
			row.Get<CityIconWidget>("ICON").Icon = CityUi.LedgerIcon(key);
			var name = key == null ? "" : CategoryName(key);
			var label = row.Get<LabelWidget>("NAME");
			label.Bounds.X = 22;
			label.Bounds.Width = width - 140;
			label.GetText = () => name;
			var shareLabel = row.Get<LabelWidget>("SHARE");
			shareLabel.Bounds.X = width - 116;
			shareLabel.GetText = () => share ?? "";
			shareLabel.GetColor = () => CityTheme.Muted("finance");
			var value = row.Get<LabelWidget>("VALUE");
			value.Bounds.X = width - 74;
			value.Bounds.Width = 70;
			value.GetText = () => amount;
			value.GetColor = () => color;
			return row;
		}
	}
}
