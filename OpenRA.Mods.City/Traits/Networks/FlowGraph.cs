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
	/// Small integer max-flow graph (Dinic) for the utility solvers. Nodes are network components, edges are producers,
	/// consumers, transformers and trade connections. Edge and node order is insertion order, so results are deterministic.
	/// </summary>
	public sealed class FlowGraph
	{
		readonly List<int> head = [];
		readonly List<int> to = [];
		readonly List<int> cap = [];
		readonly List<int> flow = [];
		readonly List<int> next = [];
		int[] level = [];
		int[] iter = [];
		readonly Queue<int> queue = new();

		public int NodeCount => head.Count;
		public int EdgeCount => to.Count;

		public void Clear()
		{
			head.Clear();
			to.Clear();
			cap.Clear();
			flow.Clear();
			next.Clear();
		}

		public int AddNode()
		{
			head.Add(-1);
			return head.Count - 1;
		}

		public void AddNodes(int count)
		{
			for (var i = 0; i < count; i++)
				head.Add(-1);
		}

		/// <summary>Adds an edge and its residual twin. Returns the index of the forward edge (flow readable via <see cref="Flow"/>).</summary>
		public int AddEdge(int a, int b, int capacity, int reverseCapacity = 0)
		{
			var id = to.Count;
			to.Add(b);
			cap.Add(capacity);
			flow.Add(0);
			next.Add(head[a]);
			head[a] = id;

			to.Add(a);
			cap.Add(reverseCapacity);
			flow.Add(0);
			next.Add(head[b]);
			head[b] = id + 1;
			return id;
		}

		public int Flow(int edge) { return flow[edge]; }

		public int Capacity(int edge) { return cap[edge]; }

		/// <summary>Raises (or sets) the capacity of an existing forward edge, e.g. to enable import arcs for a second pass.</summary>
		public void SetCapacity(int edge, int capacity) { cap[edge] = capacity; }

		bool Bfs(int s, int t)
		{
			if (level.Length < head.Count)
			{
				level = new int[head.Count];
				iter = new int[head.Count];
			}

			Array.Fill(level, -1, 0, head.Count);
			level[s] = 0;
			queue.Clear();
			queue.Enqueue(s);
			while (queue.Count > 0)
			{
				var v = queue.Dequeue();
				for (var e = head[v]; e >= 0; e = next[e])
				{
					if (cap[e] - flow[e] > 0 && level[to[e]] < 0)
					{
						level[to[e]] = level[v] + 1;
						queue.Enqueue(to[e]);
					}
				}
			}

			return level[t] >= 0;
		}

		int Dfs(int v, int t, int pushed)
		{
			if (v == t)
				return pushed;

			for (; iter[v] >= 0; iter[v] = next[iter[v]])
			{
				var e = iter[v];
				var u = to[e];
				if (cap[e] - flow[e] > 0 && level[u] == level[v] + 1)
				{
					var d = Dfs(u, t, Math.Min(pushed, cap[e] - flow[e]));
					if (d > 0)
					{
						flow[e] += d;
						flow[e ^ 1] -= d;
						return d;
					}
				}
			}

			return 0;
		}

		/// <summary>Augments the current flow as far as possible. Returns the additional flow pushed.</summary>
		public int MaxFlow(int s, int t)
		{
			var total = 0;
			while (Bfs(s, t))
			{
				for (var i = 0; i < head.Count; i++)
					iter[i] = head[i];

				int f;
				while ((f = Dfs(s, t, int.MaxValue)) > 0)
					total += f;
			}

			return total;
		}
	}
}
