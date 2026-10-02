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
	/// <summary>Runtime view of a development tree node for the UI.</summary>
	public sealed class NodeData
	{
		public int Index;
		public string Id;
		public string Tree;
		public int Cost, Milestone, Tier;
		public string[] Requires;
		public string[] Unlocks;
		public int[] RequiresIndex;
	}

	public enum NodeState : byte { Locked, Available, Owned }

	// Unlock registry (milestones + bought nodes + legacy CityPlaceable.UnlockPopulation) and the development tree.
	public partial class Progression
	{
		readonly Dictionary<string, int> keyMilestone = [];
		readonly Dictionary<string, List<int>> keyNodes = [];
		readonly Dictionary<string, int> legacyUnlock = [];
		readonly List<NodeData> nodes = [];
		readonly Dictionary<string, int> nodeIndex = [];
		bool[] nodeOwned = [];

		/// <summary>Human readable problems found in the catalogue (cycles, unknown requirements, tree cost above available points).</summary>
		public readonly List<string> CatalogueWarnings = [];

		void RegisterMilestoneKey(string key, int milestone)
		{
			if (!keyMilestone.TryGetValue(key, out var existing) || milestone < existing)
				keyMilestone[key] = milestone;
		}

		void BuildUnlockRegistry()
		{
			for (var i = 1; i < milestones.Count; i++)
				foreach (var key in milestones[i].Unlocks)
					RegisterMilestoneKey(key, i);
		}

		void BuildTree()
		{
			foreach (var ni in self.Info.TraitInfos<ProgressionNodeInfo>())
			{
				var id = ni.InstanceName;
				if (string.IsNullOrEmpty(id) || nodeIndex.ContainsKey(id))
				{
					CatalogueWarnings.Add("node id missing or duplicated: " + id);
					continue;
				}

				nodeIndex[id] = nodes.Count;
				nodes.Add(new NodeData
				{
					Index = nodes.Count,
					Id = id,
					Tree = ni.Tree,
					Cost = ni.Cost,
					Milestone = ni.Milestone,
					Tier = ni.Tier,
					Requires = ni.Requires,
					Unlocks = ni.Unlocks
				});
			}

			nodeOwned = new bool[nodes.Count];
			var total = 0;
			foreach (var n in nodes)
			{
				total += n.Cost;
				n.RequiresIndex = new int[n.Requires.Length];
				for (var r = 0; r < n.Requires.Length; r++)
				{
					if (nodeIndex.TryGetValue(n.Requires[r], out var ri))
						n.RequiresIndex[r] = ri;
					else
					{
						n.RequiresIndex[r] = -1;
						CatalogueWarnings.Add($"node {n.Id} requires unknown node {n.Requires[r]}");
					}
				}

				foreach (var key in n.Unlocks)
				{
					if (!keyNodes.TryGetValue(key, out var list))
						keyNodes[key] = list = [];

					list.Add(n.Index);
				}
			}

			// Cycle check: a node can only be reached if repeatedly "owning" nodes whose requirements are owned reaches all nodes.
			var reached = new bool[nodes.Count];
			var progress = true;
			while (progress)
			{
				progress = false;
				foreach (var n in nodes)
				{
					if (reached[n.Index])
						continue;

					var ok = true;
					foreach (var ri in n.RequiresIndex)
						if (ri < 0 || !reached[ri])
							ok = false;

					if (ok)
					{
						reached[n.Index] = true;
						progress = true;
					}
				}
			}

			foreach (var n in nodes)
				if (!reached[n.Index])
					CatalogueWarnings.Add($"node {n.Id} can never be unlocked (cycle or unknown requirement)");

			var dp = 0;
			foreach (var m in milestones)
				dp += m.DevPoints;

			TotalTreeCost = total;
			TotalDevPoints = dp;
			if (total > dp)
				CatalogueWarnings.Add($"development tree costs {total} points but milestones only grant {dp}");

			foreach (var w in CatalogueWarnings)
				Log.Write("debug", "Progression catalogue: " + w);
		}

		public int NodeCount => nodes.Count;

		public int TotalTreeCost { get; private set; }

		/// <summary>Development points granted by all milestones together.</summary>
		public int TotalDevPoints { get; private set; }

		public NodeData GetNode(int index) { return nodes[index]; }

		public int FindNode(string id) { return id != null && nodeIndex.TryGetValue(id, out var i) ? i : -1; }

		public bool IsNodeOwned(int index) { return index >= 0 && index < nodeOwned.Length && nodeOwned[index]; }

		public int OwnedNodeCount
		{
			get
			{
				var n = 0;
				for (var i = 0; i < nodeOwned.Length; i++)
					if (nodeOwned[i])
						n++;

				return n;
			}
		}

		bool RequirementsMet(NodeData n)
		{
			if (MilestoneIndex < n.Milestone)
				return false;

			for (var i = 0; i < n.RequiresIndex.Length; i++)
				if (n.RequiresIndex[i] < 0 || !nodeOwned[n.RequiresIndex[i]])
					return false;

			return true;
		}

		public NodeState GetNodeState(int index)
		{
			if (nodeOwned[index])
				return NodeState.Owned;

			return RequirementsMet(nodes[index]) ? NodeState.Available : NodeState.Locked;
		}

		public bool CanBuyNode(int index)
		{
			var n = nodes[index];
			return !nodeOwned[index] && n.Cost > 0 && n.Cost <= DevPoints && RequirementsMet(n);
		}

		/// <summary>Buys a node by id (order handler). Returns false and changes nothing if it is not allowed.</summary>
		public bool BuyNode(string id)
		{
			var i = FindNode(id);
			if (i < 0 || !CanBuyNode(i))
				return false;

			DevPoints -= nodes[i].Cost;
			nodeOwned[i] = true;
			Notify("notification-prg-node", id);
			GrantAutoNodes();
			return true;
		}

		// Nodes with cost 0 open together with their milestone.
		void GrantAutoNodes()
		{
			var changed = true;
			while (changed)
			{
				changed = false;
				for (var i = 0; i < nodes.Count; i++)
				{
					if (nodeOwned[i] || nodes[i].Cost > 0 || !RequirementsMet(nodes[i]))
						continue;

					nodeOwned[i] = true;
					changed = true;
				}
			}
		}

		int NodeHash()
		{
			unchecked
			{
				var h = 0;
				for (var i = 0; i < nodeOwned.Length; i++)
					if (nodeOwned[i])
						h = h * 31 + i + 1;

				return h;
			}
		}

		// ---- IProgression: unlock registry ----

		/// <summary>
		/// Keys listed by a milestone or node need that milestone / node. Unlisted actor names fall back to the legacy
		/// CityPlaceable.UnlockPopulation (peak population); everything else is open.
		/// </summary>
		public bool IsUnlocked(string key)
		{
			if (string.IsNullOrEmpty(key))
				return true;

			if (key.StartsWith("node:", StringComparison.Ordinal))
				return IsNodeOwned(FindNode(key[5..]));

			// Signature buildings unlock by their own rules (ZON): ask ISignatureUnlocks.
			if (signatures != null && key.StartsWith("sig-", StringComparison.Ordinal))
				return signatures.IsUnlocked(key);

			if (sandbox)
				return true;

			var listed = false;
			if (keyMilestone.TryGetValue(key, out var m))
			{
				if (MilestoneIndex >= m)
					return true;

				listed = true;
			}

			if (keyNodes.TryGetValue(key, out var list))
			{
				listed = true;
				for (var i = 0; i < list.Count; i++)
					if (nodeOwned[list[i]])
						return true;
			}

			if (listed)
				return false;

			if (!legacyUnlock.TryGetValue(key, out var pop))
			{
				var ci = world.Map.Rules.Actors.TryGetValue(key, out var ai) ? ai.TraitInfoOrDefault<CityPlaceableInfo>() : null;
				pop = ci?.UnlockPopulation ?? 0;
				legacyUnlock[key] = pop;
			}

			return PeakPopulation >= pop;
		}

		/// <summary>Milestone index at which a key opens, or -1 when it is not milestone-gated (UI hint: "unlocks at ...").</summary>
		public int GetUnlockMilestone(string key)
		{
			return !sandbox && keyMilestone.TryGetValue(key, out var m) ? m : -1;
		}
	}
}
