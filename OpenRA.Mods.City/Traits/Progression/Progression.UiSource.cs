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

namespace OpenRA.Mods.City.Traits
{
	// Read side for the UI panels (policies, tree, milestones, tiles, districts). Builds fresh lists: UI code only.
	public partial class Progression
	{
		static readonly int[] DistrictColors =
		[
			0x4C9AE6, 0xE6794C, 0x5CC96A, 0xD65CC9, 0xE6D24C, 0x4CD6D0, 0xA06CE0, 0xE04C6C,
			0x8CB84C, 0xE69A4C, 0x4C6CE0, 0x4CE0A0, 0xC9A04C, 0x6CA0E0, 0xE06CA0, 0x9CC9E0
		];

		public IReadOnlyList<PolicyEntry> Policies(int districtId)
		{
			var list = new List<PolicyEntry>();
			var city = districtId == 0;
			foreach (var p in policies)
			{
				if (p.Scope == PolicyScope.City != city)
					continue;

				var value = GetPolicyValue(p.Index, districtId);
				list.Add(new PolicyEntry
				{
					Id = p.Id,
					NameKey = "policy-" + p.Id,
					DescKey = "policy-" + p.Id + "-desc",
					CityScope = p.Scope == PolicyScope.City,
					Unlocked = IsPolicyAvailable(p.Index),
					Active = value > 0,
					SliderMin = p.SliderMin,
					SliderMax = p.SliderMax,
					SliderValue = value > 0 ? value : p.SliderDefault,
					UpkeepPerMonth = p.Upkeep
				});
			}

			return list;
		}

		public IReadOnlyList<DevNode> DevNodes
		{
			get
			{
				var list = new List<DevNode>(nodes.Count);
				foreach (var n in nodes)
					list.Add(new DevNode
					{
						Id = n.Id,
						NameKey = "node-" + n.Id,
						DescKey = "node-" + n.Id,
						Tree = n.Tree,
						Tier = n.Tier,
						Cost = n.Cost,
						Owned = nodeOwned[n.Index],
						Available = !nodeOwned[n.Index] && RequirementsMet(n),
						Requires = n.Requires
					});

				return list;
			}
		}

		public IReadOnlyList<MilestoneEntry> Milestones
		{
			get
			{
				var list = new List<MilestoneEntry>();
				for (var i = 1; i < milestones.Count; i++)
				{
					var m = milestones[i];
					list.Add(new MilestoneEntry
					{
						Name = m.Name,
						Xp = m.Xp,
						Reached = MilestoneIndex >= i,
						Reward = $"{CityUtils.FormatMoney(m.Money)}, {m.DevPoints} pts, {m.Tiles} tiles"
					});
				}

				return list;
			}
		}

		public int TileGridSize => Info.TileGrid;

		public int TilePrice(int tileX, int tileY)
		{
			if (!tilesInit || tileX < 0 || tileY < 0 || tileX >= Info.TileGrid || tileY >= Info.TileGrid)
				return -1;

			if (IsTileOwned(tileX, tileY))
				return -1;

			if (!TileAdjacentToOwned(tileX, tileY) && !tileHasConnection[tileY * Info.TileGrid + tileX])
				return -1;

			return GetTilePrice(tileX, tileY);
		}

		public bool TileAt(CPos cell, out int tileX, out int tileY)
		{
			tileX = tileY = 0;
			var ti = tilesInit ? TileIndexAt(cell) : -1;
			if (ti < 0)
				return false;

			tileX = ti % Info.TileGrid;
			tileY = ti / Info.TileGrid;
			return true;
		}

		public void TileBounds(int tileX, int tileY, out CPos topLeft, out CPos bottomRight)
		{
			var r = GetTileRect(tileX, tileY);
			topLeft = new CPos(r.Left, r.Top);
			bottomRight = new CPos(r.Right - 1, r.Bottom - 1);
		}

		public IReadOnlyList<DistrictEntry> Districts
		{
			get
			{
				var list = new List<DistrictEntry>();
				for (var i = 1; i < districtExists.Length; i++)
				{
					if (!districtExists[i])
						continue;

					var s = districtStats[i];
					list.Add(new DistrictEntry
					{
						Id = i,
						Name = districtNames[i] ?? FluentProvider.GetMessage("district-default-name", "id", i),
						Population = s.Population,
						Households = s.Households,
						Jobs = s.Jobs,
						Happiness = s.AverageHappiness,
						LandValue = s.AverageLandValue,
						ArgbColor = unchecked((int)(0xFF000000u | (uint)DistrictColors[(i - 1) % DistrictColors.Length]))
					});
				}

				return list;
			}
		}
	}
}
