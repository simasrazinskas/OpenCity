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
	// Wave 2: crime API for the citizen simulation, police patrols, park maintenance, service fees, district restriction, helicopters.
	public partial class ServiceSimulation : ICityCrime
	{
		readonly int[] feePercent = [100, 100];
		readonly int[] feeCents = new int[2];
		int cPatrols, cMaint, cHeli;

		// ---- ICityCrime ----
		public bool ExternalCrimeSource { get; set; }

		public bool ReportCrime(int criminalId, int targetProperty, ICrimeListener listener)
		{
			var d = Data(targetProperty);
			if (d == null || !d.Alive || d.CrimeEnd != 0)
				return false;

			StartCrime(d, Now);
			d.CrimeCriminal = criminalId;
			d.CrimeListener = listener;
			return true;
		}

		public bool IsCrimeActive(int propertyId) { return (Data(propertyId)?.CrimeEnd ?? 0) > 0; }

		public int JailOccupancy
		{
			get
			{
				var n = 0;
				for (var i = 0; i < providers.Count; i++)
					if (providers[i].Group == CatchmentGroup.Police)
						n += providers[i].Inmates;

				return n;
			}
		}

		public int JailCapacity
		{
			get
			{
				var n = 0;
				for (var i = 0; i < providers.Count; i++)
					if (providers[i].Group == CatchmentGroup.Police && providers[i].Active)
						n += providers[i].Capacity;

				return n;
			}
		}

		/// <summary>A criminal was caught by `home`'s car: jail him, or let him go when the jail is full.</summary>
		void Arrest(PropData d, ServiceState home)
		{
			var id = d.CrimeCriminal;
			var listener = d.CrimeListener;
			d.CrimeCriminal = 0;
			d.CrimeListener = null;
			if (home != null && home.Inmates < home.Capacity)
			{
				home.Inmates++;
				cArrests++;
				if (id > 0)
				{
					home.Named.Add(new NamedInmate { Id = id, Until = Now + Info.JailTicks, Listener = listener });
					listener?.OnArrested(id, home.Prop.Id);
				}
			}
			else
			{
				cArrests++;
				cEscapes++;
				if (id > 0)
					listener?.OnEscaped(id);
			}
		}

		void CrimeEscaped(PropData d)
		{
			var id = d.CrimeCriminal;
			var listener = d.CrimeListener;
			d.CrimeCriminal = 0;
			d.CrimeListener = null;
			if (id > 0)
				listener?.OnEscaped(id);
		}

		void ReleaseNamed(int t)
		{
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				for (var k = s.Named.Count - 1; k >= 0; k--)
				{
					if (s.Named[k].Until > t)
						continue;

					var n = s.Named[k];
					s.Named.RemoveAt(k);
					s.Inmates = Math.Max(0, s.Inmates - 1);
					n.Listener?.OnReleased(n.Id, s.Prop.Id);
				}
			}
		}

		// ---- patrols ----
		void DispatchPatrols(int t)
		{
			if (Info.PatrolIntervalTicks <= 0)
				return;

			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != CatchmentGroup.Police || !s.Active || s.Info.Prison || s.Assigned.Count == 0)
					continue;

				if (s.PatrolDue == 0)
				{
					s.PatrolDue = t + ServiceMath.Hash(s.Prop.Id, 1, 5) % Info.PatrolIntervalTicks + 1;
					continue;
				}

				// Cars needed for responses are never taken out on patrol.
				if (t < s.PatrolDue || s.Fleet - s.FleetInUse <= Info.PatrolReserve)
					continue;

				s.PatrolDue = t + Info.PatrolIntervalTicks;
				var d = Data(s.Assigned[ServiceMath.Hash(s.Prop.Id, t, 6) % s.Assigned.Count]);
				if (d == null || !d.Alive || !HasAccess(d))
					continue;

				var v = NewVehicle(ServiceKind.Police, s, true);
				v.Patrol = true;
				AddStop(v, d.Prop.Id, 0);
				cPatrols++;
				StartLeg(v, t);
			}
		}

		/// <summary>A patrol car passed `stop`: nearby lots get safer for a while.</summary>
		void ApplyPatrol(ServiceVehicle v, PropData stop)
		{
			if (Info.PatrolRelief <= 0 || v.Home == null)
				return;

			for (var i = 0; i < v.Home.Assigned.Count; i++)
			{
				var d = Data(v.Home.Assigned[i]);
				if (d != null && d.Alive && Manhattan(d.Prop.Origin, stop.Prop.Origin) <= Info.PatrolRadius)
					d.PatrolRelief = Math.Min(Info.PatrolMax, d.PatrolRelief + Info.PatrolRelief);
			}
		}

		// ---- parks ----
		void DecayParks()
		{
			if (Info.ParkDecayPerDay <= 0)
				return;

			var tpd = TicksPerDay;
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != CatchmentGroup.Parks || !s.Active)
					continue;

				s.ProcessMilli += (long)Info.ParkDecayPerDay * 1000 * 25 / tpd;
				var lose = (int)(s.ProcessMilli / 1000);
				if (lose > 0)
				{
					s.ProcessMilli -= lose * 1000L;
					s.Condition = Math.Max(0, s.Condition - lose);
				}
			}
		}

		void DispatchMaintenance(int t)
		{
			if (Info.ParkDecayPerDay <= 0)
				return;

			for (var i = 0; i < providers.Count; i++)
			{
				var park = providers[i];
				if (park.Group != CatchmentGroup.Parks || !park.Active || park.Condition >= Info.ParkMaintThreshold)
					continue;

				if (park.MaintSince > 0 && t - park.MaintSince < 3000)
					continue;

				var worker = park.Fleet - park.FleetInUse > 0 ? park : null;
				if (worker == null)
				{
					ProvidersAt(CatchmentGroup.Parks, AccessOf(park.Need), out var a, out _, out var b, out _);
					worker = a != null && a.Active && a.Fleet - a.FleetInUse > 0 ? a : b != null && b.Active && b.Fleet - b.FleetInUse > 0 ? b : null;
				}

				if (worker == null)
					continue;

				park.MaintSince = t;
				var v = NewVehicle(ServiceKind.Parks, worker, true);
				v.Maintenance = true;
				AddStop(v, park.Prop.Id, 0);
				cMaint++;
				StartLeg(v, t);
			}
		}

		/// <summary>0..100 condition of a park building (100 = well kept). Other buildings: 100.</summary>
		public int GetCondition(Actor building)
		{
			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			return s?.Condition ?? 100;
		}

		// ---- fees ----
		public int GetFeePercent(ServiceFee fee) { return feePercent[(int)fee]; }

		void BookFees()
		{
			for (var i = 0; i < providers.Count; i++)
				if (providers[i].Group >= CatchmentGroup.Edu1 && providers[i].Group <= CatchmentGroup.Edu4 && providers[i].Active)
					feeCents[1] += providers[i].Enrolled * Info.EducationFeeCentsPerStudent * feePercent[1] / 100;

			if (cm == null)
				return;

			for (var i = 0; i < feeCents.Length; i++)
			{
				var dollars = feeCents[i] / 100;
				if (dollars > 0)
				{
					cm.AddFunds(dollars, i == 0 ? "fee-health" : "fee-education");
					feeCents[i] %= 100;
				}
			}
		}

		// ---- districts ----
		bool Allows(ServiceState s, CPos cell)
		{
			if (s.DistrictMask == 0 || progression == null || cell == CPos.Zero)
				return true;

			var district = Math.Clamp(progression.GetDistrict(cell), 0, 31);
			return (s.DistrictMask >> district & 1) != 0;
		}

		public int GetDistrictMask(Actor building)
		{
			return Data(registry?.GetByActor(building)?.Id ?? 0)?.Service?.DistrictMask ?? 0;
		}

		void SetDistricts(Actor building, int mask)
		{
			if (building == null || building.Disposed || building.Owner != self.Owner)
				return;

			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			if (s == null)
				return;

			s.DistrictMask = mask;
			MarkAllDirty();
		}

		// ---- helicopters ----
		ServiceState PickHeli(CatchmentGroup group, CPos target)
		{
			ServiceState best = null;
			var bestDist = int.MaxValue;
			for (var i = 0; i < providers.Count; i++)
			{
				var h = providers[i];
				if (h.Group != group || !h.Info.Helicopter || !h.Active || h.Fleet - h.FleetInUse <= 0 || !Allows(h, target))
					continue;

				var dist = Manhattan(h.Prop.Origin, target);
				if (dist <= h.Info.FlightRange && dist < bestDist)
				{
					bestDist = dist;
					best = h;
				}
			}

			return best;
		}

		bool TryHeliMedevac(PropData d, int waiting, int t)
		{
			if (t - d.SickSince < Info.HeliWaitTicks)
				return false;

			var h = PickHeli(CatchmentGroup.Health, d.Prop.Origin);
			if (h == null)
				return false;

			ServiceState dest = null;
			var bestDist = int.MaxValue;
			for (var i = 0; i < providers.Count; i++)
			{
				var f = providers[i];
				if (f.Group != CatchmentGroup.Health || f.Info.Helicopter || FreeBeds(f) <= 0 || !Allows(f, d.Prop.Origin))
					continue;

				var dist = Manhattan(f.Prop.Origin, d.Prop.Origin);
				if (dist < bestDist)
				{
					bestDist = dist;
					dest = f;
				}
			}

			if (dest == null)
				return false;

			var n = Math.Min(Math.Min(Info.AmbulanceCapacity, waiting), FreeBeds(dest));
			var v = NewVehicle(ServiceKind.Health, h, true);
			v.Flying = true;
			v.Dest = dest;
			v.MaxLoad = n;
			v.Reserved = n;
			dest.Reserved += n;
			d.SickReserved += n;
			AddStop(v, d.Prop.Id, n);
			cRequests += n;
			cHeli++;
			StartLeg(v, t);
			return true;
		}

		bool TryHeliFire(PropData d, int t)
		{
			if (t - d.FireStart < Info.HeliWaitTicks)
				return false;

			var h = PickHeli(CatchmentGroup.Fire, d.Prop.Origin);
			if (h == null)
				return false;

			var v = NewVehicle(ServiceKind.Fire, h, true);
			v.Flying = true;
			v.Target = d.Prop.Id;
			AddStop(v, d.Prop.Id, 1);
			d.EnginesEnRoute++;
			cHeli++;
			StartLeg(v, t);
			return true;
		}

		/// <summary>Helicopters currently flying (straight lines between cells), for renderers.</summary>
		public void GetFlights(List<HelicopterFlight> into)
		{
			var t = Now;
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (!v.Active || !v.Flying || v.TripId != 0 || v.Phase == VehiclePhase.Working || v.DueTick <= v.LegStart || v.DueTick == int.MaxValue)
					continue;

				var p = Math.Clamp((t - v.LegStart) * 1000 / (v.DueTick - v.LegStart), 0, 1000);
				into.Add(new HelicopterFlight { From = v.LegFrom, To = v.LegTo, Permille = p, Kind = v.Kind });
			}
		}
	}
}
