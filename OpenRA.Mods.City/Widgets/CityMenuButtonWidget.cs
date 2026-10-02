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
	/// Big RCT2 menu button (tools/iso_ui_menus.mbtn): a raised button of the window family with an icon (in a sunken
	/// well when Well is set), a bold label, an optional smaller subtitle line below it and an optional hotkey hint on
	/// the right. Default marks the suggested action with a yellow frame.
	/// </summary>
	public class CityMenuButtonWidget : ButtonWidget, ICityFamilyWidget
	{
		public string Family { get; set; }

		public string Icon;
		public int IconSize = 24;
		public bool Well = true;

		/// <summary>Colour ramp of the icon well (default: the window family).</summary>
		public string WellRamp;

		/// <summary>Space between the button edge and the icon well, and between the well and the text (logical pixels).</summary>
		public int Pad = 3;

		[FluentReference]
		public string Sub;
		public Func<string> GetSub;
		public Func<string> GetHint;
		public bool Default;
		public Func<bool> IsDefault;

		[ObjectCreator.UseCtor]
		public CityMenuButtonWidget(ModData modData)
			: base(modData)
		{
			IsDefault = () => Default;
			Contrast = false;
			Shadow = false;
			Align = TextAlign.Left;
			VisualHeight = 0;
		}

		protected CityMenuButtonWidget(CityMenuButtonWidget other)
			: base(other)
		{
			Family = other.Family;
			Icon = other.Icon;
			IconSize = other.IconSize;
			Well = other.Well;
			WellRamp = other.WellRamp;
			Pad = other.Pad;
			Sub = other.Sub;
			GetSub = other.GetSub;
			GetHint = other.GetHint;
			Default = other.Default;
			IsDefault = other.IsDefault;
		}

		public override CityMenuButtonWidget Clone() { return new CityMenuButtonWidget(this); }

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);
			if (GetSub == null && Sub != null)
			{
				var text = FluentProvider.GetMessage(Sub);
				GetSub = () => text;
			}
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var family = string.IsNullOrEmpty(Family) ? CityTheme.FamilyOf(this) : Family;
			var disabled = IsDisabled();
			var hover = Ui.MouseOverWidget == this && !disabled;
			var pressed = Depressed && !disabled;
			var b = CityTheme.BevelLogical;

			if (IsDefault() && !disabled)
				CityTheme.Fill(rb.X - b, rb.Y - b, rb.Width + 2 * b, rb.Height + 2 * b, CityTheme.Ramp("yellow", 5));

			DrawBackground(Background, rb, disabled, pressed, hover, IsHighlighted());

			var shift = pressed ? b : 0;
			float x = rb.X + shift, y = rb.Y + shift;
			var h = rb.Height;
			float textX;
			if (Well)
			{
				var side = h - 2 * Pad;
				CityTheme.Bevel(x + Pad, y + Pad, side, side, CityTheme.FamilyShade(family, 2), CityTheme.FamilyShade(family, 7),
					WellRamp != null ? CityTheme.Ramp(WellRamp, 3) : CityTheme.Body(family, -2));
				CityTheme.DrawCentered(CityTheme.Icon(Icon, IconSize, disabled), x + Pad, y + Pad, side, side);
				textX = x + Pad + side + 8;
			}
			else
			{
				var side = Math.Max(IconSize, h - 2 * Pad);
				CityTheme.DrawCentered(CityTheme.Icon(Icon, IconSize, disabled), x + Pad, y, side, h);
				textX = x + Pad + side + 4;
			}

			var font = Game.Renderer.Fonts[Font];
			var small = Game.Renderer.Fonts["Tiny"];
			var text = GetText() ?? "";
			var sub = GetSub?.Invoke();
			var textHeight = font.Measure(text).Y;
			const int Gap = 2;
			var total = string.IsNullOrEmpty(sub) ? textHeight : textHeight + Gap + small.Measure(sub).Y;
			var ty = MathF.Round(y + (h - total) / 2f) - font.TopOffset / 2f;
			var ink = disabled ? CityTheme.Muted(family) : CityTheme.Ink;
			font.DrawText(text, new Vector2(textX, ty), ink);
			if (!string.IsNullOrEmpty(sub))
				small.DrawText(sub, new Vector2(textX, MathF.Round(ty + textHeight + Gap)), disabled ? ink : CityTheme.FamilyShade(family, 1));

			var hint = GetHint?.Invoke();
			if (!string.IsNullOrEmpty(hint))
			{
				var size = small.Measure(hint);
				small.DrawText(hint, new Vector2(x + rb.Width - Pad - 2 - size.X, MathF.Round(y + (h - size.Y) / 2f)), disabled ? ink : CityTheme.FamilyShade(family, 1));
			}
		}
	}
}
