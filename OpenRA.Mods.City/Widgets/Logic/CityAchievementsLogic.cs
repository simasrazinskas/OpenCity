#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>Achievements panel (PRG): every achievement with its progress bar, the months streak for timed ones and XP reward.</summary>
	public class CityAchievementsLogic : ChromeLogic
	{
		[FluentReference("count", "total")]
		const string Summary = "label-achievements-summary";

		[FluentReference("streak", "months")]
		const string StreakLabel = "label-achievements-streak";

		[FluentReference("xp")]
		const string XpLabel = "label-achievements-xp";

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget list;
		int builtCount = -1;

		[ObjectCreator.UseCtor]
		public CityAchievementsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			list = widget.Get<ScrollPanelWidget>("LIST");
			widget.Get<LabelWidget>("SUMMARY").GetText = () => ctx.Achievements == null ? "" :
				FluentProvider.GetMessage(Summary, "count", ctx.Achievements.Unlocked, "total", ctx.Achievements.Entries.Count);

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Build();

				return visible;
			};
		}

		AchievementEntry Find(string id)
		{
			foreach (var e in ctx.Achievements.Entries)
				if (e.Id == id)
					return e;

			return default;
		}

		void Build()
		{
			var source = ctx.Achievements;
			if (source == null || builtCount == source.Entries.Count)
				return;

			builtCount = source.Entries.Count;
			list.RemoveChildren();
			foreach (var entry in source.Entries)
				AddRow(entry.Id);

			list.Layout.AdjustChildren();
		}

		void AddRow(string id)
		{
			var first = Find(id);
			var row = Game.LoadWidget(world, "CITY_ACHIEVEMENT_ROW", list, []);
			row.Bounds.Width = list.Bounds.Width - list.ScrollbarWidth - 6;

			var name = CityUi.Message(first.NameKey);
			var nameLabel = row.Get<LabelWidget>("NAME");
			nameLabel.GetText = () => name;
			nameLabel.GetColor = () => Find(id).Unlocked ? CityUi.Good : Primitives.Color.White;

			var description = CityUi.Message(first.DescKey, "");
			var descriptionLabel = row.Get<LabelWidget>("DESCRIPTION");
			descriptionLabel.GetText = CityUi.Fitted(descriptionLabel, () =>
			{
				var entry = Find(id);
				var text = description;
				if (entry.Months > 0 && !entry.Unlocked)
					text += "  " + FluentProvider.GetMessage(StreakLabel, "streak", entry.Streak, "months", entry.Months);

				return text;
			});

			var bar = row.Get<CityBarWidget>("PROGRESS");
			bar.Bounds.X = row.Bounds.Width - 140;
			bar.GetPercentage = () => Find(id).Progress;
			bar.GetBarColor = () => Find(id).Unlocked ? CityUi.Good : CityUi.Accent;

			var xp = row.Get<LabelWidget>("XP");
			xp.Bounds.X = row.Bounds.Width - 140;
			xp.GetText = () => first.Xp > 0 ? FluentProvider.GetMessage(XpLabel, "xp", first.Xp) : "";

			var icon = row.Get<ImageWidget>("ICON");
			icon.GetImageName = () => Find(id).Unlocked ? "achievements" : "lock";
		}
	}
}
