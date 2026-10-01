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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Budget panel with tabs: Budget (last month's income and expenses by category and a trend chart), Taxes (category
	/// rates plus per-education and per-resource details), Services (budget sliders), Fees and Loan. Tabs whose
	/// provider is missing are hidden. Every change is an order issued on slider release.
	/// </summary>
	public partial class CityBudgetLogic : ChromeLogic
	{
		[FluentReference("amount")]
		const string TotalLabel = "label-budget-total";

		[FluentReference("balance")]
		const string BalanceLabel = "label-budget-balance";

		[FluentReference]
		const string TabBudget = "label-budget-tab-budget";

		[FluentReference]
		const string TabTaxes = "label-budget-tab-taxes";

		[FluentReference]
		const string TabServices = "label-budget-tab-services";

		[FluentReference]
		const string TabFees = "label-budget-tab-fees";

		[FluentReference]
		const string TabLoan = "label-budget-tab-loan";

		[FluentReference]
		const string Income = "label-budget-trend-income";

		[FluentReference]
		const string Expenses = "label-budget-trend-expenses";

		readonly World world;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget incomeList;
		readonly ScrollPanelWidget expenseList;
		readonly Dictionary<string, Widget> pages = [];
		readonly Dictionary<string, ButtonWidget> tabButtons = [];
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

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;

			foreach (var id in new[] { "budget", "taxes", "services", "fees", "loan" })
				pages[id] = widget.Get("PAGE_" + id.ToUpperInvariant());

			BuildTabs(widget.Get("TABS"));

			incomeList = pages["budget"].Get<ScrollPanelWidget>("INCOME_LIST");
			expenseList = pages["budget"].Get<ScrollPanelWidget>("EXPENSE_LIST");

			var incomeTotal = pages["budget"].Get<LabelWidget>("INCOME_TOTAL");
			incomeTotal.GetText = () => FluentProvider.GetMessage(TotalLabel, "amount", CityUtils.FormatMoney(manager?.LastMonthIncomeTotal ?? 0));
			incomeTotal.GetColor = () => CityUi.Good;

			var expenseTotal = pages["budget"].Get<LabelWidget>("EXPENSE_TOTAL");
			expenseTotal.GetText = () => FluentProvider.GetMessage(TotalLabel, "amount", CityUtils.FormatMoney(manager?.LastMonthExpensesTotal ?? 0));
			expenseTotal.GetColor = () => CityUi.Bad;

			var balance = pages["budget"].Get<LabelWidget>("BALANCE");
			balance.GetText = () => manager == null ? "" :
				FluentProvider.GetMessage(BalanceLabel, "balance", CityUi.SignedMoney(manager.MonthlyBalance));
			balance.GetColor = () => manager != null && manager.MonthlyBalance < 0 ? CityUi.Bad : CityUi.Good;

			var chart = pages["budget"].Get<CityGraphWidget>("TREND");
			chart.GetSeries = TrendSeries;
			chart.IsVisible = () => ctx.Statistics != null;
			chart.GetSampleLabel = ago => ago <= 0 ? FluentProvider.GetMessage("label-stats-now") : FluentProvider.GetMessage("label-stats-months-ago", "count", ago);

			BuildTaxes();
			BuildServices();
			BuildFees();
			BuildLoan();

			widget.Get<LogicTickerWidget>("BUDGET_TICKER").OnTick = RefreshLists;
			ShowPage("budget");
		}

		void BuildTabs(Widget container)
		{
			var tabs = new List<(string Id, string Key, Func<bool> Available)>
			{
				("budget", TabBudget, () => true),
				("taxes", TabTaxes, () => manager != null),
				("services", TabServices, () => ctx.Services != null),
				("fees", TabFees, () => ctx.EconomyUi != null || ctx.Get<ServiceSimulation>() != null),
				("loan", TabLoan, () => ctx.EconomyUi != null)
			};

			// The tabs share the row evenly.
			var x = 0;
			var tabWidth = (container.Bounds.Width - (tabs.Count - 1) * 4) / tabs.Count;
			foreach (var (id, key, available) in tabs)
			{
				var tab = id;
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", container, []) as ButtonWidget;
				button.Id = "BUDGET_TAB_" + id.ToUpperInvariant();
				button.Bounds.X = x;
				button.Bounds.Width = tabWidth;
				button.Bounds.Height = 28;
				var text = FluentProvider.GetMessage(key);
				button.GetText = () => text;
				button.IsHighlighted = () => page == tab;
				button.IsVisible = available;
				button.OnClick = () => ShowPage(tab);
				tabButtons[id] = button;
				x += tabWidth + 4;
			}
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

			trend.Add(new GraphSeries { Name = FluentProvider.GetMessage(Income), Color = CityUi.Good, Values = stats.History("income", 12) });
			trend.Add(new GraphSeries { Name = FluentProvider.GetMessage(Expenses), Color = CityUi.Bad, Values = stats.History("expenses", 12) });
			return trend;
		}

		static string CategoryName(string key)
		{
			return CityUi.Message("label-budget-" + key);
		}

		void RefreshLists()
		{
			Refresh(incomeList, manager?.LastMonthIncome, ref incomeShown, CityUi.Good);
			Refresh(expenseList, manager?.LastMonthExpenses, ref expenseShown, CityUi.Bad);
			RefreshTaxDetail();
		}

		void Refresh(ScrollPanelWidget list, IReadOnlyDictionary<string, int> values, ref object shown, Color valueColor)
		{
			if (shown != null && ReferenceEquals(values, shown))
				return;

			shown = values;
			var sorted = values == null ? [] : values.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
			list.RemoveChildren();

			foreach (var kv in sorted)
			{
				var row = Game.LoadWidget(world, "CITY_BUDGET_ROW", list, []);
				row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;
				var name = CategoryName(kv.Key);
				var amount = CityUtils.FormatMoney(kv.Value);
				var label = row.Get<LabelWidget>("NAME");
				label.GetText = () => name;
				label.Bounds.Width = row.Bounds.Width - 100;
				var value = row.Get<LabelWidget>("VALUE");
				value.Bounds.X = row.Bounds.Width - 100;
				value.GetText = () => amount;
				value.GetColor = () => valueColor;
			}

			list.Layout.AdjustChildren();
		}
	}
}
