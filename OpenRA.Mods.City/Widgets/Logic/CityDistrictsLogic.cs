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
	/// Districts panel: the districts with their colour and population, tools to paint a new district, paint or erase the
	/// selected one (rectangle drags issue CityDistrict orders), rename and delete, and the statistics of the selected district.
	/// </summary>
	public class CityDistrictsLogic : ChromeLogic
	{
		[FluentReference]
		const string ActionNew = "button-districts-new";

		[FluentReference]
		const string ActionPaint = "button-districts-paint";

		[FluentReference]
		const string ActionErase = "button-districts-erase";

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

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		[FluentReference("count")]
		const string Cells = "label-tool-cells-count";

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		readonly TextFieldWidget nameField;
		readonly List<DistrictEntry> districts = [];

		int fieldFor = -1;
		string listSignature;

		[ObjectCreator.UseCtor]
		public CityDistrictsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			list = widget.Get<ScrollPanelWidget>("LIST");

			nameField = widget.Get<TextFieldWidget>("NAME_FIELD");
			nameField.OnEnterKey = _ =>
			{
				Rename();
				nameField.YieldKeyboardFocus();
				return true;
			};

			nameField.OnLoseFocus = Rename;
			nameField.IsDisabled = () => Selected() == null;

			var actions = widget.Get("ACTIONS");
			AddAction(actions, 0, ActionNew, () => true, () => Paint(-1, "district-new"));
			AddAction(actions, 1, ActionPaint, () => Selected() != null, () => Paint(ctx.SelectedDistrict, "district-paint"));
			AddAction(actions, 2, ActionErase, () => districts.Count > 0, () => Paint(0, "district-erase"));
			AddAction(actions, 3, ActionDelete, () => Selected() != null, () =>
			{
				if (world.LocalPlayer != null)
					world.IssueOrder(UiOrders.DistrictRemove(world.LocalPlayer, ctx.SelectedDistrict));

				ctx.SelectedDistrict = 0;
			});

			BuildDetails(widget.Get("DETAILS"));

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		DistrictEntry? Selected()
		{
			foreach (var d in districts)
				if (d.Id == ctx.SelectedDistrict)
					return d;

			return null;
		}

		void AddAction(Widget actions, int index, string key, Func<bool> enabled, Action click)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", actions, []) as ButtonWidget;
			button.Bounds.X = index * 76;
			button.Bounds.Width = 72;
			button.Bounds.Height = 28;
			var text = FluentProvider.GetMessage(key);
			button.GetText = () => text;
			button.IsDisabled = () => !enabled() || world.LocalPlayer == null;
			button.OnClick = click;
		}

		void Paint(int districtId, string toolId)
		{
			if (ctx.IsToolActive(toolId))
			{
				ctx.CancelTool();
				return;
			}

			ctx.ActivateTool(toolId, new UiAreaToolGenerator(world, (p, a, b) => UiOrders.DistrictPaint(p, a, b, districtId),
				Color.FromArgb(110, 90, 160, 255),
				(a, b) => FluentProvider.GetMessage(Cells, "count", CityUtils.Rect(a, b).Count())));
		}

		void Rename()
		{
			var selected = Selected();
			if (selected == null || world.LocalPlayer == null || nameField.Text == selected.Value.Name)
				return;

			world.IssueOrder(UiOrders.DistrictRename(world.LocalPlayer, selected.Value.Id, nameField.Text));
		}

		void BuildDetails(Widget details)
		{
			void AddRow(int index, string key, Func<string> value, Func<Color> color)
			{
				var row = Game.LoadWidget(world, "CITY_INFO_ROW", details, []);
				row.Bounds.Y = index * 22;
				var name = FluentProvider.GetMessage(key);
				row.Get<LabelWidget>("NAME").GetText = () => name;
				var label = row.Get<LabelWidget>("VALUE");
				label.GetText = value;
				label.GetColor = color;
			}

			string Number(Func<DistrictEntry, int> read) => Selected() is { } d ? read(d).ToString("N0", CultureInfo.CurrentCulture) : "";

			AddRow(0, RowPopulation, () => Number(d => d.Population), () => Color.White);
			AddRow(1, RowHouseholds, () => Number(d => d.Households), () => Color.White);
			AddRow(2, RowJobs, () => Number(d => d.Jobs), () => Color.White);
			AddRow(3, RowHappiness, () => Selected() is { } d ? FluentProvider.GetMessage(Percent, "value", d.Happiness) : "",
				() => Selected() is { } d ? CityUi.PercentColor(d.Happiness) : Color.White);

			AddRow(4, RowLandValue, () => Number(d => d.LandValue), () => Color.White);
		}

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			districts.Clear();
			districts.AddRange(source.Districts);

			var signature = string.Join(",", districts.ConvertAll(d => d.Id + d.Name));
			if (signature != listSignature)
			{
				listSignature = signature;
				BuildList();
			}

			// The name field follows the selection unless the player is typing.
			if (fieldFor != ctx.SelectedDistrict && !nameField.HasKeyboardFocus)
			{
				fieldFor = ctx.SelectedDistrict;
				nameField.Text = Selected()?.Name ?? "";
			}
		}

		void BuildList()
		{
			list.RemoveChildren();
			foreach (var district in districts)
			{
				var id = district.Id;
				var row = Game.LoadWidget(world, "CITY_DISTRICT_ROW", list, []);
				row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;
				var entry = district;
				row.Get<ColorBlockWidget>("SWATCH").GetColor = () => CityUi.FromArgb(entry.ArgbColor);
				var name = string.IsNullOrEmpty(entry.Name) ? "#" + id : entry.Name;
				row.Get<LabelWidget>("NAME").GetText = () => name;
				row.Get<LabelWidget>("NAME").GetColor = () => ctx.SelectedDistrict == id ? CityUi.Accent : Color.White;
				var population = entry.Population.ToString("N0", CultureInfo.CurrentCulture);
				row.Get<LabelWidget>("POPULATION").GetText = () => population;

				// A transparent button over the row selects it.
				var select = new ButtonWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(0, 0, row.Bounds.Width, row.Bounds.Height),
					Background = "",
					VisualHeight = 0,
					OnClick = () => ctx.SelectedDistrict = id
				};

				row.AddChild(select);
			}

			list.Layout.AdjustChildren();
		}
	}
}
