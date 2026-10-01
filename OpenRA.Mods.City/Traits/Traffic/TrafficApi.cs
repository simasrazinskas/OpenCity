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
	// Traffic-owned optional contracts. TrafficSim implements the provider side; other work packages find it with
	// world.WorldActor.TraitsImplementing<IVehicleInspector>().FirstOrDefault() (and tolerate null). Proposed for CityInterfaces.cs.

	/// <summary>Road flags traffic would like from the road network (NET). RoadLayer may implement it; absent = no lanes/tracks.</summary>
	public interface ITrafficRoadFlags
	{
		/// <summary>Reserved bus lane: buses, taxis and emergency vehicles drive free-flow; cars avoid the cell.</summary>
		bool HasBusLane(CPos cell);

		/// <summary>Tram rails in the cell: trams use them without taking road capacity.</summary>
		bool HasTramTrack(CPos cell);
	}

	/// <summary>Read-only view of a vehicle on the road (for selection panels and the follow camera).</summary>
	public struct VehicleView
	{
		/// <summary>Stable id of the vehicle's trip (same as the id returned by RequestTrip, aggregate trips have their own ids).</summary>
		public int TripId;

		/// <summary>The requester's id: citizen id for citizen trips, shipment / service request id otherwise (0 = aggregate).</summary>
		public int OwnerId;

		/// <summary>Sprite image name of the vehicle (car-a, truck, ambulance...).</summary>
		public string Image;

		public TripPurpose Purpose;
		public TravelMode Mode;
		public CPos OriginRoad;
		public CPos DestinationRoad;
		public int OriginProperty;
		public int DestinationProperty;
		public int DepartTick;

		/// <summary>0..100 progress along the route.</summary>
		public int ProgressPercent;

		/// <summary>Current drawn position (interpolated) and facing.</summary>
		public WPos Position;
		public WAngle Facing;

		/// <summary>True for a vehicle that crashed and waits for help.</summary>
		public bool Crashed;
	}

	public interface IVehicleInspector
	{
		/// <summary>The visible vehicle closest to a world position within `radius` world units (1024 = one cell). Uses the last drawn frame.</summary>
		bool TryGetVehicleAt(WPos position, int radius, out VehicleView view);

		/// <summary>Looks a vehicle up by its trip id (follow camera: call every frame, the position is interpolated).</summary>
		bool TryGetVehicle(int tripId, out VehicleView view);
	}

	public struct TrafficIncident
	{
		public int Id;
		public CPos Cell;
		public int StartTick;

		/// <summary>True once a police / maintenance vehicle has been dispatched.</summary>
		public bool ResponderDispatched;

		/// <summary>True once a responder arrived and the wreck is being cleared.</summary>
		public bool Clearing;
	}

	/// <summary>Accidents that currently block a lane. SVC (or UI) may dispatch their own responder and resolve incidents.</summary>
	public interface ITrafficIncidents
	{
		IReadOnlyList<TrafficIncident> Incidents { get; }

		/// <summary>A responder reached the incident: it is cleared shortly afterwards.</summary>
		void ResolveIncident(int incidentId);
	}

	/// <summary>Data behind the traffic info view modes (volume vs flow, like CS2).</summary>
	public interface ITrafficInfo
	{
		/// <summary>0..100 vehicles per time relative to the capacity of the cell's lanes (volume mode).</summary>
		int GetTrafficVolumePercent(CPos cell);

		/// <summary>0..100 speed relative to free flow (flow mode). Same as ITrafficService.GetTrafficFlow.</summary>
		int GetTrafficFlowPercent(CPos cell);

		/// <summary>City flow percent averaged over the given game hour (0..23) of the last day, or -1 if unknown.</summary>
		int GetFlowHistory(int hour);

		/// <summary>Rolling average ticks of completed trips per 100 route cells.</summary>
		int AverageTicksPer100Cells { get; }

		int OpenIncidents { get; }
	}
}
