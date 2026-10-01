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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("One road type of the road registry (declare as RoadType@name on the World actor). The index in yaml order plus one is the stable type id.")]
	public class RoadTypeInfo : TraitInfo
	{
		[Desc("Internal name, used in unlock keys (\"road:<Name>\") and orders.")]
		public readonly string Name = "street";

		[FluentReference]
		[Desc("Fluent key of the display name.")]
		public readonly string DisplayName = null;

		[Desc("Road class (Local, Collector, Arterial, Highway). Highway-class cells only connect to other highway cells or ramps.")]
		public readonly RoadClass Class = RoadClass.Local;

		[Desc("Lanes per direction.")]
		public readonly int Lanes = 1;

		[Desc("Speed relative to a street (100).")]
		public readonly int SpeedPercent = 100;

		[Desc("Construction cost per cell.")]
		public readonly int Cost = 10;

		[Desc("Monthly upkeep per cell.")]
		public readonly int Upkeep = 1;

		[Desc("Noise at the source, 0..100.")]
		public readonly int Noise = 20;

		[Desc("Air pollution at the source, 0..100.")]
		public readonly int AirPollution = 10;

		[Desc("Zones may be painted next to this road.")]
		public readonly bool Zoneable = true;

		[Desc("Buildings may use it as their street (access road).")]
		public readonly bool GivesAccess = true;

		[Desc("Carries LV cable.")]
		public readonly bool CarriesCable = true;

		[Desc("Carries water and sewage pipes.")]
		public readonly bool CarriesPipes = true;

		[Desc("Default junction control where this type meets others (None, Yield, Stop, Signal). Higher classes win.")]
		public readonly JunctionControl DefaultControl = JunctionControl.None;

		[Desc("Laid as two one-way carriageways side by side when the paired mode is on.")]
		public readonly bool Paired = false;

		[Desc("Paired mode is forced on (highways).")]
		public readonly bool AlwaysPaired = false;

		[Desc("One-way variants are allowed.")]
		public readonly bool AllowOneWay = true;

		[Desc("May be built as a bridge over water.")]
		public readonly bool AllowBridge = true;

		[Desc("On-street parking spaces per cell.")]
		public readonly int ParkingSlots = 2;

		[Desc("Population needed to unlock when no IProgression exists.")]
		public readonly int UnlockPopulation = 0;

		[Desc("Sprite image holding the sequences of this type.")]
		public readonly string Image = "roadnet";

		[Desc("Sequence with 32 frames: 16 two-way frames then 16 one-way frames, indexed by the N/E/S/W mask (bit0 N, bit1 E, bit2 S, bit3 W).")]
		public readonly string Sequence = "street";

		[Desc("Terrain the road can be built on besides the layer default (empty = layer default).")]
		public readonly HashSet<string> ExtraTerrain = [];

		public override object Create(ActorInitializer init) { return new RoadTypeMarker(this); }
	}

	/// <summary>Instance trait that only exposes the info (RoadLayer reads the infos from the World actor).</summary>
	public class RoadTypeMarker
	{
		public readonly RoadTypeInfo Info;
		public RoadTypeMarker(RoadTypeInfo info) { Info = info; }
	}

	/// <summary>Runtime data of a road type (registry entry) with its stable id.</summary>
	public sealed class RoadTypeData
	{
		public readonly byte Id;
		public readonly RoadTypeInfo Info;

		public RoadTypeData(byte id, RoadTypeInfo info)
		{
			Id = id;
			Info = info;
		}

		public string Name => Info.Name;
		public RoadClass Class => Info.Class;
		public bool IsHighway => Info.Class == RoadClass.Highway;
	}
}
