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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityProgressLogic
	{
		[FluentReference("count", "total")]
		const string AchievementSummary = "label-achievements-summary";

		[FluentReference("streak", "months")]
		const string StreakLabel = "label-achievements-streak";

		[FluentReference("xp")]
		const string EarnedLabel = "label-achievements-earned";

		[FluentReference("xp")]
		const string CardXp = "label-achievements-card-xp";

		const int AchievementColumns = 7, AchievementGap = 4, AchievementRowHeight = 66;

		readonly Dictionary<string, AchievementEntry> achievements = [];
		ScrollPanelWidget achievementList;
		string achievementSignature;

		void InitAchievements(Widget page)
		{
			achievementList = page.Get<ScrollPanelWidget>("LIST");

			var summary = page.Get<LabelWidget>("SUMMARY");
			summary.GetText = () => ctx.Achievements == null ? "" :
				FluentProvider.GetMessage(AchievementSummary, "count", ctx.Achievements.Unlocked, "total", ctx.Achievements.Entries.Count);
			summary.GetColor = () => CityTheme.Ramp("yellow", 2);

			var earned = page.Get<LabelWidget>("EARNED");
			earned.GetText = () => ctx.Achievements == null ? "" :
				FluentProvider.GetMessage(EarnedLabel, "xp", Number(ctx.Achievements.Entries.Where(e => e.Unlocked).Sum(e => (long)e.Xp)));
		}

		AchievementEntry Achievement(string id)
		{
			return achievements.TryGetValue(id, out var entry) ? entry : default;
		}

		void RefreshAchievements()
		{
			var source = ctx.Achievements;
			if (source == null)
				return;

			achievements.Clear();
			foreach (var entry in source.Entries)
				achievements[entry.Id] = entry;

			var signature = source.Entries.Count + ":" + achievementList.Bounds.Width;
			if (signature == achievementSignature)
				return;

			achievementSignature = signature;
			achievementList.RemoveChildren();
			var width = achievementList.Bounds.Width - achievementList.ScrollbarWidth - 2;
			var cardWidth = (width - (AchievementColumns - 1) * AchievementGap) / AchievementColumns;
			Widget row = null;
			var i = 0;
			foreach (var entry in source.Entries)
			{
				if (i % AchievementColumns == 0)
				{
					row = new ContainerWidget { Bounds = new WidgetBounds(0, 0, width, AchievementRowHeight) };
					achievementList.AddChild(row);
				}

				MakeAchievementCard(row, entry.Id, i % AchievementColumns * (cardWidth + AchievementGap), cardWidth);
				i++;
			}

			achievementList.Layout.AdjustChildren();
		}

		void MakeAchievementCard(Widget row, string id, int x, int width)
		{
			var first = Achievement(id);
			var card = Game.LoadWidget(world, "CITY_ACHIEVEMENT_CARD", row, []) as LabelWithTooltipWidget;
			card.Bounds.X = x;
			card.Bounds.Width = width;

			// Children were laid out for the template width.
			foreach (var part in new[] { "DONE", "TODO", "XP" })
				card.Get(part).Bounds.Width = width;

			card.Get("NAME").Bounds.Width = width - 4;
			card.Get("PROGRESS").Bounds.Width = width - 10;
			card.Get("PLATE").Bounds.X = (width - 28) / 2;
			card.Get("ICON").Bounds.X = (width - 24) / 2;
			card.Get("DIM").Bounds.X = (width - 24) / 2;
			var name = CityUi.Message(first.NameKey);
			var description = CityUi.Message(first.DescKey, "");
			card.GetTooltipText = () =>
			{
				var entry = Achievement(id);
				var text = name + "\n" + description;
				if (entry.Months > 0 && !entry.Unlocked)
					text += "\n" + FluentProvider.GetMessage(StreakLabel, "streak", entry.Streak, "months", entry.Months);

				return text;
			};

			card.Get("DONE").IsVisible = () => Achievement(id).Unlocked;
			card.Get("TODO").IsVisible = () => !Achievement(id).Unlocked;

			var plate = card.Get<ColorBlockWidget>("PLATE");
			plate.GetColor = () => CityTheme.Ramp("yellow", 4);
			plate.IsVisible = () => Achievement(id).Unlocked;

			var dim = card.Get<CityTintWidget>("DIM");
			dim.IsVisible = () => !Achievement(id).Unlocked;
			dim.GetColor = () => Color.FromArgb(150, CityTheme.FamilyShade(family, 7));

			var label = card.Get<LabelWidget>("NAME");
			label.GetText = () => name;
			label.GetColor = () => Achievement(id).Unlocked ? CityTheme.Ink : CityTheme.Muted(family);

			var xp = card.Get<LabelWidget>("XP");
			xp.IsVisible = () => Achievement(id).Unlocked && first.Xp > 0;
			xp.GetText = () => FluentProvider.GetMessage(CardXp, "xp", first.Xp);
			xp.GetColor = () => CityTheme.MoneyPositive;

			var bar = card.Get<CityBarWidget>("PROGRESS");
			bar.IsVisible = () => !Achievement(id).Unlocked;
			bar.GetPercentage = () => Achievement(id).Progress;
		}
	}
}
