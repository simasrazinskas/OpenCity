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

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// Resolution independent icon drawings (bits/chrome/*.vec, exported from the tools/ui*.py recipes by
	/// tools/uiexport.py). Each icon is a list of vector primitives in design units, replayed onto a ChromeCanvas.
	/// </summary>
	public sealed class VectorIcons
	{
		public sealed class Icon
		{
			public readonly string Name;
			public readonly int Width;
			public readonly int Height;
			internal readonly List<string[]> Ops = [];

			public Icon(string name, int width, int height)
			{
				Name = name;
				Width = width;
				Height = height;
			}
		}

		readonly Dictionary<string, Dictionary<string, Icon>> sets = [];

		public VectorIcons(Stream stream)
		{
			Dictionary<string, Icon> set = null;
			Icon icon = null;
			using var reader = new StreamReader(stream);
			string line;
			var lineNumber = 0;
			while ((line = reader.ReadLine()) != null)
			{
				lineNumber++;
				line = line.Trim();
				if (line.Length == 0 || line[0] == '#')
					continue;

				var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				switch (t[0])
				{
					case "set":
						if (!sets.TryGetValue(t[1], out set))
							sets[t[1]] = set = [];
						icon = null;
						break;
					case "icon":
						icon = new Icon(t[1], Int(t[2]), Int(t[3]));
						set[t[1]] = icon;
						break;
					case "alias":
						if (set.TryGetValue(t[2], out var target))
							set[t[1]] = target;
						break;
					default:
						if (icon == null)
							throw new InvalidDataException($"Vector icon op outside of an icon on line {lineNumber}.");

						icon.Ops.Add(t);
						break;
				}
			}
		}

		public IReadOnlyDictionary<string, Icon> Set(string name)
		{
			return sets.TryGetValue(name, out var s) ? s : [];
		}

		static int Int(string s) { return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture); }
		static float F(string s) { return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }

		/// <summary>Colour transform applied while replaying (e.g. to recolour glyphs).</summary>
		public static Paint P(string s, Func<Rgba, Rgba> tint)
		{
			if (s == "-")
				return null;

			var c = Rgba.Parse(s);
			if (tint != null)
				c = tint(c);

			return ChromeCanvas.Solid(c);
		}

		/// <summary>Replays an icon; the canvas Scale maps design units to device pixels, (ox, oy) offsets in design units.</summary>
		public static void Draw(Icon icon, ChromeCanvas g, Func<Rgba, Rgba> tint = null, float ox = 0, float oy = 0)
		{
			foreach (var t in icon.Ops)
			{
				switch (t[0])
				{
					case "R":
					{
						var radii = t[6] == "-" ? null : new[] { F(t[6]), F(t[7]), F(t[8]), F(t[9]) };
						g.RRect(F(t[1]) + ox, F(t[2]) + oy, F(t[3]), F(t[4]), F(t[5]), P(t[10], tint), P(t[11], tint), F(t[12]), radii);
						break;
					}

					case "C":
						g.Circle(F(t[1]) + ox, F(t[2]) + oy, F(t[3]), P(t[4], tint), P(t[5], tint), F(t[6]));
						break;
					case "E":
						g.Ellipse(F(t[1]) + ox, F(t[2]) + oy, F(t[3]), F(t[4]), P(t[5], tint));
						break;
					case "G":
						g.Ring(F(t[1]) + ox, F(t[2]) + oy, F(t[3]), F(t[4]), P(t[5], tint));
						break;
					case "L":
						g.Line(F(t[1]) + ox, F(t[2]) + oy, F(t[3]) + ox, F(t[4]) + oy, F(t[5]), P(t[6], tint), t[7] == "1");
						break;
					case "P":
					{
						var pts = new List<(float, float)>();
						for (var i = 2; i + 1 < t.Length; i += 2)
							pts.Add((F(t[i]) + ox, F(t[i + 1]) + oy));

						g.Poly(pts, P(t[1], tint));
						break;
					}

					case "B":
					{
						var c = Rgba.Parse(t[5]);
						g.Rect(F(t[1]) + ox, F(t[2]) + oy, F(t[3]), F(t[4]), tint != null ? tint(c) : c);
						break;
					}

					default:
						throw new InvalidDataException($"Unknown vector icon op `{t[0]}` in icon `{icon.Name}`.");
				}
			}
		}
	}
}
