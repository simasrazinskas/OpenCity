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
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Policies window (design/iso/ui/panels/policies-*.png): city policies and per-district policies (data driven,
	/// IProgressionUiSource.Policies), one tab each. A policy is a checkbox, optionally with a slider; changes are CitySetPolicy
	/// orders. Locked policies are shown dimmed with a lock. Selecting a row shows its description and the monthly total of
	/// all active policies. The window height follows the number of policies.
	/// </summary>
	public class CityPoliciesLogic : ChromeLogic
	{
		[FluentReference]
		const string ScopeCity = "label-policies-city";

		[FluentReference]
		const string ScopeDistrict = "label-policies-district";

		[FluentReference]
		const string TitleCity = "label-policies-window-city";

		[FluentReference]
		const string TitleDistrict = "label-policies-window-district";

		[FluentReference]
		const string WholeCity = "label-policies-whole-city";

		[FluentReference]
		const string NoDistricts = "label-policies-no-districts";

		[FluentReference("amount")]
		const string PerMonth = "label-policies-per-month";

		[FluentReference]
		const string Free = "label-policies-free";

		[FluentReference]
		const string Locked = "label-policies-locked";

		[FluentReference("active", "total")]
		const string ActiveCount = "label-policies-active";

		[FluentReference("amount")]
		const string MonthlyTotal = "label-policies-monthly-total";

		[FluentReference]
		const string HintText = "label-policies-hint";

		const int RowHeight = 26;
		const int ChromeHeight = 138;
		const int MinRows = 3;
		const int ListExtra = 136;

		static readonly Dictionary<string, string> Icons = new()
		{
			["min-fare"] = "tr_bus",
			["import-services"] = "stat_expenses",
			["smoking-ban"] = "stat_health",
			["education-boost"] = "cat_education",
			["free-transit"] = "tr_metro",
			["pollution-management"] = "st_air_pollution",
			["city-promotion"] = "stat_tourist",
			["high-speed-highways"] = "road_highway",
			["energy-saving"] = "info_power",
			["water-saving"] = "cat_water",
			["recycling"] = "cat_garbage",
			["speed-bumps"] = "ctl_stop",
			["parking-fee"] = "addon_parking",
			["small-business"] = "cat_zoning",
			["heavy-traffic-ban"] = "tr_truck",
			["gated-community"] = "stat_home",
			["industrial-planning"] = "cat_industry",
			["combustion-ban"] = "st_air_pollution",
		};

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly ScrollPanelWidget list;
		readonly Widget header;
		readonly Widget detail;
		readonly LabelWidget scopeLabel;
		readonly List<PolicyEntry> current = [];
		readonly StringBuilder signature = new();

		bool districtScope;
		string builtSignature;
		string selected;
		int rowsInList;

		[ObjectCreator.UseCtor]
		public CityPoliciesLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;
			list = widget.Get<ScrollPanelWidget>("LIST");
			header = widget.Get("HEADER");

			var window = (CityPanelWidget)widget;
			var titleCity = FluentProvider.GetMessage(TitleCity);
			var titleDistrict = FluentProvider.GetMessage(TitleDistrict);
			window.GetTitle = () => districtScope ? titleDistrict : titleCity;
			var city = FluentProvider.GetMessage(ScopeCity);
			var district = FluentProvider.GetMessage(ScopeDistrict);
			window.SetTabs(
			[
				new CityWindowTab { Icon = "pnl_city_info", GetTooltip = () => city, IsActive = () => !districtScope, OnClick = () => SetScope(false) },
				new CityWindowTab { Icon = "pnl_districts", GetTooltip = () => district, IsActive = () => districtScope, OnClick = () => SetScope(true) }
			]);

			scopeLabel = header.Get<LabelWidget>("SCOPE_LABEL");
			var wholeCity = FluentProvider.GetMessage(WholeCity);
			var districtText = FluentProvider.GetMessage(ScopeDistrict);
			scopeLabel.GetText = () => districtScope ? districtText : wholeCity;

			var picker = header.Get<DropDownButtonWidget>("DISTRICT");
			picker.IsVisible = () => districtScope;
			picker.GetText = DistrictLabel;
			picker.IsDisabled = () => (ctx.ProgressionUi?.Districts.Count ?? 0) == 0;
			picker.OnClick = () =>
			{
				var districts = ctx.ProgressionUi?.Districts;
				if (districts == null || districts.Count == 0)
					return;

				CityDropDown.Show(picker, districts.ToList(), d => string.IsNullOrEmpty(d.Name) ? "#" + d.Id : d.Name,
					d => d.Id == ctx.SelectedDistrict, d => SelectDistrict(d.Id));
			};

			var previous = header.Get<ButtonWidget>("PREVIOUS");
			var next = header.Get<ButtonWidget>("NEXT");
			previous.IsVisible = next.IsVisible = () => districtScope;
			previous.IsDisabled = next.IsDisabled = () => (ctx.ProgressionUi?.Districts.Count ?? 0) < 2;
			previous.OnClick = () => StepDistrict(-1);
			next.OnClick = () => StepDistrict(1);

			var active = header.Get<LabelWidget>("ACTIVE");
			active.GetText = () => FluentProvider.GetMessage(ActiveCount, "active", current.Count(p => p.Active), "total", current.Count);
			active.GetColor = () => CityTheme.Muted("people");

			detail = widget.Get("DETAIL");
			var name = detail.Get<LabelWidget>("NAME");
			name.GetText = () => Selected() is { Id: not null } p ? CityUi.Message(p.NameKey) : "";
			name.GetColor = () => CityTheme.FamilyShade("people", 1);
			var total = detail.Get<LabelWidget>("TOTAL");
			total.GetText = () => current.Count == 0 ? "" : FluentProvider.GetMessage(MonthlyTotal, "amount", CityUi.SignedMoney(MonthlyBalance()));
			total.GetColor = () => MonthlyBalance() >= 0 ? CityTheme.MoneyPositive : CityTheme.MoneyNegative;
			var description = detail.Get<LabelWidget>("DESCRIPTION");
			var hint = FluentProvider.GetMessage(HintText);
			description.GetText = () => Selected() is { Id: not null } p ? CityUi.Message(p.DescKey, "") : hint;

			widget.Get("EMPTY").IsVisible = () => current.Count == 0;

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
				{
					Refresh();
					FitWindow();
				}

				return visible;
			};
		}

		int DistrictId => districtScope ? ctx.SelectedDistrict : 0;

		void SetScope(bool district)
		{
			districtScope = district;
			selected = null;
			builtSignature = null;
		}

		void SelectDistrict(int id)
		{
			ctx.SelectedDistrict = id;
			builtSignature = null;
		}

		void StepDistrict(int step)
		{
			var districts = ctx.ProgressionUi?.Districts;
			if (districts == null || districts.Count == 0)
				return;

			var index = 0;
			for (var i = 0; i < districts.Count; i++)
				if (districts[i].Id == ctx.SelectedDistrict)
					index = i;

			SelectDistrict(districts[(index + step + districts.Count) % districts.Count].Id);
		}

		string DistrictLabel()
		{
			var districts = ctx.ProgressionUi?.Districts;
			if (districts != null)
				foreach (var d in districts)
					if (d.Id == ctx.SelectedDistrict)
						return string.IsNullOrEmpty(d.Name) ? "#" + d.Id : d.Name;

			return FluentProvider.GetMessage(NoDistricts);
		}

		PolicyEntry Selected()
		{
			return selected == null ? default : Find(selected);
		}

		/// <summary>Monthly money of the active policies: income positive, upkeep negative.</summary>
		int MonthlyBalance()
		{
			var sum = 0;
			foreach (var p in current)
				if (p.Active)
					sum -= p.UpkeepPerMonth;

			return sum;
		}

		void FitWindow()
		{
			detail.Bounds.Y = panel.Bounds.Height - 61;
			var rows = Math.Max(MinRows, current.Count);
			if (CityPeopleFeed.FitHeight(panel, ChromeHeight + rows * RowHeight, ChromeHeight + MinRows * RowHeight))
				list.Bounds.Height = panel.Bounds.Height - ListExtra;
			else if (rowsInList != rows)
				list.Bounds.Height = panel.Bounds.Height - ListExtra;

			rowsInList = rows;
		}

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			if (districtScope && source.Districts.Count > 0 && source.Districts.All(d => d.Id != ctx.SelectedDistrict))
				ctx.SelectedDistrict = source.Districts[0].Id;

			current.Clear();
			if (!districtScope || source.Districts.Count > 0)
				current.AddRange(source.Policies(DistrictId));

			// Rebuild only when the set of policies or the scope changes; values are read live.
			signature.Clear();
			signature.Append(districtScope).Append(DistrictId).Append(list.Bounds.Width);
			foreach (var p in current)
				signature.Append('|').Append(p.Id).Append(p.Unlocked);

			var text = signature.ToString();
			if (text == builtSignature)
				return;

			builtSignature = text;
			if (selected == null || current.All(p => p.Id != selected))
				selected = current.FirstOrDefault(p => p.Unlocked).Id ?? current.FirstOrDefault().Id;

			list.RemoveChildren();
			foreach (var policy in current)
				AddRow(policy.Id);

			list.Layout.AdjustChildren();
			list.ScrollToTop();
		}

		PolicyEntry Find(string id)
		{
			foreach (var p in current)
				if (p.Id == id)
					return p;

			return default;
		}

		static string CostText(PolicyEntry p)
		{
			if (p.UpkeepPerMonth == 0)
				return FluentProvider.GetMessage(Free);

			return FluentProvider.GetMessage(PerMonth, "amount", CityUi.SignedMoney(-p.UpkeepPerMonth));
		}

		void AddRow(string id)
		{
			var entry = Find(id);
			var row = Game.LoadWidget(world, "CITY_POLICY_ROW", list, []);
			var width = list.Bounds.Width - list.ScrollbarWidth - 2;
			row.Bounds.Width = width;
			row.Bounds.Height = RowHeight;

			var background = row.Get<CityRowBackgroundWidget>("BG");
			background.Bounds.Width = width;
			background.IsSelected = () => selected == id;
			bool Dark() => selected == id;

			var select = row.Get<ButtonWidget>("SELECT");
			select.Background = "";
			select.Bounds.Width = width;
			select.OnClick = () => selected = id;

			var icon = row.Get<CityIconWidget>("ICON");
			icon.Icon = Icons.TryGetValue(id, out var name) ? name : "pnl_policies";
			icon.IsDisabled = () => !Find(id).Unlocked;

			var hasSlider = entry.SliderMax > 0;
			var slider = row.Get<CitySliderWidget>("SLIDER");
			var value = row.Get<LabelWidget>("VALUE");
			var cost = row.Get<LabelWidget>("COST");
			cost.Bounds.X = width - 96;
			cost.Bounds.Width = 74;
			var nameWidth = (hasSlider ? slider.Bounds.X : cost.Bounds.X) - 26 - 4;

			var label = row.Get<LabelWidget>("NAME");
			label.Bounds.Width = nameWidth;
			var title = CityUi.Message(entry.NameKey);
			label.GetText = CityUi.Fitted(label, () => title);
			label.GetColor = () => Dark() ? CityTheme.InkLight : !Find(id).Unlocked ? CityTheme.Muted("people") : CityTheme.Ink;

			var effect = row.Get<LabelWidget>("EFFECT");
			effect.Bounds.Width = nameWidth;
			var description = CityUi.Message(entry.DescKey, "");
			effect.GetText = CityUi.Fitted(effect, () => description);
			effect.GetColor = () => Dark() ? CityTheme.InkLight : CityTheme.Muted("people");

			slider.IsVisible = () => hasSlider;
			slider.MinimumValue = entry.SliderMin;
			slider.MaximumValue = Math.Max(entry.SliderMin + 1, entry.SliderMax);
			slider.GetValue = () => Find(id).SliderValue;
			slider.IsDisabled = () => !Find(id).Unlocked || world.LocalPlayer == null;
			slider.OnCommit = v => world.IssueOrder(UiOrders.Policy(world.LocalPlayer, id, DistrictId, v));
			value.IsVisible = () => hasSlider;
			value.GetText = () => slider.DisplayValue.ToString(CultureInfo.CurrentCulture);
			value.GetColor = () => Dark() ? CityTheme.InkLight : CityTheme.Ink;

			var lockedText = FluentProvider.GetMessage(Locked);
			cost.GetText = () => !Find(id).Unlocked ? lockedText : CostText(Find(id));
			cost.GetColor = () =>
			{
				var p = Find(id);
				if (!p.Unlocked)
					return Dark() ? CityTheme.InkLight : CityTheme.Muted("people");

				if (p.UpkeepPerMonth == 0)
					return Dark() ? CityTheme.InkLight : CityTheme.Muted("people");

				var good = p.UpkeepPerMonth < 0;
				return Dark() ? (good ? CityTheme.MoneyPositiveLight : CityTheme.MoneyNegativeLight) : (good ? CityTheme.MoneyPositive : CityTheme.MoneyNegative);
			};

			var toggle = row.Get<CheckboxWidget>("TOGGLE");
			toggle.Bounds.X = width - 22;
			toggle.IsVisible = () => Find(id).Unlocked;
			toggle.IsChecked = () => Find(id).Active;
			toggle.IsDisabled = () => world.LocalPlayer == null;
			toggle.OnClick = () =>
			{
				selected = id;
				var policy = Find(id);
				var isSlider = policy.SliderMax > 0;
				var v = policy.Active ? 0 : isSlider ? Math.Max(policy.SliderMin, policy.SliderValue) : 1;
				world.IssueOrder(UiOrders.Policy(world.LocalPlayer, id, DistrictId, v));
			};

			var padlock = row.Get<CityIconWidget>("LOCK");
			padlock.Bounds.X = width - 24;
			padlock.IsVisible = () => !Find(id).Unlocked;
		}
	}
}
