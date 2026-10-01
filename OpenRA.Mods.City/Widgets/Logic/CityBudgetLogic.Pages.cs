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

		static readonly (ZoneCategory Category, string Key, int Rgb)[] TaxCategories =
		[
			(ZoneCategory.Residential, "residential", 0x7ED957),
			(ZoneCategory.Commercial, "commercial", 0x5AB4F0),
			(ZoneCategory.Industrial, "industrial", 0xF2C94C),
			(ZoneCategory.Office, "office", 0xB07CE8)
		];

		static readonly ServiceKind[] ServiceKinds =
		[
			ServiceKind.Police, ServiceKind.Fire, ServiceKind.Health, ServiceKind.Education, ServiceKind.Garbage, ServiceKind.Deathcare,
			ServiceKind.Parks, ServiceKind.Power, ServiceKind.Water, ServiceKind.Sewage, ServiceKind.Telecom, ServiceKind.Post, ServiceKind.Admin
		];

		ScrollPanelWidget taxDetail;
		LabelWidget taxDetailHeader;
		ZoneCategory detailCategory = ZoneCategory.Residential;
		ZoneCategory detailBuilt = ZoneCategory.None;

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
			taxDetailHeader = taxPage.Get<LabelWidget>("TAX_DETAIL_HEADER");

			// With the economy provider the rates go down to -10 (subsidised) and use the detailed order.
			var detailed = ctx.EconomyUi != null;
			var y = 0;
			foreach (var (category, key, rgb) in TaxCategories)
			{
				var cat = category;
				var color = CityUi.FromArgb(rgb);
				var name = FluentProvider.GetMessage("label-budget-tax-" + key);
				var row = CityRows.AddSlider(world, rows, y, name, color, detailed ? MinTax : 0, MaxTax, 1,
					() => manager?.GetTaxRate(cat) ?? 0,
					value =>
					{
						if (world.LocalPlayer == null)
							return;

						Issue(detailed ? UiOrders.CategoryTax(world.LocalPlayer, cat, value) : CityOrders.SetTaxOrder(world.LocalPlayer, cat, value));
					},
					null, () => manager == null);

				row.Slider.FillColor = color;
				row.Value.GetColor = () => row.Slider.DisplayValue > 12 ? CityUi.Warn : Color.White;
				row.Name.OnClick = () => detailCategory = cat;
				row.Name.IsHighlighted = () => detailed && detailCategory == cat;
				y += 30;
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
			taxDetailHeader.GetText = () => FluentProvider.GetMessage(DetailHeader, "name", catName);

			var source = ctx.EconomyUi;
			var y = 0;
			var width = taxDetail.Bounds.Width - taxDetail.ScrollbarWidth - 8;
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
				value => Issue(world.LocalPlayer != null ? order(value) : null), null, null, width, 150, 80);

			row.Slider.FillColor = CityUi.Accent;
			y += 28;
		}

		// ---- Services ----
		void BuildServices()
		{
			var services = pages["services"].Get("SERVICE_ROWS");
			if (ctx.Services == null)
				return;

			for (var i = 0; i < ServiceKinds.Length; i++)
			{
				var kind = ServiceKinds[i];
				var name = CityUi.Message("label-service-" + kind.ToString().ToLowerInvariant(), kind.ToString());
				var row = CityRows.AddSlider(world, services, i * 27, name, Color.White, 50, 150, 5,
					() => ctx.Services.GetBudget(kind),
					value => Issue(world.LocalPlayer != null ? UiOrders.ServiceBudget(world.LocalPlayer, kind, value) : null),
					value => FluentProvider.GetMessage(ServiceValue, "budget", value, "eff", Efficiency(value)),
					null, 560, 150, 150);

				row.Slider.FillColor = CityUi.Accent;
			}
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
			if (ctx.EconomyUi == null)
				return;

			var y = 0;
			foreach (var key in UiOrders.FeeKeys)
			{
				var fee = key;
				var name = CityUi.Message("label-budget-fee-" + key);
				var row = CityRows.AddSlider(world, fees, y, name, Color.White, 50, 200, 5,
					() => ctx.EconomyUi.GetFee(fee),
					value => Issue(world.LocalPlayer != null ? UiOrders.Fee(world.LocalPlayer, fee, value) : null));

				row.Value.GetColor = () => row.Slider.DisplayValue > 120 ? CityUi.Warn : row.Slider.DisplayValue < 80 ? CityUi.Good : Color.White;
				y += 34;
			}
		}

		// ---- Loan ----
		void BuildLoan()
		{
			var loanPage = pages["loan"];
			var rows = loanPage.Get("LOAN_ROWS");
			if (ctx.EconomyUi == null)
				return;

			var name = FluentProvider.GetMessage("label-budget-loan-slider");
			var slider = CityRows.AddSlider(world, rows, 10, name, Color.White, 0, Math.Max(1000, ctx.EconomyUi.LoanLimit), 1000,
				() => ctx.EconomyUi.LoanPrincipal,
				value => Issue(world.LocalPlayer != null ? UiOrders.Loan(world.LocalPlayer, value) : null),
				value => CityUtils.FormatMoney(value));

			slider.Slider.FillColor = CityUi.Warn;

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

			var preview = loanPage.Get<LabelWidget>("LOAN_PREVIEW");
			preview.GetText = () => FluentProvider.GetMessage(LoanPreview,
				"amount", CityUtils.FormatMoney((long)slider.Slider.DisplayValue * ctx.EconomyUi.LoanInterestTenthsPercent / 1000));
			preview.GetColor = () => CityUi.Warn;
		}
	}
}
