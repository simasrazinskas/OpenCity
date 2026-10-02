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
using System.Numerics;
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.City.UIArt;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>A widget that sets the colour scheme (window family) of its descendants.</summary>
	public interface ICityFamilyWidget
	{
		string Family { get; }
	}

	/// <summary>
	/// Runtime access to the RCT2-style theme (mods/city/uistyle.yaml): colour ramps, window families, text colours,
	/// the names of the per-family chrome art and helpers that draw it at device-pixel precision.
	/// </summary>
	public static class CityTheme
	{
		public const string DefaultFamily = CityChromeGenerator.DefaultFamily;
		const string StylePath = "city|uistyle.yaml";

		static CityChromeStyle style;
		static ModData styleModData;

		public static CityChromeStyle Style
		{
			get
			{
				if (style == null || styleModData != Game.ModData)
				{
					style = new CityChromeStyle(Game.ModData.DefaultFileSystem, StylePath);
					styleModData = Game.ModData;
					ArtExists.Clear();
				}

				return style;
			}
		}

		static Color ToColor(Rgba c)
		{
			return Color.FromArgb((int)Math.Round(c.A), (int)Math.Round(c.R), (int)Math.Round(c.G), (int)Math.Round(c.B));
		}

		/// <summary>Shade i (0 = darkest .. 7 = lightest) of a colour ramp.</summary>
		public static Color Ramp(string ramp, int i) { return ToColor(Style.Ramp(ramp, i)); }

		public static CityFamily Family(string family)
		{
			return Style.HasFamily(family ?? DefaultFamily) ? Style.Family(family ?? DefaultFamily) : Style.Family(DefaultFamily);
		}

		/// <summary>Shade of a family ramp relative to its body shade (0 = the body colour).</summary>
		public static Color Body(string family, int offset = 0)
		{
			var f = Family(family);
			return Ramp(f.Ramp, f.Body + offset);
		}

		public static Color FamilyShade(string family, int shade) { return Ramp(Family(family).Ramp, shade); }

		public static Color InkColor(string name) { return ToColor(Style.Ink(name)); }

		/// <summary>Body text on light window bodies.</summary>
		public static Color Ink => InkColor("Text");

		/// <summary>Text on dark wells (ticker, graphs).</summary>
		public static Color InkLight => InkColor("Light");

		public static Color MoneyPositive => InkColor("MoneyPositive");
		public static Color MoneyNegative => InkColor("MoneyNegative");
		public static Color MoneyPositiveLight => InkColor("MoneyPositiveLight");
		public static Color MoneyNegativeLight => InkColor("MoneyNegativeLight");

		/// <summary>Muted text with enough contrast to read unavailable actions and hints.</summary>
		public static Color Muted(string family) { return Body(family, -4); }

		/// <summary>The name of a part of the family art, e.g. Art("finance", "button") = "finance-button".</summary>
		public static string Art(string family, string part)
		{
			return (string.IsNullOrEmpty(family) ? DefaultFamily : family) + "-" + part;
		}

		/// <summary>The family of the nearest ancestor (or the widget itself) that sets one; the default family otherwise.</summary>
		public static string FamilyOf(Widget widget)
		{
			for (var w = widget; w != null; w = w.Parent)
				if (w is ICityFamilyWidget f && !string.IsNullOrEmpty(f.Family))
					return f.Family;

			return DefaultFamily;
		}

		/// <summary>The ramp whose middle shade is closest to a colour (maps legacy bar colours onto RCT ramps).</summary>
		public static string RampFor(Color c)
		{
			var best = "green";
			var bestDistance = float.MaxValue;
			foreach (var ramp in Style.RampNames)
			{
				var r = Style.Ramp(ramp, 4);
				float dr = r.R - c.R, dg = r.G - c.G, db = r.B - c.B;
				var d = dr * dr + dg * dg + db * db;
				if (d < bestDistance)
				{
					bestDistance = d;
					best = ramp;
				}
			}

			return best;
		}

		// ---- drawing ------------------------------------------------------------------------------

		/// <summary>DrawPanel at fractional logical coordinates (whole device pixels at fractional UI scales).</summary>
		public static void DrawPanel(string collection, float x, float y, float w, float h)
		{
			var sprites = ChromeProvider.TryGetPanelImages(collection);
			if (sprites == null || sprites.Length != 9 || w <= 0 || h <= 0)
				return;

			var top = sprites[1]?.Size.Y ?? 0;
			var left = sprites[3]?.Size.X ?? 0;
			var right = sprites[5]?.Size.X ?? 0;
			var bottom = sprites[7]?.Size.Y ?? 0;
			if (sprites[4] != null)
				WidgetUtils.FillRectWithSprite(x + left, y + top, w - left - right, h - top - bottom, sprites[4]);
			if (sprites[3] != null)
				WidgetUtils.FillRectWithSprite(x, y + top, left, h - top - bottom, sprites[3]);
			if (sprites[5] != null)
				WidgetUtils.FillRectWithSprite(x + w - right, y + top, right, h - top - bottom, sprites[5]);
			if (sprites[1] != null)
				WidgetUtils.FillRectWithSprite(x + left, y, w - left - right, top, sprites[1]);
			if (sprites[7] != null)
				WidgetUtils.FillRectWithSprite(x + left, y + h - bottom, w - left - right, bottom, sprites[7]);
			if (sprites[0] != null)
				WidgetUtils.DrawSprite(sprites[0], new Vector2(x, y));
			if (sprites[2] != null)
				WidgetUtils.DrawSprite(sprites[2], new Vector2(x + w - sprites[2].Size.X, y));
			if (sprites[6] != null)
				WidgetUtils.DrawSprite(sprites[6], new Vector2(x, y + h - sprites[6].Size.Y));
			if (sprites[8] != null)
				WidgetUtils.DrawSprite(sprites[8], new Vector2(x + w - sprites[8].Size.X, y + h - sprites[8].Size.Y));
		}

		public static void DrawPanel(string collection, Rectangle r) { DrawPanel(collection, r.X, r.Y, r.Width, r.Height); }

		/// <summary>Device pixels per logical UI pixel (window scale times UI scale).</summary>
		public static float DeviceScale => Game.Renderer?.WindowScale ?? 1f;

		/// <summary>The thickness of one bevel in logical pixels (one whole device pixel line, or more at large scales).</summary>
		public static float BevelLogical
		{
			get
			{
				var scale = DeviceScale;
				return Math.Max(1, (int)Math.Floor(scale + 0.001f)) / scale;
			}
		}

		/// <summary>Fills a rectangle whose edges are snapped to device pixels (crisp at fractional UI scales).</summary>
		public static void Fill(float x, float y, float w, float h, Color c)
		{
			if (w <= 0 || h <= 0)
				return;

			var s = DeviceScale;
			var x0 = PixelSnap.Snap(x, s);
			var y0 = PixelSnap.Snap(y, s);
			var x1 = Math.Max(x0 + 1 / s, PixelSnap.Snap(x + w, s));
			var y1 = Math.Max(y0 + 1 / s, PixelSnap.Snap(y + h, s));
			Game.Renderer.RgbaColorRenderer.FillRect(new Vector3(x0 - 0.5f, y0 - 0.5f, 0), new Vector3(x1 - 0.5f, y1 - 0.5f, 0), c);
		}

		/// <summary>An RCT bevel: light top/left and dark bottom/right lines, one bevel thick, around a rectangle (fill optional).</summary>
		public static void Bevel(float x, float y, float w, float h, Color light, Color dark, Color? fill = null)
		{
			var b = BevelLogical;
			if (fill is Color f)
				Fill(x, y, w, h, f);

			Fill(x, y, w, b, light);
			Fill(x, y, b, h, light);
			Fill(x, y + h - b, w, b, dark);
			Fill(x + w - b, y, b, h, dark);
		}

		/// <summary>An icon of the RCT2 set (bits/chrome/iso, names as in tools/iso_ui_icons_*.py) at a logical size.</summary>
		public static Sprite Icon(string name, int size = 16, bool disabled = false)
		{
			if (string.IsNullOrEmpty(name))
				return null;

			return ChromeProvider.TryGetImage("icons-" + size + (disabled ? "-disabled" : ""), name)
				?? (disabled ? ChromeProvider.TryGetImage("icons-" + size, name) : null);
		}

		/// <summary>A build-menu thumbnail (64 logical pixels) of an actor, or null.</summary>
		public static Sprite Thumbnail(string actor)
		{
			return string.IsNullOrEmpty(actor) ? null : ChromeProvider.TryGetImage("thumbs-64", actor);
		}

		/// <summary>Draws a sprite centred in a logical rectangle, on whole device pixels.</summary>
		public static void DrawCentered(Sprite sprite, float x, float y, float w, float h)
		{
			if (sprite == null)
				return;

			var scale = DeviceScale;
			var px = MathF.Round((x + (w - sprite.Size.X) / 2) * scale) / scale;
			var py = MathF.Round((y + (h - sprite.Size.Y) / 2) * scale) / scale;
			Game.Renderer.EnableAntialiasingFilter();
			WidgetUtils.DrawSprite(sprite, new Vector2(px, py));
			Game.Renderer.DisableAntialiasingFilter();
		}

		// ---- family theming of the shared widgets --------------------------------------------------
		static readonly Dictionary<string, bool> ArtExists = [];
		static readonly FieldInfo DropDownDecorations = typeof(DropDownButtonWidget).GetField("Decorations");
		static readonly FieldInfo DropDownSeparators = typeof(DropDownButtonWidget).GetField("Separators");

		static bool PanelExists(string name)
		{
			if (!ArtExists.TryGetValue(name, out var exists))
			{
				exists = ChromeProvider.TryGetPanelImages(name) != null || ChromeProvider.TryGetPanelImages(name + "-hover") != null;
				ArtExists[name] = exists;
			}

			return exists;
		}

		static bool IsFamilyArt(string value)
		{
			if (string.IsNullOrEmpty(value))
				return true;

			foreach (var f in Style.FamilyNames)
				if (value.Length > f.Length && value[f.Length] == '-' && value.StartsWith(f, StringComparison.Ordinal))
					return true;

			return value.StartsWith("bar-", StringComparison.Ordinal) || value.StartsWith("chip-", StringComparison.Ordinal);
		}

		/// <summary>The family version of a shared art name (`button` -> `finance-button`), or the name itself.</summary>
		static string Themed(string family, string value, string part = null)
		{
			if (IsFamilyArt(value))
				return value;

			var themed = Art(family, part ?? value);
			return PanelExists(themed) ? themed : value;
		}

		/// <summary>Moves the art of a window that changed its family from `from` to `to` (names forced to other families stay).</summary>
		public static void Refamily(Widget root, string from, string to)
		{
			var prefix = from + "-";
			string Swap(string value) =>
				value != null && value.StartsWith(prefix, StringComparison.Ordinal) ? Art(to, value[prefix.Length..]) : value;

			foreach (var child in root.Children)
			{
				if (child is ICityFamilyWidget nested && !string.IsNullOrEmpty(nested.Family))
					continue;

				switch (child)
				{
					case CheckboxWidget cb:
						cb.Background = Swap(cb.Background);
						if (cb.Checkmark != null && cb.Checkmark.StartsWith(prefix, StringComparison.Ordinal))
							cb.Checkmark = to + "-" + cb.Checkmark[prefix.Length..];
						break;
					case DropDownButtonWidget dd:
						dd.Background = Swap(dd.Background);
						if (DropDownDecorations?.GetValue(dd) is string dec)
							DropDownDecorations.SetValue(dd, Swap(dec));
						if (DropDownSeparators?.GetValue(dd) is string sep)
							DropDownSeparators.SetValue(dd, Swap(sep));
						break;
					case ButtonWidget b:
						b.Background = Swap(b.Background);
						break;
					case ScrollPanelWidget sp:
						sp.Background = Swap(sp.Background);
						sp.ScrollBarBackground = Swap(sp.ScrollBarBackground);
						sp.Button = Swap(sp.Button);
						sp.Decorations = Swap(sp.Decorations);
						break;
					case TextFieldWidget tf:
						tf.Background = Swap(tf.Background);
						break;
					case SliderWidget sl:
						sl.Track = Swap(sl.Track);
						sl.Thumb = Swap(sl.Thumb);
						break;
					case ProgressBarWidget pb:
						pb.Background = Swap(pb.Background);
						pb.Bar = Swap(pb.Bar);
						break;
					case CityPanelWidget:
						break;
					case BackgroundWidget bg:
						bg.Background = Swap(bg.Background);
						break;
				}

				Refamily(child, from, to);
			}
		}

		/// <summary>
		/// Gives every shared widget under root the art of a window family (buttons, checkboxes, lists, scrollbars, text
		/// fields, sliders, progress bars, backgrounds). Art names that already name a family are left alone, so this is
		/// cheap to repeat every frame and picks up rows that logic adds later.
		/// </summary>
		public static void ApplyFamily(Widget root, string family)
		{
			foreach (var child in root.Children)
			{
				if (child is ICityFamilyWidget nested && !string.IsNullOrEmpty(nested.Family) && nested.Family != family)
				{
					ApplyFamily(child, nested.Family);
					continue;
				}

				switch (child)
				{
					case DropDownButtonWidget dd:
						if (dd.Background == "button")
							dd.Background = Art(family, "dropdown");
						else
							dd.Background = Themed(family, dd.Background);

						if (DropDownDecorations?.GetValue(dd) is string dec && !IsFamilyArt(dec))
							DropDownDecorations.SetValue(dd, Art(family, dec));
						if (DropDownSeparators?.GetValue(dd) is string sep && !IsFamilyArt(sep))
							DropDownSeparators.SetValue(dd, Art(family, sep));
						break;
					case CheckboxWidget cb:
						cb.Background = Themed(family, cb.Background);
						if (cb.Checkmark is "tick" or "cross")
							cb.Checkmark = family + "-" + cb.Checkmark;
						break;
					case ButtonWidget b:
						b.Background = Themed(family, b.Background);
						break;
					case ScrollPanelWidget sp:
						if (!IsFamilyArt(sp.ScrollBarBackground))
							sp.ScrollBarBackground = Art(family, "scroll-trough");
						sp.Background = Themed(family, sp.Background);
						sp.Button = Themed(family, sp.Button);
						if (!IsFamilyArt(sp.Decorations))
							sp.Decorations = Art(family, sp.Decorations);
						break;
					case TextFieldWidget tf:
						tf.Background = Themed(family, tf.Background);
						break;
					case SliderWidget sl:
						sl.Track = Themed(family, sl.Track);
						sl.Thumb = Themed(family, sl.Thumb);
						break;
					case ProgressBarWidget pb:
						pb.Background = Themed(family, pb.Background);
						pb.Bar = Themed(family, pb.Bar);
						break;
					case CityPanelWidget:
						break;
					case BackgroundWidget bg:
						bg.Background = Themed(family, bg.Background);
						break;
				}

				ApplyFamily(child, family);
			}
		}
	}

	/// <summary>A rectangle in fractional logical pixels (RCT chrome is placed on whole device pixels).</summary>
	public readonly record struct CityRect(float X, float Y, float Width, float Height)
	{
		public float Right => X + Width;
		public float Bottom => Y + Height;
		public bool Contains(int2 p) { return p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height; }
	}
}
