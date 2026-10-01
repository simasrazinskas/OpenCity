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
	/// <summary>
	/// Read side for the population and tourism simulations: visitors arriving by intercity train. A train station counts as connected
	/// when its rail reaches the map border. Owner: PT. See the integration notes (CIT / PRG feed these into tourism and immigration).
	/// </summary>
	public interface IIntercityRail
	{
		/// <summary>Train stations whose rail network reaches the map border.</summary>
		int ConnectedStations { get; }

		/// <summary>Road-side cells of the connected stations, in stop id order (arrival points for visitors).</summary>
		IReadOnlyList<CPos> ConnectedStationCells { get; }

		int ArrivalsThisMonth { get; }

		/// <summary>Visitors (immigrants and tourists) brought by intercity trains this month and in total.</summary>
		int VisitorsThisMonth { get; }
		int VisitorsTotal { get; }
	}

	// Train specifics: level crossings, intercity arrivals.
	public sealed partial class TransitLayer : IIntercityRail
	{
		CellLayer<int> crossingClosedUntil;
		readonly List<CPos> connectedCells = [];

		public int ConnectedStations { get; private set; }

		public IReadOnlyList<CPos> ConnectedStationCells => connectedCells;

		public int ArrivalsThisMonth { get; private set; }

		public int VisitorsThisMonth { get; private set; }

		public int VisitorsTotal { get; private set; }

		/// <summary>True while a train is at or approaching this level crossing. The traffic sim must make road vehicles wait.</summary>
		public bool IsCrossingClosed(CPos cell)
		{
			return crossingClosedUntil != null && crossingClosedUntil.Contains(cell) && crossingClosedUntil[cell] > world.WorldTick;
		}

		/// <summary>Positions of a train's engine and cars (index 0 = engine) for rendering. Cars trail the engine along the track.</summary>
		public void GetTrainPoses(TransitVehicle v, int now, int units, List<(WPos Pos, WAngle Facing)> into)
		{
			var cells = v.LegCells;
			var map = world.Map;
			if (cells.Length == 0)
				return;

			if (cells.Length == 1)
			{
				for (var k = 0; k < units; k++)
					into.Add((map.CenterOfCell(cells[0]), WAngle.Zero));

				return;
			}

			var moving = v.Virtual && (v.State == TransitVehicleState.Leg || v.State == TransitVehicleState.ToDepot);
			var head = moving
				? (long)Math.Clamp(now - v.LegStartTick, 0, v.LegTicks) * (cells.Length - 1) * 1024 / Math.Max(1, v.LegTicks)
				: (cells.Length - 1) * 1024L;
			for (var k = 0; k < units; k++)
			{
				var t = Math.Max(0, head - k * 1000L);
				var i = Math.Min(cells.Length - 2, (int)(t / 1024));
				var frac = (int)(t - i * 1024L);
				var a = map.CenterOfCell(cells[i]);
				var b = map.CenterOfCell(cells[i + 1]);
				into.Add((WPos.Lerp(a, b, frac, 1024), (b - a).Yaw));
			}
		}

		void UpdateCrossings(int now)
		{
			if (rail == null)
				return;

			crossingClosedUntil ??= new CellLayer<int>(world.Map);
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (v.Mode != TransitMode.Train || !v.Virtual || v.LegCells.Length < 2 || v.State == TransitVehicleState.Gone)
					continue;

				var cells = v.LegCells;
				var at = (int)((long)Math.Clamp(now - v.LegStartTick, 0, v.LegTicks) * (cells.Length - 1) / Math.Max(1, v.LegTicks));
				for (var k = Math.Max(0, at - 3); k <= Math.Min(cells.Length - 1, at + 5); k++)
					if (rail.IsCrossing(cells[k]))
						crossingClosedUntil[cells[k]] = now + 3;
			}
		}

		void ManageIntercity(int now)
		{
			ConnectedStations = 0;
			connectedCells.Clear();
			if (rail == null)
				return;

			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				if (s.Mode != TransitMode.Train || !rail.IsConnectedToEdge(s.Cell))
					continue;

				ConnectedStations++;
				connectedCells.Add(s.Cell);
				if (Info.IntercityIntervalTicks <= 0 || vehicles.Count >= Info.MaxVehicles)
					continue;

				if (s.NextIntercityTick == 0)
					s.NextIntercityTick = now + Info.IntercityIntervalTicks / 2;

				if (now < s.NextIntercityTick)
					continue;

				s.NextIntercityTick = now + Info.IntercityIntervalTicks;
				SpawnIntercity(s, now);
			}
		}

		void SpawnIntercity(TransitStop station, int now)
		{
			if (railRouter == null)
				return;

			// Enter at the nearest border cell that is connected to the station.
			var border = rail.BorderCells;
			CPos[] best = null;
			for (var i = 0; i < border.Count; i++)
			{
				var path = railRouter.FindPath(border[i], station.Cell);
				if (path != null && (best == null || path.Length < best.Length))
					best = path;
			}

			if (best == null)
				return;

			var v = new TransitVehicle
			{
				Id = nextVehicleId++,
				Mode = TransitMode.Train,
				Intercity = true,
				StopId = station.Id,
				Cell = best[0],
				LastDeparture = now,
			};
			vehicles.Add(v);
			StartMove(v, best, TransitVehicleState.Leg, now);
			Version++;
		}

		void OnIntercityArrived(TransitVehicle v, int now)
		{
			ArrivalsThisMonth++;
			VisitorsThisMonth += Info.IntercityPassengers;
			VisitorsTotal += Info.IntercityPassengers;
			v.State = TransitVehicleState.Dwell;
			v.DwellUntil = now + Info.DwellMax;
		}

		void DepartIntercity(TransitVehicle v, int now)
		{
			var back = new CPos[v.LegCells.Length];
			for (var i = 0; i < back.Length; i++)
				back[i] = v.LegCells[back.Length - 1 - i];

			v.Recall = true;
			StartMove(v, back, TransitVehicleState.ToDepot, now);
		}
	}
}
