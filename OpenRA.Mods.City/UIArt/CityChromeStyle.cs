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
using System.IO;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>The palette and settings of the procedural chrome (mods/city/uistyle.yaml).</summary>
	public sealed class CityChromeStyle
	{
		readonly Dictionary<string, Rgba> palette = [];
		readonly Dictionary<string, string> settings = [];
		readonly Dictionary<string, (Rgba Dark, Rgba Light)> orderTiles = [];

		public CityChromeStyle(IReadOnlyFileSystem fileSystem, string path)
		{
			IEnumerable<MiniYamlNode> nodes;
			using (var stream = fileSystem.Open(path))
				nodes = MiniYaml.FromStream(stream, path).ToList();

			foreach (var n in nodes)
			{
				switch (n.Key)
				{
					case "Palette":
						foreach (var c in n.Value.Nodes)
							palette[c.Key] = Rgba.Parse(c.Value.Value);
						break;
					case "Settings":
						foreach (var c in n.Value.Nodes)
							settings[c.Key] = c.Value.Value;
						break;
					case "OrderTiles":
						foreach (var c in n.Value.Nodes)
						{
							var parts = c.Value.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
							if (parts.Length != 2)
								throw new InvalidDataException($"{path}: order tile `{c.Key}` needs two colours.");

							orderTiles[c.Key] = (Rgba.Parse(parts[0]), Rgba.Parse(parts[1]));
						}

						break;
				}
			}
		}

		/// <summary>A palette colour; unknown names fail loudly so typos are caught on the first draw.</summary>
		public Rgba this[string name]
		{
			get
			{
				if (palette.TryGetValue(name, out var c))
					return c;

				throw new KeyNotFoundException($"The UI palette has no colour `{name}`.");
			}
		}

		public string Setting(string name, string fallback)
		{
			return settings.TryGetValue(name, out var v) ? v : fallback;
		}

		public bool Flag(string name)
		{
			return settings.TryGetValue(name, out var v) && v.Equals("true", StringComparison.OrdinalIgnoreCase);
		}

		public IEnumerable<string> OrderTileNames => orderTiles.Keys.OrderBy(k => k, StringComparer.Ordinal);

		public (Rgba Dark, Rgba Light) OrderTile(string name)
		{
			return orderTiles.TryGetValue(name, out var t) ? t : (this["Grey1"], this["Grey3"]);
		}
	}
}
