#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Citizen inspector (ICitizenPopulation.TryGetCitizen), design/iso/ui/panels/citizen-*.png, three tabs. Overview: name, age,
	/// education, activity, happiness (hover for the factors), health, cash, home and workplace with locate buttons, and what is
	/// on the citizen's mind (derived from the citizen's state). Household: the members of the household, rent and savings.
	/// Life: the known facts of the citizen (the simulation keeps no event history, so there is no timeline).
	/// </summary>
	public class CityCitizenInfoLogic : ChromeLogic
	{
		[FluentReference("age", "years")]
		const string AgeLine = "label-citizen-age-line";

		[FluentReference]
		const string Unemployed = "label-citizen-unemployed";

		[FluentReference]
		const string Retired = "label-citizen-retired";

		[FluentReference]
		const string Homeless = "label-citizen-homeless";

		[FluentReference]
		const string AtSchool = "label-citizen-school";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		[FluentReference("value")]
		const string HappinessTitle = "label-building-happiness-title";

		[FluentReference]
		const string TabOverview = "label-citizen-tab-overview";

		[FluentReference]
		const string TabHousehold = "label-citizen-tab-household";

		[FluentReference]
		const string TabLife = "label-citizen-tab-life";

		[FluentReference]
		const string WorkColon = "label-citizen-work-colon";

		[FluentReference]
		const string SchoolColon = "label-citizen-school-colon";

		[FluentReference]
		const string RoleSelf = "label-citizen-role-self";

		[FluentReference("home")]
		const string HouseholdTitle = "label-citizen-household-title";

		[FluentReference("home", "count")]
		const string HomeLine = "label-citizen-home-line";

		[FluentReference]
		const string LocateTip = "button-chirper-locate";

		[FluentReference]
		const string FollowTip = "button-citizen-follow";

		[FluentReference]
		const string ThoughtJobless = "label-citizen-thought-jobless";

		[FluentReference]
		const string ThoughtHomeless = "label-citizen-thought-homeless";

		[FluentReference]
		const string ThoughtSick = "label-citizen-thought-sick";

		[FluentReference]
		const string ThoughtBroke = "label-citizen-thought-broke";

		[FluentReference]
		const string ThoughtUnhappy = "label-citizen-thought-unhappy";

		[FluentReference]
		const string ThoughtHappy = "label-citizen-thought-happy";

		[FluentReference]
		const string ThoughtWorking = "label-citizen-thought-working";

		[FluentReference]
		const string ThoughtStudying = "label-citizen-thought-studying";

		[FluentReference]
		const string ThoughtNothing = "label-citizen-thought-nothing";

		[FluentReference]
		const string FactAge = "label-citizen-fact-age";

		[FluentReference]
		const string FactEducation = "label-citizen-fact-education";

		[FluentReference]
		const string FactHome = "label-citizen-fact-home";

		[FluentReference]
		const string FactWork = "label-citizen-fact-work";

		[FluentReference]
		const string FactSchool = "label-citizen-fact-school";

		[FluentReference]
		const string FactActivity = "label-citizen-fact-activity";

		[FluentReference]
		const string FactCondition = "label-citizen-fact-condition";

		[FluentReference("happy", "health")]
		const string FactWellbeing = "label-citizen-fact-wellbeing";

		const int ThoughtRows = 3;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly CityPanelWidget window;
		readonly Widget[] pages;
		readonly ScrollPanelWidget members;
		readonly List<(string Icon, string Text)> thoughts = [];
		readonly List<CitizenView> household = [];
		readonly StringBuilder signature = new();

		CitizenView citizen;
		bool valid;
		bool following;
		int followedProperty = -1;
		int lastCitizen;
		int page;
		int thoughtsTick = -1;
		string builtMembers;

		[ObjectCreator.UseCtor]
		public CityCitizenInfoLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;
			window = (CityPanelWidget)widget;
			window.GetTitle = () => valid ? citizen.Name : "";
			window.OnClose = () => ctx.SelectedCitizen = 0;
			pages = [widget.Get("PAGE_OVERVIEW"), widget.Get("PAGE_HOUSEHOLD"), widget.Get("PAGE_LIFE")];

			var tabs = new (string Icon, string Key)[] { ("pnl_citizen", TabOverview), ("stat_households", TabHousehold), ("time_calendar", TabLife) };
			window.SetTabs(tabs.Select((t, i) =>
			{
				var text = FluentProvider.GetMessage(t.Key);
				return new CityWindowTab { Icon = t.Icon, GetTooltip = () => text, IsActive = () => page == i, OnClick = () => ShowPage(i) };
			}));

			members = pages[1].Get<ScrollPanelWidget>("MEMBERS");
			InitOverview(pages[0]);
			InitHousehold(pages[1]);
			InitLife(pages[2]);
			ShowPage(0);

			panel.IsVisible = () =>
			{
				Update();
				return valid;
			};
		}

		void ShowPage(int index)
		{
			page = index;
			for (var i = 0; i < pages.Length; i++)
				pages[i].Visible = i == index;
		}

		// ---- overview -------------------------------------------------------------------------------------
		void InitOverview(Widget overview)
		{
			overview.Get<LabelWidget>("NAME").GetText = () => valid ? citizen.Name : "";
			overview.Get<LabelWidget>("NAME").GetColor = () => CityTheme.FamilyShade("people", 1);

			overview.Get<LabelWidget>("AGE").GetText = () => valid ?
				FluentProvider.GetMessage(AgeLine, "age", CityUi.AgeName(citizen.AgeGroup), "years", citizen.Age) : "";
			overview.Get<LabelWidget>("EDUCATION").GetText = () => valid ? CityUi.EducationName(citizen.Education) : "";
			var activity = overview.Get<LabelWidget>("ACTIVITY");
			activity.GetText = () => valid ? CityUi.ActivityName(citizen.Activity) : "";
			activity.GetColor = () => CityTheme.Ramp("blue", 2);
			overview.Get<CityIconWidget>("ACTIVITY_ICON").GetIcon = () => valid ? ActivityIcon(citizen.Activity) : null;

			// Follow keeps the camera on the building the citizen is in (home, or the workplace while working or studying).
			var follow = overview.Get<ButtonWidget>("FOLLOW");
			var followTip = FluentProvider.GetMessage(FollowTip);
			follow.GetTooltipText = () => followTip;
			follow.IsDisabled = () => !valid;
			follow.IsHighlighted = () => following;
			follow.OnClick = () =>
			{
				following = !following;
				followedProperty = -1;
				ctx.FollowTarget = following ? LivePosition() : null;
			};

			var happy = overview.Get<CityBarWidget>("HAPPY_BAR");
			happy.GetPercentage = () => valid ? citizen.Happiness : 0;
			var happyValue = overview.Get<LabelWidget>("HAPPY");
			happyValue.GetText = () => valid ? FluentProvider.GetMessage(Percent, "value", citizen.Happiness) : "";
			var health = overview.Get<CityBarWidget>("HEALTH_BAR");
			health.GetPercentage = () => valid ? citizen.Health : 0;
			overview.Get<LabelWidget>("HEALTH").GetText = () => valid ? FluentProvider.GetMessage(Percent, "value", citizen.Health) : "";

			// Hovering the happiness line explains what drives happiness in the city.
			var hover = new FactorHoverWidget
			{
				TooltipContainer = "TOOLTIP_CONTAINER",
				Bounds = new WidgetBounds(110, 55, overview.Bounds.Width - 110, 11),
				GetTitle = () => FluentProvider.GetMessage(HappinessTitle, "value", citizen.Happiness),
				GetFactors = () => ctx.Citizens?.HappinessFactors
			};
			overview.AddChild(hover);

			var cash = overview.Get<LabelWidget>("CASH");
			cash.GetText = () => valid ? CityUtils.FormatMoney(citizen.HouseholdCash) : "";
			cash.GetColor = () => citizen.HouseholdCash < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;

			var home = overview.Get("HOME_ROW");
			home.Get<LabelWidget>("VALUE").GetText = CityUi.Fitted(home.Get<LabelWidget>("VALUE"), () => valid ? HomeText() : "");
			var locateTip = FluentProvider.GetMessage(LocateTip);
			var homeLocate = home.Get<ButtonWidget>("LOCATE");
			homeLocate.GetTooltipText = () => locateTip;
			homeLocate.IsDisabled = () => !valid || citizen.HomeProperty == 0;
			homeLocate.OnClick = () => Locate(citizen.HomeProperty);

			var work = overview.Get("WORK_ROW");
			var workColon = FluentProvider.GetMessage(WorkColon);
			var schoolColon = FluentProvider.GetMessage(SchoolColon);
			work.Get<LabelWidget>("LABEL").GetText = () => valid && citizen.AgeGroup < AgeGroup.Adult ? schoolColon : workColon;
			work.Get<CityIconWidget>("ICON").GetIcon = () => valid && citizen.AgeGroup < AgeGroup.Adult ? "cat_education" : "stat_work";
			work.Get<LabelWidget>("VALUE").GetText = CityUi.Fitted(work.Get<LabelWidget>("VALUE"), () => valid ? WorkText() : "");
			var workLocate = work.Get<ButtonWidget>("LOCATE");
			workLocate.GetTooltipText = () => locateTip;
			workLocate.IsDisabled = () => !valid || citizen.WorkProperty == 0;
			workLocate.OnClick = () => Locate(citizen.WorkProperty);
			foreach (var row in new[] { home, work })
				row.Get<LabelWidget>("LABEL").GetColor = () => CityTheme.Muted("people");

			var well = overview.Get("THOUGHTS");
			for (var i = 0; i < ThoughtRows; i++)
			{
				var index = i;
				var row = Game.LoadWidget(world, "CITY_CITIZEN_THOUGHT_ROW", well, []);
				row.Bounds.Y = 1 + i * 18;
				row.Bounds.X = 1;
				row.Bounds.Width = well.Bounds.Width - 2;
				row.Get<CityRowBackgroundWidget>("BG").Bounds.Width = row.Bounds.Width;
				row.IsVisible = () => index < Thoughts().Count;
				row.Get<CityIconWidget>("ICON").GetIcon = () => index < Thoughts().Count ? Thoughts()[index].Icon : null;
				var label = row.Get<LabelWidget>("TEXT");
				label.Bounds.Width = row.Bounds.Width - 28;
				label.GetText = CityUi.Fitted(label, () => index < Thoughts().Count ? Thoughts()[index].Text : "");
			}
		}

		static string ActivityIcon(CitizenActivity activity)
		{
			return activity switch
			{
				CitizenActivity.Home => "stat_home",
				CitizenActivity.Working => "stat_work",
				CitizenActivity.Studying => "cat_education",
				CitizenActivity.Shopping => "stat_money",
				CitizenActivity.Leisure => "cat_parks",
				CitizenActivity.Travelling => "tr_car",
				CitizenActivity.Hospital => "cat_health",
				CitizenActivity.Prison => "cat_police",
				CitizenActivity.Moving => "stat_home",
				_ => null,
			};
		}

		/// <summary>What is on the citizen's mind: the most pressing facts of the citizen's state, at most three.</summary>
		List<(string Icon, string Text)> Thoughts()
		{
			if (thoughtsTick == world.WorldTick)
				return thoughts;

			thoughtsTick = world.WorldTick;
			thoughts.Clear();
			if (!valid)
				return thoughts;

			void Add(string icon, string key) { if (thoughts.Count < ThoughtRows) thoughts.Add((icon, FluentProvider.GetMessage(key))); }

			if (citizen.AgeGroup == AgeGroup.Adult && citizen.WorkProperty == 0)
				Add("st_no_workers", ThoughtJobless);

			if (citizen.HomeProperty == 0)
				Add("st_abandoned", ThoughtHomeless);

			if (citizen.Health < 40)
				Add("st_sick", ThoughtSick);

			if (citizen.HouseholdCash < 0)
				Add("stat_expenses", ThoughtBroke);

			if (citizen.Happiness < 40)
				Add("stat_unhappy", ThoughtUnhappy);
			else if (citizen.Happiness >= 70)
				Add("stat_happiness", ThoughtHappy);

			if (citizen.Activity == CitizenActivity.Working)
				Add("stat_work", ThoughtWorking);
			else if (citizen.Activity == CitizenActivity.Studying)
				Add("cat_education", ThoughtStudying);

			if (thoughts.Count == 0)
				Add("stat_neutral", ThoughtNothing);

			return thoughts;
		}

		// ---- household ------------------------------------------------------------------------------------
		void InitHousehold(Widget page)
		{
			var header = page.Get<CityHeaderWidget>("HEADER");
			header.GetText = () => valid ? FluentProvider.GetMessage(HouseholdTitle, "home", HomeText()) : "";

			var rent = page.Get<LabelWidget>("RENT");
			rent.GetText = () => valid ? CityUi.SignedMoney(-MonthlyRent()) : "";
			rent.GetColor = () => MonthlyRent() > 0 ? CityTheme.MoneyNegative : CityTheme.Ink;
			var savings = page.Get<LabelWidget>("SAVINGS");
			savings.GetText = () => valid ? CityUtils.FormatMoney(citizen.HouseholdCash) : "";
			savings.GetColor = () => citizen.HouseholdCash < 0 ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;

			page.Get<CityBarWidget>("HAPPINESS_BAR").GetPercentage = () => household.Count == 0 ? 0 : (int)household.Average(m => m.Happiness);
			var happiness = page.Get<LabelWidget>("HAPPINESS");
			happiness.GetText = () => household.Count == 0 ? "" : FluentProvider.GetMessage(Percent, "value", (int)household.Average(m => m.Happiness));

			var home = page.Get("HOME_ROW");
			var homeLabel = home.Get<LabelWidget>("VALUE");
			homeLabel.GetText = CityUi.Fitted(homeLabel, () => valid ?
				FluentProvider.GetMessage(HomeLine, "home", HomeText(), "count", Math.Max(1, ResidentCount())) : "");
			var homeLocate = home.Get<ButtonWidget>("LOCATE");
			var tip = FluentProvider.GetMessage(LocateTip);
			homeLocate.GetTooltipText = () => tip;
			homeLocate.IsDisabled = () => !valid || citizen.HomeProperty == 0;
			homeLocate.OnClick = () => Locate(citizen.HomeProperty);
		}

		int ResidentCount()
		{
			return citizen.HomeProperty == 0 ? 0 : ctx.Citizens?.ResidentsOf(citizen.HomeProperty).Count() ?? 0;
		}

		/// <summary>The rent of this household: the building's rent shared by its households.</summary>
		int MonthlyRent()
		{
			var property = citizen.HomeProperty == 0 ? null : ctx.Properties?.Get(citizen.HomeProperty);
			return property == null ? 0 : property.RentPerMonth / Math.Max(1, property.Households);
		}

		void RefreshHousehold()
		{
			household.Clear();
			if (!valid || citizen.HomeProperty == 0 || ctx.Citizens == null)
			{
				household.Add(citizen);
			}
			else
			{
				foreach (var id in ctx.Citizens.ResidentsOf(citizen.HomeProperty))
					if (ctx.Citizens.TryGetCitizen(id, out var member) && member.HouseholdId == citizen.HouseholdId)
						household.Add(member);

				if (household.All(m => m.Id != citizen.Id))
					household.Add(citizen);
			}

			household.Sort((a, b) => a.Id == citizen.Id ? -1 : b.Id == citizen.Id ? 1 : b.Age.CompareTo(a.Age));

			signature.Clear();
			signature.Append(citizen.Id);
			foreach (var m in household)
				signature.Append('|').Append(m.Id).Append(m.Age).Append(m.WorkProperty);

			var text = signature.ToString();
			if (text == builtMembers)
				return;

			builtMembers = text;
			members.RemoveChildren();
			foreach (var member in household)
				AddMember(member);

			members.Layout.AdjustChildren();
			members.ScrollToTop();
		}

		void AddMember(CitizenView member)
		{
			var row = Game.LoadWidget(world, "CITY_CITIZEN_MEMBER_ROW", members, []);
			var width = members.Bounds.Width - members.ScrollbarWidth - 2;
			row.Bounds.Width = width;
			var self = member.Id == citizen.Id;
			var background = row.Get<CityRowBackgroundWidget>("BG");
			background.Bounds.Width = width;
			background.IsSelected = () => self;
			row.Get<ButtonWidget>("SELECT").Bounds.Width = width;
			row.Get<ButtonWidget>("SELECT").Background = "";
			row.Get<ButtonWidget>("SELECT").OnClick = () => ctx.SelectedCitizen = member.Id;

			void Text(string id, string text, int? x = null, int? w = null)
			{
				var label = row.Get<LabelWidget>(id);
				label.GetText = () => text;
				label.GetColor = () => self ? CityTheme.InkLight : CityTheme.Ink;
				if (x != null)
					label.Bounds.X = x.Value;

				if (w != null)
					label.Bounds.Width = w.Value;
			}

			Text("NAME", member.Name);
			Text("ROLE", self ? FluentProvider.GetMessage(RoleSelf) : CityUi.AgeName(member.AgeGroup));
			Text("AGE", member.Age.ToString(System.Globalization.CultureInfo.CurrentCulture));
			Text("OCCUPATION", Occupation(member), w: width - 174);
		}

		string Occupation(CitizenView view)
		{
			if (view.WorkProperty != 0)
				return CityUi.PropertyName(ctx.Properties?.Get(view.WorkProperty));

			if (view.AgeGroup == AgeGroup.Senior)
				return FluentProvider.GetMessage(Retired);

			return view.AgeGroup >= AgeGroup.Adult ? FluentProvider.GetMessage(Unemployed) : FluentProvider.GetMessage(AtSchool);
		}

		// ---- life -----------------------------------------------------------------------------------------
		void InitLife(Widget life)
		{
			var well = life.Get("FACTS");
			var rows = new (string Ramp, Func<string> Icon, Func<string> Title, Func<string> Sub)[]
			{
				("yellow", () => "stat_citizen", () => FluentProvider.GetMessage(AgeLine, "age", CityUi.AgeName(citizen.AgeGroup), "years", citizen.Age),
					() => FluentProvider.GetMessage(FactAge)),
				("blue", () => "cat_education", () => CityUi.EducationName(citizen.Education), () => FluentProvider.GetMessage(FactEducation)),
				("green", () => "stat_home", HomeText, () => FluentProvider.GetMessage(FactHome)),
				("orange", () => citizen.AgeGroup < AgeGroup.Adult ? "cat_education" : "stat_work", WorkText,
					() => FluentProvider.GetMessage(citizen.AgeGroup < AgeGroup.Adult ? FactSchool : FactWork)),
				("teal", () => ActivityIcon(citizen.Activity) ?? "stat_neutral", () => CityUi.ActivityName(citizen.Activity),
					() => FluentProvider.GetMessage(FactActivity)),
				("red", () => "stat_happiness", () => FluentProvider.GetMessage(FactWellbeing, "happy", citizen.Happiness, "health", citizen.Health),
					() => FluentProvider.GetMessage(FactCondition)),
			};

			for (var i = 0; i < rows.Length; i++)
			{
				var (ramp, icon, title, sub) = rows[i];
				var row = Game.LoadWidget(world, "CITY_CITIZEN_FACT_ROW", well, []);
				row.Bounds.X = 1;
				row.Bounds.Y = 1 + i * 26;
				row.Bounds.Width = well.Bounds.Width - 2;
				row.Get<CityRowBackgroundWidget>("BG").Bounds.Width = row.Bounds.Width;
				row.Get<BackgroundWidget>("BADGE").Background = "chip-" + ramp;
				row.Get<CityIconWidget>("ICON").GetIcon = icon;
				var titleLabel = row.Get<LabelWidget>("TITLE");
				titleLabel.Bounds.Width = row.Bounds.Width - 34;
				titleLabel.GetText = CityUi.Fitted(titleLabel, () => valid ? title() : "");
				var subLabel = row.Get<LabelWidget>("SUBTITLE");
				subLabel.Bounds.Width = row.Bounds.Width - 34;
				subLabel.GetText = () => valid ? sub() : "";
				subLabel.GetColor = () => CityTheme.Muted("people");
			}
		}

		// ---- data -----------------------------------------------------------------------------------------
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

			if (page == 1)
				RefreshHousehold();
			else
				household.Clear();

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
