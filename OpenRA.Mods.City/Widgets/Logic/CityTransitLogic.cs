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

using System.Globalization;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Transit overview: every line with colour, mode, stops, vehicles, usage bar and monthly profit (ITransitUiSource.Lines).
	/// The selected line has a ticket price slider, a vehicle count slider and a delete button (TransitSetLine / TransitDeleteLine).
	/// </summary>
	public class CityTransitLogic : ChromeLogic
	{
		[FluentReference("stops", "vehicles", "passengers")]
		const string LineInfo = "label-transit-line-info";

		[FluentReference("amount")]
		const string Profit = "label-transit-profit";

		[FluentReference]
		const string TicketPrice = "label-transit-ticket";

		[FluentReference]
		const string Vehicles = "label-transit-vehicles";

		[FluentReference]
		const string DeleteLine = "button-transit-delete";

		const int MaxVehicles = 40;

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		readonly Widget settings;

		int selected;
		string builtSignature;

		[ObjectCreator.UseCtor]
		public CityTransitLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			list = widget.Get<ScrollPanelWidget>("LIST");
			settings = widget.Get("SETTINGS");
			widget.Get<LabelWidget>("EMPTY").IsVisible = () => ctx.TransitUi == null || ctx.TransitUi.Lines.Count == 0;

			BuildSettings();

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
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

		void BuildSettings()
		{
			settings.IsVisible = () => Selected() != null;

			var ticket = CityRows.AddSlider(world, settings, 0, FluentProvider.GetMessage(TicketPrice), Color.White, 0, 500, 10,
				() => Selected()?.TicketCents ?? 100,
				value => Apply(value, Selected()?.VehicleTarget ?? 1),
				value => CityUtils.FormatMoney(value / 100) + "." + (value % 100).ToString("D2", CultureInfo.InvariantCulture),
				() => Selected() == null || world.LocalPlayer == null);

			ticket.Slider.FillColor = CityUi.Accent;

			var vehicles = CityRows.AddSlider(world, settings, 36, FluentProvider.GetMessage(Vehicles), Color.White, 1, MaxVehicles, 1,
				() => Selected()?.VehicleTarget ?? 1,
				value => Apply(Selected()?.TicketCents ?? 100, value),
				value => value.ToString(CultureInfo.CurrentCulture), () => Selected() == null || world.LocalPlayer == null);

			vehicles.Slider.FillColor = CityUi.Accent;

			var delete = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", settings, []) as ButtonWidget;
			delete.Bounds.Y = 80;
			delete.Bounds.Width = 160;
			delete.Bounds.Height = 30;
			var text = FluentProvider.GetMessage(DeleteLine);
			delete.GetText = () => text;
			delete.IsDisabled = () => Selected() == null || world.LocalPlayer == null;
			delete.OnClick = () =>
			{
				if (selected != 0 && world.LocalPlayer != null)
					world.IssueOrder(UiOrders.DeleteLine(world.LocalPlayer, selected));

				selected = 0;
			};
		}

		void Apply(int ticketCents, int vehicles)
		{
			if (selected == 0 || world.LocalPlayer == null)
				return;

			world.IssueOrder(UiOrders.SetLine(world.LocalPlayer, selected, ticketCents, vehicles));
		}

		void Refresh()
		{
			var lines = ctx.TransitUi?.Lines;
			if (lines == null)
				return;

			var signature = string.Join(",", System.Linq.Enumerable.Select(lines, l => l.Id + l.Name));
			if (signature == builtSignature)
				return;

			builtSignature = signature;
			list.RemoveChildren();
			foreach (var line in lines)
				AddRow(line.Id);

			list.Layout.AdjustChildren();
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

		void AddRow(int id)
		{
			var row = Game.LoadWidget(world, "CITY_LINE_ROW", list, []);
			row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;

			var entry = Find(id);
			row.Get<ColorBlockWidget>("SWATCH").GetColor = () => CityUi.FromArgb(Find(id).ArgbColor);
			var name = string.IsNullOrEmpty(entry.Name) ? "#" + id : entry.Name;
			var nameLabel = row.Get<LabelWidget>("NAME");
			nameLabel.GetText = () => name;
			nameLabel.GetColor = () => selected == id ? CityUi.Accent : Color.White;

			row.Get<LabelWidget>("INFO").GetText = () =>
			{
				var line = Find(id);
				return CityUi.Message("label-transit-mode-" + (line.Mode ?? "bus").ToLowerInvariant(), line.Mode) + "  " +
					FluentProvider.GetMessage(LineInfo, "stops", line.Stops, "vehicles", line.Vehicles, "passengers", line.PassengersThisMonth);
			};

			var usage = row.Get<CityBarWidget>("USAGE");
			usage.GetPercentage = () => Find(id).Usage;
			usage.GetBarColor = () => CityUi.PercentColor(100 - Find(id).Usage);

			var profit = row.Get<LabelWidget>("PROFIT");
			profit.Bounds.X = row.Bounds.Width - 126;
			row.Get<CityBarWidget>("USAGE").Bounds.X = row.Bounds.Width - 240;
			profit.GetText = () => FluentProvider.GetMessage(Profit, "amount", CityUi.SignedMoney(Find(id).RevenueMonth - Find(id).CostMonth));
			profit.GetColor = () => Find(id).RevenueMonth >= Find(id).CostMonth ? CityUi.Good : CityUi.Bad;

			var select = new ButtonWidget(Game.ModData)
			{
				Bounds = new WidgetBounds(0, 0, row.Bounds.Width, row.Bounds.Height),
				Background = "",
				VisualHeight = 0,
				OnClick = () => selected = id
			};

			row.AddChild(select);
		}
	}
}
