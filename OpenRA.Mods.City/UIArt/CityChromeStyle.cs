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
	/// <summary>A window colour scheme: the ramp of its chrome, the body shade and an accent ramp (slider fills).</summary>
	public sealed record CityFamily(string Name, string Ramp, int Body, string Accent);

	/// <summary>The palette and settings of the procedural chrome (mods/city/uistyle.yaml).</summary>
	public sealed class CityChromeStyle
	{
		readonly Dictionary<string, Rgba> palette = [];
		readonly Dictionary<string, string> settings = [];
		readonly Dictionary<string, Rgba[]> ramps = [];
		readonly Dictionary<string, CityFamily> families = [];
		readonly List<string> familyOrder = [];
		readonly Dictionary<string, Rgba> ink = [];

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
					case "Ramps":
						foreach (var c in n.Value.Nodes)
						{
							var parts = c.Value.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
							if (parts.Length != 8)
								throw new InvalidDataException($"{path}: ramp `{c.Key}` needs 8 colours.");

							ramps[c.Key] = parts.Select(Rgba.Parse).ToArray();
						}

						break;
					case "Families":
						foreach (var c in n.Value.Nodes)
						{
							var parts = c.Value.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
							if (parts.Length != 3)
								throw new InvalidDataException($"{path}: family `{c.Key}` needs a ramp, a body shade and an accent ramp.");

							families[c.Key] = new CityFamily(c.Key, parts[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), parts[2]);
							familyOrder.Add(c.Key);
						}

						break;
					case "Ink":
						foreach (var c in n.Value.Nodes)
							ink[c.Key] = Rgba.Parse(c.Value.Value);
						break;
				}
			}

			foreach (var f in families.Values)
				if (!ramps.ContainsKey(f.Ramp) || !ramps.ContainsKey(f.Accent))
					throw new InvalidDataException($"{path}: family `{f.Name}` uses an unknown ramp.");
		}

		/// <summary>Shade i (0 = darkest .. 7 = lightest, clamped) of a colour ramp.</summary>
		public Rgba Ramp(string ramp, int i)
		{
			if (!ramps.TryGetValue(ramp, out var r))
				throw new KeyNotFoundException($"The UI style has no ramp `{ramp}`.");

			return r[Math.Clamp(i, 0, 7)];
		}

		public bool HasRamp(string ramp) { return ramps.ContainsKey(ramp); }

		public IEnumerable<string> RampNames => ramps.Keys;

		/// <summary>Window families in declaration order.</summary>
		public IReadOnlyList<string> FamilyNames => familyOrder;

		public CityFamily Family(string name)
		{
			if (families.TryGetValue(name, out var f))
				return f;

			throw new KeyNotFoundException($"The UI style has no window family `{name}`.");
		}

		public bool HasFamily(string name) { return families.ContainsKey(name); }

		/// <summary>A text colour of the RCT chrome (Text, Light, Shadow, MoneyPositive ...).</summary>
		public Rgba Ink(string name)
		{
			if (ink.TryGetValue(name, out var c))
				return c;

			throw new KeyNotFoundException($"The UI style has no ink colour `{name}`.");
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
	}
}
