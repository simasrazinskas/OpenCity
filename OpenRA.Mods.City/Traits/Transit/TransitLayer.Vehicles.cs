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
	// Line vehicles: spawn at a depot, drive stop-to-stop legs (traffic trips, or a deterministic virtual timer when no
	// traffic service accepts them), alight/board/dwell at stops, return to the depot when recalled.
	public sealed partial class TransitLayer
	{
		const int TripPending = -1;
		bool vehiclesGone;

		/// <summary>Metro and trains appear at the first station; they need no depot.</summary>
		static bool NeedsDepot(TransitMode mode) => mode != TransitMode.Metro && mode != TransitMode.Train;

		/// <summary>Metro and trains never use the traffic sim.</summary>
		static bool DataOnly(TransitMode mode) => mode == TransitMode.Metro || mode == TransitMode.Train;

		int TicksPerCell(TransitMode mode)
		{
			return mode switch
			{
				TransitMode.Metro => Info.MetroTicksPerCell,
				TransitMode.Tram => Info.TramTicksPerCell,
				TransitMode.Train => Info.TrainTicksPerCell,
				_ => Info.VirtualTicksPerCell,
			};
		}

		int Capacity(TransitMode mode)
		{
			return mode switch
			{
				TransitMode.Taxi => Info.TaxiCapacity,
				TransitMode.Metro => Info.MetroCapacity,
				TransitMode.Tram => Info.TramCapacity,
				TransitMode.Train => Info.TrainCapacity,
				_ => Info.BusCapacity,
			};
		}

		void ManageFleets(int now)
		{
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				if (line.Broken || line.NeedsRebuild || line.LegCount == 0)
					continue;

				var active = ActiveVehicles(line);
				var target = Math.Min(line.TargetVehicles, MaxVehiclesFor(line));
				if (active < target && vehicles.Count < Info.MaxVehicles)
				{
					var spacing = Math.Max(Info.SpawnInterval, line.CycleTicks / Math.Max(1, target));
					if (now - line.LastSpawnTick >= spacing && SpawnVehicle(line, now))
						line.LastSpawnTick = now;
				}
				else if (active > target)
				{
					for (var k = line.Vehicles.Count - 1; k >= 0; k--)
					{
						if (!line.Vehicles[k].Recall)
						{
							line.Vehicles[k].Recall = true;
							break;
						}
					}
				}
			}

			ManageTaxiFleet(now);
			ManageIntercity(now);
		}

		bool SpawnVehicle(TransitLine line, int now)
		{
			var first = GetStop(line.StopIds[0]);
			if (first == null)
				return false;

			// Metro and trains need no depot: they appear at the first station.
			var depot = NeedsDepot(line.Mode) ? PickDepot(line.Mode, first.Cell) : null;
			if (depot == null && NeedsDepot(line.Mode))
				return false;

			var path = !NeedsDepot(line.Mode) ? [first.Cell] : router.FindPath(depot.Road, first.Cell);
			if (path == null)
				return false;

			var v = new TransitVehicle
			{
				Id = nextVehicleId++,
				Mode = line.Mode,
				LineId = line.Id,
				DepotId = depot?.ActorId ?? 0,
				Pos = line.LegCount - 1,
				LegIndex = -1,
				Cell = depot?.Road ?? first.Cell,
				StopId = first.Id,
				LastDeparture = now,
			};
			if (depot != null)
				depot.Used++;

			vehicles.Add(v);
			line.Vehicles.Add(v);
			StartMove(v, path, TransitVehicleState.Leg, now);
			Version++;
			return true;
		}

		void TickVehicles(int now)
		{
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				switch (v.State)
				{
					case TransitVehicleState.Leg:
					case TransitVehicleState.ToDepot:
					case TransitVehicleState.ToPickup:
					case TransitVehicleState.Carrying:
						if (v.Virtual && now - v.LegStartTick >= v.LegTicks)
							HandleArrival(v, now);

						break;
					case TransitVehicleState.Dwell:
						if (now >= v.DwellUntil)
							DepartStop(v, now);

						break;
					case TransitVehicleState.WaitingPassenger:
						TickWaitingTaxi(v, now);
						break;
				}
			}

			UpdateCrossings(now);
			CompactVehicles();
		}

		void CompactVehicles()
		{
			if (!vehiclesGone)
				return;

			vehiclesGone = false;
			vehicles.RemoveAll(v => v.State == TransitVehicleState.Gone);
		}

		/// <summary>Begin moving along `path` (road cells, first = current cell). Asks the traffic service first; falls back to the virtual timer.</summary>
		void StartMove(TransitVehicle v, CPos[] path, TransitVehicleState state, int now)
		{
			v.State = state;
			v.Virtual = false;
			v.LegCells = path;
			v.LegStartTick = now;
			v.LegTicks = Math.Max(4, (path.Length - 1) * TicksPerCell(v.Mode));
			v.Cell = path[0];
			v.ChunkStart = 0;
			v.ChunkEnd = -1;
			var dest = path[^1];
			if (v.Mode == TransitMode.Tram && traffic != null && path.Length > 2)
			{
				// Trams are sent to the traffic sim a few cells at a time so they stay on their track.
				v.ChunkEnd = NextChunkEnd(path, 0);
				dest = path[v.ChunkEnd];
			}

			if (traffic != null && !DataOnly(v.Mode))
			{
				v.TripId = TripPending;
				var request = new TripRequest
				{
					Purpose = TripPurpose.Transit,
					AllowedModes = TravelModes.Transit,
					AgeGroup = AgeGroup.Adult,
					OwnerId = v.Id,
					OriginRoad = path[0],
					DestinationRoad = dest,
					DepartTick = now,
					VehicleType = ModeName(v.Mode),
				};

				var id = traffic.RequestTrip(request, this);
				if (id > 0)
				{
					TrafficDrivesVehicles = true;
					if (v.TripId == TripPending)
						v.TripId = id;

					return;
				}

				// A synchronous arrival callback may already have moved the vehicle on.
				if (v.TripId != TripPending)
					return;

				v.TripId = 0;
			}

			v.ChunkEnd = -1;
			v.Virtual = true;
		}

		/// <summary>End index of the next traffic trip chunk of a tram: the next corner or `TramChunkCells` further, at most the end of the path.</summary>
		int NextChunkEnd(CPos[] path, int from)
		{
			var last = path.Length - 1;
			var limit = Math.Min(last, from + Math.Max(2, Info.TramChunkCells));
			for (var i = from + 1; i < limit; i++)
			{
				var a = path[i] - path[i - 1];
				var b = path[i + 1] - path[i];
				if (a != b)
					return i;
			}

			return limit;
		}

		/// <summary>Sends the next chunk of a tram's leg to the traffic sim. Returns false when it fell back to the virtual timer.</summary>
		void ContinueChunk(TransitVehicle v, int now)
		{
			var path = v.LegCells;
			v.ChunkStart = v.ChunkEnd;
			var end = NextChunkEnd(path, v.ChunkStart);
			var request = new TripRequest
			{
				Purpose = TripPurpose.Transit,
				AllowedModes = TravelModes.Transit,
				AgeGroup = AgeGroup.Adult,
				OwnerId = v.Id,
				OriginRoad = path[v.ChunkStart],
				DestinationRoad = path[end],
				DepartTick = now,
				VehicleType = ModeName(v.Mode),
			};

			v.TripId = TripPending;
			v.ChunkEnd = end;
			var id = traffic.RequestTrip(request, this);
			if (id > 0)
			{
				if (v.TripId == TripPending)
					v.TripId = id;

				return;
			}

			if (v.TripId != TripPending)
				return;

			v.TripId = 0;
			FallbackVirtual(v, now);
		}

		/// <summary>Finish the rest of the leg with the virtual timer.</summary>
		void FallbackVirtual(TransitVehicle v, int now)
		{
			if (v.ChunkEnd >= 0)
			{
				var rest = new CPos[v.LegCells.Length - v.ChunkStart];
				Array.Copy(v.LegCells, v.ChunkStart, rest, 0, rest.Length);
				v.LegCells = rest;
				v.LegTicks = Math.Max(4, (rest.Length - 1) * TicksPerCell(v.Mode));
				v.ChunkStart = 0;
				v.ChunkEnd = -1;
			}

			v.Virtual = true;
			v.LegStartTick = now;
		}

		void ITripListener.OnTripArrived(in TripResult result)
		{
			var v = GetVehicle(result.OwnerId);
			if (v == null || v.State == TransitVehicleState.Gone)
				return;

			if (v.TripId != result.TripId && v.TripId != TripPending)
				return;

			v.TripId = 0;
			HandleArrival(v, world.WorldTick);
		}

		void ITripListener.OnTripFailed(int tripId, int ownerId, TripFailure reason)
		{
			var v = GetVehicle(ownerId);
			if (v == null || v.State == TransitVehicleState.Gone || (v.TripId != tripId && v.TripId != TripPending))
				return;

			// Fall back to the virtual timer so lines keep working while traffic cannot route this leg.
			v.TripId = 0;
			v.FailCount++;
			FallbackVirtual(v, world.WorldTick);
		}

		void HandleArrival(TransitVehicle v, int now)
		{
			v.TripId = 0;
			v.Virtual = false;
			if (v.ChunkEnd >= 0 && v.ChunkEnd < v.LegCells.Length - 1)
			{
				v.Cell = v.LegCells[v.ChunkEnd];
				ContinueChunk(v, now);
				return;
			}

			v.ChunkEnd = -1;
			if (v.LegCells.Length > 0)
				v.Cell = v.LegCells[^1];

			switch (v.State)
			{
				case TransitVehicleState.ToDepot:
					RemoveVehicle(v);
					return;
				case TransitVehicleState.ToPickup:
				case TransitVehicleState.Carrying:
				case TransitVehicleState.Idle:
					HandleTaxiArrival(v, now);
					return;
			}

			if (v.Intercity)
			{
				OnIntercityArrived(v, now);
				return;
			}

			var line = GetLine(v.LineId);
			if (line == null || line.LegCount == 0)
			{
				GoToDepot(v, now);
				return;
			}

			v.Pos = v.LegIndex < 0 ? line.LegCount - 1 : v.LegIndex % line.LegCount;
			var stopId = v.LegIndex < 0 ? line.StopIds[0] : line.Legs[v.Pos].ToStopId;
			var stop = GetStop(stopId);
			if (stop == null)
			{
				GoToDepot(v, now);
				return;
			}

			ArriveAtStop(v, line, stop, now);
		}

		void ArriveAtStop(TransitVehicle v, TransitLine line, TransitStop stop, int now)
		{
			v.State = TransitVehicleState.Dwell;
			v.Cell = stop.Cell;
			v.StopId = stop.Id;

			var alighted = 0;
			var keep = 0;
			for (var i = 0; i < v.Pax.Count; i++)
			{
				var g = v.Pax[i];
				if (g.AlightStopId != stop.Id)
				{
					v.Pax[keep++] = g;
					continue;
				}

				alighted += g.Count;
				AlightGroup(g, stop, now);
			}

			v.Pax.RemoveRange(keep, v.Pax.Count - keep);
			v.PaxCount -= alighted;
			stop.AlightedThisMonth += alighted;
			totalAlightings += alighted;

			var boarded = 0;
			if (!v.Recall && !line.Broken)
				boarded = BoardGroups(v, line, stop, now);

			var capacity = Math.Max(1, Capacity(v.Mode));
			var load = v.PaxCount * 100 / capacity;
			line.ThisMonth.LoadSum += load;
			line.ThisMonth.LoadSamples++;
			line.AutoLoadSum += load;
			line.AutoLoadSamples++;

			var dwell = Math.Min(Info.DwellMax, Info.DwellBase + Info.DwellPerPassenger * (boarded + alighted));
			v.DwellUntil = now + dwell;
		}

		int BoardGroups(TransitVehicle v, TransitLine line, TransitStop stop, int now)
		{
			var boarded = 0;
			var free = Capacity(v.Mode) - v.PaxCount;
			var i = 0;
			while (i < stop.Waiting.Count && free > 0)
			{
				var g = stop.Waiting[i];
				if (g.LineId != line.Id || LegsUntil(line, v.Pos, g.AlightStopId) > g.RideLegs)
				{
					i++;
					continue;
				}

				var take = Math.Min(g.Count, free);
				if (take < g.Count && g.Listener != null)
				{
					i++;
					continue;
				}

				var riding = g;
				riding.Count = take;
				var ticket = TicketFor(line, stop.Cell);
				riding.FarePaid += ticket;
				if (take == g.Count)
				{
					stop.Waiting.RemoveAt(i);
				}
				else
				{
					g.Count -= take;
					stop.Waiting[i] = g;
					i++;
				}

				stop.WaitingCount -= take;
				free -= take;
				boarded += take;
				v.Pax.Add(riding);
				v.PaxCount += take;

				var waited = Math.Max(0, now - riding.WaitStart);
				var fare = ticket * take;
				line.ThisMonth.Passengers += take;
				line.ThisMonth.FareCents += fare;
				line.ThisMonth.Boardings += take;
				line.ThisMonth.WaitTicks += (long)waited * take;
				line.ObservedWait += (waited - line.ObservedWait) / 8;
				stop.BoardedThisMonth += take;
				TotalBoardings += take;
				monthPassengers += take;
				TotalFareCents += fare;
				farePending[(int)line.Mode] += fare;
			}

			return boarded;
		}

		void AlightGroup(PaxGroup g, TransitStop stop, int now)
		{
			if (g.Line2 != 0)
			{
				var next = g;
				next.LineId = g.Line2;
				next.AlightStopId = g.Alight2Stop;
				next.RideLegs = g.RideLegs2;
				next.Line2 = 0;
				PushEvent(now + Math.Max(1, g.TransferWalkTicks), EventEnqueue, g.Board2Stop, next);
				return;
			}

			PushEvent(now + Math.Max(1, g.WalkOutTicks), EventComplete, stop.Id, g);
		}

		void DepartStop(TransitVehicle v, int now)
		{
			if (v.Intercity)
			{
				DepartIntercity(v, now);
				return;
			}

			var line = GetLine(v.LineId);
			if (line == null || line.Broken || v.Recall || line.LegCount == 0)
			{
				GoToDepot(v, now);
				return;
			}

			var next = (v.Pos + 1) % line.LegCount;
			var leg = line.Legs[next];
			if (!leg.Reachable)
			{
				GoToDepot(v, now);
				return;
			}

			v.LegIndex = next;
			v.StopId = leg.ToStopId;
			v.LastDeparture = now;
			StartMove(v, leg.Cells, TransitVehicleState.Leg, now);
		}

		void GoToDepot(TransitVehicle v, int now)
		{
			v.Recall = true;
			DumpPassengers(v);
			var depot = GetDepot(v.DepotId);
			if (depot == null || depot.Road == CPos.Zero || router == null)
			{
				RemoveVehicle(v);
				return;
			}

			if (v.Cell == depot.Road)
			{
				RemoveVehicle(v);
				return;
			}

			var path = router.FindPath(v.Cell, depot.Road);
			if (path == null)
			{
				RemoveVehicle(v);
				return;
			}

			StartMove(v, path, TransitVehicleState.ToDepot, now);
		}

		static void DumpPassengers(TransitVehicle v)
		{
			for (var i = 0; i < v.Pax.Count; i++)
				FailGroup(v.Pax[i], TripFailure.RoadClosed);

			v.Pax.Clear();
			v.PaxCount = 0;
		}

		void RemoveVehicle(TransitVehicle v)
		{
			if (v.State == TransitVehicleState.Gone)
				return;

			if (v.TripId > 0)
				traffic?.CancelTrip(v.TripId);

			v.TripId = 0;
			DumpPassengers(v);
			if (v.HasFare)
			{
				FailGroup(v.Fare, TripFailure.Cancelled);
				v.HasFare = false;
			}

			var home = GetDepot(v.DepotId);
			if (home != null && home.Used > 0)
				home.Used--;

			GetLine(v.LineId)?.Vehicles.Remove(v);
			v.State = TransitVehicleState.Gone;
			vehiclesGone = true;
			Version++;
		}

		static void FailGroup(PaxGroup g, TripFailure reason)
		{
			g.Listener?.OnTripFailed(g.JourneyId, g.OwnerId, reason);
		}

		void AutoTuneFleets(int now)
		{
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				if (!line.Auto || now - line.LastAutoTick < Info.AutoInterval)
					continue;

				line.LastAutoTick = now;
				if (line.AutoLoadSamples >= 3)
				{
					var usage = line.AutoLoadSum / line.AutoLoadSamples;
					if (usage > Info.AutoUpUsage && line.TargetVehicles < MaxVehiclesFor(line))
						line.TargetVehicles++;
					else if (usage < Info.AutoDownUsage && line.TargetVehicles > 1)
						line.TargetVehicles--;
				}

				line.AutoLoadSum = 0;
				line.AutoLoadSamples = 0;
			}
		}
	}
}
