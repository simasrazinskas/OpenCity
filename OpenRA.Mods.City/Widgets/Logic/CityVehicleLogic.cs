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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Vehicle inspector (RCT2 style, transit family) for a vehicle picked on the map (VehiclePickerWidget). Two icon tabs: the
	/// overview (vehicle icon by travel mode, purpose, driver button, trip progress, status, origin and destination with locate
	/// buttons) and the trip (origin, current position and destination as a stop list, mode and purpose). The icon buttons on the
	/// right centre the view on the vehicle and toggle the follow camera, which keeps the camera on the vehicle every frame.
	/// </summary>
	public class CityVehicleLogic : ChromeLogic
	{
		[FluentReference]
		const string RowPurpose = "label-vehicle-purpose";

		[FluentReference]
		const string RowMode = "label-vehicle-mode";

		[FluentReference]
		const string RowStatus = "label-vehicle-status";

		[FluentReference]
		const string Crashed = "label-vehicle-crashed";

		[FluentReference]
		const string Driving = "label-vehicle-driving";

		[FluentReference]
		const string TabOverview = "label-vehicle-tab-overview";

		[FluentReference]
		const string TabTrip = "label-vehicle-tab-route";

		[FluentReference]
		const string Now = "label-vehicle-now";

		[FluentReference]
		const string FromShort = "label-vehicle-from-short";

		[FluentReference]
		const string ToShort = "label-vehicle-to-short";

		[FluentReference("name")]
		const string DriverLine = "label-vehicle-driver-line";

		[FluentReference]
		const string LocateVehicle = "button-vehicle-locate";

		[FluentReference]
		const string FollowVehicle = "button-vehicle-follow";

		[FluentReference]
		const string LocatePlace = "button-vehicle-locate-place";

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityPanelWidget window;
		readonly Widget[] pages;
		readonly string family;
		VehicleView view;
		bool valid;
		bool following;
		int tab;

		[ObjectCreator.UseCtor]
		public CityVehicleLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			window = (CityPanelWidget)widget;
			family = window.Family;
			pages = [widget.Get("PAGE_OVERVIEW"), widget.Get("PAGE_TRIP")];

			window.OnClose = Close;
			window.GetTitle = () => valid ? CityUi.Message("label-vehicle-" + view.Image, CityUi.Prettify(view.Image ?? "vehicle")) : "";

			var tabs = new (string Icon, string Key)[] { ("pnl_vehicle", TabOverview), ("tr_line", TabTrip) };
			window.SetTabs(tabs.Select((t, i) =>
			{
				var text = FluentProvider.GetMessage(t.Key);
				return new CityWindowTab
				{
					Icon = t.Icon,
					GetTooltip = () => text,
					IsActive = () => tab == i,
					OnClick = () => ShowTab(i)
				};
			}));

			InitSideButtons(widget);
			InitOverview(pages[0]);
			InitTrip(pages[1]);

			widget.IsVisible = () =>
			{
				Update();
				return valid;
			};
		}

		void ShowTab(int index)
		{
			tab = index;
			for (var i = 0; i < pages.Length; i++)
				pages[i].Visible = i == index;
		}

		void InitSideButtons(Widget widget)
		{
			var locate = widget.Get<ButtonWidget>("LOCATE");
			locate.GetTooltipText = () => FluentProvider.GetMessage(LocateVehicle);
			locate.IsDisabled = () => !valid;
			locate.OnClick = () => ctx.CenterOnWorld?.Invoke(view.Position);

			var follow = widget.Get<ButtonWidget>("FOLLOW");
			follow.GetTooltipText = () => FluentProvider.GetMessage(FollowVehicle);
			follow.IsHighlighted = () => following;
			follow.IsDisabled = () => !valid;
			follow.OnClick = () =>
			{
				following = !following;
				var trip = ctx.SelectedVehicle;
				ctx.FollowTarget = following ? () => ctx.Get<IVehicleInspector>() is { } i && i.TryGetVehicle(trip, out var v) ? v.Position : null : null;
			};
		}

		void InitOverview(Widget page)
		{
			var icon = page.Get("VIEW").Get<CityIconWidget>("VEHICLE");
			icon.GetIcon = () => valid ? ModeIcon(view.Mode) : null;

			var purpose = page.Get<LabelWidget>("PURPOSE");
			purpose.GetText = CityUi.Fitted(purpose, PurposeText);
			purpose.GetColor = () => CityTheme.FamilyShade(family, 1);

			var chip = page.Get("MODE_CHIP");
			chip.Get<CityIconWidget>("ICON").GetIcon = () => valid ? ModeIcon(view.Mode) : null;
			var mode = chip.Get<LabelWidget>("NAME");
			mode.GetText = CityUi.Fitted(mode, ModeText);
			mode.GetColor = () => CityTheme.InkLight;

			var driver = page.Get<ButtonWidget>("DRIVER");
			driver.GetText = () => FluentProvider.GetMessage(DriverLine, "name", DriverName());
			driver.IsDisabled = () => !valid || view.OwnerId <= 0 || ctx.Citizens == null || !ctx.Citizens.TryGetCitizen(view.OwnerId, out _);
			driver.OnClick = () => ctx.SelectedCitizen = view.OwnerId;

			var bar = page.Get<CityBarWidget>("PROGRESS");
			bar.GetPercentage = () => valid ? view.ProgressPercent : 0;

			var percent = page.Get<LabelWidget>("PERCENT");
			percent.GetText = () => valid ? FluentProvider.GetMessage("label-city-percent", "value", view.ProgressPercent) : "";

			var status = page.Get<LabelWidget>("STATUS");
			status.GetText = StatusText;
			status.GetColor = StatusColor;

			InitPlaceRow(page.Get("FROM_ROW"), () => view.OriginProperty);
			InitPlaceRow(page.Get("TO_ROW"), () => view.DestinationProperty);
		}

		void InitPlaceRow(Widget row, Func<int> property)
		{
			var label = row.Get<LabelWidget>("LABEL");
			label.GetColor = () => CityTheme.FamilyShade(family, 2);

			var name = row.Get<LabelWidget>("NAME");
			name.GetText = CityUi.Fitted(name, () => valid ? PropertyName(property()) : "");
			name.GetColor = () => CityTheme.Ink;

			InitLocate(row.Get<ButtonWidget>("LOCATE"), property);
		}

		void InitLocate(ButtonWidget button, Func<int> property)
		{
			button.GetTooltipText = () => FluentProvider.GetMessage(LocatePlace);
			button.IsDisabled = () => !valid || property() == 0;
			button.OnClick = () => Locate(property());
		}

		void InitTrip(Widget page)
		{
			var stops = page.Get("STOPS");
			var now = stops.Get<CityRowBackgroundWidget>("BG_NOW");
			now.IsSelected = () => true;

			stops.Get<ColorBlockWidget>("LINE").GetColor = () => CityTheme.FamilyShade(family, 2);

			void Stop(string id, Func<string> text, Func<Color> color)
			{
				var label = stops.Get<LabelWidget>(id);
				label.GetText = CityUi.Fitted(label, text);
				label.GetColor = color;
			}

			Stop("FROM_NAME", () => valid ? FluentProvider.GetMessage(FromShort) + " " + PropertyName(view.OriginProperty) : "", () => CityTheme.Ink);
			Stop("NOW_NAME", () => FluentProvider.GetMessage(Now), () => CityTheme.InkLight);
			Stop("TO_NAME", () => valid ? FluentProvider.GetMessage(ToShort) + " " + PropertyName(view.DestinationProperty) : "", () => CityTheme.Ink);

			var percent = stops.Get<LabelWidget>("NOW_PERCENT");
			percent.GetText = () => valid ? FluentProvider.GetMessage("label-city-percent", "value", view.ProgressPercent) : "";
			percent.GetColor = () => CityTheme.InkLight;

			InitLocate(stops.Get<ButtonWidget>("FROM_LOCATE"), () => view.OriginProperty);
			InitLocate(stops.Get<ButtonWidget>("TO_LOCATE"), () => view.DestinationProperty);

			var rows = page.Get("ROWS");
			AddRow(rows, 0, RowMode, ModeText, () => CityTheme.Ink);
			AddRow(rows, 1, RowPurpose, PurposeText, () => CityTheme.Ink);
			AddRow(rows, 2, RowStatus, StatusText, StatusColor);
		}

		void AddRow(Widget rows, int index, string key, Func<string> value, Func<Color> color)
		{
			var row = Game.LoadWidget(world, "CITY_INFO_ROW", rows, []);
			row.Bounds.Y = index * 13;
			var name = FluentProvider.GetMessage(key);
			var nameLabel = row.Get<LabelWidget>("NAME");
			nameLabel.GetText = () => name;
			nameLabel.GetColor = () => CityTheme.FamilyShade(family, 2);
			var label = row.Get<LabelWidget>("VALUE");
			label.GetText = value;
			label.GetColor = color;
		}

		string PurposeText()
		{
			return valid ? CityUi.Message("label-purpose-" + view.Purpose.ToString().ToLowerInvariant(), view.Purpose.ToString()) : "";
		}

		string ModeText()
		{
			return valid ? CityUi.Message("label-travelmode-" + view.Mode.ToString().ToLowerInvariant(), view.Mode.ToString()) : "";
		}

		string StatusText()
		{
			return valid ? FluentProvider.GetMessage(view.Crashed ? Crashed : Driving) : "";
		}

		Color StatusColor()
		{
			return valid && view.Crashed ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;
		}

		static string ModeIcon(TravelMode mode)
		{
			return mode switch
			{
				TravelMode.Walk => "tr_pedestrian",
				TravelMode.Transit => "tr_bus",
				TravelMode.Bus => "tr_bus",
				TravelMode.Taxi => "tr_taxi",
				TravelMode.Bike => "tr_bicycle",
				TravelMode.Truck => "tr_truck",
				TravelMode.EmergencyVehicle => "st_ambulance",
				_ => "tr_car"
			};
		}

		string PropertyName(int propertyId)
		{
			return propertyId == 0 ? "-" : CityUi.PropertyName(ctx.Properties?.Get(propertyId));
		}

		string DriverName()
		{
			if (valid && view.OwnerId > 0 && ctx.Citizens != null && ctx.Citizens.TryGetCitizen(view.OwnerId, out var citizen))
				return citizen.Name;

			return "-";
		}

		void Locate(int propertyId)
		{
			var property = ctx.Properties?.Get(propertyId);
			if (property != null)
				ctx.CenterOn?.Invoke(property.Origin);
		}

		void Close()
		{
			ctx.SelectedVehicle = 0;
			StopFollowing();
		}

		void StopFollowing()
		{
			if (following)
				ctx.FollowTarget = null;

			following = false;
		}

		void Update()
		{
			valid = false;
			var inspector = ctx.Get<IVehicleInspector>();
			if (ctx.SelectedVehicle == 0 || inspector == null)
			{
				StopFollowing();
				return;
			}

			// The vehicle arrived (or was removed): the window goes away with it.
			if (!inspector.TryGetVehicle(ctx.SelectedVehicle, out view))
			{
				ctx.SelectedVehicle = 0;
				StopFollowing();
				return;
			}

			valid = true;
		}
	}
}
