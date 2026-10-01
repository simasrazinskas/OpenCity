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
	// Journey planning (ITransitPlanner), the passenger event queue and the aggregate passenger sampler.
	public sealed partial class TransitLayer
	{
		const byte EventEnqueue = 0;
		const byte EventComplete = 1;

		struct Plan
		{
			public bool Valid;
			public int Cost, Ticks, Fare, Transfers, Comfort;
			public int LineA, BoardA, AlightA, LegsA;
			public int LineB, BoardB, AlightB, LegsB;
			public int WalkIn, WalkOut, TransferWalk;
		}

		readonly List<TransitEvent> heap = [];
		readonly int[] fromIds = new int[3], fromDist = new int[3], toIds = new int[3], toDist = new int[3];
		readonly int[] farePending = new int[8];

		// ---- event heap ----
		void PushEvent(int tick, byte kind, int stopId, PaxGroup g)
		{
			var e = new TransitEvent { Tick = tick, Seq = nextEventSeq++, Kind = kind, StopId = stopId, Group = g };
			heap.Add(e);
			var i = heap.Count - 1;
			while (i > 0)
			{
				var parent = (i - 1) / 2;
				if (!Before(heap[i], heap[parent]))
					break;

				(heap[i], heap[parent]) = (heap[parent], heap[i]);
				i = parent;
			}
		}

		static bool Before(in TransitEvent a, in TransitEvent b)
		{
			return a.Tick < b.Tick || (a.Tick == b.Tick && a.Seq < b.Seq);
		}

		TransitEvent PopEvent()
		{
			var top = heap[0];
			var last = heap[^1];
			heap.RemoveAt(heap.Count - 1);
			if (heap.Count > 0)
			{
				heap[0] = last;
				var i = 0;
				while (true)
				{
					var l = i * 2 + 1;
					var r = l + 1;
					var m = i;
					if (l < heap.Count && Before(heap[l], heap[m]))
						m = l;

					if (r < heap.Count && Before(heap[r], heap[m]))
						m = r;

					if (m == i)
						break;

					(heap[i], heap[m]) = (heap[m], heap[i]);
					i = m;
				}
			}

			return top;
		}

		void ProcessEvents(int now)
		{
			while (heap.Count > 0 && heap[0].Tick <= now)
			{
				var e = PopEvent();
				var g = e.Group;
				if (e.Kind == EventEnqueue)
				{
					var stop = GetStop(e.StopId);
					if (stop == null || GetLine(g.LineId) == null)
					{
						FailGroup(g, TripFailure.NoRoute);
						continue;
					}

					if (stop.WaitingCount + g.Count > Info.MaxStopQueue)
					{
						stop.GaveUpThisMonth += g.Count;
						totalGaveUp += g.Count;
						FailGroup(g, TripFailure.Stuck);
						continue;
					}

					g.WaitStart = now;
					stop.Waiting.Add(g);
					stop.WaitingCount += g.Count;
				}
				else
				{
					TotalJourneys += g.Count;
					if (g.Listener != null)
					{
						var result = new TripResult
						{
							TripId = g.JourneyId,
							OwnerId = g.OwnerId,
							Mode = TravelMode.Transit,
							DepartTick = g.StartTick,
							ArriveTick = now,
							CostCents = g.FarePaid,
						};
						g.Listener.OnTripArrived(result);
					}
				}
			}
		}

		void SnapshotStops()
		{
			for (var i = 0; i < stops.Count; i++)
				stops[i].CrowdSnapshot = stops[i].WaitingCount;
		}

		void GiveUpWaiting(int now)
		{
			for (var s = 0; s < stops.Count; s++)
			{
				var stop = stops[s];
				while (stop.Waiting.Count > 0 && now - stop.Waiting[0].WaitStart > Info.GiveUpTicks)
				{
					var g = stop.Waiting[0];
					stop.Waiting.RemoveAt(0);
					stop.WaitingCount -= g.Count;
					stop.GaveUpThisMonth += g.Count;
					totalGaveUp += g.Count;
					var line = GetLine(g.LineId);
					if (line != null)
						line.ThisMonth.GaveUp += g.Count;

					FailGroup(g, TripFailure.Cancelled);
				}
			}
		}

		// ---- planning ----
		static int AgeIndex(AgeGroup age) => Math.Min(3, (int)age);

		int FindNearStops(CPos cell, int[] ids, int[] dists)
		{
			var count = 0;
			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				if (s.Mode == TransitMode.Taxi || s.LineIds.Count == 0)
					continue;

				var d = Math.Abs(s.Cell.X - cell.X) + Math.Abs(s.Cell.Y - cell.Y);
				if (d > Info.WalkRadius)
					continue;

				// Keep the three nearest, ties by stop id (stops are iterated by ascending id).
				var at = count;
				while (at > 0 && dists[at - 1] > d)
					at--;

				if (at >= ids.Length)
					continue;

				var last = Math.Min(count, ids.Length - 1);
				for (var k = last; k > at; k--)
				{
					ids[k] = ids[k - 1];
					dists[k] = dists[k - 1];
				}

				ids[at] = s.Id;
				dists[at] = d;
				if (count < ids.Length)
					count++;
			}

			return count;
		}

		bool LineRuns(TransitLine line)
		{
			var n = line.StopIds.Count;
			if (line.Broken || n < 2 || line.RideTicks.Length != n * n)
				return false;

			return line.Vehicles.Count > 0 || (line.TargetVehicles > 0 && (!NeedsDepot(line.Mode) || HasDepot(line.Mode)));
		}

		int LineWait(TransitLine line)
		{
			var headway = line.CycleTicks / Math.Max(1, line.TargetVehicles);
			if (line.Vehicles.Count > 0)
				headway = line.CycleTicks / line.Vehicles.Count;

			return Math.Min(Info.MaxPlannedWait, Math.Max(headway / 2, line.ObservedWait));
		}

		int ComfortOf(TransitStop boardStop, TransitLine line, int age)
		{
			var c = line.Mode == TransitMode.Metro ? -20 : (line.Mode == TransitMode.Tram || line.Mode == TransitMode.Train ? -10 : 0);
			if (boardStop.CrowdSnapshot > 30)
				c += 20;

			if (line.LastMonth.UsagePercent > 80 || line.ThisMonth.UsagePercent > 80)
				c += 40;

			return c * Info.ComfortWeight[age] / 100;
		}

		bool TryPlan(CPos from, CPos to, AgeGroup ageGroup, out Plan best)
		{
			best = default;
			if (lines.Count == 0)
				return false;

			var age = AgeIndex(ageGroup);
			var nFrom = FindNearStops(from, fromIds, fromDist);
			var nTo = FindNearStops(to, toIds, toDist);
			if (nFrom == 0 || nTo == 0)
				return false;

			var walk = Info.WalkTicksPerCell;
			var moneyWeight = Info.MoneyWeight[age];

			// Direct rides.
			for (var oi = 0; oi < nFrom; oi++)
			{
				var o = GetStop(fromIds[oi]);
				for (var li = 0; li < o.LineIds.Count; li++)
				{
					var line = GetLine(o.LineIds[li]);
					if (line == null || !LineRuns(line))
						continue;

					var n = line.StopIds.Count;
					var ia = StopIndex(line, o.Id);
					for (var di = 0; di < nTo; di++)
					{
						var ib = StopIndex(line, toIds[di]);
						if (ib < 0 || ib == ia)
							continue;

						var ride = line.RideTicks[ia * n + ib];
						if (ride < 0)
							continue;

						var time = (fromDist[oi] + toDist[di]) * walk + LineWait(line) + ride;
						var comfort = ComfortOf(o, line, age);
						var ticket = TicketFor(line, o.Cell);
						var cost = time + comfort + ticket * moneyWeight / 200;
						if (best.Valid && cost >= best.Cost)
							continue;

						best = new Plan
						{
							Valid = true,
							Cost = cost,
							Ticks = time,
							Fare = ticket,
							Comfort = comfort,
							LineA = line.Id,
							BoardA = o.Id,
							AlightA = toIds[di],
							LegsA = line.RideLegs[ia * n + ib],
							WalkIn = fromDist[oi] * walk,
							WalkOut = toDist[di] * walk,
						};
					}
				}
			}

			if (best.Valid)
				return true;

			// One transfer: ride A to t1, walk to a nearby stop t2, ride B to the destination stop.
			for (var oi = 0; oi < nFrom; oi++)
			{
				var o = GetStop(fromIds[oi]);
				for (var li = 0; li < o.LineIds.Count; li++)
				{
					var lineA = GetLine(o.LineIds[li]);
					if (lineA == null || !LineRuns(lineA))
						continue;

					var nA = lineA.StopIds.Count;
					var ia = StopIndex(lineA, o.Id);
					for (var k = 0; k < nA; k++)
					{
						var rideA = k == ia ? -1 : lineA.RideTicks[ia * nA + k];
						if (rideA < 0)
							continue;

						var t1 = GetStop(lineA.StopIds[k]);
						if (t1 == null)
							continue;

						// m = -1 is the same stop (a shared stop of two lines), the rest are nearby stops.
						for (var m = -1; m < t1.NearIds.Count; m++)
						{
							var t2 = m < 0 ? t1 : GetStop(t1.NearIds[m]);
							if (t2 == null)
								continue;

							var transferWalk = (Math.Abs(t1.Cell.X - t2.Cell.X) + Math.Abs(t1.Cell.Y - t2.Cell.Y)) * walk;
							for (var lb = 0; lb < t2.LineIds.Count; lb++)
							{
								var lineB = GetLine(t2.LineIds[lb]);
								if (lineB == null || lineB == lineA || !LineRuns(lineB))
									continue;

								var nB = lineB.StopIds.Count;
								var ib0 = StopIndex(lineB, t2.Id);
								for (var di = 0; di < nTo; di++)
								{
									var ib = StopIndex(lineB, toIds[di]);
									if (ib < 0 || ib == ib0)
										continue;

									var rideB = lineB.RideTicks[ib0 * nB + ib];
									if (rideB < 0)
										continue;

									var time = (fromDist[oi] + toDist[di]) * walk + LineWait(lineA) + rideA + transferWalk + LineWait(lineB) + rideB;
									var comfort = ComfortOf(o, lineA, age) + ComfortOf(t2, lineB, age);
									var fare = TicketFor(lineA, o.Cell) + TicketFor(lineB, t2.Cell);
									var cost = time + comfort + Info.TransferPenalty + fare * moneyWeight / 200;
									if (best.Valid && cost >= best.Cost)
										continue;

									best = new Plan
									{
										Valid = true,
										Cost = cost,
										Ticks = time,
										Fare = fare,
										Transfers = 1,
										Comfort = comfort,
										LineA = lineA.Id,
										BoardA = o.Id,
										AlightA = t1.Id,
										LegsA = lineA.RideLegs[ia * nA + k],
										LineB = lineB.Id,
										BoardB = t2.Id,
										AlightB = toIds[di],
										LegsB = lineB.RideLegs[ib0 * nB + ib],
										WalkIn = fromDist[oi] * walk,
										WalkOut = toDist[di] * walk,
										TransferWalk = transferWalk,
									};
								}
							}
						}
					}
				}
			}

			return best.Valid;
		}

		/// <summary>Quote for riding public transport between two road cells. Pure function of the current state (safe to call from UI).</summary>
		public TransitQuote Quote(CPos fromRoad, CPos toRoad, AgeGroup age)
		{
			if (!TryPlan(fromRoad, toRoad, age, out var plan))
				return default;

			return new TransitQuote
			{
				Available = true,
				Ticks = plan.Ticks,
				FareCents = plan.Fare,
				Transfers = plan.Transfers,
				ComfortPenalty = plan.Comfort,
			};
		}

		/// <summary>Book a journey: the passenger walks to the first stop, waits, rides (optionally transfers) and walks on. The listener gets OnTripArrived or OnTripFailed.</summary>
		public int StartJourney(in TripRequest request, ITripListener listener)
		{
			if (roads == null)
				return 0;

			var now = world.WorldTick;
			externalJourneyTick = now;
			var depart = Math.Max(now, request.DepartTick);
			var allowed = request.AllowedModes;
			if (allowed == TravelModes.None || (allowed & TravelModes.Transit) != 0)
			{
				if (TryPlan(request.OriginRoad, request.DestinationRoad, request.AgeGroup, out var plan))
				{
					var g = new PaxGroup
					{
						JourneyId = nextJourneyId++,
						Listener = listener,
						OwnerId = request.OwnerId,
						Count = 1,
						LineId = plan.LineA,
						AlightStopId = plan.AlightA,
						RideLegs = plan.LegsA,
						Line2 = plan.LineB,
						Board2Stop = plan.BoardB,
						Alight2Stop = plan.AlightB,
						RideLegs2 = plan.LegsB,
						TransferWalkTicks = plan.TransferWalk,
						WalkOutTicks = plan.WalkOut,
						StartTick = depart,
					};
					PushEvent(depart + Math.Max(1, plan.WalkIn), EventEnqueue, plan.BoardA, g);
					return g.JourneyId;
				}
			}

			if ((allowed & TravelModes.Taxi) != 0)
				return StartTaxiJourney(request, listener, depart);

			return 0;
		}

		/// <summary>Cancel a booked journey that has not completed (waiting passengers are removed; riders are dropped at the next stop).</summary>
		public void CancelJourney(int journeyId)
		{
			for (var s = 0; s < stops.Count; s++)
			{
				var stop = stops[s];
				for (var i = 0; i < stop.Waiting.Count; i++)
				{
					if (stop.Waiting[i].JourneyId != journeyId)
						continue;

					stop.WaitingCount -= stop.Waiting[i].Count;
					stop.Waiting.RemoveAt(i);
					return;
				}
			}

			for (var i = 0; i < heap.Count; i++)
			{
				if (heap[i].Group.JourneyId != journeyId)
					continue;

				var e = heap[i];
				e.Group.Listener = null;
				heap[i] = e;
			}

			for (var v = 0; v < vehicles.Count; v++)
			{
				for (var i = 0; i < vehicles[v].Pax.Count; i++)
				{
					if (vehicles[v].Pax[i].JourneyId != journeyId)
						continue;

					var g = vehicles[v].Pax[i];
					g.Listener = null;
					vehicles[v].Pax[i] = g;
				}
			}
		}

		// ---- aggregate sampler ----
		int catchmentTick = -1000;

		void UpdateCatchments(int now)
		{
			if (registry == null || stops.Count == 0 || now - catchmentTick < 250)
				return;

			catchmentTick = now;
			for (var i = 0; i < stops.Count; i++)
				stops[i].Catchment = 0;

			var all = registry.All;
			for (var p = 0; p < all.Count; p++)
			{
				var prop = all[p];
				if (prop.Kind != PropertyKind.Residential && prop.Kind != PropertyKind.None)
					continue;

				var residents = prop.Residents;
				if (residents <= 0 && prop.Actor != null)
					residents = prop.Actor.TraitOrDefault<CityBuilding>()?.Residents ?? 0;

				if (residents <= 0)
					continue;

				var cx = prop.Origin.X + prop.Width / 2;
				var cy = prop.Origin.Y + prop.Depth / 2;
				var covering = 0;
				for (var i = 0; i < stops.Count; i++)
					if (stops[i].Mode != TransitMode.Taxi && stops[i].LineIds.Count > 0 && Math.Abs(stops[i].Cell.X - cx) + Math.Abs(stops[i].Cell.Y - cy) <= Info.WalkRadius)
						covering++;

				if (covering == 0)
					continue;

				for (var i = 0; i < stops.Count; i++)
					if (stops[i].Mode != TransitMode.Taxi && stops[i].LineIds.Count > 0 && Math.Abs(stops[i].Cell.X - cx) + Math.Abs(stops[i].Cell.Y - cy) <= Info.WalkRadius)
						stops[i].Catchment += Math.Max(1, residents / covering);
			}
		}

		void SampleAggregatePassengers(int now)
		{
			// Once the citizen simulation books real journeys, the aggregate sampler stays quiet.
			if (now - externalJourneyTick < 4800 || stops.Count == 0)
				return;

			UpdateCatchments(now);
			var ticksPerDay = clock?.TicksPerDay ?? 2400;
			var pulsesPerDay = Math.Max(1, ticksPerDay / 25);
			for (var s = 0; s < stops.Count; s++)
			{
				var stop = stops[s];
				if (stop.Mode == TransitMode.Taxi || stop.Catchment <= 0)
					continue;

				// Quickest line serving the stop decides how attractive it is.
				TransitLine bestLine = null;
				var bestWait = int.MaxValue;
				for (var li = 0; li < stop.LineIds.Count; li++)
				{
					var line = GetLine(stop.LineIds[li]);
					if (line == null || !LineRuns(line) || line.Vehicles.Count == 0)
						continue;

					var w = LineWait(line);
					if (w < bestWait)
					{
						bestWait = w;
						bestLine = line;
					}
				}

				if (bestLine == null)
					continue;

				var share = Info.ShareBasePercent - TicketFor(bestLine, stop.Cell) / 20 - bestWait / 60 - (stop.CrowdSnapshot > 30 ? 5 : 0);
				share = Math.Clamp(share, 1, 60) * (100 + (Progression()?.GetPolicy("TransitRidershipPct", stop.Cell) ?? 0)) / 100;
				stop.SampleAccumMilli += stop.Catchment * Info.TripsPerResidentPerDay * 10 * share / pulsesPerDay;
				if (stop.SampleAccumMilli < 1000)
					continue;

				var count = Math.Min(4, stop.SampleAccumMilli / 1000);
				stop.SampleAccumMilli -= count * 1000;
				SpawnAggregateGroup(stop, bestLine, count, now);
			}
		}

		void SpawnAggregateGroup(TransitStop stop, TransitLine line, int count, int now)
		{
			var n = line.StopIds.Count;
			var ia = StopIndex(line, stop.Id);
			if (ia < 0)
				return;

			var valid = 0;
			for (var b = 0; b < n; b++)
				if (b != ia && line.RideTicks[ia * n + b] >= 0)
					valid++;

			if (valid == 0)
				return;

			var pick = Hash(stop.Id, now, 2) % valid;
			var dest = -1;
			for (var b = 0; b < n; b++)
			{
				if (b == ia || line.RideTicks[ia * n + b] < 0)
					continue;

				if (pick-- == 0)
				{
					dest = b;
					break;
				}
			}

			if (dest < 0)
				return;

			if (stop.WaitingCount + count > Info.MaxStopQueue)
			{
				stop.GaveUpThisMonth += count;
				totalGaveUp += count;
				return;
			}

			stop.Waiting.Add(new PaxGroup
			{
				Count = count,
				LineId = line.Id,
				AlightStopId = line.StopIds[dest],
				RideLegs = line.RideLegs[ia * n + dest],
				WalkOutTicks = 100 + Hash(stop.Id, now, 3) % 200,
				WaitStart = now,
				StartTick = now,
			});
			stop.WaitingCount += count;
		}
	}
}
