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

using System.Collections.Generic;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Presents the progression WP's policies, development tree, milestones, map tiles and districts to the UI panels.</summary>
	public sealed class ProgressionUiAdapter : IProgressionUiSource
	{
		readonly Progression progression;
		readonly List<PolicyEntry> policies = [];
		readonly List<DevNode> nodes = [];
		readonly List<MilestoneEntry> milestones = [];
		readonly List<DistrictEntry> districts = [];

		// The lists are rebuilt at most once per tick; panels read them several times per frame.
		int policiesTick = -1;
		int policiesDistrict = -1;
		int nodesTick = -1;
		int milestonesTick = -1;
		int districtsTick = -1;
		readonly World world;

		public ProgressionUiAdapter(World world, Progression progression)
		{
			this.world = world;
			this.progression = progression;
		}

		public IReadOnlyList<PolicyEntry> Policies(int districtId)
		{
			if (policiesTick == world.WorldTick && policiesDistrict == districtId)
				return policies;

			policiesTick = world.WorldTick;
			policiesDistrict = districtId;
			policies.Clear();
			for (var i = 0; i < progression.PolicyCount; i++)
			{
				var data = progression.GetPolicyData(i);
				var cityScope = data.Scope == PolicyScope.City;
				if (cityScope != (districtId == 0))
					continue;

				var value = progression.GetPolicyValue(i, districtId);
				policies.Add(new PolicyEntry
				{
					Id = data.Id,
					NameKey = "policy-" + data.Id,
					DescKey = "policy-" + data.Id + "-desc",
					CityScope = cityScope,
					Unlocked = progression.IsPolicyAvailable(i),
					Active = value > 0,
					SliderMin = data.SliderMin,
					SliderMax = data.SliderMax,
					SliderValue = data.HasSlider ? (value > 0 ? value : data.SliderDefault) : 0,
					UpkeepPerMonth = data.Upkeep
				});
			}

			return policies;
		}

		public IReadOnlyList<DevNode> DevNodes
		{
			get
			{
				if (nodesTick == world.WorldTick)
					return nodes;

				nodesTick = world.WorldTick;
				nodes.Clear();
				for (var i = 0; i < progression.NodeCount; i++)
				{
					var data = progression.GetNode(i);
					var state = progression.GetNodeState(i);
					nodes.Add(new DevNode
					{
						Id = data.Id,
						NameKey = "node-" + data.Id,
						DescKey = "node-" + data.Id + "-desc",
						Tree = data.Tree,
						Tier = data.Tier,
						Cost = data.Cost,
						Owned = state == NodeState.Owned,
						Available = state == NodeState.Available,
						Requires = data.Requires ?? []
					});
				}

				return nodes;
			}
		}

		public IReadOnlyList<MilestoneEntry> Milestones
		{
			get
			{
				if (milestonesTick == world.WorldTick)
					return milestones;

				milestonesTick = world.WorldTick;
				milestones.Clear();
				for (var i = 0; i <= progression.MilestoneCount; i++)
				{
					var data = progression.GetMilestone(i);
					milestones.Add(new MilestoneEntry
					{
						Name = data.Name,
						Xp = data.Xp,
						Reached = i <= progression.MilestoneIndex,
						Reward = CityUtils.FormatMoney(data.Money) + ", " + data.DevPoints + " DP, " + data.Tiles + " tiles"
					});
				}

				return milestones;
			}
		}

		public int Permits => progression.Permits;

		public int TileGridSize => progression.TileGrid;

		public int TilePrice(int tileX, int tileY)
		{
			switch (progression.CheckBuyTile(tileX, tileY))
			{
				case TileBuyResult.Ok:
				case TileBuyResult.NoPermit:
				case TileBuyResult.NoFunds:
					return progression.GetTilePrice(tileX, tileY);
				default:
					return -1;
			}
		}

		public bool IsTileOwned(int tileX, int tileY) { return progression.IsTileOwned(tileX, tileY); }

		public bool TileAt(CPos cell, out int tileX, out int tileY)
		{
			var index = progression.TileIndexAt(cell);
			tileX = index < 0 ? 0 : index % progression.TileGrid;
			tileY = index < 0 ? 0 : index / progression.TileGrid;
			return index >= 0;
		}

		public void TileBounds(int tileX, int tileY, out CPos topLeft, out CPos bottomRight)
		{
			var r = progression.GetTileRect(tileX, tileY);
			topLeft = new CPos(r.Left, r.Top);
			bottomRight = new CPos(r.Right - 1, r.Bottom - 1);
		}

		public IReadOnlyList<DistrictEntry> Districts
		{
			get
			{
				if (districtsTick == world.WorldTick)
					return districts;

				districtsTick = world.WorldTick;
				districts.Clear();
				for (var id = 1; id <= progression.MaxDistricts; id++)
				{
					if (!progression.DistrictExists(id))
						continue;

					var stats = progression.GetDistrictStats(id);
					var color = InfoViews.CategoryColor(id - 1);
					districts.Add(new DistrictEntry
					{
						Id = id,
						Name = progression.GetDistrictName(id) ?? FluentProvider.GetMessage("district-default-name", "id", id),
						Population = stats.Population,
						Households = stats.Households,
						Jobs = stats.Jobs,
						Happiness = stats.AverageHappiness < 0 ? 0 : stats.AverageHappiness,
						LandValue = stats.AverageLandValue < 0 ? 0 : stats.AverageLandValue,
						ArgbColor = (color.R << 16) | (color.G << 8) | color.B
					});
				}

				return districts;
			}
		}
	}
}
