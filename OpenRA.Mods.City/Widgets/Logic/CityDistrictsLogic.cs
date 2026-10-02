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
	/// The RCT2 Districts window (design/iso/ui/panels/districts-*.png): the districts with colour swatch, population and active
	/// policies, tool buttons to paint a new district, paint or erase the selected one (rectangle drags issue CityDistrict orders),
	/// an inline rename field, delete, the figures of the selected district and a hint that follows the active tool.
	/// </summary>
	public class CityDistrictsLogic : ChromeLogic
	{
		[FluentReference]
		const string ActionNew = "button-districts-new";

		[FluentReference]
		const string ActionPaint = "button-districts-paint-district";

		[FluentReference]
		const string ActionErase = "button-districts-erase";

		[FluentReference]
		const string ActionRename = "button-districts-rename";

		[FluentReference]
		const string ActionDelete = "button-districts-delete";

		[FluentReference]
		const string RowPopulation = "label-districts-population";

		[FluentReference]
		const string RowHouseholds = "label-districts-households";

		[FluentReference]
		const string RowJobs = "label-districts-jobs";

		[FluentReference]
		const string RowHappiness = "label-districts-happiness";

		[FluentReference]
		const string RowLandValue = "label-districts-landvalue";

		[FluentReference]
		const string HintSelect = "label-districts-hint-select";

		[FluentReference]
		const string HintNone = "label-districts-hint-none";

		[FluentReference]
		const string HintRename = "label-districts-hint-rename";

		[FluentReference]
		const string HintPaint = "label-districts-hint-paint";

		[FluentReference]
		const string HintNew = "label-districts-hint-new";

		[FluentReference]
		const string HintErase = "label-districts-hint-erase";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		[FluentReference("count")]
		const string Cells = "label-tool-cells-count";

		// Right-hand columns of a row, in logical pixels from the right edge.
		const int PolicyColumn = 40, PopulationColumn = 60, NameStart = 26;

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		readonly Widget columns;
		readonly List<DistrictEntry> districts = [];
		readonly Dictionary<int, int> policyCounts = [];
		readonly Dictionary<int, TextFieldWidget> renameFields = [];

		bool renaming;
		bool focusPending;
		bool cancelRename;
		int renameFor;
		int builtWidth = -1;
		string listSignature;

		[ObjectCreator.UseCtor]
		public CityDistrictsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			list = widget.Get<ScrollPanelWidget>("LIST");
			columns = widget.Get("COLUMNS");

			var actions = widget.Get("ACTIONS");
			var paint = AddAction(actions, "PAINT", 0, 106, ActionPaint, "ui_colour", () => Selected() != null, () => Paint(ctx.SelectedDistrict, "district-paint"));
			paint.IsHighlighted = () => ctx.IsToolActive("district-paint");
			var erase = AddAction(actions, "ERASE", 110, 62, ActionErase, "tool_dezone", () => districts.Count > 0, () => Paint(0, "district-erase"));
			erase.IsHighlighted = () => ctx.IsToolActive("district-erase");
			var rename = AddAction(actions, "RENAME_BUTTON", 176, 74, ActionRename, "ui_edit", () => Selected() != null, StartRename);
			rename.IsHighlighted = () => renaming;
			var create = AddAction(actions, "NEW", 254, 56, ActionNew, "ui_plus", () => true, () => Paint(-1, "district-new"));
			create.IsHighlighted = () => ctx.IsToolActive("district-new");

			var delete = AddAction(actions, "DELETE", actions.Bounds.Width - 20, 20, null, "ui_trash", () => Selected() != null, () =>
			{
				if (world.LocalPlayer != null)
					world.IssueOrder(UiOrders.DistrictRemove(world.LocalPlayer, ctx.SelectedDistrict));

				ctx.SelectedDistrict = 0;
			});

			var deleteText = FluentProvider.GetMessage(ActionDelete);
			delete.GetTooltipText = () => deleteText;
			delete.LeftMargin = 0;

			BuildDetails(widget);

			var hint = widget.Get<LabelWidget>("HINT");
			hint.GetText = () => FluentProvider.GetMessage(HintKey());

			widget.Get<LogicTickerWidget>("DISTRICTS_TICKER").OnTick = Refresh;
		}

		string HintKey()
		{
			if (renaming)
				return HintRename;
			if (ctx.IsToolActive("district-paint"))
				return HintPaint;
			if (ctx.IsToolActive("district-new"))
				return HintNew;
			if (ctx.IsToolActive("district-erase"))
				return HintErase;

			return districts.Count == 0 ? HintNone : HintSelect;
		}

		DistrictEntry? Selected()
		{
			foreach (var d in districts)
				if (d.Id == ctx.SelectedDistrict)
					return d;

			return null;
		}

		ButtonWidget AddAction(Widget actions, string id, int x, int width, string key, string icon, Func<bool> enabled, Action click)
		{
			var button = (ButtonWidget)Game.LoadWidget(world, "CITY_DISTRICT_ACTION", actions, []);
			button.Id = id;
			button.Bounds.X = x;
			button.Bounds.Width = width;
			button.Get<CityIconWidget>("ICON").Icon = icon;
			var text = key == null ? "" : FluentProvider.GetMessage(key);
			button.GetText = () => text;
			button.IsDisabled = () => !enabled() || world.LocalPlayer == null;
			button.OnClick = click;
			if (key == null)
				button.Get("ICON").Bounds.X = (width - 16) / 2;

			return button;
		}

		void Paint(int districtId, string toolId)
		{
			StopRename();
			if (ctx.IsToolActive(toolId))
			{
				ctx.CancelTool();
				return;
			}

			ctx.ActivateTool(toolId, new UiAreaToolGenerator(world, (p, a, b) => UiOrders.DistrictPaint(p, a, b, districtId),
				Color.FromArgb(110, 90, 160, 255),
				(a, b) => FluentProvider.GetMessage(Cells, "count", CityUtils.Rect(a, b).Count())));
		}

		void StartRename()
		{
			if (Selected() == null)
				return;

			ctx.CancelTool();
			renaming = !renaming;
			renameFor = ctx.SelectedDistrict;
			focusPending = renaming;
			cancelRename = false;
			if (!renaming)
				Commit(renameFor);
		}

		void StopRename()
		{
			if (!renaming)
				return;

			renaming = false;
			if (renameFields.TryGetValue(renameFor, out var field))
				field.YieldKeyboardFocus();
		}

		void Commit(int id)
		{
			if (!renameFields.TryGetValue(id, out var field) || world.LocalPlayer == null)
				return;

			foreach (var d in districts)
				if (d.Id == id && field.Text != d.Name && !string.IsNullOrWhiteSpace(field.Text))
					world.IssueOrder(UiOrders.DistrictRename(world.LocalPlayer, id, field.Text));
		}

		void BuildDetails(Widget panel)
		{
			var details = panel.Get("DETAILS");
			var header = panel.Get<CityHeaderWidget>("DETAIL_HEADER");
			header.GetText = () => Selected() is { } d ? DisplayName(d) : "";

			Widget AddRow(int index, string icon, string key, Func<string> value, Func<Color> color)
			{
				var row = Game.LoadWidget(world, "CITY_DISTRICT_DETAIL", details, []);
				row.Bounds.Y = index * 13;
				row.Get<CityIconWidget>("ICON").Icon = icon;
				var name = FluentProvider.GetMessage(key);
				row.Get<LabelWidget>("NAME").GetText = () => name;
				var label = row.Get<LabelWidget>("VALUE");
				label.GetText = value;
				label.GetColor = color;
				row.IsVisible = () => Selected() != null;
				return row;
			}

			string Number(Func<DistrictEntry, int> read) => Selected() is { } d ? read(d).ToString("N0", CultureInfo.CurrentCulture) : "";
			Color Ink() => CityTheme.Ink;

			AddRow(0, "stat_population", RowPopulation, () => Number(d => d.Population), Ink);
			AddRow(1, "stat_households", RowHouseholds, () => Number(d => d.Households), Ink);
			AddRow(2, "stat_jobs", RowJobs, () => Number(d => d.Jobs), Ink);
			AddRow(3, "stat_land_value", RowLandValue, () => Number(d => d.LandValue), Ink);

			var happiness = AddRow(4, "stat_happiness", RowHappiness,
				() => Selected() is { } d ? FluentProvider.GetMessage(Percent, "value", d.Happiness) : "", Ink);
			var bar = happiness.Get<CityBarWidget>("BAR");
			bar.Visible = true;
			bar.GetPercentage = () => Selected()?.Happiness ?? 0;
			bar.GetRamp = () => Selected() is { } d && d.Happiness < 40 ? "red" : Selected() is { } e && e.Happiness < 60 ? "yellow" : "green";
		}

		static string DisplayName(DistrictEntry d) => string.IsNullOrEmpty(d.Name) ? "#" + d.Id : d.Name;

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			districts.Clear();
			districts.AddRange(source.Districts);

			policyCounts.Clear();
			foreach (var d in districts)
			{
				var count = 0;
				foreach (var policy in source.Policies(d.Id))
					if (policy.Active)
						count++;

				policyCounts[d.Id] = count;
			}

			var width = list.Bounds.Width - list.ScrollbarWidth - 2;
			var signature = string.Join(",", districts.ConvertAll(d => d.Id + d.Name));
			if (signature != listSignature || width != builtWidth)
			{
				listSignature = signature;
				builtWidth = width;
				BuildList(width);
			}

			if (renaming && (renameFor != ctx.SelectedDistrict || Selected() == null))
				StopRename();

			// The rename field takes the keyboard once its row is shown.
			if (focusPending && renameFields.TryGetValue(renameFor, out var field))
			{
				focusPending = false;
				field.Text = Selected()?.Name ?? "";
				field.TakeKeyboardFocus();
				field.CursorPosition = field.Text.Length;
			}

			LayoutHeader(columns, width);
		}

		/// <summary>Positions the right-hand columns of a list row.</summary>
		static void LayoutRow(Widget row, int width)
		{
			row.Get("POLICIES").Bounds.X = width - 24;
			row.Get("POLICY_ICON").Bounds.X = width - PolicyColumn + 2;
			var population = row.Get("POPULATION");
			population.Bounds.X = width - PolicyColumn - 8 - population.Bounds.Width;
			row.Get("NAME").Bounds.Width = width - NameStart - PolicyColumn - PopulationColumn - 6;
		}

		static void LayoutHeader(Widget header, int width)
		{
			var policies = header.Get("C_POLICIES");
			policies.Bounds.X = width - 4 - policies.Bounds.Width;
			var population = header.Get("C_POPULATION");
			population.Bounds.X = width - PolicyColumn - 8 - population.Bounds.Width;
			header.Get("C_NAME").Bounds.Width = width - NameStart - PolicyColumn - PopulationColumn - 6;
		}

		void BuildList(int width)
		{
			list.RemoveChildren();
			renameFields.Clear();
			foreach (var district in districts)
			{
				var id = district.Id;
				var entry = district;
				var row = Game.LoadWidget(world, "CITY_DISTRICT_ROW", list, []);
				row.Bounds.Width = width;
				LayoutRow(row, width);

				bool Selected() => ctx.SelectedDistrict == id;
				row.Get<CityRowBackgroundWidget>("BG").IsSelected = Selected;
				row.Get<CitySwatchWidget>("SWATCH").GetColor = () => CityUi.FromArgb(entry.ArgbColor);

				var name = DisplayName(entry);
				var nameLabel = row.Get<LabelWidget>("NAME");
				nameLabel.GetText = CityUi.Fitted(nameLabel, () => name);
				nameLabel.GetColor = () => Selected() ? CityTheme.InkLight : CityTheme.Ink;
				nameLabel.IsVisible = () => !(renaming && renameFor == id);

				var field = row.Get<TextFieldWidget>("RENAME");
				field.IsVisible = () => renaming && renameFor == id;
				field.OnEnterKey = _ =>
				{
					Commit(id);
					StopRename();
					return true;
				};

				field.OnEscKey = _ =>
				{
					cancelRename = true;
					StopRename();
					return true;
				};

				field.OnLoseFocus = () =>
				{
					if (renaming && renameFor == id && !focusPending)
					{
						if (!cancelRename)
							Commit(id);

						renaming = false;
					}
				};

				renameFields[id] = field;

				var population = row.Get<LabelWidget>("POPULATION");
				var text = entry.Population.ToString("N0", CultureInfo.CurrentCulture);
				population.GetText = () => text;
				population.GetColor = () => Selected() ? CityTheme.InkLight : CityTheme.Ink;

				var policies = row.Get<LabelWidget>("POLICIES");
				policies.GetText = () => policyCounts.TryGetValue(id, out var n) ? n.ToString(CultureInfo.CurrentCulture) : "0";
				policies.GetColor = () => Selected() ? CityTheme.InkLight : CityTheme.Ink;

				row.Get<ButtonWidget>("SELECT").OnClick = () =>
				{
					if (renaming && renameFor != id)
						StopRename();

					ctx.SelectedDistrict = id;
				};
			}

			list.Layout.AdjustChildren();
		}
	}
}
