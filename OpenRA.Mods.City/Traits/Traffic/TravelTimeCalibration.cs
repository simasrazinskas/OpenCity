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
	/// <summary>Physical movement calibration, independent of the simulated calendar and selected game speed.</summary>
	public interface ITravelTimeCalibration
	{
		int FreeFlowTicksPerCell(TravelMode mode);
	}

	public static class TravelTimeCalibration
	{
		/// <summary>Thousandths of a base simulation tick to travel one cell at the given physical speed.</summary>
		public static int MilliTicksPerCell(int metersPerCell, int kilometersPerHour, int tickMilliseconds)
		{
			if (metersPerCell <= 0 || kilometersPerHour <= 0 || tickMilliseconds <= 0)
				throw new ArgumentOutOfRangeException(nameof(metersPerCell), "Distance, speed and tick duration must be positive.");

			var numerator = metersPerCell * 3600000L;
			var denominator = (long)kilometersPerHour * tickMilliseconds;
			return checked((int)((numerator + denominator - 1) / denominator));
		}

		public static int ScaleSpeed(int timeUnits, int speedPercent)
		{
			return checked((int)(((long)timeUnits * 100 + Math.Max(1, speedPercent) - 1) / Math.Max(1, speedPercent)));
		}

		/// <summary>Mean wait for a uniformly arriving vehicle during the red half of a two-phase signal.</summary>
		public static int SignalWaitTicks(int cycleTicks, int clearanceTicks)
		{
			var cycle = Math.Max(8, cycleTicks);
			var red = Math.Min(cycle, cycle - cycle / 2 + Math.Max(0, clearanceTicks));
			return (int)(((long)red * red + 2L * cycle - 1) / (2L * cycle));
		}
	}
}
