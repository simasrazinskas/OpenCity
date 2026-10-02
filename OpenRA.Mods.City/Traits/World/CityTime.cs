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
	/// <summary>Integer conversions shared by schedules and UI. All durations follow simulation ticks, including pause and fast-forward.</summary>
	public static class CityTime
	{
		public const int DefaultTicksPerHour = 7500;

		public static int MinutesToTicks(int minutes, int ticksPerHour)
		{
			return (int)Math.Min(int.MaxValue, ((long)Math.Max(0, minutes) * Math.Max(1, ticksPerHour) + 59) / 60);
		}

		public static int TicksToMinutes(int ticks, int ticksPerHour)
		{
			return (int)Math.Min(int.MaxValue, (long)Math.Max(0, ticks) * 60 / Math.Max(1, ticksPerHour));
		}
	}
}
