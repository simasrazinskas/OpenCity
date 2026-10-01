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
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Policies panel: city policies and per-district policies (data driven, IProgressionUiSource.Policies). A policy is
	/// a toggle or a slider; changes are CitySetPolicy orders. Locked policies are shown disabled.
	/// </summary>
	public class CityPoliciesLogic : ChromeLogic
	{
		[FluentReference]
		const string ScopeCity = "label-policies-city";

		[FluentReference]
		const string ScopeDistrict = "label-policies-district";

		[FluentReference]
		const string NoDistricts = "label-policies-no-districts";

		[FluentReference("amount")]
		const string Upkeep = "label-policies-upkeep";

		[FluentReference]
		const string Locked = "label-policies-locked";

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		readonly LabelWidget districtName;
		readonly List<PolicyEntry> current = [];
		readonly StringBuilder signature = new();

		bool districtScope;
		string builtSignature;

		[ObjectCreator.UseCtor]
		public CityPoliciesLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			list = widget.Get<ScrollPanelWidget>("LIST");
			districtName = widget.Get<LabelWidget>("DISTRICT_NAME");
			districtName.IsVisible = () => districtScope;
			districtName.GetText = DistrictLabel;

			BuildScope(widget.Get("SCOPE"));

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		int DistrictId => districtScope ? ctx.SelectedDistrict : 0;

		string DistrictLabel()
		{
			var districts = ctx.ProgressionUi?.Districts;
			if (districts == null || districts.Count == 0)
				return FluentProvider.GetMessage(NoDistricts);

			foreach (var d in districts)
				if (d.Id == ctx.SelectedDistrict)
					return string.IsNullOrEmpty(d.Name) ? "#" + d.Id : d.Name;

			return FluentProvider.GetMessage(NoDistricts);
		}

		void BuildScope(Widget scope)
		{
			AddScopeButton(scope, 0, 120, ScopeCity, () => !districtScope, () => districtScope = false);
			AddScopeButton(scope, 124, 120, ScopeDistrict, () => districtScope, () => districtScope = true);
			AddArrow(scope, 440, "arrow-left", -1);
			AddArrow(scope, 480, "arrow-right", 1);
		}

		void AddScopeButton(Widget scope, int x, int width, string key, System.Func<bool> highlighted, System.Action click)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", scope, []) as ButtonWidget;
			button.Bounds.X = x;
			button.Bounds.Width = width;
			button.Bounds.Height = 28;
			var text = FluentProvider.GetMessage(key);
			button.GetText = () => text;
			button.IsHighlighted = highlighted;
			button.OnClick = () =>
			{
				click();
				builtSignature = null;
			};
		}

		void AddArrow(Widget scope, int x, string icon, int step)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", scope, []) as ButtonWidget;
			button.Bounds.X = x;
			button.Bounds.Width = 36;
			button.Bounds.Height = 28;
			button.IsVisible = () => districtScope;
			var image = new ImageWidget
			{
				ImageCollection = "city-icons-small",
				ImageName = icon,
				Bounds = new WidgetBounds(10, 6, 16, 16)
			};

			button.AddChild(image);
			button.OnClick = () =>
			{
				var districts = ctx.ProgressionUi?.Districts;
				if (districts == null || districts.Count == 0)
					return;

				var index = 0;
				for (var i = 0; i < districts.Count; i++)
					if (districts[i].Id == ctx.SelectedDistrict)
						index = i;

				ctx.SelectedDistrict = districts[(index + step + districts.Count) % districts.Count].Id;
				builtSignature = null;
			};
		}

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			if (districtScope && ctx.SelectedDistrict == 0 && source.Districts.Count > 0)
				ctx.SelectedDistrict = source.Districts[0].Id;

			current.Clear();
			current.AddRange(source.Policies(DistrictId));

			// Rebuild only when the set of policies or the scope changes; values are read live.
			signature.Clear();
			signature.Append(districtScope).Append(DistrictId);
			foreach (var p in current)
				signature.Append('|').Append(p.Id).Append(p.Unlocked);

			var text = signature.ToString();
			if (text == builtSignature)
				return;

			builtSignature = text;
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

		void AddRow(string id)
		{
			var entry = Find(id);
			var row = Game.LoadWidget(world, "CITY_POLICY_ROW", list, []);
			row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;

			var name = CityUi.Message(entry.NameKey);
			var nameLabel = row.Get<LabelWithTooltipWidget>("NAME");
			nameLabel.GetText = () => name;
			nameLabel.GetColor = () => Find(id).Unlocked ? Color.White : CityUi.Muted;
			var tip = name + "\n" + CityUi.Message(entry.DescKey, "");
			nameLabel.GetTooltipText = () => tip;

			var toggle = row.Get<ButtonWidget>("TOGGLE");
			toggle.IsDisabled = () => !Find(id).Unlocked || world.LocalPlayer == null;
			toggle.IsHighlighted = () => Find(id).Active;
			toggle.Get<ImageWidget>("CHECK").IsVisible = () => Find(id).Active;
			toggle.OnClick = () =>
			{
				var policy = Find(id);
				var slider = policy.SliderMax > 0;
				var value = policy.Active ? 0 : slider ? System.Math.Max(policy.SliderMin, policy.SliderValue) : 1;
				world.IssueOrder(UiOrders.Policy(world.LocalPlayer, id, DistrictId, value));
			};

			var slider = row.Get<CitySliderWidget>("SLIDER");
			slider.IsVisible = () => entry.SliderMax > 0;
			slider.MinimumValue = entry.SliderMin;
			slider.MaximumValue = System.Math.Max(entry.SliderMin + 1, entry.SliderMax);
			slider.GetValue = () => Find(id).SliderValue;
			slider.IsDisabled = () => !Find(id).Unlocked || world.LocalPlayer == null;
			slider.OnCommit = value => world.IssueOrder(UiOrders.Policy(world.LocalPlayer, id, DistrictId, value));

			// Columns from the right: upkeep, slider value, slider; the name takes the rest.
			var width = row.Bounds.Width;
			slider.Bounds.X = width - 292;
			slider.Bounds.Width = 110;
			nameLabel.Bounds.Width = slider.Bounds.X - 8 - nameLabel.Bounds.X;

			var value = row.Get<LabelWidget>("VALUE");
			value.Bounds.X = width - 174;
			value.Bounds.Width = 50;
			value.IsVisible = () => entry.SliderMax > 0;
			value.GetText = () => slider.DisplayValue.ToString(System.Globalization.CultureInfo.CurrentCulture);

			var upkeep = row.Get<LabelWidget>("UPKEEP");
			upkeep.Bounds.X = width - 116;
			upkeep.Bounds.Width = 110;
			upkeep.GetText = () => !Find(id).Unlocked ? FluentProvider.GetMessage(Locked) :
				Find(id).UpkeepPerMonth > 0 ? FluentProvider.GetMessage(Upkeep, "amount", CityUtils.FormatMoney(Find(id).UpkeepPerMonth)) : "";
		}
	}
}
