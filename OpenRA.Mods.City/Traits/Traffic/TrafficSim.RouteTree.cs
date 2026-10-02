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
	// Route-tree cache: popular destinations (workplaces, gates, shops) get a reverse Dijkstra tree over the link states
	// (cost-to-go and best next heading for every (cell, heading)). Routes of ordinary cars to such a destination are then read
	// off the tree instead of running A*. Trees are only used for plain car routes (no jitter, no policy bans) and expire after
	// RouteTreeTtl ticks or a road change.
	public sealed partial class TrafficSim
	{
		sealed class RouteTree
		{
			public int Destination;
			public int Version;
			public int CostVersion;
			public int BuiltTick;
			public int LastUse;
			public int[] Cost;
			public byte[] Next;
		}

		readonly List<RouteTree> trees = [];
		int[] destHits;
		int hitWindowStart;
		int treeBuiltTick = -1;
		long treeRoutes, treeBuilds, treeLookups, treeUnreach, treeFull;

		// 0 = no tree for this destination (yet), 1 = route found, 2 = the destination is unreachable from here.
		int TryTreeRoute(int from, int fromHeading, int to, out byte[] route, out int costU)
		{
			route = null;
			costU = 0;
			if (Info.RouteTreeMinHits <= 0 || Info.RouteTreeCapacity <= 0)
				return 0;

			destHits ??= new int[cellCount];
			if (tick - hitWindowStart >= Math.Max(1, Info.RouteTreeTtl))
			{
				hitWindowStart = tick;
				Array.Clear(destHits);
			}

			treeLookups++;
			var tree = FindTree(to);
			if (tree == null)
			{
				if (++destHits[to] < Info.RouteTreeMinHits || treeBuiltTick == tick)
					return 0;

				treeBuiltTick = tick;
				tree = BuildTree(to);
				if (tree == null)
				{
					treeFull++;
					return 0;
				}

				treeBuiltTick = tick;
			}

			tree.LastUse = tick;
			if (!ExtractRoute(tree, from, fromHeading, out route, out costU))
			{
				treeUnreach++;
				return 2;
			}

			treeRoutes++;
			return 1;
		}

		RouteTree FindTree(int to)
		{
			for (var i = trees.Count - 1; i >= 0; i--)
			{
				var t = trees[i];
				if (t.Version != graphVersion || t.CostVersion != costVersion || tick - t.BuiltTick > Info.RouteTreeTtl)
				{
					trees.RemoveAt(i);
					continue;
				}

				if (t.Destination == to)
					return t;
			}

			return null;
		}

		RouteTree BuildTree(int to)
		{
			RouteTree tree;
			if (trees.Count >= Info.RouteTreeCapacity)
			{
				// Recycle the least recently used tree.
				var oldest = 0;
				for (var i = 1; i < trees.Count; i++)
					if (trees[i].LastUse < trees[oldest].LastUse)
						oldest = i;

				// Never evict a tree that is still in use: more popular destinations than trees must not thrash.
				if (tick - trees[oldest].LastUse < 100)
					return null;

				tree = trees[oldest];
				trees.RemoveAt(oldest);
			}
			else
				tree = new RouteTree { Cost = new int[linkCount], Next = new byte[linkCount] };

			tree.Destination = to;
			tree.Version = graphVersion;
			tree.CostVersion = costVersion;
			tree.BuiltTick = tick;
			tree.LastUse = tick;
			treeBuilds++;
			EnsureSearchArrays();

			var cost = tree.Cost;
			Array.Fill(cost, int.MaxValue);
			heapCount = 0;
			for (var h = 0; h < 4; h++)
			{
				cost[to * 4 + h] = 0;
				HeapPush(0, 0, to * 4 + h);
			}

			var expanded = 0;
			while (heapCount > 0)
			{
				if (++expanded > Math.Max(1, Info.MaxRouteNodes))
					return null;
				var item = HeapPop();
				var s = item.Link;
				if (item.G > cost[s])
					continue;

				// Predecessors of state s = (n, d): a vehicle in (c, h) with c = n - dir(d) that leaves c towards d.
				var n = s >> 2;
				var d = s & 3;
				var c = n - dIdx[d];
				if (c < 0 || c >= cellCount || (exitMask[c] & (1 << d)) == 0)
					continue;

				var mask = exitMask[c];
				var deadEnd = (mask & (mask - 1)) == 0;
				for (var h = 0; h < 4; h++)
				{
					if (d == ((h + 2) & 3) && !deadEnd)
						continue;

					var p = c * 4 + h;
					var cand = item.G + EdgeCost(c, h, d, n, 100, false, 0);
					if (cand < cost[p])
					{
						cost[p] = cand;
						tree.Next[p] = (byte)d;
						HeapPush(cand, cand, p);
					}
				}
			}

			trees.Add(tree);
			return tree;
		}

		bool ExtractRoute(RouteTree tree, int from, int fromHeading, out byte[] route, out int costU)
		{
			route = null;
			costU = 0;
			var cost = tree.Cost;
			int state;
			int total;
			byte first;
			if (fromHeading >= 0)
			{
				state = from * 4 + fromHeading;
				if (cost[state] == int.MaxValue)
					return false;

				total = cost[state];
				first = tree.Next[state];
			}
			else
			{
				var best = int.MaxValue;
				first = 0;
				for (var d = 0; d < 4; d++)
				{
					if ((exitMask[from] & (1 << d)) == 0)
						continue;

					var n = from + dIdx[d];
					var c = cost[n * 4 + d];
					if (c == int.MaxValue)
						continue;

					c += EdgeCost(from, -1, d, n, 100, false, 0);
					if (c < best)
					{
						best = c;
						first = (byte)d;
					}
				}

				if (best == int.MaxValue)
					return false;

				total = best;
			}

			var path = new List<byte>(32) { first };
			var cell = from + dIdx[first];
			var heading = first;
			for (var guard = 0; guard < linkCount; guard++)
			{
				if (cell == tree.Destination)
					break;

				var d = tree.Next[cell * 4 + heading];
				path.Add(d);
				cell += dIdx[d];
				heading = d;
			}

			route = path.ToArray();
			costU = total;
			return cell == tree.Destination;
		}
	}
}
