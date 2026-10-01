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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Info panel shown when exactly one city building is selected: status, level, households and jobs by education,
	/// company, utilities, rent, land value, happiness (hover for factors), rows added by inspection contributors and
	/// the lists of residents and workers (click a name to open the citizen panel).
	/// </summary>
	public partial class CityBuildingInfoLogic : ChromeLogic
	{
		[FluentReference("zone", "level")]
		const string ZoneAndLevel = "label-building-zone-level";

		[FluentReference]
		const string RowStatus = "label-building-status";

		[FluentReference]
		const string RowResidents = "label-building-residents";

		[FluentReference]
		const string RowJobs = "label-building-jobs";

		[FluentReference]
		const string RowPower = "label-building-power";

		[FluentReference]
		const string RowWater = "label-building-water";

		[FluentReference]
		const string RowRoad = "label-building-road";

		[FluentReference]
		const string RowHappiness = "label-building-happiness";

		[FluentReference]
		const string RowLandValue = "label-building-landvalue";

		[FluentReference]
		const string RowUpkeep = "label-building-upkeep";

		[FluentReference]
		const string RowLevel = "label-building-level";

		[FluentReference]
		const string RowHouseholds = "label-building-households";

		[FluentReference]
		const string RowStudents = "label-building-students";

		[FluentReference]
		const string RowBeds = "label-building-beds";

		[FluentReference]
		const string RowCompany = "label-building-company";

		[FluentReference]
		const string RowSewage = "label-building-sewage";

		[FluentReference]
		const string RowRent = "label-building-rent";

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

		[FluentReference]
		const string TabResidents = "label-building-tab-residents";

		[FluentReference]
		const string TabWorkers = "label-building-tab-workers";

		[FluentReference("value")]
		const string HappinessTitle = "label-building-happiness-title";

		const int ListHeight = 132;
		const int ExtraRows = 10;
		const int MaxListed = 60;

		sealed class Row
		{
			public Widget Widget;
			public Func<bool> Visible;
			public LabelWidget Name;
			public LabelWidget Value;
		}

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityManager manager;
		readonly Widget panel;
		readonly Widget rowContainer;
		readonly Widget listTabs;
		readonly ScrollPanelWidget list;
		readonly ScrollItemWidget listTemplate;
		readonly ButtonWidget tabResidents;
		readonly ButtonWidget tabWorkers;
		readonly List<Row> rows = [];
		readonly List<Row> extraRows = [];
		readonly List<InspectionRow> extra = [];
		readonly LabelWidget title;
		readonly LabelWidget subtitle;

		Actor actor;
		CityBuilding building;
		GrowableBuilding growable;
		Property property;
		int selectionHash = -1;
		string titleText = "";
		string subtitleText = "";
		bool showWorkers;
		string listSignature;

		[ObjectCreator.UseCtor]
		public CityBuildingInfoLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			manager = CityUi.GetManager(world);
			panel = widget;

			title = widget.Get<LabelWidget>("TITLE");
			title.GetText = () => titleText;
			subtitle = widget.Get<LabelWidget>("SUBTITLE");
			subtitle.GetText = () => subtitleText;

			rowContainer = widget.Get("ROWS");
			actions = widget.Get("ACTIONS");
			listTabs = widget.Get("LIST_TABS");
			list = widget.Get<ScrollPanelWidget>("LIST");
			listTemplate = list.Get<ScrollItemWidget>("LIST_TEMPLATE");
			list.RemoveChild(listTemplate);

			tabResidents = AddTab(0, TabResidents, () => !showWorkers, () => showWorkers = false);
			tabWorkers = AddTab(128, TabWorkers, () => showWorkers, () => showWorkers = true);

			BuildRows();

			// Visibility doubles as the update hook: it runs every frame, even while the panel is hidden.
			panel.IsVisible = () =>
			{
				Update();
				return building != null;
			};
		}

		ButtonWidget AddTab(int x, string key, Func<bool> highlighted, Action onClick)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", listTabs, []) as ButtonWidget;
			button.Bounds.X = x;
			button.Bounds.Width = 124;
			button.Bounds.Height = 24;
			var text = FluentProvider.GetMessage(key);
			button.GetText = () => text;
			button.IsHighlighted = highlighted;
			button.OnClick = () =>
			{
				onClick();
				listSignature = null;
			};

			return button;
		}

		void BuildRows()
		{
			AddRow(RowStatus, StatusText, StatusColor, () => true);
			AddRow(RowLevel, () => FluentProvider.GetMessage(LevelOfFive, "level", property.Level), () => Color.White,
				() => property != null && property.Level > 0 && growable != null);

			AddRow(RowHouseholds, () => FluentProvider.GetMessage(CurrentOfMax, "current", property.Households, "max", property.HouseholdSlots),
				() => Color.White, () => property != null && property.HouseholdSlots > 0);

			AddRow(RowResidents, ResidentsText, () => Color.White,
				() => property != null ? property.HouseholdSlots > 0 : building.Info.MaxResidents > 0);

			AddRow(RowJobs, JobsText, () => Color.White,
				() => property != null ? property.TotalJobSlots > 0 : building.Info.MaxJobs > 0);

			for (var i = 0; i < 5; i++)
			{
				var level = (EducationLevel)i;
				AddRow("", () => FluentProvider.GetMessage(CurrentOfMax, "current", property.JobsFilled[(int)level], "max", property.JobSlots[(int)level]),
					() => Color.White, () => property != null && property.JobSlots[(int)level] > 0, "   " + CityUi.EducationName(level));
			}

			AddRow(RowStudents, () => FluentProvider.GetMessage(CurrentOfMax, "current", property.Students, "max", property.StudentSeats),
				() => Color.White, () => property != null && property.StudentSeats > 0);

			AddRow(RowBeds, () => FluentProvider.GetMessage(CurrentOfMax, "current", property.Patients, "max", property.Beds),
				() => Color.White, () => property != null && property.Beds > 0);

			AddRow(RowCompany, () => ctx.Economy.DescribeCompany(property.Id) ?? "", () => Color.White,
				() => property != null && property.CompanyId != 0 && ctx.Economy != null && ctx.Economy.DescribeCompany(property.Id) != null);

			AddRow(RowPower, PowerText, PowerColor, () => true);
			AddRow(RowWater, WaterText, WaterColor, () => true);
			AddRow(RowSewage, () => FluentProvider.GetMessage(ctx.Utilities.HasSewage(actor) ? Connected : NoSewage),
				() => ctx.Utilities.HasSewage(actor) ? CityUi.Good : CityUi.Bad, () => ctx.Utilities != null);

			AddRow(RowRoad, () => FluentProvider.GetMessage(building.HasRoadAccess && (property == null || property.HasRoadAccess) ? Connected : NoRoad),
				() => building.HasRoadAccess && (property == null || property.HasRoadAccess) ? CityUi.Good : CityUi.Bad, () => true);

			var happiness = AddRow(RowHappiness, () => FluentProvider.GetMessage(Percent, "value", HappinessValue()),
				() => CityUi.PercentColor(HappinessValue()), () => true);

			// Hovering the happiness row lists the factors the citizen simulation reports for the whole city.
			var hover = new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				Bounds = new WidgetBounds(0, 0, happiness.Widget.Bounds.Width, happiness.Widget.Bounds.Height),
				GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", HappinessValue()),
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};

			happiness.Widget.AddChild(hover);

			AddRow(RowLandValue, () => (property?.LandValue ?? building.LandValue).ToString(CultureInfo.CurrentCulture), () => Color.White, () => true);
			AddRow(RowRent, () => FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(property.RentPerMonth)), () => Color.White,
				() => property != null && property.RentPerMonth > 0);

			AddRow(RowUpkeep, () => FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(building.Info.Upkeep)),
				() => Color.White, () => building.Info.Upkeep > 0);

			for (var i = 0; i < ExtraRows; i++)
			{
				var index = i;
				var row = AddRow("", () => index < extra.Count ? extra[index].Value : "",
					() => index < extra.Count ? CityUi.ToneColor(extra[index].Tone) : Color.White, () => index < extra.Count);

				row.Name.GetText = () => index < extra.Count ? extra[index].Label : "";
				rows.Remove(row);
				extraRows.Add(row);
			}
		}

		Row AddRow(string labelKey, Func<string> value, Func<Color> color, Func<bool> visible, string rawLabel = null)
		{
			var widget = Game.LoadWidget(world, "CITY_INFO_ROW", rowContainer, []);
			var name = rawLabel ?? (labelKey.Length > 0 ? FluentProvider.GetMessage(labelKey) : "");
			var nameLabel = widget.Get<LabelWidget>("NAME");
			nameLabel.GetText = () => name;
			var valueLabel = widget.Get<LabelWidget>("VALUE");
			valueLabel.GetText = value;
			valueLabel.GetColor = color;

			var row = new Row { Widget = widget, Visible = visible, Name = nameLabel, Value = valueLabel };
			rows.Add(row);
			return row;
		}

		int HappinessValue()
		{
			if (property != null && ctx.Citizens != null && property.Residents > 0)
			{
				long sum = 0;
				var count = 0;
				foreach (var id in ctx.Citizens.ResidentsOf(property.Id))
				{
					if (ctx.Citizens.TryGetCitizen(id, out var view))
					{
						sum += view.Happiness;
						count++;
					}

					if (count >= 20)
						break;
				}

				if (count > 0)
					return (int)(sum / count);
			}

			return building.Happiness;
		}

		string ResidentsText()
		{
			var residents = property?.Residents ?? building.Residents;
			var capacity = property != null ? property.HouseholdSlots * 3 : building.Info.MaxResidents;
			return FluentProvider.GetMessage(CurrentOfMax, "current", residents, "max", capacity);
		}

		string JobsText()
		{
			if (property != null)
				return FluentProvider.GetMessage(CurrentOfMax, "current", property.TotalJobsFilled, "max", property.TotalJobSlots);

			return FluentProvider.GetMessage(CurrentOfMax, "current", building.Workers, "max", building.Info.MaxJobs);
		}

		string PowerText()
		{
			if (ctx.Utilities != null)
				return FluentProvider.GetMessage(Percent, "value", ctx.Utilities.PowerPercent(actor));

			return FluentProvider.GetMessage(building.HasPower ? Connected : NoPower);
		}

		Color PowerColor()
		{
			return ctx.Utilities != null ? CityUi.PercentColor(ctx.Utilities.PowerPercent(actor)) : building.HasPower ? CityUi.Good : CityUi.Bad;
		}

		string WaterText()
		{
			if (ctx.Utilities != null)
				return FluentProvider.GetMessage(Percent, "value", ctx.Utilities.WaterPercent(actor));

			return FluentProvider.GetMessage(building.HasWater ? Connected : NoWater);
		}

		Color WaterColor()
		{
			return ctx.Utilities != null ? CityUi.PercentColor(ctx.Utilities.WaterPercent(actor)) : building.HasWater ? CityUi.Good : CityUi.Bad;
		}

		string StatusText()
		{
			if (growable != null && growable.UnderConstruction)
				return FluentProvider.GetMessage(StatusConstruction);

			if (growable != null && growable.Abandoned)
				return FluentProvider.GetMessage(StatusAbandoned);

			return FluentProvider.GetMessage(building.IsOperational ? StatusOperational : StatusOffline);
		}

		Color StatusColor()
		{
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

			extra.Clear();
			foreach (var contributor in ctx.Contributors)
				contributor.Contribute(actor, property, extra);

			if (extra.Count > ExtraRows)
				extra.RemoveRange(ExtraRows, extra.Count - ExtraRows);

			var y = 0;
			foreach (var row in rows.Concat(extraRows))
			{
				var visible = row.Visible();
				row.Widget.Visible = visible;
				if (!visible)
					continue;

				row.Widget.Bounds.Y = y;
				y += row.Widget.Bounds.Height + 2;
			}

			rowContainer.Bounds.Height = y;
			var bottom = rowContainer.Bounds.Y + y + 8;

			actions.Bounds.Y = bottom;
			bottom += actionsHeight;

			var hasResidents = property != null && (property.Residents > 0 || property.HouseholdSlots > 0);
			var hasWorkers = property != null && (property.TotalJobSlots > 0);
			var listing = ctx.Citizens != null && (hasResidents || hasWorkers);
			listTabs.Visible = list.Visible = listing;
			if (listing)
			{
				if (showWorkers && !hasWorkers)
					showWorkers = false;
				else if (!showWorkers && !hasResidents)
					showWorkers = true;

				tabResidents.Visible = hasResidents;
				tabWorkers.Visible = hasWorkers;
				listTabs.Bounds.Y = bottom;
				list.Bounds.Y = bottom + 28;
				list.Bounds.Height = ListHeight;
				bottom += 28 + ListHeight + 10;
				RefreshList();
			}

			panel.Bounds.Height = bottom;
		}

		void RefreshList()
		{
			var signature = property.Id + ":" + showWorkers + ":" + property.Residents + ":" + property.TotalJobsFilled;
			if (signature == listSignature)
				return;

			listSignature = signature;
			list.RemoveChildren();
			var ids = showWorkers ? ctx.Citizens.WorkersOf(property.Id) : ctx.Citizens.ResidentsOf(property.Id);
			var count = 0;
			foreach (var id in ids)
			{
				if (count++ >= MaxListed)
					break;

				if (!ctx.Citizens.TryGetCitizen(id, out var view))
					continue;

				var citizenId = id;
				var item = ScrollItemWidget.Setup(listTemplate, () => ctx.SelectedCitizen == citizenId, () => ctx.SelectedCitizen = citizenId, () => { });
				var line = view.Name + "  (" + CityUi.AgeName(view.AgeGroup) + ", " + CityUi.EducationName(view.Education) + ")";
				item.Get<LabelWidget>("NAME").GetText = () => line;
				list.AddChild(item);
			}

			list.ScrollToTop();
		}

		void SetActor(Actor selected)
		{
			actor = null;
			building = null;
			growable = null;
			property = null;
			listSignature = null;
			actions.RemoveChildren();
			actionsHeight = 0;

			if (selected == null || selected.IsDead || !selected.IsInWorld)
				return;

			var candidate = selected.TraitOrDefault<CityBuilding>();
			if (candidate == null)
				return;

			actor = selected;
			building = candidate;
			growable = selected.TraitOrDefault<GrowableBuilding>();
			BuildActions();

			var tooltip = selected.Info.TraitInfoOrDefault<TooltipInfo>();
			titleText = tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : selected.Info.Name;

			if (growable != null)
				subtitleText = FluentProvider.GetMessage(ZoneAndLevel, "zone", CityUi.ZoneName(growable.Zone), "level", growable.Level);
			else
			{
				var placeable = selected.Info.TraitInfoOrDefault<CityPlaceableInfo>();
				var key = placeable != null ? "label-city-category-" + placeable.Category : null;
				subtitleText = key != null && FluentProvider.TryGetMessage(key, out var category) ? category : "";
			}
		}
	}
}
