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
	// Stops, lines, depots and routes. Every mutator here is called from synced code only (order handlers, ticks).
	public sealed partial class TransitLayer
	{
		public const string ErrorMoney = "notification-transit-no-money";
		public const string ErrorNotRoad = "notification-transit-not-road";
		public const string ErrorStopExists = "notification-transit-stop-exists";
		public const string ErrorNoDepot = "notification-transit-no-depot";
		public const string ErrorBadLine = "notification-transit-bad-line";
		public const string ErrorNoStop = "notification-transit-no-stop";
		public const string ErrorNotSupported = "notification-transit-not-supported";
		public const string ErrorNotOwned = "notification-transit-not-owned";
		public const string ErrorLocked = "notification-transit-locked";
		public const string NotifyLineCut = "notification-transit-line-cut";

		/// <summary>Place a stop (bus) or stand (taxi) on a road cell. Returns a fluent error key or null.</summary>
		public string PlaceStop(CPos cell, TransitMode mode, int side, CityManager cm)
		{
			var error = ValidateStop(cell, mode);
			if (error != null)
				return error;

			if (cm != null && !cm.TrySpend(Info.StopCost, ConstructionKey))
				return ErrorMoney;

			if (side < 0 || side > 3)
				side = DefaultSide(cell);

			stops.Add(new TransitStop { Id = nextStopId++, Mode = mode, Cell = cell, Side = side });
			RebuildStopLineIndex();
			Version++;
			return null;
		}

		/// <summary>Why a stop of this mode cannot be placed on the cell now (fluent key), or null.</summary>
		public string ValidateStop(CPos cell, TransitMode mode)
		{
			if (roads == null || !roads.IsRoad(cell))
				return ErrorNotRoad;

			var progress = Progression();
			if (progress != null && !progress.IsCellOwned(cell))
				return ErrorNotOwned;

			if (mode != TransitMode.Bus && mode != TransitMode.Taxi && mode != TransitMode.Tram)
				return ErrorNotSupported;

			if (mode == TransitMode.Tram && !IsTramTrack(cell))
				return ErrorNeedsTrack;

			if (!ModeUnlocked(mode))
				return ErrorLocked;

			return StopAt(cell, mode) != null ? ErrorStopExists : null;
		}

		int DefaultSide(CPos cell)
		{
			// The sign goes on the first side that is not road (the pavement), preferring north/east/south/west in that order.
			for (var d = 0; d < 4; d++)
			{
				var n = cell + CityUtils.Neighbours4[d];
				if (world.Map.Contains(n) && !roads.IsRoad(n))
					return d;
			}

			return 0;
		}

		public string RemoveStop(int stopId, CityManager cm)
		{
			var stop = GetStop(stopId);
			if (stop == null)
				return ErrorNoStop;

			// Station stops go away with their building.
			if (stop.StationActorId != 0)
				return ErrorNotSupported;

			RemoveStopInternal(stop);
			cm?.AddFunds(Info.StopCost / 2, "refund");
			return null;
		}

		void RemoveStopInternal(TransitStop stop)
		{
			var stopId = stop.Id;

			// Waiting passengers lose their ride.
			for (var i = 0; i < stop.Waiting.Count; i++)
				FailGroup(stop.Waiting[i], TripFailure.RoadClosed);

			stop.Waiting.Clear();
			stop.WaitingCount = 0;
			stops.Remove(stop);

			// Taxis headed for this stand lose their booking.
			ReleaseTaxisForStop(stopId);

			for (var i = lines.Count - 1; i >= 0; i--)
			{
				var line = lines[i];
				if (!line.StopIds.Contains(stopId))
					continue;

				line.StopIds.Remove(stopId);
				if (line.StopIds.Count < 2)
					DeleteLineInternal(line);
				else
					LineStructureChanged(line);
			}

			Version++;
			RebuildStopLineIndex();
		}

		/// <summary>Create a line. `stopIds` are existing stop ids of the mode; `vehicles` &lt;= 0 means 1.</summary>
		public string CreateLine(TransitMode mode, bool loop, IList<int> stopIds, int color, string name, int vehicles, CityManager cm, out int lineId)
		{
			lineId = 0;
			if (mode == TransitMode.Taxi)
				return ErrorNotSupported;

			if (!ModeUnlocked(mode))
				return ErrorLocked;

			if (stopIds == null || stopIds.Count < 2)
				return ErrorBadLine;

			var seen = new HashSet<int>();
			for (var i = 0; i < stopIds.Count; i++)
			{
				var s = GetStop(stopIds[i]);
				if (s == null || s.Mode != mode || !seen.Add(s.Id))
					return ErrorBadLine;
			}

			if (NeedsDepot(mode) && !HasDepot(mode))
				return ErrorNoDepot;

			var tunnel = mode == TransitMode.Metro ? TunnelCells(stopIds, loop && stopIds.Count > 2) * Info.MetroTunnelCostPerCell : 0;
			if (cm != null && tunnel > 0 && !cm.TrySpend(tunnel, ConstructionKey))
				return ErrorMoney;

			var line = new TransitLine
			{
				Id = nextLineId++,
				Mode = mode,
				Loop = loop && stopIds.Count > 2,
				Color = Math.Max(0, color),
				TicketCents = Info.DefaultTicketCents,
				TargetVehicles = Math.Max(1, vehicles),
			};
			line.Name = string.IsNullOrWhiteSpace(name) ? $"{(mode == TransitMode.Bus ? "Bus" : "Line")} {line.Id}" : Sanitize(name);
			for (var i = 0; i < stopIds.Count; i++)
				line.StopIds.Add(stopIds[i]);

			lines.Add(line);
			lineId = line.Id;
			RebuildStopLineIndex();
			Version++;
			return null;
		}

		/// <summary>Number of tunnel cells (L-shaped polyline between consecutive stations) a metro line needs.</summary>
		public int TunnelCells(IList<int> stopIds, bool loop)
		{
			var n = stopIds.Count;
			var cells = 0;
			var count = loop ? n : n - 1;
			for (var i = 0; i < count; i++)
			{
				var a = GetStop(stopIds[i]);
				var b = GetStop(stopIds[(i + 1) % n]);
				if (a != null && b != null)
					cells += Math.Abs(a.Cell.X - b.Cell.X) + Math.Abs(a.Cell.Y - b.Cell.Y);
			}

			return cells;
		}

		static string Sanitize(string s)
		{
			s = s.Replace(';', ' ').Replace('=', ' ').Replace(',', ' ').Trim();
			return s.Length > 24 ? s[..24] : s;
		}

		public string EditLine(int lineId, bool loop, IList<int> stopIds, CityManager cm = null)
		{
			var line = GetLine(lineId);
			if (line == null)
				return ErrorBadLine;

			if (stopIds == null || stopIds.Count < 2)
				return ErrorBadLine;

			var seen = new HashSet<int>();
			for (var i = 0; i < stopIds.Count; i++)
			{
				var s = GetStop(stopIds[i]);
				if (s == null || s.Mode != line.Mode || !seen.Add(s.Id))
					return ErrorBadLine;
			}

			if (line.Mode == TransitMode.Metro && cm != null)
			{
				var before = TunnelCells(line.StopIds, line.Loop);
				var after = TunnelCells(stopIds, loop && stopIds.Count > 2);
				if (after > before && !cm.TrySpend((after - before) * Info.MetroTunnelCostPerCell, ConstructionKey))
					return ErrorMoney;
			}

			line.StopIds.Clear();
			for (var i = 0; i < stopIds.Count; i++)
				line.StopIds.Add(stopIds[i]);

			line.Loop = loop && stopIds.Count > 2;
			LineStructureChanged(line);
			return null;
		}

		/// <summary>Set a property of a line. `ticketPercent` is percent of the default fare. Negative numbers leave a value unchanged; `auto` is -1 (keep), 0 or 1.</summary>
		public string SetLine(int lineId, int ticketPercent, int vehicles, int auto, int color, string name)
		{
			var line = GetLine(lineId);
			if (line == null)
				return ErrorBadLine;

			if (ticketPercent >= 0)
			{
				var first = line.StopIds.Count > 0 ? GetStop(line.StopIds[0]) : null;
				ticketPercent = Math.Max(ticketPercent, first != null ? Progression()?.GetPolicy("FareMinPct", first.Cell) ?? 0 : 0);
			}

			if (ticketPercent >= 0)
				line.TicketCents = Math.Min(ticketPercent * Info.DefaultTicketCents / 100, Info.MaxTicketCents);

			if (vehicles >= 0)
				line.TargetVehicles = Math.Min(vehicles, MaxVehiclesFor(line));

			if (auto >= 0)
				line.Auto = auto != 0;

			if (color >= 0)
				line.Color = color;

			if (!string.IsNullOrWhiteSpace(name))
				line.Name = Sanitize(name);

			Version++;
			return null;
		}

		public int MaxVehiclesFor(TransitLine line)
		{
			var byHeadway = line.CycleTicks > 0 ? Math.Max(1, line.CycleTicks / Math.Max(1, Info.MinHeadway)) : 20;
			return Math.Min(byHeadway, 20);
		}

		public void DeleteLine(int lineId)
		{
			var line = GetLine(lineId);
			if (line != null)
				DeleteLineInternal(line);
		}

		void DeleteLineInternal(TransitLine line)
		{
			PurgeLinePassengers(line.Id);
			for (var i = 0; i < line.Vehicles.Count; i++)
				line.Vehicles[i].Recall = true;

			lines.Remove(line);
			RebuildStopLineIndex();
			Version++;
		}

		void LineStructureChanged(TransitLine line)
		{
			PurgeLinePassengers(line.Id);
			line.NeedsRebuild = true;
			for (var i = 0; i < line.Vehicles.Count; i++)
				line.Vehicles[i].Recall = true;

			RebuildStopLineIndex();
			Version++;
		}

		/// <summary>Waiting passengers that planned to use the line lose their ride.</summary>
		void PurgeLinePassengers(int lineId)
		{
			for (var s = 0; s < stops.Count; s++)
			{
				var stop = stops[s];
				for (var i = stop.Waiting.Count - 1; i >= 0; i--)
				{
					var g = stop.Waiting[i];
					if (g.LineId != lineId && g.Line2 != lineId)
						continue;

					stop.WaitingCount -= g.Count;
					stop.Waiting.RemoveAt(i);
					FailGroup(g, TripFailure.RoadClosed);
				}
			}
		}

		void RebuildStopLineIndex()
		{
			for (var i = 0; i < stops.Count; i++)
			{
				stops[i].LineIds.Clear();
				stops[i].NearIds.Clear();
				if (stops[i].Mode == TransitMode.Taxi)
					continue;

				for (var k = 0; k < stops.Count; k++)
				{
					if (k == i || stops[k].Mode == TransitMode.Taxi)
						continue;

					var dist = Math.Abs(stops[i].Cell.X - stops[k].Cell.X) + Math.Abs(stops[i].Cell.Y - stops[k].Cell.Y);
					if (dist <= Info.TransferRadius)
						stops[i].NearIds.Add(stops[k].Id);
				}
			}

			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				for (var k = 0; k < line.StopIds.Count; k++)
					GetStop(line.StopIds[k])?.LineIds.Add(line.Id);
			}
		}

		bool HasDepot(TransitMode mode)
		{
			for (var i = 0; i < depots.Count; i++)
				if (depots[i].Mode == mode)
					return true;

			return false;
		}

		// ---- depots ----
		public void RegisterDepot(Actor actor, TransitMode mode, int capacity)
		{
			var rec = new TransitDepotRecord { ActorId = actor.ActorID, Actor = actor, Mode = mode, Capacity = capacity, Road = DepotRoad(actor) };
			var at = depots.Count;
			while (at > 0 && depots[at - 1].ActorId > rec.ActorId)
				at--;

			depots.Insert(at, rec);
			Version++;
		}

		public void UnregisterDepot(Actor actor)
		{
			for (var i = 0; i < depots.Count; i++)
			{
				if (depots[i].ActorId != actor.ActorID)
					continue;

				var id = depots[i].ActorId;
				for (var v = vehicles.Count - 1; v >= 0; v--)
					if (vehicles[v].DepotId == id)
						RemoveVehicle(vehicles[v]);

				depots.RemoveAt(i);
				Version++;
				CompactVehicles();
				return;
			}
		}

		CPos DepotRoad(Actor actor)
		{
			if (roads == null || actor.OccupiesSpace == null)
				return CPos.Zero;

			var occupied = actor.OccupiesSpace.OccupiedCells();
			var cells = new List<CPos>(occupied.Length);
			for (var i = 0; i < occupied.Length; i++)
				cells.Add(occupied[i].Cell);

			return roads.GetAccessRoad(cells);
		}

		void RefreshDepotRoads()
		{
			for (var i = 0; i < depots.Count; i++)
				depots[i].Road = DepotRoad(depots[i].Actor);
		}

		/// <summary>Nearest depot of a mode with a free slot (by Manhattan distance, ties by actor id), or null.</summary>
		TransitDepotRecord PickDepot(TransitMode mode, CPos near)
		{
			TransitDepotRecord best = null;
			var bestDist = int.MaxValue;
			for (var i = 0; i < depots.Count; i++)
			{
				var d = depots[i];
				if (d.Mode != mode || d.Used >= d.Capacity || d.Road == CPos.Zero)
					continue;

				var dist = Math.Abs(d.Road.X - near.X) + Math.Abs(d.Road.Y - near.Y);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = d;
				}
			}

			return best;
		}

		TransitDepotRecord GetDepot(uint actorId)
		{
			for (var i = 0; i < depots.Count; i++)
				if (depots[i].ActorId == actorId)
					return depots[i];

			return null;
		}

		// ---- routes ----
		void RebuildOneLine()
		{
			for (var i = 0; i < lines.Count; i++)
			{
				if (!lines[i].NeedsRebuild)
					continue;

				RebuildLine(lines[i]);
				return;
			}
		}

		public void RebuildLine(TransitLine line)
		{
			line.NeedsRebuild = false;
			var wasBroken = line.Broken;
			line.Legs.Clear();
			var n = line.StopIds.Count;
			var stopsOnLine = new TransitStop[n];
			for (var i = 0; i < n; i++)
				stopsOnLine[i] = GetStop(line.StopIds[i]);

			var ok = n >= 2 && (RouterFor(line.Mode, false) != null || line.Mode == TransitMode.Metro);
			for (var i = 0; i < n && ok; i++)
				ok = stopsOnLine[i] != null;

			if (!ok)
			{
				line.Broken = true;
				line.CycleTicks = 0;
				line.RideLegs = [];
				line.RideTicks = [];
				if (!wasBroken)
					Version++;

				return;
			}

			var order = new List<int>();
			if (line.Loop)
			{
				for (var i = 0; i < n; i++)
				{
					order.Add(i);
					order.Add((i + 1) % n);
				}
			}
			else
			{
				for (var i = 0; i < n - 1; i++)
				{
					order.Add(i);
					order.Add(i + 1);
				}

				for (var i = n - 1; i > 0; i--)
				{
					order.Add(i);
					order.Add(i - 1);
				}
			}

			var allReachable = true;
			var cycle = 0;
			for (var k = 0; k < order.Count; k += 2)
			{
				var from = stopsOnLine[order[k]];
				var to = stopsOnLine[order[k + 1]];
				var path = Route(line.Mode, from.Cell, to.Cell, false);
				var leg = new TransitLeg
				{
					FromStopId = from.Id,
					ToStopId = to.Id,
					Reachable = path != null,
					Cells = path ?? [],
					Ticks = path == null ? 0 : Math.Max(4, (path.Length - 1) * TicksPerCell(line.Mode)),
				};
				allReachable &= leg.Reachable;
				cycle += leg.Ticks + Info.DwellBase + 8;
				line.Legs.Add(leg);
			}

			line.CycleTicks = cycle;
			line.Broken = !allReachable;
			BuildRideTables(line);
			if (line.Broken != wasBroken)
				Version++;

			if (line.Broken && !wasBroken)
				TextNotificationsManager.AddTransientLine(world.LocalPlayer, NotifyLineCut);

			if (line.Broken)
				for (var i = 0; i < line.Vehicles.Count; i++)
					line.Vehicles[i].Recall = true;
		}

		/// <summary>Fills RideLegs/RideTicks[a * n + b]: the quickest ride from stop index a to b along the vehicle's cycle.</summary>
		void BuildRideTables(TransitLine line)
		{
			var n = line.StopIds.Count;
			var legs = line.Legs.Count;
			line.RideLegs = new int[n * n];
			line.RideTicks = new int[n * n];
			for (var i = 0; i < line.RideLegs.Length; i++)
			{
				line.RideLegs[i] = -1;
				line.RideTicks[i] = -1;
			}

			var index = new Dictionary<int, int>();
			for (var i = 0; i < n; i++)
				index[line.StopIds[i]] = i;

			for (var p = 0; p < legs; p++)
			{
				var a = index[line.Legs[p].ToStopId];
				var ticks = 0;
				for (var j = 1; j <= legs; j++)
				{
					var leg = line.Legs[(p + j) % legs];
					ticks += leg.Ticks + Info.DwellBase;
					var b = index[leg.ToStopId];
					if (a == b)
						continue;

					var at = a * n + b;
					if (line.RideTicks[at] < 0 || ticks < line.RideTicks[at])
					{
						line.RideTicks[at] = ticks;
						line.RideLegs[at] = j;
					}
				}
			}
		}

		/// <summary>Index of a stop id on a line, or -1.</summary>
		static int StopIndex(TransitLine line, int stopId)
		{
			for (var i = 0; i < line.StopIds.Count; i++)
				if (line.StopIds[i] == stopId)
					return i;

			return -1;
		}

		/// <summary>Legs a vehicle that just finished leg `pos` needs to reach `stopId`, or int.MaxValue.</summary>
		static int LegsUntil(TransitLine line, int pos, int stopId)
		{
			var legs = line.Legs.Count;
			for (var j = 1; j <= legs; j++)
				if (line.Legs[(pos + j) % legs].ToStopId == stopId)
					return j;

			return int.MaxValue;
		}

		// ---- data for the UI ----

		/// <summary>Road cells of a line's route for one cycle (consecutive legs, shared cells repeated once).</summary>
		public List<CPos> GetLinePath(TransitLine line)
		{
			var result = new List<CPos>();
			for (var i = 0; i < line.Legs.Count; i++)
			{
				var cells = line.Legs[i].Cells;
				for (var k = result.Count > 0 && cells.Length > 0 && result[^1] == cells[0] ? 1 : 0; k < cells.Length; k++)
					result.Add(cells[k]);
			}

			return result;
		}

		/// <summary>Route preview for the line tool: cells for the given stop sequence (UI-safe, uses a scratch router). Null cells mean an unreachable leg.</summary>
		public List<CPos> PreviewRoute(IList<int> stopIds, bool loop, out bool broken, out int cycleTicks, TransitMode mode = TransitMode.Bus)
		{
			broken = false;
			cycleTicks = 0;
			var result = new List<CPos>();
			if ((RouterFor(mode, true) == null && mode != TransitMode.Metro) || stopIds == null || stopIds.Count < 2)
				return result;

			var n = stopIds.Count;
			var count = loop && n > 2 ? n : n - 1;
			for (var i = 0; i < count; i++)
			{
				var a = GetStop(stopIds[i]);
				var b = GetStop(stopIds[(i + 1) % n]);
				if (a == null || b == null)
				{
					broken = true;
					continue;
				}

				var path = Route(mode, a.Cell, b.Cell, true);
				if (path == null)
				{
					broken = true;
					continue;
				}

				cycleTicks += (path.Length - 1) * TicksPerCell(mode) + Info.DwellBase + 8;
				for (var k = result.Count > 0 && result[^1] == path[0] ? 1 : 0; k < path.Length; k++)
					result.Add(path[k]);
			}

			if (!loop || n <= 2)
				cycleTicks *= 2;

			return result;
		}

		/// <summary>Active (non-recalled) vehicles of a line.</summary>
		public static int ActiveVehicles(TransitLine line)
		{
			var c = 0;
			for (var i = 0; i < line.Vehicles.Count; i++)
				if (!line.Vehicles[i].Recall)
					c++;

			return c;
		}

		/// <summary>Whole-line headway estimate (ticks) with the current vehicle count.</summary>
		public static int Headway(TransitLine line)
		{
			var n = Math.Max(1, ActiveVehicles(line));
			return line.CycleTicks <= 0 ? 0 : line.CycleTicks / n;
		}
	}
}
