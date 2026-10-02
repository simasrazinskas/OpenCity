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
	/// The RCT2 transit lines window (design/iso/ui/panels/transit-lines-*.png): an icon tab per mode (bus, tram, metro,
	/// train, taxi) and the lines of that mode with colour chip, name, stops, vehicles, load meter and monthly profit
	/// (ITransitUiSource.Lines); the taxi tab lists the stands. The footer shows the totals of the mode. Selecting a line
	/// opens its own window (CityTransitLogic.Line.cs: route, vehicles, passengers, ticket price, vehicle count, delete).
	/// </summary>
	public partial class CityTransitLogic : ChromeLogic
	{
		[FluentReference("mode")]
		const string WindowTitle = "label-transit-window-title";

		[FluentReference("mode")]
		const string EmptyMode = "label-transit-empty-mode";

		[FluentReference]
		const string EmptyTaxi = "label-transit-empty-taxi";

		[FluentReference("x", "y")]
		const string StandName = "label-transit-stand-name";

		[FluentReference("amount")]
		const string Profit = "label-transit-profit";

		[FluentReference("count")]
		const string FooterLines = "label-transit-footer-lines";

		[FluentReference("count")]
		const string FooterStands = "label-transit-footer-stands";

		[FluentReference("count")]
		const string FooterVehicles = "label-transit-footer-vehicles";

		[FluentReference("count")]
		const string FooterWaiting = "label-transit-footer-waiting";

		[FluentReference("count")]
		const string FooterPassengers = "label-transit-footer-passengers";

		[FluentReference("count")]
		const string FooterRides = "label-transit-footer-rides";

		[FluentReference("id")]
		const string LineName = "label-transit-line-name";

		static readonly string[] Modes = ["bus", "tram", "metro", "train", "taxi"];
		static readonly string[] ModeIcons = ["tr_bus", "tr_tram", "tr_metro", "tr_train", "tr_taxi"];

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityPanelWidget panel;
		readonly ScrollPanelWidget list;
		readonly Widget columns;
		readonly Widget footer;
		readonly ButtonWidget edit;
		readonly ButtonWidget delete;

		string mode = "bus";
		int selected;
		bool detailOpen;
		bool listShown;
		bool wasShown;
		readonly List<int> rowIds = [];
		string builtSignature;
		int footerTick = -1;
		string linesText = "", vehiclesText = "", passengersText = "", incomeText = "", upkeepText = "";
		Color incomeColor, upkeepColor;

		TransitLayer Layer => ctx.Get<TransitLayer>();

		bool Taxi => mode == "taxi";

		[ObjectCreator.UseCtor]
		public CityTransitLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = (CityPanelWidget)widget;
			list = widget.Get<ScrollPanelWidget>("LIST");
			columns = widget.Get("COLUMNS");
			footer = widget.Get("FOOTER");

			panel.GetTitle = () => FluentProvider.GetMessage(WindowTitle, "mode", ModeName(mode));
			panel.OnClose = () => panel.Visible = false;

			panel.SetTabs(Modes.Select((m, i) =>
			{
				var text = ModeName(m);
				return new CityWindowTab
				{
					Icon = ModeIcons[i],
					GetTooltip = () => text,
					IsActive = () => mode == m,
					IsDisabled = () => ctx.Get<TransitLayer>() == null,
					OnClick = () => SetMode(m)
				};
			}));

			var empty = widget.Get<LabelWidget>("EMPTY");
			empty.GetText = () => Taxi ? FluentProvider.GetMessage(EmptyTaxi) : FluentProvider.GetMessage(EmptyMode, "mode", ModeName(mode).ToLowerInvariant());
			empty.IsVisible = () => rowIds.Count == 0;

			InitFooter(widget);
			edit = widget.Get<ButtonWidget>("EDIT");
			edit.GetText = () => CityUi.Message("label-transit-edit");
			edit.IsDisabled = () => Taxi || Selected() == null;
			edit.IsHighlighted = () => detailOpen && Selected() != null;
			edit.OnClick = () => detailOpen = !detailOpen;
			delete = widget.Get<ButtonWidget>("DELETE");
			delete.GetText = () => CityUi.Message("label-transit-delete");
			delete.IsDisabled = () => Taxi || Selected() == null || world.LocalPlayer == null;
			delete.OnClick = DeleteSelected;

			var showOnMap = widget.Get<CheckboxWidget>("SHOW_ON_MAP");
			showOnMap.GetText = () => CityUi.Message("label-transit-show-on-map");
			showOnMap.IsChecked = () => Layer?.ShowAllLines ?? false;
			showOnMap.OnClick = () =>
			{
				if (Layer != null)
					Layer.ShowAllLines = !Layer.ShowAllLines;
			};

			InitColumns();
			InitLineWindow(widget);

			var panelVisible = panel.IsVisible;
			panel.IsVisible = () =>
			{
				listShown = panelVisible();
				if (listShown)
				{
					if (!wasShown)
						ChooseMode();

					Refresh();
				}
				else if (wasShown && Layer != null)
					Layer.HighlightLineId = 0;

				wasShown = listShown;
				return listShown;
			};
		}

		static string ModeName(string m)
		{
			return CityUi.Message("label-transit-mode-" + m, m);
		}

		/// <summary>The lines of a mode in id order.</summary>
		List<TransitLineEntry> LinesOf(string m)
		{
			var result = new List<TransitLineEntry>();
			var lines = ctx.TransitUi?.Lines;
			if (lines != null)
				foreach (var line in lines)
					if (string.Equals(line.Mode, m, StringComparison.OrdinalIgnoreCase))
						result.Add(line);

			return result;
		}

		List<TransitStop> TaxiStands()
		{
			var result = new List<TransitStop>();
			var layer = Layer;
			if (layer != null)
				foreach (var stop in layer.Stops)
					if (stop.Mode == TransitMode.Taxi)
						result.Add(stop);

			return result;
		}

		void RefreshRowIds()
		{
			rowIds.Clear();
			if (Taxi)
				rowIds.AddRange(TaxiStands().Select(s => s.Id));
			else
				rowIds.AddRange(LinesOf(mode).Select(l => l.Id));
		}

		TransitLineEntry? Selected()
		{
			var lines = ctx.TransitUi?.Lines;
			if (lines == null)
				return null;

			foreach (var line in lines)
				if (line.Id == selected)
					return line;

			return null;
		}

		TransitLineEntry Find(int id)
		{
			var lines = ctx.TransitUi?.Lines;
			if (lines != null)
				foreach (var line in lines)
					if (line.Id == id)
						return line;

			return default;
		}

		void SetMode(string m)
		{
			mode = m;
			builtSignature = null;
			footerTick = -1;
		}

		/// <summary>On opening: the mode of the selected line, else the first mode that has lines.</summary>
		void ChooseMode()
		{
			var current = Selected();
			if (current != null)
			{
				SetMode(current.Value.Mode.ToLowerInvariant());
				return;
			}

			if (LinesOf(mode).Count > 0 || (Taxi && TaxiStands().Count > 0))
				return;

			foreach (var m in Modes)
			{
				if (m != "taxi" && LinesOf(m).Count > 0)
				{
					SetMode(m);
					return;
				}
			}
		}

		void DeleteSelected()
		{
			if (selected != 0 && world.LocalPlayer != null)
				world.IssueOrder(UiOrders.DeleteLine(world.LocalPlayer, selected));

			selected = 0;
			detailOpen = false;
		}

		// ---- columns of the list ----
		int RowWidth => list.Bounds.Width - list.ScrollbarWidth - 2;

		/// <summary>X and width of the columns for a row width: chip, name, stops, vehicles, usage, profit.</summary>
		static (int NameX, int NameW, int StopsX, int VehiclesX, int UsageX, int ProfitX) Columns(int width)
		{
			var profitX = width - 62 - 3;
			var usageX = profitX - 46 - 6;
			var vehiclesX = usageX - 30 - 4;
			var stopsX = vehiclesX - 34 - 2;
			return (34, Math.Max(40, stopsX - 38), stopsX, vehiclesX, usageX, profitX);
		}

		void InitColumns()
		{
			void Header(string id, Func<string> text, Func<bool> visible = null)
			{
				var label = columns.Get<LabelWidget>(id);
				label.GetText = text;
				if (visible != null)
					label.IsVisible = visible;
			}

			Header("C_LINE", () => Taxi ? "" : CityUi.Message("label-transit-col-line"));
			Header("C_NAME", () => CityUi.Message(Taxi ? "label-transit-col-stand" : "label-transit-col-name"));
			Header("C_STOPS", () => CityUi.Message(Taxi ? "label-transit-col-waiting" : "label-transit-col-stops"));
			Header("C_VEHICLES", () => CityUi.Message(Taxi ? "label-transit-col-rides" : "label-transit-col-vehicles"));
			Header("C_USAGE", () => CityUi.Message("label-transit-col-usage"), () => !Taxi);
			Header("C_PROFIT", () => CityUi.Message("label-transit-col-profit"), () => !Taxi);
		}

		void PlaceColumns()
		{
			var (nameX, nameW, stopsX, vehiclesX, usageX, profitX) = Columns(RowWidth);
			Place(columns.Get("C_LINE"), 2, 30);
			Place(columns.Get("C_NAME"), Taxi ? 3 : nameX, nameW);
			Place(columns.Get("C_STOPS"), stopsX, 34 + (Taxi ? 20 : 0));
			Place(columns.Get("C_VEHICLES"), vehiclesX + (Taxi ? 20 : 0), 30 + (Taxi ? 10 : 0));
			Place(columns.Get("C_USAGE"), usageX + 3, 46);
			Place(columns.Get("C_PROFIT"), profitX, 62);
		}

		static void Place(Widget widget, int x, int width)
		{
			widget.Bounds.X = x;
			widget.Bounds.Width = width;
		}

		// ---- rows ----
		void Refresh()
		{
			if (Layer != null)
				Layer.HighlightLineId = detailOpen ? selected : 0;

			RefreshRowIds();
			PlaceColumns();
			if (selected != 0 && Selected() == null)
			{
				selected = 0;
				detailOpen = false;
			}

			var rows = rowIds;
			var signature = mode + ":" + string.Join(",", rows) + ":" + string.Join(",", Taxi ? [] : LinesOf(mode).Select(l => l.Name));
			if (signature != builtSignature)
			{
				builtSignature = signature;
				list.RemoveChildren();
				foreach (var id in rows)
				{
					if (Taxi)
						AddStandRow(id);
					else
						AddLineRow(id);
				}

				list.Layout.AdjustChildren();
			}

			RefreshFooter();
		}

		Widget NewRow(Action click, out CityRowBackgroundWidget background)
		{
			var row = Game.LoadWidget(world, "CITY_TRANSIT_LINE_ROW", list, []);
			row.Bounds.Width = RowWidth;
			background = row.Get<CityRowBackgroundWidget>("BG");
			row.Get<ButtonWidget>("SELECT").OnClick = click;
			return row;
		}

		static void Text(Widget row, string id, Func<string> text, Func<Color> color, int x, int width, bool fit = false)
		{
			var label = row.Get<LabelWidget>(id);
			label.Bounds.X = x;
			label.Bounds.Width = width;
			label.GetText = fit ? CityUi.Fitted(label, text) : text;
			label.GetColor = color;
		}

		void AddLineRow(int id)
		{
			var row = NewRow(() =>
			{
				selected = id;
				detailOpen = true;
			}, out var background);

			var (nameX, nameW, stopsX, vehiclesX, usageX, profitX) = Columns(row.Bounds.Width);
			Color Ink() => background.Selected ? CityTheme.InkLight : CityTheme.Ink;

			var swatch = row.Get<ColorBlockWidget>("SWATCH");
			swatch.GetColor = () => CityUi.FromArgb(Find(id).ArgbColor);
			var number = row.Get<LabelWidget>("NUMBER");
			number.GetText = () => id.ToString(System.Globalization.CultureInfo.CurrentCulture);
			number.GetColor = () => CityTheme.InkLight;

			var fallback = FluentProvider.GetMessage(LineName, "id", id);
			Text(row, "NAME", () => string.IsNullOrEmpty(Find(id).Name) ? fallback : Find(id).Name, Ink, nameX, nameW, true);
			Text(row, "STOPS", () => Find(id).Stops.ToString(System.Globalization.CultureInfo.CurrentCulture), Ink, stopsX, 34);
			Text(row, "VEHICLES", () => Find(id).Vehicles.ToString(System.Globalization.CultureInfo.CurrentCulture), Ink, vehiclesX, 30);

			var usage = row.Get<CityBarWidget>("USAGE");
			usage.Bounds.X = usageX;
			usage.GetPercentage = () => Find(id).Usage;
			usage.GetRamp = () => Find(id).Usage < 70 ? "green" : Find(id).Usage < 90 ? "yellow" : "red";

			var profit = row.Get<LabelWidget>("PROFIT");
			profit.Bounds.X = profitX;
			profit.GetText = () => FluentProvider.GetMessage(Profit, "amount", CityUi.SignedMoney(Find(id).RevenueMonth - Find(id).CostMonth));
			profit.GetColor = () =>
			{
				var gain = Find(id).RevenueMonth >= Find(id).CostMonth;
				return background.Selected
					? (gain ? CityTheme.MoneyPositiveLight : CityTheme.MoneyNegativeLight)
					: (gain ? CityTheme.MoneyPositive : CityTheme.MoneyNegative);
			};

			background.IsSelected = () => selected == id;
		}

		void AddStandRow(int id)
		{
			var stop = Layer?.GetStop(id);
			var cell = stop?.Cell ?? CPos.Zero;
			var row = NewRow(() => ctx.CenterOn?.Invoke(cell), out var background);
			var (nameX, nameW, stopsX, vehiclesX, usageX, profitX) = Columns(row.Bounds.Width);
			Color Ink() => background.Selected ? CityTheme.InkLight : CityTheme.Ink;
			row.Get("SWATCH").Visible = false;
			row.Get("NUMBER").Visible = false;
			row.Get("USAGE").Visible = false;
			row.Get("PROFIT").Visible = false;

			var name = FluentProvider.GetMessage(StandName, "x", cell.X, "y", cell.Y);
			Text(row, "NAME", () => name, Ink, 3, nameW + 28, true);
			Text(row, "STOPS", () => (Layer?.GetStop(id)?.WaitingCount ?? 0).ToString(System.Globalization.CultureInfo.CurrentCulture),
				Ink, stopsX, 54);
			Text(row, "VEHICLES", () => (Layer?.GetStop(id)?.BoardedThisMonth ?? 0).ToString(System.Globalization.CultureInfo.CurrentCulture),
				Ink, vehiclesX + 20, 40);
		}

		// ---- footer: totals of the mode ----
		void InitFooter(Widget widget)
		{
			var lines = footer.Get<LabelWidget>("LINES");
			lines.GetText = () => linesText;
			var vehicles = footer.Get<LabelWidget>("VEHICLES");
			vehicles.GetText = () => vehiclesText;
			var passengers = footer.Get<LabelWidget>("PASSENGERS");
			passengers.GetText = () => passengersText;

			footer.Get<CityIconWidget>("LINES_ICON").GetIcon = () => Taxi ? "tr_taxi_stand" : "tr_line";
			footer.Get<CityIconWidget>("VEHICLES_ICON").GetIcon = () => Taxi ? "stat_citizen" : "stat_vehicles";

			void Money(string labelId, string valueId, string key, Func<string> text, Func<Color> color)
			{
				widget.Get<LabelWidget>(labelId).GetText = () => CityUi.Message(key);
				var value = widget.Get<LabelWidget>(valueId);
				value.GetText = text;
				value.GetColor = color;
			}

			Money("INCOME_LABEL", "INCOME", "label-transit-income", () => incomeText, () => incomeColor);
			Money("UPKEEP_LABEL", "UPKEEP", "label-transit-upkeep", () => upkeepText, () => upkeepColor);
			widget.Get("INCOME_LABEL").IsVisible = () => !Taxi;
			widget.Get("INCOME").IsVisible = () => !Taxi;
			widget.Get("UPKEEP_LABEL").IsVisible = () => !Taxi;
			widget.Get("UPKEEP").IsVisible = () => !Taxi;
		}

		void RefreshFooter()
		{
			var tick = world.WorldTick / 8;
			if (tick == footerTick)
				return;

			footerTick = tick;
			if (Taxi)
			{
				var stands = TaxiStands();
				linesText = FluentProvider.GetMessage(FooterStands, "count", stands.Count);
				vehiclesText = FluentProvider.GetMessage(FooterWaiting, "count", stands.Sum(s => s.WaitingCount));
				passengersText = FluentProvider.GetMessage(
					FooterRides, "count", stands.Sum(s => s.BoardedThisMonth).ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
				return;
			}

			var lines = LinesOf(mode);
			linesText = FluentProvider.GetMessage(FooterLines, "count", lines.Count);
			vehiclesText = FluentProvider.GetMessage(FooterVehicles, "count", lines.Sum(l => l.Vehicles));
			passengersText = FluentProvider.GetMessage(
				FooterPassengers, "count", lines.Sum(l => l.PassengersThisMonth).ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
			var income = lines.Sum(l => l.RevenueMonth);
			var upkeep = lines.Sum(l => l.CostMonth);
			incomeText = CityUi.SignedMoney(income);
			incomeColor = CityTheme.MoneyPositive;
			upkeepText = CityUtils.FormatMoney(-upkeep);
			upkeepColor = CityTheme.MoneyNegative;
		}
	}
}
