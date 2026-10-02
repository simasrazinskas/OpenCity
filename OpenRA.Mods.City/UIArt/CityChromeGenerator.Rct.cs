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
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// The RCT2-style chrome (design: mods/city/design/iso/ui, recipes: tools/iso_ui_chrome.py, iso_ui_widgets*.py).
	/// A collection with `Rct: true` (and `Dynamic: true`) makes the generator draw the whole widget kit once per
	/// window family of uistyle.yaml: `F-window`, `F-button-hover`, `F-well`, ... for every family F, and the same
	/// art of the default family (`city`) without a prefix under the names the shared OpenRA widgets expect
	/// (button, dialog, checkbox, scrollpanel-bg, textfield ...). Bevels are floor(scale) device pixels thick, so the
	/// look is identical at every UI scale; all panels are nine-slices with uniform (or tiling) edges.
	/// </summary>
	public sealed partial class CityChromeGenerator
	{
		public const string DefaultFamily = "city";

		/// <summary>Bevel thickness in device pixels: floor(scale), at least one.</summary>
		int bv;

		/// <summary>Device pixels of a logical length (rounded, at least one).</summary>
		int S(float logical) { return Math.Max(1, (int)Math.Round(logical * scale, MidpointRounding.AwayFromZero)); }

		enum Kind { Flat, Out, In }

		/// <summary>An RCT box: optional outline ring, a bevel (light top-left when raised) and a fill.</summary>
		readonly record struct RctBox(Rgba? Outline, Kind Kind, Rgba? Fill, Rgba Light, Rgba Dark, Rgba? Ring = null, bool Shadow = false);

		Rgba Rr(string ramp, int i) { return style.Ramp(ramp, i); }

		/// <summary>RCT bevel exactly as tools/iso_ui_chrome.bevel: top/left rows first, bottom/right overwrite the corners.</summary>
		static void Bevel(ChromeCanvas c, int x, int y, int w, int h, Kind kind, Rgba? fill, Rgba light, Rgba dark, int thickness)
		{
			if (fill is Rgba f)
				c.Fill(x, y, w, h, f);

			if (kind == Kind.Flat)
				return;

			var (tl, br) = kind == Kind.Out ? (light, dark) : (dark, light);
			for (var i = 0; i < thickness; i++)
			{
				c.Fill(x + i, y + i, w - 2 * i, 1, tl);
				c.Fill(x + i, y + i, 1, h - 2 * i, tl);
				c.Fill(x + i, y + h - 1 - i, w - 2 * i, 1, br);
				c.Fill(x + w - 1 - i, y + i, 1, h - 2 * i, br);
			}
		}

		/// <summary>Draws a box recipe into a w x h canvas; returns the border width in device pixels.</summary>
		int DrawBox(ChromeCanvas c, RctBox box, int w, int h)
		{
			var shadow = box.Shadow ? S(2) : 0;
			w -= shadow;
			h -= shadow;
			if (shadow > 0)
			{
				var black = new Rgba(0, 0, 0);
				c.Fill(shadow, h, w, shadow, black);
				c.Fill(w, shadow, shadow, h, black);
			}

			var d = 0;
			if (box.Ring is Rgba ring)
			{
				c.Fill(0, 0, w, h, ring);
				d += bv;
			}

			if (box.Outline is Rgba o)
			{
				c.Fill(d, d, w - 2 * d, h - 2 * d, o);
				d += bv;
			}

			Bevel(c, d, d, w - 2 * d, h - 2 * d, box.Kind, box.Fill, box.Light, box.Dark, box.Kind == Kind.Flat ? 0 : bv);
			return d + (box.Kind == Kind.Flat ? 0 : bv);
		}

		void AddRctPanel(string name, RctBox box, PanelSides sides = PanelSides.All)
		{
			var shadow = box.Shadow ? S(2) : 0;
			var border = (box.Ring != null ? bv : 0) + (box.Outline != null ? bv : 0) + (box.Kind == Kind.Flat ? 0 : bv);
			var size = 2 * border + Mid + shadow;
			var c = new ChromeCanvas(size, size);
			DrawBox(c, box, size, size);
			ctx.AddPanel(name, c.ToBitmap(), border, border, border + shadow, border + shadow, sides);
		}

		/// <summary>A panel that draws nothing (flat buttons in their normal state).</summary>
		void AddEmptyPanel(string name)
		{
			ctx.AddPanel(name, new ChromeCanvas(Mid, Mid).ToBitmap(), 0, 0, 0, 0, PanelSides.Center);
		}

		void GenerateRct()
		{
			bv = Math.Max(1, (int)Math.Floor(scale + 0.001f));

			// Tooltips are always the pale yellow of the tooltip family, with a hard drop shadow
			var tip = style.Family(style.HasFamily("tooltip") ? "tooltip" : DefaultFamily).Ramp;
			AddRctPanel("tooltip", new RctBox(Rr(tip, 0), Kind.Out, Rr(tip, 7), Rr(tip, 7), Rr(tip, 5), null, true));

			foreach (var name in style.FamilyNames)
			{
				var f = style.Family(name);
				GenerateFamily(name + "-", f);
				if (name == DefaultFamily)
					GenerateFamily("", f);
			}

			foreach (var ramp in style.RampNames)
			{
				GenerateBar("bar-" + ramp, ramp, true);
				GenerateBar("bar-" + ramp + "-smooth", ramp, false);
				AddRctPanel("chip-" + ramp, new RctBox(Rr(ramp, 0), Kind.Out, Rr(ramp, 4), Rr(ramp, 6), Rr(ramp, 2)));
			}

			GenerateRctImages();
		}

		void GenerateFamily(string p, CityFamily f)
		{
			var r = f.Ramp;
			var body = f.Body;
			Rgba R(int i) => Rr(r, i);
			var o = R(0);

			// Windows, title bars, raised and sunken areas
			AddRctPanel(p + "window", new RctBox(o, Kind.Out, R(body), R(7), R(2)));
			AddRctPanel(p + "raised", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "titlebar", new RctBox(null, Kind.Out, R(3), R(5), R(1)));
			AddRctPanel(p + "titlebar-inactive", new RctBox(null, Kind.Out, R(2), R(4), R(1)));
			AddRctPanel(p + "well", new RctBox(null, Kind.In, R(7), R(7), R(2)));
			AddRctPanel(p + "well-dark", new RctBox(null, Kind.In, R(1), R(5), R(0)));
			AddRctPanel(p + "trough", new RctBox(null, Kind.In, R(1), R(7), R(1)));
			AddRctPanel(p + "toolbar", new RctBox(o, Kind.Out, R(body - 1), R(6), R(1)));
			AddRctPanel(p + "statusbar", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "graph", new RctBox(null, Kind.In, style.Ink("Graph"), R(7), R(1)));
			AddTab(p + "tab", R(body - 1), R(7), R(1));
			AddTab(p + "tab-hover", R(body), R(7), R(1));
			AddTab(p + "tab-active", R(body), R(7), R(1));
			AddEtched(p + "etched-h", R(2), R(7), true);
			AddEtched(p + "etched-v", R(2), R(7), false);
			AddGroupBox(p + "groupbox", R(2), R(7));

			// Text buttons (ButtonWidget state names) and the same art for scrollbar buttons
			foreach (var b in new[] { "button", "button-purchase", "scrollpanel-button" })
			{
				AddRctPanel(p + b, new RctBox(null, Kind.Out, R(body), R(7), R(1)));
				AddRctPanel(p + b + "-hover", new RctBox(null, Kind.Out, R(body + 1), R(7), R(1)));
				AddRctPanel(p + b + "-pressed", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));
				AddRctPanel(p + b + "-disabled", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted", new RctBox(null, Kind.In, R(3), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-hover", new RctBox(null, Kind.In, R(4), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-pressed", new RctBox(null, Kind.In, R(2), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-disabled", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));
			}

			// Default (focused) button: outlined
			AddRctPanel(p + "button-default", new RctBox(o, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "button-default-hover", new RctBox(o, Kind.Out, R(body + 1), R(7), R(1)));
			AddRctPanel(p + "button-default-pressed", new RctBox(o, Kind.In, R(body - 1), R(7), R(1)));
			AddRctPanel(p + "button-default-disabled", new RctBox(o, Kind.Out, R(body), R(7), R(1)));

			// Flat icon buttons (toolbar buttons, tool icons): only a bevel when hovered, sunk when pressed or toggled
			foreach (var b in new[] { "iconbutton", "checkbox-toggle" })
			{
				AddEmptyPanel(p + b);
				AddRctPanel(p + b + "-hover", new RctBox(null, Kind.Out, null, R(7), R(1)));
				AddRctPanel(p + b + "-pressed", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));
				AddEmptyPanel(p + b + "-disabled");
				AddRctPanel(p + b + "-highlighted", new RctBox(null, Kind.In, R(body - 2), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-hover", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-pressed", new RctBox(null, Kind.In, R(body - 2), R(7), R(1)));
				AddRctPanel(p + b + "-highlighted-disabled", new RctBox(null, Kind.In, R(body - 2), R(7), R(1)));
			}

			// Text fields and dropdown fields: sunken paper
			foreach (var b in new[] { "textfield", "dropdown" })
			{
				AddRctPanel(p + b, new RctBox(null, Kind.In, R(7), R(7), R(1)));
				AddRctPanel(p + b + "-hover", new RctBox(null, Kind.In, R(7), R(7), R(1)));
				AddRctPanel(p + b + "-pressed", new RctBox(null, Kind.In, R(7), R(7), R(1)));
				AddRctPanel(p + b + "-disabled", new RctBox(null, Kind.In, R(body), R(7), R(1)));
				AddRctPanel(p + b + "-focused", new RctBox(null, Kind.In, R(7), R(7), R(1), Rr("yellow", 5)));
				AddRctPanel(p + b + "-highlighted", new RctBox(null, Kind.In, R(7), R(7), R(1), Rr("yellow", 5)));
				AddRctPanel(p + b + "-highlighted-hover", new RctBox(null, Kind.In, R(7), R(7), R(1), Rr("yellow", 5)));
				AddRctPanel(p + b + "-highlighted-pressed", new RctBox(null, Kind.In, R(7), R(7), R(1), Rr("yellow", 5)));
				AddRctPanel(p + b + "-highlighted-disabled", new RctBox(null, Kind.In, R(body), R(7), R(1)));
			}

			// Checkboxes: small sunken squares
			foreach (var hl in new[] { "", "-highlighted" })
			{
				AddRctPanel(p + "checkbox" + hl, new RctBox(null, Kind.In, R(7), R(7), R(1)));
				AddRctPanel(p + "checkbox" + hl + "-hover", new RctBox(null, Kind.In, R(6), R(7), R(1)));
				AddRctPanel(p + "checkbox" + hl + "-pressed", new RctBox(null, Kind.In, R(6), R(7), R(1)));
				AddRctPanel(p + "checkbox" + hl + "-disabled", new RctBox(null, Kind.In, R(body), R(7), R(1)));
			}

			// Sliders and scrollbars
			AddRctPanel(p + "slider-track", new RctBox(null, Kind.In, R(2), R(7), R(1)));
			AddRctPanel(p + "slider-thumb", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "slider-thumb-hover", new RctBox(null, Kind.Out, R(body + 1), R(7), R(1)));
			AddRctPanel(p + "slider-thumb-pressed", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));
			AddRctPanel(p + "slider-thumb-disabled", new RctBox(null, Kind.Out, R(body - 1), R(7), R(1)));
			AddRctPanel(p + "scroll-trough", new RctBox(null, Kind.In, R(body - 1), R(7), R(1)));

			// List rows: hover, selected, zebra stripe, column header
			var zebra = Rgba.Lerp(R(7), R(6), 0.55f);
			AddRctPanel(p + "item-hover", new RctBox(null, Kind.Flat, R(5), R(5), R(5)));
			AddRctPanel(p + "item-selected", new RctBox(null, Kind.Flat, R(3), R(3), R(3)));
			AddRctPanel(p + "item-zebra", new RctBox(null, Kind.Flat, zebra, zebra, zebra));
			AddRctPanel(p + "item-header", new RctBox(null, Kind.Out, R(body), R(7), R(2)));

			// Build menu cards
			AddRctPanel(p + "card", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "card-hover", new RctBox(null, Kind.Out, R(body + 1), R(7), R(1)));
			AddRctPanel(p + "card-selected", new RctBox(null, Kind.In, R(body - 1), R(7), R(1), Rr("yellow", 6)));
			AddRctPanel(p + "card-locked", new RctBox(null, Kind.Out, R(body), R(7), R(1)));
			AddRctPanel(p + "card-well", new RctBox(null, Kind.In, Rgba.Lerp(R(2), Rr("grey", 3), 0.6f), R(4), R(0)));

			// Names used by the shared OpenRA widgets (common chrome yaml)
			Alias(p + "dialog", p + "window");
			Alias(p + "dialog2", p + "raised");
			Alias(p + "dialog3", p + "well");
			Alias(p + "dialog4", "tooltip");
			AddRctPanel(p + "dialog5", new RctBox(o, Kind.Out, R(body), R(7), R(2)), PanelSides.Center);
			Alias(p + "separator", p + "etched-h");
			Alias(p + "scrollheader", p + "item-header");
			Alias(p + "scrollheader-highlighted", p + "item-header");
			Alias(p + "scrollpanel-bg", p + "well");
			AddEmptyPanel(p + "scrollitem");
			AddEmptyPanel(p + "scrollitem-nohover");
			Alias(p + "scrollitem-hover", p + "item-hover");
			Alias(p + "scrollitem-pressed", p + "item-selected");
			Alias(p + "scrollitem-highlighted", p + "item-selected");
			Alias(p + "scrollitem-nohover-highlighted", p + "item-selected");
			Alias(p + "progressbar-bg", p + "trough");
			GenerateBar(p + "progressbar-thumb", f.Accent, true);
			GenerateFamilyImages(p, f);
		}

		void Alias(string name, string source)
		{
			if (!ctx.AddPanelAlias(name, source))
				throw new InvalidOperationException($"RCT chrome alias `{name}` refers to `{source}`, which is not drawn (yet).");
		}

		/// <summary>An unselected / selected tab: light top and left edges, dark right edge, open at the bottom.</summary>
		void AddTab(string name, Rgba fill, Rgba light, Rgba dark)
		{
			var b = bv;
			var size = 2 * b + Mid;
			var c = new ChromeCanvas(size, size);
			c.Fill(0, 0, size, size, fill);
			c.Fill(b, 0, size - 2 * b, b, light);
			c.Fill(0, b, b, size - b, light);
			c.Fill(size - b, b, b, size - b, dark);
			ctx.AddPanel(name, c.ToBitmap(), b, b, b, 0);
		}

		/// <summary>An etched separator: a dark then a light line, each one bevel thick.</summary>
		void AddEtched(string name, Rgba dark, Rgba light, bool horizontal)
		{
			var b = bv;
			if (horizontal)
			{
				var c = new ChromeCanvas(Mid, 2 * b);
				c.Fill(0, 0, Mid, b, dark);
				c.Fill(0, b, Mid, b, light);
				ctx.AddPanel(name, c.ToBitmap(), 0, b, 0, b, PanelSides.Top | PanelSides.Bottom);
			}
			else
			{
				var c = new ChromeCanvas(2 * b, Mid);
				c.Fill(0, 0, b, Mid, dark);
				c.Fill(b, 0, b, Mid, light);
				ctx.AddPanel(name, c.ToBitmap(), b, 0, b, 0, PanelSides.Left | PanelSides.Right);
			}
		}

		/// <summary>An etched rectangle (group boxes): dark outer, light inner line on every side.</summary>
		void AddGroupBox(string name, Rgba dark, Rgba light)
		{
			var b = bv;
			var size = 4 * b + Mid;
			var c = new ChromeCanvas(size, size);
			c.Fill(0, 0, size, b, dark);
			c.Fill(0, 0, b, size, dark);
			c.Fill(b, b, size - 2 * b, b, light);
			c.Fill(b, b, b, size - 2 * b, light);
			c.Fill(0, size - 2 * b, size, b, dark);
			c.Fill(size - 2 * b, 0, b, size, dark);
			c.Fill(0, size - b, size, b, light);
			c.Fill(size - b, 0, b, size, light);
			ctx.AddPanel(name, c.ToBitmap(), 2 * b, 2 * b, 2 * b, 2 * b, PanelSides.Edges);
		}

		/// <summary>
		/// Meter fill (tools/iso_ui_widgets2.meter): ramp shade 4 with a light top third and a dark bottom line; segmented
		/// bars get a dark divider every 5 logical pixels. The middle tile is exactly one segment wide, so the dividers
		/// repeat seamlessly at any bar length.
		/// </summary>
		void GenerateBar(string name, string ramp, bool segmented)
		{
			var b = bv;
			var top = Math.Max(1, S(6) / 3);
			var period = segmented ? S(5) : Mid;
			var midH = Mid;
			var h = top + midH + b;
			var c = new ChromeCanvas(period, h);
			c.Fill(0, 0, period, h, Rr(ramp, 4));
			c.Fill(0, 0, period, top, Rr(ramp, 6));
			c.Fill(0, h - b, period, b, Rr(ramp, 3));
			if (segmented)
				c.Fill(period - b, 0, b, h, Rr(ramp, 2));

			ctx.AddPanel(name, c.ToBitmap(), 0, top, 0, b, PanelSides.Top | PanelSides.Center | PanelSides.Bottom);
		}
	}
}
