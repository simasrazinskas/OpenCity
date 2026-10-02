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

using System.Collections.Generic;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Chirper feed (design/iso/ui/panels/chirper-feed.png): the newest messages of the city (IChirperSource), each with an
	/// avatar, its author, age, the text (fluent key plus argument), the likes and a locate button that centres the camera on
	/// the event. A category filter (dropdown plus three quick buttons) narrows the feed to citizens, city services or alerts.
	/// </summary>
	public class CityChirperLogic : ChromeLogic
	{
		[FluentReference]
		const string CityHall = "label-chirper-cityhall";

		[FluentReference("age")]
		const string Ago = "label-chirper-ago";

		[FluentReference]
		const string FilterAll = "label-chirper-filter-all";

		[FluentReference]
		const string FilterCitizens = "label-chirper-filter-citizens";

		[FluentReference]
		const string FilterServices = "label-chirper-filter-services";

		[FluentReference]
		const string FilterAlerts = "label-chirper-filter-alerts";

		[FluentReference("count")]
		internal const string AgeMinutes = "label-people-age-min";

		[FluentReference("count")]
		internal const string AgeHours = "label-people-age-hour";

		[FluentReference("count")]
		internal const string AgeDays = "label-people-age-day";

		[FluentReference]
		internal const string AgeNow = "label-people-age-now";

		[FluentReference]
		const string LocateTip = "button-chirper-locate";

		const int MaxEntries = 40;
		const int DesignHeight = 384;
		const int MinHeight = 160;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly ScrollPanelWidget feed;
		readonly Widget empty;

		ChirpCategory? filter;
		int shownVersion = -1;
		int shownCount = -1;
		bool rebuild;

		[ObjectCreator.UseCtor]
		public CityChirperLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;

			feed = widget.Get<ScrollPanelWidget>("FEED");
			empty = widget.Get("EMPTY");
			empty.IsVisible = () => feed.Children.Count == 0;

			var all = FluentProvider.GetMessage(FilterAll);
			var names = new Dictionary<ChirpCategory, string>
			{
				[ChirpCategory.Citizens] = FluentProvider.GetMessage(FilterCitizens),
				[ChirpCategory.Services] = FluentProvider.GetMessage(FilterServices),
				[ChirpCategory.Alerts] = FluentProvider.GetMessage(FilterAlerts),
			};

			var dropdown = widget.Get<DropDownButtonWidget>("FILTER");
			dropdown.GetText = () => filter == null ? all : names[filter.Value];
			var options = new List<ChirpCategory?> { null, ChirpCategory.Citizens, ChirpCategory.Services, ChirpCategory.Alerts };
			dropdown.OnClick = () => CityDropDown.Show(dropdown, options, o => o == null ? all : names[o.Value], o => filter == o, SetFilter);

			Quick(widget.Get<ButtonWidget>("FILTER_SERVICES"), ChirpCategory.Services, names[ChirpCategory.Services]);
			Quick(widget.Get<ButtonWidget>("FILTER_CITIZENS"), ChirpCategory.Citizens, names[ChirpCategory.Citizens]);
			Quick(widget.Get<ButtonWidget>("FILTER_ALERTS"), ChirpCategory.Alerts, names[ChirpCategory.Alerts]);

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
				{
					if (CityPeopleFeed.FitHeight(panel, DesignHeight, MinHeight))
						feed.Bounds.Height = panel.Bounds.Height - 49;

					Refresh();
				}

				return visible;
			};
		}

		void SetFilter(ChirpCategory? category)
		{
			filter = category;
			rebuild = true;
		}

		/// <summary>A quick button toggles one category (a second click shows everything again).</summary>
		void Quick(ButtonWidget button, ChirpCategory category, string tooltip)
		{
			button.IsHighlighted = () => filter == category;
			button.GetTooltipText = () => tooltip;
			button.OnClick = () => SetFilter(filter == category ? null : category);
		}

		void Refresh()
		{
			var source = ctx.Chirper;
			if (source == null)
				return;

			if (!rebuild && source.Version == shownVersion && source.Entries.Count == shownCount)
				return;

			rebuild = false;
			shownVersion = source.Version;
			shownCount = source.Entries.Count;
			feed.RemoveChildren();

			// Newest first, at most MaxEntries of the chosen category.
			var entries = source.Entries;
			var added = 0;
			for (var i = entries.Count - 1; i >= 0 && added < MaxEntries; i--)
			{
				if (filter != null && CityPeopleFeed.CategoryOf(entries[i]) != filter)
					continue;

				AddItem(entries[i]);
				added++;
			}

			feed.Layout.AdjustChildren();
			feed.ScrollToTop();
		}

		void AddItem(ChirpEntry entry)
		{
			var item = Game.LoadWidget(world, "CITY_CHIRP_TEMPLATE", feed, []);
			item.Bounds.Width = feed.Bounds.Width - feed.ScrollbarWidth - 2;
			var width = item.Bounds.Width;
			var (icon, ramp) = CityPeopleFeed.ChirpLook(entry);

			var avatar = item.Get<BackgroundWidget>("AVATAR");
			avatar.Background = "chip-" + ramp;
			item.Get<CityIconWidget>("ICON").Icon = icon;

			var author = item.Get<LabelWidget>("AUTHOR");
			author.Bounds.Width = width - 38 - 66;
			var name = AuthorName(entry);
			author.GetText = CityUi.Fitted(author, () => name);
			author.GetColor = () => CityTheme.Ramp("purple", 2);

			var age = item.Get<LabelWidget>("AGE");
			age.Bounds.X = width - 66;
			var tick = entry.Tick;
			var lastText = "";
			var lastTick = -1;
			age.GetText = () =>
			{
				if (world.WorldTick != lastTick)
				{
					lastTick = world.WorldTick;
					lastText = FluentProvider.GetMessage(Ago, "age", CityPeopleFeed.Age(world, tick));
				}

				return lastText;
			};
			age.GetColor = () => CityTheme.Muted("people");

			var text = item.Get<LabelWidget>("TEXT");
			text.Bounds.Width = width - 38 - 6;
			var message = Message(entry);
			text.GetText = () => message;
			var font = Game.Renderer.Fonts[text.Font];
			var textHeight = font.Measure(WidgetUtils.WrapText(message, text.Bounds.Width, font)).Y + 1;
			text.Bounds.Height = textHeight;

			var likes = item.Get<LabelWidget>("LIKES");
			var likeIcon = item.Get<CityIconWidget>("LIKE_ICON");
			likes.Bounds.Y = likeIcon.Bounds.Y = text.Bounds.Y + textHeight + 2;
			likes.IsVisible = likeIcon.IsVisible = () => entry.Likes > 0;
			var likeText = entry.Likes.ToString(System.Globalization.CultureInfo.CurrentCulture);
			likes.GetText = () => likeText;
			likes.GetColor = () => entry.Likes > 50 ? CityTheme.MoneyNegative : CityTheme.Ink;
			likes.Font = entry.Likes > 50 ? "TinyBold" : "Tiny";

			var locate = item.Get<ButtonWidget>("LOCATE");
			locate.Bounds.X = width - 22;
			locate.Bounds.Y = likes.Bounds.Y;
			locate.IsVisible = () => entry.HasCell || entry.CitizenId != 0;
			var locateTip = FluentProvider.GetMessage(LocateTip);
			locate.GetTooltipText = () => locateTip;
			locate.OnClick = () =>
			{
				if (entry.CitizenId != 0)
					ctx.SelectedCitizen = entry.CitizenId;

				if (entry.HasCell)
					ctx.CenterOn?.Invoke(entry.Cell);
			};

			item.Bounds.Height = likes.Bounds.Y + 18 + 3;
			var line = item.Get<BackgroundWidget>("LINE");
			line.Bounds.Y = item.Bounds.Height - 2;
			line.Bounds.Width = width;
			var background = item.Get<CityRowBackgroundWidget>("BG");
			background.Bounds.Width = width;
			background.Bounds.Height = item.Bounds.Height;
		}

		string AuthorName(ChirpEntry entry) { return AuthorName(ctx, entry); }

		/// <summary>The display name of a chirp's author: the citizen, the given author or the city hall.</summary>
		public static string AuthorName(CityUiContext ctx, ChirpEntry entry)
		{
			if (entry.CitizenId != 0 && ctx.Citizens != null && ctx.Citizens.TryGetCitizen(entry.CitizenId, out var citizen))
				return citizen.Name;

			return string.IsNullOrEmpty(entry.Author) ? FluentProvider.GetMessage(CityHall) : entry.Author;
		}

		public static string Message(ChirpEntry entry)
		{
			if (FluentProvider.TryGetMessage(entry.Key, out var text, "arg", entry.Arg ?? ""))
				return text;

			return CityUi.Prettify(entry.Key) + (string.IsNullOrEmpty(entry.Arg) ? "" : " " + entry.Arg);
		}
	}
}
