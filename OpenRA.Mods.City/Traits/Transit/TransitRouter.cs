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
	/// Deterministic shortest-path search over IRoadNetwork (N, E, S, W order, respects CanEnter). Scratch state only,
	/// so a separate instance can be used from UI code (route previews) without influencing the simulation.
	/// </summary>
	public sealed class TransitRouter
	{
		readonly Func<CPos, bool> passable;
		readonly Func<CPos, int, bool> canEnter;
		readonly CellLayer<int> stamp;
		readonly CellLayer<byte> stepDir;
		readonly PriorityQueue<CPos, (int Cost, int Order)> queue = new();
		readonly CellLayer<int> bestCost, steps;
		readonly Func<CPos, int> traversalCost;
		int generation;

		/// <summary>Search over the road network.</summary>
		public TransitRouter(Map map, IRoadNetwork roads, Func<CPos, int> traversalCost = null)
			: this(map, roads.IsRoad, roads.CanEnter, traversalCost)
		{
		}

		/// <summary>Search over any cell network: `passable` says whether a cell is part of it, `canEnter` whether a step in direction d (index into Neighbours4) is allowed.</summary>
		public TransitRouter(Map map, Func<CPos, bool> passable, Func<CPos, int, bool> canEnter, Func<CPos, int> traversalCost = null)
		{
			this.passable = passable;
			this.canEnter = canEnter;
			this.traversalCost = traversalCost;
			bestCost = new CellLayer<int>(map);
			steps = new CellLayer<int>(map);
			stamp = new CellLayer<int>(map);
			stepDir = new CellLayer<byte>(map);
		}

		/// <summary>Path from `from` to `to` (inclusive), or null when there is none. A path from a cell to itself is that single cell.</summary>
		public CPos[] FindPath(CPos from, CPos to, int maxExplored = 60000, int maxSteps = int.MaxValue)
		{
			if (!stamp.Contains(from) || !stamp.Contains(to) || !passable(from) || !passable(to))
				return null;

			if (from == to)
				return [from];

			generation++;
			queue.Clear();
			stamp[from] = generation;
			bestCost[from] = 0;
			steps[from] = 0;
			var order = 0;
			queue.Enqueue(from, (0, order++));
			var explored = 0;
			var found = false;
			while (queue.Count > 0 && explored++ < maxExplored)
			{
				queue.TryDequeue(out var c, out var priority);
				if (priority.Cost != bestCost[c])
					continue;

				if (c == to)
				{
					found = true;
					break;
				}

				if (steps[c] >= maxSteps)
					continue;

				for (var d = 0; d < 4; d++)
				{
					var n = c + CityUtils.Neighbours4[d];
					if (!stamp.Contains(n) || !passable(n) || !canEnter(c, d))
						continue;

					var nextCost = bestCost[c] + Math.Max(1, traversalCost?.Invoke(n) ?? 1);
					if (stamp[n] == generation && bestCost[n] <= nextCost)
						continue;

					stamp[n] = generation;
					bestCost[n] = nextCost;
					steps[n] = steps[c] + 1;
					stepDir[n] = (byte)d;
					queue.Enqueue(n, (nextCost, order++));
				}
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
		public int Distance(CPos from, CPos to, int maxSteps = int.MaxValue)
		{
			var p = FindPath(from, to, 60000, maxSteps);
			return p == null ? -1 : p.Length - 1;
		}
	}
}
