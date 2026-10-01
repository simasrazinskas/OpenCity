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
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	public sealed partial class CityChromeGenerator
	{
		/// <summary>The width/height of the repeating middle of a panel bitmap, in device pixels.</summary>
		int Mid => unit * (int)Math.Ceiling(64f / unit);

		void GeneratePanel(string collection, string kind, string state, MiniYaml yaml)
		{
			var sides = PanelSides.All;
			var sidesValue = Value(yaml, "PanelSides");
			if (sidesValue != null)
				sides = FieldLoader.GetValue<PanelSides>("PanelSides", sidesValue);

			var u = unit;
			switch (kind)
			{
				case "Button":
				{
					var c = ButtonColors(state);
					AddBox(collection, 3, sides, (p, w, h) =>
					{
						p.Box(0, 0, w, h, [new Ring(c.Outline), new Ring(c.Light, c.Dark)], c.Face, true);
						p.HLine(2 * u, 2 * u, w - 4 * u, c.Sheen);
						p.HLine(2 * u, h - 3 * u, w - 4 * u, c.Shade);
					});
					break;
				}

				case "Compact":
				{
					var c = ButtonColors(state);
					AddBox(collection, 2, sides, (p, w, h) => p.Box(0, 0, w, h, [new Ring(c.Outline), new Ring(c.Light, c.Dark)], c.Face, true));
					break;
				}

				case "Dialog":
					AddBox(collection, 7, sides, (p, w, h) =>
					{
						p.Box(0, 0, w, h,
						[
							new Ring(C("Outline")), new Ring(C("Gold2"), C("Gold0")), new Ring(C("Red3"), C("Red1")),
							new Ring(C("Red2")), new Ring(C("Red1"), C("Red3")), new Ring(C("Outline")), new Ring(C("PanelDark"), C("Panel"))
						], C("Panel"), true);

						// Gold studs in the corners of the frame
						foreach (var (x, y) in new[] { (2 * u, 2 * u), (w - 4 * u, 2 * u), (2 * u, h - 4 * u), (w - 4 * u, h - 4 * u) })
							p.Stud(x, y, C("Gold3"), C("Gold4"), C("Gold1"));
					});
					break;
				case "Toolbar":
					AddBox(collection, 5, sides, (p, w, h) => p.Box(0, 0, w, h,
					[
						new Ring(C("Outline")), new Ring(C("Gold2"), C("Gold0")), new Ring(C("Red2"), C("Red0")),
						new Ring(C("Outline")), new Ring(C("PanelDark"), C("Panel"))
					], C("Panel"), true));
					break;
				case "Thumb":
				{
					// Slider thumbs: gold, so they stand out on the dark track
					var (light, face, dark) = state switch
					{
						"Hover" => (C("Gold4"), C("Gold3"), C("Gold1")),
						"Pressed" => (C("Gold1"), C("Gold2"), C("Gold3")),
						"Disabled" => (C("Grey3"), C("Grey2"), C("Grey1")),
						_ => (C("Gold4"), C("Gold2"), C("Gold0"))
					};

					AddBox(collection, 2, sides, (p, w, h) => p.Box(0, 0, w, h, [new Ring(C("Outline")), new Ring(light, dark)], face, true));
					break;
				}

				case "Raised":
					AddBox(collection, 2, sides, (p, w, h) =>
						p.Box(0, 0, w, h, [new Ring(C("Outline")), new Ring(C("PanelLight"), C("PanelDark"))], C("Panel"), true));
					break;
				case "Track":
				{
					var c = WellColors(state);
					AddBox(collection, 1, sides, (p, w, h) => p.Box(0, 0, w, h, [new Ring(C("Gold0"), C("Gold1"))], c.Face == C("Well") ? C("Outline") : c.Face));
					break;
				}

				case "Well":
				{
					var c = WellColors(state);
					AddBox(collection, 2, sides, (p, w, h) =>
						p.Box(0, 0, w, h, [new Ring(c.Light, c.Dark), new Ring(c.Shade, c.Face)], c.Face));
					break;
				}

				case "Tooltip":
				case "Frame":
					AddBox(collection, 3, sides, (p, w, h) =>
						p.Box(0, 0, w, h, [new Ring(C("Outline")), new Ring(C("Gold1"), C("Gold0")), new Ring(C("Outline"))], C("PanelDark"), true));
					break;
				case "Item":
				{
					var (edge, edgeDark, fill) = state switch
					{
						"Hover" => (C("Grey2"), C("Grey1"), C("ItemHover")),
						"Pressed" => (C("Red1"), C("Red2"), C("Red0")),
						"Highlighted" => (C("ItemSelectedEdge"), C("Gold0"), C("ItemSelected")),
						_ => throw new InvalidDataException($"Unknown Item state `{state}` for `{collection}`.")
					};

					AddBox(collection, 1, sides, (p, w, h) => p.Box(0, 0, w, h, [new Ring(edge, edgeDark)], fill));
					break;
				}

				case "Header":
					AddBox(collection, 1, sides, (p, w, h) => p.Box(0, 0, w, h, [new Ring(C("Red2"), C("Gold1"))], C("Red1")));
					break;
				case "Bar":
					AddBox(collection, 3, sides, (p, w, h) =>
					{
						p.Box(0, 0, w, h, [new Ring(C("Outline")), new Ring(C("Gold3"), C("Gold0"))], C("Gold2"), true);
						p.HLine(2 * u, 2 * u, w - 4 * u, C("Gold4"));
						p.HLine(2 * u, h - 3 * u, w - 4 * u, C("Gold1"));
					});
					break;
				case "Stripe":
					GenerateStripe(collection);
					break;
				default:
					throw new InvalidDataException($"Unknown chrome panel kind `{kind}` for `{collection}`.");
			}
		}

		/// <summary>Draws a box recipe into a bitmap with `border` art-pixel corners and a repeating middle, and adds it as a panel.</summary>
		void AddBox(string collection, int border, PanelSides sides, Action<PixelPainter, int, int> draw)
		{
			var b = border * unit;
			var size = 2 * b + Mid;
			var canvas = new ChromeCanvas(size, size);
			draw(new PixelPainter(canvas, unit), size, size);
			ctx.AddPanel(collection, canvas.ToBitmap(), b, b, b, b, sides);
		}

		readonly record struct BoxColors(Rgba Outline, Rgba Light, Rgba Dark, Rgba Face, Rgba Sheen, Rgba Shade);

		BoxColors ButtonColors(string state)
		{
			var o = C("Outline");
			return state switch
			{
				"Normal" => new BoxColors(o, C("Red4"), C("Red0"), C("Red2"), C("Red3"), C("Red1")),
				"Hover" => new BoxColors(o, C("Gold3"), C("Red1"), C("Red3"), C("Red4"), C("Red2")),
				"Pressed" => new BoxColors(o, C("Red0"), C("Red3"), C("Red1"), C("Red0"), C("Red1")),
				"Disabled" => new BoxColors(o, C("Grey2"), C("Grey0"), C("Grey1"), C("Grey2"), C("Grey0")),
				"Highlighted" => new BoxColors(o, C("Gold4"), C("Gold1"), C("Red2"), C("Gold2"), C("Red1")),
				"HighlightedHover" => new BoxColors(o, C("Gold4"), C("Gold2"), C("Red3"), C("Gold3"), C("Red2")),
				"HighlightedPressed" => new BoxColors(o, C("Gold1"), C("Gold3"), C("Red1"), C("Gold1"), C("Red1")),
				"HighlightedDisabled" => new BoxColors(o, C("Gold1"), C("Gold0"), C("Grey1"), C("Grey2"), C("Grey0")),
				_ => throw new InvalidDataException($"Unknown button state `{state}`.")
			};
		}

		/// <summary>Sunken boxes: Light/Dark = outer ring (top-left/bottom-right), Shade = inner shadow, Face = fill.</summary>
		BoxColors WellColors(string state)
		{
			var o = C("Outline");
			var shadow = C("Outline");
			return state switch
			{
				"Normal" => new BoxColors(o, C("WellEdgeDark"), C("WellEdge"), C("Well"), C("Well"), C("Outline")),
				"Hover" => new BoxColors(o, C("Gold1"), C("Gold2"), C("Well"), C("Well"), shadow),
				"Pressed" => new BoxColors(o, C("Gold0"), C("Gold1"), C("PanelDark"), C("PanelDark"), C("Well")),
				"Focused" => new BoxColors(o, C("Gold2"), C("Gold3"), C("Well"), C("Well"), shadow),
				"Disabled" => new BoxColors(o, C("Grey1"), C("Grey2"), C("Grey0"), C("Grey0"), C("Grey0")),
				"Highlighted" => new BoxColors(o, C("Gold1"), C("Gold3"), C("Red0"), C("Red0"), C("Well")),
				"HighlightedHover" => new BoxColors(o, C("Gold2"), C("Gold4"), C("Red0"), C("Red0"), C("Well")),
				"HighlightedPressed" => new BoxColors(o, C("Gold0"), C("Gold2"), C("Red0"), C("Red0"), C("Well")),
				"HighlightedDisabled" => new BoxColors(o, C("Gold0"), C("Grey2"), C("Grey0"), C("Grey0"), C("Grey0")),
				_ => throw new InvalidDataException($"Unknown well state `{state}`.")
			};
		}

		/// <summary>The load screen band: a dark steel stripe with gold edges, as a centre-only panel 256 logical pixels high.</summary>
		void GenerateStripe(string collection)
		{
			ctx.AddPanel(collection, StripeTile().ToBitmap(), 0, 0, 0, 0, PanelSides.Center);
		}

		ChromeCanvas StripeTile()
		{
			var h = D(256);
			var w = Mid;
			var canvas = new ChromeCanvas(w, h);
			var p = new PixelPainter(canvas, unit);
			int y0 = D(100), y1 = D(156);
			p.Fill(0, y0, w, y1 - y0, C("PanelDark"));
			p.HLine(0, y0, w, C("Gold2"));
			p.HLine(0, y0 + unit, w, C("Gold0"));
			p.HLine(0, y1 - 2 * unit, w, C("Gold0"));
			p.HLine(0, y1 - unit, w, C("Gold2"));
			return canvas;
		}
	}
}
