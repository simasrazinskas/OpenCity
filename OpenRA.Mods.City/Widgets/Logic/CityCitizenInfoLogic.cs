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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Citizen card (ICitizenPopulation.TryGetCitizen): name, age, education, job or school, home, activity, happiness
	/// (hover for the factors), health and cash, with buttons that centre the camera on the home or the workplace.
	/// </summary>
	public class CityCitizenInfoLogic : ChromeLogic
	{
		[FluentReference("age", "years")]
		const string AgeLine = "label-citizen-age-line";

		[FluentReference]
		const string RowEducation = "label-citizen-education";

		[FluentReference]
		const string RowJob = "label-citizen-job";

		[FluentReference]
		const string RowSchool = "label-citizen-school";

		[FluentReference]
		const string RowHome = "label-citizen-home";

		[FluentReference]
		const string RowActivity = "label-citizen-activity";

		[FluentReference]
		const string RowHappiness = "label-citizen-happiness";

		[FluentReference]
		const string RowHealth = "label-citizen-health";

		[FluentReference]
		const string RowCash = "label-citizen-cash";

		[FluentReference]
		const string Unemployed = "label-citizen-unemployed";

		[FluentReference]
		const string Retired = "label-citizen-retired";

		[FluentReference]
		const string Homeless = "label-citizen-homeless";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		[FluentReference("value")]
		const string HappinessTitle = "label-building-happiness-title";

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly LabelWidget title;
		readonly LabelWidget subtitle;
		readonly Widget rowContainer;
		readonly ButtonWidget homeButton;
		readonly ButtonWidget workButton;
		readonly List<(LabelWidget Name, LabelWidget Value, Widget Row)> rows = [];

		CitizenView citizen;
		bool valid;
		bool following;
		int followedProperty = -1;
		int lastCitizen;

		[ObjectCreator.UseCtor]
		public CityCitizenInfoLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;

			title = widget.Get<LabelWidget>("TITLE");
			title.GetText = () => valid ? citizen.Name : "";
			subtitle = widget.Get<LabelWidget>("SUBTITLE");
			subtitle.GetText = () => valid ?
				FluentProvider.GetMessage(AgeLine, "age", CityUi.AgeName(citizen.AgeGroup), "years", citizen.Age) : "";

			rowContainer = widget.Get("ROWS");
			widget.Get<ButtonWidget>("CLOSE").OnClick = () => ctx.SelectedCitizen = 0;

			homeButton = widget.Get<ButtonWidget>("HOME");
			homeButton.IsDisabled = () => !valid || citizen.HomeProperty == 0;
			homeButton.OnClick = () => Locate(citizen.HomeProperty);
			workButton = widget.Get<ButtonWidget>("WORK");
			workButton.IsDisabled = () => !valid || citizen.WorkProperty == 0;
			workButton.OnClick = () => Locate(citizen.WorkProperty);

			// Follow keeps the camera on the building the citizen is in (home, or the workplace while working or studying).
			var followButton = widget.Get<ButtonWidget>("FOLLOW");
			followButton.IsDisabled = () => !valid;
			followButton.IsHighlighted = () => following;
			followButton.OnClick = () =>
			{
				following = !following;
				followedProperty = -1;
				ctx.FollowTarget = following ? LivePosition() : null;
			};

			AddRow(RowEducation, () => CityUi.EducationName(citizen.Education), () => Color.White);
			AddRow(RowJob, WorkText, () => Color.White);
			AddRow(RowHome, HomeText, () => Color.White);
			AddRow(RowActivity, () => CityUi.ActivityName(citizen.Activity), () => Color.White);
			var (_, _, happinessRow) = AddRow(RowHappiness, () => FluentProvider.GetMessage(Percent, "value", citizen.Happiness),
				() => CityUi.PercentColor(citizen.Happiness));

			AddRow(RowHealth, () => FluentProvider.GetMessage(Percent, "value", citizen.Health), () => CityUi.PercentColor(citizen.Health));
			AddRow(RowCash, () => CityUtils.FormatMoney(citizen.HouseholdCash), () => citizen.HouseholdCash < 0 ? CityUi.Bad : Color.White);

			var hover = new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				Bounds = new WidgetBounds(0, 0, happinessRow.Bounds.Width, happinessRow.Bounds.Height),
				GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", citizen.Happiness),
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};

			happinessRow.AddChild(hover);

			panel.IsVisible = () =>
			{
				Update();
				return valid;
			};
		}

		(LabelWidget Name, LabelWidget Value, Widget Row) AddRow(string labelKey, Func<string> value, Func<Color> color)
		{
			var row = Game.LoadWidget(world, "CITY_INFO_ROW", rowContainer, []);
			row.Bounds.Y = rows.Count * 22;
			var key = labelKey;
			var name = row.Get<LabelWidget>("NAME");

			// The job row reads "School" for children and teens.
			name.GetText = () => key == RowJob && valid && citizen.AgeGroup < AgeGroup.Adult ?
				FluentProvider.GetMessage(RowSchool) : FluentProvider.GetMessage(key);

			var label = row.Get<LabelWidget>("VALUE");
			label.GetText = value;
			label.GetColor = color;
			var entry = (name, label, row);
			rows.Add(entry);
			return entry;
		}

		string WorkText()
		{
			if (citizen.WorkProperty != 0)
				return CityUi.PropertyName(ctx.Properties?.Get(citizen.WorkProperty));

			if (citizen.AgeGroup == AgeGroup.Senior)
				return FluentProvider.GetMessage(Retired);

			return citizen.AgeGroup >= AgeGroup.Adult ? FluentProvider.GetMessage(Unemployed) : "-";
		}

		string HomeText()
		{
			return citizen.HomeProperty != 0 ? CityUi.PropertyName(ctx.Properties?.Get(citizen.HomeProperty)) : FluentProvider.GetMessage(Homeless);
		}

		void Update()
		{
			valid = false;
			if (ctx.SelectedCitizen == 0 || ctx.Citizens == null)
			{
				if (following)
					ctx.FollowTarget = null;

				following = false;
				return;
			}

			if (!ctx.Citizens.TryGetCitizen(ctx.SelectedCitizen, out citizen) || citizen.Activity == CitizenActivity.Dead)
			{
				ctx.SelectedCitizen = 0;
				return;
			}

			valid = true;
			if (citizen.Id != lastCitizen)
			{
				lastCitizen = citizen.Id;
				followedProperty = -1;
				if (following)
				{
					following = false;
					ctx.FollowTarget = null;
				}
			}

			// With a position source the HUD keeps the camera on the citizen every frame; without one the camera visits the home or workplace.
			if (following && ctx.Follow == null)
				Follow();
		}

		/// <summary>The citizen's live position from the traffic or citizen simulation (IFollowSource), or null when it has none.</summary>
		Func<WPos?> LivePosition()
		{
			if (ctx.Follow == null)
				return null;

			var id = ctx.SelectedCitizen;
			return () => ctx.Follow.TryGetCitizenPosition(id, out var position) ? position : null;
		}

		void Follow()
		{
			var at = citizen.Activity == CitizenActivity.Working || citizen.Activity == CitizenActivity.Studying ? citizen.WorkProperty : citizen.HomeProperty;
			if (at == 0 || at == followedProperty)
				return;

			followedProperty = at;
			var property = ctx.Properties?.Get(at);
			if (property != null)
				ctx.CenterOn?.Invoke(property.Origin + new CVec(Math.Max(1, property.Width) / 2, Math.Max(1, property.Depth) / 2));
		}

		void Locate(int propertyId)
		{
			var property = ctx.Properties?.Get(propertyId);
			if (property == null)
				return;

			ctx.CenterOn?.Invoke(property.Origin + new CVec(Math.Max(1, property.Width) / 2, Math.Max(1, property.Depth) / 2));
			if (property.Actor != null && !property.Actor.IsDead && property.Actor.IsInWorld)
				world.Selection.Combine(world, [property.Actor], false, true);
		}
	}
}
