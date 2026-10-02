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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 city info window (design/iso/ui/panels/city-info.png): the city's level and XP, the population pyramid by age,
	/// education levels, households, employment (jobs, workers, unemployed, vacancies) and happiness with the factors that
	/// drive it. A read-only dashboard over ICitizenPopulation, IProgression and the CityManager.
	/// </summary>
	public class CityInfoLogic : ChromeLogic
	{
		[FluentReference("count")]
		const string PopulationHeader = "label-cityinfo-population";

		[FluentReference("value")]
		const string HappinessHeader = "label-cityinfo-happiness";

		[FluentReference("level", "total")]
		const string LevelText = "label-cityinfo-level";

		[FluentReference("name", "xp")]
		const string NextText = "label-cityinfo-next";

		[FluentReference]
		const string TopLevel = "label-cityinfo-top-level";

		[FluentReference]
		const string AverageSize = "label-cityinfo-avg-size";

		[FluentReference("rate")]
		const string UnemployedRate = "label-cityinfo-unemployed-rate";

		[FluentReference("count")]
		const string Vacant = "label-cityinfo-vacant";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		const int MaxFactors = 10;

		static readonly (AgeGroup Age, string Ramp)[] Ages =
			[(AgeGroup.Senior, "orange"), (AgeGroup.Adult, "blue"), (AgeGroup.Teen, "green"), (AgeGroup.Child, "yellow")];

		static readonly (EducationLevel Level, string Ramp)[] Levels =
		[
			(EducationLevel.Uneducated, "orange"), (EducationLevel.Poorly, "yellow"), (EducationLevel.Educated, "green"),
			(EducationLevel.Well, "blue"), (EducationLevel.Highly, "purple")
		];

		readonly World world;
		readonly CityManager manager;
		readonly CityUiContext ctx;
		DemandFactor[] factors = [];
		int factorsAt = -1000;

		[ObjectCreator.UseCtor]
		public CityInfoLogic(Widget widget, World world)
		{
			this.world = world;
			manager = CityUi.GetManager(world);
			ctx = CityUiContext.For(world);

			BuildLevel(widget.Get("LEVEL"));
			var left = widget.Get("LEFT");
			var right = widget.Get("RIGHT");
			BuildPopulation(left);
			BuildEmployment(right);
			BuildHappiness(right);

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible && Game.RenderFrame - factorsAt >= 10)
				{
					factorsAt = Game.RenderFrame;
					factors = (ctx.Citizens?.HappinessFactors ?? [])
						.OrderByDescending(f => f.Value).ThenBy(f => f.Key, StringComparer.Ordinal).Take(MaxFactors).ToArray();
				}

				return visible;
			};
		}

		int Population => ctx.Citizens?.Population ?? manager?.Population ?? 0;

		static string Number(long value) { return value.ToString("N0", CultureInfo.CurrentCulture); }

		static string Pct(int value) { return FluentProvider.GetMessage(Percent, "value", value); }

		// ---- level and XP -----------------------------------------------------------------------
		void BuildLevel(Widget strip)
		{
			var milestones = ctx.ProgressionUi?.Milestones;
			var progression = ctx.Progression;
			int Index() => progression?.MilestoneIndex ?? manager?.MilestoneIndex ?? 0;
			int Total() => milestones is { Count: > 0 } ? milestones.Count : CityManager.MilestoneNames.Length;
			int Xp() => progression?.Xp ?? Population;
			int NextXp() => progression != null ? progression.NextMilestoneXp : manager?.NextMilestonePopulation ?? 1;
			bool Last() => Index() + 1 >= Total();

			strip.Get<LabelWidget>("LEVEL_NAME").GetText = () => progression?.MilestoneName ?? manager?.MilestoneName ?? "";
			var sub = strip.Get<LabelWidget>("LEVEL_SUB");
			sub.GetText = () => FluentProvider.GetMessage(LevelText, "level", Index() + 1, "total", Total());
			sub.GetColor = () => CityTheme.Muted("city");

			strip.Get<LabelWidget>("XP_LABEL").GetColor = () => CityTheme.Ink;
			strip.Get<LabelWidget>("XP_VALUE").GetText = () => Number(Xp()) + " / " + Number(Math.Max(NextXp(), Xp()));
			strip.Get<CityBarWidget>("XP_BAR").GetPercentage = () => Last() ? 100 : (int)(Xp() * 100L / Math.Max(1, NextXp()));

			var next = strip.Get<LabelWidget>("XP_NEXT");
			next.GetColor = () => CityTheme.Muted("city");
			next.GetText = () =>
			{
				if (Last())
					return FluentProvider.GetMessage(TopLevel);

				var upcoming = Math.Min(Index() + 1, CityManager.MilestoneNames.Length - 1);
				var name = milestones is { Count: > 0 } && Index() + 1 < milestones.Count ? milestones[Index() + 1].Name : CityManager.MilestoneNames[upcoming];
				return FluentProvider.GetMessage(NextText, "name", name, "xp", Number(NextXp()));
			};
		}

		// ---- population, education, households --------------------------------------------------
		void BuildPopulation(Widget column)
		{
			column.Get<CityHeaderWidget>("POP_HEADER").GetText = () => FluentProvider.GetMessage(PopulationHeader, "count", Number(Population));

			var ageRows = column.Get("AGE_WELL").Get("AGE_ROWS");
			int MaxAge() => Math.Max(1, Ages.Max(a => ctx.Citizens?.CountByAge(a.Age) ?? 0));
			for (var i = 0; i < Ages.Length; i++)
			{
				var (age, ramp) = Ages[i];
				var row = Game.LoadWidget(world, "CITY_INFO_AGE_ROW", ageRows, []);
				row.Bounds.Y = i * row.Bounds.Height;
				row.Bounds.Width = ageRows.Bounds.Width;
				var name = CityUi.AgeName(age);
				row.Get<LabelWidget>("NAME").GetText = () => name;
				var bar = row.Get<CityCenterBarWidget>("BAR");
				bar.Ramp = ramp;
				bar.GetPercentage = () => (ctx.Citizens?.CountByAge(age) ?? 0) * 100 / MaxAge();
				var value = row.Get<LabelWidget>("VALUE");
				value.Bounds.Width = row.Bounds.Width - 3;
				value.GetText = () => Number(ctx.Citizens?.CountByAge(age) ?? 0);
			}

			var eduRows = column.Get("EDU_WELL").Get("EDU_ROWS");
			int Share(EducationLevel level) => (ctx.Citizens?.CountByEducation(level) ?? 0) * 100 / Math.Max(1, Population);
			int MaxShare() => Math.Max(1, Levels.Max(l => Share(l.Level)));
			for (var i = 0; i < Levels.Length; i++)
			{
				var (level, ramp) = Levels[i];
				var row = Game.LoadWidget(world, "CITY_INFO_BAR_ROW", eduRows, []);
				row.Bounds.Y = i * row.Bounds.Height;
				row.Bounds.Width = eduRows.Bounds.Width;
				row.Get<CityRowBackgroundWidget>("BG").Bounds.Width = row.Bounds.Width;
				var name = CityUi.EducationName(level);
				row.Get<LabelWidget>("NAME").GetText = () => name;
				var bar = row.Get<CityBarWidget>("BAR");
				bar.Ramp = ramp;
				bar.GetPercentage = () => Share(level) * 100 / MaxShare();
				var value = row.Get<LabelWidget>("VALUE");
				value.Bounds.Width = row.Bounds.Width - 3;
				value.GetText = () => Pct(Share(level));
			}

			var houseRows = column.Get("HOUSE_ROWS");
			var lines = new (string Label, Func<string> Value, Func<Color> Color)[]
			{
				(FluentProvider.GetMessage("label-stats-households"), () => Number(ctx.Citizens?.Households ?? manager?.Households ?? 0), () => CityTheme.Ink),
				(FluentProvider.GetMessage(AverageSize), () =>
				{
					var households = ctx.Citizens?.Households ?? 0;
					return households > 0 ? (Population / (float)households).ToString("0.0", CultureInfo.CurrentCulture) : "-";
				}, () => CityTheme.Ink),
				(CityUi.Message("label-overview-students"), () => Number(ctx.Citizens?.Students ?? 0), () => CityTheme.Ink),
				(CityUi.Message("label-overview-homeless"), () => Number(ctx.Citizens?.Homeless ?? 0),
					() => (ctx.Citizens?.Homeless ?? 0) > 0 ? CityTheme.MoneyNegative : CityTheme.Ink)
			};

			for (var i = 0; i < lines.Length; i++)
			{
				var (label, value, color) = lines[i];
				var row = Game.LoadWidget(world, "CITY_INFO_ROW", houseRows, []);
				row.Bounds.Y = i * 12;
				row.Bounds.Height = 12;
				row.Bounds.Width = houseRows.Bounds.Width;
				var name = row.Get<LabelWidget>("NAME");
				var val = row.Get<LabelWidget>("VALUE");
				name.Bounds.Height = val.Bounds.Height = 12;
				name.Bounds.Width = 100;
				val.Bounds.X = 60;
				val.Bounds.Width = row.Bounds.Width - 60 - 3;
				name.Bounds.X = 3;
				name.GetText = () => label;
				val.GetText = value;
				val.GetColor = color;
			}
		}

		// ---- employment --------------------------------------------------------------------------
		void BuildEmployment(Widget column)
		{
			int Jobs() => manager?.Jobs ?? 0;
			int Workers() => ctx.Citizens?.Workers ?? manager?.Workers ?? 0;
			int Unemployed() => ctx.Citizens?.Unemployed ?? manager?.Unemployed ?? 0;
			int Scale() => Math.Max(1, Math.Max(Jobs(), Workers()));

			var rows = column.Get("EMP_ROWS");
			var lines = new (string Label, Func<int> Value, string Ramp)[]
			{
				(FluentProvider.GetMessage("label-stats-jobs"), Jobs, "blue"),
				(CityUi.Message("label-overview-workers"), Workers, "green"),
				(CityUi.Message("label-overview-unemployed"), Unemployed, "red")
			};

			for (var i = 0; i < lines.Length; i++)
			{
				var (label, value, ramp) = lines[i];
				var row = Game.LoadWidget(world, "CITY_INFO_BAR_ROW", rows, []);
				row.Bounds.Y = i * row.Bounds.Height;
				row.Bounds.Width = rows.Bounds.Width;
				row.Get<CityRowBackgroundWidget>("BG").Visible = false;
				row.Get<LabelWidget>("NAME").GetText = () => label;
				var bar = row.Get<CityBarWidget>("BAR");
				bar.Ramp = ramp;
				bar.Smooth = false;
				bar.Bounds.X = 62;
				bar.Bounds.Width = rows.Bounds.Width - 62 - 44;
				bar.GetPercentage = () => value() * 100 / Scale();
				var number = row.Get<LabelWidget>("VALUE");
				number.Bounds.Width = row.Bounds.Width - 3;
				number.GetText = () => Number(value());
			}

			var chips = column.Get("CHIPS");
			AddChip(chips, 0, 88, "chip-orange", () =>
			{
				var workers = Workers();
				var rate = workers > 0 ? Unemployed() * 100f / workers : 0;
				return FluentProvider.GetMessage(UnemployedRate, "rate", rate.ToString("0.#", CultureInfo.CurrentCulture) + "%");
			});

			AddChip(chips, 92, 76, "chip-blue", () => FluentProvider.GetMessage(Vacant, "count", Number(Math.Max(0, Jobs() - Workers() + Unemployed()))));
		}

		void AddChip(Widget parent, int x, int width, string art, Func<string> text)
		{
			var chip = Game.LoadWidget(world, "CITY_INFO_CHIP", parent, []) as BackgroundWidget;
			chip.Bounds.X = x;
			chip.Bounds.Width = width;
			chip.Background = art;
			var label = chip.Get<LabelWidget>("TEXT");
			label.Bounds.Width = width;
			label.GetText = text;
			label.GetColor = () => CityTheme.InkLight;
		}

		// ---- happiness ---------------------------------------------------------------------------
		void BuildHappiness(Widget column)
		{
			int Happiness() => ctx.Citizens?.AverageHappiness ?? manager?.AverageHappiness ?? 0;
			column.Get<CityHeaderWidget>("HAPPY_HEADER").GetText = () => FluentProvider.GetMessage(HappinessHeader, "value", Pct(Happiness()));
			column.Get<CityBarWidget>("HAPPY_BAR").GetPercentage = Happiness;

			var rows = column.Get("FACTOR_WELL").Get("FACTOR_ROWS");
			for (var i = 0; i < MaxFactors; i++)
			{
				var index = i;
				var row = Game.LoadWidget(world, "CITY_INFO_FACTOR_ROW", rows, []);
				row.Bounds.Y = i * row.Bounds.Height;
				row.Bounds.Width = rows.Bounds.Width;
				row.Get<CityRowBackgroundWidget>("BG").Bounds.Width = row.Bounds.Width;
				row.IsVisible = () => index < factors.Length;

				var arrow = row.Get<CityIconWidget>("ARROW");
				arrow.GetIcon = () => index < factors.Length ? (factors[index].Value >= 0 ? "ui_up" : "ui_down") : null;
				row.Get<LabelWidget>("NAME").GetText = () => index < factors.Length ? CityUi.FactorName(factors[index].Key) : "";
				var value = row.Get<LabelWidget>("VALUE");
				value.Bounds.Width = row.Bounds.Width - 3;
				value.GetText = () => index < factors.Length ? CityUi.SignedNumber(factors[index].Value) : "";
				value.GetColor = () => index < factors.Length && factors[index].Value < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;
			}
		}
	}
}
