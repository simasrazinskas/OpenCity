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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Info views panel: group tabs (services, networks, environment, city), the views of the group whose providers
	/// exist, the legend strip with its real labels and a few city-wide numbers of the active view.
	/// </summary>
	public class CityInfoViewsLogic : ChromeLogic
	{
		[FluentReference]
		const string Off = "label-infoview-off";

		[FluentReference]
		const string OffDesc = "label-infoview-off-desc";

		const int Columns = 4;
		const int ItemWidth = 130;
		const int ItemHeight = 32;
		const int Spacing = 6;
		const int SummaryRows = 3;

		readonly World world;
		readonly Widget widgetPanel;
		readonly CityUiContext ctx;
		readonly InfoViewLayer layer;
		readonly Widget modes;
		readonly Widget tabs;
		readonly Widget summary;
		readonly Widget trafficModes;
		readonly CityGraphWidget flowChart;
		readonly List<GraphSeries> flowSeries = [];
		readonly Dictionary<string, ButtonWidget> tabButtons = [];
		readonly LabelWidget[] summaryNames = new LabelWidget[SummaryRows];
		readonly LabelWidget[] summaryValues = new LabelWidget[SummaryRows];
		readonly HeatLegendWidget legend;
		readonly LabelWidget legendLow;
		readonly LabelWidget legendHigh;
		readonly LabelWidget legendDesc;

		string group;
		CityInfoView shownMode;
		int builtFor = -1;

		[ObjectCreator.UseCtor]
		public CityInfoViewsLogic(Widget widget, World world)
		{
			this.world = world;
			widgetPanel = widget;
			ctx = CityUiContext.For(world);
			layer = world.WorldActor.TraitOrDefault<InfoViewLayer>();

			modes = widget.Get("MODES");
			tabs = widget.Get("TABS");
			summary = widget.Get("SUMMARY");
			trafficModes = widget.Get("TRAFFIC_MODES");
			flowChart = widget.Get<CityGraphWidget>("FLOW_CHART");
			BuildTrafficSection();

			legend = widget.Get<HeatLegendWidget>("LEGEND");
			legendLow = widget.Get<LabelWidget>("LEGEND_LOW");
			legendHigh = widget.Get<LabelWidget>("LEGEND_HIGH");
			legendDesc = widget.Get<LabelWidget>("LEGEND_DESC");

			legend.GetColor = t => InfoViews.RampColor(CurrentDef()?.Ramp ?? InfoRamp.Good, (int)(t * 100));
			legend.IsVisible = () => CurrentDef() != null && CurrentDef().Ramp != InfoRamp.Category && CurrentDef().Ramp != InfoRamp.Resource;
			legendLow.IsVisible = legend.IsVisible;
			legendHigh.IsVisible = legend.IsVisible;
			legendLow.GetText = () => CurrentDef()?.Low ?? "";
			legendHigh.GetText = () => CurrentDef()?.High ?? "";
			legendDesc.GetText = () => CurrentDef() != null ? CurrentDef().Description : FluentProvider.GetMessage(OffDesc);

			for (var i = 0; i < SummaryRows; i++)
			{
				var row = Game.LoadWidget(world, "CITY_INFO_ROW", summary, []);
				row.Bounds.Y = i * 20;
				row.Bounds.Width = 260;
				var index = i;
				summaryNames[i] = row.Get<LabelWidget>("NAME");
				summaryValues[i] = row.Get<LabelWidget>("VALUE");
				summaryNames[i].GetText = () => SummaryLine(index).Label;
				summaryValues[i].GetText = () => SummaryLine(index).Value;
				summaryNames[i].Bounds.Width = 150;
				summaryValues[i].Bounds.X = 150;
				summaryValues[i].Bounds.Width = 110;
			}

			// The panel rebuilds its tabs and view buttons whenever the set of available providers changes.
			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
				{
					EnsureBuilt(widget);
					FollowMode();
				}

				return visible;
			};
		}

		void BuildTrafficSection()
		{
			var x = 0;
			foreach (var (volume, key) in new[] { (false, "label-traffic-flow"), (true, "label-traffic-volume") })
			{
				var isVolume = volume;
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", trafficModes, []) as ButtonWidget;
				button.Bounds.X = x;
				button.Bounds.Width = 120;
				button.Bounds.Height = 26;
				var text = FluentProvider.GetMessage(key);
				button.GetText = () => text;
				button.IsHighlighted = () => layer != null && layer.TrafficVolume == isVolume;
				button.OnClick = () =>
				{
					if (layer != null)
						layer.TrafficVolume = isVolume;
				};

				x += 126;
			}

			flowChart.GetSeries = () =>
			{
				flowSeries.Clear();
				var info = ctx.Get<ITrafficInfo>();
				if (info == null)
					return flowSeries;

				var values = new int[24];
				for (var h = 0; h < 24; h++)
					values[h] = Math.Max(0, info.GetFlowHistory(h));

				flowSeries.Add(new GraphSeries { Name = FluentProvider.GetMessage("label-traffic-flow"), Color = CityUi.Good, Values = values });
				return flowSeries;
			};

			flowChart.GetSampleLabel = ago => FluentProvider.GetMessage("label-traffic-hour", "hour", 23 - ago);
		}

		void FollowMode()
		{
			var mode = layer?.Mode ?? CityInfoView.None;
			if (mode == shownMode)
				return;

			// A hotkey or another tool changed the view: switch the tab to its group.
			shownMode = mode;
			var def = CurrentDef();
			if (def != null && def.Group != group && tabButtons.ContainsKey(def.Group))
			{
				group = def.Group;
				BuildModes();
			}
			else
				Relayout(widgetPanel, modeRows);
		}

		InfoViewDef CurrentDef()
		{
			return layer == null ? null : InfoViews.Get(layer.Mode);
		}

		(string Label, string Value) SummaryLine(int index)
		{
			var mode = layer?.Mode ?? CityInfoView.None;
			var i = 0;
			foreach (var line in InfoViews.Summary(mode, ctx))
			{
				if (i++ == index)
					return line;
			}

			return ("", "");
		}

		IEnumerable<InfoViewDef> Available(string forGroup)
		{
			foreach (var def in InfoViews.All)
				if (def.Group == forGroup && def.Available(ctx))
					yield return def;
		}

		void EnsureBuilt(Widget panel)
		{
			if (builtFor == 0)
				return;

			builtFor = 0;
			tabs.RemoveChildren();
			tabButtons.Clear();

			var x = 0;
			foreach (var g in InfoViews.Groups)
			{
				var any = false;
				foreach (var _ in Available(g))
				{
					any = true;
					break;
				}

				if (!any)
					continue;

				var groupId = g;
				group ??= g;
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", tabs, []) as ButtonWidget;
				button.Bounds.X = x;
				button.Bounds.Width = 100;
				button.Bounds.Height = 28;
				var text = CityUi.Message("label-infoview-group-" + g, CityUi.Prettify(g));
				button.GetText = () => text;
				button.IsHighlighted = () => group == groupId;
				button.OnClick = () =>
				{
					group = groupId;
					BuildModes();
				};

				tabButtons[g] = button;
				x += 104;
			}

			var off = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", tabs, []) as ButtonWidget;
			off.Bounds.X = panel.Bounds.Width - 24 - 110;
			off.Bounds.Width = 110;
			off.Bounds.Height = 28;
			var offText = FluentProvider.GetMessage(Off);
			off.GetText = () => offText;
			off.IsDisabled = () => layer == null;
			off.IsHighlighted = () => layer != null && layer.Mode == CityInfoView.None;
			off.OnClick = () =>
			{
				if (layer != null)
					layer.Mode = CityInfoView.None;
			};

			BuildModes();
		}

		void BuildModes()
		{
			modes.RemoveChildren();
			if (group == null)
				return;

			var i = 0;
			foreach (var def in Available(group))
			{
				var current = def;
				var button = Game.LoadWidget(world, "CITY_INFOVIEW_ITEM", modes, []) as ButtonWidget;
				button.Bounds.X = i % Columns * (ItemWidth + Spacing);
				button.Bounds.Y = i / Columns * (ItemHeight + Spacing);
				button.Bounds.Width = ItemWidth;
				button.Bounds.Height = ItemHeight;

				var text = def.Name;
				button.GetText = () => text;
				button.IsDisabled = () => layer == null;
				button.IsHighlighted = () => layer != null && layer.Mode == current.Mode;
				button.OnClick = () =>
				{
					if (layer != null)
						layer.Mode = current.Mode;
				};

				i++;
			}

			modeRows = Math.Max(1, (i + Columns - 1) / Columns);
			Relayout(widgetPanel, modeRows);
		}

		int modeRows = 1;

		/// <summary>Fits the panel to the number of view rows of the selected group.</summary>
		void Relayout(Widget panel, int rows)
		{
			var modesHeight = rows * (ItemHeight + Spacing);
			var y = modes.Bounds.Y + modesHeight + 6;
			legend.Bounds.Y = y;
			legendLow.Bounds.Y = y + 14;
			legendDesc.Bounds.Y = y + 14;
			legendHigh.Bounds.Y = y + 14;
			summary.Bounds.Y = y + 40;
			panel.Bounds.Height = summary.Bounds.Y + summary.Bounds.Height + 8;

			// The traffic view adds its flow / volume switch and the 24 hour flow chart below the numbers.
			var traffic = layer != null && layer.Mode == CityInfoView.Traffic && ctx.Get<ITrafficInfo>() != null;
			trafficModes.Visible = flowChart.Visible = traffic;
			if (traffic)
			{
				trafficModes.Bounds.Y = panel.Bounds.Height - 6;
				flowChart.Bounds.Y = trafficModes.Bounds.Y + 34;
				panel.Bounds.Height += 34 + flowChart.Bounds.Height + 6;
			}

			panel.Bounds.Y = Game.Renderer.Resolution.Height - panel.Bounds.Height - 52;
		}
	}
}
