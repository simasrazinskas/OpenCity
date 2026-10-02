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

using System.Globalization;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityProgressLogic
	{
		const int UnlockColumns = 2, UnlockChipHeight = 24, UnlockRowStep = 28;

		ScrollPanelWidget milestoneList;
		Widget unlockContainer;
		Widget milestoneLeft;
		string milestoneSignature;
		string unlockSignature;
		Progression progression;
		CityManager manager;

		void InitMilestones(Widget page)
		{
			progression = ctx.Get<Progression>();
			manager = CityUi.GetManager(world);
			milestoneList = page.Get<ScrollPanelWidget>("MILESTONES");
			var left = milestoneLeft = page.Get("LEFT");
			unlockContainer = left.Get("UNLOCKS");

			var name = left.Get<LabelWidget>("MILESTONE_NAME");
			name.GetText = () => progression == null ? "" : MilestoneName(progression.MilestoneIndex);
			name.GetColor = () => Heading;

			left.Get<LabelWidget>("MILESTONE_POP").GetText = () => manager == null ? "" :
				FluentProvider.GetMessage(Population, "population", Number(manager.Population));

			var xp = left.Get<LabelWidget>("XP_VALUE");
			xp.GetText = () =>
			{
				var p = ctx.Progression;
				if (p == null)
					return "";

				return p.NextMilestoneXp <= p.Xp ? FluentProvider.GetMessage(XpMax, "xp", Number(p.Xp)) :
					FluentProvider.GetMessage(XpOf, "xp", Number(p.Xp), "next", Number(p.NextMilestoneXp));
			};

			var bar = left.Get<CityBarWidget>("XP_BAR");
			bar.GetPercentage = BandPercent;

			var next = left.Get<LabelWidget>("XP_NEXT");
			next.GetText = () => progression == null || Maxed ? "" : FluentProvider.GetMessage(NextName, "name", MilestoneName(progression.MilestoneIndex + 1));
			next.GetColor = () => CityTheme.FamilyShade(family, 2);

			var toGo = left.Get<LabelWidget>("XP_TO_GO");
			toGo.GetText = () => progression == null || Maxed ? "" : FluentProvider.GetMessage(XpToGo, "xp", Number(progression.NextMilestoneXp - progression.Xp));
			toGo.GetColor = () => CityTheme.FamilyShade(family, 2);

			var reward = left.Get<CityHeaderWidget>("REWARD_HEADER");
			reward.GetText = () => progression == null || Maxed ? "" : FluentProvider.GetMessage(Reward, "name", MilestoneName(progression.MilestoneIndex + 1));

			var money = left.Get<LabelWidget>("MONEY_VALUE");
			money.GetText = () => RewardOf(m => "+" + CityUtils.FormatMoney(m.Money));
			money.GetColor = () => CityTheme.MoneyPositive;
			left.Get<LabelWidget>("POINTS_VALUE").GetText = () => RewardOf(m => Number(m.DevPoints));
			left.Get<LabelWidget>("TILES_VALUE").GetText = () => RewardOf(m => Number(m.Tiles));

			// The reward block goes away once the last milestone is reached.
			foreach (var id in new[]
			{
				"MONEY_ICON", "MONEY_LABEL", "MONEY_VALUE", "POINTS_ICON", "POINTS_LABEL", "POINTS_VALUE",
				"TILES_ICON", "TILES_LABEL", "TILES_VALUE", "UNLOCKS_LABEL"
			})
			{
				var row = left.Get(id);
				row.IsVisible = () => progression != null && !Maxed;
			}
		}

		bool Maxed => progression.MilestoneIndex >= progression.MilestoneCount;

		string MilestoneName(int index)
		{
			if (progression == null)
				return "";

			var name = progression.GetMilestone(index).Name;
			return CityUi.Message("label-milestone-" + name.ToLowerInvariant().Replace(' ', '-'), name);
		}

		string RewardOf(System.Func<MilestoneData, string> format)
		{
			return progression == null || Maxed ? "" : format(progression.GetMilestone(progression.MilestoneIndex + 1));
		}

		/// <summary>XP progress inside the current milestone band (from this milestone's XP to the next one's).</summary>
		int BandPercent()
		{
			if (progression == null || Maxed)
				return 100;

			var from = progression.GetMilestone(progression.MilestoneIndex).Xp;
			var to = progression.NextMilestoneXp;
			return to <= from ? 100 : (int)(((long)progression.Xp - from) * 100 / (to - from));
		}

		void RefreshMilestones()
		{
			var source = ctx.ProgressionUi;
			if (source == null || progression == null)
				return;

			var signature = source.Milestones.Count + ":" + progression.MilestoneIndex + ":" + milestoneList.Bounds.Width;
			if (signature != milestoneSignature)
			{
				milestoneSignature = signature;
				BuildMilestones(source);
			}

			var unlockKey = progression.MilestoneIndex + ":" + unlockContainer.Bounds.Width;
			if (unlockKey != unlockSignature)
			{
				unlockSignature = unlockKey;
				BuildUnlocks();
			}
		}

		void BuildMilestones(IProgressionUiSource source)
		{
			milestoneList.RemoveChildren();
			var width = milestoneList.Bounds.Width - milestoneList.ScrollbarWidth - 2;
			var index = 0;
			foreach (var milestone in source.Milestones)
			{
				var m = milestone;
				var number = ++index;
				var row = Game.LoadWidget(world, "CITY_MILESTONE_ROW", milestoneList, []);
				row.Bounds.Width = width;
				row.Get("BG").Bounds.Width = width;

				var background = row.Get<CityRowBackgroundWidget>("BG");
				background.IsSelected = () => progression.MilestoneIndex == number;
				Color Ink() => background.Selected ? CityTheme.InkLight : CityTheme.Ink;

				var num = row.Get<LabelWidget>("NUM");
				num.GetText = () => number.ToString(CultureInfo.CurrentCulture);
				num.GetColor = Ink;

				var name = MilestoneName(number);
				var label = row.Get<LabelWidget>("NAME");
				label.Bounds.Width = width - 26 - 100;
				label.GetText = CityUi.Fitted(label, () => name);
				label.GetColor = Ink;

				var xp = row.Get<LabelWidget>("XP");
				xp.Bounds.X = width - 18 - 64;
				xp.Bounds.Width = 64;
				var xpText = Number(m.Xp);
				xp.GetText = () => xpText;
				xp.GetColor = Ink;

				var state = row.Get<CityIconWidget>("STATE");
				state.Bounds.X = width - 15;
				state.GetIcon = () => progression.MilestoneIndex >= number ? "ui_check" : "ui_lock";
			}

			milestoneList.Layout.AdjustChildren();
		}

		void BuildUnlocks()
		{
			unlockContainer.RemoveChildren();
			if (progression == null || Maxed)
				return;

			var width = (unlockContainer.Bounds.Width - (UnlockColumns - 1) * 4) / UnlockColumns;
			var keys = progression.GetMilestone(progression.MilestoneIndex + 1).Unlocks;
			var i = 0;
			foreach (var (icon, name) in unlocks.List(keys, 2 * UnlockColumns))
			{
				var column = i % UnlockColumns;
				var line = i / UnlockColumns;
				i++;
				var chip = Game.LoadWidget(world, "CITY_UNLOCK_CHIP", unlockContainer, []);
				chip.Bounds.X = column * (width + 4);
				chip.Bounds.Y = line * UnlockRowStep;
				chip.Bounds.Width = width;
				chip.Get("BG").Bounds.Width = width;
				chip.IsVisible = () => line * UnlockRowStep + UnlockChipHeight <= unlockContainer.Bounds.Height;
				chip.Get<CityIconWidget>("ICON").Icon = icon;

				var label = chip.Get<LabelWidget>("NAME");
				label.Bounds.Width = width - 24;
				label.GetText = CityUi.Fitted(label, () => name);
			}
		}
	}
}
