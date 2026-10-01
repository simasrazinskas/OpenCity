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
	/// <summary>
	/// Deterministic breadth-first search over IRoadNetwork (N, E, S, W order, respects CanEnter). Scratch state only,
	/// so a separate instance can be used from UI code (route previews) without influencing the simulation.
	/// </summary>
	public sealed class TransitRouter
	{
		readonly Func<CPos, bool> passable;
		readonly Func<CPos, int, bool> canEnter;
		readonly CellLayer<int> stamp;
		readonly CellLayer<byte> stepDir;
		readonly Queue<CPos> queue = new();
		int generation;

		/// <summary>Search over the road network.</summary>
		public TransitRouter(Map map, IRoadNetwork roads)
			: this(map, roads.IsRoad, roads.CanEnter)
		{
		}

		/// <summary>Search over any cell network: `passable` says whether a cell is part of it, `canEnter` whether a step in direction d (index into Neighbours4) is allowed.</summary>
		public TransitRouter(Map map, Func<CPos, bool> passable, Func<CPos, int, bool> canEnter)
		{
			this.passable = passable;
			this.canEnter = canEnter;
			stamp = new CellLayer<int>(map);
			stepDir = new CellLayer<byte>(map);
		}

		/// <summary>Path from `from` to `to` (inclusive), or null when there is none. A path from a cell to itself is that single cell.</summary>
		public CPos[] FindPath(CPos from, CPos to, int maxExplored = 60000)
		{
			if (!stamp.Contains(from) || !stamp.Contains(to) || !passable(from) || !passable(to))
				return null;

			if (from == to)
				return [from];

			generation++;
			queue.Clear();
			stamp[from] = generation;
			queue.Enqueue(from);
			var explored = 0;
			var found = false;
			while (queue.Count > 0 && explored++ < maxExplored)
			{
				var c = queue.Dequeue();
				for (var d = 0; d < 4; d++)
				{
					var n = c + CityUtils.Neighbours4[d];
					if (!stamp.Contains(n) || stamp[n] == generation || !passable(n) || !canEnter(c, d))
						continue;

					stamp[n] = generation;
					stepDir[n] = (byte)d;
					if (n == to)
					{
						found = true;
						break;
					}

					queue.Enqueue(n);
				}

				if (found)
					break;
			}

			queue.Clear();
			if (!found)
				return null;

			var length = 1;
			for (var c = to; c != from; c -= CityUtils.Neighbours4[stepDir[c]])
				length++;

			var path = new CPos[length];
			var i = length - 1;
			for (var c = to; ; c -= CityUtils.Neighbours4[stepDir[c]])
			{
				path[i--] = c;
				if (c == from)
					break;
			}

			return path;
		}

		/// <summary>Number of road cells on the shortest drivable path, or -1.</summary>
		public int Distance(CPos from, CPos to)
		{
			var p = FindPath(from, to);
			return p == null ? -1 : p.Length - 1;
		}
	}
}
