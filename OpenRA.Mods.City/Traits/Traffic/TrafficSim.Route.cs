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

namespace OpenRA.Mods.City.Traits
{
	// A* over link states (cell, heading) with integer costs in 1/U ticks, using the congestion snapshot.
	public sealed partial class TrafficSim
	{
		struct HeapItem
		{
			public int F, G, Link;
		}

		HeapItem[] heap = new HeapItem[1024];
		int heapCount;
		int[] gScore, parentLink, searchStamp;
		int stampCounter;
		int[] dIdx;
		int costVersion;
		byte routeKind;          // vehicle kind of the route being searched (set by callers, 0 = car)
		long expandedTotal, plannedTotal;

		void EnsureSearchArrays()
		{
			if (gScore != null)
				return;

			gScore = new int[linkCount];
			parentLink = new int[linkCount];
			searchStamp = new int[linkCount];
			dIdx = [-width, 1, width, -1];
		}

		static bool Less(in HeapItem a, in HeapItem b)
		{
			if (a.F != b.F)
				return a.F < b.F;

			if (a.G != b.G)
				return a.G < b.G;

			return a.Link < b.Link;
		}

		void HeapPush(int f, int g, int link)
		{
			if (heapCount == heap.Length)
				Array.Resize(ref heap, heap.Length * 2);

			var i = heapCount++;
			var item = new HeapItem { F = f, G = g, Link = link };
			while (i > 0)
			{
				var p = (i - 1) >> 1;
				if (!Less(item, heap[p]))
					break;

				heap[i] = heap[p];
				i = p;
			}

			heap[i] = item;
		}

		HeapItem HeapPop()
		{
			var top = heap[0];
			var last = heap[--heapCount];
			var i = 0;
			while (true)
			{
				var c = 2 * i + 1;
				if (c >= heapCount)
					break;

				if (c + 1 < heapCount && Less(heap[c + 1], heap[c]))
					c++;

				if (!Less(heap[c], last))
					break;

				heap[i] = heap[c];
				i = c;
			}

			if (heapCount > 0)
				heap[i] = last;

			return top;
		}

		// Cost of entering link (next, d) coming from heading `h` at cell c (h < 0: start, no turn).
		int EdgeCost(int c, int h, int d, int next, int speedPct, bool emergency, int seed)
		{
			var link = next * 4 + d;
			var cost = (long)costSnap[link];
			if (speedPct != 100)
				cost = cost * 100 / speedPct;

			if (flagsActive)
				cost = cost * RoadFlagCostPercent(routeKind, next) / 100;

			if (seed != 0)
				cost = cost * (1000 + (Hash(seed, next) & 63) - 32) / 1000;

			if (h >= 0)
			{
				var turn = (d - h) & 3;
				if (turn == 1)
					cost += U / 2;
				else if (turn == 3)
					cost += U * 3 / 2;
				else if (turn == 2)
					cost += emergency ? 10 * U : 30 * U;

				switch ((JunctionControl)ctl[c])
				{
					case JunctionControl.Stop: cost += emergency ? 0 : 2 * U; break;
					case JunctionControl.Signal: cost += emergency ? 0 : 4 * U; break;
					case JunctionControl.Yield: cost += emergency || turn == 1 ? 0 : U; break;
					case JunctionControl.Roundabout: cost += U; break;
				}
			}

			return (int)Math.Min(cost, 1 << 24);
		}

		bool FindRouteFor(byte kind, int from, int fromHeading, int to, int speedPct, bool emergency, int seed, out byte[] route, out int costU)
		{
			routeKind = kind;
			var ok = FindRoute(from, fromHeading, to, speedPct, emergency, seed, out route, out costU);
			routeKind = 0;
			return ok;
		}

		/// <summary>
		/// Finds the cheapest route from a cell to a destination cell. `fromHeading` is the heading the vehicle currently has
		/// (-1 = free to leave in any direction). The route is the list of headings of every move from `from` to `to`.
		/// </summary>
		bool FindRoute(int from, int fromHeading, int to, int speedPct, bool emergency, int seed, out byte[] route, out int costU)
		{
			EnsureSearchArrays();
			route = null;
			costU = 0;
			if (roadFlag[from] == 0 || roadFlag[to] == 0)
				return false;

			if (from == to)
			{
				route = [];
				return true;
			}

			// Plain car routes to popular destinations come from a cached route tree.
			if (routeKind == KindCar && !routeThrough && seed == 0 && !emergency && speedPct == 100)
			{
				var tr = TryTreeRoute(from, fromHeading, to, out route, out costU);
				if (tr == 1)
					return true;

				if (tr == 2)
					return false;
			}

			plannedTotal++;
			stampCounter++;
			heapCount = 0;
			var tx = to % width;
			var ty = to / width;
			var hScale = Math.Max(1, minFfU * (emergency ? 55 : 60) / 100 * 100 / Math.Max(10, speedPct));
			var startLink = fromHeading >= 0 ? from * 4 + fromHeading : -1;

			if (startLink >= 0)
			{
				searchStamp[startLink] = stampCounter;
				gScore[startLink] = 0;
				parentLink[startLink] = -1;
				HeapPush(0, 0, startLink);
			}
			else
			{
				ExpandFrom(from, -1, 0, -1, tx, ty, hScale, speedPct, emergency, seed);
			}

			var goal = -1;
			var expanded = 0;
			while (heapCount > 0)
			{
				var item = HeapPop();
				var l = item.Link;
				if (item.G > gScore[l])
					continue;

				var c = l >> 2;
				if (c == to)
				{
					goal = l;
					break;
				}

				expanded++;
				ExpandFrom(c, l & 3, item.G, l, tx, ty, hScale, speedPct, emergency, seed);
			}

			expandedTotal += expanded;
			if (goal < 0)
				return false;

			var length = 0;
			for (var l = goal; l >= 0 && l != startLink; l = parentLink[l])
				length++;

			route = new byte[length];
			var idx = length;
			for (var l = goal; l >= 0 && l != startLink; l = parentLink[l])
				route[--idx] = (byte)(l & 3);

			costU = gScore[goal];
			return true;
		}

		void ExpandFrom(int c, int h, int g, int parent, int tx, int ty, int hScale, int speedPct, bool emergency, int seed)
		{
			var mask = exitMask[c];
			var deadEnd = (mask & (mask - 1)) == 0;
			for (var d = 0; d < 4; d++)
			{
				if ((mask & (1 << d)) == 0)
					continue;

				if (h >= 0 && d == ((h + 2) & 3) && !deadEnd && !emergency)
					continue;

				var n = c + dIdx[d];
				var nl = n * 4 + d;
				var ng = g + EdgeCost(c, h, d, n, speedPct, emergency, seed);
				if (searchStamp[nl] == stampCounter && gScore[nl] <= ng)
					continue;

				searchStamp[nl] = stampCounter;
				gScore[nl] = ng;
				parentLink[nl] = parent;
				var hx = Math.Abs(n % width - tx) + Math.Abs(n / width - ty);
				HeapPush(ng + hx * hScale, ng, nl);
			}
		}
	}
}
