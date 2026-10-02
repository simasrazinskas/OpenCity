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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Tick marks and percentage labels under a UI scale slider (design/iso/ui/panels/settings-display.png): a tick at every
	/// multiple of Step (percent) inside the slider's range, aligned with the slider thumb, the one at the slider's value in green.
	/// Place it right under the slider (same X and Width); it reads the slider named by For from its parent.
	/// </summary>
	public class CityTickLabelsWidget : Widget
	{
		public string For;
		public int Step = 50;
		public string Font = "Tiny";
		SliderWidget slider;

		public CityTickLabelsWidget() { }

		protected CityTickLabelsWidget(CityTickLabelsWidget other)
			: base(other)
		{
			For = other.For;
			Step = other.Step;
			Font = other.Font;
		}

		public override CityTickLabelsWidget Clone() { return new CityTickLabelsWidget(this); }

		public override void Draw()
		{
			slider ??= Parent?.GetOrNull<SliderWidget>(For);
			if (slider == null || slider.MaximumValue <= slider.MinimumValue)
				return;

			var rb = RenderBounds;
			var sb = slider.RenderBounds;
			var font = Game.Renderer.Fonts[Font];
			var family = CityTheme.FamilyOf(this);
			var value = (int)MathF.Round(slider.GetValue() * 100);
			var min = slider.MinimumValue * 100;
			var max = slider.MaximumValue * 100;
			var b = CityTheme.BevelLogical;
			for (var pct = (int)MathF.Ceiling(min / Step - 0.01f) * Step; pct <= max + 0.01f; pct += Step)
			{
				var x = sb.X + sb.Height / 2f + (sb.Width - sb.Height) * (pct - min) / (max - min);
				CityTheme.Fill(x - b / 2, rb.Y, b, 3, CityTheme.FamilyShade(family, 2));

				var text = pct + "%";
				var size = font.Measure(text);
				var tx = Math.Clamp(x - size.X / 2f, rb.X, rb.Right - size.X);
				font.DrawText(text, new Vector2(MathF.Round(tx), rb.Y + 5 - font.TopOffset),
					Math.Abs(pct - value) < Step / 2 ? CityTheme.MoneyPositive : CityTheme.Ink);
			}
		}
	}

	/// <summary>
	/// Live UI scale preview (design/iso/ui/panels/settings-display.png): a well with a sample button, icon and text drawn
	/// in proportion to the scale the UI scale slider (For) points at, the slider's largest scale filling the well. It follows
	/// the slider while it is dragged, before the scale is applied.
	/// </summary>
	public class CityScalePreviewWidget : Widget
	{
		static readonly int[] IconSizes = [12, 16, 20, 24, 32];

		[FluentReference]
		public string Text;
		public string Icon = "pnl_save";
		public string For;
		string text;
		SliderWidget slider;

		public CityScalePreviewWidget() { }

		protected CityScalePreviewWidget(CityScalePreviewWidget other)
			: base(other)
		{
			Text = other.Text;
			Icon = other.Icon;
			For = other.For;
		}

		public override CityScalePreviewWidget Clone() { return new CityScalePreviewWidget(this); }

		public override void Draw()
		{
			for (var w = Parent; w != null && slider == null; w = w.Parent)
				slider = w.GetOrNull<SliderWidget>(For);

			text ??= Text != null ? FluentProvider.GetMessage(Text) : "";
			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			CityTheme.DrawPanel(CityTheme.Art(family, "well"), rb.X, rb.Y, rb.Width, rb.Height);

			var scale = slider?.GetValue() ?? Game.Renderer.UIScale;
			var largest = Math.Max(scale, slider?.MaximumValue ?? scale);

			// A 52x16 button with an icon, and a line of text below it: as large as the well allows at the slider's
			// largest scale, in proportion to the scale chosen. The text size steps with the scale.
			var textFont = Game.Renderer.Fonts[scale < 1.4f ? "Tiny" : scale < 2.2f ? "Regular" : "Bold"];
			var textSize = textFont.Measure(text);
			const float Pad = 4f;
			var fit = Math.Min((rb.Width - 2 * Pad) / 52f, (rb.Height - 2 * Pad - textSize.Y) / (16f + 4f));
			var r = fit * scale / largest;
			var bw = MathF.Round(52 * r);
			var bh = MathF.Round(16 * r);
			var bx = MathF.Round(rb.X + (rb.Width - bw) / 2);
			var by = MathF.Round(rb.Y + (rb.Height - bh - 4 * r - textSize.Y) / 2);
			CityTheme.DrawPanel(CityTheme.Art(family, "button-default"), bx, by, bw, bh);

			var iconSize = IconSizes[0];
			foreach (var s in IconSizes)
				if (s <= bh - 2)
					iconSize = s;

			CityTheme.DrawCentered(CityTheme.Icon(Icon, iconSize), bx, by, bw, bh);
			textFont.DrawText(text, new Vector2(MathF.Round(rb.X + (rb.Width - textSize.X) / 2f), MathF.Round(by + bh + 4 * r)), CityTheme.Ink);
		}
	}
}
