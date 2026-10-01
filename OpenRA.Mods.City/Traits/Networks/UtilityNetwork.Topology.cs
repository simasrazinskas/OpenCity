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
	// Building registry, component labelling and attachment of buildings to networks.
	public sealed partial class UtilityNetwork
	{
		/// <summary>Solver view of one city building.</summary>
		sealed class UNode
		{
			public Actor Actor;
			public CityBuilding Cb;
			public UtilityProducer Prod;
			public uint Id;
			public int X0, Y0, X1, Y1;

			// Attached component index per network, -1 = not connected.
			public int Lv = -1, Hv = -1, Water = -1, Sewer = -1;

			// Per-solve values.
			public bool Operational;
			public int PowerUse, WaterUse, SewageUse;
			public int PowerPct = 100, WaterPct = 100;
			public bool Sewage = true;
			public int ReturnWater;
			public int PowerOut;
			public int PowerFlowComp = -1;
			public int WaterFlowComp = -1;
		}

		readonly List<UNode> nodes = [];
		readonly Dictionary<uint, UNode> pool = [];
		readonly Dictionary<uint, UNode> nodeByActor = [];
		readonly List<(Actor Actor, CityBuilding Cb)> scratch = [];

		readonly CellLayer<int> lvLabel;
		readonly CellLayer<int> hvLabel;
		readonly CellLayer<int> waterLabel;
		readonly CellLayer<int> sewerLabel;
		int lvComps, hvComps, waterComps, sewerComps;
		readonly List<int> lvEdge = [];
		readonly List<int> hvEdge = [];
		readonly List<int> waterEdge = [];
		readonly List<int> sewerEdge = [];

		bool topologyDirty = true;
		int lastRoadVersion = -1;
		int lastSignature;
		readonly Queue<CPos> fill = new();

		// Collects the managed city buildings in ActorID order. Returns a signature of the set.
		int RefreshBuildings()
		{
			scratch.Clear();
			foreach (var kv in world.ActorsWithTrait<CityBuilding>())
			{
				if (kv.Trait.Manager == null || !kv.Actor.IsInWorld || kv.Actor.IsDead || kv.Trait.Cells.Length == 0)
					continue;

				scratch.Add((kv.Actor, kv.Trait));
			}

			// ActorsWithTrait order is stable but not guaranteed to be ActorID order: sort only if needed.
			var sorted = true;
			for (var i = 1; i < scratch.Count; i++)
			{
				if (scratch[i - 1].Actor.ActorID > scratch[i].Actor.ActorID)
				{
					sorted = false;
					break;
				}
			}

			if (!sorted)
				scratch.Sort((a, b) => a.Actor.ActorID.CompareTo(b.Actor.ActorID));

			nodes.Clear();
			nodeByActor.Clear();
			var sig = scratch.Count;
			foreach (var (actor, cb) in scratch)
			{
				if (!pool.TryGetValue(actor.ActorID, out var n) || n.Actor != actor)
				{
					n = new UNode { Actor = actor, Cb = cb, Id = actor.ActorID, Prod = actor.TraitOrDefault<UtilityProducer>() };
					pool[actor.ActorID] = n;
					var cells = cb.Cells;
					n.X0 = n.X1 = cells[0].X;
					n.Y0 = n.Y1 = cells[0].Y;
					foreach (var c in cells)
					{
						n.X0 = Math.Min(n.X0, c.X);
						n.X1 = Math.Max(n.X1, c.X);
						n.Y0 = Math.Min(n.Y0, c.Y);
						n.Y1 = Math.Max(n.Y1, c.Y);
					}

					topologyDirty = true;
				}

				unchecked
				{
					sig = sig * 31 + (int)actor.ActorID;
				}

				nodes.Add(n);
				nodeByActor[actor.ActorID] = n;
			}

			// Forget nodes of buildings that are gone.
			if (pool.Count > nodes.Count + 64)
			{
				var stale = new List<uint>();
				foreach (var kv in pool)
					if (!nodeByActor.ContainsKey(kv.Key))
						stale.Add(kv.Key);

				foreach (var id in stale)
					pool.Remove(id);
			}

			return sig;
		}

		// Relabels every network when Roads, lines, pipes or the building set changed.
		bool EnsureTopology(int signature)
		{
			var roadVersion = Roads?.NetworkVersion ?? 0;
			if (!topologyDirty && roadVersion == lastRoadVersion && signature == lastSignature)
				return false;

			topologyDirty = false;
			lastRoadVersion = roadVersion;
			lastSignature = signature;

			lvComps = Label(lvLabel, c => Roads != null && Roads.CarriesCable(c), lvEdge);
			hvComps = Label(hvLabel, c => hv[c] != 0, hvEdge);
			waterComps = Label(waterLabel, HasWaterPipe, waterEdge);
			sewerComps = Label(sewerLabel, HasSewagePipe, sewerEdge);

			foreach (var n in nodes)
			{
				n.Lv = FindAttach(n, lvLabel, Info.AttachRange);
				n.Hv = FindAttach(n, hvLabel, 1);
				n.Water = FindAttach(n, waterLabel, Info.AttachRange);
				n.Sewer = FindAttach(n, sewerLabel, Info.AttachRange);
			}

			return true;
		}

		int Label(CellLayer<int> label, Func<CPos, bool> carrier, List<int> edgeCounts)
		{
			label.Clear(-1);
			edgeCounts.Clear();
			var count = 0;
			foreach (var start in map.AllCells)
			{
				if (!map.Contains(start) || label[start] >= 0 || !carrier(start))
					continue;

				var id = count++;
				var edges = 0;
				label[start] = id;
				fill.Enqueue(start);
				while (fill.Count > 0)
				{
					var c = fill.Dequeue();
					var onEdge = false;
					for (var i = 0; i < 4; i++)
					{
						var nb = c + CityUtils.Neighbours4[i];
						if (!map.Contains(nb))
						{
							onEdge = true;
							continue;
						}

						if (label[nb] < 0 && carrier(nb))
						{
							label[nb] = id;
							fill.Enqueue(nb);
						}
					}

					if (onEdge)
						edges++;
				}

				edgeCounts.Add(edges);
			}

			return count;
		}

		// Nearest labelled cell (Chebyshev distance to the footprint, ties row-major) within `range`; -1 if none.
		int FindAttach(UNode n, CellLayer<int> label, int range)
		{
			var best = -1;
			var bestDist = int.MaxValue;
			for (var y = n.Y0 - range; y <= n.Y1 + range; y++)
			{
				for (var x = n.X0 - range; x <= n.X1 + range; x++)
				{
					var c = new CPos(x, y);
					if (!map.Contains(c))
						continue;

					var l = label[c];
					if (l < 0)
						continue;

					var dx = Math.Max(0, Math.Max(n.X0 - x, x - n.X1));
					var dy = Math.Max(0, Math.Max(n.Y0 - y, y - n.Y1));
					var d = Math.Max(dx, dy);
					if (d < bestDist)
					{
						bestDist = d;
						best = l;
						if (d <= 1)
							return best;
					}
				}
			}

			return best;
		}
	}
}
