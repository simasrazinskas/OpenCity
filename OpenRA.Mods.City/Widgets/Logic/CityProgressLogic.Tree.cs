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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityProgressLogic
	{
		[FluentReference("cost")]
		const string NodeCost = "label-progress-node-pt";

		[FluentReference("cost")]
		const string UnlockButton = "button-progress-unlock";

		[FluentReference]
		const string NodeOwned = "label-progress-node-owned";

		[FluentReference("names")]
		const string NodeRequires = "label-progress-node-requires";

		[FluentReference("names")]
		const string NodeUnlocks = "label-progress-unlock-names";

		[FluentReference]
		const string NodePoints = "label-progress-node-points";

		[FluentReference]
		const string NodeHint = "label-progress-hint";

		const int NodeStep = 34, NodeLeft = 66, TreeRowHeight = 35, TreeGap = 8;

		/// <summary>Order of the trees in the window (others follow alphabetically).</summary>
		static readonly string[] TreeOrder =
			["roads", "electricity", "water", "health", "education", "police", "garbage", "parks", "comms", "admin", "transport"];

		Color LockedTint
		{
			get
			{
				// The fill of the sunken locked card.
				Color a = CityTheme.FamilyShade(family, 2), b = CityTheme.Ramp("grey", 3);
				static int Mix(int x, int y) => (int)(x + (y - x) * 0.6f);
				return Color.FromArgb(150, Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
			}
		}

		readonly Dictionary<string, DevNode> nodes = [];
		ScrollPanelWidget treeList;
		Widget treeDetail;
		string treeSignature;
		string selectedNode;

		void InitTree(Widget page)
		{
			treeList = page.Get<ScrollPanelWidget>("TREES");
			treeDetail = page.Get("DETAIL");

			var points = page.Get<LabelWidget>("POINTS");
			points.GetText = () => ctx.Progression == null ? "" : FluentProvider.GetMessage(Points, "points", ctx.Progression.DevPoints);
			points.GetColor = () => Heading;

			var permits = page.Get<LabelWidget>("PERMITS");
			permits.GetText = CityUi.Fitted(permits, () => ctx.ProgressionUi == null ? "" : FluentProvider.GetMessage(Permits, "permits", ctx.ProgressionUi.Permits));
			permits.GetColor = () => Heading;

			var icon = treeDetail.Get<CityIconWidget>("ICON");
			icon.GetIcon = () => selectedNode != null && nodes.ContainsKey(selectedNode) ? NodeIcon(selectedNode) : null;

			var name = treeDetail.Get<LabelWidget>("NAME");
			name.GetText = CityUi.Fitted(name, () => selectedNode != null && nodes.TryGetValue(selectedNode, out var n) ? CityUi.Message(n.NameKey) : "");
			name.GetColor = () => Heading;

			var info = treeDetail.Get<LabelWidget>("INFO");
			info.GetText = DetailText;

			var unlock = treeDetail.Get<ButtonWidget>("UNLOCK");
			unlock.GetText = () =>
			{
				if (selectedNode == null || !nodes.TryGetValue(selectedNode, out var n))
					return "";

				return n.Owned ? FluentProvider.GetMessage(NodeOwned) : FluentProvider.GetMessage(UnlockButton, "cost", n.Cost);
			};
			unlock.IsVisible = () => selectedNode != null && nodes.ContainsKey(selectedNode);
			unlock.IsDisabled = () => selectedNode == null || !CanBuy(Node(selectedNode));
			unlock.OnClick = () =>
			{
				var current = Node(selectedNode);
				if (CanBuy(current))
					world.IssueOrder(UiOrders.Node(world.LocalPlayer, selectedNode));
			};
		}

		bool CanBuy(DevNode node)
		{
			return node.Id != null && !node.Owned && node.Available && world.LocalPlayer != null &&
				(ctx.Progression == null || ctx.Progression.DevPoints >= node.Cost);
		}

		DevNode Node(string id)
		{
			return id != null && nodes.TryGetValue(id, out var node) ? node : default;
		}

		string NodeIcon(string id)
		{
			var keys = unlocks.OfNode(id);
			return keys.Length > 0 ? unlocks.Icon(keys[0]) : CityProgressUnlocks.TreeIcon(Node(id).Tree);
		}

		string DetailText()
		{
			if (selectedNode == null || !nodes.TryGetValue(selectedNode, out var node))
				return FluentProvider.GetMessage(NodeHint);

			var lines = new List<string>();
			var keys = unlocks.OfNode(node.Id);
			if (keys.Length > 0)
				lines.Add(FluentProvider.GetMessage(NodeUnlocks, "names", string.Join(", ", keys.Select(unlocks.Name))));

			if (!node.Owned)
			{
				var missing = (node.Requires ?? []).Where(r => nodes.TryGetValue(r, out var req) && !req.Owned).ToArray();
				if (missing.Length > 0)
					lines.Add(FluentProvider.GetMessage(NodeRequires, "names", string.Join(", ", missing.Select(r => CityUi.Message(nodes[r].NameKey)))));
				else if (ctx.Progression != null && ctx.Progression.DevPoints < node.Cost)
					lines.Add(FluentProvider.GetMessage(NodePoints));
			}

			return string.Join("\n", lines);
		}

		string NodeTooltip(string id)
		{
			var node = Node(id);
			var text = CityUi.Message(node.NameKey);
			var keys = unlocks.OfNode(id);
			if (keys.Length > 0)
				text += "\n" + FluentProvider.GetMessage(NodeUnlocks, "names", string.Join(", ", keys.Select(unlocks.Name)));

			if (node.Requires != null && node.Requires.Length > 0 && !node.Owned)
			{
				var names = string.Join(", ", node.Requires.Select(r => nodes.TryGetValue(r, out var req) ? CityUi.Message(req.NameKey) : r));
				text += "\n" + FluentProvider.GetMessage(NodeRequires, "names", names);
			}

			if (!node.Owned && node.Available && ctx.Progression != null && ctx.Progression.DevPoints < node.Cost)
				text += "\n" + FluentProvider.GetMessage(NodePoints);

			return text;
		}

		void RefreshTree()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			nodes.Clear();
			foreach (var node in source.DevNodes)
				nodes[node.Id] = node;

			var signature = nodes.Count + ":" + treeList.Bounds.Width;
			if (signature != treeSignature)
			{
				treeSignature = signature;
				BuildTree();
			}

			// Nothing picked yet (or the picked node vanished): the first node that can be bought, else the first node.
			if (selectedNode == null || !nodes.ContainsKey(selectedNode))
				selectedNode = nodes.Values.Where(n => n.Available).OrderBy(n => n.Cost).Select(n => n.Id).FirstOrDefault() ??
					nodes.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
		}

		void BuildTree()
		{
			treeList.RemoveChildren();
			var width = treeList.Bounds.Width - treeList.ScrollbarWidth - 2;
			var blockWidth = (width - TreeGap) / 2;
			var groups = nodes.Values.GroupBy(n => n.Tree ?? "")
				.OrderBy(g => TreeIndex(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
			var rows = (groups.Count + 1) / 2;

			for (var r = 0; r < rows; r++)
			{
				var row = new ContainerWidget { Bounds = new WidgetBounds(0, 0, width, TreeRowHeight + 1) };
				treeList.AddChild(row);
				for (var column = 0; column < 2; column++)
				{
					var index = r + column * rows;
					if (index < groups.Count)
						BuildTreeBlock(row, groups[index], column * (blockWidth + TreeGap), blockWidth);
				}
			}

			treeList.Layout.AdjustChildren();
		}

		static int TreeIndex(string tree)
		{
			var i = Array.IndexOf(TreeOrder, tree);
			return i < 0 ? TreeOrder.Length : i;
		}

		void BuildTreeBlock(Widget row, IGrouping<string, DevNode> group, int x, int width)
		{
			var block = Game.LoadWidget(world, "CITY_TREE_BLOCK", row, []);
			block.Bounds.X = x;
			block.Bounds.Width = width;
			block.Get("BG").Bounds.Width = width;
			block.Get<CityIconWidget>("ICON").Icon = CityProgressUnlocks.TreeIcon(group.Key);

			var name = block.Get<LabelWidget>("NAME");
			var treeName = CityUi.Message("tree-" + group.Key, CityUi.Prettify(group.Key));
			name.GetText = CityUi.Fitted(name, () => treeName);

			var treeNodes = group.OrderBy(n => n.Tier).ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
			for (var k = 0; k < treeNodes.Count; k++)
			{
				var nodeX = NodeLeft + k * NodeStep;
				if (k > 0)
					AddConnector(block, nodeX - 6, treeNodes[k - 1].Id, treeNodes[k].Id);

				MakeNodeCard(block, treeNodes[k].Id, nodeX, 2);
			}
		}

		void AddConnector(Widget block, int x, string previous, string id)
		{
			var line = new ColorBlockWidget(Game.ModData)
			{
				Bounds = new WidgetBounds(x, 2 + 14, 6, 2),
				GetColor = () =>
				{
					if (Node(id).Owned)
						return CityTheme.Ramp("green", 4);

					return Node(previous).Owned ? CityTheme.FamilyShade(family, 3) : CityTheme.Ramp("grey", 3);
				}
			};

			block.AddChild(line);
		}

		void MakeNodeCard(Widget block, string id, int x, int y)
		{
			var card = Game.LoadWidget(world, "CITY_NODE_CARD", block, []);
			card.Bounds.X = x - 2;
			card.Bounds.Y = y - 2;

			var selected = card.Get<ColorBlockWidget>("SELECTED");
			selected.GetColor = () => CityTheme.Ramp("yellow", 5);
			selected.IsVisible = () => selectedNode == id;

			var button = card.Get<ButtonWidget>("CARD");
			var owned = button.Get("OWNED");
			var open = button.Get("OPEN");
			var locked = button.Get("LOCKED");
			owned.IsVisible = () => Node(id).Owned;
			open.IsVisible = () => { var n = Node(id); return !n.Owned && n.Available; };
			locked.IsVisible = () => { var n = Node(id); return !n.Owned && !n.Available; };

			var icon = button.Get<CityIconWidget>("ICON");
			icon.Icon = NodeIcon(id);
			var dim = button.Get<CityTintWidget>("DIM");
			dim.IsVisible = () => locked.IsVisible();
			dim.GetColor = () => LockedTint;

			var cost = button.Get<LabelWidget>("COST");
			cost.GetText = () => Node(id).Owned ? FluentProvider.GetMessage(NodeOwned) : FluentProvider.GetMessage(NodeCost, "cost", Node(id).Cost);
			cost.GetColor = () => owned.IsVisible() ? CityTheme.InkLight : locked.IsVisible() ? CityTheme.Ramp("grey", 6) : CityTheme.Ink;

			button.GetTooltipText = () => NodeTooltip(id);
			button.OnClick = () => selectedNode = id;
		}
	}
}
