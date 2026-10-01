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
using System.Globalization;
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Progression panel: XP and milestone bar, development points and map tile permits, the milestone list with rewards and
	/// the development tree (one block per tree, nodes show cost and state; click to buy with CityUnlockNode).
	/// </summary>
	public class CityProgressLogic : ChromeLogic
	{
		[FluentReference("name", "xp", "next")]
		const string MilestoneProgress = "label-progress-milestone";

		[FluentReference("name", "xp")]
		const string MilestoneMax = "label-progress-milestone-max";

		[FluentReference("points")]
		const string Points = "label-progress-points";

		[FluentReference("permits")]
		const string Permits = "label-progress-permits";

		[FluentReference("cost")]
		const string NodeCost = "label-progress-node-cost";

		[FluentReference]
		const string NodeOwned = "label-progress-node-owned";

		[FluentReference("names")]
		const string NodeRequires = "label-progress-node-requires";

		[FluentReference]
		const string NodePoints = "label-progress-node-points";

		const int NodesPerRow = 3;

		readonly World world;
		readonly CityUiContext ctx;
		readonly ScrollPanelWidget tree;
		readonly ScrollPanelWidget milestones;
		readonly Dictionary<string, DevNode> nodes = [];

		string builtSignature;

		[ObjectCreator.UseCtor]
		public CityProgressLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);

			widget.Get<ButtonWidget>("CLOSE").OnClick = () => widget.Visible = false;
			tree = widget.Get<ScrollPanelWidget>("TREE");
			milestones = widget.Get<ScrollPanelWidget>("MILESTONES");

			widget.Get<LabelWidget>("MILESTONE").GetText = MilestoneText;

			var bar = widget.Get<CityBarWidget>("XP_BAR");
			bar.GetPercentage = () =>
			{
				var progression = ctx.Progression;
				if (progression == null || progression.NextMilestoneXp <= 0)
					return 100;

				return (int)(progression.Xp * 100L / progression.NextMilestoneXp);
			};

			var points = widget.Get<LabelWidget>("POINTS");
			points.GetText = () => ctx.Progression == null ? "" : FluentProvider.GetMessage(Points, "points", ctx.Progression.DevPoints);

			var permits = widget.Get<LabelWidget>("PERMITS");
			permits.GetText = () => ctx.ProgressionUi == null ? "" : FluentProvider.GetMessage(Permits, "permits", ctx.ProgressionUi.Permits);

			var panelVisible = widget.IsVisible;
			widget.IsVisible = () =>
			{
				var visible = panelVisible();
				if (visible)
					Refresh();

				return visible;
			};
		}

		string MilestoneText()
		{
			var progression = ctx.Progression;
			if (progression == null)
				return "";

			var xp = progression.Xp.ToString("N0", CultureInfo.CurrentCulture);
			if (progression.NextMilestoneXp <= progression.Xp)
				return FluentProvider.GetMessage(MilestoneMax, "name", progression.MilestoneName, "xp", xp);

			return FluentProvider.GetMessage(MilestoneProgress, "name", progression.MilestoneName, "xp", xp,
				"next", progression.NextMilestoneXp.ToString("N0", CultureInfo.CurrentCulture));
		}

		void Refresh()
		{
			var source = ctx.ProgressionUi;
			if (source == null)
				return;

			nodes.Clear();
			foreach (var node in source.DevNodes)
				nodes[node.Id] = node;

			var signature = source.DevNodes.Count + ":" + source.Milestones.Count + ":" + source.Milestones.Count(m => m.Reached);
			if (signature == builtSignature)
				return;

			builtSignature = signature;
			BuildMilestones(source);
			BuildTree(source);
		}

		void BuildMilestones(IProgressionUiSource source)
		{
			milestones.RemoveChildren();
			foreach (var milestone in source.Milestones)
			{
				var m = milestone;
				var row = Game.LoadWidget(world, "CITY_BUDGET_ROW", milestones, []);
				row.Bounds.Width = milestones.Bounds.Width - milestones.ScrollbarWidth - 6;
				var name = CityUi.Message("label-milestone-" + m.Name.ToLowerInvariant().Replace(' ', '-'), m.Name);
				var label = row.Get<LabelWidget>("NAME");
				label.GetText = () => name;
				label.Bounds.Width = row.Bounds.Width - 70;
				label.GetColor = () => m.Reached ? CityUi.Good : CityUi.Muted;

				var xp = row.Get<LabelWidget>("VALUE");
				xp.Bounds.X = row.Bounds.Width - 70;
				xp.Bounds.Width = 70;
				var xpText = m.Xp.ToString("N0", CultureInfo.CurrentCulture);
				xp.GetText = () => xpText;
				xp.GetColor = () => m.Reached ? CityUi.Good : CityUi.Muted;
			}

			milestones.Layout.AdjustChildren();
		}

		void BuildTree(IProgressionUiSource source)
		{
			tree.RemoveChildren();
			var width = tree.Bounds.Width - tree.ScrollbarWidth - 8;
			foreach (var group in source.DevNodes.GroupBy(n => n.Tree ?? "").OrderBy(g => g.Key, StringComparer.Ordinal))
			{
				var treeNodes = group.OrderBy(n => n.Tier).ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
				var rows = (treeNodes.Count + NodesPerRow - 1) / NodesPerRow;
				var block = new ContainerWidget { Bounds = new WidgetBounds(0, 0, width, 22 + rows * 48 + 6) };

				var header = new LabelWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(2, 0, width, 20),
					Font = "Bold",
					Shadow = true,
					GetText = () => CityUi.Message("label-tree-" + group.Key.ToLowerInvariant(), CityUi.Prettify(group.Key)),
					GetColor = () => CityUi.Accent
				};

				block.AddChild(header);
				for (var i = 0; i < treeNodes.Count; i++)
					MakeNodeButton(block, treeNodes[i].Id, i % NodesPerRow * 128, 22 + i / NodesPerRow * 48);

				tree.AddChild(block);
			}
		}

		void MakeNodeButton(Widget block, string id, int x, int y)
		{
			var button = Game.LoadWidget(world, "CITY_NODE_BUTTON", block, []) as ButtonWidget;
			button.Bounds.X = x;
			button.Bounds.Y = y;

			var node = nodes[id];
			var name = CityUi.Message(node.NameKey);
			button.GetText = () => name + "\n" + (Node(id).Owned ? FluentProvider.GetMessage(NodeOwned) : FluentProvider.GetMessage(NodeCost, "cost", Node(id).Cost));
			button.IsHighlighted = () => Node(id).Owned;
			button.IsDisabled = () => !Node(id).Owned && !Node(id).Available;
			button.GetTooltipText = () => Tooltip(id);
			button.OnClick = () =>
			{
				var current = Node(id);
				if (current.Owned || !current.Available || world.LocalPlayer == null)
					return;

				if (ctx.Progression != null && ctx.Progression.DevPoints < current.Cost)
					return;

				world.IssueOrder(UiOrders.Node(world.LocalPlayer, id));
			};
		}

		DevNode Node(string id)
		{
			return nodes.TryGetValue(id, out var node) ? node : default;
		}

		string Tooltip(string id)
		{
			var node = Node(id);
			var text = CityUi.Message(node.NameKey) + "\n" + CityUi.Message(node.DescKey, "");
			if (node.Requires != null && node.Requires.Length > 0 && !node.Owned)
			{
				var names = string.Join(", ", node.Requires.Select(r => nodes.TryGetValue(r, out var req) ? CityUi.Message(req.NameKey) : r));
				text += "\n" + FluentProvider.GetMessage(NodeRequires, "names", names);
			}

			if (!node.Owned && node.Available && ctx.Progression != null && ctx.Progression.DevPoints < node.Cost)
				text += "\n" + FluentProvider.GetMessage(NodePoints);

			return text;
		}
	}
}
