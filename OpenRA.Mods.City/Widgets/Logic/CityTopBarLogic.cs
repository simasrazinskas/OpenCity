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
	/// Sidebar readouts: cash, monthly balance and date in the top strip, the status block (milestone,
	/// happiness, power, water) and the speed buttons of the command bar.
	/// </summary>
	public class CityTopBarLogic : ChromeLogic
	{
		[FluentReference("balance")]
		const string BalancePerMonth = "label-city-balance";

		[FluentReference("name", "population", "next")]
		const string MilestoneProgress = "label-city-milestone-bar";

		[FluentReference("name", "population")]
		const string MilestoneMax = "label-city-milestone-max";

		[FluentReference("value")]
		const string HappinessBar = "label-city-happiness-bar";

		[FluentReference("value")]
		const string HappinessTitle = "label-city-happiness-title";

		[FluentReference("xp", "next")]
		const string ProgressXp = "label-city-progress-xp";

		[FluentReference("points")]
		const string ProgressPoints = "label-city-progress-points";

		[FluentReference("permits")]
		const string ProgressPermits = "label-city-progress-permits";

		[FluentReference("used", "produced")]
		const string PowerBar = "label-city-power-bar";

		[FluentReference("used", "produced")]
		const string WaterBar = "label-city-water-bar";

		readonly World world;
		readonly CityManager manager;
		readonly CityUiContext ctx;

		[ObjectCreator.UseCtor]
		public CityTopBarLogic(Widget widget, World world)
		{
			this.world = world;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);

			InitStrip(widget);
			InitStatus(widget);
			InitSpeedButtons(widget);
		}

		void InitStrip(Widget widget)
		{
			var cash = widget.Get<LabelWithTooltipWidget>("CASH");
			cash.GetText = () => manager == null ? "" : CityUtils.FormatMoneyCompact(manager.Funds, dollar: false);
			cash.GetColor = () => manager != null && manager.Funds < 0 ? CityUi.Bad : Color.White;
			cash.GetTooltipText = () => manager == null ? "" :
				CityUtils.FormatMoney(manager.Funds) + "\n" + FluentProvider.GetMessage(BalancePerMonth, "balance", CityUi.SignedMoney(manager.MonthlyBalance));

			var balance = widget.Get<LabelWidget>("BALANCE");
			balance.GetText = () => manager == null ? "" : (manager.MonthlyBalance > 0 ? "+" : "") + CityUtils.FormatMoneyCompact(manager.MonthlyBalance, 10000);
			balance.GetColor = () => manager != null && manager.MonthlyBalance < 0 ? CityUi.Bad : CityUi.Good;

			var date = widget.Get<LabelWidget>("DATE");
			date.GetText = () => manager?.Date.ToString() ?? "";
		}

		// The utility network provider (NET WP) wins over the legacy aggregate numbers of the city manager.
		int PowerUsed => ctx.Utilities?.PowerConsumed ?? manager?.PowerConsumed ?? 0;
		int PowerMade => ctx.Utilities?.PowerProduced ?? manager?.PowerProduced ?? 0;
		int WaterUsed => ctx.Utilities?.WaterConsumed ?? manager?.WaterConsumed ?? 0;
		int WaterMade => ctx.Utilities?.WaterProduced ?? manager?.WaterProduced ?? 0;

		static int Ratio(int used, int produced)
		{
			if (produced <= 0)
				return used > 0 ? 100 : 0;

			return (int)(used * 100L / produced);
		}

		string ProgressText()
		{
			var progression = ctx.Progression;
			if (progression == null)
				return "";

			var text = FluentProvider.GetMessage(ProgressXp,
				"xp", progression.Xp.ToString("N0", CultureInfo.CurrentCulture),
				"next", progression.NextMilestoneXp.ToString("N0", CultureInfo.CurrentCulture)) +
				"\n" + FluentProvider.GetMessage(ProgressPoints, "points", progression.DevPoints);

			if (ctx.ProgressionUi != null)
				text += "\n" + FluentProvider.GetMessage(ProgressPermits, "permits", ctx.ProgressionUi.Permits);

			return text;
		}

		void InitStatus(Widget widget)
		{
			var milestoneLabel = widget.Get<LabelWithTooltipWidget>("MILESTONE_LABEL");
			milestoneLabel.GetTooltipText = ProgressText;
			milestoneLabel.GetText = () =>
			{
				if (manager == null)
					return "";

				if (manager.NextMilestonePopulation <= manager.Population)
					return FluentProvider.GetMessage(MilestoneMax, "name", manager.MilestoneName, "population", manager.Population.ToString("N0", CultureInfo.CurrentCulture));

				return FluentProvider.GetMessage(MilestoneProgress,
					"name", manager.MilestoneName,
					"population", manager.Population.ToString("N0", CultureInfo.CurrentCulture),
					"next", manager.NextMilestonePopulation.ToString("N0", CultureInfo.CurrentCulture));
			};

			var milestoneBar = widget.Get<CityBarWidget>("MILESTONE_BAR");
			milestoneBar.GetPercentage = () =>
			{
				if (manager == null)
					return 0;

				if (manager.NextMilestonePopulation <= 0 || manager.NextMilestonePopulation <= manager.Population)
					return 100;

				return (int)(manager.Population * 100L / manager.NextMilestonePopulation);
			};

			var happinessLabel = widget.Get<LabelWidget>("HAPPINESS_LABEL");
			happinessLabel.GetText = () => manager == null ? "" : FluentProvider.GetMessage(HappinessBar, "value", manager.AverageHappiness);

			var happinessBar = widget.Get<CityBarWidget>("HAPPINESS_BAR");

			// Hovering the happiness bar lists what makes the citizens (un)happy.
			var happinessHover = new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				Bounds = happinessBar.Bounds,
				GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", manager?.AverageHappiness ?? 0),
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};

			happinessBar.Parent.AddChild(happinessHover);
			happinessBar.GetPercentage = () => manager?.AverageHappiness ?? 0;
			happinessBar.GetBarColor = () =>
			{
				var happy = manager?.AverageHappiness ?? 0;
				return happy >= 60 ? CityUi.Good : happy >= 35 ? CityUi.Warn : CityUi.Bad;
			};

			var powerLabel = widget.Get<LabelWidget>("POWER_LABEL");
			powerLabel.GetText = () => manager == null ? "" :
				FluentProvider.GetMessage(PowerBar, "used", PowerUsed, "produced", PowerMade);

			var powerBar = widget.Get<CityBarWidget>("POWER_BAR");
			powerBar.GetPercentage = () => manager == null ? 0 : Ratio(PowerUsed, PowerMade);
			powerBar.GetBarColor = () => manager != null && PowerUsed > PowerMade ? CityUi.Bad : CityUi.Good;

			var waterLabel = widget.Get<LabelWidget>("WATER_LABEL");
			waterLabel.GetText = () => manager == null ? "" :
				FluentProvider.GetMessage(WaterBar, "used", WaterUsed, "produced", WaterMade);

			var waterBar = widget.Get<CityBarWidget>("WATER_BAR");
			waterBar.GetPercentage = () => manager == null ? 0 : Ratio(WaterUsed, WaterMade);
			waterBar.GetBarColor = () => manager != null && WaterUsed > WaterMade ? CityUi.Bad : CityUi.Good;
		}

		void InitSpeedButtons(Widget widget)
		{
			var pause = widget.Get<ButtonWidget>("SPEED_PAUSE");
			pause.OnClick = () => world.SetPauseState(!world.PredictedPaused);
			pause.IsHighlighted = () => world.PredictedPaused;

			for (var i = 1; i <= 3; i++)
			{
				var speed = i;
				var button = widget.Get<ButtonWidget>("SPEED_" + i);
				button.IsDisabled = () => manager == null;
				button.IsHighlighted = () => manager != null && !world.PredictedPaused && manager.Speed == speed;
				button.OnClick = () =>
				{
					if (world.LocalPlayer == null)
						return;

					world.IssueOrder(CityOrders.SetSpeedOrder(world.LocalPlayer, speed));
					if (world.PredictedPaused)
						world.SetPauseState(false);
				};
			}
		}
	}
}
