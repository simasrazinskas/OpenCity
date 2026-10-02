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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The info view legend window (design/iso/ui/panels/legend-*.png, bottom left of hud-infoview-1920x1080-1x.png): shown
	/// while an info view is active and the info views window is closed. Titled with the view's name, it shows the ramp strip
	/// with low / medium / high (continuous views), or a swatch list (road classes, districts, natural resources), and the
	/// average / highest / lowest of the coloured cells when there are some.
	/// </summary>
	public class CityInfoViewLegendLogic : ChromeLogic
	{
		[FluentReference]
		const string Medium = "label-infoview-legend-medium";

		[FluentReference]
		const string Districts = "label-infoview-short-districts";

		const int LineHeight = 12;
		const int ListPitch = 13;
		const int ResourcePitch = 19;

		static readonly string[] Roads = ["label-roadclass-local", "label-roadclass-collector", "label-roadclass-arterial", "label-roadclass-highway"];

		readonly World world;
		readonly CityPanelWidget panel;
		readonly CityUiContext ctx;
		readonly InfoViewLayer layer;
		readonly InfoViewStats stats;
		readonly LabelWidget desc;
		readonly Widget strip;
		readonly Widget ticks;
		readonly HeatLegendWidget ramp;
		readonly Widget iconLow;
		readonly Widget iconHigh;
		readonly LabelWidget low;
		readonly LabelWidget mid;
		readonly LabelWidget high;
		readonly Widget list;
		readonly Widget line;
		readonly Widget rows;
		readonly CityBarWidget meter;
		readonly Widget lines;
		readonly LabelWidget[] lineNames = new LabelWidget[3];
		readonly LabelWidget[] lineValues = new LabelWidget[3];
		readonly Widget[] lineRows = new Widget[3];

		Color[] colors = [];
		List<InfoViewLine> shown = [];
		int shownAt = -1000;
		(CityInfoView Mode, int Districts, int Version, int Lines) laidOut = (CityInfoView.None, -1, -1, -1);

		[ObjectCreator.UseCtor]
		public CityInfoViewLegendLogic(Widget widget, World world)
		{
			this.world = world;
			panel = (CityPanelWidget)widget;
			ctx = CityUiContext.For(world);
			layer = InfoViewLayer.Get(world);
			stats = InfoViewStats.For(world);

			desc = widget.Get<LabelWidget>("DESC");
			strip = widget.Get("STRIP");
			ticks = strip.Get("TICKS");
			ramp = strip.Get<HeatLegendWidget>("RAMP");
			iconLow = strip.Get("ICON_LOW");
			iconHigh = strip.Get("ICON_HIGH");
			low = strip.Get<LabelWidget>("LOW");
			mid = strip.Get<LabelWidget>("MID");
			high = strip.Get<LabelWidget>("HIGH");
			list = widget.Get("LIST");
			line = widget.Get("LINE");
			rows = widget.Get("ROWS");
			meter = rows.Get<CityBarWidget>("METER");
			lines = rows.Get("LINES");

			desc.GetColor = () => CityTheme.FamilyShade("info", 2);
			mid.GetText = () => FluentProvider.GetMessage(Medium);
			low.GetText = () => CurrentDef()?.Low ?? "";
			high.GetText = () => CurrentDef()?.High ?? "";
			ramp.GetColor = t => colors.Length == 0 ? Color.Gray : colors[Math.Clamp((int)Math.Round(t * (colors.Length - 1)), 0, colors.Length - 1)];
			meter.GetPercentage = () => stats.Valid ? stats.Average : 0;
			meter.GetRamp = () => CurrentDef()?.Ramp is InfoRamp.Bad or InfoRamp.Pollution ? "red" : "green";

			for (var i = 0; i < lineRows.Length; i++)
			{
				var row = Game.LoadWidget(world, "CITY_INFO_ROW", lines, []);
				row.Bounds.Y = i * LineHeight;
				row.Bounds.Height = LineHeight;
				var index = i;
				lineRows[i] = row;
				lineNames[i] = row.Get<LabelWidget>("NAME");
				lineValues[i] = row.Get<LabelWidget>("VALUE");
				lineNames[i].Bounds.Height = lineValues[i].Bounds.Height = LineHeight;
				lineNames[i].GetText = () => index < shown.Count ? shown[index].Label : "";
				lineValues[i].GetText = () => index < shown.Count ? shown[index].Value : "";
				lineValues[i].GetColor = () => index < shown.Count && shown[index].Color is Color c ? c : CityTheme.Ink;
			}

			panel.GetTitle = () => CurrentDef()?.Name ?? "";
			panel.OnClose = () =>
			{
				if (layer != null)
					layer.Mode = CityInfoView.None;
			};

			// Visible while a view is on and the info views window is closed.
			widget.IsVisible = () =>
			{
				var infoViews = Ui.Root.GetOrNull("CITY_INFOVIEWS_PANEL");
				if (layer == null || layer.Mode == CityInfoView.None || (infoViews != null && infoViews.IsVisible()))
					return false;

				Refresh();
				return true;
			};
		}

		InfoViewDef CurrentDef() { return layer == null ? null : InfoViews.Get(layer.Mode); }

		void Refresh()
		{
			stats.Update(layer);
			var districts = ctx.ProgressionUi?.Districts.Count ?? 0;
			var def = CurrentDef();
			if (Game.RenderFrame - shownAt >= 10)
			{
				shownAt = Game.RenderFrame;
				if (def.Ramp is InfoRamp.Category or InfoRamp.Resource)
					shown = def.Mode == CityInfoView.Districts
						? [new InfoViewLine(FluentProvider.GetMessage(Districts), districts.ToString(CultureInfo.InvariantCulture))]
						: [];
				else
					shown = InfoViewLines.Stats(stats, def.Ramp).ToList();
			}

			var key = (layer.Mode, districts, CityLayout.Version, shown.Count);
			if (key == laidOut)
				return;

			laidOut = key;
			Layout(def);
		}

		void Layout(InfoViewDef def)
		{
			var y = CityPanelWidget.ContentTop;

			// Description, word-wrapped to the window.
			var font = Game.Renderer.Fonts[desc.Font];
			var text = def.Description;
			var wrapped = text.Length == 0 ? "" : WidgetUtils.WrapText(text, desc.Bounds.Width, font);
			var descLines = wrapped.Length == 0 ? 0 : wrapped.Count(c => c == '\n') + 1;
			desc.GetText = () => wrapped;
			desc.Visible = descLines > 0;
			desc.Bounds.Y = y;
			desc.Bounds.Height = descLines * 10 + 2;
			y += descLines > 0 ? desc.Bounds.Height + 2 : 0;

			var continuous = def.Ramp != InfoRamp.Category && def.Ramp != InfoRamp.Resource;
			strip.Visible = continuous;
			list.Visible = !continuous;
			if (continuous)
			{
				SetStrip(def);
				strip.Bounds.Y = y;
				y += strip.Bounds.Height + 3;
			}
			else
			{
				BuildList(def);
				list.Bounds.Y = y;
				y += list.Bounds.Height + 3;
			}

			var count = shown.Count;
			line.Visible = rows.Visible = count > 0;
			if (count > 0)
			{
				line.Bounds.Y = y;
				y += 6;
				rows.Bounds.Y = y;
				meter.Visible = continuous && def.Ramp is InfoRamp.Good or InfoRamp.Bad;
				for (var i = 0; i < lineRows.Length; i++)
					lineRows[i].Visible = i < count;

				rows.Bounds.Height = count * LineHeight;
				lines.Bounds.Height = count * LineHeight;
				y += count * LineHeight;
			}

			panel.Bounds.Height = y + CityPanelWidget.Padding + 2;
			panel.Place();
		}

		void SetStrip(InfoViewDef def)
		{
			colors = Enumerable.Range(0, InfoViewRamps.Steps).Select(i => InfoViewRamps.Opaque(InfoViewRamps.Step(def.Ramp, i))).ToArray();
			ramp.Steps = InfoViewRamps.Steps;

			// Happiness gets the sad / happy faces at the ends of the strip (legend-happiness.png).
			var faces = def.Mode == CityInfoView.Happiness;
			var width = strip.Bounds.Width;
			iconLow.Visible = iconHigh.Visible = faces;
			var inset = faces ? 19 : 0;
			ramp.Bounds.X = inset;
			ramp.Bounds.Width = width - 2 * inset;
			ramp.Bounds.Y = faces ? 2 : 1;

			// Medium and the tick marks only on the heat ramps.
			var heat = def.Ramp is not (InfoRamp.Good or InfoRamp.Bad);
			mid.Visible = heat;
			ticks.Visible = heat;
			ticks.RemoveChildren();
			if (heat)
			{
				var inner = ramp.Bounds.Width - 2;
				for (var i = 0; i < InfoViewRamps.Steps; i += 2)
				{
					var swatch = new CitySwatchWidget { Bounds = new WidgetBounds(1 + inner * i / InfoViewRamps.Steps + inner / (2 * InfoViewRamps.Steps), 0, 1, 3) };
					ticks.AddChild(swatch);
				}
			}

			var labelY = faces ? 20 : 17;
			low.Bounds.Y = mid.Bounds.Y = high.Bounds.Y = labelY;
			strip.Bounds.Height = labelY + 10;
		}

		void BuildList(InfoViewDef def)
		{
			list.RemoveChildren();
			var resources = def.Ramp == InfoRamp.Resource;
			var entries = new List<(string Name, Color Color, string Icon, Func<string> Value)>();
			if (resources)
			{
				for (var kind = 0; kind < 6; kind++)
				{
					var k = kind;
					var name = ((NaturalResourceKind)kind).ToString().ToLowerInvariant();
					entries.Add((CityUi.Message("label-resource-kind-" + name, name), InfoViewRamps.Opaque(InfoViews.RampColor(InfoRamp.Resource, kind * 16 + 10)),
						"nat_" + name, () => stats.KindRichness(k) is var r and >= 0 ? FluentProvider.GetMessage("label-city-percent", "value", r) : "-"));
				}
			}
			else if (def.Mode == CityInfoView.Roads)
			{
				for (var i = 0; i < Roads.Length; i++)
					entries.Add((FluentProvider.GetMessage(Roads[i]), InfoViewRamps.Opaque(InfoViews.RampColor(InfoRamp.Category, i + 1)), null, null));
			}
			else
			{
				var districts = ctx.ProgressionUi?.Districts;
				if (districts != null)
					foreach (var d in districts.Take(12))
						entries.Add((string.IsNullOrEmpty(d.Name) ? "#" + d.Id : d.Name, InfoViewRamps.Opaque(InfoViews.RampColor(InfoRamp.Category, d.Id - 1)), null, null));
			}

			var width = list.Bounds.Width;
			var columns = resources ? 1 : 2;
			var pitch = resources ? ResourcePitch : ListPitch;
			var perColumn = Math.Max(1, (entries.Count + columns - 1) / columns);
			for (var i = 0; i < entries.Count; i++)
			{
				var (name, color, icon, value) = entries[i];
				var entry = Game.LoadWidget(world, "CITY_LEGEND_ENTRY", list, []);
				entry.Bounds.X = i / perColumn * (width / columns);
				entry.Bounds.Y = i % perColumn * pitch;
				entry.Bounds.Width = width / columns - (columns > 1 ? 2 : 0);
				entry.Bounds.Height = pitch - (resources ? 1 : 0);

				var swatch = entry.Get<CitySwatchWidget>("SWATCH");
				var nameLabel = entry.Get<LabelWidget>("NAME");
				var valueLabel = entry.Get<LabelWidget>("VALUE");
				var iconWidget = entry.Get<CityIconWidget>("ICON");
				swatch.GetColor = () => color;
				nameLabel.GetText = () => name;
				valueLabel.Bounds.Width = entry.Bounds.Width;
				valueLabel.GetText = value ?? (() => "");
				if (resources)
				{
					iconWidget.Visible = true;
					iconWidget.Icon = icon;
					iconWidget.Size = 16;
					swatch.Bounds.X = 19;
					swatch.Bounds.Y = (entry.Bounds.Height - 10) / 2;
					swatch.Bounds.Width = 8;
					swatch.Bounds.Height = 10;
					nameLabel.Bounds.X = 31;
					nameLabel.Bounds.Width = entry.Bounds.Width - 31 - 30;
				}
				else
					nameLabel.Bounds.Width = entry.Bounds.Width - 16;
			}

			list.Bounds.Height = perColumn * pitch;
		}
	}
}
