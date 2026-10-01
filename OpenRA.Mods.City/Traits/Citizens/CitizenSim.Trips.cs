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
	// Daily schedules: each citizen plans its trips of the day (work/school/shopping/leisure/home) in its daily bucket.
	// Plans live in a ring of per-tick-of-day buckets (pooled linked entries), and become ITrafficService trips at departure.
	// A traffic service that refuses trips (legacy TrafficManager) is tolerated: the effect of the trip is applied abstractly.
	public partial class CitizenSim
	{
		enum Leg : byte { None = 0, ToWork, ToSchool, ToHome, ToShop, ToLeisure }

		int[] tripHead;
		int[] tripNext = new int[1024];
		int[] tripCit = new int[1024];
		int[] tripBirth = new int[1024];
		int[] tripDest = new int[1024];
		byte[] tripKind = new byte[1024];
		int tripFree = -1;
		int tripPoolCount;

		readonly List<Property> shopCands = [];
		readonly List<Property> leisureCands = [];
		readonly int[] tripsByHour = new int[24];
		long tripsRequested, tripsAccepted, tripsRefused, tripsFailed, tripsArrived;

		/// <summary>Average commute (ticks) of work trips.</summary>
		public int AverageCommuteTicks { get; private set; }

		// A traffic service that never accepts a trip (legacy) is probed on one day in ten only.
		void CancelActiveTrip(int ci)
		{
			if (cits[ci].TripId == 0)
				return;

			if (cits[ci].ByTransit)
				transitLayer?.CancelJourney(cits[ci].TripId);
			else
				traffic?.CancelTrip(cits[ci].TripId);

			cits[ci].TripId = 0;
			cits[ci].ByTransit = false;
		}

		bool UseTrips => traffic != null && (tripsAccepted > 0 || tripsRefused <= 2000 || Today % 10 == 0);

		int TicksPerHourC => Math.Max(1, TicksPerDay / 24);

		void EnsureTripRing()
		{
			if (tripHead != null && tripHead.Length == TicksPerDay)
				return;

			tripHead = new int[TicksPerDay];
			Array.Fill(tripHead, -1);
		}

		void AddTrip(int ci, Leg kind, int slot, int dest)
		{
			EnsureTripRing();
			int e;
			if (tripFree >= 0)
			{
				e = tripFree;
				tripFree = tripNext[e];
			}
			else
			{
				if (tripPoolCount == tripNext.Length)
				{
					var n = tripNext.Length * 2;
					Array.Resize(ref tripNext, n);
					Array.Resize(ref tripCit, n);
					Array.Resize(ref tripBirth, n);
					Array.Resize(ref tripDest, n);
					Array.Resize(ref tripKind, n);
				}

				e = tripPoolCount++;
			}

			slot = (slot % tripHead.Length + tripHead.Length) % tripHead.Length;
			tripCit[e] = ci;
			tripBirth[e] = cits[ci].BirthDay;
			tripKind[e] = (byte)kind;
			tripDest[e] = dest;
			tripNext[e] = tripHead[slot];
			tripHead[slot] = e;
		}

		int HourTick(int hour, int ci, int salt)
		{
			var jitter = info.DepartJitterTicks;
			return hour * TicksPerHourC + (jitter > 0 ? Hash(ci, Today, salt) % (2 * jitter) - jitter : 0);
		}

		void PlanDay(int ci, int age, int today)
		{
			if (!UseTrips)
				return;

			var c = cits[ci];
			if ((c.Flags & (CitFlags.Sick | CitFlags.Jailed)) != 0 || c.Household < 0 || hhs[c.Household].Home == 0)
				return;

			if ((c.Flags & CitFlags.Worker) != 0 && c.Work != 0)
			{
				AddTrip(ci, Leg.ToWork, HourTick(info.WorkStartHour - 1, ci, 80), c.Work);
				AddTrip(ci, Leg.ToHome, HourTick(info.WorkStartHour + info.WorkHours, ci, 81), 0);
			}
			else if ((c.Flags & CitFlags.Student) != 0 && c.Work != 0)
			{
				AddTrip(ci, Leg.ToSchool, HourTick(info.SchoolStartHour - 1, ci, 82), c.Work);
				AddTrip(ci, Leg.ToHome, HourTick(info.SchoolStartHour + info.SchoolHours, ci, 83), 0);
			}

			if (c.Leisure >= 100 + Hash(ci, today, 84) % 60 && age > info.ChildMaxAge / 2 && leisureCands.Count > 0)
			{
				var p = PickLeisureDestination(registry.Get(hhs[c.Household].Home));
				if (p != null)
					AddTrip(ci, Leg.ToLeisure, HourTick(17 + Hash(ci, today, 85) % 4, ci, 86), p.Id);
			}
		}

		bool PlanShopTrip(int hh)
		{
			if (!UseTrips || shopCands.Count == 0)
				return false;

			var who = -1;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
				if (AgeOf(m) > info.TeenMaxAge && (cits[m].Flags & CitFlags.Sick) == 0)
				{
					who = m;
					break;
				}

			if (who < 0)
				return false;

			var shop = shopCands[NextRandom(shopCands.Count)];
			AddTrip(who, Leg.ToShop, HourTick(10 + Hash(hh, Today, 87) % 9, who, 88), shop.Id);
			return true;
		}

		void RunScheduledTrips(int t)
		{
			if (tripHead == null)
				return;

			var head = tripHead[t];
			if (head < 0)
				return;

			tripHead[t] = -1;
			var budget = info.MaxTripRequestsPerTick;
			var nextSlot = (t + 1) % tripHead.Length;
			var e = head;
			while (e >= 0)
			{
				var next = tripNext[e];
				var ci = tripCit[e];
				var valid = ci < citCount && (cits[ci].Flags & CitFlags.Alive) != 0 && cits[ci].BirthDay == tripBirth[e];
				if (valid)
				{
					if (budget <= 0)
					{
						// Over budget: retry on the next tick instead of dropping the plan.
						tripNext[e] = tripHead[nextSlot];
						tripHead[nextSlot] = e;
						e = next;
						continue;
					}

					budget--;
					StartLeg(ci, (Leg)tripKind[e], tripDest[e]);
				}

				tripNext[e] = tripFree;
				tripFree = e;
				e = next;
			}
		}

		void StartLeg(int ci, Leg kind, int dest)
		{
			var c = cits[ci];
			var hh = c.Household;
			if (hh < 0 || (c.Flags & CitFlags.Sick) != 0 || c.TripId != 0)
			{
				if (kind == Leg.ToShop && hh >= 0)
					BuyNow(hh);

				return;
			}

			var home = registry.Get(hhs[hh].Home);
			var target = kind == Leg.ToHome ? home : registry.Get(dest);
			var from = c.Loc != 0 ? registry.Get(c.Loc) : home;
			if (target == null || from == null || (kind == Leg.ToHome && c.Loc == 0))
			{
				ApplyArrival(ci, kind, dest);
				return;
			}

			var age = AgeOf(ci);
			var req = new TripRequest
			{
				Purpose = kind switch
				{
					Leg.ToWork => TripPurpose.Work,
					Leg.ToSchool => TripPurpose.School,
					Leg.ToShop => TripPurpose.Shopping,
					Leg.ToLeisure => TripPurpose.Leisure,
					_ => TripPurpose.GoingHome,
				},
				AllowedModes = age <= info.ChildMaxAge ? TravelModes.Walk | TravelModes.Transit : TravelModes.Walk | TravelModes.Car | TravelModes.Transit,
				AgeGroup = GroupOf(age),
				OwnerId = ci + 1,
				OriginRoad = from.AccessRoad,
				DestinationRoad = target.AccessRoad,
				OriginProperty = from.Id,
				DestinationProperty = target.Id,
				DepartTick = world.WorldTick,
			};

			tripsRequested++;
			tripsByHour[Math.Clamp(TickOfDay / TicksPerHourC, 0, 23)]++;
			var id = BookTrip(ref req, out var byTransit, hh);
			if (id <= 0)
			{
				tripsRefused++;
				ApplyArrival(ci, kind, dest);
				return;
			}

			tripsAccepted++;
			cits[ci].TripId = id;
			cits[ci].ByTransit = byTransit;
			cits[ci].Act = (byte)kind;
			cits[ci].TripDest = dest;
		}

		/// <summary>
		/// Mode choice: compares the car estimate (EstimateTravelTicks) with the transit quote (time + comfort penalty + fare as time)
		/// and, when neither is available but a taxi is idle nearby, a taxi. Transit and taxi are booked at the planner, the rest at traffic.
		/// </summary>
		int BookTrip(ref TripRequest req, out bool byTransit, int hh)
		{
			byTransit = false;
			var hasCar = req.AgeGroup != AgeGroup.Child && hhs[hh].Cash > -info.DebtLimitCents;
			var car = hasCar ? traffic.EstimateTravelTicks(req.OriginRoad, req.DestinationRoad, TravelMode.Car) : -1;
			if (planner != null)
			{
				var q = planner.Quote(req.OriginRoad, req.DestinationRoad, req.AgeGroup);
				if (q.Available)
				{
					var transitCost = q.Ticks + q.ComfortPenalty + q.FareCents * info.FareTicksPerCent / 100;
					if (car < 0 || transitCost < car)
					{
						var tr = req;
						tr.AllowedModes = TravelModes.Transit;
						var jid = planner.StartJourney(tr, this);
						if (jid > 0)
						{
							byTransit = true;
							return jid;
						}
					}
				}
				else if (car < 0 && transitLayer != null && req.AgeGroup != AgeGroup.Child)
				{
					var tq = transitLayer.QuoteTaxi(req.OriginRoad, req.DestinationRoad, req.AgeGroup);
					if (tq.Available && hhs[hh].Cash > tq.FareCents)
					{
						var tr = req;
						tr.AllowedModes = TravelModes.Taxi;
						var jid = planner.StartJourney(tr, this);
						if (jid > 0)
						{
							byTransit = true;
							hhs[hh].Cash -= tq.FareCents;
							return jid;
						}
					}
				}
			}

			return traffic.RequestTrip(req, this);
		}

		/// <summary>Effect of reaching the destination of a leg (also applied instantly when no trip could be booked).</summary>
		void ApplyArrival(int ci, Leg kind, int dest)
		{
			var hh = cits[ci].Household;
			switch (kind)
			{
				case Leg.ToWork:
				case Leg.ToSchool:
					cits[ci].Loc = dest;
					break;
				case Leg.ToHome:
					cits[ci].Loc = 0;
					break;
				case Leg.ToShop:
					cits[ci].Loc = dest;
					if (hh >= 0)
						BuyNow(hh);

					AddTrip(ci, Leg.ToHome, TickOfDay + 60 + Hash(ci, Today, 89) % 100, 0);
					break;
				case Leg.ToLeisure:
					cits[ci].Loc = dest;
					cits[ci].Leisure = 0;
					cits[ci].Wellbeing = (byte)Math.Min(100, cits[ci].Wellbeing + 8);
					if (hh >= 0)
						SpendLeisure(hh, dest);

					AddTrip(ci, Leg.ToHome, TickOfDay + 100 + Hash(ci, Today, 90) % 100, 0);
					break;
			}
		}

		void ITripListener.OnTripArrived(in TripResult result)
		{
			var ci = result.OwnerId - 1;
			if (ci < 0 || ci >= citCount || (cits[ci].Flags & CitFlags.Alive) == 0 || cits[ci].TripId != result.TripId)
				return;

			tripsArrived++;
			var kind = (Leg)cits[ci].Act;
			var dest = cits[ci].TripDest;
			cits[ci].TripId = 0;
			cits[ci].ByTransit = false;
			if (kind == Leg.ToWork)
			{
				AverageCommuteTicks += (result.ArriveTick - result.DepartTick - AverageCommuteTicks) / 8;
				RecordCommute(ci, result.ArriveTick - result.DepartTick);
			}

			ApplyArrival(ci, kind, dest);
		}

		void ITripListener.OnTripFailed(int tripId, int ownerId, TripFailure reason)
		{
			var ci = ownerId - 1;
			if (ci < 0 || ci >= citCount || (cits[ci].Flags & CitFlags.Alive) == 0 || cits[ci].TripId != tripId)
				return;

			tripsFailed++;
			var kind = (Leg)cits[ci].Act;
			cits[ci].TripId = 0;
			cits[ci].ByTransit = false;

			// The citizen could not travel: shopping is done abstractly, anything else is skipped (stays where it is).
			if (kind == Leg.ToShop && cits[ci].Household >= 0)
				BuyNow(cits[ci].Household);
		}

		CitizenActivity ActivityOf(int ci)
		{
			var c = cits[ci];
			if ((c.Flags & CitFlags.Jailed) != 0)
				return CitizenActivity.Prison;

			if (c.TripId != 0)
				return CitizenActivity.Travelling;

			if ((c.Flags & CitFlags.Sick) != 0)
				return (c.Flags & CitFlags.Treated) != 0 ? CitizenActivity.Hospital : CitizenActivity.Home;

			if (c.Household >= 0 && hhs[c.Household].Home == 0)
				return CitizenActivity.Moving;

			if (UseTrips && tripsAccepted > 0)
			{
				if (c.Loc == 0)
					return CitizenActivity.Home;

				if (c.Loc == c.Work)
					return (c.Flags & CitFlags.Student) != 0 ? CitizenActivity.Studying : CitizenActivity.Working;

				return (Leg)c.Act == Leg.ToShop ? CitizenActivity.Shopping : CitizenActivity.Leisure;
			}

			var hour = TickOfDay / TicksPerHourC;
			if ((c.Flags & CitFlags.Worker) != 0 && hour >= info.WorkStartHour && hour < info.WorkStartHour + info.WorkHours)
				return CitizenActivity.Working;

			if ((c.Flags & CitFlags.Student) != 0 && hour >= info.SchoolStartHour && hour < info.SchoolStartHour + info.SchoolHours)
				return CitizenActivity.Studying;

			return CitizenActivity.Home;
		}
	}
}
