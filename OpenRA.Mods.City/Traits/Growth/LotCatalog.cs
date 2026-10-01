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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>One lot shape of a zone: W cells along the road, D cells deep, with the two actors that realise it.</summary>
	public sealed class LotShape
	{
		public int W;
		public int D;

		/// <summary>Actor for a road to the north or south (footprint W x D).</summary>
		public string ActorNS;

		/// <summary>Actor for a road to the east or west (footprint D x W).</summary>
		public string ActorEW;

		public int Area => W * D;
	}

	/// <summary>
	/// Data-driven catalogue: for every zone, the lot shapes for which actors named "prefix-WxD" exist in the rules.
	/// Rectangular shapes need both orientations. Shapes are ordered like the CS2 spawn rule: deepest first, then widest.
	/// </summary>
	public sealed class LotCatalog
	{
		public const int MaxWidth = 4;
		public const int MaxDepth = 3;

		readonly List<LotShape>[] shapes;
		readonly GrowableBuildingInfo[] infos;
		readonly int[] minWidth;
		readonly int[] minDepth;

		public LotCatalog(Ruleset rules)
		{
			var zones = Enum.GetValues<ZoneType>().Length;
			shapes = new List<LotShape>[zones];
			infos = new GrowableBuildingInfo[zones];
			minWidth = new int[zones];
			minDepth = new int[zones];
			for (var z = 1; z < zones; z++)
			{
				shapes[z] = [];
				var prefix = ((ZoneType)z).GrowablePrefix();
				if (prefix == null)
					continue;

				for (var d = MaxDepth; d >= 1; d--)
				{
					for (var w = MaxWidth; w >= 1; w--)
					{
						var ns = prefix + "-" + w + "x" + d;
						var ew = prefix + "-" + d + "x" + w;
						if (!rules.Actors.TryGetValue(ns, out var nsInfo) || !rules.Actors.ContainsKey(ew))
							continue;

						var growable = nsInfo.TraitInfoOrDefault<GrowableBuildingInfo>();
						if (growable == null || growable.Zone != (ZoneType)z)
							continue;

						infos[z] ??= growable;
						shapes[z].Add(new LotShape { W = w, D = d, ActorNS = ns, ActorEW = ew });
					}
				}

				var min = int.MaxValue;
				var minD = int.MaxValue;
				foreach (var s in shapes[z])
				{
					min = Math.Min(min, s.W);
					minD = Math.Min(minD, s.D);
				}

				minWidth[z] = min == int.MaxValue ? 0 : min;
				minDepth[z] = minD == int.MaxValue ? 0 : minD;
			}
		}

		public IReadOnlyList<LotShape> Shapes(ZoneType zone) { return shapes[(int)zone]; }

		/// <summary>Archetype data of the zone (any of its actors), or null if the zone has no actors.</summary>
		public GrowableBuildingInfo InfoOf(ZoneType zone) { return infos[(int)zone]; }

		/// <summary>Smallest frontage width of the zone's shapes (0 = zone cannot grow).</summary>
		public int MinWidth(ZoneType zone) { return minWidth[(int)zone]; }

		/// <summary>Smallest lot depth of the zone's shapes (0 = zone cannot grow).</summary>
		public int MinDepth(ZoneType zone) { return minDepth[(int)zone]; }

		public bool CanGrow(ZoneType zone) { return shapes[(int)zone].Count > 0; }
	}
}
