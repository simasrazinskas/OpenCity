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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 ride-window style building inspector (design/iso/ui/panels/inspector-*.png), shown when exactly one city
	/// building is selected. The window takes the colours of the building's family (services, zoning, transit, city) and
	/// has five icon tabs: Overview (isometric thumbnail in the viewport well, status, meters, problem chips), Occupants
	/// (residents or workers, click a name for the citizen panel), Finances, Upkeep (service budget, upgrades, districts,
	/// hub production) and Efficiency (supply meters and the rows of the inspection contributors). The action column on
	/// the right holds the icon buttons (go to, info view, upgrades, districts, hub areas).
	/// The partial files hold the page contents (.Pages.cs), the occupants list (.Occupants.cs) and the actions (.Hub.cs).
	/// </summary>
	public partial class CityBuildingInfoLogic : ChromeLogic
	{
		[FluentReference]
		const string StatusOperational = "label-building-status-operational";

		[FluentReference]
		const string StatusConstruction = "label-building-status-construction";

		[FluentReference]
		const string StatusAbandoned = "label-building-status-abandoned";

		[FluentReference]
		const string StatusOffline = "label-building-status-offline";

		[FluentReference]
		const string Connected = "label-building-connected";

		[FluentReference]
		const string NoPower = "label-building-no-power";

		[FluentReference]
		const string NoWater = "label-building-no-water";

		[FluentReference]
		const string NoRoad = "label-building-no-road";

		[FluentReference]
		const string NoSewage = "label-building-no-sewage";

		[FluentReference("current", "max")]
		const string CurrentOfMax = "label-building-current-of-max";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		[FluentReference("amount")]
		const string PerMonth = "label-building-per-month";

		[FluentReference("level")]
		const string LevelOfFive = "label-building-level-of";

		[FluentReference("value")]
		const string HappinessTitle = "label-building-happiness-title";

		[FluentReference]
		const string TabOverview = "label-inspect-tab-overview";

		[FluentReference]
		const string TabOccupants = "label-inspect-tab-occupants";

		[FluentReference]
		const string TabFinances = "label-inspect-tab-finances";

		[FluentReference]
		const string TabUpkeep = "label-inspect-tab-upkeep";

		[FluentReference]
		const string TabEfficiency = "label-inspect-tab-efficiency";

		const int ExtraRows = 10;

		enum Page { Overview, Occupants, Finances, Upkeep, Efficiency }

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityManager manager;
		readonly CityPanelWidget panel;
		readonly Widget[] pages = new Widget[5];
		readonly List<InspectionRow> extra = [];
		readonly List<Chip> chips = [];
		readonly LinePool overviewLines;
		readonly LinePool financeLines;
		readonly LinePool efficiencyLines;
		readonly Widget chipContainer;
		readonly LabelWidget status;
		readonly CityIconWidget thumb;
		readonly CityIconWidget fallback;

		Actor actor;
		CityBuilding building;
		GrowableBuilding growable;
		Property property;
		Page page;
		int selectionHash = -1;
		int refreshedTick = -1;
		string titleText = "";

		[ObjectCreator.UseCtor]
		public CityBuildingInfoLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			manager = CityUi.GetManager(world);
			panel = (CityPanelWidget)widget;
			panel.GetTitle = () => titleText;
			panel.OnClose = world.Selection.Clear;

			foreach (var p in Enum.GetValues<Page>())
				pages[(int)p] = widget.Get("PAGE_" + p.ToString().ToUpperInvariant());

			actions = widget.Get("ACTIONS");

			var overview = pages[(int)Page.Overview];
			thumb = overview.Get<CityIconWidget>("THUMB");
			fallback = overview.Get<CityIconWidget>("FALLBACK");
			status = overview.Get<LabelWidget>("STATUS");
			status.GetText = StatusText;
			status.GetColor = StatusColor;
			chipContainer = overview.Get("CHIPS");
			overview.Get<LabelWidget>("NO_PROBLEMS").IsVisible = () => chips.Count == 0;

			var goTo = overview.Get<ButtonWidget>("GOTO");
			goTo.OnClick = Locate;
			var goToText = CityUi.Message("label-inspect-goto") + "\n" + CityUi.Message("label-inspect-goto-desc");
			goTo.GetTooltipText = () => goToText;

			overviewLines = new LinePool(world, overview.Get("METERS"), 4, 58);
			financeLines = new LinePool(world, pages[(int)Page.Finances], 13, 100);
			efficiencyLines = new LinePool(world, pages[(int)Page.Efficiency], 13, 100, HappinessTitleText, MakeHappinessHover);
			upkeepContent = pages[(int)Page.Upkeep];

			InitOccupants();
			InitTabs();

			// Visibility doubles as the update hook: it runs every frame, even while the panel is hidden.
			panel.IsVisible = () =>
			{
				Update();
				return building != null;
			};
		}

		void InitTabs()
		{
			CityWindowTab Tab(Page target, string icon, string key, Func<bool> available)
			{
				var text = FluentProvider.GetMessage(key);
				return new CityWindowTab
				{
					Icon = icon,
					GetTooltip = () => text,
					IsActive = () => page == target,
					IsDisabled = () => !available(),
					OnClick = () => ShowPage(target)
				};
			}

			panel.SetTabs(
			[
				Tab(Page.Overview, "ui_eye", TabOverview, () => true),
				Tab(Page.Occupants, "stat_population", TabOccupants, HasOccupants),
				Tab(Page.Finances, "stat_money", TabFinances, () => true),
				Tab(Page.Upkeep, "stat_fee", TabUpkeep, HasUpkeepPage),
				Tab(Page.Efficiency, "ui_chart_line", TabEfficiency, () => true)
			]);

			ShowPage(Page.Overview);
		}

		void ShowPage(Page target)
		{
			page = target;
			foreach (var p in Enum.GetValues<Page>())
				pages[(int)p].Visible = p == target;

			refreshedTick = -1;
			occupantsSignature = null;
		}

		void Locate()
		{
			if (actor == null)
				return;

			if (ctx.CenterOnWorld != null)
				ctx.CenterOnWorld(actor.CenterPosition);
			else
				ctx.CenterOn?.Invoke(actor.Location);
		}

		/// <summary>The window family of a building: services (red), zoning (orange), transit (blue), city (brown) for utilities and the rest.</summary>
		static string FamilyOf(Actor selected)
		{
			if (selected.TraitOrDefault<GrowableBuilding>() != null)
				return "zoning";

			var category = selected.Info.TraitInfoOrDefault<CityPlaceableInfo>()?.Category;
			if (category == "transit")
				return "transit";

			if (category == "power" || category == "water")
				return "city";

			var service = selected.Info.TraitInfoOrDefault<ServiceBuildingInfo>();
			if (service != null)
				return service.Kind is ServiceKind.Power or ServiceKind.Water or ServiceKind.Sewage ? "city" : "services";

			return "city";
		}

		string StatusText()
		{
			if (building == null)
				return "";

			if (growable != null && growable.UnderConstruction)
				return FluentProvider.GetMessage(StatusConstruction);

			if (growable != null && growable.Abandoned)
				return FluentProvider.GetMessage(StatusAbandoned);

			return FluentProvider.GetMessage(building.IsOperational ? StatusOperational : StatusOffline);
		}

		Color StatusColor()
		{
			if (building == null)
				return CityTheme.Ink;

			if (growable != null && growable.UnderConstruction)
				return CityUi.Warn;

			return (growable != null && growable.Abandoned) || !building.IsOperational ? CityUi.Bad : CityUi.Good;
		}

		void Update()
		{
			var selection = world.Selection;
			if (selection.Hash != selectionHash || (actor != null && (actor.IsDead || !actor.IsInWorld)))
			{
				selectionHash = selection.Hash;
				SetActor(selection.Actors.Count == 1 ? selection.Actors.First() : null);
			}

			if (building == null)
				return;

			// A growable keeps its property id across rebuilds but not its actor, so look the property up again.
			property = ctx.Properties?.GetByActor(actor);

			if (page == Page.Occupants && !HasOccupants())
				ShowPage(Page.Overview);
			else if (page == Page.Upkeep && !HasUpkeepPage())
				ShowPage(Page.Overview);

			// Texts change slowly: rebuild the page lines a few times per second.
			var tick = world.WorldTick / 4;
			if (tick != refreshedTick)
			{
				refreshedTick = tick;
				extra.Clear();
				foreach (var contributor in ctx.Contributors)
					contributor.Contribute(actor, property, extra);

				if (extra.Count > ExtraRows)
					extra.RemoveRange(ExtraRows, extra.Count - ExtraRows);

				switch (page)
				{
					case Page.Overview:
						RefreshOverview();
						break;
					case Page.Finances:
						RefreshFinances();
						break;
					case Page.Efficiency:
						RefreshEfficiency();
						break;
				}
			}

			if (page == Page.Occupants)
				RefreshOccupants();
		}

		void SetActor(Actor selected)
		{
			actor = null;
			building = null;
			growable = null;
			property = null;
			occupantsSignature = null;
			refreshedTick = -1;
			actions.RemoveChildren();
			actionsHeight = 0;
			upkeepContent.RemoveChildren();
			chips.Clear();
			chipSignature = null;
			chipContainer.RemoveChildren();

			if (selected == null || selected.IsDead || !selected.IsInWorld)
				return;

			var candidate = selected.TraitOrDefault<CityBuilding>();
			if (candidate == null)
				return;

			actor = selected;
			building = candidate;
			growable = selected.TraitOrDefault<GrowableBuilding>();
			SetFamily(FamilyOf(selected));

			var tooltip = selected.Info.TraitInfoOrDefault<TooltipInfo>();
			titleText = tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : selected.Info.Name;

			var hasThumb = CityTheme.Thumbnail(selected.Info.Name) != null;
			thumb.Icon = selected.Info.Name;
			thumb.IsVisible = () => hasThumb;
			fallback.Icon = FallbackIcon(selected);
			fallback.IsVisible = () => !hasThumb;

			BuildActions();
			BuildUpkeepPage();
			ShowPage(Page.Overview);
		}

		static string FallbackIcon(Actor selected)
		{
			var zone = selected.TraitOrDefault<GrowableBuilding>()?.Zone ?? ZoneType.None;
			switch (zone)
			{
				case ZoneType.ResidentialLow: return "zone_res_low";
				case ZoneType.ResidentialRow: return "zone_res_row";
				case ZoneType.ResidentialMedium: return "zone_res_med";
				case ZoneType.ResidentialHigh: return "zone_res_high";
				case ZoneType.ResidentialMixed: return "zone_res_mixed";
				case ZoneType.ResidentialLowRent: return "zone_res_lowrent";
				case ZoneType.CommercialLow: return "zone_com_low";
				case ZoneType.CommercialHigh: return "zone_com_high";
				case ZoneType.Industrial: return "zone_ind";
				case ZoneType.Warehouse: return "zone_warehouse";
				case ZoneType.Office: return "zone_off";
				case ZoneType.OfficeHigh: return "zone_off_high";
			}

			var category = selected.Info.TraitInfoOrDefault<CityPlaceableInfo>()?.Category;
			return category != null ? "cat_" + category : "stat_buildings";
		}
	}
}
