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
	// Trip API (ITrafficService), the pending queues, planning budget and listener notifications.
	public sealed partial class TrafficSim
	{
		int nextTripId;
		readonly int[] failBy = new int[8];
		readonly PriorityQueue<TripRec, long> future = new();
		readonly Queue<TripRec> ready = new();
		readonly PriorityQueue<TripRec, long> walkers = new();
		int externalCitizenTrips;
		int lastExternalCitizenTick = -100000;

		int PendingCount => future.Count + ready.Count + walkers.Count;

		// Estimate cache (direct mapped); only valid for the current cost snapshot and network version.
		struct EstimateEntry
		{
			public int From, To, Version, Mode, Result;
		}

		readonly EstimateEntry[] estimates = new EstimateEntry[512];

		int ITrafficService.RequestTrip(in TripRequest request, ITripListener listener)
		{
			if (net == null)
				return 0;

			if (graphVersion < 0)
				EnsureGraphNow();

			if (!InMap(request.OriginRoad) || !InMap(request.DestinationRoad))
				return 0;

			var origin = Cell(request.OriginRoad);
			var destination = Cell(request.DestinationRoad);
			if (roadFlag[origin] == 0 || roadFlag[destination] == 0)
				return 0;

			if (PendingCount > Info.MaxVehicles * 2)
				return 0;

			var walkOnly = request.AllowedModes == TravelModes.Walk;
			if (!walkOnly && (request.AllowedModes & TravelModes.Walk) != 0 && WalkLimit(destination) > 0 && string.IsNullOrEmpty(request.VehicleType)
				&& Math.Abs(request.OriginRoad.X - request.DestinationRoad.X) + Math.Abs(request.OriginRoad.Y - request.DestinationRoad.Y) <= WalkLimit(destination))
				walkOnly = true;

			if (!walkOnly && request.AllowedModes != TravelModes.None && (request.AllowedModes & (TravelModes.Car | TravelModes.Taxi)) == 0
				&& string.IsNullOrEmpty(request.VehicleType))
				return 0;

			var citizenTrip = IsCitizenPurpose(request.Purpose);
			if (citizenTrip)
			{
				externalCitizenTrips++;
				lastExternalCitizenTick = tick;
			}

			var t = NewTrip(origin, destination, request.OwnerId, request.Purpose, request.DepartTick, request.VehicleType);
			t.Listener = listener;
			t.OriginProperty = request.OriginProperty;
			t.DestinationProperty = request.DestinationProperty;
			t.Walk = walkOnly;
			t.Parks = !walkOnly && t.Kind == KindCar && citizenTrip;
			Enqueue(t);
			return t.Id;
		}

		static bool IsCitizenPurpose(TripPurpose p)
		{
			return p == TripPurpose.Work || p == TripPurpose.School || p == TripPurpose.Shopping || p == TripPurpose.Leisure
				|| p == TripPurpose.Health || p == TripPurpose.GoingHome || p == TripPurpose.Tourism || p == TripPurpose.Commute;
		}

		void EnsureGraphNow()
		{
			graphVersion = net.NetworkVersion;
			lastGraphTick = tick;
			RebuildGraph();
		}

		TripRec NewTrip(int origin, int destination, int owner, TripPurpose purpose, int depart, string vehicleType)
		{
			var id = ++nextTripId;
			var type = ResolveType(vehicleType, Hash(owner, id));
			var t = new TripRec
			{
				Id = id,
				Owner = owner,
				Purpose = purpose,
				DepartTick = Math.Max(depart, tick),
				Origin = origin,
				Destination = destination,
				Kind = type.Kind,
				Slots = type.Kind == KindTram && tramCells > 0 ? (byte)0 : type.Slots,
				SpeedPct = type.SpeedPct,
				Sprite = type.Sprite,
			};

			trips[id] = t;
			totalTrips++;
			return t;
		}

		void Enqueue(TripRec t)
		{
			if (t.DepartTick <= tick)
				ready.Enqueue(t);
			else
				future.Enqueue(t, ((long)t.DepartTick << 32) | (uint)t.Id);
		}

		void ITrafficService.CancelTrip(int tripId)
		{
			if (!trips.TryGetValue(tripId, out var t))
				return;

			// Owner-initiated: no callback. Pending trips are dropped lazily when they come up.
			t.Cancelled = true;
			t.Listener = null;
			if (t.Vehicle != NoVehicle)
			{
				var v = t.Vehicle;
				RemoveFromQueue(v);
				ReleaseVehicle(v);
				t.Vehicle = NoVehicle;
				trips.Remove(tripId);
			}
		}

		int ITrafficService.EstimateTravelTicks(CPos fromRoad, CPos toRoad, TravelMode mode)
		{
			if (net == null || !InMap(fromRoad) || !InMap(toRoad))
				return -1;

			if (graphVersion < 0)
				EnsureGraphNow();

			var from = Cell(fromRoad);
			var to = Cell(toRoad);
			if (roadFlag[from] == 0 || roadFlag[to] == 0)
				return -1;

			var dist = Math.Abs(fromRoad.X - toRoad.X) + Math.Abs(fromRoad.Y - toRoad.Y);
			if (mode == TravelMode.Walk)
				return dist * Info.WalkTicksPerCell;

			if (mode == TravelMode.Transit || mode == TravelMode.Bike)
				return -1;

			var slot = (Hash(from, to) & 0x7fffffff) % estimates.Length;
			ref var e = ref estimates[slot];
			var version = graphVersion * 31 + costVersion;
			if (e.From == from + 1 && e.To == to + 1 && e.Version == version && e.Mode == (int)mode)
				return e.Result;

			var speed = mode == TravelMode.Truck || mode == TravelMode.Bus ? 80 : mode == TravelMode.EmergencyVehicle ? 150 : 100;
			var kind = mode == TravelMode.Bus ? KindBus : KindCar;
			var found = FindRouteFor(kind, from, -1, to, speed, mode == TravelMode.EmergencyVehicle, 0, out _, out var cost);
			var result = found ? (cost + U - 1) / U : -1;
			e = new EstimateEntry { From = from + 1, To = to + 1, Version = version, Mode = (int)mode, Result = result };
			return result;
		}

		// Plans pending trips (bounded number of A* runs per tick) and puts vehicles on their origin links.
		void PlanAndSpawn()
		{
			while (future.TryPeek(out var f, out _) && f.DepartTick <= tick)
				ready.Enqueue(future.Dequeue());

			var budget = Info.PlanBudget;
			var count = ready.Count;
			for (var i = 0; i < count; i++)
			{
				var t = ready.Dequeue();
				if (t.Cancelled)
				{
					trips.Remove(t.Id);
					continue;
				}

				if (t.Walk)
				{
					StartWalk(t);
					continue;
				}

				var route = t.Route;
				if (route == null)
				{
					if (budget <= 0)
					{
						ready.Enqueue(t);
						continue;
					}

					budget--;
					if (t.Origin == t.Destination)
					{
						Arrive(t, tick);
						continue;
					}

					var seed = Hash(t.Id, 17);
					if (!FindRouteForTrip(t, t.Origin, -1, t.Destination, t.SpeedPct, t.Kind == KindEmergency, (seed & 3) == 0 ? seed | 1 : 0, out route, out _))
					{
						Fail(t, TripFailure.NoRoute);
						continue;
					}

					t.Route = route;
					t.Length = route.Length;
				}

				if (TrySpawn(t, route))
					t.Route = null;
				else if (tick - t.DepartTick > 900)
					Fail(t, TripFailure.Stuck);
				else
					ready.Enqueue(t);
			}
		}

		void StartWalk(TripRec t)
		{
			var a = ToCPos(t.Origin);
			var b = ToCPos(t.Destination);
			var ticks = (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) * Info.WalkTicksPerCell + 1;
			t.StartTick = tick;
			t.WalkStart = tick;
			t.WalkEnd = tick + ticks;

			// Cosmetic pedestrian: remember a route along the roads for the renderer (sampled, capped).
			if (pedestrians < Info.PedestrianCap && FindRouteFor(KindCar, t.Origin, -1, t.Destination, 100, false, 0, out var route, out _) && route.Length > 0)
			{
				t.WalkRoute = route;
				pedestrians++;
			}

			walkers.Enqueue(t, ((long)(tick + ticks) << 32) | (uint)t.Id);
		}

		void TickWalkers()
		{
			while (walkers.TryPeek(out var t, out var key) && key >> 32 <= tick)
			{
				walkers.Dequeue();
				if (t.WalkRoute != null)
				{
					t.WalkRoute = null;
					pedestrians--;
				}

				if (t.Cancelled)
				{
					trips.Remove(t.Id);
					continue;
				}

				Notify(t, true, 0, tick);
			}
		}

		void Fail(TripRec t, TripFailure reason)
		{
			totalFailed++;
			failBy[(int)reason]++;
			Notify(t, false, reason, tick);
		}

		void Notify(TripRec t, bool arrived, TripFailure failure, int arriveTick)
		{
			if (arrived)
				totalArrived++;

			trips.Remove(t.Id);
			if (t.Listener != null)
				notifications.Add(new Notification { Trip = t, Arrived = arrived, Failure = failure, ArriveTick = arriveTick });
		}

		void DeliverNotifications()
		{
			// Index loop: listeners may request new trips while being notified (those never add notifications themselves this tick).
			for (var i = 0; i < notifications.Count; i++)
			{
				var n = notifications[i];
				if (n.Arrived)
				{
					n.Trip.Listener.OnTripArrived(new TripResult
					{
						TripId = n.Trip.Id,
						OwnerId = n.Trip.Owner,
						Mode = ModeOf(n.Trip),
						DepartTick = n.Trip.DepartTick,
						ArriveTick = n.ArriveTick,
						CostCents = 0,
					});
				}
				else
					n.Trip.Listener.OnTripFailed(n.Trip.Id, n.Trip.Owner, n.Failure);
			}

			notifications.Clear();
		}

		static TravelMode ModeOf(TripRec t)
		{
			if (t.Walk)
				return TravelMode.Walk;

			switch (t.Kind)
			{
				case KindTruck: return TravelMode.Truck;
				case KindBus:
				case KindTram:
					return TravelMode.Bus;
				case KindEmergency: return TravelMode.EmergencyVehicle;
				default: return t.Purpose == TripPurpose.Service ? TravelMode.ServiceVehicle : TravelMode.Car;
			}
		}
	}
}
