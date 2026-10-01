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
	/// <summary>Per-cell road add-ons (bit flags stored in RoadLayer). The yaml registry (RoadAddon@name) gives them costs and effects.</summary>
	[Flags]
	public enum RoadAddons : byte
	{
		None = 0,
		Trees = 1,
		Barrier = 2,
		Lights = 4,
		Parking = 8,
		BusLane = 16,
		BikeLane = 32,
	}

	[TraitLocation(SystemActors.World)]
	[Desc("One road add-on (declare as RoadAddon@name on the World actor): trees, barrier, lights, parking, buslane, bikelane.",
		"Defaults are neutral (no noise change, no land value, no parking), so a bare entry only costs money.")]
	public class RoadAddonInfo : TraitInfo
	{
		[Desc("Internal name: trees, barrier, lights, parking, buslane or bikelane.")]
		public readonly string Name = "trees";

		[FluentReference]
		[Desc("Fluent key of the display name.")]
		public readonly string DisplayName = null;

		[Desc("Construction cost per cell.")]
		public readonly int Cost = 2;

		[Desc("Monthly upkeep per 10 cells.")]
		public readonly int UpkeepPer10 = 1;

		[Desc("Noise multiplier in percent (100 = no change, 50 = halves the noise of the road cell).")]
		public readonly int NoisePercent = 100;

		[Desc("Air pollution multiplier in percent.")]
		public readonly int AirPercent = 100;

		[Desc("Crime multiplier in percent around the cell (street lights reduce it). Hook for the services WP.")]
		public readonly int CrimePercent = 100;

		[Desc("Land value points added to the cells next to the road (read through RoadLayer.GetLandValueBonus).")]
		public readonly int LandValue = 0;

		[Desc("Extra on-street parking spaces in the cell.")]
		public readonly int ParkingSlots = 0;

		public override object Create(ActorInitializer init) { return new RoadAddonMarker(this); }
	}

	public class RoadAddonMarker
	{
		public readonly RoadAddonInfo Info;
		public RoadAddonMarker(RoadAddonInfo info) { Info = info; }
	}

	/// <summary>Registry entry of an add-on.</summary>
	public sealed class RoadAddonData
	{
		public readonly RoadAddons Flag;
		public readonly RoadAddonInfo Info;

		public RoadAddonData(RoadAddons flag, RoadAddonInfo info)
		{
			Flag = flag;
			Info = info;
		}

		public string Name => Info.Name;

		public static RoadAddons FlagOf(string name)
		{
			switch (name)
			{
				case "trees": return RoadAddons.Trees;
				case "barrier": return RoadAddons.Barrier;
				case "lights": return RoadAddons.Lights;
				case "parking": return RoadAddons.Parking;
				case "buslane": return RoadAddons.BusLane;
				case "bikelane": return RoadAddons.BikeLane;
				default: return RoadAddons.None;
			}
		}
	}
}
