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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>One "label  value" line under a legend strip; Color null = body ink.</summary>
	public readonly record struct InfoViewLine(string Label, string Value, Color? Color = null);

	/// <summary>
	/// The RCT2 info views window (design/iso/ui/panels/infoviews-*.png): the 34 views of InfoViews.All as icon + text toggle
	/// buttons under four section headers (a view whose provider is missing is disabled), the legend strip of the active
	/// view with its low / high labels, the Off button and up to three summary lines. The traffic view adds its flow /
	/// volume switch and 24 hour flow chart. Fits every UI scale: the effective resolution is never below 1024x720.
	/// </summary>
	public class CityInfoViewsLogic : ChromeLogic
	{
		[FluentReference]
		const string Off = "label-infoview-off";

		[FluentReference]
		const string OffDesc = "label-infoview-off-desc";

		const int Columns = 4;
		const int Gap = 4;
		const int TileHeight = 19;
		const int RowPitch = TileHeight + 3;
		const int HeaderBlock = 13;
		const int FooterBase = 67;
		const int FooterTraffic = 104;

		readonly World world;
		readonly CityPanelWidget panel;
		readonly CityUiContext ctx;
		readonly InfoViewLayer layer;
		readonly InfoViewStats stats;
		readonly Widget views;
		readonly Widget footer;
		readonly Widget trafficBox;
		readonly CityGraphWidget flowChart;
		readonly List<GraphSeries> flowSeries = [];
		readonly Dictionary<string, (CityHeaderWidget Header, List<ButtonWidget> Tiles)> sections = [];
		readonly HeatLegendWidget legend;
		readonly LabelWidget legendLow;
		readonly LabelWidget legendHigh;
		readonly LabelWidget legendDesc;
		readonly LabelWidget[] summaryNames = new LabelWidget[3];
		readonly LabelWidget[] summaryValues = new LabelWidget[3];

		List<InfoViewLine> lines = [];
		int linesAt = -1000;
		CityInfoView shownMode = (CityInfoView)255;
		bool laidOut;
		int laidOutVersion = -1;

		[ObjectCreator.UseCtor]
		public CityInfoViewsLogic(Widget widget, World world)
		{
			this.world = world;
			panel = (CityPanelWidget)widget;
			ctx = CityUiContext.For(world);
			layer = InfoViewLayer.Get(world);
			stats = InfoViewStats.For(world);

			views = widget.Get("VIEWS");
			footer = widget.Get("FOOTER");
			trafficBox = footer.Get("TRAFFIC");
			flowChart = trafficBox.Get<CityGraphWidget>("FLOW_CHART");
			legend = footer.Get<HeatLegendWidget>("LEGEND");
			legendLow = footer.Get<LabelWidget>("LEGEND_LOW");
			legendHigh = footer.Get<LabelWidget>("LEGEND_HIGH");
			legendDesc = footer.Get<LabelWidget>("LEGEND_DESC");

			BuildSections();
			BuildFooter();
			BuildTraffic();

			// Layout, legend and summary follow the active view and the screen while the window is open.
			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		InfoViewDef CurrentDef() { return layer == null ? null : InfoViews.Get(layer.Mode); }

		// ---- construction --------------------------------------------------------------------------
		void BuildSections()
		{
			foreach (var g in InfoViews.Groups)
			{
				var header = Game.LoadWidget(world, "CITY_INFOVIEWS_HEADER", views, []) as CityHeaderWidget;
				var headerText = CityUi.Message("label-infoview-group-" + g, CityUi.Prettify(g));
				header.GetText = () => headerText;
				var tiles = new List<ButtonWidget>();
				foreach (var def in InfoViews.All.Where(d => d.Group == g))
					tiles.Add(BuildTile(def));

				sections[g] = (header, tiles);
			}
		}

		ButtonWidget BuildTile(InfoViewDef def)
		{
			var button = Game.LoadWidget(world, "CITY_INFOVIEW_TILE", views, []) as ButtonWidget;
			button.Get<CityIconWidget>("ICON").Icon = "info_" + def.Id;
			var text = CityUi.Message("label-infoview-short-" + def.Id, def.Name);
			button.GetText = () => text;
			button.GetTooltipText = () =>
			{
				var description = def.Description;
				return description.Length > 0 ? def.Name + "\n" + description : def.Name;
			};

			button.IsDisabled = () => layer == null || !def.Available(ctx);
			button.IsHighlighted = () => layer != null && layer.Mode == def.Mode;

			// The active view is pressed in on a dark fill: light text there.
			button.GetColor = () => button.IsHighlighted() ? CityTheme.InkLight : CityTheme.Ink;
			button.OnClick = () =>
			{
				if (layer != null)
					layer.Mode = layer.Mode == def.Mode ? CityInfoView.None : def.Mode;
			};

			return button;
		}

		void BuildFooter()
		{
			var off = footer.Get<ButtonWidget>("OFF");
			var offText = FluentProvider.GetMessage(Off);
			var offTip = FluentProvider.GetMessage(OffDesc);
			off.GetText = () => offText;
			off.GetTooltipText = () => offTip;
			off.IsDisabled = () => layer == null || layer.Mode == CityInfoView.None;
			off.OnClick = () =>
			{
				if (layer != null)
					layer.Mode = CityInfoView.None;
			};

			legend.GetColor = t => legendColors != null && legendColors.Length > 0
				? legendColors[Math.Clamp((int)Math.Round(t * (legendColors.Length - 1)), 0, legendColors.Length - 1)]
				: InfoViews.RampColor(InfoRamp.Good, (int)(t * 100));
			legend.IsVisible = () => CurrentDef() != null;
			legendLow.IsVisible = legendHigh.IsVisible = ContinuousDef;
			legendLow.GetText = () => CurrentDef()?.Low ?? "";
			legendHigh.GetText = () => CurrentDef()?.High ?? "";
			legendDesc.IsVisible = () => !ContinuousDef();
			legendDesc.GetText = () => CurrentDef() != null ? CurrentDef().Description : FluentProvider.GetMessage(OffDesc);

			var summary = footer.Get("SUMMARY");
			for (var i = 0; i < summaryNames.Length; i++)
			{
				var row = Game.LoadWidget(world, "CITY_INFO_ROW", summary, []);
				row.Bounds.Y = i * 12;
				row.Bounds.Height = 12;
				var index = i;
				summaryNames[i] = row.Get<LabelWidget>("NAME");
				summaryValues[i] = row.Get<LabelWidget>("VALUE");
				summaryNames[i].Bounds.Height = summaryValues[i].Bounds.Height = 12;
				summaryNames[i].GetText = () => index < lines.Count ? lines[index].Label : "";
				summaryValues[i].GetText = () => index < lines.Count ? lines[index].Value : "";
				summaryValues[i].GetColor = () => index < lines.Count && lines[index].Color is Color c ? c : CityTheme.Ink;
			}
		}

		bool ContinuousDef()
		{
			var def = CurrentDef();
			return def != null && def.Ramp != InfoRamp.Category && def.Ramp != InfoRamp.Resource;
		}

		void BuildTraffic()
		{
			var flow = trafficBox.Get<CheckboxWidget>("FLOW");
			var volume = trafficBox.Get<CheckboxWidget>("VOLUME");
			flow.IsChecked = () => layer != null && !layer.TrafficVolume;
			volume.IsChecked = () => layer != null && layer.TrafficVolume;
			flow.OnClick = () =>
			{
				if (layer != null)
					layer.TrafficVolume = false;
			};

			volume.OnClick = () =>
			{
				if (layer != null)
					layer.TrafficVolume = true;
			};

			flowChart.ShowAxis = false;
			flowChart.FillFirst = true;
			flowChart.GetSeries = () =>
			{
				flowSeries.Clear();
				var info = ctx.Get<ITrafficInfo>();
				if (info == null)
					return flowSeries;

				var values = new int[24];
				for (var h = 0; h < 24; h++)
					values[h] = Math.Max(0, info.GetFlowHistory(h));

				flowSeries.Add(new GraphSeries { Name = FluentProvider.GetMessage("label-traffic-flow"), Color = Color.FromArgb(0xF6, 0xE0, 0x80), Values = values });
				return flowSeries;
			};

			flowChart.GetSampleLabel = ago => FluentProvider.GetMessage("label-traffic-hour", "hour", 23 - ago);
		}

		// ---- per frame -----------------------------------------------------------------------------
		Color[] legendColors;

		void Refresh()
		{
			stats.Update(layer);
			var mode = layer?.Mode ?? CityInfoView.None;
			if (mode != shownMode)
			{
				shownMode = mode;
				UpdateLegend(CurrentDef());
				LayoutFooter();
				linesAt = -1000;
			}

			if (Game.RenderFrame - linesAt >= 10)
			{
				linesAt = Game.RenderFrame;
				lines = SummaryLines();
			}

			Layout();
		}

		/// <summary>The legend strip: NET's ramp in 11 steps, or one swatch per category / resource kind.</summary>
		void UpdateLegend(InfoViewDef def)
		{
			legendColors = null;
			legend.Steps = InfoViewRamps.Steps;
			if (def == null)
				return;

			switch (def.Ramp)
			{
				case InfoRamp.Category:
					var n = def.Mode == CityInfoView.Roads ? 4 : Math.Clamp(ctx.ProgressionUi?.Districts.Count ?? 1, 1, InfoViewRamps.CategoryCount);
					var offset = def.Mode == CityInfoView.Roads ? 1 : 0;
					legendColors = Enumerable.Range(0, n).Select(i => InfoViewRamps.Opaque(InfoViews.RampColor(InfoRamp.Category, i + offset))).ToArray();
					legend.Steps = n;
					break;
				case InfoRamp.Resource:
					legendColors = Enumerable.Range(0, 6).Select(k => InfoViewRamps.Opaque(InfoViews.RampColor(InfoRamp.Resource, k * 16 + 10))).ToArray();
					legend.Steps = 6;
					break;
				default:
					legendColors = Enumerable.Range(0, InfoViewRamps.Steps).Select(i => InfoViewRamps.Opaque(InfoViewRamps.Step(def.Ramp, i))).ToArray();
					break;
			}
		}

		List<InfoViewLine> SummaryLines()
		{
			var result = new List<InfoViewLine>();
			var mode = layer?.Mode ?? CityInfoView.None;
			if (mode == CityInfoView.None)
				return result;

			foreach (var (label, value) in InfoViews.Summary(mode, ctx))
				result.Add(new InfoViewLine(label, value));

			// Views with fewer than two own numbers get the average / highest / lowest of the coloured cells.
			if (result.Count < 2 && ContinuousDef())
				result.AddRange(InfoViewLines.Stats(stats, CurrentDef().Ramp).Take(3 - result.Count));

			return result.Take(summaryNames.Length).ToList();
		}

		// ---- layout --------------------------------------------------------------------------------
		static int GroupHeight(int views)
		{
			return HeaderBlock + (views + Columns - 1) / Columns * RowPitch;
		}

		void Layout()
		{
			var traffic = layer != null && layer.Mode == CityInfoView.Traffic && ctx.Get<ITrafficInfo>() != null;
			var version = CityLayout.Version;
			if (laidOut == traffic && version == laidOutVersion)
				return;

			laidOut = traffic;
			laidOutVersion = version;
			var tileWidth = (views.Bounds.Width - (Columns - 1) * Gap) / Columns;
			var y = 0;
			foreach (var g in InfoViews.Groups)
			{
				var (header, tiles) = sections[g];
				header.Bounds.Y = y;
				for (var i = 0; i < tiles.Count; i++)
				{
					tiles[i].Bounds.X = i % Columns * (tileWidth + Gap);
					tiles[i].Bounds.Y = y + HeaderBlock + i / Columns * RowPitch;
					tiles[i].Bounds.Width = tileWidth;
				}

				y += GroupHeight(tiles.Count);
			}

			views.Bounds.Y = panel.ContentY;
			views.Bounds.Height = y;
			footer.Bounds.Y = views.Bounds.Y + y;
			footer.Bounds.Height = traffic ? FooterTraffic : FooterBase;
			trafficBox.Visible = traffic;
			panel.Bounds.Height = footer.Bounds.Bottom + CityPanelWidget.Padding;
			panel.Place();
		}

		void LayoutFooter()
		{
			// The description of a category / resource view sits where the low / high labels are; "no overlay" at the top.
			legendDesc.Bounds.Y = CurrentDef() == null ? 6 : 18;
			legendDesc.Align = CurrentDef() == null ? TextAlign.Left : TextAlign.Center;
		}
	}

	/// <summary>Average / highest / lowest lines shared by the info views window and the legend window.</summary>
	static class InfoViewLines
	{
		public static IEnumerable<InfoViewLine> Stats(InfoViewStats stats, InfoRamp ramp)
		{
			if (!stats.Valid)
				yield break;

			// Whether a high value is good news on this ramp decides the colour of the extremes.
			var highIsBad = ramp is InfoRamp.Bad or InfoRamp.Pollution;
			Color High() => highIsBad ? CityTheme.MoneyNegative : CityTheme.MoneyPositive;
			Color Low() => highIsBad ? CityTheme.MoneyPositive : CityTheme.MoneyNegative;
			var all = stats.Lines().ToArray();
			yield return new InfoViewLine(all[0].Label, all[0].Value);
			yield return new InfoViewLine(all[1].Label, all[1].Value, High());
			yield return new InfoViewLine(all[2].Label, all[2].Value, Low());
		}
	}
}
