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
using System.IO;

namespace OpenRA.Mods.City.UIArt
{
	public sealed partial class CityChromeGenerator
	{
		/// <summary>Device pixel position of a logical coordinate (no minimum, unlike D).</summary>
		int P(float logical) { return (int)Math.Round(logical * scale, MidpointRounding.AwayFromZero); }

		/// <summary>Fixed-size art (sidebar pieces, bars, separators), drawn at D(w) x D(h) device pixels.</summary>
		ChromeCanvas Art(string recipe, int w, int h)
		{
			var canvas = new ChromeCanvas(D(w), D(h));
			var p = new PixelPainter(canvas, unit);
			switch (recipe)
			{
				case "sidebar-top": SidebarTop(p); break;
				case "sidebar-row": SidebarRow(p); break;
				case "sidebar-plainrow": SidebarEdges(p); break;
				case "sidebar-bottom": SidebarBottom(p); break;
				case "commandbar": CommandBar(p); break;
				case "minimap-tl": MinimapCorner(p, false, false); break;
				case "minimap-tr": MinimapCorner(p, true, false); break;
				case "minimap-bl": MinimapCorner(p, false, true); break;
				case "minimap-br": MinimapCorner(p, true, true); break;
				case "minimap-top": MinimapBar(p, true); break;
				case "minimap-side": MinimapBar(p, false); break;
				case "slider-tick": p.Fill((canvas.Width - unit) / 2, 0, unit, canvas.Height, C("Gold2")); break;
				case "separator": DropSeparator(p, C("Gold1")); break;
				case "separator-hover": DropSeparator(p, C("Gold3")); break;
				case "separator-pressed": DropSeparator(p, C("Gold0")); break;
				case "separator-disabled": DropSeparator(p, C("Grey2")); break;
				case "logo": return Logo(w, h);
				default: throw new InvalidDataException($"Unknown chrome art recipe `{recipe}`.");
			}

			return canvas;
		}

		/// <summary>Left and right edges of the sidebar column: outline, gold trim, red bevel, outline.</summary>
		void SidebarEdges(PixelPainter p)
		{
			var u = unit;
			var w = p.Canvas.Width;
			var h = p.Canvas.Height;
			p.Fill(0, 0, w, h, C("Panel"));
			var left = new[] { C("Outline"), C("Gold2"), C("Red3"), C("Red2"), C("Outline") };
			var right = new[] { C("Outline"), C("Gold0"), C("Red1"), C("Red2"), C("Outline") };
			for (var i = 0; i < left.Length; i++)
			{
				p.VLine(i * u, 0, h, left[i]);
				p.VLine(w - (i + 1) * u, 0, h, right[i]);
			}
		}

		/// <summary>A sunken well around a logical rectangle (the content goes inside).</summary>
		void Well(PixelPainter p, float x, float y, float w, float h, bool gold, bool subtle = false)
		{
			int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
			var edge = gold ? new Ring(C("Gold0"), C("Gold2")) : subtle ? new Ring(C("Outline"), C("PanelLight")) : new Ring(C("Outline"), C("WellEdge"));
			p.Box(x0, y0, x1 - x0, y1 - y0, [edge, new Ring(C("PanelDark"), C("Well"))], C("Well"));
		}

		/// <summary>The top band of a frame (outline, gold, red bevel, outline), u units per line.</summary>
		void FrameBand(PixelPainter p, int y, bool bottom)
		{
			var u = unit;
			var w = p.Canvas.Width;
			Rgba[] rows = bottom
				? [C("Outline"), C("Red1"), C("Red2"), C("Gold1"), C("Outline")]
				: [C("Outline"), C("Gold2"), C("Red3"), C("Red2"), C("Outline")];
			for (var i = 0; i < rows.Length; i++)
				p.Fill(0, bottom ? y - (i + 1) * u : y + i * u, w, u, rows[i]);
		}

		void SidebarTop(PixelPainter p)
		{
			SidebarEdges(p);
			FrameBand(p, 0, false);

			// Cash / date strip and the minimap well
			Well(p, 8, 4, 210, 23, false);
			var coin = RenderIcon(Glyph("coin"), 16, 16, 1f, 0.8f, null);
			p.Canvas.Blit(coin, P(11), P(7));
			Well(p, 10, 32, 206, 206, true);

			// Groove under the order buttons
			p.HLine(P(12), P(280), P(202), C("Outline"));
			p.HLine(P(12), P(280) + unit, P(202), C("PanelLight"));
		}

		void SidebarRow(PixelPainter p)
		{
			SidebarEdges(p);

			// Tab column and three icon slots (CityPaletteWidget: IconX 39, 58x48 slots, 2px apart)
			Well(p, 6, 0, 25, 48, false, true);
			for (var c = 0; c < 3; c++)
				Well(p, 39 + c * 60, 1, 58, 46, false, true);
		}

		void SidebarBottom(PixelPainter p)
		{
			SidebarEdges(p);
			FrameBand(p, p.Canvas.Height, true);
		}

		void CommandBar(PixelPainter p)
		{
			var u = unit;
			var w = p.Canvas.Width;
			var h = p.Canvas.Height;
			p.Box(0, 0, w, h + 6 * u,
			[
				new Ring(C("Outline")), new Ring(C("Gold2"), C("Gold0")), new Ring(C("Red3"), C("Red1")),
				new Ring(C("Red2")), new Ring(C("Outline")), new Ring(C("PanelDark"), C("Panel"))
			], C("Panel"), true);
		}

		void MinimapCorner(PixelPainter p, bool right, bool bottom)
		{
			var u = unit;
			var w = p.Canvas.Width;
			var h = p.Canvas.Height;
			var x = right ? w - 2 * u : 0;
			var y = bottom ? h - 2 * u : 0;
			p.Fill(0, y, w, 2 * u, C("Outline"));
			p.Fill(x, 0, 2 * u, h, C("Outline"));
			p.Fill(right ? 0 : u, y + (bottom ? 0 : u), w - u, u, C("Gold3"));
			p.Fill(x + (right ? 0 : u), right || bottom ? 0 : u, u, h - u, C("Gold3"));
		}

		void MinimapBar(PixelPainter p, bool horizontal)
		{
			var w = p.Canvas.Width;
			var h = p.Canvas.Height;
			if (horizontal)
			{
				p.Fill(0, 0, w, Math.Min(h, 2 * unit), C("Outline"));
				p.HLine(unit, 0, w - 2 * unit, C("Gold2"));
			}
			else
			{
				p.Fill(0, 0, Math.Min(w, 2 * unit), h, C("Outline"));
				p.VLine(0, unit, h - 2 * unit, C("Gold2"));
			}
		}

		void DropSeparator(PixelPainter p, Rgba c)
		{
			var h = p.Canvas.Height;
			var inset = P(3);
			p.Fill((p.Canvas.Width - unit) / 2, inset, unit, h - 2 * inset, c);
		}

		/// <summary>A sidebar order tile: chamfered bevelled tile in the kind's colour with a light glyph.</summary>
		ChromeCanvas OrderTile(string kind, string state, int w, int h)
		{
			int dw = D(w), dh = D(h);
			var u = unit;
			int aw = dw / u, ah = dh / u;
			var art = new ChromeCanvas(aw, ah);
			var a = new PixelPainter(art, 1);
			var (dark, light) = style.OrderTile(kind);
			Rgba frameLight, frameDark;
			switch (state)
			{
				case "disabled":
					dark = dark.Desaturate(0.8f, 0.6f);
					light = light.Desaturate(0.8f, 0.6f);
					(frameLight, frameDark) = (C("Grey3"), C("Grey1"));
					break;
				case "active":
					(dark, light) = (light, light.Shade(1.2f));
					(frameLight, frameDark) = (C("Gold4"), C("Gold2"));
					break;
				default:
					(frameLight, frameDark) = (C("Gold3"), C("Gold0"));
					break;
			}

			a.Box(0, 0, aw, ah, [new Ring(C("Outline")), new Ring(frameLight, frameDark), new Ring(C("Outline"))], null, true);

			// Body: banded vertical gradient (pixel art style), light at the top
			const int Bands = 5;
			var bodyH = ah - 6;
			for (var y = 0; y < bodyH; y++)
			{
				var band = Math.Min(Bands - 1, y * Bands / Math.Max(1, bodyH));
				a.Fill(3, 3 + y, aw - 6, 1, Rgba.Lerp(light, dark, band / (float)(Bands - 1)));
			}

			// Chamfered top-left corner
			var ch = Math.Max(3, aw / 5);
			for (var i = 0; i < ch; i++)
			{
				art.Clear(0, i, ch - i, 1);
				art.Set(ch - i, i, C("Outline"));
				if (ch - i + 1 < aw)
					art.Set(ch - i + 1, i, frameLight);
			}

			var tile = art.Upscale(u).Resize(dw, dh);

			// Glyph: the city icon of the same name, recoloured to sand (gold when active, grey when disabled)
			var glyphName = kind switch { "options" => "tools", "power" => "bolt", _ => kind };
			var glyphTint = state switch
			{
				"disabled" => (Func<Rgba, Rgba>)(c => Mono(c, C("Grey3"))),
				"active" => c => Mono(c, C("Gold4")),
				_ => c => Mono(c, C("Sand"))
			};

			var size = (int)Math.Round(Math.Min(w, h) * 0.7f);
			var glyph = RenderIcon(Glyph(glyphName), size, size, 1f, 1.3f, glyphTint);
			tile.Blit(glyph, (dw - glyph.Width) / 2 + unit, (dh - glyph.Height) / 2 + unit);
			return tile;
		}

		/// <summary>Maps a colour to a single hue by its brightness (keeps alpha).</summary>
		static Rgba Mono(Rgba c, Rgba target)
		{
			var l = (0.3f * c.R + 0.59f * c.G + 0.11f * c.B) / 255f;
			var k = 0.55f + 0.6f * l;
			return new Rgba(Math.Min(255, target.R * k), Math.Min(255, target.G * k), Math.Min(255, target.B * k), c.A);
		}

		void GenerateOrderTiles(string collection, int[] size)
		{
			foreach (var kind in style.OrderTileNames)
			{
				var w = kind == "options" ? 40 : size[0];
				var h = kind == "options" ? 38 : size[1];
				ctx.AddImage(collection, kind, OrderTile(kind, "normal", w, h).ToBitmap());
				ctx.AddImage(collection, kind + "-disabled", OrderTile(kind, "disabled", w, h).ToBitmap());
				ctx.AddImage(collection, kind + "-active", OrderTile(kind, "active", w, h).ToBitmap());
			}
		}
	}
}
