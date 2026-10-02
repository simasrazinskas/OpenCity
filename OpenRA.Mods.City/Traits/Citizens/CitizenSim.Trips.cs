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
	// Daily planning stays spread across citizen buckets. Plans have absolute world ticks, so a ring slot can
	// contain tomorrow's departure without accidentally running it today. Visits finish before later legs start.
	public partial class CitizenSim
	{
		enum LegStart : byte { Done, Retry, Deferred }

		enum Leg : byte { None = 0, ToWork, ToSchool, ToHome, ToShop, ToLeisure }

		int[] tripHead;
		int[] tripNext = new int[1024];
		int[] tripCit = new int[1024];
		int[] tripGeneration = new int[1024];
		int[] tripDest = new int[1024];
		int[] tripOrigin = new int[1024];
		int[] tripDue = new int[1024];
		int[] tripDeadline = new int[1024];
		int[] tripHold = new int[1024];
		byte[] tripKind = new byte[1024];
		byte[] tripAttempts = new byte[1024];
		int tripFree = -1;
		int tripPoolCount;

		readonly List<Property> shopCands = [];
		readonly List<Property> leisureCands = [];
		readonly int[] tripsByHour = new int[24];
		long tripsRequested, tripsAccepted, tripsRefused, tripsFailed, tripsArrived;

		/// <summary>Average commute (ticks) of work trips.</summary>
		public int AverageCommuteTicks { get; private set; }

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
			cits[ci].BusyUntil = 0;
		}

		bool UseTrips => traffic != null;

		int TicksPerHourC => Math.Max(1, TicksPerDay / 24);

		int MinuteTicks(int minutes) => clock?.MinutesToTicks(minutes) ?? CityTime.MinutesToTicks(minutes, TicksPerHourC);

		int NextDayStart => world.WorldTick - TickOfDay + TicksPerDay;

		void EnsureTripRing()
		{
			if (tripHead != null && tripHead.Length == TicksPerDay)
				return;

			tripHead = new int[TicksPerDay];
			Array.Fill(tripHead, -1);
		}

		void QueueTrip(int e, int due)
		{
			tripDue[e] = due;
			var slot = (int)(((long)TickOfDay + due - world.WorldTick) % tripHead.Length);
			if (slot < 0)
				slot += tripHead.Length;

			tripNext[e] = tripHead[slot];
			tripHead[slot] = e;
		}

		void AddTrip(int ci, Leg kind, int due, int dest, int deadline, int holdUntil = 0, int origin = 0, byte attempts = 0)
		{
			if (deadline < world.WorldTick)
				return;

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
					Array.Resize(ref tripGeneration, n);
					Array.Resize(ref tripDest, n);
					Array.Resize(ref tripOrigin, n);
					Array.Resize(ref tripDue, n);
					Array.Resize(ref tripDeadline, n);
					Array.Resize(ref tripHold, n);
					Array.Resize(ref tripKind, n);
					Array.Resize(ref tripAttempts, n);
				}

				e = tripPoolCount++;
			}

			tripCit[e] = ci;
			tripGeneration[e] = cits[ci].Generation;
			tripKind[e] = (byte)kind;
			tripDest[e] = dest;
			tripOrigin[e] = origin;
			tripDeadline[e] = deadline;
			tripHold[e] = holdUntil;
			tripAttempts[e] = attempts;
			QueueTrip(e, Math.Max(world.WorldTick + 1, due));
		}

		int FreeHour(int ci)
		{
			var c = cits[ci];
			if ((c.Flags & CitFlags.Worker) != 0 && c.Work != 0)
				return info.WorkStartHour + info.WorkHours;

			if ((c.Flags & CitFlags.Student) != 0 && c.Work != 0)
				return info.SchoolStartHour + info.SchoolHours;

			return 9;
		}

		int EstimateTrip(int ci, Property from, Property target)
		{
			if (from == null || target == null || !from.HasRoadAccess || !target.HasRoadAccess)
				return -1;

			if (from.Id == target.Id)
				return 0;

			var walk = traffic.EstimateTravelTicks(from.AccessRoad, target.AccessRoad, TravelMode.Walk);
			if (walk == -2)
				return -2;

			if (walk >= 0 && ManhattanRoad(from, target) <= info.WalkPreferenceCells)
				return walk;

			var age = AgeOf(ci);
			var hh = cits[ci].Household;
			var car = age > info.ChildMaxAge && hh >= 0 && hhs[hh].Cash > -info.DebtLimitCents
				? traffic.EstimateTravelTicks(from.AccessRoad, target.AccessRoad, TravelMode.Car) : -1;
			if (car == -2)
				return -2;

			var best = car < 0 ? walk : walk < 0 ? car : Math.Min(walk, car);
			if (planner != null)
			{
				var quote = planner.Quote(from.AccessRoad, target.AccessRoad, GroupOf(age));
				if (quote.Available && (best < 0 || quote.Ticks < best))
					best = quote.Ticks;
			}

			return best;
		}

		void PlanDay(int ci, int age, int today)
		{
			if (!UseTrips)
				return;

			var c = cits[ci];
			if ((c.Flags & (CitFlags.Sick | CitFlags.Jailed)) != 0 || c.Household < 0 || hhs[c.Household].Home == 0)
				return;

			// First-time citizens can still travel today; later daily buckets prepare tomorrow in advance.
			if (c.PlannedThroughDay < today)
				PlanItinerary(ci, age, today, world.WorldTick - TickOfDay);

			if (c.PlannedThroughDay < today + 1)
				PlanItinerary(ci, age, today + 1, NextDayStart);

			cits[ci].PlannedThroughDay = today + 1;
		}

		void PlanItinerary(int ci, int age, int day, int dayStart)
		{
			var c = cits[ci];
			var home = registry.Get(hhs[c.Household].Home);
			if (c.Loc != 0 && c.TripId == 0 && day == Today)
				AddTrip(ci, Leg.ToHome, world.WorldTick + 1, 0, world.WorldTick + TicksPerHourC);

			var working = (c.Flags & CitFlags.Worker) != 0;
			if ((c.Flags & (CitFlags.Worker | CitFlags.Student)) != 0 && c.Work != 0)
			{
				var work = registry.Get(c.Work);
				var travel = EstimateTrip(ci, home, work);
				if (travel == -2)
				{
					var mode = age <= info.ChildMaxAge ? TravelMode.Walk : TravelMode.Car;
					var perCell = (traffic as ITravelTimeCalibration)?.FreeFlowTicksPerCell(mode) ?? (mode == TravelMode.Walk ? 288 : 36);
					travel = Math.Max(MinuteTicks(60), ManhattanRoad(home, work) * perCell);
				}

				if (travel >= 0)
				{
					var start = working ? info.WorkStartHour : info.SchoolStartHour;
					var hours = working ? info.WorkHours : info.SchoolHours;
					var end = dayStart + (start + hours) * TicksPerHourC;
					var early = info.DepartJitterTicks > 0 ? Hash(ci, day, 80) % info.DepartJitterTicks : 0;
					var departure = dayStart + start * TicksPerHourC - travel - early;
					AddTrip(ci, working ? Leg.ToWork : Leg.ToSchool, departure, c.Work, end, end);
					AddTrip(ci, Leg.ToHome, end, 0, end + TicksPerDay, origin: c.Work);
				}
			}

			if (age > info.ChildMaxAge / 2 && leisureCands.Count > 0
				&& Hash(ci, day, 84) % 100 < Math.Min(95, info.LeisureTripPercent + c.Leisure / 4))
			{
				var p = PickLocalDestination(ci, home, leisureCands, true, day);
				if (p != null)
				{
					var departure = dayStart + Math.Max(FreeHour(ci) + 2, 11) * TicksPerHourC
						+ Hash(ci, day, 85) % Math.Max(1, 2 * TicksPerHourC);
					AddTrip(ci, Leg.ToLeisure, departure, p.Id, dayStart + info.OutingEndHour * TicksPerHourC);
				}
			}
		}

		bool PlanShopTrip(int hh)
		{
			if (!UseTrips || shopCands.Count == 0)
				return false;

			var who = -1;
			var firstFree = int.MaxValue;
			for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
				if (AgeOf(m) > info.TeenMaxAge && (cits[m].Flags & (CitFlags.Sick | CitFlags.Jailed)) == 0 && FreeHour(m) < firstFree)
				{
					who = m;
					firstFree = FreeHour(m);
				}

			if (who < 0)
				return false;

			var home = registry.Get(hhs[hh].Home);
			var shop = PickLocalDestination(who, home, shopCands, false, Today + 1);
			if (shop == null)
				return false;

			var start = world.WorldTick - TickOfDay;
			if (TickOfDay >= info.OutingEndHour * TicksPerHourC)
				start += TicksPerDay;

			var departure = start + firstFree * TicksPerHourC + MinuteTicks(30)
				+ Hash(hh, Today + 1, 87) % Math.Max(1, 2 * TicksPerHourC);
			AddTrip(who, Leg.ToShop, departure, shop.Id, start + info.OutingEndHour * TicksPerHourC);
			return true;
		}

		Property PickLocalDestination(int ci, Property home, List<Property> candidates, bool leisure, int day)
		{
			if (home == null || candidates.Count == 0 || (leisure && climate?.Weather == CityWeather.Storm))
				return null;

			var badWeather = climate != null && (climate.Weather == CityWeather.Rain || climate.Weather == CityWeather.Snow || climate.TemperatureX10 < 20);
			Property best = null;
			Property deferred = null;
			var bestScore = long.MaxValue;
			var samples = Math.Min(candidates.Count, Math.Max(1, info.SearchSamples));
			var offset = Hash(ci, day, leisure ? 86 : 88) % candidates.Count;
			for (var s = 0; s < samples; s++)
			{
				var p = candidates[(offset + s) % candidates.Count];
				if (!Alive(p) || !p.Operational || p.Id == home.Id)
					continue;

				var travel = EstimateTrip(ci, home, p);
				if (travel == -2)
				{
					deferred ??= p;
					continue;
				}

				if (travel < 0 || travel > TicksPerHourC * 2)
					continue;

				var park = !IsShop(p);
				var score = (long)travel + (leisure && park ? (badWeather ? MinuteTicks(120) : -MinuteTicks(10)) : 0);
				if (score < bestScore)
				{
					best = p;
					bestScore = score;
				}
			}

			return best ?? deferred;
		}

		void RunScheduledTrips(int t)
		{
			if (tripHead == null)
				return;

			var e = tripHead[t];
			tripHead[t] = -1;
			var budget = Math.Max(1, info.MaxTripRequestsPerTick);
			var now = world.WorldTick;
			while (e >= 0)
			{
				var next = tripNext[e];
				var ci = tripCit[e];
				var valid = ci < citCount && (cits[ci].Flags & CitFlags.Alive) != 0 && cits[ci].Generation == tripGeneration[e];
				if (valid && tripDue[e] > now)
				{
					QueueTrip(e, tripDue[e]);
					e = next;
					continue;
				}

				if (valid && now <= tripDeadline[e])
				{
					var c = cits[ci];
					if (c.TripId != 0 || c.BusyUntil > now || budget <= 0)
					{
						// Wait for this citizen's earlier leg/visit, instead of dropping an overlapping plan.
						QueueTrip(e, Math.Max(now + 1, c.BusyUntil));
						e = next;
						continue;
					}

					budget--;
					var started = StartLeg(ci, (Leg)tripKind[e], tripDest[e], tripHold[e], tripOrigin[e], tripAttempts[e]);
					if (started == LegStart.Deferred || (started == LegStart.Retry && ++tripAttempts[e] < 3))
					{
						QueueTrip(e, now + (started == LegStart.Deferred ? 1 : Math.Max(1, MinuteTicks(info.TripRetryMinutes))));
						e = next;
						continue;
					}
				}

				tripNext[e] = tripFree;
				tripFree = e;
				e = next;
			}
		}

		LegStart StartLeg(int ci, Leg kind, int dest, int holdUntil = 0, int origin = 0, byte attempts = 0)
		{
			var c = cits[ci];
			var hh = c.Household;
			if (hh < 0 || hhs[hh].Home == 0 || (c.Flags & (CitFlags.Sick | CitFlags.Jailed)) != 0)
				return LegStart.Done;

			if ((kind == Leg.ToWork || kind == Leg.ToSchool) && c.Work != dest)
				return LegStart.Done;

			// A return from an old visit must not cut short a newer visit at another destination.
			if (kind == Leg.ToHome && origin != 0 && c.Loc != origin)
				return LegStart.Done;

			var home = registry.Get(hhs[hh].Home);
			var target = kind == Leg.ToHome ? home : registry.Get(dest);
			var from = c.Loc != 0 ? registry.Get(c.Loc) : home;
			if (from == null && c.Loc != 0 && c.HasLastRoad)
				from = new Property
				{
					Id = c.Loc,
					AccessRoad = c.LastRoad,
					HasRoadAccess = true,
					Operational = true
				};
			if (target == null || from == null || !target.Operational || !target.HasRoadAccess)
				return LegStart.Done;

			if (kind == Leg.ToWork || kind == Leg.ToSchool)
			{
				var travel = EstimateTrip(ci, from, target);
				if (travel == -2)
					return LegStart.Deferred;

				if (travel < 0 || (holdUntil > 0 && world.WorldTick + travel >= holdUntil))
					return LegStart.Done;
			}

			if (target.Id == from.Id)
			{
				cits[ci].BusyUntil = holdUntil;
				ApplyArrival(ci, kind, dest);
				return LegStart.Done;
			}

			if (kind == Leg.ToShop || kind == Leg.ToLeisure)
			{
				var travel = EstimateTrip(ci, from, target);
				var back = EstimateTrip(ci, target, home);
				if (travel == -2 || back == -2)
					return LegStart.Deferred;

				var stay = MinuteTicks(kind == Leg.ToShop ? info.ShoppingMinutes : info.LeisureMinutes);
				if (travel < 0 || back < 0 || TickOfDay + travel + stay + back > TicksPerDay)
					return LegStart.Done;
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

			var id = BookTrip(ref req, out var byTransit, hh);
			if (id == -2)
				return LegStart.Deferred;

			tripsRequested++;
			tripsByHour[Math.Clamp(TickOfDay / TicksPerHourC, 0, 23)]++;
			if (id <= 0)
			{
				tripsRefused++;
				return LegStart.Retry;
			}

			tripsAccepted++;
			cits[ci].TripId = id;
			cits[ci].ByTransit = byTransit;
			cits[ci].Act = (byte)kind;
			cits[ci].TripDest = dest;
			cits[ci].TripDestinationRoad = req.DestinationRoad;
			cits[ci].BusyUntil = holdUntil;
			cits[ci].TripRetries = attempts;
			return LegStart.Done;
		}

		/// <summary>
		/// Mode choice: compares the car estimate (EstimateTravelTicks) with the transit quote (time + comfort penalty + fare as time)
		/// and, when neither is available but a taxi is idle nearby, a taxi. Transit and taxi are booked at the planner, the rest at traffic.
		/// </summary>
		int BookTrip(ref TripRequest req, out bool byTransit, int hh)
		{
			byTransit = false;
			var hasCar = req.AgeGroup != AgeGroup.Child && hhs[hh].Cash > -info.DebtLimitCents;
			var walk = traffic.EstimateTravelTicks(req.OriginRoad, req.DestinationRoad, TravelMode.Walk);
			if (walk == -2)
				return -2;

			var distance = Math.Abs(req.OriginRoad.X - req.DestinationRoad.X) + Math.Abs(req.OriginRoad.Y - req.DestinationRoad.Y);
			if (walk >= 0 && distance <= info.WalkPreferenceCells)
			{
				req.AllowedModes = TravelModes.Walk;
				return traffic.RequestTrip(req, this);
			}

			var car = hasCar ? traffic.EstimateTravelTicks(req.OriginRoad, req.DestinationRoad, TravelMode.Car) : -1;
			if (car == -2)
				return -2;

			var fastest = car < 0 ? walk : walk < 0 ? car : Math.Min(car, walk);
			if (planner != null)
			{
				var q = planner.Quote(req.OriginRoad, req.DestinationRoad, req.AgeGroup);
				if (q.Available)
				{
					var transitCost = q.Ticks + q.ComfortPenalty + q.FareCents * info.FareTicksPerCent / 100;
					if (fastest < 0 || transitCost < fastest)
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
				else if (fastest < 0 && transitLayer != null && req.AgeGroup != AgeGroup.Child)
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

			if (walk >= 0 && (car < 0 || walk <= car))
				req.AllowedModes = TravelModes.Walk;
			else
				req.AllowedModes = hasCar ? TravelModes.Car : TravelModes.Walk;

			return traffic.RequestTrip(req, this);
		}

		/// <summary>Apply destination effects only after actual arrival (or when already at that property).</summary>
		void ApplyArrival(int ci, Leg kind, int dest)
		{
			var hh = cits[ci].Household;
			var reached = registry.Get(kind == Leg.ToHome && hh >= 0 ? hhs[hh].Home : dest);
			if (reached != null)
			{
				cits[ci].LastRoad = reached.AccessRoad;
				cits[ci].HasLastRoad = true;
			}

			// Arrival at a demolished destination still reaches its road, but grants no visit benefits.
			if (kind != Leg.ToHome && (reached == null || !reached.Operational))
			{
				cits[ci].Loc = dest;
				cits[ci].BusyUntil = 0;
				AddTrip(ci, Leg.ToHome, world.WorldTick + 1, 0, world.WorldTick + TicksPerDay, origin: dest);
				return;
			}

			cits[ci].Act = (byte)kind;
			switch (kind)
			{
				case Leg.ToWork:
				case Leg.ToSchool:
					cits[ci].Loc = dest;
					break;
				case Leg.ToHome:
					cits[ci].Loc = 0;
					cits[ci].BusyUntil = 0;
					break;
				case Leg.ToShop:
					cits[ci].Loc = dest;
					if (hh >= 0)
						BuyNow(hh, dest);

					FinishVisit(ci, dest, info.ShoppingMinutes);
					break;
				case Leg.ToLeisure:
					cits[ci].Loc = dest;
					cits[ci].Leisure = 0;
					cits[ci].Wellbeing = (byte)Math.Min(100, cits[ci].Wellbeing + 8);
					if (hh >= 0)
						SpendLeisure(hh, dest);

					FinishVisit(ci, dest, info.LeisureMinutes);
					break;
			}
		}

		void FinishVisit(int ci, int dest, int minutes)
		{
			var until = world.WorldTick + Math.Max(1, MinuteTicks(minutes));
			cits[ci].BusyUntil = until;
			AddTrip(ci, Leg.ToHome, until, 0, until + TicksPerDay, origin: dest);
		}

		void ITripListener.OnTripArrived(in TripResult result)
		{
			var ci = result.OwnerId - 1;
			if (ci < 0 || ci >= citCount || (cits[ci].Flags & CitFlags.Alive) == 0 || cits[ci].TripId != result.TripId)
				return;

			tripsArrived++;
			cits[ci].LastRoad = cits[ci].TripDestinationRoad;
			cits[ci].HasLastRoad = true;
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

			// Keep the last reached property. A failed trip does not deliver goods or leisure benefits.
			cits[ci].BusyUntil = 0;
			if (kind == Leg.ToHome && reason != TripFailure.Cancelled && cits[ci].TripRetries < 2)
				AddTrip(ci, Leg.ToHome, world.WorldTick + Math.Max(1, MinuteTicks(info.TripRetryMinutes)), 0,
					world.WorldTick + TicksPerHourC, attempts: (byte)(cits[ci].TripRetries + 1));
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

			if (UseTrips)
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
