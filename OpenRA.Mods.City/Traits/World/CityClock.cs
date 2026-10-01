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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[IncludeStaticFluentReferences(typeof(CityFluentKeys))]
	[Desc("The single source of in-game time. A pure function of World.WorldTick (synced, stops while paused).",
		"As in Cities: Skylines 2, one day/night cycle is one calendar month.")]
	public class CityClockInfo : TraitInfo
	{
		[Desc("World ticks per in-game hour.")]
		public readonly int TicksPerHour = 100;

		public readonly int HoursPerDay = 24;

		[Desc("Calendar months per year (one day = one month).")]
		public readonly int MonthsPerYear = 12;

		public readonly int StartYear = 2026;

		[Desc("Calendar month (1..12) at tick 0.")]
		public readonly int StartMonth = 3;

		[Desc("Hour of day (0..23) at tick 0.")]
		public readonly int StartHour = 7;

		[Desc("Ticks between aggregate simulation pulses (CityManager's legacy 'day').")]
		public readonly int PulseTicks = 25;

		public override object Create(ActorInitializer init) { return new CityClock(init.Self, this); }
	}

	public class CityClock
	{
		public readonly CityClockInfo Info;
		readonly World world;

		public CityClock(Actor self, CityClockInfo info)
		{
			Info = info;
			world = self.World;
			TicksPerHour = Math.Max(1, info.TicksPerHour);
			TicksPerDay = TicksPerHour * Math.Max(1, info.HoursPerDay);
			startOffset = Math.Clamp(info.StartHour, 0, info.HoursPerDay - 1) * TicksPerHour;
		}

		readonly int startOffset;

		public int TicksPerHour { get; }

		/// <summary>Ticks per day/night cycle (= one calendar month).</summary>
		public int TicksPerDay { get; }

		public int TicksPerMonth => TicksPerDay;

		public int TicksPerYear => TicksPerDay * Info.MonthsPerYear;

		/// <summary>Ticks elapsed since the start of the game, including the start-hour offset.</summary>
		public int Now => world.WorldTick + startOffset;

		/// <summary>0 .. TicksPerDay-1.</summary>
		public int TickOfDay => Now % TicksPerDay;

		/// <summary>0 .. HoursPerDay-1.</summary>
		public int Hour => TickOfDay / TicksPerHour;

		/// <summary>0 .. 59.</summary>
		public int Minute => TickOfDay % TicksPerHour * 60 / TicksPerHour;

		/// <summary>Minutes since midnight, 0 .. 24*60-1. Handy for schedules.</summary>
		public int MinuteOfDay => Hour * 60 + Minute;

		/// <summary>Whole days (= months) elapsed since the game started.</summary>
		public int DayIndex => Now / TicksPerDay;

		/// <summary>1 .. MonthsPerYear.</summary>
		public int Month => (Info.StartMonth - 1 + DayIndex) % Info.MonthsPerYear + 1;

		public int Year => Info.StartYear + (Info.StartMonth - 1 + DayIndex) / Info.MonthsPerYear;

		/// <summary>0 Spring, 1 Summer, 2 Autumn, 3 Winter (northern hemisphere, 3 months each).</summary>
		public int Season => (Month + 9) % 12 / 3;

		/// <summary>0..99 progress through the current day.</summary>
		public int DayProgressPercent => TickOfDay * 100 / TicksPerDay;

		/// <summary>True on the first tick of a new day (month). Use from synced ITick code to trigger monthly work.</summary>
		public bool IsNewDay => world.WorldTick > 0 && TickOfDay == 0;

		/// <summary>True on the first tick of a new hour.</summary>
		public bool IsNewHour => world.WorldTick > 0 && TickOfDay % TicksPerHour == 0;

		/// <summary>True on aggregate pulse ticks (every PulseTicks).</summary>
		public bool IsPulse => world.WorldTick % Math.Max(1, Info.PulseTicks) == 0;

		/// <summary>True between 06:00 and 20:00.</summary>
		public bool IsDaytime => Hour >= 6 && Hour < 20;

		public CityDate Date => new(Year, Month, 1, 1, Hour, Minute);

		/// <summary>Converts a duration in game hours to ticks.</summary>
		public int HoursToTicks(int hours) => hours * TicksPerHour;
	}
}
