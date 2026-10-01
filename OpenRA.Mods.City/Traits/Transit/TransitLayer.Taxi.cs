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
	// Taxis: no lines. Stands are taxi stops; a taxi from a taxidepot idles at a stand, drives to the pickup stand of a booking,
	// waits for the passenger to walk up, drives to the destination road and returns to the nearest stand.
	public sealed partial class TransitLayer
	{
		const int MaxTaxiCandidates = 25;

		TransitStop NearestStand(CPos cell, out int dist)
		{
			TransitStop best = null;
			dist = int.MaxValue;
			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				if (s.Mode != TransitMode.Taxi)
					continue;

				var d = Math.Abs(s.Cell.X - cell.X) + Math.Abs(s.Cell.Y - cell.Y);
				if (d < dist)
				{
					dist = d;
					best = s;
				}
			}

			return best;
		}

		int StandCount()
		{
			var c = 0;
			for (var i = 0; i < stops.Count; i++)
				if (stops[i].Mode == TransitMode.Taxi)
					c++;

			return c;
		}

		TransitVehicle FindIdleTaxi(CPos near)
		{
			TransitVehicle best = null;
			var bestDist = int.MaxValue;
			var seen = 0;
			for (var i = 0; i < vehicles.Count && seen < MaxTaxiCandidates; i++)
			{
				var v = vehicles[i];
				if (v.Mode != TransitMode.Taxi || v.State != TransitVehicleState.Idle)
					continue;

				seen++;
				var d = Math.Abs(v.Cell.X - near.X) + Math.Abs(v.Cell.Y - near.Y);
				if (d < bestDist)
				{
					bestDist = d;
					best = v;
				}
			}

			return best;
		}

		int TaxiFare(CPos standCell, CPos dest)
		{
			var cells = Math.Abs(standCell.X - dest.X) + Math.Abs(standCell.Y - dest.Y);
			return Info.TaxiFareBaseCents + Info.TaxiFarePerCellCents * cells;
		}

		/// <summary>Cost of a taxi ride (door to stand walk, taxi wait and drive, fare). Available only with an idle taxi near a stand.</summary>
		public TransitQuote QuoteTaxi(CPos fromRoad, CPos toRoad, AgeGroup age)
		{
			var stand = NearestStand(fromRoad, out var walkCells);
			if (stand == null || walkCells > Info.WalkRadius)
				return default;

			var taxi = FindIdleTaxi(stand.Cell);
			if (taxi == null)
				return default;

			var eta = (Math.Abs(taxi.Cell.X - stand.Cell.X) + Math.Abs(taxi.Cell.Y - stand.Cell.Y)) * Info.VirtualTicksPerCell;
			var drive = (Math.Abs(stand.Cell.X - toRoad.X) + Math.Abs(stand.Cell.Y - toRoad.Y)) * Info.VirtualTicksPerCell * 12 / 10;
			return new TransitQuote
			{
				Available = true,
				Ticks = walkCells * Info.WalkTicksPerCell + Math.Max(eta - walkCells * Info.WalkTicksPerCell, 0) + drive,
				FareCents = TaxiFare(stand.Cell, toRoad),
				Transfers = 0,
				ComfortPenalty = -10 * Info.ComfortWeight[AgeIndex(age)] / 100,
			};
		}

		int StartTaxiJourney(in TripRequest request, ITripListener listener, int depart)
		{
			if (router == null)
				return 0;

			var stand = NearestStand(request.OriginRoad, out var walkCells);
			if (stand == null || walkCells > Info.WalkRadius)
				return 0;

			var taxi = FindIdleTaxi(stand.Cell);
			if (taxi == null)
				return 0;

			if (router.FindPath(stand.Cell, request.DestinationRoad) == null)
				return 0;

			CPos[] toStand = [taxi.Cell];
			if (taxi.Cell != stand.Cell)
			{
				toStand = router.FindPath(taxi.Cell, stand.Cell);
				if (toStand == null)
					return 0;
			}

			var now = world.WorldTick;
			var fare = TaxiFare(stand.Cell, request.DestinationRoad);
			taxi.HasFare = true;
			taxi.Fare = new PaxGroup
			{
				JourneyId = nextJourneyId++,
				Listener = listener,
				OwnerId = request.OwnerId,
				Count = 1,
				StartTick = depart,
				FarePaid = fare,
			};
			taxi.DestRoad = request.DestinationRoad;
			taxi.StopId = stand.Id;
			taxi.PassengerReadyTick = depart + walkCells * Info.WalkTicksPerCell;
			taxi.PaxCount = 0;
			if (toStand.Length == 1)
			{
				taxi.State = TransitVehicleState.WaitingPassenger;
				taxi.Cell = stand.Cell;
			}
			else
			{
				StartMove(taxi, toStand, TransitVehicleState.ToPickup, now);
			}

			return taxi.Fare.JourneyId;
		}

		void ManageTaxiFleet(int now)
		{
			var stands = StandCount();
			if (stands == 0)
				return;

			var wanted = stands * Info.TaxisPerStand;
			var taxis = 0;
			for (var i = 0; i < vehicles.Count; i++)
				if (vehicles[i].Mode == TransitMode.Taxi && vehicles[i].State != TransitVehicleState.Gone && !vehicles[i].Recall)
					taxis++;

			if (taxis < wanted && vehicles.Count < Info.MaxVehicles)
			{
				var stand = FirstStandOrNull();
				for (var i = 0; i < depots.Count && stand != null; i++)
				{
					var depot = depots[i];
					if (depot.Mode != TransitMode.Taxi || depot.Used >= depot.Capacity || depot.Road == CPos.Zero)
						continue;

					var target = NearestStand(depot.Road, out _);
					var path = router.FindPath(depot.Road, target.Cell);
					if (path == null)
						continue;

					var v = new TransitVehicle
					{
						Id = nextVehicleId++,
						Mode = TransitMode.Taxi,
						DepotId = depot.ActorId,
						Cell = depot.Road,
						StopId = target.Id,
					};
					depot.Used++;
					vehicles.Add(v);
					StartMove(v, path, TransitVehicleState.ToPickup, now);
					Version++;
					break;
				}
			}
			else if (taxis > wanted)
			{
				for (var i = vehicles.Count - 1; i >= 0; i--)
				{
					if (vehicles[i].Mode == TransitMode.Taxi && vehicles[i].State == TransitVehicleState.Idle)
					{
						GoToDepot(vehicles[i], now);
						break;
					}
				}
			}
		}

		TransitStop FirstStandOrNull()
		{
			for (var i = 0; i < stops.Count; i++)
				if (stops[i].Mode == TransitMode.Taxi)
					return stops[i];

			return null;
		}

		void TickWaitingTaxi(TransitVehicle v, int now)
		{
			if (!v.HasFare)
			{
				v.State = TransitVehicleState.Idle;
				return;
			}

			if (now < v.PassengerReadyTick)
				return;

			var path = router?.FindPath(v.Cell, v.DestRoad);
			if (path == null)
			{
				FailGroup(v.Fare, TripFailure.NoRoute);
				v.HasFare = false;
				v.State = TransitVehicleState.Idle;
				return;
			}

			v.PaxCount = 1;
			if (path.Length == 1)
			{
				v.LegCells = path;
				HandleTaxiArrivalCarrying(v, now);
				return;
			}

			StartMove(v, path, TransitVehicleState.Carrying, now);
		}

		void HandleTaxiArrival(TransitVehicle v, int now)
		{
			switch (v.State)
			{
				case TransitVehicleState.ToPickup:
					v.PaxCount = 0;
					v.State = v.HasFare ? TransitVehicleState.WaitingPassenger : TransitVehicleState.Idle;
					break;
				case TransitVehicleState.Carrying:
					HandleTaxiArrivalCarrying(v, now);
					break;
			}
		}

		void HandleTaxiArrivalCarrying(TransitVehicle v, int now)
		{
			if (v.HasFare)
			{
				var fare = v.Fare.FarePaid;
				TotalFareCents += fare;
				farePending[(int)TransitMode.Taxi] += fare;
				TotalBoardings++;
				monthPassengers++;
				totalAlightings++;
				PushEvent(now + 1, EventComplete, v.StopId, v.Fare);
				v.HasFare = false;
			}

			v.PaxCount = 0;
			var stand = NearestStand(v.Cell, out _);
			if (stand == null)
			{
				GoToDepot(v, now);
				return;
			}

			v.StopId = stand.Id;
			if (stand.Cell == v.Cell)
			{
				v.State = TransitVehicleState.Idle;
				return;
			}

			var path = router?.FindPath(v.Cell, stand.Cell);
			if (path == null)
			{
				GoToDepot(v, now);
				return;
			}

			StartMove(v, path, TransitVehicleState.ToPickup, now);
		}

		void ReleaseTaxisForStop(int stopId)
		{
			var now = world.WorldTick;
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (v.Mode != TransitMode.Taxi || v.StopId != stopId || v.State == TransitVehicleState.Carrying || v.State == TransitVehicleState.Gone)
					continue;

				if (v.HasFare)
				{
					FailGroup(v.Fare, TripFailure.NoRoute);
					v.HasFare = false;
				}

				if (v.TripId > 0)
					traffic?.CancelTrip(v.TripId);

				v.TripId = 0;
				GoToDepot(v, now);
			}

			CompactVehicles();
		}
	}
}
