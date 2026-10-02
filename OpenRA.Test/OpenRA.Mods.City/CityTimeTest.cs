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

using NUnit.Framework;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class CityTimeTest
	{
		[Test]
		public void DefaultDayLastsTwoRealHoursAtNormalSpeed()
		{
			var info = new CityClockInfo();
			Assert.That((long)info.TicksPerHour * info.HoursPerDay * 40, Is.EqualTo(120L * 60 * 1000));
		}

		[TestCase(0, 3750, 0)]
		[TestCase(1, 3750, 63)]
		[TestCase(60, 3750, 3750)]
		[TestCase(1440, 3750, 90000)]
		[TestCase(1440, 7500, 180000)]
		[TestCase(1, 100, 2)]
		[TestCase(int.MaxValue, 3750, int.MaxValue)]
		public void ActivityDurationsRoundUpAndDoNotOverflow(int minutes, int ticksPerHour, int expected)
		{
			Assert.That(CityTime.MinutesToTicks(minutes, ticksPerHour), Is.EqualTo(expected));
		}

		[TestCase(62, 3750, 0)]
		[TestCase(63, 3750, 1)]
		[TestCase(90000, 3750, 1440)]
		[TestCase(int.MaxValue, 3750, 34359738)]
		public void ElapsedMinutesUseWholeMinutesAndDoNotOverflow(int ticks, int ticksPerHour, int expected)
		{
			Assert.That(CityTime.TicksToMinutes(ticks, ticksPerHour), Is.EqualTo(expected));
		}
	}
}
