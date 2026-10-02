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

using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The Occupants page: the residents or workers of the building, one row per citizen (click opens the citizen panel).</summary>
	public partial class CityBuildingInfoLogic
	{
		[FluentReference]
		const string TabResidents = "label-building-tab-residents";

		[FluentReference]
		const string TabWorkers = "label-building-tab-workers";

		const int MaxListed = 60;

		Widget occupantsPage;
		Widget occupantTabs;
		ScrollPanelWidget occupantList;
		Widget occupantsEmpty;
		CityHeaderWidget occupantsHeader;
		ButtonWidget tabResidents;
		ButtonWidget tabWorkers;
		bool showWorkers;
		string occupantsSignature;

		void InitOccupants()
		{
			occupantsPage = pages[(int)Page.Occupants];
			occupantTabs = occupantsPage.Get("LIST_TABS");
			occupantList = occupantsPage.Get<ScrollPanelWidget>("LIST");
			occupantsEmpty = occupantsPage.Get("EMPTY");
			occupantsEmpty.IsVisible = () => occupantList.Children.Count == 0;
			occupantsHeader = occupantsPage.Get<CityHeaderWidget>("LIST_HEADER");
			occupantsHeader.GetText = () => FluentProvider.GetMessage(showWorkers ? TabWorkers : TabResidents);
			tabResidents = AddOccupantTab(0, TabResidents, () => !showWorkers, () => showWorkers = false);
			tabWorkers = AddOccupantTab(84, TabWorkers, () => showWorkers, () => showWorkers = true);
		}

		ButtonWidget AddOccupantTab(int x, string key, System.Func<bool> highlighted, System.Action onClick)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", occupantTabs, []) as ButtonWidget;
			button.Bounds.X = x;
			button.Bounds.Width = 80;
			button.Bounds.Height = 14;
			var text = FluentProvider.GetMessage(key);
			button.GetText = () => text;
			button.IsHighlighted = highlighted;
			button.OnClick = () =>
			{
				onClick();
				occupantsSignature = null;
			};

			return button;
		}

		bool HasResidents => property != null && (property.Residents > 0 || property.HouseholdSlots > 0);

		bool HasWorkers => property != null && property.TotalJobSlots > 0;

		bool HasOccupants() { return building != null && ctx.Citizens != null && (HasResidents || HasWorkers); }

		void RefreshOccupants()
		{
			if (showWorkers && !HasWorkers)
				showWorkers = false;
			else if (!showWorkers && !HasResidents)
				showWorkers = true;

			var both = HasResidents && HasWorkers;
			tabResidents.Visible = both;
			tabWorkers.Visible = both;
			occupantsHeader.Visible = !both;

			var signature = property.Id + ":" + showWorkers + ":" + property.Residents + ":" + property.TotalJobsFilled;
			if (signature == occupantsSignature)
				return;

			occupantsSignature = signature;
			occupantList.RemoveChildren();
			var ids = showWorkers ? ctx.Citizens.WorkersOf(property.Id) : ctx.Citizens.ResidentsOf(property.Id);
			var width = occupantList.Bounds.Width - occupantList.ScrollbarWidth - 2;
			var count = 0;
			foreach (var id in ids)
			{
				if (count++ >= MaxListed)
					break;

				if (!ctx.Citizens.TryGetCitizen(id, out var view))
					continue;

				var citizenId = id;
				var row = Game.LoadWidget(world, "CITY_BUILDING_CITIZEN", occupantList, []);
				row.Bounds.Width = width;
				var background = row.Get<CityRowBackgroundWidget>("BG");
				background.IsSelected = () => ctx.SelectedCitizen == citizenId;

				void Text(string childId, string text)
				{
					var label = row.Get<LabelWidget>(childId);
					label.GetText = CityUi.Fitted(label, () => text);
					label.GetColor = () => background.Selected ? CityTheme.InkLight : CityTheme.Ink;
				}

				Text("NAME", view.Name);
				Text("AGE", CityUi.AgeName(view.AgeGroup));
				Text("EDUCATION", CityUi.EducationName(view.Education));
				var edu = row.Get<LabelWidget>("EDUCATION");
				edu.Bounds.Width = width - edu.Bounds.X - 2;
				row.Get<ButtonWidget>("SELECT").OnClick = () => ctx.SelectedCitizen = citizenId;
			}

			occupantList.Layout.AdjustChildren();
			occupantList.ScrollToTop();
		}
	}
}
