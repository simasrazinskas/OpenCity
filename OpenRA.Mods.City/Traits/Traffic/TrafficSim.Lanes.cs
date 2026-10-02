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

namespace OpenRA.Mods.City.Traits
{
	// Render-only lane geometry per road cell, from NET's lane contract (design/iso/net/INVENTORY.md). Units: world units
	// (1 q = 1/64 cell = 16), measured from the road centreline, + = right of the travel direction (right-hand traffic).
	public sealed partial class TrafficSim
	{
		sealed class LaneProfile
		{
			/// <summary>Lane centres of one travel direction, innermost (leftmost) first.</summary>
			public int[] Lanes;

			/// <summary>Kerb distance of the parked-car centre, 0 = no on-street parking spot.</summary>
			public int Parked;

			/// <summary>Sidewalk centre distance, 0 = no sidewalk.</summary>
			public int Sidewalk;
		}

		// Index = profile id (see ProfileOf). Values: NET lane centres x 16, half-width - 1 q for parking, cell edge - sidewalk/2.
		static readonly LaneProfile[] Profiles =
		[
			new() { Lanes = [160], Parked = 304, Sidewalk = 416 },              // 0 street two-way
			new() { Lanes = [-160, 160], Parked = 304, Sidewalk = 416 },        // 1 street one-way
			new() { Lanes = [0], Parked = 224, Sidewalk = 352 },                // 2 alley (shared lane, flat apron)
			new() { Lanes = [0], Parked = 240, Sidewalk = 0 },                  // 3 gravel
			new() { Lanes = [128, 328], Parked = 400, Sidewalk = 464 },         // 4 avenue two-way
			new() { Lanes = [-176, 176], Parked = 336, Sidewalk = 432 },        // 5 avenue one-way (paired)
			new() { Lanes = [160, 352], Parked = 416, Sidewalk = 472 },         // 6 boulevard two-way
			new() { Lanes = [-256, 0, 256], Parked = 368, Sidewalk = 448 },     // 7 boulevard one-way (paired)
			new() { Lanes = [152, 328], Parked = 0, Sidewalk = 0 },             // 8 highway two-way
			new() { Lanes = [-232, 0, 232], Parked = 0, Sidewalk = 0 },         // 9 highway one-way (paired)
		];

		byte[] laneProfile;
		int laneProfileVersion = int.MinValue;

		LaneProfile ProfileOfCell(int cell)
		{
			if (laneProfile == null || laneProfileVersion != net.NetworkVersion)
			{
				laneProfile ??= new byte[cellCount];
				Array.Fill(laneProfile, (byte)255);
				laneProfileVersion = net.NetworkVersion;
			}

			var p = laneProfile[cell];
			if (p == 255)
				laneProfile[cell] = p = ProfileOf(ToCPos(cell));

			return Profiles[p];
		}

		byte ProfileOf(CPos c)
		{
			var name = "street";
			var oneWay = false;
			if (net is RoadLayer layer)
			{
				name = layer.GetRoadType(c)?.Name ?? name;
				oneWay = layer.GetOneWay(c) >= 0 || layer.IsPaired(c);
			}
			else
			{
				switch (net.GetClass(c))
				{
					case RoadClass.Collector: name = "avenue"; break;
					case RoadClass.Arterial: name = "boulevard"; break;
					case RoadClass.Highway: name = "highway"; break;
				}
			}

			return name switch
			{
				"alley" => 2,
				"gravel" => 3,
				"avenue" => (byte)(oneWay ? 5 : 4),
				"boulevard" => (byte)(oneWay ? 7 : 6),
				"highway" => (byte)(oneWay ? 9 : 8),
				_ => (byte)(oneWay ? 1 : 0),
			};
		}

		/// <summary>Render lane of a vehicle: maps the simulation's lanes onto NET's drawn lanes (spreads single-lane one-ways over both).</summary>
		static int RenderLane(LaneProfile p, int simLane, int simLanes, int vehicle)
		{
			var drawn = p.Lanes.Length;
			if (drawn == simLanes)
				return simLane;

			if (drawn > simLanes)
				return Math.Min(drawn - 1, simLane * drawn / simLanes + (vehicle & 0x7fff) % Math.Max(1, drawn / simLanes));

			return Math.Min(drawn - 1, simLane * drawn / simLanes);
		}
	}
}
