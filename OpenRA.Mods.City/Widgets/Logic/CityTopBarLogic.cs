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

		[FluentReference]
		const string HappinessStat = "label-city-stat-happiness";

		[FluentReference]
		const string PowerStat = "label-city-stat-power";

		[FluentReference]
		const string WaterStat = "label-city-stat-water";

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
			stripLabels = [cash, balance, date];
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
			string MilestoneText()
			{
				if (manager == null)
					return "";

				var population = manager.Population.ToString("N0", CultureInfo.CurrentCulture);
				if (manager.NextMilestonePopulation <= manager.Population)
					return FluentProvider.GetMessage(MilestoneMax, "name", manager.MilestoneName, "population", population);

				return FluentProvider.GetMessage(MilestoneProgress, "name", manager.MilestoneName, "population", population,
					"next", manager.NextMilestonePopulation.ToString("N0", CultureInfo.CurrentCulture));
			}

			var milestone = widget.Get<CityStatWidget>("MILESTONE_STAT");
			milestone.GetLabel = () => manager?.MilestoneName ?? "";
			milestone.GetValue = () =>
			{
				if (manager == null)
					return "";

				var population = CityUi.Compact(manager.Population);
				return manager.NextMilestonePopulation <= manager.Population ? population : population + " / " + CityUi.Compact(manager.NextMilestonePopulation);
			};

			milestone.GetTooltipText = () =>
			{
				var progress = ProgressText();
				return progress.Length > 0 ? MilestoneText() + "\n" + progress : MilestoneText();
			};

			milestone.GetBarColor = () => CityUi.Accent;
			milestone.GetPercentage = () =>
			{
				if (manager == null)
					return 0;

				if (manager.NextMilestonePopulation <= 0 || manager.NextMilestonePopulation <= manager.Population)
					return 100;

				return (int)(manager.Population * 100L / manager.NextMilestonePopulation);
			};

			var happiness = widget.Get<CityStatWidget>("HAPPINESS_STAT");
			var happinessLabel = FluentProvider.GetMessage(HappinessStat);
			happiness.GetLabel = () => happinessLabel;
			happiness.GetValue = () => manager == null ? "" : manager.AverageHappiness.ToString(CultureInfo.CurrentCulture) + "%";
			happiness.GetValueColor = () => CityUi.PercentColor(manager?.AverageHappiness ?? 0);
			happiness.GetPercentage = () => manager?.AverageHappiness ?? 0;
			happiness.GetBarColor = () => CityUi.PercentColor(manager?.AverageHappiness ?? 0);

			// Hovering the happiness readout lists what makes the citizens (un)happy. The sidebar layout keeps it on the readout.
			happinessHover = new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				Bounds = happiness.Bounds,
				GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", manager?.AverageHappiness ?? 0),
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};

			happiness.Parent.AddChild(happinessHover);
			happinessStat = happiness;

			SetupUtility(widget.Get<CityStatWidget>("POWER_STAT"), PowerStat, PowerBar, () => PowerUsed, () => PowerMade);
			SetupUtility(widget.Get<CityStatWidget>("WATER_STAT"), WaterStat, WaterBar, () => WaterUsed, () => WaterMade);
		}

		void SetupUtility(CityStatWidget stat, string labelKey, string tooltipKey, Func<int> used, Func<int> made)
		{
			var label = FluentProvider.GetMessage(labelKey);
			bool Short() => manager != null && used() > made();
			stat.GetLabel = () => label;
			stat.GetValue = () => manager == null ? "" : CityUi.Compact(used()) + " / " + CityUi.Compact(made());
			stat.GetValueColor = () => Short() ? CityUi.Bad : Color.White;
			stat.GetTooltipText = () => manager == null ? "" : FluentProvider.GetMessage(tooltipKey, "used", used(), "produced", made());
			stat.GetPercentage = () => manager == null ? 0 : Ratio(used(), made());
			stat.GetBarColor = () => Short() ? CityUi.Bad : CityUi.Good;
		}

		FactorHoverWidget happinessHover;
		CityStatWidget happinessStat;

		/// <summary>Keeps the happiness factor hover region on its readout after the sidebar moved it.</summary>
		public override void Tick()
		{
			if (happinessHover != null && happinessStat != null)
				happinessHover.Bounds = happinessStat.Bounds;

			foreach (var label in stripLabels)
				CityUi.FitFont(label, StripFonts);
		}

		/// <summary>The strip readouts step down to a smaller font rather than overflow (large numbers, bigger UI scales).</summary>
		static readonly string[] StripFonts = ["Bold", "Regular", "Small", "Tiny"];

		LabelWidget[] stripLabels = [];

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
