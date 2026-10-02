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
	/// <summary>Bounded deterministic BFS over a sidewalk graph. Masks describe physical pedestrian connections, independent of car one-way rules.</summary>
	public sealed class WalkingRouter
	{
		readonly int width;
		readonly int[] visited, previous, queue;
		readonly byte[] step;
		int generation;

		public WalkingRouter(int width, int height)
		{
			this.width = width;
			visited = new int[checked(width * height)];
			previous = new int[visited.Length];
			queue = new int[visited.Length];
			step = new byte[visited.Length];
		}

		/// <summary>Headings from origin to destination. Returns null if disconnected; deferred distinguishes a bounded unfinished search.</summary>
		public byte[] FindRoute(byte[] connections, bool[] sidewalk, int from, int to, int maxNodes, out bool deferred)
		{
			deferred = false;
			if (from < 0 || to < 0 || from >= visited.Length || to >= visited.Length || !sidewalk[from] || !sidewalk[to])
				return null;

			if (from == to)
				return [];

			if (++generation == int.MaxValue)
			{
				Array.Clear(visited);
				generation = 1;
			}

			var head = 0;
			var tail = 1;
			queue[0] = from;
			visited[from] = generation;
			while (head < tail)
			{
				if (head >= Math.Max(1, maxNodes))
				{
					deferred = true;
					return null;
				}

				var cell = queue[head++];
				for (var d = 0; d < 4; d++)
				{
					if ((connections[cell] & (1 << d)) == 0)
						continue;

					if ((d == 1 && cell % width == width - 1) || (d == 3 && cell % width == 0))
						continue;

					var next = cell + (d == 0 ? -width : d == 1 ? 1 : d == 2 ? width : -1);
					if (next < 0 || next >= visited.Length || visited[next] == generation || !sidewalk[next])
						continue;

					visited[next] = generation;
					previous[next] = cell;
					step[next] = (byte)d;
					if (next == to)
					{
						var length = 0;
						for (var c = to; c != from; c = previous[c])
							length++;

						var route = new byte[length];
						for (var c = to; c != from; c = previous[c])
							route[--length] = step[c];

						return route;
					}

					queue[tail++] = next;
				}
			}

			return null;
		}
	}
}
