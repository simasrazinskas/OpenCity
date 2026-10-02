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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Progression window (RCT2 style, people family) with three icon tabs: the current milestone with its XP meter, reward and
	/// unlocks plus the list of all milestones; the development tree (node cards, buy with CityUnlockNode); and the achievements.
	/// The achievements button of the toolbar toggles the proxy panel CITY_ACHIEVEMENTS_PANEL (CityAchievementsLogic), which opens
	/// this window on the achievements tab through <see cref="RequestedTab"/>.
	/// </summary>
	public partial class CityProgressLogic : ChromeLogic
	{
		public const int TabMilestones = 0, TabTree = 1, TabAchievements = 2;

		/// <summary>Tab the next opening of the window starts on (set by the achievements proxy), -1 = the first tab.</summary>
		public static int RequestedTab = -1;

		[FluentReference("population")]
		const string Population = "label-progress-population";

		[FluentReference("xp", "next")]
		const string XpOf = "label-progress-xp-of";

		[FluentReference("xp")]
		const string XpMax = "label-progress-xp-max";

		[FluentReference("name")]
		const string NextName = "label-progress-next";

		[FluentReference("xp")]
		const string XpToGo = "label-progress-xp-to-go";

		[FluentReference("name")]
		const string Reward = "label-progress-reward";

		[FluentReference("points")]
		const string Points = "label-progress-points";

		[FluentReference("permits")]
		const string Permits = "label-progress-permits";

		const int FullHeight = 344, PageInset = 54;

		[FluentReference]
		const string TitleMilestones = "label-progress-milestones";

		[FluentReference]
		const string TitleTree = "label-progress-tree";

		[FluentReference]
		const string TitleAchievements = "label-achievements-title";

		const string PanelId = "CITY_PROGRESS_PANEL";

		readonly World world;
		readonly CityUiContext ctx;
		readonly CityPanelWidget window;
		readonly CityProgressUnlocks unlocks;
		readonly Widget[] pages;
		readonly string family;

		int tab;
		bool wasVisible;

		[ObjectCreator.UseCtor]
		public CityProgressLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			window = (CityPanelWidget)widget;
			family = window.Family;
			unlocks = new CityProgressUnlocks(world);
			pages = [widget.Get("PAGE_MILESTONES"), widget.Get("PAGE_DEVTREE"), widget.Get("PAGE_ACHIEVEMENTS")];

			var titles = new[] { TitleMilestones, TitleTree, TitleAchievements }.Select(k => FluentProvider.GetMessage(k)).ToArray();
			window.GetTitle = () => titles[tab];
			window.SetTabs(new (string Icon, Func<bool> Enabled)[]
			{
				("pnl_milestone", () => ctx.Progression != null),
				("pnl_progression", () => ctx.ProgressionUi != null),
				("pnl_achievements", () => ctx.Achievements != null)
			}.Select((t, i) => new CityWindowTab
			{
				Icon = t.Icon,
				GetTooltip = () => titles[i],
				IsActive = () => tab == i,
				IsDisabled = () => !t.Enabled(),
				OnClick = () => ShowTab(i)
			}));

			InitMilestones(pages[TabMilestones]);
			InitTree(pages[TabTree]);
			InitAchievements(pages[TabAchievements]);

			window.Place();
			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
				{
					if (RequestedTab >= 0)
					{
						ShowTab(RequestedTab);
						RequestedTab = -1;
					}
					else if (!wasVisible)
						ShowTab(TabMilestones);

					FitHeight();
					Refresh();
				}

				wasVisible = visible;
				return visible;
			};
		}

		/// <summary>Opens the window on the achievements tab.</summary>
		public static void OpenAchievements()
		{
			if (Ui.Root.GetOrNull<CityPanelWidget>(PanelId) is not { } panel)
				return;

			RequestedTab = TabAchievements;
			panel.Visible = true;
			panel.BringToFront();
		}

		/// <summary>Whether the window is open on the achievements tab.</summary>
		public static bool AchievementsOpen()
		{
			return Ui.Root.GetOrNull<CityPanelWidget>(PanelId) is { } panel && panel.IsVisible() &&
				panel.Tabs.Count > TabAchievements && panel.Tabs[TabAchievements].IsActive();
		}

		/// <summary>Toolbar button: opens the achievements tab, closes the window when it already shows it.</summary>
		public static void ToggleAchievements()
		{
			if (AchievementsOpen())
				Ui.Root.Get(PanelId).Visible = false;
			else
				OpenAchievements();
		}

		void ShowTab(int index)
		{
			tab = index;
			for (var i = 0; i < pages.Length; i++)
				pages[i].Visible = i == index;
		}

		/// <summary>Shortens the window and its lists when the work area is lower than the window (small screens, large UI scales).</summary>
		void FitHeight()
		{
			var height = Math.Min(FullHeight, window.MaxHeight);
			if (window.Bounds.Height == height)
				return;

			window.Bounds.Height = height;
			var page = height - PageInset;
			foreach (var p in pages)
				p.Bounds.Height = page;

			milestoneLeft.Bounds.Height = page;
			unlockContainer.Bounds.Height = page - 182;
			milestoneList.Bounds.Height = page - 14;
			treeList.Bounds.Height = page - 18 - 46;
			treeDetail.Bounds.Y = page - 42;
			achievementList.Bounds.Height = page - 20;
		}

		void Refresh()
		{
			switch (tab)
			{
				case TabMilestones: RefreshMilestones(); break;
				case TabTree: RefreshTree(); break;
				case TabAchievements: RefreshAchievements(); break;
			}
		}

		Color Heading => CityTheme.FamilyShade(family, 1);

		static string Number(long value) { return value.ToString("N0", CultureInfo.CurrentCulture); }
	}
}
