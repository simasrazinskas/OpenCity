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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// Data-only traits: the progression catalogue lives in yaml as player trait instances, e.g.
	//   ProgressionMilestone@3: Name: Town, Xp: 2000, Money: 15000, DevPoints: 2, Tiles: 2
	// The Progression trait reads them from the actor info in yaml order (milestone index = order of appearance, 1-based).
	[TraitLocation(SystemActors.Player)]
	[Desc("One milestone (data only). Index 1..20 follows the order of appearance in yaml; milestone 0 (start) is implicit.")]
	public class ProgressionMilestoneInfo : TraitInfo
	{
		[Desc("Display name, e.g. 'Hamlet'.")]
		public readonly string Name = "";

		[Desc("Cumulative XP needed.")]
		public readonly int Xp = 0;

		[Desc("Cash reward, booked as 'milestone' income.")]
		public readonly int Money = 0;

		public readonly int DevPoints = 0;

		[Desc("Map tile purchase permits granted.")]
		public readonly int Tiles = 0;

		[Desc("Unlock keys: actor names, zone:<ZoneType>, road:<type>, tool:<name>, service:<name>.",
			"Policies and nodes unlock via their own Milestone / Node fields.")]
		public readonly string[] Unlocks = [];

		public override object Create(ActorInitializer init) { return new ProgressionDataMarker(); }
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("One development tree node (data only). The instance name (after @) is the node id.")]
	public class ProgressionNodeInfo : TraitInfo
	{
		[Desc("Tree (service) the node belongs to, for the UI.")]
		public readonly string Tree = "";

		[Desc("Development points to buy it (CS2 tiers 1/2/4/8). 0 = granted automatically when the milestone is reached.")]
		public readonly int Cost = 1;

		[Desc("Node ids that must be owned first.")]
		public readonly string[] Requires = [];

		[Desc("Unlock keys granted by the node.")]
		public readonly string[] Unlocks = [];

		[Desc("Milestone the node becomes available at.")]
		public readonly int Milestone = 1;

		[Desc("Row/column hint for the tree layout in the UI.")]
		public readonly int Tier = 1;

		public override object Create(ActorInitializer init) { return new ProgressionDataMarker(); }
	}

	public enum PolicyScope : byte { City, District }

	[TraitLocation(SystemActors.Player)]
	[Desc("One policy (data only). The instance name (after @) is the policy id: unlock key 'policy:<id>', fluent keys 'policy-<id>' / 'policy-<id>-desc'.")]
	public class ProgressionPolicyInfo : TraitInfo
	{
		public readonly PolicyScope Scope = PolicyScope.City;

		[Desc("Milestone that makes the policy available (-1 = only via a node's Unlocks).")]
		public readonly int Milestone = 4;

		[Desc("Monthly upkeep in dollars plus per 100 residents (of the city / district).")]
		public readonly int Upkeep = 0;

		public readonly int UpkeepPer100Residents = 0;

		[Desc("Slider range. SliderMax = 0 means a plain on/off policy.")]
		public readonly int SliderMin = 0;

		public readonly int SliderMax = 0;

		public readonly int SliderDefault = 0;

		[Desc("Integer effects 'Key=Value'. Value '@slider' uses the slider value. Summed over city + district policies.")]
		public readonly string[] Effects = [];

		public override object Create(ActorInitializer init) { return new ProgressionDataMarker(); }
	}

	/// <summary>Placeholder runtime object for data-only traits.</summary>
	public sealed class ProgressionDataMarker { }

	[Desc("Per-building progression values: XP for placing it, tourist attractiveness, landmark flag. Add to any placeable in yaml.")]
	public class ProgressionValueInfo : TraitInfo
	{
		[Desc("XP granted when the player places it (full for the first FullCount of its type, then reduced).")]
		public readonly int Xp = 0;

		[Desc("Tourist attractiveness points while operational.")]
		public readonly int Attractiveness = 0;

		[Desc("Landmark: XP only once per type; counts for 'making a mark'.")]
		public readonly bool Landmark = false;

		public override object Create(ActorInitializer init) { return new ProgressionValue(this); }
	}

	public class ProgressionValue
	{
		public readonly ProgressionValueInfo Info;

		public ProgressionValue(ProgressionValueInfo info)
		{
			Info = info;
		}
	}

	/// <summary>Order strings and factories of the progression WP (resolved by the Progression player trait).</summary>
	public static class ProgressionOrders
	{
		/// <summary>TargetString = node id.</summary>
		public const string UnlockNode = "CityUnlockNode";

		/// <summary>ExtraLocation = tile (x, y) in the 9x9 grid.</summary>
		public const string BuyTile = "CityBuyTile";

		/// <summary>ExtraLocation = (policy index, district id or 0 = city), ExtraData = 0 off, 1 on, or the slider value (> 0).</summary>
		public const string SetPolicy = "CitySetPolicy";

		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData = district id (0 = erase, 255 = new district).</summary>
		public const string DistrictPaint = "CityDistrict";

		/// <summary>ExtraData = district id, TargetString = name.</summary>
		public const string DistrictName = "CityDistrictName";

		/// <summary>ExtraData = district id.</summary>
		public const string DistrictDelete = "CityDistrictDelete";

		public const uint NewDistrict = 255;

		public static Order UnlockNodeOrder(Player p, string nodeId) =>
			new(UnlockNode, p.PlayerActor, false) { TargetString = nodeId };

		public static Order BuyTileOrder(Player p, int tileX, int tileY) =>
			new(BuyTile, p.PlayerActor, false) { ExtraLocation = new CPos(tileX, tileY) };

		public static Order SetPolicyOrder(Player p, int policyIndex, int districtId, int value) =>
			new(SetPolicy, p.PlayerActor, false) { ExtraLocation = new CPos(policyIndex, districtId), ExtraData = (uint)Math.Max(0, value) };

		public static Order DistrictPaintOrder(Player p, CPos from, CPos to, int districtId) =>
			new(DistrictPaint, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = (uint)districtId };

		public static Order NewDistrictPaintOrder(Player p, CPos from, CPos to) =>
			new(DistrictPaint, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = NewDistrict };

		public static Order DistrictNameOrder(Player p, int districtId, string name) =>
			new(DistrictName, p.PlayerActor, false) { TargetString = name, ExtraData = (uint)districtId };

		public static Order DistrictDeleteOrder(Player p, int districtId) =>
			new(DistrictDelete, p.PlayerActor, false) { ExtraData = (uint)districtId };
	}
}
