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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// A compact status readout: optional 16 px icon, a label on the left, the value on the right and a thin
	/// progress bar underneath. Text is drawn on the dark panel, never on top of the coloured bar, so it stays
	/// readable at any fill level. When label and value do not both fit, the label is dropped (it stays in the tooltip).
	/// </summary>
	public class CityStatWidget : Widget
	{
		public string TooltipContainer;
		public string TooltipTemplate = "CITY_TOOLTIP";
		public string Font = "Small";
		public string ValueFont = "Bold";
		public string IconCollection = "city-icons-small";
		public string Icon;
		public int BarHeight = 4;
		public Color TrackColor = Color.FromArgb(255, 30, 36, 44);
		public Color LabelColor = CityUi.Muted;

		public Func<string> GetLabel = () => "";
		public Func<string> GetValue = () => "";
		public Func<Color> GetValueColor = () => Color.White;
		public Func<int> GetPercentage = () => 0;
		public Func<Color> GetBarColor = () => CityUi.Accent;
		public Func<string> GetTooltipText;

		static readonly string[] FallbackFonts = ["Small", "Tiny"];

		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		public CityStatWidget()
		{
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected CityStatWidget(CityStatWidget other)
			: base(other)
		{
			TooltipContainer = other.TooltipContainer;
			TooltipTemplate = other.TooltipTemplate;
			Font = other.Font;
			ValueFont = other.ValueFont;
			IconCollection = other.IconCollection;
			Icon = other.Icon;
			BarHeight = other.BarHeight;
			TrackColor = other.TrackColor;
			LabelColor = other.LabelColor;
			GetLabel = other.GetLabel;
			GetValue = other.GetValue;
			GetValueColor = other.GetValueColor;
			GetPercentage = other.GetPercentage;
			GetBarColor = other.GetBarColor;
			GetTooltipText = other.GetTooltipText;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override CityStatWidget Clone() { return new CityStatWidget(this); }

		public override void MouseEntered()
		{
			if (TooltipContainer != null && GetTooltipText != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "getText", GetTooltipText } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null && tooltipContainer.IsValueCreated)
				tooltipContainer.Value.RemoveTooltip();
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var textLeft = rb.X;

			var icon = Icon != null ? ChromeProvider.TryGetImage(IconCollection, Icon) : null;
			if (icon != null)
			{
				var size = new int2((int)icon.Size.X, (int)icon.Size.Y);
				WidgetUtils.DrawSprite(icon, new Vector2(rb.X, rb.Y + (rb.Height - size.Y) / 2));
				textLeft += size.X + 4;
			}

			// Bar along the bottom, from the text column to the right edge.
			var barRect = new Rectangle(textLeft, rb.Bottom - BarHeight, rb.Right - textLeft, BarHeight);
			WidgetUtils.FillRectWithColor(barRect, TrackColor);
			var fill = barRect.Width * Math.Clamp(GetPercentage(), 0, 100) / 100;
			if (fill > 0)
				WidgetUtils.FillRectWithColor(new Rectangle(barRect.X, barRect.Y, fill, barRect.Height), GetBarColor());

			// Text line above the bar: label and value share one baseline, caps centred in the remaining height.
			var textHeight = rb.Height - BarHeight - 1;
			var font = Game.Renderer.Fonts[Font];
			var valueFont = Game.Renderer.Fonts[ValueFont];
			var baseline = rb.Y + (textHeight + Ascender(valueFont)) / 2;

			// The value never runs into the icon: step down to smaller fonts when it is too wide for the cell.
			var value = GetValue() ?? "";
			var valueSize = valueFont.Measure(value);
			foreach (var smaller in FallbackFonts)
			{
				if (valueSize.X <= rb.Right - textLeft || !Game.Renderer.Fonts.TryGetValue(smaller, out var f))
					break;

				valueFont = f;
				valueSize = f.Measure(value);
			}

			valueFont.DrawTextWithShadow(value, new Vector2(rb.Right - valueSize.X, baseline - valueSize.Y), GetValueColor(), Color.Black, Color.Black, 1);

			var label = GetLabel() ?? "";
			if (label.Length == 0)
				return;

			var labelSize = font.Measure(label);
			if (labelSize.X > rb.Right - valueSize.X - 6 - textLeft)
				return;

			font.DrawTextWithShadow(label, new Vector2(textLeft, baseline - labelSize.Y), LabelColor, Color.Black, Color.Black, 1);
		}

		/// <summary>Cap height of a font in pixels (its line size minus the top offset).</summary>
		public static int Ascender(SpriteFont font)
		{
			return font.Measure("").Y - font.TopOffset;
		}
	}
}
