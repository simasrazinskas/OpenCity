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
using System.IO;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// Draws the OpenCity chrome (panels, buttons, icons, glyphs, sidebar art) at the exact device scale.
	/// Collections opt in with `Generator: CityChrome` and describe what to draw with these keys:
	///   Style: palette file (uistyle.yaml), Icons: vector icon file (cityicons.vec)
	///   Panel: Button|Compact|Dialog|Raised|Well|Track|Tooltip|Toolbar|Item|Header|Bar|Frame, State: Normal|Hover|...
	///   IconSet: name + IconSize: n             every icon of a vector icon set
	///   OrderTiles: w, h                        all sidebar order tiles (+ -disabled / -active)
	///   Image: file.png + Regions               a 1x raster atlas, upscaled by a whole factor (nearest neighbour)
	///   Images: name: glyph|icon|tile|art|empty ...   individual images.
	/// </summary>
	public sealed partial class CityChromeGenerator : IChromeGenerator
	{
		ChromeGeneratorContext ctx;
		CityChromeStyle style;
		VectorIcons icons;
		float scale;
		int unit;

		/// <summary>Device pixels per icon art pixel: whole, and never so large that the icon loses resolution.</summary>
		int iconPixel;

		void Setup(IReadOnlyFileSystem fileSystem, float deviceScale, string stylePath, string iconPath)
		{
			scale = deviceScale;
			unit = ChromeGeneratorContext.PixelUnit(deviceScale);
			style = new CityChromeStyle(fileSystem, stylePath);
			using (var s = fileSystem.Open(iconPath))
				icons = new VectorIcons(s);

			iconPixel = style.Setting("IconPixels", "Scaled") == "Device" ? 1 : Math.Max(1, (int)Math.Floor(scale + 0.001f));
		}

		/// <summary>The load screen art (drawn before the chrome is loaded): the logo and a tile of the stripe band.</summary>
		public static (ChromeBitmap Logo, ChromeBitmap Stripe) RenderLoadScreen(IReadOnlyFileSystem fileSystem, float deviceScale,
			string stylePath, string iconPath)
		{
			var g = new CityChromeGenerator();
			g.Setup(fileSystem, deviceScale, stylePath, iconPath);
			return (g.Logo(256, 256).ToBitmap(), g.StripeTile().ToBitmap());
		}

		void IChromeGenerator.Generate(ChromeGeneratorContext context)
		{
			ctx = context;
			var first = context.Collections.Values.FirstOrDefault();
			if (first == null)
				return;

			Setup(context.FileSystem, context.Scale, Value(first, "Style") ?? "city|uistyle.yaml", Value(first, "Icons") ?? "cityicons.vec");

			// Panels first, so that images and aliases can refer to them
			foreach (var (name, yaml) in context.Collections)
			{
				var panel = Value(yaml, "Panel");
				if (panel != null)
					GeneratePanel(name, panel, Value(yaml, "State") ?? "Normal", yaml);
			}

			// Aliases are resolved last, so they may refer to images of any collection
			var aliases = new List<(string Collection, string Image, string Spec)>();
			foreach (var (name, yaml) in context.Collections)
			{
				var iconSet = Value(yaml, "IconSet");
				if (iconSet != null)
					GenerateIconSet(name, iconSet, Int(Value(yaml, "IconSize") ?? "32"));

				var tiles = Value(yaml, "OrderTiles");
				if (tiles != null)
					GenerateOrderTiles(name, Ints(tiles));

				var image = Value(yaml, "Image");
				if (image != null)
					GenerateRaster(name, image, yaml);

				var images = yaml.NodeWithKeyOrDefault("Images");
				if (images != null)
				{
					foreach (var n in images.Value.Nodes)
					{
						if (n.Value.Value?.StartsWith("alias ", StringComparison.Ordinal) == true)
							aliases.Add((name, n.Key, n.Value.Value));
						else
							GenerateImage(name, n.Key, n.Value.Value ?? "");
					}
				}
			}

			foreach (var (collection, image, spec) in aliases)
				GenerateImage(collection, image, spec);

			foreach (var name in context.Collections.Keys)
				if (!context.Contains(name))
					Log.Write("debug", $"CityChromeGenerator: collection `{name}` has nothing to draw.");
		}

		static string Value(MiniYaml yaml, string key)
		{
			var v = yaml.NodeWithKeyOrDefault(key)?.Value.Value;
			return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
		}

		static int Int(string s) { return int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture); }

		static int[] Ints(string s)
		{
			return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Int).ToArray();
		}

		/// <summary>Device pixels of a logical length (whole pixels, at least one).</summary>
		int D(float logical) { return Math.Max(1, (int)Math.Round(logical * scale, MidpointRounding.AwayFromZero)); }

		Rgba C(string name) { return style[name]; }

		void GenerateImage(string collection, string image, string spec)
		{
			var t = spec.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (t.Length == 0)
				throw new InvalidDataException($"Chrome image `{collection}/{image}` has no recipe.");

			switch (t[0])
			{
				case "empty":
					ctx.AddImage(collection, image, new ChromeBitmap(0, 0));
					break;
				case "glyph":
				case "glyph-disabled":
				{
					var icon = Glyph(t[1]);
					var w = t.Length > 3 ? Int(t[2]) : icon.Width;
					var h = t.Length > 3 ? Int(t[3]) : icon.Height;
					var canvas = RenderIcon(icon, w, h, 1f, 0.8f, t[0] == "glyph-disabled" ? Disabled : null);
					ctx.AddImage(collection, image, canvas.ToBitmap());
					break;
				}

				case "icon":
				{
					var parts = t[1].Split('/');
					var icon = icons.Set(parts[0])[parts[1]];
					var size = t.Length > 2 ? Int(t[2]) : icon.Width;
					ctx.AddImage(collection, image, RenderIcon(icon, size, size, 1f, 1.3f, null).ToBitmap());
					break;
				}

				case "tile":
				{
					var w = t.Length > 4 ? Int(t[3]) : 34;
					var h = t.Length > 4 ? Int(t[4]) : 35;
					ctx.AddImage(collection, image, OrderTile(t[1], t.Length > 2 ? t[2] : "normal", w, h).ToBitmap());
					break;
				}

				case "alias":
				{
					var parts = t[1].Split('/');
					if (!ctx.AddImageAlias(collection, image, parts[0], parts[1]))
						throw new InvalidDataException($"Chrome image alias `{collection}/{image}` refers to `{t[1]}`, which is not generated (yet).");

					break;
				}

				case "art":
					ctx.AddImage(collection, image, Art(t[1], Int(t[2]), Int(t[3])).ToBitmap());
					break;
				default:
					throw new InvalidDataException($"Unknown chrome image recipe `{t[0]}` for `{collection}/{image}`.");
			}
		}

		VectorIcons.Icon Glyph(string name)
		{
			if (icons.Set("glyphs").TryGetValue(name, out var g))
				return g;

			if (icons.Set("city").TryGetValue(name, out g))
				return g;

			throw new InvalidDataException($"No vector glyph named `{name}`.");
		}

		static Rgba Disabled(Rgba c) { return c.Desaturate(0.85f, 0.62f); }

		/// <summary>
		/// Renders a vector icon to a canvas of exactly D(w) x D(h) device pixels. The icon is drawn on a grid of
		/// iconPixel sized art pixels (then upscaled with nearest neighbour), outlined and bevelled like the D2k glyphs.
		/// </summary>
		ChromeCanvas RenderIcon(VectorIcons.Icon icon, int w, int h, float outline, float bevel, Func<Rgba, Rgba> tint)
		{
			int dw = D(w), dh = D(h);
			var p = iconPixel;
			int aw = (dw + p - 1) / p, ah = (dh + p - 1) / p;

			// Fit the design box into the art grid (keeping the aspect ratio)
			var k = Math.Min(aw / (float)icon.Width, ah / (float)icon.Height);
			var art = new ChromeCanvas(aw, ah, k);
			var ox = (aw / k - icon.Width) / 2;
			var oy = (ah / k - icon.Height) / 2;
			VectorIcons.Draw(icon, art, tint, ox, oy);

			// The outline is one art pixel when the art is smaller than or at the design size
			var outlinePixels = Math.Max(1, (int)Math.Round(outline * Math.Min(k, 1.5f), MidpointRounding.AwayFromZero));
			art.Stylize(style["IconOutline"], outline, bevel, 1f, 1.14f, 0.8f, outlinePixels);
			if (style.Flag("IconHardEdges"))
				art.ThresholdAlpha(128);

			return art.Upscale(p).Resize(dw, dh);
		}

		void GenerateIconSet(string collection, string set, int size)
		{
			foreach (var (name, icon) in icons.Set(set))
			{
				var small = size < icon.Width;

				// Small icons keep a one pixel outline and a softer bevel, so they stay readable
				var canvas = RenderIcon(icon, size, size, small ? 2f : 1f, small ? 2f : 1.3f, null);
				ctx.AddImage(collection, name, canvas.ToBitmap());
			}
		}

		/// <summary>A 1x raster atlas (e.g. build icons rendered from building sprites), upscaled by a whole factor.</summary>
		void GenerateRaster(string collection, string file, MiniYaml yaml)
		{
			Png png;
			using (var s = ctx.FileSystem.Open(file))
				png = new Png(s);

			var factor = Math.Max(1, (int)Math.Floor(scale + 0.001f));
			var regions = yaml.NodeWithKeyOrDefault("Regions");
			if (regions == null)
				return;

			foreach (var n in regions.Value.Nodes)
			{
				var r = Ints(n.Value.Value);
				var src = new ChromeCanvas(r[2], r[3]);
				for (var y = 0; y < r[3]; y++)
					for (var x = 0; x < r[2]; x++)
						src.Set(x, y, PngPixel(png, r[0] + x, r[1] + y));

				ctx.AddImage(collection, n.Key, src.Upscale(factor).Resize(D(r[2]), D(r[3])).ToBitmap());
			}
		}

		static Rgba PngPixel(Png png, int x, int y)
		{
			if (x < 0 || y < 0 || x >= png.Width || y >= png.Height)
				return Rgba.Transparent;

			switch (png.Type)
			{
				case SpriteFrameType.Indexed8:
				{
					var c = png.Palette[png.Data[y * png.Width + x]];
					return new Rgba(c.R, c.G, c.B, c.A);
				}

				case SpriteFrameType.Rgba32:
				{
					var i = 4 * (y * png.Width + x);
					return new Rgba(png.Data[i], png.Data[i + 1], png.Data[i + 2], png.Data[i + 3]);
				}

				case SpriteFrameType.Rgb24:
				{
					var i = 3 * (y * png.Width + x);
					return new Rgba(png.Data[i], png.Data[i + 1], png.Data[i + 2]);
				}

				default:
					throw new InvalidDataException($"Unsupported png pixel format {png.Type}.");
			}
		}
	}
}
