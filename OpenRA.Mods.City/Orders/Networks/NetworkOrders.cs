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

using OpenRA.Mods.City.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Road tool settings carried in Order.ExtraData of CityOrders.BuildRoad.
	/// Bits 0..7 road type id (0 = default street, so old orders without ExtraData build streets),
	/// bit 8 one-way, bit 9 reverse the flow, bit 10 paired (two one-way carriageways), bit 11 replace existing roads, bit 12 allow bridges.
	/// </summary>
	public struct RoadToolOptions
	{
		public const uint OneWayBit = 1u << 8;
		public const uint ReverseBit = 1u << 9;
		public const uint PairedBit = 1u << 10;
		public const uint ReplaceBit = 1u << 11;
		public const uint BridgeBit = 1u << 12;

		/// <summary>Registry id (1 based), 0 = the layer's default type.</summary>
		public byte TypeId;
		public bool OneWay;
		public bool Reverse;
		public bool Paired;
		public bool Replace;

		/// <summary>Bridge water runs of up to RoadLayerInfo.BridgeMaxSpan cells (cost x3).</summary>
		public bool Bridge;

		public readonly uint ToExtraData()
		{
			return TypeId | (OneWay ? OneWayBit : 0) | (Reverse ? ReverseBit : 0) | (Paired ? PairedBit : 0) | (Replace ? ReplaceBit : 0) | (Bridge ? BridgeBit : 0);
		}

		public static RoadToolOptions FromExtraData(uint data)
		{
			return new RoadToolOptions
			{
				TypeId = (byte)(data & 0xff),
				OneWay = (data & OneWayBit) != 0,
				Reverse = (data & ReverseBit) != 0,
				Paired = (data & PairedBit) != 0,
				Replace = (data & ReplaceBit) != 0,
				Bridge = (data & BridgeBit) != 0,
			};
		}

		/// <summary>Plain two-way street that keeps existing roads (what the legacy order builds).</summary>
		public static RoadToolOptions Default => default;
	}

	/// <summary>
	/// Order strings and factories of the road and utility network tools (NET). Resolved by ConstructionTools (roads)
	/// and UtilityTools (power lines, pipes). Every Subject is the issuing player's PlayerActor.
	/// </summary>
	public static class NetworkOrders
	{
		/// <summary>Target = junction cell, ExtraData = 0 default, else (uint)JunctionControl + 1. Handled by ConstructionTools.</summary>
		public const string SetRoadControl = "CitySetRoadControl";

		/// <summary>Target = centre cell, TargetString = "roundabout", "roundabout-large" or "ramp". Handled by ConstructionTools.</summary>
		public const string PlaceRoadPrefab = "CityPlaceRoadPrefab";

		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData bit 0 = remove instead of build. HV line along an L path. Handled by UtilityTools.</summary>
		public const string BuildPowerLine = "CityBuildPowerLine";

		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData bits 0..1 = pipe kind (1 water, 2 sewage, 3 both), bit 8 = remove. Handled by UtilityTools.</summary>
		public const string BuildPipe = "CityBuildPipe";

		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData = (uint)RoadAddons flag, bit 8 = remove. Handled by ConstructionTools.</summary>
		public const string RoadAddon = "CityRoadAddon";

		public const string PrefabRoundabout = "roundabout";
		public const string PrefabRoundaboutLarge = "roundabout-large";
		public const string PrefabRamp = "ramp";

		/// <summary>Road drag with a type and modes. With default options this equals CityOrders.BuildRoadOrder.</summary>
		public static Order BuildRoad(Player p, CPos from, CPos to, RoadToolOptions options) =>
			new(CityOrders.BuildRoad, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = options.ToExtraData() };

		/// <summary>value null = back to the default control of the road types meeting there.</summary>
		public static Order SetControl(Player p, CPos cell, JunctionControl? value) =>
			new(SetRoadControl, p.PlayerActor, Target.FromCell(p.World, cell), false) { ExtraData = value == null ? 0u : (uint)value.Value + 1 };

		public static Order PlaceRoadPrefabOrder(Player p, CPos centre, string prefab) =>
			new(PlaceRoadPrefab, p.PlayerActor, Target.FromCell(p.World, centre), false) { TargetString = prefab };

		/// <summary>Drag along existing roads to add (or remove) one add-on: trees, barrier, lights, parking, bus lane, bike lane.</summary>
		public static Order RoadAddonOrder(Player p, CPos from, CPos to, RoadAddons addon, bool remove = false) =>
			new(RoadAddon, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = (uint)addon | (remove ? 256u : 0u) };

		public static Order PowerLine(Player p, CPos from, CPos to, bool remove = false) =>
			new(BuildPowerLine, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = remove ? 1u : 0u };

		/// <summary>kind: 1 water, 2 sewage, 3 both.</summary>
		public static Order Pipe(Player p, CPos from, CPos to, int kind, bool remove = false) =>
			new(BuildPipe, p.PlayerActor, Target.FromCell(p.World, to), false) { ExtraLocation = from, ExtraData = (uint)(kind & 3) | (remove ? 256u : 0u) };
	}
}
