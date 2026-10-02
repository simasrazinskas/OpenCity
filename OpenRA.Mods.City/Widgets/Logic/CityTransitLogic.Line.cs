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
	/// The window of the selected line (design/iso/ui/panels/transit-line-*.png). Route: name, colour, the stops with their
	/// waiting passengers and the vehicles between them, vehicle count, ticket price, passengers / income / upkeep.
	/// Vehicles: the vehicles of the line with load and state. Passengers: this and last month, load factor, busiest stops.
	/// Every change is the existing TransitSetLine / TransitDeleteLine order.
	/// </summary>
	public partial class CityTransitLogic
	{
		[FluentReference("mode", "id")]
		const string LineTitle = "label-transit-line-title";

		[FluentReference("stops", "cells")]
		const string RouteHeader = "label-transit-route-header";

		[FluentReference("count")]
		const string DepotSlots = "label-transit-depot-slots";

		[FluentReference("count")]
		const string PerMonth = "label-transit-per-month";

		[FluentReference("count")]
		const string WaitingCount = "label-transit-waiting-count";

		[FluentReference("mode", "id")]
		const string VehicleName = "label-transit-vehicle-name";

		[FluentReference("value")]
		const string AverageLoad = "label-transit-average-load";

		[FluentReference("seconds")]
		const string Cycle = "label-transit-line-cycle";

		[FluentReference("id")]
		const string StopName = "label-transit-stop-name";

		[FluentReference]
		const string LineBroken = "label-transit-line-broken";

		[FluentReference]
		const string TicketPrice = "label-transit-ticket";

		const int MaxVehicles = 40;

		enum LinePage { Route, Vehicles, Passengers }

		LineWindowParts lineWindow;

		// The widgets of the line window.
		sealed class LineWindowParts
		{
			public CityPanelWidget Panel;
			public Widget[] Pages;
			public TextFieldWidget Name;
			public StripView Strip;
			public LinePool Passengers;
			public ScrollPanelWidget VehicleList;
			public Widget VehicleEmpty;
			public ColorBlockWidget Chip;
			public LabelWidget ChipText;
		}

		sealed class StripView
		{
			public CityRouteStripWidget Widget;
			public readonly List<RouteStopView> Stops = [];
			public readonly List<RouteVehicleView> Vehicles = [];
		}

		LinePage linePage;
		int selectedStop = -1;
		int selectedVehicle;
		int lineTick = -1;
		int tabsFor = -1;
		string pendingName;
		int pendingUntil;
		string vehicleSignature;
		readonly Dictionary<int, string> stopNames = [];
		string routeHeaderText = "", depotText = "", cycleText = "", averageLoadText = "";

		void InitLineWindow(Widget listWidget)
		{
			var window = (CityPanelWidget)Game.LoadWidget(world, "CITY_TRANSIT_LINE_PANEL", listWidget.Parent, []);
			lineWindow = new LineWindowParts
			{
				Panel = window,
				Pages = [window.Get("PAGE_ROUTE"), window.Get("PAGE_VEHICLES"), window.Get("PAGE_PASSENGERS")]
			};

			window.GetTitle = () => Selected() is { } line
				? FluentProvider.GetMessage(LineTitle, "mode", ModeName(line.Mode.ToLowerInvariant()), "id", line.Id)
				: "";
			window.OnClose = () => detailOpen = false;
			window.IsVisible = () => listShown && detailOpen && Selected() != null && RefreshLine();

			InitRoutePage(window.Get("PAGE_ROUTE"));
			InitVehiclesPage(window.Get("PAGE_VEHICLES"));
			lineWindow.Passengers = new LinePool(world, window.Get("PAGE_PASSENGERS"), 14, 100);
			ShowLinePage(LinePage.Route);
		}

		TransitLine LineData => Layer?.GetLine(selected);

		void ShowLinePage(LinePage target)
		{
			linePage = target;
			for (var i = 0; i < lineWindow.Pages.Length; i++)
				lineWindow.Pages[i].Visible = i == (int)target;

			lineTick = -1;
		}

		void SetLineTabs(string lineMode)
		{
			var vehicleIcon = "tr_" + lineMode;
			CityWindowTab Tab(LinePage target, string icon, string key)
			{
				var text = CityUi.Message(key);
				return new CityWindowTab { Icon = icon, GetTooltip = () => text, IsActive = () => linePage == target, OnClick = () => ShowLinePage(target) };
			}

			lineWindow.Panel.SetTabs(
			[
				Tab(LinePage.Route, "tr_line", "label-transit-tab-route"),
				Tab(LinePage.Vehicles, vehicleIcon, "label-transit-tab-vehicles"),
				Tab(LinePage.Passengers, "ui_chart_line", "label-transit-tab-passengers")
			]);
		}

		/// <summary>Runs every frame while the line window is open; returns true to keep it visible.</summary>
		bool RefreshLine()
		{
			var line = LineData;
			var entry = Selected();
			if (line == null || entry == null)
				return false;

			var lineMode = entry.Value.Mode.ToLowerInvariant();
			var tabsKey = selected * 8 + Array.IndexOf(Modes, lineMode);
			if (tabsKey != tabsFor)
			{
				tabsFor = tabsKey;
				SetLineTabs(lineMode);
				selectedStop = -1;
				selectedVehicle = 0;
				vehicleSignature = null;
				lineWindow.Strip.Widget.VehicleIcon = "tr_" + lineMode;
			}

			var tick = world.WorldTick / 4;
			if (tick != lineTick)
			{
				lineTick = tick;
				switch (linePage)
				{
					case LinePage.Route:
						RefreshRoute(line, entry.Value);
						break;
					case LinePage.Vehicles:
						RefreshVehicles(line);
						break;
					default:
						RefreshPassengers(line, entry.Value);
						break;
				}
			}

			return true;
		}

		string NameOfStop(int id)
		{
			if (stopNames.TryGetValue(id, out var cached))
				return cached;

			var stop = Layer?.GetStop(id);
			string name = null;
			if (stop != null && stop.StationActorId != 0)
			{
				var station = world.GetActorById(stop.StationActorId);
				var tooltip = station?.Info.TraitInfoOrDefault<TooltipInfo>();
				if (tooltip != null && FluentProvider.TryGetMessage(tooltip.Name, out var text))
					name = text;
			}

			name ??= FluentProvider.GetMessage(StopName, "id", id);
			stopNames[id] = name;
			return name;
		}

		int Capacity(TransitMode vehicleMode)
		{
			var info = Layer?.Info;
			if (info == null)
				return 1;

			return vehicleMode switch
			{
				TransitMode.Taxi => info.TaxiCapacity,
				TransitMode.Metro => info.MetroCapacity,
				TransitMode.Tram => info.TramCapacity,
				TransitMode.Train => info.TrainCapacity,
				_ => info.BusCapacity
			};
		}

		/// <summary>Order carrying a new ticket price and / or vehicle count (the other keeps its value).</summary>
		void Apply(int ticketCents, int vehicles)
		{
			if (selected == 0 || world.LocalPlayer == null)
				return;

			world.IssueOrder(UiOrders.SetLine(world.LocalPlayer, selected, ticketCents, Math.Clamp(vehicles, 1, MaxVehicles)));
		}

		void SetLineOrder(int color = -1, string name = null)
		{
			if (selected != 0 && world.LocalPlayer != null)
				world.IssueOrder(TransitOrders.SetLineOrder(world.LocalPlayer, selected, color: color, name: name));
		}

		ButtonWidget AddStepper(Widget page, int x, int y, string icon, int delta)
		{
			var button = (ButtonWidget)Game.LoadWidget(world, "CITY_TRANSIT_STEPPER", page, []);
			button.Bounds.X = x;
			button.Bounds.Y = y;
			button.Get<CityIconWidget>("ICON").Icon = icon;
			button.IsDisabled = () => Selected() == null || world.LocalPlayer == null;
			button.OnClick = () => Apply(Selected()?.TicketCents ?? 100, (Selected()?.VehicleTarget ?? 1) + delta);
			return button;
		}

		void InitVehicleStepper(Widget page, int y)
		{
			page.Get<LabelWidget>("VEHICLES_LABEL").GetText = () => CityUi.Message("label-transit-vehicles");
			page.Get<LabelWidget>("VEHICLES_COUNT").GetText = () => (Selected()?.VehicleTarget ?? 0).ToString(CultureInfo.CurrentCulture);
			AddStepper(page, 58, y, "ui_minus", -1);
			AddStepper(page, 100, y, "ui_plus", 1);
		}

		// ---- route page ----
		void InitRoutePage(Widget page)
		{
			page.Get<LabelWidget>("NAME_LABEL").GetText = () => CityUi.Message("label-transit-name");
			page.Get<LabelWidget>("COLOUR_LABEL").GetText = () => CityUi.Message("label-transit-colour");

			var name = page.Get<TextFieldWidget>("NAME");
			lineWindow.Name = name;
			name.IsDisabled = () => world.LocalPlayer == null;
			void Commit()
			{
				var text = name.Text.Trim();
				var current = Selected() is { } l ? l.Name : "";
				if (text.Length == 0 || text == current)
					return;

				pendingName = text;
				pendingUntil = world.WorldTick + 60;
				SetLineOrder(name: text);
			}

			name.OnEnterKey = _ =>
			{
				Commit();
				name.YieldKeyboardFocus();
				return true;
			};

			name.OnEscKey = _ =>
			{
				pendingName = null;
				name.Text = Selected() is { } l ? DisplayName(l) : "";
				name.YieldKeyboardFocus();
				return true;
			};

			name.OnLoseFocus = Commit;

			lineWindow.Chip = page.Get<ColorBlockWidget>("CHIP");
			lineWindow.ChipText = page.Get<LabelWidget>("CHIP_TEXT");
			lineWindow.Chip.GetColor = () => CityUi.FromArgb(Selected()?.ArgbColor ?? 0);
			lineWindow.ChipText.GetText = () => selected.ToString(CultureInfo.CurrentCulture);
			lineWindow.ChipText.GetColor = () => CityTheme.InkLight;

			BuildSwatches(page.Get("SWATCHES"));

			var header = page.Get<CityHeaderWidget>("ROUTE_HEADER");
			header.GetText = () => routeHeaderText;

			var strip = page.Get<CityRouteStripWidget>("STRIP");
			lineWindow.Strip = new StripView { Widget = strip };
			strip.GetStops = () => lineWindow.Strip.Stops;
			strip.GetVehicles = () => lineWindow.Strip.Vehicles;
			strip.GetLineColor = () => CityUi.FromArgb(Selected()?.ArgbColor ?? 0);
			strip.GetSelected = () => selectedStop;
			strip.GetWaitingText = n => FluentProvider.GetMessage(WaitingCount, "count", n);
			strip.OnStopClick = index =>
			{
				selectedStop = index;
				var line = LineData;
				if (line != null && index >= 0 && index < line.StopIds.Count && Layer?.GetStop(line.StopIds[index]) is { } stop)
					ctx.CenterOn?.Invoke(stop.Cell);
			};

			InitVehicleStepper(page, 143);
			page.Get<LabelWidget>("DEPOT_SLOTS").GetText = () => depotText;
			page.Get<LabelWidget>("CYCLE").GetText = () => cycleText;
			page.Get<LabelWidget>("CYCLE").GetColor = () => CityTheme.Muted("transit");
			page.Get<LabelWidget>("DEPOT_SLOTS").GetColor = () => CityTheme.Muted("transit");

			var max = Layer?.Info.MaxTicketCents ?? 500;
			var ticket = CityRows.AddSlider(world, page.Get("TICKET"), 0, FluentProvider.GetMessage(TicketPrice), Color.White, 0, max, 10,
				() => Selected()?.TicketCents ?? 100,
				value => Apply(value, Selected()?.VehicleTarget ?? 1),
				value => CityUtils.FormatMoney(value / 100) + "." + (value % 100).ToString("D2", CultureInfo.InvariantCulture),
				() => Selected() == null || world.LocalPlayer == null, page.Bounds.Width, 72, 44);

			ticket.Row.Get("BG").Bounds.Width = page.Bounds.Width;
			ticket.Slider.FillColor = CityUi.Accent;

			Stat(
				page, "PASSENGERS", "label-transit-passengers",
				() => FluentProvider.GetMessage(PerMonth, "count", (Selected()?.PassengersThisMonth ?? 0).ToString("N0", CultureInfo.CurrentCulture)),
				() => CityTheme.Ink);
			Stat(page, "INCOME", "label-transit-income", () => CityUi.SignedMoney(Selected()?.RevenueMonth ?? 0), () => CityTheme.MoneyPositive);
			Stat(page, "UPKEEP", "label-transit-upkeep", () => CityUi.SignedMoney(-(Selected()?.CostMonth ?? 0)), () => CityTheme.MoneyNegative);

			var locate = page.Get<ButtonWidget>("LOCATE");
			locate.GetText = () => CityUi.Message("label-transit-locate");
			locate.OnClick = () =>
			{
				var line = LineData;
				if (line != null && line.StopIds.Count > 0 && Layer?.GetStop(line.StopIds[0]) is { } stop)
					ctx.CenterOn?.Invoke(stop.Cell);
			};

			var delete = page.Get<ButtonWidget>("DELETE");
			delete.GetText = () => FluentProvider.GetMessage("button-transit-delete");
			delete.IsDisabled = () => Selected() == null || world.LocalPlayer == null;
			delete.OnClick = DeleteSelected;
		}

		static void Stat(Widget page, string id, string labelKey, Func<string> value, Func<Color> color)
		{
			var label = page.Get<LabelWidget>(id + "_LABEL");
			label.GetText = () => CityUi.Message(labelKey);
			label.GetColor = () => CityTheme.Muted("transit");
			var text = page.Get<LabelWidget>(id);
			text.GetText = value;
			text.GetColor = color;
		}

		static string DisplayName(TransitLineEntry line)
		{
			return string.IsNullOrEmpty(line.Name) ? FluentProvider.GetMessage(LineName, "id", line.Id) : line.Name;
		}

		void BuildSwatches(Widget container)
		{
			for (var i = 0; i < TransitLayer.LineColors.Length; i++)
			{
				var index = i;
				var color = TransitLayer.LineColors[i];
				var frame = new ColorBlockWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(i * 17, 0, 15, 17),
					GetColor = () => CityTheme.Ramp("yellow", 6),
					IsVisible = () => Selected() is { } l && (l.ArgbColor & 0xFFFFFF) == ((color.R << 16) | (color.G << 8) | color.B)
				};

				var swatch = new ColorBlockWidget(Game.ModData) { Bounds = new WidgetBounds(i * 17 + 1, 2, 13, 13), GetColor = () => color };
				var button = new ButtonWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(i * 17, 0, 15, 17),
					Background = "",
					VisualHeight = 0,
					OnClick = () => SetLineOrder(color: index)
				};

				container.AddChild(frame);
				container.AddChild(swatch);
				container.AddChild(button);
			}
		}

		void RefreshRoute(TransitLine line, TransitLineEntry entry)
		{
			// The name field follows the line unless the player is typing (or just changed it).
			var name = lineWindow.Name;
			if (pendingName != null && (DisplayName(entry) == pendingName || world.WorldTick > pendingUntil))
				pendingName = null;

			if (!name.HasKeyboardFocus)
				name.Text = pendingName ?? DisplayName(entry);

			var strip = lineWindow.Strip;
			strip.Stops.Clear();
			var waiting = Layer;
			foreach (var id in line.StopIds)
				strip.Stops.Add(new RouteStopView { Name = NameOfStop(id), Waiting = waiting.GetStop(id)?.WaitingCount ?? 0 });

			strip.Vehicles.Clear();
			var count = line.StopIds.Count;
			foreach (var vehicle in line.Vehicles)
			{
				var position = VehiclePosition(line, vehicle);
				if (position >= 0 && count > 1)
					strip.Vehicles.Add(new RouteVehicleView { Position = Math.Min(position, count - 1) });
			}

			var cells = 0;
			foreach (var leg in line.Legs)
				cells += leg.Cells.Length;

			routeHeaderText = FluentProvider.GetMessage(RouteHeader, "stops", count, "cells", cells);

			var slots = 0;
			var depots = ctx.Get<ITransitUiSourceEx>()?.DepotEntries;
			if (depots != null)
				foreach (var depot in depots)
					if (string.Equals(depot.Mode, entry.Mode, StringComparison.OrdinalIgnoreCase))
						slots += depot.Capacity;

			depotText = slots > 0 ? FluentProvider.GetMessage(DepotSlots, "count", slots) : CityUi.Message("label-transit-no-depot");
			cycleText = line.Broken ? FluentProvider.GetMessage(LineBroken)
				: line.CycleTicks > 0 ? FluentProvider.GetMessage(Cycle, "seconds", line.CycleTicks * world.Timestep / 1000) : "";
		}

		/// <summary>Position of a vehicle along its line in stops (index + fraction), or -1 when it is not on the route.</summary>
		float VehiclePosition(TransitLine line, TransitVehicle vehicle)
		{
			var count = line.StopIds.Count;
			if (count == 0)
				return -1;

			switch (vehicle.State)
			{
				case TransitVehicleState.Leg:
				{
					if (vehicle.LegIndex < 0)
						return -1;

					var progress = vehicle.LegTicks > 0 ? Math.Clamp((world.WorldTick - vehicle.LegStartTick) / (float)vehicle.LegTicks, 0f, 1f) : 0.5f;
					return (vehicle.LegIndex + progress) % count;
				}

				case TransitVehicleState.Dwell:
					return Math.Clamp(vehicle.Pos, 0, count - 1);
				default:
					return -1;
			}
		}

		// ---- vehicles page ----
		void InitVehiclesPage(Widget page)
		{
			InitVehicleStepper(page, 0);

			var average = page.Get<LabelWidget>("AVERAGE_LOAD");
			average.GetText = () => averageLoadText;
			average.GetColor = () => CityTheme.Muted("transit");

			var columnsRow = page.Get("COLUMNS");
			void Header(string id, string key) => columnsRow.Get<LabelWidget>(id).GetText = () => CityUi.Message(key);

			Header("C_NAME", "label-transit-col-vehicle");
			Header("C_NEXT", "label-transit-col-next");
			Header("C_LOAD", "label-transit-col-load");
			Header("C_STATE", "label-transit-col-state");

			lineWindow.VehicleList = page.Get<ScrollPanelWidget>("LIST");
			var listWidth = lineWindow.VehicleList.Bounds.Width - lineWindow.VehicleList.ScrollbarWidth - 2;
			columnsRow.Get("C_LOAD").Bounds.X = listWidth - 56 - 66 - 6;
			columnsRow.Get("C_STATE").Bounds.X = listWidth - 66;
			var empty = page.Get<LabelWidget>("EMPTY");
			empty.GetText = () => CityUi.Message("label-transit-no-vehicles");
			empty.IsVisible = () => lineWindow.VehicleList.Children.Count == 0;
			lineWindow.VehicleEmpty = empty;

			var locate = page.Get<ButtonWidget>("LOCATE");
			locate.GetText = () => CityUi.Message("label-transit-locate");
			locate.IsDisabled = () => SelectedVehicle() == null;
			locate.OnClick = () =>
			{
				if (SelectedVehicle() is { } v)
					ctx.CenterOn?.Invoke(v.Cell);
			};

			var follow = page.Get<ButtonWidget>("FOLLOW");
			follow.GetText = () => CityUi.Message("label-transit-follow");
			follow.IsDisabled = () => SelectedVehicle() == null;
			follow.IsHighlighted = () => ctx.FollowTarget != null && selectedVehicle != 0;
			follow.OnClick = () =>
			{
				var id = selectedVehicle;
				var layer = Layer;
				if (id == 0 || layer == null)
					return;

				ctx.FollowTarget = () =>
				{
					var v = LineData?.Vehicles.FirstOrDefault(x => x.Id == id);
					if (v == null)
						return null;

					return layer.TryGetVehiclePose(v, world.WorldTick, out var pos, out _) ? pos : world.Map.CenterOfCell(v.Cell);
				};
			};
		}

		TransitVehicle SelectedVehicle()
		{
			if (selectedVehicle == 0)
				return null;

			return LineData?.Vehicles.FirstOrDefault(v => v.Id == selectedVehicle);
		}

		TransitVehicle VehicleById(int id)
		{
			var line = LineData;
			if (line != null)
				foreach (var v in line.Vehicles)
					if (v.Id == id)
						return v;

			return null;
		}

		void RefreshVehicles(TransitLine line)
		{
			var load = 0;
			var capacity = 0;
			foreach (var v in line.Vehicles)
			{
				load += v.PaxCount;
				capacity += Capacity(v.Mode);
			}

			averageLoadText = FluentProvider.GetMessage(AverageLoad, "value", capacity > 0 ? load * 100 / capacity : 0);

			var signature = selected + ":" + string.Join(",", line.Vehicles.Select(v => v.Id));
			if (signature == vehicleSignature)
				return;

			vehicleSignature = signature;
			var listPanel = lineWindow.VehicleList;
			listPanel.RemoveChildren();
			var width = listPanel.Bounds.Width - listPanel.ScrollbarWidth - 2;
			foreach (var v in line.Vehicles)
				AddVehicleRow(listPanel, v.Id, width);

			listPanel.Layout.AdjustChildren();
		}

		void AddVehicleRow(ScrollPanelWidget listPanel, int id, int width)
		{
			var row = Game.LoadWidget(world, "CITY_TRANSIT_VEHICLE_ROW", listPanel, []);
			row.Bounds.Width = width;
			var background = row.Get<CityRowBackgroundWidget>("BG");
			background.IsSelected = () => selectedVehicle == id;
			row.Get<ButtonWidget>("SELECT").OnClick = () => selectedVehicle = id;
			Color Ink() => background.Selected ? CityTheme.InkLight : CityTheme.Ink;

			var load = row.Get<LabelWidget>("LOAD");
			load.Bounds.X = width - 56 - 66 - 6;
			load.GetText = () =>
			{
				var v = VehicleById(id);
				return v == null ? "" : v.PaxCount + " / " + Capacity(v.Mode);
			};
			load.GetColor = Ink;

			var state = row.Get<LabelWidget>("STATE");
			state.Bounds.X = width - 66;
			state.GetText = () => StateText(VehicleById(id));
			state.GetColor = () =>
			{
				var v = VehicleById(id);
				if (background.Selected || v == null)
					return Ink();

				var full = v.PaxCount >= Capacity(v.Mode);
				return full ? CityUi.Bad : v.State == TransitVehicleState.Dwell ? CityUi.Warn : v.State == TransitVehicleState.Leg ? CityUi.Good : CityTheme.Ink;
			};

			var name = row.Get<LabelWidget>("NAME");
			var mode = ModeName(VehicleById(id)?.Mode is { } m ? TransitLayer.ModeName(m) : "bus");
			var text = FluentProvider.GetMessage(VehicleName, "mode", mode, "id", id);
			name.GetText = () => text;
			name.GetColor = Ink;

			var next = row.Get<LabelWidget>("NEXT");
			next.Bounds.Width = Math.Max(40, load.Bounds.X - next.Bounds.X - 4);
			next.GetText = CityUi.Fitted(next, () => NextStopText(VehicleById(id)));
			next.GetColor = Ink;
		}

		string NextStopText(TransitVehicle v)
		{
			var line = LineData;
			if (v == null || line == null)
				return "";

			if (v.State == TransitVehicleState.Leg && v.LegIndex >= 0 && v.LegIndex < line.Legs.Count)
				return NameOfStop(line.Legs[v.LegIndex].ToStopId);

			if (v.State == TransitVehicleState.Dwell && v.Pos >= 0 && v.Pos < line.StopIds.Count)
				return NameOfStop(line.StopIds[v.Pos]);

			return "";
		}

		static string StateText(TransitVehicle v)
		{
			if (v == null)
				return "";

			switch (v.State)
			{
				case TransitVehicleState.Leg: return CityUi.Message("label-transit-state-moving");
				case TransitVehicleState.Dwell: return CityUi.Message("label-transit-state-boarding");
				case TransitVehicleState.ToDepot: return CityUi.Message("label-transit-state-returning");
				default: return CityUi.Message("label-transit-state-waiting");
			}
		}

		// ---- passengers page ----
		void RefreshPassengers(TransitLine line, TransitLineEntry entry)
		{
			var l = lineWindow.Passengers;
			l.Clear();
			var thisMonth = line.ThisMonth;
			var lastMonth = line.LastMonth;
			var scale = Math.Max(1, Math.Max(thisMonth.Passengers, lastMonth.Passengers));
			static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

			l.AddHeader(CityUi.Message("label-transit-header-riders"));
			l.AddMeter(CityUi.Message("label-transit-this-month"), Number(thisMonth.Passengers), thisMonth.Passengers * 100 / scale, "yellow");
			l.AddMeter(CityUi.Message("label-transit-last-month"), Number(lastMonth.Passengers), lastMonth.Passengers * 100 / scale, "blue");
			l.AddMeter(CityUi.Message("label-transit-load-factor"), FluentProvider.GetMessage("label-city-percent", "value", entry.Usage), entry.Usage,
				entry.Usage < 70 ? "green" : entry.Usage < 90 ? "yellow" : "red");
			l.AddValue(CityUi.Message("label-transit-waiting-now"), Number(entry.WaitingNow));
			l.AddValue(CityUi.Message("label-transit-gave-up"), Number(thisMonth.GaveUp));

			var money = Math.Max(1, Math.Max(entry.RevenueMonth, entry.CostMonth));
			l.AddHeader(CityUi.Message("label-transit-header-money"));
			l.AddMeter(CityUi.Message("label-transit-income"), CityUi.SignedMoney(entry.RevenueMonth), entry.RevenueMonth * 100 / money, "green");
			l.AddMeter(CityUi.Message("label-transit-upkeep"), CityUi.SignedMoney(-entry.CostMonth), entry.CostMonth * 100 / money, "red");

			// The stops with the most passengers waiting.
			var busiest = line.StopIds
				.Select(id => (Id: id, Waiting: Layer.GetStop(id)?.WaitingCount ?? 0))
				.OrderByDescending(s => s.Waiting).ThenBy(s => s.Id).Take(4).ToList();

			if (busiest.Count > 0)
			{
				l.AddHeader(CityUi.Message("label-transit-header-busiest"));
				var top = Math.Max(1, busiest[0].Waiting);
				foreach (var (id, waiting) in busiest)
					l.AddMeter(NameOfStop(id), Number(waiting), waiting * 100 / top, "orange");
			}

			l.Apply();
		}
	}
}
