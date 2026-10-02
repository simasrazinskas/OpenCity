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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The contents of the Overview, Finances and Efficiency pages (lines rebuilt a few times per second) and the problem chips.</summary>
	public partial class CityBuildingInfoLogic
	{
		static string Msg(string key) { return CityUi.Message(key); }

		static string OfMax(int current, int max)
		{
			return FluentProvider.GetMessage(CurrentOfMax, "current", current, "max", max);
		}

		static string PercentText(int value)
		{
			return FluentProvider.GetMessage(Percent, "value", value);
		}

		static string RampFor(int percent)
		{
			return percent >= 60 ? "green" : percent >= 35 ? "yellow" : "red";
		}

		int PowerPercent => ctx.Utilities != null ? ctx.Utilities.PowerPercent(actor) : building.HasPower ? 100 : 0;

		int WaterPercent => ctx.Utilities != null ? ctx.Utilities.WaterPercent(actor) : building.HasWater ? 100 : 0;

		bool RoadOk => building.HasRoadAccess && (property == null || property.HasRoadAccess);

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

		string HappinessTitleText(PageLine line)
		{
			return FluentProvider.GetMessage(HappinessTitle, "value", line.Percent);
		}

		/// <summary>The happiness row lists the factors the citizen simulation reports for the whole city when hovered.</summary>
		FactorHoverWidget MakeHappinessHover()
		{
			return new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};
		}

		// Occupancy meters: what the building holds against its capacity (patients, students, residents, workers).
		void AddOccupancy(LinePool l)
		{
			if (property != null)
			{
				if (property.Beds > 0)
					l.AddMeter(Msg("label-inspect-patients"), OfMax(property.Patients, property.Beds), Ratio(property.Patients, property.Beds), "green");

				if (property.StudentSeats > 0)
					l.AddMeter(Msg("label-inspect-students"), OfMax(property.Students, property.StudentSeats), Ratio(property.Students, property.StudentSeats), "green");

				if (property.HouseholdSlots > 0)
				{
					var capacity = property.HouseholdSlots * 3;
					l.AddMeter(Msg("label-inspect-residents"), OfMax(property.Residents, capacity), Ratio(property.Residents, capacity), "green");
				}

				if (property.TotalJobSlots > 0)
					l.AddMeter(
						Msg("label-inspect-workers"), OfMax(property.TotalJobsFilled, property.TotalJobSlots),
						Ratio(property.TotalJobsFilled, property.TotalJobSlots), "blue");

				return;
			}

			if (building.Info.MaxResidents > 0)
				l.AddMeter(
					Msg("label-inspect-residents"), OfMax(building.Residents, building.Info.MaxResidents),
					Ratio(building.Residents, building.Info.MaxResidents), "green");

			if (building.Info.MaxJobs > 0)
				l.AddMeter(Msg("label-inspect-workers"), OfMax(building.Workers, building.Info.MaxJobs), Ratio(building.Workers, building.Info.MaxJobs), "blue");
		}

		static int Ratio(int value, int max) { return max > 0 ? (int)(value * 100L / max) : 0; }

		void RefreshOverview()
		{
			var l = overviewLines;
			l.Clear();
			AddOccupancy(l);

			// The first bar of an inspection contributor is the building's own performance meter (service efficiency, hub output).
			foreach (var row in extra)
			{
				if (row.BarPercent < 0)
					continue;

				l.AddMeter(row.Label, PercentText(row.BarPercent), row.BarPercent, "yellow");
				break;
			}

			if (growable != null)
				l.AddMeter(Msg("label-inspect-happiness"), PercentText(HappinessValue()), HappinessValue(), "yellow");

			if (l.Count < 3 && !(building.Info.PowerUse == 0 && ctx.Utilities == null))
				l.AddMeter(Msg("label-inspect-electricity"), PercentText(PowerPercent), PowerPercent, RampFor(PowerPercent));

			l.Apply();
			RefreshChips();
		}

		void RefreshChips()
		{
			chips.Clear();
			void Problem(string icon, string text, string ramp) => chips.Add(new Chip { Icon = icon, Text = text, Ramp = ramp });

			if (growable != null && growable.UnderConstruction)
				Problem("stat_buildings", FluentProvider.GetMessage(StatusConstruction), "yellow");
			else if (growable != null && growable.Abandoned)
				Problem("st_abandoned", FluentProvider.GetMessage(StatusAbandoned), "red");
			else if (!building.IsOperational)
				Problem("ui_warning", FluentProvider.GetMessage(StatusOffline), "red");

			if (!building.HasPower || (ctx.Utilities != null && PowerPercent < 50))
				Problem("st_no_power", FluentProvider.GetMessage(NoPower), "orange");

			if (!building.HasWater || (ctx.Utilities != null && WaterPercent < 50))
				Problem("st_no_water", FluentProvider.GetMessage(NoWater), "orange");

			if (growable != null && ctx.Utilities != null && !ctx.Utilities.HasSewage(actor))
				Problem("st_no_sewage", FluentProvider.GetMessage(NoSewage), "orange");

			if (!RoadOk)
				Problem("st_no_road", FluentProvider.GetMessage(NoRoad), "red");

			if (property != null && property.TotalJobSlots > 0 && Ratio(property.TotalJobsFilled, property.TotalJobSlots) < 50)
				Problem("st_no_workers", Msg("label-inspect-problem-staff"), "yellow");

			if (property != null && property.Residents > 0 && HappinessValue() < 35)
				Problem("st_unhappy", Msg("label-inspect-problem-unhappy"), "yellow");

			var signature = chips.Count + ":" + string.Join(",", chips.ConvertAll(c => c.Text));
			if (signature == chipSignature)
				return;

			chipSignature = signature;
			chipContainer.RemoveChildren();
			var font = Game.Renderer.Fonts["TinyBold"];
			var x = 0;
			foreach (var chip in chips)
			{
				var width = 2 + 16 + 2 + font.Measure(chip.Text).X + 6;
				if (x + width > chipContainer.Bounds.Width)
					break;

				var widget = (BackgroundWidget)Game.LoadWidget(world, "CITY_BUILDING_CHIP", chipContainer, []);
				widget.Background = "chip-" + chip.Ramp;
				widget.Bounds = new WidgetBounds(x, 0, width, 16);
				widget.Get<CityIconWidget>("ICON").Icon = chip.Icon;
				var label = widget.Get<LabelWidget>("TEXT");
				label.Bounds.Width = width - 22;
				var text = chip.Text;
				label.GetText = () => text;
				var light = chip.Ramp != "yellow";
				label.GetColor = () => light ? CityTheme.InkLight : CityTheme.Ink;
				x += width + 4;
			}
		}

		string chipSignature;

		int UpgradeUpkeep()
		{
			var upgrades = ctx.Upgrades?.UpgradesOf(actor);
			var sum = 0;
			if (upgrades != null)
				foreach (var upgrade in upgrades)
					if (upgrade.Owned)
						sum += upgrade.UpkeepPerMonth;

			return sum;
		}

		void RefreshFinances()
		{
			var l = financeLines;
			l.Clear();
			var upkeep = building.Info.Upkeep;
			var upgrades = UpgradeUpkeep();
			if (upkeep > 0 || upgrades > 0)
			{
				l.AddHeader(Msg("label-inspect-header-costs"));
				if (upkeep > 0)
					l.AddValue(Msg("label-building-upkeep"), FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(-upkeep)), CityTheme.MoneyNegative);

				if (upgrades > 0)
					l.AddValue(Msg("label-inspect-upgrade-upkeep"), FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(-upgrades)), CityTheme.MoneyNegative);

				if (upkeep > 0 && upgrades > 0)
					l.AddValue(
						Msg("label-inspect-total"),
						FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(-(upkeep + upgrades))),
						CityTheme.MoneyNegative);
			}

			var level = property != null && property.Level > 0 && growable != null;
			var rent = property != null && property.RentPerMonth > 0;
			var company = property != null && property.CompanyId != 0 && ctx.Economy?.DescribeCompany(property.Id) != null;
			l.AddHeader(Msg("label-inspect-header-property"));
			if (level)
				l.AddValue(Msg("label-building-level"), FluentProvider.GetMessage(LevelOfFive, "level", property.Level));

			if (rent)
				l.AddValue(
					Msg("label-building-rent"),
					FluentProvider.GetMessage(PerMonth, "amount", CityUtils.FormatMoney(property.RentPerMonth)),
					CityTheme.MoneyPositive);

			var landValue = property?.LandValue ?? building.LandValue;
			l.AddMeter(Msg("label-building-landvalue"), landValue.ToString(CultureInfo.CurrentCulture), landValue, "blue");
			if (company)
				l.AddValue(Msg("label-building-company"), ctx.Economy.DescribeCompany(property.Id));

			l.Apply();
		}

		void RefreshEfficiency()
		{
			var l = efficiencyLines;
			l.Clear();
			l.AddHeader(Msg("label-inspect-header-supply"));

			if (ctx.Utilities != null)
			{
				l.AddMeter(Msg("label-inspect-electricity"), PercentText(PowerPercent), PowerPercent, RampFor(PowerPercent));
				l.AddMeter(Msg("label-building-water"), PercentText(WaterPercent), WaterPercent, RampFor(WaterPercent));
				var sewage = ctx.Utilities.HasSewage(actor);
				l.AddValue(Msg("label-building-sewage"), FluentProvider.GetMessage(sewage ? Connected : NoSewage), sewage ? CityUi.Good : CityUi.Bad);
			}
			else
			{
				l.AddValue(
					Msg("label-inspect-electricity"), FluentProvider.GetMessage(building.HasPower ? Connected : NoPower),
					building.HasPower ? CityUi.Good : CityUi.Bad);
				l.AddValue(
					Msg("label-building-water"), FluentProvider.GetMessage(building.HasWater ? Connected : NoWater),
					building.HasWater ? CityUi.Good : CityUi.Bad);
			}

			l.AddValue(Msg("label-building-road"), FluentProvider.GetMessage(RoadOk ? Connected : NoRoad), RoadOk ? CityUi.Good : CityUi.Bad);

			if (property != null && property.TotalJobSlots > 0)
			{
				var staffing = Ratio(property.TotalJobsFilled, property.TotalJobSlots);
				l.AddMeter(Msg("label-inspect-staffing"), PercentText(staffing), staffing, RampFor(staffing));
			}

			l.AddHeader(Msg("label-inspect-header-performance"));
			if (growable != null || (property != null && property.Residents > 0) || building.Info.MaxResidents > 0)
			{
				var happiness = HappinessValue();
				l.AddMeter(Msg("label-inspect-happiness"), PercentText(happiness), happiness, RampFor(happiness), "happiness");
			}

			foreach (var row in extra)
			{
				if (row.BarPercent >= 0)
					l.AddMeter(row.Label, row.Value, row.BarPercent, RampFor(row.Tone == 3 ? 0 : row.Tone == 2 ? 40 : 100));
				else
					l.AddValue(row.Label, row.Value, CityUi.ToneColor(row.Tone));
			}

			l.Apply();
		}
	}
}
