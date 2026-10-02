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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// RCT2 section header (tools/iso_ui_parts.header): bold text in the dark shade of the window ramp, then an etched
	/// line to the right edge. Text is a fluent key (or GetText).
	/// </summary>
	public class CityHeaderWidget : Widget
	{
		[FluentReference]
		public string Text;
		public string Font = "TinyBold";
		public Func<string> GetText;

		public CityHeaderWidget() { }

		protected CityHeaderWidget(CityHeaderWidget other)
			: base(other)
		{
			Text = other.Text;
			Font = other.Font;
			GetText = other.GetText;
		}

		public override CityHeaderWidget Clone() { return new CityHeaderWidget(this); }

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
			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			var font = Game.Renderer.Fonts[Font];
			var text = GetText() ?? "";
			var size = font.Measure(text);
			var y = rb.Y + (rb.Height - size.Y - font.TopOffset) / 2;
			font.DrawText(text, new Vector2(rb.X, MathF.Round(y)), CityTheme.FamilyShade(family, 1));
			var x = rb.X + size.X + (size.X > 0 ? 5 : 0);
			var b = CityTheme.BevelLogical;
			var ly = rb.Y + rb.Height / 2f;
			CityTheme.Fill(x, ly, rb.Right - x, b, CityTheme.FamilyShade(family, 2));
			CityTheme.Fill(x, ly + b, rb.Right - x, b, CityTheme.FamilyShade(family, 7));
		}
	}

	/// <summary>RCT2 group box (tools/iso_ui_widgets2.groupbox): an etched rectangle whose top line is broken by the label.</summary>
	public class CityGroupBoxWidget : Widget
	{
		[FluentReference]
		public string Text;
		public string Font = "Tiny";
		public Func<string> GetText;

		public CityGroupBoxWidget() { }

		protected CityGroupBoxWidget(CityGroupBoxWidget other)
			: base(other)
		{
			Text = other.Text;
			Font = other.Font;
			GetText = other.GetText;
		}

		public override CityGroupBoxWidget Clone() { return new CityGroupBoxWidget(this); }

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
			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			var font = Game.Renderer.Fonts[Font];
			var text = GetText() ?? "";
			var size = font.Measure(text);
			var b = CityTheme.BevelLogical;
			var dark = CityTheme.FamilyShade(family, 2);
			var light = CityTheme.FamilyShade(family, 7);
			var top = rb.Y + size.Y / 2f;
			var gapStart = rb.X + 4;
			var gapEnd = text.Length > 0 ? gapStart + size.X + 6 : gapStart;

			void H(float x0, float x1, float y)
			{
				CityTheme.Fill(x0, y, x1 - x0, b, dark);
				CityTheme.Fill(x0 + b, y + b, x1 - x0 - b, b, light);
			}

			H(rb.X, gapStart, top);
			H(gapEnd, rb.Right - b, top);
			H(rb.X, rb.Right - b, rb.Bottom - 2 * b);
			CityTheme.Fill(rb.X, top, b, rb.Bottom - top - b, dark);
			CityTheme.Fill(rb.X + b, top + b, b, rb.Bottom - top - 3 * b, light);
			CityTheme.Fill(rb.Right - 2 * b, top, b, rb.Bottom - top - b, dark);
			CityTheme.Fill(rb.Right - b, top, b, rb.Bottom - top, light);

			if (text.Length > 0)
				font.DrawText(text, new Vector2(gapStart + 3, rb.Y), CityTheme.Ink);
		}
	}

	/// <summary>
	/// Background of a list row (design/iso/ui/system/ds-lists.png): zebra stripe on every second row of its list, light
	/// hover shade under the mouse, dark "selected" shade (IsSelected). Put it first in a row template, filling the row.
	/// </summary>
	public class CityRowBackgroundWidget : Widget
	{
		public bool Zebra = true;
		public bool Hover = true;
		public Func<bool> IsSelected = () => false;

		public CityRowBackgroundWidget() { }

		protected CityRowBackgroundWidget(CityRowBackgroundWidget other)
			: base(other)
		{
			Zebra = other.Zebra;
			Hover = other.Hover;
			IsSelected = other.IsSelected;
		}

		public override CityRowBackgroundWidget Clone() { return new CityRowBackgroundWidget(this); }

		/// <summary>Whether the row this background belongs to is drawn with the "selected" shade (white text expected).</summary>
		public bool Selected => IsSelected();

		public override void Draw()
		{
			var row = Parent;
			if (row == null)
				return;

			var family = CityTheme.FamilyOf(this);
			var rb = RenderBounds;
			string part = null;
			if (IsSelected())
				part = "item-selected";
			else if (Hover && row.RenderBounds.Contains(Viewport.LastMousePos))
				part = "item-hover";
			else if (Zebra && row.Parent != null && row.Parent.Children.IndexOf(row) % 2 == 1)
				part = "item-zebra";

			if (part != null)
				CityTheme.DrawPanel(CityTheme.Art(family, part), rb);
		}
	}
}
