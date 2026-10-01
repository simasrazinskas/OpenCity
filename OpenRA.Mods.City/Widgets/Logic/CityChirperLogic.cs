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

using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Chirper feed: the newest messages of the city (IChirperSource), each with its author, the text (fluent key plus
	/// argument) and a locate button that centres the camera on the event. Clicking a citizen author opens the citizen panel.
	/// </summary>
	public class CityChirperLogic : ChromeLogic
	{
		[FluentReference]
		const string CityHall = "label-chirper-cityhall";

		[FluentReference("name", "likes")]
		const string AuthorLikes = "label-chirper-author-likes";

		const int MaxEntries = 20;

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget feed;

		int shownVersion = -1;
		int shownCount = -1;

		[ObjectCreator.UseCtor]
		public CityChirperLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;

			feed = widget.Get<ScrollPanelWidget>("FEED");

			var empty = widget.Get<LabelWidget>("EMPTY");
			empty.IsVisible = () => ctx.Chirper == null || ctx.Chirper.Entries.Count == 0;

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		void Refresh()
		{
			var source = ctx.Chirper;
			if (source == null)
				return;

			if (source.Version == shownVersion && source.Entries.Count == shownCount)
				return;

			shownVersion = source.Version;
			shownCount = source.Entries.Count;
			feed.RemoveChildren();

			// Newest first, at most MaxEntries.
			var entries = source.Entries;
			for (var i = entries.Count - 1; i >= 0 && i >= entries.Count - MaxEntries; i--)
				AddItem(entries[i]);

			feed.Layout.AdjustChildren();
			feed.ScrollToTop();
		}

		void AddItem(ChirpEntry entry)
		{
			var item = Game.LoadWidget(world, "CITY_CHIRP_TEMPLATE", feed, []);
			item.Bounds.Width = feed.Bounds.Width - feed.ScrollbarWidth - 4;

			var author = item.Get<LabelWidget>("AUTHOR");
			var name = AuthorName(entry);
			var authorText = entry.Likes > 0 ? FluentProvider.GetMessage(AuthorLikes, "name", name, "likes", entry.Likes) : name;
			author.GetText = () => authorText;

			var text = item.Get<LabelWidget>("TEXT");
			text.Bounds.Width = item.Bounds.Width - 40;
			var message = Message(entry);
			text.GetText = () => message;

			var locate = item.Get<ButtonWidget>("LOCATE");
			locate.Bounds.X = item.Bounds.Width - 32;
			locate.IsVisible = () => entry.HasCell || entry.CitizenId != 0;
			locate.OnClick = () =>
			{
				if (entry.CitizenId != 0)
					ctx.SelectedCitizen = entry.CitizenId;

				if (entry.HasCell)
					ctx.CenterOn?.Invoke(entry.Cell);
			};
		}

		string AuthorName(ChirpEntry entry)
		{
			if (entry.CitizenId != 0 && ctx.Citizens != null && ctx.Citizens.TryGetCitizen(entry.CitizenId, out var citizen))
				return citizen.Name;

			return string.IsNullOrEmpty(entry.Author) ? FluentProvider.GetMessage(CityHall) : entry.Author;
		}

		static string Message(ChirpEntry entry)
		{
			if (FluentProvider.TryGetMessage(entry.Key, out var text, "arg", entry.Arg ?? ""))
				return text;

			return CityUi.Prettify(entry.Key) + (string.IsNullOrEmpty(entry.Arg) ? "" : " " + entry.Arg);
		}
	}
}
