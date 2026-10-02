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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 status bar along the bottom (design/iso/ui/screens/hud-*.png, tools/iso_ui_hud.statusbar): money and
	/// monthly balance, population (with the milestone in its tooltip) and happiness | the newest chirp as a news ticker
	/// (click opens the chirper) and the alert chips | date, clock, weather and the RCI(O) demand bars. The three sections
	/// share the window width; the side sections narrow on small logical widths.
	/// </summary>
	public class CityStatusBarLogic : ChromeLogic
	{
		[FluentReference("balance")]
		const string BalancePerMonth = "label-city-balance";

		[FluentReference]
		const string UnlimitedMoney = "label-city-unlimited-money";

		[FluentReference("name", "population", "next")]
		const string MilestoneProgress = "label-city-milestone-bar";

		[FluentReference("name", "population")]
		const string MilestoneMax = "label-city-milestone-max";

		[FluentReference("value")]
		const string HappinessTitle = "label-city-happiness-title";

		[FluentReference("xp", "next")]
		const string ProgressXp = "label-city-progress-xp";

		[FluentReference("points")]
		const string ProgressPoints = "label-city-progress-points";

		[FluentReference("permits")]
		const string ProgressPermits = "label-city-progress-permits";

		[FluentReference("balance")]
		const string BalanceLine = "label-city-statusbar-balance";

		[FluentReference]
		const string NoNews = "label-city-statusbar-no-news";

		[FluentReference("temperature")]
		const string TemperatureLabel = "label-city-statusbar-temperature";

		public const int Height = 38;

		readonly World world;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		readonly Widget bar;
		readonly Widget left;
		readonly Widget center;
		readonly Widget right;
		readonly LabelWidget[] fitted;
		int laidOutVersion = -1;

		[ObjectCreator.UseCtor]
		public CityStatusBarLogic(Widget widget, World world)
		{
			this.world = world;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);

			bar = widget.Get("CITY_STATUSBAR");
			left = bar.Get("LEFT");
			center = bar.Get("CENTER");
			right = bar.Get("RIGHT");

			InitMoney();
			InitPopulation();
			InitTicker();
			InitDate();

			fitted = [left.Get<LabelWidget>("CASH"), left.Get<LabelWidget>("POPULATION"), right.Get<LabelWidget>("DATE")];
		}

		void InitMoney()
		{
			var cash = left.Get<LabelWithTooltipWidget>("CASH");

			// The bundled regular face includes infinity; its bold counterpart does not.
			if (manager?.UnlimitedMoney == true)
				cash.Font = "Regular";

			cash.GetText = () => manager == null ? "" : manager.UnlimitedMoney ? "∞" : CityUtils.FormatMoney(manager.Funds);
			cash.GetColor = () => manager != null && manager.Funds < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;
			cash.GetTooltipText = () => manager == null ? "" :
				(manager.UnlimitedMoney ? FluentProvider.GetMessage(UnlimitedMoney) : CityUtils.FormatMoney(manager.Funds)) + "\n" +
				FluentProvider.GetMessage(BalancePerMonth, "balance", CityUi.SignedMoney(manager.MonthlyBalance));

			left.Get<CityIconWidget>("BALANCE_ICON").GetIcon = () => manager != null && manager.MonthlyBalance < 0 ? "stat_balance_down" : "stat_balance_up";
			var balance = left.Get<LabelWidget>("BALANCE");
			var balanceFont = Game.Renderer.Fonts[balance.Font];
			balance.GetText = () =>
			{
				if (manager == null)
					return "";

				// Narrow status bars: compact amounts ($12.4k) before the text would be cut.
				var full = FluentProvider.GetMessage(BalanceLine, "balance", CityUi.SignedMoney(manager.MonthlyBalance));
				if (balanceFont.Measure(full).X <= balance.Bounds.Width)
					return full;

				var sign = manager.MonthlyBalance > 0 ? "+" : "";
				return FluentProvider.GetMessage(BalanceLine, "balance", sign + CityUtils.FormatMoneyCompact(manager.MonthlyBalance, 1000));
			};
			balance.GetColor = () => manager != null && manager.MonthlyBalance < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;
		}

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

		void InitPopulation()
		{
			var population = left.Get<LabelWithTooltipWidget>("POPULATION");
			population.GetText = () => manager == null ? "" : manager.Population.ToString("N0", CultureInfo.CurrentCulture);
			population.GetColor = () => CityTheme.Ink;
			population.GetTooltipText = () =>
			{
				var progress = ProgressText();
				return progress.Length > 0 ? MilestoneText() + "\n" + progress : MilestoneText();
			};

			// Trend arrow after the number: population now against the previous statistics sample.
			var trend = left.Get<LabelWidget>("POPULATION_TREND");
			var font = Game.Renderer.Fonts[population.Font];
			int Trend()
			{
				var history = ctx.Statistics?.History("population", 2);
				return history == null || history.Count < 2 ? 0 : Math.Sign(history[^1] - history[^2]);
			}

			trend.GetText = () => Trend() > 0 ? "▲" : Trend() < 0 ? "▼" : "";
			trend.GetColor = () => Trend() >= 0 ? CityTheme.MoneyPositive : CityTheme.MoneyNegative;
			trend.IsVisible = () =>
			{
				trend.Bounds.X = population.Bounds.X + font.Measure(population.GetText()).X + 4;
				return true;
			};

			var happiness = left.Get<CityBarWidget>("HAPPINESS");
			happiness.GetPercentage = () => manager?.AverageHappiness ?? 0;
			happiness.GetBarColor = () => CityUi.PercentColor(manager?.AverageHappiness ?? 0);

			var hover = left.Get<FactorHoverWidget>("HAPPINESS_HOVER");
			hover.GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", manager?.AverageHappiness ?? 0);
			hover.GetFactors = () => ctx.Citizens?.HappinessFactors;
		}

		void InitTicker()
		{
			var ticker = center.Get<ButtonWidget>("TICKER");
			var text = ticker.Get<LabelWidget>("TEXT");
			var noNews = FluentProvider.GetMessage(NoNews);
			string Latest()
			{
				var entries = ctx.Chirper?.Entries;
				if (entries == null || entries.Count == 0)
					return noNews;

				var chirp = entries[^1];
				return "@" + CityChirperLogic.AuthorName(ctx, chirp) + ": " + CityChirperLogic.Message(chirp);
			}

			text.GetText = CityUi.Fitted(text, Latest);
			ticker.GetTooltipText = Latest;
			ticker.OnClick = () =>
			{
				var panel = Ui.Root.GetOrNull("CITY_CHIRPER_PANEL");
				if (panel != null)
					panel.Visible = !panel.Visible;
			};
		}

		void InitDate()
		{
			var date = right.Get<LabelWidget>("DATE");
			date.GetText = () => manager == null ? "" : DateText(manager.Date);

			var time = right.Get<LabelWidget>("TIME");
			time.GetText = () => manager == null || manager.Date.Hour < 0 ? "" : $"{manager.Date.Hour:D2}:{manager.Date.Minute:D2}";
			right.Get("TIME_ICON").IsVisible = () => manager != null && manager.Date.Hour >= 0;

			var climate = world.WorldActor.TraitOrDefault<CityClimate>();
			var weatherIcon = right.Get<CityIconWidget>("WEATHER_ICON");
			weatherIcon.GetIcon = () => climate == null ? null : WeatherIcon(climate.Weather, manager?.Date.Hour ?? 12);
			var weather = right.Get<LabelWithTooltipWidget>("WEATHER");
			weather.IsVisible = () => climate != null;
			weather.GetText = () => climate == null ? "" : FluentProvider.GetMessage(TemperatureLabel, "temperature",
				(climate.TemperatureX10 / 10f).ToString("0", CultureInfo.CurrentCulture));
			weather.GetTooltipText = () => climate == null ? "" : CityUi.Message("label-weather-" + climate.Weather.ToString().ToLowerInvariant());
		}

		static string DateText(CityDate d)
		{
			var month = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(Math.Clamp(d.Month, 1, 12));
			if (d.Hour >= 0)
				return $"{month} {d.Year}";

			return d.ToString();
		}

		static string WeatherIcon(CityWeather weather, int hour)
		{
			var night = hour >= 0 && (hour < 6 || hour >= 20);
			return weather switch
			{
				CityWeather.Cloudy => "wx_cloud",
				CityWeather.Rain => "wx_rain",
				CityWeather.Snow => "wx_snow",
				CityWeather.Storm => "wx_storm",
				_ => night ? "wx_night" : "wx_sun"
			};
		}

		public override void Tick()
		{
			var version = CityLayout.Version;
			if (version != laidOutVersion)
			{
				laidOutVersion = version;
				Layout();
			}

			foreach (var label in fitted)
				CityUi.FitFont(label, ["TinyBold", "Tiny"]);
		}

		/// <summary>Side sections: 250 logical pixels on wide screens, narrower (down to 180) when the window is small.</summary>
		void Layout()
		{
			var width = CityLayout.Window.X;
			var side = Math.Clamp(width / 5, 180, 250);
			bar.Bounds = new WidgetBounds(0, CityLayout.Window.Y - Height, width, Height);
			left.Bounds = new WidgetBounds(0, 0, side, Height);
			right.Bounds = new WidgetBounds(width - side, 0, side, Height);
			center.Bounds = new WidgetBounds(side + 2, 0, Math.Max(40, width - 2 * side - 4), Height);

			// Left section: money column and population column split the width
			var split = side * 3 / 5;
			left.Get("DIVIDER").Bounds.X = split;
			foreach (var id in new[] { "CASH", "BALANCE" })
				left.Get(id).Bounds.Width = split - 27;

			var popX = split + 6;
			left.Get("POPULATION_ICON").Bounds.X = popX;
			left.Get("HAPPINESS_ICON").Bounds.X = popX;
			left.Get("HAPPINESS_HOVER").Bounds = new WidgetBounds(popX, 19, side - popX - 4, 16);
			foreach (var id in new[] { "POPULATION", "HAPPINESS" })
			{
				var w = left.Get(id);
				w.Bounds.X = popX + 20;
				w.Bounds.Width = side - popX - 26;
			}

			// Right section: date column, weather, demand bars at the right edge
			var demand = right.Get("DEMAND_BARS");
			demand.Bounds.X = side - demand.Bounds.Width - 6;
			var label = right.Get("DEMAND_LABEL");
			label.Bounds.X = demand.Bounds.X - label.Bounds.Width - 4;

			// Narrow bars: the "Demand" caption gives its room to the clock and the weather (the bars keep their tooltips).
			var showLabel = side >= 230;
			label.Visible = showLabel;
			if (!showLabel)
				label.Bounds.X = demand.Bounds.X - 2;
			right.Get("DATE").Bounds.Width = label.Bounds.X - 26;
			var weatherX = Math.Min(66, label.Bounds.X - 80);
			right.Get("WEATHER_ICON").Bounds.X = weatherX;
			right.Get("WEATHER").Bounds.X = weatherX + 20;

			var ticker = center.Get("TICKER");
			ticker.Bounds.Width = center.Bounds.Width - 8;
			ticker.Get("TEXT").Bounds.Width = ticker.Bounds.Width - 26;
			center.Get("ALERTS").Bounds.Width = center.Bounds.Width - 8;
		}
	}
}
