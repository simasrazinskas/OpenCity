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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Optional road flags (ITrafficRoadFlags): bus lanes and tram tracks. Without a provider every flag is 0 and nothing changes.
	public sealed partial class TrafficSim
	{
		const byte FlagBusLane = 1, FlagTram = 2, FlagCrossing = 4;

		byte[] laneFlags;
		ITrafficRoadFlags roadFlags;
		bool roadFlagsSearched;
		int tramCells, crossingCells;
		bool anyLaneFlag;
		ITransitUiSourceEx transit;
		TransitLayer transitLayer;
		int transitVersion = -1;

		void FindRoadFlags()
		{
			if (roadFlagsSearched)
				return;

			roadFlagsSearched = true;
			transit = world.WorldActor.TraitsImplementing<ITransitUiSourceEx>().FirstOrDefault();
			transitLayer = world.WorldActor.TraitOrDefault<TransitLayer>();
			roadFlags = net as ITrafficRoadFlags ?? world.WorldActor.TraitsImplementing<ITrafficRoadFlags>().FirstOrDefault();

			// NET's RoadLayer exposes bus lanes as road add-ons (no tram rails yet).
			if (roadFlags == null && net is RoadLayer layer)
				roadFlags = new RoadLayerFlags(layer);
		}

		sealed class RoadLayerFlags : ITrafficRoadFlags
		{
			readonly RoadLayer layer;

			public RoadLayerFlags(RoadLayer layer) { this.layer = layer; }

			public bool HasBusLane(CPos cell) { return layer.HasBusLane(cell); }

			public bool HasTramTrack(CPos cell) { return false; }
		}

		void RebuildLaneFlags()
		{
			laneFlags ??= new byte[cellCount];
			System.Array.Clear(laneFlags);
			tramCells = 0;
			crossingCells = 0;
			anyLaneFlag = false;
			FindRoadFlags();
			transitVersion = transit?.Version ?? -1;
			if (roadFlags == null && transit == null)
				return;

			for (var k = 0; k < roadListCount; k++)
			{
				var i = roadList[k];
				var c = ToCPos(i);
				byte f = 0;
				if (roadFlags != null && roadFlags.HasBusLane(c))
					f |= FlagBusLane;

				if ((roadFlags != null && roadFlags.HasTramTrack(c)) || (transit != null && transit.HasTramTrack(c)))
				{
					f |= FlagTram;
					tramCells++;
				}

				// Level crossings: road vehicles wait while a train is at or approaching (TransitLayer.IsCrossingClosed).
				if (transit != null && transitLayer != null && transit.IsLevelCrossing(c))
				{
					f |= FlagCrossing;
					crossingCells++;
				}

				laneFlags[i] = f;
				anyLaneFlag |= f != 0;
			}
		}

		// Transit track and crossings change without a road change: poll the PT version.
		void CheckRoadFlags()
		{
			if (transit == null || transit.Version == transitVersion)
				return;

			RebuildLaneFlags();
			flagsActive = anyLaneFlag || policyAny;
			RefreshCostSnapshot();
		}

		bool CrossingClosed(int cell)
		{
			return (laneFlags[cell] & FlagCrossing) != 0 && transitLayer.IsCrossingClosed(ToCPos(cell));
		}

		// Buses and emergency vehicles on a bus lane, and trams on rails, drive free flow (other traffic does not slow them).
		bool FreeOccupancy(byte kind, int cell)
		{
			if (laneFlags == null)
				return false;

			var f = laneFlags[cell];
			if (f == 0)
				return false;

			return ((f & FlagBusLane) != 0 && (kind == KindBus || kind == KindEmergency || kind == KindTram)) || ((f & FlagTram) != 0 && kind == KindTram);
		}

		// Extra route cost factor (percent) and bans for the vehicle kind entering `next`; 100 = neutral.
		int RoadFlagCostPercent(byte kind, int next)
		{
			var pct = 100;
			if (laneFlags != null)
			{
				var f = laneFlags[next];
				if (kind == KindTram)
					return tramCells > 0 && (f & FlagTram) == 0 ? 100000 : 100;

				if ((f & FlagBusLane) != 0 && (kind == KindCar || kind == KindTruck))
					pct = 150;
			}

			if (policyFlags != null)
				pct = (int)Math.Min(100000L, (long)pct * PolicyCostPercent(kind, next) / 100);

			return pct;
		}
	}
}
