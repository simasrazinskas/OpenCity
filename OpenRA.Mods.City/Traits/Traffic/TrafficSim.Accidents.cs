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
	// Accidents: a rare crash takes the vehicle out of the queue and blocks one lane of its link until a police car from the
	// nearest police building (or any caller of ResolveIncident) arrives, or the wreck times out. Pure hash randomness, no SharedRandom.
	public sealed partial class TrafficSim
	{
		sealed class Incident
		{
			public int Id;
			public int Link;
			public int Start;
			public int ClearAt = -1;
			public bool Dispatched;
			public TripRec Crashed;
			public int Sprite;
		}

		readonly List<Incident> incidents = [];
		byte[] laneBlock;
		int nextIncidentId;
		int totalAccidents;
		CityClimate climate;
		bool climateSearched;

		void InitAccidents()
		{
			laneBlock = new byte[linkCount];
		}

		CityClimate Climate()
		{
			if (!climateSearched)
			{
				climateSearched = true;
				climate = world.WorldActor.TraitOrDefault<CityClimate>();
			}

			return climate;
		}

		// Bad weather (CityClimate) multiplies the accident chance and slows traffic; 100 = neutral / no climate.
		int WeatherPercent() { return Climate()?.AccidentRiskPercent ?? 100; }

		int WeatherSpeedPercent() { return Climate()?.TrafficSpeedPercent ?? 100; }

		// Called right after vehicle v entered link nl.
		void MaybeCrash(int v, int nl)
		{
			if (Info.AccidentOdds <= 0 || laneBlock[nl] > 0 || incidents.Count >= 32)
				return;

			var t = vTrip[v];
			if (t.Kind == KindEmergency || t.Kind == KindTram)
				return;

			var mult = Math.Max(1, WeatherPercent() * AccidentPolicyPercent(nl >> 2) / 100);
			var odds = Math.Max(1L, (long)Info.AccidentOdds * 100 / mult);
			if ((Hash(t.Id * 7919 + vStep[v], tick) & 0x7fffffff) % odds != 0)
				return;

			RemoveFromQueue(v);
			ReleaseVehicle(v);
			t.Vehicle = NoVehicle;
			t.Crashed = true;

			var inc = new Incident { Id = ++nextIncidentId, Link = nl, Start = tick, Crashed = t, Sprite = t.Sprite };
			incidents.Add(inc);
			totalAccidents++;
			laneBlock[nl]++;
			RefreshCostSnapshot();
			DispatchResponder(inc);
		}

		void DispatchResponder(Incident inc)
		{
			var cell = inc.Link >> 2;
			var at = ToCPos(cell);
			var bestId = uint.MaxValue;
			var bestDist = int.MaxValue;
			var bestRoad = -1;
			foreach (var tp in world.ActorsWithTrait<ServiceBuilding>())
			{
				var a = tp.Actor;
				var info = tp.Trait.Info;
				if (info.Kind != ServiceKind.Police || info.Prison || !a.IsInWorld || a.IsDead)
					continue;

				var cb = a.TraitOrDefault<CityBuilding>();
				if (cb == null || !cb.IsOperational)
					continue;

				var road = net.GetAccessRoad(cb.Cells);
				if (road == CPos.Zero || !InMap(road) || roadFlag[Cell(road)] == 0)
					continue;

				var dist = Math.Abs(road.X - at.X) + Math.Abs(road.Y - at.Y);
				if (dist < bestDist || (dist == bestDist && a.ActorID < bestId))
				{
					bestDist = dist;
					bestId = a.ActorID;
					bestRoad = Cell(road);
				}
			}

			if (bestRoad < 0)
				return;

			var t = NewTrip(bestRoad, cell, 0, TripPurpose.Emergency, tick, Info.AccidentResponder);
			t.Internal = true;
			t.Incident = inc.Id;
			inc.Dispatched = true;
			Enqueue(t);
		}

		void TickIncidents()
		{
			for (var i = incidents.Count - 1; i >= 0; i--)
			{
				var inc = incidents[i];
				var gone = roadFlag[inc.Link >> 2] == 0;
				if (inc.ClearAt < 0 && tick - inc.Start >= Info.AccidentTimeoutTicks)
					inc.ClearAt = tick;

				if (!gone && (inc.ClearAt < 0 || tick < inc.ClearAt))
					continue;

				if (laneBlock[inc.Link] > 0)
					laneBlock[inc.Link]--;

				incidents.RemoveAt(i);

				// The occupants carry on: the trip ends where the wreck was.
				if (!gone)
					Arrive(inc.Crashed, tick);
				else
					Fail(inc.Crashed, TripFailure.RoadClosed);

				RefreshCostSnapshot();
			}
		}

		void ITrafficIncidents.ResolveIncident(int incidentId)
		{
			foreach (var inc in incidents)
			{
				if (inc.Id == incidentId)
				{
					if (inc.ClearAt < 0)
						inc.ClearAt = tick + Math.Max(1, Info.AccidentClearTicks);

					return;
				}
			}
		}

		IReadOnlyList<TrafficIncident> ITrafficIncidents.Incidents
		{
			get
			{
				var list = new List<TrafficIncident>(incidents.Count);
				foreach (var inc in incidents)
					list.Add(new TrafficIncident
					{
						Id = inc.Id,
						Cell = ToCPos(inc.Link >> 2),
						StartTick = inc.Start,
						ResponderDispatched = inc.Dispatched,
						Clearing = inc.ClearAt >= 0,
					});

				return list;
			}
		}
	}
}
