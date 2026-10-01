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

namespace OpenRA.Mods.City.Traits
{
	// Flow metrics, parking-lite and the state hash.
	public sealed partial class TrafficSim
	{
		const int WheelSize = 1024;

		// Rolling flow window: 4 buckets of 25 ticks (free-flow ticks vs experienced ticks, in units).
		readonly long[] flowFree = new long[4];
		readonly long[] flowActual = new long[4];
		int flowBucket;

		readonly List<int>[] parkWheel = new List<int>[WheelSize];
		int parkedNow, parkOverflow, parkSearches;
		long tripTickSum, tripCellSum;
		int[] bfsStamp, bfsQueue, bfsDepth;
		int bfsCounter;

		// A vehicle leaves link l: update the experienced time (EMA) and the flow window.
		void Traversed(int l, int v)
		{
			var cell = l >> 2;
			CountTraversal(cell);
			var speed = vTrip[v].SpeedPct;
			var actual = (int)((long)(nowU - vEnterU[v]) * speed / 100);
			var free = ffU[cell];
			if (actual < free)
				actual = free;

			emaU[l] = emaU[l] == 0 ? actual : (emaU[l] * 7 + actual) / 8;
			flowFree[flowBucket] += free;
			flowActual[flowBucket] += actual;
		}

		// ---- parking ----

		// Called when a vehicle reaches the end of its route. A car that finds the destination full cruises to a nearby free space once.
		bool TryParkSearch(int v, int cell, int h)
		{
			var t = vTrip[v];
			if (!t.Parks || t.Searched || gateFlag[t.ParkCell >= 0 ? t.ParkCell : t.Destination])
				return false;

			var target = t.ParkCell >= 0 ? t.ParkCell : t.Destination;
			if (parkUsed[target] < parkSlots[target])
				return false;

			t.Searched = true;
			parkSearches++;
			var free = FindFreeParking(cell, Info.ParkSearchRadius);
			if (free < 0)
				return false;

			if (!FindRouteForTrip(t, cell, h, free, t.SpeedPct, false, 0, out var route, out _))
				return false;

			t.ParkCell = free;
			vRoute[v] = route;
			vStep[v] = 0;
			return true;
		}

		int FindFreeParking(int start, int radius)
		{
			if (bfsStamp == null)
			{
				bfsStamp = new int[cellCount];
				bfsQueue = new int[cellCount];
				bfsDepth = new int[cellCount];
			}

			bfsCounter++;
			var head = 0;
			var tail = 0;
			bfsQueue[tail++] = start;
			bfsStamp[start] = bfsCounter;
			bfsDepth[start] = 0;
			while (head < tail)
			{
				var c = bfsQueue[head++];
				if (c != start && parkUsed[c] < parkSlots[c])
					return c;

				if (bfsDepth[c] >= radius)
					continue;

				for (var d = 0; d < 4; d++)
				{
					if ((exitMask[c] & (1 << d)) == 0)
						continue;

					var n = c + dIdx[d];
					if (bfsStamp[n] == bfsCounter)
						continue;

					bfsStamp[n] = bfsCounter;
					bfsDepth[n] = bfsDepth[c] + 1;
					bfsQueue[tail++] = n;
				}
			}

			return -1;
		}

		// Final arrival of a trip: take a parking space (or overflow) and tell the owner.
		void FinishArrival(TripRec t)
		{
			if (t.Parks && !gateFlag[t.ParkCell >= 0 ? t.ParkCell : t.Destination])
			{
				var cell = t.ParkCell >= 0 ? t.ParkCell : t.Destination;
				if (parkUsed[cell] < parkSlots[cell])
				{
					parkUsed[cell]++;
					parkedNow++;
					var spread = (Hash(t.Id, 5) & 0xffff) % Math.Max(1, Info.ParkDwellTicks);
					var dwell = Math.Min(WheelSize - 1, Info.ParkDwellTicks / 2 + spread);
					var slot = (tick + dwell) % WheelSize;
					(parkWheel[slot] ??= []).Add(cell);
				}
				else
					parkOverflow++;
			}

			if (t.Incident != 0)
				((ITrafficIncidents)this).ResolveIncident(t.Incident);

			Arrive(t, tick);
		}

		void Arrive(TripRec t, int arriveTick)
		{
			if (t.Length >= 10)
			{
				tripTickSum += arriveTick - t.StartTick;
				tripCellSum += t.Length;
			}

			Notify(t, true, 0, arriveTick);
		}

		void TickParking()
		{
			var list = parkWheel[tick % WheelSize];
			if (list == null || list.Count == 0)
				return;

			foreach (var c in list)
			{
				if (parkUsed[c] > 0)
				{
					parkUsed[c]--;
					parkedNow--;
				}
			}

			list.Clear();
		}

		void UpdateStats()
		{
			// Rolling flow: sum the last four 25-tick buckets, then open a new one.
			long free = 0, actual = 0;
			for (var i = 0; i < 4; i++)
			{
				free += flowFree[i];
				actual += flowActual[i];
			}

			CityTrafficFlow = actual > 0 ? (int)Math.Clamp(free * 100 / actual, 0, 100) : 100;
			SampleFlowHistory();
			DecayVolume();
			flowBucket = (flowBucket + 1) & 3;
			flowFree[flowBucket] = 0;
			flowActual[flowBucket] = 0;

			// Experienced times relax towards free flow on empty links, so old jams do not linger in route costs.
			var blocked = 0;
			for (var k = 0; k < roadListCount; k++)
			{
				var cell = roadList[k];
				freightLoad[cell] -= freightLoad[cell] / 4;
				for (var h = 0; h < 4; h++)
				{
					var l = cell * 4 + h;
					if (qCount[l] == 0)
					{
						if (emaU[l] > ffU[cell])
							emaU[l] = ffU[cell] + (emaU[l] - ffU[cell]) * 3 / 4;
					}
					else if (blockedSince[l] >= 0 && nowU - blockedSince[l] > 10 * U)
						blocked++;
				}
			}

			blockedFronts = blocked;

			unchecked
			{
				var hash = ActiveVehicles * 31 + totalTrips;
				for (var v = 0; v < vCapacity; v++)
				{
					if (vLink[v] < 0)
						continue;

					hash = hash * 16777619 ^ (vLink[v] * 7 + vStep[v]);
					hash = hash * 16777619 ^ vLane[v];
				}

				StateHash = hash;
			}

			if (tick % 1000 == 0 && tick > 0)
				LogPerf();
		}

		void LogPerf()
		{
			var avgMs = watchSamples > 0 ? watchTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / watchSamples : 0;
			var worstMs = worstTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
			var expandedAvg = plannedTotal > 0 ? expandedTotal / plannedTotal : 0;
			Console.WriteLine($"[autotest-perf t={tick}] traffic avg={avgMs:F3}ms worst={worstMs:F2}ms vehicles={ActiveVehicles} " +
				$"planned={plannedTotal} expandedAvg={expandedAvg} treeLookups={treeLookups} unreach={treeUnreach} full={treeFull}");
			watchTicks = 0;
			watchSamples = 0;
			worstTicks = 0;
		}

		public int GetTrafficLoad(CPos cell)
		{
			if (!InMap(cell))
				return 0;

			var c = Cell(cell);
			if (roadFlag[c] == 0)
				return 0;

			var s = Math.Max(1, Storage(c));
			var load = 0;
			for (var h = 0; h < 4; h++)
			{
				var l = c * 4 + h;
				var fill = (qOcc[l] + freightLoad[c] / 2000) * 100 / s;
				var flow = emaU[l] > ffU[c] ? ffU[c] * 100 / emaU[l] : 100;
				load = Math.Max(load, Math.Max(fill, 100 - flow));
			}

			return Math.Min(100, load);
		}

		int ITrafficService.GetTrafficFlow(CPos cell)
		{
			if (!InMap(cell) || roadFlag[Cell(cell)] == 0)
				return 100;

			var c = Cell(cell);
			var flow = 100;
			for (var h = 0; h < 4; h++)
			{
				var l = c * 4 + h;
				if (emaU[l] > ffU[c])
					flow = Math.Min(flow, ffU[c] * 100 / emaU[l]);
			}

			return flow;
		}

		/// <summary>Vehicles currently on the cell's links (volume proxy for info views).</summary>
		public int GetTrafficVolume(CPos cell)
		{
			if (!InMap(cell))
				return 0;

			var c = Cell(cell);
			return qCount[c * 4] + qCount[c * 4 + 1] + qCount[c * 4 + 2] + qCount[c * 4 + 3];
		}

		public int CityTrafficFlow { get; private set; } = 100;

		/// <summary>Freight load from the logistics simulation: adds occupancy to every road cell of the path (decays).</summary>
		void IFreightFlow.AddFlow(IReadOnlyList<CPos> pathCells, int vehicleEquivalents)
		{
			if (vehicleEquivalents <= 0)
				return;

			foreach (var c in pathCells)
			{
				if (!InMap(c))
					continue;

				var i = Cell(c);
				if (roadFlag[i] != 0)
					freightLoad[i] = Math.Min(8000, freightLoad[i] + vehicleEquivalents * 1000);
			}
		}

		int ITrafficService.GetNoise(CPos cell)
		{
			if (!InMap(cell) || roadFlag[Cell(cell)] == 0)
				return 0;

			var c = Cell(cell);
			var occ = qOcc[c * 4] + qOcc[c * 4 + 1] + qOcc[c * 4 + 2] + qOcc[c * 4 + 3];
			return Math.Min(100, occ * 100 / Math.Max(1, Storage(c) * 2));
		}
	}
}
