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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Road graph snapshot: flat per-cell arrays derived from IRoadNetwork, and the per-link queue state arrays.
	public sealed partial class TrafficSim
	{
		// Per cell.
		byte[] roadFlag;
		byte[] walkMask;
		bool[] sidewalk;
		WalkingRouter walkingRouter;
		byte[] exitMask;     // bit d: a vehicle may leave this cell towards Neighbours4[d]
		byte[] cellLanes;
		byte[] cellClass;
		byte[] ctl;          // JunctionControl for cells with 3+ legs, None otherwise
		byte[] parkSlots;
		int[] freightLoad;   // vehicle equivalents * 1000 added through IFreightFlow, decays
		byte[] parkUsed;
		bool[] gateFlag;
		int[] ffU;           // free-flow time of a car crossing the cell
		int[] roadList = [];
		int roadListCount;
		readonly List<int> gates = [];

		// Per link (cell * 4 + entry heading).
		int[] qHead, qTail, qOcc, qCount, nextRelU, blockedSince, emaU, costSnap;
		byte[] approachRank;

		int graphVersion = -1;
		int graphRebuilds;
		int minFfU = 1;

		void InitGraph()
		{
			roadFlag = new byte[cellCount];
			walkMask = new byte[cellCount];
			sidewalk = new bool[cellCount];
			walkingRouter = new WalkingRouter(width, height);
			exitMask = new byte[cellCount];
			cellLanes = new byte[cellCount];
			cellClass = new byte[cellCount];
			ctl = new byte[cellCount];
			parkSlots = new byte[cellCount];
			parkUsed = new byte[cellCount];
			freightLoad = new int[cellCount];
			gateFlag = new bool[cellCount];
			ffU = new int[cellCount];
			approachRank = new byte[linkCount];
		}

		void InitLinks()
		{
			qHead = new int[linkCount];
			qTail = new int[linkCount];
			qOcc = new int[linkCount];
			qCount = new int[linkCount];
			nextRelU = new int[linkCount];
			blockedSince = new int[linkCount];
			emaU = new int[linkCount];
			costSnap = new int[linkCount];
			Array.Fill(qHead, NoVehicle);
			Array.Fill(qTail, NoVehicle);
			Array.Fill(blockedSince, -1);
			InitFlowState();
			InitAccidents();
		}

		int Storage(int cell) { return cellLanes[cell] * Info.SlotsPerLane; }

		void EnsureGraph()
		{
			if (net.NetworkVersion == graphVersion)
				return;

			graphVersion = net.NetworkVersion;
			RebuildGraph();
		}

		void RebuildGraph()
		{
			graphRebuilds++;
			Array.Clear(roadFlag);
			Array.Clear(exitMask);
			Array.Clear(ctl);
			Array.Clear(approachRank);

			var list = new List<int>();
			foreach (var c in net.RoadCells)
				if (InMap(c))
					list.Add(Cell(c));

			list.Sort();
			roadList = list.ToArray();
			roadListCount = roadList.Length;
			foreach (var i in roadList)
				roadFlag[i] = 1;

			minFfU = int.MaxValue;
			foreach (var i in roadList)
			{
				var cell = ToCPos(i);
				var mask = 0;
				for (var d = 0; d < 4; d++)
				{
					var n = cell + CityUtils.Neighbours4[d];
					if (InMap(n) && roadFlag[Cell(n)] != 0 && net.CanEnter(cell, d))
						mask |= 1 << d;
				}

				exitMask[i] = (byte)mask;
				cellLanes[i] = (byte)Math.Clamp(net.GetLanes(cell), 1, 4);
				cellClass[i] = (byte)net.GetClass(cell);
				parkSlots[i] = (byte)Math.Clamp(net.GetParkingSlots(cell), 0, 8);

				var speed = Math.Max(10, net.GetSpeedPercent(cell));
				ffU[i] = Math.Max(8, (int)((long)CarMilliTicks * U * 100 / (1000L * speed)));
				minFfU = Math.Min(minFfU, ffU[i]);
			}

			if (minFfU == int.MaxValue)
				minFfU = 1;

			Array.Clear(walkMask);
			Array.Clear(sidewalk);
			foreach (var i in roadList)
			{
				ComputeControl(i);
				sidewalk[i] = ProfileOfCell(i).Sidewalk > 0;
			}

			foreach (var i in roadList)
			{
				if (!sidewalk[i])
					continue;

				for (var d = 0; d < 4; d++)
				{
					var n = ToCPos(i) + CityUtils.Neighbours4[d];
					if (InMap(n) && sidewalk[Cell(n)] && HasLeg(i, d))
						walkMask[i] |= (byte)(1 << d);
				}
			}

			// Outside connections: highway entry cells, ordered by actor id.
			gates.Clear();
			Array.Clear(gateFlag);
			foreach (var tp in world.ActorsWithTrait<OutsideConnection>().OrderBy(t => t.Actor.ActorID))
			{
				var a = tp.Actor;
				if (a.IsInWorld && InMap(a.Location) && roadFlag[Cell(a.Location)] != 0)
				{
					gates.Add(Cell(a.Location));
					gateFlag[Cell(a.Location)] = true;
				}
			}

			// Vehicles standing on cells that are no longer roads are lost.
			for (var v = 0; v < vCapacity; v++)
			{
				if (vLink[v] < 0)
					continue;

				if (roadFlag[vLink[v] >> 2] == 0)
					FailVehicle(v, TripFailure.RoadClosed);
			}

			// Parking spaces that disappeared.
			for (var i = 0; i < cellCount; i++)
				if (parkUsed[i] > parkSlots[i])
					parkUsed[i] = parkSlots[i];

			RebuildLaneFlags();
			PolicyAfterRebuild();
			RefreshCostSnapshot();
			costVersion++;
		}

		// A leg is a neighbouring road cell connected to this one in at least one direction.
		bool HasLeg(int cell, int d)
		{
			var n = ToCPos(cell) + CityUtils.Neighbours4[d];
			if (!InMap(n))
				return false;

			var ni = Cell(n);
			return roadFlag[ni] != 0 && ((exitMask[cell] & (1 << d)) != 0 || (exitMask[ni] & (1 << ((d + 2) & 3))) != 0);
		}

		void ComputeControl(int cell)
		{
			var legs = 0;
			var maxClass = 0;
			var minClass = 99;
			var maxLanes = 0;
			for (var d = 0; d < 4; d++)
			{
				if (!HasLeg(cell, d))
					continue;

				legs++;
				var ni = Cell(ToCPos(cell) + CityUtils.Neighbours4[d]);
				maxClass = Math.Max(maxClass, cellClass[ni]);
				minClass = Math.Min(minClass, cellClass[ni]);
				maxLanes = Math.Max(maxLanes, cellLanes[ni]);
			}

			if (legs < 3)
				return;

			var explicitControl = net.GetControl(ToCPos(cell));
			JunctionControl control;
			if (explicitControl != JunctionControl.None)
				control = explicitControl;
			else if (maxLanes >= 3 || (legs == 4 && maxClass >= (int)RoadClass.Arterial))
				control = JunctionControl.Signal;
			else if (minClass != maxClass || legs == 3)
				control = JunctionControl.Yield;
			else
				control = JunctionControl.Stop;

			ctl[cell] = (byte)control;

			// Approach ranks for priority junctions: higher road class and through legs win over stems and lower classes.
			for (var h = 0; h < 4; h++)
			{
				var from = ToCPos(cell) - CityUtils.Neighbours4[h];
				var rank = 0;
				if (InMap(from) && roadFlag[Cell(from)] != 0)
				{
					rank = cellClass[Cell(from)] * 2;
					if (HasLeg(cell, h))
						rank++;
				}

				approachRank[cell * 4 + h] = (byte)rank;
			}
		}

		void RefreshCostSnapshot()
		{
			for (var k = 0; k < roadListCount; k++)
			{
				var cell = roadList[k];
				var s = Storage(cell);
				for (var h = 0; h < 4; h++)
				{
					var l = cell * 4 + h;
					var cost = Math.Max(ffU[cell], emaU[l]);

					// Heavily filled links are expensive even when the experienced time has not caught up yet; freight counts as occupancy.
					var occ = qOcc[l] + freightLoad[cell] / 2000;
					if (occ * 4 > s * 3)
						cost += ffU[cell] * (occ * 4 - s * 3) / Math.Max(1, s);

					if (laneBlock[l] > 0)
						cost += ffU[cell] * (laneBlock[l] >= cellLanes[cell] ? 80 : 15);

					costSnap[l] = cost;
				}
			}

			costVersion++;
		}

		// Speed-density travel time (Greenshields) for a vehicle entering link `l` with `occ` slots already inside.
		int LinkTime(int cell, int occ, int speedPct)
		{
			var s = Storage(cell);
			var k = Math.Min(14, 16 * occ / Math.Max(1, s));
			var t = (long)ffU[cell] * 16 / (16 - k);
			if (speedPct != 100)
				t = t * 100 / speedPct;

			return (int)t;
		}
	}
}
