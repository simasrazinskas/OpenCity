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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// Arrival (timing wheel), failure handling, sampled visible trucks, freight load and reporting.
	public partial class Logistics
	{
		readonly List<CPos> pathScratch = [];
		int[] freightLoad;
		readonly List<int> freightActive = [];
		bool blockedChirpedToday;

		void ITick.Tick(Actor self)
		{
			var now = world.WorldTick;
			DrainWheel(now);
			if (now % 25 == 0)
				Pulse(now);
		}

		void Pulse(int now)
		{
			Resolve();
			RefreshOutsideNodes();

			// Outside trade throughput: a token bucket refilled per pulse, capped at one clock day of trade.
			for (var k = 0; k < outsideNodes.Count + terminalNodes.Count; k++)
			{
				var o = k < outsideNodes.Count ? outsideNodes[k] : terminalNodes[k - outsideNodes.Count];
				var cap = (long)o.TradePerDay * 1000;
				var refill = cap * 25 / Math.Max(1, TicksPerDay);
				o.TradeTokensMilli = Math.Min(cap, o.TradeTokensMilli + Math.Max(1, refill));
			}

			// Freight congestion decays.
			for (var i = freightActive.Count - 1; i >= 0; i--)
			{
				var idx = freightActive[i];
				var v = freightLoad[idx];
				v -= v / 8 + 1;
				if (v <= 0)
				{
					freightLoad[idx] = 0;
					freightActive.RemoveAt(i);
				}
				else
					freightLoad[idx] = v;
			}

			// A missing traffic callback must cancel the real trip, never silently abandon its vehicle.
			for (var i = sampled.Count - 1; i >= 0; i--)
				if (sampled[i].ExpireTick < now)
				{
					var trip = sampled[i];
					traffic?.CancelTrip(trip.TripId);
					if (shipmentById.TryGetValue(trip.ShipmentId, out var ship))
					{
						ships[ship].TripId = 0;
						ships[ship].TripFailed = true;
					}

					sampled.RemoveAt(i);
				}

			var day = clock != null ? clock.DayIndex : now / 2400;
			if (lastDay < 0)
				lastDay = day;
			else if (day != lastDay)
			{
				lastDay = day;
				Array.Copy(MonthlyDelivered, LastMonthDelivered, MonthlyDelivered.Length);
				Array.Clear(MonthlyDelivered);
				RotateStats();
				AssignTypes();
				BalanceWarehouses();
				blockedChirpedToday = false;
			}
		}

		void DrainWheel(int now)
		{
			var bucket = now & (WheelSize - 1);
			var idx = wheelHead[bucket];
			if (idx < 0)
				return;

			wheelHead[bucket] = -1;
			wheelTail[bucket] = -1;
			while (idx >= 0)
			{
				var next = ships[idx].Next;
				Arrive(idx, now);
				idx = next;
			}
		}

		LogiNode NodeById(int id) { return nodes[id - 1]; }

		void Arrive(int index, int now)
		{
			ref var s = ref ships[index];
			if (!s.Active)
				return;

			if (s.ArriveTick > now)
			{
				Enqueue(index, s.ArriveTick);
				return;
			}

			var to = NodeById(s.ToNode);
			var from = NodeById(s.FromNode);
			var heavy = WeightClass(s.Resource) != 0;

			if (s.TripId > 0)
			{
				// A sampled truck's real arrival is authoritative. Congestion cannot make cargo
				// appear in stock while its truck is still waiting at a junction.
				s.ArriveTick = now + 25;
				Enqueue(index, s.ArriveTick);
				return;
			}

			if (s.Returning)
			{
				if (s.ReturnPending)
				{
					BeginReturn(index, from, to, now, s.ReturnedCargo);
					return;
				}

				if (s.ReturnedCargo)
					ReturnCargo(index, from, to);

				Finish(index);
				return;
			}

			if (s.TripFailed || !from.Alive)
			{
				RedirectOrReturn(index, from, now);
				return;
			}

			if (!to.Alive)
			{
				RedirectOrReturn(index, from, now);
				return;
			}

			// Roads changed while the load was on its way: check the route is still there.
			if (heavy && roads != null && s.PathVersion != roads.NetworkVersion)
			{
				var length = RouteLength(from.Road, to.Road);
				if (length == RouteBusy)
				{
					s.ArriveTick = now + 1;
					Enqueue(index, s.ArriveTick);
					return;
				}

				if (length == Unreachable)
				{
					RedirectOrReturn(index, from, now);
					return;
				}

				var travel = TravelTicks(from.Road, to.Road);
				if (travel == RouteBusy)
				{
					s.ArriveTick = now + 1;
					Enqueue(index, s.ArriveTick);
					return;
				}

				if (travel < 0)
				{
					RedirectOrReturn(index, from, now);
					return;
				}

				s.PathVersion = roads.NetworkVersion;
				var revisedArrival = s.DepartTick + travel;
				if (revisedArrival > now)
				{
					s.ArriveTick = revisedArrival;
					Enqueue(index, revisedArrival);
					return;
				}
			}

			Deliver(index, from, to, now);
		}

		static ShipmentEvent MakeEvent(in Shipment s, LogiNode from, LogiNode to)
		{
			return new ShipmentEvent
			{
				ShipmentId = s.Id,
				FromProperty = from.Kind == NodeKind.Outside ? 0 : from.PropertyId,
				ToProperty = to.Kind == NodeKind.Outside ? 0 : to.PropertyId,
				Resource = s.Resource,
				Units = s.Units,
				UnitPriceCents = s.UnitPriceCents,
				FreightCents = s.FreightCents,
				ToStorage = s.ToStorage,
				FromStorage = s.FromStorage,
			};
		}

		void Deliver(int index, LogiNode from, LogiNode to, int now)
		{
			var s = ships[index];
			var r = Math.Min((int)s.Resource, 63);
			DeliveredUnits[r] += s.Units;
			MonthlyDelivered[r] += s.Units;
			totalDelivered += s.Units;
			AddStat(IndustryStat.Delivered, r, s.Units);
			if (s.ToStorage && to.Stock != null)
			{
				to.Inbound[r] = Math.Max(0, to.Inbound[r] - s.Units);
				to.Stock[r] += s.Units;
			}

			BeginReturn(index, from, to, now, false);
			unchecked
			{
				StateHash = StateHash * 31 + s.Id * 3 + 1;
			}

			Delivered?.Invoke(MakeEvent(s, from, to));
		}

		/// <summary>A company closed before receipt. Keep cargo on the returning truck and settle it on return.</summary>
		public void RejectDelivery(int shipmentId)
		{
			if (!shipmentById.TryGetValue(shipmentId, out var index))
				return;

			ref var s = ref ships[index];
			if (!s.Returning || s.ReturnedCargo)
				return;

			s.ReturnedCargo = true;
			var resource = (int)s.Resource;
			DeliveredUnits[resource] -= s.Units;
			MonthlyDelivered[resource] -= s.Units;
			totalDelivered -= s.Units;
			AddStat(IndustryStat.Delivered, resource, -s.Units);
			var to = NodeById(s.ToNode);
			if (s.ToStorage && to.Stock != null)
				to.Stock[resource] = Math.Max(0, to.Stock[resource] - s.Units);
		}

		void RedirectOrReturn(int index, LogiNode from, int now)
		{
			ref var s = ref ships[index];
			if (!s.Redirected && s.UnitPriceCents == 0)
			{
				// Buyer closed or unreachable: the nearest connected warehouse takes the load, else it goes back.
				LogiNode best = null;
				var bestLength = int.MaxValue;
				foreach (var n in nodes)
				{
					if (n.Kind != NodeKind.Storage || !n.Alive || n.Id == s.ToNode || FreeSpace(n, s.Resource) < s.Units)
						continue;

					var length = RouteLength(from.Road, n.Road);
					if (length >= 0 && length < bestLength)
					{
						best = n;
						bestLength = length;
					}
				}

				if (best != null)
				{
					ReleaseInbound(ref s);
					best.Inbound[Math.Min((int)s.Resource, 63)] += s.Units;
					s.ToStorage = true;
					s.Redirected = true;
					s.ToNode = best.Id;
					s.PathVersion = roads?.NetworkVersion ?? 0;
					s.ArriveTick = now + 1 + bestLength * Info.TruckTicksPerCell;
					Enqueue(index, s.ArriveTick);
					return;
				}
			}

			var to = NodeById(s.ToNode);
			ReleaseInbound(ref s);
			BeginReturn(index, from, to, now, true);
		}

		void BeginReturn(int index, LogiNode from, LogiNode to, int now, bool cargo)
		{
			ref var s = ref ships[index];
			s.Returning = true;
			s.ReturnedCargo = cargo;
			s.TripFailed = false;
			var travel = WeightClass(s.Resource) == 0 ? 0 : TravelTicks(to.Road, from.Road);
			if (travel == RouteBusy)
			{
				s.ReturnPending = true;
				s.ArriveTick = now + 1;
				Enqueue(index, s.ArriveTick);
				return;
			}

			s.ReturnPending = false;
			if (travel < 0)
				travel = Math.Max(1, s.ReturnTick - s.ArriveTick - Info.TurnaroundTicks);

			var depart = now + Info.TurnaroundTicks;
			s.ArriveTick = depart + travel;
			s.ReturnTick = s.ArriveTick;
			if (s.Sampled && from.Alive && to.Alive)
				s.TripId = RequestTruck(s.Id, to, from, depart, s.ReturnTick);

			Enqueue(index, s.ArriveTick);
		}

		void ReturnCargo(int index, LogiNode from, LogiNode to)
		{
			var s = ships[index];
			var r = Math.Min((int)s.Resource, 63);
			Stranded[r] += s.Units;
			totalReturned += s.Units;
			if (s.FromStorage && from.Alive && from.Stock != null && HasType(from, r))
				from.Stock[r] += s.Units;

			NoteBlocked();
			unchecked
			{
				StateHash = StateHash * 31 + s.Id * 3 + 2;
			}

			Returned?.Invoke(MakeEvent(s, from, to));
		}

		void ReleaseInbound(ref Shipment s)
		{
			if (!s.ToStorage)
				return;

			var target = NodeById(s.ToNode);
			if (target.Inbound != null)
			{
				var r = Math.Min((int)s.Resource, 63);
				target.Inbound[r] = Math.Max(0, target.Inbound[r] - s.Units);
			}

			s.ToStorage = false;
		}

		// Once per clock day at most: freight went back because roads are cut.
		void NoteBlocked()
		{
			if (blockedChirpedToday)
				return;

			blockedChirpedToday = true;
			var stats = world.WorldActor.TraitsImplementing<ICityStatistics>().FirstOrDefault();
			stats?.Chirp("chirp-industry-freight-blocked");
		}

		void Finish(int index)
		{
			ref var s = ref ships[index];
			s.Active = false;
			shipmentById.Remove(s.Id);
			if (s.Slot >= 0)
				fleetFreeAt[s.Slot] = world.WorldTick;

			freeShips.Push(index);
			ShipmentsInTransit--;
		}

		void TrySampleTruck(int index, LogiNode from, LogiNode to, int depart, int returnAt)
		{
			if (traffic == null || IndustryHash.Mix(ships[index].Id, 11) % Math.Max(1, Info.SampleDiv) != 0 || sampled.Count >= MaxVisible)
				return;

			var trip = RequestTruck(ships[index].Id, from, to, depart, returnAt);
			if (trip > 0)
			{
				ships[index].Sampled = true;
				ships[index].TripId = trip;
			}
		}

		int RequestTruck(int shipmentId, LogiNode from, LogiNode to, int depart, int expire)
		{
			if (traffic == null || sampled.Count >= MaxVisible || from.Road == CPos.Zero || to.Road == CPos.Zero)
				return 0;

			var request = new TripRequest
			{
				Purpose = TripPurpose.Freight,
				AllowedModes = TravelModes.Car,
				OwnerId = shipmentId,
				OriginRoad = from.Road,
				DestinationRoad = to.Road,
				OriginProperty = from.Kind == NodeKind.Outside ? 0 : from.PropertyId,
				DestinationProperty = to.Kind == NodeKind.Outside ? 0 : to.PropertyId,
				DepartTick = depart,
				VehicleType = Info.TruckVehicleType,
			};

			var trip = traffic.RequestTrip(request, this);
			if (trip <= 0)
				return 0;

			sampled.Add(new SampledTrip { TripId = trip, ExpireTick = Math.Max(expire, depart) + Math.Max(1, Info.MaxQueueTicks), ShipmentId = shipmentId });
			return trip;
		}

		void ITripListener.OnTripArrived(in TripResult result)
		{
			if (shipmentById.TryGetValue(result.OwnerId, out var index) && ships[index].TripId == result.TripId)
				ships[index].TripId = 0;

			ForgetTrip(result.TripId);
		}

		void ITripListener.OnTripFailed(int tripId, int ownerId, TripFailure reason)
		{
			if (shipmentById.TryGetValue(ownerId, out var index) && ships[index].TripId == tripId)
			{
				ships[index].TripId = 0;
				ships[index].TripFailed = true;
			}

			ForgetTrip(tripId);
		}

		void ForgetTrip(int tripId)
		{
			for (var i = 0; i < sampled.Count; i++)
				if (sampled[i].TripId == tripId)
				{
					sampled.RemoveAt(i);
					return;
				}
		}

		void AddFreight(CPos fromRoad, CPos toRoad, int loads)
		{
			CollectPath(fromRoad, toRoad, pathScratch);
			if (pathScratch.Count == 0)
				return;

			freightLoad ??= new int[mapWidth * mapHeight];
			var add = loads * Info.FreightPerLoad;
			foreach (var c in pathScratch)
			{
				var idx = IndexOf(c);
				if (freightLoad[idx] == 0)
					freightActive.Add(idx);

				freightLoad[idx] = Math.Min(200, freightLoad[idx] + add);
			}

			freightFlow?.AddFlow(pathScratch, loads * 25 / 10);
		}

		/// <summary>0..100 freight congestion of a road cell (decays over a few pulses).</summary>
		public int GetFreightLoad(CPos cell)
		{
			return freightLoad == null || !InMap(cell) ? 0 : Math.Min(100, freightLoad[IndexOf(cell)]);
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			return $"logistics ships={ShipmentsInTransit} nodes={nodes.Count} trucks={sampled.Count}/{MaxVisible} " +
				$"dispatched={totalDispatched} delivered={totalDelivered} " +
				$"returned={totalReturned} local={totalDispatched - ImportedUnits.Sum() - ExportedUnits.Sum()} imports={ImportedUnits.Sum()} exports={ExportedUnits.Sum()} busy={rejectedBusy} bfs={BfsRuns} freightCells={freightActive.Count} {StorageReport()} hash={StateHash}";
		}
	}
}
