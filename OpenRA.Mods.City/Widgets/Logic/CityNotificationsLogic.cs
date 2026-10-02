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
using System.Globalization;
using System.Linq;
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The notification centre (design/iso/ui/panels/notifications-*.png): one list of everything the city wants the mayor to
	/// know, in four tabs. Problems are the city-wide problem summary (ICityProblems), events are wildfires, road accidents,
	/// good news and city chirps, chirps are the citizens' messages. Each notice has a severity stripe, status icon, title, the
	/// district, its age in game time and a locate button. Problems and events that vanish stay in the list as "resolved" until
	/// hidden. Read and cleared state is kept in the UI only (nothing synced); problem ages count from when this window's tracker
	/// first saw them, which runs from the moment the game starts, also while the window is closed.
	/// </summary>
	public class CityNotificationsLogic : ChromeLogic
	{
		[FluentReference("name", "count")]
		const string ProblemCount = "label-notif-problem-count";

		[FluentReference("title")]
		const string ResolvedTitle = "label-notif-resolved";

		[FluentReference("name", "text")]
		const string CitizenText = "label-notif-citizen-text";

		[FluentReference("count")]
		const string UnreadCount = "label-notif-unread";

		[FluentReference("count")]
		const string MessageCount = "label-notif-messages";

		[FluentReference]
		const string TabAll = "label-notif-tab-all";

		[FluentReference]
		const string TabProblems = "label-notif-tab-problems";

		[FluentReference]
		const string TabEvents = "label-notif-tab-events";

		[FluentReference]
		const string TabChirps = "label-notif-tab-chirps";

		[FluentReference]
		const string SeverityAll = "label-notif-severity-all";

		[FluentReference]
		const string SeverityProblem = "label-notif-severity-problem";

		[FluentReference]
		const string SeverityWarning = "label-notif-severity-warning";

		[FluentReference]
		const string SeverityMajor = "label-notif-severity-major";

		[FluentReference]
		const string LocateTip = "button-chirper-locate";

		[FluentReference]
		const string Wildfire = "label-alert-wildfire";

		[FluentReference]
		const string Accident = "label-alert-accident";

		enum Kind { Problem, Event, Chirp }

		sealed class Note
		{
			public string Key;
			public Kind Kind;
			public ProblemTier Tier;
			public string Icon;
			public string Title;
			public string Place;
			public int Tick;
			public bool Resolved;
			public CPos Cell;
			public bool HasCell;
			public uint ActorId;
			public int CitizenId;
		}

		/// <summary>A problem or event seen by the tracker: when it appeared and whether it is gone.</summary>
		sealed class Tracked
		{
			public Note Note;
			public bool Seen;
			public int ResolvedTick;
		}

		const int DesignHeight = 300;
		const int MinHeight = 160;
		const int MaxResolved = 14;
		const int MaxChirps = 60;
		const int RowHeight = 27;
		const int ListTop = 67;
		const int FooterHeight = 26;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly ScrollPanelWidget list;
		readonly Dictionary<string, Tracked> tracks = [];
		readonly HashSet<string> read = [];
		readonly HashSet<string> cleared = [];
		readonly List<Note> notes = [];
		readonly List<Note> shown = [];
		readonly StringBuilder signature = new();

		int tab;
		int severity;
		bool hideResolved;
		int trackedTick = -1;
		int instances;
		int unread;
		int total;
		string builtSignature;
		int sourceHash;
		bool collected;

		[ObjectCreator.UseCtor]
		public CityNotificationsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;
			list = widget.Get<ScrollPanelWidget>("LIST");
			var empty = widget.Get("EMPTY");
			empty.IsVisible = () => shown.Count == 0;

			var window = (CityPanelWidget)widget;
			var tabs = new (string Icon, string Key)[]
			{
				("pnl_notifications", TabAll), ("ui_warning", TabProblems), ("pnl_milestone", TabEvents), ("pnl_chirper", TabChirps)
			};

			window.SetTabs(tabs.Select((t, i) =>
			{
				var text = FluentProvider.GetMessage(t.Key);
				return new CityWindowTab
				{
					Icon = t.Icon,
					GetTooltip = () => text,
					IsActive = () => tab == i,
					OnClick = () =>
					{
						tab = i;
						builtSignature = null;
					}
				};
			}));

			var severities = new[] { SeverityAll, SeverityProblem, SeverityWarning, SeverityMajor }
				.Select(k => FluentProvider.GetMessage(k)).ToArray();
			var dropdown = widget.Get<DropDownButtonWidget>("SEVERITY");
			var levels = new List<int> { 0, 1, 2, 3 };
			dropdown.GetText = () => severities[severity];
			dropdown.OnClick = () => CityDropDown.Show(dropdown, levels, l => severities[l], l => severity == l, l =>
			{
				severity = l;
				builtSignature = null;
			});

			var hide = widget.Get<CheckboxWidget>("HIDE_RESOLVED");
			hide.IsChecked = () => hideResolved;
			hide.OnClick = () =>
			{
				hideResolved = !hideResolved;
				builtSignature = null;
			};

			widget.Get<LabelWidget>("UNREAD").GetText = () => unread == 0 ? "" : FluentProvider.GetMessage(UnreadCount, "count", unread);
			widget.Get<LabelWidget>("COUNT").GetText = () => FluentProvider.GetMessage(MessageCount, "count", total);

			widget.Get<ButtonWidget>("MARK_READ").OnClick = () =>
			{
				foreach (var note in notes)
					read.Add(note.Key);

				builtSignature = null;
			};

			widget.Get<ButtonWidget>("CLEAR_ALL").OnClick = () =>
			{
				foreach (var note in InTab())
					cleared.Add(note.Key);

				builtSignature = null;
			};

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				Track();
				var visible = panelVisible();
				if (visible)
				{
					if (CityPeopleFeed.FitHeight(panel, DesignHeight, MinHeight))
						list.Bounds.Height = panel.Bounds.Height - ListTop - FooterHeight;

					Refresh();
				}

				return visible;
			};
		}

		// ---- data -----------------------------------------------------------------------------------------

		/// <summary>Follows the problem summary, wildfires and accidents (every few ticks), also while the window is closed.</summary>
		void Track()
		{
			if (world.LocalPlayer == null || world.WorldTick == trackedTick || world.WorldTick % 5 != 0)
				return;

			trackedTick = world.WorldTick;
			foreach (var track in tracks.Values)
				track.Seen = false;

			var problems = ctx.Problems;
			if (problems != null)
			{
				foreach (var entry in problems.Summary)
				{
					var good = entry.Tier == ProblemTier.Good;
					var name = CityUi.Message(ProblemCatalog.NameKey(entry.Problem));
					var title = entry.Count > 1 && !good ? FluentProvider.GetMessage(ProblemCount, "name", name, "count", entry.Count) : name;
					Touch("p:" + entry.Problem, new Note
					{
						Kind = good ? Kind.Event : Kind.Problem,
						Tier = entry.Tier,
						Icon = CityAlertsLogic.ProblemIcon(entry.Problem),
						Title = title,
						Place = CityPeopleFeed.PlaceName(ctx, entry.Cell),
						Tick = world.WorldTick,
						Cell = entry.Cell,
						HasCell = entry.Cell != CPos.Zero,
						ActorId = entry.ActorId
					});
				}
			}

			var wildfires = ctx.Get<ServiceSimulation>()?.WildfireCount ?? 0;
			if (wildfires > 0)
			{
				var name = CityUi.Message(Wildfire);
				Touch("e:wildfire", new Note
				{
					Kind = Kind.Event,
					Tier = ProblemTier.Major,
					Icon = "st_wildfire",
					Title = wildfires > 1 ? FluentProvider.GetMessage(ProblemCount, "name", name, "count", wildfires) : name,
					Place = "",
					Tick = world.WorldTick
				});
			}

			var incidents = ctx.Get<ITrafficIncidents>()?.Incidents;
			if (incidents != null)
			{
				foreach (var incident in incidents)
				{
					Touch("e:accident:" + incident.Id.ToString(CultureInfo.InvariantCulture), new Note
					{
						Kind = Kind.Event,
						Tier = ProblemTier.Warning,
						Icon = "st_accident",
						Title = CityUi.Message(Accident),
						Place = CityPeopleFeed.PlaceName(ctx, incident.Cell),
						Tick = incident.StartTick,
						Cell = incident.Cell,
						HasCell = true
					});
				}
			}

			var gone = 0;
			foreach (var track in tracks.Values)
			{
				if (track.Seen)
					continue;

				if (!track.Note.Resolved)
				{
					track.Note.Resolved = true;
					track.ResolvedTick = world.WorldTick;
				}

				gone++;
			}

			// Keep only the newest few resolved notices.
			if (gone > MaxResolved)
			{
				var drop = tracks.Where(t => !t.Value.Seen).OrderBy(t => t.Value.ResolvedTick).Take(gone - MaxResolved).Select(t => t.Key).ToList();
				foreach (var key in drop)
					tracks.Remove(key);
			}
		}

		/// <summary>Records a present notice; one that reappears after being resolved is a new notice (unread again).</summary>
		void Touch(string key, Note fresh)
		{
			if (!tracks.TryGetValue(key, out var track) || track.Note.Resolved)
			{
				track = new Tracked();
				tracks[key] = track;
				fresh.Key = key + "#" + (++instances).ToString(CultureInfo.InvariantCulture);
				track.Note = fresh;
			}
			else
			{
				// The same notice: keep its first-seen tick and key, refresh what can change.
				var note = track.Note;
				note.Tier = fresh.Tier;
				note.Title = fresh.Title;
				note.Place = fresh.Place;
				note.Cell = fresh.Cell;
				note.HasCell = fresh.HasCell;
				note.ActorId = fresh.ActorId;
			}

			track.Seen = true;
		}

		void Collect()
		{
			notes.Clear();
			foreach (var track in tracks.Values)
				notes.Add(track.Note);

			var entries = ctx.Chirper?.Entries;
			if (entries == null)
				return;

			var added = 0;
			for (var i = entries.Count - 1; i >= 0 && added < MaxChirps; i--)
			{
				var entry = entries[i];

				// The city's own "N buildings have a problem" chirps repeat the problem list.
				if ((entry.Key ?? "").StartsWith("chirp-problem-", StringComparison.Ordinal))
					continue;

				var citizen = entry.CitizenId != 0;
				var message = CityChirperLogic.Message(entry);
				var (icon, _) = CityPeopleFeed.ChirpLook(entry);
				notes.Add(new Note
				{
					Key = "c:" + entry.Tick.ToString(CultureInfo.InvariantCulture) + ":" + entry.Key + ":" + entry.CitizenId.ToString(CultureInfo.InvariantCulture),
					Kind = citizen ? Kind.Chirp : Kind.Event,
					Tier = CityPeopleFeed.ChirpTier(entry),
					Icon = icon,
					Title = citizen ? FluentProvider.GetMessage(CitizenText, "name", CityChirperLogic.AuthorName(ctx, entry), "text", message) : message,
					Place = entry.HasCell ? CityPeopleFeed.PlaceName(ctx, entry.Cell) : "",
					Tick = entry.Tick,
					Cell = entry.Cell,
					HasCell = entry.HasCell,
					CitizenId = entry.CitizenId
				});
				added++;
			}
		}

		IEnumerable<Note> InTab()
		{
			return notes.Where(n => !cleared.Contains(n.Key) && (tab == 0 || (int)n.Kind == tab - 1));
		}

		int SourceHash()
		{
			unchecked
			{
				var h = tracks.Count * 31 + instances;
				foreach (var t in tracks.Values)
					h = h * 17 + (t.Note.Resolved ? 1 : 0) + t.Note.Title.GetHashCode(StringComparison.Ordinal) + t.Note.Place.GetHashCode(StringComparison.Ordinal);

				var chirper = ctx.Chirper;
				if (chirper != null)
					h = h * 31 + chirper.Version * 7 + chirper.Entries.Count;

				return h;
			}
		}

		static int SeverityRank(int level) { return level switch { 1 => 2, 2 => 3, 3 => 4, _ => 0 }; }

		void Refresh()
		{
			// The notice list is rebuilt only when a source changed.
			var hash = SourceHash();
			if (!collected || hash != sourceHash)
			{
				collected = true;
				sourceHash = hash;
				Collect();
			}

			shown.Clear();
			total = 0;
			unread = 0;
			foreach (var note in InTab().OrderByDescending(n => n.Tick).ThenBy(n => n.Key, StringComparer.Ordinal))
			{
				total++;
				if (!read.Contains(note.Key))
					unread++;

				if (hideResolved && note.Resolved)
					continue;

				if (CityPeopleFeed.Rank(note.Tier) < SeverityRank(severity))
					continue;

				shown.Add(note);
			}

			signature.Clear();
			signature.Append(tab).Append('/').Append(severity).Append('/').Append(hideResolved).Append('/').Append(list.Bounds.Width);
			foreach (var note in shown)
				signature.Append('|').Append(note.Key).Append(note.Resolved ? 'r' : '-').Append(read.Contains(note.Key) ? 'x' : '-').Append(note.Title).Append(note.Place);

			var text = signature.ToString();
			if (text == builtSignature)
				return;

			builtSignature = text;
			list.RemoveChildren();
			foreach (var note in shown)
				AddRow(note);

			list.Layout.AdjustChildren();
			list.ScrollToTop();
		}

		// ---- rows -----------------------------------------------------------------------------------------
		void AddRow(Note note)
		{
			var row = Game.LoadWidget(world, "CITY_NOTIFICATION_ROW", list, []);
			var width = list.Bounds.Width - list.ScrollbarWidth - 2;
			row.Bounds.Width = width;
			row.Bounds.Height = RowHeight;
			row.Get<CityRowBackgroundWidget>("BG").Bounds.Width = width;

			var unreadNote = !read.Contains(note.Key);
			var color = CityPeopleFeed.TierColor(note.Tier);
			var stripe = row.Get<ColorBlockWidget>("STRIPE");
			stripe.GetColor = () => note.Resolved ? CityTheme.Muted("city") : color;
			var edge = row.Get<ColorBlockWidget>("STRIPE_EDGE");
			var dark = Color.FromArgb(color.R / 2, color.G / 2, color.B / 2);
			edge.GetColor = () => note.Resolved ? CityTheme.FamilyShade("city", 1) : dark;

			var dot = row.Get<ColorBlockWidget>("UNREAD");
			dot.GetColor = () => CityTheme.Ramp("blue", 4);
			dot.IsVisible = () => unreadNote;

			row.Get<CityIconWidget>("ICON").Icon = note.Icon;

			var title = row.Get<LabelWidget>("TITLE");
			title.Bounds.Width = width - 29 - 80;
			var titleText = note.Resolved ? FluentProvider.GetMessage(ResolvedTitle, "title", note.Title) : note.Title;
			title.Font = unreadNote ? "TinyBold" : "Tiny";
			title.GetText = CityUi.Fitted(title, () => titleText);
			title.GetColor = () => note.Resolved ? CityTheme.Muted("city") : CityTheme.Ink;

			if (string.IsNullOrEmpty(note.Place))
				title.Bounds.Y = (RowHeight - title.Bounds.Height) / 2;

			var place = row.Get<LabelWidget>("PLACE");
			place.Bounds.Width = width - 29 - 80;
			var placeText = note.Place;
			place.GetText = CityUi.Fitted(place, () => placeText);
			place.GetColor = () => CityTheme.Muted("city");

			var age = row.Get<LabelWidget>("AGE");
			age.Bounds.X = width - 78;
			var lastTick = -1;
			var lastText = "";
			age.GetText = () =>
			{
				if (world.WorldTick != lastTick)
				{
					lastTick = world.WorldTick;
					lastText = CityPeopleFeed.Age(world, note.Tick);
				}

				return lastText;
			};
			age.GetColor = () => CityTheme.Muted("city");

			var locate = row.Get<ButtonWidget>("LOCATE");
			locate.Bounds.X = width - 24;
			var tip = FluentProvider.GetMessage(LocateTip);
			locate.GetTooltipText = () => tip;
			locate.IsVisible = () => note.HasCell || note.CitizenId != 0;
			locate.OnClick = () =>
			{
				read.Add(note.Key);
				builtSignature = null;
				if (note.CitizenId != 0)
					ctx.SelectedCitizen = note.CitizenId;

				if (note.ActorId != 0)
					ctx.Locate?.Invoke(note.ActorId, note.Cell);
				else if (note.HasCell)
					ctx.CenterOn?.Invoke(note.Cell);
			};
		}
	}
}
