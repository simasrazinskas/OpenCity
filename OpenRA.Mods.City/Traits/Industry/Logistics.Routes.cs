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
	// Route cache (design/06 3.6): one BFS distance field per source road cell, invalidated by IRoadNetwork.NetworkVersion,
	// bounded LRU, at most MaxBfsPerTick searches per tick. Neighbours expand in CityUtils.Neighbours4 order, so results are deterministic.
	public partial class Logistics
	{
		/// <summary>RouteLength result: no path between the cells.</summary>
		public const int Unreachable = -1;

		/// <summary>RouteLength result: the per-tick search budget is used up, try again later.</summary>
		public const int RouteBusy = -2;

		sealed class RouteField
		{
			public CPos Source;
			public int Version;
			public int LastUse;
			public ushort[] Dist;
		}

		readonly List<RouteField> fields = [];
		IRoadNetwork roads;
		int[] queue;
		int mapWidth;
		int mapHeight;
		int bfsTick = -1;
		int bfsThisTick;
		public int BfsRuns { get; private set; }

		void SetupRoutes()
		{
			mapWidth = world.Map.MapSize.Width;
			mapHeight = world.Map.MapSize.Height;
			queue = new int[mapWidth * mapHeight];
		}

		bool InMap(CPos c) { return c.X >= 0 && c.Y >= 0 && c.X < mapWidth && c.Y < mapHeight; }

		int IndexOf(CPos c) { return c.Y * mapWidth + c.X; }

		RouteField GetField(CPos source)
		{
			var version = roads?.NetworkVersion ?? 0;
			RouteField stale = null;
			RouteField oldest = null;
			for (var i = 0; i < fields.Count; i++)
			{
				var f = fields[i];
				if (f.Source == source)
				{
					if (f.Version == version)
					{
						f.LastUse = world.WorldTick;
						return f;
					}

					stale = f;
					break;
				}

				if (oldest == null || f.LastUse < oldest.LastUse)
					oldest = f;
			}

			if (bfsTick != world.WorldTick)
			{
				bfsTick = world.WorldTick;
				bfsThisTick = 0;
			}

			if (bfsThisTick >= Info.MaxBfsPerTick)
				return null;

			bfsThisTick++;
			BfsRuns++;
			var field = stale;
			if (field == null)
			{
				if (fields.Count >= Math.Max(1, Info.RouteCacheSize))
				{
					field = oldest;
				}
				else
				{
					field = new RouteField { Dist = new ushort[mapWidth * mapHeight] };
					fields.Add(field);
				}
			}

			field.Source = source;
			field.Version = version;
			field.LastUse = world.WorldTick;
			Search(field);
			return field;
		}

		void Search(RouteField field)
		{
			Array.Fill(field.Dist, ushort.MaxValue);
			var source = field.Source;
			if (roads == null || !InMap(source) || !roads.IsRoad(source))
				return;

			var head = 0;
			var tail = 0;
			field.Dist[IndexOf(source)] = 0;
			queue[tail++] = IndexOf(source);
			while (head < tail)
			{
				var idx = queue[head++];
				var c = new CPos(idx % mapWidth, idx / mapWidth);
				var d = field.Dist[idx];
				if (d >= ushort.MaxValue - 1)
					continue;

				for (var dir = 0; dir < 4; dir++)
				{
					var n = c + CityUtils.Neighbours4[dir];
					if (!InMap(n))
						continue;

					var ni = IndexOf(n);
					if (field.Dist[ni] != ushort.MaxValue || !roads.CanEnter(c, dir))
						continue;

					field.Dist[ni] = (ushort)(d + 1);
					queue[tail++] = ni;
				}
			}
		}

		/// <summary>Road cells between two road cells (at least 1), Unreachable (-1) or RouteBusy (-2). Without a road network: Manhattan distance.</summary>
		public int RouteLength(CPos from, CPos to)
		{
			if (from == CPos.Zero || to == CPos.Zero)
				return Unreachable;

			if (roads == null)
				return Math.Max(1, Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y));

			if (!InMap(from) || !InMap(to))
				return Unreachable;

			var field = GetField(from);
			if (field == null)
				return RouteBusy;

			var d = field.Dist[IndexOf(to)];
			return d == ushort.MaxValue ? Unreachable : Math.Max(1, (int)d);
		}

		/// <summary>Walks the shortest path from `to` back to the source of its field and adds every cell to `path` (destination first).</summary>
		void CollectPath(CPos source, CPos to, List<CPos> path)
		{
			path.Clear();
			if (roads == null || !InMap(to))
				return;

			RouteField field = null;
			foreach (var f in fields)
				if (f.Source == source && f.Version == roads.NetworkVersion)
					field = f;

			if (field == null)
				return;

			var cur = to;
			var d = field.Dist[IndexOf(cur)];
			while (d != ushort.MaxValue && path.Count < 4096)
			{
				path.Add(cur);
				if (d == 0)
					break;

				var found = false;
				for (var dir = 0; dir < 4 && !found; dir++)
				{
					var n = cur + CityUtils.Neighbours4[dir];
					if (!InMap(n) || field.Dist[IndexOf(n)] != d - 1)
						continue;

					// n -> cur must be a legal step: cur lies in direction (dir + 2) % 4 from... i.e. n + Neighbours4[(dir + 2) % 4] == cur.
					if (roads.CanEnter(n, (dir + 2) % 4))
					{
						cur = n;
						d--;
						found = true;
					}
				}

				if (!found)
					break;
			}
		}
	}
}
