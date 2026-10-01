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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Fares into the ledger (fares-bus, fares-taxi), running costs out of it (transit-upkeep), monthly line statistics.
	public sealed partial class TransitLayer
	{
		long costMilliPending;

		void PayAndCollect()
		{
			var cm = Funds();
			var ticksPerDay = clock?.TicksPerDay ?? 2400;
			var pulsesPerDay = Math.Max(1, ticksPerDay / 25);

			// Running costs accrue every pulse: vehicles and stops, split over the month.
			var running = 0;
			for (var i = 0; i < vehicles.Count; i++)
				if (vehicles[i].State != TransitVehicleState.Gone)
					running += RunningCents(vehicles[i].Mode);

			costMilliPending += (long)(running + stops.Count * Info.StopUpkeepCents) * 1000 / pulsesPerDay;
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				var cents = line.Vehicles.Count * RunningCents(line.Mode);
				for (var k = 0; k < line.StopIds.Count; k++)
				{
					var stop = GetStop(line.StopIds[k]);
					if (stop != null)
						cents += Info.StopUpkeepCents / Math.Max(1, stop.LineIds.Count);
				}

				line.ThisMonth.CostCents += cents / pulsesPerDay;
			}

			for (var m = 0; m < farePending.Length; m++)
			{
				var dollars = farePending[m] / 100;
				if (dollars <= 0)
					continue;

				farePending[m] -= dollars * 100;
				cm?.AddFunds(dollars, FareKey((TransitMode)m));
			}

			var due = (int)(costMilliPending / 100_000);
			if (due > 0 && cm != null && cm.TrySpend(due, UpkeepKey))
				costMilliPending -= (long)due * 100_000;
			else if (cm == null)
				costMilliPending %= 100_000;
		}

		int RunningCents(TransitMode mode)
		{
			return mode switch
			{
				TransitMode.Metro => Info.MetroRunningCents,
				TransitMode.Tram => Info.TramRunningCents,
				TransitMode.Train => Info.TrainRunningCents,
				_ => Info.VehicleRunningCents,
			};
		}

		/// <summary>Ticket price at a cell: 0 under the free-transit policy (FareFree).</summary>
		int TicketFor(TransitLine line, CPos cell)
		{
			var p = Progression();
			if (p == null)
				return line.TicketCents;

			if (p.GetPolicy("FareFree", cell) != 0)
				return 0;

			// Minimum fare policy: tickets cannot be cheaper than this percent of the default fare.
			var floor = p.GetPolicy("FareMinPct", cell);
			return floor > 0 ? Math.Max(line.TicketCents, Info.DefaultTicketCents * floor / 100) : line.TicketCents;
		}

		ICityStatistics statistics;
		int monthPassengers;

		void RecordStatistics()
		{
			statistics ??= world.WorldActor.TraitsImplementing<ICityStatistics>().FirstOrDefault();
			statistics?.Record("transit.passengers", monthPassengers);
		}

		void MonthlyRollover()
		{
			monthPassengers = 0;
			ArrivalsThisMonth = 0;
			VisitorsThisMonth = 0;
			for (var i = 0; i < lines.Count; i++)
			{
				lines[i].LastMonth = lines[i].ThisMonth;
				lines[i].ThisMonth = default;
			}

			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				s.BoardedLastMonth = s.BoardedThisMonth;
				s.AlightedLastMonth = s.AlightedThisMonth;
				s.GaveUpLastMonth = s.GaveUpThisMonth;
				s.BoardedThisMonth = s.AlightedThisMonth = s.GaveUpThisMonth = 0;
			}
		}

		/// <summary>Line profit in cents for the finished month (fares minus running costs), for the line panel.</summary>
		public static int ProfitCents(in LineStats stats) => stats.FareCents - stats.CostCents;

		/// <summary>Fares per month as percent of costs (0 when there were no costs), for the line panel.</summary>
		public static int CostRecoveryPercent(in LineStats stats) => stats.CostCents <= 0 ? 0 : stats.FareCents * 100 / stats.CostCents;
	}
}
