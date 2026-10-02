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
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 build window (design/iso/ui/panels/build-menu-health.png): one window per build group in the group's
	/// colours, an icon tab per category (roads and transit split into sections), a grid of cards with thumbnails and
	/// prices, and the details of the hovered (or active) card underneath. Clicking a card starts its tool; closing the
	/// window cancels a tool started from it.
	/// </summary>
	public partial class CityToolbarLogic
	{
		const string BuildPanelId = "CITY_BUILD_PANEL";
		const int DetailHeight = 62;
		const int MaxColumns = 6;
		const int MinColumns = 3;

		sealed class BuildTab
		{
			public string Category;
			public string Section;
			public string Icon;
			public string Tooltip;
			public List<ToolItem> Items;
		}

		CityPanelWidget buildPanel;
		CityBuildMenuWidget buildMenu;
		LabelWidget detailName;
		LabelWidget detailText;
		string buildGroup;
		BuildTab buildTab;
		List<BuildTab> buildTabs = [];
		bool toolFromBuild;

		void InitBuildWindow()
		{
			buildPanel = Ui.Root.Get<CityPanelWidget>(BuildPanelId);
			buildMenu = buildPanel.Get<CityBuildMenuWidget>("CARDS");
			detailName = buildPanel.Get<LabelWidget>("DETAIL_NAME");
			detailText = buildPanel.Get<LabelWidget>("DETAIL_TEXT");
			buildPanel.GetTitle = () => buildTab?.Tooltip ?? "";
			buildPanel.OnClose = CloseBuild;

			string Detail(out string name)
			{
				var index = buildMenu.Hovered;
				ToolItem item = null;
				var items = buildTab?.Items;
				if (items != null && index >= 0 && index < items.Count)
					item = items[index];
				else if (items != null)
					item = items.FirstOrDefault(i => i.IsActive != null ? i.IsActive() : activeToolId == i.Id && ToolActive());

				name = item?.Name ?? "";
				if (item == null)
					return CityUi.Message("label-city-build-hint");

				var text = item.Description ?? "";
				if (item.ExtraTooltip != null)
				{
					var extra = item.ExtraTooltip();
					if (!string.IsNullOrEmpty(extra))
						text += "\n" + extra;
				}

				return text;
			}

			var cachedDetail = "";
			var cachedWidth = -1;
			var cachedScale = -1f;
			var wrapped = "";
			detailName.GetText = () =>
			{
				Detail(out var n);
				return n;
			};

			detailText.GetText = () =>
			{
				var text = Detail(out _);
				if (text != cachedDetail || detailText.Bounds.Width != cachedWidth || Game.Renderer.WindowScale != cachedScale)
				{
					cachedDetail = text;
					cachedWidth = detailText.Bounds.Width;
					cachedScale = Game.Renderer.WindowScale;
					var font = Game.Renderer.Fonts[detailText.Font];
					var lines = WidgetUtils.WrapText(text.Replace("\n", " · "), cachedWidth, font).Split('\n');
					wrapped = string.Join("\n", lines.Take(3));
				}

				return wrapped;
			};
		}

		List<BuildTab> TabsForGroup(string group)
		{
			var result = new List<BuildTab>();
			var categories = BuildGroups.FirstOrDefault(g => g.Name == group).Categories ?? [group];
			if (group == "signature")
				categories = [.. categories, .. placeables.Keys.Where(k => !TabOrder.Contains(k) && k != "signature").OrderBy(k => k, StringComparer.Ordinal)];

			foreach (var category in categories)
			{
				if (!TabHasItems(category))
					continue;

				var items = ItemsFor(category);
				var title = CityUi.Message("label-city-category-" + category);
				foreach (var (section, icon, filter) in Sections(category))
				{
					var sectionItems = items.Where(filter).ToList();
					if (sectionItems.Count == 0)
						continue;

					result.Add(new BuildTab
					{
						Category = category,
						Section = section,
						Icon = icon ?? CategoryIcon(category),
						Tooltip = section == null ? title : title + ": " + CityUi.Message("label-city-build-section-" + section),
						Items = sectionItems
					});
				}
			}

			return result;
		}

		static IEnumerable<(string Section, string Icon, Func<ToolItem, bool> Filter)> Sections(string category)
		{
			if (category == "road")
			{
				yield return ("types", "road_street", i => i.Id == "road" || i.Id.StartsWith("roadtype:", StringComparison.Ordinal));
				yield return ("modes", "mode_oneway", i => i.Id.StartsWith("roadmode:", StringComparison.Ordinal) || i.Id.StartsWith("prefab:", StringComparison.Ordinal));
				yield return ("junctions", "ctl_signal", i => i.Id.StartsWith("road-control-", StringComparison.Ordinal));
				yield return ("addons", "addon_trees", i => i.Id.StartsWith("addon", StringComparison.Ordinal));
			}
			else if (category == "transit")
			{
				yield return ("stops", "tr_bus_stop", i => i.Id is "busstop" or "taxistand" or "tramstop" or "stop-remove");
				yield return ("tracks", "tr_rail", i => i.Id.StartsWith("tramtrack", StringComparison.Ordinal) || i.Id.StartsWith("rail", StringComparison.Ordinal));
				yield return ("lines", "tr_line", i => i.Id.EndsWith("line", StringComparison.Ordinal));
				yield return ("buildings", "tr_station", i => i.ActorType != null);
			}
			else
				yield return (null, null, _ => true);
		}

		bool BuildOpen => buildPanel != null && buildPanel.IsVisible();

		bool BuildOpenFor(string category) => BuildOpen && buildTab?.Category == category;

		bool BuildOpenForGroup(string group) => BuildOpen && buildGroup == group;

		/// <summary>Opens the build window at a category (closes it when that category is already shown).</summary>
		void ToggleBuild(string category)
		{
			if (BuildOpenFor(category))
			{
				CloseBuild();
				return;
			}

			OpenBuild(category, category == "road");
		}

		void OpenBuild(string category, bool activateSingleTool)
		{
			var group = GroupOf(category);
			if (group != buildGroup || buildTabs.Count == 0)
			{
				buildGroup = group;
				buildTabs = TabsForGroup(group);
				var family = BuildGroups.FirstOrDefault(g => g.Name == group).Family ?? "city";
				buildPanel.Family = family;
				buildPanel.SetTabs(buildTabs.Select(t => new CityWindowTab
				{
					Icon = t.Icon,
					GetTooltip = () => t.Tooltip,
					IsActive = () => buildTab == t,
					OnClick = () => SelectBuildTab(t, false)
				}));
			}

			var tab = buildTabs.FirstOrDefault(t => t.Category == category) ?? buildTabs.FirstOrDefault();
			if (tab == null)
				return;

			buildPanel.Visible = true;
			SelectBuildTab(tab, activateSingleTool);
		}

		void SelectBuildTab(BuildTab tab, bool activateSingleTool)
		{
			buildTab = tab;
			buildMenu.SetItems(tab.Items.Select(MakePaletteItem));
			SizeBuildWindow();

			if (activateSingleTool && tab.Items.Count == 1)
			{
				Activate(tab.Items[0].Id, tab.Items[0].Create());
				toolFromBuild = true;
			}
			else if (activateSingleTool && tab.Category == "road")
			{
				StartRoadTool();
				toolFromBuild = true;
			}
		}

		void SizeBuildWindow()
		{
			if (buildTab == null)
				return;

			var area = CityLayout.WorkArea();
			var most = buildTabs.Max(t => t.Items.Count);
			var maxFit = CityBuildMenuWidget.ColumnsFor(area.Width - 2 * CityPanelWidget.Padding - 4);
			var columns = Math.Clamp(Math.Min(most, MaxColumns), Math.Min(MinColumns, maxFit), Math.Max(1, maxFit));
			var rowsNeeded = (buildTab.Items.Count + columns - 1) / columns;
			const int Chrome = CityPanelWidget.ContentTopWithTabs + DetailHeight + 2 * CityPanelWidget.Padding;
			var maxRows = Math.Max(1, (area.Height - Chrome) / (CityBuildMenuWidget.CardHeight + CityBuildMenuWidget.Gap));
			var rows = Math.Clamp(rowsNeeded, 1, Math.Min(2, maxRows));

			var cardsWidth = CityBuildMenuWidget.WidthFor(columns);
			var cardsHeight = CityBuildMenuWidget.HeightFor(rows);
			buildMenu.Bounds = new WidgetBounds(CityPanelWidget.Padding + 2, CityPanelWidget.ContentTopWithTabs, cardsWidth, cardsHeight);

			var width = Math.Max(cardsWidth + 2 * CityPanelWidget.Padding + 4, buildTabs.Count * (CityPanelWidget.TabWidth + 1) + 12);
			var detailY = CityPanelWidget.ContentTopWithTabs + cardsHeight + 4;
			var separator = buildPanel.Get("DETAIL_SEPARATOR");
			separator.Bounds = new WidgetBounds(CityPanelWidget.Padding + 2, detailY, width - 2 * CityPanelWidget.Padding - 4, 2);
			detailName.Bounds = new WidgetBounds(CityPanelWidget.Padding + 2, detailY + 4, width - 2 * CityPanelWidget.Padding - 4, 16);
			detailText.Bounds = new WidgetBounds(CityPanelWidget.Padding + 2, detailY + 22, width - 2 * CityPanelWidget.Padding - 4, 36);
			buildPanel.Bounds.Width = width;
			buildPanel.Bounds.Height = detailY + DetailHeight + CityPanelWidget.Padding;
		}

		void CloseBuild()
		{
			if (buildPanel == null)
				return;

			buildPanel.Visible = false;
			if (toolFromBuild && ToolActive() && activeToolId != "bulldoze")
				CancelTool();

			toolFromBuild = false;
		}
	}
}
