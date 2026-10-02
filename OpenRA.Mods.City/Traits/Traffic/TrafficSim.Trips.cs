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

		int PendingCount => Math.Max(0, trips.Count - ActiveVehicles);

		// Estimate cache (direct mapped); only valid for the current cost snapshot and network version.
		struct EstimateEntry
		{
			public int From, To, GraphVersion, CostVersion, Mode, Result, SearchNodes, AttemptTick;
		}

		readonly EstimateEntry[] estimates = new EstimateEntry[2048];
		int estimateTick = -1;
		int estimatesThisTick;
		int walkerGraphVersion = -1;

		int ITrafficService.RequestTrip(in TripRequest request, ITripListener listener)
		{
			if (net == null)
				return 0;

			if (graphVersion != net.NetworkVersion)
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
			}

			var t = NewTrip(origin, destination, request.OwnerId, request.Purpose, request.DepartTick, request.VehicleType);
			t.Listener = listener;
			t.OriginProperty = request.OriginProperty;
			t.DestinationProperty = request.DestinationProperty;
			t.Walk = walkOnly;
			t.CanDrive = (request.AllowedModes & (TravelModes.Car | TravelModes.Taxi)) != 0;
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
			tick = world.WorldTick;
			nowU = tick * U;
			graphVersion = net.NetworkVersion;
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
			trips.Remove(tripId);
			if (t.WalkRoute != null)
			{
				t.WalkRoute = null;
				if (t.VisibleWalker)
					pedestrians--;

				t.VisibleWalker = false;
			}

			if (t.Vehicle != NoVehicle)
			{
				var v = t.Vehicle;
				RemoveFromQueue(v);
				ReleaseVehicle(v);
				t.Vehicle = NoVehicle;
				trips.Remove(tripId);
			}
		}

		bool ReserveEstimate()
		{
			if (estimateTick != world.WorldTick)
			{
				estimateTick = world.WorldTick;
				estimatesThisTick = 0;
			}

			return estimatesThisTick++ < Math.Max(1, Info.EstimateBudget);
		}

		int ITrafficService.EstimateTravelTicks(CPos fromRoad, CPos toRoad, TravelMode mode)
		{
			if (net == null || !InMap(fromRoad) || !InMap(toRoad))
				return -1;

			if (graphVersion != net.NetworkVersion)
				EnsureGraphNow();

			var from = Cell(fromRoad);
			var to = Cell(toRoad);
			if (roadFlag[from] == 0 || roadFlag[to] == 0)
				return -1;

			if (mode == TravelMode.Transit || mode == TravelMode.Bike)
				return -1;

			var slot = (Hash(Hash(from, to), (int)mode) & 0x7fffffff) % estimates.Length;
			ref var e = ref estimates[slot];
			var cached = e.From == from + 1 && e.To == to + 1 && e.GraphVersion == graphVersion && e.CostVersion == costVersion && e.Mode == (int)mode;
			if (cached && (e.Result != -2 || e.AttemptTick == world.WorldTick))
				return e.Result;

			var nodes = cached ? NextSearchNodes(e.SearchNodes) : Math.Max(1, Info.MaxRouteNodes);

			if (from == to)
				return 0;

			if (!ReserveEstimate())
				return -2;

			int result;
			if (mode == TravelMode.Walk)
			{
				var path = walkingRouter.FindRoute(walkMask, sidewalk, from, to, nodes, out var deferred);
				result = deferred ? -2 : path == null ? -1 : checked(path.Length * WalkingTicks);
			}
			else
			{
				var speed = mode == TravelMode.Truck || mode == TravelMode.Bus ? 80 : mode == TravelMode.EmergencyVehicle ? 150 : 100;
				var kind = mode == TravelMode.Bus ? KindBus : mode == TravelMode.Truck ? KindTruck : mode == TravelMode.EmergencyVehicle ? KindEmergency : KindCar;
				var found = FindRouteFor(kind, from, -1, to, speed, mode == TravelMode.EmergencyVehicle, 0, out var path, out _, nodes);
				result = routeSearchDeferred ? -2 : found ? EstimateRouteTicks(from, path, speed, kind) : -1;
			}

			e = new EstimateEntry
			{
				From = from + 1,
				To = to + 1,
				GraphVersion = graphVersion,
				CostVersion = costVersion,
				Mode = (int)mode,
				Result = result,
				SearchNodes = nodes,
				AttemptTick = world.WorldTick,
			};

			return result;
		}

		int EstimateRouteTicks(int origin, byte[] route, int speed, byte kind)
		{
			if (route.Length == 0)
				return 0;

			var speedPct = Math.Max(1, speed * WeatherSpeedPercent() / 100);
			long total = TravelTimeCalibration.ScaleSpeed(costSnap[origin * 4 + route[0]], speedPct) / 2;
			var cell = origin;
			var heading = route[0];
			for (var i = 0; i < route.Length; i++)
			{
				if (kind != KindEmergency)
					total += JunctionExpectedDelay(cell, heading, route[i]);

				cell += dIdx[route[i]];
				heading = route[i];
				var baseline = FreeOccupancy(kind, cell) ? ffU[cell] : costSnap[cell * 4 + heading];
				var travel = TravelTimeCalibration.ScaleSpeed(baseline, speedPct);
				total += i == route.Length - 1 ? travel / 2 : travel;
			}

			return (int)Math.Min(int.MaxValue, (total + U - 1) / U);
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
					if (budget <= 0)
					{
						ready.Enqueue(t);
						continue;
					}

					budget--;
					if (!StartWalk(t))
						ready.Enqueue(t);

					continue;
				}

				if (t.RouteVersion != graphVersion)
					t.Route = null;

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
						if (routeSearchDeferred)
						{
							t.SearchNodes = NextSearchNodes(t.SearchNodes);
							ready.Enqueue(t);
						}
						else
							Fail(t, TripFailure.NoRoute);

						continue;
					}

					t.Route = route;
					t.RouteVersion = graphVersion;
					t.Length = route.Length;
				}

				if (TrySpawn(t, route))
					t.Route = null;
				else if (tick - t.DepartTick > Info.StuckTicks)
					Fail(t, TripFailure.Stuck);
				else
					ready.Enqueue(t);
			}
		}

		bool StartWalk(TripRec t)
		{
			var route = walkingRouter.FindRoute(walkMask, sidewalk, t.Origin, t.Destination, Math.Max(Info.MaxRouteNodes, t.SearchNodes), out var deferred);
			if (deferred)
			{
				t.SearchNodes = NextSearchNodes(t.SearchNodes);
				return false;
			}

			if (t.CanDrive && (route == null || route.Length > WalkLimit(t.Destination)))
			{
				t.Walk = false;
				t.Parks = t.Kind == KindCar && IsCitizenPurpose(t.Purpose);
				return false;
			}

			if (route == null)
			{
				Fail(t, TripFailure.NoRoute);
				return true;
			}

			if (route.Length == 0)
			{
				Arrive(t, tick);
				return true;
			}

			var ticks = checked(route.Length * WalkingTicks);
			t.StartTick = tick;
			t.WalkStart = tick;
			t.WalkEnd = tick + ticks;
			t.WalkRoute = route;
			t.VisibleWalker = pedestrians < Info.PedestrianCap;
			if (t.VisibleWalker)
				pedestrians++;

			walkers.Enqueue(t, ((long)t.WalkEnd << 32) | (uint)t.Id);
			return true;
		}

		void TickWalkers()
		{
			if (walkerGraphVersion != graphVersion)
			{
				walkerGraphVersion = graphVersion;
				foreach (var (walk, _) in walkers.UnorderedItems)
				{
					if (walk.Cancelled || walk.WalkRoute == null)
						continue;

					var completed = Math.Clamp((int)(((long)(tick - walk.WalkStart) * 2 + WalkingTicks) / (2L * WalkingTicks)), 0, walk.WalkRoute.Length);
					var cell = walk.Origin;
					for (var i = 0; i < completed; i++)
					{
						var h = walk.WalkRoute[i];
						cell += h == 0 ? -width : h == 1 ? 1 : h == 2 ? width : -1;
					}

					var closed = !sidewalk[cell];
					for (var i = completed; i < walk.WalkRoute.Length && !closed; i++)
					{
						var heading = walk.WalkRoute[i];
						closed = !sidewalk[cell] || (walkMask[cell] & (1 << heading)) == 0;
						cell += heading == 0 ? -width : heading == 1 ? 1 : heading == 2 ? width : -1;
					}

					if (closed)
					{
						Fail(walk, TripFailure.RoadClosed);
						walk.Cancelled = true;
						walk.WalkRoute = null;
						if (walk.VisibleWalker)
							pedestrians--;

						walk.VisibleWalker = false;
					}
				}
			}

			while (walkers.TryPeek(out var t, out var key) && (t.Cancelled || key >> 32 <= tick))
			{
				walkers.Dequeue();
				if (t.WalkRoute != null)
				{
					t.WalkRoute = null;
					if (t.VisibleWalker)
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
