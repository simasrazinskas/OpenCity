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
	// Dispatch: needs become missions for service vehicles. A vehicle drives its legs as traffic trips when ITrafficService is
	// present (and the real-vehicle cap allows), otherwise (or on failure) as a virtual timer with the same effects.
	public partial class ServiceSimulation
	{
		readonly List<ServiceVehicle> vehicles = [];
		int realActive;

		int RealCap()
		{
			return Math.Min(Info.RealVehiclesMax, Info.RealVehiclesBase + Population() / Math.Max(1, Info.RealVehiclesPopDivisor));
		}

		ServiceVehicle NewVehicle(ServiceKind kind, ServiceState home, bool usesFleet)
		{
			ServiceVehicle v = null;
			for (var i = 0; i < vehicles.Count && v == null; i++)
				if (!vehicles[i].Active)
					v = vehicles[i];

			if (v == null)
			{
				v = new ServiceVehicle { Slot = vehicles.Count };
				vehicles.Add(v);
			}

			var slot = v.Slot;
			v.Active = true;
			v.Kind = kind;
			v.Home = home;
			v.Dest = null;
			v.UsesFleet = usesFleet;
			v.Emergency = kind == ServiceKind.Fire || kind == ServiceKind.Police || kind == ServiceKind.Health;
			v.StopCount = v.StopIdx = 0;
			v.Load = v.MaxLoad = v.Reserved = 0;
			v.Phase = VehiclePhase.ToStop;
			v.TripId = 0;
			v.DueTick = 0;
			v.Position = home != null ? AccessOf(home.Need) : CPos.Zero;
			v.Target = 0;
			v.Imported = false;
			v.Patrol = v.Maintenance = v.Flying = false;
			v.LegStart = 0;
			v.Slot = slot;
			if (usesFleet && home != null)
				home.FleetInUse++;

			cDispatched++;
			return v;
		}

		static void AddStop(ServiceVehicle v, int propertyId, int amount)
		{
			v.Stops[v.StopCount] = propertyId;
			v.Taken[v.StopCount] = amount;
			v.StopCount++;
		}

		void DispatchPulse(int t)
		{
			DispatchCollect(CatchmentGroup.Garbage, t);
			DispatchCollect(CatchmentGroup.Post, t);
			DispatchHealth(t);
			DispatchDeath(t);
			DispatchFire(t);
			DispatchPolice(t);
			DispatchGarbageImport(t);
			DispatchPatrols(t);
			DispatchMaintenance(t);
		}

		// ---- garbage and mail: batched pick-up routes ----
		void DispatchCollect(CatchmentGroup group, int t)
		{
			var garbage = group == CatchmentGroup.Garbage;
			var threshold = garbage ? Info.CollectKg : Info.MailCollectItems;
			var capacity = garbage ? Info.TruckKg : Info.PostVanItems;

			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != group || !s.Active || s.Fleet - s.FleetInUse <= 0 || s.Assigned.Count == 0)
					continue;

				// Anchor: the biggest unreserved pile in the catchment.
				PropData anchor = null;
				var best = threshold - 1;
				for (var k = 0; k < s.Assigned.Count; k++)
				{
					var d = Data(s.Assigned[k]);
					if (d == null || !d.Alive)
						continue;

					var free = garbage ? d.GarbageKg - d.GarbageReserved : d.MailItems - d.MailReserved;
					if (free > best)
					{
						best = free;
						anchor = d;
					}
				}

				if (anchor == null)
					continue;

				ServiceState dest = null;
				if (garbage)
				{
					dest = FindGarbageFacility(s, anchor, Math.Min(capacity, best));
					if (dest == null)
					{
						NotifyOnce(ref lastLandfillNotice, t, "notification-service-landfill-full");
						Chirp("chirp-service-landfill-full", 1, anchor);
						continue;
					}
				}

				var v = NewVehicle(s.Info.Kind, s, true);
				v.MaxLoad = capacity;
				v.Dest = dest;
				var expected = 0;
				AddCollectStop(v, anchor, garbage, capacity, ref expected);
				for (var round = 0; round < 7 && expected < capacity; round++)
				{
					PropData next = null;
					var nextDist = 9;
					for (var k = 0; k < s.Assigned.Count; k++)
					{
						var d = Data(s.Assigned[k]);
						if (d == null || !d.Alive || IsStop(v, d.Prop.Id))
							continue;

						var free = garbage ? d.GarbageKg - d.GarbageReserved : d.MailItems - d.MailReserved;
						if (free < threshold)
							continue;

						var dist = Manhattan(AccessOf(anchor), AccessOf(d));
						if (dist < nextDist)
						{
							nextDist = dist;
							next = d;
						}
					}

					if (next == null)
						break;

					AddCollectStop(v, next, garbage, capacity, ref expected);
				}

				if (dest != null)
				{
					v.Reserved = expected;
					dest.Reserved += expected;
				}

				StartLeg(v, t);
			}
		}

		static bool IsStop(ServiceVehicle v, int propertyId)
		{
			for (var i = 0; i < v.StopCount; i++)
				if (v.Stops[i] == propertyId)
					return true;

			return false;
		}

		void AddCollectStop(ServiceVehicle v, PropData d, bool garbage, int capacity, ref int expected)
		{
			var free = garbage ? d.GarbageKg - d.GarbageReserved : d.MailItems - d.MailReserved;
			var take = Math.Min(free, capacity - expected);
			if (garbage)
				d.GarbageReserved += take;
			else
				d.MailReserved += take;

			expected += take;
			AddStop(v, d.Prop.Id, take);
			cRequests++;
		}

		ServiceState FindGarbageFacility(ServiceState home, PropData from, int expected)
		{
			if (home.Stored + home.Reserved + expected <= home.Capacity || (home.Throughput > 0 && home.Stored + home.Reserved < home.Capacity))
				return home;

			ServiceState best = null;
			var bestDist = int.MaxValue;
			for (var i = 0; i < providers.Count; i++)
			{
				var f = providers[i];
				if (f.Group != CatchmentGroup.Garbage || !f.Active || f == home)
					continue;

				if (f.Stored + f.Reserved + expected > f.Capacity && !(f.Throughput > 0 && f.Stored + f.Reserved < f.Capacity))
					continue;

				var dist = Manhattan(from.Prop.Origin, f.Prop.Origin);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = f;
				}
			}

			return best;
		}

		// ---- patients ----
		void DispatchHealth(int t)
		{
			for (var q = sickQueue.Count - 1; q >= 0; q--)
			{
				var d = sickQueue[q];
				if (!d.Alive)
				{
					sickQueue.RemoveAt(q);
					continue;
				}

				var waiting = d.SickWaiting - d.SickReserved;
				if (waiting > 0 && t - d.SickSince > Info.UntreatedRecoverTicks)
				{
					// Gave up waiting: recovers, or dies.
					for (var k = 0; k < waiting; k++)
					{
						if (ServiceMath.Hash(d.Prop.Id, t, 55 + k) % 100 < Info.UntreatedDeathPercent)
						{
							AddBody(d);
							cDeaths++;
						}
					}

					d.SickWaiting -= waiting;
				}

				if (d.SickWaiting == 0 && d.SickReserved == 0)
					sickQueue.RemoveAt(q);
			}

			for (var q = 0; q < sickQueue.Count; q++)
			{
				var d = sickQueue[q];
				var waiting = d.SickWaiting - d.SickReserved;
				if (waiting <= 0 || !HasAccess(d))
					continue;

				ProvidersAt(CatchmentGroup.Health, AccessOf(d), out var a, out _, out var b, out _, d.Prop.Origin);
				var s = PickAmbulanceProvider(a, b);
				if (s != null)
				{
					var n = Math.Min(Math.Min(Info.AmbulanceCapacity, waiting), FreeBeds(s));
					var v = NewVehicle(ServiceKind.Health, s, true);
					v.Dest = s;
					v.MaxLoad = n;
					v.Reserved = n;
					s.Reserved += n;
					d.SickReserved += n;
					AddStop(v, d.Prop.Id, n);
					cRequests += n;
					StartLeg(v, t);
				}
				else if (!TryHeliMedevac(d, waiting, t) && ((a == null && b == null) || ImportPolicy(AccessOf(d))) && t - d.SickSince > 60)
					ImportVehicle(ServiceKind.Health, d, waiting, Info.ImportCostAmbulance, t);
			}
		}

		static ServiceState PickAmbulanceProvider(ServiceState a, ServiceState b)
		{
			if (a != null && a.Active && a.Fleet - a.FleetInUse > 0 && FreeBeds(a) > 0)
				return a;

			if (b != null && b.Active && b.Fleet - b.FleetInUse > 0 && FreeBeds(b) > 0)
				return b;

			return null;
		}

		// ---- bodies ----
		void DispatchDeath(int t)
		{
			for (var q = bodyQueue.Count - 1; q >= 0; q--)
				if (!bodyQueue[q].Alive || (bodyQueue[q].Bodies == 0 && bodyQueue[q].BodiesReserved == 0))
					bodyQueue.RemoveAt(q);

			for (var q = 0; q < bodyQueue.Count; q++)
			{
				var d = bodyQueue[q];
				var waiting = d.Bodies - d.BodiesReserved;
				if (waiting <= 0 || !HasAccess(d))
					continue;

				ProvidersAt(CatchmentGroup.Deathcare, AccessOf(d), out var a, out _, out var b, out _, d.Prop.Origin);
				var s = a != null && a.Active && a.Fleet - a.FleetInUse > 0 ? a : b != null && b.Active && b.Fleet - b.FleetInUse > 0 ? b : null;
				if (s != null)
				{
					var n = Math.Min(Info.HearseCapacity, waiting);
					var dest = FindBodyFacility(s, d, n);
					if (dest == null)
						continue;

					var v = NewVehicle(ServiceKind.Deathcare, s, true);
					v.Dest = dest;
					v.MaxLoad = n;
					v.Reserved = n;
					dest.Reserved += n;
					d.BodiesReserved += n;
					AddStop(v, d.Prop.Id, n);
					cRequests += n;
					StartLeg(v, t);
				}
				else if (((a == null && b == null) || ImportPolicy(AccessOf(d))) && t - d.BodySince > 120)
					ImportVehicle(ServiceKind.Deathcare, d, waiting, Info.ImportCostHearse, t);
			}
		}

		ServiceState FindBodyFacility(ServiceState home, PropData from, int n)
		{
			if (home.Stored + home.Reserved + n <= home.Capacity)
				return home;

			ServiceState best = null;
			var bestDist = int.MaxValue;
			for (var i = 0; i < providers.Count; i++)
			{
				var f = providers[i];
				if (f.Group != CatchmentGroup.Deathcare || !f.Active || f.Stored + f.Reserved + n > f.Capacity)
					continue;

				var dist = Manhattan(from.Prop.Origin, f.Prop.Origin);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = f;
				}
			}

			return best;
		}

		// ---- imported services (virtual vehicles from the outside connection) ----
		readonly int[] lastImport = new int[ServiceMath.KindCount];

		void ImportVehicle(ServiceKind kind, PropData d, int amount, int costDollars, int t)
		{
			// The outside connection only sends so many vehicles.
			if (lastImport[(int)kind] != 0 && t - lastImport[(int)kind] < Info.ImportCooldownTicks)
				return;

			if (cm != null && !cm.TrySpend(costDollars, "imported-services"))
				return;

			lastImport[(int)kind] = Math.Max(1, t);
			var v = NewVehicle(kind, null, false);
			v.Imported = true;
			v.MaxLoad = amount;
			v.Target = d.Prop.Id;
			AddStop(v, d.Prop.Id, amount);
			if (kind == ServiceKind.Health)
				d.SickReserved += amount;
			else if (kind == ServiceKind.Deathcare)
				d.BodiesReserved += amount;
			else if (kind == ServiceKind.Garbage)
				d.GarbageReserved += amount;
			else if (kind == ServiceKind.Fire)
				d.EnginesEnRoute++;

			v.Phase = VehiclePhase.ToStop;
			v.TripId = 0;
			v.DueTick = t + Info.ImportDelayTicks;
			cImported++;
			cImportCost += costDollars;
			cVirtualTrips++;
		}

		// With the "import services" policy neighbours also take away garbage that piles up beyond the local trucks.
		void DispatchGarbageImport(int t)
		{
			if (progression == null || t % 100 != 0)
				return;

			for (var i = 0; i < propList.Count; i++)
			{
				var d = propList[i];
				if (d.GarbageKg - d.GarbageReserved < Info.SeverePileKg || !ImportPolicy(d.Prop.Origin))
					continue;

				ImportVehicle(ServiceKind.Garbage, d, Math.Min(Info.TruckKg, d.GarbageKg - d.GarbageReserved), Info.ImportCostGarbage, t);
				return;
			}
		}

		// ---- legs ----
		void StartLeg(ServiceVehicle v, int t)
		{
			int target;
			switch (v.Phase)
			{
				case VehiclePhase.ToStop: target = v.StopIdx < v.StopCount ? v.Stops[v.StopIdx] : 0; break;
				case VehiclePhase.ToDest: target = v.Dest != null ? v.Dest.Prop.Id : 0; break;
				default: target = v.Home != null ? v.Home.Prop.Id : 0; break;
			}

			var td = Data(target);
			if (td == null || !td.Alive || !HasAccess(td))
			{
				// Target is gone: skip this leg.
				NextPhase(v, t);
				return;
			}

			var road = AccessOf(td);
			var dist = v.Position == CPos.Zero ? 10 : Manhattan(v.Position, road);
			v.LegFrom = v.Position;
			v.LegTo = road;
			v.LegStart = t;
			if (v.Flying)
			{
				var fx = Math.Abs(v.Position.X - road.X);
				var fy = Math.Abs(v.Position.Y - road.Y);
				StartVirtualLeg(v, t, Math.Max(fx, fy) + Math.Min(fx, fy) / 2);
				return;
			}

			if (traffic != null && !Info.VirtualOnly && realActive < RealCap() && v.Position != CPos.Zero && v.Position != road)
			{
				var req = new TripRequest
				{
					Purpose = v.Emergency ? TripPurpose.Emergency : TripPurpose.Service,
					AllowedModes = TravelModes.Car,
					OwnerId = v.Slot,
					OriginRoad = v.Position,
					DestinationRoad = road,
					OriginProperty = 0,
					DestinationProperty = target,
					DepartTick = t,
					VehicleType = v.Home?.Info.VehicleType,
				};

				var id = traffic.RequestTrip(req, this);
				if (id > 0)
				{
					v.TripId = id;
					v.DueTick = 0;
					realActive++;
					cRealTrips++;
					return;
				}
			}

			StartVirtualLeg(v, t, dist);
		}

		void StartVirtualLeg(ServiceVehicle v, int t, int dist)
		{
			var perCell = v.Flying ? Info.HeliTicksPerCell : v.Emergency ? Info.EmergencyTicksPerCell : Info.VirtualTicksPerCell;
			v.TripId = 0;
			v.DueTick = t + Math.Max(10, (v.Flying ? dist + 2 : dist * 4 / 3 + 2) * perCell);
			cVirtualTrips++;
		}

		void TickVehicles(int t)
		{
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (!v.Active || v.TripId != 0)
					continue;

				if (v.Phase == VehiclePhase.Working && v.Kind == ServiceKind.Fire)
				{
					var d = Data(v.Target);
					if (t % 5 == 0 && (d == null || !d.Alive || d.FireDamage == 0))
						EndWork(v, t);

					continue;
				}

				if (v.DueTick > 0 && t >= v.DueTick)
				{
					if (v.Phase == VehiclePhase.Working)
						EndWork(v, t);
					else
						Arrive(v, t);
				}
			}
		}

		void ITripListener.OnTripArrived(in TripResult result)
		{
			if (result.OwnerId < 0 || result.OwnerId >= vehicles.Count)
				return;

			var v = vehicles[result.OwnerId];
			if (!v.Active || v.TripId != result.TripId)
				return;

			realActive = Math.Max(0, realActive - 1);
			v.TripId = 0;
			Arrive(v, Now);
		}

		void ITripListener.OnTripFailed(int tripId, int ownerId, TripFailure reason)
		{
			if (ownerId < 0 || ownerId >= vehicles.Count)
				return;

			var v = vehicles[ownerId];
			if (!v.Active || v.TripId != tripId)
				return;

			realActive = Math.Max(0, realActive - 1);
			cTripFailures++;
			StartVirtualLeg(v, Now, Math.Max(4, v.Position == CPos.Zero ? 10 : 12));
		}

		CPos TargetRoad(ServiceVehicle v)
		{
			int target;
			switch (v.Phase)
			{
				case VehiclePhase.ToStop: target = v.StopIdx < v.StopCount ? v.Stops[v.StopIdx] : 0; break;
				case VehiclePhase.ToDest: target = v.Dest != null ? v.Dest.Prop.Id : 0; break;
				default: target = v.Home != null ? v.Home.Prop.Id : 0; break;
			}

			var td = Data(target);
			return td != null && td.Alive ? AccessOf(td) : v.Position;
		}

		// ---- arrival ----
		void Arrive(ServiceVehicle v, int t)
		{
			v.Position = TargetRoad(v);
			switch (v.Phase)
			{
				case VehiclePhase.ToStop:
					AtStop(v, Data(v.Stops[v.StopIdx]), t);
					break;
				case VehiclePhase.ToDest:
					Unload(v);
					v.Phase = VehiclePhase.ToHome;
					StartLeg(v, t);
					break;
				default:
					Finish(v);
					break;
			}
		}

		void AtStop(ServiceVehicle v, PropData d, int t)
		{
			var planned = v.Taken[v.StopIdx];
			if (d != null && d.Alive)
			{
				switch (v.Kind)
				{
					case ServiceKind.Garbage:
					{
						d.GarbageReserved = Math.Max(0, d.GarbageReserved - planned);
						var take = Math.Min(d.GarbageKg, v.MaxLoad - v.Load);
						d.GarbageKg -= take;
						v.Load += take;
						cGarbageCollected += take;
						break;
					}

					case ServiceKind.Post:
					{
						d.MailReserved = Math.Max(0, d.MailReserved - planned);
						var take = Math.Min(d.MailItems, v.MaxLoad - v.Load);
						d.MailItems -= take;
						v.Load += take;
						break;
					}

					case ServiceKind.Health:
					{
						d.SickReserved = Math.Max(0, d.SickReserved - planned);
						var take = Math.Min(d.SickWaiting, planned);
						d.SickWaiting -= take;
						v.Load += take;
						break;
					}

					case ServiceKind.Deathcare:
					{
						d.BodiesReserved = Math.Max(0, d.BodiesReserved - planned);
						var take = Math.Min(d.Bodies, planned);
						d.Bodies -= take;
						if (d.Bodies == 0)
							d.BodySince = 0;

						v.Load += take;
						cBodiesCollected += take;
						break;
					}

					case ServiceKind.Fire:
						v.Target = d.Prop.Id;
						d.EnginesEnRoute = Math.Max(0, d.EnginesEnRoute - 1);
						d.EnginesOnSite++;
						v.Phase = VehiclePhase.Working;
						v.DueTick = int.MaxValue;
						return;

					case ServiceKind.Police:
						if (v.Patrol)
							ApplyPatrol(v, d);

						ResolveCrimeArrival(v, d, t);
						break;

					case ServiceKind.Parks:
					{
						var park = d.Service;
						if (park != null)
						{
							park.Condition = Math.Min(100, park.Condition + Info.ParkRestore);
							park.MaintSince = 0;
						}

						break;
					}
				}
			}

			v.Phase = VehiclePhase.Working;
			v.DueTick = t + Info.StopTicks;
		}

		void EndWork(ServiceVehicle v, int t)
		{
			if (v.Kind == ServiceKind.Fire)
			{
				var d = Data(v.Target);
				if (d != null)
					d.EnginesOnSite = Math.Max(0, d.EnginesOnSite - 1);

				if (v.Imported)
				{
					v.Active = false;
					return;
				}

				v.StopIdx = v.StopCount;
			}

			NextPhase(v, t);
		}

		void NextPhase(ServiceVehicle v, int t)
		{
			if (v.Phase == VehiclePhase.ToStop || v.Phase == VehiclePhase.Working)
			{
				v.StopIdx++;
				if (v.StopIdx < v.StopCount)
				{
					v.Phase = VehiclePhase.ToStop;
					StartLeg(v, t);
					return;
				}

				if (v.Imported)
				{
					FinishImported(v);
					return;
				}

				v.Phase = v.Dest != null && v.Load > 0 ? VehiclePhase.ToDest : VehiclePhase.ToHome;
				ReleaseUnusedDestReservation(v);
				StartLeg(v, t);
				return;
			}

			if (v.Phase == VehiclePhase.ToDest)
			{
				v.Phase = VehiclePhase.ToHome;
				StartLeg(v, t);
				return;
			}

			Finish(v);
		}

		static void ReleaseUnusedDestReservation(ServiceVehicle v)
		{
			if (v.Dest != null && v.Load == 0 && v.Reserved > 0)
			{
				v.Dest.Reserved = Math.Max(0, v.Dest.Reserved - v.Reserved);
				v.Reserved = 0;
			}
		}

		// Imported vehicles did their work at the stop and just leave (they never return to a provider).
		void FinishImported(ServiceVehicle v)
		{
			if (v.Kind == ServiceKind.Health)
				cTreated += v.Load;

			v.Load = 0;
			v.Active = false;
		}

		void Unload(ServiceVehicle v)
		{
			var dest = v.Dest;
			if (dest == null || !dest.Alive)
				return;

			switch (v.Kind)
			{
				case ServiceKind.Garbage:
				case ServiceKind.Deathcare:
					dest.Reserved = Math.Max(0, dest.Reserved - v.Reserved);
					dest.Stored += v.Load;
					break;
				case ServiceKind.Health:
					Admit(dest, v.Load);
					break;
			}

			v.Reserved = 0;
			v.Load = 0;
		}

		static void Finish(ServiceVehicle v)
		{
			if (v.UsesFleet && v.Home != null && v.Home.FleetInUse > 0)
				v.Home.FleetInUse--;

			// A route that ended early (all stops vanished) still returns its dest reservation.
			if (v.Dest != null && v.Reserved > 0)
				v.Dest.Reserved = Math.Max(0, v.Dest.Reserved - v.Reserved);

			v.Active = false;
			v.Reserved = 0;
		}
	}
}
