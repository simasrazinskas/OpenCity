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
	// Vehicle pool (struct of arrays), FIFO link queues and vehicle types.
	public sealed partial class TrafficSim
	{
		const byte KindCar = 0, KindTruck = 1, KindBus = 2, KindEmergency = 3, KindTram = 4;

		sealed class TripRec
		{
			public int Id;
			public int Owner;
			public ITripListener Listener;
			public TripPurpose Purpose;
			public int DepartTick;
			public int StartTick;
			public int Origin, Destination;
			public byte Kind;
			public byte Slots;
			public int SpeedPct;
			public int Sprite;
			public bool Walk;
			public bool Cancelled;
			public bool Internal;     // created by the aggregate source: no listener
			public bool Parks;        // car trip that needs a parking space at the destination
			public bool Searched;     // already looked for a free space once
			public int ParkCell = -1;
			public int Vehicle = NoVehicle;
			public int LastReplan = -100000;
			public int Length;
			public int OriginProperty, DestinationProperty;
			public int Incident;      // responder trips: the incident to clear
			public bool Crashed;
			public byte[] WalkRoute;  // sampled pedestrians (render only)
			public int WalkStart, WalkEnd;
			public byte[] Route;      // planned in advance while waiting for room at the origin
		}

		struct VehType
		{
			public byte Kind;
			public byte Slots;
			public int SpeedPct;
			public int Sprite;
		}

		readonly Dictionary<string, VehType> typeLookup = [];
		readonly Dictionary<int, TripRec> trips = [];
		int spriteCountCars;
		readonly List<string> spriteNames = [];

		// Vehicle pool.
		int vCapacity;
		int[] vNext, vLink, vStep, vEnterU, vReadyU;
		byte[] vLane, vSlots;
		byte[][] vRoute;
		TripRec[] vTrip;
		int[] freeList;
		int freeCount;
		int laneCounter;

		void InitVehicles()
		{
			GrowVehicles(256);
		}

		void GrowVehicles(int capacity)
		{
			var old = vCapacity;
			Array.Resize(ref vNext, capacity);
			Array.Resize(ref vLink, capacity);
			Array.Resize(ref vStep, capacity);
			Array.Resize(ref vEnterU, capacity);
			Array.Resize(ref vReadyU, capacity);
			Array.Resize(ref vLane, capacity);
			Array.Resize(ref vSlots, capacity);
			Array.Resize(ref vRoute, capacity);
			Array.Resize(ref vTrip, capacity);
			Array.Resize(ref freeList, capacity);
			for (var i = old; i < capacity; i++)
				vLink[i] = -1;

			// Lowest ids are handed out first (stack top = lowest).
			for (var i = capacity - 1; i >= old; i--)
				freeList[freeCount++] = i;

			vCapacity = capacity;
		}

		void InitTypes()
		{
			spriteCountCars = Math.Max(1, Info.Cars.Length);
			spriteNames.AddRange(Info.Cars);
			AddTypes(Info.Trucks, KindTruck, 2, 80);
			AddTypes(Info.Buses, KindBus, 2, 80);
			AddTypes(Info.Trams, KindTram, 2, 80);
			AddTypes(Info.Emergency, KindEmergency, 1, 150);
			AddTypes(Info.Others, KindCar, 1, 100);
		}

		void AddTypes(string[] defs, byte kind, byte slots, int speed)
		{
			foreach (var d in defs)
			{
				var parts = d.Split(':');
				var image = parts.Length > 1 ? parts[1] : parts[0];
				var sprite = spriteNames.IndexOf(image);
				if (sprite < 0)
				{
					spriteNames.Add(image);
					sprite = spriteNames.Count - 1;
				}

				// Fire engines are long vehicles.
				var s = image == "firetruck" ? (byte)2 : slots;
				typeLookup[parts[0]] = new VehType { Kind = kind, Slots = s, SpeedPct = speed, Sprite = sprite };
			}
		}

		// Unknown vehicle types and null fall back to a passenger car.
		VehType ResolveType(string name, int variantSeed)
		{
			if (!string.IsNullOrEmpty(name) && typeLookup.TryGetValue(name, out var t))
				return t;

			return new VehType { Kind = KindCar, Slots = 1, SpeedPct = 100, Sprite = (Hash(variantSeed, 3) & 0x7fffffff) % spriteCountCars };
		}

		int AllocVehicle()
		{
			if (freeCount == 0)
				GrowVehicles(vCapacity * 2);

			return freeList[--freeCount];
		}

		void ReleaseVehicle(int v)
		{
			vLink[v] = -1;
			vRoute[v] = null;
			vTrip[v] = null;
			freeList[freeCount++] = v;
			ActiveVehicles--;
		}

		void Push(int l, int v)
		{
			vNext[v] = NoVehicle;
			if (qTail[l] < 0)
				qHead[l] = v;
			else
				vNext[qTail[l]] = v;

			qTail[l] = v;
			qOcc[l] += vSlots[v];
			qCount[l]++;
			vLink[v] = l;
		}

		int PopHead(int l)
		{
			var v = qHead[l];
			qHead[l] = vNext[v];
			if (qHead[l] < 0)
				qTail[l] = NoVehicle;

			qOcc[l] -= vSlots[v];
			qCount[l]--;
			blockedSince[l] = -1;
			return v;
		}

		void RemoveFromQueue(int v)
		{
			var l = vLink[v];
			if (qHead[l] == v)
			{
				PopHead(l);
				return;
			}

			var p = qHead[l];
			while (p >= 0 && vNext[p] != v)
				p = vNext[p];

			if (p < 0)
				return;

			vNext[p] = vNext[v];
			if (qTail[l] == v)
				qTail[l] = p;

			qOcc[l] -= vSlots[v];
			qCount[l]--;
		}

		// Starts a vehicle on its origin link. Returns false if the link has no room.
		bool TrySpawn(TripRec t, byte[] route)
		{
			if (ActiveVehicles >= Info.MaxVehicles)
				return false;

			var heading = route.Length > 0 ? route[0] : 0;
			var link = t.Origin * 4 + heading;
			if (qOcc[link] > 0 && qOcc[link] + t.Slots > Storage(t.Origin))
				return false;

			var v = AllocVehicle();
			ActiveVehicles++;
			vRoute[v] = route;
			vStep[v] = 0;
			vSlots[v] = t.Slots;
			vTrip[v] = t;
			vLane[v] = (byte)(laneCounter++ % cellLanes[t.Origin]);
			t.Vehicle = v;
			if (t.StartTick == 0)
				t.StartTick = tick;

			EnterLink(v, link, nowU);
			return true;
		}

		// Puts a vehicle at the tail of a link; the clock starts at `enterU` (carries over sub-tick remainders).
		void EnterLink(int v, int link, int enterU)
		{
			var cell = link >> 2;
			var t = vTrip[v];
			vLane[v] = (byte)(laneCounter++ % cellLanes[cell]);
			vEnterU[v] = enterU;
			vReadyU[v] = enterU + LinkTime(cell, FreeOccupancy(t.Kind, cell) ? 0 : qOcc[link], t.SpeedPct * WeatherSpeedPercent() / 100);
			Push(link, v);
		}

		// Removes the vehicle of a trip and reports a failure.
		void FailVehicle(int v, TripFailure reason)
		{
			var t = vTrip[v];
			RemoveFromQueue(v);
			ReleaseVehicle(v);
			if (t != null)
				Fail(t, reason);
		}
	}
}
