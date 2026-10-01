#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Vehicle card for a vehicle picked on the map (VehiclePickerWidget): type, purpose, route progress, origin and destination
	/// buttons, the driver (opens the citizen panel) and a Follow toggle that keeps the camera on the vehicle every frame.
	/// </summary>
	public class CityVehicleLogic : ChromeLogic
	{
		[FluentReference]
		const string RowPurpose = "label-vehicle-purpose";

		[FluentReference]
		const string RowMode = "label-vehicle-mode";

		[FluentReference]
		const string RowFrom = "label-vehicle-from";

		[FluentReference]
		const string RowTo = "label-vehicle-to";

		[FluentReference]
		const string RowDriver = "label-vehicle-driver";

		[FluentReference]
		const string RowStatus = "label-vehicle-status";

		[FluentReference]
		const string Crashed = "label-vehicle-crashed";

		[FluentReference]
		const string Driving = "label-vehicle-driving";

		readonly World world;
		readonly CityUiContext ctx;
		readonly LabelWidget title;
		readonly ButtonWidget followButton;
		VehicleView view;
		bool valid;
		bool following;

		[ObjectCreator.UseCtor]
		public CityVehicleLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = Close;
			title = widget.Get<LabelWidget>("TITLE");
			title.GetText = () => valid ? CityUi.Message("label-vehicle-" + view.Image, CityUi.Prettify(view.Image ?? "vehicle")) : "";

			var rows = widget.Get("ROWS");
			AddRow(rows, 0, RowPurpose, () => CityUi.Message("label-purpose-" + view.Purpose.ToString().ToLowerInvariant(), view.Purpose.ToString()), () => Color.White);
			AddRow(rows, 1, RowMode, () => CityUi.Message("label-travelmode-" + view.Mode.ToString().ToLowerInvariant(), view.Mode.ToString()), () => Color.White);
			AddRow(rows, 2, RowStatus, () => FluentProvider.GetMessage(view.Crashed ? Crashed : Driving), () => view.Crashed ? CityUi.Bad : CityUi.Good);

			var bar = widget.Get<CityBarWidget>("PROGRESS");
			bar.GetPercentage = () => view.ProgressPercent;

			var from = widget.Get<ButtonWidget>("FROM");
			from.GetText = () => FluentProvider.GetMessage(RowFrom) + ": " + PropertyName(view.OriginProperty);
			from.IsDisabled = () => !valid || view.OriginProperty == 0;
			from.OnClick = () => Locate(view.OriginProperty);

			var to = widget.Get<ButtonWidget>("TO");
			to.GetText = () => FluentProvider.GetMessage(RowTo) + ": " + PropertyName(view.DestinationProperty);
			to.IsDisabled = () => !valid || view.DestinationProperty == 0;
			to.OnClick = () => Locate(view.DestinationProperty);

			var driver = widget.Get<ButtonWidget>("DRIVER");
			driver.GetText = () => FluentProvider.GetMessage(RowDriver) + ": " + DriverName();
			driver.IsDisabled = () => !valid || view.OwnerId <= 0 || ctx.Citizens == null || !ctx.Citizens.TryGetCitizen(view.OwnerId, out _);
			driver.OnClick = () => ctx.SelectedCitizen = view.OwnerId;

			followButton = widget.Get<ButtonWidget>("FOLLOW");
			followButton.IsHighlighted = () => following;
			followButton.IsDisabled = () => !valid;
			followButton.OnClick = () =>
			{
				following = !following;
				var trip = ctx.SelectedVehicle;
				ctx.FollowTarget = following ? () => ctx.Get<IVehicleInspector>() is { } i && i.TryGetVehicle(trip, out var v) ? v.Position : null : null;
			};

			widget.IsVisible = () =>
			{
				Update();
				return valid;
			};
		}

		void AddRow(Widget rows, int index, string key, System.Func<string> value, System.Func<Color> color)
		{
			var row = Game.LoadWidget(world, "CITY_INFO_ROW", rows, []);
			row.Bounds.Y = index * 22;
			row.Bounds.Width = 236;
			var name = FluentProvider.GetMessage(key);
			row.Get<LabelWidget>("NAME").GetText = () => name;
			var label = row.Get<LabelWidget>("VALUE");
			label.Bounds.Width = 126;
			label.GetText = value;
			label.GetColor = color;
		}

		string PropertyName(int propertyId)
		{
			return propertyId == 0 ? "-" : CityUi.PropertyName(ctx.Properties?.Get(propertyId));
		}

		string DriverName()
		{
			if (view.OwnerId > 0 && ctx.Citizens != null && ctx.Citizens.TryGetCitizen(view.OwnerId, out var citizen))
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

			// The vehicle arrived (or was removed): the card goes away with it.
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
