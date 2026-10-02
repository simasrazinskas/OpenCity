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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// The brown tagline plate of the title screens (design/iso/ui/screens/mainmenu-*.png): light text on a dark-edged
	/// brown plate that is sized to its text and centred in the widget.
	/// </summary>
	public class CityPlateWidget : Widget
	{
		[FluentReference]
		public string Text;
		public string Font = "Bold";
		public Func<string> GetText;

		static readonly Color Edge = Color.FromArgb(0x1a, 0x0c, 0x04);
		static readonly Color Fill = Color.FromArgb(0x7a, 0x3a, 0x10);
		static readonly Color Light = Color.FromArgb(0xc2, 0x7a, 0x30);
		static readonly Color Ink = Color.FromArgb(0xff, 0xe9, 0xa8);
		static readonly Color Shadow = Color.FromArgb(0x2a, 0x12, 0x04);

		public CityPlateWidget() { }

		protected CityPlateWidget(CityPlateWidget other)
			: base(other)
		{
			Text = other.Text;
			Font = other.Font;
			GetText = other.GetText;
		}

		public override CityPlateWidget Clone() { return new CityPlateWidget(this); }

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);
			if (GetText == null)
			{
				var text = Text != null ? FluentProvider.GetMessage(Text) : "";
				GetText = () => text;
			}
		}

		public override void Draw()
		{
			var text = GetText() ?? "";
			if (text.Length == 0)
				return;

			var rb = RenderBounds;
			var font = Game.Renderer.Fonts[Font];
			var size = font.Measure(text);
			var b = CityTheme.BevelLogical;
			var w = size.X + 16;
			var h = rb.Height;
			var x = rb.X + (rb.Width - w) / 2f;
			CityTheme.Fill(x - 2 * b, rb.Y - 2 * b, w + 4 * b, h + 4 * b, Edge);
			CityTheme.Fill(x, rb.Y, w, h, Fill);
			CityTheme.Fill(x, rb.Y, w, b, Light);
			font.DrawTextWithShadow(text, new Vector2(MathF.Round(x + 8), MathF.Round(rb.Y + (h - size.Y - font.TopOffset) / 2f)), Ink, Shadow, Shadow, 1);
		}
	}
}
