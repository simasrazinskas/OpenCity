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

namespace OpenRA.Mods.City.Traits
{
	public sealed partial class TransitLayer
	{
		readonly Dictionary<int, int> walkingJourneys = [];

		sealed class WalkingArrival : ITripListener
		{
			readonly TransitLayer layer;
			readonly PaxGroup group;
			readonly byte kind;
			readonly int stop;

			public WalkingArrival(TransitLayer layer, PaxGroup group, byte kind, int stop)
			{
				this.layer = layer;
				this.group = group;
				this.kind = kind;
				this.stop = stop;
			}

			public void OnTripArrived(in TripResult result)
			{
				if (layer.walkingJourneys.Remove(group.JourneyId))
					layer.PushEvent(result.ArriveTick, kind, stop, group);
			}

			public void OnTripFailed(int tripId, int ownerId, TripFailure reason)
			{
				if (layer.walkingJourneys.Remove(group.JourneyId))
				{
					if (kind == EventTaxiReady)
					{
						var taxi = layer.GetVehicle(stop);
						if (taxi != null && taxi.HasFare && taxi.Fare.JourneyId == group.JourneyId)
						{
							taxi.HasFare = false;
							if (taxi.State == TransitVehicleState.WaitingPassenger)
								taxi.State = TransitVehicleState.Idle;
						}
					}

					FailGroup(group, reason);
				}
			}
		}

		bool ScheduleWalk(PaxGroup group, CPos from, CPos to, byte kind, int stop, int depart, int fallbackTicks)
		{
			if (group.Listener == null || traffic == null)
			{
				PushEvent(depart + System.Math.Max(1, fallbackTicks), kind, stop, group);
				return true;
			}

			walkingJourneys[group.JourneyId] = TripPending;
			var id = traffic.RequestTrip(new TripRequest
			{
				Purpose = TripPurpose.Commute,
				AllowedModes = TravelModes.Walk,
				OriginRoad = from,
				DestinationRoad = to,
				DepartTick = depart,
				OwnerId = group.OwnerId,
			}, new WalkingArrival(this, group, kind, stop));
			if (id <= 0)
			{
				walkingJourneys.Remove(group.JourneyId);
				return false;
			}

			if (walkingJourneys.ContainsKey(group.JourneyId))
				walkingJourneys[group.JourneyId] = id;

			return true;
		}
	}
}
